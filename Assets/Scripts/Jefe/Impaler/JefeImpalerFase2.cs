using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// El Impaler (2): la tregua al acabar la fase 1 y la fase 2.
//
// TREGUA: al vaciar su primera barra cae con su muerte y la flor roja se queda
// en la arena (el ultimo cuadro). Sale el cartel de jefe derrotado (solo el
// cartel y su sonido: la arena no se da por vencida), te llega un frasco de
// sangre gratis (una vez por pelea; si los tienes llenos, llega al beber uno) y
// la flor late. No recibe dano ni ataca, y no queda ningun hechizo en el aire.
// Renace cuando te alejas de la flor (un rato seguido) o, si no, pasado un
// tiempo; renacer es su muerte al reves, invulnerable y sin caja de golpe.
//
// FASE 2: segunda barra llena, la punta de la lanza arde (la copia del sprite
// con el brillo de la Sombra, sin sprites nuevos), va mas rapido, algunos golpes
// acumulan Quemadura y sus ataques suman hechizos de siempre (PeligrosJefe).
public partial class JefeImpaler
{
    [Header("Efectos de los hechizos (los clips del Wraith)")]
    public AnimadorHoja.Clip fxOnda;
    public AnimadorHoja.Clip fxPilar;
    public AnimadorHoja.Clip fxGlifo;
    public AnimadorHoja.Clip fxOrbe;
    public AnimadorHoja.Clip fxSangre;
    public AnimadorHoja.Clip fxMeteoro;
    public AnimadorHoja.Clip fxExplosion;
    public AnimadorHoja.Clip fxMedialuna;
    public AnimadorHoja.Clip fxVorticeInicio;
    public AnimadorHoja.Clip fxVorticeBucle;
    public AnimadorHoja.Clip fxVorticeFin;

    // Lo que leen las pruebas.
    public bool EnTregua { get; private set; }
    public Vector2 Flor { get; private set; }
    public string MotivoRenacer { get; private set; }
    public int Renacimientos { get; private set; }
    public int FrascosDados { get; private set; }
    public int FrascoPendiente => frascoPendiente;
    public readonly List<AjustesImpaler.TipoHechizo> HechizosLanzados = new List<AjustesImpaler.TipoHechizo>();
    public bool LanzaEncendida => lanza != null && lanza.enabled;

    private bool frascoDado, pausaInicial;
    private int frascoPendiente;
    private float destinoSalto = float.NaN;
    private SpriteRenderer lanza;
    private Material materialLanza;
    private LineRenderer circulo;

    // ------------------------------------------------------------------ Tregua

