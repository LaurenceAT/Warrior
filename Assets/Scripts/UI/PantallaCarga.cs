using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Cambio de nivel con fundido a negro y pantalla de carga.
//   1. La pantalla se funde a negro.
//   2. Sale "Cargando nivel..." con una barra.
//   3. El nivel siguiente se carga en segundo plano (LoadSceneAsync) y la barra
//      muestra el avance real de esa carga; el juego no se congela mientras.
//   4. Al terminar, se activa la escena nueva y el negro se aclara.
// Sobrevive al cambio de escena y se destruye sola al acabar.
public class PantallaCarga : MonoBehaviour
{
    public static bool Cargando { get; private set; }

    private CanvasGroup grupo;
    private CanvasGroup grupoTexto;
    private RectTransform relleno;
    private TextMeshProUGUI textoPorcentaje;

    // Nombre de la escena (tal como sale en Build Settings) o vacio para la
    // siguiente de la lista. Si no hay siguiente, vuelve a la primera.
    public static void Cargar(string escena, float fundido = 0.6f)
    {
        if (Cargando) return;
        Crear().StartCoroutine(Secuencia(IndiceDestino(escena), fundido));
    }

    public static void Cargar(int indice, float fundido = 0.6f)
    {
        if (Cargando) return;
        Crear().StartCoroutine(Secuencia(Mathf.Clamp(indice, 0, SceneManager.sceneCountInBuildSettings - 1), fundido));
    }

