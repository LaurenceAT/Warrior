using System.Collections.Generic;
using UnityEngine;

// Estados que dejan los golpes de la espada imbuida. Se anade solo al enemigo la
// primera vez que le llega uno (EstadosEnemigo.Aplicar).
//   - Fuego: quemadura, dano cada medio segundo durante un rato.
//   - Escarcha: ralentiza; cuatro golpes seguidos lo congelan un instante.
//   - Oscuridad: el drenaje lo hace el player (se cura con parte del dano).
//   - Sagrado: pequena probabilidad de aturdirlo (el mismo aturdimiento que un
//     parry, pero mas corto), con espera entre aturdimientos. Los jefes lo
//     guardan para despues de su ataque (IAturdible): nunca les corta un ataque.
//   - Sangrado (como en Elden Ring): cada golpe llena un contador; al llenarse
//     le quita de golpe una parte de su vida, y la siguiente vez le cuesta mas.
//     Los jefes pierden menos y solo sangran unas pocas veces por pelea. Si se
//     deja de golpear, el contador baja solo.
// Los numeros del sangrado, el sagrado y los fragmentos de hielo estan en
// Resources/AjustesProgreso.
// Los estados activos se ven como iconos pequenos bajo su barra de vida (la del
// enemigo o la grande del jefe). Cuando a uno le queda poco, parpadea.
public class EstadosEnemigo : MonoBehaviour
{
    public enum Estado { Quemado, Lento, Congelado, Aturdido, Sangrado, Drenado }

    // Un icono de la fila de estados: que estado es y si esta a punto de acabar.
    public struct Icono
    {
        public string clave;
        public bool acabando;
        // Solo el sangrado: cuanto lleva acumulado (0..1), para el borde del icono.
        public float carga;
    }

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

    // El contador de sangrado se llena en 100 (luego sube con cada sangrado).
    private const float SangradoMaximo = 100f;
    private static AjustesProgreso A => AjustesProgreso.Get();

    [Header("Iconos")]
    [Tooltip("Segundos antes de acabar en los que el icono parpadea.")]
    [SerializeField] private float avisoFin = 1.2f;

    private EnemyHealth salud;
    private Rigidbody2D rb;
    private AnimadorHoja anim;
    private SpriteRenderer sr;
    private Color colorBase = Color.white;

    private float finQuemadura, siguienteTick;
    private int danoQuemadura;
    private float finLento, finCongelado, finInmuneCongelar, finAturdido, siguienteAturdir, finDrenado;
    private int cargasEscarcha;
    private float ultimaEscarcha;
    private float sangrado, ultimoSangrado = -99f, finEstallido = -99f, maximoSangrado;
    private int vecesSangrado;
    private float siguienteChispa;

    private Vector2 ultimaVelocidad;
    private Transform iconos;
    private readonly List<SpriteRenderer> iconosSr = new List<SpriteRenderer>();
    private readonly List<Icono> activos = new List<Icono>(6);

