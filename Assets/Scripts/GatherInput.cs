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

    #endregion
}
