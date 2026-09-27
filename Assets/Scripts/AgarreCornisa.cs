using System.Collections;
using UnityEngine;

// Agarre de cornisa, como en Blasphemous: saltando contra una pared cuyo borde
// queda un poco por encima del alcance, el personaje se engancha al borde y
// sube de un tiron (rapido, no una escalada lenta).
//
// Se comprueba en el aire, empujando hacia la pared (o yendo hacia ella), y
// solo si arriba del borde cabe el personaje. Mientras cuelga y sube, el
// PlayerControler no hace nada mas; la animacion la pone este script con los
// fotogramas de Cornisa-Agarrar y Cornisa-Trepar (por encima del Animator).
[RequireComponent(typeof(PlayerControler))]
public class AgarreCornisa : MonoBehaviour
{
    [Header("Deteccion")]
    [Tooltip("Apagalo para quitar el agarre de cornisa sin tocar nada mas.")]
    [SerializeField] private bool activado = true;
    [Tooltip("Cuanto por encima de la cabeza puede estar el borde para agarrarlo.")]
    [SerializeField] private float alcanceSobreCabeza = 0.8f;
    [Tooltip("Cuanto por debajo del centro puede estar el borde (bordes bajos en los que uno se queda trabado).")]
    [SerializeField] private float margenBajo = 0.35f;
    [Tooltip("Distancia a la pared para engancharse.")]
    [SerializeField] private float distanciaPared = 0.4f;
    [Tooltip("Solo se engancha subiendo despacio o cayendo (velocidad vertical maxima).")]
    [SerializeField] private float velocidadSubidaMaxima = 3f;
    [Tooltip("Tras soltarse o terminar, cuanto espera para poder volver a engancharse.")]
    [SerializeField] private float espera = 0.25f;

    [Header("Cuerpo del personaje")]
    [SerializeField] private float altoCabeza = 0.42f;
    [SerializeField] private float altoPies = 0.625f;
    [SerializeField] private float medioAncho = 0.24f;
    [Tooltip("Donde queda el centro del personaje colgado, respecto al borde (y hacia abajo).")]
    [SerializeField] private float colgadoBajoBorde = 0.6f;

    [Header("Tiempos")]
    [Tooltip("Lo que dura el enganche antes de subir.")]
    [SerializeField] private float duracionAgarre = 0.1f;
    [Tooltip("Lo que dura la subida.")]
    [SerializeField] private float duracionSubida = 0.26f;

    [Header("Animacion y sonido")]
    [SerializeField] private Sprite[] fotogramasAgarre;
    [SerializeField] private Sprite[] fotogramasSubida;
    [SerializeField] private string sonidoAgarre = "aterrizaje";
    [SerializeField] private string sonidoSubida = "esfuerzo";

    private PlayerControler player;
    private Rigidbody2D rb;
    private SpriteRenderer sr;
    private Sprite spriteForzado;
    private float siguiente;

    public bool Activo { get; private set; }

    private void Awake()
    {
        player = GetComponent<PlayerControler>();
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();
    }

    private void OnDisable()
    {
        if (Activo) Terminar();
    }

    // Lo llama el PlayerControler en cada paso de fisica. Si hay borde, se engancha.
    public bool Intentar()
    {
        if (!activado || Activo || Time.time < siguiente || !player.PuedeAgarrarCornisa) return false;
        if (rb.linearVelocityY > velocidadSubidaMaxima) return false;

        // Hacia donde: hacia donde se empuja; sin input, hacia donde se mueve.
        float ex = player.EntradaMovimiento.x;
        int lado = Mathf.Abs(ex) > 0.3f ? (int)Mathf.Sign(ex) : Mathf.Abs(rb.linearVelocityX) > 1f ? (int)Mathf.Sign(rb.linearVelocityX) : 0;
        if (lado == 0) return false;

        if (!BuscarBorde(lado, out Vector2 esquina)) return false;
        StartCoroutine(Subir(lado, esquina));
        return true;
    }

