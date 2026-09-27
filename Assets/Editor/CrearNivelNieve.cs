using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// Genera la escena "Nivel Nieve" (Warrior > Crear nivel Nieve): el nivel nevado
// definitivo, largo y con tramos distintos:
//   A  Campamento del Paso: hoguera 1 y las primeras ratas.
//   B  Ladera helada: suelo de hielo resbaladizo, fosos de pinchos y un pozo.
//   C  Paso de la ventisca: el viento empuja en contra, rafagas y voladores.
//   D  Lago helado: solo se cruza congelandolo con la espada imbuida en escarcha.
//   E  Tunel sellado: un muro de hielo que solo el fuego derrite.
//   F  Ruinas: subida a una meseta, arqueras y un escondite tras un sello de
//      sombras (se abre con lo sagrado).
//   G  Antesala: la ultima hoguera y la pista del jefe.
//   H  Arena del jefe: la Sombra de los Humedales.
//
// Trampas: solo pinchos y caidas mortales. El terreno es un Tilemap con el
// tileset de suelo nevado del pack Mapa_BosqueGandalf; el fondo, el parallax
// del castillo de hielo (Fondo_CastilloHielo).
public static class CrearNivelNieve
{
    private const string Plantilla = "Assets/Scenes/Nivel 1.unity";
    private const string Destino = "Assets/Scenes/Nivel Nieve.unity";
    private const string Pack = ConfigurarRecursosRPG.Pack;
    private const string Gandalf = Pack + "MAPAS/Mapa_BosqueGandalf/";
    private const string Fondo = Pack + "BACKGROUND/Fondo_CastilloHielo/pngs/";
    private const string Trampas = Pack + "TRAMPAS/Trampas_TrampasYArmas/";
    private const string Torre = Pack + "HOGUERA O CHECKPOINT/Checkpoint_TorreLunaSangre/RedMoonTower_free_idle_animation..png";
    private const string Enemigos = Pack + "ENEMIGOS/";
    private const string Jefe = Pack + "BOSSES/Boss_ShadowedWetlands/Sprite Sheet/";
    private const string FxEnemigos = Pack + "EFECTOS DE ATAQUE DE LOS ENEMIGOS/";
    private const string FxPlayer = Pack + "EFECTOS DE ATAQUE DEL PLAYER/";
    private const string Musica = Pack + "SONIDOS/Sonidos_MusicaBosses/";
    private const string CarpetaPrefabs = "Assets/Prefabs/Enemies/Nieve";
    private const string CarpetaTiles = "Assets/Tiles/Nieve";

    private static readonly string[] Quitar =
        { "Grid", "Diamonds", "Enemies", "Saw_Idle", "Sierras_Moviles", "DeadZones", "Checkpoint", "Heart" };

    // Fuera del mapa, por abajo y por los lados, todo es roca.
    private static readonly RectInt Mapa = new RectInt(-6, -12, 410, 44);

    // ------------------------------------------------------------------ Trazado
    // Roca: x, y (abajo), ancho, alto. Una unidad = una casilla del tileset.
    private static readonly RectInt[] Roca =
    {
        // A  Campamento
        new RectInt(-4, -12, 38, 12),     // suelo 0
        new RectInt(34, -12, 14, 14),     // escalon a 2
        // B  Ladera helada
        new RectInt(48, -12, 12, 14),     // nieve 2
        new RectInt(72, -12, 3, 12),      // fondo del foso de pinchos (0)
        new RectInt(92, -12, 8, 15),      // nieve 3
        new RectInt(100, -12, 12, 18),    // meseta de la arquera (6)
        new RectInt(112, -12, 10, 15),    // hoguera 2 (3)
        // C  Ventisca
        new RectInt(122, -12, 10, 15),
        new RectInt(132, -12, 3, 13),     // foso de pinchos (1)
        new RectInt(135, -12, 7, 15),
        new RectInt(154, -12, 14, 15),
        new RectInt(160, 3, 2, 2),        // roca en medio del camino
        new RectInt(168, -12, 3, 13),     // foso de pinchos (1)
        new RectInt(171, -12, 29, 15),
        // D  Lago (el agua va de 200 a 217)
        new RectInt(217, -12, 23, 15),
        // E  Tunel del muro de hielo
        new RectInt(240, -12, 42, 15),
        new RectInt(242, 8, 34, 24),      // techo del tunel
        // F  Ruinas
        new RectInt(282, -12, 48, 15),    // suelo 3 (sigue bajo la meseta: el escondite)
        new RectInt(291, 3, 3, 2),        // escalones a la meseta
        new RectInt(294, 3, 3, 3),
        new RectInt(297, 3, 3, 5),
        new RectInt(300, 6, 18, 4),       // meseta (arriba 10); debajo, el escondite
        new RectInt(326, 6, 4, 4),        // meseta, tras el tramo de hielo
        new RectInt(329, 3, 1, 3),        // fondo del escondite (cerrado)
        new RectInt(330, -12, 32, 15),    // bajada y antesala
        // H  Arena
        new RectInt(362, -12, 36, 15),
        new RectInt(362, 20, 36, 12),     // techo de la arena
        new RectInt(398, -12, 12, 44),    // pared del fondo
        new RectInt(-6, -12, 2, 44),      // pared del principio
    };

    // Tramos de hielo resbaladizo (roca propia, con el tileset tenido de azul).
    private static readonly RectInt[] Hielo =
    {
        new RectInt(60, -12, 12, 14),     // B: 60-72, arriba 2
        new RectInt(75, -12, 13, 14),     // B: 75-88, arriba 2
        new RectInt(142, -12, 8, 15),     // C: 142-150, justo antes del pozo
        new RectInt(318, 6, 8, 4),        // F: parte de la meseta
    };

    private static readonly Vector2 Salida = new Vector2(2f, 0f);
    private static readonly Vector2[] Hogueras = { new Vector2(10f, 0f), new Vector2(116f, 3f), new Vector2(236f, 3f), new Vector2(350f, 3f) };
    // Fosos de pinchos: x, ancho, y del fondo.
    private static readonly Vector3[] Pinchos = { new Vector3(72f, 3f, 0f), new Vector3(132f, 3f, 1f), new Vector3(168f, 3f, 1f) };

    private static readonly (string tipo, Vector2 pos)[] PosEnemigos =
    {
        ("Rata", new Vector2(24f, 0f)), ("Rata", new Vector2(29f, 0f)),
        ("Murcielago", new Vector2(66f, 6f)), ("Rata", new Vector2(81f, 2f)),
        ("Arquero", new Vector2(106f, 6f)),
        ("Murcielago", new Vector2(140f, 8f)), ("Ojo", new Vector2(159f, 8f)),
        ("Rata", new Vector2(177f, 3f)), ("Rata", new Vector2(182f, 3f)), ("Murcielago", new Vector2(190f, 8f)),
        ("Ojo", new Vector2(224f, 8f)),
        ("Hechicero", new Vector2(262f, 3f)),
        ("Rata", new Vector2(287f, 3f)), ("Arquero", new Vector2(314f, 10f)), ("Ojo", new Vector2(320f, 14f)),
        ("Rata", new Vector2(324f, 10f)),
        ("Hechicero", new Vector2(339f, 3f)),
    };

    // ------------------------------------------------------------------ Menu

    [MenuItem("Warrior/Crear nivel Nieve")]
    public static void Crear()
    {
        if (File.Exists(Destino) && !Application.isBatchMode &&
            !EditorUtility.DisplayDialog("Nivel Nieve", "La escena ya existe. Regenerarla borra los cambios hechos a mano.", "Regenerar", "Cancelar"))
            return;
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        ConfigurarRecursosRPG.Configurar();
        PrepararImportacion();

        if (File.Exists(Destino)) AssetDatabase.DeleteAsset(Destino);
        if (!AssetDatabase.CopyAsset(Plantilla, Destino)) { Debug.LogError("[Nieve] No se pudo copiar " + Plantilla); return; }

        Scene escena = EditorSceneManager.OpenScene(Destino, OpenSceneMode.Single);
        foreach (GameObject raiz in escena.GetRootGameObjects())
            if (Quitar.Any(n => raiz.name == n || raiz.name.StartsWith(n + " "))) Object.DestroyImmediate(raiz);

        System.Random azar = new System.Random(4321);
        Transform nivel = new GameObject("Nivel").transform;

        ConstruirTerreno(nivel);
        ConstruirFondo(nivel);
        ConstruirDecoracion(nivel, azar);
        ConstruirHogueras(nivel);
        ConstruirTrampas(nivel);
        ConstruirObstaculos(nivel);
        ConstruirVentisca(nivel);
        ConstruirEnemigos(nivel);
        ConstruirPistas(nivel);
        ConstruirZonasCamara(nivel);
        new GameObject("ReinicioEnemigos", typeof(ReinicioEnemigos)).transform.SetParent(nivel);
        ColocarPlayerYCamara(escena);
        ConstruirArena(nivel, escena);
        Ambiente(nivel, escena);

        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
        AnadirABuild();
        Debug.Log("[Nieve] Nivel generado en " + Destino);
    }

    // ------------------------------------------------------------------ Importacion

