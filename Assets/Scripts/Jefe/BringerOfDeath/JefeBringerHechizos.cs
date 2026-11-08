using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The Bringer of Death (2): los hechizos. Los efectos son el hechizo propio del
// Bringer (portal y mano) y los packs del Necromante y del Starcaller (que no
// usaba ningun otro jefe). Los clips los pone Warrior > Bringer of Death.
//
// Los de toda la arena (cosecha, lluvia de cometas, juicio y marea) siempre dejan
// un hueco o un momento libre: se esquivan moviendose, no aguantando.
public partial class JefeBringer
{
    [Header("Efectos (los pone Warrior > Bringer of Death)")]
    public AnimadorHoja.Clip fxPortal;
    public AnimadorHoja.Clip fxMano;
    public AnimadorHoja.Clip fxAnillo;
    public AnimadorHoja.Clip fxEstallido;
    public AnimadorHoja.Clip fxOla;
    public AnimadorHoja.Clip fxMediaLuna;
    public AnimadorHoja.Clip fxEspiritu;
    public AnimadorHoja.Clip fxEspirituFin;
    public AnimadorHoja.Clip fxMarca;
    public AnimadorHoja.Clip fxCometa;
    public AnimadorHoja.Clip fxEstrellas;
    public AnimadorHoja.Clip fxEsferaAbrir;
    public AnimadorHoja.Clip fxEsferaBucle;
    public AnimadorHoja.Clip fxEsferaFin;

    // Lo que leen las pruebas: posiciones libres de la ultima cosecha y del ultimo juicio.
    public readonly List<float> UltimosHuecos = new List<float>();
    public int UltimasManos { get; private set; }
    public int UltimosCometas { get; private set; }
    public EsferaVacio UltimaEsfera { get; private set; }

    // ------------------------------------------------------------------ Ayudas

    private float XMin => arena.width > 0f ? arena.xMin + 0.8f : transform.position.x - 12f;
    private float XMax => arena.width > 0f ? arena.xMax - 0.8f : transform.position.x + 12f;

    private float SueloEn(float x)
    {
        float desde = arena.height > 0f ? arena.yMax - 0.2f : transform.position.y + 8f;
        return PeligrosJefe.SueloBajo(new Vector2(x, desde), suelo);
    }

    // Alza el brazo (cuadros 0-4) con el aviso verde; desde el 5 el anillo sobre la mano, en bucle.
    private IEnumerator Conjurar(float aviso)
    {
        Parar();
        MirarYa();
        CambiarEstado(EstadoEnemigo.Anticipacion);
        anim.Reproducir(ClipConjurar, true, Vel);
        StartCoroutine(Avisar(ajustes.colorHechizo, aviso));
        Sonar("hielo_conjuro", 0.5f, 0.6f);
        float inicio = Time.time;
        while (!anim.Terminado) yield return null;
        anim.Reproducir(ClipConjurarBucle, true, Vel);
        while (Time.time - inicio < aviso) yield return null;
        UltimoAviso = Time.time - inicio;
        CambiarEstado(EstadoEnemigo.Ataque);
    }

    private IEnumerator Terminar(float recuperacion)
    {
        anim.Reproducir(ClipQuieto, true);
        CambiarEstado(EstadoEnemigo.Recuperacion);
        yield return Esperar(recuperacion / Vel);
    }

    // ------------------------------------------------------------------ Mano del abismo

    private IEnumerator Manos()
    {
        yield return Conjurar(0.35f);
        int n = fase2 ? Mathf.Max(1, ajustes.manosFase2) : 1;
        var manos = new List<ManoAbismo>();
        for (int i = 0; i < n; i++)
        {
            BuscarPlayer();
            Vector2 p = new Vector2(PosPlayer.x, SueloEn(PosPlayer.x));
            manos.Add(ManoAbismo.Invocar(fxPortal, fxMano, p, player, ajustes.seguirMano / Vel, ajustes.fijoMano,
                                         ajustes.danoMano, ajustes.cajaMano, ajustes.colorMano));
            if (i < n - 1) yield return Esperar(ajustes.intervaloManos / Vel);
        }
        for (float t = 0f; t < 6f && manos.Exists(m => m != null && !m.Termino); t += Time.deltaTime) yield return null;
        yield return Terminar(0.3f);
    }

    // ------------------------------------------------------------------ Media luna

