using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// El Black Knight: un duelista que pelea como otra persona. Jefe nuevo, SOLO en
// la sala de pruebas (no sale en niveles, Desafios, Bitacora, Bestiario ni logros).
// Sus numeros: Assets/Data/Jefes/Ajustes Black Knight. Sprites, prefab y entrada
// en la sala: Warrior > Black Knight > Crear o actualizar.
//
// Archivos:
//   JefeBlackKnight            nucleo: vida y 3 fases, cerebro, movimiento y golpes
//   JefeBlackKnightLectura     te lee: reacciona a tus golpes, al frasco, a quedarte
//                              quieto; su parry, rodadas, salto y provocacion
//   JefeBlackKnightEspeciales  ondas de fuego (fase 2+) y sombras espectrales (fase 3)
//   EfectosBlackKnight         la onda (dibujada por codigo) y las sombras
//
// Tres barras: al vaciar una pasa a la siguiente fase (invulnerable mientras).
// Combos con las reglas comunes (RitmoCombo + ReglasCombo, en su ficha): aviso
// naranja rapido el ligero, rojo lento el pesado; siguen aunque los pares; parar
// todos los golpes de un combo de 2 o mas lo aturde. Algunos golpes se retrasan
// (sostiene el aviso) y a veces corta el combo a medias y rueda.
public partial class JefeBlackKnight : JefeBase, IModificadorDano, IAturdible, IAfinidadElemental
{
    // Clips del animador (los crea Warrior > Black Knight).
    public const string ClipQuieto = "quieto", ClipQuietoSin = "quieto_sin", ClipCorrer = "correr", ClipRodar = "rodar",
                        ClipSaltar = "saltar", ClipAire = "aire", ClipCaer = "caer", ClipAterrizar = "caer_golpe",
                        ClipGuardia = "guardia", ClipContra = "contra", ClipFalloGuardia = "fallo_guardia",
                        ClipDesenvainar = "desenvainar", ClipEnvainar = "envainar", ClipBuff = "buff", ClipDolor = "dolor",
                        ClipMuerte = "muerte", ClipClavada = "clavada", ClipArte = "arte";

    // Acciones que no son ataques de la lista (tambien se lanzan desde la sala).
    public const string AccRodadaEspalda = "rodada_espalda", AccSalto = "salto", AccGuardia = "guardia", AccDesenvaine = "desenvaine",
                        AccProvocar = "provocar", AccRetroceder = "retroceder", AccOndas = "ondas_fuego", AccSombras = "sombras",
                        AccSello = "sello_amuleto", AccErupciones = "erupciones";

    [SerializeField] private AjustesBlackKnight ajustes;

    private Rect arena;
    private float suelo;
    private int fase = 1;
    private bool cambiando, invulnerable, entrado;
    private string ataqueForzado, ultimoElegido;
    private RitmoCombo ritmo;
    private SpriteRenderer srCaballero;
    private float velX;
    private float aturdimientoPendiente;
    private string tipoPendiente;
    private bool aturdido;

    public AjustesBlackKnight Ajustes => ajustes;
    public new RitmoCombo Ritmo => ritmo;
    public int Fase => fase;
    public bool Cambiando => cambiando;
    public bool Aturdido => aturdido;
    public int Mirada => mirada;
    public string AccionActual { get; private set; } = "";

    // Lo que leen las pruebas.
    public AjustesBlackKnight.Golpe GolpeEnCurso { get; private set; }
    public int GolpesActivos { get; private set; }
    public event System.Action<AjustesBlackKnight.Golpe, float, float> AlActivarGolpe;
    public event System.Action<AjustesBlackKnight.Golpe, PlayerControler.ResultadoDano?> AlTerminarGolpe;
    public readonly List<PlayerControler.ResultadoDano?> Resultados = new List<PlayerControler.ResultadoDano?>();
    public readonly List<string> AccionesHechas = new List<string>();
    public readonly List<(string tipo, float segundos)> Aturdimientos = new List<(string, float)>();
    public int Retrasos { get; private set; }
    public int CombosCortados { get; private set; }
    public int Amagos { get; private set; }

    private float Vel => ajustes.Velocidad(fase);

    // ------------------------------------------------------------------ Sala de pruebas