    private static void PrepararImportacion()
    {
        CortarEnCeldas(Gandalf + "Floor Tiles1.png", 32, 32f);
        ConfigurarRecursosRPG.PrepararPixel(Gandalf + "Animated Sprites/GandalfHardcore Water Tiles sheet.png", 32f);
        ConfigurarRecursosRPG.PrepararPixel(Gandalf + "Snow blizzard sheet frame size 484x274.png");
        ConfigurarRecursosRPG.PrepararPixel(Gandalf + "Pine Trees.png", 32f);
        ConfigurarRecursosRPG.PrepararPixel(Gandalf + "Decor.png", 32f);
        ConfigurarRecursosRPG.PrepararPixel(Gandalf + "Large Pine Tree.png", 32f);
        ConfigurarRecursosRPG.PrepararPixel(Gandalf + "Small Tent.png", 32f);
        ConfigurarRecursosRPG.PrepararPixel(Gandalf + "Large Tent.png", 32f);
        ConfigurarRecursosRPG.PrepararPixel(Gandalf + "Angel Statue.png", 32f);
        foreach (string f in Directory.GetFiles(Fondo, "*.png")) ConfigurarRecursosRPG.PrepararPixel(f.Replace('\\', '/'));
        ConfigurarRecursosRPG.PrepararPixel(Torre);
        ConfigurarRecursosRPG.PrepararPixel(Trampas + "Spike_B.png");
        foreach (string f in Directory.GetFiles(Pack + "ENEMIGOS", "*.png", SearchOption.AllDirectories)) ConfigurarRecursosRPG.PrepararPixel(f.Replace('\\', '/'));

        // El jefe: la hoja de muerte mide 8151 de ancho; no se puede encoger.
        foreach (string f in Directory.GetFiles(Jefe, "*.png", SearchOption.AllDirectories))
        {
            TextureImporter ti = AssetImporter.GetAtPath(f.Replace('\\', '/')) as TextureImporter;
            if (ti == null) continue;
            if (ti.maxTextureSize == 8192 && ti.filterMode == FilterMode.Point && ti.textureCompression == TextureImporterCompression.Uncompressed) continue;
            ti.maxTextureSize = 8192;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = false;
            ti.SaveAndReimport();
        }

        // Musica larga: en streaming (no se carga entera en memoria).
        foreach (string m in new[] { MusicaFase1, MusicaFase2, MusicaNivel, MusicaCueva })
        {
            AudioImporter ai = AssetImporter.GetAtPath(m) as AudioImporter;
            if (ai == null) { Debug.LogWarning("[Nieve] No encuentro la musica " + m); continue; }
            AudioImporterSampleSettings s = ai.defaultSampleSettings;
            if (s.loadType == AudioClipLoadType.Streaming) continue;
            s.loadType = AudioClipLoadType.Streaming;
            ai.defaultSampleSettings = s;
            ai.SaveAndReimport();
        }
    }

    // Corta una textura en celdas cuadradas (nombre "c{col}_r{fila}", fila desde arriba).
    private static void CortarEnCeldas(string ruta, int celda, float ppu)
    {
        TextureImporter ti = AssetImporter.GetAtPath(ruta) as TextureImporter;
        if (ti == null) { Debug.LogWarning("[Nieve] No encuentro " + ruta); return; }
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(ruta);
        int cols = tex.width / celda, filas = tex.height / celda;
#pragma warning disable 618
        bool hecho = ti.spriteImportMode == SpriteImportMode.Multiple && ti.spritesheet != null && ti.spritesheet.Length == cols * filas
                     && Mathf.Approximately(ti.spritePixelsPerUnit, ppu) && ti.filterMode == FilterMode.Point;
        if (hecho) return;
        var metas = new List<SpriteMetaData>();
        for (int r = 0; r < filas; r++)
        for (int c = 0; c < cols; c++)
            metas.Add(new SpriteMetaData
            {
                name = $"c{c}_r{r}", rect = new Rect(c * celda, tex.height - (r + 1) * celda, celda, celda),
                alignment = (int)SpriteAlignment.Center, pivot = new Vector2(0.5f, 0.5f),
            });
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Multiple;
        ti.spritesheet = metas.ToArray();
#pragma warning restore 618
        ti.spritePixelsPerUnit = ppu;
        ti.filterMode = FilterMode.Point;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.mipmapEnabled = false;
        ti.SaveAndReimport();
    }

    // ------------------------------------------------------------------ Terreno

    private static bool EsRoca(int x, int y)
    {
        if (x < Mapa.xMin || x >= Mapa.xMax || y < Mapa.yMin) return true;
        if (y >= Mapa.yMax) return false;
        Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
        return Roca.Any(r => Contiene(r, p)) || Hielo.Any(r => Contiene(r, p));
    }

    private static bool EsHielo(int x, int y) => Hielo.Any(r => Contiene(r, new Vector2(x + 0.5f, y + 0.5f)));

    private static bool Contiene(RectInt r, Vector2 p) => p.x >= r.xMin && p.x < r.xMax && p.y >= r.yMin && p.y < r.yMax;

    private static void ConstruirTerreno(Transform nivel)
    {
        Sprite[] celdas = AssetDatabase.LoadAllAssetsAtPath(Gandalf + "Floor Tiles1.png").OfType<Sprite>().ToArray();
        Sprite S(int c, int r) => celdas.FirstOrDefault(s => s.name == $"c{c}_r{r}");
        if (!AssetDatabase.IsValidFolder("Assets/Tiles")) AssetDatabase.CreateFolder("Assets", "Tiles");
        if (!AssetDatabase.IsValidFolder(CarpetaTiles)) AssetDatabase.CreateFolder("Assets/Tiles", "Nieve");

        // Bloque nevado del tileset (filas 12-14, columnas 0-2).
        string[] nombres = { "esq_ai", "arriba", "esq_ad", "izq", "centro", "der", "esq_bi", "abajo", "esq_bd" };
        var nieve = new Dictionary<string, Tile>();
        var hielo = new Dictionary<string, Tile>();
        for (int i = 0; i < 9; i++)
        {
            Sprite s = S(i % 3, 12 + i / 3);
            nieve[nombres[i]] = TileAsset("nieve_" + nombres[i], s, Color.white);
            hielo[nombres[i]] = TileAsset("hielo_" + nombres[i], s, new Color(0.62f, 0.86f, 1f));
        }

        Grid grid = new GameObject("Terreno", typeof(Grid)).GetComponent<Grid>();
        grid.transform.SetParent(nivel);
        Tilemap tm = new GameObject("Suelo", typeof(Tilemap), typeof(TilemapRenderer)).GetComponent<Tilemap>();
        tm.transform.SetParent(grid.transform, false);
        TilemapRenderer tr = tm.GetComponent<TilemapRenderer>();
        tr.sortingLayerName = "Ground";
        tr.mode = TilemapRenderer.Mode.Chunk;

        // Se pintan las celdas de roca que se pueden ver (hasta 3 por debajo de
        // una superficie; mas abajo no llega la camara, y el resto se rellena
        // con una sola pieza oscura).
        var todas = Roca.Concat(Hielo).ToArray();
        foreach (RectInt r in todas)
        for (int x = r.xMin; x < r.xMax; x++)
        for (int y = Mathf.Max(r.yMin, -4); y < r.yMax; y++)
        {
            if (!EsRoca(x, y)) continue;
            bool arriba = !EsRoca(x, y + 1), abajo = !EsRoca(x, y - 1);
            bool izq = !EsRoca(x - 1, y), der = !EsRoca(x + 1, y);
            string n = arriba ? (izq ? "esq_ai" : der ? "esq_ad" : "arriba")
                     : abajo ? (izq ? "esq_bi" : der ? "esq_bd" : "abajo")
                     : izq ? "izq" : der ? "der" : "centro";
            tm.SetTile(new Vector3Int(x, y, 0), EsHielo(x, y) ? hielo[n] : nieve[n]);
        }
        // Relleno bajo lo pintado (por si se asoma la camara en los pozos).
        Sprite centro = S(1, 13);
        Transform relleno = Hijo(nivel, "RellenoProfundo");
        foreach (RectInt r in todas)
            if (r.yMin < -4 && centro != null)
            {
                SpriteRenderer sr = Pieza(relleno, "Relleno", Blanco(), new Vector2(r.center.x, (r.yMin - 4) * 0.5f), new Vector2(r.width, -4 - r.yMin), "Ground", -1);
                sr.color = new Color(0.12f, 0.1f, 0.09f);
            }

        // Colisiones: una caja por rectangulo, en la capa Ground. El hielo, con SueloHielo.
        int capa = LayerMask.NameToLayer("Ground");
        Transform colis = Hijo(nivel, "Colisiones");
        foreach (RectInt r in Roca)
        {
            GameObject go = new GameObject("Roca");
            go.layer = capa;
            go.transform.SetParent(colis);
            go.transform.position = r.center;
            go.AddComponent<BoxCollider2D>().size = r.size;
        }
        foreach (RectInt r in Hielo)
        {
            GameObject go = new GameObject("Hielo");
            go.layer = capa;
            go.transform.SetParent(colis);
            go.transform.position = r.center;
            go.AddComponent<BoxCollider2D>().size = r.size;
            SueloHielo sh = go.AddComponent<SueloHielo>();
            // Brillos que corren por la superficie del hielo.
            var brillos = new List<SpriteRenderer>();
            for (float x = r.xMin + 0.6f; x < r.xMax - 0.4f; x += 1.7f)
            {
                SpriteRenderer b = new GameObject("Brillo").AddComponent<SpriteRenderer>();
                b.transform.SetParent(go.transform, false);
                b.sprite = Blanco();
                b.transform.position = new Vector3(x, r.yMax - 0.12f, 0f);
                b.transform.localScale = new Vector3(0.9f, 0.05f, 1f);
                b.color = new Color(1f, 1f, 1f, 0.35f);
                b.sortingLayerName = "Ground";
                b.sortingOrder = 3;
                brillos.Add(b);
            }
            sh.PonerBrillos(brillos.ToArray());
        }
    }

    private static Sprite blancoAsset;

    // Cuadrado blanco de 1x1 guardado como PNG del proyecto, para teñir.
    private static Sprite Blanco()
    {
        if (blancoAsset != null) return blancoAsset;
        string ruta = CarpetaTiles + "/Blanco.png";
        if (!File.Exists(ruta))
        {
            Texture2D t = new Texture2D(4, 4);
            Color[] px = new Color[16];
            for (int i = 0; i < 16; i++) px[i] = Color.white;
            t.SetPixels(px);
            File.WriteAllBytes(ruta, t.EncodeToPNG());
            AssetDatabase.ImportAsset(ruta);
        }
        TextureImporter ti = (TextureImporter)AssetImporter.GetAtPath(ruta);
        if (ti.textureType != TextureImporterType.Sprite || !Mathf.Approximately(ti.spritePixelsPerUnit, 4f))
        {
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = 4f;
            ti.filterMode = FilterMode.Point;
            ti.SaveAndReimport();
        }
        blancoAsset = AssetDatabase.LoadAssetAtPath<Sprite>(ruta);
        return blancoAsset;
    }

