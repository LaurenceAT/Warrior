using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Cartel de "objeto obtenido", como en los Souls: una franja oscura abajo en el
// centro con el icono, el nombre y para que sirve. Aparece, se queda un momento
// y se va. Si llegan varios seguidos, salen uno detras de otro.
public class AvisoObjeto : MonoBehaviour
{
    private static AvisoObjeto instancia;
    private readonly System.Collections.Generic.Queue<(Sprite, string, string)> cola =
        new System.Collections.Generic.Queue<(Sprite, string, string)>();
    private CanvasGroup grupo;
    private Image icono;
    private TextMeshProUGUI titulo, nombre, descripcion;
    private bool mostrando;

    public static void Mostrar(Sprite icono, string nombre, string descripcion)
    {
        if (instancia == null) instancia = Crear();
        instancia.cola.Enqueue((icono, nombre, descripcion));
        if (!instancia.mostrando) instancia.StartCoroutine(instancia.Siguiente());
    }

    private IEnumerator Siguiente()
    {
        mostrando = true;
        while (cola.Count > 0)
        {
            var (s, n, d) = cola.Dequeue();
            icono.sprite = s;
            icono.enabled = s != null;
            nombre.text = n;
            descripcion.text = d;
            for (float t = 0f; t < 0.3f; t += Time.unscaledDeltaTime) { grupo.alpha = t / 0.3f; yield return null; }
            grupo.alpha = 1f;
            yield return new WaitForSecondsRealtime(2.6f);
            for (float t = 0f; t < 0.4f; t += Time.unscaledDeltaTime) { grupo.alpha = 1f - t / 0.4f; yield return null; }
            grupo.alpha = 0f;
        }
        mostrando = false;
    }

    private static AvisoObjeto Crear()
    {
        GameObject go = new GameObject("AvisoObjeto");
        Canvas c = go.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 90;
        CanvasScaler cs = go.AddComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920, 1080);
        cs.matchWidthOrHeight = 0.5f;
        AvisoObjeto a = go.AddComponent<AvisoObjeto>();
        a.grupo = go.AddComponent<CanvasGroup>();
        a.grupo.alpha = 0f;
        a.grupo.blocksRaycasts = false;

        // Franja que se difumina hacia los lados.
        Image franja = EstiloMenu.Caja("Franja", go.transform, new Color(0f, 0f, 0f, 0.82f));
        RectTransform rf = franja.rectTransform;
        rf.anchorMin = new Vector2(0.2f, 0.2f);
        rf.anchorMax = new Vector2(0.8f, 0.2f);
        rf.sizeDelta = new Vector2(0f, 150f);
        Image arriba = EstiloMenu.Caja("Filo", franja.transform, EstiloMenu.FiloTenue);
        arriba.rectTransform.anchorMin = new Vector2(0.1f, 1f); arriba.rectTransform.anchorMax = new Vector2(0.9f, 1f);
        arriba.rectTransform.sizeDelta = new Vector2(0f, 2f);
        Image abajo = EstiloMenu.Caja("Filo", franja.transform, EstiloMenu.FiloTenue);
        abajo.rectTransform.anchorMin = new Vector2(0.1f, 0f); abajo.rectTransform.anchorMax = new Vector2(0.9f, 0f);
        abajo.rectTransform.sizeDelta = new Vector2(0f, 2f);

        a.titulo = EstiloMenu.Texto("OBJETO OBTENIDO", franja.transform, 22, new Color(0.8f, 0.72f, 0.58f));
        a.titulo.characterSpacing = 10f;
        EstiloMenu.Arriba(a.titulo.rectTransform, -12f, 30f);

        a.icono = EstiloMenu.Caja("Icono", franja.transform, Color.white);
        a.icono.preserveAspect = true;
        a.icono.rectTransform.anchorMin = a.icono.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        a.icono.rectTransform.sizeDelta = new Vector2(64f, 64f);
        a.icono.rectTransform.anchoredPosition = new Vector2(-250f, -12f);

        a.nombre = EstiloMenu.Texto("", franja.transform, 38, EstiloMenu.Titulo);
        a.nombre.alignment = TextAlignmentOptions.Left;
        a.nombre.rectTransform.anchorMin = a.nombre.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        a.nombre.rectTransform.sizeDelta = new Vector2(480f, 46f);
        a.nombre.rectTransform.anchoredPosition = new Vector2(40f, 4f);

        a.descripcion = EstiloMenu.Texto("", franja.transform, 22, EstiloMenu.TextoNormal);
        a.descripcion.alignment = TextAlignmentOptions.Left;
        a.descripcion.rectTransform.anchorMin = a.descripcion.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        a.descripcion.rectTransform.sizeDelta = new Vector2(480f, 30f);
        a.descripcion.rectTransform.anchoredPosition = new Vector2(40f, -36f);
        return a;
    }
}