    private IEnumerator FinFase1()
    {
        transformando = true;
        GolpesActivos = 0;
        GolpeEnCurso = null;
        enGuardia = false;
        TerminarVuelo();
        PlayerControler.SiguienteOrigenX = float.NaN;
        PlayerControler.TonoSiguienteParry = 1f;
        QuitarAviso();
        QuitarAvisoRitmo();
        aturdimientoPendiente = 0f;
        if (visual != null) visual.localPosition = Vector3.zero;
        PeligrosJefe.LimpiarTodo();
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        CambiarEstado(EstadoEnemigo.Recuperacion);
        AvisarSilencio();
        Sonar("jefe_caida");
        anim.Reproducir("muerte", true);
        Flor = transform.position;
        EnTregua = true;
        StartCoroutine(CartelTregua());
        DarFrasco();

        // Renace al alejarte de la flor (un rato seguido) o, si no, pasado el tiempo maximo.
        float inicio = Time.time, lejos = 0f;
        string motivo = null;
        while (motivo == null)
        {
            bool caido = anim.Terminado;
            float queda = ajustes.tiempoMaxRenacer > 0f ? ajustes.tiempoMaxRenacer - (Time.time - inicio) : float.MaxValue;
            float latido = queda <= ajustes.segundosLatidoRapido ? ajustes.latidoFlorRapido : ajustes.latidoFlor;
            if (caido) Destello(ajustes.colorFlor, ajustes.brilloFlor * (0.5f - 0.5f * Mathf.Cos((Time.time - inicio) * latido * Mathf.PI * 2f)));
            LatidoActual = caido ? latido : 0f;
            Circulo(DepuracionCazadora.VerCajas);
            BuscarPlayer();
            lejos = player != null && Vector2.Distance(PosPlayer, Flor) > ajustes.distanciaRenacer ? lejos + Time.deltaTime : 0f;
            if (caido && lejos >= ajustes.tiempoLejos) motivo = "distancia";
            else if (queda <= 0f) motivo = "tiempo";
            yield return null;
        }
        EnTregua = false;
        MotivoRenacer = motivo;
        Circulo(false);
        Destello(ajustes.colorFlor, 0f);
        LatidoActual = 0f;

        // Renace: la muerte al reves (la flor se cierra, vuelve el cuerpo y la lanza
        // a la mano). Sigue invulnerable y sin caja de golpe hasta que acaba.
        anim.Reproducir("renacer", true, ajustes.velocidadRenacer);
        Sonar("jefe_revivir");
        while (!anim.Terminado) yield return null;
        ScreenFlash.Destello(new Color(ajustes.colorLanzaFase2.r, ajustes.colorLanzaFase2.g, ajustes.colorLanzaFase2.b, 0.35f), 0.4f);
        if (efectos != null) efectos.Chispas((Vector2)transform.position + Vector2.up * 1.4f, ajustes.chispas * 2, ajustes.colorLanzaFase2, ajustes.colorChispas, 90f);
        fase2 = true;
        PonerFase2(true);
        salud.Revivir(VidaDesafio(ajustes.vidaFase2));
        Renacimientos++;
        AvisarCambioFase();
        transformando = false;
        pausaInicial = true;
        Pensar();
    }

    public float LatidoActual { get; private set; }

    // El mismo cartel que al vencer (MensajePantalla), sin dar la pelea por ganada:
    // nada de almas, Bitacora, logros, puertas ni musica de victoria.
    private IEnumerator CartelTregua()
    {
        yield return new WaitForSeconds(ajustes.esperaCartel);
        if (!EnTregua) yield break;
        MensajePantalla.Banner(ajustes.textoTregua, ajustes.colorTregua, ajustes.duracionCartel);
        Sonido.Reproducir("victoria");
        CartelesTregua++;
    }
    public int CartelesTregua { get; private set; }

    // Frasco de sangre gratis: +1 carga sin pasar del maximo (nada de mana ni de
    // lo guardado). Con los frascos llenos se guarda y llega al beber uno.
    private void DarFrasco()
    {
        if (!ajustes.frascoGratis || frascoDado || ajustes.frascosGratis <= 0) return;
        frascoDado = true;
        ReservaPociones r = ReservaPociones.Get();
        for (int i = 0; i < ajustes.frascosGratis; i++)
        {
            if (r.Cargas < r.Maximo) r.Devolver(ReservaPociones.Tipo.Vida);
            else frascoPendiente++;
        }
        FrascosDados += ajustes.frascosGratis;
        AvisoObjeto.Mostrar(ReservaPociones.Frasco(), ajustes.textoFrasco, ajustes.descripcionFrasco);
    }

    private void AlBeber()
    {
        if (frascoPendiente <= 0) return;
        ReservaPociones r = ReservaPociones.Get();
        if (r.Cargas >= r.Maximo) return;
        frascoPendiente--;
        r.Devolver(ReservaPociones.Tipo.Vida);
    }

    protected override void OnEnable() { base.OnEnable(); ReservaPociones.AlBeber += AlBeber; }
    protected override void OnDisable() { base.OnDisable(); ReservaPociones.AlBeber -= AlBeber; }

