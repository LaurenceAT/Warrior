using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The Blind Huntress, la Cazadora Ciega: el jefe secreto de los Desafios.
//
// Separada en tres partes, cada una con su trabajo:
//   - Cerebro (este archivo y los de Ataques/Especiales): decide que hacer.
//   - Cuerpo (CuerpoCazadora): se mueve con aceleracion y curvas, sin tirones.
//   - Animacion (AnimCazadora): muestra el cuadro y tine los efectos.
//
// Cada ataque tiene tres partes con tiempos editables (AjustesCazadora): aviso
// (pose quieta y brillo), golpe (la caja de dano va con los cuadros del tajo) y
// recuperacion (tu ventana de castigo). Nunca se gira en pleno golpe.
//
// Es ciega: de lejos ataca a donde oyo tu ultimo ruido (OidoCazadora). Si falla
// porque ya no estabas alli, se queda confundida un momento.
//
// Tres barras de vida (una por fase). Entre barra y barra, una muerte falsa: se
// deshace en polvo y se reconstruye. Tiene super armadura: tus golpes normales
// no la interrumpen; solo el parry, el sagrado o romperle el escudo.
public partial class JefeCazadora : JefeBase, IModificadorDano, IAfinidadElemental, IAturdible
{
    public static JefeCazadora Actual { get; private set; }

    [Header("Cazadora")]
    [SerializeField] private AjustesCazadora ajustes;
    [SerializeField] private AnimCazadora cuerpoAnim;
    [SerializeField] private IlusionCazadora plantillaIlusion;
    [Tooltip("Sombra bajo sus pies (las ilusiones no tienen).")]
    [SerializeField] private SpriteRenderer sombra;
    [Tooltip("Contorno leve para verla en la oscuridad de la fase 3.")]
    [SerializeField] private SpriteRenderer contorno;
    [Tooltip("Burbuja de luz del escudo.")]
    [SerializeField] private SpriteRenderer burbuja;

    // Avisos para la arena (musica, ambiente, barra, camara).
    public event System.Action<int> AlMuerteFalsa;      // barra que acaba de vaciar (0 o 1)
    public event System.Action<int> AlNuevaFase;        // fase nueva (1 o 2)
    public event System.Action AlMuerteReal;
    public event System.Action<bool> AlAvisoInstakill;  // empieza / termina el aviso
    public event System.Action<bool> AlSilencioCaza;    // El Silencio empieza / termina
    public event System.Action<string> AlFrase;         // subtitulo corto

    public AjustesCazadora Ajustes => ajustes;
    public int Fase => fase;
    public bool Activa => activa;
    public bool Transicionando => transicion;
    public float EscudoFraccion => escudoActivo ? Mathf.Clamp01(1f - golpesEscudo / Mathf.Max(1, golpesEscudoMax)) : -1f;
    public Elemento Resistencia => Time.time < finResistencia ? resistido : Elemento.Ninguno;
    public bool Oscuridad => fase >= 2 && !muertaDelTodo;
    public CuerpoCazadora Cuerpo => cuerpo;

    private CuerpoCazadora cuerpo;
    private OidoCazadora oido;
    private PlayerControler pc;
    private Rect arena;
    private float suelo;

    private int fase;
    private bool activa, transicion, muertaDelTodo;
    private bool invulnerable;
    // En el aviso o en el golpe de un ataque (sus golpes no cuentan para la guardia).
    private bool enAviso, enGolpe;
    private bool enGuardia, parryHecho;
    private bool escudoActivo;
    private bool escudoRoto;
    private float golpesEscudo, pesoGolpeOscuro = 2f;
    private int golpesEscudoMax = 10, frameGolpeEscudo = -1;
    private readonly bool[] escudoUsado = new bool[3];
    private readonly bool[] instakillUsado = new bool[3];
    private float bonusDano = 1f;
    private bool bonusActivo;
    private Elemento elementoAtaque = Elemento.Ninguno;
    private Elemento resistido = Elemento.Ninguno;
    private float finResistencia, siguienteResistencia;
    private float curadoEnBarra, robadoTotal;
    private string ultimoAtaque = "";
    private readonly Queue<float> golpesRecibidos = new Queue<float>();
    private float siguienteGuardia;
    private bool noLetalAccion = true;
    private float inicioCombate;
    private float aturdimientoPendiente;
    private bool parado;
    private PlayerControler.ResultadoDano? ultimoResultado;
    private readonly List<IlusionCazadora> ilusiones = new List<IlusionCazadora>();
    private readonly List<(float t, Vector2 p)> rastro = new List<(float, Vector2)>();
    private float tIlusionesCaen = -99f, tTrampas = -99f, tEspejismo = -99f, tDanza = -99f, tEspejos = -99f, tFantasma = -99f;
    private string ataqueForzado;
    private Transform raizIlusiones;
    private MaterialPropertyBlock bloqueContorno;

    private AjustesCazadora.Fase F => ajustes.FaseN(fase);
    private Vector2 Pos => cuerpo != null ? cuerpo.Posicion : (Vector2)transform.position;

