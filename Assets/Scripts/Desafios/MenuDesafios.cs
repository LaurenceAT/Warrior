using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Pantalla de Desafios del menu principal. No necesita partida.
//   - Izquierda: los jefes, con miniatura, nombre, marcas (completado en Normal
//     y en Dificil), mejor tiempo y un punto si hay algo nuevo en su ficha.
//   - Derecha, arriba: imagen grande del jefe (respira despacio) y su nombre.
//   - Derecha, en medio: pestanas Ataques / Debilidades / Fases / Historia /
//     Estadisticas. Todo empieza oculto y se desbloquea peleando (Bitacora).
//   - Derecha, abajo (siempre a la vista): Normal / Dificil y DESAFIAR.
// Navegacion por capas: pasar el raton solo resalta; clic o Enter selecciona y
// se queda marcado. Atras (clic derecho o Esc) vuelve un paso: de la ficha a la
// lista, y de la lista al menu principal.
// Codigos secretos (CodigosSecretos): mientras esta pantalla esta abierta y sin
// confirmacion, se escuchan las letras que se escriben (sin cuadro de texto).
public class MenuDesafios : MonoBehaviour
{
    private class Fila
    {
        public FichaJefe ficha;
        public Button boton;
        public OpcionEstilo estilo;
        public TextMeshProUGUI texto;
        public Image marcaNormal, marcaDificil;
        public PuntoNuevo nuevo;
    }

    private class Pestana
    {
        public Bitacora.Seccion seccion;
        public Button boton;
        public OpcionEstilo estilo;
        public TextMeshProUGUI texto;
        public PuntoNuevo nuevo;
    }

    private static readonly string[] NombresSeccion = { "Ataques", "Debilidades", "Fases", "Historia", "Estadísticas" };
    private static readonly string[] IconosSeccion = { "ficha_ataques", "ficha_debilidades", "ficha_fases", "ficha_historia", "ficha_estadisticas" };

    private const string Gris = "#8f8780", Verde = "#9fe0a0", Rojo = "#e0776c", Oro = "#f0d49a", Nuevo = "#ffb84d";

    private readonly List<Fila> filas = new List<Fila>();
    private readonly List<Pestana> pestanas = new List<Pestana>();
    private readonly CapasMenu capas = new CapasMenu();
    private readonly GrupoMarcado grupoJefes = new GrupoMarcado(), grupoPestanas = new GrupoMarcado(), grupoDificultad = new GrupoMarcado();
    private FichaJefe elegido;
    private Bitacora.Seccion seccion;
    private bool dificil;

    private RectTransform ficha, marcoImagen, contenido;
    private CanvasGroup grupoFicha;
    private ScrollRect scroll;
    private GameObject placeholder;
    private Image imagen;
    private TextMeshProUGUI nombre, subtitulo, progreso, avisoDificultad;
    private Button botonNormal, botonDificil, botonDesafiar, botonVolver;
    private Image candadoDificil;
    private GameObject confirmacion;
    private TextMeshProUGUI textoConfirmacion;
    private Button botonEntrar;
    private System.Action volverAlMenu;
    private Coroutine transicion;
    // Codigos secretos: lo ultimo escrito (sin mostrarlo) y cuando.
    private Transform listaJefes;
    private TextMeshProUGUI susurro;
    private string escrito = "";
    private float ultimaLetra;

    public static MenuDesafios Construir(Transform lienzo, System.Action volver)
    {
        RectTransform raiz = new GameObject("Desafios", typeof(RectTransform)).GetComponent<RectTransform>();
        raiz.SetParent(lienzo, false);
        EstiloMenu.Estirar(raiz);
        MenuDesafios m = raiz.gameObject.AddComponent<MenuDesafios>();
        m.volverAlMenu = volver;
        m.Montar(raiz);
        return m;
    }

    // Al entrar en la pantalla: todo al dia y ningun jefe elegido (salvo al
    // volver de un desafio: el de ese desafio).
    public void Abrir()
    {
        Bitacora.Migrar();
        confirmacion.SetActive(false);
        capas.Vaciar();
        foreach (Fila f in filas) PintarFila(f);
        FichaJefe inicial = Desafio.Ultima != null ? filas.FirstOrDefault(f => f.ficha == Desafio.Ultima)?.ficha : null;
        Desafio.Ultima = null;
        if (inicial != null) Seleccionar(inicial);
        else Deseleccionar();
        RevelarSecretos();
    }

    private IEnumerator Revelar(Fila f)
    {
        Sonido.Reproducir("secreto_descubierto", 0.9f);
        CanvasGroup g = f.boton.gameObject.AddComponent<CanvasGroup>();
        g.alpha = 0f;
        Image brillo = EstiloMenu.Caja("Revelado", f.boton.transform, new Color(0.75f, 1f, 0.8f, 0f));
        brillo.sprite = EstiloMenu.Resplandor();
        brillo.raycastTarget = false;
        EstiloMenu.Estirar(brillo.rectTransform, -14f);
        const float dura = 1.8f;
        for (float t = 0f; t < dura; t += Time.unscaledDeltaTime)
        {
            g.alpha = Mathf.Clamp01(t / 0.9f);
            brillo.color = new Color(0.75f, 1f, 0.8f, 0.85f * Mathf.Sin(Mathf.PI * t / dura));
            yield return null;
        }
        g.alpha = 1f;
        Destroy(brillo.gameObject);
        Destroy(g);
    }

