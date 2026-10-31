using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// El Martial Hero: duelista rapido con katana. Minijefe nuevo, SOLO en la sala
// de pruebas (no sale en niveles, Desafios, Bitacora, Bestiario ni logros).
// Sus numeros: Assets/Data/Jefes/Ajustes Martial Hero. Sprites, prefab y entrada
// en la sala: Warrior > Martial Hero > Crear o actualizar.
//
// Como un duelista de Sekiro: se cubre siempre que no ataca (tenido de celeste)
// y solo ataca con su combo. Maquina de estados (EstadoMartial, con nombre):
//   Guardia (te mira cubierto; de frente le entra poco) y ataca con el Combo:
//     - si entras en su radio (ataca enseguida),
//     - si le pegas (contraataque tras un instante),
//     - o al acabar su guardia (corre hacia ti, Acecho, y ataca al llegar).
//   Tras el combo: Aturdido (si paraste todos los golpes) o Recuperacion (quieto y
//   sin guardia: tu hueco para pegarle) y vuelta a la Guardia.
//   Un golpe sagrado rompe la guardia con un aturdimiento corto.
//
// Combo: desenvaine, tajo, desenvaine, tajo (los dos dibujos enlazan), seguidos
// y sin pausas, como en el dibujo; NO se corta aunque pares un golpe. Cada golpe
// avisa en naranja durante su propio movimiento. Las reglas de ritmo y parry son
// las comunes (RitmoCombo + ReglasCombo, dentro de su ficha).
//
// Aturdido por un parry recibe mas dano y, al levantarse, queda inmune a otro
// aturdimiento un momento (tus parrys sacan chispas grises y, si lo habrian
// aturdido, sale "Firme"). Los aturdimientos del sagrado no multiplican el dano
// ni dan inmunidad, y estando inmune tampoco lo aturden.
//
// Reaccion al dano: solo el destello blanco del HitFlash; nunca corta lo que hace.
public class JefeMartialHero : JefeBase, IModificadorDano, IAturdible
{
    public enum EstadoMartial { Guardia, Acecho, Combo, Aturdido, Recuperacion, Muerte }
    // Por que ataco: entraste en su radio, le pegaste en guardia o fue a por ti.
    public enum Motivo { Radio, Contraataque, Acecho }

    // Clips del animador (los crea Warrior > Martial Hero).
    public const string ClipQuieto = "quieto", ClipCorrer = "correr", ClipCaer = "caer", ClipMuerte = "muerte",
                        ClipAturdido = "aturdido", ClipLevantarse = "levantarse",
                        ClipDesenvaine = "desenvaine", ClipTajo = "tajo", ClipGuardia = "guardia";

    [SerializeField] private AjustesMartialHero ajustes;

    private Rect arena;
    private float suelo;
    private string ataqueForzado, ultimoElegido;
    private RitmoCombo ritmo;
    private float aturdimientoPendiente, inmuneHasta = -99f;
    private string tipoPendiente;
    private bool pendientePorParry, aturdidoPorParry;
    // Guardia: cubierto; un golpe pide el contraataque; el sagrado la rompe.
    private bool enGuardia, pideContraataque, guardiaRota, pideRadio;

    public AjustesMartialHero Ajustes => ajustes;
    public RitmoCombo Ritmo => ritmo;
    public EstadoMartial EstadoActual { get; private set; } = EstadoMartial.Guardia;
    public event System.Action<EstadoMartial> AlCambiarEstadoMartial;
    public int Mirada => mirada;

