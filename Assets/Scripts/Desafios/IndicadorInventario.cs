using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Desafios: una marca discreta junto a los frascos cuando hay objetos de mejora
// sin aplicar ("2 por aplicar · hoguera"). Late un momento al llegar uno nuevo.
// Sin objetos pendientes no se ve. Lo crea ModoDesafio en cada escena.
public class IndicadorInventario : MonoBehaviour
{
    private CanvasGroup grupo;
    private RectTransform caja;
    private Image icono;
    private TextMeshProUGUI texto;
    private int antes;

    public static void Crear()
    {
        GameObject go = new GameObject("IndicadorInventario");
        Canvas c = go.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 41;
        CanvasScaler cs = go.AddComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920, 1080);
        cs.matchWidthOrHeight = 0.5f;
        IndicadorInventario ind = go.AddComponent<IndicadorInventario>();
        ind.grupo = go.AddComponent<CanvasGroup>();
        ind.grupo.blocksRaycasts = false;
        ind.grupo.interactable = false;

        ind.caja = new GameObject("Caja", typeof(RectTransform)).GetComponent<RectTransform>();
        ind.caja.SetParent(go.transform, false);
        ind.caja.anchorMin = ind.caja.anchorMax = new Vector2(0f, 0f);
        ind.caja.pivot = new Vector2(0f, 0f);
        ind.caja.anchoredPosition = new Vector2(456f, 70f);
        ind.caja.sizeDelta = new Vector2(360f, 48f);

        ind.icono = EstiloMenu.Caja("Icono", ind.caja, Color.white);
        ind.icono.preserveAspect = true;
        RectTransform ri = ind.icono.rectTransform;
        ri.anchorMin = ri.anchorMax = new Vector2(0f, 0.5f);
        ri.pivot = new Vector2(0f, 0.5f);
        ri.sizeDelta = new Vector2(40f, 40f);

        ind.texto = EstiloMenu.Texto("", ind.caja, 22, new Color(0.95f, 0.88f, 0.7f));
        ind.texto.alignment = TextAlignmentOptions.MidlineLeft;
        ind.texto.outlineWidth = 0.2f;
        ind.texto.outlineColor = new Color32(0, 0, 0, 255);
        RectTransform rt = ind.texto.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(48f, 0f);
        rt.offsetMax = Vector2.zero;

        ind.antes = Desafio.TotalPorAplicar;
        ind.Actualizar();
    }

    private void OnEnable() => Desafio.AlCambiarInventario += Actualizar;
    private void OnDisable() => Desafio.AlCambiarInventario -= Actualizar;

    private void Actualizar()
    {
        if (texto == null) return;
        int n = Desafio.TotalPorAplicar;
        Equipo.Objeto? primero = Desafio.ObjetosMejora.Where(o => Desafio.PorAplicar(o) > 0).Select(o => (Equipo.Objeto?)o).FirstOrDefault();
        icono.sprite = primero.HasValue ? RecursosRPG.Get().Icono(Equipo.ClaveIcono(primero.Value)) : null;
        icono.enabled = icono.sprite != null;
        texto.text = $"<b>{n}</b> por aplicar  <color=#b8b0a6>·  hoguera</color>";
        grupo.alpha = n > 0 ? 0.8f : 0f;
        if (n > antes && isActiveAndEnabled) { StopAllCoroutines(); StartCoroutine(Latido()); }
        antes = n;
    }

    private IEnumerator Latido()
    {
        for (float t = 0f; t < 0.6f; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.Sin(t / 0.6f * Mathf.PI);
            caja.localScale = Vector3.one * (1f + 0.12f * k);
            grupo.alpha = 0.8f + 0.2f * k;
            yield return null;
        }
        caja.localScale = Vector3.one;
        grupo.alpha = Desafio.TotalPorAplicar > 0 ? 0.8f : 0f;
    }
}
