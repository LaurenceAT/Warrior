using UnityEngine;

// Hace dano mientras el player (o un enemigo) este dentro del trigger. A
// diferencia de Damage, que solo pega al entrar, esta vuelve a pegar si se queda
// dentro cuando acaba la invulnerabilidad: quedarse encima de unos pinchos o
// dentro del fuego no sale gratis. La usan los pinchos y el fuego.
[RequireComponent(typeof(Collider2D))]
public class ZonaDano : MonoBehaviour
{
    [SerializeField] private int dano = 20;
    // Las trampas tambien hieren a los enemigos: se puede aprovechar empujandolos.
    [SerializeField] private bool danaEnemigos = true;
    [SerializeField] private int danoEnemigos = 10;
    // Cada cuanto puede volver a herir al mismo enemigo (el player ya tiene su
    // propia invulnerabilidad).
    [SerializeField] private float intervaloEnemigos = 0.6f;

    private float siguienteGolpeEnemigos;

    private void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnTriggerStay2D(Collider2D otro)
    {
        if (otro.CompareTag("Player"))
        {
            PlayerControler player = otro.GetComponent<PlayerControler>();
            if (player != null) player.TakeDamage(dano);
            return;
        }

        if (!danaEnemigos || !otro.CompareTag("Enemy") || Time.time < siguienteGolpeEnemigos) return;

        EnemyHealth enemigo = otro.GetComponent<EnemyHealth>();
        if (enemigo == null) return;
        siguienteGolpeEnemigos = Time.time + intervaloEnemigos;
        enemigo.TakeDamage(danoEnemigos, transform.position);
    }
}
