using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Aviso de objetos de mejora sin aplicar ("2 por aplicar · hoguera"), en todos
// los niveles. Va abajo a la derecha, encima de los frascos (que estan encima
// de las almas): AjustesInterfaz. Sin objetos pendientes no se ve.
//   - Late un momento al llegar uno nuevo.
//   - Cerca de una hoguera encendida, y con algo pendiente, late despacio para
//     recordar que se puede aplicar ahi (radio, ritmo e intensidad en
//     AjustesInterfaz). Sin sonido.
public class IndicadorInventario : MonoBehaviour
{
    private static IndicadorInventario instancia;
    private CanvasGroup grupo;
    private RectTransform caja;
    private Image icono, halo;
    private TextMeshProUGUI texto;
    private int antes;
    private bool latidoNuevo;
    private float fase, siguienteBusqueda;
    private Hoguera[] hogueras = new Hoguera[0];
    private PlayerControler player;

    public static void Asegurar()
    {
        if (instancia != null) return;
        GameObject go = new GameObject("IndicadorInventario");
        Canvas c = go.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 41;
        CanvasScaler cs = go.AddComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920, 1080);
        cs.matchWidthOrHeight = 0.5f;
        IndicadorInventario ind = go.AddComponent<IndicadorInventario>();
        instancia = ind;
        ind.grupo = go.AddComponent<CanvasGroup>();
        ind.grupo.blocksRaycasts = false;
        ind.grupo.interactable = false;
        AjustesInterfaz ai = AjustesInterfaz.Get();

        ind.caja = new GameObject("Caja", typeof(RectTransform)).GetComponent<RectTransform>();
        ind.caja.SetParent(go.transform, false);
        // Abajo a la derecha, encima de los frascos (pegado al borde derecho).
        ind.caja.anchorMin = ind.caja.anchorMax = new Vector2(1f, 0f);
        ind.caja.pivot = new Vector2(1f, 0.5f);
        ind.caja.anchoredPosition = new Vector2(-ai.margen, ai.alturaAvisoColumna + 24f);
        ind.caja.sizeDelta = new Vector2(ai.anchoAviso, 48f);

        ind.halo = EstiloMenu.Caja("Halo", ind.caja, new Color(1f, 0.8f, 0.45f, 0f));
        ind.halo.sprite = EstiloMenu.Resplandor();
        RectTransform rh = ind.halo.rectTransform;
        rh.anchorMin = rh.anchorMax = new Vector2(0f, 0.5f);
        rh.sizeDelta = new Vector2(150f, 90f);
        rh.anchoredPosition = new Vector2(20f, 0f);

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

        ind.antes = Inventario.Total;
        ind.Actualizar();
    }

    private void OnEnable() => Inventario.AlCambiar += Actualizar;
    private void OnDisable() => Inventario.AlCambiar -= Actualizar;
    private void OnDestroy() { if (instancia == this) instancia = null; }

    private void Actualizar()
    {
        if (texto == null) return;
        int n = Inventario.Total;
        Equipo.Objeto? primero = Inventario.Objetos.Where(o => Inventario.PorAplicar(o) > 0).Select(o => (Equipo.Objeto?)o).FirstOrDefault();
        icono.sprite = primero.HasValue ? RecursosRPG.Get().Icono(Equipo.ClaveIcono(primero.Value)) : null;
        icono.enabled = icono.sprite != null;
        texto.text = $"<b>{n}</b> por aplicar  <color=#b8b0a6>·  hoguera</color>";
        if (n > antes && isActiveAndEnabled) { StopAllCoroutines(); StartCoroutine(LatidoNuevo()); }
        antes = n;
    }

    // Hay una hoguera encendida cerca del player.
    public bool CercaDeHoguera { get; private set; }

    private void Update()
    {
        if (texto == null) return;
        int n = Inventario.Total;
        AjustesInterfaz ai = AjustesInterfaz.Get();
        if (Time.unscaledTime >= siguienteBusqueda)
        {
            siguienteBusqueda = Time.unscaledTime + 0.5f;
            hogueras = FindObjectsByType<Hoguera>(FindObjectsSortMode.None);
            if (player == null) player = FindFirstObjectByType<PlayerControler>();
        }
        CercaDeHoguera = n > 0 && player != null && hogueras.Any(h => h != null && h.isActiveAndEnabled && h.Encendida
                         && Vector2.Distance(h.transform.position, player.transform.position) <= ai.radioLatido);
        if (latidoNuevo) return;

        // Latido suave cerca de la hoguera: algo mas grande y brillante, y vuelta.
        float k = 0f;
        if (CercaDeHoguera)
        {
            fase += Time.unscaledDeltaTime * ai.velocidadLatido;
            k = 0.5f - 0.5f * Mathf.Cos(fase * 2f * Mathf.PI);
        }
        else fase = 0f;
        caja.localScale = Vector3.one * (1f + ai.intensidadLatido * k);
        halo.color = new Color(1f, 0.8f, 0.45f, 0.35f * k);
        grupo.alpha = n > 0 ? 0.82f + 0.18f * k : 0f;
    }

    private IEnumerator LatidoNuevo()
    {
        latidoNuevo = true;
        for (float t = 0f; t < 0.6f; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.Sin(t / 0.6f * Mathf.PI);
            caja.localScale = Vector3.one * (1f + 0.12f * k);
            grupo.alpha = 0.82f + 0.18f * k;
            yield return null;
        }
        caja.localScale = Vector3.one;
        latidoNuevo = false;
    }
}
