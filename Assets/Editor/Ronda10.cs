using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// Decima ronda: The Blind Huntress, el jefe secreto de los Desafios.
//   - Sprites: cada hoja numerada ("1 - Idle"... "15 - death", 240x128) se parte
//     en dos capas: cuerpo y efecto blanco (para tenirlo). Pixel art nitido a 28
//     pixeles por unidad. En la estocada y el cruce el pivote sigue al cuerpo
//     (ella se desplaza dentro del dibujo): el movimiento lo pone el codigo.
//   - Caja de dano de cada cuadro = lo que ocupa el efecto en ese cuadro.
//   - Fondo: el bosque Gloomwood pixelado al tamano de pixel del personaje.
//   - Escena "Bosque Cazadora" (solo desde Desafios), prefab, ajustes, sonidos,
//     ficha del desafio (secreta) y sus dos logros.
// Lo editable (ajustes, ficha, logros, sonidos) NO se pisa si ya existe. La
// escena tampoco: para rehacerla, borrala antes (o CAZADORA_REGENERAR=1).
// Warrior > Actualizar > Ronda 10 (o ActualizarProyecto.Ronda10 por linea de comandos).
public static class Ronda10
{
    private const string Pack = "Assets/SPRITES PARA NUEVOS NIVELES/";
    private const string Hojas = Pack + "BOSSES/Boss_BlindHuntress/Sprite Sheet/";
    private const string Gloom = Pack + "BACKGROUND/Fondo_BosqueGloomwood/PNG Files/";
    private const string Gandalf = Pack + "MAPAS/Mapa_BosqueGandalf/";
    private const string Musica = Pack + "SONIDOS/Sonidos_MusicaBosses/";
    private const string Iconos = Pack + "ICONOS/Iconos_RPGPack/";
    private const string Propia = "Assets/Sprites/Cazadora/";
    private const string Datos = "Assets/Data/Jefes/";
    private const string RutaAjustes = Datos + "Ajustes Cazadora.asset";
    private const string RutaSprites = Datos + "Sprites Cazadora.asset";
    private const string RutaSonidos = "Assets/Data/Sonidos/Sonidos Jefe Cazadora.asset";
    private const string RutaPrefab = "Assets/Prefabs/Enemies/Bosque/CazadoraCiega.prefab";
    public const string Escena = "Assets/Scenes/Bosque Cazadora.unity";
    private const string Plantilla = "Assets/Scenes/Nivel Nieve.unity";
    private const string CarpetaTiles = "Assets/Tiles/Bosque";

    private const int Celda = 240, AltoCelda = 128, PieY = 48;
    private const float PPU = 28f;

    // Hoja, nombre del clip, cuadros por segundo, bucle, pivote que sigue al
    // cuerpo, y si se separa el blanco en la capa de efecto.
    private static readonly (string hoja, string clip, float fps, bool bucle, bool centrar, bool separar)[] Tabla =
    {
        ("1 - Idle", "quieto", 10f, true, false, true),
        ("2 - Run", "correr", 12f, true, false, true),
        ("3 - jump", "salto", 12f, false, false, true),
        ("4 - mid-air", "aire", 1f, true, false, true),
        ("5 - fall", "caer", 10f, false, false, true),
        ("6 - dash", "dash", 14f, true, false, true),
        ("7 - jump-up-attack", "tajoArribaAire", 14f, false, false, true),
        ("8 - jump-down-attack", "tajoAbajo", 14f, false, false, true),
        ("9 - idle-up-attack", "tajoArriba", 14f, false, false, true),
        ("10 - attack 1", "barrido", 14f, false, false, true),
        ("11 - attack 3", "luna", 14f, false, false, true),
        ("12 - dash attack", "estocada", 14f, false, true, true),
        ("13 - spetial dash", "cruce", 14f, false, true, true),
        ("14 - hit", "golpe", 1f, false, false, false),
        ("15 - death", "muerte", 12f, false, false, true),
    };

    [MenuItem("Warrior/Actualizar/Ronda 10 (Cazadora)")]
    public static void Todo()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        foreach (string c in new[] { Propia + "Cuerpo", Propia + "Efecto", Propia + "Fondo", Datos, "Assets/Prefabs/Enemies/Bosque", CarpetaTiles, "Assets/Sprites/Desafios" })
            Directory.CreateDirectory(c);
        AssetDatabase.Refresh();

