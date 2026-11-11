using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// El Black Knight (2): te lee como lo haria otra persona.
//   - Si machacas el ataque cerca de el (y esta libre), a veces reacciona: te
//     para con su guardia, rueda a tu espalda o se aparta rodando. Cuanto mas
//     machacas, mas a menudo (con tope; se reinicia en cada pelea).
//   - Si bebes un frasco cerca, va a por ti con el desenvaine.
//   - Si te quedas quieto un rato, salta sobre ti.
//   - A veces envaina para provocarte: si te acercas o le pegas, desenvaine.
// Su guardia: la espada en alto. Si le pegas de frente, te para el golpe (te
// aturde un instante) y responde con un estallido. Si no le pegas, al bajarla
// queda abierto un momento.
public partial class JefeBlackKnight
{
    // Libre para leerte (te esta estudiando).
    private bool neutral;
    private bool pideReaccion, pideCastigo, pideSalto;
    private float adaptacion;
    private readonly Queue<float> pulsaciones = new Queue<float>();
    private bool atacabaAntes;
    private float quietoDesde = -1f;
    private PlayerControler pcCache;

    // Guardia (su parry) y si tu golpe choco con ella.
    private bool enGuardia, golpeParado;
    public bool EnGuardia => enGuardia;

    // Lo que leen las pruebas.
    public readonly List<string> Lecturas = new List<string>();
    public float Adaptacion => adaptacion;
    public int ParrysDelJefe { get; private set; }

    private bool HayLectura => pideReaccion || pideCastigo || pideSalto;

    protected override void OnEnable() { base.OnEnable(); ReservaPociones.AlBeber += AlBeberJugador; }
    protected override void OnDisable() { base.OnDisable(); ReservaPociones.AlBeber -= AlBeberJugador; }

    private PlayerControler PC
    {
        get
        {
            if (pcCache == null && BuscarPlayer()) pcCache = player.GetComponent<PlayerControler>();
            return pcCache;
        }
    }

    protected override void Update()
    {
        base.Update();
        if (salud == null || salud.Muerto || ajustes == null) return;
        // Red de seguridad: si algo lo dejara bajo el suelo de la arena, vuelve encima.
        if (arena.height > 0f && !volando && transform.position.y < arena.yMin - 2f)
        {
            Debug.LogWarning("[BlackKnight] estaba bajo el suelo de la arena: vuelve encima.");
            rb.position = new Vector2(LimitarX(transform.position.x), suelo + 0.2f);
            rb.linearVelocity = Vector2.zero;
        }
        PlayerControler p = PC;
        if (p == null) return;
        ActualizarCamara();

        // Cada vez que empiezas un ataque (cuenta las de los ultimos 1,5 s).
        bool ataca = p.Atacando;
        if (ataca && !atacabaAntes) AlAtacarJugador();
        atacabaAntes = ataca;
        while (pulsaciones.Count > 0 && Time.time - pulsaciones.Peek() > 1.5f) pulsaciones.Dequeue();

        // Quieto: en el suelo, sin moverte ni atacar.
        bool quieto = p.EnSuelo && Mathf.Abs(p.Velocidad.x) < 0.3f && !ataca && !p.Bloqueando;
        if (!quieto) quietoDesde = -1f;
        else if (quietoDesde < 0f) quietoDesde = Time.time;
        else if (neutral && Time.time - quietoDesde >= ajustes.quietoParaSaltar && Mathf.Abs(DxPlayer) > 1.2f)
        {
            pideSalto = true;
            quietoDesde = Time.time;
        }
    }

    private void AlAtacarJugador()
    {
        pulsaciones.Enqueue(Time.time);
        bool machaca = pulsaciones.Count >= ajustes.golpesParaLeer;
        if (machaca) adaptacion = Mathf.Min(ajustes.topeAdaptacion, adaptacion + ajustes.subidaPorMachacar);
        if (!neutral || cambiando || Mathf.Abs(DxPlayer) > 2.8f) return;
        float prob = AjustesBlackKnight.PorFase(ajustes.probReaccion, fase) * (machaca ? 1f : 0.35f) + adaptacion;
        if (Random.value < prob) pideReaccion = true;
    }

    private void AlBeberJugador()
    {
        if (salud == null || salud.Muerto || cambiando || player == null) return;
        if (Mathf.Abs(DxPlayer) <= ajustes.distanciaCastigoFrasco) pideCastigo = true;
    }

