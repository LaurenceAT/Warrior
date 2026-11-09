using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// La tienda del totem (desafios). Arriba la frase del totem (letra a letra);
// debajo de las almas, dos pestanas:
//   - Objetos: mejoras (FichaTienda). Van al inventario del desafio y se aplican
//     en la hoguera (MenuHoguera, "Aplicar mejoras").
//   - Amuletos: la coleccion (FichaAmuletos). Se tienen al momento y se equipan
//     en la hoguera ("Amuletos").
// A la izquierda la cuadricula de la pestana; a la derecha lo elegido y el
// boton de comprar. Todo se paga con almas.
//   - Sin almas suficientes: precio en rojo, boton apagado y sonido de error.
//   - Lo comprado queda "Agotado" / "Tuyo" (dura todo el desafio, aunque mueras).
//   - Lista de deseos: M (o el boton de la estrella) marca algo; se ve
//     destacado, el cursor empieza en el y arriba se dice cuanto falta.
// Se maneja con teclado, raton o mando. Q / E (o LB / RB) cambian de pestana.
// F o Esc cierran (Esc no abre la pausa).
public class TiendaTotem : MonoBehaviour
{
    private static TiendaTotem instancia;
    public static bool Abierta => instancia != null && (instancia.abierta || instancia.cerradaEnFrame == Time.frameCount);
    public static float UltimoCierre { get; private set; } = -10f;

    public enum Seccion { Objetos, Amuletos }

    private class Celda
    {
        public FichaTienda.Articulo art;
        public FichaAmuletos.Amuleto amu;
        public Button boton;
        public OpcionEstilo estilo;
        public Image icono, estrella, candado, fondo;
        public TextMeshProUGUI nombre, precio, agotado;
        public string Id => art != null ? art.id : amu.id;
        public string Nombre => art != null ? art.nombre : amu.nombre;
        public Seccion Seccion => amu != null ? Seccion.Amuletos : Seccion.Objetos;
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

    private Seccion seccion = Seccion.Objetos;
    private RectTransform gridObjetos, gridAmuletos;
    private Button pestanaObjetos, pestanaAmuletos;

    // Lo leen las pruebas.
    public static Seccion SeccionActual => instancia != null ? instancia.seccion : Seccion.Objetos;
    public static string Elegido => instancia != null && instancia.actual != null ? instancia.actual.Id : null;

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

    // Pestana (tambien la usan las pruebas).
    public static void IrA(Seccion s)
    {
        if (instancia != null && instancia.abierta) instancia.Cambiar(s, true);
    }

    // Comprar lo que este elegido (las pruebas eligen con el EventSystem).
    public static void ComprarElegido()
    {
        if (instancia != null && instancia.abierta) instancia.Comprar();
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

        // Empieza en la pestana de lo deseado (si hay) o en Objetos.
        Celda deseada = celdas.FirstOrDefault(c => c.Id == Desafio.Deseado);
        Cambiar(deseada != null ? deseada.Seccion : Seccion.Objetos, false);
        if (deseada != null) Seleccionar(deseada);
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
        if ((k != null && (k.escapeKey.wasPressedThisFrame || k.fKey.wasPressedThisFrame)) || (g != null && g.buttonEast.wasPressedThisFrame)
            || (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)) { Cerrar(); return; }
        if ((k != null && k.mKey.wasPressedThisFrame) || (g != null && g.buttonNorth.wasPressedThisFrame)) Marcar();
        if ((k != null && k.qKey.wasPressedThisFrame) || (g != null && g.leftShoulder.wasPressedThisFrame)) Cambiar(Seccion.Objetos, true);
        if ((k != null && k.eKey.wasPressedThisFrame) || (g != null && g.rightShoulder.wasPressedThisFrame)) Cambiar(Seccion.Amuletos, true);
    }

    // ------------------------------------------------------------------ Pestanas