        SpritesCazadora hojas = PrepararSprites();
        Sprite menu = ImagenMenu();
        Formas();
        Materiales();
        AjustesCazadora aj = Ajustes();
        SonidosAcciones son = Sonidos();
        SonidoLibreria();
        GameObject prefab = Prefab(hojas, aj, son);
        Fondo();
        Ficha(menu);
        Logros();
        AssetDatabase.SaveAssets();
        if (!File.Exists(Escena) || System.Environment.GetEnvironmentVariable("CAZADORA_REGENERAR") == "1") CrearEscena(prefab, aj);
        AnadirABuild();
        AssetDatabase.SaveAssets();
        Debug.Log("[Ronda10] Listo.");
    }

    // ------------------------------------------------------------------ Sprites

    private static SpritesCazadora PrepararSprites()
    {
        SpritesCazadora sc = AssetDatabase.LoadAssetAtPath<SpritesCazadora>(RutaSprites);
        bool nuevo = sc == null;
        if (nuevo) sc = ScriptableObject.CreateInstance<SpritesCazadora>();
        sc.clips = new List<SpritesCazadora.Clip>();
        sc.pixelesPorUnidad = PPU;
        float referencia = -1f;

        foreach (var e in Tabla)
        {
            string origen = Hojas + e.hoja + ".png";
            if (!File.Exists(origen)) { Debug.LogError("[Ronda10] Falta " + origen); continue; }
            ConfigurarRecursosRPG.PrepararPixel(origen, PPU);
            Texture2D t = Leer(origen);
            int w = t.width, h = t.height, cuadros = w / Celda;
            Color32[] px = t.GetPixels32();
            Color32[] pc = new Color32[px.Length], pe = new Color32[px.Length];
            for (int i = 0; i < px.Length; i++)
            {
                Color32 c = px[i];
                if (c.a == 0) continue;
                bool blanco = e.separar && c.r >= 200 && c.g >= 200 && c.b >= 200;
                if (blanco) pe[i] = c; else pc[i] = c;
            }

            // Pivote de cada cuadro: los pies (y = 48) y, en horizontal, el centro
            // del cuerpo en reposo (o el del cuadro, si el cuerpo se desplaza).
            float[] piv = new float[cuadros];
            RectInt[] cajaFx = new RectInt[cuadros];
            int[] cuentaFx = new int[cuadros];
            for (int k = 0; k < cuadros; k++)
            {
                Limites(pc, w, k, out RectInt cuerpo, out int nc);
                Limites(pe, w, k, out cajaFx[k], out cuentaFx[k]);
                float centro = nc > 0 ? (cuerpo.xMin + cuerpo.xMax) * 0.5f - k * Celda : Celda * 0.5f;
                piv[k] = centro;
            }
            if (e.clip == "quieto") referencia = Mathf.Round(piv.Average());
            for (int k = 0; k < cuadros; k++) piv[k] = e.centrar ? Mathf.Round(piv[k]) : (referencia > 0f ? referencia : Mathf.Round(piv[k]));

            string rc = Propia + "Cuerpo/" + e.clip + ".png", re = Propia + "Efecto/" + e.clip + ".png";
            Guardar(pc, w, h, rc);
            Guardar(pe, w, h, re);
            Cortar(rc, cuadros, piv, e.clip);
            Cortar(re, cuadros, piv, e.clip + "_fx");

            var clip = new SpritesCazadora.Clip
            {
                nombre = e.clip, fps = e.fps, bucle = e.bucle,
                cuerpo = Sprites(rc), efecto = Sprites(re),
                cajas = new Rect[cuadros], activos = new bool[cuadros],
            };
            for (int k = 0; k < cuadros; k++)
            {
                if (cuentaFx[k] == 0) continue;
                RectInt b = cajaFx[k];
                float x0 = b.xMin - k * Celda;
                clip.cajas[k] = new Rect((x0 - piv[k]) / PPU, (b.yMin - PieY) / PPU, b.width / PPU, b.height / PPU);
                clip.activos[k] = cuentaFx[k] >= 150;
            }
            sc.clips.Add(clip);
            if (e.clip == "golpe" && clip.cuerpo.Length > 0) sc.golpe = clip.cuerpo[0];
            Debug.Log($"[Ronda10] {e.clip}: {cuadros} cuadros, activos [{string.Join(",", Enumerable.Range(0, cuadros).Where(k => clip.activos[k]))}] pivotes [{string.Join(",", piv)}]");
        }
        if (nuevo) AssetDatabase.CreateAsset(sc, RutaSprites);
        EditorUtility.SetDirty(sc);
        return sc;
    }

    private static Texture2D Leer(string ruta)
    {
        Texture2D t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        t.LoadImage(File.ReadAllBytes(ruta));
        return t;
    }

    // Limites de lo pintado en el cuadro k (en pixeles de la hoja, desde abajo).
    private static void Limites(Color32[] px, int ancho, int k, out RectInt r, out int cuenta)
    {
        int alto = px.Length / ancho;
        int x0 = int.MaxValue, x1 = -1, y0 = int.MaxValue, y1 = -1;
        cuenta = 0;
        for (int y = 0; y < alto; y++)
        for (int x = k * Celda; x < (k + 1) * Celda; x++)
        {
            if (px[y * ancho + x].a == 0) continue;
            cuenta++;
            if (x < x0) x0 = x; if (x > x1) x1 = x;
            if (y < y0) y0 = y; if (y > y1) y1 = y;
        }
        r = cuenta > 0 ? new RectInt(x0, y0, x1 - x0 + 1, y1 - y0 + 1) : new RectInt();
    }

    private static void Guardar(Color32[] px, int w, int h, string ruta)
    {
        Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, false);
        t.SetPixels32(px);
        t.Apply();
        File.WriteAllBytes(ruta, t.EncodeToPNG());
        AssetDatabase.ImportAsset(ruta, ImportAssetOptions.ForceUpdate);
    }

    // Corta la hoja en cuadros de 240x128 con el pivote de cada uno.
    private static void Cortar(string ruta, int cuadros, float[] piv, string nombre)
    {
        TextureImporter ti = (TextureImporter)AssetImporter.GetAtPath(ruta);
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Multiple;
        ti.spritePixelsPerUnit = PPU;
        ti.filterMode = FilterMode.Point;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
        ti.maxTextureSize = 4096;
        ti.wrapMode = TextureWrapMode.Clamp;
        TextureImporterSettings st = new TextureImporterSettings();
        ti.ReadTextureSettings(st);
        st.spriteMeshType = SpriteMeshType.FullRect;
        st.spriteExtrude = 0;
        ti.SetTextureSettings(st);
        var metas = new List<SpriteMetaData>();
        for (int k = 0; k < cuadros; k++)
            metas.Add(new SpriteMetaData
            {
                name = $"{nombre}_{k:00}", rect = new Rect(k * Celda, 0, Celda, AltoCelda),
                alignment = (int)SpriteAlignment.Custom, pivot = new Vector2(piv[k] / Celda, (float)PieY / AltoCelda),
            });
#pragma warning disable 618
        ti.spritesheet = metas.ToArray();
#pragma warning restore 618
        ti.SaveAndReimport();
    }

    private static Sprite[] Sprites(string ruta) =>
        AssetDatabase.LoadAllAssetsAtPath(ruta).OfType<Sprite>().OrderBy(s => s.name).ToArray();

    // Imagen para la ficha del menu: el primer cuadro de Idle, recortado.
    private static Sprite ImagenMenu()
    {
        const string destino = "Assets/Sprites/Desafios/Jefe_BlindHuntress.png";
        Texture2D t = Leer(Hojas + "1 - Idle.png");
        Color32[] px = t.GetPixels32();
        Limites(px, t.width, 0, out RectInt r, out int n);
        if (n == 0) return null;
        const int m = 2;
        RectInt c = new RectInt(r.xMin - m, r.yMin - m, r.width + m * 2, r.height + m * 2);
        Texture2D o = new Texture2D(c.width, c.height, TextureFormat.RGBA32, false);
        o.SetPixels(t.GetPixels(c.x, c.y, c.width, c.height));
        o.Apply();
        File.WriteAllBytes(destino, o.EncodeToPNG());
        AssetDatabase.ImportAsset(destino, ImportAssetOptions.ForceUpdate);
        TextureImporter ti = (TextureImporter)AssetImporter.GetAtPath(destino);
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.filterMode = FilterMode.Point;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.mipmapEnabled = false;
        ti.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(destino);
    }

    // Sombra (elipse) y burbuja del escudo, como imagenes del proyecto.
    private static void Formas()
    {
        Dibujar(Propia + "sombra.png", 24, 8, 24f, true, (x, y) =>
        {
            float dx = (x + 0.5f - 12f) / 12f, dy = (y + 0.5f - 4f) / 4f;
            float d = dx * dx + dy * dy;
            return new Color(0f, 0f, 0f, d <= 1f ? (d < 0.5f ? 1f : 0.6f) : 0f);
        });
        Dibujar(Propia + "burbuja.png", 64, 64, 64f, false, (x, y) =>
        {
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(32f, 32f)) / 32f;
            float aro = Mathf.Clamp01(1f - Mathf.Abs(d - 0.9f) / 0.08f);
            float dentro = d < 0.92f ? 0.25f * d * d : 0f;
            return new Color(1f, 1f, 1f, Mathf.Clamp01(aro + dentro));
        });
    }

    private static void Dibujar(string ruta, int w, int h, float ppu, bool pixel, System.Func<int, int, Color> f)
    {
        Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color[] px = new Color[w * h];
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) px[y * w + x] = f(x, y);
        t.SetPixels(px);
        t.Apply();
        File.WriteAllBytes(ruta, t.EncodeToPNG());
        AssetDatabase.ImportAsset(ruta, ImportAssetOptions.ForceUpdate);
        TextureImporter ti = (TextureImporter)AssetImporter.GetAtPath(ruta);
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = ppu;
        ti.filterMode = pixel ? FilterMode.Point : FilterMode.Bilinear;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.mipmapEnabled = false;
        ti.SaveAndReimport();
    }

    // ------------------------------------------------------------------ Materiales

    private static Material mSinLuz, mContorno, mIlusion, mFlash;

    private static void Materiales()
    {
        mFlash = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materiales/Enemy_Flash.mat");
        mSinLuz = Material("Assets/Materiales/Cazadora_SinLuz.mat", "Universal Render Pipeline/2D/Sprite-Unlit-Default", null);
        mContorno = Material("Assets/Materiales/Cazadora_Contorno.mat", "Sprites/Aura", m =>
        {
            m.SetColor("_AuraColor", new Color(0.7f, 1f, 0.8f, 1f));
            m.SetFloat("_Width", 1f);
            m.SetFloat("_Amount", 0.75f);
            m.SetFloat("_Inner", 0.12f);
        });
        mIlusion = Material("Assets/Materiales/Cazadora_Ilusion.mat", "Sprites/Aura", m =>
        {
            m.SetColor("_AuraColor", new Color(0.55f, 0.75f, 1f, 1f));
            m.SetFloat("_Width", 1f);
            m.SetFloat("_Amount", 0.8f);
            m.SetFloat("_Inner", 0.2f);
        });
    }

    private static Material Material(string ruta, string shader, System.Action<Material> ajustar)
    {
        Material m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (m != null) return m;
        Shader s = Shader.Find(shader);
        if (s == null) { Debug.LogWarning("[Ronda10] No encuentro el shader " + shader); return null; }
        m = new Material(s);
        ajustar?.Invoke(m);
        AssetDatabase.CreateAsset(m, ruta);
        return m;
    }

    // ------------------------------------------------------------------ Ajustes y sonidos

    private static AjustesCazadora Ajustes()
    {
        AjustesCazadora a = AssetDatabase.LoadAssetAtPath<AjustesCazadora>(RutaAjustes);
        bool nuevo = a == null;
        if (nuevo) a = ScriptableObject.CreateInstance<AjustesCazadora>();
        (string ruta, float vol, float inicio)[] musica =
        {
            (Musica + "Sonidos_BossFightPack/02 - The Hooded March - The hooded March.ogg", 0.6f, 0f),
            (Musica + "Sonidos_BossFightPack/05 - one mistep - W-one Mistep.ogg", 0.62f, 0f),
            (Musica + "Sonidos_BossFightPack/07 - Skeletron x - Skelatoron X.ogg", 0.62f, 18f),
        };
        for (int i = 0; i < 3; i++)
        {
            Streaming(musica[i].ruta);
            if (a.fases[i].musica == null) a.fases[i].musica = new ConfigNivel.MusicaFase();
            if (a.fases[i].musica.pista != null) continue;
            a.fases[i].musica.pista = AssetDatabase.LoadAssetAtPath<AudioClip>(musica[i].ruta);
            a.fases[i].musica.volumen = musica[i].vol;
            a.fases[i].musica.inicio = musica[i].inicio;
        }
        if (a.ambiente == null) a.ambiente = Clip("Forest Night");
        if (a.viento == null) a.viento = Clip("wind_spell_cast_air_long_loop_01");
        if (nuevo) AssetDatabase.CreateAsset(a, RutaAjustes);
        EditorUtility.SetDirty(a);
        return a;
    }

    private static void Streaming(string ruta)
    {
        AudioImporter ai = AssetImporter.GetAtPath(ruta) as AudioImporter;
        if (ai == null) { Debug.LogWarning("[Ronda10] No encuentro la musica " + ruta); return; }
        AudioImporterSampleSettings s = ai.defaultSampleSettings;
        if (s.loadType == AudioClipLoadType.Streaming) return;
        s.loadType = AudioClipLoadType.Streaming;
        ai.defaultSampleSettings = s;
        ai.SaveAndReimport();
    }

    // Un sonido del proyecto por su nombre de archivo (el .ogg de 48 kHz si hay varios).
    private static AudioClip Clip(string nombre)
    {
        string mejor = null;
        int puntos = -1;
        foreach (string g in AssetDatabase.FindAssets(nombre.Replace("#", " ") + " t:AudioClip"))
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            if (Path.GetFileNameWithoutExtension(p) != nombre) continue;
            int s = (p.EndsWith(".ogg") ? 2 : 0) + (p.Contains("48000") ? 1 : 0) + (p.Contains("96000") ? 0 : 1);
            if (s > puntos) { puntos = s; mejor = p; }
        }
        if (mejor == null) { Debug.LogWarning("[Ronda10] No encuentro el sonido " + nombre); return null; }
        return AssetDatabase.LoadAssetAtPath<AudioClip>(mejor);
    }

    // Sonidos de la Cazadora, accion por accion. Los que faltan en tu libreria
    // (rugido, latido, romper escudo) van con un sustituto: cambialos aqui.
    private static SonidosAcciones Sonidos()
    {
        SonidosAcciones s = AssetDatabase.LoadAssetAtPath<SonidosAcciones>(RutaSonidos);
        bool nuevo = s == null;
        if (nuevo) s = ScriptableObject.CreateInstance<SonidosAcciones>();
        RecursosRPG lib = AssetDatabase.LoadAssetAtPath<RecursosRPG>("Assets/Resources/RecursosRPG.asset");
        AudioClip[] DeLib(string clave) => lib != null ? lib.sonidos.FirstOrDefault(g => g.clave == clave)?.clips ?? new AudioClip[0] : new AudioClip[0];
        AudioClip[] C(params string[] n) => n.Select(Clip).Where(c => c != null).ToArray();
        void A(string clave, string que, float vol, AudioClip[] clips)
        {
            if (s.acciones.Any(x => x.clave == clave)) return;
            s.acciones.Add(new SonidosAcciones.Accion { accion = que, clave = clave, clips = clips, volumen = vol, variacionTono = 0.05f });
        }
        A("tajo", "Tajo de espada", 0.8f, C("WEAPSwrd_MetalwWhoosh_HoveAud_SwordCombat_03", "WEAPSwrd_MetalwWhoosh_HoveAud_SwordCombat_06", "WHSH_Whoosh_HoveAud_SwordCombat_07"));
        A("tajo_elemento", "Tajo con elemento", 0.7f, C("magic-spell-dark-whoosh-epic-stock-media-2-2-00-02", "WHSH_Whoosh_HoveAud_SwordCombat_26"));
        A("aviso_ligero", "Aviso de ataque ligero (brillo)", 0.45f, C("MAGShim_Ring_HoveAud_SwordCombat_01"));
        A("aviso_pesado", "Aviso de ataque pesado", 0.6f, C("MAGShim_Ring_HoveAud_SwordCombat_11", "MAGShim_Ring_HoveAud_SwordCombat_17"));
        A("dash", "Dash", 0.7f, C("wind_spell_cast_air_short_rise_whoosh_01", "wind_spell_cast_air_short_rise_whoosh_02", "wind_spell_cast_air_short_rise_whoosh_03"));
        A("cruce_letal", "Cruce de la Ejecucion", 0.95f, C("wind_spell_cast_air_rise_loud_impact_01"));
        A("ola", "Ola de luz", 0.55f, C("Holy_spell_wave_cast_01", "Holy_spell_wave_cast_02", "Holy_spell_wave_cast_03"));
        A("salto", "Salto", 0.5f, C("wind_spell_cast_air_short_whoosh_01", "wind_spell_cast_air_short_whoosh_02"));
        A("aterrizaje", "Aterrizaje del tajo descendente", 0.8f, DeLib("jefe_impacto_suelo"));
        A("parry", "Parry (el tuyo o el suyo)", 0.9f, C("Sword Parry 1", "Sword Parry 2", "Sword Parry 3"));
        A("guardia", "Guardia (su pose de parry)", 0.7f, C("MAGShim_Ring_HoveAud_SwordCombat_17", "Sword Unsheath 1"));
        A("rugido", "Rugido (SUSTITUTO: no hay rugido en la libreria)", 1f, DeLib("jefe_transformacion"));
        A("escudo", "Escudo de luz", 0.8f, C("Holy_spell_protect_cast_01", "Holy_spell_protect_cast_02"));
        A("escudo_golpe", "Golpe contra el escudo", 0.5f, C("Ice_Spell_cast_glassy_01", "Ice_Spell_cast_glassy_02"));
        A("escudo_roto", "Escudo roto (SUSTITUTO: cristal de hielo)", 0.95f, C("Ice_Spell_cast_glassy_05", "Ice_Spell_cast_glassy_06"));
        A("curar", "Se cura", 0.8f, C("Holy_spell_heal_cast_long_01"));
        A("ilusion_aparece", "Aparece una ilusion", 0.55f, C("dark_spell_cast_warp_0-001", "dark_spell_cast_warp_0-002"));
        A("ilusion_deshace", "Una ilusion se deshace", 0.5f, C("Holy_spell_heal_dust_cast_evolving_03", "wind_spell_cast_air_transient_whoosh_01"));
        A("reconstruir", "Polvo que se reconstruye", 0.9f, C("Holy_spell_heal_dust_cast_evolving_01"));
        A("muerte_falsa", "Muerte falsa (se deshace)", 0.9f, DeLib("jefe_caida"));
        A("muerte_real", "Muerte final", 1f, DeLib("jefe_muerte"));
        A("aviso_ejecucion", "Aviso: Ejecucion", 1f, C("dark_spell_cast_energy_riser_metallic_01"));
        A("aviso_sentencia", "Aviso: Sentencia", 1f, C("Holy_spell_revive_cast_03"));
        A("sentencia_fija", "La marca de la Sentencia se fija", 1f, C("dark_spell_cast_charge_impact_01"));
        A("tajo_gigante", "Tajo gigante de la Sentencia", 1f, C("dark_spell_cast_energy_wave_01"));
        A("aviso_silencio", "Aviso: El Silencio", 0.9f, C("wind_spell_cast_frost_air_long_01"));
        A("silencio_delatado", "Te delatas en El Silencio", 1f, C("WEAPSwrd_SwordStabwWhoosh_HoveAud_SwordCombat_17"));
        A("latido", "Latido (SUSTITUTO: golpe grave de viento)", 0.8f, C("wind_spell_cast_air_rise_thump_01"));
        A("aturdida", "Aturdida", 0.7f, DeLib("jefe_postura"));
        A("contra_elemento", "Copia tu imbuicion", 0.8f, C("Holy_spell_wave_cast_07", "dark_spell_cast_curse_02"));
        A("invocar", "Invoca ilusiones", 0.6f, C("dark_spell_cast_invoke_raven_fast_01"));
        A("marca", "Marca en el suelo", 0.35f, C("MAGShim_Ring_HoveAud_SwordCombat_01"));
        A("fantasma_aviso", "Cuchillada fantasma", 0.6f, C("dark_spell_cast_curse_01"));
        A("trampas", "Trampas de sonido", 0.5f, C("dark_spell_cast_energy_wave_03"));
        A("espejismo", "Espejismo", 0.6f, C("dark_spell_cast_warp_0-003"));
        A("espejismo_carga", "El espejismo va a explotar", 0.6f, C("dark_spell_cast_energy_riser_01"));
        A("explosion", "Explota el espejismo", 0.9f, C("dark_spell_cast_charge_impact_02", "dark_spell_cast_charge_impact_03"));
        A("danza", "Danza de la Caceria", 0.7f, C("wind_spell_cast_air_rise_sharp_shoot_01"));
        if (nuevo) AssetDatabase.CreateAsset(s, RutaSonidos);
        EditorUtility.SetDirty(s);
        return s;
    }

    // El sonido inquietante de "Algo desperto en el bosque..." (libreria general).
    private static void SonidoLibreria()
    {
        RecursosRPG lib = AssetDatabase.LoadAssetAtPath<RecursosRPG>("Assets/Resources/RecursosRPG.asset");
        if (lib == null || lib.sonidos.Any(g => g.clave == "secreto_inquietante")) return;
        AudioClip c = Clip("dark_spell_cast_curse_03");
        if (c == null) return;
        lib.sonidos.Add(new RecursosRPG.GrupoSonido { clave = "secreto_inquietante", clips = new[] { c }, volumen = 0.7f, variacionTono = 0f });
        EditorUtility.SetDirty(lib);
    }

    // ------------------------------------------------------------------ Prefab

    private static GameObject Prefab(SpritesCazadora hojas, AjustesCazadora aj, SonidosAcciones son)
    {
        GameObject raiz = new GameObject("CazadoraCiega");
        raiz.tag = "Enemy";
        raiz.layer = LayerMask.NameToLayer("Enemies");
        Rigidbody2D rb = raiz.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.gravityScale = 0f;
        CapsuleCollider2D col = raiz.AddComponent<CapsuleCollider2D>();
        col.isTrigger = true;
        col.size = new Vector2(0.75f, 1.35f);
        col.offset = new Vector2(0f, 0.68f);
        EnemyHealth eh = raiz.AddComponent<EnemyHealth>();
        Poner(eh, "maxHealth", 2400);
        Poner(eh, "bodyMass", 500f);
        Poner(eh, "showHealthBar", false);
        Poner(eh, "deathAnimDuration", 8f);
        Poner(eh, "inamovible", true);
        Poner(eh, "healthBarOffset", new Vector2(0f, 1.6f));
        Poner(eh, "almas", 0);
        eh.tieneSangre = true;
        raiz.AddComponent<CuerpoCazadora>();
        raiz.AddComponent<OidoCazadora>();
        JefeCazadora j = raiz.AddComponent<JefeCazadora>();

        AnimCazadora anim = Visual(raiz.transform, hojas, "Visual", "Player", -2, 5, out SpriteRenderer cuerpo);
        SpriteRenderer contorno = Renderer(anim.transform, "Contorno", "VFX", -15, mContorno);
        contorno.enabled = false;

        SpriteRenderer sombra = Renderer(raiz.transform, "Sombra", "Player", -6, mSinLuz);
        sombra.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Propia + "sombra.png");
        sombra.color = new Color(0f, 0f, 0f, 0.45f);
        SpriteRenderer burbuja = Renderer(raiz.transform, "Burbuja", "VFX", 4, mSinLuz);
        burbuja.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Propia + "burbuja.png");
        burbuja.transform.localPosition = new Vector3(0f, 0.75f, 0f);
        burbuja.enabled = false;

        // La plantilla de las ilusiones (apagada): la jefa la copia cuando hace falta.
        GameObject il = new GameObject("PlantillaIlusion");
        il.transform.SetParent(raiz.transform, false);
        il.tag = "Enemy";
        il.layer = LayerMask.NameToLayer("Enemies");
        BoxCollider2D bc = il.AddComponent<BoxCollider2D>();
        bc.isTrigger = true;
        bc.size = new Vector2(0.75f, 1.35f);
        bc.offset = new Vector2(0f, 0.68f);
        IlusionCazadora ic = il.AddComponent<IlusionCazadora>();
        EnemyHealth ieh = il.GetComponent<EnemyHealth>();
        Poner(ieh, "maxHealth", 999999);
        Poner(ieh, "showHealthBar", false);
        Poner(ieh, "inamovible", true);
        Poner(ieh, "almas", 0);
        ieh.tieneSangre = false;
        AnimCazadora ianim = Visual(il.transform, hojas, "Visual", "Player", -3, 4, out _);
        ic.anim = ianim;
        ic.aura = Renderer(ianim.transform, "Aura", "VFX", -14, mIlusion);
        ic.aura.enabled = false;
        il.SetActive(false);

        Poner(j, "ajustes", aj);
        Poner(j, "cuerpoAnim", anim);
        Poner(j, "plantillaIlusion", ic);
        Poner(j, "sombra", sombra);
        Poner(j, "contorno", contorno);
        Poner(j, "burbuja", burbuja);
        Poner(j, "visual", anim.transform);
        Poner(j, "sonidos", son);

        GameObject p = PrefabUtility.SaveAsPrefabAsset(raiz, RutaPrefab);
        Object.DestroyImmediate(raiz);
        return p;
    }

    private static AnimCazadora Visual(Transform padre, SpritesCazadora hojas, string nombre, string capa, int orden, int ordenFx, out SpriteRenderer cuerpo)
    {
        GameObject v = new GameObject(nombre);
        v.transform.SetParent(padre, false);
        AnimCazadora a = v.AddComponent<AnimCazadora>();
        a.hojas = hojas;
        cuerpo = Renderer(v.transform, "Cuerpo", capa, orden, mFlash);
        SpritesCazadora.Clip q = hojas.Buscar("quieto");
        if (q != null && q.cuerpo.Length > 0) cuerpo.sprite = q.cuerpo[0];
        a.cuerpo = cuerpo;
        a.efecto = Renderer(v.transform, "Efecto", "VFX", ordenFx, mSinLuz);
        return a;
    }

    private static SpriteRenderer Renderer(Transform padre, string nombre, string capa, int orden, Material m)
    {
        GameObject g = new GameObject(nombre);
        g.transform.SetParent(padre, false);
        SpriteRenderer sr = g.AddComponent<SpriteRenderer>();
        sr.sortingLayerName = capa;
        sr.sortingOrder = orden;
        if (m != null) sr.sharedMaterial = m;
        return sr;
    }

    private static void Poner(Object o, string campo, object valor)
    {
        SerializedObject so = new SerializedObject(o);
        SerializedProperty p = so.FindProperty(campo);
        if (p == null) { Debug.LogWarning($"[Ronda10] {o.GetType().Name} no tiene el campo {campo}"); return; }
        switch (valor)
        {
            case int i: p.intValue = i; break;
            case float f: p.floatValue = f; break;
            case bool b: p.boolValue = b; break;
            case Vector2 v: p.vector2Value = v; break;
            case Rect r: p.rectValue = r; break;
            case string s: p.stringValue = s; break;
            case Color c: p.colorValue = c; break;
            case Object ob: p.objectReferenceValue = ob; break;
            case Object[] arr:
                p.arraySize = arr.Length;
                for (int k = 0; k < arr.Length; k++) p.GetArrayElementAtIndex(k).objectReferenceValue = arr[k];
                break;
            default: if (valor == null) p.objectReferenceValue = null; break;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ------------------------------------------------------------------ Fondo

    // Capas del bosque Gloomwood, pixeladas: (archivo, nombre, escala, opaca).
    // Las del lienzo grande (2560x1440) van a 19 unidades de alto.
    private const float EscalaLienzo = 19f * PPU / 1440f;
    private static readonly (string archivo, string nombre, float escala, bool opaca)[] CapasFondo =
    {
        ("SKY/sky.png", "cielo", EscalaLienzo, true),
        ("HILLS/Hills.png", "colinas", EscalaLienzo, false),
        ("TREES/Tree Clusters/background/tree_cluster_background1.png", "arboles_lejos", EscalaLienzo, false),
        ("GROUND/Ground.png", "suelo_lejos", EscalaLienzo, false),
        ("TREES/Tree Clusters/midground/tree_cluster_midground1.png", "arboles_medio", EscalaLienzo, false),
        ("TREES/Tree Clusters/foreground/tree_cluster_foreground1.png", "arboles_cerca", EscalaLienzo, false),
        ("FOLIAGE/Foliage Clusters/foreground/foliage_cluster_foreground1.png", "matas_bajas", 0.07f, false),
        ("TREES/tree_2.png", "arbol_2", 0.22f, false),
        ("TREES/tree_3.png", "arbol_3", 0.22f, false),
    };

    private static void Fondo()
    {
        foreach (var c in CapasFondo)
        {
            string destino = Propia + "Fondo/" + c.nombre + ".png";
            if (!File.Exists(destino)) Pixelar(Gloom + c.archivo, destino, c.escala, c.opaca);
            ImportarFondo(destino, false);
        }
        // Niebla: la del castillo de hielo, pixelada y repetible.
        string niebla = Propia + "Fondo/niebla.png";
        if (!File.Exists(niebla)) Pixelar(Pack + "BACKGROUND/Fondo_CastilloHielo/pngs/fog.png", niebla, 1f, false, true);
        ImportarFondo(niebla, true);
    }

    // Reduce la imagen promediando (sin perder detalle a lo bruto), deja los
    // bordes duros (pixel art) y limita un poco la paleta.
    private static void Pixelar(string origen, string destino, float escala, bool opaca, bool conservarAlfa = false)
    {
        Texture2D s = Leer(origen);
        int w = Mathf.Max(1, Mathf.RoundToInt(s.width * escala)), h = Mathf.Max(1, Mathf.RoundToInt(s.height * escala));
        Color[] src = s.GetPixels();
        Color[] dst = new Color[w * h];
        float fx = (float)s.width / w, fy = (float)s.height / h;
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            int x0 = Mathf.FloorToInt(x * fx), x1 = Mathf.Max(x0 + 1, Mathf.FloorToInt((x + 1) * fx));
            int y0 = Mathf.FloorToInt(y * fy), y1 = Mathf.Max(y0 + 1, Mathf.FloorToInt((y + 1) * fy));
            float r = 0f, g = 0f, b = 0f, a = 0f;
            int n = 0;
            for (int yy = y0; yy < y1 && yy < s.height; yy++)
            for (int xx = x0; xx < x1 && xx < s.width; xx++)
            {
                Color c = src[yy * s.width + xx];
                r += c.r * c.a; g += c.g * c.a; b += c.b * c.a; a += c.a;
                n++;
            }
            Color o = a > 0f ? new Color(r / a, g / a, b / a, a / Mathf.Max(1, n)) : Color.clear;
            if (!opaca && !conservarAlfa) o.a = o.a >= 0.45f ? 1f : 0f;
            if (opaca) o.a = 1f;
            const float niveles = 31f;
            o.r = Mathf.Round(o.r * niveles) / niveles;
            o.g = Mathf.Round(o.g * niveles) / niveles;
            o.b = Mathf.Round(o.b * niveles) / niveles;
            dst[y * w + x] = o;
        }
        Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, false);
        t.SetPixels(dst);
        t.Apply();
        File.WriteAllBytes(destino, t.EncodeToPNG());
        AssetDatabase.ImportAsset(destino, ImportAssetOptions.ForceUpdate);
    }

    private static void ImportarFondo(string ruta, bool repetir)
    {
        TextureImporter ti = (TextureImporter)AssetImporter.GetAtPath(ruta);
        if (ti == null) return;
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = PPU;
        ti.filterMode = FilterMode.Point;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.mipmapEnabled = false;
        ti.maxTextureSize = 4096;
        ti.wrapMode = repetir ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
        TextureImporterSettings st = new TextureImporterSettings();
        ti.ReadTextureSettings(st);
        st.spriteMeshType = SpriteMeshType.FullRect;
        st.spriteAlignment = (int)SpriteAlignment.Custom;
        st.spritePivot = new Vector2(0.5f, 0f);
        ti.SetTextureSettings(st);
        ti.SaveAndReimport();
    }

    private static Sprite SpriteFondo(string nombre) => AssetDatabase.LoadAssetAtPath<Sprite>(Propia + "Fondo/" + nombre + ".png");

    // ------------------------------------------------------------------ Ficha y logros

    private static void Ficha(Sprite imagen)
    {
        const string ruta = "Assets/Resources/Desafios/Jefe_BlindHuntress.asset";
        FichaJefe f = AssetDatabase.LoadAssetAtPath<FichaJefe>(ruta);
        if (f != null)
        {
            if (f.imagen == null) { f.imagen = imagen; EditorUtility.SetDirty(f); }
            return;
        }
        f = ScriptableObject.CreateInstance<FichaJefe>();
        f.id = "blind_huntress";
        f.nombre = "The Blind Huntress";
        f.escena = "Bosque Cazadora";
        f.orden = 2;
        f.imagen = imagen;
        f.afinidades = new List<FichaJefe.Afinidad>();
        f.notaAfinidades = "No tiene debilidades fijas. Se adapta: resiste temporalmente el elemento que llevas imbuido.";
        f.informacion =
            "Es ciega: de lejos ataca a donde oyó tu último ruido (atacar, rodar, aterrizar, beber, imbuir, correr). Si fallas donde golpea, queda confundida. De cerca te siente siempre.\n" +
            "<b>Fase 1</b>: Tres Lunas, Cruce, Ola de luz y tajos contra los saltos. Si atacas sin parar, se pone en guardia y te castiga.\n" +
            "<b>Fase 2</b>: fuego, hielo, luz y sangre; ilusiones, cuchillada fantasma y trampas de sonido.\n" +
            "<b>Fase 3</b>: oscuridad, Danza de la Cacería y Cacería de Espejos.\n" +
            "Tres barras. Con poca vida se cura en un escudo de luz: rómpelo. Un ataque mortal por fase, siempre avisado: Ejecución, Sentencia y El Silencio.";
        f.historia =
            "Nadie recuerda cuándo se quedó ciega ni quién la dejó así. Lo que se sabe es que el bosque murió primero: las hojas cayeron una noche y no volvieron a brotar. " +
            "Ella se quedó. Aprendió a cazar con los oídos, a leer el miedo en un latido, a distinguir el paso de un ladrón del de un hijo que vuelve a casa. " +
            "Muchos vinieron a matar a la cazadora ciega. Ninguno la sorprendió. En el bosque sin hojas todo se oye, y ella escucha desde hace tanto que ya no recuerda " +
            "otra cosa que el sonido de quien viene a morir.";
        f.almasCofre = 2500;
        f.objetosCofre = new[] { Equipo.Objeto.PiedraForja, Equipo.Objeto.LagrimaCarmesi, Equipo.Objeto.FrascoSangre };
        f.multVida = 1.5f;
        f.multDano = 1.3f;
        f.multVelocidad = 1.35f;
        f.secreto = true;
        f.requisitos = new[] { "crimson_wraith", "shadowed_wetlands" };
        AssetDatabase.CreateAsset(f, ruta);
    }

    private static void Logros()
    {
        void L(string id, FichaLogro.Seccion s, int orden, string nombre, string desc, FichaLogro.Condicion c, string icono)
        {
            string ruta = $"Assets/Resources/Logros/{s}_{orden}_{id}.asset";
            if (AssetDatabase.LoadAssetAtPath<FichaLogro>(ruta) != null) return;
            FichaLogro l = ScriptableObject.CreateInstance<FichaLogro>();
            l.id = id; l.seccion = s; l.orden = orden; l.nombre = nombre; l.descripcion = desc;
            l.condicion = c; l.parametro = "blind_huntress";
            l.jefeSecreto = "blind_huntress";
            string ri = Iconos + icono;
            ConfigurarRecursosRPG.PrepararPixel(ri);
            l.icono = AssetDatabase.LoadAllAssetsAtPath(ri).OfType<Sprite>().FirstOrDefault();
            AssetDatabase.CreateAsset(l, ruta);
        }
        L("prueba_bosque", FichaLogro.Seccion.Desafios, 2, "Prueba del bosque", "Completa el desafío de The Blind Huntress.",
          FichaLogro.Condicion.CompletarDesafio, "08-loot-treasure/icon_63.png");
        L("cicatriz_bosque", FichaLogro.Seccion.Dificil, 2, "Cicatriz del bosque", "Derrota a The Blind Huntress en dificultad Difícil.",
          FichaLogro.Condicion.CompletarDesafioDificil, "08-loot-treasure/icon_51.png");
    }

    // ------------------------------------------------------------------ Escena

    // Trazado (en casillas del tileset; el suelo arriba en y = 0):
    //   -24..-19  pared izquierda
    //   -19..0    linde: la hoguera (el desafio pone ahi el totem y el cofre)
    //   0         bloqueo de la arena (niebla)
    //   0..45     arena (un poco mas ancha que la de Shadowed Wetlands)
    //   45..52    pared derecha
    private static readonly RectInt[] Roca =
    {
        new RectInt(-30, -16, 92, 16),
        new RectInt(-30, 0, 11, 26),
        new RectInt(45, 0, 17, 26),
    };
    private static readonly Rect ZonaArena = Rect.MinMaxRect(0.4f, 0f, 45f, 16f);
    private const float XJefa = 32f, XHoguera = -10f;
    // Base de las capas del fondo pintado (los troncos quedan tras el suelo).
    private const float AlturaFondo = -2.5f;

    private static void CrearEscena(GameObject prefab, AjustesCazadora aj)
    {
        if (File.Exists(Escena)) AssetDatabase.DeleteAsset(Escena);
        if (!AssetDatabase.CopyAsset(Plantilla, Escena)) { Debug.LogError("[Ronda10] No se pudo copiar " + Plantilla); return; }
        Scene escena = EditorSceneManager.OpenScene(Escena, OpenSceneMode.Single);
        // Abrir la escena descarga lo que no se usa: se vuelven a leer del disco.
        aj = AssetDatabase.LoadAssetAtPath<AjustesCazadora>(RutaAjustes);
        prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RutaPrefab);

        // Lo que se aprovecha de la escena de la nieve: la hoguera, un cofre (el
        // desafio lo usa de plantilla) y la niebla del bloqueo.
        GameObject viejo = escena.GetRootGameObjects().FirstOrDefault(g => g.name == "Nivel");
        Transform hoguera = viejo != null ? viejo.transform.Find("Hogueras/Hoguera_4") : null;
        Transform cofre = viejo != null ? viejo.transform.Find("CofreMejora_mejora_1") : null;
        Transform niebla = viejo != null ? viejo.transform.Find("ArenaJefe/NieblaJefe") : null;
        foreach (Transform t in new[] { hoguera, cofre, niebla }) if (t != null) t.SetParent(null, true);
        foreach (GameObject r in escena.GetRootGameObjects())
            if (new[] { "Nivel", "AmbienteNieve", "Estatuas", "DecoracionExtra", "EntranceDoor", "ExitDoor" }.Contains(r.name)) Object.DestroyImmediate(r);
        GameObject camaraPrincipal = escena.GetRootGameObjects().FirstOrDefault(g => g.name == "Main Camera");
        Transform nieve = camaraPrincipal != null ? camaraPrincipal.transform.Find("NieveCayendo") : null;
        if (nieve != null) Object.DestroyImmediate(nieve.gameObject);

        Transform nivel = new GameObject("Nivel").transform;
        Terreno(nivel);
        FondoEscena(nivel, out SpriteRenderer oscurecedor);
        AmbienteBosque amb = Ambiente(nivel, oscurecedor);

        if (hoguera != null)
        {
            hoguera.SetParent(nivel, true);
            hoguera.position = new Vector3(XHoguera, 0f, 0f);
            hoguera.name = "Hoguera";
            Hoguera hc = hoguera.GetComponent<Hoguera>();
            if (hc != null) Poner(hc, "nombreLugar", "Linde del bosque");
        }
        if (cofre != null)
        {
            cofre.SetParent(nivel, true);
            cofre.position = new Vector3(-15f, 0f, 0f);
        }

        // Bloqueo de la arena (la niebla de siempre, tenida de verde).
        var visual = new List<SpriteRenderer>();
        Collider2D muro = null;
        if (niebla != null)
        {
            niebla.SetParent(nivel, true);
            niebla.name = "BloqueoArena";
            niebla.position = new Vector3(0f, 7f, 0f);
            niebla.gameObject.layer = LayerMask.NameToLayer("Ground");
            BoxCollider2D b = niebla.GetComponent<BoxCollider2D>();
            if (b != null) { b.offset = Vector2.zero; b.size = new Vector2(0.8f, 16f); muro = b; }
            visual.AddRange(niebla.GetComponentsInChildren<SpriteRenderer>());
        }

        // La arena.
        GameObject ga = new GameObject("ArenaCazadora");
        ga.layer = LayerMask.NameToLayer("Items");
        ga.transform.SetParent(nivel);
        BoxCollider2D disparador = ga.AddComponent<BoxCollider2D>();
        disparador.isTrigger = true;
        disparador.offset = new Vector2(23.5f, 5f);
        disparador.size = new Vector2(42f, 10f);
        CamaraCazadora cc = ga.AddComponent<CamaraCazadora>();
        cc.tamanoBase = 6.4f;
        ArenaCazadora arena = ga.AddComponent<ArenaCazadora>();
        Poner(arena, "prefabJefe", prefab.GetComponent<JefeCazadora>());
        Poner(arena, "ajustes", aj);
        Poner(arena, "zona", ZonaArena);
        Poner(arena, "suelo", 0f);
        Poner(arena, "xAparicion", XJefa);
        Poner(arena, "muro", muro);
        Poner(arena, "visualMuro", visual.Cast<Object>().ToArray());
        Poner(arena, "ambiente", amb);
        Poner(arena, "camara", cc);

        // Camara: una zona para la arena y los limites de todo el bosque.
        GameObject zc = new GameObject("ZonaCamaraArena");
        zc.layer = LayerMask.NameToLayer("Items");
        zc.transform.SetParent(nivel);
        BoxCollider2D zb = zc.AddComponent<BoxCollider2D>();
        zb.isTrigger = true;
        zb.offset = new Vector2(22.5f, 7f);
        zb.size = new Vector2(45f, 16f);
        ZonaCamara z = zc.AddComponent<ZonaCamara>();
        Poner(z, "tamano", 6.4f);
        Poner(z, "desplazamientoY", 1.8f);
        GameObject limite = escena.GetRootGameObjects().FirstOrDefault(g => g.name == "CameraLimit");
        if (limite != null)
        {
            BoxCollider2D lb = limite.GetComponent<BoxCollider2D>();
            if (lb != null) { lb.offset = new Vector2(13.5f, 7f); lb.size = new Vector2(69f, 24f); }
        }

        // Datos del nivel: frases de la pantalla de muerte.
        GameObject dn = new GameObject("DatosNivel");
        dn.transform.SetParent(nivel);
        DatosNivel datos = dn.AddComponent<DatosNivel>();
        Poner(datos, "config", ConfigBosque());

        // Player, reaparicion y luz.
        foreach (GameObject r in escena.GetRootGameObjects())
        {
            if (r.name == "Player" || r.name == "RespawnPoint") r.transform.position = new Vector3(XHoguera - 2f, 0.8f, 0f);
            if (r.name == "Global Light 2D")
            {
                Light2D l = r.GetComponent<Light2D>();
                if (l != null) { l.color = aj.FaseN(0).luz; l.intensity = aj.FaseN(0).intensidadLuz; }
            }
        }

        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
        Debug.Log("[Ronda10] Escena creada: " + Escena);
    }

    private static ConfigNivel ConfigBosque()
    {
        const string ruta = "Assets/Data/Niveles/Config Nivel Bosque.asset";
        ConfigNivel c = AssetDatabase.LoadAssetAtPath<ConfigNivel>(ruta);
        if (c != null) return c;
        c = ScriptableObject.CreateInstance<ConfigNivel>();
        c.frasesMuerte = new[]
        {
            "En el bosque sin hojas, todo se oye.",
            "Respira más despacio.",
            "Ella no te vio. Te oyó.",
            "Las hojas cayeron hace mucho. Tú, ahora.",
        };
        AssetDatabase.CreateAsset(c, ruta);
        return c;
    }

    // Suelo de pasto seco (Floor Tiles1, bloque ocre), apagado hacia el verde.
    private static void Terreno(Transform nivel)
    {
        Sprite[] celdas = AssetDatabase.LoadAllAssetsAtPath(Gandalf + "Floor Tiles1.png").OfType<Sprite>().ToArray();
        Sprite S(int c, int r) => celdas.FirstOrDefault(s => s.name == $"c{c}_r{r}");
        string[] nombres = { "esq_ai", "arriba", "esq_ad", "izq", "centro", "der", "esq_bi", "abajo", "esq_bd" };
        var tiles = new Dictionary<string, Tile>();
        Color tinte = new Color(0.78f, 0.8f, 0.64f);
        for (int i = 0; i < 9; i++) tiles[nombres[i]] = TileAsset("bosque_" + nombres[i], S(i % 3, 6 + i / 3), tinte);

        Grid grid = new GameObject("Terreno", typeof(Grid)).GetComponent<Grid>();
        grid.transform.SetParent(nivel);
        Tilemap tm = new GameObject("Suelo", typeof(Tilemap), typeof(TilemapRenderer)).GetComponent<Tilemap>();
        tm.transform.SetParent(grid.transform, false);
        TilemapRenderer tr = tm.GetComponent<TilemapRenderer>();
        tr.sortingLayerName = "Ground";
        tr.mode = TilemapRenderer.Mode.Chunk;
        foreach (RectInt r in Roca)
        for (int x = r.xMin; x < r.xMax; x++)
        for (int y = r.yMin; y < r.yMax; y++)
        {
            bool arriba = !EsRoca(x, y + 1), abajo = !EsRoca(x, y - 1), izq = !EsRoca(x - 1, y), der = !EsRoca(x + 1, y);
            string n = arriba ? (izq ? "esq_ai" : der ? "esq_ad" : "arriba")
                     : abajo ? (izq ? "esq_bi" : der ? "esq_bd" : "abajo")
                     : izq ? "izq" : der ? "der" : "centro";
            tm.SetTile(new Vector3Int(x, y, 0), tiles[n]);
        }
        tm.gameObject.AddComponent<CapaTerreno>();
        PaletaNieve.PonerColisionTilemap(tm, LayerMask.NameToLayer("Ground"));
    }

    private static bool EsRoca(int x, int y)
    {
        if (y < -16) return true;
        Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
        return Roca.Any(r => p.x >= r.xMin && p.x < r.xMax && p.y >= r.yMin && p.y < r.yMax);
    }

    private static Tile TileAsset(string nombre, Sprite s, Color color)
    {
        string ruta = CarpetaTiles + "/" + nombre + ".asset";
        Tile t = AssetDatabase.LoadAssetAtPath<Tile>(ruta);
        if (t == null) { t = ScriptableObject.CreateInstance<Tile>(); AssetDatabase.CreateAsset(t, ruta); }
        t.sprite = s;
        t.color = color;
        t.colliderType = Tile.ColliderType.Grid;
        t.flags = TileFlags.LockColor;
        EditorUtility.SetDirty(t);
        return t;
    }

    // Capas de parallax: de la mas lejana a la mas cercana, cada vez mas oscuras
    // (frias y verdosas). El velo del cielo va entre las colinas y los arboles.
    private static void FondoEscena(Transform nivel, out SpriteRenderer oscurecedor)
    {
        GameObject go = new GameObject("Fondo");
        go.transform.SetParent(nivel);
        ParallaxBosque px = go.AddComponent<ParallaxBosque>();
        px.seguimientoVertical = 0.5f;
        px.alturaCamara = 2.2f;
        var capas = new List<ParallaxBosque.Capa>();
        (string nombre, float seguimiento, Color color, int orden)[] orden =
        {
            ("cielo", 0.97f, new Color(0.72f, 0.8f, 0.74f), 0),
            ("colinas", 0.9f, new Color(0.6f, 0.68f, 0.62f), 1),
            ("arboles_lejos", 0.82f, new Color(0.54f, 0.62f, 0.56f), 3),
            ("suelo_lejos", 0.7f, new Color(0.5f, 0.55f, 0.47f), 5),
            ("arboles_medio", 0.62f, new Color(0.46f, 0.52f, 0.45f), 6),
            ("arboles_cerca", 0.4f, new Color(0.34f, 0.38f, 0.32f), 8),
        };
        foreach (var c in orden)
        {
            Sprite s = SpriteFondo(c.nombre);
            if (s == null) continue;
            Transform raiz = new GameObject("Capa_" + c.nombre).transform;
            raiz.SetParent(go.transform, false);
            raiz.localPosition = new Vector3(0f, AlturaFondo, 0f);
            float ancho = s.bounds.size.x;
            for (int k = -2; k <= 2; k++)
            {
                SpriteRenderer sr = new GameObject("Copia").AddComponent<SpriteRenderer>();
                sr.transform.SetParent(raiz, false);
                sr.transform.localPosition = new Vector3(k * ancho, 0f, 0f);
                sr.sprite = s;
                sr.color = c.color;
                sr.sortingLayerName = "Background";
                sr.sortingOrder = c.orden;
            }
            capas.Add(new ParallaxBosque.Capa { raiz = raiz, ancho = ancho, seguimiento = c.seguimiento, altura = AlturaFondo });
        }
        px.capas = capas.ToArray();

        // Velo que oscurece el cielo y lo lejano en las fases 2 y 3.
        oscurecedor = new GameObject("VeloCielo").AddComponent<SpriteRenderer>();
        oscurecedor.transform.SetParent(go.transform, false);
        oscurecedor.transform.position = new Vector3(12f, 8f, 0f);
        oscurecedor.transform.localScale = new Vector3(400f, 120f, 1f);
        oscurecedor.sprite = Blanco();
        oscurecedor.sortingLayerName = "Background";
        oscurecedor.sortingOrder = 2;
        oscurecedor.color = new Color(0f, 0f, 0f, 0f);

        // Arboles muertos en los dos extremos (enmarcan la arena).
        Arbol(nivel, "arbol_2", new Vector2(-17.2f, 0f), false);
        Arbol(nivel, "arbol_3", new Vector2(46f, 0f), true);
        // Matas en primer plano, a ras de suelo (bajas: no tapan la pelea).
        Sprite matas = SpriteFondo("matas_bajas");
        if (matas != null)
            for (float x = -20f; x < 50f; x += matas.bounds.size.x * 1.3f)
            {
                SpriteRenderer sr = new GameObject("Matas").AddComponent<SpriteRenderer>();
                sr.transform.SetParent(nivel, false);
                sr.transform.position = new Vector3(x, -0.55f, 0f);
                sr.sprite = matas;
                sr.color = new Color(0.26f, 0.3f, 0.23f, 1f);
                sr.sortingLayerName = "VFX";
                sr.sortingOrder = -40;
            }
    }

    private static void Arbol(Transform nivel, string nombre, Vector2 pos, bool volteado)
    {
        Sprite s = SpriteFondo(nombre);
        if (s == null) return;
        SpriteRenderer sr = new GameObject("Arbol").AddComponent<SpriteRenderer>();
        sr.transform.SetParent(nivel, false);
        sr.transform.position = pos;
        sr.flipX = volteado;
        sr.sprite = s;
        sr.color = new Color(0.3f, 0.34f, 0.28f, 1f);
        sr.sortingLayerName = "Middleground";
        sr.sortingOrder = -5;
    }

    private static AmbienteBosque Ambiente(Transform nivel, SpriteRenderer oscurecedor)
    {
        GameObject go = new GameObject("AmbienteBosque");
        go.transform.SetParent(nivel);
        AmbienteBosque a = go.AddComponent<AmbienteBosque>();
        a.zona = Rect.MinMaxRect(-19f, 0f, 45f, 16f);
        a.oscurecedor = oscurecedor;
        Sprite fog = SpriteFondo("niebla");
        var nieblas = new List<SpriteRenderer>();
        (float y, float alto, string capa, int orden)[] capas = { (-0.8f, 0.7f, "Background", 7), (-1.2f, 0.45f, "Middleground", 10) };
        foreach (var c in capas)
        {
            if (fog == null) break;
            SpriteRenderer sr = new GameObject("Niebla").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(go.transform, false);
            sr.transform.localPosition = new Vector3(13f, c.y, 0f);
            sr.transform.localScale = new Vector3(1f, c.alto, 1f);
            sr.sprite = fog;
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.size = new Vector2(fog.bounds.size.x * 6f, fog.bounds.size.y);
            sr.sortingLayerName = c.capa;
            sr.sortingOrder = c.orden;
            sr.color = new Color(1f, 1f, 1f, 0f);
            nieblas.Add(sr);
        }
        a.nieblas = nieblas.ToArray();
        return a;
    }

    private static Sprite blanco;

    private static Sprite Blanco()
    {
        if (blanco != null) return blanco;
        const string ruta = Propia + "blanco.png";
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
        blanco = AssetDatabase.LoadAssetAtPath<Sprite>(ruta);
        return blanco;
    }

    private static void AnadirABuild()
    {
        var escenas = EditorBuildSettings.scenes.ToList();
        if (escenas.Any(s => s.path == Escena)) return;
        escenas.Add(new EditorBuildSettingsScene(Escena, true));
        EditorBuildSettings.scenes = escenas.ToArray();
    }
}
