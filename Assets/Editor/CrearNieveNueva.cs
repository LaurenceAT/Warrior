using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// Ronda 19: la nieve renovada (diseno en SPRITES PARA NUEVOS NIVELES/DISENO_NIEVE.md).
//
// La nieve de siempre se guarda como copia en Assets/Scenes/Nieve_Antigua.unity
// (fuera de la lista de escenas). La que se juega se llama siempre "Nivel Nieve":
// de ese nombre dependen el logro "Aliento helado", las partidas, el jefe vencido
// y el desafio de la Sombra. Menu Warrior > Nieve:
//   - Generar nieve nueva: rehace "Nivel Nieve" a partir de la antigua.
//   - Usar la nieve antigua / nueva: intercambia los archivos.
// Se conserva el aspecto (fondo, tiles, nieve, ambiente): cambian el trazado, los
// secretos, las mecanicas y los enemigos. La arena del jefe no se toca.
public static class CrearNieveNueva
{
    public const string RutaActiva = "Assets/Scenes/Nivel Nieve.unity";
    public const string RutaAntigua = "Assets/Scenes/Nieve_Antigua.unity";
    public const string RutaNuevaGuardada = "Assets/Scenes/Nieve_Nueva.unity";
    private const string Tiles = "Assets/Tiles/Nieve/";
    private const string Runas = "Assets/SPRITES PARA NUEVOS NIVELES/ICONOS/Iconos_RPGPack/16-runes-enchantments/";
    private const string RutaPistas = "Assets/Resources/PistasSellos.asset";

    // ------------------------------------------------------------------ Menu

    [MenuItem("Warrior/Nieve/Generar nieve nueva")]
    public static void Generar()
    {
        if (!Application.isBatchMode && !EditorUtility.DisplayDialog("Nieve nueva",
                "Se rehace la escena \"Nivel Nieve\" a partir de la copia de la nieve antigua (Nieve_Antigua). " +
                "Lo que hayas cambiado a mano en la nieve nueva se pierde.", "Generar", "Cancelar"))
            return;
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (File.Exists(RutaNuevaGuardada)) { Debug.LogWarning("[NieveNueva] Ahora se juega la antigua: usa primero \"Usar la nieve nueva\"."); return; }
        // La primera vez: la nieve de siempre pasa a ser la copia de seguridad.
        if (!File.Exists(RutaAntigua))
        {
            if (!AssetDatabase.CopyAsset(RutaActiva, RutaAntigua)) { Debug.LogError("[NieveNueva] No se pudo copiar la nieve antigua"); return; }
            AssetDatabase.Refresh();
        }
        Montar();
    }

    [MenuItem("Warrior/Nieve/Usar la nieve antigua")]
    public static void UsarAntigua()
    {
        if (!File.Exists(RutaAntigua)) { Debug.LogWarning("[NieveNueva] No hay nieve antigua guardada."); return; }
        if (File.Exists(RutaNuevaGuardada)) { Debug.LogWarning("[NieveNueva] La nieve antigua ya es la que se juega."); return; }
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        AssetDatabase.MoveAsset(RutaActiva, RutaNuevaGuardada);
        AssetDatabase.MoveAsset(RutaAntigua, RutaActiva);
        AsegurarEnBuild();
        Debug.Log("[NieveNueva] Ahora se juega la nieve ANTIGUA (la nueva queda en Nieve_Nueva).");
    }

    [MenuItem("Warrior/Nieve/Usar la nieve nueva")]
    public static void UsarNueva()
    {
        if (!File.Exists(RutaNuevaGuardada)) { Debug.LogWarning("[NieveNueva] La nieve nueva ya es la que se juega."); return; }
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        AssetDatabase.MoveAsset(RutaActiva, RutaAntigua);
        AssetDatabase.MoveAsset(RutaNuevaGuardada, RutaActiva);
        AsegurarEnBuild();
        Debug.Log("[NieveNueva] Ahora se juega la nieve NUEVA (la antigua queda en Nieve_Antigua).");
    }

    private static void AsegurarEnBuild()
    {
        var escenas = EditorBuildSettings.scenes.Where(s => s.path != RutaAntigua && s.path != RutaNuevaGuardada).ToList();
        int i = escenas.FindIndex(s => s.path == RutaActiva);
        var nueva = new EditorBuildSettingsScene(RutaActiva, true);
        if (i >= 0) escenas[i] = nueva; else escenas.Insert(Mathf.Min(1, escenas.Count), nueva);
        EditorBuildSettings.scenes = escenas.ToArray();
    }

    // ------------------------------------------------------------------ Trazado (1 casilla = 1 u)

    private const int X0 = -6, X1 = 410, Y0 = -12, Y1 = 32, YVisible = -8;
    private static bool[,] roca;
    private static HashSet<Vector2Int> ilusorias, falsas, ocultas, fondoInterior;
    private static readonly List<RectInt> hielo = new List<RectInt>();

    private static bool R(int x, int y)
    {
        if (x < X0 || x >= X1 || y < Y0) return true;
        if (y >= Y1) return false;
        return roca[x - X0, y - Y0];
    }

    // Para los bordes de los tiles: las paredes ilusorias y el hielo quebradizo cuentan como roca.
    private static bool RB(int x, int y) => R(x, y) || ilusorias.Contains(new Vector2Int(x, y)) || falsas.Contains(new Vector2Int(x, y));

    // Rectangulo en casillas: x, y (abajo), ancho, alto.
    private static void Cerrar(int x, int y, int w, int h) => Poner(x, y, w, h, true);
    private static void Abrir(int x, int y, int w, int h) => Poner(x, y, w, h, false);
    private static void Poner(int x, int y, int w, int h, bool r)
    {
        for (int i = x; i < x + w; i++)
        for (int j = y; j < y + h; j++)
            if (i >= X0 && i < X1 && j >= Y0 && j < Y1) roca[i - X0, j - Y0] = r;
    }
    private static void Hielo(int x, int y, int w, int h) { Cerrar(x, y, w, h); hielo.Add(new RectInt(x, y, w, h)); }
    private static void Marcar(HashSet<Vector2Int> s, int x, int y, int w, int h)
    {
        for (int i = x; i < x + w; i++) for (int j = y; j < y + h; j++) s.Add(new Vector2Int(i, j));
    }

    // Grietas mortales: x (izquierda), ancho, superficie.
    private static readonly (int x, int w, int sup, string nombre)[] Grietas =
    {
        (74, 2, 3, "Bosque"), (132, 2, 3, "Desfiladero_1"), (150, 2, 3, "Desfiladero_2"), (168, 2, 3, "Desfiladero_3"), (286, 2, 3, "Ruinas"),
    };

