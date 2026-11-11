using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// El Black Knight (3): sus dos ataques especiales.
//
// ONDAS DE FUEGO (desde la fase 2): te empuja lejos (solo se esquiva), se va al
// otro extremo de la arena y lanza ondas de fuego altas, una tras otra. No se
// pueden parar ni bloquear ni saltar: solo se atraviesan esquivando. Si te toca
// una, solo te hace dano. Si llegas hasta el y le pegas, las corta y queda
// aturdido un momento. Si no, acaba solo al llegar a su maximo.
//
// SOMBRAS ESPECTRALES (fase 3): clava la espada en el suelo y se queda quieto.
// Durante unos segundos salen sombras con su figura, de una en una: por un lado,
// por el otro, desde arriba... Cada una hace un trozo de sus ataques (se paran o
// se esquivan). Mientras, puedes pegarle a el. Al acabar saca la espada.
public partial class JefeBlackKnight
{
    private bool enOndas, retirandose, clavado;
    private int golpesDuranteOndas;
    private float finEnfriamientoOndas, finEnfriamientoSombras;
    private readonly List<OndaFuegoBK> ondas = new List<OndaFuegoBK>();
    private readonly List<SombraBK> sombras = new List<SombraBK>();

    public bool EnOndas => enOndas;
    public bool Clavado => clavado;
    public bool Retirandose => retirandose;
    public int OndasLanzadas { get; private set; }
    public int OndasCortadas { get; private set; }
    public int SombrasInvocadas { get; private set; }
    public readonly List<string> TiposSombra = new List<string>();
    public int OndasVivas { get { ondas.RemoveAll(o => o == null); return ondas.Count; } }

    // ------------------------------------------------------------------ Ondas de fuego

    private IEnumerator OndasDeFuego()
    {
        finEnfriamientoOndas = Time.time + ajustes.enfriamientoOndas;
        BuscarPlayer();
        golpesDuranteOndas = 0;

        // 1. El empujon: el estallido de su parry, de cerca. Solo se esquiva.
        yield return Acercarse(1.5f);
        MirarYa();
        anim.Reproducir(ClipContra, true, 1f);
        ritmo.Avisar(PesoGolpe.Pesado, 0.75f);
        anim.Reproducir(ClipContra, false, 0f);
        for (float t = 0f; t < 0.6f; t += Time.deltaTime) { Parar(); MirarYa(); yield return null; }
        anim.Reproducir(ClipContra, false, 1f);
        while (anim.Fotograma < 2 && !anim.Terminado) yield return null;
        ritmo.QuitarAviso();
        Sonar("jefe_tajo_fuerte");
        CamaraDinamica.Sacudir(0.5f);
        PlayerControler p = GolpeCazadora.PlayerEn((Vector2)transform.position + new Vector2(mirada * 0.6f, 0.9f), new Vector2(3.2f, 2.4f));
        if (p != null)
        {
            PlayerControler.ResultadoDano r = GolpeCazadora.Aplicar(p, ajustes.danoEmpujon, this, PlayerControler.TipoDano.Fisico,
                                                                    EstadoPlayer.Ninguno, 0f, true, true);
            if (r != PlayerControler.ResultadoDano.Ignorado)
                p.Derribar(new Vector2(mirada * ajustes.empujon.x, ajustes.empujon.y), true);
        }
        while (!anim.Terminado) yield return null;

        // 2. Se va al otro extremo (el mas lejano a ti), rodando y corriendo; sin
        //    que le entre nada mientras.
        enOndas = true;
        retirandose = true;
        camaraAmplia = true;
        invulnerable = true;
        BuscarPlayer();
        float destino = DestinoAlejarse();
        int dir = destino > transform.position.x ? 1 : -1;
        Mirar(dir);
        anim.Reproducir(ClipCorrer, true);
        AtravesarPlayer(true);
        for (float t = 0f; t < 3f && (destino - transform.position.x) * dir > 0.15f; t += Time.deltaTime)
        {
            Mirar(dir);
            float v = ajustes.velocidadCorrer * 1.7f;
            velX = dir * v;
            rb.linearVelocity = new Vector2(velX, rb.linearVelocity.y);
            if (HayParedHacia(dir, 0.4f, 0.5f)) break;
            yield return null;
        }
        AtravesarPlayer(false);
        Parar();
        invulnerable = false;
        retirandose = false;
        MirarYa();
        golpesDuranteOndas = 0;

        // 3. Las ondas, una tras otra, hasta que le pegues o se acabe el tiempo.
        float inicio = Time.time;
        bool cortado = false;
        string[] clips = { "ligero2", "pesado2" };
        for (int i = 0; i < ajustes.ondasMaximas && Time.time - inicio < ajustes.duracionMaxOndas; i++)
        {
            MirarYa();
            string clip = clips[i % clips.Length];
            anim.Reproducir(clip, true, 1f);
            // Cada onda con un aviso corto naranja (la onda viaja: hay tiempo de verla).
            ritmo.Avisar(PesoGolpe.Ligero, 0.3f);
            anim.Reproducir(clip, false, 0f);
            for (float t = 0f; t < 0.22f && golpesDuranteOndas < ajustes.golpesParaCortar; t += Time.deltaTime) { Parar(); yield return null; }
            if (golpesDuranteOndas >= ajustes.golpesParaCortar) { cortado = true; break; }
            anim.Reproducir(clip, false, 1f);
            while (anim.Fotograma < 1 && !anim.Terminado) yield return null;
            ritmo.QuitarAviso();
            LanzarOnda();
            float espera = Random.Range(ajustes.intervaloOndas.x, ajustes.intervaloOndas.y) / Mathf.Max(1f, Desafio.MultVelocidad);
            for (float t = 0f; t < espera && golpesDuranteOndas < ajustes.golpesParaCortar; t += Time.deltaTime)
            {
                if (anim.Terminado) anim.Reproducir(ClipQuieto);
                Parar();
                MirarYa();
                yield return null;
            }
            if (golpesDuranteOndas >= ajustes.golpesParaCortar) { cortado = true; break; }
        }
        enOndas = false;
        camaraAmplia = false;
        ritmo.QuitarAviso();
        if (cortado)
        {
            OndasCortadas++;
            TextoFlotante.Mostrar("¡Cortado!", (Vector2)transform.position + Vector2.up * 1.9f, ajustes.colorTextoAturdido, 1f);
            yield return Aturdirse(ajustes.aturdidoAlCortar, "ondas");
        }
        else yield return Quieto(0.8f);
    }

