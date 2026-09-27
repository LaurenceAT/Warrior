using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Almas que suelta un enemigo al morir: unas chispas palidas salen del cuerpo,
// flotan un instante y vuelan al player. Al llegar se suman (Progreso).
public class OrbeAlma : MonoBehaviour
{
    private int valor;
    private Vector2 velocidad;
    private float t;
    private Transform objetivo;
    private SpriteRenderer sr;
    private TrailRenderer estela;
    private static Sprite punto;

    public static readonly Color ColorAlma = new Color(0.75f, 0.9f, 1f, 1f);

    public static void Soltar(Vector2 donde, int total)
    {
        if (total <= 0) return;
        // Varias chispas repartiendose el total (la ultima se lleva el resto).
        int n = Mathf.Clamp(total / 25, 3, 12);
        int cada = total / n;
        for (int i = 0; i < n; i++)
        {
            int v = i == n - 1 ? total - cada * (n - 1) : cada;
            Crear(donde, v, Random.insideUnitCircle.normalized * Random.Range(2f, 4.5f) + Vector2.up * 2f);
        }
    }

    private static void Crear(Vector2 donde, int valor, Vector2 vel)
    {
        GameObject go = new GameObject("OrbeAlma");
        go.transform.position = donde;
        OrbeAlma o = go.AddComponent<OrbeAlma>();
        o.valor = valor;
        o.velocidad = vel;
        o.sr = go.AddComponent<SpriteRenderer>();
        o.sr.sprite = Punto();
        o.sr.color = ColorAlma;
        o.sr.sortingLayerName = "VFX";
        o.sr.sortingOrder = 20;
        o.sr.sharedMaterial = EfectoVisual.MaterialSinLuz();
        go.transform.localScale = Vector3.one * Random.Range(0.18f, 0.26f);

        o.estela = go.AddComponent<TrailRenderer>();
        o.estela.time = 0.25f;
        o.estela.startWidth = 0.12f;
        o.estela.endWidth = 0f;
        o.estela.sharedMaterial = EfectoVisual.MaterialSinLuz();
        o.estela.startColor = new Color(0.8f, 0.95f, 1f, 0.8f);
        o.estela.endColor = new Color(0.5f, 0.7f, 1f, 0f);
        o.estela.sortingLayerName = "VFX";
        o.estela.sortingOrder = 19;
    }

    private void Update()
    {
        t += Time.deltaTime;
        if (objetivo == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) objetivo = p.transform;
        }

        // Primero se dispersa y frena; luego vuela hacia el player, cada vez mas rapido.
        if (t < 0.45f || objetivo == null)
        {
            velocidad = Vector2.Lerp(velocidad, Vector2.zero, Time.deltaTime * 4f);
            transform.position += (Vector3)(velocidad * Time.deltaTime);
            if (objetivo == null && t > 8f) Destroy(gameObject);
        }
        else
        {
            Vector2 hacia = (Vector2)objetivo.position - (Vector2)transform.position;
            float rapidez = Mathf.Lerp(4f, 22f, Mathf.Clamp01((t - 0.45f) / 0.8f));
            velocidad = Vector2.Lerp(velocidad, hacia.normalized * rapidez, Time.deltaTime * 8f);
            transform.position += (Vector3)(velocidad * Time.deltaTime);
            if (hacia.magnitude < 0.35f)
            {
                Progreso.SumarAlmas(valor);
                Sonido.Reproducir("alma_recoger", 0.6f, Random.Range(0.95f, 1.2f));
                Destroy(gameObject);
                return;
            }
        }
        sr.color = new Color(ColorAlma.r, ColorAlma.g, ColorAlma.b, 0.7f + 0.3f * Mathf.Sin(t * 18f));
    }

    public static Sprite Punto()
    {
        if (punto != null) return punto;
        const int n = 16;
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n * 0.5f, n * 0.5f)) / (n * 0.5f);
            float a = Mathf.Clamp01(1f - d);
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
        }
        tex.Apply();
        punto = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
        return punto;
    }
}

