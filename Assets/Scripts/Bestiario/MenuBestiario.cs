using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Bestiario: los enemigos normales (no los jefes), agrupados por zona. Es de todo
// el juego (globales.json), como los logros: se ve desde el menu principal sin
// partida y desde la pausa de la partida normal.
//   - Izquierda: la lista por zonas, con cuantos llevas descubiertos. Los no
//     vistos salen como silueta con "???". Las variantes de color y los elites
//     van debajo de su enemigo base.
//   - Derecha: la ficha del elegido (retrato, zona, rol, derrotas, descripcion,
//     almas, patrones y reaccion a cada elemento), segun lo descubierto.
// Navegacion por capas: pasar el raton resalta, clic o Enter selecciona; atras
// (clic derecho o Esc) vuelve de la ficha a la lista y de la lista a quien lo abrio.
public class MenuBestiario : MonoBehaviour
{
    private class Fila
    {
        public FichaBestiario.Entrada entrada;
        public Button boton;
        public OpcionEstilo estilo;
        public Image retrato;
        public TextMeshProUGUI texto;
        public PuntoNuevo nuevo;
        public bool sangrada;
    }

    private const string Gris = "#8f8780", Verde = "#9fe0a0", Rojo = "#e0776c", Oro = "#f0d49a", Nuevo = "#ffb84d";

    private readonly List<Fila> filas = new List<Fila>();
    private readonly Dictionary<int, TextMeshProUGUI> cabecerasZona = new Dictionary<int, TextMeshProUGUI>();
    private readonly CapasMenu capas = new CapasMenu();
    private readonly GrupoMarcado grupo = new GrupoMarcado();
    private readonly Dictionary<string, Material> materiales = new Dictionary<string, Material>();
    private FichaBestiario.Entrada elegida;
    private System.Action volver;
    private bool conClicDerecho;

    private TextMeshProUGUI total, nombre, subtitulo, sinBestiario;
    private RectTransform ficha, contenido;
    private CanvasGroup grupoFicha;
    private ScrollRect scrollLista, scrollFicha;
    private GameObject placeholder;
    private Image retrato;
    private Button botonVolver;
    private Coroutine transicion;

    // "clicDerecho": en la pausa el clic derecho es el bloqueo; alli no se usa.
    public static MenuBestiario Construir(Transform lienzo, System.Action volver, bool clicDerecho = true)
    {
        RectTransform raiz = new GameObject("Bestiario", typeof(RectTransform)).GetComponent<RectTransform>();
        raiz.SetParent(lienzo, false);
        EstiloMenu.Estirar(raiz);
        MenuBestiario m = raiz.gameObject.AddComponent<MenuBestiario>();
        m.volver = volver;
        m.conClicDerecho = clicDerecho;
        m.Montar(raiz);
        return m;
    }

    public void Abrir()
    {
        Globales.GuardarPendiente();
        capas.Vaciar();
        foreach (Fila f in filas) PintarFila(f);
        PintarTotales();
        Deseleccionar();
    }

    public GameObject Primero()
    {
        if (elegida != null) return filas.First(f => f.entrada == elegida).boton.gameObject;
        return filas.Count > 0 ? filas[0].boton.gameObject : botonVolver.gameObject;
    }

    // Un paso atras. False si ya estaba en la lista.
    public bool Atras() => capas.Atras();

    private void Update()
    {
        if (retrato != null && elegida != null)
        {
            float t = Time.unscaledTime;
            retrato.rectTransform.localScale = new Vector3(1f - 0.006f * Mathf.Sin(t * 1.9f), 1f + 0.014f * Mathf.Sin(t * 1.9f), 1f);
        }
        // RePag / AvPag o el stick derecho mueven la ficha.
        if (scrollFicha != null && elegida != null)
        {
            float eje = 0f;
            Keyboard k = Keyboard.current;
            Gamepad g = Gamepad.current;
            if (k != null && k.pageDownKey.isPressed) eje -= 1f;
            if (k != null && k.pageUpKey.isPressed) eje += 1f;
            if (g != null) eje += g.rightStick.ReadValue().y;
            float alto = contenido.rect.height - scrollFicha.viewport.rect.height;
            if (Mathf.Abs(eje) > 0.2f && alto > 0f)
                scrollFicha.verticalNormalizedPosition = Mathf.Clamp01(scrollFicha.verticalNormalizedPosition + eje * Time.unscaledDeltaTime * 700f / alto);
        }
    }

