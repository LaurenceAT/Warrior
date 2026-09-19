using UnityEngine;

// Punto de reaparición: se activa una sola vez al pasar el player y actualiza el respawn en el GameManager.
public class Checkpoint : MonoBehaviour
{
    // ID DEL PARÁMETRO DEL ANIMATOR PARA ACTIVAR EL CHECKPOINT
    private static readonly int IsActive = Animator.StringToHash("isActive");

    // REFERENCIAS Y ESTADO DEL CHECKPOINT
    [SerializeField] private Animator animator;
    [SerializeField] private bool isActive;

    // Obtiene el componente Animator del checkpoint.
    private void Awake()
    {
        animator = GetComponent<Animator>();
    }

    // Detecta la colisión con el player: activa el checkpoint (una sola vez) y guarda el punto de respawn.
    private void OnTriggerEnter2D(Collider2D other)
    {
        // Solo el player activa un checkpoint. Cualquier otro collider que pase por aquí
        // (un enemigo patrullando, por ejemplo) no debe tocar el punto de reaparición.
        if (!other.CompareTag("Player")) return;

        // EVITA ACTIVAR EL CHECKPOINT MÁS DE UNA VEZ
        if (isActive) return;

        ActiveCheckpoint();
    }

    // Activa la animación, marca el checkpoint como activo y guarda el punto de reaparición.
    private void ActiveCheckpoint()
    {
        isActive = true;
        animator.SetTrigger(IsActive);

        GameManager.Instance.hasCheckPointActive = true;
        GameManager.Instance.checkpointRespawnPosition = transform.position;
    }
}
