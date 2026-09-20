using System.Collections;
using UnityEngine;

// Utilidades de aparición y desaparición que comparten las trampas que se ocultan
// y vuelven (la flecha giratoria, la plataforma que cae...).
public static class TrapFx
{
    // Curva que sobrepasa el destino y vuelve. Es lo que da la sensación de rebote
    // al aparecer, en lugar de un crecimiento plano.
    public static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;

        float p = t - 1f;
        return 1f + c3 * p * p * p + c1 * p * p;
    }

    // Interpola escala y transparencia entre dos factores (0 = invisible, 1 = normal).
    public static IEnumerator ScaleAndFade(Transform target, SpriteRenderer renderer,
        Vector3 baseScale, Color baseColor, float from, float to, float duration, bool overshoot)
    {
        if (duration <= 0f)
        {
            Apply(target, renderer, baseScale, baseColor, to);
            yield break;
        }

        float transcurrido = 0f;
        while (transcurrido < duration)
        {
            transcurrido += Time.deltaTime;
            float k = Mathf.Clamp01(transcurrido / duration);
            if (overshoot) k = EaseOutBack(k);

            Apply(target, renderer, baseScale, baseColor, Mathf.LerpUnclamped(from, to, k));
            yield return null;
        }

        Apply(target, renderer, baseScale, baseColor, to);
    }

    // Parpadeo de aviso: la trampa destella pequeña y tenue antes de volver, para que
    // el jugador vea dónde y cuándo va a reaparecer.
    public static IEnumerator Telegraph(Transform target, SpriteRenderer renderer,
        Vector3 baseScale, Color baseColor, float scale, float duration, int blinks)
    {
        if (duration <= 0f || blinks <= 0 || renderer == null) yield break;

        Apply(target, renderer, baseScale, baseColor, scale);
        SetAlpha(renderer, baseColor, 0.4f);

        float paso = duration / (blinks * 2f);
        for (int i = 0; i < blinks; i++)
        {
            renderer.enabled = true;
            yield return new WaitForSeconds(paso);
            renderer.enabled = false;
            yield return new WaitForSeconds(paso);
        }
    }

    // Aplica un factor de 0 a 1 sobre la escala y la transparencia originales.
    public static void Apply(Transform target, SpriteRenderer renderer, Vector3 baseScale, Color baseColor, float k)
    {
        if (target != null) target.localScale = baseScale * k;
        SetAlpha(renderer, baseColor, baseColor.a * Mathf.Clamp01(k));
    }

    public static void SetAlpha(SpriteRenderer renderer, Color baseColor, float alpha)
    {
        if (renderer == null) return;

        Color color = baseColor;
        color.a = alpha;
        renderer.color = color;
    }
}
