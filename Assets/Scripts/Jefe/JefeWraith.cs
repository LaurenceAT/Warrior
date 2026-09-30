using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

// Crimson Wraith, el Espectro Carmesi: el jefe final de la cueva.
//
// ENTRADA. Una sombra carmesi aparece bajo el player y el jefe cae del techo en
// picado. Si el player para el impacto con un parry, el jefe queda aturdido un buen
// rato y se abre el contraataque.
//
// FASE 1 (forma humanoide):
//   - Tajos: se prepara con la garra en alto (aviso) y encadena dos zarpazos.
//   - Embestida: se agacha (aviso) y cruza el escenario dejando una estela.
//   - Onda carmesi: conjura y lanza dos ondas por el suelo (hay que saltarlas).
//   - Pilares: marca el suelo con glifos y brotan pilares de sangre (sangrado).
//   - Orbes: flota y suelta orbes que persiguen (sangrado; parry los devuelve).
//
// FASE 2 (al 50%): se transforma en su forma grande. El escenario se tine de
// rojo. Ataca mas rapido y con movimientos nuevos: zarpazo gigante con ondas,
// lluvia de fuego por toda la arena, teletransporte a la espalda y pilares en
// cadena. Su coraza resiste el filo sin imbuir (x0.6) y cede a la luz
// sagrada (x1.8); la oscuridad apenas le hace nada. Hay que imbuir la espada.
//
// Los ataques grandes abren la camara (CamaraDinamica.Ampliar) para que se vean.
public partial class JefeWraith : JefeBase, IModificadorDano, IAfinidadElemental, IAturdible
{

    [Header("Dano")]
    [SerializeField] private int danoNormal = 40;
    [SerializeField] private int danoFuerte = 60;
    // Golpes pequenos: orbes, vortice y la onda de la transformacion.
    [SerializeField] private int danoLeve = 20;
    [SerializeField] private float sangradoPilar = 45f;
    [SerializeField] private float sangradoOrbe = 55f;

    [Header("Fases")]
    [Range(0f, 1f)] [SerializeField] private float umbralFase2 = 0.5f;
    [SerializeField] private float velocidadDeslizar = 2.6f;

    [Header("Debilidad en la fase 2")]
    // Espada sin imbuir, y cada elemento. Antes la debilidad eran los punos, pero
    // el combate sin arma ya no esta disponible.
    [SerializeField] private float multEspada = 0.6f;
    [SerializeField] private float multArco = 1f;
    [SerializeField] private float multSagrado = 1.8f;
    [SerializeField] private float multFuego = 1f;
    [SerializeField] private float multOscuro = 0.3f;
    [SerializeField] private float multHielo = 0.8f;
    [Tooltip("Tus golpes con sangrado le hacen esto... y lo curan (ver Absorcion).")]
    [SerializeField] private float multSangrado = 0.2f;
    [Tooltip("Fase 2: parte del dano de tus golpes con sangrado que se convierte en vida suya.")]
    [Range(0f, 1f)] [SerializeField] private float absorcionSangrado = 0.3f;

    [Header("Debilidad en la fase 1")]
    [SerializeField] private float fase1Fuego = 1.3f;
    [SerializeField] private float fase1Hielo = 1.2f;
    [Tooltip("Con 0.5 o menos, el sangrado tampoco le aplica su estado.")]
    [SerializeField] private float fase1Sangrado = 0.6f;
    [SerializeField] private float fase1Oscuro = 0.7f;
    [SerializeField] private float fase1Sagrado = 1f;

    [Header("Corazon expuesto (tras Nova, Lluvia y Frenesi)")]
    [Tooltip("Todo el dano se multiplica por esto mientras el nucleo brilla.")]
    [SerializeField] private float multCorazon = 1.5f;
    [SerializeField] private float duracionCorazon = 2f;

    [Header("Parry")]
    // Aturdimiento al parar el impacto de la entrada, y el de un golpe normal.
    [SerializeField] private float aturdidoEntrada = 2.6f;
    [SerializeField] private float aturdidoParry = 1.4f;

    [Header("Efectos")]
    public AnimadorHoja.Clip fxOnda;
    public AnimadorHoja.Clip fxPilar;
    public AnimadorHoja.Clip fxGlifo;
    public AnimadorHoja.Clip fxOrbe;
    public AnimadorHoja.Clip fxSangre;
    public AnimadorHoja.Clip fxMeteoro;
    public AnimadorHoja.Clip fxExplosion;
    public AnimadorHoja.Clip fxAnillo;
    public AnimadorHoja.Clip fxPortal;
    public AnimadorHoja.Clip fxPolvo;
    public AnimadorHoja.Clip fxColumnaPolvo;
    public AnimadorHoja.Clip fxMedialuna;
    public AnimadorHoja.Clip fxVorticeInicio;
    public AnimadorHoja.Clip fxVorticeBucle;
    public AnimadorHoja.Clip fxVorticeFin;
    [SerializeField] private Color carmesi = new Color(1f, 0.18f, 0.22f, 1f);
    // La onda es morada de origen: se tine menos para que no se pierda en la roca.
    [SerializeField] private Color colorOnda = new Color(1f, 0.45f, 0.55f, 1f);

    [Header("Cuerpo")]
    [SerializeField] private Vector2 cuerpoFase1 = new Vector2(1.6f, 3.2f);
    [SerializeField] private Vector2 cuerpoFase2 = new Vector2(2.8f, 4.4f);

    private Rect arena;
    private float suelo;
    private bool fase2, transformando, parado, invisible;
    // Tambaleo del sagrado pendiente: se cumple al acabar el ataque en curso.
    private float aturdimientoSagrado;

