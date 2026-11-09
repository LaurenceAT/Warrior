using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

// The Bringer of Death: jefe nuevo, SOLO en la sala de pruebas (no sale en
// niveles, Desafios, Bitacora, Bestiario, logros ni guardado).
// Sus numeros: Assets/Data/Jefes/Ajustes Bringer of Death. Sprites y prefab:
// Warrior > Bringer of Death > Crear o actualizar.
//
// Una sola barra de vida. Al bajar de la mitad (marcaFase) pasa a la fase 2: un
// momento invulnerable conjurando, va mas rapido y suma hechizos nuevos.
//
// Fase 1: guadanazo (a veces doble, el segundo con retraso al azar), mano del
// abismo (portal que te sigue), media luna (baja o alta), ola de almas por el
// suelo, desvanecerse (reaparece a tu espalda) y cosecha (manos por toda la
// arena con huecos libres).
// Fase 2: ademas circulo de condena, espiritus, lluvia de cometas (toda la
// arena), esfera del vacio (especial: congelacion), juicio (toda la arena, por
// tramos alternos) y marea de almas (olas desde las dos paredes).
//
// Parar un guadanazo lo hace tambalearse; dos parrys seguidos lo aturden.
// El sagrado es su debilidad y la oscuridad le hace poco.
public partial class JefeBringer : JefeBase, IModificadorDano, IAfinidadElemental, IAturdible
{
    [SerializeField] private AjustesBringer ajustes;

    public const string Guadana = "guadana", Mano = "mano", MediaLuna = "mediaLuna", Ola = "ola", Desvanecer = "desvanecer",
                        Cosecha = "cosecha", Circulo = "circulo", Espiritus = "espiritus", Cometas = "cometas", Esfera = "esfera",
                        Juicio = "juicio", Marea = "marea";
    public static readonly string[] Todos = { Guadana, Mano, MediaLuna, Ola, Desvanecer, Cosecha, Circulo, Espiritus, Cometas, Esfera, Juicio, Marea };
    public static readonly string[] SoloFase2 = { Circulo, Espiritus, Cometas, Esfera, Juicio, Marea };
    public static readonly string[] DeArena = { Cosecha, Cometas, Juicio, Marea };

    // Clips del cuerpo.
    public const string ClipQuieto = "quieto", ClipAndar = "andar", ClipGuadana = "guadana", ClipConjurar = "conjurar",
                        ClipConjurarBucle = "conjurarBucle", ClipGolpeado = "golpeado", ClipMuerte = "muerte",
                        ClipDesvanecer = "desvanecer", ClipAparecer = "aparecer";

    private Rect arena;
    private float suelo;
    private bool fase2, transformando, entrado, invulnerable, oculto;
    private string ataqueForzado, ultimoElegido;
    private float ultimoArena = -99f;
    private int parrysSeguidos;
    private float aturdimientoPendiente;

    private SpriteRenderer cuerpo;
    private MaterialPropertyBlock bloqueAviso;
    private CinemachineImpulseSource impulso;

    // Lo que leen las pruebas.
    public AjustesBringer Ajustes => ajustes;
    public bool Fase2 => fase2;
    public bool Transformando => transformando;
    public bool Oculto => oculto;
    public readonly List<string> Hechos = new List<string>();
    public readonly List<string> Aturdimientos = new List<string>();
    public readonly List<PlayerControler.ResultadoDano?> ResultadosGuadana = new List<PlayerControler.ResultadoDano?>();
    public string EnCurso { get; private set; }
    public float UltimoAviso { get; private set; }
    public int CambiosDeFase { get; private set; }

    private float Vel => fase2 ? ajustes.multVelocidadFase2 : 1f;

    // ------------------------------------------------------------------ Sala de pruebas

    public override string[] AtaquesPrueba => Todos;
    public override void ForzarAtaque(string ataque) => ataqueForzado = ataque;

    // No cae desde arriba (la arena puede tener techo): aparece ya en el suelo,
    // en el sitio donde lo pone la arena.
    public override void Configurar(Rect zona, float alturaSuelo)
    {
        arena = zona;
        suelo = alturaSuelo;
        float y = PeligrosJefe.SueloBajo(new Vector2(transform.position.x, alturaSuelo + 2f), alturaSuelo);
        Vector2 p = new Vector2(transform.position.x, y + 0.05f);
        transform.position = p;
        if (rb != null) { rb.position = p; rb.linearVelocity = Vector2.zero; }
    }

    // ------------------------------------------------------------------ Vida y dano

