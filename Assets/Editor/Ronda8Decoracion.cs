using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// Decoracion extra de los niveles (Ronda 8). Se reparte sola por el suelo (y en
// la cueva tambien por el techo), con estas reglas:
//   - Solo sobre suelo llano y con sitio por encima.
//   - Nunca cerca de estatuas, hogueras, cofres, enemigos, trampas, portales,
//     objetos que se usan ni en la arena del jefe.
//   - Sin repetir la misma pieza dos veces seguidas, volteada al azar.
//   - Sin colisiones y detras del player (capa Middleground).
// Todo va en el objeto "DecoracionExtra" de la escena: se puede borrar entero o
// mover pieza a pieza. Volver a lanzar la ronda lo rehace desde cero.
public static partial class Ronda8
{
    private const string Decor = Gandalf + "Decor.png";
    private const string CuevaProps1 = "Assets/SPRITES PARA NUEVOS NIVELES/MAPAS/Mapa_CuevaPixelFantasy/props1.png";
    private const string CuevaProps2 = "Assets/SPRITES PARA NUEVOS NIVELES/MAPAS/Mapa_CuevaPixelFantasy/props2.png";
    private const float PixelesPersonaje = 28f;

    private class Pieza
    {
        public string tipo;
        public Sprite sprite;
        public float alto;          // unidades; 0 = pixeles como los del personaje
        public Color color = Color.white;
        public bool techo;          // cuelga del techo
        public Sprite[] animacion;  // antorchas
        public float peso = 1f;
    }

    static partial void Decoracion(Scene escena, bool nieve)
    {
        Transform raiz = Raiz(escena, "DecoracionExtra");
        for (int i = raiz.childCount - 1; i >= 0; i--) Object.DestroyImmediate(raiz.GetChild(i).gameObject);

        List<Pieza> piezas = nieve ? PiezasNieve() : PiezasCueva();
        if (piezas.Count == 0) { Debug.LogWarning("[Ronda8] Sin piezas de decoracion para " + escena.name); return; }

        Physics2D.SyncTransforms();
        Bounds nivel = LimitesNivel(escena);
        List<(Bounds b, string n)> prohibidas = Prohibidas(escena);
        // Lo que ya habia en primer plano cuenta como puesto: no se amontona encima.
        var puestas = escena.GetRootGameObjects().Where(r => r != raiz.gameObject)
                            .SelectMany(r => r.GetComponentsInChildren<SpriteRenderer>(true))
                            .Where(sr => sr.sortingLayerName == "Middleground" && sr.sprite != null && sr.bounds.size.x < 12f)
                            .Select(sr => sr.bounds).ToList();
        int previas = puestas.Count;
        var cuenta = new Dictionary<string, int>();
        var descartes = new Dictionary<string, int>();
        System.Random azar = new System.Random(nieve ? 11 : 23);
        Pieza ultima = null;

        for (float x = nivel.min.x + 3f; x < nivel.max.x - 3f; x += Rango(azar, 1.8f, 4.2f))
        {
            foreach ((float y, bool esTecho) in Superficies(escena, x, nivel))
            {
                if (azar.NextDouble() > (esTecho ? 0.22 : 0.7)) continue;
                var candidatas = piezas.Where(p => p.techo == esTecho && p != ultima).ToList();
                if (candidatas.Count == 0) continue;
                Pieza pz = Elegir(candidatas, azar);
                Vector2 tam = Tamano(pz);
                float esc = Rango(azar, 0.9f, 1.1f);
                tam *= esc;
                if (!Llano(escena, x, y, tam.x, esTecho)) { Contar(descartes, "no llano"); continue; }
                if (!HayHueco(escena, x, y, tam, esTecho)) { Contar(descartes, "sin hueco"); continue; }
                Bounds b = new Bounds(new Vector3(x, esTecho ? y - tam.y * 0.5f : y + tam.y * 0.5f), new Vector3(tam.x, tam.y, 1f));
                var choque = prohibidas.FirstOrDefault(p => p.b.Intersects(b));
                if (choque.n != null) { Contar(descartes, "zona " + choque.n); continue; }
                Bounds holgura = b;
                holgura.Expand(new Vector3(0.6f, 0f, 0f));
                if (puestas.Any(p => p.Intersects(holgura))) { Contar(descartes, "ya hay algo"); continue; }

                Poner(raiz, pz, x, y, esc, azar.NextDouble() < 0.5, esTecho);
                puestas.Add(b);
                ultima = pz;
                cuenta[pz.tipo] = cuenta.TryGetValue(pz.tipo, out int c) ? c + 1 : 1;
            }
        }
        Debug.Log($"[Ronda8] {escena.name}: decoracion {puestas.Count - previas} piezas -> " + string.Join(", ", cuenta.Select(k => $"{k.Key} {k.Value}")) +
                  $" | nivel {nivel.min.x:0}..{nivel.max.x:0} | descartes: " + string.Join(", ", descartes.Select(k => $"{k.Key} {k.Value}")));
    }

