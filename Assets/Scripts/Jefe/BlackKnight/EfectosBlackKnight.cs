using System.Collections;
using UnityEngine;

// Efectos del Black Knight (jefe nuevo, solo en la sala de pruebas).

// Onda de fuego: media luna alta dibujada por codigo en pixel art (como en el
// boceto: el borde curvo delante y estelas de fuego detras). Viaja por el suelo
// hacia un lado. No se puede parar ni bloquear: solo se atraviesa esquivando. Toca
// una sola vez; si la atraviesas esquivando ya no te toca.
public class OndaFuegoBK : MonoBehaviour
{
    private const float Ppu = 28f;
    private const int Ancho = 44, Alto = 160;
    private static Sprite dibujo;

    private JefeBlackKnight jefe;
    private AjustesBlackKnight aj;
    private int dir;
    private float recorrido, alcance;
    private bool usada;
    private SpriteRenderer sr;

    public bool Tocada { get; private set; }
    public bool Esquivada { get; private set; }

    public static OndaFuegoBK Lanzar(JefeBlackKnight jefe, Vector2 pies, int dir, AjustesBlackKnight aj, float alcance)
    {
        GameObject go = new GameObject("OndaFuegoBK");
        go.transform.position = pies;
        OndaFuegoBK o = go.AddComponent<OndaFuegoBK>();
        o.jefe = jefe;
        o.aj = aj;
        o.dir = dir >= 0 ? 1 : -1;
        o.alcance = alcance;
        o.sr = DibujosCazadora.Renderer(go, "VFX", 20);
        o.sr.sprite = Dibujo(aj);
        Vector2 nativo = new Vector2(Ancho / Ppu, Alto / Ppu);
        go.transform.localScale = new Vector3(o.dir * aj.tamanoOnda.x / nativo.x, aj.tamanoOnda.y / nativo.y, 1f);
        return o;
    }

    private void Update()
    {
        float paso = aj.velocidadOnda * Time.deltaTime;
        transform.position += new Vector3(dir * paso, 0f, 0f);
        recorrido += paso;
        // Parpadeo del fuego: un leve latido de escala y brillo.
        float l = 0.92f + 0.08f * Mathf.Sin(Time.time * 30f + transform.position.x);
        sr.color = new Color(1f, 1f, 1f, Mathf.Clamp01((alcance - recorrido) / 1.5f) * l);

        if (!usada && jefe != null)
        {
            Vector2 centro = (Vector2)transform.position + new Vector2(0f, aj.cajaOnda.y * 0.5f);
            PlayerControler p = GolpeCazadora.PlayerEn(centro, aj.cajaOnda);
            if (p != null)
            {
                usada = true;
                PlayerControler.ResultadoDano r = GolpeCazadora.Aplicar(p, aj.danoOnda, jefe, PlayerControler.TipoDano.Magico,
                                                                        EstadoPlayer.Quemadura, aj.quemaduraPorGolpe, false, true);
                if (r == PlayerControler.ResultadoDano.Ignorado) Esquivada = true;
                else Tocada = true;
            }
            if (DepuracionCazadora.VerCajas) CajaDebug.Mostrar(centro, aj.cajaOnda, aj.colorOnda, 0.03f);
        }
        if (recorrido >= alcance) Destroy(gameObject);
    }