    private void LanzarOnda()
    {
        Sonar("jefe_medialuna", 0.9f);
        Vector2 desde = (Vector2)transform.position + new Vector2(mirada * 0.9f, 0f);
        float alcance = arena.width > 0f ? arena.width + 2f : 26f;
        OndaFuegoBK o = OndaFuegoBK.Lanzar(this, desde, mirada, ajustes, alcance);
        ondas.Add(o);
        OndasLanzadas++;
    }

    // ------------------------------------------------------------------ Sombras espectrales

    private IEnumerator SombrasEspectrales()
    {
        finEnfriamientoSombras = Time.time + ajustes.enfriamientoSombras;
        BuscarPlayer();
        MirarYa();
        Parar();
        // Clava la espada: el barrido hasta el suelo y se queda ahi.
        anim.Reproducir("pesado3", true, 1f);
        while (anim.Fotograma < 6 && !anim.Terminado) yield return null;
        anim.Reproducir(ClipClavada, true);
        clavado = true;
        camaraAmplia = true;
        Sonar("jefe_caida");
        CamaraDinamica.Sacudir(0.7f);
        ScreenFlash.Destello(new Color(ajustes.colorSombra.r, ajustes.colorSombra.g, ajustes.colorSombra.b, 0.25f), 0.35f);

        float inicio = Time.time;
        int orden = 0;
        int lado = Random.value < 0.5f ? -1 : 1;
        while (Time.time - inicio < ajustes.duracionSombras)
        {
            // Primero de un lado, luego del otro, luego desde arriba... y vuelta.
            string tipo = orden % 3 == 2 ? "arriba" : "lado";
            if (tipo == "lado") lado = -lado;
            orden++;
            yield return SombraAtaca(tipo, lado);
            float pausa = Random.Range(ajustes.pausaSombras.x, ajustes.pausaSombras.y);
            for (float t = 0f; t < pausa; t += Time.deltaTime) { Latido(inicio); Parar(); yield return null; }
        }
        clavado = false;
        camaraAmplia = false;
        ritmo.Destello(ajustes.colorSombraBorde, 0f);
        // Saca la espada (el final del barrido).
        anim.Reproducir("pesado3", true, 1f);
        while (anim.Fotograma < 8 && !anim.Terminado) yield return null;
        while (!anim.Terminado) yield return null;
        yield return Quieto(0.5f);
    }

