using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

// Menu Warrior > Crear jefe nuevo: pide un nombre y crea un jefe basico, ya
// registrado en el catalogo de la sala de pruebas:
//   - Assets/Scripts/Jefe/Nuevos/Jefe<Nombre>.cs  (hereda de JefeBase)
//   - Assets/Data/Jefes/Ajustes <Nombre>.asset     (sus numeros: AjustesJefeBasico)
//   - Assets/Prefabs/Enemies/Nuevos/Jefe<Nombre>.prefab (un rectangulo de color)
//   - Su entrada en Assets/Data/SalaPruebas/Catalogo Pruebas (arena generica)
// Nunca sobrescribe nada y no lo conecta con niveles, Desafios, Bitacora,
// Bestiario ni logros. Unity tiene que compilar el script nuevo: el prefab, la
// ficha y la entrada del catalogo se crean solos justo despues.
public class CrearJefeNuevo : EditorWindow
{
    public const string CarpetaScripts = "Assets/Scripts/Jefe/Nuevos";
    public const string CarpetaPrefabs = "Assets/Prefabs/Enemies/Nuevos";
    public const string CarpetaAjustes = "Assets/Data/Jefes";
    private const string ClavePendiente = "Warrior.JefeNuevoPendiente";
    private const string Blanco = "Assets/Tiles/Nieve/Blanco.png";

    private string nombre = "";
    private string error;

    [MenuItem("Warrior/Crear jefe nuevo", false, 30)]
    private static void Abrir()
    {
        CrearJefeNuevo v = GetWindow<CrearJefeNuevo>(true, "Crear jefe nuevo");
        v.minSize = v.maxSize = new Vector2(420f, 150f);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Nombre del jefe (letras y números, sin espacios):");
        nombre = EditorGUILayout.TextField(nombre);
        error = Validar(nombre);
        if (!string.IsNullOrEmpty(nombre) && error != null) EditorGUILayout.HelpBox(error, MessageType.Warning);
        else if (!string.IsNullOrEmpty(nombre)) EditorGUILayout.HelpBox($"Se crearán Jefe{Limpio(nombre)}.cs, su prefab, su ficha \"Ajustes {Limpio(nombre)}\" y su entrada en la sala de pruebas.", MessageType.Info);
        using (new EditorGUI.DisabledScope(error != null || EditorApplication.isCompiling))
            if (GUILayout.Button("Crear")) { Crear(nombre); Close(); }
    }

    // ------------------------------------------------------------------ Validacion

    public static string Limpio(string n) => string.IsNullOrEmpty(n) ? "" : char.ToUpperInvariant(n.Trim()[0]) + n.Trim().Substring(1);

    // null = se puede crear; si no, el motivo.
    public static string Validar(string n)
    {
        n = Limpio(n);
        if (n == "") return "Escribe un nombre.";
        if (!Regex.IsMatch(n, "^[A-Za-z][A-Za-z0-9]*$")) return "Solo letras sin tilde y números, empezando por una letra.";
        string clase = "Jefe" + n;
        if (TypeCache.GetTypesDerivedFrom<MonoBehaviour>().Any(t => t.Name == clase) || TypeCache.GetTypesDerivedFrom<ScriptableObject>().Any(t => t.Name == clase))
            return $"Ya existe una clase {clase}.";
        foreach (string ruta in Rutas(n))
            if (File.Exists(ruta)) return "Ya existe " + ruta + ".";
        CatalogoPruebas cat = Catalogo;
        if (cat != null && cat.entradas.Any(e => e != null && e.nombre == n)) return $"Ya hay una entrada \"{n}\" en el catálogo.";
        if (SessionState.GetString(ClavePendiente, "") != "") return "Hay otro jefe a medio crear (espera a que Unity compile).";
        return null;
    }

    public static string[] Rutas(string n)
    {
        n = Limpio(n);
        return new[] { $"{CarpetaScripts}/Jefe{n}.cs", $"{CarpetaPrefabs}/Jefe{n}.prefab", $"{CarpetaAjustes}/Ajustes {n}.asset" };
    }

