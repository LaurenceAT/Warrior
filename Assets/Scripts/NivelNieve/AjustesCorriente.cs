using UnityEngine;

// Numeros de la corriente de viento (CorrienteViento). Ficha: Assets/Data/Niveles/Corriente Viento.
// El ciclo: sopla hacia arriba, pausa (el aviso), sopla hacia abajo, pausa, y vuelta a empezar.
[CreateAssetMenu(menuName = "Warrior/Ajustes corriente de viento", fileName = "Corriente Viento")]
public class AjustesCorriente : ScriptableObject
{
    [Header("Ciclo (segundos)")]
    public float duracionSubida = 3.5f;
    [Tooltip("Pausa tras soplar hacia arriba: el aviso de que va a soplar hacia abajo.")]
    public float pausaTrasSubida = 0.5f;
    public float duracionBajada = 2f;
    [Tooltip("Pausa tras soplar hacia abajo: el aviso de que va a soplar hacia arriba.")]
    public float pausaTrasBajada = 0.5f;

    [Header("Fuerza (solo con el player en el aire)")]
    [Tooltip("Velocidad hacia arriba a la que lleva al player (unidades por segundo).")]
    public float velocidadSubida = 6f;
    [Tooltip("Velocidad hacia abajo a la que lleva al player (unidades por segundo, en positivo).")]
    public float velocidadBajada = 10f;
    [Tooltip("Lo rapido que cambia la velocidad del player hacia la del viento.")]
    public float aceleracion = 40f;

    [Header("Copos (muestran la direccion)")]
    public float coposPorSegundo = 22f;
    public float velocidadCopos = 5f;
    public Color colorCopos = new Color(0.9f, 0.96f, 1f, 0.75f);
    [Tooltip("En las pausas los copos casi se paran: asi se ve el aviso.")]
    [Range(0f, 1f)] public float coposEnPausa = 0.15f;

    [Header("Sonido (al empezar cada soplido, si el player esta cerca)")]
    public string sonido = "ventisca_rafaga";
    public float volumen = 0.55f;
    public float tonoSubida = 1.15f;
    public float tonoBajada = 0.8f;
    public float distanciaSonido = 16f;
}
