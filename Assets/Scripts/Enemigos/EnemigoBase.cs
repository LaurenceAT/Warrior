using System.Collections;
using UnityEngine;

// Base de los enemigos de la cueva. La vida, el retroceso, el combo aereo y la
// barra los lleva EnemyHealth; aqui va lo comun de la IA:
//   - Un "cerebro" (corrutina) que cada enemigo escribe a su manera.
//   - Al recibir un golpe se interrumpe, reproduce "golpe" y espera a que se le
//     pase el aturdimiento antes de volver a pensar (salvo con armadura).
//   - Al morir reproduce "muerte" y se apaga.
//   - Ayudas: ver al player (con linea de vision), mirar hacia el, bordes y
//     paredes, el aviso de ataque (destello de color) y la caja de golpe.
//
// El sprite va en un hijo ("Visual"): se voltea el hijo, no el objeto, para que
// el golpe de escala del HitFlash no pise el volteo.
[RequireComponent(typeof(Rigidbody2D), typeof(EnemyHealth))]
public abstract class EnemigoBase : MonoBehaviour
{
    [Header("Base")]
    [SerializeField] protected AnimadorHoja anim;
    [SerializeField] protected Transform visual;
    [SerializeField] protected float radioDeteccion = 7f;
    // Una vez visto, lo sigue hasta esta distancia aunque pierda la vision.
    [SerializeField] protected float radioOlvido = 11f;
    [SerializeField] protected LayerMask capaSuelo;
    // Los sprites de los packs miran a la derecha.
    [SerializeField] protected bool spriteMiraDerecha = true;

    [Header("Aviso de ataque")]
    [SerializeField] protected Color colorAviso = new Color(1f, 0.55f, 0.15f, 1f);

    protected Rigidbody2D rb;
    protected EnemyHealth salud;
    protected Transform player;
    protected int mirada = 1;
    protected bool alerta;
    protected bool muerto;
    // Mientras sea true, los golpes quitan vida pero no interrumpen (el picado del
    // volador, por ejemplo).
    protected bool armadura;

    private Coroutine cerebro;
    private Coroutine avisoActivo;
    private SpriteRenderer sr;
    private MaterialPropertyBlock bloque;
    private static readonly int IdFlashColor = Shader.PropertyToID("_FlashColor");
    private static readonly int IdFlashAmount = Shader.PropertyToID("_FlashAmount");

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        salud = GetComponent<EnemyHealth>();
        if (anim == null) anim = GetComponentInChildren<AnimadorHoja>();
        if (visual == null && anim != null) visual = anim.transform;
        sr = visual != null ? visual.GetComponent<SpriteRenderer>() : null;
        bloque = new MaterialPropertyBlock();
        if (capaSuelo.value == 0) capaSuelo = LayerMask.GetMask("Ground");

