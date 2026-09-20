using System.Collections;
using UnityEngine;

// Trampolín: al pisarlo, impulsa al player hacia arriba y reproduce su animación
// de rebote antes de volver al reposo.
// Funciona con el collider en trigger o como collider sólido; atiende a los dos casos.
[RequireComponent(typeof(Collider2D))]
public class Trampoline : MonoBehaviour
{
    [Header("Rebote")]
    // Velocidad vertical que recibe el player. Para comparar: su salto normal es 10.
    [SerializeField] private float bounceVelocity = 18f;
    // Tiempo mínimo entre dos rebotes, para que no se dispare dos veces seguidas.
    [SerializeField] private float cooldown = 0.15f;

    [Header("Animación")]
    [SerializeField] private Animator animator;
    // Nombres de los estados del Animator. Se usan por nombre, sin necesidad de
    // crear parámetros ni transiciones en el controlador.
    [SerializeField] private string idleStateName = "Trampolline_Idle";
    [SerializeField] private string activeStateName = "Trampolline_Active";
    // Cuánto se ve la animación de rebote antes de volver al reposo.
    [SerializeField] private float activeTime = 0.3f;

    [Header("Gizmo")]
    // Solo para dibujar la altura estimada del rebote en el editor.
    // Debe coincidir con el Gravity Scale del Rigidbody2D del player.
    [SerializeField] private float playerGravityScale = 3f;

    private bool ready = true;

    #region Ciclo de Unity

    private void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    private void Start()
    {
        // Avisa pronto si los nombres no coinciden: Animator.Play() con un estado
        // inexistente no da error, simplemente no hace nada.
        WarnIfMissingState(idleStateName);
        WarnIfMissingState(activeStateName);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryBounce(other);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryBounce(collision.collider);
    }

    // Dibuja hasta dónde llegará el player, para colocar las plataformas de arriba.
    private void OnDrawGizmosSelected()
    {
        float gravedad = 9.81f * playerGravityScale;
        if (gravedad <= 0f) return;

        float altura = (bounceVelocity * bounceVelocity) / (2f * gravedad);
        Vector3 cima = transform.position + Vector3.up * altura;

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, cima);
        Gizmos.DrawLine(cima + Vector3.left * 0.5f, cima + Vector3.right * 0.5f);
    }

    #endregion

    // Comprueba que sea el player y lanza el rebote.
    private void TryBounce(Collider2D other)
    {
        if (!ready) return;
        if (!other.CompareTag("Player")) return;

        PlayerControler player = other.GetComponent<PlayerControler>();
        if (player == null) return;

        player.Bounce(bounceVelocity);
        StartCoroutine(BounceRoutine());
    }

    // Reproduce la animación de rebote y devuelve el trampolín a su estado de reposo.
    private IEnumerator BounceRoutine()
    {
        ready = false;

        if (animator != null && !string.IsNullOrEmpty(activeStateName))
            animator.Play(activeStateName, 0, 0f);

        yield return new WaitForSeconds(Mathf.Max(activeTime, cooldown));

        if (animator != null && !string.IsNullOrEmpty(idleStateName))
            animator.Play(idleStateName, 0, 0f);

        ready = true;
    }

    private void WarnIfMissingState(string stateName)
    {
        if (animator == null || string.IsNullOrEmpty(stateName)) return;
        if (animator.HasState(0, Animator.StringToHash(stateName))) return;

        Debug.LogWarning($"El trampolín '{name}' no encuentra el estado '{stateName}' en su Animator. Revisa el nombre.", this);
    }
}