    public override string[] AtaquesPrueba
    {
        get
        {
            var l = new List<string>();
            if (ajustes == null) return l.ToArray();
            foreach (AjustesBlackKnight.Ataque a in ajustes.ataques) if (a != null && !string.IsNullOrEmpty(a.id)) l.Add(a.id);
            l.AddRange(new[] { AccRodadaEspalda, AccSalto, AccGuardia, AccDesenvaine, AccProvocar, AccRetroceder, AccOndas, AccSombras, AccSello, AccErupciones });
            return l.ToArray();
        }
    }

    public override void ForzarAtaque(string ataque) => ataqueForzado = ataque;

    public override void Configurar(Rect zona, float alturaSuelo)
    {
        arena = zona;
        suelo = alturaSuelo;
    }

    // Salta a la fase pedida (pruebas y sala): vacia las barras que falten.
    public void SaltarAFase(int objetivo)
    {
        if (salud.Muerto || objetivo <= fase || objetivo > 3) return;
        StopAllCoroutines();
        StartCoroutine(CambioFase(objetivo));
    }

    // ------------------------------------------------------------------ Vida

    protected override void Awake()
    {
        base.Awake();
        radioDeteccion = radioOlvido = 60f;
        armadura = true;
        salud.Inamovible = true;
        BoxCollider2D caja = GetComponent<BoxCollider2D>();
        if (caja != null) { caja.size = ajustes.cuerpo; caja.offset = new Vector2(0f, ajustes.cuerpo.y * 0.5f); }
        srCaballero = visual != null ? visual.GetComponent<SpriteRenderer>() : null;
        // Rueda y embiste rapido: colision continua para no atravesar el suelo.
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        ritmo = gameObject.AddComponent<RitmoCombo>();
        ritmo.Configurar(ajustes.reglas, srCaballero);
        PonerRitmoAnimacion();
        salud.PuedeMorir = () => fase >= 3;
        salud.AlAgotarse += () => { if (!cambiando) { StopAllCoroutines(); StartCoroutine(CambioFase(fase + 1)); } };
        salud.AlRecibirGolpe += AlRecibirGolpeBK;
    }

    protected override void Start()
    {
        salud.Revivir(VidaDesafio(ajustes.vidaFase1));
        base.Start();
    }

    private void PonerRitmoAnimacion()
    {
        if (anim == null) return;
        foreach (AnimadorHoja.Clip c in anim.clips)
        {
            if (c == null) continue;
            if (c.nombre == ClipQuieto || c.nombre == ClipQuietoSin) c.fps = ajustes.fpsEspera;
            else if (c.nombre == ClipCorrer) c.fps = ajustes.fpsCorrer;
            else if (c.nombre == ClipRodar) c.fps = ajustes.fpsRodada;
            else if (c.nombre.StartsWith("ligero") || c.nombre.StartsWith("pesado") || c.nombre == ClipArte || c.nombre == ClipDesenvainar
                     || c.nombre == ClipContra || c.nombre == ClipAterrizar) c.fps = ajustes.fpsAtaque;
            else c.fps = ajustes.fpsOtros;
        }
    }

    // Cambiando de fase o rodando, no le entra nada. En su guardia (de frente)
    // te para el golpe. Aturdido le entra mas; con la espada clavada, lo de la ficha.
    // Resiste el fuego, la oscuridad y el sangrado; debil al hielo y a la luz (ficha).
    public float Multiplicador(Elemento e) => ajustes.Afinidad(e);

    public int Modificar(int dano, TipoArma arma)
    {
        if (cambiando || invulnerable) return 0;
        if (enGuardia && DeFrente) { golpeParado = true; return 0; }
        if (aturdido) dano = Mathf.RoundToInt(dano * ajustes.multDanoAturdido);
        if (clavado) dano = Mathf.RoundToInt(dano * ajustes.danoClavado);
        return dano;
    }

    private void AlRecibirGolpeBK()
    {
        if (salud.UltimoDano <= 0) return;
        if (enOndas && !retirandose) golpesDuranteOndas++;
        GolpesRecibidos++;
    }

    public int GolpesRecibidos { get; private set; }

    // El jugador esta del lado al que mira.
    private bool DeFrente => player == null || Mathf.Abs(DxPlayer) < 0.05f || (DxPlayer > 0f ? 1 : -1) == mirada;

    // ------------------------------------------------------------------ Cerebro