    // Lo que le pide leerte, por orden: el frasco, tus golpes, quedarte quieto.
    private string AccionLeida()
    {
        string r = null;
        if (pideCastigo) r = AccDesenvaine;
        else if (pideReaccion)
        {
            Vector3 w = ajustes.pesosReaccion;
            float total = w.x + w.y + w.z, x = Random.value * Mathf.Max(0.01f, total);
            r = x < w.x ? AccGuardia : x < w.x + w.y ? AccRodadaEspalda : AccRetroceder;
        }
        else if (pideSalto) r = AccSalto;
        LimpiarLectura();
        if (r != null) Lecturas.Add(r);
        return r;
    }

    private void LimpiarLectura() => pideReaccion = pideCastigo = pideSalto = false;

    // ------------------------------------------------------------------ Su guardia (parry)

    // Espada en alto. Si le pegas de frente, para tu golpe: te aturde un instante
    // y responde con el estallido (con su aviso). Si no, la baja y queda abierto.
    private IEnumerator Guardia()
    {
        BuscarPlayer();
        MirarYa();
        Parar();
        CambiarEstado(EstadoEnemigo.Anticipacion);
        anim.Reproducir(ClipGuardia, true);
        enGuardia = true;
        golpeParado = false;
        for (float t = 0f; t < ajustes.duracionGuardia && !golpeParado; t += Time.deltaTime)
        {
            ritmo.Destello(ajustes.colorGuardia, ajustes.brilloGuardia);
            Parar();
            yield return null;
        }
        enGuardia = false;
        ritmo.Destello(ajustes.colorGuardia, 0f);
        if (golpeParado)
        {
            ParrysDelJefe++;
            PlayerControler p = PC;
            if (p != null) p.Aturdir(ajustes.aturdirAlJugador, 0.4f, DxPlayer > 0f ? 1 : -1);
            Sonar("jefe_parry", 0.9f, 0.9f);
            Vector2 punto = (Vector2)transform.position + new Vector2(mirada * 0.5f, 0.8f);
            ritmo.Chispas.Chispas(punto, ajustes.reglas.chispas, ajustes.reglas.colorChispas, ajustes.reglas.colorChispas2, mirada > 0 ? 20f : 160f);
            AjustesBlackKnight.Golpe g = ajustes.Buscar("Estallido");
            if (g != null)
            {
                ritmo.Empezar();
                Resultados.Clear();
                // Tu golpe te dejo a su alcance; si saliste despedido, se acerca un paso.
                yield return Acercarse(1.5f);
                yield return HacerGolpe(g, false, false);
            }
            yield return Quieto(0.35f);
        }
        else
        {
            anim.Reproducir(ClipFalloGuardia, true);
            while (!anim.Terminado) yield return null;
            yield return Quieto(ajustes.abiertoTrasGuardia);
        }
    }

    // ------------------------------------------------------------------ Rodadas

    // Rueda hasta tu espalda (te atraviesa) y te corta por detras.
    private IEnumerator RodadaEspalda()
    {
        yield return Acercarse(2.6f);
        BuscarPlayer();
        int dir = DxPlayer > 0f ? 1 : -1;
        float d = Mathf.Abs(DxPlayer) + 1.4f;
        yield return Rodar(dir, Mathf.Min(d, ajustes.distanciaRodada + 1f));
        AjustesBlackKnight.Golpe g = ajustes.Buscar("Tajo por la espalda");
        if (g == null) yield break;
        ritmo.Empezar();
        Resultados.Clear();
        yield return HacerGolpe(g, false, false);
        yield return Quieto(0.45f / Vel);
    }

    // Se aparta rodando (lejos de ti) y vuelve a estudiarte.
    private IEnumerator Retroceder()
    {
        BuscarPlayer();
        yield return Rodar(DxPlayer > 0f ? -1 : 1, ajustes.distanciaRodada);
        MirarYa();
    }

    // ------------------------------------------------------------------ Salto

    private bool volando;
    public bool Volando => volando;

