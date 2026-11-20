using TMPro;
using UnityEngine;
using UnityEngine.UI;

// El boton de "volver" de la version de movil.
//
// En el PC se sale de un menu con Esc o con el clic derecho, y con el dedo no
// hay ninguna de las dos cosas. Este boton sale solo mientras hay un menu
// abierto y, al tocarlo, pide "atras": lo recoge CapasMenu.PulsadoAtras, que es
// por donde pasan todos los menus.
//
// Solo existe con ModoMovil.Activo, asi que en PC no cambia nada. Donde va y
// cuanto mide esta en Resources/AjustesTactiles.
public class BotonVolver : MonoBehaviour
{
    private static BotonVolver instancia;

    private CanvasGroup grupo;
    private float alfa;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Arranque()
    {
        if (!ModoMovil.Activo || instancia != null) return;
        GameObject go = new GameObject("BotonVolver");
        DontDestroyOnLoad(go);
        instancia = go.AddComponent<BotonVolver>();
        instancia.Construir();
    }

    // Lo encienden los menus que no se pueden mirar desde fuera (el menu
    // principal y los suyos: Desafios, Bestiario, Logros, controles).
    public static bool Pedido { get; set; }

    // Hay algun menu delante del que se pueda salir. Un menu nuevo que quiera
    // el boton se apunta aqui (o enciende Pedido).
    private static bool HayMenu()
    {
        if (Pedido) return true;
        if (GameManager.Instance != null && GameManager.Instance.IsPaused) return true;
        return MenuHoguera.Abierto || TiendaTotem.Abierta || CuadroPista.Abierto;
    }

    private void Construir()
    {
        AjustesTactiles aj = AjustesTactiles.Cargar();

        Canvas c = gameObject.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        // Por encima de todos los menus (el mas alto es la guia, en 150).
        c.sortingOrder = 200;
        CanvasScaler cs = gameObject.AddComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920f, 1080f);
        cs.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();
        grupo = gameObject.AddComponent<CanvasGroup>();
        grupo.alpha = 0f;
        grupo.blocksRaycasts = false;

        Image fondo = EstiloMenu.Caja("Volver", transform, aj.colorBoton);
        fondo.sprite = DibujosCazadora.Circulo();
        // EstiloMenu.Caja no recibe toques: este boton si tiene que recibirlos.
        fondo.raycastTarget = true;
        RectTransform r = fondo.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(0f, 0f);
        r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = new Vector2(aj.tamanoVolver, aj.tamanoVolver);
        r.anchoredPosition = aj.posicionVolver;

        Image filo = EstiloMenu.Caja("Filo", r, aj.colorFilo);
        filo.sprite = DibujosCazadora.Anillo();
        EstiloMenu.Estirar(filo.rectTransform);

        TextMeshProUGUI flecha = EstiloMenu.Texto("←", r, aj.tamanoVolver * 0.45f, aj.colorIcono);
        EstiloMenu.Estirar(flecha.rectTransform, 4f);

        Button b = fondo.gameObject.AddComponent<Button>();
        b.targetGraphic = fondo;
        b.onClick.AddListener(() => { SonidoMenu.Atras(); CapasMenu.PedirAtras(); });
    }

    private void Update()
    {
        bool quiero = ModoMovil.Activo && HayMenu();
        // Pedido es un latido: quien lo quiere lo enciende cada fotograma, asi
        // que al cerrarse ese menu (o al cambiar de escena) se apaga solo.
        Pedido = false;
        alfa = Mathf.MoveTowards(alfa, quiero ? 1f : 0f, Time.unscaledDeltaTime * 6f);
        grupo.alpha = alfa;
        grupo.blocksRaycasts = quiero;
    }
}
