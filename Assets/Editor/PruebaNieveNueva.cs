using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// Prueba de la nieve del diagrama (linea de comandos, sin -quit):
//   ... -executeMethod PruebaNieveNueva.Todo
// En el Editor: generar dos veces da lo mismo, el fondo no cambia, los saltos se pueden
// hacer (calculo) y la decoracion no tiene colisiones. Jugando: la escena carga, las 3
// hogueras, los 4 cofres con su contenido, las paredes falsas, la pared que se rompe solo
// desde la derecha (y queda guardada), el agua solo con escarcha, la corriente de viento,
// el minijefe opcional, el desafio de la Sombra, el portal y las partidas viejas.
// Al final comprueba que la carpeta real del progreso no cambio.
// Escribe "[NN] OK ..." / "[NN] FALLO ..." y el total. Sin sonido.
[InitializeOnLoad]
public static class PruebaNieveNueva
{
    private const string Clave = "PruebaNieveNueva.Modo";
    private const string ClaveFotoReal = "PruebaNieveNueva.FotoReal";
    private const string ClaveEditor = "PruebaNieveNueva.Editor";

    static PruebaNieveNueva()
    {
        EditorApplication.playModeStateChanged += e =>
        {
            string modo = SessionState.GetString(Clave, "");
            if (modo == "") return;
            if (e == PlayModeStateChange.EnteredPlayMode) new GameObject("PruebaNieveNueva").AddComponent<Conductor>();
            else if (e == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetString(Clave, "");
                bool igual = FotoCarpetaReal() == SessionState.GetString(ClaveFotoReal, "");
                Debug.Log((igual ? "[NN] OK " : "[NN] FALLO ") + "la carpeta real del progreso no cambio");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
        };
    }

    public static void Todo()
    {
        SessionState.SetString(ClaveFotoReal, FotoCarpetaReal());
        var r = new List<string>();
        void C(bool c, string s) => r.Add((c ? "OK " : "FALLO ") + s);

        // Generar dos veces: el mismo resultado. Y el fondo, el ambiente y la arena, los de la base.
        Scene b = EditorSceneManager.OpenScene(GenerarNieveDiagrama.RutaBase, OpenSceneMode.Single);
        string fondoBase = Parte(b, "Nivel/Fondo") + Parte(b, "AmbienteNieve") + Parte(b, "Nivel/ArenaJefe");
        string config = File.ReadAllText("Assets/Data/Niveles/Config Nivel Nieve.asset");
        GenerarNieveDiagrama.Generar();
        string h1 = GenerarNieveDiagrama.Huella(EditorSceneManager.GetActiveScene());
        GenerarNieveDiagrama.Generar();
        Scene s = EditorSceneManager.GetActiveScene();
        C(h1 == GenerarNieveDiagrama.Huella(s), "generar dos veces da el mismo resultado");
        C(fondoBase == Parte(s, "Nivel/Fondo") + Parte(s, "AmbienteNieve") + Parte(s, "Nivel/ArenaJefe"), "el parallax, el ambiente y la arena de la Sombra no cambiaron");
        C(config == File.ReadAllText("Assets/Data/Niveles/Config Nivel Nieve.asset"), "Config Nivel Nieve no cambio");

        // Saltos (calculo con los numeros del player).
        var saltos = GenerarNieveDiagrama.ComprobarSaltos();
        foreach (var j in saltos.Where(j => !j.ok)) C(false, "salto posible: " + j.nombre + " (" + j.detalle + ")");
        C(saltos.Where(j => j.principal).All(j => j.ok), $"todos los saltos del camino principal se pueden hacer ({saltos.Count(j => j.principal)})");

        // Decoracion: ninguna pieza con colision.
        Transform deco = s.GetRootGameObjects().First(g => g.name == "Nivel").transform.Find("Decoracion");
        C(deco != null && deco.GetComponentsInChildren<Collider2D>(true).All(c => c.isTrigger), "ninguna decoracion tiene colision (no bloquea el camino)");
        // La escena esta en la lista y el portal lleva a la cueva.
        C(EditorBuildSettings.scenes.Any(e => e.path == GenerarNieveDiagrama.RutaActiva && e.enabled) &&
          !EditorBuildSettings.scenes.Any(e => e.path == GenerarNieveDiagrama.RutaBase), "Nivel Nieve en la lista de escenas y Nieve_Ronda19 fuera");
        PortalNivel salida = Object.FindObjectsByType<PortalNivel>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(x => x.name == "ExitDoor");
        string destino = salida != null ? new SerializedObject(salida).FindProperty("escenaDestino").stringValue : "";
        C(destino == "Nivel Cueva" && EditorBuildSettings.scenes.Any(e => e.path == "Assets/Scenes/Nivel Cueva.unity"), "el portal del final lleva a la cueva (" + destino + ")");

        SessionState.SetString(ClaveEditor, string.Join("\n", r));
        SessionState.SetString(Clave, "todo");
        EditorSceneManager.OpenScene("Assets/Scenes/Menu Principal.unity");
        ArranquePruebas.Proteger();
        EditorApplication.EnterPlaymode();
    }

    private static string Parte(Scene s, string camino)
    {
        string[] p = camino.Split('/');
        GameObject r = s.GetRootGameObjects().FirstOrDefault(g => g.name == p[0]);
        Transform t = r == null ? null : p.Length > 1 ? r.transform.Find(string.Join("/", p.Skip(1))) : r.transform;
        if (t == null) return "falta " + camino;
        var sb = new System.Text.StringBuilder();
        GenerarNieveDiagrama.HuellaDe(t, "", sb);
        return sb.ToString();
    }

    // Lista de archivos de la carpeta real del progreso, con su tamano y fecha.
    private static string FotoCarpetaReal()
    {
        string c = RegistroGuardado.CarpetaDe(false);
        if (!Directory.Exists(c)) return "(no existe)";
        return string.Join("\n", Directory.GetFiles(c, "*", SearchOption.AllDirectories).OrderBy(f => f)
            .Select(f => f + " " + new FileInfo(f).Length + " " + File.GetLastWriteTimeUtc(f).Ticks));
    }

    private class Conductor : MonoBehaviour
    {
        private int ok, fallos, errores;
        private PlayerControler p;
        private const BindingFlags Todo = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static;

        private void Ok(string s) { ok++; Debug.Log("[NN] OK " + s); }
        private void Fallo(string s) { fallos++; Debug.LogWarning("[NN] FALLO " + s); }
        private void Comprobar(bool c, string s) { if (c) Ok(s); else Fallo(s); }

        private IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject);
            AudioListener.volume = 0f;
            Application.logMessageReceived += (c, st, t) => { if ((t == LogType.Exception || t == LogType.Error) && !c.StartsWith("[NN]")) { errores++; Debug.Log("[NN] error visto: " + c); } };
            foreach (string l in SessionState.GetString(ClaveEditor, "").Split('\n').Where(l => l.Length > 0))
                if (l.StartsWith("OK ")) Ok(l.Substring(3)); else Fallo(l.Substring(6));
            string carpeta = Path.Combine(Directory.GetCurrentDirectory(), "PruebaNieve", "PartidasNieveNueva");
            Partida.CarpetaPruebas = carpeta;
            if (Directory.Exists(carpeta)) Directory.Delete(carpeta, true);
            Directory.CreateDirectory(carpeta);
            Globales.Recargar();
            IEnumerator r = Pruebas();
            float limite = Time.realtimeSinceStartup + 600f;
            while (true)
            {
                bool sigue;
                try { sigue = r.MoveNext(); }
                catch (System.Exception e) { Fallo("excepcion: " + e); sigue = false; }
                if (!sigue || Time.realtimeSinceStartup > limite) break;
                yield return r.Current;
            }
            Comprobar(errores == 0, $"sin errores en la consola ({errores})");
            Debug.Log($"[NN] RESULTADO: {ok} OK, {fallos} FALLOS (mas la comprobacion de la carpeta real, al salir)");
            Time.timeScale = 1f;
            Partida.Descargar();
            Globales.GuardarPendiente();
            EditorApplication.ExitPlaymode();
        }

