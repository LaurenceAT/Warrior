using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Arquera arcana: ataca de lejos y no deja que se le acerquen.
//   - Disparo: tensa el arco (aviso) y suelta una flecha recta y rapida.
//   - Lluvia: dispara al cielo; unas marcas en el suelo avisan de donde caeran
//     las flechas (alrededor del player) y un momento despues caen.
//   - Voltereta: si el player se acerca, salta hacia atras (invulnerable).
//   - Destello: arrinconada contra una pared, se teletransporta lejos.
// Debil a la oscuridad; resiste lo sagrado.
public class EnemigoArquero : EnemigoBase, IModificadorDano
{
    [SerializeField] private float distanciaIdeal = 6f;
    [SerializeField] private float velocidad = 2.4f;
    [SerializeField] private int danoFlecha = 14;
    [SerializeField] private int danoLluvia = 16;
    [SerializeField] private float velocidadFlecha = 13f;
    [SerializeField] private Vector2 enfriamiento = new Vector2(1.4f, 2.4f);
    public Sprite spriteFlecha;
    public AnimadorHoja.Clip clipMarca;

    private float listoPara;
    private float siguienteVoltereta;
    private float siguienteLluvia;
    private bool invulnerable;

    protected override void Awake()
    {
        base.Awake();
        colorAviso = new Color(1f, 0.4f, 1f, 1f);
    }

    protected override IEnumerator Cerebro()
    {
        while (true)
        {
            Vector2 ojos = (Vector2)transform.position + Vector2.up * 0.8f;
            if (!VerPlayer(ojos))
            {
                anim.Reproducir("quieto");
                Frenar(10f);
                yield return null;
                continue;
            }
            MirarAlPlayer();
            float dx = Mathf.Abs(DxPlayer);

            if (dx < 2.6f && Time.time >= siguienteVoltereta)
            {
                // Sin sitio detras: destello lejos. Si no, voltereta.
                Mirar(-mirada);
                bool hayHueco = HaySueloDelante(1.5f) && !HayParedDelante(2f);
                MirarAlPlayer();
                yield return hayHueco ? Voltereta() : Destello();
                continue;
            }

            if (Time.time >= listoPara)
            {
                if (Time.time >= siguienteLluvia && Random.value < 0.35f) yield return Lluvia();
                else yield return Disparo();
                continue;
            }

            // Mantiene la distancia: se aparta si esta cerca, se acerca si esta lejos.
            float quiere = dx < distanciaIdeal - 1f ? -1f : dx > distanciaIdeal + 2f ? 1f : 0f;
            if (quiere != 0f)
            {
                Mirar(quiere > 0f ? (DxPlayer > 0f ? 1 : -1) : (DxPlayer > 0f ? -1 : 1));
                if (HaySueloDelante(0.6f) && !HayParedDelante(0.6f)) { anim.Reproducir("correr"); Andar(velocidad); }
                else { anim.Reproducir("quieto"); Frenar(12f); }
                MirarAlPlayerSinGirarVelocidad();
            }
            else
            {
                anim.Reproducir("quieto");
                Frenar(12f);
            }
            yield return null;
        }
    }

    // Mira al player sin cambiar la velocidad que lleva (retrocede de espaldas).
    private void MirarAlPlayerSinGirarVelocidad()
    {
        Vector2 v = rb.linearVelocity;
        MirarAlPlayer();
        rb.linearVelocity = v;
    }

    private IEnumerator Disparo()
    {
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        // Tensa el arco (fotogramas 0-3, el aviso) y suelta en el 4, cuando se ve.
        anim.Reproducir("disparar", true, 4f / 12f / 0.5f * Ritmo);
        LanzarAviso(0.5f, 0.5f);
        Sonido.Reproducir("arco_tensar", 0.6f);
        yield return HastaFotograma(4);
        anim.Reproducir("disparar", false, 1f);
        MirarAlPlayer();
        Vector2 boca = (Vector2)transform.position + new Vector2(mirada * 0.5f, 0.55f);
        Vector2 destino = PosPlayer;
        Vector2 dir = (destino - boca).normalized;
        // Solo dispara recto o en un angulo suave: no tira a la vertical.
        dir = new Vector2(Mathf.Sign(dir.x) * Mathf.Max(0.8f, Mathf.Abs(dir.x)), Mathf.Clamp(dir.y, -0.5f, 0.5f)).normalized;
        Sonido.Reproducir("arco_disparo", 0.8f);
        ProyectilNieve.Lanzar(new ProyectilNieve.Datos
        {
            sprite = spriteFlecha, color = new Color(1f, 0.6f, 1f), escala = 1.2f, dano = danoFlecha, magico = false,
            radio = 0.12f, sonidoImpacto = "flecha_impacto",
        }, boca, dir * velocidadFlecha, transform);
        yield return EsperarRitmo(0.35f);
        listoPara = Time.time + Random.Range(enfriamiento.x, enfriamiento.y);
    }