    private void OnDestroy()
    {
        foreach (Material m in materiales.Values) if (m != null) Destroy(m);
    }

    // ------------------------------------------------------------------ Capas

    private void Seleccionar(FichaBestiario.Entrada e)
    {
        bool yaDentro = elegida != null;
        elegida = e;
        if (!yaDentro) capas.Entrar(Deseleccionar);
        Fila fila = filas.First(f => f.entrada == e);
        grupo.Marcar(fila.estilo);
        placeholder.SetActive(false);
        ficha.gameObject.SetActive(true);
        PintarFicha();
        Bitacora.MarcarVisto(e.Id);
        PintarFila(fila);
        PintarTotales();
        if (transicion != null) StopCoroutine(transicion);
        ficha.anchoredPosition = Vector2.zero;
        transicion = StartCoroutine(TransicionMenu.Entrar(ficha, grupoFicha));
    }

    private void Deseleccionar()
    {
        FichaBestiario.Entrada antes = elegida;
        elegida = null;
        grupo.Desmarcar();
        ficha.gameObject.SetActive(false);
        placeholder.SetActive(true);
        GameObject foco = filas.FirstOrDefault(f => f.entrada == antes)?.boton.gameObject ?? Primero();
        SonidoMenu.Silenciar();
        EventSystem.current?.SetSelectedGameObject(foco);
    }

    private void Volver()
    {
        capas.Vaciar();
        if (elegida != null) Deseleccionar();
        volver?.Invoke();
    }

    // ------------------------------------------------------------------ Ficha

    private static bool Visto(FichaBestiario.Entrada e) => Bitacora.Tiene(Bitacora.ClaveVisto(e.Id));
    private static string MarcaNuevo(string clave) => Bitacora.Nuevo(clave) ? $"  <size=70%><color={Nuevo}>NUEVO</color></size>" : "";

    private void PintarFicha()
    {
        FichaBestiario.Entrada e = elegida;
        DefinicionEnemigo d = e.enemigo;
        bool visto = Visto(e);
        PonerRetrato(retrato, e, visto);
        nombre.text = visto ? e.Nombre.ToUpper() : "???";
        FichaBestiario b = FichaBestiario.Get();
        int derrotas = Bitacora.Derrotas(e.Id);
        string tipo = d.EsElite ? $"<color={Oro}>Élite</color>" : d.EsVariante ? "Variante" : Rol(d.rol);
        subtitulo.text = visto
            ? $"{b.NombreZona(d.zona)}   ·   {tipo}   ·   Derrotados: <b>{derrotas}</b>"
            : $"{b.NombreZona(d.zona)}   ·   <color={Gris}>Aún no lo has visto</color>";

        for (int i = contenido.childCount - 1; i >= 0; i--) { GameObject h = contenido.GetChild(i).gameObject; h.transform.SetParent(null); Destroy(h); }
        if (!visto)
        {
            Linea($"<i>Algo acecha en {b.NombreZona(d.zona)}. Encuéntralo para saber qué es.</i>", null, 23, true);
            scrollFicha.verticalNormalizedPosition = 1f;
            return;
        }

        // Relacion con su enemigo base (variantes y elites).
        if (e.relacionado != null)
        {
            FichaBestiario.Entrada basee = b.Buscar(e.relacionado.name);
            string nombreBase = basee != null && Visto(basee) ? basee.Nombre : "???";
            Linea($"<color={Gris}>{(d.EsElite ? "Élite de" : "Variante de")}:</color> {nombreBase}", null, 21, false);
        }

        string kd = Bitacora.ClaveDerrotado(e.Id);
        if (Bitacora.Tiene(kd))
            Linea($"<b><color={Oro}>Descripción</color></b>{MarcaNuevo(kd)}\n<i>{e.descripcion}</i>\n<color={Gris}>Almas:</color> <b>{d.Almas:N0}</b>", null, 22, false);
        else
            Linea($"<b><color={Oro}>Descripción</color></b>\n<color={Gris}>??? — derrótalo para saber más.</color>", null, 22, false);

        string kp = Bitacora.ClavePatrones(e.Id);
        int umbral = Mathf.Max(1, e.derrotasParaPatrones);
        if (Bitacora.Tiene(kp))
            Linea($"<b><color={Oro}>Patrones de ataque</color></b>{MarcaNuevo(kp)}\n{e.patrones}", null, 22, false);
        else
            Linea($"<b><color={Oro}>Patrones de ataque</color></b>\n<color={Gris}>??? — derrotados {Mathf.Min(derrotas, umbral)}/{umbral}</color>", null, 22, false);

        Linea($"<b><color={Oro}>Reacción a los elementos</color></b>", null, 22, false);
        RecursosRPG r = RecursosRPG.Get();
        foreach (Elemento el in Elementos.Todos)
        {
            string k = Bitacora.ClaveElemento(e.Id, el);
            Sprite ico = r.Icono("elemento_" + (int)el);
            if (!Bitacora.Tiene(k)) { Linea($"<color={Gris}>{Elementos.Nombre(el)}: ???</color>", ico, 21, true); continue; }
            Linea($"{Elementos.Nombre(el)}: {Reaccion(d.AfinidadDe(el))}{MarcaNuevo(k)}", ico, 21, false);
        }
        scrollFicha.verticalNormalizedPosition = 1f;
    }