    private static void Trazar()
    {
        roca = new bool[X1 - X0, Y1 - Y0];
        ilusorias = new HashSet<Vector2Int>();
        falsas = new HashSet<Vector2Int>();
        ocultas = new HashSet<Vector2Int>();
        fondoInterior = new HashSet<Vector2Int>();
        hielo.Clear();

        Cerrar(-6, -12, 2, 44);                 // pared del principio
        // 1  Campamento del Paso
        Cerrar(-4, -12, 38, 12);                // suelo 0
        // 2  Bosque Nevado
        Cerrar(34, -12, 8, 14);                 // escalon a 2
        Cerrar(42, -12, 14, 15);                // suelo 3
        Hielo(56, -12, 12, 15);                 // hielo de enseñanza (seguro)
        Cerrar(68, -12, 6, 15);
        Cerrar(76, -12, 8, 15);                 // tras la grieta de enseñanza (74-76)
        Cerrar(79, 5, 3, 1);                    // repisa (arriba 6) para subir a la loma
        Cerrar(84, -12, 14, 19);                // loma (arriba 7)
        Abrir(85, 3, 10, 3);                    // Cueva del Ermitaño
        Marcar(ilusorias, 84, 3, 1, 3);         // su pared ilusoria
        Marcar(ocultas, 85, 3, 10, 3);
        Marcar(fondoInterior, 84, 3, 11, 3);
        Cerrar(98, -12, 8, 18);                 // bajada (arriba 6)
        Abrir(101, 3, 5, 2);                    // hueco del primer sello (claro)
        Marcar(ocultas, 101, 3, 4, 2);
        Marcar(fondoInterior, 101, 3, 5, 2);
        // 3  Claro de la Hoguera
        Cerrar(106, -12, 16, 15);
        // 4  Desfiladero del Viento
        Cerrar(122, -12, 10, 15);
        Cerrar(130, 3, 1, 2);                   // roca del refugio 1
        Cerrar(134, -12, 16, 15);
        Cerrar(147, 3, 1, 2);                   // refugio 2
        Cerrar(152, -12, 16, 15);
        Cerrar(160, 3, 2, 2);                   // refugio 3 (la roca de siempre)
        Cerrar(165, 3, 1, 2);                   // refugio 4
        Cerrar(170, -12, 30, 15);
        // 5  Lago Helado (el agua va de 200 a 217) y el Tumulo
        Cerrar(217, -12, 23, 15);
        Cerrar(223, 3, 2, 1);                   // escalon del tumulo (arriba 4)
        Cerrar(225, 3, 8, 3);                   // tumulo (arriba 6)
        Abrir(226, 3, 6, 2);                    // pasillo del tumulo (el derrumbe en x 232)
        Abrir(226, -3, 4, 6);                   // pozo del tumulo
        Abrir(226, -3, 16, 4);                  // pasadizo bajo la orilla
        Cerrar(228, -2, 2, 1);                  // repisa del pozo (arriba -1)
        Cerrar(226, 0, 2, 1);                   // repisa del pozo (arriba 1)
        Marcar(ocultas, 226, -3, 16, 8);
        Marcar(fondoInterior, 226, -3, 16, 8);
        // 6  Cuevas de Hielo
        Cerrar(240, -12, 46, 9);                // suelo de la galeria inferior (arriba -3)
        Cerrar(240, 1, 36, 2);                  // suelo de la galeria (arriba 3)
        Cerrar(242, 8, 34, 24);                 // techo del tunel
        Abrir(256, 1, 3, 2);                    // hielo quebradizo de enseñanza
        Marcar(falsas, 256, 1, 3, 2);
        Cerrar(276, -3, 2, 2);                  // escalones de salida (arriba -1)
        Cerrar(278, -3, 2, 4);                  // (arriba 1)
        Cerrar(280, -3, 2, 2);                  // (arriba -1)
        Marcar(fondoInterior, 240, -3, 42, 4);
        // 7  Ruinas de la Meseta
        Cerrar(282, -12, 48, 15);
        Cerrar(291, 3, 3, 2);                   // escalones a la meseta (5, 6, 8)
        Cerrar(294, 3, 3, 3);
        Cerrar(297, 3, 3, 5);
        Cerrar(300, 6, 18, 4);                  // meseta (arriba 10)
        Hielo(318, 6, 8, 4);
        Cerrar(326, 6, 4, 4);
        Marcar(ocultas, 300, 3, 29, 3);         // la Cripta (debajo)
        Marcar(fondoInterior, 300, 3, 30, 3);
        // Atalaya: repisas hasta el nicho de arriba (20).
        Cerrar(302, 11, 2, 1);
        Cerrar(306, 13, 2, 1);
        Cerrar(302, 15, 2, 1);
        Cerrar(306, 17, 2, 1);
        Cerrar(299, 19, 7, 1);                  // plataforma (arriba 20)
        Cerrar(299, 20, 1, 3);                  // pared del nicho
        Cerrar(299, 23, 7, 1);                  // techo del nicho
        Marcar(fondoInterior, 300, 20, 6, 3);
        // 8  Antesala
        Cerrar(330, -12, 32, 15);
        // Arena (igual que antes)
        Cerrar(362, -12, 36, 15);
        Cerrar(362, 20, 36, 12);
        Cerrar(398, -12, 12, 44);

        // Grietas: abiertas hasta y -8 (debajo, roca).
        foreach (var g in Grietas) Abrir(g.x, -8, g.w, g.sup + 8);
        // Hielo quebradizo con pista sobre la grieta de las ruinas.
        Marcar(falsas, 286, 2, 2, 1);
    }

    private static int Superficie(int x, float cercaDe, int vacioMinimo = 4)
    {
        int mejor = int.MinValue;
        float d = float.MaxValue;
        for (int y = YVisible; y <= Y1; y++)
        {
            if (!R(x, y - 1) || R(x, y)) continue;
            int libres = 0;
            while (libres < vacioMinimo && !R(x, y + libres) && y + libres < Y1 + 2) libres++;
            if (libres < vacioMinimo) continue;
            if (falsas.Contains(new Vector2Int(x, y - 1))) continue;
            float dd = Mathf.Abs(y - cercaDe);
            if (dd < d) { d = dd; mejor = y; }
        }
        return mejor;
    }

    // ------------------------------------------------------------------ Montaje

    private static Scene escena;
    private static Transform nivel, raizNueva;
    private static Sprite blanco, grietaSprite, monticuloSprite, rocaSprite;
    private static TileBase reglaParedFalsa, tileSombra;
    private static System.Random azar;

    private static void Montar()
    {
        Scene antigua = EditorSceneManager.OpenScene(RutaAntigua, OpenSceneMode.Single);
        // La salida va a la Cueva por su nombre (no "la siguiente de la lista": en la
        // lista de escenas el Bosque de la Cazadora esta entre la Nieve y la Cueva).
        PortalNivel salida = antigua.GetRootGameObjects().Select(g => g.GetComponent<PortalNivel>()).FirstOrDefault(c => c != null && c.name == "ExitDoor");
        if (salida != null)
        {
            SerializedObject so = new SerializedObject(salida);
            so.FindProperty("escenaDestino").stringValue = "Nivel Cueva";
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(antigua);
            EditorSceneManager.SaveScene(antigua);
        }
        EditorSceneManager.SaveScene(antigua, RutaActiva, true);
        escena = EditorSceneManager.OpenScene(RutaActiva, OpenSceneMode.Single);
        azar = new System.Random(1919);
        nivel = Raiz("Nivel").transform;
        raizNueva = new GameObject("NieveNueva").transform;

        Recursos();
        Trazar();
        Terreno();
        Decoracion();
        Peligros();
        Sellos();
        Objetos();
        Enemigos();
        Camara();
        Depuracion();

        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
        AsegurarEnBuild();
        Debug.Log("[NieveNueva] Nieve generada en " + RutaActiva);
    }

