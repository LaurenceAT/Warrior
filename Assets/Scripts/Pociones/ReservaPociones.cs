using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Los frascos, al estilo del Estus de los Souls: pocas cargas, que se recargan al
// descansar en la hoguera (y al reaparecer tras morir). Hay dos:
//   - Frasco de sangre (Q): varias cargas; cura vida.
//   - Frasco de mana (R): una sola carga; el mana solo se recupera con el.
// Lo que cura cada uno sube con las mejoras de la hoguera (Equipo: Lagrimas
// sagradas). Vive en la escena (no en el player), asi las cargas se mantienen
// aunque el player muera y reaparezca.
//
// No hay sprite de pocion: el frasco del contador se dibuja por codigo.
public class ReservaPociones : MonoBehaviour
{
    // Cada vez que se bebe (el reto del jefe mira esto).
    public static event Action AlBeber;
    public static event Action<int, int> AlCambiar;
    // Cualquier cambio (cargas de las dos, o la elegida).
    public static event Action AlCambiarAlgo;

    public enum Tipo { Vida = 0, Mana = 1 }

    [SerializeField] private int maximo = 3;
    [SerializeField] private int curacion = 40;

    [Header("Frasco de mana")]
    [Tooltip("Cargas del frasco de mana (una, como el de los Souls).")]
    [SerializeField] private int maximoMana = 1;

    private int cargas;
    private int cargasMana;
    private Tipo elegida = Tipo.Vida;
    private bool recompensaCogida;
    private static ReservaPociones instancia;

