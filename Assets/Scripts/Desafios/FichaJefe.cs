using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Ficha de un jefe para el menu de Desafios (Resources/Desafios). Todo se edita
// en el Inspector: textos, debilidades que se muestran, el cofre de la arena y
// los numeros del modo Dificil. Para anadir un jefe nuevo basta con crear su
// ficha (Crear > Warrior > Ficha de jefe) y que su nivel tenga una ArenaJefe.
[CreateAssetMenu(menuName = "Warrior/Ficha de jefe", fileName = "FichaJefe")]
public class FichaJefe : ScriptableObject
{
    [Serializable]
    public class Afinidad
    {
        public Elemento elemento;
        [Tooltip("Multiplicador de dano (1.8 = +80 %, 0.4 = -60 %).")]
        public float multiplicador = 1f;
        [Tooltip("Cuando pasa (\"Fase 2\", \"Siempre\"...).")]
        public string cuando = "Siempre";
    }

    [Tooltip("Identificador (no cambiarlo: los tiempos y completados se guardan con el).")]
    public string id;
    public string nombre;
    [Tooltip("Escena donde esta su arena.")]
    public string escena;
    [Tooltip("Orden en la lista.")]
    public int orden;
    public Sprite imagen;

    [Header("Debilidades y resistencias (lo que hace de verdad el jefe)")]
    public List<Afinidad> afinidades = new List<Afinidad>();
    [Tooltip("Algo mas que no sea de un elemento (por ejemplo: la espada sin imbuir).")]
    [TextArea(1, 3)] public string notaAfinidades;

    [Header("Textos")]
    [TextArea(4, 14)] public string informacion;
    [TextArea(4, 14)] public string historia;

    [Header("Cofre de la arena")]
    public int almasCofre = 2500;
    public Equipo.Objeto[] objetosCofre = { Equipo.Objeto.PiedraForja, Equipo.Objeto.LagrimaCarmesi, Equipo.Objeto.FrascoSangre };

    [Header("Modo Dificil (solo numeros: mismos ataques y avisos)")]
    [Tooltip("Vida del jefe (1.5 = +50 %).")]
    public float multVida = 1.5f;
    [Tooltip("Dano que te hace (1.3 = +30 %).")]
    public float multDano = 1.3f;
    [Tooltip("Rapidez entre ataques: las pausas se dividen por esto (1.35 = pausas un 26 % mas cortas).")]
    public float multVelocidad = 1.35f;

    private static FichaJefe[] todas;

    public static FichaJefe[] Todas()
    {
        if (todas == null) todas = Resources.LoadAll<FichaJefe>("Desafios").OrderBy(f => f.orden).ThenBy(f => f.nombre).ToArray();
        return todas;
    }

    public static FichaJefe DeEscena(string escena) => Todas().FirstOrDefault(f => f.escena == escena);
}
