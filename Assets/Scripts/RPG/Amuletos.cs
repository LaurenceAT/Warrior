using System;
using System.Collections.Generic;
using UnityEngine;

// Lo que tienes de amuletos: los conseguidos, los equipados (un hueco cada uno)
// y cuantos huecos hay abiertos (2 al empezar; el Vestigio del Vacio abre otro).
// Por ahora solo en memoria: en los desafios, como su inventario (se conserva
// al morir y se pierde al reiniciar o salir). Aun no se guarda en la partida.
public static class Amuletos
{
    private static readonly HashSet<string> conseguidos = new HashSet<string>();
    private static string[] equipados = new string[0];
    private static int abiertos = -1;

    public static event Action AlCambiar;

    private static FichaAmuletos F => FichaAmuletos.Get();
    public static int HuecosMaximos => F != null ? Mathf.Max(1, F.huecosMaximos) : 4;
    public static int HuecosIniciales => F != null ? Mathf.Clamp(F.huecosIniciales, 0, HuecosMaximos) : 2;

    public static int HuecosAbiertos
    {
        get { Asegurar(); return abiertos; }
    }

    public static int Conseguidos => conseguidos.Count;
    public static int Total => F != null ? F.amuletos.Count : 0;

    private static void Asegurar()
    {
        if (abiertos < 0) abiertos = HuecosIniciales;
        if (equipados.Length != HuecosMaximos) Array.Resize(ref equipados, HuecosMaximos);
    }

    public static bool Tiene(string id) => !string.IsNullOrEmpty(id) && conseguidos.Contains(id);

    public static void Dar(string id)
    {
        if (string.IsNullOrEmpty(id) || !conseguidos.Add(id)) return;
        AlCambiar?.Invoke();
    }

    // El amuleto de un hueco (null si esta vacio o cerrado).
    public static string Equipado(int hueco)
    {
        Asegurar();
        return hueco >= 0 && hueco < equipados.Length ? equipados[hueco] : null;
    }

    public static int HuecoDe(string id)
    {
        Asegurar();
        return string.IsNullOrEmpty(id) ? -1 : Array.IndexOf(equipados, id);
    }

    public static bool Abierto(int hueco) => hueco >= 0 && hueco < HuecosAbiertos;

    // En el primer hueco abierto y libre. False si no lo tienes, ya esta puesto
    // o no queda hueco.
    public static bool Equipar(string id)
    {
        Asegurar();
        if (!Tiene(id) || HuecoDe(id) >= 0) return false;
        for (int i = 0; i < abiertos; i++)
            if (string.IsNullOrEmpty(equipados[i])) { equipados[i] = id; AlCambiar?.Invoke(); return true; }
        return false;
    }

    public static bool HayHuecoLibre
    {
        get
        {
            Asegurar();
            for (int i = 0; i < abiertos; i++) if (string.IsNullOrEmpty(equipados[i])) return true;
            return false;
        }
    }

    public static void Quitar(int hueco)
    {
        Asegurar();
        if (hueco < 0 || hueco >= equipados.Length || string.IsNullOrEmpty(equipados[hueco])) return;
        equipados[hueco] = null;
        AlCambiar?.Invoke();
    }

    // Vestigio del Vacio: abre un hueco mas. False si ya estan todos abiertos.
    public static bool AbrirHueco()
    {
        Asegurar();
        if (abiertos >= HuecosMaximos) return false;
        abiertos++;
        AlCambiar?.Invoke();
        return true;
    }

    // De cero (desafio nuevo, reinicio o salida).
    public static void Reiniciar()
    {
        conseguidos.Clear();
        equipados = new string[0];
        abiertos = -1;
        Asegurar();
        EfectosAmuletos.RecargarCalavera();
        AlCambiar?.Invoke();
    }
}