    // Lo que se elige al entrar (y al volver con el teclado a la pantalla).
    public GameObject Primero()
    {
        if (confirmacion.activeSelf) return botonEntrar.gameObject;
        if (elegido != null) return (pestanas.FirstOrDefault(p => p.seccion == seccion) ?? pestanas[0]).boton.gameObject;
        return filas.Count > 0 ? filas[0].boton.gameObject : botonVolver.gameObject;
    }

    // Atras (Esc / clic derecho): un paso. False si ya estaba en la lista (el
    // menu principal vuelve entonces a su pantalla).
    public bool Atras() => capas.Atras();

    private void Update()
    {
        // Respira: la imagen del jefe crece y se encoge muy poco.
        if (marcoImagen != null && elegido != null)
        {
            float t = Time.unscaledTime;
            imagen.rectTransform.localScale = new Vector3(1f - 0.006f * Mathf.Sin(t * 1.7f), 1f + 0.014f * Mathf.Sin(t * 1.7f), 1f);
            imagen.rectTransform.anchoredPosition = new Vector2(0f, 2f * Mathf.Sin(t * 0.85f));
        }
        // Rueda del raton, RePag / AvPag o el stick derecho: desplazar la ficha.
        if (scroll != null && elegido != null && !confirmacion.activeSelf)
        {
            float eje = 0f;
            Keyboard k = Keyboard.current;
            Gamepad g = Gamepad.current;
            if (k != null && k.pageDownKey.isPressed) eje -= 1f;
            if (k != null && k.pageUpKey.isPressed) eje += 1f;
            if (g != null) eje += g.rightStick.ReadValue().y;
            float alto = contenido.rect.height - scroll.viewport.rect.height;
            if (Mathf.Abs(eje) > 0.2f && alto > 0f)
                scroll.verticalNormalizedPosition = Mathf.Clamp01(scroll.verticalNormalizedPosition + eje * Time.unscaledDeltaTime * 700f / alto);
        }
    }

    // ------------------------------------------------------------------ Capas

    private void Seleccionar(FichaJefe f)
    {
        if (f == null) return;
        bool yaDentro = elegido != null;
        elegido = f;
        if (!yaDentro) capas.Entrar(Deseleccionar);
        grupoJefes.Marcar(filas.First(x => x.ficha == f).estilo);
        placeholder.SetActive(false);
        ficha.gameObject.SetActive(true);
        if (!Desafio.DificilDesbloqueado(f)) dificil = false;
        PintarFicha();
        // Se abre por la pestana con algo nuevo (si no, Ataques).
        Bitacora.Seccion inicial = Bitacora.Seccion.Ataques;
        foreach (Bitacora.Seccion s in new[] { Bitacora.Seccion.Ataques, Bitacora.Seccion.Debilidades, Bitacora.Seccion.Fases, Bitacora.Seccion.Historia })
            if (Bitacora.HayNuevo(f, s)) { inicial = s; break; }
        MostrarSeccion(inicial);
        if (transicion != null) StopCoroutine(transicion);
        ficha.anchoredPosition = Vector2.zero;
        transicion = StartCoroutine(TransicionMenu.Entrar(ficha, grupoFicha));
        SonidoMenu.Silenciar();
        EventSystem.current?.SetSelectedGameObject(Primero());
    }

    private void Deseleccionar()
    {
        FichaJefe antes = elegido;
        elegido = null;
        grupoJefes.Desmarcar();
        ficha.gameObject.SetActive(false);
        placeholder.SetActive(true);
        foreach (Fila f in filas) PintarFila(f);
        GameObject foco = filas.FirstOrDefault(f => f.ficha == antes)?.boton.gameObject ?? Primero();
        SonidoMenu.Silenciar();
        EventSystem.current?.SetSelectedGameObject(foco);
    }

    private void Volver()
    {
        capas.Vaciar();
        if (elegido != null) Deseleccionar();
        volverAlMenu?.Invoke();
    }

    // ------------------------------------------------------------------ Ficha

    private void PintarFicha()
    {
        FichaJefe f = elegido;
        imagen.sprite = f.imagen;
        imagen.enabled = f.imagen != null;
        nombre.text = f.nombre.ToUpper();
        Globales.Registro n = Globales.Desafio(f.id, false), d = Globales.Desafio(f.id, true);
        string Estado(string dif, Globales.Registro r) =>
            r != null && r.completado ? $"<color={Verde}>{dif}  {Globales.Tiempo(r.mejorTiempo)}</color>" : $"<color={Gris}>{dif}  —</color>";
        subtitulo.text = $"{Estado("Normal", n)}      {Estado("Difícil", d)}";
        int ataques = f.ataques.Count(a => Bitacora.Tiene(Bitacora.ClaveAtaque(f, a.id)));
        int elementos = Elementos.Todos.Count(e => Bitacora.Tiene(Bitacora.ClaveElemento(f, e)));
        progreso.text = ataques + elementos == 0 && !Bitacora.HayEstadisticas(f)
            ? $"<color={Gris}><i>Aún no sabes nada de este enemigo. Enfréntate a él para descubrirlo.</i></color>"
            : $"<color={Gris}>Descubierto:</color>  {ataques}/{f.ataques.Count} ataques   ·   {elementos}/{Elementos.Cantidad} elementos";
        foreach (Pestana p in pestanas) PintarPestana(p);
        pestanas[(int)Bitacora.Seccion.Estadisticas].boton.gameObject.SetActive(Bitacora.HayEstadisticas(f));
        PintarDificultad();
    }

