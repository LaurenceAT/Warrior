using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Ataques de la Cazadora (fases 1, 2 y 3). Todos siguen el mismo esquema: aviso
// (pose quieta con brillo), golpe (caja sincronizada con el tajo) y recuperacion
// (ventana de castigo). Los numeros estan en AjustesCazadora.
public partial class JefeCazadora
{
    // ------------------------------------------------------------------ Ayudas de golpe

    private IEnumerator Recuperar(AjustesCazadora.Ataque a, bool grande)
    {
        cuerpoAnim.Velocidad(1f);
        float s = Recuperacion(a, grande);
        for (float t = 0f; t < s; t += Time.deltaTime)
        {
            if (cuerpoAnim.Terminado && cuerpoAnim.Actual != "quieto") cuerpoAnim.Reproducir("quieto");
            yield return null;
        }
        if (cuerpoAnim.Actual != "quieto") cuerpoAnim.Reproducir("quieto");
    }

    private bool ApuntaARuido()
    {
        PlayerControler p = PC;
        return p != null && Vector2.Distance(p.transform.position, Pos) > ajustes.rangoCercano;
    }

    private void LanzarOla(int dir, float escalaExtra = 1f)
    {
        SpritesCazadora.Clip c = cuerpoAnim.hojas.Buscar("barrido");
        if (c == null || c.efecto.Length == 0) return;
        float esc = escalaExtra * (elementoAtaque == Elemento.Sagrado ? ajustes.sagradoAncho : 1f);
        float vel = ajustes.velocidadOla * RapidezElemento * F.velocidadAtaque;
        Elemento e = elementoAtaque;
        OlaLuz.Lanzar(c.efecto[0], c.Caja(0), new Vector2(Pos.x + dir * 0.4f, suelo), dir, vel, ajustes.alcanceOla, Dano(ajustes.ola.dano),
                      ColorElemento(e), e, ajustes, arena.xMin + 0.4f, arena.xMax - 0.4f, noLetalAccion, r =>
                      {
                          if (r == PlayerControler.ResultadoDano.Recibido)
                          {
                              CamaraCazadora.Sacudir(ajustes.temblorLigero);
                              if (this != null && !muertaDelTodo) RobarVidaCon(e);
                          }
                          // Durante su escudo, parar una ola le quita 1 al momento (y otro
                          // mas cuando la ola devuelta le llega).
                          else if (r == PlayerControler.ResultadoDano.Parry && this != null && escudoActivo)
                          {
                              OlasParadasEscudo++;
                              RestarEscudo(1f, PC != null ? (Vector2)PC.transform.position + Vector2.up * 0.6f : Pos);
                          }
                      }, esc);
        Sonar("ola", 0.8f);
    }

    private void RobarVidaCon(Elemento e)
    {
        Elemento antes = elementoAtaque;
        elementoAtaque = e;
        RobarVida();
        elementoAtaque = antes;
    }

    // Cruza hasta "destino" con el dash especial, golpeando lo que barre por el
    // camino (una sola vez). "soloSuelo": la caja no pasa de la altura de un
    // salto (Ejecucion: se esquiva saltando).
    private IEnumerator CruceCuerpo(float destino, float segundos, int dano, bool imparable, bool letal, bool soloSuelo)
    {
        enGolpe = true;
        cuerpoAnim.Pose("cruce", 1);
        cuerpoAnim.ColorEfecto(letal ? ajustes.colorInstakill : ColorElemento(elementoAtaque));
        Sonar(letal ? "cruce_letal" : "dash", 0.9f);
        cuerpo.Trayecto(new Vector2(destino, suelo), Mathf.Max(0.08f, segundos), 0f, letal ? CuerpoCazadora.Curva.Lineal : CuerpoCazadora.Curva.Dash);
        ultimoResultado = null;
        bool pego = false;
        float anterior = Pos.x, siguiente = 0f, inicio = Time.time;
        // De donde viene: el parry cuenta ese lado aunque ya te haya atravesado.
        float desdeX = Pos.x;
        while (cuerpo.EnTrayecto)
        {
            yield return new WaitForFixedUpdate();
            // El cuadro 1 es el arranque (el trazo por delante); en marcha, el 2
            // (el cuerpo con el rastro detras), mas las estelas.
            if (Time.time - inicio > 0.06f && cuerpoAnim.Fotograma == 1) cuerpoAnim.Pose("cruce", 2);
            if (Time.time >= siguiente) { siguiente = Time.time + ajustes.cadaEstela; Estela(); }
            if (pego) { anterior = Pos.x; continue; }
            float x0 = Mathf.Min(anterior, Pos.x) - 0.45f, x1 = Mathf.Max(anterior, Pos.x) + 0.45f;
            float alto = soloSuelo ? 1.05f : 1.4f;
            Vector2 centro = new Vector2((x0 + x1) * 0.5f, suelo + alto * 0.5f);
            Vector2 tam = new Vector2(x1 - x0, alto);
            if (DepuracionCazadora.VerCajas) CajaDebug.Mostrar(centro, tam, Color.red, 0.03f);
            EstadoPlayer estado = letal ? EstadoPlayer.Ninguno : OlaLuz.EstadoDe(elementoAtaque);
            if (estado == EstadoPlayer.Sangrado) estado = EstadoPlayer.Ninguno;
            var r = GolpeCazadora.Caja(centro, tam, dano, this, PlayerControler.TipoDano.Fisico, estado, ajustes.acumulacionEstado, !letal && noLetalAccion, imparable, false, desdeX);
            anterior = Pos.x;
            if (!r.HasValue || r.Value == PlayerControler.ResultadoDano.Ignorado) continue;
            pego = true;
            ultimoResultado = r;
            TrasPegar(r.Value, dano);
            if (r.Value == PlayerControler.ResultadoDano.Parry) { cuerpo.CortarTrayecto(); break; }
        }
        enGolpe = false;
        cuerpoAnim.Reproducir("cruce", 1f, 2);
    }

