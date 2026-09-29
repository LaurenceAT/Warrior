using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Menu de la hoguera (F junto a una hoguera). El juego se para mientras esta
// abierto. Tres pantallas:
//   - Principal: Descansar / Subir de nivel y estadisticas / Mejorar
//     equipamiento / Levantarse.
//   - Nivel: una fila por estadistica (pulsarla sube un nivel si hay almas) y, a
//     la derecha, todas las estadisticas del personaje con sus numeros exactos,
//     como en Elden Ring.
//   - Equipo: mejorar la espada (Piedras de forja) y los frascos (Lagrimas
//     sagradas).
// Escape (o el boton este del mando) vuelve atras; en la principal, se levanta.
// Al salir, las barras que hayan subido se ven crecer en el HUD.
public class MenuHoguera : MonoBehaviour
{
    private static MenuHoguera instancia;
    public static bool Abierto => instancia != null && (instancia.abierto || instancia.cerradoEnFrame == Time.frameCount);
    // Momento (tiempo real) en que se cerro: la tecla que lo cierra no debe reabrirlo.
    public static float UltimoCierre { get; private set; } = -10f;

    private enum Pagina { Principal, Nivel, Equipo }

    private CanvasGroup grupo;
    private RectTransform paginaPrincipal, paginaNivel, paginaEquipo;
    private Pagina pagina;
    private bool abierto;
    private int cerradoEnFrame = -1;
    private float escalaPrevia = 1f;
    private float abiertoDesde;
    private Hoguera hoguera;
    private PlayerControler player;

    // Principal
    private TextMeshProUGUI textoLugar, avisoPrincipal;
    private Button botonDescansar;

    // Nivel
    private TextMeshProUGUI textoNivel, textoAlmas, textoCoste, avisoNivel, textoFicha;
    private readonly Fila[] filas = new Fila[Progreso.NumEstadisticas];

    // Equipo
    private TextMeshProUGUI textoEspada, textoFrascos, textoMana, textoInventario, avisoEquipo;
    private Button botonEspada, botonFrascos, botonMana;

    private class Fila
    {
        public Progreso.Estadistica estadistica;
        public Button boton;
        public OpcionEstilo estilo;
        public TextMeshProUGUI nivel, valor;
    }

    public static void Abrir() => Abrir(null, Object.FindFirstObjectByType<PlayerControler>());

    public static void Abrir(Hoguera h, PlayerControler p)
    {
        if (instancia == null) instancia = Crear();
        instancia.hoguera = h;
        instancia.player = p;
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
        SonidoMenu.Abrir();
        Mostrar(Pagina.Principal);
        StopAllCoroutines();
        StartCoroutine(Fundido(true));
    }

    private void Cerrar()
    {
        if (!abierto) return;
        abierto = false;
        cerradoEnFrame = Time.frameCount;
        UltimoCierre = Time.unscaledTime;
        Time.timeScale = escalaPrevia;
        SonidoMenu.Cancelar();
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

    private void Mostrar(Pagina p)
    {
        pagina = p;
        paginaPrincipal.gameObject.SetActive(p == Pagina.Principal);
        paginaNivel.gameObject.SetActive(p == Pagina.Nivel);
        paginaEquipo.gameObject.SetActive(p == Pagina.Equipo);
        avisoPrincipal.text = avisoNivel.text = avisoEquipo.text = "";
        Refrescar();
        GameObject primero = p == Pagina.Principal ? botonDescansar.gameObject : p == Pagina.Nivel ? filas[0].boton.gameObject : botonEspada.gameObject;
        SonidoMenu.Silenciar();
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(primero);
    }

    private void Update()
    {
        if (!abierto) return;
        if (Time.unscaledTime - abiertoDesde < 0.2f) return;
        Keyboard k = Keyboard.current;
        Gamepad g = Gamepad.current;
        bool atras = (k != null && k.escapeKey.wasPressedThisFrame) || (g != null && g.buttonEast.wasPressedThisFrame);
        bool f = k != null && k.fKey.wasPressedThisFrame;
        if (atras || (f && pagina == Pagina.Principal))
        {
            if (pagina == Pagina.Principal) Cerrar();
            else { SonidoMenu.Cancelar(); Mostrar(Pagina.Principal); }
            return;
        }
        // Que siempre haya algo elegido (tras un clic en el fondo).
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null)
            EventSystem.current.SetSelectedGameObject(pagina == Pagina.Principal ? botonDescansar.gameObject
                                                     : pagina == Pagina.Nivel ? filas[0].boton.gameObject : botonEspada.gameObject);
    }