    // Lo que leen las pruebas: el golpe que avisa o golpea ahora (null entre
    // golpes), golpes con la caja activa (nunca mas de uno), el aviso real y el
    // hueco con el golpe anterior de cada golpe, y el resultado de cada uno.
    public AjustesMartialHero.Golpe GolpeEnCurso { get; private set; }
    public int GolpesActivos { get; private set; }
    public event System.Action<AjustesMartialHero.Golpe, float, float> AlActivarGolpe;
    public event System.Action<AjustesMartialHero.Golpe, PlayerControler.ResultadoDano?> AlTerminarGolpe;
    public readonly List<PlayerControler.ResultadoDano?> Resultados = new List<PlayerControler.ResultadoDano?>();
    public readonly List<string> CombosHechos = new List<string>();
    // Por que empezo cada combo, y cuando (desde que paso lo que lo provoco).
    public readonly List<(Motivo motivo, float hora)> Ataques = new List<(Motivo, float)>();
    public int DanoUltimoAtaque { get; private set; }
    public readonly List<(string tipo, float segundos)> Aturdimientos = new List<(string, float)>();
    public bool Aturdido => EstadoActual == EstadoMartial.Aturdido;
    public bool EnGuardia => enGuardia;
    // Tras un aturdimiento por parry: nada lo vuelve a aturdir hasta entonces.
    public bool Inmune => Time.time < inmuneHasta;
    // Aturdimientos que la inmunidad no dejo pasar (tipo).
    public readonly List<string> Rechazados = new List<string>();
    // Guardias rotas por el sagrado.
    public int GuardiasRotas { get; private set; }
    // Cada golpe que le entra: dano antes y despues de su guardia (lo leen las pruebas).
    public event System.Action<int, int> AlModificarDano;

    // ------------------------------------------------------------------ Sala de pruebas

    public override string[] AtaquesPrueba
    {
        get
        {
            var l = new List<string>();
            if (ajustes == null) return l.ToArray();
            foreach (AjustesMartialHero.Variante v in ajustes.variantes) if (v != null && !string.IsNullOrEmpty(v.id)) l.Add(v.id);
            return l.ToArray();
        }
    }

    public override void ForzarAtaque(string ataque) => ataqueForzado = ataque;

    public override void Configurar(Rect zona, float alturaSuelo)
    {
        arena = zona;
        suelo = alturaSuelo;
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
        ritmo = gameObject.AddComponent<RitmoCombo>();
        ritmo.Configurar(ajustes.reglas, visual != null ? visual.GetComponent<SpriteRenderer>() : null);
        PonerRitmoAnimacion();
    }

    protected override void Start()
    {
        salud.Revivir(VidaDesafio(ajustes.vida));
        base.Start();
    }

    // En guardia (tambien mientras corre hacia ti), de frente le entra solo una
    // parte (nunca menos de 1); por la espalda, entero. Todo golpe en guardia pide
    // el contraataque; un golpe sagrado la rompe (y entra entero). Aturdido por un
    // parry, mas dano. Recuperandose o atacando, entero.
    public int Modificar(int dano, TipoArma arma)
    {
        int antes = dano;
        if (enGuardia && EnemyHealth.ElementoDelGolpe == Elemento.Sagrado)
        {
            enGuardia = false;
            guardiaRota = true;
        }
        else if (enGuardia)
        {
            if (DeFrente) dano = Mathf.Max(1, Mathf.RoundToInt(dano * ajustes.danoEnGuardia));
            pideContraataque = true;
            ChispasGuardia();
        }
        else if (Aturdido && aturdidoPorParry)
            dano = Mathf.RoundToInt(dano * ajustes.multDanoAturdido);
        AlModificarDano?.Invoke(antes, dano);
        return dano;
    }

    // El jugador esta del lado al que mira.
    private bool DeFrente => player == null || Mathf.Abs(DxPlayer) < 0.05f || (DxPlayer > 0f ? 1 : -1) == mirada;

    // Cuadros por segundo de cada tira, desde la ficha.
    private void PonerRitmoAnimacion()
    {
        if (anim == null) return;
        foreach (AnimadorHoja.Clip c in anim.clips)
        {
            if (c == null) continue;
            if (c.nombre == ClipQuieto || c.nombre == ClipGuardia) c.fps = ajustes.fpsEspera;
            else if (c.nombre == ClipCorrer) c.fps = ajustes.fpsCorrer;
            else if (c.nombre == ClipMuerte) c.fps = ajustes.fpsMuerte;
            else if (c.nombre == ClipAturdido || c.nombre == ClipLevantarse) c.fps = ajustes.fpsAturdido;
            else if (c.nombre.StartsWith(ClipDesenvaine) || c.nombre.StartsWith(ClipTajo)) c.fps = ajustes.fpsAtaque;
        }
    }

    private void Pasar(EstadoMartial e)
    {
        if (EstadoActual == e) return;
        EstadoActual = e;
        AlCambiarEstadoMartial?.Invoke(e);
    }

    // ------------------------------------------------------------------ Cerebro

