using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Menu principal: fondo negro, brasas de fuego que suben y que, al pasar, dan un
// poco de luz al titulo (poca: el tono es oscuro y sobrio). Opciones:
//   Nueva Partida  -> empieza de cero (nivel 1) en el primer nivel.
//   Cargar Partida -> las partidas guardadas, con nivel, hoguera y tiempo jugado.
//   Opciones       -> volumen general, de musica y de efectos, y brillo.
//   Controles      -> todos los controles y mecanicas, con iconos.
//   Salir
// Se maneja con raton, teclado (flechas / WASD, Enter, Esc) o mando.
// Todo lo de aspecto y sonido se cambia en el Inspector.
public class MenuPrincipal : MonoBehaviour
{
    [Header("Titulo")]
    [SerializeField] private string titulo = "WARRIOR";
    [SerializeField] private string subtitulo = "";
    [Tooltip("Fuente del titulo y el menu (vacio = la de por defecto).")]
    [SerializeField] private TMP_FontAsset fuente;
    [SerializeField] private float tamanoTitulo = 150f;
    [SerializeField] private Color colorTitulo = new Color(0.62f, 0.6f, 0.58f, 1f);
    [Tooltip("Color del titulo cuando lo ilumina el fuego (al maximo).")]
    [SerializeField] private Color colorTituloIluminado = new Color(1f, 0.82f, 0.6f, 1f);

    [Header("Fuego")]
    [SerializeField] private float brasasPorSegundo = 12f;
    [SerializeField] private Color colorBrasaA = new Color(1f, 0.62f, 0.2f, 1f);
    [SerializeField] private Color colorBrasaB = new Color(1f, 0.28f, 0.06f, 1f);
    [Tooltip("Cuanto ilumina el fuego el titulo (0 = nada, 1 = mucho).")]
    [Range(0f, 1f)] [SerializeField] private float intensidadLuz = 0.35f;

    [Header("Sonido")]
    [Tooltip("Ambiente de fondo (una fogata crepitando). Vacio = silencio.")]
    [SerializeField] private AudioClip ambiente;
    [Range(0f, 1f)] [SerializeField] private float volumenAmbiente = 0.5f;

    [Header("Partida")]
    [Tooltip("Escena en la que empieza una partida nueva.")]
    [SerializeField] private string primerNivel = "Nivel Nieve";

    private TMP_FontAsset Fuente => fuente != null ? fuente : TMP_Settings.defaultFontAsset;

    private RectTransform lienzo;
    private TextMeshProUGUI textoTitulo;
    private Image resplandor, brilloSuelo;
    private CanvasGroup grupo;
    private GameObject panelPrincipal, panelCargar, panelOpciones, panelControles;
    private RectTransform listaPartidas;
    private GameObject primeroPrincipal;
    private AudioSource fuenteAmbiente;
    private float semilla;
    private bool saliendo;

    private class Brasa
    {
        public RectTransform rt;
        public Image img;
        public Vector2 vel;
        public float vida, t, fase, tamano;
        public Color color;
    }

    private readonly List<Brasa> brasas = new List<Brasa>();
    private float acumulado;

    // ------------------------------------------------------------------ Ciclo

    private void Awake()
    {
        Time.timeScale = 1f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        semilla = Random.value * 100f;
        if (FindFirstObjectByType<EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem", typeof(EventSystem));
            es.AddComponent<InputSystemUIInputModule>();
        }
        Construir();
        Mostrar(panelPrincipal);

        if (ambiente != null)
        {
            fuenteAmbiente = gameObject.AddComponent<AudioSource>();
            fuenteAmbiente.clip = ambiente;
            fuenteAmbiente.loop = true;
            fuenteAmbiente.volume = 0f;
            fuenteAmbiente.Play();
        }
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        grupo.alpha = Mathf.MoveTowards(grupo.alpha, 1f, dt * 0.7f);
        if (fuenteAmbiente != null)
            fuenteAmbiente.volume = Mathf.MoveTowards(fuenteAmbiente.volume, volumenAmbiente * ControlVolumen.Efectos, dt * 0.3f);

        ActualizarBrasas(dt);
        ActualizarLuz();

        // Atras: Esc, clic derecho o el boton este del mando.
        Keyboard k = Keyboard.current;
        Mouse m = Mouse.current;
        Gamepad g = Gamepad.current;
        bool atras = (k != null && k.escapeKey.wasPressedThisFrame) || (m != null && m.rightButton.wasPressedThisFrame)
                     || (g != null && g.buttonEast.wasPressedThisFrame);
        if (atras && !panelPrincipal.activeSelf && !saliendo) { SonidoMenu.Cancelar(); Mostrar(panelPrincipal); }

        // Las listas largas (controles) bajan con las flechas, W/S o el stick.
        if (!panelPrincipal.activeSelf)
        {
            ScrollRect sr = (panelControles.activeSelf ? panelControles : panelCargar.activeSelf ? panelCargar : panelOpciones).GetComponentInChildren<ScrollRect>();
            float eje = 0f;
            if (k != null && (k.downArrowKey.isPressed || k.sKey.isPressed)) eje -= 1f;
            if (k != null && (k.upArrowKey.isPressed || k.wKey.isPressed)) eje += 1f;
            if (g != null) eje += g.leftStick.ReadValue().y;
            if (sr != null && panelControles.activeSelf && Mathf.Abs(eje) > 0.2f && sr.content.rect.height > sr.viewport.rect.height)
                sr.verticalNormalizedPosition = Mathf.Clamp01(sr.verticalNormalizedPosition + eje * dt * 600f / sr.content.rect.height);
        }

        // Si nada esta seleccionado (clic en el fondo), el teclado vuelve al menu.
        EventSystem es = EventSystem.current;
        if (es != null && es.currentSelectedGameObject == null && k != null && (k.upArrowKey.wasPressedThisFrame || k.downArrowKey.wasPressedThisFrame
            || k.wKey.wasPressedThisFrame || k.sKey.wasPressedThisFrame))
            SeleccionarPrimero();
    }

