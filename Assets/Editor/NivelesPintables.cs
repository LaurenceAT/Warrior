using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Tilemaps;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// Pasa las dos escenas al sistema de Tilemaps para pintarlas a mano:
//   - Cueva: la roca (antes rectangulos con piezas) pasa a un Tilemap "Roca" de
//     tiles negros con colision; los bordes de losas y pilares los pone
//     BordesCueva solo. Las plantas y estalagmitas se conservan.
//   - Las dos: Tilemaps "ParedesFalsas" (se ve roca, se atraviesa) y
//     "ZonasOcultas" (sombra que se aparta al entrar). El escondite de la nieve
//     pasa a estar hecho asi.
//   - Paletas: "Paleta Cueva" (roca y sombra) y la de la nieve con la sombra.
// Tambien borra las escenas viejas y deja Build Settings con cueva y nieve.
public static class NivelesPintables
{
    private const string CarpetaCueva = "Assets/Tiles/Cueva";
    private const string CarpetaComun = "Assets/Tiles/Comun";
    private const string RutaSombra = CarpetaComun + "/Sombra.asset";
    private const string RutaRocaCueva = CarpetaCueva + "/Roca cueva.asset";
    private const string RutaPaletaCueva = CarpetaCueva + "/Paleta Cueva.prefab";

    private static readonly string[] EscenasViejas =
    {
        "Assets/Scenes/Nivel 1.unity", "Assets/Scenes/Nivel 2.unity", "Assets/Scenes/Nivel 3.unity", "Assets/Scenes/Nivel 4.unity",
        "Assets/Scenes/SampleScene.unity", "Assets/Scenes/Level_Prototype.unity",
    };

    public static void Todo()
    {
        Carpeta("Assets/Tiles", "Cueva");
        Carpeta("Assets/Tiles", "Comun");
        Tile sombra = TileSimple(RutaSombra, new Color(0.02f, 0.02f, 0.045f, 1f), Tile.ColliderType.None, TileFlags.None);
        Tile rocaCueva = TileSimple(RutaRocaCueva, new Color(0.025f, 0.02f, 0.025f, 1f), Tile.ColliderType.Grid, TileFlags.LockColor);

        Scene nieve = EditorSceneManager.OpenScene(ConfigNivelEditor.EscenaNieve);
        ConvertirNieve(nieve, sombra);
        EditorSceneManager.MarkSceneDirty(nieve);
        EditorSceneManager.SaveScene(nieve);

        Scene cueva = EditorSceneManager.OpenScene(ConfigNivelEditor.EscenaCueva);
        ConvertirCueva(cueva, rocaCueva, sombra);
        EditorSceneManager.MarkSceneDirty(cueva);
        EditorSceneManager.SaveScene(cueva);

        PaletaNieve.CrearPaleta();
        AnadirAPaleta(PaletaNieve.RutaPaleta, sombra, new Vector3Int(4, 1, 0));
        PaletaCueva(rocaCueva, sombra);
        BorrarEscenasViejas();
        AssetDatabase.SaveAssets();
        Debug.Log("[Pintables] Listo.");
    }

    // Rehace la colision de los Tilemaps de las dos escenas.
    public static void ArreglarColisiones()
    {
        foreach (string ruta in new[] { ConfigNivelEditor.EscenaCueva, ConfigNivelEditor.EscenaNieve })
        {
            Scene e = EditorSceneManager.OpenScene(ruta);
            foreach (GameObject r in e.GetRootGameObjects())
                foreach (TilemapCollider2D tc in r.GetComponentsInChildren<TilemapCollider2D>(true).ToList())
                {
                    Tilemap t = tc.GetComponent<Tilemap>();
                    RehacerColision(t);
                    CompositeCollider2D cc = t.GetComponent<CompositeCollider2D>();
                    Debug.Log($"[Pintables] {e.name}/{t.name}: {(cc != null ? cc.pathCount : -1)} contornos");
                }
            // Y los tiles automaticos, ahora que todas las capas se ven entre si.
            foreach (GameObject r in e.GetRootGameObjects())
                foreach (Tilemap t in r.GetComponentsInChildren<Tilemap>(true))
                    if (t.GetComponent<CapaTerreno>() != null) t.RefreshAllTiles();
            EditorSceneManager.MarkSceneDirty(e);
            EditorSceneManager.SaveScene(e);
        }
    }

    // ------------------------------------------------------------------ Nieve