    protected override IEnumerator Cerebro()
    {
        if (!entrado) yield return Entrada();
        while (true)
        {
            Vector2 p = ajustes.Pausa(fase);
            yield return Estudiar(PausaPrueba(PausaDesafio(Random.Range(p.x, p.y))));
            string accion = ElegirAccion();
            if (string.IsNullOrEmpty(accion)) continue;
            AccionActual = accion;
            AnunciarAtaque(accion);
            yield return Hacer(accion);
            AccionesHechas.Add(accion);
            AccionActual = "";
            yield return TrasAccion();
            // A veces, tras un ataque, envaina para provocarte (no en el bucle de la sala).
            if (PausaExtraPrueba <= 0f && ajustes.BuscarAtaque(accion) != null && Random.value < ajustes.probProvocar && !HayLectura)
            {
                AccionActual = AccProvocar;
                AnunciarAtaque(AccProvocar);
                yield return Provocar();
                AccionesHechas.Add(AccProvocar);
                AccionActual = "";
                yield return TrasAccion();
            }
        }
    }

    // Entrada: cae en la arena, aterriza y desenvaina con floritura.
    private IEnumerator Entrada()
    {
        CambiarEstado(EstadoEnemigo.Alerta);
        anim.Reproducir(ClipCaer);
        BuscarPlayer();
        MirarYa();
        while (!EnSuelo) yield return null;
        anim.Reproducir(ClipAterrizar, true);
        Sonar("jefe_aparicion");
        AvisarAterrizaje();
        while (!anim.Terminado) yield return null;
        anim.Reproducir(ClipDesenvainar, true);
        while (!anim.Terminado) yield return null;
        entrado = true;
    }

    // Que hace ahora: lo forzado desde la sala; si no, lo que le pide leerte
    // (frasco, tus golpes, quedarte quieto); si no, un especial (con su
    // enfriamiento) o un ataque al azar por frecuencia, nunca el mismo dos veces.
    private string ElegirAccion()
    {
        if (!string.IsNullOrEmpty(ataqueForzado))
        {
            string f = ataqueForzado;
            ataqueForzado = null;
            if (ExisteAccion(f)) { ultimoElegido = f; return f; }
        }
        string leido = AccionLeida();
        if (leido != null) return leido;

        if (fase >= 2 && Time.time >= finEnfriamientoSello && AmuletoParaSellar() != null
            && Random.value < (fase >= 3 ? ajustes.probSello.y : ajustes.probSello.x))
            return ultimoElegido = AccSello;
        if (fase >= 2 && Time.time >= finEnfriamientoErupciones && Random.value < (fase >= 3 ? ajustes.probErupciones.y : ajustes.probErupciones.x))
            return ultimoElegido = AccErupciones;
        if (fase >= 2 && Time.time >= finEnfriamientoOndas && Random.value < (fase >= 3 ? ajustes.probOndas.y : ajustes.probOndas.x))
            return ultimoElegido = AccOndas;
        if (fase >= 3 && Time.time >= finEnfriamientoSombras && Random.value < ajustes.probSombras)
            return ultimoElegido = AccSombras;

        var opciones = new List<(string id, float peso)>();
        foreach (AjustesBlackKnight.Ataque a in ajustes.ataques)
            if (a != null && a.frecuencia > 0f && a.faseMinima <= fase && a.golpes.Count > 0) opciones.Add((a.id, a.frecuencia));
        opciones.Add((AccRodadaEspalda, ajustes.frecuenciaRodadaEspalda));
        opciones.Add((AccSalto, ajustes.frecuenciaSalto));
        opciones.Add((AccGuardia, ajustes.frecuenciaGuardia));
        if (opciones.Count > 1) opciones.RemoveAll(o => o.id == ultimoElegido);
        float total = 0f;
        foreach (var o in opciones) total += o.peso;
        float r = Random.value * total;
        foreach (var o in opciones)
        {
            r -= o.peso;
            if (r <= 0f) return ultimoElegido = o.id;
        }
        return opciones.Count > 0 ? ultimoElegido = opciones[opciones.Count - 1].id : null;
    }

    private bool ExisteAccion(string id)
    {
        if (ajustes.BuscarAtaque(id) != null) return true;
        foreach (string a in AtaquesPrueba) if (a == id) return true;
        return false;
    }

