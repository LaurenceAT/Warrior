using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Dos desafios nuevos (menu Warrior > Desafios nuevos > Crear o actualizar):
//
//   1. Bringer of Death, en la cueva: la escena "Cueva Bringer" es una copia de
//      "Nivel Cueva" (roca, fondo, oscuridad y ambiente de siempre) con el
//      Bringer en la arena del Wraith. En el desafio, ModoDesafio deja solo la
//      ultima hoguera antes de la arena y quita enemigos, cofres y portales.
//   2. Duo Martial Hero, en el bosque: "Bosque Duo Martial" es una copia de
//      "Bosque Cazadora" con una ArenaJefe normal en vez de la de la Cazadora y
//      DOS Martial Hero (el de siempre y uno carmesi, +20 % de vida cada uno),
//      cada uno con su barra.
//
// Ademas: sus fichas de Desafios (Resources/Desafios), 4 logros (Normal y
// Dificil de cada uno), su musica (Config Nivel Bringer / Duo Martial), la
// imagen del menu y las dos escenas en Build Settings.
//
// No pisa lo editado: escenas, fichas, logros, configs y prefabs solo se crean
// si no existen ("Rehacer escenas" vuelve a copiar las escenas).
public static class CrearDesafiosNuevos
{
    public const string EscenaBringer = "Assets/Scenes/Cueva Bringer.unity";
    public const string EscenaDuo = "Assets/Scenes/Bosque Duo Martial.unity";
    private const string EscenaCueva = "Assets/Scenes/Nivel Cueva.unity";
    private const string EscenaBosque = "Assets/Scenes/Bosque Cazadora.unity";

    public const string FichaBringer = "Assets/Resources/Desafios/Jefe_BringerOfDeath.asset";
    public const string FichaDuo = "Assets/Resources/Desafios/Jefe_DuoMartialHero.asset";
    public const string IdBringer = "bringer_of_death";
    public const string IdDuo = "duo_martial_hero";

    public const string AjustesDuo = "Assets/Data/Jefes/Ajustes Martial Hero Duo.asset";
    public const string PrefabDuo1 = "Assets/Prefabs/Enemies/Nuevos/JefeMartialHeroDuo.prefab";
    public const string PrefabDuo2 = "Assets/Prefabs/Enemies/Nuevos/JefeMartialHeroCarmesi.prefab";
    public const string CarpetaCarmesi = "Assets/Sprites/MartialHero/Carmesi";
    public const float VidaDuo = 1.2f;
    // Ondas del duo: medias lunas del pack generico (sin usar), en blanco y en rojo.
    private const string OndaOriginal = "Assets/SPRITES PARA NUEVOS NIVELES/EFECTOS DE ATAQUE DEL PLAYER/Efecto_ImpactosGenericosPixelFX/Free/Part 9/438.png";
    public const string HojaOnda = "Assets/Sprites/MartialHero/Onda.png";
    public const int FilaOndaBlanca = 5, FilaOndaRoja = 7;

    private const string ConfigCueva = "Assets/Data/Niveles/Config Nivel Cueva.asset";
    private const string ConfigBosque = "Assets/Data/Niveles/Config Nivel Bosque.asset";
    public const string ConfigBringer = "Assets/Data/Niveles/Config Nivel Bringer.asset";
    public const string ConfigDuo = "Assets/Data/Niveles/Config Nivel Duo Martial.asset";
    private const string Musica = "Assets/SPRITES PARA NUEVOS NIVELES/SONIDOS/Sonidos_MusicaBosses/";
    private const string MusicaBringer = Musica + "Sonidos_BossFightPack/02 - The Hooded March - The hooded March.ogg";
    private const string MusicaDuo = Musica + "Sonidos_BossThemesNuevos/Boss Theme 5.mp3";

    private const string ImagenBringer = "Assets/Sprites/Desafios/Jefe_BringerOfDeath.png";
    private const string ImagenDuo = "Assets/Sprites/Desafios/Jefe_DuoMartialHero.png";
    private const string Iconos = "Assets/SPRITES PARA NUEVOS NIVELES/ICONOS/Iconos_RPGPack/08-loot-treasure/";

    [MenuItem("Warrior/Desafios nuevos/Crear o actualizar", false, 60)]
    public static void Crear() => Hacer(false);

