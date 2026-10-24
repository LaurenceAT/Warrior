using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Sala de pruebas: menu Warrior > Sala de pruebas.
//   - Abrir y jugar: crea lo que falte (escena y fichas), abre la sala y le da a Play.
//   - Rehacer escena: vuelve a crear la escena (las fichas no se tocan).
//   - Rellenar catalogo: anade los jefes y enemigos que falten (no pisa lo editado).
// La escena se hace como la del bosque (Ronda10): copia de "Nivel Nieve" sin el
// nivel (mismo player, HUD, camara y GameManager). NO entra en Build Settings.
//
// Al salir del Play en la sala: se borran la carpeta temporal y las claves
// "sala_", y se recarga el codigo para que estaticos y singletons vuelvan a como
// estaban antes de entrar.
[InitializeOnLoad]
public static class CrearSalaPruebas
{
    private const string Plantilla = "Assets/Scenes/Nivel Nieve.unity";
    private const string Carpeta = SalaPruebas.CarpetaDatos;
    private static readonly Vector2 PosPlayer = new Vector2(-8f, 1f);

    static CrearSalaPruebas()
    {
        EditorApplication.playModeStateChanged += AlCambiarPlay;
    }

    // ------------------------------------------------------------------ Salir de la sala

    private static void AlCambiarPlay(PlayModeStateChange c)
    {
        if (c != PlayModeStateChange.EnteredEditMode || RegistroGuardado.CarpetaSala == null) return;
        Recoger();
        EditorUtility.RequestScriptReload();
    }

    // Deja todo como antes de entrar en la sala (tambien lo usa la prueba).
    public static void Recoger()
    {
        SalaPruebas.BorrarClavesSala();
        string c = SalaPruebas.CarpetaTemporal;
        if (Directory.Exists(c)) Directory.Delete(c, true);
        RegistroGuardado.CarpetaSala = null;
        Desafio.FichaSala = null;
        DepuracionCazadora.VerCajas = false;
        Cinematica.Activa = false;
        Globales.Recargar();
    }

    // ------------------------------------------------------------------ Menu

