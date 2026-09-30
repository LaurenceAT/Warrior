using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// Cueva renovada (Warrior > Cueva > Generar cueva nueva).
//
// La cueva de siempre se guarda como copia en Assets/Scenes/Cueva_Antigua.unity
// (fuera de la lista de escenas) y la nueva se monta en "Nivel Cueva" partiendo
// de esa copia: asi conserva el GameManager, la camara, el HUD, el player, la
// arena del Crimson Wraith (medidas, colisiones, musica, camara), las hogueras,
// la estatua, el cofre, las puertas y los enemigos (se recolocan). El resto se
// rehace con el pack Mapa_CuevaPixelFantasy a 28 px por unidad (como el player):
//   - Roca en Tilemaps de casillas de 16 px (4/7 de unidad): relleno, paredes,
//     techos con estalactitas y suelos con borde, y una capa de colision aparte
//     (Composite, sin huecos).
//   - Sin trampas: grietas mortales con niebla, suelos falsos, paredes
//     ilusorias con salas de almas, un derrumbe que abre un atajo, zonas oscuras
//     con antorchas y brasas guia.
//   - Fondo parallax con los fondos del pack, bruma, polvo y gotas.
// El plano esta en "SPRITES PARA NUEVOS NIVELES/DISENO_CUEVA.md".
//
// Para cambiar de cueva: Warrior > Cueva > Usar la cueva antigua / nueva
// (intercambia los archivos; el juego siempre carga "Nivel Cueva").
public static class CrearCuevaNueva
{
    public const string RutaActiva = "Assets/Scenes/Nivel Cueva.unity";
    public const string RutaAntigua = "Assets/Scenes/Cueva_Antigua.unity";
    public const string RutaNuevaGuardada = "Assets/Scenes/Cueva_Nueva.unity";
    private const string Pack = "Assets/SPRITES PARA NUEVOS NIVELES/MAPAS/Mapa_CuevaPixelFantasy/";
    private const string Tileset = Pack + "mainlev_build.png";
    private const string Props1 = Pack + "props1.png";
    private const string Props2 = Pack + "props2.png";
    private const string Antorcha = "Assets/SPRITES PARA NUEVOS NIVELES/MAPAS/Mapa_BosqueGandalf/Torch.png";
    private const string Sonidos = "Assets/SPRITES PARA NUEVOS NIVELES/SONIDOS/Sonidos_Efectos/Sonidos_FantasyGeneral/OGG Files/";
    private const string CarpetaDatos = "Assets/Data/CuevaNueva";
    private const string CarpetaTiles = CarpetaDatos + "/Tiles";
    private const string CarpetaSprites = "Assets/Sprites/CuevaNueva";
    private const string ConfigCueva = "Assets/Data/Niveles/Config Nivel Cueva.asset";

    public const float PPU = 28f;
    // Una casilla: 16 px del pack a 28 px por unidad.
    public const float S = 16f / PPU;
    private static int C(float u) => Mathf.RoundToInt(u / S);
    private static float U(int c) => c * S;

    // ------------------------------------------------------------------ Piezas del pack
    // Rectangulos en pixeles desde arriba a la izquierda (como en un editor de imagenes).
    private static readonly RectInt[] Relleno =
    {
        new RectInt(32, 32, 16, 16), new RectInt(48, 32, 16, 16), new RectInt(32, 48, 16, 16), new RectInt(48, 48, 16, 16),
    };
    private static readonly RectInt[] RellenoTextura =
    {
        new RectInt(128, 32, 16, 16), new RectInt(160, 32, 16, 16), new RectInt(192, 32, 16, 16), new RectInt(160, 64, 16, 16),
        new RectInt(192, 64, 16, 16), new RectInt(480, 32, 16, 16), new RectInt(512, 32, 16, 16), new RectInt(480, 64, 16, 16), new RectInt(512, 64, 16, 16),
    };
    private static readonly RectInt[] Suelos =
    {
        new RectInt(192, 368, 16, 32), new RectInt(224, 368, 32, 32), new RectInt(272, 368, 16, 32), new RectInt(304, 368, 16, 32),
        new RectInt(336, 368, 32, 32), new RectInt(384, 368, 32, 32), new RectInt(432, 368, 32, 32), new RectInt(480, 368, 16, 32),
    };
    private static readonly RectInt[] Techos =
    {
        new RectInt(224, 96, 32, 36), new RectInt(272, 96, 16, 32), new RectInt(304, 96, 32, 32),
        new RectInt(352, 96, 16, 41), new RectInt(384, 96, 16, 32), new RectInt(416, 96, 32, 32),
    };
    // Pared con la roca a la izquierda y el hueco a la derecha (borde claro a la derecha).
    private static readonly RectInt[] ParedDerecha =
    {
        new RectInt(192, 320, 16, 32), new RectInt(224, 320, 32, 32),
        new RectInt(192, 224, 16, 16), new RectInt(224, 224, 16, 16), new RectInt(256, 224, 16, 16),
        new RectInt(192, 256, 16, 16), new RectInt(224, 256, 16, 16), new RectInt(256, 256, 16, 16),
    };
    // Y al reves (hueco a la izquierda): las mismas piezas del otro lado del pack.
    private static RectInt Espejo(RectInt r) => new RectInt(688 - r.x - r.width, r.y, r.width, r.height);
    private static readonly RectInt[] ParedIzquierda = ParedDerecha.Select(Espejo).ToArray();

    // Madera de mina y ruinas de ladrillo (decoracion).
    private static readonly (string nombre, RectInt r)[] Adornos =
    {
        ("marco", new RectInt(960, 112, 48, 64)), ("puerta", new RectInt(960, 16, 48, 64)),
        ("caja", new RectInt(752, 96, 48, 48)), ("caja2", new RectInt(816, 96, 48, 48)),
        ("aspa", new RectInt(880, 96, 48, 48)), ("tablon", new RectInt(754, 16, 172, 16)),
        ("pilar_ladrillo", new RectInt(832, 288, 32, 92)), ("pilar_ladrillo2", new RectInt(976, 288, 32, 92)),
        ("columna_ladrillo", new RectInt(880, 400, 32, 96)), ("columna_ladrillo2", new RectInt(928, 400, 32, 96)),
        ("muro_ladrillo", new RectInt(832, 512, 80, 80)), ("muro_ladrillo2", new RectInt(928, 512, 80, 80)),
    };

    // ------------------------------------------------------------------ Menu

    [MenuItem("Warrior/Cueva/Generar cueva nueva")]
    public static void Generar()
    {
        if (!Application.isBatchMode && !EditorUtility.DisplayDialog("Cueva nueva",
                "Se rehace la escena \"Nivel Cueva\" a partir de la copia de la cueva antigua (Cueva_Antigua). " +
                "Lo que hayas cambiado a mano en la cueva nueva se pierde.", "Generar", "Cancelar"))
            return;
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        // La primera vez: la cueva de siempre pasa a ser la copia de seguridad.
        if (!File.Exists(RutaAntigua))
        {
            if (!AssetDatabase.CopyAsset(RutaActiva, RutaAntigua)) { Debug.LogError("[CuevaNueva] No se pudo copiar la cueva antigua"); return; }
            AssetDatabase.Refresh();
        }
        Preparar();
        Montar();
    }

    // Intercambia los archivos: la que se juega es siempre "Nivel Cueva".
    [MenuItem("Warrior/Cueva/Usar la cueva antigua")]
    public static void UsarAntigua()
    {
        if (!File.Exists(RutaAntigua)) { Debug.LogWarning("[CuevaNueva] No hay cueva antigua guardada."); return; }
        if (File.Exists(RutaNuevaGuardada)) { Debug.LogWarning("[CuevaNueva] La cueva antigua ya es la que se juega."); return; }
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        AssetDatabase.MoveAsset(RutaActiva, RutaNuevaGuardada);
        AssetDatabase.MoveAsset(RutaAntigua, RutaActiva);
        AsegurarEnBuild();
        Debug.Log("[CuevaNueva] Ahora se juega la cueva ANTIGUA (la nueva queda en Cueva_Nueva).");
    }

    [MenuItem("Warrior/Cueva/Usar la cueva nueva")]
    public static void UsarNueva()
    {
        if (!File.Exists(RutaNuevaGuardada)) { Debug.LogWarning("[CuevaNueva] La cueva nueva ya es la que se juega."); return; }
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        AssetDatabase.MoveAsset(RutaActiva, RutaAntigua);
        AssetDatabase.MoveAsset(RutaNuevaGuardada, RutaActiva);
        AsegurarEnBuild();
        Debug.Log("[CuevaNueva] Ahora se juega la cueva NUEVA (la antigua queda en Cueva_Antigua).");
    }

    // "Nivel Cueva" en la lista de escenas (con el GUID del archivo que tenga ahora
    // ese nombre); las copias, fuera.
    private static void AsegurarEnBuild()
    {
        var escenas = EditorBuildSettings.scenes.Where(s => s.path != RutaAntigua && s.path != RutaNuevaGuardada).ToList();
        int i = escenas.FindIndex(s => s.path == RutaActiva);
        var nueva = new EditorBuildSettingsScene(RutaActiva, true);
        if (i >= 0) escenas[i] = nueva; else escenas.Add(nueva);
        EditorBuildSettings.scenes = escenas.ToArray();
    }

