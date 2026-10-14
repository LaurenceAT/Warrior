using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Construye la version web (itch.io) y cuenta que pesa mas.
//   Menu Warrior > Web > Construir version web
//   Linea de comandos: -executeMethod ConstruirWeb.Construir
// La build sale en una carpeta hermana del proyecto ("Warrior_BuildWeb"), fuera
// de Git. Despues de construir, el informe de pesos queda en el log y en
// Warrior_BuildWeb/_informe_pesos.txt (no se sube a itch: el ZIP se hace de la
// carpeta "Warrior_BuildWeb/web").
public static class ConstruirWeb
{
    private static string Raiz => Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "Warrior_BuildWeb"));

    [MenuItem("Warrior/Web/Construir version web")]
    public static void Construir()
    {
        string carpeta = Path.Combine(Raiz, "web");
        if (Directory.Exists(carpeta)) Directory.Delete(carpeta, true);
        Directory.CreateDirectory(carpeta);

        // Ajustes para itch.io: gzip con alternativa por si el servidor no lo sirve
        // comprimido, sin pantalla de Unity, y sin guardar el estado en el depurador.
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.SplashScreen.show = false;
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
        PlayerSettings.WebGL.dataCaching = true;

        string[] escenas = EditorBuildSettings.scenes.Where(e => e.enabled).Select(e => e.path).ToArray();
        var opciones = new BuildPlayerOptions
        {
            scenes = escenas,
            locationPathName = carpeta,
            target = BuildTarget.WebGL,
            options = BuildOptions.None,
        };
        BuildReport r = BuildPipeline.BuildPlayer(opciones);
        Informe(r, carpeta);
        if (r.summary.result != BuildResult.Succeeded) { Debug.LogError("[Web] La build ha fallado: " + r.summary.result); if (Application.isBatchMode) EditorApplication.Exit(1); return; }
        Debug.Log($"[Web] Build lista en {carpeta} ({r.summary.totalSize / 1048576f:0.0} MB)");
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    private static void Informe(BuildReport r, string carpeta)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Resultado: {r.summary.result}, tamano {r.summary.totalSize / 1048576f:0.0} MB, tiempo {r.summary.totalTime}");
        long total = 0, archivos = 0;
        foreach (string f in Directory.Exists(carpeta) ? Directory.GetFiles(carpeta, "*", SearchOption.AllDirectories) : new string[0])
        {
            total += new FileInfo(f).Length;
            archivos++;
            if (new FileInfo(f).Length > 2 * 1048576) sb.AppendLine($"  archivo: {Path.GetFileName(f)} {new FileInfo(f).Length / 1048576f:0.0} MB");
        }
        sb.AppendLine($"Carpeta web: {archivos} archivos, {total / 1048576f:0.0} MB");
        sb.AppendLine();
        sb.AppendLine("Lo que mas pesa dentro de la build (MB, ya importado):");
        var filas = r.packedAssets.SelectMany(p => p.contents).OrderByDescending(c => c.packedSize).Take(60);
        foreach (var c in filas) sb.AppendLine($"  {c.packedSize / 1048576f,7:0.00}  {c.type?.Name,-16} {c.sourceAssetPath}");
        sb.AppendLine();
        sb.AppendLine("Por tipo (MB):");
        foreach (var g in r.packedAssets.SelectMany(p => p.contents).GroupBy(c => c.type?.Name ?? "?").OrderByDescending(g => g.Sum(c => (long)c.packedSize)).Take(10))
            sb.AppendLine($"  {g.Sum(c => (long)c.packedSize) / 1048576f,7:0.0}  {g.Key}");
        Directory.CreateDirectory(Raiz);
        File.WriteAllText(Path.Combine(Raiz, "_informe_pesos.txt"), sb.ToString());
        Debug.Log("[Web] " + sb);
    }
}
