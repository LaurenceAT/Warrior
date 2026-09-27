using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Borrado de lo que ya no se usa, con la lista que aprobo el usuario. Antes de
// borrar comprueba que nada de lo que se queda (cueva, nieve, player, prefabs
// que se conservan, Resources, Data) lo necesite; si algo si lo usa, no se toca
// y sale en el informe. Lo borrado va a la papelera del sistema (y sigue en git).
public static class LimpiezaAprobada
{
    private static readonly string[] Carpetas =
    {
        "Assets/Sprites/01-King Human",
        "Assets/Sprites/02-King Pig", "Assets/Sprites/03-Pig", "Assets/Sprites/04-Pig Throwing a Box",
        "Assets/Sprites/05-Pig Thowing a Bomb", "Assets/Sprites/06-Pig Hide in the Box", "Assets/Sprites/07-Pig With a Match",
        "Assets/Sprites/08-Box", "Assets/Sprites/09-Bomb", "Assets/Sprites/10-Cannon", "Assets/Sprites/11-Door",
        "Assets/Sprites/13-Dialogue Boxes", "Assets/Sprites/14-TileSets",
        "Assets/Sprites/12-Live and Coins",
        "Assets/Tilemaps",
        "Assets/Animations/Door", "Assets/Animations/Enemy", "Assets/Animations/Flag", "Assets/Animations/Items",
    };

    private static readonly string[] Archivos =
    {
        "Assets/Prefabs/Enemies/Enemy melee.prefab",
        "Assets/Prefabs/Checkpoint.prefab", "Assets/Prefabs/Diamond.prefab", "Assets/Prefabs/Items/Heart.prefab",
        "Assets/Sprites/NuevosSprites/FlagFlying.png", "Assets/Sprites/NuevosSprites/FlagIdle.png", "Assets/Sprites/NuevosSprites/FlagUp.png",
        "Assets/Sprites/NuevosSprites/Big Diamond Idle Blue.png", "Assets/Sprites/NuevosSprites/Big Diamond Idle Green.png",
        "Assets/Sprites/NuevosSprites/Big Diamond Idle Red.png", "Assets/Sprites/NuevosSprites/Big Diamond Idle Yellow.png",
    };

    // Ademas: las animaciones sueltas de Assets/Animations (raiz y Player) que no
    // use nada de lo que se queda. Las de Traps no se tocan.

    [MenuItem("Warrior/Borrar lo aprobado (limpieza)")]
    public static void Borrar()
    {
        var borrar = new HashSet<string>(Archivos.Where(File.Exists));
        foreach (string c in Carpetas.Where(AssetDatabase.IsValidFolder))
            foreach (string a in AssetDatabase.FindAssets("", new[] { c }).Select(AssetDatabase.GUIDToAssetPath))
                if (!AssetDatabase.IsValidFolder(a)) borrar.Add(a);

        HashSet<string> seQueda = Necesario(borrar);

        foreach (string a in AssetDatabase.FindAssets("", new[] { "Assets/Animations" }).Select(AssetDatabase.GUIDToAssetPath))
        {
            if (AssetDatabase.IsValidFolder(a) || a.StartsWith("Assets/Animations/Traps/")) continue;
            string dir = Path.GetDirectoryName(a).Replace('\\', '/');
            if ((dir == "Assets/Animations" || dir == "Assets/Animations/Player") && !seQueda.Contains(a)) borrar.Add(a);
        }

        var informe = new List<string>();
        var bloqueados = borrar.Where(seQueda.Contains).OrderBy(a => a).ToList();
        foreach (string a in bloqueados) informe.Add("NO BORRADO (lo usa algo que se queda): " + a);
        var fallidos = new List<string>();
        var lista = borrar.Except(bloqueados).OrderBy(a => a).ToList();
        foreach (string a in lista)
            if (!AssetDatabase.MoveAssetToTrash(a)) fallidos.Add(a);
        foreach (string c in Carpetas.Where(AssetDatabase.IsValidFolder))
            if (AssetDatabase.FindAssets("", new[] { c }).All(g => AssetDatabase.IsValidFolder(AssetDatabase.GUIDToAssetPath(g))))
                AssetDatabase.MoveAssetToTrash(c);
        AssetDatabase.Refresh();

        informe.Insert(0, $"Borrados {lista.Count - fallidos.Count} archivos. Bloqueados {bloqueados.Count}. Fallidos {fallidos.Count}.");
        informe.AddRange(fallidos.Select(f => "FALLO: " + f));
        informe.Add("");
        informe.AddRange(lista.Except(fallidos).Select(a => "borrado: " + a));
        File.WriteAllLines("LimpiezaHecha.txt", informe);
        Debug.Log("[Limpieza] " + informe[0] + " Detalle en LimpiezaHecha.txt");
    }

    // Todo lo que necesitan la cueva, la nieve, el player, Resources, Data y los
    // prefabs que se conservan (todos menos los que se van a borrar).
    private static HashSet<string> Necesario(HashSet<string> borrar)
    {
        var raices = new List<string> { ConfigNivelEditor.EscenaCueva, ConfigNivelEditor.EscenaNieve };
        raices.AddRange(AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" }).Select(AssetDatabase.GUIDToAssetPath).Where(p => !borrar.Contains(p)));
        raices.AddRange(AssetDatabase.FindAssets("", new[] { "Assets/Resources", "Assets/Data", "Assets/Tiles" }).Select(AssetDatabase.GUIDToAssetPath));
        var r = new HashSet<string>(AssetDatabase.GetDependencies(raices.ToArray(), true));
        // Lo que se usa por su ruta en los generadores del Player.
        r.Add("Assets/Animations/Player/Player.controller");
        return r;
    }
}
