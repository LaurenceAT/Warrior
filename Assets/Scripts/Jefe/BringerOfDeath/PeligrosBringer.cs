using System.Collections;
using UnityEngine;

// Hechizos que el Bringer of Death deja en la arena. Todos avisan antes de hacer
// dano (portal, anillo, marca en el suelo, su propio vuelo visible) y se
// registran en PeligrosJefe: se borran de golpe al reaparecer o al vencerlo.
// Cada uno golpea una sola vez.
public static class PeligrosBringer
{
    // Lo leen las pruebas: hechizos que te han alcanzado y el ultimo que lo hizo.
    public static int Golpes;
    public static string UltimoGolpeador = "";

    // Golpe de un hechizo: magico, y con estado si es el especial.
    public static PlayerControler.ResultadoDano? Golpear(Vector2 centro, Vector2 tamano, int dano, Component atacante,
                                                         bool desdeArriba, float origenX = float.NaN,
                                                         EstadoPlayer estado = EstadoPlayer.Ninguno, float acumulacion = 0f)
    {
        foreach (Collider2D c in Physics2D.OverlapBoxAll(centro, tamano, 0f))
        {
            if (!c.CompareTag("Player")) continue;
            PlayerControler p = c.GetComponent<PlayerControler>();
            if (p == null) continue;
            PlayerControler.SiguienteDesdeArriba = desdeArriba;
            PlayerControler.SiguienteOrigenX = origenX;
            Golpes++;
            UltimoGolpeador = atacante != null ? atacante.GetType().Name : "";
            return p.TakeDamage(dano, atacante, PlayerControler.TipoDano.Magico, estado, acumulacion);
        }
        return null;
    }

    public static bool VerCajas => DepuracionCazadora.VerCajas;

    public static void Caja(Vector2 centro, Vector2 tamano, Color c)
    {
        if (VerCajas) CajaDebug.Mostrar(centro, tamano, c, 0.03f);
    }

    public static GameObject Nuevo(string nombre, Vector2 pos)
    {
        GameObject go = new GameObject(nombre);
        go.transform.position = pos;
        PeligrosJefe.Registrar(go);
        return go;
    }

    // Efecto colgado del peligro (si el peligro se borra, el efecto se va con el).
    public static EfectoVisual Efecto(Transform padre, AnimadorHoja.Clip clip, Vector2 pos, float escala, Color color, bool voltear, int orden)
    {
        EfectoVisual fx = EfectoVisual.Crear(clip, pos, escala, color, voltear, -1f, "VFX", orden);
        if (fx != null && padre != null) fx.transform.SetParent(padre, true);
        return fx;
    }
}

// Mano del abismo: un portal sobre ti que te sigue un momento, se queda fijo y
// deja caer la mano negra (el hechizo propio del Bringer).
public class ManoAbismo : MonoBehaviour
{
    private AnimadorHoja.Clip portal, mano;
    private Transform objetivo;
    private float seguir, fijo, suelo;
    private int dano;
    private Vector2 caja;
    private Color color;
    public PlayerControler.ResultadoDano? Resultado { get; private set; }
    public bool Termino { get; private set; }

    public static ManoAbismo Invocar(AnimadorHoja.Clip portal, AnimadorHoja.Clip mano, Vector2 pos, Transform objetivo,
                                     float seguir, float fijo, int dano, Vector2 caja, Color color)
    {
        GameObject go = PeligrosBringer.Nuevo("ManoAbismo", pos);
        ManoAbismo m = go.AddComponent<ManoAbismo>();
        m.portal = portal; m.mano = mano; m.objetivo = objetivo; m.seguir = seguir; m.fijo = fijo;
        m.dano = dano; m.caja = caja; m.color = color; m.suelo = pos.y;
        return m;
    }