    protected override IEnumerator Cerebro()
    {
        yield return Entrada();
        while (true)
        {
            yield return Cubrirse(Random.Range(ajustes.pausa.x, ajustes.pausa.y));
            if (guardiaRota) { yield return GuardiaRota(); continue; }
            Motivo motivo;
            if (pideContraataque)
            {
                // Te ha parado el golpe: contraataca tras un instante (sigue cubierto).
                motivo = Motivo.Contraataque;
                yield return Cubierto(ajustes.retrasoContraataque);
                if (guardiaRota) { yield return GuardiaRota(); continue; }
            }
            else if (pideRadio) motivo = Motivo.Radio;
            else
            {
                motivo = Motivo.Acecho;
                yield return Acechar();
                if (guardiaRota) { yield return GuardiaRota(); continue; }
            }
            pideContraataque = pideRadio = false;
            Ataques.Add((motivo, Time.time));
            // Si no te alcanza desde donde esta (entraste por el borde de su radio o
            // le pegaste mientras corria), se acerca antes de atacar.
            if (Mathf.Abs(DxPlayer) > ajustes.distanciaAtaque)
            {
                yield return Acechar();
                pideContraataque = false;
                if (guardiaRota) { yield return GuardiaRota(); continue; }
            }
            enGuardia = false;
            AjustesMartialHero.Variante v = Elegir();
            if (v == null) continue;
            AnunciarAtaque(v.id);
            yield return Combo(v);
            yield return TrasAccion();
        }
    }

    // Entrada: cae en la arena; al tocar el suelo salen su nombre y su barra.
    private IEnumerator Entrada()
    {
        Pasar(EstadoMartial.Guardia);
        CambiarEstado(EstadoEnemigo.Alerta);
        anim.Reproducir(ClipCaer);
        BuscarPlayer();
        MirarYa();
        while (!EnSuelo) yield return null;
        Sonar("jefe_aparicion");
        AvisarAterrizaje();
    }

    // Guardia: agachado con la mano en la empunadura (cuadros 0-2 del desenvaine),
    // mirandote. Acaba al pasar el tiempo (mas corto en Dificil), si entras en su
    // radio, si le pegas o si un golpe sagrado la rompe.
    private IEnumerator Cubrirse(float segundos)
    {
        PonerGuardia();
        segundos = PausaPrueba(PausaDesafio(segundos));
        BuscarPlayer();
        bool estabaDentro = DentroRadio;
        float detras = -1f;
        for (float t = 0f; t < segundos && !pideContraataque && !pideRadio && !guardiaRota; t += Time.deltaTime)
        {
            BuscarPlayer();
            Tenir();
            Girar(ref detras);
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            // Entras en su radio (si ya estabas dentro, ataca al acabar la guardia).
            bool dentro = DentroRadio;
            if (dentro && !estabaDentro) pideRadio = true;
            estabaDentro = dentro;
            yield return null;
        }
    }

    // Sigue cubierto un rato (antes de contraatacar).
    private IEnumerator Cubierto(float segundos)
    {
        PonerGuardia();
        float detras = -1f;
        for (float t = 0f; t < segundos && !guardiaRota; t += Time.deltaTime)
        {
            Tenir();
            Girar(ref detras);
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            yield return null;
        }
    }

    private void PonerGuardia()
    {
        Pasar(EstadoMartial.Guardia);
        CambiarEstado(EstadoEnemigo.Alerta);
        enGuardia = true;
        anim.Reproducir(ClipGuardia);
    }

    // Tinte celeste mientras se cubre (cada fotograma: el destello al recibir un
    // golpe lo borra un instante).
    private void Tenir()
    {
        if (enGuardia) ritmo.Destello(ajustes.colorGuardia, ajustes.intensidadGuardia);
    }

    // Si estas a su espalda, se gira hacia ti tras un instante.
    private void Girar(ref float detras)
    {
        if (DeFrente) { detras = -1f; return; }
        if (detras < 0f) detras = Time.time;
        else if (Time.time - detras >= ajustes.giroGuardia) { MirarYa(); detras = -1f; }
    }

    private bool DentroRadio => player != null && DistanciaPlayer <= ajustes.radioEntrada;

