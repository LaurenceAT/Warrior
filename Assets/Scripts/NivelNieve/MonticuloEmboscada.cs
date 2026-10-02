using System.Collections;
using UnityEngine;

// Emboscada: el enemigo espera escondido bajo un montículo de nieve. Al
// acercarse el player, el montículo avisa (tiembla, suelta nieve y cruje) y un
// momento después el enemigo sale de golpe, ya en alerta. Siempre se puede ver
// venir: el aviso dura lo bastante para retroceder o prepararse.
// Va en el propio enemigo; el montículo es un hijo ("Monticulo").
public class MonticuloEmboscada : MonoBehaviour
{
    [Tooltip("Distancia en horizontal a la que se activa.")]
    public float radio = 3.6f;
    [Tooltip("Diferencia de altura maxima con el player para activarse.")]
    public float alturaMaxima = 2.5f;
    [Tooltip("Segundos de aviso (tiembla y suelta nieve) antes de salir.")]
    public float aviso = 0.9f;
    public SpriteRenderer monticulo;
    public Color colorNieve = new Color(0.92f, 0.96f, 1f);

    // Depuracion: false = los enemigos estan a la vista desde el principio.
    public static bool Activas = true;

    public bool Escondido { get; private set; }
    public bool Avisando { get; private set; }

    private EnemigoBase enemigo;
    private Rigidbody2D rb;
    private Renderer[] renderers;
    private Collider2D[] colliders;
    private Canvas[] lienzos;
    private Transform player;
    private Vector3 basePos;
    private float siguientePolvo;

    private void Awake()
    {
        enemigo = GetComponent<EnemigoBase>();
        rb = GetComponent<Rigidbody2D>();
        if (monticulo == null)
        {
            Transform m = transform.Find("Monticulo");
            if (m != null) monticulo = m.GetComponent<SpriteRenderer>();
        }
        if (monticulo != null) basePos = monticulo.transform.localPosition;
        if (!Activas) { if (monticulo != null) monticulo.gameObject.SetActive(false); return; }
        Esconder();
    }

    private void Esconder()
    {
        Escondido = true;
        renderers = GetComponentsInChildren<Renderer>(true);
        colliders = GetComponentsInChildren<Collider2D>(true);
        lienzos = GetComponentsInChildren<Canvas>(true);
        foreach (Renderer r in renderers) if (monticulo == null || r != monticulo) r.enabled = false;
        foreach (Collider2D c in colliders) c.enabled = false;
        foreach (Canvas c in lienzos) c.enabled = false;
        if (rb != null) rb.simulated = false;
        if (enemigo != null) enemigo.enabled = false;
    }

    private void Update()
    {
        if (!Escondido || Avisando) return;
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p == null) return;
            player = p.transform;
        }
        // Un poco de nieve que se mueve de vez en cuando: algo respira debajo.
        if (Time.time >= siguientePolvo && monticulo != null && monticulo.isVisible)
        {
            siguientePolvo = Time.time + Random.Range(2.5f, 4.5f);
            ParticulasFx.Rafaga((Vector2)monticulo.bounds.center + Vector2.up * monticulo.bounds.extents.y * 0.6f, 3, colorNieve, Color.white,
                                new Vector2(0.3f, 1f), 0.8f, new Vector2(0.03f, 0.06f), new Vector2(0.3f, 0.6f));
        }
        Vector2 d = player.position - transform.position;
        if (Mathf.Abs(d.x) < radio && Mathf.Abs(d.y) < alturaMaxima) StartCoroutine(Salir());
    }

    private IEnumerator Salir()
    {
        Avisando = true;
        Sonido.Reproducir("cueva_crujido", 0.6f, 1.2f);
        for (float t = 0f; t < aviso; t += Time.deltaTime)
        {
            float k = t / aviso;
            if (monticulo != null)
                monticulo.transform.localPosition = basePos + (Vector3)(Random.insideUnitCircle * (0.03f + 0.07f * k));
            if (Random.value < 0.25f && monticulo != null)
                ParticulasFx.Rafaga((Vector2)monticulo.bounds.center + Vector2.up * monticulo.bounds.extents.y * 0.5f, 2, colorNieve, Color.white,
                                    new Vector2(0.5f, 2f), 1f, new Vector2(0.03f, 0.07f), new Vector2(0.3f, 0.6f));
            yield return null;
        }
        Mostrar();
    }

    private void Mostrar()
    {
        Escondido = false;
        Avisando = false;
        foreach (Renderer r in renderers) if (r != null) r.enabled = true;
        foreach (Collider2D c in colliders) if (c != null) c.enabled = true;
        foreach (Canvas c in lienzos) if (c != null) c.enabled = true;
        if (rb != null) rb.simulated = true;
        if (enemigo != null) enemigo.enabled = true;
        Sonido.Reproducir("hielo_romper", 0.5f, 1.3f);
        CamaraDinamica.Sacudir(0.15f);
        if (monticulo != null)
        {
            Vector2 c = monticulo.bounds.center;
            ParticulasFx.Rafaga(c, 18, colorNieve, Color.white, new Vector2(1.5f, 4f), 1.5f, new Vector2(0.04f, 0.1f), new Vector2(0.4f, 0.8f));
            monticulo.gameObject.SetActive(false);
        }
        if (rb != null) rb.linearVelocity = new Vector2(0f, 4f);
        if (enemigo != null) StartCoroutine(AlertarLuego());
    }

    private IEnumerator AlertarLuego()
    {
        yield return null;
        if (enemigo != null) enemigo.Alertar(0f);
    }

    // Depuracion: sale ya.
    public void ForzarSalida() { if (Escondido && !Avisando) StartCoroutine(Salir()); }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.8f, 0.9f, 1f, 0.6f);
        Gizmos.DrawWireCube(transform.position + Vector3.up * 0.5f, new Vector3(radio * 2f, alturaMaxima * 2f, 0f));
#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * (alturaMaxima + 0.3f), "Emboscada");
#endif
    }
}