    private static string Reaccion(Afinidad a)
    {
        switch (a)
        {
            case Afinidad.Debil: return $"<color={Verde}>Débil</color>";
            case Afinidad.Resiste: return $"<color={Rojo}>Resiste</color>";
            case Afinidad.Inmune: return $"<color={Rojo}>Inmune</color>";
            default: return "Neutral";
        }
    }

    private static string Rol(RolEnemigo r)
    {
        switch (r)
        {
            case RolEnemigo.Debil: return "Débil";
            case RolEnemigo.Pesado: return "Pesado";
            case RolEnemigo.Elite: return "Élite";
            default: return "Común";
        }
    }

    // Retrato con el color de su variante; sin ver, una silueta negra.
    private void PonerRetrato(Image img, FichaBestiario.Entrada e, bool visto)
    {
        img.sprite = e.retrato;
        img.enabled = e.retrato != null;
        img.color = visto ? Color.white : new Color(0f, 0f, 0f, 0.92f);
        DefinicionEnemigo d = e.enemigo;
        if (d != null && d.EsVariante && visto)
        {
            if (!materiales.TryGetValue(e.Id, out Material m))
            {
                Shader s = Shader.Find("Sprites/Flash");
                m = s != null ? new Material(s) : null;
                if (m != null)
                {
                    m.SetFloat("_Tono", d.tono);
                    m.SetFloat("_Saturacion", d.saturacion);
                    m.SetFloat("_Brillo", d.brillo);
                }
                materiales[e.Id] = m;
            }
            if (m != null) { img.material = m; return; }
        }
        img.material = null;
    }

    private void Linea(string texto, Sprite icono, float tamano, bool apagada)
    {
        GameObject fila = new GameObject("Linea", typeof(RectTransform));
        fila.transform.SetParent(contenido, false);
        HorizontalLayoutGroup h = fila.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 14f;
        h.childControlWidth = h.childControlHeight = true;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = false;
        h.childAlignment = TextAnchor.MiddleLeft;
        if (icono != null)
        {
            Image ico = EstiloMenu.Caja("Icono", fila.transform, apagada ? new Color(1f, 1f, 1f, 0.35f) : Color.white);
            ico.sprite = icono;
            ico.preserveAspect = true;
            LayoutElement le = ico.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = le.minWidth = 36f;
            le.preferredHeight = le.minHeight = 36f;
        }
        TextMeshProUGUI t = EstiloMenu.Texto(texto, fila.transform, tamano, apagada ? EstiloMenu.TextoApagado : EstiloMenu.TextoElegido);
        t.alignment = TextAlignmentOptions.TopLeft;
        t.enableWordWrapping = true;
        t.lineSpacing = 2f;
        t.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
    }

    // ------------------------------------------------------------------ Lista

    private void PintarFila(Fila f)
    {
        bool visto = Visto(f.entrada);
        PonerRetrato(f.retrato, f.entrada, visto);
        string sangria = f.sangrada ? $"<color={Gris}>·</color> " : "";
        f.texto.text = visto
            ? $"{sangria}{f.entrada.Nombre}\n<size=68%><color={Gris}>Derrotados: {Bitacora.Derrotas(f.entrada.Id)}</color></size>"
            : $"{sangria}<color={Gris}>???</color>";
        f.nuevo.Poner(Bitacora.HayNuevo(f.entrada.Id));
    }