    // Golpe de una ilusion (a la vez que ella). Hace dano como ella, pero si la
    // golpeas se deshace.
    private IEnumerator GolpeIlusion(IlusionCazadora il, string clip, float segundos, int dano)
    {
        SpritesCazadora.Clip c = cuerpoAnim.hojas.Buscar(clip);
        if (c == null || il == null) yield break;
        int a0 = Mathf.Max(0, c.PrimerActivo), a1 = Mathf.Max(a0, c.UltimoActivo);
        il.anim.Reproducir(clip, ((a1 - a0 + 1) / c.fps) / Mathf.Max(0.02f, segundos), a0);
        il.anim.ColorEfecto(Color.Lerp(ColorElemento(elementoAtaque), ajustes.colorIlusion, 0.4f));
        bool pego = false, listo = false;
        for (float t = 0f; il.Viva && t < segundos + 0.05f && il.anim.Fotograma <= a1; t += Time.deltaTime)
        {
            if (!pego && il.anim.CuadroActivo)
            {
                // Un fotograma de margen: si paras a la verdadera en ese instante, la
                // ilusion se deshace antes de tocarte.
                if (!listo) { listo = true; yield return null; if (!il.Viva) break; }
                Rect caja = c.Caja(il.anim.Fotograma);
                Vector2 centro = il.Posicion + new Vector2(il.Mirada * caja.center.x, caja.center.y);
                if (DepuracionCazadora.VerCajas) CajaDebug.Mostrar(centro, caja.size, Color.magenta, 0.03f);
                var r = GolpeCazadora.Caja(centro, caja.size, dano, il, PlayerControler.TipoDano.Fisico, EstadoPlayer.Ninguno, 0f, noLetalAccion);
                if (r.HasValue) { pego = true; if (r.Value == PlayerControler.ResultadoDano.Recibido) CamaraCazadora.Sacudir(ajustes.temblorLigero); }
            }
            yield return null;
        }
    }

    // Una ilusion que cruza la arena (dash) golpeando lo que barre.
    private IEnumerator CruceIlusion(IlusionCazadora il, float destino, float segundos, int dano)
    {
        if (il == null) yield break;
        il.anim.Pose("cruce", 1);
        il.anim.ColorEfecto(ajustes.colorIlusion);
        il.Mover(new Vector2(destino, suelo), segundos);
        bool pego = false;
        float anterior = il.Posicion.x, siguiente = 0f, desdeX = il.Posicion.x;
        while (il.Viva && il.Moviendose)
        {
            yield return null;
            if (Time.time >= siguiente)
            {
                siguiente = Time.time + ajustes.cadaEstela * 1.5f;
                EstelaFantasma.Lanzar(il.anim.SpriteCuerpo, il.anim.cuerpo.transform.position, il.anim.Volteado, new Color(0.55f, 0.75f, 1f, 0.35f), ajustes.vidaEstela);
            }
            if (pego) continue;
            float x0 = Mathf.Min(anterior, il.Posicion.x) - 0.45f, x1 = Mathf.Max(anterior, il.Posicion.x) + 0.45f;
            Vector2 centro = new Vector2((x0 + x1) * 0.5f, suelo + 0.7f);
            // El parry a la verdadera manda: si la ilusion llega un instante antes,
            // espera (hasta 0,12 s) a que ella resuelva su golpe; si la paras, se deshace.
            if (GolpeCazadora.PlayerEn(centro, new Vector2(x1 - x0, 1.4f)) != null)
            {
                float espera = 0f;
                do { yield return null; espera += Time.deltaTime; }
                while (il.Viva && ilusionesConElla.Contains(il) && enGolpe && espera < 0.12f);
                if (!il.Viva) break;
                x0 = Mathf.Min(x0, il.Posicion.x - 0.45f);
                x1 = Mathf.Max(x1, il.Posicion.x + 0.45f);
                centro = new Vector2((x0 + x1) * 0.5f, suelo + 0.7f);
            }
            var r = GolpeCazadora.Caja(centro, new Vector2(x1 - x0, 1.4f), dano, il, PlayerControler.TipoDano.Fisico, EstadoPlayer.Ninguno, 0f, noLetalAccion, false, false, desdeX);
            anterior = il.Posicion.x;
            if (r.HasValue) pego = true;
        }
        if (il.Viva) il.anim.Reproducir("cruce", 1f, 2);
    }