    // ------------------------------------------------------------------ Arranque

    public override void Configurar(Rect zona, float alturaSuelo)
    {
        arena = zona;
        suelo = alturaSuelo;
        if (cuerpo == null) cuerpo = GetComponent<CuerpoCazadora>();
        cuerpo.Configurar(zona.xMin, zona.xMax, alturaSuelo);
    }

    protected override void Awake()
    {
        base.Awake();
        Actual = this;
        cuerpo = GetComponent<CuerpoCazadora>();
        oido = GetComponent<OidoCazadora>();
        oido.Configurar(ajustes);
        radioDeteccion = radioOlvido = 80f;
        armadura = true;
        salud.Inamovible = true;
        salud.PuedeMorir = () => fase >= 2;
        salud.AlAgotarse += () => { if (!transicion) { DetenerTodo(); StartCoroutine(MuerteFalsa()); } };
        salud.AlRecibirGolpe += Golpeada;
        if (burbuja != null) burbuja.enabled = false;
        if (contorno != null) contorno.enabled = false;
        // Las ilusiones van sueltas por la escena (no se mueven con ella).
        if (plantillaIlusion != null)
        {
            raizIlusiones = new GameObject("IlusionesCazadora").transform;
            plantillaIlusion.transform.SetParent(raizIlusiones, true);
        }
    }

    protected override void Start()
    {
        salud.Revivir(VidaDesafio(ajustes.FaseN(0).vida));
        base.Start();
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (Actual == this) Actual = null;
        PlayerControler.TopeVentanaParry = -1f;
        if (pc != null) pc.InvulnerableExterno = false;
        if (raizIlusiones != null) Destroy(raizIlusiones.gameObject);
    }

    // La arena la despierta al acabar el dialogo.
    public void Activar()
    {
        activa = true;
        inicioCombate = Time.time;
    }

    // Hacia donde mira (en la entrada habla hacia el vacio, de espaldas a ti).
    public void Orientar(int dir) => Mirar(dir);

    // Tras "Voy a derrotarte": un tajo horizontal rapido con un aviso minimo pero
    // legible, y la ventana de parry algo mas corta. Si lo paras, queda aturdida
    // (golpe libre); si te da, quita mucho pero nunca mata.
    public IEnumerator TajoDialogo()
    {
        PlayerControler p = PC;
        if (p == null) yield break;
        PlayerControler.TopeVentanaParry = ajustes.ventanaParryDialogo;
        AnunciarAtaque("tajo_dialogo");
        MirarA(p.transform.position.x);
        yield return AvisoPose("estocada", 0, ajustes.avisoTajoDialogo, false);
        cuerpo.Trayecto(new Vector2(p.transform.position.x - mirada * 0.7f, suelo), 0.12f, 0f, CuerpoCazadora.Curva.Dash);
        Estela();
        noLetalAccion = true;
        yield return Golpe("estocada", 0.14f, Dano(ajustes.danoTajoDialogo), PlayerControler.TipoDano.Fisico, 1.2f, false, true);
        PlayerControler.TopeVentanaParry = -1f;
        if (parado)
        {
            parado = false;
            AlFrase?.Invoke(ajustes.trasParryDialogo);
            yield return Aturdida(1.4f, "Aturdida", false);
        }
        else yield return Recuperar(ajustes.estocadaCombo, false);
    }

    private PlayerControler PC
    {
        get
        {
            if (pc == null && BuscarPlayer()) pc = player.GetComponent<PlayerControler>();
            return pc;
        }
    }

    // Para que las ilusiones y la arena suenen con los sonidos de la Cazadora.
    public void SonarPublico(string clave, float volumen = 1f, float tono = 1f) => Sonar(clave, volumen, tono);

    // ------------------------------------------------------------------ Dano recibido

    public int Modificar(int dano, TipoArma arma)
    {
        if (invulnerable || transicion || muertaDelTodo) return 0;
        // En guardia: tu golpe rebota (parry de la Cazadora).
        if (enGuardia) { ParryDeLaCazadora(); return 0; }
        // El escudo se lleva el golpe (cuenta golpes, no dano).
        if (escudoActivo) { GolpeAlEscudo(EnemyHealth.ElementoDelGolpe); return 0; }
        if (bonusActivo) dano = Mathf.RoundToInt(dano * bonusDano);
        if (!enAviso && !enGolpe && dano > 0) ContarGolpe();
        return dano;
    }

    public float Multiplicador(Elemento e)
    {
        if (e != Elemento.Ninguno && e == Resistencia) return ajustes.resistenciaCopiada;
        return 1f;
    }

    public bool PedirAturdimiento(float segundos)
    {
        if (transicion || escudoActivo || muertaDelTodo || aturdimientoPendiente > 0f) return false;
        aturdimientoPendiente = segundos;
        return true;
    }

