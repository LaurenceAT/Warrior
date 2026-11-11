using System.Collections;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Prueba del desafio del Black Knight (bosque).
//   ... -executeMethod PruebaDesafioBlackKnight.Todo   (batchmode, sin -quit)
//   1. Sin jugar: Warrior > Black Knight > Desafio; escena, ficha (sus ataques con
//      los ids del jefe, cofre igual que el de la Cazadora, visible), 2 logros,
//      config sin musica y Build Settings; "Bosque Cazadora" no cambia.
//   2. Jugando (desde el menu principal, en silencio y en una carpeta de pruebas):
//      el desafio en Normal: una hoguera, la arena normal, el jefe con una barra,
//      fase 2 y fase 3 con la misma barra, la camara abierta en las ondas,
//      victoria, desafio completado y logro.
// Escribe "[DESBK] OK/FALLO" y capturas en PruebaNieve/desafio_black_knight_*.png.
[InitializeOnLoad]
public static class PruebaDesafioBlackKnight
{
    private const string Clave = "PruebaDesafioBlackKnight.";
    private const string Bosque = "Assets/Scenes/Bosque Cazadora.unity";

    static PruebaDesafioBlackKnight()
    {
        EditorApplication.playModeStateChanged += e =>
        {
            if (SessionState.GetString(Clave + "Modo", "") == "") return;
            if (e == PlayModeStateChange.EnteredPlayMode) new GameObject("PruebaDesafioBlackKnight").AddComponent<Conductor>();
            else if (e == PlayModeStateChange.EnteredEditMode)
            {
                Comprobar(Huella(Bosque) == SessionState.GetString(Clave + "Huella", ""), "Bosque Cazadora sigue igual tras jugar");
                int ok = SessionState.GetInt(Clave + "ok", 0), fallos = SessionState.GetInt(Clave + "fallos", 0);
                Debug.Log($"[DESBK] RESULTADO: {ok} OK, {fallos} FALLOS");
                SessionState.SetString(Clave + "Modo", "");
                if (Application.isBatchMode) EditorApplication.Exit(fallos > 0 ? 1 : 0);
            }
        };
    }

    private static void Ok(string s) { SessionState.SetInt(Clave + "ok", SessionState.GetInt(Clave + "ok", 0) + 1); Debug.Log("[DESBK] OK " + s); }
    private static void Fallo(string s) { SessionState.SetInt(Clave + "fallos", SessionState.GetInt(Clave + "fallos", 0) + 1); Debug.LogWarning("[DESBK] FALLO " + s); }
    private static void Comprobar(bool c, string s) { if (c) Ok(s); else Fallo(s); }