// Contador de almas abajo a la derecha, como en los Souls. Al ganar almas se ve
// la cifra que entra ("+120") y el total va subiendo poco a poco.
public class ContadorAlmas : MonoBehaviour
{
    private static ContadorAlmas instancia;
    private TextMeshProUGUI total, ganancia;
    private CanvasGroup grupoGanancia;
    private float mostrado;
    private int ultimo;
    private int acumulado;
    private float esperaGanancia;

    public static void Asegurar()
    {
        if (instancia != null) return;
        GameObject go = new GameObject("ContadorAlmas");
        Canvas c = go.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 41;
        CanvasScaler cs = go.AddComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920, 1080);
        cs.matchWidthOrHeight = 0.5f;
        instancia = go.AddComponent<ContadorAlmas>();
        instancia.Construir();
    }

    private void Construir()
    {
        Image caja = new GameObject("Caja").AddComponent<Image>();
        caja.transform.SetParent(transform, false);
        caja.color = new Color(0.03f, 0.03f, 0.05f, 0.7f);
        RectTransform rc = caja.rectTransform;
        rc.anchorMin = rc.anchorMax = new Vector2(1f, 0f);
        rc.pivot = new Vector2(1f, 0f);
        rc.anchoredPosition = new Vector2(-40f, 36f);
        rc.sizeDelta = new Vector2(250f, 58f);
        Image borde = new GameObject("Borde").AddComponent<Image>();
        borde.transform.SetParent(caja.transform, false);
        borde.color = new Color(0.78f, 0.72f, 0.58f, 0.8f);
        RectTransform rb = borde.rectTransform;
        rb.anchorMin = new Vector2(0f, 0f); rb.anchorMax = new Vector2(1f, 0f);
        rb.sizeDelta = new Vector2(0f, 2f);

        Image icono = new GameObject("Icono").AddComponent<Image>();
        icono.transform.SetParent(caja.transform, false);
        icono.sprite = RecursosRPG.Get().Icono("almas");
        icono.preserveAspect = true;
        icono.enabled = icono.sprite != null;
        RectTransform ri = icono.rectTransform;
        ri.anchorMin = ri.anchorMax = new Vector2(0f, 0.5f);
        ri.pivot = new Vector2(0f, 0.5f);
        ri.anchoredPosition = new Vector2(10f, 0f);
        ri.sizeDelta = new Vector2(42f, 42f);

        total = Texto(caja.transform, 34, TextAlignmentOptions.MidlineRight);
        total.rectTransform.anchorMin = Vector2.zero;
        total.rectTransform.anchorMax = Vector2.one;
        total.rectTransform.offsetMin = new Vector2(60f, 0f);
        total.rectTransform.offsetMax = new Vector2(-16f, 0f);

        ganancia = Texto(caja.transform, 26, TextAlignmentOptions.BottomRight);
        ganancia.color = new Color(0.8f, 0.93f, 1f);
        RectTransform rg = ganancia.rectTransform;
        rg.anchorMin = new Vector2(0f, 1f); rg.anchorMax = new Vector2(1f, 1f);
        rg.pivot = new Vector2(1f, 0f);
        rg.offsetMin = new Vector2(0f, 4f); rg.offsetMax = new Vector2(-16f, 40f);
        grupoGanancia = ganancia.gameObject.AddComponent<CanvasGroup>();
        grupoGanancia.alpha = 0f;

        mostrado = ultimo = Progreso.Almas;
        total.text = ultimo.ToString("N0");
        Progreso.AlCambiarAlmas += Cambio;
    }

    private static TextMeshProUGUI Texto(Transform padre, float tamano, TextAlignmentOptions alin)
    {
        TextMeshProUGUI t = new GameObject("Texto").AddComponent<TextMeshProUGUI>();
        t.transform.SetParent(padre, false);
        t.fontSize = tamano;
        t.color = new Color(0.93f, 0.9f, 0.85f);
        t.alignment = alin;
        t.outlineWidth = 0.2f;
        t.outlineColor = new Color32(0, 0, 0, 255);
        t.enableWordWrapping = false;
        return t;
    }

    private void OnDestroy()
    {
        Progreso.AlCambiarAlmas -= Cambio;
        if (instancia == this) instancia = null;
    }

    private void Cambio()
    {
        int nuevo = Progreso.Almas;
        if (nuevo > ultimo)
        {
            acumulado += nuevo - ultimo;
            ganancia.text = "+" + acumulado.ToString("N0");
            grupoGanancia.alpha = 1f;
            esperaGanancia = 1.4f;
        }
        else
        {
            // Gasto o perdida: la cifra baja de golpe.
            mostrado = nuevo;
            acumulado = 0;
            grupoGanancia.alpha = 0f;
        }
        ultimo = nuevo;
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        if (esperaGanancia > 0f)
        {
            esperaGanancia -= dt;
            if (esperaGanancia <= 0f) acumulado = 0;
        }
        else grupoGanancia.alpha = Mathf.MoveTowards(grupoGanancia.alpha, 0f, dt * 2f);

        if (mostrado < ultimo) mostrado = Mathf.Min(ultimo, mostrado + Mathf.Max(ultimo - mostrado, 40f) * dt * 2.5f);
        total.text = Mathf.FloorToInt(mostrado).ToString("N0");
    }
}