    // Acecho: corre hacia ti hasta su distancia de ataque (o el borde de la arena).
    // Sigue cubierto: si le pegas, para y contraataca.
    private IEnumerator Acechar()
    {
        BuscarPlayer();
        MirarYa();
        if (Mathf.Abs(DxPlayer) <= ajustes.distanciaAtaque) yield break;
        Pasar(EstadoMartial.Acecho);
        CambiarEstado(EstadoEnemigo.Persecucion);
        anim.Reproducir(ClipCorrer);
        for (float t = 0f; t < ajustes.tiempoMaxCorrer && Mathf.Abs(DxPlayer) > ajustes.distanciaAtaque && !pideContraataque && !guardiaRota; t += Time.deltaTime)
        {
            MirarAlPlayer();
            Tenir();
            if (!DentroDeArena(mirada)) break;
            rb.linearVelocity = new Vector2(mirada * ajustes.velocidadCorrer * Desafio.MultVelocidad, rb.linearVelocity.y);
            yield return null;
        }
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        MirarYa();
        if (pideContraataque) yield return Cubierto(ajustes.retrasoContraataque);
    }

    private bool DentroDeArena(int dir)
    {
        if (arena.width <= 0f) return true;
        float x = transform.position.x;
        return dir > 0 ? x < arena.xMax - ajustes.margenArena : x > arena.xMin + ajustes.margenArena;
    }

    // El forzado desde la sala; si no, una variante al azar por frecuencia y
    // nunca la misma dos veces seguidas.
    private AjustesMartialHero.Variante Elegir()
    {
        if (!string.IsNullOrEmpty(ataqueForzado))
        {
            AjustesMartialHero.Variante f = ajustes.Buscar(ataqueForzado);
            ataqueForzado = null;
            if (f != null && f.golpes.Count > 0) { ultimoElegido = f.id; return f; }
        }
        var lista = ajustes.variantes.FindAll(x => x != null && x.frecuencia > 0f && x.golpes.Count > 0);
        if (lista.Count > 1) lista.RemoveAll(x => x.id == ultimoElegido);
        float total = 0f;
        foreach (AjustesMartialHero.Variante x in lista) total += x.frecuencia;
        float r = Random.value * total;
        foreach (AjustesMartialHero.Variante x in lista)
        {
            r -= x.frecuencia;
            if (r <= 0f) { ultimoElegido = x.id; return x; }
        }
        if (lista.Count == 0) return null;
        ultimoElegido = lista[lista.Count - 1].id;
        return lista[lista.Count - 1];
    }

    // ------------------------------------------------------------------ Combo

    // Los golpes de la variante seguidos, aunque pares alguno. Parar todos lo
    // aturde al acabar; fallar uno (bloqueo normal o golpe recibido), no.
    private IEnumerator Combo(AjustesMartialHero.Variante v)
    {
        Pasar(EstadoMartial.Combo);
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        ritmo.QuitarAviso();
        Resultados.Clear();
        ritmo.Empezar();
        int n = v.golpes.Count;
        for (int i = 0; i < n; i++)
        {
            // Nunca mas golpes seguidos de los que permite la ficha sin recuperarse.
            if (ritmo.PideRecuperacion(1))
            {
                yield return Recuperarse(ajustes.reglas.pausaRecuperacion);
                Pasar(EstadoMartial.Combo);
            }
            if (i == 0) Sonar("jefe_carga_tajo", 0.6f);
            yield return HacerGolpe(ajustes.DeTipo(v.golpes[i]), i == n - 1 && n > 1);
        }
        while (!anim.Terminado) yield return null;
        CombosHechos.Add(v.id);
        DanoUltimoAtaque = ritmo.DanoCombo;
        if (ritmo.Perfecto && ritmo.Parrys == n) Pedir(ajustes.aturdidoCombo, "combo", true);
    }

    // Su clip entero, con el aviso de color durante el movimiento hasta el golpe.
    // Si el dibujo no llega al aviso de la ficha (o a la pausa minima con el golpe
    // anterior), sostiene el cuadro del destello lo que falte; si llega, va seguido.
    // El golpe toca una sola vez mientras se ven sus cuadros de golpe.
    private IEnumerator HacerGolpe(AjustesMartialHero.Golpe g, bool final)
    {
        CambiarEstado(EstadoEnemigo.Anticipacion);
        MirarYa();
        string clip = ClipDe(g);
        int fAviso = g.cuadroAviso, fDesde = g.golpeDesde, fHasta = g.golpeHasta;
        GolpeEnCurso = g;
        float objetivo = ritmo.AvisoObjetivo(g.peso, final, g.aviso);
        float dibujado = fDesde / ajustes.fpsAtaque;
        float inicio = Time.time;
        // El aviso dura hasta el instante del golpe (se quita al activarse; el margen es por si tarda un cuadro mas).
        ritmo.Avisar(g.peso, Mathf.Max(objetivo, dibujado) + 0.5f);
        anim.Reproducir(clip, true, 1f);
        if (objetivo > dibujado + 0.005f)
        {
            while (anim.Fotograma < fAviso && !anim.Terminado) yield return null;
            anim.Reproducir(clip, false, 0f);
            while (Time.time - inicio < objetivo) { rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y); yield return null; }
            anim.Reproducir(clip, false, 1f);
        }
        while (anim.Fotograma < fDesde && !anim.Terminado) yield return null;

