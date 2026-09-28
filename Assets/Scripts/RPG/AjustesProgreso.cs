using UnityEngine;

// Numeros de la subida de nivel en la hoguera y de las mejoras de equipo
// (Resources/AjustesProgreso). Se editan en el Inspector: lo que vale cada nivel
// en almas, cuanto sube cada estadistica y cuanto mejoran la espada y los frascos.
// Los cambios se notan al darle a Play.
[CreateAssetMenu(menuName = "Warrior/Ajustes de progreso", fileName = "AjustesProgreso")]
public class AjustesProgreso : ScriptableObject
{
    [Header("Coste de cada nivel (en almas)")]
    [Tooltip("Coste del primer nivel.")]
    public float costeBase = 150f;
    [Tooltip("Lo que se suma al coste por cada nivel ya subido.")]
    public float costePorNivel = 70f;
    [Tooltip("Curva: cuanto mas alto, mas se encarecen los niveles altos.")]
    public float costeCurva = 14f;
    [Tooltip("Nivel maximo de cada estadistica.")]
    public int nivelMaximo = 20;

    [Header("Vida (al empezar y por nivel)")]
    public int vidaBase = 150;
    public int vidaPorNivel = 15;

    [Header("Estamina")]
    public float estaminaBase = 100f;
    public float estaminaPorNivel = 8f;

    [Header("Mana")]
    public float manaBase = 75f;
    public float manaPorNivel = 8f;

    [Header("Resistencias")]
    [Tooltip("Parte de lo que falta hasta el tope que se gana con cada nivel (0.12 = 12 %). " +
             "Asi cada nivel se nota, pero cada vez un poco menos.")]
    [Range(0.01f, 0.5f)] public float resistenciaCurva = 0.12f;
    [Tooltip("Tope de las resistencias: nunca se quita mas dano que esto (0.55 = 55 %).")]
    [Range(0f, 0.9f)] public float resistenciaMaxima = 0.55f;

    [Header("Mejora de la espada (Piedras de forja)")]
    public int espadaNivelMaximo = 5;
    [Tooltip("Dano extra por cada nivel de la espada (0.12 = +12 %).")]
    public float espadaDanoPorNivel = 0.12f;
    [Tooltip("Piedras que cuesta cada mejora de la espada.")]
    public int espadaCoste = 1;

    [Header("Mejora de los frascos (Lagrimas sagradas)")]
    public int frascosNivelMaximo = 5;
    [Tooltip("Lo que cura el frasco de sangre sin mejorar (parte de la vida maxima).")]
    [Range(0f, 1f)] public float frascoVidaBase = 0.6f;
    public float frascoVidaPorNivel = 0.06f;
    [Tooltip("Lo que devuelve el frasco de mana sin mejorar (parte del mana maximo).")]
    [Range(0f, 1f)] public float frascoManaBase = 0.5f;
    public float frascoManaPorNivel = 0.1f;
    [Tooltip("Lagrimas que cuesta cada mejora de los frascos.")]
    public int frascosCoste = 1;

    private static AjustesProgreso instancia;

    public static AjustesProgreso Get()
    {
        if (instancia == null)
        {
            instancia = Resources.Load<AjustesProgreso>("AjustesProgreso");
            if (instancia == null) instancia = CreateInstance<AjustesProgreso>();
        }
        return instancia;
    }
}
