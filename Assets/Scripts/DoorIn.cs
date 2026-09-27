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

        // Antes la puerta pedia todos los diamantes. Ahora son cristales de alma
        // (moneda para subir de nivel) y ya no hacen falta para salir.

        MyAnimator.SetTrigger(IdOpenDoor);
        PlayerControler player = other.GetComponent<PlayerControler>();
        if (player != null)
        {
            other.transform.position = new Vector3(transform.position.x, other.transform.position.y, other.transform.position.z);
            player.DoorIn();
        }
    }
}