    private void Latido(float inicio) => ritmo.Destello(ajustes.colorSombraBorde, 0.18f + 0.12f * Mathf.Sin((Time.time - inicio) * 5f));

    // Una sombra: aparece, avisa con su ritmo (naranja rapido / rojo lento) y
    // hace uno o dos golpes. "lado": a tu izquierda o derecha, mirandote. "arriba":
    // cae sobre ti (se para mires hacia donde mires).
    private IEnumerator SombraAtaca(string tipo, int lado)
    {
        BuscarPlayer();
        if (player == null) yield break;
        SombrasInvocadas++;
        TiposSombra.Add(tipo);
        float inicio = Time.time;
        Vector2 pos;
        if (tipo == "arriba") pos = new Vector2(PosPlayer.x, PosPlayer.y + ajustes.alturaSombraArriba);
        else
        {
            float x = LimitarX(PosPlayer.x + lado * ajustes.distanciaSombra);
            // Si el borde no deja sitio a ese lado, sale del otro.
            if (Mathf.Abs(x - PosPlayer.x) < ajustes.distanciaSombra * 0.6f) x = LimitarX(PosPlayer.x - lado * ajustes.distanciaSombra);
            pos = new Vector2(x, PeligrosJefe.SueloBajo(new Vector2(x, PosPlayer.y + 1.5f), transform.position.y));
        }
        SombraBK s = SombraBK.Crear(this, anim, srCaballero, pos, ajustes);
        sombras.Add(s);
        s.Mirar(PosPlayer.x >= pos.x ? 1 : -1);
        yield return s.Aparecer(0.25f);
        ritmo.Empezar();
        Resultados.Clear();

        if (tipo == "arriba")
        {
            AjustesBlackKnight.Golpe g = ajustes.Buscar("Caida");
            if (g != null)
            {
                s.Reproducir(ClipCaer, 1f);
                float aviso = ritmo.AvisoObjetivo(PesoGolpe.Pesado, false, 0.55f);
                // Aviso mientras flota sobre ti; te sigue hasta la mitad.
                yield return s.Avisar(ajustes.reglas.ColorAviso(PesoGolpe.Pesado), ajustes.reglas.Parpadeo(PesoGolpe.Pesado), true, aviso,
                                      () => new Vector2(PosPlayer.x, PosPlayer.y + ajustes.alturaSombraArriba), 0.5f);
                // Cae en un instante.
                float yS = PeligrosJefe.SueloBajo(new Vector2(s.transform.position.x, PosPlayer.y + 1.5f), transform.position.y);
                Vector2 desde = s.transform.position;
                for (float t = 0f; t < 0.12f; t += Time.deltaTime) { s.transform.position = Vector2.Lerp(desde, new Vector2(desde.x, yS), t / 0.12f); yield return null; }
                s.transform.position = new Vector2(desde.x, yS);
                s.Reproducir(ClipAterrizar, 1f);
                yield return GolpeSombra(s, g, Time.time - inicio, true);
            }
        }
        else
        {
            // Un trozo de sus ataques: uno o dos golpes de un combo.
            string[][] trozos =
            {
                new[] { "Tajo alto" }, new[] { "Estocada" }, new[] { "Tajo pesado" }, new[] { "Arco", "Estocada" }, new[] { "Reves pesado" },
            };
            string[] trozo = trozos[Random.Range(0, trozos.Length)];
            for (int i = 0; i < trozo.Length; i++)
            {
                AjustesBlackKnight.Golpe g = ajustes.Buscar(trozo[i]);
                if (g == null) continue;
                s.Mirar(PosPlayer.x >= s.transform.position.x ? 1 : -1);
                s.Reproducir(g.clip, 1f);
                while (s.Fotograma < g.cuadroAviso && !s.Terminado) yield return null;
                float fps = Mathf.Max(1f, anim.Fps(g.clip));
                float dibujado = Mathf.Max(0, g.golpeDesde - g.cuadroAviso) / fps;
                float objetivo = ritmo.AvisoObjetivo(g.peso, i == trozo.Length - 1 && trozo.Length > 1, g.aviso);
                s.Pausar(true);
                float inicioAviso = Time.time;
                yield return s.Avisar(ajustes.reglas.ColorAviso(g.peso), ajustes.reglas.Parpadeo(g.peso), g.peso == PesoGolpe.Pesado,
                                      Mathf.Max(0f, objetivo - dibujado), null, 0f);
                s.Pausar(false);
                while (s.Fotograma < g.golpeDesde && !s.Terminado) yield return null;
                yield return GolpeSombra(s, g, Time.time - inicioAviso + dibujado, false);
                if (i < trozo.Length - 1 && g.cortarEn >= 0) while (s.Fotograma < g.cortarEn && !s.Terminado) yield return null;
            }
            while (!s.Terminado) yield return null;
        }
        yield return s.Desaparecer(0.25f);
        sombras.Remove(s);
        if (s != null) Destroy(s.gameObject);
    }

