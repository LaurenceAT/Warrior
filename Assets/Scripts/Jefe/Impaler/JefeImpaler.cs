using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// El Impaler: lancero con fuego dibujado en sus ataques. Jefe nuevo, SOLO en la
// sala de pruebas (no sale en niveles, Desafios, Bitacora, Bestiario ni logros).
// Sus numeros: Assets/Data/Jefes/Ajustes Impaler. Sprites y prefab: Warrior > Impaler.
//
// Maquina de estados (CambiarEstado):
//   quieto (Alerta) -> anda hacia ti (Reposicion) -> senal (Anticipacion)
//   -> golpe (Ataque) -> resto del dibujo (Recuperacion) -> quieto...
//
// Cada ataque es una lista de golpes de su ficha. Cada golpe tiene su senal (el
// aviso de color con su ritmo: naranja que parpadea rapido el ligero, rojo que
// late lento el pesado), sus cuadros con caja de golpe y su caja propia (no la
// del dibujo: el fuego dibujado es enorme). Un golpe solo toca una vez y nunca
// hay dos golpes activos a la vez.
//
// Combos (varios ataques seguidos): siguen aunque pares sus golpes. Parar todos
// los que te alcanzan lo aturde al final; acumular parrys seguidos le rompe la
// postura. Cada parry seguido suena mas agudo y suelta chispas (con reserva).
//
// Resiste el fuego y el hielo, y la oscuridad es su debilidad.
public partial class JefeImpaler : JefeBase, IModificadorDano, IAfinidadElemental, IAturdible
{
    [SerializeField] private AjustesImpaler ajustes;

    private const float Fps = 10f;

    private Rect arena;
    private float suelo;
    private bool fase2, transformando, invulnerable, entrado;
    private string ataqueForzado, ultimoElegido;

    public AjustesImpaler Ajustes => ajustes;
    public bool Fase2 => fase2;
    public bool Transformando => transformando;
    // El aviso real del ultimo golpe (de la senal al golpe, segundos): lo leen las pruebas.
    public float UltimoAvisoReal { get; private set; }
    public AjustesImpaler.Golpe UltimoGolpe { get; private set; }
    // Un golpe se activa (golpe y aviso real): lo escuchan las pruebas.
    public event System.Action<AjustesImpaler.Golpe, float> AlActivarGolpe;
    public event System.Action<AjustesImpaler.Golpe, PlayerControler.ResultadoDano?> AlTerminarGolpe;
    // Golpes con la caja activa ahora (nunca mas de uno) y la ultima ventana de castigo.
    public int GolpesActivos { get; private set; }
    // El golpe que esta avisando o golpeando ahora (null entre golpes) y hacia donde mira.
    public AjustesImpaler.Golpe GolpeEnCurso { get; private set; }
    public int Mirada => mirada;
    public float UltimoCastigo { get; private set; }
    private float finGolpeAnterior = -99f;
    // Resultado de cada golpe de la ultima secuencia (ataque o combo; null = no te toco).
    public readonly List<PlayerControler.ResultadoDano?> Resultados = new List<PlayerControler.ResultadoDano?>();

    // Combo en curso: golpes, parrys y dano que te ha hecho; golpes seguidos sin
    // pausa de recuperacion; parrys seguidos (aunque sean de combos distintos).
    private int golpesSecuencia, parrysSecuencia, tocadosSecuencia, danoSecuencia, seguidos, parrysSeguidos;
    private float aturdimientoPendiente;
    // Tono del sonido del parry: sube con cada parry seguido.
    public float TonoParry { get; private set; } = 1f;
    private float ultimoParry = -99f;
    // Lo que leen las pruebas: secuencias hechas, dano de la ultima y aturdimientos.
    public readonly List<string> SecuenciasHechas = new List<string>();
    public int DanoUltimaSecuencia { get; private set; }
    public readonly List<(string tipo, float segundos)> Aturdimientos = new List<(string, float)>();
    public int ParrysSeguidos => parrysSeguidos;
    // Cada aviso: el golpe, su color y su ritmo (parpadeos por segundo).
    public event System.Action<AjustesImpaler.Golpe, Color, float> AlAvisar;

    // Guardia del contraataque y golpes que le has dado en ella (en toda la pelea).
    private bool enGuardia;
    public bool EnGuardia => enGuardia;
    public int GolpesEnGuardia { get; private set; }

    private SpriteRenderer srImpaler;
    private MaterialPropertyBlock bloqueAviso;
    private Coroutine avisoRitmo;
    private EfectosImpaler efectos;

