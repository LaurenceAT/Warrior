using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

// La Sombra de los Humedales Helados (Boss_ShadowedWetlands): el jefe del nivel
// nevado. Pequeno y rapido, con una cola larga y una espada que deja tajos
// blancos enormes.
//
// FASE 1 (sombra):
//   - Barrido: tajo a ras de suelo con mucho alcance (se salta o se para).
//   - Barrido + tajo alzado: tras el barrido, con un retraso que cambia, un tajo
//     vertical que castiga a quien salto el primero. Dos ventanas de parry.
//   - Reves: si el player esta a su espalda, tajos hacia atras sin girarse.
//   - Paso sombrio: se hace esfera, cruza al otro lado del player y sale con un
//     tajo circular.
//   - Medialunas de sombra y caida desde lo alto (marca en el suelo).
//   - Agarre (tambien en la fase 2): pose de carga muy visible, se teletransporta
//     al player y, si lo atrapa, lo lanza al aire y lo corta en diagonal varias
//     veces: mata. Justo al aparecer hay un instante a camara lenta para
//     esquivarlo con el barrido (ultima oportunidad).
//
// TRANSICION: al vaciar la barra no muere: cae, la musica calla, la escarcha se
// junta en su cuerpo y se levanta imbuido en hielo (barra nueva, azul).
//
// FASE 2 (hielo): mas rapido y con pausas mas cortas. Sus tajos dejan escarcha
// en el suelo (ralentiza al player) y suma hechizos: lanzas de escarcha, estacas
// de hielo en cadena, ventisca que arrastra hacia el y medialunas heladas.
//
// Parry: cada golpe de un combo se puede parar. Parar todos los de un combo, o
// acumular varios parrys seguidos, le rompe la postura: queda aturdido un buen
// rato (ventana de contraataque).
public class JefeSombra : JefeBase, IModificadorDano, IAfinidadElemental
{
    [Header("Vida por fase")]
    [SerializeField] private int vidaFase1 = 1500;
    [SerializeField] private int vidaFase2 = 1700;

    [Header("Dano")]
    [SerializeField] private int danoTajo = 30;
    [SerializeField] private int danoFuerte = 38;
    [SerializeField] private int danoHechizo = 24;
    [SerializeField] private int danoLeve = 16;
    // Dano total del agarre, en fraccion de la vida maxima del player (0.8 = 80 %):
    // con la vida llena se sobrevive; con menos del 80 %, mata.
    [Range(0f, 1f)] [SerializeField] private float danoTotalAgarre = 0.8f;
    // Alcance del tajo alzado que atrapa (mas grande que el del combo normal).
    [SerializeField] private Vector2 alcanceAgarre = new Vector2(5f, 4.6f);

    [Header("Ritmo")]
    [SerializeField] private float velocidadAndar = 2.4f;
    [SerializeField] private float aceleracionFase2 = 1.3f;
    [SerializeField] private float aturdidoParry = 1.2f;
    [SerializeField] private float aturdidoPostura = 2.6f;
    [SerializeField] private int parrysParaRomper = 4;

    [Header("Afinidad")]
    [SerializeField] private float sagradoFase1 = 1.4f;
    [SerializeField] private float oscuroFase1 = 0.5f;
    [SerializeField] private float fuegoFase2 = 1.6f;
    [SerializeField] private float hieloFase2 = 0.2f;

    [Header("Efectos")]
    public AnimadorHoja.Clip fxMedialuna;
    public AnimadorHoja.Clip fxLanza;
    public AnimadorHoja.Clip fxLanzaImpacto;
    public AnimadorHoja.Clip fxEstaca;
    public AnimadorHoja.Clip fxMarca;
    public AnimadorHoja.Clip fxOnda;
    public AnimadorHoja.Clip fxPortal;
    public AnimadorHoja.Clip fxPolvo;
    public AnimadorHoja.Clip fxAnillo;
    public AnimadorHoja.Clip fxCorte;
    public AnimadorHoja.Clip fxCorteHielo;
    public Shader shaderBrillo;
    [SerializeField] private Color colorSombra = new Color(0.75f, 0.55f, 1f, 1f);
    [SerializeField] private Color colorHielo = new Color(0.55f, 0.9f, 1f, 1f);

    private Rect arena;
    private float suelo;
    private bool fase2, invulnerable, transformando;
    private float tAgarre = -99f, tLanzas = -99f, tVentisca = -99f, tEstacas = -99f;
    private string ultimoAtaque = "";
    private int parrysSeguidos, parrysCombo, golpesCombo;
    private bool parado;
    private CinemachineImpulseSource impulso;
    private SpriteRenderer sr, brillo;
    private Material materialBrillo;

    private Color ColorFase => fase2 ? colorHielo : colorSombra;
    private float Velocidad => fase2 ? aceleracionFase2 : 1f;

    public override void Configurar(Rect zona, float alturaSuelo)
    {
        arena = zona;
        suelo = alturaSuelo;
    }

    protected override void Awake()
    {
        base.Awake();
        colorAviso = new Color(0.85f, 0.4f, 1f, 1f);
        radioDeteccion = radioOlvido = 60f;
        impulso = GetComponent<CinemachineImpulseSource>();
        sr = visual != null ? visual.GetComponent<SpriteRenderer>() : null;
        salud.Inamovible = true;
        armadura = true;
        salud.PuedeMorir = () => fase2;
        salud.AlAgotarse += () => { if (!transformando) { StopAllCoroutines(); StartCoroutine(Resurreccion()); } };
    }

    protected override void Start()
    {
        salud.Revivir(vidaFase1);
        base.Start();
    }

    // ------------------------------------------------------------------ Dano

    public int Modificar(int dano, TipoArma arma)
    {
        if (invulnerable || transformando) return 0;
        return dano;
    }

    public float Multiplicador(Elemento e)
    {
        if (!fase2)
        {
            if (e == Elemento.Sagrado) return sagradoFase1;
            if (e == Elemento.Oscuro) return oscuroFase1;
            return 1f;
        }
        switch (e)
        {
            case Elemento.Fuego: return fuegoFase2;
            case Elemento.Hielo: return hieloFase2;
            case Elemento.Oscuro: return 0.8f;
            case Elemento.Acido: return 1.1f;
            default: return 1f;
        }
    }

    // ------------------------------------------------------------------ Cerebro

    protected override IEnumerator Cerebro()
    {
        yield return Entrada();
        while (true)
        {
            yield return Reposicionar();
            string ataque = Elegir();
            ultimoAtaque = ataque;
            parado = false;
            parrysCombo = golpesCombo = 0;
            yield return Ejecutar(ataque);

            // Todos los golpes del combo parados (o muchos parrys seguidos): postura rota.
            bool rota = parrysSeguidos >= parrysParaRomper || (golpesCombo >= 2 && parrysCombo == golpesCombo);
            if (rota) { parrysSeguidos = 0; yield return Aturdido(aturdidoPostura, true); }
            else if (parado) yield return Aturdido(aturdidoParry, false);
            else yield return Pausa(fase2 ? Random.Range(0.25f, 0.5f) : Random.Range(0.5f, 0.9f));
        }
    }