        float hueco = ritmo.SegundosDesdeUltimoGolpe;
        GolpesActivos++;
        AlActivarGolpe?.Invoke(g, Time.time - inicio, hueco);
        ritmo.QuitarAviso();
        Sonar(g.peso == PesoGolpe.Pesado ? "jefe_tajo_fuerte" : "jefe_tajo");

        PlayerControler pc = player != null ? player.GetComponent<PlayerControler>() : null;
        int vidaAntes = pc != null ? pc.VidaActual : 0;
        int dano = ritmo.DanoPermitido(g.dano, pc);
        PlayerControler.ResultadoDano? r = null;
        while (!r.HasValue && anim.Fotograma <= fHasta)
        {
            ritmo.AntesDeGolpear();
            r = Golpear(g.cajaCentro, g.cajaTamano, dano, PlayerControler.TipoDano.Fisico);
            ritmo.DespuesDeGolpear();
            if (DepuracionCazadora.VerCajas)
                CajaDebug.Mostrar((Vector2)transform.position + new Vector2(g.cajaCentro.x * mirada, g.cajaCentro.y), g.cajaTamano, ajustes.reglas.ColorAviso(g.peso), 0.03f);
            if (r.HasValue || anim.Terminado) break;
            yield return null;
        }
        Resultados.Add(r);
        Vector2 punto = player != null ? (Vector2)player.position + new Vector2(-mirada * 0.3f, 0.6f) : (Vector2)transform.position;
        float tono = ritmo.TonoParry;
        // Inmune: chispas grises (el parry vale, pero no lo va a aturdir).
        Color? a = Inmune ? ajustes.colorChispasInmune : (Color?)null, b = Inmune ? ajustes.colorChispasInmune2 : (Color?)null;
        if (ritmo.Registrar(r, pc != null ? vidaAntes - pc.VidaActual : 0, punto, mirada > 0 ? 20f : 160f, a, b))
            Sonar("jefe_parry", 0.8f, tono);
        while (anim.Fotograma <= fHasta && !anim.Terminado) yield return null;
        GolpesActivos--;
        GolpeEnCurso = null;
        ritmo.FinGolpe();
        AlTerminarGolpe?.Invoke(g, r);
        CambiarEstado(EstadoEnemigo.Recuperacion);
    }

    // El desenvaine usa la tira Attack1; el tajo, la Attack2.
    private string ClipDe(AjustesMartialHero.Golpe g) => g == ajustes.desenvaine ? ClipDesenvaine : ClipTajo;

    // ------------------------------------------------------------------ Guardia

    // Chispas y el sonido del bloqueo en el choque con tu espada.
    private void ChispasGuardia()
    {
        Sonar("bloqueo", 0.8f);
        Vector2 punto = (Vector2)transform.position + new Vector2(mirada * 0.5f, 0.9f);
        ritmo.Chispas.Chispas(punto, ajustes.chispasGuardia, ajustes.reglas.colorChispas, ajustes.reglas.colorChispas2, mirada > 0 ? 20f : 160f);
    }

    // Un golpe sagrado rompio la guardia: aturdido corto (si no esta inmune).
    private IEnumerator GuardiaRota()
    {
        guardiaRota = pideContraataque = pideRadio = false;
        enGuardia = false;
        GuardiasRotas++;
        ritmo.QuitarAviso();
        TextoFlotante.Mostrar(ajustes.textoGuardiaRota, (Vector2)transform.position + Vector2.up * 2.6f, ajustes.colorTextoAturdido, 1f);
        // Si el sagrado ya pidio su aturdimiento con ese mismo golpe, sale ese.
        if (Pedir(ajustes.aturdidoRomperGuardia, "sagrado_guardia", false) || aturdimientoPendiente > 0f) yield return TrasAccion();
    }

    // ------------------------------------------------------------------ Aturdimiento y recuperacion

    // El sagrado (EstadosEnemigo) lo pide durante cualquier accion: se cumple al acabarla.
    public bool PedirAturdimiento(float segundos) => Pedir(segundos, "sagrado", false);

    // porParry: el que se gana parando (multiplica el dano y da inmunidad al acabar).
    private bool Pedir(float segundos, string tipo, bool porParry)
    {
        if (salud.Muerto || Aturdido || segundos <= 0f) return false;
        if (Inmune)
        {
            Rechazados.Add(tipo);
            if (porParry) TextoFlotante.Mostrar(ajustes.textoInmune, (Vector2)transform.position + Vector2.up * 2.3f, ajustes.colorTextoInmune, 1f);
            return false;
        }
        if (segundos <= aturdimientoPendiente) return false;
        aturdimientoPendiente = segundos;
        tipoPendiente = tipo;
        pendientePorParry = porParry;
        return true;
    }

    // Tras cada accion: el aturdimiento pendiente o la pausa de recuperacion.
    private IEnumerator TrasAccion()
    {
        enGuardia = false;
        float s = aturdimientoPendiente;
        string tipo = tipoPendiente;
        bool porParry = pendientePorParry;
        aturdimientoPendiente = 0f;
        if (s > 0f) yield return Aturdirse(s, tipo, porParry);
        else yield return Recuperarse(ajustes.reglas.pausaRecuperacion);
        ritmo.Recuperado();
        // Lo que le pegaste mientras no se cubria no cuenta para el contraataque.
        pideContraataque = pideRadio = false;
    }

    // Quieto y sin guardia, mirandote: tu hueco tras su triple.
    private IEnumerator Recuperarse(float segundos)
    {
        Pasar(EstadoMartial.Recuperacion);
        CambiarEstado(EstadoEnemigo.Recuperacion);
        anim.Reproducir(ClipQuieto, true);
        for (float t = 0f; t < segundos; t += Time.deltaTime)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            MirarAlPlayer();
            yield return null;
        }
        ritmo.Recuperado();
    }

    // De rodillas (cuadros 2-4 de la muerte) temblando; al final se levanta (al reves).
    private IEnumerator Aturdirse(float segundos, string tipo, bool porParry)
    {
        aturdidoPorParry = porParry;
        Pasar(EstadoMartial.Aturdido);
        CambiarEstado(EstadoEnemigo.Golpeado);
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        ritmo.QuitarAviso();
        Aturdimientos.Add((tipo, segundos));
        TextoFlotante.Mostrar(ajustes.textoAturdido, (Vector2)transform.position + Vector2.up * 2.3f, ajustes.colorTextoAturdido, 1f);
        anim.Reproducir(ClipAturdido, true);
        float levantar = Mathf.Min(segundos, anim.Duracion(ClipLevantarse));
        float t = 0f;
        for (; t < segundos - levantar; t += Time.deltaTime)
        {
            visual.localPosition = (Vector3)(Random.insideUnitCircle * ajustes.temblorAturdido * (1f - t / segundos));
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            yield return null;
        }
        visual.localPosition = Vector3.zero;
        anim.Reproducir(ClipLevantarse, true);
        for (; t < segundos; t += Time.deltaTime) yield return null;
        aturdidoPorParry = false;
        if (porParry) inmuneHasta = Time.time + ajustes.inmunidadTrasAturdido;
        anim.Reproducir(ClipQuieto, true);
    }

    // ------------------------------------------------------------------ Muerte

    // Queda sentado con la espada en el suelo hasta que la sala lo limpie.
    protected override void AlMorir()
    {
        base.AlMorir();
        Pasar(EstadoMartial.Muerte);
        enGuardia = pideContraataque = guardiaRota = aturdidoPorParry = pideRadio = false;
        GolpesActivos = 0;
        GolpeEnCurso = null;
        aturdimientoPendiente = 0f;
        PlayerControler.TonoSiguienteParry = 1f;
        if (ritmo != null) ritmo.QuitarAviso();
        if (visual != null) visual.localPosition = Vector3.zero;
        Sonar("jefe_muerte");
        AvisarDerrota();
    }
}
