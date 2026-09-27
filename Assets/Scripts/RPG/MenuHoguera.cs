using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Menu de la hoguera (se abre al descansar con la E). El juego se para mientras
// esta abierto. Arriba el nivel del personaje, las almas y lo que cuesta el
// siguiente nivel; debajo una fila por estadistica: pulsarla sube un nivel (si
// hay almas). "Levantarse" (o Escape) cierra.
public class MenuHoguera : MonoBehaviour
{
    private static MenuHoguera instancia;
    public static bool Abierto => instancia != null && (instancia.abierto || instancia.cerradoEnFrame == Time.frameCount);

    private CanvasGroup grupo;
    private TextMeshProUGUI textoNivel, textoAlmas, textoCoste, textoAviso;
    private readonly Fila[] filas = new Fila[Progreso.NumEstadisticas];
    private Button primero;
    private bool abierto;
    private int cerradoEnFrame = -1;
    // Momento (tiempo real) en que se cerro: la E que lo cierra no debe reabrirlo.
    public static float UltimoCierre { get; private set; } = -10f;
    private float escalaPrevia = 1f;
    private float abiertoDesde;

    private class Fila
    {
        public Progreso.Estadistica estadistica;
        public Button boton;
        public TextMeshProUGUI nivel, valor;
        public Image fondo;
    }

    public static void Abrir()
    {
        if (instancia == null) instancia = Crear();
        instancia.AbrirInterno();
    }

    private void AbrirInterno()
    {
        if (abierto) return;
        abierto = true;
        abiertoDesde = Time.unscaledTime;
        escalaPrevia = Time.timeScale > 0.01f ? Time.timeScale : 1f;
        Time.timeScale = 0f;
        gameObject.SetActive(true);
        textoAviso.text = "";
        Refrescar();
        StopAllCoroutines();
        StartCoroutine(Fundido(true));
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(primero.gameObject);
    }

    private void CerrarInterno()
    {
        if (!abierto) return;
        abierto = false;
        cerradoEnFrame = Time.frameCount;
        UltimoCierre = Time.unscaledTime;
        Time.timeScale = escalaPrevia;
        Sonido.Reproducir("menu_cancelar", 0.6f);
        StopAllCoroutines();
        StartCoroutine(Fundido(false));
    }

    private IEnumerator Fundido(bool mostrar)
    {
        grupo.interactable = mostrar;
        grupo.blocksRaycasts = mostrar;
        float desde = grupo.alpha, hasta = mostrar ? 1f : 0f;
        for (float t = 0f; t < 0.2f; t += Time.unscaledDeltaTime)
        {
            grupo.alpha = Mathf.Lerp(desde, hasta, t / 0.2f);
            yield return null;
        }
        grupo.alpha = hasta;
        if (!mostrar) gameObject.SetActive(false);
    }