    private static void Contar(Dictionary<string, int> d, string k) => d[k] = d.TryGetValue(k, out int n) ? n + 1 : 1;

    // ------------------------------------------------------------------ Piezas

    private static List<Pieza> PiezasNieve()
    {
        ConfigurarRecursosRPG.PrepararPixel(Cementerio, 32f);
        var d = Sprites(Decor).ToDictionary(s => s.name);
        var c = Sprites(Cementerio).ToDictionary(s => s.name);
        var l = new List<Pieza>();
        Color frio = new Color(0.82f, 0.88f, 1f);
        Color sombra = new Color(0.45f, 0.52f, 0.66f);
        void Add(string tipo, Dictionary<string, Sprite> dic, string nombre, Color color, float peso = 1f)
        {
            if (dic.TryGetValue(nombre, out Sprite s)) l.Add(new Pieza { tipo = tipo, sprite = s, color = color, peso = peso });
        }
        Add("roca nevada", d, "Decor_75", frio, 0.6f);
        Add("roca nevada", d, "Decor_70", frio);
        Add("roca nevada", d, "Decor_71", frio);
        Add("roca nevada", d, "Decor_61", frio);
        Add("roca nevada", d, "Decor_62", frio);
        Add("piedrecillas", d, "Decor_55", frio);
        Add("piedrecillas", d, "Decor_56", frio);
        Add("monticulo de nieve", d, "Decor_79", Color.white, 0.8f);
        Add("monticulo de nieve", d, "Decor_82", Color.white);
        Add("arbusto nevado", d, "Decor_89", frio);
        Add("arbusto nevado", d, "Decor_90", frio);
        Add("hierba helada", d, "Decor_91", frio);
        Add("hierba helada", d, "Decor_43", frio);
        Add("lena", d, "Decor_19", frio, 0.5f);
        Add("tumba", d, "Decor_27", frio, 0.4f);
        Add("tumba", d, "Decor_30", frio, 0.4f);
        Add("arbol seco", c, "free_6", sombra, 0.5f);
        Add("arbol seco", c, "free_7", sombra, 0.5f);
        Add("pino oscuro", c, "free_11", sombra, 0.5f);
        Add("lapida vieja", c, "free_18", sombra, 0.4f);
        return l;
    }

