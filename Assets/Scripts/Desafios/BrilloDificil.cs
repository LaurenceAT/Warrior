using UnityEngine;

// Jefe en modo Dificil: un contorno rojizo de un pixel que late despacio. No
// tapa el sprite ni cambia sus colores (los avisos de ataque se ven igual).
public class BrilloDificil : MonoBehaviour
{
    public Color color = new Color(1f, 0.15f, 0.12f, 1f);
    [Range(0f, 1f)] public float intensidad = 0.45f;

    private SpriteRenderer origen, copia;
    private Material material;

    private void Start()
    {
        AnimadorHoja a = GetComponentInChildren<AnimadorHoja>();
        origen = a != null && a.destino != null ? a.destino : GetComponentInChildren<SpriteRenderer>();
        Shader s = RecursosRPG.Get().shaderAura;
        if (s == null) s = Shader.Find("Sprites/Aura");
        if (origen == null || s == null) { enabled = false; return; }
        material = new Material(s);
        material.SetFloat("_Width", 1f);
        material.SetFloat("_Inner", 0.04f);
        copia = new GameObject("BrilloDificil").AddComponent<SpriteRenderer>();
        copia.transform.SetParent(origen.transform, false);
        copia.sharedMaterial = material;
    }

    private void LateUpdate()
    {
        if (copia == null) return;
        copia.enabled = origen.enabled;
        copia.sprite = origen.sprite;
        copia.flipX = origen.flipX;
        copia.sortingLayerID = origen.sortingLayerID;
        copia.sortingOrder = origen.sortingOrder - 1;
        material.SetColor("_AuraColor", color);
        material.SetFloat("_Amount", intensidad * (0.7f + 0.3f * Mathf.Sin(Time.time * 2.4f)));
    }

    private void OnDestroy()
    {
        if (material != null) Destroy(material);
    }
}
