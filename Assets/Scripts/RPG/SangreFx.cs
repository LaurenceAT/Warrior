using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Sangre (Resources/AjustesSangre para los numeros y los dibujos):
//   - Al golpear a un enemigo con sangre: una salpicadura pequena en el golpe.
//   - Al matarlo: una mas grande y manchas en el suelo que se desvanecen.
//   - Al recibir tu un golpe: unas gotas (mas si el golpe es fuerte). No dejan mancha.
//   - Al morir tu: salpicadura y manchas en el suelo que se quedan en el nivel
//     (se guardan en la partida). Cuantas mas muertes en un sitio, mas manchas;
//     al pasar del limite por nivel, las mas antiguas se desvanecen.
// Los dibujos son los de Efecto_Sangre pasados a blanco (Ronda 8) y tenidos del
// color de cada uno: rojo por defecto, verde el slime, etc.
public static class SangreFx
{
    private static AjustesSangre A => AjustesSangre.Get();

    // ------------------------------------------------------------------ Enemigos

    public static void GolpeEnemigo(EnemyHealth e, int dano)
    {
        if (e == null || !e.tieneSangre || !A.sangreEnemigos) return;
        Bounds b = Limites(e);
        Vector2 p = (Vector2)b.center + new Vector2(Random.Range(-b.extents.x, b.extents.x) * 0.4f, Random.Range(-0.1f, b.extents.y * 0.5f));
        float fuerza = Mathf.Clamp01(dano / 30f);
        Salpicadura(p, e.colorSangre, A.tamanoGolpe * (0.8f + 0.4f * fuerza));
        Gotas(p, e.colorSangre, Mathf.RoundToInt(A.gotasGolpe * (0.6f + fuerza)), 1f);
    }

    public static void MuerteEnemigo(EnemyHealth e)
    {
        if (e == null || !e.tieneSangre || !A.sangreEnemigos) return;
        Bounds b = Limites(e);
        Salpicadura(b.center, e.colorSangre, A.tamanoMuerteEnemigo);
        Gotas(b.center, e.colorSangre, A.gotasGolpe * 2, 1.4f);
        int n = Random.Range(1, A.manchasMuerteEnemigo + 1);
        for (int i = 0; i < n; i++)
            Mancha(new Vector2(b.center.x + Random.Range(-b.extents.x - 0.4f, b.extents.x + 0.4f), b.min.y + 0.3f),
                   e.colorSangre, Random.Range(0.8f, 1.1f), A.segundosManchaEnemigo);
    }

    // ------------------------------------------------------------------ Player

    // Gotas al recibir un golpe. "fuerza": 0..1 (golpes del jefe, 1).
    public static void GotasPlayer(Vector2 punto, float fuerza)
    {
        if (!A.gotasPlayer) return;
        int n = Mathf.RoundToInt(A.gotasPlayerCantidad * Mathf.Lerp(0.5f, 1.6f, Mathf.Clamp01(fuerza)) * A.gotasPlayerIntensidad);
        Gotas(punto, A.colorPlayer, n, Mathf.Lerp(0.8f, 1.3f, fuerza) * A.gotasPlayerIntensidad);
    }

    // Al morir: salpicadura grande y manchas que se quedan en el nivel.
    public static void MuertePlayer(Vector2 pos)
    {
        if (!A.sangreMuerte) return;
        Salpicadura(pos + Vector2.up * 0.4f, A.colorPlayer, A.tamanoMuertePlayer);
        Gotas(pos + Vector2.up * 0.4f, A.colorPlayer, A.gotasPlayerCantidad * 2, 1.4f);
        string escena = SceneManager.GetActiveScene().name;
        int n = Random.Range(Mathf.Max(1, A.manchasMuertePlayer - 1), A.manchasMuertePlayer + 1);
        for (int i = 0; i < n; i++)
        {
            Vector2 p = new Vector2(pos.x + Random.Range(-0.9f, 0.9f), pos.y + 0.3f);
            if (!Suelo(p, out float y, out _)) continue;
            var m = new Partida.ManchaSangre
            {
                escena = escena, x = p.x, y = y, angulo = 0f, escala = 1f,
                variante = Random.Range(0, VariantesCharco),
            };
            Partida.AnadirMancha(m, A.maximoManchasPorNivel);
            ManchasPermanentes.Poner(m);
        }
        ManchasPermanentes.Recortar(escena);
    }

    // ------------------------------------------------------------------ Piezas

    public static void Salpicadura(Vector2 p, Color color, float escala)
    {
        if (A.salpicaduras == null || A.salpicaduras.Length == 0) return;
        AnimadorHoja.Clip c = A.salpicaduras[Random.Range(0, A.salpicaduras.Length)];
        EfectoVisual.Crear(c, p, escala, color, Random.value < 0.5f, -1f, "VFX", 18, Random.Range(-25f, 25f));
    }