    private void PintarPestana(Pestana p)
    {
        FichaJefe f = elegido;
        string cuenta = "";
        if (p.seccion == Bitacora.Seccion.Ataques)
            cuenta = $"  <size=80%><color={Gris}>{f.ataques.Count(a => Bitacora.Tiene(Bitacora.ClaveAtaque(f, a.id)))}/{f.ataques.Count}</color></size>";
        else if (p.seccion == Bitacora.Seccion.Debilidades)
            cuenta = $"  <size=80%><color={Gris}>{Elementos.Todos.Count(e => Bitacora.Tiene(Bitacora.ClaveElemento(f, e)))}/{Elementos.Cantidad}</color></size>";
        p.texto.text = NombresSeccion[(int)p.seccion] + cuenta;
        p.nuevo.Poner(Bitacora.HayNuevo(f, p.seccion));
    }

    private void MostrarSeccion(Bitacora.Seccion s)
    {
        seccion = s;
        Pestana p = pestanas[(int)s];
        grupoPestanas.Marcar(p.estilo);
        for (int i = contenido.childCount - 1; i >= 0; i--) { GameObject h = contenido.GetChild(i).gameObject; h.transform.SetParent(null); Destroy(h); }
        switch (s)
        {
            case Bitacora.Seccion.Ataques: Ataques(); break;
            case Bitacora.Seccion.Debilidades: Debilidades(); break;
            case Bitacora.Seccion.Fases: Fases(); break;
            case Bitacora.Seccion.Historia: Historia(); break;
            default: Estadisticas(); break;
        }
        scroll.verticalNormalizedPosition = 1f;
        // Visto: el punto de "nuevo" se va (el texto de esta vez aun lo marca).
        Bitacora.MarcarVisto(elegido, s);
        PintarPestana(p);
        PintarFila(filas.First(x => x.ficha == elegido));
    }

    private static string MarcaNuevo(string clave) => Bitacora.Nuevo(clave) ? $"  <size=70%><color={Nuevo}>NUEVO</color></size>" : "";

    private void Ataques()
    {
        FichaJefe f = elegido;
        List<FichaJefe.Ataque> vistos = f.ataques.Where(a => Bitacora.Tiene(Bitacora.ClaveAtaque(f, a.id))).ToList();
        if (vistos.Count == 0) Linea($"<i>Aún no has visto ninguno de sus ataques.</i>", null, 23, true);
        foreach (FichaJefe.Ataque a in vistos)
        {
            string ka = Bitacora.ClaveAtaque(f, a.id), kc = Bitacora.ClaveConsejo(f, a.id);
            string etiqueta = a.instakill ? $"<color={Rojo}>Mortal</color>" : $"Fase {a.fase}";
            string consejo = Bitacora.Tiene(kc)
                ? $"<color={Verde}>Consejo:</color> {a.consejo}{MarcaNuevo(kc)}"
                : $"<color={Gris}>Consejo: ???</color>";
            Linea($"<b><color={Oro}>{a.nombre}</color></b>  <size=75%><color={Gris}>{etiqueta}</color></size>{MarcaNuevo(ka)}\n" +
                  $"<size=92%>{a.descripcion}</size>\n<size=88%>{consejo}</size>", null, 22, false);
        }
        int faltan = f.ataques.Count - vistos.Count;
        for (int i = 0; i < faltan; i++) Linea("???", null, 22, true);
    }

    private void Debilidades()
    {
        FichaJefe f = elegido;
        RecursosRPG r = RecursosRPG.Get();
        // Solo las fases alcanzadas (las demas no se muestran).
        int alcanzadas = 1;
        for (int i = 2; i <= 3; i++) if (Bitacora.Tiene(Bitacora.ClaveFase(f, i))) alcanzadas = i;
        bool alguno = false;
        foreach (Elemento e in Elementos.Todos)
        {
            string k = Bitacora.ClaveElemento(f, e);
            Sprite ico = r.Icono("elemento_" + (int)e);
            if (!Bitacora.Tiene(k)) { Linea($"<color={Gris}>???</color>", ico, 23, true); continue; }
            alguno = true;
            var partes = new List<string>();
            for (int fase = 1; fase <= alcanzadas; fase++)
            {
                FichaJefe.Afinidad a = f.afinidades.FirstOrDefault(x => x.elemento == e && x.Fase == fase) ?? f.afinidades.FirstOrDefault(x => x.elemento == e && x.Fase == 0);
                string reaccion = Reaccion(a != null ? a.multiplicador : 1f);
                partes.Add(alcanzadas > 1 ? $"<color={Gris}>Fase {fase}:</color> {reaccion}" : reaccion);
            }
            Linea($"<b>{Elementos.Nombre(e)}</b>{MarcaNuevo(k)}\n{string.Join("     ", partes)}", ico, 22, false);
        }
        // La nota general (si habla de fases, solo cuando ya se han visto todas).
        if (alguno && !string.IsNullOrEmpty(f.notaAfinidades) && (!f.notaAfinidades.Contains("Fase") || alcanzadas >= f.NumeroFases))
            Linea($"<color=#b8b0a6><i>{f.notaAfinidades}</i></color>", null, 21, false);
        if (!alguno) Linea("<i>Golpéalo con la espada imbuida para ver cómo reacciona a cada elemento.</i>", null, 21, true);
    }

