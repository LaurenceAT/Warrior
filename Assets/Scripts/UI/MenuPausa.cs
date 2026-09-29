using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Menu de pausa (Escape), montado por codigo, con el mismo estilo que el resto
// de menus del juego (EstiloMenu). Dos columnas:
//   - Partida: Reanudar, Destrabar (devuelve al player al ultimo suelo seguro y
//     limpia efectos atascados), Reiniciar desde el ultimo punto de control y
//     Salir al menu (pide confirmacion).
//   - Ajustes: volumen general, de musica y de efectos, y brillo (se guardan
//     entre partidas).
//   - Libro de pistas: las inscripciones de las estatuas ya leidas, por nivel.
// Arriba, donde estas: el nivel, el nivel del personaje y las almas.
// Se maneja con raton, teclado o mando (flechas / stick y Enter / boton sur).
public class MenuPausa : MonoBehaviour
{
    private static MenuPausa instancia;

    private CanvasGroup grupo;
    private Button primero;
    private TextMeshProUGUI textoSalir, textoLugar, ayuda;
    private bool confirmarSalir;

    // Libro de pistas: tapa el panel principal mientras esta abierto.
    private GameObject principal, libro;
    private Button botonLibro;
    private RectTransform listaLibro;
    private TextMeshProUGUI tituloLeido, textoLeido;
    private readonly System.Collections.Generic.Dictionary<GameObject, Partida.PistaLeida> entradas =
        new System.Collections.Generic.Dictionary<GameObject, Partida.PistaLeida>();
    private bool libroAbierto;
    private int libroCerradoEnFrame = -1;

    // El Esc que cierra el libro no debe cerrar tambien la pausa (GameManager lo mira).
    public static bool LibroAbierto => instancia != null && (instancia.libroAbierto || instancia.libroCerradoEnFrame == Time.frameCount);
    private const string AyudaPrincipal = "Enter / clic: elegir      ← →: ajustar      Esc: volver al juego";
    private const string AyudaLibro = "↑ ↓: elegir pista      Esc: volver";

    public static MenuPausa Get()
    {
        if (instancia == null) instancia = Crear();
        return instancia;
    }

    public void Mostrar(bool mostrar)
    {
        StopAllCoroutines();
        confirmarSalir = false;
        if (libroAbierto) CerrarLibro(false);
        if (textoSalir != null) textoSalir.text = "Salir al menú";
        if (textoLugar != null)
            textoLugar.text = $"{Partida.NombreNivel(SceneManager.GetActiveScene().name)}   ·   Nivel {Progreso.NivelTotal}   ·   Almas {Progreso.Almas:N0}";
        gameObject.SetActive(true);
        if (mostrar) SonidoMenu.Abrir();
        StartCoroutine(Fundido(mostrar));
        if (mostrar && EventSystem.current != null)
        {
            SonidoMenu.Silenciar();
            EventSystem.current.SetSelectedGameObject(primero.gameObject);
        }
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

    private void Update()
    {
        if (grupo == null || !grupo.interactable) return;
        if (libroAbierto)
        {
            UnityEngine.InputSystem.Keyboard k = UnityEngine.InputSystem.Keyboard.current;
            UnityEngine.InputSystem.Gamepad g = UnityEngine.InputSystem.Gamepad.current;
            if ((k != null && k.escapeKey.wasPressedThisFrame) || (g != null && g.buttonEast.wasPressedThisFrame)) { CerrarLibro(true); return; }
            // La pista elegida se lee a la derecha.
            GameObject sel = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (sel != null && entradas.TryGetValue(sel, out Partida.PistaLeida p))
            {
                tituloLeido.text = string.IsNullOrEmpty(p.titulo) ? "" : p.titulo.ToUpper();
                textoLeido.text = p.texto;
            }
        }
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null && primero != null)
            EventSystem.current.SetSelectedGameObject(libroAbierto ? PrimeraDelLibro() : primero.gameObject);
    }

    // ------------------------------------------------------------------ Libro de pistas

    private void AbrirLibro()
    {
        libroAbierto = true;
        principal.SetActive(false);
        libro.SetActive(true);
        ayuda.text = AyudaLibro;

        // Se sueltan antes de borrarlos: si no, seguirian en la lista este fotograma.
        for (int i = listaLibro.childCount - 1; i >= 0; i--) { GameObject h = listaLibro.GetChild(i).gameObject; h.transform.SetParent(null); Destroy(h); }
        entradas.Clear();
        tituloLeido.text = "";
        textoLeido.text = "";

        // Por nivel, en el orden del juego (el de Build Settings).
        var porNivel = new System.Collections.Generic.List<string>();
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            porNivel.Add(System.IO.Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(i)));
        foreach (Partida.PistaLeida p in Partida.Pistas)
            if (!porNivel.Contains(p.escena)) porNivel.Add(p.escena);

