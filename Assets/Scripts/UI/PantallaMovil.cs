using UnityEngine;
using UnityEngine.UI;

// La pantalla en la version de movil: horizontal siempre, y la interfaz bien
// puesta en los moviles alargados (20:9 y parecidos).
//
// Por que hace falta lo segundo: los lienzos estan hechos para 1920x1080 y se
// escalan con "match 0.5", que reparte el ajuste entre el ancho y el alto. En
// una pantalla mas alargada que 16:9 eso ENCOGE el alto util (en un 20:9 el
// lienzo pasa de 1080 a unos 966 de alto), y todo lo de arriba y lo de abajo se
// junta. Con "match 1" se respeta el alto de siempre y lo que sobra se va a los
// lados, que es justo lo que se quiere en un movil tumbado.
//
// En pantallas MAS ESTRECHAS que 16:9 (una tablet 4:3) pasa lo contrario, asi
// que ahi se deja el 0.5 de siempre. En PC no se toca nada.
public class PantallaMovil : MonoBehaviour
{
    private static PantallaMovil instancia;
    private float proxima;

    // Cuanto se tarda en mirar si hay lienzos nuevos (los menus se crean segun
    // hacen falta, no todos al empezar).
    private const float CadaCuanto = 0.4f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Orientacion()
    {
        if (!ModoMovil.EnMovilDeVerdad) return;
        Horizontal();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Arranque()
    {
        if (!ModoMovil.Activo || instancia != null) return;
        GameObject go = new GameObject("PantallaMovil");
        DontDestroyOnLoad(go);
        instancia = go.AddComponent<PantallaMovil>();
    }

    // Solo horizontal, en los dos sentidos (para poder cargar el movil por
    // cualquier lado). El vertical queda prohibido.
    public static void Horizontal()
    {
        Screen.autorotateToPortrait = false;
        Screen.autorotateToPortraitUpsideDown = false;
        Screen.autorotateToLandscapeLeft = true;
        Screen.autorotateToLandscapeRight = true;
        Screen.orientation = ScreenOrientation.AutoRotation;
    }

    // Lo que le toca a un lienzo segun la forma de la pantalla.
    public static float Reparto()
    {
        float aspecto = Screen.width / Mathf.Max(1f, (float)Screen.height);
        return aspecto > 1920f / 1080f ? 1f : 0.5f;
    }

    private void Update()
    {
        if (Time.unscaledTime < proxima) return;
        proxima = Time.unscaledTime + CadaCuanto;

        float reparto = Reparto();
        foreach (CanvasScaler cs in FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (cs.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize) continue;
            if (!Mathf.Approximately(cs.matchWidthOrHeight, reparto)) cs.matchWidthOrHeight = reparto;
        }
    }
}