    // Un tajo de guadana al aire que suelta la media luna verde: baja (saltala) o
    // alta (quedate en el suelo). En la fase 2, dos seguidas.
    private IEnumerator MediasLunas()
    {
        int n = fase2 ? ajustes.mediasLunasFase2 : ajustes.mediasLunasFase1;
        for (int i = 0; i < Mathf.Max(1, n); i++)
        {
            Parar();
            BuscarPlayer();
            MirarYa();
            CambiarEstado(EstadoEnemigo.Anticipacion);
            anim.Reproducir(ClipGuadana, true, Vel * 1.2f);
            StartCoroutine(Avisar(ajustes.colorHechizo, 0.4f));
            while (anim.Fotograma < 4 && !anim.Terminado) yield return null;
            bool alta = Random.value < 0.5f;
            Vector2 desde = (Vector2)transform.position + new Vector2(mirada * 1.2f, alta ? ajustes.alturaAlta : ajustes.alturaBaja);
            MediaLunaNecro.Lanzar(fxMediaLuna, desde, mirada, ajustes.velocidadMediaLuna, ajustes.alcanceMediaLuna,
                                  ajustes.danoMediaLuna, ajustes.cajaMediaLuna, ajustes.colorNecro);
            while (!anim.Terminado) yield return null;
            if (i < n - 1) yield return Esperar(ajustes.intervaloMediaLuna / Vel);
        }
        yield return Terminar(0.35f);
    }

    // ------------------------------------------------------------------ Ola de almas y marea

    // Ola: de el hacia ti, hasta la pared. Marea (fase 2): una desde cada pared,
    // la segunda un poco despues: hay que saltar las dos.
    private IEnumerator OlaAlmas(bool marea)
    {
        yield return Conjurar(ajustes.avisoOla);
        Sacudir(0.6f);
        if (!marea)
        {
            yield return Cadena(transform.position.x + mirada * 1f, mirada);
        }
        else
        {
            BuscarPlayer();
            int primera = DxPlayer >= 0f ? 1 : -1;
            StartCoroutine(Cadena(primera > 0 ? XMin : XMax, primera));
            yield return Esperar(ajustes.retrasoMarea / Vel);
            yield return Cadena(primera > 0 ? XMax : XMin, -primera);
        }
        yield return Terminar(0.35f);
    }

    private IEnumerator Cadena(float x0, int dir)
    {
        for (float x = x0; x >= XMin - 0.1f && x <= XMax + 0.1f; x += dir * ajustes.pasoOla)
        {
            TramoOla.Invocar(fxOla, new Vector2(x, SueloEn(x)), dir, ajustes.danoOla, ajustes.cajaOla, ajustes.colorNecro);
            yield return Esperar(ajustes.intervaloOla / Vel);
        }
    }

    // ------------------------------------------------------------------ Cosecha (toda la arena)

    // Portales por toda la arena; los huecos (2 portales cada uno) quedan libres.
    // Las manos caen en barrido desde su lado.
    private IEnumerator CosechaManos()
    {
        yield return Conjurar(0.4f);
        var xs = new List<float>();
        for (float x = XMin + 0.4f; x <= XMax - 0.4f; x += ajustes.separacionCosecha) xs.Add(x);
        int huecos = Mathf.Max(1, fase2 ? ajustes.huecosFase2 : ajustes.huecosFase1);
        var libres = new HashSet<int>();
        // Huecos de 2, separados entre si, nunca pegados a la pared.
        for (int intento = 0; intento < 60 && libres.Count < huecos * 2; intento++)
        {
            int i = Random.Range(1, Mathf.Max(2, xs.Count - 2));
            if (libres.Contains(i - 1) || libres.Contains(i) || libres.Contains(i + 1) || libres.Contains(i + 2)) continue;
            libres.Add(i);
            libres.Add(i + 1);
        }
        UltimosHuecos.Clear();
        foreach (int i in libres) if (i < xs.Count) UltimosHuecos.Add(xs[i]);
        var manos = new List<ManoAbismo>();
        bool desdeIzquierda = transform.position.x < (XMin + XMax) * 0.5f;
        for (int k = 0; k < xs.Count; k++)
        {
            int i = desdeIzquierda ? k : xs.Count - 1 - k;
            if (libres.Contains(i)) continue;
            float fijo = ajustes.avisoCosecha + k * ajustes.barridoCosecha;
            manos.Add(ManoAbismo.Invocar(fxPortal, fxMano, new Vector2(xs[i], SueloEn(xs[i])), null, 0f, fijo,
                                         ajustes.danoMano, ajustes.cajaMano, ajustes.colorMano));
        }
        UltimasManos = manos.Count;
        Sacudir(0.8f);
        for (float t = 0f; t < 8f && manos.Exists(m => m != null && !m.Termino); t += Time.deltaTime) yield return null;
        yield return Terminar(0.5f);
    }

    // ------------------------------------------------------------------ Fase 2

    // Circulos de condena: uno donde estas, y otro, y otro.
    private IEnumerator Circulos()
    {
        yield return Conjurar(0.3f);
        for (int i = 0; i < Mathf.Max(1, ajustes.circulos); i++)
        {
            BuscarPlayer();
            EstallidoNecro.Invocar(fxAnillo, fxEstallido, new Vector2(PosPlayer.x, SueloEn(PosPlayer.x)), ajustes.avisoCirculo,
                                   ajustes.danoCirculo, ajustes.cajaCirculo, ajustes.colorNecro);
            yield return Esperar(ajustes.intervaloCirculo / Vel);
        }
        yield return Esperar(ajustes.avisoCirculo);
        yield return Terminar(0.3f);
    }

