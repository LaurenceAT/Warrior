using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Tilemaps;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// Paletas de decoracion para pintar adornos (pinos, rocas, plantas...) con la
// Tile Palette, igual que el suelo. Los tiles salen de los adornos que ya hay en
// cada nivel, con su tamano y su color, apoyados en el suelo de la casilla (las
// estalactitas cuelgan del techo). Se pintan en dos Tilemaps nuevos sin colision:
//   - "Decoracion": delante del fondo y detras del personaje.
//   - "DecoracionFondo": mas atras, para los arboles lejanos y oscuros.
// Tambien rellena la Cueva - Roca (roca y sombra).
public static class PaletasDecoracion
{
    public static void Todo()
    {
        Rellenar("Assets/Tiles/Cueva/Cueva - Roca.prefab",
                 (AssetDatabase.LoadAssetAtPath<TileBase>("Assets/Tiles/Cueva/Roca cueva.asset"), new Vector3Int(0, 0, 0)),
                 (AssetDatabase.LoadAssetAtPath<TileBase>("Assets/Tiles/Comun/Sombra.asset"), new Vector3Int(2, 0, 0)));
        Nivel(ConfigNivelEditor.EscenaNieve, "Nieve");
        Nivel(ConfigNivelEditor.EscenaCueva, "Cueva");
        AssetDatabase.SaveAssets();
    }

    private static void Nivel(string rutaEscena, string nombre)
    {
        Scene e = EditorSceneManager.OpenScene(rutaEscena);
        GameObject nivel = e.GetRootGameObjects().First(r => r.name == "Nivel");
        Transform deco = nivel.transform.Find("Decoracion");
        Grid grid = nivel.GetComponentInChildren<Grid>();
        string carpeta = $"Assets/Tiles/{nombre}/Decoracion";
        if (!AssetDatabase.IsValidFolder(carpeta)) AssetDatabase.CreateFolder($"Assets/Tiles/{nombre}", "Decoracion");

        // Un tile por imagen (y capa), con el tamano mas repetido de sus copias.
        var grupos = new Dictionary<string, List<SpriteRenderer>>();
        if (deco != null)
            foreach (SpriteRenderer sr in deco.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (sr.sprite == null) continue;
                string clave = sr.sprite.name + "|" + sr.sortingLayerName + "|" + Limpio(sr.name);
                if (!grupos.TryGetValue(clave, out var l)) grupos[clave] = l = new List<SpriteRenderer>();
                l.Add(sr);
            }

        var delante = new List<(Tile, float)>();
        var fondo = new List<(Tile, float)>();
        var usados = new HashSet<string>();
        foreach (var g in grupos.OrderBy(k => k.Key))
        {
            SpriteRenderer mediano = g.Value.OrderBy(s => s.bounds.size.y).ElementAt(g.Value.Count / 2);
            string nombreTile = Limpio(mediano.name);
            int n = 1;
            while (!usados.Add(nombreTile + (n > 1 ? " " + n : ""))) n++;
            if (n > 1) nombreTile += " " + n;
            Tile t = TileDe(mediano, $"{carpeta}/{nombreTile}.asset");
            float ancho = mediano.bounds.size.x;
            if (mediano.sortingLayerName == "Background") fondo.Add((t, ancho)); else delante.Add((t, ancho));
        }

        Tilemap tDelante = Capa(grid, "Decoracion", "Middleground", 1);
        Tilemap tFondo = Capa(grid, "DecoracionFondo", "Background", 20);
        EditorSceneManager.MarkSceneDirty(e);
        EditorSceneManager.SaveScene(e);

        // La paleta: una fila con los de delante y otra, debajo, con los de fondo.
        string rutaPaleta = $"Assets/Tiles/{nombre}/{nombre} - Decoracion.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(rutaPaleta) == null)
            GridPaletteUtility.CreateNewPalette($"Assets/Tiles/{nombre}", nombre + " - Decoracion", GridLayout.CellLayout.Rectangle,
                                               GridPalette.CellSizing.Automatic, Vector3.one, GridLayout.CellSwizzle.XYZ);
        var celdas = new List<(TileBase, Vector3Int)>();
        int x = 0;
        foreach (var (t, ancho) in delante) { celdas.Add((t, new Vector3Int(x, 0, 0))); x += Mathf.CeilToInt(ancho) + 1; }
        x = 0;
        foreach (var (t, ancho) in fondo) { celdas.Add((t, new Vector3Int(x, -10, 0))); x += Mathf.CeilToInt(ancho) + 1; }
        Rellenar(rutaPaleta, celdas.ToArray());
        Debug.Log($"[Decoracion] {nombre}: {delante.Count} adornos, {fondo.Count} de fondo. Capas {tDelante.name} y {tFondo.name}.");
    }