        private IEnumerator Pruebas()
        {
            // --- La escena carga con una partida nueva.
            yield return new WaitForSeconds(1f);
            Partida.Nueva("Nivel Nieve");
            Equipo.Reiniciar();
            SceneManager.LoadScene("Nivel Nieve");
            yield return EsperarEscena("Nivel Nieve");
            yield return new WaitForSeconds(2f);
            p = Object.FindFirstObjectByType<PlayerControler>();
            Comprobar(SceneManager.GetActiveScene().name == "Nivel Nieve" && p != null, "la escena Nivel Nieve carga con el player");
            p.InvulnerableExterno = true;
            StartCoroutine(MantenerVivo());

            // --- Hogueras y cofres.
            Hoguera[] hs = Object.FindObjectsByType<Hoguera>(FindObjectsSortMode.None).OrderBy(h => h.transform.position.x).ToArray();
            Comprobar(hs.Length == 3, $"hay 3 hogueras ({hs.Length})");
            var almas = Object.FindObjectsByType<CofreAlmas>(FindObjectsSortMode.None).Select(c => (int)Campo(c, "almas")).OrderBy(a => a).ToArray();
            Comprobar(almas.SequenceEqual(new[] { 300, 500 }), "2 cofres de almas: 300 y 500 (" + string.Join(", ", almas) + ")");
            var mejoras = Object.FindObjectsByType<CofreMejora>(FindObjectsSortMode.None).Select(c => (Equipo.Objeto)Campo(c, "objeto")).OrderBy(o => o).ToArray();
            Comprobar(mejoras.SequenceEqual(new[] { Equipo.Objeto.LagrimaCarmesi, Equipo.Objeto.FrascoSangre }), "2 cofres de mejora: Lágrima carmesí y Frasco de sangre");
            Comprobar(Object.FindObjectsByType<CofreMejora>(FindObjectsSortMode.None).All(c => ((Equipo.Objeto[])Campo(c, "extra")).Length == 0), "los cofres de mejora no llevan nada más");

            // --- Paredes falsas: se atraviesan (sin colision) y tapan las 3 salas ocultas.
            Tilemap paredes = GameObject.Find("ParedesFalsas").GetComponent<Tilemap>();
            Collider2D cp = paredes.GetComponent<Collider2D>();
            int casillas = 0;
            foreach (Vector3Int c in paredes.cellBounds.allPositionsWithin) if (paredes.HasTile(c)) casillas++;
            Comprobar((cp == null || !cp.enabled) && casillas >= 3, $"las paredes falsas se atraviesan ({casillas} casillas, sin colision)");
            Tilemap ocultas = GameObject.Find("ZonasOcultas").GetComponent<Tilemap>();
            bool Oculta(Vector2 w) => ocultas.HasTile(ocultas.WorldToCell(w + Vector2.up * 0.5f));
            CofreAlmas c300 = Object.FindObjectsByType<CofreAlmas>(FindObjectsSortMode.None).First(c => (int)Campo(c, "almas") == 300);
            CofreMejora cFrasco = Object.FindObjectsByType<CofreMejora>(FindObjectsSortMode.None).First(c => (Equipo.Objeto)Campo(c, "objeto") == Equipo.Objeto.FrascoSangre);
            ArenaJefe mini = Object.FindObjectsByType<ArenaJefe>(FindObjectsSortMode.None).FirstOrDefault(a => !a.EsPrincipal);
            Comprobar(Oculta(c300.transform.position) && Oculta(cFrasco.transform.position) && mini != null && Oculta(mini.transform.position),
                      "las paredes falsas esconden el escondite 4, la arena del minijefe y el escondite 17");

            // --- Pared que se rompe (7): solo desde la derecha, y queda guardada.
            DerrumbeAtajo d = Object.FindFirstObjectByType<DerrumbeAtajo>();
            float pieD = d.GetComponent<Collider2D>().bounds.min.y + 0.1f;
            Teletransportar(new Vector3(d.transform.position.x - 2f, pieD, 0f));
            yield return new WaitForSeconds(0.3f);
            for (int i = 0; i < 6; i++) { d.Golpear(Elemento.Ninguno, d.transform.position); yield return new WaitForSeconds(0.1f); }
            Comprobar(d.gameObject.activeSelf && d.GetComponent<Collider2D>().enabled, "la pared (7) no cede desde la izquierda");
            Teletransportar(new Vector3(d.transform.position.x + 2f, pieD, 0f));
            yield return new WaitForSeconds(0.3f);
            for (int i = 0; i < 4 && d.gameObject.activeSelf; i++) { d.Golpear(Elemento.Ninguno, d.transform.position); yield return new WaitForSeconds(0.1f); }
            yield return new WaitForSeconds(1.2f);
            string banderaAtajo = "atajo_Nivel Nieve_" + d.clave;
            Comprobar(!d.gameObject.activeSelf && Partida.Bandera(banderaAtajo), "la pared (7) se rompe desde la derecha");
            Partida.Guardar();
            string archivo = Directory.GetFiles(Partida.CarpetaPruebas, "partida_*.json").FirstOrDefault();
            Comprobar(archivo != null && File.ReadAllText(archivo).Contains(banderaAtajo), "su bandera se guarda en la partida (carpeta de pruebas)");

            // --- Agua: solo con escarcha.
            AguaCongelable agua = Object.FindFirstObjectByType<AguaCongelable>();
            Collider2D puente = (Collider2D)Campo(agua, "puenteHielo");
            agua.Golpear(Elemento.Fuego, agua.transform.position);
            agua.Golpear(Elemento.Sagrado, agua.transform.position);
            yield return new WaitForSeconds(0.3f);
            Comprobar(!puente.enabled, "el agua no se congela con fuego ni con lo sagrado");
            agua.Golpear(Elemento.Hielo, agua.transform.position);
            yield return new WaitForSeconds(1.2f);
            Comprobar(puente.enabled && Partida.Bandera("sello_Nivel Nieve_" + agua.clave), "el agua se congela con escarcha y queda guardada");

            // --- Corriente de viento: sube en su fase hacia arriba, no en la de hacia abajo.
            CorrienteViento cv = Object.FindFirstObjectByType<CorrienteViento>();
            Bounds cb = cv.GetComponent<Collider2D>().bounds;
            Vector3 aire = new Vector3(cb.min.x + 11f, -20f, 0f);   // hueco libre entre las plataformas (x 146-148)
            yield return Esperar(() => cv.FaseActual == CorrienteViento.Fase.PausaTrasBajada, 10f);
            yield return Esperar(() => cv.FaseActual == CorrienteViento.Fase.Subida, 3f);
            Teletransportar(aire);
            yield return new WaitForSeconds(0.8f);
            float subio = p.transform.position.y - aire.y;
            Comprobar(subio > 1f, $"la corriente sube al player en su fase hacia arriba ({subio:+0.0;-0.0} u en 0,8 s)");
            yield return Esperar(() => cv.FaseActual == CorrienteViento.Fase.Bajada, 6f);
            Teletransportar(aire);
            yield return new WaitForSeconds(0.4f);
            float bajada = p.transform.position.y - aire.y;
            Comprobar(bajada < -0.5f, $"en la fase hacia abajo no sube: lo hunde ({bajada:+0.0;-0.0} u en 0,4 s)");
            Teletransportar(hs[1].PuntoReaparicion);
            yield return new WaitForSeconds(0.5f);

            // --- Minijefe opcional: su victoria no cuenta como la de la Sombra.
            ZonaJefe zm = mini.GetComponentInChildren<ZonaJefe>();
            Vector2 pj = mini.PuntoJefe;
            Teletransportar(new Vector3(zm.transform.position.x + zm.distanciaActivador + 1.5f, zm.transform.position.y + 0.7f, 0f));
            yield return Esperar(() => ArenaJefe.EnCombate, 4f);
            yield return Esperar(() => Object.FindFirstObjectByType<JefeMartialHero>() != null, 6f);
            JefeMartialHero mh = Object.FindFirstObjectByType<JefeMartialHero>();
            Comprobar(mh != null, "el Martial Hero aparece en su arena");
            for (int i = 0; i < 4 && mh != null && ArenaJefe.EnCombate; i++)
            {
                mh.GetComponent<EnemyHealth>().DanoEstado(99999);
                yield return Esperar(() => !ArenaJefe.EnCombate, 8f);
            }
            yield return new WaitForSeconds(2.5f);
            Comprobar(Partida.Bandera("minijefe_Nivel Nieve_martial") && !Partida.Bandera("jefe_Nivel Nieve"),
                      "vencer al minijefe guarda su bandera y no marca a la Sombra como vencida");
            ArenaJefe sombra = Object.FindObjectsByType<ArenaJefe>(FindObjectsSortMode.None).First(a => a.EsPrincipal);
            Comprobar(sombra.PuedeEmpezar, "la arena de la Sombra sigue esperando (el nivel se termina sin el minijefe)");

            // --- Partidas viejas: una hoguera que ya no existe reaparece en la que le corresponde.
            Partida.Descargar();
            yield return CargarVieja(new Vector2(237.2f, 3.6f), new[] { "sello_Nivel Nieve_lago", "atajo_Nivel Nieve_tumulo", "sello_Nivel Nieve_muro" });
            hs = Object.FindObjectsByType<Hoguera>(FindObjectsSortMode.None).OrderBy(h => h.transform.position.x).ToArray();
            Comprobar(Vector2.Distance(p.transform.position, hs[1].PuntoReaparicion) < 1.5f,
                      $"partida en la hoguera 3 vieja: reaparece en la hoguera 2 nueva ({p.transform.position.x:0.#}, {p.transform.position.y:0.#})");
            Comprobar(!((Collider2D)Campo(Object.FindFirstObjectByType<AguaCongelable>(), "puenteHielo")).enabled &&
                      Object.FindFirstObjectByType<DerrumbeAtajo>() != null && Object.FindObjectsByType<SelloElemental>(FindObjectsSortMode.None).All(x => !x.Abierto),
                      "las banderas viejas no abren nada (lago, atajo, sellos)");
            Partida.Descargar();
            yield return CargarVieja(new Vector2(500f, 80f), new string[0]);
            hs = Object.FindObjectsByType<Hoguera>(FindObjectsSortMode.None).OrderBy(h => h.transform.position.x).ToArray();
            Comprobar(Vector2.Distance(p.transform.position, hs[0].PuntoReaparicion) < 1.5f, "partida en una posicion desconocida: reaparece en la hoguera 1");
            Partida.Descargar();

            // --- Desafio de la Sombra: monta su arena en el nivel nuevo.
            FichaJefe f = FichaJefe.DeEscena("Nivel Nieve");
            Desafio.Empezar(f, false);
            yield return EsperarEscena("Nivel Nieve");
            yield return new WaitForSeconds(2.5f);
            p = Object.FindFirstObjectByType<PlayerControler>();
            p.InvulnerableExterno = true;
            hs = Object.FindObjectsByType<Hoguera>(FindObjectsSortMode.None);
            Comprobar(hs.Length == 1 && Mathf.Abs(hs[0].transform.position.x - 350.3f) < 0.5f && Object.FindFirstObjectByType<TotemTienda>() != null,
                      $"desafio: una hoguera (la 3), totem y cofre ({hs.Length} hogueras)");
            ZonaJefe z = Object.FindObjectsByType<ZonaJefe>(FindObjectsSortMode.None).First(x => (x.Arena as ArenaJefe) != null && ((ArenaJefe)x.Arena).EsPrincipal);
            Teletransportar(new Vector3(z.transform.position.x + z.distanciaActivador + 1f, 3.7f, 0f));
            yield return Esperar(() => ArenaJefe.EnCombate, 3f);
            yield return Esperar(() => Object.FindFirstObjectByType<JefeBase>() != null, 8f);
            JefeBase jb = Object.FindFirstObjectByType<JefeBase>();
            Comprobar(ArenaJefe.EnCombate && jb != null && jb.name.Contains("Sombra"), "desafio: la pelea con la Sombra empieza");
            Desafio.Salir();
            yield return EsperarEscena("Menu Principal");
            yield return new WaitForSeconds(1f);
        }