    // Espiritus: salen en abanico por encima de el y te persiguen.
    private IEnumerator LanzarEspiritus()
    {
        yield return Conjurar(0.4f);
        int n = Mathf.Max(1, ajustes.espiritus);
        for (int i = 0; i < n; i++)
        {
            float a = Mathf.Lerp(40f, 140f, n == 1 ? 0.5f : i / (float)(n - 1));
            Vector2 dir = new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad));
            EspirituNecro.Lanzar(fxEspiritu, fxEspirituFin, (Vector2)transform.position + Vector2.up * 2.6f + dir * 0.4f, dir,
                                 ajustes.velocidadEspiritu, ajustes.giroEspiritu, ajustes.vidaEspiritu, ajustes.danoEspiritu,
                                 player, ajustes.colorNecro);
            Sonar("sombra_carga", 0.4f, 1.3f);
            yield return Esperar(0.15f);
        }
        yield return Terminar(0.4f);
    }

    // Lluvia de cometas por toda la arena: carriles barajados (asi cubre toda la
    // arena sin amontonarse) y uno de cada tantos justo donde estas.
    private IEnumerator LluviaCometas()
    {
        yield return Conjurar(0.5f);
        int n = Mathf.Max(1, ajustes.cometas);
        var carriles = new List<float>();
        for (int i = 0; i < n; i++) carriles.Add(Mathf.Lerp(XMin, XMax, (i + 0.5f) / n));
        for (int i = carriles.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (carriles[i], carriles[j]) = (carriles[j], carriles[i]); }
        UltimosCometas = 0;
        float cada = ajustes.duracionLluvia / n;
        for (int i = 0; i < n; i++)
        {
            BuscarPlayer();
            float x = ajustes.cometaApuntado > 0 && i % ajustes.cometaApuntado == ajustes.cometaApuntado - 1 ? PosPlayer.x : carriles[i];
            x = Mathf.Clamp(x + Random.Range(-0.3f, 0.3f), XMin, XMax);
            CometaEstelar.Caer(fxMarca, fxCometa, fxEstrellas, new Vector2(x, SueloEn(x)), ajustes.avisoCometa, ajustes.velocidadCometa,
                               Random.value < 0.5f ? 1 : -1, ajustes.danoCometa, ajustes.cajaCometa, ajustes.colorEstrellas);
            UltimosCometas++;
            yield return Esperar(cada);
        }
        yield return Esperar(ajustes.avisoCometa);
        yield return Terminar(0.4f);
    }

    // Esfera del vacio (especial): se abre en el suelo a un lado tuyo, te atrae
    // y luego implosiona. Hay que tirar hacia el otro lado (o esquivar al final).
    private IEnumerator EsferaDelVacio()
    {
        yield return Conjurar(0.5f);
        BuscarPlayer();
        float lado = Random.value < 0.5f ? 1f : -1f;
        float x = PosPlayer.x + lado * 4.5f;
        if (x < XMin + 1.5f || x > XMax - 1.5f) x = PosPlayer.x - lado * 4.5f;
        x = Mathf.Clamp(x, XMin + 1.5f, XMax - 1.5f);
        UltimaEsfera = EsferaVacio.Invocar(fxEsferaAbrir, fxEsferaBucle, fxEsferaFin, new Vector2(x, SueloEn(x)), ajustes.atraccion,
                                           ajustes.fuerzaAtraccion, ajustes.radioAtraccion, ajustes.danoEsfera, ajustes.cajaEsfera,
                                           ajustes.congelacionEsfera, ajustes.colorEstrellas);
        for (float t = 0f; t < 6f && UltimaEsfera != null && !UltimaEsfera.Termino; t += Time.deltaTime) yield return null;
        yield return Terminar(0.5f);
    }

    // Juicio: el suelo de toda la arena en tramos; estallan los pares, luego los
    // impares, luego los pares... Hay que ir cambiando de tramo.
    private IEnumerator JuicioFinal()
    {
        yield return Conjurar(0.5f);
        Sacudir(1f);
        var tramos = new List<float>();
        for (float x = XMin + ajustes.tramoJuicio * 0.5f; x <= XMax; x += ajustes.tramoJuicio) tramos.Add(x);
        Vector2 caja = new Vector2(ajustes.tramoJuicio * 0.98f, ajustes.cajaCirculo.y);
        int paridad = Random.value < 0.5f ? 0 : 1;
        for (int p = 0; p < Mathf.Max(1, ajustes.pasadasJuicio); p++)
        {
            UltimosHuecos.Clear();
            for (int i = 0; i < tramos.Count; i++)
            {
                if (i % 2 != paridad) { UltimosHuecos.Add(tramos[i]); continue; }
                EstallidoNecro.Invocar(fxAnillo, fxEstallido, new Vector2(tramos[i], SueloEn(tramos[i])), ajustes.avisoJuicio,
                                       ajustes.danoJuicio, caja, ajustes.colorNecro);
            }
            paridad = 1 - paridad;
            yield return Esperar(ajustes.avisoJuicio + ajustes.entrePasadas);
        }
        yield return Terminar(0.5f);
    }
}
