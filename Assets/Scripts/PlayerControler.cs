using System;
using System.Collections;
using System.Collections.Generic;
//using UnityEditor.Tilemaps;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Cinemachine;

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

        // Duracion real del clip de este golpe, en segundos. Es lo que decide
        // cuanto se protege la animacion y cuando se admite el siguiente golpe.
        public float duracion = 0.4f;
        // Segundo a partir del cual se acepta encadenar. Los fotogramas de
        // recuperacion se cortan solo si el jugador sigue atacando, que es lo que
        // hace que una cadena se sienta fluida en vez de a trompicones.
        public float encadenarDesde = 0.3f;
        // Empujon hacia delante al lanzar el golpe. Da sensacion de peso.
        public float avance = 1.5f;

        // --- Impacto propio de este golpe ---
        // Congelacion al conectar. En negativo usa la del Inspector general.
        // Subirla solo en el remate del combo es lo que hace que se note
        // mas fuerte que los dos primeros.
        public float congelacion = -1f;
        // Fuerza de la sacudida de camara. A 0 este golpe no sacude.
        public float sacudida = 0.15f;
        // Efecto que aparece en el punto del golpe (un corte, chispas...).
        // Se puede dejar vacio.
        public GameObject efectoGolpe;
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
    private int idKnockDown;
    private int idIsSprinting;
    private int idIsWallRunning;
    private int idIsDodging;
    private static readonly int IdDoorIn = Animator.StringToHash("doorIn");
    private static readonly int IdAttack = Animator.StringToHash("attack");
    private static readonly int IdDoubleJump = Animator.StringToHash("doubleJump");
    private static readonly int IdComboIndex = Animator.StringToHash("comboIndex");
    private static readonly int IdGetUp = Animator.StringToHash("getUp");
    private static readonly int IdPlungePhase = Animator.StringToHash("plungePhase");
    // La entrada va por trigger y no por plungePhase: una transicion desde Any State
    // con plungePhase == 1 se volveria a disparar en cada fotograma mientras cae.
    private static readonly int IdPlunge = Animator.StringToHash("plunge");

    [Header("Opciones de Movimiento y Salto")]
    [SerializeField] private float speed;
    [SerializeField] private bool canMove;
    [SerializeField] private float moveDelay;
    private int direction = 1;

    [Header("Carrera (Shift)")]
    // Velocidad con el boton de correr pulsado. Si la dejas igual que "speed"
    // la mecanica queda desactivada de hecho, sin tocar codigo.
    [SerializeField] private float sprintSpeed = 8.5f;
    // Unidades por segundo que gana o pierde al entrar y salir de la carrera.
    // Sin esta rampa el cambio de velocidad se siente como un tiron.
    [SerializeField] private float sprintAcceleration = 30f;
    // Si esta marcado, soltar el boton en el aire no corta la carrera: el impulso
    // se conserva hasta aterrizar, que es como se comportan la mayoria de plataformas.
    [SerializeField] private bool keepSprintInAir = true;
    // Velocidad horizontal actual, ya rampeada. Es la que se aplica al Rigidbody.
    private float currentSpeed;
    private bool isSprinting;

    [Header("Ataques aereos")]
    // Perfil del primer golpe aereo. El segundo usa el siguiente. Con 3 golpes
    // de suelo (perfiles 0, 1 y 2), los aereos son el 3 y el 4. El Animator los
    // recibe por el mismo parametro comboIndex.
    [SerializeField] private int airAttackFirstProfile = 3;
    [SerializeField] private int airComboLength = 2;
    // Al atacar en el aire la caida se frena un poco (0 = se queda quieto, 1 = no
    // se frena). Da tiempo a que el espadazo se vea sin convertirlo en un planeo.
    [Range(0f, 1f)] [SerializeField] private float airAttackHang = 0.3f;
    // Acertar a un enemigo en el aire devuelve el salto extra y los golpes
    // aereos, asi se pueden encadenar enemigos sin tocar el suelo.
    [SerializeField] private bool airHitRestoresJump = true;
    private int airComboIndex;
    private bool atacandoEnAire;

    [Header("Estocada hacia abajo (S + ataque en el aire)")]
    // Areas propias, relativas al centro del player (no al AttackPoint). Antes
    // usaba los perfiles 5 y 6, y si no existian se cogia el ultimo de la lista:
    // el golpe 3 de suelo, que esta DELANTE del personaje. La caida buscaba
    // enemigos enfrente en vez de debajo y nunca les daba.
    // Caja bajo los pies durante la caida. Si toca un enemigo, rebota.
    [SerializeField] private Vector2 plungeHitOffset = new Vector2(0f, -0.7f);
    [SerializeField] private Vector2 plungeHitSize = new Vector2(0.6f, 0.4f);
    // Caja del impacto contra el suelo, mas ancha: pilla a los que estan al lado.
    [SerializeField] private Vector2 plungeLandOffset = new Vector2(0f, -0.45f);
    [SerializeField] private Vector2 plungeLandSize = new Vector2(2f, 0.6f);
    [SerializeField] private int plungeDamage = 2;
    // Cuanto mas lejos que un golpe normal sale despedido el enemigo.
    [SerializeField] private float plungeKnockback = 2.5f;
    [SerializeField] private float plungeHitStop = 0.08f;
    [SerializeField] private bool showPlungeGizmos = true;
    // Instante de suspension antes de caer: es lo que avisa del golpe y lo
    // hace leerse como un ataque cargado y no como una caida sin mas.
    [SerializeField] private float plungeWindup = 0.1f;
    [SerializeField] private float plungeSpeed = 18f;
    // Velocidad con la que sale despedido el enemigo al atravesarlo cayendo.
    // X hacia fuera (el lado lo decide su posicion respecto al player), Y hacia
    // arriba. Lo manda a volar sin sacarlo de la pantalla.
    [SerializeField] private Vector2 plungeLaunch = new Vector2(3.5f, 7f);
    // Cuanto se queda clavado tras impactar contra el suelo.
    [SerializeField] private float plungeLandTime = 0.25f;
    [SerializeField] private float plungeLandShake = 0.35f;
    // Red de seguridad por si nunca llega a tocar suelo (un pozo sin fondo).
    [SerializeField] private float plungeMaxTime = 1.5f;
    [SerializeField] private bool isPlunging;
    private Coroutine plungeRoutine;
    private float gravedadAntesDeEstocada = -1f;

    [Header("Barrido (C)")]
    // Velocidad al arrancar el barrido.
    [SerializeField] private float dodgeSpeed = 10f;
    [SerializeField] private float dodgeDuration = 0.4f;
    // Cuanto frena hacia el final (0 = velocidad constante, 1 = acaba parado).
    // Arrancar rapido y frenar es lo que da la sensacion de peso de Dark Souls.
    [Range(0f, 1f)] [SerializeField] private float dodgeSlowdown = 0.5f;
    // Ventana de invulnerabilidad dentro del barrido. Es mas corta que el
    // barrido entero a proposito: esquivar pide acertar el momento, no solo
    // pulsar el boton cuando viene el golpe.
    [SerializeField] private float iFrameStart = 0.03f;
    [SerializeField] private float iFrameDuration = 0.25f;
    [SerializeField] private float dodgeCooldown = 0.45f;
    // Margen para pulsar la esquiva un poco antes de poder hacerla.
    [SerializeField] private float dodgeBufferTime = 0.15f;
    // Atravesar enemigos durante el barrido, para cambiar de lado.
    [SerializeField] private bool passThroughEnemies = true;
    // Si al acabar sigue metido dentro de un enemigo, cuanto se le deja seguir
    // atravesandolo antes de volver a chocar. Evita que la fisica lo expulse de
    // golpe o que el trigger de dano del enemigo le de al reactivar la colision.
    [SerializeField] private float passThroughMaxExtra = 0.4f;
    [SerializeField] private bool isDodging;
    [SerializeField] private bool hasIFrames;
    private float dodgeCooldownTimer;
    private float dodgeBuffer;
    private Coroutine dodgeRoutine;
    private Collider2D m_collider;
    private int capaPlayer = -1;
    private int capaEnemigos = -1;
    private bool colisionEnemigosOriginal;
    private bool ignorandoEnemigos;
    private readonly Collider2D[] solapes = new Collider2D[4];

    // Altura del collider mientras se desliza. El sprite de barrido mide la mitad
    // que de pie (15 px frente a 29), asi que el cuerpo tambien: es lo que deja
    // pasar por debajo de sierras, techos bajos o enemigos altos.
    [SerializeField] private float slideColliderHeight = 0.55f;
    // Si al acabar hay techo encima, sigue deslizandose hasta salir de debajo
    // (con este tope). Levantarse ahi dejaria el cuerpo metido en el techo.
    [SerializeField] private float slideMaxExtraUnderCeiling = 0.6f;
    private CapsuleCollider2D capsula;
    private Vector2 capsulaSize;
    private Vector2 capsulaOffset;

    //VIDA
    [Header("Health")]
    [SerializeField] private int maxHealth = 5;
    [SerializeField] private int currentHealth;
    [SerializeField] private float invincibleTime = 1f;
    // Cada cuánto parpadea el sprite mientras el player es invulnerable.
    [SerializeField] private float blinkInterval = 0.1f;
    [SerializeField] private HealthBar healthBar;

    private bool isInvincible;

    [Header("Curacion")]
    // Parpadeo verde al recoger vida. Va por color y no por encender/apagar el
    // sprite, asi que no choca con el parpadeo de invulnerabilidad.
    [SerializeField] private Color healFlashColor = new Color(0.4f, 1f, 0.4f, 1f);
    [SerializeField] private float healFlashDuration = 0.4f;
    // Cuantos destellos caben en esa duracion.
    [SerializeField] private int healFlashCount = 2;
    private Coroutine healFlashRoutine;

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
    // Alturas entre las que se reparten los rayos de pared, respecto al centro.
    // Con un solo rayo a la altura del pecho, en la punta de una plataforma el rayo
    // pasa por encima del borde y el muro no se detecta.
    [SerializeField] private float wallCheckTop = 0.3f;
    [SerializeField] private float wallCheckBottom = -0.45f;
    [SerializeField] private int wallCheckRays = 3;
    // Cuantos de esos rayos tienen que tocar para dar la pared por buena.
    // Con uno solo bastaba, y eso hacia que en la cresta de un muro se agarrara
    // tocando solo con los pies: el cuerpo quedaba por encima del muro y parecia
    // que se colgaba del aire. Pidiendo dos, el torso tiene que estar contra la
    // pared. Tambien descarta bloques de un solo tile.
    [SerializeField] private int wallCheckMinRays = 2;
    // Cuanto de vertical tiene que ser la superficie (1 = pared perfecta).
    // Sin esto se agarraba al canto de un suelo o a una rampa.
    [Range(0f, 1f)] [SerializeField] private float wallMinNormalX = 0.8f;
    // Ademas del minimo, el rayo mas bajo (el de las piernas) tiene que tocar.
    // Sin esto, bajo el canto inferior de una plataforma tocaban el rayo del
    // medio y el de arriba, y el personaje se agarraba con las piernas colgando
    // en el vacio.
    [SerializeField] private bool wallCheckRequireFeet = true;
    [SerializeField] private bool isWallDetected;
    // Lado en el que está la pared (1 derecha, -1 izquierda). Es independiente de hacia
    // dónde mira el player, que puede girarse para atacar sin soltar el muro.
    [SerializeField] private int wallDirection = 1;
    [SerializeField] private bool canWallSlide;
    // Verdadero solo cuando el player está realmente deslizándose: pegado a la pared,
    // en el aire y sin estar atacando. Es lo que lee el Animator.
    [SerializeField] private bool isWallSliding;
    // Velocidad maxima de bajada pegado al muro. Antes esto era un multiplicador
    // sobre la velocidad vertical, que daba una caida de 0.59 u/s (practicamente
    // pegado) o caida libre, sin nada en medio. Un tope es predecible: el numero
    // que pongas aqui es lo que baja.
    [SerializeField] private float wallSlideSpeed = 2.5f;
    // Tope con la direccion hacia abajo pulsada, para bajar a proposito.
    [SerializeField] private float wallSlideFastSpeed = 7f;
    [SerializeField] private Vector2 wallJumpForce;

    [Header("Correr por la pared")]
    [SerializeField] private bool wallRunEnabled = true;
    // Velocidad de subida. Sustituye a la gravedad mientras dura.
    [SerializeField] private float wallRunSpeed = 7f;
    // Tope de subida por pared. Sin el, el jugador treparia sin fin y se
    // saltaria niveles enteros. Se recarga al pisar suelo.
    [SerializeField] private float wallRunDuration = 0.7f;
    // Margen antes de poder volver a engancharse tras soltar. Va a 0 porque no
    // hace falta: wallRunTimer solo se recarga pisando suelo, asi que encadenar
    // subidas ya es imposible. Cualquier valor aqui solo mete pausas raras.
    [SerializeField] private float wallRunCooldown;
    // Con esto, llegar a una pared corriendo por el suelo engancha la subida
    // sin tener que saltar primero.
    [SerializeField] private bool wallRunFromGroundSprint = true;
    // Empuje contra el muro mientras sube, para que los rayos lo sigan viendo.
    [SerializeField] private float wallRunStick = 0.5f;
    // Margen de gracia cuando los rayos pierden la pared. Entre el capsule y el
    // alcance del rayo hay centesimas, y el cuerpo vibra contra el muro, asi que
    // un fotograma suelto sin deteccion es normal. Sin este respiro cada uno de
    // esos fotogramas cortaba la subida: eso eran los tirones.
    [SerializeField] private float wallRunGraceTime = 0.12f;
    // Cuanto se alarga el rayo para confirmar si la pared se ha acabado de
    // verdad o solo ha sido un fotograma de temblor. Es lo que separa los dos
    // casos, y sin ello coronar un muro dejaba al personaje corriendo en el aire.
    [SerializeField] private float wallRunProbeExtra = 0.15f;
    // Empujon horizontal al coronar el muro, hacia la cornisa. Sin el, Move()
    // pone la velocidad en X a cero en cuanto se pierde la pared y el personaje
    // sube en vertical y vuelve a bajar rozando la cara del muro sin llegar a
    // pisar arriba, en animacion de caida todo el rato.
    [SerializeField] private float wallRunLedgePush = 6f;
    // Que parte de la subida se conserva al coronar. Lo demas se cambia por
    // avance. Con 1 el personaje salia disparado en vertical y volvia a bajar
    // rozando la cara del muro sin llegar a pisar arriba: de ahi que se quedara
    // en animacion de caida. Con medio, el remate es un arco hacia delante.
    [Range(0f, 1f)] [SerializeField] private float wallRunLedgeUpKeep = 0.45f;
    // Cuanto dura ese empujon sin que el input pueda pisarlo. Se corta antes si
    // toca suelo, para devolver el control en cuanto aterriza.
    [SerializeField] private float wallRunLedgeTime = 0.25f;
    private float wallRunLedgeTimer;
    [SerializeField] private bool isWallRunning;
    private float wallRunTimer;
    private float wallRunCooldownTimer;
    private float wallRunGraceTimer;
    [SerializeField] private bool isWallJumping;
    [SerializeField] private float wallJumpDuration;
    // Margen tras despegarse del muro en el que el salto de pared sigue valiendo.
    // Encadenando paredes es facil pulsar un fotograma tarde y perder el salto;
    // con esto el juego perdona ese desfase sin que se note.
    [SerializeField] private float wallCoyoteTime = 0.12f;
    private float wallCoyoteTimer;
    // Si se marca, solo se desliza mientras se empuja hacia el muro. Evita
    // agarres sin querer al caer rozando una pared, a cambio de ser menos
    // indulgente. Va desactivado: cambia el tacto y ahora mismo funciona sin el.
    [SerializeField] private bool wallSlideNeedsInput;

    [Header("Polvo")]
    // Particulas en el punto de contacto mientras baja pegado al muro. Es lo
    // que hace que el deslizamiento se lea como friccion y no como flotar.
    [SerializeField] private ParticleSystem wallSlideDust;
    // Posicion respecto al centro del player. La X se orienta sola hacia el muro.
    // Se queda por dentro del borde del capsule (0.24) a proposito: justo en el
    // borde, media particula nace dentro del tile y el muro la tapa.
    [SerializeField] private Vector2 wallSlideDustOffset = new Vector2(0.18f, -0.4f);

    // Polvo de la carrera por el suelo, a la altura de los pies y por detras.
    [SerializeField] private ParticleSystem sprintDust;
    // X negativa = detras del personaje. Se voltea sola al girar.
    [SerializeField] private Vector2 sprintDustOffset = new Vector2(-0.22f, -0.45f);

    // Polvo de la carrera vertical. Sale hacia abajo, que es de donde viene.
    [SerializeField] private ParticleSystem wallRunDust;
    [SerializeField] private Vector2 wallRunDustOffset = new Vector2(0.18f, -0.35f);

    //IMPULSO EXTERNO (flechas giratorias, trampolines...)
    [Header("Opciones de Impulso")]
    [SerializeField] private bool isLaunched;
    // Si está marcado, salir impulsado devuelve los saltos extra.
    [SerializeField] private bool launchRestoresJumps = true;
    private Coroutine launchRoutine;

    [Header("Knock settings")]
    [SerializeField] private bool isKnocked;
    // Si el golpe llega en el aire, en vez del empujon corto el player cae
    // derribado y tiene que levantarse.
    [SerializeField] private bool knockDownInAir = true;
    [SerializeField] private bool isKnockedDown;
    // Tiempo minimo en el aire antes de empezar a mirar si ya toco el suelo.
    [SerializeField] private float knockDownMinAirTime = 0.15f;
    // Tope de duracion del derribo. Red de seguridad: pase lo que pase, el player
    // se levanta y recupera el control. Sin esto, cualquier fallo de deteccion
    // deja la partida bloqueada sin salida.
    [SerializeField] private float knockDownMaxTime = 2.5f;
    // Lo que tarda en levantarse. Debe parecerse al largo del clip de levantarse.
    [SerializeField] private float getUpDuration = 0.7f;
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
    // Respaldo: solo se usa si un perfil se deja con duracion 0. Lo normal es
    // que mande la duracion del propio golpe, que es lo que dura su clip.
    [SerializeField] private float attackCooldown = 0.4f;
    private float attackAnimationTimer;
    [SerializeField] private bool isAttacking;
    private bool canAttack = true;
    // Se guarda para poder cortarla cuando un golpe encadena con el siguiente:
    // si no, la corrutina del golpe viejo apagaria isAttacking a media animacion.
    private Coroutine attackRoutine;

    [Header("Impacto")]
    // Congelacion al conectar un golpe. Es el truco que mas sensacion de fuerza
    // da por lo poco que cuesta. A 0 queda desactivado.
    [SerializeField] private float hitStopDuration = 0.06f;
    [Range(0f, 1f)] [SerializeField] private float hitStopScale = 0.05f;
    // Cuanto frena el avance del golpe. Sin esto el personaje derraparia.
    [SerializeField] private float attackDrag = 14f;
    private bool enHitStop;

    // Fuente de sacudida de camara. Si se deja vacia se busca en el propio
    // player al arrancar; sin ella los golpes simplemente no sacuden.
    [SerializeField] private CinemachineImpulseSource impulseSource;
    // Cuanto vive el efecto de corte antes de borrarse solo.
    [SerializeField] private float efectoDuracion = 0.5f;

    [Header("Combo")]
    // Cuantos golpes encadena. Debe coincidir con el numero de estados de ataque
    // del Animator y con el de perfiles de la lista de arriba.
    [SerializeField] private int comboLength = 3;
    // Margen para encadenar el siguiente golpe. Empieza a contar cuando termina el
    // anterior, no cuando empieza. Si se agota, la cadena vuelve al primer golpe.
    [SerializeField] private float comboWindow = 0.6f;
    // Golpe que sale al atacar pegado a una pared: 0 el primero, 1 el segundo,
    // 2 el tercero. Ahi no se encadena combo, siempre sale este.
    [SerializeField] private int wallAttackProfileIndex;
    // Golpe que saldra la proxima vez que ataques. Visible para depurar.
    [SerializeField] private int comboIndex;
    private float comboTimer;
    // Cuanto se recuerda una pulsacion que llega durante el tiempo de recarga.
    // Sin esto, al hacer clic rapido los golpes se pierden y la cadena se reinicia.
    [SerializeField] private float attackBufferTime = 0.25f;
    private float attackBuffer;

    #region Unity Lifecycle

    // Obtiene los componentes principales del player y define su estado inicial (respawn/checkpoint).
    private void Awake()
    {
        m_gatherInput = GetComponent<GatherInput>();
        m_transform = GetComponent<Transform>();
        m_rigitbody2D = GetComponent<Rigidbody2D>();
        m_animator = GetComponent<Animator>();
        m_spriteRenderer = GetComponent<SpriteRenderer>();
        m_collider = GetComponent<Collider2D>();
        capsula = GetComponent<CapsuleCollider2D>();
        if (capsula != null)
        {
            capsulaSize = capsula.size;
            capsulaOffset = capsula.offset;
        }
        capaPlayer = gameObject.layer;
        capaEnemigos = LayerMask.NameToLayer("Enemies");
        if (capaEnemigos >= 0)
            colisionEnemigosOriginal = Physics2D.GetIgnoreLayerCollision(capaPlayer, capaEnemigos);
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
        idKnockDown = Animator.StringToHash("knockDown");
        idIsSprinting = Animator.StringToHash("isSprinting");
        idIsWallRunning = Animator.StringToHash("isWallRunning");
        idIsDodging = Animator.StringToHash("isDodging");
        currentSpeed = speed;
        if (impulseSource == null) impulseSource = GetComponent<CinemachineImpulseSource>();
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
        ActualizarCombo();
    }

    // Actualiza físicas, colisiones, movimiento y salto en el paso de física.
    void FixedUpdate()
    {
        // La deteccion corre SIEMPRE, incluso sin control. Si no, durante la
        // aparicion por la puerta o un derribo el Animator recibe isGrounded en
        // falso y mete un fotograma de caida que no corresponde.
        CheckCollision();

        if (!canMove) return;
        if (isKnocked) return;

        // La estocada lleva su propia velocidad de principio a fin.
        if (isPlunging) return;

        // Durante el barrido no hay ni andar, ni saltar, ni atacar: se esta
        // comprometido con el, como en los Souls. Lo que se pulse mientras
        // tanto queda guardado y sale al terminar.
        Dodge();
        if (isDodging) return;

        Move();
        Jump();
        Attack();
    }

    // Dibuja en el editor la línea de detección de pared y el radio de ataque para depurar.
    private void OnDrawGizmos()
    {
        // Rayos de deteccion de pared, a las dos alturas y hacia los dos lados.
        Gizmos.color = Color.cyan;
        int rayos = Mathf.Max(1, wallCheckRays);
        for (int i = 0; i < rayos; i++)
        {
            Vector3 origen = OrigenDelRayoDePared(i, rayos);
            Gizmos.DrawLine(origen, origen + Vector3.right * checkWallDistance);
            Gizmos.DrawLine(origen, origen + Vector3.left * checkWallDistance);
        }

        // Rayo de los pies, el que decide cuando termina la subida por la pared.
        Gizmos.color = Color.magenta;
        Vector3 pies = OrigenDeLosPies();
        Gizmos.DrawLine(pies, pies + Vector3.right * checkWallDistance);
        Gizmos.DrawLine(pies, pies + Vector3.left * checkWallDistance);

        // Areas de la estocada: naranja la de caida, amarilla la del impacto.
        if (showPlungeGizmos && m_transform != null)
        {
            Vector3 p = m_transform.position;
            Gizmos.color = new Color(1f, 0.5f, 0f);
            Gizmos.DrawWireCube(p + (Vector3)plungeHitOffset, plungeHitSize);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(p + (Vector3)plungeLandOffset, plungeLandSize);
        }

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
        // Cuando el movimiento no lo manda el input (muro, impulso, ataque) la
        // carrera se apaga: si no, al recuperar el control el personaje saldria
        // disparado a velocidad de sprint sin que el jugador lo haya pedido.
        if (!canMove || (isWallDetected && !isGrounded) || isWallJumping || isLaunched || isAttacking
            || isWallRunning || wallRunLedgeTimer > 0f)
        {
            isSprinting = false;
            currentSpeed = speed;

            // El empujon del golpe se frena solo, para que no derrape.
            if (isAttacking && isGrounded)
            {
                float vx = Mathf.MoveTowards(m_rigitbody2D.linearVelocityX, 0f, attackDrag * Time.fixedDeltaTime);
                m_rigitbody2D.linearVelocity = new Vector2(vx, m_rigitbody2D.linearVelocityY);
            }
            return;
        }

        ActualizarCarrera();

        Flip();
        m_rigitbody2D.linearVelocity = new Vector2(currentSpeed * m_gatherInput.Value.x, m_rigitbody2D.linearVelocityY);
    }

    // Decide si el personaje esta corriendo y lleva la velocidad hasta la que toca.
    private void ActualizarCarrera()
    {
        // Correr solo cuenta si ademas se esta pidiendo movimiento: con el boton
        // pulsado y el personaje quieto, no arranca solo.
        bool hayInput = Mathf.Abs(m_gatherInput.Value.x) > 0.1f;
        bool pideCorrer = m_gatherInput.IsSprinting && hayInput;

        if (isGrounded)
        {
            // Con los pies en el suelo manda el boton, sin mas.
            isSprinting = pideCorrer;
        }
        else
        {
            // En el aire la carrera NO se puede arrancar: pulsar el boton a media
            // trayectoria no debe alargar un salto que salio andando. Solo se
            // conserva la carrera que ya se traia al despegar.
            isSprinting = isSprinting && hayInput && (keepSprintInAir || pideCorrer);
        }

        // La rampa evita el tiron de pasar de golpe de una velocidad a otra.
        float objetivo = isSprinting ? sprintSpeed : speed;
        currentSpeed = sprintAcceleration <= 0f
            ? objetivo
            : Mathf.MoveTowards(currentSpeed, objetivo, sprintAcceleration * Time.fixedDeltaTime);
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
            else if (isWallDetected || wallCoyoteTimer > 0f)
            {
                // Saltar siempre corta la subida, aunque se siga pulsando Shift.
                // Sin cortar el impulso: WallJump fija su propia velocidad justo
                // despues y tocarla aqui solo restaria altura al salto.
                TerminarWallRun(false);
                WallJump();
            }
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

        // Se gasta el margen: si no, un solo despegue daria varios saltos de pared.
        wallCoyoteTimer = 0f;
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
        // Recogemos la pulsacion aunque ahora mismo no se pueda atacar, y la
        // guardamos un momento. Asi un clic que cae durante la recarga no se tira:
        // se ejecuta en cuanto el personaje vuelve a estar listo.
        if (m_gatherInput.IsAttacking) attackBuffer = attackBufferTime;
        m_gatherInput.IsAttacking = false;

        if (attackBuffer > 0f && canAttack)
        {
            attackBuffer = 0f;

            // S + ataque en el aire: estocada hacia abajo. No necesita los golpes
            // aereos anteriores, sale directa.
            if (!isGrounded && !canWallSlide && !isWallRunning && m_gatherInput.Value.y < -0.5f)
            {
                IniciarEstocada();
                return;
            }

            bool desdePared = canWallSlide;
            bool enAire = !isGrounded && !desdePared;
            int golpe;

            if (desdePared)
            {
                // Pegado a una pared el golpe sale hacia fuera: atacar al muro no
                // tiene sentido y el espadazo queda oculto dentro del tile.
                SetFacing(-wallDirection);

                // Y aqui no se encadena combo: siempre sale el mismo golpe, el que
                // elijas en el Inspector. Encadenar colgado de un muro daba saltos
                // de animacion raros al hacer clic varias veces seguidas.
                golpe = Mathf.Clamp(wallAttackProfileIndex, 0, Mathf.Max(0, comboLength - 1));
                comboIndex = 0;
            }
            else if (enAire)
            {
                // Los golpes aereos van por su propia cuenta. Se gastan en cada salto
                // y se recargan al pisar suelo o al acertar a un enemigo: si no,
                // con el frenado de caida se podria flotar a base de clics.
                if (airComboIndex >= airComboLength) return;

                golpe = airAttackFirstProfile + airComboIndex;
                airComboIndex++;
                comboIndex = 0;
                atacandoEnAire = true;

                if (m_rigitbody2D.linearVelocityY < 0f)
                    m_rigitbody2D.linearVelocity = new Vector2(m_rigitbody2D.linearVelocityX, m_rigitbody2D.linearVelocityY * airAttackHang);
            }
            else
            {
                golpe = comboIndex;
                // Deja preparado el siguiente golpe de la cadena. El margen para
                // encadenarlo no arranca aqui, sino al terminar este ataque.
                comboIndex = (comboIndex + 1) % Mathf.Max(1, comboLength);
            }

            // El Animator elige que ataque reproducir segun este numero.
            m_animator.SetInteger(IdComboIndex, golpe);
            m_animator.SetTrigger(IdAttack);

            // Cada golpe manda sobre su propio tiempo: mientras dura, ni se puede
            // lanzar otro ni el deslizamiento de pared puede pisar la animacion.
            // Eso es lo que impide que a base de clics salgan medios espadazos.
            AttackProfile perfil = PerfilDeAtaque(golpe);
            float duracion = (perfil != null && perfil.duracion > 0f) ? perfil.duracion : attackCooldown;
            float encadenar = (perfil != null) ? perfil.encadenarDesde : duracion;

            attackAnimationTimer = duracion;
            comboTimer = 0f;

            // Un pasito adelante al golpear. Solo en suelo: en el aire estropearia
            // la trayectoria del salto, y en la pared despegaria al personaje.
            if (isGrounded && !desdePared && perfil != null && perfil.avance != 0f)
                m_rigitbody2D.linearVelocity = new Vector2(direction * perfil.avance, m_rigitbody2D.linearVelocityY);

            if (attackRoutine != null) StopCoroutine(attackRoutine);
            attackRoutine = StartCoroutine(AttackRoutine(desdePared, duracion, encadenar));
        }
    }

    // Bloquea el movimiento y los ataques nuevos mientras dura la animación de ataque.
    private IEnumerator AttackRoutine(bool volverAMirarLaPared, float duracion, float encadenarDesde)
    {
        canAttack = false;
        isAttacking = true;

        // Primer tramo: la animacion es intocable. Aqui un clic no lanza otro
        // golpe, se queda guardado en el buffer.
        float ventana = Mathf.Clamp(encadenarDesde, 0f, duracion);
        yield return new WaitForSeconds(ventana);

        // Segundo tramo: los fotogramas de recuperacion. Si hay un clic guardado,
        // el siguiente golpe entra ya y se los come. Si no, la animacion termina.
        canAttack = true;
        yield return new WaitForSeconds(duracion - ventana);

        isAttacking = false;
        atacandoEnAire = false;

        // Ahora si empieza a correr el margen para encadenar el siguiente golpe.
        comboTimer = comboWindow;

        if (!volverAMirarLaPared) yield break;

        // Si sigue colgado del muro, vuelve a mirarlo para que el deslizamiento
        // no se quede reproduciéndose del revés. Pero NO antes de que acabe la
        // animación: el cooldown puede ser de centésimas, y girar aquí mismo hacía
        // que el espadazo se viera contra el muro aunque hubiera salido hacia fuera.
        while (attackAnimationTimer > 0f) yield return null;

        if (canWallSlide && !isGrounded) SetFacing(wallDirection);
    }

    // Golpea todo lo que haya en una caja relativa al centro del player. Lo usa la
    // estocada, que no tiene sentido atarla a los perfiles de ataque (esos van
    // relativos al AttackPoint, que esta delante del personaje).
    private int GolpearCaja(Vector2 offset, Vector2 tamano, int dano, float retroceso, HashSet<EnemyHealth> excluir = null)
    {
        Vector2 centro = (Vector2)m_transform.position + offset;
        int capas = attackLayers.value == 0 ? ~0 : attackLayers.value;
        Collider2D[] hits = Physics2D.OverlapBoxAll(centro, tamano, 0f, capas);
        HashSet<EnemyHealth> tocados = new HashSet<EnemyHealth>();

        foreach (Collider2D hit in hits)
        {
            if (!hit.CompareTag("Enemy")) continue;
            EnemyHealth enemigo = hit.GetComponent<EnemyHealth>();
            if (enemigo == null || (excluir != null && excluir.Contains(enemigo))) continue;
            if (tocados.Add(enemigo))
                enemigo.TakeDamage(dano, m_transform.position, retroceso);
        }

        if (tocados.Count > 0) StartCoroutine(HitStop(plungeHitStop));
        return tocados.Count;
    }

    // Lanza por los aires a los enemigos que la caja de caida va atravesando. Cada
    // uno solo una vez por estocada: siguen dentro de la caja varios fotogramas.
    private void LanzarEnemigosAtravesados(HashSet<EnemyHealth> yaLanzados)
    {
        Vector2 centro = (Vector2)m_transform.position + plungeHitOffset;
        int capas = attackLayers.value == 0 ? ~0 : attackLayers.value;
        Collider2D[] hits = Physics2D.OverlapBoxAll(centro, plungeHitSize, 0f, capas);
        bool alguno = false;

        foreach (Collider2D hit in hits)
        {
            if (!hit.CompareTag("Enemy")) continue;
            EnemyHealth enemigo = hit.GetComponent<EnemyHealth>();
            if (enemigo == null || !yaLanzados.Add(enemigo)) continue;

            // Sale hacia el lado en el que ya estaba. Si cae justo encima, hacia
            // donde mira el player.
            float dx = enemigo.transform.position.x - m_transform.position.x;
            float lado = Mathf.Abs(dx) > 0.05f ? Mathf.Sign(dx) : direction;
            enemigo.TakeDamage(plungeDamage, m_transform.position, new Vector2(plungeLaunch.x * lado, plungeLaunch.y));
            alguno = true;
        }

        if (!alguno) return;
        StartCoroutine(HitStop(plungeHitStop));
        Sacudir(plungeLandShake * 0.5f);
    }

    // Devuelve el salto extra y los golpes aereos. Lo usa acertar en el aire.
    private void RecuperarSaltoAereo()
    {
        counterExtraJumps = extraJumps;
        canDoubleJump = true;
        airComboIndex = 0;
    }

    private void IniciarEstocada()
    {
        if (isPlunging) return;
        if (isAttacking) CancelarAtaque();
        plungeRoutine = StartCoroutine(EstocadaRoutine());
    }

    // Suspension, caida en picado y, o rebote en un enemigo, o impacto en el suelo.
    private IEnumerator EstocadaRoutine()
    {
        isPlunging = true;
        isAttacking = true;
        canAttack = false;
        m_animator.SetInteger(IdPlungePhase, 1);
        m_animator.SetTrigger(IdPlunge);

        // Suspension: se queda quieto en el aire un instante.
        gravedadAntesDeEstocada = m_rigitbody2D.gravityScale;
        m_rigitbody2D.gravityScale = 0f;
        float t = 0f;
        while (t < plungeWindup)
        {
            m_rigitbody2D.linearVelocity = Vector2.zero;
            yield return new WaitForFixedUpdate();
            t += Time.fixedDeltaTime;
        }
        m_rigitbody2D.gravityScale = gravedadAntesDeEstocada;
        gravedadAntesDeEstocada = -1f;

        // Caida en picado, recta. Se atraviesa a los enemigos: la colision con
        // ellos se desactiva hasta que acaba y ha salido de dentro de todos.
        IgnorarEnemigos(true);
        HashSet<EnemyHealth> atravesados = new HashSet<EnemyHealth>();
        t = 0f;
        while (!isGrounded && t < plungeMaxTime)
        {
            m_rigitbody2D.linearVelocity = new Vector2(0f, -plungeSpeed);

            // Si cae encima de un enemigo no rebota: lo atraviesa y lo lanza por
            // los aires, y sigue cayendo hasta el suelo.
            LanzarEnemigosAtravesados(atravesados);

            yield return new WaitForFixedUpdate();
            t += Time.fixedDeltaTime;
        }

        if (isGrounded)
        {
            // Impacto: dano en area alrededor de los pies y sacudida fuerte.
            m_rigitbody2D.linearVelocity = Vector2.zero;
            m_animator.SetInteger(IdPlungePhase, 2);
            // Los que ya salieron volando al atravesarlos no reciben otro golpe.
            GolpearCaja(plungeLandOffset, plungeLandSize, plungeDamage, plungeKnockback, atravesados);
            Sacudir(plungeLandShake);
            yield return new WaitForSeconds(plungeLandTime);
        }

        FinalizarEstocada();
    }

    // Deja el estado limpio al acabar la estocada por su camino normal.
    private void FinalizarEstocada()
    {
        if (gravedadAntesDeEstocada >= 0f)
        {
            m_rigitbody2D.gravityScale = gravedadAntesDeEstocada;
            gravedadAntesDeEstocada = -1f;
        }

        plungeRoutine = null;
        isPlunging = false;

        // Vuelve a chocar con los enemigos, pero solo cuando ya no este dentro
        // de ninguno: reactivarlo dentro lo expulsaria de golpe.
        if (ignorandoEnemigos && isActiveAndEnabled) StartCoroutine(EsperarASalirDeEnemigos());
        isAttacking = false;
        canAttack = true;
        m_animator.SetInteger(IdPlungePhase, 0);
        m_animator.ResetTrigger(IdPlunge);
    }

    // La corta desde fuera, por ejemplo al recibir un golpe. Importante devolver
    // la gravedad: si el golpe llega durante la suspension, se quedaria a cero y
    // el personaje flotaria.
    private void TerminarEstocada()
    {
        if (plungeRoutine != null) StopCoroutine(plungeRoutine);
        FinalizarEstocada();
    }

    // Deshace la cadena si el jugador tarda demasiado en volver a atacar.
    private void ActualizarCombo()
    {
        // La pulsacion guardada caduca: no queremos que un clic de hace un segundo
        // dispare un golpe cuando el jugador ya se ha puesto a correr.
        if (attackBuffer > 0f) attackBuffer -= Time.deltaTime;

        // Ventana en la que la animacion de ataque manda sobre el deslizamiento de pared.
        if (attackAnimationTimer > 0f) attackAnimationTimer -= Time.deltaTime;

        if (comboIndex == 0 || comboTimer <= 0f) return;

        comboTimer -= Time.deltaTime;
        if (comboTimer <= 0f) comboIndex = 0;
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
    private int AplicarGolpe(int index)
    {
        AttackProfile perfil = PerfilDeAtaque(index);
        if (perfil == null) return 0;

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

        // Todo lo de abajo solo si el golpe ha conectado: al aire no aporta nada
        // y sacudir la camara por fallar marea.
        if (alreadyHit.Count == 0) return 0;

        // En el aire, acertar devuelve el salto extra.
        if (!isGrounded && airHitRestoresJump) RecuperarSaltoAereo();

        float congelacion = perfil.congelacion >= 0f ? perfil.congelacion : hitStopDuration;
        StartCoroutine(HitStop(congelacion));

        Sacudir(perfil.sacudida);
        MostrarEfecto(perfil, centro);
        return alreadyHit.Count;
    }

    // Sacude la camara a traves de Cinemachine. La direccion sale del propio
    // golpe para que el tirón vaya en el sentido del espadazo.
    private void Sacudir(float fuerza)
    {
        if (impulseSource == null || fuerza <= 0f) return;

        impulseSource.GenerateImpulseWithVelocity(new Vector3(direction * fuerza, -fuerza * 0.5f, 0f));
    }

    // Coloca el efecto de corte en el punto del golpe, girado hacia donde mira
    // el personaje y con el angulo del perfil si es un arco.
    private void MostrarEfecto(AttackProfile perfil, Vector2 centro)
    {
        if (perfil.efectoGolpe == null) return;

        float angulo = perfil.anguloCentro * direction;
        GameObject efecto = Instantiate(perfil.efectoGolpe, centro, Quaternion.Euler(0f, 0f, angulo));

        // Se voltea con el personaje para que el corte no salga del reves.
        Vector3 escala = efecto.transform.localScale;
        escala.x = Mathf.Abs(escala.x) * direction;
        efecto.transform.localScale = escala;

        Destroy(efecto, efectoDuracion);
    }

    // Congela un instante el juego al conectar. Es lo que hace que un golpe se
    // sienta solido en vez de atravesar al enemigo como si nada.
    private IEnumerator HitStop(float duracion)
    {
        // Si el juego ya esta parado (pausa, muerte...) no se toca nada: al
        // restaurar pondriamos el tiempo en marcha en mitad de una pausa.
        if (duracion <= 0f || enHitStop || Time.timeScale < 0.99f) yield break;

        enHitStop = true;
        float escalaPrevia = Time.timeScale;
        Time.timeScale = hitStopScale;

        // En tiempo real: con el timeScale bajado, una espera normal duraria
        // lo que no es.
        yield return new WaitForSecondsRealtime(duracion);

        Time.timeScale = escalaPrevia;
        enHitStop = false;
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
        HandleWallRun();
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
            airComboIndex = 0;

            // Un golpe aereo no sigue en el suelo: se corta al aterrizar para que
            // el personaje no se quede clavado terminando un espadazo de salto.
            if (atacandoEnAire) CancelarAtaque();
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
        bool paredDelante = HayParedHacia(direction);
        bool paredDetras = HayParedHacia(-direction);

        isWallDetected = paredDelante || paredDetras;

        // Si hay muro a los dos lados manda el de delante.
        if (paredDelante) wallDirection = direction;
        else if (paredDetras) wallDirection = -direction;

        if (isWallDetected && !isGrounded)
        {
            counterExtraJumps = extraJumps;
            canDoubleJump = true;
        }

        // Margen de coyote. Pisar suelo lo anula: ahi manda el salto normal y no
        // queremos saltos de pared saliendo del suelo.
        if (isGrounded) wallCoyoteTimer = 0f;
        else if (isWallDetected) wallCoyoteTimer = wallCoyoteTime;
        else if (wallCoyoteTimer > 0f) wallCoyoteTimer -= Time.fixedDeltaTime;
    }

    // Lanza varios rayos repartidos en altura hacia un lado. Basta con que uno
    // toque para dar la pared por detectada: asi funciona tambien en las esquinas,
    // donde solo las piernas del player quedan junto al muro.
    private bool HayParedHacia(int lado)
    {
        return HayParedHacia(lado, checkWallDistance);
    }

    // Con alcance explicito: la carrera por la pared lo usa mas largo para
    // distinguir un fallo de deteccion de haberse acabado el muro.
    private bool HayParedHacia(int lado, float alcance)
    {
        return HayParedHacia(lado, alcance, wallCheckMinRays);
    }

    // Con el minimo de rayos tambien explicito. Agarrarse a una pared y seguir
    // subiendo por ella no piden lo mismo: para agarrarse hace falta el torso
    // contra el muro, pero para seguir subiendo basta con que los pies tengan
    // pared debajo.
    private bool HayParedHacia(int lado, float alcance, int minimoRayos)
    {
        int rayos = Mathf.Max(1, wallCheckRays);

        int minimo = Mathf.Clamp(minimoRayos, 1, rayos);
        int tocados = 0;

        for (int i = 0; i < rayos; i++)
        {
            Vector2 origen = OrigenDelRayoDePared(i, rayos);
            RaycastHit2D hit = Physics2D.Raycast(origen, Vector2.right * lado, alcance, groundLayer);

            // Solo cuentan superficies verticales de verdad. El canto de un suelo
            // o una rampa devuelven una normal casi vertical y quedan descartados.
            bool valido = hit && Mathf.Abs(hit.normal.x) >= wallMinNormalX;

            // El rayo 0 es el de las piernas. Si es obligatorio y falla, no hay
            // pared que valga, toquen los que toquen por encima.
            if (i == 0 && wallCheckRequireFeet && !valido) return false;
            if (!valido) continue;

            tocados++;
            if (tocados >= minimo) return true;
        }

        return false;
    }

    // Un rayo a ras del fondo del capsule. El rayo mas bajo de los normales va a
    // -0.45 del centro, pero los pies estan a -0.62: cuando ese rayo deja de ver
    // la pared, el cuerpo aun sobresale 0.17 por debajo del borde, y el empujon
    // de coronar estrellaba esa esquina contra el labio del muro. Con este rayo
    // la subida sigue hasta que los pies de verdad estan por encima.
    private bool PiesContraPared(int lado, float alcance)
    {
        Vector2 origen = OrigenDeLosPies();
        RaycastHit2D hit = Physics2D.Raycast(origen, Vector2.right * lado, alcance, groundLayer);
        return hit && Mathf.Abs(hit.normal.x) >= wallMinNormalX;
    }

    private Vector2 OrigenDeLosPies()
    {
        // Un pelin por encima del fondo, para que el rayo no roce el suelo. Se usa
        // la forma original del capsule, no la encogida del barrido. En el editor
        // (sin Awake) se lee del componente, para que el gizmo salga bien.
        float pies;
        if (capsula != null) pies = capsulaOffset.y - capsulaSize.y * 0.5f;
        else
        {
            CapsuleCollider2D c = GetComponent<CapsuleCollider2D>();
            pies = c != null ? c.offset.y - c.size.y * 0.5f : wallCheckBottom;
        }
        pies += 0.05f;
        return (Vector2)m_transform.position + new Vector2(0f, pies);
    }

    private Vector2 OrigenDelRayoDePared(int indice, int total)
    {
        float t = total <= 1 ? 0.5f : indice / (float)(total - 1);
        return (Vector2)m_transform.position + new Vector2(0f, Mathf.Lerp(wallCheckBottom, wallCheckTop, t));
    }

    // Carrera vertical por la pared. Se engancha con el boton de correr y dura
    // mientras quede tiempo en el contador, que se recarga al pisar suelo.
    private void HandleWallRun()
    {
        if (wallRunCooldownTimer > 0f) wallRunCooldownTimer -= Time.fixedDeltaTime;
        // El bloqueo del remate se cae solo al pisar suelo: a partir de ahi el
        // jugador tiene que poder seguir corriendo con normalidad.
        if (wallRunLedgeTimer > 0f)
            wallRunLedgeTimer = isGrounded ? 0f : wallRunLedgeTimer - Time.fixedDeltaTime;

        // Tocar suelo devuelve la subida entera. Es lo que impide encadenar
        // paredes para subir sin limite.
        if (isGrounded && !isWallRunning) wallRunTimer = wallRunDuration;

        if (!wallRunEnabled)
        {
            isWallRunning = false;
            return;
        }

        // Para ENGANCHAR la subida vale la deteccion normal, la de dos rayos. Pero
        // para SEGUIRLA basta con que toque el rayo mas bajo: si los pies aun
        // tienen pared, hay pared que correr. Exigiendo dos, la subida se cortaba
        // con los pies medio tile por debajo del borde, sin llegar a coronar.
        bool paredParaSubir = isWallRunning
            ? PiesContraPared(wallDirection, checkWallDistance) || HayParedHacia(wallDirection, checkWallDistance, 1)
            : isWallDetected;

        // Los rayos pierden la pared con facilidad: entre el borde del capsule y
        // su alcance hay centesimas. Mientras sube, se le conceden unos
        // fotogramas de margen antes de dar la pared por perdida.
        if (paredParaSubir)
        {
            wallRunGraceTimer = wallRunGraceTime;
        }
        else if (wallRunGraceTimer > 0f)
        {
            // Pero ese margen es solo para el temblor del cuerpo contra el muro.
            // Se comprueba con un rayo mas largo: si ni asi hay pared, es que se
            // ha acabado (ha coronado el borde) y la salida es inmediata, sin
            // quedarse corriendo en el aire.
            bool paredCerca = PiesContraPared(wallDirection, checkWallDistance + wallRunProbeExtra)
                              || HayParedHacia(wallDirection, checkWallDistance + wallRunProbeExtra, 1);
            wallRunGraceTimer = paredCerca ? wallRunGraceTimer - Time.fixedDeltaTime : 0f;
        }

        bool hayPared = paredParaSubir || (isWallRunning && wallRunGraceTimer > 0f);

        // Subir pide el boton de correr MAS la direccion hacia el muro. Con solo
        // el boton, el personaje podia estar rozando la pared sin empujar contra
        // ella y la deteccion iba y venia entre fotogramas. Exigiendo la tecla de
        // direccion, el cuerpo se mantiene apoyado y la subida es fiable.
        bool empujaAlMuro = m_gatherInput.Value.x * wallDirection > 0f;

        // Desde el suelo, ademas, no basta con detectar el muro: los rayos lo ven
        // un poco antes de tocarlo, y enganchar ahi lanzaba al personaje hacia
        // arriba en plena carrera. Se pide que la pared le haya frenado de verdad,
        // que es lo que significa haber chocado con ella.
        bool frenadoPorElMuro = Mathf.Abs(m_rigitbody2D.linearVelocityX) < 0.5f;
        bool puedeArrancarDesdeSuelo = wallRunFromGroundSprint && isGrounded
                                       && isSprinting && frenadoPorElMuro;

        bool condiciones = hayPared
                           && empujaAlMuro
                           && canMove
                           && !isKnocked
                           && !isLaunched
                           && !isAttacking
                           && wallRunTimer > 0f
                           && wallRunCooldownTimer <= 0f
                           && wallRunLedgeTimer <= 0f
                           && (!isGrounded || puedeArrancarDesdeSuelo);

        // Shift + la direccion hacia el muro. Ni una cosa ni la otra por separado.
        if (!m_gatherInput.IsSprinting || !condiciones)
        {
            // Si la pared sigue ahi, lo que ha pasado es que ha soltado el boton
            // o se ha agotado el tiempo: se corta el impulso para que no siga
            // subiendo solo. Si la pared se ha acabado, se conserva, y asi la
            // subida remata coronando el borde en vez de frenar en seco.
            TerminarWallRun(hayPared);
            return;
        }

        isWallRunning = true;
        wallRunTimer -= Time.fixedDeltaTime;

        // Mirando al muro, como en el deslizamiento: asi la animacion de subir
        // y la de deslizarse encajan cuando se pasa de una a otra.
        SetFacing(wallDirection);

        // La X empuja contra la pared para que los rayos no la pierdan; la Y se
        // fija a pelo, que es lo que anula la gravedad mientras sube.
        m_rigitbody2D.linearVelocity = new Vector2(wallDirection * wallRunStick, wallRunSpeed);

        // Subir por la pared tambien devuelve el doble salto, igual que el
        // deslizamiento: si no, rematar la subida con un salto seria imposible.
        canDoubleJump = true;
    }

    // Corta la subida. Si sigue en el aire y pegado al muro, HandleWallSlide
    // toma el relevo en el mismo paso de fisica y entra el deslizamiento.
    private void TerminarWallRun(bool cortarImpulso)
    {
        if (!isWallRunning) return;

        isWallRunning = false;
        wallRunCooldownTimer = wallRunCooldown;
        wallRunGraceTimer = 0f;

        // Corta el impulso hacia arriba para que la caida empiece ya. Sin esto
        // el personaje seguiria subiendo por inercia con la animacion de
        // deslizamiento puesta, que queda fatal.
        if (cortarImpulso)
        {
            if (m_rigitbody2D.linearVelocityY > 0f)
                m_rigitbody2D.linearVelocity = new Vector2(m_rigitbody2D.linearVelocityX, 0f);

            return;
        }

        // Aqui la pared se ha acabado: el personaje esta coronando. Se le empuja
        // hacia la cornisa para que aterrice encima en vez de quedarse subiendo
        // en vertical pegado a la cara del muro. El temporizador bloquea a Move()
        // esos fotogramas: si no, sin tocar ninguna tecla la X se pondria a cero
        // al instante y el empujon no serviria de nada.
        if (wallRunLedgePush <= 0f) return;

        // Parte de la velocidad de subida se cambia por avance, asi que en vez de
        // frenar en el borde el personaje sale despedido por encima y aterriza
        // corriendo. Es el mismo trato que el arranque desde el suelo, al reves.
        float subidaRestante = Mathf.Max(0f, m_rigitbody2D.linearVelocityY) * wallRunLedgeUpKeep;
        m_rigitbody2D.linearVelocity = new Vector2(wallDirection * wallRunLedgePush, subidaRestante);
        wallRunLedgeTimer = wallRunLedgeTime;

        // Y se gasta la subida entera. Es lo que impide el bucle del borde: el
        // empujon acerca de nuevo al muro, los rayos vuelven a tocarlo y, con
        // tiempo restante, la carrera se reenganchaba y volvia a lanzar al
        // personaje hacia arriba. Asi se corona una vez y hay que pisar suelo
        // para volver a tener subida, que es como ya funcionaba el contador.
        wallRunTimer = 0f;

        // Y se mantiene la carrera puesta: al aterrizar sigue corriendo en lugar
        // de caer a Idle y tener que arrancar otra vez.
        isSprinting = true;
        currentSpeed = Mathf.Max(currentSpeed, sprintSpeed);
    }

    // Enciende y apaga los tres sistemas de polvo segun lo que este haciendo
    // el personaje, y los coloca donde toca.
    private void ActualizarPolvo()
    {
        // Roce de pared: solo bajando de verdad. Quieto contra el muro no hay
        // friccion que mostrar.
        // La posicion es local y el player voltea su escala en X al girar. Con
        // wallDirection * direction el offset apunta al muro tanto si mira hacia
        // el como si acaba de atacar y mira al lado contrario.
        ColocarPolvo(wallSlideDust,
            isWallSliding && m_rigitbody2D.linearVelocityY < -0.1f,
            new Vector3(wallSlideDustOffset.x * wallDirection * direction, wallSlideDustOffset.y, 0f));

        // Carrera por el suelo: hace falta ir de verdad a esa velocidad, no solo
        // tener el boton pulsado.
        ColocarPolvo(sprintDust,
            (isSprinting || isDodging) && isGrounded && Mathf.Abs(m_rigitbody2D.linearVelocityX) > 0.1f,
            new Vector3(sprintDustOffset.x, sprintDustOffset.y, 0f));

        // Carrera vertical: subiendo el personaje siempre mira al muro, asi que
        // basta con el offset tal cual.
        ColocarPolvo(wallRunDust, isWallRunning,
            new Vector3(wallRunDustOffset.x * wallDirection * direction, wallRunDustOffset.y, 0f));
    }

    // Enciende, apaga y recoloca un sistema de particulas.
    private void ColocarPolvo(ParticleSystem sistema, bool activo, Vector3 posicionLocal)
    {
        if (sistema == null) return;

        if (activo)
        {
            sistema.transform.localPosition = posicionLocal;
            if (!sistema.isEmitting) sistema.Play();
        }
        else if (sistema.isEmitting)
        {
            // Deja morir las que ya estan en el aire en vez de borrarlas de golpe.
            sistema.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    // Reduce la velocidad de caída mientras el player está pegado a una pared (deslizamiento).
    private void HandleWallSlide()
    {
        // Solo hay deslizamiento pegado a una pared y en el aire. De pie junto a un muro
        // el rayo también lo detecta, pero ahí no se desliza nada.
        canWallSlide = isWallDetected && !isGrounded
                       && (!wallSlideNeedsInput || m_gatherInput.Value.x * wallDirection > 0f);

        // El flag que lee el Animator excluye además el ataque, para que el espadazo
        // se vea entero, y el derribo, para que no se agarre al muro mientras cae
        // sin control: si no, la animación de pared pisaría a la de derribo.
        // También exige tener el control: durante la aparición por la puerta el
        // player puede estar junto a un muro, y no queremos que salga deslizándose.
        // Y respeta la ventana de animación del ataque: si volviera a activarse en
        // cuanto acaba el cooldown, con cooldowns cortos el espadazo no se vería.
        // Y cede ante la carrera vertical: mientras sube no se desliza, y en
        // cuanto suelta el boton vuelve aqui, que es justo lo que se quiere ver.
        // Y solo cayendo: subiendo pegado al muro no se desliza nada, se sube.
        isWallSliding = canWallSlide && canMove && !isLaunched && !isKnocked
                        && !isAttacking && attackAnimationTimer <= 0f && !isWallRunning
                        && m_rigitbody2D.linearVelocityY <= 0f;

        if (!canWallSlide) return;
        // En plena estocada no se frena: rozar una pared cortaria el picado.
        if (isPlunging) return;
        // Subiendo por el muro manda HandleWallRun: aqui solo estorbariamos
        // frenandole la velocidad vertical que acaba de fijar.
        if (isWallRunning) return;
        // Durante un impulso no frenamos la caída: si no, rozar una pared anularía el lanzamiento.
        if (isLaunched) return;
        // Derribado tampoco se agarra: debe caer hasta el suelo y levantarse ahí.
        if (isKnocked) return;

        // Subiendo no se toca la velocidad. Antes el multiplicador se aplicaba
        // tambien al subir, asi que el impulso del salto de pared pasaba por el
        // varias veces seguidas y llegaba arriba con una fraccion de su fuerza.
        if (m_rigitbody2D.linearVelocityY > 0f) return;

        canDoubleJump = true;

        // Tope de bajada, no multiplicador: la velocidad se recorta solo si se
        // pasa del limite, asi que la caida es constante y se puede ajustar a ojo.
        float tope = m_gatherInput.Value.y < 0f ? wallSlideFastSpeed : wallSlideSpeed;
        if (m_rigitbody2D.linearVelocityY < -tope)
            m_rigitbody2D.linearVelocity = new Vector2(m_rigitbody2D.linearVelocityX, -tope);
    }

    #endregion

    #region Esquiva

    // Lee la pulsacion y lanza el barrido si se puede.
    private void Dodge()
    {
        if (dodgeCooldownTimer > 0f) dodgeCooldownTimer -= Time.fixedDeltaTime;

        if (m_gatherInput.IsDodging) dodgeBuffer = dodgeBufferTime;
        m_gatherInput.IsDodging = false;

        if (dodgeBuffer <= 0f) return;
        dodgeBuffer -= Time.fixedDeltaTime;

        if (isDodging || dodgeCooldownTimer > 0f) return;
        // Solo en el suelo: es un deslizamiento, en el aire no tiene donde apoyarse.
        if (!isGrounded || isWallRunning || isLaunched || isWallJumping) return;
        // Se puede cancelar un ataque, pero solo en sus fotogramas de
        // recuperacion. En plena estocada no: hay que comprometerse con el golpe.
        if (isAttacking && !canAttack) return;

        dodgeBuffer = 0f;
        if (isAttacking) CancelarAtaque();

        // Hacia donde se pulse; sin direccion, hacia donde mira.
        int lado = Mathf.Abs(m_gatherInput.Value.x) > 0.1f ? (int)Mathf.Sign(m_gatherInput.Value.x) : direction;
        SetFacing(lado);

        dodgeRoutine = StartCoroutine(DodgeRoutine(lado));
    }

    private IEnumerator DodgeRoutine(int lado)
    {
        isDodging = true;
        isSprinting = false;
        if (passThroughEnemies) IgnorarEnemigos(true);
        Agacharse(true);

        float t = 0f;
        while (t < dodgeDuration)
        {
            float k = t / dodgeDuration;
            hasIFrames = t >= iFrameStart && t < iFrameStart + iFrameDuration;

            // Arranca a tope y va frenando.
            float velocidad = dodgeSpeed * (1f - dodgeSlowdown * k);
            m_rigitbody2D.linearVelocity = new Vector2(lado * velocidad, m_rigitbody2D.linearVelocityY);

            yield return new WaitForFixedUpdate();
            t += Time.fixedDeltaTime;
        }

        hasIFrames = false;

        // Con techo encima no se puede levantar: sigue deslizandose a la velocidad
        // con la que acabo hasta salir de debajo. Ya sin invulnerabilidad.
        float velocidadFinal = dodgeSpeed * (1f - dodgeSlowdown);
        float bajoTecho = 0f;
        while (HayTechoEncima() && bajoTecho < slideMaxExtraUnderCeiling)
        {
            m_rigitbody2D.linearVelocity = new Vector2(lado * velocidadFinal, m_rigitbody2D.linearVelocityY);
            yield return new WaitForFixedUpdate();
            bajoTecho += Time.fixedDeltaTime;
        }

        Agacharse(false);
        isDodging = false;
        dodgeCooldownTimer = dodgeCooldown;
        currentSpeed = speed;

        // Si acaba dentro de un enemigo, se le deja terminar de atravesarlo.
        if (passThroughEnemies) yield return EsperarASalirDeEnemigos();
        dodgeRoutine = null;
    }

    // Mantiene la colision con los enemigos desactivada mientras siga solapado
    // con alguno, con un tope. Reactivarla estando dentro haria que la fisica
    // lo expulsara de golpe, y que el trigger de dano del enemigo le golpeara.
    private IEnumerator EsperarASalirDeEnemigos()
    {
        if (m_collider != null && capaEnemigos >= 0)
        {
            ContactFilter2D filtro = new ContactFilter2D();
            filtro.SetLayerMask(1 << capaEnemigos);
            filtro.useTriggers = true;

            float extra = 0f;
            while (extra < passThroughMaxExtra && m_collider.Overlap(filtro, solapes) > 0)
            {
                yield return new WaitForFixedUpdate();
                extra += Time.fixedDeltaTime;
            }
        }

        // Si mientras tanto ha empezado otro barrido o estocada, ese ya se encarga.
        if (!isDodging && !isPlunging) IgnorarEnemigos(false);
    }

    // Corta el barrido en seco. Lo usa el dano cuando el golpe entra fuera de
    // la ventana de invulnerabilidad.
    private void TerminarEsquiva()
    {
        if (dodgeRoutine != null) StopCoroutine(dodgeRoutine);
        dodgeRoutine = null;
        isDodging = false;
        hasIFrames = false;
        dodgeCooldownTimer = dodgeCooldown;
        IgnorarEnemigos(false);
        Agacharse(false);
    }

    // Deja el ataque en curso sin terminar, para que el barrido pueda salir.
    private void CancelarAtaque()
    {
        if (attackRoutine != null) StopCoroutine(attackRoutine);
        attackRoutine = null;
        isAttacking = false;
        canAttack = true;
        attackAnimationTimer = 0f;
        comboIndex = 0;
        atacandoEnAire = false;
    }

    // Activa o restaura la colision entre las capas del player y los enemigos.
    // Es un ajuste global de la fisica, asi que al restaurar se vuelve al valor
    // que tenia el proyecto, no a uno fijo.
    private void IgnorarEnemigos(bool ignorar)
    {
        if (capaEnemigos < 0 || ignorandoEnemigos == ignorar) return;

        Physics2D.IgnoreLayerCollision(capaPlayer, capaEnemigos, ignorar || colisionEnemigosOriginal);
        ignorandoEnemigos = ignorar;
    }

    // Si el player muere o se desactiva a mitad de barrido, la colision con los
    // enemigos tiene que volver. Es global: si no, se quedaria desactivada para
    // el siguiente player que aparezca, e incluso en el editor tras salir de Play.
    private void OnDisable()
    {
        IgnorarEnemigos(false);
    }

    // Encoge el capsule a la altura del barrido o lo devuelve a su forma. Encoge
    // desde arriba: los pies se quedan donde estan, asi no se levanta del suelo.
    private void Agacharse(bool agachado)
    {
        if (capsula == null) return;

        if (!agachado)
        {
            capsula.size = capsulaSize;
            capsula.offset = capsulaOffset;
            return;
        }

        float pies = capsulaOffset.y - capsulaSize.y * 0.5f;
        float alto = Mathf.Min(slideColliderHeight, capsulaSize.y);
        capsula.size = new Vector2(capsulaSize.x, alto);
        capsula.offset = new Vector2(capsulaOffset.x, pies + alto * 0.5f);
    }

    // Mira si en el hueco que ocuparia la cabeza al levantarse hay suelo o techo.
    // Solo comprueba esa franja, no el cuerpo entero, para no confundir el suelo
    // que se esta pisando con un techo.
    private bool HayTechoEncima()
    {
        if (capsula == null) return false;

        float pies = capsulaOffset.y - capsulaSize.y * 0.5f;
        float altoAgachado = Mathf.Min(slideColliderHeight, capsulaSize.y);
        float franja = capsulaSize.y - altoAgachado;
        if (franja <= 0.01f) return false;

        Vector2 centroLocal = new Vector2(capsulaOffset.x, pies + altoAgachado + franja * 0.5f);
        Vector2 centro = capsula.transform.TransformPoint(centroLocal);
        Vector3 escala = capsula.transform.lossyScale;
        Vector2 tamano = new Vector2(capsulaSize.x * 0.9f * Mathf.Abs(escala.x), franja * Mathf.Abs(escala.y));

        return Physics2D.OverlapBox(centro, tamano, 0f, groundLayer) != null;
    }

    #endregion

    #region Vida y Daño

    // Aplica daño al player, actualiza la barra de vida, dispara el knockback y controla la muerte.
    public void TakeDamage(int damage)
    {
        // Ignora el daño durante la invulnerabilidad. Esto evita recibir dos golpes a la vez
        // (el contacto del cuerpo del enemigo y el espadazo de su animación).
        if (isInvincible) return;
        // Ventana de invulnerabilidad del barrido. Esquiva golpes de enemigo
        // y trampas por igual; las zonas de muerte no pasan por aqui y siguen
        // matando.
        if (hasIFrames) return;
        // La estocada es invulnerable de principio a fin. Al caer encima de un
        // enemigo, este atacaba en el mismo instante y el golpe del jugador
        // perdia; ademas el rebote tiene que salir limpio.
        if (isPlunging) return;

        currentHealth -= damage;

        healthBar.UpdateHealthBar(currentHealth, maxHealth);

        Debug.Log("Vida actual: " + currentHealth);

        // Si el golpe entra con el barrido aun en marcha (fuera de la ventana de
        // invulnerabilidad), se corta: el retroceso tiene que mandar.
        if (isDodging) TerminarEsquiva();
        if (isPlunging) TerminarEstocada();

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
        // En el suelo es un empujon corto; en el aire el player cae derribado y
        // tiene que levantarse, que se lee mucho mejor que un retroceso en el vacio.
        if (knockDownInAir && !isGrounded)
            StartCoroutine(KnockDownRoutine());
        else
            StartCoroutine(KnockbackRoutine());

        m_rigitbody2D.linearVelocity = new Vector2(knockedPower.x * -direction, knockedPower.y);
    }

    // Derribo: cae sin control, y al tocar el suelo se levanta antes de devolverle el mando.
    private IEnumerator KnockDownRoutine()
    {
        isKnocked = true;
        isKnockedDown = true;
        m_animator.SetBool(idKnockDown, true);

        // Margen para que no detecte como "aterrizaje" el suelo del que acaba de salir.
        yield return new WaitForSeconds(knockDownMinAirTime);
        yield return EsperarAterrizaje();

        // Ya apoyado: frena la inercia y se levanta.
        m_rigitbody2D.linearVelocity = new Vector2(0f, m_rigitbody2D.linearVelocityY);
        m_animator.SetBool(idKnockDown, false);
        m_animator.SetTrigger(IdGetUp);

        yield return new WaitForSeconds(getUpDuration);

        isKnockedDown = false;
        isKnocked = false;
    }

    // Espera a que el player deje de caer tras un derribo.
    //
    // No vale con mirar isGrounded: esa comprobacion solo reconoce la capa Ground,
    // asi que caer encima de un enemigo, una plataforma movil o una trampa no contaba
    // y el player se quedaba colgado repitiendo el derribo sin forma de salir.
    // Por eso tambien damos por aterrizado si lleva un rato sin velocidad vertical,
    // y ademas hay un tiempo maximo como ultima red de seguridad.
    private IEnumerator EsperarAterrizaje()
    {
        float transcurrido = 0f;
        float quieto = 0f;

        while (transcurrido < knockDownMaxTime)
        {
            if (isGrounded) yield break;

            // Si ha dejado de caer es que esta apoyado en algo, sea lo que sea.
            if (Mathf.Abs(m_rigitbody2D.linearVelocityY) < 0.05f)
            {
                quieto += Time.deltaTime;
                if (quieto >= 0.12f) yield break;
            }
            else
            {
                quieto = 0f;
            }

            transcurrido += Time.deltaTime;
            yield return null;
        }
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

    // Devuelve vida al player. Devuelve false si ya estaba lleno, para que quien
    // cura (un corazon, por ejemplo) sepa que no ha hecho nada y no se gaste.
    public bool Heal(int amount)
    {
        if (amount <= 0 || currentHealth <= 0 || currentHealth >= maxHealth) return false;

        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);

        if (healthBar != null) healthBar.AnimateHeal(currentHealth, maxHealth);

        if (healFlashRoutine != null) StopCoroutine(healFlashRoutine);
        healFlashRoutine = StartCoroutine(HealFlashRoutine());
        return true;
    }

    // Tine el sprite de verde un par de veces. Solo toca el RGB y respeta el alfa
    // que tenga en ese momento, por si coincide con un fundido (la puerta, por
    // ejemplo, juega con la transparencia).
    private IEnumerator HealFlashRoutine()
    {
        if (m_spriteRenderer == null) yield break;

        Color original = m_spriteRenderer.color;
        int veces = Mathf.Max(1, healFlashCount);
        float paso = healFlashDuration / (veces * 2f);

        for (int i = 0; i < veces; i++)
        {
            PintarRGB(healFlashColor);
            yield return new WaitForSecondsRealtime(paso);
            PintarRGB(original);
            yield return new WaitForSecondsRealtime(paso);
        }

        PintarRGB(original);
        healFlashRoutine = null;
    }

    private void PintarRGB(Color rgb)
    {
        Color c = m_spriteRenderer.color;
        c.r = rgb.r; c.g = rgb.g; c.b = rgb.b;
        m_spriteRenderer.color = c;
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
        // Solo se anuncia la carrera con los pies en el suelo: en el aire mandan
        // los estados de salto y caida, no el de correr.
        m_animator.SetBool(idIsSprinting, isSprinting && isGrounded && !isKnocked);
        m_animator.SetBool(idIsWallRunning, isWallRunning);
        m_animator.SetBool(idIsDodging, isDodging);
        ActualizarPolvo();
        m_animator.SetBool(idIsGrounded, isGrounded);
        m_animator.SetBool(idIsWallDetected, isWallDetected);
        m_animator.SetBool(idIsWallSliding, isWallSliding);
        // Velocidad vertical: distingue subida (Jump) de caida (Fall) en el Animator.
        m_animator.SetFloat(idVerticalSpeed, m_rigitbody2D.linearVelocityY);
    }

    #endregion
}
