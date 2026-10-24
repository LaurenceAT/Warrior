#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

// Sala de pruebas (SOLO Editor): las formas de terreno que se ponen y quitan
// desde el menu F1. Cada forma son una o varias piezas (centro y tamano, en
// unidades; el suelo de la sala esta en y = 0). Se pintan en casillas de 0,5.
public class TerrenosPruebas : ScriptableObject
{
    [Serializable]
    public struct Pieza
    {
        public Vector2 centro;
        public Vector2 tamano;

        public Pieza(float x, float y, float ancho, float alto)
        {
            centro = new Vector2(x, y);
            tamano = new Vector2(ancho, alto);
        }

        public Rect Rect => new Rect(centro - tamano * 0.5f, tamano);
    }

    [Serializable]
    public class Forma
    {
        public string nombre;
        public List<Pieza> piezas = new List<Pieza>();
    }

    public List<Forma> formas = new List<Forma>();
    public Color color = new Color(0.55f, 0.55f, 0.58f, 1f);
}
#endif
