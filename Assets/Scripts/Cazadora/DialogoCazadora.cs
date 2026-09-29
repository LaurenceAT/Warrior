using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Cuadro de dialogo de la Cazadora: el mismo estilo que el de las estatuas
// (marco dorado, letras que aparecen de a poco con su "tic"), con respuestas
// para elegir (teclado o raton).
//   - F, Enter, Espacio o clic: muestra todo el texto / pasa a la siguiente.
//   - Mantener F: salta el dialogo entero.
// El juego no se pausa: el player se queda quieto (Cinematica).
public class DialogoCazadora : MonoBehaviour
{
    private static DialogoCazadora inst;

    // Se mantuvo F: lo que queda del dialogo se salta.
    public bool Saltado { get; private set; }

    private CanvasGroup grupo;
    private TextMeshProUGUI titulo, texto, ayuda;
    private Image barraSaltar;
    private RectTransform opciones;
    private readonly List<Button> botones = new List<Button>();
    private float mantenido;
    private int eleccion = -1;
    private AudioSource escritura;
    // El sonido de las letras (lo pone la arena, de los ajustes).
    public AudioClip sonidoLetras;
    public float volumenLetras = 0.35f;

    public static DialogoCazadora Crear()
    {
        if (inst != null) return inst;
        GameObject go = new GameObject("DialogoCazadora");
        inst = go.AddComponent<DialogoCazadora>();
        inst.Montar(go);
        return inst;
    }

    private void OnDestroy()
    {
        if (inst == this) inst = null;
    }

    public void Abrir()
    {
        Saltado = false;
        mantenido = 0f;
        StopAllCoroutines();
        StartCoroutine(Fundido(1f));
    }

    public void Cerrar()
    {
        Escribiendo(false);
        LimpiarOpciones();
        StopAllCoroutines();
        if (isActiveAndEnabled) StartCoroutine(Fundido(0f));
    }

    // Una frase con letras que aparecen de a poco. Termina al pulsar para pasar
    // (o al mantener F, que salta todo).
    public IEnumerator Decir(string quien, string linea, float letrasPorSegundo, float mantenerParaSaltar)
    {
        if (Saltado) yield break;
        titulo.text = string.IsNullOrEmpty(quien) ? "" : quien.ToUpper();
        texto.text = linea;
        texto.ForceMeshUpdate();
        int total = texto.textInfo.characterCount;
        texto.maxVisibleCharacters = 0;
        float letras = 0f;
        int sonada = 0;
        yield return null;
        while (true)
        {
            float dt = Time.unscaledDeltaTime;
            if (Saltado || ActualizarSalto(dt, mantenerParaSaltar)) { Escribiendo(false); yield break; }
            bool pulso = Pulsado();
            if (texto.maxVisibleCharacters < total)
            {
                letras += letrasPorSegundo * dt;
                int visibles = Mathf.Min(total, Mathf.FloorToInt(letras));
                if (visibles > texto.maxVisibleCharacters)
                {
                    texto.maxVisibleCharacters = visibles;
                    if (sonidoLetras != null) Escribiendo(true);
                    else if (visibles - sonada >= 2 && !EsEspacio(visibles - 1))
                    {
                        sonada = visibles;
                        Sonido.Reproducir("menu_mover", 0.2f, Random.Range(0.8f, 0.92f));
                    }
                }
                if (pulso) { texto.maxVisibleCharacters = total; letras = total; }
                ayuda.text = "F: mostrar todo      Mantén F: saltar";
            }
            else
            {
                Escribiendo(false);
                ayuda.text = "F: continuar      Mantén F: saltar";
                if (pulso) break;
            }
            yield return null;
        }
    }

    // El sonido de escribir suena en bucle mientras aparecen las letras.
    private void Escribiendo(bool si)
    {
        if (sonidoLetras == null) return;
        if (escritura == null)
        {
            escritura = gameObject.AddComponent<AudioSource>();
            escritura.loop = true;
            escritura.playOnAwake = false;
        }
        escritura.clip = sonidoLetras;
        escritura.volume = volumenLetras * ControlVolumen.Efectos;
        if (si && !escritura.isPlaying) escritura.Play();
        else if (!si && escritura.isPlaying) escritura.Stop();
    }

