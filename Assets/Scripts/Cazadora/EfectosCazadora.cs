using System;
using System.Collections.Generic;
using UnityEngine;

// Efectos de la Cazadora: estelas, olas de luz, marcas en el suelo, tajos que
// caen, trampas de sonido, llamas, polvo y el anillo del oido. Todos salen de
// una reserva (pool): se apagan al terminar y se reutilizan, nunca se crean y
// destruyen en cada uso.

// ------------------------------------------------------------------ Reserva

public static class PoolCazadora
{
    private static Transform raiz;
    private static readonly Dictionary<Type, Stack<Component>> libres = new Dictionary<Type, Stack<Component>>();
    private static readonly List<Component> activos = new List<Component>();

    private static Transform Raiz
    {
        get
        {
            if (raiz == null)
            {
                raiz = new GameObject("PoolCazadora").transform;
                libres.Clear();
                activos.Clear();
            }
            return raiz;
        }
    }

    public static T Sacar<T>(Func<GameObject, T> crear) where T : Component
    {
        Transform r = Raiz;
        T c = null;
        if (libres.TryGetValue(typeof(T), out Stack<Component> pila))
            while (pila.Count > 0 && c == null) c = pila.Pop() as T;
        if (c == null)
        {
            GameObject go = new GameObject(typeof(T).Name);
            go.transform.SetParent(r, false);
            c = crear(go);
        }
        c.gameObject.SetActive(true);
        activos.Add(c);
        return c;
    }

    public static void Devolver(Component c)
    {
        if (c == null) return;
        c.gameObject.SetActive(false);
        activos.Remove(c);
        if (!libres.TryGetValue(c.GetType(), out Stack<Component> pila)) libres[c.GetType()] = pila = new Stack<Component>();
        if (!pila.Contains(c)) pila.Push(c);
    }

    // Apaga todo lo que haya en pantalla (reinicio del combate, victoria).
    public static void Limpiar()
    {
        for (int i = activos.Count - 1; i >= 0; i--)
        {
            Component c = activos[i];
            if (c == null) { activos.RemoveAt(i); continue; }
            Devolver(c);
        }
    }

    public static int Activos => activos.Count;
}

// ------------------------------------------------------------------ Dibujos

// Formas sencillas hechas por codigo (no hay sprites para ellas en el pack).
public static class DibujosCazadora
{
    private static Sprite pixel, circulo, anillo, oido, runa;