    private IEnumerator Lluvia()
    {
        siguienteLluvia = Time.time + 6f;
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        anim.Reproducir("cielo", true, 3f / 10f / 0.55f * Ritmo);
        LanzarAviso(0.55f, 0.6f);
        yield return HastaFotograma(3);
        anim.Reproducir("cielo", false, 1f);
        Sonido.Reproducir("arco_disparo", 0.8f, 0.85f);

        // Marcas en el suelo: donde esta el player y a sus lados.
        var puntos = new List<Vector2>();
        float x0 = PosPlayer.x;
        foreach (float d in new[] { 0f, -1.6f, 1.6f, Random.Range(-3.2f, 3.2f) })
        {
            float x = x0 + d;
            RaycastHit2D suelo = Physics2D.Raycast(new Vector2(x, PosPlayer.y + 2f), Vector2.down, 8f, capaSuelo);
            if (!suelo) continue;
            puntos.Add(suelo.point);
            EfectoVisual m = EfectoVisual.Crear(clipMarca, suelo.point + Vector2.up * 0.05f, 0.5f, new Color(1f, 0.5f, 1f, 0.8f), false, 1f, "VFX", 3);
            if (m != null) m.transform.localScale = new Vector3(0.9f, 0.3f, 1f);
        }
        yield return EsperarRitmo(0.9f);
        foreach (Vector2 p in puntos)
        {
            ProyectilNieve.Lanzar(new ProyectilNieve.Datos
            {
                sprite = spriteFlecha, color = new Color(1f, 0.6f, 1f), escala = 1.2f, dano = danoLluvia, magico = false,
                radio = 0.15f, sonidoImpacto = "flecha_impacto",
            }, p + new Vector2(0.4f, 6f), new Vector2(-1f, -16f), transform);
            yield return new WaitForSeconds(0.07f);
        }
        yield return EsperarRitmo(0.4f);
        listoPara = Time.time + Random.Range(enfriamiento.x, enfriamiento.y);
    }

    private IEnumerator Voltereta()
    {
        siguienteVoltereta = Time.time + 2.8f;
        anim.Reproducir("voltereta", true);
        Sonido.Reproducir("esquiva", 0.5f, 1.2f);
        invulnerable = true;
        armadura = true;
        rb.linearVelocity = new Vector2(-mirada * 6.5f, 4f);
        for (float t = 0f; t < 0.55f; t += Time.deltaTime)
        {
            if (!HaySueloDelante(-0.8f, 2f)) rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            yield return null;
        }
        invulnerable = false;
        armadura = false;
        Frenar(40f);
        // Sale de la voltereta con un disparo rapido.
        listoPara = Mathf.Min(listoPara, Time.time + 0.2f);
    }

    private IEnumerator Destello()
    {
        siguienteVoltereta = Time.time + 3.5f;
        anim.Reproducir("destello", true);
        LanzarAviso(0.3f, 0.8f);
        Sonido.Reproducir("teletransporte", 0.6f);
        yield return EsperarRitmo(0.3f);
        // Busca sitio lejos del player, a un lado u otro.
        foreach (float d in new[] { 7f, -7f, 5f, -5f })
        {
            Vector2 prueba = new Vector2(player.position.x + d, transform.position.y + 1.5f);
            RaycastHit2D suelo = Physics2D.Raycast(prueba, Vector2.down, 4f, capaSuelo);
            if (!suelo || Physics2D.OverlapCircle(suelo.point + Vector2.up * 0.6f, 0.3f, capaSuelo)) continue;
            ParticulasFx.Rafaga((Vector2)transform.position + Vector2.up * 0.5f, 14, new Color(1f, 0.5f, 1f), Color.white,
                                new Vector2(1f, 3f), 0f, new Vector2(0.05f, 0.1f), new Vector2(0.3f, 0.5f));
            transform.position = suelo.point + Vector2.up * 0.05f;
            break;
        }
        MirarAlPlayer();
        yield return EsperarRitmo(0.3f);
    }

    // Durante la voltereta los golpes no entran.
    public bool Invulnerable => invulnerable;

    public int Modificar(int dano, TipoArma arma)
    {
        if (!invulnerable) return dano;
        TextoFlotante.Mostrar("Esquiva", (Vector2)transform.position + Vector2.up * 1.4f, new Color(0.9f, 0.8f, 1f), 0.7f);
        return 0;
    }
}
