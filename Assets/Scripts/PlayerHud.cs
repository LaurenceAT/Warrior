using UnityEngine;
using UnityEngine.UI;

// HUD de vida y estamina al estilo de Elden Ring: un rombo a la izquierda y, pegadas
// a el, una barra de vida roja y debajo una de estamina verde, mas fina.
// Todo son Image de color plano, sin sprites.
//
// Se construye solo por codigo. Si en la escena no hay ninguno, el player crea uno
// al aparecer, asi que no hay que montar nada en cada nivel. Si quieres cambiar
// colores o medidas, pon este componente en un objeto vacio de la escena y ajustalo
// ahi: el player usara ese en vez de crear otro.
//
// Extras:
//  - La estamina se apaga y parpadea cuando queda poca.
//  - La barra de vida tiembla con los golpes fuertes.
//  - El HUD se desvanece si no pasa nada durante un rato y vuelve al instante.
//  - Numeros exactos opcionales (desactivados por defecto).
//  - El borde del rombo late con la estamina baja; en el centro va el icono.
//  - La estela de dano baja rapido al principio y frena al final.
//
// Nota: el relleno no usa Image tipo Filled porque Filled no funciona sin sprite.
// En su lugar se estira el ancho del rectangulo con sus anclas, que se ve igual.
public class PlayerHud : MonoBehaviour
{
    [Header("Posicion (pixeles a 1920x1080)")]
    [SerializeField] private Vector2 margin = new Vector2(40f, 40f);

    [Header("Rombo")]
    [SerializeField] private float boxSize = 54f;
    [SerializeField] private bool rotateBox = true;
    [SerializeField] private float boxBorder = 3f;
    [SerializeField] private Color boxBorderColor = new Color(0.78f, 0.72f, 0.58f, 0.9f);
    [SerializeField] private Color boxFillColor = new Color(0.08f, 0.07f, 0.06f, 0.75f);

    [Header("Rombo: icono")]
    // Lado del icono en pixeles. Va recto aunque el rombo este girado. A 38 cabe
    // entero dentro de un rombo de 54 (la mitad de su diagonal).
    [SerializeField] private float iconSize = 38f;

    [Header("Rombo: aviso de estamina")]
    // El borde del rombo late cuando queda poca estamina. El centro es para el icono.
    [SerializeField] private bool staminaWarningInBox = true;
    [SerializeField] private Color warningLowColor = new Color(0.95f, 0.78f, 0.25f, 1f);
    [SerializeField] private Color warningExhaustedColor = new Color(0.75f, 0.2f, 0.15f, 1f);

    [Header("Barra de vida")]
    [SerializeField] private float healthWidth = 420f;
    [SerializeField] private float healthHeight = 12f;
    [SerializeField] private Color healthColor = new Color(0.62f, 0.07f, 0.07f, 1f);
    [SerializeField] private Color trailColor = new Color(0.92f, 0.86f, 0.78f, 0.9f);
    // Cuanto se queda quieta la estela antes de empezar a bajar.
    [SerializeField] private float trailDelay = 0.6f;
    // Rapidez con la que baja: a mas valor, mas rapido. Baja rapido al principio y
    // frena al llegar, en vez de caer a velocidad constante.
    [SerializeField] private float trailSharpness = 3.5f;
    [SerializeField] private Color healFlashColor = new Color(1f, 0.55f, 0.45f, 1f);
    [SerializeField] private float healDuration = 0.35f;

    [Header("Temblor al recibir un golpe fuerte")]
    // Solo tiembla si el golpe quita al menos esta parte de la vida maxima.
    [Range(0f, 1f)] [SerializeField] private float shakeMinDamage = 0.15f;
    [SerializeField] private float shakeDuration = 0.18f;
    // Pixeles de desplazamiento maximo; crece con el tamano del golpe.
    [SerializeField] private float shakeStrength = 6f;

    [Header("Barra de estamina")]
    [SerializeField] private float staminaWidth = 330f;
    [SerializeField] private float staminaHeight = 7f;
    [SerializeField] private Color staminaColor = new Color(0.25f, 0.55f, 0.22f, 1f);
    [SerializeField] private Color staminaExhaustedColor = new Color(0.33f, 0.36f, 0.3f, 1f);
    // Por debajo de esta parte se considera "poca" y la barra avisa.
    [Range(0f, 1f)] [SerializeField] private float lowStaminaThreshold = 0.2f;
    [SerializeField] private Color staminaLowColor = new Color(0.4f, 0.45f, 0.18f, 1f);
    [SerializeField] private float lowStaminaBlinkSpeed = 9f;