        salud.AlRecibirGolpe += AlRecibirGolpe;
        salud.AlMorir += AlMorir;
    }

    protected virtual void OnDestroy()
    {
        if (salud == null) return;
        salud.AlRecibirGolpe -= AlRecibirGolpe;
        salud.AlMorir -= AlMorir;
    }

    protected virtual void Start()
    {
        Pensar();
    }

    protected abstract IEnumerator Cerebro();

    protected void Pensar()
    {
        if (muerto) return;
        if (cerebro != null) StopCoroutine(cerebro);
        cerebro = StartCoroutine(Cerebro());
    }

    // ------------------------------------------------------------------ Golpes

    private void AlRecibirGolpe()
    {
        if (muerto) return;
        alerta = true;
        if (armadura) return;

        if (cerebro != null) StopCoroutine(cerebro);
        AlInterrumpir();
        cerebro = StartCoroutine(Reaccion());
    }

    // Para que cada enemigo limpie lo suyo al ser interrumpido (un proyectil a
    // medio cargar, la gravedad de un picado...).
    protected virtual void AlInterrumpir()
    {
        QuitarAviso();
    }

    private IEnumerator Reaccion()
    {
        if (anim.Tiene("golpe")) anim.Reproducir("golpe", true);
        float minimo = Mathf.Max(0.25f, anim.Duracion("golpe"));
        float t = 0f;
        while (t < minimo || salud.Aturdido)
        {
            t += Time.deltaTime;
            yield return null;
        }
        cerebro = StartCoroutine(Cerebro());
    }

    protected virtual void AlMorir()
    {
        muerto = true;
        StopAllCoroutines();
        QuitarAviso();
        if (rb != null && rb.simulated) rb.linearVelocity = Vector2.zero;
        if (anim.Tiene("muerte")) anim.Reproducir("muerte", true);
    }

    // ------------------------------------------------------------------ Percepcion

    protected bool BuscarPlayer()
    {
        if (player != null) return true;
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;
        return player != null;
    }

    // Centro del cuerpo del player (su transform esta en el centro de la capsula).
    protected Vector2 PosPlayer => player != null ? (Vector2)player.position : (Vector2)transform.position;

    protected float DistanciaPlayer => player != null ? Vector2.Distance(transform.position, player.position) : Mathf.Infinity;

    protected float DxPlayer => player != null ? player.position.x - transform.position.x : 0f;

    // Lo ve si esta cerca y no hay roca en medio. Una vez en alerta aguanta hasta
    // radioOlvido aunque se esconda un momento.
    protected bool VerPlayer(Vector2 ojos)
    {
        if (!BuscarPlayer()) return false;
        float d = Vector2.Distance(ojos, player.position);
        float radio = alerta ? radioOlvido : radioDeteccion;
        if (d > radio) { alerta = false; return false; }

        bool despejado = !Physics2D.Linecast(ojos, player.position, capaSuelo);
        if (despejado) alerta = true;
        return despejado || alerta;
    }

    protected void Mirar(int dir)
    {
        if (dir == 0) return;
        mirada = dir > 0 ? 1 : -1;
        if (visual == null) return;
        Vector3 e = visual.localScale;
        e.x = Mathf.Abs(e.x) * mirada * (spriteMiraDerecha ? 1 : -1);
        visual.localScale = e;
    }

    protected void MirarAlPlayer()
    {
        if (player != null && Mathf.Abs(DxPlayer) > 0.1f) Mirar(DxPlayer > 0 ? 1 : -1);
    }

    // Suelo delante de los pies (para no caerse por un borde).
    protected bool HaySueloDelante(float adelante, float bajada = 0.8f)
    {
        Vector2 o = (Vector2)transform.position + new Vector2(adelante * mirada, 0.2f);
        return Physics2D.Raycast(o, Vector2.down, bajada, capaSuelo);
    }

    protected bool HayParedDelante(float distancia, float altura = 0.4f)
    {
        Vector2 o = (Vector2)transform.position + new Vector2(0f, altura);
        return Physics2D.Raycast(o, Vector2.right * mirada, distancia, capaSuelo);
    }

    // ------------------------------------------------------------------ Ataque

    // Caja de golpe relativa a los pies, volteada con la mirada. Devuelve el
    // resultado sobre el player, o null si no lo ha tocado.
    protected PlayerControler.ResultadoDano? Golpear(Vector2 offset, Vector2 tamano, int dano)
    {
        Vector2 centro = (Vector2)transform.position + new Vector2(offset.x * mirada, offset.y);
        foreach (Collider2D c in Physics2D.OverlapBoxAll(centro, tamano, 0f))
        {
            if (!c.CompareTag("Player")) continue;
            PlayerControler p = c.GetComponent<PlayerControler>();
            if (p != null) return p.TakeDamage(dano, this);
        }
        return null;
    }

    // Destello de aviso antes de un ataque: late del color de aviso durante
    // "duracion". Es la senal de que va a pegar (no hay sonidos de aviso).
    protected IEnumerator Aviso(float duracion, float intensidad = 0.55f)
    {
        // El color se fija al empezar: quien lo lance puede cambiar colorAviso despues.
        Color color = colorAviso;
        float t = 0f;
        while (t < duracion)
        {
            t += Time.deltaTime;
            float pulso = 0.5f + 0.5f * Mathf.Sin(t * 30f);
            PonerDestello(color, intensidad * (0.4f + 0.6f * pulso));
            yield return null;
        }
        avisoActivo = null;
        PonerDestello(colorAviso, 0f);
    }

    // Lanza el aviso como corrutina aparte (el ataque sigue mientras tanto). Se
    // corta solo si el enemigo es interrumpido.
    protected void LanzarAviso(float duracion, float intensidad = 0.55f)
    {
        if (avisoActivo != null) StopCoroutine(avisoActivo);
        avisoActivo = StartCoroutine(Aviso(duracion, intensidad));
    }

    protected void QuitarAviso()
    {
        if (avisoActivo != null) { StopCoroutine(avisoActivo); avisoActivo = null; }
        PonerDestello(colorAviso, 0f);
    }

    private void PonerDestello(Color c, float cantidad)
    {
        if (sr == null) return;
        sr.GetPropertyBlock(bloque);
        bloque.SetColor(IdFlashColor, c);
        bloque.SetFloat(IdFlashAmount, cantidad);
        sr.SetPropertyBlock(bloque);
    }

    // Espera a que el clip actual termine (o el tiempo tope).
    protected IEnumerator EsperarClip(float tope = 5f)
    {
        float t = 0f;
        while (!anim.Terminado && t < tope)
        {
            t += Time.deltaTime;
            yield return null;
        }
    }

    protected void Frenar(float desaceleracion)
    {
        float vx = Mathf.MoveTowards(rb.linearVelocity.x, 0f, desaceleracion * Time.deltaTime);
        rb.linearVelocity = new Vector2(vx, rb.linearVelocity.y);
    }

    protected void Andar(float velocidad)
    {
        rb.linearVelocity = new Vector2(velocidad * mirada, rb.linearVelocity.y);
    }

    // Rebusca estalactitas cerca y las suelta (rugidos, golpes contra el techo).
    protected static void SoltarEstalactitas(Vector2 centro, float radio)
    {
        foreach (Estalactita e in Object.FindObjectsByType<Estalactita>(FindObjectsSortMode.None))
            if (Vector2.Distance(e.transform.position, centro) <= radio) e.Soltar();
    }

    protected virtual void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 1f, 0f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, radioDeteccion);
    }
}