    private static void Gotas(Vector2 p, Color color, int n, float fuerza)
    {
        if (n <= 0) return;
        Color oscuro = new Color(color.r * 0.55f, color.g * 0.55f, color.b * 0.55f, 1f);
        ParticulasFx.Rafaga(p, n, color, oscuro, new Vector2(0.8f, 2f) * fuerza, 1.2f,
                            new Vector2(0.035f, 0.06f), new Vector2(0.3f, 0.55f), 120f, 90f);
    }

    // Mancha en el suelo que se desvanece a los "segundos" (0 = se queda).
    public static SpriteRenderer Mancha(Vector2 cerca, Color color, float escala, float segundos)
    {
        if (!Suelo(cerca, out float y, out Renderer suelo)) return null;
        SpriteRenderer sr = CrearMancha(new Vector2(cerca.x, y), Random.Range(0, VariantesCharco), color, Random.value < 0.5f, suelo);
        if (segundos > 0f) sr.gameObject.AddComponent<Desvanecer>().Iniciar(segundos, 1.5f);
        return sr;
    }

    // La sangre pintada en el suelo: un charco plano de pixeles sobre la capa de
    // arriba del suelo (la nieve), dibujado justo encima del propio suelo. No
    // sobresale: casi todo queda por debajo de la superficie, con algun
    // chorreon hacia abajo.
    public static SpriteRenderer CrearMancha(Vector2 enSuelo, int variante, Color color, bool voltear, Renderer suelo)
    {
        SpriteRenderer sr = new GameObject("ManchaSangre").AddComponent<SpriteRenderer>();
        sr.sprite = Charco(variante);
        sr.color = color;
        sr.flipX = voltear;
        sr.sharedMaterial = EfectoVisual.MaterialSinLuz();
        if (suelo != null)
        {
            sr.sortingLayerID = suelo.sortingLayerID;
            sr.sortingOrder = suelo.sortingOrder + 1;
        }
        else sr.sortingLayerName = "Ground";
        float e = Mathf.Max(0.1f, A.tamanoMancha);
        sr.transform.localScale = new Vector3(e, e, 1f);
        // El borde de arriba del charco, un pixel por encima de la superficie.
        sr.transform.position = new Vector3(enSuelo.x, enSuelo.y + e / PixelesCharco, 0f);
        return sr;
    }

    // Suelo justo debajo (hasta 3 unidades) y lo que lo dibuja.
    public static bool Suelo(Vector2 desde, out float y, out Renderer dibujo)
    {
        y = desde.y;
        dibujo = null;
        bool antes = Physics2D.queriesStartInColliders;
        Physics2D.queriesStartInColliders = false;
        RaycastHit2D h = Physics2D.Raycast(desde + Vector2.up * 0.3f, Vector2.down, 3f, LayerMask.GetMask("Ground"));
        Physics2D.queriesStartInColliders = antes;
        if (h.collider == null || h.collider.isTrigger) return false;
        y = h.point.y;
        // Lo que se VE en ese punto (el suelo que choca y el que se dibuja pueden
        // ser tilemaps distintos): el que quede mas por delante.
        dibujo = DibujoEn(new Vector2(h.point.x, h.point.y - 0.08f));
        if (dibujo == null) dibujo = h.collider.GetComponent<Renderer>();
        if (dibujo == null) dibujo = h.collider.GetComponentInParent<Renderer>();
        return true;
    }

    private static UnityEngine.Tilemaps.Tilemap[] tilemaps;
    private static int escenaTilemaps = -1;

    private static Renderer DibujoEn(Vector2 p)
    {
        Scene s = SceneManager.GetActiveScene();
        if (tilemaps == null || escenaTilemaps != s.handle)
        {
            tilemaps = Object.FindObjectsByType<UnityEngine.Tilemaps.Tilemap>(FindObjectsSortMode.None);
            escenaTilemaps = s.handle;
        }
        Renderer mejor = null;
        long mejorValor = long.MinValue;
        foreach (UnityEngine.Tilemaps.Tilemap t in tilemaps)
        {
            if (t == null || !t.gameObject.activeInHierarchy || !t.HasTile(t.WorldToCell(p))) continue;
            Renderer r = t.GetComponent<Renderer>();
            if (r == null || !r.enabled) continue;
            long valor = (long)SortingLayer.GetLayerValueFromID(r.sortingLayerID) * 100000 + r.sortingOrder;
            if (valor > mejorValor) { mejorValor = valor; mejor = r; }
        }
        return mejor;
    }

    // ------------------------------------------------------------------ Charcos

    public const int VariantesCharco = 8;
    private const float PixelesCharco = 28f;   // como los pixeles del personaje
    private static Sprite[] charcos;