    // ------------------------------------------------------------------ Acciones

    private void Descansar()
    {
        if (hoguera != null) hoguera.Descansar(player);
        else { if (player != null) player.CurarCompleto(); ReservaPociones.Get().Rellenar(); }
        avisoPrincipal.text = "Has descansado. Tus frascos se han rellenado.";
    }

    private void Subir(Progreso.Estadistica e)
    {
        if (Progreso.Nivel(e) >= Progreso.NivelMaximo) { avisoNivel.text = "Esa estadística ya está al máximo"; SonidoMenu.Error(); return; }
        if (!Progreso.Subir(e)) { avisoNivel.text = "No tienes almas suficientes"; SonidoMenu.Error(); return; }
        avisoNivel.text = $"{Progreso.Nombre(e)} sube a nivel {Progreso.Nivel(e)}";
        Sonido.Reproducir("subir_nivel");
        // El player aplica el nuevo maximo (vida, estamina, mana) y se cura.
        if (player == null) player = FindFirstObjectByType<PlayerControler>();
        if (player != null) player.AplicarProgreso(true);
        Refrescar();
        StartCoroutine(Pulso((RectTransform)filas[(int)e].boton.transform));
    }

    private void MejorarEspada()
    {
        if (Equipo.NivelEspada >= Equipo.NivelMaximoEspada) { avisoEquipo.text = "La espada ya está al máximo"; SonidoMenu.Error(); return; }
        if (!Equipo.MejorarEspada()) { avisoEquipo.text = "Te faltan Piedras de forja"; SonidoMenu.Error(); return; }
        avisoEquipo.text = $"Espada mejorada a +{Equipo.NivelEspada}";
        Sonido.Reproducir("mejorar_equipo");
        Refrescar();
        StartCoroutine(Pulso((RectTransform)botonEspada.transform));
    }

    private void MejorarCuracion()
    {
        if (Equipo.NivelCuracion >= Equipo.NivelMaximoCuracion) { avisoEquipo.text = "El frasco de sangre ya está al máximo"; SonidoMenu.Error(); return; }
        if (!Equipo.MejorarCuracion()) { avisoEquipo.text = "Te faltan Lágrimas carmesí"; SonidoMenu.Error(); return; }
        avisoEquipo.text = $"Frasco de sangre mejorado a +{Equipo.NivelCuracion}";
        Sonido.Reproducir("mejorar_equipo");
        Refrescar();
        StartCoroutine(Pulso((RectTransform)botonFrascos.transform));
    }

    private void MejorarMana()
    {
        if (Equipo.NivelMana >= Equipo.NivelMaximoMana) { avisoEquipo.text = "El frasco de maná ya está al máximo"; SonidoMenu.Error(); return; }
        if (!Equipo.MejorarMana()) { avisoEquipo.text = "Te faltan Lágrimas celestes"; SonidoMenu.Error(); return; }
        avisoEquipo.text = $"Frasco de maná mejorado a +{Equipo.NivelMana}";
        Sonido.Reproducir("mejorar_equipo");
        Refrescar();
        StartCoroutine(Pulso((RectTransform)botonMana.transform));
    }

    private IEnumerator Pulso(RectTransform r)
    {
        for (float t = 0f; t < 0.3f; t += Time.unscaledDeltaTime)
        {
            r.localScale = Vector3.one * (1f + 0.04f * Mathf.Sin(t / 0.3f * Mathf.PI));
            yield return null;
        }
        r.localScale = Vector3.one;
    }

    // ------------------------------------------------------------------ Textos

    private const string Gris = "#8a8580", Verde = "#9fe0a0", Rojo = "#e08a80", Oro = "#f0d49a";