    private void Cambiar(Seccion s, bool sonido)
    {
        bool otra = s != seccion;
        seccion = s;
        gridObjetos.gameObject.SetActive(s == Seccion.Objetos);
        gridAmuletos.gameObject.SetActive(s == Seccion.Amuletos);
        Pestana(pestanaObjetos, s == Seccion.Objetos);
        Pestana(pestanaAmuletos, s == Seccion.Amuletos);
        if (sonido && otra) SonidoMenu.Mover();
        Refrescar();
        if (actual == null || actual.Seccion != s)
        {
            Celda inicio = celdas.FirstOrDefault(c => c.Seccion == s && c.Id == Desafio.Deseado) ?? celdas.FirstOrDefault(c => c.Seccion == s);
            Seleccionar(inicio);
        }
    }

    private static void Pestana(Button b, bool activa)
    {
        Image fondo = b.GetComponent<Image>();
        fondo.color = activa ? new Color(0.42f, 0.31f, 0.14f, 0.95f) : new Color(0.08f, 0.07f, 0.08f, 0.9f);
        Image filo = b.transform.Find("Filo").GetComponent<Image>();
        filo.color = activa ? EstiloMenu.Filo : EstiloMenu.FiloTenue;
        b.GetComponentInChildren<TextMeshProUGUI>().color = activa ? EstiloMenu.TextoElegido : EstiloMenu.TextoNormal;
    }

    // ------------------------------------------------------------------ Acciones

    private static bool Vendido(Celda c) => c.amu != null ? Amuletos.Tiene(c.amu.id) : Desafio.Comprados.Contains(c.art.id);
    private static bool Bloqueado(Celda c) => c.art != null && !string.IsNullOrEmpty(c.art.requiere) && !Desafio.Comprados.Contains(c.art.requiere);

    // Ya al maximo, contando lo que hay sin aplicar (no tiene sentido comprarlo).
    private static bool AlMaximo(Celda c) => c.art != null && Equipo.AlMaximo(c.art.objeto, Desafio.PorAplicar(c.art.objeto));

    // Llave del osario: el totem cobra menos.
    private static int Precio(Celda c) =>
        Mathf.Max(0, Mathf.RoundToInt((c.art != null ? Desafio.Precio(c.art) : PrecioAmuleto(c.amu)) * EfectosAmuletos.MultPrecios));

    // Precio de un amuleto en este desafio (multiplicador de la ficha del jefe).
    public static int PrecioAmuleto(FichaAmuletos.Amuleto a) =>
        a == null ? 0 : Mathf.Max(0, Mathf.RoundToInt(a.precio * (Desafio.Activo && Desafio.Ficha != null ? Mathf.Max(0.1f, Desafio.Ficha.multPrecios) : 1f)));

    private static Sprite Icono(Celda c) => c.amu != null ? c.amu.icono : RecursosRPG.Get().Icono(Equipo.ClaveIcono(c.art.objeto));

    private void Comprar()
    {
        if (actual == null) return;
        Celda c = actual;
        if (Vendido(c) || Bloqueado(c) || AlMaximo(c) || Progreso.Almas < Precio(c)) { SonidoMenu.Error(); return; }
        if (!Progreso.Gastar(Precio(c))) { SonidoMenu.Error(); return; }
        if (c.amu != null)
        {
            Amuletos.Dar(c.amu.id);
            AvisoObjeto.Mostrar(c.amu.icono, c.amu.nombre, "Equípalo en la hoguera.");
        }
        else
        {
            Desafio.GuardarObjeto(c.art.objeto);
            Desafio.Comprados.Add(c.art.id);
            AvisoObjeto.Mostrar(Icono(c), c.art.nombre, CofreMejora.AvisoDesafio);
        }
        if (Desafio.Deseado == c.Id) Desafio.Deseado = null;
        Sonido.Reproducir("mejorar_equipo");
        Refrescar();
    }

