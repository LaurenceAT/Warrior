using UnityEngine;
using UnityEngine.Tilemaps;

// Tile "automatico" para pintar el terreno con la Tile Palette: elige solo la
// pieza correcta (esquina, borde de arriba, lado, centro...) segun las casillas
// de alrededor. Cuenta como vecina cualquier casilla pintada del mismo Tilemap y
// de los demas Tilemaps de terreno del Grid (CapaTerreno): asi una pared falsa
// pintada junto al suelo se une a el sin bordes.
// Con "resbaladizo" el player patina encima (hielo).
[CreateAssetMenu(menuName = "Warrior/Tile de terreno automatico", fileName = "Terreno (auto)")]
public class TileTerreno : RuleTile
{
    [Tooltip("El player resbala encima (hielo).")]
    public bool resbaladizo;
    [Tooltip("Tono de todas sus piezas (el hielo va tenido de azul).")]
    public Color color = Color.white;

    public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
    {
        base.GetTileData(position, tilemap, ref tileData);
        tileData.color = color;
        tileData.flags |= TileFlags.LockColor;
    }

    // Las reglas solo miran si hay o no roca en cada vecina (sin giros).
    public override bool RuleMatches(TilingRule rule, Vector3Int position, ITilemap tilemap, ref Matrix4x4 transform)
    {
        Tilemap tm = tilemap.GetComponent<Tilemap>();
        for (int i = 0; i < rule.m_Neighbors.Count && i < rule.m_NeighborPositions.Count; i++)
        {
            Vector3Int c = position + rule.m_NeighborPositions[i];
            bool ocupado = tm != null ? CapaTerreno.Ocupado(tm, c) : tilemap.GetTile(c) != null;
            int n = rule.m_Neighbors[i];
            if (n == TilingRuleOutput.Neighbor.This && !ocupado) return false;
            if (n == TilingRuleOutput.Neighbor.NotThis && ocupado) return false;
        }
        transform = Matrix4x4.identity;
        return true;
    }
}
