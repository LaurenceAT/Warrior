using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Player;
using UnityEngine;

// Ronda 16: codigos secretos y comprobacion de la build.
//   - Resources/Desafios/Codigos: la ficha de codigos (si no existe) con
//     "warrior" -> desbloquear a The Blind Huntress.
//   - ComprobarBuild: compila los scripts como en una version final (sin
//     UNITY_EDITOR) y comprueba que no quedan las herramientas del Editor.
// Warrior > Actualizar > Ronda 16 (o Ronda16.Todo / Ronda16.ComprobarBuild por linea de comandos).
public static class Ronda16
{
    private const string RutaCodigos = "Assets/Resources/Desafios/Codigos.asset";

    [MenuItem("Warrior/Actualizar/Ronda 16 (códigos secretos)")]
    public static void Todo()
    {
        if (AssetDatabase.LoadAssetAtPath<CodigosSecretos>(RutaCodigos) == null)
        {
            CodigosSecretos c = ScriptableObject.CreateInstance<CodigosSecretos>();
            c.codigos = new List<CodigosSecretos.Codigo>
            {
                new CodigosSecretos.Codigo { texto = "warrior", accion = CodigosSecretos.Accion.DesbloquearJefeSecreto, parametro = "blind_huntress" },
            };
            AssetDatabase.CreateAsset(c, RutaCodigos);
            Debug.Log("[Ronda16] Creada la ficha de codigos.");
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[Ronda16] Listo.");
    }

    [MenuItem("Warrior/Comprobar build (sin herramientas de Editor)")]
    public static void ComprobarBuild()
    {
        string salida = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "ComprobarBuild"));
        if (Directory.Exists(salida)) Directory.Delete(salida, true);
        Directory.CreateDirectory(salida);
        var ajustes = new ScriptCompilationSettings
        {
            target = BuildTarget.StandaloneWindows64,
            group = BuildTargetGroup.Standalone,
            options = ScriptCompilationOptions.None,
        };
        ScriptCompilationResult r = PlayerBuildInterface.CompilePlayerScripts(ajustes, salida);
        string dll = Path.Combine(salida, "Assembly-CSharp.dll");
        bool compila = r.assemblies != null && r.assemblies.Contains("Assembly-CSharp.dll") && File.Exists(dll);
        Debug.Log($"[Build] compila={compila} ensamblados={r.assemblies?.Count}");
        if (compila)
        {
            string texto = System.Text.Encoding.ASCII.GetString(File.ReadAllBytes(dll));
            foreach (string prohibido in new[] { "ProgresoJuego", "EtiquetaPerfilPruebas", "DepuracionBitacora", "EditorPrefs", "UnityEditor" })
                Debug.Log($"[Build] '{prohibido}' en la build: {(texto.Contains(prohibido) ? "SI (MAL)" : "no")}");
            Debug.Log($"[Build] RegistroGuardado presente: {texto.Contains("RegistroGuardado")}  CodigosSecretos presente: {texto.Contains("CodigosSecretos")}");
        }
        if (Application.isBatchMode) EditorApplication.Exit(compila ? 0 : 1);
    }
}
