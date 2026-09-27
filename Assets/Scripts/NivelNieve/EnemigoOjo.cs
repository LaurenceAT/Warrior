using System.Collections;
using UnityEngine;

// Ojo vigia: volador que ataca a distancia y castiga si te acercas.
//   - Se mantiene lejos y alto, apartandose si el player se acerca.
//   - Escupitajo: uno apuntado o una rafaga de tres en abanico (parry lo devuelve).
//   - Embestida giratoria: si el player esta cerca, gira (aviso) y embiste.
// Debil al acido; resiste el fuego.
public class EnemigoOjo : EnemigoBase
{
    [SerializeField] private float velocidad = 3f;
    [SerializeField] private float distancia = 5f;
    [SerializeField] private float altura = 3f;
    [SerializeField] private int danoEscupitajo = 14;
    [SerializeField] private int danoEmbestida = 16;
    [SerializeField] private float velocidadEscupitajo = 7.5f;
    [SerializeField] private Vector2 enfriamiento = new Vector2(1.6f, 2.6f);
    public AnimadorHoja.Clip clipEscupitajo;
    public AnimadorHoja.Clip clipImpacto;

    private Vector2 origen;
    private float listoPara;
    private float fase;

    protected override void Awake()
    {
        base.Awake();
        salud.Volador = true;
        rb.gravityScale = 0f;
        origen = transform.position;
        fase = Random.value * 10f;
        colorAviso = new Color(0.6f, 1f, 0.3f, 1f);
        listoPara = Time.time + Random.Range(1.2f, 2f);
    }

    protected override IEnumerator Cerebro()
    {
        rb.gravityScale = 0f;
        while (true)
        {
            anim.Reproducir("vuelo");
            if (!VerPlayer(transform.position))
            {
                Volar(origen + Vector2.up * Mathf.Sin(Time.time + fase) * 0.4f, 0.5f);
                yield return null;
                continue;
            }
            MirarAlPlayer();
            int lado = transform.position.x >= player.position.x ? 1 : -1;
            Vector2 objetivo = PosPlayer + new Vector2(lado * distancia, altura + Mathf.Sin(Time.time * 1.4f + fase) * 0.5f);
            Volar(objetivo, 1f);

            bool despejado = !Physics2D.Linecast(transform.position, player.position, capaSuelo);
            if (Time.time >= listoPara && despejado)
            {
                if (DistanciaPlayer < 2.4f) yield return Embestida();
                else if (DistanciaPlayer < 9f) yield return Escupir(Random.value < 0.4f ? 3 : 1);
                continue;
            }
            yield return null;
        }
    }

    // No tiene animacion de muerte: cae girando y se desvanece.
    protected override void AlMorir()
    {
        base.AlMorir();
        StartCoroutine(Caer());
    }

    private IEnumerator Caer()
    {
        SpriteRenderer s = anim != null ? anim.destino : null;
        anim.enabled = false;
        Vector3 p = visual.position;
        for (float t = 0f; t < 0.8f; t += Time.deltaTime)
        {
            visual.position = p + Vector3.down * (t * t * 4f);
            visual.Rotate(0f, 0f, 540f * Time.deltaTime);
            if (s != null) { Color c = s.color; c.a = 1f - t / 0.8f; s.color = c; }
            yield return null;
        }
    }

    private void Volar(Vector2 objetivo, float factor)
    {
        Vector2 hacia = objetivo - (Vector2)transform.position;
        Vector2 deseada = Vector2.ClampMagnitude(hacia * 1.8f, velocidad * factor);
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, deseada, Time.deltaTime * 3f);
    }

    private IEnumerator Escupir(int cuantos)
    {
        rb.linearVelocity = Vector2.zero;
        // Carga (fotogramas 0-2) y escupe en el 3.
        anim.Reproducir("escupir", true, 3f / 12f / 0.5f * Ritmo);
        LanzarAviso(0.5f, 0.55f);
        yield return HastaFotograma(3);
        anim.Reproducir("escupir", false, 1f);
        MirarAlPlayer();
        Vector2 boca = (Vector2)transform.position + new Vector2(mirada * 0.4f, -0.1f);
        Vector2 dir = (PosPlayer - boca).normalized;
        Sonido.Reproducir("ojo_escupir", 0.7f);
        for (int i = 0; i < cuantos; i++)
        {
            float ang = cuantos == 1 ? 0f : (i - 1) * 16f;
            Vector2 d = Quaternion.Euler(0f, 0f, ang) * dir;
            ProyectilNieve.Lanzar(new ProyectilNieve.Datos
            {
                clip = clipEscupitajo, impacto = clipImpacto, color = Color.white, colorImpacto = new Color(0.7f, 1f, 0.4f),
                escala = 0.7f, escalaImpacto = 0.6f, dano = danoEscupitajo, magico = true, radio = 0.22f, rotar = true,
                sonidoImpacto = "ojo_impacto",
            }, boca, d * velocidadEscupitajo, transform);
        }
        yield return EsperarRitmo(0.5f);
        listoPara = Time.time + Random.Range(enfriamiento.x, enfriamiento.y);
    }

    private IEnumerator Embestida()
    {
        rb.linearVelocity = Vector2.zero;
        anim.Reproducir("girar", true);
        LanzarAviso(0.45f, 0.7f);
        yield return EsperarRitmo(0.45f);
        Vector2 dir = (PosPlayer - (Vector2)transform.position).normalized;
        Sonido.Reproducir("murcielago_chillido", 0.5f, 0.7f);
        bool pego = false;
        for (float t = 0f; t < 0.45f; t += Time.deltaTime)
        {
            rb.linearVelocity = dir * 10f;
            if (!pego)
            {
                var r = Golpear(Vector2.zero, new Vector2(0.9f, 0.9f), danoEmbestida);
                if (r.HasValue) { pego = true; if (r.Value == PlayerControler.ResultadoDano.Parry) break; }
            }
            if (Physics2D.OverlapCircle(transform.position, 0.3f, capaSuelo)) break;
            yield return null;
        }
        rb.linearVelocity = -dir * 2f;
        yield return EsperarRitmo(0.5f);
        listoPara = Time.time + Random.Range(enfriamiento.x, enfriamiento.y);
    }
}
