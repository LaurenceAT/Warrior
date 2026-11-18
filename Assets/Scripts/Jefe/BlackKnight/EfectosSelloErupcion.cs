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

// Indicador del amuleto sellado, con animacion (tiempo real: no le afectan las
// paradas de imagen):
//   1. Arriba en el centro aparece el amuleto en grande, con un aro morado.
//   2. Dos barras moradas caen encima en cruz: destello, sacudida y "SELLADO".
//   3. Se encoge hasta el indicador pequeno, con los segundos que quedan.
// Se quita solo al acabar el sello (o al quitarlo el jefe).
public class IndicadorSelloBK : MonoBehaviour
{
    private static IndicadorSelloBK actual;
    private RectTransform grupo, cruz, aro;
    private UnityEngine.UI.Image icono, destello;
    private UnityEngine.UI.Image[] barras;
    private TMPro.TextMeshProUGUI texto, sello;
    private string nombre;
    private Color color;
    private bool animando = true;

    public static bool Visible => actual != null;
    // Ya termino la animacion y queda el indicador pequeno (lo leen las pruebas).
    public static bool Asentado => actual != null && !actual.animando;
    public static float Escala => actual != null ? actual.grupo.localScale.x : 0f;

    // Donde queda el indicador pequeno y donde se ve la animacion (desde arriba).
    private const float YGrande = -200f, YPequeno = -110f, EscalaPequena = 0.5f;

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
        cs.matchWidthOrHeight = 0.5f;
        IndicadorSelloBK s = actual = go.AddComponent<IndicadorSelloBK>();
        s.nombre = nombre;
        s.color = color;

        // Todo cuelga de un grupo que luego se encoge y sube a su sitio.
        s.grupo = Crear<RectTransform>(go.transform, "Grupo", new Vector2(0f, YGrande), new Vector2(200f, 200f));
        s.aro = Crear<RectTransform>(s.grupo, "Aro", Vector2.zero, new Vector2(150f, 150f));
        UnityEngine.UI.Image fondoAro = s.aro.gameObject.AddComponent<UnityEngine.UI.Image>();
        fondoAro.color = new Color(color.r * 0.35f, color.g * 0.35f, color.b * 0.35f, 0.85f);
        UnityEngine.UI.Outline borde = s.aro.gameObject.AddComponent<UnityEngine.UI.Outline>();
        borde.effectColor = color;
        borde.effectDistance = new Vector2(4f, -4f);
        s.icono = Crear<UnityEngine.UI.Image>(s.grupo, "Icono", Vector2.zero, new Vector2(118f, 118f));
        s.icono.sprite = sprite;
        s.icono.preserveAspect = true;
        s.icono.color = sprite != null ? Color.white : new Color(color.r, color.g, color.b, 0.6f);

        // La cruz del sello: dos barras que caen desde lejos.
        s.cruz = Crear<RectTransform>(s.grupo, "Cruz", Vector2.zero, new Vector2(200f, 200f));
        s.barras = new UnityEngine.UI.Image[2];
        for (int i = 0; i < 2; i++)
        {
            s.barras[i] = Crear<UnityEngine.UI.Image>(s.cruz, "Barra", Vector2.zero, new Vector2(190f, 16f));
            s.barras[i].color = new Color(color.r, color.g, color.b, 0f);
            s.barras[i].rectTransform.localEulerAngles = new Vector3(0f, 0f, i == 0 ? 45f : -45f);
            UnityEngine.UI.Outline o = s.barras[i].gameObject.AddComponent<UnityEngine.UI.Outline>();
            o.effectColor = new Color(0.1f, 0.02f, 0.18f, 1f);
            o.effectDistance = new Vector2(2f, -2f);
        }
        s.destello = Crear<UnityEngine.UI.Image>(s.grupo, "Destello", Vector2.zero, new Vector2(260f, 260f));
        s.destello.color = new Color(1f, 1f, 1f, 0f);
        s.destello.raycastTarget = false;

        s.sello = Crear<TMPro.TextMeshProUGUI>(s.grupo, "Sello", new Vector2(0f, -105f), new Vector2(500f, 60f));
        s.sello.alignment = TMPro.TextAlignmentOptions.Center;
        s.sello.fontSize = 46f;
        s.sello.fontStyle = TMPro.FontStyles.Bold;
        s.sello.characterSpacing = 12f;
        s.sello.color = new Color(color.r, color.g, color.b, 0f);
        s.sello.outlineWidth = 0.25f;
        s.sello.outlineColor = new Color32(10, 0, 20, 255);
        s.sello.text = "SELLADO";

