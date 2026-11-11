using System.Collections;
using UnityEngine;

// Efectos del Black Knight (2): la erupcion y el indicador del amuleto sellado.

// Erupcion: una grieta que brilla en el suelo (el aviso) y despues una columna de
// fuego que sube, del ancho y alto pedidos. No se para ni se bloquea: hay que
// apartarse o atravesarla esquivando. Toca una sola vez y acumula quemadura.
public class ErupcionBK : MonoBehaviour
{
    private const float Ppu = 28f;
    private const int AnchoCol = 24, AltoCol = 96, AnchoGrieta = 32, AltoGrieta = 5;
    private static Sprite columna, grieta;

    private JefeBlackKnight jefe;
    private AjustesBlackKnight aj;
    private float ancho, alto;
    private bool usada;

    public bool Tocada { get; private set; }
    public bool Esquivada { get; private set; }
    public bool Activa { get; private set; }
    public float Ancho => ancho;
    public float Alto => alto;

    public static ErupcionBK Crear(JefeBlackKnight jefe, Vector2 pies, float ancho, float alto, AjustesBlackKnight aj)
    {
        GameObject go = new GameObject("ErupcionBK");
        go.transform.position = pies;
        ErupcionBK e = go.AddComponent<ErupcionBK>();
        e.jefe = jefe;
        e.aj = aj;
        e.ancho = ancho;
        e.alto = alto;
        return e;
    }

