using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Muro de niebla de la entrada a un jefe (como en Dark Souls / Elden Ring).
// Se dibuja por codigo en pixel art (pixeles del mismo tamano que el personaje):
//   - Tres capas de niebla que suben a distintas velocidades y ondulan, con zonas
//     densas y claras, y un borde luminoso que se mueve.
//   - Motas que flotan y se desprenden, un brillo que pulsa y una luz suave.
//   - Formacion (~1 s): la niebla llega de los lados y del suelo y se condensa,
//     con una rafaga y un pequeno temblor. Disolucion: se deshace en motas.
//   - Bloquea de verdad (collider en la capa Ground). La ZonaJefe, ademas,
//     nunca deja al player al otro lado (ni con un empujon).
// Abierto no se ve nada. Lo maneja la ZonaJefe (su padre).
[DisallowMultipleComponent]
public class MuroNiebla : MonoBehaviour
{
    public enum EstadoMuro { Abierto, Formando, Cerrado, Disolviendo }

    [Header("Tamano (unidades)")]
    [Tooltip("Alto del muro, desde el suelo (este objeto esta a ras de suelo).")]
    public float alto = 16f;
    [Tooltip("Ancho de la niebla que se ve.")]
    public float anchoVisual = 3f;
    [Tooltip("Ancho de la pared que bloquea el paso.")]
    public float anchoBloqueo = 0.8f;
    [Tooltip("Pixeles por unidad del dibujo (28 = el mismo tamano de pixel que el personaje).")]
    public int pixelesPorUnidad = 28;

    [Header("Color")]
    [Tooltip("Niebla clara (zonas finas).")]
    public Color colorNiebla = new Color(0.72f, 0.8f, 0.92f, 1f);
    [Tooltip("Niebla densa (el centro del muro).")]
    public Color colorDenso = new Color(0.86f, 0.91f, 1f, 1f);
    [Tooltip("Borde luminoso, hebras brillantes, motas y luz.")]
    public Color colorBorde = new Color(0.94f, 0.98f, 1f, 1f);
    [Tooltip("Opacidad general (1 = la mas espesa).")]
    [Range(0.1f, 1f)] public float intensidad = 0.9f;

    [Header("Movimiento de las capas")]
    [Tooltip("Multiplica la velocidad de todas las capas.")]
    public float velocidadCapas = 1f;
    [Tooltip("Velocidad de subida (unidades/s) de cada capa: fondo, media y hebras.")]
    public Vector3 velocidades = new Vector3(0.45f, 0.9f, 1.6f);
    [Tooltip("Cuanto ondula de lado a lado (unidades).")]
    public float ondulacion = 0.32f;
    [Tooltip("Veces por segundo que se redibuja (pocas = mas pixel art).")]
    public float fotogramasPorSegundo = 12f;

    [Header("Brillo")]
    public float pulsosPorSegundo = 0.45f;
    [Range(0f, 1f)] public float fuerzaPulso = 0.3f;
    [Tooltip("Radio de la luz que da el muro (0 = sin luz).")]
    public float radioLuz = 3.5f;
    public float intensidadLuz = 0.45f;

    [Header("Motas")]
    public float motasPorSegundo = 16f;

    [Header("Formacion y disolucion")]
    public float tiempoFormacion = 1f;
    public float tiempoDisolucion = 1.4f;
    [Tooltip("Temblor de camara al cerrarse (0 = nada).")]
    public float temblor = 0.25f;

    [Header("Sonido")]
    [Tooltip("Rafaga al formarse.")]
    public AudioClip sonidoFormar;
    [Range(0f, 1f)] public float volumenFormar = 0.75f;
    [Tooltip("Sonido continuo mientras existe (viento o susurro grave).")]
    public AudioClip sonidoContinuo;
    [Range(0f, 1f)] public float volumenContinuo = 0.28f;
    [Tooltip("Tono del continuo (menos de 1 = mas grave).")]
    public float tonoContinuo = 0.7f;
    [Tooltip("Al disolverse (vacio = sin sonido).")]
    public AudioClip sonidoDisolver;
    [Range(0f, 1f)] public float volumenDisolver = 0.5f;
    [Tooltip("A partir de esta distancia de la camara (unidades) ya no se oye.")]
    public float distanciaSonido = 18f;

