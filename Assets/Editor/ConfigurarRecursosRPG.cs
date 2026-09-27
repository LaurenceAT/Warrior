using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Rellena Assets/Resources/RecursosRPG.asset: sonidos por clave, iconos del pack
// Iconos_RPGPack y los tajos de color por elemento. Lo llama el generador del
// nivel nevado; tambien se puede lanzar solo (Warrior > Configurar recursos RPG).
//
// Los .wav del pack no se suben a GitHub (.gitignore): los pocos que se usan se
// copian a Assets/Audio, que si se sube.
public static class ConfigurarRecursosRPG
{
    public const string Pack = "Assets/SPRITES PARA NUEVOS NIVELES/";
    private const string Ruta = "Assets/Resources/RecursosRPG.asset";
    private const string FG = Pack + "SONIDOS/Sonidos_Efectos/Sonidos_FantasyGeneral/OGG Files/";
    private const string MAG = Pack + "SONIDOS/Sonidos_Efectos/Sonidos_Magia/";
    private const string CE = Pack + "SONIDOS/Sonidos_Efectos/Sonidos_CombateEspada/";
    private const string Iconos = Pack + "ICONOS/Iconos_RPGPack/";
    private const string Tajos = Pack + "EFECTOS DE ATAQUE DEL PLAYER/Efecto_Tajos/128x128/";
    private const string CopiaAudio = "Assets/Audio/Combate";

    private const string Fuego = MAG + "Fire Spell Pack/48000kHz/ogg/";
    private const string Hielo = MAG + "Ice Spell Pack/96000kHz/ogg/";
    private const string Oscuro = MAG + "Dark Spell Pack/96000 kHz/ogg/";
    private const string Sagrado = MAG + "Holy Spell Pack/96000kHz/ogg/";
    private const string Acido = MAG + "Acid Spell Pack/48000 kHz/ogg/";
    private const string Agua = MAG + "Water Spell Pack/48000 kHz/ogg/";
    private const string Viento = MAG + "Wind Spell Pack/48000 kHz/ogg/";
    private const string Espada = FG + "SFX/Attacks/Sword Attacks Hits and Blocks/";
    private const string Arco = FG + "SFX/Attacks/Bow Attacks Hits and Blocks/";

    [MenuItem("Warrior/Configurar recursos RPG")]
    public static RecursosRPG Configurar()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        RecursosRPG r = AssetDatabase.LoadAssetAtPath<RecursosRPG>(Ruta);
        if (r == null)
        {
            r = ScriptableObject.CreateInstance<RecursosRPG>();
            AssetDatabase.CreateAsset(r, Ruta);
        }

