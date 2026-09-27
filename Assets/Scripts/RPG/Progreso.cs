using System;
using UnityEngine;

// Progreso del personaje que sobrevive a la muerte y a los cambios de nivel:
// las almas y los niveles de cada estadistica. Se guarda en PlayerPrefs.
//
// Como en los Souls, cada nivel cuesta mas que el anterior (sube con el nivel
// total, no con el de esa estadistica), y al morir las almas se quedan en el
// suelo: si se vuelve a morir antes de recogerlas, se pierden.
public static class Progreso
{
    public enum Estadistica { Vida = 0, Estamina = 1, Mana = 2, ResHechizos = 3, ResGolpes = 4 }
    public const int NumEstadisticas = 5;
    public const int NivelMaximo = 20;

    public static event Action AlCambiarAlmas;
    public static event Action AlSubirNivel;

    private static int almas;
    private static readonly int[] niveles = new int[NumEstadisticas];
    private static bool cargado;

    // Mancha de almas: donde murio y cuantas se dejo.
    public static int AlmasPerdidas { get; private set; }
    public static Vector2 PosicionMancha { get; private set; }
    public static string EscenaMancha { get; private set; }

    public static int Almas { get { Cargar(); return almas; } }

    public static int Nivel(Estadistica e) { Cargar(); return niveles[(int)e]; }

    // Nivel del personaje: 1 + todo lo subido.
    public static int NivelTotal
    {
        get { Cargar(); int n = 1; foreach (int v in niveles) n += v; return n; }
    }

    // Coste del siguiente nivel: crece en curva, como en los Souls.
    public static int CosteSiguiente
    {
        get
        {
            int n = NivelTotal - 1;
            return Mathf.RoundToInt(150f + 70f * n + 14f * n * n);
        }
    }

    // ------------------------------------------------------------------ Valores

    public static int VidaMax => 100 + 10 * Nivel(Estadistica.Vida);
    public static float EstaminaMax => 100f + 8f * Nivel(Estadistica.Estamina);
    public static float ManaMax => 60f + 10f * Nivel(Estadistica.Mana);
    // Fraccion del dano que se quita (0.03 por nivel, hasta 0.45).
    public static float ResHechizos => Mathf.Min(0.45f, 0.03f * Nivel(Estadistica.ResHechizos));
    public static float ResGolpes => Mathf.Min(0.45f, 0.03f * Nivel(Estadistica.ResGolpes));

    // Valor de una estadistica a un nivel dado (para la vista previa del menu).
    public static string Describir(Estadistica e, int nivel)
    {
        switch (e)
        {
            case Estadistica.Vida: return (100 + 10 * nivel).ToString();
            case Estadistica.Estamina: return Mathf.RoundToInt(100f + 8f * nivel).ToString();
            case Estadistica.Mana: return Mathf.RoundToInt(60f + 10f * nivel).ToString();
            default: return Mathf.RoundToInt(Mathf.Min(0.45f, 0.03f * nivel) * 100f) + "%";
        }
    }

    public static string Nombre(Estadistica e)
    {
        switch (e)
        {
            case Estadistica.Vida: return "Vida";
            case Estadistica.Estamina: return "Estamina";
            case Estadistica.Mana: return "Maná";
            case Estadistica.ResHechizos: return "Resistencia a hechizos";
            default: return "Resistencia a golpes";
        }
    }

    // ------------------------------------------------------------------ Almas

    public static void SumarAlmas(int cantidad)
    {
        if (cantidad <= 0) return;
        Cargar();
        almas += cantidad;
        Guardar();
        AlCambiarAlmas?.Invoke();
    }

    public static bool PuedeSubir(Estadistica e) => Almas >= CosteSiguiente && Nivel(e) < NivelMaximo;

    public static bool Subir(Estadistica e)
    {
        if (!PuedeSubir(e)) return false;
        almas -= CosteSiguiente;
        niveles[(int)e]++;
        Guardar();
        AlCambiarAlmas?.Invoke();
        AlSubirNivel?.Invoke();
        return true;
    }

    // Al morir: las almas que se llevaban se quedan en el suelo. Las de una mancha
    // anterior sin recoger se pierden.
    public static void Morir(Vector2 donde, string escena)
    {
        Cargar();
        AlmasPerdidas = almas;
        PosicionMancha = donde;
        EscenaMancha = escena;
        almas = 0;
        Guardar();
        AlCambiarAlmas?.Invoke();
    }

    public static void RecuperarMancha()
    {
        if (AlmasPerdidas <= 0) return;
        int n = AlmasPerdidas;
        AlmasPerdidas = 0;
        SumarAlmas(n);
    }

    // ------------------------------------------------------------------ Guardado

    private static void Cargar()
    {
        if (cargado) return;
        cargado = true;
        almas = PlayerPrefs.GetInt("rpg_almas", 0);
        for (int i = 0; i < NumEstadisticas; i++) niveles[i] = PlayerPrefs.GetInt("rpg_nivel_" + i, 0);
        AlmasPerdidas = PlayerPrefs.GetInt("rpg_mancha", 0);
        PosicionMancha = new Vector2(PlayerPrefs.GetFloat("rpg_mancha_x", 0f), PlayerPrefs.GetFloat("rpg_mancha_y", 0f));
        EscenaMancha = PlayerPrefs.GetString("rpg_mancha_escena", "");
    }

    private static void Guardar()
    {
        PlayerPrefs.SetInt("rpg_almas", almas);
        for (int i = 0; i < NumEstadisticas; i++) PlayerPrefs.SetInt("rpg_nivel_" + i, niveles[i]);
        PlayerPrefs.SetInt("rpg_mancha", AlmasPerdidas);
        PlayerPrefs.SetFloat("rpg_mancha_x", PosicionMancha.x);
        PlayerPrefs.SetFloat("rpg_mancha_y", PosicionMancha.y);
        PlayerPrefs.SetString("rpg_mancha_escena", EscenaMancha ?? "");
        PlayerPrefs.Save();
    }

    // Empieza de cero (lo usan las pruebas automaticas).
    public static void Reiniciar()
    {
        cargado = true;
        almas = 0;
        for (int i = 0; i < NumEstadisticas; i++) niveles[i] = 0;
        AlmasPerdidas = 0;
        EscenaMancha = "";
        Guardar();
        AlCambiarAlmas?.Invoke();
    }
}
