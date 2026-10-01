using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Barra de vida grande del jefe, abajo y larga, con su nombre encima. Mas
// ancha y distinta a las barras pequenas de los enemigos comunes:
//   - Relleno carmesi y, detras, un rastro claro que baja con retraso (se ve
//     cuanto ha quitado el ultimo golpe).
//   - Una marca al 50%: ahi cambia de fase.
public class BarraJefe : MonoBehaviour
{
    private RectTransform relleno, rastro;
    private Image imagenRelleno, imagenBrillo;
    private TextMeshProUGUI textoNombre;
    private float llenando = -1f;
    private CanvasGroup grupo;
    private float objetivo = 1f, valorRastro = 1f, esperaRastro;
    private float alfaObjetivo;
    private RectTransform marco, lienzo;

    // Larga, desde la izquierda hasta la columna de la derecha (AjustesInterfaz).
    private void Colocar()
    {
        if (marco == null || lienzo == null) return;
        AjustesInterfaz.Get().Barra(lienzo.rect.width, out float x, out float ancho, out float altura);
        marco.sizeDelta = new Vector2(ancho, 22f);
        marco.anchoredPosition = new Vector2(x, altura);
    }

    private void LateUpdate() => Colocar();

    // Iconos de los estados del jefe (quemado, sangrado...), bajo la barra.
    public void PonerEstados(EnemyHealth salud)
    {
        if (salud == null || marco == null) return;
        salud.BarraExterna = true;
        FilaEstados.Crear(marco, salud, 34f, 5f, -8f, false);
    }

    public static BarraJefe Crear(string nombre, float marcaFase = 0.5f)
    {
        GameObject go = new GameObject("BarraJefe");
        Canvas c = go.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 55;
        CanvasScaler cs = go.AddComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920, 1080);
        cs.matchWidthOrHeight = 0.5f;

        BarraJefe b = go.AddComponent<BarraJefe>();
        b.grupo = go.AddComponent<CanvasGroup>();
        b.grupo.alpha = 0f;

        // Abajo, larga, hasta la columna de la derecha (AjustesInterfaz): se ajusta
        // cada fotograma al tamano real de la pantalla.
        RectTransform marco = b.Caja("Marco", go.transform, new Color(0.55f, 0.45f, 0.4f, 0.9f));
        marco.anchorMin = marco.anchorMax = new Vector2(0f, 0f);
        marco.pivot = new Vector2(0f, 0f);
        marco.sizeDelta = new Vector2(1000f, 22f);
        b.marco = marco;
        b.lienzo = (RectTransform)go.transform;
        b.Colocar();

        RectTransform fondo = b.Caja("Fondo", marco, new Color(0.05f, 0.02f, 0.03f, 1f));
        Estirar(fondo, 2f);
        b.rastro = b.Caja("Rastro", fondo, new Color(1f, 0.8f, 0.55f, 1f));
        Estirar(b.rastro, 0f);
        b.relleno = b.Caja("Relleno", fondo, new Color(0.62f, 0.03f, 0.08f, 1f));
        b.imagenRelleno = b.relleno.GetComponent<Image>();
        Estirar(b.relleno, 0f);

        // Brillo en la mitad de arriba del relleno, para que no sea plano.
        RectTransform brillo = b.Caja("Brillo", b.relleno, new Color(1f, 0.35f, 0.35f, 0.35f));
        b.imagenBrillo = brillo.GetComponent<Image>();
        brillo.anchorMin = new Vector2(0f, 0.55f);
        brillo.anchorMax = Vector2.one;
        brillo.offsetMin = brillo.offsetMax = Vector2.zero;

        if (marcaFase > 0f && marcaFase < 1f)
        {
            RectTransform marca = b.Caja("MarcaFase", fondo, new Color(0.9f, 0.85f, 0.7f, 0.8f));
            marca.anchorMin = new Vector2(marcaFase, 0f);
            marca.anchorMax = new Vector2(marcaFase, 1f);
            marca.sizeDelta = new Vector2(3f, 0f);
        }

        TextMeshProUGUI t = new GameObject("Nombre").AddComponent<TextMeshProUGUI>();
        t.transform.SetParent(marco, false);
        t.text = nombre;
        b.textoNombre = t;
        t.fontSize = 30;
        t.color = new Color(0.92f, 0.86f, 0.8f);
        t.alignment = TextAlignmentOptions.BottomLeft;
        t.outlineWidth = 0.2f;
        t.outlineColor = new Color32(0, 0, 0, 255);
        RectTransform rt = t.rectTransform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(0f, 44f);
        rt.anchoredPosition = new Vector2(0f, 6f);
        return b;
    }

    public void Mostrar(bool mostrar) => alfaObjetivo = mostrar ? 1f : 0f;

    // Para anadir cosas bajo la barra (segmentos de fase, escudo...).
    public RectTransform Marco => marco;

    // Segunda fase con barra propia: se rellena entera con otro color y el nombre
    // cambia. Mientras se rellena no hace caso de la vida.
    public void NuevaFase(Color color, string nombre)
    {
        imagenRelleno.color = color;
        imagenBrillo.color = new Color(Mathf.Min(1f, color.r + 0.4f), Mathf.Min(1f, color.g + 0.4f), Mathf.Min(1f, color.b + 0.4f), 0.35f);
        if (!string.IsNullOrEmpty(nombre)) textoNombre.text = nombre;
        llenando = 0f;
        objetivo = 1f;
    }

    public void Actualizar(int vida, int maxima)
    {
        if (llenando >= 0f) return;
        float nuevo = maxima > 0 ? Mathf.Clamp01((float)vida / maxima) : 0f;
        if (nuevo < objetivo) esperaRastro = 0.6f;
        objetivo = nuevo;
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        grupo.alpha = Mathf.MoveTowards(grupo.alpha, alfaObjetivo, dt * 1.5f);

        if (llenando >= 0f)
        {
            llenando = Mathf.MoveTowards(llenando, 1f, dt * 0.8f);
            Poner(relleno, llenando);
            Poner(rastro, llenando);
            valorRastro = llenando;
            if (llenando >= 1f) llenando = -1f;
            return;
        }
        Poner(relleno, objetivo);
        if (esperaRastro > 0f) esperaRastro -= dt;
        else valorRastro = Mathf.MoveTowards(valorRastro, objetivo, dt * 0.35f);
        if (valorRastro < objetivo) valorRastro = objetivo;
        Poner(rastro, valorRastro);
    }

    private static void Poner(RectTransform r, float u)
    {
        r.anchorMax = new Vector2(u, 1f);
    }

    private RectTransform Caja(string nombre, Transform padre, Color color)
    {
        Image i = new GameObject(nombre).AddComponent<Image>();
        i.transform.SetParent(padre, false);
        i.color = color;
        return i.rectTransform;
    }

    private static void Estirar(RectTransform r, float margen)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = new Vector2(margen, margen);
        r.offsetMax = new Vector2(-margen, -margen);
    }
}
