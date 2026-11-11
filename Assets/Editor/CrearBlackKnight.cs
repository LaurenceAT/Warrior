using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// El Black Knight (jefe nuevo, SOLO en la sala de pruebas). Menu Warrior > Black Knight.
//
// El original es una sola hoja (Assets/SPRITES PARA NUEVOS NIVELES/BOSSES/Black
// Knight/ALL_In_One_NEW, que no se toca) de 18 x 18 celdas de 144x64, con un marco
// de color de 1 px en cada celda que separa las animaciones. Aqui se hace una
// copia limpia (sin marcos) en Assets/Sprites/BlackKnight, importada para pixel
// art. Las animaciones van seguidas por la hoja (una puede empezar en una fila y
// acabar en la siguiente): cada clip es "desde la celda N, tantos cuadros".
// Pies en la fila 62 desde arriba y el cuerpo en x = 69: a 28 px por unidad,
// como el jugador (mide lo mismo que el).
//
// "Crear o actualizar" hace:
//   - la copia limpia de la hoja (si no existe) y su importacion,
//   - la ficha Assets/Data/Jefes/Ajustes Black Knight (solo si no existe: lo editado no se pisa),
//   - el prefab Assets/Prefabs/Enemies/Nuevos/JefeBlackKnight (si existe, solo rehace sus clips),
//   - su entrada en el catalogo de la sala (si ya esta, solo copia el nombre y colores de la ficha).
// No lo conecta con niveles, Desafios, Bitacora, Bestiario, logros ni Resources.
public static class CrearBlackKnight
{
    public const string Original = "Assets/SPRITES PARA NUEVOS NIVELES/BOSSES/Black Knight/ALL_In_One_NEW/Black_Knight_All_In_One_New.png";
    public const string CarpetaOriginal = "Assets/SPRITES PARA NUEVOS NIVELES/BOSSES/Black Knight";
    public const string CarpetaSprites = "Assets/Sprites/BlackKnight";
    public const string RutaHoja = CarpetaSprites + "/BlackKnight.png";
    public const string RutaFicha = "Assets/Data/Jefes/Ajustes Black Knight.asset";
    public const string RutaPrefab = "Assets/Prefabs/Enemies/Nuevos/JefeBlackKnight.prefab";
    private const string MaterialFlash = "Assets/Materiales/Enemy_Flash.mat";

    public const int AnchoCelda = 144, AltoCelda = 64, Columnas = 18, Filas = 18;
    public const float PixelesPorUnidad = 28f;
    // Pies: x = 69,5 de 144; y = el borde de abajo de la fila 62 (1 px desde abajo).
    public static readonly Vector2 Pivote = new Vector2(69.5f / AnchoCelda, 1f / AltoCelda);

    // Cada clip: nombre, primera celda (contando por filas desde arriba a la
    // izquierda), cuadros y si se repite.
    public static readonly (string nombre, int desde, int cuadros, bool bucle)[] Tabla =
    {
        (JefeBlackKnight.ClipDesenvainar, 0, 18, false),
        (JefeBlackKnight.ClipEnvainar, 18, 14, false),
        (JefeBlackKnight.ClipQuieto, 32, 6, true),
        (JefeBlackKnight.ClipQuietoSin, 38, 6, true),
        ("ligero1", 44, 19, false),
        ("ligero2", 63, 8, false),
        ("ligero3", 71, 13, false),
        ("pesado1", 84, 9, false),
        ("pesado2", 93, 9, false),
        ("pesado3", 102, 11, false),
        ("pesado4", 113, 11, false),
        (JefeBlackKnight.ClipRodar, 136, 12, false),
        (JefeBlackKnight.ClipCorrer, 160, 12, true),
        (JefeBlackKnight.ClipSaltar, 197, 6, false),
        (JefeBlackKnight.ClipAire, 203, 1, true),
        (JefeBlackKnight.ClipCaer, 210, 6, true),
        (JefeBlackKnight.ClipAterrizar, 216, 6, false),
        (JefeBlackKnight.ClipArte, 222, 35, false),
        // Espada clavada: el cuadro 31 del arte (agachado, la hoja en el suelo).
        (JefeBlackKnight.ClipClavada, 253, 1, true),
        (JefeBlackKnight.ClipGuardia, 257, 6, false),
        (JefeBlackKnight.ClipContra, 263, 15, false),
        (JefeBlackKnight.ClipFalloGuardia, 278, 3, false),
        (JefeBlackKnight.ClipDolor, 286, 5, false),
        (JefeBlackKnight.ClipMuerte, 291, 9, false),
        (JefeBlackKnight.ClipBuff, 312, 12, false),
    };

    [MenuItem("Warrior/Black Knight/Crear o actualizar", false, 47)]
    public static void Crear()
    {
        HojaLimpia();
        AjustarHoja();
        AjustesBlackKnight aj = Ficha();
        GameObject prefab = Prefab(aj);
        Catalogo(prefab, aj);
        AssetDatabase.SaveAssets();
        Debug.Log($"[BlackKnight] Listo: {RutaHoja}, {RutaFicha}, {RutaPrefab} y su entrada en la sala de pruebas.");
    }

