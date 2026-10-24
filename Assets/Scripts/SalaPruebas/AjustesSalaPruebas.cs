#if UNITY_EDITOR
using UnityEngine;

// Sala de pruebas (SOLO Editor): numeros de la sala y del menu F1.
public class AjustesSalaPruebas : ScriptableObject
{
    [Header("Sala")]
    [Tooltip("Ancho del suelo (centrado en x = 0).")]
    [Min(10)] public int ancho = 40;
    [Tooltip("Altura del techo.")]
    [Min(6)] public int alto = 20;
    public Color colorSuelo = new Color(0.32f, 0.32f, 0.35f, 1f);
    [Tooltip("Donde aparece el jefe o el enemigo (x).")]
    public float xAparicion = 8f;
    [Tooltip("Donde vuelve el jugador con \"Volver al centro\" (x).")]
    public float xCentro = 0f;

    [Header("Combate")]
    [Tooltip("Suenan la musica del jefe y el ambiente de su arena.")]
    public bool musica;
    [Tooltip("La Cazadora entra sin su dialogo largo (como en un reintento).")]
    public bool saltarDialogoCazadora = true;
    [Tooltip("Vida que quita \"Bajar vida\" (fraccion de la barra actual).")]
    [Range(0.01f, 1f)] public float bajarVida = 0.1f;
    [Tooltip("Pausa extra entre ataques con el bucle encendido (segundos).")]
    [Min(0f)] public float pausaBucle = 1f;
    [Tooltip("Dificil para jefes sin ficha de Desafio.")]
    public float multVida = 1.5f, multDano = 1.3f, multVelocidad = 1.35f;

    [Header("Menu")]
    public float anchoMenu = 400f;
    public float altoOpcion = 36f;
    public float tamanoTexto = 22f;
    [Tooltip("Separacion del menu con el borde izquierdo y con el HUD.")]
    public float margen = 40f;
}
#endif
