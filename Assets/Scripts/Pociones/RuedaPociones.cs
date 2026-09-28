using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Rueda de pociones: sale al mantener la Q (como la de imbuir con la E). Dos
// huecos, vida a la izquierda y mana a la derecha. Se elige apuntando con el
// raton, con A/D (o el stick) o con 1/2, y al soltar la Q (o con clic) se bebe la
// elegida, que ademas queda como la de la Q. Clic derecho o Escape cancelan.
// Mientras esta abierta el juego va a camara lenta.
public class RuedaPociones : MonoBehaviour
{
    private const float Separacion = 150f;
    private const float CamaraLenta = 0.2f;

    private static RuedaPociones instancia;
    public static bool Abierta => instancia != null && (instancia.abierta || instancia.cerradaEnFrame == Time.frameCount);
    private int cerradaEnFrame = -1;

    private CanvasGroup grupo;
    private RectTransform centro;
    private readonly Image[] fondos = new Image[2];
    private readonly Image[] iconos = new Image[2];
    private readonly TextMeshProUGUI[] cargas = new TextMeshProUGUI[2];
    private TextMeshProUGUI nombre, efecto;

    private bool abierta;
    private int elegido;
    private float escalaPrevia = 1f;
    private float abiertaDesde;
    private Action<ReservaPociones.Tipo> alElegir;

    private static readonly Color[] Colores = { new Color(0.85f, 0.2f, 0.25f), new Color(0.3f, 0.55f, 1f) };

    public static void Abrir(Action<ReservaPociones.Tipo> alElegir)
    {
        if (instancia == null) instancia = Crear();
        instancia.AbrirInterno(alElegir);
    }

    private void AbrirInterno(Action<ReservaPociones.Tipo> alElegir)
    {
        if (abierta) return;
        abierta = true;
        this.alElegir = alElegir;
        elegido = (int)ReservaPociones.Get().Elegida;
        abiertaDesde = Time.unscaledTime;
        escalaPrevia = Time.timeScale > 0.01f ? Time.timeScale : 1f;
        Time.timeScale = CamaraLenta;
        grupo.alpha = 0f;
        gameObject.SetActive(true);
        Sonido.Reproducir("menu_abrir", 0.7f);
        Refrescar();
    }

    private void Cerrar()
    {
        abierta = false;
        cerradaEnFrame = Time.frameCount;
        if (GameManager.Instance == null || !GameManager.Instance.IsPaused) Time.timeScale = escalaPrevia;
        gameObject.SetActive(false);
    }

    private void Update()
    {
        if (!abierta) return;
        if (GameManager.Instance != null && GameManager.Instance.IsPaused) { abierta = false; gameObject.SetActive(false); return; }

        grupo.alpha = Mathf.MoveTowards(grupo.alpha, 1f, Time.unscaledDeltaTime * 8f);
        centro.localScale = Vector3.one * Mathf.Lerp(0.85f, 1f, grupo.alpha);

        int antes = elegido;
        LeerSeleccion();
        if (elegido != antes) { Sonido.Reproducir("menu_mover", 0.5f); Refrescar(); }

        Keyboard k = Keyboard.current;
        Mouse m = Mouse.current;
        Gamepad g = Gamepad.current;
        bool qSuelta = (k == null || !k.qKey.isPressed) && (g == null || !g.buttonNorth.isPressed);
        bool listo = Time.unscaledTime - abiertaDesde > 0.1f;
        bool confirmar = listo && (qSuelta || (m != null && m.leftButton.wasPressedThisFrame) || (g != null && g.buttonSouth.wasPressedThisFrame)
                                   || (k != null && (k.enterKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame)));
        bool cancelar = (k != null && k.escapeKey.wasPressedThisFrame) || (m != null && m.rightButton.wasPressedThisFrame)
                        || (g != null && g.buttonEast.wasPressedThisFrame);

        if (cancelar) { Sonido.Reproducir("menu_cancelar", 0.6f); Cerrar(); return; }
        if (!confirmar) return;
        ReservaPociones.Tipo t = (ReservaPociones.Tipo)elegido;
        Cerrar();
        alElegir?.Invoke(t);
    }

    private void LeerSeleccion()
    {
        Keyboard k = Keyboard.current;
        if (k != null)
        {
            if (k.digit1Key.wasPressedThisFrame || k.aKey.isPressed || k.leftArrowKey.isPressed) { elegido = 0; return; }
            if (k.digit2Key.wasPressedThisFrame || k.dKey.isPressed || k.rightArrowKey.isPressed) { elegido = 1; return; }
        }
        Gamepad g = Gamepad.current;
        if (g != null && Mathf.Abs(g.leftStick.ReadValue().x) > 0.5f) { elegido = g.leftStick.ReadValue().x < 0f ? 0 : 1; return; }
        Mouse m = Mouse.current;
        if (m != null && m.delta.ReadValue().sqrMagnitude > 0.5f)
        {
            float dx = m.position.ReadValue().x - Screen.width * 0.5f;
            if (Mathf.Abs(dx) > Screen.height * 0.04f) elegido = dx < 0f ? 0 : 1;
        }
    }

