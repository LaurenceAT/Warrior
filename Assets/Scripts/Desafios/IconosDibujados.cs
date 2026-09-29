using UnityEngine;

// Iconos pequenos de pixel art dibujados por codigo, para lo que no hay en el
// pack de iconos: el candado (logros ocultos, dificultad bloqueada).
public static class IconosDibujados
{
    private static Sprite candado;

    public static Sprite Candado()
    {
        if (candado != null) return candado;
        // 16x16: arco arriba y cuerpo abajo con el ojo de la cerradura.
        string[] filas =
        {
            "................",
            ".....######.....",
            "....##....##....",
            "....#......#....",
            "....#......#....",
            "....#......#....",
            "...##########...",
            "...#oooooooo#...",
            "...#oooooooo#...",
            "...#ooo..ooo#...",
            "...#ooo..ooo#...",
            "...#oooo.ooo#...",
            "...#oooooooo#...",
            "...#oooooooo#...",
            "...##########...",
            "................",
        };
        Color borde = new Color(0.55f, 0.5f, 0.45f, 1f), cuerpo = new Color(0.36f, 0.33f, 0.3f, 1f);
        Texture2D t = new Texture2D(16, 16, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                char ch = filas[15 - y][x];
                t.SetPixel(x, y, ch == '#' ? borde : ch == 'o' ? cuerpo : new Color(0, 0, 0, 0));
            }
        t.Apply();
        candado = Sprite.Create(t, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16f);
        return candado;
    }
}
