using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Lo nuevo del Crimson Wraith (Ronda12):
//   - Movimiento con aceleracion y frenada (nunca pasa de 0 a 19 de golpe) y
//     poses "vivas" (PoseViva: respira, se inclina, se estira al golpear).
//   - Avisos distintos: ligero (destello claro y corto) y pesado (rojo, largo,
//     con sonido de carga y el cuerpo encogido).
//   - Poderes nuevos: Guadana doble, Cosecha de sangre, Transfusion, Raices
//     carmesi, Semillas del vacio, Zigzag y Frenesi.
//   - Corazon expuesto: tras Nova, Lluvia y Frenesi el nucleo brilla y recibe
//     mas dano de todo.
public partial class JefeWraith
{
    [Header("Ritmo")]
    [Tooltip("Pausa entre ataques en la fase 1 (x = minima, y = maxima).")]
    [SerializeField] private Vector2 pausaFase1 = new Vector2(0.35f, 0.7f);
    [SerializeField] private Vector2 pausaFase2 = new Vector2(0.15f, 0.4f);
    [Tooltip("Fase 2: probabilidad de encadenar la Guadana doble tras un zarpazo, un teletransporte o un salto.")]
    [Range(0f, 1f)] [SerializeField] private float probabilidadCombo = 0.4f;

    [Header("Frenesi (fase 2, con poca vida)")]
    [Range(0f, 1f)] [SerializeField] private float umbralFrenesi = 0.2f;
    [SerializeField] private float duracionFrenesi = 6f;
    [Tooltip("Rapidez de sus esperas durante el frenesi (1.35 = un 35 % mas rapido).")]
    [SerializeField] private float ritmoFrenesi = 1.35f;
    [Tooltip("Agotado tras el frenesi (con el corazon expuesto).")]
    [SerializeField] private float agotadoTrasFrenesi = 3f;
    [Tooltip("Cuanto se oscurece la arena durante el frenesi (1 = nada).")]
    [Range(0f, 1f)] [SerializeField] private float luzFrenesi = 0.55f;

    [Header("Cosecha de sangre")]
    [Tooltip("La usa si tu barra de sangrado pasa de esto (0.5 = la mitad).")]
    [Range(0f, 1f)] [SerializeField] private float umbralCosecha = 0.5f;

    [Header("Transfusion")]
    [Tooltip("Si te alejas mas que esto, el hilo se rompe.")]
    [SerializeField] private float alcanceTransfusion = 8f;
    [SerializeField] private float duracionTransfusion = 4.5f;
    [SerializeField] private float cadaPulso = 0.9f;
    [Tooltip("Lo que se cura con cada pulso que te llega (fraccion de su vida maxima).")]
    [SerializeField] private float curaPorPulso = 0.02f;

    [Header("Semillas del vacio")]
    public AnimadorHoja.Clip fxSemilla;
    [SerializeField] private Color morado = new Color(0.75f, 0.35f, 1f, 1f);
    [SerializeField] private int semillas = 5;

    public PoseViva Pose { get; private set; }

    // Pruebas: el siguiente ataque que haga.
    private string ataqueForzado;
    public void ForzarAtaque(string ataque) => ataqueForzado = ataque;
    public bool Fase2 => fase2;
    public bool EnFrenesi => enFrenesi;
    public bool CorazonVisible => CorazonExpuesto;

    private float tCosecha = -99f, tTransfusion = -99f, tRaices = -99f, tSemillas = -99f;
    private bool frenesiHecho, enFrenesi;
    private float finFrenesi;
    private float corazonHasta;
    private SpriteRenderer brilloCorazon;
    private readonly List<(float t, Vector2 p)> rastro = new List<(float, Vector2)>();
    private Light2D luzGlobal;
    private float intensidadAntesFrenesi;

    private bool CorazonExpuesto => Time.time < corazonHasta;

    // Lo llama Awake.
    private void IniciarPoderes()
    {
        if (visual != null)
        {
            Pose = visual.GetComponent<PoseViva>();
            if (Pose == null) Pose = visual.gameObject.AddComponent<PoseViva>();
            Pose.cuerpo = rb;
        }
        foreach (Light2D l in FindObjectsByType<Light2D>(FindObjectsSortMode.None))
            if (l.lightType == Light2D.LightType.Global) { luzGlobal = l; break; }
    }

