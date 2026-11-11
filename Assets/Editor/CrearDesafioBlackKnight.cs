using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// El desafio del Black Knight (menu Warrior > Black Knight > Desafio).
//
// La escena "Bosque Black Knight" es una copia de "Bosque Cazadora" (el bosque,
// su fondo y su ambiente) con una ArenaJefe normal en vez de la de la Cazadora y
// el Black Knight dentro, con una barra por fase (la 2 la pone la arena; la 3 la
// pinta el propio jefe). Ademas: su ficha de Desafios (Resources/Desafios, visible
// desde el principio, con el mismo cofre que la Cazadora), 2 logros (Normal y
// Dificil), su Config Nivel (sin musica: se pone a mano en el Inspector), la
// imagen del menu y la escena en Build Settings.
//
// No pisa lo editado: escena, ficha, logros y config solo se crean si no existen
// ("Rehacer escena" vuelve a copiar la escena del bosque).
public static class CrearDesafioBlackKnight
{
    public const string Escena = "Assets/Scenes/Bosque Black Knight.unity";
    private const string EscenaBosque = "Assets/Scenes/Bosque Cazadora.unity";
    public const string RutaFicha = "Assets/Resources/Desafios/Jefe_BlackKnight.asset";
    public const string Id = "black_knight";
    private const string FichaCazadora = "Assets/Resources/Desafios/Jefe_BlindHuntress.asset";
    private const string ConfigBosque = "Assets/Data/Niveles/Config Nivel Bosque.asset";
    public const string RutaConfig = "Assets/Data/Niveles/Config Nivel Black Knight.asset";
    public const string RutaImagen = "Assets/Sprites/Desafios/Jefe_BlackKnight.png";
    private const string Iconos = "Assets/SPRITES PARA NUEVOS NIVELES/ICONOS/Iconos_RPGPack/08-loot-treasure/";
    public const string LogroNormal = "prueba_caballero", LogroDificil = "cicatriz_caballero";

    [MenuItem("Warrior/Black Knight/Desafio: crear o actualizar", false, 49)]
    public static void Crear() => Hacer(false);

    [MenuItem("Warrior/Black Knight/Desafio: rehacer escena", false, 50)]
    public static void RehacerEscena() => Hacer(true);

    private static void Hacer(bool rehacer)
    {
        if (EditorSceneManager.GetActiveScene().isDirty && !Application.isBatchMode &&
            !EditorUtility.DisplayDialog("Desafio Black Knight", "La escena abierta tiene cambios sin guardar. Se cerrara sin guardarlos. ¿Seguir?", "Seguir", "Cancelar"))
            return;
        string abierta = EditorSceneManager.GetActiveScene().path;

        CrearBlackKnight.Crear();
        AjustesBlackKnight aj = AssetDatabase.LoadAssetAtPath<AjustesBlackKnight>(CrearBlackKnight.RutaFicha);
        ConfigNivel config = Config();
        Sprite imagen = Imagen();
        Ficha(imagen, aj);
        Logros();
        AssetDatabase.SaveAssets();

        EscenaDelCaballero(aj, config, rehacer);
        Build();
        AssetDatabase.SaveAssets();
        if (!string.IsNullOrEmpty(abierta) && File.Exists(abierta) && !Application.isBatchMode) EditorSceneManager.OpenScene(abierta);
        Debug.Log("[DesafioBlackKnight] Listo: Bosque Black Knight, ficha, 2 logros, config (sin musica) y Build Settings.");
    }

    // ------------------------------------------------------------------ Config e imagen

    // Copia de la del bosque, sin musica de jefe (la pone el usuario en el Inspector).
    private static ConfigNivel Config()
    {
        ConfigNivel c = AssetDatabase.LoadAssetAtPath<ConfigNivel>(RutaConfig);
        if (c != null) return c;
        AssetDatabase.CopyAsset(ConfigBosque, RutaConfig);
        c = AssetDatabase.LoadAssetAtPath<ConfigNivel>(RutaConfig);
        c.jefeFase1 = new ConfigNivel.MusicaFase();
        c.jefeFase2 = new ConfigNivel.MusicaFase();
        c.frasesJefe = new string[0];
        EditorUtility.SetDirty(c);
        return c;
    }

