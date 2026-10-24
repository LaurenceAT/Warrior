#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// Sala de pruebas (SOLO Editor): pone y quita las formas de la ficha "Terrenos
// Pruebas". Son casillas grises con la misma colision que el suelo del juego
// (Tilemap + Composite en la capa Ground). Viven solo en memoria: al salir de la
// sala no queda nada.
public class TerrenoPruebas : MonoBehaviour
{
    private Tilemap mapa;
    private Tile tile;
    private readonly HashSet<int> puestas = new HashSet<int>();

    private TerrenosPruebas Ficha => SalaPruebas.Actual != null ? SalaPruebas.Actual.Terrenos : null;
    public int Cantidad => Ficha != null ? Ficha.formas.Count : 0;
    public string Nombre(int i) => Ficha.formas[i].nombre;
    public bool Puesta(int i) => puestas.Contains(i);
    public int Puestas => puestas.Count;

    private void Awake()
    {
        Color c = Ficha != null ? Ficha.color : Color.gray;
        mapa = SalaPruebas.CrearMapa(transform, "TerrenoPruebas", c, out tile);
    }

    // Devuelve null si se puso, o el motivo si no cabe.
    public string Poner(int i)
    {
        if (Ficha == null || i < 0 || i >= Cantidad || puestas.Contains(i)) return null;
        string choque = Choque(Ficha.formas[i]);
        if (choque != null) return choque;
        puestas.Add(i);
        Rehacer();
        return null;
    }

    public void Quitar(int i)
    {
        if (puestas.Remove(i)) Rehacer();
    }

    // Todas las que caben; devuelve los avisos de las que no.
    public List<string> PonerTodas()
    {
        var avisos = new List<string>();
        for (int i = 0; i < Cantidad; i++)
        {
            if (puestas.Contains(i)) continue;
            string choque = Choque(Ficha.formas[i]);
            if (choque != null) avisos.Add(choque);
            else puestas.Add(i);
        }
        Rehacer();
        return avisos;
    }

    public void Limpiar()
    {
        puestas.Clear();
        Rehacer();
    }

    private void Rehacer()
    {
        mapa.ClearAllTiles();
        foreach (int i in puestas)
            foreach (TerrenosPruebas.Pieza p in Ficha.formas[i].piezas) SalaPruebas.Pintar(mapa, tile, p.Rect);
        SalaPruebas.ActualizarColision(mapa);
    }

    // No se pone encima del jugador ni del jefe o los enemigos.
    private string Choque(TerrenosPruebas.Forma f)
    {
        foreach (TerrenosPruebas.Pieza p in f.piezas)
        {
            Rect r = p.Rect;
            foreach (Collider2D c in Physics2D.OverlapBoxAll(r.center, r.size, 0f))
            {
                if (c.GetComponentInParent<PlayerControler>() != null) return $"\"{f.nombre}\" no cabe: está encima del jugador.";
                if (c.GetComponentInParent<EnemigoBase>() != null) return $"\"{f.nombre}\" no cabe: está encima del jefe o del enemigo.";
            }
        }
        return null;
    }
}
#endif
