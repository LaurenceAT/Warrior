using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Herramientas > Progreso del juego (solo Editor). Trabajan sobre todo lo que
// esta en RegistroGuardado, en el perfil activo (el real o "Simular jugador nuevo"):
//   - Borrar TODO el progreso (partida, desafios, logros, fichas de jefe,
//     Bestiario, jefe secreto y codigos). Las opciones se quedan.
//   - Borrar solo una parte (Partida, Desafios, Logros, Bestiario, Opciones).
//   - Restaurar el ultimo respaldo.
//   - Abrir la carpeta del progreso (y la de respaldos).
//   - Simular jugador nuevo: el juego usa otra carpeta y otras claves (vacias).
// Antes de borrar se hace un respaldo con fecha en "_RespaldoProgreso" (en la
// raiz del proyecto, fuera de Assets e ignorado por Git). Si un borrado se corta
// a medias, al abrir Unity se ofrece restaurar ese respaldo.
[InitializeOnLoad]
public static class ProgresoJuego
{
    private const string Menu = "Herramientas/Progreso del juego/";
    private const string Pendiente = "borrado_en_curso.txt";
    private const string AntesDeRestaurar = "antes-de-restaurar";

    public static string CarpetaRespaldos => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "_RespaldoProgreso"));

    static ProgresoJuego()
    {
        EditorApplication.delayCall += RevisarInterrumpido;
    }

    // ------------------------------------------------------------------ Menu

    [MenuItem(Menu + "Borrar TODO el progreso", false, 1)]
    private static void BorrarTodo()
    {
        if (!Detenido()) return;
        var cats = new[] { CategoriaGuardado.Partida, CategoriaGuardado.Desafios, CategoriaGuardado.Logros, CategoriaGuardado.Bestiario };
        if (!EditorUtility.DisplayDialog("Borrar TODO el progreso",
                $"Perfil: {NombrePerfil}\n\nSe borrarán las partidas, los desafíos completados y sus tiempos, los logros, las fichas de jefe, " +
                "el Bestiario, el jefe secreto y lo desbloqueado con códigos.\n\nLas opciones (volumen y brillo) se quedan.\n\n" +
                "Antes se guarda un respaldo con la fecha.", "Borrar todo", "Cancelar")) return;
        Borrar(cats, "todo");
    }

    [MenuItem(Menu + "Borrar solo una parte/Partida", false, 20)] private static void BorrarPartida() => BorrarUna(CategoriaGuardado.Partida);
    [MenuItem(Menu + "Borrar solo una parte/Desafíos (y jefe secreto, códigos y fichas de jefe)", false, 21)] private static void BorrarDesafios() => BorrarUna(CategoriaGuardado.Desafios);
    [MenuItem(Menu + "Borrar solo una parte/Logros", false, 22)] private static void BorrarLogros() => BorrarUna(CategoriaGuardado.Logros);
    [MenuItem(Menu + "Borrar solo una parte/Bestiario", false, 23)] private static void BorrarBestiario() => BorrarUna(CategoriaGuardado.Bestiario);
    [MenuItem(Menu + "Borrar solo una parte/Opciones (volumen y brillo)", false, 24)] private static void BorrarOpciones() => BorrarUna(CategoriaGuardado.Opciones);

    private static void BorrarUna(CategoriaGuardado c)
    {
        if (!Detenido()) return;
        string que = string.Join("\n", RegistroGuardado.De(c).Select(d => "- " + d.descripcion));
        if (!EditorUtility.DisplayDialog($"Borrar {c}", $"Perfil: {NombrePerfil}\n\nSe borrará:\n{que}\n\nAntes se guarda un respaldo con la fecha.", "Borrar", "Cancelar")) return;
        Borrar(new[] { c }, c.ToString().ToLowerInvariant());
    }

    [MenuItem(Menu + "Restaurar el último respaldo", false, 40)]
    private static void RestaurarUltimo()
    {
        if (!Detenido()) return;
        string r = UltimoRespaldo(RegistroGuardado.PerfilPruebas);
        if (r == null) { EditorUtility.DisplayDialog("Restaurar", $"No hay respaldos del perfil {NombrePerfil}.", "Vale"); return; }
        if (!EditorUtility.DisplayDialog("Restaurar el último respaldo",
                $"Perfil: {NombrePerfil}\nRespaldo: {Path.GetFileName(r)}\n\nTu progreso actual se sustituye por el del respaldo (antes se guarda una copia de lo actual).",
                "Restaurar", "Cancelar")) return;
        Respaldar(AntesDeRestaurar);
        Restaurar(r);
        EditorUtility.DisplayDialog("Restaurar", "Progreso restaurado: " + Path.GetFileName(r), "Vale");
    }

    [MenuItem(Menu + "Abrir la carpeta del progreso", false, 60)]
    private static void AbrirCarpeta()
    {
        Directory.CreateDirectory(RegistroGuardado.Carpeta);
        EditorUtility.RevealInFinder(Path.Combine(RegistroGuardado.Carpeta, "."));
    }

    [MenuItem(Menu + "Abrir la carpeta de respaldos", false, 61)]
    private static void AbrirRespaldos()
    {
        Directory.CreateDirectory(CarpetaRespaldos);
        EditorUtility.RevealInFinder(Path.Combine(CarpetaRespaldos, "."));
    }

    private const string MenuPerfil = Menu + "Simular jugador nuevo";

    [MenuItem(MenuPerfil, false, 80)]
    private static void CambiarPerfil()
    {
        if (!Detenido()) return;
        bool activar = !RegistroGuardado.PerfilPruebas;
        if (activar)
        {
            // Empieza vacio cada vez: es un jugador nuevo (sus datos son de usar y tirar).
            BorrarPerfilPruebas();
        }
        EditorPrefs.SetBool(RegistroGuardado.ClaveEditorPerfil, activar);
        Globales.Recargar();
        Debug.Log(activar ? "[Progreso] Simular jugador nuevo: ACTIVADO (perfil de pruebas vacío; tu progreso real no se toca)."
                          : "[Progreso] Simular jugador nuevo: desactivado (vuelve tu progreso real).");
    }

    [MenuItem(MenuPerfil, true)]
    private static bool CambiarPerfilValidar()
    {
        UnityEditor.Menu.SetChecked(MenuPerfil, RegistroGuardado.PerfilPruebas);
        return true;
    }

    // ------------------------------------------------------------------ Borrar

    public static string NombrePerfil => RegistroGuardado.PerfilPruebas ? "PRUEBAS (Simular jugador nuevo)" : "real";

    private static bool Detenido()
    {
        if (!EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode) return true;
        EditorUtility.DisplayDialog("Progreso del juego", "El juego está en ejecución. Detenlo primero (botón Play) y vuelve a intentarlo.", "Vale");
        return false;
    }

    // Respaldo, marca de "en curso", borrado y fin. Publico para las pruebas.
    public static string Borrar(CategoriaGuardado[] cats, string etiqueta)
    {
        string respaldo = Respaldar(etiqueta);
        File.WriteAllText(Path.Combine(CarpetaRespaldos, Pendiente), respaldo);
        Globales.Recargar();
        foreach (CategoriaGuardado c in cats)
            foreach (RegistroGuardado.Dato d in RegistroGuardado.De(c))
            {
                foreach (RegistroGuardado.ClavePrefs p in d.prefs) PlayerPrefs.DeleteKey(RegistroGuardado.Clave(p.nombre));
                if (d.enGlobales) Globales.Borrar(c);
                else if (d.archivos != null) foreach (string f in Archivos(d.archivos)) File.Delete(f);
            }
        PlayerPrefs.Save();
        Globales.Recargar();
        File.Delete(Path.Combine(CarpetaRespaldos, Pendiente));
        Debug.Log($"[Progreso] Borrado ({string.Join(", ", cats)}) en el perfil {NombrePerfil}. Respaldo: {respaldo}");
        return respaldo;
    }

    private static IEnumerable<string> Archivos(string patron)
    {
        string carpeta = RegistroGuardado.Carpeta;
        return Directory.Exists(carpeta) ? Directory.GetFiles(carpeta, patron) : new string[0];
    }

    private static void BorrarPerfilPruebas()
    {
        string carpeta = RegistroGuardado.CarpetaDe(true);
        if (Directory.Exists(carpeta)) Directory.Delete(carpeta, true);
        foreach (RegistroGuardado.Dato d in RegistroGuardado.Datos)
            foreach (RegistroGuardado.ClavePrefs p in d.prefs)
            {
                string k = RegistroGuardado.ClaveDe(p.nombre, true);
                if (k.StartsWith(RegistroGuardado.PrefijoPruebas)) PlayerPrefs.DeleteKey(k);
            }
        PlayerPrefs.Save();
    }

    // ------------------------------------------------------------------ Respaldos

    [Serializable] private class ValorPrefs { public string clave; public int tipo; public string valor; }
    [Serializable] private class Prefs { public List<ValorPrefs> valores = new List<ValorPrefs>(); }

    // Copia todo lo registrado (archivos y PlayerPrefs) del perfil activo.
    public static string Respaldar(string etiqueta)
    {
        string nombre = $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss}_{(RegistroGuardado.PerfilPruebas ? "pruebas" : "real")}_{etiqueta}";
        string dir = Path.Combine(CarpetaRespaldos, nombre);
        for (int i = 2; Directory.Exists(dir); i++) dir = Path.Combine(CarpetaRespaldos, nombre + "_" + i);
        Directory.CreateDirectory(dir);
        foreach (string patron in RegistroGuardado.Datos.Where(d => d.archivos != null).Select(d => d.archivos).Distinct())
            foreach (string f in Archivos(patron)) File.Copy(f, Path.Combine(dir, Path.GetFileName(f)), true);
        Prefs pr = new Prefs();
        foreach (RegistroGuardado.ClavePrefs p in RegistroGuardado.Datos.SelectMany(d => d.prefs))
        {
            string k = RegistroGuardado.Clave(p.nombre);
            if (!PlayerPrefs.HasKey(k)) continue;
            string v = p.tipo == RegistroGuardado.Tipo.Int ? PlayerPrefs.GetInt(k).ToString()
                     : p.tipo == RegistroGuardado.Tipo.Float ? PlayerPrefs.GetFloat(k).ToString("R", System.Globalization.CultureInfo.InvariantCulture)
                     : PlayerPrefs.GetString(k);
            pr.valores.Add(new ValorPrefs { clave = p.nombre, tipo = (int)p.tipo, valor = v });
        }
        File.WriteAllText(Path.Combine(dir, "prefs.json"), JsonUtility.ToJson(pr, true));
        AsegurarGitignore();
        return dir;
    }

    public static string UltimoRespaldo(bool perfilPruebas)
    {
        if (!Directory.Exists(CarpetaRespaldos)) return null;
        string tag = perfilPruebas ? "_pruebas_" : "_real_";
        return Directory.GetDirectories(CarpetaRespaldos)
            .Where(d => Path.GetFileName(d).Contains(tag) && !Path.GetFileName(d).Contains(AntesDeRestaurar))
            .OrderByDescending(d => Directory.GetCreationTimeUtc(d)).ThenByDescending(d => Path.GetFileName(d)).FirstOrDefault();
    }

    // Deja el perfil activo exactamente como estaba en el respaldo.
    public static void Restaurar(string dir)
    {
        Globales.Recargar();
        foreach (string patron in RegistroGuardado.Datos.Where(d => d.archivos != null).Select(d => d.archivos).Distinct())
            foreach (string f in Archivos(patron)) File.Delete(f);
        foreach (RegistroGuardado.ClavePrefs p in RegistroGuardado.Datos.SelectMany(d => d.prefs)) PlayerPrefs.DeleteKey(RegistroGuardado.Clave(p.nombre));
        Directory.CreateDirectory(RegistroGuardado.Carpeta);
        foreach (string f in Directory.GetFiles(dir).Where(f => Path.GetFileName(f) != "prefs.json"))
            File.Copy(f, Path.Combine(RegistroGuardado.Carpeta, Path.GetFileName(f)), true);
        string pj = Path.Combine(dir, "prefs.json");
        if (File.Exists(pj))
            foreach (ValorPrefs v in JsonUtility.FromJson<Prefs>(File.ReadAllText(pj)).valores)
            {
                string k = RegistroGuardado.Clave(v.clave);
                switch ((RegistroGuardado.Tipo)v.tipo)
                {
                    case RegistroGuardado.Tipo.Int: PlayerPrefs.SetInt(k, int.Parse(v.valor)); break;
                    case RegistroGuardado.Tipo.Float: PlayerPrefs.SetFloat(k, float.Parse(v.valor, System.Globalization.CultureInfo.InvariantCulture)); break;
                    default: PlayerPrefs.SetString(k, v.valor); break;
                }
            }
        PlayerPrefs.Save();
        Globales.Recargar();
        Debug.Log($"[Progreso] Restaurado {Path.GetFileName(dir)} en el perfil {NombrePerfil}.");
    }

    // Un borrado que no llego al final (Unity se cerro, un error...): se ofrece
    // volver al respaldo que se hizo justo antes.
    private static void RevisarInterrumpido()
    {
        string marca = Path.Combine(CarpetaRespaldos, Pendiente);
        if (!File.Exists(marca) || Application.isBatchMode) return;
        string dir = File.ReadAllText(marca).Trim();
        if (Directory.Exists(dir) && EditorUtility.DisplayDialog("Progreso del juego",
                $"Un borrado del progreso se interrumpió a medias.\n\n¿Restaurar el respaldo que se hizo justo antes ({Path.GetFileName(dir)})?", "Restaurar", "No"))
            Restaurar(dir);
        File.Delete(marca);
    }

    private static void AsegurarGitignore()
    {
        string gi = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".gitignore"));
        if (!File.Exists(gi)) return;
        string t = File.ReadAllText(gi);
        if (!t.Contains("/_RespaldoProgreso/")) File.AppendAllText(gi, "\n# Respaldos del progreso (Herramientas > Progreso del juego)\n/_RespaldoProgreso/\n");
    }
}
