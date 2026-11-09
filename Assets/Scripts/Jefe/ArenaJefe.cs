using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// La arena de un jefe. Al pisar el activador de su ZonaJefe (sin ZonaJefe: el
// trigger de este objeto):
//   - El muro de niebla se forma detras del player: no se sale hasta vencer.
//   - Empieza la musica de la fase 1 con un fundido.
//   - Aparece el jefe, su barra grande y su nombre.
// En la fase 2 la luz se tine, caen particulas (brasas o nieve) y la musica pasa
// a la pista de la fase 2, en el momento exacto del cambio.
// Al vencer: se abre la niebla y la salida, se apaga la musica y sale el cartel.
// Si el player muere, todo vuelve a como estaba antes de entrar.
//
// Musica por fases: cada fase tiene su pista, el segundo por el que empieza y el
// tramo que se repite (bucle). Asi una misma pista puede dar la parte tranquila
// a la fase 1 y el climax a la fase 2 sin empezar siempre desde el principio.
[RequireComponent(typeof(Collider2D))]
public class ArenaJefe : MonoBehaviour, IArenaJefe
{
    // Se lanza al vencer al jefe (la parte de las pociones mira aqui el reto).
    public static event System.Action AlVencer;
    // Se lanza al empezar cada intento.
    public static event System.Action AlEmpezarCombate;
    // Hay un intento en curso (la pantalla de muerte saca burlas del jefe).
    public static bool EnCombate { get; private set; }
    // El jefe ya esta en su fase 2 (el viento de la nieve sopla mas fuerte).
    public static bool EnFase2 { get; private set; }

    // Otra arena (la de la Cazadora) avisa de su combate y de su victoria por
    // aqui: la musica del nivel, la pantalla de muerte y los desafios lo miran.
    public static void CombateExterno(bool enCombate, bool fase2 = false)
    {
        EnCombate = enCombate;
        EnFase2 = enCombate && fase2;
        if (enCombate) AlEmpezarCombate?.Invoke();
    }

    public static void VictoriaExterna()
    {
        EnCombate = false;
        EnFase2 = false;
        AlVencer?.Invoke();
    }

    private enum Estado { Esperando, Combate, Vencido }

    [Header("Jefe")]
    [SerializeField] private JefeBase prefabJefe;
    [SerializeField] private string nombre = "Crimson Wraith";
    [SerializeField] private string titulo = "El Espectro Carmesí";
    // Limites de la arena (para el jefe y sus ataques) y altura del suelo.
    [SerializeField] private Rect zona = Rect.MinMaxRect(140.5f, 4f, 176f, 22f);
    [SerializeField] private float suelo = 4f;
    // Frases del jefe para la pantalla de muerte (vacio = las de siempre).
    [SerializeField] private string[] burlas;

    [Header("Segundo jefe (opcional: un duo)")]
    [Tooltip("Vacio = un solo jefe. Con dos, cada uno tiene su barra (la segunda encima) y se gana al caer los dos.")]
    [SerializeField] private JefeBase prefabJefe2;
    [SerializeField] private string nombreJefe1 = "";
    [SerializeField] private string nombreJefe2 = "";
    [Tooltip("Distancia entre los dos al aparecer (unidades).")]
    [SerializeField] private float separacionDuo = 6f;

    [Header("Barra")]
    // Marca en la barra donde cambia de fase (0 = sin marca: cada fase tiene su barra).
    [Range(0f, 1f)] [SerializeField] private float marcaFase = 0.5f;
    [SerializeField] private bool barraPorFase;
    [SerializeField] private Color colorBarraFase2 = new Color(0.25f, 0.6f, 0.95f, 1f);
    [SerializeField] private string nombreFase2 = "";

    [Header("Jefe principal o minijefe")]
    [Tooltip("Marcado: el jefe del nivel (su victoria cuenta para la partida, la Bitacora, los logros y el desafio). " +
             "Desmarcado: un minijefe opcional, que solo guarda su propia bandera y no avisa a nadie mas.")]
    [SerializeField] private bool jefePrincipal = true;
    [Tooltip("Solo minijefes: nombre de su bandera de vencido (minijefe_<escena>_<clave>).")]
    [SerializeField] private string claveMinijefe = "minijefe";
    public bool EsPrincipal => jefePrincipal;
    private string BanderaVencido => jefePrincipal ? "jefe_" + gameObject.scene.name : "minijefe_" + gameObject.scene.name + "_" + claveMinijefe;