    public bool PedirAturdimiento(float segundos)
    {
        if (transformando || invisible || salud.Muerto || aturdimientoSagrado > 0f) return false;
        aturdimientoSagrado = segundos;
        return true;
    }
    private float tLluvia = -99f, tVortice = -99f, tNova = -99f;
    private string ultimoAtaque = "";
    private BoxCollider2D cuerpo;
    private CinemachineImpulseSource impulso;
    private SpriteRenderer sr;

    // La arena la pone ArenaJefe al crearlo: limites y altura del suelo.
    public override void Configurar(Rect zona, float alturaSuelo)
    {
        arena = zona;
        suelo = alturaSuelo;
        // Modo Dificil de los desafios: mas vida (fuera de ellos no cambia nada).
        if (Desafio.MultVida != 1f) salud.Revivir(VidaDesafio(salud.MaxHealth));
    }

    protected override void Awake()
    {
        base.Awake();
        colorAviso = new Color(1f, 0.15f, 0.12f, 1f);
        radioDeteccion = radioOlvido = 60f;
        cuerpo = GetComponent<BoxCollider2D>();
        impulso = GetComponent<CinemachineImpulseSource>();
        sr = visual != null ? visual.GetComponent<SpriteRenderer>() : null;
        salud.Inamovible = true;
        // Nada interrumpe al jefe salvo el parry (lo gestiona el propio jefe).
        armadura = true;
        EstadosPlayer.efectoSangrado = fxSangre;
        IniciarPoderes();
    }

    // ------------------------------------------------------------------ Debilidad

    public int Modificar(int dano, TipoArma arma)
    {
        if (transformando || invisible) return 0;
        // Corazon expuesto: todo entra mas.
        if (CorazonExpuesto) dano = Mathf.RoundToInt(dano * multCorazon);
        if (!fase2) return dano;

        // Esta hecho de sangre: tus golpes con sangrado lo curan un poco.
        if (EnemyHealth.ElementoDelGolpe == Elemento.Sangrado && absorcionSangrado > 0f)
        {
            int cura = Mathf.RoundToInt(dano * absorcionSangrado);
            if (cura > 0)
            {
                salud.Curar(cura);
                TextoFlotante.Mostrar("Absorbe +" + cura, (Vector2)transform.position + new Vector2(Random.Range(-0.6f, 0.6f), 3.6f),
                                      new Color(0.9f, 0.2f, 0.25f), 0.9f);
            }
        }

        // Con la espada imbuida manda la afinidad (Multiplicador), que ya avisa.
        if (arma == TipoArma.Espada && EnemyHealth.ElementoDelGolpe != Elemento.Ninguno) return dano;
        float m = arma == TipoArma.Espada ? multEspada : arma == TipoArma.Arco ? multArco : 1f;
        Vector2 donde = (Vector2)transform.position + new Vector2(Random.Range(-0.6f, 0.6f), 3.2f);
        if (m > 1.01f) TextoFlotante.Mostrar("¡Débil!", donde, new Color(1f, 0.75f, 0.2f), 1.1f);
        else if (m < 0.99f) TextoFlotante.Mostrar("Resiste", donde, new Color(0.7f, 0.7f, 0.75f), 0.9f);
        return Mathf.Max(1, Mathf.RoundToInt(dano * m));
    }

    // Fase 1: el fuego hace hervir su sangre y el hielo la congela; resiste la
    // oscuridad y el sangrado. Fase 2: la luz lo quema y el fuego mas; la
    // oscuridad y el hielo apenas; el sangrado casi nada (y lo cura).
    public float Multiplicador(Elemento e)
    {
        if (!fase2)
        {
            switch (e)
            {
                case Elemento.Fuego: return fase1Fuego;
                case Elemento.Hielo: return fase1Hielo;
                case Elemento.Sangrado: return fase1Sangrado;
                case Elemento.Oscuro: return fase1Oscuro;
                case Elemento.Sagrado: return fase1Sagrado;
                default: return 1f;
            }
        }
        switch (e)
        {
            case Elemento.Sagrado: return multSagrado;
            case Elemento.Fuego: return multFuego;
            case Elemento.Oscuro: return multOscuro;
            case Elemento.Hielo: return multHielo;
            case Elemento.Sangrado: return multSangrado;
            default: return 1f;
        }
    }

    // ------------------------------------------------------------------ Cerebro

    protected override IEnumerator Cerebro()
    {
        yield return Entrada();

        while (true)
        {
            if (!fase2 && salud.CurrentHealth <= salud.MaxHealth * umbralFase2)
                yield return Transformacion();
            // Frenesi: una vez, con poca vida en la fase 2.
            if (fase2 && !frenesiHecho && salud.CurrentHealth <= salud.MaxHealth * umbralFrenesi)
            {
                AnunciarAtaque("frenesi");
                yield return Frenesi();
            }
            if (enFrenesi && Time.time >= finFrenesi) yield return FinFrenesi();

            yield return Reposicionar();

            string ataque = Elegir();
            ultimoAtaque = ataque;
            parado = false;
            yield return Ejecutar(ataque);

            // Fase 2: a veces encadena sin respiro (zarpazo o teletransporte y
            // guadana doble).
            if (fase2 && !parado && aturdimientoSagrado <= 0f && (ataque == "zarpazo" || ataque == "teletransporte" || ataque == "salto")
                && Random.value < probabilidadCombo)
            {
                yield return Esperar(0.08f);
                ultimoAtaque = "guadana";
                AnunciarAtaque("guadana");
                yield return GuadanaDoble();
            }

            float sagrado = aturdimientoSagrado;
            aturdimientoSagrado = 0f;
            if (parado) yield return Aturdido(aturdidoParry);
            else if (sagrado > 0f) yield return Aturdido(sagrado);
            else if (enFrenesi) yield return Pausa(0.05f);
            else yield return Pausa(fase2 ? Random.Range(pausaFase2.x, pausaFase2.y) : Random.Range(pausaFase1.x, pausaFase1.y));
        }
    }