    // Destello blanco (la silueta, como el sprite Hit) sin cortar su animacion:
    // tiene super armadura.
    private void Golpeada()
    {
        if (muertaDelTodo) return;
        if (salud.UltimoDano > 0) cuerpoAnim.Destello(Color.white, 0.9f, 0.07f);
    }

    // Golpes seguidos tuyos mientras ella esta libre: si son muchos, se pone en
    // guardia (Parry de la Cazadora).
    private void ContarGolpe()
    {
        float ahora = Time.time;
        golpesRecibidos.Enqueue(ahora);
        while (golpesRecibidos.Count > 0 && ahora - golpesRecibidos.Peek() > ajustes.ventanaGolpes) golpesRecibidos.Dequeue();
    }

    // Nunca justo despues de que reaparezcas.
    private bool QuiereGuardia => golpesRecibidos.Count >= ajustes.golpesParaParry && Time.time >= siguienteGuardia
                                  && Time.time - inicioCombate >= ajustes.esperaTrasReaparecer;

    // ------------------------------------------------------------------ Cerebro

    protected override IEnumerator Cerebro()
    {
        if (!activa)
        {
            cuerpoAnim.Reproducir("quieto");
            while (!activa) yield return null;
        }
        while (!muertaDelTodo)
        {
            yield return Decidir();
            yield return null;
        }
    }

    private IEnumerator Decidir()
    {
        PlayerControler p = PC;
        if (p == null || p.VidaActual <= 0) { yield return Pausa(0.3f); yield break; }
        noLetalAccion = p.VidaActual >= p.VidaMaxima;
        parado = false;

        // Aturdimiento del sagrado pendiente.
        if (aturdimientoPendiente > 0f)
        {
            float s = aturdimientoPendiente;
            aturdimientoPendiente = 0f;
            yield return Aturdida(s, "Aturdida", false);
            yield break;
        }

        float vida = salud.MaxHealth > 0 ? (float)salud.CurrentHealth / salud.MaxHealth : 1f;
        if (!escudoUsado[fase] && vida <= ajustes.umbralEscudo) { AnunciarAtaque("escudo"); yield return RugidoYEscudo(); yield break; }
        if (PuedeInstakill(vida)) { yield return Instakill(); yield break; }
        if (QuiereGuardia) { AnunciarAtaque("guardia"); yield return Guardia(); yield break; }

        // Forzado desde las herramientas de depuracion.
        if (!string.IsNullOrEmpty(ataqueForzado))
        {
            string f = ataqueForzado;
            ataqueForzado = null;
            yield return Ejecutar(f);
            yield return TrasAtaque(F.pausa.x);
            yield break;
        }

        // Anti-salto: saltas encima de ella.
        Vector2 real = p.transform.position;
        if (!p.EnSuelo && real.y > Pos.y + 1f && Mathf.Abs(real.x - Pos.x) < ajustes.distanciaAntiSalto)
        {
            yield return Ejecutar("ascendente");
            yield return TrasAtaque(F.pausa.x);
            yield break;
        }

        // Sin rastro: va a donde te oyo por ultima vez y adivina.
        if (!oido.Localizado(Pos))
        {
            yield return Buscar();
            yield break;
        }

        Vector2 obj = oido.Objetivo(Pos);
        float d = Mathf.Abs(obj.x - Pos.x);

        // Te acercas corriendo: le caen ilusiones del cielo delante.
        if (fase >= 1 && Time.time - tIlusionesCaen > 9f && d < 7f && Acercandose(p))
        {
            AnunciarAtaque("caen");
            yield return IlusionesCaen(2, false);
            yield return TrasAtaque(F.pausa.x);
            yield break;
        }

        string ataque = Elegir(d);
        ultimoAtaque = ataque;
        yield return Ejecutar(ataque);
        yield return TrasAtaque(Random.Range(F.pausa.x, F.pausa.y));
    }

    private bool Acercandose(PlayerControler p)
    {
        float dx = Pos.x - p.transform.position.x;
        return Mathf.Abs(p.Velocidad.x) > 3f && Mathf.Sign(p.Velocidad.x) == Mathf.Sign(dx);
    }