    private IEnumerator Hacer(string accion)
    {
        AjustesBlackKnight.Ataque a = ajustes.BuscarAtaque(accion);
        if (a != null) { yield return HacerAtaque(a); yield break; }
        switch (accion)
        {
            case AccRodadaEspalda: yield return RodadaEspalda(); break;
            case AccSalto: yield return SaltoCaida(); break;
            case AccGuardia: yield return Guardia(); break;
            case AccDesenvaine: yield return DesenvaineRapido(); break;
            case AccProvocar: yield return Provocar(); break;
            case AccRetroceder: yield return Retroceder(); break;
            case AccOndas: yield return OndasDeFuego(); break;
            case AccSombras: yield return SombrasEspectrales(); break;
            case AccSello: yield return SelloAmuleto(); break;
            case AccErupciones: yield return Erupciones(); break;
        }
    }

    // ------------------------------------------------------------------ Movimiento

    // Velocidad con aceleracion y frenada (arranca y para como una persona).
    private void MoverHacia(float objetivo)
    {
        float k = Mathf.Abs(objetivo) > Mathf.Abs(velX) ? ajustes.aceleracion : ajustes.frenada;
        velX = Mathf.MoveTowards(velX, objetivo, k * Time.deltaTime);
        if (velX != 0f && !DentroDeArena(velX > 0f ? 1 : -1)) velX = 0f;
        rb.linearVelocity = new Vector2(velX, rb.linearVelocity.y);
        if (anim != null) anim.ritmoMovimiento = anim.Actual == ClipCorrer ? Mathf.Clamp(Mathf.Abs(velX) / Mathf.Max(0.1f, ajustes.velocidadCorrer), 0.35f, 1.3f) : 1f;
    }

    private void Parar()
    {
        velX = 0f;
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        if (anim != null) anim.ritmoMovimiento = 1f;
    }

    private bool DentroDeArena(int dir)
    {
        if (arena.width <= 0f) return true;
        float x = transform.position.x;
        return dir > 0 ? x < arena.xMax - ajustes.margenArena : x > arena.xMin + ajustes.margenArena;
    }

    private float LimitarX(float x) =>
        arena.width <= 0f ? x : Mathf.Clamp(x, arena.xMin + ajustes.margenArena, arena.xMax - ajustes.margenArena);

    // Velocidad para llegar a x en el siguiente paso de fisica, con tope: asi no
    // sale disparado si el juego va a muchos fotogramas (ni atraviesa el suelo).
    private const float VelocidadMaxima = 18f;
    private float VelocidadHacia(float x) =>
        Mathf.Clamp((x - transform.position.x) / Mathf.Max(Time.deltaTime, Time.fixedDeltaTime), -VelocidadMaxima, VelocidadMaxima);

    // Te estudia: guarda su distancia (se acerca si estas lejos, se queda si
    // estas a su gusto), siempre mirandote. Acaba antes si te lee algo.
    private IEnumerator Estudiar(float segundos)
    {
        CambiarEstado(EstadoEnemigo.Alerta);
        neutral = true;
        for (float t = 0f; t < segundos && !HayLectura; t += Time.deltaTime)
        {
            BuscarPlayer();
            MirarAlPlayer();
            float d = Mathf.Abs(DxPlayer);
            float objetivo = 0f;
            if (d > ajustes.distanciaEstudio.y) objetivo = mirada * ajustes.velocidadCorrer * 0.75f * Desafio.MultVelocidad;
            if (Mathf.Abs(objetivo) > 0.1f) anim.Reproducir(ClipCorrer); else if (Mathf.Abs(velX) < 0.5f) anim.Reproducir(ClipQuieto);
            MoverHacia(objetivo);
            yield return null;
        }
        neutral = false;
        Parar();
        anim.Reproducir(ClipQuieto);
    }

    // Corre hacia ti hasta tenerte a su alcance (o el borde, o el tiempo maximo).
    private IEnumerator Acercarse(float alcance)
    {
        BuscarPlayer();
        // Pegado a ti (o encima): un paso atras para tener sitio para el golpe.
        if (Mathf.Abs(DxPlayer) < DistanciaMinima) yield return Separarse();
        MirarYa();
        if (Mathf.Abs(DxPlayer) <= alcance) { Parar(); yield break; }
        CambiarEstado(EstadoEnemigo.Persecucion);
        anim.Reproducir(ClipCorrer);
        for (float t = 0f; t < ajustes.tiempoMaxCorrer && Mathf.Abs(DxPlayer) > alcance; t += Time.deltaTime)
        {
            MirarAlPlayer();
            if (!DentroDeArena(mirada)) break;
            MoverHacia(mirada * ajustes.velocidadCorrer * Desafio.MultVelocidad);
            yield return null;
        }
        // Frena en seco al llegar (unos pocos fotogramas, sin pasarse).
        for (float t = 0f; t < 0.12f && Mathf.Abs(velX) > 0.2f; t += Time.deltaTime) { MoverHacia(0f); yield return null; }
        Parar();
        MirarYa();
    }