    private IEnumerator Start()
    {
        EfectoVisual p = PeligrosBringer.Efecto(transform, portal, transform.position, 1f, color, false, 9);
        Sonido.Reproducir("sombra_carga", 0.55f, 0.85f);
        for (float t = 0f; t < seguir; t += Time.deltaTime)
        {
            if (objetivo != null)
            {
                float x = Mathf.MoveTowards(transform.position.x, objetivo.position.x, 9f * Time.deltaTime);
                transform.position = new Vector2(x, PeligrosJefe.SueloBajo(new Vector2(x, objetivo.position.y + 1f), suelo));
            }
            yield return null;
        }
        // Fijo: el portal late mas rapido.
        for (float t = 0f; t < fijo; t += Time.deltaTime)
        {
            if (p != null) p.Render.color = Color.Lerp(color, Color.white, 0.5f + 0.5f * Mathf.Sin(t * 40f));
            yield return null;
        }
        if (p != null) Destroy(p.gameObject);

        EfectoVisual m = PeligrosBringer.Efecto(transform, mano, transform.position, 1f, color, false, 11);
        float fps = mano != null ? Mathf.Max(1f, mano.fps) : 12f;
        Vector2 centro = (Vector2)transform.position + Vector2.up * caja.y * 0.5f;
        Sonido.Reproducir("sombra_golpe", 0.7f, 0.8f);
        // La mano toca el suelo en su cuadro 2 y sube en el 7.
        for (float t = 0f; t < 12f / fps; t += Time.deltaTime)
        {
            float f = t * fps;
            if (!Resultado.HasValue && f >= 2f && f < 7f)
            {
                Resultado = PeligrosBringer.Golpear(centro, caja, dano, this, true);
                PeligrosBringer.Caja(centro, caja, Color.magenta);
            }
            yield return null;
        }
        Termino = true;
        Destroy(gameObject);
    }
}

// Estallido del Necromante: un anillo verde que late en el suelo (aviso) y
// luego revienta hacia arriba.
public class EstallidoNecro : MonoBehaviour
{
    private AnimadorHoja.Clip anillo, estallido;
    private float aviso;
    private int dano;
    private Vector2 caja;
    private Color color;

    public static EstallidoNecro Invocar(AnimadorHoja.Clip anillo, AnimadorHoja.Clip estallido, Vector2 suelo, float aviso,
                                         int dano, Vector2 caja, Color color)
    {
        GameObject go = PeligrosBringer.Nuevo("EstallidoNecro", suelo);
        EstallidoNecro e = go.AddComponent<EstallidoNecro>();
        e.anillo = anillo; e.estallido = estallido; e.aviso = aviso; e.dano = dano; e.caja = caja; e.color = color;
        return e;
    }

    private IEnumerator Start()
    {
        Vector2 p = transform.position;
        EfectoVisual a = PeligrosBringer.Efecto(transform, anillo, p, 1f, color, false, 8);
        for (float t = 0f; t < aviso; t += Time.deltaTime)
        {
            if (a != null)
            {
                Color c = color;
                c.a = 0.35f + 0.65f * (0.5f + 0.5f * Mathf.Sin(t * Mathf.Lerp(10f, 38f, t / aviso)));
                a.Render.color = c;
            }
            yield return null;
        }
        if (a != null) Destroy(a.gameObject);
        PeligrosBringer.Efecto(transform, estallido, p, 1f, color, false, 11);
        Sonido.Reproducir("jefe_impacto_suelo", 0.45f, 1.2f);
        Vector2 centro = p + Vector2.up * caja.y * 0.5f;
        bool pego = false;
        for (float t = 0f; t < 0.3f; t += Time.deltaTime)
        {
            if (!pego && t > 0.05f) pego = PeligrosBringer.Golpear(centro, caja, dano, this, true).HasValue;
            PeligrosBringer.Caja(centro, caja, Color.green);
            yield return null;
        }
        float resto = estallido != null ? estallido.Duracion - 0.3f : 0f;
        if (resto > 0f) yield return new WaitForSeconds(resto);
        Destroy(gameObject);
    }
}

