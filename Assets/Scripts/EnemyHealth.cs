using System.Collections;
using UnityEngine;

// Vida de un enemigo: recibe dano y retroceso, puede quedar en el aire para un combo
// aereo, y muere al llegar a cero. Todo el dano pasa por aqui (espada, arco, punos,
// trampas), asi que la barra de vida de encima solo tiene que escuchar a este script.
public class EnemyHealth : MonoBehaviour
{
    [Header("Vida")]
    [SerializeField] private int maxHealth = 50;
    [SerializeField] private int currentHealth;
    [SerializeField] private GameObject deathVFX;
    [SerializeField] private float deathAnimDuration = 0.8f;

    [Header("Peso")]
    // Masa del cuerpo. El player pesa 1: con la misma masa, al chocar lo empujaba como
    // a una caja. Pesando mucho mas, el player se para contra el. Los golpes no se
    // ven afectados: el retroceso fija la velocidad directamente.
    [SerializeField] private float bodyMass = 50f;

    [Header("Knockback")]
    // Empuje de los golpes que no traen el suyo propio (arco, trampas...). Los golpes
    // de espada y sin arma llevan su knockback en sus propios datos.
    [SerializeField] private Vector2 knockbackForce = new Vector2(4f, 2f);
    [SerializeField] private float knockbackDuration = 0.2f;

    [Header("Combo aereo (tras un golpe que lanza)")]
    // Golpes que aguanta en el aire antes de caer. Cada uno le da un rebote y
    // reinicia la caida; el ultimo lo suelta.
    [SerializeField] private int maxAirHits = 8;
    // Curva que usa si el golpe que lo lanza no trae la suya (normalmente la trae).
    [SerializeField] private Elevacion defaultLaunch = new Elevacion();
    // Golpes a un enemigo que esta en el aire pero fuera del combo (cayendo tras
    // el): su empuje se reduce a esta fraccion, para que apenas se aparte.
    [Range(0f, 1f)] [SerializeField] private float airKnockbackScale = 0.15f;
    [SerializeField] private bool isAirborne;

    [Header("Barra de vida")]
    [SerializeField] private bool showHealthBar = true;
    // Posicion de la barra respecto al centro del enemigo, y su ancho en unidades.
    [SerializeField] private Vector2 healthBarOffset = new Vector2(0f, 0.45f);
    [SerializeField] private float healthBarWidth = 0.7f;

    private static readonly int IdHit = Animator.StringToHash("hit");
    private static readonly int IdDead = Animator.StringToHash("dead");
    // Mantiene la animacion de hit mientras esta aturdido o en el aire.
    private static readonly int IdIsHurt = Animator.StringToHash("isHurt");
    private bool tieneIsHurt;
    private Collider2D cuerpo;
    private Coroutine rutinaAturdido;

    [Header("Aterrizaje tras un golpe")]
    // Tope por si cae a un sitio sin suelo que no sea zona de muerte.
    [SerializeField] private float maxAirStunTime = 2.5f;
    private Animator animator;
    private Rigidbody2D rb;
    private EnemyController enemyController;
    private HitFlash hitFlash;
    private EnemyHealthBar barra;

    private int golpesAereos;
    private float gravedadOriginal = -1f;

    // Arco del combo aereo: sube (ease-out) y cae (ease-in), sin quedarse parado.
    // Cada golpe en el aire empieza un tramo nuevo con un pequeno rebote.
    private enum FaseAerea { Subida, Caida }
    private FaseAerea fase;
    private float tFase;
    private float tTramo;
    private Elevacion curva;
    // Altura de la que salio: la caida se mide hasta aqui.
    private float ySalida;
    private float xTramo;
    private float yTramo;
    private float yCima;
    private float duracionSubida;
    private float desplazamientoTramo;

    // El enemigo esta en pleno combo aereo: IA pausada y flotando.
    public bool IsAirborne => isAirborne;
    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
        if (rb != null) { rb.useAutoMass = false; rb.mass = bodyMass; }
        enemyController = GetComponent<EnemyController>();

        // El collider solido (no un trigger) es el que apoya en el suelo.
        foreach (Collider2D c in GetComponents<Collider2D>())
            if (!c.isTrigger) { cuerpo = c; break; }

