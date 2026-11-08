using UnityEngine;

// Elementos con los que se puede imbuir la espada (rueda de la E). Solo estan
// los que tienen tajo de color propio en Efecto_Tajos: rayo, agua y viento
// tienen sonidos pero no tajo, y se han dejado fuera. El sangrado sustituye al
// acido (conserva su numero, 4, para no romper lo ya guardado).
// Natura (5) no es un elemento de dano: no hace nada al enemigo (los golpes son
// fisicos), solo cambia la estamina del player. Por eso no esta en "Todos"
// (resistencias, Bitacora, Bestiario...), solo en la rueda ("Rueda").
public enum Elemento { Ninguno = -1, Fuego = 0, Hielo = 1, Oscuro = 2, Sagrado = 3, Sangrado = 4, Natura = 5 }

public static class Elementos
{
    // Elementos de dano (los que cuentan en fichas, resistencias y desafios).
    public const int Cantidad = 5;

    public static readonly Elemento[] Todos = { Elemento.Fuego, Elemento.Hielo, Elemento.Oscuro, Elemento.Sagrado, Elemento.Sangrado };

    // Lo que sale en la rueda de la E (en orden: su posicion es su numero).
    public const int CantidadRueda = 6;
    public static readonly Elemento[] Rueda = { Elemento.Fuego, Elemento.Hielo, Elemento.Oscuro, Elemento.Sagrado, Elemento.Sangrado, Elemento.Natura };

    // Si el elemento hace algo al golpear (Natura y Ninguno, no).
    public static bool EsDeDano(Elemento e) => e >= Elemento.Fuego && e <= Elemento.Sangrado;

    public static string Nombre(Elemento e)
    {
        switch (e)
        {
            case Elemento.Fuego: return "Fuego";
            case Elemento.Hielo: return "Escarcha";
            case Elemento.Oscuro: return "Oscuridad";
            case Elemento.Sagrado: return "Sagrado";
            case Elemento.Sangrado: return "Sangrado";
            case Elemento.Natura: return "Natura";
            default: return "Sin imbuir";
        }
    }

    // Lo que hace ademas del color. Se ve en la rueda.
    public static string Efecto(Elemento e)
    {
        switch (e)
        {
            case Elemento.Fuego: return "Quemadura: daño con el tiempo";
            case Elemento.Hielo: return "Ralentiza; acumulado, congela";
            case Elemento.Oscuro: return "Drena vida al golpear";
            case Elemento.Sagrado: return "A veces aturde un instante (menos que un parry)";
            case Elemento.Sangrado: return "Cuesta vida. Acumula sangrado en él... y un poco en ti";
            case Elemento.Natura: return "Vacía tu estamina. Luego vuelve antes y los golpes cansan menos";
            default: return "";
        }
    }

    public static Color Color(Elemento e)
    {
        switch (e)
        {
            case Elemento.Fuego: return new Color(1f, 0.48f, 0.1f, 1f);
            case Elemento.Hielo: return new Color(0.35f, 0.85f, 1f, 1f);
            case Elemento.Oscuro: return new Color(0.62f, 0.3f, 1f, 1f);
            case Elemento.Sagrado: return new Color(1f, 0.82f, 0.35f, 1f);
            case Elemento.Sangrado: return new Color(0.86f, 0.06f, 0.14f, 1f);
            case Elemento.Natura: return new Color(0.42f, 0.86f, 0.3f, 1f);
            default: return UnityEngine.Color.white;
        }
    }

    // Icono del elemento (RecursosRPG, clave "elemento_N"). Si Natura no tiene
    // icono propio todavia, se dibuja una hoja en pixel art.
    public static Sprite Icono(Elemento e)
    {
        Sprite s = RecursosRPG.Get().Icono("elemento_" + (int)e);
        if (s == null && e == Elemento.Natura) s = HojaNatura();
        return s;
    }

    // Sonido con prefijo ("imbuir_", "golpe_"...). Si Natura no tiene el suyo en
    // RecursosRPG, usa el del sagrado. Al anadir "imbuir_5" alli, se usara ese.
    public static string ClaveSonido(string prefijo, Elemento e)
    {
        string clave = prefijo + (int)e;
        if (e == Elemento.Natura && RecursosRPG.Get().Sonido(clave) == null) clave = prefijo + (int)Elemento.Sagrado;
        return clave;
    }

    private static Sprite hoja;
    private static Sprite HojaNatura()
    {
        if (hoja != null) return hoja;
        // 16x16: . vacio, o borde oscuro, g verde, l verde claro, n nervio.
        string[] filas =
        {
            "................",
            "..........oooo..",
            "........oogllo..",
            ".......oggglno..",
            "......ogggglno..",
            ".....oggggnggo..",
            "....ogggngggo...",
            "....oggngggo....",
            "...oggngggo.....",
            "...ognggggo.....",
            "...onggggo......",
            "...ongggo.......",
            "..onoooo........",
            ".on.............",
            "on..............",
            "................",
        };
        Color borde = new Color(0.1f, 0.28f, 0.1f), verde = new Color(0.36f, 0.74f, 0.26f),
              claro = new Color(0.62f, 0.93f, 0.42f), nervio = new Color(0.2f, 0.48f, 0.16f);
        var t = new Texture2D(16, 16, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                char c = filas[15 - y][x];
                t.SetPixel(x, y, c == 'o' ? borde : c == 'g' ? verde : c == 'l' ? claro : c == 'n' ? nervio : UnityEngine.Color.clear);
            }
        t.Apply();
        hoja = Sprite.Create(t, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16f);
        hoja.name = "HojaNatura";
        return hoja;
    }

    // Tinte que se pone al tajo de color. Ya no se tine ninguno: tenir un tajo de
    // otro color lo ensuciaba. El sagrado y el sangrado tienen sus propios dibujos
    // recoloreados (Assets/Sprites/Tajos, los hace la Ronda 8).
    public static Color TinteTajo(Elemento e) => UnityEngine.Color.white;

    // Variante de color de Efecto_Tajos (color1..color5) de la que sale cada uno.
    public static int ColorTajo(Elemento e)
    {
        switch (e)
        {
            case Elemento.Fuego: return 4;    // naranja
            case Elemento.Hielo: return 5;    // cian
            case Elemento.Oscuro: return 3;   // violeta
            case Elemento.Sagrado: return 4;  // naranja -> recoloreado a amarillo y blanco
            case Elemento.Sangrado: return 2; // carmesi -> recoloreado a rojo
            default: return 0;                // Natura: el tajo normal (golpe fisico)
        }
    }
}