    private IEnumerator Ejecutar(string ataque)
    {
        switch (ataque)
        {
            case "barrido": return Barrido(false);
            case "barrido_alzado": return Barrido(true);
            case "triple": return BarridoTriple();
            case "reves": return Reves();
            case "paso": return PasoSombrio();
            case "medialunas": return Medialunas();
            case "caida": return Caida();
            case "agarre": return Agarre();
            case "lanzas": return Lanzas();
            case "estacas": return Estacas();
            case "ventisca": return Ventisca();
            default: return Pausa(0.3f);
        }
    }

    private string Elegir()
    {
        float dx = DxPlayer;
        float d = Mathf.Abs(dx);
        bool detras = Mathf.Sign(dx) != mirada && d < 3.5f;
        var op = new List<(string, float)>();

        if (detras) op.Add(("reves", 5f));
        if (d < 5f) { op.Add(("barrido", 2f)); op.Add(("barrido_alzado", 3f)); }
        if (d >= 3f) { op.Add(("paso", 2f)); op.Add(("medialunas", 2f)); op.Add(("caida", 1.5f)); }
        if (d < 3f) op.Add(("paso", 1f));

        float cadaAgarre = fase2 ? 13f : 19f;
        bool puedeAgarrar = (fase2 || salud.CurrentHealth < salud.MaxHealth * 0.85f) && Time.time - tAgarre > cadaAgarre;
        if (puedeAgarrar) op.Add(("agarre", 3.5f));

        if (fase2)
        {
            if (d < 5f) op.Add(("triple", 2.5f));
            if (Time.time - tLanzas > 9f) op.Add(("lanzas", 2.5f));
            if (Time.time - tEstacas > 7f) op.Add(("estacas", 2.5f));
            if (Time.time - tVentisca > 16f) op.Add(("ventisca", 2f));
        }

        op.RemoveAll(o => o.Item1 == ultimoAtaque);
        float total = 0f;
        foreach (var o in op) total += o.Item2;
        float r = Random.value * total;
        foreach (var o in op) { r -= o.Item2; if (r <= 0f) return o.Item1; }
        return op.Count > 0 ? op[0].Item1 : "barrido";
    }

    // ------------------------------------------------------------------ Entrada

    private IEnumerator Entrada()
    {
        invulnerable = true;
        MostrarSprite(false);
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.linearVelocity = Vector2.zero;
        BuscarPlayer();

        // La esfera de sombra baja del cielo al centro de la arena y se abre.
        float x = Mathf.Clamp(arena.center.x + (PosPlayer.x < arena.center.x ? 4f : -4f), arena.xMin + 3f, arena.xMax - 3f);
        transform.position = new Vector3(x, suelo, 0f);
        CamaraDinamica.Encuadrar(new Vector2(x, suelo + 2f), 0.6f, 3f);
        CamaraDinamica.Ampliar(6.2f, 3f);
        Sonar("jefe_aparicion");
        EfectoVisual portal = EfectoVisual.Crear(fxPortal, new Vector2(x, suelo + 1.4f), 2.4f, colorSombra);
        yield return Esperar(0.7f);

        MostrarSprite(true);
        MirarAlPlayer();
        anim.Reproducir("emerger", true);
        yield return Esperar(0.2f);
        Sacudir(1f);
        CamaraDinamica.Acercar(4.4f, 0.5f);
        EfectoVisual.Crear(fxPolvo, new Vector2(x, suelo + 0.3f), 2.4f, new Color(0.9f, 0.95f, 1f, 0.9f));
        yield return EsperarClip(1.2f);
        rb.bodyType = RigidbodyType2D.Dynamic;
        invulnerable = false;
        AvisarAterrizaje();
        anim.Reproducir("quieto");
        yield return Esperar(0.8f);
    }

    // ------------------------------------------------------------------ Tajos

    // Registra el resultado de un golpe cuerpo a cuerpo para la cuenta de parrys
    // del combo (el combo sigue tras un parry: cada golpe se para por separado).
    private void Registrar(PlayerControler.ResultadoDano? r)
    {
        if (!r.HasValue) { parrysSeguidos = 0; return; }
        if (r.Value == PlayerControler.ResultadoDano.Parry)
        {
            parrysCombo++;
            parrysSeguidos++;
            Sonar("jefe_parry", 0.8f);
            return;
        }
        parrysSeguidos = 0;
    }

    // Golpe cuerpo a cuerpo activo mientras el sprite muestra el tajo: de
    // "desde" a "hasta" (fotogramas del clip que suena ahora). Asi la caja de
    // dano coincide con lo que se ve y el parry se lee en la animacion.
    private IEnumerator Ventana(int desde, int hasta, Vector2 offset, Vector2 tamano, int dano)
    {
        while (anim.Fotograma < desde && !anim.Terminado) yield return null;
        golpesCombo++;
        PlayerControler.ResultadoDano? r = null;
        while (!r.HasValue && anim.Fotograma <= hasta && !anim.Terminado)
        {
            r = Golpear(offset, tamano, dano);
            if (!r.HasValue) yield return null;
        }
        // En el ultimo fotograma (el clip ya termino) tambien cuenta una vez.
        if (!r.HasValue && anim.Fotograma <= hasta) r = Golpear(offset, tamano, dano);
        Registrar(r);
    }

    // Arranca un clip de ataque a camara lenta durante el aviso, de forma que el
    // primer fotograma del golpe ("golpe") llegue justo al acabar el aviso.
    private IEnumerator Preparar(string clip, int golpe, float aviso, float intensidad)
    {
        float fps = 12f;
        anim.Reproducir(clip, true, golpe / fps / Mathf.Max(0.05f, aviso));
        LanzarAviso(aviso, intensidad);
        while (anim.Fotograma < golpe && !anim.Terminado) yield return null;
        anim.Reproducir(clip, false, Velocidad);
    }

