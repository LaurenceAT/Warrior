using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Lo especial de la Cazadora: su guardia (Parry de la Cazadora), el rugido con
// escudo, los tres instakills (uno por fase), el aturdimiento, las muertes
// falsas entre barras y la muerte final.
public partial class JefeCazadora
{
    // ------------------------------------------------------------------ Aturdida

    // Tiempo real del juego (la camara lenta no alarga los temporizadores; la
    // pausa si los detiene).
    private static float DtReal => Time.timeScale > 0.01f ? Time.unscaledDeltaTime : 0f;

    // Aturdida (tu parry, el sagrado, el escudo roto) o expuesta (tras un
    // instakill esquivado): la pose de los primeros cuadros de Death sin llegar a
    // caer, con temblor y destellos. Es tu ventana para castigar.
    private IEnumerator Aturdida(float segundos, string texto, bool conBonus)
    {
        cuerpo.CortarTrayecto();
        cuerpo.Detener();
        enAviso = enGolpe = false;
        cuerpoAnim.Pose("muerte", 1);
        cuerpoAnim.Temblar(0.02f);
        if (!string.IsNullOrEmpty(texto))
            TextoFlotante.Mostrar(texto, Pos + Vector2.up * 2.1f * Tam, new Color(1f, 0.95f, 0.7f), 1f);
        Sonar("aturdida", 0.8f);
        bonusActivo = conBonus;
        float siguiente = 0f;
        for (float t = 0f; t < segundos; t += DtReal)
        {
            if (t >= siguiente) { siguiente = t + 0.4f; cuerpoAnim.Destello(Color.white, 0.35f, 0.08f); }
            yield return null;
        }
        bonusActivo = false;
        cuerpoAnim.Temblar(0f);
        cuerpoAnim.Reproducir("quieto");
        golpesRecibidos.Clear();
    }

    // ------------------------------------------------------------------ Guardia

    // Parry de la Cazadora: si le pegas sin parar, adopta la guardia (tajo arriba
    // con un brillo fino en la espada). Hasta el contacto se puede evitar
    // (esperar a que se apague el brillo, retroceder o rodar). Si la golpeas:
    //   1. Contacto: pausa breve, suena el parry y quedas aturdido de verdad, con
    //      un retroceso corto.
    //   2. Ella se gira hacia donde estas DE VERDAD (no al ultimo ruido) justo
    //      antes del golpe y, si quedaste lejos, te alcanza con un dash.
    //   3. Estocada: media vida (letal si tienes eso o menos).
    //   4. Si sobrevives, sales despedido y caes; invulnerable hasta levantarte.
    private IEnumerator Guardia()
    {
        siguienteGuardia = Time.time + ajustes.enfriamientoParry;
        golpesRecibidos.Clear();
        PlayerControler p = PC;
        if (p == null) yield break;
        bool vidaBaja = p.VidaActual < p.VidaMaxima * 0.5f;
        MirarA(p.transform.position.x);
        cuerpo.Parar(ajustes.frenada * 2f);
        cuerpoAnim.Pose("tajoArriba", 0);
        Sonar("guardia", 0.8f);
        if (vidaBaja) UICazadora.Latido(true);
        enGuardia = true;
        inicioGuardia = Time.time;
        parryHecho = false;
        float siguiente = 0f;
        for (float t = 0f; t < ajustes.duracionPose && !parryHecho; t += Time.deltaTime)
        {
            if (Time.time >= siguiente)
            {
                siguiente = Time.time + 0.16f;
                PolvoCazadora.Soltar(Pos + new Vector2(mirada * 0.25f, 1.5f) * Tam, new Color(1f, 1f, 1f, 0.9f), 2, 0.25f);
            }
            cuerpoAnim.Destello(new Color(0.9f, 0.95f, 1f), 0.18f + 0.12f * Mathf.Sin(t * 18f), 0.05f);
            yield return null;
        }
        enGuardia = false;
        if (!parryHecho)
        {
            UICazadora.Latido(false);
            cuerpoAnim.Reproducir("quieto");
            yield return Esperar(0.15f);
            yield break;
        }
        yield return EstocadaTrasParry(vidaBaja);
    }

    // Al principio de la guardia: tu golpe rebota en su espada sin castigo.
    private float inicioGuardia = -99f;
    public int RebotesGuardia { get; private set; }
    public bool ParoTuGolpe => parryHecho;

    private void ReboteGuardia()
    {
        RebotesGuardia++;
        Sonar("guardia", 0.5f, 1.3f);
        PolvoCazadora.Soltar(Pos + new Vector2(mirada * 0.4f, 1.1f) * Tam, Color.white, 6, 0.6f);
    }

