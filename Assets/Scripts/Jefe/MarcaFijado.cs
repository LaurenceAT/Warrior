using UnityEngine;

// La marca del objetivo mientras la camara de combate esta fijada (clic central):
// un rombo fino que late sobre el jefe. Si el jefe se sale de la pantalla, la
// marca se queda pegada al borde y se convierte en una flecha que apunta hacia
// el, para no perderlo nunca de vista. Se dibuja por codigo (sin sprites).
public class MarcaFijado : MonoBehaviour
{
    private const float Ppu = 28f;
    private const int Lado = 24;
    private static Sprite rombo, flecha;

    private SpriteRenderer sr;
    private float nacido;

    public bool Fuera { get; private set; }

    public static MarcaFijado Crear()
    {
        GameObject go = new GameObject("MarcaFijado");
        DontDestroyOnLoad(go);
        MarcaFijado m = go.AddComponent<MarcaFijado>();
        m.sr = DibujosCazadora.Renderer(go, "VFX", 80);
        m.nacido = Time.unscaledTime;
        return m;
    }

    // Coloca la marca sobre "punto" (el jefe). Si no cabe en pantalla y se pide,
    // se queda en el borde apuntando hacia el.
    public void Poner(Vector3 punto, Camera cam, Color color, float tamano, bool flechaFuera)
    {
        if (cam == null) return;
        Vector3 v = cam.WorldToViewportPoint(punto);
        bool dentro = v.z > 0f && v.x > 0.04f && v.x < 0.96f && v.y > 0.05f && v.y < 0.95f;
        Fuera = !dentro;

        if (dentro || !flechaFuera)
        {
            sr.sprite = Rombo();
            sr.transform.position = punto;
            sr.transform.rotation = Quaternion.identity;
            float late = 1f + 0.12f * Mathf.Sin((Time.unscaledTime - nacido) * 6f);
            sr.transform.localScale = Vector3.one * (tamano * late * Ppu / Lado);
            sr.color = new Color(color.r, color.g, color.b, dentro ? color.a : 0f);
            return;
        }

        // Fuera de pantalla: flecha pegada al borde, mirando hacia el jefe.
        Vector3 borde = new Vector3(Mathf.Clamp(v.x, 0.06f, 0.94f), Mathf.Clamp(v.y, 0.08f, 0.92f), Mathf.Abs(v.z));
        if (v.z < 0f) borde.x = 1f - borde.x;
        Vector3 mundo = cam.ViewportToWorldPoint(borde);
        mundo.z = 0f;
        Vector3 hacia = punto - mundo;
        sr.sprite = Flecha();
        sr.transform.position = mundo;
        sr.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(hacia.y, hacia.x) * Mathf.Rad2Deg);
        float pulso = 1f + 0.18f * Mathf.Sin((Time.unscaledTime - nacido) * 9f);
        sr.transform.localScale = Vector3.one * (tamano * 1.15f * pulso * Ppu / Lado);
        sr.color = color;
    }

    // Rombo hueco (como la mira de "fijar objetivo").
    private static Sprite Rombo()
    {
        if (rombo != null) return rombo;
        var px = new Color32[Lado * Lado];
        float medio = (Lado - 1) * 0.5f;
        for (int y = 0; y < Lado; y++)
            for (int x = 0; x < Lado; x++)
            {
                float d = (Mathf.Abs(x - medio) + Mathf.Abs(y - medio)) / medio;   // rombo
                if (d > 1.02f || d < 0.72f) continue;
                px[y * Lado + x] = d > 0.95f || d < 0.78f ? new Color32(255, 255, 255, 190) : new Color32(255, 255, 255, 255);
            }
        return rombo = Hacer(px, "RomboFijado");
    }

    // Punta de flecha hacia la derecha (se gira hacia el jefe).
    private static Sprite Flecha()
    {
        if (flecha != null) return flecha;
        var px = new Color32[Lado * Lado];
        float medio = (Lado - 1) * 0.5f;
        for (int y = 0; y < Lado; y++)
            for (int x = 0; x < Lado; x++)
            {
                float t = x / (float)(Lado - 1);                 // 0 atras, 1 punta
                float semi = Mathf.Lerp(medio * 0.95f, 0.5f, t);
                if (t < 0.25f || Mathf.Abs(y - medio) > semi) continue;
                bool borde = Mathf.Abs(y - medio) > semi - 1.6f || t > 0.93f;
                px[y * Lado + x] = borde ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 205);
            }
        return flecha = Hacer(px, "FlechaFijado");
    }

    private static Sprite Hacer(Color32[] px, string nombre)
    {
        var tex = new Texture2D(Lado, Lado, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        tex.SetPixels32(px);
        tex.Apply();
        Sprite s = Sprite.Create(tex, new Rect(0, 0, Lado, Lado), new Vector2(0.5f, 0.5f), Ppu, 0, SpriteMeshType.FullRect);
        s.name = nombre;
        return s;
    }
}