    public static Sprite Pixel()
    {
        if (pixel != null) return pixel;
        Texture2D t = Nueva(4, 4);
        Rellenar(t, (x, y) => Color.white);
        return pixel = Sprite.Create(t, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
    }

    // Circulo difuso (1 unidad de diametro): halos, luz, niebla.
    public static Sprite Circulo()
    {
        if (circulo != null) return circulo;
        const int n = 64;
        Texture2D t = Nueva(n, n);
        t.filterMode = FilterMode.Bilinear;
        Rellenar(t, (x, y) =>
        {
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f)) / (n / 2f);
            float a = Mathf.Clamp01(1f - d);
            return new Color(1f, 1f, 1f, a * a);
        });
        return circulo = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
    }

    // Aro fino de pixel art (1 unidad de diametro).
    public static Sprite Anillo()
    {
        if (anillo != null) return anillo;
        const int n = 28;
        Texture2D t = Nueva(n, n);
        Rellenar(t, (x, y) =>
        {
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f));
            return d <= n / 2f - 0.2f && d >= n / 2f - 2.2f ? Color.white : Color.clear;
        });
        return anillo = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 28f);
    }

    // Icono de oido (El Silencio, el ruido oido).
    public static Sprite Oido()
    {
        if (oido != null) return oido;
        string[] m =
        {
            "...#####....",
            "..#.....#...",
            ".#.......#..",
            ".#..###...#.",
            "#..#...#..#.",
            "#..#....#.#.",
            "#...#...#.#.",
            ".#...#..#...",
            ".#......#...",
            "..#....#....",
            "..#...#.....",
            "...#.#......",
            "...###......",
        };
        oido = DeMapa(m, 28f);
        return oido;
    }

    // Simbolo tenue de las trampas de sonido.
    public static Sprite Runa()
    {
        if (runa != null) return runa;
        string[] m =
        {
            "..#......#..",
            ".#.#....#.#.",
            "#...#..#...#",
            ".#...##...#.",
            "..#..##..#..",
            "...#....#...",
            "############",
        };
        runa = DeMapa(m, 28f);
        return runa;
    }

    private static Sprite DeMapa(string[] m, float ppu)
    {
        int w = m[0].Length, h = m.Length;
        Texture2D t = Nueva(w, h);
        Rellenar(t, (x, y) => m[h - 1 - y][x] == '#' ? Color.white : Color.clear);
        return Sprite.Create(t, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), ppu);
    }

    private static Texture2D Nueva(int w, int h)
    {
        Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, false);
        t.filterMode = FilterMode.Point;
        t.wrapMode = TextureWrapMode.Clamp;
        return t;
    }

    private static void Rellenar(Texture2D t, Func<int, int, Color> f)
    {
        Color[] px = new Color[t.width * t.height];
        for (int y = 0; y < t.height; y++)
        for (int x = 0; x < t.width; x++) px[y * t.width + x] = f(x, y);
        t.SetPixels(px);
        t.Apply();
    }

    public static SpriteRenderer Renderer(GameObject go, string capa, int orden)
    {
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sortingLayerName = capa;
        sr.sortingOrder = orden;
        sr.sharedMaterial = EfectoVisual.MaterialSinLuz();
        return sr;
    }
}

// ------------------------------------------------------------------ Golpe al player

public static class GolpeCazadora
{
    private static int mascaraPlayer = -1;

    public static PlayerControler PlayerEn(Vector2 centro, Vector2 tamano)
    {
        if (mascaraPlayer < 0) mascaraPlayer = LayerMask.GetMask("Player");
        foreach (Collider2D c in Physics2D.OverlapBoxAll(centro, tamano, 0f, mascaraPlayer == 0 ? ~0 : mascaraPlayer))
        {
            if (!c.CompareTag("Player")) continue;
            PlayerControler p = c.GetComponent<PlayerControler>();
            if (p != null) return p;
        }
        return null;
    }

    // Golpea al player si esta en la caja. "noLetal": deja al menos 1 de vida
    // (los ataques normales nunca matan con la vida llena). "imparable": no se
    // puede bloquear ni parar (instakills): solo se esquiva.
    public static PlayerControler.ResultadoDano? Caja(Vector2 centro, Vector2 tamano, int dano, Component atacante,
                                                     PlayerControler.TipoDano tipo, EstadoPlayer estado = EstadoPlayer.Ninguno,
                                                     float acumulacion = 0f, bool noLetal = false, bool imparable = false)
    {
        PlayerControler p = PlayerEn(centro, tamano);
        if (p == null) return null;
        return Aplicar(p, dano, atacante, tipo, estado, acumulacion, noLetal, imparable);
    }

    public static PlayerControler.ResultadoDano Aplicar(PlayerControler p, int dano, Component atacante, PlayerControler.TipoDano tipo,
                                                       EstadoPlayer estado, float acumulacion, bool noLetal, bool imparable)
    {
        PlayerControler.SiguienteNoLetal = noLetal;
        PlayerControler.ResultadoDano r = p.TakeDamage(dano, imparable ? null : atacante, tipo, estado, acumulacion);
        PlayerControler.SiguienteNoLetal = false;
        if (DepuracionCazadora.VerCajas) CajaDebug.Mostrar(p.transform.position, Vector2.one * 0.3f, Color.yellow);
        return r;
    }
}

// Cajas de dano visibles (herramienta de depuracion).
public class CajaDebug : MonoBehaviour
{
    private SpriteRenderer sr;
    private float fin;