    // ------------------------------------------------------------------ Fuego

    private void ActualizarBrasas(float dt)
    {
        acumulado += dt * brasasPorSegundo;
        while (acumulado >= 1f)
        {
            acumulado -= 1f;
            NuevaBrasa();
        }
        float ancho = lienzo.rect.width, alto = lienzo.rect.height;
        foreach (Brasa b in brasas)
        {
            if (b.vida <= 0f) continue;
            b.t += dt;
            if (b.t >= b.vida) { b.vida = 0f; b.img.enabled = false; continue; }
            float k = b.t / b.vida;
            // Suben ondulando, frenan un poco y se apagan.
            Vector2 p = b.rt.anchoredPosition;
            p += new Vector2(b.vel.x + Mathf.Sin(b.t * 2.2f + b.fase) * 18f, b.vel.y * (1f - k * 0.4f)) * dt;
            b.rt.anchoredPosition = p;
            float a = Mathf.Clamp01(k * 6f) * (1f - k) * (0.6f + 0.4f * Mathf.Sin(b.t * 9f + b.fase));
            b.img.color = new Color(b.color.r, b.color.g, b.color.b, a);
            b.rt.sizeDelta = Vector2.one * b.tamano * (1f - k * 0.5f);
            if (p.y > alto * 0.5f + 40f || Mathf.Abs(p.x) > ancho * 0.5f + 40f) { b.vida = 0f; b.img.enabled = false; }
        }
    }

    private void NuevaBrasa()
    {
        Brasa b = brasas.Find(x => x.vida <= 0f);
        if (b == null)
        {
            if (brasas.Count >= 140) return;
            Image img = Caja("Brasa", lienzo.Find("Brasas"), Color.clear);
            img.sprite = RuedaImbuir.Circulo();
            b = new Brasa { rt = img.rectTransform, img = img };
            brasas.Add(b);
        }
        float ancho = lienzo.rect.width, alto = lienzo.rect.height;
        b.rt.anchoredPosition = new Vector2(Random.Range(-ancho * 0.45f, ancho * 0.45f), -alto * 0.5f - 10f);
        b.vel = new Vector2(Random.Range(-15f, 15f), Random.Range(70f, 150f));
        b.vida = Random.Range(5f, 11f);
        b.t = 0f;
        b.fase = Random.value * 10f;
        b.tamano = Random.Range(3f, 9f);
        b.color = Color.Lerp(colorBrasaA, colorBrasaB, Random.value);
        b.img.enabled = true;
    }

    // Luz del fuego en el titulo: un parpadeo lento de fondo y, sobre todo, las
    // brasas que pasan cerca.
    private void ActualizarLuz()
    {
        float t = Time.unscaledTime;
        float fondo = Mathf.PerlinNoise(t * 0.6f, semilla) * 0.5f + Mathf.PerlinNoise(t * 2.3f, semilla + 7f) * 0.2f;
        Vector2 centro = textoTitulo.rectTransform.anchoredPosition;
        float cerca = 0f;
        foreach (Brasa b in brasas)
        {
            if (b.vida <= 0f) continue;
            float d = Vector2.Distance(b.rt.anchoredPosition, centro);
            if (d < 420f) cerca += (1f - d / 420f) * b.img.color.a * 0.2f;
        }
        float luz = Mathf.Clamp01((fondo * 0.35f + cerca * 0.6f) * intensidadLuz);
        textoTitulo.color = Color.Lerp(colorTitulo, colorTituloIluminado, luz);
        resplandor.color = new Color(1f, 0.55f, 0.2f, luz * 0.25f);
        brilloSuelo.color = new Color(1f, 0.35f, 0.1f, 0.03f + fondo * 0.035f);
    }

    // ------------------------------------------------------------------ Acciones

