using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// El Black Knight (5): CIELO PARTIDO, su ataque mas peligroso (desde la fase 1).
//
// Avisa con la senal de peligro parpadeando sobre tu cabeza, cada vez mas
// rapido; al ultimo parpadeo se desvanece y aparece pegado a ti. No se bloquea
// ni se para: solo se salva la esquiva (sus fotogramas invulnerables). Si lo
// esquivas, queda expuesto un buen rato. Si te atrapa, te lanza al cielo y te
// cruza con tajos larguisimos, uno tras otro (cuando uno acaba ya entra el
// siguiente), cada uno desde un lado distinto, como en el boceto.
//
// Al acabar: si llegabas con la vida llena, te deja con 1 de vida; con menos, te
// mata. Todos los numeros estan en la ficha (Ajustes Black Knight).
public partial class JefeBlackKnight
{
    private bool enCielo;
    private float finEnfriamientoCielo;
    private readonly List<TajoCieloBK> tajosCielo = new List<TajoCieloBK>();

    public bool EnCielo => enCielo;
    // Esta avisando (la senal sobre tu cabeza) o ya en la ventana del agarre
    // (lo leen las pruebas).
    public bool AvisoCielo { get; private set; }
    public bool CieloCazando { get; private set; }
    public int TajosCieloDados { get; private set; }
    public int CielosAgarrados { get; private set; }
    public int CielosFallados { get; private set; }

    private IEnumerator CieloPartido()
    {
        finEnfriamientoCielo = Time.time + EnfriamientoConPresion(ajustes.enfriamientoCielo);
        AvisoCielo = true;
        BuscarPlayer();
        MirarYa();
        Parar();
        CambiarEstado(EstadoEnemigo.Anticipacion);

        // 1. Aviso: la senal de peligro parpadea sobre tu cabeza, cada vez mas
        //    rapido. No hay marca en el suelo: huir no sirve, hay que esquivar.
        anim.Reproducir(ClipArte, true, 0.5f);
        Sonar("jefe_carga_tajo", 0.8f, 0.8f);
        SpriteRenderer senal = SenalPeligro();
        float aviso = ajustes.avisoCielo / Mathf.Max(0.5f, Desafio.MultVelocidad);
        int parpadeos = Mathf.Max(1, ajustes.parpadeosCielo);
        float total = parpadeos * (parpadeos + 1f) * 0.5f;
        for (int i = 0; i < parpadeos; i++)
        {
            // El primero es el mas largo y el ultimo el mas corto: se acelera.
            float dur = aviso * (parpadeos - i) / total;
            Sonar("jefe_carga_tajo", 0.5f, 1f + 0.12f * i);
            for (float t = 0f; t < dur; t += Time.deltaTime)
            {
                bool visible = t < dur * 0.6f;
                ColocarSenal(senal, visible, 1f + 0.15f * i, t / dur);
                ritmo.Destello(ajustes.colorTajoCielo, visible ? 0.55f : 0.15f);
                Parar();
                MirarAlPlayer();
                if (anim.Terminado) anim.Reproducir(ClipArte, true, 0.5f);
                yield return null;
            }
        }
        ritmo.Destello(ajustes.colorTajoCielo, 0f);
        if (senal != null) Destroy(senal.gameObject);

        // 2. Se desvanece y aparece pegado a ti: no se puede bloquear ni parar,
        //    solo esquivar (los fotogramas invulnerables de la esquiva).
        PlayerControler p = PC;
        bool atrapado = false;
        AtravesarPlayer(true);
        yield return FundirCaballero(0.08f, false);
        BuscarPlayer();
        int lado = DentroDeArena(PosPlayer.x >= transform.position.x ? 1 : -1) ? (PosPlayer.x >= transform.position.x ? 1 : -1) : -1;
        float x = LimitarX(PosPlayer.x - lado * ajustes.distanciaTeleportCielo);
        Colocar(new Vector2(x, PeligrosJefe.SueloBajo(new Vector2(x, PosPlayer.y + 0.5f), suelo)));
        MirarYa();
        Sonar("jefe_aparicion", 0.7f, 1.2f);
        ParticulasFx.Rafaga(transform.position, 12, ajustes.colorTajoCielo, ajustes.colorTajoCielo2, new Vector2(1.5f, 4f), 0.3f,
                            new Vector2(0.05f, 0.1f), new Vector2(0.15f, 0.35f));
        yield return FundirCaballero(0.06f, true);
        anim.Reproducir(ClipArte, true, 1.4f);

        CieloCazando = true;
        float ventana = ajustes.embestidaCielo + 0.15f;
        for (float t = 0f; t < ventana && !atrapado; t += Time.deltaTime)
        {
            Parar();
            MirarAlPlayer();
            if (p == null) p = PC;
            if (p != null)
            {
                Vector2 caja = ajustes.alcanceCielo * Esc;
                Vector2 centro = (Vector2)transform.position + new Vector2(mirada * caja.x * 0.3f, caja.y * 0.45f);
                if (GolpeCazadora.PlayerEn(centro, caja) != null && p.IntentarAgarre()) atrapado = true;
                if (DepuracionCazadora.VerCajas) CajaDebug.Mostrar(centro, caja, ajustes.colorTajoCielo, 0.03f);
            }
            yield return null;
        }
        CieloCazando = AvisoCielo = false;
        Parar();
        AtravesarPlayer(false);

        if (!atrapado)
        {
            // Lo esquivaste: se queda vendido.
            CielosFallados++;
            TextoFlotante.Mostrar("¡Esquivado!", (Vector2)transform.position + Vector2.up * 2.4f, new Color(0.8f, 1f, 0.8f), 1f);
            yield return Aturdirse(ajustes.aturdidoCieloFallado, "cielo");
            yield break;
        }

        CielosAgarrados++;
        yield return SentenciaEnElCielo(p);
    }

