using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;

// The Bringer of Death (jefe nuevo, SOLO en la sala de pruebas). Menu Warrior > Bringer of Death.
//
// Sus sprites y los efectos que usa son COPIAS (los originales de
// Assets/SPRITES PARA NUEVOS NIVELES no se tocan) en Assets/Sprites/BringerOfDeath,
// importadas para pixel art (Point, sin compresion ni mipmaps):
//   - el cuerpo: cuadros sueltos de 140x93, pies en la ultima fila y el cuerpo en
//     x = 105 (mira a la izquierda). A 22 px por unidad: mide unos 2,5 unidades.
//   - su hechizo propio (portal y mano), y los packs del Necromante y del
//     Starcaller, que no usaba ningun otro jefe.
//
// "Crear o actualizar" hace:
//   - copia lo que falte y ajusta la importacion,
//   - la ficha Assets/Data/Jefes/Ajustes Bringer of Death (solo si no existe),
//   - el prefab Assets/Prefabs/Enemies/Nuevos/JefeBringerOfDeath (si existe, solo rehace sus clips),
//   - su entrada en el catalogo de la sala (si ya esta, no la toca).
// No lo conecta con niveles, Desafios, Bitacora, Bestiario, logros, Resources ni guardado.
public static class CrearBringerOfDeath
{
    public const string CarpetaJefe = "Assets/SPRITES PARA NUEVOS NIVELES/BOSSES/Boss_BringerOfDeath/Individual Sprite";
    public const string CarpetaNecro = "Assets/SPRITES PARA NUEVOS NIVELES/EFECTOS DE ATAQUE DE LOS ENEMIGOS/Efecto_Necromante";
    public const string CarpetaStar = "Assets/SPRITES PARA NUEVOS NIVELES/EFECTOS DE ATAQUE DE LOS ENEMIGOS/Efecto_Starcaller";
    public const string CarpetaSprites = "Assets/Sprites/BringerOfDeath";
    public const string RutaFicha = "Assets/Data/Jefes/Ajustes Bringer of Death.asset";
    public const string RutaPrefab = "Assets/Prefabs/Enemies/Nuevos/JefeBringerOfDeath.prefab";
    public const string NombreCatalogo = "Bringer of Death";
    private const string MaterialFlash = "Assets/Materiales/Enemy_Flash.mat";

    public const float PixelesCuerpo = 22f;
    // Pies en la ultima fila y el cuerpo en x = 105 de 140.
    public static readonly Vector2 PivoteCuerpo = new Vector2(105f / 140f, 0f);
    // La mano: columna en x = 68 de 140, tocando el suelo.
    public static readonly Vector2 PivoteMano = new Vector2(68f / 140f, 0f);

    // Animaciones del cuerpo que se copian (carpeta original -> cuadros).
    public static readonly string[] Cuerpo = { "Idle", "Walk", "Attack", "Cast", "Hurt", "Death", "Spell" };
    // Efectos: nombre de la copia -> carpeta de cuadros original.
    public static readonly (string nombre, string carpeta)[] Efectos =
    {
        ("Necro1_Anillo", CarpetaNecro + "/VFX 1/Frames"),
        ("Necro2_MediaLuna", CarpetaNecro + "/VFX 2/Frames"),
        ("Necro3_Ola", CarpetaNecro + "/VFX 3/Frames"),
        ("Necro4_Espiritu", CarpetaNecro + "/VFX 4/Frames"),
        ("Star1_Estrellas", CarpetaStar + "/VFX 1/Frames"),
        ("Star2_Cometa", CarpetaStar + "/VFX 2/Frames"),
        ("Star3_Esfera", CarpetaStar + "/VFX 3/Frames"),
    };

