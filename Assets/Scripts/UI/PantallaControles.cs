using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Pantalla "Controles y guia": a la izquierda las categorias (se abren y se
// cierran) con sus acciones; a la derecha la tarjeta de la accion elegida.
// Una sola pantalla, reutilizada en el menu principal y en la pausa (partida
// normal y desafios). Raton: pasar resalta, clic elige. Teclado: flechas
// arriba/abajo y Enter (en una categoria la abre o la cierra). Volver atras lo
// hace quien la usa (Esc, Tab o clic derecho).
public class PantallaControles : MonoBehaviour
{
    private class Fila
    {
        public int categoria;
        public GuiaControles.Tarjeta tarjeta;   // null: cabecera de la categoria
        public Button boton;
        public OpcionEstilo estilo;
        public RectTransform flecha;
    }

    private GuiaControles guia;
    private TarjetaControl tarjeta;
    private ScrollRect lista;
    private readonly List<Fila> filas = new List<Fila>();
    private readonly GrupoMarcado marcadas = new GrupoMarcado();
    private bool[] abiertas;

    public TarjetaControl Tarjeta => tarjeta;
    public GuiaControles.Tarjeta Elegida => tarjeta != null ? tarjeta.Actual : null;
    public int Categorias => abiertas?.Length ?? 0;
    public bool Abierta(int c) => abiertas != null && c >= 0 && c < abiertas.Length && abiertas[c];
    public int FilasVisibles { get { int n = 0; foreach (Fila f in filas) if (f.boton.gameObject.activeSelf) n++; return n; } }
    public RectTransform Panel { get; private set; }

    public static PantallaControles Construir(Transform padre)
    {
        GuiaControles g = GuiaControles.Get();
        RectTransform raiz = new GameObject("ControlesYGuia", typeof(RectTransform)).GetComponent<RectTransform>();
        raiz.SetParent(padre, false);
        EstiloMenu.Estirar(raiz);
        PantallaControles p = raiz.gameObject.AddComponent<PantallaControles>();
        p.guia = g;

        RectTransform panel = EstiloMenu.Panel(raiz, g != null ? g.titulo : "CONTROLES Y GUÍA", new Vector2(1500f, 820f));
        p.Panel = (RectTransform)panel.parent;

        // Izquierda: la lista.
        RectTransform izq = new GameObject("Lista", typeof(RectTransform)).GetComponent<RectTransform>();
        izq.SetParent(panel, false);
        izq.anchorMin = new Vector2(0f, 0f); izq.anchorMax = new Vector2(0.33f, 1f);
        izq.offsetMin = new Vector2(30f, 30f); izq.offsetMax = new Vector2(-10f, -116f);
        p.lista = Desplazable(izq);

        Image divisor = EstiloMenu.Caja("Divisor", panel, EstiloMenu.FiloTenue);
        divisor.rectTransform.anchorMin = new Vector2(0.335f, 0f);
        divisor.rectTransform.anchorMax = new Vector2(0.335f, 1f);
        divisor.rectTransform.sizeDelta = new Vector2(2f, 0f);
        divisor.rectTransform.offsetMin = new Vector2(-1f, 30f);
        divisor.rectTransform.offsetMax = new Vector2(1f, -116f);

        // Derecha: la tarjeta.
        RectTransform der = new GameObject("Tarjeta", typeof(RectTransform)).GetComponent<RectTransform>();
        der.SetParent(panel, false);
        der.anchorMin = new Vector2(0.34f, 0f); der.anchorMax = new Vector2(1f, 1f);
        der.offsetMin = new Vector2(20f, 24f); der.offsetMax = new Vector2(-30f, -108f);
        p.tarjeta = TarjetaControl.Crear(der, 1f);

        if (g != null) p.CrearFilas();
        raiz.gameObject.SetActive(false);
        return p;
    }

    private static ScrollRect Desplazable(RectTransform zona)
    {
        zona.gameObject.AddComponent<RectMask2D>();
        ScrollRect sr = zona.gameObject.AddComponent<ScrollRect>();
        sr.horizontal = false;
        sr.movementType = ScrollRect.MovementType.Clamped;
        sr.scrollSensitivity = 40f;
        RectTransform contenido = new GameObject("Contenido", typeof(RectTransform)).GetComponent<RectTransform>();
        contenido.SetParent(zona, false);
        contenido.anchorMin = new Vector2(0f, 1f); contenido.anchorMax = new Vector2(1f, 1f);
        contenido.pivot = new Vector2(0.5f, 1f);
        contenido.sizeDelta = Vector2.zero;
        VerticalLayoutGroup v = contenido.gameObject.AddComponent<VerticalLayoutGroup>();
        v.spacing = 2f;
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandHeight = false;
        ContentSizeFitter f = contenido.gameObject.AddComponent<ContentSizeFitter>();
        f.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        sr.content = contenido;
        sr.viewport = zona;
        return sr;
    }

    private void CrearFilas()
    {
        abiertas = new bool[guia.categorias.Length];
        for (int c = 0; c < guia.categorias.Length; c++)
        {
            GuiaControles.Categoria cat = guia.categorias[c];
            int ci = c;
            Button cab = EstiloMenu.Opcion(lista.content, cat.nombre.ToUpperInvariant(), () => Alternar(ci), 54f, 26f, null, TextAlignmentOptions.Left);
            TextMeshProUGUI tc = cab.GetComponentInChildren<TextMeshProUGUI>();
            tc.characterSpacing = 6f;
            tc.fontStyle = FontStyles.Bold;
            tc.rectTransform.offsetMin = new Vector2(52f, 0f);
            Image fl = EstiloMenu.Caja("Flecha", cab.transform, EstiloMenu.Filo);
            fl.sprite = Triangulo();
            RectTransform rf = fl.rectTransform;
            rf.anchorMin = rf.anchorMax = new Vector2(0f, 0.5f);
            rf.sizeDelta = new Vector2(18f, 18f);
            rf.anchoredPosition = new Vector2(30f, 0f);
            filas.Add(new Fila { categoria = c, boton = cab, estilo = cab.GetComponent<OpcionEstilo>(), flecha = rf });

            foreach (GuiaControles.Tarjeta t in cat.tarjetas)
            {
                if (t == null) continue;
                GuiaControles.Tarjeta tt = t;
                Button b = EstiloMenu.Opcion(lista.content, t.titulo, null, 46f, 24f, null, TextAlignmentOptions.Left);
                b.GetComponentInChildren<TextMeshProUGUI>().rectTransform.offsetMin = new Vector2(70f, 0f);
                OpcionEstilo oe = b.GetComponent<OpcionEstilo>();
                marcadas.Anadir(oe);
                b.onClick.AddListener(() => Elegir(tt));
                filas.Add(new Fila { categoria = c, tarjeta = t, boton = b, estilo = oe });
            }
        }
    }

    // Al abrir: la primera categoria abierta, las demas cerradas, y elegida la primera accion.
    public void Abrir()
    {
        gameObject.SetActive(true);
        if (guia == null || abiertas == null) return;
        for (int c = 0; c < abiertas.Length; c++) abiertas[c] = c == 0;
        Aplicar();
        GuiaControles.Tarjeta primera = null;
        foreach (Fila f in filas) if (f.tarjeta != null) { primera = f.tarjeta; break; }
        Elegir(primera, false);
        lista.verticalNormalizedPosition = 1f;
        SonidoMenu.Silenciar();
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(Primero());
    }

    public void Cerrar() => gameObject.SetActive(false);

    // Lo que queda seleccionado al abrir: la accion elegida.
    public GameObject Primero()
    {
        foreach (Fila f in filas) if (f.tarjeta != null && f.tarjeta == Elegida) return f.boton.gameObject;
        return filas.Count > 0 ? filas[0].boton.gameObject : null;
    }

    public void Alternar(int c)
    {
        if (abiertas == null || c < 0 || c >= abiertas.Length) return;
        abiertas[c] = !abiertas[c];
        Aplicar();
    }

    public void Elegir(GuiaControles.Tarjeta t, bool sonido = true)
    {
        tarjeta.Mostrar(t);
        Fila f = filas.Find(x => x.tarjeta == t);
        marcadas.Marcar(f?.estilo);
        // La categoria de la accion elegida queda abierta.
        if (f != null && !abiertas[f.categoria]) { abiertas[f.categoria] = true; Aplicar(); }
    }

    // Muestra u oculta las acciones segun su categoria y gira las flechas.
    private void Aplicar()
    {
        foreach (Fila f in filas)
        {
            if (f.tarjeta == null) f.flecha.localRotation = Quaternion.Euler(0f, 0f, abiertas[f.categoria] ? 0f : 90f);
            else f.boton.gameObject.SetActive(abiertas[f.categoria]);
        }
        // Lo seleccionado puede haberse ocultado: vuelve a su cabecera.
        EventSystem es = EventSystem.current;
        if (es != null && es.currentSelectedGameObject != null && !es.currentSelectedGameObject.activeInHierarchy)
        {
            Fila oculta = filas.Find(x => x.boton.gameObject == es.currentSelectedGameObject);
            Fila cab = oculta != null ? filas.Find(x => x.tarjeta == null && x.categoria == oculta.categoria) : null;
            es.SetSelectedGameObject(cab != null ? cab.boton.gameObject : Primero());
        }
        LayoutRebuilder.ForceRebuildLayoutImmediate(lista.content);
    }

    private void Update()
    {
        // La lista sigue a lo seleccionado con el teclado.
        EventSystem es = EventSystem.current;
        if (es == null || es.currentSelectedGameObject == null) return;
        RectTransform sel = es.currentSelectedGameObject.transform as RectTransform;
        if (sel == null || sel.parent != lista.content) return;
        RectTransform vista = lista.viewport;
        float alto = lista.content.rect.height - vista.rect.height;
        if (alto <= 0f) return;
        float arriba = -sel.anchoredPosition.y - sel.rect.height * sel.pivot.y;
        float abajo = arriba + sel.rect.height;
        float y = lista.content.anchoredPosition.y;
        if (arriba < y) y = arriba;
        else if (abajo > y + vista.rect.height) y = abajo - vista.rect.height;
        if (!Mathf.Approximately(y, lista.content.anchoredPosition.y))
            lista.content.anchoredPosition = new Vector2(lista.content.anchoredPosition.x, Mathf.Clamp(y, 0f, alto));
    }

    // Triangulo que apunta hacia abajo (categoria abierta); girado, cerrada.
    private static Sprite triangulo;
    private static Sprite Triangulo()
    {
        if (triangulo != null) return triangulo;
        const int n = 32;
        Texture2D t = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n, v = (y + 0.5f) / n;
                // Base arriba (v alto), punta abajo.
                float ancho = Mathf.Clamp01((v - 0.15f) / 0.7f) * 0.5f;
                bool dentro = v > 0.15f && v < 0.85f && Mathf.Abs(u - 0.5f) <= ancho;
                t.SetPixel(x, y, dentro ? Color.white : Color.clear);
            }
        t.Apply();
        triangulo = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
        return triangulo;
    }

    // Para las pruebas.
    public IEnumerable<GuiaControles.Tarjeta> TarjetasEnLista()
    {
        foreach (Fila f in filas) if (f.tarjeta != null) yield return f.tarjeta;
    }
}