    private void NuevaPartida()
    {
        if (saliendo) return;
        saliendo = true;
        Partida.Nueva(primerNivel);
        PantallaCarga.Cargar(primerNivel, 1.2f);
    }

    private void CargarPartida(Partida.Datos d)
    {
        if (saliendo) return;
        saliendo = true;
        Partida.Cargar(d);
        PantallaCarga.Cargar(d.escena, 1.2f);
    }

    private void Salir()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void Mostrar(GameObject panel)
    {
        panelPrincipal.SetActive(panel == panelPrincipal);
        panelCargar.SetActive(panel == panelCargar);
        panelOpciones.SetActive(panel == panelOpciones);
        panelControles.SetActive(panel == panelControles);
        if (panel == panelCargar) RellenarPartidas();
        SonidoMenu.Silenciar();
        SeleccionarPrimero();
    }

    private void SeleccionarPrimero()
    {
        if (EventSystem.current == null) return;
        GameObject g = panelPrincipal.activeSelf ? primeroPrincipal : PrimerBoton(panelCargar.activeSelf ? panelCargar : panelOpciones.activeSelf ? panelOpciones : panelControles);
        EventSystem.current.SetSelectedGameObject(g);
    }

    private static GameObject PrimerBoton(GameObject panel)
    {
        Selectable s = panel.GetComponentInChildren<Selectable>();
        return s != null ? s.gameObject : null;
    }

    // ------------------------------------------------------------------ Construccion

    private void Construir()
    {
        GameObject go = new GameObject("Lienzo");
        go.transform.SetParent(transform, false);
        Canvas c = go.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler cs = go.AddComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920, 1080);
        cs.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        lienzo = (RectTransform)go.transform;
        grupo = go.AddComponent<CanvasGroup>();
        grupo.alpha = 0f;

        Image negro = Caja("Fondo", lienzo, Color.black);
        Estirar(negro.rectTransform);

        // Resplandor del fuego desde abajo, apenas visible.
        brilloSuelo = Caja("BrilloSuelo", lienzo, Color.clear);
        brilloSuelo.sprite = Degradado();
        RectTransform rb = brilloSuelo.rectTransform;
        rb.anchorMin = new Vector2(0f, 0f); rb.anchorMax = new Vector2(1f, 0.45f);
        rb.offsetMin = rb.offsetMax = Vector2.zero;

        resplandor = Caja("Resplandor", lienzo, Color.clear);
        resplandor.sprite = Suave();
        resplandor.rectTransform.sizeDelta = new Vector2(1500f, 420f);
        resplandor.rectTransform.anchoredPosition = new Vector2(0f, 220f);

        RectTransform capaBrasas = new GameObject("Brasas").AddComponent<RectTransform>();
        capaBrasas.SetParent(lienzo, false);
        Estirar(capaBrasas);

        textoTitulo = Texto(titulo, lienzo, tamanoTitulo, colorTitulo);
        textoTitulo.characterSpacing = 28f;
        textoTitulo.rectTransform.sizeDelta = new Vector2(1800f, 220f);
        textoTitulo.rectTransform.anchoredPosition = new Vector2(0f, 220f);

        // Linea fina bajo el titulo, que se desvanece por los lados.
        Image linea = Caja("Linea", lienzo, new Color(0.75f, 0.7f, 0.65f, 0.5f));
        linea.sprite = LineaDifuminada();
        linea.rectTransform.sizeDelta = new Vector2(1300f, 2f);
        linea.rectTransform.anchoredPosition = new Vector2(0f, 128f);

        if (!string.IsNullOrEmpty(subtitulo))
        {
            TextMeshProUGUI sub = Texto(subtitulo, lienzo, 40f, new Color(0.6f, 0.57f, 0.55f));
            sub.characterSpacing = 14f;
            sub.rectTransform.anchoredPosition = new Vector2(0f, 90f);
        }

        panelPrincipal = Panel("Principal");
        VerticalLayoutGroup vl = Columna(panelPrincipal.transform, new Vector2(0f, -120f), 460f, 8f);
        primeroPrincipal = Opcion("Nueva Partida", vl.transform, NuevaPartida);
        Opcion("Cargar Partida", vl.transform, () => Mostrar(panelCargar));
        Opcion("Opciones", vl.transform, () => Mostrar(panelOpciones));
        Opcion("Controles", vl.transform, () => Mostrar(panelControles));
        Opcion("Salir", vl.transform, Salir);

        TextMeshProUGUI pie = Texto("Enter / Clic: aceptar     Esc / Clic derecho: volver", lienzo, 20f, new Color(0.45f, 0.42f, 0.4f));
        pie.rectTransform.anchorMin = pie.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        pie.rectTransform.anchoredPosition = new Vector2(0f, 40f);