    private void Refrescar()
    {
        textoLugar.text = hoguera != null ? hoguera.NombreLugar : "Hoguera";

        // Nivel
        textoNivel.text = $"Nivel  <b><color={Oro}>{Progreso.NivelTotal}</color></b>";
        textoAlmas.text = $"Almas  <b>{Progreso.Almas:N0}</b>";
        bool alcanza = Progreso.Almas >= Progreso.CosteSiguiente;
        textoCoste.text = $"Siguiente nivel: <color={(alcanza ? Verde : Rojo)}><b>{Progreso.CosteSiguiente:N0}</b></color> almas";
        foreach (Fila f in filas)
        {
            int n = Progreso.Nivel(f.estadistica);
            bool max = n >= Progreso.NivelMaximo;
            f.nivel.text = max ? "MÁX" : n.ToString();
            f.valor.text = max ? Progreso.Describir(f.estadistica, n)
                               : $"{Progreso.Describir(f.estadistica, n)}  <color={Gris}>→</color>  <color={(alcanza ? Verde : "#aaaaaa")}>{Progreso.Describir(f.estadistica, n + 1)}</color>";
            f.estilo.PonerActiva(alcanza && !max);
        }
        textoFicha.text = Ficha();

        // Equipo
        int ne = Equipo.NivelEspada, nf = Equipo.NivelCuracion, nm = Equipo.NivelMana;
        int piedras = Equipo.Cantidad(Equipo.Objeto.PiedraForja), lagrimas = Equipo.Cantidad(Equipo.Objeto.LagrimaCarmesi);
        int celestes = Equipo.Cantidad(Equipo.Objeto.LagrimaCeleste);
        bool espadaMax = ne >= Equipo.NivelMaximoEspada, frascosMax = nf >= Equipo.NivelMaximoCuracion, manaMax = nm >= Equipo.NivelMaximoMana;
        textoEspada.text = $"<size=120%><b>Espada +{ne}</b></size>  <color={Gris}>/ +{Equipo.NivelMaximoEspada}</color>\n" +
            (espadaMax ? $"Daño  <color={Oro}>x{Equipo.MultiplicadorEspadaEn(ne):0.00}</color>   <color={Gris}>(al máximo)</color>"
                       : $"Daño  x{Equipo.MultiplicadorEspadaEn(ne):0.00}  <color={Gris}>→</color>  <color={Verde}>x{Equipo.MultiplicadorEspadaEn(ne + 1):0.00}</color>\n" +
                         $"Cuesta {Equipo.CosteEspada} Piedra de forja  <color={(piedras >= Equipo.CosteEspada ? Verde : Rojo)}>(tienes {piedras})</color>");
        textoFrascos.text = $"<size=120%><b>Frasco de sangre +{nf}</b></size>  <color={Gris}>/ +{Equipo.NivelMaximoCuracion}</color>\n" +
            (frascosMax ? $"Cura {Pct(Equipo.CuraFrascoEn(nf))} de la vida   <color={Gris}>(al máximo)</color>"
                        : $"Cura {Pct(Equipo.CuraFrascoEn(nf))} <color={Gris}>→</color> <color={Verde}>{Pct(Equipo.CuraFrascoEn(nf + 1))}</color> de la vida\n" +
                          $"Cuesta {Equipo.CosteFrascos} Lágrima carmesí  <color={(lagrimas >= Equipo.CosteFrascos ? Verde : Rojo)}>(tienes {lagrimas})</color>");
        textoMana.text = $"<size=120%><b>Frasco de maná +{nm}</b></size>  <color={Gris}>/ +{Equipo.NivelMaximoMana}</color>\n" +
            (manaMax ? $"Devuelve {Pct(Equipo.ManaFrascoEn(nm))} del maná   <color={Gris}>(al máximo)</color>"
                     : $"Devuelve {Pct(Equipo.ManaFrascoEn(nm))} <color={Gris}>→</color> <color={Verde}>{Pct(Equipo.ManaFrascoEn(nm + 1))}</color> del maná\n" +
                       $"Cuesta {Equipo.CosteFrascos} Lágrima celeste  <color={(celestes >= Equipo.CosteFrascos ? Verde : Rojo)}>(tienes {celestes})</color>");
        botonEspada.GetComponent<OpcionEstilo>().PonerActiva(!espadaMax && piedras >= Equipo.CosteEspada);
        botonFrascos.GetComponent<OpcionEstilo>().PonerActiva(!frascosMax && lagrimas >= Equipo.CosteFrascos);
        botonMana.GetComponent<OpcionEstilo>().PonerActiva(!manaMax && celestes >= Equipo.CosteFrascos);
        textoInventario.text = $"Piedras de forja: <b>{piedras}</b>      Lágrimas carmesí: <b>{lagrimas}</b>      Lágrimas celestes: <b>{celestes}</b>";
    }

    private static string Pct(float f) => Mathf.RoundToInt(f * 100f) + " %";

