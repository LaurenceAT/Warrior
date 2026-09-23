using System.Collections;
using UnityEngine;

// Vida de un enemigo: recibe daño y retroceso del ataque del player, y muere al llegar a cero.
public class EnemyHealth : MonoBehaviour
{
    [Header("Vida")]
    [SerializeField] private int maxHealth = 3;
    [SerializeField] private int currentHealth;
    [SerializeField] private GameObject deathVFX;
    [SerializeField] private float deathAnimDuration = 0.8f;

    [Header("Knockback")]
    [SerializeField] private Vector2 knockbackForce = new Vector2(4f, 2f);
    [SerializeField] private float knockbackDuration = 0.2f;

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

    // Obtiene los componentes necesarios e inicializa la vida al máximo.
    private void Awake()
    {
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
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
    }

    // Resta vida al enemigo; si sobrevive reproduce el golpe y el retroceso, si no, muere directamente.
    // Golpe con una velocidad de lanzamiento concreta. La usa la estocada del player
    // para mandar al enemigo por los aires.
    public void TakeDamage(int damage, Vector2 attackerPosition, Vector2 launchVelocity)
    {
        if (!RecibirDano(damage)) return;

        if (rb != null) rb.linearVelocity = launchVelocity;
        Aturdir(knockbackDuration);
    }

    // knockbackMultiplier escala el retroceso: 1 es un golpe normal, la estocada
    // del player usa mas para que el impacto se note en el enemigo.
    public void TakeDamage(int damage, Vector2 attackerPosition, float knockbackMultiplier = 1f)
    {
        if (!RecibirDano(damage)) return;
        ApplyKnockback(attackerPosition, knockbackMultiplier);
    }

    // Parte comun: resta vida, destella y dispara el hit. Devuelve false si muere.
    private bool RecibirDano(int damage)
    {
        currentHealth -= damage;

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

    // Empuja al enemigo en dirección contraria al atacante y bloquea su movimiento mientras dura.
    private void ApplyKnockback(Vector2 attackerPosition, float multiplicador)
    {
        if (rb == null) return;

        float knockDirection = transform.position.x < attackerPosition.x ? -1 : 1;
        float k = Mathf.Max(0f, multiplicador);
        rb.linearVelocity = new Vector2(knockbackForce.x * knockDirection * k, knockbackForce.y * k);

        // Un golpe mas fuerte tambien aturde algo mas, si no el enemigo volveria
        // a la carga antes de terminar de salir despedido.
        float aturdimiento = knockbackDuration * (1f + (k - 1f) * 0.3f);
        Aturdir(aturdimiento);
    }

    // Mantiene bloqueada la persecución del EnemyController durante el retroceso.
    private void Aturdir(float minimo)
    {
        if (rutinaAturdido != null) StopCoroutine(rutinaAturdido);
        rutinaAturdido = StartCoroutine(KnockbackRoutine(minimo));
    }

    // Aturdido el tiempo minimo y, si ha salido despedido, hasta que vuelva a
    // pisar suelo. Mientras tanto se mantiene la animacion de hit: antes, al
    // acabar el tiempo en pleno vuelo, el controller volvia a mandar y ponia la
    // de correr o la de ataque en el aire.
    private IEnumerator KnockbackRoutine(float duracion)
    {
        if (enemyController != null) enemyController.SetKnocked(true);

        if (tieneIsHurt) animator.SetBool(IdIsHurt, true);

        yield return new WaitForSeconds(duracion);

        float enElAire = 0f;
        while (!EnElSuelo() && enElAire < maxAirStunTime)
        {
            yield return new WaitForFixedUpdate();
            enElAire += Time.fixedDeltaTime;
        }

        if (tieneIsHurt) animator.SetBool(IdIsHurt, false);
        rutinaAturdido = null;

        if (enemyController != null) enemyController.SetKnocked(false);
    }

    // Instancia el VFX de muerte, detiene al enemigo y lo destruye después de reproducir su animación de muerte.
    // Mira si el cuerpo esta apoyado en la capa Ground.
    private bool EnElSuelo()
    {
        if (cuerpo == null) return true;
        // Subiendo todavia no puede estar aterrizando.
        if (rb != null && rb.linearVelocity.y > 0.1f) return false;

        Bounds b = cuerpo.bounds;
        return Physics2D.Raycast(b.center, Vector2.down, b.extents.y + 0.08f, LayerMask.GetMask("Ground"));
    }

    public void Die()
    {
        if (rutinaAturdido != null) StopCoroutine(rutinaAturdido);
        if (tieneIsHurt && animator != null) animator.SetBool(IdIsHurt, false);
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
