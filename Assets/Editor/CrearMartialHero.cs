using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// El Martial Hero (minijefe nuevo, SOLO en la sala de pruebas). Menu Warrior > Martial Hero.
//
// Sus sprites son copias de los originales (Assets/SPRITES PARA NUEVOS
// NIVELES/BOSSES/Boss_MartialHero, que no se tocan) en Assets/Sprites/MartialHero,
// importadas para pixel art: filtro Point, sin compresion ni mipmaps. Se cortan
// por cuadricula de 200x200 (el corte de Unity no se usa) con el anclaje en los
// pies: en todos los cuadros los pies estan en y = 122 desde arriba y el cuerpo
// centrado en x = 94. A 28 px por unidad, como el jugador: mide 1,9 unidades.
//
// "Crear o actualizar" hace:
//   - copia los sprites que falten y ajusta su importacion,
//   - la ficha Assets/Data/Jefes/Ajustes Martial Hero (solo si no existe: lo editado no se pisa),
//   - el prefab Assets/Prefabs/Enemies/Nuevos/JefeMartialHero (si existe, solo rehace sus clips),
//   - su entrada en el catalogo de la sala (si ya esta, solo copia el nombre de la ficha).
// No lo conecta con niveles, Desafios, Bitacora, Bestiario, logros ni Resources.
public static class CrearMartialHero
{
    public const string CarpetaOriginal = "Assets/SPRITES PARA NUEVOS NIVELES/BOSSES/Boss_MartialHero";
    public const string CarpetaSprites = "Assets/Sprites/MartialHero";
    public const string RutaFicha = "Assets/Data/Jefes/Ajustes Martial Hero.asset";
    public const string RutaPrefab = "Assets/Prefabs/Enemies/Nuevos/JefeMartialHero.prefab";
    private const string MaterialFlash = "Assets/Materiales/Enemy_Flash.mat";

    public const int Celda = 200;
    public const float PixelesPorUnidad = 28f;
    // Pies: x = 94 de 200 y y = 122 desde arriba (78 desde abajo).
    public static readonly Vector2 Pivote = new Vector2(94f / Celda, 78f / Celda);
    // Las tiras que usa (las demas no se copian): Take Hit, la silueta, Jump no hacen falta.
    public static readonly string[] Tiras = { "Idle", "Run", "Fall", "Attack1", "Attack2", "Death" };

    [MenuItem("Warrior/Martial Hero/Crear o actualizar", false, 45)]
    public static void Crear()
    {
        CopiarSprites();
        AjustarSprites();
        AjustesMartialHero aj = Ficha();
        GameObject prefab = Prefab(aj);
        Catalogo(prefab, aj);
        AssetDatabase.SaveAssets();
        Debug.Log($"[MartialHero] Listo: {CarpetaSprites}, {RutaFicha}, {RutaPrefab} y su entrada en la sala de pruebas.");
    }

    // ------------------------------------------------------------------ Sprites

    // Copia solo los PNG que falten (los .meta los crea Unity; los originales no se tocan).
    private static void CopiarSprites()
    {
        Directory.CreateDirectory(CarpetaSprites);
        int copiados = 0;
        foreach (string t in Tiras)
        {
            string origen = $"{CarpetaOriginal}/{t}.png", destino = $"{CarpetaSprites}/{t}.png";
            if (File.Exists(destino)) continue;
            if (!File.Exists(origen)) { Debug.LogError("[MartialHero] Falta el original " + origen); continue; }
            File.Copy(origen, destino);
            copiados++;
        }
        if (copiados > 0) AssetDatabase.Refresh();
        Debug.Log($"[MartialHero] Sprites copiados: {copiados}.");
    }

    // Pixel art: filtro Point, sin compresion ni mipmaps y a su tamano real.
    [MenuItem("Warrior/Martial Hero/Ajustar sprites", false, 46)]
    public static void AjustarSprites()
    {
        foreach (string t in Tiras)
        {
            TextureImporter ti = AssetImporter.GetAtPath($"{CarpetaSprites}/{t}.png") as TextureImporter;
            if (ti == null || Listo(ti)) continue;
            ti.textureType = TextureImporterType.Default;
            ti.alphaIsTransparency = true;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.mipmapEnabled = false;
            ti.filterMode = FilterMode.Point;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.maxTextureSize = 2048;
            ti.isReadable = false;
            ti.SaveAndReimport();
        }
    }

    private static bool Listo(TextureImporter ti) =>
        ti.textureType == TextureImporterType.Default && ti.npotScale == TextureImporterNPOTScale.None && !ti.mipmapEnabled &&
        ti.filterMode == FilterMode.Point && ti.textureCompression == TextureImporterCompression.Uncompressed && ti.maxTextureSize == 2048;

