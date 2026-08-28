using UnityEngine;

// Aplica daño al player cuando entra en contacto con este trigger (picos, enemigos, etc.).
public class Damage : MonoBehaviour
{
    [SerializeField] private int damage = 1;

    // Detecta la colisión con el player y le aplica el daño configurado.
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!collision.CompareTag("Player"))
            return;

        PlayerControler player = collision.GetComponent<PlayerControler>();

        if (player != null)
        {
            player.TakeDamage(damage);
        }
    }
}