    // Todas las estadisticas con sus numeros exactos.
    private string Ficha()
    {
        PlayerControler p = player != null ? player : FindFirstObjectByType<PlayerControler>();
        PlayerMana m = p != null ? p.GetComponent<PlayerMana>() : null;
        PlayerStamina s = p != null ? p.GetComponent<PlayerStamina>() : null;
        ReservaPociones r = ReservaPociones.Get();
        int vidaMax = Progreso.VidaMax;
        int vida = p != null ? Mathf.Min(p.VidaActual, vidaMax) : vidaMax;
        int manaMax = Mathf.RoundToInt(Progreso.ManaMax);
        int mana = m != null ? Mathf.RoundToInt(Mathf.Min(m.Actual, Progreso.ManaMax)) : manaMax;
        int estMax = Mathf.RoundToInt(Progreso.EstaminaMax);

        string L(string nombre, string valor) => $"<color=#b8b0a6>{nombre}</color><pos=58%>{valor}\n";
        return
            $"<color={Oro}><b>ATRIBUTOS</b></color>\n" +
            L("Nivel", Progreso.NivelTotal.ToString()) +
            L("Almas", Progreso.Almas.ToString("N0")) +
            "\n" +
            $"<color={Oro}><b>ESTADO</b></color>\n" +
            L("Vida", $"{vida} / {vidaMax}") +
            L("Estamina", $"{(s != null ? Mathf.RoundToInt(Mathf.Min(s.Current, estMax)) : estMax)} / {estMax}") +
            L("Maná", $"{mana} / {manaMax}") +
            "\n" +
            $"<color={Oro}><b>DEFENSA</b></color>\n" +
            L("Resist. a golpes", $"-{Mathf.RoundToInt(Progreso.ResGolpes * 100f)} % físico") +
            L("Resist. a hechizos", $"-{Mathf.RoundToInt(Progreso.ResHechizos * 100f)} % mágico") +
            $"<size=80%><color={Gris}>Tope de las resistencias: {Mathf.RoundToInt(AjustesProgreso.Get().resistenciaMaxima * 100f)} %</color></size>\n" +
            "\n" +
            $"<color={Oro}><b>EQUIPO</b></color>\n" +
            L("Espada", $"+{Equipo.NivelEspada}  (daño x{Equipo.MultiplicadorEspada:0.00})") +
            L("Frasco de sangre", $"{r.Cargas}/{r.Maximo}  · cura {Mathf.RoundToInt(vidaMax * Equipo.CuraFrasco)}") +
            L("Frasco de maná", $"{r.CargasMana}/{r.MaximoMana}  · da {Mathf.RoundToInt(Progreso.ManaMax * Equipo.ManaFrasco)}");
    }

    // ------------------------------------------------------------------ Construccion

    private static MenuHoguera Crear()
    {
        GameObject go = new GameObject("MenuHoguera");
        MenuHoguera m = go.AddComponent<MenuHoguera>();
        m.grupo = EstiloMenu.Lienzo(go, 95);
        m.ConstruirPrincipal(go.transform);
        m.ConstruirNivel(go.transform);
        m.ConstruirEquipo(go.transform);

        TextMeshProUGUI ayuda = EstiloMenu.Texto("Enter / clic: elegir      Esc: volver      F: levantarse", go.transform, 22, new Color(0.8f, 0.78f, 0.75f, 0.6f));
        ayuda.rectTransform.anchorMin = ayuda.rectTransform.anchorMax = new Vector2(0.5f, 0.05f);
        ayuda.rectTransform.sizeDelta = new Vector2(1400f, 40f);

        go.SetActive(false);
        return m;
    }

    private TextMeshProUGUI Aviso(RectTransform padre, float y)
    {
        TextMeshProUGUI t = EstiloMenu.Texto("", padre, 24, new Color(1f, 0.8f, 0.55f));
        t.rectTransform.anchorMin = new Vector2(0f, 0f);
        t.rectTransform.anchorMax = new Vector2(1f, 0f);
        t.rectTransform.sizeDelta = new Vector2(0f, 34f);
        t.rectTransform.anchoredPosition = new Vector2(0f, y);
        return t;
    }

