using UnityEngine;

// Puerta de salida de nivel: al tocarla el player (con checkpoint activo), lo encamina y avanza de nivel.
public class DoorIn : MonoBehaviour
{
    private static readonly int IdOpenDoor = Animator.StringToHash("OpenDoor");
    private Animator MyAnimator => GetComponent<Animator>();

    // Detecta al player entrando a la puerta, lo centra en ella y dispara la animación + transición de nivel.
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!GameManager.Instance.hasCheckPointActive) return;
        if (!other.CompareTag("Player")) return;

        // La puerta no se abre hasta juntar todos los diamantes del nivel.
        if (!GameManager.Instance.AllDiamondsCollected)
        {
            Debug.Log($"Faltan diamantes: {GameManager.Instance.DiamondCollected} / {GameManager.Instance.TotalDiamonds}");
            return;
        }

        MyAnimator.SetTrigger(IdOpenDoor);
        PlayerControler player = other.GetComponent<PlayerControler>();
        if (player != null)
        {
            other.transform.position = new Vector3(transform.position.x, other.transform.position.y, other.transform.position.z);
            player.DoorIn();
        }
    }
}