    // El contacto (lo llama tu golpe, desde Modificar).
    private void ParryDeLaCazadora()
    {
        if (!enGuardia) return;
        enGuardia = false;
        parryHecho = true;
        Sonar("parry", 1f);
        PolvoCazadora.Soltar(Pos + new Vector2(mirada * 0.4f, 1.1f) * Tam, Color.white, 24, 1.8f);
        ScreenFlash.Destello(new Color(1f, 1f, 1f, 0.3f), 0.12f);
        CamaraCazadora.Congelar(ajustes.hitStopParry);
        CamaraCazadora.Acercar(ajustes.zoomParry, ajustes.esperaEstocada + 0.6f);
        TextoFlotante.Mostrar("¡Parada!", Pos + Vector2.up * 2.2f * Tam, new Color(1f, 0.9f, 0.6f), 1f);
        PlayerControler p = PC;
        if (p != null)
        {
            int lado = p.transform.position.x >= Pos.x ? 1 : -1;
            p.Aturdir(ajustes.aturdimientoJugador, ajustes.retrocesoParry, lado);
        }
    }

    private IEnumerator EstocadaTrasParry(bool vidaBaja)
    {
        // Durante toda la secuencia no hace caso de los ruidos: va a por ti.
        oido.Sordo = true;
        enAviso = true;
        cuerpoAnim.Pose("estocada", 0);
        if (vidaBaja)
        {
            // Momento de panico: latido, vineta roja y una pausa dramatica.
            Sonar("latido", 1f);
            UICazadora.PulsoRojo(0.55f, 1.2f);
            CamaraCazadora.Congelar(ajustes.pausaDramatica, 0.06f);
        }
        for (float t = 0f; t < ajustes.esperaEstocada; t += Time.deltaTime)
        {
            cuerpoAnim.Destello(new Color(1f, 0.25f, 0.2f), 0.35f + 0.25f * Mathf.Sin(t * 30f), 0.05f);
            yield return null;
        }
        enAviso = false;

        PlayerControler p = PC;
        if (p == null || p.VidaActual <= 0) { FinEstocada(); yield break; }
        // Reapunta ahora, a tu posicion real, y no cambia de objetivo hasta el final.
        float x = p.transform.position.x;
        int dir = x >= Pos.x ? 1 : -1;
        Mirar(dir);
        float llegada = cuerpo.Limitar(x - dir * 0.55f);
        if (Mathf.Abs(x - Pos.x) > ajustes.alcanceEstocada && Mathf.Sign(llegada - Pos.x) == dir)
        {
            // Dash attack hasta alcanzarte (el rastro queda detras, como en el dibujo).
            cuerpoAnim.Pose("estocada", 1);
            cuerpoAnim.ColorEfecto(ajustes.colorNormal);
            Sonar("dash", 0.9f);
            cuerpo.Trayecto(new Vector2(llegada, suelo), Mathf.Abs(llegada - Pos.x) / Mathf.Max(1f, ajustes.velocidadDashParry), 0f, CuerpoCazadora.Curva.Lineal);
            float siguiente = 0f;
            while (cuerpo.EnTrayecto)
            {
                if (Time.time >= siguiente) { siguiente = Time.time + ajustes.cadaEstela; Estela(); }
                yield return null;
            }
        }

        // La estocada: la caja va DELANTE de ella, hacia ti (nunca hacia atras).
        enGolpe = true;
        cuerpoAnim.Pose("estocada", 1);
        cuerpoAnim.ColorEfecto(ajustes.colorNormal);
        Sonar("tajo", 1f, 0.9f);
        Estela();
        Vector2 centro = new Vector2(Pos.x + dir * (ajustes.alcanceEstocada * 0.5f + 0.2f), suelo + 0.75f * Tam);
        Vector2 tam = new Vector2(ajustes.alcanceEstocada + 1f, 1.6f * Tam);
        if (DepuracionCazadora.VerCajas) CajaDebug.Mostrar(centro, tam, Color.red, 0.2f);
        var r = GolpeCazadora.Caja(centro, tam, Dano(ajustes.danoEstocadaParry), this, PlayerControler.TipoDano.Fisico, EstadoPlayer.Ninguno, 0f, false, true);
        enGolpe = false;
        UICazadora.Latido(false);
        if (r.HasValue && r.Value == PlayerControler.ResultadoDano.Recibido)
        {
            CamaraCazadora.Sacudir(ajustes.temblorPesado);
            CamaraCazadora.Congelar(ajustes.congeladoPesado);
            // Si sobrevive, sale despedido (la fisica lo para en las paredes).
            if (p != null && p.VidaActual > 0)
            {
                Rigidbody2D rbp = p.GetComponent<Rigidbody2D>();
                float g = Mathf.Abs(Physics2D.gravity.y * (rbp != null ? rbp.gravityScale : 1f));
                float vuelo = Mathf.Max(0.2f, 2f * ajustes.empujonAltura / Mathf.Max(1f, g));
                p.Derribar(new Vector2(dir * ajustes.empujonDistancia / vuelo, ajustes.empujonAltura), ajustes.invulnerableAlCaer);
            }
        }
        cuerpoAnim.Reproducir("estocada", 1f, 2);
        for (float t = 0f; t < ajustes.recuperacionTrasEstocada; t += Time.deltaTime)
        {
            if (cuerpoAnim.Terminado && cuerpoAnim.Actual != "quieto") cuerpoAnim.Reproducir("quieto");
            yield return null;
        }
        FinEstocada();
    }

    private void FinEstocada()
    {
        oido.Sordo = false;
        enAviso = enGolpe = false;
        UICazadora.Latido(false);
        if (cuerpoAnim.Actual != "quieto") cuerpoAnim.Reproducir("quieto");
    }