    // Aviso compartido: ella y sus ilusiones brillan a la vez.
    private IEnumerator AvisoConIlusiones(string clip, int cuadro, float segundos, bool pesado, List<IlusionCazadora> otras)
    {
        foreach (IlusionCazadora i in otras) if (i != null && i.Viva) i.anim.Pose(clip, cuadro);
        enAviso = true;
        cuerpoAnim.Pose(clip, cuadro);
        Color c = pesado ? new Color(1f, 0.55f, 0.25f) : new Color(1f, 0.92f, 0.7f);
        Sonar(pesado ? "aviso_pesado" : "aviso_ligero", 0.8f);
        for (float t = 0f; t < segundos; t += Time.deltaTime)
        {
            float f = (pesado ? 0.55f : 0.4f) * (0.4f + 0.6f * (0.5f + 0.5f * Mathf.Sin(t * 28f)));
            cuerpoAnim.Destello(c, f, 0.05f);
            foreach (IlusionCazadora i in otras) if (i != null && i.Viva) i.anim.Destello(c, f, 0.05f);
            yield return null;
        }
        enAviso = false;
    }

    // ------------------------------------------------------------------ Fase 1

    // Tres Lunas: barrido, luna y estocada seguidos. Hueco de castigo al final.
    private IEnumerator TresLunas()
    {
        PrepararElementos();
        yield return Acercarse(ajustes.distanciaCerca, 1.3f);
        bool aRuido = ApuntaARuido();
        Vector2 obj = oido.Objetivo(Pos);
        MirarA(obj.x);

        yield return AvisoPose("estocada", 0, Aviso(ajustes.barrido, false), false);
        cuerpo.Trayecto(Pos + Vector2.right * mirada * 0.7f, 0.1f, 0f, CuerpoCazadora.Curva.Dash);
        yield return Golpe("barrido", GolpeT(ajustes.barrido) / RapidezElemento, Dano(ajustes.barrido.dano));
        if (parado) yield break;
        yield return Esperar(ajustes.enlaceCombo);

        AlternarElemento(1);
        yield return AvisoPose("luna", 0, Aviso(ajustes.luna, false), false);
        cuerpo.Trayecto(Pos + Vector2.right * mirada * 0.6f, 0.1f, 0f, CuerpoCazadora.Curva.Dash);
        yield return Golpe("luna", GolpeT(ajustes.luna) / RapidezElemento, Dano(ajustes.luna.dano));
        if (parado) yield break;
        yield return Esperar(ajustes.enlaceCombo);

        AlternarElemento(2);
        yield return AvisoPose("estocada", 0, Aviso(ajustes.estocadaCombo, false), false);
        cuerpo.Trayecto(new Vector2(Pos.x + mirada * 2.8f, suelo), GolpeT(ajustes.estocadaCombo), 0f, CuerpoCazadora.Curva.Dash);
        yield return Golpe("estocada", GolpeT(ajustes.estocadaCombo) / RapidezElemento, Dano(ajustes.estocadaCombo.dano));
        if (parado) yield break;
        yield return Recuperar(ajustes.estocadaCombo, true);
        yield return ComprobarFallo(obj, aRuido);
    }

    // Cruce: un dash largo que te atraviesa y un tajo hacia atras al pasar.
    private IEnumerator Cruce()
    {
        PrepararElementos();
        bool aRuido = ApuntaARuido();
        Vector2 obj = oido.Objetivo(Pos);
        MirarA(obj.x);
        yield return AvisoPose("cruce", 0, Aviso(ajustes.cruce, true), true);
        float destino = cuerpo.Limitar(obj.x + mirada * ajustes.cruceDetras);
        CamaraCazadora.Abrir(0.6f);
        yield return CruceCuerpo(destino, GolpeT(ajustes.cruce) / RapidezElemento, Dano(ajustes.cruce.dano), false, false, false);
        if (parado) yield break;

        AlternarElemento(1);
        if (PC != null) Mirar(PC.transform.position.x > Pos.x ? 1 : -1);
        yield return AvisoPose("luna", 0, Aviso(ajustes.tajoAtras, false), false, null, false);
        yield return Golpe("luna", GolpeT(ajustes.tajoAtras) / RapidezElemento, Dano(ajustes.tajoAtras.dano));
        if (parado) yield break;
        yield return Recuperar(ajustes.tajoAtras, true);
        yield return ComprobarFallo(obj, aRuido);
    }

    // Ola de luz: el barrido sale como una ola baja por el suelo (se salta).
    // "adivina": sin rastro de ti, la lanza hacia donde este mirando.
    private IEnumerator OlaDeLuz(bool adivina = false)
    {
        PrepararElementos();
        if (!adivina) MirarA(oido.Objetivo(Pos).x);
        yield return AvisoPose("estocada", 0, Aviso(ajustes.ola, true), true, null, !adivina);
        LanzarOla(mirada);
        yield return Golpe("barrido", GolpeT(ajustes.ola), Dano(ajustes.ola.dano));
        if (parado) yield break;
        yield return Recuperar(ajustes.ola, false);
    }

    // Tajo ascendente: castiga al que salta encima de ella.
    private IEnumerator TajoAscendente()
    {
        PrepararElementos();
        if (PC != null) MirarA(PC.transform.position.x);
        yield return AvisoPose("tajoArriba", 0, Aviso(ajustes.tajoAscendente, false), false);
        yield return Golpe("tajoArriba", GolpeT(ajustes.tajoAscendente) / RapidezElemento, Dano(ajustes.tajoAscendente.dano));
        if (parado) yield break;
        yield return Recuperar(ajustes.tajoAscendente, true);
    }

