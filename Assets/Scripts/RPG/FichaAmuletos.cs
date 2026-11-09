using System;
using System.Collections.Generic;
using UnityEngine;

// Los amuletos (Resources/Amuletos): nombre, imagen y precio en el totem. Por
// ahora no hacen nada: solo se compran y se equipan en la hoguera. El orden de
// la lista es el de la coleccion (la cuadricula de la hoguera y del totem).
// La crea Warrior > Amuletos > Crear o actualizar (no pisa lo editado).
[CreateAssetMenu(menuName = "Warrior/Amuletos", fileName = "Amuletos")]
public class FichaAmuletos : ScriptableObject
{
    [Serializable]
    public class Amuleto
    {
        [Tooltip("Identificador (no cambiarlo: lo que tienes y lo equipado se guardan con el).")]
        public string id;
        public string nombre;
        public Sprite icono;
        [Tooltip("Lo que hace (y su desventaja, si tiene). Se ve en el totem y en la hoguera.")]
        [TextArea(2, 4)] public string descripcion;
        [Tooltip("Precio en almas en el totem.")]
        [Min(0)] public int precio = 1;
    }

    public List<Amuleto> amuletos = new List<Amuleto>();

    [Header("Huecos para equipar")]
    [Tooltip("Abiertos al empezar (los demas se abren con el Vestigio del Vacio).")]
    [Min(0)] public int huecosIniciales = 2;
    [Min(1)] public int huecosMaximos = 4;

    public Amuleto Buscar(string id) => amuletos.Find(a => a != null && a.id == id);

    private static FichaAmuletos instancia;

    public static FichaAmuletos Get()
    {
        if (instancia == null) instancia = Resources.Load<FichaAmuletos>("Amuletos");
        return instancia;
    }
}
