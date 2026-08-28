using UnityEngine;

// Enemigo simple que persigue al player cuando está dentro de un radio de detección.
public class EnemyController : MonoBehaviour
{
    private Transform player;

    public float detectionRadius = 5f;
    public float speed = 2f;

    private Rigidbody2D rb;
    private Vector2 movement;

    private int direction = 1;

    #region Unity Lifecycle

    // Obtiene el Rigidbody2D y busca al player al iniciar.
    void Start()
    {
        rb = GetComponent<Rigidbody2D>();

        BuscarJugador();
    }

    // Persigue al player si está dentro del radio de detección; si no, se detiene.
    void FixedUpdate()
    {
        // Si el jugador murió, buscar el nuevo
        if (player == null)
        {
            BuscarJugador();
            return;
        }

        float distance = Vector2.Distance(transform.position, player.position);

        if (distance < detectionRadius)
        {
            Vector2 direction = (player.position - transform.position).normalized;

            movement = new Vector2(direction.x, 0);

            Flip(direction.x);
        }
        else
        {
            movement = Vector2.zero;
        }

        rb.linearVelocity = new Vector2(movement.x * speed, rb.linearVelocity.y);
    }

    // Dibuja el radio de detección en el editor cuando el enemigo está seleccionado.
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }

    #endregion

    // Busca en la escena al GameObject con tag "Player" y guarda su Transform.
    void BuscarJugador()
    {
        GameObject obj = GameObject.FindGameObjectWithTag("Player");

        if (obj != null)
            player = obj.transform;
    }

    // Gira el sprite del enemigo según la dirección horizontal hacia el player.
    private void Flip(float directionX)
    {
        if (directionX > 0 && direction == -1)
        {
            transform.localScale = new Vector3(1, 1, 1);
            direction = 1;
        }
        else if (directionX < 0 && direction == 1)
        {
            transform.localScale = new Vector3(-1, 1, 1);
            direction = -1;
        }
    }
}