    private static CatalogoPruebas Catalogo => AssetDatabase.LoadAssetAtPath<CatalogoPruebas>(SalaPruebas.CarpetaDatos + "Catalogo Pruebas.asset");

    // ------------------------------------------------------------------ Paso 1: el script

    public static bool Crear(string n)
    {
        string motivo = Validar(n);
        if (motivo != null) { Debug.LogWarning("[JefeNuevo] " + motivo); return false; }
        n = Limpio(n);
        Directory.CreateDirectory(CarpetaScripts);
        File.WriteAllText(Rutas(n)[0], Plantilla.Replace("NOMBRE", n));
        SessionState.SetString(ClavePendiente, n);
        AssetDatabase.ImportAsset(Rutas(n)[0]);
        Debug.Log($"[JefeNuevo] Script creado: {Rutas(n)[0]}. Al compilar se crean el prefab, la ficha y su entrada en la sala.");
        return true;
    }

    // ------------------------------------------------------------------ Paso 2: tras compilar

    [DidReloadScripts]
    private static void TrasCompilar()
    {
        string n = SessionState.GetString(ClavePendiente, "");
        if (n == "") return;
        // Puede llegar antes de que la base de datos este lista: se reintenta.
        EditorApplication.delayCall += () => Terminar(n);
    }

    public static void Terminar(string n)
    {
        System.Type tipo = TypeCache.GetTypesDerivedFrom<JefeBase>().FirstOrDefault(t => t.Name == "Jefe" + n);
        if (tipo == null) { Debug.LogError($"[JefeNuevo] No se encuentra la clase Jefe{n} (¿error de compilación?)."); SessionState.EraseString(ClavePendiente); return; }
        SessionState.EraseString(ClavePendiente);
        string[] rutas = Rutas(n);
        if (File.Exists(rutas[1]) || File.Exists(rutas[2])) { Debug.LogWarning("[JefeNuevo] El prefab o la ficha ya existen: no se tocan."); return; }

        // Ficha de numeros.
        AjustesJefeBasico aj = ScriptableObject.CreateInstance<AjustesJefeBasico>();
        Random.State estado = Random.state;
        Random.InitState(n.GetHashCode());
        aj.color = Color.HSVToRGB(Random.value, 0.45f, 0.85f);
        Random.state = estado;
        Directory.CreateDirectory(CarpetaAjustes);
        AssetDatabase.CreateAsset(aj, rutas[2]);

        // Prefab: como los jefes de siempre (capa Enemies, etiqueta Enemy, cuerpo
        // con gravedad y rotacion fija, la imagen en el hijo "Visual").
        GameObject go = new GameObject("Jefe" + n);
        go.layer = LayerMask.NameToLayer("Enemies");
        go.tag = "Enemy";
        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 3f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        BoxCollider2D col = go.AddComponent<BoxCollider2D>();
        col.size = new Vector2(1.4f, 2.4f);
        col.offset = new Vector2(0f, 1.2f);
        EnemyHealth salud = go.AddComponent<EnemyHealth>();
        GameObject visual = new GameObject("Visual");
        visual.transform.SetParent(go.transform, false);
        visual.transform.localPosition = new Vector3(0f, 1.2f, 0f);
        visual.transform.localScale = new Vector3(1.4f, 2.4f, 1f);
        SpriteRenderer sr = visual.AddComponent<SpriteRenderer>();
        sr.sprite = AssetDatabase.LoadAllAssetsAtPath(Blanco).OfType<Sprite>().FirstOrDefault();
        sr.color = aj.color;
        sr.sortingLayerName = "Player";
        AnimadorHoja anim = visual.AddComponent<AnimadorHoja>();
        anim.destino = sr;
        Component jefe = go.AddComponent(tipo);

        SerializedObject sj = new SerializedObject(jefe);
        sj.FindProperty("ajustes").objectReferenceValue = aj;
        sj.FindProperty("visual").objectReferenceValue = visual.transform;
        sj.FindProperty("anim").objectReferenceValue = anim;
        sj.ApplyModifiedPropertiesWithoutUndo();
        SerializedObject sh = new SerializedObject(salud);
        sh.FindProperty("showHealthBar").boolValue = false;
        sh.FindProperty("maxHealth").intValue = aj.vidaFase1;
        sh.ApplyModifiedPropertiesWithoutUndo();

        Directory.CreateDirectory(CarpetaPrefabs);
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, rutas[1]);
        Object.DestroyImmediate(go);