    private static GameObject Raiz(string n) => escena.GetRootGameObjects().FirstOrDefault(g => g.name == n);

    private static Transform Hijo(Transform padre, string n)
    {
        Transform t = padre.Find(n);
        if (t == null) { t = new GameObject(n).transform; t.SetParent(padre, false); }
        return t;
    }

    private static void Recursos()
    {
        blanco = AssetDatabase.LoadAllAssetsAtPath(Tiles + "Blanco.png").OfType<Sprite>().FirstOrDefault();
        grietaSprite = AssetDatabase.LoadAllAssetsAtPath(Tiles + "Grieta.png").OfType<Sprite>().FirstOrDefault();
        reglaParedFalsa = AssetDatabase.LoadAssetAtPath<TileBase>(Tiles + "Nieve (auto).asset");
        Tilemap zo = nivel.Find("Terreno/ZonasOcultas").GetComponent<Tilemap>();
        tileSombra = zo.GetTile(new Vector3Int(301, 3, 0)) ?? zo.GetTile(new Vector3Int(310, 4, 0));
        GameObject deco = Raiz("DecoracionExtra");
        foreach (SpriteRenderer s in deco.GetComponentsInChildren<SpriteRenderer>())
        {
            if (monticuloSprite == null && s.name == "monticulo de nieve") monticuloSprite = s.sprite;
            if (rocaSprite == null && s.name == "roca nevada") rocaSprite = s.sprite;
        }
        // Runas: pixel nitido (se ven en el mundo).
        foreach (string n in new[] { "icon_01", "icon_02", "icon_14" })
        {
            TextureImporter ti = AssetImporter.GetAtPath(Runas + n + ".png") as TextureImporter;
            if (ti != null && ti.filterMode != FilterMode.Point) { ti.filterMode = FilterMode.Point; ti.SaveAndReimport(); }
        }
        CrearPistasSellos();
    }

    private static Sprite Runa(string n) => AssetDatabase.LoadAllAssetsAtPath(Runas + n + ".png").OfType<Sprite>().FirstOrDefault();

    // Datos editables de los sellos (no pisa lo editado).
    private static void CrearPistasSellos()
    {
        if (AssetDatabase.LoadAssetAtPath<PistasSellos>(RutaPistas) != null) return;
        PistasSellos p = ScriptableObject.CreateInstance<PistasSellos>();
        p.entradas = new[]
        {
            new PistasSellos.Entrada { elemento = Elemento.Sagrado, color = new Color(1f, 0.84f, 0.4f), runa = Runa("icon_14"),
                sonidoReaccion = "sombra_rebote", sonidoAbrir = "sello_roto",
                textoPista = "Las sombras absorben el golpe. Solo la luz sagrada las disipa", textoAbrir = "La luz disipa las sombras" },
            new PistasSellos.Entrada { elemento = Elemento.Fuego, color = new Color(1f, 0.55f, 0.2f), runa = Runa("icon_01"),
                sonidoReaccion = "hielo_rebote", sonidoAbrir = "hielo_romper",
                textoPista = "El hielo es demasiado duro. Quizá el fuego...", textoAbrir = "¡El hielo se derrite!" },
            new PistasSellos.Entrada { elemento = Elemento.Hielo, color = new Color(0.6f, 0.85f, 1f), runa = Runa("icon_02"),
                sonidoReaccion = "agua_chapoteo", sonidoAbrir = "hielo_congelar_lago",
                textoPista = "El agua está helada... casi a punto de congelarse", textoAbrir = "Se congela" },
            new PistasSellos.Entrada { elemento = Elemento.Oscuro, color = new Color(0.62f, 0.42f, 0.95f), sonidoReaccion = "sombra_rebote", sonidoAbrir = "sello_roto" },
            new PistasSellos.Entrada { elemento = Elemento.Sangrado, color = new Color(0.9f, 0.2f, 0.25f), sonidoReaccion = "sombra_rebote", sonidoAbrir = "sello_roto" },
        };
        AssetDatabase.CreateAsset(p, RutaPistas);
    }

    // ------------------------------------------------------------------ Terreno

    private static void Terreno()
    {
        Transform t = nivel.Find("Terreno");
        Tilemap suelo = t.Find("Suelo").GetComponent<Tilemap>();
        Tilemap paredes = t.Find("ParedesFalsas").GetComponent<Tilemap>();
        Tilemap zonas = t.Find("ZonasOcultas").GetComponent<Tilemap>();
        suelo.ClearAllTiles();
        paredes.ClearAllTiles();
        zonas.ClearAllTiles();

        string[] nombres = { "esq_ai", "arriba", "esq_ad", "izq", "centro", "der", "esq_bi", "abajo", "esq_bd" };
        var nieve = nombres.ToDictionary(n => n, n => AssetDatabase.LoadAssetAtPath<TileBase>(Tiles + "nieve_" + n + ".asset"));
        var hie = nombres.ToDictionary(n => n, n => AssetDatabase.LoadAssetAtPath<TileBase>(Tiles + "hielo_" + n + ".asset"));

        for (int x = X0; x < X1; x++)
        for (int y = YVisible; y <= Y1; y++)
        {
            if (!R(x, y) || ilusorias.Contains(new Vector2Int(x, y))) continue;
            bool arriba = !RB(x, y + 1), abajo = !RB(x, y - 1), izq = !RB(x - 1, y), der = !RB(x + 1, y);
            string n = arriba ? (izq ? "esq_ai" : der ? "esq_ad" : "arriba")
                     : abajo ? (izq ? "esq_bi" : der ? "esq_bd" : "abajo")
                     : izq && der ? "der" : izq ? "izq" : der ? "der" : "centro";
            bool esHielo = hielo.Any(r => r.Contains(new Vector2Int(x, y)));
            suelo.SetTile(new Vector3Int(x, y, 0), esHielo ? hie[n] : nieve[n]);
        }
        PaletaNieve.ActualizarColision(suelo);
        foreach (Vector2Int c in ilusorias) paredes.SetTile(new Vector3Int(c.x, c.y, 0), reglaParedFalsa);
        // Solo los huecos (no la roca de alrededor, que se veria negra desde fuera).
        if (tileSombra != null) foreach (Vector2Int c in ocultas) if (!R(c.x, c.y) || ilusorias.Contains(c)) zonas.SetTile(new Vector3Int(c.x, c.y, 0), tileSombra);

        // Pared de fondo de los interiores nuevos (tapa el cielo del fondo).
        Transform viejo = t.Find("ParedFondo");
        if (viejo != null) Object.DestroyImmediate(viejo.gameObject);
        Tilemap fondo = new GameObject("ParedFondo", typeof(Tilemap), typeof(TilemapRenderer)).GetComponent<Tilemap>();
        fondo.transform.SetParent(t, false);
        fondo.color = new Color(0.42f, 0.45f, 0.55f, 1f);
        TilemapRenderer fr = fondo.GetComponent<TilemapRenderer>();
        fr.sortingLayerName = "Middleground";
        fr.sortingOrder = -50;
        foreach (Vector2Int c in fondoInterior)
            if (!R(c.x, c.y) || ilusorias.Contains(c)) fondo.SetTile(new Vector3Int(c.x, c.y, 0), nieve["centro"]);

        // Relleno bajo lo pintado.
        Transform relleno = nivel.Find("RellenoProfundo");
        if (relleno != null) Object.DestroyImmediate(relleno.gameObject);
        relleno = Hijo(nivel, "RellenoProfundo");
        SpriteRenderer sr = Pieza(relleno, "Relleno", blanco, new Vector2((X0 + X1) * 0.5f, (Y0 + YVisible) * 0.5f - 1f), new Vector2(X1 - X0, YVisible - Y0 + 2f), "Ground", -1);
        sr.color = new Color(0.12f, 0.1f, 0.09f);

        // Brillos sobre el hielo resbaladizo.
        Transform brillos = nivel.Find("BrillosHielo");
        if (brillos != null) Object.DestroyImmediate(brillos.gameObject);
        brillos = Hijo(nivel, "BrillosHielo");
        foreach (RectInt r in hielo)
        {
            GameObject go = new GameObject("Hielo");
            go.transform.SetParent(brillos);
            go.transform.position = new Vector3(r.center.x, r.center.y, 0f);
            SueloHielo sh = go.AddComponent<SueloHielo>();
            var lista = new List<SpriteRenderer>();
            for (float x = r.xMin + 0.6f; x < r.xMax - 0.4f; x += 1.7f)
            {
                SpriteRenderer b = Pieza(go.transform, "Brillo", blanco, new Vector2(x, r.yMax - 0.12f), new Vector2(0.9f, 0.05f), "Ground", 3);
                b.color = new Color(1f, 1f, 1f, 0.35f);
                lista.Add(b);
            }
            sh.PonerBrillos(lista.ToArray());
        }

        // Fuera los pinchos (la caida mortal general se queda).
        Transform trampas = nivel.Find("Trampas");
        if (trampas != null)
            foreach (Transform p in trampas.Cast<Transform>().ToList())
                if (p.name.StartsWith("Pinchos")) Object.DestroyImmediate(p.gameObject);
    }