    [MenuItem("Warrior/Desafios nuevos/Rehacer escenas", false, 61)]
    public static void RehacerEscenas() => Hacer(true);

    private static void Hacer(bool rehacerEscenas)
    {
        if (EditorSceneManager.GetActiveScene().isDirty && !Application.isBatchMode &&
            !EditorUtility.DisplayDialog("Desafios nuevos", "La escena abierta tiene cambios sin guardar. Se cerrara sin guardarlos. ¿Seguir?", "Seguir", "Cancelar"))
            return;
        string abierta = EditorSceneManager.GetActiveScene().path;

        CrearBringerOfDeath.Crear();
        CrearMartialHero.Crear();
        Recolorear();
        if (!File.Exists(HojaOnda)) { File.Copy(OndaOriginal, HojaOnda); AssetDatabase.ImportAsset(HojaOnda); }
        PixelArt(HojaOnda, false);
        GameObject duo1 = PrefabDuo(PrefabDuo1, false);
        GameObject duo2 = PrefabDuo(PrefabDuo2, true);
        ConfigNivel cb = Config(ConfigBringer, ConfigCueva, MusicaBringer);
        ConfigNivel cd = Config(ConfigDuo, ConfigBosque, MusicaDuo);
        Sprite ib = Imagen(ImagenBringer, false);
        Sprite id = Imagen(ImagenDuo, true);
        Fichas(ib, id);
        Logros();
        AssetDatabase.SaveAssets();

        EscenaDelBringer(cb, rehacerEscenas);
        EscenaDelDuo(duo1, duo2, cd, rehacerEscenas);
        Build();
        AssetDatabase.SaveAssets();
        if (!string.IsNullOrEmpty(abierta) && File.Exists(abierta) && !Application.isBatchMode) EditorSceneManager.OpenScene(abierta);
        Debug.Log("[DesafiosNuevos] Listo: Cueva Bringer y Bosque Duo Martial, fichas, 4 logros, musica y Build Settings.");
    }

    // ------------------------------------------------------------------ Martial Hero carmesi

    // Copias de las tiras con la ropa morada y gris pasada a carmesi (los
    // acentos rojos, la piel y lo oscuro se quedan).
    public static Color Carmesi(Color c)
    {
        if (c.a <= 0.01f) return c;
        Color.RGBToHSV(c, out float h, out float s, out float v);
        float g = h * 360f;
        if (g >= 225f && g <= 310f) { h = 352f / 360f; s = Mathf.Min(1f, s * 1.15f + 0.2f); v = Mathf.Min(1f, v * 1.05f); }
        else if ((g >= 315f || g <= 15f) && s < 0.3f && v > 0.35f) { h = 356f / 360f; s = Mathf.Min(1f, s + 0.4f); v *= 0.9f; }
        Color r = Color.HSVToRGB(h, s, v);
        r.a = c.a;
        return r;
    }