    // Distancia minima para que sus golpes (que van hacia delante) te alcancen.
    private const float DistanciaMinima = 0.75f;

    // Paso atras rapido, mirandote, hasta tener sitio (o el borde de la arena).
    private IEnumerator Separarse()
    {
        BuscarPlayer();
        int lejos = DxPlayer > 0f ? -1 : 1;
        // Contra el borde: se separa hacia el otro lado (pasa por detras de ti).
        if (!DentroDeArena(lejos)) lejos = -lejos;
        Mirar(-lejos);
        AtravesarPlayer(true);
        for (float t = 0f; t < 0.35f && Mathf.Abs(DxPlayer) < DistanciaMinima + 0.25f; t += Time.deltaTime)
        {
            if (!DentroDeArena(lejos)) break;
            rb.linearVelocity = new Vector2(lejos * ajustes.velocidadCorrer, rb.linearVelocity.y);
            yield return null;
        }
        AtravesarPlayer(false);
        Parar();
    }

    // Rodada en una direccion (invulnerable en sus cuadros). Atraviesa al jugador.
    private IEnumerator Rodar(int dir, float distancia)
    {
        CambiarEstado(EstadoEnemigo.Reposicion);
        Mirar(dir);
        float x0 = transform.position.x;
        float xF = LimitarX(x0 + dir * distancia);
        if (HayParedHacia(dir, Mathf.Abs(xF - x0), 0.5f)) xF = x0 + dir * Mathf.Max(0f, Mathf.Abs(xF - x0) - 0.6f);
        anim.Reproducir(ClipRodar, true, 1f);
        Sonar("esquiva", 0.6f);
        float dur = anim.Duracion(ClipRodar);
        AtravesarPlayer(true);
        for (float t = 0f; t < dur && !anim.Terminado; t += Time.deltaTime)
        {
            int f = anim.Fotograma;
            invulnerable = f >= ajustes.rodadaInvulnerable.x && f <= ajustes.rodadaInvulnerable.y;
            float k = Mathf.Clamp01(t / (dur * 0.8f));
            float x = Mathf.Lerp(x0, xF, 1f - (1f - k) * (1f - k));
            rb.linearVelocity = new Vector2(VelocidadHacia(x), rb.linearVelocity.y);
            yield return null;
        }
        invulnerable = false;
        Parar();
        AtravesarPlayer(false);
        anim.Reproducir(ClipQuieto, true);
    }

    // El cuerpo no choca con el del jugador mientras rueda o salta por encima.
    private Collider2D[] collidersPlayer;
    private void AtravesarPlayer(bool atravesar)
    {
        Collider2D mio = GetComponent<Collider2D>();
        if (mio == null || !BuscarPlayer()) return;
        if (collidersPlayer == null || collidersPlayer.Length == 0) collidersPlayer = player.GetComponentsInChildren<Collider2D>();
        foreach (Collider2D c in collidersPlayer) if (c != null && !c.isTrigger) Physics2D.IgnoreCollision(mio, c, atravesar);
    }

    // ------------------------------------------------------------------ Ataques y golpes