    // Pared delante a la altura del pecho, borde con el hueco libre encima y sitio
    // para ponerse de pie arriba.
    private bool BuscarBorde(int lado, out Vector2 esquina)
    {
        esquina = Vector2.zero;
        LayerMask capa = player.CapaSuelo;
        Vector2 p = rb.position;
        Vector2 dir = Vector2.right * lado;

        // Pared: se prueba a la altura de los pies y del pecho (vale cualquiera).
        RaycastHit2D pared = default;
        foreach (float h in new[] { 0.05f, -0.3f })
        {
            pared = Physics2D.Raycast(p + Vector2.up * h, dir, medioAncho + distanciaPared, capa);
            if (pared && Mathf.Abs(pared.normal.x) > 0.9f) break;
        }
        if (!pared || Mathf.Abs(pared.normal.x) <= 0.9f) return false;

        // La parte alta del borde: rayo hacia abajo justo pasado la cara de la pared.
        float techo = p.y + altoCabeza + alcanceSobreCabeza;
        float x = pared.point.x + lado * 0.12f;
        float largo = techo - (p.y - margenBajo);
        RaycastHit2D arriba = Physics2D.Raycast(new Vector2(x, techo), Vector2.down, largo, capa);
        // Si el rayo empieza dentro de la roca, el borde esta demasiado alto.
        if (!arriba || arriba.distance < 0.02f || arriba.normal.y < 0.7f) return false;
        float bordeY = arriba.point.y;

        // Tiene que haber hueco para la cabeza del personaje al lado de la pared
        // (si no, es un techo bajo, no un borde) y sitio para estar de pie arriba.
        if (Physics2D.Raycast(new Vector2(p.x, p.y), Vector2.up, Mathf.Max(0.05f, bordeY + 0.1f - p.y), capa)) return false;
        Vector2 centroArriba = new Vector2(pared.point.x + lado * (medioAncho + 0.15f), bordeY + altoPies + 0.05f);
        if (Physics2D.OverlapBox(centroArriba, new Vector2(medioAncho * 1.8f, altoCabeza + altoPies - 0.1f), 0f, capa)) return false;

        esquina = new Vector2(pared.point.x, bordeY);
        return true;
    }

    private IEnumerator Subir(int lado, Vector2 esquina)
    {
        Activo = true;
        player.MirarHacia(lado);

        RigidbodyType2D tipo = rb.bodyType;
        rb.linearVelocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Kinematic;

        Vector2 colgado = new Vector2(esquina.x - lado * (medioAncho + 0.02f), esquina.y - colgadoBajoBorde);
        Vector2 arriba = new Vector2(esquina.x + lado * (medioAncho + 0.12f), esquina.y + altoPies + 0.03f);
        Vector2 inicio = rb.position;

        Sonido.Reproducir(sonidoAgarre, 0.6f, 1.15f);

        // Enganche: se ajusta al borde en un instante.
        for (float t = 0f; t < duracionAgarre; t += Time.deltaTime)
        {
            if (player.Interrumpido) { Terminar(tipo); yield break; }
            float k = Mathf.Clamp01(t / (duracionAgarre * 0.5f));
            rb.MovePosition(Vector2.Lerp(inicio, colgado, 1f - (1f - k) * (1f - k)));
            spriteForzado = Fotograma(fotogramasAgarre, t / duracionAgarre);
            yield return null;
        }

        Sonido.Reproducir(sonidoSubida, 0.5f);

        // Subida: primero arriba y luego hacia dentro, sin atravesar la esquina.
        for (float t = 0f; t < duracionSubida; t += Time.deltaTime)
        {
            if (player.Interrumpido) { Terminar(tipo); yield break; }
            float k = t / duracionSubida;
            float ky = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k / 0.7f));
            float kx = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((k - 0.45f) / 0.55f));
            rb.MovePosition(new Vector2(Mathf.Lerp(colgado.x, arriba.x, kx), Mathf.Lerp(colgado.y, arriba.y, ky)));
            spriteForzado = Fotograma(fotogramasSubida, k);
            yield return null;
        }
        rb.position = arriba;
        transform.position = arriba;
        Terminar(tipo);
    }

    private void Terminar(RigidbodyType2D tipo = RigidbodyType2D.Dynamic)
    {
        StopAllCoroutines();
        rb.bodyType = tipo;
        rb.linearVelocity = Vector2.zero;
        spriteForzado = null;
        Activo = false;
        siguiente = Time.time + espera;
    }

    private static Sprite Fotograma(Sprite[] f, float k)
    {
        if (f == null || f.Length == 0) return null;
        return f[Mathf.Clamp(Mathf.FloorToInt(k * f.Length), 0, f.Length - 1)];
    }

    // Despues del Animator: el fotograma de la cornisa manda sobre el suyo.
    private void LateUpdate()
    {
        if (Activo && spriteForzado != null && sr != null) sr.sprite = spriteForzado;
    }
}
