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

// Prueba del Impaler en la sala de pruebas (batchmode: -executeMethod PruebaImpaler.Todo).
//   1. Sin jugar: huellas del progreso real, Warrior > Impaler > Crear o actualizar,
//      sprites (importacion, tamano y anclaje de cada cuadro) y ficha (avisos,
//      dano, pausa entre golpes, afinidades). No esta en Resources, Build
//      Settings, Bestiario ni Desafios.
//   2. En la sala: se invoca, entra, ataca solo, cada ataque suelto y en bucle
//      (aviso real >= minimo), un golpe de verdad al jugador, fase 2 con la barra
//      llena, muerte con la flor y 10 reapariciones sin objetos de mas.
//   3. Al salir: el progreso real es identico.
// Escribe "[IMPALER] OK ..." / "[IMPALER] FALLO ..." y el total.
[InitializeOnLoad]
public static class PruebaImpaler
{
    private const string Pre = "PruebaImpaler.";

    private static string Fase { get => SessionState.GetString(Pre + "Fase", ""); set => SessionState.SetString(Pre + "Fase", value); }

    static PruebaImpaler()
    {
        EditorApplication.playModeStateChanged += e =>
        {
            if (Fase != "jugar") return;
            if (e == PlayModeStateChange.EnteredPlayMode) new GameObject("PruebaImpaler").AddComponent<Conductor>();
            else if (e == PlayModeStateChange.EnteredEditMode)
            {
                Fase = "salida";
                SessionState.SetFloat(Pre + "Desde", (float)EditorApplication.timeSinceStartup);
                EditorApplication.update += Continuar;
            }
        };
        if (Fase == "salida") EditorApplication.update += Continuar;
    }

    private static void Ok(string s) { SessionState.SetInt(Pre + "Ok", SessionState.GetInt(Pre + "Ok", 0) + 1); Debug.Log("[IMPALER] OK " + s); }
    private static void Fallo(string s) { SessionState.SetInt(Pre + "Fallos", SessionState.GetInt(Pre + "Fallos", 0) + 1); Debug.LogWarning("[IMPALER] FALLO " + s); }
    private static void Comprobar(bool c, string s) { if (c) Ok(s); else Fallo(s); }

    // ------------------------------------------------------------------ Sin jugar

    public static void Todo()
    {
        SessionState.SetInt(Pre + "Ok", 0);
        SessionState.SetInt(Pre + "Fallos", 0);
        SessionState.SetString(Pre + "Antes", Instantanea());

        CrearImpaler.Crear();
        Comprobar(CrearImpaler.Comprobar(out string informe), "sprites: " + informe);
        ComprobarCuadros();
        ComprobarFicha();
        ComprobarAislado();

        Fase = "jugar";
        EditorSceneManager.OpenScene(SalaPruebas.Ruta, OpenSceneMode.Single);
        ArranquePruebas.Proteger();
        EditorApplication.EnterPlaymode();
    }

