using System.Collections;
using UnityEngine;

// Enemigo que patrulla de un lado a otro, persigue al player cuando entra en su radio
// de detección, y lo ataca (con cooldown) cuando lo tiene dentro del radio de ataque.
public class EnemyController : MonoBehaviour
{
    private Transform player;

    [Header("Persecución")]
    public float detectionRadius = 5f;
    public float speed = 2f;

    // Marcar si el sprite original está dibujado mirando a la izquierda (los cerdos lo están; el King no).
    [SerializeField] private bool spriteFacesLeft = true;

    [Header("Ataque")]
    [SerializeField] private float attackRange = 1f;
    [SerializeField] private float attackCooldown = 1.5f;
    // Cuánto dura el bloqueo de movimiento/giro del golpe (debe parecerse al largo de la animación).
    [SerializeField] private float attackDuration = 0.5f;
    [SerializeField] private int attackDamage = 1;
    // Centro del área del golpe, relativo al enemigo (la X se invierte según hacia dónde mira).
    [SerializeField] private Vector2 attackOffset = new Vector2(0.5f, -0.15f);
    [SerializeField] private float attackRadius = 0.4f;
    [SerializeField] private bool isAttacking;
    private bool canAttack = true;

    [Header("Patrulla")]
    // Velocidad al patrullar (normalmente más lenta que al perseguir).
    [SerializeField] private float patrolSpeed = 1f;
    // Capa del suelo, usada para detectar el borde de la plataforma.
    // Si se deja vacía, se usa automáticamente la capa "Ground".
    [SerializeField] private LayerMask groundLayer;
    // Capas que obligan a darse la vuelta: paredes y otros enemigos.
    // Si se deja vacía, se usan automáticamente "Ground" y "Enemies".
    [SerializeField] private LayerMask wallLayer;
    // El rayo debe salir a la altura del collider y llegar más lejos que su borde frontal,
    // si no el cuerpo choca antes de que el rayo detecte nada.
    [SerializeField] private float wallCheckHeight = -0.33f;
    [SerializeField] private float wallCheckDistance = 0.55f;
    // Punto adelantado desde el que se mira hacia abajo para detectar el borde de la plataforma.
    [SerializeField] private Vector2 edgeCheckOffset = new Vector2(0.45f, -0.3f);
    [SerializeField] private float edgeCheckDistance = 0.4f;

    [Header("Trampas")]
    // Si está marcado, el enemigo se da la vuelta al ver una trampa delante en vez de
    // meterse en ella. Sigue recibiendo daño si acaba tocándola.
    [SerializeField] private bool avoidTraps = true;
    // Capa de las trampas a esquivar. Si se deja vacía se usa automáticamente "Traps".
    [SerializeField] private LayerMask trapLayer;
    // Zona por delante de los pies donde se comprueba si hay trampa.
    [SerializeField] private Vector2 trapCheckOffset = new Vector2(0.55f, -0.25f);
    [SerializeField] private float trapCheckRadius = 0.35f;

    private static readonly int IdAttack = Animator.StringToHash("attack");
    private static readonly int IdSpeed = Animator.StringToHash("Speed");

    private Rigidbody2D rb;
    private Animator animator;
    private Vector2 movement;

    private int direction = 1;

    // Bloquea la persecución mientras el enemigo está en retroceso por un golpe.
    private bool isKnocked;

    #region Unity Lifecycle

    // Obtiene los componentes y busca al player al iniciar.
    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();

        // Si no se asignaron las capas en el Inspector, se usan las de por defecto.
        if (groundLayer.value == 0)
            groundLayer = LayerMask.GetMask("Ground");

        if (wallLayer.value == 0)
            wallLayer = LayerMask.GetMask("Ground", "Enemies");

        if (trapLayer.value == 0)
            trapLayer = LayerMask.GetMask("Traps");

