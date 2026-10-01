using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

// Lo que es de todo el juego y no de una partida: logros, desafios completados
// y mejores tiempos, y lo descubierto de jefes y enemigos (Bitacora). Va en su
// propio archivo (globales.json), aparte de las partidas guardadas: entrar a un
// desafio nunca toca una partida.
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
        // Estadisticas de la ficha (en archivos viejos empiezan en 0).
        public int intentos;
        public int muertes;
    }

    [Serializable]
    public class Contador
    {
        public string clave;
        public int n;
    }

    [Serializable]
    private class Datos
    {
        public List<string> logros = new List<string>();
        public List<Registro> desafios = new List<Registro>();
        // Marcas sueltas de todo el juego (jefe secreto ya revelado, dialogo visto...).
        public List<string> marcas = new List<string>();
        // Bitacora: lo descubierto (ataques, fases, elementos, enemigos...) y lo
        // que ya se ha visto en el menu (lo demas lleva el punto de "nuevo").
        public List<string> descubiertos = new List<string>();
        public List<string> vistos = new List<string>();
        public List<Contador> contadores = new List<Contador>();
    }

    private static Datos datos;
    // Copias rapidas de las listas (se consultan en cada golpe).
    private static HashSet<string> descubiertos, vistos;
    private static bool pendiente;

    private static string Archivo => Path.Combine(RegistroGuardado.Carpeta, RegistroGuardado.ArchivoGlobales);

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
            datos.descubiertos ??= new List<string>();
            datos.vistos ??= new List<string>();
            datos.contadores ??= new List<Contador>();
            descubiertos = new HashSet<string>(datos.descubiertos);
            vistos = new HashSet<string>(datos.vistos);
            return datos;
        }
    }

    private static void Guardar()
    {
        pendiente = false;
        try
        {
            RegistroGuardado.EscribirSeguro(Archivo, JsonUtility.ToJson(D, true));
        }
        catch (Exception e) { Debug.LogWarning("[Globales] No se pudo guardar: " + e.Message); }
    }

    // Los contadores (enemigos derrotados) no escriben el archivo en cada golpe:
    // se guardan al morir, al cambiar de escena, en la pausa y al salir del juego.
    public static void GuardarPendiente()
    {
        if (pendiente) Guardar();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Iniciar()
    {
        UnityEngine.SceneManagement.SceneManager.sceneUnloaded += _ => GuardarPendiente();
        Application.quitting += GuardarPendiente;
    }

    // Para las pruebas: vuelve a leer del archivo.
    public static void Recargar()
    {
        datos = null;
        pendiente = false;
    }

    // Borra una categoria de globales.json (reinicio del Editor, RegistroGuardado):
    //   - Desafios: desafios completados, tiempos, estadisticas, fichas de jefe
    //     ("j:") y todas las marcas (jefe secreto, dialogo, codigos, migracion).
    //   - Logros: los logros.
    //   - Bestiario: lo descubierto y las derrotas ("b:").
    // Si un dato nuevo va en globales.json, su parte se borra aqui.
    public static void Borrar(CategoriaGuardado c)
    {
        Datos d = D;
        switch (c)
        {
            case CategoriaGuardado.Desafios:
                d.desafios.Clear();
                d.marcas.Clear();
                Quitar(d, "j:");
                break;
            case CategoriaGuardado.Logros:
                d.logros.Clear();
                break;
            case CategoriaGuardado.Bestiario:
                Quitar(d, "b:");
                break;
            default:
                return;
        }
        Guardar();
    }

    private static void Quitar(Datos d, string prefijo)
    {
        d.descubiertos.RemoveAll(x => x.StartsWith(prefijo));
        d.vistos.RemoveAll(x => x.StartsWith(prefijo));
        d.contadores.RemoveAll(x => x.clave.StartsWith(prefijo));
        descubiertos = new HashSet<string>(d.descubiertos);
        vistos = new HashSet<string>(d.vistos);
    }

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

    // ------------------------------------------------------------------ Descubrimientos

    public static bool Descubierto(string clave)
    {
        Datos _ = D;
        return clave != null && descubiertos.Contains(clave);
    }

    // True si es nuevo. "guardar": se escribe al momento (si no, con lo pendiente).
    public static bool Descubrir(string clave, bool guardar = true)
    {
        Datos d = D;
        if (string.IsNullOrEmpty(clave) || !descubiertos.Add(clave)) return false;
        d.descubiertos.Add(clave);
        if (guardar) Guardar(); else pendiente = true;
        return true;
    }

    // Ya se ha visto en el menu (sin punto de "nuevo").
    public static bool Visto(string clave)
    {
        Datos _ = D;
        return clave != null && vistos.Contains(clave);
    }

    public static void MarcarVistos(IEnumerable<string> claves)
    {
        Datos d = D;
        bool cambio = false;
        foreach (string c in claves)
            if (!string.IsNullOrEmpty(c) && vistos.Add(c)) { d.vistos.Add(c); cambio = true; }
        if (cambio) Guardar();
    }

    // Olvida todo lo que empieza por "prefijo" (herramientas de prueba).
    public static void Olvidar(string prefijo)
    {
        Datos d = D;
        d.descubiertos.RemoveAll(c => c.StartsWith(prefijo));
        d.vistos.RemoveAll(c => c.StartsWith(prefijo));
        d.contadores.RemoveAll(c => c.clave.StartsWith(prefijo));
        descubiertos = new HashSet<string>(d.descubiertos);
        vistos = new HashSet<string>(d.vistos);
        Guardar();
    }

    public static int Cuenta(string clave) => D.contadores.FirstOrDefault(c => c.clave == clave)?.n ?? 0;

    // Suma al contador y devuelve el valor nuevo (se guarda con lo pendiente).
    public static int Sumar(string clave, int cuanto = 1)
    {
        Contador c = D.contadores.FirstOrDefault(x => x.clave == clave);
        if (c == null) D.contadores.Add(c = new Contador { clave = clave });
        c.n += cuanto;
        pendiente = true;
        return c.n;
    }

    // ------------------------------------------------------------------ Desafios

    public static Registro Desafio(string jefe, bool dificil) => D.desafios.FirstOrDefault(r => r.jefe == jefe && r.dificil == dificil);

    private static Registro RegistroDe(string jefe, bool dificil)
    {
        Registro r = Desafio(jefe, dificil);
        if (r == null) D.desafios.Add(r = new Registro { jefe = jefe, dificil = dificil });
        return r;
    }

    public static bool Completado(string jefe, bool dificil) => Desafio(jefe, dificil)?.completado ?? false;

    // Apunta el desafio completado. True si es un tiempo mejor que el anterior.
    public static bool Completar(string jefe, bool dificil, float segundos, int muertes)
    {
        Registro r = RegistroDe(jefe, dificil);
        bool record = !r.completado || segundos < r.mejorTiempo;
        r.completado = true;
        if (record) { r.mejorTiempo = segundos; r.muertesMejor = muertes; }
        Guardar();
        return record;
    }

    // Estadisticas de la ficha: cada vez que se entra a la arena y cada muerte.
    public static void SumarIntento(string jefe, bool dificil)
    {
        RegistroDe(jefe, dificil).intentos++;
        Guardar();
    }

    public static void SumarMuerte(string jefe, bool dificil)
    {
        RegistroDe(jefe, dificil).muertes++;
        Guardar();
    }

    // Borra intentos y muertes de un jefe (herramientas de prueba). Los tiempos se quedan.
    public static void OlvidarEstadisticas(string jefe)
    {
        foreach (Registro r in D.desafios.Where(r => r.jefe == jefe)) r.intentos = r.muertes = 0;
        Guardar();
    }

    public static string Tiempo(float segundos)
    {
        int s = Mathf.Max(0, Mathf.FloorToInt(segundos));
        return s >= 3600 ? $"{s / 3600}:{s / 60 % 60:00}:{s % 60:00}" : $"{s / 60}:{s % 60:00}";
    }
}