    // El primer cuadro de su postura quieta (celda 32 de la hoja limpia), recortado.
    private static Sprite Imagen()
    {
        if (!File.Exists(RutaImagen))
        {
            var hoja = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            hoja.LoadImage(File.ReadAllBytes(CrearBlackKnight.RutaHoja));
            int celda = 32, col = celda % CrearBlackKnight.Columnas, fila = celda / CrearBlackKnight.Columnas;
            int x0 = col * CrearBlackKnight.AnchoCelda + 48, y0 = hoja.height - (fila + 1) * CrearBlackKnight.AltoCelda;
            var salida = new Texture2D(44, 40, TextureFormat.RGBA32, false);
            salida.SetPixels(hoja.GetPixels(x0, y0, 44, 40));
            salida.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(RutaImagen));
            File.WriteAllBytes(RutaImagen, salida.EncodeToPNG());
            Object.DestroyImmediate(hoja);
            Object.DestroyImmediate(salida);
            AssetDatabase.ImportAsset(RutaImagen);
        }
        TextureImporter ti = AssetImporter.GetAtPath(RutaImagen) as TextureImporter;
        if (ti != null && (ti.textureType != TextureImporterType.Sprite || ti.filterMode != FilterMode.Point || ti.textureCompression != TextureImporterCompression.Uncompressed))
        {
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.alphaIsTransparency = true;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.mipmapEnabled = false;
            ti.filterMode = FilterMode.Point;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(RutaImagen);
    }

    // ------------------------------------------------------------------ Ficha de Desafios

