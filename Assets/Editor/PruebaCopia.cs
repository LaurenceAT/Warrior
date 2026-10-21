using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Prueba de la copia del progreso (descargar / cargar), linea de comandos (sin -quit):
//   ... -executeMethod PruebaCopia.Todo
// Escribe "[COPIA] OK/FALLO" y capturas de Opciones en PruebaNieve/copia_*.png.
// Trabaja en su propia carpeta: nunca toca el progreso real.
[InitializeOnLoad]
public static class PruebaCopia
{
    private const string Clave = "PruebaCopia.Modo";

    static PruebaCopia()
    {
        EditorApplication.playModeStateChanged += e =>
        {
            if (SessionState.GetString(Clave, "") == "") return;
            if (e == PlayModeStateChange.EnteredPlayMode) new GameObject("PruebaCopia").AddComponent<Conductor>();
            else if (e == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetString(Clave, "");
                SessionState.SetBool(CopiaProgreso.ClaveForzarBrave, false);
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
        };
    }

    public static void Todo()
    {
        SessionState.SetString(Clave, "todo");
        EditorSceneManager.OpenScene("Assets/Scenes/Menu Principal.unity");
        ArranquePruebas.Proteger();
        SessionState.SetBool(CopiaProgreso.ClaveForzarBrave, true);
        EditorApplication.EnterPlaymode();
    }

    private class Conductor : MonoBehaviour
    {
        private int ok, fallos, errores;
        private void Comprobar(bool c, string s) { if (c) { ok++; Debug.Log("[COPIA] OK " + s); } else { fallos++; Debug.LogWarning("[COPIA] FALLO " + s); } }

        private IEnumerator Start()
        {
            Application.logMessageReceived += (c, st, t) => { if (t == LogType.Exception || t == LogType.Error) { errores++; Debug.Log("[COPIA] error visto: " + c); } };
            string carpeta = Path.Combine(Directory.GetCurrentDirectory(), "PruebaNieve");
            Partida.CarpetaPruebas = Path.Combine(carpeta, "PartidasCopia");
            if (Directory.Exists(Partida.CarpetaPruebas)) Directory.Delete(Partida.CarpetaPruebas, true);
            Directory.CreateDirectory(Partida.CarpetaPruebas);
            Globales.Recargar();
            yield return new WaitForSecondsRealtime(1f);

            string dir = RegistroGuardado.Carpeta;
            Comprobar(dir == Partida.CarpetaPruebas, "trabaja en la carpeta de pruebas");
            File.WriteAllText(Path.Combine(dir, "partida_3.json"), "{\"ranura\":3,\"escena\":\"Nivel Nieve\",\"almas\":777}");
            Globales.PonerMarca("prueba_copia", true);
            Globales.PonerMarca(GuiaDesafio.Marca, true);

            string copia = CopiaProgreso.Crear();
            Comprobar(copia.Contains("partida_3.json") && copia.Contains(RegistroGuardado.ArchivoGlobales) && copia.Contains("prueba_copia"),
                      "la copia lleva las partidas y globales.json");
            Comprobar(CopiaProgreso.NombreArchivo.StartsWith("Warrior_progreso_") && CopiaProgreso.NombreArchivo.EndsWith(".json"), "nombre del archivo: " + CopiaProgreso.NombreArchivo);

            // Se pierde todo y se pone otra cosa...
            File.Delete(Path.Combine(dir, "partida_3.json"));
            File.WriteAllText(Path.Combine(dir, "partida_4.json"), "{\"ranura\":4}");
            Globales.PonerMarca("prueba_copia", false);
            Globales.PonerMarca(GuiaDesafio.Marca, false);
            // ... y la copia lo devuelve tal cual.
            string error = CopiaProgreso.Cargar(copia);
            Comprobar(error == null, "la copia se carga sin errores " + error);
            Comprobar(File.Exists(Path.Combine(dir, "partida_3.json")) && File.ReadAllText(Path.Combine(dir, "partida_3.json")).Contains("777"), "vuelve la partida de la copia");
            Comprobar(!File.Exists(Path.Combine(dir, "partida_4.json")), "lo de despues de la copia se sustituye");
            Comprobar(Globales.Marca("prueba_copia") && Globales.Marca(GuiaDesafio.Marca), "vuelven las marcas de globales.json (desafios, guia vista...)");
            Comprobar(Partida.Listar().Count == 1, "el menu Cargar ve la partida recuperada");

            // Archivos que no son copias: se rechazan sin tocar nada.
            Comprobar(CopiaProgreso.Cargar("hola") != null, "un archivo cualquiera se rechaza");
            Comprobar(CopiaProgreso.Cargar("{\"juego\":\"Otro\",\"version\":1,\"archivos\":[]}") != null, "una copia de otro juego se rechaza");
            Comprobar(CopiaProgreso.Cargar("{\"juego\":\"Warrior\",\"version\":1,\"archivos\":[{\"nombre\":\"../malo.json\",\"contenido\":\"x\"}]}") != null,
                      "un nombre de archivo peligroso se rechaza");
            Comprobar(File.Exists(Path.Combine(dir, "partida_3.json")), "tras rechazar, el progreso sigue intacto");

            // Los botones de la copia: en el menu principal, abajo a la derecha.
            MenuPrincipal m = FindFirstObjectByType<MenuPrincipal>();
            BindingFlags todo = BindingFlags.NonPublic | BindingFlags.Instance;
            GameObject principal = (GameObject)typeof(MenuPrincipal).GetField("panelPrincipal", todo).GetValue(m);
            GameObject opciones = (GameObject)typeof(MenuPrincipal).GetField("panelOpciones", todo).GetValue(m);
            yield return new WaitForSecondsRealtime(0.5f);
            Comprobar(principal.GetComponentsInChildren<TextMeshProUGUI>().Any(t => t.text == "Descargar mi progreso")
                      && principal.GetComponentsInChildren<TextMeshProUGUI>().Any(t => t.text == "Cargar mi progreso"), "el menu principal tiene los dos botones");
            Comprobar(!opciones.GetComponentsInChildren<TextMeshProUGUI>(true).Any(t => t.text.Contains("mi progreso")), "ya no estan en Opciones");
            TextMeshProUGUI brave = principal.GetComponentsInChildren<TextMeshProUGUI>().FirstOrDefault(t => t.name == "AvisoBrave");
            Comprobar(brave != null && brave.text.EndsWith("o guarda una copia."), "aviso de Brave con el texto nuevo");
            foreach (Vector2Int res in new[] { new Vector2Int(1920, 1080), new Vector2Int(1920, 1200), new Vector2Int(2560, 1080), new Vector2Int(1280, 720), new Vector2Int(1024, 768), new Vector2Int(800, 600) })
                yield return Captura(carpeta, $"copia_{res.x}x{res.y}", res, principal);

            Comprobar(errores == 0, $"sin errores en la consola ({errores})");
            Debug.Log($"[COPIA] RESULTADO: {ok} OK, {fallos} FALLOS");
            Partida.Descargar();
            EditorApplication.ExitPlaymode();
        }

        // Captura y comprueba que todo lo de Opciones queda dentro de la pantalla.
        private IEnumerator Captura(string carpeta, string nombre, Vector2Int res, GameObject panel)
        {
            Camera cam = Camera.main;
            RenderTexture rt = new RenderTexture(res.x, res.y, 24);
            cam.targetTexture = rt;
            var cambiados = new System.Collections.Generic.List<Canvas>();
            foreach (Canvas c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay && c.gameObject.activeInHierarchy)
                { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = cam; c.planeDistance = 1f; c.sortingLayerName = "VFX"; c.sortingOrder += 500; cambiados.Add(c); }
            for (int i = 0; i < 3; i++) { Canvas.ForceUpdateCanvases(); yield return null; }
            Rect R(RectTransform r)
            {
                Vector3[] w = new Vector3[4];
                r.GetWorldCorners(w);
                Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, w[0]), b = RectTransformUtility.WorldToScreenPoint(cam, w[2]);
                return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
            }
            var botones = panel.GetComponentsInChildren<TextMeshProUGUI>().Where(t => t.text.Contains("mi progreso")).Select(t => R(t.rectTransform)).ToList();
            TextMeshProUGUI aviso = panel.GetComponentsInChildren<TextMeshProUGUI>().FirstOrDefault(t => t.name == "AvisoBrave");
            var otros = panel.transform.root.GetComponentsInChildren<TextMeshProUGUI>()
                .Where(t => !t.text.Contains("mi progreso") && t.name != "AvisoBrave" && t.text.Length > 0 && t.gameObject.activeInHierarchy).Select(t => (t.text, R(t.rectTransform))).ToList();
            if (aviso != null)
            {
                aviso.ForceMeshUpdate();
                Rect ra = R(aviso.rectTransform);
                bool encima = botones.All(b => ra.yMin >= b.yMax - 1f) && ra.center.x > res.x * 0.55f && ra.xMax <= res.x && ra.yMax <= res.y;
                bool cabe = aviso.preferredHeight <= aviso.rectTransform.rect.height + 1f;
                // Su parte con letras (derecha) no pisa otros textos.
                Rect letras = Rect.MinMaxRect(ra.xMax - Mathf.Min(ra.width, aviso.preferredWidth * ra.width / aviso.rectTransform.rect.width), ra.yMin, ra.xMax, ra.yMax);
                var pisaAviso = otros.Where(o => letras.Overlaps(o.Item2)).Select(o => o.Item1).ToList();
                Comprobar(encima && cabe, $"{res.x}x{res.y}: aviso de Brave encima de los botones, sin cortarse");
                Comprobar(pisaAviso.Count == 0, $"{res.x}x{res.y}: el aviso no pisa otros textos " + string.Join(" | ", pisaAviso));
            }
            bool dentro = botones.All(b => b.xMin >= 0 && b.yMin >= 0 && b.xMax <= res.x + 1 && b.yMax <= res.y);
            bool esquina = botones.All(b => b.center.x > res.x * 0.6f && b.center.y < res.y * 0.25f);
            // El texto de un boton ocupa todo su ancho: se mira solo la parte con letras (derecha).
            var pisa = otros.Where(o => botones.Any(b => Rect.MinMaxRect(b.xMax - b.width * 0.55f, b.yMin, b.xMax, b.yMax).Overlaps(o.Item2))).Select(o => o.Item1).ToList();
            Comprobar(dentro && esquina, $"{res.x}x{res.y}: abajo a la derecha y dentro de la pantalla");
            Comprobar(pisa.Count == 0, $"{res.x}x{res.y}: no pisan otros textos " + string.Join(" | ", pisa));
            cam.Render();
            cam.targetTexture = null;
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(res.x, res.y, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, res.x, res.y), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(carpeta, nombre + ".png"), tex.EncodeToPNG());
            rt.Release();
            foreach (Canvas c in cambiados) { c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder -= 500; }
        }
    }
}