    // "Pino (3)" -> "Pino".
    private static string Limpio(string n)
    {
        int i = n.IndexOf(" (");
        return (i > 0 ? n.Substring(0, i) : n).Trim();
    }

    // Tile con la imagen, el color y el tamano de ese adorno, apoyado en el suelo
    // de la casilla (o colgado del techo, si es una estalactita).
    private static Tile TileDe(SpriteRenderer sr, string ruta)
    {
        Tile t = AssetDatabase.LoadAssetAtPath<Tile>(ruta);
        if (t == null)
        {
            t = ScriptableObject.CreateInstance<Tile>();
            AssetDatabase.CreateAsset(t, ruta);
        }
        Sprite s = sr.sprite;
        Vector3 esc = sr.transform.lossyScale;
        float sx = Mathf.Abs(esc.x) * (sr.flipX ? -1f : 1f), sy = Mathf.Abs(esc.y) * (sr.flipY ? -1f : 1f);
        Bounds b = s.bounds;
        bool colgada = sr.name.Contains("Estalactita") || sr.flipY;
        float oy = colgada ? 0.5f - b.max.y * Mathf.Abs(sy) : -0.5f - b.min.y * Mathf.Abs(sy);
        float ox = -b.center.x * Mathf.Abs(sx);
        t.sprite = s;
        t.color = sr.color;
        t.colliderType = Tile.ColliderType.None;
        t.flags = TileFlags.LockColor | TileFlags.LockTransform;
        t.transform = Matrix4x4.TRS(new Vector3(ox, oy, 0f), Quaternion.identity, new Vector3(sx, sy, 1f));
        EditorUtility.SetDirty(t);
        return t;
    }

    private static Tilemap Capa(Grid grid, string nombre, string capa, int orden)
    {
        Tilemap t = grid.GetComponentsInChildren<Tilemap>(true).FirstOrDefault(x => x.name == nombre);
        if (t != null) return t;
        GameObject go = new GameObject(nombre, typeof(Tilemap), typeof(TilemapRenderer));
        go.transform.SetParent(grid.transform, false);
        TilemapRenderer r = go.GetComponent<TilemapRenderer>();
        r.sortingLayerName = capa;
        r.sortingOrder = orden;
        // Individual: cada adorno se ordena y se ve entero aunque sea mas grande que la casilla.
        r.mode = TilemapRenderer.Mode.Individual;
        return go.GetComponent<Tilemap>();
    }

    private static void Rellenar(string ruta, params (TileBase tile, Vector3Int celda)[] tiles)
    {
        GameObject contenido = PrefabUtility.LoadPrefabContents(ruta);
        try
        {
            Tilemap tm = contenido.GetComponentInChildren<Tilemap>();
            tm.ClearAllTiles();
            foreach (var (tile, celda) in tiles) if (tile != null) tm.SetTile(celda, tile);
            PrefabUtility.SaveAsPrefabAsset(contenido, ruta);
            Debug.Log($"[Decoracion] Paleta {ruta}: {tiles.Count(x => x.tile != null)} tiles.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contenido);
        }
    }
}