    // Barrido a ras de suelo (Attack 1, fotogramas 2-3) y, si "alzado", el tajo
    // vertical (fotogramas 6-7) tras una pausa que cambia cada vez. Sin tajo de
    // color: el propio sprite ya dibuja el corte, y es a el al que se hace parry.
    private IEnumerator Barrido(bool alzado)
    {
        MirarAlPlayer();
        Sonar("jefe_carga_tajo", 0.6f);
        yield return Preparar("tajo1", 2, 0.5f / Velocidad, 0.6f);

        Sonar("jefe_tajo");
        rb.linearVelocity = new Vector2(mirada * 3f, rb.linearVelocity.y);
        if (fase2) ZonaEscarcha.Crear(new Vector2(transform.position.x + mirada * 2.6f, suelo), 4.5f, 5f);
        Sacudir(0.5f);
        yield return Ventana(2, 3, new Vector2(2.6f, 0.6f), new Vector2(5.4f, 1.3f), danoTajo);
        while (anim.Fotograma < 4 && !anim.Terminado) yield return null;
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

        if (!alzado)
        {
            yield return Esperar(0.45f / Velocidad);
            yield break;
        }

        // Retraso variable antes del tajo alzado: no se puede memorizar. El
        // sprite se queda quieto en la pose del barrido mientras tanto.
        anim.Reproducir("tajo1", false, 0f);
        yield return Esperar(Random.Range(0.15f, 0.55f) / Velocidad);
        MirarAlPlayer();
        LanzarAviso(0.25f, 0.7f);
        anim.Reproducir("tajo1", false, 2f / 12f / 0.25f);
        while (anim.Fotograma < 6 && !anim.Terminado) yield return null;
        anim.Reproducir("tajo1", false, Velocidad);
        Sonar("jefe_tajo_fuerte");
        CamaraDinamica.Acercar(4.5f, 0.3f);
        Sacudir(0.9f);
        yield return Ventana(6, 7, new Vector2(1.4f, 1.9f), new Vector2(3.2f, 3.8f), danoFuerte);
        yield return EsperarClip(1.5f);
        yield return Esperar(0.3f / Velocidad);
    }

    // Solo en la fase 2: barrido, barrido y tajo alzado, sin respiro.
    private IEnumerator BarridoTriple()
    {
        for (int i = 0; i < 2; i++)
        {
            MirarAlPlayer();
            yield return Preparar("tajo1", 2, 0.3f, 0.6f);
            Sonar("jefe_tajo");
            rb.linearVelocity = new Vector2(mirada * 4f, rb.linearVelocity.y);
            ZonaEscarcha.Crear(new Vector2(transform.position.x + mirada * 2.6f, suelo), 4.5f, 5f);
            yield return Ventana(2, 3, new Vector2(2.6f, 0.6f), new Vector2(5.4f, 1.3f), danoTajo);
            while (anim.Fotograma < 4 && !anim.Terminado) yield return null;
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        }
        yield return Barrido(true);
    }

    // Tajos hacia atras sin girarse (Attack 2): castiga al que se queda a su
    // espalda. Dos cortes (fotogramas 2-3 y 5) y la nieve que levanta (6-7).
    private IEnumerator Reves()
    {
        yield return Preparar("tajo2", 2, 0.4f / Velocidad, 0.7f);
        Sonar("jefe_tajo");
        yield return Ventana(2, 3, new Vector2(-1.6f, 1.2f), new Vector2(2.6f, 2.6f), danoTajo);
        while (anim.Fotograma < 5 && !anim.Terminado) yield return null;
        Sonar("jefe_tajo", 0.8f, 1.1f);
        yield return Ventana(5, 5, new Vector2(-1.6f, 1.2f), new Vector2(2.8f, 2.6f), danoTajo);
        while (anim.Fotograma < 6 && !anim.Terminado) yield return null;
        if (fase2) ZonaEscarcha.Crear(new Vector2(transform.position.x - mirada * 2.3f, suelo), 3f, 5f);
        EfectoVisual.Crear(fxPolvo, new Vector2(transform.position.x - mirada * 2f, suelo + 0.3f), 1.6f, new Color(0.95f, 0.97f, 1f, 0.8f), mirada > 0);
        yield return Ventana(6, 7, new Vector2(-2.7f, 0.3f), new Vector2(3.2f, 0.6f), danoLeve);
        yield return EsperarClip(1.5f);
    }

    // ------------------------------------------------------------------ Esfera de sombra

    private IEnumerator HacerseEsfera()
    {
        anim.Reproducir("carga", true, Velocidad);
        yield return EsperarClip(1f);
        anim.Reproducir("esfera", true, Velocidad);
        invulnerable = true;
        Sonar("teletransporte", 0.8f);
    }

    private IEnumerator Emerger(bool tajoCircular)
    {
        anim.Reproducir("emerger", true, Velocidad);
        while (anim.Fotograma < 1) yield return null;
        invulnerable = false;
        if (!tajoCircular) { yield return EsperarClip(1.2f); yield break; }
        LanzarAviso(0.2f, 0.7f);
        while (anim.Fotograma < 2 && !anim.Terminado) yield return null;
        Sonar("jefe_tajo_fuerte");
        TajoFx(new Vector2(0.4f, 1.3f), 0f, 2.2f, true, true);
        Sacudir(0.8f);
        yield return Ventana(2, 3, new Vector2(0.4f, 1.3f), new Vector2(4.6f, 2.8f), danoFuerte);
        if (fase2) ZonaEscarcha.Crear(new Vector2(transform.position.x + mirada, suelo), 4f, 5f);
        yield return Ventana(5, 5, new Vector2(1f, 0.3f), new Vector2(4f, 0.6f), danoLeve);
        yield return EsperarClip(1f);
    }

    private IEnumerator PasoSombrio()
    {
        MirarAlPlayer();
        LanzarAviso(0.35f, 0.6f);
        yield return HacerseEsfera();
        // Cruza al otro lado del player, rapido y a ras de suelo.
        float destino = Mathf.Clamp(PosPlayer.x + Mathf.Sign(DxPlayer) * 2.2f, arena.xMin + 1.5f, arena.xMax - 1.5f);
        float x0 = transform.position.x;
        for (float t = 0f; t < 0.3f; t += Time.deltaTime)
        {
            transform.position = new Vector3(Mathf.Lerp(x0, destino, t / 0.3f), suelo, 0f);
            Estela();
            yield return null;
        }
        transform.position = new Vector3(destino, suelo, 0f);
        MirarAlPlayer();
        yield return Emerger(true);
    }

