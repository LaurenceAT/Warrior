using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Rueda radial para imbuir la espada (tecla E). Mientras esta abierta el juego va
// a camara lenta. Se elige apuntando con el raton, con la direccion (WASD /
// flechas / stick) o con las teclas 1-5; se confirma con clic, E, Enter o el
// boton sur del mando, y se cancela con clic derecho, Escape o el boton este.
public class RuedaImbuir : MonoBehaviour
{
    private const float Radio = 190f;
    private const float CamaraLenta = 0.2f;

    private static RuedaImbuir instancia;
    public static bool Abierta => instancia != null && (instancia.abierta || instancia.cerradaEnFrame == Time.frameCount);
    private int cerradaEnFrame = -1;
    public static float UltimoCierre { get; private set; } = -10f;

    private CanvasGroup grupo;
    private RectTransform centro;
    private readonly Image[] fondos = new Image[Elementos.Cantidad];
    private readonly Image[] iconos = new Image[Elementos.Cantidad];
    private readonly RectTransform[] ranuras = new RectTransform[Elementos.Cantidad];
    private TextMeshProUGUI nombre, efecto, coste, aviso;
    private Image anilloActivo;

    private bool abierta;
    private int elegido = -1;
    private float escalaPrevia = 1f;
    private float abiertaDesde;
    private Action<Elemento> alElegir;
    private Func<string> impedimento;
    private Elemento actual;

    // Abre la rueda. "impedimento" dice por que no se puede imbuir ahora (o null).
    public static void Abrir(Elemento actual, Func<string> impedimento, Action<Elemento> alElegir)
    {
        if (instancia == null) instancia = Crear();
        instancia.AbrirInterno(actual, impedimento, alElegir);
    }

    public static void Cerrar()
    {
        if (instancia != null && instancia.abierta) instancia.CerrarInterno();
    }

    private void AbrirInterno(Elemento actual, Func<string> impedimento, Action<Elemento> alElegir)
    {
        if (abierta) return;
        abierta = true;
        this.actual = actual;
        this.impedimento = impedimento;
        this.alElegir = alElegir;
        elegido = actual != Elemento.Ninguno ? (int)actual : -1;
        abiertaDesde = Time.unscaledTime;
        escalaPrevia = Time.timeScale > 0.01f ? Time.timeScale : 1f;
        Time.timeScale = CamaraLenta;
        grupo.alpha = 0f;
        gameObject.SetActive(true);
        Sonido.Reproducir("menu_abrir", 0.7f);
        Refrescar();
    }

    private void CerrarInterno()
    {
        abierta = false;
        cerradaEnFrame = Time.frameCount;
        UltimoCierre = Time.unscaledTime;
        if (GameManager.Instance == null || !GameManager.Instance.IsPaused) Time.timeScale = escalaPrevia;
        gameObject.SetActive(false);
    }

    private void Update()
    {
        if (!abierta) return;
        // Si se pausa el juego con la rueda abierta, se cierra sin elegir.
        if (GameManager.Instance != null && GameManager.Instance.IsPaused) { abierta = false; gameObject.SetActive(false); return; }

        grupo.alpha = Mathf.MoveTowards(grupo.alpha, 1f, Time.unscaledDeltaTime * 8f);
        centro.localScale = Vector3.one * Mathf.Lerp(0.85f, 1f, grupo.alpha);

        int antes = elegido;
        LeerSeleccion();
        if (elegido != antes) { Sonido.Reproducir("menu_mover", 0.5f); Refrescar(); }

        // Pulsar la E al abrir no debe confirmar en el mismo fotograma.
        bool listo = Time.unscaledTime - abiertaDesde > 0.12f;
        Keyboard k = Keyboard.current;
        Mouse m = Mouse.current;
        Gamepad g = Gamepad.current;
        bool confirmar = listo && ((k != null && (k.eKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame))
                                   || (m != null && m.leftButton.wasPressedThisFrame)
                                   || (g != null && g.buttonSouth.wasPressedThisFrame));
        bool cancelar = (k != null && k.escapeKey.wasPressedThisFrame) || (m != null && m.rightButton.wasPressedThisFrame)
                        || (g != null && g.buttonEast.wasPressedThisFrame);

        if (cancelar) { Sonido.Reproducir("menu_cancelar", 0.6f); CerrarInterno(); return; }
        if (!confirmar) return;

        if (elegido < 0) { CerrarInterno(); return; }
        string motivo = impedimento != null ? impedimento() : null;
        if (motivo != null)
        {
            aviso.text = motivo;
            aviso.color = new Color(1f, 0.45f, 0.4f);
            Sonido.Reproducir("menu_error", 0.7f);
            return;
        }
        Elemento e = Elementos.Todos[elegido];
        CerrarInterno();
        alElegir?.Invoke(e);
    }

