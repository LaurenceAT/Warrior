using System.Collections;
using UnityEngine;

// Destello al recibir un golpe. Va en el enemigo (o en cualquier cosa que reciba
// dano) y lo dispara EnemyHealth.
//
// Funciona de dos maneras segun el material del sprite:
//  - Si usa el shader "Sprites/Flash", pinta la silueta entera de blanco, que es
//    lo que se ve en la mayoria de juegos de accion.
//  - Si no, se conforma con tenir el sprite. Se nota menos, pero no obliga a
//    cambiar el material de nada para que esto funcione.
public class HitFlash : MonoBehaviour
{
    [Header("Destello")]
    // Si se deja vacio se cogen todos los SpriteRenderer del objeto y sus hijos.
    [SerializeField] private SpriteRenderer[] renderers;
    [SerializeField] private Color flashColor = Color.white;
    [SerializeField] private float flashDuration = 0.08f;
    // Cuantas veces destella. Dos parpadeos rapidos se leen mejor que uno largo.
    [SerializeField] private int flashCount = 1;
    // Color del modo de respaldo, cuando el material no lleva el shader.
    // Ahi solo se puede multiplicar, y multiplicar por blanco no cambia nada,
    // asi que tiene que ser un tono que si oscurezca o tina.
    [SerializeField] private Color colorSinShader = new Color(1f, 0.3f, 0.3f);

    [Header("Golpe de escala")]
    // Estiron corto del sprite. Funciona aunque no haya shader y ayuda a que el
    // impacto se lea incluso con el juego congelado por el hit stop.
    [SerializeField] private float scalePunch = 0.15f;
    [SerializeField] private float punchDuration = 0.12f;

    private static readonly int IdFlashColor = Shader.PropertyToID("_FlashColor");
    private static readonly int IdFlashAmount = Shader.PropertyToID("_FlashAmount");

    private MaterialPropertyBlock bloque;
    private Color[] coloresOriginales;
    private bool tieneShaderDeFlash;
    private Vector3 escalaOriginal;
    private Coroutine rutinaFlash;
    private Coroutine rutinaPunch;

    private void Awake()
    {
        if (renderers == null || renderers.Length == 0)
            renderers = GetComponentsInChildren<SpriteRenderer>(true);

        bloque = new MaterialPropertyBlock();
        escalaOriginal = transform.localScale;

        coloresOriginales = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null) coloresOriginales[i] = renderers[i].color;

        // Basta con mirar el primero: todos los sprites de un mismo enemigo
        // comparten material en la practica.
        tieneShaderDeFlash = renderers.Length > 0
                             && renderers[0] != null
                             && renderers[0].sharedMaterial != null
                             && renderers[0].sharedMaterial.HasProperty(IdFlashAmount);

        if (!tieneShaderDeFlash)
            Debug.LogWarning($"[HitFlash] {name} usa un material sin el shader Sprites/Flash, " +
                             "asi que el destello se hace tinendo el sprite. Para el destello " +
                             "entero, asigna un material con ese shader al SpriteRenderer.", this);
    }

    // Punto de entrada. Lo llama EnemyHealth al recibir dano.
    public void Flash()
    {
        if (!isActiveAndEnabled) return;

        if (rutinaFlash != null) StopCoroutine(rutinaFlash);
        rutinaFlash = StartCoroutine(FlashRoutine());

        if (scalePunch > 0f)
        {
            if (rutinaPunch != null) StopCoroutine(rutinaPunch);
            rutinaPunch = StartCoroutine(PunchRoutine());
        }
    }

    private IEnumerator FlashRoutine()
    {
        int veces = Mathf.Max(1, flashCount);
        float paso = flashDuration / veces;

        for (int i = 0; i < veces; i++)
        {
            AplicarFlash(1f);
            // Sin escalar por timeScale: el destello tiene que verse igual de
            // largo aunque el hit stop haya congelado el juego.
            yield return new WaitForSecondsRealtime(paso * 0.5f);

            AplicarFlash(0f);
            if (i < veces - 1) yield return new WaitForSecondsRealtime(paso * 0.5f);
        }

        AplicarFlash(0f);
        rutinaFlash = null;
    }

    // cantidad va de 0 (normal) a 1 (silueta del color del destello).
    private void AplicarFlash(float cantidad)
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer sr = renderers[i];
            if (sr == null) continue;

            if (tieneShaderDeFlash)
            {
                sr.GetPropertyBlock(bloque);
                bloque.SetColor(IdFlashColor, flashColor);
                bloque.SetFloat(IdFlashAmount, cantidad);
                sr.SetPropertyBlock(bloque);
            }
            else
            {
                // Sin shader solo se puede multiplicar: no hay forma de aclarar
                // el sprite, solo de tenirlo.
                sr.color = Color.Lerp(coloresOriginales[i], colorSinShader, cantidad);
            }
        }
    }

    private IEnumerator PunchRoutine()
    {
        float transcurrido = 0f;

        while (transcurrido < punchDuration)
        {
            transcurrido += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(transcurrido / punchDuration);

            // Sube de golpe y vuelve: un seno da justo esa forma.
            float extra = Mathf.Sin(k * Mathf.PI) * scalePunch;
            transform.localScale = escalaOriginal * (1f + extra);
            yield return null;
        }

        transform.localScale = escalaOriginal;
        rutinaPunch = null;
    }

    // Si el enemigo muere a media animacion, hay que dejar el sprite como estaba:
    // el destello vive en un MaterialPropertyBlock que sobrevive al objeto.
    private void OnDisable()
    {
        AplicarFlash(0f);
        transform.localScale = escalaOriginal;
        rutinaFlash = null;
        rutinaPunch = null;
    }
}
