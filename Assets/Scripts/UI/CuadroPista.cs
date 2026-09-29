using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Cuadro de texto de las estatuas (la F al lado de una). Estilo de los menus:
// marco con filo dorado sobre fondo oscuro, titulo arriba y el texto que aparece
// letra a letra (maquina de escribir) con un "tic" muy suave.
//   - F mientras escribe: sale todo el texto. F otra vez: se cierra.
//   - Esc: se cierra (y no abre la pausa).
//   - Si el player recibe dano, se cierra solo.
// El juego no se para: el player se queda quieto (lo mira PlayerControler).
public class CuadroPista : MonoBehaviour
{
    private static CuadroPista instancia;

    // Abierto (o cerrado en este mismo fotograma: la F o el Esc que lo cierran
    // no deben usarse tambien para otra cosa).
    public static bool Abierto => instancia != null && (instancia.abierto || instancia.cerradoEnFrame == Time.frameCount);
    public static float UltimoCierre { get; private set; } = -10f;

    [Tooltip("Letras por segundo.")]
    public float velocidad = 38f;
    [Tooltip("Suena el tic cada tantas letras (0 = sin sonido).")]
    public int letrasPorSonido = 2;
    [Range(0f, 1f)] public float volumenSonido = 0.22f;

    private CanvasGroup grupo;
    private TextMeshProUGUI titulo, texto, ayuda;
    private bool abierto;
    private int cerradoEnFrame = -1;
    private float letras;
    private int total, ultimaSonada;
    private int abiertoEnFrame;
    private PlayerControler player;
    private int vidaAlAbrir;
    private System.Action alCerrar;

    public static void Mostrar(string tituloTexto, string cuerpo, float letrasPorSegundo, PlayerControler p, System.Action cerrado = null)
    {
        if (instancia == null) instancia = Crear();
        CuadroPista c = instancia;
        if (letrasPorSegundo > 0f) c.velocidad = letrasPorSegundo;
        c.titulo.text = string.IsNullOrEmpty(tituloTexto) ? "" : tituloTexto.ToUpper();
        c.texto.text = cuerpo;
        // Activo antes de contar las letras: oculto, TextMeshPro da 0.
        c.gameObject.SetActive(true);
        c.texto.ForceMeshUpdate();
        c.total = c.texto.textInfo.characterCount;
        c.letras = 0f;
        c.ultimaSonada = 0;
        c.texto.maxVisibleCharacters = 0;
        c.player = p;
        c.vidaAlAbrir = p != null ? p.VidaActual : 0;
        c.alCerrar = cerrado;
        c.abierto = true;
        c.abiertoEnFrame = Time.frameCount;
        c.StopAllCoroutines();
        c.StartCoroutine(c.Fundido(1f));
        SonidoMenu.Abrir();
    }

    public static void Cerrar()
    {
        if (instancia == null || !instancia.abierto) return;
        CuadroPista c = instancia;
        c.abierto = false;
        c.cerradoEnFrame = Time.frameCount;
        UltimoCierre = Time.unscaledTime;
        c.StopAllCoroutines();
        c.StartCoroutine(c.Fundido(0f));
        System.Action a = c.alCerrar;
        c.alCerrar = null;
        a?.Invoke();
    }

    private bool Escribiendo => texto.maxVisibleCharacters < total;

    private void Update()
    {
        if (!abierto) return;

        // Recibir un golpe (o morir) lo cierra.
        if (player == null || player.VidaActual < vidaAlAbrir) { Cerrar(); return; }

        if (Escribiendo)
        {
            letras += velocidad * Time.unscaledDeltaTime;
            int visibles = Mathf.Min(total, Mathf.FloorToInt(letras));
            if (visibles > texto.maxVisibleCharacters)
            {
                texto.maxVisibleCharacters = visibles;
                if (letrasPorSonido > 0 && visibles - ultimaSonada >= letrasPorSonido && !EsEspacio(visibles - 1))
                {
                    ultimaSonada = visibles;
                    Sonido.Reproducir("menu_mover", volumenSonido, Random.Range(0.95f, 1.1f));
                }
            }
        }
        ayuda.text = Escribiendo ? "F: mostrar todo      Esc: cerrar" : "F / Esc: cerrar";

        // La F que abrio el cuadro no cuenta.
        if (Time.frameCount == abiertoEnFrame) return;
        Keyboard k = Keyboard.current;
        Gamepad g = Gamepad.current;
        bool f = (k != null && k.fKey.wasPressedThisFrame) || (g != null && g.buttonSouth.wasPressedThisFrame);
        bool esc = (k != null && k.escapeKey.wasPressedThisFrame) || (g != null && g.buttonEast.wasPressedThisFrame);
        if (esc) { Cerrar(); return; }
        if (f)
        {
            if (Escribiendo) { texto.maxVisibleCharacters = total; letras = total; }
            else Cerrar();
        }
    }

