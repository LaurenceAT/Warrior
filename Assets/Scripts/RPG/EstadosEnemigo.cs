using System.Collections.Generic;
using UnityEngine;

// Estados que dejan los golpes de la espada imbuida. Se anade solo al enemigo la
// primera vez que le llega uno (EstadosEnemigo.Aplicar).
//   - Fuego: quemadura, dano cada medio segundo durante un rato.
//   - Escarcha: ralentiza; cuatro golpes seguidos lo congelan un instante.
//   - Oscuridad: el drenaje lo hace el player (se cura con parte del dano).
//   - Sagrado: probabilidad de aturdirlo (con espera entre aturdimientos).
//   - Acido: corrosion, cada carga hace que reciba mas dano.
// Encima del enemigo flotan los iconos de los estados activos.
public class EstadosEnemigo : MonoBehaviour
{
    public enum Estado { Quemado, Lento, Congelado, Aturdido, Corroido, Drenado }

    [Header("Quemadura")]
    [SerializeField] private float quemaduraDuracion = 3.5f;
    [SerializeField] private float quemaduraIntervalo = 0.5f;
    // Dano por tick: una parte del golpe que la provoco, con un minimo.
    [SerializeField] private float quemaduraFraccion = 0.2f;

    [Header("Escarcha")]
    [SerializeField] private float lentoDuracion = 3f;
    [Range(0f, 1f)] [SerializeField] private float lentoRitmo = 0.55f;
    [SerializeField] private int cargasParaCongelar = 4;
    [SerializeField] private float congeladoDuracion = 1.4f;
    // Tras congelarse no se vuelve a congelar en un rato (se puede ralentizar).
    [SerializeField] private float inmuneCongelar = 5f;

    [Header("Sagrado")]
    [Range(0f, 1f)] [SerializeField] private float probAturdir = 0.22f;
    [SerializeField] private float aturdidoDuracion = 0.8f;
    [SerializeField] private float esperaAturdir = 2.5f;

    [Header("Acido")]
    [SerializeField] private int maxCorrosion = 4;
    [SerializeField] private float corrosionPorCarga = 0.08f;
    [SerializeField] private float corrosionDuracion = 5f;

    private EnemyHealth salud;
    private Rigidbody2D rb;
    private AnimadorHoja anim;
    private SpriteRenderer sr;
    private Color colorBase = Color.white;

    private float finQuemadura, siguienteTick;
    private int danoQuemadura;
    private float finLento, finCongelado, finInmuneCongelar, finAturdido, siguienteAturdir, finCorrosion, finDrenado;
    private int cargasEscarcha;
    private float ultimaEscarcha;
    private int corrosion;
    private float siguienteChispa;

    private Vector2 ultimaVelocidad;
    private Transform iconos;
    private readonly List<SpriteRenderer> iconosSr = new List<SpriteRenderer>();

    // 1 = normal; menos es mas lento; 0 = congelado. Lo leen las IA nuevas.
    public float Ritmo => Time.time < finCongelado ? 0f : Time.time < finLento ? lentoRitmo : 1f;
    public bool Congelado => Time.time < finCongelado;
    // Dano extra por la corrosion.
    public float MultiplicadorDano => 1f + (Time.time < finCorrosion ? corrosion * corrosionPorCarga : 0f);

    public static float RitmoDe(Component c)
    {
        EstadosEnemigo e = c.GetComponent<EstadosEnemigo>();
        return e != null ? e.Ritmo : 1f;
    }

    // Lo llama el player tras un golpe imbuido que ha entrado. "dano" es lo que
    // ha quitado el golpe; "afinidad" el multiplicador del enemigo contra ese
    // elemento (con 0.5 o menos, resiste y no sufre el estado).
    public static void Aplicar(EnemyHealth objetivo, Elemento e, int dano)
    {
        if (objetivo == null || objetivo.Muerto || e == Elemento.Ninguno) return;
        IAfinidadElemental af = objetivo.GetComponent<IAfinidadElemental>();
        if (af != null && af.Multiplicador(e) <= 0.5f) return;
        EstadosEnemigo est = objetivo.GetComponent<EstadosEnemigo>();
        if (est == null) est = objetivo.gameObject.AddComponent<EstadosEnemigo>();
        est.Recibir(e, dano);
    }

    private void Awake()
    {
        salud = GetComponent<EnemyHealth>();
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponentInChildren<AnimadorHoja>();
        sr = anim != null ? anim.destino : GetComponentInChildren<SpriteRenderer>();
        if (sr != null) colorBase = sr.color;
    }