        // Solo se usa isHurt si el Animator lo tiene; si no, SetBool llenaria la
        // consola de avisos hasta que se anada el parametro.
        if (animator != null)
            foreach (AnimatorControllerParameter p in animator.parameters)
                if (p.nameHash == IdIsHurt) { tieneIsHurt = true; break; }
        hitFlash = GetComponent<HitFlash>();
        currentHealth = maxHealth;

        if (showHealthBar)
            barra = EnemyHealthBar.Crear(transform, healthBarOffset, healthBarWidth);
    }

    // ------------------------------------------------------------------ Recibir golpes

    // Golpe con su propio empuje: X hacia fuera del atacante, Y hacia arriba (negativa
    // lo estampa contra el suelo). Si "lanza", lo deja en el aire para un combo aereo.
    // Lo usan la espada y los golpes sin arma, cada uno con sus datos. Si lanza, la
    // elevacion dice cuanto sube y cuanto dura (null = la de este enemigo).
    public void TakeHit(int damage, Vector2 attackerPosition, Vector2 knockback, bool launches, Elevacion elevacion = null)
    {
        if (!RecibirDano(damage)) return;

        float lado = transform.position.x < attackerPosition.x ? -1f : 1f;

        if (isAirborne) { GolpeEnElAire(lado, knockback); return; }
        if (launches) { Lanzar(lado, elevacion); return; }

        // En el aire (cayendo), el retroceso es minimo. Salvo si lo estampa hacia abajo.
        if (knockback.y >= 0f && !EnElSuelo()) knockback *= airKnockbackScale;

        if (rb != null) rb.linearVelocity = new Vector2(knockback.x * lado, knockback.y);
        Aturdir(knockbackDuration);
    }

    // Golpe con una velocidad de lanzamiento concreta. La usa la estocada del player
    // para mandar al enemigo por los aires.
    public void TakeDamage(int damage, Vector2 attackerPosition, Vector2 launchVelocity)
    {
        if (!RecibirDano(damage)) return;

        if (isAirborne) TerminarComboAereo();
        if (rb != null) rb.linearVelocity = launchVelocity;
        Aturdir(knockbackDuration);
    }

    // Golpe con el empuje por defecto del enemigo, escalado. Lo usan el arco, las
    // trampas y la estocada al tocar suelo.
    public void TakeDamage(int damage, Vector2 attackerPosition, float knockbackMultiplier = 1f)
    {
        if (!RecibirDano(damage)) return;

        float lado = transform.position.x < attackerPosition.x ? -1f : 1f;
        float k = Mathf.Max(0f, knockbackMultiplier);

        // En el aire cuenta como un golpe mas del combo aereo.
        if (isAirborne) { GolpeEnElAire(lado, knockbackForce * k); return; }

        if (rb != null) rb.linearVelocity = new Vector2(knockbackForce.x * lado * k, knockbackForce.y * k);

        // Un golpe mas fuerte tambien aturde algo mas, si no el enemigo volveria
        // a la carga antes de terminar de salir despedido.
        Aturdir(knockbackDuration * (1f + (k - 1f) * 0.3f));
    }

    // Parte comun: resta vida, avisa a la barra, destella y dispara el hit. Devuelve
    // false si muere.
    private bool RecibirDano(int damage)
    {
        currentHealth = Mathf.Max(0, currentHealth - Mathf.Max(0, damage));
        if (barra != null) barra.Mostrar(currentHealth, maxHealth);

        // El destello sale siempre, tanto si sobrevive como si muere: es la
        // confirmacion de que el golpe ha entrado.
        if (hitFlash != null) hitFlash.Flash();

        if (currentHealth <= 0)
        {
            Die();
            return false;
        }

        if (animator != null)
            animator.SetTrigger(IdHit);

        return true;
    }

    // ------------------------------------------------------------------ Combo aereo

    // Lo eleva siguiendo la curva, sin gravedad: asi el tiempo en el aire es exacto,
    // se ajusta a mano y no depende de la fisica.
    private void Lanzar(float lado, Elevacion elevacion)
    {
        if (rb == null) return;

        curva = elevacion ?? defaultLaunch;
        isAirborne = true;
        golpesAereos = 0;

        if (gravedadOriginal < 0f) gravedadOriginal = rb.gravityScale;
        rb.gravityScale = 0f;
        ySalida = rb.position.y;
        // Hacia fuera del atacante. A 0, recto hacia arriba.
        EmpezarTramo(curva.altura, curva.subida, curva.desplazamientoX * lado);

        Aturdir(knockbackDuration);
    }

    // Arranca una subida desde donde esta ahora: la del lanzamiento, o el rebote
    // de un golpe en el aire.
    private void EmpezarTramo(float altura, float subida, float desplazamiento)
    {
        fase = FaseAerea.Subida;
        tFase = 0f;
        tTramo = 0f;
        xTramo = rb.position.x;
        yTramo = rb.position.y;
        yCima = yTramo + altura;
        duracionSubida = subida;
        desplazamientoTramo = desplazamiento;
    }

    // Un golpe mas en el aire: le da un pequeno rebote y vuelve a empezar la caida.
    // Asi, mientras se le siga pegando, no baja; al dejar de pegarle cae solo.
    // El ultimo golpe permitido, o uno que lo estampe (knockback con Y negativa),
    // lo suelta.
    private void GolpeEnElAire(float lado, Vector2 knockback)
    {
        golpesAereos++;

        bool estampa = knockback.y < 0f;
        bool ultimo = golpesAereos >= maxAirHits;

        if (estampa || ultimo)
        {
            TerminarComboAereo();
            if (rb != null)
                rb.linearVelocity = estampa
                    ? new Vector2(knockback.x * lado, knockback.y)
                    : new Vector2(knockback.x * lado * 0.5f, 1f);
        }
        else if (rb != null)
        {
            EmpezarTramo(curva.alturaPorGolpe, curva.subidaPorGolpe, 0f);
        }

        Aturdir(knockbackDuration);
    }

    // Aturdimiento sin dano, para el parry del jugador: se para en seco y pierde
    // el turno el tiempo indicado.
    public void Stagger(float segundos)
    {
        if (currentHealth <= 0) return;
        if (rb != null && !isAirborne) rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        if (animator != null) animator.SetTrigger(IdHit);
        Aturdir(segundos);
    }

    // Fin del combo aereo: vuelve la gravedad normal y cae. La IA vuelve al aterrizar
    // (eso lo lleva el aturdimiento).
    private void TerminarComboAereo()
    {
        isAirborne = false;
        golpesAereos = 0;
        if (rb != null && gravedadOriginal >= 0f)
        {
            rb.gravityScale = gravedadOriginal;
            gravedadOriginal = -1f;
        }
    }

    // Mueve al enemigo por el arco. Va por velocidad, no teletransportando: asi
    // sigue chocando con techos y paredes.
    private void FixedUpdate()
    {
        if (!isAirborne || rb == null) return;

        float dt = Time.fixedDeltaTime;
        tFase += dt;
        tTramo += dt;
        Vector2 p = rb.position;
        float caida = curva.suspension + curva.bajada;

        // En horizontal se aparta durante todo el arco, frenando poco a poco: por
        // eso dibuja una curva y no sube y baja en linea recta.
        float tH = duracionSubida + caida * 0.6f;
        float h = tH > 0f ? Mathf.Clamp01(tTramo / tH) : 1f;
        float x = xTramo + desplazamientoTramo * (1f - (1f - h) * (1f - h));
        float y;

        if (fase == FaseAerea.Subida)
        {
            // Ease-out: sale disparado y frena al llegar a la cima.
            float u = duracionSubida > 0f ? Mathf.Clamp01(tFase / duracionSubida) : 1f;
            y = Mathf.Lerp(yTramo, yCima, 1f - (1f - u) * (1f - u));
            if (u >= 1f) { fase = FaseAerea.Caida; tFase = 0f; }
        }
        else
        {
            // Ease-in al cubo: en la cima apenas baja (flota sin quedarse parado) y
            // luego acelera hacia el suelo del que salio.
            float u = caida > 0f ? Mathf.Clamp01(tFase / caida) : 1f;
            float alto = Mathf.Max(0f, yCima - ySalida);
            y = yCima - alto * u * u * u;
            if (u >= 1f)
            {
                // Suelta con la velocidad a la que iba, y la gravedad hace el resto
                // (por si ya no hay suelo donde salio).
                float v = caida > 0f ? 3f * alto / caida : 0f;
                TerminarComboAereo();
                rb.linearVelocity = new Vector2(0f, -v);
                return;
            }
        }

        rb.linearVelocity = (new Vector2(x, y) - p) / dt;
    }

    // ------------------------------------------------------------------ Aturdimiento

    // Mantiene bloqueada la persecución del EnemyController durante el retroceso.
    private void Aturdir(float minimo)
    {
        if (rutinaAturdido != null) StopCoroutine(rutinaAturdido);
        rutinaAturdido = StartCoroutine(KnockbackRoutine(minimo));
    }

    // Aturdido el tiempo minimo y, si ha salido despedido, hasta que vuelva a
    // pisar suelo. Mientras tanto se mantiene la animacion de hit. En pleno combo
    // aereo sigue aturdido: la IA no se mueve, ni ataca, ni persigue.
    private IEnumerator KnockbackRoutine(float duracion)
    {
        if (enemyController != null) enemyController.SetKnocked(true);

        if (tieneIsHurt) animator.SetBool(IdIsHurt, true);

        yield return new WaitForSeconds(duracion);

        float enElAire = 0f;
        while (isAirborne || (!EnElSuelo() && enElAire < maxAirStunTime))
        {
            yield return new WaitForFixedUpdate();
            // El tope solo cuenta al caer: durante el combo aereo no.
            if (!isAirborne) enElAire += Time.fixedDeltaTime;
        }

        if (tieneIsHurt) animator.SetBool(IdIsHurt, false);
        rutinaAturdido = null;

        if (enemyController != null) enemyController.SetKnocked(false);
    }

    // Mira si el cuerpo esta apoyado en la capa Ground.
    private bool EnElSuelo()
    {
        if (cuerpo == null) return true;
        // Subiendo todavia no puede estar aterrizando.
        if (rb != null && rb.linearVelocity.y > 0.1f) return false;

        Bounds b = cuerpo.bounds;
        return Physics2D.Raycast(b.center, Vector2.down, b.extents.y + 0.08f, LayerMask.GetMask("Ground"));
    }

    // ------------------------------------------------------------------ Muerte

    // Instancia el VFX de muerte, detiene al enemigo y lo destruye después de reproducir su animación de muerte.
    public void Die()
    {
        TerminarComboAereo();
        if (rutinaAturdido != null) StopCoroutine(rutinaAturdido);
        if (tieneIsHurt && animator != null) animator.SetBool(IdIsHurt, false);
        if (barra != null) barra.Ocultar();
        if (deathVFX != null)
            Instantiate(deathVFX, transform.position, Quaternion.identity);

        if (enemyController != null) enemyController.enabled = false;
        if (rb != null) rb.simulated = false;

        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        if (animator != null)
            animator.SetTrigger(IdDead);

        Destroy(gameObject, deathAnimDuration);
    }
}

// Como sube un enemigo lanzado y cuanto se queda arriba. Va por tiempo, no por
// gravedad: la suspension dura exactamente subida + suspension + bajada.
// La usan los golpes que lanzan (gancho, corte lanzador de la espada...).
[System.Serializable]
public class Elevacion
{
    // Cuanto sube, en unidades (1 = una casilla).
    public float altura = 1.5f;
    // Cuanto se aparta del atacante mientras sube. 0 = recto hacia arriba.
    public float desplazamientoX = 0f;
    // Segundos subiendo (ease-out, frena al llegar a la cima).
    public float subida = 0.3f;
    // La caida dura suspension + bajada. Va en ease-in al cubo: durante la
    // "suspension" apenas baja (flota en la cima sin pararse) y en la "bajada"
    // acelera hacia el suelo.
    public float suspension = 0.6f;
    public float bajada = 0.3f;
    // Rebote de cada golpe en el aire: cuanto lo sube y en cuanto tiempo. Tras el
    // rebote vuelve a empezar la caida, asi que mientras se le pegue no baja.
    public float alturaPorGolpe = 0.1f;
    public float subidaPorGolpe = 0.1f;
}