    private static List<Pieza> PiezasCueva()
    {
        var l = new List<Pieza>();
        Color roca = new Color(0.62f, 0.55f, 0.56f);
        Color fondo = new Color(0.45f, 0.4f, 0.42f);
        foreach (Sprite s in Sprites(CuevaProps2))
        {
            int n = int.Parse(s.name.Substring(s.name.LastIndexOf('_') + 1));
            // 0-7: arboles y arbustos (no pintan en una cueva). 8-11: montones de roca.
            if (n >= 8) l.Add(new Pieza { tipo = "roca", sprite = s, alto = n == 11 ? 2.2f : 1.6f, color = roca, peso = n == 11 ? 0.6f : 1.2f });
        }
        // props1: estalagmitas (base abajo) y estalactitas (base arriba), tintadas
        // mas oscuras que las que caen de verdad (Estalactita) para no confundirlas.
        Texture2D tex = LeerPng(CuevaProps1);
        foreach (Sprite s in Sprites(CuevaProps1))
        {
            if (s.rect.width < 20 || s.rect.height < 40 || s.rect.height < s.rect.width * 0.8f) continue;
            float abajo = Opacidad(tex, s.rect, true), arriba = Opacidad(tex, s.rect, false);
            if (Mathf.Abs(abajo - arriba) < 0.15f) continue;
            bool techo = arriba > abajo;
            l.Add(new Pieza { tipo = techo ? "estalactita" : "estalagmita", sprite = s, alto = Mathf.Clamp(s.rect.height / 140f, 0.8f, 2f), color = fondo, techo = techo, peso = techo ? 0.6f : 0.15f });
        }
        if (tex != null) Object.DestroyImmediate(tex);
        // Antorchas del pack del bosque, animadas y con luz.
        Sprite[] antorcha = Sprites(Gandalf + "Torch.png").OrderBy(s => int.Parse(s.name.Substring(s.name.LastIndexOf('_') + 1))).ToArray();
        if (antorcha.Length > 0)
        {
            ConfigurarRecursosRPG.PrepararPixel(Gandalf + "Torch.png", 32f);
            antorcha = Sprites(Gandalf + "Torch.png").OrderBy(s => int.Parse(s.name.Substring(s.name.LastIndexOf('_') + 1))).ToArray();
            l.Add(new Pieza { tipo = "antorcha", sprite = antorcha[0], animacion = antorcha, peso = 0.35f });
        }
        // Plantas de cueva (primer fotograma).
        string plantas = "Assets/SPRITES PARA NUEVOS NIVELES/MAPAS/Mapa_CuevaVegetacion/Vegetation/Comp 1/PlantSmall_00000.png";
        Sprite planta = Sprites(plantas).FirstOrDefault();
        if (planta != null) l.Add(new Pieza { tipo = "planta", sprite = planta, alto = 0.7f, color = new Color(0.55f, 0.6f, 0.55f), peso = 0.7f });
        return l;
    }

    private static Vector2 Tamano(Pieza p)
    {
        Vector2 px = p.sprite.rect.size;
        if (p.alto > 0f) return new Vector2(p.alto * px.x / px.y, p.alto);
        return px / PixelesPersonaje;
    }

    private static void Poner(Transform raiz, Pieza p, float x, float y, float esc, bool voltear, bool techo)
    {
        Vector2 tam = Tamano(p) * esc;
        SpriteRenderer sr = Apoyada(raiz, p.tipo, p.sprite, x, techo ? y - tam.y : y, tam.y, "Middleground", 0, p.color, voltear);
        if (p.animacion != null && p.animacion.Length > 1)
        {
            AnimarSprites a = sr.gameObject.AddComponent<AnimarSprites>();
            a.fotogramas = p.animacion;
            a.luz = true;
        }
    }

    // ------------------------------------------------------------------ Donde