    // Eleccion al azar con pesos segun la distancia. Nunca repite el ultimo.
    private string Elegir(float d)
    {
        bool lejos = d >= ajustes.distanciaLejos;
        bool media = !lejos && d > ajustes.distanciaCerca + 1.5f;
        var op = new List<(string, float)>();
        op.Add(("tresLunas", lejos ? 0.6f : media ? 2f : 3.2f));
        op.Add(("cruce", lejos ? 2.5f : media ? 2f : 1f));
        op.Add(("ola", lejos ? 2.8f : media ? 1.4f : 0.6f));
        op.Add(("salto", lejos ? 1.2f : media ? 1.6f : 0.8f));

        if (fase >= 1)
        {
            if (!lejos) op.Add(("flanqueo", 1.8f));
            op.Add(("fantasma", Time.time - tFantasma > 6f ? 1.5f : 0f));
            op.Add(("trampas", Time.time - tTrampas > 13f ? 1.3f : 0f));
            op.Add(("lluvia", lejos ? 1.8f : 1f));
            op.Add(("cruceDoble", 1.4f));
            op.Add(("espejismo", Time.time - tEspejismo > 14f ? 1.1f : 0f));
            ArmaImbuida arma = PC != null ? PC.GetComponent<ArmaImbuida>() : null;
            if (arma != null && arma.Activo != Elemento.Ninguno && Time.time >= siguienteResistencia && Resistencia != arma.Activo)
                op.Add(("contra", 3f));
        }
        if (fase >= 2)
        {
            op.Add(("danza", Time.time - tDanza > 15f ? 2.6f : 0f));
            op.Add(("espejos", Time.time - tEspejos > 17f ? 2f : 0f));
        }

        op.RemoveAll(o => o.Item1 == ultimoAtaque || o.Item2 <= 0f);
        float total = 0f;
        foreach (var o in op) total += o.Item2;
        float r = Random.value * total;
        foreach (var o in op) { r -= o.Item2; if (r <= 0f) return o.Item1; }
        return op.Count > 0 ? op[0].Item1 : "ola";
    }

    public IEnumerator Ejecutar(string ataque)
    {
        // Los instakills se apuntan al mostrar su aviso (en su propio codigo).
        if (ataque != "ejecucion" && ataque != "sentencia" && ataque != "silencio") AnunciarAtaque(ataque);
        switch (ataque)
        {
            case "tresLunas": return TresLunas();
            case "cruce": return Cruce();
            case "ola": return OlaDeLuz();
            case "ascendente": return TajoAscendente();
            case "salto": return SaltoDescendente();
            case "guardia": return Guardia();
            case "escudo": return RugidoYEscudo();
            case "ejecucion": return Ejecucion();
            case "contra": return ContraElemental();
            case "flanqueo": return Flanqueo();
            case "caen": return IlusionesCaen(3, false);
            case "espejismo": return Espejismo();
            case "fantasma": return CuchilladaFantasma();
            case "trampas": return TrampasDeSonido();
            case "lluvia": return LluviaDeTajos();
            case "cruceDoble": return CruceDoble();
            case "sentencia": return Sentencia();
            case "danza": return DanzaDeLaCaceria();
            case "espejos": return CaceriaDeEspejos();
            case "silencio": return ElSilencio();
            default: return Pausa(0.3f);
        }
    }

    // Tras cada ataque: primero el parry o el sagrado; si no, la pausa.
    private IEnumerator TrasAtaque(float pausa)
    {
        if (parado) { parado = false; yield return Aturdida(0.75f, "Aturdida", false); yield break; }
        if (aturdimientoPendiente > 0f)
        {
            float s = aturdimientoPendiente;
            aturdimientoPendiente = 0f;
            yield return Aturdida(s, "Aturdida", false);
            yield break;
        }
        yield return Pausa(pausa);
    }

    // ------------------------------------------------------------------ Movimiento

    // Espera entre ataques: se gira hacia ti y a veces da unos pasos (viva, no
    // estatua). Se acorta en Dificil.
    private IEnumerator Pausa(float segundos)
    {
        segundos = PausaDesafio(segundos);
        bool pasea = Random.value < 0.35f && segundos > 0.3f;
        int dir = Random.value < 0.5f ? -1 : 1;
        float t = 0f;
        while (t < segundos)
        {
            Vector2 obj = oido.Objetivo(Pos);
            MirarA(obj.x);
            if (pasea && t < segundos * 0.7f)
            {
                cuerpo.Andar(dir * ajustes.velocidadCorrer * 0.35f * F.velocidadMovimiento, ajustes.aceleracion, ajustes.frenada);
                if (cuerpoAnim.Actual != "correr") cuerpoAnim.Reproducir("correr", 0.6f);
            }
            else
            {
                cuerpo.Parar(ajustes.frenada);
                if (cuerpoAnim.Actual != "quieto" || cuerpoAnim.Terminado) cuerpoAnim.Reproducir("quieto");
            }
            t += Time.deltaTime;
            yield return null;
        }
        cuerpo.Parar(ajustes.frenada);
    }

    // Corre hasta quedar a "distancia" del objetivo (o hasta el tope de tiempo).
    // Acelera y frena con curva: nada de parar en seco.
    private IEnumerator Acercarse(float distancia, float tope)
    {
        float t = 0f;
        while (t < tope)
        {
            Vector2 obj = oido.Objetivo(Pos);
            float dx = obj.x - Pos.x;
            if (Mathf.Abs(dx) <= distancia) break;
            int dir = dx > 0f ? 1 : -1;
            MirarA(obj.x);
            cuerpo.Andar(dir * ajustes.velocidadCorrer * F.velocidadMovimiento, ajustes.aceleracion, ajustes.frenada);
            if (cuerpoAnim.Actual != "correr") cuerpoAnim.Reproducir("correr", F.velocidadMovimiento);
            t += Time.deltaTime;
            yield return null;
        }
        cuerpo.Parar(ajustes.frenada);
    }