    private static void Recolorear()
    {
        Directory.CreateDirectory(CarpetaCarmesi);
        bool nuevos = false;
        foreach (string t in CrearMartialHero.Tiras)
        {
            string destino = $"{CarpetaCarmesi}/{t}.png";
            if (File.Exists(destino)) continue;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(File.ReadAllBytes($"{CrearMartialHero.CarpetaSprites}/{t}.png"));
            Color[] px = tex.GetPixels();
            for (int i = 0; i < px.Length; i++) px[i] = Carmesi(px[i]);
            tex.SetPixels(px);
            File.WriteAllBytes(destino, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            nuevos = true;
        }
        if (nuevos) AssetDatabase.Refresh();
        foreach (string t in CrearMartialHero.Tiras) PixelArt($"{CarpetaCarmesi}/{t}.png", false);
    }

    private static void PixelArt(string ruta, bool sprite)
    {
        TextureImporter ti = AssetImporter.GetAtPath(ruta) as TextureImporter;
        if (ti == null) return;
        TextureImporterType tipo = sprite ? TextureImporterType.Sprite : TextureImporterType.Default;
        if (ti.textureType == tipo && ti.filterMode == FilterMode.Point && !ti.mipmapEnabled
            && ti.textureCompression == TextureImporterCompression.Uncompressed && ti.npotScale == TextureImporterNPOTScale.None) return;
        ti.textureType = tipo;
        if (sprite) ti.spriteImportMode = SpriteImportMode.Single;
        ti.alphaIsTransparency = true;
        ti.npotScale = TextureImporterNPOTScale.None;
        ti.mipmapEnabled = false;
        ti.filterMode = FilterMode.Point;
        ti.wrapMode = TextureWrapMode.Clamp;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.maxTextureSize = 2048;
        ti.SaveAndReimport();
    }

    // Los dos del duo: copias del Martial Hero con su propia ficha (+20 % de vida);
    // el segundo con las tiras carmesi. Si ya existen, solo se ponen al dia sus clips.
    private static GameObject PrefabDuo(string ruta, bool carmesi)
    {
        AjustesMartialHero aj = AssetDatabase.LoadAssetAtPath<AjustesMartialHero>(AjustesDuo);
        if (aj == null)
        {
            AssetDatabase.CopyAsset(CrearMartialHero.RutaFicha, AjustesDuo);
            aj = AssetDatabase.LoadAssetAtPath<AjustesMartialHero>(AjustesDuo);
            AjustesMartialHero original = AssetDatabase.LoadAssetAtPath<AjustesMartialHero>(CrearMartialHero.RutaFicha);
            aj.vida = Mathf.RoundToInt(original.vida * VidaDuo);
            EditorUtility.SetDirty(aj);
        }
        // Es la ficha del duo: siempre pelea en pareja.
        if (!aj.duo) { aj.duo = true; EditorUtility.SetDirty(aj); }
        bool existe = File.Exists(ruta);
        GameObject raiz = PrefabUtility.LoadPrefabContents(existe ? ruta : CrearMartialHero.RutaPrefab);
        raiz.name = Path.GetFileNameWithoutExtension(ruta);
        AnimadorHoja an = raiz.GetComponentInChildren<AnimadorHoja>();
        if (an != null)
        {
            an.clips = CrearMartialHero.Clips(aj);
            if (carmesi)
                foreach (AnimadorHoja.Clip c in an.clips)
                    if (c.hoja != null) c.hoja = AssetDatabase.LoadAssetAtPath<Texture2D>($"{CarpetaCarmesi}/{Path.GetFileName(AssetDatabase.GetAssetPath(c.hoja))}");
        }
        JefeMartialHero j = raiz.GetComponent<JefeMartialHero>();
        // La onda: una media luna de 64x64 (cuadro 2 de su fila), blanca o roja.
        j.fxOnda = new AnimadorHoja.Clip
        {
            nombre = "onda", hoja = AssetDatabase.LoadAssetAtPath<Texture2D>(HojaOnda), anchoCelda = 64, altoCelda = 64,
            fila = carmesi ? FilaOndaRoja : FilaOndaBlanca, primero = 2, cantidad = 1, fps = 10f, bucle = true,
            pivote = new Vector2(0.5f, 0.5f), pixelesPorUnidad = 32f,
        };
        SerializedObject sj = new SerializedObject(j);
        sj.FindProperty("ajustes").objectReferenceValue = aj;
        sj.ApplyModifiedPropertiesWithoutUndo();
        SerializedObject sh = new SerializedObject(raiz.GetComponent<EnemyHealth>());
        sh.FindProperty("maxHealth").intValue = aj.vida;
        sh.ApplyModifiedPropertiesWithoutUndo();
        GameObject p = PrefabUtility.SaveAsPrefabAsset(raiz, ruta);
        PrefabUtility.UnloadPrefabContents(raiz);
        return p;
    }

    // ------------------------------------------------------------------ Musica e imagen

    private static ConfigNivel Config(string ruta, string plantilla, string musica)
    {
        ConfigNivel c = AssetDatabase.LoadAssetAtPath<ConfigNivel>(ruta);
        if (c != null) return c;
        AssetDatabase.CopyAsset(plantilla, ruta);
        c = AssetDatabase.LoadAssetAtPath<ConfigNivel>(ruta);
        AudioClip pista = AssetDatabase.LoadAssetAtPath<AudioClip>(musica);
        if (pista == null) Debug.LogWarning("[DesafiosNuevos] Falta la musica " + musica);
        c.jefeFase1 = new ConfigNivel.MusicaFase { pista = pista, volumen = 0.6f };
        c.jefeFase2 = new ConfigNivel.MusicaFase();
        c.frasesJefe = new string[0];
        EditorUtility.SetDirty(c);
        return c;
    }

    // Imagen del menu: el primer cuadro quieto, recortado (el duo: los dos, el carmesi detras).
    private static Sprite Imagen(string ruta, bool duo)
    {
        if (!File.Exists(ruta))
        {
            Texture2D salida;
            if (!duo)
            {
                Texture2D t = Leer($"{CrearBringerOfDeath.CarpetaSprites}/Idle/1.png");
                salida = Recortar(t, new RectInt(80, 0, 50, 57));
            }
            else
            {
                Texture2D t = Leer($"{CrearMartialHero.CarpetaSprites}/Idle.png");
                // Cuadro 0 de 200x200; el cuerpo esta en x 60-130, y 78-170 desde abajo.
                Texture2D uno = Recortar(t, new RectInt(55, 74, 80, 100));
                salida = new Texture2D(110, 100, TextureFormat.RGBA32, false);
                salida.SetPixels(Enumerable.Repeat(Color.clear, 110 * 100).ToArray());
                for (int y = 0; y < 100; y++)
                    for (int x = 0; x < 80; x++)
                    {
                        Color c = Carmesi(uno.GetPixel(x, y));
                        // El carmesi, detras y mirando al otro lado.
                        if (c.a > 0.01f) salida.SetPixel(109 - x, y, c * new Color(0.8f, 0.8f, 0.8f, 1f));
                    }
                for (int y = 0; y < 100; y++)
                    for (int x = 0; x < 80; x++)
                    {
                        Color c = uno.GetPixel(x, y);
                        if (c.a > 0.01f) salida.SetPixel(x, y, c);
                    }
            }
            salida.Apply();
            File.WriteAllBytes(ruta, salida.EncodeToPNG());
            AssetDatabase.ImportAsset(ruta);
        }
        PixelArt(ruta, true);
        return AssetDatabase.LoadAssetAtPath<Sprite>(ruta);
    }

    private static Texture2D Leer(string ruta)
    {
        var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        t.LoadImage(File.ReadAllBytes(ruta));
        return t;
    }

    private static Texture2D Recortar(Texture2D t, RectInt r)
    {
        var s = new Texture2D(r.width, r.height, TextureFormat.RGBA32, false);
        s.SetPixels(t.GetPixels(r.x, r.y, r.width, r.height));
        s.Apply();
        return s;
    }

    // ------------------------------------------------------------------ Fichas de Desafios

    private static void Fichas(Sprite ib, Sprite id)
    {
        if (AssetDatabase.LoadAssetAtPath<FichaJefe>(FichaBringer) == null)
        {
            FichaJefe f = ScriptableObject.CreateInstance<FichaJefe>();
            f.id = IdBringer;
            f.nombre = "Bringer of Death";
            f.escena = Path.GetFileNameWithoutExtension(EscenaBringer);
            f.orden = 3;
            f.imagen = ib;
            f.afinidades = new List<FichaJefe.Afinidad>
            {
                new FichaJefe.Afinidad { elemento = Elemento.Sagrado, multiplicador = 1.5f, cuando = "Fase 1" },
                new FichaJefe.Afinidad { elemento = Elemento.Sagrado, multiplicador = 1.8f, cuando = "Fase 2" },
                new FichaJefe.Afinidad { elemento = Elemento.Fuego, multiplicador = 1.1f, cuando = "Siempre" },
                new FichaJefe.Afinidad { elemento = Elemento.Sangrado, multiplicador = 0.8f, cuando = "Siempre" },
                new FichaJefe.Afinidad { elemento = Elemento.Oscuro, multiplicador = 0.5f, cuando = "Siempre" },
            };
            f.notaAfinidades = "Mientras es humo no le entra nada. Dos parrys seguidos a su guadaña lo dejan aturdido.";
            f.ataques = new List<FichaJefe.Ataque>
            {
                A(JefeBringer.Guadana, "Guadañazo", 1, "Alza la guadaña, la sostiene y barre todo lo que tiene delante. A veces vuelve a cortar, y el segundo tajo llega tarde a propósito.",
                  "El primer tajo se para con un parry; si lo consigues, se tambalea y no hay segundo. Si lo esquivas, espera: el segundo corte castiga al que se lanza enseguida."),
                A(JefeBringer.Mano, "Mano del abismo", 1, "Abre un portal sobre ti que te sigue un momento. Cuando deja de moverse, cae una mano negra.",
                  "Sigue andando mientras el portal te persigue; cuando se quede quieto y brille, sal de debajo. En la fase 2 caen tres seguidas."),
                A(JefeBringer.MediaLuna, "Media luna", 1, "Corta el aire y la hoja suelta una media luna verde que cruza la arena, baja o alta.",
                  "La baja se salta; la alta pasa por encima si te quedas en el suelo. Un parry la deshace."),
                A(JefeBringer.Ola, "Ola de almas", 1, "Clava la mirada en el suelo y una ola de almas verdes avanza hacia ti, estallido tras estallido, hasta la pared.",
                  "Salta justo cuando el estallido de delante va a tocarte. No intentes correr más que ella."),
                A(JefeBringer.Desvanecer, "Desvanecerse", 1, "Se deshace en humo y vuelve a aparecer a tu espalda con la guadaña ya en alto.",
                  "Cuando se convierta en humo, date la vuelta. Su tajo por la espalda avisa menos, pero se puede parar igual."),
                A(JefeBringer.Cosecha, "Cosecha", 1, "Llena la arena de portales. Solo quedan unos pocos huecos sin portal, y las manos caen en barrido de un lado al otro.",
                  "Busca el hueco sin portal en cuanto aparezcan. En la fase 2 solo queda uno."),
                A(JefeBringer.Circulo, "Círculo de condena", 2, "Marca el suelo bajo tus pies con un anillo verde, y otro, y otro. Cada uno revienta poco después.",
                  "No te quedes quieto: cada anillo te sigue a donde estabas, no a donde vas."),
                A(JefeBringer.Espiritus, "Espíritus", 2, "Suelta tres almas que te persiguen girando despacio hasta que se apagan.",
                  "Giran mal: un cambio de dirección brusco las hace pasar de largo. Un parry deshace una."),
                A(JefeBringer.Cometas, "Lluvia de cometas", 2, "Cae una lluvia de cometas por toda la arena. Cada uno marca antes el suelo, y alguno cae justo donde estás.",
                  "Mira las marcas moradas y muévete entre ellas; quedarse quieto es esperar al cometa que va a por ti."),
                A(JefeBringer.Esfera, "Esfera del vacío", 2, "Abre una esfera de estrellas a tu lado que te arrastra hacia ella y luego implosiona. Si te alcanza, te congela.",
                  "Tira hacia el lado contrario desde el primer momento y, al final, esquiva o salta lejos."),
                A(JefeBringer.Juicio, "Juicio", 2, "Todo el suelo de la arena se llena de anillos, por tramos: estallan unos, luego los de al lado, luego los primeros otra vez.",
                  "Ponte en un tramo sin anillo y cambia al de al lado justo después de cada estallido."),
                A(JefeBringer.Marea, "Marea de almas", 2, "Dos olas de almas salen de las dos paredes, una detrás de otra.",
                  "Son dos saltos: el de la primera ola y, enseguida, el de la segunda, que viene del otro lado."),
            };
            f.fases = new List<FichaJefe.FaseInfo>
            {
                new FichaJefe.FaseInfo { numero = 1, nombre = "La siega", descripcion = "Camina hacia ti sin prisa y deja que la guadaña haga el resto." },
                new FichaJefe.FaseInfo { numero = 2, nombre = "El juicio", descripcion = "A media vida se detiene y conjura. Desde ahí todo va más rápido y la arena entera se vuelve suya." },
            };
            f.fragmentosHistoria = new List<FichaJefe.Fragmento>
            {
                new FichaJefe.Fragmento { id = "h1", cuando = FichaJefe.Cuando.PrimerIntento, texto = "En la cueva nadie lo llamó. Llegó igual." },
                new FichaJefe.Fragmento { id = "h2", cuando = FichaJefe.Cuando.Fase2, texto = "Dicen que no mata: solo recoge lo que ya estaba a punto de caer. Por eso no corre." },
                new FichaJefe.Fragmento { id = "h3", cuando = FichaJefe.Cuando.PrimeraVictoria, texto = "Cuando se deshace en humo deja un ojo rojo flotando. Mira un momento. Luego se apaga, como si hubiera visto lo que necesitaba." },
                new FichaJefe.Fragmento { id = "h4", cuando = FichaJefe.Cuando.VictoriaDificil, texto = "Volverá. Siempre vuelve. Pero esta vez se fue con las manos vacías." },
            };
            f.almasCofre = 3000;
            f.objetosCofre = new[] { Equipo.Objeto.PiedraForja, Equipo.Objeto.LagrimaCarmesi, Equipo.Objeto.FrascoSangre };
            AssetDatabase.CreateAsset(f, FichaBringer);
        }
        if (AssetDatabase.LoadAssetAtPath<FichaJefe>(FichaDuo) == null)
        {
            FichaJefe f = ScriptableObject.CreateInstance<FichaJefe>();
            f.id = IdDuo;
            f.nombre = "Duo Martial Hero";
            f.escena = Path.GetFileNameWithoutExtension(EscenaDuo);
            f.orden = 4;
            f.imagen = id;
            f.afinidades = new List<FichaJefe.Afinidad>();
            f.notaAfinidades = "Ningún elemento les hace más ni menos. De frente, en guardia, apenas les entra nada; por la espalda, el golpe entero. El sagrado rompe la guardia.";
            f.ataques = new List<FichaJefe.Ataque>
            {
                A("combo_cuadruple", "Combo cuádruple", 1, "Desenvaina y corta cuatro veces seguidas: desenvaine, tajo, desenvaine, tajo. Y ahora son dos haciéndolo.",
                  "Para los cuatro golpes y el que ataca queda aturdido. Mientras, el otro sigue: no le des la espalda al que espera."),
            };
            f.fases = new List<FichaJefe.FaseInfo>
            {
                new FichaJefe.FaseInfo { numero = 1, nombre = "Dos filos", descripcion = "Se cubren, esperan y atacan cuando entras en su distancia. Uno tapa los huecos del otro." },
            };
            f.fragmentosHistoria = new List<FichaJefe.Fragmento>
            {
                new FichaJefe.Fragmento { id = "h1", cuando = FichaJefe.Cuando.PrimerIntento, texto = "Entrenaron juntos tanto tiempo que ya no recuerdan quién enseñó a quién." },
                new FichaJefe.Fragmento { id = "h2", cuando = FichaJefe.Cuando.PrimeraVictoria, texto = "El de rojo llegó al bosque cuando el otro ya llevaba años esperando a alguien digno. Desde entonces esperan los dos." },
                new FichaJefe.Fragmento { id = "h3", cuando = FichaJefe.Cuando.VictoriaDificil, texto = "Cayeron a la vez, como habían vivido. Ninguno soltó la espada." },
            };
            f.almasCofre = 2500;
            f.objetosCofre = new[] { Equipo.Objeto.PiedraForja, Equipo.Objeto.LagrimaCeleste, Equipo.Objeto.FrascoSangre };
            AssetDatabase.CreateAsset(f, FichaDuo);
        }
    }

    private static FichaJefe.Ataque A(string id, string nombre, int fase, string desc, string consejo) =>
        new FichaJefe.Ataque { id = id, nombre = nombre, fase = fase, descripcion = desc, consejo = consejo };

    // ------------------------------------------------------------------ Logros

    private static void Logros()
    {
        // Iconos del pack que aun no usa ningun logro.
        var usados = new HashSet<string>(FichaLogro.Todos().Where(l => l.icono != null).Select(l => AssetDatabase.GetAssetPath(l.icono)));
        var libres = new Queue<Sprite>(new[] { 30, 31, 40, 41, 44, 45, 47, 48 }
            .Select(n => Iconos + $"icon_{n}.png").Where(r => !usados.Contains(r))
            .Select(r => AssetDatabase.LoadAllAssetsAtPath(r).OfType<Sprite>().FirstOrDefault()).Where(s => s != null));
        Logro("Desafios_4_prueba_guadana", "prueba_guadana", FichaLogro.Seccion.Desafios, 4, "La siega interrumpida",
              "Completa el desafío de Bringer of Death.", FichaLogro.Condicion.CompletarDesafio, IdBringer, libres);
        Logro("Desafios_5_prueba_duo", "prueba_duo", FichaLogro.Seccion.Desafios, 5, "Dos filos, un silencio",
              "Completa el desafío de Duo Martial Hero.", FichaLogro.Condicion.CompletarDesafio, IdDuo, libres);
        Logro("Dificil_3_cicatriz_guadana", "cicatriz_guadana", FichaLogro.Seccion.Dificil, 3, "Cicatriz de la guadaña",
              "Derrota a Bringer of Death en dificultad Difícil.", FichaLogro.Condicion.CompletarDesafioDificil, IdBringer, libres);
        Logro("Dificil_4_cicatriz_duo", "cicatriz_duo", FichaLogro.Seccion.Dificil, 4, "Cicatriz doble",
              "Derrota a Duo Martial Hero en dificultad Difícil.", FichaLogro.Condicion.CompletarDesafioDificil, IdDuo, libres);
    }

    private static void Logro(string archivo, string id, FichaLogro.Seccion seccion, int orden, string nombre, string desc,
                              FichaLogro.Condicion cond, string parametro, Queue<Sprite> iconos)
    {
        string ruta = $"Assets/Resources/Logros/{archivo}.asset";
        if (AssetDatabase.LoadAssetAtPath<FichaLogro>(ruta) != null) return;
        FichaLogro l = ScriptableObject.CreateInstance<FichaLogro>();
        l.id = id; l.seccion = seccion; l.orden = orden; l.nombre = nombre; l.descripcion = desc;
        l.condicion = cond; l.parametro = parametro;
        l.icono = iconos.Count > 0 ? iconos.Dequeue() : null;
        AssetDatabase.CreateAsset(l, ruta);
    }

    // ------------------------------------------------------------------ Escenas

    // Copia de la cueva con el Bringer en la arena del Wraith.
    private static void EscenaDelBringer(ConfigNivel config, bool rehacer)
    {
        bool nueva = rehacer || !File.Exists(EscenaBringer);
        if (nueva)
        {
            if (File.Exists(EscenaBringer)) AssetDatabase.DeleteAsset(EscenaBringer);
            if (!AssetDatabase.CopyAsset(EscenaCueva, EscenaBringer)) { Debug.LogError("[DesafiosNuevos] No se pudo copiar " + EscenaCueva); return; }
        }
        Scene e = EditorSceneManager.OpenScene(EscenaBringer, OpenSceneMode.Single);
        ArenaJefe arena = Object.FindObjectsByType<ArenaJefe>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(a => a.EsPrincipal);
        if (arena == null) { Debug.LogError("[DesafiosNuevos] La cueva no tiene la arena del jefe"); return; }
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CrearBringerOfDeath.RutaPrefab);
        SerializedObject so = new SerializedObject(arena);
        so.FindProperty("prefabJefe").objectReferenceValue = prefab.GetComponent<JefeBase>();
        if (nueva)
        {
            so.FindProperty("nombre").stringValue = "Bringer of Death";
            so.FindProperty("titulo").stringValue = "El que viene a recoger";
            so.FindProperty("burlas").arraySize = 0;
            so.FindProperty("marcaFase").floatValue = 0.5f;
            so.FindProperty("barraPorFase").boolValue = false;
            so.FindProperty("textoFase2").stringValue = "La muerte se impacienta...";
            so.FindProperty("luzFase2").colorValue = new Color(0.7f, 0.55f, 1f, 1f);
            so.FindProperty("particulasFase2A").colorValue = new Color(0.55f, 0.25f, 0.95f, 0.9f);
            so.FindProperty("particulasFase2B").colorValue = new Color(0.3f, 1f, 0.7f, 0.7f);
            so.FindProperty("particulasCaen").boolValue = false;
            so.FindProperty("recompensa").arraySize = 0;
            so.FindProperty("musica").objectReferenceValue = null;
            so.FindProperty("musicaFase2").objectReferenceValue = null;
        }
        so.FindProperty("config").objectReferenceValue = config;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(e);
        EditorSceneManager.SaveScene(e);
    }