    // Te tiene agarrado: al cielo y tajo tras tajo.
    private IEnumerator SentenciaEnElCielo(PlayerControler p)
    {
        enCielo = true;
        invulnerable = true;
        volando = true;
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.linearVelocity = Vector2.zero;
        AtravesarPlayer(true);
        Sonar("jefe_caida", 0.9f, 0.8f);
        CamaraDinamica.Sacudir(1f);
        ScreenFlash.Destello(new Color(1f, 0.5f, 0.15f, 0.3f), 0.2f);

        // Con la vida llena sales vivo; con menos, no.
        bool perdonar = p.VidaActual >= p.VidaMaxima;
        Vector2 inicio = p.transform.position;
        Vector2 alto = inicio + Vector2.up * ajustes.alturaCielo;

        // 1. Lo sube de un empujon, con el caballero a su lado.
        for (float t = 0f; t < ajustes.subidaCielo; t += Time.deltaTime)
        {
            if (p == null || !p.Agarrado) { yield return Aterrizar(); yield break; }
            float u = 1f - (1f - t / ajustes.subidaCielo) * (1f - t / ajustes.subidaCielo);
            Vector2 pos = Vector2.Lerp(inicio, alto, u);
            p.MoverAgarrado(pos);
            Colocar(pos + new Vector2(-mirada * 1.6f, -0.4f));
            anim.Reproducir(ClipAire, true);
            EncuadrarCielo(pos);
            yield return null;
        }

        // 2. Los tajos: uno tras otro, cruzandose desde lados distintos.
        int n = Mathf.Max(1, ajustes.tajosCielo);
        float angulo = Random.Range(0f, 180f);
        for (int i = 0; i < n - 1; i++)
        {
            if (p == null || !p.Agarrado) break;
            // Angulos repartidos (como los trazos del boceto) con algo de azar.
            angulo += 180f / n + Random.Range(-18f, 18f);
            float rad = angulo * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            Vector2 centro = alto + Random.insideUnitCircle * 0.25f;
            p.MoverAgarrado(centro);
            tajosCielo.Add(TajoCieloBK.Lanzar(centro, angulo, ajustes.largoTajoCielo, ajustes.grosorTajoCielo, ajustes,
                                              ajustes.trazoTajoCielo, ajustes.quietoTajoCielo, ajustes.salidaTajoCielo));
            // El caballero cruza con el tajo, de un lado al otro.
            Vector2 desde = centro - dir * 3.2f, hasta = centro + dir * 3.2f;
            Colocar(desde);
            Mirar(dir.x >= 0f ? 1 : -1);
            anim.Reproducir(ClipArte, true, 1.6f);
            Sonar("jefe_tajo_fuerte", 0.8f, 1f + 0.04f * i);
            CamaraDinamica.Sacudir(0.35f);
            ParticulasFx.Rafaga(centro, 14, ajustes.colorTajoCielo, ajustes.colorTajoCielo2, new Vector2(2f, 6f), 0.45f,
                                new Vector2(0.05f, 0.1f), new Vector2(0.15f, 0.4f));
            TajosCieloDados++;

            // El dano se reparte entre los que quedan (el ultimo lo decide todo).
            int falta = p.VidaActual - Mathf.Max(1, ajustes.vidaRestanteCielo);
            if (falta > 0) p.CorteAgarre(Mathf.Max(1, Mathf.CeilToInt(falta / (float)(n - i))));

            float espera = ajustes.intervaloTajoCielo / Mathf.Max(0.5f, Desafio.MultVelocidad);
            for (float t = 0f; t < espera; t += Time.deltaTime)
            {
                if (p == null || !p.Agarrado) break;
                p.MoverAgarrado(centro + (Vector2)(Random.insideUnitCircle * 0.06f));
                Colocar(Vector2.Lerp(desde, hasta, Mathf.Clamp01(t / espera)));
                EncuadrarCielo(centro);
                yield return null;
            }
        }

        // 3. El ultimo, desde arriba: el que sentencia.
        if (p != null && p.Agarrado)
        {
            p.MoverAgarrado(alto);
            Colocar(alto + Vector2.up * 2.2f);
            anim.Reproducir(ClipArte, true, 1f);
            tajosCielo.Add(TajoCieloBK.Lanzar(alto, Random.Range(80f, 100f), ajustes.largoTajoCielo * 1.1f, ajustes.grosorTajoCielo * 1.6f,
                                              ajustes, ajustes.trazoTajoCielo * 0.8f, ajustes.quietoTajoCielo + 0.08f, ajustes.salidaTajoCielo * 1.6f));
            TajosCieloDados++;
            Sonar("jefe_tajo_fuerte", 1f, 0.75f);
            CamaraDinamica.CamaraLenta(0.3f, 0.3f);
            CamaraDinamica.Sacudir(1.4f);
            ScreenFlash.Destello(new Color(1f, 0.55f, 0.2f, 0.45f), 0.35f);
            p.CorteAgarreFinal(Mathf.Max(1, ajustes.vidaRestanteCielo), perdonar);
            yield return EsperarCielo(0.35f);
            if (p != null && p.Agarrado) p.SoltarAgarre(true);
        }
        yield return Aterrizar();
    }