    // Salto con tajo descendente: salta hacia ti y cae cortando.
    private IEnumerator SaltoDescendente()
    {
        PrepararElementos();
        bool aRuido = ApuntaARuido();
        Vector2 obj = oido.Objetivo(Pos);
        MirarA(obj.x);
        yield return AvisoPose("salto", 0, Aviso(ajustes.saltoDescendente, true), true);
        Sonar("salto", 0.8f);
        float dur = 0.62f / F.velocidadMovimiento;
        cuerpo.Trayecto(new Vector2(obj.x, suelo), dur, ajustes.alturaSalto, CuerpoCazadora.Curva.Suave);
        cuerpoAnim.Reproducir("salto", 1.5f);
        SpritesCazadora.Clip c = cuerpoAnim.hojas.Buscar("tajoAbajo");
        bool cayendo = false, cortando = false, pego = false;
        ultimoResultado = null;
        int dano = Dano(ajustes.saltoDescendente.dano);
        float t = 0f, siguiente = 0f;
        while (cuerpo.EnTrayecto)
        {
            t += Time.deltaTime;
            float u = t / dur;
            if (!cayendo && u > 0.35f) { cayendo = true; cuerpoAnim.Pose("tajoAbajo", 0); BrilloEspada(Color.white); }
            if (!cortando && u > 0.62f) { cortando = true; enGolpe = true; cuerpoAnim.Pose("tajoAbajo", 1); cuerpoAnim.ColorEfecto(ColorElemento(elementoAtaque)); Sonar("tajo", 0.9f); }
            if (Time.time >= siguiente) { siguiente = Time.time + ajustes.cadaEstela * 2f; Estela(); }
            if (cortando && !pego)
            {
                var r = GolpearCuadro(c, 1, dano, PlayerControler.TipoDano.Fisico, 1f, false, noLetalAccion, true);
                if (r.HasValue) { pego = true; ultimoResultado = r; TrasPegar(r.Value, dano); }
            }
            yield return null;
        }
        enGolpe = false;
        Aterrizaje();
        cuerpoAnim.Reproducir("tajoAbajo", 1f, 2);
        if (parado) yield break;
        yield return Recuperar(ajustes.saltoDescendente, true);
        yield return ComprobarFallo(obj, aRuido);
    }

    private void Aterrizaje()
    {
        Sonar("aterrizaje", 0.9f);
        CamaraCazadora.Sacudir(ajustes.temblorAterrizaje);
        PolvoCazadora.Soltar(new Vector2(Pos.x, suelo + 0.1f), new Color(0.6f, 0.62f, 0.5f, 0.8f), 16, 1.4f, false);
    }

    // ------------------------------------------------------------------ Fase 2

    // Contraataque elemental: copia tu imbuicion y la resiste unos segundos.
    private IEnumerator ContraElemental()
    {
        ArmaImbuida arma = PC != null ? PC.GetComponent<ArmaImbuida>() : null;
        if (arma == null || arma.Activo == Elemento.Ninguno) yield break;
        Elemento e = arma.Activo;
        Color c = ColorElemento(e);
        cuerpo.Parar(ajustes.frenada);
        cuerpoAnim.Reproducir("quieto");
        cuerpoAnim.Temblar(0.025f);
        Sonar("contra_elemento", 0.9f);
        float siguientePolvo = 0f;
        for (float t = 0f; t < 0.55f; t += Time.deltaTime)
        {
            cuerpoAnim.Destello(c, 0.35f + 0.35f * Mathf.Sin(t * 20f), 0.05f);
            if (t >= siguientePolvo) { siguientePolvo = t + 0.1f; PolvoCazadora.Soltar(Pos + new Vector2(Random.Range(-0.5f, 0.5f), 0.4f), c, 4, 0.8f); }
            yield return null;
        }
        cuerpoAnim.Temblar(0f);
        resistido = e;
        finResistencia = Time.time + ajustes.duracionResistencia;
        siguienteResistencia = Time.time + ajustes.enfriamientoResistencia;
        TextoFlotante.Mostrar("Resiste " + Elementos.Nombre(e), Pos + Vector2.up * 2.1f * Tam, c, 1f);
        cuerpoAnim.ColorEfecto(c);
    }

    // Flanqueo: ella a un lado, una ilusion al otro, y las dos cortan a la vez.
    // Variante: una a ras de suelo y la otra en el aire.
    private IEnumerator Flanqueo()
    {
        PlayerControler p = PC;
        if (p == null) yield break;
        PrepararElementos();
        Vector2 pp = p.transform.position;
        int lado = Random.value < 0.5f ? -1 : 1;
        bool alta = Random.value < 0.45f;
        float xa = cuerpo.Limitar(pp.x + lado * 2.4f), xb = cuerpo.Limitar(pp.x - lado * 2.4f);
        yield return Fundido(0f, 0.12f);
        cuerpo.Colocar(new Vector2(xa, suelo));
        MirarA(pp.x);
        yield return Fundido(1f, 0.12f);
        IlusionCazadora il = SacarIlusion();
        var otras = new List<IlusionCazadora>();
        if (il != null)
        {
            il.Aparecer(this, new Vector2(xb, suelo + (alta ? 1.5f : 0f)), xb < pp.x ? 1 : -1, ajustes.colorIlusion, false, 4f, Oscuridad);
            otras.Add(il);
        }
        yield return AvisoConIlusiones("estocada", 0, Aviso(ajustes.flanqueo, true), true, otras);
        ilusionesConElla.Clear();
        if (il != null && il.Viva) { ilusionesConElla.Add(il); StartCoroutine(GolpeIlusion(il, alta ? "luna" : "barrido", GolpeT(ajustes.flanqueo), Dano(ajustes.danoIlusion))); }
        yield return Golpe("barrido", GolpeT(ajustes.flanqueo), Dano(ajustes.flanqueo.dano));
        ilusionesConElla.Clear();
        if (il != null) il.Deshacer();
        if (parado) yield break;
        yield return Recuperar(ajustes.flanqueo, true);
    }