    private static string Huella(string ruta)
    {
        using (var sha = SHA1.Create()) return System.Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(ruta)));
    }

    public static void Todo()
    {
        SessionState.SetInt(Clave + "ok", 0);
        SessionState.SetInt(Clave + "fallos", 0);
        string antes = Huella(Bosque);
        CrearDesafioBlackKnight.Crear();
        string despues = Huella(Bosque);
        Comprobar(antes == despues, "el generador no toca Bosque Cazadora");
        SessionState.SetString(Clave + "Huella", despues);

        FichaJefe f = AssetDatabase.LoadAssetAtPath<FichaJefe>(CrearDesafioBlackKnight.RutaFicha);
        FichaJefe caz = AssetDatabase.LoadAssetAtPath<FichaJefe>("Assets/Resources/Desafios/Jefe_BlindHuntress.asset");
        Comprobar(f != null && f.escena == "Bosque Black Knight" && f.nombre == "Black Knight" && f.imagen != null && !f.secreto,
                  "ficha: escena, nombre, imagen y visible desde el principio");
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CrearBlackKnight.RutaPrefab);
        string[] ids = prefab.GetComponent<JefeBlackKnight>().AtaquesPrueba;
        Comprobar(f != null && ids.All(a => f.BuscarAtaque(a) != null) && f.ataques.Count == ids.Length,
                  $"la ficha tiene los {ids.Length} ataques del jefe con sus ids");
        Comprobar(f != null && f.fases.Count == 3 && f.fragmentosHistoria.Count >= 4, "3 fases e historia por fragmentos");
        Comprobar(f != null && caz != null && f.almasCofre == caz.almasCofre && f.objetosCofre.SequenceEqual(caz.objetosCofre),
                  $"cofre igual que el de la Cazadora: {f?.almasCofre} almas + {string.Join(", ", f?.objetosCofre ?? new Equipo.Objeto[0])}");
        var logros = AssetDatabase.FindAssets("t:FichaLogro", new[] { "Assets/Resources/Logros" })
                                  .Select(g => AssetDatabase.LoadAssetAtPath<FichaLogro>(AssetDatabase.GUIDToAssetPath(g))).ToList();
        foreach (string id in new[] { CrearDesafioBlackKnight.LogroNormal, CrearDesafioBlackKnight.LogroDificil })
            Comprobar(logros.Any(l => l.id == id && l.icono != null && l.parametro == CrearDesafioBlackKnight.Id), "logro " + id);
        Comprobar(EditorBuildSettings.scenes.Any(s => s.path == CrearDesafioBlackKnight.Escena && s.enabled), "la escena en Build Settings");
        ConfigNivel c = AssetDatabase.LoadAssetAtPath<ConfigNivel>(CrearDesafioBlackKnight.RutaConfig);
        Comprobar(c != null && c.jefeFase1.pista == null, "su Config Nivel existe y no tiene musica (la pones tu)");

        SessionState.SetString(Clave + "Modo", "jugar");
        EditorSceneManager.OpenScene("Assets/Scenes/Menu Principal.unity");
        ArranquePruebas.Proteger();
        EditorApplication.EnterPlaymode();
    }

    private class Conductor : MonoBehaviour
    {
        private int errores;

        private IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject);
            AudioListener.volume = 0f;
            Application.logMessageReceived += (c, st, t) =>
            {
                if (t == LogType.Exception || t == LogType.Error) { errores++; Debug.Log("[DESBK] error visto: " + c + "\n" + st); }
            };
            Partida.CarpetaPruebas = Path.Combine(Directory.GetCurrentDirectory(), "PruebaNieve", "PartidasDesafioBlackKnight");
            if (Directory.Exists(Partida.CarpetaPruebas)) Directory.Delete(Partida.CarpetaPruebas, true);
            Directory.CreateDirectory(Partida.CarpetaPruebas);
            Globales.Recargar();
            yield return new WaitForSeconds(1f);

            Comprobar(FichaJefe.Visibles().Any(x => x.id == CrearDesafioBlackKnight.Id), "sale en Desafios desde el principio");
            yield return MenuConSieteJefes();
            yield return Desafiar();

            AudioListener.volume = 0f;
            Comprobar(errores == 0, $"sin errores en la consola ({errores})");
            Partida.Descargar();
            EditorApplication.ExitPlaymode();
        }

        // El menu de Desafios con todos los jefes (la Cazadora desbloqueada solo en la
        // carpeta de pruebas): la fila del Black Knight cabe dentro de la columna.
        private IEnumerator MenuConSieteJefes()
        {
            const System.Reflection.BindingFlags Priv = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            FichaJefe.ForzarSecreto("blind_huntress", true);
            yield return new WaitForSeconds(1f);
            MenuPrincipal m = FindFirstObjectByType<MenuPrincipal>();
            if (m == null) { Fallo("no encuentro el menu principal"); yield break; }
            object panel = typeof(MenuPrincipal).GetField("panelDesafios", Priv)?.GetValue(m);
            typeof(MenuPrincipal).GetMethods(Priv | System.Reflection.BindingFlags.Public).FirstOrDefault(x => x.Name == "Mostrar" && x.GetParameters().Length == 1)?.Invoke(m, new[] { panel });
            yield return new WaitForSecondsRealtime(0.8f);
            MenuDesafios md = (MenuDesafios)typeof(MenuPrincipal).GetField("menuDesafios", Priv)?.GetValue(m);
            System.Collections.IList filas = md != null ? (System.Collections.IList)typeof(MenuDesafios).GetField("filas", Priv).GetValue(md) : null;
            bool esta = md != null && md.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true).Any(t => t.text.Contains("Black Knight"));
            Comprobar(filas != null && filas.Count >= 5 && esta, $"el menu de Desafios lista al Black Knight ({filas?.Count} jefes)");
            Canvas.ForceUpdateCanvases();
            RectTransform lista = md != null ? (RectTransform)((Transform)typeof(MenuDesafios).GetField("listaJefes", Priv).GetValue(md)) : null;
            bool cabe = false;
            if (lista != null && lista.childCount > 0)
            {
                // La ultima fila (la del Black Knight, orden 5... la mas baja) dentro de la pantalla.
                var esquinas = new Vector3[4];
                float minY = float.MaxValue;
                foreach (RectTransform hijo in lista)
                {
                    hijo.GetWorldCorners(esquinas);
                    minY = Mathf.Min(minY, esquinas[0].y);
                }
                Canvas cv = lista.GetComponentInParent<Canvas>();
                Camera cc = cv != null && cv.renderMode != RenderMode.ScreenSpaceOverlay ? cv.worldCamera : null;
                float y = RectTransformUtility.WorldToScreenPoint(cc, new Vector3(0f, minY, 0f)).y;
                cabe = y >= 0f;
                Debug.Log($"[DESBK] INFO la fila mas baja termina en y = {y:0} px de pantalla (0 = borde de abajo)");
            }
            Comprobar(cabe, "todas las filas caben en pantalla");
            yield return Captura("desafio_black_knight_menu");
            typeof(MenuPrincipal).GetMethods(Priv | System.Reflection.BindingFlags.Public).FirstOrDefault(x => x.Name == "Mostrar" && x.GetParameters().Length == 1)?.Invoke(m, new[] { typeof(MenuPrincipal).GetField("panelPrincipal", Priv)?.GetValue(m) });
            FichaJefe.ForzarSecreto("blind_huntress", false);
            yield return new WaitForSecondsRealtime(0.5f);
        }

        private IEnumerator Desafiar()
        {
            FichaJefe f = FichaJefe.Todas().First(x => x.id == CrearDesafioBlackKnight.Id);
            Desafio.Empezar(f, false);
            for (float t = 0f; t < 30f && (SceneManager.GetActiveScene().name != f.escena || PantallaCarga.Cargando); t += Time.unscaledDeltaTime) yield return null;
            Comprobar(SceneManager.GetActiveScene().name == f.escena, $"carga su escena ({f.escena})");
            yield return new WaitForSeconds(1.5f);
            AudioListener.volume = 0f;
            PlayerControler p = FindFirstObjectByType<PlayerControler>();
            int hogueras = FindObjectsByType<Hoguera>(FindObjectsSortMode.None).Length;
            ArenaJefe arena = FindObjectsByType<ArenaJefe>(FindObjectsSortMode.None).FirstOrDefault(a => a.EsPrincipal);
            Comprobar(p != null && hogueras == 1 && arena != null && FindFirstObjectByType<ModoDesafio>() != null,
                      $"player, una hoguera ({hogueras}) y la arena del jefe");
            Comprobar(FindFirstObjectByType<ArenaCazadora>() == null && arena != null && !arena.EsDuo, "una arena normal, sin nada de la Cazadora");
            if (p == null || arena == null) yield break;
            yield return Captura("desafio_black_knight_hoguera");

            p.InvulnerableExterno = true;
            arena.EmpezarDesdeZona(p);
            yield return new WaitForSeconds(3f);
            JefeBlackKnight j = FindFirstObjectByType<JefeBlackKnight>();
            Comprobar(j != null && FindObjectsByType<BarraJefe>(FindObjectsSortMode.None).Length == 1, "aparece el Black Knight con una barra");
            if (j == null) yield break;
            float suelo = new SerializedObject(arena).FindProperty("suelo").floatValue;
            Comprobar(Mathf.Abs(j.transform.position.y - suelo) < 1f, $"aparece en el suelo de la arena (y = {j.transform.position.y:0.0}, suelo {suelo:0.0})");
            EnemyHealth s = j.GetComponent<EnemyHealth>();
            Comprobar(s.MaxHealth == j.Ajustes.vidaFase1, $"fase 1 con {s.MaxHealth} de vida");
            yield return Captura("desafio_black_knight_pelea");

            // Fase 2: barra nueva (la misma, de otro color).
            s.DanoEstado(s.CurrentHealth + 10);
            for (float t = 0f; t < 8f && (j.Fase < 2 || j.Cambiando); t += Time.deltaTime) yield return null;
            Comprobar(j.Fase == 2 && s.MaxHealth == j.Ajustes.vidaFase2 && FindObjectsByType<BarraJefe>(FindObjectsSortMode.None).Length == 1,
                      $"fase 2 con su barra llena ({s.CurrentHealth}/{s.MaxHealth}) y una sola barra");
            // La camara se abre cuando se aleja (ondas).
            j.ForzarAtaque(JefeBlackKnight.AccOndas);
            for (float t = 0f; t < 15f && !(j.EnOndas && !j.Retirandose); t += Time.deltaTime) yield return null;
            yield return new WaitForSeconds(1.5f);
            Camera cam = Camera.main;
            Vector3 v = cam != null ? cam.WorldToViewportPoint(j.transform.position) : Vector3.zero;
            Vector3 vp = cam != null ? cam.WorldToViewportPoint(p.transform.position) : Vector3.zero;
            float dx = Mathf.Abs(j.transform.position.x - p.transform.position.x);
            Comprobar(j.CamaraAmplia && v.x > 0f && v.x < 1f && vp.x > 0f && vp.x < 1f && dx <= j.Ajustes.distanciaAlejarse + 1.5f,
                      $"ondas en el bosque: se aleja {dx:0.0} u, la camara se abre (tamano {(cam != null ? cam.orthographicSize : 0f):0.0}) y se ven los dos (jefe {v.x:0.00}, tu {vp.x:0.00})");
            yield return Captura("desafio_black_knight_ondas");

            // Fase 3.
            s.DanoEstado(s.CurrentHealth + 10);
            for (float t = 0f; t < 8f && (j.Fase < 3 || j.Cambiando); t += Time.deltaTime) yield return null;
            Comprobar(j.Fase == 3 && s.MaxHealth == j.Ajustes.vidaFase3 && FindObjectsByType<BarraJefe>(FindObjectsSortMode.None).Length == 1,
                      $"fase 3 con su tercera barra ({s.CurrentHealth}/{s.MaxHealth})");
            yield return new WaitForSeconds(1f);
            yield return Captura("desafio_black_knight_fase3");

            s.DanoEstado(s.CurrentHealth + 10);
            for (float t = 0f; t < 10f && !Desafio.Completado; t += Time.unscaledDeltaTime) yield return null;
            Comprobar(Desafio.Completado && Globales.Completado(CrearDesafioBlackKnight.Id, false), "desafio completado");
            yield return new WaitForSeconds(1f);
            Comprobar(Globales.TieneLogro(CrearDesafioBlackKnight.LogroNormal), "logro " + CrearDesafioBlackKnight.LogroNormal);
            yield return Captura("desafio_black_knight_victoria");
            Desafio.Salir();
            for (float t = 0f; t < 20f && (SceneManager.GetActiveScene().name != "Menu Principal" || PantallaCarga.Cargando); t += Time.unscaledDeltaTime) yield return null;
            yield return new WaitForSeconds(1f);
        }

        // Lo que ve la camara, con la interfaz, en 1280x720.
        private IEnumerator Captura(string nombre)
        {
            yield return null;
            Camera cam = Camera.main;
            if (cam == null) yield break;
            var cambiados = new System.Collections.Generic.List<Canvas>();
            foreach (Canvas c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay && c.gameObject.activeInHierarchy)
                { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = cam; c.planeDistance = 1f; c.sortingLayerName = "VFX"; c.sortingOrder += 500; cambiados.Add(c); }
            RenderTexture rt = new RenderTexture(1280, 720, 24);
            cam.targetTexture = rt;
            for (int i = 0; i < 2; i++) { Canvas.ForceUpdateCanvases(); yield return null; }
            cam.Render();
            cam.targetTexture = null;
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(), "PruebaNieve", nombre + ".png"), tex.EncodeToPNG());
            rt.Release();
            Destroy(tex);
            foreach (Canvas c in cambiados) { c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder -= 500; }
        }
    }
}
