using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// El Bestiario (Resources/Bestiario): una entrada por enemigo normal, con su
// retrato, su descripcion y sus patrones. Todo se edita en el Inspector. Los
// numeros (zona, almas, elementos) salen de la ficha del enemigo
// (Assets/Data/Enemigos). Los jefes no van aqui.
//
// Lo que se desbloquea (global, como los logros):
//   - Al verlo por primera vez (te detecta o le haces dano): imagen y nombre.
//   - Al derrotarlo por primera vez: descripcion y almas.
//   - Al derrotarlo "derrotasParaPatrones" veces: sus patrones de ataque.
//   - Al golpearlo con cada elemento: si es debil, resistente o neutral.
[CreateAssetMenu(menuName = "Warrior/Bestiario", fileName = "Bestiario")]
public class FichaBestiario : ScriptableObject
{
    [Serializable]
    public class Entrada
    {
        public DefinicionEnemigo enemigo;
        [Tooltip("Imagen del enemigo (un fotograma quieto).")]
        public Sprite retrato;
        [Tooltip("Enemigo del que es variante de color o elite (vacio = es un enemigo base).")]
        public DefinicionEnemigo relacionado;
        [TextArea(2, 6)] public string descripcion;
        [TextArea(2, 6)] public string patrones;
        [Tooltip("Derrotas para desbloquear sus patrones de ataque.")]
        [Min(1)] public int derrotasParaPatrones = 5;
        [Tooltip("Desmarcar para que no salga en el Bestiario.")]
        public bool mostrar = true;

        public string Id => enemigo != null ? enemigo.name : "";
        public string Nombre => enemigo == null ? "???" : enemigo.EsElite ? enemigo.NombreElite : enemigo.nombre;
        public int Zona => enemigo != null ? enemigo.zona : 1;
    }

    public List<Entrada> entradas = new List<Entrada>();
    [Tooltip("Nombre de cada zona (1, 2 y 3) en las cabeceras del Bestiario.")]
    public string[] nombresZona = { "Paso Nevado", "Cueva Carmesí", "Más allá" };

    public string NombreZona(int zona) => nombresZona != null && zona >= 1 && zona <= nombresZona.Length ? nombresZona[zona - 1] : "Zona " + zona;

    public IEnumerable<Entrada> Visibles => entradas.Where(e => e != null && e.enemigo != null && e.mostrar);

    public Entrada Buscar(string id) => entradas.FirstOrDefault(e => e != null && e.Id == id);

    private static FichaBestiario instancia;

    public static FichaBestiario Get()
    {
        if (instancia == null) instancia = Resources.Load<FichaBestiario>("Bestiario");
        return instancia;
    }
}