        foreach (string escena in porNivel)
        {
            bool cabecera = false;
            foreach (Partida.PistaLeida p in Partida.Pistas)
            {
                if (p.escena != escena) continue;
                if (!cabecera)
                {
                    cabecera = true;
                    TextMeshProUGUI t = EstiloMenu.Texto(Partida.NombreNivel(escena).ToUpper(), listaLibro, 20, new Color(0.78f, 0.66f, 0.46f));
                    t.characterSpacing = 6f;
                    t.alignment = TextAlignmentOptions.BottomLeft;
                    LayoutElement le = t.gameObject.AddComponent<LayoutElement>();
                    le.preferredHeight = le.minHeight = 36f;
                }
                Button b = EstiloMenu.Opcion(listaLibro, string.IsNullOrEmpty(p.titulo) ? "Inscripción" : p.titulo, null, 46f, 24f, null, TextAlignmentOptions.Left);
                entradas[b.gameObject] = p;
            }
        }
        if (entradas.Count == 0)
        {
            TextMeshProUGUI t = EstiloMenu.Texto("Aún no has leído ninguna inscripción.", listaLibro, 24, EstiloMenu.TextoApagado);
            t.fontStyle = FontStyles.Italic;
            t.alignment = TextAlignmentOptions.Left;
            LayoutElement le = t.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = le.minHeight = 50f;
        }
        Button volver = EstiloMenu.Opcion(listaLibro, "Volver", () => CerrarLibro(true), 50f, 26f, null, TextAlignmentOptions.Left);
        GameObject pVolver = volver.gameObject;