    private IEnumerator Ejecutar(string ataque)
    {
        AnunciarAtaque(ataque);
        switch (ataque)
        {
            case "tajos": return Tajos();
            case "embestida": return Embestida();
            case "onda": return Onda();
            case "pilares": return Pilares();
            case "orbes": return Orbes();
            case "zarpazo": return Zarpazo();
            case "lluvia": return Lluvia();
            case "teletransporte": return Teletransporte();
            case "cadena": return PilaresEnCadena();
            case "salto": return Salto();
            case "medialuna": return Medialuna();
            case "vortice": return Vortice();
            case "nova": return Nova();
            case "guadana": return GuadanaDoble();
            case "cosecha": return CosechaDeSangre();
            case "transfusion": return Transfusion();
            case "raices": return RaicesCarmesi();
            case "semillas": return SemillasDelVacio();
            case "zigzag": return Zigzag();
            case "frenesi": return Frenesi();
            default: return Pausa(0.3f);
        }
    }

    // Elige ataque segun la distancia y la fase, sin repetir el anterior.
    private string Elegir()
    {
        if (!string.IsNullOrEmpty(ataqueForzado)) { string f = ataqueForzado; ataqueForzado = null; return f; }
        float d = Mathf.Abs(DxPlayer);
        var opciones = new List<(string, float)>();
        if (!fase2)
        {
            if (d < 4.5f) { opciones.Add(("tajos", 3f)); opciones.Add(("onda", 1f)); opciones.Add(("pilares", 1f)); opciones.Add(("salto", 1f)); }
            else if (d < 9f) { opciones.Add(("embestida", 2.5f)); opciones.Add(("pilares", 2f)); opciones.Add(("orbes", 1.5f)); opciones.Add(("onda", 1f)); opciones.Add(("salto", 2f)); opciones.Add(("medialuna", 2f)); }
            else { opciones.Add(("embestida", 2f)); opciones.Add(("orbes", 2.5f)); opciones.Add(("pilares", 1.5f)); opciones.Add(("salto", 2f)); opciones.Add(("medialuna", 2.5f)); }
            // Nuevos: raices que siguen tu rastro y la embestida doble.
            if (Time.time - tRaices > 10f) opciones.Add(("raices", 1.8f));
            if (d >= 4.5f) opciones.Add(("zigzag", 1.6f));
        }
        else
        {
            if (d < 5f) { opciones.Add(("zarpazo", 3f)); opciones.Add(("teletransporte", 1f)); opciones.Add(("cadena", 1.5f)); opciones.Add(("onda", 1f)); opciones.Add(("salto", 1f)); }
            else { opciones.Add(("teletransporte", 2.5f)); opciones.Add(("cadena", 2f)); opciones.Add(("onda", 1.5f)); opciones.Add(("orbes", 1.5f)); opciones.Add(("salto", 2f)); }
            if (Time.time - tLluvia > 12f) opciones.Add(("lluvia", 3f));
            if (Time.time - tVortice > 10f) opciones.Add(("vortice", 2.5f));
            if (Time.time - tNova > 9f && d < 7f) opciones.Add(("nova", 2.5f));
            if (d < 5.5f) opciones.Add(("guadana", 2.2f));
            if (Time.time - tSemillas > 11f) opciones.Add(("semillas", 2f));
            if (Time.time - tTransfusion > 14f && d < 8f) opciones.Add(("transfusion", 1.8f));
        }

        // Cosecha (fases 1 y 2): solo si tu barra de sangrado va por la mitad.
        if (Time.time - tCosecha > 8f && SangradoDelPlayer() >= umbralCosecha) opciones.Add(("cosecha", 4f));
        opciones.RemoveAll(o => o.Item1 == ultimoAtaque);
        float total = 0f;
        foreach (var o in opciones) total += o.Item2;
        float r = Random.value * total;
        foreach (var o in opciones) { r -= o.Item2; if (r <= 0f) return o.Item1; }
        return opciones.Count > 0 ? opciones[0].Item1 : "tajos";
    }

    // ------------------------------------------------------------------ Entrada