    private void PintarTotales()
    {
        FichaBestiario b = FichaBestiario.Get();
        if (b == null) return;
        List<FichaBestiario.Entrada> todas = b.Visibles.ToList();
        total.text = $"{todas.Count(Visto)}/{todas.Count} descubiertos";
        foreach (var kv in cabecerasZona)
        {
            List<FichaBestiario.Entrada> z = todas.Where(e => e.Zona == kv.Key).ToList();
            kv.Value.text = $"{b.NombreZona(kv.Key).ToUpper()}  <size=80%><color={Gris}>{z.Count(Visto)}/{z.Count}</color></size>";
        }
    }

    // ------------------------------------------------------------------ Construccion

    private void Montar(RectTransform raiz)
    {
        Image velo = EstiloMenu.Caja("Velo", raiz, new Color(0f, 0f, 0f, 0.9f));
        velo.raycastTarget = true;
        EstiloMenu.Estirar(velo.rectTransform);
        RectTransform panel = EstiloMenu.Panel(raiz, "BESTIARIO", new Vector2(1780f, 980f));
        total = EstiloMenu.Texto("", panel, 24, new Color(0.82f, 0.76f, 0.68f));
        total.fontStyle = FontStyles.Italic;
        EstiloMenu.Arriba(total.rectTransform, -104f, 30f);

        FichaBestiario b = FichaBestiario.Get();

        // Izquierda: la lista por zonas (con barra de desplazamiento).
        RectTransform izq = Zona(panel, 0f, 0.34f, 56f, 20f);
        RectTransform vista = new GameObject("Vista", typeof(RectTransform)).GetComponent<RectTransform>();
        vista.SetParent(izq, false);
        EstiloMenu.Estirar(vista);
        vista.offsetMin = new Vector2(0f, 70f);
        vista.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f);
        vista.gameObject.AddComponent<RectMask2D>();
        scrollLista = vista.gameObject.AddComponent<ScrollRect>();
        scrollLista.horizontal = false;
        scrollLista.movementType = ScrollRect.MovementType.Clamped;
        scrollLista.scrollSensitivity = 40f;
        RectTransform lista = new GameObject("Lista", typeof(RectTransform)).GetComponent<RectTransform>();
        lista.SetParent(vista, false);
        lista.anchorMin = new Vector2(0f, 1f); lista.anchorMax = new Vector2(1f, 1f);
        lista.pivot = new Vector2(0.5f, 1f);
        lista.sizeDelta = Vector2.zero;
        VerticalLayoutGroup vl = lista.gameObject.AddComponent<VerticalLayoutGroup>();
        vl.spacing = 6f;
        vl.padding = new RectOffset(4, 12, 4, 10);
        vl.childControlWidth = vl.childControlHeight = true;
        vl.childForceExpandWidth = true;
        vl.childForceExpandHeight = false;
        lista.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scrollLista.viewport = vista;
        scrollLista.content = lista;
        vista.gameObject.AddComponent<SeguirSeleccionLista>().scroll = scrollLista;

        if (b == null || !b.Visibles.Any())
        {
            sinBestiario = EstiloMenu.Texto("El Bestiario está vacío.", lista, 24, EstiloMenu.TextoApagado);
            sinBestiario.gameObject.AddComponent<LayoutElement>().preferredHeight = 60f;
        }
        else
        {
            // Por zona; cada enemigo base seguido de sus variantes y elites.
            List<FichaBestiario.Entrada> todas = b.Visibles.ToList();
            foreach (int zona in todas.Select(e => e.Zona).Distinct().OrderBy(z => z))
            {
                TextMeshProUGUI cab = EstiloMenu.Texto("", lista, 21, new Color(0.78f, 0.66f, 0.46f));
                cab.characterSpacing = 6f;
                cab.alignment = TextAlignmentOptions.BottomLeft;
                LayoutElement le = cab.gameObject.AddComponent<LayoutElement>();
                le.preferredHeight = le.minHeight = 40f;
                cabecerasZona[zona] = cab;
                List<FichaBestiario.Entrada> enZona = todas.Where(e => e.Zona == zona).ToList();
                foreach (FichaBestiario.Entrada e in enZona.Where(x => x.relacionado == null || !enZona.Any(y => y.enemigo == x.relacionado)))
                {
                    NuevaFila(lista, e, false);
                    foreach (FichaBestiario.Entrada v in enZona.Where(x => x.relacionado == e.enemigo)) NuevaFila(lista, v, true);
                }
            }
            // Variantes de otra zona que la de su base: al final de su zona ya salen.
        }
        VerticalLayoutGroup pie = EstiloMenu.Columna(izq, 0f, 0f, 0f, 0f, 0f);
        pie.childAlignment = TextAnchor.LowerCenter;
        botonVolver = EstiloMenu.Opcion(pie.transform, "Volver", Volver, 56f, 26f, null, TextAlignmentOptions.Left);
        botonVolver.GetComponent<OpcionEstilo>().animar = true;

