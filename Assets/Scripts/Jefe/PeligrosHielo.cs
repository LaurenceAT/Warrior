using System.Collections;
using UnityEngine;

// Estaca de hielo que brota del suelo (fase 2 del jefe de la nieve). Primero una
// marca que late en el suelo (aviso) y luego las estacas: hieren y dejan escarcha.
public class EstacaHielo : MonoBehaviour
{
    public static void Invocar(AnimadorHoja.Clip marca, AnimadorHoja.Clip estaca, Vector2 suelo, float aviso, int dano,
                               Component atacante, AnimadorHoja.Clip clipEscarcha, float escala = 1f)
    {
        GameObject go = new GameObject("EstacaHielo");
        go.transform.position = suelo;
        EstacaHielo e = go.AddComponent<EstacaHielo>();
        PeligrosJefe.Registrar(go);
        e.StartCoroutine(e.Rutina(marca, estaca, suelo, aviso, dano, atacante, clipEscarcha, escala));
    }

    private IEnumerator Rutina(AnimadorHoja.Clip marca, AnimadorHoja.Clip estaca, Vector2 suelo, float aviso, int dano,
                               Component atacante, AnimadorHoja.Clip clipEscarcha, float escala)
    {
        // Marca: el clip que se pase o, sin clip, un ovalo palido.
        SpriteRenderer m;
        EfectoVisual ev = marca != null ? EfectoVisual.Crear(marca, suelo + Vector2.up * 0.05f, 1f, new Color(0.6f, 0.9f, 1f, 0.8f), false, -1f, "VFX", 3) : null;
        if (ev != null) { m = ev.Render; ev.transform.localScale = new Vector3(1.2f * escala, 0.35f, 1f); }
        else
        {
            m = new GameObject("Marca").AddComponent<SpriteRenderer>();
            m.sprite = EnemigoMago.CirculoSprite();
            m.sortingLayerName = "VFX";
            m.sortingOrder = 3;
            m.sharedMaterial = EfectoVisual.MaterialSinLuz();
            m.transform.position = suelo + Vector2.up * 0.05f;
            m.transform.localScale = new Vector3(1.3f * escala, 0.33f * escala, 1f);
            m.color = new Color(0.7f, 0.93f, 1f, 0.8f);
        }
        m.transform.SetParent(transform, true);
        for (float t = 0f; t < aviso; t += Time.deltaTime)
        {
            Color c = m.color; c.a = 0.4f + 0.5f * Mathf.Abs(Mathf.Sin(t * 14f)); m.color = c;
            yield return null;
        }
        Destroy(m.gameObject);

        Sonido.Reproducir("hielo_estaca", 0.6f);
        EfectoVisual e = EfectoVisual.Crear(estaca, suelo + Vector2.up * 0.05f, 1.2f * escala, Color.white, Random.value < 0.5f, -1f, "VFX", 11);
        if (e != null) e.transform.SetParent(transform, true);
        PlayerControler.SiguienteGolpeMagico = true;
        var r = PeligrosJefe.GolpearCaja(suelo + new Vector2(0f, 0.9f * escala), new Vector2(1.3f * escala, 1.8f * escala), dano, atacante, out _);
        if (!r.HasValue) PlayerControler.SiguienteGolpeMagico = false;
        ZonaEscarcha.Crear(suelo, 1.4f * escala, 6f, 0.55f, null);
        yield return new WaitForSeconds(estaca != null ? estaca.Duracion + 0.1f : 0.6f);
        Destroy(gameObject);
    }
}

// Deja escarcha en el suelo por donde pasa lo que la lleva (una onda, una
// medialuna...). Solo si va a ras de suelo.
public class RastroEscarcha : MonoBehaviour
{
    public float cada = 1.3f;
    public float vida = 5f;
    private Vector2 ultimo;
    private bool primero = true;

    private void Update()
    {
        Vector2 p = transform.position;
        if (!primero && Vector2.Distance(p, ultimo) < cada) return;
        primero = false;
        ultimo = p;
        RaycastHit2D suelo = Physics2D.Raycast(p + Vector2.up * 0.3f, Vector2.down, 1.2f, LayerMask.GetMask("Ground"));
        if (suelo) ZonaEscarcha.Crear(suelo.point, cada + 0.1f, vida);
    }
}