    public EstadoMuro Estado { get; private set; } = EstadoMuro.Abierto;
    public bool Bloquea => bloqueo != null && bloqueo.enabled;
    public BoxCollider2D Bloqueo => bloqueo;

    private BoxCollider2D bloqueo;
    private SpriteRenderer dibujo;
    private Texture2D textura;
    private Color32[] pixeles;
    private int ancho, altoPx;
    private ParticleSystem motas;
    private Light2D luz;
    private FuenteLuz fuenteLuz;
    private AudioSource fuenteUna, fuenteBucle;
    private float formacion, disolucion = 1f, reloj, siguienteDibujo, acumMotas;
    private Coroutine animacion;

    // Ruido que se repite (se calcula una vez para todos los muros).
    private const int R1W = 64, R1H = 64, R2W = 48, R2H = 96, R3W = 32, R3H = 128;
    private static float[] ruido1, ruido2, ruido3;

    private void Awake()
    {
        bloqueo = GetComponent<BoxCollider2D>();
        if (bloqueo == null) bloqueo = gameObject.AddComponent<BoxCollider2D>();
        bloqueo.isTrigger = false;
        AjustarBloqueo();
        bloqueo.enabled = false;
        PrepararRuido();
        CrearDibujo();
        CrearMotas();
        CrearLuz();
        fuenteUna = NuevaFuente(false);
        fuenteBucle = NuevaFuente(true);
    }

    private void OnValidate()
    {
        alto = Mathf.Max(1f, alto);
        anchoVisual = Mathf.Max(0.5f, anchoVisual);
        anchoBloqueo = Mathf.Max(0.2f, anchoBloqueo);
        pixelesPorUnidad = Mathf.Clamp(pixelesPorUnidad, 4, 64);
        BoxCollider2D b = GetComponent<BoxCollider2D>();
        if (b != null) { bloqueo = b; AjustarBloqueo(); }
    }

    private void AjustarBloqueo()
    {
        bloqueo.size = new Vector2(anchoBloqueo, alto);
        bloqueo.offset = new Vector2(0f, alto * 0.5f);
    }

    private void OnDestroy()
    {
        if (textura != null) Destroy(textura);
    }

    // ------------------------------------------------------------------ Abrir y cerrar

    // Se cierra: bloquea desde este mismo momento y la niebla se forma.
    public void Cerrar()
    {
        if (Estado == EstadoMuro.Cerrado || Estado == EstadoMuro.Formando) return;
        bloqueo.enabled = true;
        Physics2D.SyncTransforms();
        if (animacion != null) StopCoroutine(animacion);
        animacion = StartCoroutine(Formar());
    }

    // Se abre: deja pasar al momento. Suave: se deshace en motas; si no, desaparece.
    public void Abrir(bool suave)
    {
        bloqueo.enabled = false;
        if (Estado == EstadoMuro.Abierto) return;
        if (animacion != null) StopCoroutine(animacion);
        animacion = null;
        if (suave) animacion = StartCoroutine(Disolver());
        else Quitar();
    }

