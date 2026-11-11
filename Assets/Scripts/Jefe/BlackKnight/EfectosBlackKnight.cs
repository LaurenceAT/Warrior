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