// Un tramo de la ola de almas: humo verde que sale del suelo hacia un lado.
public class TramoOla : MonoBehaviour
{
    public static void Invocar(AnimadorHoja.Clip clip, Vector2 suelo, int dir, int dano, Vector2 caja, Color color)
    {
        GameObject go = PeligrosBringer.Nuevo("TramoOla", suelo);
        TramoOla o = go.AddComponent<TramoOla>();
        o.StartCoroutine(o.Hacer(clip, suelo, dir, dano, caja, color));
    }

    private IEnumerator Hacer(AnimadorHoja.Clip clip, Vector2 suelo, int dir, int dano, Vector2 caja, Color color)
    {
        PeligrosBringer.Efecto(transform, clip, suelo - Vector2.right * dir * 0.3f, 1f, color, dir < 0, 11);
        Vector2 centro = suelo + new Vector2(dir * caja.x * 0.5f, caja.y * 0.5f);
        bool pego = false;
        for (float t = 0f; t < 0.32f; t += Time.deltaTime)
        {
            if (!pego && t > 0.06f) pego = PeligrosBringer.Golpear(centro, caja, dano, this, false, suelo.x - dir).HasValue;
            PeligrosBringer.Caja(centro, caja, Color.green);
            yield return null;
        }
        float resto = clip != null ? clip.Duracion - 0.32f : 0f;
        if (resto > 0f) yield return new WaitForSeconds(resto);
        Destroy(gameObject);
    }
}

// Media luna verde: vuela recta a una altura. Un parry o una pared la deshacen.
public class MediaLunaNecro : MonoBehaviour
{
    private int dir, dano;
    private float velocidad, alcance, recorrido;
    private Vector2 caja;
    private bool acabada;
    private EfectoVisual fx;

    public static void Lanzar(AnimadorHoja.Clip clip, Vector2 pos, int dir, float velocidad, float alcance, int dano, Vector2 caja, Color color)
    {
        GameObject go = PeligrosBringer.Nuevo("MediaLunaNecro", pos);
        MediaLunaNecro m = go.AddComponent<MediaLunaNecro>();
        m.dir = dir; m.velocidad = velocidad; m.alcance = alcance; m.dano = dano; m.caja = caja;
        m.fx = PeligrosBringer.Efecto(go.transform, clip, pos, 1f, color, dir < 0, 12);
        Sonido.Reproducir("jefe_medialuna", 0.6f, 0.9f);
    }

    private void Update()
    {
        if (acabada) return;
        float paso = velocidad * Time.deltaTime;
        Vector2 p = transform.position;
        if (Physics2D.Raycast(p, Vector2.right * dir, paso + 0.3f, LayerMask.GetMask("Ground"))) { Acabar(); return; }
        transform.position = p + Vector2.right * dir * paso;
        recorrido += paso;
        if (recorrido >= alcance) { Acabar(); return; }
        PeligrosBringer.Caja(transform.position, caja, Color.green);
        var r = PeligrosBringer.Golpear(transform.position, caja, dano, this, false, transform.position.x - dir);
        if (r.HasValue) Acabar();
    }

    private void Acabar()
    {
        acabada = true;
        if (fx != null) fx.Desvanecer(0.2f);
        Destroy(gameObject, 0.25f);
    }
}

// Espiritu que te persigue girando poco a poco. Se deshace al tocarte, con un
// parry o al acabar su vida (se vuelve rojo y se apaga).
public class EspirituNecro : MonoBehaviour
{
    private Vector2 dir;
    private float velocidad, giro, vida;
    private int dano;
    private Transform objetivo;
    private AnimadorHoja.Clip clipFin;
    private Color color;
    private EfectoVisual fx;
    private bool acabado;
    public static int Vivos { get; private set; }

    public static void Lanzar(AnimadorHoja.Clip vuelo, AnimadorHoja.Clip fin, Vector2 pos, Vector2 dir, float velocidad, float giro,
                              float vida, int dano, Transform objetivo, Color color)
    {
        GameObject go = PeligrosBringer.Nuevo("EspirituNecro", pos);
        EspirituNecro e = go.AddComponent<EspirituNecro>();
        e.dir = dir.normalized; e.velocidad = velocidad; e.giro = giro; e.vida = vida; e.dano = dano;
        e.objetivo = objetivo; e.clipFin = fin; e.color = color;
        e.fx = PeligrosBringer.Efecto(go.transform, vuelo, pos, 0.8f, color, false, 13);
        Vivos++;
    }

