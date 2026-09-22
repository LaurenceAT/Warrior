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
        hitFlash = GetComponent<HitFlash>();
        currentHealth = maxHealth;
    }

    // Resta vida al enemigo; si sobrevive reproduce el golpe y el retroceso, si no, muere directamente.
    public void TakeDamage(int damage, Vector2 attackerPosition)
    {
        currentHealth -= damage;

        // El destello sale siempre, tanto si sobrevive como si muere: es la
        // confirmacion de que el golpe ha entrado.
        if (hitFlash != null) hitFlash.Flash();

        if (currentHealth <= 0)
        {
            Die();
            return;
        }

        if (animator != null)
            animator.SetTrigger(IdHit);

        ApplyKnockback(attackerPosition);
    }

    // Empuja al enemigo en dirección contraria al atacante y bloquea su movimiento mientras dura.
    private void ApplyKnockback(Vector2 attackerPosition)
    {
        if (rb == null) return;

        float knockDirection = transform.position.x < attackerPosition.x ? -1 : 1;
        rb.linearVelocity = new Vector2(knockbackForce.x * knockDirection, knockbackForce.y);

        StartCoroutine(KnockbackRoutine());
    }

    // Mantiene bloqueada la persecución del EnemyController durante el retroceso.
    private IEnumerator KnockbackRoutine()
    {
        if (enemyController != null) enemyController.SetKnocked(true);

        yield return new WaitForSeconds(knockbackDuration);

        if (enemyController != null) enemyController.SetKnocked(false);
    }

    // Instancia el VFX de muerte, detiene al enemigo y lo destruye después de reproducir su animación de muerte.
    public void Die()
    {
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