        // Entrada en la sala de pruebas (solo ahi).
        CrearSalaPruebas.Fichas();
        CatalogoPruebas cat = Catalogo;
        cat.entradas.Add(new CatalogoPruebas.Entrada
        {
            nombre = n,
            prefab = prefab,
            arena = CatalogoPruebas.TipoArena.Generica,
            titulo = "Jefe de prueba",
            marcaFase = 0f,
            barraPorFase = true,
            nombreFase2 = n + " — Fase 2",
            textoFase2 = n + " se levanta de nuevo...",
            vidaFase2 = 0f,
        });
        EditorUtility.SetDirty(cat);
        AssetDatabase.SaveAssets();
        Debug.Log($"[JefeNuevo] Jefe{n} listo: prefab {rutas[1]}, ficha {rutas[2]} y entrada en la sala de pruebas.");
    }

    // Hay un jefe a medio crear (la prueba automatica espera a que acabe).
    public static bool Pendiente => SessionState.GetString(ClavePendiente, "") != "";

    // ------------------------------------------------------------------ Plantilla

    private const string Plantilla =
@"using System.Collections;
using UnityEngine;

// Jefe NOMBRE: jefe basico creado con Warrior > Crear jefe nuevo. Imagen temporal
// (un rectangulo de color), dos fases y un ataque de ejemplo (""embestida"").
// Sus numeros estan en su ficha: Assets/Data/Jefes/Ajustes NOMBRE.
// Solo esta en el catalogo de la sala de pruebas: no sale en ningun nivel,
// Desafio, Bitacora, Bestiario ni logro.
//
// Maquina de estados de los enemigos (CambiarEstado):
//   quieto (Alerta) -> aviso (Anticipacion) -> ataque (Ataque) -> recuperacion (Recuperacion)
public class JefeNOMBRE : JefeBase, IModificadorDano
{
    [SerializeField] private AjustesJefeBasico ajustes;

    private Rect arena;
    private float suelo;
    private bool fase2, transformando, entrado;
    private string ataqueForzado;
    private SpriteRenderer sr;

    private float Velocidad => fase2 ? ajustes.multVelocidadFase2 : 1f;
    private Color ColorFase => fase2 ? ajustes.colorFase2 : ajustes.color;

    // Sala de pruebas: sus ataques y el ataque forzado (el siguiente que haga).
    public override string[] AtaquesPrueba => new[] { ""embestida"" };
    public override void ForzarAtaque(string ataque) => ataqueForzado = ataque;

    public override void Configurar(Rect zona, float alturaSuelo)
    {
        arena = zona;
        suelo = alturaSuelo;
    }

    protected override void Awake()
    {
        base.Awake();
        radioDeteccion = radioOlvido = 60f;
        armadura = true;
        salud.Inamovible = true;
        sr = visual != null ? visual.GetComponent<SpriteRenderer>() : null;
        // Fase 1: al vaciar la barra no muere, pasa a la fase 2.
        salud.PuedeMorir = () => fase2;
        salud.AlAgotarse += () => { if (!transformando) { StopAllCoroutines(); StartCoroutine(PasarAFase2()); } };
    }

    protected override void Start()
    {
        salud.Revivir(VidaDesafio(ajustes.vidaFase1));
        if (sr != null) sr.color = ColorFase;
        base.Start();
    }

    // Los golpes no entran mientras cambia de fase.
    public int Modificar(int dano, TipoArma arma) => transformando ? 0 : dano;

    // ------------------------------------------------------------------ Cerebro

