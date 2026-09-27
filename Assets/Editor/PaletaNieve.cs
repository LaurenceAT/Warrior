using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Tilemaps;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// Deja el nivel nevado listo para pintarlo a mano con la Tile Palette:
//   - Tiles automaticos "Nieve (auto)" y "Hielo (auto)" (eligen solos la esquina,
//     el borde o el centro) y una paleta "Paleta Nieve" con ellos y las 18 piezas.
//   - El Tilemap del suelo pone su propia colision: lo que se pinte se puede pisar
//     y lo que se borre deja de estar (antes la colision eran cajas aparte).
// Warrior > Preparar nivel Nieve para pintar.
public static class PaletaNieve
{
    private const string Carpeta = "Assets/Tiles/Nieve";
    public const string RutaPaleta = Carpeta + "/Paleta Nieve.prefab";
    private static readonly string[] Piezas = { "esq_ai", "arriba", "esq_ad", "izq", "centro", "der", "esq_bi", "abajo", "esq_bd" };

    [MenuItem("Warrior/Preparar nivel Nieve para pintar")]
    public static void Preparar()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Scene e = SceneManager.GetActiveScene().path == ConfigNivelEditor.EscenaNieve
            ? SceneManager.GetActiveScene() : EditorSceneManager.OpenScene(ConfigNivelEditor.EscenaNieve);
        ConvertirEscena(e);
        CrearPaleta();
        EditorSceneManager.MarkSceneDirty(e);
        EditorSceneManager.SaveScene(e);
    }

    // La escena ya generada: colision en el Tilemap y fuera las cajas viejas.
    public static void ConvertirEscena(Scene e)
    {
        foreach (string conj in new[] { "nieve", "hielo" })
            foreach (string p in Piezas)
            {
                Tile t = AssetDatabase.LoadAssetAtPath<Tile>($"{Carpeta}/{conj}_{p}.asset");
                if (t == null || t.colliderType == Tile.ColliderType.Grid) continue;
                t.colliderType = Tile.ColliderType.Grid;
                EditorUtility.SetDirty(t);
            }

        GameObject nivel = e.GetRootGameObjects().FirstOrDefault(r => r.name == "Nivel");
        if (nivel == null) { Debug.LogWarning("[Paleta] No hay objeto Nivel"); return; }
        Tilemap tm = nivel.GetComponentsInChildren<Tilemap>(true).FirstOrDefault(t => t.name == "Suelo");
        if (tm != null)
        {
            PonerColisionTilemap(tm, LayerMask.NameToLayer("Ground"));
            // El Tilemap guarda por casilla el tipo de colision que tenia el tile al
            // pintarse (ninguna): se cambia casilla a casilla.
            int n = 0;
            foreach (Vector3Int c in tm.cellBounds.allPositionsWithin)
                if (tm.HasTile(c)) { tm.SetColliderType(c, Tile.ColliderType.Grid); n++; }
            tm.RefreshAllTiles();
            ActualizarColision(tm);
            Debug.Log($"[Paleta] Colision puesta en {n} casillas.");
        }

        Transform colis = nivel.transform.Find("Colisiones");
        if (colis != null)
        {
            for (int i = colis.childCount - 1; i >= 0; i--)
            {
                Transform c = colis.GetChild(i);
                if (c.name == "Roca") { Object.DestroyImmediate(c.gameObject); continue; }
                // El hielo se queda solo con sus brillos.
                foreach (BoxCollider2D b in c.GetComponents<BoxCollider2D>()) Object.DestroyImmediate(b);
                c.gameObject.layer = 0;
            }
            colis.name = "BrillosHielo";
        }
        Debug.Log("[Paleta] Escena convertida: colision en el Tilemap.");
    }

    public static void PonerColisionTilemap(Tilemap tm, int capa)
    {
        tm.gameObject.layer = capa;
        Rigidbody2D rb = tm.GetComponent<Rigidbody2D>();
        if (rb == null) rb = tm.gameObject.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Static;
        TilemapCollider2D tc = tm.GetComponent<TilemapCollider2D>();
        if (tc == null) tc = tm.gameObject.AddComponent<TilemapCollider2D>();
        tc.compositeOperation = Collider2D.CompositeOperation.Merge;
        CompositeCollider2D cc = tm.GetComponent<CompositeCollider2D>();
        if (cc == null) cc = tm.gameObject.AddComponent<CompositeCollider2D>();
        cc.geometryType = CompositeCollider2D.GeometryType.Polygons;
        cc.generationType = CompositeCollider2D.GenerationType.Synchronous;
        ActualizarColision(tm);
    }

    // Rehace la forma de la colision con lo que haya pintado ahora mismo (desde
    // codigo no se rehace sola hasta el siguiente fotograma del editor).
    public static void ActualizarColision(Tilemap tm)
    {
        TilemapCollider2D tc = tm.GetComponent<TilemapCollider2D>();
        if (tc != null) tc.ProcessTilemapChanges();
        CompositeCollider2D cc = tm.GetComponent<CompositeCollider2D>();
        if (cc != null) cc.GenerateGeometry();
    }

    // ------------------------------------------------------------------ Tiles y paleta

    public static void CrearPaleta()
    {
        TileTerreno nieve = TileAuto("Nieve (auto)", "nieve", false);
        TileTerreno hielo = TileAuto("Hielo (auto)", "hielo", true);

        GameObject paleta = AssetDatabase.LoadAssetAtPath<GameObject>(RutaPaleta);
        if (paleta == null)
            paleta = GridPaletteUtility.CreateNewPalette(Carpeta, "Paleta Nieve", GridLayout.CellLayout.Rectangle,
                                                         GridPalette.CellSizing.Automatic, Vector3.one, GridLayout.CellSwizzle.XYZ);
        string ruta = AssetDatabase.GetAssetPath(paleta);
        GameObject contenido = PrefabUtility.LoadPrefabContents(ruta);
        try
        {
            Tilemap tm = contenido.GetComponentInChildren<Tilemap>();
            tm.ClearAllTiles();
            // Arriba los automaticos; debajo, las piezas sueltas (nieve y hielo)
            // para cuando se quiera una en concreto.
            tm.SetTile(new Vector3Int(0, 1, 0), nieve);
            tm.SetTile(new Vector3Int(2, 1, 0), hielo);
            // La sombra de las zonas ocultas (se pinta en el Tilemap "ZonasOcultas").
            TileBase sombra = AssetDatabase.LoadAssetAtPath<TileBase>("Assets/Tiles/Comun/Sombra.asset");
            if (sombra != null) tm.SetTile(new Vector3Int(4, 1, 0), sombra);
            for (int i = 0; i < 9; i++)
            {
                Vector3Int c = new Vector3Int(i % 3, -1 - i / 3, 0);
                tm.SetTile(c, AssetDatabase.LoadAssetAtPath<Tile>($"{Carpeta}/nieve_{Piezas[i]}.asset"));
                tm.SetTile(c + new Vector3Int(4, 0, 0), AssetDatabase.LoadAssetAtPath<Tile>($"{Carpeta}/hielo_{Piezas[i]}.asset"));
            }
            PrefabUtility.SaveAsPrefabAsset(contenido, ruta);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contenido);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[Paleta] Paleta lista en " + ruta);
    }

    // Tile automatico con las 9 piezas de un conjunto: mira arriba, abajo y a los
    // lados, igual que el generador del nivel.
    private static TileTerreno TileAuto(string nombre, string conj, bool resbaladizo)
    {
        string ruta = $"{Carpeta}/{nombre}.asset";
        TileTerreno t = AssetDatabase.LoadAssetAtPath<TileTerreno>(ruta);
        if (t == null)
        {
            t = ScriptableObject.CreateInstance<TileTerreno>();
            AssetDatabase.CreateAsset(t, ruta);
        }
        var s = new Dictionary<string, Tile>();
        foreach (string p in Piezas) s[p] = AssetDatabase.LoadAssetAtPath<Tile>($"{Carpeta}/{conj}_{p}.asset");
        Vector3Int arriba = Vector3Int.up, abajo = Vector3Int.down, izq = Vector3Int.left, der = Vector3Int.right;
        const int No = RuleTile.TilingRuleOutput.Neighbor.NotThis;

        RuleTile.TilingRule Regla(string pieza, params Vector3Int[] vacios)
        {
            return new RuleTile.TilingRule
            {
                m_Sprites = new[] { s[pieza] != null ? s[pieza].sprite : null },
                m_ColliderType = Tile.ColliderType.Grid,
                m_NeighborPositions = vacios.ToList(),
                m_Neighbors = vacios.Select(_ => No).ToList(),
            };
        }

        // El orden importa: gana la primera que encaje.
        t.m_TilingRules = new List<RuleTile.TilingRule>
        {
            Regla("esq_ai", arriba, izq), Regla("esq_ad", arriba, der), Regla("arriba", arriba),
            Regla("esq_bi", abajo, izq), Regla("esq_bd", abajo, der), Regla("abajo", abajo),
            // Columna de una casilla de ancho (una pared falsa en un hueco): el
            // borde hacia fuera, como la dibujaba el generador.
            Regla("der", izq, der),
            Regla("izq", izq), Regla("der", der),
        };
        t.m_DefaultSprite = s["centro"] != null ? s["centro"].sprite : null;
        t.m_DefaultColliderType = Tile.ColliderType.Grid;
        t.resbaladizo = resbaladizo;
        t.color = s["centro"] != null ? s["centro"].color : Color.white;
        EditorUtility.SetDirty(t);
        return t;
    }
}