    private static string Reaccion(float m)
    {
        if (m <= 0.01f) return $"<color={Rojo}>Inmune</color>";
        if (m > 1.01f) return $"<color={Verde}>Débil (x{m:0.0#})</color>";
        if (m < 0.99f) return $"<color={Rojo}>Resiste (x{m:0.0#})</color>";
        return "Neutral";
    }

    private void Fases()
    {
        FichaJefe f = elegido;
        bool alguna = false;
        foreach (FichaJefe.FaseInfo fi in f.fases.OrderBy(x => x.numero))
        {
            string k = Bitacora.ClaveFase(f, fi.numero);
            if (!Bitacora.Tiene(k)) continue;
            alguna = true;
            Linea($"<b><color={Oro}>Fase {fi.numero}{(string.IsNullOrEmpty(fi.nombre) ? "" : " — " + fi.nombre)}</color></b>{MarcaNuevo(k)}\n<size=92%>{fi.descripcion}</size>", null, 22, false);
        }
        if (!alguna) Linea("<i>Enfréntate a él para conocer su forma de luchar.</i>", null, 22, true);
    }

    private void Historia()
    {
        FichaJefe f = elegido;
        bool alguno = false;
        foreach (FichaJefe.Fragmento h in f.fragmentosHistoria)
        {
            string k = Bitacora.ClaveHistoria(f, h.id);
            if (!Bitacora.Tiene(k)) continue;
            alguno = true;
            Linea($"<i>{h.texto}</i>{MarcaNuevo(k)}", null, 23, false);
        }
        if (!alguno) Linea("<i>Nada se sabe aún de él.</i>", null, 23, true);
    }

    private void Estadisticas()
    {
        FichaJefe f = elegido;
        foreach (bool d in new[] { false, true })
        {
            Globales.Registro r = Globales.Desafio(f.id, d);
            if (d && r == null && !Desafio.DificilDesbloqueado(f)) continue;
            string titulo = d ? $"<color={Rojo}>Difícil</color>" : "Normal";
            if (r == null || (r.intentos == 0 && !r.completado)) { Linea($"<b>{titulo}</b>\n<color={Gris}>Sin intentos.</color>", null, 22, false); continue; }
            Linea($"<b>{titulo}</b>{(r.completado ? $"   <color={Verde}>Completado</color>" : "")}\n" +
                  $"Intentos  <b>{r.intentos}</b>        Muertes  <b>{r.muertes}</b>        Mejor tiempo  <b>{(r.completado ? Globales.Tiempo(r.mejorTiempo) : "—")}</b>",
                  null, 22, false);
        }
    }

    // Una linea de la ficha (con icono opcional a la izquierda).
    private void Linea(string texto, Sprite icono, float tamano, bool apagada)
    {
        GameObject fila = new GameObject("Linea", typeof(RectTransform));
        fila.transform.SetParent(contenido, false);
        HorizontalLayoutGroup h = fila.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 16f;
        h.childControlWidth = h.childControlHeight = true;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = false;
        h.childAlignment = TextAnchor.UpperLeft;
        if (icono != null)
        {
            Image ico = EstiloMenu.Caja("Icono", fila.transform, apagada ? new Color(1f, 1f, 1f, 0.35f) : Color.white);
            ico.sprite = icono;
            ico.preserveAspect = true;
            LayoutElement le = ico.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = le.minWidth = 44f;
            le.preferredHeight = le.minHeight = 44f;
        }
        TextMeshProUGUI t = EstiloMenu.Texto(texto, fila.transform, tamano, apagada ? EstiloMenu.TextoApagado : EstiloMenu.TextoElegido);
        t.alignment = TextAlignmentOptions.TopLeft;
        t.enableWordWrapping = true;
        t.lineSpacing = 2f;
        t.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
    }

    // ------------------------------------------------------------------ Lista y dificultad

    private void PintarFila(Fila f)
    {
        Globales.Registro n = Globales.Desafio(f.ficha.id, false), d = Globales.Desafio(f.ficha.id, true);
        string tiempos = "";
        if (n != null && n.completado) tiempos += $"Normal {Globales.Tiempo(n.mejorTiempo)}";
        if (d != null && d.completado) tiempos += (tiempos.Length > 0 ? "   ·   " : "") + $"Difícil {Globales.Tiempo(d.mejorTiempo)}";
        f.texto.text = $"{f.ficha.nombre}\n<size=68%><color={Gris}>{(tiempos.Length > 0 ? "Mejor: " + tiempos : "Sin completar")}</color></size>";
        f.marcaNormal.enabled = n != null && n.completado;
        f.marcaDificil.enabled = d != null && d.completado;
        f.nuevo.Poner(Bitacora.HayNuevo(f.ficha));
    }

    private void PintarDificultad()
    {
        bool libre = Desafio.DificilDesbloqueado(elegido);
        candadoDificil.enabled = !libre;
        botonDificil.GetComponent<OpcionEstilo>().PonerBloqueada(!libre);
        grupoDificultad.Marcar((dificil ? botonDificil : botonNormal).GetComponent<OpcionEstilo>());
        avisoDificultad.text = libre ? (dificil ? "Más vida, más daño y menos pausa entre ataques." : "Pulsa DESAFIAR para entrar a la arena.")
                                     : "Difícil: completa antes el desafío en Normal.";
    }