    // Circulo de la distancia de renacer alrededor de la flor (con las cajas de la sala).
    private void Circulo(bool ver)
    {
        if (!ver) { if (circulo != null) circulo.enabled = false; return; }
        if (circulo == null)
        {
            GameObject go = new GameObject("DistanciaRenacer");
            go.transform.SetParent(transform, false);
            circulo = go.AddComponent<LineRenderer>();
            circulo.useWorldSpace = true;
            circulo.loop = true;
            circulo.positionCount = 48;
            circulo.widthMultiplier = 0.05f;
            circulo.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
            circulo.sortingLayerName = "VFX";
        }
        circulo.enabled = true;
        circulo.startColor = circulo.endColor = ajustes.colorFlor;
        for (int i = 0; i < 48; i++)
        {
            float a = i / 48f * Mathf.PI * 2f;
            circulo.SetPosition(i, Flor + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * ajustes.distanciaRenacer);
        }
    }

    // ------------------------------------------------------------------ Fase 2: lanza encendida

    private void PonerFase2(bool poner)
    {
        if (srImpaler != null) srImpaler.color = poner ? ajustes.tinteFase2 : Color.white;
        if (!poner) { if (lanza != null) lanza.enabled = false; return; }
        if (lanza == null && ajustes.shaderBrillo != null && srImpaler != null)
        {
            materialLanza = new Material(ajustes.shaderBrillo);
            materialLanza.SetColor("_Key1", ajustes.lanza1);
            materialLanza.SetColor("_Key2", ajustes.lanza2);
            materialLanza.SetColor("_Key3", ajustes.lanza3);
            materialLanza.SetFloat("_Tolerance", ajustes.toleranciaLanza);
            GameObject go = new GameObject("LanzaEncendida");
            go.transform.SetParent(visual, false);
            lanza = go.AddComponent<SpriteRenderer>();
            lanza.sharedMaterial = materialLanza;
        }
        if (lanza != null) lanza.enabled = true;
    }

    protected override void LateUpdate()
    {
        base.LateUpdate();
        if (lanza == null || !lanza.enabled || srImpaler == null) return;
        lanza.sprite = srImpaler.sprite;
        lanza.flipX = srImpaler.flipX;
        lanza.sortingLayerID = srImpaler.sortingLayerID;
        lanza.sortingOrder = srImpaler.sortingOrder + 1;
        materialLanza.SetColor("_GlowColor", ajustes.colorLanzaFase2);
        materialLanza.SetFloat("_Amount", ajustes.brilloLanza * (0.75f + 0.25f * Mathf.Sin(Time.time * 6f)));
    }

    // ------------------------------------------------------------------ Fase 2: Quemadura

    // Solo si te da (bloqueado, un 30 %) y te queda vida de sobra: asi nunca mata sin aviso.
    private void Quemar(AjustesImpaler.Golpe g, PlayerControler.ResultadoDano? r, PlayerControler pc)
    {
        if (!fase2 || g.quemadura <= 0f || pc == null) return;
        if (r != PlayerControler.ResultadoDano.Recibido && r != PlayerControler.ResultadoDano.Bloqueado) return;
        if (pc.VidaActual < ajustes.quemaduraVidaMinima * pc.VidaMaxima) return;
        EstadosPlayer.Acumular(pc, EstadoPlayer.Quemadura, r == PlayerControler.ResultadoDano.Bloqueado ? g.quemadura * 0.3f : g.quemadura);
        QuemadurasDadas++;
    }
    public int QuemadurasDadas { get; private set; }

    // ------------------------------------------------------------------ Fase 2: hechizos

    // Los hechizos de un golpe en ese momento. "ultimo": su golpe es el ultimo de lo
    // que hace ahora (asi los que salen despues no caen encima del golpe siguiente).
    private void Hechizos(AjustesImpaler.Ataque a, int golpe, AjustesImpaler.Momento momento, bool ultimo)
    {
        if (!fase2 || a.hechizosFase2 == null) return;
        foreach (AjustesImpaler.Hechizo h in a.hechizosFase2)
        {
            if (h == null || !h.activo || h.golpe != golpe || h.momento != momento || (h.soloSiEsElUltimo && !ultimo)) continue;
            if (h.retraso > 0f) StartCoroutine(HechizoLuego(h, a));
            else Hechizo(h, a);
        }
    }

    private IEnumerator HechizoLuego(AjustesImpaler.Hechizo h, AjustesImpaler.Ataque a)
    {
        int dir = mirada;
        Vector2 pie = transform.position;
        for (float t = 0f; t < h.retraso; t += Time.deltaTime) yield return null;
        if (!transformando && !salud.Muerto) Hechizo(h, a, dir, pie);
    }

