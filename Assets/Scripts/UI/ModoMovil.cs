using UnityEngine;

// "Haz como si esto fuera un movil".
//
// En un movil de verdad se enciende solo. En el Editor se enciende desde el menu
// Warrior > Movil, y asi se ven y se prueban los controles tactiles con el raton
// sin tener que compilar una APK cada vez.
//
// No guarda nada en el progreso del jugador: en el Editor vive en EditorPrefs
// (los ajustes del Editor, que no son del juego) y en una build no se guarda.
public static class ModoMovil
{
    public const string ClaveEditor = "warrior_modo_movil";

    private static bool forzado;
    private static bool leido;

    // Encendido a mano (menu del Editor o una prueba).
    public static bool Forzado
    {
        get
        {
            if (!leido)
            {
                leido = true;
#if UNITY_EDITOR
                forzado = UnityEditor.EditorPrefs.GetBool(ClaveEditor, false);
#endif
            }
            return forzado;
        }
        set
        {
            leido = true;
            forzado = value;
#if UNITY_EDITOR
            UnityEditor.EditorPrefs.SetBool(ClaveEditor, value);
#endif
        }
    }

    // Si hay que dibujar los controles tactiles.
    public static bool Activo => Application.isMobilePlatform || Forzado;

    // Si el juego esta en un movil de verdad (para lo que no se puede fingir:
    // orientacion de la pantalla, calidad, zona segura...).
    public static bool EnMovilDeVerdad => Application.isMobilePlatform;
}