        s.texto = Crear<TMPro.TextMeshProUGUI>(s.grupo, "Texto", new Vector2(0f, 120f), new Vector2(700f, 50f));
        s.texto.alignment = TMPro.TextAlignmentOptions.Center;
        s.texto.fontSize = 32f;
        s.texto.color = new Color(0.92f, 0.85f, 1f, 1f);
        s.texto.outlineWidth = 0.2f;
        s.texto.outlineColor = new Color32(0, 0, 0, 255);
        s.texto.text = nombre;
        s.grupo.localScale = Vector3.zero;
        s.StartCoroutine(s.Animar());
    }

    private static T Crear<T>(Transform padre, string n, Vector2 pos, Vector2 tam) where T : Component
    {
        GameObject g = new GameObject(n, typeof(RectTransform));
        g.transform.SetParent(padre, false);
        RectTransform rt = (RectTransform)g.transform;
        rt.anchorMin = rt.anchorMax = padre is RectTransform && padre.name != "IndicadorSelloBK" ? new Vector2(0.5f, 0.5f) : new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = tam;
        return typeof(T) == typeof(RectTransform) ? (T)(Component)rt : g.AddComponent<T>();
    }

    private System.Collections.IEnumerator Animar()
    {
        // 1. Aparece el amuleto: crece de golpe y se asienta.
        for (float t = 0f; t < 0.35f; t += Time.unscaledDeltaTime)
        {
            float k = t / 0.35f;
            float e = k < 0.7f ? Mathf.Lerp(0f, 1.25f, k / 0.7f) : Mathf.Lerp(1.25f, 1f, (k - 0.7f) / 0.3f);
            grupo.localScale = Vector3.one * e;
            aro.localEulerAngles = new Vector3(0f, 0f, -90f * (1f - k));
            yield return null;
        }
        grupo.localScale = Vector3.one;
        for (float t = 0f; t < 0.3f; t += Time.unscaledDeltaTime) { aro.localScale = Vector3.one * (1f + 0.06f * Mathf.Sin(t * 30f)); yield return null; }

        // 2. Caen las barras en cruz desde lejos.
        for (float t = 0f; t < 0.18f; t += Time.unscaledDeltaTime)
        {
            float k = t / 0.18f;
            for (int i = 0; i < 2; i++)
            {
                barras[i].rectTransform.localScale = new Vector3(Mathf.Lerp(3f, 1f, k * k), 1f, 1f);
                barras[i].color = new Color(color.r, color.g, color.b, k);
            }
            yield return null;
        }
        foreach (UnityEngine.UI.Image b in barras) { b.rectTransform.localScale = Vector3.one; b.color = color; }
        // El golpe del sello: destello, sacudida, el amuleto se apaga y sale "SELLADO".
        Sonido.Reproducir("bloqueo", 1f, 0.7f);
        Sonido.Reproducir("parry_brillo", 0.6f, 0.6f);
        icono.color = new Color(0.45f, 0.4f, 0.55f, icono.color.a);
        Vector2 base0 = grupo.anchoredPosition;
        for (float t = 0f; t < 0.45f; t += Time.unscaledDeltaTime)
        {
            float k = t / 0.45f;
            destello.color = new Color(1f, 1f, 1f, 0.8f * (1f - k));
            grupo.anchoredPosition = base0 + Random.insideUnitCircle * 9f * (1f - k);
            float s = k < 0.35f ? Mathf.Lerp(2f, 0.92f, k / 0.35f) : Mathf.Lerp(0.92f, 1f, (k - 0.35f) / 0.65f);
            sello.rectTransform.localScale = Vector3.one * s;
            sello.color = new Color(color.r, color.g, color.b, Mathf.Clamp01(k * 3f));
            yield return null;
        }
        grupo.anchoredPosition = base0;
        destello.color = new Color(1f, 1f, 1f, 0f);
        for (float t = 0f; t < 0.8f; t += Time.unscaledDeltaTime) { Actualizar(); yield return null; }

        // 3. Se encoge hasta el indicador pequeno y el "SELLADO" se va.
        for (float t = 0f; t < 0.4f; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / 0.4f);
            grupo.localScale = Vector3.one * Mathf.Lerp(1f, EscalaPequena, k);
            grupo.anchoredPosition = new Vector2(0f, Mathf.Lerp(YGrande, YPequeno, k));
            sello.color = new Color(color.r, color.g, color.b, 1f - k);
            Actualizar();
            yield return null;
        }
        grupo.localScale = Vector3.one * EscalaPequena;
        grupo.anchoredPosition = new Vector2(0f, YPequeno);
        sello.gameObject.SetActive(false);
        animando = false;
    }

    private void Update() { if (!animando) Actualizar(); }

    private void Actualizar()
    {
        float s = EfectosAmuletos.SegundosSello;
        if (s <= 0f) { Destroy(gameObject); return; }
        texto.text = $"{nombre} sellado - {Mathf.CeilToInt(s)} s";
    }

    private void OnDestroy() { if (actual == this) actual = null; }
}