    private void Update()
    {
        if (!abierto) return;
        Keyboard k = Keyboard.current;
        Gamepad g = Gamepad.current;
        if (Time.unscaledTime - abiertoDesde < 0.2f) return;
        if ((k != null && (k.escapeKey.wasPressedThisFrame || k.eKey.wasPressedThisFrame)) || (g != null && g.buttonEast.wasPressedThisFrame))
            CerrarInterno();

        // Sin raton ni teclado sobre nada: que siempre haya algo seleccionado.
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null)
            EventSystem.current.SetSelectedGameObject(primero.gameObject);
    }

    private void Subir(Progreso.Estadistica e)
    {
        if (Progreso.Nivel(e) >= Progreso.NivelMaximo)
        {
            textoAviso.text = "Esa estadística ya está al máximo";
            Sonido.Reproducir("menu_error", 0.7f);
            return;
        }
        if (!Progreso.Subir(e))
        {
            textoAviso.text = "No tienes almas suficientes";
            Sonido.Reproducir("menu_error", 0.7f);
            return;
        }
        textoAviso.text = $"{Progreso.Nombre(e)} sube a nivel {Progreso.Nivel(e)}";
        Sonido.Reproducir("subir_nivel");
        // El player aplica el nuevo maximo (vida, estamina, mana) y se cura.
        PlayerControler p = FindFirstObjectByType<PlayerControler>();
        if (p != null) p.AplicarProgreso(true);
        Refrescar();
        StartCoroutine(Pulso(filas[(int)e].fondo.rectTransform));
    }

    private IEnumerator Pulso(RectTransform r)
    {
        for (float t = 0f; t < 0.3f; t += Time.unscaledDeltaTime)
        {
            r.localScale = Vector3.one * (1f + 0.05f * Mathf.Sin(t / 0.3f * Mathf.PI));
            yield return null;
        }
        r.localScale = Vector3.one;
    }

    private void Refrescar()
    {
        textoNivel.text = $"Nivel <b>{Progreso.NivelTotal}</b>";
        textoAlmas.text = $"Almas  <b>{Progreso.Almas:N0}</b>";
        bool alcanza = Progreso.Almas >= Progreso.CosteSiguiente;
        textoCoste.text = $"Siguiente nivel: <color={(alcanza ? "#9fe0a0" : "#e08a80")}><b>{Progreso.CosteSiguiente:N0}</b></color> almas";

        foreach (Fila f in filas)
        {
            int n = Progreso.Nivel(f.estadistica);
            bool max = n >= Progreso.NivelMaximo;
            f.nivel.text = max ? "MÁX" : n.ToString();
            f.valor.text = max ? Progreso.Describir(f.estadistica, n)
                               : $"{Progreso.Describir(f.estadistica, n)}  <color=#888>→</color>  <color={(alcanza ? "#9fe0a0" : "#aaaaaa")}>{Progreso.Describir(f.estadistica, n + 1)}</color>";
        }
    }

    // ------------------------------------------------------------------ Construccion

    private static MenuHoguera Crear()
    {
        GameObject go = new GameObject("MenuHoguera");
        Canvas c = go.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 95;
        CanvasScaler cs = go.AddComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920, 1080);
        cs.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        if (FindFirstObjectByType<EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem", typeof(EventSystem));
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }

        MenuHoguera m = go.AddComponent<MenuHoguera>();
        m.grupo = go.AddComponent<CanvasGroup>();
        m.grupo.alpha = 0f;

        Image velo = Caja("Velo", go.transform, new Color(0.02f, 0.01f, 0.01f, 0.72f));
        Estirar(velo.rectTransform);

        Image panel = Caja("Panel", go.transform, new Color(0.07f, 0.05f, 0.05f, 0.94f));
        RectTransform rp = panel.rectTransform;
        rp.anchorMin = rp.anchorMax = new Vector2(0.5f, 0.5f);
        rp.sizeDelta = new Vector2(980f, 780f);
        Image linea = Caja("Linea", panel.transform, new Color(0.85f, 0.45f, 0.2f, 0.85f));
        linea.rectTransform.anchorMin = new Vector2(0f, 1f); linea.rectTransform.anchorMax = new Vector2(1f, 1f);
        linea.rectTransform.sizeDelta = new Vector2(0f, 3f); linea.rectTransform.anchoredPosition = new Vector2(0f, -110f);

        TextMeshProUGUI titulo = Texto("HOGUERA", panel.transform, 60, new Color(1f, 0.85f, 0.65f));
        titulo.fontStyle = FontStyles.Bold;
        titulo.characterSpacing = 16f;
        Arriba(titulo.rectTransform, -22f, 70f);

        m.textoNivel = Texto("", panel.transform, 30, new Color(0.92f, 0.88f, 0.84f));
        m.textoNivel.alignment = TextAlignmentOptions.Left;
        Arriba(m.textoNivel.rectTransform, -130f, 40f, 60f);
        m.textoAlmas = Texto("", panel.transform, 30, new Color(0.78f, 0.9f, 1f));
        m.textoAlmas.alignment = TextAlignmentOptions.Right;
        Arriba(m.textoAlmas.rectTransform, -130f, 40f, 60f);
        m.textoCoste = Texto("", panel.transform, 24, new Color(0.8f, 0.76f, 0.72f));
        Arriba(m.textoCoste.rectTransform, -176f, 34f, 60f);

        VerticalLayoutGroup vl = new GameObject("Estadisticas").AddComponent<VerticalLayoutGroup>();
        vl.transform.SetParent(panel.transform, false);
        RectTransform rv = (RectTransform)vl.transform;
        rv.anchorMin = new Vector2(0f, 0f); rv.anchorMax = new Vector2(1f, 1f);
        rv.offsetMin = new Vector2(50f, 150f); rv.offsetMax = new Vector2(-50f, -224f);
        vl.spacing = 12f;
        vl.childForceExpandHeight = false;
        vl.childControlHeight = false;
        vl.childAlignment = TextAnchor.UpperCenter;

        string[] iconos = { "stat_vida", "stat_estamina", "stat_mana", "stat_reshechizos", "stat_resgolpes" };
        for (int i = 0; i < Progreso.NumEstadisticas; i++)
        {
            Progreso.Estadistica e = (Progreso.Estadistica)i;
            Fila f = new Fila { estadistica = e };
            f.fondo = Caja("Fila_" + e, vl.transform, new Color(0.14f, 0.1f, 0.09f, 1f));
            f.fondo.raycastTarget = true;
            f.fondo.rectTransform.sizeDelta = new Vector2(880f, 66f);
            f.boton = f.fondo.gameObject.AddComponent<Button>();
            ColorBlock cb = f.boton.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.9f, 1.45f, 1.1f);
            cb.selectedColor = new Color(1.9f, 1.45f, 1.1f);
            cb.pressedColor = new Color(2.4f, 1.8f, 1.3f);
            cb.colorMultiplier = 1.5f;
            f.boton.colors = cb;
            f.boton.onClick.AddListener(() => m.Subir(e));
            AnadirSonidoSeleccion(f.fondo.gameObject);

            Image ico = Caja("Icono", f.fondo.transform, Color.white);
            ico.sprite = RecursosRPG.Get().Icono(iconos[i]);
            ico.enabled = ico.sprite != null;
            ico.preserveAspect = true;
            ico.rectTransform.anchorMin = ico.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            ico.rectTransform.anchoredPosition = new Vector2(38f, 0f);
            ico.rectTransform.sizeDelta = new Vector2(48f, 48f);

            TextMeshProUGUI nombre = Texto(Progreso.Nombre(e), f.fondo.transform, 28, new Color(0.93f, 0.88f, 0.82f));
            nombre.alignment = TextAlignmentOptions.Left;
            nombre.rectTransform.anchorMin = new Vector2(0f, 0f); nombre.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            nombre.rectTransform.offsetMin = new Vector2(80f, 0f); nombre.rectTransform.offsetMax = Vector2.zero;

            f.nivel = Texto("", f.fondo.transform, 30, new Color(1f, 0.85f, 0.6f));
            f.nivel.fontStyle = FontStyles.Bold;
            f.nivel.rectTransform.anchorMin = new Vector2(0.5f, 0f); f.nivel.rectTransform.anchorMax = new Vector2(0.6f, 1f);
            f.nivel.rectTransform.offsetMin = f.nivel.rectTransform.offsetMax = Vector2.zero;

            f.valor = Texto("", f.fondo.transform, 26, new Color(0.9f, 0.9f, 0.9f));
            f.valor.alignment = TextAlignmentOptions.Right;
            f.valor.rectTransform.anchorMin = new Vector2(0.6f, 0f); f.valor.rectTransform.anchorMax = new Vector2(1f, 1f);
            f.valor.rectTransform.offsetMin = Vector2.zero; f.valor.rectTransform.offsetMax = new Vector2(-24f, 0f);
            m.filas[i] = f;
        }
        m.primero = m.filas[0].boton;

        m.textoAviso = Texto("", panel.transform, 24, new Color(1f, 0.8f, 0.55f));
        m.textoAviso.rectTransform.anchorMin = new Vector2(0f, 0f); m.textoAviso.rectTransform.anchorMax = new Vector2(1f, 0f);
        m.textoAviso.rectTransform.sizeDelta = new Vector2(0f, 34f);
        m.textoAviso.rectTransform.anchoredPosition = new Vector2(0f, 120f);

        Image levantarse = Caja("Levantarse", panel.transform, new Color(0.2f, 0.12f, 0.1f, 1f));
        levantarse.raycastTarget = true;
        levantarse.rectTransform.anchorMin = levantarse.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        levantarse.rectTransform.sizeDelta = new Vector2(420f, 62f);
        levantarse.rectTransform.anchoredPosition = new Vector2(0f, 58f);
        Button bl = levantarse.gameObject.AddComponent<Button>();
        bl.colors = m.filas[0].boton.colors;
        bl.onClick.AddListener(m.CerrarInterno);
        AnadirSonidoSeleccion(levantarse.gameObject);
        TextMeshProUGUI tl = Texto("Levantarse", levantarse.transform, 30, new Color(0.95f, 0.88f, 0.82f));
        Estirar(tl.rectTransform);

        TextMeshProUGUI ayuda = Texto("Clic o Enter en una estadística para subirla     E / Esc: levantarse", go.transform, 22, new Color(0.8f, 0.78f, 0.75f, 0.7f));
        ayuda.rectTransform.anchorMin = ayuda.rectTransform.anchorMax = new Vector2(0.5f, 0.06f);
        ayuda.rectTransform.sizeDelta = new Vector2(1400f, 40f);

        go.SetActive(false);
        return m;
    }

    // Sonido al pasar por una opcion (raton o teclado).
    private static void AnadirSonidoSeleccion(GameObject go)
    {
        EventTrigger et = go.AddComponent<EventTrigger>();
        EventTrigger.Entry sel = new EventTrigger.Entry { eventID = EventTriggerType.Select };
        sel.callback.AddListener(_ => Sonido.Reproducir("menu_mover", 0.45f));
        et.triggers.Add(sel);
        EventTrigger.Entry encima = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        encima.callback.AddListener(_ => EventSystem.current?.SetSelectedGameObject(go));
        et.triggers.Add(encima);
    }

    private static void Arriba(RectTransform r, float y, float alto, float margen = 0f)
    {
        r.anchorMin = new Vector2(0f, 1f); r.anchorMax = new Vector2(1f, 1f);
        r.pivot = new Vector2(0.5f, 1f);
        r.offsetMin = new Vector2(margen, 0f); r.offsetMax = new Vector2(-margen, 0f);
        r.sizeDelta = new Vector2(r.sizeDelta.x, alto);
        r.anchoredPosition = new Vector2(0f, y);
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
        return t;
    }

    private static void Estirar(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
    }
}
