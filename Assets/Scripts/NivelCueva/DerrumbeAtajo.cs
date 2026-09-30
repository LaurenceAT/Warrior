using System.Collections;
using UnityEngine;

// Atajo: un derrumbe de rocas que corta un paso. Solo cede a los espadazos
// dados desde un lado (el del final del atajo); desde el otro, la roca no se
// mueve y sale un aviso. Abierto, se queda abierto en la partida.
[RequireComponent(typeof(BoxCollider2D))]
public class DerrumbeAtajo : MonoBehaviour, IGolpeable
{
    [Tooltip("Clave unica (para recordar que se abrio).")]
    public string clave = "derrumbe";
    [Tooltip("Lado desde el que se puede abrir: 1 = golpeando desde la derecha, -1 = desde la izquierda.")]
    public int ladoQueAbre = 1;
    [Tooltip("Golpes que aguanta.")]
    public int golpes = 3;
    public Transform visual;
    public string sonidoGolpe = "cueva_crujido";
    public string sonidoAbrir = "cueva_derrumbe";

    private int recibidos;
    private float siguienteAviso;
    private string Bandera => "atajo_" + gameObject.scene.name + "_" + clave;

    private void Start()
    {
        if (Partida.Bandera(Bandera)) gameObject.SetActive(false);
    }

    public void Golpear(Elemento elemento, Vector2 punto)
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        float lado = p != null ? Mathf.Sign(p.transform.position.x - transform.position.x) : 0f;
        if (lado != ladoQueAbre)
        {
            Sonido.Reproducir(sonidoGolpe, 0.35f, 1.3f);
            if (Time.time >= siguienteAviso)
            {
                siguienteAviso = Time.time + 1.5f;
                TextoFlotante.Mostrar("No cede desde este lado", (Vector2)transform.position + Vector2.up * 1.6f, new Color(0.8f, 0.78f, 0.75f), 0.75f);
            }
            return;
        }
        recibidos++;
        Sonido.Reproducir(sonidoGolpe, 0.6f, Random.Range(0.85f, 1f));
        ParticulasFx.Rafaga(punto, 6, new Color(0.5f, 0.46f, 0.44f), new Color(0.3f, 0.28f, 0.27f),
                            new Vector2(1f, 3f), 2f, new Vector2(0.05f, 0.1f), new Vector2(0.3f, 0.6f));
        if (visual != null) StartCoroutine(Sacudir());
        if (recibidos >= golpes) StartCoroutine(Abrir());
    }

    private IEnumerator Sacudir()
    {
        Vector3 b = visual.localPosition;
        for (float t = 0f; t < 0.15f; t += Time.deltaTime) { visual.localPosition = b + (Vector3)(Random.insideUnitCircle * 0.05f); yield return null; }
        visual.localPosition = b;
    }

    private IEnumerator Abrir()
    {
        Partida.PonerBandera(Bandera);
        Sonido.Reproducir(sonidoAbrir, 0.8f);
        Sonido.Reproducir("secreto_descubierto", 0.5f);
        CamaraDinamica.Sacudir(0.25f);
        GetComponent<Collider2D>().enabled = false;
        Bounds b = GetComponent<Collider2D>().bounds;
        for (int i = 0; i < 20; i++)
            ParticulasFx.Rafaga(new Vector2(Random.Range(b.min.x, b.max.x), Random.Range(b.min.y, b.max.y)), 1,
                                new Color(0.5f, 0.46f, 0.44f), new Color(0.3f, 0.28f, 0.27f), new Vector2(1f, 3f), 2.5f,
                                new Vector2(0.06f, 0.12f), new Vector2(0.4f, 0.8f));
        TextoFlotante.Mostrar("Atajo abierto", (Vector2)transform.position + Vector2.up * 1.6f, new Color(0.95f, 0.85f, 0.6f), 0.9f);
        SpriteRenderer[] srs = GetComponentsInChildren<SpriteRenderer>();
        for (float t = 0f; t < 0.6f; t += Time.deltaTime)
        {
            foreach (SpriteRenderer s in srs) { Color c = s.color; c.a = 1f - t / 0.6f; s.color = c; }
            if (visual != null) visual.localPosition += Vector3.down * Time.deltaTime * 1.5f;
            yield return null;
        }
        gameObject.SetActive(false);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.4f, 1f, 0.5f, 0.5f);
        BoxCollider2D c = GetComponent<BoxCollider2D>();
        if (c != null) Gizmos.DrawWireCube(transform.TransformPoint(c.offset), c.size);
    }
}