    private static Bounds LimitesNivel(Scene escena)
    {
        int suelo = LayerMask.NameToLayer("Ground");
        Bounds? b = null;
        foreach (Collider2D c in escena.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Collider2D>()))
        {
            if (c.gameObject.layer != suelo || c.isTrigger) continue;
            if (b == null) b = c.bounds;
            else { Bounds x = b.Value; x.Encapsulate(c.bounds); b = x; }
        }
        return b ?? new Bounds(Vector3.zero, Vector3.one * 100f);
    }

    private static readonly Dictionary<Scene, Tilemap[]> tilemaps = new Dictionary<Scene, Tilemap[]>();

    private static bool Solido(Scene escena, Vector2 p)
    {
        if (!tilemaps.TryGetValue(escena, out Tilemap[] tms))
        {
            int suelo = LayerMask.NameToLayer("Ground");
            tms = escena.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Tilemap>())
                        .Where(t => t.gameObject.layer == suelo && t.GetComponent<TilemapCollider2D>() != null).ToArray();
            tilemaps[escena] = tms;
        }
        foreach (Tilemap t in tms) if (t.HasTile(t.WorldToCell(p))) return true;
        // Lo que no es tilemap (losas y pilares de la cueva...).
        foreach (Collider2D c in Physics2D.OverlapPointAll(p, LayerMask.GetMask("Ground")))
            if (!c.isTrigger && c.GetComponent<Tilemap>() == null) return true;
        return false;
    }

    // Suelos (y techos) que cruza la vertical x, con su altura exacta.
    private static IEnumerable<(float, bool)> Superficies(Scene escena, float x, Bounds nivel)
    {
        const float paso = 0.25f;
        bool antes = Solido(escena, new Vector2(x, nivel.max.y + 1f));
        for (float y = nivel.max.y + 1f - paso; y > nivel.min.y - 1f; y -= paso)
        {
            bool ahora = Solido(escena, new Vector2(x, y));
            if (ahora != antes)
            {
                // Vacio arriba y solido abajo: suelo. Al reves: techo.
                float exacta = Afinar(escena, x, y, y + paso, ahora);
                yield return (exacta, !ahora);
            }
            antes = ahora;
        }
    }

    // Busca el borde entre y0 (abajo) e y1 (arriba) por biseccion.
    private static float Afinar(Scene escena, float x, float y0, float y1, bool abajoSolido)
    {
        for (int i = 0; i < 8; i++)
        {
            float m = (y0 + y1) * 0.5f;
            if (Solido(escena, new Vector2(x, m)) == abajoSolido) y0 = m; else y1 = m;
        }
        return (y0 + y1) * 0.5f;
    }

    // Suelo (o techo) de la misma altura a lo ancho de la pieza.
    private static bool Llano(Scene escena, float x, float y, float ancho, bool techo)
    {
        float d = techo ? 0.15f : -0.15f;
        for (float dx = -ancho * 0.5f; dx <= ancho * 0.5f + 0.01f; dx += Mathf.Max(0.2f, ancho / 6f))
        {
            if (!Solido(escena, new Vector2(x + dx, y + d))) return false;
            if (Solido(escena, new Vector2(x + dx, y - d))) return false;
        }
        return true;
    }

    // Sitio libre para la pieza (y un poco mas, para que no toque nada).
    private static bool HayHueco(Scene escena, float x, float y, Vector2 tam, bool techo)
    {
        float dir = techo ? -1f : 1f;
        for (float dy = 0.3f; dy <= tam.y + 0.4f; dy += 0.3f)
            for (float dx = -tam.x * 0.5f; dx <= tam.x * 0.5f + 0.01f; dx += Mathf.Max(0.3f, tam.x / 4f))
                if (Solido(escena, new Vector2(x + dx, y + dir * dy))) return false;
        return true;
    }

    // Zonas donde no se pone nada: lo que se usa, enemigos, trampas, la arena
    // del jefe y lo que tiene colision (salvo el suelo).
    private static List<(Bounds b, string n)> Prohibidas(Scene escena)
    {
        var l = new List<(Bounds b, string n)>();
        int suelo = LayerMask.NameToLayer("Ground");
        string[] tipos = { "EstatuaPista", "Hoguera", "CofreMejora", "CofreAlmas", "EnemigoBase", "Portal", "ZonaDano", "PendulumTrap",
                           "Estalactita", "SueloHielo", "AguaCongelable", "MuroHielo", "SelloSombrio", "ParedFalsa", "ZonaOculta",
                           "HealthPickup", "Damage", "Checkpoint", "Puerta" };
        // (DeadArea no: son las caidas mortales, por debajo del suelo.)
        foreach (GameObject r in escena.GetRootGameObjects())
        {
            foreach (MonoBehaviour m in r.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (m == null) continue;
                string n = m.GetType().Name;
                bool enemigo = m is EnemigoBase;
                if (!enemigo && !tipos.Any(t => n.Contains(t))) continue;
                // Los enemigos van delante de la decoracion (no los tapa): basta un
                // margen pequeno. Lo que se usa o hace dano, mas margen.
                // Pieza a pieza: un grupo (todas las paredes falsas bajo un padre)
                // no debe prohibir todo lo que hay entre ellas.
                Vector3 margen = new Vector3(enemigo ? 1.5f : 3f, 1f, 0f);
                foreach (Bounds b in Piezas(m.gameObject))
                {
                    Bounds c = b;
                    c.Expand(margen);
                    l.Add((c, n));
                }
            }
            // La arena del jefe entera: ahi manda la lectura del combate.
            foreach (ArenaJefe a in r.GetComponentsInChildren<ArenaJefe>(true))
            {
                Bounds b = Limites(a.gameObject);
                b.Expand(new Vector3(6f, 4f, 0f));
                l.Add((b, "arena"));
            }
            // Lo solido que no es suelo (plataformas que se mueven, muros...). Las
            // zonas invisibles (camara, ventisca, zonas ocultas) no molestan.
            foreach (Collider2D c in r.GetComponentsInChildren<Collider2D>(true))
            {
                if (c.isTrigger || (c.gameObject.layer == suelo && c.GetComponent<Tilemap>() != null)) continue;
                if (c.gameObject.layer == suelo && c.GetComponent<MonoBehaviour>() == null) continue;
                Bounds b = c.bounds;
                b.Expand(1f);
                l.Add((b, "colision " + c.name));
            }
        }
        return l;
    }

    private static Bounds Limites(GameObject g)
    {
        Bounds? b = null;
        foreach (Renderer r in g.GetComponentsInChildren<Renderer>(true))
            b = b == null ? r.bounds : Unir(b.Value, r.bounds);
        foreach (Collider2D c in g.GetComponentsInChildren<Collider2D>(true))
            b = b == null ? c.bounds : Unir(b.Value, c.bounds);
        return b ?? new Bounds(g.transform.position, Vector3.one);
    }

    private static Bounds Unir(Bounds a, Bounds b) { a.Encapsulate(b); return a; }

    // Los limites de cada pieza visible o con colision del objeto, por separado.
    private static IEnumerable<Bounds> Piezas(GameObject g)
    {
        bool alguna = false;
        // Un tilemap (las paredes falsas) ocupa solo sus casillas, no su contorno.
        foreach (Tilemap t in g.GetComponentsInChildren<Tilemap>(true))
            foreach (Vector3Int p in t.cellBounds.allPositionsWithin)
                if (t.HasTile(p)) { alguna = true; yield return new Bounds(t.GetCellCenterWorld(p), t.layoutGrid.cellSize); }
        foreach (Renderer r in g.GetComponentsInChildren<Renderer>(true))
            if (!(r is TilemapRenderer)) { alguna = true; yield return r.bounds; }
        foreach (Collider2D c in g.GetComponentsInChildren<Collider2D>(true))
            if (c.GetComponent<Tilemap>() == null && !(c is CompositeCollider2D && c.GetComponent<TilemapCollider2D>() != null)) { alguna = true; yield return c.bounds; }
        if (!alguna) yield return new Bounds(g.transform.position, Vector3.one);
    }

    // ------------------------------------------------------------------ Ayudas

    private static float Rango(System.Random r, float a, float b) => a + (float)r.NextDouble() * (b - a);

    private static Pieza Elegir(List<Pieza> l, System.Random r)
    {
        float total = l.Sum(p => p.peso), t = (float)r.NextDouble() * total;
        foreach (Pieza p in l) { t -= p.peso; if (t <= 0f) return p; }
        return l[l.Count - 1];
    }

    private static Texture2D LeerPng(string ruta)
    {
        string f = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), ruta);
        if (!System.IO.File.Exists(f)) return null;
        Texture2D t = new Texture2D(2, 2);
        return t.LoadImage(System.IO.File.ReadAllBytes(f)) ? t : null;
    }

    // Parte opaca de la fila de abajo (o de arriba) de un recorte.
    private static float Opacidad(Texture2D t, Rect r, bool abajo)
    {
        if (t == null) return 0f;
        int y = abajo ? (int)r.y + 2 : (int)r.yMax - 3;
        int llenos = 0, n = 0;
        for (int x = (int)r.x; x < (int)r.xMax; x++, n++) if (t.GetPixel(x, y).a > 0.5f) llenos++;
        return n > 0 ? llenos / (float)n : 0f;
    }
}
