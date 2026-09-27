using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Ataques que el jefe deja en el escenario: la onda que recorre el suelo, los
// pilares que salen de el, los orbes que persiguen y los meteoros. Todos avisan
// antes de hacer dano (un glifo en el suelo, el propio recorrido visible...).
//
// Se registran en una lista para poder borrarlos de golpe al reiniciar el
// combate (muerte del player) o al vencer al jefe.
public static class PeligrosJefe
{
    private static readonly List<GameObject> vivos = new List<GameObject>();

    public static void Registrar(GameObject go)
    {
        vivos.RemoveAll(g => g == null);
        vivos.Add(go);
    }

    public static void LimpiarTodo()
    {
        foreach (GameObject g in vivos) if (g != null) Object.Destroy(g);
        vivos.Clear();
    }

    // Busca el suelo bajo un punto (para colocar glifos y pilares).
    public static float SueloBajo(Vector2 desde, float porDefecto)
    {
        RaycastHit2D r = Physics2D.Raycast(desde, Vector2.down, 30f, LayerMask.GetMask("Ground"));
        return r ? r.point.y : porDefecto;
    }

    // Aplica el golpe al player si esta dentro de la caja. Devuelve el resultado.
    public static PlayerControler.ResultadoDano? GolpearCaja(Vector2 centro, Vector2 tamano, int dano, Component atacante,
                                                            out PlayerControler player)
    {
        player = null;
        foreach (Collider2D c in Physics2D.OverlapBoxAll(centro, tamano, 0f))
        {
            if (!c.CompareTag("Player")) continue;
            player = c.GetComponent<PlayerControler>();
            if (player != null) return player.TakeDamage(dano, atacante);
        }
        return null;
    }
}

// Onda que recorre el suelo. Se esquiva saltando. Un parry la deshace.
public class OndaCarmesi : MonoBehaviour
{
    private int dir;
    private float velocidad, alcance, recorrido;
    private int dano;
    private Vector2 tamano;
    private bool pego, acabada;
    private EfectoVisual fx;

    public static void Lanzar(AnimadorHoja.Clip clip, Vector2 pos, int dir, float velocidad, int dano, float alcance, Color color, float escala)
    {
        EfectoVisual fx = EfectoVisual.Crear(clip, pos, escala, color, dir < 0, -1f, "VFX", 12);
        if (fx == null) return;
        OndaCarmesi o = fx.gameObject.AddComponent<OndaCarmesi>();
        o.fx = fx;
        o.dir = dir;
        o.velocidad = velocidad;
        o.dano = dano;
        o.alcance = alcance;
        o.tamano = new Vector2(1.5f, 1.1f) * escala;
        PeligrosJefe.Registrar(fx.gameObject);
    }

    private void Update()
    {
        if (acabada) return;
        float paso = velocidad * Time.deltaTime;
        Vector2 p = transform.position;

        // Contra una pared se deshace.
        if (Physics2D.Raycast(p + Vector2.up * 0.5f, Vector2.right * dir, paso + 0.3f, LayerMask.GetMask("Ground")))
        {
            Acabar();
            return;
        }

        transform.position = p + Vector2.right * dir * paso;
        recorrido += paso;
        if (recorrido >= alcance) { Acabar(); return; }

        if (pego) return;
        var r = PeligrosJefe.GolpearCaja((Vector2)transform.position + Vector2.up * tamano.y * 0.5f, tamano, dano, this, out _);
        if (!r.HasValue) return;
        pego = true;
        if (r.Value == PlayerControler.ResultadoDano.Parry) Acabar();
    }

    private void Acabar()
    {
        acabada = true;
        if (fx != null) fx.Desvanecer(0.25f);
    }
}

// Pilar que brota del suelo: primero un glifo que late (aviso), luego el pilar.
// Hiere y acumula sangrado.
public class PilarSangre : MonoBehaviour
{
    private AnimadorHoja.Clip clipPilar, clipGlifo;
    private float aviso, sangrado, alto;
    private int dano;
    private Color color;

    public static void Invocar(AnimadorHoja.Clip glifo, AnimadorHoja.Clip pilar, Vector2 suelo, float aviso, int dano,
                               float sangrado, Color color, float alto = 3.6f)
    {
        GameObject go = new GameObject("PilarSangre");
        go.transform.position = suelo;
        PilarSangre p = go.AddComponent<PilarSangre>();
        p.clipGlifo = glifo; p.clipPilar = pilar; p.aviso = aviso; p.dano = dano; p.sangrado = sangrado;
        p.color = color; p.alto = alto;
        PeligrosJefe.Registrar(go);
    }

