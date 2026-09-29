using UnityEngine;

// Oscuridad de la fase 3: todo negro salvo un circulo de luz alrededor del
// player. Va por encima del escenario y de los cuerpos, pero por debajo de los
// efectos (capa VFX): los tajos blancos, las marcas de aviso y las ilusiones
// siguen brillando. La Cazadora lleva un contorno leve para no perderla.
public class OscuridadCazadora : MonoBehaviour
{
    [Tooltip("Radio del circulo de luz (unidades).")]
    public float radio = 4.2f;
    [Range(0f, 1f)] public float opacidad = 0.92f;
    [Tooltip("Rapidez del fundido al encenderse o apagarse.")]
    public float fundido = 0.8f;
    [Tooltip("Orden de dibujo dentro de la capa VFX (los efectos van por encima).")]
    public int orden = -20;

    private SpriteRenderer agujero;
    private readonly SpriteRenderer[] bordes = new SpriteRenderer[4];
    private float actual, objetivo;
    private bool pulso;
    private Transform player;

    // Borde suave del agujero: del 62 % al 95 % del radio de la textura.
    private const float BordeDentro = 0.62f, BordeFuera = 0.95f;

    public void Activar(bool on) => objetivo = on ? 1f : 0f;
    public void Pulsar(bool on) => pulso = on;
    public bool Encendida => actual > 0.02f;

    private void Awake()
    {
        agujero = Crear("Agujero");
        agujero.sprite = SpriteAgujero();
        for (int i = 0; i < 4; i++)
        {
            bordes[i] = Crear("Borde");
            bordes[i].sprite = DibujosCazadora.Pixel();
        }
        Pintar();
    }

    private SpriteRenderer Crear(string n)
    {
        GameObject go = new GameObject(n);
        go.transform.SetParent(transform, false);
        SpriteRenderer sr = DibujosCazadora.Renderer(go, "VFX", orden);
        sr.color = new Color(0f, 0f, 0f, 0f);
        return sr;
    }

    private void LateUpdate()
    {
        actual = Mathf.MoveTowards(actual, objetivo, Time.unscaledDeltaTime * fundido);
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }
        if (player != null) transform.position = new Vector3(player.position.x, player.position.y + 0.4f, 0f);
        Pintar();
    }

    private void Pintar()
    {
        bool ver = actual > 0.001f;
        float r = radio * (pulso ? 1f + 0.12f * Mathf.Sin(Time.time * 5f) : 1f);
        // El tamano del agujero para que el radio visible sea "r".
        float lado = 2f * r / ((BordeDentro + BordeFuera) * 0.5f);
        Color c = new Color(0f, 0f, 0f, opacidad * actual);
        agujero.enabled = ver;
        agujero.color = c;
        agujero.transform.localScale = new Vector3(lado, lado, 1f);
        // Cuatro rectangulos enormes alrededor del agujero.
        const float grande = 400f;
        float m = lado * 0.5f;
        Poner(bordes[0], new Vector2(0f, m + grande * 0.5f), new Vector2(grande * 2f + lado, grande), c, ver);
        Poner(bordes[1], new Vector2(0f, -m - grande * 0.5f), new Vector2(grande * 2f + lado, grande), c, ver);
        Poner(bordes[2], new Vector2(-m - grande * 0.5f, 0f), new Vector2(grande, lado), c, ver);
        Poner(bordes[3], new Vector2(m + grande * 0.5f, 0f), new Vector2(grande, lado), c, ver);
    }

    private static void Poner(SpriteRenderer sr, Vector2 pos, Vector2 tam, Color c, bool ver)
    {
        sr.enabled = ver;
        sr.color = c;
        sr.transform.localPosition = pos;
        sr.transform.localScale = new Vector3(tam.x, tam.y, 1f);
    }

    private static Sprite agujeroSprite;

    private static Sprite SpriteAgujero()
    {
        if (agujeroSprite != null) return agujeroSprite;
        const int n = 128;
        Texture2D t = new Texture2D(n, n, TextureFormat.RGBA32, false);
        t.filterMode = FilterMode.Bilinear;
        t.wrapMode = TextureWrapMode.Clamp;
        Color[] px = new Color[n * n];
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f)) / (n / 2f);
            float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(BordeDentro, BordeFuera, d));
            px[y * n + x] = new Color(1f, 1f, 1f, a);
        }
        t.SetPixels(px);
        t.Apply();
        agujeroSprite = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
        return agujeroSprite;
    }
}