    // Ilusiones que caen del cielo con un tajo descendente, con una marca en el
    // suelo antes del impacto. La camara sube un poco para verlas llegar.
    private IEnumerator IlusionesCaen(int cuantas, bool duranteEscudo)
    {
        tIlusionesCaen = Time.time;
        PrepararElementos();
        CamaraCazadora.Subir(ajustes.subidaIlusiones, cuantas * 0.5f + 1.2f);
        if (!duranteEscudo) { cuerpoAnim.Pose("tajoArriba", 0); Sonar("invocar", 0.7f); }
        for (int i = 0; i < cuantas; i++)
        {
            PlayerControler p = PC;
            if (p == null) yield break;
            float x = cuerpo.Limitar(p.transform.position.x + p.Velocidad.x * 0.3f);
            StartCoroutine(CaeIlusion(x, Aviso(ajustes.ilusionCae, true)));
            yield return Esperar(0.45f);
        }
        if (!duranteEscudo) yield return Recuperar(ajustes.ilusionCae, false);
    }

    private IEnumerator CaeIlusion(float x, float aviso)
    {
        MarcaSuelo.Poner(MarcaSuelo.Forma.Franja, new Vector2(x, suelo + 0.07f), new Vector2(1.7f, 0.13f), new Color(0.7f, 0.85f, 1f, 0.9f), aviso);
        Sonar("marca", 0.6f);
        const float caida = 0.18f;
        yield return Esperar(Mathf.Max(0f, aviso - caida));
        IlusionCazadora il = SacarIlusion();
        if (il == null) yield break;
        il.Aparecer(this, new Vector2(x, suelo + 6.5f), PC != null && PC.transform.position.x < x ? -1 : 1, ajustes.colorIlusion, false, 1.2f, Oscuridad);
        il.anim.Pose("tajoAbajo", 1);
        il.anim.ColorEfecto(ajustes.colorIlusion);
        il.Mover(new Vector2(x, suelo), caida);
        yield return Esperar(caida);
        SpritesCazadora.Clip c = cuerpoAnim.hojas.Buscar("tajoAbajo");
        Rect caja = c.Caja(1);
        Vector2 centro = new Vector2(x + il.Mirada * caja.center.x, suelo + Mathf.Max(0.6f, caja.center.y));
        Vector2 tam = new Vector2(caja.width, Mathf.Max(1.2f, caja.height * 0.7f));
        if (DepuracionCazadora.VerCajas) CajaDebug.Mostrar(centro, tam, Color.magenta, 0.15f);
        var r = GolpeCazadora.Caja(centro, tam, Dano(ajustes.ilusionCae.dano), il, PlayerControler.TipoDano.Fisico, EstadoPlayer.Ninguno, 0f, noLetalAccion, false, true);
        if (r.HasValue && r.Value == PlayerControler.ResultadoDano.Recibido) CamaraCazadora.Sacudir(ajustes.temblorLigero);
        // Durante su escudo, parar una ilusion que cae le quita un golpe al escudo.
        if (r.HasValue && r.Value == PlayerControler.ResultadoDano.Parry && escudoActivo) { IlusionesParadasEscudo++; RestarEscudo(1f, new Vector2(x, suelo + 0.8f)); }
        CamaraCazadora.Sacudir(ajustes.temblorLigero * 0.6f);
        PolvoCazadora.Soltar(new Vector2(x, suelo + 0.1f), new Color(0.7f, 0.82f, 1f, 0.8f), 10, 1.2f, false);
        il.anim.Reproducir("tajoAbajo", 1f, 2);
        yield return Esperar(0.3f);
        il.Deshacer();
    }

    // Espejismo: deja un senuelo quieto donde estaba y reaparece en otro sitio.
    // Si golpeas el senuelo, explota.
    private IEnumerator Espejismo()
    {
        tEspejismo = Time.time;
        PlayerControler p = PC;
        if (p == null) yield break;
        IlusionCazadora il = SacarIlusion();
        if (il != null)
        {
            Color c = ajustes.colorIlusion;
            c.a = Mathf.Min(0.85f, c.a + 0.2f);
            il.Aparecer(this, Pos, mirada, c, true, ajustes.vidaEspejismo, Oscuridad);
            il.anim.Pose("estocada", 0);
        }
        Sonar("espejismo", 0.8f);
        yield return Fundido(0f, 0.15f);
        int lado = Random.value < 0.5f ? -1 : 1;
        float x = cuerpo.Limitar(p.transform.position.x + lado * 5.5f);
        if (Mathf.Abs(x - p.transform.position.x) < 3f) x = cuerpo.Limitar(p.transform.position.x - lado * 5.5f);
        cuerpo.Colocar(new Vector2(x, suelo));
        MirarA(p.transform.position.x);
        yield return Fundido(1f, 0.2f);
        yield return OlaDeLuz();
    }