    private static void Ficha(Sprite imagen, AjustesBlackKnight aj)
    {
        if (AssetDatabase.LoadAssetAtPath<FichaJefe>(RutaFicha) != null) return;
        FichaJefe cazadora = AssetDatabase.LoadAssetAtPath<FichaJefe>(FichaCazadora);
        FichaJefe f = ScriptableObject.CreateInstance<FichaJefe>();
        f.id = Id;
        f.nombre = "Black Knight";
        f.escena = Path.GetFileNameWithoutExtension(Escena);
        f.orden = 5;
        f.imagen = imagen;
        f.afinidades = new List<FichaJefe.Afinidad>
        {
            new FichaJefe.Afinidad { elemento = Elemento.Hielo, multiplicador = aj.multHielo, cuando = "Siempre" },
            new FichaJefe.Afinidad { elemento = Elemento.Sagrado, multiplicador = aj.multSagrado, cuando = "Siempre" },
            new FichaJefe.Afinidad { elemento = Elemento.Fuego, multiplicador = aj.multFuego, cuando = "Siempre" },
            new FichaJefe.Afinidad { elemento = Elemento.Oscuro, multiplicador = aj.multOscuridad, cuando = "Siempre" },
            new FichaJefe.Afinidad { elemento = Elemento.Sangrado, multiplicador = aj.multSangrado, cuando = "Siempre" },
        };
        f.notaAfinidades = "Su hoja arde: cada golpe que te alcanza deja quemadura. Si le pegas de frente con la guardia en alto, te para el golpe.";
        f.ataques = new List<FichaJefe.Ataque>
        {
            A("combo_ligero", "Combo ligero", 1, "Tres cortes seguidos: arriba, en arco y una estocada. A veces se arrepiente a mitad y rueda hacia atrás.",
              "Los tres se paran. Si los paras todos, se queda aturdido; si rueda a medias, no lo persigas a ciegas."),
            A("combo_corto", "Combo corto", 1, "Un arco y un tajo, rápidos y pegados.", "Dos parrys seguidos y queda abierto."),
            A("estocada", "Estocada", 1, "Desde lejos, cruza la distancia de una estocada baja.", "Llega más lejos de lo que parece: el parry vale más que retroceder."),
            A("combo_pesado", "Combo pesado", 1, "Tres golpes rojos: tajo, revés y un barrido a ras de suelo.", "Avisan despacio. No te adelantes: algunos se hacen esperar."),
            A("estocada_fuego", "Estocada de fuego", 1, "Carga la hoja y lanza una estocada larga envuelta en llamas.", "Larga pero lenta: un parry a tiempo o un paso fuera de su línea."),
            A("amago", "Amago", 1, "Enseña un golpe pesado y lo deja a medias para cortarte rápido.", "El rojo era mentira: espera al naranja que viene detrás."),
            A("rodada_espalda", "Rodada a la espalda", 1, "Rueda a través de ti y te corta por detrás.", "Date la vuelta en cuanto ruede: el tajo de la espalda se para igual."),
            A("salto", "Salto", 1, "Si te quedas quieto, salta y cae sobre ti con la espada.", "Se para mires hacia donde mires. Moverse también sirve."),
            A("guardia", "Guardia", 1, "Levanta la espada y espera tu golpe. Si llega de frente, lo para y responde con un estallido.",
              "No le pegues de frente con la espada en alto: espera a que la baje, o pásale por detrás."),
            A("desenvaine", "Desenvaine", 1, "Si bebes cerca de él, desenvaina y cruza la distancia de un tajo.", "Bebe lejos. Muy lejos."),
            A("provocar", "Provocación", 1, "Envaina y te mira. Si te acercas, desenvaina.", "Es una invitación. Acércate solo si vas a parar."),
            A("retroceder", "Paso atrás", 1, "Rueda lejos de ti cuando machacas el ataque.", "Cuanto más machacas, más te lee."),
            A("mixto", "Combo mixto", 2, "Ligeros y pesados mezclados en un mismo combo.", "Mira el color de cada aviso: el ritmo cambia a mitad."),
            A("ondas_fuego", "Ondas de fuego", 2, "Te empuja, se va al otro extremo y lanza ondas de fuego altas, una tras otra.",
              "No se paran ni se saltan: atraviésalas esquivando y, al llegar a él, un golpe las corta."),
            A("sello_amuleto", "Sello", 2, "Hace su pose y te sella un amuleto durante unos segundos: sigue puesto, pero no hace nada.",
              "No se evita. Lleva más de un amuleto para que no te deje sin nada."),
            A("erupciones", "Erupciones", 2, "Se aleja y del suelo salen columnas de fuego de todos los tamaños. Al acabar, salta sobre ti.",
              "Cada columna avisa con una grieta que brilla. Apártate o esquiva a través; al final, prepárate para su caída."),
            A("arte", "Arte del arma", 3, "Cinco golpes encadenados con fuego, el último en un gran arco.", "Los dos últimos son rojos y más lentos: no te adelantes."),
            A("sombras", "Sombras espectrales", 3, "Clava la espada en el suelo y salen sombras con su figura, de una en una, por los lados y desde arriba.",
              "Para o esquiva cada sombra. Mientras, él no se defiende: es tu momento para pegarle."),
        };
        f.fases = new List<FichaJefe.FaseInfo>
        {
            new FichaJefe.FaseInfo { numero = 1, nombre = "El duelo", descripcion = "Esgrima pura: combos, amagos y su propia guardia. Te estudia mientras pelea." },
            new FichaJefe.FaseInfo { numero = 2, nombre = "La hoja encendida", descripcion = "Su espada arde. Se aleja para lanzar ondas y erupciones, y sella tus amuletos." },
            new FichaJefe.FaseInfo { numero = 3, nombre = "Los que cayeron", descripcion = "Las sombras de sus duelos pasados pelean con él. Más rápido que nunca." },
        };
        f.fragmentosHistoria = new List<FichaJefe.Fragmento>
        {
            new FichaJefe.Fragmento { id = "h1", cuando = FichaJefe.Cuando.PrimerIntento, texto = "Nadie le ha visto la cara. Pelea como alguien que ya ha perdido una vez y no piensa repetirlo." },
            new FichaJefe.Fragmento { id = "h2", cuando = FichaJefe.Cuando.Fase2, texto = "La hoja no siempre ardió. Empezó a quemar el día que dejó de dudar." },
            new FichaJefe.Fragmento { id = "h3", cuando = FichaJefe.Cuando.Fase3, texto = "Las sombras tienen su figura porque son él: cada vez que cayó, se levantó otro." },
            new FichaJefe.Fragmento { id = "h4", cuando = FichaJefe.Cuando.PrimeraVictoria, texto = "Bajó la espada antes de caer. Un saludo, o una despedida." },
            new FichaJefe.Fragmento { id = "h5", cuando = FichaJefe.Cuando.VictoriaDificil, texto = "En el bosque queda una marca de ceniza con la forma de un hombre de rodillas." },
        };
        // El mismo cofre que el jefe secreto (The Blind Huntress).
        f.almasCofre = cazadora != null ? cazadora.almasCofre : 6500;
        f.objetosCofre = cazadora != null && cazadora.objetosCofre != null ? (Equipo.Objeto[])cazadora.objetosCofre.Clone()
            : new[] { Equipo.Objeto.PiedraForja, Equipo.Objeto.LagrimaCarmesi, Equipo.Objeto.LagrimaCeleste, Equipo.Objeto.FrascoMana, Equipo.Objeto.FrascoSangre };
        Directory.CreateDirectory(Path.GetDirectoryName(RutaFicha));
        AssetDatabase.CreateAsset(f, RutaFicha);
    }

    private static FichaJefe.Ataque A(string id, string nombre, int fase, string desc, string consejo) =>
        new FichaJefe.Ataque { id = id, nombre = nombre, fase = fase, descripcion = desc, consejo = consejo };

    // ------------------------------------------------------------------ Logros

