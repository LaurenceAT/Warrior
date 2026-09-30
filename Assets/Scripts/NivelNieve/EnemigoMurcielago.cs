using System.Collections;
using UnityEngine;

// Murcielago de las cumbres: volador nervioso que acosa desde arriba.
//   - Vuelo erratico alrededor del player, a distinta altura cada vez.
//   - Picado en arco: chilla (aviso), se lanza en curva pasando por donde esta
//     el player y vuelve a subir por el otro lado.
//   - Chillido: si el player se le acerca mucho, suelta una onda que empuja.
// Debil a la escarcha (lo frena y lo deja congelado en el aire); resiste el acido.
public class EnemigoMurcielago : EnemigoBase
{
    [SerializeField] private float velocidad = 4.2f;
    [SerializeField] private float altura = 2.6f;
    [Tooltip("Pesos de cada ataque (1 = el golpe principal de su ficha).")]
    [SerializeField] private float pesoPicado = 1f;
    [SerializeField] private float pesoChillido = 0.67f;
    [SerializeField] private Vector2 enfriamiento = new Vector2(1.8f, 3f);
    [Tooltip("Probabilidad de encadenar un segundo picado.")]
    [Range(0f, 1f)] [SerializeField] private float probabilidadDoble = 0.35f;
    [Tooltip("Tope de velocidad del picado (unidades/s).")]
    [SerializeField] private float velocidadPicadoMaxima = 16f;

    // El ultimo picado termino sin parry ni chocar con la roca.
    private bool ultimoPicadoLimpio;

    private Vector2 origen;
    private float listoPara;
    private float fase;
    private float siguienteChillido;

    protected override void Awake()
    {
        base.Awake();
        salud.Volador = true;
        rb.gravityScale = 0f;
        origen = transform.position;
        fase = Random.value * 10f;
        colorAviso = new Color(0.9f, 0.3f, 1f, 1f);
        listoPara = Time.time + Random.Range(1f, 2.5f);
    }

    protected override IEnumerator Cerebro()
    {
        rb.gravityScale = 0f;
        while (true)
        {
            anim.Reproducir("vuelo");
            if (!VerPlayer(transform.position))
            {
                Volar(origen + new Vector2(Mathf.Sin(Time.time * 0.8f + fase) * 1.5f, 0f), 0.4f);
                yield return null;
                continue;
            }
            MirarAlPlayer();

            // Ronda al player, cambiando de lado y altura.
            float t = Time.time * 0.9f + fase;
            Vector2 objetivo = PosPlayer + new Vector2(Mathf.Sin(t) * 3.2f, altura + Mathf.Sin(t * 2.3f) * 0.7f);
            Volar(objetivo, 1f);

            if (DistanciaPlayer < 1.6f && Time.time >= siguienteChillido) { yield return Chillido(); continue; }
            bool despejado = !Physics2D.Linecast(transform.position, player.position, capaSuelo);
            if (Time.time >= listoPara && despejado && DistanciaPlayer < 6.5f)
            {
                yield return Picado();
                // A veces encadena un segundo picado sin descanso (hay que seguir atento).
                if (ultimoPicadoLimpio && Random.value < probabilidadDoble && DistanciaPlayer < 6.5f)
                {
                    rb.linearVelocity = Vector2.up * 1.2f;
                    yield return EsperarRitmo(0.3f);
                    yield return Picado();
                }
                continue;
            }
            yield return null;
        }
    }