    // Decoracion de siempre, apoyada en el suelo nuevo; fuera la que cae en un hueco.
    private static void Decoracion()
    {
        int movidas = 0, quitadas = 0;
        var piezas = new List<Transform>();
        Transform deco = nivel.Find("Decoracion");
        if (deco != null) piezas.AddRange(deco.Cast<Transform>());
        GameObject extra = Raiz("DecoracionExtra");
        if (extra != null) piezas.AddRange(extra.transform.Cast<Transform>());
        foreach (Transform p in piezas)
        {
            if (p.GetComponent<EstatuaPista>() != null) continue;
            SpriteRenderer s = p.GetComponentInChildren<SpriteRenderer>();
            float baseY = s != null ? s.bounds.min.y + 0.05f : p.position.y;
            int x = Mathf.FloorToInt(p.position.x);
            int sup = Superficie(x, baseY);
            bool lejos = sup == int.MinValue || Mathf.Abs(sup - baseY) > 9f;
            bool tapa = EnGrieta(x) || (x >= 199 && x <= 217) || (x >= 99 && x <= 107) || (x >= 222 && x <= 234) || (x >= 254 && x <= 261) || (x >= 299 && x <= 331 && sup < 9);
            if (lejos || tapa) { Object.DestroyImmediate(p.gameObject); quitadas++; continue; }
            float dy = sup - baseY;
            if (Mathf.Abs(dy) > 0.01f) { p.position += new Vector3(0f, dy, 0f); movidas++; }
        }
        Debug.Log($"[NieveNueva] decoracion: {movidas} apoyadas en el suelo nuevo, {quitadas} quitadas (huecos, sellos o secretos)");
    }

    private static bool EnGrieta(int x) => Grietas.Any(g => x >= g.x - 1 && x <= g.x + g.w);

    // ------------------------------------------------------------------ Peligros

    private static void Peligros()
    {
        Transform padre = Hijo(raizNueva, "Peligros");
        int items = LayerMask.NameToLayer("Items"), suelo = LayerMask.NameToLayer("Ground");

        // Grietas profundas: muerte al fondo (reaparicion en la ultima hoguera), niebla y velo.
        foreach (var g in Grietas)
        {
            GameObject go = new GameObject("Grieta_" + g.nombre);
            go.transform.SetParent(padre, false);
            go.transform.position = new Vector3(g.x + g.w * 0.5f, g.sup, 0f);
            GrietaMortal gm = go.AddComponent<GrietaMortal>();
            gm.ancho = g.w;
            gm.fondo = g.sup + 8f;
            gm.colorNiebla = new Color(0.85f, 0.92f, 1f, 0.3f);
            gm.colorVacio = new Color(0.03f, 0.04f, 0.08f, 0.95f);
            GameObject muerte = new GameObject("Muerte");
            muerte.layer = items;
            muerte.transform.SetParent(go.transform, false);
            muerte.transform.position = new Vector3(g.x + g.w * 0.5f, -7.3f, 0f);
            BoxCollider2D b = muerte.AddComponent<BoxCollider2D>();
            b.isTrigger = true;
            b.size = new Vector2(g.w, 1.4f);
            muerte.AddComponent<DeadArea>();
        }

        // Hielo quebradizo: el de enseñanza (cae a la galeria inferior) y el de las ruinas (con pista, sobre la grieta).
        HieloQuebradizo(padre, "HieloQuebradizo_Ensenanza", 256, 1, 3, 2, false);
        HieloQuebradizo(padre, "HieloQuebradizo_ConPista", 286, 2, 2, 1, true);

        // Refugios de la ventisca: tras cada roca, un remanso sin viento.
        int[] refugios = { 127, 144, 157, 162 };
        foreach (int x in refugios)
        {
            GameObject r = new GameObject("Refugio_" + x);
            r.layer = items;
            r.transform.SetParent(padre, false);
            r.transform.position = new Vector3(x + 1.5f, 4.5f, 0f);
            BoxCollider2D b = r.AddComponent<BoxCollider2D>();
            b.isTrigger = true;
            b.size = new Vector2(3f, 3f);
            r.AddComponent<RefugioViento>();
            // El remanso se ve: nieve quieta y mas clara en el suelo.
            SpriteRenderer m = Pieza(r.transform, "Remanso", blanco, new Vector2(x + 1.5f, 3.06f), new Vector2(2.6f, 0.12f), "Ground", 4);
            m.color = new Color(0.95f, 0.98f, 1f, 0.55f);
            if (monticuloSprite != null)
            {
                SpriteRenderer nm = Pieza(r.transform, "NieveQuieta", monticuloSprite, new Vector2(x + 0.8f, 3f), Vector2.zero, "Middleground", 3);
                nm.transform.localScale = Vector3.one * 0.6f;
                nm.transform.position = new Vector3(x + 0.8f, 3f - (nm.bounds.min.y - nm.transform.position.y) - 0.02f, 0f);
            }
        }

        // Atajo: el derrumbe del Tumulo del Lago (solo cede desde dentro).
        GameObject der = new GameObject("DerrumbeTumulo");
        der.layer = suelo;
        der.transform.SetParent(padre, false);
        der.transform.position = new Vector3(232.5f, 4f, 0f);
        BoxCollider2D bd = der.AddComponent<BoxCollider2D>();
        bd.size = new Vector2(1f, 2f);
        Transform dv = new GameObject("Visual").transform;
        dv.SetParent(der.transform, false);
        for (int k = 0; k < 3; k++)
        {
            SpriteRenderer sr = Pieza(dv, "Roca", rocaSprite != null ? rocaSprite : blanco, new Vector2(232.5f + (k % 2 == 0 ? -0.1f : 0.12f), 3.35f + k * 0.62f), Vector2.zero, "Middleground", 8 + k);
            float esc = rocaSprite != null ? 1.05f / Mathf.Max(0.1f, sr.sprite.bounds.size.x) : 1f;
            sr.transform.localScale = new Vector3(esc, esc, 1f);
            sr.color = new Color(0.78f, 0.82f, 0.9f);
        }
        DerrumbeAtajo da = der.AddComponent<DerrumbeAtajo>();
        da.clave = "tumulo";
        da.ladoQueAbre = -1;
        da.visual = dv;
        da.sonidoGolpe = "cueva_crujido";
        da.sonidoAbrir = "cueva_derrumbe";
    }