    private void Recibir(Elemento e, int dano)
    {
        float ahora = Time.time;
        switch (e)
        {
            case Elemento.Fuego:
                danoQuemadura = Mathf.Max(2, Mathf.RoundToInt(dano * quemaduraFraccion));
                if (ahora >= finQuemadura) siguienteTick = ahora + quemaduraIntervalo;
                finQuemadura = ahora + quemaduraDuracion;
                break;

            case Elemento.Hielo:
                finLento = ahora + lentoDuracion;
                if (ahora - ultimaEscarcha > 3f) cargasEscarcha = 0;
                ultimaEscarcha = ahora;
                if (ahora >= finInmuneCongelar && ++cargasEscarcha >= cargasParaCongelar) Congelar();
                break;

            case Elemento.Sagrado:
                if (ahora >= siguienteAturdir && Random.value < probAturdir)
                {
                    siguienteAturdir = ahora + esperaAturdir;
                    finAturdido = ahora + aturdidoDuracion;
                    salud.Stagger(aturdidoDuracion);
                    TextoFlotante.Mostrar("Aturdido", Encima(0.5f), Elementos.Color(e), 0.85f);
                    ParticulasFx.Rafaga(Encima(0f), 14, new Color(1f, 0.95f, 0.6f), Elementos.Color(e),
                                        new Vector2(1.5f, 3f), 0f, new Vector2(0.05f, 0.1f), new Vector2(0.3f, 0.6f));
                }
                break;

            case Elemento.Acido:
                if (ahora >= finCorrosion) corrosion = 0;
                corrosion = Mathf.Min(maxCorrosion, corrosion + 1);
                finCorrosion = ahora + corrosionDuracion;
                break;

            case Elemento.Oscuro:
                finDrenado = ahora + 1.2f;
                break;
        }
    }

    private void Congelar()
    {
        cargasEscarcha = 0;
        float dur = congeladoDuracion;
        // Los enemigos que no se mueven con los golpes (jefes) se congelan menos.
        if (salud.Inamovible) dur *= 0.4f;
        finCongelado = Time.time + dur;
        finInmuneCongelar = Time.time + inmuneCongelar;
        salud.Stagger(dur);
        Sonido.Reproducir("hielo_congelar");
        TextoFlotante.Mostrar("¡Congelado!", Encima(0.5f), Elementos.Color(Elemento.Hielo), 0.95f);
        ParticulasFx.Rafaga(Encima(-0.2f), 22, new Color(0.8f, 0.95f, 1f), Elementos.Color(Elemento.Hielo),
                            new Vector2(1f, 3.5f), 1f, new Vector2(0.05f, 0.12f), new Vector2(0.4f, 0.8f));
        RecursosRPG r = RecursosRPG.Get();
        if (r.fxCongelado != null && sr != null)
            EfectoVisual.Crear(r.fxCongelado, new Vector2(sr.bounds.center.x, sr.bounds.min.y), Mathf.Clamp(sr.bounds.size.x * 0.9f, 0.8f, 2.5f),
                               new Color(1f, 1f, 1f, 0.9f), false, -1f, "VFX", 4);
    }

    public void Limpiar()
    {
        finQuemadura = finLento = finCongelado = finAturdido = finCorrosion = finDrenado = 0f;
        if (anim != null) anim.multiplicador = 1f;
        if (sr != null) sr.color = colorBase;
        if (iconos != null) iconos.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (salud == null || salud.Muerto) return;
        float ahora = Time.time;

        // Quemadura.
        if (ahora < finQuemadura)
        {
            if (ahora >= siguienteTick)
            {
                siguienteTick = ahora + quemaduraIntervalo;
                salud.DanoEstado(danoQuemadura);
            }
            if (ahora >= siguienteChispa)
            {
                siguienteChispa = ahora + 0.12f;
                ParticulasFx.Rafaga(Encima(-0.4f) + new Vector2(Random.Range(-0.3f, 0.3f), 0f), 3,
                                    new Color(1f, 0.55f, 0.15f), new Color(1f, 0.2f, 0.05f),
                                    new Vector2(0.6f, 1.4f), -0.6f, new Vector2(0.05f, 0.1f), new Vector2(0.3f, 0.6f), 60f, 90f);
            }
        }
        else if (ahora < finCorrosion && ahora >= siguienteChispa)
        {
            siguienteChispa = ahora + 0.2f;
            ParticulasFx.Rafaga(Encima(-0.4f) + new Vector2(Random.Range(-0.3f, 0.3f), 0f), 2,
                                new Color(0.6f, 1f, 0.2f), new Color(0.3f, 0.7f, 0.1f),
                                new Vector2(0.2f, 0.6f), 0.4f, new Vector2(0.05f, 0.08f), new Vector2(0.4f, 0.7f));
        }

        // Ritmo de la animacion y color de la escarcha.
        float ritmo = Ritmo;
        if (anim != null) anim.multiplicador = ritmo;
        if (sr != null)
        {
            Color objetivo = ritmo <= 0f ? new Color(0.55f, 0.85f, 1f, colorBase.a)
                           : ritmo < 1f ? Color.Lerp(colorBase, new Color(0.6f, 0.85f, 1f, colorBase.a), 0.6f)
                           : ahora < finQuemadura ? Color.Lerp(colorBase, new Color(1f, 0.6f, 0.45f, colorBase.a), 0.35f + 0.25f * Mathf.Sin(ahora * 20f))
                           : ahora < finCorrosion ? Color.Lerp(colorBase, new Color(0.7f, 1f, 0.5f, colorBase.a), 0.35f)
                           : colorBase;
            sr.color = objetivo;
        }

        ActualizarIconos();
    }