    // Sin rastro de ti: camina a donde te oyo por ultima vez, "escucha" y adivina.
    private IEnumerator Buscar()
    {
        Vector2 ultimo = oido.UltimaPosicion;
        float t = 0f;
        while (t < 1.6f && Mathf.Abs(ultimo.x - Pos.x) > 1.2f && !oido.Localizado(Pos))
        {
            int dir = ultimo.x > Pos.x ? 1 : -1;
            Mirar(dir);
            cuerpo.Andar(dir * ajustes.velocidadCorrer * 0.45f, ajustes.aceleracion, ajustes.frenada);
            if (cuerpoAnim.Actual != "correr") cuerpoAnim.Reproducir("correr", 0.55f);
            t += Time.deltaTime;
            yield return null;
        }
        cuerpo.Parar(ajustes.frenada);
        if (oido.Localizado(Pos)) yield break;
        // Escucha un momento, girando la cabeza.
        cuerpoAnim.Reproducir("quieto");
        yield return Esperar(0.35f);
        Mirar(-mirada);
        yield return Esperar(0.35f);
        if (oido.Localizado(Pos)) yield break;
        // Adivina: una ola hacia un lado al azar.
        Mirar(Random.value < 0.5f ? 1 : -1);
        yield return OlaDeLuz(true);
        yield return Pausa(0.4f);
    }

    private void MirarA(float x)
    {
        if (enGolpe) return;
        if (Mathf.Abs(x - Pos.x) > 0.15f) Mirar(x > Pos.x ? 1 : -1);
    }

    private IEnumerator Esperar(float segundos)
    {
        for (float t = 0f; t < segundos; t += Time.deltaTime) yield return null;
    }

    // ------------------------------------------------------------------ Piezas de un ataque

    private float Aviso(AjustesCazadora.Ataque a, bool pesado)
    {
        float minimo = Mathf.Min(a.aviso, pesado ? ajustes.avisoMinimoPesado : ajustes.avisoMinimoLigero);
        return Mathf.Max(a.aviso / Mathf.Max(0.1f, F.velocidadAtaque), minimo);
    }

    private float GolpeT(AjustesCazadora.Ataque a) => a.golpe / Mathf.Max(0.1f, F.velocidadAtaque);

    private float Recuperacion(AjustesCazadora.Ataque a, bool grande)
    {
        float r = PausaDesafio(a.recuperacion / Mathf.Max(0.1f, F.velocidadAtaque));
        return grande ? Mathf.Max(r, ajustes.castigoMinimo) : r;
    }

    private int Dano(float fraccion)
    {
        PlayerControler p = PC;
        int max = p != null ? p.VidaMaxima : 100;
        return Mathf.Max(1, Mathf.RoundToInt(max * fraccion * F.dano));
    }

    // El aviso: una pose quieta con un destello que late, un brillo en la espada
    // y su sonido. Durante la primera mitad todavia puede girarse hacia ti.
    private IEnumerator AvisoPose(string clip, int cuadro, float segundos, bool pesado, Color? color = null, bool girar = true)
    {
        enAviso = true;
        cuerpo.Parar(ajustes.frenada * 1.5f);
        cuerpoAnim.Pose(clip, cuadro);
        Color c = color ?? (pesado ? new Color(1f, 0.55f, 0.25f) : new Color(1f, 0.92f, 0.7f));
        Sonar(pesado ? "aviso_pesado" : "aviso_ligero", pesado ? 0.8f : 0.6f);
        BrilloEspada(c);
        float t = 0f;
        while (t < segundos)
        {
            if (girar && t < segundos * 0.5f) MirarA(oido.Objetivo(Pos).x);
            float pulso = 0.5f + 0.5f * Mathf.Sin(t * 28f);
            cuerpoAnim.Destello(c, (pesado ? 0.55f : 0.4f) * (0.4f + 0.6f * pulso), 0.05f);
            t += Time.deltaTime;
            yield return null;
        }
        enAviso = false;
    }