        r.sonidos = Sonidos();
        r.iconos = IconosElegidos();
        r.tajosElemento = TajosPorElemento();
        r.impactoElemental = Clip("impacto_elemental", Pack + "EFECTOS DE ATAQUE DEL PLAYER/Efecto_Impactos/VFX1/B&W/Frames", 22f, false, 64f, new Vector2(0.5f, 0.5f));
        r.fxCongelado = Clip("congelado", Pack + "EFECTOS DE ATAQUE DEL PLAYER/Efecto_CaballeroHielo/VFX3/Frames", 16f, false, 64f, new Vector2(0.5f, 0.12f));
        r.shaderAura = Shader.Find("Sprites/Aura");
        EditorUtility.SetDirty(r);
        AssetDatabase.SaveAssets();
        Debug.Log($"[RPG] Recursos: {r.sonidos.Count} grupos de sonido, {r.iconos.Count} iconos, {r.tajosElemento.Length} tajos.");
        return r;
    }

    // ------------------------------------------------------------------ Sonidos

    private static List<RecursosRPG.GrupoSonido> Sonidos()
    {
        var l = new List<RecursosRPG.GrupoSonido>();
        void G(string clave, float vol, params string[] rutas) => l.Add(new RecursosRPG.GrupoSonido
        {
            clave = clave, volumen = vol, clips = rutas.Select(Audio).Where(a => a != null).ToArray(),
        });
        string[] N(string baseRuta, string nombre, params int[] nums) => nums.Select(n => baseRuta + string.Format(nombre, n)).ToArray();

        // Player
        // Espadazo al aire: solo el silbido de la hoja (un sonido por golpe).
        G("espada_tajo", 0.55f, CE + "Whooshes/WHSH_Whoosh_HoveAud_SwordCombat_07.wav", CE + "Whooshes/WHSH_Whoosh_HoveAud_SwordCombat_26.wav");
        G("espada_impacto", 0.65f, N(Espada, "Sword Impact Hit {0}.ogg", 1, 2, 3));
        G("bloqueo", 0.75f, N(Espada, "Sword Blocked {0}.ogg", 1, 2, 3));
        // Parry: choque de metal contra metal y, encima, el anillo de la hoja.
        G("parry", 1f, CE + "Main Sounds/Sword Collisions/Base Metal/WEAPSwrd_BaseMetal_HoveAud_SwordCombat_03.wav",
          CE + "Main Sounds/Sword Collisions/Base Metal/WEAPSwrd_BaseMetal_HoveAud_SwordCombat_06.wav");
        G("parry_brillo", 0.7f, N(CE + "Ring FX/", "MAGShim_Ring_HoveAud_SwordCombat_{0:00}.wav", 1, 11, 17));
        G("dano_player", 0.65f, N(CE + "Voicelines/Damage Grunts/", "VOXScrm_DamageGrunt_HoveAudio_SwordCombat_{0:00}.wav", 1, 13, 22, 27));
        G("esfuerzo", 0.45f, N(CE + "Voicelines/Action Grunts/", "VOXEfrt_ActionGrunt_HoveAud_SwordCombat_{0:00}.wav", 1, 7, 23, 29));
        G("muerte_player", 0.8f, Oscuro + "dark_spell_cast_curse_02.ogg");
        G("esquiva", 0.5f, N(Viento, "wind_spell_cast_air_short_whoosh_{0:00}.ogg", 1, 2, 3));
        G("salto", 0.5f, FG + "SFX/Footsteps/Dirt/Dirt Jump.ogg");
        G("aterrizaje", 0.55f, FG + "SFX/Footsteps/Dirt/Dirt Land.ogg");
        G("pasos", 0.35f, N(FG + "SFX/Footsteps/Dirt/", "Dirt Walk {0}.ogg", 1, 2, 3, 4, 5));
        G("pasos_hielo", 0.35f, N(FG + "SFX/Footsteps/Stone/", "Stone Walk {0}.ogg", 1, 2, 3, 4, 5));
        G("beber", 0.5f, N(Sagrado, "Holy_spell_heal_cast_bright_{0:00}.ogg", 1, 2, 3));

        // Elementos: imbuir, golpe y tajo.
        G("imbuir_0", 0.7f, Fuego + "fire_spell_burst_evolving_warm_01.ogg");
        G("imbuir_1", 0.7f, Hielo + "Ice_Spell_cast_glassy_01.ogg");
        G("imbuir_2", 0.7f, Oscuro + "dark_spell_cast_curse_01.ogg");
        G("imbuir_3", 0.7f, Sagrado + "Holy_spell_protect_cast_01.ogg");
        G("imbuir_4", 0.7f, Acido + "acid_spell_cast_bubble_poison_01.ogg");
        G("golpe_0", 0.5f, N(Fuego, "fire_spell_burst_impact_short_{0:00}.ogg", 1, 2, 3, 4, 5));
        G("golpe_1", 0.5f, N(Hielo, "Ice_Spell_fast_impact_{0:00}.ogg", 1, 2, 3, 4));
        G("golpe_2", 0.5f, N(Oscuro, "dark_spell_cast_charge_impact_{0:00}.ogg", 1, 2, 3));
        G("golpe_3", 0.5f, N(Sagrado, "Holy_spell_blast_cast_{0:00}.ogg", 1, 2, 3));
        G("golpe_4", 0.5f, N(Acido, "acid_spell_cast_squish_ball_impact_{0:00}.ogg", 1, 2, 3, 4));
        G("tajo_0", 0.4f, N(Fuego, "fire_spell_fireball_burst_bright_{0:00}.ogg", 1, 2, 3));
        G("tajo_1", 0.4f, N(Hielo, "Ice_Spell_whoosh_belly_{0:00}.ogg", 1, 2, 3, 4));
        G("tajo_2", 0.4f, N(Oscuro, "dark_spell_cast_energy_wave_{0:00}.ogg", 1, 2, 3));
        G("tajo_3", 0.4f, N(Sagrado, "Holy_spell_wave_cast_{0:00}.ogg", 1, 2, 3));
        G("tajo_4", 0.4f, N(Acido, "acid_spell_cast_splash_liquid_{0:00}.ogg", 1, 2, 3));

        // Escenario del nivel nevado
        G("hielo_congelar", 0.7f, Hielo + "Ice_Spell_charge_impact_01.ogg");
        G("hielo_congelar_lago", 0.85f, Hielo + "Ice_Spell_cast_special_power_01.ogg");
        G("hielo_rebote", 0.6f, CE + "Main Sounds/Sword Collisions/Base Metal/WEAPSwrd_BaseMetal_HoveAud_SwordCombat_03.wav",
          CE + "Main Sounds/Sword Collisions/Base Metal/WEAPSwrd_BaseMetal_HoveAud_SwordCombat_06.wav");
        G("hielo_derretir", 0.7f, N(Fuego, "fire_spell_charge_explosion_wet_{0:00}.ogg", 1, 2, 3));
        G("hielo_romper", 0.9f, Hielo + "Ice_Spell_cast_and_impact_05.ogg");
        G("hielo_estaca", 0.55f, N(Hielo, "Ice_Spell_cast_and_impact_{0:00}.ogg", 1, 2, 3, 4));
        G("hielo_impacto", 0.5f, N(Hielo, "Ice_Spell_fast_impact_{0:00}.ogg", 5, 6, 7, 8));
        G("hielo_conjuro", 0.7f, Hielo + "Ice_Spell_charge_impact_02.ogg");
        G("agua_chapoteo", 0.5f, N(Agua, "water_spell_cast_impact_ball_{0:00}.ogg", 1, 2, 3));
        G("sombra_rebote", 0.6f, Oscuro + "dark_spell_cast_force_energy_field_02.ogg");
        G("sello_roto", 0.8f, Sagrado + "Holy_spell_revive_cast_01.ogg");
        G("ventisca", 0.5f, Viento + "wind_spell_cast_air_long_loop_01.ogg");
        G("ventisca_rafaga", 0.7f, N(Viento, "wind_spell_cast_air_rise_loud_impact_{0:00}.ogg", 1, 2));
        // Ambiente de nevada: el viento de fondo, en bucle.
        G("ambiente_nieve", 0.4f, Viento + "wind_spell_cast_air_long_loop_01.ogg");

        // Menus, hoguera y almas
        G("menu_abrir", 0.5f, Viento + "wind_spell_cast_air_short_spacious_whoosh_01.ogg");
        G("menu_mover", 0.3f, N(Viento, "wind_spell_cast_air_transient_whoosh_{0:00}.ogg", 1, 2, 3));
        G("menu_cancelar", 0.5f, FG + "SFX/Doors Gates and Chests/Chest Close 1.ogg");
        G("menu_error", 0.6f, Arco + "Bow Blocked 1.ogg");
        G("subir_nivel", 0.8f, Sagrado + "Holy_spell_revive_cast_02.ogg");
        G("hoguera_encender", 0.8f, FG + "SFX/Torch/Light Torch 1.ogg");
        G("hoguera_descansar", 0.7f, Sagrado + "Holy_spell_heal_cast_long_01.ogg");
        G("alma_recoger", 0.25f, N(Sagrado, "Holy_spell_heal_dust_cast_evolving_{0:00}.ogg", 1, 2));
        G("alma_mancha", 0.7f, Oscuro + "dark_spell_cast_energy_riser_01.ogg");

        // Enemigos
        G("rata_mordisco", 0.6f, CE + "Main Sounds/Sword Stabs/w_Gore/GOREStab_SwordStabGore_HoveAud_SwordCombat_01.wav");
        G("rata_chillido", 0.5f, Oscuro + "dark_spell_cast_bats_impact_01.ogg");
        G("murcielago_chillido", 0.5f, N(Oscuro, "dark_spell_cast_bats_impact_{0:00}.ogg", 2, 3));
        G("murcielago_onda", 0.6f, Viento + "wind_spell_cast_air_impact_splash_01.ogg");
        G("ojo_escupir", 0.6f, N(Acido, "acid_spell_cast_gas_poison_squish_ball__{0:00}.ogg", 1, 2));
        G("ojo_impacto", 0.5f, Acido + "acid_spell_cast_splash_liquid_02.ogg");
        G("arco_tensar", 0.6f, Arco + "Bow Take Out 1.ogg");
        G("arco_disparo", 0.7f, Arco + "Bow Attack 1.ogg", Arco + "Bow Attack 2.ogg");
        G("flecha_impacto", 0.6f, N(Arco, "Bow Impact Hit {0}.ogg", 1, 2, 3));
        G("sombra_carga", 0.6f, Oscuro + "dark_spell_cast_energy_riser_02.ogg");
        G("sombra_golpe", 0.7f, Oscuro + "dark_spell_cast_dark_energy_ball_01.ogg");
        G("teletransporte", 0.6f, Oscuro + "dark_spell_cast_warp_0-001.ogg", Oscuro + "dark_spell_cast_warp_0-002.ogg", Oscuro + "dark_spell_cast_warp_0-003.ogg");

        // Jefes
        G("jefe_tajo", 0.7f, CE + "Whooshes/WHSH_Whoosh_HoveAud_SwordCombat_07.wav",
          CE + "Main Sounds/Sword Stabs/w_Whoosh/WEAPSwrd_SwordStabwWhoosh_HoveAud_SwordCombat_01.wav",
          CE + "Main Sounds/Sword Stabs/w_Whoosh/WEAPSwrd_SwordStabwWhoosh_HoveAud_SwordCombat_11.wav");
        G("jefe_tajo_fuerte", 0.85f, CE + "Main Sounds/Sword Stabs/Stab w_Ring/WEAPSwrd_SwordStabwRing_HoveAud_SwordCombat_01.wav",
          CE + "Main Sounds/Sword Stabs/Stab w_Ring/WEAPSwrd_SwordStabwRing_HoveAud_SwordCombat_17.wav");
        G("jefe_carga_tajo", 0.5f, N(Viento, "wind_spell_cast_air_rise_cut_{0:00}.ogg", 1, 2));
        G("jefe_golpe_fuerte", 0.85f, CE + "Main Sounds/Sword Stabs/Full Stab Combo/WEAPSwrd_SwordStabCombo_HoveAud_SwordCombat_01.wav");
        G("jefe_impacto_suelo", 0.85f, FG + "SFX/Spells/Rock Meteor Throw 1.ogg", FG + "SFX/Spells/Rock Meteor Throw 2.ogg");
        G("jefe_transformacion", 0.9f, Oscuro + "dark_spell_cast_force_energy_field_03.ogg");
        G("jefe_nova", 0.85f, Fuego + "fire_spell_charge_explosion_dark_01.ogg");
        G("jefe_aparicion", 0.8f, Oscuro + "dark_spell_cast_warp_0-003.ogg");
        G("jefe_parry", 0.8f, CE + "Main Sounds/Sword Collisions/Metal Combo/WEAPSwrd_MetalwWhoosh_HoveAud_SwordCombat_06.wav");
        G("jefe_medialuna", 0.7f, Oscuro + "dark_spell_cast_energy_wave_04.ogg");
        G("jefe_agarre_carga", 0.9f, Oscuro + "dark_spell_cast_energy_riser_metallic_01.ogg");
        G("jefe_agarre", 0.9f, Oscuro + "dark_spell_cast_smash_head_blood_01.ogg");
        G("jefe_corte_agarre", 0.8f, CE + "Main Sounds/Sword Stabs/Stab w_Ring/WEAPSwrd_SwordStabwRing_HoveAud_SwordCombat_11.wav",
          CE + "Main Sounds/Sword Stabs/Stab w_Ring/WEAPSwrd_SwordStabwRing_HoveAud_SwordCombat_17.wav");
        G("jefe_caida", 0.8f, Oscuro + "dark_spell_cast_smash_head_blood_02.ogg");
        G("jefe_revivir_carga", 0.8f, Hielo + "Ice_Spell_long_freeze_loop_01.ogg");
        G("jefe_revivir", 1f, Hielo + "Ice_Spell_cast_special_power_02.ogg");
        G("jefe_postura", 0.9f, CE + "Main Sounds/Sword Collisions/Metal Combo/WEAPSwrd_MetalwWhoosh_HoveAud_SwordCombat_03.wav");
        G("jefe_muerte", 0.9f, Hielo + "Ice_Spell_cast_and_impact_12.ogg");
        G("victoria", 0.8f, Sagrado + "Holy_spell_special_power.ogg");

        foreach (var g in l)
            if (g.clips.Length == 0) Debug.LogWarning("[RPG] Sin clips para el sonido " + g.clave);
        return l;
    }

    // Carga un audio; si es un .wav del pack (no se sube), lo copia antes a Assets/Audio.
    private static AudioClip Audio(string ruta)
    {
        if (ruta.EndsWith(".wav") && ruta.StartsWith(Pack))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Audio")) AssetDatabase.CreateFolder("Assets", "Audio");
            if (!AssetDatabase.IsValidFolder(CopiaAudio)) AssetDatabase.CreateFolder("Assets/Audio", "Combate");
            string destino = CopiaAudio + "/" + Path.GetFileName(ruta);
            if (!File.Exists(destino))
            {
                if (!File.Exists(ruta)) { Debug.LogWarning("[RPG] No encuentro " + ruta); return null; }
                AssetDatabase.CopyAsset(ruta, destino);
            }
            ruta = destino;
        }
        AudioClip a = AssetDatabase.LoadAssetAtPath<AudioClip>(ruta);
        if (a == null) Debug.LogWarning("[RPG] No encuentro el audio " + ruta);
        return a;
    }

    // ------------------------------------------------------------------ Iconos

    private static List<RecursosRPG.EntradaIcono> IconosElegidos()
    {
        var l = new List<RecursosRPG.EntradaIcono>();
        void I(string clave, string carpeta, int n)
        {
            string ruta = Iconos + carpeta + "/icon_" + n.ToString("00") + ".png";
            PrepararIcono(ruta);
            Sprite s = AssetDatabase.LoadAllAssetsAtPath(ruta).OfType<Sprite>().FirstOrDefault();
            if (s == null) Debug.LogWarning("[RPG] No encuentro el icono " + ruta);
            l.Add(new RecursosRPG.EntradaIcono { clave = clave, sprite = s });
        }
        // Elementos de la rueda (en el orden de Elemento).
        I("elemento_0", "07-magic-spells", 0);    // fuego
        I("elemento_1", "07-magic-spells", 1);    // cristales de hielo
        I("elemento_2", "07-magic-spells", 4);    // vortice oscuro
        I("elemento_3", "07-magic-spells", 5);    // sol
        I("elemento_4", "15-status-effects", 32); // calavera verde (acido)
        // Estados de los enemigos
        I("estado_quemado", "15-status-effects", 33);
        I("estado_lento", "15-status-effects", 18);
        I("estado_congelado", "15-status-effects", 34);
        I("estado_aturdido", "15-status-effects", 61);
        I("estado_corroido", "15-status-effects", 16);
        I("estado_drenado", "15-status-effects", 26);
        // Hoguera
        I("stat_vida", "15-status-effects", 57);
        I("stat_estamina", "15-status-effects", 40);
        I("stat_mana", "07-magic-spells", 28);
        I("stat_reshechizos", "15-status-effects", 22);
        I("stat_resgolpes", "02-armor", 60);
        // Otros
        I("pocion", "03-potions-consumables", 0);
        I("almas", "07-magic-spells", 25);
        I("peligro", "15-status-effects", 39);
        return l;
    }

    private static void PrepararIcono(string ruta)
    {
        TextureImporter ti = AssetImporter.GetAtPath(ruta) as TextureImporter;
        if (ti == null) return;
        if (ti.textureType == TextureImporterType.Sprite && ti.filterMode == FilterMode.Point && ti.spritePixelsPerUnit == 32f
            && ti.textureCompression == TextureImporterCompression.Uncompressed) return;
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = 32f;
        ti.filterMode = FilterMode.Point;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
        ti.SaveAndReimport();
    }

    // ------------------------------------------------------------------ Tajos

    private static AnimadorHoja.Clip[] TajosPorElemento()
    {
        var l = new List<AnimadorHoja.Clip>();
        Vector2 c = new Vector2(0.5f, 0.5f);
        foreach (Elemento e in Elementos.Todos)
        {
            int color = Elementos.ColorTajo(e);
            l.Add(Clip("tajo_h_" + e, Tajos + "Slash 1/color" + color + "/Frames", 24f, false, 64f, c));
            l.Add(Clip("tajo_c_" + e, Tajos + "Slash 3/color" + color + "/frames", 24f, false, 64f, c));
            l.Add(Clip("tajo_a_" + e, Tajos + "Slash 2/color" + color + "/Frames", 22f, false, 64f, c));
        }
        return l.ToArray();
    }

    // Efecto de fotogramas sueltos: todos los PNG "...frameN" de una carpeta, en orden.
    public static AnimadorHoja.Clip Clip(string nombre, string ruta, float fps, bool bucle, float ppu, Vector2 pivote,
                                         int desde = 0, int hasta = -1)
    {
        if (!Directory.Exists(ruta)) { Debug.LogWarning("[RPG] No existe " + ruta); return null; }
        var archivos = Directory.GetFiles(ruta, "*.png")
            .Where(f => Path.GetFileNameWithoutExtension(f).ToLower().Contains("frame"))
            .OrderBy(f => int.Parse(new string(Path.GetFileNameWithoutExtension(f).Reverse().TakeWhile(char.IsDigit).Reverse().ToArray())))
            .Select(f => f.Replace('\\', '/')).ToList();
        foreach (string f in archivos) PrepararPixel(f);
        var texturas = archivos.Select(f => AssetDatabase.LoadAssetAtPath<Texture2D>(f)).Where(t => t != null).ToList();
        if (hasta < 0 || hasta >= texturas.Count) hasta = texturas.Count - 1;
        var elegidos = texturas.Skip(desde).Take(Mathf.Max(0, hasta - desde + 1)).ToArray();
        return new AnimadorHoja.Clip
        {
            nombre = nombre, fotogramas = elegidos, cantidad = elegidos.Length, fps = fps, bucle = bucle,
            pivote = pivote, pixelesPorUnidad = ppu,
        };
    }

    // Pixel art nitido: sin filtrado ni compresion.
    public static void PrepararPixel(string ruta, float ppu = 0f)
    {
        TextureImporter ti = AssetImporter.GetAtPath(ruta) as TextureImporter;
        if (ti == null) return;
        bool ok = ti.filterMode == FilterMode.Point && ti.textureCompression == TextureImporterCompression.Uncompressed
                  && ti.maxTextureSize >= 4096 && (ppu <= 0f || Mathf.Approximately(ti.spritePixelsPerUnit, ppu));
        if (ok) return;
        ti.maxTextureSize = 4096;
        ti.filterMode = FilterMode.Point;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.mipmapEnabled = false;
        if (ppu > 0f) ti.spritePixelsPerUnit = ppu;
        ti.SaveAndReimport();
    }
}