    private void Refrescar()
    {
        ReservaPociones r = ReservaPociones.Get();
        for (int i = 0; i < 2; i++)
        {
            bool sel = i == elegido;
            Color c = Colores[i];
            fondos[i].color = sel ? new Color(c.r * 0.5f, c.g * 0.5f, c.b * 0.5f, 0.95f) : new Color(0.08f, 0.07f, 0.09f, 0.85f);
            fondos[i].rectTransform.localScale = Vector3.one * (sel ? 1.18f : 1f);
            ReservaPociones.Tipo t = (ReservaPociones.Tipo)i;
            int n = r.CargasDe(t);
            cargas[i].text = $"{n}/{r.MaximoDe(t)}";
            iconos[i].color = n > 0 ? Color.white : new Color(0.45f, 0.45f, 0.45f, 0.8f);
        }
        ReservaPociones.Tipo e = (ReservaPociones.Tipo)elegido;
        nombre.text = e == ReservaPociones.Tipo.Vida ? "Frasco de sangre" : "Frasco de maná";
        nombre.color = Color.Lerp(Colores[elegido], Color.white, 0.4f);
        efecto.text = e == ReservaPociones.Tipo.Vida
            ? $"Cura el {Mathf.RoundToInt(r.FraccionCuracion * 100f)} % de la vida"
            : $"Devuelve el {Mathf.RoundToInt(r.FraccionMana * 100f)} % del maná";
    }

    // ------------------------------------------------------------------ Construccion

    private static RuedaPociones Crear()
    {
        GameObject go = new GameObject("RuedaPociones");
        Canvas c = go.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 80;
        CanvasScaler cs = go.AddComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920, 1080);
        cs.matchWidthOrHeight = 0.5f;
        RuedaPociones r = go.AddComponent<RuedaPociones>();
        r.grupo = go.AddComponent<CanvasGroup>();
        r.grupo.blocksRaycasts = false;

        Image velo = Caja("Velo", go.transform, new Color(0f, 0.02f, 0.05f, 0.45f));
        velo.rectTransform.anchorMin = Vector2.zero;
        velo.rectTransform.anchorMax = Vector2.one;
        velo.rectTransform.offsetMin = velo.rectTransform.offsetMax = Vector2.zero;

        r.centro = new GameObject("Centro").AddComponent<RectTransform>();
        r.centro.SetParent(go.transform, false);
        r.centro.anchorMin = r.centro.anchorMax = new Vector2(0.5f, 0.5f);
        r.centro.sizeDelta = Vector2.zero;

        Image disco = Caja("Disco", r.centro, new Color(0.03f, 0.03f, 0.05f, 0.75f));
        disco.sprite = RuedaImbuir.Circulo();
        disco.rectTransform.sizeDelta = new Vector2(Separacion * 3.4f, Separacion * 3.4f);

        RecursosRPG rec = RecursosRPG.Get();
        Sprite[] sprites = { rec.Icono("pocion") ?? ReservaPociones.Frasco(), rec.Icono("pocion_mana") };
        for (int i = 0; i < 2; i++)
        {
            Image f = Caja(i == 0 ? "Vida" : "Mana", r.centro, Color.black);
            f.sprite = RuedaImbuir.Circulo();
            f.rectTransform.sizeDelta = new Vector2(140f, 140f);
            f.rectTransform.anchoredPosition = new Vector2(i == 0 ? -Separacion : Separacion, 10f);
            r.fondos[i] = f;

            Image borde = Caja("Borde", f.transform, Colores[i]);
            borde.sprite = RuedaImbuir.Anillo();
            borde.rectTransform.anchorMin = Vector2.zero;
            borde.rectTransform.anchorMax = Vector2.one;
            borde.rectTransform.offsetMin = borde.rectTransform.offsetMax = Vector2.zero;

            Image ico = Caja("Icono", f.transform, Color.white);
            ico.sprite = sprites[i];
            ico.enabled = ico.sprite != null;
            ico.preserveAspect = true;
            ico.rectTransform.sizeDelta = new Vector2(80f, 80f);
            r.iconos[i] = ico;

            r.cargas[i] = Texto("", f.transform, 24, Color.white);
            r.cargas[i].rectTransform.anchoredPosition = new Vector2(0f, -88f);

            TextMeshProUGUI num = Texto((i + 1).ToString(), f.transform, 20, new Color(1f, 1f, 1f, 0.6f));
            num.rectTransform.anchoredPosition = new Vector2(0f, 56f);
        }

        r.nombre = Texto("", r.centro, 36, Color.white);
        r.nombre.fontStyle = FontStyles.Bold;
        r.nombre.rectTransform.anchoredPosition = new Vector2(0f, -Separacion * 1.15f);
        r.efecto = Texto("", r.centro, 22, new Color(0.85f, 0.82f, 0.78f));
        r.efecto.rectTransform.anchoredPosition = new Vector2(0f, -Separacion * 1.15f - 38f);

        TextMeshProUGUI ayuda = Texto("Suelta Q / clic: beber     Clic derecho / Esc: cancelar", go.transform, 22, new Color(0.8f, 0.8f, 0.8f, 0.7f));
        ayuda.rectTransform.anchorMin = ayuda.rectTransform.anchorMax = new Vector2(0.5f, 0.07f);
        ayuda.rectTransform.sizeDelta = new Vector2(1200f, 40f);

        go.SetActive(false);
        return r;
    }

    private static Image Caja(string nombre, Transform padre, Color color)
    {
        Image i = new GameObject(nombre).AddComponent<Image>();
        i.transform.SetParent(padre, false);
        i.color = color;
        i.raycastTarget = false;
        return i;
    }

    private static TextMeshProUGUI Texto(string texto, Transform padre, float tamano, Color color)
    {
        TextMeshProUGUI t = new GameObject("Texto").AddComponent<TextMeshProUGUI>();
        t.transform.SetParent(padre, false);
        t.text = texto;
        t.fontSize = tamano;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.enableWordWrapping = false;
        t.raycastTarget = false;
        t.rectTransform.sizeDelta = new Vector2(420f, 44f);
        t.outlineWidth = 0.15f;
        t.outlineColor = new Color32(0, 0, 0, 255);
        return t;
    }
}