        // Partida guardada con otra version de la nieve: reaparecia en "pos" y tenia esas banderas.
        private IEnumerator CargarVieja(Vector2 pos, string[] banderas)
        {
            Partida.Nueva("Nivel Nieve");
            foreach (string b in banderas) Partida.PonerBandera(b);
            Partida.Actual.enHoguera = true;
            Partida.Actual.x = pos.x;
            Partida.Actual.y = pos.y;
            Partida.Guardar(escena: "Nivel Nieve");
            Partida.Cargar(Partida.Actual);
            SceneManager.LoadScene("Nivel Nieve");
            yield return EsperarEscena("Nivel Nieve");
            yield return new WaitForSeconds(2f);
            p = Object.FindFirstObjectByType<PlayerControler>();
            p.InvulnerableExterno = true;
        }

        private IEnumerator MantenerVivo()
        {
            while (true)
            {
                if (p == null) p = Object.FindFirstObjectByType<PlayerControler>();
                if (p != null) { SetCampo(p, "currentHealth", p.VidaMaxima); SetCampo(p, "isInvincible", true); }
                yield return null;
            }
        }

        private static IEnumerator Esperar(System.Func<bool> cond, float segundos)
        {
            float t0 = Time.realtimeSinceStartup;
            while (!cond() && Time.realtimeSinceStartup - t0 < segundos) yield return null;
        }

        private IEnumerator EsperarEscena(string nombre)
        {
            float t0 = Time.realtimeSinceStartup;
            while ((SceneManager.GetActiveScene().name != nombre || PantallaCarga.Cargando) && Time.realtimeSinceStartup - t0 < 40f) yield return null;
        }

        private void Teletransportar(Vector3 pos)
        {
            if (p == null) p = Object.FindFirstObjectByType<PlayerControler>();
            if (p == null) return;
            Rigidbody2D rb = p.GetComponent<Rigidbody2D>();
            rb.position = pos;
            rb.linearVelocity = Vector2.zero;
            p.transform.position = pos;
        }

        private static object Campo(object o, string campo)
        {
            for (System.Type t = o.GetType(); t != null; t = t.BaseType)
            {
                FieldInfo f = t.GetField(campo, Todo);
                if (f != null) return f.GetValue(o);
            }
            return null;
        }

        private static void SetCampo(object o, string campo, object v)
        {
            for (System.Type t = o.GetType(); t != null; t = t.BaseType)
            {
                FieldInfo f = t.GetField(campo, Todo);
                if (f != null) { f.SetValue(o, v); return; }
            }
        }
    }
}
