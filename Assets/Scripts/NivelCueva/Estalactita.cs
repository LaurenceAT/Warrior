using System.Collections;
using UnityEngine;

// Estalactita que se suelta del techo cuando el player pasa por debajo. Tiembla
// un instante antes de caer (el aviso), cae acelerando, hiere a lo que toque y se
// rompe contra el suelo. Vuelve a colgar al rato, y al descansar en la hoguera.
//
// Soltar() la tira desde fuera: la usan tambien los enemigos (el volador la
// desprende al chocar contra el techo en su picado).
[RequireComponent(typeof(SpriteRenderer))]
public class Estalactita : MonoBehaviour
{
    private enum Estado { Colgada, Temblando, Cayendo, Rota }

    [Header("Deteccion")]
    // Mitad del ancho de la franja de debajo en la que se suelta.
    [SerializeField] private float anchoDeteccion = 0.8f;
    [SerializeField] private float alcanceDeteccion = 9f;
    [SerializeField] private LayerMask capaSuelo;

    [Header("Caida")]
    [SerializeField] private float temblor = 0.45f;
    [SerializeField] private float amplitudTemblor = 0.05f;
    [SerializeField] private float gravedad = 30f;
    [SerializeField] private float velocidadMaxima = 22f;

    [Header("Dano")]
    [SerializeField] private int dano = 20;
    [SerializeField] private int danoEnemigos = 25;

    [Header("Reaparicion")]
    // Segundos hasta volver a colgar tras romperse. 0 = no vuelve (salvo al descansar).
    [SerializeField] private float reaparece = 6f;

    private SpriteRenderer sr;
    private Collider2D col;
    private Rigidbody2D rb;
    private Estado estado;
    private Vector3 origen;
    private float velocidad;
    private Transform player;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        col = GetComponent<Collider2D>();
        rb = GetComponent<Rigidbody2D>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        if (col != null) col.isTrigger = true;
        if (capaSuelo.value == 0) capaSuelo = LayerMask.GetMask("Ground");
        origen = transform.position;
    }

    private void OnEnable()
    {
        Hoguera.AlDescansar += Recolgar;
    }

    private void OnDisable()
    {
        Hoguera.AlDescansar -= Recolgar;
    }

    private void Update()
    {
        if (estado != Estado.Colgada) return;

        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p == null) return;
            player = p.transform;
        }

        Vector2 punta = Punta();
        Vector2 pp = player.position;
        if (Mathf.Abs(pp.x - punta.x) > anchoDeteccion) return;
        if (pp.y > punta.y || punta.y - pp.y > alcanceDeteccion) return;
        // Que no haya roca en medio (por ejemplo, el player en el piso de abajo).
        if (Physics2D.Linecast(punta + Vector2.down * 0.05f, pp, capaSuelo)) return;

        Soltar();
    }

    public void Soltar()
    {
        if (estado != Estado.Colgada) return;
        StartCoroutine(Caer());
    }

    private IEnumerator Caer()
    {
        // Aviso: tiembla en el sitio.
        estado = Estado.Temblando;
        float t = 0f;
        while (t < temblor)
        {
            t += Time.deltaTime;
            transform.position = origen + new Vector3(Random.Range(-1f, 1f) * amplitudTemblor, 0f, 0f);
            yield return null;
        }
        transform.position = origen;

        // Pequena lluvia de polvo al desprenderse.
        ParticulasFx.Rafaga(Punta() + Vector2.up * sr.bounds.size.y, 6, new Color(0.45f, 0.38f, 0.33f), new Color(0.3f, 0.25f, 0.22f),
                            new Vector2(0.3f, 1f), 1f, new Vector2(0.04f, 0.08f), new Vector2(0.3f, 0.6f), 60f, -90f);

        estado = Estado.Cayendo;
        velocidad = 0f;
        while (estado == Estado.Cayendo)
        {
            float dt = Time.fixedDeltaTime;
            velocidad = Mathf.Min(velocidad + gravedad * dt, velocidadMaxima);
            float paso = velocidad * dt;

            // Mira si la punta llega al suelo en este paso.
            RaycastHit2D suelo = Physics2D.Raycast(Punta(), Vector2.down, paso, capaSuelo);
            if (suelo)
            {
                rb.MovePosition(rb.position + Vector2.down * suelo.distance);
                Romper(suelo.point);
                yield break;
            }

            rb.MovePosition(rb.position + Vector2.down * paso);
            yield return new WaitForFixedUpdate();
        }
    }

    private void OnTriggerEnter2D(Collider2D otro)
    {
        if (estado != Estado.Cayendo) return;

        if (otro.CompareTag("Player"))
        {
            PlayerControler p = otro.GetComponent<PlayerControler>();
            if (p != null) p.TakeDamage(dano);
            Romper(Punta());
        }
        else if (otro.CompareTag("Enemy"))
        {
            EnemyHealth e = otro.GetComponent<EnemyHealth>();
            if (e != null) e.TakeDamage(danoEnemigos, transform.position);
            Romper(Punta());
        }
    }

    private void Romper(Vector2 punto)
    {
        if (estado == Estado.Rota) return;
        estado = Estado.Rota;
        StopAllCoroutines();

        ParticulasFx.Rafaga(punto, 14, new Color(0.5f, 0.42f, 0.36f), new Color(0.28f, 0.22f, 0.2f),
                            new Vector2(2f, 5f), 2.5f, new Vector2(0.07f, 0.16f), new Vector2(0.35f, 0.7f), 140f, 90f);

        sr.enabled = false;
        if (col != null) col.enabled = false;
        if (reaparece > 0f) StartCoroutine(RecolgarLuego());
    }

    private IEnumerator RecolgarLuego()
    {
        yield return new WaitForSeconds(reaparece);
        Recolgar();
    }

    // Vuelve a su sitio, apareciendo con un fundido.
    private void Recolgar()
    {
        if (this == null) return;
        StopAllCoroutines();
        transform.position = origen;
        rb.position = origen;
        estado = Estado.Colgada;
        sr.enabled = true;
        if (col != null) col.enabled = true;
        StartCoroutine(Aparecer());
    }

    private IEnumerator Aparecer()
    {
        Color c = sr.color;
        for (float t = 0f; t < 0.5f; t += Time.deltaTime)
        {
            c.a = t / 0.5f;
            sr.color = c;
            yield return null;
        }
        c.a = 1f;
        sr.color = c;
    }

    private Vector2 Punta()
    {
        return new Vector2(sr.bounds.center.x, sr.bounds.min.y);
    }

    private void OnDrawGizmosSelected()
    {
        SpriteRenderer s = GetComponent<SpriteRenderer>();
        if (s == null) return;
        Vector2 punta = new Vector2(s.bounds.center.x, s.bounds.min.y);
        Gizmos.color = new Color(1f, 0.6f, 0.2f);
        Gizmos.DrawWireCube(punta + Vector2.down * alcanceDeteccion * 0.5f, new Vector3(anchoDeteccion * 2f, alcanceDeteccion, 0f));
    }
}
