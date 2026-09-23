using UnityEngine;
using UnityEngine.InputSystem;

// Lee los inputs del jugador (movimiento y salto) usando el Input System
// y los expone como propiedades para que otros scripts (PlayerControler) los consuman.
public class GatherInput : MonoBehaviour
{
    private Controls controls;

    // Valor del input de movimiento (eje X e Y), normalizado.
    [SerializeField] private Vector2 _value;
    public Vector2 Value { get => _value; }

    // Indica si se presionó el botón de salto este frame.
    [SerializeField] private bool _isJumping;
    public bool IsJumping { get => _isJumping; set => _isJumping = value; }

    // Indica si se presionó el botón de ataque este frame.
    [SerializeField] private bool _isAttacking;
    public bool IsAttacking { get => _isAttacking; set => _isAttacking = value; }

    // Indica si el jugador mantiene pulsada la tecla de correr. A diferencia del
    // ataque, esto no es una pulsacion que se consume: es un estado sostenido,
    // asi que aqui si escuchamos tambien el "canceled" para apagarlo al soltar.
    [SerializeField] private bool _isSprinting;
    public bool IsSprinting { get => _isSprinting; }

    // Pulsacion del disparo con arco. Como el ataque: se marca al pulsar y la consume
    // PlayerControler, asi un toque breve no se pierde entre pasos de fisica.
    [SerializeField] private bool _isShooting;
    public bool IsShooting { get => _isShooting; set => _isShooting = value; }
    // Si el boton de disparo sigue pulsado. Sirve para cargar el disparo: la
    // pulsacion de arriba se consume al empezar, esto dura mientras se mantenga.
    [SerializeField] private bool _isShootHeld;
    public bool IsShootHeld { get => _isShootHeld; }

    // Pulsacion de esquiva. Como el ataque: se marca al pulsar y la consume
    // PlayerControler, asi un toque breve no se pierde entre pasos de fisica.
    [SerializeField] private bool _isDodging;
    public bool IsDodging { get => _isDodging; set => _isDodging = value; }

    #region Unity Lifecycle

    // Crea la instancia del sistema de controles generado por el Input System.
    private void Awake()
    {
        controls = new Controls();
    }

    // Suscribe los callbacks de movimiento/salto y habilita el mapa de acciones "Player".
    private void OnEnable()
    {
        controls.Player.Move.performed += StartMove;
        controls.Player.Move.canceled += StopMove;
        controls.Player.Jump.performed += StartJump;
        controls.Player.Jump.canceled += StopJump;
        // El ataque solo escucha "performed". No se limpia al soltar el boton:
        // de la otra forma, un clic mas corto que un paso de fisica se perdia entero.
        // Quien consume la pulsacion es PlayerControler, poniendo IsAttacking a false.
        controls.Player.Attack.performed += StartAttack;
        controls.Player.Sprint.performed += StartSprint;
        controls.Player.Sprint.canceled += StopSprint;
        controls.Player.Shoot.performed += StartShoot;
        controls.Player.Shoot.canceled += StopShoot;
        controls.Player.Dodge.performed += StartDodge;
        controls.Player.Enable();
    }

    // Desuscribe los callbacks y deshabilita el mapa de acciones "Player".
    private void OnDisable()
    {
        controls.Player.Move.performed -= StartMove;
        controls.Player.Move.canceled -= StopMove;
        controls.Player.Jump.performed -= StartJump;
        controls.Player.Jump.canceled -= StopJump;
        controls.Player.Attack.performed -= StartAttack;
        controls.Player.Sprint.performed -= StartSprint;
        controls.Player.Sprint.canceled -= StopSprint;
        controls.Player.Shoot.performed -= StartShoot;
        controls.Player.Shoot.canceled -= StopShoot;
        controls.Player.Dodge.performed -= StartDodge;
        controls.Player.Disable();
    }

    #endregion

    #region Input Callbacks

    // Captura el valor del input de movimiento cuando se presiona/mantiene.
    private void StartMove(InputAction.CallbackContext context)
    {
        _value = context.ReadValue<Vector2>().normalized;
    }

    // Resetea el movimiento a cero cuando se suelta el input.
    private void StopMove(InputAction.CallbackContext context)
    {
        _value = Vector2.zero;
    }

    // Marca el salto como activo cuando se presiona el botón.
    private void StartJump(InputAction.CallbackContext context)
    {
        _isJumping = true;
    }

    // Marca el salto como inactivo cuando se suelta el botón.
    private void StopJump(InputAction.CallbackContext context)
    {
        _isJumping = false;
    }

    // Marca el ataque como activo cuando se presiona el botón.
    private void StartAttack(InputAction.CallbackContext context)
    {
        _isAttacking = true;
    }

    // Mantiene la carrera activa mientras el boton siga pulsado.
    private void StartSprint(InputAction.CallbackContext context)
    {
        _isSprinting = true;
    }

    // Y la apaga al soltarlo.
    private void StopSprint(InputAction.CallbackContext context)
    {
        _isSprinting = false;
    }

    // Marca el disparo al pulsar el boton.
    private void StartShoot(InputAction.CallbackContext context)
    {
        _isShooting = true;
        _isShootHeld = true;
    }

    // Suelta el boton de disparo: se acaba la carga.
    private void StopShoot(InputAction.CallbackContext context)
    {
        _isShootHeld = false;
    }

    // Marca la esquiva al pulsar el boton.
    private void StartDodge(InputAction.CallbackContext context)
    {
        _isDodging = true;
    }

    #endregion
}
