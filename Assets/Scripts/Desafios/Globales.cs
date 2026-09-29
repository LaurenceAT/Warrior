using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

// Lo que es de todo el juego y no de una partida: logros, desafios completados
// y mejores tiempos. Va en su propio archivo (globales.json), aparte de las
// partidas guardadas: entrar a un desafio nunca toca una partida.
public static class Globales
{
    [Serializable]
    public class Registro
    {
        public string jefe;
        public bool dificil;
        public bool completado;
        public float mejorTiempo;
        public int muertesMejor;
    }

    [Serializable]
    private class Datos
    {
        public List<string> logros = new List<string>();
        public List<Registro> desafios = new List<Registro>();
        // Marcas sueltas de todo el juego (jefe secreto ya revelado, dialogo visto...).
        public List<string> marcas = new List<string>();
    }

    private static Datos datos;

    private static string Archivo => Path.Combine(Partida.CarpetaPruebas ?? Path.Combine(Application.persistentDataPath, "Partidas"), "globales.json");

    private static Datos D
    {
        get
        {
            if (datos != null) return datos;
            try
            {
                if (File.Exists(Archivo)) datos = JsonUtility.FromJson<Datos>(File.ReadAllText(Archivo));
            }
            catch (Exception e) { Debug.LogWarning("[Globales] No se pudo leer: " + e.Message); }
            if (datos == null) datos = new Datos();
            datos.logros ??= new List<string>();
            datos.desafios ??= new List<Registro>();
            datos.marcas ??= new List<string>();
            return datos;
        }
    }

    private static void Guardar()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Archivo));
            File.WriteAllText(Archivo, JsonUtility.ToJson(D, true));
        }
        catch (Exception e) { Debug.LogWarning("[Globales] No se pudo guardar: " + e.Message); }
    }

    // Para las pruebas: vuelve a leer del archivo.
    public static void Recargar() => datos = null;

    // ------------------------------------------------------------------ Logros

    public static bool TieneLogro(string id) => D.logros.Contains(id);

    // True si es nuevo.
    public static bool DarLogro(string id)
    {
        if (string.IsNullOrEmpty(id) || D.logros.Contains(id)) return false;
        D.logros.Add(id);
        Guardar();
        return true;
    }

    // ------------------------------------------------------------------ Marcas

    public static bool Marca(string id) => D.marcas.Contains(id);

    public static void PonerMarca(string id, bool valor)
    {
        if (string.IsNullOrEmpty(id) || Marca(id) == valor) return;
        if (valor) D.marcas.Add(id);
        else D.marcas.Remove(id);
        Guardar();
    }

    // ------------------------------------------------------------------ Desafios

    public static Registro Desafio(string jefe, bool dificil) => D.desafios.FirstOrDefault(r => r.jefe == jefe && r.dificil == dificil);

    public static bool Completado(string jefe, bool dificil) => Desafio(jefe, dificil)?.completado ?? false;

    // Apunta el desafio completado. True si es un tiempo mejor que el anterior.
    public static bool Completar(string jefe, bool dificil, float segundos, int muertes)
    {
        Registro r = Desafio(jefe, dificil);
        if (r == null) D.desafios.Add(r = new Registro { jefe = jefe, dificil = dificil });
        bool record = !r.completado || segundos < r.mejorTiempo;
        r.completado = true;
        if (record) { r.mejorTiempo = segundos; r.muertesMejor = muertes; }
        Guardar();
        return record;
    }

    public static string Tiempo(float segundos)
    {
        int s = Mathf.Max(0, Mathf.FloorToInt(segundos));
        return s >= 3600 ? $"{s / 3600}:{s / 60 % 60:00}:{s % 60:00}" : $"{s / 60}:{s % 60:00}";
    }
}
