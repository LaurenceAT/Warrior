using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Destello de pantalla completa: un velo de color que aparece de golpe y se
// desvanece. Se crea al vuelo y se destruye solo, asi que no hay que montar nada
// en las escenas.
//
// Uso: ScreenFlash.Destello(color, duracion);
public class ScreenFlash : MonoBehaviour
{
    private Image velo;

    public static void Destello(Color color, float duracion)
    {
        if (duracion <= 0f || color.a <= 0f) return;

        GameObject go = new GameObject("ScreenFlash");
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // Por encima del HUD, pero es tan breve que no estorba.
        canvas.sortingOrder = 1000;

        GameObject hijo = new GameObject("Velo");
        hijo.transform.SetParent(go.transform, false);
        Image img = hijo.AddComponent<Image>();
        img.raycastTarget = false;
        RectTransform rt = img.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        ScreenFlash flash = go.AddComponent<ScreenFlash>();
        flash.velo = img;
        flash.StartCoroutine(flash.Rutina(color, duracion));
    }

    private IEnumerator Rutina(Color color, float duracion)
    {
        float t = 0f;
        while (t < duracion)
        {
            // Tiempo real: suele coincidir con un hit stop que congela el juego.
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duracion);

            Color c = color;
            c.a = color.a * (1f - k) * (1f - k);
            velo.color = c;
            yield return null;
        }

        Destroy(gameObject);
    }
}