    // ------------------------------------------------------------------ Importacion

    private static Dictionary<string, Sprite> piezas;
    private static Dictionary<string, Tile> tiles;

    public static void Preparar()
    {
        Directory.CreateDirectory(CarpetaTiles);
        Directory.CreateDirectory(CarpetaSprites);

        // El tileset: pixel nitido, sin compresion ni mipmaps, a 28 px por unidad,
        // cortado en las piezas que usa la cueva (cada una con su punto de anclaje:
        // arriba a la izquierda de la casilla donde se pone).
        var metas = new List<SpriteMetaData>();
        void Meta(string nombre, RectInt r, Vector2 pivote)
            => metas.Add(new SpriteMetaData { name = nombre, rect = new Rect(r.x, 1024 - r.y - r.height, r.width, r.height), alignment = (int)SpriteAlignment.Custom, pivot = pivote });
        Vector2 Ancla(RectInt r, bool derecha = false) => new Vector2(derecha ? 1f - 8f / r.width : 8f / r.width, 1f - 8f / r.height);
        for (int i = 0; i < Relleno.Length; i++) Meta("relleno_" + i, Relleno[i], new Vector2(0.5f, 0.5f));
        for (int i = 0; i < RellenoTextura.Length; i++) Meta("relleno_t" + i, RellenoTextura[i], new Vector2(0.5f, 0.5f));
        for (int i = 0; i < Suelos.Length; i++) Meta("suelo_" + i, Suelos[i], Ancla(Suelos[i]));
        for (int i = 0; i < Techos.Length; i++) Meta("techo_" + i, Techos[i], Ancla(Techos[i]));
        for (int i = 0; i < ParedDerecha.Length; i++) Meta("paredD_" + i, ParedDerecha[i], Ancla(ParedDerecha[i], true));
        for (int i = 0; i < ParedIzquierda.Length; i++) Meta("paredI_" + i, ParedIzquierda[i], Ancla(ParedIzquierda[i]));
        foreach (var a in Adornos) Meta("adorno_" + a.nombre, a.r, new Vector2(0.5f, 0f));

        TextureImporter ti = (TextureImporter)AssetImporter.GetAtPath(Tileset);
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Multiple;
        ti.spritePixelsPerUnit = PPU;
        PixelNitido(ti);
#pragma warning disable 618
        ti.spritesheet = metas.ToArray();
#pragma warning restore 618
        ti.SaveAndReimport();

        // Fondos: un sprite cada uno, a 28 px por unidad.
        foreach (string f in new[] { "background1", "background2", "background3", "background4a", "background4b" })
        {
            TextureImporter tf = (TextureImporter)AssetImporter.GetAtPath(Pack + f + ".png");
            tf.textureType = TextureImporterType.Sprite;
            tf.spriteImportMode = SpriteImportMode.Single;
            tf.spritePixelsPerUnit = PPU;
            tf.wrapMode = TextureWrapMode.Clamp;
            PixelNitido(tf);
            tf.SaveAndReimport();
        }
        // Props y antorcha: se quedan con sus px por unidad (los usa la cueva
        // antigua); en la nueva se escalan para que el pixel mida lo mismo.
        foreach (string f in new[] { Props1, Props2, Antorcha })
        {
            TextureImporter tp = (TextureImporter)AssetImporter.GetAtPath(f);
            PixelNitido(tp);
            tp.SaveAndReimport();
        }

        piezas = AssetDatabase.LoadAllAssetsAtPath(Tileset).OfType<Sprite>().ToDictionary(s => s.name);
        tiles = new Dictionary<string, Tile>();
        foreach (Sprite s in piezas.Values)
        {
            if (s.name.StartsWith("adorno_")) continue;
            string ruta = $"{CarpetaTiles}/{s.name}.asset";
            Tile t = AssetDatabase.LoadAssetAtPath<Tile>(ruta);
            if (t == null) { t = ScriptableObject.CreateInstance<Tile>(); AssetDatabase.CreateAsset(t, ruta); }
            t.sprite = s;
            t.colliderType = Tile.ColliderType.None;
            EditorUtility.SetDirty(t);
            tiles[s.name] = t;
        }
        // La colision: una casilla entera, sin dibujo.
        string rc = $"{CarpetaTiles}/colision.asset";
        Tile col = AssetDatabase.LoadAssetAtPath<Tile>(rc);
        if (col == null) { col = ScriptableObject.CreateInstance<Tile>(); AssetDatabase.CreateAsset(col, rc); }
        col.sprite = null;
        col.colliderType = Tile.ColliderType.Grid;
        EditorUtility.SetDirty(col);
        tiles["colision"] = col;
        // Sombra de las salas ocultas: como el relleno, pero con el color libre
        // (ZonaOculta la aclara casilla a casilla al entrar).
        string rs = $"{CarpetaTiles}/sombra.asset";
        Tile sombra = AssetDatabase.LoadAssetAtPath<Tile>(rs);
        if (sombra == null) { sombra = ScriptableObject.CreateInstance<Tile>(); AssetDatabase.CreateAsset(sombra, rs); }
        sombra.sprite = piezas["relleno_0"];
        sombra.colliderType = Tile.ColliderType.None;
        sombra.flags = TileFlags.None;
        EditorUtility.SetDirty(sombra);
        tiles["sombra"] = sombra;

        CrearBruma();
        Sonidos_();
        AssetDatabase.SaveAssets();
    }

    private static void PixelNitido(TextureImporter ti)
    {
        ti.filterMode = FilterMode.Point;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
        ti.maxTextureSize = Mathf.Max(ti.maxTextureSize, 2048);
        var st = new TextureImporterSettings();
        ti.ReadTextureSettings(st);
        st.spriteMeshType = SpriteMeshType.FullRect;
        st.spriteExtrude = 0;
        ti.SetTextureSettings(st);
    }

    // Franja de bruma: suave arriba y abajo, en un PNG (asi se guarda en la escena).
    private static void CrearBruma()
    {
        string ruta = CarpetaSprites + "/bruma.png";
        if (!File.Exists(ruta))
        {
            var t = new Texture2D(64, 16, TextureFormat.RGBA32, false);
            for (int y = 0; y < 16; y++)
            for (int x = 0; x < 64; x++)
            {
                float v = Mathf.Sin(y / 15f * Mathf.PI);
                float bx = Mathf.Min(1f, Mathf.Min(x, 63 - x) / 12f);
                float ruido = 0.85f + 0.15f * Mathf.PerlinNoise(x * 0.15f, y * 0.3f);
                t.SetPixel(x, y, new Color(1f, 1f, 1f, v * v * bx * ruido));
            }
            t.Apply();
            File.WriteAllBytes(ruta, t.EncodeToPNG());
            AssetDatabase.ImportAsset(ruta);
        }
        TextureImporter ti = (TextureImporter)AssetImporter.GetAtPath(ruta);
        ti.textureType = TextureImporterType.Sprite;
        ti.spritePixelsPerUnit = 16f;
        ti.filterMode = FilterMode.Bilinear;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.mipmapEnabled = false;
        ti.wrapMode = TextureWrapMode.Clamp;
        ti.SaveAndReimport();
    }

    // Sonidos nuevos (claves de Resources/RecursosRPG) y el ambiente de la cueva.
    private static void Sonidos_()
    {
        RecursosRPG r = Resources.Load<RecursosRPG>("RecursosRPG");
        if (r != null)
        {
            AudioClip A(string f) => AssetDatabase.LoadAssetAtPath<AudioClip>(Sonidos + f);
            void Grupo(string clave, float vol, params AudioClip[] clips)
            {
                clips = clips.Where(c => c != null).ToArray();
                if (clips.Length == 0) { Debug.LogWarning("[CuevaNueva] Sin sonido para " + clave); return; }
                RecursosRPG.GrupoSonido g = r.sonidos.Find(x => x.clave == clave);
                if (g == null) { g = new RecursosRPG.GrupoSonido { clave = clave }; r.sonidos.Add(g); }
                g.clips = clips;
                g.volumen = vol;
                g.variacionTono = 0.08f;
            }
            Grupo("cueva_crujido", 0.55f, A("SFX/Chopping and Mining/mine 1.ogg"), A("SFX/Chopping and Mining/mine 2.ogg"));
            Grupo("cueva_derrumbe", 0.7f, A("SFX/Chopping and Mining/mine 3.ogg"), A("SFX/Chopping and Mining/mine 4.ogg"), A("SFX/Chopping and Mining/mine 5.ogg"));
            Grupo("antorcha_encender", 0.65f, A("SFX/Torch/Light Torch 1.ogg"), A("SFX/Torch/Light Torch 2.ogg"));
            EditorUtility.SetDirty(r);
        }
        ConfigNivel cfg = AssetDatabase.LoadAssetAtPath<ConfigNivel>(ConfigCueva);
        AudioClip cueva = AssetDatabase.LoadAssetAtPath<AudioClip>(Sonidos + "BGS Loops/Cave/Cave.ogg");
        if (cfg != null && cueva != null && cfg.ambiente == null)
        {
            cfg.ambiente = cueva;
            cfg.volumenAmbiente = 0.35f;
            EditorUtility.SetDirty(cfg);
        }
    }