// Mancha de almas: donde murio el player la ultima vez. Tocarla devuelve las
// almas. La crea el GameManager al cargar la escena y al reaparecer.
public class ManchaAlmas : MonoBehaviour
{
    private static ManchaAlmas actual;
    private float t;
    private SpriteRenderer brillo;

    public static void Colocar()
    {
        if (actual != null) Destroy(actual.gameObject);
        if (Progreso.AlmasPerdidas <= 0 || Progreso.EscenaMancha != SceneManager.GetActiveScene().name) return;

        GameObject go = new GameObject("ManchaAlmas");
        go.transform.position = Progreso.PosicionMancha;
        go.layer = LayerMask.NameToLayer("Items");
        CircleCollider2D c = go.AddComponent<CircleCollider2D>();
        c.isTrigger = true;
        c.radius = 0.6f;
        actual = go.AddComponent<ManchaAlmas>();
        actual.brillo = go.AddComponent<SpriteRenderer>();
        actual.brillo.sprite = OrbeAlma.Punto();
        actual.brillo.sharedMaterial = EfectoVisual.MaterialSinLuz();
        actual.brillo.sortingLayerName = "VFX";
        actual.brillo.sortingOrder = 3;
        go.transform.localScale = Vector3.one * 0.7f;
    }

    private void Update()
    {
        t += Time.deltaTime;
        float p = 0.5f + 0.5f * Mathf.Sin(t * 3f);
        brillo.color = new Color(0.55f, 0.9f, 0.75f, 0.55f + 0.35f * p);
        transform.localScale = Vector3.one * (0.6f + 0.15f * p);
        if (Random.value < Time.deltaTime * 6f)
            ParticulasFx.Rafaga((Vector2)transform.position + Random.insideUnitCircle * 0.2f, 2,
                                new Color(0.6f, 1f, 0.8f), OrbeAlma.ColorAlma, new Vector2(0.3f, 0.8f), -0.3f,
                                new Vector2(0.04f, 0.08f), new Vector2(0.6f, 1f), 40f, 90f);
    }

    private void OnTriggerEnter2D(Collider2D otro)
    {
        if (!otro.CompareTag("Player")) return;
        int n = Progreso.AlmasPerdidas;
        Progreso.RecuperarMancha();
        TextoFlotante.Mostrar("Almas recuperadas +" + n.ToString("N0"), (Vector2)transform.position + Vector2.up * 1.2f, OrbeAlma.ColorAlma, 0.9f);
        Sonido.Reproducir("alma_mancha");
        ParticulasFx.Rafaga(transform.position, 30, new Color(0.7f, 1f, 0.85f), OrbeAlma.ColorAlma,
                            new Vector2(1.5f, 4f), -0.2f, new Vector2(0.05f, 0.12f), new Vector2(0.5f, 1f));
        actual = null;
        Destroy(gameObject);
    }
}