    // Los golpes de un ataque seguidos, aunque pares alguno. A veces corta el
    // combo a medias y rueda hacia atras. Parar todos (2 o mas) lo aturde.
    private IEnumerator HacerAtaque(AjustesBlackKnight.Ataque a)
    {
        yield return Acercarse(a.alcance);
        ritmo.QuitarAviso();
        ritmo.Empezar();
        Resultados.Clear();
        int n = a.golpes.Count, reales = 0;
        bool cortado = false;
        for (int i = 0; i < n; i++)
        {
            AjustesBlackKnight.Golpe g = ajustes.Buscar(a.golpes[i]);
            if (g == null) { Debug.LogWarning($"[BlackKnight] {a.id}: no existe el golpe '{a.golpes[i]}'."); continue; }
            // Corta a medias (como quien se arrepiente) y se aparta rodando.
            if (i > 0 && i < n - 1 && a.probCortar > 0f && Random.value < a.probCortar)
            {
                cortado = true;
                CombosCortados++;
                yield return Rodar(DxPlayer > 0f ? -1 : 1, ajustes.distanciaRodada * 0.8f);
                break;
            }
            if (ritmo.PideRecuperacion(1))
            {
                yield return Quieto(ajustes.reglas.pausaRecuperacion);
                ritmo.Recuperado();
            }
            if (i == 0) Sonar("jefe_carga_tajo", 0.5f);
            bool siguiente = i < n - 1;
            yield return HacerGolpe(g, !siguiente && n > 1, siguiente);
            if (!g.amago) reales++;
        }
        if (!cortado) while (!anim.Terminado) yield return null;
        Parar();
        if (!cortado && reales >= 2 && ritmo.Perfecto && ritmo.Parrys == reales) Pedir(ajustes.aturdidoCombo, "combo");
        yield return Quieto(a.recuperacion / Vel);
    }

    // Un golpe: su clip (o sigue el mismo si ya va por delante), el aviso desde
    // su cuadro de aviso (sostenido lo que falte, y a veces un retraso extra), y
    // la caja activa en sus cuadros de golpe. "siguiente": hay otro detras (enlaza
    // en su cuadro de corte); si no, el clip llega al final.
    private IEnumerator HacerGolpe(AjustesBlackKnight.Golpe g, bool final, bool siguiente)
    {
        CambiarEstado(EstadoEnemigo.Anticipacion);
        BuscarPlayer();
        // Al empezar un golpe (no a mitad de clip): si esta encima de ti, se separa.
        if (anim.Actual != g.clip && Mathf.Abs(DxPlayer) < DistanciaMinima * 0.6f) yield return Separarse();
        MirarYa();
        Parar();
        GolpeEnCurso = g;
        float vel = Vel;
        bool sigue = anim.Actual == g.clip && !anim.Terminado && anim.Fotograma <= g.cuadroAviso;
        anim.Reproducir(g.clip, !sigue, vel);
        while (anim.Fotograma < g.cuadroAviso && !anim.Terminado) yield return null;

        float fps = Mathf.Max(1f, anim.Fps(g.clip)) * vel;
        float dibujado = Mathf.Max(0, g.golpeDesde - g.cuadroAviso) / fps;
        float objetivo = ritmo.AvisoObjetivo(g.peso, final, g.aviso);
        if (!g.amago && Random.value < AjustesBlackKnight.PorFase(ajustes.probRetraso, fase))
        {
            objetivo += Random.Range(ajustes.retraso.x, ajustes.retraso.y);
            Retrasos++;
        }
        float inicio = Time.time;
        ritmo.Avisar(g.peso, Mathf.Max(objetivo, dibujado) + 0.5f);
        if (objetivo > dibujado + 0.005f)
        {
            anim.Reproducir(g.clip, false, 0f);
            while (Time.time - inicio < objetivo - dibujado) { MirarYa(); yield return null; }
            anim.Reproducir(g.clip, false, vel);
        }
        if (g.amago)
        {
            // Amago: enseña el golpe y lo deja a medias (el siguiente sale enseguida).
            Amagos++;
            ritmo.QuitarAviso();
            GolpeEnCurso = null;
            yield break;
        }
        while (anim.Fotograma < g.golpeDesde && !anim.Terminado) yield return null;
        yield return GolpeActivo(g, Time.time - inicio, false);
        if (siguiente && g.cortarEn >= 0) while (anim.Fotograma < g.cortarEn && !anim.Terminado) yield return null;
        else if (!siguiente) while (!anim.Terminado) yield return null;
        CambiarEstado(EstadoEnemigo.Recuperacion);
    }