    private void Marcar()
    {
        if (actual == null || Vendido(actual)) { SonidoMenu.Error(); return; }
        Desafio.Deseado = Desafio.Deseado == actual.Id ? null : actual.Id;
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
        Celda d = celdas.FirstOrDefault(c => c.Id == Desafio.Deseado);
        if (d == null) deseo.text = $"<color={Gris}>Marca algo con M para tenerlo a la vista</color>";
        else if (Bloqueado(d)) deseo.text = $"Deseado: <b>{d.Nombre}</b> ({Precio(d):N0})  ·  <color={Gris}>antes: {celdas.FirstOrDefault(c => c.art != null && c.art.id == d.art.requiere)?.Nombre}</color>";
        else if (alm >= Precio(d)) deseo.text = $"Deseado: <b>{d.Nombre}</b>  ·  <color={Verde}>Puedes comprarlo</color>";
        else deseo.text = $"Deseado: <b>{d.Nombre}</b> ({Precio(d):N0})  ·  <color={Rojo}>te faltan {Precio(d) - alm:N0}</color>";

        foreach (Celda c in celdas)
        {
            bool vendido = Vendido(c), bloq = Bloqueado(c), max = AlMaximo(c);
            bool alcanza = alm >= Precio(c);
            c.agotado.gameObject.SetActive(vendido || max);
            c.agotado.text = vendido ? (c.amu != null ? "TUYO" : "AGOTADO") : "AL MÁXIMO";
            c.candado.enabled = bloq && !vendido;
            c.estrella.enabled = Desafio.Deseado == c.Id;
            c.precio.text = vendido ? "" : $"<color={(alcanza ? "#f0d49a" : Rojo)}>{Precio(c):N0}</color>{(c.amu != null ? "" : " almas")}";
            float atenuar = vendido || bloq || max ? 0.35f : 1f;
            c.icono.color = new Color(atenuar, atenuar, atenuar, 1f);
            c.fondo.color = Desafio.Deseado == c.Id ? new Color(0.35f, 0.26f, 0.1f, 0.9f) : new Color(0.08f, 0.07f, 0.08f, 0.9f);
            c.estilo.PonerActiva(!vendido && !bloq && !max && alcanza);
        }
        Detalle();
    }

    private void Detalle()
    {
        if (actual == null) return;
        Celda c = actual;
        detIcono.sprite = Icono(c);
        detIcono.enabled = detIcono.sprite != null;
        detNombre.text = c.Nombre;
        bool vendido = Vendido(c), bloq = Bloqueado(c), max = AlMaximo(c);
        string efecto;
        if (c.amu != null)
        {
            efecto = (string.IsNullOrEmpty(c.amu.descripcion) ? $"<color={Gris}>Amuleto.</color>" : c.amu.descripcion) +
                     (vendido ? $"\n\n<size=85%><color=#f0d49a>Ya es tuyo: equípalo en la hoguera (Amuletos).</color></size>"
                              : $"\n\n<size=85%><color=#f0d49a>Al comprarlo es tuyo: se equipa en la hoguera (Amuletos).</color></size>");
        }
        else
        {
            efecto = Efecto(c.art);
            if (bloq)
            {
                Celda req = celdas.FirstOrDefault(x => x.art != null && x.art.id == c.art.requiere);
                efecto += $"\n\n<color={Rojo}>Compra antes: {(req != null ? req.Nombre : c.art.requiere)}</color>";
            }
        }
        detTexto.text = efecto;
        bool alcanza = Progreso.Almas >= Precio(c);
        string agotado = c.amu != null ? "Ya es tuyo" : "Agotado";
        detPrecio.text = vendido ? agotado : max ? "Ya está al máximo" : $"Precio: <color={(alcanza ? "#f0d49a" : Rojo)}><b>{Precio(c):N0}</b></color> almas";
        textoComprar.text = vendido ? agotado : $"Comprar ({Precio(c):N0})";
        botonComprar.GetComponent<OpcionEstilo>().PonerActiva(!vendido && !bloq && !max && alcanza);
        botonMarcar.GetComponentInChildren<TextMeshProUGUI>().text = Desafio.Deseado == c.Id ? "Quitar de deseos (M)" : "Marcar como deseado (M)";
    }

    // Lo que hace el objeto, con los numeros de despues de aplicar tambien lo
    // que ya tienes sin aplicar. Lo comprado va al inventario.
    private static string Efecto(FichaTienda.Articulo a)
    {
        string que;
        switch (a.objeto)
        {
            case Equipo.Objeto.PiedraForja: que = "Mejora la espada."; break;
            case Equipo.Objeto.LagrimaCarmesi: que = "Cada frasco de sangre cura más."; break;
            case Equipo.Objeto.LagrimaCeleste: que = "Cada frasco de maná devuelve más."; break;
            case Equipo.Objeto.FrascoSangre: que = "Una carga más para el frasco de sangre."; break;
            case Equipo.Objeto.FrascoMana: que = "Una carga más para el frasco de maná."; break;
            case Equipo.Objeto.VestigioVacio: que = "Un resto de algo que ya no existe. Abre un hueco de amuleto."; break;
            default: return Equipo.Descripcion(a.objeto);
        }
        int pendientes = Desafio.PorAplicar(a.objeto);
        return $"{que}\n{VistaPrevia(a.objeto, pendientes)}" +
               (pendientes > 0 ? $"\n<size=85%><color={Gris}>Ya tienes {pendientes} sin aplicar.</color></size>" : "") +
               $"\n\n<size=85%><color=#f0d49a>Se guarda en tu inventario: aplícalo en la hoguera.</color></size>";
    }

