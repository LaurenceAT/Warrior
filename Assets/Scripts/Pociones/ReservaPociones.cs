using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Las pociones de curacion, al estilo del Estus de los Souls: pocas cargas, que se
// recargan al descansar en la hoguera. Vive en la escena (no en el player), asi
// las cargas se mantienen aunque el player muera y reaparezca.
//
// No hay sprite de pocion: el frasco del contador se dibuja por codigo.
public class ReservaPociones : MonoBehaviour
{
    // Cada vez que se bebe (el reto del jefe mira esto).
    public static event Action AlBeber;
    public static event Action<int, int> AlCambiar;

    [SerializeField] private int maximo = 3;
    [SerializeField] private int curacion = 40;

    private int cargas;
    private bool recompensaCogida;
    private static ReservaPociones instancia;

    public int Cargas => cargas;
    public int Maximo => maximo;
    public int Curacion => curacion;
    public bool RecompensaCogida => recompensaCogida;

    public static ReservaPociones Get()
    {
        if (instancia != null) return instancia;
        instancia = FindFirstObjectByType<ReservaPociones>();
        if (instancia == null) instancia = new GameObject("ReservaPociones").AddComponent<ReservaPociones>();
        return instancia;
    }

    private void Awake()
    {
        if (instancia != null && instancia != this) { Destroy(gameObject); return; }
        instancia = this;
        cargas = maximo;
        ContadorPociones.Crear(this);
    }

    // Se rellenan al descansar en la hoguera y al reaparecer tras morir.
    private void OnEnable() { Hoguera.AlDescansar += Rellenar; GameManager.AlReaparecerPlayer += Rellenar; }
    private void OnDisable() { Hoguera.AlDescansar -= Rellenar; GameManager.AlReaparecerPlayer -= Rellenar; }
    private void OnDestroy() { if (instancia == this) instancia = null; }

    public bool Gastar()
    {
        if (cargas <= 0) return false;
        cargas--;
        AlCambiar?.Invoke(cargas, maximo);
        AlBeber?.Invoke();
        return true;
    }

    public void Rellenar()
    {
        cargas = maximo;
        AlCambiar?.Invoke(cargas, maximo);
    }

    // La recompensa del camino secreto: una carga mas para siempre (y llena).
    public void AumentarMaximo()
    {
        maximo++;
        cargas++;
        recompensaCogida = true;
        AlCambiar?.Invoke(cargas, maximo);
    }

    // Frasco de pixel art dibujado a mano en codigo (16x20): cristal, liquido rojo
    // y tapon. Sirve de icono generico mientras no haya sprite de pocion.
    private static Sprite frasco;
    public static Sprite Frasco()
    {
        if (frasco != null) return frasco;
        const int w = 16, h = 20;
        Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        Color vacio = new Color(0, 0, 0, 0);
        Color borde = new Color(0.12f, 0.07f, 0.08f, 1f);
        Color cristal = new Color(0.75f, 0.82f, 0.95f, 0.55f);
        Color liquido = new Color(0.82f, 0.08f, 0.14f, 1f);
        Color brillo = new Color(1f, 0.65f, 0.65f, 1f);
        Color tapon = new Color(0.55f, 0.35f, 0.2f, 1f);
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            Color c = vacio;
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(8f, 7f));
            if (y >= 17 && x >= 5 && x <= 10) c = (x == 5 || x == 10 || y == 19) ? borde : tapon;
            else if (y >= 12 && y <= 16 && x >= 6 && x <= 9) c = (x == 6 || x == 9) ? borde : cristal;
            else if (d <= 6.5f) c = d > 5.6f ? borde : (y <= 8 ? liquido : cristal);
            if (c == liquido && x == 5 && (y == 6 || y == 7)) c = brillo;
            t.SetPixel(x, y, c);
        }
        t.Apply();
        frasco = Sprite.Create(t, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 16f);
        return frasco;
    }
}

// Contador de pociones en pantalla, bajo la vida: el frasco y "x3".
public class ContadorPociones : MonoBehaviour
{
    private Image icono;
    private TextMeshProUGUI texto;
    private RectTransform caja;
    private float pulso;

    public static void Crear(ReservaPociones r)
    {
        GameObject go = new GameObject("ContadorPociones");
        go.transform.SetParent(r.transform, false);
        Canvas c = go.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 40;
        CanvasScaler cs = go.AddComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920, 1080);
        cs.matchWidthOrHeight = 0.5f;
        ContadorPociones cp = go.AddComponent<ContadorPociones>();

        cp.caja = new GameObject("Caja").AddComponent<RectTransform>();
        cp.caja.SetParent(go.transform, false);
        cp.caja.anchorMin = cp.caja.anchorMax = new Vector2(0f, 1f);
        cp.caja.pivot = new Vector2(0f, 1f);
        cp.caja.anchoredPosition = new Vector2(44f, -168f);
        cp.caja.sizeDelta = new Vector2(200f, 70f);

        cp.icono = new GameObject("Frasco").AddComponent<Image>();
        cp.icono.transform.SetParent(cp.caja, false);
        cp.icono.sprite = ReservaPociones.Frasco();
        RectTransform ri = cp.icono.rectTransform;
        ri.anchorMin = ri.anchorMax = new Vector2(0f, 0.5f);
        ri.pivot = new Vector2(0f, 0.5f);
        ri.sizeDelta = new Vector2(48f, 60f);

        cp.texto = new GameObject("Cargas").AddComponent<TextMeshProUGUI>();
        cp.texto.transform.SetParent(cp.caja, false);
        cp.texto.fontSize = 34;
        cp.texto.fontStyle = FontStyles.Bold;
        cp.texto.alignment = TextAlignmentOptions.MidlineLeft;
        cp.texto.outlineWidth = 0.2f;
        cp.texto.outlineColor = new Color32(0, 0, 0, 255);
        RectTransform rt = cp.texto.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(56f, 0f);
        rt.sizeDelta = new Vector2(150f, 50f);

        ReservaPociones.AlCambiar += cp.Actualizar;
        ReservaPociones.AlBeber += cp.Pulso;
        cp.Actualizar(r.Cargas, r.Maximo);
    }

    private void OnDestroy()
    {
        ReservaPociones.AlCambiar -= Actualizar;
        ReservaPociones.AlBeber -= Pulso;
    }

    private void Actualizar(int cargas, int maximo)
    {
        texto.text = $"x{cargas} <size=60%><color=#bbbbbb>[Q]</color></size>";
        bool quedan = cargas > 0;
        icono.color = quedan ? Color.white : new Color(0.4f, 0.4f, 0.4f, 0.8f);
        texto.color = quedan ? new Color(1f, 0.92f, 0.85f) : new Color(0.6f, 0.6f, 0.6f);
        pulso = 1f;
    }

    private void Pulso() { pulso = 1f; }

    private void Update()
    {
        if (pulso <= 0f) return;
        pulso = Mathf.Max(0f, pulso - Time.unscaledDeltaTime * 3f);
        caja.localScale = Vector3.one * (1f + 0.2f * pulso);
    }
}
