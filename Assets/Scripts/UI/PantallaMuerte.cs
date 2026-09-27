using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Pantalla de muerte: la imagen se funde a negro, aparece "HAS MUERTO" en rojo y,
// debajo, una frase (una burla del jefe si se muere contra el, o una frase de la
// cueva). Se queda en negro hasta que el jugador pulsa cualquier boton; entonces
// el player reaparece detras y la pantalla se abre.
//
// GameManager espera con EsperarContinuar() antes de hacer reaparecer al player.
public class PantallaMuerte : MonoBehaviour
{
    // Burlas del jefe que se esta combatiendo (las pone su arena). Si no hay, las
    // del Espectro Carmesi.
    public static string[] BurlasJefe;
    // Frases del nivel (las pone DatosNivel). Si no hay, las de la cueva.
    public static string[] FrasesNivel;

    private static readonly string[] BurlasWraith =
    {
        "«Tu sangre ya me pertenece.»",
        "«Vuelve. La niebla siempre te trae de nuevo a mí.»",
        "«¿Eso era todo tu acero?»",
        "«Otro caballero más para adornar mi cueva.»",
        "«Arrodíllate. Ya sabes cómo se hace.»",
        "«Tu mano tiembla. Puedo olerlo.»",
        "«Cada vez que caes, mi hambre crece.»",
        "«Ni siquiera has visto mi verdadera forma.»",
        "«Corre hacia la hoguera. Te estaré esperando.»",
    };

    private static readonly string[] FrasesCueva =
    {
        "La cueva no perdona los pasos en falso.",
        "Las sombras recuerdan cada error.",
        "Levántate, caballero. Aún no has terminado.",
        "Algo carmesí se alimenta de tu caída.",
    };

    // Tiempo minimo en negro antes de admitir el boton (que no se salte sin leer).
    private const float EsperaMinima = 1.5f;

    private static PantallaMuerte instancia;
    private CanvasGroup grupo;
    private TextMeshProUGUI titulo, frase, aviso;
    private bool activa, puedeContinuar, continuar;

    // Mientras se ve (desde la muerte hasta que se abre otra vez).
    public static bool Activa => instancia != null && instancia.activa;

    public static void Mostrar(bool contraJefe)
    {
        if (instancia == null) instancia = Crear();
        string[] lista = contraJefe ? (BurlasJefe != null && BurlasJefe.Length > 0 ? BurlasJefe : BurlasWraith)
                                    : (FrasesNivel != null && FrasesNivel.Length > 0 ? FrasesNivel : FrasesCueva);
        instancia.frase.text = lista[Random.Range(0, lista.Length)];
        instancia.activa = true;
        instancia.puedeContinuar = false;
        instancia.continuar = false;
        instancia.StopAllCoroutines();
        instancia.StartCoroutine(instancia.Entrar());
    }

    // Espera a que el jugador pulse un boton (lo usa GameManager antes de reaparecer).
    public static IEnumerator EsperarContinuar()
    {
        while (instancia != null && instancia.activa && !instancia.continuar) yield return null;
    }

    // El player ya ha reaparecido detras: se abre la pantalla.
    public static void Ocultar()
    {
        if (instancia == null || !instancia.activa) return;
        instancia.StopAllCoroutines();
        instancia.StartCoroutine(instancia.Salir());
    }

    private IEnumerator Entrar()
    {
        aviso.alpha = 0f;
        const float entrada = 1f;
        for (float t = 0f; t < entrada; t += Time.unscaledDeltaTime)
        {
            float u = t / entrada;
            grupo.alpha = u;
            // El titulo se abre despacio, como en los Souls.
            titulo.characterSpacing = Mathf.Lerp(4f, 22f, u);
            titulo.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.92f, 1f, u);
            yield return null;
        }
        grupo.alpha = 1f;
        yield return new WaitForSecondsRealtime(EsperaMinima - entrada * 0.5f);
        puedeContinuar = true;