    private IEnumerator Entrada()
    {
        invisible = true;
        MostrarSprite(false);
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.linearVelocity = Vector2.zero;
        BuscarPlayer();

        // Sombra en el suelo bajo el player: crece mientras el jefe se acerca.
        float x = Mathf.Clamp(PosPlayer.x, arena.xMin + 3f, arena.xMax - 3f);
        CamaraDinamica.Ampliar(6.4f, 3.2f);
        EfectoVisual sombra = EfectoVisual.Crear(fxGlifo, new Vector2(x, suelo + 0.1f), 1f, carmesi, false, -1f, "VFX", 8);
        if (sombra != null) PeligrosJefe.Registrar(sombra.gameObject);
        float t = 0f;
        const float aviso = 1.4f;
        while (t < aviso)
        {
            t += Time.deltaTime;
            // Sigue un poco al player durante la primera mitad: luego se fija.
            if (t < aviso * 0.5f) x = Mathf.Lerp(x, Mathf.Clamp(PosPlayer.x, arena.xMin + 3f, arena.xMax - 3f), Time.deltaTime * 3f);
            if (sombra != null)
            {
                sombra.transform.position = new Vector2(x, suelo + 0.1f);
                float s = Mathf.Lerp(0.6f, 2.4f, t / aviso);
                sombra.transform.localScale = new Vector3(s, s * 0.45f, 1f);
                Color c = carmesi; c.a = 0.4f + 0.6f * (0.5f + 0.5f * Mathf.Sin(t * 30f)); sombra.Render.color = c;
            }
            yield return null;
        }
        if (sombra != null) Destroy(sombra.gameObject);

        // Cae en picado desde lo alto de la arena.
        AnunciarAtaque("caida_entrada");
        transform.position = new Vector3(x, arena.yMax - 1f, 0f);
        MirarAlPlayer();
        MostrarSprite(true);
        invisible = false;
        anim.Reproducir("caida", true);
        // Por fisica (con interpolacion): sin tirones.
        rb.position = transform.position;
        while (rb.position.y > suelo + 0.05f)
        {
            rb.MovePosition(new Vector2(x, Mathf.MoveTowards(rb.position.y, suelo, 30f * Time.fixedDeltaTime)));
            yield return new WaitForFixedUpdate();
        }
        rb.position = new Vector2(x, suelo);
        Pose?.Estirar(1.15f, 0.85f, 0.2f);
        rb.bodyType = RigidbodyType2D.Dynamic;

        // Impacto: polvo, sacudida y onda de choque alrededor.
        anim.Reproducir("impacto", true);
        Sacudir(1.4f);
        CamaraDinamica.Acercar(4.3f, 0.45f);
        EfectoVisual.Crear(fxColumnaPolvo, new Vector2(x, suelo), 2.6f, new Color(0.8f, 0.6f, 0.6f));
        EfectoVisual.Crear(fxPolvo, new Vector2(x, suelo + 0.4f), 2.4f, new Color(0.8f, 0.6f, 0.6f));
        var r = GolpearMundo(new Vector2(x, suelo + 1.3f), new Vector2(5f, 2.6f), danoFuerte);
        AvisarAterrizaje();

        if (r == PlayerControler.ResultadoDano.Parry)
        {
            TextoFlotante.Mostrar("¡Parry!", (Vector2)transform.position + Vector2.up * 4f, new Color(1f, 0.9f, 0.5f), 1.4f);
            yield return Aturdido(aturdidoEntrada);
        }
        else yield return Pausa(0.9f);
    }

    // ------------------------------------------------------------------ Fase 1

    private IEnumerator Tajos()
    {
        int golpes = 2;
        for (int i = 0; i < golpes; i++)
        {
            MirarAlPlayer();
            anim.Reproducir("preparar", true);
            float aviso = i == 0 ? 0.5f : 0.3f;
            AvisoLigero(aviso);
            yield return Esperar(aviso);

            anim.Reproducir("tajo", true);
            Pose?.Soltar();
            Sonar("jefe_tajo", 0.8f);
            if (i == golpes - 1) CamaraDinamica.Acercar(4.6f, 0.3f);
            float t = 0f;
            bool pego = false;
            while (t < anim.Duracion("tajo"))
            {
                t += Time.deltaTime;
                // Avanza con el zarpazo durante los dos primeros fotogramas.
                bool activo = anim.Fotograma <= 1;
                Acelerar(activo ? mirada * 4.5f : 0f, activo ? 60f : 28f);
                if (activo && !pego)
                {
                    var r = Golpear(new Vector2(2.2f, 1.8f), new Vector2(4f, 3.4f), danoNormal);
                    if (r.HasValue) { pego = true; if (r.Value == PlayerControler.ResultadoDano.Parry) { parado = true; yield break; } }
                }
                yield return null;
            }
            FrenarSuave(28f);
            if (Mathf.Abs(DxPlayer) > 6f) break;
        }
        yield return RevesSiDetras();
        if (parado) yield break;
        yield return Esperar(0.25f);
    }

    private IEnumerator Embestida(float aviso = 0.55f)
    {
        MirarAlPlayer();
        anim.Reproducir("guardia", true);
        AvisoPesado(aviso);
        // Se agacha: echa el cuerpo atras un poco mientras avisa.
        yield return Temblar(aviso, 0.05f);

        float destino = Mathf.Clamp(PosPlayer.x + mirada * 3.5f, arena.xMin + 1.5f, arena.xMax - 1.5f);
        anim.Reproducir("embestida", true);
        bool pego = false;
        float t = 0f, tEstela = 0f;
        while (t < 1.2f && (destino - transform.position.x) * mirada > 0.2f)
        {
            t += Time.deltaTime;
            Acelerar(mirada * 19f, 110f);
            if ((tEstela -= Time.deltaTime) <= 0f) { Estela(); tEstela = 0.04f; }
            if (!pego)
            {
                var r = Golpear(new Vector2(0.4f, 1.6f), new Vector2(3f, 3.2f), danoNormal);
                if (r.HasValue) { pego = true; if (r.Value == PlayerControler.ResultadoDano.Parry) { rb.linearVelocity = Vector2.zero; parado = true; yield break; } }
            }
            if (HayParedDelante(2f, 1f)) break;
            yield return null;
        }

        // Frena levantando polvo.
        FrenarSuave(75f);
        anim.Reproducir("frenar", true);
        Pose?.Estirar(1.1f, 0.92f, 0.25f);
        EfectoVisual.Crear(fxPolvo, (Vector2)transform.position + Vector2.up * 0.4f, 1.8f, new Color(0.8f, 0.6f, 0.6f), mirada < 0);
        for (float tf = 0f; tf < 0.4f && Mathf.Abs(rb.linearVelocity.x) > 0.3f; tf += Time.deltaTime) yield return null;
        yield return Esperar(0.2f);
    }