    // El senuelo golpeado explota (un instante despues, con destello).
    public void EspejismoGolpeado(IlusionCazadora il)
    {
        if (muertaDelTodo || !isActiveAndEnabled) return;
        StartCoroutine(Explosion(il.Posicion + Vector2.up * 0.7f));
    }

    private IEnumerator Explosion(Vector2 centro)
    {
        MarcaSuelo.Poner(MarcaSuelo.Forma.Aro, new Vector2(centro.x, suelo + 0.1f), new Vector2(3f, 0f), new Color(0.75f, 0.85f, 1f, 1f), 0.2f);
        Sonar("espejismo_carga", 0.7f);
        yield return Esperar(0.2f);
        Sonar("explosion", 0.9f);
        CamaraCazadora.Sacudir(ajustes.temblorPesado * 0.8f);
        PolvoCazadora.Soltar(centro, new Color(0.75f, 0.88f, 1f, 1f), 36, 2.4f);
        GolpeCazadora.Caja(centro, new Vector2(3.2f, 2.4f), Dano(ajustes.danoExplosionEspejismo), this, PlayerControler.TipoDano.Magico, EstadoPlayer.Ninguno, 0f, noLetalAccion);
    }

    // Cuchillada fantasma: un tajo aparece donde estabas hace un segundo, con una
    // marca antes.
    private IEnumerator CuchilladaFantasma()
    {
        tFantasma = Time.time;
        PrepararElementos();
        int veces = fase >= 2 ? 3 : 1;
        SpritesCazadora.Clip c = cuerpoAnim.hojas.Buscar("luna");
        for (int i = 0; i < veces; i++)
        {
            AlternarElemento(i);
            Vector2 donde = PosicionHace(ajustes.retrasoFantasma);
            donde.y = suelo;
            MirarA(donde.x);
            float aviso = Aviso(ajustes.cuchilladaFantasma, true);
            MarcaSuelo.Poner(MarcaSuelo.Forma.Franja, new Vector2(donde.x, suelo + 0.07f), new Vector2(2.3f, 0.13f), ColorElemento(elementoAtaque), aviso);
            Sonar("fantasma_aviso", 0.7f);
            yield return AvisoPose("luna", 0, aviso, true, ColorElemento(elementoAtaque), false);
            Rect caja = c.Caja(1);
            int dir = mirada;
            Elemento e = elementoAtaque;
            TajoAparecido.Lanzar(c.efecto[1], caja, new Vector2(donde.x - dir * caja.center.x, suelo), dir, 1.1f, ColorElemento(e),
                                 Dano(ajustes.cuchilladaFantasma.dano), false, noLetalAccion, 0f, OlaLuz.EstadoDe(e) == EstadoPlayer.Sangrado ? EstadoPlayer.Ninguno : OlaLuz.EstadoDe(e),
                                 ajustes.acumulacionEstado, r => { if (r.HasValue && r.Value == PlayerControler.ResultadoDano.Recibido && this != null) { CamaraCazadora.Sacudir(ajustes.temblorLigero); RobarVidaCon(e); } });
            Sonar("tajo", 0.8f);
            cuerpoAnim.Reproducir("luna", 1f, 1);
            yield return Esperar(0.28f);
        }
        yield return Recuperar(ajustes.cuchilladaFantasma, false);
    }

    // Trampas de sonido: simbolos tenues en el suelo; si pisas uno, te ataca al
    // instante con un tajo desde arriba.
    private IEnumerator TrampasDeSonido()
    {
        tTrampas = Time.time;
        PlayerControler p = PC;
        if (p == null) yield break;
        cuerpo.Parar(ajustes.frenada);
        cuerpoAnim.Pose("tajoArriba", 0);
        Sonar("trampas", 0.7f);
        for (int i = 0; i < ajustes.trampasPorVez; i++)
        {
            float x = cuerpo.Limitar(p.transform.position.x + (i % 2 == 0 ? 1 : -1) * Random.Range(1.6f, 6f));
            TrampaSonido.Poner(new Vector2(x, suelo), 1.1f, ajustes.vidaTrampas, TrampaPisada);
            yield return Esperar(0.12f);
        }
        yield return Esperar(0.3f);
        cuerpoAnim.Reproducir("quieto");
    }

    private void TrampaPisada(Vector2 donde)
    {
        if (this == null || muertaDelTodo || transicion || !isActiveAndEnabled) return;
        AnilloRuido.Mostrar(donde, 1f);
        StartCoroutine(AtaqueTrampa(donde));
    }

    private IEnumerator AtaqueTrampa(Vector2 donde)
    {
        float aviso = Aviso(ajustes.trampaSonido, false);
        MarcaSuelo.Poner(MarcaSuelo.Forma.Franja, new Vector2(donde.x, suelo + 0.07f), new Vector2(1.6f, 0.13f), new Color(0.7f, 0.85f, 1f, 1f), aviso);
        Sonar("marca", 0.7f);
        yield return Esperar(aviso);
        SpritesCazadora.Clip c = cuerpoAnim.hojas.Buscar("tajoAbajo");
        Rect caja = c.Caja(1);
        TajoAparecido.Lanzar(c.efecto[1], caja, new Vector2(donde.x - caja.center.x, suelo - caja.yMin), 1, 1f, ColorElemento(elementoAtaque),
                             Dano(ajustes.trampaSonido.dano), false, noLetalAccion, 3f);
        Sonar("tajo", 0.8f);
    }