    private IEnumerator Formar()
    {
        Estado = EstadoMuro.Formando;
        disolucion = 0f;
        formacion = 0f;
        Pintar();
        dibujo.enabled = true;
        Sonar(fuenteUna, sonidoFormar, volumenFormar);
        fuenteBucle.clip = sonidoContinuo;
        fuenteBucle.pitch = tonoContinuo;
        if (sonidoContinuo != null) { fuenteBucle.volume = 0f; fuenteBucle.Play(); }
        if (fuenteLuz != null) fuenteLuz.encendida = true;
        LlegadaDeNiebla();
        float dur = Mathf.Max(0.05f, tiempoFormacion);
        bool temblo = false;
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            formacion = t / dur;
            if (!temblo && formacion >= 0.55f) { temblo = true; if (temblor > 0f) CamaraDinamica.Sacudir(temblor); }
            yield return null;
        }
        if (!temblo && temblor > 0f) CamaraDinamica.Sacudir(temblor);
        formacion = 1f;
        Estado = EstadoMuro.Cerrado;
        animacion = null;
    }

    private IEnumerator Disolver()
    {
        Estado = EstadoMuro.Disolviendo;
        Sonar(fuenteUna, sonidoDisolver, volumenDisolver);
        float dur = Mathf.Max(0.05f, tiempoDisolucion);
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            float antes = disolucion;
            disolucion = t / dur;
            // Se deshace en motas que suben y se apagan.
            int n = Mathf.RoundToInt((disolucion - antes) * 160f * alto / 16f);
            for (int i = 0; i < n; i++) Mota(Random.Range(-anchoVisual * 0.35f, anchoVisual * 0.35f), Random.Range(0.2f, alto * 0.85f),
                new Vector2(Random.Range(-0.6f, 0.6f), Random.Range(0.6f, 1.8f)), Random.Range(0.7f, 1.5f), true);
            yield return null;
        }
        Quitar();
        animacion = null;
    }

    private void Quitar()
    {
        Estado = EstadoMuro.Abierto;
        formacion = 0f;
        disolucion = 1f;
        dibujo.enabled = false;
        fuenteBucle.Stop();
        if (luz != null) luz.intensity = 0f;
        if (fuenteLuz != null) fuenteLuz.encendida = false;
    }

    // Niebla que llega de los lados y del suelo hacia el muro.
    private void LlegadaDeNiebla()
    {
        int n = Mathf.RoundToInt(70f * Mathf.Clamp(alto / 16f, 0.5f, 2f));
        float dur = Mathf.Max(0.2f, tiempoFormacion);
        for (int i = 0; i < n; i++)
        {
            Vector2 desde;
            if (i % 3 == 2) desde = new Vector2(Random.Range(-3f, 3f), Random.Range(-0.2f, 0.4f)); // del suelo
            else desde = new Vector2((i % 2 == 0 ? -1f : 1f) * Random.Range(anchoVisual * 0.6f + 1f, anchoVisual * 0.6f + 4f), Random.Range(0f, alto * 0.75f));
            Vector2 hasta = new Vector2(Random.Range(-anchoVisual * 0.2f, anchoVisual * 0.2f), Random.Range(0.3f, alto * 0.9f));
            float vida = dur * Random.Range(0.55f, 1f);
            Mota(desde.x, desde.y, (hasta - desde) / vida, vida, false);
        }
    }

    // ------------------------------------------------------------------ Cada fotograma

    private void Update()
    {
        if (Estado == EstadoMuro.Abierto) return;
        reloj += Time.deltaTime * velocidadCapas;
        float pulso = 1f + fuerzaPulso * Mathf.Sin(Time.time * pulsosPorSegundo * 2f * Mathf.PI);
        float visible = Visible();

        if (luz != null) luz.intensity = intensidadLuz * visible * pulso;
        if (fuenteLuz != null) fuenteLuz.intensidad = 0.6f * visible;
        float vol = volumenContinuo * VolumenPorDistancia() * ControlVolumen.Efectos * visible;
        fuenteBucle.volume = Mathf.MoveTowards(fuenteBucle.volume, vol, Time.deltaTime * 0.8f);

        // Motas que flotan y se desprenden del muro.
        if (Estado == EstadoMuro.Cerrado || Estado == EstadoMuro.Formando)
        {
            acumMotas += Time.deltaTime * motasPorSegundo * (alto / 16f) * visible;
            while (acumMotas >= 1f)
            {
                acumMotas -= 1f;
                float lado = Random.value < 0.5f ? -1f : 1f;
                Mota(lado * Random.Range(0f, anchoVisual * 0.45f), Random.Range(0.1f, alto * 0.9f),
                     new Vector2(lado * Random.Range(0.1f, 0.5f), Random.Range(0.15f, 0.6f)), Random.Range(1.4f, 3f), false);
            }
        }

        if (Time.time >= siguienteDibujo && dibujo.isVisible)
        {
            siguienteDibujo = Time.time + 1f / Mathf.Max(1f, fotogramasPorSegundo);
            Pintar(pulso);
        }
    }

    private float Visible() => Estado == EstadoMuro.Formando ? Mathf.SmoothStep(0f, 1f, formacion)
                              : Estado == EstadoMuro.Disolviendo ? 1f - disolucion : 1f;

    private float VolumenPorDistancia()
    {
        Camera c = Camera.main;
        if (c == null) return 1f;
        float d = Mathf.Abs(c.transform.position.x - transform.position.x);
        return Mathf.Clamp01(1f - (d - distanciaSonido * 0.4f) / (distanciaSonido * 0.6f));
    }

    // ------------------------------------------------------------------ Dibujo

    private void CrearDibujo()
    {
        ancho = Mathf.Max(8, Mathf.RoundToInt(anchoVisual * pixelesPorUnidad));
        altoPx = Mathf.Max(8, Mathf.RoundToInt(alto * pixelesPorUnidad));
        textura = new Texture2D(ancho, altoPx, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = "MuroNiebla",
        };
        pixeles = new Color32[ancho * altoPx];
        GameObject go = new GameObject("Dibujo");
        go.transform.SetParent(transform, false);
        dibujo = go.AddComponent<SpriteRenderer>();
        dibujo.sprite = Sprite.Create(textura, new Rect(0, 0, ancho, altoPx), new Vector2(0.5f, 0f), pixelesPorUnidad, 0, SpriteMeshType.FullRect);
        dibujo.sharedMaterial = EfectoVisual.MaterialSinLuz();
        dibujo.sortingLayerName = "VFX";
        dibujo.sortingOrder = 2;
        dibujo.enabled = false;
    }

    private void Pintar(float pulso = 1f)
    {
        if (pixeles == null) return;
        float ppu = pixelesPorUnidad;
        float f = Estado == EstadoMuro.Formando ? formacion : 1f;
        float g = Estado == EstadoMuro.Disolviendo ? disolucion : 0f;
        int o1 = Mathf.RoundToInt(reloj * velocidades.x * ppu);
        int o2 = Mathf.RoundToInt(reloj * velocidades.y * ppu);
        int o3 = Mathf.RoundToInt(reloj * velocidades.z * ppu);
        float ond = ondulacion * ppu;
        float mitad = ancho * 0.5f;
        float brillo = Mathf.Max(0.5f, pulso);
        Color32 claro = Tono(colorNiebla, 1f, 0.13f * intensidad);
        Color32 medio = Tono(colorNiebla, 1.05f, 0.22f * intensidad);
        Color32 denso = Tono(colorDenso, 1f, 0.32f * intensidad);
        Color32 nucleo = Tono(colorDenso, 1.08f, 0.44f * intensidad);
        Color32 borde = Tono(colorBorde, brillo, Mathf.Clamp01(0.55f * intensidad * brillo));
        Color32 bordeSuave = Tono(colorBorde, brillo, Mathf.Clamp01(0.28f * intensidad * brillo));
        Color32 hebra = Tono(colorBorde, brillo, Mathf.Clamp01(0.4f * intensidad * brillo));
        Color32 nada = new Color32(0, 0, 0, 0);

        for (int y = 0; y < altoPx; y++)
        {
            float yn = y / (float)(altoPx - 1);
            int s1 = Mathf.RoundToInt(Mathf.Sin(y * 0.045f + reloj * 1.3f) * ond);
            int s2 = Mathf.RoundToInt(Mathf.Sin(y * 0.08f - reloj * 2.1f) * ond * 0.6f);
            // Arriba se deshilacha; abajo, a ras de suelo, algo mas espesa.
            float alturaF = Mathf.Clamp01((1f - yn) / 0.14f) * (1f + 0.25f * Mathf.Pow(1f - yn, 6f));
            int fila = y * ancho;
            for (int x = 0; x < ancho; x++)
            {
                float n1 = Muestra(ruido1, R1W, R1H, x + s1, y - o1);
                float n2 = Muestra(ruido2, R2W, R2H, x + s2 + 17, y - o2);
                float n3 = Muestra(ruido3, R3W, R3H, x - s1 / 2 + 5, y - o3);
                float dx = Mathf.Abs(x + 0.5f - mitad - s1 * 0.5f) / mitad;
                float forma = 1f - dx / (0.5f + 0.5f * n1);
                if (forma <= 0f) { pixeles[fila + x] = nada; continue; }
                // Manchas grandes (densas y claras) que suben despacio.
                float grande = Muestra(ruido1, R1W, R1H, Mathf.FloorToInt((x + s2 + 40) * 0.5f), Mathf.FloorToInt((y - o1 * 0.5f) * 0.5f));
                float dens = Mathf.Pow(forma, 0.7f) * (0.22f + 0.55f * n2 + 0.3f * n3) * (0.5f + 0.7f * grande) * alturaF;
                // Formacion: primero los lados y el suelo, luego el centro y lo alto.
                if (f < 1f)
                {
                    float sube = f * 1.35f - yn * 0.3f - (1f - dx) * 0.2f - (1f - n2) * 0.2f;
                    if (sube <= 0f) { pixeles[fila + x] = nada; continue; }
                    dens *= Mathf.Clamp01(sube * 4f) * Mathf.Lerp(0.5f, 1f, f);
                }
                // Disolucion: se apaga a trozos (con el ruido), no de golpe.
                if (g > 0f)
                {
                    if (n3 * 0.6f + n2 * 0.4f < g * 1.15f) { pixeles[fila + x] = nada; continue; }
                    dens *= 1f - g * 0.5f;
                }
                Color32 c;
                // Borde luminoso a trozos (fluye con las hebras), no un contorno fijo.
                if (dens < 0.15f) c = nada;
                else if (forma < 0.12f) c = n2 > 0.6f ? borde : n2 > 0.38f ? bordeSuave : claro;
                else if (n3 > 0.86f && dens > 0.34f) c = hebra;
                else if (dens < 0.27f) c = claro;
                else if (dens < 0.42f) c = medio;
                else if (dens < 0.58f) c = denso;
                else c = nucleo;
                pixeles[fila + x] = c;
            }
        }
        textura.SetPixels32(pixeles);
        textura.Apply(false);
    }

    private static Color32 Tono(Color c, float brillo, float alfa) =>
        new Color(Mathf.Clamp01(c.r * brillo), Mathf.Clamp01(c.g * brillo), Mathf.Clamp01(c.b * brillo), Mathf.Clamp01(alfa));

    private static float Muestra(float[] r, int w, int h, int x, int y)
    {
        x %= w; if (x < 0) x += w;
        y %= h; if (y < 0) y += h;
        return r[y * w + x];
    }

    private static void PrepararRuido()
    {
        if (ruido1 != null) return;
        ruido1 = Ruido(R1W, R1H, 3, 3, 3, 11);
        ruido2 = Ruido(R2W, R2H, 3, 6, 3, 23);
        // Hebras: finas de lado y largas hacia arriba.
        ruido3 = Ruido(R3W, R3H, 8, 3, 2, 37);
    }

    // Ruido de valor que se repite sin costuras (w x h), con varias octavas, de 0 a 1.
    private static float[] Ruido(int w, int h, int celdasX, int celdasY, int octavas, int semilla)
    {
        System.Random rnd = new System.Random(semilla);
        float[] r = new float[w * h];
        float amp = 1f;
        for (int o = 0; o < octavas; o++)
        {
            int cx = celdasX << o, cy = celdasY << o;
            float[] red = new float[cx * cy];
            for (int i = 0; i < red.Length; i++) red[i] = (float)rnd.NextDouble();
            for (int y = 0; y < h; y++)
            {
                float fy = y / (float)h * cy;
                int iy = (int)fy;
                float ty = Mathf.SmoothStep(0f, 1f, fy - iy);
                int y0 = iy % cy, y1 = (iy + 1) % cy;
                for (int x = 0; x < w; x++)
                {
                    float fx = x / (float)w * cx;
                    int ix = (int)fx;
                    float tx = Mathf.SmoothStep(0f, 1f, fx - ix);
                    int x0 = ix % cx, x1 = (ix + 1) % cx;
                    float a = Mathf.Lerp(red[y0 * cx + x0], red[y0 * cx + x1], tx);
                    float b = Mathf.Lerp(red[y1 * cx + x0], red[y1 * cx + x1], tx);
                    r[y * w + x] += amp * Mathf.Lerp(a, b, ty);
                }
            }
            amp *= 0.5f;
        }
        float min = float.MaxValue, max = float.MinValue;
        foreach (float v in r) { if (v < min) min = v; if (v > max) max = v; }
        for (int i = 0; i < r.Length; i++) r[i] = (r[i] - min) / Mathf.Max(0.0001f, max - min);
        return r;
    }

    // ------------------------------------------------------------------ Motas, luz y sonido

    private void CrearMotas()
    {
        GameObject go = new GameObject("Motas");
        go.transform.SetParent(transform, false);
        motas = go.AddComponent<ParticleSystem>();
        motas.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = motas.main;
        main.loop = false;
        main.playOnAwake = false;
        main.startSpeed = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 600;
        var em = motas.emission;
        em.enabled = false;
        var sh = motas.shape;
        sh.enabled = false;
        var col = motas.colorOverLifetime;
        col.enabled = true;
        Gradient gr = new Gradient();
        gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                   new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0f, 1f) });
        col.color = gr;
        ParticleSystemRenderer pr = go.GetComponent<ParticleSystemRenderer>();
        pr.sharedMaterial = EfectoVisual.MaterialSinLuz();
        pr.sortingLayerName = "VFX";
        pr.sortingOrder = 3;
        motas.Play();
    }

    // Una mota: cuadradito de 1-2 pixeles.
    private void Mota(float x, float y, Vector2 velocidad, float vida, bool brillante)
    {
        if (motas == null) return;
        float px = 1f / pixelesPorUnidad;
        Color c = brillante || Random.value < 0.5f ? colorBorde : colorDenso;
        c.a = brillante ? 0.95f : 0.8f;
        var p = new ParticleSystem.EmitParams
        {
            position = transform.position + new Vector3(x, y, 0f),
            velocity = velocidad,
            startLifetime = vida,
            startSize = px * (Random.value < 0.3f ? 3f : 2f),
            startColor = c,
            applyShapeToPosition = false,
        };
        motas.Emit(p, 1);
    }

    private void CrearLuz()
    {
        fuenteLuz = gameObject.AddComponent<FuenteLuz>();
        fuenteLuz.radio = 3f;
        fuenteLuz.parpadeo = 0.04f;
        fuenteLuz.desplazamiento = new Vector2(0f, 1.5f);
        fuenteLuz.encendida = false;
        if (radioLuz <= 0f) return;
        GameObject go = new GameObject("Luz");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, Mathf.Min(alto * 0.3f, 3f), 0f);
        luz = go.AddComponent<Light2D>();
        luz.lightType = Light2D.LightType.Point;
        luz.pointLightOuterRadius = radioLuz;
        luz.pointLightInnerRadius = 0f;
        luz.color = colorBorde;
        luz.intensity = 0f;
    }

    private AudioSource NuevaFuente(bool bucle)
    {
        AudioSource a = gameObject.AddComponent<AudioSource>();
        a.playOnAwake = false;
        a.loop = bucle;
        a.spatialBlend = 0f;
        return a;
    }

    private void Sonar(AudioSource a, AudioClip clip, float volumen)
    {
        if (clip == null) return;
        a.PlayOneShot(clip, volumen * ControlVolumen.Efectos * VolumenPorDistancia());
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(colorBorde.r, colorBorde.g, colorBorde.b, 0.9f);
        Vector3 c = transform.position + Vector3.up * alto * 0.5f;
        Gizmos.DrawWireCube(c, new Vector3(anchoBloqueo, alto, 0f));
        Gizmos.color = new Color(colorNiebla.r, colorNiebla.g, colorNiebla.b, 0.15f);
        Gizmos.DrawCube(c, new Vector3(anchoVisual, alto, 0f));
    }
}