    private IEnumerator Onda()
    {
        MirarAlPlayer();
        anim.Reproducir(fase2 ? "grande_guardia" : "conjuro", true);
        AvisoPesado(0.6f);
        CamaraDinamica.Ampliar(6.4f, 2.4f);
        yield return Esperar(0.6f);

        anim.Reproducir(fase2 ? "grande_zarpazo" : "zarpazo", true);
        Sacudir(0.6f);
        int oleadas = fase2 ? 2 : 1;
        for (int i = 0; i < oleadas; i++)
        {
            Vector2 pie = new Vector2(transform.position.x, suelo);
            OndaCarmesi.Lanzar(fxOnda, pie + Vector2.right * 1.2f, 1, fase2 ? 10.5f : 9f, danoNormal, 22f, colorOnda, 1.2f, EstadoPlayer.Sangrado, 20f);
            OndaCarmesi.Lanzar(fxOnda, pie + Vector2.left * 1.2f, -1, fase2 ? 10.5f : 9f, danoNormal, 22f, colorOnda, 1.2f, EstadoPlayer.Sangrado, 20f);
            if (!fase2 && anim.Tiene("zarpazo_reves")) anim.Reproducir(i % 2 == 0 ? "zarpazo_reves" : "zarpazo", true);
            yield return Esperar(0.55f);
        }
        yield return Esperar(0.3f);
    }

    private IEnumerator Pilares()
    {
        anim.Reproducir("conjuro", true);
        AvisoLigero(0.45f);
        yield return Esperar(0.4f);

        // Uno bajo el player y dos a los lados: hay que leer el hueco.
        float x = PosPlayer.x;
        float[] desvios = fase2 ? new[] { 0f, -2.4f, 2.4f, -4.8f, 4.8f } : new[] { 0f, -2.6f, 2.6f };
        foreach (float dx in desvios)
        {
            float px = Mathf.Clamp(x + dx, arena.xMin + 0.6f, arena.xMax - 0.6f);
            PilarSangre.Invocar(fxGlifo, fxPilar, new Vector2(px, suelo), 0.9f, danoNormal, sangradoPilar, carmesi);
        }
        yield return Esperar(1f);
    }

    private IEnumerator Orbes()
    {
        anim.Reproducir(fase2 ? "grande_rugido" : "flotar", true);
        AvisoLigero(0.45f);
        yield return Esperar(0.45f);

        int n = fase2 ? 5 : 3;
        for (int i = 0; i < n; i++)
        {
            Vector2 boca = (Vector2)transform.position + new Vector2(mirada * 0.8f, fase2 ? 4f : 3.2f);
            Vector2 dir = Quaternion.Euler(0f, 0f, (i - (n - 1) * 0.5f) * 25f) * new Vector2(mirada, 0.9f);
            OrbeSangre.Lanzar(fxOrbe, fxSangre, boca, dir, 5.5f, 95f, 5f, danoLeve, sangradoOrbe, transform, carmesi);
            yield return Esperar(0.3f);
        }
        yield return Esperar(0.6f);
    }

    // ------------------------------------------------------------------ Fase 2

    private IEnumerator Transformacion()
    {
        transformando = true;
        Detener();
        anim.Reproducir("grande_rugido", true);
        CamaraDinamica.Ampliar(7f, 3.2f);
        CamaraDinamica.Encuadrar((Vector2)transform.position + Vector2.up * 2.5f, 0.7f, 3f);
        CamaraDinamica.CamaraLenta(0.35f, 0.6f);
        Sonar("jefe_transformacion");
        ScreenFlash.Destello(new Color(0.9f, 0.05f, 0.1f, 0.45f), 0.5f);
        Sacudir(2f);
        EfectoVisual anillo = EfectoVisual.Crear(fxAnillo, (Vector2)transform.position + Vector2.up * 2.5f, 3f, carmesi);
        PeligrosJefe.LimpiarTodo();
        cuerpo.size = cuerpoFase2;
        cuerpo.offset = new Vector2(0f, cuerpoFase2.y * 0.5f);
        fase2 = true;
        AvisarCambioFase();

        // Onda de choque de la transformacion: un aro muestra su alcance mientras
        // crece y, al llenarse, estalla. Antes salia al instante y remataba al
        // player sin aviso.
        yield return AroDeAviso((Vector2)transform.position + Vector2.up * 1.2f, 3.5f, 0.9f, 0.04f);
        GolpearCirculo((Vector2)transform.position + Vector2.up * 1.2f, 3.5f, danoLeve);
        Sacudir(1.2f);
        yield return Temblar(1.1f, 0.06f);
        transformando = false;
        yield return Esperar(0.4f);
    }

    private IEnumerator Zarpazo()
    {
        MirarAlPlayer();
        anim.Reproducir(anim.Tiene("grande_preparar") ? "grande_preparar" : "grande_guardia", true);
        AvisoPesado(0.65f);
        CamaraDinamica.Ampliar(6.4f, 2f);
        yield return Temblar(0.65f, 0.04f);

        anim.Reproducir("grande_zarpazo", true);
        Pose?.Soltar(1.3f, 0.15f);
        Sacudir(1.5f);
        CamaraDinamica.Acercar(4.5f, 0.4f);
        Sonar("jefe_golpe_fuerte");
        Acelerar(mirada * 3f, 40f);
        var r = Golpear(new Vector2(2.8f, 2.2f), new Vector2(5.6f, 4.6f), danoFuerte);
        if (r == PlayerControler.ResultadoDano.Parry) { parado = true; yield break; }

        // El zarpazo levanta ondas a los dos lados.
        Vector2 pie = new Vector2(transform.position.x, suelo);
        OndaCarmesi.Lanzar(fxOnda, pie + Vector2.right * mirada * 3f, mirada, 11f, danoNormal, 18f, colorOnda, 1.2f, EstadoPlayer.Sangrado, 20f);
        OndaCarmesi.Lanzar(fxOnda, pie - Vector2.right * mirada * 1.5f, -mirada, 11f, danoNormal, 18f, colorOnda, 1.2f, EstadoPlayer.Sangrado, 20f);
        yield return Esperar(0.25f);
        FrenarSuave(30f);
        yield return Esperar(0.35f);
    }