    // ------------------------------------------------------------------ Mapa

    // Casillas del mapa: de x -24 a 191 y de y -20 a 40 (unidades).
    private const int X0 = -42, X1 = 335, Y0 = -35, Y1 = 71;
    private static int W => X1 - X0;
    private static int H => Y1 - Y0;
    // v: se ve roca. c: tiene colision.
    private static bool[,] v, c;
    private static HashSet<Vector2Int> ilusorias, ocultas, falsas;

    private static bool V(int x, int y) => x < X0 || y < Y0 || x >= X1 || y >= Y1 || v[x - X0, y - Y0];

    private static void Abrir(float x0, float y0, float x1, float y1) => Poner(x0, y0, x1, y1, false);
    private static void Cerrar(float x0, float y0, float x1, float y1) => Poner(x0, y0, x1, y1, true);
    private static void Poner(float x0, float y0, float x1, float y1, bool roca)
    {
        for (int x = C(x0); x < C(x1); x++)
        for (int y = C(y0); y < C(y1); y++)
        {
            if (x < X0 || y < Y0 || x >= X1 || y >= Y1) continue;
            v[x - X0, y - Y0] = roca;
            c[x - X0, y - Y0] = roca;
        }
    }
    private static IEnumerable<Vector2Int> Casillas(float x0, float y0, float x1, float y1)
    {
        for (int x = C(x0); x < C(x1); x++) for (int y = C(y0); y < C(y1); y++) yield return new Vector2Int(x, y);
    }

    // Grietas mortales: boca (x0..x1 en el suelo "y") y fondo.
    private static readonly (float x0, float x1, float suelo, float fondo)[] Grietas =
    {
        (U(37), U(41), 0f, -6f),        // 1: entrada (enseñanza)
        (U(140), U(144), 20f, 14f),     // 2: galeria de las grietas
        (U(172), U(176), 20f, 14f),     // 3: borde de la sima (con suelo falso encima)
    };

    private static void Trazar()
    {
        v = new bool[W, H];
        c = new bool[W, H];
        for (int x = 0; x < W; x++) for (int y = 0; y < H; y++) v[x, y] = c[x, y] = true;
        ilusorias = new HashSet<Vector2Int>();
        ocultas = new HashSet<Vector2Int>();
        falsas = new HashSet<Vector2Int>();

        // 1. Entrada humeda (suelo 0, techo 6.86) con techos mas altos a ratos.
        Abrir(U(-1), 0f, U(67), U(12));
        Abrir(U(14), U(12), U(25), U(16));
        Abrir(U(46), U(12), U(59), U(15));
        // Escalon del mago.
        Cerrar(U(53), 0f, U(64), U(2));
        // Secreto 1: pared ilusoria (2 casillas) y sala detras.
        Abrir(U(-12), 0f, U(-1), U(6));
        foreach (Vector2Int k in Casillas(U(-3), 0f, U(-1), U(6))) { ilusorias.Add(k); v[k.x - X0, k.y - Y0] = true; }
        foreach (Vector2Int k in Casillas(U(-12), 0f, U(-3), U(6))) ocultas.Add(k);

        // 2. Pozo de raices: de 0 a 27.43, repisas en zigzag cada 4 casillas.
        Abrir(U(67), 0f, U(91), U(48));
        Cerrar(U(71), 0f, U(76), U(2));      // columna baja
        Cerrar(U(83), 0f, U(91), U(7));      // columna alta (llega al paso del atajo)
        Cerrar(U(67), U(9), U(75), U(11));
        Cerrar(U(83), U(13), U(91), U(15));
        Cerrar(U(67), U(17), U(75), U(19));
        Cerrar(U(83), U(21), U(91), U(23));
        Cerrar(U(67), U(25), U(75), U(27));
        Cerrar(U(83), U(29), U(91), U(31));
        // Paso del atajo (derrumbe) hacia la galeria inferior.
        Abrir(U(91), U(7), U(97), U(13));

        // 3. Galeria alta (suelo 20, techo 27.43), techos mas altos a ratos.
        Abrir(U(91), U(35), U(185), U(48));
        Abrir(U(101), U(48), U(116), U(52));
        Abrir(U(147), U(48), U(161), U(53));
        Abrir(U(175), U(48), U(185), U(50));
        // Suelo falso de enseñanza: pozo hasta la galeria inferior.
        Abrir(U(109), U(18), U(113), U(35));
        foreach (Vector2Int k in Casillas(U(109), U(33), U(113), U(35))) { falsas.Add(k); v[k.x - X0, k.y - Y0] = true; }
        // Grietas 2 y 3 (la 3 con un suelo falso encima).
        Abrir(U(140), U(24), U(144), U(35));
        Abrir(U(172), U(24), U(176), U(35));
        foreach (Vector2Int k in Casillas(U(172), U(33), U(176), U(35))) { falsas.Add(k); v[k.x - X0, k.y - Y0] = true; }

        // 4. Galeria inferior (suelo 4, techo 10.29), a oscuras.
        Abrir(U(97), U(7), U(185), U(18));
        Cerrar(U(147), U(7), U(153), U(8));

        // 5. La sima (de 27.43 a 4), con repisas y el secreto 2.
        Abrir(U(185), U(7), U(203), U(48));
        // Repisas en zigzag cada 5 casillas (doble salto): se puede volver a subir.
        Cerrar(U(191), U(10), U(197), U(12));
        Cerrar(U(197), U(15), U(203), U(17));
        Cerrar(U(185), U(20), U(191), U(22));
        Cerrar(U(193), U(25), U(203), U(27));
        Abrir(U(203), U(27), U(213), U(33));
        foreach (Vector2Int k in Casillas(U(203), U(27), U(205), U(33))) { ilusorias.Add(k); v[k.x - X0, k.y - Y0] = true; }
        foreach (Vector2Int k in Casillas(U(205), U(27), U(213), U(33))) ocultas.Add(k);

        // 6. Antesala (suelo 4, techo 12) y 7. arena (suelo 4, techo 22.29).
        Abrir(U(203), U(7), U(245), U(21));
        Abrir(U(245), U(7), U(308), U(39));

        // Las grietas: hasta el fondo.
        foreach (var g in Grietas) Abrir(g.x0, g.fondo, g.x1, g.suelo);
    }

    // ------------------------------------------------------------------ Escena

    // Limite de la camara (la vista nunca sale de aqui).
    private static readonly Rect LimiteCamara = Rect.MinMaxRect(-7.5f, -6.5f, 176f, 30.5f);

    private static System.Random azar;
    private static Transform raizNueva;

    private static void Montar()
    {
        Scene antigua = EditorSceneManager.OpenScene(RutaAntigua, OpenSceneMode.Single);
        EditorSceneManager.SaveScene(antigua, RutaActiva, true);
        Scene escena = EditorSceneManager.OpenScene(RutaActiva, OpenSceneMode.Single);
        if (piezas == null) Preparar();
        azar = new System.Random(4321);

        Trazar();
        Limpiar(escena);
        raizNueva = new GameObject("CuevaNueva").transform;

        Transform grid = Terreno();
        Fondo();
        Peligros();
        Objetos(escena);
        Oscuridad();
        Camara(escena);
        Decorar();
        Ambiente();

        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
        AsegurarEnBuild();
        Debug.Log("[CuevaNueva] Cueva generada en " + RutaActiva);
    }

    private static GameObject Raiz(Scene e, string n) => e.GetRootGameObjects().FirstOrDefault(g => g.name == n);
    private static Transform Hijo(Transform padre, string n)
    {
        Transform t = padre.Find(n);
        if (t == null) { t = new GameObject(n).transform; t.SetParent(padre, false); }
        return t;
    }