    // El golpe: el clip desde su cuadro de golpe, a la rapidez justa para que el
    // tajo dure "segundos". La caja de dano es la del efecto de cada cuadro
    // activo, asi que coincide con lo dibujado; se apaga al acabar esos cuadros
    // (o si el ataque se corta: vive dentro de esta corrutina).
    private IEnumerator Golpe(string clip, float segundos, int dano, PlayerControler.TipoDano tipo = PlayerControler.TipoDano.Fisico,
                              float escalaCaja = 1f, bool imparable = false, bool noLetal = true, System.Action<PlayerControler.ResultadoDano> alPegar = null)
    {
        SpritesCazadora.Clip c = cuerpoAnim.hojas.Buscar(clip);
        if (c == null) yield break;
        int a0 = Mathf.Max(0, c.PrimerActivo), a1 = Mathf.Max(a0, c.UltimoActivo);
        float vel = ((a1 - a0 + 1) / c.fps) / Mathf.Max(0.02f, segundos);
        cuerpoAnim.Reproducir(clip, vel, a0);
        Color col = ColorElemento(elementoAtaque);
        cuerpoAnim.ColorEfecto(col);
        enGolpe = true;
        ultimoResultado = null;
        bool pego = false;
        float t = 0f;
        Sonar(elementoAtaque == Elemento.Ninguno ? "tajo" : "tajo_elemento", 0.85f, Random.Range(0.95f, 1.08f));
        while (t < segundos + 0.05f && cuerpoAnim.Fotograma <= a1)
        {
            if (!pego && cuerpoAnim.CuadroActivo)
            {
                var r = GolpearCuadro(c, cuerpoAnim.Fotograma, dano, tipo, escalaCaja, imparable, noLetal && noLetalAccion);
                if (r.HasValue)
                {
                    pego = true;
                    ultimoResultado = r;
                    TrasPegar(r.Value, dano);
                    alPegar?.Invoke(r.Value);
                }
            }
            t += Time.deltaTime;
            yield return null;
        }
        enGolpe = false;
        // Llamas del fuego donde cae el tajo.
        if (elementoAtaque == Elemento.Fuego)
        {
            Rect caja = c.Caja(a0);
            LlamaSuelo.Poner(new Vector2(Pos.x + mirada * caja.center.x, suelo), ajustes.duracionLlamas, Mathf.Max(1, dano / 8), ajustes.acumulacionEstado * 0.4f);
        }
    }

    private PlayerControler.ResultadoDano? GolpearCuadro(SpritesCazadora.Clip c, int cuadro, int dano, PlayerControler.TipoDano tipo,
                                                         float escala, bool imparable, bool noLetal)
    {
        Rect caja = c.Caja(cuadro);
        if (caja.width <= 0f) return null;
        if (elementoAtaque == Elemento.Sagrado) escala *= ajustes.sagradoAncho;
        Vector2 centro = Pos + new Vector2(mirada * caja.center.x * escala, caja.center.y);
        Vector2 tam = new Vector2(caja.width * escala, caja.height);
        if (DepuracionCazadora.VerCajas) CajaDebug.Mostrar(centro, tam, Color.red, 0.03f);
        EstadoPlayer estado = OlaLuz.EstadoDe(elementoAtaque);
        if (estado == EstadoPlayer.Sangrado) estado = EstadoPlayer.Ninguno;
        return GolpeCazadora.Caja(centro, tam, dano, this, tipo, estado, ajustes.acumulacionEstado, noLetal, imparable);
    }

    // Lo que pasa al conectar: parry (se aturde), o golpe (camara, robo de vida).
    private void TrasPegar(PlayerControler.ResultadoDano r, int dano)
    {
        if (r == PlayerControler.ResultadoDano.Parry)
        {
            parado = true;
            Sonar("parry", 0.9f);
            CamaraCazadora.Lenta(ajustes.lentaParry, ajustes.lentaParryTiempo);
            return;
        }
        if (r != PlayerControler.ResultadoDano.Recibido) return;
        PlayerControler p = PC;
        bool pesado = p != null && dano >= p.VidaMaxima * 0.3f;
        CamaraCazadora.Sacudir(pesado ? ajustes.temblorPesado : ajustes.temblorLigero);
        if (pesado) CamaraCazadora.Congelar(ajustes.congeladoPesado);
        RobarVida();
    }

    // Robo de vida: la sangre en la fase 2 (tope por barra) y cualquier golpe en
    // la fase 3 (tope en toda la pelea).
    private void RobarVida()
    {
        int barra = salud.MaxHealth;
        if (fase == 1 && elementoAtaque == Elemento.Sangrado)
        {
            int cura = Mathf.RoundToInt(barra * ajustes.roboSangre);
            int queda = Mathf.RoundToInt(barra * ajustes.topeRoboSangre) - Mathf.RoundToInt(curadoEnBarra);
            cura = Mathf.Min(cura, queda);
            if (cura > 0) { curadoEnBarra += cura; salud.Curar(cura); TextoCura(cura); }
        }
        else if (fase >= 2)
        {
            int cura = Mathf.RoundToInt(barra * ajustes.roboVidaFase3);
            int queda = Mathf.RoundToInt(barra * ajustes.topeRoboFase3) - Mathf.RoundToInt(robadoTotal);
            cura = Mathf.Min(cura, queda);
            if (cura > 0) { robadoTotal += cura; salud.Curar(cura); TextoCura(cura); }
        }
    }

    private void TextoCura(int cura)
    {
        TextoFlotante.Mostrar("+" + cura, Pos + Vector2.up * 1.9f, new Color(0.95f, 0.35f, 0.4f), 0.8f);
        PolvoCazadora.Soltar(Pos + Vector2.up * 0.8f, new Color(0.95f, 0.2f, 0.25f, 0.8f), 8, 0.6f);
    }