    private void OnDestroy() { if (!acabado) Vivos--; }

    private void Update()
    {
        if (acabado) return;
        vida -= Time.deltaTime;
        if (vida <= 0f) { Acabar(); return; }
        if (objetivo != null)
        {
            Vector2 hacia = ((Vector2)objetivo.position + Vector2.up * 0.8f - (Vector2)transform.position).normalized;
            float a = Vector2.SignedAngle(dir, hacia);
            dir = Quaternion.Euler(0f, 0f, Mathf.Clamp(a, -giro * Time.deltaTime, giro * Time.deltaTime)) * dir;
        }
        transform.position += (Vector3)(dir * velocidad * Time.deltaTime);
        if (fx != null) fx.transform.localScale = new Vector3(dir.x < 0f ? -0.8f : 0.8f, 0.8f, 1f);
        Vector2 caja = new Vector2(0.7f, 0.7f);
        PeligrosBringer.Caja(transform.position, caja, Color.green);
        if (PeligrosBringer.Golpear(transform.position, caja, dano, this, false, transform.position.x - Mathf.Sign(dir.x)).HasValue) Acabar();
    }

    private void Acabar()
    {
        acabado = true;
        Vivos--;
        if (fx != null) Destroy(fx.gameObject);
        PeligrosBringer.Efecto(transform, clipFin, transform.position, 0.8f, color, dir.x < 0f, 13);
        Destroy(gameObject, clipFin != null ? clipFin.Duracion : 0.3f);
    }
}

// Cometa de la lluvia: una marca morada en el suelo (aviso) y, justo al acabar,
// el cometa llega en diagonal y estalla en estrellas.
public class CometaEstelar : MonoBehaviour
{
    public static void Caer(AnimadorHoja.Clip marca, AnimadorHoja.Clip cometa, AnimadorHoja.Clip estallido, Vector2 suelo,
                            float aviso, float velocidad, int lado, int dano, Vector2 caja, Color color)
    {
        GameObject go = PeligrosBringer.Nuevo("CometaEstelar", suelo);
        CometaEstelar c = go.AddComponent<CometaEstelar>();
        c.StartCoroutine(c.Hacer(marca, cometa, estallido, suelo, aviso, velocidad, lado, dano, caja, color));
    }

    private IEnumerator Hacer(AnimadorHoja.Clip marca, AnimadorHoja.Clip cometa, AnimadorHoja.Clip estallido, Vector2 suelo,
                              float aviso, float velocidad, int lado, int dano, Vector2 caja, Color color)
    {
        EfectoVisual m = PeligrosBringer.Efecto(transform, marca, suelo, 0.6f, color, false, 8);
        // Viene en diagonal desde el lado "lado" (el dibujo cae de derecha a izquierda).
        Vector2 dir = new Vector2(-lado * 0.55f, -1f).normalized;
        float vuelo = Mathf.Min(aviso, 0.45f);
        Vector2 desde = suelo - dir * velocidad * vuelo + Vector2.up * 0.6f;
        EfectoVisual c = null;
        for (float t = 0f; t < aviso; t += Time.deltaTime)
        {
            if (m != null) { Color k = color; k.a = 0.4f + 0.6f * Mathf.Abs(Mathf.Sin(t * 18f)); m.Render.color = k; }
            if (t >= aviso - vuelo)
            {
                if (c == null) c = PeligrosBringer.Efecto(transform, cometa, desde, 0.9f, color, lado < 0, 13);
                if (c != null) c.transform.position = Vector2.Lerp(desde, suelo + Vector2.up * 0.6f, (t - (aviso - vuelo)) / vuelo);
            }
            yield return null;
        }
        if (m != null) Destroy(m.gameObject);
        if (c != null) Destroy(c.gameObject);
        PeligrosBringer.Efecto(transform, estallido, suelo, 0.9f, color, false, 12);
        Sonido.Reproducir("jefe_impacto_suelo", 0.35f, 1.4f);
        Vector2 centro = suelo + Vector2.up * caja.y * 0.5f;
        bool pego = false;
        for (float t = 0f; t < 0.18f; t += Time.deltaTime)
        {
            if (!pego) pego = PeligrosBringer.Golpear(centro, caja, dano, this, true).HasValue;
            PeligrosBringer.Caja(centro, caja, Color.cyan);
            yield return null;
        }
        yield return new WaitForSeconds(estallido != null ? estallido.Duracion : 0.4f);
        Destroy(gameObject);
    }
}