    // ------------------------------------------------------------------ Motor

    // La velocidad se acerca a la pedida con esta aceleracion (unidades/s por
    // segundo): arranca, llega y frena con curva, sin saltos.
    private bool motor;
    private float vxObjetivo, acelMotor;

    private void Acelerar(float vx, float aceleracion)
    {
        motor = true;
        vxObjetivo = vx;
        acelMotor = Mathf.Max(1f, aceleracion);
    }

    private void FrenarSuave(float frenada) => Acelerar(0f, frenada);

    // En seco (aturdido, un parry, transformarse).
    private void Detener()
    {
        motor = false;
        if (rb != null && rb.bodyType == RigidbodyType2D.Dynamic) rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
    }

    private void FixedUpdate()
    {
        if (!motor || rb == null || rb.bodyType != RigidbodyType2D.Dynamic) return;
        float vx = Mathf.MoveTowards(rb.linearVelocity.x, vxObjetivo, acelMotor * Time.fixedDeltaTime);
        rb.linearVelocity = new Vector2(vx, rb.linearVelocity.y);
        if (Mathf.Approximately(vxObjetivo, 0f) && Mathf.Abs(vx) < 0.01f) motor = false;
    }

    private void Update()
    {
        // Rastro de tus posiciones (las raices salen por donde pasaste).
        if (player != null)
        {
            rastro.Add((Time.time, player.position));
            while (rastro.Count > 0 && Time.time - rastro[0].t > 2f) rastro.RemoveAt(0);
        }
        if (brilloCorazon != null)
        {
            bool on = CorazonExpuesto && !muerto;
            brilloCorazon.enabled = on;
            if (on)
            {
                float p = 0.5f + 0.5f * Mathf.Sin(Time.time * 12f);
                float s = (fase2 ? 1.3f : 0.9f) * (0.85f + 0.3f * p);
                brilloCorazon.transform.localScale = new Vector3(s, s, 1f);
                brilloCorazon.color = new Color(1f, 0.85f, 0.4f, 0.45f + 0.4f * p);
                brilloCorazon.transform.localPosition = new Vector3(0f, fase2 ? 2.6f : 1.7f, 0f);
            }
        }
    }

    private Vector2 PosicionHace(float segundos)
    {
        float t = Time.time - segundos;
        for (int i = rastro.Count - 1; i >= 0; i--) if (rastro[i].t <= t) return rastro[i].p;
        return rastro.Count > 0 ? rastro[0].p : PosPlayer;
    }

    // ------------------------------------------------------------------ Avisos

    // Ligero: destello claro y corto, y se encoge un poco.
    private void AvisoLigero(float segundos)
    {
        colorAviso = new Color(1f, 0.75f, 0.55f, 1f);
        LanzarAviso(segundos, 0.5f);
        Pose?.Anticipar(0.7f, segundos);
    }

    // Pesado: rojo intenso, largo, con sonido de carga y el cuerpo muy encogido.
    private void AvisoPesado(float segundos)
    {
        colorAviso = new Color(1f, 0.1f, 0.1f, 1f);
        LanzarAviso(segundos, 0.85f);
        Pose?.Anticipar(1.4f, segundos);
        Sonar("jefe_carga_tajo", 0.7f, 0.8f);
    }

    // ------------------------------------------------------------------ Corazon expuesto

    private void ExponerCorazon(float segundos = -1f)
    {
        corazonHasta = Time.time + (segundos > 0f ? segundos : duracionCorazon);
        if (brilloCorazon == null)
        {
            brilloCorazon = new GameObject("Corazon").AddComponent<SpriteRenderer>();
            brilloCorazon.transform.SetParent(transform, false);
            brilloCorazon.sprite = EnemigoMago.CirculoSprite();
            brilloCorazon.sharedMaterial = EfectoVisual.MaterialSinLuz();
            brilloCorazon.sortingLayerName = "VFX";
            brilloCorazon.sortingOrder = 8;
        }
        TextoFlotante.Mostrar("¡Corazón expuesto!", (Vector2)transform.position + Vector2.up * (fase2 ? 4.6f : 3.6f), new Color(1f, 0.85f, 0.4f), 1.1f);
        Sonar("jefe_postura", 0.6f, 1.2f);
    }

    // ------------------------------------------------------------------ Frenesi

