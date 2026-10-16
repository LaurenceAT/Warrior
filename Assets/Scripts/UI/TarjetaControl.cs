using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Tarjeta de "Controles y guia": titulo grande, imagen (fija o con 2-3 cuadros
// en bucle) y explicacion con las teclas dibujadas. La misma en la pantalla de
// Controles (grande) y en la guia del primer desafio (pequena). Se anima con el
// tiempo real: funciona con el juego en pausa.
public class TarjetaControl : MonoBehaviour
{
    private GuiaControles guia;
    private GuiaControles.Tarjeta actual;
    private TextMeshProUGUI titulo, texto;
    private RectTransform zonaImagen;
    private Image imagen, icono, anillo;
    private readonly Image[] ranuras = new Image[5];
    private readonly Image[] cajasTecla = new Image[4];
    private readonly TextMeshProUGUI[] textosTecla = new TextMeshProUGUI[4];
    private RectTransform rueda, teclas;
    private int cuadro = -1;

    public GuiaControles.Tarjeta Actual => actual;
    public string TextoMostrado => texto != null ? texto.text : "";
    public TextMeshProUGUI CampoTitulo => titulo;
    public TextMeshProUGUI CampoTexto => texto;
    public RectTransform ZonaImagen => zonaImagen;

    // "padre" debe tener el tamano de la tarjeta; "escala" ajusta letras y margenes.
    public static TarjetaControl Crear(RectTransform padre, float escala = 1f)
    {
        GuiaControles g = GuiaControles.Get();
        TarjetaControl t = padre.gameObject.AddComponent<TarjetaControl>();
        t.guia = g;

        t.titulo = EstiloMenu.Texto("", padre, 44f * escala, EstiloMenu.Titulo);
        t.titulo.fontStyle = TMPro.FontStyles.Bold;
        t.titulo.characterSpacing = 6f;
        t.titulo.alignment = TextAlignmentOptions.Left;
        t.titulo.enableWordWrapping = false;
        t.titulo.overflowMode = TextOverflowModes.Ellipsis;
        RectTransform rt = t.titulo.rectTransform;
        rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(-40f * escala, 60f * escala);
        rt.anchoredPosition = new Vector2(0f, -14f * escala);

        // Imagen: marco oscuro con filo, entre el titulo y el texto.
        Image marco = EstiloMenu.Caja("Imagen", padre, EstiloMenu.FiloTenue);
        t.zonaImagen = marco.rectTransform;
        t.zonaImagen.anchorMin = new Vector2(0f, 0.38f); t.zonaImagen.anchorMax = new Vector2(1f, 1f);
        t.zonaImagen.offsetMin = new Vector2(20f * escala, 0f);
        t.zonaImagen.offsetMax = new Vector2(-20f * escala, -84f * escala);
        Image fondo = EstiloMenu.Caja("Fondo", marco.transform, new Color(0.02f, 0.018f, 0.02f, 1f));
        EstiloMenu.Estirar(fondo.rectTransform, 2f);

        t.imagen = EstiloMenu.Caja("Cuadro", fondo.transform, Color.white);
        t.imagen.preserveAspect = true;
        EstiloMenu.Estirar(t.imagen.rectTransform, 14f * escala);
        t.icono = EstiloMenu.Caja("Icono", fondo.transform, Color.white);
        t.icono.preserveAspect = true;
        RectTransform ri = t.icono.rectTransform;
        ri.anchorMin = ri.anchorMax = new Vector2(1f, 0f);
        ri.pivot = new Vector2(1f, 0f);
        ri.sizeDelta = new Vector2(70f, 70f) * escala;
        ri.anchoredPosition = new Vector2(-14f, 14f) * escala;

        t.CrearRueda(fondo.rectTransform, escala);
        t.CrearAnillo(fondo.rectTransform, escala);
        t.CrearTeclas(fondo.rectTransform, escala);

        t.texto = EstiloMenu.Texto("", padre, 27f * escala, EstiloMenu.TextoNormal);
        t.texto.alignment = TextAlignmentOptions.TopLeft;
        t.texto.enableWordWrapping = true;
        t.texto.lineSpacing = 18f;
        t.texto.overflowMode = TextOverflowModes.Overflow;
        RectTransform rx = t.texto.rectTransform;
        rx.anchorMin = new Vector2(0f, 0f); rx.anchorMax = new Vector2(1f, 0.38f);
        rx.offsetMin = new Vector2(24f * escala, 14f * escala);
        rx.offsetMax = new Vector2(-24f * escala, -22f * escala);
        return t;
    }

    // Rueda pequena con las mismas piezas que la rueda de imbuir real.
    private void CrearRueda(RectTransform fondo, float escala)
    {
        rueda = new GameObject("Rueda", typeof(RectTransform)).GetComponent<RectTransform>();
        rueda.SetParent(fondo, false);
        rueda.anchorMin = rueda.anchorMax = new Vector2(0.5f, 0.5f);
        rueda.sizeDelta = new Vector2(220f, 220f) * escala;
        Image aro = EstiloMenu.Caja("Aro", rueda, new Color(0.72f, 0.58f, 0.36f, 0.35f));
        aro.sprite = RuedaImbuir.Anillo();
        EstiloMenu.Estirar(aro.rectTransform);
        RecursosRPG r = RecursosRPG.Get();
        for (int i = 0; i < ranuras.Length; i++)
        {
            float ang = Mathf.PI * 0.5f - i * Mathf.PI * 2f / ranuras.Length;
            Image f = EstiloMenu.Caja("Ranura" + i, rueda, new Color(0.1f, 0.09f, 0.1f, 1f));
            f.sprite = RuedaImbuir.Circulo();
            f.rectTransform.sizeDelta = new Vector2(66f, 66f) * escala;
            f.rectTransform.anchoredPosition = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * 86f * escala;
            Image ic = EstiloMenu.Caja("Icono", f.transform, Color.white);
            ic.sprite = r != null ? r.Icono("elemento_" + i) : null;
            ic.preserveAspect = true;
            EstiloMenu.Estirar(ic.rectTransform, 12f * escala);
            ranuras[i] = f;
        }
        rueda.gameObject.SetActive(false);
    }

    // Anillo de imbuicion del HUD, vaciandose.
    private void CrearAnillo(RectTransform fondo, float escala)
    {
        anillo = EstiloMenu.Caja("Anillo", fondo, new Color(1f, 0.78f, 0.42f, 1f));
        anillo.sprite = RuedaImbuir.Anillo();
        anillo.type = Image.Type.Filled;
        anillo.fillMethod = Image.FillMethod.Radial360;
        anillo.fillOrigin = 2;
        anillo.rectTransform.anchorMin = anillo.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        anillo.rectTransform.sizeDelta = new Vector2(170f, 170f) * escala;
        Image centro = EstiloMenu.Caja("Elemento", anillo.transform, Color.white);
        RecursosRPG r = RecursosRPG.Get();
        centro.sprite = r != null ? r.Icono("elemento_0") : null;
        centro.preserveAspect = true;
        EstiloMenu.Estirar(centro.rectTransform, 46f * escala);
        anillo.gameObject.SetActive(false);
    }

    // Teclas dibujadas por codigo (la de la pausa).
    private void CrearTeclas(RectTransform fondo, float escala)
    {
        teclas = new GameObject("Teclas", typeof(RectTransform)).GetComponent<RectTransform>();
        teclas.SetParent(fondo, false);
        teclas.anchorMin = teclas.anchorMax = new Vector2(0.5f, 0.5f);
        teclas.sizeDelta = new Vector2(420f, 120f) * escala;
        HorizontalLayoutGroup h = teclas.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 40f * escala;
        h.childAlignment = TextAnchor.MiddleCenter;
        h.childControlWidth = h.childControlHeight = false;
        h.childForceExpandWidth = h.childForceExpandHeight = false;
        for (int i = 0; i < cajasTecla.Length; i++)
        {
            Image c = EstiloMenu.Caja("Tecla" + i, teclas, EstiloMenu.Filo);
            c.rectTransform.sizeDelta = new Vector2(130f, 96f) * escala;
            Image cara = EstiloMenu.Caja("Cara", c.transform, new Color(0.16f, 0.14f, 0.13f, 1f));
            EstiloMenu.Estirar(cara.rectTransform, 3f * escala);
            cara.rectTransform.offsetMin = new Vector2(3f, 9f) * escala;
            TextMeshProUGUI tx = EstiloMenu.Texto("", cara.transform, 34f * escala, EstiloMenu.TextoElegido);
            tx.fontStyle = TMPro.FontStyles.Bold;
            EstiloMenu.Estirar(tx.rectTransform);
            cajasTecla[i] = c;
            textosTecla[i] = tx;
        }
        teclas.gameObject.SetActive(false);
    }

    public void Mostrar(GuiaControles.Tarjeta t)
    {
        actual = t;
        cuadro = -1;
        if (t == null) { titulo.text = texto.text = ""; return; }
        titulo.text = t.titulo;
        texto.text = guia.Resolver(t.texto);
        imagen.gameObject.SetActive(t.tipo == GuiaControles.TipoImagen.Cuadros);
        rueda.gameObject.SetActive(t.tipo == GuiaControles.TipoImagen.Rueda);
        anillo.gameObject.SetActive(t.tipo == GuiaControles.TipoImagen.Anillo);
        teclas.gameObject.SetActive(t.tipo == GuiaControles.TipoImagen.Teclas);
        icono.sprite = t.icono;
        icono.gameObject.SetActive(t.icono != null && t.tipo == GuiaControles.TipoImagen.Cuadros);
        for (int i = 0; i < cajasTecla.Length; i++)
        {
            bool hay = t.teclas != null && i < t.teclas.Length;
            cajasTecla[i].gameObject.SetActive(hay);
            if (hay) textosTecla[i].text = t.teclas[i];
        }
        Actualizar();
    }

    // Cuantos cuadros tiene la imagen de esta tarjeta (1 = fija).
    public static int Cuadros(GuiaControles.Tarjeta t)
    {
        if (t == null) return 0;
        switch (t.tipo)
        {
            case GuiaControles.TipoImagen.Rueda: case GuiaControles.TipoImagen.Anillo: return 3;
            case GuiaControles.TipoImagen.Teclas: return Mathf.Max(1, t.teclas?.Length ?? 0);
            default: return t.cuadros?.Length ?? 0;
        }
    }

    private void Update() => Actualizar();

    private void Actualizar()
    {
        if (actual == null) return;
        int n = Mathf.Max(1, Cuadros(actual));
        int c = Mathf.FloorToInt(Time.unscaledTime * Mathf.Max(0.1f, guia.cuadrosPorSegundo)) % n;
        if (c == cuadro) return;
        cuadro = c;
        switch (actual.tipo)
        {
            case GuiaControles.TipoImagen.Cuadros:
                GuiaControles.Cuadro q = actual.cuadros != null && actual.cuadros.Length > 0 ? actual.cuadros[c % actual.cuadros.Length] : null;
                imagen.sprite = q?.sprite;
                imagen.color = q != null ? q.color : Color.white;
                imagen.enabled = imagen.sprite != null;
                break;
            case GuiaControles.TipoImagen.Rueda:
                for (int i = 0; i < ranuras.Length; i++)
                {
                    bool elegida = i == c;
                    ranuras[i].color = elegida ? new Color(0.45f, 0.34f, 0.18f, 1f) : new Color(0.1f, 0.09f, 0.1f, 1f);
                    ranuras[i].rectTransform.localScale = Vector3.one * (elegida ? 1.15f : 1f);
                }
                break;
            case GuiaControles.TipoImagen.Anillo:
                anillo.fillAmount = 1f - c / 3f;
                break;
            case GuiaControles.TipoImagen.Teclas:
                for (int i = 0; i < cajasTecla.Length; i++)
                    cajasTecla[i].color = i == c ? new Color(1f, 0.82f, 0.45f, 1f) : EstiloMenu.FiloTenue;
                break;
        }
    }
}
