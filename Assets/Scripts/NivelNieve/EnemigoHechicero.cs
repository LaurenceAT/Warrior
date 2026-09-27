using System.Collections;
using UnityEngine;

// Hechicero sombrio: el enemigo duro del nivel (uno por tramo). Lento pero con
// golpes pesados y armadura mientras ataca (los golpes le quitan vida pero no le
// paran; un parry si).
//   - Garras de sombra: proyecta una sombra hacia delante (medio alcance).
//   - Golpe de baculo: barrido por encima, de cerca. A veces lo encadena con
//     las garras con un retraso distinto cada vez (hay que leerlo).
//   - Salto sombrio: salta por encima del player y cae a su espalda.
// Debil a lo sagrado; la oscuridad casi no le hace nada.
public class EnemigoHechicero : EnemigoBase
{
    [SerializeField] private float velocidad = 1.5f;
    [SerializeField] private int danoGarras = 26;
    [SerializeField] private int danoBaculo = 30;
    [SerializeField] private Vector2 enfriamiento = new Vector2(1.2f, 2f);
    [SerializeField] private float aturdidoParry = 1.3f;

    private float listoPara;
    private float siguienteSalto;
    private bool parado;

    protected override void Awake()
    {
        base.Awake();
        colorAviso = new Color(0.75f, 0.25f, 1f, 1f);
        listoPara = Time.time + 0.8f;
    }

    protected override IEnumerator Cerebro()
    {
        while (true)
        {
            armadura = false;
            Vector2 ojos = (Vector2)transform.position + Vector2.up * 1.4f;
            if (!VerPlayer(ojos))
            {
                anim.Reproducir("quieto");
                Frenar(10f);
                yield return null;
                continue;
            }
            MirarAlPlayer();
            float dx = Mathf.Abs(DxPlayer);

            if (Time.time >= listoPara)
            {
                parado = false;
                if (dx > 5f && Time.time >= siguienteSalto) yield return SaltoSombrio();
                else if (dx < 2.2f) yield return Baculo(Random.value < 0.5f);
                else if (dx < 4f) yield return Garras();
                else { yield return Acercarse(); continue; }

                if (parado) yield return Aturdido();
                listoPara = Time.time + Random.Range(enfriamiento.x, enfriamiento.y);
                continue;
            }
            yield return Acercarse();
        }
    }

    private IEnumerator Acercarse()
    {
        if (Mathf.Abs(DxPlayer) > 1.8f && HaySueloDelante(0.8f) && !HayParedDelante(0.8f, 1f))
        {
            anim.Reproducir("correr", false, 0.7f);
            Andar(velocidad);
        }
        else
        {
            anim.Reproducir("quieto");
            Frenar(12f);
        }
        yield return null;
    }

    private IEnumerator Garras()
    {
        armadura = true;
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        // Los fotogramas 0-2 (la sombra se forma en la mano) duran el aviso; las
        // garras golpean en los 3-5, que es cuando se ven extendidas.
        anim.Reproducir("garras", true, 3f / 12f / 0.6f);
        LanzarAviso(0.6f, 0.7f);
        Sonido.Reproducir("sombra_carga", 0.7f);
        yield return HastaFotograma(3);
        anim.Reproducir("garras", false, Ritmo);
        Sonido.Reproducir("sombra_golpe", 0.8f);
        yield return VentanaGolpe(3, 5, new Vector2(1.3f, 1.1f), new Vector2(2.2f, 2f), danoGarras, true);
        if (resultadoVentana == PlayerControler.ResultadoDano.Parry) { parado = true; yield break; }
        yield return EsperarRitmo(0.4f);
    }

    private IEnumerator Baculo(bool encadenar)
    {
        armadura = true;
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        // Levanta el baculo (fotogramas 0-3) durante el aviso; el barrido golpea
        // en los 4-6.
        anim.Reproducir("baculo", true, 4f / 12f / 0.7f);
        LanzarAviso(0.7f, 0.75f);
        Sonido.Reproducir("sombra_carga", 0.6f, 0.8f);
        yield return HastaFotograma(4);
        anim.Reproducir("baculo", false, Ritmo);
        Sonido.Reproducir("jefe_tajo", 0.7f, 1.2f);
        rb.linearVelocity = new Vector2(mirada * 2.5f, rb.linearVelocity.y);
        yield return VentanaGolpe(4, 6, new Vector2(1.1f, 1.2f), new Vector2(2.2f, 2.3f), danoBaculo);
        if (resultadoVentana == PlayerControler.ResultadoDano.Parry) { parado = true; rb.linearVelocity = Vector2.zero; yield break; }
        Frenar(30f);
        if (!encadenar) { yield return EsperarRitmo(0.5f); yield break; }

        // Remate retrasado: la pausa cambia cada vez, para que no se pueda
        // pulsar el parry de memoria.
        anim.Reproducir("quieto", true);
        yield return EsperarRitmo(Random.Range(0.15f, 0.6f));
        MirarAlPlayer();
        yield return Garras();
    }

    private IEnumerator SaltoSombrio()
    {
        siguienteSalto = Time.time + 7f;
        armadura = true;
        anim.Reproducir("saltar", true);
        LanzarAviso(0.45f, 0.6f);
        yield return EsperarRitmo(0.45f);
        Sonido.Reproducir("teletransporte", 0.6f);
        float destino = player.position.x + Mathf.Sign(DxPlayer) * 2f;
        float dur = 0.75f;
        rb.linearVelocity = new Vector2((destino - transform.position.x) / dur, 11f);
        for (float t = 0f; t < 1.4f; t += Time.deltaTime)
        {
            if (rb.linearVelocity.y < 0f) anim.Reproducir("caer");
            if (t > 0.2f && rb.linearVelocity.y <= 0f && Physics2D.Raycast(transform.position + Vector3.up * 0.1f, Vector2.down, 0.2f, capaSuelo)) break;
            yield return null;
        }
        Frenar(40f);
        CamaraDinamica.Sacudir(0.25f);
        ParticulasFx.Rafaga(transform.position, 14, new Color(0.5f, 0.2f, 0.8f), new Color(0.2f, 0.05f, 0.3f),
                            new Vector2(1f, 3f), 0.5f, new Vector2(0.06f, 0.14f), new Vector2(0.4f, 0.8f), 120f, 90f);
        MirarAlPlayer();
        yield return Baculo(false);
    }

    private IEnumerator Aturdido()
    {
        armadura = false;
        anim.Reproducir("golpe", true);
        TextoFlotante.Mostrar("Aturdido", (Vector2)transform.position + Vector2.up * 2.4f, new Color(1f, 0.95f, 0.7f), 0.9f);
        salud.Stagger(aturdidoParry);
        yield return EsperarRitmo(aturdidoParry);
    }
}
