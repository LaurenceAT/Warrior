using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Base de todos los enemigos normales. La vida, el retroceso, el combo aereo y
// la barra los lleva EnemyHealth (con su ficha); aqui va lo comun de la IA:
//   - Un "cerebro" (corrutina) que cada enemigo escribe a su manera.
//   - Estados con nombre (EstadoIA, lo ensena la depuracion): Patrulla, Alerta,
//     Persecucion, Anticipacion, Ataque, Salto, Recuperacion, Reposicion,
//     Golpeado, Regreso y Dormido.
//   - Movimiento vivo: acelera y frena (no cambia de velocidad de golpe), se
//     gira con un pequeno retraso, reacciona un instante al descubrir al player,
//     se separa de los otros enemigos, vuelve a su zona si te pierde y respira
//     en reposo. Cada individuo tiene tiempos algo distintos.
//   - Reaccion a los golpes segun su rol (ficha): los pesados aguantan varios
//     golpes antes de pararse, y tras pararlo hay un rato sin interrupciones
//     (sin aturdimiento infinito). El parry, el sagrado y el congelado lo paran
//     siempre, aunque tenga armadura.
//   - Salto comun: solo desde el suelo, altura con tope, se agacha antes y se
//     recupera al caer; nunca salta fuera de su zona ni por un borde.
//   - Lejos de la camara se duerme (no piensa ni se mueve).
//
// El sprite va en un hijo ("Visual"): se voltea el hijo, no el objeto, para que
// el golpe de escala del HitFlash no pise el volteo.
[RequireComponent(typeof(Rigidbody2D), typeof(EnemyHealth))]
public abstract class EnemigoBase : MonoBehaviour
{
    [Header("Base")]
    [SerializeField] protected AnimadorHoja anim;
    [SerializeField] protected Transform visual;
    [SerializeField] protected float radioDeteccion = 7f;
    // Una vez visto, lo sigue hasta esta distancia aunque pierda la vision.
    [SerializeField] protected float radioOlvido = 11f;
    [SerializeField] protected LayerMask capaSuelo;
    // Los sprites de los packs miran a la derecha.
    [SerializeField] protected bool spriteMiraDerecha = true;

    [Header("Aviso de ataque")]
    [SerializeField] protected Color colorAviso = new Color(1f, 0.55f, 0.15f, 1f);

    [Header("Movimiento")]
    [Tooltip("Aceleracion al arrancar (unidades/s por segundo). Los pesados la tienen menor segun su rol.")]
    [SerializeField] protected float aceleracion = 16f;
    [Tooltip("Frenada al pararse (unidades/s por segundo).")]
    [SerializeField] protected float frenada = 24f;
    [Tooltip("Retraso al girarse hacia el player (segundos, minimo y maximo).")]
    [SerializeField] protected Vector2 retrasoGiro = new Vector2(0.08f, 0.2f);
    [Tooltip("Quieto al descubrir al player antes de actuar (segundos, minimo y maximo).")]
    [SerializeField] protected Vector2 reaccionAlerta = new Vector2(0.25f, 0.45f);
    [Tooltip("Distancia que guarda con otros enemigos (a escala 1).")]
    [SerializeField] protected float distanciaGrupo = 0.8f;
    [Tooltip("Respiracion en reposo (0 = nada).")]
    [SerializeField] protected float respiracion = 0.015f;

    [Header("Rendimiento")]
    [Tooltip("Mas lejos que esto de la camara (y sin estar peleando) se duerme.")]
    [SerializeField] protected float distanciaDormir = 26f;

    protected Rigidbody2D rb;
    protected EnemyHealth salud;
    protected Transform player;
    protected int mirada = 1;
    protected bool alerta;
    protected bool muerto;
    // Ha visto al player y esta peleando (lo usa la musica de combate del nivel).
    public bool EnCombate => alerta && !muerto && player != null;
    // Mientras sea true, los golpes quitan vida pero no interrumpen (el picado del
    // volador, por ejemplo).
    protected bool armadura;

    // Estado actual, para la depuracion.
    public string EstadoIA { get; protected set; } = "Patrulla";
    public bool Alerta => alerta;
    public bool Dormido => dormido;
    public float RadioDeteccion => radioDeteccion;
    public float RadioOlvido => radioOlvido;
    public Vector2 OrigenZona => origenZona;
    public float RadioZona => radioZona;

    private Coroutine cerebro;
    private Coroutine avisoActivo;
    private SpriteRenderer sr;
    private MaterialPropertyBlock bloque;
    private static readonly int IdFlashColor = Shader.PropertyToID("_FlashColor");
    private static readonly int IdFlashAmount = Shader.PropertyToID("_FlashAmount");

    // Los jefes (JefeBase) heredan de aqui pero tienen su propia IA: para ellos
    // todo lo de los enemigos normales esta apagado (dormir lejos, alerta "!",
    // olvidar, giro con retraso, aceleracion, separacion del grupo, respiracion y que el
    // parry les quite la armadura y los reinicie). Se comportan como antes.
    protected virtual bool EsJefe => false;

    // Todos los enemigos vivos (separacion, alerta en grupo, depuracion).
    public static readonly List<EnemigoBase> Activos = new List<EnemigoBase>();

    // Cada individuo algo distinto: velocidad y respiracion.
    private float variacion = 1f, faseRespiro;