    public static void Mostrar(Vector2 centro, Vector2 tamano, Color color, float segundos = 0.08f)
    {
        CajaDebug c = PoolCazadora.Sacar(go => { CajaDebug d = go.AddComponent<CajaDebug>(); d.sr = DibujosCazadora.Renderer(go, "VFX", 90); d.sr.sprite = DibujosCazadora.Pixel(); return d; });
        c.transform.position = centro;
        c.transform.localScale = new Vector3(tamano.x, tamano.y, 1f);
        color.a = 0.35f;
        c.sr.color = color;
        c.fin = Time.time + segundos;
    }

    private void Update()
    {
        if (Time.time >= fin) PoolCazadora.Devolver(this);
    }
}

// ------------------------------------------------------------------ Estela

// Imagen fantasma del cuerpo que se desvanece: sensacion de velocidad en los
// dashes (y disimula que el pack tiene pocos cuadros).
public class EstelaFantasma : MonoBehaviour
{
    private SpriteRenderer sr;
    private float vida, t;
    private Color color;

    public static void Lanzar(Sprite s, Vector3 pos, bool volteado, Color c, float segundos, int orden = -4)
    {
        if (s == null) return;
        EstelaFantasma e = PoolCazadora.Sacar(go => { EstelaFantasma x = go.AddComponent<EstelaFantasma>(); x.sr = DibujosCazadora.Renderer(go, "Player", orden); return x; });
        e.transform.position = pos;
        e.transform.localScale = new Vector3(volteado ? -1f : 1f, 1f, 1f);
        e.sr.sprite = s;
        e.sr.sortingOrder = orden;
        e.color = c;
        e.vida = Mathf.Max(0.05f, segundos);
        e.t = 0f;
        e.sr.color = c;
    }

    private void Update()
    {
        t += Time.deltaTime;
        float u = t / vida;
        Color c = color;
        c.a = color.a * (1f - u) * (1f - u);
        sr.color = c;
        if (u >= 1f) PoolCazadora.Devolver(this);
    }
}

// ------------------------------------------------------------------ Anillo del oido

public class AnilloRuido : MonoBehaviour
{
    private SpriteRenderer aro, icono;
    private float t, fuerza;

    public static void Mostrar(Vector2 pos, float volumen)
    {
        AnilloRuido a = PoolCazadora.Sacar(go =>
        {
            AnilloRuido x = go.AddComponent<AnilloRuido>();
            x.aro = DibujosCazadora.Renderer(go, "VFX", 40);
            x.aro.sprite = DibujosCazadora.Anillo();
            GameObject i = new GameObject("Oido");
            i.transform.SetParent(go.transform, false);
            x.icono = DibujosCazadora.Renderer(i, "VFX", 41);
            x.icono.sprite = DibujosCazadora.Oido();
            return x;
        });
        a.transform.position = pos;
        a.t = 0f;
        a.fuerza = Mathf.Clamp(volumen, 0.3f, 1.2f);
        a.icono.transform.localPosition = new Vector3(0f, 0.9f, 0f);
    }

    private void Update()
    {
        t += Time.deltaTime;
        float u = Mathf.Clamp01(t / 0.55f);
        float d = Mathf.Lerp(0.4f, 1.1f + fuerza * 0.5f, 1f - (1f - u) * (1f - u));
        aro.transform.localScale = new Vector3(d, d * 0.35f, 1f);
        float a = (1f - u) * 0.75f;
        aro.color = new Color(0.85f, 0.95f, 1f, a);
        icono.color = new Color(0.85f, 0.95f, 1f, Mathf.Clamp01((1f - u) * 1.4f) * 0.9f);
        if (u >= 1f) PoolCazadora.Devolver(this);
    }
}

// ------------------------------------------------------------------ Ola de luz

// Ola baja que recorre el suelo (el barrido lanzado). Se salta. Un parry la
// deshace. Del color del elemento de la fase: el fuego deja llamas, el hielo
// congela, el sagrado va mas rapido y es mas ancha, la sangre la cura.
public class OlaLuz : MonoBehaviour
{
    private SpriteRenderer sr;
    private int dir, dano;
    private float velocidad, alcance, recorrido, xMin, xMax, siguienteLlama;
    private Rect caja;
    private bool pego, acabando;
    private float fundido;
    private Elemento elemento;
    private AjustesCazadora aj;
    private bool noLetal;
    private Action<PlayerControler.ResultadoDano> alGolpear;
    private float escala;

