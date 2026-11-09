using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Prueba de la pagina Amuletos de la hoguera (batchmode, sin -quit):
//   ... -executeMethod PruebaAmuletos.Todo
// Opcion "Amuletos" en el menu, 4 huecos equipados (2 libres y 2 bloqueados),
// coleccion ordenada con "?", ficha de la derecha, nada fuera de pantalla ni
// pisado, y vuelta atras. Capturas en PruebaNieve/amuletos_*.png. En silencio.
[InitializeOnLoad]
public static class PruebaAmuletos
{
    private const string Clave = "PruebaAmuletos.Modo";

    static PruebaAmuletos()
    {
        EditorApplication.playModeStateChanged += e =>
        {
            if (SessionState.GetString(Clave, "") == "") return;
            if (e == PlayModeStateChange.EnteredPlayMode) new GameObject("PruebaAmuletos").AddComponent<Conductor>();
            else if (e == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetString(Clave, "");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
        };
    }

    public static void Todo()
    {
        SessionState.SetString(Clave, "si");
        EditorSceneManager.OpenScene("Assets/Scenes/Nivel Nieve.unity");
        ArranquePruebas.Proteger();
        EditorApplication.EnterPlaymode();
    }

    private class Conductor : MonoBehaviour
    {
        private int ok, fallos, errores;
        private void Comprobar(bool c, string s) { if (c) { ok++; Debug.Log("[AMULETOS] OK " + s); } else { fallos++; Debug.LogWarning("[AMULETOS] FALLO " + s); } }
        private static readonly BindingFlags Priv = BindingFlags.NonPublic | BindingFlags.Instance;

        private IEnumerator Start()
        {
            AudioListener.volume = 0f;
            Application.logMessageReceived += (c, st, t) => { if (t == LogType.Exception || t == LogType.Error) { errores++; Debug.Log("[AMULETOS] error visto: " + c + "\n" + st); } };
            string carpeta = Path.Combine(Directory.GetCurrentDirectory(), "PruebaNieve");
            yield return new WaitForSecondsRealtime(2f);

            MenuHoguera.Abrir();
            yield return new WaitForSecondsRealtime(0.5f);
            MenuHoguera m = FindFirstObjectByType<MenuHoguera>();
            var opciones = m.GetComponentsInChildren<TextMeshProUGUI>(false).Select(t => t.text).ToList();
            Comprobar(opciones.Contains("Amuletos"), "la hoguera tiene la opcion Amuletos");
            yield return Captura(carpeta, "amuletos_hoguera", new Vector2Int(1920, 1080), null);

            System.Type tp = typeof(MenuHoguera).GetNestedType("Pagina", BindingFlags.NonPublic);
            MethodInfo mostrar = typeof(MenuHoguera).GetMethod("Mostrar", Priv);
            mostrar.Invoke(m, new[] { System.Enum.Parse(tp, "Amuletos") });
            yield return new WaitForSecondsRealtime(0.3f);
            AmuletosHoguera a = FindFirstObjectByType<AmuletosHoguera>();
            Comprobar(a != null && a.Pagina.gameObject.activeInHierarchy, "se abre la pagina de amuletos");
            var huecos = a.GetComponentsInChildren<HuecoAmuleto>();
            int libres = huecos.Count(h => h.tipo == AmuletosHoguera.Tipo.Libre), bloq = huecos.Count(h => h.tipo == AmuletosHoguera.Tipo.Bloqueado),
                desc = huecos.Count(h => h.tipo == AmuletosHoguera.Tipo.Desconocido);
            Comprobar(libres == 2 && bloq == 2, $"equipados: {libres} libres y {bloq} bloqueados");
            Comprobar(desc == AmuletosHoguera.TotalAmuletos && huecos.Where(h => h.tipo == AmuletosHoguera.Tipo.Desconocido)
                     .All(h => h.GetComponentInChildren<TextMeshProUGUI>().text == "?"), $"coleccion: {desc} amuletos con '?'");
            Comprobar(EventSystem.current.currentSelectedGameObject == a.Primero && a.Elegido != null && a.Elegido.tipo == AmuletosHoguera.Tipo.Libre,
                      "al entrar esta elegido el primer hueco libre (ficha: " + a.Elegido?.Titulo + ")");

            // Filas ordenadas: misma altura por fila y misma separacion.
            var col = huecos.Where(h => h.tipo == AmuletosHoguera.Tipo.Desconocido).Select(h => ((RectTransform)h.transform).anchoredPosition).ToList();
            int filas = col.Select(p => Mathf.RoundToInt(p.y)).Distinct().Count();
            Comprobar(filas == AmuletosHoguera.Filas, $"coleccion ordenada en {filas} filas de {AmuletosHoguera.Columnas}");

            foreach (Vector2Int res in new[] { new Vector2Int(1920, 1080), new Vector2Int(1280, 720), new Vector2Int(2560, 1080), new Vector2Int(1024, 768) })
                yield return Captura(carpeta, $"amuletos_{res.x}x{res.y}", res, a);

            // Elegir un amuleto desconocido y uno bloqueado.
            HuecoAmuleto d = huecos.First(h => h.tipo == AmuletosHoguera.Tipo.Desconocido);
            EventSystem.current.SetSelectedGameObject(d.gameObject);
            yield return null;
            Comprobar(a.Elegido == d && d.EstaElegido, "elegir un desconocido: " + a.Elegido.Titulo);
            HuecoAmuleto b = huecos.First(h => h.tipo == AmuletosHoguera.Tipo.Bloqueado);
            EventSystem.current.SetSelectedGameObject(b.gameObject);
            yield return null;
            Comprobar(a.Elegido == b, "elegir un bloqueado: " + a.Elegido.Titulo);
            yield return Captura(carpeta, "amuletos_bloqueado", new Vector2Int(1920, 1080), null);

            mostrar.Invoke(m, new[] { System.Enum.Parse(tp, "Principal") });
            yield return null;
            Comprobar(!a.Pagina.gameObject.activeInHierarchy, "volver deja la pagina principal");
            Comprobar(errores == 0, $"sin errores en la consola ({errores})");
            Debug.Log($"[AMULETOS] RESULTADO: {ok} OK, {fallos} FALLOS");
            EditorApplication.ExitPlaymode();
        }

        // Captura y, si hay pagina, comprueba que cada hueco y la ficha caben y no se pisan.
        private IEnumerator Captura(string carpeta, string nombre, Vector2Int res, AmuletosHoguera a)
        {
            Camera cam = Camera.main;
            RenderTexture rt = new RenderTexture(res.x, res.y, 24);
            cam.targetTexture = rt;
            var cambiados = new System.Collections.Generic.List<Canvas>();
            foreach (Canvas c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay && c.gameObject.activeInHierarchy)
                { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = cam; c.planeDistance = 1f; c.sortingLayerName = "VFX"; c.sortingOrder += 500; cambiados.Add(c); }
            for (int i = 0; i < 3; i++) { Canvas.ForceUpdateCanvases(); yield return null; }
            if (a != null)
            {
                Rect R(RectTransform r)
                {
                    Vector3[] w = new Vector3[4];
                    r.GetWorldCorners(w);
                    Vector2 p0 = RectTransformUtility.WorldToScreenPoint(cam, w[0]), p1 = RectTransformUtility.WorldToScreenPoint(cam, w[2]);
                    return Rect.MinMaxRect(Mathf.Min(p0.x, p1.x), Mathf.Min(p0.y, p1.y), Mathf.Max(p0.x, p1.x), Mathf.Max(p0.y, p1.y));
                }
                var rects = a.GetComponentsInChildren<HuecoAmuleto>().Select(h => { h.transform.localScale = Vector3.one; return R((RectTransform)h.transform); }).ToList();
                bool dentro = rects.All(r => r.xMin >= 0 && r.yMin >= 0 && r.xMax <= res.x && r.yMax <= res.y);
                int pisados = 0;
                for (int i = 0; i < rects.Count; i++) for (int j = i + 1; j < rects.Count; j++) if (rects[i].Overlaps(rects[j])) pisados++;
                Rect panel = R((RectTransform)a.transform);
                bool enPanel = rects.All(r => panel.Contains(r.min) && panel.Contains(r.max));
                Comprobar(dentro && enPanel && pisados == 0, $"{res.x}x{res.y}: los 44 huecos dentro del panel y de la pantalla, sin pisarse ({pisados})");
                if (a.Elegido != null) a.Elegido.transform.localScale = Vector3.one * 1.12f;
            }
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
