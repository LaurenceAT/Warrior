using System.Collections;
using UnityEngine;

// Cacodemonio: el enemigo volador.
//
// Flota por encima del player, a un lado, meciendose. Cada cierto tiempo se para,
// abre la boca y ruge (aviso: destello rojo y temblor). El rugido desprende las
// estalactitas cercanas, que caen sobre quien este debajo (tambien sobre el
// player). Despues se lanza en picado recto hacia donde estaba el player:
//   - El picado tiene armadura: los golpes le quitan vida pero no lo paran.
//   - Un parry si lo para en seco y lo deja aturdido.
//   - Si se estrella contra la roca queda aturdido un momento: castigo.
public class EnemigoCacodemonio : EnemigoBase
{
    [Header("Vuelo")]
    [SerializeField] private float alturaSobrePlayer = 2.8f;
    [SerializeField] private float separacion = 3.2f;
    [SerializeField] private float velocidad = 3.5f;
    [SerializeField] private float respuesta = 2.2f;
    [SerializeField] private float amplitudVaiven = 0.25f;

    [Header("Picado")]
    [SerializeField] private float aviso = 0.7f;
    [SerializeField] private float velocidadPicado = 11f;
    [SerializeField] private float duracionMaxima = 1f;
    [Tooltip("Peso del picado (1 = el golpe principal de su ficha).")]
    [SerializeField] private float pesoPicado = 1f;
    [SerializeField] private Vector2 tamanoGolpe = new Vector2(1.1f, 1f);
    [SerializeField] private Vector2 enfriamiento = new Vector2(2.2f, 3.2f);
    [SerializeField] private float aturdidoAlChocar = 0.9f;

    [Header("Rugido")]
    // Las estalactitas a esta distancia se desprenden con el rugido.
    [SerializeField] private float radioRugido = 5.5f;

    private Vector2 origen;
    private float listoPara;
    private float fase;

    protected override void Awake()
    {
        base.Awake();
        colorAviso = new Color(1f, 0.15f, 0.1f, 1f);
        salud.Volador = true;
        rb.gravityScale = 0f;
        origen = transform.position;
        fase = Random.value * 10f;
        listoPara = Time.time + Random.Range(1f, 2f);
    }

    protected override IEnumerator Cerebro()
    {
        rb.gravityScale = 0f;
        armadura = false;

        while (true)
        {
            Vector2 ojos = transform.position;
            anim.Reproducir("vuelo");

            if (!VerPlayer(ojos))
            {
                // Sin player: vuelve despacio a su sitio y se mece.
                Volar(origen, 0.5f);
                yield return null;
                continue;
            }

            MirarAlPlayer();

            // Punto de vuelo: sobre el player, en el lado en el que ya esta.
            int lado = transform.position.x >= player.position.x ? 1 : -1;
            Vector2 objetivo = PosPlayer + new Vector2(lado * separacion, alturaSobrePlayer);
            Volar(objetivo, 1f);

            bool aTiro = DistanciaPlayer < 7.5f && !Physics2D.Linecast(transform.position, player.position, capaSuelo);
            if (Time.time >= listoPara && aTiro)
            {
                yield return Picado();
                continue;
            }
            yield return null;
        }
    }

