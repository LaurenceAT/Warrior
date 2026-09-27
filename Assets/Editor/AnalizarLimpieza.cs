using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// Solo lectura: lista lo que se podria borrar al quedarse solo con la cueva y la
// nieve. No borra nada. Escribe el informe en AnalisisLimpieza.txt (raiz del proyecto).
public static class AnalizarLimpieza
{
    private static readonly string[] EscenasQueSeQuedan = { "Assets/Scenes/Nivel Cueva.unity", "Assets/Scenes/Nivel Nieve.unity" };
    private static readonly string[] EscenasViejas =
    {
        "Assets/Scenes/Nivel 1.unity", "Assets/Scenes/Nivel 2.unity", "Assets/Scenes/Nivel 3.unity", "Assets/Scenes/Nivel 4.unity",
        "Assets/Scenes/SampleScene.unity", "Assets/Scenes/Level_Prototype.unity",
    };
    // Lo que se usa sin estar enlazado en una escena (cargado por codigo o por los generadores).
    private static readonly string[] CarpetasProtegidas =
    {
        "Assets/SPRITES PARA NUEVOS NIVELES", "Assets/Sprites/PJ_Knight/1 - Adventurer 1.5 (RECOMENDADA)",
        "Assets/Resources", "Assets/Data", "Assets/Tiles", "Assets/TextMesh Pro", "Assets/Settings", "Assets/URP",
    };
    private static readonly string[] ExtImagen = { ".png", ".jpg", ".jpeg", ".psd", ".aseprite", ".gif", ".tga" };

    [MenuItem("Warrior/Analizar limpieza (solo lectura)")]
    public static void Analizar()
    {
        var usados = new HashSet<string>(AssetDatabase.GetDependencies(EscenasQueSeQuedan, true));
        // El Player (se reaparece desde su prefab) y todo lo de Resources y Data.
        var extra = new List<string> { "Assets/Prefabs/Player.prefab", "Assets/Prefabs/PlayerDeathVFX.prefab" };
        extra.AddRange(AssetDatabase.FindAssets("", new[] { "Assets/Resources", "Assets/Data" }).Select(AssetDatabase.GUIDToAssetPath));
        foreach (string d in AssetDatabase.GetDependencies(extra.ToArray(), true)) usados.Add(d);

        // Quien usa cada imagen entre los prefabs (se conservan todos).
        var prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" }).Select(AssetDatabase.GUIDToAssetPath).ToArray();
        var usoPrefab = new Dictionary<string, List<string>>();
        foreach (string p in prefabs)
            foreach (string d in AssetDatabase.GetDependencies(p, true))
            {
                if (!usoPrefab.TryGetValue(d, out var l)) usoPrefab[d] = l = new List<string>();
                l.Add(Path.GetFileNameWithoutExtension(p));
            }

        var candidatas = AssetDatabase.GetAllAssetPaths()
            .Where(a => a.StartsWith("Assets/") && ExtImagen.Contains(Path.GetExtension(a).ToLower()))
            .Where(a => !usados.Contains(a) && !CarpetasProtegidas.Any(c => a.StartsWith(c + "/")))
            .OrderBy(a => a).ToList();

        var sb = new StringBuilder();
        sb.AppendLine("== ESCENAS (punto 4) ==");
        foreach (string e in EscenasViejas)
            if (File.Exists(e)) sb.AppendLine($"{e}\t{Tam(e)}");
        sb.AppendLine();
        sb.AppendLine("== IMAGENES NO USADAS POR CUEVA/NIEVE/PLAYER (punto 5), por carpeta ==");
        foreach (var g in candidatas.GroupBy(a => Path.GetDirectoryName(a).Replace('\\', '/')).OrderBy(g => g.Key))
        {
            long bytes = g.Sum(a => new FileInfo(a).Length);
            var enPrefab = g.Where(a => usoPrefab.ContainsKey(a)).SelectMany(a => usoPrefab[a]).Distinct().OrderBy(x => x).ToList();
            sb.AppendLine($"{g.Key}\t{g.Count()} archivos\t{bytes / 1024} KB\tprefabs: {(enPrefab.Count == 0 ? "-" : string.Join(", ", enPrefab))}");
        }
        sb.AppendLine();
        sb.AppendLine($"TOTAL imagenes: {candidatas.Count} archivos, {candidatas.Sum(a => new FileInfo(a).Length) / 1024} KB");

        sb.AppendLine();
        sb.AppendLine("== OTROS ASSETS SOLO DE LOS NIVELES VIEJOS (tiles, animaciones) ==");
        var otros = AssetDatabase.GetAllAssetPaths()
            .Where(a => a.StartsWith("Assets/Tilemaps/") || a.StartsWith("Assets/Animations/"))
            .Where(a => !AssetDatabase.IsValidFolder(a) && !usados.Contains(a))
            .OrderBy(a => a).ToList();
        foreach (var g in otros.GroupBy(a => Path.GetDirectoryName(a).Replace('\\', '/')).OrderBy(g => g.Key))
        {
            var enPrefab = g.Where(a => usoPrefab.ContainsKey(a)).SelectMany(a => usoPrefab[a]).Distinct().OrderBy(x => x).ToList();
            sb.AppendLine($"{g.Key}\t{g.Count()} archivos\t{g.Sum(a => new FileInfo(a).Length) / 1024} KB\tprefabs: {(enPrefab.Count == 0 ? "-" : string.Join(", ", enPrefab))}");
        }

        sb.AppendLine();
        sb.AppendLine("== LISTA COMPLETA DE IMAGENES ==");
        foreach (string a in candidatas) sb.AppendLine(a);
        File.WriteAllText("AnalisisLimpieza.txt", sb.ToString());
        Debug.Log("[Limpieza] Informe en AnalisisLimpieza.txt: " + candidatas.Count + " imagenes.");
    }

    private static string Tam(string a) => (new FileInfo(a).Length / 1024) + " KB";
}
