using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Sonidos de los menus, iguales en todos:
//   - Mover: al pasar por una opcion (raton o teclado). Muy suave.
//   - Confirmar: al elegirla. Algo mas presente, y distinto, para que se note la
//     diferencia entre "estoy mirando" y "elegi esto".
//   - Cancelar / Error / Abrir.
// Los sonidos y su volumen estan en Resources/RecursosRPG (claves menu_*).
public static class SonidoMenu
{
    private static float silencioHasta;

    // Al abrir un menu se selecciona la primera opcion sola: eso no debe sonar.
    public static void Silenciar(float segundos = 0.12f) => silencioHasta = Time.unscaledTime + segundos;

    public static void Mover()
    {
        if (Time.unscaledTime < silencioHasta) return;
        Sonido.Reproducir("menu_mover");
    }

    public static void Confirmar() => Sonido.Reproducir("menu_confirmar");
    public static void Cancelar() => Sonido.Reproducir("menu_cancelar", 0.6f);
    public static void Error() => Sonido.Reproducir("menu_error", 0.6f);
    public static void Abrir() => Sonido.Reproducir("menu_abrir", 0.6f);
}

// Piezas comunes de los menus del juego (pausa, hoguera, mejoras...), para que
// todos se vean igual: panel oscuro con un filo dorado fino, titulo con adornos,
// opciones con una franja y una marca a la izquierda al elegirlas.
public static class EstiloMenu
{
    public static readonly Color Velo = new Color(0.01f, 0.01f, 0.015f, 0.78f);
    public static readonly Color Fondo = new Color(0.055f, 0.045f, 0.05f, 0.96f);
    public static readonly Color Filo = new Color(0.72f, 0.58f, 0.36f, 0.85f);
    public static readonly Color FiloTenue = new Color(0.72f, 0.58f, 0.36f, 0.25f);
    public static readonly Color Titulo = new Color(0.96f, 0.86f, 0.66f, 1f);
    public static readonly Color TextoNormal = new Color(0.78f, 0.74f, 0.7f, 1f);
    public static readonly Color TextoElegido = new Color(1f, 0.95f, 0.86f, 1f);
    public static readonly Color TextoApagado = new Color(0.5f, 0.48f, 0.46f, 1f);
    public static readonly Color Franja = new Color(0.75f, 0.55f, 0.3f, 0.16f);
    public static readonly Color Bueno = new Color(0.62f, 0.88f, 0.62f, 1f);
    public static readonly Color Malo = new Color(0.9f, 0.52f, 0.46f, 1f);

    // ------------------------------------------------------------------ Lienzo