    public static OlaLuz Lanzar(Sprite dibujo, Rect cajaEfecto, Vector2 pos, int dir, float velocidad, float alcance, int dano,
                                Color color, Elemento elemento, AjustesCazadora aj, float xMin, float xMax, bool noLetal,
                                Action<PlayerControler.ResultadoDano> alGolpear, float escala = 1f)
    {
        OlaLuz o = PoolCazadora.Sacar(go => { OlaLuz x = go.AddComponent<OlaLuz>(); x.sr = DibujosCazadora.Renderer(go, "VFX", 12); return x; });
        o.transform.position = pos;
        o.escala = escala;
        o.transform.localScale = new Vector3(dir * escala, escala, 1f);
        o.sr.sprite = dibujo;
        o.sr.color = color;
        o.dir = dir;
        o.velocidad = velocidad;
        o.alcance = alcance;
        o.dano = dano;
        o.caja = cajaEfecto;
        o.recorrido = 0f;
        o.pego = o.acabando = false;
        o.fundido = 0f;
        o.elemento = elemento;
        o.aj = aj;
        o.xMin = xMin;
        o.xMax = xMax;
        o.noLetal = noLetal;
        o.alGolpear = alGolpear;
        o.siguienteLlama = 0.6f;
        return o;
    }

    private void Update()
    {
        if (acabando)
        {
            fundido += Time.deltaTime / 0.2f;
            Color c = sr.color; c.a = Mathf.Max(0f, 1f - fundido); sr.color = c;
            if (fundido >= 1f) PoolCazadora.Devolver(this);
            return;
        }
        float paso = velocidad * Time.deltaTime;
        Vector3 p = transform.position + Vector3.right * dir * paso;
        transform.position = p;
        recorrido += paso;
        // Se deshace al llegar a la pared de la arena o al acabar su alcance.
        if (recorrido >= alcance || p.x <= xMin || p.x >= xMax) { acabando = true; return; }

        if (elemento == Elemento.Fuego && recorrido >= siguienteLlama)
        {
            siguienteLlama = recorrido + 1.1f;
            LlamaSuelo.Poner(new Vector2(p.x - dir * 0.6f, p.y), aj.duracionLlamas, Mathf.Max(1, dano / 6), aj.acumulacionEstado * 0.4f);
        }

        if (pego || caja.width <= 0f) return;
        // Solo la parte baja del dibujo (la ola en si): se salta por encima.
        Vector2 centro = new Vector2(p.x + dir * caja.center.x * escala, p.y + Mathf.Min(caja.center.y, 0.45f) * escala);
        Vector2 tam = new Vector2(caja.width * escala * 0.85f, Mathf.Min(caja.height, 0.9f) * escala);
        if (DepuracionCazadora.VerCajas) CajaDebug.Mostrar(centro, tam, Color.cyan, 0.02f);
        EstadoPlayer estado = EstadoDe(elemento);
        var r = GolpeCazadora.Caja(centro, tam, dano, this, PlayerControler.TipoDano.Magico, estado, aj != null ? aj.acumulacionEstado : 0f, noLetal);
        if (!r.HasValue) return;
        pego = true;
        alGolpear?.Invoke(r.Value);
        if (r.Value == PlayerControler.ResultadoDano.Parry) acabando = true;
    }

    public static EstadoPlayer EstadoDe(Elemento e)
    {
        switch (e)
        {
            case Elemento.Fuego: return EstadoPlayer.Quemadura;
            case Elemento.Hielo: return EstadoPlayer.Congelacion;
            case Elemento.Sangrado: return EstadoPlayer.Sangrado;
            default: return EstadoPlayer.Ninguno;
        }
    }
}

