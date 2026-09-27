using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// La arena del jefe. Al entrar el player (trigger de este objeto):
//   - La niebla de la puerta se vuelve solida: no se sale hasta vencer.
//   - Empieza la musica del jefe con un fundido.
//   - Aparece el jefe (cae del techo), su barra grande y su nombre.
// En la fase 2 la luz de la cueva se tine de rojo y caen brasas.
// Al vencer: se abre la niebla y la salida, se apaga la musica y sale el cartel.
// Si el player muere, todo vuelve a como estaba antes de entrar.
[RequireComponent(typeof(Collider2D))]
public class ArenaJefe : MonoBehaviour
{
    // Se lanza al vencer al jefe (la parte de las pociones mira aqui el reto).
    public static event System.Action AlVencer;
    // Se lanza al empezar cada intento.
    public static event System.Action AlEmpezarCombate;
    // Hay un intento en curso (la pantalla de muerte saca burlas del jefe).
    public static bool EnCombate { get; private set; }

    private enum Estado { Esperando, Combate, Vencido }

    [Header("Jefe")]
    [SerializeField] private JefeWraith prefabJefe;
    [SerializeField] private string nombre = "Crimson Wraith";
    [SerializeField] private string titulo = "El Espectro Carmesí";
    // Limites de la arena (para el jefe y sus ataques) y altura del suelo.
    [SerializeField] private Rect zona = Rect.MinMaxRect(140.5f, 4f, 176f, 22f);
    [SerializeField] private float suelo = 4f;

    [Header("Niebla y salida")]
    [SerializeField] private Collider2D muroNiebla;
    [SerializeField] private SpriteRenderer[] visualNiebla;
    [SerializeField] private Color nieblaAbierta = new Color(0.8f, 0.15f, 0.2f, 0.25f);
    [SerializeField] private Color nieblaCerrada = new Color(0.9f, 0.1f, 0.15f, 0.75f);
    [SerializeField] private GameObject salida;

    [Header("Musica")]
    [SerializeField] private AudioClip musica;
    [Range(0f, 1f)] [SerializeField] private float volumen = 0.6f;

    [Header("Fase 2")]
    [SerializeField] private Color luzFase2 = new Color(1f, 0.55f, 0.55f, 1f);

    private Estado estado;
    private JefeWraith jefe;
    private BarraJefe barra;
    private AudioSource audioSrc;
    private Light2D luzGlobal;
    private Color luzOriginal;
    private float intensidadOriginal;
    private ParticleSystem brasas;
    private Coroutine fundido;

    // Reto opcional: vencer sin beber en el combate, y vencer al primer intento
    // (sin morir ni volver a la hoguera entre intentos).
    private int intentos;
    private bool pocionUsada;

    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
        audioSrc = gameObject.AddComponent<AudioSource>();
        audioSrc.loop = true;
        audioSrc.playOnAwake = false;
        audioSrc.volume = 0f;

        foreach (Light2D l in FindObjectsByType<Light2D>(FindObjectsSortMode.None))
            if (l.lightType == Light2D.LightType.Global) { luzGlobal = l; break; }
        if (luzGlobal != null) { luzOriginal = luzGlobal.color; intensidadOriginal = luzGlobal.intensity; }

        PonerNiebla(false);
        if (salida != null) salida.SetActive(false);
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
    }

    // Cambio de volumen desde el menu de pausa con la musica sonando.
    private void AjustarVolumen()
    {
        if (estado == Estado.Combate && fundido == null) audioSrc.volume = volumen * ControlVolumen.Musica;
    }

    private void MarcarPocion()
    {
        if (estado == Estado.Combate) pocionUsada = true;
    }

    private void OnTriggerEnter2D(Collider2D otro)
    {
        if (estado != Estado.Esperando || !otro.CompareTag("Player") || prefabJefe == null) return;
        Empezar();
    }

    private void Empezar()
    {
        estado = Estado.Combate;
        EnCombate = true;
        PonerNiebla(true);
        intentos++;
        pocionUsada = false;
        AlEmpezarCombate?.Invoke();

        if (musica != null)
        {
            audioSrc.clip = musica;
            audioSrc.Play();
            Fundir(volumen * ControlVolumen.Musica, 2f);
        }

        barra = BarraJefe.Crear(nombre);
        jefe = Instantiate(prefabJefe, new Vector3(zona.center.x, zona.yMax, 0f), Quaternion.identity);
        jefe.Configurar(zona, suelo);
        EnemyHealth salud = jefe.GetComponent<EnemyHealth>();
        salud.AlCambiarVida += barra.Actualizar;
        jefe.AlAterrizar += () =>
        {
            MensajePantalla.Titulo(nombre.ToUpper(), titulo);
            if (barra != null) barra.Mostrar(true);
        };
        jefe.AlCambiarFase += Fase2;
        jefe.AlDerrotado += Victoria;
    }

    private void Fase2()
    {
        MensajePantalla.Narrativo("El Espectro Carmesí revela su verdadera forma...", 3.5f);
        StartCoroutine(TenirLuz(luzFase2, intensidadOriginal * 0.9f, 2f));
        CrearBrasas();
    }

    private void Victoria()
    {
        estado = Estado.Vencido;
        EnCombate = false;
        Fundir(0f, 3f);
        if (barra != null) barra.Mostrar(false);
        StartCoroutine(TrasVictoria());
    }

    private IEnumerator TrasVictoria()
    {
        yield return new WaitForSeconds(1.5f);
        MensajePantalla.Banner("ENEMIGO CAÍDO", new Color(1f, 0.85f, 0.45f), 4f);
        PonerNiebla(false, true);
        if (salida != null) salida.SetActive(true);
        StartCoroutine(TenirLuz(luzOriginal, intensidadOriginal, 3f));
        if (brasas != null) brasas.Stop();
        AlVencer?.Invoke();

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

    // El player ha muerto: se deshace el intento.
    private void Reiniciar()
    {
        if (estado != Estado.Combate) return;
        estado = Estado.Esperando;
        EnCombate = false;
        PeligrosJefe.LimpiarTodo();
        if (jefe != null) Destroy(jefe.gameObject);
        if (barra != null) Destroy(barra.gameObject);
        Fundir(0f, 1f);
        PonerNiebla(false);
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
        fundido = StartCoroutine(FundirMusica(destino, segundos));
    }

    private IEnumerator FundirMusica(float destino, float segundos)
    {
        float inicio = audioSrc.volume;
        for (float t = 0f; t < segundos; t += Time.unscaledDeltaTime)
        {
            audioSrc.volume = Mathf.Lerp(inicio, destino, t / segundos);
            yield return null;
        }
        audioSrc.volume = destino;
        if (destino <= 0f) audioSrc.Stop();
        fundido = null;
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

    // Brasas rojas que suben por toda la arena en la fase 2.
    private void CrearBrasas()
    {
        GameObject go = new GameObject("BrasasArena");
        go.transform.position = new Vector3(zona.center.x, zona.yMin, 0f);
        brasas = go.AddComponent<ParticleSystem>();
        brasas.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = brasas.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.14f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.3f, 0.2f, 0.9f), new Color(1f, 0.7f, 0.3f, 0.7f));
        main.gravityModifier = -0.05f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 300;
        var em = brasas.emission;
        em.rateOverTime = 35f;
        var sh = brasas.shape;
        sh.shapeType = ParticleSystemShapeType.Box;
        sh.scale = new Vector3(zona.width, 0.5f, 1f);
        sh.rotation = new Vector3(-90f, 0f, 0f);
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