    // "Curación de sangre: 40 → 55": el efecto de aplicar uno mas, contando
    // "antes" ya aplicados (lo que esta pendiente). Lo usa tambien la hoguera.
    public static string VistaPrevia(Equipo.Objeto o, int antes = 0)
    {
        ReservaPociones r = ReservaPociones.Get();
        int vida = Progreso.VidaMax, mana = Mathf.RoundToInt(Progreso.ManaMax);
        switch (o)
        {
            case Equipo.Objeto.PiedraForja:
                int ne = Equipo.NivelEspada + antes;
                return $"Daño de la espada: x{Equipo.MultiplicadorEspadaEn(ne):0.00} → <color={Verde}>x{Equipo.MultiplicadorEspadaEn(ne + 1):0.00}</color>";
            case Equipo.Objeto.LagrimaCarmesi:
                int nc = Equipo.NivelCuracion + antes;
                return $"Curación de sangre: {Mathf.RoundToInt(vida * Equipo.CuraFrascoEn(nc))} → <color={Verde}>{Mathf.RoundToInt(vida * Equipo.CuraFrascoEn(nc + 1))}</color>";
            case Equipo.Objeto.LagrimaCeleste:
                int nm = Equipo.NivelMana + antes;
                return $"Recuperación de maná: {Mathf.RoundToInt(mana * Equipo.ManaFrascoEn(nm))} → <color={Verde}>{Mathf.RoundToInt(mana * Equipo.ManaFrascoEn(nm + 1))}</color>";
            case Equipo.Objeto.FrascoSangre:
                return $"Cargas del frasco de sangre: {r.Maximo + antes} → <color={Verde}>{r.Maximo + antes + 1}</color>";
            case Equipo.Objeto.FrascoMana:
                return $"Cargas del frasco de maná: {r.MaximoMana + antes} → <color={Verde}>{r.MaximoMana + antes + 1}</color>";
            case Equipo.Objeto.VestigioVacio:
                int h = Amuletos.HuecosAbiertos + antes;
                return $"Huecos de amuleto: {h} → <color={Verde}>{Mathf.Min(h + 1, Amuletos.HuecosMaximos)}</color>";
            default: return Equipo.Descripcion(o);
        }
    }

    // ------------------------------------------------------------------ Construccion

    private const float ArribaCuadricula = 275f;

