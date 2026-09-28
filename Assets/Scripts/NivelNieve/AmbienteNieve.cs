using UnityEngine;
using UnityEngine.UI;

// Ambiente del nivel de nieve, todo de adorno (no toca la jugabilidad):
//   - Vineta: los bordes de la pantalla un poco oscuros y frios.
//   - Aurora: un brillo lento en el cielo, entre el cielo y las montanas.
//   - Viento: la nieve cambia de fuerza y direccion a rafagas suaves; con el
//     jefe sopla mas fuerte (y mas en su fase 2) y al acabar vuelve poco a poco.
//   - Nieve en planos: copos pequenos y lentos al fondo, y unos pocos grandes y
//     rapidos delante. Los del medio son los de siempre (NieveCayendo).
// Cada cosa se enciende, se apaga y se ajusta aqui, tambien jugando.
public class AmbienteNieve : MonoBehaviour
{
    [Header("Vineta (bordes de la pantalla)")]
    public bool vineta = true;
    [Tooltip("Lo oscuras que quedan las esquinas (0 = nada).")]
    [Range(0f, 1f)] public float intensidadVineta = 0.3f;
    [Tooltip("Donde empieza a oscurecer: 0 = desde el centro, 1 = solo en las esquinas.")]
    [Range(0f, 0.95f)] public float tamanoVineta = 0.55f;
    public Color colorVineta = new Color(0.03f, 0.07f, 0.16f);

    [Header("Aurora (cielo)")]
    public bool aurora = true;
    public Color colorAurora = new Color(0.45f, 0.95f, 0.9f);
    [Tooltip("Lo que se nota. Si distrae del combate, bajalo.")]
    [Range(0f, 1f)] public float intensidadAurora = 0.16f;
    [Tooltip("Rapidez con la que ondula y cambia de brillo.")]
    [Range(0f, 1f)] public float velocidadAurora = 0.15f;
    [Tooltip("Altura sobre el centro de la pantalla (unidades).")]
    public float alturaAurora = 2.1f;
    [Tooltip("Pixeles por unidad de la aurora (igual que el fondo: 28).")]
    public float pixelesAurora = 28f;

    [Header("Viento en la nieve (solo visual)")]
    public bool viento = true;
    [Tooltip("Fuerza normal del viento.")]
    public float fuerzaBase = 0.6f;
    [Tooltip("Fuerza extra de las rafagas.")]
    public float fuerzaRafagas = 0.9f;
    [Tooltip("Rafagas por segundo, mas o menos (0.1 = una cada 10 s).")]
    [Range(0.01f, 1f)] public float frecuenciaRafagas = 0.12f;
    [Tooltip("Fuerza durante la pelea con el jefe.")]
    public float fuerzaJefe = 2f;
    [Tooltip("Fuerza en la fase 2 del jefe.")]
    public float fuerzaJefeFase2 = 3.2f;
    [Tooltip("Segundos que tarda en pasar de la calma al viento del jefe (y al reves).")]
    public float transicion = 3f;

    [Header("Nieve en varios planos")]
    public bool nieveLejana = true;
    [Tooltip("Copos por segundo al fondo (pequenos y lentos).")]
    public float cantidadLejana = 40f;
    public bool nieveCercana = true;
    [Tooltip("Copos por segundo delante (grandes y rapidos). Pocos, para no tapar.")]
    public float cantidadCercana = 5f;
    [Range(0f, 1f)] public float opacidadCercana = 0.4f;

    // El viento que sopla ahora mismo (lo pueden mirar otros adornos).
    public static float VientoActual { get; private set; }

    private Camera cam;
    private Image imagenVineta;
    private Texture2D texVineta;
    private float tamanoHecho = -1f;
    private SpriteRenderer auroraA, auroraB;
    private float anchoAurora;
    private ParticleSystem psLejana, psMedia, psCercana;
    private float fuerzaActual;

    private void Start()
    {
        cam = Camera.main;
        if (cam == null) { enabled = false; return; }
        fuerzaActual = fuerzaBase;
        CrearVineta();
        CrearAurora();
        Transform media = cam.transform.Find("NieveCayendo");
        if (media != null) psMedia = media.GetComponent<ParticleSystem>();
        Material mat = psMedia != null ? psMedia.GetComponent<ParticleSystemRenderer>().sharedMaterial : null;
        psLejana = CrearNieve("NieveLejana", mat, "Background", 19, new Vector2(0.025f, 0.05f), new Vector2(0.25f, 0.5f),
                              new Vector2(10f, 14f), new Color(0.85f, 0.92f, 1f, 0.55f), new Color(0.75f, 0.85f, 1f, 0.35f));
        psCercana = CrearNieve("NieveCercana", mat, "VFX", 40, new Vector2(0.07f, 0.12f), new Vector2(2.2f, 3.2f),
                               new Vector2(3f, 4.5f), Color.white, new Color(0.9f, 0.95f, 1f, 0.7f));
    }

