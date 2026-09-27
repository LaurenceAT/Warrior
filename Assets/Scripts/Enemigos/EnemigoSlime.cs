using System.Collections;
using UnityEngine;

// Slime: el enemigo cuerpo a cuerpo comun.
//
// Patrulla dando la vuelta en paredes y bordes. Al ver al player se acerca y,
// a distancia de ataque, se encoge (los primeros fotogramas de su ataque, con
// el destello de aviso) y se lanza hacia delante. Tras el salto se queda un
// momento expuesto: es la ventana para castigarlo. El salto se puede bloquear
// y hacerle parry.
public class EnemigoSlime : EnemigoBase
{
    [Header("Movimiento")]
    [SerializeField] private float velocidadPatrulla = 1.1f;
    [SerializeField] private float velocidadPersecucion = 2.3f;
    [SerializeField] private float pausaPatrulla = 1.2f;

    [Header("Ataque")]
    [SerializeField] private float rangoAtaque = 2.2f;
    [SerializeField] private float enfriamiento = 1.3f;
    [SerializeField] private int dano = 20;
    // Fotogramas del clip "ataque": se encoge hasta el 8, salta del 9 al 14.
    [SerializeField] private int fotogramaSalto = 9;
    [SerializeField] private int fotogramaFinGolpe = 14;
    [SerializeField] private float impulsoSalto = 5.5f;
    [SerializeField] private Vector2 cajaOffset = new Vector2(0.9f, 0.35f);
    [SerializeField] private Vector2 cajaTamano = new Vector2(1.3f, 0.7f);

    private float listoPara;

    protected override IEnumerator Cerebro()
    {
        float tPatrulla = Random.Range(1.5f, 3.5f);

        while (true)
        {
            Vector2 ojos = (Vector2)transform.position + Vector2.up * 0.4f;

            if (!VerPlayer(ojos))
            {
                // Patrulla: anda, gira en paredes y bordes, y a ratos se para.
                tPatrulla -= Time.deltaTime;
                if (tPatrulla <= 0f)
                {
                    anim.Reproducir("quieto");
                    rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                    yield return new WaitForSeconds(pausaPatrulla);
                    tPatrulla = Random.Range(2f, 4f);
                    continue;
                }

                if (HayParedDelante(0.8f) || !HaySueloDelante(0.7f)) Mirar(-mirada);
                anim.Reproducir("andar");
                Andar(velocidadPatrulla);
                yield return null;
                continue;
            }

            MirarAlPlayer();
            float dx = Mathf.Abs(DxPlayer);

            if (dx <= rangoAtaque && Time.time >= listoPara && Mathf.Abs(player.position.y - transform.position.y) < 1.6f)
            {
                yield return Atacar();
                continue;
            }

            // Se acerca, sin tirarse por un borde. Si ya esta a tiro espera al
            // enfriamiento balanceandose en el sitio.
            if (dx > rangoAtaque * 0.8f && HaySueloDelante(0.7f) && !HayParedDelante(0.6f))
            {
                anim.Reproducir("andar", false, 1.4f);
                Andar(velocidadPersecucion);
            }
            else
            {
                anim.Reproducir("quieto");
                Frenar(12f);
            }
            yield return null;
        }
    }

    private IEnumerator Atacar()
    {
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        anim.Reproducir("ataque", true);

        // Aviso mientras se encoge.
        LanzarAviso(fotogramaSalto / anim.Fps("ataque"));

        bool pego = false;
        bool salto = false;
        while (!anim.Terminado)
        {
            int f = anim.Fotograma;
            if (!salto && f >= fotogramaSalto)
            {
                salto = true;
                rb.linearVelocity = new Vector2(impulsoSalto * mirada, 2.5f);
            }

            // El salto no lo tira por un borde: se frena en seco al llegar.
            if (salto && !HaySueloDelante(0.5f, 1.2f)) rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

            if (salto && !pego && f <= fotogramaFinGolpe)
            {
                var r = Golpear(cajaOffset, cajaTamano, dano);
                if (r.HasValue) pego = true;
            }

            if (f > fotogramaFinGolpe) Frenar(14f);
            yield return null;
        }

        listoPara = Time.time + enfriamiento;
    }

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();
        Gizmos.color = Color.red;
        int m = Application.isPlaying ? mirada : 1;
        Gizmos.DrawWireCube(transform.position + new Vector3(cajaOffset.x * m, cajaOffset.y, 0f), cajaTamano);
    }
}
