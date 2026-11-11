using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Prueba del Black Knight en la sala de pruebas (batchmode: -executeMethod PruebaBlackKnight.Todo).
//   1. Sin jugar: huellas del progreso real y de los originales, Warrior > Black
//      Knight > Crear o actualizar, hoja limpia (sin marcos), clips del prefab,
//      ficha (cuadros de cada golpe dentro de su clip, golpes de cada ataque,
//      ninguno mata con la vida llena) y que no esta fuera de la sala.
//   2. En la sala (en silencio, jugador automatico): entra y ataca solo; cada
//      ataque con parry a todo (el combo sigue; aturdido si los paras todos y no
//      lo corta), sin bloquear (tope del combo), avisos minimos y un solo golpe
//      activo; su guardia para tu golpe y te aturde; te castiga el frasco; te lee
//      si machacas; 3 barras (fase 2 y 3, invulnerable al cambiar); ondas de fuego
//      (te empuja, se va al otro extremo, no se paran, se atraviesan esquivando y
//      se cortan pegandole); sombras espectrales (de lado y desde arriba, se
//      paran, y el jefe recibe dano con la espada clavada); muerte, reapariciones
//      y dos duelos libres (lineas "[BKNIGHT] DUELO").
//   3. Al salir: el progreso real y los originales son identicos.
// Escribe "[BKNIGHT] OK ..." / "[BKNIGHT] FALLO ..." y el total.
[InitializeOnLoad]
public static class PruebaBlackKnight
{
    private const string Pre = "PruebaBlackKnight.";

    private static string Fase { get => SessionState.GetString(Pre + "Fase", ""); set => SessionState.SetString(Pre + "Fase", value); }

    static PruebaBlackKnight()
    {
        EditorApplication.playModeStateChanged += e =>
        {
            if (Fase != "jugar") return;
            if (e == PlayModeStateChange.EnteredPlayMode) new GameObject("PruebaBlackKnight").AddComponent<Conductor>();
            else if (e == PlayModeStateChange.EnteredEditMode)
            {
                Fase = "salida";
                SessionState.SetFloat(Pre + "Desde", (float)EditorApplication.timeSinceStartup);
                EditorApplication.update += Continuar;
            }
        };
        if (Fase == "salida") EditorApplication.update += Continuar;
    }

    private static void Ok(string s) { SessionState.SetInt(Pre + "Ok", SessionState.GetInt(Pre + "Ok", 0) + 1); Debug.Log("[BKNIGHT] OK " + s); }
    private static void Fallo(string s) { SessionState.SetInt(Pre + "Fallos", SessionState.GetInt(Pre + "Fallos", 0) + 1); Debug.LogWarning("[BKNIGHT] FALLO " + s); }
    private static void Info(string s) => Debug.Log("[BKNIGHT] INFO " + s);
    private static void Comprobar(bool c, string s) { if (c) Ok(s); else Fallo(s); }

    // ------------------------------------------------------------------ Sin jugar

    public static void Todo()
    {
        SessionState.SetInt(Pre + "Ok", 0);
        SessionState.SetInt(Pre + "Fallos", 0);
        SessionState.SetString(Pre + "Antes", Instantanea());
        SessionState.SetString(Pre + "Originales", HuellaOriginales());

        CrearBlackKnight.Crear();
        Comprobar(CrearBlackKnight.Comprobar(out string informe), "sprites: " + informe);
        ComprobarHoja();
        ComprobarFicha();
        ComprobarAislado();

        Fase = "jugar";
        EditorSceneManager.OpenScene(SalaPruebas.Ruta, OpenSceneMode.Single);
        ArranquePruebas.Proteger();
        EditorApplication.EnterPlaymode();
    }

    private static AjustesBlackKnight Ficha => AssetDatabase.LoadAssetAtPath<AjustesBlackKnight>(CrearBlackKnight.RutaFicha);