        BuscarJugador();
    }

    // Patrulla mientras no vea al player; si lo detecta lo persigue, y lo ataca si lo tiene cerca.
    void FixedUpdate()
    {
        if (isKnocked) return;

        // Si el jugador murió, buscar el nuevo (mientras tanto se sigue patrullando).
        if (player == null) BuscarJugador();

        float distance = Mathf.Infinity;
        Vector2 dirToPlayer = Vector2.zero;

        if (player != null)
        {
            distance = Vector2.Distance(transform.position, player.position);
            dirToPlayer = (player.position - transform.position).normalized;
        }

        bool playerDetected = distance < detectionRadius;

        if (playerDetected && !isAttacking)
        {
            Flip(dirToPlayer.x);
        }

        HandleAttack(distance);

        if (isAttacking)
        {
            // Quieto mientras golpea.
            movement = Vector2.zero;
        }
        else if (playerDetected)
        {
            // Persigue hasta quedar a distancia de golpe, pero se planta si tiene una trampa
            // delante: sin esto se metería en los pinchos por seguir al player.
            bool puedeAvanzar = distance > attackRange && !IsTrapAhead();
            movement = puedeAvanzar ? new Vector2(dirToPlayer.x, 0) : Vector2.zero;
        }
        else
        {
            movement = Patrol();
        }

        float currentSpeed = playerDetected ? speed : patrolSpeed;
        rb.linearVelocity = new Vector2(movement.x * currentSpeed, rb.linearVelocity.y);

        if (animator != null)
            animator.SetFloat(IdSpeed, Mathf.Abs(rb.linearVelocity.x));
    }

    // Dibuja los radios de detección, de ataque y el área del golpe cuando el enemigo está seleccionado.
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(GetAttackOrigin(), attackRadius);

        // Rayos de patrulla: cyan detecta paredes, verde detecta el borde de la plataforma.
        Gizmos.color = Color.cyan;
        Vector2 wallOrigin = GetWallCheckOrigin();
        Gizmos.DrawLine(wallOrigin, wallOrigin + new Vector2(wallCheckDistance * direction, 0f));

        Gizmos.color = Color.green;
        Vector2 edgeOrigin = GetEdgeCheckOrigin();
        Gizmos.DrawLine(edgeOrigin, edgeOrigin + new Vector2(0f, -edgeCheckDistance));

        // Zona de detección de trampas.
        if (!avoidTraps) return;
        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireSphere(GetTrapCheckOrigin(), trapCheckRadius);
    }

    #endregion

    #region Ataque

    // Dispara la animación de ataque cuando el player está en rango y no hay cooldown activo.
    private void HandleAttack(float distance)
    {
        if (isAttacking || !canAttack) return;
        if (distance > attackRange) return;

        if (animator != null)
            animator.SetTrigger(IdAttack);

        StartCoroutine(AttackRoutine());
    }

    // Bloquea movimiento y giro mientras dura el golpe, y espera el cooldown antes de permitir otro.
    private IEnumerator AttackRoutine()
    {
        canAttack = false;
        isAttacking = true;

        // Durante el golpe el enemigo no se mueve ni gira.
        yield return new WaitForSeconds(attackDuration);
        isAttacking = false;

        // Después ya puede volver a perseguir y girar, pero aún no puede atacar de nuevo.
        yield return new WaitForSeconds(Mathf.Max(0f, attackCooldown - attackDuration));
        canAttack = true;
    }

    // Aplica daño al player si está dentro del área del golpe.
    // Se debe llamar mediante un Animation Event en el frame de "EnemyAttack" donde el golpe conecta.
    public void DealAttackDamage()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(GetAttackOrigin(), attackRadius);

        foreach (Collider2D hit in hits)
        {
            if (!hit.CompareTag("Player")) continue;

            PlayerControler player = hit.GetComponent<PlayerControler>();
            if (player != null)
            {
                player.TakeDamage(attackDamage);
                return;
            }
        }
    }

    // Centro del área del golpe, desplazado hacia el lado al que mira el enemigo.
    private Vector2 GetAttackOrigin()
    {
        return (Vector2)transform.position + new Vector2(attackOffset.x * direction, attackOffset.y);
    }

    #endregion

    #region Patrulla

    // Camina siempre hacia adelante, dándose la vuelta al toparse con una pared
    // o al llegar al borde de la plataforma (para no caerse).
    private Vector2 Patrol()
    {
        if (IsWallAhead() || !IsGroundAhead() || IsTrapAhead())
            SetDirection(-direction);

        return new Vector2(direction, 0);
    }

    // Comprueba si hay una trampa justo delante de los pies.
    // Las trampas son triggers, así que esto depende de que "Queries Hit Triggers"
    // siga activo en Project Settings > Physics 2D.
    private bool IsTrapAhead()
    {
        if (!avoidTraps || trapLayer.value == 0) return false;

        return Physics2D.OverlapCircle(GetTrapCheckOrigin(), trapCheckRadius, trapLayer);
    }

    // Centro de la zona de comprobación de trampas, desplazada hacia donde camina el enemigo.
    private Vector2 GetTrapCheckOrigin()
    {
        return (Vector2)transform.position + new Vector2(trapCheckOffset.x * direction, trapCheckOffset.y);
    }

    // Lanza un rayo horizontal al frente para detectar paredes u otros enemigos.
    private bool IsWallAhead()
    {
        RaycastHit2D[] hits = Physics2D.RaycastAll(GetWallCheckOrigin(), Vector2.right * direction, wallCheckDistance, wallLayer);

        foreach (RaycastHit2D hit in hits)
        {
            // El rayo nace dentro del propio cuerpo del enemigo, así que hay que ignorarse a sí mismo.
            if (hit.rigidbody == rb) continue;

            return true;
        }

        return false;
    }

    // Lanza un rayo hacia abajo desde un punto adelantado: si no encuentra suelo, hay un borde.
    private bool IsGroundAhead()
    {
        return Physics2D.Raycast(GetEdgeCheckOrigin(), Vector2.down, edgeCheckDistance, groundLayer);
    }

    // Origen del rayo que detecta paredes, a la altura configurada.
    private Vector2 GetWallCheckOrigin()
    {
        return (Vector2)transform.position + new Vector2(0f, wallCheckHeight);
    }

    // Origen del rayo que detecta el borde, desplazado hacia donde camina el enemigo.
    private Vector2 GetEdgeCheckOrigin()
    {
        return (Vector2)transform.position + new Vector2(edgeCheckOffset.x * direction, edgeCheckOffset.y);
    }

    #endregion

    // Activa o desactiva el bloqueo de movimiento por knockback (llamado desde EnemyHealth).
    public void SetKnocked(bool knocked)
    {
        isKnocked = knocked;
    }

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
        if (directionX > 0 && direction == -1) SetDirection(1);
        else if (directionX < 0 && direction == 1) SetDirection(-1);
    }

    // Aplica la dirección (1 = player a la derecha, -1 = a la izquierda), invirtiendo
    // la escala si el sprite original está dibujado hacia el lado contrario.
    private void SetDirection(int newDirection)
    {
        direction = newDirection;

        float scaleX = spriteFacesLeft ? -direction : direction;
        transform.localScale = new Vector3(scaleX, 1, 1);
    }
}
