using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Menu de pausa (Escape), montado por codigo. Sustituye al panel de pausa antiguo:
//   - Reanudar.
//   - Volumen general y de musica (se guardan entre partidas).
//   - Destrabar: devuelve al player al ultimo suelo seguro y limpia efectos
//     que se hayan quedado atascados en pantalla.
//   - Reiniciar desde el ultimo punto de control (hoguera o checkpoint).
//   - Salir al menu (pide confirmacion).
// Se maneja con raton, teclado o mando (flechas / stick y Enter / boton sur).
public class MenuPausa : MonoBehaviour
{
    private static MenuPausa instancia;

    private CanvasGroup grupo;
    private Button primero;
    private TextMeshProUGUI textoSalir;
    private bool confirmarSalir;

    public static MenuPausa Get()
    {
        if (instancia == null) instancia = Crear();
        return instancia;
    }

    public void Mostrar(bool mostrar)
    {
        StopAllCoroutines();
        confirmarSalir = false;
        if (textoSalir != null) textoSalir.text = "Salir al menú";
        gameObject.SetActive(true);
        StartCoroutine(Fundido(mostrar));
        if (mostrar && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(primero.gameObject);
    }

    private IEnumerator Fundido(bool mostrar)
    {
        grupo.interactable = mostrar;
        grupo.blocksRaycasts = mostrar;
        float desde = grupo.alpha, hasta = mostrar ? 1f : 0f;
        for (float t = 0f; t < 0.15f; t += Time.unscaledDeltaTime)
        {
            grupo.alpha = Mathf.Lerp(desde, hasta, t / 0.15f);
            yield return null;
        }
        grupo.alpha = hasta;
    }

    // ------------------------------------------------------------------ Acciones

    private void Reanudar()
    {
        if (GameManager.Instance != null) GameManager.Instance.ResumeGame();
    }

    private void Destrabar()
    {
        Reanudar();
        PeligrosJefe.LimpiarTodo();
        EfectoVisual.LimpiarAtascados();
        PlayerControler p = GameManager.Instance != null ? GameManager.Instance.PlayerControler : null;
        if (p == null) p = FindFirstObjectByType<PlayerControler>();
        if (p != null) p.Destrabar();
    }

    private void ReiniciarDesdeCheckpoint()
    {
        Reanudar();
        if (GameManager.Instance != null) GameManager.Instance.ReiniciarDesdeCheckpoint();
    }

    private void Salir()
    {
        if (!confirmarSalir)
        {
            confirmarSalir = true;
            textoSalir.text = "¿Seguro? Pulsa otra vez";
            return;
        }
        Time.timeScale = 1f;
        SceneManager.LoadScene(0);
    }

    // ------------------------------------------------------------------ Construccion

    private static MenuPausa Crear()
    {
        GameObject go = new GameObject("MenuPausa");
        Canvas c = go.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 100;
        CanvasScaler cs = go.AddComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920, 1080);
        cs.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        MenuPausa m = go.AddComponent<MenuPausa>();
        m.grupo = go.AddComponent<CanvasGroup>();
        m.grupo.alpha = 0f;
        m.grupo.interactable = false;
        m.grupo.blocksRaycasts = false;

        // Velo oscuro y panel central.
        Image velo = Caja("Velo", go.transform, new Color(0f, 0f, 0f, 0.65f));
        Estirar(velo.rectTransform);

        Image panel = Caja("Panel", go.transform, new Color(0.07f, 0.05f, 0.06f, 0.92f));
        RectTransform rp = panel.rectTransform;
        rp.anchorMin = rp.anchorMax = new Vector2(0.5f, 0.5f);
        rp.sizeDelta = new Vector2(700f, 650f);
        Image marco = Caja("Marco", panel.transform, new Color(0.6f, 0.15f, 0.18f, 0.9f));
        RectTransform rm = marco.rectTransform;
        rm.anchorMin = new Vector2(0f, 1f); rm.anchorMax = new Vector2(1f, 1f);
        rm.sizeDelta = new Vector2(0f, 4f); rm.anchoredPosition = new Vector2(0f, -112f);

        TextMeshProUGUI titulo = Texto("PAUSA", panel.transform, 64, new Color(0.95f, 0.85f, 0.8f));
        titulo.fontStyle = FontStyles.Bold;
        titulo.characterSpacing = 18f;
        RectTransform rtit = titulo.rectTransform;
        rtit.anchorMin = new Vector2(0f, 1f); rtit.anchorMax = new Vector2(1f, 1f);
        rtit.pivot = new Vector2(0.5f, 1f);
        rtit.sizeDelta = new Vector2(0f, 80f);
        rtit.anchoredPosition = new Vector2(0f, -24f);

        VerticalLayoutGroup vl = new GameObject("Opciones").AddComponent<VerticalLayoutGroup>();
        vl.transform.SetParent(panel.transform, false);
        RectTransform ro = (RectTransform)vl.transform;
        ro.anchorMin = new Vector2(0f, 0f); ro.anchorMax = new Vector2(1f, 1f);
        ro.offsetMin = new Vector2(50f, 30f); ro.offsetMax = new Vector2(-50f, -136f);
        vl.spacing = 14f;
        vl.childForceExpandHeight = false;
        vl.childControlHeight = false;
        vl.childAlignment = TextAnchor.UpperCenter;

        m.primero = m.Boton("Reanudar", vl.transform, m.Reanudar).GetComponent<Button>();
        m.Deslizador("Volumen general", vl.transform, ControlVolumen.General, v => ControlVolumen.General = v);
        m.Deslizador("Volumen de la música", vl.transform, ControlVolumen.Musica, v => ControlVolumen.Musica = v);
        m.Boton("Destrabar", vl.transform, m.Destrabar);
        m.Boton("Reiniciar desde el último punto de control", vl.transform, m.ReiniciarDesdeCheckpoint);
        m.textoSalir = m.Boton("Salir al menú", vl.transform, m.Salir).GetComponentInChildren<TextMeshProUGUI>();

        go.SetActive(false);
        return m;
    }

