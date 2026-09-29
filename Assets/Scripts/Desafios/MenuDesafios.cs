using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Pantalla de Desafios del menu principal. A la izquierda los jefes (con la
// marca de completado, la de Dificil y el mejor tiempo); a la derecha la ficha
// del elegido: imagen, debilidades y resistencias, informacion, historia, la
// dificultad y el boton de Desafiar (con confirmacion). No necesita partida.
public class MenuDesafios : MonoBehaviour
{
    private class Fila
    {
        public FichaJefe ficha;
        public Button boton;
        public TextMeshProUGUI texto;
        public Image marcaNormal, marcaDificil;
    }

    private readonly List<Fila> filas = new List<Fila>();
    private FichaJefe elegido;
    private bool dificil;

    private Image imagen;
    private TextMeshProUGUI nombre, afinidades, info, historia, avisoDificultad;
    private RectTransform iconosAfinidad;
    private Button botonNormal, botonDificil, botonDesafiar, botonVolver;
    private Image candadoDificil;
    private GameObject confirmacion;
    private TextMeshProUGUI textoConfirmacion;
    private Button botonEntrar;
    private System.Action volverAlMenu;

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

    // Al entrar en la pantalla: todo al dia y el primer jefe elegido.
    public void Abrir()
    {
        confirmacion.SetActive(false);
        foreach (Fila f in filas) PintarFila(f);
        Elegir(elegido != null ? elegido : filas.FirstOrDefault()?.ficha);
        // Un jefe secreto recien desbloqueado aparece con un destello (solo la
        // primera vez; despues ya se queda en la lista).
        foreach (Fila f in filas)
            if (f.ficha.secreto && !Globales.Marca("revelado_" + f.ficha.id))
            {
                Globales.PonerMarca("revelado_" + f.ficha.id, true);
                StartCoroutine(Revelar(f));
            }
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

    public GameObject Primero() => filas.FirstOrDefault(f => f.ficha == elegido)?.boton.gameObject ?? botonVolver.gameObject;

    // Esc: si esta la confirmacion abierta, la cierra (y no sale de la pantalla).
    public bool Atras()
    {
        if (!confirmacion.activeSelf) return false;
        CerrarConfirmacion();
        SonidoMenu.Cancelar();
        return true;
    }

    private void Update()
    {
        if (confirmacion.activeSelf) return;
        GameObject sel = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        Fila f = filas.FirstOrDefault(x => x.boton.gameObject == sel);
        if (f != null && f.ficha != elegido) Elegir(f.ficha);
    }

    // ------------------------------------------------------------------ Logica

    private void Elegir(FichaJefe f)
    {
        elegido = f;
        if (f == null) return;
        imagen.sprite = f.imagen;
        imagen.enabled = f.imagen != null;
        nombre.text = f.nombre.ToUpper();
        info.text = f.informacion;
        historia.text = $"<i>{f.historia}</i>";
        MostrarAfinidades(f);
        if (!Desafio.DificilDesbloqueado(f)) dificil = false;
        PintarDificultad();
    }

    private void MostrarAfinidades(FichaJefe f)
    {
        for (int i = iconosAfinidad.childCount - 1; i >= 0; i--) Destroy(iconosAfinidad.GetChild(i).gameObject);
        RecursosRPG r = RecursosRPG.Get();
        foreach (FichaJefe.Afinidad a in f.afinidades)
        {
            bool debil = a.multiplicador > 1f;
            GameObject celda = new GameObject("Afinidad", typeof(RectTransform));
            celda.transform.SetParent(iconosAfinidad, false);
            LayoutElement le = celda.AddComponent<LayoutElement>();
            le.preferredWidth = 210f; le.preferredHeight = 58f;
            Image ico = EstiloMenu.Caja("Icono", celda.transform, Color.white);
            ico.sprite = r.Icono("elemento_" + (int)a.elemento);
            ico.preserveAspect = true;
            RectTransform ri = ico.rectTransform;
            ri.anchorMin = ri.anchorMax = new Vector2(0f, 0.5f);
            ri.pivot = new Vector2(0f, 0.5f);
            ri.sizeDelta = new Vector2(48f, 48f);
            TextMeshProUGUI t = EstiloMenu.Texto($"<b>x{a.multiplicador:0.0#}</b>\n<size=75%>{a.cuando}</size>", celda.transform, 22,
                                                 debil ? new Color(0.62f, 0.9f, 0.6f) : new Color(0.92f, 0.5f, 0.45f));
            t.alignment = TextAlignmentOptions.Left;
            t.lineSpacing = -10f;
            RectTransform rt = t.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(56f, 0f); rt.offsetMax = Vector2.zero;
        }
        string debiles = string.Join(", ", f.afinidades.Where(a => a.multiplicador > 1f).Select(a => Elementos.Nombre(a.elemento)).Distinct());
        string fuertes = string.Join(", ", f.afinidades.Where(a => a.multiplicador < 1f).Select(a => Elementos.Nombre(a.elemento)).Distinct());
        afinidades.text = $"<color=#9fe0a0>Débil a:</color> {(debiles.Length > 0 ? debiles : "nada")}     " +
                          $"<color=#e0776c>Resiste:</color> {(fuertes.Length > 0 ? fuertes : "nada")}" +
                          (string.IsNullOrEmpty(f.notaAfinidades) ? "" : $"\n<size=85%><color=#b8b0a6>{f.notaAfinidades}</color></size>");
    }

    private void PintarFila(Fila f)
    {
        Globales.Registro n = Globales.Desafio(f.ficha.id, false), d = Globales.Desafio(f.ficha.id, true);
        string tiempos = "";
        if (n != null && n.completado) tiempos += $"Normal {Globales.Tiempo(n.mejorTiempo)}";
        if (d != null && d.completado) tiempos += (tiempos.Length > 0 ? "   ·   " : "") + $"Difícil {Globales.Tiempo(d.mejorTiempo)}";
        f.texto.text = $"{f.ficha.nombre}\n<size=70%><color=#8f8780>{(tiempos.Length > 0 ? "Mejor: " + tiempos : "Sin completar")}</color></size>";
        f.marcaNormal.enabled = n != null && n.completado;
        f.marcaDificil.enabled = d != null && d.completado;
    }

    private void PintarDificultad()
    {
        bool libre = Desafio.DificilDesbloqueado(elegido);
        candadoDificil.enabled = !libre;
        botonDificil.GetComponent<OpcionEstilo>().PonerActiva(libre);
        botonNormal.GetComponentInChildren<TextMeshProUGUI>().text = (!dificil ? "» " : "") + "Normal";
        botonDificil.GetComponentInChildren<TextMeshProUGUI>().text = (dificil ? "» " : "") + "Difícil";
        avisoDificultad.text = libre ? (dificil ? "Más vida, más daño y menos pausa entre ataques." : "")
                                     : "Completa el desafío normal para desbloquearla";
    }

    private void ElegirDificultad(bool d)
    {
        if (d && !Desafio.DificilDesbloqueado(elegido)) { SonidoMenu.Error(); PintarDificultad(); return; }
        dificil = d;
        PintarDificultad();
    }

    private void PedirConfirmacion()
    {
        if (elegido == null) return;
        textoConfirmacion.text = $"Entrarás a la arena de <b>{elegido.nombre}</b> ({(dificil ? "Difícil" : "Normal")}).\n" +
                                 "<size=85%><color=#b8b0a6>Tu partida guardada no se verá afectada.</color></size>";
        confirmacion.SetActive(true);
        SonidoMenu.Silenciar();
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(botonEntrar.gameObject);
    }

    private void CerrarConfirmacion()
    {
        confirmacion.SetActive(false);
        SonidoMenu.Silenciar();
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(botonDesafiar.gameObject);
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

        // Izquierda: los jefes.
        RectTransform izq = Zona(panel, 0f, 0.3f, 60f, 20f);
        TextMeshProUGUI cab = EstiloMenu.Texto("JEFES", izq, 22, new Color(0.78f, 0.66f, 0.46f));
        cab.characterSpacing = 8f;
        cab.alignment = TextAlignmentOptions.Left;
        EstiloMenu.Arriba(cab.rectTransform, 0f, 30f);
        VerticalLayoutGroup col = EstiloMenu.Columna(izq, 0f, 0f, 46f, 0f, 8f);
        RecursosRPG r = RecursosRPG.Get();
        foreach (FichaJefe f in FichaJefe.Visibles())
        {
            FichaJefe ficha = f;
            Button b = EstiloMenu.Opcion(col.transform, f.nombre, () => { Elegir(ficha); SonidoMenu.Silenciar(); EventSystem.current?.SetSelectedGameObject(botonDesafiar.gameObject); },
                                         96f, 28f, null, TextAlignmentOptions.Left);
            Fila fila = new Fila { ficha = f, boton = b, texto = b.GetComponentInChildren<TextMeshProUGUI>() };
            fila.texto.richText = true;
            fila.texto.rectTransform.offsetMax = new Vector2(-104f, 0f);
            fila.marcaNormal = Marca(b.transform, r.Icono("marca_desafio"), -56f);
            fila.marcaDificil = Marca(b.transform, r.Icono("marca_dificil"), -8f);
            filas.Add(fila);
        }
        botonVolver = EstiloMenu.Opcion(col.transform, "Volver", () => volverAlMenu?.Invoke(), 56f, 26f, null, TextAlignmentOptions.Left);

        Image divisor = EstiloMenu.Caja("Divisor", panel, EstiloMenu.FiloTenue);
        divisor.rectTransform.anchorMin = new Vector2(0.31f, 0f);
        divisor.rectTransform.anchorMax = new Vector2(0.31f, 1f);
        divisor.rectTransform.sizeDelta = new Vector2(2f, 0f);
        divisor.rectTransform.offsetMin = new Vector2(-1f, 50f);
        divisor.rectTransform.offsetMax = new Vector2(1f, -130f);

        // Derecha: la ficha.
        RectTransform der = Zona(panel, 0.32f, 1f, 20f, 60f);
        Image marco = EstiloMenu.Caja("Marco", der, EstiloMenu.FiloTenue);
        marco.rectTransform.anchorMin = marco.rectTransform.anchorMax = new Vector2(0f, 1f);
        marco.rectTransform.pivot = new Vector2(0f, 1f);
        marco.rectTransform.sizeDelta = new Vector2(280f, 280f);
        Image fondoImg = EstiloMenu.Caja("Fondo", marco.transform, new Color(0.03f, 0.03f, 0.04f, 1f));
        EstiloMenu.Estirar(fondoImg.rectTransform, 2f);
        imagen = EstiloMenu.Caja("Jefe", fondoImg.transform, Color.white);
        imagen.preserveAspect = true;
        EstiloMenu.Estirar(imagen.rectTransform, 10f);

        nombre = EstiloMenu.Texto("", der, 40, EstiloMenu.Titulo);
        nombre.fontStyle = FontStyles.Bold;
        nombre.characterSpacing = 6f;
        nombre.alignment = TextAlignmentOptions.Left;
        EstiloMenu.Arriba(nombre.rectTransform, 0f, 52f);
        nombre.rectTransform.offsetMin = new Vector2(310f, nombre.rectTransform.offsetMin.y);

        iconosAfinidad = new GameObject("Afinidades", typeof(RectTransform)).GetComponent<RectTransform>();
        iconosAfinidad.SetParent(der, false);
        iconosAfinidad.anchorMin = new Vector2(0f, 1f); iconosAfinidad.anchorMax = new Vector2(1f, 1f);
        iconosAfinidad.pivot = new Vector2(0f, 1f);
        iconosAfinidad.offsetMin = new Vector2(310f, -190f); iconosAfinidad.offsetMax = new Vector2(0f, -66f);
        GridLayoutGroup g = iconosAfinidad.gameObject.AddComponent<GridLayoutGroup>();
        g.cellSize = new Vector2(210f, 58f);
        g.spacing = new Vector2(8f, 6f);

        afinidades = EstiloMenu.Texto("", der, 22, EstiloMenu.TextoElegido);
        afinidades.alignment = TextAlignmentOptions.TopLeft;
        afinidades.enableWordWrapping = true;
        RectTransform ra = afinidades.rectTransform;
        ra.anchorMin = new Vector2(0f, 1f); ra.anchorMax = new Vector2(1f, 1f);
        ra.pivot = new Vector2(0f, 1f);
        ra.offsetMin = new Vector2(310f, -290f); ra.offsetMax = new Vector2(0f, -198f);

        // Informacion e historia, en dos columnas.
        info = Columna(der, "INFORMACIÓN", 0f, 0.5f);
        historia = Columna(der, "HISTORIA", 0.52f, 1f);

        // Abajo: dificultad y Desafiar.
        RectTransform abajo = new GameObject("Abajo", typeof(RectTransform)).GetComponent<RectTransform>();
        abajo.SetParent(der, false);
        abajo.anchorMin = new Vector2(0f, 0f); abajo.anchorMax = new Vector2(1f, 0f);
        abajo.pivot = new Vector2(0.5f, 0f);
        abajo.sizeDelta = new Vector2(0f, 120f);
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
        botonDesafiar.GetComponent<LayoutElement>().preferredWidth = 360f;
        candadoDificil = EstiloMenu.Caja("Candado", botonDificil.transform, Color.white);
        candadoDificil.sprite = IconosDibujados.Candado();
        RectTransform rc = candadoDificil.rectTransform;
        rc.anchorMin = rc.anchorMax = new Vector2(0f, 0.5f);
        rc.pivot = new Vector2(0f, 0.5f);
        rc.sizeDelta = new Vector2(34f, 34f);
        rc.anchoredPosition = new Vector2(16f, 0f);
        avisoDificultad = EstiloMenu.Texto("", der, 21, new Color(1f, 0.78f, 0.55f));
        avisoDificultad.alignment = TextAlignmentOptions.Left;
        RectTransform rv = avisoDificultad.rectTransform;
        rv.anchorMin = new Vector2(0f, 0f); rv.anchorMax = new Vector2(1f, 0f);
        rv.pivot = new Vector2(0.5f, 0f);
        rv.sizeDelta = new Vector2(0f, 30f);
        rv.anchoredPosition = new Vector2(0f, 76f);

        MontarConfirmacion(raiz);
    }

    private TextMeshProUGUI Columna(RectTransform padre, string titulo, float desde, float hasta)
    {
        RectTransform z = new GameObject(titulo, typeof(RectTransform)).GetComponent<RectTransform>();
        z.SetParent(padre, false);
        z.anchorMin = new Vector2(desde, 0f); z.anchorMax = new Vector2(hasta, 1f);
        z.offsetMin = new Vector2(0f, 120f); z.offsetMax = new Vector2(0f, -305f);
        TextMeshProUGUI cab = EstiloMenu.Texto(titulo, z, 20, new Color(0.78f, 0.66f, 0.46f));
        cab.characterSpacing = 8f;
        cab.alignment = TextAlignmentOptions.Left;
        EstiloMenu.Arriba(cab.rectTransform, 0f, 28f);
        TextMeshProUGUI t = EstiloMenu.Texto("", z, 21, EstiloMenu.TextoElegido);
        t.alignment = TextAlignmentOptions.TopLeft;
        t.enableWordWrapping = true;
        t.overflowMode = TextOverflowModes.Ellipsis;
        t.lineSpacing = 4f;
        RectTransform rt = t.rectTransform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = new Vector2(0f, -36f);
        return t;
    }

    private void MontarConfirmacion(RectTransform raiz)
    {
        confirmacion = new GameObject("Confirmacion", typeof(RectTransform));
        RectTransform rc = (RectTransform)confirmacion.transform;
        rc.SetParent(raiz, false);
        EstiloMenu.Estirar(rc);
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
        EstiloMenu.Opcion(col.transform, "Cancelar", CerrarConfirmacion, 50f, 24f);
        confirmacion.SetActive(false);
    }

    private static RectTransform Zona(RectTransform panel, float desde, float hasta, float izq, float der)
    {
        RectTransform r = new GameObject("Zona", typeof(RectTransform)).GetComponent<RectTransform>();
        r.SetParent(panel, false);
        r.anchorMin = new Vector2(desde, 0f);
        r.anchorMax = new Vector2(hasta, 1f);
        r.offsetMin = new Vector2(izq, 50f);
        r.offsetMax = new Vector2(-der, -130f);
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
        r.sizeDelta = new Vector2(42f, 42f);
        r.anchoredPosition = new Vector2(x, 0f);
        i.enabled = false;
        return i;
    }
}
