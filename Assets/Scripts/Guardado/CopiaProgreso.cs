using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using UnityEngine;

// Copia del progreso (version web): un archivo de texto con las partidas y
// globales.json (desafios, logros, bestiario, guia vista...). "Descargar" lo baja
// a la carpeta de descargas del jugador; "Cargar" lo vuelve a poner y sustituye
// el progreso actual. Sirve aunque el navegador (Brave, cookies bloqueadas)
// borre lo guardado. No guarda nada nuevo en el juego: solo lee y escribe los
// archivos de RegistroGuardado.Carpeta.
public static class CopiaProgreso
{
    [Serializable]
    private class Archivo
    {
        public string nombre;
        public string contenido;
    }

    [Serializable]
    private class Copia
    {
        public string juego;
        public int version;
        public string fecha;
        public List<Archivo> archivos = new List<Archivo>();
    }

    private const string Juego = "Warrior";
    private const int Version = 1;
    // Solo estos archivos entran y salen de una copia.
    private static readonly Regex Permitidos = new Regex(@"^(partida_\d+\.json|" + Regex.Escape(RegistroGuardado.ArchivoGlobales) + ")$");

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern int WarriorDescargar(string nombre, string texto);
    [DllImport("__Internal")] private static extern void WarriorElegirArchivo(string objeto, string metodo);
    [DllImport("__Internal")] private static extern int WarriorEsBrave();
#endif

    // Los botones salen en la version web (y en el Editor, para probarlos).
    public static bool Disponible
    {
        get
        {
#if UNITY_WEBGL || UNITY_EDITOR
            return true;
#else
            return false;
#endif
        }
    }

    public static bool EsBrave
    {
        get
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            try { return WarriorEsBrave() == 1; } catch { return false; }
#elif UNITY_EDITOR
            // Las pruebas pueden hacer como si fuera Brave (PruebaCopia).
            return UnityEditor.SessionState.GetBool(ClaveForzarBrave, false);
#else
            return false;
#endif
        }
    }

#if UNITY_EDITOR
    public const string ClaveForzarBrave = "Warrior.PruebasForzarBrave";
#endif

    public static string NombreArchivo => $"Warrior_progreso_{DateTime.Now:yyyy-MM-dd}.json";

    // El texto de la copia con todo lo guardado ahora mismo.
    public static string Crear()
    {
        Globales.GuardarPendiente();
        Copia c = new Copia { juego = Juego, version = Version, fecha = DateTime.Now.ToString("yyyy-MM-dd HH:mm") };
        string carpeta = RegistroGuardado.Carpeta;
        if (Directory.Exists(carpeta))
            foreach (string f in Directory.GetFiles(carpeta))
            {
                string n = Path.GetFileName(f);
                if (Permitidos.IsMatch(n)) c.archivos.Add(new Archivo { nombre = n, contenido = File.ReadAllText(f) });
            }
        return JsonUtility.ToJson(c, true);
    }

    // Baja la copia. Devuelve false si no se pudo.
    public static bool Descargar()
    {
        string texto = Crear();
#if UNITY_WEBGL && !UNITY_EDITOR
        return WarriorDescargar(NombreArchivo, texto) == 1;
#elif UNITY_EDITOR
        string ruta = UnityEditor.EditorUtility.SaveFilePanel("Descargar mi progreso", "", NombreArchivo, "json");
        if (string.IsNullOrEmpty(ruta)) return false;
        File.WriteAllText(ruta, texto);
        return true;
#else
        return false;
#endif
    }

    // Abre "Abrir archivo"; al elegir uno, "alLeer" recibe su texto.
    public static void Elegir(Action<string> alLeer)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        Receptor.Preparar(alLeer);
        WarriorElegirArchivo(Receptor.Nombre, nameof(Receptor.AlLeer));
#elif UNITY_EDITOR
        string ruta = UnityEditor.EditorUtility.OpenFilePanel("Cargar mi progreso", "", "json");
        if (!string.IsNullOrEmpty(ruta)) alLeer?.Invoke(File.ReadAllText(ruta));
#endif
    }

    // Pone el progreso de una copia en lugar del actual. Devuelve el error, o null si fue bien.
    public static string Cargar(string texto)
    {
        Copia c;
        try { c = JsonUtility.FromJson<Copia>(texto); }
        catch { return "Ese archivo no es una copia de Warrior."; }
        if (c == null || c.juego != Juego || c.archivos == null) return "Ese archivo no es una copia de Warrior.";
        if (c.version > Version) return "Esa copia es de una versión más nueva del juego.";
        foreach (Archivo a in c.archivos)
            if (a == null || string.IsNullOrEmpty(a.nombre) || !Permitidos.IsMatch(a.nombre) || a.contenido == null) return "La copia está dañada.";

        string carpeta = RegistroGuardado.Carpeta;
        Directory.CreateDirectory(carpeta);
        // Fuera lo de ahora (solo los archivos de progreso) y dentro lo de la copia.
        foreach (string f in Directory.GetFiles(carpeta))
            if (Permitidos.IsMatch(Path.GetFileName(f))) File.Delete(f);
        foreach (Archivo a in c.archivos)
            RegistroGuardado.EscribirSeguro(Path.Combine(carpeta, a.nombre), a.contenido);
        RegistroGuardado.GuardarDisco();
        Partida.Descargar();
        Globales.Recargar();
        return null;
    }

    // Recibe el texto del archivo desde el navegador (SendMessage).
    private class Receptor : MonoBehaviour
    {
        public const string Nombre = "CopiaProgresoReceptor";
        private static Action<string> pendiente;

        public static void Preparar(Action<string> alLeer)
        {
            pendiente = alLeer;
            if (GameObject.Find(Nombre) != null) return;
            GameObject go = new GameObject(Nombre);
            go.hideFlags = HideFlags.HideInHierarchy;
            DontDestroyOnLoad(go);
            go.AddComponent<Receptor>();
        }

        public void AlLeer(string texto)
        {
            Action<string> a = pendiente;
            pendiente = null;
            a?.Invoke(texto);
        }
    }
}