    private void Hechizo(AjustesImpaler.Hechizo h, AjustesImpaler.Ataque a) => Hechizo(h, a, mirada, transform.position);

    private void Hechizo(AjustesImpaler.Hechizo h, AjustesImpaler.Ataque a, int dir, Vector2 pie)
    {
        Vector2 origen = pie;
        if (h.desdeElEco)
        {
            AjustesImpaler.Golpe eco = a.golpes.Find(x => !Mathf.Approximately(x.origenX, 0f));
            if (eco != null) origen.x += dir * eco.origenX;
            dir = -dir;
        }
        float sueloY = Suelo(origen.x, pie.y);
        EstadoPlayer sangra = h.sangrado > 0f ? EstadoPlayer.Sangrado : EstadoPlayer.Ninguno;
        switch (h.tipo)
        {
            case AjustesImpaler.TipoHechizo.Vortice:
                VorticeSangre.Invocar(fxVorticeInicio, fxVorticeBucle, fxVorticeFin, new Vector2(EnArena(origen.x + dir * h.distancia), sueloY + 1.2f),
                                      h.duracion, h.radio, h.fuerza, h.dano, h.sangrado, ajustes.colorHechizos);
                break;
            case AjustesImpaler.TipoHechizo.Onda:
                OndaCarmesi.Lanzar(fxOnda, new Vector2(origen.x + dir * h.distancia, sueloY), dir, h.velocidad, h.dano, 18f, ajustes.colorOnda, 1.2f, sangra, h.sangrado);
                break;
            case AjustesImpaler.TipoHechizo.MediaLuna:
                MedialunaSangre.Lanzar(fxMedialuna, new Vector2(origen.x + dir * h.distancia, sueloY + 1.1f), dir, h.velocidad, h.dano, transform, ajustes.colorHechizos, sangra, h.sangrado);
                break;
            case AjustesImpaler.TipoHechizo.Orbe:
                OrbeSangre.Lanzar(fxOrbe, fxSangre, new Vector2(origen.x + dir * h.distancia, sueloY + 1.6f), new Vector2(dir, 0.5f), h.velocidad, 90f,
                                  h.duracion, h.dano, h.sangrado, transform, ajustes.colorHechizos);
                break;
            case AjustesImpaler.TipoHechizo.Meteoros:
            {
                // Alrededor de donde aterriza (o de donde esta).
                float centro = float.IsNaN(destinoSalto) ? pie.x : destinoSalto;
                for (int i = 0; i < h.cantidad; i++)
                {
                    float x = EnArena(centro + (i - (h.cantidad - 1) * 0.5f) * h.separacion + dir * h.distancia);
                    Meteoro.Caer(fxMeteoro, fxExplosion, fxGlifo, new Vector2(x, Suelo(x, pie.y)), arena.height > 0f ? arena.yMax + 1f : pie.y + 12f,
                                 h.aviso, h.dano, ajustes.colorMeteoro);
                }
                break;
            }
            case AjustesImpaler.TipoHechizo.Pilares:
            {
                // En fila desde "distancia", alejandose del jefe (detras si es negativa: la estela).
                float sentido = h.distancia < 0f ? -1f : 1f;
                for (int i = 0; i < h.cantidad; i++)
                {
                    float x = EnArena(pie.x + dir * (h.distancia + sentido * i * h.separacion));
                    PilarSangre.Invocar(fxGlifo, fxPilar, new Vector2(x, Suelo(x, pie.y)), h.aviso, h.dano, h.sangrado, ajustes.colorHechizos);
                }
                break;
            }
        }
        HechizosLanzados.Add(h.tipo);
    }

    private float Suelo(float x, float y) => PeligrosJefe.SueloBajo(new Vector2(x, y + 0.5f), y);

    private float EnArena(float x) => arena.width > 0f ? Mathf.Clamp(x, arena.xMin + 0.5f, arena.xMax - 0.5f) : x;
}