    // Lluvia de tajos: salta fuera de la pantalla y cae con un tajo descendente,
    // soltando olas de luz a los dos lados.
    private IEnumerator LluviaDeTajos()
    {
        PrepararElementos();
        int rondas = fase >= 2 ? 2 : 1;
        SpritesCazadora.Clip c = cuerpoAnim.hojas.Buscar("tajoAbajo");
        for (int i = 0; i < rondas; i++)
        {
            AlternarElemento(i);
            Sonar("salto", 0.8f);
            cuerpoAnim.Reproducir("salto", 1.5f);
            cuerpo.Trayecto(new Vector2(Pos.x, suelo + 9f), 0.32f, 0f, CuerpoCazadora.Curva.Suave);
            yield return Fundido(0f, 0.3f);
            PlayerControler p = PC;
            if (p == null) yield break;
            float x = cuerpo.Limitar(p.transform.position.x);
            float aviso = Aviso(ajustes.lluvia, true);
            MarcaSuelo.Poner(MarcaSuelo.Forma.Franja, new Vector2(x, suelo + 0.07f), new Vector2(1.9f, 0.14f), ColorElemento(elementoAtaque), aviso);
            Sonar("marca", 0.8f);
            yield return Esperar(Mathf.Max(0.05f, aviso - 0.15f));
            cuerpo.Colocar(new Vector2(x, suelo + 6f));
            cuerpoAnim.Alfa(1f);
            cuerpoAnim.Pose("tajoAbajo", 1);
            cuerpoAnim.ColorEfecto(ColorElemento(elementoAtaque));
            cuerpo.Trayecto(new Vector2(x, suelo), 0.15f, 0f, CuerpoCazadora.Curva.Caida);
            enGolpe = true;
            bool pego = false;
            int dano = Dano(ajustes.lluvia.dano);
            while (cuerpo.EnTrayecto)
            {
                Estela();
                if (!pego)
                {
                    var r = GolpearCuadro(c, 1, dano, PlayerControler.TipoDano.Fisico, 1f, false, noLetalAccion, true);
                    if (r.HasValue) { pego = true; TrasPegar(r.Value, dano); }
                }
                yield return null;
            }
            enGolpe = false;
            Aterrizaje();
            LanzarOla(1);
            LanzarOla(-1);
            cuerpoAnim.Reproducir("tajoAbajo", 1f, 2);
            if (parado) yield break;
            if (i < rondas - 1) yield return Esperar(0.35f);
        }
        yield return Recuperar(ajustes.lluvia, true);
    }

    // Cruce doble: ella cruza hacia un lado y una ilusion hacia el otro.
    private IEnumerator CruceDoble()
    {
        PlayerControler p = PC;
        if (p == null) yield break;
        PrepararElementos();
        Vector2 pp = p.transform.position;
        int lado = Pos.x < pp.x ? -1 : 1;
        // Las dos a la misma distancia de ti (si una pared recorta un lado, el otro
        // tambien): llegan a la vez, y asi se puede parar a la verdadera.
        float hueco = Mathf.Min(4.5f, Mathf.Abs(cuerpo.Limitar(pp.x + lado * 4.5f) - pp.x), Mathf.Abs(cuerpo.Limitar(pp.x - lado * 4.5f) - pp.x));
        float xa = pp.x + lado * hueco, xb = pp.x - lado * hueco;
        MirarA(xa);
        cuerpoAnim.Pose("cruce", 1);
        yield return Dash(xa, 0.22f);
        MirarA(pp.x);
        IlusionCazadora il = SacarIlusion();
        var otras = new List<IlusionCazadora>();
        if (il != null) { il.Aparecer(this, new Vector2(xb, suelo), lado, ajustes.colorIlusion, false, 3f, Oscuridad); otras.Add(il); }
        CamaraCazadora.Abrir(1.2f);
        yield return AvisoConIlusiones("cruce", 0, Aviso(ajustes.cruceDoble, true), true, otras);
        pp = p.transform.position;
        float dur = GolpeT(ajustes.cruceDoble) / RapidezElemento;
        ilusionesConElla.Clear();
        float detras = Mathf.Min(ajustes.cruceDetras, Mathf.Abs(cuerpo.Limitar(pp.x + lado * ajustes.cruceDetras) - pp.x), Mathf.Abs(cuerpo.Limitar(pp.x - lado * ajustes.cruceDetras) - pp.x));
        if (il != null && il.Viva) { ilusionesConElla.Add(il); StartCoroutine(CruceIlusion(il, pp.x + lado * detras, dur, Dano(ajustes.danoIlusion))); }
        yield return CruceCuerpo(pp.x - lado * detras, dur, Dano(ajustes.cruceDoble.dano), false, false, false);
        ilusionesConElla.Clear();
        if (il != null) il.Deshacer();
        if (parado) yield break;
        yield return Recuperar(ajustes.cruceDoble, true);
    }