        if (EventSystem.current != null)
        {
            SonidoMenu.Silenciar();
            EventSystem.current.SetSelectedGameObject(entradas.Count > 0 ? PrimeraDelLibro() : pVolver);
        }
    }

    private GameObject PrimeraDelLibro()
    {
        for (int i = 0; i < listaLibro.childCount; i++)
            if (listaLibro.GetChild(i).GetComponent<Button>() != null) return listaLibro.GetChild(i).gameObject;
        return primero.gameObject;
    }

    private void CerrarLibro(bool sonido)
    {
        libroAbierto = false;
        libroCerradoEnFrame = Time.frameCount;
        libro.SetActive(false);
        principal.SetActive(true);
        ayuda.text = AyudaPrincipal;
        if (sonido) SonidoMenu.Cancelar();
        if (EventSystem.current != null)
        {
            SonidoMenu.Silenciar();
            EventSystem.current.SetSelectedGameObject(botonLibro.gameObject);
        }
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
        Partida.Guardar();
        if (GameManager.Instance != null) GameManager.Instance.ResumeGame();
        PantallaCarga.Cargar(0);
    }

    // ------------------------------------------------------------------ Construccion

    private static MenuPausa Crear()
    {
        GameObject go = new GameObject("MenuPausa");
        MenuPausa m = go.AddComponent<MenuPausa>();
        m.grupo = EstiloMenu.Lienzo(go, 100);

        RectTransform panel = EstiloMenu.Panel(go.transform, "PAUSA", new Vector2(1240f, 700f));
        m.textoLugar = EstiloMenu.Texto("", panel, 24, new Color(0.82f, 0.76f, 0.68f));
        m.textoLugar.fontStyle = FontStyles.Italic;
        EstiloMenu.Arriba(m.textoLugar.rectTransform, -108f, 34f);

        // Columna izquierda: la partida.
        RectTransform izq = Zona(panel, 0f, 0.46f);
        Cabecera(izq, "PARTIDA");
        VerticalLayoutGroup ci = EstiloMenu.Columna(izq, 0f, 0f, 56f, 0f, 6f);
        m.primero = EstiloMenu.Opcion(ci.transform, "Reanudar", m.Reanudar, 60f, 30f, null, TextAlignmentOptions.Left);
        EstiloMenu.Opcion(ci.transform, "Destrabar", m.Destrabar, 60f, 30f, null, TextAlignmentOptions.Left);
        EstiloMenu.Opcion(ci.transform, "Reiniciar desde el último punto de control", m.ReiniciarDesdeCheckpoint, 60f, 26f, null, TextAlignmentOptions.Left);
        m.botonLibro = EstiloMenu.Opcion(ci.transform, "Libro de pistas", m.AbrirLibro, 60f, 30f, null, TextAlignmentOptions.Left);
        m.textoSalir = EstiloMenu.Opcion(ci.transform, "Salir al menú", m.Salir, 60f, 30f, null, TextAlignmentOptions.Left)
                                 .GetComponentInChildren<TextMeshProUGUI>();

        // Linea vertical entre columnas.
        Image divisor = EstiloMenu.Caja("Divisor", panel, EstiloMenu.FiloTenue);
        divisor.rectTransform.anchorMin = new Vector2(0.5f, 0f);
        divisor.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        divisor.rectTransform.sizeDelta = new Vector2(2f, 0f);
        divisor.rectTransform.offsetMin = new Vector2(-1f, 70f);
        divisor.rectTransform.offsetMax = new Vector2(1f, -170f);

        // Columna derecha: ajustes.
        RectTransform der = Zona(panel, 0.54f, 1f);
        Cabecera(der, "AJUSTES");
        VerticalLayoutGroup cd = EstiloMenu.Columna(der, 0f, 0f, 56f, 0f, 10f);
        Deslizador("Volumen general", cd.transform, ControlVolumen.General, v => ControlVolumen.General = v);
        Deslizador("Música", cd.transform, ControlVolumen.Musica, v => ControlVolumen.Musica = v);
        Deslizador("Efectos", cd.transform, ControlVolumen.Efectos, v => ControlVolumen.Efectos = v);
        Deslizador("Brillo", cd.transform, ControlBrillo.Brillo - 0.5f, v => ControlBrillo.Brillo = 0.5f + v);

        m.principal = panel.parent.gameObject;
        m.CrearLibro(go.transform);

        TextMeshProUGUI ayuda = EstiloMenu.Texto(AyudaPrincipal, go.transform, 22, new Color(0.8f, 0.78f, 0.75f, 0.6f));
        ayuda.rectTransform.anchorMin = ayuda.rectTransform.anchorMax = new Vector2(0.5f, 0.08f);
        ayuda.rectTransform.sizeDelta = new Vector2(1400f, 40f);
        m.ayuda = ayuda;

        go.SetActive(false);
        return m;
    }

    // Mismo panel que la pausa: a la izquierda la lista (por nivel) y a la
    // derecha la pista elegida.
    private void CrearLibro(Transform padre)
    {
        RectTransform panel = EstiloMenu.Panel(padre, "LIBRO DE PISTAS", new Vector2(1240f, 700f));
        libro = panel.parent.gameObject;

        RectTransform izq = Zona(panel, 0f, 0.4f);
        Cabecera(izq, "LEÍDAS");
        VerticalLayoutGroup ci = EstiloMenu.Columna(izq, 0f, 0f, 50f, 0f, 2f);
        listaLibro = (RectTransform)ci.transform;

        Image divisor = EstiloMenu.Caja("Divisor", panel, EstiloMenu.FiloTenue);
        divisor.rectTransform.anchorMin = new Vector2(0.43f, 0f);
        divisor.rectTransform.anchorMax = new Vector2(0.43f, 1f);
        divisor.rectTransform.sizeDelta = new Vector2(2f, 0f);
        divisor.rectTransform.offsetMin = new Vector2(-1f, 70f);
        divisor.rectTransform.offsetMax = new Vector2(1f, -130f);

        RectTransform der = Zona(panel, 0.46f, 1f);
        der.offsetMax = new Vector2(der.offsetMax.x, -130f);
        tituloLeido = EstiloMenu.Texto("", der, 26, EstiloMenu.Titulo);
        tituloLeido.fontStyle = FontStyles.Bold;
        tituloLeido.characterSpacing = 8f;
        tituloLeido.alignment = TextAlignmentOptions.Left;
        EstiloMenu.Arriba(tituloLeido.rectTransform, 0f, 36f);
        textoLeido = EstiloMenu.Texto("", der, 28, EstiloMenu.TextoElegido);
        textoLeido.enableWordWrapping = true;
        textoLeido.alignment = TextAlignmentOptions.TopLeft;
        textoLeido.lineSpacing = 8f;
        RectTransform rt = textoLeido.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = new Vector2(0f, -56f);

        libro.SetActive(false);
    }

    private static RectTransform Zona(RectTransform panel, float desde, float hasta)
    {
        RectTransform r = new GameObject("Columna", typeof(RectTransform)).GetComponent<RectTransform>();
        r.SetParent(panel, false);
        r.anchorMin = new Vector2(desde, 0f);
        r.anchorMax = new Vector2(hasta, 1f);
        r.offsetMin = new Vector2(desde == 0f ? 60f : 20f, 70f);
        r.offsetMax = new Vector2(hasta >= 1f ? -60f : -20f, -165f);
        return r;
    }

    private static void Cabecera(RectTransform zona, string texto)
    {
        TextMeshProUGUI t = EstiloMenu.Texto(texto, zona, 22, new Color(0.78f, 0.66f, 0.46f));
        t.characterSpacing = 8f;
        t.alignment = TextAlignmentOptions.Left;
        EstiloMenu.Arriba(t.rectTransform, 0f, 30f);
        Image l = EstiloMenu.Caja("Linea", zona, EstiloMenu.FiloTenue);
        l.rectTransform.anchorMin = new Vector2(0f, 1f);
        l.rectTransform.anchorMax = new Vector2(1f, 1f);
        l.rectTransform.sizeDelta = new Vector2(0f, 1f);
        l.rectTransform.anchoredPosition = new Vector2(0f, -38f);
    }

    // Deslizador con su nombre y el valor en %. Suena suave al elegirlo.
    private static void Deslizador(string etiqueta, Transform padre, float valor, System.Action<float> alCambiar)
    {
        RectTransform fila = new GameObject("Fila_" + etiqueta, typeof(RectTransform)).GetComponent<RectTransform>();
        fila.SetParent(padre, false);
        LayoutElement le = fila.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = le.minHeight = 72f;

        TextMeshProUGUI t = EstiloMenu.Texto(etiqueta, fila, 26, EstiloMenu.TextoNormal);
        t.alignment = TextAlignmentOptions.TopLeft;
        EstiloMenu.Arriba(t.rectTransform, 0f, 32f);
        TextMeshProUGUI pct = EstiloMenu.Texto("", fila, 24, EstiloMenu.TextoNormal);
        pct.alignment = TextAlignmentOptions.TopRight;
        EstiloMenu.Arriba(pct.rectTransform, 0f, 32f);

        Slider s = new GameObject("Slider", typeof(RectTransform)).AddComponent<Slider>();
        s.transform.SetParent(fila, false);
        RectTransform rs = (RectTransform)s.transform;
        rs.anchorMin = new Vector2(0f, 0f); rs.anchorMax = new Vector2(1f, 0f);
        rs.pivot = new Vector2(0.5f, 0f);
        rs.sizeDelta = new Vector2(0f, 22f);
        rs.anchoredPosition = new Vector2(0f, 6f);

        Image fondo = EstiloMenu.Caja("Fondo", s.transform, new Color(0.12f, 0.1f, 0.1f, 1f));
        fondo.raycastTarget = true;
        EstiloMenu.Estirar(fondo.rectTransform, 0f);
        fondo.rectTransform.anchorMin = new Vector2(0f, 0.3f);
        fondo.rectTransform.anchorMax = new Vector2(1f, 0.7f);
        RectTransform area = new GameObject("Relleno", typeof(RectTransform)).GetComponent<RectTransform>();
        area.SetParent(s.transform, false);
        EstiloMenu.Estirar(area);
        area.anchorMin = new Vector2(0f, 0.3f);
        area.anchorMax = new Vector2(1f, 0.7f);
        Image relleno = EstiloMenu.Caja("Barra", area, new Color(0.62f, 0.48f, 0.28f, 1f));
        EstiloMenu.Estirar(relleno.rectTransform);
        RectTransform zonaAsa = new GameObject("ZonaAsa", typeof(RectTransform)).GetComponent<RectTransform>();
        zonaAsa.SetParent(s.transform, false);
        EstiloMenu.Estirar(zonaAsa);
        Image asa = EstiloMenu.Caja("Asa", zonaAsa, new Color(0.95f, 0.88f, 0.75f, 1f));
        asa.raycastTarget = true;
        asa.rectTransform.sizeDelta = new Vector2(12f, 0f);
        asa.rectTransform.localRotation = Quaternion.identity;

        s.fillRect = relleno.rectTransform;
        s.handleRect = asa.rectTransform;
        s.targetGraphic = asa;
        s.transition = Selectable.Transition.None;
        s.minValue = 0f;
        s.maxValue = 1f;
        s.value = valor;
        pct.text = Mathf.RoundToInt(valor * 100f) + " %";
        s.onValueChanged.AddListener(v => { alCambiar(v); pct.text = Mathf.RoundToInt(v * 100f) + " %"; });

        // Al elegirlo se ilumina el nombre, como las opciones.
        OpcionEstilo o = s.gameObject.AddComponent<OpcionEstilo>();
        o.texto = t;
        o.marca = EstiloMenu.Caja("Marca", fila, EstiloMenu.Filo);
        o.marca.rectTransform.anchorMin = new Vector2(0f, 0.2f);
        o.marca.rectTransform.anchorMax = new Vector2(0f, 0.8f);
        o.marca.rectTransform.sizeDelta = new Vector2(3f, 0f);
        o.marca.rectTransform.anchoredPosition = new Vector2(-12f, 0f);
        o.marca.enabled = false;
    }
}
