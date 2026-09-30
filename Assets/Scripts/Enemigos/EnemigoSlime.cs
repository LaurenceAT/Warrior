using System.Collections;
using UnityEngine;

// Slime: el enemigo cuerpo a cuerpo comun.
//
// Patrulla dando la vuelta en paredes y bordes. Al ver al player se acerca y,
// a distancia de ataque, se encoge (los primeros fotogramas de su ataque, con
// el destello de aviso) y se lanza hacia delante. Tras el salto se queda un
// momento expuesto: es la ventana para castigarlo. El salto se puede bloquear
// y hacerle parry. Si falla, a veces rebota y repite la embestida enseguida;
// si no, a veces se aparta un poco antes de volver.
public class EnemigoSlime : EnemigoBase
{
    [Header("Movimiento")]
    [SerializeField] private float velocidadPatrulla = 1.1f;
    [SerializeField] private float velocidadPersecucion = 2.3f;
    [SerializeField] private float pausaPatrulla = 1.2f;

    [Header("Ataque")]
    [SerializeField] private float rangoAtaque = 2.2f;
    [SerializeField] private float enfriamiento = 1.3f;
    [Tooltip("Peso del salto (1 = el golpe principal de su ficha).")]
    [SerializeField] private float pesoSalto = 1f;
    // Fotogramas del clip "ataque": se encoge hasta el 8, salta del 9 al 14.
    [SerializeField] private int fotogramaSalto = 9;
    [SerializeField] private int fotogramaFinGolpe = 14;
    [Tooltip("Hasta donde llega el salto del ataque y cuanto sube (unidades).")]
    [SerializeField] private float distanciaSalto = 2.8f;
    [SerializeField] private float alturaSalto = 0.3f;
    [SerializeField] private Vector2 cajaOffset = new Vector2(0.9f, 0.35f);
    [SerializeField] private Vector2 cajaTamano = new Vector2(1.3f, 0.7f);

    [Header("Variedad")]
    [Tooltip("Si falla la embestida, probabilidad de repetirla enseguida.")]
    [Range(0f, 1f)] [SerializeField] private float probabilidadDoble = 0.3f;
    [Tooltip("Probabilidad de apartarse un poco tras atacar.")]
    [Range(0f, 1f)] [SerializeField] private float probabilidadRetroceso = 0.35f;

    private float listoPara;
    private bool ultimoPego;

    protected override IEnumerator Cerebro()
    {
        float tPatrulla = Random.Range(1.5f, 3.5f);

        while (true)
        {
            Vector2 ojos = Alto(0.4f);

            if (!VerPlayer(ojos))
            {
                // Patrulla: anda, gira en paredes y bordes, y a ratos se para.
                tPatrulla -= Time.deltaTime;
                if (tPatrulla <= 0f)
                {
                    // Pausa (de duracion algo distinta cada vez), atento por si aparece el player.
                    anim.Reproducir("quieto");
                    float pausa = pausaPatrulla * Random.Range(0.7f, 1.4f);
                    for (float t = 0f; t < pausa && !VerPlayer(Alto(0.4f)); t += Time.deltaTime) { Frenar(frenada); yield return null; }
                    tPatrulla = Random.Range(2f, 4f);
                    if (Random.value < 0.4f) Mirar(-mirada);
                    continue;
                }

                if (HayParedDelante(0.8f) || !HaySueloDelante(0.7f) || (LejosDeZona && mirada != HaciaZona)) Mirar(-mirada);
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
                // Si fallo, a veces rebota y embiste otra vez enseguida.
                if (!ultimoPego && Random.value < probabilidadDoble && Mathf.Abs(DxPlayer) <= rangoAtaque * 1.2f)
                {
                    MirarYa();
                    yield return Atacar();
                }
                // Si no, a veces se aparta un poco antes de volver.
                else if (Random.value < probabilidadRetroceso)
                {
                    anim.Reproducir("andar", false, 1.2f);
                    yield return Reposicionar(1f, velocidadPersecucion);
                }
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
        FrenarAtaque();
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
                // Un solo impulso, desde el suelo; el destino no cae por un borde.
                salto = true;
                Impulsar(distanciaSalto * mirada, alturaSalto);
            }

            if (salto && !pego && f <= fotogramaFinGolpe)
            {
                var r = Golpear(cajaOffset, cajaTamano, Dano(pesoSalto));
                if (r.HasValue) pego = true;
            }

            if (f > fotogramaFinGolpe) Frenar(14f);
            yield return null;
        }

        ultimoPego = pego;
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
