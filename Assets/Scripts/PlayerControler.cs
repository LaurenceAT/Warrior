using System;
using System.Collections;
using System.Collections.Generic;
//using UnityEditor.Tilemaps;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerControler : MonoBehaviour
{
    //COMPONENTES DE PLAYER
    [Header("Componentes")]
    [SerializeField] private Transform m_transform;
    private Rigidbody2D m_rigitbody2D;
    private GatherInput m_gatherInput;
    private Animator m_animator;
    private SpriteRenderer m_spriteRenderer;

    //ANIMATOR IDS
    //VARIABLES HASH DEL AIMATOR
    private int idIsGrounded;
    private int idSpeed;
    private int idIsWallDetected;
    private int idIsWallSliding;
    private int idKnockback;
    private static readonly int IdDoorIn = Animator.StringToHash("doorIn");
    private static readonly int IdAttack = Animator.StringToHash("attack");

    [Header("Opciones de Movimiento y Salto")]
    [SerializeField] private float speed;
    [SerializeField] private bool canMove;
    [SerializeField] private float moveDelay;
    private int direction = 1;

    //VIDA
    [Header("Health")]
    [SerializeField] private int maxHealth = 5;
    [SerializeField] private int currentHealth;
    [SerializeField] private float invincibleTime = 1f;
    // Cada cuánto parpadea el sprite mientras el player es invulnerable.
    [SerializeField] private float blinkInterval = 0.1f;
    [SerializeField] private HealthBar healthBar;

    private bool isInvincible;

    //VARIABLES PARA CONTROLAR LOS SALTOS DEL PLAYER EN UNITY
    [SerializeField] private float jumpForce;
    [SerializeField] private int extraJumps;
    [SerializeField] private int counterExtraJumps;
    [SerializeField] private bool canDoubleJump;

    //RAYCAST PARA DETECCION DEL SUELO
    [Header("Opciones del Ground")]
    [SerializeField] private Transform lFoot;
    [SerializeField] private Transform rFoot;
    RaycastHit2D lFootRay;
    RaycastHit2D rFootRay;
    [SerializeField] private bool isGrounded;
    [SerializeField] private float rayLength;
    [SerializeField] private LayerMask groundLayer;

    //VARIABLES PARA DETECCION Y DESLIZAMIENTO EN LA PARED
    [Header("Opciones de la Pared")]
    [SerializeField] private float checkWallDistance;
    [SerializeField] private bool isWallDetected;
    // Lado en el que está la pared (1 derecha, -1 izquierda). Es independiente de hacia
    // dónde mira el player, que puede girarse para atacar sin soltar el muro.
    [SerializeField] private int wallDirection = 1;
    [SerializeField] private bool canWallSlide;
    // Verdadero solo cuando el player está realmente deslizándose: pegado a la pared,
    // en el aire y sin estar atacando. Es lo que lee el Animator.
    [SerializeField] private bool isWallSliding;
    [SerializeField] private float slideSpeed;
    [SerializeField] private Vector2 wallJumpForce;
    [SerializeField] private bool isWallJumping;
    [SerializeField] private float wallJumpDuration;

    //IMPULSO EXTERNO (flechas giratorias, trampolines...)
    [Header("Opciones de Impulso")]
    [SerializeField] private bool isLaunched;
    // Si está marcado, salir impulsado devuelve los saltos extra.
    [SerializeField] private bool launchRestoresJumps = true;
    private Coroutine launchRoutine;

    [Header("Knock settings")]
    [SerializeField] private bool isKnocked;
    //[SerializeField] private bool canBeKnocked;
    [SerializeField] private Vector2 knockedPower;
    [SerializeField] private float knockedDuration;

    [Header("Death VFX")]
    [SerializeField] private GameObject deathVFX;

    //VARIABLES PARA EL SISTEMA DE ATAQUE
    [Header("Opciones de Ataque")]
    [SerializeField] private Transform attackPoint;
    [SerializeField] private float attackRadius = 0.6f;
    [SerializeField] private int attackDamage = 1;
    [SerializeField] private float attackCooldown = 0.4f;
    [SerializeField] private bool isAttacking;
    private bool canAttack = true;

    #region Unity Lifecycle

    // Obtiene los componentes principales del player y define su estado inicial (respawn/checkpoint).
    private void Awake()
    {
        m_gatherInput = GetComponent<GatherInput>();
        m_transform = GetComponent<Transform>();
        m_rigitbody2D = GetComponent<Rigidbody2D>();
        m_animator = GetComponent<Animator>();
        m_spriteRenderer = GetComponent<SpriteRenderer>();
        CheckPlayerRespawnState();
    }

    // Inicializa los hashes del Animator y la vida del player al comenzar la escena.
    void Start()
    {
        idSpeed = Animator.StringToHash("Speed");
        idIsGrounded = Animator.StringToHash("isGrounded");
        idIsWallDetected = Animator.StringToHash("isWallDetected");
        idIsWallSliding = Animator.StringToHash("isWallSliding");
        idKnockback = Animator.StringToHash("knockback");
        // CONFIGURA EL ESTADO DEL PLAYER AL REAPARECER
        counterExtraJumps = extraJumps;
        //vida
        currentHealth = maxHealth;

        if (healthBar == null)
        {
            healthBar = FindFirstObjectByType<HealthBar>();
        }

        healthBar.UpdateHealthBar(currentHealth, maxHealth);
    }

    // Actualiza los parámetros del Animator cada frame.
    private void Update()
    {
        SetAnimatorValues();
    }

    // Actualiza físicas, colisiones, movimiento y salto en el paso de física.
    void FixedUpdate()
    {
        if (!canMove) return;
        if (isKnocked) return;
        CheckCollision();
        Move();
        Jump();
        Attack();
    }

    // Dibuja en el editor la línea de detección de pared y el radio de ataque para depurar.
    private void OnDrawGizmos()
    {
        Gizmos.DrawLine(m_transform.position, new Vector2(m_transform.position.x + (checkWallDistance * direction), m_transform.position.y));

        if (attackPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(attackPoint.position, attackRadius);
        }
    }

    #endregion

    #region Respawn / Checkpoint

    // Define si el player puede moverse de inmediato (si hay checkpoint activo) o debe esperar (respawn inicial).
    private void CheckPlayerRespawnState()
    {
        if (GameManager.Instance.hasCheckPointActive)
        {
            canMove = true;
            StartInCheckpoint();
        }
        else
        {
            canMove = false;
            StartCoroutine(CanMoveRoutine());
        }
    }

    // Reproduce la animación de reposo al reaparecer en un checkpoint.
    private void StartInCheckpoint()
    {
        m_animator.Play("PlayerIdle");
    }

    // Habilita el movimiento del player luego de un pequeño retraso (usado en el respawn inicial).
    private IEnumerator CanMoveRoutine()
    {
        yield return new WaitForSeconds(moveDelay);
        canMove = true;
    }

    #endregion

    #region Movimiento

    // Aplica la velocidad horizontal según el input y gira al player si cambia de dirección.
    private void Move()
    {
        if (!canMove) return;
        if (isWallDetected && !isGrounded) return;
        if (isWallJumping) return;
        if (isLaunched) return;
        if (isAttacking) return;

        Flip();
        m_rigitbody2D.linearVelocity = new Vector2(speed * m_gatherInput.Value.x, m_rigitbody2D.linearVelocityY);
    }

    // Detecta si el input de movimiento cambió de dirección respecto a la actual.
    private void Flip()
    {
        if (m_gatherInput.Value.x * direction < 0)
        {
            HandleDirection();
        }
    }

    // Invierte la escala del sprite y actualiza la dirección hacia la que mira el player.
    private void HandleDirection()
    {
        m_transform.localScale = new Vector3(-m_transform.localScale.x, 1, 1);
        direction *= -1;
    }

    // Fija hacia qué lado mira el player, sin depender de hacia dónde miraba antes.
    private void SetFacing(int newDirection)
    {
        if (newDirection == 0 || newDirection == direction) return;
        HandleDirection();
    }

    #endregion

    #region Salto

    // Procesa el input de salto: salto normal desde el suelo, salto de pared o salto extra (doble salto).
    private void Jump()
    {
        if (m_gatherInput.IsJumping)
        {
            if (isGrounded)
            {
                m_rigitbody2D.linearVelocity = new Vector2(speed * m_gatherInput.Value.x, jumpForce);
                canDoubleJump = true;
            }
            else if (isWallDetected) WallJump();
            else if (counterExtraJumps > 0 && canDoubleJump) DoubleJump();
        }
        m_gatherInput.IsJumping = false;
    }

    // Impulsa al player alejándolo de la pared y gira su dirección.
    private void WallJump()
    {
        // El impulso se calcula desde el lado del muro, no desde hacia dónde mira el
        // player: si acaba de atacar hacia fuera, seguiría saltando en la dirección correcta.
        m_rigitbody2D.linearVelocity = new Vector2(wallJumpForce.x * -wallDirection, wallJumpForce.y);
        SetFacing(-wallDirection);
        StartCoroutine(WallJumpRoutine());
    }

    // Bloquea temporalmente el movimiento horizontal mientras dura el salto de pared.
    IEnumerator WallJumpRoutine()
    {
        isWallJumping = true;
        yield return new WaitForSeconds(wallJumpDuration);
        isWallJumping = false;
    }

    // Ejecuta un salto extra en el aire y descuenta un uso del contador de saltos extra.
    private void DoubleJump()
    {
        m_rigitbody2D.linearVelocity = new Vector2(speed * m_gatherInput.Value.x, jumpForce);
        counterExtraJumps -= 1;
    }

    #endregion

    #region Impulso

    // Lanza al player en una dirección concreta. Lo llaman las trampas de impulso,
    // como la flecha giratoria.
    // El control horizontal se bloquea durante controlLockTime porque Move() reescribe
    // la velocidad cada FixedUpdate y, sin ese bloqueo, anularía el impulso al instante.
    public void Launch(Vector2 velocity, float controlLockTime)
    {
        m_rigitbody2D.linearVelocity = velocity;

        if (launchRestoresJumps)
        {
            counterExtraJumps = extraJumps;
            canDoubleJump = true;
        }

        // Si ya venía de otro impulso, reiniciamos el bloqueo en vez de acumular corrutinas.
        if (launchRoutine != null) StopCoroutine(launchRoutine);
        launchRoutine = StartCoroutine(LaunchRoutine(controlLockTime));
    }

    // Mantiene bloqueado el control horizontal mientras dura el impulso.
    private IEnumerator LaunchRoutine(float duration)
    {
        isLaunched = true;
        yield return new WaitForSeconds(duration);
        isLaunched = false;
        launchRoutine = null;
    }

    // Rebote vertical, para los trampolines.
    // A diferencia de Launch() no bloquea el control: Move() respeta la velocidad
    // vertical, así que el impulso sobrevive y el jugador conserva el control
    // horizontal en el aire, que es como mejor se siente un trampolín.
    public void Bounce(float upwardVelocity)
    {
        m_rigitbody2D.linearVelocity = new Vector2(m_rigitbody2D.linearVelocityX, upwardVelocity);

        if (launchRestoresJumps)
        {
            counterExtraJumps = extraJumps;
            canDoubleJump = true;
        }
    }

    #endregion

    #region Ataque

    // Procesa el input de ataque: dispara la animación si no está en cooldown, sin importar si hay un enemigo cerca.
    private void Attack()
    {
        if (m_gatherInput.IsAttacking && canAttack)
        {
            // Pegado a una pared el golpe sale hacia fuera: atacar al muro no tiene
            // sentido y encima el espadazo queda oculto dentro del tile.
            bool desdePared = canWallSlide;
            if (desdePared) SetFacing(-wallDirection);

            m_animator.SetTrigger(IdAttack);
            StartCoroutine(AttackRoutine(desdePared));
        }
        m_gatherInput.IsAttacking = false;
    }

    // Bloquea el movimiento y los ataques nuevos mientras dura la animación de ataque.
    private IEnumerator AttackRoutine(bool volverAMirarLaPared)
    {
        canAttack = false;
        isAttacking = true;

        yield return new WaitForSeconds(attackCooldown);

        isAttacking = false;
        canAttack = true;

        // Si sigue colgado del muro, vuelve a mirarlo para que el deslizamiento
        // no se quede reproduciéndose del revés.
        if (volverAMirarLaPared && canWallSlide) SetFacing(wallDirection);
    }

    // Aplica daño a los enemigos dentro del radio de ataque.
    // Se debe llamar mediante un Animation Event en el frame de "PlayerAttack" donde el golpe conecta.
    public void DealAttackDamage()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(attackPoint.position, attackRadius);
        HashSet<EnemyHealth> alreadyHit = new HashSet<EnemyHealth>();

        foreach (Collider2D hit in hits)
        {
            if (!hit.CompareTag("Enemy")) continue;

            EnemyHealth enemyHealth = hit.GetComponent<EnemyHealth>();
            // Un mismo enemigo puede tener varios colliders (trigger de daño + sólido de suelo);
            // alreadyHit.Add evita contarlo dos veces en el mismo golpe.
            if (enemyHealth != null && alreadyHit.Add(enemyHealth))
                enemyHealth.TakeDamage(attackDamage, m_transform.position);
        }
    }

    #endregion

    #region Detección de Colisiones

    // Agrupa las comprobaciones de suelo, pared y deslizamiento en pared.
    private void CheckCollision()
    {
        HandleGround();
        HandleWall();
        HandleWallSlide();
    }

    // Detecta el suelo mediante raycasts desde ambos pies y recarga los saltos extra al aterrizar.
    private void HandleGround()
    {
        lFootRay = Physics2D.Raycast(lFoot.position, Vector2.down, rayLength, groundLayer);
        rFootRay = Physics2D.Raycast(rFoot.position, Vector2.down, rayLength, groundLayer);
        if (lFootRay || rFootRay)
        {
            isGrounded = true;
            counterExtraJumps = extraJumps;
            canDoubleJump = false;
        }
        else
        {
            isGrounded = false;
        }
    }

    // Detecta paredes frente al player y recarga el doble salto si está en el aire junto a una pared.
    private void HandleWall()
    {
        // Lanzamos el rayo a los dos lados en vez de solo hacia donde mira el player.
        // Así puede girarse para atacar hacia fuera sin que el juego "pierda" la pared
        // que tiene a la espalda y lo deje caer.
        bool paredDelante = Physics2D.Raycast(m_transform.position, Vector2.right * direction, checkWallDistance, groundLayer);
        bool paredDetras = Physics2D.Raycast(m_transform.position, Vector2.right * -direction, checkWallDistance, groundLayer);

        isWallDetected = paredDelante || paredDetras;

        // Si hay muro a los dos lados manda el de delante.
        if (paredDelante) wallDirection = direction;
        else if (paredDetras) wallDirection = -direction;

        if (isWallDetected && !isGrounded)
        {
            counterExtraJumps = extraJumps;
            canDoubleJump = true;
        }
    }

    // Reduce la velocidad de caída mientras el player está pegado a una pared (deslizamiento).
    private void HandleWallSlide()
    {
        // Solo hay deslizamiento pegado a una pared y en el aire. De pie junto a un muro
        // el rayo también lo detecta, pero ahí no se desliza nada.
        canWallSlide = isWallDetected && !isGrounded;

        // El flag que lee el Animator excluye además el ataque, para que el espadazo
        // se vea entero antes de volver al deslizamiento.
        isWallSliding = canWallSlide && !isLaunched && !isAttacking;

        if (!canWallSlide) return;
        // Durante un impulso no frenamos la caída: si no, rozar una pared anularía el lanzamiento.
        if (isLaunched) return;
        canDoubleJump = true;
        slideSpeed = m_gatherInput.Value.y < 0 ? 1 : 0.5f;
        m_rigitbody2D.linearVelocity = new Vector2(m_rigitbody2D.linearVelocityX, m_rigitbody2D.linearVelocityY * slideSpeed);
    }

    #endregion

    #region Vida y Daño

    // Aplica daño al player, actualiza la barra de vida, dispara el knockback y controla la muerte.
    public void TakeDamage(int damage)
    {
        // Ignora el daño durante la invulnerabilidad. Esto evita recibir dos golpes a la vez
        // (el contacto del cuerpo del enemigo y el espadazo de su animación).
        if (isInvincible) return;

        currentHealth -= damage;

        healthBar.UpdateHealthBar(currentHealth, maxHealth);

        Debug.Log("Vida actual: " + currentHealth);

        Knockback();

        if (currentHealth <= 0)
        {
            Die();
            GameManager.Instance.RespawnPlayer();
            return;
        }

        StartCoroutine(InvincibleRoutine());
    }

    // Aplica el impulso de retroceso al recibir un golpe.
    public void Knockback()
    {
        StartCoroutine(KnockbackRoutine());
        m_rigitbody2D.linearVelocity = new Vector2(knockedPower.x * -direction, knockedPower.y);
    }

    // Mantiene bloqueado el movimiento del player durante la animación/duración del knockback.
    private IEnumerator KnockbackRoutine()
    {
        isKnocked = true;
        m_animator.SetBool("isKnockback", isKnocked);
        yield return new WaitForSeconds(knockedDuration);
        isKnocked = false;
        m_animator.SetBool("isKnockback", isKnocked);
    }

    // Hace invulnerable al player durante invincibleTime, parpadeando el sprite para que se note.
    private IEnumerator InvincibleRoutine()
    {
        isInvincible = true;

        float elapsed = 0f;
        while (elapsed < invincibleTime)
        {
            if (m_spriteRenderer != null)
                m_spriteRenderer.enabled = !m_spriteRenderer.enabled;

            yield return new WaitForSeconds(blinkInterval);
            elapsed += blinkInterval;
        }

        if (m_spriteRenderer != null)
            m_spriteRenderer.enabled = true;

        isInvincible = false;
    }

    // Actualiza la barra de vida a cero, instancia el VFX de muerte y destruye al player.
    public void Die()
    {
        healthBar.UpdateHealthBar(0, maxHealth);
        GameObject deathVFXPrefab = Instantiate(deathVFX, m_transform.position, Quaternion.identity);
        Destroy(gameObject);
    }

    #endregion

    #region Puertas / Nivel

    // Detiene al player, reproduce la animación de entrada a la puerta y comienza el cambio de nivel.
    public void DoorIn()
    {
        m_rigitbody2D.linearVelocity = Vector2.zero;
        m_animator.Play("PlayerIdle");
        m_animator.SetBool(IdDoorIn, true);
        canMove = false;
        StartCoroutine(DoorInRoutine());
    }

    // Espera a que termine la animación de la puerta y carga el siguiente nivel.
    private IEnumerator DoorInRoutine()
    {
        yield return new WaitForSeconds(moveDelay);
        GameManager.Instance.LoadNextLevel();
    }

    #endregion

    #region Animator

    // Envía la velocidad, el estado de suelo y el de pared al Animator.
    private void SetAnimatorValues()
    {
        m_animator.SetFloat(idSpeed, Mathf.Abs(m_rigitbody2D.linearVelocityX));
        m_animator.SetBool(idIsGrounded, isGrounded);
        m_animator.SetBool(idIsWallDetected, isWallDetected);
        m_animator.SetBool(idIsWallSliding, isWallSliding);
    }

    #endregion
}