    // Charcos hechos por codigo (en blanco: se tinen). Anchos distintos, borde
    // irregular, mas grueso en el centro y algun chorreon.
    private static Sprite Charco(int v)
    {
        if (charcos == null) charcos = new Sprite[VariantesCharco];
        v = Mathf.Clamp(v, 0, VariantesCharco - 1);
        if (charcos[v] != null) return charcos[v];

        System.Random r = new System.Random(1234 + v * 97);
        int ancho = 14 + r.Next(0, 16), alto = 8;
        Texture2D t = new Texture2D(ancho, alto, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        Color32[] px = new Color32[ancho * alto];
        for (int x = 0; x < ancho; x++)
        {
            // Grosor: mas en el centro, casi nada en los extremos, con ruido.
            float u = (x + 0.5f) / ancho * 2f - 1f;
            int grosor = Mathf.RoundToInt(Mathf.Sqrt(Mathf.Max(0f, 1f - u * u)) * 4.5f + (float)r.NextDouble() * 1.4f - 0.2f);
            if (r.NextDouble() < 0.12) grosor += r.Next(1, 3); // chorreon
            // Extremos sueltos: gotas separadas.
            if (Mathf.Abs(u) > 0.8f && r.NextDouble() < 0.45) grosor = 0;
            grosor = Mathf.Clamp(grosor, 0, alto);
            for (int k = 0; k < grosor; k++)
            {
                int y = alto - 1 - k; // de arriba hacia abajo
                // Arriba mas claro (brillo), abajo mas oscuro.
                byte c = (byte)(k == 0 ? 255 : k == grosor - 1 ? 150 : 205);
                px[y * ancho + x] = new Color32(c, c, c, 255);
            }
        }
        t.SetPixels32(px);
        t.Apply();
        // Pivote arriba en el centro: el borde de arriba se apoya en el suelo.
        charcos[v] = Sprite.Create(t, new Rect(0, 0, ancho, alto), new Vector2(0.5f, 1f), PixelesCharco);
        charcos[v].name = "Charco_" + v;
        return charcos[v];
    }

    private static Bounds Limites(EnemyHealth e)
    {
        SpriteRenderer sr = e.GetComponentInChildren<SpriteRenderer>();
        return sr != null ? sr.bounds : new Bounds(e.transform.position + Vector3.up * 0.5f, Vector3.one);
    }

    // Se apaga poco a poco y se borra.
    public class Desvanecer : MonoBehaviour
    {
        private float fin, fundido;
        private SpriteRenderer sr;
        private float alfa0;

        public void Iniciar(float segundos, float duracionFundido)
        {
            sr = GetComponent<SpriteRenderer>();
            alfa0 = sr != null ? sr.color.a : 1f;
            fundido = Mathf.Max(0.05f, duracionFundido);
            fin = Time.time + Mathf.Max(segundos, fundido);
        }

        private void Update()
        {
            float queda = fin - Time.time;
            if (queda <= 0f) { Destroy(gameObject); return; }
            if (sr != null && queda < fundido)
            {
                Color c = sr.color;
                c.a = alfa0 * queda / fundido;
                sr.color = c;
            }
        }
    }
}

// Las manchas de las muertes del player en el nivel actual. Se ponen al cargar
// el nivel (las guardadas en la partida) y se quedan tras reaparecer.
public static class ManchasPermanentes
{
    private static readonly List<(Partida.ManchaSangre datos, SpriteRenderer sr)> puestas = new List<(Partida.ManchaSangre, SpriteRenderer)>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Iniciar()
    {
        SceneManager.sceneLoaded += (s, m) => Cargar(s.name);
        Cargar(SceneManager.GetActiveScene().name);
    }

    private static void Cargar(string escena)
    {
        puestas.Clear();
        foreach (Partida.ManchaSangre m in Partida.Manchas)
            if (m.escena == escena) Poner(m);
    }

    public static void Poner(Partida.ManchaSangre m)
    {
        AjustesSangre a = AjustesSangre.Get();
        // Lo que dibuja el suelo en ese punto, para pintar encima.
        SangreFx.Suelo(new Vector2(m.x, m.y + 0.2f), out _, out Renderer suelo);
        SpriteRenderer sr = SangreFx.CrearMancha(new Vector2(m.x, m.y), m.variante, a.colorPlayer, (m.variante & 1) == 1, suelo);
        puestas.Add((m, sr));
    }

    // Quita (con un fundido) las que ya no estan en la partida por el limite.
    public static void Recortar(string escena)
    {
        var siguen = new HashSet<Partida.ManchaSangre>(Partida.Manchas);
        for (int i = puestas.Count - 1; i >= 0; i--)
        {
            if (siguen.Contains(puestas[i].datos)) continue;
            if (puestas[i].sr != null) puestas[i].sr.gameObject.AddComponent<SangreFx.Desvanecer>().Iniciar(0f, 2f);
            puestas.RemoveAt(i);
        }
    }
}