    private void ConstruirPrincipal(Transform raiz)
    {
        RectTransform panel = EstiloMenu.Panel(raiz, "HOGUERA", new Vector2(720f, 600f));
        paginaPrincipal = (RectTransform)panel.parent;
        textoLugar = EstiloMenu.Texto("", panel, 26, new Color(0.85f, 0.78f, 0.68f));
        textoLugar.fontStyle = FontStyles.Italic;
        EstiloMenu.Arriba(textoLugar.rectTransform, -110f, 36f);

        VerticalLayoutGroup col = EstiloMenu.Columna(panel, 70f, 70f, 170f, 110f, 8f);
        RecursosRPG r = RecursosRPG.Get();
        botonDescansar = EstiloMenu.Opcion(col.transform, "Descansar", Descansar, 64f, 32f, r.Icono("ctrl_hoguera"), TextAlignmentOptions.Left);
        EstiloMenu.Opcion(col.transform, "Subir de nivel y estadísticas", () => Mostrar(Pagina.Nivel), 64f, 32f, r.Icono("stat_vida"), TextAlignmentOptions.Left);
        EstiloMenu.Opcion(col.transform, "Mejorar equipamiento", () => Mostrar(Pagina.Equipo), 64f, 32f, r.Icono("ctrl_espada"), TextAlignmentOptions.Left);
        EstiloMenu.Opcion(col.transform, "Levantarse", Cerrar, 64f, 32f, null, TextAlignmentOptions.Left);
        avisoPrincipal = Aviso(panel, 50f);
    }

    private void ConstruirNivel(Transform raiz)
    {
        RectTransform panel = EstiloMenu.Panel(raiz, "SUBIR DE NIVEL", new Vector2(1500f, 820f));
        paginaNivel = (RectTransform)panel.parent;

        // Cabecera: nivel, almas y coste.
        textoNivel = EstiloMenu.Texto("", panel, 30, EstiloMenu.TextoElegido);
        textoNivel.alignment = TextAlignmentOptions.Left;
        EstiloMenu.Arriba(textoNivel.rectTransform, -112f, 40f, 60f);
        textoAlmas = EstiloMenu.Texto("", panel, 30, new Color(0.78f, 0.9f, 1f));
        textoAlmas.alignment = TextAlignmentOptions.Right;
        textoAlmas.rectTransform.anchorMin = new Vector2(0f, 1f);
        textoAlmas.rectTransform.anchorMax = new Vector2(0.62f, 1f);
        textoAlmas.rectTransform.pivot = new Vector2(0.5f, 1f);
        textoAlmas.rectTransform.sizeDelta = new Vector2(0f, 40f);
        textoAlmas.rectTransform.anchoredPosition = new Vector2(-30f, -112f);
        textoCoste = EstiloMenu.Texto("", panel, 24, new Color(0.8f, 0.76f, 0.72f));
        textoCoste.alignment = TextAlignmentOptions.Left;
        EstiloMenu.Arriba(textoCoste.rectTransform, -154f, 34f, 60f);

        // Izquierda: las estadisticas que se pueden subir.
        RectTransform izq = new GameObject("Estadisticas", typeof(RectTransform)).GetComponent<RectTransform>();
        izq.SetParent(panel, false);
        izq.anchorMin = new Vector2(0f, 0f);
        izq.anchorMax = new Vector2(0.62f, 1f);
        izq.offsetMin = new Vector2(40f, 110f);
        izq.offsetMax = new Vector2(-10f, -205f);
        VerticalLayoutGroup col = EstiloMenu.Columna(izq, 0f, 0f, 0f, 0f, 8f);

        string[] iconos = { "stat_vida", "stat_estamina", "stat_mana", "stat_reshechizos", "stat_resgolpes" };
        RecursosRPG r = RecursosRPG.Get();
        for (int i = 0; i < Progreso.NumEstadisticas; i++)
        {
            Progreso.Estadistica e = (Progreso.Estadistica)i;
            Button b = EstiloMenu.Opcion(col.transform, Progreso.Nombre(e), () => Subir(e), 72f, 28f, r.Icono(iconos[i]), TextAlignmentOptions.Left);
            Fila f = new Fila { estadistica = e, boton = b, estilo = b.GetComponent<OpcionEstilo>() };
            // El nombre ocupa la mitad izquierda; luego el nivel y el valor.
            TextMeshProUGUI nombre = b.GetComponentInChildren<TextMeshProUGUI>();
            nombre.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            f.nivel = EstiloMenu.Texto("", b.transform, 30, new Color(1f, 0.85f, 0.6f));
            f.nivel.fontStyle = FontStyles.Bold;
            f.nivel.rectTransform.anchorMin = new Vector2(0.5f, 0f); f.nivel.rectTransform.anchorMax = new Vector2(0.6f, 1f);
            f.nivel.rectTransform.offsetMin = f.nivel.rectTransform.offsetMax = Vector2.zero;
            f.valor = EstiloMenu.Texto("", b.transform, 26, new Color(0.9f, 0.9f, 0.9f));
            f.valor.alignment = TextAlignmentOptions.Right;
            f.valor.rectTransform.anchorMin = new Vector2(0.6f, 0f); f.valor.rectTransform.anchorMax = new Vector2(1f, 1f);
            f.valor.rectTransform.offsetMin = Vector2.zero; f.valor.rectTransform.offsetMax = new Vector2(-20f, 0f);
            filas[i] = f;
        }
        EstiloMenu.Opcion(col.transform, "Volver", () => Mostrar(Pagina.Principal), 56f, 26f);

        // Derecha: la ficha con todos los numeros.
        Image ficha = EstiloMenu.Caja("Ficha", panel, new Color(0f, 0f, 0f, 0.35f));
        RectTransform rf = ficha.rectTransform;
        rf.anchorMin = new Vector2(0.62f, 0f);
        rf.anchorMax = new Vector2(1f, 1f);
        rf.offsetMin = new Vector2(20f, 110f);
        rf.offsetMax = new Vector2(-40f, -112f);
        Image filo = EstiloMenu.Caja("Filo", ficha.transform, EstiloMenu.FiloTenue);
        filo.rectTransform.anchorMin = new Vector2(0f, 0f); filo.rectTransform.anchorMax = new Vector2(0f, 1f);
        filo.rectTransform.sizeDelta = new Vector2(2f, 0f);
        textoFicha = EstiloMenu.Texto("", ficha.transform, 23, EstiloMenu.TextoElegido);
        textoFicha.alignment = TextAlignmentOptions.TopLeft;
        textoFicha.enableWordWrapping = true;
        textoFicha.lineSpacing = 6f;
        EstiloMenu.Estirar(textoFicha.rectTransform, 22f);

        avisoNivel = Aviso(panel, 55f);
    }

