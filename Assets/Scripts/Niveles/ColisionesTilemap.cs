using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// En la version web, la colision de la cueva (capa "Colision", casillas que no se
// dibujan) cargaba sin ninguna forma y el player caia al vacio. Al cargar cada
// escena, si una colision de casillas ha quedado vacia, se rehace con cajas: una
// por cada tramo seguido de casillas en cada fila, unidas por su CompositeCollider2D.
// Si ya tiene formas (lo normal, y siempre en el Editor) no se toca.
public static class ColisionesTilemap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Iniciar()
    {
        Revisar();
        SceneManager.sceneLoaded += (s, m) => Revisar();
    }

    private static void Revisar()
    {
        foreach (TilemapCollider2D tc in Object.FindObjectsByType<TilemapCollider2D>(FindObjectsSortMode.None))
        {
            Tilemap tm = tc.GetComponent<Tilemap>();
            if (tm == null || tm.GetUsedTilesCount() == 0 || !Vacia(tc)) continue;

            // Primero, volver a calcularla.
            tm.RefreshAllTiles();
            tc.ProcessTilemapChanges();
            CompositeCollider2D cc = tc.GetComponent<CompositeCollider2D>();
            if (cc != null) cc.GenerateGeometry();
            if (!Vacia(tc)) { Debug.Log($"[Colision] {tm.name} recalculada"); continue; }

            // Si sigue vacia: cajas por tramos de casillas.
            int cajas = Cajas(tm, cc != null);
            if (cc != null) cc.GenerateGeometry();
            Debug.Log($"[Colision] {tm.name} rehecha con {cajas} cajas ({(cc != null ? cc.pathCount + " caminos" : "sin composite")})");
        }
    }

    private static bool Vacia(TilemapCollider2D tc)
    {
        CompositeCollider2D cc = tc.GetComponent<CompositeCollider2D>();
        return cc != null ? cc.pathCount == 0 : tc.shapeCount == 0;
    }

    private static int Cajas(Tilemap tm, bool unir)
    {
        BoundsInt b = tm.cellBounds;
        Vector3 celda = tm.layoutGrid != null ? tm.layoutGrid.cellSize : Vector3.one;
        int n = 0;
        for (int y = b.yMin; y < b.yMax; y++)
        {
            int x = b.xMin;
            while (x < b.xMax)
            {
                if (!tm.HasTile(new Vector3Int(x, y, 0))) { x++; continue; }
                int x0 = x;
                while (x < b.xMax && tm.HasTile(new Vector3Int(x, y, 0))) x++;
                BoxCollider2D box = tm.gameObject.AddComponent<BoxCollider2D>();
                box.size = new Vector2((x - x0) * celda.x, celda.y);
                box.offset = (Vector2)tm.CellToLocal(new Vector3Int(x0, y, 0)) + new Vector2((x - x0) * celda.x * 0.5f, celda.y * 0.5f);
                if (unir) box.compositeOperation = Collider2D.CompositeOperation.Merge;
                n++;
            }
        }
        return n;
    }
}
