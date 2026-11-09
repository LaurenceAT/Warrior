using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Pagina "Amuletos" del menu de la hoguera:
//   - Arriba a la izquierda, "Equipados": 4 huecos (2 abiertos al empezar; el
//     Vestigio del Vacio abre los cerrados).
//   - Debajo, la coleccion ordenada (FichaAmuletos): los que tienes con su
//     imagen; los que no, con "?".
//   - A la derecha, el nombre, la imagen grande y lo que se puede hacer.
// Pulsar un amuleto tuyo lo equipa (en el primer hueco libre) o lo quita si ya
// estaba puesto; pulsar un hueco con un amuleto lo quita. Los amuletos aun no
// hacen nada. Se navega como los demas menus; Esc / clic derecho vuelve.
public class AmuletosHoguera : MonoBehaviour
{
    public const int Columnas = 10, Filas = 4;
    public static int TotalAmuletos => Columnas * Filas;
    public static int HuecosEquipo => Amuletos.HuecosMaximos;

    public enum Tipo { Libre, Bloqueado, Desconocido, Conseguido, Equipado }

    public RectTransform Pagina { get; private set; }
    public GameObject Primero { get; private set; }
    public HuecoAmuleto Elegido { get; private set; }

    private TextMeshProUGUI nombre, descripcion, cuenta, aviso;
    private Image icono, fondoIcono;
    private TextMeshProUGUI interrogacion;
    private HuecoAmuleto[] huecos = new HuecoAmuleto[0];

    private static readonly Color Disco = new Color(0.1f, 0.085f, 0.08f, 1f);
    private static readonly Color DiscoBloqueado = new Color(0.06f, 0.055f, 0.055f, 1f);
    private static readonly Color AnilloNormal = new Color(0.72f, 0.58f, 0.36f, 0.45f);

