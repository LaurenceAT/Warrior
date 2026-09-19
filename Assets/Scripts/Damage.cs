using UnityEngine;

// Aplica daño a quien entre en este trigger, sea el player o un enemigo (picos, sierras, etc.).
public class Damage : MonoBehaviour
{
    [SerializeField] private int damage = 1;

    // Detecta la colisión con el player o un enemigo y le aplica el daño configurado.
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            PlayerControler player = collision.GetComponent<PlayerControler>();
            if (player != null)
                player.TakeDamage(damage);
        }
        else if (collision.CompareTag("Enemy"))
        {
            // Un enemigo no daña a otro enemigo: ahora que los enemigos también llevan este
            // script, sin esto se matarían entre ellos al amontonarse persiguiendo al player.
            if (CompareTag("Enemy")) return;

            EnemyHealth enemyHealth = collision.GetComponent<EnemyHealth>();
            if (enemyHealth != null)
                enemyHealth.TakeDamage(damage, transform.position);
        }
    }
}