    private static Tile TileAsset(string nombre, Sprite s, Color color)
    {
        string ruta = CarpetaTiles + "/" + nombre + ".asset";
        Tile t = AssetDatabase.LoadAssetAtPath<Tile>(ruta);
        if (t == null)
        {
            t = ScriptableObject.CreateInstance<Tile>();
            AssetDatabase.CreateAsset(t, ruta);
        }
        t.sprite = s;
        t.color = color;
        t.colliderType = Tile.ColliderType.None;
        t.flags = TileFlags.LockColor;
        EditorUtility.SetDirty(t);
        return t;
    }

    // ------------------------------------------------------------------ Fondo

    private static void ConstruirFondo(Transform nivel)
    {
        GameObject go = new GameObject("Fondo");
        go.transform.SetParent(nivel);
        ParallaxCueva parallax = go.AddComponent<ParallaxCueva>();
        parallax.seguimientoVertical = 0.92f;
        var capas = new List<ParallaxCueva.Capa>();
        const float alto = 19f;
        // De la mas lejana a la mas cercana; la niebla entre el castillo y los montes.
        (string archivo, float seguimiento, Color color)[] orden =
        {
            ("sky", 0.99f, Color.white),
            ("4_BG_mts", 0.93f, new Color(0.9f, 0.93f, 1f)),
            ("3_ice_castle", 0.86f, Color.white),
            ("fog", 0.8f, new Color(1f, 1f, 1f, 0.6f)),
            ("2_foreground_mts", 0.72f, new Color(0.85f, 0.9f, 1f)),
            ("1_foreground_mts", 0.6f, new Color(0.8f, 0.86f, 0.95f)),
        };
        for (int i = 0; i < orden.Length; i++)
        {
            Sprite s = PrimerSprite(Fondo + orden[i].archivo + ".png");
            if (s == null) continue;
            Transform raiz = Hijo(go.transform, "Capa_" + orden[i].archivo);
            float escala = alto / s.bounds.size.y;
            float ancho = s.bounds.size.x * escala;
            for (int k = -1; k <= 1; k++)
            {
                SpriteRenderer sr = new GameObject("Copia").AddComponent<SpriteRenderer>();
                sr.transform.SetParent(raiz);
                sr.transform.localPosition = new Vector3(k * ancho, 0f, 0f);
                sr.transform.localScale = new Vector3(escala, escala, 1f);
                sr.sprite = s;
                sr.color = orden[i].color;
                sr.sortingLayerName = "Background";
                sr.sortingOrder = i;
            }
            capas.Add(new ParallaxCueva.Capa { raiz = raiz, ancho = ancho, seguimiento = orden[i].seguimiento });
        }
        parallax.capas = capas.ToArray();
    }

    // Pinos nevados, rocas y matas con nieve, el campamento del principio.
    private static void ConstruirDecoracion(Transform nivel, System.Random azar)
    {
        Transform padre = Hijo(nivel, "Decoracion");
        Sprite[] pinos = Sprites(Gandalf + "Pine Trees.png").Where(s => s.rect.x > 440 && s.rect.height > 120).ToArray();
        Sprite[] decor = Sprites(Gandalf + "Decor.png");
        float Alto(Sprite s) => 544f - (s.rect.y + s.rect.height * 0.5f);
        Sprite[] rocasNieve = decor.Where(s => s.rect.center.x > 190 && s.rect.center.x < 300 && Alto(s) > 145 && Alto(s) < 260).ToArray();
        Sprite[] rocaGrande = decor.Where(s => s.rect.center.x > 295 && s.rect.center.x < 395 && Alto(s) > 270 && Alto(s) < 330).ToArray();
        Sprite[] matas = decor.Where(s => s.rect.center.x < 135 && Alto(s) > 480).ToArray();
        Sprite[] montones = decor.Where(s => s.rect.center.x > 240 && s.rect.center.x < 360 && Alto(s) > 330 && Alto(s) < 390).ToArray();

        // Por cada superficie de nieve (no hielo, no la arena), algo de adorno.
        foreach (RectInt r in Roca)
        {
            float y = r.yMax;
            if (y > 25 || r.xMin >= 362 || r.width < 3) continue;
            for (float x = r.xMin + 1f; x < r.xMax - 1f; x += 2.2f + (float)azar.NextDouble() * 3f)
            {
                if (!EsRoca(Mathf.FloorToInt(x), Mathf.FloorToInt(y) - 1) || EsRoca(Mathf.FloorToInt(x), Mathf.FloorToInt(y))) continue;
                if (Hogueras.Any(h => Mathf.Abs(h.x - x) < 2.5f)) continue;
                double tirada = azar.NextDouble();
                if (tirada < 0.22 && pinos.Length > 0)
                    PiezaApoyada(padre, "Pino", pinos[azar.Next(pinos.Length)], x, y - 0.05f, 4.5f + (float)azar.NextDouble() * 2f, "Middleground", 0, new Color(0.85f, 0.9f, 1f));
                else if (tirada < 0.4 && rocasNieve.Length > 0)
                    PiezaApoyada(padre, "Roca", rocasNieve[azar.Next(rocasNieve.Length)], x, y - 0.05f, 0.7f + (float)azar.NextDouble() * 0.4f, "Middleground", 2, Color.white);
                else if (tirada < 0.55 && matas.Length > 0)
                    PiezaApoyada(padre, "Mata", matas[azar.Next(matas.Length)], x, y - 0.05f, 0.6f, "Middleground", 2, Color.white);
            }
        }
        // Pinos grandes al fondo, mas oscuros, para dar profundidad.
        Sprite pinoGrande = pinos.OrderByDescending(s => s.rect.height).FirstOrDefault();
        foreach (float x in new[] { 6f, 40f, 95f, 126f, 186f, 226f, 288f, 344f })
            if (pinoGrande != null)
            {
                float y = SueloEn(x);
                PiezaApoyada(padre, "PinoFondo", pinoGrande, x, y - 0.1f, 7.5f, "Background", 20, new Color(0.45f, 0.52f, 0.68f));
            }

        // Campamento abandonado junto a la primera hoguera.
        Sprite tienda = PrimerSprite(Gandalf + "Large Tent.png");
        if (tienda != null) PiezaApoyada(padre, "Tienda", tienda, 17f, -0.05f, 2.4f, "Middleground", 1, new Color(0.85f, 0.88f, 0.95f));
        Sprite estatua = PrimerSprite(Gandalf + "Angel Statue.png");
        if (estatua != null) PiezaApoyada(padre, "Estatua", estatua, 355f, 2.95f, 2.6f, "Middleground", 1, new Color(0.75f, 0.82f, 0.95f));
        if (rocaGrande.Length > 0) PiezaApoyada(padre, "RocaGrande", rocaGrande[0], 44f, 1.95f, 1.4f, "Middleground", 2, Color.white);
    }

    // Altura del suelo en x (lo mas alto de la roca en esa columna, bajo y=25).
    private static float SueloEn(float x)
    {
        int cx = Mathf.FloorToInt(x);
        for (int y = 24; y > -12; y--)
            if (EsRoca(cx, y)) return y + 1;
        return 0f;
    }

    // ------------------------------------------------------------------ Hogueras

    private static void ConstruirHogueras(Transform nivel)
    {
        Transform padre = Hijo(nivel, "Hogueras");
        Sprite[] fotogramas = Sprites(Torre).OrderBy(s => -s.rect.y).ThenBy(s => s.rect.x).ToArray();
        for (int i = 0; i < Hogueras.Length; i++)
        {
            GameObject go = new GameObject("Hoguera_" + (i + 1));
            go.transform.SetParent(padre);
            go.transform.position = Hogueras[i];
            SpriteRenderer torre = new GameObject("Torre").AddComponent<SpriteRenderer>();
            torre.transform.SetParent(go.transform, false);
            torre.transform.localScale = new Vector3(2f, 2f, 1f);
            if (fotogramas.Length > 0) torre.sprite = fotogramas[0];
            torre.sortingLayerName = "Items";
            torre.transform.localPosition = new Vector3(0f, -torre.bounds.min.y + go.transform.position.y, 0f);
            AnimacionSprites anim = torre.gameObject.AddComponent<AnimacionSprites>();
            anim.fotogramas = fotogramas;
            anim.fps = 8f;
            Hoguera h = go.AddComponent<Hoguera>();
            Asignar(h, "torre", torre);
            AsignarFloat(h, "alturaAviso", torre.bounds.size.y + 0.35f);
        }
    }

    // ------------------------------------------------------------------ Trampas

    private static void ConstruirTrampas(Transform nivel)
    {
        Transform padre = Hijo(nivel, "Trampas");
        Sprite pincho = PrimerSprite(Trampas + "Spike_B.png");
        int capaTrampas = LayerMask.NameToLayer("Traps");
        foreach (Vector3 p in Pinchos)
        {
            if (pincho == null) break;
            GameObject go = new GameObject("Pinchos");
            go.layer = capaTrampas;
            go.transform.SetParent(padre);
            go.transform.position = new Vector3(p.x + p.y * 0.5f, p.z + 0.25f, 0f);
            Pieza(go.transform, "Sprite", pincho, go.transform.position, new Vector2(p.y, 0.5f), "traps", 0).color = new Color(0.85f, 0.92f, 1f);
            BoxCollider2D box = go.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(p.y, 0.45f);
            go.AddComponent<ZonaDano>();
        }

        // Caidas mortales: bajo todo el mapa (los pozos llegan hasta aqui).
        GameObject muerte = new GameObject("CaidaMortal");
        muerte.layer = capaTrampas;
        muerte.transform.SetParent(padre);
        muerte.transform.position = new Vector3(200f, -9f, 0f);
        BoxCollider2D m = muerte.AddComponent<BoxCollider2D>();
        m.isTrigger = true;
        m.size = new Vector2(420f, 3f);
        muerte.AddComponent<DeadArea>();
    }

    // ------------------------------------------------------------------ Obstaculos elementales

