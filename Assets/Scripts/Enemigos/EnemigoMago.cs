using System.Collections;
using UnityEngine;

// Mago (Evil Wizard 3): el enemigo a distancia.
//
// Mantiene la distancia y lanza bolas magicas (se pueden bloquear, esquivar con
// el barrido o devolver con parry). Si el player se le echa encima, retrocede
// sin darle la espalda; si ya no puede retroceder (pared o borde detras), suelta
// un estallido a su alrededor con un aviso claro: arrinconarlo no sale gratis.
public class EnemigoMago : EnemigoBase
{
    [Header("Distancias")]
    [SerializeField] private float distanciaIdeal = 6f;
    [SerializeField] private float distanciaMinima = 3.2f;
    [SerializeField] private float velocidad = 2f;
    [SerializeField] private float velocidadHuida = 2.8f;
    // Tope de retroceso seguido: despues ataca aunque el player siga cerca.
    [SerializeField] private float huidaMaxima = 1.6f;

    [Header("Disparo")]
    [SerializeField] private ProyectilMagico proyectil;
    [SerializeField] private Vector2 puntaBaculo = new Vector2(0.8f, 1.35f);
    [SerializeField] private int fotogramaDisparo = 8;
    [SerializeField] private Vector2 enfriamiento = new Vector2(1.9f, 2.7f);

    [Header("Estallido (arrinconado)")]
    [Tooltip("Pesos (1 = el golpe principal de su ficha: la bola).")]
    [SerializeField] private float pesoBola = 1f;
    [SerializeField] private float pesoEstallido = 1.5f;
    [SerializeField] private float radioEstallido = 1.9f;
    [SerializeField] private float avisoEstallido = 0.7f;
    [SerializeField] private float enfriamientoEstallido = 3f;
    [SerializeField] private Color colorEstallido = new Color(0.65f, 0.3f, 1f, 1f);

    private float listoDisparo;
    private float listoEstallido;
    private float tHuyendo;
    private SpriteRenderer circuloAviso;

    protected override void Awake()
    {
        base.Awake();
        colorAviso = new Color(0.7f, 0.35f, 1f, 1f);
        // A distancia tiene que verte desde lejos.
        radioDeteccion = Mathf.Max(radioDeteccion, 10f);
        radioOlvido = Mathf.Max(radioOlvido, 13f);
    }

    protected override IEnumerator Cerebro()
    {
        while (true)
        {
            if (!VerPlayer(Alto(1.3f)))
            {
                if (VolverAZona(velocidad)) anim.Reproducir("andar");
                else { anim.Reproducir("quieto"); Frenar(10f); }
                tHuyendo = 0f;
                yield return null;
                continue;
            }

            MirarAlPlayer();
            float dx = Mathf.Abs(DxPlayer);

            if (dx < distanciaMinima)
            {
                // Retrocede mirando al player: comprueba el suelo y la pared del
                // lado contrario al player.
                int huida = DxPlayer > 0f ? -1 : 1;
                bool puedeHuir = tHuyendo < huidaMaxima && HaySueloHacia(huida, 0.7f) && !HayParedHacia(huida, 0.7f, 0.5f);
                if (puedeHuir)
                {
                    tHuyendo += Time.deltaTime;
                    anim.Reproducir("andar", false, 1.3f);
                    AndarDir(huida, velocidadHuida);
                    yield return null;
                    continue;
                }

                if (Time.time >= listoEstallido && dx < (radioEstallido + 0.6f) * Escala)
                {
                    yield return Estallido();
                    continue;
                }
            }
            else
            {
                tHuyendo = Mathf.Max(0f, tHuyendo - Time.deltaTime);
            }

            if (Time.time >= listoDisparo)
            {
                yield return Disparar();
                continue;
            }

            if (dx > distanciaIdeal + 1.5f && HaySueloDelante(0.6f) && !HayParedDelante(0.6f))
            {
                anim.Reproducir("andar");
                Andar(velocidad);
            }
            else
            {
                anim.Reproducir("quieto");
                Frenar(10f);
            }
            yield return null;
        }
    }

