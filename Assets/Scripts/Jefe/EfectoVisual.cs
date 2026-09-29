using System.Collections.Generic;
using UnityEngine;

// Efecto de un solo uso (explosion, polvo, portal...): crea un objeto con el
// sprite animado, lo reproduce y se borra al acabar. Si el clip es en bucle, se
// borra al pasar "vida" segundos (o nunca, con vida <= 0: lo borra quien lo cree).
public class EfectoVisual : MonoBehaviour
{
    private AnimadorHoja anim;
    private float vida;
    private float t;
    private SpriteRenderer sr;
    private float desvanecer;

    public SpriteRenderer Render => sr;

    public static EfectoVisual Crear(AnimadorHoja.Clip clip, Vector2 pos, float escala, Color color,
                                     bool voltear = false, float vida = -1f, string capa = "VFX", int orden = 10,
                                     float rotacion = 0f)
    {
        if (clip == null) return null;
        GameObject go = new GameObject("Efecto_" + clip.nombre);
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(0f, 0f, rotacion);
        go.transform.localScale = new Vector3(voltear ? -escala : escala, escala, 1f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.color = color;
        sr.sortingLayerName = capa;
        sr.sortingOrder = orden;
        sr.sharedMaterial = MaterialSinLuz();

        AnimadorHoja a = go.AddComponent<AnimadorHoja>();
        a.destino = sr;
        a.clips = new List<AnimadorHoja.Clip> { clip };
        // El animador corta los sprites en su Awake, que ya ha corrido al
        // anadirlo sin clips: se fuerza de nuevo.
        a.Preparar();
        a.Reproducir(clip.nombre, true);

        EfectoVisual e = go.AddComponent<EfectoVisual>();
        e.anim = a;
        e.sr = sr;
        e.vida = clip.bucle ? vida : Mathf.Max(vida, clip.Duracion);
        return e;
    }

    // Borra los efectos en bucle que no tienen fin propio (glifos, orbes...). Lo
    // usa "Destrabar" por si alguno se ha quedado atascado en pantalla.
    public static void LimpiarAtascados()
    {
        foreach (EfectoVisual e in FindObjectsByType<EfectoVisual>(FindObjectsSortMode.None))
            if (e.vida <= 0f) Destroy(e.gameObject);
    }

    // Hace que la animacion (sin bucle) quepa en "segundos" y se borre justo al
    // acabar, sin quedarse en el ultimo fotograma.
    public void Durar(float segundos)
    {
        if (anim == null || segundos <= 0f) return;
        AnimadorHoja.Clip c = anim.clips != null && anim.clips.Count > 0 ? anim.clips[0] : null;
        if (c != null && c.Duracion > 0f) anim.multiplicador = Mathf.Max(1f, c.Duracion / segundos);
        vida = t + segundos;
    }

    // Se apaga con un fundido y se borra.
    public void Desvanecer(float segundos)
    {
        desvanecer = Mathf.Max(0.01f, segundos);
        vida = t + segundos;
    }

    private void Update()
    {
        t += Time.deltaTime;
        if (desvanecer > 0f && sr != null)
        {
            Color c = sr.color;
            c.a = Mathf.Clamp01((vida - t) / desvanecer) * c.a;
            sr.color = c;
        }
        if (vida > 0f && t >= vida) Destroy(gameObject);
        else if (vida <= 0f && !anim.enabled) Destroy(gameObject);
    }

    // Sin luces: los hechizos brillan igual en la cueva oscura.
    private static Material sinLuz;
    public static Material MaterialSinLuz()
    {
        if (sinLuz != null) return sinLuz;
        Shader s = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (s == null) s = Shader.Find("Sprites/Default");
        sinLuz = new Material(s);
        return sinLuz;
    }
}