// ------------------------------------------------------------------ Marcas

// Aviso en el suelo: una linea (Ejecucion), una franja (tajos que caen) o un aro
// que te sigue (Sentencia). Late mientras dura. Siempre por encima de todo lo
// demas: el aviso nunca queda tapado.
public class MarcaSuelo : MonoBehaviour
{
    public enum Forma { Franja, Aro }

    private SpriteRenderer sr, columna;
    private float fin, inicio;
    private Color color;
    private Transform seguir;
    private bool siguiendo;
    private Forma forma;
    private Vector2 tamano;

    public bool Activa => gameObject.activeSelf;

    public static MarcaSuelo Poner(Forma forma, Vector2 pos, Vector2 tamano, Color color, float segundos)
    {
        MarcaSuelo m = PoolCazadora.Sacar(go =>
        {
            MarcaSuelo x = go.AddComponent<MarcaSuelo>();
            GameObject f = new GameObject("Forma");
            f.transform.SetParent(go.transform, false);
            x.sr = DibujosCazadora.Renderer(f, "VFX", 60);
            GameObject c = new GameObject("Columna");
            c.transform.SetParent(go.transform, false);
            x.columna = DibujosCazadora.Renderer(c, "VFX", 59);
            x.columna.sprite = DibujosCazadora.Pixel();
            return x;
        });
        m.forma = forma;
        m.tamano = tamano;
        m.transform.position = pos;
        m.sr.sprite = forma == Forma.Aro ? DibujosCazadora.Anillo() : DibujosCazadora.Pixel();
        m.sr.transform.localScale = forma == Forma.Aro ? new Vector3(tamano.x, tamano.x * 0.4f, 1f) : new Vector3(tamano.x, tamano.y, 1f);
        m.columna.enabled = forma == Forma.Aro;
        m.color = color;
        m.inicio = Time.time;
        m.fin = segundos > 0f ? Time.time + segundos : float.MaxValue;
        m.seguir = null;
        m.siguiendo = false;
        return m;
    }

    // La marca sigue a algo (el player) hasta que se fije.
    public void Seguir(Transform t) { seguir = t; siguiendo = t != null; }
    public void Fijar() { siguiendo = false; inicio = Time.time; }
    public Vector2 Posicion => transform.position;
    public void Quitar() => PoolCazadora.Devolver(this);

    private void Update()
    {
        if (siguiendo && seguir != null)
        {
            Vector3 p = transform.position;
            p.x = Mathf.Lerp(p.x, seguir.position.x, 1f - Mathf.Exp(-12f * Time.deltaTime));
            transform.position = p;
        }
        float t = Time.time - inicio;
        // Late: mas rapido cuanto mas cerca del final.
        float queda = fin == float.MaxValue ? 1f : Mathf.Clamp01((fin - Time.time) / Mathf.Max(0.01f, fin - inicio));
        float pulso = 0.55f + 0.45f * Mathf.Sin(t * Mathf.Lerp(26f, 9f, queda));
        Color c = color;
        c.a = color.a * pulso * (siguiendo ? 0.75f : 1f);
        sr.color = c;
        if (columna.enabled)
        {
            columna.transform.localScale = new Vector3(0.06f + (siguiendo ? 0f : 0.05f), 14f, 1f);
            columna.transform.localPosition = new Vector3(0f, 7f, 0f);
            Color cc = color; cc.a = color.a * 0.35f * pulso;
            columna.color = cc;
        }
        if (Time.time >= fin) PoolCazadora.Devolver(this);
    }
}

// ------------------------------------------------------------------ Tajo que aparece

// Un tajo que aparece en un sitio (cuchillada fantasma, trampa, Sentencia):
// entra rapido, hace dano en su caja un instante y se desvanece.
public class TajoAparecido : MonoBehaviour
{
    private SpriteRenderer sr;
    private float t, caidaDesde;
    private Rect caja;
    private int dano;
    private bool hecho, imparable, noLetal;
    private Vector2 destino;
    private float escala, dir;
    private EstadoPlayer estado;
    private float acumulacion;
    private Action<PlayerControler.ResultadoDano?> alTerminar;
    private const float Entrada = 0.1f, Vida = 0.45f;