    private GameObject Boton(string texto, Transform padre, UnityEngine.Events.UnityAction accion)
    {
        Image fondo = Caja("Boton_" + texto, padre, new Color(0.16f, 0.1f, 0.12f, 1f));
        fondo.rectTransform.sizeDelta = new Vector2(600f, 60f);
        Button b = fondo.gameObject.AddComponent<Button>();
        ColorBlock cb = b.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1.9f, 1.2f, 1.2f);
        cb.selectedColor = new Color(1.9f, 1.2f, 1.2f);
        cb.pressedColor = new Color(2.4f, 1.6f, 1.4f);
        cb.colorMultiplier = 1.5f;
        b.colors = cb;
        b.onClick.AddListener(accion);

        TextMeshProUGUI t = Texto(texto, fondo.transform, 28, new Color(0.93f, 0.87f, 0.82f));
        t.enableWordWrapping = false;
        Estirar(t.rectTransform);
        return fondo.gameObject;
    }

    private void Deslizador(string etiqueta, Transform padre, float valor, System.Action<float> alCambiar)
    {
        RectTransform fila = new GameObject("Fila_" + etiqueta).AddComponent<RectTransform>();
        fila.SetParent(padre, false);
        fila.sizeDelta = new Vector2(600f, 66f);

        TextMeshProUGUI t = Texto(etiqueta, fila, 24, new Color(0.8f, 0.75f, 0.72f));
        t.alignment = TextAlignmentOptions.TopLeft;
        t.rectTransform.anchorMin = new Vector2(0f, 1f); t.rectTransform.anchorMax = new Vector2(1f, 1f);
        t.rectTransform.pivot = new Vector2(0.5f, 1f);
        t.rectTransform.sizeDelta = new Vector2(0f, 30f);
        t.rectTransform.anchoredPosition = Vector2.zero;

        Slider s = new GameObject("Slider").AddComponent<Slider>();
        s.transform.SetParent(fila, false);
        RectTransform rs = (RectTransform)s.transform;
        rs.anchorMin = new Vector2(0f, 0f); rs.anchorMax = new Vector2(1f, 0f);
        rs.pivot = new Vector2(0.5f, 0f);
        rs.sizeDelta = new Vector2(0f, 26f);
        rs.anchoredPosition = new Vector2(0f, 4f);

        Image fondo = Caja("Fondo", s.transform, new Color(0.16f, 0.1f, 0.12f, 1f));
        Estirar(fondo.rectTransform);
        RectTransform area = new GameObject("Relleno").AddComponent<RectTransform>();
        area.SetParent(s.transform, false);
        Estirar(area);
        Image relleno = Caja("Barra", area, new Color(0.62f, 0.05f, 0.1f, 1f));
        Estirar(relleno.rectTransform);
        RectTransform zonaAsa = new GameObject("ZonaAsa").AddComponent<RectTransform>();
        zonaAsa.SetParent(s.transform, false);
        Estirar(zonaAsa);
        Image asa = Caja("Asa", zonaAsa, new Color(0.95f, 0.85f, 0.8f, 1f));
        asa.rectTransform.sizeDelta = new Vector2(18f, 0f);

        s.fillRect = relleno.rectTransform;
        s.handleRect = asa.rectTransform;
        s.targetGraphic = asa;
        s.minValue = 0f;
        s.maxValue = 1f;
        s.value = valor;
        s.onValueChanged.AddListener(v => alCambiar(v));
    }

    private static Image Caja(string nombre, Transform padre, Color color)
    {
        Image i = new GameObject(nombre).AddComponent<Image>();
        i.transform.SetParent(padre, false);
        i.color = color;
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
        return t;
    }

    private static void Estirar(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
    }
}

// Volumenes guardados. El general va a AudioListener; la musica la leen las
// fuentes de musica (la del jefe, por ahora) multiplicando su volumen.
public static class ControlVolumen
{
    public static event System.Action AlCambiar;

    public static float General
    {
        get => PlayerPrefs.GetFloat("volGeneral", 1f);
        set { PlayerPrefs.SetFloat("volGeneral", value); AudioListener.volume = value; AlCambiar?.Invoke(); }
    }

    public static float Musica
    {
        get => PlayerPrefs.GetFloat("volMusica", 1f);
        set { PlayerPrefs.SetFloat("volMusica", value); AlCambiar?.Invoke(); }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Aplicar()
    {
        AudioListener.volume = General;
    }
}
