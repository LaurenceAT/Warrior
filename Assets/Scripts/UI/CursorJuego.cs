using UnityEngine;

// Cursor del raton: oculto mientras se juega, visible en los menus.
// Se crea solo al arrancar el juego y vive entre escenas.
public class CursorJuego : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Crear()
    {
        var go = new GameObject("CursorJuego");
        go.hideFlags = HideFlags.HideInHierarchy;
        DontDestroyOnLoad(go);
        go.AddComponent<CursorJuego>();
    }

    private void LateUpdate()
    {
        bool visible = DebeVerseCursor();
        if (Cursor.visible != visible) Cursor.visible = visible;
        if (Cursor.lockState != CursorLockMode.None) Cursor.lockState = CursorLockMode.None;
    }

    // Sin GameManager no hay partida (menu principal, pantallas sueltas): cursor visible.
    private static bool DebeVerseCursor()
    {
        var gm = GameManager.Instance;
        if (gm == null) return true;
        if (PantallaCarga.Cargando || Cinematica.Activa || PantallaMuerte.Activa) return false;
        return gm.IsPaused || MenuPausa.LibroAbierto || MenuHoguera.Abierto || TiendaTotem.Abierta
            || PantallaDesafio.Abierta || RuedaImbuir.Abierta || RuedaPociones.Abierta;
    }
}
