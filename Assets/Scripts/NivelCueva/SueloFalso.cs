using System.Collections;
using UnityEngine;

// Suelo que parece firme y cede. Al pisarlo avisa (tiembla, suelta polvo y
// cruje), cae tras un instante y vuelve a aparecer pasado un rato.
//   - Los de enseñanza llevan a un sitio seguro (la galeria de abajo).
//   - Los que caen a una grieta llevan pista: grietas marcadas y un tono algo
//     distinto, y un brillo tenue en la oscuridad.
[RequireComponent(typeof(BoxCollider2D))]
public class SueloFalso : MonoBehaviour
{
    [Tooltip("Segundos desde que se pisa hasta que cae.")]
    public float retraso = 0.55f;
    [Tooltip("Segundos hasta que vuelve a aparecer.")]
    public float reaparece = 5f;
    [Tooltip("Cuanto tiembla al avisar (unidades).")]
    public float temblor = 0.035f;
    [Tooltip("Con pista: tono distinto y grietas visibles (para los que caen a una grieta).")]
    public bool conPista;
    public Color tonoPista = new Color(0.82f, 0.78f, 0.74f, 1f);
    [Tooltip("Lo que se ve (las piezas de suelo). Se mueve al temblar y al caer.")]
    public Transform visual;
    [Tooltip("Grietas dibujadas encima (solo con pista).")]
    public SpriteRenderer grietas;
    public string sonidoCrujir = "cueva_crujido";
    public string sonidoCaer = "cueva_derrumbe";

    private BoxCollider2D col;
    private SpriteRenderer[] piezas;
    private Color[] colores;
    private Vector3 baseVisual;
    private bool activo;
    private float siguientePolvo;

    public bool Caido { get; private set; }

    private void Awake()
    {
        col = GetComponent<BoxCollider2D>();
        if (visual == null) visual = transform;
        piezas = visual.GetComponentsInChildren<SpriteRenderer>(true);
        colores = new Color[piezas.Length];
        for (int i = 0; i < piezas.Length; i++)
        {
            if (conPista && piezas[i] != grietas) piezas[i].color *= tonoPista;
            colores[i] = piezas[i].color;
        }
        baseVisual = visual.localPosition;
        if (grietas != null) grietas.enabled = conPista;
    }

    private void Update()
    {
        if (activo || Caido) return;
        // Pista: de vez en cuando cae un poco de polvo de las grietas.
        if (conPista && Time.time >= siguientePolvo)
        {
            siguientePolvo = Time.time + Random.Range(2.5f, 4.5f);
            Polvo(3);
        }
        if (PlayerEncima()) StartCoroutine(Ceder());
    }

    // El player esta de pie sobre el (una franja fina justo encima).
    private bool PlayerEncima()
    {
        Bounds b = col.bounds;
        foreach (Collider2D c in Physics2D.OverlapBoxAll(new Vector2(b.center.x, b.max.y + 0.08f), new Vector2(b.size.x - 0.1f, 0.14f), 0f))
            if (c.CompareTag("Player")) return true;
        return false;
    }

    private IEnumerator Ceder()
    {
        activo = true;
        Sonido.Reproducir(sonidoCrujir, 0.6f, Random.Range(0.9f, 1.05f));
        Polvo(8);
        for (float t = 0f; t < retraso; t += Time.deltaTime)
        {
            visual.localPosition = baseVisual + (Vector3)(Random.insideUnitCircle * temblor);
            yield return null;
        }

        // Cae.
        Caido = true;
        col.enabled = false;
        Sonido.Reproducir(sonidoCaer, 0.6f);
        Polvo(14);
        Vector3 desde = baseVisual;
        for (float t = 0f; t < 0.7f; t += Time.deltaTime)
        {
            float u = t / 0.7f;
            visual.localPosition = desde + Vector3.down * (u * u * 3f);
            for (int i = 0; i < piezas.Length; i++)
            {
                Color c = colores[i];
                c.a *= 1f - u;
                piezas[i].color = c;
            }
            yield return null;
        }
        for (int i = 0; i < piezas.Length; i++) piezas[i].enabled = false;

        // Vuelve cuando pase el rato y no haya nadie en el hueco.
        yield return new WaitForSeconds(reaparece);
        while (Physics2D.OverlapBox(col.bounds.center, col.bounds.size, 0f, LayerMask.GetMask("Player")) != null) yield return new WaitForSeconds(0.3f);
        visual.localPosition = baseVisual;
        for (int i = 0; i < piezas.Length; i++) piezas[i].enabled = true;
        col.enabled = true;
        for (float t = 0f; t < 0.4f; t += Time.deltaTime)
        {
            for (int i = 0; i < piezas.Length; i++)
            {
                Color c = colores[i];
                c.a *= t / 0.4f;
                piezas[i].color = c;
            }
            yield return null;
        }
        for (int i = 0; i < piezas.Length; i++) piezas[i].color = colores[i];
        Caido = false;
        activo = false;
    }

    private void Polvo(int n)
    {
        Bounds b = col.bounds;
        for (int i = 0; i < n; i++)
        {
            Vector2 p = new Vector2(Random.Range(b.min.x, b.max.x), b.min.y + 0.05f);
            ParticulasFx.Rafaga(p, 1, new Color(0.55f, 0.5f, 0.48f, 0.8f), new Color(0.35f, 0.32f, 0.3f, 0.6f),
                                new Vector2(0.1f, 0.5f), 1.5f, new Vector2(0.03f, 0.06f), new Vector2(0.4f, 0.8f), 60f, -90f);
        }
    }

    private void OnDrawGizmos()
    {
        BoxCollider2D c = GetComponent<BoxCollider2D>();
        if (c == null) return;
        Gizmos.color = conPista ? new Color(1f, 0.5f, 0.1f, 0.5f) : new Color(1f, 0.9f, 0.2f, 0.5f);
        Gizmos.DrawWireCube(transform.TransformPoint(c.offset), c.size);
    }
}
