using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

// Prueba de la sala de pruebas (batchmode: -executeMethod PruebaSalaPruebas.Todo).
//   1. Antes: copia (huellas) de todo el progreso real (partidas, globales.json y
//      claves de PlayerPrefs) y comprobaciones sin jugar (escena fuera de Build
//      Settings, scripts de la sala dentro de #if UNITY_EDITOR).
//   2. Crea un jefe temporal ("PruebaTmpSala") con Warrior > Crear jefe nuevo.
//   3. Ciclo 1 (completo): invoca cada entrada del catalogo, lanza un ataque
//      suelto, salta a la fase 2 y la mata; bucle, Dificil, camara lenta, trucos
//      del jugador, cajas, terreno, menu en 6 resoluciones, muerte del jugador.
//   4. Ciclos 2 a 10: entrar, invocar, limpiar y salir (sin objetos de mas).
//   5. Tras cada salida: nada de la sala queda (carpeta, claves, tiempo) y el
//      progreso real es identico. Al final borra SOLO lo que creo (el jefe temporal).
// Escribe "[SALA] OK ..." / "[SALA] FALLO ..." y el total. Capturas en PruebaNieve/SalaPruebas.
[InitializeOnLoad]
public static class PruebaSalaPruebas
{
    private const string Pre = "PruebaSala.";
    private const string NombreTmp = "PruebaTmpSala";
    private const int Ciclos = 10;

    private static string Fase { get => SessionState.GetString(Pre + "Fase", ""); set => SessionState.SetString(Pre + "Fase", value); }
    private static int Ciclo { get => SessionState.GetInt(Pre + "Ciclo", 0); set => SessionState.SetInt(Pre + "Ciclo", value); }
    private static string Carpeta => Path.Combine(Directory.GetCurrentDirectory(), "PruebaNieve", "SalaPruebas");

    static PruebaSalaPruebas()
    {
        EditorApplication.playModeStateChanged += e =>
        {
            if (Fase != "jugar") return;
            if (e == PlayModeStateChange.EnteredPlayMode) new GameObject("PruebaSala").AddComponent<Conductor>();
            else if (e == PlayModeStateChange.EnteredEditMode)
            {
                Fase = "salida";
                SessionState.SetFloat(Pre + "Desde", (float)EditorApplication.timeSinceStartup);
                EditorApplication.update += Continuar;
            }
        };
        if (Fase == "jefe" || Fase == "salida" || Fase == "fin") EditorApplication.update += Continuar;
    }

    // ------------------------------------------------------------------ Resultado

    private static void Ok(string s) { SessionState.SetInt(Pre + "Ok", SessionState.GetInt(Pre + "Ok", 0) + 1); Debug.Log("[SALA] OK " + s); }
    private static void Fallo(string s) { SessionState.SetInt(Pre + "Fallos", SessionState.GetInt(Pre + "Fallos", 0) + 1); Debug.LogWarning("[SALA] FALLO " + s); }
    private static void Comprobar(bool c, string s) { if (c) Ok(s); else Fallo(s); }

    // ------------------------------------------------------------------ Arranque