    private IEnumerator Lluvia()
    {
        tLluvia = Time.time;
        anim.Reproducir("grande_rugido", true);
        AvisoPesado(0.7f);
        // Area de toda la arena: la camara se abre del todo.
        CamaraDinamica.Ampliar(8f, 5f);
        Sacudir(0.8f);
        yield return Esperar(0.7f);

        // Uno siempre donde esta el player; el resto repartidos por la arena.
        for (int i = 0; i < 9; i++)
        {
            float x = i % 3 == 0 ? PosPlayer.x : Random.Range(arena.xMin + 1f, arena.xMax - 1f);
            Meteoro.Caer(fxMeteoro, fxExplosion, fxGlifo, new Vector2(x, suelo), arena.yMax + 1f, 1f, danoNormal, new Color(1f, 0.45f, 0.2f), EstadoPlayer.Quemadura, 45f);
            yield return Esperar(0.28f);
        }
        yield return Esperar(0.5f);
        ExponerCorazon();
        yield return Esperar(0.4f);
    }

    private IEnumerator Teletransporte()
    {
        // Desaparece en un portal...
        anim.Reproducir("grande_guardia", true);
        EfectoVisual.Crear(fxPortal, (Vector2)transform.position + Vector2.up * 2.2f, 2f, carmesi);
        yield return Desvanecerse(0.3f, false);
        invisible = true;
        yield return Esperar(0.35f);

        // ...y reaparece a la espalda del player, con otro portal de aviso.
        int ladoPlayer = player != null && player.localScale.x < 0f ? -1 : 1;
        float x = Mathf.Clamp(PosPlayer.x - ladoPlayer * 3f, arena.xMin + 2f, arena.xMax - 2f);
        transform.position = new Vector3(x, suelo, 0f);
        EfectoVisual.Crear(fxPortal, new Vector2(x, suelo + 2.2f), 2f, carmesi);
        MirarAlPlayer();
        yield return Esperar(0.35f);
        invisible = false;
        yield return Desvanecerse(0.2f, true);

        yield return Zarpazo();
    }

    private IEnumerator PilaresEnCadena()
    {
        MirarAlPlayer();
        anim.Reproducir("grande_rugido", true);
        AvisoPesado(0.5f);
        yield return Esperar(0.5f);

        // Una fila de pilares que avanza desde el jefe hacia el player y le pasa.
        float x = transform.position.x + mirada * 2f;
        for (int i = 0; i < 12; i++)
        {
            if (x < arena.xMin + 0.5f || x > arena.xMax - 0.5f) break;
            PilarSangre.Invocar(fxGlifo, fxPilar, new Vector2(x, suelo), 0.55f, danoNormal, sangradoPilar, carmesi);
            x += mirada * 1.5f;
            yield return Esperar(0.11f);
        }
        yield return Esperar(0.9f);
    }

    // Salto aplastante: se agacha, salta en arco hasta donde esta el player y cae
    // con una onda de choque. El sitio de caida se marca en cuanto despega.
    private IEnumerator Salto()
    {
        MirarAlPlayer();
        anim.Reproducir(fase2 && anim.Tiene("grande_preparar") ? "grande_preparar" : fase2 ? "grande_guardia" : "guardia", true);
        AvisoPesado(0.5f);
        yield return Temblar(0.5f, 0.05f);

        float destino = Mathf.Clamp(PosPlayer.x, arena.xMin + 2f, arena.xMax - 2f);
        EfectoVisual marca = EfectoVisual.Crear(fxGlifo, new Vector2(destino, suelo + 0.1f), 1f, carmesi, false, -1f, "VFX", 8);
        if (marca != null) marca.transform.localScale = new Vector3(2f, 0.9f, 1f);
        if (marca != null) PeligrosJefe.Registrar(marca.gameObject);

        Vector2 inicio = transform.position;
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.linearVelocity = Vector2.zero;
        anim.Reproducir(fase2 ? "grande_guardia" : "caida", true);
        const float vuelo = 0.8f;
        float alto = fase2 ? 5f : 6f;
        // Arco por fisica: sale y llega suave en horizontal, sin tirones.
        Pose?.Estirar(0.9f, 1.12f, 0.2f);
        for (float t = 0f; t < vuelo; t += Time.fixedDeltaTime)
        {
            float u = t / vuelo;
            float x = Mathf.Lerp(inicio.x, destino, u * u * (3f - 2f * u));
            float y = suelo + alto * 4f * u * (1f - u);
            rb.MovePosition(new Vector2(x, Mathf.Max(suelo, y)));
            yield return new WaitForFixedUpdate();
        }
        rb.position = new Vector2(destino, suelo);
        Pose?.Estirar(1.15f, 0.85f, 0.2f);
        rb.bodyType = RigidbodyType2D.Dynamic;
        if (marca != null) Destroy(marca.gameObject);

        anim.Reproducir(fase2 ? "grande_zarpazo" : "impacto", true);
        Sacudir(1.2f);
        CamaraDinamica.Acercar(4.6f, 0.3f);
        Sonar("jefe_impacto_suelo");
        EfectoVisual.Crear(fxPolvo, new Vector2(destino, suelo + 0.4f), 2.2f, new Color(0.8f, 0.6f, 0.6f));
        var r = GolpearMundo(new Vector2(destino, suelo + 1.2f), new Vector2(4.5f, 2.4f), fase2 ? danoFuerte : danoNormal, EstadoPlayer.Sangrado, 30f);
        if (r == PlayerControler.ResultadoDano.Parry) { parado = true; yield break; }
        if (fase2)
        {
            OndaCarmesi.Lanzar(fxOnda, new Vector2(destino + 1.5f, suelo), 1, 9f, danoNormal, 12f, colorOnda, 1.2f, EstadoPlayer.Sangrado, 20f);
            OndaCarmesi.Lanzar(fxOnda, new Vector2(destino - 1.5f, suelo), -1, 9f, danoNormal, 12f, colorOnda, 1.2f, EstadoPlayer.Sangrado, 20f);
        }
        yield return Esperar(0.6f);
    }