    protected override void Awake()
    {
        base.Awake();
        radioDeteccion = radioOlvido = 60f;
        armadura = true;
        salud.Inamovible = true;
        cuerpo = visual != null ? visual.GetComponent<SpriteRenderer>() : null;
        bloqueAviso = new MaterialPropertyBlock();
        impulso = GetComponent<CinemachineImpulseSource>();
        // Una sola barra: en la fase 1 no puede morir; si un golpe la vaciara de
        // golpe, se queda en la marca y empieza la fase 2.
        salud.PuedeMorir = () => fase2;
        salud.AlAgotarse += () =>
        {
            if (fase2) return;
            salud.Revivir(salud.MaxHealth);
            salud.QuitarVidaFija(salud.MaxHealth - VidaMarca, 1);
            EmpezarFase2();
        };
    }

    protected override void Start()
    {
        salud.Revivir(VidaDesafio(ajustes.vida));
        base.Start();
    }

    private int VidaMarca => Mathf.Max(1, Mathf.FloorToInt(salud.MaxHealth * ajustes.marcaFase));

    public int Modificar(int dano, TipoArma arma) => invulnerable || transformando || oculto ? 0 : dano;

    public float Multiplicador(Elemento e) => ajustes.Afinidad(e, fase2);

    public bool PedirAturdimiento(float segundos)
    {
        if (transformando || oculto || salud.Muerto || aturdimientoPendiente > 0f) return false;
        aturdimientoPendiente = segundos;
        return true;
    }

    protected override void Update()
    {
        base.Update();
        if (!fase2 && !transformando && !muerto && salud.CurrentHealth > 0 && salud.CurrentHealth <= VidaMarca) EmpezarFase2();
    }

    // ------------------------------------------------------------------ Cerebro

    protected override IEnumerator Cerebro()
    {
        if (!entrado) yield return Entrada();
        bool primeraFase2 = fase2;
        while (true)
        {
            float pausa = primeraFase2 ? ajustes.pausaInicialFase2 : Random.Range(Pausa.x, Pausa.y);
            primeraFase2 = false;
            yield return Quieto(pausa);
            string id = Elegir();
            if (id == null) continue;
            EnCurso = id;
            AnunciarAtaque(id);
            if (System.Array.IndexOf(DeArena, id) >= 0) ultimoArena = Time.time;
            yield return Hacer(id);
            Hechos.Add(id);
            EnCurso = null;
            if (aturdimientoPendiente > 0f)
            {
                float s = aturdimientoPendiente;
                aturdimientoPendiente = 0f;
                yield return Aturdido(s, "sagrado");
            }
        }
    }

    private Vector2 Pausa => fase2 ? ajustes.pausaFase2 : ajustes.pausaFase1;

    private IEnumerator Hacer(string id)
    {
        switch (id)
        {
            case Guadana: yield return Guadanazos(); break;
            case Mano: yield return Manos(); break;
            case MediaLuna: yield return MediasLunas(); break;
            case Ola: yield return OlaAlmas(false); break;
            case Desvanecer: yield return Desvanecerse(); break;
            case Cosecha: yield return CosechaManos(); break;
            case Circulo: yield return Circulos(); break;
            case Espiritus: yield return LanzarEspiritus(); break;
            case Cometas: yield return LluviaCometas(); break;
            case Esfera: yield return EsferaDelVacio(); break;
            case Juicio: yield return JuicioFinal(); break;
            case Marea: yield return OlaAlmas(true); break;
        }
    }

    // El forzado de la sala (si es de la fase 2 y aun no ha llegado, lo hace igual:
    // es para probarlo). Si no, al azar por frecuencia, nunca el mismo dos veces
    // seguidas y los de toda la arena con su enfriamiento.
    private string Elegir()
    {
        if (!string.IsNullOrEmpty(ataqueForzado))
        {
            string f = ataqueForzado;
            ataqueForzado = null;
            if (System.Array.IndexOf(Todos, f) >= 0) { ultimoElegido = f; return f; }
        }
        AjustesBringer.Frecuencias q = fase2 ? ajustes.fase2 : ajustes.fase1;
        var opciones = new List<(string, float)>
        {
            (Guadana, q.guadana), (Mano, q.mano), (MediaLuna, q.mediaLuna), (Ola, q.ola), (Desvanecer, q.desvanecer), (Cosecha, q.cosecha),
        };
        if (fase2)
            opciones.AddRange(new[] { (Circulo, q.circulo), (Espiritus, q.espiritus), (Cometas, q.cometas), (Esfera, q.esfera), (Juicio, q.juicio), (Marea, q.marea) });
        BuscarPlayer();
        bool cerca = Mathf.Abs(DxPlayer) < ajustes.alcanceGuadana + 1.5f;
        float total = 0f;
        for (int i = 0; i < opciones.Count; i++)
        {
            (string id, float peso) = opciones[i];
            if (id == ultimoElegido) peso = 0f;
            if (System.Array.IndexOf(DeArena, id) >= 0 && Time.time - ultimoArena < ajustes.enfriamientoArena) peso = 0f;
            if (id == Espiritus && EspirituNecro.Vivos > 0) peso = 0f;
            // De cerca prefiere la guadana; de lejos, los hechizos.
            if (id == Guadana) peso *= cerca ? 1.6f : 0.6f;
            opciones[i] = (id, Mathf.Max(0f, peso));
            total += opciones[i].Item2;
        }
        if (total <= 0f) return ultimoElegido = Guadana;
        float r = Random.value * total;
        foreach ((string id, float peso) in opciones)
        {
            r -= peso;
            if (r <= 0f && peso > 0f) return ultimoElegido = id;
        }
        return ultimoElegido = Guadana;
    }