    // Con poca vida en la fase 2: ruge, la arena se oscurece y encadena ataques
    // casi sin pausa unos segundos. Despues queda agotado y con el corazon expuesto.
    private IEnumerator Frenesi()
    {
        frenesiHecho = true;
        Detener();
        anim.Reproducir("grande_rugido", true);
        AvisoPesado(1f);
        Sonar("jefe_transformacion", 0.9f, 1.15f);
        ScreenFlash.Destello(new Color(0.9f, 0.05f, 0.1f, 0.4f), 0.4f);
        CamaraDinamica.CamaraLenta(0.4f, 0.5f);
        CamaraDinamica.Ampliar(7f, 2f);
        Sacudir(1.8f);
        TextoFlotante.Mostrar("¡Frenesí!", (Vector2)transform.position + Vector2.up * 4.8f, new Color(1f, 0.3f, 0.3f), 1.3f);
        yield return Temblar(1f, 0.07f);
        if (luzGlobal != null) { intensidadAntesFrenesi = luzGlobal.intensity; StartCoroutine(Luz(intensidadAntesFrenesi * luzFrenesi, 0.8f)); }
        enFrenesi = true;
        finFrenesi = Time.time + duracionFrenesi;
    }

    private IEnumerator FinFrenesi()
    {
        enFrenesi = false;
        Detener();
        if (luzGlobal != null) StartCoroutine(Luz(intensidadAntesFrenesi, 1.2f));
        anim.Reproducir("grande_guardia", true);
        TextoFlotante.Mostrar("Agotado", (Vector2)transform.position + Vector2.up * 4.6f, new Color(1f, 0.95f, 0.7f), 1f);
        ExponerCorazon(agotadoTrasFrenesi);
        yield return Temblar(agotadoTrasFrenesi, 0.05f);
    }

    private IEnumerator Luz(float hasta, float segundos)
    {
        if (luzGlobal == null) yield break;
        float desde = luzGlobal.intensity;
        for (float t = 0f; t < segundos; t += Time.deltaTime)
        {
            luzGlobal.intensity = Mathf.Lerp(desde, hasta, t / segundos);
            yield return null;
        }
        luzGlobal.intensity = hasta;
    }

    // ------------------------------------------------------------------ Guadana doble

    // Fase 2: un tajo enorme hacia delante y, sin girarse, otro hacia atras.
    // Castiga a quien se pone a su espalda.
    private IEnumerator GuadanaDoble()
    {
        MirarAlPlayer();
        anim.Reproducir(anim.Tiene("grande_preparar") ? "grande_preparar" : "grande_guardia", true);
        AvisoPesado(0.55f);
        CamaraDinamica.Ampliar(6.6f, 2f);
        yield return Temblar(0.55f, 0.04f);

        anim.Reproducir("grande_zarpazo", true);
        Pose?.Soltar(1.3f, 0.15f);
        Sonar("jefe_golpe_fuerte");
        Sacudir(1.3f);
        Acelerar(mirada * 3f, 40f);
        var r = Golpear(new Vector2(2.8f, 2.2f), new Vector2(5.6f, 4.6f), danoFuerte);
        if (r == PlayerControler.ResultadoDano.Parry) { parado = true; Detener(); yield break; }
        yield return Esperar(0.22f);

        // El reves, hacia atras (la pose con el arco a su espalda).
        anim.Reproducir(anim.Tiene("grande_reves") ? "grande_reves" : "grande_zarpazo", true);
        Pose?.Soltar(1.1f, 0.12f);
        Sonar("jefe_tajo_fuerte");
        Sacudir(1f);
        r = Golpear(new Vector2(-2.6f, 2.2f), new Vector2(5.2f, 4.6f), danoNormal);
        if (r == PlayerControler.ResultadoDano.Parry) { parado = true; Detener(); yield break; }
        FrenarSuave(30f);
        yield return Esperar(0.5f);
    }

    // ------------------------------------------------------------------ Cosecha de sangre

    // Cuanto tienes acumulado de sangrado (0..1). Si ya te ha estallado, 0.
    private float SangradoDelPlayer()
    {
        EstadosPlayer e = EstadosPlayer.Instancia;
        if (e == null || e.EnEfecto(EstadoPlayer.Sangrado)) return 0f;
        return e.Fraccion(EstadoPlayer.Sangrado);
    }

