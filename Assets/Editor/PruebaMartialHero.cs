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

// Prueba del Martial Hero en la sala de pruebas (batchmode: -executeMethod PruebaMartialHero.Todo).
//   1. Sin jugar: huellas del progreso real y de los PNG originales, Warrior >
//      Martial Hero > Crear o actualizar, sprites (importacion, cuadricula y pies
//      en la misma linea), ficha (avisos, dano, tope del combo, pausa minima) y
//      que no esta en Resources, Build Settings, Bestiario ni Desafios.
//   2. En la sala: se invoca, entra y ataca solo; cada variante del combo con el
//      jugador automatico (parry a todo, parry menos el ultimo, solo bloqueo, sin
//      bloquear); aturdimiento solo si paras los 3; huecos entre golpes; se cubre
//      cuando no ataca (menos dano de frente, entero por la espalda) y contraataca
//      con un combo si le pegas; en su recuperacion entra entero y no contraataca;
//      ataca enseguida si entras en su radio, al acabar su guardia si estas cerca y
//      corriendo si estas lejos; aturdido recibe mas dano y luego queda inmune
//      (tambien al sagrado); el sagrado rompe la guardia (sin aturdir si esta
//      inmune); todos los avisos naranjas hasta el instante del golpe; Dificil: el
//      tope del combo y el dano maximo de un golpe; muerte y 10 reapariciones.
//   2b. Jugador automatico: 4 peleas libres de 60 s (parry a todo, solo bloqueo,
//      sin bloquear y atacando sin parar): vida que pierde cada uno y cuantos
//      aturdimientos hacen falta para matar al jefe (lineas "[MARTIAL] DUELO").
//   3. Al salir: el progreso real y los originales son identicos.
// Escribe "[MARTIAL] OK ..." / "[MARTIAL] FALLO ..." y el total.
[InitializeOnLoad]
public static class PruebaMartialHero
{
    private const string Pre = "PruebaMartialHero.";

    private static string Fase { get => SessionState.GetString(Pre + "Fase", ""); set => SessionState.SetString(Pre + "Fase", value); }

    static PruebaMartialHero()
    {
        EditorApplication.playModeStateChanged += e =>
        {
            if (Fase != "jugar") return;
            if (e == PlayModeStateChange.EnteredPlayMode) new GameObject("PruebaMartialHero").AddComponent<Conductor>();
            else if (e == PlayModeStateChange.EnteredEditMode)
            {
                Fase = "salida";
                SessionState.SetFloat(Pre + "Desde", (float)EditorApplication.timeSinceStartup);
                EditorApplication.update += Continuar;
            }
        };
        if (Fase == "salida") EditorApplication.update += Continuar;
    }

    private static void Ok(string s) { SessionState.SetInt(Pre + "Ok", SessionState.GetInt(Pre + "Ok", 0) + 1); Debug.Log("[MARTIAL] OK " + s); }
    private static void Fallo(string s) { SessionState.SetInt(Pre + "Fallos", SessionState.GetInt(Pre + "Fallos", 0) + 1); Debug.LogWarning("[MARTIAL] FALLO " + s); }
    private static void Info(string s) => Debug.Log("[MARTIAL] INFO " + s);
    private static void Comprobar(bool c, string s) { if (c) Ok(s); else Fallo(s); }

    // ------------------------------------------------------------------ Sin jugar

    public static void Todo()
    {
        SessionState.SetInt(Pre + "Ok", 0);
        SessionState.SetInt(Pre + "Fallos", 0);
        SessionState.SetString(Pre + "Antes", Instantanea());
        SessionState.SetString(Pre + "Originales", HuellaOriginales());

        CrearMartialHero.Crear();
        Comprobar(CrearMartialHero.Comprobar(out string informe), "sprites: " + informe);
        ComprobarCuadros();
        ComprobarFicha();
        ComprobarAislado();

        Fase = "jugar";
        EditorSceneManager.OpenScene(SalaPruebas.Ruta, OpenSceneMode.Single);
        ArranquePruebas.Proteger();
        EditorApplication.EnterPlaymode();
    }

    private static AjustesMartialHero Ficha => AssetDatabase.LoadAssetAtPath<AjustesMartialHero>(CrearMartialHero.RutaFicha);