    [Header("Desvanecer sin combate")]
    [SerializeField] private bool autoFade = true;
    // Segundos sin que pase nada antes de empezar a desvanecerse.
    [SerializeField] private float fadeDelay = 4f;
    [Range(0f, 1f)] [SerializeField] private float fadedAlpha = 0.3f;
    [SerializeField] private float fadeOutSpeed = 1.5f;
    [SerializeField] private float fadeInSpeed = 8f;
    // Con poca vida no se desvanece: es justo cuando mas importa verla.
    [Range(0f, 1f)] [SerializeField] private float stayVisibleBelowHealth = 0.35f;

    [Header("Numeros (accesibilidad / debug)")]
    [SerializeField] private bool showNumbers;
    [SerializeField] private int numberFontSize = 16;
    [SerializeField] private Color numberColor = new Color(1f, 1f, 1f, 0.9f);

    [Header("Comun")]
    [SerializeField] private Color backColor = new Color(0.05f, 0.05f, 0.05f, 0.65f);
    [SerializeField] private float barGap = 5f;

    private static PlayerHud instancia;

    private CanvasGroup grupo;
    private RectTransform vidaFondo;
    private Vector2 vidaFondoPos;
    private RectTransform vidaRelleno;
    private RectTransform vidaEstela;
    private Image vidaImagen;
    private RectTransform estaminaRelleno;
    private Image estaminaImagen;
    private Image aviso;
    private Image icono;
    private Text textoVida;
    private Text textoEstamina;

    private int vidaActual = 1;
    private int vidaMaxima = 1;
    private float vida = 1f;
    private float vidaMostrada = 1f;
    private float estela = 1f;
    private float esperaEstela;
    private float curando;
    private float curaDesde;

    private float temblor;
    private float fuerzaTemblor;

    private float sinActividad;
    private float estaminaAntes = -1f;

    private PlayerStamina estamina;
    private float buscarEstamina;

    public static PlayerHud Get()
    {
        if (instancia != null) return instancia;

        instancia = FindFirstObjectByType<PlayerHud>();
        if (instancia == null)
            instancia = new GameObject("PlayerHud").AddComponent<PlayerHud>();

        return instancia;
    }

    // Pone el icono del rombo. null lo deja vacio.
    public void SetIcon(Sprite sprite)
    {
        if (icono == null) return;
        icono.sprite = sprite;
        icono.enabled = sprite != null;
    }

    // Activa o quita los numeros en marcha (por ejemplo desde un menu de opciones).
    public void SetShowNumbers(bool mostrar)
    {
        showNumbers = mostrar;
        if (textoVida != null) textoVida.enabled = mostrar;
        if (textoEstamina != null) textoEstamina.enabled = mostrar;
    }

    private void Awake()
    {
        if (instancia != null && instancia != this) { Destroy(gameObject); return; }
        instancia = this;
        Construir();
    }

    private void OnDestroy()
    {
        if (instancia == this) instancia = null;
    }

    // ------------------------------------------------------------------ Vida

    public void SetHealth(int actual, int maximo)
    {
        Guardar(actual, maximo);
        vidaMostrada = vida;
        estela = vida;
        curando = 0f;
        Dibujar();
    }

    // Dano: la roja baja de golpe, la estela se queda un momento y, si el golpe es
    // fuerte, la barra tiembla.
    public void Damage(int actual, int maximo)
    {
        float antes = vidaMostrada;
        Guardar(actual, maximo);

        estela = Mathf.Max(estela, antes);
        vidaMostrada = vida;
        curando = 0f;
        esperaEstela = trailDelay;

        float golpe = antes - vida;
        if (golpe >= shakeMinDamage && golpe > 0f)
        {
            temblor = shakeDuration;
            fuerzaTemblor = shakeStrength * Mathf.Clamp(golpe / Mathf.Max(0.01f, shakeMinDamage), 1f, 2.5f);
        }

        Actividad();
        Dibujar();
    }

    public void Heal(int actual, int maximo)
    {
        curaDesde = vidaMostrada;
        Guardar(actual, maximo);
        curando = healDuration;
        Actividad();
    }

    private void Guardar(int actual, int maximo)
    {
        vidaActual = Mathf.Max(0, actual);
        vidaMaxima = Mathf.Max(1, maximo);
        vida = Mathf.Clamp01((float)vidaActual / vidaMaxima);
    }

    // Algo ha pasado: el HUD vuelve a verse del todo.
    private void Actividad()
    {
        sinActividad = 0f;
    }

    // ------------------------------------------------------------------ Bucle

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;

