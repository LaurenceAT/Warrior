using UnityEngine;
using UnityEngine.UI;

// Controla el relleno visual de la barra de vida en la UI.
public class HealthBar : MonoBehaviour
{
    [SerializeField] private Image fillImage;

    // Actualiza el porcentaje de relleno de la barra según la vida actual y máxima.
    public void UpdateHealthBar(int currentHealth, int maxHealth)
    {
        fillImage.fillAmount = (float)currentHealth / maxHealth;
    }
}