    // Si tu barra de sangrado va por la mitad, cierra el puno y la hace estallar:
    // un aro se cierra sobre ti (aviso) y luego dano segun lo acumulado. Se
    // esquiva rodando en el momento justo; la barra se vacia.
    private IEnumerator CosechaDeSangre()
    {
        tCosecha = Time.time;
        Detener();
        anim.Reproducir(fase2 ? "grande_rugido" : "conjuro", true);
        AvisoPesado(0.8f);
        Sonar("jefe_revivir_carga", 0.6f, 1.2f);
        yield return AroDeAviso(PosPlayer + Vector2.up * 0.6f, 1.6f, 0.8f, 0.03f);

        float frac = SangradoDelPlayer();
        PlayerControler p = player != null ? player.GetComponent<PlayerControler>() : null;
        if (p == null || frac <= 0f) yield break;
        EfectoVisual.Crear(fxSangre, PosPlayer + Vector2.up * 0.8f, 2.2f, carmesi);
        Sonar("jefe_nova", 0.7f, 1.3f);
        Sacudir(1f);
        int dano = Mathf.RoundToInt(danoFuerte * (0.6f + frac));
        PlayerControler.ResultadoDano r = p.TakeDamage(dano, this, PlayerControler.TipoDano.Magico);
        if (r == PlayerControler.ResultadoDano.Parry) { parado = true; yield break; }
        if (r != PlayerControler.ResultadoDano.Ignorado && EstadosPlayer.Instancia != null) EstadosPlayer.Instancia.Vaciar(EstadoPlayer.Sangrado);
        yield return Esperar(0.5f);
    }

    // ------------------------------------------------------------------ Transfusion

    // Fase 2: un hilo de sangre te une a el. Cada pulso que te llega le cura.
    // Se rompe si te alejas o si paras un pulso con un parry.
    private IEnumerator Transfusion()
    {
        tTransfusion = Time.time;
        Detener();
        anim.Reproducir("grande_rugido", true);
        AvisoPesado(0.6f);
        yield return Esperar(0.6f);

        LineRenderer hilo = new GameObject("Transfusion").AddComponent<LineRenderer>();
        hilo.sharedMaterial = EfectoVisual.MaterialSinLuz();
        hilo.positionCount = 2;
        hilo.sortingLayerName = "VFX";
        hilo.sortingOrder = 7;
        hilo.startColor = hilo.endColor = carmesi;
        PeligrosJefe.Registrar(hilo.gameObject);
        PlayerControler p = player != null ? player.GetComponent<PlayerControler>() : null;
        Sonar("jefe_revivir_carga", 0.5f, 0.9f);

        float siguiente = 0.5f;
        bool roto = false;
        for (float t = 0f; t < duracionTransfusion && !roto && p != null; t += Time.deltaTime)
        {
            Vector2 a = (Vector2)transform.position + Vector2.up * 2.6f;
            Vector2 b = PosPlayer + Vector2.up * 0.7f;
            hilo.SetPosition(0, a);
            hilo.SetPosition(1, b);
            float ancho = 0.08f + 0.05f * Mathf.Sin(t * 14f);
            hilo.startWidth = ancho * 1.4f;
            hilo.endWidth = ancho;
            if (Vector2.Distance(a, b) > alcanceTransfusion)
            {
                roto = true;
                TextoFlotante.Mostrar("Hilo roto", b + Vector2.up, new Color(1f, 0.9f, 0.8f), 0.9f);
                break;
            }
            if (t >= siguiente)
            {
                siguiente += cadaPulso;
                PlayerControler.ResultadoDano r = p.TakeDamage(Mathf.Max(1, danoLeve / 2), this, PlayerControler.TipoDano.Magico);
                if (r == PlayerControler.ResultadoDano.Parry)
                {
                    roto = true;
                    parado = true;
                    TextoFlotante.Mostrar("¡Cortado!", b + Vector2.up, new Color(1f, 0.9f, 0.5f), 1f);
                }
                else if (r == PlayerControler.ResultadoDano.Recibido)
                {
                    int cura = Mathf.RoundToInt(salud.MaxHealth * curaPorPulso);
                    salud.Curar(cura);
                    TextoFlotante.Mostrar("+" + cura, (Vector2)transform.position + Vector2.up * 4.2f, new Color(0.95f, 0.3f, 0.35f), 0.9f);
                }
            }
            yield return null;
        }
        if (hilo != null) Destroy(hilo.gameObject);
        yield return Esperar(0.3f);
    }