    // Salta por encima, te sigue en el aire hasta lo alto y cae con la espada
    // sobre donde estabas (aviso rojo mientras cae; se para mires hacia donde mires).
    private IEnumerator SaltoCaida()
    {
        BuscarPlayer();
        MirarYa();
        Parar();
        CambiarEstado(EstadoEnemigo.Salto);
        anim.Reproducir(ClipSaltar, true);
        while (!anim.Terminado) yield return null;
        Sonar("salto", 0.7f);
        float x0 = transform.position.x, y0 = transform.position.y;
        float dur = ajustes.duracionSalto / Mathf.Sqrt(Vel);
        // Cae a tu lado (el lado del que viene), no encima: la onda del golpe te alcanza igual.
        int ladoCaida = PosPlayer.x >= x0 ? -1 : 1;
        float xL = LimitarX(PosPlayer.x + ladoCaida * 0.8f);
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.linearVelocity = Vector2.zero;
        volando = true;
        AtravesarPlayer(true);
        anim.Reproducir(ClipAire, true);
        AjustesBlackKnight.Golpe g = ajustes.Buscar("Caida");
        bool avisado = false;
        float inicioAviso = Time.time;
        // El aviso empieza con tiempo de sobra para su minimo (contando el aterrizaje).
        float avisoCaida = g != null ? ritmo.AvisoObjetivo(g.peso, false, g.aviso) + 0.05f : 0.5f;
        float kAviso = Mathf.Clamp(1f - avisoCaida / dur, 0.25f, 0.5f);
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            float k = t / dur;
            // Sube siguiendote; al empezar el aviso fija el sitio y empieza a caer.
            if (k < kAviso) xL = LimitarX(PosPlayer.x + ladoCaida * 0.8f);
            else if (!avisado && g != null)
            {
                avisado = true;
                inicioAviso = Time.time;
                anim.Reproducir(ClipCaer, true);
                ritmo.Empezar();
                Resultados.Clear();
                ritmo.Avisar(g.peso, dur * (1f - kAviso) + 0.3f);
            }
            float x = Mathf.Lerp(x0, xL, Mathf.SmoothStep(0f, 1f, k));
            float yS = PeligrosJefe.SueloBajo(new Vector2(x, Mathf.Max(y0, PosPlayer.y) + 1.5f), y0);
            float y = Mathf.Lerp(y0, yS, k) + 4f * ajustes.alturaSalto * k * (1f - k);
            rb.MovePosition(new Vector2(x, y));
            if (x != transform.position.x) Mirar(x > transform.position.x ? 1 : -1);
            yield return null;
        }
        float yFinal = PeligrosJefe.SueloBajo(new Vector2(xL, Mathf.Max(y0, PosPlayer.y) + 1.5f), y0);
        rb.MovePosition(new Vector2(xL, yFinal));
        yield return new WaitForFixedUpdate();
        TerminarVuelo();
        MirarYa();
        CamaraDinamica.Sacudir(0.5f);
        if (g != null)
        {
            anim.Reproducir(ClipAterrizar, true, Vel);
            while (anim.Fotograma < g.golpeDesde && !anim.Terminado) yield return null;
            yield return GolpeActivo(g, Time.time - inicioAviso, true);
        }
        while (!anim.Terminado) yield return null;
        AtravesarPlayer(false);
        yield return Quieto(0.6f / Vel);
    }

    private void TerminarVuelo()
    {
        if (!volando) return;
        volando = false;
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.linearVelocity = Vector2.zero;
    }

    // ------------------------------------------------------------------ Desenvaine y provocacion

    // El desenvaine rapido: se acerca lo justo y cruza la distancia de un tajo
    // (castiga el frasco o la provocacion).
    private IEnumerator DesenvaineRapido()
    {
        AjustesBlackKnight.Golpe g = ajustes.Buscar("Desenvaine");
        if (g == null) yield break;
        yield return Acercarse(Mathf.Max(1.2f, g.avance + 0.6f));
        ritmo.Empezar();
        Resultados.Clear();
        yield return HacerGolpe(g, false, false);
        yield return Quieto(0.5f / Vel);
    }

    // Envaina y espera (te provoca). Si te acercas o le pegas, desenvaine; si no,
    // al acabar el tiempo va a por ti igual.
    public bool Provocando { get; private set; }
    private IEnumerator Provocar()
    {
        BuscarPlayer();
        MirarYa();
        Parar();
        anim.Reproducir(ClipEnvainar, true);
        while (!anim.Terminado) yield return null;
        anim.Reproducir(ClipQuietoSin, true);
        Provocando = true;
        int golpes = GolpesRecibidos;
        int pulsadas = pulsaciones.Count;
        for (float t = 0f; t < ajustes.duracionProvocar; t += Time.deltaTime)
        {
            MirarAlPlayer();
            Parar();
            if (Mathf.Abs(DxPlayer) < 2.2f || GolpesRecibidos > golpes || pulsaciones.Count > pulsadas) break;
            yield return null;
        }
        Provocando = false;
        yield return DesenvaineRapido();
    }
}