    private IEnumerator Start()
    {
        Vector2 suelo = transform.position;

        // Glifo en el suelo, aplastado en perspectiva, latiendo cada vez mas rapido.
        EfectoVisual g = EfectoVisual.Crear(clipGlifo, suelo + Vector2.up * 0.1f, 1.6f, color, false, -1f, "VFX", 8);
        // Cuelga del pilar: si el pilar se borra a medias (reinicio del combate), el
        // glifo se va con el y no se queda en el suelo para siempre.
        if (g != null) g.transform.SetParent(transform, true);
        if (g != null) g.transform.localScale = new Vector3(1.1f, 0.55f, 1f);
        float t = 0f;
        while (t < aviso)
        {
            t += Time.deltaTime;
            if (g != null)
            {
                Color c = color;
                c.a = 0.35f + 0.65f * (0.5f + 0.5f * Mathf.Sin(t * Mathf.Lerp(12f, 40f, t / aviso)));
                g.Render.color = c;
            }
            yield return null;
        }
        if (g != null) Destroy(g.gameObject);

        // El pilar: crece desde el suelo.
        EfectoVisual pilar = EfectoVisual.Crear(clipPilar, suelo, alto / 3.6f, color, false, -1f, "VFX", 11);
        if (pilar != null) pilar.transform.SetParent(transform, true);
        Vector2 caja = new Vector2(1.1f, alto);
        bool pego = false;
        t = 0f;
        while (t < 0.55f)
        {
            t += Time.deltaTime;
            if (!pego && t > 0.08f)
            {
                var r = PeligrosJefe.GolpearCaja(suelo + Vector2.up * alto * 0.5f, caja, dano, this, out PlayerControler p);
                if (r.HasValue)
                {
                    pego = true;
                    if (r.Value == PlayerControler.ResultadoDano.Recibido) SangradoPlayer.Aplicar(p, sangrado);
                    else if (r.Value == PlayerControler.ResultadoDano.Bloqueado) SangradoPlayer.Aplicar(p, sangrado * 0.3f);
                }
            }
            yield return null;
        }
        Destroy(gameObject);
    }
}

// Orbe de sangre que persigue al player girando poco a poco. Si da, acumula mucho
// sangrado. Con parry sale devuelto contra el jefe.
public class OrbeSangre : MonoBehaviour
{
    private Vector2 dir;
    private float velocidad, giro, vida, sangrado;
    private int dano, danoDevuelto = 45;
    private bool devuelto, acabado;
    private Transform jefe, player;
    private AnimadorHoja.Clip clipEstallido;
    private Color color;
    private EfectoVisual fx;

    public static void Lanzar(AnimadorHoja.Clip clip, AnimadorHoja.Clip estallido, Vector2 pos, Vector2 dir, float velocidad,
                              float giro, float vida, int dano, float sangrado, Transform jefe, Color color)
    {
        EfectoVisual fx = EfectoVisual.Crear(clip, pos, 1.1f, color, false, -1f, "VFX", 13);
        if (fx == null) return;
        OrbeSangre o = fx.gameObject.AddComponent<OrbeSangre>();
        o.fx = fx; o.dir = dir.normalized; o.velocidad = velocidad; o.giro = giro; o.vida = vida;
        o.dano = dano; o.sangrado = sangrado; o.jefe = jefe; o.clipEstallido = estallido; o.color = color;
        o.Orientar();
        PeligrosJefe.Registrar(fx.gameObject);
    }

    private void Update()
    {
        if (acabado) return;
        vida -= Time.deltaTime;
        if (vida <= 0f) { Estallar(); return; }

        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }

        // Gira hacia su objetivo sin pasarse del giro maximo.
        Transform objetivo = devuelto ? jefe : player;
        if (objetivo != null)
        {
            Vector2 hacia = ((Vector2)objetivo.position + Vector2.up * (devuelto ? 2f : 0.2f) - (Vector2)transform.position).normalized;
            float ang = Vector2.SignedAngle(dir, hacia);
            float maxGiro = giro * Time.deltaTime;
            dir = Quaternion.Euler(0f, 0f, Mathf.Clamp(ang, -maxGiro, maxGiro)) * dir;
            Orientar();
        }

        float paso = velocidad * Time.deltaTime;
        if (Physics2D.Raycast(transform.position, dir, paso + 0.1f, LayerMask.GetMask("Ground"))) { Estallar(); return; }
        transform.position += (Vector3)(dir * paso);

        if (devuelto)
        {
            if (jefe != null && Vector2.Distance(transform.position, (Vector2)jefe.position + Vector2.up * 2f) < 1.3f)
            {
                EnemyHealth salud = jefe.GetComponent<EnemyHealth>();
                if (salud != null) salud.TakeDamage(danoDevuelto, transform.position);
                Estallar();
            }
            return;
        }

