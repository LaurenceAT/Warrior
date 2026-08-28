using UnityEngine;

// Utilidad para destruir este GameObject desde un evento de animación.
public class DestroyEvent : MonoBehaviour
{
    // Destruye este GameObject (llamado normalmente como Animation Event al final de una animación).
    public void DestroyGameObject() => Destroy(gameObject);
}