    private float Velocidad => fase2 ? ajustes.multVelocidadFase2 : 1f;

    // ------------------------------------------------------------------ Sala de pruebas

    public override string[] AtaquesPrueba
    {
        get
        {
            var l = new List<string>();
            if (ajustes == null) return l.ToArray();
            foreach (AjustesImpaler.Ataque a in ajustes.ataques) if (a != null && !string.IsNullOrEmpty(a.id)) l.Add(a.id);
            foreach (AjustesImpaler.Combo c in ajustes.combos) if (c != null && !string.IsNullOrEmpty(c.id)) l.Add(c.id);
            return l.ToArray();
        }
    }

    public override void ForzarAtaque(string ataque) => ataqueForzado = ataque;

    public override void Configurar(Rect zona, float alturaSuelo)
    {
        arena = zona;
        suelo = alturaSuelo;
    }

    // ------------------------------------------------------------------ Vida y dano

    protected override void Awake()
    {
        base.Awake();
        radioDeteccion = radioOlvido = 60f;
        armadura = true;
        salud.Inamovible = true;
        srImpaler = visual != null ? visual.GetComponent<SpriteRenderer>() : null;
        bloqueAviso = new MaterialPropertyBlock();
        efectos = gameObject.AddComponent<EfectosImpaler>();
        TonoParry = ajustes.tonoInicial;
        // Le pegas en la guardia del contraataque: la cuenta la usa el contraataque.
        salud.AlRecibirGolpe += () => { if (enGuardia) GolpesEnGuardia++; };
        salud.PuedeMorir = () => fase2;
        salud.AlAgotarse += () => { if (!transformando) { StopAllCoroutines(); StartCoroutine(FinFase1()); } };
    }

    protected override void Start()
    {
        salud.Revivir(VidaDesafio(ajustes.vidaFase1));
        base.Start();
    }

    // Los golpes no entran mientras cae y renace; en la guardia del contraataque, menos.
    public int Modificar(int dano, TipoArma arma)
    {
        if (invulnerable || transformando) return 0;
        return enGuardia ? Mathf.RoundToInt(dano * ajustes.danoEnGuardia) : dano;
    }

    public float Multiplicador(Elemento e) => ajustes.Afinidad(e);

    // ------------------------------------------------------------------ Cerebro

    protected override IEnumerator Cerebro()
    {
        if (!entrado) yield return Entrada();
        while (true)
        {
            // Tras renacer, la pausa inicial de la fase 2.
            yield return Quieto(pausaInicial ? ajustes.pausaInicialFase2 : Random.Range(Pausa.x, Pausa.y));
            pausaInicial = false;
            string id = Elegir(pasos);
            if (pasos.Count == 0) continue;
            yield return Acercarse(pasos[0].ataque.alcance);
            AnunciarAtaque(id);
            yield return HacerSecuencia(id, pasos);
            AprenderParrys();
            yield return TrasSecuencia();
        }
    }

    private Vector2 Pausa => fase2 ? ajustes.pausaFase2 : ajustes.pausaFase1;

    // Un paso de la secuencia: un ataque y hasta que golpe (-1 = todos).
    private struct Paso
    {
        public AjustesImpaler.Ataque ataque;
        public int hasta;
        public int Golpes => hasta < 0 ? ataque.golpes.Count : Mathf.Min(hasta + 1, ataque.golpes.Count);
    }
    private readonly List<Paso> pasos = new List<Paso>();

    // El forzado desde la sala (ataque o combo); si no, un combo (a veces) o un
    // ataque suelto, al azar por frecuencia y nunca el mismo dos veces seguidas.
    private string Elegir(List<Paso> lista)
    {
        lista.Clear();
        if (!string.IsNullOrEmpty(ataqueForzado))
        {
            string f = ataqueForzado;
            ataqueForzado = null;
            if (Llenar(f, lista)) { ultimoElegido = f; return f; }
        }
        var combos = ajustes.combos.FindAll(c => c != null && c.frecuencia > 0f && (fase2 || !c.soloFase2) && c.id != ultimoElegido && ComboValido(c));
        string id = null;
        if (combos.Count > 0 && Random.value < ajustes.probabilidadCombo)
            id = AlAzar(combos, c => c.frecuencia)?.id;
        if (id == null)
        {
            var ataques = ajustes.ataques.FindAll(a => a != null && a.frecuencia > 0f && a.golpes.Count > 0);
            if (ataques.Count > 1) ataques.RemoveAll(a => a.id == ultimoElegido);
            id = AlAzar(ataques, Frecuencia)?.id;
        }
        if (id == null || !Llenar(id, lista)) return null;
        ultimoElegido = id;
        return id;
    }