    private void LeerSeleccion()
    {
        Keyboard k = Keyboard.current;
        if (k != null)
        {
            if (k.digit1Key.wasPressedThisFrame) { elegido = 0; return; }
            if (k.digit2Key.wasPressedThisFrame) { elegido = 1; return; }
            if (k.digit3Key.wasPressedThisFrame) { elegido = 2; return; }
            if (k.digit4Key.wasPressedThisFrame) { elegido = 3; return; }
            if (k.digit5Key.wasPressedThisFrame) { elegido = 4; return; }
        }

        Vector2 dir = Vector2.zero;
        if (k != null)
        {
            if (k.wKey.isPressed || k.upArrowKey.isPressed) dir.y += 1f;
            if (k.sKey.isPressed || k.downArrowKey.isPressed) dir.y -= 1f;
            if (k.dKey.isPressed || k.rightArrowKey.isPressed) dir.x += 1f;
            if (k.aKey.isPressed || k.leftArrowKey.isPressed) dir.x -= 1f;
        }
        Gamepad g = Gamepad.current;
        if (g != null && g.leftStick.ReadValue().sqrMagnitude > 0.25f) dir = g.leftStick.ReadValue();

        Mouse m = Mouse.current;
        if (dir == Vector2.zero && m != null && m.delta.ReadValue().sqrMagnitude > 0.5f)
        {
            Vector2 c = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Vector2 d = m.position.ReadValue() - c;
            if (d.magnitude > Screen.height * 0.06f) dir = d;
        }
        if (dir.sqrMagnitude < 0.01f) return;

        // La ranura mas cercana a esa direccion.
        float mejor = -2f;
        for (int i = 0; i < Elementos.Cantidad; i++)
        {
            float p = Vector2.Dot(dir.normalized, Direccion(i));
            if (p > mejor) { mejor = p; elegido = i; }
        }
    }

