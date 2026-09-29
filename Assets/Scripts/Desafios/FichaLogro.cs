using System.Linq;
using UnityEngine;

// Ficha de un logro (Resources/Logros). Para anadir uno nuevo basta con crear
// su ficha (Crear > Warrior > Logro) con una condicion de la lista.
[CreateAssetMenu(menuName = "Warrior/Logro", fileName = "Logro")]
public class FichaLogro : ScriptableObject
{
    public enum Seccion { Partida = 0, Desafios = 1, Dificil = 2 }

    public enum Condicion
    {
        LlegarAEscena,          // parametro: nombre de la escena
        DerrotarJefeEnPartida,  // parametro: escena del jefe
        PrimeraMuerte,          // en la partida normal
        CompletarDesafio,       // parametro: id de la ficha del jefe
        CompletarDesafioDificil // parametro: id de la ficha del jefe
    }

    [Tooltip("Identificador (no cambiarlo: los logros se guardan con el).")]
    public string id;
    public Seccion seccion;
    public int orden;
    public string nombre;
    [TextArea(1, 3)] public string descripcion;
    public Sprite icono;
    public Condicion condicion;
    public string parametro;

    private static FichaLogro[] todos;

    public static FichaLogro[] Todos()
    {
        if (todos == null) todos = Resources.LoadAll<FichaLogro>("Logros").OrderBy(l => l.seccion).ThenBy(l => l.orden).ToArray();
        return todos;
    }
}