    private static T AlAzar<T>(List<T> l, System.Func<T, float> peso) where T : class
    {
        float total = 0f;
        foreach (T x in l) total += peso(x);
        float r = Random.value * total;
        foreach (T x in l) { r -= peso(x); if (r <= 0f) return x; }
        return l.Count > 0 ? l[l.Count - 1] : null;
    }

    private bool ComboValido(AjustesImpaler.Combo c) =>
        c.eslabones.Count > 0 && c.eslabones.TrueForAll(e => e != null && ajustes.Buscar(e.ataque) != null);

    private bool Llenar(string id, List<Paso> lista)
    {
        AjustesImpaler.Ataque a = ajustes.Buscar(id);
        if (a != null && a.golpes.Count > 0) { lista.Add(new Paso { ataque = a, hasta = -1 }); return true; }
        AjustesImpaler.Combo c = ajustes.BuscarCombo(id);
        if (c == null || !ComboValido(c)) return false;
        foreach (AjustesImpaler.Eslabon e in c.eslabones) lista.Add(new Paso { ataque = ajustes.Buscar(e.ataque), hasta = e.hastaGolpe });
        // Dificil: los golpes extra del combo, al final (con las mismas reglas).
        if (EsDificil && c.extraDificil != null)
            foreach (AjustesImpaler.Eslabon e in c.extraDificil)
                if (e != null && ajustes.Buscar(e.ataque) != null) lista.Add(new Paso { ataque = ajustes.Buscar(e.ataque), hasta = e.hastaGolpe });
        return true;
    }

    // Entrada: cae en la arena; al tocar el suelo salen su nombre y su barra.
    private IEnumerator Entrada()
    {
        CambiarEstado(EstadoEnemigo.Alerta);
        anim.Reproducir("quieto");
        BuscarPlayer();
        MirarYa();
        while (!EnSuelo) yield return null;
        entrado = true;
        Sonar("jefe_aparicion");
        AvisarAterrizaje();
    }

