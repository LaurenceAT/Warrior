#if UNITY_EDITOR
using UnityEngine;

// Sala de pruebas (SOLO Editor): con que personaje se juega en la sala. Se aplica
// solo en memoria al entrar; tu partida y tu personaje guardados no cambian.
public class EquipoPruebas : ScriptableObject
{
    [Header("Niveles del personaje")]
    [Min(0)] public int vida;
    [Min(0)] public int estamina;
    [Min(0)] public int mana;
    [Min(0)] public int resHechizos;
    [Min(0)] public int resGolpes;
    [Min(0)] public int almas;

    [Header("Mejoras")]
    [Tooltip("Piedras de forja aplicadas (la espada hace mas dano).")]
    [Min(0)] public int nivelEspada;
    [Tooltip("Lagrimas carmesi aplicadas (el frasco de sangre cura mas).")]
    [Min(0)] public int nivelCuracion;
    [Tooltip("Lagrimas celestes aplicadas (el frasco de mana llena mas).")]
    [Min(0)] public int nivelMana;
    [Min(0)] public int frascosSangreExtra;
    [Min(0)] public int frascosManaExtra;

    [Header("Imbuicion")]
    [Tooltip("La espada empieza imbuida de este elemento (Ninguno = sin imbuir). La rueda (E) sigue funcionando.")]
    public Elemento imbuirAlEmpezar = Elemento.Ninguno;

    public int[] Niveles => new[] { vida, estamina, mana, resHechizos, resGolpes };
}
#endif
