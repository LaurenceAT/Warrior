using UnityEngine;

// Elementos con los que se puede imbuir la espada (rueda de la E). Solo estan
// los que tienen tajo de color propio en Efecto_Tajos: rayo, agua y viento
// tienen sonidos pero no tajo, y se han dejado fuera.
public enum Elemento { Ninguno = -1, Fuego = 0, Hielo = 1, Oscuro = 2, Sagrado = 3, Acido = 4 }

public static class Elementos
{
    public const int Cantidad = 5;

    public static readonly Elemento[] Todos = { Elemento.Fuego, Elemento.Hielo, Elemento.Oscuro, Elemento.Sagrado, Elemento.Acido };

    public static string Nombre(Elemento e)
    {
        switch (e)
        {
            case Elemento.Fuego: return "Fuego";
            case Elemento.Hielo: return "Escarcha";
            case Elemento.Oscuro: return "Oscuridad";
            case Elemento.Sagrado: return "Sagrado";
            case Elemento.Acido: return "Ácido";
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
            case Elemento.Sagrado: return "Probabilidad de aturdir";
            case Elemento.Acido: return "Corroe: recibe más daño";
            default: return "";
        }
    }

    public static Color Color(Elemento e)
    {
        switch (e)
        {
            case Elemento.Fuego: return new Color(1f, 0.32f, 0.12f, 1f);
            case Elemento.Hielo: return new Color(0.35f, 0.85f, 1f, 1f);
            case Elemento.Oscuro: return new Color(0.62f, 0.3f, 1f, 1f);
            case Elemento.Sagrado: return new Color(1f, 0.82f, 0.35f, 1f);
            case Elemento.Acido: return new Color(0.6f, 1f, 0.2f, 1f);
            default: return UnityEngine.Color.white;
        }
    }

    // Variante de color de Efecto_Tajos (color1..color5) que corresponde.
    public static int ColorTajo(Elemento e)
    {
        switch (e)
        {
            case Elemento.Fuego: return 2;   // rojo
            case Elemento.Hielo: return 5;   // cian
            case Elemento.Oscuro: return 3;  // violeta
            case Elemento.Sagrado: return 4; // dorado
            case Elemento.Acido: return 1;   // verde lima
            default: return 0;
        }
    }
}
