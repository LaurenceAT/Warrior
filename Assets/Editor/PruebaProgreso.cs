using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Pruebas de la Ronda 16 (linea de comandos):
//   Unity -batchmode -projectPath ... -executeMethod PruebaProgreso.Herramientas -quit
//     Borrar por partes, borrar todo, respaldo, restaurar y perfil de pruebas.
//     Trabaja con "Simular jugador nuevo" activado: el progreso real no se toca
//     (se comprueba con su huella antes y despues).
//   Unity -batchmode -projectPath ... -executeMethod PruebaProgreso.Codigo
//     El codigo secreto en el menu de Desafios (con un teclado virtual).
// Escribe "[Progreso] OK ..." / "[Progreso] FALLO ..." y el total.
[InitializeOnLoad]
public static class PruebaProgreso
{
    private static int ok, fallos;
    private static void Comprobar(bool c, string que) { if (c) { ok++; Debug.Log("[Progreso] OK " + que); } else { fallos++; Debug.LogWarning("[Progreso] FALLO " + que); } }

    // ------------------------------------------------------------------ Herramientas

    public static void Herramientas()
    {
        ok = fallos = 0;
        string real = RegistroGuardado.CarpetaDe(false);
        string huellaReal = Huella(real);
        string prefsReal = PlayerPrefs.GetInt("rpg_almas", -1) + "|" + PlayerPrefs.GetFloat("volGeneral", -1f);
        bool perfilAntes = EditorPrefs.GetBool(RegistroGuardado.ClaveEditorPerfil, false);
        int respaldosAntes = Directory.Exists(ProgresoJuego.CarpetaRespaldos) ? Directory.GetDirectories(ProgresoJuego.CarpetaRespaldos).Length : 0;
        try
        {
            // Perfil de pruebas, vacio.
            EditorPrefs.SetBool(RegistroGuardado.ClaveEditorPerfil, true);
            if (Directory.Exists(RegistroGuardado.CarpetaDe(true))) Directory.Delete(RegistroGuardado.CarpetaDe(true), true);
            PlayerPrefs.DeleteKey("pruebas_rpg_almas");
            Globales.Recargar();
            Comprobar(RegistroGuardado.Carpeta.EndsWith(RegistroGuardado.CarpetaPerfil), "perfil de pruebas: otra carpeta");
            Comprobar(RegistroGuardado.Clave("rpg_almas") == "pruebas_rpg_almas" && RegistroGuardado.Clave("volGeneral") == "volGeneral",
                      "perfil de pruebas: claves con prefijo (las opciones se comparten)");
            Comprobar(!Directory.Exists(RegistroGuardado.Carpeta) || Directory.GetFiles(RegistroGuardado.Carpeta).Length == 0, "perfil de pruebas: empieza vacio");

            // Datos de todas las categorias.
            Directory.CreateDirectory(RegistroGuardado.Carpeta);
            File.WriteAllText(Path.Combine(RegistroGuardado.Carpeta, "partida_1.json"), "{\"ranura\":1,\"escena\":\"Nivel Nieve\"}");
            PlayerPrefs.SetInt(RegistroGuardado.Clave("rpg_almas"), 77);
            Globales.DarLogro("descenso");
            Globales.Completar("crimson_wraith", false, 100f, 1);
            Globales.PonerMarca(CodigosSecretos.Marca("blind_huntress"), true);
            Globales.Descubrir("j:crimson_wraith:a:tajos");
            Globales.Descubrir("b:RataEscarcha:v");
            float volumen = PlayerPrefs.GetFloat("volGeneral", 1f);

            ProgresoJuego.Borrar(new[] { CategoriaGuardado.Logros }, "prueba");
            Globales.Recargar();
            Comprobar(!Globales.TieneLogro("descenso") && Globales.Completado("crimson_wraith", false) && Globales.Descubierto("b:RataEscarcha:v"),
                      "borrar Logros: solo los logros");
            ProgresoJuego.Borrar(new[] { CategoriaGuardado.Bestiario }, "prueba");
            Globales.Recargar();
            Comprobar(!Globales.Descubierto("b:RataEscarcha:v") && Globales.Descubierto("j:crimson_wraith:a:tajos"), "borrar Bestiario: solo el Bestiario");

            string respaldo = ProgresoJuego.Borrar(new[] { CategoriaGuardado.Partida, CategoriaGuardado.Desafios, CategoriaGuardado.Logros, CategoriaGuardado.Bestiario }, "todo");
            Globales.Recargar();
            Comprobar(!File.Exists(Path.Combine(RegistroGuardado.Carpeta, "partida_1.json")) && !PlayerPrefs.HasKey("pruebas_rpg_almas"), "borrar todo: partida y personaje");
            Comprobar(!Globales.Completado("crimson_wraith", false) && !Globales.Marca(CodigosSecretos.Marca("blind_huntress")) && !Globales.Descubierto("j:crimson_wraith:a:tajos"),
                      "borrar todo: desafios, codigo del jefe secreto y fichas de jefe");
            Comprobar(!FichaJefe.DeId("blind_huntress").Desbloqueado, "borrar todo: el jefe secreto vuelve a estar bloqueado");
            Comprobar(Mathf.Approximately(PlayerPrefs.GetFloat("volGeneral", 1f), volumen), "borrar todo: las opciones se quedan");
            Comprobar(Directory.Exists(respaldo) && File.Exists(Path.Combine(respaldo, "partida_1.json")) && File.Exists(Path.Combine(respaldo, "prefs.json")),
                      "respaldo con fecha antes de borrar");
            Comprobar(!File.Exists(Path.Combine(ProgresoJuego.CarpetaRespaldos, "borrado_en_curso.txt")), "sin marca de borrado a medias");
            Comprobar(File.ReadAllText(Path.Combine(Application.dataPath, "..", ".gitignore")).Contains("/_RespaldoProgreso/"), "respaldos ignorados por Git");

            ProgresoJuego.Restaurar(ProgresoJuego.UltimoRespaldo(true));
            Globales.Recargar();
            Comprobar(File.Exists(Path.Combine(RegistroGuardado.Carpeta, "partida_1.json")) && PlayerPrefs.GetInt("pruebas_rpg_almas", 0) == 77
                      && Globales.Completado("crimson_wraith", false) && Globales.Marca(CodigosSecretos.Marca("blind_huntress")),
                      "restaurar el ultimo respaldo: todo vuelve");
            Comprobar(FichaJefe.DeId("blind_huntress").Desbloqueado, "con la marca del codigo, el jefe secreto esta desbloqueado");
        }
        finally
        {
            // Todo como estaba.
            if (Directory.Exists(RegistroGuardado.CarpetaDe(true))) Directory.Delete(RegistroGuardado.CarpetaDe(true), true);
            PlayerPrefs.DeleteKey("pruebas_rpg_almas");
            EditorPrefs.SetBool(RegistroGuardado.ClaveEditorPerfil, perfilAntes);
            if (Directory.Exists(ProgresoJuego.CarpetaRespaldos))
                foreach (string d in Directory.GetDirectories(ProgresoJuego.CarpetaRespaldos).Where(d => Path.GetFileName(d).Contains("_pruebas_")))
                    Directory.Delete(d, true);
            Globales.Recargar();
        }
        Comprobar(Huella(real) == huellaReal, "el progreso real (archivos) no se ha tocado");
        Comprobar(PlayerPrefs.GetInt("rpg_almas", -1) + "|" + PlayerPrefs.GetFloat("volGeneral", -1f) == prefsReal, "el progreso real (PlayerPrefs) no se ha tocado");
        Debug.Log($"[Progreso] RESULTADO herramientas: {ok} OK, {fallos} FALLOS");
    }