    private IEnumerator Disparar()
    {
        FrenarAtaque();
        anim.Reproducir("ataque", true);
        // El baculo se carga (aviso morado) hasta que suelta la bola.
        LanzarAviso(fotogramaDisparo / anim.Fps("ataque"), 0.45f);

        bool disparado = false;
        while (!anim.Terminado)
        {
            if (!disparado && anim.Fotograma >= fotogramaDisparo)
            {
                disparado = true;
                // Apunta en el momento de soltarla, no al empezar: moverse durante
                // la carga no basta, hay que moverse cuando la suelta.
                MirarYa();
                if (proyectil != null)
                {
                    Vector2 boca = (Vector2)transform.position + new Vector2(puntaBaculo.x * mirada, puntaBaculo.y) * Escala;
                    Vector2 objetivo = PosPlayer + Vector2.up * 0.1f;
                    ProyectilMagico p = Instantiate(proyectil, boca, Quaternion.identity);
                    p.Lanzar(objetivo - boca, transform, Dano(pesoBola));
                }
            }
            yield return null;
        }

        listoDisparo = Time.time + Random.Range(enfriamiento.x, enfriamiento.y);
    }

    private IEnumerator Estallido()
    {
        FrenarAtaque();
        anim.Reproducir("ataque", true, 0.8f);

        // Circulo de aviso en el suelo que crece y parpadea.
        MostrarCirculo(true);
        LanzarAviso(avisoEstallido, 0.6f);
        float t = 0f;
        while (t < avisoEstallido)
        {
            t += Time.deltaTime;
            float u = t / avisoEstallido;
            if (circuloAviso != null)
            {
                float d = radioEstallido * 2f * Mathf.Lerp(0.4f, 1f, u);
                circuloAviso.transform.localScale = new Vector3(d, d, 1f);
                Color c = colorEstallido;
                c.a = 0.25f + 0.35f * (0.5f + 0.5f * Mathf.Sin(t * 35f));
                circuloAviso.color = c;
            }
            yield return null;
        }
        MostrarCirculo(false);

        // Estallido: la explosion de la bola, grande, y el dano en el circulo.
        Vector2 centro = Alto(0.7f);
        float radio = radioEstallido * Escala;
        if (proyectil != null)
        {
            ProyectilMagico boom = Instantiate(proyectil, centro, Quaternion.identity);
            boom.Explotar(radio * 1.6f);
        }
        foreach (Collider2D c in Physics2D.OverlapCircleAll(centro, radio))
        {
            if (!c.CompareTag("Player")) continue;
            PlayerControler p = c.GetComponent<PlayerControler>();
            // Es magia: la reduce la resistencia a hechizos.
            if (p != null) p.TakeDamage(Dano(pesoEstallido), this, PlayerControler.TipoDano.Magico);
            break;
        }

        yield return EsperarClip(1.5f);
        tHuyendo = 0f;
        listoEstallido = Time.time + enfriamientoEstallido;
        listoDisparo = Mathf.Max(listoDisparo, Time.time + 0.8f);
    }

    private void MostrarCirculo(bool mostrar)
    {
        if (circuloAviso == null)
        {
            if (!mostrar) return;
            circuloAviso = new GameObject("AvisoEstallido").AddComponent<SpriteRenderer>();
            circuloAviso.transform.SetParent(transform, false);
            circuloAviso.transform.localPosition = new Vector3(0f, 0.7f, 0f);
            circuloAviso.sprite = CirculoSprite();
            circuloAviso.sortingLayerName = "VFX";
        }
        circuloAviso.gameObject.SetActive(mostrar);
    }

    protected override void AlInterrumpir()
    {
        base.AlInterrumpir();
        MostrarCirculo(false);
    }


    // Circulo de aviso generado por codigo (un anillo suave), sin asset.
    private static Sprite circulo;
    public static Sprite CirculoSprite()
    {
        if (circulo != null) return circulo;
        const int n = 64;
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n * 0.5f, n * 0.5f)) / (n * 0.5f);
            float a = d > 1f ? 0f : Mathf.Lerp(0.25f, 1f, Mathf.Clamp01((d - 0.75f) / 0.25f));
            if (d > 0.97f) a *= Mathf.Clamp01((1f - d) / 0.03f);
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        tex.Apply();
        circulo = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
        return circulo;
    }

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();
        Gizmos.color = new Color(0.7f, 0.3f, 1f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.7f, radioEstallido);
        Gizmos.color = Color.cyan;
        int m = Application.isPlaying ? mirada : 1;
        Gizmos.DrawWireSphere(transform.position + new Vector3(puntaBaculo.x * m, puntaBaculo.y, 0f), 0.1f);
    }
}