    [Header("Recompensa")]
    [Tooltip("Objetos de mejora que da el jefe al caer (uno por entrada). El player los levanta, como en los Souls. Solo la primera vez.")]
    [SerializeField] private Equipo.Objeto[] recompensa = new Equipo.Objeto[0];

    [Header("Niebla y salida")]
    [SerializeField] private Collider2D muroNiebla;
    [SerializeField] private SpriteRenderer[] visualNiebla;
    [SerializeField] private Color nieblaAbierta = new Color(0.8f, 0.15f, 0.2f, 0.25f);
    [SerializeField] private Color nieblaCerrada = new Color(0.9f, 0.1f, 0.15f, 0.75f);
    [SerializeField] private GameObject salida;

    [Header("Musica: fase 1")]
    [Tooltip("Archivo de configuracion del nivel: si tiene musica de jefe, se usa esa en vez de la de aqui abajo.")]
    [SerializeField] private ConfigNivel config;
    [SerializeField] private AudioClip musica;
    [Range(0f, 1f)] [SerializeField] private float volumen = 0.6f;
    [SerializeField] private float inicioFase1;
    // Tramo que se repite (x = desde, y = hasta). y <= 0: toda la pista.
    [SerializeField] private Vector2 bucleFase1;

    [Header("Musica: fase 2")]
    [SerializeField] private AudioClip musicaFase2;
    [Range(0f, 1f)] [SerializeField] private float volumenFase2 = 0.7f;
    [SerializeField] private float inicioFase2;
    [SerializeField] private Vector2 bucleFase2;

    [Header("Fase 2")]
    [SerializeField] private Color luzFase2 = new Color(1f, 0.55f, 0.55f, 1f);
    [SerializeField] private string textoFase2 = "El Espectro Carmesí revela su verdadera forma...";
    [SerializeField] private Color particulasFase2A = new Color(1f, 0.3f, 0.2f, 0.9f);
    [SerializeField] private Color particulasFase2B = new Color(1f, 0.7f, 0.3f, 0.7f);
    // true: las particulas caen (nieve); false: suben (brasas).
    [SerializeField] private bool particulasCaen;
    [SerializeField] private string bannerVictoria = "ENEMIGO CAÍDO";

    private Estado estado;
    private JefeBase jefe, jefe2;
    private BarraJefe barra, barra2;
    private int derrotados;
    public bool EsDuo => prefabJefe2 != null;
    private AudioSource audioSrc, audioFase2;
    private Vector2 bucleActual;
    private AudioSource fuenteActual;
    private Light2D luzGlobal;
    private Color luzOriginal;
    private float intensidadOriginal;
    private ParticleSystem brasas;
    private Coroutine fundido, fundido2;

    // Reto opcional: vencer sin beber en el combate, y vencer al primer intento
    // (sin morir ni volver a la hoguera entre intentos).
    private int intentos;
    private bool pocionUsada;

    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
        if (config != null)
        {
            if (config.jefeFase1.pista != null)
            {
                musica = config.jefeFase1.pista; volumen = config.jefeFase1.volumen;
                inicioFase1 = config.jefeFase1.inicio; bucleFase1 = config.jefeFase1.bucle;
            }
            if (config.jefeFase2.pista != null)
            {
                musicaFase2 = config.jefeFase2.pista; volumenFase2 = config.jefeFase2.volumen;
                inicioFase2 = config.jefeFase2.inicio; bucleFase2 = config.jefeFase2.bucle;
            }
            if (config.frasesJefe != null && config.frasesJefe.Length > 0) burlas = config.frasesJefe;
        }
        audioSrc = NuevaFuente();
        audioFase2 = NuevaFuente();

        foreach (Light2D l in FindObjectsByType<Light2D>(FindObjectsSortMode.None))
            if (l.lightType == Light2D.LightType.Global) { luzGlobal = l; break; }
        if (luzGlobal != null) { luzOriginal = luzGlobal.color; intensidadOriginal = luzGlobal.intensity; }

        PonerNiebla(false);
        if (salida != null) salida.SetActive(false);