    private static void ConvertirNieve(Scene e, Tile sombra)
    {
        GameObject nivel = e.GetRootGameObjects().First(r => r.name == "Nivel");
        Tilemap suelo = nivel.GetComponentsInChildren<Tilemap>(true).First(t => t.name == "Suelo");
        Grid grid = suelo.GetComponentInParent<Grid>();
        if (suelo.GetComponent<CapaTerreno>() == null) suelo.gameObject.AddComponent<CapaTerreno>();

        Tilemap falsas = CapaParedesFalsas(grid);
        Tilemap ocultas = CapaZonasOcultas(grid);
        TileTerreno nieveAuto = AssetDatabase.LoadAssetAtPath<TileTerreno>("Assets/Tiles/Nieve/Nieve (auto).asset");

        // La pared falsa y la sombra del escondite, del sistema viejo al nuevo.
        Transform viejasParedes = nivel.transform.Find("ParedesFalsas");
        if (viejasParedes != null)
        {
            foreach (BoxCollider2D b in viejasParedes.GetComponentsInChildren<BoxCollider2D>(true))
                foreach (Vector3Int c in Celdas(falsas, b.bounds)) falsas.SetTile(c, nieveAuto);
            Object.DestroyImmediate(viejasParedes.gameObject);
        }
        Transform viejasZonas = nivel.transform.Find("InterioresOcultos");
        if (viejasZonas != null)
        {
            foreach (SpriteRenderer s in viejasZonas.GetComponentsInChildren<SpriteRenderer>(true))
                foreach (Vector3Int c in Celdas(ocultas, s.bounds)) ocultas.SetTile(c, sombra);
            Object.DestroyImmediate(viejasZonas.gameObject);
        }
        Debug.Log($"[Pintables] Nieve: pared falsa {Contar(falsas)} casillas, sombra {Contar(ocultas)} casillas.");
    }

    // ------------------------------------------------------------------ Cueva

    private static void ConvertirCueva(Scene e, Tile roca, Tile sombra)
    {
        GameObject nivel = e.GetRootGameObjects().First(r => r.name == "Nivel");
        Transform grupoRoca = nivel.transform.Find("Roca");
        Transform deco = nivel.transform.Find("Decoracion");
        if (deco == null) { deco = new GameObject("Decoracion").transform; deco.SetParent(nivel.transform, false); }

        Grid grid = nivel.GetComponentInChildren<Grid>();
        if (grid == null)
        {
            grid = new GameObject("Terreno", typeof(Grid)).GetComponent<Grid>();
            grid.transform.SetParent(nivel.transform, false);
        }
        Tilemap tm = grid.GetComponentsInChildren<Tilemap>(true).FirstOrDefault(t => t.name == "Roca");
        if (tm == null) tm = NuevoTilemap(grid, "Roca", "Ground", 0);
        if (tm.GetComponent<CapaTerreno>() == null) tm.gameObject.AddComponent<CapaTerreno>();

        int celdas = 0, piezas = 0;
        if (grupoRoca != null)
        {
            foreach (Transform bloque in grupoRoca.Cast<Transform>().ToList())
            {
                BoxCollider2D b = bloque.GetComponent<BoxCollider2D>();
                if (b != null)
                {
                    // Los bordes van a la casilla entera mas cercana (repisas de 0.7 de alto: una casilla).
                    Bounds bb = b.bounds;
                    int x0 = Mathf.RoundToInt(bb.min.x), x1 = Mathf.Max(x0 + 1, Mathf.RoundToInt(bb.max.x));
                    int y0 = Mathf.RoundToInt(bb.min.y), y1 = Mathf.Max(y0 + 1, Mathf.RoundToInt(bb.max.y));
                    for (int x = x0; x < x1; x++)
                    for (int y = y0; y < y1; y++) { tm.SetTile(new Vector3Int(x, y, 0), roca); celdas++; }
                }
                // Plantas y estalagmitas: se quedan (las losas, pilares y relleno los rehace BordesCueva).
                foreach (Transform h in bloque.Cast<Transform>().ToList())
                    if (h.name == "Planta" || h.name == "Estalagmita" || h.name == "EstalactitaDeco") { h.SetParent(deco, true); piezas++; }
            }
            Object.DestroyImmediate(grupoRoca.gameObject);
        }
        foreach (Vector3Int c in tm.cellBounds.allPositionsWithin)
            if (tm.HasTile(c)) tm.SetColliderType(c, Tile.ColliderType.Grid);
        // La colision se pone despues de pintar: puesta antes, se queda vacia.
        RehacerColision(tm);

        CapaParedesFalsas(grid);
        CapaZonasOcultas(grid);

        BordesCueva bc = tm.GetComponent<BordesCueva>();
        if (bc == null) bc = tm.gameObject.AddComponent<BordesCueva>();
        ConfigNivel config = AssetDatabase.LoadAssetAtPath<ConfigNivel>(ConfigNivelEditor.RutaCueva);
        bc.config = config;
        bc.Reconstruir();
        Debug.Log($"[Pintables] Cueva: tiles reales en el Tilemap={Contar(tm)} (usados {tm.GetUsedTilesCount()})");
        Debug.Log($"[Pintables] Cueva: {celdas} casillas de roca, {piezas} adornos conservados, config={(config != null)}.");
    }

    // Quita y vuelve a poner la colision del Tilemap para que salga con lo pintado.
    private static void RehacerColision(Tilemap tm)
    {
        CompositeCollider2D cc = tm.GetComponent<CompositeCollider2D>();
        if (cc != null) Object.DestroyImmediate(cc);
        TilemapCollider2D tc = tm.GetComponent<TilemapCollider2D>();
        if (tc != null) Object.DestroyImmediate(tc);
        PaletaNieve.PonerColisionTilemap(tm, LayerMask.NameToLayer("Ground"));
    }

