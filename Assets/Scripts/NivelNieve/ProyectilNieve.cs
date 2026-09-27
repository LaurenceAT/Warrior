using System.Collections.Generic;
using UnityEngine;

// Proyectil de los enemigos del nivel nevado (flechas, escupitajos, lanzas de
// hielo...), montado por codigo. Vuela con su velocidad (y gravedad si la
// tiene) y:
//   - Si da al player o este lo bloquea, se deshace.
//   - Si el player lo esquiva con invulnerabilidad, sigue de largo.
//   - Si le hace parry, sale rebotado y ahora hiere a los enemigos.
// Contra el suelo o una pared se rompe (y puede dejar escarcha).
public class ProyectilNieve : MonoBehaviour
{
    public class Datos
    {
        public AnimadorHoja.Clip clip;
        public Sprite sprite;
        public AnimadorHoja.Clip impacto;
        public Color color = Color.white;
        public Color colorImpacto = Color.white;
        public float escala = 1f;
        public float escalaImpacto = 1f;
        public float gravedad;
        public int dano = 14;
        public bool magico = true;
        public float radio = 0.2f;
        public float vida = 5f;
        public bool rotar = true;
        // Al romperse contra el suelo deja escarcha de este ancho (0 = no).
        public float escarcha;
        public AnimadorHoja.Clip clipEscarcha;
        public string sonidoImpacto;
        public bool registrarPeligro;
    }

    private Datos d;
    private Vector2 velocidad;
    private Transform lanzador;
    private bool devuelto, roto;
    private float t;
    private int capaSuelo;
    private SpriteRenderer sr;

    public static ProyectilNieve Lanzar(Datos datos, Vector2 pos, Vector2 velocidad, Transform lanzador)
    {
        GameObject go = new GameObject("Proyectil");
        go.layer = LayerMask.NameToLayer("Traps");
        go.transform.position = pos;
        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        CircleCollider2D c = go.AddComponent<CircleCollider2D>();
        c.isTrigger = true;
        c.radius = datos.radio / Mathf.Max(0.01f, datos.escala);

        ProyectilNieve p = go.AddComponent<ProyectilNieve>();
        p.d = datos;
        p.velocidad = velocidad;
        p.lanzador = lanzador;
        p.capaSuelo = LayerMask.GetMask("Ground");
        go.transform.localScale = Vector3.one * datos.escala;

        p.sr = go.AddComponent<SpriteRenderer>();
        p.sr.sortingLayerName = "VFX";
        p.sr.sortingOrder = 12;
        p.sr.color = datos.color;
        p.sr.sharedMaterial = EfectoVisual.MaterialSinLuz();
        if (datos.clip != null)
        {
            AnimadorHoja a = go.AddComponent<AnimadorHoja>();
            a.destino = p.sr;
            a.clips = new List<AnimadorHoja.Clip> { datos.clip };
            a.Preparar();
            a.Reproducir(datos.clip.nombre, true);
        }
        else p.sr.sprite = datos.sprite;
        p.Orientar();
        if (datos.registrarPeligro) PeligrosJefe.Registrar(go);
        return p;
    }

    private void Update()
    {
        if (roto) return;
        t += Time.deltaTime;
        if (t >= d.vida) { Romper(false); return; }

        velocidad += Vector2.down * d.gravedad * Time.deltaTime;
        Vector2 pos = transform.position;
        Vector2 paso = velocidad * Time.deltaTime;
        RaycastHit2D roca = Physics2D.Raycast(pos, paso.normalized, paso.magnitude + 0.05f, capaSuelo);
        if (roca)
        {
            transform.position = roca.point - paso.normalized * 0.05f;
            Romper(true, roca.normal.y > 0.6f ? roca.point : (Vector2?)null);
            return;
        }
        transform.position = pos + paso;
        Orientar();
    }

    private void Orientar()
    {
        if (!d.rotar || velocidad.sqrMagnitude < 0.001f) return;
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(velocidad.y, velocidad.x) * Mathf.Rad2Deg);
    }

    private void OnTriggerEnter2D(Collider2D otro)
    {
        if (roto) return;
        if (!devuelto && otro.CompareTag("Player"))
        {
            PlayerControler p = otro.GetComponent<PlayerControler>();
            if (p == null) return;
            if (d.magico) PlayerControler.SiguienteGolpeMagico = true;
            else PlayerControler.SiguienteGolpeFisico = true;
            switch (p.TakeDamage(d.dano, this))
            {
                case PlayerControler.ResultadoDano.Parry: Devolver(); break;
                case PlayerControler.ResultadoDano.Ignorado: break;
                default: Romper(true); break;
            }
            return;
        }
        if (devuelto && otro.CompareTag("Enemy"))
        {
            EnemyHealth e = otro.GetComponent<EnemyHealth>();
            if (e == null) return;
            e.TakeDamage(Mathf.RoundToInt(d.dano * 1.5f), transform.position, 1.2f);
            Romper(true);
        }
    }

    private void Devolver()
    {
        devuelto = true;
        t = 0f;
        Vector2 destino = lanzador != null ? (Vector2)lanzador.position + Vector2.up * 0.8f : (Vector2)transform.position - velocidad;
        float rapidez = Mathf.Max(velocidad.magnitude * 1.6f, 9f);
        velocidad = (destino - (Vector2)transform.position).normalized * rapidez;
        d.gravedad = 0f;
        sr.color = new Color(1f, 0.9f, 0.5f, 1f);
        Orientar();
    }

    private void Romper(bool conImpacto, Vector2? suelo = null)
    {
        roto = true;
        if (!string.IsNullOrEmpty(d.sonidoImpacto) && conImpacto) Sonido.Reproducir(d.sonidoImpacto, 0.7f);
        if (conImpacto && d.impacto != null)
            EfectoVisual.Crear(d.impacto, transform.position, d.escalaImpacto, d.colorImpacto);
        else if (conImpacto)
            ParticulasFx.Rafaga(transform.position, 8, d.color, Color.white, new Vector2(1f, 2.5f), 0.5f,
                                new Vector2(0.04f, 0.08f), new Vector2(0.2f, 0.4f));
        if (suelo.HasValue && d.escarcha > 0f)
            ZonaEscarcha.Crear(suelo.Value, d.escarcha, 5f, 0.55f, d.clipEscarcha);
        Destroy(gameObject);
    }
}