    private static void HieloQuebradizo(Transform padre, string nombre, int x, int y, int w, int h, bool pista)
    {
        GameObject go = new GameObject(nombre);
        go.layer = LayerMask.NameToLayer("Ground");
        go.transform.SetParent(padre, false);
        go.transform.position = new Vector3(x + w * 0.5f, y + h * 0.5f, 0f);
        BoxCollider2D b = go.AddComponent<BoxCollider2D>();
        b.size = new Vector2(w, h);
        Transform vis = new GameObject("Visual").transform;
        vis.SetParent(go.transform, false);
        for (int i = 0; i < w; i++)
        for (int j = 0; j < h; j++)
        {
            string n = j == h - 1 ? (i == 0 ? "arriba" : i == w - 1 ? "arriba" : "arriba") : "centro";
            Tile t = AssetDatabase.LoadAssetAtPath<Tile>(Tiles + "hielo_" + n + ".asset");
            SpriteRenderer sr = Pieza(vis, "Pieza", t != null ? t.sprite : blanco, new Vector2(x + i + 0.5f, y + j + 0.5f), Vector2.zero, "Ground", 2);
            if (t != null) sr.color = t.color;
            float esc = 1f / Mathf.Max(0.01f, sr.sprite.bounds.size.x);
            sr.transform.localScale = new Vector3(esc, esc, 1f);
        }
        SueloFalso sf = go.AddComponent<SueloFalso>();
        sf.visual = vis;
        sf.conPista = pista;
        sf.tonoPista = new Color(0.86f, 0.92f, 1f, 1f);
        sf.sonidoCrujir = "hielo_rebote";
        sf.sonidoCaer = "hielo_romper";
        sf.retraso = pista ? 0.45f : 0.6f;
        if (pista && grietaSprite != null)
        {
            SpriteRenderer gri = Pieza(vis, "Grietas", grietaSprite, new Vector2(x + w * 0.5f, y + h - 0.3f), Vector2.zero, "Ground", 6);
            gri.transform.localScale = new Vector3(w / Mathf.Max(0.01f, gri.sprite.bounds.size.x), 0.5f / Mathf.Max(0.01f, gri.sprite.bounds.size.y), 1f);
            gri.color = new Color(0.55f, 0.75f, 0.95f, 0.85f);
            sf.grietas = gri;
        }
    }

    // ------------------------------------------------------------------ Sellos

    private static void Sellos()
    {
        Transform obst = nivel.Find("Obstaculos");
        Transform padre = Hijo(raizNueva, "Sellos");

        // El "portal oscuro" de siempre: ahora guarda la Cripta (cara este de la meseta).
        SelloSombrio viejo = obst.GetComponentInChildren<SelloSombrio>(true);
        Collider2D solidoViejo = viejo.GetComponent<Collider2D>();
        SpriteRenderer[] sombras = viejo.GetComponentsInChildren<SpriteRenderer>(true);
        GameObject plantillaSombra = Object.Instantiate(viejo.gameObject);
        plantillaSombra.name = "PlantillaSombra";
        viejo.transform.position = new Vector3(329.4f, viejo.transform.position.y, 0f);
        viejo.name = "Sello_Cripta";
        viejo.transform.SetParent(padre, true);
        Object.DestroyImmediate(viejo);
        SelloElemental cripta = Sello(solidoViejo.gameObject, Elemento.Sagrado, SelloElemental.Estilo.Sombra, 2, "cripta", false, solidoViejo, sombras);

        // Primer sello (enseñanza): una sombra pequeña en el hueco del claro, junto a la hoguera 2.
        GameObject claroGo = plantillaSombra;
        claroGo.name = "Sello_Claro";
        claroGo.transform.SetParent(padre, true);
        Object.DestroyImmediate(claroGo.GetComponent<SelloSombrio>());
        Collider2D cc = claroGo.GetComponent<Collider2D>();
        float altoViejo = cc.bounds.size.y;
        claroGo.transform.position = new Vector3(105.4f, 4f, 0f);
        if (cc is BoxCollider2D bc) { bc.size = new Vector2(bc.size.x, 2f); bc.offset = Vector2.zero; }
        foreach (SpriteRenderer s in claroGo.GetComponentsInChildren<SpriteRenderer>(true))
        {
            Vector3 lp = s.transform.localPosition;
            s.transform.localPosition = new Vector3(lp.x, lp.y * 2f / altoViejo, lp.z);
            s.transform.localScale = new Vector3(s.transform.localScale.x, s.transform.localScale.y * 0.75f, 1f);
        }
        SelloElemental claro = Sello(claroGo, Elemento.Sagrado, SelloElemental.Estilo.Sombra, 2, "claro", true, cc, claroGo.GetComponentsInChildren<SpriteRenderer>(true));

        // Muro de hielo de las cuevas (fuego): mismo sitio, ahora se recuerda abierto.
        MuroHielo muro = obst.GetComponentInChildren<MuroHielo>(true);
        GameObject plantillaHielo = Object.Instantiate(muro.gameObject);
        SpriteRenderer[] bloques = muro.GetComponentsInChildren<SpriteRenderer>(true);
        Collider2D solidoMuro = muro.GetComponent<Collider2D>();
        GameObject muroGo = muro.gameObject;
        Object.DestroyImmediate(muro);
        muroGo.name = "Sello_MuroHielo";
        SelloElemental selloMuro = Sello(muroGo, Elemento.Fuego, SelloElemental.Estilo.Hielo, 3, "muro", false, solidoMuro, bloques);

        // Bloque de hielo de la Atalaya (fuego): copia pequeña del muro, guarda el cofre de mejora.
        plantillaHielo.name = "Sello_Atalaya";
        plantillaHielo.transform.SetParent(padre, true);
        Object.DestroyImmediate(plantillaHielo.GetComponent<MuroHielo>());
        Collider2D ca = plantillaHielo.GetComponent<Collider2D>();
        Bounds bm = ca.bounds;
        float k = 3f / bm.size.y;
        plantillaHielo.transform.localScale = new Vector3(plantillaHielo.transform.localScale.x * 0.8f, plantillaHielo.transform.localScale.y * k, 1f);
        Physics2D.SyncTransforms();
        Bounds bn = ca.bounds;
        plantillaHielo.transform.position += new Vector3(303.7f - bn.center.x, 20f - bn.min.y, 0f);
        SelloElemental atalaya = Sello(plantillaHielo, Elemento.Fuego, SelloElemental.Estilo.Hielo, 3, "atalaya", false, ca, plantillaHielo.GetComponentsInChildren<SpriteRenderer>(true));

        // Lago: se recuerda congelado.
        AguaCongelable lago = obst.GetComponentInChildren<AguaCongelable>(true);
        if (lago != null) { lago.clave = "lago"; EditorUtility.SetDirty(lago); }

        // Señales (dos por sello como minimo: el color del sello y su runa; y algo mas).
        claro.simbolos = new[] { Simbolo(padre, Elemento.Sagrado, new Vector2(107.6f, 3f), true), Brillo(padre, Elemento.Sagrado, new Vector2(105.4f, 3.03f), 1.6f) };
        Simbolo(padre, Elemento.Hielo, new Vector2(198.4f, 3f), true);
        Brillo(padre, Elemento.Hielo, new Vector2(199f, 3.03f), 1.4f);
        selloMuro.simbolos = new[] { Simbolo(padre, Elemento.Fuego, new Vector2(244.2f, 3f), true), GrietasColor(padre, Elemento.Fuego, new Vector2(244.6f, 8.35f)) };
        // La Atalaya: el bloque se ve desde la meseta; runa grabada en el escalon de las ruinas.
        atalaya.simbolos = new[] { Simbolo(padre, Elemento.Fuego, new Vector2(298.5f, 5.2f), false), Brillo(padre, Elemento.Fuego, new Vector2(303.7f, 20.03f), 1.2f) };
        // La Cripta: brillo dorado en el suelo de la boca y runa en la antesala.
        cripta.simbolos = new[] { Simbolo(padre, Elemento.Sagrado, new Vector2(331.6f, 3f), true), Brillo(padre, Elemento.Sagrado, new Vector2(329.4f, 3.03f), 1.8f),
                                  GrietasColor(padre, Elemento.Sagrado, new Vector2(328.6f, 7.4f)) };
        foreach (SelloElemental s in new[] { claro, cripta, selloMuro, atalaya }) EditorUtility.SetDirty(s);
    }

