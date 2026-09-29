using System;
using System.Collections.Generic;
using UnityEngine;

// Donde esta la hoja de la espada en cada fotograma del player (la espada va
// pintada dentro del sprite, no es un objeto aparte). Lo genera la Ronda 8
// buscando los pixeles de los tonos de la hoja; lo usan las particulas del
// arma imbuida (EfectoArma). Resources/MapaHoja.
public class MapaHoja : ScriptableObject
{
    [Serializable]
    public class Entrada
    {
        public Sprite sprite;
        // Puntos de la hoja en unidades locales del sprite (sin voltear).
        public Vector2[] puntos;
    }

    public List<Entrada> entradas = new List<Entrada>();

    private Dictionary<Sprite, Vector2[]> porSprite;
    private static MapaHoja instancia;
    private static bool buscado;

    public static MapaHoja Get()
    {
        if (!buscado) { instancia = Resources.Load<MapaHoja>("MapaHoja"); buscado = true; }
        return instancia;
    }

    // Un punto al azar de la hoja en ese fotograma (false si no se ve la hoja).
    public bool Punto(Sprite s, out Vector2 punto)
    {
        punto = Vector2.zero;
        if (s == null) return false;
        if (porSprite == null)
        {
            porSprite = new Dictionary<Sprite, Vector2[]>();
            foreach (Entrada e in entradas) if (e.sprite != null && e.puntos != null && e.puntos.Length > 0) porSprite[e.sprite] = e.puntos;
        }
        if (!porSprite.TryGetValue(s, out Vector2[] p)) return false;
        punto = p[UnityEngine.Random.Range(0, p.Length)];
        return true;
    }
}
