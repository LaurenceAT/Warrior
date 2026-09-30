using System.Collections;
using UnityEngine;

// Bola magica del mago. Vuela recta y:
//   - Si da al player, o este la bloquea, explota.
//   - Si el player la atraviesa con el barrido (invulnerable), sigue de largo.
//   - Si le hace parry, sale rebotada hacia quien la lanzo, mas rapida, y ahora
//     hiere a los enemigos. Devolverle su bola al mago es la forma elegante de
//     tumbarlo.
// Contra la roca explota. La roca se mira con un rayo porque la capa del
// proyectil (Traps) no choca con Ground.
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class ProyectilMagico : MonoBehaviour
{
    [SerializeField] private AnimadorHoja anim;
    [SerializeField] private float velocidad = 7f;
    [SerializeField] private int dano = 20;
    [SerializeField] private int danoDevuelto = 30;
    [SerializeField] private float multiplicadorDevuelto = 1.7f;
    [SerializeField] private float vida = 5f;
    [SerializeField] private Color colorDevuelto = new Color(1f, 0.85f, 0.4f, 1f);
    [SerializeField] private LayerMask capaSuelo;

    private Rigidbody2D rb;
    private Collider2D col;
    private Vector2 direccion = Vector2.right;
    private Transform lanzador;
    private bool devuelto;
    private bool explotado;
    private float tVida;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        col = GetComponent<Collider2D>();
        col.isTrigger = true;
        if (anim == null) anim = GetComponentInChildren<AnimadorHoja>();
        if (capaSuelo.value == 0) capaSuelo = LayerMask.GetMask("Ground");
    }

    // Dano al player (lo pone quien la lanza, segun su ficha; -1 = el del prefab).
    public void Lanzar(Vector2 dir, Transform quien, int danoAlPlayer)
    {
        if (danoAlPlayer > 0) dano = danoAlPlayer;
        Lanzar(dir, quien);
    }

    public void Lanzar(Vector2 dir, Transform quien)
    {
        direccion = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.right;
        lanzador = quien;
        Orientar();
        if (anim != null) anim.Reproducir("vuelo", true);
    }

    private void FixedUpdate()
    {
        if (explotado) return;

        tVida += Time.fixedDeltaTime;
        if (tVida >= vida) { Explotar(); return; }

        float paso = velocidad * Time.fixedDeltaTime;
        RaycastHit2D roca = Physics2D.Raycast(rb.position, direccion, paso + 0.15f, capaSuelo);
        if (roca)
        {
            rb.MovePosition(roca.point - direccion * 0.1f);
            Explotar();
            return;
        }
        rb.MovePosition(rb.position + direccion * paso);
    }

    private void OnTriggerEnter2D(Collider2D otro)
    {
        if (explotado) return;

        if (!devuelto && otro.CompareTag("Player"))
        {
            PlayerControler p = otro.GetComponent<PlayerControler>();
            if (p == null) return;

            switch (p.TakeDamage(dano, this))
            {
                case PlayerControler.ResultadoDano.Parry: Devolver(); break;
                case PlayerControler.ResultadoDano.Ignorado: break; // la atraviesa
                default: Explotar(); break;
            }
            return;
        }

        if (devuelto && otro.CompareTag("Enemy"))
        {
            EnemyHealth e = otro.GetComponent<EnemyHealth>();
            if (e == null) return;
            e.TakeDamage(danoDevuelto, transform.position, 1.5f);
            Explotar();
        }
    }

    private void Devolver()
    {
        devuelto = true;
        tVida = 0f;
        velocidad *= multiplicadorDevuelto;

        // Hacia el pecho de quien la lanzo, si sigue vivo; si no, de vuelta.
        Vector2 destino = lanzador != null ? (Vector2)lanzador.position + Vector2.up * 0.9f : rb.position - direccion;
        direccion = (destino - rb.position).normalized;
        Orientar();

        foreach (SpriteRenderer sr in GetComponentsInChildren<SpriteRenderer>()) sr.color = colorDevuelto;
    }

    private void Orientar()
    {
        float angulo = Mathf.Atan2(direccion.y, direccion.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angulo);
    }

    public void Explotar(float escala = 1f)
    {
        if (explotado) return;
        explotado = true;
        col.enabled = false;
        transform.rotation = Quaternion.identity;
        transform.localScale *= escala;
        StartCoroutine(Explosion());
    }

    private IEnumerator Explosion()
    {
        if (anim != null && anim.Tiene("explosion"))
        {
            anim.Reproducir("explosion", true);
            float t = 0f;
            while (!anim.Terminado && t < 2f) { t += Time.deltaTime; yield return null; }
        }
        Destroy(gameObject);
    }
}
