using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Menu de pausa (Escape), montado por codigo, con el mismo estilo que el resto
// de menus del juego (EstiloMenu). Dos columnas:
//   - Partida: Reanudar, Destrabar (devuelve al player al ultimo suelo seguro y
//     limpia efectos atascados), Reiniciar desde el ultimo punto de control,
//     Libro de pistas, Bestiario (partida normal) o Reiniciar desafio (desafios,
//     con confirmacion) y Salir al menu / Abandonar (pide confirmacion).
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
    // Libro por capas: la pista elegida queda marcada; Esc la suelta y luego cierra.
    private readonly CapasMenu capasLibro = new CapasMenu();
    private readonly GrupoMarcado grupoLibro = new GrupoMarcado();

    // Desafio: reiniciar (con confirmacion). Partida normal: el Bestiario.
    private Button botonReiniciarDesafio, botonBestiario, botonConfirmarReinicio;
    private GameObject confirmacion;
    private MenuBestiario bestiario;
    private bool bestiarioAbierto;

    // El Esc que cierra un submenu (libro, Bestiario, confirmacion) no debe
    // cerrar tambien la pausa (GameManager lo mira).
    public static bool LibroAbierto => instancia != null && (instancia.libroAbierto || instancia.bestiarioAbierto
                                                              || (instancia.confirmacion != null && instancia.confirmacion.activeSelf)
                                                              || instancia.libroCerradoEnFrame == Time.frameCount);
    public static bool Existe => instancia != null;
    private const string AyudaPrincipal = "Enter / clic: elegir      ← →: ajustar      Esc: volver al juego";
    private const string AyudaLibro = "↑ ↓: elegir pista      Enter / clic: leer      Esc: volver";
    private const string AyudaSubmenu = "Enter / clic: elegir      Esc: volver";

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
        if (bestiarioAbierto) CerrarBestiario(false);
        if (confirmacion != null && confirmacion.activeSelf) CerrarConfirmacion(false);
        if (mostrar) Globales.GuardarPendiente();
        // En un desafio: reiniciarlo. En la partida normal: el Bestiario.
        if (botonReiniciarDesafio != null) botonReiniciarDesafio.gameObject.SetActive(Desafio.Activo);
        if (botonBestiario != null) botonBestiario.gameObject.SetActive(!Desafio.Activo);
        if (textoSalir != null) textoSalir.text = Desafio.Activo ? "Abandonar desafío" : "Salir al menú";
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
        // Atras un paso (Esc o el boton este; el clic derecho es el bloqueo del juego).
        bool atras = CapasMenu.PulsadoAtras(false);
        if (confirmacion.activeSelf)
        {
            if (atras) CerrarConfirmacion(true);
            else if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null)
                EventSystem.current.SetSelectedGameObject(botonConfirmarReinicio.gameObject);
            return;
        }
        if (bestiarioAbierto)
        {
            if (atras && !bestiario.Atras()) { CerrarBestiario(true); return; }
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null)
                EventSystem.current.SetSelectedGameObject(bestiario.Primero());
            return;
        }
        if (libroAbierto && atras && !capasLibro.Atras()) { CerrarLibro(true); return; }
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null && primero != null)
            EventSystem.current.SetSelectedGameObject(libroAbierto ? PrimeraDelLibro() : primero.gameObject);
    }

    // ------------------------------------------------------------------ Reiniciar desafio

    private void PedirReinicio()
    {
        confirmacion.SetActive(true);
        principal.SetActive(false);
        ayuda.text = AyudaSubmenu;
        StartCoroutine(TransicionMenu.Aparecer(confirmacion.GetComponent<CanvasGroup>(), 0.15f));
        SonidoMenu.Silenciar();
        EventSystem.current?.SetSelectedGameObject(botonConfirmarReinicio.gameObject);
    }

    private void CerrarConfirmacion(bool sonido)
    {
        confirmacion.SetActive(false);
        principal.SetActive(true);
        libroCerradoEnFrame = Time.frameCount;
        ayuda.text = AyudaPrincipal;
        if (sonido) SonidoMenu.Atras();
        SonidoMenu.Silenciar();
        EventSystem.current?.SetSelectedGameObject(botonReiniciarDesafio.gameObject);
    }

    private void ConfirmarReinicio()
    {
        confirmacion.SetActive(false);
        Desafio.Reiniciar();
    }

    // ------------------------------------------------------------------ Bestiario

    private void AbrirBestiario()
    {
        bestiarioAbierto = true;
        principal.SetActive(false);
        bestiario.gameObject.SetActive(true);
        ayuda.text = "";
        bestiario.Abrir();
    }

    private void CerrarBestiario(bool sonido)
    {
        bestiarioAbierto = false;
        libroCerradoEnFrame = Time.frameCount;
        bestiario.gameObject.SetActive(false);
        principal.SetActive(true);
        ayuda.text = AyudaPrincipal;
        if (sonido) SonidoMenu.Atras();
        SonidoMenu.Silenciar();
        EventSystem.current?.SetSelectedGameObject(botonBestiario.gameObject);
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
        capasLibro.Vaciar();
        grupoLibro.Vaciar();
        NingunaLeida();

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
                grupoLibro.Anadir(b.GetComponent<OpcionEstilo>());
                Button esta = b;
                b.onClick.AddListener(() => Leer(esta));
            }
        }
        NingunaLeida();
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

    // Clic o Enter en una pista: se lee a la derecha y queda marcada (Esc la suelta).
    private void Leer(Button b)
    {
        if (!entradas.TryGetValue(b.gameObject, out Partida.PistaLeida p)) return;
        if (grupoLibro.Actual == null) capasLibro.Entrar(() => { grupoLibro.Desmarcar(); NingunaLeida(); });
        grupoLibro.Marcar(b.GetComponent<OpcionEstilo>());
        tituloLeido.text = string.IsNullOrEmpty(p.titulo) ? "" : p.titulo.ToUpper();
        textoLeido.text = p.texto;
        textoLeido.color = EstiloMenu.TextoElegido;
        textoLeido.fontStyle = FontStyles.Normal;
    }

    private void NingunaLeida()
    {
        tituloLeido.text = "";
        textoLeido.text = entradas.Count > 0 ? "Elige una inscripción para leerla." : "";
        textoLeido.color = EstiloMenu.TextoApagado;
        textoLeido.fontStyle = FontStyles.Italic;
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
            textoSalir.text = Desafio.Activo ? "¿Abandonar? Pulsa otra vez" : "¿Seguro? Pulsa otra vez";
            return;
        }
        // En un desafio: se abandona y se vuelve al menu de Desafios (sin guardar nada).
        if (Desafio.Activo) { Desafio.Salir(); return; }
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
        m.botonBestiario = EstiloMenu.Opcion(ci.transform, "Bestiario", m.AbrirBestiario, 60f, 30f, null, TextAlignmentOptions.Left);
        m.botonReiniciarDesafio = EstiloMenu.Opcion(ci.transform, "Reiniciar desafío", m.PedirReinicio, 60f, 30f, null, TextAlignmentOptions.Left);
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
        m.CrearConfirmacion(go.transform);
        m.bestiario = MenuBestiario.Construir(go.transform, () => m.CerrarBestiario(false), false);
        m.bestiario.gameObject.SetActive(false);

        TextMeshProUGUI ayuda = EstiloMenu.Texto(AyudaPrincipal, go.transform, 22, new Color(0.8f, 0.78f, 0.75f, 0.6f));
        ayuda.rectTransform.anchorMin = ayuda.rectTransform.anchorMax = new Vector2(0.5f, 0.08f);
        ayuda.rectTransform.sizeDelta = new Vector2(1400f, 40f);
        m.ayuda = ayuda;

        go.SetActive(false);
        return m;
    }

    // "¿Reiniciar el desafio?": tapa la pausa; Esc o Cancelar vuelven a ella.
    private void CrearConfirmacion(Transform padre)
    {
        RectTransform raiz = new GameObject("ConfirmarReinicio", typeof(RectTransform)).GetComponent<RectTransform>();
        raiz.SetParent(padre, false);
        EstiloMenu.Estirar(raiz);
        confirmacion = raiz.gameObject;
        confirmacion.AddComponent<CanvasGroup>();
        RectTransform p = EstiloMenu.Panel(raiz, "REINICIAR DESAFÍO", new Vector2(1060f, 460f));
        TextMeshProUGUI t = EstiloMenu.Texto("¿Reiniciar el desafío?\n<size=80%><color=#b8b0a6>Perderás lo comprado y el progreso de esta prueba. " +
                                             "Lo que descubriste del jefe se conserva.</color></size>", p, 30, EstiloMenu.TextoElegido);
        t.enableWordWrapping = true;
        t.lineSpacing = 10f;
        t.rectTransform.anchoredPosition = new Vector2(0f, 34f);
        t.rectTransform.sizeDelta = new Vector2(900f, 140f);
        VerticalLayoutGroup col = EstiloMenu.Columna(p, 240f, 240f, 300f, 24f, 4f);
        botonConfirmarReinicio = EstiloMenu.Opcion(col.transform, "Reiniciar", ConfirmarReinicio, 56f, 28f);
        EstiloMenu.Opcion(col.transform, "Cancelar", () => CerrarConfirmacion(false), 50f, 24f);
        confirmacion.SetActive(false);
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