    // ------------------------------------------------------------------ Fase 3

    // Danza de la Cacería: 8-12 golpes con dashes y ritmo enganoso, cada uno con
    // su aviso; termina con un golpe pesado y una recuperacion larga.
    private IEnumerator DanzaDeLaCaceria()
    {
        tDanza = Time.time;
        PrepararElementos();
        yield return Acercarse(ajustes.distanciaCerca + 0.6f, 1f);
        int n = Random.Range(ajustes.golpesDanza.x, ajustes.golpesDanza.y + 1);
        int pausaEn = Random.Range(3, Mathf.Max(4, n - 2));
        string[] clips = { "barrido", "luna", "estocada", "luna" };
        Sonar("danza", 0.9f);
        for (int i = 0; i < n; i++)
        {
            AlternarElemento(i);
            PlayerControler p = PC;
            if (p == null) yield break;
            Vector2 pp = p.transform.position;
            MirarA(pp.x);
            string clip = clips[Random.Range(0, clips.Length)];
            if (!p.EnSuelo && pp.y > Pos.y + 1f) clip = "tajoArriba";
            float paso = Mathf.Clamp(pp.x - Pos.x - mirada * 1.1f, -2.4f, 2.4f);
            cuerpo.Trayecto(new Vector2(Pos.x + paso, suelo), 0.12f, 0f, CuerpoCazadora.Curva.Dash);
            Estela();
            // Ritmo enganoso: unos avisos mas largos que otros (nunca por debajo del minimo).
            float aviso = Mathf.Max(ajustes.avisoMinimoLigero, Aviso(ajustes.danzaGolpe, false) * Random.Range(0.9f, 1.7f));
            yield return AvisoPose(clip == "barrido" ? "estocada" : clip, 0, aviso, false);
            yield return Golpe(clip, GolpeT(ajustes.danzaGolpe), Dano(ajustes.danzaGolpe.dano));
            if (parado) yield break;
            yield return Esperar(i == pausaEn ? 0.5f : Random.Range(0.02f, 0.16f));
        }
        AlternarElemento(n);
        yield return AvisoPose("luna", 0, Aviso(ajustes.danzaFinal, true), true);
        cuerpo.Trayecto(new Vector2(Pos.x + mirada * 1.4f, suelo), 0.12f, 0f, CuerpoCazadora.Curva.Dash);
        yield return Golpe("luna", GolpeT(ajustes.danzaFinal), Dano(ajustes.danzaFinal.dano), PlayerControler.TipoDano.Fisico, 1.4f);
        CamaraCazadora.Sacudir(ajustes.temblorPesado);
        if (parado) yield break;
        yield return Recuperar(ajustes.danzaFinal, true);
    }

    // Cacería de Espejos: 3-4 figuras, solo una real. Cruzan una tras otra.
    private IEnumerator CaceriaDeEspejos()
    {
        tEspejos = Time.time;
        PlayerControler p = PC;
        if (p == null) yield break;
        PrepararElementos();
        int n = Mathf.Max(2, ajustes.ilusionesEspejos);
        int real = Random.Range(0, n + 1);
        Vector2 pp = p.transform.position;
        yield return Fundido(0f, 0.15f);
        var figuras = new List<IlusionCazadora>();
        var xs = new List<float>();
        for (int i = 0; i <= n; i++)
        {
            int lado = i % 2 == 0 ? -1 : 1;
            xs.Add(cuerpo.Limitar(pp.x + lado * (3.5f + (i / 2) * 1.6f)));
        }
        for (int i = 0; i <= n; i++)
        {
            if (i == real) { figuras.Add(null); continue; }
            IlusionCazadora il = SacarIlusion();
            if (il != null) il.Aparecer(this, new Vector2(xs[i], suelo), xs[i] < pp.x ? 1 : -1, ajustes.colorIlusion, false, 6f, Oscuridad);
            figuras.Add(il);
        }
        cuerpo.Colocar(new Vector2(xs[real], suelo));
        MirarA(pp.x);
        yield return Fundido(1f, 0.15f);
        yield return AvisoConIlusiones("cruce", 0, Aviso(ajustes.cruce, true), true, figuras.FindAll(f => f != null));
        CamaraCazadora.Abrir(2.5f);
        int dano = Dano(ajustes.cruce.dano);
        for (int i = 0; i <= n; i++)
        {
            pp = p.transform.position;
            if (i == real)
            {
                int dir = pp.x > Pos.x ? 1 : -1;
                Mirar(dir);
                yield return CruceCuerpo(cuerpo.Limitar(pp.x + dir * ajustes.cruceDetras), GolpeT(ajustes.cruce), dano, false, false, false);
                if (parado) break;
            }
            else if (figuras[i] != null && figuras[i].Viva)
            {
                IlusionCazadora il = figuras[i];
                int dir = pp.x > il.Posicion.x ? 1 : -1;
                il.Mirar(dir);
                StartCoroutine(CruceIlusion(il, cuerpo.Limitar(pp.x + dir * ajustes.cruceDetras), GolpeT(ajustes.cruce), dano));
                yield return Esperar(0.32f);
            }
        }
        yield return Esperar(0.3f);
        foreach (IlusionCazadora il in figuras) if (il != null) il.Deshacer();
        if (parado) yield break;
        yield return Recuperar(ajustes.cruce, true);
    }
}