    // Media luna: zarpazo a distancia que vuela recto a la altura del pecho.
    private IEnumerator Medialuna()
    {
        MirarAlPlayer();
        anim.Reproducir("preparar", true);
        AvisoLigero(0.5f);
        yield return Esperar(0.5f);

        anim.Reproducir("tajo", true);
        Pose?.Soltar();
        Sacudir(0.4f);
        MedialunaSangre.Lanzar(fxMedialuna, (Vector2)transform.position + new Vector2(mirada * 2f, 1.1f), mirada, 10f, danoNormal, transform, carmesi, EstadoPlayer.Sangrado, 30f);
        yield return Esperar(0.5f);
    }

    // Vortice: un remolino de sangre que atrae al player hacia su centro.
    private IEnumerator Vortice()
    {
        tVortice = Time.time;
        anim.Reproducir("grande_rugido", true);
        AvisoPesado(0.6f);
        CamaraDinamica.Ampliar(6.4f, 4f);
        yield return Esperar(0.5f);

        // Entre el jefe y el player, para arrastrarlo hacia el.
        float x = Mathf.Clamp(Mathf.Lerp(transform.position.x, PosPlayer.x, 0.45f), arena.xMin + 2f, arena.xMax - 2f);
        VorticeSangre.Invocar(fxVorticeInicio, fxVorticeBucle, fxVorticeFin, new Vector2(x, suelo + 1.2f), 3f, 7f, 3.2f, danoLeve, 35f, carmesi);
        yield return Esperar(1f);

        // Mientras tira, dispara orbes: hay que elegir entre salir o esquivarlos.
        for (int i = 0; i < 2; i++)
        {
            Vector2 boca = (Vector2)transform.position + new Vector2(mirada * 0.8f, 4f);
            OrbeSangre.Lanzar(fxOrbe, fxSangre, boca, new Vector2(mirada, 1f), 5f, 80f, 4f, danoLeve, sangradoOrbe, transform, carmesi);
            yield return Esperar(0.6f);
        }
        yield return Esperar(0.8f);
    }

    // Nova: se carga con un aro que marca su alcance y estalla alrededor.
    private IEnumerator Nova()
    {
        tNova = Time.time;
        Detener();
        anim.Reproducir("grande_rugido", true);
        AvisoPesado(1.1f);
        CamaraDinamica.Ampliar(6.6f, 2.5f);
        Vector2 centro = (Vector2)transform.position + Vector2.up * 1.6f;
        yield return AroDeAviso(centro, 5f, 1.1f, 0.05f);

        EfectoVisual.Crear(fxAnillo, centro, 3.4f, carmesi);
        ScreenFlash.Destello(new Color(1f, 0.2f, 0.2f, 0.3f), 0.25f);
        Sonar("jefe_nova");
        Sacudir(1.6f);
        var r = GolpearCirculo(centro, 5f, danoFuerte, EstadoPlayer.Sangrado, 50f);
        if (r == PlayerControler.ResultadoDano.Parry) { parado = true; yield break; }
        ExponerCorazon();
        yield return Esperar(0.6f);
    }

    // Aro que crece hasta el radio del ataque y parpadea: el aviso de las areas.
    private IEnumerator AroDeAviso(Vector2 centro, float radio, float segundos, float temblor)
    {
        SpriteRenderer aro = new GameObject("AroAviso").AddComponent<SpriteRenderer>();
        aro.sprite = EnemigoMago.CirculoSprite();
        aro.sortingLayerName = "VFX";
        aro.sortingOrder = 6;
        aro.sharedMaterial = EfectoVisual.MaterialSinLuz();
        aro.transform.position = centro;
        PeligrosJefe.Registrar(aro.gameObject);

        Vector3 base0 = visual.localPosition;
        for (float t = 0f; t < segundos; t += Time.deltaTime)
        {
            float u = t / segundos;
            float d = radio * 2f * Mathf.Lerp(0.25f, 1f, u);
            aro.transform.localScale = new Vector3(d, d, 1f);
            Color c = carmesi;
            c.a = 0.25f + 0.45f * (0.5f + 0.5f * Mathf.Sin(t * Mathf.Lerp(15f, 45f, u)));
            aro.color = c;
            visual.localPosition = base0 + new Vector3(Random.Range(-temblor, temblor), 0f, 0f);
            yield return null;
        }
        visual.localPosition = base0;
        Destroy(aro.gameObject);
    }