    // ------------------------------------------------------------------ Hoja

    // La copia sin los marcos de color (el original no se toca). Si ya existe, no se rehace.
    private static void HojaLimpia()
    {
        if (File.Exists(RutaHoja)) return;
        if (!File.Exists(Original)) { Debug.LogError("[BlackKnight] Falta el original " + Original); return; }
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.LoadImage(File.ReadAllBytes(Original));
        Color32[] px = tex.GetPixels32();
        int w = tex.width, h = tex.height;
        var nada = new Color32(0, 0, 0, 0);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                // En Unity y = 0 es abajo; los marcos estan en el primer y el ultimo pixel de cada celda.
                int cx = x % AnchoCelda, cy = y % AltoCelda;
                if (cx == 0 || cx == AnchoCelda - 1 || cy == 0 || cy == AltoCelda - 1) px[y * w + x] = nada;
            }
        tex.SetPixels32(px);
        tex.Apply();
        Directory.CreateDirectory(CarpetaSprites);
        File.WriteAllBytes(RutaHoja, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(RutaHoja);
        Debug.Log("[BlackKnight] Hoja limpia creada: " + RutaHoja);
    }

    // Pixel art: filtro Point, sin compresion ni mipmaps y a su tamano real.
    [MenuItem("Warrior/Black Knight/Ajustar sprites", false, 48)]
    public static void AjustarHoja()
    {
        TextureImporter ti = AssetImporter.GetAtPath(RutaHoja) as TextureImporter;
        if (ti == null || Listo(ti)) return;
        ti.textureType = TextureImporterType.Default;
        ti.alphaIsTransparency = true;
        ti.npotScale = TextureImporterNPOTScale.None;
        ti.mipmapEnabled = false;
        ti.filterMode = FilterMode.Point;
        ti.wrapMode = TextureWrapMode.Clamp;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.maxTextureSize = 4096;
        ti.isReadable = false;
        ti.SaveAndReimport();
    }

    private static bool Listo(TextureImporter ti) =>
        ti.textureType == TextureImporterType.Default && ti.npotScale == TextureImporterNPOTScale.None && !ti.mipmapEnabled &&
        ti.filterMode == FilterMode.Point && ti.textureCompression == TextureImporterCompression.Uncompressed && ti.maxTextureSize >= 4096;

    public static bool Comprobar(out string informe)
    {
        TextureImporter ti = AssetImporter.GetAtPath(RutaHoja) as TextureImporter;
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(RutaHoja);
        bool bien = ti != null && tex != null && Listo(ti) && tex.width == AnchoCelda * Columnas && tex.height == AltoCelda * Filas;
        informe = tex != null ? $"hoja {tex.width}x{tex.height} (celdas {AnchoCelda}x{AltoCelda}), Point y sin compresion: {bien}" : "falta la hoja";
        return bien;
    }

    // ------------------------------------------------------------------ Clips

    public static List<AnimadorHoja.Clip> Clips(AjustesBlackKnight aj)
    {
        Texture2D hoja = AssetDatabase.LoadAssetAtPath<Texture2D>(RutaHoja);
        if (hoja == null) Debug.LogError("[BlackKnight] Falta " + RutaHoja);
        return Tabla.Select(t => new AnimadorHoja.Clip
        {
            nombre = t.nombre, hoja = hoja, anchoCelda = AnchoCelda, altoCelda = AltoCelda, fila = 0,
            primero = t.desde, cantidad = t.cuadros, fps = Fps(aj, t.nombre), bucle = t.bucle,
            pivote = Pivote, pixelesPorUnidad = PixelesPorUnidad,
        }).ToList();
    }

    private static float Fps(AjustesBlackKnight aj, string n)
    {
        if (n == JefeBlackKnight.ClipQuieto || n == JefeBlackKnight.ClipQuietoSin) return aj.fpsEspera;
        if (n == JefeBlackKnight.ClipCorrer) return aj.fpsCorrer;
        if (n == JefeBlackKnight.ClipRodar) return aj.fpsRodada;
        if (n.StartsWith("ligero") || n.StartsWith("pesado") || n == JefeBlackKnight.ClipArte || n == JefeBlackKnight.ClipDesenvainar
            || n == JefeBlackKnight.ClipContra || n == JefeBlackKnight.ClipAterrizar) return aj.fpsAtaque;
        return aj.fpsOtros;
    }

    // ------------------------------------------------------------------ Ficha

    private static AjustesBlackKnight Ficha()
    {
        AjustesBlackKnight aj = AssetDatabase.LoadAssetAtPath<AjustesBlackKnight>(RutaFicha);
        if (aj != null) return aj;
        aj = ScriptableObject.CreateInstance<AjustesBlackKnight>();
        Directory.CreateDirectory(Path.GetDirectoryName(RutaFicha));
        AssetDatabase.CreateAsset(aj, RutaFicha);
        return aj;
    }

