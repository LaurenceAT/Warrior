using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// La animacion de "te ha robado el frasco" (Diezmo de sangre del Black Knight),
// al estilo de la del sello: el frasco aparece grande en el centro de arriba, se
// agrieta y se vacia mientras sube la cuenta de lo que el jefe se cura, y luego
// se encoge y se va. Es solo imagen: el robo y la curacion los hace el jefe.
public class IndicadorRoboBK : MonoBehaviour
{
    private static IndicadorRoboBK actual;
    private RectTransform grupo, aro;
    private Image icono, destello, vacio;
    private TextMeshProUGUI texto, cura;
    private Color color;
    private int curado;
    private bool animando = true;

    public static bool Visible => actual != null;
    // Ya acabo la animacion (lo leen las pruebas).
    public static bool Terminado => actual == null || !actual.animando;

    private const float YGrande = -200f;

    public static void Mostrar(Sprite frasco, string texto, Color color, int curado)
    {
        if (actual != null) Destroy(actual.gameObject);
        GameObject go = new GameObject("IndicadorRoboBK");
        Canvas cv = go.AddComponent<Canvas>();
        cv.renderMode = RenderMode.ScreenSpaceOverlay;
        cv.sortingOrder = 40;
        CanvasScaler cs = go.AddComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920f, 1080f);
        cs.matchWidthOrHeight = 0.5f;
        IndicadorRoboBK r = actual = go.AddComponent<IndicadorRoboBK>();
        r.color = color;
        r.curado = curado;

        r.grupo = Crear<RectTransform>(go.transform, "Grupo", new Vector2(0f, YGrande), new Vector2(200f, 200f));
        r.aro = Crear<RectTransform>(r.grupo, "Aro", Vector2.zero, new Vector2(150f, 150f));
        Image fondo = r.aro.gameObject.AddComponent<Image>();
        fondo.color = new Color(color.r * 0.3f, color.g * 0.25f, color.b * 0.25f, 0.85f);
        Outline borde = r.aro.gameObject.AddComponent<Outline>();
        borde.effectColor = color;
        borde.effectDistance = new Vector2(4f, -4f);

        r.icono = Crear<Image>(r.grupo, "Frasco", Vector2.zero, new Vector2(110f, 110f));
        r.icono.sprite = frasco;
        r.icono.preserveAspect = true;
        r.icono.color = frasco != null ? Color.white : new Color(color.r, color.g, color.b, 0.7f);
        // Un velo oscuro que va tapando el frasco: se vacia.
        r.vacio = Crear<Image>(r.grupo, "Vacio", Vector2.zero, new Vector2(110f, 110f));
        r.vacio.sprite = frasco;
        r.vacio.preserveAspect = true;
        r.vacio.color = new Color(0.1f, 0.05f, 0.06f, 0f);

        r.destello = Crear<Image>(r.grupo, "Destello", Vector2.zero, new Vector2(260f, 260f));
        r.destello.color = new Color(1f, 1f, 1f, 0f);
        r.destello.raycastTarget = false;

        r.texto = Crear<TextMeshProUGUI>(r.grupo, "Texto", new Vector2(0f, -105f), new Vector2(620f, 60f));
        r.texto.alignment = TextAlignmentOptions.Center;
        r.texto.fontSize = 44f;
        r.texto.fontStyle = FontStyles.Bold;
        r.texto.characterSpacing = 10f;
        r.texto.color = new Color(color.r, color.g, color.b, 0f);
        r.texto.outlineWidth = 0.25f;
        r.texto.outlineColor = new Color32(20, 0, 4, 255);
        r.texto.text = texto.ToUpperInvariant();

        r.cura = Crear<TextMeshProUGUI>(r.grupo, "Cura", new Vector2(0f, 118f), new Vector2(500f, 50f));
        r.cura.alignment = TextAlignmentOptions.Center;
        r.cura.fontSize = 34f;
        r.cura.color = new Color(1f, 0.85f, 0.85f, 0f);
        r.cura.outlineWidth = 0.2f;
        r.cura.outlineColor = new Color32(0, 0, 0, 255);
        r.cura.text = "";