    private IEnumerator Caida()
    {
        yield return HacerseEsfera();
        MostrarSprite(false);
        Vector2 marca = new Vector2(Mathf.Clamp(PosPlayer.x, arena.xMin + 2f, arena.xMax - 2f), suelo);
        EfectoVisual m = EfectoVisual.Crear(fxMarca, marca + Vector2.up * 0.05f, 1f, ColorFase, false, -1f, "VFX", 3);
        if (m != null) { m.transform.localScale = new Vector3(2.4f, 0.8f, 1f); PeligrosJefe.Registrar(m.gameObject); }
        // La marca sigue un poco al player y luego se fija.
        for (float t = 0f; t < 0.9f / Velocidad; t += Time.deltaTime)
        {
            if (t < 0.5f) marca.x = Mathf.Lerp(marca.x, Mathf.Clamp(PosPlayer.x, arena.xMin + 2f, arena.xMax - 2f), Time.deltaTime * 4f);
            if (m != null) m.transform.position = marca + Vector2.up * 0.05f;
            yield return null;
        }
        if (m != null) Destroy(m.gameObject);
        transform.position = new Vector3(marca.x, suelo + 6f, 0f);
        MostrarSprite(true);
        for (float t = 0f; t < 0.18f; t += Time.deltaTime)
        {
            transform.position = new Vector3(marca.x, Mathf.Lerp(suelo + 6f, suelo, t / 0.18f), 0f);
            yield return null;
        }
        transform.position = new Vector3(marca.x, suelo, 0f);
        MirarAlPlayer();
        Sonar("jefe_impacto_suelo");
        Sacudir(1.3f);
        CamaraDinamica.Acercar(4.6f, 0.35f);
        golpesCombo++;
        Registrar(Golpear(new Vector2(0f, 0.8f), new Vector2(3.2f, 1.8f), danoFuerte));
        EfectoVisual.Crear(fxPolvo, new Vector2(marca.x, suelo + 0.3f), 2.2f, new Color(0.95f, 0.97f, 1f, 0.9f));
        // Ondas a los dos lados, a ras de suelo: se saltan.
        OndaCarmesi.Lanzar(fxOnda, new Vector2(marca.x + 1.2f, suelo), 1, 9f, danoLeve, 10f, ColorFase, 1f);
        OndaCarmesi.Lanzar(fxOnda, new Vector2(marca.x - 1.2f, suelo), -1, 9f, danoLeve, 10f, ColorFase, 1f);
        if (fase2) EscarchaEnOndas();
        yield return Emerger(false);
    }

    // Las ondas recien lanzadas dejan escarcha por donde pasan.
    private void EscarchaEnOndas()
    {
        foreach (OndaCarmesi o in FindObjectsByType<OndaCarmesi>(FindObjectsSortMode.None))
            if (o.GetComponent<RastroEscarcha>() == null) o.gameObject.AddComponent<RastroEscarcha>();
    }

    private IEnumerator Medialunas()
    {
        MirarAlPlayer();
        int n = fase2 ? 3 : 2;
        for (int i = 0; i < n; i++)
        {
            anim.Reproducir("tajo1", true, 0.5f);
            LanzarAviso(0.35f / Velocidad, 0.6f);
            yield return Esperar(0.35f / Velocidad);
            anim.Reproducir("tajo1", true, Velocidad);
            while (anim.Fotograma < 6) yield return null;
            MirarAlPlayer();
            // Alterna alta y baja: una se salta, la otra se esquiva agachado con el barrido.
            bool baja = i % 2 == 0;
            Vector2 pos = (Vector2)transform.position + new Vector2(mirada * 1.5f, baja ? 0.6f : 1.6f);
            Sonar("jefe_medialuna");
            MedialunaSangre.Lanzar(fxMedialuna, pos, mirada, fase2 ? 11f : 9f, danoHechizo, transform, ColorFase);
            if (fase2)
                foreach (MedialunaSangre m in FindObjectsByType<MedialunaSangre>(FindObjectsSortMode.None))
                    if (baja && m.GetComponent<RastroEscarcha>() == null) m.gameObject.AddComponent<RastroEscarcha>();
            yield return EsperarClip(1f);
        }
        yield return Esperar(0.3f);
    }

    // ------------------------------------------------------------------ Agarre

    private IEnumerator Agarre()
    {
        tAgarre = Time.time;
        MirarAlPlayer();
        rb.linearVelocity = Vector2.zero;

        // 1. Pose de carga: larga, con un aviso rojo que no se puede confundir.
        float carga = fase2 ? 1.05f : 1.35f;
        anim.Reproducir("carga", true, 0.5f);
        Color previo = colorAviso;
        colorAviso = new Color(1f, 0.1f, 0.15f, 1f);
        LanzarAviso(carga, 0.9f);
        colorAviso = previo;
        Sonar("jefe_agarre_carga");
        CamaraDinamica.Acercar(4.5f, carga);
        CamaraDinamica.Encuadrar((Vector2)transform.position + Vector2.up * 1.5f, 0.35f, carga);
        SpriteRenderer peligro = SimboloPeligro();
        EfectoVisual marca = EfectoVisual.Crear(fxMarca, PosPlayer, 1f, new Color(1f, 0.2f, 0.25f, 0.8f), false, -1f, "VFX", 3);
        if (marca != null) PeligrosJefe.Registrar(marca.gameObject);

        Vector2 objetivo = PosPlayer;
        for (float t = 0f; t < carga; t += Time.deltaTime)
        {
            // La marca persigue al player y se fija al final (se puede leer y huir).
            if (t < carga - 0.3f) objetivo = new Vector2(PosPlayer.x, PeligrosJefe.SueloBajo(PosPlayer + Vector2.up * 0.2f, suelo));
            if (marca != null)
            {
                marca.transform.position = objetivo + Vector2.up * 0.05f;
                marca.transform.localScale = new Vector3(1.6f, 0.55f, 1f);
            }
            if (peligro != null) peligro.transform.localScale = Vector3.one * (0.9f + 0.2f * Mathf.Sin(t * 20f));
            yield return null;
        }
        if (peligro != null) Destroy(peligro.gameObject);

        // 2. Se desvanece en esfera y aparece donde marco.
        invulnerable = true;
        anim.Reproducir("esfera", true);
        Sonar("teletransporte");
        yield return Desvanecerse(0.12f, false);
        yield return Esperar(0.15f);
        if (marca != null) Destroy(marca.gameObject);
        float x = Mathf.Clamp(objetivo.x, arena.xMin + 1f, arena.xMax - 1f);
        transform.position = new Vector3(x, objetivo.y, 0f);
        MirarAlPlayer();
        yield return Desvanecerse(0.08f, true);

        // 3. El que alza es su segundo golpe basico, el tajo alzado (Attack 1,
        //    fotogramas 6-7), con mas alcance que en el combo. Antes, la ultima
        //    oportunidad: el barrido de preparacion va a camara lenta un instante.
        anim.Reproducir("tajo1", true, 6f / 12f / 0.3f);
        LanzarAviso(0.3f, 1f);
        CamaraDinamica.CamaraLenta(0.35f, 0.3f);
        Sonar("jefe_carga_tajo", 0.7f);
        while (anim.Fotograma < 6 && !anim.Terminado) yield return null;
        invulnerable = false;
        anim.Reproducir("tajo1", false, Velocidad);
        Sonar("jefe_tajo_fuerte");
        TajoFx(new Vector2(1.6f, 2f), 70f, 2.4f, true);
        Sacudir(0.9f);

        bool atrapado = false;
        PlayerControler p = player != null ? player.GetComponent<PlayerControler>() : null;
        if (p != null)
        {
            Vector2 centro = (Vector2)transform.position + new Vector2(mirada * alcanceAgarre.x * 0.35f, alcanceAgarre.y * 0.45f);
            // Mientras duran los fotogramas del tajo (6-7), cualquiera de ellos atrapa.
            while (!atrapado && anim.Fotograma <= 7 && !anim.Terminado)
            {
                foreach (Collider2D c in Physics2D.OverlapBoxAll(centro, alcanceAgarre, 0f))
                    if (c.CompareTag("Player") && p.IntentarAgarre()) { atrapado = true; break; }
                if (!atrapado) yield return null;
            }
        }

        if (!atrapado)
        {
            // Fallo: queda expuesto un buen rato.
            invulnerable = false;
            TextoFlotante.Mostrar("¡Esquivado!", (Vector2)transform.position + Vector2.up * 3f, new Color(0.8f, 1f, 0.8f), 1f);
            yield return Aturdido(1.4f, false);
            yield break;
        }

        yield return Ejecucion(p);
    }

