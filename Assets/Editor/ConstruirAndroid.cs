using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Construye la APK de Android.
//   Menu Warrior > Android > Construir APK
//   Linea de comandos: -executeMethod ConstruirAndroid.Construir
//
// Sale en una carpeta hermana del proyecto ("Warrior_BuildAndroid"), fuera de
// Git. La primera vez tarda mucho: al cambiar a Android, Unity reimporta todo.
//
// Se firma con la clave de depuracion de Unity, que basta para instalarla a
// mano en un movil (para subirla a Google Play haria falta una clave propia).
public static class ConstruirAndroid
{
    private static string Raiz => Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "Warrior_BuildAndroid"));

    [MenuItem("Warrior/Android/Construir APK")]
    public static void Construir()
    {
        if (!Directory.Exists(Raiz)) Directory.CreateDirectory(Raiz);
        string apk = Path.Combine(Raiz, "Warrior.apk");

        // Cambiar de plataforma (la primera vez reimporta todo el proyecto).
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
        {
            Debug.Log("[Android] Cambiando a plataforma Android (esto tarda la primera vez)...");
            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
            {
                Debug.LogError("[Android] No se ha podido cambiar a Android.");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }
        }

        // APK (no AAB: esto es para instalarla a mano, no para Google Play).
        EditorUserBuildSettings.buildAppBundle = false;
        PlayerSettings.SplashScreen.show = false;
        NamedBuildTarget android = NamedBuildTarget.Android;
        if (string.IsNullOrEmpty(PlayerSettings.GetApplicationIdentifier(android))
            || PlayerSettings.GetApplicationIdentifier(android).Contains("ProductName"))
            PlayerSettings.SetApplicationIdentifier(android, "com.DefaultCompany.Warrior");

        string[] escenas = EditorBuildSettings.scenes.Where(e => e.enabled).Select(e => e.path).ToArray();
        var opciones = new BuildPlayerOptions
        {
            scenes = escenas,
            locationPathName = apk,
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = BuildOptions.None,
        };
        BuildReport r = BuildPipeline.BuildPlayer(opciones);
        if (r.summary.result != BuildResult.Succeeded)
        {
            Debug.LogError("[Android] La build ha fallado: " + r.summary.result + " (" + r.summary.totalErrors + " errores)");
            if (Application.isBatchMode) EditorApplication.Exit(1);
            return;
        }
        long peso = File.Exists(apk) ? new FileInfo(apk).Length : 0;
        Debug.Log($"[Android] APK lista: {apk} ({peso / 1048576f:0.0} MB, {r.summary.totalTime})");
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }
}