        // En pruebas automaticas no hay nadie para pulsar: continua solo.
        float espera = 0f;
        while (!continuar)
        {
            espera += Time.unscaledDeltaTime;
            aviso.alpha = 0.45f + 0.45f * Mathf.Sin(Time.unscaledTime * 3f);
            titulo.characterSpacing = Mathf.MoveTowards(titulo.characterSpacing, 30f, Time.unscaledDeltaTime * 2f);
            if (Application.isBatchMode && espera > 1f) continuar = true;
            yield return null;
        }
        aviso.alpha = 0f;
    }

    private IEnumerator Salir()
    {
        aviso.alpha = 0f;
        yield return new WaitForSecondsRealtime(0.3f);
        const float salida = 1f;
        for (float t = 0f; t < salida; t += Time.unscaledDeltaTime)
        {
            grupo.alpha = 1f - t / salida;
            yield return null;
        }
        grupo.alpha = 0f;
        activa = false;
    }

    private void Update()
    {
        if (!activa || !puedeContinuar || continuar) return;
        if (CualquierBoton()) continuar = true;
    }

    private static bool CualquierBoton()
    {
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) return true;
        Mouse m = Mouse.current;
        if (m != null && (m.leftButton.wasPressedThisFrame || m.rightButton.wasPressedThisFrame)) return true;
        Gamepad g = Gamepad.current;
        if (g != null)
            foreach (var control in g.allControls)
                if (control is UnityEngine.InputSystem.Controls.ButtonControl b && b.wasPressedThisFrame) return true;
        return false;
    }

    private static PantallaMuerte Crear()
    {
        GameObject go = new GameObject("PantallaMuerte");
        DontDestroyOnLoad(go);
        // Si se cambia de escena con la pantalla puesta (salir al menu), se quita.
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (_, _) =>
        {
            if (instancia == null) return;
            instancia.StopAllCoroutines();
            instancia.grupo.alpha = 0f;
            instancia.activa = false;
        };
        Canvas c = go.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 90;
        CanvasScaler cs = go.AddComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920, 1080);
        cs.matchWidthOrHeight = 0.5f;
        PantallaMuerte p = go.AddComponent<PantallaMuerte>();
        p.grupo = go.AddComponent<CanvasGroup>();
        p.grupo.alpha = 0f;
        p.grupo.blocksRaycasts = false;
        p.grupo.interactable = false;

        // Negro total: solo se lee el mensaje.
        Image velo = new GameObject("Negro").AddComponent<Image>();
        velo.transform.SetParent(go.transform, false);
        velo.color = Color.black;
        velo.rectTransform.anchorMin = Vector2.zero;
        velo.rectTransform.anchorMax = Vector2.one;
        velo.rectTransform.offsetMin = velo.rectTransform.offsetMax = Vector2.zero;

        p.titulo = Texto(go.transform, "HAS MUERTO", 120, new Color(0.62f, 0.05f, 0.08f), new Vector2(0.5f, 0.55f));
        p.frase = Texto(go.transform, "", 34, new Color(0.85f, 0.78f, 0.74f), new Vector2(0.5f, 0.42f));
        p.frase.fontStyle = FontStyles.Italic;
        p.aviso = Texto(go.transform, "Pulsa cualquier botón para continuar", 26, new Color(0.7f, 0.66f, 0.62f), new Vector2(0.5f, 0.16f));
        p.aviso.characterSpacing = 6f;
        return p;
    }

    private static TextMeshProUGUI Texto(Transform padre, string texto, float tamano, Color color, Vector2 ancla)
    {
        TextMeshProUGUI t = new GameObject("Texto").AddComponent<TextMeshProUGUI>();
        t.transform.SetParent(padre, false);
        t.text = texto;
        t.fontSize = tamano;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.enableWordWrapping = false;
        t.rectTransform.anchorMin = t.rectTransform.anchorMax = ancla;
        t.rectTransform.sizeDelta = new Vector2(1800f, 160f);
        return t;
    }
}