    // Respuestas del player. Devuelve el indice elegido en "alElegir".
    public IEnumerator Elegir(string[] respuestas, System.Action<int> alElegir)
    {
        LimpiarOpciones();
        eleccion = -1;
        texto.maxVisibleCharacters = 99999;
        ayuda.text = "Elige tu respuesta";
        grupo.interactable = grupo.blocksRaycasts = true;
        for (int i = 0; i < respuestas.Length; i++)
        {
            int k = i;
            Button b = EstiloMenu.Opcion(opciones, "[" + respuestas[i] + "]", () => eleccion = k, 58f, 30f);
            botones.Add(b);
        }
        yield return null;
        if (EventSystem.current != null && botones.Count > 0) EventSystem.current.SetSelectedGameObject(botones[0].gameObject);
        while (eleccion < 0 && !Saltado)
        {
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null && botones.Count > 0)
                EventSystem.current.SetSelectedGameObject(botones[0].gameObject);
            yield return null;
        }
        grupo.interactable = grupo.blocksRaycasts = false;
        LimpiarOpciones();
        alElegir?.Invoke(eleccion);
    }

    // ------------------------------------------------------------------ Entrada

    private bool Pulsado()
    {
        Keyboard k = Keyboard.current;
        Mouse m = Mouse.current;
        Gamepad g = Gamepad.current;
        return (k != null && (k.fKey.wasReleasedThisFrame && mantenido < 0.25f || k.enterKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame))
               || (m != null && m.leftButton.wasPressedThisFrame)
               || (g != null && g.buttonSouth.wasPressedThisFrame);
    }

    private bool ActualizarSalto(float dt, float necesario)
    {
        Keyboard k = Keyboard.current;
        bool f = k != null && k.fKey.isPressed;
        mantenido = f ? mantenido + dt : 0f;
        barraSaltar.fillAmount = necesario > 0f ? Mathf.Clamp01((mantenido - 0.25f) / Mathf.Max(0.05f, necesario - 0.25f)) : 0f;
        if (mantenido >= necesario && necesario > 0f) { Saltado = true; return true; }
        return false;
    }

    private bool EsEspacio(int i)
    {
        if (i < 0 || i >= texto.textInfo.characterCount) return true;
        return char.IsWhiteSpace(texto.textInfo.characterInfo[i].character);
    }

    private void LimpiarOpciones()
    {
        foreach (Button b in botones) if (b != null) Destroy(b.gameObject);
        botones.Clear();
    }

    private IEnumerator Fundido(float hasta)
    {
        float desde = grupo.alpha;
        for (float t = 0f; t < 0.2f; t += Time.unscaledDeltaTime)
        {
            grupo.alpha = Mathf.Lerp(desde, hasta, t / 0.2f);
            yield return null;
        }
        grupo.alpha = hasta;
    }

    // ------------------------------------------------------------------ Construccion

    private void Montar(GameObject go)
    {
        grupo = EstiloMenu.Lienzo(go, 91);
        // El velo del lienzo, casi transparente: se sigue viendo el bosque.
        Transform velo = go.transform.Find("Velo");
        if (velo != null) velo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.12f);

        RectTransform panel = EstiloMenu.Panel(go.transform, null, new Vector2(1180f, 300f), new Vector2(0f, -300f));
        Image interior = EstiloMenu.Caja("FiloInterior", panel, EstiloMenu.FiloTenue);
        EstiloMenu.Estirar(interior.rectTransform, 10f);
        Image piedra = EstiloMenu.Caja("Piedra", interior.transform, EstiloMenu.Fondo);
        EstiloMenu.Estirar(piedra.rectTransform, 1f);

        titulo = EstiloMenu.Texto("", piedra.transform, 26, new Color(0.72f, 0.86f, 0.7f));
        titulo.fontStyle = FontStyles.Bold;
        titulo.characterSpacing = 10f;
        EstiloMenu.Arriba(titulo.rectTransform, -16f, 36f);
        EstiloMenu.Separador(piedra.rectTransform, -58f, 0.5f);

        texto = EstiloMenu.Texto("", piedra.transform, 30, EstiloMenu.TextoElegido);
        texto.fontStyle = FontStyles.Italic;
        texto.enableWordWrapping = true;
        texto.alignment = TextAlignmentOptions.TopLeft;
        texto.lineSpacing = 8f;
        RectTransform rt = texto.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(56f, 50f);
        rt.offsetMax = new Vector2(-56f, -76f);

        ayuda = EstiloMenu.Texto("", piedra.transform, 20, new Color(0.8f, 0.78f, 0.75f, 0.55f));
        ayuda.alignment = TextAlignmentOptions.BottomRight;
        RectTransform ra = ayuda.rectTransform;
        ra.anchorMin = new Vector2(0f, 0f);
        ra.anchorMax = new Vector2(1f, 0f);
        ra.pivot = new Vector2(0.5f, 0f);
        ra.sizeDelta = new Vector2(-60f, 30f);
        ra.anchoredPosition = new Vector2(0f, 12f);

        // Barra que se llena al mantener F.
        barraSaltar = EstiloMenu.Caja("Saltar", piedra.transform, new Color(0.75f, 0.62f, 0.4f, 0.8f));
        barraSaltar.type = Image.Type.Filled;
        barraSaltar.fillMethod = Image.FillMethod.Horizontal;
        barraSaltar.sprite = DibujosCazadora.Pixel();
        barraSaltar.fillAmount = 0f;
        RectTransform rb = barraSaltar.rectTransform;
        rb.anchorMin = new Vector2(0f, 0f);
        rb.anchorMax = new Vector2(1f, 0f);
        rb.pivot = new Vector2(0.5f, 0f);
        rb.sizeDelta = new Vector2(0f, 4f);

        // Respuestas, sobre el cuadro.
        opciones = new GameObject("Opciones", typeof(RectTransform)).GetComponent<RectTransform>();
        opciones.SetParent(go.transform, false);
        opciones.anchorMin = opciones.anchorMax = new Vector2(0.5f, 0.5f);
        opciones.sizeDelta = new Vector2(620f, 140f);
        opciones.anchoredPosition = new Vector2(0f, -40f);
        VerticalLayoutGroup v = opciones.gameObject.AddComponent<VerticalLayoutGroup>();
        v.spacing = 10f;
        v.childControlWidth = v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;
    }
}
