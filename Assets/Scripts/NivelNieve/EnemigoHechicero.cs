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
    [Tooltip("Pesos de cada ataque (1 = el golpe principal de su ficha).")]
    [SerializeField] private float pesoGarras = 0.87f;
    [SerializeField] private float pesoBaculo = 1f;
    [SerializeField] private Vector2 enfriamiento = new Vector2(1.2f, 2f);
    [SerializeField] private float aturdidoParry = 1.3f;
    [Tooltip("Altura del salto sombrio, en unidades (el player mide 1). Pasa por encima del player.")]
    [SerializeField] private float alturaSalto = 2.2f;
    [Tooltip("Probabilidad de retroceder un paso tras atacar.")]
    [Range(0f, 1f)] [SerializeField] private float probabilidadRetroceso = 0.25f;

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
            Vector2 ojos = Alto(1.4f);
            if (!VerPlayer(ojos))
            {
                if (VolverAZona(velocidad)) anim.Reproducir("correr", false, 0.6f);
                else { anim.Reproducir("quieto"); Frenar(10f); }
                yield return null;
                continue;
            }
            MirarAlPlayer();
            float dx = Mathf.Abs(DxPlayer);

            if (Time.time >= listoPara)
            {
                parado = false;
                if (dx > 5f && Time.time >= siguienteSalto && PuedeSaltar) yield return SaltoSombrio();
                else if (dx < 2.2f) yield return Baculo(Random.value < 0.5f);
                else if (dx < 4f) yield return Garras();
                else { yield return Acercarse(); continue; }

                if (parado) yield return Aturdido();
                // A veces da un paso atras y vuelve a medir la distancia.
                else if (Random.value < probabilidadRetroceso) { anim.Reproducir("correr", false, 0.5f); yield return Reposicionar(1.3f, velocidad); }
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
        FrenarAtaque();
        // Los fotogramas 0-2 (la sombra se forma en la mano) duran el aviso; las
        // garras golpean en los 3-5, que es cuando se ven extendidas.
        anim.Reproducir("garras", true, 3f / 12f / 0.6f);
        LanzarAviso(0.6f, 0.7f);
        Sonido.Reproducir("sombra_carga", 0.7f);
        yield return HastaFotograma(3);
        anim.Reproducir("garras", false, Ritmo);
        Sonido.Reproducir("sombra_golpe", 0.8f);
        yield return VentanaGolpe(3, 5, new Vector2(1.3f, 1.1f), new Vector2(2.2f, 2f), Dano(pesoGarras), true);
        if (resultadoVentana == PlayerControler.ResultadoDano.Parry) { parado = true; yield break; }
        yield return EsperarRitmo(0.4f);
    }

    private IEnumerator Baculo(bool encadenar)
    {
        armadura = true;
        FrenarAtaque();
        // Levanta el baculo (fotogramas 0-3) durante el aviso; el barrido golpea
        // en los 4-6.
        anim.Reproducir("baculo", true, 4f / 12f / 0.7f);
        LanzarAviso(0.7f, 0.75f);
        Sonido.Reproducir("sombra_carga", 0.6f, 0.8f);
        yield return HastaFotograma(4);
        anim.Reproducir("baculo", false, Ritmo);
        Sonido.Reproducir("jefe_tajo", 0.7f, 1.2f);
        rb.linearVelocity = new Vector2(mirada * 2.5f, rb.linearVelocity.y);
        yield return VentanaGolpe(4, 6, new Vector2(1.1f, 1.2f), new Vector2(2.2f, 2.3f), Dano(pesoBaculo));
        if (resultadoVentana == PlayerControler.ResultadoDano.Parry) { parado = true; rb.linearVelocity = Vector2.zero; yield break; }
        Frenar(30f);
        if (!encadenar) { yield return EsperarRitmo(0.5f); yield break; }

        // Remate retrasado: la pausa cambia cada vez, para que no se pueda
        // pulsar el parry de memoria.
        anim.Reproducir("quieto", true);
        yield return EsperarRitmo(Random.Range(0.15f, 0.6f));
        MirarYa();
        yield return Garras();
    }

    private IEnumerator SaltoSombrio()
    {
        siguienteSalto = Time.time + 7f;
        armadura = true;
        anim.Reproducir("saltar", true);
        LanzarAviso(0.45f, 0.6f);
        yield return Agacharse(0.45f / Mathf.Max(0.3f, Ritmo));
        Sonido.Reproducir("teletransporte", 0.6f);
        // Por encima del player, a caer a su espalda.
        float destino = player.position.x + Mathf.Sign(DxPlayer) * 2f;
        yield return Saltar(destino - transform.position.x, alturaSalto, () =>
        {
            if (rb.linearVelocity.y < 0f) anim.Reproducir("caer");
        }, false);
        CamaraDinamica.Sacudir(0.25f);
        ParticulasFx.Rafaga(transform.position, 14, new Color(0.5f, 0.2f, 0.8f), new Color(0.2f, 0.05f, 0.3f),
                            new Vector2(1f, 3f), 0.5f, new Vector2(0.06f, 0.14f), new Vector2(0.4f, 0.8f), 120f, 90f);
        MirarYa();
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