    // Sin marcos de color y con los pies de la quieta en la misma linea; el prefab con todos sus clips.
    private static void ComprobarHoja()
    {
        var tex = new Texture2D(2, 2);
        tex.LoadImage(File.ReadAllBytes(CrearBlackKnight.RutaHoja));
        int marcos = 0;
        for (int c = 0; c < CrearBlackKnight.Columnas; c++)
            for (int f = 0; f < CrearBlackKnight.Filas; f++)
                if (tex.GetPixel(c * CrearBlackKnight.AnchoCelda, f * CrearBlackKnight.AltoCelda + 30).a > 0.01f) marcos++;
        Comprobar(marcos == 0, $"la copia no tiene los marcos de color del original ({marcos} celdas con marco)");
        // Pies de los 6 cuadros de la quieta: la fila con dibujo mas baja (y de Unity) es la 1.
        var pies = new List<int>();
        for (int k = 32; k < 38; k++)
        {
            int col = k % CrearBlackKnight.Columnas, fila = k / CrearBlackKnight.Columnas;
            int y0 = tex.height - (fila + 1) * CrearBlackKnight.AltoCelda, abajo = -1;
            for (int y = 0; y < CrearBlackKnight.AltoCelda && abajo < 0; y++)
                for (int x = 0; x < CrearBlackKnight.AnchoCelda; x++)
                    if (tex.GetPixel(col * CrearBlackKnight.AnchoCelda + x, y0 + y).a > 0.05f) { abajo = y; break; }
            pies.Add(abajo);
        }
        Object.DestroyImmediate(tex);
        Comprobar(pies.All(p => p == 1), "pies de la quieta en la fila 1 desde abajo (el anclaje): " + string.Join("/", pies));

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CrearBlackKnight.RutaPrefab);
        AnimadorHoja an = prefab != null ? prefab.GetComponentInChildren<AnimadorHoja>() : null;
        Comprobar(an != null && CrearBlackKnight.Tabla.All(t => an.clips.Any(k => k.nombre == t.nombre && k.hoja != null && k.cantidad == t.cuadros)),
                  $"el prefab tiene sus {CrearBlackKnight.Tabla.Length} clips con sus cuadros");
        Comprobar(an != null && an.clips.All(k => Mathf.Approximately(k.pixelesPorUnidad, 28f) && k.pivote == CrearBlackKnight.Pivote),
                  "todos los clips a 28 px por unidad (como el jugador) y con el mismo anclaje");
    }

    // Cada golpe dentro de su clip; cada ataque con golpes que existen; ninguno mata con la vida llena.
    private static void ComprobarFicha()
    {
        AjustesBlackKnight aj = Ficha;
        Comprobar(aj != null, "ficha " + CrearBlackKnight.RutaFicha);
        if (aj == null) return;
        int vidaMax = AjustesProgreso.Get().vidaBase;
        float multDificil = Mathf.Max(1f, SalaPruebas.Cargar<AjustesSalaPruebas>("Ajustes Sala Pruebas").multDano);
        var cuadros = CrearBlackKnight.Tabla.ToDictionary(t => t.nombre, t => t.cuadros);
        var mal = new List<string>();
        foreach (AjustesBlackKnight.Golpe g in aj.golpes)
        {
            if (!cuadros.TryGetValue(g.clip, out int n)) { mal.Add(g.nombre + ": clip " + g.clip); continue; }
            if (!(g.cuadroAviso <= g.golpeDesde && g.golpeDesde <= g.golpeHasta && g.golpeHasta < n && g.cortarEn < n)) mal.Add($"{g.nombre}: cuadros");
            if (Mathf.RoundToInt(g.dano * multDificil) >= vidaMax) mal.Add($"{g.nombre}: mata ({g.dano})");
            if (!g.amago && (g.cajaTamano.x <= 0f || g.cajaTamano.y <= 0f)) mal.Add($"{g.nombre}: caja");
        }
        Comprobar(mal.Count == 0, $"{aj.golpes.Count} golpes con sus cuadros dentro del clip, caja propia y sin matar con la vida llena ({vidaMax})"
                                  + (mal.Count > 0 ? ": " + string.Join(", ", mal) : ""));
        var faltan = aj.ataques.SelectMany(a => a.golpes.Where(n => aj.Buscar(n) == null).Select(n => a.id + "/" + n)).ToList();
        Comprobar(faltan.Count == 0, $"{aj.ataques.Count} ataques, todos con golpes que existen" + (faltan.Count > 0 ? ": " + string.Join(", ", faltan) : ""));
        Comprobar(aj.ataques.Count(a => a.faseMinima == 1) >= 5 && aj.ataques.Any(a => a.faseMinima == 2) && aj.ataques.Any(a => a.faseMinima == 3),
                  "ataques de fase 1, 2 y 3");
        Comprobar(aj.Afinidad(Elemento.Fuego) < 1f && aj.Afinidad(Elemento.Oscuro) < 1f && aj.Afinidad(Elemento.Sangrado) < 1f
                  && aj.Afinidad(Elemento.Hielo) > 1f && aj.Afinidad(Elemento.Sagrado) > 1f && Mathf.Approximately(aj.Afinidad(Elemento.Natura), 1f),
                  $"resiste fuego/oscuridad/sangrado ({aj.multFuego}/{aj.multOscuridad}/{aj.multSangrado}), debil a hielo/luz ({aj.multHielo}/{aj.multSagrado}), natura neutral");
        Comprobar(aj.vidaFase1 >= 780 && aj.vidaFase2 >= 840 && aj.vidaFase3 >= 900, $"vida +50 %: {aj.vidaFase1}/{aj.vidaFase2}/{aj.vidaFase3}");
        Comprobar(aj.quemaduraPorGolpe > 0f, $"cada golpe suyo acumula quemadura ({aj.quemaduraPorGolpe} de 100)");
        Comprobar(aj.intervaloOndas.x >= 0.85f, $"entre onda y onda da tiempo a recuperar la esquiva ({aj.intervaloOndas.x:0.00} s >= 0,85 s)");
        Comprobar(aj.cajaOnda.y >= 4f, $"la onda es mas alta que un salto ({aj.cajaOnda.y} u): solo se pasa esquivando");
    }

    // En la sala y en su desafio (autorizado): de las escenas de la build, solo en
    // la suya ("Bosque Black Knight"); nada en Resources ni en el Bestiario.
    private static void ComprobarAislado()
    {
        string guid = AssetDatabase.AssetPathToGUID(CrearBlackKnight.RutaPrefab);
        bool enResources = AssetDatabase.FindAssets("", new[] { "Assets/Resources" })
            .Select(AssetDatabase.GUIDToAssetPath).Where(p => p.EndsWith(".asset") || p.EndsWith(".prefab"))
            .Any(p => File.ReadAllText(p).Contains(guid));
        Comprobar(!enResources, "nada de Resources apunta al prefab del Black Knight");
        var escenas = EditorBuildSettings.scenes.Where(s => File.Exists(s.path) && File.ReadAllText(s.path).Contains(guid)).Select(s => s.path).ToList();
        Comprobar(escenas.Count == 1 && escenas[0] == CrearDesafioBlackKnight.Escena,
                  "de las escenas de la build, solo esta en la de su desafio (" + string.Join(", ", escenas) + ")");
        Comprobar(FichaBestiario.Get().entradas.All(e => e.enemigo == null || e.enemigo.name != "JefeBlackKnight"), "no esta en el Bestiario");
    }

    // ------------------------------------------------------------------ Al salir

    private static void Continuar()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Pre + "Desde", 0f) < 3.0) return;
        EditorApplication.update -= Continuar;
        Comprobar(Instantanea() == SessionState.GetString(Pre + "Antes", ""), "el progreso real es identico (partidas, globales.json y PlayerPrefs)");
        Comprobar(HuellaOriginales() == SessionState.GetString(Pre + "Originales", ""), "los originales y sus .meta no cambiaron");
        int ok = SessionState.GetInt(Pre + "Ok", 0), fallos = SessionState.GetInt(Pre + "Fallos", 0);
        Debug.Log($"[BKNIGHT] RESULTADO: {ok} OK, {fallos} FALLOS");
        Fase = "";
        if (Application.isBatchMode) EditorApplication.Exit(fallos > 0 ? 1 : 0);
    }

    private static string HuellaOriginales()
    {
        var sb = new StringBuilder();
        using (SHA1 sha = SHA1.Create())
            foreach (string f in Directory.GetFiles(CrearBlackKnight.CarpetaOriginal, "*", SearchOption.AllDirectories).OrderBy(x => x))
                sb.AppendLine(f.Replace('\\', '/') + " " + BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(f))));
        return sb.ToString();
    }

    private static string Instantanea()
    {
        var sb = new StringBuilder();
        using (SHA1 sha = SHA1.Create())
            foreach (bool perfil in new[] { false, true })
            {
                string c = RegistroGuardado.CarpetaDe(perfil);
                if (!Directory.Exists(c)) continue;
                foreach (string f in Directory.GetFiles(c).OrderBy(x => x))
                    sb.AppendLine(Path.GetFileName(f) + " " + BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(f))));
            }
        foreach (RegistroGuardado.Dato d in RegistroGuardado.Datos)
            foreach (RegistroGuardado.ClavePrefs p in d.prefs)
                foreach (string k in new[] { p.nombre, RegistroGuardado.PrefijoPruebas + p.nombre })
                {
                    if (!PlayerPrefs.HasKey(k)) continue;
                    string v = p.tipo == RegistroGuardado.Tipo.Int ? PlayerPrefs.GetInt(k).ToString()
                             : p.tipo == RegistroGuardado.Tipo.Float ? PlayerPrefs.GetFloat(k).ToString("R") : PlayerPrefs.GetString(k);
                    sb.AppendLine(k + "=" + v);
                }
        return sb.ToString();
    }

    // ------------------------------------------------------------------ En la sala

    private class Conductor : MonoBehaviour
    {
        private const BindingFlags Priv = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;
        private readonly List<string> errores = new List<string>();
        private SalaPruebas sala;

        // Jugador automatico: 0 = nada, 1 = parry a todo (mirando al que pega),
        // 3 = sin bloquear, 6 = esquiva las ondas (hacia el jefe).
        private int bot;
        private int maxActivos, avisosCortos;
        private string alActivar = "";
        private readonly List<string> avisosMal = new List<string>();

        private void Awake()
        {
            Application.logMessageReceived += Registrar;
            // En silencio: la prueba no necesita oir nada (no cambia tus opciones de volumen).
            AudioListener.volume = 0f;
        }
        private void OnDestroy() => Application.logMessageReceived -= Registrar;

        private void Registrar(string c, string st, LogType t)
        {
            if ((t == LogType.Error || t == LogType.Exception) && !c.StartsWith("[BKNIGHT]")) errores.Add(c);
            if (t == LogType.Warning && (c.StartsWith("[BlackKnight]") || c.Contains("JefeBlackKnight") || c.Contains("SombraBK"))) errores.Add(c);
        }

        private void Update()
        {
            AudioListener.volume = 0f;
            JefeBlackKnight j = sala != null ? J : null;
            if (j == null) return;
            maxActivos = Mathf.Max(maxActivos, j.GolpesActivos);
            PlayerControler p = P;
            if (p == null || bot == 0) return;
            PlayerStamina st = p.GetComponent<PlayerStamina>();
            if (st != null) st.Recuperar(100f);
            if (bot == 3) { SetCampo(p, "isBlocking", false); return; }
            if (bot == 6)
            {
                SetCampo(p, "isBlocking", false);
                Mirar(p, j.transform.position.x);
                float px = p.transform.position.x;
                bool cerca = Object.FindObjectsByType<OndaFuegoBK>(FindObjectsSortMode.None)
                    .Any(o => !o.Tocada && !o.Esquivada && Mathf.Abs(o.transform.position.x - px) < 1.3f);
                if (cerca && !p.Esquivando) { GatherInput gi = p.GetComponent<GatherInput>(); if (gi != null) gi.IsDodging = true; }
                return;
            }
            // Parry: mira a la sombra que ataca (si la hay) o al jefe.
            SombraBK s = Object.FindFirstObjectByType<SombraBK>();
            Mirar(p, s != null ? s.transform.position.x : j.transform.position.x);
            SetCampo(p, "isBlocking", true);
            SetCampo(p, "tBloqueo", 0f);
            SetCampo(p, "ventanaParryActual", Campo(p, "parryWindow"));
        }

        private static void Mirar(PlayerControler p, float x)
        {
            if (Mathf.Abs(x - p.transform.position.x) > 0.05f) Llamar(p, "SetFacing", x > p.transform.position.x ? 1 : -1);
        }

        private static FieldInfo BuscarCampo(Type t, string campo)
        {
            for (; t != null; t = t.BaseType) { FieldInfo f = t.GetField(campo, Priv); if (f != null) return f; }
            return null;
        }
        private static object Campo(object o, string campo) => BuscarCampo(o.GetType(), campo)?.GetValue(o);
        private static void SetCampo(object o, string campo, object v) { if (o != null) BuscarCampo(o.GetType(), campo)?.SetValue(o, v); }
        private static void Llamar(object o, string metodo, params object[] args) =>
            o.GetType().GetMethods(Priv).FirstOrDefault(m => m.Name == metodo && m.GetParameters().Length == args.Length)?.Invoke(o, args);

        private IEnumerator Start()
        {
            float limite = Time.realtimeSinceStartup + 20f;
            while ((SalaPruebas.Actual == null || FindFirstObjectByType<PlayerControler>() == null) && Time.realtimeSinceStartup < limite) yield return null;
            yield return new WaitForSecondsRealtime(1f);
            sala = SalaPruebas.Actual;
            if (sala == null) { Fallo("la sala no arranca"); Salir(); yield break; }
            yield return Seguro(Pruebas());
            Comprobar(errores.Count == 0, "sin errores en la consola " + (errores.Count > 0 ? "(" + string.Join(" | ", errores.Distinct().Take(5)) + ")" : ""));
            Salir();
        }

        private static IEnumerator Seguro(IEnumerator raiz)
        {
            var pila = new Stack<IEnumerator>();
            pila.Push(raiz);
            while (pila.Count > 0)
            {
                IEnumerator r = pila.Peek();
                bool sigue;
                try { sigue = r.MoveNext(); }
                catch (Exception e) { Fallo("excepcion en la prueba: " + e.GetType().Name + " " + e.Message + " " + e.StackTrace?.Split('\n').FirstOrDefault()); sigue = false; }
                if (!sigue) { pila.Pop(); continue; }
                if (r.Current is IEnumerator hijo) { pila.Push(hijo); continue; }
                yield return r.Current;
            }
        }

        private void Salir()
        {
            Time.timeScale = 1f;
            EditorApplication.ExitPlaymode();
        }

        private static IEnumerator Esperar(Func<bool> c, float segundos)
        {
            float t = Time.realtimeSinceStartup + segundos;
            while (!c() && Time.realtimeSinceStartup < t) yield return null;
        }

        private PlayerControler P => FindFirstObjectByType<PlayerControler>();
        private JefeBlackKnight J => sala.Jefe as JefeBlackKnight;
        private EnemyHealth Salud => J != null ? J.GetComponent<EnemyHealth>() : null;
        private CatalogoPruebas.Entrada Entrada =>
            CrearBlackKnight.EntradaEn(sala.Catalogo, AssetDatabase.LoadAssetAtPath<GameObject>(CrearBlackKnight.RutaPrefab));
        private static string Lista(List<PlayerControler.ResultadoDano?> r) => string.Join(", ", r.Select(x => x.HasValue ? x.Value.ToString() : "fuera"));

        // Un golpe tuyo de espada al jefe (de frente o por la espalda segun donde estes).
        private void Pegar(int dano = 20)
        {
            EnemyHealth s = Salud;
            if (s == null || P == null) return;
            EnemyHealth.ArmaDelGolpe = TipoArma.Espada;
            s.TakeDamage(dano, (Vector2)P.transform.position, 1f);
        }

        private void Invocar()
        {
            sala.Invocar(Entrada);
        }

        private IEnumerator Pruebas()
        {
            Comprobar(Entrada != null && Entrada.prefab != null && Entrada.nombre == Ficha.nombreMostrado && Entrada.barraPorFase,
                      "esta en el catalogo de la sala con el nombre de la ficha y barra por fase");
            if (Entrada == null) yield break;
            sala.PonerInvencible(true);
            Invocar();
            yield return Esperar(() => J != null, 3f);
            JefeBlackKnight j = J;
            Comprobar(j != null, "se invoca con su arena");
            if (j == null) yield break;
            Escuchar(j);
            yield return Esperar(() => j == null || j.HoraUltimoAtaque >= 0f, 25f);
            Comprobar(j != null && j.HoraUltimoAtaque >= 0f, "entra en la arena y actua solo (" + (j != null ? j.UltimoAtaque : "-") + ")");
            Comprobar(FindFirstObjectByType<BarraJefe>() != null, "tiene una barra de vida");
            Info("la sala lista: " + string.Join(", ", j.AtaquesPrueba));

            sala.PonerInvencible(false);
            sala.VidaInfinita = true;
            yield return Ataques(j);
            yield return SinBloquear(j);
            yield return GuardiaDelJefe(j);
            yield return Frasco(j);
            yield return Machacar(j);
            yield return FaseDos(j);
            Comprobar(Mathf.Approximately(j.Multiplicador(Elemento.Fuego), j.Ajustes.multFuego) && Mathf.Approximately(j.Multiplicador(Elemento.Natura), 1f),
                      "el jefe usa sus afinidades de la ficha");
            yield return Sello(j);
            yield return ErupcionesPrueba(j);
            yield return Ondas(j);
            yield return FaseTres(j);
            yield return Sombras(j);
            Comprobar(maxActivos <= 1, $"nunca hay dos golpes activos a la vez (maximo {maxActivos})");
            Comprobar(avisosMal.Count == 0, $"todos los golpes avisan al menos el minimo de su peso ({avisosCortos} cortos)"
                                             + (avisosMal.Count > 0 ? ": " + string.Join(", ", avisosMal.Distinct().Take(5)) : ""));
            yield return Muerte();
            yield return Duelos();
            yield return Reapariciones();
        }

        private void Escuchar(JefeBlackKnight j)
        {
            ReglasCombo r = j.Ajustes.reglas;
            j.AlActivarGolpe += (g, aviso, hueco) =>
            {
                float min = Mathf.Max(r.AvisoMinimo(g.peso, false), 0f);
                // El aviso real incluye lo dibujado; con un margen de un fotograma.
                if (aviso + 0.04f < min) { avisosCortos++; avisosMal.Add($"{g.nombre} {aviso:0.00}<{min:0.00}"); }
            };
            j.AlActivarGolpe += (g, aviso, hueco) =>
            {
                Bounds b = P.GetComponent<Collider2D>().bounds;
                alActivar = $"al activar: jefe {j.transform.position.x:0.00} mira {j.Mirada}, jugador {b.min.x:0.00}..{b.max.x:0.00} y {b.min.y:0.00}, esquiva {P.Esquivando}, aturdido {P.Aturdido}, f{Time.frameCount}";
            };
            // Diagnostico: donde estaban los dos cuando un golpe no encontro al jugador.
            j.AlTerminarGolpe += (g, r) =>
            {
                if (r.HasValue || P == null) return;
                Bounds b = P.GetComponent<Collider2D>().bounds;
                Info($"FUERA {g.nombre}: jefe {j.transform.position.x:0.00},{j.transform.position.y:0.00} mira {j.Mirada}; jugador x {b.min.x:0.00}..{b.max.x:0.00} y {b.min.y:0.00}..{b.max.y:0.00}; caja {g.cajaCentro} {g.cajaTamano}; anim {j.GetComponentInChildren<AnimadorHoja>().Actual}:{j.GetComponentInChildren<AnimadorHoja>().Fotograma}; {alActivar}; fin f{Time.frameCount}");
            };
        }

        // Un ataque forzado de principio a fin.
        private IEnumerator UnAtaque(JefeBlackKnight j, string id, float tope = 25f)
        {
            int hechas = j.AccionesHechas.Count(a => a == id);
            sala.LanzarAtaque(id);
            yield return Esperar(() => j == null || j.AccionesHechas.Count(a => a == id) > hechas, tope);
        }

        // Cada ataque con parry a todo: el combo sigue y, si no lo corto, queda aturdido.
        private IEnumerator Ataques(JefeBlackKnight j)
        {
            bot = 1;
            foreach (AjustesBlackKnight.Ataque a in j.Ajustes.ataques.Where(a => a.faseMinima == 1))
            {
                int aturd = j.Aturdimientos.Count, cortados = j.CombosCortados;
                yield return UnAtaque(j, a.id);
                var res = new List<PlayerControler.ResultadoDano?>(j.Resultados);
                bool cortado = j.CombosCortados > cortados;
                int reales = a.golpes.Count(n => !j.Ajustes.Buscar(n).amago);
                bool todos = res.Count > 0 && res.All(x => x == PlayerControler.ResultadoDano.Parry);
                Comprobar(j.AccionesHechas.Contains(a.id) && todos && (cortado || res.Count == reales),
                          $"{a.id}, parry a todo: {res.Count}/{reales} golpes parados{(cortado ? " (lo corto y rodo)" : "")} ({Lista(res)})");
                if (!cortado && reales >= 2)
                    Comprobar(j.Aturdimientos.Count > aturd && j.Aturdimientos.Last().tipo == "combo", $"{a.id}: parar todos lo aturde");
            }
            int retrasos = j.Retrasos;
            for (int i = 0; i < 4; i++) yield return UnAtaque(j, "combo_ligero");
            Info($"retrasos en golpes hasta ahora: {j.Retrasos}; amagos {j.Amagos}; combos cortados {j.CombosCortados}");
            Comprobar(j.Amagos > 0, "el amago enseña un golpe y no lo da");
            yield return UnAtaque(j, JefeBlackKnight.AccRodadaEspalda);
            Comprobar(j.Resultados.Count == 1, "rodada a tu espalda y tajo (" + Lista(j.Resultados) + ")");
            yield return UnAtaque(j, JefeBlackKnight.AccSalto);
            Comprobar(j.Resultados.Count == 1 && j.Resultados[0] == PlayerControler.ResultadoDano.Parry && !j.Volando,
                      "salto y caida: se para mires hacia donde mires (" + Lista(j.Resultados) + ")");
        }

        // Sin bloquear: un combo nunca pasa del tope.
        private IEnumerator SinBloquear(JefeBlackKnight j)
        {
            bot = 3;
            int vidaMax = P.VidaMaxima;
            EstadosPlayer es = P.GetComponent<EstadosPlayer>();
            if (es != null) es.Vaciar(EstadoPlayer.Quemadura);
            foreach (string id in new[] { "combo_ligero", "combo_pesado" })
            {
                yield return UnAtaque(j, id);
                float tope = j.Ajustes.reglas.topeDanoCombo * vidaMax + 1;
                Comprobar(j.Ritmo.DanoCombo <= tope, $"{id} sin bloquear: {j.Ritmo.DanoCombo} de dano (tope {tope:0})");
            }
            es = P.GetComponent<EstadosPlayer>();
            Comprobar(es != null && (es.Visible(EstadoPlayer.Quemadura) || es.EnEfecto(EstadoPlayer.Quemadura)),
                      "sus golpes te acumulan quemadura (fase 1)");
            bot = 1;
        }

        // Su guardia: tu golpe de frente no entra, te aturde y responde.
        private IEnumerator GuardiaDelJefe(JefeBlackKnight j)
        {
            bot = 0;
            SetCampo(P, "isBlocking", false);
            sala.LanzarAtaque(JefeBlackKnight.AccGuardia);
            yield return Esperar(() => j.EnGuardia, 25f);
            Comprobar(j.EnGuardia, "levanta su guardia");
            // Que estes delante de el, a un paso (como al pegarle de verdad).
            P.transform.position = new Vector3(j.transform.position.x + j.Mirada * 1.1f, P.transform.position.y, 0f);
            yield return null;
            Llamar(j, "MirarYa");
            int vida = Salud.CurrentHealth, parrys = j.ParrysDelJefe;
            Pegar();
            yield return null;
            Comprobar(Salud.CurrentHealth == vida && j.ParrysDelJefe == parrys + 1 && P.Aturdido,
                      $"le pegas en guardia: no entra ({vida} -> {Salud.CurrentHealth}), para tu golpe y te aturde");
            bot = 1;
            yield return Esperar(() => j.AccionesHechas.Contains(JefeBlackKnight.AccGuardia), 10f);
            Comprobar(j.Resultados.Count == 1, "responde con el estallido (" + Lista(j.Resultados) + ")");
        }

        // Bebes cerca: desenvaine.
        private IEnumerator Frasco(JefeBlackKnight j)
        {
            yield return Esperar(() => j.AccionActual == "", 15f);
            int lect = j.Lecturas.Count(l => l == JefeBlackKnight.AccDesenvaine);
            ReservaPociones rp = ReservaPociones.Get();
            rp.Rellenar();
            bool bebio = rp.Gastar(ReservaPociones.Tipo.Vida);
            Info($"frasco: bebio {bebio} (cargas {rp.Cargas}/{rp.Maximo}), a {Mathf.Abs(P.transform.position.x - j.transform.position.x):0.0} u del jefe");
            yield return Esperar(() => j.Lecturas.Count(l => l == JefeBlackKnight.AccDesenvaine) > lect, 15f);
            Info($"frasco: lecturas {string.Join(", ", j.Lecturas)}; ultimas acciones {string.Join(", ", j.AccionesHechas.Skip(Mathf.Max(0, j.AccionesHechas.Count - 4)))}");
            Comprobar(j.Lecturas.Count(l => l == JefeBlackKnight.AccDesenvaine) > lect, "bebes un frasco cerca: va a por ti con el desenvaine");
        }

        // Machacas el ataque cerca: reacciona (guardia, rodada o se aparta).
        private IEnumerator Machacar(JefeBlackKnight j)
        {
            int antes = j.Lecturas.Count(l => l != JefeBlackKnight.AccDesenvaine && l != JefeBlackKnight.AccSalto);
            float fin = Time.realtimeSinceStartup + 25f;
            while (Time.realtimeSinceStartup < fin && j.Lecturas.Count(l => l != JefeBlackKnight.AccDesenvaine && l != JefeBlackKnight.AccSalto) == antes)
            {
                // Cerca de el (la sala te deja moverte con el transform) y pulsando.
                if (Mathf.Abs(P.transform.position.x - j.transform.position.x) > 2f)
                    P.transform.position = new Vector3(j.transform.position.x - Mathf.Sign(j.Mirada) * -1.6f, P.transform.position.y, 0f);
                SetCampo(P, "isAttacking", true);
                yield return new WaitForSeconds(0.08f);
                SetCampo(P, "isAttacking", false);
                yield return new WaitForSeconds(0.25f);
            }
            var nuevas = j.Lecturas.Where(l => l != JefeBlackKnight.AccDesenvaine && l != JefeBlackKnight.AccSalto).ToList();
            Comprobar(nuevas.Count > antes, $"machacas cerca: te lee ({string.Join(", ", nuevas.Skip(antes))}); adaptacion {j.Adaptacion:0.00}");
        }

        private IEnumerator FaseDos(JefeBlackKnight j)
        {
            yield return Esperar(() => j.AccionActual == "", 15f);
            sala.SaltarAFase(2);
            yield return Esperar(() => j.Cambiando, 3f);
            Comprobar(j.Cambiando && j.Modificar(50, TipoArma.Espada) == 0, "al vaciar la primera barra cambia de fase y no le entra nada");
            yield return Esperar(() => !j.Cambiando, 6f);
            Comprobar(j.Fase == 2 && Salud.MaxHealth == j.Ajustes.vidaFase2 && Salud.CurrentHealth == Salud.MaxHealth,
                      $"fase 2 con su barra llena ({Salud.CurrentHealth}/{Salud.MaxHealth})");
        }

        // Ondas de fuego: te empuja, se va al otro extremo, no se paran y se esquivan; pegarle las corta.
        private IEnumerator Ondas(JefeBlackKnight j)
        {
            bot = 1;
            yield return Esperar(() => j.AccionActual == "", 15f);
            int lanzadas = j.OndasLanzadas;
            sala.LanzarAtaque(JefeBlackKnight.AccOndas);
            yield return Esperar(() => j.EnOndas && !j.Retirandose, 15f);
            float dx = Mathf.Abs(j.transform.position.x - P.transform.position.x);
            Comprobar(j.EnOndas && dx > 5f, $"te empuja y se va al otro extremo (a {dx:0.0} u de ti)");
            yield return new WaitForSeconds(1.2f);
            ComprobarCamara(j, "ondas");
            int vida = P.VidaActual;
            yield return Esperar(() => FindObjectsByType<OndaFuegoBK>(FindObjectsSortMode.None).Any(o => o.Tocada || o.Esquivada), 10f);
            var tocadas = FindObjectsByType<OndaFuegoBK>(FindObjectsSortMode.None).Where(o => o.Tocada).ToList();
            Comprobar(tocadas.Count > 0 && P.VidaActual < vida, $"con parry a todo, la onda te toca igual ({vida} -> {P.VidaActual}): no se para");
            bot = 6;
            yield return Esperar(() => FindObjectsByType<OndaFuegoBK>(FindObjectsSortMode.None).Any(o => o.Esquivada), 10f);
            Comprobar(FindObjectsByType<OndaFuegoBK>(FindObjectsSortMode.None).Any(o => o.Esquivada), "esquivando, se atraviesa la onda");
            Comprobar(j.OndasLanzadas - lanzadas >= 2, $"lanza ondas una tras otra ({j.OndasLanzadas - lanzadas})");
            int cortadas = j.OndasCortadas;
            // Llegas hasta el y le pegas.
            P.transform.position = new Vector3(j.transform.position.x - Mathf.Sign(j.transform.position.x - P.transform.position.x) * 1.2f, P.transform.position.y, 0f);
            Pegar();
            yield return Esperar(() => j.OndasCortadas > cortadas, 3f);
            Comprobar(j.OndasCortadas > cortadas && !j.EnOndas, "le pegas: corta las ondas");
            yield return Esperar(() => j.Aturdido, 2f);
            Comprobar(j.Aturdimientos.Count > 0 && j.Aturdimientos.Last().tipo == "ondas", "y queda aturdido un momento");
            bot = 1;
        }

        // La camara abierta y centrada: el jefe se ve aunque este lejos.
        private void ComprobarCamara(JefeBlackKnight j, string que)
        {
            Camera c = Camera.main;
            Vector3 v = c != null ? c.WorldToViewportPoint(j.transform.position + Vector3.up * 0.6f) : Vector3.zero;
            Vector3 vp = c != null ? c.WorldToViewportPoint(P.transform.position) : Vector3.zero;
            bool dentro = v.x > 0f && v.x < 1f && v.y > 0f && v.y < 1f && vp.x > 0f && vp.x < 1f;
            // Con esta pantalla, el tamano que haria falta para veros a los dos; si pasa del
            // tope de la ficha, basta con que este abierta a tope (en 16:9 cabe de sobra).
            float aspecto = c != null ? c.aspect : 1.78f;
            float dx = Mathf.Abs(j.transform.position.x - P.transform.position.x);
            float hace = (dx * 0.5f + j.Ajustes.margenCamara) / aspecto;
            bool aTope = hace > j.Ajustes.tamanoMaxCamara && c != null && c.orthographicSize >= j.Ajustes.tamanoMaxCamara - 0.3f;
            Comprobar(j.CamaraAmplia && (dentro || aTope), $"{que}: la camara se abre (tamano {(c != null ? c.orthographicSize : 0f):0.0}, pantalla {aspecto:0.00}) y se ven los dos (jefe en {v.x:0.00}, tu en {vp.x:0.00})"
                      + (aTope && !dentro ? $"; en 16:9 bastaria {(dx * 0.5f + j.Ajustes.margenCamara) / 1.78f:0.0}" : ""));
        }

        // Sello: con amuletos puestos te sella uno 25 s (sigue puesto pero no hace nada).
        private IEnumerator Sello(JefeBlackKnight j)
        {
            bot = 1;
            Amuletos.Reiniciar();
            foreach (string id in new[] { EfectosAmuletos.Pluma, EfectosAmuletos.Vigia }) { Amuletos.Dar(id); Amuletos.Equipar(id); }
            Comprobar(EfectosAmuletos.Lleva(EfectosAmuletos.Pluma) && EfectosAmuletos.Lleva(EfectosAmuletos.Vigia), "llevas dos amuletos puestos (Pluma y Vigia)");
            yield return Esperar(() => j.AccionActual == "", 15f);
            int sellos = j.SellosPuestos;
            yield return UnAtaque(j, JefeBlackKnight.AccSello);
            string s = EfectosAmuletos.Sellado;
            Comprobar(j.SellosPuestos == sellos + 1 && (s == EfectosAmuletos.Pluma || s == EfectosAmuletos.Vigia),
                      $"te sella un amuleto al azar ({s})");
            Comprobar(s != null && !EfectosAmuletos.Lleva(s) && Amuletos.HuecoDe(s) >= 0, "el sellado sigue equipado pero no hace nada");
            Comprobar(EfectosAmuletos.SegundosSello > j.Ajustes.duracionSello - 3f && IndicadorSelloBK.Visible,
                      $"dura {j.Ajustes.duracionSello} s y se ve en pantalla (quedan {EfectosAmuletos.SegundosSello:0})");
            string otro = s == EfectosAmuletos.Pluma ? EfectosAmuletos.Vigia : EfectosAmuletos.Pluma;
            Comprobar(EfectosAmuletos.Lleva(otro), "el otro amuleto sigue funcionando");
        }

        // Erupciones: se aleja, columnas de fuego de distintos tamanos (no se paran) y salta sobre ti.
        private IEnumerator ErupcionesPrueba(JefeBlackKnight j)
        {
            bot = 1;
            yield return Esperar(() => j.AccionActual == "", 15f);
            int lanzadas = j.ErupcionesLanzadas, hechas = j.AccionesHechas.Count(a => a == JefeBlackKnight.AccErupciones);
            var tamanos = new HashSet<string>();
            var vistas = new HashSet<ErupcionBK>();
            int tocadas = 0;
            sala.LanzarAtaque(JefeBlackKnight.AccErupciones);
            yield return Esperar(() => j.EnErupciones && !j.Retirandose && j.ErupcionesLanzadas > lanzadas, 15f);
            float dx = Mathf.Abs(j.transform.position.x - P.transform.position.x);
            Comprobar(j.EnErupciones && dx > 5f, $"erupciones: se va lejos (a {dx:0.0} u) y hace la pose del buff");
            yield return new WaitForSeconds(0.8f);
            ComprobarCamara(j, "erupciones");
            float fin = Time.realtimeSinceStartup + 25f;
            while (Time.realtimeSinceStartup < fin && j.AccionesHechas.Count(a => a == JefeBlackKnight.AccErupciones) == hechas)
            {
                foreach (ErupcionBK e in FindObjectsByType<ErupcionBK>(FindObjectsSortMode.None))
                {
                    tamanos.Add($"{e.Ancho:0.0}x{e.Alto:0.0}");
                    if (e.Tocada && vistas.Add(e)) tocadas++;
                }
                yield return null;
            }
            int n = j.ErupcionesLanzadas - lanzadas;
            Comprobar(n >= 8 && tamanos.Count >= 4, $"salen {n} erupciones de distintos tamanos ({tamanos.Count} distintos)");
            Comprobar(tocadas > 0, $"con parry a todo, las erupciones te alcanzan igual: no se paran ({tocadas})");
            Comprobar(j.AccionesHechas.Count(a => a == JefeBlackKnight.AccErupciones) > hechas && j.Resultados.Count == 1 && !j.CamaraAmplia,
                      "al acabar salta sobre ti (" + Lista(j.Resultados) + ") y la camara vuelve");
        }

        private IEnumerator FaseTres(JefeBlackKnight j)
        {
            yield return Esperar(() => j.AccionActual == "" && !j.Aturdido, 15f);
            j.SaltarAFase(3);
            yield return Esperar(() => !j.Cambiando && j.Fase == 3, 8f);
            Comprobar(j.Fase == 3 && Salud.MaxHealth == j.Ajustes.vidaFase3, $"fase 3 con su tercera barra ({Salud.CurrentHealth}/{Salud.MaxHealth})");
            BarraJefe b = FindFirstObjectByType<BarraJefe>();
            Comprobar(FindObjectsByType<BarraJefe>(FindObjectsSortMode.None).Length == 1, "sigue habiendo una sola barra (la misma, de otro color)");
        }

        // Sombras: de lado y desde arriba, se paran; el jefe recibe dano con la espada clavada.
        private IEnumerator Sombras(JefeBlackKnight j)
        {
            bot = 1;
            yield return Esperar(() => j.AccionActual == "", 15f);
            int invocadas = j.SombrasInvocadas;
            int resultadosParry = 0, resultados = 0;
            Action<AjustesBlackKnight.Golpe, PlayerControler.ResultadoDano?> fin = (g, r) => { resultados++; if (r == PlayerControler.ResultadoDano.Parry) resultadosParry++; };
            j.AlTerminarGolpe += fin;
            sala.LanzarAtaque(JefeBlackKnight.AccSombras);
            yield return Esperar(() => j.Clavado, 15f);
            Comprobar(j.Clavado, "clava la espada en el suelo");
            yield return new WaitForSeconds(1f);
            Comprobar(j.CamaraAmplia, "sombras: la camara se abre mientras tiene la espada clavada");
            int vida = Salud.CurrentHealth;
            Pegar();
            Comprobar(Salud.CurrentHealth < vida, $"con la espada clavada le entra dano ({vida} -> {Salud.CurrentHealth})");
            yield return Esperar(() => !j.Clavado, 15f);
            j.AlTerminarGolpe -= fin;
            var tipos = j.TiposSombra.Skip(0).ToList();
            Comprobar(j.SombrasInvocadas - invocadas >= 3 && tipos.Contains("lado") && tipos.Contains("arriba"),
                      $"salen sombras de una en una ({j.SombrasInvocadas - invocadas}: {string.Join(", ", tipos)})");
            Comprobar(resultados > 0 && resultadosParry == resultados, $"sus golpes se paran ({resultadosParry}/{resultados})");
            yield return Esperar(() => FindFirstObjectByType<SombraBK>() == null, 3f);
            Comprobar(FindFirstObjectByType<SombraBK>() == null, "al acabar no queda ninguna sombra");
        }

        private IEnumerator Muerte()
        {
            JefeBlackKnight j = J;
            if (j == null) yield break;
            int vencidos = 0;
            Action alVencer = () => vencidos++;
            ArenaJefe.AlVencer += alVencer;
            sala.Matar();
            yield return Esperar(() => Salud == null || Salud.Muerto, 3f);
            yield return new WaitForSeconds(0.5f);
            Comprobar(Salud != null && Salud.Muerto && FindFirstObjectByType<OndaFuegoBK>() == null && FindFirstObjectByType<SombraBK>() == null,
                      "muere en la fase 3 sin dejar ondas ni sombras");
            Comprobar(EfectosAmuletos.Sellado == null && !IndicadorSelloBK.Visible && FindFirstObjectByType<ErupcionBK>() == null,
                      "al morir quita el sello del amuleto y no deja erupciones");
            Amuletos.Reiniciar();
            ArenaJefe.AlVencer -= alVencer;
            Info("victorias anunciadas: " + vencidos);
        }

        // Peleas libres (40 s): cuanta vida pierde un jugador que para todo y uno que no bloquea.
        private IEnumerator Duelos()
        {
            foreach (int b in new[] { 1, 3 })
            {
                Invocar();
                yield return Esperar(() => J != null, 3f);
                JefeBlackKnight j = J;
                if (j == null) { Fallo("duelo: no aparece"); yield break; }
                sala.VidaInfinita = true;
                bot = b;
                int danoTotal = 0, ultima = P.VidaActual;
                float fin = Time.time + 40f;
                while (Time.time < fin && j != null)
                {
                    int v = P.VidaActual;
                    if (v < ultima) danoTotal += ultima - v;
                    ultima = v;
                    yield return null;
                }
                Info($"DUELO {(b == 1 ? "parry a todo" : "sin bloquear")}: dano recibido {danoTotal} en 40 s; acciones: {string.Join(", ", j.AccionesHechas)}");
            }
            bot = 0;
        }

        private IEnumerator Reapariciones()
        {
            for (int i = 0; i < 5; i++)
            {
                Invocar();
                yield return new WaitForSeconds(0.6f);
            }
            yield return Esperar(() => J != null && J.HoraUltimoAtaque >= 0f, 20f);
            Comprobar(J != null && FindObjectsByType<JefeBlackKnight>(FindObjectsSortMode.None).Length == 1, "5 reapariciones seguidas: un solo jefe y vuelve a actuar");
            sala.Limpiar();
            yield return null;
            Comprobar(FindFirstObjectByType<OndaFuegoBK>() == null && FindFirstObjectByType<SombraBK>() == null, "al limpiar la sala no queda nada suyo");
        }
    }
}