    private IEnumerator Start()
    {
        Vector2 pies = transform.position;

        // 1. La grieta: late cada vez mas rapido.
        GameObject g = new GameObject("Grieta");
        g.transform.SetParent(transform, false);
        SpriteRenderer sg = DibujosCazadora.Renderer(g, "VFX", 8);
        sg.sprite = Grieta(aj);
        g.transform.localScale = new Vector3(ancho / (AnchoGrieta / Ppu), 1f, 1f);
        for (float t = 0f; t < aj.avisoErupcion; t += Time.deltaTime)
        {
            float k = t / aj.avisoErupcion;
            float pulso = 0.5f + 0.5f * Mathf.Sin(t * Mathf.Lerp(10f, 40f, k));
            sg.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.35f, 1f, k) * (0.55f + 0.45f * pulso));
            yield return null;
        }
        Destroy(g);

        // 2. La columna: sube de golpe, arde un momento y se apaga.
        GameObject c = new GameObject("Columna");
        c.transform.SetParent(transform, false);
        SpriteRenderer sc = DibujosCazadora.Renderer(c, "VFX", 18);
        sc.sprite = Columna(aj);
        Vector2 nativo = new Vector2(AnchoCol / Ppu, AltoCol / Ppu);
        float ex = ancho / nativo.x, ey = alto / nativo.y;
        Sonido.Reproducir("jefe_caida", 0.45f, Random.Range(1.1f, 1.4f));
        if (alto >= 3.5f) CamaraDinamica.Sacudir(0.25f);
        const float subir = 0.08f, arder = 0.4f, apagar = 0.18f;
        for (float t = 0f; t < subir + arder + apagar; t += Time.deltaTime)
        {
            float s = t < subir ? Mathf.Lerp(0.15f, 1f, t / subir)
                    : t < subir + arder ? 1f + 0.06f * Mathf.Sin(t * 40f)
                    : Mathf.Lerp(1f, 0f, (t - subir - arder) / apagar);
            c.transform.localScale = new Vector3(ex * (0.92f + 0.08f * Mathf.Sin(t * 33f)), ey * s, 1f);
            Activa = t < subir + arder;
            if (Activa && !usada && jefe != null)
            {
                Vector2 caja = new Vector2(ancho * 0.8f, alto * Mathf.Min(1f, s));
                Vector2 centro = pies + Vector2.up * caja.y * 0.5f;
                PlayerControler p = GolpeCazadora.PlayerEn(centro, caja);
                if (p != null)
                {
                    usada = true;
                    PlayerControler.ResultadoDano r = GolpeCazadora.Aplicar(p, aj.danoErupcion, jefe, PlayerControler.TipoDano.Magico,
                                                                            EstadoPlayer.Quemadura, aj.quemaduraErupcion, false, true);
                    if (r == PlayerControler.ResultadoDano.Ignorado) Esquivada = true; else Tocada = true;
                }
                if (DepuracionCazadora.VerCajas) CajaDebug.Mostrar(centro, caja, aj.colorErupcion, 0.03f);
            }
            yield return null;
        }
        Activa = false;
        Destroy(gameObject);
    }

    // Llama en pixel art: ancha abajo y en punta arriba, el centro mas claro y
    // lenguas sueltas en el borde. Se dibuja una vez y se escala para cada erupcion.
    private static Sprite Columna(AjustesBlackKnight aj)
    {
        if (columna != null) return columna;
        var tex = new Texture2D(AnchoCol, AltoCol, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[AnchoCol * AltoCol];
        var azar = new System.Random(11);
        Color fuera = aj.colorErupcion, centro = aj.colorErupcion2, rojo = new Color(0.8f, 0.15f, 0.05f, 0.85f);
        float medio = AnchoCol * 0.5f;
        for (int y = 0; y < AltoCol; y++)
        {
            float k = (float)y / AltoCol;
            float semi = medio * (1f - Mathf.Pow(k, 1.6f)) + (float)(azar.NextDouble() * 2.0 - 1.0) * 1.2f;
            for (int x = 0; x < AnchoCol; x++)
            {
                float d = Mathf.Abs(x + 0.5f - medio);
                if (d > semi) continue;
                float r = d / Mathf.Max(0.5f, semi);
                Color col = r < 0.35f ? centro : r < 0.8f ? fuera : rojo;
                if (k > 0.75f) col.a *= Mathf.Lerp(1f, 0.4f, (k - 0.75f) / 0.25f);
                px[y * AnchoCol + x] = col;
            }
            // Lenguas sueltas a los lados.
            if (y % 4 == 0 && azar.NextDouble() < 0.5)
            {
                int lado = azar.NextDouble() < 0.5 ? -1 : 1;
                int x0 = Mathf.RoundToInt(medio + lado * (semi + 1));
                for (int h = 0; h < 3 && y + h < AltoCol; h++)
                    if (x0 >= 0 && x0 < AnchoCol) px[(y + h) * AnchoCol + x0] = new Color(fuera.r, fuera.g, fuera.b, 0.6f);
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        columna = Sprite.Create(tex, new Rect(0, 0, AnchoCol, AltoCol), new Vector2(0.5f, 0f), Ppu, 0, SpriteMeshType.FullRect);
        columna.name = "ColumnaErupcionBK";
        return columna;
    }

    // La grieta: una linea quebrada de brasas a ras de suelo.
    private static Sprite Grieta(AjustesBlackKnight aj)
    {
        if (grieta != null) return grieta;
        var tex = new Texture2D(AnchoGrieta, AltoGrieta, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[AnchoGrieta * AltoGrieta];
        var azar = new System.Random(5);
        int y = 1;
        for (int x = 0; x < AnchoGrieta; x++)
        {
            if (azar.NextDouble() < 0.35) y = Mathf.Clamp(y + (azar.NextDouble() < 0.5 ? -1 : 1), 0, AltoGrieta - 2);
            px[y * AnchoGrieta + x] = aj.colorErupcion2;
            px[(y + 1) * AnchoGrieta + x] = new Color(aj.colorErupcion.r, aj.colorErupcion.g, aj.colorErupcion.b, 0.7f);
        }
        tex.SetPixels32(px);
        tex.Apply();
        grieta = Sprite.Create(tex, new Rect(0, 0, AnchoGrieta, AltoGrieta), new Vector2(0.5f, 0f), Ppu, 0, SpriteMeshType.FullRect);
        grieta.name = "GrietaErupcionBK";
        return grieta;
    }
}

// Indicador del amuleto sellado: su icono tachado en morado y los segundos que
// le quedan, arriba en el centro de la pantalla. Se quita solo al acabar el sello.
public class IndicadorSelloBK : MonoBehaviour
{
    private static IndicadorSelloBK actual;
    private TMPro.TextMeshProUGUI texto;
    private string nombre;

    public static bool Visible => actual != null;

    public static void Mostrar(Sprite sprite, string nombre, Color color)
    {
        if (actual != null) Destroy(actual.gameObject);
        GameObject go = new GameObject("IndicadorSelloBK");
        Canvas cv = go.AddComponent<Canvas>();
        cv.renderMode = RenderMode.ScreenSpaceOverlay;
        cv.sortingOrder = 40;
        UnityEngine.UI.CanvasScaler cs = go.AddComponent<UnityEngine.UI.CanvasScaler>();
        cs.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920f, 1080f);
        actual = go.AddComponent<IndicadorSelloBK>();
        actual.nombre = nombre;

        UnityEngine.UI.Image marco = Crear<UnityEngine.UI.Image>(go.transform, "Marco", new Vector2(0f, -120f), new Vector2(64f, 64f));
        marco.color = new Color(color.r, color.g, color.b, 0.55f);
        UnityEngine.UI.Image icono = Crear<UnityEngine.UI.Image>(go.transform, "Icono", new Vector2(0f, -120f), new Vector2(52f, 52f));
        icono.sprite = sprite;
        icono.preserveAspect = true;
        icono.color = sprite != null ? new Color(0.55f, 0.5f, 0.65f, 1f) : new Color(0f, 0f, 0f, 0f);
        // La cruz del sello encima del icono.
        foreach (float giro in new[] { 45f, -45f })
        {
            UnityEngine.UI.Image barra = Crear<UnityEngine.UI.Image>(go.transform, "Cruz", new Vector2(0f, -120f), new Vector2(70f, 6f));
            barra.color = color;
            barra.rectTransform.localEulerAngles = new Vector3(0f, 0f, giro);
        }
        actual.texto = Crear<TMPro.TextMeshProUGUI>(go.transform, "Texto", new Vector2(0f, -172f), new Vector2(600f, 40f));
        actual.texto.alignment = TMPro.TextAlignmentOptions.Center;
        actual.texto.fontSize = 26f;
        actual.texto.color = new Color(0.9f, 0.82f, 1f, 1f);
        actual.texto.outlineWidth = 0.2f;
        actual.texto.outlineColor = new Color32(0, 0, 0, 255);
        actual.Actualizar();
    }

    private static T Crear<T>(Transform padre, string n, Vector2 pos, Vector2 tam) where T : Component
    {
        GameObject g = new GameObject(n, typeof(RectTransform));
        g.transform.SetParent(padre, false);
        RectTransform rt = (RectTransform)g.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = tam;
        return g.AddComponent<T>();
    }

    private void Update() => Actualizar();

    private void Actualizar()
    {
        float s = EfectosAmuletos.SegundosSello;
        if (s <= 0f) { Destroy(gameObject); return; }
        texto.text = $"{nombre} sellado - {Mathf.CeilToInt(s)} s";
    }

    private void OnDestroy() { if (actual == this) actual = null; }
}