    // El player atrapado: lanzado al aire y cortado en diagonal.
    private IEnumerator Ejecucion(PlayerControler p)
    {
        Sonar("jefe_agarre");
        Sacudir(1.2f);
        CamaraDinamica.Acercar(3.6f, 3.2f);
        CamaraDinamica.Encuadrar(p.transform.position + Vector3.up * 2.5f, 0.5f, 3.2f);
        ScreenFlash.Destello(new Color(1f, 1f, 1f, 0.35f), 0.15f);
        MostrarSprite(false);

        // Lo lanza hacia arriba.
        Vector2 inicio = p.transform.position;
        Vector2 alto = inicio + Vector2.up * 3.8f;
        for (float t = 0f; t < 0.4f; t += Time.deltaTime)
        {
            if (p == null) yield break;
            float u = 1f - (1f - t / 0.4f) * (1f - t / 0.4f);
            p.MoverAgarrado(Vector2.Lerp(inicio, alto, u));
            yield return null;
        }

        // Cortes en diagonal desde lados distintos y el ultimo desde arriba. Entre
        // todos quitan danoTotalAgarre de la vida maxima (80 %): con la vida
        // llena se sobrevive; con menos, mata.
        Vector2[] desde = { new Vector2(-1.6f, 1.6f), new Vector2(1.6f, 1.6f), new Vector2(-1.6f, -1.2f), new Vector2(1.6f, -1.2f) };
        int total = Mathf.RoundToInt(p.VidaMaxima * danoTotalAgarre);
        int porCorte = Mathf.Max(1, Mathf.RoundToInt(total * 0.15f));
        int hecho = 0;
        for (int i = 0; i < desde.Length; i++)
        {
            if (p == null || !p.Agarrado) yield break;
            Vector2 centro = alto + new Vector2(0f, Mathf.Sin(i) * 0.1f);
            p.MoverAgarrado(centro);
            Corte(centro, desde[i]);
            p.CorteAgarre(porCorte);
            hecho += porCorte;
            yield return new WaitForSecondsRealtime(0.05f);
            yield return Esperar(0.22f);
        }

        // El ultimo, desde arriba: lo que falte hasta el 80 %.
        if (p == null || !p.Agarrado) yield break;
        transform.position = new Vector3(alto.x, suelo, 0f);
        MostrarSprite(true);
        anim.Reproducir("tajo1", true, 1.4f);
        CamaraDinamica.CamaraLenta(0.25f, 0.25f);
        Corte(alto, new Vector2(0.3f, 2.2f));
        Sacudir(2f);
        ScreenFlash.Destello(new Color(1f, 0.2f, 0.25f, 0.45f), 0.3f);
        p.CorteAgarre(Mathf.Max(1, total - hecho));
        // Si sobrevive, cae al suelo derribado.
        if (p != null && p.Agarrado) p.SoltarAgarre(true);
        yield return Esperar(1f);
    }

    private void Corte(Vector2 centro, Vector2 desde)
    {
        Sonar("jefe_corte_agarre", 1f, Random.Range(0.95f, 1.1f));
        float ang = Mathf.Atan2(-desde.y, -desde.x) * Mathf.Rad2Deg;
        AnimadorHoja.Clip c = fase2 ? fxCorteHielo : fxCorte;
        if (c != null) EfectoVisual.Crear(c, centro, 2.6f, Color.white, false, -1f, "VFX", 30, ang);
        ParticulasFx.Rafaga(centro, 16, ColorFase, Color.white, new Vector2(2f, 5f), 0.5f, new Vector2(0.05f, 0.11f), new Vector2(0.2f, 0.5f));
        CamaraDinamica.Sacudir(0.7f);
        // La silueta del jefe aparece un instante en el lado del corte.
        Estela((Vector2)centro + desde - new Vector2(0f, 1.2f));
    }

    private SpriteRenderer SimboloPeligro()
    {
        Sprite s = RecursosRPG.Get().Icono("peligro");
        if (s == null) return null;
        SpriteRenderer p = new GameObject("Peligro").AddComponent<SpriteRenderer>();
        p.sprite = s;
        p.sortingLayerName = "VFX";
        p.sortingOrder = 40;
        p.sharedMaterial = EfectoVisual.MaterialSinLuz();
        p.transform.SetParent(transform, false);
        p.transform.localPosition = new Vector3(0f, 3.4f, 0f);
        PeligrosJefe.Registrar(p.gameObject);
        return p;
    }

    // ------------------------------------------------------------------ Hechizos (fase 2)

    private IEnumerator Lanzas()
    {
        tLanzas = Time.time;
        anim.Reproducir("carga", true, 0.6f);
        LanzarAviso(0.6f, 0.6f);
        Sonar("hielo_conjuro");
        CamaraDinamica.Ampliar(6.4f, 3f);
        yield return Esperar(0.5f);
        // Marcas alrededor del player; las lanzas caen en diagonal sobre ellas.
        var puntos = new List<float>();
        float x0 = PosPlayer.x;
        for (int i = 0; i < 6; i++) puntos.Add(Mathf.Clamp(x0 + (i - 2.5f) * 1.7f + Random.Range(-0.3f, 0.3f), arena.xMin + 0.8f, arena.xMax - 0.8f));
        puntos.Add(x0);
        foreach (float x in puntos)
        {
            MarcaHielo(new Vector2(x, suelo), 1.2f, 1.1f);
        }
        yield return Esperar(0.8f);
        int dir = Random.value < 0.5f ? 1 : -1;
        foreach (float x in puntos)
        {
            Vector2 ini = new Vector2(x - dir * 3f, suelo + 7f);
            Vector2 vel = (new Vector2(x, suelo) - ini).normalized * 16f;
            ProyectilNieve.Lanzar(new ProyectilNieve.Datos
            {
                clip = fxLanza, impacto = fxLanzaImpacto, color = Color.white, escala = 0.9f, escalaImpacto = 0.8f,
                dano = danoHechizo, magico = true, radio = 0.25f, escarcha = 1.6f, sonidoImpacto = "hielo_impacto",
                registrarPeligro = true,
            }, ini, vel, transform);
            yield return Esperar(0.09f);
        }
        yield return Esperar(0.6f);
    }

