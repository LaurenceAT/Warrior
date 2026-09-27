using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Escarcha en el suelo (la dejan los ataques de hielo del jefe en la fase 2).
// Pisarla ralentiza al player un rato. Se derrite sola.
public class ZonaEscarcha : MonoBehaviour
{
    private float vida;
    private float t;
    private SpriteRenderer[] visuales;
    private float factor;

    public static ZonaEscarcha Crear(Vector2 suelo, float ancho, float vida, float factor = 0.55f, AnimadorHoja.Clip clip = null)
    {
        GameObject go = new GameObject("Escarcha");
        go.layer = LayerMask.NameToLayer("Traps");
        go.transform.position = suelo;
        BoxCollider2D box = go.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = new Vector2(ancho, 0.5f);
        box.offset = new Vector2(0f, 0.25f);
        ZonaEscarcha z = go.AddComponent<ZonaEscarcha>();
        z.vida = vida;
        z.factor = factor;

        // Capa de escarcha: una franja azul palida con cristales encima.
        var lista = new List<SpriteRenderer>();
        SpriteRenderer franja = new GameObject("Franja").AddComponent<SpriteRenderer>();
        franja.transform.SetParent(go.transform, false);
        franja.sprite = Blanco();
        franja.transform.localScale = new Vector3(ancho, 0.18f, 1f);
        franja.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        franja.color = new Color(0.75f, 0.92f, 1f, 0.8f);
        franja.sortingLayerName = "VFX";
        franja.sortingOrder = 1;
        franja.sharedMaterial = EfectoVisual.MaterialSinLuz();
        lista.Add(franja);
        if (clip != null)
        {
            for (float x = -ancho * 0.5f + 0.5f; x <= ancho * 0.5f - 0.4f; x += 1.1f)
            {
                EfectoVisual e = EfectoVisual.Crear(clip, suelo + new Vector2(x, 0f), 0.55f, new Color(0.85f, 0.97f, 1f, 0.9f), Random.value < 0.5f, -1f, "VFX", 2);
                if (e == null) continue;
                e.transform.SetParent(go.transform, true);
                lista.Add(e.Render);
            }
        }
        z.visuales = lista.ToArray();
        PeligrosJefe.Registrar(go);
        return z;
    }

    private void Update()
    {
        t += Time.deltaTime;
        // Se derrite en el ultimo segundo.
        float a = Mathf.Clamp01(vida - t);
        foreach (SpriteRenderer s in visuales)
        {
            if (s == null) continue;
            Color c = s.color;
            c.a = Mathf.Min(c.a, a * 0.9f);
            s.color = c;
        }
        if (t >= vida) Destroy(gameObject);
    }

    private void OnTriggerStay2D(Collider2D otro)
    {
        if (!otro.CompareTag("Player")) return;
        PlayerControler p = otro.GetComponent<PlayerControler>();
        if (p != null) p.Ralentizar(factor, 2.2f);
    }

    private static Sprite blanco;
    public static Sprite Blanco()
    {
        if (blanco != null) return blanco;
        Texture2D tex = new Texture2D(4, 4) { filterMode = FilterMode.Point };
        Color[] px = new Color[16];
        for (int i = 0; i < 16; i++) px[i] = Color.white;
        tex.SetPixels(px);
        tex.Apply();
        blanco = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
        return blanco;
    }
}