    public static CanvasGroup Lienzo(GameObject go, int orden)
    {
        Canvas c = go.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = orden;
        CanvasScaler cs = go.AddComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920, 1080);
        cs.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        if (Object.FindFirstObjectByType<EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem", typeof(EventSystem));
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }
        CanvasGroup g = go.AddComponent<CanvasGroup>();
        g.alpha = 0f;
        g.interactable = false;
        g.blocksRaycasts = false;
        Image velo = Caja("Velo", go.transform, Velo);
        Estirar(velo.rectTransform);
        return g;
    }

    // Panel centrado con filo dorado y titulo. Devuelve el interior.
    public static RectTransform Panel(Transform padre, string titulo, Vector2 tamano, Vector2 posicion = default)
    {
        Image filo = Caja("Panel", padre, Filo);
        RectTransform rf = filo.rectTransform;
        rf.anchorMin = rf.anchorMax = new Vector2(0.5f, 0.5f);
        rf.sizeDelta = tamano;
        rf.anchoredPosition = posicion;

        Image fondo = Caja("Fondo", filo.transform, Fondo);
        Estirar(fondo.rectTransform, 2f);
        RectTransform interior = fondo.rectTransform;
        if (!string.IsNullOrEmpty(titulo))
        {
            TextMeshProUGUI t = Texto(titulo, interior, 46, Titulo);
            t.fontStyle = FontStyles.Bold;
            t.characterSpacing = 14f;
            Arriba(t.rectTransform, -26f, 60f);
            Separador(interior, -96f, 0.72f);
        }
        return interior;
    }

    // Linea fina horizontal con un rombo en el centro.
    public static void Separador(RectTransform padre, float y, float ancho = 0.8f)
    {
        Image l = Caja("Separador", padre, FiloTenue);
        RectTransform r = l.rectTransform;
        r.anchorMin = new Vector2(0.5f - ancho * 0.5f, 1f);
        r.anchorMax = new Vector2(0.5f + ancho * 0.5f, 1f);
        r.sizeDelta = new Vector2(0f, 2f);
        r.anchoredPosition = new Vector2(0f, y);
        Image rombo = Caja("Rombo", l.transform, Filo);
        rombo.rectTransform.anchorMin = rombo.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rombo.rectTransform.sizeDelta = new Vector2(9f, 9f);
        rombo.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
    }

    // ------------------------------------------------------------------ Opciones

    // Una opcion: franja y marca al elegirla, sonido suave al pasar y otro al
    // confirmar. "icono" es opcional.
    public static Button Opcion(Transform padre, string texto, UnityEngine.Events.UnityAction accion, float alto = 58f,
                                float tamano = 30f, Sprite icono = null, TextAlignmentOptions alineado = TextAlignmentOptions.Center)
    {
        GameObject go = new GameObject("Opcion_" + texto, typeof(RectTransform));
        go.transform.SetParent(padre, false);
        ((RectTransform)go.transform).sizeDelta = new Vector2(0f, alto);
        LayoutElement le = go.AddComponent<LayoutElement>();
        le.preferredHeight = alto;
        le.minHeight = alto;

        Image fondo = go.AddComponent<Image>();
        fondo.color = new Color(0f, 0f, 0f, 0.001f);
        Button b = go.AddComponent<Button>();
        b.transition = Selectable.Transition.None;
        Navigation nav = b.navigation;
        nav.mode = Navigation.Mode.Automatic;
        b.navigation = nav;

        Image franja = Caja("Franja", go.transform, Franja);
        Estirar(franja.rectTransform);
        franja.enabled = false;
        Image marca = Caja("Marca", go.transform, Filo);
        marca.rectTransform.anchorMin = new Vector2(0f, 0.2f);
        marca.rectTransform.anchorMax = new Vector2(0f, 0.8f);
        marca.rectTransform.sizeDelta = new Vector2(4f, 0f);
        marca.rectTransform.anchoredPosition = new Vector2(2f, 0f);
        marca.enabled = false;

        float izq = 24f;
        if (icono != null)
        {
            Image ico = Caja("Icono", go.transform, Color.white);
            ico.sprite = icono;
            ico.preserveAspect = true;
            ico.rectTransform.anchorMin = ico.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            ico.rectTransform.sizeDelta = new Vector2(alto * 0.66f, alto * 0.66f);
            ico.rectTransform.anchoredPosition = new Vector2(izq + alto * 0.33f, 0f);
            izq += alto * 0.66f + 16f;
        }

        TextMeshProUGUI t = Texto(texto, go.transform, tamano, TextoNormal);
        t.alignment = alineado;
        t.rectTransform.anchorMin = Vector2.zero;
        t.rectTransform.anchorMax = Vector2.one;
        t.rectTransform.offsetMin = new Vector2(alineado == TextAlignmentOptions.Center ? 0f : izq, 0f);
        t.rectTransform.offsetMax = new Vector2(alineado == TextAlignmentOptions.Center ? 0f : -20f, 0f);

        OpcionEstilo o = go.AddComponent<OpcionEstilo>();
        o.franja = franja;
        o.marca = marca;
        o.texto = t;
        b.onClick.AddListener(() => { if (o.Activa) SonidoMenu.Confirmar(); accion?.Invoke(); });
        return b;
    }

    // Mancha de luz suave y redonda (halos, brillos). Se genera una vez.
    private static Sprite resplandor;
    public static Sprite Resplandor()
    {
        if (resplandor != null) return resplandor;
        const int n = 64;
        Texture2D t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n * 0.5f, n * 0.5f)) / (n * 0.5f);
            t.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Pow(Mathf.Clamp01(1f - d), 2f)));
        }
        t.Apply();
        resplandor = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
        return resplandor;
    }

    // ------------------------------------------------------------------ Piezas

    public static Image Caja(string nombre, Transform padre, Color color)
    {
        Image i = new GameObject(nombre, typeof(RectTransform)).AddComponent<Image>();
        i.transform.SetParent(padre, false);
        i.color = color;
        i.raycastTarget = false;
        return i;
    }

    public static TextMeshProUGUI Texto(string texto, Transform padre, float tamano, Color color)
    {
        TextMeshProUGUI t = new GameObject("Texto", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        t.transform.SetParent(padre, false);
        t.text = texto;
        t.fontSize = tamano;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.enableWordWrapping = false;
        t.raycastTarget = false;
        return t;
    }

    public static void Estirar(RectTransform r, float margen = 0f)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = new Vector2(margen, margen);
        r.offsetMax = new Vector2(-margen, -margen);
    }

    // Franja horizontal pegada arriba del padre.
    public static void Arriba(RectTransform r, float y, float alto, float margen = 0f)
    {
        r.anchorMin = new Vector2(0f, 1f);
        r.anchorMax = new Vector2(1f, 1f);
        r.pivot = new Vector2(0.5f, 1f);
        r.offsetMin = new Vector2(margen, 0f);
        r.offsetMax = new Vector2(-margen, 0f);
        r.sizeDelta = new Vector2(r.sizeDelta.x, alto);
        r.anchoredPosition = new Vector2(0f, y);
    }

    // Columna con las opciones, en una zona del panel (margenes en pixeles).
    public static VerticalLayoutGroup Columna(Transform padre, float izq, float der, float arriba, float abajo, float espacio = 6f)
    {
        VerticalLayoutGroup vl = new GameObject("Columna", typeof(RectTransform)).AddComponent<VerticalLayoutGroup>();
        vl.transform.SetParent(padre, false);
        RectTransform r = (RectTransform)vl.transform;
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = new Vector2(izq, abajo);
        r.offsetMax = new Vector2(-der, -arriba);
        vl.spacing = espacio;
        vl.childForceExpandHeight = false;
        vl.childForceExpandWidth = true;
        vl.childControlHeight = true;
        vl.childControlWidth = true;
        vl.childAlignment = TextAnchor.UpperCenter;
        return vl;
    }
}