    protected override IEnumerator Cerebro()
    {
        if (!entrado)
        {
            // Entrada: cae en la arena; al tocar el suelo sale su nombre y su barra.
            CambiarEstado(EstadoEnemigo.Alerta);
            while (!EnSuelo) yield return null;
            entrado = true;
            AvisarAterrizaje();
        }
        while (true)
        {
            yield return Quieto(Random.Range(ajustes.pausa.x, ajustes.pausa.y));
            string ataque = Elegir();
            AnunciarAtaque(ataque);
            yield return Ejecutar(ataque);
        }
    }

    private string Elegir()
    {
        if (!string.IsNullOrEmpty(ataqueForzado)) { string f = ataqueForzado; ataqueForzado = null; return f; }
        return ""embestida"";
    }

    private IEnumerator Ejecutar(string ataque)
    {
        switch (ataque)
        {
            case ""embestida"": return Embestida();
            default: return Quieto(0.3f);
        }
    }

    // Quieto: te mira y espera (mas corto en Dificil).
    private IEnumerator Quieto(float segundos)
    {
        CambiarEstado(EstadoEnemigo.Alerta);
        segundos = PausaPrueba(PausaDesafio(segundos));
        for (float t = 0f; t < segundos; t += Time.deltaTime)
        {
            BuscarPlayer();
            MirarAlPlayer();
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            yield return null;
        }
    }

    // Ataque de ejemplo: se tine de rojo (aviso), embiste hacia ti y se queda quieto.
    private IEnumerator Embestida()
    {
        BuscarPlayer();
        MirarYa();
        CambiarEstado(EstadoEnemigo.Anticipacion);
        float aviso = ajustes.aviso / Velocidad;
        for (float t = 0f; t < aviso; t += Time.deltaTime)
        {
            if (sr != null) sr.color = Color.Lerp(ColorFase, ajustes.colorAviso, Mathf.PingPong(t * 8f, 1f));
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            yield return null;
        }
        if (sr != null) sr.color = ColorFase;

        CambiarEstado(EstadoEnemigo.Ataque);
        bool golpeado = false;
        for (float t = 0f; t < ajustes.duracionAtaque; t += Time.deltaTime)
        {
            float x = transform.position.x;
            bool dentro = mirada > 0 ? x < arena.xMax - 1f : x > arena.xMin + 1f;
            rb.linearVelocity = new Vector2(dentro ? mirada * ajustes.velocidadAtaque * Velocidad : 0f, rb.linearVelocity.y);
            if (!golpeado)
            {
                Vector2 centro = (Vector2)transform.position + new Vector2(mirada * ajustes.cajaGolpe.x * 0.5f, ajustes.cajaGolpe.y * 0.5f);
                if (PeligrosJefe.GolpearCaja(centro, ajustes.cajaGolpe, ajustes.dano, this, out _, PlayerControler.TipoDano.Fisico).HasValue) golpeado = true;
                if (DepuracionCazadora.VerCajas) CajaDebug.Mostrar(centro, ajustes.cajaGolpe, Color.red, 0.03f);
            }
            yield return null;
        }

        CambiarEstado(EstadoEnemigo.Recuperacion);
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        float rec = ajustes.recuperacion / Velocidad;
        for (float t = 0f; t < rec; t += Time.deltaTime) yield return null;
    }

    // ------------------------------------------------------------------ Fase 2

    private IEnumerator PasarAFase2()
    {
        transformando = true;
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        CambiarEstado(EstadoEnemigo.Recuperacion);
        for (float t = 0f; t < ajustes.transicion; t += Time.deltaTime)
        {
            if (sr != null) sr.color = Color.Lerp(ajustes.color, ajustes.colorFase2, t / Mathf.Max(0.01f, ajustes.transicion));
            yield return null;
        }
        fase2 = true;
        if (sr != null) sr.color = ColorFase;
        salud.Revivir(VidaDesafio(ajustes.vidaFase2));
        AvisarCambioFase();
        transformando = false;
        Pensar();
    }

    protected override void AlMorir()
    {
        base.AlMorir();
        PeligrosJefe.LimpiarTodo();
        AvisarDerrota();
    }
}
";
}