    public static AmuletosHoguera Construir(Transform raiz, UnityEngine.Events.UnityAction volver)
    {
        RectTransform panel = EstiloMenu.Panel(raiz, "AMULETOS", new Vector2(1500f, 840f));
        AmuletosHoguera a = panel.gameObject.AddComponent<AmuletosHoguera>();
        a.Pagina = (RectTransform)panel.parent;
        var lista = new System.Collections.Generic.List<HuecoAmuleto>();

        // ---- Izquierda: equipados y coleccion.
        RectTransform izq = Zona(panel, "Izquierda", 0f, 0.64f, new Vector2(50f, 110f), new Vector2(-10f, -110f));

        TextMeshProUGUI eq = EstiloMenu.Texto("Equipados", izq, 30, EstiloMenu.Titulo);
        eq.alignment = TextAlignmentOptions.Left;
        EstiloMenu.Arriba(eq.rectTransform, -6f, 40f);

        for (int i = 0; i < HuecosEquipo; i++)
        {
            HuecoAmuleto h = a.Hueco(izq, true, i, 92f, new Vector2(20f + i * 122f, -60f));
            lista.Add(h);
            if (i == 0) a.Primero = h.gameObject;
        }

        // Linea entre lo equipado y la coleccion.
        Image linea = EstiloMenu.Caja("Linea", izq, EstiloMenu.FiloTenue);
        linea.rectTransform.anchorMin = new Vector2(0f, 1f);
        linea.rectTransform.anchorMax = new Vector2(1f, 1f);
        linea.rectTransform.sizeDelta = new Vector2(0f, 2f);
        linea.rectTransform.anchoredPosition = new Vector2(0f, -178f);

        a.cuenta = EstiloMenu.Texto("", izq, 22, EstiloMenu.TextoApagado);
        a.cuenta.alignment = TextAlignmentOptions.Right;
        EstiloMenu.Arriba(a.cuenta.rectTransform, -6f, 40f);

        // La coleccion, ordenada en filas (el orden de la ficha de amuletos).
        const float paso = 88f, tam = 74f;
        for (int f = 0; f < Filas; f++)
            for (int c = 0; c < Columnas; c++)
                lista.Add(a.Hueco(izq, false, f * Columnas + c, tam, new Vector2(20f + c * paso, -204f - f * (paso + 4f))));
        a.huecos = lista.ToArray();

        a.aviso = EstiloMenu.Texto("", izq, 22, new Color(1f, 0.8f, 0.55f));
        a.aviso.rectTransform.anchorMin = new Vector2(0f, 0f);
        a.aviso.rectTransform.anchorMax = new Vector2(1f, 0f);
        a.aviso.rectTransform.sizeDelta = new Vector2(0f, 30f);
        a.aviso.rectTransform.anchoredPosition = new Vector2(0f, 6f);

        // Volver, abajo a la izquierda.
        Button v = EstiloMenu.Opcion(panel, "Volver", volver, 56f, 26f);
        RectTransform rv = (RectTransform)v.transform;
        rv.anchorMin = rv.anchorMax = new Vector2(0f, 0f);
        rv.pivot = new Vector2(0f, 0f);
        rv.sizeDelta = new Vector2(240f, 56f);
        rv.anchoredPosition = new Vector2(50f, 36f);

        // ---- Derecha: lo elegido.
        Image ficha = EstiloMenu.Caja("Ficha", panel, new Color(0f, 0f, 0f, 0.35f));
        RectTransform rf = ficha.rectTransform;
        rf.anchorMin = new Vector2(0.64f, 0f);
        rf.anchorMax = new Vector2(1f, 1f);
        rf.offsetMin = new Vector2(20f, 110f);
        rf.offsetMax = new Vector2(-40f, -112f);
        Image filo = EstiloMenu.Caja("Filo", ficha.transform, EstiloMenu.FiloTenue);
        filo.rectTransform.anchorMin = new Vector2(0f, 0f);
        filo.rectTransform.anchorMax = new Vector2(0f, 1f);
        filo.rectTransform.sizeDelta = new Vector2(2f, 0f);

        a.nombre = EstiloMenu.Texto("", ficha.transform, 34, EstiloMenu.Titulo);
        a.nombre.fontStyle = FontStyles.Bold;
        a.nombre.enableWordWrapping = true;
        EstiloMenu.Arriba(a.nombre.rectTransform, -24f, 60f, 20f);

        a.fondoIcono = EstiloMenu.Caja("Imagen", ficha.transform, Disco);
        a.fondoIcono.sprite = DiscoSprite();
        RectTransform ri = a.fondoIcono.rectTransform;
        ri.anchorMin = ri.anchorMax = new Vector2(0.5f, 1f);
        ri.sizeDelta = new Vector2(170f, 170f);
        ri.anchoredPosition = new Vector2(0f, -190f);
        Image anilloGrande = EstiloMenu.Caja("Anillo", a.fondoIcono.transform, AnilloNormal);
        anilloGrande.sprite = AnilloSprite();
        EstiloMenu.Estirar(anilloGrande.rectTransform);
        a.icono = EstiloMenu.Caja("Icono", a.fondoIcono.transform, Color.white);
        a.icono.preserveAspect = true;
        EstiloMenu.Estirar(a.icono.rectTransform, 34f);
        a.interrogacion = EstiloMenu.Texto("?", a.fondoIcono.transform, 84, EstiloMenu.TextoApagado);
        a.interrogacion.fontStyle = FontStyles.Bold;
        EstiloMenu.Estirar(a.interrogacion.rectTransform);

        a.descripcion = EstiloMenu.Texto("", ficha.transform, 24, EstiloMenu.TextoNormal);
        a.descripcion.alignment = TextAlignmentOptions.Top;
        a.descripcion.enableWordWrapping = true;
        a.descripcion.lineSpacing = 6f;
        RectTransform rd = a.descripcion.rectTransform;
        rd.anchorMin = new Vector2(0f, 0f);
        rd.anchorMax = new Vector2(1f, 1f);
        rd.offsetMin = new Vector2(34f, 30f);
        rd.offsetMax = new Vector2(-34f, -300f);

        Amuletos.AlCambiar += a.Refrescar;
        a.Refrescar();
        return a;
    }

    private void OnDestroy() => Amuletos.AlCambiar -= Refrescar;

