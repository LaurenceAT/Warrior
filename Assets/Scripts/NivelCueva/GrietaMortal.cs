using UnityEngine;

// Lo que se ve de una grieta mortal (la muerte la pone el DeadArea del fondo):
// un velo oscuro que tapa el fondo (se lee como un abismo, no como un suelo),
// niebla clara por encima y motas que suben. Todo va en la capa VFX, por encima
// de la oscuridad: una grieta nunca es invisible.
public class GrietaMortal : MonoBehaviour
{
    [Tooltip("Ancho y fondo de la grieta (unidades). La boca esta en la posicion del objeto.")]
    public float ancho = 2.3f;
    public float fondo = 5f;
    public Color colorNiebla = new Color(0.55f, 0.65f, 0.75f, 0.28f);
    [Tooltip("Color del velo que tapa el fondo (el abismo).")]
    public Color colorVacio = new Color(0.02f, 0.02f, 0.05f, 0.95f);
    [Tooltip("Motas por segundo que suben.")]
    public float motas = 3f;

    private SpriteRenderer[] nieblas;
    private float siguiente;
    private static Sprite degradado;

    private void Start()
    {
        // 0: el velo oscuro (del fondo hasta cerca de la boca). 1: la niebla clara.
        // El sprite mide 1 x 1 unidades: se estira al ancho y al alto.
        nieblas = new SpriteRenderer[2];
        for (int i = 0; i < 2; i++)
        {
            SpriteRenderer sr = Crear(i == 0 ? "Vacio" : "Niebla", Degradado(), 20 + i);
            float alto = fondo * (i == 0 ? 0.85f : 0.6f);
            sr.transform.localScale = new Vector3(ancho + 0.1f, alto, 1f);
            sr.transform.localPosition = new Vector3(0f, -fondo + alto * 0.5f, 0f);
            nieblas[i] = sr;
        }
    }

    private SpriteRenderer Crear(string n, Sprite s, int orden)
    {
        SpriteRenderer sr = new GameObject(n).AddComponent<SpriteRenderer>();
        sr.transform.SetParent(transform, false);
        sr.sprite = s;
        sr.sortingLayerName = "VFX";
        sr.sortingOrder = orden;
        sr.sharedMaterial = EfectoVisual.MaterialSinLuz();
        return sr;
    }

    private void Update()
    {
        float t = Time.time + transform.position.x;
        nieblas[0].color = colorVacio;
        Color c = colorNiebla;
        c.a *= 0.75f + 0.25f * Mathf.Sin(t * 1.1f);
        nieblas[1].color = c;
        if (motas > 0f && Time.time >= siguiente)
        {
            siguiente = Time.time + 1f / motas * Random.Range(0.6f, 1.4f);
            Vector2 p = (Vector2)transform.position + new Vector2(Random.Range(-ancho * 0.45f, ancho * 0.45f), -Random.Range(0.8f, fondo * 0.7f));
            EmisorCueva.Emitir(EmisorCueva.Tipo.Mota, p, new Vector2(Random.Range(-0.1f, 0.1f), Random.Range(0.5f, 0.9f)),
                               new Color(0.8f, 0.88f, 1f, 0.7f), Random.Range(0.03f, 0.05f), Random.Range(1.5f, 2.4f));
        }
    }

    // Degradado vertical: opaco abajo, transparente arriba (la niebla sube).
    private static Sprite Degradado()
    {
        if (degradado != null) return degradado;
        var tex = new Texture2D(32, 32, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < 32; y++)
        for (int x = 0; x < 32; x++)
        {
            // Opaca abajo, se desvanece hacia arriba y hacia los lados.
            float a = Mathf.Pow(1f - y / 31f, 1.6f) * Mathf.Clamp01(Mathf.Min(x, 31 - x) / 6f);
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        tex.Apply();
        // 1 x 1 unidades: se estira con la escala.
        degradado = Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 32f);
        degradado.name = "DegradadoNiebla";
        return degradado;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.4f);
        Gizmos.DrawWireCube(transform.position + Vector3.down * fondo * 0.5f, new Vector3(ancho, fondo, 0f));
    }
}
