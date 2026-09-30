using System.Collections;
using UnityEngine;

// Rata de escarcha: pequena, rapida y en grupo. Golpea y se retira.
//   - Mordisco: pegado al player, un aviso muy corto y una dentellada.
//   - Salto: a media distancia se agacha (aviso) y salta encima del player.
// En manada: la que te descubre chilla y avisa a las demas.
// Tras atacar se aparta un poco (golpea y huye), asi que conviene esperarla con
// un parry o castigarla al aterrizar. Debil al fuego; resiste la escarcha.
public class EnemigoRata : EnemigoBase
{
    [SerializeField] private float velocidadPatrulla = 1.4f;
    [SerializeField] private float velocidadCarrera = 5f;
    [Tooltip("Peso del mordisco (1 = el golpe principal de su ficha).")]
    [SerializeField] private float pesoMordisco = 1f;
    [SerializeField] private float pesoSalto = 1.33f;
    [Tooltip("Altura del salto, en unidades (el player mide 1).")]
    [SerializeField] private float alturaSalto = 1.1f;
    [SerializeField] private float rangoMordisco = 1.1f;
    [SerializeField] private Vector2 rangoSalto = new Vector2(2.6f, 4.6f);
    [SerializeField] private Vector2 enfriamiento = new Vector2(0.8f, 1.5f);
    [Tooltip("Al descubrir al player avisa a las ratas a esta distancia.")]
    [SerializeField] private float radioManada = 7f;

    private float listoPara;

    protected override IEnumerator Cerebro()
    {
        float giro = Random.Range(1.5f, 3f);
        while (true)
        {
            Vector2 ojos = Alto(0.3f);
            if (!VerPlayer(ojos))
            {
                giro -= Time.deltaTime;
                if (giro <= 0f || HayParedDelante(0.5f, 0.2f) || !HaySueloDelante(0.4f) || (LejosDeZona && mirada != HaciaZona)) { Mirar(-mirada); giro = Random.Range(1.5f, 3f); }
                anim.Reproducir("andar", false, 0.6f);
                Andar(velocidadPatrulla);
                yield return null;
                continue;
            }

            MirarAlPlayer();
            float dx = Mathf.Abs(DxPlayer);
            float dy = player.position.y - transform.position.y;
            bool listo = Time.time >= listoPara && Mathf.Abs(dy) < 1.5f;

            if (listo && dx <= rangoMordisco) { yield return Mordisco(); continue; }
            if (listo && PuedeSaltar && dx >= rangoSalto.x && dx <= rangoSalto.y && Random.value < Time.deltaTime * 1.8f) { yield return Salto(); continue; }

            if (dx > rangoMordisco * 0.8f && HaySueloDelante(0.45f) && !HayParedDelante(0.5f, 0.2f))
            {
                anim.Reproducir("andar", false, 1.5f);
                Andar(velocidadCarrera);
            }
            else
            {
                anim.Reproducir("quieto");
                Frenar(20f);
            }
            yield return null;
        }
    }

    private IEnumerator Mordisco()
    {
        FrenarAtaque();
        // Se encoge (fotogramas 0-6, el aviso) y muerde al estirarse (7-10).
        anim.Reproducir("morder", true, Ritmo);
        LanzarAviso(7f / 22f, 0.5f);
        yield return HastaFotograma(7);
        Sonido.Reproducir("rata_mordisco", 0.7f);
        rb.linearVelocity = new Vector2(mirada * 3.5f, rb.linearVelocity.y);
        yield return VentanaGolpe(7, 10, new Vector2(0.55f, 0.3f), new Vector2(0.9f, 0.6f), Dano(pesoMordisco));
        yield return Retirarse();
    }

    private IEnumerator Salto()
    {
        FrenarAtaque();
        anim.Reproducir("quieto", true);
        LanzarAviso(0.45f, 0.6f);
        // Se agacha (aviso) y salta en arco hasta donde estaba el player.
        yield return Agacharse(0.45f / Mathf.Max(0.3f, Ritmo));

        Sonido.Reproducir("rata_chillido", 0.6f);
        anim.Reproducir("morder", true);
        bool pego = false;
        yield return Saltar(DxPlayer, alturaSalto, () =>
        {
            if (!pego && Golpear(new Vector2(0.3f, 0.3f), new Vector2(1f, 0.8f), Dano(pesoSalto)).HasValue) pego = true;
        }, false);
        // Al aterrizar se queda un instante expuesta.
        anim.Reproducir("quieto");
        yield return EsperarRitmo(0.35f);
        yield return Retirarse();
    }

    private IEnumerator Retirarse()
    {
        listoPara = Time.time + Random.Range(enfriamiento.x, enfriamiento.y);
        Mirar(-mirada);
        anim.Reproducir("andar", false, 1.4f);
        for (float t = 0f; t < 0.45f; t += Time.deltaTime)
        {
            if (!HaySueloDelante(0.45f) || HayParedDelante(0.5f, 0.2f)) break;
            Andar(velocidadCarrera * 0.7f);
            yield return null;
        }
        Frenar(30f);
        MirarYa();
    }

    // En manada: al descubrir al player chilla y avisa a las ratas cercanas,
    // que reaccionan un poco despues cada una.
    protected override void AlDescubrir()
    {
        Sonido.Reproducir("rata_chillido", 0.45f, Random.Range(1.05f, 1.25f));
        foreach (EnemigoBase o in Activos)
            if (o != this && o is EnemigoRata && Vector2.Distance(o.transform.position, transform.position) < radioManada)
                o.Alertar(Random.Range(0.1f, 0.35f));
    }
}
