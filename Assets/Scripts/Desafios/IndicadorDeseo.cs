using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Desafios: el objeto marcado como deseado en el totem, arriba a la derecha
// (AjustesInterfaz). Icono, nombre, precio y cuantas almas faltan, o
// "¡Puedes comprarlo!" con un brillo suave. Se ve al instante cuando cambian
// las almas. Se oculta en la tienda, en dialogos y escenas, y desaparece al
// comprarlo o quitar la marca. Dura lo que el desafio (como la lista de deseos).
public class IndicadorDeseo : MonoBehaviour
{
    private static IndicadorDeseo instancia;
    private CanvasGroup grupo;
    private Image icono, halo;
    private TextMeshProUGUI nombre, estado;
    private string mostrado;
    private int almasMostradas = -1;
    private float alfa;

    public static void Asegurar()
    {
        if (instancia != null) return;
        GameObject go = new GameObject("IndicadorDeseo");
        Canvas c = go.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 42;
        CanvasScaler cs = go.AddComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920, 1080);
        cs.matchWidthOrHeight = 0.5f;
        IndicadorDeseo d = go.AddComponent<IndicadorDeseo>();
        instancia = d;
        d.grupo = go.AddComponent<CanvasGroup>();
        d.grupo.alpha = 0f;
        d.grupo.blocksRaycasts = d.grupo.interactable = false;
        AjustesInterfaz ai = AjustesInterfaz.Get();

        Image caja = EstiloMenu.Caja("Caja", go.transform, new Color(0.03f, 0.03f, 0.05f, 0.7f));
        RectTransform rc = caja.rectTransform;
        rc.anchorMin = rc.anchorMax = new Vector2(1f, 1f);
        rc.pivot = new Vector2(1f, 1f);
        rc.anchoredPosition = new Vector2(-ai.margen, -ai.margenArribaDeseo);
        rc.sizeDelta = new Vector2(ai.anchoDeseo, 72f);
        Image borde = EstiloMenu.Caja("Borde", caja.transform, new Color(0.78f, 0.72f, 0.58f, 0.8f));
        borde.rectTransform.anchorMin = new Vector2(0f, 0f);
        borde.rectTransform.anchorMax = new Vector2(1f, 0f);
        borde.rectTransform.sizeDelta = new Vector2(0f, 2f);

        d.halo = EstiloMenu.Caja("Brillo", caja.transform, new Color(1f, 0.85f, 0.45f, 0f));
        d.halo.sprite = EstiloMenu.Resplandor();
        EstiloMenu.Estirar(d.halo.rectTransform, -12f);

        Image estrella = EstiloMenu.Caja("Estrella", caja.transform, Color.white);
        estrella.sprite = RecursosRPG.Get().Icono("estrella");
        estrella.preserveAspect = true;
        estrella.enabled = estrella.sprite != null;
        RectTransform re = estrella.rectTransform;
        re.anchorMin = re.anchorMax = new Vector2(0f, 1f);
        re.sizeDelta = new Vector2(20f, 20f);
        re.anchoredPosition = new Vector2(10f, -10f);

        d.icono = EstiloMenu.Caja("Icono", caja.transform, Color.white);
        d.icono.preserveAspect = true;
        RectTransform ri = d.icono.rectTransform;
        ri.anchorMin = ri.anchorMax = new Vector2(0f, 0.5f);
        ri.pivot = new Vector2(0f, 0.5f);
        ri.sizeDelta = new Vector2(52f, 52f);
        ri.anchoredPosition = new Vector2(14f, 0f);

        d.nombre = EstiloMenu.Texto("", caja.transform, 21, EstiloMenu.TextoElegido);
        d.nombre.alignment = TextAlignmentOptions.BottomLeft;
        RectTransform rn = d.nombre.rectTransform;
        rn.anchorMin = new Vector2(0f, 0.5f); rn.anchorMax = new Vector2(1f, 1f);
        rn.offsetMin = new Vector2(76f, 0f); rn.offsetMax = new Vector2(-10f, -6f);
        d.estado = EstiloMenu.Texto("", caja.transform, 19, EstiloMenu.TextoNormal);
        d.estado.alignment = TextAlignmentOptions.TopLeft;
        RectTransform rs = d.estado.rectTransform;
        rs.anchorMin = new Vector2(0f, 0f); rs.anchorMax = new Vector2(1f, 0.5f);
        rs.offsetMin = new Vector2(76f, 6f); rs.offsetMax = new Vector2(-10f, -2f);
    }

    private void OnDestroy() { if (instancia == this) instancia = null; }

    private FichaTienda.Articulo Deseado()
    {
        FichaTienda t = FichaTienda.Get();
        if (!Desafio.Activo || t == null || string.IsNullOrEmpty(Desafio.Deseado) || Desafio.Comprados.Contains(Desafio.Deseado)) return null;
        return t.articulos.FirstOrDefault(a => a.id == Desafio.Deseado);
    }

    private void Update()
    {
        FichaTienda.Articulo a = Deseado();
        bool oculto = a == null || TiendaTotem.Abierta || Cinematica.Activa || CuadroPista.Abierto || PantallaMuerte.Activa || PantallaDesafio.Abierta;
        alfa = Mathf.MoveTowards(alfa, oculto ? 0f : 1f, Time.unscaledDeltaTime * 4f);
        grupo.alpha = alfa;
        if (a == null) { mostrado = null; return; }

        int almas = Progreso.Almas, precio = Desafio.Precio(a);
        if (a.id != mostrado || almas != almasMostradas)
        {
            mostrado = a.id;
            almasMostradas = almas;
            icono.sprite = RecursosRPG.Get().Icono(Equipo.ClaveIcono(a.objeto));
            icono.enabled = icono.sprite != null;
            nombre.text = $"{a.nombre}  <size=80%><color=#f0d49a>{precio:N0}</color></size>";
            estado.text = almas >= precio ? "<color=#9fe0a0>¡Puedes comprarlo!</color>" : $"<color=#e0776c>Te faltan {precio - almas:N0}</color> almas";
        }
        // Brillo sutil cuando ya alcanza.
        float k = almas >= precio ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2.4f) : 0f;
        halo.color = new Color(1f, 0.85f, 0.45f, 0.22f * k);
    }
}
