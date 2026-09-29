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

    [Tooltip("Id de un jefe secreto: mientras no este desbloqueado, este logro no sale ni cuenta (vacio = siempre).")]
    public string jefeSecreto;

    public bool Visible
    {
        get
        {
            if (string.IsNullOrEmpty(jefeSecreto)) return true;
            FichaJefe f = FichaJefe.Todas().FirstOrDefault(j => j.id == jefeSecreto);
            return f == null || f.Desbloqueado;
        }
    }

    private static FichaLogro[] todos;

    public static FichaLogro[] Todos()
    {
        if (todos == null) todos = Resources.LoadAll<FichaLogro>("Logros").OrderBy(l => l.seccion).ThenBy(l => l.orden).ToArray();
        return todos;
    }

    // Los que se ven y cuentan ahora (sin los de un jefe secreto aun oculto).
    public static FichaLogro[] Visibles() => Todos().Where(l => l.Visible).ToArray();
}
