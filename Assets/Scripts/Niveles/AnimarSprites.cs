using UnityEngine;
using UnityEngine.Rendering.Universal;

// Anima un SpriteRenderer pasando por una lista de sprites (antorchas de la
// decoracion). Con "luz", ademas pone una luz 2D calida que parpadea un poco.
public class AnimarSprites : MonoBehaviour
{
    public Sprite[] fotogramas;
    public float fps = 10f;
    public bool luz;
    public Color colorLuz = new Color(1f, 0.62f, 0.3f);
    public float intensidadLuz = 0.9f;
    public float radioLuz = 3.2f;

    private SpriteRenderer sr;
    private Light2D luz2D;
    private float desfase;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        desfase = Random.value * 10f;
        if (luz)
        {
            GameObject go = new GameObject("Luz");
            go.transform.SetParent(transform, false);
            // Arriba del todo: la llama.
            if (sr != null && sr.sprite != null) go.transform.localPosition = new Vector3(0f, sr.sprite.bounds.max.y * 0.8f, 0f);
            luz2D = go.AddComponent<Light2D>();
            luz2D.lightType = Light2D.LightType.Point;
            luz2D.color = colorLuz;
            luz2D.pointLightOuterRadius = radioLuz / Mathf.Max(0.01f, transform.lossyScale.x);
            luz2D.pointLightInnerRadius = 0.2f;
            luz2D.intensity = intensidadLuz;
        }
    }

    private void Update()
    {
        float t = Time.time + desfase;
        if (sr != null && fotogramas != null && fotogramas.Length > 0)
            sr.sprite = fotogramas[(int)(t * fps) % fotogramas.Length];
        if (luz2D != null)
            luz2D.intensity = intensidadLuz * (0.85f + 0.15f * Mathf.PerlinNoise(t * 3f, 0.5f));
    }
}