    // Los enemigos no chocan entre si: al chocar se empujaban, se subian unos
    // encima de otros y la fisica los despedia hacia arriba (saltos de mas).
    // Se separan solos al moverse (EmpujeSeparacion).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CapaEnemigos()
    {
        int capa = LayerMask.NameToLayer("Enemies");
        if (capa >= 0) Physics2D.IgnoreLayerCollision(capa, capa, true);
    }

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        salud = GetComponent<EnemyHealth>();
        if (anim == null) anim = GetComponentInChildren<AnimadorHoja>();
        if (visual == null && anim != null) visual = anim.transform;
        sr = visual != null ? visual.GetComponent<SpriteRenderer>() : null;
        bloque = new MaterialPropertyBlock();
        if (capaSuelo.value == 0) capaSuelo = LayerMask.GetMask("Ground");
        if (visual != null) escalaVisual = new Vector3(Mathf.Abs(visual.localScale.x), Mathf.Abs(visual.localScale.y), visual.localScale.z);
        if (rb != null && rb.interpolation == RigidbodyInterpolation2D.None) rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        variacion = Random.Range(0.92f, 1.08f);
        faseRespiro = Random.value * 10f;
        siguienteSueno = Time.time + Random.value * 0.4f;

        salud.AlRecibirGolpe += AlRecibirGolpe;
        salud.AlAturdirse += AlAturdirse;
        salud.AlMorir += AlMorir;
        origenZona = transform.position;
        PonerVariante();
    }

    // Variante de color de la ficha: gira el tono en el shader del sprite (no
    // lo oscurece como un tinte).
    private static readonly int IdTono = Shader.PropertyToID("_Tono");
    private static readonly int IdSaturacion = Shader.PropertyToID("_Saturacion");
    private static readonly int IdBrillo = Shader.PropertyToID("_Brillo");
    private void PonerVariante()
    {
        DefinicionEnemigo d = salud.Definicion;
        if (sr == null || d == null || !d.EsVariante) return;
        sr.GetPropertyBlock(bloque);
        bloque.SetFloat(IdTono, d.tono);
        bloque.SetFloat(IdSaturacion, d.saturacion);
        bloque.SetFloat(IdBrillo, d.brillo);
        sr.SetPropertyBlock(bloque);
    }

    protected virtual void OnEnable() { if (!EsJefe && !Activos.Contains(this)) Activos.Add(this); }
    protected virtual void OnDisable() { Activos.Remove(this); }

    protected virtual void OnDestroy()
    {
        Activos.Remove(this);
        if (salud == null) return;
        salud.AlRecibirGolpe -= AlRecibirGolpe;
        salud.AlAturdirse -= AlAturdirse;
        salud.AlMorir -= AlMorir;
    }

    protected virtual void Start()
    {
        Pensar();
    }

    protected abstract IEnumerator Cerebro();

    protected void Pensar()
    {
        if (muerto || dormido) return;
        if (cerebro != null) StopCoroutine(cerebro);
        cerebro = StartCoroutine(Cerebro());
    }

    // Peso del rol: los pesados arrancan y frenan despacio; los debiles, rapido.
    private float PesoRol
    {
        get
        {
            if (salud == null || salud.Definicion == null) return 1f;
            switch (salud.Definicion.rol)
            {
                case RolEnemigo.Debil: return 1.25f;
                case RolEnemigo.Pesado: return 0.55f;
                case RolEnemigo.Elite: return 0.5f;
                default: return 1f;
            }
        }
    }

    // ------------------------------------------------------------------ Golpes

    // Un golpe con dano. Segun su rol (ficha) hace falta mas de uno seguido
    // para interrumpirlo, y tras interrumpirlo hay un rato en que los golpes ya
    // no lo paran: asi no se le puede aturdir sin fin.
    private int golpesSeguidos;
    private float ultimoGolpe = -99f, finInmuneInterrupcion, frameAturdido = -1;

    private void AlRecibirGolpe()
    {
        if (muerto) return;
        if (dormido) Despertar();
        if (!alerta) { alerta = true; finAlerta = 0f; }
        // El aturdimiento (parry, sagrado, congelado) ya lo ha interrumpido.
        if (Time.frameCount == frameAturdido) return;
        if (armadura) return;

        AjustesEnemigos.Rol rol = salud.Definicion != null ? salud.Definicion.Rol : null;
        int necesarios = rol != null ? Mathf.Max(1, rol.golpesParaAturdir) : 1;
        if (Time.time - ultimoGolpe > 1.5f) golpesSeguidos = 0;
        ultimoGolpe = Time.time;
        golpesSeguidos++;
        if (Time.time < finInmuneInterrupcion || golpesSeguidos < necesarios) return;

        golpesSeguidos = 0;
        finInmuneInterrupcion = Time.time + (rol != null ? rol.esperaAturdir : 0f);
        Interrumpir();
    }

    // Parry, sagrado o congelado: siempre lo para, aunque tenga armadura.
    private void AlAturdirse(float segundos)
    {
        if (muerto) return;
        // Los jefes: como antes (su armadura manda; no se reinician).
        if (EsJefe) return;
        if (dormido) Despertar();
        alerta = true;
        frameAturdido = Time.frameCount;
        golpesSeguidos = 0;
        armadura = false;
        Interrumpir();
    }

    // Veces que lo han interrumpido (lo leen las pruebas y la depuracion).
    public int Interrupciones { get; private set; }

    private void Interrumpir()
    {
        Interrupciones++;
        if (cerebro != null) StopCoroutine(cerebro);
        AlInterrumpir();
        cerebro = StartCoroutine(Reaccion());
    }

    // Para que cada enemigo limpie lo suyo al ser interrumpido (un proyectil a
    // medio cargar, la gravedad de un picado...).
    protected virtual void AlInterrumpir()
    {
        QuitarAviso();
    }

    private IEnumerator Reaccion()
    {
        EstadoIA = "Golpeado";
        bool clip = anim.Tiene("golpe");
        if (clip) anim.Reproducir("golpe", true);
        float minimo = Mathf.Max(0.25f, anim.Duracion("golpe"));
        bool vuela = rb.gravityScale == 0f && !salud.IsAirborne;
        Vector3 base0 = visual != null ? visual.localPosition : Vector3.zero;
        if (!EsJefe) Deformar(0.9f, 1.08f);
        float t = 0f;
        while (t < minimo || salud.Aturdido)
        {
            t += Time.deltaTime;
            // Sin clip de golpe: una sacudida corta para que se note.
            if (!clip && visual != null) visual.localPosition = base0 + (t < 0.18f ? (Vector3)(Random.insideUnitCircle * 0.05f * Escala) : Vector3.zero);
            // Los voladores no siguen de largo con la velocidad que llevaban.
            if (vuela) rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, Vector2.zero, Time.deltaTime * 5f);
            yield return null;
        }
        if (!clip && visual != null) visual.localPosition = base0;
        cerebro = StartCoroutine(Cerebro());
    }

    protected virtual void AlMorir()
    {
        muerto = true;
        EstadoIA = "Muerto";
        StopAllCoroutines();
        QuitarAviso();
        if (rb != null && rb.simulated) rb.linearVelocity = Vector2.zero;
        if (anim.Tiene("muerte")) anim.Reproducir("muerte", true);
        if (anim != null) anim.ritmoMovimiento = 1f;
    }

    // ------------------------------------------------------------------ Percepcion

    protected bool BuscarPlayer()
    {
        if (player != null) return true;
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;
        return player != null;
    }

    // Centro del cuerpo del player (su transform esta en el centro de la capsula).
    protected Vector2 PosPlayer => player != null ? (Vector2)player.position : (Vector2)transform.position;

    protected float DistanciaPlayer => player != null ? Vector2.Distance(transform.position, player.position) : Mathf.Infinity;

    protected float DxPlayer => player != null ? player.position.x - transform.position.x : 0f;

    // Reaccion al descubrir al player: un instante quieto, mirandole.
    private float finAlerta;
    protected bool Reaccionando => Time.time < finAlerta;

    // Lo ve si esta cerca y no hay roca en medio. Una vez en alerta aguanta hasta
    // radioOlvido aunque se esconda un momento. Si el player se va muy lejos de
    // su zona, lo olvida y vuelve. Mientras reacciona al descubrirlo, devuelve
    // false (se queda quieto mirandole).
    protected bool VerPlayer(Vector2 ojos)
    {
        if (!BuscarPlayer()) return false;
        if (EsJefe) return VerPlayerJefe(ojos);
        float d = Vector2.Distance(ojos, player.position);
        float radio = alerta ? radioOlvido : radioDeteccion;
        bool fuera = Mathf.Abs(player.position.x - origenZona.x) > radioZona + radioOlvido * 0.5f;
        if (d > radio || fuera)
        {
            if (alerta) Olvidar();
            if (EstadoIA != "Regreso") EstadoIA = "Patrulla";
            return false;
        }

        bool despejado = !Physics2D.Linecast(ojos, player.position, capaSuelo);
        if (despejado && !alerta) Descubrir(0f);
        if (!despejado && !alerta) { if (EstadoIA != "Regreso") EstadoIA = "Patrulla"; return false; }

        if (Reaccionando)
        {
            EstadoIA = "Alerta";
            MirarYa();
            if (rb.gravityScale > 0f) Frenar(frenada);
            return false;
        }
        EstadoIA = "Persecucion";
        return true;
    }

    // La vision de siempre, para los jefes.
    private bool VerPlayerJefe(Vector2 ojos)
    {
        float d = Vector2.Distance(ojos, player.position);
        float radio = alerta ? radioOlvido : radioDeteccion;
        if (d > radio) { alerta = false; return false; }
        bool despejado = !Physics2D.Linecast(ojos, player.position, capaSuelo);
        if (despejado) alerta = true;
        return despejado || alerta;
    }

    // Acaba de verlo: "!" encima, un saltito y un momento quieto (cada uno tarda
    // un poco distinto).
    private void Descubrir(float extra)
    {
        alerta = true;
        finAlerta = Time.time + Random.Range(reaccionAlerta.x, reaccionAlerta.y) / variacion + extra;
        EstadoIA = "Alerta";
        MirarYa();
        Deformar(0.88f, 1.16f);
        Vector2 sobre = salud != null ? salud.OffsetBarra : Vector2.up;
        TextoFlotante.Mostrar("!", (Vector2)transform.position + sobre + Vector2.up * 0.15f, new Color(1f, 0.9f, 0.45f), 0.8f);
        // Un elite se presenta la primera vez que se le ve (una vez por partida).
        DefinicionEnemigo d = salud != null ? salud.Definicion : null;
        if (d != null && d.EsElite && ElitesPresentados.Add(d.NombreElite)) MensajePantalla.TituloElite(d.NombreElite);
        AlDescubrir();
    }

    private static readonly HashSet<string> ElitesPresentados = new HashSet<string>();

    // Para comportamientos propios (la rata avisa a las de su grupo).
    protected virtual void AlDescubrir() { }

    // Otro enemigo le avisa del player.
    public void Alertar(float retraso)
    {
        if (muerto || alerta) return;
        if (dormido) Despertar();
        if (!BuscarPlayer()) return;
        Descubrir(retraso);
    }

    private void Olvidar()
    {
        alerta = false;
        EstadoIA = "Regreso";
        Vector2 sobre = salud != null ? salud.OffsetBarra : Vector2.up;
        TextoFlotante.Mostrar("?", (Vector2)transform.position + sobre + Vector2.up * 0.15f, new Color(0.8f, 0.85f, 0.95f), 0.7f);
    }

    protected void Mirar(int dir)
    {
        if (dir == 0) return;
        mirada = dir > 0 ? 1 : -1;
        if (visual == null) return;
        Vector3 e = visual.localScale;
        e.x = Mathf.Abs(e.x) * mirada * (spriteMiraDerecha ? 1 : -1);
        visual.localScale = e;
    }

    // Se gira hacia el player con un pequeno retraso (no en el mismo frame en
    // que le pasa por detras): se nota mas natural y premia rodar a su espalda.
    private int ladoGiro;
    private float giroDesde, retrasoEsteGiro;
    protected void MirarAlPlayer()
    {
        if (player == null || Mathf.Abs(DxPlayer) <= 0.1f) return;
        if (EsJefe) { MirarYa(); return; }
        int lado = DxPlayer > 0 ? 1 : -1;
        if (lado == mirada) { ladoGiro = 0; return; }
        if (ladoGiro != lado)
        {
            ladoGiro = lado;
            giroDesde = Time.time;
            retrasoEsteGiro = Random.Range(retrasoGiro.x, retrasoGiro.y);
        }
        if (Time.time - giroDesde < retrasoEsteGiro) return;
        ladoGiro = 0;
        Mirar(lado);
        Deformar(0.86f, 1.05f);
    }

    // Mirar al player ya (al apuntar un disparo, al caer de un salto...).
    protected void MirarYa()
    {
        if (player != null && Mathf.Abs(DxPlayer) > 0.1f) Mirar(DxPlayer > 0 ? 1 : -1);
        ladoGiro = 0;
    }

    // Suelo delante de los pies (para no caerse por un borde).
    protected bool HaySueloDelante(float adelante, float bajada = 0.8f) => HaySueloHacia(mirada, adelante, bajada);
    protected bool HayParedDelante(float distancia, float altura = 0.4f) => HayParedHacia(mirada, distancia, altura);

    // Lo mismo hacia un lado concreto (para moverse sin girarse).
    protected bool HaySueloHacia(int dir, float adelante, float bajada = 0.8f)
    {
        float s = Escala;
        Vector2 o = (Vector2)transform.position + new Vector2(adelante * dir * s, 0.2f);
        return Physics2D.Raycast(o, Vector2.down, bajada * Mathf.Max(1f, s), capaSuelo);
    }

    protected bool HayParedHacia(int dir, float distancia, float altura = 0.4f)
    {
        float s = Escala;
        Vector2 o = (Vector2)transform.position + new Vector2(0f, altura * s);
        return Physics2D.Raycast(o, Vector2.right * dir, distancia * s, capaSuelo);
    }

    // ------------------------------------------------------------------ Ataque

    // Caja de golpe relativa a los pies, volteada con la mirada. Devuelve el
    // resultado sobre el player, o null si no lo ha tocado. Sin tipo, el golpe
    // de un enemigo con cuerpo es fisico (salvo que se marcara SiguienteGolpeMagico).
    protected PlayerControler.ResultadoDano? Golpear(Vector2 offset, Vector2 tamano, int dano)
        => Golpear(offset, tamano, dano, PlayerControler.SiguienteGolpeMagico ? PlayerControler.TipoDano.Magico : PlayerControler.TipoDano.Fisico);

    // Con tipo (fisico o magico: cada resistencia reduce el suyo) y, si es un
    // ataque especial, el estado que acumula (sangrado, congelacion...).
    protected PlayerControler.ResultadoDano? Golpear(Vector2 offset, Vector2 tamano, int dano, PlayerControler.TipoDano tipo,
                                                     EstadoPlayer estado = EstadoPlayer.Ninguno, float acumulacion = 0f)
    {
        EstadoIA = "Ataque";
        float s = Escala;
        Vector2 centro = (Vector2)transform.position + new Vector2(offset.x * mirada, offset.y) * s;
        foreach (Collider2D c in Physics2D.OverlapBoxAll(centro, tamano * s, 0f))
        {
            if (!c.CompareTag("Player")) continue;
            PlayerControler p = c.GetComponent<PlayerControler>();
            if (p != null) return p.TakeDamage(dano, this, tipo, estado, acumulacion);
        }
        return null;
    }

    // Espera a que el clip que suena llegue al fotograma "f" (o termine).
    protected IEnumerator HastaFotograma(int f)
    {
        while (anim.Fotograma < f && !anim.Terminado) yield return null;
    }

    // Golpe activo mientras el sprite muestra el ataque (fotogramas desde..hasta del
    // clip actual): la caja de dano va sincronizada con la animacion. Deja el
    // resultado en "resultadoVentana" (null si no ha tocado).
    protected PlayerControler.ResultadoDano? resultadoVentana;
    protected IEnumerator VentanaGolpe(int desde, int hasta, Vector2 offset, Vector2 tamano, int dano, bool magico = false)
    {
        resultadoVentana = null;
        yield return HastaFotograma(desde);
        do
        {
            resultadoVentana = Golpear(offset, tamano, dano, magico ? PlayerControler.TipoDano.Magico : PlayerControler.TipoDano.Fisico);
            if (resultadoVentana.HasValue || anim.Terminado) yield break;
            yield return null;
        } while (anim.Fotograma <= hasta);
    }

    // Destello de aviso antes de un ataque: late del color de aviso durante
    // "duracion" y el cuerpo se encoge un poco (anticipacion).
    protected IEnumerator Aviso(float duracion, float intensidad = 0.55f)
    {
        // El color se fija al empezar: quien lo lance puede cambiar colorAviso despues.
        Color color = colorAviso;
        EstadoIA = "Anticipacion";
        float t = 0f;
        while (t < duracion)
        {
            t += Time.deltaTime;
            float pulso = 0.5f + 0.5f * Mathf.Sin(t * 30f);
            PonerDestello(color, intensidad * (0.4f + 0.6f * pulso));
            float u = Mathf.Clamp01(t / Mathf.Max(0.01f, duracion));
            if (!EsJefe) Deformar(1f + 0.05f * u, 1f - 0.07f * u, false);
            yield return null;
        }
        avisoActivo = null;
        PonerDestello(colorAviso, 0f);
    }

    // Lanza el aviso como corrutina aparte (el ataque sigue mientras tanto). Se
    // corta solo si el enemigo es interrumpido.
    protected void LanzarAviso(float duracion, float intensidad = 0.55f)
    {
        if (avisoActivo != null) StopCoroutine(avisoActivo);
        avisoActivo = StartCoroutine(Aviso(duracion, intensidad));
    }

    protected void QuitarAviso()
    {
        if (avisoActivo != null) { StopCoroutine(avisoActivo); avisoActivo = null; }
        PonerDestello(colorAviso, 0f);
    }

    private void PonerDestello(Color c, float cantidad)
    {
        if (sr == null) return;
        sr.GetPropertyBlock(bloque);
        bloque.SetColor(IdFlashColor, c);
        bloque.SetFloat(IdFlashAmount, cantidad);
        sr.SetPropertyBlock(bloque);
    }

    // Espera a que el clip actual termine (o el tiempo tope).
    protected IEnumerator EsperarClip(float tope = 5f)
    {
        float t = 0f;
        while (!anim.Terminado && t < tope)
        {
            t += Time.deltaTime;
            yield return null;
        }
    }

    // 1 normal, menos si esta ralentizado por la escarcha (0 congelado).
    protected float Ritmo => EstadosEnemigo.RitmoDe(this);

    // Espera que se alarga si el enemigo esta ralentizado.
    protected IEnumerator EsperarRitmo(float segundos)
    {
        float t = 0f;
        while (t < segundos)
        {
            t += Time.deltaTime * Mathf.Max(0.3f, Ritmo);
            yield return null;
        }
    }

    // ------------------------------------------------------------------ Movimiento

    private float velocidadPedida;

    // Frena poco a poco (y se aparta de otro enemigo si esta encima).
    protected void Frenar(float desaceleracion)
    {
        if (EsJefe)
        {
            rb.linearVelocity = new Vector2(Mathf.MoveTowards(rb.linearVelocity.x, 0f, desaceleracion * Time.deltaTime), rb.linearVelocity.y);
            return;
        }
        float objetivo = rb.gravityScale > 0f ? EmpujeSeparacion() : 0f;
        float vx = Mathf.MoveTowards(rb.linearVelocity.x, objetivo, desaceleracion * PesoRol * Time.deltaTime);
        rb.linearVelocity = new Vector2(vx, rb.linearVelocity.y);
        velocidadPedida = 0f;
    }

    // Al empezar un ataque: frena muy rapido pero no en seco (unos fotogramas
    // de derrape), mientras se prepara.
    private float frenoAtaqueHasta;
    protected void FrenarAtaque()
    {
        EstadoIA = "Anticipacion";
        frenoAtaqueHasta = Time.time + 0.2f;
        velocidadPedida = 0f;
    }

    protected virtual void FixedUpdate()
    {
        if (EsJefe || rb == null || !rb.simulated || rb.gravityScale == 0f || salud.IsAirborne || muerto) return;
        float dt = Time.fixedDeltaTime;
        Vector2 v = rb.linearVelocity;
        if (Time.time < frenoAtaqueHasta) v.x = Mathf.MoveTowards(v.x, 0f, frenada * 3f * dt);
        // Siempre un empujon suave si esta encima de otro enemigo (tambien
        // mientras ataca, cuando no anda): asi nunca se quedan apilados.
        float separar = EmpujeSeparacion();
        if (separar != 0f) v.x += separar * 4f * dt;
        rb.linearVelocity = v;
    }

    // Anda hacia donde mira, acelerando hasta "velocidad". Mientras reacciona
    // al descubrir al player, se queda quieto.
    protected void Andar(float velocidad) => AndarDir(mirada, velocidad);

    // Anda hacia "dir" sin girarse (el mago retrocede mirando al player).
    protected void AndarDir(int dir, float velocidad)
    {
        if (EsJefe) { rb.linearVelocity = new Vector2(velocidad * dir, rb.linearVelocity.y); return; }
        if (Reaccionando) { Frenar(frenada); return; }
        if (EstadoIA != "Persecucion" && EstadoIA != "Regreso" && EstadoIA != "Reposicion") EstadoIA = alerta ? "Persecucion" : "Patrulla";
        velocidadPedida = Mathf.Abs(velocidad * variacion);
        float objetivo = velocidad * variacion * dir + EmpujeSeparacion();
        float vx = Mathf.MoveTowards(rb.linearVelocity.x, objetivo, aceleracion * PesoRol * Time.deltaTime);
        rb.linearVelocity = new Vector2(vx, rb.linearVelocity.y);
    }

    // Empuje lateral para no quedar encima de otro enemigo de suelo.
    private float EmpujeSeparacion()
    {
        float empuje = 0f;
        float r = distanciaGrupo * Escala;
        for (int i = 0; i < Activos.Count; i++)
        {
            EnemigoBase o = Activos[i];
            if (o == this || o == null || o.muerto || o.rb == null || o.rb.gravityScale == 0f) continue;
            Vector2 d = (Vector2)(transform.position - o.transform.position);
            float minimo = (r + o.distanciaGrupo * o.Escala) * 0.5f;
            if (Mathf.Abs(d.y) > 1f * Mathf.Max(Escala, o.Escala) || Mathf.Abs(d.x) >= minimo) continue;
            float lado = Mathf.Abs(d.x) > 0.01f ? Mathf.Sign(d.x) : (GetInstanceID() > o.GetInstanceID() ? 1f : -1f);
            empuje += lado * (minimo - Mathf.Abs(d.x)) / minimo * 3f;
        }
        return empuje;
    }

    // Lo mismo para voladores: vector que los aparta de los que tienen cerca.
    protected Vector2 Separacion2D()
    {
        Vector2 empuje = Vector2.zero;
        float r = distanciaGrupo * Escala * 1.4f;
        for (int i = 0; i < Activos.Count; i++)
        {
            EnemigoBase o = Activos[i];
            if (o == this || o == null || o.muerto) continue;
            Vector2 d = (Vector2)(transform.position - o.transform.position);
            float m = d.magnitude;
            if (m >= r) continue;
            Vector2 dir = m > 0.01f ? d / m : new Vector2(GetInstanceID() > o.GetInstanceID() ? 1f : -1f, 0.3f);
            empuje += dir * (r - m) / r * 3f;
        }
        return empuje;
    }

    // Un punto del vuelo, sin salir de su zona.
    protected Vector2 LimitarZona(Vector2 p)
    {
        float r = radioZona;
        return new Vector2(Mathf.Clamp(p.x, origenZona.x - r, origenZona.x + r),
                           Mathf.Clamp(p.y, origenZona.y - r * 0.5f, origenZona.y + r * 0.5f));
    }

    // Sin player a la vista: si esta lejos de donde lo colocaron, vuelve andando.
    // Devuelve true mientras vuelve (quien llama pone la animacion de andar).
    protected bool VolverAZona(float velocidad)
    {
        float dx = origenZona.x - transform.position.x;
        if (Mathf.Abs(dx) < 1.2f * Escala) { if (EstadoIA == "Regreso") EstadoIA = "Patrulla"; return false; }
        Mirar(dx > 0f ? 1 : -1);
        if (!HaySueloDelante(0.6f) || HayParedDelante(0.6f)) { Frenar(frenada); return false; }
        EstadoIA = "Regreso";
        Andar(velocidad);
        return true;
    }

    // Lejos de su sitio (para que la patrulla de la vuelta hacia el).
    protected bool LejosDeZona => Mathf.Abs(transform.position.x - origenZona.x) > radioZona * 0.6f;
    protected int HaciaZona => origenZona.x >= transform.position.x ? 1 : -1;

    // Retrocede un poco mirando al player (cambia de distancia tras atacar).
    protected IEnumerator Reposicionar(float distancia, float velocidad)
    {
        EstadoIA = "Reposicion";
        int lejos = DxPlayer > 0f ? -1 : 1;
        float recorrido = 0f, t = 0f;
        float s = Escala;
        while (recorrido < distancia * s && t < 2f)
        {
            t += Time.deltaTime;
            Vector2 pos = transform.position;
            bool suelo = Physics2D.Raycast(pos + new Vector2(lejos * 0.6f * s, 0.2f), Vector2.down, 0.8f * Mathf.Max(1f, s), capaSuelo);
            bool pared = Physics2D.Raycast(pos + Vector2.up * 0.4f * s, Vector2.right * lejos, 0.6f * s, capaSuelo);
            if (!suelo || pared) break;
            float vx = Mathf.MoveTowards(rb.linearVelocity.x, lejos * velocidad * variacion, aceleracion * PesoRol * Time.deltaTime);
            rb.linearVelocity = new Vector2(vx, rb.linearVelocity.y);
            velocidadPedida = velocidad;
            recorrido += Mathf.Abs(vx) * Time.deltaTime;
            MirarYa();
            yield return null;
        }
        float f = 0f;
        while (f < 0.15f) { f += Time.deltaTime; Frenar(frenada); yield return null; }
    }

    // ------------------------------------------------------------------ Salto comun

    // Todos los saltos pasan por aqui: solo desde el suelo, un unico impulso
    // calculado para una altura (con tope), se agacha antes (aviso) y se
    // recupera al caer. El destino se acorta si no hay suelo o hay pared, y
    // nunca sale de su zona (donde lo colocaron +- radioZona).
    [Header("Saltos (tope para todos)")]
    [Tooltip("Ningun salto sube mas que esto (unidades, a escala 1). El player mide 1.")]
    [SerializeField] protected float alturaSaltoMaxima = 2.5f;
    [Tooltip("Ningun salto llega mas lejos que esto (unidades, a escala 1).")]
    [SerializeField] protected float distanciaSaltoMaxima = 9f;
    [Tooltip("Segundos agachado antes de saltar.")]
    [SerializeField] protected float preparacionSalto = 0.22f;
    [Tooltip("Segundos de recuperacion al aterrizar.")]
    [SerializeField] protected float recuperacionSalto = 0.2f;
    [Tooltip("Tiempo minimo entre dos saltos.")]
    [SerializeField] protected float enfriamientoSalto = 1f;
    [Tooltip("No se aleja (ni salta) mas alla de esto desde donde lo colocaron.")]
    [SerializeField] protected float radioZona = 14f;

    protected Vector2 origenZona;
    private float listoSalto;
    // Registro de la ultima subida (la prueba de saltos lo lee).
    public float UltimaAlturaSalto { get; private set; }

    // Pisa suelo (y no esta subiendo).
    protected bool EnSuelo
    {
        get
        {
            if (rb == null || rb.linearVelocity.y > 0.05f) return false;
            Vector2 o = (Vector2)transform.position + Vector2.up * 0.1f;
            return Physics2D.BoxCast(o, new Vector2(0.3f * Escala, 0.05f), 0f, Vector2.down, 0.16f, capaSuelo);
        }
    }

    protected bool PuedeSaltar => !muerto && Time.time >= listoSalto && EnSuelo;

    // Velocidad de un salto de "altura" que cae a "dx" (hacia la X real, con
    // signo). False si no esta en el suelo.
    protected bool CalcularSalto(float dx, float altura, out Vector2 velocidad)
    {
        velocidad = Vector2.zero;
        if (!EnSuelo) return false;
        float s = Escala;
        float h = Mathf.Clamp(altura * s, 0.05f, alturaSaltoMaxima * s);
        float x0 = rb.position.x, y0 = rb.position.y;
        float destino = x0 + Mathf.Clamp(dx, -distanciaSaltoMaxima * s, distanciaSaltoMaxima * s);
        destino = Mathf.Clamp(destino, origenZona.x - radioZona, origenZona.x + radioZona);

        // Pared en medio: se queda antes.
        float dir = Mathf.Sign(destino - x0);
        if (Mathf.Abs(destino - x0) > 0.05f)
        {
            RaycastHit2D pared = Physics2D.Raycast(new Vector2(x0, y0 + h * 0.5f + 0.2f), Vector2.right * dir, Mathf.Abs(destino - x0) + 0.3f * s, capaSuelo);
            if (pared) destino = pared.point.x - dir * 0.45f * s;
        }

        // Suelo donde caer (no mas alto que el salto): si no hay, se acorta.
        float yDestino = y0;
        bool haySuelo = false;
        for (int i = 0; i < 8 && !haySuelo; i++)
        {
            RaycastHit2D suelo = Physics2D.Raycast(new Vector2(destino, y0 + h), Vector2.down, h + 3f, capaSuelo);
            if (suelo && suelo.point.y <= y0 + h - 0.25f) { yDestino = suelo.point.y; haySuelo = true; }
            else destino = Mathf.Lerp(destino, x0, 0.4f);
        }
        if (!haySuelo) { destino = x0; yDestino = y0; }

        float g = Mathf.Abs(Physics2D.gravity.y) * Mathf.Max(0.01f, rb.gravityScale);
        float vy = Mathf.Sqrt(2f * g * h);
        float caida = Mathf.Max(0.05f, h - (yDestino - y0));
        float t = vy / g + Mathf.Sqrt(2f * caida / g);
        velocidad = new Vector2((destino - x0) / t, vy);
        return true;
    }

    // Un unico impulso (el salto de un ataque). False si no puede.
    protected bool Impulsar(float dx, float altura)
    {
        if (!CalcularSalto(dx, altura, out Vector2 v)) return false;
        EstadoIA = "Salto";
        rb.linearVelocity = v;
        listoSalto = Time.time + enfriamientoSalto;
        yInicioSalto = rb.position.y;
        yMaxSalto = yInicioSalto;
        midiendoSalto = true;
        Deformar(0.85f, 1.18f);
        return true;
    }

    // Se agacha (aviso visible) el tiempo indicado.
    protected IEnumerator Agacharse(float segundos)
    {
        EstadoIA = "Anticipacion";
        float t = 0f;
        while (t < segundos)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / Mathf.Max(0.01f, segundos));
            Deformar(1f + 0.12f * u, 1f - 0.18f * u, false);
            Frenar(30f);
            yield return null;
        }
    }

    // Salto entero: se agacha, salta, llama a "enVuelo" cada frame en el aire
    // (para golpear) y se recupera al caer.
    protected IEnumerator Saltar(float dx, float altura, System.Action enVuelo = null, bool agacharse = true)
    {
        if (!EnSuelo) yield break;
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        if (agacharse) yield return Agacharse(preparacionSalto);
        if (!Impulsar(dx, altura)) yield break;
        yield return EnElAire(enVuelo);
    }

    // Hasta que vuelve a pisar suelo; luego la recuperacion.
    protected IEnumerator EnElAire(System.Action enVuelo = null)
    {
        float t = 0f;
        while (t < 3f)
        {
            t += Time.deltaTime;
            enVuelo?.Invoke();
            if (t > 0.12f && EnSuelo) break;
            yield return null;
        }
        EstadoIA = "Recuperacion";
        rb.linearVelocity = new Vector2(rb.linearVelocity.x * 0.3f, rb.linearVelocity.y);
        Deformar(1.2f, 0.8f);
        float r = 0f;
        while (r < recuperacionSalto)
        {
            r += Time.deltaTime;
            Frenar(40f);
            yield return null;
        }
    }

    // ------------------------------------------------------------------ Dibujo vivo

    // Estirar / aplastar el dibujo. "resorte" = que vuelva solo; si no, se
    // mantiene mientras se siga pidiendo cada frame (agacharse).
    private Vector2 deformacion = Vector2.one;
    private bool deformacionFija;
    private Vector3 escalaVisual = Vector3.one;
    private float yInicioSalto, yMaxSalto;
    private bool midiendoSalto;

    protected void Deformar(float ancho, float alto, bool resorte = true)
    {
        deformacion = new Vector2(ancho, alto);
        deformacionFija = !resorte;
    }

    protected virtual void LateUpdate()
    {
        if (midiendoSalto && rb != null)
        {
            yMaxSalto = Mathf.Max(yMaxSalto, rb.position.y);
            if (rb.linearVelocity.y <= 0f && Time.time > listoSalto - enfriamientoSalto + 0.1f)
            {
                midiendoSalto = false;
                UltimaAlturaSalto = yMaxSalto - yInicioSalto;
            }
        }
        if (visual == null || muerto || dormido || EsJefe) return;

        if (!deformacionFija) deformacion = Vector2.Lerp(deformacion, Vector2.one, Time.deltaTime * 10f);
        deformacionFija = false;

        // Respira en reposo (los de suelo, parados).
        float respiro = 1f;
        if (respiracion > 0f && rb != null && Mathf.Abs(rb.linearVelocity.x) < 0.2f)
            respiro = 1f + respiracion * Mathf.Sin((Time.time + faseRespiro) * 2.4f * variacion);

        float signo = visual.localScale.x < 0f ? -1f : 1f;
        visual.localScale = new Vector3(signo * escalaVisual.x * deformacion.x, escalaVisual.y * deformacion.y * respiro, escalaVisual.z);

        // El paso de la animacion de andar va con la velocidad real: al arrancar
        // y al frenar las piernas van despacio (sin deslizar).
        if (anim != null)
        {
            string c = anim.Actual;
            bool andando = c == "andar" || c == "correr";
            anim.ritmoMovimiento = andando && rb != null && rb.gravityScale > 0f && velocidadPedida > 0.1f
                ? Mathf.Clamp(Mathf.Abs(rb.linearVelocity.x) / velocidadPedida, 0.35f, 1.15f) : 1f;
        }
    }

    // ------------------------------------------------------------------ Dormir lejos

    private bool dormido;
    private float siguienteSueno;
    private static Camera camara;

    protected virtual void Update()
    {
        if (EsJefe || muerto || Time.time < siguienteSueno) return;
        siguienteSueno = Time.time + 0.4f;
        if (camara == null) camara = Camera.main;
        if (camara == null) return;
        Vector2 d = (Vector2)(transform.position - camara.transform.position);
        bool lejos = !alerta && (Mathf.Abs(d.x) > distanciaDormir || Mathf.Abs(d.y) > distanciaDormir * 0.7f);
        bool cerca = Mathf.Abs(d.x) < distanciaDormir * 0.85f && Mathf.Abs(d.y) < distanciaDormir * 0.6f;
        if (lejos && !dormido) Dormir();
        else if (dormido && (cerca || alerta)) Despertar();
    }

    private void Dormir()
    {
        dormido = true;
        EstadoIA = "Dormido";
        if (cerebro != null) { StopCoroutine(cerebro); cerebro = null; }
        QuitarAviso();
        if (rb != null) { rb.linearVelocity = Vector2.zero; rb.simulated = false; }
        if (anim != null) anim.enabled = false;
    }

    private void Despertar()
    {
        if (!dormido) return;
        dormido = false;
        EstadoIA = "Patrulla";
        if (rb != null && !muerto) rb.simulated = true;
        if (anim != null) anim.enabled = true;
        Pensar();
    }

    // ------------------------------------------------------------------ Ficha

    // Tamano de la ficha: las cajas de golpe, rayos y alturas del codigo estan
    // pensados a escala 1 y se multiplican por esto.
    protected float Escala => salud != null ? salud.Escala : 1f;

    // Punto a una altura del cuerpo (a escala).
    protected Vector2 Alto(float y) => (Vector2)transform.position + Vector2.up * (y * Escala);

    // Dano de un ataque: el golpe principal de la ficha por el peso del ataque
    // (1 = el principal). Sin ficha, 10 por peso.
    protected int Dano(float peso = 1f)
    {
        DefinicionEnemigo d = salud != null ? salud.Definicion : null;
        return Mathf.Max(1, Mathf.RoundToInt((d != null ? d.DanoBase : 10f) * Mathf.Max(0f, peso)));
    }

    // Rebusca estalactitas cerca y las suelta (rugidos, golpes contra el techo).
    protected static void SoltarEstalactitas(Vector2 centro, float radio)
    {
        foreach (Estalactita e in Object.FindObjectsByType<Estalactita>(FindObjectsSortMode.None))
            if (Vector2.Distance(e.transform.position, centro) <= radio) e.Soltar();
    }

    // ------------------------------------------------------------------ Depuracion

    // Fuerza un estado desde la depuracion (solo Editor): Aturdir, Alerta,
    // Olvidar, Dormir, Despertar o Reposicion.
    public void ForzarEstado(string estado)
    {
        if (muerto) return;
        switch (estado)
        {
            case "Aturdir": salud.Stagger(1.2f); break;
            case "Alerta": alerta = false; Alertar(0f); break;
            case "Olvidar": if (alerta) Olvidar(); break;
            case "Dormir": if (!dormido) Dormir(); break;
            case "Despertar": Despertar(); break;
            case "Reposicion":
                if (cerebro != null) StopCoroutine(cerebro);
                cerebro = StartCoroutine(ForzarReposicion());
                break;
        }
    }

    private IEnumerator ForzarReposicion()
    {
        BuscarPlayer();
        yield return Reposicionar(2f, 2f);
        cerebro = StartCoroutine(Cerebro());
    }

    protected virtual void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 1f, 0f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, radioDeteccion);
        Gizmos.color = new Color(1f, 0.6f, 0f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, radioOlvido);
        Vector3 o = Application.isPlaying ? (Vector3)origenZona : transform.position;
        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.35f);
        Gizmos.DrawWireCube(o, new Vector3(radioZona * 2f, 1f, 0f));
    }
}