    private static Vector2 Direccion(int i)
    {
        // La primera arriba y en el sentido de las agujas del reloj.
        float a = (90f - i * 360f / Elementos.Cantidad) * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(a), Mathf.Sin(a));
    }

    private void Refrescar()
    {
        for (int i = 0; i < Elementos.Cantidad; i++)
        {
            Elemento e = Elementos.Todos[i];
            bool sel = i == elegido;
            Color c = Elementos.Color(e);
            fondos[i].color = sel ? new Color(c.r * 0.55f, c.g * 0.55f, c.b * 0.55f, 0.95f) : new Color(0.08f, 0.07f, 0.09f, 0.85f);
            ranuras[i].localScale = Vector3.one * (sel ? 1.18f : 1f);
            iconos[i].color = sel ? Color.white : new Color(0.8f, 0.8f, 0.8f, 0.85f);
        }
        anilloActivo.enabled = actual != Elemento.Ninguno;
        if (actual != Elemento.Ninguno)
        {
            anilloActivo.rectTransform.anchoredPosition = Direccion((int)actual) * Radio;
            anilloActivo.color = Elementos.Color(actual);
        }

        if (elegido < 0)
        {
            nombre.text = "Imbuir arma";
            efecto.text = "Elige un elemento";
            coste.text = "";
        }
        else
        {
            Elemento e = Elementos.Todos[elegido];
            nombre.text = Elementos.Nombre(e);
            nombre.color = Color.Lerp(Elementos.Color(e), Color.white, 0.35f);
            efecto.text = Elementos.Efecto(e);
            PlayerMana mana = FindFirstObjectByType<PlayerMana>();
            float cuesta = FindFirstObjectByType<ArmaImbuida>() is ArmaImbuida a ? a.CosteMana : 25f;
            coste.text = $"<color=#6fa8ff>{cuesta:0} maná</color>" + (mana != null ? $"  <size=80%><color=#999>({mana.Actual:0}/{mana.Maximo:0})</color></size>" : "");
        }
        string motivo = impedimento != null ? impedimento() : null;
        aviso.text = motivo ?? "";
        aviso.color = new Color(1f, 0.75f, 0.45f);
    }

    // ------------------------------------------------------------------ Construccion

    private static RuedaImbuir Crear()
    {
        GameObject go = new GameObject("RuedaImbuir");
        Canvas c = go.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 80;
        CanvasScaler cs = go.AddComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920, 1080);
        cs.matchWidthOrHeight = 0.5f;
        RuedaImbuir r = go.AddComponent<RuedaImbuir>();
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

        // Circulo de fondo.
        Image disco = Caja("Disco", r.centro, new Color(0.03f, 0.03f, 0.05f, 0.75f));
        disco.sprite = Circulo();
        disco.rectTransform.sizeDelta = new Vector2(Radio * 2.7f, Radio * 2.7f);

        r.anilloActivo = Caja("Activo", r.centro, Color.white);
        r.anilloActivo.sprite = Anillo();
        r.anilloActivo.rectTransform.sizeDelta = new Vector2(150f, 150f);

        RecursosRPG rec = RecursosRPG.Get();
        for (int i = 0; i < Elementos.Cantidad; i++)
        {
            Elemento e = Elementos.Todos[i];
            Image f = Caja("Ranura_" + e, r.centro, Color.black);
            f.sprite = Circulo();
            f.rectTransform.sizeDelta = new Vector2(122f, 122f);
            f.rectTransform.anchoredPosition = Direccion(i) * Radio;
            r.fondos[i] = f;
            r.ranuras[i] = f.rectTransform;

            Image borde = Caja("Borde", f.transform, Elementos.Color(e));
            borde.sprite = Anillo();
            borde.rectTransform.anchorMin = Vector2.zero;
            borde.rectTransform.anchorMax = Vector2.one;
            borde.rectTransform.offsetMin = borde.rectTransform.offsetMax = Vector2.zero;

            Image ico = Caja("Icono", f.transform, Color.white);
            ico.sprite = rec.Icono("elemento_" + (int)e);
            ico.preserveAspect = true;
            ico.enabled = ico.sprite != null;
            ico.rectTransform.sizeDelta = new Vector2(72f, 72f);
            r.iconos[i] = ico;

            TextMeshProUGUI num = Texto((i + 1).ToString(), f.transform, 20, new Color(1f, 1f, 1f, 0.6f));
            num.rectTransform.anchoredPosition = new Vector2(0f, -50f);
            num.rectTransform.sizeDelta = new Vector2(40f, 24f);
        }

        r.nombre = Texto("", r.centro, 38, Color.white);
        r.nombre.fontStyle = FontStyles.Bold;
        r.nombre.rectTransform.anchoredPosition = new Vector2(0f, 26f);
        r.efecto = Texto("", r.centro, 22, new Color(0.85f, 0.82f, 0.78f));
        r.efecto.rectTransform.anchoredPosition = new Vector2(0f, -12f);
        r.coste = Texto("", r.centro, 24, Color.white);
        r.coste.rectTransform.anchoredPosition = new Vector2(0f, -46f);
        r.aviso = Texto("", r.centro, 22, new Color(1f, 0.75f, 0.45f));
        r.aviso.rectTransform.anchoredPosition = new Vector2(0f, -Radio * 1.45f);

        TextMeshProUGUI ayuda = Texto("Clic / E: imbuir     Clic derecho / Esc: cancelar", go.transform, 22, new Color(0.8f, 0.8f, 0.8f, 0.7f));
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

    private static Sprite circulo, anillo;

    public static Sprite Circulo()
    {
        if (circulo == null) circulo = Disco(0f);
        return circulo;
    }

    public static Sprite Anillo()
    {
        if (anillo == null) anillo = Disco(0.86f);
        return anillo;
    }

    // Disco (o anillo, con hueco) suavizado, dibujado por codigo.
    private static Sprite Disco(float hueco)
    {
        const int n = 128;
        Texture2D t = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n * 0.5f, n * 0.5f)) / (n * 0.5f);
            float a = Mathf.Clamp01((1f - d) * n * 0.5f);
            if (hueco > 0f) a *= Mathf.Clamp01((d - hueco) * n * 0.5f);
            t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        t.Apply();
        return Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
    }
}