    // Lo pinta todo de nuevo (al entrar y cada vez que cambia lo que tienes).
    public void Refrescar()
    {
        if (cuenta == null) return;
        cuenta.text = $"Conseguidos  {Amuletos.Conseguidos} / {Mathf.Max(Amuletos.Total, 0)}";
        foreach (HuecoAmuleto h in huecos) h.Pintar();
        if (Elegido != null) Elegir(Elegido, false);
    }

    public void Avisar(string texto) { if (aviso != null) aviso.text = texto; }

    // Lo elegido pasa a la ficha de la derecha.
    public void Elegir(HuecoAmuleto h, bool limpiarAviso = true)
    {
        Elegido = h;
        if (limpiarAviso) Avisar("");
        FichaAmuletos.Amuleto amu = h.Amuleto;
        Tipo t = h.tipo;
        nombre.text = h.Titulo;
        descripcion.text = h.Descripcion;
        bool conImagen = amu != null && (t == Tipo.Conseguido || t == Tipo.Equipado);
        icono.sprite = conImagen ? amu.icono : t == Tipo.Bloqueado ? RecursosRPG.Get().Icono("candado") : null;
        icono.enabled = icono.sprite != null;
        icono.rectTransform.offsetMin = Vector2.one * (conImagen ? 34f : 50f);
        icono.rectTransform.offsetMax = -Vector2.one * (conImagen ? 34f : 50f);
        interrogacion.enabled = t == Tipo.Desconocido;
        fondoIcono.color = t == Tipo.Bloqueado ? DiscoBloqueado : Disco;
    }

    private HuecoAmuleto Hueco(RectTransform padre, bool equipo, int indice, float tam, Vector2 pos)
    {
        Image disco = EstiloMenu.Caja((equipo ? "Equipado_" : "Amuleto_") + indice, padre, Disco);
        disco.sprite = DiscoSprite();
        disco.raycastTarget = true;
        RectTransform r = disco.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
        r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = new Vector2(tam, tam);
        r.anchoredPosition = pos + new Vector2(tam * 0.5f, -tam * 0.5f);

        Image anillo = EstiloMenu.Caja("Anillo", disco.transform, AnilloNormal);
        anillo.sprite = AnilloSprite();
        EstiloMenu.Estirar(anillo.rectTransform);

        Image img = EstiloMenu.Caja("Icono", disco.transform, Color.white);
        img.preserveAspect = true;
        EstiloMenu.Estirar(img.rectTransform, tam * 0.16f);

        Image candado = EstiloMenu.Caja("Candado", disco.transform, new Color(0.75f, 0.72f, 0.68f, 0.85f));
        candado.sprite = RecursosRPG.Get().Icono("candado");
        candado.preserveAspect = true;
        EstiloMenu.Estirar(candado.rectTransform, tam * 0.26f);

        TextMeshProUGUI q = EstiloMenu.Texto("?", disco.transform, tam * 0.5f, EstiloMenu.TextoApagado);
        q.fontStyle = FontStyles.Bold;
        EstiloMenu.Estirar(q.rectTransform);

        Button b = disco.gameObject.AddComponent<Button>();
        b.transition = Selectable.Transition.None;
        HuecoAmuleto h = disco.gameObject.AddComponent<HuecoAmuleto>();
        h.menu = this;
        h.equipo = equipo;
        h.indice = indice;
        h.disco = disco;
        h.anillo = anillo;
        h.imagen = img;
        h.candado = candado;
        h.interrogacion = q;
        b.onClick.AddListener(h.Pulsar);
        h.Pintar();
        return h;
    }

    private static RectTransform Zona(RectTransform padre, string n, float x0, float x1, Vector2 offMin, Vector2 offMax)
    {
        RectTransform r = new GameObject(n, typeof(RectTransform)).GetComponent<RectTransform>();
        r.SetParent(padre, false);
        r.anchorMin = new Vector2(x0, 0f);
        r.anchorMax = new Vector2(x1, 1f);
        r.offsetMin = offMin;
        r.offsetMax = offMax;
        return r;
    }

    // ------------------------------------------------------------------ Sprites redondos

    private static Sprite disco, anillo, iconoMenu;

    public static Sprite DiscoSprite() => disco != null ? disco : disco = Redondo(0f);
    public static Sprite AnilloSprite() => anillo != null ? anillo : anillo = Redondo(0.84f);