    // La media luna: borde delantero curvo y brillante, cuerpo naranja que se
    // estrecha arriba y abajo, y estelas detras (hacia la izquierda: la onda va a
    // la derecha; se voltea con la escala). Se dibuja una vez y se reutiliza.
    private static Sprite Dibujo(AjustesBlackKnight aj)
    {
        if (dibujo != null) return dibujo;
        var tex = new Texture2D(Ancho, Alto, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[Ancho * Alto];
        Color borde = aj.colorOnda2, cuerpo = aj.colorOnda, estela = new Color(aj.colorOnda.r, aj.colorOnda.g * 0.8f, aj.colorOnda.b, 0.7f);
        var azar = new System.Random(7);
        for (int y = 0; y < Alto; y++)
        {
            float t = (y + 0.5f) / Alto * 2f - 1f;           // -1 abajo, 1 arriba
            float curva = 1f - t * t;                          // 0 en las puntas, 1 en medio
            int frente = Mathf.RoundToInt(10 + 26 * curva);    // x del borde delantero
            int grosor = Mathf.RoundToInt(2 + 7 * curva);
            for (int x = frente - grosor; x <= frente; x++)
            {
                if (x < 0 || x >= Ancho) continue;
                px[y * Ancho + x] = x >= frente - 1 ? (Color32)borde : (Color32)cuerpo;
            }
            // Estelas: trazos detras del cuerpo, cada pocas filas.
            if (y % 3 == 0 && azar.NextDouble() < 0.75 && curva > 0.08f)
            {
                int largo = 4 + azar.Next(0, 4 + Mathf.RoundToInt(14 * curva));
                int x0 = frente - grosor - 2 - azar.Next(0, 3);
                for (int k = 0; k < largo; k++)
                {
                    int x = x0 - k;
                    if (x < 0) break;
                    Color c = estela;
                    c.a *= 1f - (float)k / largo;
                    px[y * Ancho + x] = c;
                }
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        dibujo = Sprite.Create(tex, new Rect(0, 0, Ancho, Alto), new Vector2(0.75f, 0f), Ppu, 0, SpriteMeshType.FullRect);
        dibujo.name = "OndaFuegoBK";
        return dibujo;
    }
}

// Sombra espectral: la figura del caballero, oscura y transparente, con sus
// mismos clips. La mueve el jefe (JefeBlackKnightEspeciales); aqui solo su
// imagen: aparecer, desaparecer, mirar, animarse y avisar con su ritmo.
public class SombraBK : MonoBehaviour
{
    private SpriteRenderer sr;
    private AnimadorHoja anim;
    private MaterialPropertyBlock bloque;
    private Color color;
    private string clip;
    private float vel = 1f;

    public int Dir { get; private set; } = 1;
    public int Fotograma => anim != null ? anim.Fotograma : 0;
    public bool Terminado => anim == null || anim.Terminado;

    private static readonly int IdFlashColor = Shader.PropertyToID("_FlashColor");
    private static readonly int IdFlashAmount = Shader.PropertyToID("_FlashAmount");

    public static SombraBK Crear(JefeBlackKnight jefe, AnimadorHoja modelo, SpriteRenderer srModelo, Vector2 pies, AjustesBlackKnight aj)
    {
        GameObject go = new GameObject("SombraBK");
        go.transform.position = pies;
        SombraBK s = go.AddComponent<SombraBK>();
        GameObject vis = new GameObject("Visual");
        vis.transform.SetParent(go.transform, false);
        s.sr = vis.AddComponent<SpriteRenderer>();
        if (srModelo != null)
        {
            s.sr.sharedMaterial = srModelo.sharedMaterial;
            s.sr.sortingLayerID = srModelo.sortingLayerID;
            s.sr.sortingOrder = srModelo.sortingOrder - 1;
        }
        s.color = aj.colorSombra;
        s.sr.color = new Color(s.color.r, s.color.g, s.color.b, 0f);
        s.anim = vis.AddComponent<AnimadorHoja>();
        s.anim.destino = s.sr;
        s.anim.clips = modelo.clips;
        s.anim.Preparar();
        s.bloque = new MaterialPropertyBlock();
        s.Reproducir(JefeBlackKnight.ClipQuieto, 1f);
        s.Destello(Color.white, 0f);
        return s;
    }

    public void Mirar(int dir)
    {
        Dir = dir >= 0 ? 1 : -1;
        Transform v = sr.transform;
        v.localScale = new Vector3(Dir * Mathf.Abs(v.localScale.x), v.localScale.y, 1f);
    }

    public void Reproducir(string nombre, float velocidad)
    {
        clip = nombre;
        vel = velocidad;
        anim.Reproducir(nombre, true, velocidad);
    }

    // Sostiene el cuadro en el que esta (durante el aviso) o sigue.
    public void Pausar(bool pausar) => anim.Reproducir(clip, false, pausar ? 0f : vel);

    public IEnumerator Aparecer(float segundos) => Fundir(0f, color.a, segundos);
    public IEnumerator Desaparecer(float segundos) => Fundir(sr.color.a, 0f, segundos);

    private IEnumerator Fundir(float desde, float hasta, float segundos)
    {
        for (float t = 0f; t < segundos; t += Time.deltaTime)
        {
            sr.color = new Color(color.r, color.g, color.b, Mathf.Lerp(desde, hasta, t / segundos));
            yield return null;
        }
        sr.color = new Color(color.r, color.g, color.b, hasta);
    }

    // Aviso de color con el ritmo de su peso. Si "seguir", la sombra se mueve a
    // ese punto durante la primera parte del aviso (fraccion) y luego se queda.
    public IEnumerator Avisar(Color c, float hz, bool pesado, float segundos, System.Func<Vector2> seguir, float fraccion)
    {
        for (float t = 0f; t < segundos; t += Time.deltaTime)
        {
            float pulso = pesado ? 0.5f - 0.5f * Mathf.Cos(t * hz * Mathf.PI * 2f) : (Mathf.Repeat(t * hz, 1f) < 0.5f ? 1f : 0.2f);
            Destello(c, 0.7f * (0.3f + 0.7f * pulso));
            if (seguir != null && t < segundos * fraccion) transform.position = Vector2.Lerp(transform.position, seguir(), Time.deltaTime * 10f);
            yield return null;
        }
        Destello(c, 0f);
    }

    // Sin aviso, la silueta va tenida de su color (el caballero es casi negro).
    private void Destello(Color c, float cantidad)
    {
        sr.GetPropertyBlock(bloque);
        bool baseSombra = cantidad <= 0f;
        bloque.SetColor(IdFlashColor, baseSombra ? new Color(color.r * 1.6f, color.g * 1.6f, color.b * 1.6f, 1f) : c);
        bloque.SetFloat(IdFlashAmount, baseSombra ? 0.55f : Mathf.Max(0.55f, cantidad));
        sr.SetPropertyBlock(bloque);
    }
}

// Tajo del cielo (ataque "Cielo partido"): una hoja larguisima y afilada, naranja,
// con el nucleo claro. Se dibuja de golpe (como una estrella fugaz), se queda un
// instante y se apaga. Es solo imagen: el dano lo pone el jefe, que te tiene
// agarrado en el aire. Se dibuja una vez y se reutiliza, como la onda de fuego.
public class TajoCieloBK : MonoBehaviour
{
    private const float Ppu = 28f;
    private const int Ancho = 256, Alto = 24;
    private static Sprite dibujo;

    private SpriteRenderer sr;
    private float edad, trazo, quieto, salida;
    private Vector3 escala;

    // "angulo" en grados, "largo" y "grosor" en unidades.
    public static TajoCieloBK Lanzar(Vector2 centro, float angulo, float largo, float grosor,
                                     AjustesBlackKnight aj, float trazo, float quieto, float salida)
    {
        GameObject go = new GameObject("TajoCieloBK");
        go.transform.position = centro;
        go.transform.rotation = Quaternion.Euler(0f, 0f, angulo);
        TajoCieloBK t = go.AddComponent<TajoCieloBK>();
        t.sr = DibujosCazadora.Renderer(go, "VFX", 45);
        t.sr.sprite = Dibujo(aj);
        t.trazo = Mathf.Max(0.01f, trazo);
        t.quieto = Mathf.Max(0f, quieto);
        t.salida = Mathf.Max(0.01f, salida);
        Vector2 nativo = new Vector2(Ancho / Ppu, Alto / Ppu);
        t.escala = new Vector3(largo / nativo.x, grosor / nativo.y, 1f);
        go.transform.localScale = new Vector3(t.escala.x * 0.12f, t.escala.y, 1f);
        PeligrosJefe.Registrar(go);
        return t;
    }

    private void Update()
    {
        edad += Time.deltaTime;
        if (edad < trazo)
        {
            // Se estira a lo largo en un suspiro: el tajo "pasa" por la pantalla.
            float u = Mathf.Sqrt(edad / trazo);
            transform.localScale = new Vector3(escala.x * Mathf.Lerp(0.12f, 1f, u), escala.y, 1f);
            sr.color = Color.white;
        }
        else if (edad < trazo + quieto)
        {
            transform.localScale = escala;
            sr.color = Color.white;
        }
        else
        {
            float f = (edad - trazo - quieto) / salida;
            // Al apagarse se afina (como una brasa que se cierra).
            transform.localScale = new Vector3(escala.x * (1f + 0.04f * f), escala.y * (1f - 0.8f * f), 1f);
            sr.color = new Color(1f, 1f, 1f, 1f - f);
            if (f >= 1f) Destroy(gameObject);
        }
    }

    // La hoja: puntiaguda en los dos extremos, mas ancha un poco antes del medio,
    // con el nucleo claro por dentro y el borde encendido.
    private static Sprite Dibujo(AjustesBlackKnight aj)
    {
        if (dibujo != null) return dibujo;
        var tex = new Texture2D(Ancho, Alto, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[Ancho * Alto];
        Color cuerpo = aj.colorTajoCielo, nucleo = aj.colorTajoCielo2;
        float medio = (Alto - 1) * 0.5f;
        for (int x = 0; x < Ancho; x++)
        {
            float t = (x + 0.5f) / Ancho;                       // 0 a 1 a lo largo
            // Perfil de hoja: nace en punta, engorda antes del medio y muere en punta.
            float perfil = Mathf.Pow(Mathf.Sin(Mathf.Pow(t, 0.8f) * Mathf.PI), 1.3f);
            float semi = perfil * medio;
            if (semi < 0.5f) continue;
            int y0 = Mathf.RoundToInt(medio - semi), y1 = Mathf.RoundToInt(medio + semi);
            for (int y = y0; y <= y1; y++)
            {
                if (y < 0 || y >= Alto) continue;
                float d = Mathf.Abs(y - medio) / Mathf.Max(0.5f, semi);   // 0 centro, 1 borde
                Color c = d < 0.45f ? nucleo : Color.Lerp(nucleo, cuerpo, Mathf.InverseLerp(0.45f, 1f, d));
                c.a = cuerpo.a * (d > 0.85f ? 0.75f : 1f);
                px[y * Ancho + x] = c;
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        dibujo = Sprite.Create(tex, new Rect(0, 0, Ancho, Alto), new Vector2(0.5f, 0.5f), Ppu, 0, SpriteMeshType.FullRect);
        dibujo.name = "TajoCieloBK";
        return dibujo;
    }
}
