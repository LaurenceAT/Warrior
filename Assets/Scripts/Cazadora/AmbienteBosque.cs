using UnityEngine;
using UnityEngine.Rendering.Universal;

// Ambiente del bosque sin hojas (la arena de la Cazadora): niebla baja que se
// mueve despacio, hojas secas cayendo, rafagas de viento (solo visuales) y la
// luz fria y verdosa. Cambia con las fases (AjustesCazadora): mas niebla y el
// cielo mas oscuro en la fase 2; en la 3, casi negro (y la oscuridad).
public class AmbienteBosque : MonoBehaviour
{
    [Header("Niebla baja")]
    [Tooltip("Las capas de niebla (las pone la Ronda10).")]
    public SpriteRenderer[] nieblas = new SpriteRenderer[0];
    public Color colorNiebla = new Color(0.62f, 0.7f, 0.64f, 1f);
    [Tooltip("Opacidad maxima de cada capa con niebla = 1.")]
    [Range(0f, 1f)] public float opacidadNiebla = 0.55f;
    [Tooltip("Rapidez a la que se desplaza la niebla.")]
    public float velocidadNiebla = 0.25f;
    [Tooltip("Cuanto se mece arriba y abajo.")]
    public float vaivenNiebla = 0.12f;

    [Header("Cielo")]
    [Tooltip("Velo oscuro por encima del fondo (la fase lo oscurece).")]
    public SpriteRenderer oscurecedor;
    public Color colorCieloOscuro = new Color(0.02f, 0.03f, 0.03f, 1f);

    [Header("Hojas secas")]
    public bool hojas = true;
    [Tooltip("Hojas por segundo.")]
    public float cantidadHojas = 7f;
    public Color hojaA = new Color(0.45f, 0.38f, 0.22f, 0.9f);
    public Color hojaB = new Color(0.33f, 0.36f, 0.2f, 0.9f);
    [Tooltip("Lo que tarda en caer una hoja (segundos).")]
    public float vidaHoja = 9f;

    [Header("Viento visual")]
    public float vientoBase = 0.35f;
    public float rafagas = 0.9f;
    [Range(0.01f, 1f)] public float frecuenciaRafagas = 0.15f;

    [Header("Cambio de fase")]
    [Tooltip("Segundos que tarda en cambiar la niebla, el cielo y la luz.")]
    public float transicion = 3f;

    [Header("Zona (la arena)")]
    public Rect zona = new Rect(0f, 0f, 44f, 14f);

    private float niebla, nieblaObjetivo = 0.3f, cielo, cieloObjetivo;
    private Color luz, luzObjetivo;
    private float intensidad, intensidadObjetivo;
    private Light2D global;
    private Color luzInicial;
    private float intensidadInicial;
    private ParticleSystem ps;
    private Vector3[] basesNiebla;
    private float semilla;

    public static float Viento { get; private set; }

    private void Awake()
    {
        foreach (Light2D l in FindObjectsByType<Light2D>(FindObjectsSortMode.None))
            if (l.lightType == Light2D.LightType.Global) { global = l; break; }
        if (global != null) { luzInicial = luz = luzObjetivo = global.color; intensidadInicial = intensidad = intensidadObjetivo = global.intensity; }
        basesNiebla = new Vector3[nieblas.Length];
        for (int i = 0; i < nieblas.Length; i++) if (nieblas[i] != null) basesNiebla[i] = nieblas[i].transform.localPosition;
        semilla = Random.value * 100f;
        if (hojas) CrearHojas();
    }

    // La fase manda: niebla, cielo y luz.
    public void Fase(AjustesCazadora.Fase f, bool alInstante = false)
    {
        if (f == null) return;
        nieblaObjetivo = f.niebla;
        cieloObjetivo = f.cieloOscuro;
        luzObjetivo = f.luz;
        intensidadObjetivo = f.intensidadLuz;
        if (alInstante) { niebla = nieblaObjetivo; cielo = cieloObjetivo; luz = luzObjetivo; intensidad = intensidadObjetivo; }
    }