    // Tras un ataque dirigido a un ruido: si no te dio porque ya no estabas alli
    // (estas lejos del sitio), se queda confundida (vulnerable).
    private IEnumerator ComprobarFallo(Vector2 apuntado, bool aRuido)
    {
        PlayerControler p = PC;
        if (!aRuido || p == null || ultimoResultado.HasValue) yield break;
        if (Vector2.Distance(p.transform.position, apuntado) < 1.8f) yield break;
        cuerpoAnim.Reproducir("quieto");
        TextoFlotante.Mostrar("?", Pos + Vector2.up * 2f, new Color(0.8f, 0.9f, 1f), 1f);
        AnilloRuido.Mostrar(Pos + Vector2.up * 1.6f, 0.4f);
        yield return Esperar(ajustes.confusion);
    }

    // Movimiento de un dash con estelas. Devuelve cuando llega.
    private IEnumerator Dash(float xDestino, float segundos, float altura = 0f, CuerpoCazadora.Curva curva = CuerpoCazadora.Curva.Dash,
                             System.Func<bool> cadaCuadro = null)
    {
        cuerpo.Trayecto(new Vector2(xDestino, suelo), segundos / Mathf.Max(0.1f, F.velocidadMovimiento), altura, curva);
        float siguiente = 0f;
        while (cuerpo.EnTrayecto)
        {
            if (Time.time >= siguiente)
            {
                siguiente = Time.time + ajustes.cadaEstela;
                Estela();
            }
            if (cadaCuadro != null && cadaCuadro()) { }
            yield return null;
        }
    }

    private void Estela()
    {
        Color c = elementoAtaque == Elemento.Ninguno ? new Color(0.55f, 0.75f, 0.6f, 0.5f) : ColorElemento(elementoAtaque) * new Color(1f, 1f, 1f, 0.5f);
        EstelaFantasma.Lanzar(cuerpoAnim.SpriteCuerpo, cuerpoAnim.cuerpo.transform.position, cuerpoAnim.Volteado, c, ajustes.vidaEstela);
    }

    private void BrilloEspada(Color c)
    {
        Vector2 p = Pos + new Vector2(mirada * 0.3f, 0.95f);
        PolvoCazadora.Soltar(p, new Color(c.r, c.g, c.b, 0.9f), 5, 0.4f);
    }

    // ------------------------------------------------------------------ Elementos

    public Color ColorElemento(Elemento e)
    {
        Color c;
        switch (e)
        {
            case Elemento.Fuego: c = ajustes.colorFuego; break;
            case Elemento.Hielo: c = ajustes.colorHielo; break;
            case Elemento.Sagrado: c = ajustes.colorSagrado; break;
            case Elemento.Sangrado: c = ajustes.colorSangre; break;
            case Elemento.Oscuro: c = Elementos.Color(Elemento.Oscuro); break;
            default: c = ajustes.colorNormal; break;
        }
        return c;
    }

    // Elemento del proximo ataque: ninguno en la fase 1; en la 2 uno; en la 3 dos
    // alternados dentro del mismo combo. Con la resistencia copiada, el tuyo.
    private Elemento NuevoElemento()
    {
        if (fase == 0) return Elemento.Ninguno;
        if (Resistencia != Elemento.Ninguno) return Resistencia;
        Elemento[] e = { Elemento.Fuego, Elemento.Hielo, Elemento.Sagrado, Elemento.Sangrado };
        return e[Random.Range(0, e.Length)];
    }

    private Elemento elementoB = Elemento.Ninguno;

    private void PrepararElementos()
    {
        elementoAtaque = NuevoElemento();
        elementoB = fase >= 2 ? NuevoElemento() : elementoAtaque;
        if (fase >= 2 && elementoB == elementoAtaque) elementoB = NuevoElemento();
        cuerpoAnim.ColorEfecto(ColorElemento(elementoAtaque));
    }

    // En la fase 3, alterna los dos elementos del combo.
    private void AlternarElemento(int golpe)
    {
        if (fase < 2) return;
        Elemento a = elementoAtaque;
        elementoAtaque = golpe % 2 == 0 ? a : elementoB;
        if (golpe % 2 == 1) elementoB = a;
        cuerpoAnim.ColorEfecto(ColorElemento(elementoAtaque));
    }

    private float RapidezElemento => elementoAtaque == Elemento.Sagrado ? ajustes.sagradoRapidez : 1f;

    // ------------------------------------------------------------------ Ayudas

    private void DetenerTodo()
    {
        StopAllCoroutines();
        enAviso = enGolpe = enGuardia = false;
        cuerpoAnim.Temblar(0f);
        cuerpo.CortarTrayecto();
        cuerpo.Detener();
        if (PC != null) PlayerControler.TopeVentanaParry = -1f;
    }