        r.grupo.localScale = Vector3.zero;
        r.StartCoroutine(r.Animar());
    }

    private static T Crear<T>(Transform padre, string n, Vector2 pos, Vector2 tam) where T : Component
    {
        GameObject g = new GameObject(n, typeof(RectTransform));
        g.transform.SetParent(padre, false);
        RectTransform rt = (RectTransform)g.transform;
        rt.anchorMin = rt.anchorMax = padre is RectTransform && padre.name != "IndicadorRoboBK" ? new Vector2(0.5f, 0.5f) : new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = tam;
        return typeof(T) == typeof(RectTransform) ? (T)(Component)rt : g.AddComponent<T>();
    }

    private IEnumerator Animar()
    {
        // 1. El frasco aparece de golpe, como arrancado de tu cinturon.
        for (float t = 0f; t < 0.3f; t += Time.unscaledDeltaTime)
        {
            float k = t / 0.3f;
            float e = k < 0.7f ? Mathf.Lerp(0f, 1.25f, k / 0.7f) : Mathf.Lerp(1.25f, 1f, (k - 0.7f) / 0.3f);
            grupo.localScale = Vector3.one * e;
            grupo.anchoredPosition = new Vector2(Mathf.Lerp(-260f, 0f, Mathf.SmoothStep(0f, 1f, k)), YGrande);
            yield return null;
        }
        grupo.localScale = Vector3.one;
        grupo.anchoredPosition = new Vector2(0f, YGrande);
        Sonido.Reproducir("bloqueo", 0.8f, 0.6f);

        // 2. Se vacia: el velo sube y el texto sale con un temblor.
        Vector2 base0 = grupo.anchoredPosition;
        for (float t = 0f; t < 0.55f; t += Time.unscaledDeltaTime)
        {
            float k = t / 0.55f;
            vacio.color = new Color(0.1f, 0.05f, 0.06f, 0.85f * k);
            icono.color = Color.Lerp(Color.white, new Color(0.5f, 0.35f, 0.38f, 1f), k);
            destello.color = new Color(1f, 0.5f, 0.5f, 0.7f * (1f - k));
            grupo.anchoredPosition = base0 + Random.insideUnitCircle * 8f * (1f - k);
            texto.color = new Color(color.r, color.g, color.b, Mathf.Clamp01(k * 2.5f));
            texto.rectTransform.localScale = Vector3.one * (k < 0.35f ? Mathf.Lerp(1.8f, 0.95f, k / 0.35f) : Mathf.Lerp(0.95f, 1f, (k - 0.35f) / 0.65f));
            cura.color = new Color(1f, 0.85f, 0.85f, Mathf.Clamp01((k - 0.3f) * 2.5f));
            cura.text = "+" + Mathf.RoundToInt(curado * Mathf.Clamp01(k * 1.4f));
            yield return null;
        }
        grupo.anchoredPosition = base0;
        destello.color = new Color(1f, 1f, 1f, 0f);
        cura.text = "+" + curado;
        Sonido.Reproducir("objeto_obtenido", 0.7f, 0.7f);

        // 3. Se queda un momento y se va hacia arriba (hacia el jefe).
        for (float t = 0f; t < 0.9f; t += Time.unscaledDeltaTime) yield return null;
        for (float t = 0f; t < 0.45f; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / 0.45f);
            grupo.localScale = Vector3.one * Mathf.Lerp(1f, 0.6f, k);
            grupo.anchoredPosition = new Vector2(0f, Mathf.Lerp(YGrande, YGrande + 90f, k));
            SetAlfa(1f - k);
            yield return null;
        }
        animando = false;
        Destroy(gameObject);
    }

    private void SetAlfa(float a)
    {
        icono.color = new Color(icono.color.r, icono.color.g, icono.color.b, a);
        vacio.color = new Color(vacio.color.r, vacio.color.g, vacio.color.b, 0.85f * a);
        texto.color = new Color(color.r, color.g, color.b, a);
        cura.color = new Color(1f, 0.85f, 0.85f, a);
        Image fondo = aro.GetComponent<Image>();
        if (fondo != null) fondo.color = new Color(fondo.color.r, fondo.color.g, fondo.color.b, 0.85f * a);
    }

    private void OnDestroy() { if (actual == this) actual = null; }
}