    // La caja activa mientras el dibujo muestra el golpe (toca una sola vez) y el
    // avance de esos cuadros. Cuenta el resultado para el combo y el parry.
    private IEnumerator GolpeActivo(AjustesBlackKnight.Golpe g, float aviso, bool desdeArriba)
    {
        float hueco = ritmo.SegundosDesdeUltimoGolpe;
        GolpesActivos++;
        AlActivarGolpe?.Invoke(g, aviso, hueco);
        ritmo.QuitarAviso();
        Sonar(g.peso == PesoGolpe.Pesado ? "jefe_tajo_fuerte" : "jefe_tajo");

        float fps = Mathf.Max(1f, anim.Fps(g.clip)) * Vel;
        float durGolpe = (g.golpeHasta - g.golpeDesde + 1) / fps;
        float x0 = transform.position.x;
        float xF = x0;
        if (g.avance > 0f)
        {
            // Avanza, pero nunca te atraviesa (se queda delante de ti).
            float hasta = player != null ? (PosPlayer.x - mirada * 0.55f - x0) * mirada : g.avance;
            xF = LimitarX(x0 + mirada * Mathf.Clamp(Mathf.Min(g.avance, hasta), 0f, g.avance));
        }

        PlayerControler pc = player != null ? player.GetComponent<PlayerControler>() : null;
        int vidaAntes = pc != null ? pc.VidaActual : 0;
        int dano = ritmo.DanoPermitido(g.dano, pc);
        PlayerControler.ResultadoDano? r = null;
        int hecho = 0;
        float t = 0f;
        while (anim.Fotograma <= g.golpeHasta && !anim.Terminado)
        {
            t += Time.deltaTime;
            if (g.avance > 0f)
            {
                float x = Mathf.Lerp(x0, xF, Mathf.Clamp01(t / Mathf.Max(0.05f, durGolpe)));
                if (HayParedHacia(mirada, 0.5f, 0.5f)) x = transform.position.x;
                rb.linearVelocity = new Vector2(VelocidadHacia(x), rb.linearVelocity.y);
            }
            if (!r.HasValue)
            {
                ritmo.AntesDeGolpear();
                Vector2 centro = (Vector2)transform.position + new Vector2(g.cajaCentro.x * mirada, g.cajaCentro.y);
                // Cada golpe que te alcanza acumula quemadura (bloqueado, un 30 %; con parry, nada).
                r = GolpeCazadora.Caja(centro, g.cajaTamano, dano, this, PlayerControler.TipoDano.Fisico,
                                       EstadoPlayer.Quemadura, ajustes.quemaduraPorGolpe, desdeArriba: desdeArriba);
                ritmo.DespuesDeGolpear();
                // El dano se mide en el mismo instante (despues algo puede curarte).
                if (r.HasValue && pc != null) hecho = Mathf.Max(0, vidaAntes - pc.VidaActual);
                if (DepuracionCazadora.VerCajas) CajaDebug.Mostrar(centro, g.cajaTamano, ajustes.reglas.ColorAviso(g.peso), 0.03f);
            }
            yield return null;
        }
        Parar();
        Resultados.Add(r);
        Vector2 punto = player != null ? (Vector2)player.position + new Vector2(-mirada * 0.3f, 0.5f) : (Vector2)transform.position;
        float tono = ritmo.TonoParry;
        if (ritmo.Registrar(r, hecho, punto, mirada > 0 ? 20f : 160f))
            Sonar("jefe_parry", 0.8f, tono);
        GolpesActivos--;
        GolpeEnCurso = null;
        ritmo.FinGolpe();
        AlTerminarGolpe?.Invoke(g, r);
    }

    // Quieto mirandote (recuperacion: tu hueco para pegarle).
    private IEnumerator Quieto(float segundos)
    {
        CambiarEstado(EstadoEnemigo.Recuperacion);
        anim.Reproducir(ClipQuieto);
        for (float t = 0f; t < segundos; t += Time.deltaTime)
        {
            MoverHacia(0f);
            MirarAlPlayer();
            yield return null;
        }
        Parar();
    }

    // ------------------------------------------------------------------ Aturdimiento

    // El sagrado lo pide durante cualquier accion: se cumple al acabarla.
    public bool PedirAturdimiento(float segundos) => Pedir(segundos, "sagrado");

    private bool Pedir(float segundos, string tipo)
    {
        if (salud.Muerto || cambiando || aturdido || segundos <= aturdimientoPendiente) return false;
        aturdimientoPendiente = segundos;
        tipoPendiente = tipo;
        return true;
    }

    private IEnumerator TrasAccion()
    {
        float s = aturdimientoPendiente;
        aturdimientoPendiente = 0f;
        if (s > 0f) yield return Aturdirse(s, tipoPendiente);
        ritmo.Recuperado();
        // Lo que leyo mientras atacaba ya no vale (el frasco si: lo castiga igual).
        pideReaccion = pideSalto = false;
    }