    // ------------------------------------------------------------------ Capas

    private static Tilemap CapaParedesFalsas(Grid grid)
    {
        Tilemap t = grid.GetComponentsInChildren<Tilemap>(true).FirstOrDefault(x => x.name == "ParedesFalsas");
        if (t == null) t = NuevoTilemap(grid, "ParedesFalsas", "Ground", 0);
        if (t.GetComponent<CapaTerreno>() == null) t.gameObject.AddComponent<CapaTerreno>();
        ParedFalsa pf = t.GetComponent<ParedFalsa>();
        if (pf == null) pf = t.gameObject.AddComponent<ParedFalsa>();
        SerializedObject so = new SerializedObject(pf);
        so.FindProperty("grieta").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Tiles/Nieve/Grieta.png");
        so.ApplyModifiedPropertiesWithoutUndo();
        return t;
    }

    private static Tilemap CapaZonasOcultas(Grid grid)
    {
        Tilemap t = grid.GetComponentsInChildren<Tilemap>(true).FirstOrDefault(x => x.name == "ZonasOcultas");
        if (t == null) t = NuevoTilemap(grid, "ZonasOcultas", "Ground", 40);
        if (t.GetComponent<ZonaOculta>() == null) t.gameObject.AddComponent<ZonaOculta>();
        return t;
    }

    private static Tilemap NuevoTilemap(Grid grid, string nombre, string capa, int orden)
    {
        GameObject go = new GameObject(nombre, typeof(Tilemap), typeof(TilemapRenderer));
        go.transform.SetParent(grid.transform, false);
        TilemapRenderer r = go.GetComponent<TilemapRenderer>();
        r.sortingLayerName = capa;
        r.sortingOrder = orden;
        r.mode = TilemapRenderer.Mode.Chunk;
        return go.GetComponent<Tilemap>();
    }

    // Casillas cuyo centro cae dentro de un rectangulo del mundo.
    private static IEnumerable<Vector3Int> Celdas(Tilemap tm, Bounds b)
    {
        Vector3Int a = tm.WorldToCell(b.min), z = tm.WorldToCell(b.max);
        for (int x = a.x; x <= z.x; x++)
        for (int y = a.y; y <= z.y; y++)
        {
            Vector3Int c = new Vector3Int(x, y, 0);
            Vector3 centro = tm.GetCellCenterWorld(c);
            if (centro.x > b.min.x && centro.x < b.max.x && centro.y > b.min.y && centro.y < b.max.y) yield return c;
        }
    }

    private static int Contar(Tilemap t)
    {
        int n = 0;
        foreach (Vector3Int c in t.cellBounds.allPositionsWithin) if (t.HasTile(c)) n++;
        return n;
    }

    // ------------------------------------------------------------------ Tiles y paletas

    private static Tile TileSimple(string ruta, Color color, Tile.ColliderType colision, TileFlags flags)
    {
        Tile t = AssetDatabase.LoadAssetAtPath<Tile>(ruta);
        if (t == null)
        {
            t = ScriptableObject.CreateInstance<Tile>();
            AssetDatabase.CreateAsset(t, ruta);
        }
        t.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Tiles/Nieve/Blanco.png");
        t.color = color;
        t.colliderType = colision;
        t.flags = flags;
        EditorUtility.SetDirty(t);
        return t;
    }

    private static void PaletaCueva(Tile roca, Tile sombra)
    {
        GameObject p = AssetDatabase.LoadAssetAtPath<GameObject>(RutaPaletaCueva);
        if (p == null)
            GridPaletteUtility.CreateNewPalette(CarpetaCueva, "Paleta Cueva", GridLayout.CellLayout.Rectangle,
                                                GridPalette.CellSizing.Automatic, Vector3.one, GridLayout.CellSwizzle.XYZ);
        AnadirAPaleta(RutaPaletaCueva, roca, new Vector3Int(0, 0, 0));
        AnadirAPaleta(RutaPaletaCueva, sombra, new Vector3Int(2, 0, 0));
    }

    private static void AnadirAPaleta(string ruta, TileBase tile, Vector3Int celda)
    {
        GameObject contenido = PrefabUtility.LoadPrefabContents(ruta);
        try
        {
            contenido.GetComponentInChildren<Tilemap>().SetTile(celda, tile);
            PrefabUtility.SaveAsPrefabAsset(contenido, ruta);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contenido);
        }
    }

    private static void Carpeta(string padre, string nombre)
    {
        if (!AssetDatabase.IsValidFolder(padre + "/" + nombre)) AssetDatabase.CreateFolder(padre, nombre);
    }

    // ------------------------------------------------------------------ Escenas viejas

    private static void BorrarEscenasViejas()
    {
        foreach (string e in EscenasViejas)
            if (System.IO.File.Exists(e))
                Debug.Log($"[Pintables] Escena {e} a la papelera: {AssetDatabase.MoveAssetToTrash(e)}");
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(ConfigNivelEditor.EscenaCueva, true),
            new EditorBuildSettingsScene(ConfigNivelEditor.EscenaNieve, true),
        };
    }
}