    private void Update()
    {
        // Rastro de tus posiciones (la cuchillada fantasma sale donde estabas).
        PlayerControler p = PC;
        if (p != null)
        {
            rastro.Add((Time.time, p.transform.position));
            while (rastro.Count > 0 && Time.time - rastro[0].t > 3f) rastro.RemoveAt(0);
        }
        if (sombra != null)
        {
            float alto = Mathf.Max(0f, Pos.y - suelo);
            sombra.transform.position = new Vector3(Pos.x, suelo + 0.03f, 0f);
            float e = Mathf.Clamp01(1f - alto * 0.12f);
            sombra.transform.localScale = new Vector3(1.1f * e, 0.22f * e, 1f);
            sombra.enabled = !muertaDelTodo && cuerpoAnim.AlfaActual > 0.2f;
        }
        if (contorno != null)
        {
            // Contorno leve siempre (su capa oscura se pierde en el bosque) y mas
            // fuerte en la oscuridad de la fase 3.
            float fuerza = muertaDelTodo ? 0f : Oscuridad ? ajustes.contornoOscuridad : ajustes.contorno;
            contorno.enabled = fuerza > 0.01f && cuerpoAnim.cuerpo.enabled && cuerpoAnim.AlfaActual > 0.2f;
            if (contorno.enabled)
            {
                contorno.sprite = cuerpoAnim.SpriteCuerpo;
                if (bloqueContorno == null) bloqueContorno = new MaterialPropertyBlock();
                contorno.GetPropertyBlock(bloqueContorno);
                bloqueContorno.SetFloat("_Amount", fuerza * cuerpoAnim.AlfaActual);
                bloqueContorno.SetFloat("_Inner", Oscuridad ? 0.12f : 0f);
                contorno.SetPropertyBlock(bloqueContorno);
            }
        }
        if (burbuja != null && escudoActivo)
        {
            float s = 2.4f + 0.12f * Mathf.Sin(Time.time * 6f);
            burbuja.transform.localScale = new Vector3(s, s, 1f);
        }
        cuerpoAnim.multiplicador = Ritmo;
    }

    private Vector2 PosicionHace(float segundos)
    {
        float t = Time.time - segundos;
        for (int i = rastro.Count - 1; i >= 0; i--) if (rastro[i].t <= t) return rastro[i].p;
        return rastro.Count > 0 ? rastro[0].p : (PC != null ? (Vector2)PC.transform.position : Pos);
    }

    // Una ilusion de la reserva (la plantilla va en el prefab).
    private IlusionCazadora SacarIlusion()
    {
        foreach (IlusionCazadora i in ilusiones) if (i != null && !i.gameObject.activeSelf) return i;
        if (plantillaIlusion == null) return null;
        IlusionCazadora n = Instantiate(plantillaIlusion, raizIlusiones);
        n.name = "Ilusion";
        ilusiones.Add(n);
        return n;
    }

    private void QuitarIlusiones()
    {
        foreach (IlusionCazadora i in ilusiones) if (i != null && i.Viva) i.Deshacer();
    }

    // Aparecer o desaparecer con un fundido y polvo (nunca un salto brusco).
    private IEnumerator Fundido(float hasta, float segundos)
    {
        float desde = cuerpoAnim.AlfaActual;
        if (hasta > desde) PolvoCazadora.Soltar(Pos + Vector2.up * 0.7f, new Color(0.7f, 0.85f, 0.7f, 0.8f), 12, 0.9f);
        for (float t = 0f; t < segundos; t += Time.deltaTime)
        {
            cuerpoAnim.Alfa(Mathf.Lerp(desde, hasta, t / segundos));
            yield return null;
        }
        cuerpoAnim.Alfa(hasta);
        if (hasta < desde) PolvoCazadora.Soltar(Pos + Vector2.up * 0.7f, new Color(0.7f, 0.85f, 0.7f, 0.8f), 12, 0.9f);
    }

    protected override void AlMorir()
    {
        muerto = true;
        muertaDelTodo = true;
        DetenerTodo();
        QuitarIlusiones();
        StartCoroutine(MuerteReal());
    }

    // ------------------------------------------------------------------ Depuracion

    public void ForzarAtaque(string ataque) => ataqueForzado = ataque;

    public void SaltarAFase(int nueva)
    {
        nueva = Mathf.Clamp(nueva, 0, 2);
        if (nueva == fase || transicion || muertaDelTodo) return;
        DetenerTodo();
        QuitarIlusiones();
        fase = nueva;
        escudoUsado[fase] = false;
        instakillUsado[fase] = false;
        curadoEnBarra = 0f;
        salud.Revivir(VidaDesafio(F.vida));
        cuerpoAnim.Alfa(1f);
        AlNuevaFase?.Invoke(fase);
        Pensar();
    }

    public void PonerVida(float fraccion)
    {
        int v = Mathf.Clamp(Mathf.RoundToInt(salud.MaxHealth * fraccion), 1, salud.MaxHealth);
        int dif = v - salud.CurrentHealth;
        if (dif > 0) salud.Curar(dif);
        else if (dif < 0) salud.DanoEstado(-dif);
    }

    protected override void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0.3f);
        Gizmos.DrawWireCube(arena.center, arena.size);
    }
}