    private void Volar(Vector2 objetivo, float intensidad)
    {
        Vector2 p = transform.position;
        objetivo = LimitarZona(objetivo);
        objetivo.y += Mathf.Sin((Time.time + fase) * 2.2f) * amplitudVaiven;
        Vector2 deseada = Vector2.ClampMagnitude((objetivo - p) * respuesta, velocidad * intensidad) + Separacion2D();
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, deseada, Time.deltaTime * 5f);
    }

    private IEnumerator Picado()
    {
        // Aviso: se frena, abre la boca y tiembla. El rugido suelta las estalactitas.
        anim.Reproducir("rugido", true);
        LanzarAviso(aviso, 0.65f);
        SoltarEstalactitas(transform.position, radioRugido);

        Vector3 base0 = visual.localPosition;
        float t = 0f;
        while (t < aviso)
        {
            t += Time.deltaTime;
            rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, Vector2.zero, Time.deltaTime * 8f);
            visual.localPosition = base0 + (Vector3)Random.insideUnitCircle * 0.05f;
            MirarAlPlayer();
            yield return null;
        }
        visual.localPosition = base0;

        // Picado recto hacia donde estaba el player al acabar el aviso.
        Vector2 inicio = transform.position;
        Vector2 destino = PosPlayer;
        Vector2 dir = (destino - inicio).normalized;
        float recorrido = Vector2.Distance(inicio, destino) + 2.5f;
        Mirar(dir.x >= 0f ? 1 : -1);

        armadura = true;
        bool pego = false;
        t = 0f;
        while (t < duracionMaxima && Vector2.Distance(inicio, transform.position) < recorrido)
        {
            t += Time.deltaTime;

            // Contra la roca: se estrella y queda aturdido.
            if (Physics2D.CircleCast(transform.position, 0.45f, dir, 0.25f, capaSuelo))
            {
                armadura = false;
                yield return Estrellado(dir);
                yield break;
            }

            rb.linearVelocity = dir * velocidadPicado;

            if (!pego)
            {
                var r = Golpear(Vector2.zero, tamanoGolpe, Dano(pesoPicado));
                if (r.HasValue)
                {
                    pego = true;
                    if (r.Value == PlayerControler.ResultadoDano.Parry)
                    {
                        // El parry ya lo ha aturdido (EnemyHealth.Stagger); aqui se
                        // corta el picado en seco y se deja caer un poco.
                        armadura = false;
                        rb.linearVelocity = -dir * 2f;
                        yield return Aturdido(0f);
                        yield break;
                    }
                }
            }
            yield return null;
        }

        armadura = false;

        // Remonta frenando.
        t = 0f;
        while (t < 0.45f)
        {
            t += Time.deltaTime;
            rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, Vector2.up * 1.5f, Time.deltaTime * 6f);
            yield return null;
        }
        listoPara = Time.time + Random.Range(enfriamiento.x, enfriamiento.y);
    }

    private IEnumerator Estrellado(Vector2 dir)
    {
        rb.linearVelocity = -dir * 3f;
        // El golpe contra la roca tambien desprende lo que haya cerca.
        SoltarEstalactitas(transform.position, radioRugido * 0.6f);
        ParticulasFx.Rafaga(transform.position, 10, new Color(0.5f, 0.42f, 0.36f), new Color(0.3f, 0.25f, 0.22f),
                            new Vector2(2f, 4f), 1.5f, new Vector2(0.06f, 0.12f), new Vector2(0.3f, 0.6f));
        yield return Aturdido(aturdidoAlChocar);
    }

    private IEnumerator Aturdido(float extra)
    {
        anim.Reproducir("golpe", true);
        float t = 0f;
        while (t < Mathf.Max(extra, anim.Duracion("golpe")) || salud.Aturdido)
        {
            t += Time.deltaTime;
            rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, Vector2.zero, Time.deltaTime * 4f);
            yield return null;
        }
        listoPara = Time.time + Random.Range(enfriamiento.x, enfriamiento.y);
        Pensar();
    }

    protected override void AlInterrumpir()
    {
        base.AlInterrumpir();
        armadura = false;
        visual.localPosition = Vector3.zero;
    }

    protected override void AlMorir()
    {
        base.AlMorir();
        // Cae mientras muere (EnemyHealth apaga el cuerpo; se mueve el dibujo).
        StartCoroutine(CaerMuerto());
    }

    private IEnumerator CaerMuerto()
    {
        float v = 0f;
        float t = 0f;
        while (t < 1.2f && visual != null)
        {
            t += Time.deltaTime;
            v += 12f * Time.deltaTime;
            if (!Physics2D.Raycast(visual.position, Vector2.down, 0.4f, capaSuelo))
                visual.position += Vector3.down * v * Time.deltaTime;
            yield return null;
        }
    }

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();
        Gizmos.color = new Color(1f, 0.3f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, radioRugido);
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(transform.position, tamanoGolpe);
    }
}