    private void ElegirDificultad(bool d)
    {
        if (d && !Desafio.DificilDesbloqueado(elegido))
        {
            SonidoMenu.Error();
            StartCoroutine(TransicionMenu.Pulso(avisoDificultad.rectTransform, 0.08f, 0.25f));
            return;
        }
        dificil = d;
        PintarDificultad();
    }

    private void PedirConfirmacion()
    {
        if (elegido == null) return;
        textoConfirmacion.text = $"Entrarás a la arena de <b>{elegido.nombre}</b> ({(dificil ? "Difícil" : "Normal")}).\n" +
                                 "<size=85%><color=#b8b0a6>Tu partida guardada no se verá afectada.</color></size>";
        confirmacion.SetActive(true);
        capas.Entrar(() => CerrarConfirmacion());
        StartCoroutine(TransicionMenu.Aparecer(confirmacion.GetComponent<CanvasGroup>(), 0.15f));
        SonidoMenu.Silenciar();
        EventSystem.current?.SetSelectedGameObject(botonEntrar.gameObject);
    }

    private void CerrarConfirmacion()
    {
        confirmacion.SetActive(false);
        SonidoMenu.Silenciar();
        EventSystem.current?.SetSelectedGameObject(botonDesafiar.gameObject);
    }

    // Cancelar con el boton (con Esc lo hace la capa).
    private void Cancelar()
    {
        capas.Atras(false);
    }

    private void Entrar()
    {
        confirmacion.SetActive(false);
        Desafio.Empezar(elegido, dificil);
    }

    // ------------------------------------------------------------------ Construccion

