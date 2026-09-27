using UnityEngine;

// Numeros de la subida de nivel en la hoguera (Resources/AjustesProgreso).
// Se editan en el Inspector: lo que vale cada nivel en almas y cuanto sube cada
// estadistica. Los cambios se notan al darle a Play.
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

    [Header("Vida")]
    public int vidaBase = 100;
    public int vidaPorNivel = 10;

    [Header("Estamina")]
    public float estaminaBase = 100f;
    public float estaminaPorNivel = 8f;

    [Header("Mana")]
    public float manaBase = 60f;
    public float manaPorNivel = 10f;

    [Header("Resistencias (0.03 = 3 % menos de dano por nivel)")]
    public float resistenciaPorNivel = 0.03f;
    [Tooltip("Tope de las resistencias (0.45 = 45 %).")]
    public float resistenciaMaxima = 0.45f;

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