    private static TiendaTotem Crear(FichaTienda f)
    {
        GameObject go = new GameObject("TiendaTotem");
        TiendaTotem t = go.AddComponent<TiendaTotem>();
        t.grupo = EstiloMenu.Lienzo(go, 92);
        RectTransform panel = EstiloMenu.Panel(go.transform, "TÓTEM", new Vector2(1560f, 960f));

        t.frase = EstiloMenu.Texto("", panel, 26, new Color(0.62f, 0.95f, 1f));
        t.frase.fontStyle = FontStyles.Italic;
        EstiloMenu.Arriba(t.frase.rectTransform, -112f, 40f, 60f);

        t.almas = EstiloMenu.Texto("", panel, 28, new Color(0.78f, 0.9f, 1f));
        t.almas.alignment = TextAlignmentOptions.Left;
        EstiloMenu.Arriba(t.almas.rectTransform, -162f, 36f, 70f);
        t.deseo = EstiloMenu.Texto("", panel, 22, EstiloMenu.TextoNormal);
        t.deseo.alignment = TextAlignmentOptions.Right;
        EstiloMenu.Arriba(t.deseo.rectTransform, -166f, 32f, 70f);

        // Las dos pestanas, bajo las almas.
        t.pestanaObjetos = NuevaPestana(panel, "Objetos   <size=70%><color=#8a8580>[Q]</color></size>", 0, () => t.Cambiar(Seccion.Objetos, true));
        t.pestanaAmuletos = NuevaPestana(panel, "Amuletos   <size=70%><color=#8a8580>[E]</color></size>", 1, () => t.Cambiar(Seccion.Amuletos, true));

        // Izquierda: las dos cuadriculas (una por pestana).
        t.gridObjetos = Cuadricula(panel, "Objetos", new Vector2(204f, 186f), 4);
        foreach (FichaTienda.Articulo a in f.articulos) t.celdas.Add(t.NuevaCelda(t.gridObjetos, a, null));
        t.gridAmuletos = Cuadricula(panel, "Amuletos", new Vector2(98f, 108f), 8);
        FichaAmuletos fa = FichaAmuletos.Get();
        if (fa != null) foreach (FichaAmuletos.Amuleto a in fa.amuletos) if (a != null) t.celdas.Add(t.NuevaCelda(t.gridAmuletos, null, a));

        // Derecha: el detalle.
        Image det = EstiloMenu.Caja("Detalle", panel, new Color(0f, 0f, 0f, 0.35f));
        RectTransform rd = det.rectTransform;
        rd.anchorMin = new Vector2(0.6f, 0f);
        rd.anchorMax = new Vector2(1f, 1f);
        rd.offsetMin = new Vector2(20f, 90f);
        rd.offsetMax = new Vector2(-60f, -ArribaCuadricula);
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

        TextMeshProUGUI ayuda = EstiloMenu.Texto("Enter / clic: comprar      Q / E: pestaña      M: marcar deseado      F / Esc: salir", go.transform, 22, new Color(0.8f, 0.78f, 0.75f, 0.6f));
        ayuda.rectTransform.anchorMin = ayuda.rectTransform.anchorMax = new Vector2(0.5f, 0.03f);
        ayuda.rectTransform.sizeDelta = new Vector2(1400f, 40f);

        go.SetActive(false);
        return t;
    }

    // Una pestana: rectangulo con su nombre, una al lado de la otra bajo las almas.
    private static Button NuevaPestana(RectTransform panel, string texto, int indice, UnityEngine.Events.UnityAction accion)
    {
        Image fondo = EstiloMenu.Caja("Pestana_" + indice, panel, new Color(0.08f, 0.07f, 0.08f, 0.9f));
        fondo.raycastTarget = true;
        RectTransform r = fondo.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
        r.pivot = new Vector2(0f, 1f);
        r.sizeDelta = new Vector2(240f, 52f);
        r.anchoredPosition = new Vector2(60f + indice * 254f, -208f);
        Image filo = EstiloMenu.Caja("Filo", fondo.transform, EstiloMenu.FiloTenue);
        filo.rectTransform.anchorMin = new Vector2(0f, 0f);
        filo.rectTransform.anchorMax = new Vector2(1f, 0f);
        filo.rectTransform.sizeDelta = new Vector2(0f, 3f);
        filo.rectTransform.anchoredPosition = new Vector2(0f, 1.5f);
        TextMeshProUGUI t = EstiloMenu.Texto(texto, fondo.transform, 26, EstiloMenu.TextoNormal);
        EstiloMenu.Estirar(t.rectTransform);
        Button b = fondo.gameObject.AddComponent<Button>();
        b.transition = Selectable.Transition.None;
        // Las pestanas se cambian con clic o Q / E: no roban la seleccion de la cuadricula.
        b.navigation = new Navigation { mode = Navigation.Mode.None };
        b.onClick.AddListener(accion);
        return b;
    }