// Aspecto de una opcion elegida (franja, marca y texto claro). Pasar el raton
// por encima la elige, igual que moverse con el teclado o el mando.
public class OpcionEstilo : MonoBehaviour, IPointerEnterHandler, ISelectHandler, IDeselectHandler
{
    public Image franja, marca;
    public TextMeshProUGUI texto;
    // Apagada (sin almas, al maximo...): se ve gris, pero se puede elegir.
    public bool Activa { get; private set; } = true;
    private bool elegida;

    public void PonerActiva(bool activa)
    {
        Activa = activa;
        Pintar();
    }

    public void OnPointerEnter(PointerEventData e)
    {
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != gameObject)
            EventSystem.current.SetSelectedGameObject(gameObject);
    }

    public void OnSelect(BaseEventData e)
    {
        elegida = true;
        Pintar();
        SonidoMenu.Mover();
    }

    public void OnDeselect(BaseEventData e)
    {
        elegida = false;
        Pintar();
    }

    private void Pintar()
    {
        if (franja != null) franja.enabled = elegida;
        if (marca != null) marca.enabled = elegida;
        if (texto != null)
            texto.color = !Activa ? (elegida ? EstiloMenu.TextoNormal : EstiloMenu.TextoApagado)
                                  : elegida ? EstiloMenu.TextoElegido : EstiloMenu.TextoNormal;
    }
}
