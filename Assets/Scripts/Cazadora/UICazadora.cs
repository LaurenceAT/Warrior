using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Interfaz propia del combate con la Cazadora:
//   - Bandas negras de cine (entrada y transiciones).
//   - Subtitulos cortos sobre la barra del jefe (sin pausar el juego).
//   - Vineta: pulso rojo del latido y bordes oscuros de El Silencio.
//   - Icono de oido arriba en El Silencio.
//   - Bajo la barra del jefe: las marcas I, II, III (barras que quedan), la
//     barra del escudo y el aviso "Resistente a X".
public class UICazadora : MonoBehaviour
{
    private static UICazadora inst;

    private RectTransform bandaArriba, bandaAbajo;
    private float bandas, bandasObjetivo;
    private TextMeshProUGUI subtitulo;
    private float finSubtitulo, alfaSubtitulo;
    private Image vineta, oido;
    private float pulsoRojo, finPulso, bordes, bordesObjetivo;
    private bool latido, iconoOido;
    private readonly TextMeshProUGUI[] marcas = new TextMeshProUGUI[3];
    private RectTransform escudo, rellenoEscudo;
    private TextMeshProUGUI textoResistencia;
    private Image iconoResistencia;
    private int fase;

    private static readonly Color Oscuro = new Color(0f, 0f, 0f, 1f);