        Image divisor = EstiloMenu.Caja("Divisor", panel, EstiloMenu.FiloTenue);
        divisor.rectTransform.anchorMin = new Vector2(0.345f, 0f);
        divisor.rectTransform.anchorMax = new Vector2(0.345f, 1f);
        divisor.rectTransform.sizeDelta = new Vector2(2f, 0f);
        divisor.rectTransform.offsetMin = new Vector2(-1f, 50f);
        divisor.rectTransform.offsetMax = new Vector2(1f, -140f);

        // Derecha: la ficha.
        RectTransform der = Zona(panel, 0.355f, 1f, 16f, 56f);
        placeholder = new GameObject("SinElegir", typeof(RectTransform));
        placeholder.transform.SetParent(der, false);
        EstiloMenu.Estirar((RectTransform)placeholder.transform);
        TextMeshProUGUI ph = EstiloMenu.Texto("Elige un enemigo", placeholder.transform, 40, new Color(0.62f, 0.56f, 0.5f));
        ph.characterSpacing = 8f;
        ph.rectTransform.anchoredPosition = new Vector2(0f, 40f);
        ph.rectTransform.sizeDelta = new Vector2(900f, 60f);
        TextMeshProUGUI ph2 = EstiloMenu.Texto(conClicDerecho ? "Clic o Enter para ver su ficha.   Clic derecho o Esc para volver." : "Clic o Enter para ver su ficha.   Esc para volver.",
                                               placeholder.transform, 22, EstiloMenu.TextoApagado);
        ph2.rectTransform.anchoredPosition = new Vector2(0f, -14f);
        ph2.rectTransform.sizeDelta = new Vector2(1000f, 40f);

        ficha = new GameObject("Ficha", typeof(RectTransform)).GetComponent<RectTransform>();
        ficha.SetParent(der, false);
        EstiloMenu.Estirar(ficha);
        grupoFicha = ficha.gameObject.AddComponent<CanvasGroup>();

        Image marco = EstiloMenu.Caja("Marco", ficha, EstiloMenu.FiloTenue);
        RectTransform rm = marco.rectTransform;
        rm.anchorMin = rm.anchorMax = new Vector2(0f, 1f);
        rm.pivot = new Vector2(0f, 1f);
        rm.sizeDelta = new Vector2(220f, 220f);
        Image fondoImg = EstiloMenu.Caja("Fondo", marco.transform, new Color(0.03f, 0.03f, 0.04f, 1f));
        EstiloMenu.Estirar(fondoImg.rectTransform, 2f);
        fondoImg.gameObject.AddComponent<RectMask2D>();
        retrato = EstiloMenu.Caja("Retrato", fondoImg.transform, Color.white);
        retrato.preserveAspect = true;
        EstiloMenu.Estirar(retrato.rectTransform, 16f);
        retrato.rectTransform.pivot = new Vector2(0.5f, 0f);

        nombre = EstiloMenu.Texto("", ficha, 40, EstiloMenu.Titulo);
        nombre.fontStyle = FontStyles.Bold;
        nombre.characterSpacing = 5f;
        nombre.alignment = TextAlignmentOptions.Left;
        TextoArriba(nombre.rectTransform, 250f, 10f, 56f);
        subtitulo = EstiloMenu.Texto("", ficha, 23, EstiloMenu.TextoElegido);
        subtitulo.alignment = TextAlignmentOptions.TopLeft;
        subtitulo.enableWordWrapping = true;
        TextoArriba(subtitulo.rectTransform, 252f, 76f, 70f);