        var r = PeligrosJefe.GolpearCaja(transform.position, new Vector2(0.6f, 0.6f), dano, this, out PlayerControler pc);
        if (!r.HasValue) return;
        switch (r.Value)
        {
            case PlayerControler.ResultadoDano.Parry:
                devuelto = true;
                velocidad *= 1.8f;
                giro *= 3f;
                fx.Render.color = new Color(1f, 0.9f, 0.6f);
                break;
            case PlayerControler.ResultadoDano.Recibido:
                SangradoPlayer.Aplicar(pc, sangrado);
                Estallar();
                break;
            case PlayerControler.ResultadoDano.Bloqueado:
                SangradoPlayer.Aplicar(pc, sangrado * 0.3f);
                Estallar();
                break;
            // Ignorado: el barrido lo atraviesa, sigue.
        }
    }

    private void Orientar()
    {
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
    }

    private void Estallar()
    {
        if (acabado) return;
        acabado = true;
        EfectoVisual.Crear(clipEstallido, transform.position, 0.9f, color);
        Destroy(gameObject);
    }
}

// Meteoro: glifo en el suelo y, al acabar el aviso, cae en diagonal y estalla.
public class Meteoro : MonoBehaviour
{
    private AnimadorHoja.Clip clipMeteoro, clipExplosion, clipGlifo;
    private float aviso, techo;
    private int dano;
    private Color color;

    public static void Caer(AnimadorHoja.Clip meteoro, AnimadorHoja.Clip explosion, AnimadorHoja.Clip glifo,
                            Vector2 suelo, float techo, float aviso, int dano, Color color)
    {
        GameObject go = new GameObject("Meteoro");
        go.transform.position = suelo;
        Meteoro m = go.AddComponent<Meteoro>();
        m.clipMeteoro = meteoro; m.clipExplosion = explosion; m.clipGlifo = glifo;
        m.aviso = aviso; m.techo = techo; m.dano = dano; m.color = color;
        PeligrosJefe.Registrar(go);
    }

    private IEnumerator Start()
    {
        Vector2 suelo = transform.position;
        EfectoVisual g = EfectoVisual.Crear(clipGlifo, suelo + Vector2.up * 0.1f, 1.4f, color, false, -1f, "VFX", 8);
        if (g != null) g.transform.SetParent(transform, true);
        if (g != null) g.transform.localScale = new Vector3(1.5f, 0.7f, 1f);

        // El meteoro sale antes de que acabe el aviso y llega justo al final.
        const float caida = 0.45f;
        float t = 0f;
        while (t < aviso - caida)
        {
            t += Time.deltaTime;
            if (g != null) { Color c = color; c.a = 0.4f + 0.6f * (0.5f + 0.5f * Mathf.Sin(t * 25f)); g.Render.color = c; }
            yield return null;
        }

        Vector2 inicio = new Vector2(suelo.x + 2.5f, techo);
        Vector2 dir = (suelo - inicio).normalized;
        EfectoVisual m = EfectoVisual.Crear(clipMeteoro, inicio, 1.2f, Color.white, false, -1f, "VFX", 14,
                                            Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        if (m != null) m.transform.SetParent(transform, true);
        t = 0f;
        while (t < caida)
        {
            t += Time.deltaTime;
            if (m != null) m.transform.position = Vector2.Lerp(inicio, suelo, t / caida);
            yield return null;
        }
        if (m != null) Destroy(m.gameObject);
        if (g != null) Destroy(g.gameObject);

        EfectoVisual.Crear(clipExplosion, suelo + Vector2.up * 0.6f, 1.8f, Color.white);
        PeligrosJefe.GolpearCaja(suelo + Vector2.up * 0.7f, new Vector2(2.4f, 1.6f), dano, this, out _);
        Destroy(gameObject);
    }
}

// Media luna de sangre: vuela recta a la altura del pecho. Se salta, se atraviesa
// con el barrido o, con parry, se le devuelve al jefe.
public class MedialunaSangre : MonoBehaviour
{
    private int dir;
    private float velocidad;
    private int dano, danoDevuelto = 50;
    private bool devuelto, acabada;
    private Transform jefe;
    private EfectoVisual fx;
    private float vida = 4f;

    public static void Lanzar(AnimadorHoja.Clip clip, Vector2 pos, int dir, float velocidad, int dano, Transform jefe, Color color)
    {
        EfectoVisual fx = EfectoVisual.Crear(clip, pos, 1.3f, color, dir < 0, -1f, "VFX", 13);
        if (fx == null) return;
        MedialunaSangre m = fx.gameObject.AddComponent<MedialunaSangre>();
        m.fx = fx; m.dir = dir; m.velocidad = velocidad; m.dano = dano; m.jefe = jefe;
        PeligrosJefe.Registrar(fx.gameObject);
    }