    // Copia del bosque de la Cazadora con una arena normal y los dos Martial Hero.
    private static void EscenaDelDuo(GameObject duo1, GameObject duo2, ConfigNivel config, bool rehacer)
    {
        bool nueva = rehacer || !File.Exists(EscenaDuo);
        if (nueva)
        {
            if (File.Exists(EscenaDuo)) AssetDatabase.DeleteAsset(EscenaDuo);
            if (!AssetDatabase.CopyAsset(EscenaBosque, EscenaDuo)) { Debug.LogError("[DesafiosNuevos] No se pudo copiar " + EscenaBosque); return; }
        }
        Scene e = EditorSceneManager.OpenScene(EscenaDuo, OpenSceneMode.Single);
        ArenaJefe arena = Object.FindFirstObjectByType<ArenaJefe>(FindObjectsInactive.Include);
        if (arena == null)
        {
            ArenaCazadora vieja = Object.FindFirstObjectByType<ArenaCazadora>(FindObjectsInactive.Include);
            if (vieja == null) { Debug.LogError("[DesafiosNuevos] El bosque no tiene la arena de la Cazadora"); return; }
            GameObject go = vieja.gameObject;
            SerializedObject sv = new SerializedObject(vieja);
            Rect zona = sv.FindProperty("zona").rectValue;
            float suelo = sv.FindProperty("suelo").floatValue;
            Object.DestroyImmediate(vieja);
            CamaraCazadora cc = go.GetComponent<CamaraCazadora>();
            if (cc != null) Object.DestroyImmediate(cc);
            go.name = "ArenaJefe";
            arena = go.AddComponent<ArenaJefe>();
            SerializedObject so = new SerializedObject(arena);
            so.FindProperty("nombre").stringValue = "Duo Martial Hero";
            so.FindProperty("titulo").stringValue = "Dos filos, una sola espera";
            so.FindProperty("nombreJefe1").stringValue = "Martial Hero";
            so.FindProperty("nombreJefe2").stringValue = "Martial Hero carmesí";
            so.FindProperty("separacionDuo").floatValue = 7f;
            so.FindProperty("zona").rectValue = zona;
            so.FindProperty("suelo").floatValue = suelo;
            so.FindProperty("burlas").arraySize = 0;
            so.FindProperty("marcaFase").floatValue = 0f;
            so.FindProperty("barraPorFase").boolValue = false;
            so.FindProperty("textoFase2").stringValue = "";
            so.FindProperty("bannerVictoria").stringValue = "DÚO CAÍDO";
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        SerializedObject sa = new SerializedObject(arena);
        sa.FindProperty("prefabJefe").objectReferenceValue = duo1.GetComponent<JefeBase>();
        sa.FindProperty("prefabJefe2").objectReferenceValue = duo2.GetComponent<JefeBase>();
        sa.FindProperty("config").objectReferenceValue = config;
        sa.ApplyModifiedPropertiesWithoutUndo();

        // La zona de entrada (muro de niebla) pasa a esta arena.
        foreach (ZonaJefe z in Object.FindObjectsByType<ZonaJefe>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            SerializedObject sz = new SerializedObject(z);
            sz.FindProperty("arena").objectReferenceValue = arena;
            sz.ApplyModifiedPropertiesWithoutUndo();
        }
        // Las frases de la pantalla de muerte, de su config.
        foreach (DatosNivel d in Object.FindObjectsByType<DatosNivel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            SerializedObject sd = new SerializedObject(d);
            sd.FindProperty("config").objectReferenceValue = config;
            sd.ApplyModifiedPropertiesWithoutUndo();
        }
        EditorSceneManager.MarkSceneDirty(e);
        EditorSceneManager.SaveScene(e);
    }

    // Las dos escenas, despues de las de siempre.
    private static void Build()
    {
        var escenas = EditorBuildSettings.scenes.ToList();
        foreach (string r in new[] { EscenaBringer, EscenaDuo })
            if (!escenas.Any(s => s.path == r)) escenas.Add(new EditorBuildSettingsScene(r, true));
        EditorBuildSettings.scenes = escenas.ToArray();
    }
}