        ConstruirCargar();
        ConstruirOpciones();
        ConstruirControles();
    }

    private GameObject Panel(string nombre)
    {
        RectTransform r = new GameObject(nombre).AddComponent<RectTransform>();
        r.SetParent(lienzo, false);
        Estirar(r);
        return r.gameObject;
    }

    private VerticalLayoutGroup Columna(Transform padre, Vector2 pos, float ancho, float espacio)
    {
        VerticalLayoutGroup vl = new GameObject("Columna").AddComponent<VerticalLayoutGroup>();
        vl.transform.SetParent(padre, false);
        RectTransform r = (RectTransform)vl.transform;
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.pivot = new Vector2(0.5f, 1f);
        r.anchoredPosition = pos;
        r.sizeDelta = new Vector2(ancho, 0f);
        vl.spacing = espacio;
        vl.childAlignment = TextAnchor.UpperCenter;
        vl.childControlHeight = false;
        vl.childControlWidth = true;
        vl.childForceExpandHeight = false;
        ContentSizeFitter f = vl.gameObject.AddComponent<ContentSizeFitter>();
        f.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return vl;
    }

    // Opcion de texto al estilo Souls: sin caja; la elegida lleva una franja oscura
    // detras y el texto se aclara.
    private GameObject Opcion(string texto, Transform padre, UnityEngine.Events.UnityAction accion, float alto = 54f, float tamano = 32f)
    {
        Image fondo = Caja("Opcion_" + texto, padre, new Color(0f, 0f, 0f, 0f));
        fondo.raycastTarget = true;
        fondo.rectTransform.sizeDelta = new Vector2(0f, alto);
        Image franja = Caja("Franja", fondo.transform, new Color(0.45f, 0.18f, 0.08f, 0.55f));
        franja.sprite = LineaDifuminada();
        Estirar(franja.rectTransform);
        franja.enabled = false;
        TextMeshProUGUI t = Texto(texto, fondo.transform, tamano, new Color(0.62f, 0.58f, 0.55f));
        t.characterSpacing = 4f;
        Estirar(t.rectTransform);

        Button b = fondo.gameObject.AddComponent<Button>();
        b.transition = Selectable.Transition.None;
        b.onClick.AddListener(() => { if (saliendo) return; SonidoMenu.Confirmar(); accion(); });
        OpcionMenu om = fondo.gameObject.AddComponent<OpcionMenu>();
        om.franja = franja;
        om.texto = t;
        return fondo.gameObject;
    }

    private void Volver(Transform padre)
    {
        Opcion("Volver", padre, () => Mostrar(panelPrincipal), 50f, 28f);
    }

    // ------------------------------------------------------------------ Cargar

    private void ConstruirCargar()
    {
        panelCargar = Panel("Cargar");
        TextMeshProUGUI cab = Texto("CARGAR PARTIDA", panelCargar.transform, 44f, new Color(0.75f, 0.7f, 0.65f));
        cab.characterSpacing = 12f;
        cab.rectTransform.anchoredPosition = new Vector2(0f, 60f);
        listaPartidas = Desplazable(panelCargar.transform, new Vector2(0f, 20f), new Vector2(1100f, 420f));
        VerticalLayoutGroup pie = Columna(panelCargar.transform, new Vector2(0f, -420f), 360f, 0f);
        Volver(pie.transform);
    }

    private void RellenarPartidas()
    {
        for (int i = listaPartidas.childCount - 1; i >= 0; i--) Destroy(listaPartidas.GetChild(i).gameObject);
        List<Partida.Datos> partidas = Partida.Listar();
        if (partidas.Count == 0)
        {
            TextMeshProUGUI v = Texto("No hay partidas guardadas", listaPartidas, 28f, new Color(0.5f, 0.47f, 0.45f));
            v.rectTransform.sizeDelta = new Vector2(0f, 80f);
            return;
        }
        foreach (Partida.Datos d in partidas)
        {
            Partida.Datos datos = d;
            GameObject fila = Opcion("", listaPartidas, () => CargarPartida(datos), 104f, 26f);
            TextMeshProUGUI t = fila.GetComponentInChildren<TextMeshProUGUI>();
            t.alignment = TextAlignmentOptions.MidlineLeft;
            t.margin = new Vector4(30f, 0f, 170f, 0f);
            t.richText = true;
            t.text = $"<size=32><color=#d8cfc6>{Partida.NombreNivel(d.escena)}</color></size>  <color=#8a817a>—  {d.lugar}</color>\n" +
                     $"<size=22><color=#8f8780>Nivel {d.NivelPersonaje}   ·   {d.almas:N0} almas   ·   Tiempo jugado {Partida.Tiempo(d.segundosJugados)}   ·   {d.fecha}</color></size>";

            // Borrar (hay que pulsarlo dos veces).
            Image borrar = Caja("Borrar", fila.transform, new Color(0.25f, 0.08f, 0.08f, 0.8f));
            borrar.raycastTarget = true;
            RectTransform rb = borrar.rectTransform;
            rb.anchorMin = rb.anchorMax = new Vector2(1f, 0.5f);
            rb.pivot = new Vector2(1f, 0.5f);
            rb.sizeDelta = new Vector2(140f, 44f);
            rb.anchoredPosition = new Vector2(-16f, 0f);
            TextMeshProUGUI tb = Texto("Borrar", borrar.transform, 22f, new Color(0.85f, 0.6f, 0.55f));
            Estirar(tb.rectTransform);
            Button bb = borrar.gameObject.AddComponent<Button>();
            bool confirmar = false;
            bb.onClick.AddListener(() =>
            {
                if (!confirmar) { confirmar = true; tb.text = "¿Seguro?"; return; }
                Partida.Borrar(datos);
                Sonido.Reproducir("menu_cancelar", 0.6f);
                RellenarPartidas();
                SeleccionarPrimero();
            });
        }
    }

    // ------------------------------------------------------------------ Opciones

    private void ConstruirOpciones()
    {
        panelOpciones = Panel("Opciones");
        TextMeshProUGUI cab = Texto("OPCIONES", panelOpciones.transform, 44f, new Color(0.75f, 0.7f, 0.65f));
        cab.characterSpacing = 12f;
        cab.rectTransform.anchoredPosition = new Vector2(0f, 60f);
        VerticalLayoutGroup vl = Columna(panelOpciones.transform, new Vector2(0f, 10f), 700f, 18f);
        Deslizador("Volumen general", vl.transform, 0f, 1f, ControlVolumen.General, v => ControlVolumen.General = v);
        Deslizador("Volumen de la música", vl.transform, 0f, 1f, ControlVolumen.Musica, v => ControlVolumen.Musica = v);
        Deslizador("Volumen de los efectos", vl.transform, 0f, 1f, ControlVolumen.Efectos, v => ControlVolumen.Efectos = v);
        Deslizador("Brillo", vl.transform, 0.5f, 1.5f, ControlBrillo.Brillo, v => ControlBrillo.Brillo = v);
        Volver(vl.transform);
    }

    private void Deslizador(string etiqueta, Transform padre, float min, float max, float valor, System.Action<float> alCambiar)
    {
        RectTransform fila = new GameObject("Fila_" + etiqueta).AddComponent<RectTransform>();
        fila.SetParent(padre, false);
        fila.sizeDelta = new Vector2(0f, 70f);
        TextMeshProUGUI t = Texto(etiqueta, fila, 26f, new Color(0.7f, 0.66f, 0.62f));
        t.alignment = TextAlignmentOptions.TopLeft;
        t.rectTransform.anchorMin = new Vector2(0f, 1f); t.rectTransform.anchorMax = new Vector2(1f, 1f);
        t.rectTransform.pivot = new Vector2(0.5f, 1f);
        t.rectTransform.sizeDelta = new Vector2(0f, 32f);
        t.rectTransform.anchoredPosition = Vector2.zero;

        Slider s = new GameObject("Slider").AddComponent<Slider>();
        s.transform.SetParent(fila, false);
        RectTransform rs = (RectTransform)s.transform;
        rs.anchorMin = new Vector2(0f, 0f); rs.anchorMax = new Vector2(1f, 0f);
        rs.pivot = new Vector2(0.5f, 0f);
        rs.sizeDelta = new Vector2(0f, 16f);
        rs.anchoredPosition = new Vector2(0f, 6f);
        Image fondo = Caja("Fondo", s.transform, new Color(0.14f, 0.12f, 0.11f, 1f));
        Estirar(fondo.rectTransform);
        RectTransform area = new GameObject("Relleno").AddComponent<RectTransform>();
        area.SetParent(s.transform, false);
        Estirar(area);
        Image relleno = Caja("Barra", area, new Color(0.6f, 0.3f, 0.12f, 1f));
        Estirar(relleno.rectTransform);
        RectTransform zonaAsa = new GameObject("ZonaAsa").AddComponent<RectTransform>();
        zonaAsa.SetParent(s.transform, false);
        Estirar(zonaAsa);
        Image asa = Caja("Asa", zonaAsa, new Color(0.9f, 0.8f, 0.7f, 1f));
        asa.raycastTarget = true;
        asa.rectTransform.sizeDelta = new Vector2(16f, 12f);
        s.fillRect = relleno.rectTransform;
        s.handleRect = asa.rectTransform;
        s.targetGraphic = asa;
        ColorBlock cb = s.colors;
        cb.selectedColor = new Color(1.6f, 1.2f, 0.9f);
        cb.highlightedColor = new Color(1.3f, 1.1f, 0.9f);
        s.colors = cb;
        s.minValue = min;
        s.maxValue = max;
        s.value = valor;
        s.onValueChanged.AddListener(v => alCambiar(v));
    }

    // ------------------------------------------------------------------ Controles

    private struct Fila { public string icono, tecla, texto; }

    private static Fila F(string icono, string tecla, string texto) => new Fila { icono = icono, tecla = tecla, texto = texto };

    // Lo que hay en el juego ahora mismo (revisado en PlayerControler, GatherInput,
    // AgarreCornisa, ArmaImbuida, ReservaPociones, Hoguera y EstadosEnemigo).
    private static readonly (string categoria, Fila[] filas)[] Controles =
    {
        ("MOVIMIENTO", new[]
        {
            F("ctrl_mover", "A / D", "Moverse"),
            F("ctrl_correr", "Shift (mantener)", "Correr. Gasta estamina mientras corres"),
            F("ctrl_saltar", "Espacio", "Saltar"),
            F("ctrl_doble", "Espacio en el aire", "Doble salto (uno por salto; se recarga al tocar suelo o pared)"),
            F("ctrl_pared", "Contra una pared, en el aire", "Deslizarse por la pared (S: bajar más rápido)  ·  Espacio: salto de pared"),
            F("ctrl_correr_pared", "Shift contra una pared", "Correr por la pared: subes un tramo. Gasta estamina"),
            F("ctrl_cornisa", "Saltar hacia un borde alto", "Agarre de cornisa: te enganchas al borde y subes de un tirón"),
            F("ctrl_esquiva", "C", "Barrido: esquiva con un instante de invulnerabilidad. Gasta estamina"),
        }),
        ("COMBATE", new[]
        {
            F("ctrl_espada", "Clic izquierdo", "Combo de espada (hasta 3 golpes seguidos). Cada golpe gasta estamina"),
            F("ctrl_aereo", "Clic izquierdo en el aire", "Golpes aéreos. Si aciertas, te quedas suspendido y puedes encadenar"),
            F("ctrl_lanzador", "W + clic izquierdo", "Lanzador: manda al enemigo hacia arriba"),
            F("ctrl_estocada", "S + clic izquierdo en el aire", "Estocada hacia abajo: caes con fuerza y rebotas en los enemigos. Gasta estamina"),
            F("ctrl_bloqueo", "Clic derecho (mantener)", "Bloquear: recibes solo una parte del daño, a cambio de estamina"),
            F("ctrl_parry", "Clic derecho justo antes del golpe", "Parry: anula el golpe y aturde al enemigo. Si lo pulsas sin parar, la ventana se acorta"),
            F("elemento_0", "E", "Rueda de imbuir: elige un elemento para la espada (25 de maná, dura 75 s)"),
        }),
        ("OBJETOS", new[]
        {
            F("pocion", "Q", "Frasco de sangre: cura vida. Tiene varias cargas y se recargan al descansar en la hoguera"),
            F("pocion_mana", "R", "Frasco de maná: una carga (1/1). Es la única forma de recuperar maná; se recarga en la hoguera"),
            F("ctrl_interactuar", "F", "Interactuar: usar hogueras y abrir cofres"),
            F("objeto_piedra", "—", "Piedra de forja: mejora la espada en la hoguera. Sale de cofres escondidos y de los jefes"),
            F("objeto_lagrima", "—", "Lágrima sagrada: mejora los dos frascos en la hoguera. Sale de cofres escondidos y de los jefes"),
            F("cofre_especial", "—", "Los cofres con un brillo dorado suave guardan objetos de mejora"),
            F("almas", "—", "Almas: las sueltan los enemigos. Se gastan en la hoguera para subir de nivel"),
            F("ctrl_mancha", "—", "Al morir, tus almas se quedan donde caíste. Recógelas antes de volver a morir"),
        }),
        ("HOGUERAS Y MENÚS", new[]
        {
            F("ctrl_hoguera", "F junto a una hoguera", "Abre su menú: Descansar (cura, rellena los frascos y guarda), Subir de nivel y estadísticas, y Mejorar equipamiento"),
            F("stat_resgolpes", "Resistencias", "Resistencia a golpes: reduce el daño físico. Resistencia a hechizos: reduce el mágico. Nunca más de un 55 %"),
            F("ctrl_rueda", "En la rueda de imbuir", "Ratón, WASD o 1-5: elegir  ·  Clic o E: imbuir  ·  Clic derecho o Esc: cancelar"),
            F("ctrl_pausa", "Esc", "Pausa: opciones, destrabar al personaje, volver a la última hoguera o salir al menú"),
        }),
        ("BARRAS Y ESTADOS", new[]
        {
            F("ctrl_vida", "Barra roja", "Vida. Las barras se alargan al subir su estadística"),
            F("stat_estamina", "Barra verde", "Estamina: la gastan correr, atacar, esquivar y bloquear. Se recupera sola"),
            F("stat_mana", "Barra azul", "Maná: lo gasta imbuir la espada. Solo vuelve con el frasco de maná"),
            F("elemento_3", "Anillo del rombo", "Con la espada imbuida, el anillo se vacía con el tiempo que le queda"),
            F("jugador_sangrado", "Bajo tus barras", "Sangrado, congelación y quemadura: los ataques especiales de los jefes llenan su barra. Si se llena, te afecta; si dejas de recibirlos, baja sola"),
            F("estado_quemado", "Fuego", "Quemado: el enemigo pierde vida un rato"),
            F("estado_lento", "Hielo", "Lento: se mueve más despacio. Varios golpes seguidos lo congelan"),
            F("estado_aturdido", "Sagrado", "Aturdido: a veces lo deja quieto un momento"),
            F("estado_sangrado", "Sangrado", "Cada golpe acumula sangrado; al llenarse, el enemigo pierde de golpe parte de su vida"),
            F("estado_drenado", "Oscuridad", "Drenado: te curas con parte del daño que haces"),
            F("ctrl_estados", "Bajo su barra de vida", "Los iconos muestran los estados del enemigo. Parpadean cuando están por acabar"),
        }),
    };

    private void ConstruirControles()
    {
        panelControles = Panel("Controles");
        TextMeshProUGUI cab = Texto("CONTROLES", panelControles.transform, 44f, new Color(0.75f, 0.7f, 0.65f));
        cab.characterSpacing = 12f;
        cab.rectTransform.anchoredPosition = new Vector2(0f, 60f);
        RectTransform lista = Desplazable(panelControles.transform, new Vector2(0f, 20f), new Vector2(1500f, 440f));
        RecursosRPG rec = RecursosRPG.Get();
        foreach (var (categoria, filas) in Controles)
        {
            TextMeshProUGUI tc = Texto(categoria, lista, 26f, new Color(0.85f, 0.55f, 0.3f));
            tc.characterSpacing = 8f;
            tc.alignment = TextAlignmentOptions.BottomLeft;
            tc.rectTransform.sizeDelta = new Vector2(0f, 56f);
            foreach (Fila f in filas)
            {
                RectTransform r = new GameObject("Fila").AddComponent<RectTransform>();
                r.SetParent(lista, false);
                r.sizeDelta = new Vector2(0f, 52f);
                Image ico = Caja("Icono", r, Color.white);
                ico.sprite = rec.Icono(f.icono);
                ico.enabled = ico.sprite != null;
                ico.preserveAspect = true;
                RectTransform ri = ico.rectTransform;
                ri.anchorMin = ri.anchorMax = new Vector2(0f, 0.5f);
                ri.pivot = new Vector2(0f, 0.5f);
                ri.sizeDelta = new Vector2(44f, 44f);
                ri.anchoredPosition = new Vector2(8f, 0f);
                TextMeshProUGUI tt = Texto(f.tecla, r, 22f, new Color(0.9f, 0.84f, 0.76f));
                tt.fontStyle = FontStyles.Bold;
                tt.alignment = TextAlignmentOptions.MidlineLeft;
                tt.rectTransform.anchorMin = tt.rectTransform.anchorMax = new Vector2(0f, 0.5f);
                tt.rectTransform.pivot = new Vector2(0f, 0.5f);
                tt.rectTransform.sizeDelta = new Vector2(360f, 50f);
                tt.rectTransform.anchoredPosition = new Vector2(66f, 0f);
                TextMeshProUGUI td = Texto(f.texto, r, 22f, new Color(0.68f, 0.64f, 0.6f));
                td.alignment = TextAlignmentOptions.MidlineLeft;
                td.enableWordWrapping = true;
                RectTransform rd = td.rectTransform;
                rd.anchorMin = new Vector2(0f, 0f); rd.anchorMax = new Vector2(1f, 1f);
                rd.offsetMin = new Vector2(440f, 0f); rd.offsetMax = new Vector2(-10f, 0f);
            }
        }
        VerticalLayoutGroup pie = Columna(panelControles.transform, new Vector2(0f, -420f), 360f, 0f);
        Volver(pie.transform);
    }

    // Lista con barra de desplazamiento (rueda del raton o arrastrando).
    private RectTransform Desplazable(Transform padre, Vector2 posArriba, Vector2 tamano)
    {
        RectTransform vista = new GameObject("Vista").AddComponent<RectTransform>();
        vista.SetParent(padre, false);
        vista.anchorMin = vista.anchorMax = new Vector2(0.5f, 0.5f);
        vista.pivot = new Vector2(0.5f, 1f);
        vista.anchoredPosition = posArriba;
        vista.sizeDelta = tamano;
        Image fondoVista = vista.gameObject.AddComponent<Image>();
        fondoVista.color = new Color(0.03f, 0.025f, 0.02f, 0.6f);
        vista.gameObject.AddComponent<RectMask2D>();
        ScrollRect sr = vista.gameObject.AddComponent<ScrollRect>();
        sr.horizontal = false;
        sr.movementType = ScrollRect.MovementType.Clamped;
        sr.scrollSensitivity = 40f;

        RectTransform contenido = new GameObject("Contenido").AddComponent<RectTransform>();
        contenido.SetParent(vista, false);
        contenido.anchorMin = new Vector2(0f, 1f); contenido.anchorMax = new Vector2(1f, 1f);
        contenido.pivot = new Vector2(0.5f, 1f);
        contenido.sizeDelta = Vector2.zero;
        VerticalLayoutGroup vl = contenido.gameObject.AddComponent<VerticalLayoutGroup>();
        vl.padding = new RectOffset(20, 20, 10, 20);
        vl.spacing = 6f;
        vl.childControlHeight = false;
        vl.childControlWidth = true;
        vl.childForceExpandHeight = false;
        ContentSizeFitter f = contenido.gameObject.AddComponent<ContentSizeFitter>();
        f.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        sr.viewport = vista;
        sr.content = contenido;
        vista.gameObject.AddComponent<SeguirSeleccion>().scroll = sr;
        return contenido;
    }

    // ------------------------------------------------------------------ Ayudas

    private static Image Caja(string nombre, Transform padre, Color color)
    {
        Image i = new GameObject(nombre).AddComponent<Image>();
        i.transform.SetParent(padre, false);
        i.color = color;
        i.raycastTarget = false;
        return i;
    }

    private TextMeshProUGUI Texto(string texto, Transform padre, float tamano, Color color)
    {
        TextMeshProUGUI t = new GameObject("Texto").AddComponent<TextMeshProUGUI>();
        t.transform.SetParent(padre, false);
        if (Fuente != null) t.font = Fuente;
        t.text = texto;
        t.fontSize = tamano;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.enableWordWrapping = false;
        t.raycastTarget = false;
        t.rectTransform.sizeDelta = new Vector2(1200f, tamano * 1.4f);
        return t;
    }

    private static void Estirar(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
    }

    private static Sprite lineaDifuminada, degradado;
    private static Sprite suave;

    // Mancha de luz redonda que se desvanece hacia el borde.
    private static Sprite Suave()
    {
        if (suave != null) return suave;
        const int n = 128;
        Texture2D t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n * 0.5f, n * 0.5f)) / (n * 0.5f);
            t.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Pow(Mathf.Clamp01(1f - d), 2.2f)));
        }
        t.Apply();
        suave = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
        return suave;
    }


    // Franja que se desvanece hacia los dos lados.
    private static Sprite LineaDifuminada()
    {
        if (lineaDifuminada != null) return lineaDifuminada;
        const int n = 256;
        Texture2D t = new Texture2D(n, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int x = 0; x < n; x++)
        {
            float d = Mathf.Abs(x - n * 0.5f) / (n * 0.5f);
            t.SetPixel(x, 0, new Color(1f, 1f, 1f, Mathf.SmoothStep(1f, 0f, d)));
        }
        t.Apply();
        lineaDifuminada = Sprite.Create(t, new Rect(0, 0, n, 1), new Vector2(0.5f, 0.5f));
        return lineaDifuminada;
    }

    // De abajo (color) a arriba (nada).
    private static Sprite Degradado()
    {
        if (degradado != null) return degradado;
        const int n = 128;
        Texture2D t = new Texture2D(1, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < n; y++) t.SetPixel(0, y, new Color(1f, 1f, 1f, Mathf.Pow(1f - y / (float)n, 2f)));
        t.Apply();
        degradado = Sprite.Create(t, new Rect(0, 0, 1, n), new Vector2(0.5f, 0.5f));
        return degradado;
    }

    // La opcion elegida: franja detras y texto claro. Pasar el raton la elige.
    private class OpcionMenu : MonoBehaviour, IPointerEnterHandler, ISelectHandler, IDeselectHandler
    {
        public Image franja;
        public TextMeshProUGUI texto;
        private Color normal;
        private bool guardado;

        public void OnPointerEnter(PointerEventData e)
        {
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != gameObject)
                EventSystem.current.SetSelectedGameObject(gameObject);
        }

        public void OnSelect(BaseEventData e)
        {
            if (!guardado && texto != null) { normal = texto.color; guardado = true; }
            if (franja != null) franja.enabled = true;
            if (texto != null) texto.color = new Color(0.95f, 0.9f, 0.85f);
            SonidoMenu.Mover();
        }

        public void OnDeselect(BaseEventData e)
        {
            if (franja != null) franja.enabled = false;
            if (texto != null) texto.color = normal;
        }
    }

    // Con teclado o mando, la lista baja sola hasta la opcion elegida.
    private class SeguirSeleccion : MonoBehaviour
    {
        public ScrollRect scroll;

        private void Update()
        {
            GameObject sel = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (sel == null || !sel.transform.IsChildOf(scroll.content)) return;
            RectTransform r = (RectTransform)sel.transform;
            RectTransform vista = scroll.viewport;
            Vector3[] esquinas = new Vector3[4];
            r.GetWorldCorners(esquinas);
            Vector3[] ev = new Vector3[4];
            vista.GetWorldCorners(ev);
            float arriba = esquinas[1].y, abajo = esquinas[0].y;
            if (arriba > ev[1].y) scroll.content.position -= new Vector3(0f, arriba - ev[1].y, 0f);
            else if (abajo < ev[0].y) scroll.content.position += new Vector3(0f, ev[0].y - abajo, 0f);
        }
    }
}