    private void Montar(RectTransform raiz)
    {
        Image velo = EstiloMenu.Caja("Velo", raiz, new Color(0f, 0f, 0f, 0.9f));
        velo.raycastTarget = true;
        EstiloMenu.Estirar(velo.rectTransform);
        RectTransform panel = EstiloMenu.Panel(raiz, "DESAFÍOS", new Vector2(1780f, 980f));
        RecursosRPG r = RecursosRPG.Get();

        // Izquierda: los jefes.
        RectTransform izq = Zona(panel, 0f, 0.3f, 56f, 20f);
        TextMeshProUGUI cab = EstiloMenu.Texto("JEFES", izq, 22, new Color(0.78f, 0.66f, 0.46f));
        cab.characterSpacing = 8f;
        cab.alignment = TextAlignmentOptions.Left;
        EstiloMenu.Arriba(cab.rectTransform, 0f, 30f);
        VerticalLayoutGroup col = EstiloMenu.Columna(izq, 0f, 0f, 46f, 70f, 10f);
        listaJefes = col.transform;
        foreach (FichaJefe f in FichaJefe.Visibles()) NuevaFila(f);

        // La frase del codigo secreto ("Algo desperto en el bosque..."), abajo.
        susurro = EstiloMenu.Texto("", panel, 24, new Color(0.7f, 0.85f, 0.72f));
        susurro.fontStyle = FontStyles.Italic;
        susurro.alpha = 0f;
        susurro.rectTransform.anchorMin = susurro.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        susurro.rectTransform.sizeDelta = new Vector2(900f, 36f);
        susurro.rectTransform.anchoredPosition = new Vector2(0f, 22f);
        VerticalLayoutGroup pie = EstiloMenu.Columna(izq, 0f, 0f, 0f, 0f, 0f);
        pie.childAlignment = TextAnchor.LowerCenter;
        botonVolver = EstiloMenu.Opcion(pie.transform, "Volver", Volver, 56f, 26f, null, TextAlignmentOptions.Left);
        botonVolver.GetComponent<OpcionEstilo>().animar = true;

        Image divisor = EstiloMenu.Caja("Divisor", panel, EstiloMenu.FiloTenue);
        divisor.rectTransform.anchorMin = new Vector2(0.305f, 0f);
        divisor.rectTransform.anchorMax = new Vector2(0.305f, 1f);
        divisor.rectTransform.sizeDelta = new Vector2(2f, 0f);
        divisor.rectTransform.offsetMin = new Vector2(-1f, 50f);
        divisor.rectTransform.offsetMax = new Vector2(1f, -130f);

        // Derecha: la ficha (o, sin jefe elegido, que hacer).
        RectTransform der = Zona(panel, 0.315f, 1f, 16f, 56f);
        placeholder = new GameObject("SinElegir", typeof(RectTransform));
        placeholder.transform.SetParent(der, false);
        EstiloMenu.Estirar((RectTransform)placeholder.transform);
        TextMeshProUGUI ph = EstiloMenu.Texto("Elige un jefe", placeholder.transform, 40, new Color(0.62f, 0.56f, 0.5f));
        ph.characterSpacing = 8f;
        ph.rectTransform.anchoredPosition = new Vector2(0f, 40f);
        ph.rectTransform.sizeDelta = new Vector2(900f, 60f);
        TextMeshProUGUI ph2 = EstiloMenu.Texto("Clic o Enter para ver su ficha.   Clic derecho o Esc para volver.", placeholder.transform, 22, EstiloMenu.TextoApagado);
        ph2.rectTransform.anchoredPosition = new Vector2(0f, -14f);
        ph2.rectTransform.sizeDelta = new Vector2(1000f, 40f);

        ficha = new GameObject("Ficha", typeof(RectTransform)).GetComponent<RectTransform>();
        ficha.SetParent(der, false);
        EstiloMenu.Estirar(ficha);
        grupoFicha = ficha.gameObject.AddComponent<CanvasGroup>();

        // Arriba: imagen grande y nombre.
        Image marco = EstiloMenu.Caja("Marco", ficha, EstiloMenu.FiloTenue);
        marcoImagen = marco.rectTransform;
        marcoImagen.anchorMin = marcoImagen.anchorMax = new Vector2(0f, 1f);
        marcoImagen.pivot = new Vector2(0f, 1f);
        marcoImagen.sizeDelta = new Vector2(220f, 220f);
        Image fondoImg = EstiloMenu.Caja("Fondo", marco.transform, new Color(0.03f, 0.03f, 0.04f, 1f));
        EstiloMenu.Estirar(fondoImg.rectTransform, 2f);
        fondoImg.gameObject.AddComponent<RectMask2D>();
        imagen = EstiloMenu.Caja("Jefe", fondoImg.transform, Color.white);
        imagen.preserveAspect = true;
        EstiloMenu.Estirar(imagen.rectTransform, 10f);
        imagen.rectTransform.pivot = new Vector2(0.5f, 0f);

        nombre = EstiloMenu.Texto("", ficha, 42, EstiloMenu.Titulo);
        nombre.fontStyle = FontStyles.Bold;
        nombre.characterSpacing = 6f;
        nombre.alignment = TextAlignmentOptions.Left;
        TextoArriba(nombre.rectTransform, 250f, 8f, 56f);
        subtitulo = EstiloMenu.Texto("", ficha, 24, EstiloMenu.TextoElegido);
        subtitulo.alignment = TextAlignmentOptions.Left;
        TextoArriba(subtitulo.rectTransform, 252f, 74f, 34f);
        progreso = EstiloMenu.Texto("", ficha, 22, EstiloMenu.TextoElegido);
        progreso.alignment = TextAlignmentOptions.TopLeft;
        progreso.enableWordWrapping = true;
        TextoArriba(progreso.rectTransform, 252f, 124f, 70f);

        // En medio: pestanas y contenido.
        RectTransform barra = new GameObject("Pestanas", typeof(RectTransform)).GetComponent<RectTransform>();
        barra.SetParent(ficha, false);
        barra.anchorMin = new Vector2(0f, 1f); barra.anchorMax = new Vector2(1f, 1f);
        barra.pivot = new Vector2(0.5f, 1f);
        barra.offsetMin = new Vector2(0f, -300f); barra.offsetMax = new Vector2(0f, -244f);
        HorizontalLayoutGroup hb = barra.gameObject.AddComponent<HorizontalLayoutGroup>();
        hb.spacing = 8f;
        hb.childControlWidth = hb.childControlHeight = true;
        hb.childForceExpandWidth = true;
        for (int i = 0; i < NombresSeccion.Length; i++)
        {
            Bitacora.Seccion s = (Bitacora.Seccion)i;
            Button b = EstiloMenu.Opcion(barra, NombresSeccion[i], () => MostrarSeccion(s), 56f, 22f, r.Icono(IconosSeccion[i]), TextAlignmentOptions.Left);
            Pestana p = new Pestana { seccion = s, boton = b, estilo = b.GetComponent<OpcionEstilo>(), texto = b.GetComponentInChildren<TextMeshProUGUI>() };
            p.texto.richText = true;
            p.nuevo = PuntoNuevo.Crear(b.transform, new Vector2(1f, 1f), new Vector2(-10f, -10f), 12f);
            grupoPestanas.Anadir(p.estilo);
            pestanas.Add(p);
        }
        Image lineaPestanas = EstiloMenu.Caja("Linea", ficha, EstiloMenu.FiloTenue);
        lineaPestanas.rectTransform.anchorMin = new Vector2(0f, 1f); lineaPestanas.rectTransform.anchorMax = new Vector2(1f, 1f);
        lineaPestanas.rectTransform.sizeDelta = new Vector2(0f, 1f);
        lineaPestanas.rectTransform.anchoredPosition = new Vector2(0f, -304f);

        RectTransform vista = new GameObject("Vista", typeof(RectTransform)).GetComponent<RectTransform>();
        vista.SetParent(ficha, false);
        vista.anchorMin = Vector2.zero; vista.anchorMax = Vector2.one;
        vista.offsetMin = new Vector2(0f, 150f); vista.offsetMax = new Vector2(0f, -318f);
        Image fondoVista = vista.gameObject.AddComponent<Image>();
        fondoVista.color = new Color(0f, 0f, 0f, 0.25f);
        vista.gameObject.AddComponent<RectMask2D>();
        scroll = vista.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;
        contenido = new GameObject("Contenido", typeof(RectTransform)).GetComponent<RectTransform>();
        contenido.SetParent(vista, false);
        contenido.anchorMin = new Vector2(0f, 1f); contenido.anchorMax = new Vector2(1f, 1f);
        contenido.pivot = new Vector2(0.5f, 1f);
        contenido.sizeDelta = Vector2.zero;
        VerticalLayoutGroup vc = contenido.gameObject.AddComponent<VerticalLayoutGroup>();
        vc.padding = new RectOffset(22, 22, 16, 16);
        vc.spacing = 16f;
        vc.childControlWidth = vc.childControlHeight = true;
        vc.childForceExpandWidth = true;
        vc.childForceExpandHeight = false;
        contenido.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = vista;
        scroll.content = contenido;

        // Abajo (siempre a la vista): dificultad y Desafiar.
        avisoDificultad = EstiloMenu.Texto("", ficha, 21, new Color(1f, 0.78f, 0.55f));
        avisoDificultad.alignment = TextAlignmentOptions.Left;
        RectTransform rv = avisoDificultad.rectTransform;
        rv.anchorMin = new Vector2(0f, 0f); rv.anchorMax = new Vector2(1f, 0f);
        rv.pivot = new Vector2(0f, 0f);
        rv.sizeDelta = new Vector2(0f, 30f);
        rv.anchoredPosition = new Vector2(0f, 94f);
        RectTransform abajo = new GameObject("Abajo", typeof(RectTransform)).GetComponent<RectTransform>();
        abajo.SetParent(ficha, false);
        abajo.anchorMin = new Vector2(0f, 0f); abajo.anchorMax = new Vector2(1f, 0f);
        abajo.pivot = new Vector2(0.5f, 0f);
        abajo.sizeDelta = new Vector2(0f, 72f);
        abajo.anchoredPosition = new Vector2(0f, 10f);
        HorizontalLayoutGroup h = abajo.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 16f;
        h.childControlWidth = h.childControlHeight = true;
        h.childForceExpandWidth = false;
        h.childAlignment = TextAnchor.LowerLeft;
        botonNormal = EstiloMenu.Opcion(abajo, "Normal", () => ElegirDificultad(false), 64f, 28f);
        botonDificil = EstiloMenu.Opcion(abajo, "Difícil", () => ElegirDificultad(true), 64f, 28f);
        botonDesafiar = EstiloMenu.Opcion(abajo, "DESAFIAR", PedirConfirmacion, 64f, 32f);
        botonNormal.GetComponent<LayoutElement>().preferredWidth = 220f;
        botonDificil.GetComponent<LayoutElement>().preferredWidth = 220f;
        botonDesafiar.GetComponent<LayoutElement>().preferredWidth = 380f;
        grupoDificultad.Anadir(botonNormal.GetComponent<OpcionEstilo>());
        grupoDificultad.Anadir(botonDificil.GetComponent<OpcionEstilo>());
        botonDesafiar.GetComponent<OpcionEstilo>().animar = true;
        botonDesafiar.GetComponentInChildren<TextMeshProUGUI>().characterSpacing = 10f;
        botonDesafiar.GetComponentInChildren<TextMeshProUGUI>().fontStyle = FontStyles.Bold;
        candadoDificil = EstiloMenu.Caja("Candado", botonDificil.transform, Color.white);
        candadoDificil.sprite = IconosDibujados.Candado();
        candadoDificil.preserveAspect = true;
        RectTransform rc = candadoDificil.rectTransform;
        rc.anchorMin = rc.anchorMax = new Vector2(0f, 0.5f);
        rc.pivot = new Vector2(0f, 0.5f);
        rc.sizeDelta = new Vector2(34f, 34f);
        rc.anchoredPosition = new Vector2(16f, 0f);

        ficha.gameObject.SetActive(false);
        MontarConfirmacion(raiz);
    }