    private void Update()
    {
        if (acabada) return;
        vida -= Time.deltaTime;
        float paso = velocidad * Time.deltaTime;
        if (vida <= 0f || Physics2D.Raycast(transform.position, Vector2.right * dir, paso + 0.3f, LayerMask.GetMask("Ground")))
        {
            Acabar();
            return;
        }
        transform.position += Vector3.right * dir * paso;

        if (devuelto)
        {
            if (jefe != null && Mathf.Abs(jefe.position.x - transform.position.x) < 1f)
            {
                EnemyHealth s = jefe.GetComponent<EnemyHealth>();
                if (s != null) s.TakeDamage(danoDevuelto, transform.position);
                Acabar();
            }
            return;
        }

        var r = PeligrosJefe.GolpearCaja(transform.position, new Vector2(0.8f, 1.7f), dano, this, out _);
        if (!r.HasValue) return;
        if (r.Value == PlayerControler.ResultadoDano.Parry)
        {
            // Sale devuelta hacia el jefe, mas rapida y dorada.
            devuelto = true;
            dir = -dir;
            velocidad *= 1.5f;
            vida = 4f;
            Vector3 e = transform.localScale; e.x = -e.x; transform.localScale = e;
            fx.Render.color = new Color(1f, 0.9f, 0.55f);
        }
        else if (r.Value != PlayerControler.ResultadoDano.Ignorado) Acabar();
    }

    private void Acabar()
    {
        acabada = true;
        if (fx != null) fx.Desvanecer(0.2f);
    }
}

// Vortice de sangre: aparece, atrae al player hacia su centro y, si le pilla
// dentro, le hiere y le llena de sangrado. Se sale corriendo en contra.
public class VorticeSangre : MonoBehaviour
{
    private AnimadorHoja.Clip inicio, bucle, fin;
    private float duracion, radio, fuerza, sangrado;
    private int dano;
    private Color color;

    public static void Invocar(AnimadorHoja.Clip inicio, AnimadorHoja.Clip bucle, AnimadorHoja.Clip fin, Vector2 pos,
                               float duracion, float radio, float fuerza, int dano, float sangrado, Color color)
    {
        GameObject go = new GameObject("VorticeSangre");
        go.transform.position = pos;
        VorticeSangre v = go.AddComponent<VorticeSangre>();
        v.inicio = inicio; v.bucle = bucle; v.fin = fin; v.duracion = duracion; v.radio = radio;
        v.fuerza = fuerza; v.dano = dano; v.sangrado = sangrado; v.color = color;
        PeligrosJefe.Registrar(go);
    }

    private IEnumerator Start()
    {
        Vector2 c = transform.position;
        EfectoVisual a = EfectoVisual.Crear(inicio, c, 2.2f, color);
        if (a != null) { a.transform.SetParent(transform, true); yield return new WaitForSeconds(inicio.Duracion); }

        EfectoVisual b = EfectoVisual.Crear(bucle, c, 2.2f, color, false, -1f);
        if (b != null) b.transform.SetParent(transform, true);
        float t = 0f, siguienteGolpe = 0f;
        while (t < duracion)
        {
            t += Time.deltaTime;
            GameObject go = GameObject.FindGameObjectWithTag("Player");
            if (go != null)
            {
                Rigidbody2D rb = go.GetComponent<Rigidbody2D>();
                Vector2 p = go.transform.position;
                float d = Vector2.Distance(p, c);
                if (d < radio && rb != null)
                {
                    // Tira del player hacia el centro, mas fuerte cuanto mas cerca.
                    float k = Mathf.Lerp(1f, 0.35f, d / radio);
                    float dx = Mathf.Sign(c.x - p.x) * fuerza * k * Time.deltaTime;
                    rb.position += new Vector2(dx, 0f);
                }
                if (d < 1.4f && Time.time >= siguienteGolpe)
                {
                    siguienteGolpe = Time.time + 0.8f;
                    var r = PeligrosJefe.GolpearCaja(c, new Vector2(2.2f, 2.6f), dano, this, out PlayerControler pc);
                    if (r == PlayerControler.ResultadoDano.Recibido) SangradoPlayer.Aplicar(pc, sangrado);
                }
            }
            yield return null;
        }

        if (b != null) Destroy(b.gameObject);
        EfectoVisual e = EfectoVisual.Crear(fin, c, 2.2f, color);
        yield return new WaitForSeconds(fin != null ? fin.Duracion : 0.3f);
        Destroy(gameObject);
    }
}
