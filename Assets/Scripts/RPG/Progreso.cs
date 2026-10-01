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
    // Los numeros (costes, lo que sube cada estadistica) estan en
    // Resources/AjustesProgreso, editables en el Inspector.
    public static int NivelMaximo => A.nivelMaximo;
    private static AjustesProgreso A => AjustesProgreso.Get();

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
            return Mathf.RoundToInt(A.costeBase + A.costePorNivel * n + A.costeCurva * n * n);
        }
    }

    // ------------------------------------------------------------------ Valores

    public static int VidaMax => A.vidaBase + A.vidaPorNivel * Nivel(Estadistica.Vida);
    public static float EstaminaMax => A.estaminaBase + A.estaminaPorNivel * Nivel(Estadistica.Estamina);
    public static float ManaMax => A.manaBase + A.manaPorNivel * Nivel(Estadistica.Mana);
    // Parte del dano que se quita. Cada nivel acerca la resistencia al tope
    // (AjustesProgreso.resistenciaMaxima) un poco: al principio se nota mucho y
    // luego cada vez menos, sin pasarse nunca del tope. Con los valores por
    // defecto: nivel 1 = 7 %, 5 = 26 %, 10 = 40 %, 20 = 51 %, tope 55 %.
    public static float ResHechizos => Resistencia(Nivel(Estadistica.ResHechizos));
    public static float ResGolpes => Resistencia(Nivel(Estadistica.ResGolpes));
    public static float Resistencia(int nivel) =>
        Mathf.Clamp01(A.resistenciaMaxima) * (1f - Mathf.Pow(1f - Mathf.Clamp(A.resistenciaCurva, 0.001f, 1f), Mathf.Max(0, nivel)));

    // Valor de una estadistica a un nivel dado (para la vista previa del menu).
    public static string Describir(Estadistica e, int nivel)
    {
        switch (e)
        {
            case Estadistica.Vida: return (A.vidaBase + A.vidaPorNivel * nivel).ToString();
            case Estadistica.Estamina: return Mathf.RoundToInt(A.estaminaBase + A.estaminaPorNivel * nivel).ToString();
            case Estadistica.Mana: return Mathf.RoundToInt(A.manaBase + A.manaPorNivel * nivel).ToString();
            default: return Mathf.RoundToInt(Resistencia(nivel) * 100f) + "%";
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

    // Paga almas (la tienda del totem). False si no llegan.
    public static bool Gastar(int cantidad)
    {
        Cargar();
        if (cantidad < 0 || almas < cantidad) return false;
        almas -= cantidad;
        Guardar();
        AlCambiarAlmas?.Invoke();
        return true;
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

    // En un desafio el personaje es otro: no se lee ni se escribe nada en
    // PlayerPrefs (asi no se mezcla con la partida normal).
    public static bool Aislado { get; set; }

    private static void Cargar()
    {
        if (cargado || Aislado) return;
        cargado = true;
        almas = PlayerPrefs.GetInt(RegistroGuardado.Clave("rpg_almas"), 0);
        for (int i = 0; i < NumEstadisticas; i++) niveles[i] = PlayerPrefs.GetInt(RegistroGuardado.Clave("rpg_nivel_" + i), 0);
        AlmasPerdidas = PlayerPrefs.GetInt(RegistroGuardado.Clave("rpg_mancha"), 0);
        PosicionMancha = new Vector2(PlayerPrefs.GetFloat(RegistroGuardado.Clave("rpg_mancha_x"), 0f), PlayerPrefs.GetFloat(RegistroGuardado.Clave("rpg_mancha_y"), 0f));
        EscenaMancha = PlayerPrefs.GetString(RegistroGuardado.Clave("rpg_mancha_escena"), "");
    }

    private static void Guardar()
    {
        if (Aislado) return;
        PlayerPrefs.SetInt(RegistroGuardado.Clave("rpg_almas"), almas);
        for (int i = 0; i < NumEstadisticas; i++) PlayerPrefs.SetInt(RegistroGuardado.Clave("rpg_nivel_" + i), niveles[i]);
        PlayerPrefs.SetInt(RegistroGuardado.Clave("rpg_mancha"), AlmasPerdidas);
        PlayerPrefs.SetFloat(RegistroGuardado.Clave("rpg_mancha_x"), PosicionMancha.x);
        PlayerPrefs.SetFloat(RegistroGuardado.Clave("rpg_mancha_y"), PosicionMancha.y);
        PlayerPrefs.SetString(RegistroGuardado.Clave("rpg_mancha_escena"), EscenaMancha ?? "");
        PlayerPrefs.Save();
    }

    // Lo pone una partida guardada al cargarla.
    public static void Establecer(int nuevasAlmas, int[] nuevosNiveles, int manchaAlmas, Vector2 manchaPos, string manchaEscena)
    {
        cargado = true;
        almas = nuevasAlmas;
        for (int i = 0; i < NumEstadisticas; i++) niveles[i] = nuevosNiveles != null && i < nuevosNiveles.Length ? nuevosNiveles[i] : 0;
        AlmasPerdidas = manchaAlmas;
        PosicionMancha = manchaPos;
        EscenaMancha = manchaEscena ?? "";
        Guardar();
        AlCambiarAlmas?.Invoke();
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
