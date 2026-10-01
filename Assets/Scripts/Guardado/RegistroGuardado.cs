using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

// Categorias del progreso (las usa el reinicio del Editor: Herramientas > Progreso del juego).
public enum CategoriaGuardado { Partida, Desafios, Logros, Bestiario, Opciones }

// Registro central de TODO lo que el juego guarda. Las herramientas del Editor
// (borrar, respaldar, restaurar, perfil de pruebas) recorren esta lista: lo que
// este aqui queda cubierto solo.
//
// Hay tres tipos de dato:
//   - Claves de PlayerPrefs: se leen y escriben siempre con Clave("nombre").
//   - Archivos de la carpeta de guardado (Carpeta): se buscan con un patron.
//   - Partes de globales.json: las borra Globales.Borrar(categoria).
//
// COMO REGISTRAR UN DATO NUEVO:
//   1. Anadelo a la lista "Datos" de abajo con su categoria:
//        new Dato(CategoriaGuardado.X, "Para que sirve", prefs: new[] { P("mi_clave", Tipo.Int) })
//      o, si es un archivo:  new Dato(CategoriaGuardado.X, "...", archivos: "mi_archivo_*.json")
//   2. Al leerlo o escribirlo usa RegistroGuardado.Clave("mi_clave") (PlayerPrefs) o
//      RegistroGuardado.Carpeta (archivos). En el Editor sale un aviso si una
//      clave no esta registrada.
//   3. Si va dentro de globales.json, anade su parte en Globales.Borrar.
public static class RegistroGuardado
{
    public enum Tipo { Int, Float, String }

    public class ClavePrefs
    {
        public string nombre;
        public Tipo tipo;
    }

    public class Dato
    {
        public CategoriaGuardado categoria;
        public string descripcion;
        public ClavePrefs[] prefs;
        public string archivos;      // patron en la carpeta de guardado
        public bool enGlobales;      // una parte de globales.json

        public Dato(CategoriaGuardado c, string descripcion, ClavePrefs[] prefs = null, string archivos = null, bool enGlobales = false)
        {
            categoria = c;
            this.descripcion = descripcion;
            this.prefs = prefs ?? new ClavePrefs[0];
            this.archivos = archivos;
            this.enGlobales = enGlobales;
        }
    }

    private static ClavePrefs P(string nombre, Tipo tipo) => new ClavePrefs { nombre = nombre, tipo = tipo };

    public const string ArchivoGlobales = "globales.json";

    // ------------------------------------------------------------------ La lista

    public static readonly Dato[] Datos =
    {
        new Dato(CategoriaGuardado.Partida, "Partidas guardadas", archivos: "partida_*.json"),
        new Dato(CategoriaGuardado.Partida, "Personaje (almas, niveles y mancha de almas)", new[]
        {
            P("rpg_almas", Tipo.Int), P("rpg_nivel_0", Tipo.Int), P("rpg_nivel_1", Tipo.Int), P("rpg_nivel_2", Tipo.Int),
            P("rpg_nivel_3", Tipo.Int), P("rpg_nivel_4", Tipo.Int), P("rpg_mancha", Tipo.Int), P("rpg_mancha_x", Tipo.Float),
            P("rpg_mancha_y", Tipo.Float), P("rpg_mancha_escena", Tipo.String),
        }),
        new Dato(CategoriaGuardado.Desafios, "Desafíos completados, tiempos, estadísticas, fichas de jefe, jefe secreto y códigos", archivos: ArchivoGlobales, enGlobales: true),
        new Dato(CategoriaGuardado.Logros, "Logros", archivos: ArchivoGlobales, enGlobales: true),
        new Dato(CategoriaGuardado.Bestiario, "Bestiario", archivos: ArchivoGlobales, enGlobales: true),
        new Dato(CategoriaGuardado.Opciones, "Volumen y brillo", new[]
        {
            P("volGeneral", Tipo.Float), P("volMusica", Tipo.Float), P("volEfectos", Tipo.Float), P("brillo", Tipo.Float),
        }),
    };

    public static IEnumerable<Dato> De(CategoriaGuardado c) => Datos.Where(d => d.categoria == c);

    // ------------------------------------------------------------------ Perfil de pruebas (solo Editor)

    // "Simular jugador nuevo": en el Editor, el juego usa otra carpeta y otras
    // claves (vacias). Las opciones (volumen, brillo) se comparten.
    public const string ClaveEditorPerfil = "Warrior.PerfilPruebas";
    public const string PrefijoPruebas = "pruebas_";
    public const string CarpetaReal = "Partidas", CarpetaPerfil = "Partidas_PerfilPruebas";

    public static bool PerfilPruebas
    {
        get
        {
#if UNITY_EDITOR
            return UnityEditor.EditorPrefs.GetBool(ClaveEditorPerfil, false);
#else
            return false;
#endif
        }
    }

    // Carpeta del perfil activo (las pruebas automaticas usan la suya: Partida.CarpetaPruebas).
    public static string Carpeta => Partida.CarpetaPruebas ?? CarpetaDe(PerfilPruebas);

    public static string CarpetaDe(bool perfilPruebas) => Path.Combine(Application.persistentDataPath, perfilPruebas ? CarpetaPerfil : CarpetaReal);

    // Nombre real de una clave de PlayerPrefs (con el prefijo del perfil de pruebas).
    public static string Clave(string nombre) => ClaveDe(nombre, PerfilPruebas);

    private static Dictionary<string, Dato> porClave;

    public static string ClaveDe(string nombre, bool perfilPruebas)
    {
        if (porClave == null)
        {
            porClave = new Dictionary<string, Dato>();
            foreach (Dato x in Datos) foreach (ClavePrefs p in x.prefs) porClave[p.nombre] = x;
        }
        porClave.TryGetValue(nombre, out Dato d);
#if UNITY_EDITOR
        if (d == null) Debug.LogWarning($"[RegistroGuardado] La clave '{nombre}' no esta registrada: el reinicio del Editor no la cubre.");
#endif
        bool compartida = d != null && d.categoria == CategoriaGuardado.Opciones;
        return perfilPruebas && !compartida ? PrefijoPruebas + nombre : nombre;
    }

    // Escribe un archivo sin dejarlo a medias: primero a .tmp y luego lo sustituye.
    public static void EscribirSeguro(string ruta, string texto)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ruta));
        string tmp = ruta + ".tmp";
        File.WriteAllText(tmp, texto);
        if (File.Exists(ruta)) File.Replace(tmp, ruta, null);
        else File.Move(tmp, ruta);
    }
}
