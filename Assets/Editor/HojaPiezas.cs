using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// Solo Editor: hoja de contacto de los sprites de una textura (cada pieza
// ampliada en su celda, en orden) y la lista con su numero y rectangulo. Sirve
// para identificar las piezas de un tileset sin abrirlo a mano.
public static class HojaPiezas
{
    public static void Generar()
    {
        string ruta = System.Environment.GetEnvironmentVariable("HOJA_TEXTURA");
        int escala = int.Parse(System.Environment.GetEnvironmentVariable("HOJA_ESCALA") ?? "3");
        Generar(ruta, escala);
    }

    public static void Generar(string ruta, int escala)
    {
        var fuente = new Texture2D(2, 2);
        fuente.LoadImage(File.ReadAllBytes(ruta));
        Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(ruta).OfType<Sprite>()
            .OrderByDescending(s => Mathf.Round(s.rect.y / 8f)).ThenBy(s => s.rect.x).ToArray();
        int col = 12;
        int celda = (int)sprites.Max(s => Mathf.Max(s.rect.width, s.rect.height)) * escala + 16;
        celda = Mathf.Min(celda, 360);
        int filas = Mathf.CeilToInt(sprites.Length / (float)col);
        var hoja = new Texture2D(col * celda, filas * celda, TextureFormat.RGBA32, false);
        Color fondo = new Color(0.35f, 0.4f, 0.45f, 1f);
        Color[] relleno = Enumerable.Repeat(fondo, hoja.width * hoja.height).ToArray();
        hoja.SetPixels(relleno);
        var lista = new StringBuilder();
        for (int i = 0; i < sprites.Length; i++)
        {
            Rect r = sprites[i].rect;
            int cx = (i % col) * celda, cy = (filas - 1 - i / col) * celda;
            int esc = Mathf.Max(1, Mathf.Min(escala, (celda - 16) / (int)Mathf.Max(r.width, r.height)));
            for (int y = 0; y < (int)r.height; y++)
            for (int x = 0; x < (int)r.width; x++)
            {
                Color c = fuente.GetPixel((int)r.x + x, (int)r.y + y);
                if (c.a < 0.05f) continue;
                for (int dy = 0; dy < esc; dy++)
                for (int dx = 0; dx < esc; dx++)
                {
                    int px = cx + 8 + x * esc + dx, py = cy + 8 + y * esc + dy;
                    if (px < hoja.width && py < hoja.height) hoja.SetPixel(px, py, c);
                }
            }
            // Marca de numero: una fila de puntos (decenas) y otra (unidades) en la esquina.
            for (int d = 0; d < i / 10; d++) Punto(hoja, cx + 2 + d * 3, cy + celda - 3, Color.yellow);
            for (int u = 0; u < i % 10; u++) Punto(hoja, cx + 2 + u * 3, cy + celda - 7, Color.cyan);
            lista.AppendLine($"{i}: {sprites[i].name} x={r.x} y={r.y} w={r.width} h={r.height}");
        }
        hoja.Apply();
        string nombre = Path.GetFileNameWithoutExtension(ruta);
        string dir = Path.Combine(Directory.GetCurrentDirectory(), "PruebaNieve");
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, "hoja_" + nombre + ".png"), hoja.EncodeToPNG());
        File.WriteAllText(Path.Combine(dir, "hoja_" + nombre + ".txt"), lista.ToString());
        Debug.Log("[Hoja] " + nombre + " " + sprites.Length);
    }

    // Recorte ampliado de una zona de la textura (x, y desde arriba a la
    // izquierda, como en un editor de imagenes) con una rejilla cada 16 px.
    public static void Recorte()
    {
        string ruta = System.Environment.GetEnvironmentVariable("HOJA_TEXTURA");
        string[] r = System.Environment.GetEnvironmentVariable("HOJA_RECORTE").Split(',');
        int x0 = int.Parse(r[0]), y0 = int.Parse(r[1]), w = int.Parse(r[2]), h = int.Parse(r[3]);
        int esc = int.Parse(System.Environment.GetEnvironmentVariable("HOJA_ESCALA") ?? "3");
        var fuente = new Texture2D(2, 2);
        fuente.LoadImage(File.ReadAllBytes(ruta));
        var sal = new Texture2D(w * esc, h * esc, TextureFormat.RGBA32, false);
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            int sx = x0 + x, sy = fuente.height - 1 - (y0 + y);
            Color c = sx < fuente.width && sy >= 0 ? fuente.GetPixel(sx, sy) : Color.clear;
            if (c.a < 0.05f) c = ((sx / 16 + (fuente.height - 1 - sy) / 16) % 2 == 0) ? new Color(0.55f, 0.6f, 0.65f) : new Color(0.47f, 0.52f, 0.57f);
            for (int dy = 0; dy < esc; dy++) for (int dx = 0; dx < esc; dx++) sal.SetPixel(x * esc + dx, (h - 1 - y) * esc + dy, c);
        }
        sal.Apply();
        string dir = Path.Combine(Directory.GetCurrentDirectory(), "PruebaNieve");
        File.WriteAllBytes(Path.Combine(dir, $"recorte_{x0}_{y0}.png"), sal.EncodeToPNG());
    }

    private static void Punto(Texture2D t, int x, int y, Color c)
    {
        for (int a = 0; a < 2; a++) for (int b = 0; b < 2; b++) if (x + a < t.width && y + b < t.height && y + b >= 0) t.SetPixel(x + a, y + b, c);
    }
}