        // Jefe ya vencido en esta partida: la arena queda abierta y la salida puesta.
        if (Partida.Bandera(BanderaVencido))
        {
            estado = Estado.Vencido;
            if (salida != null) salida.SetActive(true);
        }
    }

    private AudioSource NuevaFuente()
    {
        AudioSource a = gameObject.AddComponent<AudioSource>();
        a.loop = true;
        a.playOnAwake = false;
        a.volume = 0f;
        return a;
    }

    private void OnEnable()
    {
        GameManager.AlReaparecerPlayer += Reiniciar;
        ReservaPociones.AlBeber += MarcarPocion;
        ControlVolumen.AlCambiar += AjustarVolumen;
    }

    private void OnDisable()
    {
        GameManager.AlReaparecerPlayer -= Reiniciar;
        ReservaPociones.AlBeber -= MarcarPocion;
        ControlVolumen.AlCambiar -= AjustarVolumen;
        EnCombate = false;
        EnFase2 = false;
        PantallaMuerte.BurlasJefe = null;
    }

    // Cambio de volumen desde el menu de pausa con la musica sonando.
    private void AjustarVolumen()
    {
        if (estado != Estado.Combate || fundido != null || fundido2 != null || fuenteActual == null) return;
        fuenteActual.volume = (fuenteActual == audioFase2 ? volumenFase2 : volumen) * ControlVolumen.Musica;
    }

    private void MarcarPocion()
    {
        if (estado == Estado.Combate) pocionUsada = true;
    }

    // ------------------------------------------------------------------ Zona de entrada (ZonaJefe)

    // Con una ZonaJefe enlazada, la pelea la empieza su activador (y no el
    // trigger de la arena) y la niebla es su muro.
    public ZonaJefe ZonaEntrada { get; set; }
    public bool PuedeEmpezar => estado == Estado.Esperando && prefabJefe != null;
    public Vector2 PuntoJefe => new Vector2(zona.center.x, suelo + 2f);
    public void EmpezarDesdeZona(PlayerControler p) { if (PuedeEmpezar) Empezar(); }

    private void OnTriggerEnter2D(Collider2D otro)
    {
        if (ZonaEntrada != null || estado != Estado.Esperando || !otro.CompareTag("Player") || prefabJefe == null) return;
        Empezar();
    }

    // El bucle de cada tramo: al pasar del final vuelve al principio del tramo.
    private void Update()
    {
        if (fuenteActual == null || !fuenteActual.isPlaying || bucleActual.y <= 0f) return;
        if (fuenteActual.time >= bucleActual.y) fuenteActual.time = bucleActual.x;
    }

    private void Empezar()
    {
        estado = Estado.Combate;
        EnCombate = true;
        EnFase2 = false;
        PonerNiebla(true);
        if (ZonaEntrada != null) ZonaEntrada.Cerrar();
        intentos++;
        pocionUsada = false;
        PantallaMuerte.BurlasJefe = burlas != null && burlas.Length > 0 ? burlas : null;
        AlEmpezarCombate?.Invoke();
        if (jefePrincipal) Bitacora.Intento(FichaJefe.DeEscena(gameObject.scene.name));

        if (musica != null) Sonar(audioSrc, musica, inicioFase1, bucleFase1, volumen, 2f);

        derrotados = 0;
        bool duo = EsDuo;
        float dx = duo ? separacionDuo * 0.5f : 0f;
        barra = BarraJefe.Crear(duo && !string.IsNullOrEmpty(nombreJefe1) ? nombreJefe1 : nombre, barraPorFase ? 0f : marcaFase);
        jefe = Aparecer(prefabJefe, zona.center.x - dx, barra);
        jefe.AlCambiarFase += Fase2;
        jefe.AlSilencio += Silencio;
        bool titulado = false;
        void Titulo()
        {
            if (titulado) return;
            titulado = true;
            MensajePantalla.Titulo(nombre.ToUpper(), titulo);
        }
        jefe.AlAterrizar += () => { Titulo(); if (barra != null) barra.Mostrar(true); };
        if (!duo) return;

        // Duo: el segundo, al otro lado, con su barra encima de la primera.
        barra2 = BarraJefe.Crear(string.IsNullOrEmpty(nombreJefe2) ? nombre : nombreJefe2, 0f);
        barra2.Apilar(1);
        jefe2 = Aparecer(prefabJefe2, zona.center.x + dx, barra2);
        jefe2.AlAterrizar += () => { Titulo(); if (barra2 != null) barra2.Mostrar(true); };
    }

    // Un jefe de la arena con su barra. Se gana cuando caen todos.
    private JefeBase Aparecer(JefeBase prefab, float x, BarraJefe suBarra)
    {
        JefeBase j = Instantiate(prefab, new Vector3(x, zona.yMax, 0f), Quaternion.identity);
        j.Configurar(zona, suelo);
        // Desafio en Dificil: un leve contorno rojizo para distinguirlo.
        if (Desafio.Activo && Desafio.Dificil) j.gameObject.AddComponent<BrilloDificil>();
        EnemyHealth salud = j.GetComponent<EnemyHealth>();
        salud.AlCambiarVida += suBarra.Actualizar;
        suBarra.PonerEstados(salud);
        j.AlDerrotado += () =>
        {
            derrotados++;
            if (suBarra != null && EsDuo) suBarra.Mostrar(false);
            if (derrotados >= (EsDuo ? 2 : 1)) Victoria();
        };
        return j;
    }

    private void Sonar(AudioSource fuente, AudioClip clip, float inicio, Vector2 bucle, float vol, float entrada)
    {
        fuente.clip = clip;
        fuente.time = Mathf.Clamp(inicio, 0f, Mathf.Max(0f, clip.length - 1f));
        fuente.Play();
        fuenteActual = fuente;
        bucleActual = bucle;
        if (fuente == audioFase2) Fundir2(vol * ControlVolumen.Musica, entrada);
        else Fundir(vol * ControlVolumen.Musica, entrada);
    }

    // El jefe cae y parece muerto: la musica casi se apaga (la fase 2 entra despues).
    private void Silencio()
    {
        if (fuenteActual == audioSrc) Fundir(volumen * ControlVolumen.Musica * 0.12f, 1.2f);
    }

    private void Fase2()
    {
        EnFase2 = true;
        if (!string.IsNullOrEmpty(textoFase2)) MensajePantalla.Narrativo(textoFase2, 3.5f);
        StartCoroutine(TenirLuz(luzFase2, intensidadOriginal * 0.9f, 2f));
        CrearParticulas();
        if (barraPorFase && barra != null) barra.NuevaFase(colorBarraFase2, nombreFase2);

        // La musica cambia justo ahora: la de la fase 2 entra rapido y fuerte.
        if (musicaFase2 != null)
        {
            Fundir(0f, 0.8f);
            Sonar(audioFase2, musicaFase2, inicioFase2, bucleFase2, volumenFase2, 0.6f);
        }
        else if (fuenteActual == audioSrc) Fundir(volumen * ControlVolumen.Musica, 0.8f);
    }

    private void Victoria()
    {
        estado = Estado.Vencido;
        EnCombate = false;
        EnFase2 = false;
        PantallaMuerte.BurlasJefe = null;
        Fundir(0f, 3f);
        Fundir2(0f, 3f);
        if (barra != null) barra.Mostrar(false);
        if (barra2 != null) barra2.Mostrar(false);
        StartCoroutine(TrasVictoria());
    }

    private IEnumerator TrasVictoria()
    {
        yield return new WaitForSeconds(1.5f);
        MensajePantalla.Banner(bannerVictoria, new Color(1f, 0.85f, 0.45f), 4f);
        Sonido.Reproducir("victoria");
        PonerNiebla(false, true);
        if (ZonaEntrada != null) ZonaEntrada.Abrir(true);
        if (salida != null) salida.SetActive(true);
        StartCoroutine(TenirLuz(luzOriginal, intensidadOriginal, 3f));
        if (brasas != null) brasas.Stop();
        // El jefe del nivel avisa a todos (partida, Bitacora, logros, desafio); un minijefe solo apunta lo suyo.
        if (jefePrincipal) AlVencer?.Invoke();
        else Partida.PonerBandera(BanderaVencido);
        StartCoroutine(DarRecompensa());

        // Reconocimientos del reto, despues del cartel.
        yield return new WaitForSeconds(4.5f);
        if (!pocionUsada)
        {
            MensajePantalla.Banner("RETO: VENCIDO SIN POCIONES", new Color(0.7f, 0.95f, 1f), 3.5f);
            yield return new WaitForSeconds(4.5f);
        }
        if (intentos == 1)
            MensajePantalla.Banner("RETO: A LA PRIMERA", new Color(1f, 0.75f, 0.35f), 3.5f);
    }

    // Tras el cartel de victoria, el player levanta lo que suelta el jefe (una vez
    // por partida).
    private IEnumerator DarRecompensa()
    {
        string clave = jefePrincipal ? "recompensa_" + gameObject.scene.name : "recompensa_" + gameObject.scene.name + "_" + claveMinijefe;
        if (recompensa == null || recompensa.Length == 0 || Partida.Bandera(clave)) yield break;
        Partida.PonerBandera(clave);
        yield return new WaitForSeconds(2.5f);
        foreach (Equipo.Objeto o in recompensa)
        {
            // Al inventario: se aplica en la hoguera.
            Inventario.Guardar(o);
            PlayerControler p = FindFirstObjectByType<PlayerControler>();
            Sprite icono = RecursosRPG.Get().Icono(Equipo.ClaveIcono(o));
            if (p == null) { AvisoObjeto.Mostrar(icono, Equipo.Nombre(o), Inventario.Aviso); continue; }
            p.LevantarObjeto(icono, Equipo.Nombre(o), Inventario.Aviso);
            yield return new WaitForSeconds(0.5f);
            while (p != null && p.LevantandoObjeto) yield return null;
            yield return new WaitForSeconds(0.3f);
        }
    }

    // El player ha muerto: se deshace el intento.
    private void Reiniciar()
    {
        if (estado != Estado.Combate) return;
        estado = Estado.Esperando;
        EnCombate = false;
        EnFase2 = false;
        PeligrosJefe.LimpiarTodo();
        if (jefe != null) Destroy(jefe.gameObject);
        if (barra != null) Destroy(barra.gameObject);
        if (jefe2 != null) Destroy(jefe2.gameObject);
        if (barra2 != null) Destroy(barra2.gameObject);
        Fundir(0f, 1f);
        Fundir2(0f, 1f);
        PonerNiebla(false);
        if (ZonaEntrada != null) ZonaEntrada.Reiniciar();
        if (luzGlobal != null) { luzGlobal.color = luzOriginal; luzGlobal.intensity = intensidadOriginal; }
        if (brasas != null) Destroy(brasas.gameObject);
    }

    // ------------------------------------------------------------------ Ayudas

    private void PonerNiebla(bool cerrada, bool desvanecer = false)
    {
        if (muroNiebla != null) muroNiebla.enabled = cerrada;
        if (visualNiebla == null) return;
        foreach (SpriteRenderer s in visualNiebla)
        {
            if (s == null) continue;
            s.gameObject.SetActive(!desvanecer || cerrada);
            s.color = cerrada ? nieblaCerrada : nieblaAbierta;
        }
    }

    private void Fundir(float destino, float segundos)
    {
        if (fundido != null) StopCoroutine(fundido);
        fundido = StartCoroutine(FundirMusica(audioSrc, destino, segundos, () => fundido = null));
    }

    private void Fundir2(float destino, float segundos)
    {
        if (fundido2 != null) StopCoroutine(fundido2);
        fundido2 = StartCoroutine(FundirMusica(audioFase2, destino, segundos, () => fundido2 = null));
    }

    private IEnumerator FundirMusica(AudioSource fuente, float destino, float segundos, System.Action fin)
    {
        float inicio = fuente.volume;
        for (float t = 0f; t < segundos; t += Time.unscaledDeltaTime)
        {
            fuente.volume = Mathf.Lerp(inicio, destino, t / segundos);
            yield return null;
        }
        fuente.volume = destino;
        if (destino <= 0f) fuente.Stop();
        fin();
    }

    private IEnumerator TenirLuz(Color color, float intensidad, float segundos)
    {
        if (luzGlobal == null) yield break;
        Color c0 = luzGlobal.color;
        float i0 = luzGlobal.intensity;
        for (float t = 0f; t < segundos; t += Time.deltaTime)
        {
            luzGlobal.color = Color.Lerp(c0, color, t / segundos);
            luzGlobal.intensity = Mathf.Lerp(i0, intensidad, t / segundos);
            yield return null;
        }
        luzGlobal.color = color;
        luzGlobal.intensity = intensidad;
    }

    // Particulas por toda la arena en la fase 2: brasas que suben o nieve que cae.
    private void CrearParticulas()
    {
        GameObject go = new GameObject("ParticulasArena");
        go.transform.position = new Vector3(zona.center.x, particulasCaen ? zona.yMax : zona.yMin, 0f);
        brasas = go.AddComponent<ParticleSystem>();
        brasas.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = brasas.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.14f);
        main.startColor = new ParticleSystem.MinMaxGradient(particulasFase2A, particulasFase2B);
        main.gravityModifier = particulasCaen ? 0.04f : -0.05f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 300;
        var em = brasas.emission;
        em.rateOverTime = 35f;
        var sh = brasas.shape;
        sh.shapeType = ParticleSystemShapeType.Box;
        sh.scale = new Vector3(zona.width, 0.5f, 1f);
        sh.rotation = new Vector3(particulasCaen ? 90f : -90f, 0f, 0f);
        var ruido = brasas.noise;
        ruido.enabled = true;
        ruido.strength = 0.4f;
        ParticleSystemRenderer pr = go.GetComponent<ParticleSystemRenderer>();
        pr.sharedMaterial = EfectoVisual.MaterialSinLuz();
        pr.sortingLayerName = "Middleground";
        brasas.Play();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.2f, 0.2f);
        Gizmos.DrawWireCube(zona.center, zona.size);
    }
}
