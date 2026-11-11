using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// El Black Knight (4): dos especiales mas (desde la fase 2) y la camara amplia.
//
// AMULETO SELLADO: hace la pose del buff (aviso morado) y te sella un amuleto
// equipado al azar durante 25 s: sigue puesto pero no hace nada (EfectosAmuletos).
// Es inevitable. No elige el Corazon ni el Costillar (subir y bajar la vida o la
// estamina maxima a mitad de pelea no se recalcula). Si no llevas ninguno que se
// pueda sellar, no lo usa. Al morir el, el sello se quita.
//
// ERUPCIONES: se va al otro extremo, hace la pose del buff y del suelo salen
// columnas de fuego en distintos sitios y de distintos tamanos (cada una avisa con
// una grieta que brilla). No se paran ni se bloquean: hay que apartarse o esquivar.
// Al acabar, salta sobre ti y la pelea sigue.
//
// CAMARA: mientras se aleja (ondas, sombras, erupciones) la camara se abre y se
// centra entre los dos para que siempre se le vea.
public partial class JefeBlackKnight
{
    private bool camaraAmplia, enErupciones;
    private float finEnfriamientoSello, finEnfriamientoErupciones;
    private string miSello;
    private readonly List<ErupcionBK> erupciones = new List<ErupcionBK>();

    public bool CamaraAmplia => camaraAmplia;
    public bool EnErupciones => enErupciones;
    public int SellosPuestos { get; private set; }
    public int ErupcionesLanzadas { get; private set; }
    public readonly List<string> AmuletosSellados = new List<string>();

    // ------------------------------------------------------------------ Camara

    // Cada fotograma mientras dura: abre el plano lo justo para veros a los dos
    // (con margen y tope) y lo centra entre ambos.
    private void ActualizarCamara()
    {
        if (!camaraAmplia || player == null) return;
        Camera c = Camera.main;
        float aspecto = c != null ? Mathf.Max(0.5f, c.aspect) : 16f / 9f;
        float dx = Mathf.Abs(DxPlayer), dy = Mathf.Abs(transform.position.y - PosPlayer.y);
        float tamano = Mathf.Max((dx * 0.5f + ajustes.margenCamara) / aspecto, dy * 0.5f + ajustes.margenCamara);
        tamano = Mathf.Min(tamano, ajustes.tamanoMaxCamara);
        CamaraDinamica.Ampliar(tamano, 0.25f);
        // El centro, entre los dos y algo por encima: el suelo queda abajo, no en medio.
        Vector2 punto = (Vector2)transform.position + Vector2.up * (2f * tamano * ajustes.subirEncuadre);
        CamaraDinamica.Encuadrar(punto, 0.5f, 0.25f, dx * 0.5f + 1f);
    }

    // A donde se va cuando se aleja: hacia el lado de la arena con mas sitio, pero
    // nunca a mas de distanciaAlejarse de ti (el otro extremo de la pantalla, no
    // de una arena enorme).
    private float DestinoAlejarse()
    {
        BuscarPlayer();
        int dir = arena.width > 0f ? (PosPlayer.x < arena.center.x ? 1 : -1) : (DxPlayer > 0f ? -1 : 1);
        float x = PosPlayer.x + dir * ajustes.distanciaAlejarse;
        return arena.width > 0f ? LimitarX(x) : x;
    }

    // ------------------------------------------------------------------ Amuleto sellado

    // Los amuletos que puede sellarte (los equipados, salvo los de vida/estamina maxima).
    private string AmuletoParaSellar()
    {
        var lista = new List<string>();
        for (int i = 0; i < Amuletos.HuecosAbiertos; i++)
        {
            string id = Amuletos.Equipado(i);
            if (string.IsNullOrEmpty(id) || id == EfectosAmuletos.Corazon || id == EfectosAmuletos.Costillar) continue;
            if (EfectosAmuletos.EstaSellado(id)) continue;
            lista.Add(id);
        }
        return lista.Count > 0 ? lista[Random.Range(0, lista.Count)] : null;
    }

    private IEnumerator SelloAmuleto()
    {
        finEnfriamientoSello = Time.time + ajustes.enfriamientoSello;
        BuscarPlayer();
        MirarYa();
        Parar();
        CambiarEstado(EstadoEnemigo.Anticipacion);
        anim.Reproducir(ClipBuff, true);
        Sonar("jefe_revivir", 0.7f, 1.2f);
        for (float t = 0f; t < ajustes.avisoSello; t += Time.deltaTime)
        {
            ritmo.Destello(ajustes.colorSello, 0.25f + 0.35f * (0.5f + 0.5f * Mathf.Sin(t * 16f)));
            Parar();
            MirarYa();
            if (anim.Terminado) anim.Reproducir(ClipBuff, true);
            yield return null;
        }
        ritmo.Destello(ajustes.colorSello, 0f);
        string id = AmuletoParaSellar();
        if (id != null)
        {
            EfectosAmuletos.Sellar(id, ajustes.duracionSello);
            miSello = id;
            SellosPuestos++;
            AmuletosSellados.Add(id);
            FichaAmuletos.Amuleto a = FichaAmuletos.Get() != null ? FichaAmuletos.Get().Buscar(id) : null;
            string nombre = a != null ? a.nombre : id;
            Sonar("jefe_parry", 0.7f, 0.6f);
            ScreenFlash.Destello(new Color(ajustes.colorSello.r, ajustes.colorSello.g, ajustes.colorSello.b, 0.25f), 0.35f);
            if (player != null)
                TextoFlotante.Mostrar($"{ajustes.textoSello}: {nombre}", PosPlayer + Vector2.up * 1.4f, ajustes.colorSello, 1f);
            IndicadorSelloBK.Mostrar(a != null ? a.icono : null, nombre, ajustes.colorSello);
        }
        while (!anim.Terminado) yield return null;
        yield return Quieto(0.5f / Vel);
    }