    // ------------------------------------------------------------------ Rugido y escudo

    // Una vez por barra, con poca vida: ruge (empuja sin dano, con limite), se
    // aleja un poco hacia el lado con sitio y se cura dentro de un escudo de luz.
    // El escudo se rompe a golpes (10, o 5 con la espada imbuida de oscuridad):
    // si lo rompes, la cura se cancela y queda aturdida con dano extra.
    private IEnumerator RugidoYEscudo()
    {
        escudoUsado[fase] = true;
        AjustesCazadora.Escudo es = ajustes.EscudoN(fase);
        PlayerControler p = PC;

        // Rugido: empuja (sin dano) hasta cierta distancia, nunca contra la pared.
        cuerpo.Parar(ajustes.frenada * 2f);
        cuerpoAnim.Reproducir("quieto");
        cuerpoAnim.Temblar(0.035f);
        Sonar("rugido", 1f, 1.1f);
        CamaraCazadora.Sacudir(ajustes.temblorRugido);
        PolvoCazadora.Soltar(Pos + Vector2.up * 0.8f, new Color(0.8f, 0.9f, 0.8f, 0.8f), 30, 3f);
        for (float t = 0f; t < 0.6f; t += Time.deltaTime)
        {
            if (p != null)
            {
                float px = p.transform.position.x;
                float dir = Mathf.Sign(px - Pos.x);
                bool cerca = Mathf.Abs(px - Pos.x) < ajustes.empujeMaximoRugido;
                bool pared = px < arena.xMin + 1.5f || px > arena.xMax - 1.5f;
                if (cerca && !pared) p.AplicarViento(new Vector2(dir * ajustes.empujeRugido * (1f - t / 0.6f), 0f));
            }
            yield return null;
        }
        cuerpoAnim.Temblar(0f);

        // Se aleja un poco, hacia el lado con sitio.
        float destino = Pos.x;
        if (p != null)
        {
            float px = p.transform.position.x;
            int lado = Pos.x >= px ? 1 : -1;
            float d = px + lado * ajustes.distanciaEscudo;
            if (d > cuerpo.XMax - 0.5f || d < cuerpo.XMin + 0.5f) { lado = -lado; d = px + lado * ajustes.distanciaEscudo; }
            destino = cuerpo.Limitar(d);
        }
        if (Mathf.Abs(destino - Pos.x) > 0.3f)
        {
            MirarA(destino);
            cuerpoAnim.Pose("cruce", 2);
            yield return Dash(destino, 0.35f);
        }
        if (p != null) MirarA(p.transform.position.x);

        // Se cura dentro del escudo.
        escudoActivo = true;
        escudoRoto = false;
        golpesEscudo = 0f;
        golpesEscudoMax = Mathf.Max(1, es.golpes);
        pesoGolpeOscuro = es.golpesOscuridad > 0 ? (float)es.golpes / es.golpesOscuridad : 1f;
        if (burbuja != null) { burbuja.enabled = true; burbuja.color = new Color(1f, 0.95f, 0.7f, 0.35f); }
        MostrarEscudo(true);
        Sonar("escudo", 0.9f);
        cuerpoAnim.Reproducir("quieto");
        cuerpoAnim.Temblar(0.01f);
        float siguienteOla = 0.5f, siguienteCaida = es.inicioCaidas, siguienteLuz = 0f;
        PrepararElementos();
        // Se cura poco a poco mientras dura: empieza casi sin curarse y va
        // acelerando (premia romperlo pronto); si aguanta entero, suma "cura".
        // Si le rompes el escudo deja de curarse, pero no pierde lo ya curado.
        float curaTotal = salud.MaxHealth * es.cura, pendiente = 0f;
        int curado = 0;
        float t2 = 0f;
        while (t2 < es.tiempo && escudoActivo)
        {
            float duracion = Mathf.Max(0.1f, es.tiempo);
            pendiente += curaTotal * 2f * (t2 / duracion) * DtReal / duracion;
            if (pendiente >= 1f && salud.CurrentHealth < salud.MaxHealth)
            {
                int n = Mathf.FloorToInt(pendiente);
                pendiente -= n;
                curado += n;
                salud.Curar(n);
                if (Random.value < 0.35f) PolvoCazadora.Soltar(Pos + new Vector2(Random.Range(-0.4f, 0.4f), 0.4f) * Tam, new Color(1f, 0.85f, 0.45f, 1f), 1, 0.35f);
            }
            // La camara se abre para verte a ti, a ella y al escudo.
            CamaraCazadora.Ampliar(ajustes.zoomEscudo, 0.25f);
            if (Time.time >= siguienteLuz)
            {
                siguienteLuz = Time.time + 0.14f;
                PolvoCazadora.Soltar(Pos + new Vector2(Random.Range(-0.6f, 0.6f), 0.2f), new Color(1f, 0.92f, 0.6f, 0.8f), 2, 0.5f);
            }
            if (t2 >= siguienteOla)
            {
                siguienteOla += es.cadaOla;
                int dir = p != null && p.transform.position.x > Pos.x ? 1 : -1;
                Mirar(dir);
                LanzarOla(dir);
                if (es.olasDosLados) LanzarOla(-dir);
                cuerpoAnim.Reproducir("barrido", 1f);
            }
            if (es.ilusionesCaen && p != null && t2 >= siguienteCaida)
            {
                siguienteCaida = t2 + es.cadaCaida;
                CamaraCazadora.Subir(ajustes.subidaIlusiones, 1.2f);
                float baseX = p.transform.position.x + p.Velocidad.x * 0.3f;
                for (int i = 0; i < Mathf.Max(1, es.ilusionesPorCaida); i++)
                {
                    float x = baseX + (i == 0 ? 0f : (i % 2 == 1 ? 1.6f : -1.6f) * ((i + 1) / 2));
                    StartCoroutine(CaeIlusion(cuerpo.Limitar(x), Aviso(ajustes.ilusionCae, true)));
                }
            }
            if (cuerpoAnim.Terminado) cuerpoAnim.Reproducir("quieto");
            t2 += DtReal;
            yield return null;
        }
        cuerpoAnim.Temblar(0f);
        MostrarEscudo(false);
        if (escudoRoto)
        {
            yield return Aturdida(es.aturdida, "¡Escudo roto!", true);
            yield break;
        }
        escudoActivo = false;
        if (burbuja != null) burbuja.enabled = false;
        // Ya se curo durante el escudo: aqui solo el aviso de lo que gano.
        Sonar("curar", 0.9f);
        TextoFlotante.Mostrar("+" + curado, Pos + Vector2.up * 2.1f * Tam, new Color(1f, 0.9f, 0.55f), 1.1f);
        PolvoCazadora.Soltar(Pos + Vector2.up * 0.8f, new Color(1f, 0.92f, 0.6f, 1f), 30, 1.6f);
        yield return Pausa(0.4f);
    }