    private void ConstruirEquipo(Transform raiz)
    {
        RectTransform panel = EstiloMenu.Panel(raiz, "MEJORAR EQUIPAMIENTO", new Vector2(1100f, 930f));
        paginaEquipo = (RectTransform)panel.parent;

        textoInventario = EstiloMenu.Texto("", panel, 24, new Color(0.85f, 0.8f, 0.72f));
        EstiloMenu.Arriba(textoInventario.rectTransform, -112f, 34f);

        RecursosRPG r = RecursosRPG.Get();
        VerticalLayoutGroup col = EstiloMenu.Columna(panel, 60f, 60f, 165f, 100f, 14f);
        botonEspada = Seccion(col.transform, "Mejorar espada", r.Icono("objeto_piedra"), MejorarEspada, out textoEspada);
        botonFrascos = Seccion(col.transform, "Mejorar frasco de sangre", r.Icono("objeto_lagrima_curacion"), MejorarCuracion, out textoFrascos);
        botonMana = Seccion(col.transform, "Mejorar frasco de maná", r.Icono("objeto_lagrima_mana"), MejorarMana, out textoMana);
        EstiloMenu.Opcion(col.transform, "Volver", () => Mostrar(Pagina.Principal), 56f, 26f);
        avisoEquipo = Aviso(panel, 55f);
    }

    // Una seccion: la opcion (con su icono) y debajo lo que cambia y lo que cuesta.
    private Button Seccion(Transform padre, string titulo, Sprite icono, UnityEngine.Events.UnityAction accion, out TextMeshProUGUI detalle)
    {
        Button b = EstiloMenu.Opcion(padre, titulo, accion, 170f, 30f, icono, TextAlignmentOptions.TopLeft);
        TextMeshProUGUI t = b.GetComponentInChildren<TextMeshProUGUI>();
        t.rectTransform.offsetMax = new Vector2(-20f, -16f);
        // El icono, algo mas grande y arriba.
        Image ico = b.transform.Find("Icono")?.GetComponent<Image>();
        if (ico != null) { ico.rectTransform.sizeDelta = new Vector2(84f, 84f); ico.rectTransform.anchoredPosition = new Vector2(70f, 0f); }
        t.rectTransform.offsetMin = new Vector2(140f, 0f);
        detalle = EstiloMenu.Texto("", b.transform, 24, new Color(0.88f, 0.86f, 0.82f));
        detalle.alignment = TextAlignmentOptions.TopLeft;
        detalle.enableWordWrapping = true;
        detalle.lineSpacing = 8f;
        detalle.rectTransform.anchorMin = Vector2.zero;
        detalle.rectTransform.anchorMax = Vector2.one;
        detalle.rectTransform.offsetMin = new Vector2(140f, 10f);
        detalle.rectTransform.offsetMax = new Vector2(-20f, -60f);
        return b;
    }
}
