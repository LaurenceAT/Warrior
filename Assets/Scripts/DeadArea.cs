using Unity.Cinemachine;
using UnityEngine;

// Zona de muerte instantánea (ej. fuera del nivel o lava) que mata y reaparece al player.
public class DeadArea : MonoBehaviour
{
    // REFERENCIA AL COMPONENTE DEL PLAYER
    [SerializeField] private PlayerControler player;

    // Detecta la colisión con la zona de muerte y ejecuta la muerte + respawn del player.
    private void OnTriggerEnter2D(Collider2D other)
    {
        // VERIFICA SI EL OBJETO QUE ENTRÓ AL TRIGGER ES EL PLAYER
        if (other.CompareTag("Player"))
        {
            // OBTIENE EL COMPONENTE DEL PLAYER QUE HA COLISIONADO
            player = other.gameObject.GetComponent<PlayerControler>();

            // EJECUTA LA MUERTE DEL JUGADOR
            player.Die();

            // INICIA EL PROCESO DE REAPARICIÓN DEL JUGADOR
            GameManager.Instance.RespawnPlayer();
        }
    }
}