    public static UICazadora Crear()
    {
        if (inst != null) return inst;
        GameObject go = new GameObject("UICazadora");
        inst = go.AddComponent<UICazadora>();
        Canvas c = go.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 54;
        CanvasScaler cs = go.AddComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920, 1080);
        cs.matchWidthOrHeight = 0.5f;
        inst.Montar(go.transform);
        return inst;
    }

    private void OnDestroy()
    {
        if (inst == this) inst = null;
    }

    // ------------------------------------------------------------------ API

    public static void Bandas(bool mostrar) { if (inst != null) inst.bandasObjetivo = mostrar ? 1f : 0f; }

    public static void Subtitulo(string texto, float segundos = 3.5f)
    {
        if (inst == null || string.IsNullOrEmpty(texto)) return;
        inst.subtitulo.text = texto;
        inst.finSubtitulo = Time.unscaledTime + segundos;
    }

    public static void PulsoRojo(float fuerza, float segundos)
    {
        if (inst == null) return;
        inst.pulsoRojo = fuerza;
        inst.finPulso = Time.unscaledTime + segundos;
    }

    public static void Latido(bool activo) { if (inst != null) inst.latido = activo; }

    public static void BordesOscuros(float cuanto) { if (inst != null) inst.bordesObjetivo = Mathf.Clamp01(cuanto); }

    public static void IconoOido(bool mostrar) { if (inst != null) inst.iconoOido = mostrar; }

    // Las marcas I-II-III, el escudo y la resistencia van bajo la barra del jefe.
    public static void MontarEnBarra(BarraJefe barra)
    {
        if (inst == null || barra == null || barra.Marco == null) return;
        inst.MontarBarra(barra.Marco);
    }

    public static void FaseActual(int f)
    {
        if (inst == null) return;
        inst.fase = f;
        for (int i = 0; i < 3; i++)
        {
            if (inst.marcas[i] == null) continue;
            bool gastada = i < f;
            inst.marcas[i].color = gastada ? new Color(0.4f, 0.36f, 0.34f, 0.7f) : i == f ? new Color(1f, 0.92f, 0.75f) : new Color(0.85f, 0.8f, 0.72f, 0.85f);
            inst.marcas[i].fontStyle = i == f ? FontStyles.Bold : FontStyles.Normal;
            inst.marcas[i].text = gastada ? "<s>" + Romano(i) + "</s>" : Romano(i);
        }
    }

    public static void Escudo(float fraccion)
    {
        if (inst == null || inst.escudo == null) return;
        bool on = fraccion >= 0f;
        if (inst.escudo.gameObject.activeSelf != on) inst.escudo.gameObject.SetActive(on);
        if (on) inst.rellenoEscudo.anchorMax = new Vector2(Mathf.Clamp01(fraccion), 1f);
    }

    public static void Resistencia(Elemento e)
    {
        if (inst == null || inst.textoResistencia == null) return;
        bool on = e != Elemento.Ninguno;
        inst.textoResistencia.gameObject.SetActive(on);
        inst.iconoResistencia.gameObject.SetActive(on);
        if (!on) return;
        inst.textoResistencia.text = "Resistente a " + Elementos.Nombre(e);
        inst.textoResistencia.color = Elementos.Color(e);
        inst.iconoResistencia.sprite = RecursosRPG.Get().Icono("elemento_" + (int)e);
    }

    private static string Romano(int i) => i == 0 ? "I" : i == 1 ? "II" : "III";

    // ------------------------------------------------------------------ Cada fotograma

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        bandas = Mathf.MoveTowards(bandas, bandasObjetivo, dt * 2.2f);
        float alto = 115f * (bandas * bandas * (3f - 2f * bandas));
        bandaArriba.sizeDelta = new Vector2(0f, alto);
        bandaAbajo.sizeDelta = new Vector2(0f, alto);

        alfaSubtitulo = Mathf.MoveTowards(alfaSubtitulo, Time.unscaledTime < finSubtitulo ? 1f : 0f, dt * 3f);
        Color cs = subtitulo.color; cs.a = alfaSubtitulo; subtitulo.color = cs;

        bordes = Mathf.MoveTowards(bordes, bordesObjetivo, dt * 1.5f);
        float rojo = 0f;
        if (Time.unscaledTime < finPulso) rojo = pulsoRojo * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 9f));
        if (latido)
        {
            // Latido doble (pum-pum) cada ~0.9 s.
            float f = Mathf.Repeat(Time.unscaledTime, 0.9f);
            float golpe = Mathf.Exp(-f * 14f) + 0.7f * Mathf.Exp(-Mathf.Abs(f - 0.22f) * 18f);
            rojo = Mathf.Max(rojo, 0.45f * golpe);
        }
        Color v = Color.Lerp(new Color(0f, 0f, 0f, 0f), new Color(0.02f, 0.02f, 0.03f, 0.85f), bordes);
        if (rojo > 0.01f) v = Color.Lerp(v, new Color(0.55f, 0f, 0.02f, 0.9f), Mathf.Clamp01(rojo));
        vineta.color = v;

        float ao = iconoOido ? 0.6f + 0.3f * Mathf.Sin(Time.unscaledTime * 4f) : 0f;
        oido.color = new Color(0.85f, 0.92f, 1f, Mathf.MoveTowards(oido.color.a, ao, dt * 3f));
    }

    // ------------------------------------------------------------------ Construccion

    private void Montar(Transform raiz)
    {
        vineta = EstiloMenu.Caja("Vineta", raiz, new Color(0f, 0f, 0f, 0f));
        vineta.sprite = SpriteVineta();
        vineta.raycastTarget = false;
        EstiloMenu.Estirar(vineta.rectTransform);

        bandaArriba = Banda(raiz, true);
        bandaAbajo = Banda(raiz, false);

        subtitulo = EstiloMenu.Texto("", raiz, 30, new Color(0.9f, 0.9f, 0.85f, 0f));
        subtitulo.fontStyle = FontStyles.Italic;
        subtitulo.outlineWidth = 0.2f;
        subtitulo.outlineColor = new Color32(0, 0, 0, 255);
        RectTransform rs = subtitulo.rectTransform;
        rs.anchorMin = new Vector2(0.15f, 0.15f);
        rs.anchorMax = new Vector2(0.85f, 0.15f);
        rs.sizeDelta = new Vector2(0f, 50f);
        rs.anchoredPosition = new Vector2(0f, 22f);

        oido = EstiloMenu.Caja("Oido", raiz, new Color(1f, 1f, 1f, 0f));
        oido.sprite = DibujosCazadora.Oido();
        oido.preserveAspect = true;
        oido.raycastTarget = false;
        RectTransform ro = oido.rectTransform;
        ro.anchorMin = ro.anchorMax = new Vector2(0.5f, 0.82f);
        ro.sizeDelta = new Vector2(84f, 84f);
    }

    private RectTransform Banda(Transform raiz, bool arriba)
    {
        Image i = EstiloMenu.Caja(arriba ? "BandaArriba" : "BandaAbajo", raiz, Oscuro);
        i.raycastTarget = false;
        RectTransform r = i.rectTransform;
        r.anchorMin = new Vector2(0f, arriba ? 1f : 0f);
        r.anchorMax = new Vector2(1f, arriba ? 1f : 0f);
        r.pivot = new Vector2(0.5f, arriba ? 1f : 0f);
        r.sizeDelta = Vector2.zero;
        return r;
    }

    private void MontarBarra(RectTransform marco)
    {
        // Marcas I II III encima de la barra, en su extremo derecho (el nombre va
        // encima a la izquierda): asi la barra puede ser larga (AjustesInterfaz).
        for (int i = 0; i < 3; i++)
        {
            TextMeshProUGUI t = EstiloMenu.Texto(Romano(i), marco, 26, Color.white);
            t.outlineWidth = 0.2f;
            t.outlineColor = new Color32(0, 0, 0, 255);
            RectTransform r = t.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(1f, 1f);
            r.pivot = new Vector2(1f, 0f);
            r.sizeDelta = new Vector2(48f, 34f);
            r.anchoredPosition = new Vector2(-(2 - i) * 50f, 6f);
            marcas[i] = t;
        }
        FaseActual(fase);

        // Barra del escudo, fina y dorada, justo encima de la de vida.
        Image fondo = EstiloMenu.Caja("Escudo", marco, new Color(0.08f, 0.07f, 0.04f, 0.9f));
        escudo = fondo.rectTransform;
        escudo.anchorMin = new Vector2(0.25f, 1f);
        escudo.anchorMax = new Vector2(0.75f, 1f);
        escudo.pivot = new Vector2(0.5f, 0f);
        escudo.sizeDelta = new Vector2(0f, 9f);
        escudo.anchoredPosition = new Vector2(0f, 52f);
        Image relleno = EstiloMenu.Caja("Relleno", escudo, new Color(1f, 0.88f, 0.5f, 1f));
        rellenoEscudo = relleno.rectTransform;
        rellenoEscudo.anchorMin = Vector2.zero;
        rellenoEscudo.anchorMax = Vector2.one;
        rellenoEscudo.offsetMin = new Vector2(1f, 1f);
        rellenoEscudo.offsetMax = new Vector2(-1f, -1f);
        TextMeshProUGUI te = EstiloMenu.Texto("Escudo de luz", escudo, 18, new Color(1f, 0.92f, 0.7f));
        te.rectTransform.anchoredPosition = new Vector2(0f, 16f);
        escudo.gameObject.SetActive(false);

        // "Resistente a X" con el icono del elemento, bajo la barra a la derecha.
        iconoResistencia = EstiloMenu.Caja("IconoResistencia", marco, Color.white);
        iconoResistencia.preserveAspect = true;
        RectTransform ri = iconoResistencia.rectTransform;
        ri.anchorMin = ri.anchorMax = new Vector2(1f, 0f);
        ri.pivot = new Vector2(1f, 1f);
        ri.sizeDelta = new Vector2(32f, 32f);
        ri.anchoredPosition = new Vector2(-250f, -6f);
        textoResistencia = EstiloMenu.Texto("", marco, 22, Color.white);
        textoResistencia.alignment = TextAlignmentOptions.Right;
        textoResistencia.outlineWidth = 0.2f;
        textoResistencia.outlineColor = new Color32(0, 0, 0, 255);
        RectTransform rt = textoResistencia.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 1f);
        rt.sizeDelta = new Vector2(240f, 32f);
        rt.anchoredPosition = new Vector2(0f, -6f);
        Resistencia(Elemento.Ninguno);
    }

    private static Sprite vinetaSprite;

    private static Sprite SpriteVineta()
    {
        if (vinetaSprite != null) return vinetaSprite;
        const int n = 128;
        Texture2D t = new Texture2D(n, n, TextureFormat.RGBA32, false);
        t.wrapMode = TextureWrapMode.Clamp;
        Color[] px = new Color[n * n];
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
            float d = Mathf.Sqrt(dx * dx + dy * dy) / 1.414f;
            float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1f, d));
            px[y * n + x] = new Color(1f, 1f, 1f, a);
        }
        t.SetPixels(px);
        t.Apply();
        vinetaSprite = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        return vinetaSprite;
    }
}