    public int Cargas => cargas;
    public int Maximo => maximo + Equipo.FrascosSangreExtra;
    public int Curacion => curacion;
    public float FraccionCuracion => Equipo.CuraFrasco;
    public bool RecompensaCogida => recompensaCogida;
    public int CargasMana => cargasMana;
    public int MaximoMana => maximoMana + Equipo.FrascosManaExtra;
    public float FraccionMana => Equipo.ManaFrasco;
    public Tipo Elegida => elegida;
    public int CargasDe(Tipo t) => t == Tipo.Vida ? cargas : cargasMana;
    public int MaximoDe(Tipo t) => t == Tipo.Vida ? Maximo : MaximoMana;

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
        // El frasco extra del camino secreto ya cogido (las cargas extra de cada
        // frasco estan en Equipo).
        recompensaCogida = Partida.Bandera("frasco_extra");
        cargas = Maximo;
        cargasMana = MaximoMana;
        maximoAntes = Maximo;
        maximoManaAntes = MaximoMana;
        ContadorPociones.Crear(this);
    }

    // Se rellenan al descansar en la hoguera y al reaparecer tras morir.
    private void OnEnable() { Hoguera.AlDescansar += Rellenar; GameManager.AlReaparecerPlayer += Rellenar; Equipo.AlCambiar += CambioEquipo; }
    private void OnDisable() { Hoguera.AlDescansar -= Rellenar; GameManager.AlReaparecerPlayer -= Rellenar; Equipo.AlCambiar -= CambioEquipo; }

    private int maximoAntes = -1, maximoManaAntes = -1;

    // Un frasco nuevo (recogido o comprado) llega lleno: suma una carga al momento.
    private void CambioEquipo()
    {
        if (maximoAntes >= 0 && Maximo > maximoAntes) cargas += Maximo - maximoAntes;
        if (maximoManaAntes >= 0 && MaximoMana > maximoManaAntes) cargasMana += MaximoMana - maximoManaAntes;
        cargas = Mathf.Min(cargas, Maximo);
        cargasMana = Mathf.Min(cargasMana, MaximoMana);
        maximoAntes = Maximo;
        maximoManaAntes = MaximoMana;
        AlCambiar?.Invoke(cargas, Maximo);
        AlCambiarAlgo?.Invoke();
    }
    private void OnDestroy() { if (instancia == this) instancia = null; }

    public bool Gastar() => Gastar(Tipo.Vida);

    public bool Gastar(Tipo t)
    {
        if (CargasDe(t) <= 0) return false;
        if (t == Tipo.Vida) cargas--; else cargasMana--;
        AlCambiar?.Invoke(cargas, Maximo);
        AlCambiarAlgo?.Invoke();
        AlBeber?.Invoke();
        return true;
    }

    // Un trago cortado antes de hacer efecto no gasta la carga.
    public void Devolver(Tipo t)
    {
        if (t == Tipo.Vida) cargas = Mathf.Min(Maximo, cargas + 1);
        else cargasMana = Mathf.Min(MaximoMana, cargasMana + 1);
        AlCambiar?.Invoke(cargas, Maximo);
        AlCambiarAlgo?.Invoke();
    }

    public void Elegir(Tipo t)
    {
        elegida = t;
        AlCambiarAlgo?.Invoke();
    }

    public void Rellenar()
    {
        cargas = Maximo;
        cargasMana = MaximoMana;
        AlCambiar?.Invoke(cargas, Maximo);
        AlCambiarAlgo?.Invoke();
    }

    // La recompensa del camino secreto: una carga mas para siempre (y llena).
    // Solo una por partida (la marca "frasco_extra").
    public void AumentarMaximo()
    {
        recompensaCogida = true;
        Partida.PonerBandera("frasco_extra");
        Equipo.Sumar(Equipo.Objeto.FrascoSangre);
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

// Contador de frascos en pantalla, abajo a la izquierda: el frasco de sangre (Q) y el de
// mana (R) con sus cargas.
public class ContadorPociones : MonoBehaviour
{
    private class Hueco
    {
        public RectTransform caja;
        public Image icono;
        public TextMeshProUGUI texto;
        public float pulso;
    }

    private readonly Hueco[] huecos = new Hueco[2];
    private readonly int[] cargasAntes = new int[2];
    private ReservaPociones reserva;

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
        cp.reserva = r;

        RecursosRPG rec = RecursosRPG.Get();
        Sprite vida = rec.Icono("pocion");
        cp.huecos[0] = cp.NuevoHueco(go.transform, "Vida", vida != null ? vida : ReservaPociones.Frasco(), new Vector2(44f, 60f));
        cp.huecos[1] = cp.NuevoHueco(go.transform, "Mana", rec.Icono("pocion_mana"), new Vector2(250f, 60f));

        ReservaPociones.AlCambiarAlgo += cp.Actualizar;
        ReservaPociones.AlBeber += cp.Pulso;
        cp.Actualizar();
    }

    private Hueco NuevoHueco(Transform padre, string nombre, Sprite icono, Vector2 pos)
    {
        Hueco h = new Hueco();
        h.caja = new GameObject(nombre).AddComponent<RectTransform>();
        h.caja.SetParent(padre, false);
        // Abajo a la izquierda, como los objetos rapidos de los Souls (arriba, bajo
        // las barras, van los estados que se acumulan).
        h.caja.anchorMin = h.caja.anchorMax = new Vector2(0f, 0f);
        h.caja.pivot = new Vector2(0f, 0f);
        h.caja.anchoredPosition = pos;
        h.caja.sizeDelta = new Vector2(200f, 70f);

        h.icono = new GameObject("Frasco").AddComponent<Image>();
        h.icono.transform.SetParent(h.caja, false);
        h.icono.sprite = icono;
        h.icono.enabled = icono != null;
        h.icono.preserveAspect = true;
        RectTransform ri = h.icono.rectTransform;
        ri.anchorMin = ri.anchorMax = new Vector2(0f, 0.5f);
        ri.pivot = new Vector2(0f, 0.5f);
        ri.sizeDelta = new Vector2(48f, 60f);

        h.texto = new GameObject("Cargas").AddComponent<TextMeshProUGUI>();
        h.texto.transform.SetParent(h.caja, false);
        h.texto.fontSize = 34;
        h.texto.fontStyle = FontStyles.Bold;
        h.texto.alignment = TextAlignmentOptions.MidlineLeft;
        h.texto.outlineWidth = 0.2f;
        h.texto.outlineColor = new Color32(0, 0, 0, 255);
        RectTransform rt = h.texto.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(56f, 0f);
        rt.sizeDelta = new Vector2(150f, 50f);
        return h;
    }

    private void OnDestroy()
    {
        ReservaPociones.AlCambiarAlgo -= Actualizar;
        ReservaPociones.AlBeber -= Pulso;
    }

    private void Actualizar()
    {
        if (reserva == null) return;
        for (int i = 0; i < 2; i++)
        {
            ReservaPociones.Tipo t = (ReservaPociones.Tipo)i;
            Hueco h = huecos[i];
            const bool elegida = true;
            string tecla = t == ReservaPociones.Tipo.Vida ? "Q" : "R";
            int cargas = reserva.CargasDe(t);
            bool quedan = cargas > 0;
            h.texto.text = $"{cargas}/{reserva.MaximoDe(t)} <size=60%><color=#bbbbbb>[{tecla}]</color></size>";
            float a = elegida ? 1f : 0.55f;
            h.icono.color = quedan ? new Color(1f, 1f, 1f, a) : new Color(0.4f, 0.4f, 0.4f, 0.8f * a);
            Color tc = quedan ? (t == ReservaPociones.Tipo.Vida ? new Color(1f, 0.92f, 0.85f) : new Color(0.8f, 0.9f, 1f)) : new Color(0.6f, 0.6f, 0.6f);
            h.texto.color = new Color(tc.r, tc.g, tc.b, a);
            h.caja.localScale = Vector3.one * (elegida ? 1f : 0.85f);
            if (cargas < cargasAntes[i]) h.pulso = 1f;
            cargasAntes[i] = cargas;
        }
    }

    private void Pulso()
    {
        // El latido lo pone Actualizar en el frasco que ha bajado.
    }

    private void Update()
    {
        if (reserva == null) return;
        for (int i = 0; i < 2; i++)
        {
            Hueco h = huecos[i];
            if (h.pulso <= 0f) continue;
            h.pulso = Mathf.Max(0f, h.pulso - Time.unscaledDeltaTime * 3f);
            const float baseEscala = 1f;
            h.caja.localScale = Vector3.one * baseEscala * (1f + 0.2f * h.pulso);
        }
    }
}