    private IEnumerator Estacas()
    {
        tEstacas = Time.time;
        MirarAlPlayer();
        anim.Reproducir("tajo2", true, 0.5f);
        LanzarAviso(0.45f, 0.6f);
        Sonar("hielo_conjuro", 0.8f, 1.2f);
        yield return Esperar(0.45f);
        // Una fila que avanza desde el jefe hacia el player (y le pasa).
        float x = transform.position.x + mirada * 1.6f;
        for (int i = 0; i < 12; i++)
        {
            if (x < arena.xMin + 0.5f || x > arena.xMax - 0.5f) break;
            EstacaHielo.Invocar(null, fxEstaca, new Vector2(x, suelo), 0.45f, danoHechizo, this, null);
            x += mirada * 1.3f;
            yield return Esperar(0.09f);
        }
        // Y una segunda fila en sentido contrario, con retraso, si esta muy cerca.
        if (Mathf.Abs(DxPlayer) < 4f)
        {
            yield return Esperar(0.3f);
            float y = transform.position.x - mirada * 1.6f;
            for (int i = 0; i < 6; i++)
            {
                if (y < arena.xMin + 0.5f || y > arena.xMax - 0.5f) break;
                EstacaHielo.Invocar(null, fxEstaca, new Vector2(y, suelo), 0.45f, danoHechizo, this, null);
                y -= mirada * 1.3f;
                yield return Esperar(0.09f);
            }
        }
        yield return Esperar(0.7f);
    }

    // Ventisca: se hace esfera en el centro, tira del player hacia el y le
    // dispara esquirlas; al final estalla (se ve crecer el aro).
    private IEnumerator Ventisca()
    {
        tVentisca = Time.time;
        yield return HacerseEsfera();
        float cx = Mathf.Clamp(transform.position.x, arena.xMin + 5f, arena.xMax - 5f);
        transform.position = new Vector3(cx, suelo, 0f);
        CamaraDinamica.Ampliar(7f, 5f);
        Sonar("ventisca_rafaga");
        AudioSource viento = Sonido.Bucle("ventisca", 0.8f);
        const float dur = 3.2f;
        float siguienteEsquirla = 0f;
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            PlayerControler p = player != null ? player.GetComponent<PlayerControler>() : null;
            if (p != null)
            {
                float lado = Mathf.Sign(transform.position.x - p.transform.position.x);
                p.AplicarViento(new Vector2(lado * 3.4f, 0f));
            }
            if (t >= siguienteEsquirla && player != null)
            {
                siguienteEsquirla = t + 0.45f;
                Vector2 boca = (Vector2)transform.position + Vector2.up * 1f;
                Vector2 dir = (PosPlayer - boca).normalized;
                ProyectilNieve.Lanzar(new ProyectilNieve.Datos
                {
                    clip = fxLanza, color = new Color(0.8f, 0.95f, 1f), escala = 0.5f, dano = danoLeve, magico = true,
                    radio = 0.18f, sonidoImpacto = "hielo_impacto", registrarPeligro = true,
                }, boca, dir * 9f, transform);
            }
            yield return null;
        }
        if (viento != null) Destroy(viento.gameObject);

