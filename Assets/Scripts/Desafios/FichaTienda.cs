using System;
using System.Collections.Generic;
using UnityEngine;

// La tienda del totem de los desafios (Resources/Desafios/Tienda): que vende,
// a que precio y las frases del totem. Todo editable en el Inspector.
[CreateAssetMenu(menuName = "Warrior/Tienda del totem", fileName = "Tienda")]
public class FichaTienda : ScriptableObject
{
    [Serializable]
    public class Articulo
    {
        [Tooltip("Identificador unico (no repetir).")]
        public string id;
        public string nombre;
        public Equipo.Objeto objeto;
        public int precio = 500;
        [Tooltip("Hay que comprar antes este otro (id), por ejemplo el Frasco I antes del II. Vacio = ninguno.")]
        public string requiere;
    }

    public List<Articulo> articulos = new List<Articulo>();

    [Tooltip("Frases del totem al abrir la tienda (una al azar, sin repetir la anterior).")]
    [TextArea(1, 3)] public List<string> frases = new List<string>();
    [Tooltip("Letras por segundo de la frase.")]
    public float velocidadFrase = 40f;

    [Header("El totem")]
    public Sprite spriteTotem;
    [Tooltip("Sombra en el suelo (opcional).")]
    public Sprite sombraTotem;
    [Tooltip("Alto del totem en unidades.")]
    public float altoTotem = 2.6f;
    [Tooltip("Cuanto flota sobre el suelo (unidades).")]
    public float alturaVuelo = 0.35f;
    [Tooltip("Cuanto sube y baja al flotar (unidades) y a que ritmo.")]
    public float vaivenTotem = 0.08f;
    public float ritmoVaiven = 1.3f;
    public Color brilloRunas = new Color(0.3f, 0.95f, 1f, 1f);

    private static FichaTienda instancia;

    public static FichaTienda Get()
    {
        if (instancia == null) instancia = Resources.Load<FichaTienda>("Desafios/Tienda");
        return instancia;
    }
}
