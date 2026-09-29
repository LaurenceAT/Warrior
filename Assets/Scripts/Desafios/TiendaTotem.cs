using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// La tienda del totem (desafios). Arriba la frase del totem (letra a letra);
// a la izquierda una cuadricula con lo que vende; a la derecha lo que hace el
// elegido y el boton de comprar. Todo se paga con almas y se aplica al momento.
//   - Sin almas suficientes: precio en rojo, boton apagado y sonido de error.
//   - Lo comprado queda "Agotado" (dura todo el desafio, aunque mueras).
//   - Lista de deseos: M (o el boton de la estrella) marca un objeto; se ve
//     destacado, el cursor empieza en el y arriba se dice cuanto falta.
// Se maneja con teclado, raton o mando. F o Esc cierran (Esc no abre la pausa).
public class TiendaTotem : MonoBehaviour
{
    private static TiendaTotem instancia;
    public static bool Abierta => instancia != null && (instancia.abierta || instancia.cerradaEnFrame == Time.frameCount);
    public static float UltimoCierre { get; private set; } = -10f;

    private class Celda
    {
        public FichaTienda.Articulo art;
        public Button boton;
        public OpcionEstilo estilo;
        public Image icono, estrella, candado, fondo;
        public TextMeshProUGUI nombre, precio, agotado;
    }

    private readonly List<Celda> celdas = new List<Celda>();
    private CanvasGroup grupo;
    private TextMeshProUGUI frase, almas, deseo, detNombre, detTexto, detPrecio, textoComprar;
    private Image detIcono;
    private Button botonComprar, botonMarcar;
    private Celda actual;
    private bool abierta;
    private int cerradaEnFrame = -1, abiertaEnFrame;
    private float letras;
    private int ultimaFrase = -1;

    public static void Abrir(PlayerControler p)
    {
        FichaTienda f = FichaTienda.Get();
        if (f == null) return;
        if (instancia == null) instancia = Crear(f);
        instancia.AbrirInterno(f);
    }

    public static void Cerrar()
    {
        if (instancia == null || !instancia.abierta) return;
        instancia.abierta = false;
        instancia.cerradaEnFrame = Time.frameCount;
        UltimoCierre = Time.unscaledTime;
        instancia.grupo.alpha = 0f;
        instancia.grupo.interactable = instancia.grupo.blocksRaycasts = false;
        instancia.gameObject.SetActive(false);
        SonidoMenu.Cancelar();
    }

    private void AbrirInterno(FichaTienda f)
    {
        abierta = true;
        abiertaEnFrame = Time.frameCount;
        gameObject.SetActive(true);
        grupo.alpha = 1f;
        grupo.interactable = grupo.blocksRaycasts = true;
        SonidoMenu.Abrir();

        // Una frase al azar, sin repetir la de la vez anterior.
        if (f.frases != null && f.frases.Count > 0)
        {
            int i = Random.Range(0, f.frases.Count);
            if (f.frases.Count > 1 && i == ultimaFrase) i = (i + 1 + Random.Range(0, f.frases.Count - 1)) % f.frases.Count;
            ultimaFrase = i;
            frase.text = "«" + f.frases[i] + "»";
        }
        else frase.text = "";
        frase.ForceMeshUpdate();
        frase.maxVisibleCharacters = 0;
        letras = 0f;

        Refrescar();
        // El cursor empieza en el objeto deseado (si hay).
        Celda inicio = celdas.FirstOrDefault(c => c.art.id == Desafio.Deseado) ?? celdas.FirstOrDefault();
        Seleccionar(inicio);
    }