    // Una ola suya que le devolviste con un parry. True si la toca (y se deshace).
    //   - Con escudo: le quita 1 golpe.
    //   - En guardia: se deshace sin efecto (no es un golpe tuyo: no te castiga).
    //   - Si no: le hace dano (danoOlaDevuelta de su barra).
    public int OlasDevueltas { get; private set; }
    public int IlusionesParadasEscudo { get; private set; }
    public int OlasParadasEscudo { get; private set; }
    public bool RecibirOlaDevuelta(Vector2 centro, Vector2 tam)
    {
        if (muertaDelTodo) return false;
        Collider2D col = GetComponent<Collider2D>();
        Bounds b = col != null ? col.bounds : new Bounds(Pos + Vector2.up * 0.8f * Tam, new Vector3(0.9f, 1.7f, 0f) * Tam);
        Rect ola = new Rect(centro - tam * 0.5f, tam);
        if (!ola.Overlaps(Rect.MinMaxRect(b.min.x, b.min.y, b.max.x, b.max.y))) return false;
        if (cuerpoAnim.AlfaActual < 0.2f) return false;   // fuera de la vista (saltando, fundida): pasa de largo
        OlasDevueltas++;
        Vector2 punto = new Vector2(Mathf.Clamp(centro.x, b.min.x, b.max.x), b.min.y + 0.4f);
        PolvoCazadora.Soltar(punto, new Color(1f, 0.95f, 0.75f, 1f), 12, 1.2f);
        if (escudoActivo) { RestarEscudo(1f, punto); return true; }
        if (enGuardia || invulnerable || transicion) { Sonar("guardia", 0.5f, 1.3f); return true; }
        int dano = Mathf.Max(1, Mathf.RoundToInt(salud.MaxHealth * ajustes.danoOlaDevuelta));
        salud.TakeDamage(dano, punto, 0f);
        Sonar("tajo", 0.6f, 1.3f);
        return true;
    }

    // Quita golpes al escudo sin ser un golpe de espada (ola devuelta, parry a una
    // ilusion que cae): mismo aviso visual que un golpe normal.
    private void RestarEscudo(float cantidad, Vector2 punto)
    {
        if (!escudoActivo) return;
        golpesEscudo += cantidad;
        Sonar("escudo_golpe", 0.8f, 1.15f);
        PolvoCazadora.Soltar(punto, new Color(1f, 0.95f, 0.75f, 1f), 8, 1f);
        if (burbuja != null) StartCoroutine(DestelloBurbuja(new Color(1f, 1f, 1f, 0.6f)));
        ActualizarEscudo();
        if (golpesEscudo >= golpesEscudoMax - 0.001f) RomperEscudo();
    }