    private void OnDestroy()
    {
        VientoActual = 0f;
        if (texVineta != null) Destroy(texVineta);
        if (imagenVineta != null) Destroy(imagenVineta.canvas.gameObject);
    }

    private void Update()
    {
        ActualizarVineta();
        ActualizarAurora();
        ActualizarViento();
    }

    // ------------------------------------------------------------------ Vineta

    private void CrearVineta()
    {
        // Lienzo propio por debajo del HUD (que va en -1) y de la neblina de la
        // ventisca (-5): nunca tapa la interfaz.
        GameObject lienzo = new GameObject("VinetaNieve");
        Canvas c = lienzo.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = -8;
        imagenVineta = new GameObject("Vineta").AddComponent<Image>();
        imagenVineta.transform.SetParent(lienzo.transform, false);
        imagenVineta.raycastTarget = false;
        RectTransform r = imagenVineta.rectTransform;
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
    }

    // La textura se estira a toda la pantalla: vale para cualquier resolucion.
    private void HacerTexturaVineta()
    {
        const int n = 128;
        if (texVineta == null)
        {
            texVineta = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            imagenVineta.sprite = Sprite.Create(texVineta, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
        }
        Color32[] px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                // 1 en las esquinas, 0 en el centro.
                float d = Mathf.Sqrt(dx * dx + dy * dy) / 1.4142f;
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(tamanoVineta, 1f, d));
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        texVineta.SetPixels32(px);
        texVineta.Apply();
        tamanoHecho = tamanoVineta;
    }

    private void ActualizarVineta()
    {
        if (imagenVineta == null) return;
        imagenVineta.enabled = vineta && intensidadVineta > 0f;
        if (!imagenVineta.enabled) return;
        if (!Mathf.Approximately(tamanoHecho, tamanoVineta)) HacerTexturaVineta();
        imagenVineta.color = new Color(colorVineta.r, colorVineta.g, colorVineta.b, intensidadVineta);
    }

    // ------------------------------------------------------------------ Aurora