    // Frena el movimiento sin pelearse con la IA: solo se escala la velocidad
    // cuando la IA la ha cambiado desde el ultimo paso (si no, la frenaria dos veces).
    private void FixedUpdate()
    {
        if (rb == null || !rb.simulated || salud.IsAirborne) return;
        float ritmo = Ritmo;
        if (ritmo >= 1f) { ultimaVelocidad = rb.linearVelocity; return; }
        Vector2 v = rb.linearVelocity;
        if ((v - ultimaVelocidad).sqrMagnitude > 0.0001f)
        {
            v.x *= ritmo;
            if (rb.gravityScale == 0f) v.y *= ritmo;
            rb.linearVelocity = v;
        }
        ultimaVelocidad = rb.linearVelocity;
    }

    // ------------------------------------------------------------------ Iconos

    private void ActualizarIconos()
    {
        float ahora = Time.time;
        List<string> activos = new List<string>(4);
        if (ahora < finQuemadura) activos.Add("estado_quemado");
        if (ahora < finCongelado) activos.Add("estado_congelado");
        else if (ahora < finLento) activos.Add("estado_lento");
        if (ahora < finAturdido) activos.Add("estado_aturdido");
        if (ahora < finCorrosion) activos.Add("estado_corroido");
        if (ahora < finDrenado) activos.Add("estado_drenado");

        if (activos.Count == 0)
        {
            if (iconos != null) iconos.gameObject.SetActive(false);
            return;
        }
        if (iconos == null)
        {
            iconos = new GameObject("IconosEstado").transform;
            iconos.SetParent(transform, false);
        }
        iconos.gameObject.SetActive(true);
        // Sin girar con el enemigo ni heredar su escala volteada.
        Vector3 e = transform.lossyScale;
        iconos.localScale = new Vector3(1f / Mathf.Max(0.01f, Mathf.Abs(e.x)) * Mathf.Sign(e.x), 1f / Mathf.Max(0.01f, Mathf.Abs(e.y)), 1f);
        iconos.position = (Vector3)Encima(0.28f + 0.04f * Mathf.Sin(ahora * 4f));

        const float lado = 0.34f;
        RecursosRPG r = RecursosRPG.Get();
        for (int i = 0; i < Mathf.Max(activos.Count, iconosSr.Count); i++)
        {
            if (i >= iconosSr.Count)
            {
                SpriteRenderer n = new GameObject("Icono").AddComponent<SpriteRenderer>();
                n.transform.SetParent(iconos, false);
                n.sortingLayerName = "VFX";
                n.sortingOrder = 40;
                n.sharedMaterial = EfectoVisual.MaterialSinLuz();
                iconosSr.Add(n);
            }
            SpriteRenderer s = iconosSr[i];
            bool usar = i < activos.Count;
            s.gameObject.SetActive(usar);
            if (!usar) continue;
            s.sprite = r.Icono(activos[i]);
            if (s.sprite == null) { s.gameObject.SetActive(false); continue; }
            float esc = lado / Mathf.Max(0.01f, s.sprite.bounds.size.x);
            s.transform.localScale = new Vector3(esc, esc, 1f);
            s.transform.localPosition = new Vector3((i - (activos.Count - 1) * 0.5f) * (lado + 0.04f), 0f, 0f);
        }
    }

    // Punto por encima de la cabeza (a la altura de la barra de vida).
    private Vector2 Encima(float extra)
    {
        Vector2 baseY = salud != null ? salud.OffsetBarra : new Vector2(0f, 1f);
        if (sr != null && (salud == null || !salud.TieneBarra)) baseY = new Vector2(0f, sr.bounds.max.y - transform.position.y);
        return (Vector2)transform.position + new Vector2(0f, baseY.y + 0.25f + extra);
    }
}