    private bool EsEspacio(int i)
    {
        if (i < 0 || i >= texto.textInfo.characterCount) return true;
        return char.IsWhiteSpace(texto.textInfo.characterInfo[i].character);
    }

    private System.Collections.IEnumerator Fundido(float hasta)
    {
        float desde = grupo.alpha;
        for (float t = 0f; t < 0.15f; t += Time.unscaledDeltaTime)
        {
            grupo.alpha = Mathf.Lerp(desde, hasta, t / 0.15f);
            yield return null;
        }
        grupo.alpha = hasta;
        if (hasta <= 0f) gameObject.SetActive(false);
    }

    private static CuadroPista Crear()
    {
        GameObject go = new GameObject("CuadroPista");
        DontDestroyOnLoad(go);
        CuadroPista c = go.AddComponent<CuadroPista>();

        // Lienzo propio sin velo: el nivel se sigue viendo detras. Por debajo de
        // la pausa (100) y por encima del HUD.
        Canvas cv = go.AddComponent<Canvas>();
        cv.renderMode = RenderMode.ScreenSpaceOverlay;
        cv.sortingOrder = 90;
        CanvasScaler cs = go.AddComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920, 1080);
        cs.matchWidthOrHeight = 0.5f;
        c.grupo = go.AddComponent<CanvasGroup>();
        c.grupo.alpha = 0f;
        c.grupo.interactable = false;
        c.grupo.blocksRaycasts = false;

        // Marco: filo dorado, fondo de piedra oscura y un filo interior tenue.
        RectTransform panel = EstiloMenu.Panel(go.transform, null, new Vector2(1180f, 330f), new Vector2(0f, -330f));
        Image interior = EstiloMenu.Caja("FiloInterior", panel, EstiloMenu.FiloTenue);
        EstiloMenu.Estirar(interior.rectTransform, 10f);
        Image piedra = EstiloMenu.Caja("Piedra", interior.transform, EstiloMenu.Fondo);
        EstiloMenu.Estirar(piedra.rectTransform, 1f);
        // Rombos en las esquinas, como remaches.
        foreach (Vector2 esquina in new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) })
        {
            Image r = EstiloMenu.Caja("Remache", panel, EstiloMenu.Filo);
            r.rectTransform.anchorMin = r.rectTransform.anchorMax = esquina;
            r.rectTransform.sizeDelta = new Vector2(12f, 12f);
            r.rectTransform.anchoredPosition = new Vector2(esquina.x > 0f ? -18f : 18f, esquina.y > 0f ? -18f : 18f);
            r.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        }

        c.titulo = EstiloMenu.Texto("", piedra.transform, 26, EstiloMenu.Titulo);
        c.titulo.fontStyle = FontStyles.Bold;
        c.titulo.characterSpacing = 10f;
        EstiloMenu.Arriba(c.titulo.rectTransform, -18f, 36f);
        EstiloMenu.Separador(piedra.rectTransform, -62f, 0.5f);

        c.texto = EstiloMenu.Texto("", piedra.transform, 30, EstiloMenu.TextoElegido);
        c.texto.enableWordWrapping = true;
        c.texto.alignment = TextAlignmentOptions.TopLeft;
        c.texto.lineSpacing = 8f;
        RectTransform rt = c.texto.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(56f, 54f);
        rt.offsetMax = new Vector2(-56f, -84f);

        c.ayuda = EstiloMenu.Texto("", piedra.transform, 20, new Color(0.8f, 0.78f, 0.75f, 0.55f));
        c.ayuda.alignment = TextAlignmentOptions.BottomRight;
        RectTransform ra = c.ayuda.rectTransform;
        ra.anchorMin = new Vector2(0f, 0f);
        ra.anchorMax = new Vector2(1f, 0f);
        ra.pivot = new Vector2(0.5f, 0f);
        ra.sizeDelta = new Vector2(-60f, 30f);
        ra.anchoredPosition = new Vector2(0f, 14f);

        go.SetActive(false);
        return c;
    }
}