    private void Update()
    {
        if (!abierta) return;
        FichaTienda f = FichaTienda.Get();
        // La frase va letra a letra; la tienda se puede usar mientras.
        if (frase.maxVisibleCharacters < frase.textInfo.characterCount)
        {
            letras += (f != null ? f.velocidadFrase : 40f) * Time.unscaledDeltaTime;
            frase.maxVisibleCharacters = Mathf.Min(frase.textInfo.characterCount, Mathf.FloorToInt(letras));
        }

        // Lo elegido con el teclado o el raton se describe a la derecha.
        GameObject sel = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        Celda c = celdas.FirstOrDefault(x => x.boton.gameObject == sel);
        if (c != null && c != actual) { actual = c; Detalle(); }
        if (sel == null && actual != null && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(actual.boton.gameObject);

        if (Time.frameCount == abiertaEnFrame) return;
        Keyboard k = Keyboard.current;
        Gamepad g = Gamepad.current;
        if ((k != null && (k.escapeKey.wasPressedThisFrame || k.fKey.wasPressedThisFrame)) || (g != null && g.buttonEast.wasPressedThisFrame)) { Cerrar(); return; }
        if ((k != null && k.mKey.wasPressedThisFrame) || (g != null && g.buttonNorth.wasPressedThisFrame)) Marcar();
    }

    // ------------------------------------------------------------------ Acciones

    private bool Vendido(FichaTienda.Articulo a) => Desafio.Comprados.Contains(a.id);
    private bool Bloqueado(FichaTienda.Articulo a) => !string.IsNullOrEmpty(a.requiere) && !Desafio.Comprados.Contains(a.requiere);

    // Ya al maximo (no tiene sentido comprarlo).
    private static bool AlMaximo(FichaTienda.Articulo a)
    {
        switch (a.objeto)
        {
            case Equipo.Objeto.PiedraForja: return Equipo.NivelEspada >= Equipo.NivelMaximoEspada;
            case Equipo.Objeto.LagrimaCarmesi: return Equipo.NivelCuracion >= Equipo.NivelMaximoCuracion;
            case Equipo.Objeto.LagrimaCeleste: return Equipo.NivelMana >= Equipo.NivelMaximoMana;
            case Equipo.Objeto.FrascoSangre: return Equipo.FrascosSangreExtra >= AjustesProgreso.Get().frascosExtraMaximo;
            case Equipo.Objeto.FrascoMana: return Equipo.FrascosManaExtra >= AjustesProgreso.Get().frascosExtraMaximo;
            default: return false;
        }
    }

    private void Comprar()
    {
        if (actual == null) return;
        FichaTienda.Articulo a = actual.art;
        if (Vendido(a) || Bloqueado(a) || AlMaximo(a) || Progreso.Almas < a.precio) { SonidoMenu.Error(); return; }
        if (!Progreso.Gastar(a.precio)) { SonidoMenu.Error(); return; }
        Equipo.SubirDirecto(a.objeto);
        Desafio.Comprados.Add(a.id);
        if (Desafio.Deseado == a.id) Desafio.Deseado = null;
        Sonido.Reproducir("mejorar_equipo");
        Refrescar();
    }

    private void Marcar()
    {
        if (actual == null || Vendido(actual.art)) { SonidoMenu.Error(); return; }
        Desafio.Deseado = Desafio.Deseado == actual.art.id ? null : actual.art.id;
        SonidoMenu.Confirmar();
        Refrescar();
    }

    private void Seleccionar(Celda c)
    {
        actual = c;
        Detalle();
        if (c != null && EventSystem.current != null)
        {
            SonidoMenu.Silenciar();
            EventSystem.current.SetSelectedGameObject(c.boton.gameObject);
        }
    }

    // ------------------------------------------------------------------ Textos

    private const string Rojo = "#e0776c", Verde = "#9fe0a0", Gris = "#8a8580";

    private void Refrescar()
    {
        int alm = Progreso.Almas;
        almas.text = $"Almas  <b>{alm:N0}</b>";
        FichaTienda.Articulo d = celdas.Select(c => c.art).FirstOrDefault(a => a.id == Desafio.Deseado);
        if (d == null) deseo.text = $"<color={Gris}>Marca un objeto con M para tenerlo a la vista</color>";
        else if (Bloqueado(d)) deseo.text = $"Deseado: <b>{d.nombre}</b> ({d.precio:N0})  ·  <color={Gris}>antes: {celdas.Select(c => c.art).FirstOrDefault(a => a.id == d.requiere)?.nombre}</color>";
        else if (alm >= d.precio) deseo.text = $"Deseado: <b>{d.nombre}</b>  ·  <color={Verde}>Puedes comprarlo</color>";
        else deseo.text = $"Deseado: <b>{d.nombre}</b> ({d.precio:N0})  ·  <color={Rojo}>te faltan {d.precio - alm:N0}</color>";

        foreach (Celda c in celdas)
        {
            FichaTienda.Articulo a = c.art;
            bool vendido = Vendido(a), bloq = Bloqueado(a), max = AlMaximo(a);
            bool alcanza = alm >= a.precio;
            c.agotado.gameObject.SetActive(vendido || max);
            c.agotado.text = vendido ? "AGOTADO" : "AL MÁXIMO";
            c.candado.enabled = bloq && !vendido;
            c.estrella.enabled = Desafio.Deseado == a.id;
            c.precio.text = vendido ? "" : $"<color={(alcanza ? "#f0d49a" : Rojo)}>{a.precio:N0}</color> almas";
            float atenuar = vendido || bloq || max ? 0.35f : 1f;
            c.icono.color = new Color(atenuar, atenuar, atenuar, 1f);
            c.fondo.color = Desafio.Deseado == a.id ? new Color(0.35f, 0.26f, 0.1f, 0.9f) : new Color(0.08f, 0.07f, 0.08f, 0.9f);
            c.estilo.PonerActiva(!vendido && !bloq && !max && alcanza);
        }
        Detalle();
    }

    private void Detalle()
    {
        if (actual == null) return;
        FichaTienda.Articulo a = actual.art;
        detIcono.sprite = RecursosRPG.Get().Icono(Equipo.ClaveIcono(a.objeto));
        detIcono.enabled = detIcono.sprite != null;
        detNombre.text = a.nombre;
        bool vendido = Vendido(a), bloq = Bloqueado(a), max = AlMaximo(a);
        string efecto = Efecto(a);
        if (bloq)
        {
            FichaTienda.Articulo req = celdas.Select(c => c.art).FirstOrDefault(x => x.id == a.requiere);
            efecto += $"\n\n<color={Rojo}>Compra antes: {(req != null ? req.nombre : a.requiere)}</color>";
        }
        detTexto.text = efecto;
        bool alcanza = Progreso.Almas >= a.precio;
        detPrecio.text = vendido ? "Agotado" : max ? "Ya está al máximo" : $"Precio: <color={(alcanza ? "#f0d49a" : Rojo)}><b>{a.precio:N0}</b></color> almas";
        textoComprar.text = vendido ? "Agotado" : $"Comprar ({a.precio:N0})";
        botonComprar.GetComponent<OpcionEstilo>().PonerActiva(!vendido && !bloq && !max && alcanza);
        botonMarcar.GetComponentInChildren<TextMeshProUGUI>().text = Desafio.Deseado == a.id ? "Quitar de deseos (M)" : "Marcar como deseado (M)";
    }

    // Lo que hace el objeto, con los numeros de ahora y los de despues.
    private static string Efecto(FichaTienda.Articulo a)
    {
        ReservaPociones r = ReservaPociones.Get();
        int vida = Progreso.VidaMax, mana = Mathf.RoundToInt(Progreso.ManaMax);
        switch (a.objeto)
        {
            case Equipo.Objeto.PiedraForja:
                return $"Mejora la espada al momento.\nDaño x{Equipo.MultiplicadorEspadaEn(Equipo.NivelEspada):0.00} → <color={Verde}>x{Equipo.MultiplicadorEspadaEn(Equipo.NivelEspada + 1):0.00}</color>";
            case Equipo.Objeto.LagrimaCarmesi:
                int c0 = Mathf.RoundToInt(vida * Equipo.CuraFrascoEn(Equipo.NivelCuracion)), c1 = Mathf.RoundToInt(vida * Equipo.CuraFrascoEn(Equipo.NivelCuracion + 1));
                return $"Cada frasco de sangre cura {c1 - c0} de vida más.\nCura {c0} → <color={Verde}>{c1}</color>";
            case Equipo.Objeto.LagrimaCeleste:
                int m0 = Mathf.RoundToInt(mana * Equipo.ManaFrascoEn(Equipo.NivelMana)), m1 = Mathf.RoundToInt(mana * Equipo.ManaFrascoEn(Equipo.NivelMana + 1));
                return $"Cada frasco de maná devuelve {m1 - m0} de maná más.\nDevuelve {m0} → <color={Verde}>{m1}</color>";
            case Equipo.Objeto.FrascoSangre:
                return $"Una carga más para el frasco de sangre.\nCargas {r.Maximo} → <color={Verde}>{r.Maximo + 1}</color>";
            case Equipo.Objeto.FrascoMana:
                return $"Una carga más para el frasco de maná.\nCargas {r.MaximoMana} → <color={Verde}>{r.MaximoMana + 1}</color>";
            default: return Equipo.Descripcion(a.objeto);
        }
    }

    // ------------------------------------------------------------------ Construccion

    private static TiendaTotem Crear(FichaTienda f)
    {
        GameObject go = new GameObject("TiendaTotem");
        TiendaTotem t = go.AddComponent<TiendaTotem>();
        t.grupo = EstiloMenu.Lienzo(go, 92);
        RectTransform panel = EstiloMenu.Panel(go.transform, "TÓTEM", new Vector2(1560f, 880f));

        t.frase = EstiloMenu.Texto("", panel, 26, new Color(0.62f, 0.95f, 1f));
        t.frase.fontStyle = FontStyles.Italic;
        EstiloMenu.Arriba(t.frase.rectTransform, -112f, 40f, 60f);

        t.almas = EstiloMenu.Texto("", panel, 28, new Color(0.78f, 0.9f, 1f));
        t.almas.alignment = TextAlignmentOptions.Left;
        EstiloMenu.Arriba(t.almas.rectTransform, -162f, 36f, 70f);
        t.deseo = EstiloMenu.Texto("", panel, 22, EstiloMenu.TextoNormal);
        t.deseo.alignment = TextAlignmentOptions.Right;
        EstiloMenu.Arriba(t.deseo.rectTransform, -166f, 32f, 70f);

        // Izquierda: la cuadricula.
        RectTransform izq = new GameObject("Articulos", typeof(RectTransform)).GetComponent<RectTransform>();
        izq.SetParent(panel, false);
        izq.anchorMin = new Vector2(0f, 0f);
        izq.anchorMax = new Vector2(0.6f, 1f);
        izq.offsetMin = new Vector2(60f, 90f);
        izq.offsetMax = new Vector2(-10f, -215f);
        GridLayoutGroup grid = izq.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(204f, 250f);
        grid.spacing = new Vector2(14f, 14f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 4;
        foreach (FichaTienda.Articulo a in f.articulos) t.celdas.Add(t.NuevaCelda(izq, a));

        // Derecha: el detalle.
        Image det = EstiloMenu.Caja("Detalle", panel, new Color(0f, 0f, 0f, 0.35f));
        RectTransform rd = det.rectTransform;
        rd.anchorMin = new Vector2(0.6f, 0f);
        rd.anchorMax = new Vector2(1f, 1f);
        rd.offsetMin = new Vector2(20f, 90f);
        rd.offsetMax = new Vector2(-60f, -215f);
        Image filo = EstiloMenu.Caja("Filo", det.transform, EstiloMenu.FiloTenue);
        filo.rectTransform.anchorMin = new Vector2(0f, 0f); filo.rectTransform.anchorMax = new Vector2(0f, 1f);
        filo.rectTransform.sizeDelta = new Vector2(2f, 0f);

        t.detIcono = EstiloMenu.Caja("Icono", det.transform, Color.white);
        t.detIcono.preserveAspect = true;
        RectTransform ri = t.detIcono.rectTransform;
        ri.anchorMin = ri.anchorMax = new Vector2(0f, 1f);
        ri.pivot = new Vector2(0f, 1f);
        ri.sizeDelta = new Vector2(96f, 96f);
        ri.anchoredPosition = new Vector2(26f, -22f);
        t.detNombre = EstiloMenu.Texto("", det.transform, 30, EstiloMenu.Titulo);
        t.detNombre.fontStyle = FontStyles.Bold;
        t.detNombre.alignment = TextAlignmentOptions.Left;
        t.detNombre.enableWordWrapping = true;
        EstiloMenu.Arriba(t.detNombre.rectTransform, -40f, 60f);
        t.detNombre.rectTransform.offsetMin = new Vector2(140f, t.detNombre.rectTransform.offsetMin.y);
        t.detNombre.rectTransform.offsetMax = new Vector2(-20f, t.detNombre.rectTransform.offsetMax.y);

        t.detTexto = EstiloMenu.Texto("", det.transform, 25, EstiloMenu.TextoElegido);
        t.detTexto.alignment = TextAlignmentOptions.TopLeft;
        t.detTexto.enableWordWrapping = true;
        t.detTexto.lineSpacing = 8f;
        RectTransform rt = t.detTexto.rectTransform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(26f, 200f);
        rt.offsetMax = new Vector2(-24f, -140f);

        t.detPrecio = EstiloMenu.Texto("", det.transform, 26, EstiloMenu.TextoElegido);
        t.detPrecio.alignment = TextAlignmentOptions.Left;
        RectTransform rp = t.detPrecio.rectTransform;
        rp.anchorMin = new Vector2(0f, 0f); rp.anchorMax = new Vector2(1f, 0f);
        rp.pivot = new Vector2(0.5f, 0f);
        rp.sizeDelta = new Vector2(-50f, 36f);
        rp.anchoredPosition = new Vector2(0f, 150f);

        VerticalLayoutGroup col = new GameObject("Botones", typeof(RectTransform)).AddComponent<VerticalLayoutGroup>();
        col.transform.SetParent(det.transform, false);
        RectTransform rc = (RectTransform)col.transform;
        rc.anchorMin = new Vector2(0f, 0f); rc.anchorMax = new Vector2(1f, 0f);
        rc.pivot = new Vector2(0.5f, 0f);
        rc.sizeDelta = new Vector2(-40f, 130f);
        rc.anchoredPosition = new Vector2(0f, 12f);
        col.spacing = 6f;
        col.childControlHeight = col.childControlWidth = true;
        col.childForceExpandHeight = false;
        t.botonComprar = EstiloMenu.Opcion(col.transform, "Comprar", t.Comprar, 60f, 30f, null, TextAlignmentOptions.Center);
        t.textoComprar = t.botonComprar.GetComponentInChildren<TextMeshProUGUI>();
        t.botonMarcar = EstiloMenu.Opcion(col.transform, "Marcar como deseado (M)", t.Marcar, 52f, 22f, RecursosRPG.Get().Icono("estrella"), TextAlignmentOptions.Left);

        TextMeshProUGUI ayuda = EstiloMenu.Texto("Enter / clic: comprar      M: marcar deseado      F / Esc: salir", go.transform, 22, new Color(0.8f, 0.78f, 0.75f, 0.6f));
        ayuda.rectTransform.anchorMin = ayuda.rectTransform.anchorMax = new Vector2(0.5f, 0.05f);
        ayuda.rectTransform.sizeDelta = new Vector2(1400f, 40f);

        go.SetActive(false);
        return t;
    }

    private Celda NuevaCelda(Transform padre, FichaTienda.Articulo a)
    {
        Celda c = new Celda { art = a };
        GameObject go = new GameObject("Articulo_" + a.id, typeof(RectTransform));
        go.transform.SetParent(padre, false);
        c.fondo = go.AddComponent<Image>();
        c.fondo.color = new Color(0.08f, 0.07f, 0.08f, 0.9f);
        c.boton = go.AddComponent<Button>();
        c.boton.transition = Selectable.Transition.None;
        c.boton.onClick.AddListener(() => { actual = c; Comprar(); });

        Image franja = EstiloMenu.Caja("Franja", go.transform, EstiloMenu.Franja);
        EstiloMenu.Estirar(franja.rectTransform);
        franja.enabled = false;
        Image marca = EstiloMenu.Caja("Marca", go.transform, EstiloMenu.Filo);
        marca.rectTransform.anchorMin = new Vector2(0f, 0f);
        marca.rectTransform.anchorMax = new Vector2(1f, 0f);
        marca.rectTransform.sizeDelta = new Vector2(0f, 4f);
        marca.rectTransform.anchoredPosition = new Vector2(0f, 2f);
        marca.enabled = false;

        c.icono = EstiloMenu.Caja("Icono", go.transform, Color.white);
        c.icono.sprite = RecursosRPG.Get().Icono(Equipo.ClaveIcono(a.objeto));
        c.icono.preserveAspect = true;
        RectTransform ri = c.icono.rectTransform;
        ri.anchorMin = ri.anchorMax = new Vector2(0.5f, 1f);
        ri.pivot = new Vector2(0.5f, 1f);
        ri.sizeDelta = new Vector2(96f, 96f);
        ri.anchoredPosition = new Vector2(0f, -18f);

        c.nombre = EstiloMenu.Texto(a.nombre, go.transform, 21, EstiloMenu.TextoNormal);
        c.nombre.enableWordWrapping = true;
        RectTransform rn = c.nombre.rectTransform;
        rn.anchorMin = new Vector2(0f, 0f); rn.anchorMax = new Vector2(1f, 0f);
        rn.pivot = new Vector2(0.5f, 0f);
        rn.sizeDelta = new Vector2(-16f, 64f);
        rn.anchoredPosition = new Vector2(0f, 44f);

        c.precio = EstiloMenu.Texto("", go.transform, 20, EstiloMenu.TextoNormal);
        RectTransform rp = c.precio.rectTransform;
        rp.anchorMin = new Vector2(0f, 0f); rp.anchorMax = new Vector2(1f, 0f);
        rp.pivot = new Vector2(0.5f, 0f);
        rp.sizeDelta = new Vector2(0f, 30f);
        rp.anchoredPosition = new Vector2(0f, 12f);

        c.agotado = EstiloMenu.Texto("AGOTADO", go.transform, 26, new Color(0.95f, 0.85f, 0.7f));
        c.agotado.fontStyle = FontStyles.Bold;
        c.agotado.characterSpacing = 6f;
        c.agotado.rectTransform.anchoredPosition = new Vector2(0f, 60f);
        c.agotado.rectTransform.sizeDelta = new Vector2(200f, 40f);
        c.agotado.gameObject.SetActive(false);

        c.estrella = EstiloMenu.Caja("Estrella", go.transform, Color.white);
        c.estrella.sprite = RecursosRPG.Get().Icono("estrella");
        c.estrella.preserveAspect = true;
        RectTransform re = c.estrella.rectTransform;
        re.anchorMin = re.anchorMax = new Vector2(1f, 1f);
        re.pivot = new Vector2(1f, 1f);
        re.sizeDelta = new Vector2(40f, 40f);
        re.anchoredPosition = new Vector2(-6f, -6f);
        c.estrella.enabled = false;

        c.candado = EstiloMenu.Caja("Candado", go.transform, Color.white);
        c.candado.sprite = IconosDibujados.Candado();
        c.candado.preserveAspect = true;
        RectTransform rcan = c.candado.rectTransform;
        rcan.anchorMin = rcan.anchorMax = new Vector2(0.5f, 1f);
        rcan.pivot = new Vector2(0.5f, 1f);
        rcan.sizeDelta = new Vector2(48f, 48f);
        rcan.anchoredPosition = new Vector2(0f, -42f);
        c.candado.enabled = false;

        c.estilo = go.AddComponent<OpcionEstilo>();
        c.estilo.franja = franja;
        c.estilo.marca = marca;
        c.estilo.texto = c.nombre;
        return c;
    }
}