    // Disco (hueco = 0) o anillo (hueco = radio interior relativo), con borde suave.
    private static Sprite Redondo(float hueco)
    {
        const int n = 128;
        Texture2D t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float c = n * 0.5f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(c, c)) / c;
                float a = Mathf.Clamp01((1f - d) * c * 0.5f);
                if (hueco > 0f) a *= Mathf.Clamp01((d - hueco) * c * 0.5f);
                t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        t.Apply();
        return Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
    }

    // Icono de la opcion del menu de la hoguera: un colgante en pixel art.
    public static Sprite IconoMenu()
    {
        if (iconoMenu != null) return iconoMenu;
        string[] filas =
        {
            "................",
            "......oooo......",
            ".....o....o.....",
            "....o......o....",
            "....o......o....",
            ".....o....o.....",
            "......oooo......",
            ".......oo.......",
            "......kkkk......",
            ".....kggggk.....",
            "....kgglggk.....",
            "....kgllggk.....",
            "....kggggggk....",
            ".....kggggk.....",
            "......kkkk......",
            "................",
        };
        Color cad = new Color(0.78f, 0.66f, 0.42f), borde = new Color(0.35f, 0.12f, 0.12f),
              gema = new Color(0.78f, 0.2f, 0.25f), luz = new Color(1f, 0.7f, 0.7f);
        Texture2D t = new Texture2D(16, 16, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                char k = filas[15 - y][x];
                t.SetPixel(x, y, k == 'o' ? cad : k == 'k' ? borde : k == 'g' ? gema : k == 'l' ? luz : Color.clear);
            }
        t.Apply();
        iconoMenu = Sprite.Create(t, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16f);
        return iconoMenu;
    }
}

// Un hueco: de los equipados (equipo = true, indice = su hueco) o de la
// coleccion (indice = su amuleto en la ficha). Pasar el raton por encima lo
// elige; elegido, su anillo se ilumina y llena la ficha.
public class HuecoAmuleto : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler
{
    public AmuletosHoguera menu;
    public bool equipo;
    public int indice;
    public Image disco, anillo, imagen, candado;
    public TextMeshProUGUI interrogacion;
    public bool EstaElegido { get; private set; }

    private static readonly Color AnilloNormal = new Color(0.72f, 0.58f, 0.36f, 0.45f);
    private static readonly Color AnilloPuesto = new Color(0.62f, 0.95f, 0.72f, 0.95f);
    private static readonly Color AnilloElegido = new Color(1f, 0.85f, 0.5f, 1f);
    private static readonly Color Disco = new Color(0.1f, 0.085f, 0.08f, 1f);
    private static readonly Color DiscoBloqueado = new Color(0.06f, 0.055f, 0.055f, 1f);

    // El amuleto de este hueco (el equipado o el de la coleccion); null si no hay.
    public FichaAmuletos.Amuleto Amuleto
    {
        get
        {
            FichaAmuletos f = FichaAmuletos.Get();
            if (f == null) return null;
            if (equipo) return f.Buscar(Amuletos.Equipado(indice));
            return indice < f.amuletos.Count ? f.amuletos[indice] : null;
        }
    }

    public AmuletosHoguera.Tipo tipo
    {
        get
        {
            if (equipo)
            {
                if (!Amuletos.Abierto(indice)) return AmuletosHoguera.Tipo.Bloqueado;
                return string.IsNullOrEmpty(Amuletos.Equipado(indice)) ? AmuletosHoguera.Tipo.Libre : AmuletosHoguera.Tipo.Equipado;
            }
            FichaAmuletos.Amuleto a = Amuleto;
            if (a == null || !Amuletos.Tiene(a.id)) return AmuletosHoguera.Tipo.Desconocido;
            return Amuletos.HuecoDe(a.id) >= 0 ? AmuletosHoguera.Tipo.Equipado : AmuletosHoguera.Tipo.Conseguido;
        }
    }

    public string Titulo
    {
        get
        {
            switch (tipo)
            {
                case AmuletosHoguera.Tipo.Libre: return "Hueco libre";
                case AmuletosHoguera.Tipo.Bloqueado: return "Hueco bloqueado";
                case AmuletosHoguera.Tipo.Desconocido: return "???";
                default: return Amuleto != null ? Amuleto.nombre : "???";
            }
        }
    }