    private static void Logros()
    {
        // Iconos del pack que aun no usa ningun logro.
        var usados = new HashSet<string>(AssetDatabase.FindAssets("t:FichaLogro", new[] { "Assets/Resources/Logros" })
            .Select(g => AssetDatabase.LoadAssetAtPath<FichaLogro>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(l => l != null && l.icono != null).Select(l => AssetDatabase.GetAssetPath(l.icono)));
        var libres = new Queue<Sprite>(Enumerable.Range(1, 60).Select(n => Iconos + $"icon_{n}.png")
            .Where(r => File.Exists(r) && !usados.Contains(r))
            .Select(r => AssetDatabase.LoadAllAssetsAtPath(r).OfType<Sprite>().FirstOrDefault()).Where(s => s != null));
        Logro("Desafios_6_prueba_caballero", LogroNormal, FichaLogro.Seccion.Desafios, 6, "El duelo terminado",
              "Completa el desafío de Black Knight.", FichaLogro.Condicion.CompletarDesafio, libres);
        Logro("Dificil_5_cicatriz_caballero", LogroDificil, FichaLogro.Seccion.Dificil, 5, "Cicatriz de ceniza",
              "Derrota a Black Knight en dificultad Difícil.", FichaLogro.Condicion.CompletarDesafioDificil, libres);
    }

    private static void Logro(string archivo, string id, FichaLogro.Seccion seccion, int orden, string nombre, string desc,
                              FichaLogro.Condicion cond, Queue<Sprite> iconos)
    {
        string ruta = $"Assets/Resources/Logros/{archivo}.asset";
        if (AssetDatabase.LoadAssetAtPath<FichaLogro>(ruta) != null) return;
        FichaLogro l = ScriptableObject.CreateInstance<FichaLogro>();
        l.id = id; l.seccion = seccion; l.orden = orden; l.nombre = nombre; l.descripcion = desc;
        l.condicion = cond; l.parametro = Id;
        l.icono = iconos.Count > 0 ? iconos.Dequeue() : null;
        AssetDatabase.CreateAsset(l, ruta);
    }

    // ------------------------------------------------------------------ Escena

    // Copia del bosque de la Cazadora con una arena normal y el Black Knight.
    private static void EscenaDelCaballero(AjustesBlackKnight aj, ConfigNivel config, bool rehacer)
    {
        bool nueva = rehacer || !File.Exists(Escena);
        if (nueva)
        {
            if (File.Exists(Escena)) AssetDatabase.DeleteAsset(Escena);
            if (!AssetDatabase.CopyAsset(EscenaBosque, Escena)) { Debug.LogError("[DesafioBlackKnight] No se pudo copiar " + EscenaBosque); return; }
        }
        Scene e = EditorSceneManager.OpenScene(Escena, OpenSceneMode.Single);
        ArenaJefe arena = Object.FindFirstObjectByType<ArenaJefe>(FindObjectsInactive.Include);
        if (arena == null)
        {
            ArenaCazadora vieja = Object.FindFirstObjectByType<ArenaCazadora>(FindObjectsInactive.Include);
            if (vieja == null) { Debug.LogError("[DesafioBlackKnight] El bosque no tiene la arena de la Cazadora"); return; }
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
            so.FindProperty("nombre").stringValue = aj.nombreMostrado;
            so.FindProperty("titulo").stringValue = "El duelista sin rostro";
            so.FindProperty("zona").rectValue = zona;
            so.FindProperty("suelo").floatValue = suelo;
            so.FindProperty("burlas").arraySize = 0;
            // Una barra por fase (tres): la arena hace la 2 y el jefe pinta la 3.
            so.FindProperty("marcaFase").floatValue = 0f;
            so.FindProperty("barraPorFase").boolValue = true;
            so.FindProperty("colorBarraFase2").colorValue = aj.colorBarraFase2;
            so.FindProperty("nombreFase2").stringValue = "";
            // Los textos de cada fase los pone el propio jefe (de su ficha).
            so.FindProperty("textoFase2").stringValue = "";
            so.FindProperty("luzFase2").colorValue = new Color(1f, 0.7f, 0.5f, 1f);
            so.FindProperty("particulasFase2A").colorValue = new Color(1f, 0.45f, 0.15f, 0.9f);
            so.FindProperty("particulasFase2B").colorValue = new Color(1f, 0.8f, 0.35f, 0.7f);
            so.FindProperty("particulasCaen").boolValue = false;
            so.FindProperty("bannerVictoria").stringValue = "CABALLERO CAÍDO";
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CrearBlackKnight.RutaPrefab);
        SerializedObject sa = new SerializedObject(arena);
        sa.FindProperty("prefabJefe").objectReferenceValue = prefab.GetComponent<JefeBase>();
        sa.FindProperty("prefabJefe2").objectReferenceValue = null;
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

    // La escena, despues de las de siempre.
    private static void Build()
    {
        var escenas = EditorBuildSettings.scenes.ToList();
        if (!escenas.Any(s => s.path == Escena)) escenas.Add(new EditorBuildSettingsScene(Escena, true));
        EditorBuildSettings.scenes = escenas.ToArray();
    }
}