    // "caida": sube tantas unidades y cae hasta el sitio (Sentencia, ilusiones).
    public static TajoAparecido Lanzar(Sprite dibujo, Rect cajaEfecto, Vector2 pos, int dir, float escala, Color color, int dano,
                                       bool imparable, bool noLetal, float caida = 0f, EstadoPlayer estado = EstadoPlayer.Ninguno,
                                       float acumulacion = 0f, Action<PlayerControler.ResultadoDano?> alTerminar = null)
    {
        TajoAparecido x = PoolCazadora.Sacar(go => { TajoAparecido a = go.AddComponent<TajoAparecido>(); a.sr = DibujosCazadora.Renderer(go, "VFX", 30); return a; });
        x.sr.sprite = dibujo;
        x.sr.color = color;
        x.caja = cajaEfecto;
        x.destino = pos;
        x.dir = dir;
        x.escala = escala;
        x.dano = dano;
        x.imparable = imparable;
        x.noLetal = noLetal;
        x.caidaDesde = caida;
        x.estado = estado;
        x.acumulacion = acumulacion;
        x.alTerminar = alTerminar;
        x.t = 0f;
        x.hecho = false;
        x.transform.localScale = new Vector3(dir * escala, escala, 1f);
        x.transform.position = pos + Vector2.up * caida;
        return x;
    }

    private void Update()
    {
        t += Time.deltaTime;
        float u = Mathf.Clamp01(t / Entrada);
        transform.position = destino + Vector2.up * caidaDesde * (1f - u * u);
        Color c = sr.color;
        c.a = t < Entrada ? u : Mathf.Clamp01(1f - (t - Entrada) / (Vida - Entrada));
        sr.color = c;
        if (!hecho && t >= Entrada)
        {
            hecho = true;
            Vector2 centro = destino + new Vector2(dir * caja.center.x * escala, caja.center.y * escala);
            Vector2 tam = new Vector2(caja.width, caja.height) * escala;
            if (DepuracionCazadora.VerCajas) CajaDebug.Mostrar(centro, tam, Color.red, 0.15f);
            var r = GolpeCazadora.Caja(centro, tam, dano, this, PlayerControler.TipoDano.Magico, estado, acumulacion, noLetal, imparable);
            alTerminar?.Invoke(r);
        }
        if (t >= Vida) PoolCazadora.Devolver(this);
    }
}

// ------------------------------------------------------------------ Trampas de sonido

// Simbolo tenue en el suelo. Si lo pisas, ella lo oye y ataca al instante.
public class TrampaSonido : MonoBehaviour
{
    private SpriteRenderer sr;
    private float fin, ancho;
    private Action<Vector2> alPisar;

    public static void Poner(Vector2 pos, float ancho, float segundos, Action<Vector2> alPisar)
    {
        TrampaSonido x = PoolCazadora.Sacar(go => { TrampaSonido a = go.AddComponent<TrampaSonido>(); a.sr = DibujosCazadora.Renderer(go, "VFX", 8); a.sr.sprite = DibujosCazadora.Runa(); return a; });
        x.transform.position = pos + Vector2.up * 0.13f;
        x.ancho = ancho;
        x.fin = Time.time + segundos;
        x.alPisar = alPisar;
    }

    private void Update()
    {
        float a = 0.22f + 0.1f * Mathf.Sin(Time.time * 3f + transform.position.x);
        sr.color = new Color(0.7f, 0.85f, 1f, a);
        if (Time.time >= fin) { PoolCazadora.Devolver(this); return; }
        PlayerControler p = GolpeCazadora.PlayerEn((Vector2)transform.position + Vector2.up * 0.35f, new Vector2(ancho, 0.5f));
        if (p == null || !p.EnSuelo) return;
        Action<Vector2> a2 = alPisar;
        PoolCazadora.Devolver(this);
        a2?.Invoke(transform.position);
    }
}