    [MenuItem("Warrior/Sala de pruebas/Abrir y jugar", false, 0)]
    public static void AbrirYJugar()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("[SalaPruebas] Sal del Play primero."); return; }
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Preparar();
        EditorSceneManager.OpenScene(SalaPruebas.Ruta, OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    [MenuItem("Warrior/Sala de pruebas/Rehacer escena", false, 1)]
    public static void RehacerEscena()
    {
        if (EditorApplication.isPlaying) return;
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Fichas();
        CrearEscena();
    }

    [MenuItem("Warrior/Sala de pruebas/Rellenar catálogo", false, 2)]
    public static void RellenarCatalogo()
    {
        Fichas();
        int n = Rellenar(Cargar<CatalogoPruebas>("Catalogo Pruebas"));
        Debug.Log($"[SalaPruebas] Catálogo: {n} entradas nuevas.");
    }

    // Lo que falte: fichas y escena.
    public static void Preparar()
    {
        Fichas();
        if (!File.Exists(SalaPruebas.Ruta)) CrearEscena();
        QuitarDeBuild();
    }

    // ------------------------------------------------------------------ Fichas (solo si no existen)

    private static T Cargar<T>(string nombre) where T : ScriptableObject => AssetDatabase.LoadAssetAtPath<T>(Carpeta + nombre + ".asset");

    private static T Ficha<T>(string nombre, System.Action<T> rellenar) where T : ScriptableObject
    {
        T f = Cargar<T>(nombre);
        if (f != null) return f;
        if (!AssetDatabase.IsValidFolder(Carpeta.TrimEnd('/'))) AssetDatabase.CreateFolder("Assets/Data", "SalaPruebas");
        f = ScriptableObject.CreateInstance<T>();
        rellenar?.Invoke(f);
        AssetDatabase.CreateAsset(f, Carpeta + nombre + ".asset");
        return f;
    }

    public static void Fichas()
    {
        Ficha<AjustesSalaPruebas>("Ajustes Sala Pruebas", null);
        Ficha<EquipoPruebas>("Equipo Pruebas", null);
        Ficha<TerrenosPruebas>("Terrenos Pruebas", t =>
        {
            t.formas.Add(Forma("Plataformas bajas", new TerrenosPruebas.Pieza(-8f, 2.5f, 5f, 0.5f), new TerrenosPruebas.Pieza(8f, 2.5f, 5f, 0.5f)));
            t.formas.Add(Forma("Plataformas altas", new TerrenosPruebas.Pieza(-5f, 5.5f, 4f, 0.5f), new TerrenosPruebas.Pieza(5f, 5.5f, 4f, 0.5f)));
            t.formas.Add(Forma("Pilar central", new TerrenosPruebas.Pieza(0f, 2f, 1.5f, 4f)));
            t.formas.Add(Forma("Escalones", new TerrenosPruebas.Pieza(-12f, 1.5f, 2.5f, 0.5f), new TerrenosPruebas.Pieza(-9f, 3f, 2.5f, 0.5f),
                               new TerrenosPruebas.Pieza(-6f, 4.5f, 2.5f, 0.5f)));
        });
        CatalogoPruebas cat = Ficha<CatalogoPruebas>("Catalogo Pruebas", null);
        if (cat.entradas.Count == 0) Rellenar(cat);
        AssetDatabase.SaveAssets();
    }

    private static TerrenosPruebas.Forma Forma(string nombre, params TerrenosPruebas.Pieza[] piezas)
        => new TerrenosPruebas.Forma { nombre = nombre, piezas = piezas.ToList() };

    // Los tres jefes (con los datos de su arena en su escena) y los enemigos de
    // Prefabs/Enemies. Solo anade lo que no este ya (por prefab).
    public static int Rellenar(CatalogoPruebas cat)
    {
        if (cat == null) return 0;
        int antes = cat.entradas.Count;
        bool Esta(GameObject p) => p != null && cat.entradas.Any(e => e != null && e.prefab == p);
        void Jefe(CatalogoPruebas.Entrada e) { if (e.prefab != null && !Esta(e.prefab)) cat.entradas.Add(e); }

        // Valores de ArenaJefe en "Nivel Nieve" y "Nivel Cueva".
        Jefe(new CatalogoPruebas.Entrada
        {
            nombre = "Sombra de los Humedales",
            prefab = Prefab("Assets/Prefabs/Enemies/Nieve/SombraHumedales.prefab"),
            arena = CatalogoPruebas.TipoArena.Generica,
            titulo = "La que duerme bajo el hielo",
            config = AssetDatabase.LoadAssetAtPath<ConfigNivel>("Assets/Data/Niveles/Config Nivel Nieve.asset"),
            marcaFase = 0f,
            barraPorFase = true,
            colorBarraFase2 = new Color(0.25f, 0.62f, 0.95f, 1f),
            nombreFase2 = "Sombra de los Humedales — Escarcha",
            luzFase2 = new Color(0.62f, 0.8f, 1f, 1f),
            textoFase2 = "La sombra se alza de nuevo, envuelta en escarcha...",
            particulasFase2A = new Color(1f, 1f, 1f, 0.9f),
            particulasFase2B = new Color(0.7f, 0.9f, 1f, 0.7f),
            particulasCaen = true,
            bannerVictoria = "SOMBRA DISIPADA",
            vidaFase2 = 0f,
            fichaDesafio = AssetDatabase.LoadAssetAtPath<FichaJefe>("Assets/Resources/Desafios/Jefe_ShadowedWetlands.asset"),
            asumeSueloPlano = true,
            avisoTerreno = "La Sombra supone suelo plano: sus lanzas, estacas y escarcha salen a ras del suelo y no alcanzan las plataformas.",
        });
        GameObject wraith = Prefab("Assets/Prefabs/Enemies/Cueva/CrimsonWraith.prefab");
        float umbral = 0.5f;
        JefeWraith jw = wraith != null ? wraith.GetComponent<JefeWraith>() : null;
        if (jw != null) umbral = new SerializedObject(jw).FindProperty("umbralFase2").floatValue;
        Jefe(new CatalogoPruebas.Entrada
        {
            nombre = "Crimson Wraith",
            prefab = wraith,
            arena = CatalogoPruebas.TipoArena.Generica,
            titulo = "El Espectro Carmesí",
            config = AssetDatabase.LoadAssetAtPath<ConfigNivel>("Assets/Data/Niveles/Config Nivel Cueva.asset"),
            marcaFase = 0.5f,
            barraPorFase = false,
            colorBarraFase2 = new Color(0.25f, 0.6f, 0.95f, 1f),
            luzFase2 = new Color(1f, 0.55f, 0.55f, 1f),
            textoFase2 = "El Espectro Carmesí revela su verdadera forma...",
            particulasFase2A = new Color(1f, 0.3f, 0.2f, 0.9f),
            particulasFase2B = new Color(1f, 0.7f, 0.3f, 0.7f),
            particulasCaen = false,
            bannerVictoria = "ENEMIGO CAÍDO",
            // Se transforma al bajar de su umbral (lo hace al acabar el ataque en curso).
            vidaFase2 = Mathf.Max(0.01f, umbral - 0.01f),
            fichaDesafio = AssetDatabase.LoadAssetAtPath<FichaJefe>("Assets/Resources/Desafios/Jefe_CrimsonWraith.asset"),
            asumeSueloPlano = true,
            avisoTerreno = "El Wraith supone suelo plano: sus pilares, raíces y ondas salen a ras del suelo y no alcanzan las plataformas.",
        });
        Jefe(new CatalogoPruebas.Entrada
        {
            nombre = "The Blind Huntress",
            prefab = Prefab("Assets/Prefabs/Enemies/Bosque/CazadoraCiega.prefab"),
            arena = CatalogoPruebas.TipoArena.Cazadora,
            titulo = "La Cazadora Ciega",
            ajustesCazadora = AssetDatabase.LoadAssetAtPath<AjustesCazadora>("Assets/Data/Jefes/Ajustes Cazadora.asset"),
            fichaDesafio = AssetDatabase.LoadAssetAtPath<FichaJefe>("Assets/Resources/Desafios/Jefe_BlindHuntress.asset"),
            asumeSueloPlano = true,
            avisoTerreno = "La Cazadora se mueve pegada al suelo de la arena: atraviesa las plataformas.",
        });

        // Enemigos normales (los que no son jefes).
        foreach (string carpeta in new[] { "Nieve", "Cueva", "Elites", "Variantes" })
        {
            string ruta = "Assets/Prefabs/Enemies/" + carpeta;
            if (!AssetDatabase.IsValidFolder(ruta)) continue;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { ruta }))
            {
                GameObject p = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (p == null || Esta(p) || p.GetComponent<EnemigoBase>() == null || p.GetComponent<JefeBase>() != null) continue;
                EnemyHealth h = p.GetComponent<EnemyHealth>();
                DefinicionEnemigo d = h != null ? h.Definicion : null;
                string nombre = d == null ? p.name : d.EsElite ? d.NombreElite : d.nombre;
                cat.entradas.Add(new CatalogoPruebas.Entrada { nombre = nombre, prefab = p, arena = CatalogoPruebas.TipoArena.Ninguna });
            }
        }
        EditorUtility.SetDirty(cat);
        AssetDatabase.SaveAssets();
        return cat.entradas.Count - antes;
    }

    private static GameObject Prefab(string ruta) => AssetDatabase.LoadAssetAtPath<GameObject>(ruta);

    // ------------------------------------------------------------------ Escena

    private static readonly string[] Quedan = { "Main Camera", "Global Light 2D", "---GameManager---", "Canva", "EventSystem", "CameraLimit", "RespawnPoint" };
    private static readonly string[] PrefabesQueQuedan = { "Assets/Prefabs/Player.prefab", "Assets/Prefabs/Camera/CinemachineCamera.prefab", "Assets/Prefabs/UI/HUD.prefab" };

    public static void CrearEscena()
    {
        AjustesSalaPruebas a = Cargar<AjustesSalaPruebas>("Ajustes Sala Pruebas");
        if (File.Exists(SalaPruebas.Ruta)) AssetDatabase.DeleteAsset(SalaPruebas.Ruta);
        if (!AssetDatabase.CopyAsset(Plantilla, SalaPruebas.Ruta)) { Debug.LogError("[SalaPruebas] No se pudo copiar " + Plantilla); return; }
        Scene escena = EditorSceneManager.OpenScene(SalaPruebas.Ruta, OpenSceneMode.Single);

        // Fuera todo lo del nivel: queda el player, la camara, el HUD y el GameManager.
        foreach (GameObject r in escena.GetRootGameObjects())
        {
            string origen = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(r);
            bool queda = Quedan.Contains(r.name) || PrefabesQueQuedan.Contains(origen);
            if (!queda) Object.DestroyImmediate(r);
        }
        foreach (GameObject r in escena.GetRootGameObjects())
        {
            if (r.name == "Main Camera")
                for (int i = r.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(r.transform.GetChild(i).gameObject);
            if (r.name == "RespawnPoint" || r.GetComponent<PlayerControler>() != null) r.transform.position = PosPlayer;
            if (r.name == "Global Light 2D")
            {
                Light2D l = r.GetComponent<Light2D>();
                if (l != null) { l.color = Color.white; l.intensity = 1f; }
            }
            if (r.name == "CameraLimit")
            {
                BoxCollider2D b = r.GetComponent<BoxCollider2D>();
                r.transform.position = Vector3.zero;
                if (b != null) { b.offset = new Vector2(0f, a.alto * 0.5f); b.size = new Vector2(a.ancho + 4f, a.alto + 8f); }
            }
        }
        Camera cam = Object.FindFirstObjectByType<Camera>();
        if (cam != null) cam.backgroundColor = new Color(0.12f, 0.12f, 0.14f, 1f);

        new GameObject("SalaPruebas").AddComponent<SalaPruebas>();
        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
        QuitarDeBuild();
        Debug.Log("[SalaPruebas] Escena creada: " + SalaPruebas.Ruta);
    }

    // La sala nunca entra en el juego compilado.
    private static void QuitarDeBuild()
    {
        EditorBuildSettingsScene[] escenas = EditorBuildSettings.scenes;
        if (escenas.Any(s => s.path == SalaPruebas.Ruta))
            EditorBuildSettings.scenes = escenas.Where(s => s.path != SalaPruebas.Ruta).ToArray();
    }
}