    // Un golpe tuyo contra el escudo (desde Modificar). Cuenta 1 por golpe que
    // conecta, haga el dano que haga; dos en el mismo fotograma cuentan 1. Con la
    // espada imbuida de oscuridad cuenta mas (se rompe con la mitad).
    private void GolpeAlEscudo(Elemento elemento)
    {
        if (!escudoActivo || Time.frameCount == frameGolpeEscudo) return;
        frameGolpeEscudo = Time.frameCount;
        bool oscuro = elemento == Elemento.Oscuro;
        if (!oscuro)
        {
            ArmaImbuida arma = PC != null ? PC.GetComponent<ArmaImbuida>() : null;
            oscuro = arma != null && arma.Activo == Elemento.Oscuro;
        }
        golpesEscudo += oscuro ? pesoGolpeOscuro : 1f;
        Vector2 impacto = PC != null ? Vector2.Lerp(Pos + Vector2.up * 0.8f, (Vector2)PC.transform.position, 0.45f) : Pos;
        if (oscuro)
        {
            Sonar("escudo_golpe_oscuro", 0.9f, 0.75f);
            PolvoCazadora.Soltar(impacto, new Color(0.35f, 0.1f, 0.5f, 1f), 14, 1.4f);
            if (burbuja != null) StartCoroutine(DestelloBurbuja(new Color(0.4f, 0.15f, 0.6f, 0.75f)));
        }
        else
        {
            Sonar("escudo_golpe", 0.7f, Random.Range(0.95f, 1.08f));
            PolvoCazadora.Soltar(impacto, new Color(1f, 0.95f, 0.75f, 1f), 6, 1f);
            if (burbuja != null) StartCoroutine(DestelloBurbuja(new Color(1f, 1f, 1f, 0.6f)));
        }
        ActualizarEscudo();
        if (golpesEscudo >= golpesEscudoMax - 0.001f) RomperEscudo();
    }

    private void RomperEscudo()
    {
        if (!escudoActivo) return;
        escudoActivo = false;
        escudoRoto = true;
        if (burbuja != null) burbuja.enabled = false;
        MostrarEscudo(false);
        Sonar("escudo_roto", 1f);
        PolvoCazadora.Soltar(Pos + Vector2.up * 0.9f, new Color(1f, 0.95f, 0.7f, 1f), 40, 3f);
        CamaraCazadora.Sacudir(ajustes.temblorPesado);
        // Camara lenta breve con un acercamiento hacia el escudo.
        CamaraCazadora.Lenta(ajustes.lentaRotura, ajustes.lentaRoturaTiempo);
        CamaraCazadora.Acercar(ajustes.zoomRotura, ajustes.lentaRoturaTiempo + 0.3f);
        CamaraDinamica.Encuadrar(Pos + Vector2.up * 0.8f, 0.6f, ajustes.lentaRoturaTiempo + 0.3f, ajustes.apartamientoMaximo);
        bonusDano = ajustes.EscudoN(fase).bonusDano;
    }

    private IEnumerator DestelloBurbuja(Color c)
    {
        if (burbuja == null) yield break;
        burbuja.color = c;
        for (float t = 0f; t < 0.08f; t += DtReal) yield return null;
        if (burbuja != null) burbuja.color = new Color(1f, 0.95f, 0.7f, 0.35f);
    }

    // ------------------------------------------------------------------ Grietas y marcas del escudo

    private SpriteRenderer grietas;
    private readonly List<SpriteRenderer> marcasEscudo = new List<SpriteRenderer>();
    private static Sprite[] spritesGrietas;

    private void MostrarEscudo(bool on)
    {
        if (on && grietas == null && burbuja != null)
        {
            grietas = new GameObject("Grietas").AddComponent<SpriteRenderer>();
            grietas.transform.SetParent(burbuja.transform, false);
            grietas.sharedMaterial = EfectoVisual.MaterialSinLuz();
            grietas.sortingLayerName = "VFX";
            grietas.sortingOrder = burbuja.sortingOrder + 1;
        }
        if (grietas != null) grietas.enabled = on;
        for (int i = marcasEscudo.Count; on && i < golpesEscudoMax; i++)
        {
            SpriteRenderer m = new GameObject("Marca").AddComponent<SpriteRenderer>();
            m.transform.SetParent(transform, false);
            m.sprite = DibujosCazadora.Pixel();
            m.sharedMaterial = EfectoVisual.MaterialSinLuz();
            m.sortingLayerName = "VFX";
            m.sortingOrder = 45;
            marcasEscudo.Add(m);
        }
        for (int i = 0; i < marcasEscudo.Count; i++) marcasEscudo[i].enabled = on && i < golpesEscudoMax;
        if (on) ActualizarEscudo();
    }

    // Grietas segun el progreso y una fila de puntos: los que quedan, encendidos.
    private void ActualizarEscudo()
    {
        float progreso = Mathf.Clamp01(golpesEscudo / Mathf.Max(1, golpesEscudoMax));
        if (grietas != null) grietas.sprite = SpriteGrietas(Mathf.FloorToInt(progreso * 10f));
        int hechos = Mathf.FloorToInt(golpesEscudo + 0.001f);
        float ancho = (golpesEscudoMax - 1) * 0.2f;
        for (int i = 0; i < golpesEscudoMax && i < marcasEscudo.Count; i++)
        {
            SpriteRenderer m = marcasEscudo[i];
            m.transform.localPosition = new Vector3(-ancho * 0.5f + i * 0.2f, 2.45f, 0f);
            m.transform.localScale = new Vector3(0.1f, 0.1f, 1f);
            bool queda = i >= hechos;
            m.color = queda ? new Color(1f, 0.93f, 0.65f, 0.95f) : new Color(0.3f, 0.28f, 0.25f, 0.5f);
        }
    }

    public string ContadorEscudo => escudoActivo ? $"{golpesEscudo:0.#}/{golpesEscudoMax}" : "-";