        RectTransform vf = new GameObject("VistaFicha", typeof(RectTransform)).GetComponent<RectTransform>();
        vf.SetParent(ficha, false);
        vf.anchorMin = Vector2.zero; vf.anchorMax = Vector2.one;
        vf.offsetMin = new Vector2(0f, 20f); vf.offsetMax = new Vector2(0f, -244f);
        vf.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.25f);
        vf.gameObject.AddComponent<RectMask2D>();
        scrollFicha = vf.gameObject.AddComponent<ScrollRect>();
        scrollFicha.horizontal = false;
        scrollFicha.movementType = ScrollRect.MovementType.Clamped;
        scrollFicha.scrollSensitivity = 40f;
        contenido = new GameObject("Contenido", typeof(RectTransform)).GetComponent<RectTransform>();
        contenido.SetParent(vf, false);
        contenido.anchorMin = new Vector2(0f, 1f); contenido.anchorMax = new Vector2(1f, 1f);
        contenido.pivot = new Vector2(0.5f, 1f);
        contenido.sizeDelta = Vector2.zero;
        VerticalLayoutGroup vc = contenido.gameObject.AddComponent<VerticalLayoutGroup>();
        vc.padding = new RectOffset(22, 22, 16, 16);
        vc.spacing = 12f;
        vc.childControlWidth = vc.childControlHeight = true;
        vc.childForceExpandWidth = true;
        vc.childForceExpandHeight = false;
        contenido.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scrollFicha.viewport = vf;
        scrollFicha.content = contenido;

        ficha.gameObject.SetActive(false);
    }

    private void NuevaFila(Transform lista, FichaBestiario.Entrada e, bool sangrada)
    {
        FichaBestiario.Entrada en = e;
        Button b = EstiloMenu.Opcion(lista, "", () => Seleccionar(en), 72f, 24f, null, TextAlignmentOptions.Left);
        Fila f = new Fila { entrada = e, sangrada = sangrada, boton = b, estilo = b.GetComponent<OpcionEstilo>(), texto = b.GetComponentInChildren<TextMeshProUGUI>() };
        f.texto.richText = true;
        f.texto.rectTransform.offsetMin = new Vector2(sangrada ? 110f : 92f, 0f);
        f.retrato = EstiloMenu.Caja("Retrato", b.transform, Color.white);
        f.retrato.preserveAspect = true;
        RectTransform rr = f.retrato.rectTransform;
        rr.anchorMin = rr.anchorMax = new Vector2(0f, 0.5f);
        rr.pivot = new Vector2(0f, 0.5f);
        rr.sizeDelta = new Vector2(58f, 58f);
        rr.anchoredPosition = new Vector2(sangrada ? 38f : 20f, 0f);
        f.nuevo = PuntoNuevo.Crear(b.transform, new Vector2(0f, 1f), new Vector2(sangrada ? 40f : 22f, -10f), 12f);
        grupo.Anadir(f.estilo);
        filas.Add(f);
    }

    private static void TextoArriba(RectTransform r, float x, float y, float alto)
    {
        r.anchorMin = new Vector2(0f, 1f); r.anchorMax = new Vector2(1f, 1f);
        r.pivot = new Vector2(0f, 1f);
        r.offsetMin = new Vector2(x, -y - alto);
        r.offsetMax = new Vector2(0f, -y);
    }

    private static RectTransform Zona(RectTransform panel, float desde, float hasta, float izq, float der)
    {
        RectTransform r = new GameObject("Zona", typeof(RectTransform)).GetComponent<RectTransform>();
        r.SetParent(panel, false);
        r.anchorMin = new Vector2(desde, 0f);
        r.anchorMax = new Vector2(hasta, 1f);
        r.offsetMin = new Vector2(izq, 40f);
        r.offsetMax = new Vector2(-der, -144f);
        return r;
    }

    // Con teclado o mando, la lista baja sola hasta la opcion elegida.
    private class SeguirSeleccionLista : MonoBehaviour
    {
        public ScrollRect scroll;

        private void Update()
        {
            GameObject sel = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (sel == null || !sel.transform.IsChildOf(scroll.content)) return;
            RectTransform r = (RectTransform)sel.transform;
            Vector3[] esquinas = new Vector3[4];
            r.GetWorldCorners(esquinas);
            Vector3[] ev = new Vector3[4];
            scroll.viewport.GetWorldCorners(ev);
            float arriba = esquinas[1].y, abajo = esquinas[0].y;
            if (arriba > ev[1].y) scroll.content.position -= new Vector3(0f, arriba - ev[1].y, 0f);
            else if (abajo < ev[0].y) scroll.content.position += new Vector3(0f, ev[0].y - abajo, 0f);
        }
    }
}