        // El estallido final, con aro de aviso.
        Vector2 centro = (Vector2)transform.position + Vector2.up * 1f;
        yield return AroDeAviso(centro, 3.6f, 0.8f);
        StartCoroutine(Estallido(centro, 3.6f));
        Sonar("jefe_nova");
        Sacudir(1.4f);
        ScreenFlash.Destello(new Color(0.7f, 0.9f, 1f, 0.3f), 0.2f);
        GolpeCirculo(centro, 3.6f, danoFuerte);
        ZonaEscarcha.Crear(new Vector2(centro.x, suelo), 7f, 6f);
        yield return Emerger(false);
    }

    // Aro de hielo que se abre de golpe, con esquirlas.
    private IEnumerator Estallido(Vector2 centro, float radio)
    {
        ParticulasFx.Rafaga(centro, 40, Color.white, colorHielo, new Vector2(3f, 8f), 0.6f, new Vector2(0.06f, 0.14f), new Vector2(0.4f, 0.9f));
        SpriteRenderer aro = new GameObject("Estallido").AddComponent<SpriteRenderer>();
        aro.sprite = RuedaImbuir.Anillo();
        aro.sortingLayerName = "VFX";
        aro.sortingOrder = 15;
        aro.sharedMaterial = EfectoVisual.MaterialSinLuz();
        aro.transform.position = centro;
        PeligrosJefe.Registrar(aro.gameObject);
        for (float t = 0f; t < 0.4f; t += Time.deltaTime)
        {
            float d = radio * 2f / 1.28f * Mathf.Lerp(0.3f, 1.05f, 1f - (1f - t / 0.4f) * (1f - t / 0.4f));
            aro.transform.localScale = new Vector3(d, d, 1f);
            aro.color = new Color(0.8f, 0.95f, 1f, 1f - t / 0.4f);
            yield return null;
        }
        Destroy(aro.gameObject);
    }

    // Marca ovalada y palida en el suelo (lanzas y estacas de hielo).
    private void MarcaHielo(Vector2 suelo, float ancho, float vida)
    {
        SpriteRenderer m = new GameObject("MarcaHielo").AddComponent<SpriteRenderer>();
        m.sprite = EnemigoMago.CirculoSprite();
        m.sortingLayerName = "VFX";
        m.sortingOrder = 3;
        m.sharedMaterial = EfectoVisual.MaterialSinLuz();
        m.transform.position = suelo + Vector2.up * 0.05f;
        m.transform.localScale = new Vector3(ancho, ancho * 0.25f, 1f);
        m.color = new Color(0.7f, 0.93f, 1f, 0.7f);
        PeligrosJefe.Registrar(m.gameObject);
        Destroy(m.gameObject, vida);
    }

    private IEnumerator AroDeAviso(Vector2 centro, float radio, float segundos)
    {
        SpriteRenderer aro = new GameObject("AroAviso").AddComponent<SpriteRenderer>();
        aro.sprite = EnemigoMago.CirculoSprite();
        aro.sortingLayerName = "VFX";
        aro.sortingOrder = 6;
        aro.sharedMaterial = EfectoVisual.MaterialSinLuz();
        aro.transform.position = centro;
        PeligrosJefe.Registrar(aro.gameObject);
        for (float t = 0f; t < segundos; t += Time.deltaTime)
        {
            float u = t / segundos;
            float d = radio * 2f * Mathf.Lerp(0.25f, 1f, u);
            aro.transform.localScale = new Vector3(d, d, 1f);
            Color c = ColorFase;
            c.a = 0.25f + 0.45f * (0.5f + 0.5f * Mathf.Sin(t * Mathf.Lerp(15f, 45f, u)));
            aro.color = c;
            yield return null;
        }
        Destroy(aro.gameObject);
    }

    private void GolpeCirculo(Vector2 centro, float radio, int dano)
    {
        foreach (Collider2D c in Physics2D.OverlapCircleAll(centro, radio))
        {
            if (!c.CompareTag("Player")) continue;
            PlayerControler p = c.GetComponent<PlayerControler>();
            if (p == null) continue;
            PlayerControler.SiguienteGolpeMagico = true;
            if (p.TakeDamage(dano, this) == PlayerControler.ResultadoDano.Parry) parado = true;
            return;
        }
    }

    // ------------------------------------------------------------------ Resurreccion

    private IEnumerator Resurreccion()
    {
        transformando = true;
        invulnerable = true;
        QuitarAviso();
        PeligrosJefe.LimpiarTodo();
        rb.linearVelocity = Vector2.zero;
        AvisarSilencio();
        Sonar("jefe_caida");

        // Cae como si muriera: la camara se acerca y lo encuadra.
        CamaraDinamica.Encuadrar((Vector2)transform.position + Vector2.up * 1f, 0.8f, 6.5f);
        CamaraDinamica.Acercar(3.9f, 3.4f);
        CamaraDinamica.CamaraLenta(0.3f, 0.5f);
        anim.Reproducir("caer", true);
        yield return EsperarClip(3f);
        yield return Esperar(0.6f);

        // La escarcha se junta en su cuerpo.
        Sonar("jefe_revivir_carga");
        Vector2 cuerpo = (Vector2)transform.position + Vector2.up * 0.4f;
        for (float t = 0f; t < 2f; t += Time.deltaTime)
        {
            if (Random.value < 0.7f)
            {
                Vector2 desde = cuerpo + Random.insideUnitCircle.normalized * Random.Range(2.5f, 4.5f);
                ParticulasHacia(desde, cuerpo);
            }
            if (sr != null) sr.color = Color.Lerp(Color.white, new Color(0.7f, 0.9f, 1f), t / 2f);
            yield return null;
        }

        // Se levanta: la camara se abre para verle entero, destello y estallido.
        fase2 = true;
        PonerBrilloHielo(true);
        anim.Reproducir("levantarse", true);
        CamaraDinamica.Ampliar(7.2f, 3.2f);
        CamaraDinamica.Encuadrar((Vector2)transform.position + Vector2.up * 2f, 0.6f, 3f);
        Sonar("jefe_revivir");
        yield return EsperarClip(2.5f);
        ScreenFlash.Destello(new Color(0.75f, 0.95f, 1f, 0.55f), 0.5f);
        Sacudir(2.2f);
        CamaraDinamica.CamaraLenta(0.3f, 0.5f);
        salud.Revivir(vidaFase2);
        AvisarCambioFase();
        MensajePantalla.Titulo("SEGUNDA FORMA", "Imbuido en escarcha");

        // Onda de la resurreccion: poco dano, aro visible antes.
        Vector2 centro = (Vector2)transform.position + Vector2.up * 1f;
        StartCoroutine(Estallido(centro, 3.2f));
        ZonaEscarcha.Crear(new Vector2(transform.position.x, suelo), 6f, 6f);
        yield return AroDeAviso(centro, 3.2f, 0.7f);
        GolpeCirculo(centro, 3.2f, danoLeve);
        yield return Esperar(0.6f);
        transformando = false;
        invulnerable = false;
        parrysSeguidos = 0;
        StartCoroutine(BucleFase2());
    }

    // Tras revivir, el cerebro vuelve sin repetir la entrada.
    private IEnumerator BucleFase2()
    {
        while (true)
        {
            yield return Reposicionar();
            string ataque = Elegir();
            ultimoAtaque = ataque;
            parado = false;
            parrysCombo = golpesCombo = 0;
            yield return Ejecutar(ataque);
            bool rota = parrysSeguidos >= parrysParaRomper || (golpesCombo >= 2 && parrysCombo == golpesCombo);
            if (rota) { parrysSeguidos = 0; yield return Aturdido(aturdidoPostura, true); }
            else if (parado) yield return Aturdido(aturdidoParry, false);
            else yield return Pausa(Random.Range(0.25f, 0.5f));
        }
    }

    private void ParticulasHacia(Vector2 desde, Vector2 hacia)
    {
        Vector2 dir = hacia - desde;
        float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        ParticulasFx.Rafaga(desde, 2, new Color(0.8f, 0.95f, 1f), colorHielo, new Vector2(dir.magnitude * 1.4f, dir.magnitude * 1.8f), 0f,
                            new Vector2(0.05f, 0.1f), new Vector2(0.5f, 0.6f), 8f, ang);
    }

    // Brillo de hielo sobre lo blanco del sprite (la espada, los tajos, la cara):
    // copia del sprite con el shader de la hoja, tecleado a los tonos claros.
    private void PonerBrilloHielo(bool poner)
    {
        if (!poner) { if (brillo != null) brillo.enabled = false; return; }
        if (brillo == null && shaderBrillo != null && sr != null)
        {
            materialBrillo = new Material(shaderBrillo);
            materialBrillo.SetColor("_Key1", Color.white);
            materialBrillo.SetColor("_Key2", new Color(0.9f, 0.9f, 0.9f));
            materialBrillo.SetColor("_Key3", new Color(0.8f, 0.82f, 0.85f));
            materialBrillo.SetFloat("_Tolerance", 0.2f);
            GameObject go = new GameObject("BrilloHielo");
            go.transform.SetParent(visual, false);
            brillo = go.AddComponent<SpriteRenderer>();
            brillo.sharedMaterial = materialBrillo;
        }
        if (brillo != null) brillo.enabled = true;
    }

    private void LateUpdate()
    {
        if (brillo == null || !brillo.enabled || sr == null) return;
        brillo.sprite = sr.sprite;
        brillo.flipX = sr.flipX;
        brillo.sortingLayerID = sr.sortingLayerID;
        brillo.sortingOrder = sr.sortingOrder + 1;
        brillo.enabled = sr.color.a > 0.05f;
        materialBrillo.SetColor("_GlowColor", colorHielo);
        materialBrillo.SetFloat("_Amount", 0.55f + 0.25f * Mathf.Sin(Time.time * 3f));
        // Escarcha que cae del cuerpo de vez en cuando.
        if (Random.value < Time.deltaTime * 5f)
            ParticulasFx.Rafaga((Vector2)transform.position + new Vector2(Random.Range(-0.5f, 0.5f), Random.Range(0.5f, 2.2f)), 1,
                                Color.white, colorHielo, new Vector2(0.2f, 0.5f), 0.3f, new Vector2(0.04f, 0.07f), new Vector2(0.5f, 0.9f));
    }

    // ------------------------------------------------------------------ Estados

    private IEnumerator Reposicionar()
    {
        MirarAlPlayer();
        float d = Mathf.Abs(DxPlayer);
        if (d < 4f || Random.value < 0.35f) yield break;
        anim.Reproducir("andar", false, Velocidad);
        for (float t = 0f; t < 0.9f && Mathf.Abs(DxPlayer) > 3.2f; t += Time.deltaTime)
        {
            MirarAlPlayer();
            rb.linearVelocity = new Vector2(mirada * velocidadAndar * Velocidad, rb.linearVelocity.y);
            yield return null;
        }
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
    }

    private IEnumerator Pausa(float segundos)
    {
        anim.Reproducir("quieto");
        for (float t = 0f; t < segundos; t += Time.deltaTime)
        {
            MirarAlPlayer();
            yield return null;
        }
    }

    private IEnumerator Aturdido(float segundos, bool postura)
    {
        rb.linearVelocity = Vector2.zero;
        anim.Reproducir("golpe", true);
        string texto = postura ? "¡Postura rota!" : "Aturdido";
        TextoFlotante.Mostrar(texto, (Vector2)transform.position + Vector2.up * 3.2f, new Color(1f, 0.95f, 0.7f), postura ? 1.3f : 1f);
        if (postura)
        {
            Sonar("jefe_postura");
            ScreenFlash.Destello(new Color(1f, 1f, 1f, 0.25f), 0.2f);
            CamaraDinamica.Acercar(4.4f, 0.6f);
        }
        Color previo = colorAviso;
        colorAviso = Color.white;
        LanzarAviso(0.3f, 0.8f);
        colorAviso = previo;
        yield return Temblar(segundos, postura ? 0.07f : 0.04f);
        anim.Reproducir("quieto");
    }

    protected override void AlMorir()
    {
        base.AlMorir();
        PeligrosJefe.LimpiarTodo();
        PonerBrilloHielo(false);
        StartCoroutine(Muerte());
        AvisarDerrota();
    }

    private IEnumerator Muerte()
    {
        anim.Reproducir("caer", true);
        Sonar("jefe_muerte");
        ScreenFlash.Destello(new Color(0.8f, 0.95f, 1f, 0.5f), 0.6f);
        CamaraDinamica.Encuadrar((Vector2)transform.position + Vector2.up, 0.8f, 3f);
        CamaraDinamica.Acercar(4f, 2.5f);
        CamaraDinamica.CamaraLenta(0.3f, 0.8f);
        float nube = 0f;
        for (float t = 0f; t < 3f; t += Time.deltaTime)
        {
            if (t > 1.8f && sr != null) { Color c = sr.color; c.a = 1f - (t - 1.8f) / 1.2f; sr.color = c; }
            if ((nube -= Time.deltaTime) <= 0f)
            {
                nube = 0.12f;
                Vector2 p = (Vector2)transform.position + new Vector2(Random.Range(-1.2f, 1.2f), Random.Range(0.2f, 2f));
                ParticulasFx.Rafaga(p, 8, colorHielo, new Color(0.2f, 0.1f, 0.3f), new Vector2(1f, 3f), -0.3f,
                                    new Vector2(0.06f, 0.15f), new Vector2(0.5f, 1.1f));
            }
            yield return null;
        }
    }

    // ------------------------------------------------------------------ Ayudas

    private IEnumerator Esperar(float segundos)
    {
        for (float t = 0f; t < segundos; t += Time.deltaTime) yield return null;
    }

    private IEnumerator Temblar(float segundos, float fuerza)
    {
        Vector3 base0 = visual.localPosition;
        for (float t = 0f; t < segundos; t += Time.deltaTime)
        {
            visual.localPosition = base0 + new Vector3(Random.Range(-fuerza, fuerza), 0f, 0f);
            yield return null;
        }
        visual.localPosition = base0;
    }

    private IEnumerator Desvanecerse(float segundos, bool aparecer)
    {
        if (sr == null) yield break;
        for (float t = 0f; t < segundos; t += Time.deltaTime)
        {
            Color c = sr.color;
            c.a = aparecer ? t / segundos : 1f - t / segundos;
            sr.color = c;
            yield return null;
        }
        Color f = sr.color;
        f.a = aparecer ? 1f : 0f;
        sr.color = f;
    }

    private void MostrarSprite(bool mostrar)
    {
        if (sr == null) return;
        Color c = sr.color;
        c.a = mostrar ? 1f : 0f;
        sr.color = c;
    }

    private void Sacudir(float fuerza)
    {
        if (impulso != null) impulso.GenerateImpulse(fuerza);
        else CamaraDinamica.Sacudir(fuerza);
    }

    // Tajo de color encima del blanco del sprite: violeta en la fase 1, cian en la 2.
    private void TajoFx(Vector2 offset, float angulo, float escala, bool grande, bool circular = false)
    {
        AnimadorHoja.Clip c = fase2 ? fxCorteHielo : fxCorte;
        if (c == null) return;
        Vector2 pos = (Vector2)transform.position + new Vector2(offset.x * mirada, offset.y);
        float ang = mirada > 0 ? angulo : 180f - angulo;
        EfectoVisual e = EfectoVisual.Crear(c, pos, escala * (grande ? 1.2f : 1f), new Color(1f, 1f, 1f, 0.85f), mirada < 0 && !circular, -1f, "VFX", 14, circular ? 0f : ang * 0.25f);
        if (circular && e != null)
        {
            EfectoVisual e2 = EfectoVisual.Crear(c, pos, escala, new Color(1f, 1f, 1f, 0.85f), mirada > 0, -1f, "VFX", 14, 180f);
            if (e2 != null) e2.transform.SetParent(transform, true);
        }
        if (e != null) e.transform.SetParent(transform, true);
    }

    // Copia fantasma del sprite que se desvanece (esfera, cortes del agarre).
    private void Estela(Vector2? donde = null)
    {
        if (sr == null || sr.sprite == null) return;
        SpriteRenderer copia = new GameObject("Estela").AddComponent<SpriteRenderer>();
        copia.sprite = sr.sprite;
        copia.transform.position = donde.HasValue ? (Vector3)donde.Value : visual.position;
        copia.transform.localScale = visual.lossyScale;
        copia.sortingLayerName = sr.sortingLayerName;
        copia.sortingOrder = sr.sortingOrder - 1;
        copia.color = new Color(ColorFase.r, ColorFase.g, ColorFase.b, 0.55f);
        copia.sharedMaterial = EfectoVisual.MaterialSinLuz();
        copia.gameObject.AddComponent<DesvanecerEstela>();
    }

    private class DesvanecerEstela : MonoBehaviour
    {
        private float t;
        private SpriteRenderer s;
        private void Awake() { s = GetComponent<SpriteRenderer>(); }
        private void Update()
        {
            t += Time.deltaTime;
            Color c = s.color;
            c.a = Mathf.Lerp(0.55f, 0f, t / 0.3f);
            s.color = c;
            if (t >= 0.3f) Destroy(gameObject);
        }
    }
}