        ActualizarVida(dt);
        ActualizarEstamina(dt);
        ActualizarTemblor(dt);
        ActualizarFundido(dt);
        ActualizarNumeros();
        Dibujar();
    }

    private void ActualizarVida(float dt)
    {
        if (curando > 0f)
        {
            curando -= dt;
            float k = 1f - Mathf.Clamp01(curando / Mathf.Max(0.01f, healDuration));
            vidaMostrada = Mathf.Lerp(curaDesde, vida, 1f - (1f - k) * (1f - k));
            vidaImagen.color = Color.Lerp(healFlashColor, healthColor, k);
            if (estela < vidaMostrada) estela = vidaMostrada;
        }
        else
        {
            vidaMostrada = vida;
            vidaImagen.color = healthColor;
        }

        // Estela: espera y luego se acerca a la vida real recorriendo cada fotograma
        // una parte de lo que le falta. Asi baja rapido al principio y frena al final.
        if (estela > vidaMostrada)
        {
            if (esperaEstela > 0f)
            {
                esperaEstela -= dt;
            }
            else
            {
                float paso = (estela - vidaMostrada) * (1f - Mathf.Exp(-trailSharpness * dt));
                // Un minimo, para que el final no se eternice.
                estela = Mathf.Max(vidaMostrada, estela - Mathf.Max(paso, 0.03f * dt));
            }
        }
        else estela = vidaMostrada;
    }

    private void ActualizarEstamina(float dt)
    {
        if (estamina == null)
        {
            buscarEstamina -= dt;
            if (buscarEstamina <= 0f)
            {
                buscarEstamina = 0.25f;
                estamina = FindFirstObjectByType<PlayerStamina>();
                estaminaAntes = -1f;
            }
        }
        if (estamina == null) return;

        float f = estamina.Fraction;
        Anclar(estaminaRelleno, f);

        // Gastar o regenerar cuenta como actividad: el HUD se ve mientras cambia.
        if (estaminaAntes >= 0f && Mathf.Abs(f - estaminaAntes) > 0.0001f) Actividad();
        estaminaAntes = f;

        bool poca = f < lowStaminaThreshold;
        float parpadeo = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * lowStaminaBlinkSpeed);

        if (estamina.Exhausted)
            estaminaImagen.color = staminaExhaustedColor;
        else if (poca)
            // Mas apagada y latiendo: ya no da para mucho.
            estaminaImagen.color = Color.Lerp(staminaLowColor, staminaColor, parpadeo * 0.6f);
        else
            estaminaImagen.color = staminaColor;

        if (aviso != null)
        {
            if (estamina.Exhausted)
                aviso.color = warningExhaustedColor;
            else if (poca)
                aviso.color = Color.Lerp(boxBorderColor, warningLowColor, parpadeo);
            else
                aviso.color = boxBorderColor;
        }
    }

    private void ActualizarTemblor(float dt)
    {
        if (vidaFondo == null) return;

        if (temblor > 0f)
        {
            temblor -= dt;
            // Se va calmando mientras dura.
            float k = Mathf.Clamp01(temblor / Mathf.Max(0.01f, shakeDuration));
            Vector2 desvio = new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)) * (fuerzaTemblor * k);
            vidaFondo.anchoredPosition = vidaFondoPos + desvio;
        }
        else
        {
            vidaFondo.anchoredPosition = vidaFondoPos;
        }
    }

    private void ActualizarFundido(float dt)
    {
        if (grupo == null) return;

        if (!autoFade)
        {
            grupo.alpha = 1f;
            return;
        }

        sinActividad += dt;

        bool debeVerse = sinActividad < fadeDelay
                         || vida < stayVisibleBelowHealth
                         || estela > vidaMostrada
                         || curando > 0f;

        float objetivo = debeVerse ? 1f : fadedAlpha;
        float velocidad = debeVerse ? fadeInSpeed : fadeOutSpeed;
        grupo.alpha = Mathf.MoveTowards(grupo.alpha, objetivo, velocidad * dt);
    }

    private void ActualizarNumeros()
    {
        if (!showNumbers || textoVida == null) return;

        textoVida.text = vidaActual + " / " + vidaMaxima;
        if (textoEstamina != null && estamina != null)
            textoEstamina.text = Mathf.CeilToInt(estamina.Current) + " / " + Mathf.RoundToInt(estamina.Max);
    }

    private void Dibujar()
    {
        if (vidaRelleno == null) return;
        Anclar(vidaRelleno, vidaMostrada);
        Anclar(vidaEstela, estela);
    }

    // ------------------------------------------------------------------ Construccion

    private void Construir()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // Por debajo de tu Canvas del juego, para que el menu de pausa lo tape.
        canvas.sortingOrder = -1;

        CanvasScaler escalador = gameObject.AddComponent<CanvasScaler>();
        escalador.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escalador.referenceResolution = new Vector2(1920f, 1080f);
        escalador.matchWidthOrHeight = 0.5f;

        RectTransform raiz = Crear("HUD", transform, out GameObject raizGo);
        raiz.anchorMin = raiz.anchorMax = new Vector2(0f, 1f);
        raiz.pivot = new Vector2(0f, 1f);
        raiz.anchoredPosition = new Vector2(margin.x, -margin.y);

        // El fundido se hace sobre todo el HUD de una vez.
        grupo = raizGo.AddComponent<CanvasGroup>();
        grupo.interactable = false;
        grupo.blocksRaycasts = false;

        float visible = rotateBox ? boxSize * 1.41421f : boxSize;
        float altoBarras = healthHeight + barGap + staminaHeight;
        float centroY = -visible * 0.5f;

        RectTransform borde = Rect("Rombo", raiz, boxBorderColor, out Image bordeImagen);
        if (staminaWarningInBox) aviso = bordeImagen;
        borde.anchorMin = borde.anchorMax = new Vector2(0f, 1f);
        borde.sizeDelta = new Vector2(boxSize, boxSize);
        borde.anchoredPosition = new Vector2(visible * 0.5f, centroY);
        if (rotateBox) borde.localRotation = Quaternion.Euler(0f, 0f, 45f);

        RectTransform interior = Rect("Interior", borde, boxFillColor, out _);
        interior.anchorMin = Vector2.zero;
        interior.anchorMax = Vector2.one;
        interior.offsetMin = new Vector2(boxBorder, boxBorder);
        interior.offsetMax = new Vector2(-boxBorder, -boxBorder);

        // Icono: hermano del rombo y no hijo, para que no gire con el.
        RectTransform ico = Rect("Icono", raiz, Color.white, out icono);
        ico.anchorMin = ico.anchorMax = new Vector2(0f, 1f);
        ico.sizeDelta = new Vector2(iconSize, iconSize);
        ico.anchoredPosition = borde.anchoredPosition;
        icono.preserveAspect = true;
        icono.enabled = false;

        float x = visible;
        float yVida = centroY + altoBarras * 0.5f;
        float yEstamina = yVida - healthHeight - barGap;

        vidaFondo = Barra("Vida", raiz, x, yVida, healthWidth, healthHeight);
        vidaFondoPos = vidaFondo.anchoredPosition;
        vidaEstela = Relleno("Estela", vidaFondo, trailColor, out _);
        vidaRelleno = Relleno("Relleno", vidaFondo, healthColor, out vidaImagen);

        RectTransform estFondo = Barra("Estamina", raiz, x, yEstamina, staminaWidth, staminaHeight);
        estaminaRelleno = Relleno("Relleno", estFondo, staminaColor, out estaminaImagen);

        // Numeros a la derecha de cada barra, apagados salvo que se activen.
        textoVida = Numero("TextoVida", vidaFondo, healthHeight);
        textoEstamina = Numero("TextoEstamina", estFondo, staminaHeight);
        SetShowNumbers(showNumbers);

        Dibujar();
    }

    private Text Numero(string nombre, RectTransform barra, float altoBarra)
    {
        RectTransform r = Crear(nombre, barra, out GameObject go);
        r.anchorMin = r.anchorMax = new Vector2(1f, 0.5f);
        r.pivot = new Vector2(0f, 0.5f);
        r.anchoredPosition = new Vector2(8f, 0f);
        r.sizeDelta = new Vector2(120f, Mathf.Max(altoBarra, numberFontSize + 4f));

        Text t = go.AddComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = numberFontSize;
        t.color = numberColor;
        t.alignment = TextAnchor.MiddleLeft;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    private RectTransform Barra(string nombre, RectTransform padre, float x, float y, float ancho, float alto)
    {
        RectTransform r = Rect(nombre, padre, backColor, out _);
        r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
        r.pivot = new Vector2(0f, 1f);
        r.anchoredPosition = new Vector2(x, y);
        r.sizeDelta = new Vector2(ancho, alto);
        return r;
    }

    private RectTransform Relleno(string nombre, RectTransform padre, Color color, out Image img)
    {
        RectTransform r = Rect(nombre, padre, color, out img);
        r.pivot = new Vector2(0f, 0.5f);
        Anclar(r, 1f);
        return r;
    }

    private static void Anclar(RectTransform r, float fraccion)
    {
        if (r == null) return;
        r.anchorMin = Vector2.zero;
        r.anchorMax = new Vector2(Mathf.Clamp01(fraccion), 1f);
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
    }

    private static RectTransform Rect(string nombre, Transform padre, Color color, out Image img)
    {
        RectTransform r = Crear(nombre, padre, out GameObject go);
        img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return r;
    }

    private static RectTransform Crear(string nombre, Transform padre, out GameObject go)
    {
        go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);
        return (RectTransform)go.transform;
    }
}