    // Una franja de luz hecha a pixeles (con tramado, sin degradados borrosos) que
    // se repite a lo ancho. Dos copias se cruzan despacio y cambian de brillo:
    // eso hace la ondulacion.
    private void CrearAurora()
    {
        const int w = 256, h = 56;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point };
        int[] bayer = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
        Color32[] px = new Color32[w * h];
        float k = 2f * Mathf.PI / w;
        for (int x = 0; x < w; x++)
        {
            // Frecuencias enteras: el borde izquierdo encaja con el derecho.
            float centro = h * 0.5f + Mathf.Sin(x * k * 2f) * 8f + Mathf.Sin(x * k * 5f + 1f) * 4f;
            float grosor = 7f + Mathf.Sin(x * k * 3f + 2f) * 3f;
            float cortinas = 0.55f + 0.45f * Mathf.Pow(Mathf.Sin(x * k * 11f), 2f);
            for (int y = 0; y < h; y++)
            {
                float u = (y - centro) / grosor;
                // Por arriba se deshilacha mas que por abajo, como las auroras.
                float a = Mathf.Exp(-u * u * (u < 0f ? 2.2f : 0.9f)) * cortinas;
                // Cuatro niveles con tramado ordenado: pixel art, no degradado.
                float umbral = (bayer[(y % 4) * 4 + x % 4] + 0.5f) / 16f;
                float nivel = Mathf.Floor(a * 4f + umbral) / 4f;
                px[y * w + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(nivel) * 255f));
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        Sprite s = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), pixelesAurora, 0, SpriteMeshType.FullRect);
        anchoAurora = w / pixelesAurora;
        auroraA = CapaAurora("AuroraA", s, false);
        auroraB = CapaAurora("AuroraB", s, true);
    }

    private SpriteRenderer CapaAurora(string nombre, Sprite s, bool volteada)
    {
        SpriteRenderer sr = new GameObject(nombre).AddComponent<SpriteRenderer>();
        sr.transform.SetParent(transform, false);
        sr.sprite = s;
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.size = new Vector2(anchoAurora * 8f, s.bounds.size.y);
        sr.flipX = volteada;
        // Entre el cielo (0) y las montanas lejanas (2).
        sr.sortingLayerName = "Background";
        sr.sortingOrder = 1;
        return sr;
    }

    private void ActualizarAurora()
    {
        if (auroraA == null) return;
        auroraA.enabled = auroraB.enabled = aurora && intensidadAurora > 0f;
        if (!auroraA.enabled) return;
        float t = Time.time * velocidadAurora;
        Vector3 c = cam.transform.position;
        // Casi pegada a la camara, como el cielo; se mueve sola muy despacio.
        float baseX = c.x - c.x * 0.02f;
        Poner(auroraA, baseX + t * 1.3f, c.y + alturaAurora, 0.55f + 0.45f * Mathf.Sin(t * 2.1f));
        Poner(auroraB, baseX - t * 0.8f, c.y + alturaAurora + 0.35f, 0.5f + 0.5f * Mathf.Sin(t * 1.4f + 2f));
    }

    private void Poner(SpriteRenderer sr, float x, float y, float brillo)
    {
        // Al avanzar una copia entera se vuelve atras: nunca se acaba.
        float cx = cam.transform.position.x;
        x = cx + Mathf.Repeat(x - cx + anchoAurora * 0.5f, anchoAurora) - anchoAurora * 0.5f;
        sr.transform.position = new Vector3(x, y, transform.position.z);
        sr.color = new Color(colorAurora.r, colorAurora.g, colorAurora.b, intensidadAurora * brillo);
    }

    // ------------------------------------------------------------------ Nieve y viento

    private ParticleSystem CrearNieve(string nombre, Material mat, string capa, int orden, Vector2 tamano, Vector2 velocidad,
                                      Vector2 vida, Color colorA, Color colorB)
    {
        GameObject go = new GameObject(nombre);
        go.transform.SetParent(cam.transform, false);
        go.transform.localPosition = new Vector3(0f, 8f, 10f);
        go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(vida.x, vida.y);
        main.startSpeed = new ParticleSystem.MinMaxCurve(velocidad.x, velocidad.y);
        main.startSize = new ParticleSystem.MinMaxCurve(tamano.x, tamano.y);
        main.startColor = new ParticleSystem.MinMaxGradient(colorA, colorB);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 600;
        main.prewarm = true;
        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Box;
        sh.scale = new Vector3(34f, 2f, 1f);
        var ruido = ps.noise;
        ruido.enabled = true;
        ruido.strength = 0.3f;
        ruido.frequency = 0.25f;
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(-0.3f, -0.1f);
        vel.y = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        ParticleSystemRenderer pr = go.GetComponent<ParticleSystemRenderer>();
        if (mat != null) pr.sharedMaterial = mat;
        else
        {
            Shader s = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            pr.sharedMaterial = new Material(s != null ? s : Shader.Find("Sprites/Default"));
        }
        pr.sortingLayerName = capa;
        pr.sortingOrder = orden;
        ps.Play();
        return ps;
    }

    private void ActualizarViento()
    {
        // Con el jefe el viento sube; al acabar, vuelve a la calma poco a poco.
        float objetivo = ArenaJefe.EnFase2 ? fuerzaJefeFase2 : ArenaJefe.EnCombate ? fuerzaJefe : fuerzaBase;
        float paso = Mathf.Max(0.01f, Mathf.Abs(fuerzaJefeFase2 - fuerzaBase)) / Mathf.Max(0.1f, transicion);
        fuerzaActual = Mathf.MoveTowards(fuerzaActual, objetivo, paso * Time.deltaTime);

        // Rafagas: ruido suave en el tiempo. Con calma el viento puede incluso
        // girar un poco; las rafagas siempre soplan hacia la izquierda.
        float t = Time.time * frecuenciaRafagas;
        float rafaga = Mathf.Pow(Mathf.Max(0f, Mathf.PerlinNoise(t, 0.3f) * 2f - 0.8f), 2f) * fuerzaRafagas;
        float vaiven = (Mathf.PerlinNoise(t * 0.6f, 7.1f) - 0.5f) * 0.8f * fuerzaBase;
        float v = viento ? fuerzaActual * (0.75f + 0.5f * Mathf.PerlinNoise(t * 1.7f, 3.3f)) + rafaga + vaiven : 0.25f;
        VientoActual = v;

        Soplar(psLejana, v * 0.4f, nieveLejana, cantidadLejana, 1f);
        Soplar(psMedia, v, true, -1f, 1f);
        Soplar(psCercana, v * 1.8f, nieveCercana, cantidadCercana, opacidadCercana);
    }

    // El viento empuja hacia la izquierda: cada plano con su fuerza (lo lejano se
    // mueve menos en pantalla, lo cercano mas).
    private void Soplar(ParticleSystem ps, float fuerza, bool activo, float cantidad, float opacidad)
    {
        if (ps == null) return;
        if (activo != ps.gameObject.activeSelf) ps.gameObject.SetActive(activo);
        if (!activo) return;
        var vel = ps.velocityOverLifetime;
        vel.x = new ParticleSystem.MinMaxCurve(-fuerza * 1.15f, -fuerza * 0.75f);
        if (cantidad >= 0f)
        {
            var em = ps.emission;
            em.rateOverTime = cantidad;
            // La transparencia de los copos de delante va por el color de inicio.
            var main = ps.main;
            Color a = main.startColor.colorMin, b = main.startColor.colorMax;
            if (!Mathf.Approximately(b.a, opacidad * 0.7f) && opacidad < 1f)
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(a.r, a.g, a.b, opacidad), new Color(b.r, b.g, b.b, opacidad * 0.7f));
        }
    }
}