    private void QuitarMiSello()
    {
        if (string.IsNullOrEmpty(miSello)) return;
        if (EfectosAmuletos.Sellado == miSello) EfectosAmuletos.QuitarSello();
        miSello = null;
    }

    // ------------------------------------------------------------------ Erupciones

    private IEnumerator Erupciones()
    {
        finEnfriamientoErupciones = Time.time + ajustes.enfriamientoErupciones;
        enErupciones = true;
        camaraAmplia = true;
        yield return IrAlExtremo();
        MirarYa();
        Parar();
        CambiarEstado(EstadoEnemigo.Anticipacion);
        anim.Reproducir(ClipBuff, true);
        Sonar("jefe_caida");
        CamaraDinamica.Sacudir(0.4f);

        float inicio = Time.time;
        while (Time.time - inicio < ajustes.duracionErupciones)
        {
            LanzarErupcion();
            float espera = Random.Range(ajustes.intervaloErupciones.x, ajustes.intervaloErupciones.y) / Mathf.Max(1f, Desafio.MultVelocidad);
            for (float t = 0f; t < espera; t += Time.deltaTime)
            {
                if (anim.Terminado) anim.Reproducir(ClipBuff, true);
                ritmo.Destello(ajustes.colorErupcion, 0.15f + 0.15f * Mathf.Sin((Time.time - inicio) * 9f));
                Parar();
                MirarYa();
                yield return null;
            }
        }
        ritmo.Destello(ajustes.colorErupcion, 0f);
        // Espera a que acaben las que quedan y salta sobre ti.
        for (float t = 0f; t < 3f && ErupcionesVivas > 0; t += Time.deltaTime) { Parar(); yield return null; }
        enErupciones = false;
        yield return SaltoCaida();
        camaraAmplia = false;
    }

    public int ErupcionesVivas { get { erupciones.RemoveAll(e => e == null); return erupciones.Count; } }

    // Una erupcion: a veces justo donde estas; si no, en cualquier sitio de la arena.
    private void LanzarErupcion()
    {
        BuscarPlayer();
        float ancho = Random.Range(ajustes.anchoErupcion.x, ajustes.anchoErupcion.y);
        float alto = Random.Range(ajustes.altoErupcion.x, ajustes.altoErupcion.y);
        float x;
        if (player != null && Random.value < ajustes.probBajoJugador) x = PosPlayer.x + Random.Range(-0.4f, 0.4f);
        else if (arena.width > 0f) x = Random.Range(arena.xMin + ajustes.margenArena, arena.xMax - ajustes.margenArena);
        else x = PosPlayer.x + Random.Range(-8f, 8f);
        // Nunca encima del propio caballero.
        if (Mathf.Abs(x - transform.position.x) < ancho * 0.5f + 0.8f) x = transform.position.x + (PosPlayer.x >= transform.position.x ? 1 : -1) * (ancho * 0.5f + 1.2f);
        x = LimitarX(x);
        float y = PeligrosJefe.SueloBajo(new Vector2(x, Mathf.Max(PosPlayer.y, transform.position.y) + 1.5f), transform.position.y);
        erupciones.Add(ErupcionBK.Crear(this, new Vector2(x, y), ancho, alto, ajustes));
        ErupcionesLanzadas++;
    }

    // Se va al extremo de la arena mas lejano a ti, corriendo y sin que le entre nada.
    private IEnumerator IrAlExtremo()
    {
        retirandose = true;
        invulnerable = true;
        BuscarPlayer();
        float destino = DestinoAlejarse();
        int dir = destino > transform.position.x ? 1 : -1;
        anim.Reproducir(ClipCorrer, true);
        AtravesarPlayer(true);
        for (float t = 0f; t < 3f && (destino - transform.position.x) * dir > 0.15f; t += Time.deltaTime)
        {
            Mirar(dir);
            velX = dir * ajustes.velocidadCorrer * 1.7f;
            rb.linearVelocity = new Vector2(velX, rb.linearVelocity.y);
            if (HayParedHacia(dir, 0.4f, 0.5f)) break;
            yield return null;
        }
        AtravesarPlayer(false);
        Parar();
        invulnerable = false;
        retirandose = false;
    }

    private void LimpiarErupciones()
    {
        foreach (ErupcionBK e in erupciones) if (e != null) Destroy(e.gameObject);
        erupciones.Clear();
    }
}