    private static SelloElemental Sello(GameObject go, Elemento e, SelloElemental.Estilo estilo, int golpes, string clave, bool ensenanza, Collider2D solido, SpriteRenderer[] visuales)
    {
        SelloElemental s = go.AddComponent<SelloElemental>();
        s.elemento = e;
        s.estilo = estilo;
        s.golpesNecesarios = golpes;
        s.clave = clave;
        s.ensenanza = ensenanza;
        s.solido = solido;
        s.visuales = visuales.Where(v => v != null).ToArray();
        return s;
    }

    // Runa grabada: de pie sobre el suelo (enSuelo) o en una pared.
    private static SimboloSello Simbolo(Transform padre, Elemento e, Vector2 pos, bool enSuelo)
    {
        PistasSellos.Entrada d = AssetDatabase.LoadAssetAtPath<PistasSellos>(RutaPistas).De(e);
        GameObject go = new GameObject("Runa_" + e);
        go.transform.SetParent(padre, false);
        SpriteRenderer sr = Pieza(go.transform, "Runa", d.runa != null ? d.runa : blanco, pos, Vector2.zero, "Ground", 7);
        float esc = 0.62f / Mathf.Max(0.01f, sr.sprite.bounds.size.x);
        sr.transform.localScale = new Vector3(esc, esc, 1f);
        if (enSuelo) sr.transform.position = new Vector3(pos.x, pos.y + sr.bounds.extents.y - 0.04f, 0f);
        // Halo suave detras.
        SpriteRenderer halo = Pieza(go.transform, "Halo", EfectoResplandor(), sr.transform.position, Vector2.zero, "Ground", 6);
        halo.transform.localScale = Vector3.one * 1.3f;
        halo.color = new Color(d.color.r, d.color.g, d.color.b, 0.35f);
        go.transform.position = Vector3.zero;
        SimboloSello s = go.AddComponent<SimboloSello>();
        s.elemento = e;
        s.piezas = new[] { sr, halo };
        return s;
    }

    // Brillo en el suelo (franja fina del color del elemento).
    private static SimboloSello Brillo(Transform padre, Elemento e, Vector2 pos, float ancho)
    {
        PistasSellos.Entrada d = AssetDatabase.LoadAssetAtPath<PistasSellos>(RutaPistas).De(e);
        GameObject go = new GameObject("Brillo_" + e);
        go.transform.SetParent(padre, false);
        SpriteRenderer sr = Pieza(go.transform, "Franja", blanco, pos, new Vector2(ancho, 0.08f), "Ground", 5);
        sr.color = new Color(d.color.r, d.color.g, d.color.b, 0.7f);
        SimboloSello s = go.AddComponent<SimboloSello>();
        s.elemento = e;
        s.piezas = new[] { sr };
        return s;
    }

    // Grietas finas del color del elemento en la roca (cerca del sello).
    private static SimboloSello GrietasColor(Transform padre, Elemento e, Vector2 pos)
    {
        PistasSellos.Entrada d = AssetDatabase.LoadAssetAtPath<PistasSellos>(RutaPistas).De(e);
        GameObject go = new GameObject("Grietas_" + e);
        go.transform.SetParent(padre, false);
        var piezas = new List<SpriteRenderer>();
        float[] angulos = { 62f, -35f, 18f, -70f };
        Vector2 p = pos;
        for (int i = 0; i < angulos.Length; i++)
        {
            float largo = 0.35f + (float)azar.NextDouble() * 0.3f;
            SpriteRenderer sr = Pieza(go.transform, "Grieta", blanco, p, new Vector2(largo, 0.06f), "Ground", 6);
            sr.transform.rotation = Quaternion.Euler(0f, 0f, angulos[i]);
            sr.color = new Color(d.color.r, d.color.g, d.color.b, 0.8f);
            piezas.Add(sr);
            p += (Vector2)(Quaternion.Euler(0f, 0f, angulos[i]) * Vector2.right) * largo * 0.9f;
        }
        SimboloSello s = go.AddComponent<SimboloSello>();
        s.elemento = e;
        s.piezas = piezas.ToArray();
        return s;
    }

