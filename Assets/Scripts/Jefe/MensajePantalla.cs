using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Textos grandes en pantalla, sin sonido que los acompane:
//   - Titulo(): el nombre del jefe al empezar el combate.
//   - Narrativo(): la pista de la puerta del jefe, arriba, en cursiva.
//   - Banner(): "ENEMIGO CAIDO", el reconocimiento del reto...
// Todo se monta por codigo en un canvas propio.
public class MensajePantalla : MonoBehaviour
{
    private static MensajePantalla instancia;
    private Canvas canvas;

    private static MensajePantalla Get()
    {
        if (instancia != null) return instancia;
        GameObject go = new GameObject("MensajesPantalla");
        instancia = go.AddComponent<MensajePantalla>();
        instancia.canvas = go.AddComponent<Canvas>();
        instancia.canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        instancia.canvas.sortingOrder = 60;
        CanvasScaler cs = go.AddComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920, 1080);
        cs.matchWidthOrHeight = 0.5f;
        return instancia;
    }

    public static void Titulo(string nombre, string subtitulo)
    {
        MensajePantalla m = Get();
        m.StartCoroutine(m.Mostrar(m.Texto(nombre, 88, new Color(0.95f, 0.85f, 0.8f), new Vector2(0.5f, 0.6f), FontStyles.Bold),
                                   0.7f, 2.2f, 1.2f, 4f));
        m.StartCoroutine(m.Mostrar(m.Texto(subtitulo, 40, new Color(0.9f, 0.3f, 0.3f), new Vector2(0.5f, 0.52f), FontStyles.Italic),
                                   0.9f, 2f, 1.2f, 0f));
    }

    public static void Narrativo(string texto, float duracion = 6f)
    {
        MensajePantalla m = Get();
        TextMeshProUGUI t = m.Texto(texto, 34, new Color(0.9f, 0.85f, 0.8f), new Vector2(0.5f, 0.84f), FontStyles.Italic);
        t.rectTransform.sizeDelta = new Vector2(1400f, 200f);
        t.enableWordWrapping = true;
        m.StartCoroutine(m.Mostrar(t, 1f, duracion, 1.5f, 0f));
    }

    public static void Banner(string texto, Color color, float duracion = 4f)
    {
        MensajePantalla m = Get();
        // Franja oscura detras, como en los Souls.
        Image franja = new GameObject("Franja").AddComponent<Image>();
        franja.transform.SetParent(m.canvas.transform, false);
        franja.color = new Color(0f, 0f, 0f, 0.55f);
        RectTransform r = franja.rectTransform;
        r.anchorMin = new Vector2(0f, 0.44f);
        r.anchorMax = new Vector2(1f, 0.58f);
        r.offsetMin = r.offsetMax = Vector2.zero;
        CanvasGroup g = franja.gameObject.AddComponent<CanvasGroup>();

        TextMeshProUGUI t = m.Texto(texto, 76, color, new Vector2(0.5f, 0.51f), FontStyles.Bold);
        t.characterSpacing = 12f;
        m.StartCoroutine(m.Mostrar(t, 1.2f, duracion, 1.5f, 0f, g));
    }

    private TextMeshProUGUI Texto(string texto, float tamano, Color color, Vector2 ancla, FontStyles estilo)
    {
        TextMeshProUGUI t = new GameObject("Texto").AddComponent<TextMeshProUGUI>();
        t.transform.SetParent(canvas.transform, false);
        t.text = texto;
        t.fontSize = tamano;
        t.fontStyle = estilo;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.enableWordWrapping = false;
        t.outlineWidth = 0.18f;
        t.outlineColor = new Color32(0, 0, 0, 255);
        RectTransform r = t.rectTransform;
        r.anchorMin = r.anchorMax = ancla;
        r.sizeDelta = new Vector2(1800f, 160f);
        t.alpha = 0f;
        return t;
    }

    // Aparece, se queda y se va. "crecer" hace que el texto se abra un poco
    // mientras esta en pantalla (espaciado), da solemnidad al titulo.
    private IEnumerator Mostrar(TextMeshProUGUI t, float entrada, float quieto, float salida, float crecer, CanvasGroup fondo = null)
    {
        float espacio0 = t.characterSpacing;
        float total = entrada + quieto + salida;
        for (float x = 0f; x < total; x += Time.unscaledDeltaTime)
        {
            float a = x < entrada ? x / entrada : x < entrada + quieto ? 1f : 1f - (x - entrada - quieto) / salida;
            t.alpha = a;
            if (fondo != null) fondo.alpha = a;
            t.characterSpacing = espacio0 + crecer * (x / total);
            yield return null;
        }
        Destroy(t.gameObject);
        if (fondo != null) Destroy(fondo.gameObject);
    }
}

// Texto que sube y se desvanece en el mundo ("¡Debil!", "¡Sangrado!").
public class TextoFlotante : MonoBehaviour
{
    private TextMeshPro t;
    private float vida = 0.9f, edad;

    public static void Mostrar(string texto, Vector2 pos, Color color, float tamano = 1f)
    {
        GameObject go = new GameObject("TextoFlotante");
        go.transform.position = pos;
        TextMeshPro tm = go.AddComponent<TextMeshPro>();
        tm.text = texto;
        tm.fontSize = 3.2f * tamano;
        tm.fontStyle = FontStyles.Bold;
        tm.color = color;
        tm.alignment = TextAlignmentOptions.Center;
        tm.enableWordWrapping = false;
        tm.outlineWidth = 0.25f;
        tm.outlineColor = new Color32(0, 0, 0, 255);
        MeshRenderer mr = go.GetComponent<MeshRenderer>();
        mr.sortingLayerName = "VFX";
        mr.sortingOrder = 40;
        go.AddComponent<TextoFlotante>().t = tm;
    }

    private void Update()
    {
        edad += Time.unscaledDeltaTime;
        transform.position += Vector3.up * Time.unscaledDeltaTime * 1.2f;
        t.alpha = 1f - Mathf.Clamp01((edad - vida * 0.5f) / (vida * 0.5f));
        if (edad >= vida) Destroy(gameObject);
    }
}