    // Area magica alrededor del jefe (onda de la transformacion, nova).
    private PlayerControler.ResultadoDano? GolpearCirculo(Vector2 centro, float radio, int dano,
                                                          EstadoPlayer estado = EstadoPlayer.Ninguno, float acumulacion = 0f)
    {
        foreach (Collider2D c in Physics2D.OverlapCircleAll(centro, radio))
        {
            if (!c.CompareTag("Player")) continue;
            PlayerControler p = c.GetComponent<PlayerControler>();
            if (p != null) return p.TakeDamage(dano, this, PlayerControler.TipoDano.Magico, estado, acumulacion);
        }
        return null;
    }

    // ------------------------------------------------------------------ Estados

    private IEnumerator Reposicionar()
    {
        MirarAlPlayer();
        float d = Mathf.Abs(DxPlayer);
        if (d < 6f || Random.value < 0.4f) yield break;

        // Se desliza hacia el player un momento, sin prisa.
        anim.Reproducir(fase2 ? "grande_guardia" : "guardia", true);
        float t = 0f;
        while (t < 0.8f && Mathf.Abs(DxPlayer) > 4f)
        {
            t += Time.deltaTime;
            MirarAlPlayer();
            Acelerar(mirada * velocidadDeslizar * (fase2 ? 1.4f : 1f), 14f);
            yield return null;
        }
        FrenarSuave(18f);
    }

    private IEnumerator Pausa(float segundos)
    {
        segundos = PausaDesafio(segundos);
        FrenarSuave(30f);
        anim.Reproducir(fase2 ? "grande_guardia" : "guardia");
        float t = 0f;
        while (t < segundos)
        {
            t += Time.deltaTime;
            MirarAlPlayer();
            yield return null;
        }
    }

    // Aturdido tras un parry: se tambalea con destellos blancos. Es la ventana
    // para castigarlo (y el contraataque del player hace x2).
    private IEnumerator Aturdido(float segundos)
    {
        Detener();
        anim.Reproducir(fase2 ? "grande_guardia" : "aturdido", true);
        TextoFlotante.Mostrar("Aturdido", (Vector2)transform.position + Vector2.up * 4.2f, new Color(1f, 0.95f, 0.7f), 1f);
        Color previo = colorAviso;
        colorAviso = Color.white;
        LanzarAviso(0.3f, 0.8f);
        colorAviso = previo;
        yield return Temblar(segundos, 0.08f);
    }

    protected override void AlMorir()
    {
        base.AlMorir();
        PeligrosJefe.LimpiarTodo();
        StartCoroutine(Muerte());
        AvisarDerrota();
    }

    private IEnumerator Muerte()
    {
        anim.Reproducir("muerte", true);
        ScreenFlash.Destello(new Color(1f, 0.2f, 0.2f, 0.5f), 0.6f);
        CamaraDinamica.Ampliar(6.4f, 3f);
        float t = 0f;
        float nube = 0f;
        while (t < 2.8f)
        {
            t += Time.deltaTime;
            if (sr != null) { Color c = sr.color; c.a = 1f - t / 2.8f; sr.color = c; }
            if ((nube -= Time.deltaTime) <= 0f)
            {
                nube = 0.15f;
                Vector2 p = (Vector2)transform.position + new Vector2(Random.Range(-1.6f, 1.6f), Random.Range(0.5f, 4f));
                ParticulasFx.Rafaga(p, 10, carmesi, new Color(0.3f, 0f, 0.05f), new Vector2(1f, 3f), -0.4f,
                                    new Vector2(0.08f, 0.2f), new Vector2(0.5f, 1.2f));
            }
            yield return null;
        }
    }

    // ------------------------------------------------------------------ Ayudas

    private IEnumerator Esperar(float segundos)
    {
        float t = 0f;
        while (t < segundos) { t += Time.deltaTime * (enFrenesi ? ritmoFrenesi : 1f); yield return null; }
    }

    private IEnumerator Temblar(float segundos, float fuerza)
    {
        Vector3 base0 = visual.localPosition;
        float t = 0f;
        while (t < segundos)
        {
            t += Time.deltaTime;
            visual.localPosition = base0 + new Vector3(Random.Range(-fuerza, fuerza), 0f, 0f);
            yield return null;
        }
        visual.localPosition = base0;
    }

    private IEnumerator Desvanecerse(float segundos, bool aparecer)
    {
        if (sr == null) yield break;
        float t = 0f;
        while (t < segundos)
        {
            t += Time.deltaTime;
            Color c = sr.color;
            c.a = aparecer ? t / segundos : 1f - t / segundos;
            sr.color = c;
            yield return null;
        }
        Color f = sr.color;
        f.a = aparecer ? 1f : 0f;
        sr.color = f;
    }

    private void MostrarSprite(bool mostrar)
    {
        if (sr == null) return;
        Color c = sr.color;
        c.a = mostrar ? 1f : 0f;
        sr.color = c;
    }

    // Golpe del cuerpo en una caja del mundo (no relativa a la mirada): fisico.
    private PlayerControler.ResultadoDano? GolpearMundo(Vector2 centro, Vector2 tamano, int dano,
                                                        EstadoPlayer estado = EstadoPlayer.Ninguno, float acumulacion = 0f)
    {
        return PeligrosJefe.GolpearCaja(centro, tamano, dano, this, out _, PlayerControler.TipoDano.Fisico, estado, acumulacion);
    }

    private void Sacudir(float fuerza)
    {
        if (impulso != null) impulso.GenerateImpulse(fuerza);
    }

    // Copia fantasma del sprite que se desvanece: la estela de la embestida.
    private void Estela()
    {
        if (sr == null) return;
        EstelaFantasma.Lanzar(sr.sprite, visual.position, visual.lossyScale, new Color(1f, 0.2f, 0.25f, 0.5f), 0.3f, sr.sortingLayerName, sr.sortingOrder - 1);
    }

}