    // Entrada: se forma desde el humo en el suelo de la arena (invulnerable
    // mientras); entonces salen su nombre y su barra.
    private IEnumerator Entrada()
    {
        CambiarEstado(EstadoEnemigo.Alerta);
        BuscarPlayer();
        MirarYa();
        oculto = true;
        anim.Reproducir(ClipAparecer, true, 0.8f);
        Sonar("teletransporte", 0.8f, 0.7f);
        while (!anim.Terminado) yield return null;
        anim.Reproducir(ClipQuieto, true);
        oculto = false;
        for (float t = 0f; t < 2f && !EnSuelo; t += Time.deltaTime) yield return null;
        entrado = true;
        Sacudir(1f);
        Sonar("jefe_aparicion");
        AvisarAterrizaje();
    }

    private IEnumerator Quieto(float segundos)
    {
        CambiarEstado(EstadoEnemigo.Alerta);
        anim.Reproducir(ClipQuieto, false, 1f);
        segundos = PausaPrueba(PausaDesafio(segundos / Vel));
        for (float t = 0f; t < segundos; t += Time.deltaTime)
        {
            BuscarPlayer();
            MirarAlPlayer();
            Parar();
            yield return null;
        }
    }

    // Anda hacia ti hasta tenerte a su alcance (o hasta el borde de la arena).
    private IEnumerator Acercarse(float alcance)
    {
        BuscarPlayer();
        MirarYa();
        if (Mathf.Abs(DxPlayer) <= alcance) yield break;
        CambiarEstado(EstadoEnemigo.Reposicion);
        anim.Reproducir(ClipAndar, false, Vel);
        for (float t = 0f; t < ajustes.tiempoMaxAndar && Mathf.Abs(DxPlayer) > alcance; t += Time.deltaTime)
        {
            MirarAlPlayer();
            if (!DentroDeArena(mirada)) break;
            rb.linearVelocity = new Vector2(mirada * ajustes.velocidadAndar * Vel, rb.linearVelocity.y);
            yield return null;
        }
        Parar();
        MirarYa();
    }

    private void Parar() => rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

    private bool DentroDeArena(int dir)
    {
        if (arena.width <= 0f) return true;
        float x = transform.position.x;
        return dir > 0 ? x < arena.xMax - ajustes.margenArena : x > arena.xMin + ajustes.margenArena;
    }

    // ------------------------------------------------------------------ Guadanazo

    private IEnumerator Guadanazos()
    {
        yield return Acercarse(ajustes.alcanceGuadana);
        yield return UnGuadanazo(ajustes.avisoGuadana, 0f);
        PlayerControler.ResultadoDano? r = ResultadosGuadana.Count > 0 ? ResultadosGuadana[ResultadosGuadana.Count - 1] : null;
        // Si lo has parado, se tambalea (o queda aturdido) y no hay segundo.
        if (r == PlayerControler.ResultadoDano.Parry) { yield return TrasParry(); yield break; }
        if (Random.value < (fase2 ? ajustes.dobleFase2 : ajustes.dobleFase1))
        {
            BuscarPlayer();
            MirarYa();
            yield return UnGuadanazo(ajustes.avisoGuadana * 0.7f, Random.Range(ajustes.retrasoSegundo.x, ajustes.retrasoSegundo.y));
            r = ResultadosGuadana[ResultadosGuadana.Count - 1];
            if (r == PlayerControler.ResultadoDano.Parry) { yield return TrasParry(); yield break; }
        }
        parrysSeguidos = 0;
        yield return Esperar(ajustes.recuperacionGuadana / Vel);
    }

