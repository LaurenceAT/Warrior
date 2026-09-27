using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// Marca un Tilemap como "terreno": sus casillas cuentan como roca para los
// tiles automaticos y los bordes de los demas Tilemaps del mismo Grid (el suelo
// y las paredes falsas se unen sin costuras). Cuando se pinta en uno, se
// refrescan los bordes del otro.
[ExecuteAlways]
[RequireComponent(typeof(Tilemap))]
public class CapaTerreno : MonoBehaviour
{
    // Tilemaps de terreno de cada Grid. Se busca al pedirlo (no depende del orden
    // en que se cargan los objetos) y se vacia cuando alguno aparece o se quita.
    private static readonly Dictionary<GridLayout, Tilemap[]> porGrid = new Dictionary<GridLayout, Tilemap[]>();
    private Tilemap tm;

    private void OnEnable()
    {
        tm = GetComponent<Tilemap>();
        porGrid.Clear();
        Tilemap.tilemapTileChanged += AlCambiar;
    }

    private void OnDisable()
    {
        porGrid.Clear();
        Tilemap.tilemapTileChanged -= AlCambiar;
    }

    private void AlCambiar(Tilemap otro, Tilemap.SyncTile[] casillas)
    {
        if (tm == null || otro == tm || otro.layoutGrid != tm.layoutGrid || otro.GetComponent<CapaTerreno>() == null) return;
        foreach (Tilemap.SyncTile s in casillas)
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
                tm.RefreshTile(s.position + new Vector3Int(dx, dy, 0));
    }

    // Hay roca en esa casilla en alguno de los Tilemaps de terreno del Grid.
    public static bool Ocupado(Tilemap desde, Vector3Int celda)
    {
        if (desde == null) return false;
        if (desde.HasTile(celda)) return true;
        GridLayout g = desde.layoutGrid;
        if (g == null) return false;
        if (!porGrid.TryGetValue(g, out Tilemap[] capas) || capas == null)
        {
            var l = new List<Tilemap>();
            foreach (CapaTerreno c in g.GetComponentsInChildren<CapaTerreno>(true))
            {
                Tilemap t = c.GetComponent<Tilemap>();
                if (t != null) l.Add(t);
            }
            porGrid[g] = capas = l.ToArray();
        }
        foreach (Tilemap t in capas)
            if (t != null && t != desde && t.HasTile(celda)) return true;
        return false;
    }
}