    // Dolorido (el cuadro final de su dano), temblando.
    private IEnumerator Aturdirse(float segundos, string tipo)
    {
        aturdido = true;
        CambiarEstado(EstadoEnemigo.Golpeado);
        Parar();
        ritmo.QuitarAviso();
        Aturdimientos.Add((tipo, segundos));
        TextoFlotante.Mostrar(ajustes.textoAturdido, (Vector2)transform.position + Vector2.up * 1.6f, ajustes.colorTextoAturdido, 1f);
        anim.Reproducir(ClipDolor, true);
        for (float t = 0f; t < segundos; t += Time.deltaTime)
        {
            if (visual != null) visual.localPosition = (Vector3)(Random.insideUnitCircle * ajustes.temblorAturdido * (1f - t / segundos));
            Parar();
            yield return null;
        }
        if (visual != null) visual.localPosition = Vector3.zero;
        aturdido = false;
        anim.Reproducir(ClipQuieto, true);
    }

    // ------------------------------------------------------------------ Fases

    // Al vaciar una barra: se rehace (el buff con chispas rojas), invulnerable, y
    // vuelve con la barra siguiente llena. La fase 2 la avisa la arena (barra
    // nueva y su color); la 3 la pinta el propio jefe sobre la misma barra.
    private IEnumerator CambioFase(int nueva)
    {
        cambiando = true;
        Limpiar();
        CambiarEstado(EstadoEnemigo.Recuperacion);
        AvisarSilencio();
        Sonar("jefe_caida");
        anim.Reproducir(ClipDolor, true);
        for (float t = 0f; t < 0.5f; t += Time.deltaTime) { Parar(); yield return null; }
        while (fase < nueva)
        {
            fase++;
            salud.Revivir(VidaDesafio(ajustes.Vida(fase)));
            if (fase == 2) AvisarCambioFase();
            else
            {
                BarraJefe b = FindAnyObjectByType<BarraJefe>();
                if (b != null) b.NuevaFase(ajustes.colorBarraFase3, "");
                // La arena solo sabe de la fase 2: la 3 la anota el propio jefe en la Bitacora.
                Bitacora.Fase(FichaBitacora, 3);
            }
        }
        string texto = fase >= 3 ? ajustes.textoFase3 : ajustes.textoFase2;
        if (!string.IsNullOrEmpty(texto)) MensajePantalla.Narrativo(texto, 3f);
        Color c = fase >= 3 ? ajustes.colorBarraFase3 : ajustes.colorBarraFase2;
        anim.Reproducir(ClipBuff, true);
        Sonar("jefe_revivir");
        ScreenFlash.Destello(new Color(c.r, c.g, c.b, 0.3f), 0.4f);
        CamaraDinamica.Sacudir(0.6f);
        for (float t = 0f; t < ajustes.duracionCambioFase; t += Time.deltaTime)
        {
            ritmo.Destello(c, 0.5f * (0.5f + 0.5f * Mathf.Sin(t * 14f)));
            Parar();
            yield return null;
        }
        ritmo.Destello(c, 0f);
        anim.Reproducir(ClipQuieto, true);
        cambiando = false;
        // Tras cambiar de fase, si estabas a punto de algo, nada se arrastra.
        finEnfriamientoOndas = Time.time + 4f;
        finEnfriamientoSombras = Time.time + 4f;
        Pensar();
    }

    // Deja todo como al empezar una accion (al cambiar de fase o morir).
    private void Limpiar()
    {
        GolpesActivos = 0;
        GolpeEnCurso = null;
        enGuardia = clavado = enOndas = retirandose = aturdido = invulnerable = camaraAmplia = enErupciones = false;
        aturdimientoPendiente = 0f;
        PlayerControler.TonoSiguienteParry = 1f;
        PlayerControler.SiguienteOrigenX = float.NaN;
        if (ritmo != null) ritmo.QuitarAviso();
        if (visual != null) visual.localPosition = Vector3.zero;
        TerminarVuelo();
        AtravesarPlayer(false);
        LimpiarEfectos();
        Parar();
        AccionActual = "";
    }

    protected override void AlMorir()
    {
        Limpiar();
        QuitarMiSello();
        base.AlMorir();
        Sonar("jefe_muerte");
        AvisarDerrota();
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        LimpiarEfectos();
        QuitarMiSello();
    }
}
