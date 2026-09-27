using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Muro de hielo que cierra el paso. Solo el fuego lo derrite: con la espada
// imbuida en fuego, unos cuantos tajos lo agrietan y lo rompen. Con otro
// elemento (o sin imbuir) la espada rebota y un aviso da la pista.
public class MuroHielo : MonoBehaviour, IGolpeable
{
    [SerializeField] private int golpesFuego = 3;
    [SerializeField] private Collider2D solido;
    [SerializeField] private SpriteRenderer[] visuales;

    private int golpes;
    private bool roto;
    private float siguientePista;
    private float siguienteGolpe;
    private Vector3[] posiciones;

    public void Configurar(Collider2D solido, SpriteRenderer[] visuales)
    {
        this.solido = solido;
        this.visuales = visuales;
    }

    private void Awake()
    {
        if (visuales == null) visuales = new SpriteRenderer[0];
        posiciones = new Vector3[visuales.Length];
        for (int i = 0; i < visuales.Length; i++) if (visuales[i] != null) posiciones[i] = visuales[i].transform.localPosition;
    }

    public void Golpear(Elemento elemento, Vector2 punto)
    {
        if (roto || Time.time < siguienteGolpe) return;
        siguienteGolpe = Time.time + 0.15f;

        if (elemento != Elemento.Fuego)
        {
            Sonido.Reproducir("hielo_rebote", 0.8f);
            ParticulasFx.Rafaga(punto, 6, Color.white, new Color(0.7f, 0.9f, 1f), new Vector2(1f, 3f), 1f,
                                new Vector2(0.04f, 0.08f), new Vector2(0.2f, 0.4f));
            if (Time.time >= siguientePista)
            {
                siguientePista = Time.time + 4f;
                string texto = elemento == Elemento.Hielo ? "El hielo se hace más fuerte..." : "El hielo es demasiado duro. Quizá el fuego...";
                TextoFlotante.Mostrar(texto, punto + Vector2.up * 1.2f, new Color(0.75f, 0.9f, 1f), 0.8f);
            }
            StartCoroutine(Temblar(0.12f, 0.03f));
            return;
        }

        golpes++;
        Sonido.Reproducir("hielo_derretir");
        ParticulasFx.Rafaga(punto, 14, new Color(1f, 0.6f, 0.2f), new Color(0.8f, 0.95f, 1f), new Vector2(1f, 3.5f), -0.3f,
                            new Vector2(0.05f, 0.1f), new Vector2(0.3f, 0.7f));
        // Cada golpe de fuego lo deja mas transparente y agrietado.
        float k = 1f - (float)golpes / golpesFuego;
        foreach (SpriteRenderer s in visuales)
            if (s != null) s.color = Color.Lerp(new Color(1f, 0.75f, 0.6f, 0.7f), Color.white, k);
        StartCoroutine(Temblar(0.2f, 0.06f));
        if (golpes >= golpesFuego) StartCoroutine(Romper());
    }

    private IEnumerator Temblar(float dur, float fuerza)
    {
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            for (int i = 0; i < visuales.Length; i++)
                if (visuales[i] != null) visuales[i].transform.localPosition = posiciones[i] + (Vector3)(Random.insideUnitCircle * fuerza);
            yield return null;
        }
        for (int i = 0; i < visuales.Length; i++) if (visuales[i] != null) visuales[i].transform.localPosition = posiciones[i];
    }

    private IEnumerator Romper()
    {
        roto = true;
        Sonido.Reproducir("hielo_romper");
        CamaraDinamica.Sacudir(0.5f);
        if (solido != null) solido.enabled = false;
        foreach (Collider2D c in GetComponents<Collider2D>()) c.enabled = false;
        Bounds b = new Bounds(transform.position, Vector3.one);
        foreach (SpriteRenderer s in visuales) if (s != null) b.Encapsulate(s.bounds);
        for (int i = 0; i < 5; i++)
            ParticulasFx.Rafaga(new Vector2(b.center.x, Mathf.Lerp(b.min.y, b.max.y, i / 4f)), 18,
                                Color.white, new Color(0.6f, 0.85f, 1f), new Vector2(2f, 6f), 2f, new Vector2(0.06f, 0.16f), new Vector2(0.5f, 1.1f));
        TextoFlotante.Mostrar("¡El muro se derrite!", (Vector2)b.center + Vector2.up * (b.extents.y + 0.3f), new Color(1f, 0.75f, 0.45f), 1f);
        for (float t = 0f; t < 0.5f; t += Time.deltaTime)
        {
            foreach (SpriteRenderer s in visuales)
                if (s != null) { Color c = s.color; c.a = 1f - t / 0.5f; s.color = c; }
            yield return null;
        }
        gameObject.SetActive(false);
    }
}