    private static void ConstruirObstaculos(Transform nivel)
    {
        Transform padre = Hijo(nivel, "Obstaculos");
        int suelo = LayerMask.NameToLayer("Ground");
        int trampas = LayerMask.NameToLayer("Traps");
        Sprite[] celdas = AssetDatabase.LoadAllAssetsAtPath(Gandalf + "Floor Tiles1.png").OfType<Sprite>().ToArray();
        Sprite centro = celdas.FirstOrDefault(s => s.name == "c1_r13");
        Sprite arriba = celdas.FirstOrDefault(s => s.name == "c1_r12");

        // --- D: el lago (x 200-217). Agua animada; caer dentro mata.
        const float x0 = 200f, x1 = 217f, superficie = 2.75f;
        GameObject lago = new GameObject("Lago");
        lago.transform.SetParent(padre);
        lago.transform.position = new Vector3((x0 + x1) * 0.5f, 2.5f, 0f);
        GameObject agua = new GameObject("Agua");
        agua.transform.SetParent(lago.transform);
        Texture2D hojaAgua = AssetDatabase.LoadAssetAtPath<Texture2D>(Gandalf + "Animated Sprites/GandalfHardcore Water Tiles sheet.png");
        var clipAgua = new AnimadorHoja.Clip
        {
            nombre = "agua", hoja = hojaAgua, anchoCelda = 160, altoCelda = 64, fila = 0, primero = 0, cantidad = 3,
            fps = 4f, bucle = true, pivote = new Vector2(0.5f, 1f), pixelesPorUnidad = 32f,
        };
        for (float x = x0 + 2.5f; x < x1 + 2.5f; x += 5f)
        {
            SpriteRenderer sr = new GameObject("Tramo").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(agua.transform);
            sr.transform.position = new Vector3(Mathf.Min(x, x1 - 2.5f), superficie + 0.1f, 0f);
            sr.sortingLayerName = "Ground";
            sr.sortingOrder = 4;
            sr.color = new Color(0.7f, 0.85f, 0.95f, 0.95f);
            AnimadorHoja a = sr.gameObject.AddComponent<AnimadorHoja>();
            a.destino = sr;
            a.clips = new List<AnimadorHoja.Clip> { clipAgua };
            a.clipInicial = "agua";
        }
        SpriteRenderer fondoAgua = new GameObject("Profundo").AddComponent<SpriteRenderer>();
        fondoAgua.transform.SetParent(agua.transform);
        fondoAgua.sprite = Blanco();
        fondoAgua.transform.position = new Vector3((x0 + x1) * 0.5f, (superficie - 1.9f - 12f) * 0.5f, 0f);
        fondoAgua.transform.localScale = new Vector3(x1 - x0, superficie - 1.9f + 12f, 1f);
        fondoAgua.color = new Color(0.05f, 0.14f, 0.2f, 1f);
        fondoAgua.sortingLayerName = "Ground";
        fondoAgua.sortingOrder = 3;

        GameObject ahogo = new GameObject("Ahogarse");
        ahogo.layer = trampas;
        ahogo.transform.SetParent(lago.transform);
        ahogo.transform.position = new Vector3((x0 + x1) * 0.5f, superficie - 1.2f, 0f);
        BoxCollider2D ah = ahogo.AddComponent<BoxCollider2D>();
        ah.isTrigger = true;
        ah.size = new Vector2(x1 - x0, 1.6f);
        ahogo.AddComponent<DeadArea>();

        // El puente de hielo (apagado hasta congelarlo) y su aspecto.
        GameObject puente = new GameObject("PuenteHielo");
        puente.layer = suelo;
        puente.transform.SetParent(lago.transform);
        puente.transform.position = new Vector3((x0 + x1) * 0.5f, 2.5f, 0f);
        BoxCollider2D pc = puente.AddComponent<BoxCollider2D>();
        pc.size = new Vector2(x1 - x0, 1f);
        puente.AddComponent<SueloHielo>();
        GameObject visHielo = new GameObject("HieloVisual");
        visHielo.transform.SetParent(puente.transform);
        for (float x = x0 + 0.5f; x < x1; x += 1f)
        {
            SpriteRenderer t = Pieza(visHielo.transform, "Losa", Blanco(), new Vector2(x, 2.5f), new Vector2(1.02f, 1f), "Ground", 6);
            t.color = Mathf.RoundToInt(x) % 2 == 0 ? new Color(0.62f, 0.85f, 1f, 0.92f) : new Color(0.7f, 0.9f, 1f, 0.92f);
            SpriteRenderer borde = Pieza(visHielo.transform, "Escarcha", Blanco(), new Vector2(x, 2.95f), new Vector2(1.02f, 0.1f), "Ground", 7);
            borde.color = new Color(0.95f, 0.98f, 1f, 1f);
        }
        GameObject golpeable = new GameObject("SuperficieLago");
        golpeable.transform.SetParent(lago.transform);
        golpeable.transform.position = new Vector3((x0 + x1) * 0.5f, 3.1f, 0f);
        BoxCollider2D gc = golpeable.AddComponent<BoxCollider2D>();
        gc.isTrigger = true;
        gc.size = new Vector2(x1 - x0 + 0.4f, 1.2f);
        AguaCongelable ac = golpeable.AddComponent<AguaCongelable>();
        ac.Configurar(pc, agua, visHielo, x1 - x0);

        // --- E: muro de hielo que cierra el tunel (x 246-247.5, de 3 a 8).
        GameObject muro = new GameObject("MuroHielo");
        muro.layer = suelo;
        muro.transform.SetParent(padre);
        muro.transform.position = new Vector3(246.75f, 5.5f, 0f);
        BoxCollider2D mc = muro.AddComponent<BoxCollider2D>();
        mc.size = new Vector2(1.5f, 5f);
        var visuales = new List<SpriteRenderer>();
        for (int i = 0; i < 5; i++)
        for (int j = 0; j < 2; j++)
        {
            SpriteRenderer b = Pieza(muro.transform, "Bloque", Blanco(), new Vector2(246.25f + j * 0.75f, 3.5f + i), new Vector2(0.78f, 1.02f), "Ground", 5);
            b.color = (i + j) % 2 == 0 ? new Color(0.6f, 0.85f, 1f, 0.9f) : new Color(0.72f, 0.92f, 1f, 0.88f);
            visuales.Add(b);
        }
        AnimadorHoja.Clip cristales = ConfigurarRecursosRPG.Clip("cristal", FxPlayer + "Efecto_CaballeroHielo/VFX3/Frames", 1f, true, 64f, new Vector2(0.5f, 0.1f), 7, 7);
        if (cristales != null && cristales.fotogramas.Length > 0)
        {
            Sprite cs = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(cristales.fotogramas[0])).OfType<Sprite>().FirstOrDefault();
            if (cs != null)
                foreach (Vector3 c in new[] { new Vector3(246.7f, 3f, 0f), new Vector3(246.7f, 5.4f, 180f) })
                {
                    SpriteRenderer b = new GameObject("Cristales").AddComponent<SpriteRenderer>();
                    b.transform.SetParent(muro.transform);
                    b.sprite = cs;
                    b.transform.position = new Vector3(c.x, c.y, 0f);
                    b.transform.rotation = Quaternion.Euler(0f, 0f, c.z);
                    b.transform.localScale = Vector3.one * 1.3f;
                    b.sortingLayerName = "Ground";
                    b.sortingOrder = 6;
                    visuales.Add(b);
                }
        }
        muro.AddComponent<MuroHielo>().Configurar(mc, visuales.ToArray());

        // --- F: sello de sombras en la boca del escondite (x 300, de 3 a 6).
        GameObject sello = new GameObject("SelloSombrio");
        sello.layer = suelo;
        sello.transform.SetParent(padre);
        sello.transform.position = new Vector3(300.4f, 4.5f, 0f);
        BoxCollider2D sc = sello.AddComponent<BoxCollider2D>();
        sc.size = new Vector2(0.8f, 3f);
        AnimadorHoja.Clip portal = ConfigurarRecursosRPG.Clip("sombra", FxEnemigos + "Efecto_Portal/Frames", 10f, true, 32f, new Vector2(0.5f, 0.5f));
        var sombras = new List<SpriteRenderer>();
        for (int i = 0; i < 2; i++)
        {
            SpriteRenderer s = new GameObject("Sombra").AddComponent<SpriteRenderer>();
            s.transform.SetParent(sello.transform);
            s.transform.position = new Vector3(300.4f, 3.8f + i * 1.4f, 0f);
            s.transform.localScale = new Vector3(0.9f, 1.3f, 1f);
            s.color = new Color(0.35f, 0.1f, 0.55f, 0.85f);
            s.sortingLayerName = "Ground";
            s.sortingOrder = 7;
            AnimadorHoja a = s.gameObject.AddComponent<AnimadorHoja>();
            a.destino = s;
            a.clips = new List<AnimadorHoja.Clip> { portal };
            a.clipInicial = "sombra";
            sombras.Add(s);
        }
        sello.AddComponent<SelloSombrio>().Configurar(sc, sombras.ToArray());

