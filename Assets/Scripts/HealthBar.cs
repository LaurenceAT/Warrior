using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Controla el relleno visual de la barra de vida en la UI.
public class HealthBar : MonoBehaviour
{
    [SerializeField] private Image fillImage;

    [Header("Curacion")]
    // Cuanto tarda la barra en rellenar el tramo curado. Instantaneo no se ve:
    // el jugador tiene que notar que la barra sube.
    [SerializeField] private float healFillDuration = 0.35f;
    // Color al que destella el relleno mientras sube.
    [SerializeField] private Color healColor = new Color(0.45f, 1f, 0.45f, 1f);
    // Estiron de la barra entera al curar.
    [SerializeField] private float healPunch = 0.12f;

    private Color colorBase;
    private Vector3 escalaBase;
    private Coroutine rutinaCura;

    private void Awake()
    {
        if (fillImage != null) colorBase = fillImage.color;
        escalaBase = transform.localScale;
    }

    // Actualiza el porcentaje de relleno de la barra según la vida actual y máxima.
    // Es instantaneo a proposito: lo usan el dano, la muerte y el arranque del
    // nivel, y ahi no queremos animacion ni destello verde.
    public void UpdateHealthBar(int currentHealth, int maxHealth)
    {
        if (rutinaCura != null)
        {
            StopCoroutine(rutinaCura);
            RestaurarAspecto();
            rutinaCura = null;
        }

        fillImage.fillAmount = Porcentaje(currentHealth, maxHealth);
    }

    // Igual que UpdateHealthBar, pero la barra sube poco a poco, destella en
    // verde y da un pequeno estiron. Lo usa la curacion.
    public void AnimateHeal(int currentHealth, int maxHealth)
    {
        if (rutinaCura != null) StopCoroutine(rutinaCura);
        rutinaCura = StartCoroutine(CuraRoutine(Porcentaje(currentHealth, maxHealth)));
    }

    private IEnumerator CuraRoutine(float destino)
    {
        float inicio = fillImage.fillAmount;
        float transcurrido = 0f;

        while (transcurrido < healFillDuration)
        {
            // Tiempo real: si justo coincide con un hit stop, la barra no se congela.
            transcurrido += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(transcurrido / healFillDuration);

            // Sale rapido y frena al llegar.
            float suave = 1f - (1f - k) * (1f - k);
            fillImage.fillAmount = Mathf.Lerp(inicio, destino, suave);

            // El verde se va apagando hacia el color normal mientras sube.
            fillImage.color = Color.Lerp(healColor, colorBase, k);

            // Crece y vuelve con forma de campana.
            transform.localScale = escalaBase * (1f + Mathf.Sin(k * Mathf.PI) * healPunch);

            yield return null;
        }

        fillImage.fillAmount = destino;
        RestaurarAspecto();
        rutinaCura = null;
    }

    private void RestaurarAspecto()
    {
        fillImage.color = colorBase;
        transform.localScale = escalaBase;
    }

    private static float Porcentaje(int actual, int maximo)
    {
        return maximo > 0 ? Mathf.Clamp01((float)actual / maximo) : 0f;
    }
}