    // Quita lo que se rehace: roca, decoracion, fondo, trampas y la zona de muerte vieja.
    private static void Limpiar(Scene e)
    {
        GameObject deco = Raiz(e, "DecoracionExtra");
        if (deco != null) Object.DestroyImmediate(deco);
        Transform nivel = Raiz(e, "Nivel").transform;
        foreach (string n in new[] { "Decoracion", "Fondo", "Trampas", "Plataformas" })
        {
            Transform t = nivel.Find(n);
            if (t != null) Object.DestroyImmediate(t.gameObject);
        }
        foreach (DeadArea d in Object.FindObjectsByType<DeadArea>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Object.DestroyImmediate(d.gameObject);
    }

    // ------------------------------------------------------------------ Terreno

    private static Tilemap tmRelleno, tmSuelos, tmTechos, tmParedes;

    private static Tilemap Mapa(Transform grid, string n, string capa, int orden, bool colision = false)
    {
        Transform t = Hijo(grid, n);
        Tilemap tm = t.GetComponent<Tilemap>();
        if (tm == null) tm = t.gameObject.AddComponent<Tilemap>();
        tm.ClearAllTiles();
        TilemapRenderer tr = t.GetComponent<TilemapRenderer>();
        if (!colision)
        {
            if (tr == null) tr = t.gameObject.AddComponent<TilemapRenderer>();
            tr.sortingLayerName = capa;
            tr.sortingOrder = orden;
            tr.mode = TilemapRenderer.Mode.Chunk;
        }
        return tm;
    }

    private static Transform Terreno()
    {
        Transform nivel = GameObject.Find("Nivel").transform;
        Transform viejo = nivel.Find("Terreno");
        GameObject g = new GameObject("TerrenoNuevo");
        g.transform.SetParent(nivel, false);
        Grid grid = g.AddComponent<Grid>();
        grid.cellSize = new Vector3(S, S, 0f);

        // La colision: una capa invisible con todas las casillas de roca solida.
        Tilemap col = Mapa(g.transform, "Colision", null, 0, true);
        col.gameObject.layer = LayerMask.NameToLayer("Ground");
        Rigidbody2D rb = col.gameObject.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Static;
        TilemapCollider2D tc = col.gameObject.AddComponent<TilemapCollider2D>();
        tc.compositeOperation = Collider2D.CompositeOperation.Merge;
        CompositeCollider2D cc = col.gameObject.AddComponent<CompositeCollider2D>();
        cc.geometryType = CompositeCollider2D.GeometryType.Polygons;

        tmRelleno = Mapa(g.transform, "Relleno", "Middleground", 0);
        tmParedes = Mapa(g.transform, "Paredes", "Middleground", 1);
        tmTechos = Mapa(g.transform, "Techos", "Middleground", 2);
        tmSuelos = Mapa(g.transform, "Suelos", "Middleground", 3);

        // Paredes falsas y zonas ocultas: se reaprovechan (ya configuradas).
        Tilemap paredesFalsas = null, zonasOcultas = null;
        if (viejo != null)
        {
            Transform pf = viejo.Find("ParedesFalsas"), zo = viejo.Find("ZonasOcultas");
            if (pf != null) { pf.SetParent(g.transform, false); paredesFalsas = pf.GetComponent<Tilemap>(); paredesFalsas.ClearAllTiles(); }
            if (zo != null) { zo.SetParent(g.transform, false); zonasOcultas = zo.GetComponent<Tilemap>(); zonasOcultas.ClearAllTiles(); }
            Object.DestroyImmediate(viejo.gameObject);
        }

        var pos = new List<Vector3Int>();
        var tl = new List<TileBase>();
        var posCol = new List<Vector3Int>();
        // Solo se pinta lo que la camara puede llegar a ver (nunca sale de su
        // limite) y la colision solo junto a los huecos: la escena pesa mucho menos.
        int vx0 = C(LimiteCamara.xMin) - 2, vx1 = C(LimiteCamara.xMax) + 2, vy0 = C(LimiteCamara.yMin) - 2, vy1 = C(LimiteCamara.yMax) + 2;
        bool CercaDelHueco(int x, int y, int r)
        {
            for (int dx = -r; dx <= r; dx++) for (int dy = -r; dy <= r; dy++) if (!V(x + dx, y + dy) || ilusorias.Contains(new Vector2Int(x + dx, y + dy))) return true;
            return false;
        }
        for (int x = Mathf.Max(X0, vx0); x < Mathf.Min(X1, vx1); x++)
        for (int y = Mathf.Max(Y0, vy0); y < Mathf.Min(Y1, vy1); y++)
        {
            Vector2Int k = new Vector2Int(x, y);
            if (c[x - X0, y - Y0] && CercaDelHueco(x, y, 2)) posCol.Add(new Vector3Int(x, y, 0));
            if (!V(x, y) || falsas.Contains(k)) continue;
            // Cerca del hueco, algo de textura; en lo profundo, liso.
            bool cerca = false;
            for (int dx = -2; dx <= 2 && !cerca; dx++) for (int dy = -2; dy <= 2 && !cerca; dy++) if (!V(x + dx, y + dy)) cerca = true;
            string n = cerca && azar.NextDouble() < 0.35 ? "relleno_t" + azar.Next(RellenoTextura.Length) : "relleno_" + (((x % 2) + 2) % 2 + (((y % 2) + 2) % 2) * 2);
            Tile t = tiles[n];
            if (ilusorias.Contains(k) && paredesFalsas != null) { paredesFalsas.SetTile(new Vector3Int(x, y, 0), t); continue; }
            pos.Add(new Vector3Int(x, y, 0));
            tl.Add(t);
        }
        tmRelleno.SetTiles(pos.ToArray(), tl.ToArray());
        col.SetTiles(posCol.ToArray(), Enumerable.Repeat<TileBase>(tiles["colision"], posCol.Count).ToArray());
        if (zonasOcultas != null)
            foreach (Vector2Int k in ocultas)
            {
                Vector3Int p = new Vector3Int(k.x, k.y, 0);
                zonasOcultas.SetTile(p, tiles["sombra"]);
                // Sin esto no se le puede cambiar el color (la zona no se aclararia).
                zonasOcultas.SetTileFlags(p, TileFlags.None);
            }

        Bordes();
        return g.transform;
    }

    // Suelos, techos y paredes en cada lado de roca que da al hueco. Las piezas
    // de 32 px ocupan dos casillas del tramo.
    private static void Bordes()
    {
        for (int y = Y0; y < Y1; y++)
        {
            // Suelos y techos: tramos horizontales.
            TramoHorizontal(y, 1, tmSuelos, "suelo_", Suelos);
            TramoHorizontal(y, -1, tmTechos, "techo_", Techos);
        }
        for (int x = X0; x < X1; x++)
        {
            TramoVertical(x, 1, tmParedes, "paredD_", ParedDerecha);
            TramoVertical(x, -1, tmParedes, "paredI_", ParedIzquierda);
        }
    }

    private static bool Pintable(int x, int y) => V(x, y) && !falsas.Contains(new Vector2Int(x, y)) && !ilusorias.Contains(new Vector2Int(x, y));

    private static void TramoHorizontal(int y, int lado, Tilemap tm, string prefijo, RectInt[] rects)
    {
        int x = X0;
        while (x < X1)
        {
            if (!(Pintable(x, y) && !V(x, y + lado))) { x++; continue; }
            int inicio = x;
            while (x < X1 && Pintable(x, y) && !V(x, y + lado)) x++;
            Rellenar(tm, prefijo, rects, inicio, x, y, true);
        }
    }

    private static void TramoVertical(int x, int lado, Tilemap tm, string prefijo, RectInt[] rects)
    {
        int y = Y1 - 1;
        while (y >= Y0)
        {
            if (!(Pintable(x, y) && !V(x + lado, y))) { y--; continue; }
            int inicio = y;
            while (y >= Y0 && Pintable(x, y) && !V(x + lado, y)) y--;
            // De arriba a abajo: las piezas de 32 de alto cubren la casilla y la de debajo.
            int k = inicio;
            while (k > y)
            {
                bool doble = k - 1 > y;
                var elegibles = Enumerable.Range(0, rects.Length).Where(i => (rects[i].height >= 32) == doble && rects[i].width == 16).ToArray();
                if (elegibles.Length == 0) elegibles = Enumerable.Range(0, rects.Length).Where(i => rects[i].width == 16).ToArray();
                int e = elegibles[azar.Next(elegibles.Length)];
                tm.SetTile(new Vector3Int(x, k, 0), tiles[prefijo + e]);
                k -= rects[e].height >= 32 ? 2 : 1;
            }
            // Algunas de 32 de ancho para que la pared no sea una tira recta.
            if (azar.NextDouble() < 0.5 && inicio - y >= 2)
            {
                int[] anchas = Enumerable.Range(0, rects.Length).Where(i => rects[i].width == 32).ToArray();
                if (anchas.Length > 0 && V(x - lado, inicio) && V(x - lado, inicio - 1))
                {
                    int e = anchas[azar.Next(anchas.Length)];
                    tm.SetTile(new Vector3Int(x, inicio, 0), tiles[prefijo + e]);
                }
            }
        }
    }

    private static void Rellenar(Tilemap tm, string prefijo, RectInt[] rects, int desde, int hasta, int y, bool horizontal)
    {
        int x = desde;
        while (x < hasta)
        {
            bool cabe32 = hasta - x >= 2;
            var elegibles = Enumerable.Range(0, rects.Length).Where(i => rects[i].width == 16 || cabe32).ToArray();
            int e = elegibles[azar.Next(elegibles.Length)];
            tm.SetTile(new Vector3Int(x, y, 0), tiles[prefijo + e]);
            x += rects[e].width / 16;
        }
    }

    // ------------------------------------------------------------------ Fondo

    private static readonly string[] Fondos = { "background1", "background2", "background3", "background4a" };
    private static readonly Color[] TinteFondos =
    {
        new Color(0.55f, 0.62f, 0.7f), new Color(0.5f, 0.56f, 0.64f), new Color(0.62f, 0.64f, 0.7f), new Color(0.78f, 0.74f, 0.74f),
    };
    private static readonly float[] SeguimientoFondos = { 0.96f, 0.88f, 0.76f, 0.58f };
    private static readonly List<SpriteRenderer> copiasFondo = new List<SpriteRenderer>();
    private static readonly List<Color> tintesCopias = new List<Color>();

    private static void Fondo()
    {
        copiasFondo.Clear();
        tintesCopias.Clear();
        GameObject go = new GameObject("Fondo");
        go.transform.SetParent(raizNueva, false);
        ParallaxCueva parallax = go.AddComponent<ParallaxCueva>();
        parallax.seguimientoVertical = 0.9f;
        var capas = new List<ParallaxCueva.Capa>();
        for (int i = 0; i < Fondos.Length; i++)
        {
            Sprite s = AssetDatabase.LoadAllAssetsAtPath(Pack + Fondos[i] + ".png").OfType<Sprite>().FirstOrDefault();
            if (s == null) { Debug.LogWarning("[CuevaNueva] Falta " + Fondos[i]); continue; }
            Transform raiz = new GameObject("Capa_" + Fondos[i]).transform;
            raiz.SetParent(go.transform, false);
            float ancho = s.bounds.size.x;
            for (int k = -1; k <= 1; k++)
            {
                SpriteRenderer sr = new GameObject("Copia").AddComponent<SpriteRenderer>();
                sr.transform.SetParent(raiz, false);
                sr.transform.localPosition = new Vector3(k * ancho, 0f, 0f);
                sr.sprite = s;
                sr.sortingLayerName = "Background";
                sr.sortingOrder = i;
                sr.color = TinteFondos[i];
                copiasFondo.Add(sr);
                tintesCopias.Add(TinteFondos[i]);
            }
            capas.Add(new ParallaxCueva.Capa { raiz = raiz, ancho = ancho, seguimiento = SeguimientoFondos[i], rellenarArriba = true, rellenarAbajo = true });
        }
        parallax.capas = capas.ToArray();
    }

    // ------------------------------------------------------------------ Peligros

    private static void Peligros()
    {
        Transform padre = Hijo(raizNueva, "Peligros");
        int suelo = LayerMask.NameToLayer("Ground");

        // Grietas: muerte en el fondo, niebla y borde.
        for (int i = 0; i < Grietas.Length; i++)
        {
            var g = Grietas[i];
            GameObject go = new GameObject("Grieta_" + (i + 1));
            go.transform.SetParent(padre, false);
            go.transform.position = new Vector3((g.x0 + g.x1) * 0.5f, g.suelo, 0f);
            GrietaMortal gm = go.AddComponent<GrietaMortal>();
            gm.ancho = g.x1 - g.x0;
            gm.fondo = g.suelo - g.fondo;
            GameObject muerte = new GameObject("Muerte");
            muerte.layer = LayerMask.NameToLayer("Items");
            muerte.transform.SetParent(go.transform, false);
            muerte.transform.position = new Vector3((g.x0 + g.x1) * 0.5f, g.fondo + 0.7f, 0f);
            BoxCollider2D b = muerte.AddComponent<BoxCollider2D>();
            b.isTrigger = true;
            b.size = new Vector2(g.x1 - g.x0, 1.4f);
            muerte.AddComponent<DeadArea>();
        }
        // Por si algo se escapa del mapa.
        GameObject fin = new GameObject("MuerteFueraDelMapa");
        fin.layer = LayerMask.NameToLayer("Items");
        fin.transform.SetParent(padre, false);
        fin.transform.position = new Vector3(85f, -18f, 0f);
        BoxCollider2D bf = fin.AddComponent<BoxCollider2D>();
        bf.isTrigger = true;
        bf.size = new Vector2(240f, 2f);
        fin.AddComponent<DeadArea>();

        // Suelos falsos: cada grupo de casillas falsas pegadas.
        var grupos = new List<List<Vector2Int>>();
        var vistas = new HashSet<Vector2Int>();
        foreach (Vector2Int k in falsas.OrderBy(p => p.x).ThenBy(p => p.y))
        {
            if (vistas.Contains(k)) continue;
            var grupo = new List<Vector2Int>();
            var cola = new Queue<Vector2Int>();
            cola.Enqueue(k); vistas.Add(k);
            while (cola.Count > 0)
            {
                Vector2Int a = cola.Dequeue();
                grupo.Add(a);
                foreach (Vector2Int d in new[] { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down })
                    if (falsas.Contains(a + d) && vistas.Add(a + d)) cola.Enqueue(a + d);
            }
            grupos.Add(grupo);
        }
        for (int i = 0; i < grupos.Count; i++)
        {
            var gr = grupos[i];
            int xa = gr.Min(p => p.x), xb = gr.Max(p => p.x) + 1, ya = gr.Min(p => p.y), yb = gr.Max(p => p.y) + 1;
            bool pista = xa >= 160; // el de la sima lleva pista; el de enseñanza no
            GameObject go = new GameObject(pista ? "SueloFalso_ConPista" : "SueloFalso_Ensenanza");
            go.layer = suelo;
            go.transform.SetParent(padre, false);
            go.transform.position = new Vector3(U(xa) + U(xb - xa) * 0.5f, U(ya) + U(yb - ya) * 0.5f, 0f);
            BoxCollider2D b = go.AddComponent<BoxCollider2D>();
            b.size = new Vector2(U(xb - xa), U(yb - ya));
            Transform vis = new GameObject("Visual").transform;
            vis.SetParent(go.transform, false);
            for (int x = xa; x < xb; x++)
            for (int y = ya; y < yb; y++)
                Pieza(vis, piezas["relleno_" + azar.Next(Relleno.Length)], new Vector2(U(x) + S * 0.5f, U(y) + S * 0.5f), "Middleground", 4);
            for (int x = xa; x < xb;)
            {
                int e = azar.Next(Suelos.Length);
                if (Suelos[e].width == 32 && x + 1 >= xb) continue;
                Sprite s = piezas["suelo_" + e];
                Pieza(vis, s, new Vector2(U(x) + S * 0.5f, U(yb - 1) + S * 0.5f), "Middleground", 6);
                x += Suelos[e].width / 16;
            }
            SueloFalso sf = go.AddComponent<SueloFalso>();
            sf.visual = vis;
            sf.conPista = pista;
            if (pista)
            {
                // Grietas finas pintadas encima (la pista).
                SpriteRenderer gri = Pieza(vis, piezas["relleno_t2"], new Vector2(go.transform.position.x, U(yb) - S * 0.5f), "Middleground", 7);
                gri.color = new Color(0.9f, 0.85f, 0.8f, 0.6f);
                sf.grietas = gri;
            }
        }

        // Atajo: derrumbe entre el pozo y la galeria inferior. Se abre desde la galeria.
        GameObject der = new GameObject("DerrumbeAtajo");
        der.layer = suelo;
        der.transform.SetParent(padre, false);
        der.transform.position = new Vector3(U(93), U(7) + U(6) * 0.5f, 0f);
        BoxCollider2D bd = der.AddComponent<BoxCollider2D>();
        bd.size = new Vector2(U(2), U(6));
        Transform dv = new GameObject("Visual").transform;
        dv.SetParent(der.transform, false);
        Sprite[] rocas = Props(Props1, 20, 29);
        for (int k = 0; k < 4; k++)
        {
            Sprite s = rocas[(k * 3) % rocas.Length];
            SpriteRenderer sr = Prop(dv, s, new Vector2(U(93) + (k % 2 == 0 ? -0.15f : 0.2f), U(7) - 0.1f + k * 0.8f), "Middleground", 8 + k, 0.55f);
            sr.color = new Color(0.5f, 0.54f, 0.6f);
        }
        DerrumbeAtajo da = der.AddComponent<DerrumbeAtajo>();
        da.clave = "pozo";
        da.ladoQueAbre = 1;
        da.visual = dv;
    }

    // ------------------------------------------------------------------ Objetos

    private static readonly (string tipo, Vector2 pos)[] Enemigos =
    {
        ("Slime", new Vector2(14f, 0f)), ("Mago", new Vector2(33.5f, U(2))), ("Cacodemonio", new Vector2(45f, 16f)),
        ("Mago", new Vector2(70f, 20f)), ("Cacodemonio", new Vector2(73f, 25f)), ("Slime", new Vector2(88f, 20f)),
        ("Mimic", new Vector2(75f, 4f)), ("Cacodemonio", new Vector2(111f, 13f)), ("Slime", new Vector2(133f, 4f)),
    };

    private static void Objetos(Scene e)
    {
        Transform nivel = Raiz(e, "Nivel").transform;

        // Enemigos: los mismos (con sus ajustes de escena), recolocados.
        Transform ene = nivel.Find("Enemigos");
        var libres = ene.Cast<Transform>().ToList();
        foreach (var d in Enemigos)
        {
            Transform t = libres.FirstOrDefault(x => x.name == d.tipo);
            if (t == null) { Debug.LogWarning("[CuevaNueva] Falta un " + d.tipo); continue; }
            libres.Remove(t);
            t.position = new Vector3(d.pos.x, d.pos.y + (d.tipo == "Cacodemonio" ? 0f : 0.02f), 0f);
        }
        // Elite: Aldren, Hueso Roto, al fondo de la galeria inferior.
        GameObject elite = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/Elites/EliteAldren.prefab");
        if (elite != null)
        {
            GameObject a = (GameObject)PrefabUtility.InstantiatePrefab(elite, ene);
            a.transform.position = new Vector3(100f, 4.02f, 0f);
        }

        // Cofre de mejora: en el mismo sitio (ahora en la galeria inferior).
        Transform cofre = nivel.Find("CofreMejora_mejora_1");
        if (cofre != null) cofre.position = new Vector3(79.5f, 4f, 0f);
        Transform marca = nivel.Find("Marcadores/Recompensa_CuevaSecreta");
        if (marca != null) marca.position = new Vector3(74.5f, 4f, 0f);

        // Cofres de almas de los secretos (solo almas).
        CofreDeAlmas("cueva_secreto_1", new Vector2(-4.6f, 0f), 350);
        CofreDeAlmas("cueva_secreto_2", new Vector2(119.8f, U(27)), 500);

        // Estatua nueva (pista de los suelos falsos) copiando la de siempre.
        GameObject estatuas = Raiz(e, "Estatuas");
        EstatuaPista original = estatuas != null ? estatuas.GetComponentInChildren<EstatuaPista>() : null;
        if (original != null && estatuas.transform.Find("Estatua_Grietas") == null)
        {
            GameObject nueva = Object.Instantiate(original.gameObject, estatuas.transform);
            nueva.name = "Estatua_Grietas";
            float dy = original.transform.position.y - 4f;
            nueva.transform.position = new Vector3(94.3f, 20f + dy, 0f);
            EstatuaPista ep = nueva.GetComponent<EstatuaPista>();
            ep.id = "estatua_cueva_2";
            ep.titulo = "Marcas de uñas";
            ep.texto = "Alguien arañó la piedra antes de caer: «No confíes en el suelo que cruje ni en la roca que se agrieta. " +
                       "Y donde la pared suena hueca... a veces el camino sigue».";
        }

        // Antorchas apagadas (F): dos en la galeria inferior y una en la sima.
        AntorchaApagada("inferior_1", new Vector2(66.5f, 4f));
        AntorchaApagada("inferior_2", new Vector2(95.5f, 4f));
        AntorchaApagada("sima", new Vector2(U(188), U(22)));
        // Antorchas siempre encendidas (decoracion) en la entrada y la antesala.
        AntorchaDecorativa(new Vector2(6f, 0f));
        AntorchaDecorativa(new Vector2(30f, 0f));
        AntorchaDecorativa(new Vector2(121f, 4f));
        AntorchaDecorativa(new Vector2(137.5f, 4f));

        // La arena: un techo invisible exactamente donde estaba (y = 22).
        Transform arena = nivel.Find("ArenaJefe");
        GameObject techo = new GameObject("TechoArena");
        techo.layer = LayerMask.NameToLayer("Ground");
        techo.transform.SetParent(arena != null ? arena : raizNueva, false);
        techo.transform.position = new Vector3(158f, 22f + 0.25f, 0f);
        techo.AddComponent<BoxCollider2D>().size = new Vector2(36f, 0.5f);

        // Puntos para la depuracion (teletransporte a cada zona).
        DepuracionCueva dep = raizNueva.gameObject.AddComponent<DepuracionCueva>();
        dep.puntos = new[]
        {
            new DepuracionCueva.Punto { nombre = "1. Entrada", pos = new Vector2(3f, 0.7f) },
            new DepuracionCueva.Punto { nombre = "Secreto 1", pos = new Vector2(-4f, 0.7f) },
            new DepuracionCueva.Punto { nombre = "2. Pozo", pos = new Vector2(45f, 0.7f) },
            new DepuracionCueva.Punto { nombre = "Hoguera 1", pos = new Vector2(56.5f, 20.7f) },
            new DepuracionCueva.Punto { nombre = "3A. Columnas", pos = new Vector2(60f, 20.7f) },
            new DepuracionCueva.Punto { nombre = "3B. Grietas", pos = new Vector2(77f, 20.7f) },
            new DepuracionCueva.Punto { nombre = "3C. Borde de la sima", pos = new Vector2(92f, 20.7f) },
            new DepuracionCueva.Punto { nombre = "4. Galeria inferior", pos = new Vector2(70f, 4.7f) },
            new DepuracionCueva.Punto { nombre = "Atajo (lado galeria)", pos = new Vector2(56f, 4.7f) },
            new DepuracionCueva.Punto { nombre = "5. Sima", pos = new Vector2(U(198), U(27) + 0.7f) },
            new DepuracionCueva.Punto { nombre = "Secreto 2", pos = new Vector2(119f, U(27) + 0.7f) },
            new DepuracionCueva.Punto { nombre = "Hoguera 2 (antesala)", pos = new Vector2(125f, 4.7f) },
            new DepuracionCueva.Punto { nombre = "7. Arena", pos = new Vector2(142f, 4.7f) },
        };
    }

    private static void CofreDeAlmas(string clave, Vector2 pos, int almas)
    {
        Transform padre = Hijo(raizNueva, "Secretos");
        GameObject go = new GameObject("CofreAlmas_" + clave);
        go.transform.SetParent(padre, false);
        go.transform.position = pos;
        Sprite cerrado = AssetDatabase.LoadAllAssetsAtPath("Assets/SPRITES PARA NUEVOS NIVELES/ENEMIGOS/Enemigo_MonsterPack2/Mimic/Idle_closed.png").OfType<Sprite>().FirstOrDefault();
        Sprite abierto = AssetDatabase.LoadAllAssetsAtPath("Assets/SPRITES PARA NUEVOS NIVELES/ENEMIGOS/Enemigo_MonsterPack2/Mimic/idle_open.png").OfType<Sprite>().FirstOrDefault();
        SpriteRenderer vis = new GameObject("Visual").AddComponent<SpriteRenderer>();
        vis.transform.SetParent(go.transform, false);
        vis.sprite = cerrado;
        vis.sortingLayerName = "Items";
        if (cerrado != null)
        {
            float esc = cerrado.pixelsPerUnit / PPU;
            vis.transform.localScale = new Vector3(esc, esc, 1f);
            // Apoyado en el suelo: la primera fila con dibujo (no el borde del
            // cuadro, que tiene hueco transparente debajo) en "pos".
            vis.transform.position = new Vector3(pos.x, pos.y, 0f);
            float hueco = FilaVisibleAbajo(cerrado) / cerrado.pixelsPerUnit * esc;
            vis.transform.position += Vector3.up * (pos.y - vis.bounds.min.y - hueco);
        }
        go.AddComponent<BoxCollider2D>().isTrigger = true;
        CofreAlmas ca = go.AddComponent<CofreAlmas>();
        var so = new SerializedObject(ca);
        so.FindProperty("clave").stringValue = clave;
        so.FindProperty("almas").intValue = almas;
        so.FindProperty("darFrasco").boolValue = false;
        so.FindProperty("visual").objectReferenceValue = vis;
        so.FindProperty("abierto").objectReferenceValue = abierto;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Filas transparentes por debajo del dibujo dentro del rectangulo del sprite.
    private static int FilaVisibleAbajo(Sprite s)
    {
        string ruta = AssetDatabase.GetAssetPath(s.texture);
        var t = new Texture2D(2, 2);
        t.LoadImage(File.ReadAllBytes(ruta));
        Rect r = s.rect;
        float fx = (float)t.width / s.texture.width, fy = (float)t.height / s.texture.height;
        for (int y = 0; y < (int)r.height; y++)
            for (int x = 0; x < (int)r.width; x++)
                if (t.GetPixel(Mathf.RoundToInt((r.x + x) * fx), Mathf.RoundToInt((r.y + y) * fy)).a > 0.1f)
                {
                    Object.DestroyImmediate(t);
                    return y;
                }
        Object.DestroyImmediate(t);
        return 0;
    }

    private static Sprite[] SpritesAntorcha(int fila)
    {
        // Fila 0: apagadas; 1-3: encendidas (6 fotogramas). Columna por posicion.
        Sprite[] todas = AssetDatabase.LoadAllAssetsAtPath(Antorcha).OfType<Sprite>().ToArray();
        return todas.Where(s => Mathf.FloorToInt((128f - s.rect.yMax) / 32f) == fila).OrderBy(s => s.rect.x).ToArray();
    }

    private static void AntorchaApagada(string clave, Vector2 pos)
    {
        Transform padre = Hijo(raizNueva, "Antorchas");
        GameObject go = new GameObject("Antorcha_" + clave);
        go.transform.SetParent(padre, false);
        go.transform.position = pos;
        Sprite[] apagadas = SpritesAntorcha(0), fuego = SpritesAntorcha(1);
        SpriteRenderer sr = new GameObject("Visual").AddComponent<SpriteRenderer>();
        sr.transform.SetParent(go.transform, false);
        sr.sortingLayerName = "Items";
        sr.sortingOrder = 1000;
        sr.sprite = apagadas.Length > 0 ? apagadas[0] : null;
        if (sr.sprite != null)
        {
            float esc = sr.sprite.pixelsPerUnit / PPU;
            sr.transform.localScale = new Vector3(esc, esc, 1f);
            sr.transform.position = new Vector3(pos.x, pos.y - (sr.bounds.min.y - sr.transform.position.y), 0f);
        }
        FuenteLuz luz = go.AddComponent<FuenteLuz>();
        luz.radio = 4.2f;
        luz.desplazamiento = new Vector2(0f, 1f);
        luz.encendida = false;
        AntorchaCueva a = go.AddComponent<AntorchaCueva>();
        a.clave = clave;
        a.sprite = sr;
        a.apagada = sr.sprite;
        a.encendida = fuego;
    }

    private static void AntorchaDecorativa(Vector2 pos)
    {
        Transform padre = Hijo(raizNueva, "Antorchas");
        Sprite[] fuego = SpritesAntorcha(2);
        if (fuego.Length == 0) return;
        SpriteRenderer sr = new GameObject("AntorchaEncendida").AddComponent<SpriteRenderer>();
        sr.transform.SetParent(padre, false);
        sr.sprite = fuego[0];
        sr.sortingLayerName = "Middleground";
        sr.sortingOrder = 20;
        float esc = fuego[0].pixelsPerUnit / PPU;
        sr.transform.localScale = new Vector3(esc, esc, 1f);
        sr.transform.position = new Vector3(pos.x, pos.y - (sr.bounds.min.y - sr.transform.position.y), 0f);
        AnimacionSprites an = sr.gameObject.AddComponent<AnimacionSprites>();
        an.fotogramas = fuego;
        an.fps = 10f;
        an.inicioAleatorio = true;
        FuenteLuz luz = sr.gameObject.AddComponent<FuenteLuz>();
        luz.radio = 3.5f;
    }

    // ------------------------------------------------------------------ Oscuridad

    private static void Oscuridad()
    {
        GameObject go = new GameObject("Oscuridad");
        go.transform.SetParent(raizNueva, false);
        OscuridadCueva o = go.AddComponent<OscuridadCueva>();
        o.shader = Shader.Find("Warrior/OscuridadCueva");

        Transform zonas = Hijo(raizNueva, "ZonasOscuras");
        ZonaOscuraCaja(zonas, "Oscura_GaleriaInferior", Rect.MinMaxRect(U(97), U(7), U(185), U(18)), 3.1f, 0f);
        ZonaOscuraCaja(zonas, "Oscura_Sima", Rect.MinMaxRect(U(185), U(7), U(203), U(48)), 0f, 0f);

        // Brasas guia por el camino bueno de cada zona oscura.
        GuiaBrasas g1 = new GameObject("Brasas_GaleriaInferior").AddComponent<GuiaBrasas>();
        g1.transform.SetParent(zonas, false);
        g1.camino = new[] { new Vector2(58f, 5.2f), new Vector2(75f, 5.2f), new Vector2(90f, 5.6f), new Vector2(104f, 5.2f), new Vector2(112f, 5.2f), new Vector2(118f, 5.2f) };
        GuiaBrasas g2 = new GameObject("Brasas_Sima").AddComponent<GuiaBrasas>();
        g2.transform.SetParent(zonas, false);
        g2.camino = new[] { new Vector2(U(182), U(36)), new Vector2(U(197), U(28)), new Vector2(U(188), U(23)), new Vector2(U(200), U(18)), new Vector2(U(194), U(13)), new Vector2(U(205), U(8)) };
    }

    private static void ZonaOscuraCaja(Transform padre, string n, Rect r, float zoom, float dy)
    {
        GameObject go = new GameObject(n);
        go.layer = LayerMask.NameToLayer("Items");
        go.transform.SetParent(padre, false);
        go.transform.position = r.center;
        BoxCollider2D b = go.AddComponent<BoxCollider2D>();
        b.isTrigger = true;
        b.size = r.size;
        go.AddComponent<ZonaOscura>();
        if (zoom > 0f) ZonaDeCamara(go, zoom, dy);
    }

    private static void ZonaDeCamara(GameObject go, float tamano, float dy)
    {
        ZonaCamara z = go.AddComponent<ZonaCamara>();
        var so = new SerializedObject(z);
        so.FindProperty("tamano").floatValue = tamano;
        so.FindProperty("desplazamientoY").floatValue = dy;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ------------------------------------------------------------------ Camara

    private static void Camara(Scene e)
    {
        Transform nivel = Raiz(e, "Nivel").transform;
        Transform zc = nivel.Find("ZonasCamara");
        void Caja(string n, Rect r, float tamano, float dy)
        {
            Transform t = zc.Find(n);
            GameObject go = t != null ? t.gameObject : new GameObject(n);
            if (t == null) go.layer = LayerMask.NameToLayer("Items");
            go.transform.SetParent(zc, false);
            go.transform.position = r.center;
            BoxCollider2D b = go.GetComponent<BoxCollider2D>();
            if (b == null) b = go.AddComponent<BoxCollider2D>();
            b.isTrigger = true;
            b.size = r.size;
            ZonaCamara z = go.GetComponent<ZonaCamara>();
            if (z == null) ZonaDeCamara(go, tamano, dy);
            else
            {
                var so = new SerializedObject(z);
                so.FindProperty("tamano").floatValue = tamano;
                so.FindProperty("desplazamientoY").floatValue = dy;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
        Caja("Zona_Pozo", Rect.MinMaxRect(U(67), 0f, U(91), U(48)), 4.1f, 0.6f);
        Transform caida = zc.Find("Zona_Caida");
        if (caida != null) caida.name = "Zona_Sima";
        Caja("Zona_Sima", Rect.MinMaxRect(U(185), U(7), U(203), U(48)), 4.1f, -0.8f);
        // Eco del abismo: al acercarse a una grieta la camara baja un poco para ensenarla.
        for (int i = 0; i < Grietas.Length; i++)
        {
            var g = Grietas[i];
            Caja("Eco_Grieta_" + (i + 1), Rect.MinMaxRect(g.x0 - 3.2f, g.suelo, g.x1 + 3.2f, g.suelo + 5f), 3.6f, -1.3f);
        }

        // Limite de la camara: todo el mapa jugable.
        GameObject lim = Raiz(e, "CameraLimit");
        PolygonCollider2D poly = lim != null ? lim.GetComponent<PolygonCollider2D>() : null;
        if (poly != null)
        {
            lim.transform.position = Vector3.zero;
            lim.transform.localScale = Vector3.one;
            poly.offset = Vector2.zero;
            poly.pathCount = 1;
            poly.SetPath(0, new[] { new Vector2(LimiteCamara.xMin, LimiteCamara.yMin), new Vector2(LimiteCamara.xMax, LimiteCamara.yMin),
                                   new Vector2(LimiteCamara.xMax, LimiteCamara.yMax), new Vector2(LimiteCamara.xMin, LimiteCamara.yMax) });
        }
    }

    // ------------------------------------------------------------------ Decoracion

    private static Sprite[] Props(string ruta, int desde, int hasta)
    {
        Sprite[] todas = AssetDatabase.LoadAllAssetsAtPath(ruta).OfType<Sprite>().ToArray();
        return Enumerable.Range(desde, hasta - desde + 1).Select(i => todas.FirstOrDefault(s => s.name == Path.GetFileNameWithoutExtension(ruta) + "_" + i)).Where(s => s != null).ToArray();
    }

    private static SpriteRenderer Pieza(Transform padre, Sprite s, Vector2 pos, string capa, int orden)
    {
        SpriteRenderer sr = new GameObject(s.name).AddComponent<SpriteRenderer>();
        sr.transform.SetParent(padre, false);
        sr.transform.position = pos;
        sr.sprite = s;
        sr.sortingLayerName = capa;
        sr.sortingOrder = orden;
        return sr;
    }

    // Prop de pack a 100 px por unidad, escalado para que el pixel mida como el
    // del player, apoyado por abajo en "pos" (o colgado por arriba si "colgado").
    private static SpriteRenderer Prop(Transform padre, Sprite s, Vector2 pos, string capa, int orden, float oscuro = 1f, bool colgado = false, bool espejo = false)
    {
        SpriteRenderer sr = new GameObject(s.name).AddComponent<SpriteRenderer>();
        sr.transform.SetParent(padre, false);
        sr.sprite = s;
        float esc = s.pixelsPerUnit / PPU;
        sr.transform.localScale = new Vector3(espejo ? -esc : esc, esc, 1f);
        sr.sortingLayerName = capa;
        sr.sortingOrder = orden;
        sr.color = new Color(oscuro, oscuro, oscuro, 1f);
        sr.transform.position = Vector3.zero;
        Bounds b = sr.bounds;
        float y = colgado ? pos.y - b.max.y : pos.y - b.min.y;
        // Pixel a pixel: la posicion en la rejilla de 1/28.
        sr.transform.position = new Vector3(Mathf.Round((pos.x - b.center.x) * PPU) / PPU, Mathf.Round(y * PPU) / PPU, 0f);
        return sr;
    }

    // Zonas: rectangulos del mapa con su tinte (fondo, decoracion) y bruma.
    private static readonly (string nombre, Rect r, Color tinte, Color bruma, float densidad)[] Zonas =
    {
        ("Entrada humeda", Rect.MinMaxRect(-10f, -8f, 38.3f, 12f), new Color(0.88f, 1f, 0.95f), new Color(0.55f, 0.75f, 0.7f), 0.22f),
        ("Pozo de raices", Rect.MinMaxRect(38.3f, -8f, 52f, 30f), new Color(0.9f, 0.95f, 0.92f), new Color(0.55f, 0.7f, 0.65f), 0.15f),
        ("Galeria de columnas", Rect.MinMaxRect(52f, 12f, 76f, 32f), new Color(1f, 0.97f, 0.92f), new Color(0.7f, 0.68f, 0.62f), 0.12f),
        ("Galeria de las grietas", Rect.MinMaxRect(76f, 12f, 106f, 32f), new Color(0.85f, 0.9f, 1.05f), new Color(0.6f, 0.7f, 0.85f), 0.2f),
        ("Galeria inferior", Rect.MinMaxRect(52f, -8f, 106f, 12f), new Color(0.72f, 0.66f, 0.88f), new Color(0.5f, 0.45f, 0.7f), 0.18f),
        ("La sima", Rect.MinMaxRect(106f, -8f, 116f, 32f), new Color(0.6f, 0.66f, 0.88f), new Color(0.45f, 0.5f, 0.75f), 0.25f),
        ("Antesala", Rect.MinMaxRect(116f, -8f, 140f, 32f), new Color(1.05f, 0.82f, 0.8f), new Color(0.7f, 0.45f, 0.45f), 0.18f),
        ("Arena", Rect.MinMaxRect(140f, -8f, 190f, 40f), new Color(1.08f, 0.76f, 0.74f), new Color(0.75f, 0.35f, 0.35f), 0.15f),
    };

    private static Color TinteEn(Vector2 p)
    {
        foreach (var z in Zonas) if (z.r.Contains(p)) return z.tinte;
        return Color.white;
    }

    private static readonly List<SpriteRenderer> brumas = new List<SpriteRenderer>();

    private static void Decorar()
    {
        Transform padre = Hijo(raizNueva, "Decoracion");
        Transform frente = Hijo(raizNueva, "PrimerPlano");
        Sprite[] estalagmitas = Props(Props1, 1, 9).Concat(Props(Props1, 20, 28)).Where(s => s.rect.height <= 95).ToArray();
        Sprite[] estalactitas = Props(Props1, 10, 19).Concat(Props(Props1, 30, 38)).Where(s => s.rect.height <= 95).ToArray();
        Sprite[] rocasGrandes = Props(Props2, 8, 11);
        Sprite[] arbustos = Props(Props2, 2, 3);
        Sprite bruma = AssetDatabase.LoadAssetAtPath<Sprite>(CarpetaSprites + "/bruma.png");
        brumas.Clear();

        // Sitios a dejar libres (puertas, hogueras, cofres, estatuas, antorchas).
        float[] libres = { 2f, 55f, 127f, 134f, 79.5f, 94.3f, 66.5f, 95.5f, 140f, -4.6f, 6f, 30f, 121f, 137.5f };
        bool Libre(float x) => libres.All(l => Mathf.Abs(l - x) > 1.6f);

        // Recorre los suelos y techos visibles (de las zonas jugables).
        for (int y = Y0 + 1; y < Y1 - 1; y++)
        for (int x = X0 + 1; x < X1 - 1; x++)
        {
            bool suelo = Pintable(x, y) && !V(x, y + 1) && !V(x, y + 2);
            bool techo = Pintable(x, y) && !V(x, y - 1) && !V(x, y - 2);
            float wx = U(x) + S * 0.5f;
            Vector2 p = new Vector2(wx, suelo ? U(y + 1) : U(y));
            if (p.x < -8f || p.x > 176f) continue;
            Color tinte = TinteEn(p);
            if (suelo && Libre(wx) && azar.NextDouble() < 0.07)
            {
                Sprite s = estalagmitas[azar.Next(estalagmitas.Length)];
                // Detras del player, un poco hundida para que el borde del suelo tape la base.
                SpriteRenderer sr = Prop(padre, s, p + Vector2.down * 0.25f, "Middleground", -10, 0.62f, false, azar.NextDouble() < 0.5);
                sr.color *= tinte;
                if (sr.bounds.size.y > 3.2f) Object.DestroyImmediate(sr.gameObject);
            }
            if (techo && azar.NextDouble() < 0.08)
            {
                Sprite s = estalactitas[azar.Next(estalactitas.Length)];
                SpriteRenderer sr = Prop(padre, s, p + Vector2.up * 0.2f, "Middleground", -10, 0.55f, true, azar.NextDouble() < 0.5);
                sr.color *= tinte;
                if (sr.bounds.size.y > 2.6f) Object.DestroyImmediate(sr.gameObject);
            }
        }

        // Rocas grandes al fondo (siluetas oscuras) en los sitios altos.
        foreach (Vector2 p in new[] { new Vector2(44f, 0f), new Vector2(112f, 4f), new Vector2(150f, 4f), new Vector2(168f, 4f), new Vector2(24f, 0f) })
        {
            SpriteRenderer sr = Prop(padre, rocasGrandes[azar.Next(rocasGrandes.Length)], p + Vector2.down * 0.3f, "Background", 20, 0.32f);
            sr.color *= TinteEn(p);
        }
        // Entrada humeda: algo de vegetacion junto a la boca.
        foreach (float x in new[] { 9f, 17.5f })
            Prop(padre, arbustos[azar.Next(arbustos.Length)], new Vector2(x, -0.2f), "Middleground", -8, 0.5f).color *= TinteEn(new Vector2(x, 0f));

        // Galeria de columnas: marcos de madera de mina.
        foreach (float x in new[] { 58.5f, 68f, 74.5f })
            Pieza(padre, piezas["adorno_marco"], new Vector2(x, 20f - 0.05f), "Middleground", -6).color = new Color(0.75f, 0.7f, 0.65f);
        Pieza(padre, piezas["adorno_caja"], new Vector2(60.5f, 19.97f), "Middleground", -5).color = new Color(0.7f, 0.66f, 0.62f);
        Pieza(padre, piezas["adorno_caja2"], new Vector2(71.8f, 19.97f), "Middleground", -5).color = new Color(0.7f, 0.66f, 0.62f);
        // Antesala y arena: ruinas de ladrillo.
        foreach ((string n, float x, float yy) in new[] { ("pilar_ladrillo", 118.5f, 4f), ("columna_ladrillo", 131f, 4f), ("muro_ladrillo", 124f, 4f),
                                                         ("pilar_ladrillo2", 138.6f, 4f), ("columna_ladrillo2", 143.5f, 4f), ("columna_ladrillo", 172.5f, 4f),
                                                         ("muro_ladrillo2", 158f, 4f) })
            Pieza(padre, piezas["adorno_" + n], new Vector2(x, yy - 0.05f), "Middleground", -7).color = new Color(0.6f, 0.52f, 0.52f) * TinteEn(new Vector2(x, yy));

        // Primer plano: algunas estalactitas oscuras delante (solo en techos altos).
        foreach (Vector2 p in new[] { new Vector2(10f, U(12)), new Vector2(34f, U(12)), new Vector2(64f, U(48)), new Vector2(90f, U(48)), new Vector2(130f, U(21)) })
        {
            SpriteRenderer sr = Prop(frente, estalactitas[azar.Next(estalactitas.Length)], p + Vector2.up * 0.4f, "Ground", 30, 0.12f, true);
            sr.transform.localScale *= 1.0f;
        }

        // Bruma baja sobre los suelos principales.
        if (bruma != null)
            foreach (Vector2 p in new[] { new Vector2(8f, 0.15f), new Vector2(26f, 0.15f), new Vector2(46f, 0.15f), new Vector2(62f, 20.15f), new Vector2(84f, 20.15f),
                                          new Vector2(100f, 20.15f), new Vector2(66f, 4.15f), new Vector2(88f, 4.15f), new Vector2(111f, 4.15f), new Vector2(128f, 4.15f),
                                          new Vector2(152f, 4.15f), new Vector2(168f, 4.15f) })
            {
                SpriteRenderer sr = new GameObject("Bruma").AddComponent<SpriteRenderer>();
                sr.transform.SetParent(padre, false);
                sr.transform.position = p;
                sr.transform.localScale = new Vector3(3.5f, 1.3f, 1f);
                sr.sprite = bruma;
                // Delante de la roca pero por debajo de la oscuridad y del player.
                sr.sortingLayerName = "Middleground";
                sr.sortingOrder = 40;
                sr.color = new Color(1f, 1f, 1f, 0.25f);
                brumas.Add(sr);
            }
    }

    private static void Ambiente()
    {
        GameObject go = new GameObject("Ambiente");
        go.transform.SetParent(raizNueva, false);
        AmbienteCueva a = go.AddComponent<AmbienteCueva>();
        a.zonas = Zonas.Select(z => new AmbienteCueva.Zona { nombre = z.nombre, area = z.r, tinteFondo = z.tinte, colorBruma = z.bruma, densidadBruma = z.densidad }).ToArray();
        a.capasFondo = copiasFondo.ToArray();
        a.tinteCapa = tintesCopias.ToArray();
        a.bruma = brumas.ToArray();
    }
}