    // Un tajo: alza la guadana (cuadros 0-3), la sostiene el aviso (mas el retraso
    // del segundo tajo) y corta en los cuadros 4-5 con su caja propia.
    private IEnumerator UnGuadanazo(float aviso, float retraso)
    {
        Parar();
        MirarYa();
        CambiarEstado(EstadoEnemigo.Anticipacion);
        anim.Reproducir(ClipGuadana, true, Vel);
        float inicio = Time.time;
        Sonar("jefe_carga_tajo", 0.7f, 0.85f);
        StartCoroutine(Avisar(ajustes.colorAviso, aviso + retraso));
        while (anim.Fotograma < 3 && !anim.Terminado) yield return null;
        float queda = aviso + retraso - (Time.time - inicio);
        if (queda > 0f)
        {
            anim.Reproducir(ClipGuadana, false, 0f);
            yield return Esperar(queda);
            anim.Reproducir(ClipGuadana, false, Vel);
        }
        while (anim.Fotograma < 4 && !anim.Terminado) yield return null;
        UltimoAviso = Time.time - inicio;
        Sonar("jefe_tajo_fuerte", 0.9f, 0.85f);
        Sacudir(0.5f);
        PlayerControler.ResultadoDano? r = null;
        while (!r.HasValue && anim.Fotograma <= 5 && !anim.Terminado)
        {
            r = Golpear(ajustes.cajaGuadanaCentro, ajustes.cajaGuadanaTamano, ajustes.danoGuadana, PlayerControler.TipoDano.Fisico);
            if (PeligrosBringer.VerCajas)
                CajaDebug.Mostrar((Vector2)transform.position + new Vector2(ajustes.cajaGuadanaCentro.x * mirada, ajustes.cajaGuadanaCentro.y),
                                  ajustes.cajaGuadanaTamano, ajustes.colorAviso, 0.03f);
            yield return null;
        }
        ResultadosGuadana.Add(r);
        if (r == PlayerControler.ResultadoDano.Parry) yield break;
        while (!anim.Terminado) yield return null;
    }

    private IEnumerator TrasParry()
    {
        parrysSeguidos++;
        Sonar("jefe_parry", 0.8f);
        if (parrysSeguidos >= 2)
        {
            parrysSeguidos = 0;
            yield return Aturdido(ajustes.aturdidoDobleParry, "parry");
        }
        else
        {
            CambiarEstado(EstadoEnemigo.Golpeado);
            anim.Reproducir(ClipGolpeado, true);
            yield return Esperar(ajustes.tambaleoParry);
        }
    }

    private IEnumerator Aturdido(float segundos, string tipo)
    {
        CambiarEstado(EstadoEnemigo.Golpeado);
        Parar();
        Aturdimientos.Add(tipo);
        anim.Reproducir(ClipGolpeado, true);
        TextoFlotante.Mostrar("Aturdido", (Vector2)transform.position + Vector2.up * 3f, new Color(1f, 0.9f, 0.5f), 1f);
        Vector3 base0 = visual.localPosition;
        for (float t = 0f; t < segundos; t += Time.deltaTime)
        {
            if (anim.Terminado) anim.Reproducir(ClipGolpeado, false, 0f);
            visual.localPosition = base0 + (Vector3)(Random.insideUnitCircle * 0.04f * (1f - t / segundos));
            yield return null;
        }
        visual.localPosition = base0;
    }

    // ------------------------------------------------------------------ Desvanecerse

    // Se deshace en humo, reaparece a tu espalda (dentro de la arena) y tajo con
    // un aviso mas corto. Mientras esta oculto no recibe dano.
    private IEnumerator Desvanecerse()
    {
        Parar();
        CambiarEstado(EstadoEnemigo.Anticipacion);
        anim.Reproducir(ClipDesvanecer, true, Vel);
        Sonar("teletransporte", 0.8f, 0.8f);
        while (!anim.Terminado) yield return null;
        oculto = true;
        if (cuerpo != null) cuerpo.enabled = false;
        rb.simulated = false;
        yield return Esperar(ajustes.tiempoOculto / Vel);
        BuscarPlayer();
        PlayerControler pc = player != null ? player.GetComponent<PlayerControler>() : null;
        float lado = pc != null && pc.Direccion != 0 ? -Mathf.Sign(pc.Direccion) : (DxPlayer > 0f ? 1f : -1f);
        // A tu espalda; si no cabe, delante.
        float x = PosPlayer.x + lado * ajustes.distanciaEspalda;
        if (arena.width > 0f && (x < arena.xMin + ajustes.margenArena || x > arena.xMax - ajustes.margenArena)) x = PosPlayer.x - lado * ajustes.distanciaEspalda;
        if (arena.width > 0f) x = Mathf.Clamp(x, arena.xMin + ajustes.margenArena, arena.xMax - ajustes.margenArena);
        float y = PeligrosJefe.SueloBajo(new Vector2(x, PosPlayer.y + 1.5f), suelo);
        transform.position = new Vector2(x, y + 0.05f);
        rb.simulated = true;
        Parar();
        if (cuerpo != null) cuerpo.enabled = true;
        MirarYa();
        anim.Reproducir(ClipAparecer, true, Vel * 1.4f);
        Sonar("teletransporte", 0.6f, 1.1f);
        while (!anim.Terminado) yield return null;
        oculto = false;
        yield return UnGuadanazo(ajustes.avisoEspalda, 0f);
        PlayerControler.ResultadoDano? r = ResultadosGuadana[ResultadosGuadana.Count - 1];
        if (r == PlayerControler.ResultadoDano.Parry) yield return TrasParry();
        else yield return Esperar(ajustes.recuperacionGuadana / Vel);
    }