    private void Volar(Vector2 objetivo, float factor)
    {
        Vector2 hacia = LimitarZona(objetivo) - (Vector2)transform.position;
        Vector2 deseada = Vector2.ClampMagnitude(hacia * 2.2f, velocidad * factor) + Separacion2D();
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, deseada, Time.deltaTime * 4f);
    }

    private IEnumerator Picado()
    {
        armadura = false;
        rb.linearVelocity = Vector2.zero;
        anim.Reproducir("ataque", true, 0.6f);
        LanzarAviso(0.55f, 0.6f);
        Sonido.Reproducir("murcielago_chillido", 0.6f);
        yield return EsperarRitmo(0.55f);

        // Curva cuadratica: desde aqui, pasando por el player, hasta el otro lado.
        Vector2 a = transform.position;
        Vector2 medio = PosPlayer + Vector2.down * 0.3f;
        // La curva no baja hasta el suelo (la roca lo frenaba y se disparaba).
        RaycastHit2D suelo = Physics2D.Raycast(PosPlayer, Vector2.down, 3f, capaSuelo);
        if (suelo) medio.y = Mathf.Max(medio.y, suelo.point.y + 0.6f);
        Vector2 c = medio + new Vector2(medio.x - a.x, a.y - medio.y);
        // El punto de control que hace pasar la curva por "medio".
        Vector2 control = 2f * medio - 0.5f * (a + c);
        anim.Reproducir("ataque", true, 1.3f);
        bool pego = false;
        ultimoPicadoLimpio = false;
        bool limpio = true;
        const float dur = 0.9f;
        for (float t = 0f; t < dur; t += Time.deltaTime * Mathf.Max(0.35f, Ritmo))
        {
            float u = t / dur;
            Vector2 p = (1 - u) * (1 - u) * a + 2 * (1 - u) * u * control + u * u * c;
            // Si algo lo frena y se queda atras de su curva, corta el picado
            // (antes la velocidad para alcanzarla crecia sin limite y salia disparado).
            if (Vector2.Distance(p, transform.position) > 1.2f) { limpio = false; break; }
            Vector2 v = Vector2.ClampMagnitude((p - (Vector2)transform.position) / Mathf.Max(0.001f, Time.deltaTime), velocidadPicadoMaxima);
            rb.linearVelocity = v;
            Mirar(v.x > 0 ? 1 : -1);
            if (!pego)
            {
                var r = Golpear(Vector2.zero, new Vector2(0.9f, 0.8f), Dano(pesoPicado));
                if (r.HasValue)
                {
                    pego = true;
                    if (r.Value == PlayerControler.ResultadoDano.Parry) { rb.linearVelocity = Vector2.zero; limpio = false; break; }
                }
            }
            if (Physics2D.OverlapCircle(transform.position, 0.25f, capaSuelo)) { limpio = false; break; }
            yield return null;
        }
        ultimoPicadoLimpio = limpio;
        rb.linearVelocity *= 0.3f;
        listoPara = Time.time + Random.Range(enfriamiento.x, enfriamiento.y);
    }

    // Onda de chillido alrededor: poco dano, pero aparta al player.
    private IEnumerator Chillido()
    {
        siguienteChillido = Time.time + 3.5f;
        rb.linearVelocity = Vector2.zero;
        anim.Reproducir("ataque", true, 0.8f);
        LanzarAviso(0.35f, 0.7f);
        yield return EsperarRitmo(0.35f);
        Sonido.Reproducir("murcielago_onda", 0.7f);
        StartCoroutine(Onda());
        foreach (Collider2D c in Physics2D.OverlapCircleAll(transform.position, 1.7f))
        {
            if (!c.CompareTag("Player")) continue;
            PlayerControler p = c.GetComponent<PlayerControler>();
            if (p != null) { PlayerControler.SiguienteGolpeMagico = true; p.TakeDamage(Dano(pesoChillido), this); }
        }
        yield return EsperarRitmo(0.4f);
    }

    // El aro de la onda se crea una vez y se reutiliza.
    private SpriteRenderer aro;

    private IEnumerator Onda()
    {
        if (aro == null)
        {
            aro = new GameObject("OndaChillido").AddComponent<SpriteRenderer>();
            aro.sprite = RuedaImbuir.Anillo();
            aro.sortingLayerName = "VFX";
            aro.sharedMaterial = EfectoVisual.MaterialSinLuz();
        }
        aro.gameObject.SetActive(true);
        aro.transform.position = transform.position;
        for (float t = 0f; t < 0.35f; t += Time.deltaTime)
        {
            float d = Mathf.Lerp(0.5f, 3.6f, t / 0.35f);
            aro.transform.localScale = new Vector3(d, d, 1f);
            aro.color = new Color(0.85f, 0.6f, 1f, 1f - t / 0.35f);
            yield return null;
        }
        aro.gameObject.SetActive(false);
    }

    protected override void AlMorir()
    {
        base.AlMorir();
        if (aro != null) aro.gameObject.SetActive(false);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (aro != null) Destroy(aro.gameObject);
    }
}
