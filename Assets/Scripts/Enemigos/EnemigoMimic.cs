using System.Collections;
using UnityEngine;

// Mimic: un cofre que es un monstruo. Guarda la recompensa de la cueva secreta.
//
// Disfrazado no se mueve ni tiene barra. Al acercarse el player (o al pegarle)
// se abre, se transforma temblando y pasa al combate:
//   - Mordisco: se alza con la boca abierta (aviso largo) y se lanza con un
//     saltito. Mucho dano, facil de leer.
//   - Latigazo: echa el cuerpo atras (aviso corto) y barre hacia delante con
//     mas alcance. Obliga a no quedarse a media distancia.
// Al morir avisa con AlDerrotado (la recompensa aparece ahi).
public class EnemigoMimic : EnemigoBase
{
    public event System.Action AlDerrotado;

    [Header("Emboscada")]
    [SerializeField] private float radioDespertar = 2.4f;

    [Header("Combate")]
    [SerializeField] private float velocidad = 2.2f;
    [SerializeField] private float enfriamiento = 0.9f;

    [Header("Mordisco (clip mordisco)")]
    [SerializeField] private int danoMordisco = 40;
    [SerializeField] private float rangoMordisco = 1.6f;
    [SerializeField] private int fotogramaSaltoMordisco = 12;
    [SerializeField] private Vector2 cajaMordisco = new Vector2(0.85f, 0.5f);
    [SerializeField] private Vector2 tamanoMordisco = new Vector2(1.2f, 0.9f);

    [Header("Latigazo (clip lengua)")]
    [SerializeField] private int danoLatigazo = 40;
    [SerializeField] private float rangoLatigazo = 2.6f;
    [SerializeField] private int fotogramaGolpeLatigazo = 8;
    [SerializeField] private int fotogramaFinLatigazo = 10;
    [SerializeField] private Vector2 cajaLatigazo = new Vector2(1.3f, 0.45f);
    [SerializeField] private Vector2 tamanoLatigazo = new Vector2(2f, 0.7f);

    private bool despierto;
    private float listoPara;

    protected override void Start()
    {
        // Disfrazado y transformandose no se le interrumpe: un golpe solo lo despierta.
        armadura = true;
        anim.Reproducir("cerrado");
        base.Start();
    }

    protected override IEnumerator Cerebro()
    {
        if (!despierto) yield return Emboscada();

        while (true)
        {
            if (!VerPlayer((Vector2)transform.position + Vector2.up * 0.5f))
            {
                anim.Reproducir("quieto");
                Frenar(10f);
                yield return null;
                continue;
            }

            MirarAlPlayer();
            float dx = Mathf.Abs(DxPlayer);
            bool alturaOk = Mathf.Abs(player.position.y - transform.position.y) < 1.8f;

            if (Time.time >= listoPara && alturaOk && dx <= rangoLatigazo)
            {
                // Pegado, mordisco; a media distancia, latigazo. Con algo de azar
                // para que no sea del todo predecible.
                bool morder = dx <= rangoMordisco ? Random.value < 0.7f : false;
                yield return morder ? Mordisco() : Latigazo();
                continue;
            }

            if (dx > rangoMordisco * 0.8f && HaySueloDelante(0.6f) && !HayParedDelante(0.6f))
            {
                anim.Reproducir("andar");
                Andar(velocidad);
            }
            else
            {
                anim.Reproducir("quieto");
                Frenar(12f);
            }
            yield return null;
        }
    }

    private IEnumerator Emboscada()
    {
        anim.Reproducir("cerrado");
        while (!despierto)
        {
            if (BuscarPlayer() && DistanciaPlayer <= radioDespertar) break;
            if (alerta) break; // le han pegado
            yield return null;
        }

        despierto = true;
        MirarAlPlayer();

        // Se abre y se transforma temblando: tiempo para que el player reaccione.
        anim.Reproducir("abrir", true);
        yield return EsperarClip();
        anim.Reproducir("transformar", true);
        Vector3 base0 = visual.localPosition;
        while (!anim.Terminado)
        {
            visual.localPosition = base0 + new Vector3(Random.Range(-0.04f, 0.04f), 0f, 0f);
            yield return null;
        }
        visual.localPosition = base0;
        listoPara = Time.time + 0.4f;
        armadura = false;
    }

    private IEnumerator Mordisco()
    {
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        anim.Reproducir("mordisco", true);
        LanzarAviso(fotogramaSaltoMordisco / anim.Fps("mordisco"), 0.6f);

        bool salto = false, pego = false;
        while (!anim.Terminado)
        {
            if (!salto && anim.Fotograma >= fotogramaSaltoMordisco)
            {
                salto = true;
                rb.linearVelocity = new Vector2(4.5f * mirada, 3f);
            }
            if (salto && !pego) pego = Golpear(cajaMordisco, tamanoMordisco, danoMordisco).HasValue;
            yield return null;
        }

        // Aterriza y se queda un instante con la guardia baja.
        yield return Recuperar(0.35f);
    }

    private IEnumerator Latigazo()
    {
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        anim.Reproducir("lengua", true);
        LanzarAviso(fotogramaGolpeLatigazo / anim.Fps("lengua"), 0.5f);

        bool pego = false;
        while (!anim.Terminado)
        {
            int f = anim.Fotograma;
            if (!pego && f >= fotogramaGolpeLatigazo && f <= fotogramaFinLatigazo)
                pego = Golpear(cajaLatigazo, tamanoLatigazo, danoLatigazo).HasValue;
            yield return null;
        }

        yield return Recuperar(0.25f);
    }

    private IEnumerator Recuperar(float tiempo)
    {
        anim.Reproducir("quieto");
        float t = 0f;
        while (t < tiempo)
        {
            t += Time.deltaTime;
            Frenar(14f);
            yield return null;
        }
        listoPara = Time.time + enfriamiento;
    }

    protected override void AlMorir()
    {
        base.AlMorir();
        AlDerrotado?.Invoke();
    }

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();
        int m = Application.isPlaying ? mirada : 1;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, radioDespertar);
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(transform.position + new Vector3(cajaMordisco.x * m, cajaMordisco.y, 0f), tamanoMordisco);
        Gizmos.color = new Color(1f, 0.4f, 0.8f);
        Gizmos.DrawWireCube(transform.position + new Vector3(cajaLatigazo.x * m, cajaLatigazo.y, 0f), tamanoLatigazo);
    }
}