    public string Descripcion
    {
        get
        {
            switch (tipo)
            {
                case AmuletosHoguera.Tipo.Libre: return "Aquí se lleva un amuleto. Elige uno de los tuyos para ponerlo.";
                case AmuletosHoguera.Tipo.Bloqueado: return "Este hueco sigue cerrado.\n<size=85%><color=#8a8580>Un Vestigio del Vacío lo abre.</color></size>";
                case AmuletosHoguera.Tipo.Desconocido: return "Un amuleto que aún no tienes.";
                case AmuletosHoguera.Tipo.Equipado: return Efecto + "<color=#9fe0a0>Puesto.</color>\n<size=85%><color=#8a8580>Pulsa para quitarlo.</color></size>";
                default: return Efecto + (Amuletos.HayHuecoLibre ? "<size=85%><color=#8a8580>Pulsa para ponértelo.</color></size>"
                                                                 : "<size=85%><color=#8a8580>No queda ningún hueco libre: quita otro antes.</color></size>");
            }
        }
    }

    // Lo que hace el amuleto (de su ficha), con un hueco debajo.
    private string Efecto => Amuleto != null && !string.IsNullOrEmpty(Amuleto.descripcion) ? Amuleto.descripcion + "\n\n" : "";

    public void Pintar()
    {
        AmuletosHoguera.Tipo t = tipo;
        FichaAmuletos.Amuleto a = Amuleto;
        bool conImagen = a != null && (t == AmuletosHoguera.Tipo.Conseguido || t == AmuletosHoguera.Tipo.Equipado);
        imagen.sprite = conImagen ? a.icono : null;
        imagen.enabled = imagen.sprite != null;
        // En la coleccion, lo que ya llevas puesto se ve algo apagado.
        imagen.color = !equipo && t == AmuletosHoguera.Tipo.Equipado ? new Color(0.55f, 0.55f, 0.55f, 1f) : Color.white;
        candado.enabled = t == AmuletosHoguera.Tipo.Bloqueado && candado.sprite != null;
        interrogacion.enabled = t == AmuletosHoguera.Tipo.Desconocido;
        disco.color = t == AmuletosHoguera.Tipo.Bloqueado ? DiscoBloqueado : Disco;
        anillo.color = EstaElegido ? AnilloElegido : t == AmuletosHoguera.Tipo.Equipado ? AnilloPuesto : AnilloNormal;
    }

    public void OnSelect(BaseEventData e)
    {
        EstaElegido = true;
        transform.localScale = Vector3.one * 1.12f;
        Pintar();
        menu.Elegir(this);
        SonidoMenu.Mover();
    }

    public void OnDeselect(BaseEventData e)
    {
        EstaElegido = false;
        transform.localScale = Vector3.one;
        Pintar();
    }

    public void OnPointerEnter(PointerEventData e)
    {
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != gameObject)
            EventSystem.current.SetSelectedGameObject(gameObject);
    }

    // Equipar o quitar.
    public void Pulsar()
    {
        AmuletosHoguera.Tipo t = tipo;
        FichaAmuletos.Amuleto a = Amuleto;
        if (equipo)
        {
            if (t == AmuletosHoguera.Tipo.Equipado) { Amuletos.Quitar(indice); SonidoMenu.Atras(); return; }
            SonidoMenu.Error();
            menu.Avisar(t == AmuletosHoguera.Tipo.Bloqueado ? "Este hueco está cerrado." : "Elige uno de tus amuletos de abajo.");
            return;
        }
        if (t == AmuletosHoguera.Tipo.Equipado) { Amuletos.Quitar(Amuletos.HuecoDe(a.id)); SonidoMenu.Atras(); return; }
        if (t == AmuletosHoguera.Tipo.Conseguido)
        {
            if (Amuletos.Equipar(a.id)) { Sonido.Reproducir("mejorar_equipo", 0.6f); menu.Avisar(a.nombre + ": puesto."); }
            else { SonidoMenu.Error(); menu.Avisar("No queda ningún hueco libre."); }
            return;
        }
        SonidoMenu.Error();
    }
}