    private static RectTransform Cuadricula(RectTransform panel, string nombre, Vector2 celda, int columnas)
    {
        RectTransform izq = new GameObject(nombre, typeof(RectTransform)).GetComponent<RectTransform>();
        izq.SetParent(panel, false);
        izq.anchorMin = new Vector2(0f, 0f);
        izq.anchorMax = new Vector2(0.6f, 1f);
        izq.offsetMin = new Vector2(60f, 90f);
        izq.offsetMax = new Vector2(-10f, -ArribaCuadricula);
        GridLayoutGroup grid = izq.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = celda;
        grid.spacing = new Vector2(columnas > 4 ? 8f : 14f, columnas > 4 ? 8f : 14f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = columnas;
        return izq;
    }

    // Una celda: objeto (grande, con nombre) o amuleto (pequena: imagen y precio).
    private Celda NuevaCelda(Transform padre, FichaTienda.Articulo a, FichaAmuletos.Amuleto amu)
    {
        Celda c = new Celda { art = a, amu = amu };
        bool pequena = amu != null;
        GameObject go = new GameObject("Articulo_" + c.Id, typeof(RectTransform));
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
        c.icono.sprite = Icono(c);
        c.icono.preserveAspect = true;
        RectTransform ri = c.icono.rectTransform;
        ri.anchorMin = ri.anchorMax = new Vector2(0.5f, 1f);
        ri.pivot = new Vector2(0.5f, 1f);
        ri.sizeDelta = pequena ? new Vector2(62f, 62f) : new Vector2(78f, 78f);
        ri.anchoredPosition = new Vector2(0f, pequena ? -8f : -10f);

        // Los amuletos no llevan el nombre en la celda (sale a la derecha).
        c.nombre = EstiloMenu.Texto(pequena ? "" : a.nombre, go.transform, 21, EstiloMenu.TextoNormal);
        c.nombre.enableWordWrapping = true;
        RectTransform rn = c.nombre.rectTransform;
        rn.anchorMin = new Vector2(0f, 0f); rn.anchorMax = new Vector2(1f, 0f);
        rn.pivot = new Vector2(0.5f, 0f);
        rn.sizeDelta = new Vector2(-16f, 56f);
        rn.anchoredPosition = new Vector2(0f, 40f);
        c.nombre.gameObject.SetActive(!pequena);

        c.precio = EstiloMenu.Texto("", go.transform, pequena ? 18 : 20, EstiloMenu.TextoNormal);
        RectTransform rp = c.precio.rectTransform;
        rp.anchorMin = new Vector2(0f, 0f); rp.anchorMax = new Vector2(1f, 0f);
        rp.pivot = new Vector2(0.5f, 0f);
        rp.sizeDelta = new Vector2(0f, 28f);
        rp.anchoredPosition = new Vector2(0f, pequena ? 6f : 10f);

        c.agotado = EstiloMenu.Texto("AGOTADO", go.transform, pequena ? 18 : 26, new Color(0.95f, 0.85f, 0.7f));
        c.agotado.fontStyle = FontStyles.Bold;
        c.agotado.characterSpacing = pequena ? 2f : 6f;
        c.agotado.rectTransform.anchoredPosition = new Vector2(0f, pequena ? -34f : 40f);
        c.agotado.rectTransform.sizeDelta = new Vector2(pequena ? 96f : 200f, 40f);
        c.agotado.gameObject.SetActive(false);

        c.estrella = EstiloMenu.Caja("Estrella", go.transform, Color.white);
        c.estrella.sprite = RecursosRPG.Get().Icono("estrella");
        c.estrella.preserveAspect = true;
        RectTransform re = c.estrella.rectTransform;
        re.anchorMin = re.anchorMax = new Vector2(1f, 1f);
        re.pivot = new Vector2(1f, 1f);
        re.sizeDelta = pequena ? new Vector2(26f, 26f) : new Vector2(40f, 40f);
        re.anchoredPosition = new Vector2(-4f, -4f);
        c.estrella.enabled = false;

        c.candado = EstiloMenu.Caja("Candado", go.transform, Color.white);
        c.candado.sprite = IconosDibujados.Candado();
        c.candado.preserveAspect = true;
        RectTransform rcan = c.candado.rectTransform;
        rcan.anchorMin = rcan.anchorMax = new Vector2(0.5f, 1f);
        rcan.pivot = new Vector2(0.5f, 1f);
        rcan.sizeDelta = new Vector2(48f, 48f);
        rcan.anchoredPosition = new Vector2(0f, -34f);
        c.candado.enabled = false;

        c.estilo = go.AddComponent<OpcionEstilo>();
        c.estilo.franja = franja;
        c.estilo.marca = marca;
        c.estilo.texto = pequena ? c.precio : c.nombre;
        return c;
    }
}