    // ------------------------------------------------------------------ Fase 2

    private void EmpezarFase2()
    {
        if (fase2 || transformando || muerto) return;
        StopAllCoroutines();
        StartCoroutine(CambioDeFase());
    }

    // Conjura invulnerable, la arena se limpia, la pantalla se tine y vuelve mas rapido.
    private IEnumerator CambioDeFase()
    {
        transformando = true;
        QuitarAviso();
        Destello(Color.white, 0f);
        PeligrosJefe.LimpiarTodo();
        oculto = false;
        if (cuerpo != null) cuerpo.enabled = true;
        rb.simulated = true;
        if (visual != null) visual.localPosition = Vector3.zero;
        Parar();
        CambiarEstado(EstadoEnemigo.Recuperacion);
        AvisarSilencio();
        anim.Reproducir(ClipConjurar, true);
        Sonar("jefe_transformacion");
        float t = 0f;
        while (!anim.Terminado) { t += Time.deltaTime; yield return null; }
        anim.Reproducir(ClipConjurarBucle, true);
        ScreenFlash.Destello(new Color(0.55f, 0.2f, 0.9f, 0.4f), 0.5f);
        Sacudir(1.6f);
        if (fxEstrellas != null)
            for (int i = 0; i < 4; i++)
                EfectoVisual.Crear(fxEstrellas, (Vector2)transform.position + new Vector2((i - 1.5f) * 1.2f, 0f), 1f, ajustes.colorEstrellas);
        for (; t < ajustes.transicion; t += Time.deltaTime) yield return null;
        fase2 = true;
        CambiosDeFase++;
        AvisarCambioFase();
        transformando = false;
        Pensar();
    }

    // ------------------------------------------------------------------ Ayudas

    private IEnumerator Esperar(float segundos)
    {
        for (float t = 0f; t < segundos; t += Time.deltaTime) yield return null;
    }

    private IEnumerator Avisar(Color c, float duracion)
    {
        for (float t = 0f; t < duracion; t += Time.deltaTime)
        {
            // Late cada vez mas rapido hasta el golpe.
            float pulso = 0.5f - 0.5f * Mathf.Cos(t * Mathf.Lerp(3f, 9f, t / Mathf.Max(0.01f, duracion)) * Mathf.PI * 2f);
            Destello(c, ajustes.intensidadAviso * (0.35f + 0.65f * pulso));
            yield return null;
        }
        Destello(c, 0f);
    }

    private static readonly int IdFlashColor = Shader.PropertyToID("_FlashColor");
    private static readonly int IdFlashAmount = Shader.PropertyToID("_FlashAmount");
    private void Destello(Color c, float cantidad)
    {
        if (cuerpo == null) return;
        cuerpo.GetPropertyBlock(bloqueAviso);
        bloqueAviso.SetColor(IdFlashColor, c);
        bloqueAviso.SetFloat(IdFlashAmount, cantidad);
        cuerpo.SetPropertyBlock(bloqueAviso);
    }

    private void Sacudir(float fuerza)
    {
        if (impulso != null) impulso.GenerateImpulse(fuerza);
    }

    // ------------------------------------------------------------------ Muerte

    protected override void AlMorir()
    {
        base.AlMorir();
        oculto = false;
        if (cuerpo != null) cuerpo.enabled = true;
        if (rb != null) rb.simulated = true;
        if (visual != null) visual.localPosition = Vector3.zero;
        Destello(Color.white, 0f);
        PeligrosJefe.LimpiarTodo();
        Sonar("jefe_muerte");
        AvisarDerrota();
    }
}
