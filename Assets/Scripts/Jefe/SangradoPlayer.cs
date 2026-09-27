using UnityEngine;

// Sangrado, al estilo Souls: los golpes de sangre del jefe llenan una barra sobre
// la cabeza del player. Si se llena, estalla: quita vida de golpe y se vacia. Si
// se deja de recibir, se va vaciando sola. Descansar en la hoguera la limpia.
//
// Se anade solo al player la primera vez que le afecta (SangradoPlayer.Aplicar).
public class SangradoPlayer : MonoBehaviour
{
    // Lo pone el jefe: el efecto del estallido.
    public static AnimadorHoja.Clip efectoEstallido;

    [SerializeField] private float maximo = 100f;
    [SerializeField] private int danoEstallido = 20;
    [SerializeField] private float esperaAntesDeBajar = 1.5f;
    [SerializeField] private float bajadaPorSegundo = 14f;
    [SerializeField] private Color colorBarra = new Color(0.85f, 0.05f, 0.1f, 1f);

    private float acumulado;
    private float ultimaSubida;
    private PlayerControler player;
    private Transform barra;
    private SpriteRenderer fondo, relleno;

    public static void Aplicar(PlayerControler p, float cantidad)
    {
        if (p == null || cantidad <= 0f) return;
        SangradoPlayer s = p.GetComponent<SangradoPlayer>();
        if (s == null) s = p.gameObject.AddComponent<SangradoPlayer>();
        s.Sumar(cantidad);
    }

    private void Awake()
    {
        player = GetComponent<PlayerControler>();
        CrearBarra();
    }

    private void OnEnable() { Hoguera.AlDescansar += Limpiar; }
    private void OnDisable() { Hoguera.AlDescansar -= Limpiar; }

    private void Limpiar() { acumulado = 0f; }

    private void Sumar(float cantidad)
    {
        acumulado += cantidad;
        ultimaSubida = Time.time;
        if (acumulado >= maximo) Estallar();
    }

    private void Estallar()
    {
        acumulado = 0f;
        ScreenFlash.Destello(new Color(0.8f, 0f, 0.05f, 0.35f), 0.25f);
        if (efectoEstallido != null)
            EfectoVisual.Crear(efectoEstallido, (Vector2)transform.position + Vector2.up * 0.2f, 1.1f, Color.white);
        TextoFlotante.Mostrar("¡Sangrado!", (Vector2)transform.position + Vector2.up * 1.3f, colorBarra, 1.1f);
        if (player != null) player.TakeDamage(danoEstallido);
    }

    private void Update()
    {
        if (acumulado > 0f && Time.time - ultimaSubida > esperaAntesDeBajar)
            acumulado = Mathf.Max(0f, acumulado - bajadaPorSegundo * Time.deltaTime);

        bool visible = acumulado > 0.5f;
        if (barra.gameObject.activeSelf != visible) barra.gameObject.SetActive(visible);
        if (!visible) return;

        // El player se voltea con la escala: la barra se endereza para no leerse al reves.
        Vector3 e = barra.localScale;
        e.x = Mathf.Sign(transform.lossyScale.x) * Mathf.Abs(e.x);
        barra.localScale = e;

        float u = acumulado / maximo;
        relleno.transform.localScale = new Vector3(u, 1f, 1f);
        relleno.transform.localPosition = new Vector3(-0.5f + u * 0.5f, 0f, 0f);
        // Late mas rapido cuanto mas cerca esta de estallar.
        Color c = colorBarra;
        c.a = u > 0.7f ? 0.6f + 0.4f * Mathf.Sin(Time.time * 25f) : 1f;
        relleno.color = c;
    }

    private void CrearBarra()
    {
        barra = new GameObject("BarraSangrado").transform;
        barra.SetParent(transform, false);
        barra.localPosition = new Vector3(0f, 1.15f, 0f);
        barra.localScale = new Vector3(1.2f, 0.13f, 1f);

        fondo = Cuadro("Fondo", new Color(0f, 0f, 0f, 0.7f), 40);
        fondo.transform.localScale = new Vector3(1.08f, 1.6f, 1f);
        relleno = Cuadro("Relleno", colorBarra, 41);
        barra.gameObject.SetActive(false);
    }

    private SpriteRenderer Cuadro(string nombre, Color color, int orden)
    {
        SpriteRenderer sr = new GameObject(nombre).AddComponent<SpriteRenderer>();
        sr.transform.SetParent(barra, false);
        sr.sprite = Blanco();
        sr.color = color;
        sr.sortingLayerName = "VFX";
        sr.sortingOrder = orden;
        sr.sharedMaterial = EfectoVisual.MaterialSinLuz();
        return sr;
    }

    private static Sprite blanco;
    public static Sprite Blanco()
    {
        if (blanco != null) return blanco;
        Texture2D t = new Texture2D(4, 4);
        Color[] c = new Color[16];
        for (int i = 0; i < 16; i++) c[i] = Color.white;
        t.SetPixels(c);
        t.Apply();
        blanco = Sprite.Create(t, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);
        return blanco;
    }
}