    private static Sprite resplandor;
    private static Sprite EfectoResplandor()
    {
        if (resplandor != null) return resplandor;
        const string ruta = "Assets/Tiles/Nieve/Resplandor.png";
        if (!File.Exists(ruta))
        {
            const int n = 32;
            Texture2D t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n * 0.5f, n * 0.5f)) / (n * 0.5f);
                float a = Mathf.Clamp01(1f - d);
                // Escalones de pixel art (no un degradado suave).
                a = Mathf.Floor(a * a * 4f) / 4f;
                t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            File.WriteAllBytes(ruta, t.EncodeToPNG());
            AssetDatabase.ImportAsset(ruta);
            TextureImporter ti = (TextureImporter)AssetImporter.GetAtPath(ruta);
            ti.textureType = TextureImporterType.Sprite;
            ti.spritePixelsToUnits = 28;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.SaveAndReimport();
        }
        resplandor = AssetDatabase.LoadAssetAtPath<Sprite>(ruta);
        return resplandor;
    }

    // ------------------------------------------------------------------ Hogueras, estatuas y cofres

    private static readonly (string id, string titulo, Vector2 pos, string texto)[] EstatuasNuevas =
    {
        ("estatua_claro", "La sombra del claro", new Vector2(109f, 3f),
            "Una sombra viva tapona la grieta de la roca. Los peregrinos la apartaban con luz: imbuye tu hoja con lo Sagrado (E) y golpéala. " +
            "Hay sellos así por toda la montaña. Cada uno teme a un elemento, y su color lo delata."),
        ("estatua_ermitano", "El ermitaño", new Vector2(87f, 3f),
            "Aquí vivió un ermitaño que contaba los inviernos con muescas en la roca. La última dice: «El viento del desfiladero se calma tras las piedras. " +
            "Espera allí a que pase la ráfaga»."),
        ("estatua_picadores", "Los picadores de hielo", new Vector2(246f, -3f),
            "Los picadores abrieron un pasadizo bajo la orilla para volver al fuego sin cruzar el muro. Cuando el túmulo se vino abajo, " +
            "taparon la salida con piedras... desde dentro."),
        ("estatua_vigias", "Los vigías", new Vector2(289f, 3f),
            "Los vigías de la meseta guardaban sus ofrendas en lo alto, tras hielo que solo el fuego ablanda. Mira arriba antes de seguir."),
        ("estatua_cripta", "La cripta de los guardianes", new Vector2(305f, 3f),
            "Los guardianes de las ruinas duermen bajo la nieve, y no les gusta que los despierten. Lo que custodiaban sigue al fondo."),
    };

    private static void Objetos()
    {
        GameObject estatuas = Raiz("Estatuas");
        EstatuaPista plantilla = estatuas.GetComponentInChildren<EstatuaPista>();
        float alturaEstatua = plantilla.transform.position.y - 0f;  // la del campamento esta en suelo 0
        foreach (EstatuaPista e in Object.FindObjectsByType<EstatuaPista>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            switch (e.id)
            {
                case "estatua_2": Mover(e.transform, 53f, 3f, alturaEstatua); break;
                case "estatua_6":
                    Mover(e.transform, 333.5f, 3f, alturaEstatua);
                    e.texto = "Al pie de la meseta, una sombra viva respira contra la roca, como la del claro de la ladera. Los guardianes sellaban así sus criptas.";
                    EditorUtility.SetDirty(e);
                    break;
            }
        }
        foreach (var n in EstatuasNuevas)
        {
            if (Object.FindObjectsByType<EstatuaPista>(FindObjectsInactive.Include, FindObjectsSortMode.None).Any(e => e.id == n.id)) continue;
            GameObject go = Object.Instantiate(plantilla.gameObject, estatuas.transform);
            go.name = "Estatua_" + n.id;
            EstatuaPista ep = go.GetComponent<EstatuaPista>();
            ep.id = n.id;
            ep.titulo = n.titulo;
            ep.texto = n.texto;
            Mover(go.transform, n.pos.x, n.pos.y, alturaEstatua);
        }

        // Cofres: los de siempre (mismos objetos) en sitios nuevos, y de almas en los secretos.
        Transform cofreMejora = nivel.Find("CofreMejora_mejora_1");
        if (cofreMejora != null) cofreMejora.position = new Vector3(301.5f, 20f, 0f);
        CofreAlmas almas = nivel.GetComponentInChildren<CofreAlmas>(true);
        GameObject plantillaCofre = Object.Instantiate(almas.gameObject);
        almas.transform.position = new Vector3(302.4f, 3f, 0f);
        Transform secretos = Hijo(raizNueva, "Secretos");
        CofreDeAlmas(plantillaCofre, secretos, "nieve_ermitano", new Vector2(93.5f, 3f), 300);
        CofreDeAlmas(plantillaCofre, secretos, "nieve_claro", new Vector2(102.2f, 3f), 250);
        Object.DestroyImmediate(plantillaCofre);
    }

    private static void Mover(Transform t, float x, float suelo, float alto) => t.position = new Vector3(x, suelo + alto, t.position.z);

    private static void CofreDeAlmas(GameObject plantilla, Transform padre, string clave, Vector2 pos, int cantidad)
    {
        GameObject go = Object.Instantiate(plantilla, padre);
        go.name = "CofreAlmas_" + clave;
        go.transform.position = pos;
        CofreAlmas ca = go.GetComponent<CofreAlmas>();
        SerializedObject so = new SerializedObject(ca);
        so.FindProperty("clave").stringValue = clave;
        so.FindProperty("almas").intValue = cantidad;
        so.FindProperty("darFrasco").boolValue = false;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ------------------------------------------------------------------ Enemigos

    private struct Ene
    {
        public string tipo;
        public float x, y;
        public bool emboscada, flanquear, retirarse;
        public int avisar;
        public Ene(string t, float x, float y) { tipo = t; this.x = x; this.y = y; emboscada = flanquear = retirarse = false; avisar = 0; }
    }

    private static Ene E(string t, float x, float y) => new Ene(t, x, y);
    private static Ene Emb(Ene e) { e.emboscada = true; return e; }
    private static Ene Fla(Ene e) { e.flanquear = true; return e; }
    private static Ene Ret(Ene e) { e.retirarse = true; return e; }
    private static Ene Avi(Ene e, int n) { e.avisar = n; return e; }

    private static readonly Ene[] ListaEnemigos =
    {
        E("RataEscarcha", 24f, 0f),                               // 1: la primera, sola
        E("MurcielagoCumbres", 62f, 6f),                          // 2: el primer murcielago, solo (sobre el hielo)
        E("RataEscarcha", 78f, 3f), Fla(E("RataEscarcha", 81f, 3f)),
        Emb(E("RataEscarcha", 92f, 7f)),                          // montículo de enseñanza (loma)
        E("MurcielagoCumbres", 140f, 8f),                         // 4: desfiladero
        Avi(E("OjoVigia", 159f, 8f), 2),
        Ret(E("RataEscarcha", 177f, 3f)), Fla(E("RataEscarcha", 182f, 3f)),
        E("MurcielagoCumbres", 190f, 8f),
        E("OjoVigia", 221f, 8f),                                  // 5: lago
        E("HechiceroSombrio", 266f, 3f),                          // 6: cuevas (el primero, solo)
        Emb(E("RataEscarcha", 262f, -3f)), Emb(E("RataEscarcha", 270f, -3f)),
        E("RataEscarcha", 283.5f, 3f), E("ArqueraArcana", 298.5f, 8f),   // 7: combinacion (rata abajo, arquera en alto)
        E("ArqueraArcana", 327f, 10f),
        Avi(E("OjoVigia", 320f, 14f), 2),
        Emb(E("RataEscarcha", 320f, 3f)), Emb(E("RataEscarcha", 313f, 3f)), Emb(E("RataEscarcha", 306f, 3f)),  // 8: la Cripta
        E("HechiceroSombrio", 339f, 3f),                          // 9: antesala
    };

    private static void Enemigos()
    {
        Transform ene = nivel.Find("Enemigos");
        var libres = ene.Cast<Transform>().ToList();
        int antes = libres.Count, creados = 0;
        foreach (Ene d in ListaEnemigos)
        {
            Transform t = libres.FirstOrDefault(x => x.name == d.tipo);
            if (t != null) libres.Remove(t);
            else
            {
                GameObject p = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/Nieve/" + d.tipo + ".prefab");
                if (p == null) { Debug.LogWarning("[NieveNueva] Falta el prefab " + d.tipo); continue; }
                t = ((GameObject)PrefabUtility.InstantiatePrefab(p, ene)).transform;
                creados++;
            }
            t.position = new Vector3(d.x, d.y + 0.02f, 0f);
            if (d.emboscada) Emboscada(t.gameObject);
            if (d.flanquear || d.retirarse)
            {
                SerializedObject so = new SerializedObject(t.GetComponent<EnemigoRata>());
                so.FindProperty("flanquear").boolValue = d.flanquear;
                so.FindProperty("retirarse").boolValue = d.retirarse;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            if (d.avisar > 0)
            {
                SerializedObject so = new SerializedObject(t.GetComponent<EnemigoBase>());
                so.FindProperty("avisarCercanos").intValue = d.avisar;
                so.FindProperty("radioAviso").floatValue = 20f;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
        foreach (Transform sobra in libres) { Debug.LogWarning("[NieveNueva] Enemigo sin sitio (se quita): " + sobra.name); Object.DestroyImmediate(sobra.gameObject); }

        // Elite: Morgath, en la meseta de las ruinas.
        GameObject elite = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/Elites/EliteMorgath.prefab");
        if (elite != null)
        {
            GameObject m = (GameObject)PrefabUtility.InstantiatePrefab(elite, ene);
            m.transform.position = new Vector3(311.5f, 10.02f, 0f);
        }
        Debug.Log($"[NieveNueva] enemigos: antes {antes}, ahora {ene.childCount} ({creados} nuevos + elite)");
    }

    private static void Emboscada(GameObject enemigo)
    {
        MonticuloEmboscada m = enemigo.GetComponent<MonticuloEmboscada>() ?? enemigo.AddComponent<MonticuloEmboscada>();
        Transform viejo = enemigo.transform.Find("Monticulo");
        if (viejo != null) Object.DestroyImmediate(viejo.gameObject);
        SpriteRenderer sr = new GameObject("Monticulo").AddComponent<SpriteRenderer>();
        sr.transform.SetParent(enemigo.transform, false);
        sr.sprite = monticuloSprite != null ? monticuloSprite : blanco;
        sr.sortingLayerName = "Middleground";
        sr.sortingOrder = 6;
        float esc = 1.5f / Mathf.Max(0.01f, sr.sprite.bounds.size.x);
        sr.transform.localScale = new Vector3(esc / Mathf.Max(0.01f, enemigo.transform.lossyScale.x), esc / Mathf.Max(0.01f, enemigo.transform.lossyScale.y), 1f);
        // Apoyado en el suelo, a los pies del enemigo.
        float pie = enemigo.transform.position.y - 0.02f;
        sr.transform.position = new Vector3(enemigo.transform.position.x, pie, 0f);
        sr.transform.position += Vector3.up * (pie - sr.bounds.min.y - 0.03f);
        m.monticulo = sr;
    }

    // ------------------------------------------------------------------ Camara y depuracion

    private static void Camara()
    {
        GameObject lim = Raiz("CameraLimit");
        PolygonCollider2D poly = lim != null ? lim.GetComponent<PolygonCollider2D>() : null;
        if (poly != null)
        {
            poly.pathCount = 1;
            poly.SetPath(0, new[] { new Vector2(-4f, -6.5f), new Vector2(398f, -6.5f), new Vector2(398f, 26f), new Vector2(-4f, 26f) });
        }
        // Las ruinas llegan ahora hasta la Atalaya.
        Transform ruinas = nivel.Find("ZonasCamara/Zona_Ruinas");
        if (ruinas != null)
        {
            ruinas.position = new Vector3(313.5f, 14.5f, 0f);
            BoxCollider2D b = ruinas.GetComponent<BoxCollider2D>();
            b.offset = Vector2.zero;
            b.size = new Vector2(63f, 23f);
        }
    }

    private static void Depuracion()
    {
        DepuracionNieve dep = raizNueva.gameObject.AddComponent<DepuracionNieve>();
        Vector2 P(float x, float y) => new Vector2(x, y + 0.7f);
        dep.puntos = new[]
        {
            new DepuracionNieve.Punto { nombre = "1. Campamento del Paso", pos = P(3f, 0f) },
            new DepuracionNieve.Punto { nombre = "2. Bosque Nevado (hielo)", pos = P(54f, 3f) },
            new DepuracionNieve.Punto { nombre = "2. Grieta de enseñanza", pos = P(71f, 3f) },
            new DepuracionNieve.Punto { nombre = "Secreto: Cueva del Ermitaño", pos = P(82f, 3f) },
            new DepuracionNieve.Punto { nombre = "2. Loma (emboscada)", pos = P(86f, 7f) },
            new DepuracionNieve.Punto { nombre = "3. Claro (primer sello)", pos = P(108f, 3f) },
            new DepuracionNieve.Punto { nombre = "4. Desfiladero del Viento", pos = P(124f, 3f) },
            new DepuracionNieve.Punto { nombre = "5. Lago Helado", pos = P(196.5f, 3f) },
            new DepuracionNieve.Punto { nombre = "5. Túmulo (atajo, lado orilla)", pos = P(234f, 3f) },
            new DepuracionNieve.Punto { nombre = "6. Cuevas de Hielo (muro)", pos = P(243f, 3f) },
            new DepuracionNieve.Punto { nombre = "6. Hielo quebradizo", pos = P(253f, 3f) },
            new DepuracionNieve.Punto { nombre = "6. Galería inferior", pos = P(250f, -3f) },
            new DepuracionNieve.Punto { nombre = "6. Pasadizo del atajo", pos = P(236f, -3f) },
            new DepuracionNieve.Punto { nombre = "7. Ruinas (entrada)", pos = P(283f, 3f) },
            new DepuracionNieve.Punto { nombre = "7. Meseta", pos = P(303f, 10f) },
            new DepuracionNieve.Punto { nombre = "7. Atalaya (arriba)", pos = P(305f, 20f) },
            new DepuracionNieve.Punto { nombre = "8. Cripta (boca)", pos = P(331f, 3f) },
            new DepuracionNieve.Punto { nombre = "9. Antesala", pos = P(345f, 3f) },
            new DepuracionNieve.Punto { nombre = "10. Arena", pos = P(372f, 3f) },
        };
    }

    // ------------------------------------------------------------------ Ayudas

    private static SpriteRenderer Pieza(Transform padre, string nombre, Sprite s, Vector2 centro, Vector2 tamano, string capa, int orden)
    {
        SpriteRenderer sr = new GameObject(nombre).AddComponent<SpriteRenderer>();
        sr.transform.SetParent(padre, false);
        sr.sprite = s;
        sr.transform.position = centro;
        if (tamano != Vector2.zero && s != null)
            sr.transform.localScale = new Vector3(tamano.x / s.bounds.size.x, tamano.y / s.bounds.size.y, 1f);
        sr.sortingLayerName = capa;
        sr.sortingOrder = orden;
        return sr;
    }
}
