using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// HUD del player al estilo de Elden Ring: un rombo a la izquierda y, pegadas a
// el, la barra de vida (roja), la de estamina (verde) y la de mana (azul).
// Todo son Image de color plano, sin sprites (salvo el anillo del rombo).
//
// Se construye solo por codigo. Si en la escena no hay ninguno, el player crea uno
// al aparecer, asi que no hay que montar nada en cada nivel. Si quieres cambiar
// colores o medidas, pon este componente en un objeto vacio de la escena y ajustalo
// ahi: el player usara ese en vez de crear otro.
//
//  - El largo de cada barra depende de su maximo (como en Elden Ring): al subir
//    de nivel en la hoguera, al salir se ve crecer la barra y llenarse.
//  - Al empezar: la vida algo mas larga que la estamina, y el mana un poco mas
//    corto que la estamina.
//  - Anillo alrededor del rombo con la espada imbuida: se vacia con el tiempo
//    que le queda a la imbuicion (cuando se vacia, se acaba).
//  - Debajo de las barras, una fila por cada estado que se esta acumulando
//    (sangrado, congelacion, quemadura) con su icono; desaparece al llegar a cero.
//  - La estamina se apaga y parpadea cuando queda poca; la vida tiembla con los
//    golpes fuertes; la estela de dano baja rapido al principio y frena al final.
//  - El HUD se desvanece si no pasa nada durante un rato y vuelve al instante.
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
    // Lado del icono en pixeles. Va recto aunque el rombo este girado.
    [SerializeField] private float iconSize = 38f;

    [Header("Rombo: aviso de estamina")]
    [SerializeField] private bool staminaWarningInBox = true;
    [SerializeField] private Color warningLowColor = new Color(0.95f, 0.78f, 0.25f, 1f);
    [SerializeField] private Color warningExhaustedColor = new Color(0.75f, 0.2f, 0.15f, 1f);

    [Header("Rombo: anillo de la imbuicion")]
    [SerializeField] private float ringSize = 96f;
    [SerializeField] private Color ringBackColor = new Color(0f, 0f, 0f, 0.55f);
    [Tooltip("Segundos finales en los que el anillo parpadea.")]
    [SerializeField] private float ringWarning = 10f;

    [Header("Largo de las barras segun su maximo")]
    [Tooltip("Largo de cada barra con los valores iniciales (vida 150, estamina 100, mana 75).")]
    [SerializeField] private float anchoVida = 360f;
    [SerializeField] private float anchoEstamina = 300f;
    [SerializeField] private float anchoMana = 285f;
    [Tooltip("Pixeles que crece cada barra por cada punto de maximo por encima del inicial.")]
    [SerializeField] private float healthPerPoint = 2f;
    [SerializeField] private float staminaPerPoint = 3.5f;
    [SerializeField] private float manaPerPoint = 3.5f;
    [SerializeField] private float maxBarWidth = 820f;
    [Tooltip("Segundos que tarda una barra en crecer tras subir de nivel.")]
    [SerializeField] private float growDuration = 1.1f;
    [SerializeField] private Color growFlashColor = new Color(1f, 0.95f, 0.8f, 0.8f);

    [Header("Barra de vida")]
    [SerializeField] private float healthHeight = 12f;
    [SerializeField] private Color healthColor = new Color(0.62f, 0.07f, 0.07f, 1f);
    [SerializeField] private Color trailColor = new Color(0.92f, 0.86f, 0.78f, 0.9f);
    [SerializeField] private float trailDelay = 0.6f;
    [SerializeField] private float trailSharpness = 3.5f;
    [SerializeField] private Color healFlashColor = new Color(1f, 0.55f, 0.45f, 1f);
    [SerializeField] private float healDuration = 0.35f;

    [Header("Temblor al recibir un golpe fuerte")]
    [Range(0f, 1f)] [SerializeField] private float shakeMinDamage = 0.15f;
    [SerializeField] private float shakeDuration = 0.18f;
    [SerializeField] private float shakeStrength = 6f;

    [Header("Barra de estamina")]
    [SerializeField] private float staminaHeight = 8f;
    [SerializeField] private Color staminaColor = new Color(0.25f, 0.55f, 0.22f, 1f);
    [SerializeField] private Color staminaExhaustedColor = new Color(0.33f, 0.36f, 0.3f, 1f);
    [Range(0f, 1f)] [SerializeField] private float lowStaminaThreshold = 0.2f;
    [SerializeField] private Color staminaLowColor = new Color(0.4f, 0.45f, 0.18f, 1f);
    [SerializeField] private float lowStaminaBlinkSpeed = 9f;

    [Header("Barra de mana")]
    [SerializeField] private float manaHeight = 7f;
    [SerializeField] private Color manaColor = new Color(0.22f, 0.42f, 0.95f, 1f);
    [SerializeField] private Color manaFlashColor = new Color(0.65f, 0.8f, 1f, 1f);

    [Header("Estados que se acumulan (bajo las barras)")]
    [SerializeField] private float statusWidth = 210f;
    [SerializeField] private float statusHeight = 6f;
    [SerializeField] private float statusIcon = 24f;
    [SerializeField] private float statusGap = 6f;

    [Header("Desvanecer sin combate")]
    [SerializeField] private bool autoFade = true;
    [SerializeField] private float fadeDelay = 4f;
    [Range(0f, 1f)] [SerializeField] private float fadedAlpha = 0.3f;
    [SerializeField] private float fadeOutSpeed = 1.5f;
    [SerializeField] private float fadeInSpeed = 8f;
    [Range(0f, 1f)] [SerializeField] private float stayVisibleBelowHealth = 0.35f;

    [Header("Numeros (accesibilidad / debug)")]
    [SerializeField] private bool showNumbers;
    [SerializeField] private int numberFontSize = 16;
    [SerializeField] private Color numberColor = new Color(1f, 1f, 1f, 0.9f);

    [Header("Comun")]
    [SerializeField] private Color backColor = new Color(0.05f, 0.05f, 0.05f, 0.65f);
    [SerializeField] private float barGap = 5f;

    private static PlayerHud instancia;

    // Una barra que crece: su fondo, el ancho mostrado y el destello al crecer.
    private class Largo
    {
        public RectTransform fondo;
        public Image brillo;
        public float mostrado = -1f, desde, hasta, t = 1f;
    }

    private class FilaEstado
    {
        public RectTransform raiz;
        public Image icono, relleno;
        public CanvasGroup grupo;
        public float alfa;
    }

    private CanvasGroup grupo;
    private RectTransform raiz;
    private Vector2 vidaFondoPos;
    private RectTransform vidaRelleno, vidaEstela;
    private Image vidaImagen;
    private RectTransform estaminaRelleno;
    private Image estaminaImagen;
    private RectTransform manaRelleno;
    private Image manaImagen;
    private readonly Largo largoVida = new Largo(), largoEstamina = new Largo(), largoMana = new Largo();
    private float manaAntes = -1f;
    private float destelloMana;
    private Sprite iconoBase;
    private Image aviso, icono;
    private Image anilloFondo, anillo;
    private Text textoVida, textoEstamina, textoMana;
    private readonly List<FilaEstado> filas = new List<FilaEstado>();
    private float yEstados;

    private int vidaActual = 1;
    private int vidaMaxima = 1;
    private float vida = 1f;
    private float vidaMostrada = 1f;
    private float estela = 1f;
    private float esperaEstela;
    private float curando;
    private float curaDesde;
    private float temblor, fuerzaTemblor;
    private float sinActividad;
    private float estaminaAntes = -1f;

    // Lo que se muestra es siempre del player de ahora (tras morir aparece otro).
    private PlayerControler player;
    private PlayerStamina estamina;
    private PlayerMana mana;
    private ArmaImbuida arma;
    private EstadosPlayer estados;
    private float buscar;

    public static PlayerHud Get()
    {
        if (instancia != null) return instancia;
        instancia = FindFirstObjectByType<PlayerHud>();
        if (instancia == null)
            instancia = new GameObject("PlayerHud").AddComponent<PlayerHud>();
        return instancia;
    }

    // El player se presenta al aparecer: asi el HUD siempre mira al actual.
    public void Vincular(PlayerControler p)
    {
        if (p == null) return;
        player = p;
        estamina = p.GetComponent<PlayerStamina>();
        mana = p.GetComponent<PlayerMana>();
        arma = p.GetComponent<ArmaImbuida>();
        estados = p.GetComponent<EstadosPlayer>();
        estaminaAntes = manaAntes = -1f;
    }

    public void SetIcon(Sprite sprite)
    {
        if (icono == null) return;
        iconoBase = sprite;
        icono.sprite = sprite;
        icono.enabled = sprite != null;
    }

    public void SetShowNumbers(bool mostrar)
    {
        showNumbers = mostrar;
        if (textoVida != null) textoVida.enabled = mostrar;
        if (textoEstamina != null) textoEstamina.enabled = mostrar;
        if (textoMana != null) textoMana.enabled = mostrar;
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

    private void Actividad() => sinActividad = 0f;

    // ------------------------------------------------------------------ Bucle

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        BuscarPlayer(dt);

        ActualizarLargos();
        ActualizarVida(dt);
        ActualizarEstamina();
        ActualizarMana(dt);
        ActualizarArma();
        ActualizarEstados(dt);
        ActualizarTemblor(dt);
        ActualizarFundido(dt);
        ActualizarNumeros();
        Dibujar();
    }

    // Por si el player no se presento (o se acaba de destruir al morir).
    private void BuscarPlayer(float dt)
    {
        if (player != null) { if (estados == null) estados = player.GetComponent<EstadosPlayer>(); return; }
        buscar -= dt;
        if (buscar > 0f) return;
        buscar = 0.25f;
        PlayerControler p = FindFirstObjectByType<PlayerControler>();
        if (p != null) Vincular(p);
    }

    // El largo sigue al maximo de cada estadistica. Crece con tiempo de juego (no
    // real): con la hoguera abierta el juego esta parado, y el crecimiento se ve
    // justo al salir de ella.
    private void ActualizarLargos()
    {
        float refVida = AjustesProgreso.Get().vidaBase, refEst = AjustesProgreso.Get().estaminaBase, refMana = AjustesProgreso.Get().manaBase;
        Crecer(largoVida, AnchoPara(anchoVida, healthPerPoint, vidaMaxima, refVida));
        if (estamina != null) Crecer(largoEstamina, AnchoPara(anchoEstamina, staminaPerPoint, estamina.Max, refEst));
        if (mana != null) Crecer(largoMana, AnchoPara(anchoMana, manaPerPoint, mana.Maximo, refMana));
    }

    private float AnchoPara(float baseAncho, float porPunto, float maximo, float referencia) =>
        Mathf.Clamp(baseAncho + (maximo - referencia) * porPunto, 60f, maxBarWidth);

    private void Crecer(Largo l, float objetivo)
    {
        if (l.fondo == null) return;
        if (l.mostrado < 0f) { l.mostrado = l.desde = l.hasta = objetivo; l.t = 1f; }
        if (!Mathf.Approximately(objetivo, l.hasta))
        {
            l.desde = l.mostrado;
            l.hasta = objetivo;
            l.t = 0f;
            Actividad();
        }
        if (l.t < 1f)
        {
            l.t = Mathf.Min(1f, l.t + Time.deltaTime / Mathf.Max(0.05f, growDuration));
            float k = 1f - Mathf.Pow(1f - l.t, 3f);
            l.mostrado = Mathf.Lerp(l.desde, l.hasta, k);
            if (Time.deltaTime > 0f) Actividad();
        }
        l.fondo.sizeDelta = new Vector2(l.mostrado, l.fondo.sizeDelta.y);
        // Destello mientras crece: se ve bien que ha subido.
        if (l.brillo != null)
        {
            float a = l.t < 1f && l.hasta > l.desde ? Mathf.Sin(l.t * Mathf.PI) : 0f;
            l.brillo.color = new Color(growFlashColor.r, growFlashColor.g, growFlashColor.b, growFlashColor.a * a);
            l.brillo.enabled = a > 0.01f;
        }
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

        if (estela > vidaMostrada)
        {
            if (esperaEstela > 0f) esperaEstela -= dt;
            else
            {
                float paso = (estela - vidaMostrada) * (1f - Mathf.Exp(-trailSharpness * dt));
                estela = Mathf.Max(vidaMostrada, estela - Mathf.Max(paso, 0.03f * dt));
            }
        }
        else estela = vidaMostrada;
    }

    private void ActualizarEstamina()
    {
        if (estamina == null) return;
        float f = estamina.Fraction;
        Anclar(estaminaRelleno, f);
        if (estaminaAntes >= 0f && Mathf.Abs(f - estaminaAntes) > 0.0001f) Actividad();
        estaminaAntes = f;

        bool poca = f < lowStaminaThreshold;
        float parpadeo = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * lowStaminaBlinkSpeed);
        if (estamina.Exhausted) estaminaImagen.color = staminaExhaustedColor;
        else if (poca) estaminaImagen.color = Color.Lerp(staminaLowColor, staminaColor, parpadeo * 0.6f);
        else estaminaImagen.color = staminaColor;

        if (aviso != null)
        {
            if (estamina.Exhausted) aviso.color = warningExhaustedColor;
            else if (poca) aviso.color = Color.Lerp(boxBorderColor, warningLowColor, parpadeo);
            else aviso.color = boxBorderColor;
        }
    }

    private void ActualizarMana(float dt)
    {
        if (mana == null) return;
        float f = mana.Fraccion;
        Anclar(manaRelleno, f);
        if (manaAntes >= 0f && Mathf.Abs(f - manaAntes) > 0.0001f) { Actividad(); if (f > manaAntes) destelloMana = 0.35f; }
        manaAntes = f;
        if (destelloMana > 0f) destelloMana -= dt;
        manaImagen.color = destelloMana > 0f ? Color.Lerp(manaColor, manaFlashColor, destelloMana / 0.35f) : manaColor;
    }

    // Con la espada imbuida: el icono del elemento en el rombo, su borde del color
    // del elemento y el anillo que se vacia con el tiempo que le queda.
    private void ActualizarArma()
    {
        if (icono == null) return;
        Elemento e = arma != null ? arma.Activo : Elemento.Ninguno;
        bool imbuida = e != Elemento.Ninguno;
        anillo.enabled = anilloFondo.enabled = imbuida;
        if (!imbuida)
        {
            if (icono.sprite != iconoBase) { icono.sprite = iconoBase; icono.enabled = iconoBase != null; }
            return;
        }
        Sprite s = RecursosRPG.Get().Icono("elemento_" + (int)e);
        if (s != null && icono.sprite != s) { icono.sprite = s; icono.enabled = true; }

        Color c = Elementos.Color(e);
        bool acabando = arma.Restante < ringWarning;
        float p = acabando ? Mathf.Abs(Mathf.Sin(Time.unscaledTime * 8f)) : 1f;
        anillo.fillAmount = arma.FraccionRestante;
        anillo.color = new Color(c.r, c.g, c.b, Mathf.Lerp(0.35f, 1f, p));
        if (aviso != null && estamina != null && !estamina.Exhausted && estamina.Fraction >= lowStaminaThreshold)
            aviso.color = Color.Lerp(boxBorderColor, c, acabando ? p : 0.6f);
    }

    // Una fila por estado que se este acumulando, en el orden en que aparecen.
    private void ActualizarEstados(float dt)
    {
        float y = yEstados;
        for (int i = 0; i < EstadosPlayer.Cantidad; i++)
        {
            EstadoPlayer e = (EstadoPlayer)i;
            FilaEstado f = filas[i];
            float u = estados != null ? estados.Fraccion(e) : 0f;
            bool visible = u > 0.001f;
            f.alfa = Mathf.MoveTowards(f.alfa, visible ? 1f : 0f, dt * (visible ? 8f : 4f));
            f.grupo.alpha = f.alfa;
            bool activa = f.alfa > 0.01f;
            if (f.raiz.gameObject.activeSelf != activa) f.raiz.gameObject.SetActive(activa);
            if (!activa) continue;
            if (visible) Actividad();
            Anclar(f.relleno.rectTransform, u);
            Color c = EstadosPlayer.ColorDe(e);
            // Durante el efecto (ya ha saltado) late; si no, mas vivo cuanto mas lleno.
            bool efecto = estados != null && estados.EnEfecto(e);
            float k = efecto ? 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 6f)) : Mathf.Lerp(0.7f, 1f, u);
            f.relleno.color = new Color(c.r * k, c.g * k, c.b * k, 1f);
            if (f.icono.sprite == null) f.icono.sprite = RecursosRPG.Get().Icono(EstadosPlayer.ClaveIcono(e));
            f.icono.enabled = f.icono.sprite != null;
            // Se colocan una debajo de otra, sin huecos.
            f.raiz.anchoredPosition = new Vector2(f.raiz.anchoredPosition.x, Mathf.Lerp(f.raiz.anchoredPosition.y, y, 1f - Mathf.Exp(-14f * dt)));
            y -= (statusIcon + 4f) * f.alfa;
        }
    }

    private void ActualizarTemblor(float dt)
    {
        RectTransform vf = largoVida.fondo;
        if (vf == null) return;
        if (temblor > 0f)
        {
            temblor -= dt;
            float k = Mathf.Clamp01(temblor / Mathf.Max(0.01f, shakeDuration));
            Vector2 desvio = new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)) * (fuerzaTemblor * k);
            vf.anchoredPosition = vidaFondoPos + desvio;
        }
        else vf.anchoredPosition = vidaFondoPos;
    }

    private void ActualizarFundido(float dt)
    {
        if (grupo == null) return;
        if (!autoFade) { grupo.alpha = 1f; return; }
        sinActividad += dt;
        bool debeVerse = sinActividad < fadeDelay || vida < stayVisibleBelowHealth || estela > vidaMostrada || curando > 0f
                         || (arma != null && arma.Activo != Elemento.Ninguno && arma.Restante < ringWarning);
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
        if (textoMana != null && mana != null)
            textoMana.text = Mathf.CeilToInt(mana.Actual) + " / " + Mathf.RoundToInt(mana.Maximo);
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

        raiz = Crear("HUD", transform, out GameObject raizGo);
        raiz.anchorMin = raiz.anchorMax = new Vector2(0f, 1f);
        raiz.pivot = new Vector2(0f, 1f);
        raiz.anchoredPosition = new Vector2(margin.x, -margin.y);

        grupo = raizGo.AddComponent<CanvasGroup>();
        grupo.interactable = false;
        grupo.blocksRaycasts = false;

        float visible = rotateBox ? boxSize * 1.41421f : boxSize;
        float hueco = Mathf.Max(visible, ringSize) + 6f;
        float altoBarras = healthHeight + barGap + staminaHeight + barGap * 0.6f + manaHeight;
        float centroY = -hueco * 0.5f;
        Vector2 centroRombo = new Vector2(hueco * 0.5f, centroY);

        // Anillo de la imbuicion: detras del rombo, un aro oscuro y encima el que se vacia.
        anilloFondo = Rect("AnilloFondo", raiz, ringBackColor, out _).GetComponent<Image>();
        Anillo(anilloFondo, centroRombo);
        anillo = Rect("Anillo", raiz, Color.white, out _).GetComponent<Image>();
        Anillo(anillo, centroRombo);
        anillo.type = Image.Type.Filled;
        anillo.fillMethod = Image.FillMethod.Radial360;
        anillo.fillOrigin = (int)Image.Origin360.Top;
        anillo.fillClockwise = false;
        anillo.enabled = anilloFondo.enabled = false;

        RectTransform borde = Rect("Rombo", raiz, boxBorderColor, out Image bordeImagen);
        if (staminaWarningInBox) aviso = bordeImagen;
        borde.anchorMin = borde.anchorMax = new Vector2(0f, 1f);
        borde.sizeDelta = new Vector2(boxSize, boxSize);
        borde.anchoredPosition = centroRombo;
        if (rotateBox) borde.localRotation = Quaternion.Euler(0f, 0f, 45f);

        RectTransform interior = Rect("Interior", borde, boxFillColor, out _);
        interior.anchorMin = Vector2.zero;
        interior.anchorMax = Vector2.one;
        interior.offsetMin = new Vector2(boxBorder, boxBorder);
        interior.offsetMax = new Vector2(-boxBorder, -boxBorder);

        RectTransform ico = Rect("Icono", raiz, Color.white, out icono);
        ico.anchorMin = ico.anchorMax = new Vector2(0f, 1f);
        ico.sizeDelta = new Vector2(iconSize, iconSize);
        ico.anchoredPosition = centroRombo;
        icono.preserveAspect = true;
        icono.enabled = false;

        float x = hueco - 4f;
        float yVida = centroY + altoBarras * 0.5f;
        float yEstamina = yVida - healthHeight - barGap;
        float yMana = yEstamina - staminaHeight - barGap * 0.6f;

        largoVida.fondo = Barra("Vida", raiz, x, yVida, anchoVida, healthHeight);
        vidaFondoPos = largoVida.fondo.anchoredPosition;
        vidaEstela = Relleno("Estela", largoVida.fondo, trailColor, out _);
        vidaRelleno = Relleno("Relleno", largoVida.fondo, healthColor, out vidaImagen);
        largoVida.brillo = Brillo(largoVida.fondo);

        largoEstamina.fondo = Barra("Estamina", raiz, x, yEstamina, anchoEstamina, staminaHeight);
        estaminaRelleno = Relleno("Relleno", largoEstamina.fondo, staminaColor, out estaminaImagen);
        largoEstamina.brillo = Brillo(largoEstamina.fondo);

        largoMana.fondo = Barra("Mana", raiz, x, yMana, anchoMana, manaHeight);
        manaRelleno = Relleno("Relleno", largoMana.fondo, manaColor, out manaImagen);
        largoMana.brillo = Brillo(largoMana.fondo);

        // Estados: empiezan bajo el rombo y las barras.
        yEstados = Mathf.Min(-hueco, yMana - manaHeight) - statusGap;
        for (int i = 0; i < EstadosPlayer.Cantidad; i++) filas.Add(NuevaFila((EstadoPlayer)i, x));

        textoVida = Numero("TextoVida", largoVida.fondo, healthHeight);
        textoEstamina = Numero("TextoEstamina", largoEstamina.fondo, staminaHeight);
        textoMana = Numero("TextoMana", largoMana.fondo, manaHeight);
        SetShowNumbers(showNumbers);

        Dibujar();
    }

    private void Anillo(Image img, Vector2 centro)
    {
        img.sprite = RuedaImbuir.Anillo();
        RectTransform r = img.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
        r.sizeDelta = new Vector2(ringSize, ringSize);
        r.anchoredPosition = centro;
    }

    private FilaEstado NuevaFila(EstadoPlayer e, float x)
    {
        FilaEstado f = new FilaEstado();
        f.raiz = Crear("Estado_" + e, raiz, out GameObject go);
        f.raiz.anchorMin = f.raiz.anchorMax = new Vector2(0f, 1f);
        f.raiz.pivot = new Vector2(0f, 1f);
        f.raiz.sizeDelta = new Vector2(statusIcon + 8f + statusWidth, statusIcon);
        f.raiz.anchoredPosition = new Vector2(x - statusIcon - 8f, yEstados);
        f.grupo = go.AddComponent<CanvasGroup>();
        f.grupo.alpha = 0f;

        RectTransform ri = Rect("Icono", f.raiz, Color.white, out f.icono);
        ri.anchorMin = ri.anchorMax = new Vector2(0f, 0.5f);
        ri.pivot = new Vector2(0f, 0.5f);
        ri.sizeDelta = new Vector2(statusIcon, statusIcon);
        f.icono.preserveAspect = true;
        f.icono.sprite = RecursosRPG.Get().Icono(EstadosPlayer.ClaveIcono(e));
        f.icono.enabled = f.icono.sprite != null;

        RectTransform fondo = Rect("Barra", f.raiz, backColor, out _);
        fondo.anchorMin = fondo.anchorMax = new Vector2(0f, 0.5f);
        fondo.pivot = new Vector2(0f, 0.5f);
        fondo.anchoredPosition = new Vector2(statusIcon + 8f, 0f);
        fondo.sizeDelta = new Vector2(statusWidth, statusHeight);
        RectTransform rr = Rect("Relleno", fondo, EstadosPlayer.ColorDe(e), out Image img);
        f.relleno = img;
        rr.pivot = new Vector2(0f, 0.5f);
        Anclar(rr, 0f);
        go.SetActive(false);
        return f;
    }

    private Image Brillo(RectTransform fondo)
    {
        RectTransform r = Rect("Brillo", fondo, new Color(1f, 1f, 1f, 0f), out Image img);
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = new Vector2(-2f, -2f);
        r.offsetMax = new Vector2(2f, 2f);
        img.enabled = false;
        return img;
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