    private Fila NuevaFila(FichaJefe f)
    {
        RecursosRPG r = RecursosRPG.Get();
        FichaJefe fj = f;
        Button b = EstiloMenu.Opcion(listaJefes, f.nombre, () => Seleccionar(fj), 104f, 28f, f.imagen, TextAlignmentOptions.Left);
        Fila fila = new Fila { ficha = f, boton = b, estilo = b.GetComponent<OpcionEstilo>(), texto = b.GetComponentInChildren<TextMeshProUGUI>() };
        fila.texto.richText = true;
        fila.texto.rectTransform.offsetMax = new Vector2(-100f, 0f);
        fila.marcaNormal = Marca(b.transform, r.Icono("marca_desafio"), -52f);
        fila.marcaDificil = Marca(b.transform, r.Icono("marca_dificil"), -6f);
        fila.nuevo = PuntoNuevo.Crear(b.transform, new Vector2(0f, 1f), new Vector2(22f, -16f));
        grupoJefes.Anadir(fila.estilo);
        filas.Add(fila);
        return fila;
    }

    // ------------------------------------------------------------------ Codigos secretos

    // Se escucha el teclado solo mientras esta pantalla esta abierta (al salir
    // se desactiva este objeto y lo escrito se olvida).
    private void OnEnable()
    {
        escrito = "";
        if (Keyboard.current != null) Keyboard.current.onTextInput += Letra;
    }

    private void OnDisable()
    {
        escrito = "";
        if (Keyboard.current != null) Keyboard.current.onTextInput -= Letra;
    }

    private void Letra(char c)
    {
        CodigosSecretos cs = CodigosSecretos.Get();
        if (cs == null || !cs.codigosActivos || !isActiveAndEnabled) return;
        // Con la confirmacion abierta no cuenta (y se olvida lo escrito).
        if (confirmacion != null && confirmacion.activeSelf) { escrito = ""; return; }
        if (Time.unscaledTime - ultimaLetra > cs.olvidarTras) escrito = "";
        ultimaLetra = Time.unscaledTime;
        string n = CodigosSecretos.Normalizar(c.ToString());
        if (n.Length == 0) return;
        escrito += n;
        int largo = Mathf.Max(1, cs.Largo);
        if (escrito.Length > largo) escrito = escrito.Substring(escrito.Length - largo);
        CodigosSecretos.Codigo codigo = cs.Buscar(escrito);
        if (codigo == null) return;
        escrito = "";
        Usar(codigo);
    }