// ------------------------------------------------------------------ Llamas

// Llamas breves que deja el fuego en el suelo: quemadura si te quedas encima.
public class LlamaSuelo : MonoBehaviour
{
    private SpriteRenderer sr;
    private ParticleSystem ps;
    private float fin, siguiente;
    private int dano;
    private float acumulacion;

    public static void Poner(Vector2 pos, float segundos, int dano, float acumulacion)
    {
        LlamaSuelo x = PoolCazadora.Sacar(go =>
        {
            LlamaSuelo a = go.AddComponent<LlamaSuelo>();
            a.sr = DibujosCazadora.Renderer(go, "VFX", 6);
            a.sr.sprite = DibujosCazadora.Circulo();
            a.ps = ParticulasSimples.Crear(go.transform, new Color(1f, 0.55f, 0.12f), new Color(1f, 0.85f, 0.3f), 18f, 0.5f, 0.07f, true);
            return a;
        });
        x.transform.position = pos;
        x.fin = Time.time + segundos;
        x.dano = dano;
        x.acumulacion = acumulacion;
        x.siguiente = 0f;
        x.ps.Play();
    }

    private void Update()
    {
        float queda = fin - Time.time;
        sr.transform.localScale = new Vector3(1.1f, 0.35f, 1f);
        sr.color = new Color(1f, 0.45f, 0.1f, 0.5f * Mathf.Clamp01(queda * 2f) * (0.8f + 0.2f * Mathf.Sin(Time.time * 20f)));
        if (queda <= 0f) { ps.Stop(); PoolCazadora.Devolver(this); return; }
        if (Time.time < siguiente) return;
        var r = GolpeCazadora.Caja((Vector2)transform.position + Vector2.up * 0.3f, new Vector2(1f, 0.6f), dano, this,
                                   PlayerControler.TipoDano.Magico, EstadoPlayer.Quemadura, acumulacion, true);
        if (r.HasValue) siguiente = Time.time + 0.6f;
    }
}

// ------------------------------------------------------------------ Polvo

// Rafaga de polvo (muertes falsas, ilusiones que se deshacen, aterrizajes).
public class PolvoCazadora : MonoBehaviour
{
    private ParticleSystem ps;
    private float fin;

    public static void Soltar(Vector2 pos, Color color, int cantidad, float fuerza = 1f, bool sube = true)
    {
        PolvoCazadora x = PoolCazadora.Sacar(go =>
        {
            PolvoCazadora a = go.AddComponent<PolvoCazadora>();
            a.ps = ParticulasSimples.Crear(go.transform, Color.white, Color.white, 0f, 1f, 0.08f, false);
            return a;
        });
        x.transform.position = pos;
        var main = x.ps.main;
        main.startColor = new ParticleSystem.MinMaxGradient(color, new Color(color.r, color.g, color.b, color.a * 0.4f));
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f * fuerza, 2.2f * fuerza);
        main.gravityModifier = sube ? -0.08f : 0.25f;
        x.ps.Emit(cantidad);
        x.fin = Time.time + 2f;
    }

    private void Update()
    {
        if (Time.time >= fin) PoolCazadora.Devolver(this);
    }
}

// Sistemas de particulas sencillos hechos por codigo.
public static class ParticulasSimples
{
    public static ParticleSystem Crear(Transform padre, Color a, Color b, float porSegundo, float vida, float tamano, bool suben)
    {
        GameObject go = new GameObject("Particulas");
        go.transform.SetParent(padre, false);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(vida * 0.6f, vida);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(tamano * 0.6f, tamano);
        main.startColor = new ParticleSystem.MinMaxGradient(a, b);
        main.gravityModifier = suben ? -0.15f : 0.1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 120;
        var em = ps.emission;
        em.rateOverTime = porSegundo;
        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Circle;
        sh.radius = 0.3f;
        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = EfectoVisual.MaterialSinLuz();
        r.sortingLayerName = "VFX";
        r.sortingOrder = 10;
        return ps;
    }
}
