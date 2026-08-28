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
        Debug.Log("Trigger con: " + other.name);

        // EVITA ACTIVAR EL CHECKPOINT MÁS DE UNA VEZ
        if (isActive) return;

        // ACTIVA EL CHECKPOINT AL ENTRAR EL PLAYER
        if (other.CompareTag("Player"))
            ActiveCheckpoint();

        // GUARDA LA POSICIÓN DEL CHECKPOINT COMO NUEVO PUNTO DE REAPARICIÓN
        GameManager.Instance.hasCheckPointActive = true;
        GameManager.Instance.checkpointRespawnPosition = transform.position;
    }

    // Activa la animación y cambia el estado del checkpoint a activo.
    private void ActiveCheckpoint()
    {
        isActive = true;
        animator.SetTrigger(IsActive);
    }
}