    // Vuelve como estaba (el player murio: la pelea se reinicia).
    public void Reiniciar(AjustesCazadora.Fase primera)
    {
        if (primera != null) Fase(primera, true);
        else { luz = luzObjetivo = luzInicial; intensidad = intensidadObjetivo = intensidadInicial; }
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        float k = transicion > 0f ? dt / transicion : 1f;
        niebla = Mathf.MoveTowards(niebla, nieblaObjetivo, k);
        cielo = Mathf.MoveTowards(cielo, cieloObjetivo, k);
        luz = Color.Lerp(luz, luzObjetivo, k * 2f);
        intensidad = Mathf.MoveTowards(intensidad, intensidadObjetivo, k);
        if (global != null) { global.color = luz; global.intensity = intensidad; }

        float t = Time.time;
        Viento = vientoBase + rafagas * Mathf.Max(0f, Mathf.PerlinNoise(semilla, t * frecuenciaRafagas) - 0.45f) * 2f;

        for (int i = 0; i < nieblas.Length; i++)
        {
            SpriteRenderer s = nieblas[i];
            if (s == null) continue;
            float vel = velocidadNiebla * (1f + i * 0.35f) * (0.6f + Viento);
            float ancho = s.sprite != null ? s.bounds.size.x / 3f : 20f;
            float x = Mathf.Repeat(t * vel + i * 7f, Mathf.Max(1f, ancho));
            s.transform.localPosition = basesNiebla[i] + new Vector3(x - ancho * 0.5f, Mathf.Sin(t * 0.3f + i) * vaivenNiebla, 0f);
            Color c = colorNiebla;
            c.a = opacidadNiebla * niebla * (i == 0 ? 1f : 0.7f);
            s.color = c;
        }
        if (oscurecedor != null)
        {
            Color c = colorCieloOscuro;
            c.a = cielo;
            oscurecedor.color = c;
            oscurecedor.enabled = cielo > 0.01f;
        }
        if (ps != null)
        {
            var vol = ps.velocityOverLifetime;
            vol.x = new ParticleSystem.MinMaxCurve(Viento * 0.8f, Viento * 1.6f);
        }
    }

    private void CrearHojas()
    {
        GameObject go = new GameObject("HojasSecas");
        go.transform.SetParent(transform, false);
        go.transform.position = new Vector3(zona.center.x, zona.yMax + 1f, 0f);
        ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(vidaHoja * 0.7f, vidaHoja);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.4f);
        main.startSize3D = true;
        main.startSizeX = new ParticleSystem.MinMaxCurve(0.08f, 0.14f);
        main.startSizeY = new ParticleSystem.MinMaxCurve(0.04f, 0.07f);
        main.startSizeZ = 1f;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(hojaA, hojaB);
        main.gravityModifier = 0.02f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 200;
        var em = ps.emission;
        em.rateOverTime = cantidadHojas;
        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Box;
        sh.scale = new Vector3(zona.width + 10f, 0.5f, 1f);
        var vol = ps.velocityOverLifetime;
        vol.enabled = true;
        vol.space = ParticleSystemSimulationSpace.World;
        vol.x = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
        vol.y = new ParticleSystem.MinMaxCurve(-0.5f, -0.25f);
        vol.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-2.5f, 2.5f);
        var ruido = ps.noise;
        ruido.enabled = true;
        ruido.strength = 0.35f;
        ruido.frequency = 0.4f;
        ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = EfectoVisual.MaterialSinLuz();
        r.sortingLayerName = "VFX";
        r.sortingOrder = -30;
        ps.Play();
        // Que ya haya hojas en el aire desde el principio.
        ps.Simulate(vidaHoja * 0.8f, true, false);
        ps.Play();
    }
}