    // Baja al suelo y vuelve a la pelea.
    private IEnumerator Aterrizar()
    {
        enCielo = false;
        float y0 = transform.position.y;
        float yF = PeligrosJefe.SueloBajo(new Vector2(transform.position.x, y0), suelo);
        anim.Reproducir(ClipCaer, true);
        for (float t = 0f; t < 0.35f; t += Time.deltaTime)
        {
            Colocar(new Vector2(transform.position.x, Mathf.Lerp(y0, yF, t / 0.35f)));
            yield return null;
        }
        Colocar(new Vector2(LimitarX(transform.position.x), yF));
        yield return new WaitForFixedUpdate();
        TerminarVuelo();
        AtravesarPlayer(false);
        invulnerable = false;
        MirarYa();
        anim.Reproducir(ClipAterrizar, true);
        CamaraDinamica.Sacudir(0.5f);
        while (!anim.Terminado) yield return null;
        yield return Quieto(0.7f / Vel);
    }

    // Mientras estas en el aire la camara se abre y os mira a los dos.
    private void EncuadrarCielo(Vector2 centro)
    {
        CamaraDinamica.Ampliar(ajustes.camaraCielo, 0.25f);
        CamaraDinamica.Encuadrar(centro, 0.8f, 0.25f, 8f);
    }

    // Lo mueve estando "volando" (cuerpo cinematico).
    private void Colocar(Vector2 pos)
    {
        rb.position = pos;
        transform.position = pos;
        rb.linearVelocity = Vector2.zero;
    }

    private Vector2 SueloBajoPlayer()
    {
        Vector2 p = PosPlayer;
        return new Vector2(p.x, PeligrosJefe.SueloBajo(p + Vector2.up * 0.2f, suelo));
    }

    private IEnumerator EsperarCielo(float segundos)
    {
        for (float t = 0f; t < segundos; t += Time.deltaTime) yield return null;
    }

    // La senal de peligro sobre tu cabeza (el icono rojo de siempre).
    private SpriteRenderer SenalPeligro()
    {
        Sprite ico = RecursosRPG.Get().Icono("peligro");
        if (ico == null) return null;
        SpriteRenderer sr = DibujosCazadora.Renderer(new GameObject("SenalPeligroBK"), "VFX", 70);
        sr.sprite = ico;
        PeligrosJefe.Registrar(sr.gameObject);
        return sr;
    }

    // La pone sobre la cabeza del jugador, encendida o apagada, con un latido.
    private void ColocarSenal(SpriteRenderer sr, bool visible, float escala, float k)
    {
        if (sr == null) return;
        sr.transform.position = PosPlayer + Vector2.up * ajustes.alturaSenalCielo;
        float s = ajustes.tamanoSenalCielo * escala * (visible ? 1f + 0.12f * Mathf.Sin(k * Mathf.PI) : 0.9f);
        sr.transform.localScale = new Vector3(s, s, 1f);
        Color c = ajustes.colorTajoCielo;
        sr.color = new Color(c.r, c.g, c.b, visible ? 1f : 0.12f);
    }

    // Se desvanece o reaparece (el teletransporte).
    private IEnumerator FundirCaballero(float segundos, bool aparecer)
    {
        if (srCaballero == null) yield break;
        Color c = srCaballero.color;
        for (float t = 0f; t < segundos; t += Time.deltaTime)
        {
            float a = aparecer ? t / segundos : 1f - t / segundos;
            srCaballero.color = new Color(c.r, c.g, c.b, a);
            yield return null;
        }
        srCaballero.color = new Color(c.r, c.g, c.b, aparecer ? 1f : 0f);
    }

    private void LimpiarTajosCielo()
    {
        foreach (TajoCieloBK t in tajosCielo) if (t != null) Destroy(t.gameObject);
        tajosCielo.Clear();
    }
}