    // ------------------------------------------------------------------ Prefab

    private static GameObject Prefab(AjustesBlackKnight aj)
    {
        List<AnimadorHoja.Clip> clips = Clips(aj);
        if (AssetDatabase.LoadAssetAtPath<GameObject>(RutaPrefab) != null)
        {
            // Ya existe: solo se rehacen sus clips (lo demas puede estar retocado).
            GameObject raiz = PrefabUtility.LoadPrefabContents(RutaPrefab);
            AnimadorHoja an = raiz.GetComponentInChildren<AnimadorHoja>();
            if (an != null) an.clips = clips;
            PrefabUtility.SaveAsPrefabAsset(raiz, RutaPrefab);
            PrefabUtility.UnloadPrefabContents(raiz);
            return AssetDatabase.LoadAssetAtPath<GameObject>(RutaPrefab);
        }

        // Como los jefes de siempre: capa Enemies, etiqueta Enemy, cuerpo con
        // gravedad y rotacion fija, la imagen en el hijo "Visual".
        GameObject go = new GameObject("JefeBlackKnight");
        go.layer = LayerMask.NameToLayer("Enemies");
        go.tag = "Enemy";
        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 3f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        BoxCollider2D col = go.AddComponent<BoxCollider2D>();
        col.size = aj.cuerpo;
        col.offset = new Vector2(0f, aj.cuerpo.y * 0.5f);
        EnemyHealth salud = go.AddComponent<EnemyHealth>();
        go.AddComponent<HitFlash>();

        GameObject visual = new GameObject("Visual");
        visual.transform.SetParent(go.transform, false);
        SpriteRenderer sr = visual.AddComponent<SpriteRenderer>();
        sr.sortingLayerName = "Player";
        Material flash = AssetDatabase.LoadAssetAtPath<Material>(MaterialFlash);
        if (flash != null) sr.sharedMaterial = flash;
        AnimadorHoja anim = visual.AddComponent<AnimadorHoja>();
        anim.destino = sr;
        anim.clips = clips;
        anim.clipInicial = JefeBlackKnight.ClipQuieto;

        JefeBlackKnight jefe = go.AddComponent<JefeBlackKnight>();
        SerializedObject sj = new SerializedObject(jefe);
        sj.FindProperty("ajustes").objectReferenceValue = aj;
        sj.FindProperty("visual").objectReferenceValue = visual.transform;
        sj.FindProperty("anim").objectReferenceValue = anim;
        sj.ApplyModifiedPropertiesWithoutUndo();
        SerializedObject sh = new SerializedObject(salud);
        sh.FindProperty("showHealthBar").boolValue = false;
        sh.FindProperty("maxHealth").intValue = aj.vidaFase1;
        // Muerto, se queda (deshecho) hasta que la sala lo limpie.
        sh.FindProperty("deathAnimDuration").floatValue = 3600f;
        sh.ApplyModifiedPropertiesWithoutUndo();

        Directory.CreateDirectory(Path.GetDirectoryName(RutaPrefab));
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, RutaPrefab);
        Object.DestroyImmediate(go);
        return prefab;
    }

    // ------------------------------------------------------------------ Sala de pruebas

    public static CatalogoPruebas.Entrada EntradaEn(CatalogoPruebas cat, GameObject prefab) =>
        cat != null && prefab != null ? cat.entradas.FirstOrDefault(e => e != null && e.prefab == prefab) : null;

    private static void Catalogo(GameObject prefab, AjustesBlackKnight aj)
    {
        if (prefab == null) return;
        CrearSalaPruebas.Fichas();
        CatalogoPruebas cat = AssetDatabase.LoadAssetAtPath<CatalogoPruebas>(SalaPruebas.CarpetaDatos + "Catalogo Pruebas.asset");
        if (cat == null) return;
        CatalogoPruebas.Entrada e = EntradaEn(cat, prefab);
        if (e == null)
        {
            e = new CatalogoPruebas.Entrada
            {
                prefab = prefab,
                arena = CatalogoPruebas.TipoArena.Generica,
                titulo = "Jefe de prueba",
                // Una barra por fase (tres): la arena hace la 2; la 3 la pinta el jefe.
                marcaFase = 0f,
                barraPorFase = true,
                nombreFase2 = "",
                // Los textos de fase los pone el propio jefe (de su ficha).
                textoFase2 = "",
                vidaFase2 = 0f,
                luzFase2 = new Color(1f, 0.7f, 0.5f, 1f),
            };
            cat.entradas.Add(e);
        }
        e.nombre = aj.nombreMostrado;
        e.colorBarraFase2 = aj.colorBarraFase2;
        EditorUtility.SetDirty(cat);
    }
}
