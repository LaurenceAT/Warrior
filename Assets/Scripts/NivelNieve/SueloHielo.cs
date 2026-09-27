using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Suelo de hielo: el player pierde traccion encima (PlayerControler mira si el
// suelo que pisa tiene este componente). Brilla un poco para que se distinga.
public class SueloHielo : MonoBehaviour
{
    [SerializeField] private SpriteRenderer[] brillos;
    private float t;

    public void PonerBrillos(SpriteRenderer[] b) => brillos = b;

    // Si el suelo que se pisa es hielo: o lleva este componente (el puente del
    // lago), o es una casilla de hielo del Tilemap del terreno (pintada con el
    // tile automatico de hielo, o una de las piezas "hielo_" del generador).
    public static bool EsResbaladizo(RaycastHit2D h)
    {
        if (!h) return false;
        if (h.collider.GetComponent<SueloHielo>() != null) return true;
        UnityEngine.Tilemaps.Tilemap tm = h.collider.GetComponent<UnityEngine.Tilemaps.Tilemap>();
        if (tm == null) return false;
        UnityEngine.Tilemaps.TileBase t = tm.GetTile(tm.WorldToCell(h.point + Vector2.down * 0.05f));
        if (t == null) return false;
        if (t is TileTerreno tt) return tt.resbaladizo;
        return t.name.StartsWith("hielo_");
    }

    private void Update()
    {
        if (brillos == null) return;
        t += Time.deltaTime;
        for (int i = 0; i < brillos.Length; i++)
        {
            if (brillos[i] == null) continue;
            Color c = brillos[i].color;
            c.a = 0.25f + 0.2f * Mathf.Sin(t * 1.7f + i * 1.3f);
            brillos[i].color = c;
        }
    }
}
