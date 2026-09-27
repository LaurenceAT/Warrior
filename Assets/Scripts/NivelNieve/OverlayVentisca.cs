using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// La ventisca que se ve delante de la camara. Solo hay una, aunque haya varias
// zonas: manda la mas intensa.
public class OverlayVentisca : MonoBehaviour
{
    private static OverlayVentisca instancia;
    private static readonly Dictionary<ZonaVentisca, float> pedidas = new Dictionary<ZonaVentisca, float>();
    public static float IntensidadMaxima { get; private set; }

    private SpriteRenderer capaCerca, capaLejos;
    private AnimadorHoja animCerca, animLejos;
    private Image niebla;
    private float neblinaMax = 0.3f;

    public static void Poner(ZonaVentisca zona, float intensidad, float neblina, AnimadorHoja.Clip clip)
    {
        if (intensidad <= 0.001f) pedidas.Remove(zona);
        else pedidas[zona] = intensidad;
        IntensidadMaxima = 0f;
        foreach (float v in pedidas.Values) IntensidadMaxima = Mathf.Max(IntensidadMaxima, v);
        if (IntensidadMaxima <= 0.001f && instancia == null) return;
        if (instancia == null && clip != null) instancia = Crear(clip);
        if (instancia != null && neblina > 0f) instancia.neblinaMax = neblina;
    }

    private static OverlayVentisca Crear(AnimadorHoja.Clip clip)
    {
        Camera cam = Camera.main;
        if (cam == null) return null;
        GameObject go = new GameObject("OverlayVentisca");
        go.transform.SetParent(cam.transform, false);
        go.transform.localPosition = new Vector3(0f, 0f, 5f);
        OverlayVentisca o = go.AddComponent<OverlayVentisca>();
        o.capaCerca = o.Capa("Cerca", clip, 60, out o.animCerca, 1f);
        o.capaLejos = o.Capa("Lejos", clip, 59, out o.animLejos, 0.7f);

        // Neblina blanca: tapa la vista de lejos.
        GameObject lienzo = new GameObject("NieblaVentisca");
        lienzo.transform.SetParent(go.transform, false);
        Canvas c = lienzo.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = -5;
        o.niebla = new GameObject("Niebla").AddComponent<Image>();
        o.niebla.transform.SetParent(lienzo.transform, false);
        o.niebla.raycastTarget = false;
        o.niebla.color = new Color(0.9f, 0.94f, 1f, 0f);
        RectTransform r = o.niebla.rectTransform;
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
        return o;
    }

    private SpriteRenderer Capa(string nombre, AnimadorHoja.Clip clip, int orden, out AnimadorHoja anim, float velocidad)
    {
        SpriteRenderer sr = new GameObject(nombre).AddComponent<SpriteRenderer>();
        sr.transform.SetParent(transform, false);
        sr.sortingLayerName = "VFX";
        sr.sortingOrder = orden;
        sr.sharedMaterial = EfectoVisual.MaterialSinLuz();
        anim = sr.gameObject.AddComponent<AnimadorHoja>();
        anim.destino = sr;
        anim.clips = new List<AnimadorHoja.Clip> { clip };
        anim.Preparar();
        anim.Reproducir(clip.nombre, true, velocidad);
        return sr;
    }

    private void LateUpdate()
    {
        Camera cam = Camera.main;
        float alto = cam != null ? cam.orthographicSize * 2f : 10f;
        float ancho = cam != null ? alto * cam.aspect : 18f;
        float a = IntensidadMaxima;
        Ajustar(capaCerca, ancho * 1.05f, alto * 1.05f, a * 0.95f);
        Ajustar(capaLejos, ancho * 1.6f, alto * 1.6f, a * 0.6f);
        // La capa lejana va algo desplazada para que no coincidan los copos.
        capaLejos.transform.localPosition = new Vector3(Mathf.Sin(Time.time * 0.3f) * 0.8f, 0.4f, 0.1f);
        if (niebla != null) niebla.color = new Color(0.9f, 0.94f, 1f, a * neblinaMax);
        capaCerca.enabled = capaLejos.enabled = a > 0.01f;
    }

    private static void Ajustar(SpriteRenderer sr, float ancho, float alto, float alfa)
    {
        if (sr == null || sr.sprite == null) return;
        Vector2 t = sr.sprite.bounds.size;
        sr.transform.localScale = new Vector3(ancho / t.x, alto / t.y, 1f);
        sr.color = new Color(1f, 1f, 1f, alfa);
    }
}