    // ------------------------------------------------------------------ Raices carmesi

    // Fase 1: tentaculos que salen del suelo por donde pasaste, uno tras otro.
    // Hay que cambiar de direccion.
    private IEnumerator RaicesCarmesi()
    {
        tRaices = Time.time;
        Detener();
        anim.Reproducir("conjuro", true);
        AvisoLigero(0.5f);
        yield return Esperar(0.5f);
        for (int i = 0; i < 7; i++)
        {
            Vector2 p = PosicionHace(0.45f);
            float x = Mathf.Clamp(p.x, arena.xMin + 0.6f, arena.xMax - 0.6f);
            PilarSangre.Invocar(fxGlifo, fxPilar, new Vector2(x, suelo), 0.55f, danoLeve, sangradoPilar * 0.6f, carmesi, 2.6f);
            yield return Esperar(0.38f);
        }
        yield return Esperar(0.5f);
    }

    // ------------------------------------------------------------------ Semillas del vacio

    // Fase 2: lanza orbes morados en arco que caen al suelo y, un momento
    // despues, brotan como pilares. Siembra el campo.
    private IEnumerator SemillasDelVacio()
    {
        tSemillas = Time.time;
        Detener();
        anim.Reproducir("grande_rugido", true);
        AvisoLigero(0.5f);
        yield return Esperar(0.5f);
        Vector2 boca = (Vector2)transform.position + new Vector2(mirada * 0.8f, 4f);
        for (int i = 0; i < semillas; i++)
        {
            float x = Mathf.Clamp(PosPlayer.x + (i - (semillas - 1) * 0.5f) * 2.4f + Random.Range(-0.4f, 0.4f), arena.xMin + 0.8f, arena.xMax - 0.8f);
            StartCoroutine(Semilla(boca, new Vector2(x, suelo), 0.75f));
            Sonar("jefe_medialuna", 0.4f, 1.3f);
            yield return Esperar(0.12f);
        }
        yield return Esperar(1.4f);
    }

    private IEnumerator Semilla(Vector2 desde, Vector2 hasta, float duracion)
    {
        AnimadorHoja.Clip clip = fxSemilla != null && ((fxSemilla.fotogramas != null && fxSemilla.fotogramas.Length > 0) || fxSemilla.hoja != null) ? fxSemilla : fxOrbe;
        EfectoVisual s = EfectoVisual.Crear(clip, desde, 1.4f, morado, false, -1f, "VFX", 9);
        if (s != null) PeligrosJefe.Registrar(s.gameObject);
        for (float t = 0f; t < duracion; t += Time.deltaTime)
        {
            float u = t / duracion;
            Vector2 p = Vector2.Lerp(desde, hasta, u) + Vector2.up * 2.5f * 4f * u * (1f - u);
            if (s != null) s.transform.position = p;
            yield return null;
        }
        if (s != null) Destroy(s.gameObject);
        PilarSangre.Invocar(fxGlifo, fxPilar, hasta, 0.9f, danoNormal, sangradoPilar, morado);
    }

    // ------------------------------------------------------------------ Reves

    // Fase 1, tras los zarpazos: si te has puesto a su espalda (cerca), un
    // reves hacia atras sin girarse. Aviso corto pero visible.
    private IEnumerator RevesSiDetras()
    {
        if (fase2 || player == null || !anim.Tiene("reves")) yield break;
        float dx = DxPlayer;
        if (Mathf.Sign(dx) == mirada || Mathf.Abs(dx) > 3.2f) yield break;
        AvisoLigero(0.3f);
        yield return Esperar(0.3f);
        anim.Reproducir("reves", true);
        Pose?.Soltar();
        Sonar("jefe_tajo", 0.8f, 0.9f);
        var r = Golpear(new Vector2(-2f, 1.8f), new Vector2(3.8f, 3.4f), danoNormal);
        if (r == PlayerControler.ResultadoDano.Parry) { parado = true; Detener(); }
        yield return Esperar(0.3f);
    }

    // ------------------------------------------------------------------ Zigzag

    // Fase 1: embestida de ida y, casi sin respiro, la de vuelta.
    private IEnumerator Zigzag()
    {
        yield return Embestida();
        if (parado) yield break;
        MirarAlPlayer();
        yield return Embestida(0.3f);
    }
}