    // Comprobacion: todas las copias importadas asi, con cuadros de 200x200 en una fila.
    public static bool Comprobar(out string informe)
    {
        int mal = 0;
        foreach (string t in Tiras)
        {
            string ruta = $"{CarpetaSprites}/{t}.png";
            TextureImporter ti = AssetImporter.GetAtPath(ruta) as TextureImporter;
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(ruta);
            if (ti == null || tex == null || !Listo(ti) || tex.height != Celda || tex.width % Celda != 0) { mal++; Debug.LogWarning("[MartialHero] Mal importado: " + ruta); }
        }
        informe = $"{Tiras.Length} tiras, {mal} mal importadas (Point, sin compresion, cuadros de {Celda}x{Celda})";
        return mal == 0;
    }

    // ------------------------------------------------------------------ Clips

    public static List<AnimadorHoja.Clip> Clips(AjustesMartialHero aj)
    {
        return new List<AnimadorHoja.Clip>
        {
            Clip(JefeMartialHero.ClipQuieto, "Idle", 0, 8, aj.fpsEspera, true),
            Clip(JefeMartialHero.ClipCorrer, "Run", 0, 8, aj.fpsCorrer, true),
            Clip(JefeMartialHero.ClipCaer, "Fall", 0, 2, aj.fpsEspera, true),
            Clip(JefeMartialHero.ClipDesenvaine, "Attack1", 0, 6, aj.fpsAtaque, false),
            // Guardia: agachado con la mano en la empunadura (cuadros 0-2 del desenvaine).
            Clip(JefeMartialHero.ClipGuardia, "Attack1", 0, 3, aj.fpsEspera, true),
            Clip(JefeMartialHero.ClipTajo, "Attack2", 0, 6, aj.fpsAtaque, false),
            // Aturdido: de rodillas (cuadros 2-4 de la muerte); al levantarse, al reves.
            Clip(JefeMartialHero.ClipAturdido, "Death", 2, 3, aj.fpsAturdido, false),
            Clip(JefeMartialHero.ClipLevantarse, "Death", 2, 3, aj.fpsAturdido, false, true),
            Clip(JefeMartialHero.ClipMuerte, "Death", 0, 6, aj.fpsMuerte, false),
        };
    }

    private static AnimadorHoja.Clip Clip(string nombre, string tira, int primero, int cantidad, float fps, bool bucle, bool alReves = false)
    {
        Texture2D hoja = AssetDatabase.LoadAssetAtPath<Texture2D>($"{CarpetaSprites}/{tira}.png");
        if (hoja == null) Debug.LogError($"[MartialHero] Falta {CarpetaSprites}/{tira}.png");
        return new AnimadorHoja.Clip
        {
            nombre = nombre, hoja = hoja, anchoCelda = Celda, altoCelda = Celda, fila = 0,
            primero = primero, cantidad = Mathf.Max(1, cantidad), fps = fps, bucle = bucle, alReves = alReves,
            pivote = Pivote, pixelesPorUnidad = PixelesPorUnidad,
        };
    }

    // ------------------------------------------------------------------ Ficha

    private static AjustesMartialHero Ficha()
    {
        AjustesMartialHero aj = AssetDatabase.LoadAssetAtPath<AjustesMartialHero>(RutaFicha);
        if (aj != null) return aj;
        aj = ScriptableObject.CreateInstance<AjustesMartialHero>();
        Directory.CreateDirectory(Path.GetDirectoryName(RutaFicha));
        AssetDatabase.CreateAsset(aj, RutaFicha);
        return aj;
    }

    // ------------------------------------------------------------------ Prefab

    private static GameObject Prefab(AjustesMartialHero aj)
    {
        List<AnimadorHoja.Clip> clips = Clips(aj);
        GameObject existente = AssetDatabase.LoadAssetAtPath<GameObject>(RutaPrefab);
        if (existente != null)
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
        GameObject go = new GameObject("JefeMartialHero");
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
        anim.clipInicial = JefeMartialHero.ClipQuieto;

        JefeMartialHero jefe = go.AddComponent<JefeMartialHero>();
        SerializedObject sj = new SerializedObject(jefe);
        sj.FindProperty("ajustes").objectReferenceValue = aj;
        sj.FindProperty("visual").objectReferenceValue = visual.transform;
        sj.FindProperty("anim").objectReferenceValue = anim;
        sj.ApplyModifiedPropertiesWithoutUndo();
        SerializedObject sh = new SerializedObject(salud);
        sh.FindProperty("showHealthBar").boolValue = false;
        sh.FindProperty("maxHealth").intValue = aj.vida;
        // Muerto, se queda sentado en la arena hasta que la sala lo limpie.
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

    private static void Catalogo(GameObject prefab, AjustesMartialHero aj)
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
                titulo = "Minijefe de prueba",
                marcaFase = 0f,
                barraPorFase = false,
                textoFase2 = "",
                vidaFase2 = 0f,
            };
            cat.entradas.Add(e);
        }
        // El nombre de la barra es el de la ficha.
        e.nombre = aj.nombreMostrado;
        EditorUtility.SetDirty(cat);
    }
}
