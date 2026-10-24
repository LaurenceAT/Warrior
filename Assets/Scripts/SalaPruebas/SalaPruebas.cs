#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// Sala de pruebas (SOLO Editor; la escena no esta en Build Settings y este codigo
// no existe en el juego compilado). Menu Warrior > Sala de pruebas.
//
// Una sala gris vacia para invocar cualquier jefe o enemigo del catalogo
// (Assets/Data/SalaPruebas) y probarlo con el menu F1. Los jefes salen con su
// arena de siempre (ArenaJefe o ArenaCazadora), montada por codigo con los limites
// de la sala: se comportan igual que en el juego.
//
// NADA SE GUARDA: antes de que se cargue la escena, todo lo que el juego escribe
// va a una carpeta temporal (Temp/SalaPruebas) y a claves "sala_" de PlayerPrefs
// (RegistroGuardado.CarpetaSala). Al salir del Play se borran y se recarga el
// codigo (Editor/SalaPruebas/CrearSalaPruebas.cs): estaticos y singletons vuelven
// a como estaban.
[DefaultExecutionOrder(-50)]
public class SalaPruebas : MonoBehaviour
{
    public const string Escena = "SalaPruebas";
    public const string Ruta = "Assets/Scenes/SalaPruebas.unity";
    public const string CarpetaDatos = "Assets/Data/SalaPruebas/";
    public const float Celda = 0.5f;

    public static SalaPruebas Actual { get; private set; }
    public static string CarpetaTemporal => Path.Combine(Directory.GetCurrentDirectory(), "Temp", "SalaPruebas");

    public CatalogoPruebas Catalogo { get; private set; }
    public AjustesSalaPruebas Ajustes { get; private set; }
    public TerrenosPruebas Terrenos { get; private set; }
    public TerrenoPruebas Terreno { get; private set; }

    // Lo invocado ahora.
    public CatalogoPruebas.Entrada Invocado { get; private set; }
    public JefeBase Jefe { get; private set; }
    public EnemyHealth Salud { get; private set; }
    public int FaseJefe { get; private set; }
    private GameObject arena;
    private readonly List<GameObject> enemigos = new List<GameObject>();
    private bool aterrizado;

    // Trucos.
    public bool Invencible { get; private set; }
    public bool VidaInfinita { get; set; }
    public bool EstaminaInfinita { get; set; }
    public bool VerCajas { get; private set; }
    public bool VerEstado { get; set; } = true;
    public bool VerCuenta { get; set; } = true;
    public bool Dificil { get; private set; }
    public bool EmpezarEnFase2 { get; set; }
    public string AtaqueBucle { get; private set; }
    private float ultimoVisto = -1f;

    // Camara lenta: solo cuando nadie mas toca el tiempo (golpes, pausa, ruedas).
    public float Velocidad { get; private set; } = 1f;
    private float escalaPuesta = -1f;

    private Light2D luz;
    private Color luzColor;
    private float luzIntensidad;
    private FichaJefe fichaDificil;
    private GUIStyle etiqueta;

    // ------------------------------------------------------------------ Antes de cargar la escena

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Proteger()
    {
        if (SceneManager.GetActiveScene().name != Escena) return;
        string c = CarpetaTemporal;
        if (Directory.Exists(c)) Directory.Delete(c, true);
        Directory.CreateDirectory(c);
        RegistroGuardado.CarpetaSala = c;
        BorrarClavesSala();
        Globales.Recargar();
        Desafio.FichaSala = null;
        AplicarEquipo(Cargar<EquipoPruebas>("Equipo Pruebas"));
    }

    // Las claves "sala_" de todo lo registrado (personaje y opciones).
    public static void BorrarClavesSala()
    {
        foreach (RegistroGuardado.Dato d in RegistroGuardado.Datos)
            foreach (RegistroGuardado.ClavePrefs p in d.prefs) PlayerPrefs.DeleteKey(RegistroGuardado.PrefijoSala + p.nombre);
        PlayerPrefs.Save();
    }

