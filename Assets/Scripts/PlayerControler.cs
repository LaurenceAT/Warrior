using System;
using System.Collections;
using System.Collections.Generic;
//using UnityEditor.Tilemaps;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerControler : MonoBehaviour
{
    // Forma del area de golpe del ataque.
    // Arco es un trozo de anillo: sirve para espadazos en media luna.
    public enum AttackShape { Circulo, Caja, Arco }

    // Por donde se cierra la punta de la media luna.
    // Interior: el borde de dentro se abre hacia fuera (punta hacia el exterior).
    // Exterior: el borde de fuera se recoge hacia dentro (punta hacia el centro).
    // Ambas: los dos se juntan a media altura, que es lo mas parecido a una hoja.
    public enum ArcTip { Interior, Exterior, Ambas }

    // Un golpe concreto: donde cae su area, que forma tiene y cuanto dana.
    // Tener varios permite que cada ataque de un combo use su propia zona,
    // ajustada a lo que hace el sprite en ese momento.
    [System.Serializable]
    public class AttackProfile
    {
        public string nombre = "Ataque";
        public AttackShape forma = AttackShape.Caja;
        // Desplazamiento respecto al AttackPoint. La X se invierte sola al girar
        // el personaje, asi que basta con ajustarlo mirando a la derecha.
        public Vector2 offset = Vector2.zero;
        // Solo se usa con la forma Circulo.
        public float radio = 0.4f;
        // Ancho y alto, solo con la forma Caja.
        public Vector2 tamano = new Vector2(0.8f, 0.5f);

        // --- Solo con la forma Arco ---
        // Hueco central de la media luna: lo que este mas cerca que esto no recibe golpe.
        public float radioInterior = 0.15f;
        // Hasta donde llega la hoja.
        public float radioExterior = 0.8f;
        // Apertura total del abanico en grados. 30 es una estocada, 180 media vuelta.
        public float angulo = 120f;
        // Hacia donde apunta el centro del abanico. 0 al frente, 90 arriba, -90 abajo.
        // Se voltea solo al girar el personaje.
        public float anguloCentro;
        // Cuanto se afilan las puntas de la media luna. A 0 los extremos quedan
        // rectos (media torta); a 1 el anillo se cierra y acaba en punta.
        [Range(0f, 1f)] public float afiladoPuntas = 0.8f;
        // Por donde se cierra esa punta.
        public ArcTip puntaHacia = ArcTip.Ambas;

        public int dano = 1;
    }

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
    private int idVerticalSpeed;
    private int idKnockback;
    private static readonly int IdDoorIn = Animator.StringToHash("doorIn");
    private static readonly int IdAttack = Animator.StringToHash("attack");
    private static readonly int IdDoubleJump = Animator.StringToHash("doubleJump");

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
    // Punto de referencia desde el que se mide el area de cada golpe.
    [SerializeField] private Transform attackPoint;
    // Capas que pueden recibir el golpe. Vacio = todas, como hasta ahora.
    [SerializeField] private LayerMask attackLayers;
    // Un perfil por golpe. El 0 es el ataque normal; los siguientes son para los
    // combos, y se eligen desde el Animation Event con DealAttackCombo(n).
    [SerializeField] private AttackProfile[] attackProfiles = { new AttackProfile() };
    // Cual de los perfiles se dibuja en la escena, para ajustarlos de uno en uno.
    [SerializeField] private int gizmoProfileIndex;
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
        idVerticalSpeed = Animator.StringToHash("VerticalSpeed");
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

        // Dibuja el area del perfil que estes ajustando, en su posicion real.
        AttackProfile perfil = PerfilDeAtaque(gizmoProfileIndex);
        if (perfil == null) return;

        Vector3 centro = CentroDelGolpe(perfil);

        Gizmos.color = Color.red;
        if (perfil.forma == AttackShape.Caja)
            Gizmos.DrawWireCube(centro, perfil.tamano);
        else if (perfil.forma == AttackShape.Arco)
            DibujarArco(perfil, centro);
        else
            Gizmos.DrawWireSphere(centro, perfil.radio);

        // Punto de referencia, para ver cuanto lo ha desplazado el offset.
        if (attackPoint == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(attackPoint.position, 0.05f);
        Gizmos.DrawLine(attackPoint.position, centro);
    }

    // Dibuja la media luna: los dos arcos y los dos bordes que los cierran.
    private void DibujarArco(AttackProfile perfil, Vector3 centro)
    {
        const int pasos = 24;

        Vector2 frente = FrenteDelArco(perfil);
        float anguloFrente = Mathf.Atan2(frente.y, frente.x) * Mathf.Rad2Deg;
        float mitad = perfil.angulo * 0.5f;
        float desde = anguloFrente - mitad;

        Vector3 PuntoDelArco(float grados, float radio)
        {
            return centro + (Quaternion.Euler(0f, 0f, grados) * Vector3.right) * radio;
        }

        // Los dos radios dependen del angulo, por eso se recalculan en cada paso.
        float TEnPaso(int paso)
        {
            return mitad <= 0.0001f ? 0f : Mathf.Abs(paso / (float)pasos - 0.5f) * 2f;
        }

        Vector3 interiorPrevio = PuntoDelArco(desde, RadioInteriorEn(perfil, TEnPaso(0)));
        Vector3 exteriorPrevio = PuntoDelArco(desde, RadioExteriorEn(perfil, TEnPaso(0)));

        // Borde inicial.
        Gizmos.DrawLine(interiorPrevio, exteriorPrevio);

        for (int i = 1; i <= pasos; i++)
        {
            float grados = desde + perfil.angulo * i / pasos;
            Vector3 interior = PuntoDelArco(grados, RadioInteriorEn(perfil, TEnPaso(i)));
            Vector3 exterior = PuntoDelArco(grados, RadioExteriorEn(perfil, TEnPaso(i)));

            Gizmos.DrawLine(interiorPrevio, interior);
            Gizmos.DrawLine(exteriorPrevio, exterior);

            interiorPrevio = interior;
            exteriorPrevio = exterior;
        }

        // Borde final.
        Gizmos.DrawLine(interiorPrevio, exteriorPrevio);
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
        // Mientras aparece, el player se queda congelado en el sitio. Sin esto la
        // gravedad lo hace caer mientras crece, y se ve como si bajara flotando y
        // quieto hasta que arranca la animacion de caida.
        float gravedadOriginal = m_rigitbody2D.gravityScale;
        m_rigitbody2D.linearVelocity = Vector2.zero;
        m_rigitbody2D.gravityScale = 0f;

        // Aqui NO forzamos ninguna animacion: el Animator entra por PlayerDoorOut,
        // cuyo Animation Event llama a DoorOut() y abre la puerta de entrada.
        // Si lo sacamos de ese estado con un Play(), la puerta nunca se abre.

        // Aparece creciendo desde la puerta. El personaje nuevo no tiene animacion
        // propia de salir por la puerta, asi que la resolvemos con escala y alpha.
        yield return TrapFx.ScaleAndFade(m_transform, m_spriteRenderer,
            m_transform.localScale, ColorDelSprite(), 0f, 1f, moveDelay, true);

        m_rigitbody2D.gravityScale = gravedadOriginal;
        canMove = true;
    }

    // Color actual del sprite, o blanco si por lo que sea no hay SpriteRenderer.
    private Color ColorDelSprite()
    {
        return m_spriteRenderer != null ? m_spriteRenderer.color : Color.white;
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
        // Solo invertimos la X y conservamos el resto de la escala. Antes se forzaba
        // Y y Z a 1, asi que si el prefab estaba escalado (por ejemplo a 1.5 para que
        // el personaje se viera mas grande) el primer giro lo dejaba deformado.
        Vector3 escala = m_transform.localScale;
        escala.x = -escala.x;
        m_transform.localScale = escala;

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

        // El segundo salto usa su propia animacion (la voltereta) en lugar de repetir
        // la del primero. Al llegar al punto mas alto, el Animator pasa solo a Fall.
        m_animator.SetTrigger(IdDoubleJump);
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
        AplicarGolpe(0);
    }

    // Para los combos: se llama desde el Animation Event pasando el indice del perfil.
    public void DealAttackCombo(int index)
    {
        AplicarGolpe(index);
    }

    // Aplica el golpe del perfil indicado a todo lo que haya en su area.
    private void AplicarGolpe(int index)
    {
        AttackProfile perfil = PerfilDeAtaque(index);
        if (perfil == null) return;

        Collider2D[] hits = BuscarObjetivos(perfil);
        Vector2 centro = CentroDelGolpe(perfil);
        HashSet<EnemyHealth> alreadyHit = new HashSet<EnemyHealth>();

        foreach (Collider2D hit in hits)
        {
            if (!hit.CompareTag("Enemy")) continue;
            if (!EstaDentroDelArco(perfil, centro, hit)) continue;

            EnemyHealth enemyHealth = hit.GetComponent<EnemyHealth>();
            // Un mismo enemigo puede tener varios colliders (trigger de daño + sólido de suelo);
            // alreadyHit.Add evita contarlo dos veces en el mismo golpe.
            if (enemyHealth != null && alreadyHit.Add(enemyHealth))
                enemyHealth.TakeDamage(perfil.dano, m_transform.position);
        }
    }

    // Devuelve el perfil pedido, o el mas cercano si el indice se sale del array.
    private AttackProfile PerfilDeAtaque(int index)
    {
        if (attackProfiles == null || attackProfiles.Length == 0) return null;
        return attackProfiles[Mathf.Clamp(index, 0, attackProfiles.Length - 1)];
    }

    // Centro del area de golpe: el AttackPoint mas el desplazamiento del perfil,
    // con la X volteada segun hacia donde mire el player.
    private Vector2 CentroDelGolpe(AttackProfile perfil)
    {
        Transform origen = attackPoint != null ? attackPoint : transform;
        return (Vector2)origen.position + new Vector2(perfil.offset.x * direction, perfil.offset.y);
    }

    // Devuelve lo que hay dentro del area, segun la forma del perfil.
    private Collider2D[] BuscarObjetivos(AttackProfile perfil)
    {
        // Si no se indican capas usamos todas, para no romper los prefabs de antes.
        int capas = attackLayers.value == 0 ? ~0 : attackLayers.value;
        Vector2 centro = CentroDelGolpe(perfil);

        if (perfil.forma == AttackShape.Caja)
            return Physics2D.OverlapBoxAll(centro, perfil.tamano, 0f, capas);

        // Con Arco cogemos primero todo lo que cae dentro del radio exterior;
        // el anillo y el abanico se recortan despues en EstaDentroDelArco.
        if (perfil.forma == AttackShape.Arco)
            return Physics2D.OverlapCircleAll(centro, perfil.radioExterior, capas);

        return Physics2D.OverlapCircleAll(centro, perfil.radio, capas);
    }

    // Recorta el circulo hasta dejar la media luna. Con las otras formas no hace nada,
    // porque OverlapBox y OverlapCircle ya son exactos.
    private bool EstaDentroDelArco(AttackProfile perfil, Vector2 centro, Collider2D objetivo)
    {
        if (perfil.forma != AttackShape.Arco) return true;

        // Medimos al punto del collider mas cercano, no a su transform: asi un enemigo
        // grande se detecta por su cuerpo y no por donde tenga el origen.
        Vector2 haciaObjetivo = objetivo.ClosestPoint(centro) - centro;
        float distancia = haciaObjetivo.magnitude;

        if (distancia > perfil.radioExterior) return false;
        if (distancia <= 0.0001f) return perfil.radioInterior <= 0f;

        // Fuera del abanico.
        float mitad = perfil.angulo * 0.5f;
        float desviacion = Vector2.Angle(FrenteDelArco(perfil), haciaObjetivo);
        if (desviacion > mitad) return false;

        // Los dos bordes se estrechan hacia la punta, asi que aqui se compara
        // contra los radios de ESE angulo, no contra los fijos del perfil.
        float t = mitad <= 0.0001f ? 0f : desviacion / mitad;
        return distancia >= RadioInteriorEn(perfil, t)
            && distancia <= RadioExteriorEn(perfil, t);
    }

    // Cuanto se ha cerrado el anillo a una desviacion t del centro del abanico.
    // t va de 0 (centro del golpe) a 1 (punta). Al cuadrado para que el centro se
    // mantenga ancho y solo adelgacen los ultimos grados.
    private float Estrechamiento(AttackProfile perfil, float t)
    {
        return Mathf.Pow(Mathf.Clamp01(t), 2f) * perfil.afiladoPuntas;
    }

    private float RadioInteriorEn(AttackProfile perfil, float t)
    {
        if (perfil.puntaHacia == ArcTip.Exterior) return perfil.radioInterior;

        float k = Estrechamiento(perfil, t);
        // Con Ambas los dos bordes se encuentran a media altura del anillo.
        float destino = perfil.puntaHacia == ArcTip.Ambas
            ? (perfil.radioInterior + perfil.radioExterior) * 0.5f
            : perfil.radioExterior;

        return Mathf.Lerp(perfil.radioInterior, destino, k);
    }

    private float RadioExteriorEn(AttackProfile perfil, float t)
    {
        if (perfil.puntaHacia == ArcTip.Interior) return perfil.radioExterior;

        float k = Estrechamiento(perfil, t);
        float destino = perfil.puntaHacia == ArcTip.Ambas
            ? (perfil.radioInterior + perfil.radioExterior) * 0.5f
            : perfil.radioInterior;

        return Mathf.Lerp(perfil.radioExterior, destino, k);
    }

    // Direccion a la que mira el centro del abanico, ya volteada segun el personaje.
    private Vector2 FrenteDelArco(AttackProfile perfil)
    {
        return Quaternion.Euler(0f, 0f, perfil.anguloCentro * direction) * new Vector2(direction, 0f);
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
        // Se encoge y se desvanece al entrar por la puerta, en lugar de desaparecer
        // de golpe al cambiar de escena.
        yield return TrapFx.ScaleAndFade(m_transform, m_spriteRenderer,
            m_transform.localScale, ColorDelSprite(), 1f, 0f, moveDelay, false);

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
        // Velocidad vertical: distingue subida (Jump) de caida (Fall) en el Animator.
        m_animator.SetFloat(idVerticalSpeed, m_rigitbody2D.linearVelocityY);
    }

    #endregion
}