    // Quieto: te mira y espera (mas corto en Dificil).
    private IEnumerator Quieto(float segundos)
    {
        CambiarEstado(EstadoEnemigo.Alerta);
        Animar("quieto", false, 1f);
        segundos = PausaPrueba(PausaDesafio(segundos));
        for (float t = 0f; t < segundos; t += Time.deltaTime)
        {
            BuscarPlayer();
            MirarAlPlayer();
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
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
        Animar("andar", false, Velocidad);
        for (float t = 0f; t < ajustes.tiempoMaxAndar && Mathf.Abs(DxPlayer) > alcance; t += Time.deltaTime)
        {
            MirarAlPlayer();
            if (!DentroDeArena(mirada)) break;
            rb.linearVelocity = new Vector2(mirada * ajustes.velocidadAndar * Velocidad, rb.linearVelocity.y);
            yield return null;
        }
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        MirarYa();
    }

    private bool DentroDeArena(int dir)
    {
        if (arena.width <= 0f) return true;
        float x = transform.position.x;
        return dir > 0 ? x < arena.xMax - ajustes.margenArena : x > arena.xMin + ajustes.margenArena;
    }

    // ------------------------------------------------------------------ Combos y parry

    // Un ataque suelto o un combo. El combo sigue aunque pares sus golpes; nunca
    // hay mas de maxGolpesSeguidos golpes sin una pausa de recuperacion, y lo que
    // te quita en total nunca pasa de topeDanoCombo de tu vida maxima.
    private IEnumerator HacerSecuencia(string id, List<Paso> lista)
    {
        Resultados.Clear();
        golpesSecuencia = parrysSecuencia = tocadosSecuencia = danoSecuencia = seguidos = 0;
        finGolpeAnterior = -99f;
        int total = 0;
        foreach (Paso p in lista) total += p.Golpes;
        int indice = 0;
        for (int i = 0; i < lista.Count; i++)
        {
            Paso p = lista[i];
            if (i > 0)
            {
                if (seguidos + p.Golpes > ajustes.maxGolpesSeguidos)
                {
                    yield return Recuperarse(ajustes.pausaRecuperacion);
                    seguidos = 0;
                }
                yield return Acercarse(p.ataque.alcance);
            }
            yield return HacerAtaque(p.ataque, p.hasta, i == lista.Count - 1, indice, total);
            indice += p.Golpes;
        }
        SecuenciasHechas.Add(id);
        DanoUltimaSecuencia = danoSecuencia;
    }

    // Pausa de recuperacion dentro de un combo: quieto, mirandote.
    private IEnumerator Recuperarse(float segundos)
    {
        CambiarEstado(EstadoEnemigo.Recuperacion);
        Animar("quieto", true, 1f);
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        for (float t = 0f; t < segundos; t += Time.deltaTime) { MirarAlPlayer(); yield return null; }
    }

    // Al acabar: la postura rota (muchos parrys seguidos), el combo perfecto (todos
    // sus golpes parados) o el aturdimiento del sagrado que pidio el jugador.
    private IEnumerator TrasSecuencia()
    {
        bool postura = parrysSeguidos >= ajustes.parrysParaRomper;
        // Perfecto: ningun golpe te toco (ni recibido ni bloqueado) y paraste al menos dos.
        bool perfecto = golpesSecuencia >= 2 && parrysSecuencia >= 2 && tocadosSecuencia == 0;
        float sagrado = aturdimientoPendiente;
        aturdimientoPendiente = 0f;
        if (postura) { parrysSeguidos = 0; yield return Aturdido(ajustes.aturdidoPostura, "postura"); }
        else if (perfecto) yield return Aturdido(ajustes.aturdidoComboPerfecto, "combo");
        else if (sagrado > 0f) yield return Aturdido(sagrado, "sagrado");
    }

    public bool PedirAturdimiento(float segundos)
    {
        if (invulnerable || transformando || salud.Muerto || aturdimientoPendiente > 0f) return false;
        aturdimientoPendiente = segundos;
        return true;
    }

    private IEnumerator Aturdido(float segundos, string tipo)
    {
        CambiarEstado(EstadoEnemigo.Golpeado);
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        Animar("quieto", true, 0f);
        Aturdimientos.Add((tipo, segundos));
        bool postura = tipo == "postura";
        TextoFlotante.Mostrar(postura ? ajustes.textoPostura : ajustes.textoAturdido, (Vector2)transform.position + Vector2.up * 3.4f,
                              ajustes.colorTextoAturdido, postura ? 1.3f : 1f);
        if (postura) Sonar("jefe_postura");
        Vector3 base0 = visual.localPosition;
        for (float t = 0f; t < segundos; t += Time.deltaTime)
        {
            visual.localPosition = base0 + (Vector3)(Random.insideUnitCircle * ajustes.temblorAturdido * (1f - t / segundos));
            yield return null;
        }
        visual.localPosition = base0;
        Animar("quieto", false, 1f);
    }

    // Lo que pasa tras cada golpe: cuenta para el combo, el tono del parry y las chispas.
    private void Registrar(AjustesImpaler.Golpe g, PlayerControler.ResultadoDano? r)
    {
        Resultados.Add(r);
        golpesSecuencia++;
        seguidos++;
        if (r == PlayerControler.ResultadoDano.Parry)
        {
            parrysSecuencia++;
            parrysSeguidos++;
            Sonar("jefe_parry", 0.8f, TonoParry);
            Vector2 punto = player != null ? (Vector2)player.position + new Vector2(-mirada * 0.35f, 0.9f) : (Vector2)transform.position;
            if (efectos != null) efectos.Chispas(punto, ajustes.chispas, ajustes.colorChispas, ajustes.colorChispas2, mirada > 0 ? 20f : 160f);
            if (ajustes.paradaImagen > 0f) StartCoroutine(ParadaImagen(ajustes.paradaImagen));
            TonoParry = Mathf.Min(ajustes.tonoMaximo, TonoParry + ajustes.subidaTono);
            ultimoParry = Time.time;
            return;
        }
        parrysSeguidos = 0;
        bool tocado = r == PlayerControler.ResultadoDano.Recibido || r == PlayerControler.ResultadoDano.Bloqueado;
        if (tocado) tocadosSecuencia++;
        if (ajustes.tonoReiniciaAlFallar && tocado) TonoParry = ajustes.tonoInicial;
    }

    // Parada de imagen extra: espera a que acabe la del jugador (si la hay) y para un instante.
    private IEnumerator ParadaImagen(float segundos)
    {
        for (float t = 0f; t < 0.4f && Time.timeScale < 0.99f; t += Time.unscaledDeltaTime) yield return null;
        if (Time.timeScale < 0.99f) yield break;
        Time.timeScale = 0.05f;
        yield return new WaitForSecondsRealtime(segundos);
        if (Mathf.Approximately(Time.timeScale, 0.05f)) Time.timeScale = 1f;
    }

    protected override void Update()
    {
        base.Update();
        if (ajustes != null && ajustes.tonoReiniciaSegundos > 0f && Time.time - ultimoParry > ajustes.tonoReiniciaSegundos)
            TonoParry = ajustes.tonoInicial;
    }

    // Aviso de color con ritmo propio: el ligero parpadea rapido, el pesado late
    // lento y mas fuerte (asi no depende solo del color).
    private void Avisar(AjustesImpaler.Golpe g, float duracion)
    {
        bool pesado = g.peso == AjustesImpaler.Peso.Pesado;
        Color c = ajustes.ColorAviso(g);
        float hz = ajustes.Parpadeo(g);
        if (avisoRitmo != null) StopCoroutine(avisoRitmo);
        avisoRitmo = StartCoroutine(AvisoRitmo(c, duracion, hz, pesado));
        AlAvisar?.Invoke(g, c, hz);
    }

    private IEnumerator AvisoRitmo(Color c, float duracion, float hz, bool pesado)
    {
        for (float t = 0f; t < duracion; t += Time.deltaTime)
        {
            // Sin parpadeo (el contraataque): fijo.
            float pulso = hz <= 0f ? 1f
                        : pesado ? 0.5f - 0.5f * Mathf.Cos(t * hz * Mathf.PI * 2f) : (Mathf.Repeat(t * hz, 1f) < 0.5f ? 1f : 0.2f);
            Destello(c, ajustes.intensidadAviso * (0.3f + 0.7f * pulso));
            yield return null;
        }
        Destello(c, 0f);
        avisoRitmo = null;
    }

    private void QuitarAvisoRitmo()
    {
        if (avisoRitmo != null) { StopCoroutine(avisoRitmo); avisoRitmo = null; }
        Destello(Color.white, 0f);
    }

    private static readonly int IdFlashColor = Shader.PropertyToID("_FlashColor");
    private static readonly int IdFlashAmount = Shader.PropertyToID("_FlashAmount");
    private void Destello(Color c, float cantidad)
    {
        if (srImpaler == null) return;
        srImpaler.GetPropertyBlock(bloqueAviso);
        bloqueAviso.SetColor(IdFlashColor, c);
        bloqueAviso.SetFloat(IdFlashAmount, cantidad);
        srImpaler.SetPropertyBlock(bloqueAviso);
    }

    // ------------------------------------------------------------------ Golpes

    // Un ataque (o sus primeros golpes, en un combo): su clip y sus golpes en orden.
    // El dibujo sigue entre golpes. Si es el ultimo paso, el clip termina, la
    // ventana de castigo (si la tiene) y la recuperacion; si no, pasa al siguiente.
    // "indice" y "total": posicion de sus golpes en la secuencia (el ultimo es el final).
    private IEnumerator HacerAtaque(AjustesImpaler.Ataque a, int hasta, bool ultimoPaso, int indice, int total)
    {
        if (a.contraataque) { yield return Contraataque(a); yield break; }
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        MirarYa();
        Animar(a.clip, true, Velocidad);
        destinoSalto = float.NaN;
        Coroutine mover = a.moverDesde >= 0 ? StartCoroutine(Mover(a)) : null;
        int fin = hasta < 0 ? a.golpes.Count - 1 : Mathf.Min(hasta, a.golpes.Count - 1);
        bool cancelado = false;
        for (int i = 0; i <= fin && !cancelado; i++)
        {
            yield return HacerGolpe(a, a.golpes[i], i, indice + i == total - 1 && total > 1, indice + i == total - 1);
            // Fase 1: parar ese golpe corta el resto del ataque (el combo sigue).
            cancelado = !fase2 && a.golpes[i].parryCancelaFase1 && i < fin && Resultados.Count > 0
                        && Resultados[Resultados.Count - 1] == PlayerControler.ResultadoDano.Parry;
        }
        CambiarEstado(EstadoEnemigo.Recuperacion);
        if (cancelado || !ultimoPaso)
        {
            if (mover != null) StopCoroutine(mover);
            TerminarVuelo();
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            if (!ultimoPaso) yield break;
            Animar("quieto", true, 1f);
        }
        else
        {
            Animar(a.clip, false, Velocidad);
            float castigo = -1f;
            while (!anim.Terminado)
            {
                if (a.cuadroCastigo >= 0 && castigo < 0f && anim.Fotograma >= a.cuadroCastigo) castigo = Time.time;
                yield return null;
            }
            // Ventana de castigo: el ultimo cuadro se sostiene hasta su minimo.
            if (a.cuadroCastigo >= 0)
            {
                if (castigo < 0f) castigo = Time.time;
                while (Time.time - castigo < a.castigoMinimo) yield return null;
                UltimoCastigo = Time.time - castigo;
            }
            if (mover != null) StopCoroutine(mover);
            TerminarVuelo();
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        }
        float rec = a.recuperacion / Velocidad;
        for (float t = 0f; t < rec; t += Time.deltaTime) yield return null;
    }

    // Contraataque blanco: guardia con la lanza sobre la cabeza (primer cuadro,
    // sostenido) en la que le entra menos dano. Si le pegas, tras su aviso blanco
    // (el retraso minimo) responde con el tajo blanco hacia arriba; si lo paras,
    // queda aturdido un buen rato. Si no le pegas, la guardia se acaba sola.
    private IEnumerator Contraataque(AjustesImpaler.Ataque a)
    {
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        MirarYa();
        Animar(a.clip, true, 0f);
        CambiarEstado(EstadoEnemigo.Anticipacion);
        enGuardia = true;
        int golpes = GolpesEnGuardia;
        for (float t = 0f; t < ajustes.duracionGuardia && GolpesEnGuardia == golpes; t += Time.deltaTime)
        {
            Destello(ajustes.colorContraataque, ajustes.brilloGuardia);
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            yield return null;
        }
        Destello(ajustes.colorContraataque, 0f);
        if (GolpesEnGuardia == golpes || a.golpes.Count == 0)
        {
            enGuardia = false;
            Animar("quieto", true, 1f);
        }
        else
        {
            AjustesImpaler.Golpe g = a.golpes[0];
            yield return HacerGolpe(a, g, 0, false, true, ajustes.retrasoContraataque);
            if (Resultados.Count > 0 && Resultados[Resultados.Count - 1] == PlayerControler.ResultadoDano.Parry)
                yield return Aturdido(ajustes.aturdidoParryContraataque, "contraataque");
            else
            {
                Animar(a.clip, false, Velocidad);
                while (!anim.Terminado) yield return null;
            }
        }
        // Adaptacion: todos los golpes que le diste en la guardia (hasta su tajo blanco).
        AprenderGuardia(GolpesEnGuardia - golpes);
        enGuardia = false;
        CambiarEstado(EstadoEnemigo.Recuperacion);
        float rec = a.recuperacion / Velocidad;
        for (float t = 0f; t < rec; t += Time.deltaTime) yield return null;
    }

    // El clip que suena y a que velocidad (0 = sostenido: mientras, no avanza).
    private float velAnim = 1f;
    private void Animar(string clip, bool reiniciar, float vel)
    {
        velAnim = vel;
        anim.Reproducir(clip, reiniciar, vel);
    }

    // Salto o embestida: entre sus cuadros avanza hasta pararse delante de donde
    // estabas al arrancar, sin salir de la arena. La embestida va por el suelo
    // (una pared la para; si no hay suelo delante, cae). El salto vuela por
    // encima del terreno y cae en el suelo de verdad bajo ti.
    private IEnumerator Mover(AjustesImpaler.Ataque a)
    {
        while (anim.Fotograma < a.moverDesde && !anim.Terminado) yield return null;
        BuscarPlayer();
        float x0 = transform.position.x;
        float distancia = Mathf.Clamp((PosPlayer.x - mirada * a.pararAntes - x0) * mirada, 0f, a.distanciaMax);
        if (arena.width > 0f)
        {
            float hastaBorde = mirada > 0 ? arena.xMax - ajustes.margenArena - x0 : x0 - (arena.xMin + ajustes.margenArena);
            distancia = Mathf.Min(distancia, Mathf.Max(0f, hastaBorde));
        }
        float duracion = Mathf.Max(1, a.moverHasta - a.moverDesde + 1) / Fps;
        if (a.salto) { yield return Volar(a, x0, distancia, duracion); yield break; }
        while (anim.Fotograma <= a.moverHasta && !anim.Terminado && Mathf.Abs(transform.position.x - x0) < distancia)
        {
            float v = velAnim > 0f ? distancia / (duracion / velAnim) : 0f;
            if (HayParedHacia(mirada, 0.8f, 1f)) break;
            rb.linearVelocity = new Vector2(mirada * v, rb.linearVelocity.y);
            yield return null;
        }
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
    }

    // El salto: sin fisica mientras vuela (pasa por encima de plataformas y
    // paredes); el alto ya va en el dibujo, el cuerpo solo va del suelo de salida
    // al suelo de llegada. Nunca cae dentro de una pared: si el sitio esta
    // ocupado, se queda un poco antes.
    private IEnumerator Volar(AjustesImpaler.Ataque a, float x0, float distancia, float duracion)
    {
        float y0 = transform.position.y;
        float xL = x0 + mirada * distancia;
        destinoSalto = xL;
        Vector2 cuerpo = CuerpoTamano;
        float yL = SueloEn(xL, y0);
        for (int i = 0; i < 40 && Ocupado(new Vector2(xL, yL), cuerpo); i++)
        {
            xL -= mirada * 0.25f;
            yL = SueloEn(xL, y0);
            if ((xL - x0) * mirada <= 0f) { xL = x0; yL = y0; break; }
        }
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.linearVelocity = Vector2.zero;
        volando = true;
        float t = 0f;
        while (t < duracion && !anim.Terminado)
        {
            t += Time.deltaTime * velAnim;
            float k = Mathf.Clamp01(t / duracion);
            rb.MovePosition(new Vector2(Mathf.Lerp(x0, xL, k), Mathf.Lerp(y0, yL, k)));
            yield return null;
        }
        rb.MovePosition(new Vector2(xL, yL));
        yield return new WaitForFixedUpdate();
        TerminarVuelo();
    }

    private bool volando;
    public bool Volando => volando;
    // Su cuerpo pisa dentro del terreno (no deberia pasar nunca fuera de un salto).
    public bool DentroDelTerreno => Ocupado((Vector2)transform.position + Vector2.up * 0.1f, CuerpoTamano * 0.8f);

    private void TerminarVuelo()
    {
        if (!volando) return;
        volando = false;
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.linearVelocity = Vector2.zero;
    }

    private Vector2 CuerpoTamano
    {
        get { BoxCollider2D b = GetComponent<BoxCollider2D>(); return b != null ? b.size : new Vector2(1.2f, 2.6f); }
    }

    // El suelo de verdad bajo x: el que esta a la altura de tus pies (si estas en
    // una plataforma, ahi), y si no, el de abajo.
    private float SueloEn(float x, float porDefecto)
    {
        float desde = Mathf.Max(PosPlayer.y, porDefecto) + 1.5f;
        if (arena.height > 0f) desde = Mathf.Min(desde, arena.yMax - 0.1f);
        return PeligrosJefe.SueloBajo(new Vector2(x, desde), porDefecto);
    }

    private bool Ocupado(Vector2 pies, Vector2 cuerpo) =>
        Physics2D.OverlapBox(pies + Vector2.up * (cuerpo.y * 0.5f + 0.05f), cuerpo * 0.95f, 0f, capaSuelo) != null;

    // Senal, aviso y golpe. El aviso real es lo que dura el dibujo de la senal al
    // golpe; si es menos que el minimo (o que la pausa minima con el golpe
    // anterior), ese tramo va a la velocidad del dibujo y, si aun falta, se
    // sostiene el cuadroPausa (repetido en el dibujo o la pose de salida).
    // "indice": el golpe dentro de su ataque. "final": el ultimo de un combo (aviso
    // minimo de golpe final). "ultimo": el ultimo de lo que hace ahora (hechizos).
    private IEnumerator HacerGolpe(AjustesImpaler.Ataque a, AjustesImpaler.Golpe g, int indice, bool final, bool ultimo, float avisoExtra = 0f)
    {
        while (anim.Fotograma < g.senal && !anim.Terminado) yield return null;
        CambiarEstado(EstadoEnemigo.Anticipacion);
        GolpeEnCurso = g;
        float dibujado = Mathf.Max(0, g.activoDesde - g.senal) / Fps;
        float objetivo = Mathf.Max(Mathf.Max(g.aviso, avisoExtra), ajustes.AvisoMinimo(g, final));
        objetivo = Mathf.Max(objetivo, ajustes.pausaMinEntreGolpes - (Time.time - finGolpeAnterior));
        float variable = g.pausaVariable.y > 0f ? Random.Range(g.pausaVariable.x, g.pausaVariable.y) : 0f;
        float vel = objetivo > dibujado ? 1f : Mathf.Min(Velocidad, dibujado / Mathf.Max(0.01f, objetivo));
        float sostener = Mathf.Max(objetivo - dibujado, variable);
        bool puede = g.cuadroPausa >= g.senal && g.cuadroPausa < g.activoDesde;
        // Adaptacion: retraso extra (solo sosteniendo su cuadro de pausa; el aviso sigue).
        if (puede) sostener += RetrasoExtra(a);
        if (sostener > 0.001f && !puede)
            Debug.LogWarning($"[Impaler] {a.id}/{g.nombre}: el aviso dibujado ({dibujado:0.00} s) no llega a {objetivo:0.00} s y no hay cuadro que sostener.");

        float inicio = Time.time;
        Avisar(g, dibujado / vel + (puede ? sostener : 0f));
        Hechizos(a, indice, AjustesImpaler.Momento.AlAvisar, ultimo);
        Animar(a.clip, false, vel);
        if (sostener > 0f && puede)
        {
            while (anim.Fotograma < g.cuadroPausa && !anim.Terminado) yield return null;
            Animar(a.clip, false, 0f);
            for (float t = 0f; t < sostener; t += Time.deltaTime) yield return null;
            Animar(a.clip, false, vel);
        }
        while (anim.Fotograma < g.activoDesde && !anim.Terminado) yield return null;
        UltimoAvisoReal = Time.time - inicio;
        UltimoGolpe = g;
        enGuardia = false;
        GolpesActivos++;
        AlActivarGolpe?.Invoke(g, UltimoAvisoReal);
        Hechizos(a, indice, AjustesImpaler.Momento.AlGolpear, ultimo);

        // Golpe activo: toca una sola vez mientras el dibujo muestra el golpe. Su
        // dano se recorta si el combo ya te quito su tope (con el dano de Dificil).
        Animar(a.clip, false, Velocidad);
        PlayerControler pc = player != null ? player.GetComponent<PlayerControler>() : null;
        int dano = g.dano;
        int vidaAntes = pc != null ? pc.VidaActual : 0;
        if (pc != null)
        {
            float queda = ajustes.topeDanoCombo * pc.VidaMaxima - danoSecuencia;
            float mult = Desafio.MultDano;
            if (g.dano * mult > queda) dano = Mathf.Max(0, Mathf.FloorToInt(queda / mult));
        }
        PlayerControler.ResultadoDano? r = null;
        while (!r.HasValue && anim.Fotograma <= g.activoHasta)
        {
            // El eco golpea desde su lado: hay que mirarle a el para pararlo.
            if (!Mathf.Approximately(g.origenX, 0f)) PlayerControler.SiguienteOrigenX = transform.position.x + mirada * g.origenX;
            // Cada parry seguido suena un poco mas agudo (el sonido lo pone el jugador).
            PlayerControler.TonoSiguienteParry = TonoParry;
            r = Golpear(g.cajaCentro, g.cajaTamano, dano, PlayerControler.TipoDano.Fisico);
            PlayerControler.TonoSiguienteParry = 1f;
            PlayerControler.SiguienteOrigenX = float.NaN;
            if (DepuracionCazadora.VerCajas)
                CajaDebug.Mostrar((Vector2)transform.position + new Vector2(g.cajaCentro.x * mirada, g.cajaCentro.y), g.cajaTamano, ajustes.ColorAviso(g), 0.03f);
            if (r.HasValue || anim.Terminado) break;
            yield return null;
        }
        if (pc != null) danoSecuencia += Mathf.Max(0, vidaAntes - pc.VidaActual);
        Registrar(g, r);
        Quemar(g, r, pc);
        while (anim.Fotograma <= g.activoHasta && !anim.Terminado) yield return null;
        GolpesActivos--;
        GolpeEnCurso = null;
        finGolpeAnterior = Time.time;
        AlTerminarGolpe?.Invoke(g, r);
        Hechizos(a, indice, AjustesImpaler.Momento.TrasGolpe, ultimo);
        CambiarEstado(EstadoEnemigo.Recuperacion);
    }

    // ------------------------------------------------------------------ Fases y muerte

    // Muerte de verdad: la flor se queda (el ultimo cuadro de la muerte).
    protected override void AlMorir()
    {
        base.AlMorir();
        GolpesActivos = 0;
        GolpeEnCurso = null;
        enGuardia = false;
        TerminarVuelo();
        PlayerControler.SiguienteOrigenX = float.NaN;
        PlayerControler.TonoSiguienteParry = 1f;
        QuitarAvisoRitmo();
        if (visual != null) visual.localPosition = Vector3.zero;
        // La flor sin la lanza encendida ni el tinte de la fase 2.
        PonerFase2(false);
        EnTregua = false;
        Circulo(false);
        PeligrosJefe.LimpiarTodo();
        Sonar("jefe_muerte");
        AvisarDerrota();
    }
}