    public static T Cargar<T>(string nombre) where T : Object => AssetDatabase.LoadAssetAtPath<T>(CarpetaDatos + nombre + ".asset");

    // El personaje de la ficha (en memoria: sin partida, todo va a la sesion).
    private static void AplicarEquipo(EquipoPruebas e)
    {
        if (e == null) return;
        Progreso.Establecer(e.almas, e.Niveles, 0, Vector2.zero, "");
        for (int i = 0; i < e.nivelEspada; i++) Equipo.SubirDirecto(Equipo.Objeto.PiedraForja);
        for (int i = 0; i < e.nivelCuracion; i++) Equipo.SubirDirecto(Equipo.Objeto.LagrimaCarmesi);
        for (int i = 0; i < e.nivelMana; i++) Equipo.SubirDirecto(Equipo.Objeto.LagrimaCeleste);
        Equipo.Sumar(Equipo.Objeto.FrascoSangre, e.frascosSangreExtra);
        Equipo.Sumar(Equipo.Objeto.FrascoMana, e.frascosManaExtra);
    }

    // ------------------------------------------------------------------ Montaje

    private void Awake()
    {
        Actual = this;
        Catalogo = Cargar<CatalogoPruebas>("Catalogo Pruebas");
        Ajustes = Cargar<AjustesSalaPruebas>("Ajustes Sala Pruebas");
        Terrenos = Cargar<TerrenosPruebas>("Terrenos Pruebas");
        if (Ajustes == null) Ajustes = ScriptableObject.CreateInstance<AjustesSalaPruebas>();
    }

    private IEnumerator Start()
    {
        foreach (Light2D l in FindObjectsByType<Light2D>(FindObjectsSortMode.None))
            if (l.lightType == Light2D.LightType.Global) { luz = l; break; }
        if (luz != null) { luzColor = luz.color; luzIntensidad = luz.intensity; }

        Suelo();
        Terreno = gameObject.AddComponent<TerrenoPruebas>();
        gameObject.AddComponent<MenuSalaPruebas>();
        Cartel();

        // El player y sus frascos ya estan montados un fotograma despues.
        yield return null;
        ReservaPociones r = ReservaPociones.Get();
        if (r != null) r.Rellenar();
        EquipoPruebas e = Cargar<EquipoPruebas>("Equipo Pruebas");
        PlayerControler p = Player;
        if (e != null && e.imbuirAlEmpezar != Elemento.Ninguno && p != null)
        {
            ArmaImbuida arma = p.GetComponent<ArmaImbuida>();
            if (arma != null) arma.Activar(e.imbuirAlEmpezar);
        }
    }

    private void OnDestroy()
    {
        if (Actual == this) Actual = null;
        if (escalaPuesta > 0f && Mathf.Approximately(Time.timeScale, escalaPuesta)) Time.timeScale = 1f;
        Desafio.FichaSala = null;
        if (fichaDificil != null) Destroy(fichaDificil);
    }

    // Suelo, paredes y techo: casillas grises con la misma colision que el suelo
    // del juego (Tilemap + Composite en la capa Ground).
    private void Suelo()
    {
        Tilemap tm = CrearMapa(transform, "SueloSala", Ajustes.colorSuelo, out Tile t);
        float x0 = -Ajustes.ancho * 0.5f, x1 = Ajustes.ancho * 0.5f;
        Pintar(tm, t, Rect.MinMaxRect(x0 - 1f, -3f, x1 + 1f, 0f));
        Pintar(tm, t, Rect.MinMaxRect(x0 - 1f, 0f, x0, Ajustes.alto));
        Pintar(tm, t, Rect.MinMaxRect(x1, 0f, x1 + 1f, Ajustes.alto));
        Pintar(tm, t, Rect.MinMaxRect(x0 - 1f, Ajustes.alto, x1 + 1f, Ajustes.alto + 1f));
        ActualizarColision(tm);
    }