// Esfera del vacio: se abre en el suelo, te atrae un rato y luego implosiona.
// Es su ataque especial: si te alcanza, ademas de dano, congelacion.
public class EsferaVacio : MonoBehaviour
{
    public bool Termino { get; private set; }
    public PlayerControler.ResultadoDano? Resultado { get; private set; }

    public static EsferaVacio Invocar(AnimadorHoja.Clip abrir, AnimadorHoja.Clip bucle, AnimadorHoja.Clip implosion, Vector2 suelo,
                                      float atraccion, float fuerza, float radio, int dano, Vector2 caja, float congelacion, Color color)
    {
        GameObject go = PeligrosBringer.Nuevo("EsferaVacio", suelo);
        EsferaVacio e = go.AddComponent<EsferaVacio>();
        e.StartCoroutine(e.Hacer(abrir, bucle, implosion, suelo, atraccion, fuerza, radio, dano, caja, congelacion, color));
        return e;
    }

    private IEnumerator Hacer(AnimadorHoja.Clip abrir, AnimadorHoja.Clip bucle, AnimadorHoja.Clip implosion, Vector2 suelo,
                              float atraccion, float fuerza, float radio, int dano, Vector2 caja, float congelacion, Color color)
    {
        EfectoVisual a = PeligrosBringer.Efecto(transform, abrir, suelo, 1.3f, color, false, 9);
        Sonido.Reproducir("jefe_nova", 0.6f, 0.7f);
        float dAbrir = abrir != null ? abrir.Duracion : 0.4f;
        yield return new WaitForSeconds(dAbrir);
        if (a != null) Destroy(a.gameObject);
        EfectoVisual b = PeligrosBringer.Efecto(transform, bucle, suelo, 1.3f, color, false, 9);
        PlayerControler p = FindFirstObjectByType<PlayerControler>();
        for (float t = 0f; t < atraccion; t += Time.deltaTime)
        {
            if (p != null)
            {
                float dx = suelo.x - p.transform.position.x;
                if (Mathf.Abs(dx) < radio && Mathf.Abs(dx) > 0.3f)
                    p.AplicarViento(new Vector2(Mathf.Sign(dx) * fuerza * (0.6f + 0.4f * t / atraccion), 0f));
            }
            if (b != null) b.transform.localScale = Vector3.one * 1.3f * (1f + 0.05f * Mathf.Sin(t * 20f));
            yield return null;
        }
        if (b != null) Destroy(b.gameObject);
        PeligrosBringer.Efecto(transform, implosion, suelo, 1.3f, color, false, 12);
        Sonido.Reproducir("jefe_transformacion", 0.5f, 1.3f);
        Vector2 centro = suelo + Vector2.up * caja.y * 0.5f;
        for (float t = 0f; t < 0.3f && !Resultado.HasValue; t += Time.deltaTime)
        {
            Resultado = PeligrosBringer.Golpear(centro, caja, dano, this, true, float.NaN, EstadoPlayer.Congelacion, congelacion);
            PeligrosBringer.Caja(centro, caja, Color.blue);
            yield return null;
        }
        Termino = true;
        yield return new WaitForSeconds(implosion != null ? implosion.Duracion : 0.5f);
        Destroy(gameObject);
    }
}