    // 1 = normal; menos es mas lento; 0 = congelado. Lo leen las IA nuevas.
    public float Ritmo => Time.time < finCongelado ? 0f : Time.time < finLento ? lentoRitmo : 1f;
    public bool Congelado => Time.time < finCongelado;
    // Ya no hay dano extra por estados (lo hacia la corrosion del acido).
    public float MultiplicadorDano => 1f;
    public float SangradoFraccion => maximoSangrado > 0f ? Mathf.Clamp01(sangrado / maximoSangrado) : 0f;

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
        maximoSangrado = SangradoMaximo;
    }

    // Un jefe ya no puede sangrar mas en esta pelea.
    private bool SangradoAgotado => salud.Inamovible && vecesSangrado >= Mathf.Max(0, A.sangradoMaximoJefe);

    // En zonas avanzadas los estados se acumulan mas despacio (ficha del enemigo).
    private float Acumulacion => salud != null && salud.Definicion != null ? Mathf.Clamp(salud.Definicion.Zona.acumulacionEstados, 0.1f, 1f) : 1f;

    private void Recibir(Elemento e, int dano)
    {
        float ahora = Time.time;
        float acum = Acumulacion;
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
                if (ahora >= finInmuneCongelar && ++cargasEscarcha >= Mathf.CeilToInt(cargasParaCongelar / acum - 0.001f)) Congelar();
                break;

            case Elemento.Sagrado:
                if (ahora >= siguienteAturdir && Random.value < A.sagradoProbabilidad * acum)
                {
                    float dur = A.sagradoDuracion;
                    // Los jefes deciden cuando (despues de su ataque) y si pueden.
                    IAturdible jefe = GetComponent<IAturdible>();
                    if (jefe != null && !jefe.PedirAturdimiento(dur)) break;
                    if (jefe == null) salud.Stagger(dur);
                    siguienteAturdir = ahora + A.sagradoEspera + dur;
                    finAturdido = ahora + dur;
                    if (jefe == null) TextoFlotante.Mostrar("Aturdido", Encima(0.5f), Elementos.Color(e), 0.85f);
                    ParticulasFx.Rafaga(Encima(0f), 10, new Color(1f, 0.97f, 0.75f), Elementos.Color(e),
                                        new Vector2(1.2f, 2.5f), 0f, new Vector2(0.04f, 0.08f), new Vector2(0.3f, 0.5f));
                }
                break;

            case Elemento.Sangrado:
                if (SangradoAgotado) break;
                sangrado += A.sangradoPorGolpe * acum;
                ultimoSangrado = ahora;
                if (sangrado >= maximoSangrado) Desangrar();
                break;

            case Elemento.Oscuro:
                finDrenado = ahora + 1.2f;
                break;
        }
    }

    // El contador de sangrado se ha llenado: pierde una parte de su vida de golpe
    // y queda algo mas resistente para la proxima vez.
    private void Desangrar()
    {
        sangrado = 0f;
        finEstallido = Time.time + 0.6f;
        vecesSangrado++;
        bool jefe = salud.Inamovible;
        int dano = jefe ? Mathf.RoundToInt(salud.MaxHealth * A.sangradoFraccionJefe) + A.sangradoFijoJefe
                        : Mathf.RoundToInt(salud.MaxHealth * A.sangradoFraccion) + A.sangradoFijo;
        maximoSangrado *= Mathf.Max(1f, jefe ? A.sangradoResistenciaJefe : A.sangradoResistencia);
        Color c = Elementos.Color(Elemento.Sangrado);
        TextoFlotante.Mostrar("¡Sangrado!", Encima(0.5f), c, 1f);
        Sonido.Reproducir("sangrado_enemigo");
        Vector2 centro = sr != null ? (Vector2)sr.bounds.center : (Vector2)transform.position;
        ParticulasFx.Rafaga(centro, 30, c, new Color(0.45f, 0f, 0.05f), new Vector2(2f, 5f), 1.2f,
                            new Vector2(0.05f, 0.12f), new Vector2(0.4f, 0.9f));
        if (EstadosPlayer.efectoSangrado != null) EfectoVisual.Crear(EstadosPlayer.efectoSangrado, centro, 1.2f, Color.white);
        salud.DanoEstado(dano);
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
        // Solo unos fragmentos pequenos alrededor de su silueta (antes era un
        // bloque de hielo grande que tapaba al enemigo y sus ataques).
        Bounds b = sr != null ? sr.bounds : new Bounds(transform.position, Vector3.one);
        int n = Mathf.Max(0, A.hieloFragmentos);
        for (int i = 0; i < n; i++)
        {
            float ang = (i + Random.value * 0.6f) / Mathf.Max(1, n) * Mathf.PI * 2f;
            Vector2 borde = (Vector2)b.center + new Vector2(Mathf.Cos(ang) * b.extents.x, Mathf.Sin(ang) * b.extents.y) * 0.95f;
            ParticulasFx.Rafaga(borde, 1, new Color(0.85f, 0.97f, 1f), Elementos.Color(Elemento.Hielo),
                                new Vector2(0.4f, 1f), 0.6f, A.hieloTamano, new Vector2(A.hieloDuracion * 0.7f, A.hieloDuracion),
                                40f, ang * Mathf.Rad2Deg);
        }
    }

    public void Limpiar()
    {
        finQuemadura = finLento = finCongelado = finAturdido = finDrenado = 0f;
        sangrado = 0f;
        finEstallido = -99f;
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
        else if (sangrado > 0f && ahora >= siguienteChispa)
        {
            // Gotas de sangre, mas seguidas cuanto mas lleno esta el contador.
            siguienteChispa = ahora + Mathf.Lerp(0.5f, 0.12f, SangradoFraccion);
            ParticulasFx.Rafaga(Encima(-0.5f) + new Vector2(Random.Range(-0.3f, 0.3f), 0f), 1,
                                Elementos.Color(Elemento.Sangrado), new Color(0.4f, 0f, 0.04f),
                                new Vector2(0.2f, 0.6f), 1.2f, new Vector2(0.04f, 0.07f), new Vector2(0.4f, 0.7f));
        }

        // El sangrado baja solo si se deja de golpear.
        if (sangrado > 0f && ahora - ultimoSangrado > A.sangradoEspera)
            sangrado = Mathf.Max(0f, sangrado - A.sangradoBajada * Time.deltaTime);

        // Ritmo de la animacion y color de la escarcha.
        float ritmo = Ritmo;
        if (anim != null) anim.multiplicador = ritmo;
        if (sr != null)
        {
            Color objetivo = ritmo <= 0f ? new Color(0.55f, 0.85f, 1f, colorBase.a)
                           : ritmo < 1f ? Color.Lerp(colorBase, new Color(0.6f, 0.85f, 1f, colorBase.a), 0.6f)
                           : ahora < finQuemadura ? Color.Lerp(colorBase, new Color(1f, 0.6f, 0.45f, colorBase.a), 0.35f + 0.25f * Mathf.Sin(ahora * 20f))
                           : ahora < finEstallido ? Color.Lerp(colorBase, new Color(1f, 0.3f, 0.35f, colorBase.a), 0.6f)
                           : colorBase;
            sr.color = objetivo;
        }

        // Sin barra de vida donde ponerlos, los iconos flotan sobre la cabeza.
        if (salud.TieneBarra) { if (iconos != null) iconos.gameObject.SetActive(false); }
        else ActualizarIconosFlotantes();
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

    // Los estados que tiene ahora, en orden. La lista se reutiliza: no guardarla.
    public List<Icono> Activos()
    {
        activos.Clear();
        if (salud != null && salud.Muerto) return activos;
        float ahora = Time.time;
        Poner("estado_quemado", finQuemadura);
        if (ahora < finCongelado) Poner("estado_congelado", finCongelado);
        else Poner("estado_lento", finLento);
        Poner("estado_aturdido", finAturdido);
        if (sangrado > 0f || ahora < finEstallido)
        {
            // Acabando: bajando solo y casi vacio.
            bool bajando = ahora - ultimoSangrado > A.sangradoEspera;
            activos.Add(new Icono
            {
                clave = "estado_sangrado",
                acabando = ahora >= finEstallido && bajando && SangradoFraccion < 0.25f,
                carga = ahora < finEstallido ? 1f : SangradoFraccion,
            });
        }
        Poner("estado_drenado", finDrenado);
        return activos;

        void Poner(string clave, float fin)
        {
            if (ahora >= fin) return;
            activos.Add(new Icono { clave = clave, acabando = fin - ahora < avisoFin, carga = -1f });
        }
    }

    // Parpadeo rapido de un icono que se acaba (alfa).
    public static float Parpadeo(bool acabando) => acabando ? (Mathf.Repeat(Time.time * 7f, 1f) < 0.5f ? 1f : 0.25f) : 1f;

    private void ActualizarIconosFlotantes()
    {
        List<Icono> lista = Activos();
        if (lista.Count == 0)
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
        iconos.position = (Vector3)Encima(0.28f);

        const float lado = 0.3f;
        RecursosRPG r = RecursosRPG.Get();
        for (int i = 0; i < Mathf.Max(lista.Count, iconosSr.Count); i++)
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
            bool usar = i < lista.Count;
            s.gameObject.SetActive(usar);
            if (!usar) continue;
            s.sprite = r.Icono(lista[i].clave);
            if (s.sprite == null) { s.gameObject.SetActive(false); continue; }
            s.color = new Color(1f, 1f, 1f, Parpadeo(lista[i].acabando));
            float esc = lado / Mathf.Max(0.01f, s.sprite.bounds.size.x);
            s.transform.localScale = new Vector3(esc, esc, 1f);
            s.transform.localPosition = new Vector3((i - (lista.Count - 1) * 0.5f) * (lado + 0.04f), 0f, 0f);
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
