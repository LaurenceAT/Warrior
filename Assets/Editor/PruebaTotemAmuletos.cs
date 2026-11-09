using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

// Prueba del totem con amuletos (batchmode, sin -quit):
//   ... -executeMethod PruebaTotemAmuletos.Todo
// Warrior > Amuletos > Crear o actualizar; en el desafio de Crimson Wraith:
// pestanas Objetos / Amuletos, comprar amuletos (1 alma) y el Vestigio del
// Vacio, aplicarlo en la hoguera (3 huecos), equipar hasta llenar, quitar uno,
// y que todo se pierde al salir. Capturas en PruebaNieve/totem_*.png. En silencio
// y sin tocar el progreso real.
[InitializeOnLoad]
public static class PruebaTotemAmuletos
{
    private const string Clave = "PruebaTotemAmuletos.Modo";

    static PruebaTotemAmuletos()
    {
        EditorApplication.playModeStateChanged += e =>
        {
            if (SessionState.GetString(Clave, "") == "") return;
            if (e == PlayModeStateChange.EnteredPlayMode) new GameObject("PruebaTotemAmuletos").AddComponent<Conductor>();
            else if (e == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetString(Clave, "");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
        };
    }

    public static void Todo()
    {
        CrearAmuletos.Crear();
        SessionState.SetString(Clave, "si");
        EditorSceneManager.OpenScene("Assets/Scenes/Menu Principal.unity");
        ArranquePruebas.Proteger();
        EditorApplication.EnterPlaymode();
    }

    private class Conductor : MonoBehaviour
    {
        private int ok, fallos, errores;
        private void Comprobar(bool c, string s) { if (c) { ok++; Debug.Log("[TOTEM] OK " + s); } else { fallos++; Debug.LogWarning("[TOTEM] FALLO " + s); } }
        private static readonly BindingFlags Priv = BindingFlags.NonPublic | BindingFlags.Instance;
        private string carpeta;

        private IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject);
            AudioListener.volume = 0f;
            Application.logMessageReceived += (c, st, t) => { if (t == LogType.Exception || t == LogType.Error) { errores++; Debug.Log("[TOTEM] error visto: " + c + "\n" + st); } };
            carpeta = Path.Combine(Directory.GetCurrentDirectory(), "PruebaNieve");
            Partida.CarpetaPruebas = Path.Combine(carpeta, "PartidasTotem");
            if (Directory.Exists(Partida.CarpetaPruebas)) Directory.Delete(Partida.CarpetaPruebas, true);
            Directory.CreateDirectory(Partida.CarpetaPruebas);
            Globales.Recargar();
            yield return new WaitForSecondsRealtime(1f);

            FichaAmuletos fa = FichaAmuletos.Get();
            Comprobar(fa != null && fa.amuletos.Count == 40 && fa.amuletos.All(a => a.icono != null && !string.IsNullOrEmpty(a.nombre) && a.precio == CrearAmuletos.Precios[a.id]),
                      $"40 amuletos con imagen, nombre y su precio ({fa?.amuletos.Count}; de {fa?.amuletos.Min(a => a.precio)} a {fa?.amuletos.Max(a => a.precio)} almas)");
            Comprobar(RecursosRPG.Get().Icono("objeto_vestigio") != null, "el Vestigio del Vacío tiene icono");
            Comprobar(fa.amuletos.Count(x => x.id == "amuleto_00" || x.id == "amuleto_01") == 2 && fa.amuletos.Count(x => x.id.StartsWith("amuleto_u")) == 38,
                      "2 anillos (cristal rojo y azul) y 38 del pack OBJETOS Y AMULETOS");

            FichaJefe f = FichaJefe.Todas().First(x => x.id == "crimson_wraith");
            Desafio.Empezar(f, false);
            for (float t = 0f; t < 30f && (SceneManager.GetActiveScene().name != f.escena || PantallaCarga.Cargando); t += Time.unscaledDeltaTime) yield return null;
            yield return new WaitForSecondsRealtime(1.5f);
            AudioListener.volume = 0f;
            Progreso.SumarAlmas(10000);
            PlayerControler p = FindFirstObjectByType<PlayerControler>();

            // ---- Totem: Objetos.
            TiendaTotem.Abrir(p);
            yield return new WaitForSecondsRealtime(1.2f);
            Comprobar(TiendaTotem.Abierta && TiendaTotem.SeccionActual == TiendaTotem.Seccion.Objetos, "el tótem abre en la pestaña Objetos");
            Comprobar(GameObject.Find("Pestana_0") != null && GameObject.Find("Pestana_1") != null, "dos pestañas: Objetos y Amuletos");
            Comprobar(GameObject.Find("Articulo_vestigio_1") != null && GameObject.Find("Articulo_vestigio_1").activeInHierarchy, "el Vestigio del Vacío está en Objetos");
            Comprobar(GameObject.Find("Articulo_amuleto_00") == null || !GameObject.Find("Articulo_amuleto_00").activeInHierarchy, "los amuletos no se ven en Objetos");
            yield return Captura("totem_objetos");

            Elegir("Articulo_vestigio_1");
            yield return null;
            TiendaTotem.ComprarElegido();
            Comprobar(Desafio.PorAplicar(Equipo.Objeto.VestigioVacio) == 1, "comprar el Vestigio: va al inventario por aplicar");

            // ---- Totem: Amuletos.
            TiendaTotem.IrA(TiendaTotem.Seccion.Amuletos);
            yield return null;
            Comprobar(TiendaTotem.SeccionActual == TiendaTotem.Seccion.Amuletos && GameObject.Find("Articulo_amuleto_00").activeInHierarchy, "pestaña Amuletos: se ven los amuletos");
            Comprobar(GameObject.Find("Articulo_vestigio_1") == null || !GameObject.Find("Articulo_vestigio_1").activeInHierarchy, "los objetos no se ven en Amuletos");
            int almas = Progreso.Almas;
            string[] compra = { "amuleto_00", "amuleto_01", "amuleto_u12", "amuleto_u29" };
            foreach (string id in compra)
            {
                Elegir("Articulo_" + id);
                yield return null;
                TiendaTotem.ComprarElegido();
            }
            Comprobar(compra.All(Amuletos.Tiene) && Amuletos.Conseguidos == 4, "comprar 4 amuletos: son tuyos al momento");
            int total = compra.Sum(id => fa.Buscar(id).precio);
            Comprobar(Progreso.Almas == almas - total, $"cada amuleto cuesta su precio ({almas} -> {Progreso.Almas}, {total} almas)");
            Elegir("Articulo_amuleto_00");
            yield return null;
            int antes = Progreso.Almas;
            TiendaTotem.ComprarElegido();
            Comprobar(Progreso.Almas == antes, "no se puede comprar dos veces el mismo");
            Elegir("Articulo_amuleto_u12");
            yield return Captura("totem_amuletos");
            TiendaTotem.Cerrar();
            yield return new WaitForSecondsRealtime(0.4f);

            // ---- Hoguera: aplicar el Vestigio y equipar.
            Comprobar(Amuletos.HuecosAbiertos == 2, "al empezar hay 2 huecos abiertos");
            MenuHoguera.Abrir();
            yield return new WaitForSecondsRealtime(0.5f);
            Comprobar(Inventario.Aplicar(Equipo.Objeto.VestigioVacio) && Amuletos.HuecosAbiertos == 3, "aplicar el Vestigio en la hoguera abre un tercer hueco");
            MenuHoguera m = FindFirstObjectByType<MenuHoguera>();
            System.Type tp = typeof(MenuHoguera).GetNestedType("Pagina", BindingFlags.NonPublic);
            MethodInfo mostrar = typeof(MenuHoguera).GetMethod("Mostrar", Priv);
            mostrar.Invoke(m, new[] { System.Enum.Parse(tp, "Amuletos") });
            yield return new WaitForSecondsRealtime(0.3f);
            AmuletosHoguera pag = FindFirstObjectByType<AmuletosHoguera>();
            var huecos = pag.GetComponentsInChildren<HuecoAmuleto>();
            HuecoAmuleto H(string id) => huecos.First(h => !h.equipo && h.Amuleto != null && h.Amuleto.id == id);
            Comprobar(compra.All(id => H(id).tipo == AmuletosHoguera.Tipo.Conseguido && H(id).imagen.enabled), "en la colección se ven los comprados con su imagen");
            Comprobar(huecos.Count(h => !h.equipo && h.tipo == AmuletosHoguera.Tipo.Desconocido) == 36, "los demás siguen con '?'");
            Comprobar(huecos.Count(h => h.equipo && h.tipo == AmuletosHoguera.Tipo.Bloqueado) == 1, "queda 1 hueco bloqueado");
            foreach (string id in compra) H(id).Pulsar();
            Comprobar(Amuletos.Equipado(0) == compra[0] && Amuletos.Equipado(1) == compra[1] && Amuletos.Equipado(2) == compra[2] && Amuletos.HuecoDe(compra[3]) < 0,
                      "se equipan en orden hasta llenar los 3 huecos; el cuarto no cabe");
            HuecoAmuleto primero = huecos.First(h => h.equipo && h.indice == 0);
            primero.Pulsar();
            Comprobar(string.IsNullOrEmpty(Amuletos.Equipado(0)) && H(compra[0]).tipo == AmuletosHoguera.Tipo.Conseguido, "pulsar un hueco puesto lo quita");
            H(compra[3]).Pulsar();
            Comprobar(Amuletos.Equipado(0) == compra[3], "ahora el cuarto entra en el hueco libre");
            EventSystem.current.SetSelectedGameObject(H(compra[1]).gameObject);
            yield return null;
            Comprobar(pag.Elegido != null && pag.Elegido.Titulo == fa.Buscar(compra[1]).nombre, "la ficha muestra el nombre: " + pag.Elegido?.Titulo);
            yield return Captura("totem_hoguera_amuletos");
            mostrar.Invoke(m, new[] { System.Enum.Parse(tp, "Principal") });

            // ---- Al salir del desafio, nada se queda.
            Desafio.Salir();
            for (float t = 0f; t < 20f && (SceneManager.GetActiveScene().name != "Menu Principal" || PantallaCarga.Cargando); t += Time.unscaledDeltaTime) yield return null;
            Comprobar(Amuletos.Conseguidos == 0 && Amuletos.HuecosAbiertos == 2 && string.IsNullOrEmpty(Amuletos.Equipado(0)), "al salir del desafío se pierden (como su inventario)");

            Comprobar(errores == 0, $"sin errores en la consola ({errores})");
            Debug.Log($"[TOTEM] RESULTADO: {ok} OK, {fallos} FALLOS");
            Partida.Descargar();
            EditorApplication.ExitPlaymode();
        }

        private static void Elegir(string nombre)
        {
            GameObject g = GameObject.Find(nombre);
            if (g != null && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(g);
        }

        private IEnumerator Captura(string nombre)
        {
            Camera cam = Camera.main;
            if (cam == null) yield break;
            var cambiados = new System.Collections.Generic.List<Canvas>();
            foreach (Canvas c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay && c.gameObject.activeInHierarchy)
                { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = cam; c.planeDistance = 1f; c.sortingLayerName = "VFX"; c.sortingOrder += 500; cambiados.Add(c); }
            RenderTexture rt = new RenderTexture(1920, 1080, 24);
            cam.targetTexture = rt;
            for (int i = 0; i < 2; i++) { Canvas.ForceUpdateCanvases(); yield return null; }
            cam.Render();
            cam.targetTexture = null;
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(carpeta, nombre + ".png"), tex.EncodeToPNG());
            rt.Release();
            Destroy(tex);
            foreach (Canvas c in cambiados) { c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder -= 500; }
        }
    }
}