    private void Usar(CodigosSecretos.Codigo codigo)
    {
        switch (codigo.accion)
        {
            case CodigosSecretos.Accion.DesbloquearJefeSecreto:
                FichaJefe f = FichaJefe.DeId(codigo.parametro);
                if (f == null) { Debug.LogWarning("[Codigos] No hay jefe con id " + codigo.parametro); return; }
                // Ya desbloqueado: solo un sonido discreto.
                if (f.Desbloqueado) { SonidoMenu.Confirmar(); return; }
                // Solo el desbloqueo: ni completa desafios, ni da logros, ni informacion del jefe.
                Globales.PonerMarca(CodigosSecretos.Marca(f.id), true);
                StartCoroutine(Despertar());
                break;
        }
    }

    // Como el desbloqueo normal: la frase con el sonido inquietante y, despues,
    // el jefe aparece en la lista con su destello.
    private IEnumerator Despertar()
    {
        Sonido.Reproducir("secreto_inquietante", 0.8f);
        susurro.text = "Algo despertó en el bosque...";
        for (float t = 0f; t < 1.4f; t += Time.unscaledDeltaTime) { susurro.alpha = Mathf.Clamp01(t / 1.4f) * 0.85f; yield return null; }
        foreach (FichaJefe f in FichaJefe.Visibles())
            if (!filas.Any(x => x.ficha == f)) PintarFila(NuevaFila(f));
        // En el orden de la lista.
        FichaJefe[] orden = FichaJefe.Visibles();
        foreach (Fila fl in filas) fl.boton.transform.SetSiblingIndex(System.Array.IndexOf(orden, fl.ficha));
        RevelarSecretos();
        yield return new WaitForSecondsRealtime(2.5f);
        for (float t = 0f; t < 1.2f; t += Time.unscaledDeltaTime) { susurro.alpha = 0.85f * (1f - t / 1.2f); yield return null; }
        susurro.alpha = 0f;
    }

    // Un jefe secreto recien desbloqueado aparece con un destello (solo la
    // primera vez; despues ya se queda en la lista).
    private void RevelarSecretos()
    {
        foreach (Fila f in filas)
            if (f.ficha.secreto && !Globales.Marca("revelado_" + f.ficha.id))
            {
                Globales.PonerMarca("revelado_" + f.ficha.id, true);
                StartCoroutine(Revelar(f));
            }
    }

    // Texto pegado arriba de la ficha, a la derecha de la imagen.
    private static void TextoArriba(RectTransform r, float x, float y, float alto)
    {
        r.anchorMin = new Vector2(0f, 1f); r.anchorMax = new Vector2(1f, 1f);
        r.pivot = new Vector2(0f, 1f);
        r.offsetMin = new Vector2(x, -y - alto);
        r.offsetMax = new Vector2(0f, -y);
    }

    private void MontarConfirmacion(RectTransform raiz)
    {
        confirmacion = new GameObject("Confirmacion", typeof(RectTransform));
        RectTransform rc = (RectTransform)confirmacion.transform;
        rc.SetParent(raiz, false);
        EstiloMenu.Estirar(rc);
        confirmacion.AddComponent<CanvasGroup>();
        Image velo = EstiloMenu.Caja("Velo", rc, new Color(0f, 0f, 0f, 0.7f));
        velo.raycastTarget = true;
        EstiloMenu.Estirar(velo.rectTransform);
        RectTransform p = EstiloMenu.Panel(rc, "DESAFIAR", new Vector2(980f, 440f));
        textoConfirmacion = EstiloMenu.Texto("", p, 28, EstiloMenu.TextoElegido);
        textoConfirmacion.enableWordWrapping = true;
        textoConfirmacion.lineSpacing = 10f;
        textoConfirmacion.rectTransform.anchoredPosition = new Vector2(0f, 20f);
        textoConfirmacion.rectTransform.sizeDelta = new Vector2(860f, 120f);
        VerticalLayoutGroup col = EstiloMenu.Columna(p, 200f, 200f, 290f, 20f, 4f);
        botonEntrar = EstiloMenu.Opcion(col.transform, "Entrar a la arena", Entrar, 56f, 28f);
        EstiloMenu.Opcion(col.transform, "Cancelar", Cancelar, 50f, 24f);
        confirmacion.SetActive(false);
    }

    private static RectTransform Zona(RectTransform panel, float desde, float hasta, float izq, float der)
    {
        RectTransform r = new GameObject("Zona", typeof(RectTransform)).GetComponent<RectTransform>();
        r.SetParent(panel, false);
        r.anchorMin = new Vector2(desde, 0f);
        r.anchorMax = new Vector2(hasta, 1f);
        r.offsetMin = new Vector2(izq, 40f);
        r.offsetMax = new Vector2(-der, -124f);
        return r;
    }

    private static Image Marca(Transform padre, Sprite s, float x)
    {
        Image i = EstiloMenu.Caja("Marca", padre, Color.white);
        i.sprite = s;
        i.preserveAspect = true;
        RectTransform r = i.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(1f, 0.5f);
        r.pivot = new Vector2(1f, 0.5f);
        r.sizeDelta = new Vector2(40f, 40f);
        r.anchoredPosition = new Vector2(x, 0f);
        i.enabled = false;
        return i;
    }
}