    public static void Todo()
    {
        SessionState.SetInt(Pre + "Ok", 0);
        SessionState.SetInt(Pre + "Fallos", 0);
        Directory.CreateDirectory(Carpeta);
        string antes = Instantanea();
        File.WriteAllText(Path.Combine(Carpeta, "progreso_antes.txt"), antes);
        SessionState.SetString(Pre + "Antes", antes);
        Debug.Log("[SALA] Progreso real antes: " + antes.Split('\n').Length + " lineas de huellas");

        // Sin jugar: la sala nunca llega al juego compilado.
        CrearSalaPruebas.Preparar();
        Comprobar(File.Exists(SalaPruebas.Ruta), "la escena de la sala existe");
        Comprobar(!EditorBuildSettings.scenes.Any(s => s.path == SalaPruebas.Ruta), "la sala no esta en Build Settings");
        foreach (string f in Directory.GetFiles("Assets/Scripts/SalaPruebas", "*.cs"))
        {
            string[] l = File.ReadAllLines(f).Where(x => x.Trim() != "").ToArray();
            Comprobar(l.Length > 0 && l[0].Trim() == "#if UNITY_EDITOR" && l[l.Length - 1].Trim() == "#endif", $"{Path.GetFileName(f)} entero dentro de #if UNITY_EDITOR");
        }
        foreach (string f in new[] { "Assets/Editor/SalaPruebas/CrearSalaPruebas.cs", "Assets/Editor/SalaPruebas/CrearJefeNuevo.cs" })
            Comprobar(File.Exists(f), f + " en una carpeta Editor");
        CatalogoPruebas cat = SalaPruebas.Cargar<CatalogoPruebas>("Catalogo Pruebas");
        Comprobar(cat != null && cat.entradas.Count(e => e.EsJefe) >= 3 && cat.entradas.Count(e => !e.EsJefe) >= 18,
                  $"catalogo: {cat?.entradas.Count(e => e.EsJefe)} jefes y {cat?.entradas.Count(e => !e.EsJefe)} enemigos");

        // Jefe temporal.
        SessionState.SetBool(Pre + "CarpetaScripts", AssetDatabase.IsValidFolder(CrearJefeNuevo.CarpetaScripts));
        SessionState.SetBool(Pre + "CarpetaPrefabs", AssetDatabase.IsValidFolder(CrearJefeNuevo.CarpetaPrefabs));
        Comprobar(CrearJefeNuevo.Validar("Sombra") != null && CrearJefeNuevo.Validar("Base") != null, "rechaza nombres de clases que ya existen (JefeSombra, JefeBase)");
        Comprobar(CrearJefeNuevo.Validar("mal nombre") != null, "rechaza nombres con espacios");
        bool creado = CrearJefeNuevo.Crear(NombreTmp);
        Comprobar(creado, "Crear jefe nuevo: script de " + NombreTmp);
        SessionState.SetFloat(Pre + "Desde", (float)EditorApplication.timeSinceStartup);
        Fase = "jefe";
        EditorApplication.update += Continuar;
    }