    // 11 dibujos de grietas (0 = nada, 10 = a punto de romperse), hechos una vez.
    private static Sprite SpriteGrietas(int nivel)
    {
        if (spritesGrietas == null)
        {
            spritesGrietas = new Sprite[11];
            const int n = 48;
            System.Random azar = new System.Random(7);
            // Diez grietas: de un punto cercano al centro hacia el borde, en zigzag.
            var lineas = new List<List<Vector2Int>>();
            for (int k = 0; k < 10; k++)
            {
                var puntos = new List<Vector2Int>();
                double ang = k * Mathf.PI * 2f / 10f + azar.NextDouble() * 0.5;
                Vector2 p = new Vector2(n / 2f, n / 2f) + new Vector2((float)System.Math.Cos(ang), (float)System.Math.Sin(ang)) * 4f;
                for (int s = 0; s < 6; s++)
                {
                    puntos.Add(new Vector2Int(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y)));
                    ang += (azar.NextDouble() - 0.5) * 0.9;
                    p += new Vector2((float)System.Math.Cos(ang), (float)System.Math.Sin(ang)) * 3.4f;
                }
                lineas.Add(puntos);
            }
            for (int nv = 0; nv <= 10; nv++)
            {
                Texture2D t = new Texture2D(n, n, TextureFormat.RGBA32, false);
                t.filterMode = FilterMode.Point;
                Color[] px = new Color[n * n];
                for (int k = 0; k < nv; k++)
                {
                    List<Vector2Int> l = lineas[k];
                    for (int s = 0; s + 1 < l.Count; s++) Linea(px, n, l[s], l[s + 1]);
                }
                t.SetPixels(px);
                t.Apply();
                spritesGrietas[nv] = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
            }
        }
        return spritesGrietas[Mathf.Clamp(nivel, 0, 10)];
    }

    private static void Linea(Color[] px, int n, Vector2Int a, Vector2Int b)
    {
        int pasos = Mathf.Max(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y), 1);
        for (int i = 0; i <= pasos; i++)
        {
            int x = Mathf.RoundToInt(Mathf.Lerp(a.x, b.x, (float)i / pasos));
            int y = Mathf.RoundToInt(Mathf.Lerp(a.y, b.y, (float)i / pasos));
            if (x >= 0 && x < n && y >= 0 && y < n) px[y * n + x] = new Color(1f, 1f, 1f, 0.95f);
        }
    }

    // ------------------------------------------------------------------ Instakills

    // Uno por fase, nunca dos a la vez, nunca con el player aturdido o recien
    // reaparecido, y solo cuando a la barra le queda poco.
    private bool PuedeInstakill(float vida)
    {
        if (instakillUsado[fase] || escudoActivo) return false;
        if (vida > ajustes.umbralInstakill) return false;
        PlayerControler p = PC;
        if (p == null || p.Aturdido || p.VidaActual <= 0) return false;
        if (Time.time - inicioCombate < ajustes.esperaTrasReaparecer) return false;
        return Random.value < 0.35f || vida < ajustes.umbralInstakill * 0.6f;
    }

    private IEnumerator Instakill()
    {
        instakillUsado[fase] = true;
        switch (fase)
        {
            case 0: yield return Ejecucion(); break;
            case 1: yield return Sentencia(); break;
            default: yield return ElSilencio(); break;
        }
    }

    private IEnumerator Expuesta()
    {
        yield return Aturdida(ajustes.expuestaTrasInstakill, "Expuesta", false);
    }

    // INSTAKILL 1, Ejecucion: una linea roja cruza el suelo de punta a punta y
    // ella la recorre con el dash especial. Se esquiva saltando o rodando.
    private IEnumerator Ejecucion()
    {
        instakillUsado[0] = true;
        elementoAtaque = Elemento.Ninguno;
        float lado = Pos.x < arena.center.x ? cuerpo.XMin : cuerpo.XMax;
        MirarA(lado);
        cuerpoAnim.Pose("cruce", 1);
        yield return Dash(lado, 0.35f);
        int dir = lado < arena.center.x ? 1 : -1;
        Mirar(dir);
        float otro = dir > 0 ? cuerpo.XMax : cuerpo.XMin;

        AlAvisoInstakill?.Invoke(true);
        AnunciarAtaque("ejecucion");
        MarcaSuelo linea = MarcaSuelo.Poner(MarcaSuelo.Forma.Franja, new Vector2((cuerpo.XMin + cuerpo.XMax) * 0.5f, suelo + 0.07f),
                                            new Vector2(cuerpo.XMax - cuerpo.XMin + 1.6f, 0.15f), ajustes.colorInstakill, ajustes.avisoEjecucion + 0.3f);
        Sonar("aviso_ejecucion", 1f);
        CamaraCazadora.Acercar(ajustes.zoomInstakill, ajustes.avisoEjecucion * 0.7f);
        cuerpoAnim.Pose("cruce", 0);
        // El aviso no cambia nunca (ni en Dificil ni por fase).
        for (float t = 0f; t < ajustes.avisoEjecucion; t += Time.deltaTime)
        {
            cuerpoAnim.Destello(ajustes.colorInstakill, 0.4f + 0.3f * Mathf.Sin(t * 22f), 0.05f);
            yield return null;
        }
        CamaraCazadora.Abrir(1.2f);
        yield return CruceCuerpo(otro, Mathf.Abs(otro - Pos.x) / Mathf.Max(5f, ajustes.velocidadEjecucion), 99999, true, true, true);
        if (linea != null && linea.Activa) linea.Quitar();
        AlAvisoInstakill?.Invoke(false);
        yield return Expuesta();
    }

    // INSTAKILL 2, Sentencia del Cazador: una marca roja te sigue, se fija con un
    // sonido y un tajo gigante cae del cielo sobre ella. Se esquiva saliendo de
    // la marca. Hay ilusiones alrededor, pero la marca siempre se ve encima.
    private IEnumerator Sentencia()
    {
        instakillUsado[1] = true;
        PlayerControler p = PC;
        if (p == null) yield break;
        elementoAtaque = Elemento.Ninguno;
        AlAvisoInstakill?.Invoke(true);
        AnunciarAtaque("sentencia");
        Sonar("salto", 0.8f);
        cuerpoAnim.Reproducir("salto", 1.5f);
        cuerpo.Trayecto(new Vector2(Pos.x, suelo + 8f), 0.3f, 0f, CuerpoCazadora.Curva.Suave);
        yield return Fundido(0f, 0.3f);

        var confusas = new List<IlusionCazadora>();
        float[] desvios = { -5f, 5f, -8f };
        foreach (float d in desvios)
        {
            IlusionCazadora il = SacarIlusion();
            if (il == null) continue;
            il.Aparecer(this, new Vector2(cuerpo.Limitar(p.transform.position.x + d), suelo), d > 0 ? -1 : 1, ajustes.colorIlusion, false,
                        ajustes.seguimientoSentencia + ajustes.fijoSentencia + 0.5f, Oscuridad);
            il.anim.Pose("tajoArriba", 0);
            confusas.Add(il);
        }

        MarcaSuelo marca = MarcaSuelo.Poner(MarcaSuelo.Forma.Aro, new Vector2(p.transform.position.x, suelo + 0.1f),
                                            new Vector2(ajustes.anchoSentencia, 0f), ajustes.colorInstakill, -1f);
        marca.Seguir(p.transform);
        Sonar("aviso_sentencia", 1f);
        CamaraCazadora.Acercar(ajustes.zoomInstakill, ajustes.seguimientoSentencia);
        yield return Esperar(ajustes.seguimientoSentencia);
        marca.Fijar();
        Sonar("sentencia_fija", 1f);
        yield return Esperar(ajustes.fijoSentencia);

        Vector2 donde = marca.Posicion;
        marca.Quitar();
        SpritesCazadora.Clip c = cuerpoAnim.hojas.Buscar("tajoAbajo");
        Rect caja = c.Caja(1);
        float esc = ajustes.anchoSentencia / Mathf.Max(0.1f, caja.width);
        TajoAparecido.Lanzar(c.efecto[1], caja, new Vector2(donde.x - caja.center.x * esc, suelo - caja.yMin * esc), 1, esc,
                             Color.Lerp(ajustes.colorInstakill, Color.white, 0.35f), 99999, true, false, 7f);
        Sonar("tajo_gigante", 1f);
        yield return Esperar(0.1f);
        CamaraCazadora.Abrir(1f);
        CamaraCazadora.Sacudir(ajustes.temblorPesado * 1.3f);
        cuerpo.Colocar(new Vector2(donde.x, suelo));
        cuerpoAnim.Alfa(1f);
        cuerpoAnim.Pose("tajoAbajo", 2);
        PolvoCazadora.Soltar(new Vector2(donde.x, suelo + 0.1f), new Color(0.9f, 0.8f, 0.75f, 0.9f), 26, 2f, false);
        foreach (IlusionCazadora il in confusas) il.Deshacer();
        AlAvisoInstakill?.Invoke(false);
        yield return Expuesta();
    }

    // INSTAKILL 3, El Silencio: durante unos segundos cualquier ruido (atacar,
    // rodar, beber, cambiar de imbuicion o correr) te delata y te ejecuta. Si te
    // quedas quieto (andar vale), pierde tu rastro y queda vulnerable.
    private IEnumerator ElSilencio()
    {
        instakillUsado[2] = true;
        elementoAtaque = Elemento.Ninguno;
        AlSilencioCaza?.Invoke(true);
        AnunciarAtaque("silencio");
        Sonar("aviso_silencio", 1f);
        cuerpo.Parar(ajustes.frenada);
        cuerpoAnim.Pose("tajoArriba", 0);
        yield return Esperar(0.5f);

        bool delatado = false;
        Vector2 dondeRuido = Vector2.zero;
        System.Action<OidoCazadora.Tipo, Vector2> oir = (tipo, pos) =>
        {
            if (tipo == OidoCazadora.Tipo.Atacar || tipo == OidoCazadora.Tipo.Rodar || tipo == OidoCazadora.Tipo.Frasco ||
                tipo == OidoCazadora.Tipo.Imbuir || tipo == OidoCazadora.Tipo.Correr)
            { delatado = true; dondeRuido = pos; }
        };
        oido.Atento = true;
        oido.AlOir += oir;
        int dir = Random.value < 0.5f ? -1 : 1;
        float cambio = 0f;
        for (float t = 0f; t < ajustes.duracionSilencio && !delatado; t += Time.deltaTime)
        {
            if (t >= cambio) { cambio = t + Random.Range(0.8f, 1.4f); dir = -dir; Mirar(dir); }
            cuerpo.Andar(dir * ajustes.velocidadCorrer * 0.22f, ajustes.aceleracion, ajustes.frenada);
            if (cuerpoAnim.Actual != "correr") cuerpoAnim.Reproducir("correr", 0.4f);
            yield return null;
        }
        oido.AlOir -= oir;
        oido.Atento = false;
        cuerpo.Parar(ajustes.frenada);

        if (!delatado)
        {
            AlSilencioCaza?.Invoke(false);
            TextoFlotante.Mostrar("...", Pos + Vector2.up * 2f * Tam, new Color(0.8f, 0.9f, 1f), 1f);
            yield return Aturdida(ajustes.expuestaTrasInstakill + 1f, "Perdió tu rastro", false);
            yield break;
        }

        // Te delataste: ejecucion.
        Sonar("silencio_delatado", 1f);
        MirarA(dondeRuido.x);
        cuerpoAnim.Pose("luna", 0);
        for (float t = 0f; t < 0.3f; t += Time.deltaTime)
        {
            cuerpoAnim.Destello(ajustes.colorInstakill, 0.6f, 0.05f);
            yield return null;
        }
        cuerpo.Trayecto(new Vector2(dondeRuido.x - mirada * 0.6f, suelo), 0.14f, 0f, CuerpoCazadora.Curva.Dash);
        Estela();
        yield return Golpe("luna", 0.14f, 99999, PlayerControler.TipoDano.Fisico, 1.6f, true, false);
        AlSilencioCaza?.Invoke(false);
        yield return Recuperar(ajustes.danzaFinal, true);
    }

    // ------------------------------------------------------------------ Barras y muerte

    // Muerte falsa entre barras: se deshace en polvo, se reconstruye y se levanta
    // con la barra nueva. Tu eres invulnerable mientras dura (4-5 s).
    private IEnumerator MuerteFalsa()
    {
        transicion = true;
        invulnerable = true;
        int barra = fase;
        PlayerControler p = PC;
        if (p != null) p.InvulnerableExterno = true;
        QuitarIlusiones();
        PoolCazadora.Limpiar();
        escudoActivo = false;
        if (burbuja != null) burbuja.enabled = false;
        resistido = Elemento.Ninguno;
        bonusActivo = false;
        cuerpoAnim.Alfa(1f);
        cuerpoAnim.ColorEfecto(ajustes.colorNormal);

        AlMuerteFalsa?.Invoke(barra);
        AlFrase?.Invoke(barra == 0 ? ajustes.finBarra1 : ajustes.finBarra2);
        Sonar("muerte_falsa", 1f);
        cuerpoAnim.Reproducir("muerte", 1f);
        yield return HastaTerminar(2f);
        PolvoCazadora.Soltar(Pos + Vector2.up * 0.3f, new Color(0.25f, 0.28f, 0.2f, 0.9f), 34, 1.2f);
        yield return Esperar(0.7f);

        Sonar("reconstruir", 1f);
        PolvoCazadora.Soltar(Pos + Vector2.up * 0.2f, new Color(0.7f, 0.8f, 0.6f, 0.9f), 30, 0.8f);
        cuerpoAnim.ReproducirAlReves("muerte", 0.9f);
        yield return HastaTerminar(2f);

        fase = barra + 1;
        curadoEnBarra = 0f;
        salud.Revivir(VidaDesafio(F.vida));
        AlNuevaFase?.Invoke(fase);

        cuerpoAnim.Reproducir("quieto");
        cuerpoAnim.Temblar(0.03f);
        Sonar("rugido", 0.8f, 1.1f);
        CamaraCazadora.Sacudir(ajustes.temblorRugido * 0.7f);
        yield return Esperar(0.6f);
        cuerpoAnim.Temblar(0f);

        invulnerable = false;
        transicion = false;
        if (p != null) p.InvulnerableExterno = false;
        golpesRecibidos.Clear();
        Pensar();
    }

    private IEnumerator HastaTerminar(float tope)
    {
        for (float t = 0f; t < tope && !cuerpoAnim.Terminado; t += Time.deltaTime) yield return null;
    }

    private IEnumerator MuerteReal()
    {
        PlayerControler p = PC;
        if (p != null) p.InvulnerableExterno = true;
        PoolCazadora.Limpiar();
        if (burbuja != null) burbuja.enabled = false;
        cuerpoAnim.Alfa(1f);
        AlFrase?.Invoke(ajustes.muerteFinal);
        AlMuerteReal?.Invoke();
        Sonar("muerte_real", 1f);
        cuerpoAnim.Reproducir("muerte", 0.75f);
        yield return HastaTerminar(3f);
        PolvoCazadora.Soltar(Pos + Vector2.up * 0.3f, new Color(0.25f, 0.28f, 0.2f, 0.9f), 40, 1f);
        AvisarDerrota();
    }
}