        // El cofre del escondite, al fondo del tunel bajo la meseta.
        GameObject cofre = new GameObject("CofreAlmas");
        cofre.layer = LayerMask.NameToLayer("Items");
        cofre.transform.SetParent(padre);
        cofre.transform.position = new Vector3(326f, 3f, 0f);
        cofre.AddComponent<BoxCollider2D>().size = new Vector2(1.2f, 1.2f);
        cofre.GetComponent<BoxCollider2D>().offset = new Vector2(0f, 0.6f);
        Sprite cerrado = PrimerSprite(Enemigos + "Enemigo_MonsterPack2/Mimic/Idle_closed.png");
        if (cerrado != null)
        {
            SpriteRenderer v = new GameObject("Visual").AddComponent<SpriteRenderer>();
            v.transform.SetParent(cofre.transform, false);
            v.sprite = cerrado;
            v.transform.localScale = Vector3.one * 0.75f;
            v.transform.localPosition = new Vector3(0f, -v.bounds.min.y + cofre.transform.position.y - 0.35f, 0f);
            v.sortingLayerName = "Items";
            CofreAlmas ca = cofre.AddComponent<CofreAlmas>();
            Asignar(ca, "visual", v);
        }
        else cofre.AddComponent<CofreAlmas>();
    }

    // ------------------------------------------------------------------ Ventisca

    private static void ConstruirVentisca(Transform nivel)
    {
        GameObject go = new GameObject("Ventisca");
        go.layer = LayerMask.NameToLayer("Items");
        go.transform.SetParent(nivel);
        go.transform.position = new Vector3(157f, 9f, 0f);
        BoxCollider2D box = go.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = new Vector2(70f, 16f);
        ZonaVentisca z = go.AddComponent<ZonaVentisca>();
        Texture2D hoja = AssetDatabase.LoadAssetAtPath<Texture2D>(Gandalf + "Snow blizzard sheet frame size 484x274.png");
        z.clipVentisca = new AnimadorHoja.Clip
        {
            nombre = "ventisca", hoja = hoja, anchoCelda = 484, altoCelda = 274, fila = 0, primero = 0, cantidad = 30,
            fps = 14f, bucle = true, pivote = new Vector2(0.5f, 0.5f), pixelesPorUnidad = 100f,
        };
    }

    // ------------------------------------------------------------------ Enemigos

    private static AnimadorHoja.Clip Hoja(string nombre, string ruta, int ancho, int alto, int cantidad, float fps, bool bucle,
                                          Vector2 pivote, float ppu, int fila = 0, int primero = 0, bool alReves = false)
    {
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(ruta);
        if (tex == null) Debug.LogWarning("[Nieve] No encuentro la hoja " + ruta);
        return new AnimadorHoja.Clip
        {
            nombre = nombre, hoja = tex, anchoCelda = ancho, altoCelda = alto, fila = fila, primero = primero, cantidad = cantidad,
            fps = fps, bucle = bucle, pivote = pivote, pixelesPorUnidad = ppu, alReves = alReves,
        };
    }

    private static void ConstruirEnemigos(Transform nivel)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs/Enemies")) AssetDatabase.CreateFolder("Assets/Prefabs", "Enemies");
        if (!AssetDatabase.IsValidFolder(CarpetaPrefabs)) AssetDatabase.CreateFolder("Assets/Prefabs/Enemies", "Nieve");

        AnimadorHoja.Clip marca = ConfigurarRecursosRPG.Clip("marca", FxEnemigos + "Efecto_MagoSangre/VFX1/part2(loop)/frames", 12f, true, 64f, new Vector2(0.5f, 0.5f));
        var prefabs = new Dictionary<string, GameObject>
        {
            { "Rata", PrefabRata() },
            { "Murcielago", PrefabMurcielago() },
            { "Ojo", PrefabOjo() },
            { "Arquero", PrefabArquero(marca) },
            { "Hechicero", PrefabHechicero() },
        };

        Transform padre = Hijo(nivel, "Enemigos");
        foreach (var (tipo, pos) in PosEnemigos)
        {
            if (!prefabs.TryGetValue(tipo, out GameObject prefab) || prefab == null) continue;
            GameObject e = (GameObject)PrefabUtility.InstantiatePrefab(prefab, padre);
            e.transform.position = new Vector3(pos.x, pos.y + 0.02f, 0f);
        }
    }

    private static GameObject BaseEnemigo(string nombre, List<AnimadorHoja.Clip> clips, int vida, float barraAltura, float muerte,
                                          int almas, out AnimadorHoja anim)
    {
        GameObject go = new GameObject(nombre);
        go.tag = "Enemy";
        go.layer = LayerMask.NameToLayer("Enemies");
        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        GameObject v = new GameObject("Visual");
        v.transform.SetParent(go.transform, false);
        SpriteRenderer sr = v.AddComponent<SpriteRenderer>();
        sr.sortingLayerName = "Player";
        sr.sortingOrder = -1;
        Material flash = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materiales/Enemy_Flash.mat");
        if (flash != null) sr.sharedMaterial = flash;
        anim = v.AddComponent<AnimadorHoja>();
        anim.destino = sr;
        anim.clips = clips;

        EnemyHealth salud = go.AddComponent<EnemyHealth>();
        SerializedObject so = new SerializedObject(salud);
        so.FindProperty("maxHealth").intValue = vida;
        so.FindProperty("deathAnimDuration").floatValue = muerte;
        so.FindProperty("healthBarOffset").vector2Value = new Vector2(0f, barraAltura);
        so.FindProperty("almas").intValue = almas;
        so.ApplyModifiedPropertiesWithoutUndo();
        go.AddComponent<HitFlash>();
        return go;
    }

    private static GameObject Guardar(GameObject go, string archivo)
    {
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, CarpetaPrefabs + "/" + archivo + ".prefab");
        Object.DestroyImmediate(go);
        return prefab;
    }

    private static void Enlazar(Component c, AnimadorHoja anim)
    {
        Asignar(c, "anim", anim);
        Asignar(c, "visual", anim.transform);
    }

    private static void Afinidad(GameObject go, float fuego, float hielo, float oscuro, float sagrado, float acido)
    {
        go.AddComponent<AfinidadElemental>().Poner(fuego, hielo, oscuro, sagrado, acido);
    }

    private static GameObject PrefabRata()
    {
        string r = Enemigos + "Enemigo_MonsterPack2/Rat/";
        Vector2 p = new Vector2(0.5f, 0.357f);
        var clips = new List<AnimadorHoja.Clip>
        {
            Hoja("quieto", r + "idle.png", 70, 70, 10, 10f, true, p, 28f),
            Hoja("andar", r + "run.png", 70, 70, 8, 14f, true, p, 28f),
            Hoja("morder", r + "attack_bite.png", 70, 70, 12, 22f, false, p, 28f),
            Hoja("golpe", r + "hurt.png", 70, 70, 3, 10f, false, p, 28f),
            Hoja("muerte", r + "rat-death.png", 70, 70, 6, 10f, false, p, 28f),
        };
        GameObject go = BaseEnemigo("RataEscarcha", clips, 40, 0.85f, 0.7f, 35, out AnimadorHoja anim);
        BoxCollider2D box = go.AddComponent<BoxCollider2D>();
        box.size = new Vector2(0.9f, 0.55f);
        box.offset = new Vector2(0f, 0.28f);
        Afinidad(go, 1.6f, 0.4f, 1f, 1f, 1f);
        Enlazar(go.AddComponent<EnemigoRata>(), anim);
        return Guardar(go, "RataEscarcha");
    }

    private static GameObject PrefabMurcielago()
    {
        string r = Enemigos + "Enemigo_MonsterPack2/Bat/";
        Vector2 p = new Vector2(0.546f, 0.5f);
        var clips = new List<AnimadorHoja.Clip>
        {
            Hoja("vuelo", r + "fly.png", 87, 87, 11, 14f, true, p, 36f),
            Hoja("ataque", r + "attack.png", 87, 87, 11, 14f, false, p, 36f),
            Hoja("golpe", r + "hurt.png", 87, 87, 3, 10f, false, p, 36f),
            Hoja("muerte", r + "death.png", 87, 87, 4, 8f, false, p, 36f),
        };
        GameObject go = BaseEnemigo("MurcielagoCumbres", clips, 35, 0.8f, 0.6f, 40, out AnimadorHoja anim);
        go.GetComponent<Rigidbody2D>().gravityScale = 0f;
        go.AddComponent<CircleCollider2D>().radius = 0.45f;
        SerializedObject so = new SerializedObject(go.GetComponent<EnemyHealth>());
        so.FindProperty("volador").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();
        Afinidad(go, 1f, 1.5f, 1f, 1f, 0.5f);
        Enlazar(go.AddComponent<EnemigoMurcielago>(), anim);
        return Guardar(go, "MurcielagoCumbres");
    }

    private static GameObject PrefabOjo()
    {
        string r = Enemigos + "Enemigo_MonsterPack1/Flying eye/";
        Vector2 p = new Vector2(0.537f, 0.527f);
        var clips = new List<AnimadorHoja.Clip>
        {
            Hoja("vuelo", r + "Attack2.png", 150, 150, 4, 10f, true, p, 32f, 0, 1),
            Hoja("girar", r + "Attack2.png", 150, 150, 3, 16f, true, p, 32f, 0, 5),
            Hoja("escupir", r + "Attack3.png", 150, 150, 6, 12f, false, p, 32f),
        };
        GameObject go = BaseEnemigo("OjoVigia", clips, 45, 0.85f, 0.9f, 50, out AnimadorHoja anim);
        go.GetComponent<Rigidbody2D>().gravityScale = 0f;
        go.AddComponent<CircleCollider2D>().radius = 0.45f;
        SerializedObject so = new SerializedObject(go.GetComponent<EnemyHealth>());
        so.FindProperty("volador").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();
        Afinidad(go, 0.5f, 1f, 1f, 1f, 1.6f);
        EnemigoOjo ojo = go.AddComponent<EnemigoOjo>();
        Enlazar(ojo, anim);
        ojo.clipEscupitajo = Hoja("escupitajo", r + "projectile_sprite.png", 48, 48, 8, 14f, true, new Vector2(0.5f, 0.5f), 32f);
        ojo.clipImpacto = ConfigurarRecursosRPG.Clip("impacto_acido", FxPlayer + "Efecto_Impactos/VFX1/COLOR/Frames", 20f, false, 64f, new Vector2(0.5f, 0.5f));
        EditorUtility.SetDirty(ojo);
        return Guardar(go, "OjoVigia");
    }

    private static GameObject PrefabArquero(AnimadorHoja.Clip marca)
    {
        string h = Enemigos + "Enemigo_ArcaneArcher/spritesheet.png";
        Vector2 p = new Vector2(0.5f, 0.25f);
        var clips = new List<AnimadorHoja.Clip>
        {
            Hoja("quieto", h, 64, 64, 4, 8f, true, p, 28f, 5),
            Hoja("correr", h, 64, 64, 8, 12f, true, p, 28f, 0),
            Hoja("muerte", h, 64, 64, 8, 10f, false, p, 28f, 1),
            Hoja("voltereta", h, 64, 64, 7, 14f, false, p, 28f, 2),
            Hoja("disparar", h, 64, 64, 7, 12f, false, p, 28f, 3),
            Hoja("cielo", h, 64, 64, 6, 10f, false, p, 28f, 4),
            Hoja("destello", h, 64, 64, 4, 12f, false, p, 28f, 6),
            Hoja("golpe", h, 64, 64, 2, 8f, false, p, 28f, 7),
        };
        GameObject go = BaseEnemigo("ArqueraArcana", clips, 55, 1.35f, 1f, 60, out AnimadorHoja anim);
        BoxCollider2D box = go.AddComponent<BoxCollider2D>();
        box.size = new Vector2(0.6f, 1.1f);
        box.offset = new Vector2(0f, 0.55f);
        Afinidad(go, 1f, 1f, 1.5f, 0.5f, 1f);
        EnemigoArquero a = go.AddComponent<EnemigoArquero>();
        Enlazar(a, anim);
        ConfigurarRecursosRPG.PrepararPixel(Enemigos + "Enemigo_ArcaneArcher/projectile.png", 28f);
        a.spriteFlecha = PrimerSprite(Enemigos + "Enemigo_ArcaneArcher/projectile.png");
        a.clipMarca = marca;
        EditorUtility.SetDirty(a);
        return Guardar(go, "ArqueraArcana");
    }

    private static GameObject PrefabHechicero()
    {
        string r = Enemigos + "Enemigo_EvilWizard2/Sprites/";
        Vector2 p = new Vector2(0.546f, 0.332f);
        var clips = new List<AnimadorHoja.Clip>
        {
            Hoja("quieto", r + "Idle.png", 250, 250, 8, 10f, true, p, 50f),
            Hoja("correr", r + "Run.png", 250, 250, 8, 12f, true, p, 50f),
            Hoja("garras", r + "Attack1.png", 250, 250, 8, 12f, false, p, 50f),
            Hoja("baculo", r + "Attack2.png", 250, 250, 8, 12f, false, p, 50f),
            Hoja("saltar", r + "Jump.png", 250, 250, 2, 8f, true, p, 50f),
            Hoja("caer", r + "Fall.png", 250, 250, 2, 8f, true, p, 50f),
            Hoja("golpe", r + "Take hit.png", 250, 250, 3, 10f, false, p, 50f),
            Hoja("muerte", r + "Death.png", 250, 250, 7, 9f, false, p, 50f),
        };
        GameObject go = BaseEnemigo("HechiceroSombrio", clips, 180, 2.3f, 1.2f, 200, out AnimadorHoja anim);
        BoxCollider2D box = go.AddComponent<BoxCollider2D>();
        box.size = new Vector2(0.7f, 1.7f);
        box.offset = new Vector2(0f, 0.85f);
        SerializedObject so = new SerializedObject(go.GetComponent<EnemyHealth>());
        so.FindProperty("bodyMass").floatValue = 90f;
        so.ApplyModifiedPropertiesWithoutUndo();
        Afinidad(go, 1f, 1f, 0.2f, 1.7f, 0.7f);
        Enlazar(go.AddComponent<EnemigoHechicero>(), anim);
        return Guardar(go, "HechiceroSombrio");
    }

    // ------------------------------------------------------------------ Pistas

    private static readonly (Vector2 pos, string texto)[] Pistas =
    {
        (new Vector2(13f, 0f), "Junto a la hoguera, una espada clavada en la nieve. Grabado en la empuñadura: «Pulsa E lejos del fuego y elige un elemento para tu hoja. Cada criatura de estas cumbres teme a uno distinto»."),
        (new Vector2(56f, 2f), "El hielo de la ladera brilla como un espejo. Quien corre sobre él no frena cuando quiere."),
        (new Vector2(119f, 3f), "Más allá, la ventisca aúlla sin descanso. Avanza entre ráfaga y ráfaga: cuando la nieve se espesa, el viento está a punto de empujar."),
        (new Vector2(197f, 3f), "El lago humea de frío, a un suspiro de congelarse. Un tajo de escarcha bastaría para dormir sus aguas."),
        (new Vector2(242f, 3f), "Un muro de hielo antiguo sella el túnel. Hay marcas de quemaduras en la roca: alguien lo abrió así antes."),
        (new Vector2(296f, 3f), "Una sombra viva tapa una grieta al pie de la meseta. Retrocede ante cualquier resplandor sagrado."),
        (new Vector2(357f, 3f), "Un guerrero congelado aún empuña su espada. En su escudo, arañado: «La sombra teme la luz... pero cuando se alza de hielo, solo el fuego la quiebra. Y nunca, nunca dejes que te atrape: cuando se agache y brille en rojo, corre»."),
    };

    private static void ConstruirPistas(Transform nivel)
    {
        Transform padre = Hijo(nivel, "Pistas");
        int items = LayerMask.NameToLayer("Items");
        foreach (var (pos, texto) in Pistas)
        {
            GameObject go = new GameObject("Pista");
            go.layer = items;
            go.transform.SetParent(padre);
            go.transform.position = new Vector3(pos.x, pos.y + 2f, 0f);
            go.AddComponent<BoxCollider2D>().size = new Vector2(1.5f, 4f);
            PistaNarrativa p = go.AddComponent<PistaNarrativa>();
            SerializedObject so = new SerializedObject(p);
            so.FindProperty("texto").stringValue = texto;
            so.FindProperty("duracion").floatValue = 8f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    // ------------------------------------------------------------------ Camara, player y ambiente

    private static readonly (string nombre, Rect zona, float tamano, float arriba)[] ZonasCamara =
    {
        ("Ventisca", Rect.MinMaxRect(122f, 1f, 192f, 16f), 4.7f, 0.4f),
        ("Lago", Rect.MinMaxRect(196f, 1f, 220f, 14f), 4.7f, 0.2f),
        ("Ruinas", Rect.MinMaxRect(282f, 3f, 345f, 22f), 4.7f, 0.8f),
        ("Arena", Rect.MinMaxRect(362.5f, 3f, 398f, 20f), 5.4f, 1.4f),
    };

    private static void ConstruirZonasCamara(Transform nivel)
    {
        Transform padre = Hijo(nivel, "ZonasCamara");
        int capa = LayerMask.NameToLayer("Items");
        foreach (var (nombre, zona, tamano, arriba) in ZonasCamara)
        {
            GameObject go = new GameObject("Zona_" + nombre);
            go.layer = capa;
            go.transform.SetParent(padre);
            go.transform.position = zona.center;
            BoxCollider2D box = go.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = zona.size;
            ZonaCamara z = go.AddComponent<ZonaCamara>();
            SerializedObject so = new SerializedObject(z);
            so.FindProperty("tamano").floatValue = tamano;
            so.FindProperty("desplazamientoY").floatValue = arriba;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void ColocarPlayerYCamara(Scene escena)
    {
        Vector3 salida = new Vector3(Salida.x, Salida.y + 0.7f, 0f);
        foreach (GameObject raiz in escena.GetRootGameObjects())
        {
            switch (raiz.name)
            {
                case "Player":
                case "RespawnPoint":
                case "Main Camera":
                    raiz.transform.position = new Vector3(salida.x, salida.y, raiz.transform.position.z);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(raiz.transform);
                    break;
                case "EntranceDoor":
                    raiz.transform.position = new Vector3(Salida.x - 1f, 0f, 0f);
                    ApoyarEnSuelo(raiz, Salida.y);
                    break;
                case "ExitDoor":
                    raiz.transform.position = new Vector3(394f, 3f, 0f);
                    ApoyarEnSuelo(raiz, 3f);
                    raiz.SetActive(false);
                    break;
                case "CinemachineCamera":
                    if (raiz.GetComponent<CamaraDinamica>() == null) raiz.AddComponent<CamaraDinamica>();
                    break;
                case "CameraLimit":
                    raiz.transform.position = Vector3.zero;
                    raiz.transform.localScale = Vector3.one;
                    PolygonCollider2D poly = raiz.GetComponent<PolygonCollider2D>();
                    if (poly != null)
                    {
                        poly.offset = Vector2.zero;
                        poly.pathCount = 1;
                        poly.SetPath(0, new[] { new Vector2(-4f, -2.5f), new Vector2(398f, -2.5f), new Vector2(398f, 26f), new Vector2(-4f, 26f) });
                    }
                    break;
            }
        }
    }

    // Luz fria, nieve que cae alrededor de la camara, musica y ambiente del nivel.
    private static void Ambiente(Transform nivel, Scene escena)
    {
        foreach (GameObject raiz in escena.GetRootGameObjects())
        foreach (UnityEngine.Rendering.Universal.Light2D luz in raiz.GetComponentsInChildren<UnityEngine.Rendering.Universal.Light2D>(true))
        {
            if (luz.lightType != UnityEngine.Rendering.Universal.Light2D.LightType.Global) continue;
            luz.color = new Color(0.85f, 0.9f, 1f);
            luz.intensity = 1f;
            SerializedObject so = new SerializedObject(luz);
            SerializedProperty capas = so.FindProperty("m_ApplyToSortingLayers");
            if (capas == null) continue;
            SortingLayer[] todas = SortingLayer.layers;
            capas.arraySize = todas.Length;
            for (int i = 0; i < todas.Length; i++) capas.GetArrayElementAtIndex(i).intValue = todas[i].id;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Nieve: un sistema de particulas que sigue a la camara.
        GameObject cam = escena.GetRootGameObjects().FirstOrDefault(r => r.name == "Main Camera");
        if (cam != null)
        {
            GameObject nieve = new GameObject("NieveCayendo");
            nieve.transform.SetParent(cam.transform, false);
            nieve.transform.localPosition = new Vector3(0f, 7f, 10f);
            nieve.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            ParticleSystem ps = nieve.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.11f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 1f, 0.9f), new Color(0.85f, 0.92f, 1f, 0.6f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 600;
            main.prewarm = true;
            var em = ps.emission;
            em.rateOverTime = 70f;
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Box;
            sh.scale = new Vector3(30f, 2f, 1f);
            var ruido = ps.noise;
            ruido.enabled = true;
            ruido.strength = 0.35f;
            ruido.frequency = 0.25f;
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-0.5f, -0.1f);
            vel.y = new ParticleSystem.MinMaxCurve(0f, 0f);
            vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            ParticleSystemRenderer pr = nieve.GetComponent<ParticleSystemRenderer>();
            Shader s = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            pr.sharedMaterial = new Material(s != null ? s : Shader.Find("Sprites/Default"));
            AssetDatabase.CreateAsset(pr.sharedMaterial, CarpetaTiles + "/NieveParticulas.mat");
            pr.sortingLayerName = "VFX";
            pr.sortingOrder = -2;
        }

        GameObject datos = new GameObject("DatosNivel");
        datos.transform.SetParent(nivel);
        DatosNivel d = datos.AddComponent<DatosNivel>();
        SerializedObject sd = new SerializedObject(d);
        SerializedProperty frases = sd.FindProperty("frasesMuerte");
        string[] lista =
        {
            "El frío no perdona a los que dudan.",
            "La nieve cubrirá tus huellas... y tu cuerpo.",
            "Levántate. La ventisca aún no ha terminado contigo.",
            "Algo en la bruma se ríe de tu caída.",
            "Hasta el hielo más firme se quiebra. Tú también.",
        };
        frases.arraySize = lista.Length;
        for (int i = 0; i < lista.Length; i++) frases.GetArrayElementAtIndex(i).stringValue = lista[i];
        sd.FindProperty("musica").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(MusicaNivel);
        sd.FindProperty("volumenMusica").floatValue = 0.3f;
        RecursosRPG.GrupoSonido amb = AssetDatabase.LoadAssetAtPath<RecursosRPG>("Assets/Resources/RecursosRPG.asset").Sonido("ambiente_nieve");
        if (amb != null && amb.clips.Length > 0) sd.FindProperty("ambiente").objectReferenceValue = amb.clips[0];
        sd.FindProperty("volumenAmbiente").floatValue = 0.35f;
        sd.ApplyModifiedPropertiesWithoutUndo();
    }

    // ------------------------------------------------------------------ Jefe

    public const string MusicaFase1 = Musica + "Sonidos_BossThemesNuevos/Boss Theme 1.mp3";
    public const string MusicaFase2 = Musica + "Sonidos_BossFightPack/03 - opus - Opus.ogg";
    public const string MusicaNivel = Musica + "BossMusic#1.ogg";
    public const string MusicaCueva = Musica + "Sonidos_BossFightPack/01 - The final revalation - W-The Final Revalation.ogg";

    private static GameObject PrefabJefe()
    {
        Vector2 p = new Vector2(0.435f, 0f);
        const float ppu = 22f;
        var clips = new List<AnimadorHoja.Clip>
        {
            Hoja("quieto", Jefe + "idle.png", 247, 87, 14, 10f, true, p, ppu),
            Hoja("andar", Jefe + "walk.png", 247, 87, 14, 12f, true, p, ppu),
            Hoja("tajo1", Jefe + "Attack 1.png", 247, 87, 10, 12f, false, p, ppu),
            Hoja("tajo2", Jefe + "Attack 2.png", 247, 87, 9, 12f, false, p, ppu),
            Hoja("carga", Jefe + "new/Pre-Attack 3.png", 247, 87, 3, 8f, true, p, ppu),
            Hoja("esfera", Jefe + "new/mid-Attack 3.png", 247, 87, 4, 12f, true, p, ppu),
            Hoja("emerger", Jefe + "new/end-Attack 3.png", 247, 87, 7, 12f, false, p, ppu),
            Hoja("golpe", Jefe + "new/hit.png", 247, 87, 1, 1f, true, p, ppu),
            Hoja("caer", Jefe + "new/death.png", 247, 87, 14, 10f, false, p, ppu),
            Hoja("levantarse", Jefe + "new/death.png", 247, 87, 14, 9f, false, p, ppu, 0, 0, true),
            Hoja("muerte", Jefe + "new/death.png", 247, 87, 33, 12f, false, p, ppu),
        };
        GameObject go = BaseEnemigo("SombraHumedales", clips, 1500, 3f, 3.5f, 4000, out AnimadorHoja anim);
        anim.destino.sortingOrder = -2;

        Rigidbody2D rb = go.GetComponent<Rigidbody2D>();
        rb.gravityScale = 3f;
        BoxCollider2D box = go.AddComponent<BoxCollider2D>();
        box.size = new Vector2(1.1f, 2.4f);
        box.offset = new Vector2(0f, 1.2f);
        SerializedObject so = new SerializedObject(go.GetComponent<EnemyHealth>());
        so.FindProperty("showHealthBar").boolValue = false;
        so.FindProperty("inamovible").boolValue = true;
        so.FindProperty("bodyMass").floatValue = 500f;
        so.ApplyModifiedPropertiesWithoutUndo();
        go.AddComponent<Unity.Cinemachine.CinemachineImpulseSource>();
        go.AddComponent<NoReaparece>();

        JefeSombra j = go.AddComponent<JefeSombra>();
        Enlazar(j, anim);
        Vector2 c = new Vector2(0.5f, 0.5f);
        string hielo = FxPlayer + "Efecto_CaballeroHielo/";
        j.fxMedialuna = ConfigurarRecursosRPG.Clip("medialuna", hielo + "VFX2/frames", 14f, true, 64f, c, 3, 6);
        j.fxLanza = ConfigurarRecursosRPG.Clip("lanza", hielo + "VFX1/frames", 12f, true, 64f, new Vector2(0.62f, 0.8f), 0, 5);
        j.fxLanzaImpacto = ConfigurarRecursosRPG.Clip("lanza_impacto", hielo + "VFX1/frames", 18f, false, 64f, c, 8, 13);
        j.fxEstaca = ConfigurarRecursosRPG.Clip("estaca", hielo + "VFX3/Frames", 18f, false, 64f, new Vector2(0.5f, 0.1f));
        j.fxMarca = ConfigurarRecursosRPG.Clip("marca", FxEnemigos + "Efecto_MagoSangre/VFX1/part2(loop)/frames", 12f, true, 64f, c);
        j.fxOnda = ConfigurarRecursosRPG.Clip("onda", FxEnemigos + "Efecto_Warlock/VFX2/Frames", 14f, true, 64f, new Vector2(0.5f, 0.42f), 3, 10);
        j.fxPortal = ConfigurarRecursosRPG.Clip("portal", FxEnemigos + "Efecto_Portal/Frames", 12f, false, 32f, c);
        j.fxPolvo = ConfigurarRecursosRPG.Clip("polvo", FxEnemigos + "Efecto_HumoPolvo/VFX2/frames", 12f, false, 64f, new Vector2(0.5f, 0.3f));
        j.fxAnillo = ConfigurarRecursosRPG.Clip("anillo", FxEnemigos + "Efecto_MagoFuego/VFX3/frames", 14f, false, 64f, c);
        RecursosRPG rec = AssetDatabase.LoadAssetAtPath<RecursosRPG>("Assets/Resources/RecursosRPG.asset");
        j.fxCorte = rec.Tajo(Elemento.Oscuro, 0);
        j.fxCorteHielo = rec.Tajo(Elemento.Hielo, 0);
        j.shaderBrillo = Shader.Find("Sprites/BladeGlow");
        EditorUtility.SetDirty(j);
        return Guardar(go, "SombraHumedales");
    }

    private static void ConstruirArena(Transform nivel, Scene escena)
    {
        Transform padre = Hijo(nivel, "ArenaJefe");
        int items = LayerMask.NameToLayer("Items");

        GameObject niebla = new GameObject("NieblaJefe");
        niebla.layer = LayerMask.NameToLayer("Ground");
        niebla.transform.SetParent(padre);
        niebla.transform.position = new Vector3(362.6f, 7f, 0f);
        BoxCollider2D muro = niebla.AddComponent<BoxCollider2D>();
        muro.size = new Vector2(0.8f, 8f);
        AnimadorHoja.Clip humo = ConfigurarRecursosRPG.Clip("humo", FxEnemigos + "Efecto_HumoPolvo/VFX1/frames", 8f, true, 64f, new Vector2(0.5f, 0.5f));
        var humos = new List<SpriteRenderer>();
        for (int i = 0; i < 5; i++)
        {
            SpriteRenderer sr = new GameObject("Humo").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(niebla.transform, false);
            sr.transform.localPosition = new Vector3(i % 2 == 0 ? -0.15f : 0.15f, -3.2f + i * 1.6f, 0f);
            sr.transform.localScale = new Vector3(1.3f, 1.6f, 1f);
            sr.sortingLayerName = "VFX";
            sr.sortingOrder = 2;
            AnimadorHoja a = sr.gameObject.AddComponent<AnimadorHoja>();
            a.destino = sr;
            a.clips = new List<AnimadorHoja.Clip> { humo };
            a.clipInicial = "humo";
            humos.Add(sr);
        }

        GameObject gestor = new GameObject("GestorArena");
        gestor.layer = items;
        gestor.transform.SetParent(padre);
        gestor.transform.position = new Vector3(381f, 11f, 0f);
        gestor.AddComponent<BoxCollider2D>().size = new Vector2(33f, 16f);
        ArenaJefe arena = gestor.AddComponent<ArenaJefe>();

        GameObject jefe = PrefabJefe();
        SerializedObject so = new SerializedObject(arena);
        so.FindProperty("prefabJefe").objectReferenceValue = jefe != null ? jefe.GetComponent<JefeSombra>() : null;
        so.FindProperty("nombre").stringValue = "Sombra de los Humedales";
        so.FindProperty("titulo").stringValue = "La que duerme bajo el hielo";
        so.FindProperty("zona").rectValue = Rect.MinMaxRect(362.8f, 3f, 397.8f, 20f);
        so.FindProperty("suelo").floatValue = 3f;
        string[] burlas =
        {
            "«La escarcha guarda tu último aliento.»",
            "«Te vi dudar. Las sombras nunca dudan.»",
            "«Otro guerrero congelado para mi pantano.»",
            "«¿Esquivar? Ya estabas en mis garras.»",
            "«Tu llama es débil. La mía no se apaga.»",
            "«Vuelve a la hoguera, pequeño. Aquí te espero.»",
            "«Mi segunda piel es hielo. La tuya, carne.»",
            "«Cada vez que caes, el invierno se alarga.»",
        };
        SerializedProperty sb = so.FindProperty("burlas");
        sb.arraySize = burlas.Length;
        for (int i = 0; i < burlas.Length; i++) sb.GetArrayElementAtIndex(i).stringValue = burlas[i];
        so.FindProperty("marcaFase").floatValue = 0f;
        so.FindProperty("barraPorFase").boolValue = true;
        so.FindProperty("colorBarraFase2").colorValue = new Color(0.25f, 0.62f, 0.95f, 1f);
        so.FindProperty("nombreFase2").stringValue = "Sombra de los Humedales — Escarcha";
        so.FindProperty("muroNiebla").objectReferenceValue = muro;
        SerializedProperty vn = so.FindProperty("visualNiebla");
        vn.arraySize = humos.Count;
        for (int i = 0; i < humos.Count; i++) vn.GetArrayElementAtIndex(i).objectReferenceValue = humos[i];
        so.FindProperty("nieblaAbierta").colorValue = new Color(0.7f, 0.85f, 1f, 0.25f);
        so.FindProperty("nieblaCerrada").colorValue = new Color(0.75f, 0.9f, 1f, 0.8f);
        GameObject salida = escena.GetRootGameObjects().FirstOrDefault(r => r.name == "ExitDoor");
        if (salida != null) so.FindProperty("salida").objectReferenceValue = salida;
        // Fase 1: Boss Theme 1, que va creciendo (tramo contenido, 0-102 s).
        so.FindProperty("musica").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(MusicaFase1);
        so.FindProperty("volumen").floatValue = 0.6f;
        so.FindProperty("inicioFase1").floatValue = 0f;
        so.FindProperty("bucleFase1").vector2Value = new Vector2(6f, 102f);
        // Fase 2: Opus desde su estallido (60 s), la parte mas intensa de todas.
        so.FindProperty("musicaFase2").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(MusicaFase2);
        so.FindProperty("volumenFase2").floatValue = 0.62f;
        so.FindProperty("inicioFase2").floatValue = 60f;
        so.FindProperty("bucleFase2").vector2Value = new Vector2(60f, 290f);
        so.FindProperty("luzFase2").colorValue = new Color(0.62f, 0.8f, 1f, 1f);
        so.FindProperty("textoFase2").stringValue = "La sombra se alza de nuevo, envuelta en escarcha...";
        so.FindProperty("particulasFase2A").colorValue = new Color(1f, 1f, 1f, 0.9f);
        so.FindProperty("particulasFase2B").colorValue = new Color(0.7f, 0.9f, 1f, 0.7f);
        so.FindProperty("particulasCaen").boolValue = true;
        so.FindProperty("bannerVictoria").stringValue = "SOMBRA DISIPADA";
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ------------------------------------------------------------------ Cueva (jefe anterior)

    // Musica por fases en la arena del Espectro Carmesi, sin regenerar la cueva:
    // fase 1 la parte contenida de "The Final Revalation" (0-156 s) y fase 2 su
    // climax final (desde 328 s). Tambien la pista de su debilidad (ahora sagrada).
    [MenuItem("Warrior/Actualizar jefe de la Cueva")]
    public static void ActualizarCueva()
    {
        const string cueva = "Assets/Scenes/Nivel Cueva.unity";
        if (!File.Exists(cueva)) { Debug.LogWarning("[Nieve] No existe " + cueva); return; }
        PrepararImportacion();
        Scene escena = EditorSceneManager.OpenScene(cueva, OpenSceneMode.Single);
        ArenaJefe arena = Object.FindFirstObjectByType<ArenaJefe>();
        if (arena != null)
        {
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(MusicaCueva);
            SerializedObject so = new SerializedObject(arena);
            so.FindProperty("musica").objectReferenceValue = clip;
            so.FindProperty("inicioFase1").floatValue = 0f;
            so.FindProperty("bucleFase1").vector2Value = new Vector2(8f, 156f);
            so.FindProperty("musicaFase2").objectReferenceValue = clip;
            so.FindProperty("volumenFase2").floatValue = 0.7f;
            so.FindProperty("inicioFase2").floatValue = 328f;
            so.FindProperty("bucleFase2").vector2Value = new Vector2(336f, 410f);
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        foreach (PistaNarrativa p in Object.FindObjectsByType<PistaNarrativa>(FindObjectsSortMode.None))
        {
            SerializedObject so = new SerializedObject(p);
            SerializedProperty t = so.FindProperty("texto");
            if (!t.stringValue.Contains("puño")) continue;
            t.stringValue = "Arañazos profundos cubren la roca. Alguien escribió con sangre: «Su piel carmesí se ríe del acero desnudo... solo la luz sagrada quiebra su verdadera forma».";
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
        Debug.Log("[Nieve] Arena de la cueva actualizada (musica por fases y pista).");
    }

    // Para la linea de comandos: la cueva, el nivel nuevo y capturas.
    public static void CrearTodo()
    {
        Crear();
        ActualizarCueva();
        Capturas();
    }

    // ------------------------------------------------------------------ Capturas

    [MenuItem("Warrior/Capturas del nivel Nieve")]
    public static void Capturas()
    {
        if (SceneManager.GetActiveScene().path != Destino) EditorSceneManager.OpenScene(Destino, OpenSceneMode.Single);
        CapturarZona(Rect.MinMaxRect(-6f, -4f, 110f, 18f), 4000, "NivelNieve_1.png");
        CapturarZona(Rect.MinMaxRect(105f, -4f, 230f, 18f), 4000, "NivelNieve_2.png");
        CapturarZona(Rect.MinMaxRect(225f, -4f, 405f, 24f), 4600, "NivelNieve_3.png");
        CapturarZona(Rect.MinMaxRect(-2f, -2f, 22f, 10f), 2400, "NivelNieve_inicio.png");
        CapturarZona(Rect.MinMaxRect(190f, -1f, 252f, 12f), 2800, "NivelNieve_lago.png");
        CapturarZona(Rect.MinMaxRect(355f, 1f, 400f, 22f), 2800, "NivelNieve_arena.png");
    }

    private static void CapturarZona(Rect vista, int ancho, string archivo)
    {
        foreach (ParallaxCueva p in Object.FindObjectsByType<ParallaxCueva>(FindObjectsSortMode.None))
            foreach (ParallaxCueva.Capa c in p.capas)
                if (c.raiz != null) c.raiz.position = new Vector3(vista.center.x, vista.center.y, 0f);
        int alto = Mathf.RoundToInt(ancho * vista.height / vista.width);
        Camera cam = new GameObject("CamaraCaptura").AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = vista.height * 0.5f;
        cam.aspect = vista.width / vista.height;
        cam.transform.position = new Vector3(vista.center.x, vista.center.y, -10f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.2f, 0.2f, 0.25f);
        RenderTexture rt = new RenderTexture(ancho, alto, 24);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        Texture2D tex = new Texture2D(ancho, alto, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, ancho, alto), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(), archivo), tex.EncodeToPNG());
        Object.DestroyImmediate(cam.gameObject);
        Object.DestroyImmediate(rt);
    }

    // ------------------------------------------------------------------ Utilidades

    private static void AnadirABuild()
    {
        List<EditorBuildSettingsScene> escenas = EditorBuildSettings.scenes.ToList();
        if (escenas.Any(s => s.path == Destino)) return;
        escenas.Add(new EditorBuildSettingsScene(Destino, true));
        EditorBuildSettings.scenes = escenas.ToArray();
    }

    private static void ApoyarEnSuelo(GameObject go, float suelo)
    {
        SpriteRenderer sr = go.GetComponentInChildren<SpriteRenderer>();
        if (sr == null) return;
        go.transform.position += Vector3.up * (suelo - sr.bounds.min.y);
    }

    private static Transform Hijo(Transform padre, string nombre)
    {
        Transform t = new GameObject(nombre).transform;
        t.SetParent(padre, false);
        return t;
    }

    private static Sprite[] Sprites(string ruta)
    {
        Sprite[] s = AssetDatabase.LoadAllAssetsAtPath(ruta).OfType<Sprite>().ToArray();
        if (s.Length == 0) Debug.LogWarning("[Nieve] Sin sprites en " + ruta);
        return s;
    }

    private static Sprite PrimerSprite(string ruta)
    {
        return Sprites(ruta).OrderByDescending(s => s.rect.width * s.rect.height).FirstOrDefault();
    }

    private static SpriteRenderer Pieza(Transform padre, string nombre, Sprite s, Vector2 centro, Vector2 tamano, string capa, int orden)
    {
        SpriteRenderer sr = new GameObject(nombre).AddComponent<SpriteRenderer>();
        sr.transform.SetParent(padre, true);
        sr.sprite = s;
        sr.sortingLayerName = capa;
        sr.sortingOrder = orden;
        if (s == null) { sr.transform.position = centro; return sr; }
        Vector2 escala = new Vector2(tamano.x / s.bounds.size.x, tamano.y / s.bounds.size.y);
        sr.transform.localScale = new Vector3(escala.x, escala.y, 1f);
        Vector2 desvio = new Vector2(s.bounds.center.x * escala.x, s.bounds.center.y * escala.y);
        sr.transform.position = new Vector3(centro.x - desvio.x, centro.y - desvio.y, 0f);
        return sr;
    }

    private static SpriteRenderer PiezaApoyada(Transform padre, string nombre, Sprite s, float x, float suelo, float alto,
                                               string capa, int orden, Color color)
    {
        float ancho = alto * s.rect.width / s.rect.height;
        SpriteRenderer sr = Pieza(padre, nombre, s, new Vector2(x, suelo + alto * 0.5f), new Vector2(ancho, alto), capa, orden);
        sr.color = color;
        return sr;
    }

    private static void Asignar(Object objetivo, string campo, Object valor)
    {
        SerializedObject so = new SerializedObject(objetivo);
        SerializedProperty p = so.FindProperty(campo);
        if (p == null) { Debug.LogWarning($"[Nieve] {objetivo.GetType().Name} no tiene el campo {campo}"); return; }
        p.objectReferenceValue = valor;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AsignarFloat(Object objetivo, string campo, float valor)
    {
        SerializedObject so = new SerializedObject(objetivo);
        SerializedProperty p = so.FindProperty(campo);
        if (p == null) { Debug.LogWarning($"[Nieve] {objetivo.GetType().Name} no tiene el campo {campo}"); return; }
        p.floatValue = valor;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