    public static Tilemap CrearMapa(Transform padre, string nombre, Color color, out Tile tile)
    {
        Grid grid = new GameObject(nombre, typeof(Grid)).GetComponent<Grid>();
        grid.transform.SetParent(padre, false);
        grid.cellSize = new Vector3(Celda, Celda, 0f);
        GameObject go = new GameObject("Casillas", typeof(Tilemap), typeof(TilemapRenderer));
        go.transform.SetParent(grid.transform, false);
        go.layer = LayerMask.NameToLayer("Ground");
        TilemapRenderer tr = go.GetComponent<TilemapRenderer>();
        tr.sortingLayerName = "Ground";
        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Static;
        TilemapCollider2D tc = go.AddComponent<TilemapCollider2D>();
        tc.compositeOperation = Collider2D.CompositeOperation.Merge;
        CompositeCollider2D cc = go.AddComponent<CompositeCollider2D>();
        cc.geometryType = CompositeCollider2D.GeometryType.Polygons;
        cc.generationType = CompositeCollider2D.GenerationType.Synchronous;

        Texture2D tex = new Texture2D(1, 1) { filterMode = FilterMode.Point };
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        tile = ScriptableObject.CreateInstance<Tile>();
        tile.sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f / Celda);
        tile.color = color;
        tile.colliderType = Tile.ColliderType.Grid;
        tile.flags = TileFlags.LockColor;
        return go.GetComponent<Tilemap>();
    }

    public static void Pintar(Tilemap tm, TileBase t, Rect r)
    {
        int x0 = Mathf.RoundToInt(r.xMin / Celda), x1 = Mathf.RoundToInt(r.xMax / Celda);
        int y0 = Mathf.RoundToInt(r.yMin / Celda), y1 = Mathf.RoundToInt(r.yMax / Celda);
        for (int x = x0; x < x1; x++)
            for (int y = y0; y < y1; y++) tm.SetTile(new Vector3Int(x, y, 0), t);
    }

    public static void ActualizarColision(Tilemap tm)
    {
        tm.GetComponent<TilemapCollider2D>().ProcessTilemapChanges();
        tm.GetComponent<CompositeCollider2D>().GenerateGeometry();
    }

    private void Cartel()
    {
        GameObject go = new GameObject("CartelSala");
        go.transform.SetParent(transform, false);
        Canvas c = go.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 40;
        UnityEngine.UI.CanvasScaler cs = go.AddComponent<UnityEngine.UI.CanvasScaler>();
        cs.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920, 1080);
        cs.matchWidthOrHeight = 0.5f;
        TextMeshProUGUI t = EstiloMenu.Texto("SALA DE PRUEBAS: no se guarda nada", go.transform, 20f, EstiloMenu.TextoApagado);
        RectTransform r = t.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 1f);
        r.pivot = new Vector2(0.5f, 1f);
        r.sizeDelta = new Vector2(600f, 30f);
        r.anchoredPosition = new Vector2(0f, -12f);
    }

    // ------------------------------------------------------------------ Invocar

    public PlayerControler Player => FindFirstObjectByType<PlayerControler>();
    public JefeCazadora Cazadora => Jefe as JefeCazadora;
    private Rect Zona => new Rect(-Ajustes.ancho * 0.5f + 0.5f, 0f, Ajustes.ancho - 1f, Ajustes.alto - 4f);

    public void Invocar(CatalogoPruebas.Entrada e)
    {
        Limpiar();
        if (e == null || e.prefab == null) return;
        Invocado = e;
        FaseJefe = 1;
        aterrizado = false;
        ultimoVisto = -1f;
        PlayerControler p = Player;
        switch (e.arena)
        {
            case CatalogoPruebas.TipoArena.Generica: ArenaGenerica(e, p); break;
            case CatalogoPruebas.TipoArena.Cazadora: ArenaDeCazadora(e, p); break;
            default:
                GameObject g = Instantiate(e.prefab, new Vector3(Ajustes.xAparicion, 1.5f, 0f), Quaternion.identity);
                enemigos.Add(g);
                Salud = g.GetComponent<EnemyHealth>();
                break;
        }
        if (Jefe != null)
        {
            Salud = Jefe.GetComponent<EnemyHealth>();
            Jefe.AlCambiarFase += () => FaseJefe++;
            Jefe.AlAterrizar += () => aterrizado = true;
            Jefe.PausaExtraPrueba = string.IsNullOrEmpty(AtaqueBucle) ? 0f : Ajustes.pausaBucle;
            if (EmpezarEnFase2) StartCoroutine(Fase2AlEmpezar());
        }
    }

    public void Reaparecer() => Invocar(Invocado);

    private void ArenaGenerica(CatalogoPruebas.Entrada e, PlayerControler p)
    {
        JefeBase prefab = e.prefab.GetComponent<JefeBase>();
        if (prefab == null) { Debug.LogWarning("[SalaPruebas] " + e.nombre + ": el prefab no tiene un JefeBase."); return; }
        // Al vencer a un jefe, el juego apunta "jefe_SalaPruebas" (en memoria: aqui
        // no hay partida) y su arena nacería ya vencida. En la sala se olvida.
        if (CampoBanderas != null && CampoBanderas.GetValue(null) is HashSet<string> banderas) banderas.Remove("jefe_" + Escena);
        ArenaJefe a = NuevaArena<ArenaJefe>();
        SerializedObject so = new SerializedObject(a);
        so.FindProperty("prefabJefe").objectReferenceValue = prefab;
        so.FindProperty("nombre").stringValue = e.nombre;
        so.FindProperty("titulo").stringValue = e.titulo ?? "";
        so.FindProperty("zona").rectValue = Zona;
        so.FindProperty("suelo").floatValue = 0f;
        so.FindProperty("marcaFase").floatValue = e.marcaFase;
        so.FindProperty("barraPorFase").boolValue = e.barraPorFase;
        so.FindProperty("colorBarraFase2").colorValue = e.colorBarraFase2;
        so.FindProperty("nombreFase2").stringValue = e.nombreFase2 ?? "";
        so.FindProperty("config").objectReferenceValue = Ajustes.musica ? e.config : null;
        so.FindProperty("musica").objectReferenceValue = null;
        so.FindProperty("musicaFase2").objectReferenceValue = null;
        so.FindProperty("luzFase2").colorValue = e.luzFase2;
        so.FindProperty("textoFase2").stringValue = e.textoFase2 ?? "";
        so.FindProperty("particulasFase2A").colorValue = e.particulasFase2A;
        so.FindProperty("particulasFase2B").colorValue = e.particulasFase2B;
        so.FindProperty("particulasCaen").boolValue = e.particulasCaen;
        so.FindProperty("bannerVictoria").stringValue = e.bannerVictoria ?? "";
        so.ApplyModifiedPropertiesWithoutUndo();
        arena.SetActive(true);
        Silenciar();
        a.EmpezarDesdeZona(p);
        Jefe = (JefeBase)typeof(ArenaJefe).GetField("jefe", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(a);
    }

    private static readonly FieldInfo CampoBanderas = typeof(Partida).GetField("banderasSesion", BindingFlags.NonPublic | BindingFlags.Static);

    private void ArenaDeCazadora(CatalogoPruebas.Entrada e, PlayerControler p)
    {
        JefeCazadora prefab = e.prefab.GetComponent<JefeCazadora>();
        if (prefab == null || e.ajustesCazadora == null) { Debug.LogWarning("[SalaPruebas] " + e.nombre + ": falta el prefab o la ficha de la Cazadora."); return; }
        // Sin el dialogo largo: cuenta como un reintento (solo en la carpeta temporal).
        if (Ajustes.saltarDialogoCazadora) Globales.PonerMarca("dialogo_cazadora", true);
        ArenaCazadora a = NuevaArena<ArenaCazadora>();
        SerializedObject so = new SerializedObject(a);
        so.FindProperty("prefabJefe").objectReferenceValue = prefab;
        so.FindProperty("ajustes").objectReferenceValue = e.ajustesCazadora;
        so.FindProperty("nombre").stringValue = e.nombre;
        so.FindProperty("titulo").stringValue = e.titulo ?? "";
        so.FindProperty("zona").rectValue = Zona;
        so.FindProperty("suelo").floatValue = 0f;
        so.FindProperty("xAparicion").floatValue = Ajustes.xAparicion;
        so.ApplyModifiedPropertiesWithoutUndo();
        arena.SetActive(true);
        // Su panel F10 sobra: sus botones estan en el menu F1.
        DepuracionCazadora dep = arena.GetComponent<DepuracionCazadora>();
        if (dep != null) Destroy(dep);
        Silenciar();
        a.EmpezarDesdeZona(p);
        Jefe = a.Jefe;
    }

    // La arena empieza apagada para ponerle los datos antes de su Awake. Su
    // disparador queda lejos: la pelea la empieza la sala.
    private T NuevaArena<T>() where T : Component
    {
        arena = new GameObject("ArenaPruebas");
        arena.SetActive(false);
        arena.transform.SetParent(transform, false);
        arena.layer = LayerMask.NameToLayer("Items");
        BoxCollider2D b = arena.AddComponent<BoxCollider2D>();
        b.isTrigger = true;
        b.size = new Vector2(0.1f, 0.1f);
        b.offset = new Vector2(0f, -500f);
        return arena.AddComponent<T>();
    }

    private void Silenciar()
    {
        if (Ajustes.musica || arena == null) return;
        foreach (AudioSource s in arena.GetComponents<AudioSource>()) s.mute = true;
    }

    public void Limpiar()
    {
        StopAllCoroutines();
        PeligrosJefe.LimpiarTodo();
        if (arena != null)
        {
            ArenaCazadora ac = arena.GetComponent<ArenaCazadora>();
            if (ac != null && ac.Oscuridad != null) Destroy(ac.Oscuridad.gameObject);
            Destroy(arena);
        }
        if (Jefe != null) Destroy(Jefe.gameObject);
        foreach (GameObject g in enemigos) if (g != null) Destroy(g);
        enemigos.Clear();
        foreach (BarraJefe b in FindObjectsByType<BarraJefe>(FindObjectsSortMode.None)) Destroy(b.gameObject);
        foreach (UICazadora u in FindObjectsByType<UICazadora>(FindObjectsSortMode.None)) Destroy(u.gameObject);
        GameObject particulas = GameObject.Find("ParticulasArena");
        if (particulas != null) Destroy(particulas);
        if (luz != null) { luz.color = luzColor; luz.intensity = luzIntensidad; }
        PlayerControler p = Player;
        if (p != null)
        {
            p.InvulnerableExterno = Invencible;
            if (GameManager.Instance != null) GameManager.Instance.SeguirConCamara(p.transform);
        }
        PlayerControler.TopeVentanaParry = -1f;
        Cinematica.Activa = false;
        arena = null;
        Jefe = null;
        Salud = null;
        Invocado = null;
    }

    // ------------------------------------------------------------------ Jefe

    public void Matar()
    {
        if (Salud != null && !Salud.Muerto) Salud.DanoEstado(Mathf.Max(1, Salud.CurrentHealth));
    }

    public void BajarVida()
    {
        if (Salud == null || Salud.Muerto) return;
        Salud.DanoEstado(Mathf.Max(1, Mathf.RoundToInt(Salud.MaxHealth * Ajustes.bajarVida)));
    }

    // Fase 2 (o la que se pida en la Cazadora, que tiene 3 barras).
    public void SaltarAFase(int fase)
    {
        if (Jefe == null || Salud == null || Salud.Muerto) return;
        JefeCazadora c = Cazadora;
        if (c != null) { c.SaltarAFase(fase - 1); return; }
        if (fase != 2 || FaseJefe >= 2) return;
        int objetivo = Mathf.FloorToInt(Salud.MaxHealth * Invocado.vidaFase2);
        if (objetivo <= 0) Salud.DanoEstado(Salud.CurrentHealth);
        else if (objetivo < Salud.CurrentHealth) Salud.DanoEstado(Salud.CurrentHealth - objetivo);
    }

    // Tras la entrada del jefe (aterriza, o la Cazadora empieza a pelear).
    private IEnumerator Fase2AlEmpezar()
    {
        float limite = Time.time + 15f;
        while (Jefe != null && Time.time < limite && !(aterrizado || (Cazadora != null && Cazadora.Activa))) yield return null;
        yield return new WaitForSeconds(0.3f);
        SaltarAFase(2);
    }

    public int FaseVisible => Cazadora != null ? Cazadora.Fase + 1 : FaseJefe;

    public void LanzarAtaque(string ataque)
    {
        if (Jefe == null || string.IsNullOrEmpty(ataque)) return;
        JefeCazadora c = Cazadora;
        if (c != null && !c.Activa) c.Activar();
        Jefe.ForzarAtaque(ataque);
    }

    // Bucle: el mismo ataque cada vez, con una pausa extra entre uno y otro.
    public void Bucle(string ataque)
    {
        AtaqueBucle = ataque;
        if (Jefe == null) return;
        Jefe.PausaExtraPrueba = string.IsNullOrEmpty(ataque) ? 0f : Ajustes.pausaBucle;
        if (string.IsNullOrEmpty(ataque)) { Jefe.ForzarAtaque(null); return; }
        ultimoVisto = Jefe.HoraUltimoAtaque;
        LanzarAtaque(ataque);
    }

    public void PonerDificil(bool dificil)
    {
        Dificil = dificil;
        if (!dificil) { Desafio.FichaSala = null; return; }
        FichaJefe f = Invocado != null ? Invocado.fichaDesafio : null;
        if (f == null)
        {
            if (fichaDificil == null) fichaDificil = ScriptableObject.CreateInstance<FichaJefe>();
            fichaDificil.multVida = Ajustes.multVida;
            fichaDificil.multDano = Ajustes.multDano;
            fichaDificil.multVelocidad = Ajustes.multVelocidad;
            f = fichaDificil;
        }
        Desafio.FichaSala = f;
    }

    // ------------------------------------------------------------------ Jugador

    public void PonerInvencible(bool si)
    {
        Invencible = si;
        PlayerControler p = Player;
        if (p != null) p.InvulnerableExterno = si;
    }

    public void AlCentro()
    {
        PlayerControler p = Player;
        if (p == null) return;
        Rigidbody2D rb = p.GetComponent<Rigidbody2D>();
        p.transform.position = new Vector3(Ajustes.xCentro, 1f, 0f);
        if (rb != null) { rb.position = p.transform.position; rb.linearVelocity = Vector2.zero; }
        p.CurarCompleto();
        PlayerStamina s = p.GetComponent<PlayerStamina>();
        if (s != null) s.Llenar();
    }

    public void PonerVerCajas(bool si)
    {
        VerCajas = si;
        DepuracionCazadora.VerCajas = si;
    }

    public void PonerVelocidad(float v)
    {
        // Si el tiempo de ahora es el que puso la sala, se cambia ya; si lo esta
        // usando otro sistema, se aplica cuando lo suelte (LateUpdate).
        bool nuestra = escalaPuesta > 0f && Mathf.Approximately(Time.timeScale, escalaPuesta);
        Velocidad = v;
        if (!nuestra) return;
        Time.timeScale = v;
        escalaPuesta = Mathf.Approximately(v, 1f) ? -1f : v;
    }

    // ------------------------------------------------------------------ Cada fotograma

    private void Update()
    {
        PlayerControler p = Player;
        if (p != null)
        {
            if (Invencible) p.InvulnerableExterno = true;
            if (VidaInfinita) p.CurarCompleto();
            if (EstaminaInfinita) { PlayerStamina s = p.GetComponent<PlayerStamina>(); if (s != null) s.Llenar(); }
        }

        // Bucle de ataque: al empezar uno, se pide el mismo para la siguiente vez.
        if (Jefe != null && !string.IsNullOrEmpty(AtaqueBucle) && Jefe.HoraUltimoAtaque > ultimoVisto)
        {
            ultimoVisto = Jefe.HoraUltimoAtaque;
            Jefe.ForzarAtaque(AtaqueBucle);
        }

        if (VerCajas) DibujarCajas();
    }

    private void LateUpdate()
    {
        if (Mathf.Approximately(Velocidad, 1f)) return;
        // Otro sistema esta usando el tiempo (golpe, pausa, rueda): no se toca.
        if (Mathf.Approximately(Time.timeScale, 1f) || (escalaPuesta > 0f && Mathf.Approximately(Time.timeScale, escalaPuesta)))
        {
            Time.timeScale = Velocidad;
            escalaPuesta = Velocidad;
        }
    }

    // Cuerpos del jefe y los enemigos y los peligros que deja el jefe (las cajas
    // de golpe de la Cazadora las pinta ella: DepuracionCazadora.VerCajas).
    private static readonly FieldInfo CampoPeligros = typeof(PeligrosJefe).GetField("vivos", BindingFlags.NonPublic | BindingFlags.Static);

    private void DibujarCajas()
    {
        if (Jefe != null) Cajas(Jefe.gameObject, new Color(1f, 0.25f, 0.25f));
        foreach (GameObject g in enemigos) if (g != null) Cajas(g, new Color(1f, 0.6f, 0.15f));
        if (CampoPeligros != null && CampoPeligros.GetValue(null) is List<GameObject> vivos)
            foreach (GameObject g in vivos) if (g != null) Cajas(g, Color.magenta);
    }

    private static void Cajas(GameObject g, Color c)
    {
        foreach (Collider2D col in g.GetComponentsInChildren<Collider2D>())
            if (col.enabled) CajaDebug.Mostrar(col.bounds.center, col.bounds.size, c, 0.03f);
    }

    // Estado y cuenta atras sobre el jefe o el enemigo.
    private void OnGUI()
    {
        if ((!VerEstado && !VerCuenta) || Salud == null || Camera.main == null) return;
        if (etiqueta == null) etiqueta = new GUIStyle(GUI.skin.label) { richText = true, fontSize = 14, alignment = TextAnchor.LowerCenter };
        Collider2D col = Salud.GetComponent<Collider2D>();
        Vector3 arriba = col != null ? new Vector3(col.bounds.center.x, col.bounds.max.y + 0.4f, 0f) : Salud.transform.position + Vector3.up * 3f;
        Vector3 s = Camera.main.WorldToScreenPoint(arriba);
        if (s.z < 0f) return;
        var lineas = new List<string>();
        if (Jefe != null)
        {
            if (VerEstado)
            {
                string ataque = string.IsNullOrEmpty(Jefe.UltimoAtaque) ? "-" : Jefe.UltimoAtaque;
                bool enPausa = Jefe.ProximoAtaque > Time.time;
                lineas.Add($"<b>Fase {FaseVisible}</b>  ·  {(enPausa ? "pausa" : "ataque: " + ataque)}  ·  vida {Salud.CurrentHealth}/{Salud.MaxHealth}");
            }
            if (VerCuenta)
                lineas.Add(Jefe.ProximoAtaque > Time.time ? $"próximo ataque en {Jefe.ProximoAtaque - Time.time:0.0} s"
                         : Jefe.HoraUltimoAtaque >= 0f ? $"atacando ({Time.time - Jefe.HoraUltimoAtaque:0.0} s)" : "entrando");
        }
        else if (VerEstado)
        {
            EnemigoBase e = Salud.GetComponent<EnemigoBase>();
            lineas.Add($"<b>{(e != null ? e.EstadoIA : "?")}</b>  ·  vida {Salud.CurrentHealth}/{Salud.MaxHealth}");
        }
        if (lineas.Count == 0) return;
        string texto = string.Join("\n", lineas);
        Rect r = new Rect(s.x - 220f, Screen.height - s.y - 60f, 440f, 56f);
        GUI.color = new Color(0f, 0f, 0f, 0.8f);
        GUI.Label(new Rect(r.x + 1f, r.y + 1f, r.width, r.height), Regex(texto), etiqueta);
        GUI.color = Color.white;
        GUI.Label(r, texto, etiqueta);
    }

    // La sombra del texto, sin las etiquetas de color.
    private static string Regex(string t) => System.Text.RegularExpressions.Regex.Replace(t, "<.*?>", "");
}
#endif