    // Cada animacion: todos sus cuadros del mismo tamano, a la misma escala y con
    // los pies en el mismo sitio del lienzo original (x = 153 + desplazamiento
    // dibujado, 9 px sobre el borde).
    private static void ComprobarCuadros()
    {
        CrearImpaler.Cuadros c = CrearImpaler.LeerCuadros();
        Comprobar(c != null && c.animaciones.Count == 10, "cuadros.json con 10 animaciones");
        if (c == null) return;
        Comprobar(Mathf.Approximately(c.pixelesPorUnidad, 50f) && c.msPorCuadro == 100, "50 px por unidad y 100 ms por cuadro");
        foreach (CrearImpaler.Animacion a in c.animaciones)
        {
            var tam = new HashSet<Vector2Int>();
            float peorX = 0f, peorY = 0f;
            for (int i = 0; i < a.cuadros.Length; i++)
            {
                Texture2D t = AssetDatabase.LoadAssetAtPath<Texture2D>($"{CrearImpaler.CarpetaSprites}/{a.nombre}/{a.cuadros[i]}");
                if (t == null) { tam.Add(new Vector2Int(-1, -1)); continue; }
                tam.Add(new Vector2Int(t.width, t.height));
                float x = a.recorte[0] + a.pivotesX[i] * a.tamano[0];
                float y = (265 - a.recorte[3]) + a.pivotesY[i] * a.tamano[1];
                peorX = Mathf.Max(peorX, Mathf.Abs(x - (153 + a.dx[i])));
                peorY = Mathf.Max(peorY, Mathf.Abs(y - 9f));
            }
            Comprobar(tam.Count == 1 && tam.First() == new Vector2Int(a.tamano[0], a.tamano[1]),
                      $"{a.nombre}: {a.cuadros.Length} cuadros del mismo tamano ({a.tamano[0]}x{a.tamano[1]})");
            Comprobar(peorX < 0.05f && peorY < 0.05f, $"{a.nombre}: mismo anclaje en todos los cuadros (error {peorX:0.000} / {peorY:0.000} px)");
        }
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CrearImpaler.RutaPrefab);
        AnimadorHoja an = prefab != null ? prefab.GetComponentInChildren<AnimadorHoja>() : null;
        string[] clips = { "quieto", "andar", "muerte", "renacer", "attack1", "attack2", "attack3", "attack4", "attack5", "attack6", "counter" };
        Comprobar(an != null && clips.All(n => an.clips.Any(k => k.nombre == n && k.fotogramas.All(f => f != null))),
                  "el prefab tiene sus clips: " + string.Join(", ", clips));
    }

    // Avisos, dano y pausas de cada golpe de la ficha.
    private static void ComprobarFicha()
    {
        AjustesImpaler aj = AssetDatabase.LoadAssetAtPath<AjustesImpaler>(CrearImpaler.RutaFicha);
        Comprobar(aj != null, "ficha " + CrearImpaler.RutaFicha);
        if (aj == null) return;
        CrearImpaler.Cuadros c = CrearImpaler.LeerCuadros();
        // La vida mas baja posible (personaje sin niveles): el peor caso.
        int vidaMax = AjustesProgreso.Get().vidaBase;
        float multDificil = Mathf.Max(1f, SalaPruebas.Cargar<AjustesSalaPruebas>("Ajustes Sala Pruebas").multDano);
        foreach (AjustesImpaler.Ataque a in aj.ataques)
        {
            CrearImpaler.Animacion anim = c.animaciones.FirstOrDefault(x => x.nombre == a.clip);
            Comprobar(anim != null, $"{a.id}: su clip {a.clip} existe");
            if (anim == null) continue;
            for (int i = 0; i < a.golpes.Count; i++)
            {
                AjustesImpaler.Golpe g = a.golpes[i];
                bool final = i == a.golpes.Count - 1 && a.golpes.Count > 1;
                string n = $"{a.id}/{g.nombre}: ";
                Comprobar(g.senal < g.activoDesde && g.activoDesde <= g.activoHasta && g.activoHasta < anim.cuadros.Length, n + "cuadros de senal y golpe dentro del clip");
                float dibujado = (g.activoDesde - g.senal) / 10f;
                float minimo = aj.AvisoMinimo(g, final);
                // Solo se sostiene un cuadro repetido del dibujo, la pose de salida
                // (el primero del clip) o la pausa pedida a proposito (pausaVariable).
                bool enRango = g.cuadroPausa >= g.senal && g.cuadroPausa < g.activoDesde;
                bool repetido = g.cuadroPausa > 0 && anim.cuadros[g.cuadroPausa] == anim.cuadros[g.cuadroPausa - 1];
                bool sostiene = enRango && (repetido || g.cuadroPausa == 0 || g.pausaVariable.y > 0f);
                Comprobar(!enRango || sostiene, n + $"el cuadro que sostiene ({g.cuadroPausa}) es repetido, la pose de salida o una pausa pedida");
                Comprobar(dibujado >= Mathf.Max(minimo, g.aviso) - 0.001f || sostiene,
                          n + $"aviso {Mathf.Max(minimo, g.aviso):0.00} s posible (dibujado {dibujado:0.00} s{(sostiene ? ", sostiene el cuadro " + g.cuadroPausa : "")})");
                int danoDificil = Mathf.RoundToInt(g.dano * multDificil);
                Comprobar(danoDificil < vidaMax, n + $"no mata con la vida llena ({g.dano}, en Dificil {danoDificil} de {vidaMax})");
                Comprobar(g.cajaTamano.x > 0f && g.cajaTamano.y > 0f, n + $"caja propia {g.cajaTamano}");
                if (i > 0)
                {
                    float hueco = (g.activoDesde - (a.golpes[i - 1].activoHasta + 1)) / 10f;
                    Comprobar(g.senal > a.golpes[i - 1].activoHasta, n + "su senal empieza despues del golpe anterior (nunca dos a la vez)");
                    Comprobar(hueco >= aj.pausaMinEntreGolpes - 0.001f || sostiene,
                              n + $"hueco con el golpe anterior: dibujado {hueco:0.00} s{(sostiene ? " + cuadro sostenido" : "")} (minimo {aj.pausaMinEntreGolpes:0.00} s)");
                }
            }
        }
        // Combos: sus pasos existen y la suma de dano si no paras nada (Normal y
        // Dificil). Si pasa del tope, el jefe recorta los ultimos golpes en la pelea.
        string[] combos = { "apertura", "cadena", "persecucion", "pinza", "tormenta" };
        Comprobar(combos.All(id => aj.BuscarCombo(id) != null), "la ficha tiene los 5 combos: " + string.Join(", ", combos));
        foreach (AjustesImpaler.Combo co in aj.combos)
        {
            bool validos = co.eslabones.All(e => aj.Buscar(e.ataque) != null);
            int golpes = 0, suma = 0;
            if (validos)
                foreach (AjustesImpaler.Eslabon e in co.eslabones)
                {
                    AjustesImpaler.Ataque at = aj.Buscar(e.ataque);
                    int n = e.hastaGolpe < 0 ? at.golpes.Count : Mathf.Min(e.hastaGolpe + 1, at.golpes.Count);
                    golpes += n;
                    suma += at.golpes.Take(n).Sum(g => g.dano);
                }
            int dificil = Mathf.RoundToInt(suma * multDificil);
            float tope = aj.topeDanoCombo * vidaMax;
            Comprobar(validos && co.eslabones.Count >= 2, $"combo {co.id}{(co.soloFase2 ? " (fase 2)" : "")}: {co.eslabones.Count} ataques, {golpes} golpes, " +
                      $"suma sin parar {suma} ({100f * suma / vidaMax:0}%) y en Dificil {dificil} ({100f * dificil / vidaMax:0}%); tope {tope:0}" +
                      (dificil > tope ? " -> en la pelea se recorta al tope" : ""));
        }

        string[] todos = { "lanzazo_largo", "tajo_pausa", "combo_relampago", "embestida_medialuna", "salto_estela", "cruce_sangre", "contraataque" };
        Comprobar(todos.All(id => aj.Buscar(id) != null), "la ficha tiene los 7 ataques");
        // El blanco es solo del contraataque.
        bool blanco = aj.ataques.All(a => a.golpes.All(g => (g.peso == AjustesImpaler.Peso.Contraataque) == a.contraataque))
                      && aj.ataques.Count(a => a.contraataque) == 1 && aj.colorContraataque != aj.colorLigero && aj.colorContraataque != aj.colorPesado;
        Comprobar(blanco, "el aviso blanco es solo del contraataque");
        Comprobar(aj.combos.All(c => c.eslabones.All(e => aj.Buscar(e.ataque) == null || !aj.Buscar(e.ataque).contraataque)), "el contraataque no va dentro de ningun combo");
        Comprobar(aj.pausaMinEntreGolpes >= 0.5f, $"pausa minima entre golpes {aj.pausaMinEntreGolpes:0.00} s (el jugador machaca si pulsa antes de 0,5 s)");
        Comprobar(Mathf.Approximately(aj.Afinidad(Elemento.Fuego), aj.fuego) && aj.fuego < 1f && aj.hielo < 1f
                  && Mathf.Approximately(aj.Afinidad(Elemento.Sagrado), 1f) && Mathf.Approximately(aj.Afinidad(Elemento.Sangrado), 1f)
                  && aj.oscuridad > 1f && Mathf.Approximately(aj.Afinidad(Elemento.Ninguno), 1f),
                  $"afinidades: fuego {aj.fuego}, hielo {aj.hielo}, sagrado {aj.sagrado}, sangrado {aj.sangrado}, oscuridad {aj.oscuridad}");
    }

    // Solo en la sala: nada en Resources, Build Settings, Bestiario ni Desafios.
    private static void ComprobarAislado()
    {
        string guid = AssetDatabase.AssetPathToGUID(CrearImpaler.RutaPrefab);
        bool enResources = AssetDatabase.FindAssets("", new[] { "Assets/Resources" })
            .Select(AssetDatabase.GUIDToAssetPath).Where(p => p.EndsWith(".asset") || p.EndsWith(".prefab"))
            .Any(p => File.ReadAllText(p).Contains(guid));
        Comprobar(!enResources, "nada de Resources apunta al Impaler");
        bool enEscenas = EditorBuildSettings.scenes.Any(s => File.Exists(s.path) && File.ReadAllText(s.path).Contains(guid));
        Comprobar(!enEscenas, "no esta en ninguna escena de Build Settings");
        Comprobar(FichaBestiario.Get().entradas.All(e => e.enemigo == null || e.enemigo.name != "JefeImpaler") && FichaJefe.Todas().All(f => f.id != CrearImpaler.NombreCatalogo),
                  "no esta en el Bestiario ni en los Desafios");
    }

    // ------------------------------------------------------------------ Al salir

    private static void Continuar()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Pre + "Desde", 0f) < 3.0) return;
        EditorApplication.update -= Continuar;
        Comprobar(Instantanea() == SessionState.GetString(Pre + "Antes", ""), "el progreso real es identico (partidas, globales.json y PlayerPrefs)");
        int ok = SessionState.GetInt(Pre + "Ok", 0), fallos = SessionState.GetInt(Pre + "Fallos", 0);
        Debug.Log($"[IMPALER] RESULTADO: {ok} OK, {fallos} FALLOS");
        Fase = "";
        if (Application.isBatchMode) EditorApplication.Exit(fallos > 0 ? 1 : 0);
    }

    // Huellas del progreso real (como PruebaSalaPruebas): archivos de las dos
    // carpetas (real y perfil de pruebas) y las claves registradas de PlayerPrefs.
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
        private readonly List<(AjustesImpaler.Golpe, float)> golpes = new List<(AjustesImpaler.Golpe, float)>();
        private SalaPruebas sala;

        // Jugador automatico: 0 = no toca al jugador, 1 = parry a todo, 2 = solo bloqueo, 3 = sin bloquear.
        private int bot;
        // Lo que se mide mientras ataca: donde esta, golpes activos a la vez y hueco entre golpes.
        private bool midiendo;
        private float minX = float.MaxValue, maxX = float.MinValue, peorHueco = 99f, finVisto = -1f, horaVista = -2f;
        private int maxActivos, dentroTerreno;
        // Retraso aprendido tras cada secuencia del jefe (-1 = no se mide).
        private readonly List<float> retrasos = new List<float>();
        private int secuenciasVistas = -1;
        // Cada golpe (secuencia, cuando se activa, cuando acaba), el tono de cada
        // parry (-1 = golpe sin parry) y cada aviso (peso, color, ritmo).
        private readonly List<(float hora, float act, float fin)> registro = new List<(float, float, float)>();
        private readonly List<float> tonos = new List<float>();
        private readonly List<(AjustesImpaler.Peso peso, Color color, float hz)> avisos = new List<(AjustesImpaler.Peso, Color, float)>();
        private bool sinRellenarEstamina;

        private void Update()
        {
            JefeImpaler j = sala != null ? J : null;
            if (j == null) return;
            if (midiendo)
            {
                minX = Mathf.Min(minX, j.transform.position.x);
                maxX = Mathf.Max(maxX, j.transform.position.x);
                maxActivos = Mathf.Max(maxActivos, j.GolpesActivos);
                if (!j.Volando && j.DentroDelTerreno) dentroTerreno++;
            }
            if (secuenciasVistas >= 0 && j.SecuenciasHechas.Count != secuenciasVistas)
            {
                secuenciasVistas = j.SecuenciasHechas.Count;
                retrasos.Add(j.RetrasoAprendido);
            }
            PlayerControler p = P;
            if (p == null || bot == 0) return;
            if (bot == 3) { SetCampo(p, "isBlocking", false); return; }
            // Mira al que golpea: el jefe o, en el Cruce, el eco desde su lado.
            float x = j.transform.position.x;
            AjustesImpaler.Golpe g = j.GolpeEnCurso;
            if (g != null && !Mathf.Approximately(g.origenX, 0f)) x += j.Mirada * g.origenX;
            if (Mathf.Abs(x - p.transform.position.x) > 0.05f) Llamar(p, "SetFacing", x > p.transform.position.x ? 1 : -1);
            SetCampo(p, "isBlocking", true);
            SetCampo(p, "tBloqueo", bot == 1 ? 0f : 1f);
            SetCampo(p, "ventanaParryActual", Campo(p, "parryWindow"));
            GatherInput gi = p.GetComponent<GatherInput>();
            if (gi != null) SetCampo(gi, "_isKickHeld", bot == 2);
            PlayerStamina st = p.GetComponent<PlayerStamina>();
            if (st != null && !sinRellenarEstamina) st.Recuperar(100f);
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
            if ((t == LogType.Error || t == LogType.Exception) && !c.StartsWith("[IMPALER]")) errores.Add(c);
            if (t == LogType.Warning && c.StartsWith("[Impaler]")) errores.Add(c);
        }

        private IEnumerator Start()
        {
            float limite = Time.realtimeSinceStartup + 20f;
            while ((SalaPruebas.Actual == null || FindFirstObjectByType<PlayerControler>() == null) && Time.realtimeSinceStartup < limite) yield return null;
            yield return new WaitForSecondsRealtime(1f);
            sala = SalaPruebas.Actual;
            if (sala == null) { Fallo("la sala no arranca"); Salir(); yield break; }
            yield return Seguro(Pruebas());
            Comprobar(errores.Count == 0, "sin errores ni avisos del Impaler en la consola " + (errores.Count > 0 ? "(" + string.Join(" | ", errores.Distinct().Take(5)) + ")" : ""));
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
        private JefeImpaler J => sala.Jefe as JefeImpaler;
        private CatalogoPruebas.Entrada Entrada => sala.Catalogo.entradas.FirstOrDefault(e => e.nombre == CrearImpaler.NombreCatalogo);

        private IEnumerator Pruebas()
        {
            Comprobar(Entrada != null && Entrada.prefab != null, "el Impaler esta en el catalogo de la sala");
            if (Entrada == null) yield break;
            sala.PonerInvencible(true);
            sala.Invocar(Entrada);
            yield return Esperar(() => J != null, 3f);
            JefeImpaler j = J;
            Comprobar(j != null, "se invoca con su arena");
            if (j == null) yield break;
            j.AlActivarGolpe += (g, aviso) =>
            {
                golpes.Add((g, aviso));
                // Hueco con el golpe anterior del mismo ataque.
                if (!Mathf.Approximately(j.HoraUltimoAtaque, horaVista)) { horaVista = j.HoraUltimoAtaque; finVisto = -1f; }
                else if (finVisto > 0f && midiendo) peorHueco = Mathf.Min(peorHueco, Time.time - finVisto);
            };
            j.AlActivarGolpe += (g, aviso) => registro.Add((j.HoraUltimoAtaque, Time.time, -1f));
            j.AlTerminarGolpe += (g, r) =>
            {
                finVisto = Time.time;
                if (registro.Count > 0) { var u = registro[registro.Count - 1]; registro[registro.Count - 1] = (u.hora, u.act, Time.time); }
                if (r == PlayerControler.ResultadoDano.Parry) tonos.Add(PlayerControler.UltimoTonoParry);
                if (r == PlayerControler.ResultadoDano.Recibido || r == PlayerControler.ResultadoDano.Bloqueado) tonos.Add(-1f);
            };
            j.AlAvisar += (g, c, hz) => avisos.Add((g.peso, c, hz));

            yield return Esperar(() => j == null || j.HoraUltimoAtaque >= 0f, 20f);
            Comprobar(j != null && j.HoraUltimoAtaque >= 0f, "entra en la arena y ataca solo (" + (j != null ? j.UltimoAtaque : "-") + ")");
            Comprobar(FindFirstObjectByType<BarraJefe>() != null, "tiene su barra de jefe");
            Comprobar(j.AtaquesPrueba.Contains("lanzazo_largo"), "la sala lista sus ataques: " + string.Join(", ", j.AtaquesPrueba));

            yield return Ataques(j);
            yield return ConTerreno(j);
            yield return ConBot(j);
            yield return Combos(j);
            yield return Contraataque(j);
            yield return GolpeDeVerdad(j);
            yield return Fases(j);
            yield return Adaptacion();
            yield return Dificil();
            yield return Duelos();
            yield return Reapariciones();
        }

        // Jugador automatico en una pelea libre de 75 s por manera: parry a todo,
        // solo bloqueo y sin bloquear. Le pega con el combo de la espada (15, 15, 22)
        // cuando el jefe esta vendido (aturdido, en pausa o recuperandose) y cerca.
        // Al jugador se le rellena la vida (no muere) y se suma lo que pierde.
        private bool duelo;
        private int perdidaJugador, perdidaJefe, vidaVista, golpeEspada;
        private float siguienteEspada;
        private static readonly int[] Espada = { 15, 15, 22 };

        private IEnumerator Duelos()
        {
            var filas = new List<string>();
            foreach (int modo in new[] { 1, 2, 3 })
            {
                yield return Nueva();
                JefeImpaler j = J;
                sala.PonerInvencible(false);
                sala.VidaInfinita = false;
                P.CurarCompleto();
                Colocar(j.transform.position.x + (j.transform.position.x > 0f ? -3f : 3f));
                perdidaJugador = perdidaJefe = golpeEspada = 0;
                vidaVista = P.VidaActual;
                int aturdidos = j.Aturdimientos.Count, secuencias = j.SecuenciasHechas.Count;
                bot = modo;
                duelo = true;
                yield return new WaitForSeconds(75f);
                duelo = false;
                bot = 0;
                SetCampo(P, "isBlocking", false);
                string n = modo == 1 ? "parry a todo" : modo == 2 ? "solo bloqueo" : "sin bloquear";
                string fila = $"{n}: el jugador pierde {perdidaJugador} de vida ({perdidaJugador / (float)P.VidaMaxima:0.0} barras de {P.VidaMaxima}); " +
                              $"el Impaler pierde {perdidaJefe} (de {j.Ajustes.vidaFase1} + {j.Ajustes.vidaFase2}); " +
                              $"{j.SecuenciasHechas.Count - secuencias} ataques o combos; {j.Aturdimientos.Count - aturdidos} aturdimientos";
                filas.Add(fila);
                duelos.Add((perdidaJugador, perdidaJefe));
                Debug.Log("[IMPALER] DUELO " + fila);
            }
            sala.PonerInvencible(true);
            P.CurarCompleto();
            // Parar debe salir a cuenta: con parry pierdes menos y el jefe pierde mas (se aturde).
            Comprobar(filas.Count == 3, "jugador automatico: 3 peleas libres de 75 s (ver DUELO en el registro)");
            Comprobar(duelos[0].jugador < duelos[2].jugador && duelos[0].jefe > duelos[2].jefe,
                      $"parar sale a cuenta: con parry pierdes {duelos[0].jugador} y el Impaler {duelos[0].jefe}; sin bloquear pierdes {duelos[2].jugador} y el Impaler {duelos[2].jefe}");
            duelos.Clear();
        }
        private readonly List<(int jugador, int jefe)> duelos = new List<(int, int)>();

        // Cada fotograma del duelo: lo que pierde el jugador y los espadazos al jefe vendido.
        private void LateUpdate()
        {
            if (!duelo || sala == null) return;
            JefeImpaler j = J;
            PlayerControler p = P;
            if (j == null || p == null) return;
            if (p.VidaActual < vidaVista) perdidaJugador += vidaVista - p.VidaActual;
            if (p.VidaActual < p.VidaMaxima * 0.5f) p.CurarCompleto();
            vidaVista = p.VidaActual;
            EstadoEnemigo e = j.Estado;
            bool vendido = (e == EstadoEnemigo.Golpeado || e == EstadoEnemigo.Alerta || e == EstadoEnemigo.Recuperacion) && !j.EnTregua && !j.Transformando && !j.EnGuardia;
            if (vendido && Mathf.Abs(j.transform.position.x - p.transform.position.x) < 2.5f && Time.time >= siguienteEspada)
            {
                EnemyHealth s = sala.Salud;
                EnemyHealth.ArmaDelGolpe = TipoArma.Espada;
                s.TakeDamage(Espada[golpeEspada % 3], p.transform.position, 0f);
                perdidaJefe += s.UltimoDano;
                golpeEspada++;
                siguienteEspada = Time.time + (golpeEspada % 3 == 0 ? 0.7f : 0.35f);
            }
        }

        // Una pelea nueva (Reaparecer) y espera a que entre y ataque.
        private IEnumerator Nueva()
        {
            sala.Reaparecer();
            yield return Esperar(() => sala.Jefe != null, 3f);
            JefeImpaler j = J;
            yield return Esperar(() => j == null || j.HoraUltimoAtaque >= 0f, 20f);
            if (j == null) yield break;
            j.AlActivarGolpe += (g, aviso) => golpes.Add((g, aviso));
            j.AlActivarGolpe += (g, aviso) => registro.Add((j.HoraUltimoAtaque, Time.time, -1f));
            j.AlTerminarGolpe += (g, r) => { if (registro.Count > 0) { var u = registro[registro.Count - 1]; registro[registro.Count - 1] = (u.hora, u.act, Time.time); } };
        }

        // Adaptacion: gradual, con tope, que nunca baja del aviso minimo, y apagada se queda fija.
        private IEnumerator Adaptacion()
        {
            yield return Nueva();
            JefeImpaler j = J;
            AjustesImpaler aj = j.Ajustes;
            Comprobar(Mathf.Approximately(j.RetrasoAprendido, 0f) && Mathf.Approximately(j.PesoContraataque, 1f),
                      "adaptacion: cada pelea empieza de cero (retraso 0, contraataque x1)");

            // Mucho parry: el retraso sube poco a poco hasta su tope.
            sala.PonerInvencible(false);
            sala.VidaInfinita = true;
            golpes.Clear();
            // El valor tras cada ataque o combo del jefe (los forzados y los suyos).
            retrasos.Clear();
            retrasos.Add(j.RetrasoAprendido);
            secuenciasVistas = j.SecuenciasHechas.Count;
            float maxExtra = 0f;
            bot = 1;
            for (int i = 0; i < 9; i++)
            {
                yield return CercaDeSinInvencible(j);
                yield return UnAtaque(j, "lanzazo_largo");
                maxExtra = Mathf.Max(maxExtra, j.UltimoRetrasoExtra);
            }
            secuenciasVistas = -1;
            bool gradual = true;
            for (int i = 1; i < retrasos.Count; i++) if (Mathf.Abs(retrasos[i] - retrasos[i - 1]) > aj.pasoRetraso + 0.001f) gradual = false;
            Comprobar(retrasos.Last() > 0f && retrasos.Max() <= aj.topeRetraso + 0.001f && gradual,
                      $"adaptacion: con muchos parrys el retraso sube poco a poco hasta el tope ({string.Join(" ", retrasos.Select(r => r.ToString("0.00")))}; tope {aj.topeRetraso})");
            Comprobar(golpes.All(x => x.Item2 >= aj.AvisoMinimo(x.Item1, false) - 0.02f) && maxExtra > 0f,
                      $"adaptacion: el retraso extra (hasta {maxExtra:0.00} s) nunca baja el aviso minimo ({golpes.Count} golpes)");
            // Deja de parar: baja poco a poco.
            float alto = j.RetrasoAprendido;
            bot = 3;
            for (int i = 0; i < 4; i++) { yield return CercaDeSinInvencible(j); yield return UnAtaque(j, "lanzazo_largo"); }
            Comprobar(j.RetrasoAprendido < alto && alto - j.RetrasoAprendido <= 4 * aj.pasoRetraso + 0.001f,
                      $"adaptacion: si dejas de parar, el retraso baja poco a poco ({alto:0.00} -> {j.RetrasoAprendido:0.00})");

            // Le pegas en la guardia: el contraataque sale mas a menudo, con tope; si paras, baja.
            bot = 0;
            SetCampo(P, "isBlocking", false);
            sala.PonerInvencible(true);
            var pesos = new List<float> { j.PesoContraataque };
            for (int i = 0; i < 7; i++)
            {
                yield return GuardiaCon(j, aj.umbralGolpesGuardia + 1);
                pesos.Add(j.PesoContraataque);
            }
            Comprobar(pesos.Last() > 1f && pesos.Max() <= aj.topePesoContraataque + 0.001f
                      && pesos.Zip(pesos.Skip(1), (a, b) => Mathf.Abs(b - a)).All(d => d <= aj.pasoPesoContraataque + 0.001f),
                      $"adaptacion: pegandole en la guardia, el contraataque sube poco a poco hasta el tope (x{string.Join(" x", pesos.Select(p => p.ToString("0.0")))})");
            float pico = j.PesoContraataque;
            for (int i = 0; i < 2; i++) yield return GuardiaCon(j, 0);
            Comprobar(j.PesoContraataque < pico, $"adaptacion: si dejas de pegarle en la guardia, vuelve a bajar (x{pico:0.0} -> x{j.PesoContraataque:0.0})");

            // Apagada: se queda fija.
            adaptacionGuardada = aj.adaptacionActiva ? 1 : 0;
            aj.adaptacionActiva = false;
            yield return Nueva();
            j = J;
            bot = 1;
            sala.PonerInvencible(false);
            for (int i = 0; i < 4; i++) { yield return CercaDeSinInvencible(j); yield return UnAtaque(j, "lanzazo_largo"); }
            bot = 0;
            SetCampo(P, "isBlocking", false);
            sala.PonerInvencible(true);
            yield return GuardiaCon(j, aj.umbralGolpesGuardia + 1);
            Comprobar(Mathf.Approximately(j.RetrasoAprendido, 0f) && Mathf.Approximately(j.PesoContraataque, 1f),
                      $"adaptacion apagada en la ficha: se queda fija (retraso {j.RetrasoAprendido}, contraataque x{j.PesoContraataque})");
            aj.adaptacionActiva = adaptacionGuardada == 1;
            adaptacionGuardada = -1;
            sala.VidaInfinita = false;
            P.CurarCompleto();
        }

        // Una guardia del contraataque en la que le pegas "golpes" veces.
        private IEnumerator GuardiaCon(JefeImpaler j, int golpesGuardia)
        {
            yield return Esperar(() => j.ProximoAtaque > Time.time, 20f);
            sala.LanzarAtaque("contraataque");
            yield return Esperar(() => j.EnGuardia, 20f);
            for (int k = 0; k < golpesGuardia && j.EnGuardia; k++)
            {
                sala.Salud.TakeDamage(5, P.transform.position, 0f);
                yield return new WaitForSeconds(0.1f);
            }
            yield return Esperar(() => !j.EnGuardia, 5f);
            yield return Esperar(() => j.ProximoAtaque > Time.time, 10f);
        }

        // Dificil: los combos con golpe extra (mas golpes al final) cumplen las reglas:
        // siguen tras el parry, como mucho 3 seguidos, aviso minimo y tope de dano.
        private IEnumerator Dificil()
        {
            sala.PonerDificil(true);
            yield return Nueva();
            JefeImpaler j = J;
            AjustesImpaler aj = j.Ajustes;
            Comprobar(JefeImpaler.EsDificil && Desafio.MultDano > 1f, $"Dificil desde la sala (dano x{Desafio.MultDano}, vida x{Desafio.MultVida}, velocidad x{Desafio.MultVelocidad})");
            sala.PonerInvencible(false);
            sala.VidaInfinita = false;
            registro.Clear();
            golpes.Clear();
            foreach (AjustesImpaler.Combo co in aj.combos.Where(c => c.extraDificil != null && c.extraDificil.Count > 0))
            {
                int esperados = co.eslabones.Concat(co.extraDificil).Sum(e =>
                {
                    AjustesImpaler.Ataque at = aj.Buscar(e.ataque);
                    int n = e.hastaGolpe < 0 ? at.golpes.Count : Mathf.Min(e.hastaGolpe + 1, at.golpes.Count);
                    return n - (at.golpes.Take(n).Any(g => g.parryCancelaFase1) && n > 1 ? 1 : 0);
                });
                P.CurarCompleto();
                bot = 1;
                yield return CercaDeSinInvencible(j);
                yield return UnAtaque(j, co.id);
                var r = new List<PlayerControler.ResultadoDano?>(j.Resultados);
                Comprobar(r.Count == esperados && r.All(x => !x.HasValue || x == PlayerControler.ResultadoDano.Parry),
                          $"Dificil, combo {co.id} con {co.extraDificil.Count} golpe(s) extra: sigue tras el parry ({Lista(r)}; {r.Count} de {esperados})");
                P.CurarCompleto();
                bot = 3;
                yield return CercaDeSinInvencible(j);
                yield return UnAtaque(j, co.id);
                yield return new WaitForSeconds(0.5f);
                float tope = aj.topeDanoCombo * P.VidaMaxima;
                Comprobar(j.DanoUltimaSecuencia <= tope && P.VidaActual > 0,
                          $"Dificil, combo {co.id} sin bloquear: te quita {j.DanoUltimaSecuencia} de {P.VidaMaxima} con el dano de Dificil (tope {tope:0})");
            }
            bot = 0;
            SetCampo(P, "isBlocking", false);
            int peor = 0, racha = 0;
            for (int i = 0; i < registro.Count; i++)
            {
                bool seguido = i > 0 && Mathf.Approximately(registro[i].hora, registro[i - 1].hora) && registro[i - 1].fin > 0f
                               && registro[i].act - registro[i - 1].fin < aj.pausaRecuperacion - 0.05f;
                racha = seguido ? racha + 1 : 1;
                peor = Mathf.Max(peor, racha);
            }
            Comprobar(peor <= aj.maxGolpesSeguidos, $"Dificil: como mucho {peor} golpes seguidos sin pausa de recuperacion");
            Comprobar(golpes.All(x => x.Item2 >= aj.AvisoMinimo(x.Item1, false) - 0.02f), $"Dificil: ningun golpe baja de su aviso minimo ({golpes.Count} golpes)");
            sala.PonerDificil(false);
            sala.PonerInvencible(true);
            P.CurarCompleto();
        }

        // Cada ataque suelto y en bucle: termina, y cada golpe llega con su aviso minimo.
        private IEnumerator Ataques(JefeImpaler j)
        {
            AjustesImpaler aj = j.Ajustes;
            midiendo = true;
            Comprobar(aj.combos.All(c => j.AtaquesPrueba.Contains(c.id)), "la sala lista tambien los combos para lanzarlos");
            // El contraataque solo golpea si le pegas: va en su propia prueba.
            foreach (string a in aj.ataques.Where(x => !x.contraataque).Select(x => x.id))
            {
                golpes.Clear();
                sala.Bucle(a);
                float desde = Time.time;
                int esperados = aj.Buscar(a).golpes.Count * 3;
                yield return Esperar(() => j == null || golpes.Count >= esperados, 40f);
                sala.Bucle(null);
                Comprobar(golpes.Count >= esperados, $"{a}: suelto y en bucle, {golpes.Count / Mathf.Max(1, aj.Buscar(a).golpes.Count)} veces");
                bool avisos = golpes.All(x => x.Item2 >= aj.AvisoMinimo(x.Item1, x.Item1 == aj.Buscar(a).golpes.Last() && aj.Buscar(a).golpes.Count > 1) - 0.02f);
                Comprobar(avisos, $"{a}: cada golpe con su aviso minimo (" + string.Join(", ", golpes.Select(x => $"{x.Item1.nombre} {x.Item2:0.00} s").Distinct().Take(4)) + ")");
            }
            yield return Esperar(() => j == null || j.ProximoAtaque > Time.time, 15f);
            Comprobar(j != null && j.ProximoAtaque > Time.time, "cuenta atras hasta el proximo ataque");
            AjustesImpaler.Ataque salto = aj.Buscar("salto_estela");
            Comprobar(j.UltimoCastigo >= salto.castigoMinimo - 0.02f, $"salto_estela: de rodillas {j.UltimoCastigo:0.00} s (minimo {salto.castigoMinimo:0.00} s)");
            ComprobarMedidas("sala vacia");
        }

        // Lo medido mientras atacaba: nunca dos golpes activos, el hueco minimo
        // entre golpes y siempre dentro de la arena.
        private void ComprobarMedidas(string donde)
        {
            midiendo = false;
            AjustesImpaler aj = J.Ajustes;
            float x0 = -sala.Ajustes.ancho * 0.5f + 0.5f, x1 = sala.Ajustes.ancho * 0.5f - 0.5f;
            Comprobar(maxActivos <= 1, $"{donde}: nunca dos golpes activos a la vez (maximo {maxActivos})");
            Comprobar(peorHueco >= aj.pausaMinEntreGolpes - 0.03f, $"{donde}: hueco minimo entre golpes {peorHueco:0.00} s (ficha {aj.pausaMinEntreGolpes:0.00} s)");
            Comprobar(minX >= x0 && maxX <= x1, $"{donde}: siempre dentro de la arena (x {minX:0.0} a {maxX:0.0}, arena {x0:0.0} a {x1:0.0})");
            Comprobar(dentroTerreno == 0, $"{donde}: nunca se queda dentro del suelo o de una pared ({dentroTerreno} fotogramas)");
            minX = float.MaxValue; maxX = float.MinValue; peorHueco = 99f; maxActivos = 0; dentroTerreno = 0;
        }

        // Con el terreno movible puesto (plataformas y paredes): saltos y embestidas.
        private IEnumerator ConTerreno(JefeImpaler j)
        {
            List<string> puestas = sala.Terreno.PonerTodas();
            yield return null;
            midiendo = true;
            foreach (string a in new[] { "embestida_medialuna", "salto_estela", "cruce_sangre" })
            {
                // El jugador lejos, al otro lado del terreno: el jefe tiene que ir hasta el.
                foreach (int lado in new[] { 1, -1 })
                {
                    yield return Esperar(() => j.ProximoAtaque > Time.time, 20f);
                    PlayerControler p = P;
                    float xj = Mathf.Clamp(lado * 8f, -16f, 16f);
                    p.transform.position = new Vector3(xj, 1f, 0f);
                    Rigidbody2D rbp = p.GetComponent<Rigidbody2D>();
                    if (rbp != null) { rbp.position = p.transform.position; rbp.linearVelocity = Vector2.zero; }
                    yield return new WaitForSeconds(0.3f);
                    float antes = Mathf.Abs(P.transform.position.x - j.transform.position.x);
                    yield return UnAtaque(j, a);
                    float despues = Mathf.Abs(P.transform.position.x - j.transform.position.x);
                    bool avanza = !j.Ajustes.Buscar(a).salto || antes - despues > 2f || despues < 3f;
                    Comprobar(avanza, $"con terreno ({sala.Terreno.Puestas} formas): {a} hacia x {xj:0}, distancia al jugador {antes:0.0} -> {despues:0.0}");
                }
            }
            yield return Esperar(() => j.ProximoAtaque > Time.time, 15f);
            ComprobarMedidas("con terreno");
            sala.Terreno.Limpiar();
            yield return null;
        }

        // Jugador automatico: con parry a todo, ningun golpe le toca sin pararlo (y en
        // la fase 1, parar el tajo corta el giro); sin bloquear, cada ataque le alcanza.
        private IEnumerator ConBot(JefeImpaler j)
        {
            sala.PonerInvencible(false);
            sala.VidaInfinita = true;
            foreach (string a in j.Ajustes.ataques.Where(x => !x.contraataque).Select(x => x.id))
            {
                bot = 1;
                yield return UnAtaque(j, a);
                var r = new List<PlayerControler.ResultadoDano?>(j.Resultados);
                bool todos = r.All(x => !x.HasValue || x == PlayerControler.ResultadoDano.Parry) && r.Any(x => x == PlayerControler.ResultadoDano.Parry);
                Comprobar(todos, $"{a}, parry a todo: " + Lista(r));
                if (a == "tajo_pausa")
                    Comprobar(r.Count == 1, $"tajo_pausa, fase 1: parar el tajo corta el giro ({r.Count} golpe)");
                bot = 3;
                yield return UnAtaque(j, a);
                r = new List<PlayerControler.ResultadoDano?>(j.Resultados);
                Comprobar(r.Any(x => x == PlayerControler.ResultadoDano.Recibido), $"{a}, sin bloquear: su caja te alcanza ({Lista(r)})");
            }
            bot = 0;
            SetCampo(P, "isBlocking", false);
            sala.VidaInfinita = false;
            sala.PonerInvencible(true);
            P.CurarCompleto();
        }

        // Combos con el jugador automatico: parry a todo (sin rellenarle la estamina),
        // solo bloqueo y sin bloquear. Luego la postura rota, el tono, los avisos,
        // los golpes seguidos y que no repite el mismo combo.
        private IEnumerator Combos(JefeImpaler j)
        {
            AjustesImpaler aj = j.Ajustes;
            sala.PonerInvencible(false);
            registro.Clear();
            tonos.Clear();
            avisos.Clear();
            PlayerStamina st = P.GetComponent<PlayerStamina>();
            foreach (AjustesImpaler.Combo co in aj.combos)
            {
                int esperados = co.eslabones.Sum(e =>
                {
                    AjustesImpaler.Ataque at = aj.Buscar(e.ataque);
                    int n = e.hastaGolpe < 0 ? at.golpes.Count : Mathf.Min(e.hastaGolpe + 1, at.golpes.Count);
                    // Fase 1: parar el tajo corta el giro.
                    return n - (at.golpes.Take(n).Any(g => g.parryCancelaFase1) && n > 1 ? 1 : 0);
                });

                // Parry a todo, sin rellenar la estamina: el combo sigue y al final se aturde.
                P.CurarCompleto();
                if (st != null) st.Llenar();
                sinRellenarEstamina = true;
                bot = 1;
                int aturdidos = j.Aturdimientos.Count;
                yield return UnAtaque(j, co.id);
                var r = new List<PlayerControler.ResultadoDano?>(j.Resultados);
                // Los que no le alcanzan (la ola baja si estas delante) salen "fuera".
                bool todos = r.Count(x => x == PlayerControler.ResultadoDano.Parry) >= 2 && r.All(x => !x.HasValue || x == PlayerControler.ResultadoDano.Parry);
                Comprobar(todos && r.Count == esperados, $"combo {co.id}, parry a todo: sigue tras cada parry ({Lista(r)}; {r.Count} de {esperados} golpes)");
                yield return Esperar(() => j.Aturdimientos.Count > aturdidos, 3f);
                Comprobar(j.Aturdimientos.Count > aturdidos, $"combo {co.id}: parados todos, al final queda aturdido ({(j.Aturdimientos.Count > aturdidos ? j.Aturdimientos.Last().tipo + " " + j.Aturdimientos.Last().segundos + " s" : "no")})");
                sinRellenarEstamina = false;

                // Sin bloquear: sigue hasta el final, no lo aturde y nunca pasa del tope.
                foreach (int modo in new[] { 3, 2 })
                {
                    P.CurarCompleto();
                    bot = modo;
                    aturdidos = j.Aturdimientos.Count;
                    yield return UnAtaque(j, co.id);
                    yield return new WaitForSeconds(0.5f);
                    r = new List<PlayerControler.ResultadoDano?>(j.Resultados);
                    float tope = aj.topeDanoCombo * P.VidaMaxima;
                    string n = modo == 3 ? "sin bloquear" : "solo bloqueo";
                    Comprobar(j.Aturdimientos.Count == aturdidos, $"combo {co.id}, {n}: no se aturde al final ({Lista(r)})");
                    Comprobar(j.DanoUltimaSecuencia <= tope, $"combo {co.id}, {n}: te quita {j.DanoUltimaSecuencia} de {P.VidaMaxima} (tope {tope:0})");
                }
            }
            bot = 0;
            SetCampo(P, "isBlocking", false);

            // Nunca mas de N golpes seguidos sin la pausa de recuperacion.
            int peor = 0, racha = 0;
            for (int i = 0; i < registro.Count; i++)
            {
                bool seguido = i > 0 && Mathf.Approximately(registro[i].hora, registro[i - 1].hora) && registro[i - 1].fin > 0f
                               && registro[i].act - registro[i - 1].fin < aj.pausaRecuperacion - 0.05f;
                racha = seguido ? racha + 1 : 1;
                peor = Mathf.Max(peor, racha);
            }
            Comprobar(peor <= aj.maxGolpesSeguidos, $"como mucho {peor} golpes seguidos sin pausa de recuperacion (ficha {aj.maxGolpesSeguidos})");

            // Tono: sube con cada parry seguido (hasta el maximo) y vuelve al fallar.
            bool sube = true, vuelve = true;
            for (int i = 1; i < tonos.Count; i++)
            {
                if (tonos[i] < 0f || tonos[i - 1] < 0f) continue;
                if (tonos[i] < tonos[i - 1] - 0.001f && !Mathf.Approximately(tonos[i], aj.tonoInicial)) sube = false;
            }
            for (int i = 1; i < tonos.Count; i++)
                if (tonos[i - 1] < 0f && tonos[i] > 0f && !Mathf.Approximately(tonos[i], aj.tonoInicial)) vuelve = false;
            float maxTono = tonos.Count > 0 ? tonos.Max() : 0f;
            Comprobar(sube && maxTono > aj.tonoInicial + 0.001f && maxTono <= aj.tonoMaximo + 0.001f,
                      $"el parry sube de tono con cada parry seguido ({string.Join(" ", tonos.Where(t => t > 0f).Take(8).Select(t => t.ToString("0.00")))}..., maximo {maxTono:0.00})");
            Comprobar(vuelve, "tras un golpe sin parry, el tono vuelve al inicial");
            yield return new WaitForSeconds(aj.tonoReiniciaSegundos + 0.5f);
            Comprobar(Mathf.Approximately(j.TonoParry, aj.tonoInicial), $"tras {aj.tonoReiniciaSegundos} s sin parry, el tono vuelve al inicial ({j.TonoParry:0.00})");

            // Avisos: naranja rapido (ligero), rojo lento (pesado); el blanco no se usa.
            var normales = avisos.Where(a => a.peso != AjustesImpaler.Peso.Contraataque).ToList();
            bool colores = normales.All(a => a.peso == AjustesImpaler.Peso.Pesado
                ? a.color == aj.colorPesado && Mathf.Approximately(a.hz, aj.parpadeoPesado)
                : a.color == aj.colorLigero && Mathf.Approximately(a.hz, aj.parpadeoLigero));
            Comprobar(colores && normales.All(a => a.color != aj.colorContraataque) && aj.parpadeoLigero > aj.parpadeoPesado * 2f,
                      $"{normales.Count} avisos: ligeros naranja a {aj.parpadeoLigero} parpadeos/s, pesados rojo a {aj.parpadeoPesado}; ninguno blanco");

            // Postura rota: parrys seguidos aunque sean de ataques distintos.
            bot = 1;
            int antes = j.Aturdimientos.Count(a => a.tipo == "postura");
            for (int i = 0; i < 3 && j.Aturdimientos.Count(a => a.tipo == "postura") == antes; i++) yield return UnAtaque(j, "lanzazo_largo");
            yield return new WaitForSeconds(0.5f);
            Comprobar(j.Aturdimientos.Count(a => a.tipo == "postura") > antes, $"{aj.parrysParaRomper} parrys seguidos le rompen la postura");
            bot = 0;
            SetCampo(P, "isBlocking", false);

            // Pelea libre: nunca repite el mismo ataque o combo dos veces seguidas.
            sala.PonerInvencible(true);
            int desde = j.SecuenciasHechas.Count;
            yield return new WaitForSeconds(35f);
            var hechas = j.SecuenciasHechas.Skip(desde).ToList();
            bool repite = false;
            for (int i = 1; i < hechas.Count; i++) if (hechas[i] == hechas[i - 1]) repite = true;
            Comprobar(hechas.Count >= 4 && !repite && hechas.Any(h => aj.BuscarCombo(h) != null),
                      $"pelea libre: {hechas.Count} ataques y combos sin repetir ({string.Join(", ", hechas)})");
            P.CurarCompleto();
        }

        // Contraataque blanco: sin pegarle, la guardia se acaba sola; pegandole, le
        // entra menos dano y responde tras su retraso con el aviso blanco fijo; parar
        // el tajo blanco lo aturde largo; sin bloquear, te da y no se aturde.
        private IEnumerator Contraataque(JefeImpaler j)
        {
            AjustesImpaler aj = j.Ajustes;
            AjustesImpaler.Ataque a = aj.Buscar("contraataque");
            if (a == null) yield break;

            // 1. Sin pegarle.
            sala.PonerInvencible(true);
            bot = 0;
            yield return Esperar(() => j.ProximoAtaque > Time.time, 20f);
            sala.LanzarAtaque("contraataque");
            yield return Esperar(() => j.EnGuardia, 20f);
            float desde = Time.time;
            Comprobar(j.EnGuardia, "contraataque: se pone en guardia");
            yield return Esperar(() => !j.EnGuardia, aj.duracionGuardia + 3f);
            float duro = Time.time - desde;
            yield return Esperar(() => j.ProximoAtaque > Time.time, 10f);
            Comprobar(Mathf.Abs(duro - aj.duracionGuardia) < 0.2f && j.Resultados.Count == 0,
                      $"contraataque: sin pegarle, la guardia se acaba sola a los {duro:0.00} s (ficha {aj.duracionGuardia}) sin golpe");

            // 2 y 3. Pegandole en la guardia, con parry a todo y sin bloquear.
            sala.PonerInvencible(false);
            sala.VidaInfinita = true;
            foreach (int modo in new[] { 1, 3 })
            {
                float activado = -1f, aviso = -1f, hz = -1f;
                Color color = Color.clear;
                Action<AjustesImpaler.Golpe, float> act = (g, av) => { if (g.peso == AjustesImpaler.Peso.Contraataque) { activado = Time.time; aviso = av; } };
                Action<AjustesImpaler.Golpe, Color, float> avi = (g, c, f) => { if (g.peso == AjustesImpaler.Peso.Contraataque) { color = c; hz = f; } };
                j.AlActivarGolpe += act;
                j.AlAvisar += avi;
                bot = modo;
                yield return Esperar(() => j.ProximoAtaque > Time.time, 20f);
                sala.LanzarAtaque("contraataque");
                yield return Esperar(() => j.EnGuardia, 20f);
                yield return new WaitForSeconds(0.3f);
                EnemyHealth s = sala.Salud;
                int vida = s.CurrentHealth;
                s.TakeDamage(20, P.transform.position, 0f);
                float golpe = Time.time;
                int quitado = vida - s.CurrentHealth;
                int aturdidos = j.Aturdimientos.Count;
                yield return Esperar(() => activado > 0f, 3f);
                yield return Esperar(() => j.ProximoAtaque > Time.time, 10f);
                j.AlActivarGolpe -= act;
                j.AlAvisar -= avi;
                var r = new List<PlayerControler.ResultadoDano?>(j.Resultados);
                string n = modo == 1 ? "parry a todo" : "sin bloquear";
                if (modo == 1)
                {
                    Comprobar(quitado == Mathf.RoundToInt(20 * aj.danoEnGuardia), $"contraataque: en la guardia le entra menos dano ({quitado} de 20; ficha {aj.danoEnGuardia})");
                    Comprobar(activado - golpe >= aj.retrasoContraataque - 0.02f && aviso >= aj.avisoMinContraataque - 0.02f,
                              $"contraataque: responde a los {activado - golpe:0.00} s de tu golpe (retraso minimo {aj.retrasoContraataque}, aviso {aviso:0.00} s)");
                    Comprobar(color == aj.colorContraataque && hz == 0f, $"contraataque: su aviso es blanco y fijo ({color}, {hz} parpadeos/s)");
                    var u = j.Aturdimientos.Count > aturdidos ? j.Aturdimientos.Last() : default;
                    Comprobar(r.Count == 1 && r[0] == PlayerControler.ResultadoDano.Parry && u.tipo == "contraataque" && Mathf.Approximately(u.segundos, aj.aturdidoParryContraataque),
                              $"contraataque, {n}: lo paras y queda aturdido {u.segundos} s ({Lista(r)}, {u.tipo})");
                }
                else
                    Comprobar(r.Count == 1 && r[0] == PlayerControler.ResultadoDano.Recibido && !j.Aturdimientos.Skip(aturdidos).Any(x => x.tipo == "contraataque"),
                              $"contraataque, {n}: te da y no se aturde ({Lista(r)})");
            }
            bot = 0;
            SetCampo(P, "isBlocking", false);
            sala.VidaInfinita = false;
            sala.PonerInvencible(true);
            P.CurarCompleto();
        }

        private static string Lista(List<PlayerControler.ResultadoDano?> r) => string.Join(", ", r.Select(x => x.HasValue ? x.Value.ToString() : "fuera"));

        // Un ataque forzado de principio a fin (de una pausa a la siguiente).
        private IEnumerator UnAtaque(JefeImpaler j, string id)
        {
            yield return Esperar(() => j.ProximoAtaque > Time.time, 20f);
            float desde = j.HoraUltimoAtaque;
            sala.LanzarAtaque(id);
            yield return Esperar(() => j.HoraUltimoAtaque > desde && j.UltimoAtaque == id, 20f);
            yield return Esperar(() => j.ProximoAtaque > Time.time, 20f);
        }

        // Un golpe de verdad: el jugador delante, de espaldas y sin bloquear.
        private IEnumerator GolpeDeVerdad(JefeImpaler j)
        {
            PlayerControler p = P;
            sala.PonerInvencible(false);
            AjustesImpaler.Ataque a = j.Ajustes.Buscar("lanzazo_largo");
            // En una pausa: el jugador a 2 unidades de el, del lado del centro de la
            // sala (nunca dentro de una pared), quieto y sin bloquear.
            yield return Esperar(() => j.ProximoAtaque > Time.time, 20f);
            Rigidbody2D rb = p.GetComponent<Rigidbody2D>();
            float x = j.transform.position.x + (j.transform.position.x > 0f ? -2f : 2f);
            p.transform.position = new Vector3(x, j.transform.position.y + 0.5f, 0f);
            if (rb != null) { rb.position = p.transform.position; rb.linearVelocity = Vector2.zero; }
            p.CurarCompleto();
            yield return new WaitForSeconds(0.2f);
            int antes = p.VidaActual, max = p.VidaMaxima;
            Comprobar(antes == max, $"el jugador empieza con la vida llena ({antes}/{max})");
            int vidaTrasPrimero = -1;
            string donde = "";
            Action<AjustesImpaler.Golpe, PlayerControler.ResultadoDano?> primero = (g, r) =>
            {
                if (vidaTrasPrimero >= 0) return;
                vidaTrasPrimero = p.VidaActual;
                Collider2D cp = p.GetComponent<Collider2D>();
                donde = $"jugador ({p.transform.position.x:0.0}, {p.transform.position.y:0.0}) collider {(cp != null && cp.enabled ? cp.bounds.ToString() : "no")}, " +
                        $"jefe ({j.transform.position.x:0.0}, {j.transform.position.y:0.0}) mira {j.Mirada}, tag {p.tag}";
            };
            j.AlTerminarGolpe += primero;
            yield return UnAtaque(j, "lanzazo_largo");
            j.AlTerminarGolpe -= primero;
            int quitado = vidaTrasPrimero >= 0 ? antes - vidaTrasPrimero : 0;
            string res = Lista(new List<PlayerControler.ResultadoDano?>(j.Resultados));
            Comprobar(quitado > 0 && quitado <= a.golpes[0].dano && p.VidaActual > 0,
                      $"la estocada corta te quita {quitado} (ficha {a.golpes[0].dano}) y no mata con la vida llena ({res}; {donde})");
            yield return Esperar(() => j.ProximoAtaque > Time.time, 10f);
            sala.PonerInvencible(true);
            p.CurarCompleto();
        }

        // Fin de la fase 1 (tregua), renacer, fase 2 y muerte final.
        private IEnumerator Fases(JefeImpaler j)
        {
            AjustesImpaler aj = j.Ajustes;
            int vencidos = 0;
            Action alVencer = () => vencidos++;
            ArenaJefe.AlVencer += alVencer;

            // --- Pelea 1: el jugador se queda cerca de la flor; renace por tiempo encima de el.
            yield return CercaDe(j.transform.position.x);
            ReservaPociones rp = ReservaPociones.Get();
            rp.Rellenar();
            rp.Gastar(ReservaPociones.Tipo.Vida);
            int cargas = rp.Cargas, mana = rp.CargasMana;
            float ataque = j.HoraUltimoAtaque;
            sala.SaltarAFase(2);
            yield return Esperar(() => j.EnTregua, 3f);
            float cae = Time.time;
            Comprobar(j.EnTregua && j.Transformando && j.Modificar(50, TipoArma.Espada) == 0, "tregua: cae y los golpes no le hacen nada");
            Comprobar(Vivos() == 0, "tregua: no queda ningun hechizo en el aire");
            Comprobar(rp.Cargas == cargas + 1 && rp.CargasMana == mana && j.FrascosDados == 1,
                      $"tregua: frasco de sangre gratis ({cargas} -> {rp.Cargas} cargas de {rp.Maximo}; mana {mana} -> {rp.CargasMana})");
            yield return new WaitForSeconds(aj.esperaCartel + 0.3f);
            Comprobar(j.CartelesTregua == 1 && vencidos == 0, $"tregua: sale el cartel de jefe derrotado ({j.CartelesTregua}) sin dar la pelea por ganada ({vencidos} victorias)");
            yield return Esperar(() => j.LatidoActual > 0f, 4f);
            Comprobar(Mathf.Approximately(j.LatidoActual, aj.latidoFlor), $"tregua: la flor late ({j.LatidoActual} por segundo)");
            if (aj.tiempoMaxRenacer > 0f)
            {
                yield return Esperar(() => Time.time - cae > aj.tiempoMaxRenacer - aj.segundosLatidoRapido + 0.3f, aj.tiempoMaxRenacer + 2f);
                Comprobar(j.EnTregua && Mathf.Approximately(j.LatidoActual, aj.latidoFlorRapido),
                          $"tregua: con el jugador cerca no renace antes de tiempo y la flor late mas rapido al final ({j.LatidoActual})");
                Comprobar(Mathf.Approximately(j.HoraUltimoAtaque, ataque) && j.GolpesActivos == 0, "tregua: no ataca");
                // Encima de la flor y sin invencible: renacer no hace dano.
                sala.PonerInvencible(false);
                P.CurarCompleto();
                Colocar(j.Flor.x);
                int vida = P.VidaActual;
                yield return Esperar(() => !j.EnTregua, 5f);
                float renace = Time.time;
                Comprobar(j.MotivoRenacer == "tiempo" && Mathf.Abs(renace - cae - aj.tiempoMaxRenacer) < 0.3f,
                          $"renace por tiempo a los {renace - cae:0.0} s (ficha {aj.tiempoMaxRenacer})");
                bool siempreInvulnerable = true;
                while (j.Transformando) { if (j.Modificar(50, TipoArma.Espada) != 0 || j.GolpesActivos > 0) siempreInvulnerable = false; yield return null; }
                Comprobar(siempreInvulnerable && P.VidaActual == vida, $"al renacer es invulnerable, no golpea y no te hace dano aunque estes encima ({vida} -> {P.VidaActual})");
                sala.PonerInvencible(true);
                EnemyHealth s = sala.Salud;
                Comprobar(sala.FaseVisible >= 2 && j.Fase2 && s.CurrentHealth == s.MaxHealth && s.MaxHealth == aj.vidaFase2,
                          $"fase 2 con la segunda barra llena ({s.CurrentHealth}/{s.MaxHealth})");
                Comprobar(j.LanzaEncendida, "fase 2: la lanza arde");
                float fase = Time.time;
                yield return Esperar(() => j.HoraUltimoAtaque > ataque, 15f);
                Comprobar(j.HoraUltimoAtaque - fase >= aj.pausaInicialFase2 - 0.05f,
                          $"fase 2: primer ataque a los {j.HoraUltimoAtaque - fase:0.00} s de renacer (pausa inicial {aj.pausaInicialFase2})");
                Comprobar(j.FrascosDados == 1, "el frasco gratis no se repite al renacer");
                yield return EnFase2(j);
            }

            // Muerte final: el mensaje de jefe derrotado (la arena lo vence) y la flor.
            EnemyHealth sf = sala.Salud;
            sala.Matar();
            yield return Esperar(() => sf.Muerto, 5f);
            yield return new WaitForSeconds(3.5f);
            AnimadorHoja an = j != null ? j.GetComponentInChildren<AnimadorHoja>() : null;
            Comprobar(sf.Muerto && vencidos == 1, $"muerte final: sale el mensaje de jefe derrotado ({vencidos} victoria)");
            Comprobar(j != null && an != null && an.Actual == "muerte" && an.Terminado && !j.LanzaEncendida, "la flor se queda en la arena (ultimo cuadro de la muerte)");

            // --- Pelea 2 (Reaparecer): tiempo maximo en 0, frascos llenos: solo renace al alejarte.
            float tiempo = aj.tiempoMaxRenacer;
            tiempoGuardado = tiempo;
            aj.tiempoMaxRenacer = 0f;
            sala.Reaparecer();
            yield return Esperar(() => sala.Jefe != null, 3f);
            j = J;
            yield return Esperar(() => j.HoraUltimoAtaque >= 0f, 20f);
            yield return CercaDe(j.transform.position.x);
            rp.Rellenar();
            cargas = rp.Cargas;
            sala.SaltarAFase(2);
            yield return Esperar(() => j.EnTregua, 3f);
            Comprobar(j.FrascosDados == 1 && j.FrascoPendiente == 1 && rp.Cargas == cargas,
                      $"reaparecer reinicia el frasco gratis; con los frascos llenos se guarda ({rp.Cargas} de {rp.Maximo}, {j.FrascoPendiente} pendiente)");
            rp.Gastar(ReservaPociones.Tipo.Vida);
            Comprobar(rp.Cargas == cargas && j.FrascoPendiente == 0, $"al beber, llega el frasco guardado ({rp.Cargas} de {rp.Maximo})");
            yield return new WaitForSeconds(15f);
            Comprobar(j.EnTregua, "con el tiempo maximo en 0 y el jugador cerca, no renace (15 s)");
            float lejos = j.Flor.x + (j.Flor.x > 0f ? -1f : 1f) * (aj.distanciaRenacer + 2f);
            Colocar(lejos);
            float alejado = Time.time;
            yield return Esperar(() => !j.EnTregua, aj.tiempoLejos + 5f);
            Comprobar(!j.EnTregua && j.MotivoRenacer == "distancia" && Time.time - alejado >= aj.tiempoLejos - 0.05f,
                      $"al alejarte {aj.distanciaRenacer + 2f:0} u, renace a los {Time.time - alejado:0.0} s ({j.MotivoRenacer})");
            yield return Esperar(() => !j.Transformando, 6f);
            EnemyHealth s2 = sala.Salud;
            Comprobar(j.Fase2 && s2.CurrentHealth == s2.MaxHealth, $"y empieza la fase 2 con la barra llena ({s2.CurrentHealth}/{s2.MaxHealth})");
            aj.tiempoMaxRenacer = tiempo;
            tiempoGuardado = -1f;
            ArenaJefe.AlVencer -= alVencer;
        }

        // Si la prueba se corta, el tiempo maximo de la ficha vuelve a su valor.
        private static float tiempoGuardado = -1f;
        private static int adaptacionGuardada = -1;
        private void OnDisable()
        {
            AjustesImpaler aj = AssetDatabase.LoadAssetAtPath<AjustesImpaler>(CrearImpaler.RutaFicha);
            if (aj == null) return;
            if (tiempoGuardado >= 0f) aj.tiempoMaxRenacer = tiempoGuardado;
            if (adaptacionGuardada >= 0) aj.adaptacionActiva = adaptacionGuardada == 1;
            tiempoGuardado = -1f;
            adaptacionGuardada = -1;
        }

        private void Colocar(float x)
        {
            PlayerControler p = P;
            x = Mathf.Clamp(x, -sala.Ajustes.ancho * 0.5f + 1.5f, sala.Ajustes.ancho * 0.5f - 1.5f);
            p.transform.position = new Vector3(x, 1f, 0f);
            Rigidbody2D rb = p.GetComponent<Rigidbody2D>();
            if (rb != null) { rb.position = p.transform.position; rb.linearVelocity = Vector2.zero; }
        }

        // El jugador a 2 unidades del jefe, del lado del centro (y espera a una pausa).
        private IEnumerator CercaDe(float x)
        {
            sala.PonerInvencible(true);
            Colocar(x + (x > 0f ? -2f : 2f));
            yield return new WaitForSeconds(0.3f);
        }

        private static readonly FieldInfo CampoVivos = typeof(PeligrosJefe).GetField("vivos", BindingFlags.NonPublic | BindingFlags.Static);
        private static int Vivos() => CampoVivos != null && CampoVivos.GetValue(null) is List<GameObject> l ? l.Count(g => g != null) : -1;

        // Fase 2: avisos minimos con su velocidad, hechizos de cada ataque (el vortice
        // apagado no sale) y la Quemadura (no si te queda poca vida).
        private IEnumerator EnFase2(JefeImpaler j)
        {
            AjustesImpaler aj = j.Ajustes;
            j.HechizosLanzados.Clear();
            golpes.Clear();
            foreach (AjustesImpaler.Ataque a in aj.ataques.Where(x => !x.contraataque))
            {
                yield return CercaDe(j.transform.position.x);
                yield return UnAtaque(j, a.id);
                yield return new WaitForSeconds(1.5f);
                var esperados = a.hechizosFase2.Where(h => h.activo).Select(h => h.tipo).Distinct().ToList();
                Comprobar(esperados.All(t => j.HechizosLanzados.Contains(t)),
                          $"fase 2, {a.id}: sus hechizos ({(esperados.Count > 0 ? string.Join(", ", esperados) : "ninguno")}) salen");
                j.HechizosLanzados.Clear();
            }
            Comprobar(aj.ataques.SelectMany(a => a.hechizosFase2).Where(h => h.activo && h.tipo == AjustesImpaler.TipoHechizo.Vortice).All(h => h.dano == 0),
                      "el vortice no hace dano: solo te atrae hacia la estocada (asi no cae encima de ella)");
            bool avisos = golpes.All(x => x.Item2 >= aj.AvisoMinimo(x.Item1, false) - 0.02f);
            Comprobar(golpes.Count > 0 && avisos, $"fase 2 (x{aj.multVelocidadFase2} de velocidad): {golpes.Count} golpes y ninguno baja de su aviso minimo");

            // Quemadura: el lanzazo largo te alcanza (bloqueando: entra un poco de dano
            // y un 30 % de la Quemadura; asi el jugador sobrevive tambien con poca vida).
            sala.PonerInvencible(false);
            bot = 2;
            int antes = j.QuemadurasDadas;
            P.CurarCompleto();
            yield return CercaDeSinInvencible(j);
            yield return UnAtaque(j, "lanzazo_largo");
            Comprobar(j.QuemadurasDadas > antes, $"fase 2: los golpes pesados acumulan Quemadura ({j.QuemadurasDadas - antes})");
            // Con poca vida, no.
            antes = j.QuemadurasDadas;
            P.CurarCompleto();
            SetCampo(P, "currentHealth", Mathf.RoundToInt(P.VidaMaxima * aj.quemaduraVidaMinima * 0.8f));
            yield return CercaDeSinInvencible(j);
            yield return UnAtaque(j, "lanzazo_largo");
            Comprobar(j.QuemadurasDadas == antes, $"fase 2: con menos del {aj.quemaduraVidaMinima * 100f:0}% de vida no te quema (asi nunca mata sin aviso)");
            bot = 0;
            SetCampo(P, "isBlocking", false);
            sala.PonerInvencible(true);
            P.CurarCompleto();
        }

        private IEnumerator CercaDeSinInvencible(JefeImpaler j)
        {
            yield return Esperar(() => j.ProximoAtaque > Time.time, 20f);
            Colocar(j.transform.position.x + (j.transform.position.x > 0f ? -2f : 2f));
            yield return new WaitForSeconds(0.2f);
        }

        // 10 reapariciones seguidas: un jefe y una barra cada vez; limpiar no deja nada.
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
                int jefes = FindObjectsByType<JefeImpaler>(FindObjectsSortMode.None).Length;
                int barras = FindObjectsByType<BarraJefe>(FindObjectsSortMode.None).Length;
                if (jefes != 1 || barras > 1) { bien = false; Fallo($"reaparicion {i + 1}: {jefes} jefes y {barras} barras"); }
            }
            Comprobar(bien, "10 reapariciones seguidas: un solo jefe y una sola barra");
            sala.Limpiar();
            yield return null;
            yield return null;
            Comprobar(FindObjectsByType<JefeImpaler>(FindObjectsSortMode.None).Length == 0 && FindObjectsByType<BarraJefe>(FindObjectsSortMode.None).Length == 0,
                      "limpiar no deja jefe ni barra");
        }
    }
}