    private static void Continuar()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        double desde = SessionState.GetFloat(Pre + "Desde", 0f);
        double ahora = EditorApplication.timeSinceStartup;
        switch (Fase)
        {
            case "jefe":
                string prefab = CrearJefeNuevo.Rutas(NombreTmp)[1];
                if ((CrearJefeNuevo.Pendiente || !File.Exists(prefab)) && ahora - desde < 300.0) return;
                EditorApplication.update -= Continuar;
                Comprobar(File.Exists(prefab) && File.Exists(CrearJefeNuevo.Rutas(NombreTmp)[2]), "Crear jefe nuevo: prefab y ficha de ajustes");
                CatalogoPruebas cat = SalaPruebas.Cargar<CatalogoPruebas>("Catalogo Pruebas");
                Comprobar(cat.entradas.Any(e => e.nombre == NombreTmp && e.prefab != null), "el jefe nuevo esta en el catalogo de la sala");
                Comprobar(CrearJefeNuevo.Validar(NombreTmp) != null, "rechaza crear otra vez el mismo nombre: " + CrearJefeNuevo.Validar(NombreTmp));
                Comprobar(FichaBestiario.Get().entradas.All(e => e.enemigo == null || e.enemigo.name != NombreTmp) && FichaJefe.Todas().All(f => f.id != NombreTmp),
                          "el jefe nuevo no esta en el Bestiario ni en los Desafios");
                Jugar(1);
                break;

            case "salida":
                if (ahora - desde < 3.0) return;
                EditorApplication.update -= Continuar;
                TrasSalir();
                if (Ciclo < Ciclos) Jugar(Ciclo + 1);
                else Terminar();
                break;

            case "fin":
                if (ahora - desde < 3.0) return;
                EditorApplication.update -= Continuar;
                Resumen();
                break;
        }
    }

    private static void Jugar(int ciclo)
    {
        Ciclo = ciclo;
        Fase = "jugar";
        EditorSceneManager.OpenScene(SalaPruebas.Ruta, OpenSceneMode.Single);
        ArranquePruebas.Proteger();
        EditorApplication.EnterPlaymode();
    }

    // Fuera del Play: nada de la sala queda y el progreso real no ha cambiado.
    private static void TrasSalir()
    {
        string c = $"ciclo {Ciclo}: ";
        Comprobar(RegistroGuardado.CarpetaSala == null && Desafio.FichaSala == null, c + "al salir, el guardado vuelve a la normalidad");
        Comprobar(!Directory.Exists(SalaPruebas.CarpetaTemporal), c + "la carpeta temporal se borra");
        bool claves = RegistroGuardado.Datos.SelectMany(d => d.prefs).Any(p => PlayerPrefs.HasKey(RegistroGuardado.PrefijoSala + p.nombre));
        Comprobar(!claves, c + "no quedan claves sala_");
        Comprobar(Mathf.Approximately(Time.timeScale, 1f), c + "el tiempo vuelve a x1");
        Comprobar(Instantanea() == SessionState.GetString(Pre + "Antes", ""), c + "el progreso real es identico (partidas, globales.json y PlayerPrefs)");
        if (Ciclo >= 2)
        {
            int base2 = SessionState.GetInt(Pre + "DDOL2", -1), este = SessionState.GetInt(Pre + "DDOL" + Ciclo, -2);
            Comprobar(este == base2, c + $"objetos que pasan de escena: {este} (igual que en el ciclo 2: {base2})");
        }
    }

    // Borra SOLO lo que creo la prueba: el jefe temporal (con sus .meta) y su entrada.
    private static void Terminar()
    {
        CatalogoPruebas cat = SalaPruebas.Cargar<CatalogoPruebas>("Catalogo Pruebas");
        int quitadas = cat.entradas.RemoveAll(e => e != null && e.nombre == NombreTmp);
        EditorUtility.SetDirty(cat);
        AssetDatabase.SaveAssets();
        var borrados = new List<string>();
        foreach (string r in CrearJefeNuevo.Rutas(NombreTmp))
            if (File.Exists(r) && AssetDatabase.DeleteAsset(r)) borrados.Add(r);
        if (!SessionState.GetBool(Pre + "CarpetaScripts", true) && AssetDatabase.IsValidFolder(CrearJefeNuevo.CarpetaScripts) && !Directory.EnumerateFileSystemEntries(CrearJefeNuevo.CarpetaScripts).Any())
            AssetDatabase.DeleteAsset(CrearJefeNuevo.CarpetaScripts);
        if (!SessionState.GetBool(Pre + "CarpetaPrefabs", true) && AssetDatabase.IsValidFolder(CrearJefeNuevo.CarpetaPrefabs) && !Directory.EnumerateFileSystemEntries(CrearJefeNuevo.CarpetaPrefabs).Any())
            AssetDatabase.DeleteAsset(CrearJefeNuevo.CarpetaPrefabs);
        Debug.Log("[SALA] Borrado lo creado por la prueba: " + string.Join(", ", borrados) + $" (+ .meta) y {quitadas} entrada del catalogo");
        Comprobar(CrearJefeNuevo.Rutas(NombreTmp).All(r => !File.Exists(r) && !File.Exists(r + ".meta")), "los archivos del jefe temporal se borran (con su .meta)");
        Fase = "fin";
        SessionState.SetFloat(Pre + "Desde", (float)EditorApplication.timeSinceStartup);
        AssetDatabase.Refresh();
        EditorApplication.update += Continuar;
    }

    private static void Resumen()
    {
        Comprobar(Instantanea() == SessionState.GetString(Pre + "Antes", ""), "al final, el progreso real es identico al de antes");
        int ok = SessionState.GetInt(Pre + "Ok", 0), fallos = SessionState.GetInt(Pre + "Fallos", 0);
        Debug.Log($"[SALA] RESULTADO: {ok} OK, {fallos} FALLOS");
        Fase = "";
        if (Application.isBatchMode) EditorApplication.Exit(fallos > 0 ? 1 : 0);
    }

    // Huellas de todo el progreso real: archivos de las dos carpetas (real y
    // perfil de pruebas) y las claves registradas de PlayerPrefs (con y sin prefijo).
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

    // ------------------------------------------------------------------ En Play

    private class Conductor : MonoBehaviour
    {
        private const BindingFlags Todo = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;
        private readonly List<string> errores = new List<string>();
        private SalaPruebas sala;

        private void Awake()
        {
            Application.logMessageReceived += Registrar;
        }

        private void OnDestroy() => Application.logMessageReceived -= Registrar;

        private void Registrar(string c, string st, LogType t)
        {
            if ((t == LogType.Error || t == LogType.Exception) && !c.StartsWith("[SALA]")) errores.Add(c);
        }

        private IEnumerator Start()
        {
            float limite = Time.realtimeSinceStartup + 20f;
            while ((SalaPruebas.Actual == null || MenuSalaPruebas.Actual == null || FindFirstObjectByType<PlayerControler>() == null) && Time.realtimeSinceStartup < limite)
                yield return null;
            yield return new WaitForSecondsRealtime(1f);
            sala = SalaPruebas.Actual;
            if (sala == null) { Fallo("la sala no arranca"); Salir(); yield break; }

            yield return Seguro(Ciclo == 1 ? Completo() : Rapido());

            // Objetos que pasan de escena (DontDestroyOnLoad).
            GameObject sonda = new GameObject("Sonda");
            DontDestroyOnLoad(sonda);
            int ddol = sonda.scene.GetRootGameObjects().Length - 1;
            Destroy(sonda);
            SessionState.SetInt(Pre + "DDOL" + Ciclo, ddol);
            Comprobar(errores.Count == 0, $"ciclo {Ciclo}: sin errores en la consola {(errores.Count > 0 ? "(" + string.Join(" | ", errores.Distinct().Take(5)) + ")" : "")}");
            Salir();
        }

        // Corre las pruebas anidadas: si una lanza una excepcion, cuenta como fallo y
        // se sigue con la siguiente (sin dejar la prueba colgada).
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
        private CatalogoPruebas.Entrada Buscar(string n) => sala.Catalogo.entradas.FirstOrDefault(e => e.nombre == n);

        // Ciclos 2 a 10: entrar, invocar, limpiar.
        private IEnumerator Rapido()
        {
            sala.PonerInvencible(true);
            sala.Invocar(Buscar("Crimson Wraith"));
            yield return Esperar(() => sala.Salud != null, 3f);
            yield return new WaitForSeconds(1f);
            sala.Limpiar();
            yield return null;
            yield return null;
            Comprobar(FindObjectsByType<JefeBase>(FindObjectsSortMode.None).Length == 0 && FindObjectsByType<BarraJefe>(FindObjectsSortMode.None).Length == 0,
                      $"ciclo {Ciclo}: limpiar no deja jefe ni barra");
        }

        private IEnumerator Completo()
        {
            Comprobar(RegistroGuardado.CarpetaSala == SalaPruebas.CarpetaTemporal, "en la sala todo se guarda en Temp/SalaPruebas");
            Comprobar(PlayerPrefs.HasKey(RegistroGuardado.PrefijoSala + "rpg_almas"), "el personaje de pruebas va a claves sala_ (no a las tuyas)");
            sala.PonerInvencible(true);

            // Cada entrada del catalogo.
            foreach (CatalogoPruebas.Entrada e in sala.Catalogo.entradas.Where(x => x.prefab != null).ToList())
                yield return e.EsJefe ? ProbarJefe(e) : ProbarEnemigo(e);

            yield return ProbarBucle();
            yield return ProbarDificil();
            yield return ProbarTiempo();
            yield return ProbarJugador();
            yield return ProbarCajas();
            yield return ProbarTerreno();
            yield return ProbarMenu();
            yield return ProbarMuerte();

            Globales.GuardarPendiente();
            Comprobar(File.Exists(Path.Combine(SalaPruebas.CarpetaTemporal, RegistroGuardado.ArchivoGlobales)),
                      "lo que se descubre (Bestiario, Bitacora) va al globales.json temporal");
            sala.Limpiar();
        }

        private IEnumerator ProbarJefe(CatalogoPruebas.Entrada e)
        {
            string n = e.nombre + ": ";
            sala.Invocar(e);
            yield return Esperar(() => sala.Jefe != null, 3f);
            Comprobar(sala.Jefe != null && sala.Salud != null, n + "aparece con su arena");
            if (sala.Jefe == null) yield break;
            JefeBase j = sala.Jefe;
            yield return Esperar(() => j == null || j.HoraUltimoAtaque >= 0f, 20f);
            Comprobar(j != null && j.HoraUltimoAtaque >= 0f, n + "empieza a pelear solo (" + (j != null ? j.UltimoAtaque : "-") + ")");
            Comprobar(FindFirstObjectByType<BarraJefe>() != null, n + "tiene su barra de jefe");

            string[] ataques = j.AtaquesPrueba;
            Comprobar(ataques.Length > 0, n + ataques.Length + " ataques en la lista");
            if (ataques.Length > 0)
            {
                string a = ataques.FirstOrDefault(x => x != j.UltimoAtaque && x != "frenesi" && x != "escudo") ?? ataques[0];
                float desde = j.HoraUltimoAtaque;
                sala.LanzarAtaque(a);
                yield return Esperar(() => j == null || (j.HoraUltimoAtaque > desde && j.UltimoAtaque == a), 25f);
                Comprobar(j != null && j.UltimoAtaque == a, n + "ataque suelto \"" + a + "\"");
                bool pausa = false;
                yield return Esperar(() => j == null || (pausa = j.ProximoAtaque > Time.time), 15f);
                Comprobar(pausa, n + "cuenta atras hasta el proximo ataque");
            }

            sala.SaltarAFase(2);
            yield return Esperar(() => j == null || sala.FaseVisible >= 2, 30f);
            Comprobar(j != null && sala.FaseVisible >= 2, n + "salta a la fase 2");

            // Matar: en la Cazadora, primero a su ultima barra.
            yield return new WaitForSeconds(2f);
            float limite = Time.realtimeSinceStartup + 60f;
            while (sala.Salud != null && !sala.Salud.Muerto && Time.realtimeSinceStartup < limite)
            {
                if (sala.Cazadora != null && sala.Cazadora.Fase < 2) sala.SaltarAFase(3);
                sala.Matar();
                yield return new WaitForSeconds(1f);
            }
            Comprobar(sala.Salud == null || sala.Salud.Muerto, n + "matar de un golpe (vencido)");
            yield return new WaitForSeconds(2f);
        }

        private IEnumerator ProbarEnemigo(CatalogoPruebas.Entrada e)
        {
            sala.Invocar(e);
            yield return new WaitForSeconds(0.5f);
            Comprobar(sala.Salud != null && !sala.Salud.Muerto, e.nombre + ": aparece");
            sala.BajarVida();
            sala.Matar();
            yield return Esperar(() => sala.Salud == null || sala.Salud.Muerto, 3f);
            Comprobar(sala.Salud == null || sala.Salud.Muerto, e.nombre + ": matar de un golpe");
        }

        private IEnumerator ProbarBucle()
        {
            sala.Invocar(Buscar("Crimson Wraith"));
            JefeBase j = null;
            yield return Esperar(() => (j = sala.Jefe) != null && j.HoraUltimoAtaque >= 0f, 20f);
            if (j == null) { Fallo("bucle: no aparece el Wraith"); yield break; }
            sala.Bucle("onda");
            int seguidas = 0;
            float visto = j.HoraUltimoAtaque;
            float limite = Time.realtimeSinceStartup + 40f;
            while (j != null && seguidas < 3 && Time.realtimeSinceStartup < limite)
            {
                if (j.HoraUltimoAtaque > visto)
                {
                    visto = j.HoraUltimoAtaque;
                    seguidas = j.UltimoAtaque == "onda" ? seguidas + 1 : 0;
                }
                yield return null;
            }
            Comprobar(seguidas >= 3, "bucle de ataque: \"onda\" 3 veces seguidas");
            Comprobar(j != null && Mathf.Approximately(j.PausaExtraPrueba, sala.Ajustes.pausaBucle), "bucle: pausa extra de la ficha");
            sala.Bucle(null);
            Comprobar(j != null && j.PausaExtraPrueba == 0f, "bucle apagado: sin pausa extra");
        }

        private IEnumerator ProbarDificil()
        {
            CatalogoPruebas.Entrada w = Buscar("Crimson Wraith");
            sala.PonerDificil(false);
            sala.Invocar(w);
            yield return Esperar(() => sala.Salud != null, 3f);
            yield return null;
            int normal = sala.Salud.MaxHealth;
            sala.PonerDificil(true);
            sala.Reaparecer();
            yield return Esperar(() => sala.Salud != null, 3f);
            yield return null;
            int dificil = sala.Salud.MaxHealth;
            int esperado = Mathf.RoundToInt(normal * w.fichaDesafio.multVida);
            Comprobar(Mathf.Abs(dificil - esperado) <= 1, $"Dificil: vida {normal} -> {dificil} (x{w.fichaDesafio.multVida})");
            sala.PonerDificil(false);
            Comprobar(Desafio.MultVida == 1f, "Normal: sin multiplicadores");
            sala.Limpiar();
        }

        private IEnumerator ProbarTiempo()
        {
            foreach (float v in new[] { 0.5f, 0.25f })
            {
                sala.PonerVelocidad(v);
                yield return null;
                yield return null;
                Comprobar(Mathf.Approximately(Time.timeScale, v), $"camara lenta x{v}");
            }
            sala.PonerVelocidad(1f);
            yield return null;
            Comprobar(Mathf.Approximately(Time.timeScale, 1f), "vuelta a x1");
        }

        private IEnumerator ProbarJugador()
        {
            PlayerControler p = P;
            sala.PonerInvencible(true);
            int antes = p.VidaActual;
            p.TakeDamage(15, sala);
            yield return null;
            Comprobar(p.VidaActual == antes, "invencible: el golpe no quita vida");

            sala.PonerInvencible(false);
            sala.VidaInfinita = true;
            p.TakeDamage(15, sala);
            yield return new WaitForSeconds(0.6f);
            Comprobar(p.VidaActual == p.VidaMaxima, $"vida infinita: {p.VidaActual}/{p.VidaMaxima}");
            sala.VidaInfinita = false;

            PlayerStamina s = p.GetComponent<PlayerStamina>();
            sala.EstaminaInfinita = true;
            s.Spend(s.Max * 0.5f);
            yield return null;
            yield return null;
            Comprobar(Mathf.Approximately(s.Current, s.Max), "estamina infinita");
            sala.EstaminaInfinita = false;

            p.transform.position = new Vector3(12f, 1f, 0f);
            p.TakeDamage(10, sala);
            yield return null;
            sala.AlCentro();
            yield return null;
            Comprobar(Mathf.Abs(p.transform.position.x - sala.Ajustes.xCentro) < 0.5f && p.VidaActual == p.VidaMaxima, "volver al centro y restaurar");
            sala.PonerInvencible(true);
        }

        private IEnumerator ProbarCajas()
        {
            sala.Invocar(Buscar("Sombra de los Humedales"));
            yield return Esperar(() => sala.Jefe != null, 3f);
            sala.PonerVerCajas(true);
            yield return null;
            yield return null;
            Comprobar(FindObjectsByType<CajaDebug>(FindObjectsSortMode.None).Any(c => c.gameObject.activeInHierarchy), "cajas de golpe visibles");
            Comprobar(DepuracionCazadora.VerCajas, "cajas reales de la Cazadora encendidas");
            sala.PonerVerCajas(false);
            sala.Limpiar();
        }

        private IEnumerator ProbarTerreno()
        {
            TerrenoPruebas t = sala.Terreno;
            PlayerControler p = P;
            p.transform.position = new Vector3(-17f, 1f, 0f);
            yield return new WaitForSeconds(0.3f);
            int suelo = LayerMask.GetMask("Ground");
            for (int i = 0; i < t.Cantidad; i++)
            {
                TerrenosPruebas.Pieza pieza = sala.Terrenos.formas[i].piezas[0];
                string r = t.Poner(i);
                yield return null;
                bool hay = Physics2D.OverlapPoint(pieza.centro, suelo) != null;
                Comprobar(r == null && t.Puesta(i) && hay, $"terreno \"{t.Nombre(i)}\": se pone (colision en la capa Ground)");
                t.Quitar(i);
                yield return null;
                Comprobar(!t.Puesta(i) && Physics2D.OverlapPoint(pieza.centro, suelo) == null, $"terreno \"{t.Nombre(i)}\": se quita");
            }
            List<string> avisos = t.PonerTodas();
            Comprobar(t.Puestas == t.Cantidad && avisos.Count == 0, "poner todas las formas");
            t.Limpiar();
            Comprobar(t.Puestas == 0, "limpiar terreno");

            // Encima del jugador: no se pone.
            int pilar = Enumerable.Range(0, t.Cantidad).FirstOrDefault(i => t.Nombre(i) == "Pilar central");
            p.transform.position = new Vector3(0f, 1f, 0f);
            yield return new WaitForSeconds(0.3f);
            string choque = t.Poner(pilar);
            Comprobar(choque != null && !t.Puesta(pilar), "no se pone encima del jugador: " + choque);

            // El jugador se apoya en una plataforma igual que en el suelo del juego.
            int bajas = Enumerable.Range(0, t.Cantidad).FirstOrDefault(i => t.Nombre(i) == "Plataformas bajas");
            p.transform.position = new Vector3(-17f, 1f, 0f);
            yield return new WaitForSeconds(1.5f);
            float enSuelo = p.transform.position.y;
            TerrenosPruebas.Pieza plat = sala.Terrenos.formas[bajas].piezas[0];
            float encima = plat.centro.y + plat.tamano.y * 0.5f;
            t.Poner(bajas);
            p.transform.position = new Vector3(plat.centro.x, encima + 2f, 0f);
            yield return new WaitForSeconds(1.5f);
            float y = p.transform.position.y - enSuelo;
            Comprobar(Mathf.Abs(y - encima) < 0.3f, $"el jugador se queda encima de la plataforma ({y:0.00} sobre el suelo; la plataforma esta a {encima:0.00})");
            t.Limpiar();
            sala.AlCentro();
        }

        // El menu no tapa el HUD ni la barra del jefe en 6 resoluciones.
        private IEnumerator ProbarMenu()
        {
            sala.Invocar(Buscar("Sombra de los Humedales"));
            yield return Esperar(() => FindFirstObjectByType<BarraJefe>() != null, 5f);
            yield return new WaitForSeconds(6f);
            MenuSalaPruebas m = MenuSalaPruebas.Actual;
            m.Abrir();
            yield return null;
            Comprobar(MenuSalaPruebas.Abierto && Cinematica.Activa, "F1 abre el menu y el jugador no recibe teclas");
            foreach (Vector2Int res in new[] { new Vector2Int(1920, 1080), new Vector2Int(1920, 1200), new Vector2Int(2560, 1080), new Vector2Int(1280, 720), new Vector2Int(1024, 768), new Vector2Int(800, 600) })
            {
                List<(string, Rect)> zonas = null;
                yield return Captura($"sala_{res.x}x{res.y}", res.x, res.y, z => zonas = z);
                Rect menu = zonas.First(z => z.Item1 == "menu").Item2;
                List<string> pisa = zonas.Where(z => z.Item1 != "menu" && z.Item2.Overlaps(menu)).Select(z => z.Item1).ToList();
                Debug.Log($"[SALA] {res.x}x{res.y}: " + string.Join("  ", zonas.Select(z => $"{z.Item1}=({z.Item2.xMin:0},{z.Item2.yMin:0})-({z.Item2.xMax:0},{z.Item2.yMax:0})")));
                Comprobar(pisa.Count == 0 && menu.height > 100f, $"{res.x}x{res.y}: el menu no tapa el HUD {(pisa.Count > 0 ? string.Join(", ", pisa) : "")}");
            }
            m.Cerrar();
            yield return null;
            Comprobar(!MenuSalaPruebas.Abierto && !Cinematica.Activa, "F1 cierra el menu");
            sala.Limpiar();
        }

        private IEnumerator ProbarMuerte()
        {
            sala.Invocar(Buscar("Sombra de los Humedales"));
            yield return Esperar(() => sala.Jefe != null, 3f);
            yield return new WaitForSeconds(3f);
            sala.PonerInvencible(false);
            PlayerControler p = P;
            float limite = Time.realtimeSinceStartup + 10f;
            while (p != null && p.VidaActual > 0 && Time.realtimeSinceStartup < limite)
            {
                p.TakeDamage(9999, sala);
                yield return new WaitForSeconds(0.4f);
            }
            yield return Esperar(() => P != null && P != p, 20f);
            Comprobar(P != null && P != p, "el jugador muere y reaparece en la sala");
            yield return new WaitForSeconds(1f);
            Comprobar(FindObjectsByType<JefeBase>(FindObjectsSortMode.None).Length == 0, "al morir, la arena retira al jefe (como en el juego)");
            sala.PonerInvencible(true);
            sala.Reaparecer();
            yield return Esperar(() => sala.Jefe != null, 3f);
            Comprobar(sala.Jefe != null, "reaparecer tras morir");
            sala.Limpiar();
        }

        // Como en PruebaRonda17: los lienzos a una camara con el tamano pedido.
        private IEnumerator Captura(string nombre, int ancho, int alto, Action<List<(string, Rect)>> zonas)
        {
            Camera cam = Camera.main;
            RenderTexture rt = new RenderTexture(ancho, alto, 24);
            cam.targetTexture = rt;
            var cambiados = new List<Canvas>();
            foreach (Canvas c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay && c.gameObject.activeInHierarchy)
                { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = cam; c.planeDistance = 1f; c.sortingLayerName = "VFX"; c.sortingOrder += 500; cambiados.Add(c); }
            for (int i = 0; i < 4; i++) { Canvas.ForceUpdateCanvases(); yield return null; }
            var l = new List<(string, Rect)>();
            void Anadir(string n, RectTransform r) { if (r != null && r.gameObject.activeInHierarchy) l.Add((n, RectPantalla(cam, r))); }
            Anadir("menu", MenuSalaPruebas.Actual.Panel);
            PlayerHud hud = FindFirstObjectByType<PlayerHud>();
            if (hud != null)
                foreach (UnityEngine.UI.Graphic g in hud.GetComponentsInChildren<UnityEngine.UI.Graphic>())
                    if (g.enabled && g.canvasRenderer.GetInheritedAlpha() >= 0.01f) Anadir("hud_" + g.name, g.rectTransform);
            ContadorPociones cp = FindFirstObjectByType<ContadorPociones>();
            if (cp != null && cp.GetType().GetField("huecos", Todo)?.GetValue(cp) is Array huecos)
                for (int i = 0; i < huecos.Length; i++) Anadir("frasco_" + i, huecos.GetValue(i).GetType().GetField("caja", Todo)?.GetValue(huecos.GetValue(i)) as RectTransform);
            IndicadorInventario ii = FindFirstObjectByType<IndicadorInventario>();
            if (ii != null) Anadir("aviso", ii.GetType().GetField("caja", Todo)?.GetValue(ii) as RectTransform);
            ContadorAlmas ca = FindFirstObjectByType<ContadorAlmas>();
            if (ca != null) Anadir("almas", ca.transform.Find("Caja") as RectTransform);
            BarraJefe b = FindFirstObjectByType<BarraJefe>();
            if (b != null) Anadir("barra", b.Marco);
            zonas(l);
            cam.Render();
            cam.targetTexture = null;
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(ancho, alto, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, ancho, alto), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(Carpeta, nombre + ".png"), tex.EncodeToPNG());
            rt.Release();
            foreach (Canvas c in cambiados) { c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder -= 500; }
            for (int i = 0; i < 2; i++) yield return null;
        }

        private static Rect RectPantalla(Camera cam, RectTransform r)
        {
            Vector3[] e = new Vector3[4];
            r.GetWorldCorners(e);
            Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, e[0]), c = RectTransformUtility.WorldToScreenPoint(cam, e[2]);
            return Rect.MinMaxRect(Mathf.Min(a.x, c.x), Mathf.Min(a.y, c.y), Mathf.Max(a.x, c.x), Mathf.Max(a.y, c.y));
        }
    }
}