    // Cada tira: cuadros de 200x200 y, en todos, los pies en la misma linea
    // (y = 122 desde arriba; la carrera levanta 1 px) y el mismo anclaje en el prefab.
    private static void ComprobarCuadros()
    {
        foreach (string t in CrearMartialHero.Tiras)
        {
            byte[] png = File.ReadAllBytes($"{CrearMartialHero.CarpetaSprites}/{t}.png");
            var tex = new Texture2D(2, 2);
            tex.LoadImage(png);
            int n = tex.width / CrearMartialHero.Celda;
            var pies = new List<int>();
            for (int i = 0; i < n; i++)
            {
                int abajo = -1;
                // Primera fila con dibujo contando desde abajo (y de Unity: 0 = abajo).
                for (int y = 0; y < CrearMartialHero.Celda && abajo < 0; y++)
                    for (int x = 0; x < CrearMartialHero.Celda; x++)
                        if (tex.GetPixel(i * CrearMartialHero.Celda + x, y).a > 0.03f) { abajo = y; break; }
                pies.Add(CrearMartialHero.Celda - 1 - abajo);
            }
            Object.DestroyImmediate(tex);
            bool bien = t == "Fall" || pies.All(p => p >= 120 && p <= 121);
            Comprobar(n > 0 && bien, $"{t}: {n} cuadros de 200x200, pies en la fila {string.Join("/", pies.Distinct())} (121 = y 122 del borde)");
        }
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CrearMartialHero.RutaPrefab);
        AnimadorHoja an = prefab != null ? prefab.GetComponentInChildren<AnimadorHoja>() : null;
        string[] clips = { "quieto", "correr", "caer", "desenvaine", "tajo", "guardia", "aturdido", "levantarse", "muerte" };
        Comprobar(an != null && clips.All(c => an.clips.Any(k => k.nombre == c && k.hoja != null)), "el prefab tiene sus clips: " + string.Join(", ", clips));
        if (an == null) return;
        bool mismos = an.clips.All(k => k.anchoCelda == 200 && k.altoCelda == 200 && k.pivote == CrearMartialHero.Pivote && Mathf.Approximately(k.pixelesPorUnidad, 28f));
        Comprobar(mismos, $"todos los clips con la misma celda (200x200), anclaje {CrearMartialHero.Pivote} y 28 px por unidad");
        AnimadorHoja.Clip d = an.clips.FirstOrDefault(k => k.nombre == "desenvaine");
        Comprobar(d != null && d.cantidad == 6, "el desenvaine tiene 6 cuadros (no los 8 cortes de Unity)");
    }

    // Avisos, dano, cajas y tope de cada variante.
    private static void ComprobarFicha()
    {
        AjustesMartialHero aj = Ficha;
        Comprobar(aj != null, "ficha " + CrearMartialHero.RutaFicha);
        if (aj == null) return;
        ReglasCombo r = aj.reglas;
        int vidaMax = AjustesProgreso.Get().vidaBase;
        float multDificil = Mathf.Max(1f, SalaPruebas.Cargar<AjustesSalaPruebas>("Ajustes Sala Pruebas").multDano);
        Comprobar(!string.IsNullOrEmpty(aj.nombreMostrado), "nombre en la ficha: " + aj.nombreMostrado);
        foreach (AjustesMartialHero.Golpe g in new[] { aj.desenvaine, aj.tajo })
        {
            string n = g.nombre + ": ";
            int dif = Mathf.RoundToInt(g.dano * multDificil);
            Comprobar(dif < vidaMax, n + $"no mata con la vida llena ({g.dano}, en Dificil {dif} de {vidaMax})");
            Comprobar(g.cuadroAviso < g.golpeDesde && g.golpeDesde <= g.golpeHasta && g.golpeHasta < 6, n + $"cuadros aviso {g.cuadroAviso}, golpe {g.golpeDesde}-{g.golpeHasta}");
            Comprobar(g.cajaTamano.x > 0f && g.cajaTamano.y > 0f && g.cajaTamano.x < 3f, n + $"caja propia {g.cajaTamano} (el arco dibujado mide hasta 3,8 u)");
            Comprobar(Mathf.Max(g.aviso, r.AvisoMinimo(g.peso, false)) >= (g.peso == PesoGolpe.Pesado ? r.avisoMinPesado : r.avisoMinLigero),
                      n + $"aviso {Mathf.Max(g.aviso, r.AvisoMinimo(g.peso, false)):0.00} s ({g.peso})");
        }
        float entreGolpes = 6f / aj.fpsAtaque;
        Comprobar(entreGolpes >= 0.5f - 0.001f, $"con los clips seguidos hay {entreGolpes:0.00} s de golpe a golpe (el jugador machaca si pulsa antes de 0,5 s; tras un parry bueno no cuenta)");
        Comprobar(aj.desenvaine.peso == PesoGolpe.Ligero && aj.tajo.peso == PesoGolpe.Ligero && r.colorLigero != Color.white,
                  "todos los golpes son ligeros: aviso naranja y rapido (ninguno rojo ni blanco)");
        foreach (AjustesMartialHero.Variante v in aj.variantes)
        {
            int suma = v.golpes.Sum(t => aj.DeTipo(t).dano);
            int dif = Mathf.RoundToInt(suma * multDificil);
            float tope = r.topeDanoCombo * vidaMax;
            Comprobar(v.golpes.Count == 4 && v.golpes.Count <= r.maxGolpesSeguidos,
                      $"{v.id}: {string.Join(", ", v.golpes)} = {suma} ({100f * suma / vidaMax:0}%), en Dificil {dif}; tope {tope:0}" + (dif > tope ? " -> se recorta al tope" : ""));
        }
    }

    // Solo en la sala: nada en Resources, Build Settings, Bestiario ni Desafios.
    private static void ComprobarAislado()
    {
        string guid = AssetDatabase.AssetPathToGUID(CrearMartialHero.RutaPrefab);
        bool enResources = AssetDatabase.FindAssets("", new[] { "Assets/Resources" })
            .Select(AssetDatabase.GUIDToAssetPath).Where(p => p.EndsWith(".asset") || p.EndsWith(".prefab"))
            .Any(p => File.ReadAllText(p).Contains(guid));
        Comprobar(!enResources, "nada de Resources apunta al Martial Hero");
        bool enEscenas = EditorBuildSettings.scenes.Any(s => File.Exists(s.path) && File.ReadAllText(s.path).Contains(guid));
        Comprobar(!enEscenas, "no esta en ninguna escena de Build Settings");
        Comprobar(FichaBestiario.Get().entradas.All(e => e.enemigo == null || e.enemigo.name != "JefeMartialHero") && FichaJefe.Todas().All(f => f.id != "Martial Hero"),
                  "no esta en el Bestiario ni en los Desafios");
    }

    // ------------------------------------------------------------------ Al salir

    private static void Continuar()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Pre + "Desde", 0f) < 3.0) return;
        EditorApplication.update -= Continuar;
        Comprobar(Instantanea() == SessionState.GetString(Pre + "Antes", ""), "el progreso real es identico (partidas, globales.json y PlayerPrefs)");
        Comprobar(HuellaOriginales() == SessionState.GetString(Pre + "Originales", ""), "los PNG originales y sus .meta no cambiaron");
        int ok = SessionState.GetInt(Pre + "Ok", 0), fallos = SessionState.GetInt(Pre + "Fallos", 0);
        Debug.Log($"[MARTIAL] RESULTADO: {ok} OK, {fallos} FALLOS");
        Fase = "";
        if (Application.isBatchMode) EditorApplication.Exit(fallos > 0 ? 1 : 0);
    }

    private static string HuellaOriginales()
    {
        var sb = new StringBuilder();
        using (SHA1 sha = SHA1.Create())
            foreach (string f in Directory.GetFiles(CrearMartialHero.CarpetaOriginal).OrderBy(x => x))
                sb.AppendLine(Path.GetFileName(f) + " " + BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(f))));
        return sb.ToString();
    }

    // Huellas del progreso real (como PruebaImpaler): archivos de las dos carpetas
    // (real y perfil de pruebas) y las claves registradas de PlayerPrefs.
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

        // Jugador automatico: 0 = no toca al jugador, 1 = parry a todo, 2 = solo
        // bloqueo, 3 = sin bloquear, 4 = parry a todo menos el ultimo golpe (bloqueo),
        // 5 = sin bloquear y atacando sin parar (en los duelos).
        private int bot;
        // Lo que se mide: golpes activos a la vez, peor hueco y peor aviso, y cada aturdimiento.
        private int maxActivos;
        private float peorHueco = 99f, peorAviso = 99f, inicioAturdido = -1f, peorEntreGolpes = 99f, ultimaActivacion = -99f;
        private readonly List<float> duracionesAturdido = new List<float>();
        // Avisos: el ultimo (peso, color, ritmo) y, por golpe, los que salieron bien o mal.
        private (PesoGolpe peso, Color color, float hz) ultimoAviso;
        private bool hayAviso;
        private readonly HashSet<string> golpesAvisados = new HashSet<string>();
        private readonly List<string> avisosMal = new List<string>();
        private int avisosLigeros, avisosPesados;
        // Dano que entra en cada golpe al jefe (antes y despues de su guardia).
        private int ultimoAntes, ultimoDespues;

        private void Update()
        {
            JefeMartialHero j = sala != null ? J : null;
            if (j == null) return;
            maxActivos = Mathf.Max(maxActivos, j.GolpesActivos);
            PlayerControler p = P;
            if (p == null || bot == 0) return;
            if (bot == 3 || bot == 5) { SetCampo(p, "isBlocking", false); return; }
            float x = j.transform.position.x;
            if (Mathf.Abs(x - p.transform.position.x) > 0.05f) Llamar(p, "SetFacing", x > p.transform.position.x ? 1 : -1);
            bool parry = bot == 1 || (bot == 4 && j.CombosHechos != null && j.Resultados.Count < CuantosGolpes(j) - 1);
            SetCampo(p, "isBlocking", true);
            SetCampo(p, "tBloqueo", parry ? 0f : 1f);
            SetCampo(p, "ventanaParryActual", Campo(p, "parryWindow"));
            PlayerStamina st = p.GetComponent<PlayerStamina>();
            if (st != null) st.Recuperar(100f);
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

        private void Awake()
        {
            Application.logMessageReceived += Registrar;
            // En silencio: la prueba no necesita oir nada (no cambia tus opciones de volumen).
            AudioListener.volume = 0f;
        }
        private void OnDestroy() => Application.logMessageReceived -= Registrar;

        private void Registrar(string c, string st, LogType t)
        {
            if ((t == LogType.Error || t == LogType.Exception) && !c.StartsWith("[MARTIAL]")) errores.Add(c);
            if (t == LogType.Warning && (c.StartsWith("[MartialHero]") || c.Contains("JefeMartialHero"))) errores.Add(c);
        }

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

        // Si una prueba lanza una excepcion, cuenta como fallo y no deja la prueba colgada.
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
        private JefeMartialHero J => sala.Jefe as JefeMartialHero;
        private CatalogoPruebas.Entrada Entrada =>
            CrearMartialHero.EntradaEn(sala.Catalogo, AssetDatabase.LoadAssetAtPath<GameObject>(CrearMartialHero.RutaPrefab));
        private static string Lista(List<PlayerControler.ResultadoDano?> r) => string.Join(", ", r.Select(x => x.HasValue ? x.Value.ToString() : "fuera"));
        private static readonly JefeMartialHero.EstadoMartial Guardia = JefeMartialHero.EstadoMartial.Guardia;
        // Golpes del combo que esta haciendo (o del primero de la ficha).
        private static int CuantosGolpes(JefeMartialHero j)
        {
            AjustesMartialHero.Variante v = j.Ajustes.Buscar(j.UltimoAtaque) ?? j.Ajustes.variantes[0];
            return v.golpes.Count;
        }

        private IEnumerator Pruebas()
        {
            Comprobar(Entrada != null && Entrada.prefab != null && Entrada.nombre == Ficha.nombreMostrado, "esta en el catalogo de la sala con el nombre de la ficha");
            if (Entrada == null) yield break;
            sala.PonerInvencible(true);
            sala.Invocar(Entrada);
            yield return Esperar(() => J != null, 3f);
            JefeMartialHero j = J;
            Comprobar(j != null, "se invoca con su arena");
            if (j == null) yield break;
            Escuchar(j);
            yield return Esperar(() => j == null || j.HoraUltimoAtaque >= 0f, 20f);
            Comprobar(j != null && j.HoraUltimoAtaque >= 0f, "entra en la arena y ataca solo (" + (j != null ? j.UltimoAtaque : "-") + ")");
            Comprobar(FindFirstObjectByType<BarraJefe>() != null, "tiene una barra de vida");
            Comprobar(j.AtaquesPrueba.SequenceEqual(new[] { "combo_cuadruple" }), "la sala lista sus ataques (solo su combo): " + string.Join(", ", j.AtaquesPrueba));

            yield return Triples(j);
            yield return GuardiaYContraataque(j);
            yield return Radio(j);
            yield return Cerca(j);
            yield return Inmunidad(j);
            yield return SagradoGuardia(j);
            Avisos(j);
            yield return NoRepite(j);
            yield return Muerte(j);
            yield return Dificil();
            yield return Duelos();
            yield return Reapariciones();
        }

        private void Escuchar(JefeMartialHero j)
        {
            ReglasCombo r = j.Ajustes.reglas;
            j.AlModificarDano += (a, d) => { ultimoAntes = a; ultimoDespues = d; };
            j.Ritmo.AlAvisar += (peso, color, hz) => { ultimoAviso = (peso, color, hz); hayAviso = true; };
            j.AlActivarGolpe += (g, aviso, hueco) =>
            {
                // Su aviso: el de su peso, con su ritmo, nunca blanco y aun encendido al golpear.
                bool bien = hayAviso && ultimoAviso.peso == g.peso && ultimoAviso.color == r.ColorAviso(g.peso) && Mathf.Approximately(ultimoAviso.hz, r.Parpadeo(g.peso))
                            && ultimoAviso.color != Color.white && j.Ritmo.AvisoActivo;
                if (!bien) avisosMal.Add($"{g.nombre} ({(hayAviso ? ultimoAviso.peso + " " + ultimoAviso.color : "sin aviso")}, activo {j.Ritmo.AvisoActivo})");
                else { golpesAvisados.Add(g.nombre); if (g.peso == PesoGolpe.Pesado) avisosPesados++; else avisosLigeros++; }
                hayAviso = false;
                if (j.Resultados.Count > 0) { peorHueco = Mathf.Min(peorHueco, hueco); peorEntreGolpes = Mathf.Min(peorEntreGolpes, Time.time - ultimaActivacion); }
                ultimaActivacion = Time.time;
                peorAviso = Mathf.Min(peorAviso, aviso);
            };
            j.AlCambiarEstadoMartial += e =>
            {
                if (e == JefeMartialHero.EstadoMartial.Aturdido) inicioAturdido = Time.time;
                else if (inicioAturdido >= 0f) { duracionesAturdido.Add(Time.time - inicioAturdido); inicioAturdido = -1f; }
            };
        }

        // Un ataque forzado de principio a fin (de una guardia a la siguiente).
        private IEnumerator UnAtaque(JefeMartialHero j, string id)
        {
            yield return Esperar(() => j.ProximoAtaque > Time.time, 20f);
            float desde = j.HoraUltimoAtaque;
            sala.LanzarAtaque(id);
            yield return Esperar(() => j.HoraUltimoAtaque > desde && j.UltimoAtaque == id, 20f);
            yield return Esperar(() => j.ProximoAtaque > Time.time, 20f);
        }

        // Cada variante con las cuatro maneras del jugador automatico.
        private IEnumerator Triples(JefeMartialHero j)
        {
            AjustesMartialHero aj = j.Ajustes;
            sala.PonerInvencible(false);
            sala.VidaInfinita = true;
            int vidaMax = P.VidaMaxima;
            foreach (AjustesMartialHero.Variante v in aj.variantes)
            {
                // Parry a todo: los 3 golpes salen y queda aturdido el tiempo de la ficha.
                yield return Esperar(() => j.ProximoAtaque > Time.time && !j.Inmune, 20f);
                bot = 1;
                int aturdidos = j.Aturdimientos.Count, hechos = j.CombosHechos.Count, duraciones = duracionesAturdido.Count;
                yield return UnAtaque(j, v.id);
                var r = new List<PlayerControler.ResultadoDano?>(j.Resultados);
                Comprobar(j.CombosHechos.Count > hechos && r.Count == v.golpes.Count && r.All(x => x == PlayerControler.ResultadoDano.Parry),
                          $"{v.id}, parry a todo: el combo sigue tras cada parry ({Lista(r)})");
                bool aturdido = j.Aturdimientos.Count == aturdidos + 1 && j.Aturdimientos.Last().tipo == "combo";
                float dur = duracionesAturdido.Count > duraciones ? duracionesAturdido.Last() : -1f;
                Comprobar(aturdido && Mathf.Abs(dur - aj.aturdidoCombo) < 0.15f,
                          $"{v.id}: parados todos, queda aturdido {dur:0.00} s (ficha {aj.aturdidoCombo} s)");

                // Parry menos el ultimo (bloqueo): sigue y no hay aturdimiento.
                yield return Esperar(() => j.ProximoAtaque > Time.time, 20f);
                bot = 4;
                aturdidos = j.Aturdimientos.Count;
                yield return UnAtaque(j, v.id);
                r = new List<PlayerControler.ResultadoDano?>(j.Resultados);
                Comprobar(r.Count == v.golpes.Count && j.Aturdimientos.Count == aturdidos, $"{v.id}, falla el ultimo: sin aturdimiento ({Lista(r)})");

                // Solo bloqueo.
                yield return Esperar(() => j.ProximoAtaque > Time.time, 20f);
                bot = 2;
                aturdidos = j.Aturdimientos.Count;
                yield return UnAtaque(j, v.id);
                r = new List<PlayerControler.ResultadoDano?>(j.Resultados);
                Comprobar(r.Count == v.golpes.Count && j.Aturdimientos.Count == aturdidos && r.All(x => x != PlayerControler.ResultadoDano.Parry),
                          $"{v.id}, solo bloqueo: sin aturdimiento ({Lista(r)}; te quita {j.DanoUltimoAtaque})");

                // Sin bloquear: nunca pasa del tope de la ficha.
                yield return Esperar(() => j.ProximoAtaque > Time.time, 20f);
                bot = 3;
                aturdidos = j.Aturdimientos.Count;
                yield return UnAtaque(j, v.id);
                r = new List<PlayerControler.ResultadoDano?>(j.Resultados);
                float tope = aj.reglas.topeDanoCombo * vidaMax;
                Comprobar(r.Count == v.golpes.Count && j.Aturdimientos.Count == aturdidos && j.DanoUltimoAtaque <= tope + 0.5f,
                          $"{v.id}, sin bloquear: te quita {j.DanoUltimoAtaque} de {vidaMax} (tope {tope:0}; {Lista(r)})");
            }
            bot = 0;
            SetCampo(P, "isBlocking", false);
            sala.VidaInfinita = false;
            sala.PonerInvencible(true);
            P.CurarCompleto();
            Comprobar(maxActivos <= 1, $"nunca dos golpes activos a la vez (maximo {maxActivos})");
            Comprobar(peorHueco >= aj.reglas.pausaMinEntreGolpes - 0.02f, $"hueco minimo entre golpes {peorHueco:0.00} s (ficha {aj.reglas.pausaMinEntreGolpes} s)");
            Comprobar(peorEntreGolpes >= 0.48f, $"de golpe a golpe del combo, nunca menos de {peorEntreGolpes:0.00} s (0,5 s = el limite de machacar)");
            Comprobar(peorAviso >= aj.reglas.avisoMinLigero - 0.02f, $"aviso real mas corto {peorAviso:0.00} s (minimo {aj.reglas.avisoMinLigero} s)");
        }

        // Un golpe de espada del jugador al jefe (15, el primero del combo).
        private static void Espadazo(JefeMartialHero j)
        {
            EnemyHealth.ArmaDelGolpe = TipoArma.Espada;
            j.GetComponent<EnemyHealth>().TakeDamage(15, (Vector2)j.transform.position + Vector2.right * j.Mirada);
        }

        // Pone al jugador a cierta distancia del jefe, delante (o detras) de el, quieto.
        private void Colocar(JefeMartialHero j, bool detras, float distancia = 1.2f)
        {
            PlayerControler p = P;
            Rigidbody2D rb = p.GetComponent<Rigidbody2D>();
            p.transform.position = new Vector3(j.transform.position.x + j.Mirada * (detras ? -distancia : distancia), j.transform.position.y + 0.3f, 0f);
            if (rb != null) { rb.position = p.transform.position; rb.linearVelocity = Vector2.zero; }
        }

        // Con el jugador ya cerca (asi no cuenta como entrar en su radio), espera a
        // que este en una guardia larga (pausa extra de la sala).
        private IEnumerator GuardiaLarga(JefeMartialHero j, float pausa = 8f)
        {
            j.PausaExtraPrueba = pausa;
            yield return Esperar(() => j.EstadoActual != Guardia && !j.Aturdido, 10f);
            Colocar(j, false);
            yield return Esperar(() => j.EstadoActual == Guardia && j.EnGuardia && j.ProximoAtaque > Time.time + 3f, 20f);
            yield return new WaitForSeconds(0.2f);
        }

        // Se cubre cuando no ataca: de frente le entra poco, por la espalda entero (y
        // se gira); todo golpe en guardia provoca su contraataque, un combo. Tras el
        // combo, en su recuperacion, tus golpes entran enteros y no contraataca.
        private IEnumerator GuardiaYContraataque(JefeMartialHero j)
        {
            AjustesMartialHero aj = j.Ajustes;
            yield return GuardiaLarga(j);
            Comprobar(j.EnGuardia && j.EstadoActual == Guardia, "cuando no ataca, se cubre");
            SpriteRenderer sr = j.GetComponentInChildren<SpriteRenderer>();
            var bloque = new MaterialPropertyBlock();
            sr.GetPropertyBlock(bloque);
            Color tinte = bloque.GetColor("_FlashColor");
            float cuanto = bloque.GetFloat("_FlashAmount");
            Comprobar(tinte == aj.colorGuardia && Mathf.Approximately(cuanto, aj.intensidadGuardia),
                      $"mientras se cubre se tine de celeste ({tinte}, {cuanto:0.00})");
            int ataques = j.Ataques.Count;
            float golpe = Time.time;
            Espadazo(j);
            int esperado = Mathf.Max(1, Mathf.RoundToInt(15 * aj.danoEnGuardia));
            Comprobar(ultimoAntes == 15 && ultimoDespues == esperado, $"en guardia, de frente le entra {ultimoDespues} de 15 (x{aj.danoEnGuardia})");
            float activa = -1f;
            Action<AjustesMartialHero.Golpe, float, float> alActivar = (g, a, h) => { if (activa < 0f) activa = Time.time; };
            j.AlActivarGolpe += alActivar;
            yield return Esperar(() => j.Ataques.Count > ataques, 2f);
            Comprobar(j.Ataques.Count > ataques && j.Ataques.Last().motivo == JefeMartialHero.Motivo.Contraataque,
                      $"al pegarle en guardia contraataca con un combo ({(j.Ataques.Count > ataques ? j.Ataques.Last().motivo.ToString() : "no")})");
            yield return Esperar(() => activa > 0f, 2f);
            j.AlActivarGolpe -= alActivar;
            float minimo = aj.retrasoContraataque + aj.reglas.avisoMinLigero;
            Comprobar(activa - golpe >= minimo - 0.02f && activa - golpe < minimo + 0.4f,
                      $"su primer golpe llega a los {activa - golpe:0.00} s de tu golpe (retraso + aviso minimo {minimo:0.00} s)");
            // Su recuperacion: quieto y sin guardia; entra entero y no contraataca.
            yield return Esperar(() => j.EstadoActual == JefeMartialHero.EstadoMartial.Recuperacion, 5f);
            ataques = j.Ataques.Count;
            Espadazo(j);
            Comprobar(ultimoDespues == 15 && !j.EnGuardia, $"recuperandose tras el combo no se cubre: entra entero ({ultimoDespues})");
            yield return Esperar(() => j.EstadoActual == Guardia, 3f);
            yield return new WaitForSeconds(0.3f);
            Comprobar(j.Ataques.Count == ataques, "pegarle en su recuperacion no provoca contraataque");

            // Por la espalda: entero y se gira (y contraataca).
            yield return GuardiaLarga(j);
            int miraba = j.Mirada;
            Colocar(j, true);
            yield return null;
            Espadazo(j);
            Comprobar(ultimoDespues == 15, $"en guardia, por la espalda entra entero ({ultimoDespues})");
            yield return new WaitForSeconds(aj.giroGuardia + 0.1f);
            Comprobar(j.Mirada != miraba, "por la espalda se gira hacia ti");
            yield return Esperar(() => j.EstadoActual == Guardia && j.EnGuardia, 8f);
            j.PausaExtraPrueba = 0f;
        }

        // Entras en su radio mientras se cubre: ataca enseguida con un combo.
        private IEnumerator Radio(JefeMartialHero j)
        {
            AjustesMartialHero aj = j.Ajustes;
            yield return GuardiaLarga(j);
            Colocar(j, false, aj.radioEntrada + 1.5f);
            yield return new WaitForSeconds(0.4f);
            int ataques = j.Ataques.Count;
            Comprobar(j.EstadoActual == Guardia, "fuera de su radio sigue cubierto");
            float entra = Time.time;
            Colocar(j, false, aj.radioEntrada - 0.6f);
            yield return Esperar(() => j.Ataques.Count > ataques, 1f);
            Comprobar(j.Ataques.Count > ataques && j.Ataques.Last().motivo == JefeMartialHero.Motivo.Radio && j.Ataques.Last().hora - entra < 0.1f,
                      $"al entrar en su radio ({aj.radioEntrada} u) ataca enseguida con un combo ({(j.Ataques.Count > ataques ? (j.Ataques.Last().hora - entra).ToString("0.00") + " s, " + j.Ataques.Last().motivo : "no")})");
            int hechos = j.CombosHechos.Count;
            yield return Esperar(() => j.GolpeEnCurso != null, 2f);
            Comprobar(Mathf.Abs(P.transform.position.x - j.transform.position.x) <= aj.distanciaAtaque + 0.3f,
                      $"antes de golpear se acerca hasta su distancia de ataque ({Mathf.Abs(P.transform.position.x - j.transform.position.x):0.00} u; ficha {aj.distanciaAtaque})");
            yield return Esperar(() => j.EstadoActual == Guardia, 8f);
            j.PausaExtraPrueba = 0f;
        }

        // Si te quedas cerca, al acabar su guardia (corta) ataca sin moverse; si
        // estas lejos, corre hacia ti y ataca al llegar.
        private IEnumerator Cerca(JefeMartialHero j)
        {
            AjustesMartialHero aj = j.Ajustes;
            yield return GuardiaLarga(j, 0f);
            yield return Esperar(() => j.EstadoActual == Guardia, 8f);
            int ataques = j.Ataques.Count;
            float desde = Time.time;
            yield return Esperar(() => j.Ataques.Count > ataques, aj.pausa.y + 1f);
            Comprobar(j.Ataques.Count > ataques && j.Ataques.Last().motivo == JefeMartialHero.Motivo.Acecho && Time.time - desde <= aj.pausa.y + 0.2f,
                      $"si te quedas cerca, ataca al acabar su guardia ({Time.time - desde:0.00} s; guardia {aj.pausa.x}-{aj.pausa.y} s)");
            yield return Esperar(() => j.EstadoActual == Guardia, 8f);
            Colocar(j, false, 7f);
            ataques = j.Ataques.Count;
            bool corrio = false;
            Action<JefeMartialHero.EstadoMartial> mira = e => { if (e == JefeMartialHero.EstadoMartial.Acecho) corrio = true; };
            j.AlCambiarEstadoMartial += mira;
            yield return Esperar(() => j.Ataques.Count > ataques, aj.pausa.y + aj.tiempoMaxCorrer + 1f);
            j.AlCambiarEstadoMartial -= mira;
            Comprobar(corrio && j.Ataques.Count > ataques && Mathf.Abs(P.transform.position.x - j.transform.position.x) <= aj.distanciaAtaque + 0.3f,
                      "si estas lejos, corre hacia ti y ataca al llegar");
        }

        // Un combo parado entero: lo deja aturdido (o lo intenta, si esta inmune).
        private IEnumerator TripleParado(JefeMartialHero j)
        {
            sala.PonerInvencible(false);
            sala.VidaInfinita = true;
            yield return Esperar(() => j.ProximoAtaque > Time.time && !j.Aturdido, 20f);
            bot = 1;
            float desde = j.HoraUltimoAtaque;
            sala.LanzarAtaque("combo_cuadruple");
            yield return Esperar(() => j.HoraUltimoAtaque > desde, 20f);
            yield return Esperar(() => j.GolpeEnCurso == null && j.EstadoActual != JefeMartialHero.EstadoMartial.Combo, 10f);
            bot = 0;
            SetCampo(P, "isBlocking", false);
            sala.VidaInfinita = false;
            sala.PonerInvencible(true);
            P.CurarCompleto();
        }

        // Aturdido por parry: mas dano. Al levantarse, inmune: un combo parado
        // enseguida no lo aturde (sale "Firme") ni el sagrado; luego vuelve a poder.
        private IEnumerator Inmunidad(JefeMartialHero j)
        {
            AjustesMartialHero aj = j.Ajustes;
            j.PausaExtraPrueba = 0f;
            yield return Esperar(() => !j.Inmune, aj.inmunidadTrasAturdido + 1f);
            int aturdidos = j.Aturdimientos.Count;
            yield return TripleParado(j);
            yield return Esperar(() => j.Aturdido, 2f);
            Comprobar(j.Aturdido && j.Aturdimientos.Count == aturdidos + 1, "combo parado: aturdido");
            Espadazo(j);
            int esperado = Mathf.RoundToInt(15 * aj.multDanoAturdido);
            Comprobar(ultimoAntes == 15 && ultimoDespues == esperado, $"aturdido recibe mas dano: {ultimoDespues} de 15 (x{aj.multDanoAturdido})");
            yield return Esperar(() => !j.Aturdido, aj.aturdidoCombo + 1f);
            yield return null;
            Comprobar(j.Inmune, $"al levantarse queda inmune ({aj.inmunidadTrasAturdido} s)");
            bool sagrado = j.PedirAturdimiento(0.6f);
            Comprobar(!sagrado && j.Rechazados.Contains("sagrado"), "inmune: el sagrado no lo aturde");
            aturdidos = j.Aturdimientos.Count;
            int rechazados = j.Rechazados.Count;
            // Un combo parado enseguida (en su guardia corta, aun inmune).
            j.PausaExtraPrueba = 0f;
            sala.PonerInvencible(false);
            sala.VidaInfinita = true;
            bot = 1;
            sala.LanzarAtaque("combo_cuadruple");
            int hechos = j.CombosHechos.Count;
            yield return Esperar(() => j.CombosHechos.Count > hechos, 6f);
            bool inmuneAlAcabar = j.Rechazados.Count > rechazados;
            yield return new WaitForSeconds(0.2f);
            bot = 0;
            SetCampo(P, "isBlocking", false);
            sala.VidaInfinita = false;
            sala.PonerInvencible(true);
            P.CurarCompleto();
            Comprobar(inmuneAlAcabar ? j.Aturdimientos.Count == aturdidos && j.Rechazados.Skip(rechazados).Contains("combo")
                                     : j.Aturdimientos.Count == aturdidos + 1,
                      inmuneAlAcabar ? $"inmune: el combo parado no lo aturde (sale \"{aj.textoInmune}\")"
                                     : $"el combo acabo pasados los {aj.inmunidadTrasAturdido} s de inmunidad: se aturde otra vez");
            yield return Esperar(() => !j.Inmune, aj.inmunidadTrasAturdido + 3f);
            Comprobar(!j.Inmune, "la inmunidad se acaba sola");
        }

        // El sagrado rompe la guardia: entra entero y lo aturde poco (sin el
        // multiplicador). Estando inmune la rompe igual, pero no lo aturde.
        private IEnumerator SagradoGuardia(JefeMartialHero j)
        {
            AjustesMartialHero aj = j.Ajustes;
            yield return GuardiaLarga(j);
            int rotas = j.GuardiasRotas, aturdidos = j.Aturdimientos.Count;
            EnemyHealth.ElementoDelGolpe = Elemento.Sagrado;
            Espadazo(j);
            int roto = ultimoDespues;
            yield return Esperar(() => j.Aturdido, 1f);
            Comprobar(j.GuardiasRotas > rotas && roto == 15, $"un golpe sagrado rompe la guardia y entra entero ({roto})");
            bool corto = j.Aturdimientos.Count == aturdidos + 1 && j.Aturdimientos.Last().tipo == "sagrado_guardia"
                         && Mathf.Approximately(j.Aturdimientos.Last().segundos, aj.aturdidoRomperGuardia);
            Espadazo(j);
            Comprobar(corto && ultimoDespues == 15, $"guardia rota: aturdido {aj.aturdidoRomperGuardia} s sin multiplicar el dano ({ultimoDespues})");
            yield return Esperar(() => !j.Aturdido, 2f);
            yield return null;
            Comprobar(!j.Inmune, "el aturdimiento del sagrado no da inmunidad");

            // Inmune: la rompe, pero no lo aturde.
            j.PausaExtraPrueba = 0f;
            yield return TripleParado(j);
            yield return Esperar(() => j.Inmune, aj.aturdidoCombo + 2f);
            j.PausaExtraPrueba = 8f;
            yield return Esperar(() => j.EstadoActual == Guardia && j.EnGuardia, 3f);
            rotas = j.GuardiasRotas;
            aturdidos = j.Aturdimientos.Count;
            bool inmune = j.Inmune;
            EnemyHealth.ElementoDelGolpe = Elemento.Sagrado;
            Espadazo(j);
            yield return new WaitForSeconds(0.3f);
            Comprobar(inmune && j.GuardiasRotas > rotas && j.Aturdimientos.Count == aturdidos && !j.Aturdido,
                      "inmune: el sagrado rompe la guardia sin aturdirlo");
            j.PausaExtraPrueba = 0f;
        }

        // Todos los golpes vistos avisaron en naranja (todos ligeros) hasta el instante del golpe.
        private void Avisos(JefeMartialHero j)
        {
            AjustesMartialHero aj = j.Ajustes;
            ReglasCombo r = aj.reglas;
            string[] todos = { aj.desenvaine.nombre, aj.tajo.nombre };
            Comprobar(avisosMal.Count == 0 && todos.All(golpesAvisados.Contains) && avisosPesados == 0,
                      $"cada golpe avisa en naranja ({r.parpadeoLigero}/s) hasta el instante del golpe: {avisosLigeros} golpes, {avisosPesados} rojos" +
                      (avisosMal.Count > 0 ? "; mal: " + string.Join(" | ", avisosMal.Take(4)) : "") +
                      (todos.All(golpesAvisados.Contains) ? "" : "; faltan: " + string.Join(", ", todos.Where(n => !golpesAvisados.Contains(n)))));
        }

        // Atacando solo, nunca repite la misma variante dos veces seguidas.
        private IEnumerator NoRepite(JefeMartialHero j)
        {
            int desde = j.CombosHechos.Count;
            yield return Esperar(() => j.CombosHechos.Count >= desde + 6, 40f);
            var l = j.CombosHechos.Skip(desde).ToList();
            bool bien = l.Count >= 6;
            if (j.Ajustes.variantes.Count > 1) for (int i = 1; i < l.Count; i++) if (l[i] == l[i - 1]) bien = false;
            Comprobar(bien, "atacando solo hace su combo una y otra vez (y, si hay varios, no repite el mismo seguido): " + string.Join(", ", l));
        }

        // Muere, se queda en el suelo con el cartel de derrota y no deja golpes activos.
        private IEnumerator Muerte(JefeMartialHero j)
        {
            bool derrotado = false;
            j.AlDerrotado += () => derrotado = true;
            EnemyHealth s = j.GetComponent<EnemyHealth>();
            s.TakeDamage(s.CurrentHealth * 10 + 100, (Vector2)j.transform.position + Vector2.left);
            yield return new WaitForSeconds(3f);
            Comprobar(derrotado && j != null && j.EstadoActual == JefeMartialHero.EstadoMartial.Muerte && j.GolpesActivos == 0,
                      "muere: aviso de derrota, se queda en el suelo y sin golpes activos");
        }

        // Una pelea nueva (Reaparecer) y espera a que entre y ataque.
        private IEnumerator Nueva()
        {
            sala.Reaparecer();
            yield return null;
            yield return Esperar(() => sala.Jefe != null, 3f);
            JefeMartialHero j = J;
            yield return Esperar(() => j == null || j.HoraUltimoAtaque >= 0f, 20f);
        }

        // Dificil (multiplicadores de la sala): el tope del combo se cumple con el
        // dano de Dificil (calculo con la regla del combo y pelea de verdad con el
        // jugador clavado en el sitio) y ningun golpe suelto mata con la vida llena.
        private IEnumerator Dificil()
        {
            sala.PonerDificil(true);
            yield return Nueva();
            JefeMartialHero j = J;
            AjustesMartialHero aj = j.Ajustes;
            EnemyHealth salud = j.GetComponent<EnemyHealth>();
            float mult = Desafio.MultDano;
            Comprobar(mult > 1f && salud.MaxHealth == Mathf.Max(1, Mathf.RoundToInt(aj.vida * Desafio.MultVida)),
                      $"Dificil desde la sala: dano x{mult}, vida {salud.MaxHealth} (x{Desafio.MultVida}), velocidad x{Desafio.MultVelocidad}");
            int vidaMax = P.VidaMaxima;
            float tope = aj.reglas.topeDanoCombo * vidaMax;

            // La regla del combo, golpe a golpe y sin parar nada (en una guardia larga).
            j.PausaExtraPrueba = 6f;
            yield return Esperar(() => j.EstadoActual == Guardia && j.ProximoAtaque > Time.time + 3f, 20f);
            foreach (AjustesMartialHero.Variante v in aj.variantes)
            {
                j.Ritmo.Empezar();
                int total = 0, sinTope = 0;
                foreach (AjustesMartialHero.TipoGolpe t in v.golpes)
                {
                    AjustesMartialHero.Golpe g = aj.DeTipo(t);
                    int real = Mathf.RoundToInt(j.Ritmo.DanoPermitido(g.dano, P) * mult);
                    j.Ritmo.Registrar(PlayerControler.ResultadoDano.Recibido, real, j.transform.position, 0f);
                    total += real;
                    sinTope += Mathf.RoundToInt(g.dano * mult);
                }
                Comprobar(total <= tope + 0.5f, $"Dificil, {v.id} sin parar nada: {sinTope} -> {total} (tope {tope:0} = {100f * aj.reglas.topeDanoCombo:0}% de {vidaMax})");
            }
            j.Ritmo.Empezar();
            j.Ritmo.Recuperado();
            j.PausaExtraPrueba = 0f;

            // Pelea de verdad: jugador clavado, sin bloquear.
            PlayerControler p = P;
            Rigidbody2D rb = p.GetComponent<Rigidbody2D>();
            RigidbodyConstraints2D antes = rb.constraints;
            sala.PonerInvencible(false);
            int peorGolpe = 0, peorTriple = 0, vidaGolpe = 0;
            string peorNombre = "";
            Action<AjustesMartialHero.Golpe, float, float> alActivar = (g, a, h) => vidaGolpe = P.VidaActual;
            Action<AjustesMartialHero.Golpe, PlayerControler.ResultadoDano?> alTerminar = (g, r) =>
            {
                int d = vidaGolpe - P.VidaActual;
                if (d > peorGolpe) { peorGolpe = d; peorNombre = g.nombre; }
            };
            j.AlActivarGolpe += alActivar;
            j.AlTerminarGolpe += alTerminar;
            bot = 3;
            foreach (AjustesMartialHero.Variante v in aj.variantes)
            {
                yield return Esperar(() => j.ProximoAtaque > Time.time, 20f);
                Colocar(j, false, 1.2f);
                rb.constraints = RigidbodyConstraints2D.FreezePositionX | RigidbodyConstraints2D.FreezeRotation;
                p.CurarCompleto();
                yield return UnAtaque(j, v.id);
                peorTriple = Mathf.Max(peorTriple, j.DanoUltimoAtaque);
                rb.constraints = antes;
            }
            bot = 0;
            j.AlActivarGolpe -= alActivar;
            j.AlTerminarGolpe -= alTerminar;
            sala.PonerInvencible(true);
            p.CurarCompleto();
            Comprobar(peorGolpe > 0 && peorGolpe < vidaMax, $"Dificil: el golpe que mas quita ({peorNombre}) quita {peorGolpe} de {vidaMax}: nunca mata con la vida llena");
            Comprobar(peorTriple <= tope + 0.5f, $"Dificil: el combo que mas quito en la pelea quito {peorTriple} (tope {tope:0}; tras un golpe recibido eres invulnerable 1 s)");
            sala.PonerDificil(false);
        }

        // Jugador automatico en 4 peleas libres de 60 s: parry a todo, solo bloqueo,
        // sin bloquear (le pega con el combo de la espada, 15, 15, 22, cuando el jefe
        // esta aturdido o recuperandose y cerca) y atacando sin parar (le pega siempre
        // que esta cerca, sin bloquear). Al jugador se le rellena la vida.
        private bool duelo, sinParar;
        private int perdidaJugador, perdidaJefe, danoAturdido, vidaVista, golpeEspada;
        private float siguienteEspada;
        private static readonly int[] Espada = { 15, 15, 22 };

        private IEnumerator Duelos()
        {
            var filas = new List<(int modo, int jugador, int jefe, int aturdidos, float aturdidosParaMatar)>();
            foreach (int modo in new[] { 1, 2, 3, 5 })
            {
                yield return Nueva();
                JefeMartialHero j = J;
                if (j == null) continue;
                AjustesMartialHero aj = j.Ajustes;
                sala.PonerInvencible(false);
                sala.VidaInfinita = false;
                P.CurarCompleto();
                // A 3 u del jefe, del lado del centro de la sala (nunca fuera de ella).
                PlayerControler pj = P;
                pj.transform.position = new Vector3(j.transform.position.x + (j.transform.position.x > 0f ? -3f : 3f), j.transform.position.y + 0.3f, 0f);
                Rigidbody2D rbj = pj.GetComponent<Rigidbody2D>();
                if (rbj != null) { rbj.position = pj.transform.position; rbj.linearVelocity = Vector2.zero; }
                perdidaJugador = perdidaJefe = danoAturdido = golpeEspada = 0;
                vidaVista = P.VidaActual;
                int aturdidos = 0, combos = j.CombosHechos.Count, ataques = j.Ataques.Count;
                j.AlCambiarEstadoMartial += e => { if (e == JefeMartialHero.EstadoMartial.Aturdido) aturdidos++; };
                bot = modo;
                sinParar = modo == 5;
                duelo = true;
                float inicio = Time.time;
                EnemyHealth salud = j.GetComponent<EnemyHealth>();
                while (Time.time - inicio < 60f && !salud.Muerto) yield return null;
                float duro = Time.time - inicio;
                duelo = false;
                bot = 0;
                SetCampo(P, "isBlocking", false);
                var motivos = j.Ataques.Skip(ataques).GroupBy(a => a.motivo).Select(g => $"{g.Key} {g.Count()}");
                float porAturdido = aturdidos > 0 ? danoAturdido / (float)aturdidos : 0f;
                float paraMatar = porAturdido > 0f ? aj.vida / porAturdido : -1f;
                string n = modo == 1 ? "parry a todo" : modo == 2 ? "solo bloqueo" : modo == 3 ? "sin bloquear" : "atacando sin parar";
                string fila = $"{n}: en {duro:0} s el jugador pierde {perdidaJugador} de vida ({perdidaJugador / (float)P.VidaMaxima:0.0} barras de {P.VidaMaxima}); " +
                              $"el jefe pierde {perdidaJefe} de {aj.vida}{(salud.Muerto ? " (MUERE)" : "")}; {j.CombosHechos.Count - combos} combos ({string.Join(", ", motivos)}), " +
                              $"{aturdidos} aturdimientos ({porAturdido:0} de dano en cada uno" +
                              (paraMatar > 0f ? $"; harian falta {paraMatar:0.0} aturdimientos para matarlo)" : "; sin aturdimientos no hay calculo)");
                filas.Add((modo, perdidaJugador, perdidaJefe, aturdidos, paraMatar));
                Debug.Log("[MARTIAL] DUELO " + fila);
            }
            sala.PonerInvencible(true);
            P.CurarCompleto();
            Comprobar(filas.Count == 4, "jugador automatico: 4 peleas libres (ver DUELO en el registro)");
            if (filas.Count < 4) yield break;
            var parry = filas[0];
            var sinBloquear = filas[2];
            Comprobar(parry.jugador < sinBloquear.jugador && parry.aturdidos > 0 && sinBloquear.aturdidos == 0,
                      $"parar sale a cuenta: con parry pierdes {parry.jugador} y lo aturdes {parry.aturdidos} veces; sin bloquear pierdes {sinBloquear.jugador} y 0 aturdimientos");
            Comprobar(parry.aturdidosParaMatar >= 2.5f && parry.aturdidosParaMatar <= 5.5f,
                      $"con parry hacen falta unos {parry.aturdidosParaMatar:0.0} aturdimientos para matarlo (objetivo: 3 o 4)");
        }

        // Cada fotograma del duelo: lo que pierde el jugador y sus espadazos al jefe.
        private void LateUpdate()
        {
            if (!duelo || sala == null) return;
            JefeMartialHero j = J;
            PlayerControler p = P;
            if (j == null || p == null) return;
            if (p.VidaActual < vidaVista) perdidaJugador += vidaVista - p.VidaActual;
            if (p.VidaActual < p.VidaMaxima * 0.5f) p.CurarCompleto();
            vidaVista = p.VidaActual;
            bool vendido = sinParar || j.Aturdido || j.EstadoActual == JefeMartialHero.EstadoMartial.Recuperacion;
            if (vendido && Mathf.Abs(j.transform.position.x - p.transform.position.x) < 2.5f && Time.time >= siguienteEspada)
            {
                EnemyHealth s = j.GetComponent<EnemyHealth>();
                if (s.Muerto) return;
                bool aturdido = j.Aturdido;
                EnemyHealth.ArmaDelGolpe = TipoArma.Espada;
                s.TakeDamage(Espada[golpeEspada % 3], p.transform.position, 0f);
                perdidaJefe += s.UltimoDano;
                if (aturdido) danoAturdido += s.UltimoDano;
                golpeEspada++;
                siguienteEspada = Time.time + (golpeEspada % 3 == 0 ? 0.7f : 0.3f);
            }
        }

        private IEnumerator Reapariciones()
        {
            bool bien = true;
            for (int i = 0; i < 10; i++)
            {
                sala.Reaparecer();
                yield return null;
                yield return null;
                yield return Esperar(() => sala.Jefe != null, 3f);
                yield return new WaitForSeconds(0.3f);
                int jefes = FindObjectsByType<JefeMartialHero>(FindObjectsSortMode.None).Length;
                int barras = FindObjectsByType<BarraJefe>(FindObjectsSortMode.None).Length;
                int reservas = FindObjectsByType<ReservaChispas>(FindObjectsSortMode.None).Length;
                if (jefes != 1 || barras > 1 || reservas > 1) { bien = false; Fallo($"reaparicion {i + 1}: {jefes} jefes, {barras} barras, {reservas} reservas de chispas"); }
            }
            Comprobar(bien, "10 reapariciones seguidas: un solo jefe, una barra y una reserva de chispas");
            sala.Limpiar();
            yield return null;
            yield return null;
            Comprobar(FindObjectsByType<JefeMartialHero>(FindObjectsSortMode.None).Length == 0 && FindObjectsByType<BarraJefe>(FindObjectsSortMode.None).Length == 0
                      && GameObject.Find("Chispas") == null, "limpiar no deja jefe, barra ni chispas");
        }
    }
}