    // El golpe de una sombra: su caja (desde su lado; el parry cuenta ese lado),
    // con el dano reducido de la ficha, una sola vez.
    private IEnumerator GolpeSombra(SombraBK s, AjustesBlackKnight.Golpe g, float aviso, bool desdeArriba)
    {
        float hueco = ritmo.SegundosDesdeUltimoGolpe;
        GolpesActivos++;
        AlActivarGolpe?.Invoke(g, aviso, hueco);
        Sonar(g.peso == PesoGolpe.Pesado ? "jefe_tajo_fuerte" : "jefe_tajo", 0.8f);
        PlayerControler pc = PC;
        int vidaAntes = pc != null ? pc.VidaActual : 0;
        int dano = ritmo.DanoPermitido(Mathf.Max(1, Mathf.RoundToInt(g.dano * ajustes.danoSombra)), pc);
        PlayerControler.ResultadoDano? r = null;
        int hecho = 0;
        while (s != null && s.Fotograma <= g.golpeHasta && !s.Terminado)
        {
            if (!r.HasValue)
            {
                ritmo.AntesDeGolpear();
                Vector2 centro = (Vector2)s.transform.position + new Vector2(g.cajaCentro.x * s.Dir, g.cajaCentro.y);
                r = GolpeCazadora.Caja(centro, g.cajaTamano, dano, this, PlayerControler.TipoDano.Magico,
                                       EstadoPlayer.Quemadura, ajustes.quemaduraPorGolpe,
                                       desdeArriba: desdeArriba, origenX: s.transform.position.x);
                ritmo.DespuesDeGolpear();
                if (r.HasValue && pc != null) hecho = Mathf.Max(0, vidaAntes - pc.VidaActual);
                if (DepuracionCazadora.VerCajas) CajaDebug.Mostrar(centro, g.cajaTamano, ajustes.colorSombraBorde, 0.03f);
            }
            yield return null;
        }
        Resultados.Add(r);
        Vector2 punto = pc != null ? (Vector2)pc.transform.position + Vector2.up * 0.5f : (Vector2)transform.position;
        float tono = ritmo.TonoParry;
        if (ritmo.Registrar(r, hecho, punto, s != null && s.Dir > 0 ? 20f : 160f,
                            ajustes.colorSombraBorde, ajustes.colorSombra))
            Sonar("jefe_parry", 0.8f, tono);
        GolpesActivos--;
        ritmo.FinGolpe();
        AlTerminarGolpe?.Invoke(g, r);
    }

    // Ondas y sombras que queden (al cambiar de fase, morir o desaparecer).
    private void LimpiarEfectos()
    {
        foreach (OndaFuegoBK o in ondas) if (o != null) Destroy(o.gameObject);
        ondas.Clear();
        foreach (SombraBK s in sombras) if (s != null) Destroy(s.gameObject);
        sombras.Clear();
        LimpiarErupciones();
    }
}