    [MenuItem("Warrior/Bringer of Death/Crear o actualizar", false, 47)]
    public static void Crear()
    {
        CopiarSprites();
        AjustarSprites();
        AjustesBringer aj = Ficha();
        GameObject prefab = Prefab(aj);
        Catalogo(prefab);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Bringer] Listo: {CarpetaSprites}, {RutaFicha}, {RutaPrefab} y su entrada en la sala de pruebas.");
    }

    // ------------------------------------------------------------------ Sprites

    private static int Numero(string ruta)
    {
        var m = System.Text.RegularExpressions.Regex.Matches(Path.GetFileNameWithoutExtension(ruta), @"\d+");
        return m.Count > 0 ? int.Parse(m[m.Count - 1].Value) : 0;
    }

    private static IEnumerable<string> Png(string carpeta) =>
        Directory.Exists(carpeta) ? Directory.GetFiles(carpeta, "*.png").OrderBy(Numero) : Enumerable.Empty<string>();

    // Copia solo los PNG que falten, con nombre corto y su numero (1, 2, 3...).
    private static void CopiarSprites()
    {
        int copiados = 0;
        void Copiar(string origen, string destino)
        {
            Directory.CreateDirectory(destino);
            foreach (string f in Png(origen))
            {
                string d = $"{destino}/{Numero(f)}.png";
                if (File.Exists(d)) continue;
                File.Copy(f, d);
                copiados++;
            }
        }
        foreach (string a in Cuerpo) Copiar($"{CarpetaJefe}/{a}", $"{CarpetaSprites}/{a}");
        foreach (var e in Efectos) Copiar(e.carpeta, $"{CarpetaSprites}/Efectos/{e.nombre}");
        if (copiados > 0) AssetDatabase.Refresh();
        Debug.Log($"[Bringer] Sprites copiados: {copiados}.");
    }

    [MenuItem("Warrior/Bringer of Death/Ajustar sprites", false, 48)]
    public static void AjustarSprites()
    {
        int cambiados = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { CarpetaSprites }))
        {
            TextureImporter ti = AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) as TextureImporter;
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
            cambiados++;
        }
        Debug.Log($"[Bringer] Sprites ajustados: {cambiados}.");
    }

    private static bool Listo(TextureImporter ti) =>
        ti.textureType == TextureImporterType.Default && ti.npotScale == TextureImporterNPOTScale.None && !ti.mipmapEnabled &&
        ti.filterMode == FilterMode.Point && ti.textureCompression == TextureImporterCompression.Uncompressed && ti.maxTextureSize == 2048;

    public static bool Comprobar(out string informe)
    {
        int total = 0, mal = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { CarpetaSprites }))
        {
            string ruta = AssetDatabase.GUIDToAssetPath(guid);
            TextureImporter ti = AssetImporter.GetAtPath(ruta) as TextureImporter;
            total++;
            if (ti == null || !Listo(ti)) { mal++; Debug.LogWarning("[Bringer] Mal importado: " + ruta); }
        }
        informe = $"{total} texturas, {mal} mal importadas (Point, sin compresion ni mipmaps)";
        return total > 0 && mal == 0;
    }

    // ------------------------------------------------------------------ Clips

    private static Texture2D[] Cuadros(string carpeta, int desde, int hasta)
    {
        var l = new List<Texture2D>();
        for (int i = desde; i <= hasta; i++)
        {
            Texture2D t = AssetDatabase.LoadAssetAtPath<Texture2D>($"{carpeta}/{i}.png");
            if (t == null) Debug.LogError($"[Bringer] Falta {carpeta}/{i}.png");
            else l.Add(t);
        }
        return l.ToArray();
    }

    private static AnimadorHoja.Clip Clip(string nombre, string carpeta, int desde, int hasta, float fps, bool bucle, Vector2 pivote,
                                          float ppu, bool alReves = false)
    {
        Texture2D[] tex = Cuadros(carpeta, desde, hasta);
        if (alReves) System.Array.Reverse(tex);
        return new AnimadorHoja.Clip
        {
            nombre = nombre, fotogramas = tex, cantidad = tex.Length, fps = fps, bucle = bucle,
            pivote = pivote, pixelesPorUnidad = ppu, alReves = false,
        };
    }

    private static string C(string a) => $"{CarpetaSprites}/{a}";
    private static string E(string a) => $"{CarpetaSprites}/Efectos/{a}";

    public static List<AnimadorHoja.Clip> ClipsCuerpo(AjustesBringer aj)
    {
        float f = aj.fps;
        return new List<AnimadorHoja.Clip>
        {
            Clip(JefeBringer.ClipQuieto, C("Idle"), 1, 8, f, true, PivoteCuerpo, PixelesCuerpo),
            Clip(JefeBringer.ClipAndar, C("Walk"), 1, 8, f, true, PivoteCuerpo, PixelesCuerpo),
            Clip(JefeBringer.ClipGuadana, C("Attack"), 1, 10, f, false, PivoteCuerpo, PixelesCuerpo),
            // Alza el brazo (1-5); con el anillo morado sobre la mano (6-9), en bucle.
            Clip(JefeBringer.ClipConjurar, C("Cast"), 1, 5, f, false, PivoteCuerpo, PixelesCuerpo),
            Clip(JefeBringer.ClipConjurarBucle, C("Cast"), 6, 9, f, true, PivoteCuerpo, PixelesCuerpo),
            Clip(JefeBringer.ClipGolpeado, C("Hurt"), 1, 3, 8f, false, PivoteCuerpo, PixelesCuerpo),
            Clip(JefeBringer.ClipMuerte, C("Death"), 1, 10, 8f, false, PivoteCuerpo, PixelesCuerpo),
            // Se deshace en humo (la muerte deprisa) y vuelve (al reves).
            Clip(JefeBringer.ClipDesvanecer, C("Death"), 1, 10, 16f, false, PivoteCuerpo, PixelesCuerpo),
            Clip(JefeBringer.ClipAparecer, C("Death"), 1, 10, 16f, false, PivoteCuerpo, PixelesCuerpo, true),
        };
    }

    // Los efectos de los hechizos, en el componente del jefe.
    public static void PonerEfectos(JefeBringer j, AjustesBringer aj)
    {
        float h = aj.fpsHechizo;
        j.fxPortal = Clip("portal", C("Spell"), 1, 4, 10f, true, PivoteMano, PixelesCuerpo);
        j.fxMano = Clip("mano", C("Spell"), 5, 16, 12f, false, PivoteMano, PixelesCuerpo);
        Vector2 suelo8 = new Vector2(0.5f, 8f / 128f);
        j.fxAnillo = Clip("anillo", E("Necro1_Anillo"), 1, 3, 10f, true, suelo8, 36f);
        j.fxEstallido = Clip("estallido", E("Necro1_Anillo"), 4, 9, h, false, suelo8, 36f);
        j.fxOla = Clip("ola", E("Necro3_Ola"), 1, 6, h, false, new Vector2(6f / 128f, 17f / 128f), 36f);
        j.fxMediaLuna = Clip("mediaLuna", E("Necro2_MediaLuna"), 2, 4, 12f, true, new Vector2(0.5f, 0.45f), 48f);
        j.fxEspiritu = Clip("espiritu", E("Necro4_Espiritu"), 4, 9, 10f, true, new Vector2(0.45f, 0.5f), 40f);
        j.fxEspirituFin = Clip("espirituFin", E("Necro4_Espiritu"), 10, 15, 14f, false, new Vector2(0.45f, 0.5f), 40f);
        j.fxMarca = Clip("marca", E("Star3_Esfera"), 1, 2, 8f, true, new Vector2(0.53f, 14f / 128f), 40f);
        j.fxCometa = Clip("cometa", E("Star2_Cometa"), 3, 4, 12f, true, new Vector2(0.5f, 0.5f), 36f);
        j.fxEstrellas = Clip("estrellas", E("Star1_Estrellas"), 1, 7, h, false, new Vector2(0.5f, 8f / 128f), 36f);
        j.fxEsferaAbrir = Clip("esferaAbrir", E("Star3_Esfera"), 1, 5, h, false, new Vector2(0.5f, 14f / 128f), 32f);
        j.fxEsferaBucle = Clip("esferaBucle", E("Star3_Esfera"), 6, 9, 10f, true, new Vector2(0.5f, 14f / 128f), 32f);
        j.fxEsferaFin = Clip("esferaFin", E("Star3_Esfera"), 10, 15, h, false, new Vector2(0.5f, 14f / 128f), 32f);
    }

    // ------------------------------------------------------------------ Ficha

    private static AjustesBringer Ficha()
    {
        AjustesBringer aj = AssetDatabase.LoadAssetAtPath<AjustesBringer>(RutaFicha);
        if (aj != null) return aj;
        aj = ScriptableObject.CreateInstance<AjustesBringer>();
        Directory.CreateDirectory(Path.GetDirectoryName(RutaFicha));
        AssetDatabase.CreateAsset(aj, RutaFicha);
        return aj;
    }

    // ------------------------------------------------------------------ Prefab

    private static GameObject Prefab(AjustesBringer aj)
    {
        List<AnimadorHoja.Clip> clips = ClipsCuerpo(aj);
        if (AssetDatabase.LoadAssetAtPath<GameObject>(RutaPrefab) != null)
        {
            // Ya existe: solo se rehacen sus clips (lo demas puede estar retocado).
            GameObject raiz = PrefabUtility.LoadPrefabContents(RutaPrefab);
            AnimadorHoja an = raiz.GetComponentInChildren<AnimadorHoja>();
            if (an != null) an.clips = clips;
            JefeBringer je = raiz.GetComponent<JefeBringer>();
            if (je != null) PonerEfectos(je, aj);
            PrefabUtility.SaveAsPrefabAsset(raiz, RutaPrefab);
            PrefabUtility.UnloadPrefabContents(raiz);
            return AssetDatabase.LoadAssetAtPath<GameObject>(RutaPrefab);
        }

        // Como los jefes de siempre: capa Enemies, etiqueta Enemy, cuerpo con
        // gravedad y rotacion fija, la imagen en el hijo "Visual".
        GameObject go = new GameObject("JefeBringerOfDeath");
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
        go.AddComponent<CinemachineImpulseSource>();

        GameObject visual = new GameObject("Visual");
        visual.transform.SetParent(go.transform, false);
        SpriteRenderer sr = visual.AddComponent<SpriteRenderer>();
        sr.sortingLayerName = "Player";
        Material flash = AssetDatabase.LoadAssetAtPath<Material>(MaterialFlash);
        if (flash != null) sr.sharedMaterial = flash;
        AnimadorHoja anim = visual.AddComponent<AnimadorHoja>();
        anim.destino = sr;
        anim.clips = clips;
        anim.clipInicial = JefeBringer.ClipQuieto;

        JefeBringer jefe = go.AddComponent<JefeBringer>();
        PonerEfectos(jefe, aj);
        SerializedObject sj = new SerializedObject(jefe);
        sj.FindProperty("ajustes").objectReferenceValue = aj;
        sj.FindProperty("visual").objectReferenceValue = visual.transform;
        sj.FindProperty("anim").objectReferenceValue = anim;
        // Los sprites miran a la izquierda.
        sj.FindProperty("spriteMiraDerecha").boolValue = false;
        sj.ApplyModifiedPropertiesWithoutUndo();
        SerializedObject sh = new SerializedObject(salud);
        sh.FindProperty("showHealthBar").boolValue = false;
        sh.FindProperty("maxHealth").intValue = aj.vida;
        // Muerto, el ojo rojo se queda en la arena hasta que la sala lo limpie.
        sh.FindProperty("deathAnimDuration").floatValue = 3600f;
        sh.ApplyModifiedPropertiesWithoutUndo();

        Directory.CreateDirectory(Path.GetDirectoryName(RutaPrefab));
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, RutaPrefab);
        Object.DestroyImmediate(go);
        return prefab;
    }

    // ------------------------------------------------------------------ Sala de pruebas

    // Una sola barra con la marca de la fase 2 en la mitad.
    private static void Catalogo(GameObject prefab)
    {
        if (prefab == null) return;
        CrearSalaPruebas.Fichas();
        CatalogoPruebas cat = AssetDatabase.LoadAssetAtPath<CatalogoPruebas>(SalaPruebas.CarpetaDatos + "Catalogo Pruebas.asset");
        if (cat == null || cat.entradas.Any(e => e != null && e.prefab == prefab)) return;
        cat.entradas.Add(new CatalogoPruebas.Entrada
        {
            nombre = NombreCatalogo,
            prefab = prefab,
            arena = CatalogoPruebas.TipoArena.Generica,
            titulo = "Jefe de prueba",
            marcaFase = 0.5f,
            barraPorFase = false,
            nombreFase2 = "",
            textoFase2 = "La muerte se impacienta",
            luzFase2 = new Color(0.7f, 0.5f, 1f, 1f),
            particulasFase2A = new Color(0.55f, 0.25f, 0.95f, 0.9f),
            particulasFase2B = new Color(0.3f, 1f, 0.7f, 0.7f),
            particulasCaen = false,
            vidaFase2 = 0.49f,
        });
        EditorUtility.SetDirty(cat);
    }
}