    private static string Huella(string carpeta)
    {
        if (!Directory.Exists(carpeta)) return "-";
        return string.Join("|", Directory.GetFiles(carpeta).OrderBy(f => f).Select(f => Path.GetFileName(f) + ":" + new FileInfo(f).Length + ":" + File.ReadAllText(f).GetHashCode()));
    }

    // ------------------------------------------------------------------ Codigo

    private const string Clave = "PruebaProgreso.Modo";

    static PruebaProgreso()
    {
        EditorApplication.playModeStateChanged += e =>
        {
            if (SessionState.GetString(Clave, "") == "") return;
            if (e == PlayModeStateChange.EnteredPlayMode) new GameObject("PruebaProgreso").AddComponent<Conductor>();
            else if (e == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetString(Clave, "");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
        };
    }

    public static void Codigo()
    {
        SessionState.SetString(Clave, "codigo");
        EditorSceneManager.OpenScene("Assets/Scenes/Menu Principal.unity");
        EditorApplication.EnterPlaymode();
    }

    private class Conductor : MonoBehaviour
    {
        private Keyboard teclado;
        private int errores;

        private IEnumerator Start()
        {
            ok = fallos = 0;
            Application.logMessageReceived += (c, st, t) => { if (t == LogType.Exception || t == LogType.Error) errores++; };
            string carpeta = Path.Combine(Directory.GetCurrentDirectory(), "PruebaNieve");
            Partida.CarpetaPruebas = Path.Combine(carpeta, "PartidasCodigo");
            if (Directory.Exists(Partida.CarpetaPruebas)) Directory.Delete(Partida.CarpetaPruebas, true);
            Directory.CreateDirectory(Partida.CarpetaPruebas);
            Globales.Recargar();
            teclado = Keyboard.current ?? InputSystem.AddDevice<Keyboard>();
            yield return new WaitForSeconds(2f);

            MenuPrincipal m = Object.FindFirstObjectByType<MenuPrincipal>();
            FichaJefe caz = FichaJefe.DeId("blind_huntress");
            CodigosSecretos cs = CodigosSecretos.Get();
            Comprobar(cs != null && cs.codigosActivos && cs.codigos.Any(c => c.texto == "warrior"), "ficha de codigos con 'warrior'");
            Comprobar(!caz.Desbloqueado, "al empezar: jefe secreto bloqueado");

            // Fuera del menu de Desafios no hace nada.
            yield return Escribir("warrior");
            Comprobar(!caz.Desbloqueado, "en el menu principal (fuera de Desafios): no hace nada");

            Llamar(m, "Mostrar", Campo(m, "panelDesafios"));
            yield return new WaitForSecondsRealtime(0.5f);
            MenuDesafios md = (MenuDesafios)Campo(m, "menuDesafios");
            System.Collections.IList filas = (System.Collections.IList)Campo(md, "filas");

            // Mas de 2 s entre letras: se olvida.
            yield return Escribir("warr");
            yield return new WaitForSecondsRealtime(2.3f);
            yield return Escribir("ior");
            Comprobar(!caz.Desbloqueado, "pausa de mas de 2 s a medias: no cuenta");

            // Con la confirmacion abierta: no cuenta.
            ((UnityEngine.UI.Button)Campo(filas[1], "boton")).onClick.Invoke();
            yield return new WaitForSecondsRealtime(0.3f);
            Llamar(md, "PedirConfirmacion");
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Escribir("warrior");
            Comprobar(!caz.Desbloqueado, "con la confirmacion abierta: no cuenta");
            md.Atras();

            // Codigos apagados: no cuenta.
            cs.codigosActivos = false;
            yield return Escribir("warrior");
            Comprobar(!caz.Desbloqueado, "con 'Codigos activos' apagado: no cuenta");
            cs.codigosActivos = true;

            // Mayusculas y acentos dan igual (con la ficha de un jefe abierta).
            yield return new WaitForSecondsRealtime(2.2f);
            yield return Escribir("WaRRÍor");
            yield return new WaitForSecondsRealtime(0.5f);
            Comprobar(caz.Desbloqueado && Globales.Marca(CodigosSecretos.Marca("blind_huntress")), "WaRRÍor: jefe secreto desbloqueado");
            yield return new WaitForSecondsRealtime(1.6f);
            yield return Captura(carpeta, "r16_c01_codigo");
            yield return new WaitForSecondsRealtime(1.5f);
            Comprobar(filas.Count == 3 && Globales.Marca("revelado_blind_huntress"), "aparece en la lista con su destello");
            Comprobar(!Globales.Completado("crimson_wraith", false) && !Globales.Completado("shadowed_wetlands", false) && Logros.Desbloqueados() == 0
                      && !Bitacora.Claves(caz, Bitacora.Seccion.Ataques).Any() && !Bitacora.Tiene(Bitacora.ClaveFase(caz, 1)),
                      "no completa desafios, no da logros ni informacion del jefe");
            string texto = ((TMPro.TextMeshProUGUI)Campo(md, "susurro")).text;
            Comprobar(texto.Contains("Algo despertó en el bosque"), "mensaje: " + texto);
            yield return Captura(carpeta, "r16_c02_lista");

            // Otra vez: sin repetir nada.
            yield return Escribir("warrior");
            yield return new WaitForSecondsRealtime(1f);
            Comprobar(filas.Count == 3, "repetir el codigo: no duplica ni repite la animacion");

            // Al volver a abrir el menu sigue ahi.
            Llamar(m, "Mostrar", Campo(m, "panelPrincipal"));
            yield return new WaitForSecondsRealtime(0.3f);
            Llamar(m, "Mostrar", Campo(m, "panelDesafios"));
            yield return new WaitForSecondsRealtime(0.3f);
            Comprobar(filas.Count == 3, "al volver al menu el jefe sigue en la lista");

            Comprobar(errores == 0, $"sin errores en la consola ({errores})");
            Debug.Log($"[Progreso] RESULTADO codigo: {ok} OK, {fallos} FALLOS");
            // La carpeta de pruebas se queda puesta: lo que se guarde al salir no toca el progreso real.
            Partida.Descargar();
            Globales.GuardarPendiente();
            EditorApplication.ExitPlaymode();
        }

        private IEnumerator Escribir(string s)
        {
            foreach (char c in s)
            {
                InputSystem.QueueTextEvent(teclado, c);
                InputSystem.Update();
                yield return new WaitForSecondsRealtime(0.05f);
            }
        }

        private static object Campo(object o, string campo)
        {
            for (System.Type t = o.GetType(); t != null; t = t.BaseType)
            {
                FieldInfo f = t.GetField(campo, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                if (f != null) return f.GetValue(o);
            }
            return null;
        }

        private static void Llamar(object o, string metodo, params object[] args) =>
            o.GetType().GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public)
             .First(x => x.Name == metodo && x.GetParameters().Length == args.Length).Invoke(o, args);

        private IEnumerator Captura(string carpeta, string nombre)
        {
            Camera cam = Camera.main;
            if (cam == null) yield break;
            var cambiados = new System.Collections.Generic.List<Canvas>();
            foreach (Canvas c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay && c.gameObject.activeInHierarchy)
                { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = cam; c.planeDistance = 1f; cambiados.Add(c); }
            Canvas.ForceUpdateCanvases();
            yield return null;
            RenderTexture rt = new RenderTexture(1280, 720, 24);
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = null;
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(carpeta, nombre + ".png"), tex.EncodeToPNG());
            rt.Release();
            foreach (Canvas c in cambiados) c.renderMode = RenderMode.ScreenSpaceOverlay;
        }
    }
}
