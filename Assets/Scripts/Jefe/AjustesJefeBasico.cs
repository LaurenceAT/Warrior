using UnityEngine;

// Numeros de un jefe basico (los que crea Warrior > Crear jefe nuevo). Cada jefe
// tiene su ficha en Assets/Data/Jefes ("Ajustes <Nombre>").
public class AjustesJefeBasico : ScriptableObject
{
    [Header("Vida por fase")]
    [Min(1)] public int vidaFase1 = 600;
    [Min(1)] public int vidaFase2 = 800;

    [Header("Ataque de ejemplo: embestida")]
    [Tooltip("Dano al player (puntos de vida).")]
    [Min(0)] public int dano = 25;
    [Tooltip("Aviso antes del ataque: se tine de rojo (segundos).")]
    [Min(0f)] public float aviso = 0.6f;
    [Tooltip("Lo que dura la embestida (segundos).")]
    [Min(0.05f)] public float duracionAtaque = 0.35f;
    [Tooltip("Velocidad de la embestida (unidades por segundo).")]
    [Min(0f)] public float velocidadAtaque = 14f;
    [Tooltip("Caja de golpe delante de el (ancho y alto).")]
    public Vector2 cajaGolpe = new Vector2(1.6f, 2f);
    [Tooltip("Se queda quieto tras atacar (segundos).")]
    [Min(0f)] public float recuperacion = 0.8f;
    [Tooltip("Pausa entre ataques (minimo y maximo, segundos).")]
    public Vector2 pausa = new Vector2(0.8f, 1.4f);

    [Header("Fase 2")]
    [Tooltip("Todo va mas rapido en la fase 2 (1 = igual).")]
    [Min(0.1f)] public float multVelocidadFase2 = 1.3f;
    [Tooltip("Lo que tarda en levantarse con la vida de la fase 2 (segundos).")]
    [Min(0f)] public float transicion = 1.2f;

    [Header("Imagen temporal")]
    public Color color = new Color(0.6f, 0.4f, 0.8f, 1f);
    public Color colorFase2 = new Color(0.85f, 0.3f, 0.3f, 1f);
    public Color colorAviso = new Color(1f, 0.15f, 0.1f, 1f);
}