    // Nombre de la escena a la que llevaria Cargar(escena).
    public static string NombreDestino(string escena) =>
        System.IO.Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(IndiceDestino(escena)));

    public static int IndiceDestino(string escena)
    {
        int indice;
        if (!string.IsNullOrEmpty(escena)) indice = SceneUtility.GetBuildIndexByScenePath(escena);
        else indice = SceneManager.GetActiveScene().buildIndex + 1;
        if (indice < 0 && !string.IsNullOrEmpty(escena))
        {
            // Solo el nombre, sin ruta: se busca en la lista de la build.
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
                if (System.IO.Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(i)) == escena) { indice = i; break; }
            if (indice < 0) Debug.LogWarning("[Carga] La escena '" + escena + "' no esta en Build Settings; se carga la siguiente.");
        }
        if (indice < 0) indice = SceneManager.GetActiveScene().buildIndex + 1;
        if (indice >= SceneManager.sceneCountInBuildSettings) indice = 0;
        return indice;
    }

    private static IEnumerator Secuencia(int indice, float fundido)
    {
        PantallaCarga p = instancia;
        Cargando = true;

        // 1. Fundido a negro.
        for (float t = 0f; t < fundido; t += Time.unscaledDeltaTime)
        {
            p.grupo.alpha = t / fundido;
            AudioListener.volume = Mathf.Lerp(1f, 0f, t / fundido) * volumenInicial;
            yield return null;
        }
        p.grupo.alpha = 1f;
        AudioListener.volume = 0f;
        Time.timeScale = 1f;

        // 2. Texto y barra.
        for (float t = 0f; t < 0.25f; t += Time.unscaledDeltaTime)
        {
            p.grupoTexto.alpha = t / 0.25f;
            yield return null;
        }
        p.grupoTexto.alpha = 1f;

        // 3. Carga real en segundo plano. Unity da el avance de 0 a 0.9 mientras
        //    carga, y se queda en 0.9 esperando permiso para activar la escena.
        AsyncOperation op = SceneManager.LoadSceneAsync(indice);
        op.allowSceneActivation = false;
        float mostrado = 0f;
        while (op.progress < 0.9f)
        {
            float real = Mathf.Clamp01(op.progress / 0.9f);
            // La barra sigue al avance real (sin pasarse nunca de el).
            mostrado = Mathf.Min(real, Mathf.MoveTowards(mostrado, real, Time.unscaledDeltaTime * 3f));
            p.Barra(mostrado);
            yield return null;
        }
        // Carga terminada: la barra se completa.
        while (mostrado < 1f)
        {
            mostrado = Mathf.MoveTowards(mostrado, 1f, Time.unscaledDeltaTime * 4f);
            p.Barra(mostrado);
            yield return null;
        }
        yield return new WaitForSecondsRealtime(0.15f);

        // 4. Activar la escena y aclarar.
        op.allowSceneActivation = true;
        while (!op.isDone) yield return null;
        yield return null;
        p.grupoTexto.alpha = 0f;
        for (float t = 0f; t < fundido; t += Time.unscaledDeltaTime)
        {
            p.grupo.alpha = 1f - t / fundido;
            AudioListener.volume = Mathf.Lerp(0f, 1f, t / fundido) * volumenInicial;
            yield return null;
        }
        AudioListener.volume = volumenInicial;
        Cargando = false;
        Destroy(p.gameObject);
    }

    private void Barra(float k)
    {
        relleno.anchorMax = new Vector2(Mathf.Clamp01(k), 1f);
        textoPorcentaje.text = Mathf.RoundToInt(k * 100f) + "%";
    }

    // ------------------------------------------------------------------ Construccion

    private static PantallaCarga instancia;
    private static float volumenInicial = 1f;

    private static PantallaCarga Crear()
    {
        volumenInicial = AudioListener.volume > 0f ? AudioListener.volume : 1f;
        GameObject go = new GameObject("PantallaCarga");
        DontDestroyOnLoad(go);
        Canvas c = go.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 1000;
        CanvasScaler cs = go.AddComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920, 1080);
        cs.matchWidthOrHeight = 0.5f;

        PantallaCarga p = go.AddComponent<PantallaCarga>();
        p.grupo = go.AddComponent<CanvasGroup>();
        p.grupo.alpha = 0f;
        p.grupo.blocksRaycasts = true;

        Image negro = Caja("Negro", go.transform, Color.black);
        Estirar(negro.rectTransform);

        GameObject textos = new GameObject("Textos", typeof(RectTransform));
        textos.transform.SetParent(go.transform, false);
        Estirar((RectTransform)textos.transform);
        p.grupoTexto = textos.AddComponent<CanvasGroup>();
        p.grupoTexto.alpha = 0f;

        TextMeshProUGUI titulo = Texto("Cargando nivel...", textos.transform, 44, new Color(0.9f, 0.88f, 0.84f));
        RectTransform rt = titulo.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(900f, 70f);
        rt.anchoredPosition = new Vector2(0f, 50f);

        Image fondo = Caja("Barra", textos.transform, new Color(1f, 1f, 1f, 0.12f));
        RectTransform rf = fondo.rectTransform;
        rf.anchorMin = rf.anchorMax = new Vector2(0.5f, 0.5f);
        rf.sizeDelta = new Vector2(640f, 12f);
        rf.anchoredPosition = new Vector2(0f, -20f);
        Image lleno = Caja("Relleno", fondo.transform, new Color(0.62f, 0.8f, 1f, 1f));
        p.relleno = lleno.rectTransform;
        p.relleno.anchorMin = Vector2.zero;
        p.relleno.anchorMax = new Vector2(0f, 1f);
        p.relleno.offsetMin = p.relleno.offsetMax = Vector2.zero;

        p.textoPorcentaje = Texto("0%", textos.transform, 26, new Color(0.7f, 0.72f, 0.78f));
        RectTransform rp = p.textoPorcentaje.rectTransform;
        rp.anchorMin = rp.anchorMax = new Vector2(0.5f, 0.5f);
        rp.sizeDelta = new Vector2(300f, 40f);
        rp.anchoredPosition = new Vector2(0f, -60f);

        instancia = p;
        return p;
    }

    private static Image Caja(string nombre, Transform padre, Color color)
    {
        Image i = new GameObject(nombre).AddComponent<Image>();
        i.transform.SetParent(padre, false);
        i.color = color;
        i.raycastTarget = false;
        return i;
    }

    private static TextMeshProUGUI Texto(string texto, Transform padre, float tamano, Color color)
    {
        TextMeshProUGUI t = new GameObject("Texto").AddComponent<TextMeshProUGUI>();
        t.transform.SetParent(padre, false);
        t.text = texto;
        t.fontSize = tamano;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.enableWordWrapping = false;
        t.raycastTarget = false;
        return t;
    }

    private static void Estirar(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
    }
}
