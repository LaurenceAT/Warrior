using System.Collections;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Prueba de los desafios de Bringer of Death (cueva) y Duo Martial Hero (bosque).
//   ... -executeMethod PruebaDesafiosNuevos.Todo   (batchmode, sin -quit)
//   1. Sin jugar: Warrior > Desafios nuevos > Crear o actualizar; escenas, fichas,
//      logros, musica, prefabs del duo (+20 % de vida, el segundo carmesi) y
//      Build Settings; "Nivel Cueva" y "Bosque Cazadora" no cambian.
//   2. Jugando (desde el menu principal, en silencio y en una carpeta de pruebas):
//      cada desafio en Normal: una hoguera, la arena, el jefe (o los dos con dos
//      barras), cambio de fase del Bringer, victoria, desafio completado y logro.
// Escribe "[DESNUEVOS] OK/FALLO" y capturas en PruebaNieve/desafio_*.png.
[InitializeOnLoad]
public static class PruebaDesafiosNuevos
{
    private const string Clave = "PruebaDesafiosNuevos.";

    static PruebaDesafiosNuevos()
    {
        EditorApplication.playModeStateChanged += e =>
        {
            if (SessionState.GetString(Clave + "Modo", "") == "") return;
            if (e == PlayModeStateChange.EnteredPlayMode) new GameObject("PruebaDesafiosNuevos").AddComponent<Conductor>();
            else if (e == PlayModeStateChange.EnteredEditMode)
            {
                Comprobar(Huella(Originales) == SessionState.GetString(Clave + "Huella", ""), "Nivel Cueva y Bosque Cazadora siguen igual tras jugar");
                Debug.Log($"[DESNUEVOS] RESULTADO: {SessionState.GetInt(Clave + "ok", 0)} OK, {SessionState.GetInt(Clave + "fallos", 0)} FALLOS");
                SessionState.SetString(Clave + "Modo", "");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
        };
    }

    private static readonly string[] Originales = { "Assets/Scenes/Nivel Cueva.unity", "Assets/Scenes/Bosque Cazadora.unity" };

    private static void Ok(string s) { SessionState.SetInt(Clave + "ok", SessionState.GetInt(Clave + "ok", 0) + 1); Debug.Log("[DESNUEVOS] OK " + s); }
    private static void Fallo(string s) { SessionState.SetInt(Clave + "fallos", SessionState.GetInt(Clave + "fallos", 0) + 1); Debug.LogWarning("[DESNUEVOS] FALLO " + s); }
    private static void Comprobar(bool c, string s) { if (c) Ok(s); else Fallo(s); }

    private static string Huella(string[] rutas)
    {
        using (var sha = SHA1.Create())
            return string.Join("|", rutas.Select(r => System.Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(r)))));
    }

    public static void Todo()
    {
        SessionState.SetInt(Clave + "ok", 0);
        SessionState.SetInt(Clave + "fallos", 0);
        string antes = Huella(Originales);
        CrearDesafiosNuevos.Crear();
        string despues = Huella(Originales);
        Comprobar(antes == despues, "el generador no toca Nivel Cueva ni Bosque Cazadora");
        SessionState.SetString(Clave + "Huella", despues);

        FichaJefe fb = AssetDatabase.LoadAssetAtPath<FichaJefe>(CrearDesafiosNuevos.FichaBringer);
        FichaJefe fd = AssetDatabase.LoadAssetAtPath<FichaJefe>(CrearDesafiosNuevos.FichaDuo);
        Comprobar(fb != null && fb.escena == "Cueva Bringer" && fb.ataques.Count == JefeBringer.Todos.Length && fb.imagen != null && !fb.secreto,
                  "ficha del Bringer: escena, sus 12 ataques, imagen y visible desde el principio");
        Comprobar(fb != null && JefeBringer.Todos.All(a => fb.BuscarAtaque(a) != null), "la ficha del Bringer tiene los ids de sus ataques");
        Comprobar(fd != null && fd.escena == "Bosque Duo Martial" && fd.nombre == "Duo Martial Hero" && fd.imagen != null && !fd.secreto,
                  "ficha del Duo Martial Hero: escena, nombre, imagen y visible");
        // De los archivos (FichaLogro.Todos() guarda la lista de antes de crearlos).
        var logros = AssetDatabase.FindAssets("t:FichaLogro", new[] { "Assets/Resources/Logros" })
                                  .Select(g => AssetDatabase.LoadAssetAtPath<FichaLogro>(AssetDatabase.GUIDToAssetPath(g))).ToList();
        foreach (string id in new[] { "prueba_guadana", "prueba_duo", "cicatriz_guadana", "cicatriz_duo" })
            Comprobar(logros.Any(l => l.id == id && l.icono != null), "logro " + id);
        var nombres = EditorBuildSettings.scenes.Select(s => s.path).ToList();
        Comprobar(nombres.Contains(CrearDesafiosNuevos.EscenaBringer) && nombres.Contains(CrearDesafiosNuevos.EscenaDuo), "las 2 escenas en Build Settings");
        AjustesMartialHero aj = AssetDatabase.LoadAssetAtPath<AjustesMartialHero>(CrearDesafiosNuevos.AjustesDuo);
        AjustesMartialHero original = AssetDatabase.LoadAssetAtPath<AjustesMartialHero>(CrearMartialHero.RutaFicha);
        Comprobar(aj != null && aj.vida == Mathf.RoundToInt(original.vida * CrearDesafiosNuevos.VidaDuo), $"el duo con +20 % de vida ({aj?.vida} frente a {original.vida})");
        GameObject p2 = AssetDatabase.LoadAssetAtPath<GameObject>(CrearDesafiosNuevos.PrefabDuo2);
        AnimadorHoja an = p2 != null ? p2.GetComponentInChildren<AnimadorHoja>() : null;
        Comprobar(an != null && an.clips.All(c => c.hoja != null && AssetDatabase.GetAssetPath(c.hoja).Contains("Carmesi")), "el segundo usa las tiras carmesi");
        ConfigNivel cb = AssetDatabase.LoadAssetAtPath<ConfigNivel>(CrearDesafiosNuevos.ConfigBringer);
        ConfigNivel cd = AssetDatabase.LoadAssetAtPath<ConfigNivel>(CrearDesafiosNuevos.ConfigDuo);
        Comprobar(cb != null && cb.jefeFase1.pista != null && cd != null && cd.jefeFase1.pista != null, $"musica: {cb?.jefeFase1.pista?.name} / {cd?.jefeFase1.pista?.name}");

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
                if (t == LogType.Exception || t == LogType.Error) { errores++; Debug.Log("[DESNUEVOS] error visto: " + c + "\n" + st); }
            };
            Partida.CarpetaPruebas = Path.Combine(Directory.GetCurrentDirectory(), "PruebaNieve", "PartidasDesafiosNuevos");
            if (Directory.Exists(Partida.CarpetaPruebas)) Directory.Delete(Partida.CarpetaPruebas, true);
            Directory.CreateDirectory(Partida.CarpetaPruebas);
            Globales.Recargar();
            yield return new WaitForSeconds(1f);

            Comprobar(FichaJefe.Visibles().Any(f => f.id == CrearDesafiosNuevos.IdBringer) && FichaJefe.Visibles().Any(f => f.id == CrearDesafiosNuevos.IdDuo),
                      "los dos salen en Desafios desde el principio");
            yield return Desafiar(CrearDesafiosNuevos.IdBringer, "prueba_guadana", false);
            yield return Desafiar(CrearDesafiosNuevos.IdDuo, "prueba_duo", true);

            AudioListener.volume = 0f;
            AudioListener.volume = 0f;
            Comprobar(errores == 0, $"sin errores en la consola ({errores})");
            Partida.Descargar();
            EditorApplication.ExitPlaymode();
        }

        private IEnumerator Desafiar(string id, string logro, bool duo)
        {
            FichaJefe f = FichaJefe.Todas().First(x => x.id == id);
            Desafio.Empezar(f, false);
            for (float t = 0f; t < 30f && (SceneManager.GetActiveScene().name != f.escena || PantallaCarga.Cargando); t += Time.unscaledDeltaTime) yield return null;
            Comprobar(SceneManager.GetActiveScene().name == f.escena, $"{f.nombre}: carga su escena ({f.escena})");
            yield return new WaitForSeconds(1.5f);
            AudioListener.volume = 0f;
            PlayerControler p = FindFirstObjectByType<PlayerControler>();
            int hogueras = FindObjectsByType<Hoguera>(FindObjectsSortMode.None).Length;
            ArenaJefe arena = FindObjectsByType<ArenaJefe>(FindObjectsSortMode.None).FirstOrDefault(a => a.EsPrincipal);
            Comprobar(p != null && hogueras == 1 && arena != null && FindFirstObjectByType<ModoDesafio>() != null,
                      $"{f.nombre}: player, una hoguera ({hogueras}) y la arena del jefe");
            Comprobar(FindFirstObjectByType<ArenaCazadora>() == null, $"{f.nombre}: sin nada de la Cazadora");
            Comprobar(arena != null && arena.EsDuo == duo, $"{f.nombre}: {(duo ? "dos jefes" : "un jefe")} en la arena");
            if (p == null || arena == null) yield break;
            yield return Captura($"desafio_{id}_hoguera");

            p.InvulnerableExterno = true;
            arena.EmpezarDesdeZona(p);
            yield return new WaitForSeconds(3f);
            var jefes = FindObjectsByType<JefeBase>(FindObjectsSortMode.None);
            var barras = FindObjectsByType<BarraJefe>(FindObjectsSortMode.None);
            if (duo)
            {
                var martial = jefes.OfType<JefeMartialHero>().ToList();
                Comprobar(martial.Count == 2, $"duo: salen 2 Martial Hero ({martial.Count})");
                var hojas = martial.Select(m => AssetDatabase.GetAssetPath(m.GetComponentInChildren<AnimadorHoja>().clips[0].hoja)).ToList();
                Comprobar(hojas.Count(h => h.Contains("Carmesi")) == 1, "duo: uno con su ropa de siempre y otro carmesi");
                AjustesMartialHero aj = AssetDatabase.LoadAssetAtPath<AjustesMartialHero>(CrearDesafiosNuevos.AjustesDuo);
                Comprobar(martial.All(m => m.GetComponent<EnemyHealth>().MaxHealth == aj.vida), $"duo: los dos con {aj.vida} de vida");
                Comprobar(barras.Length == 2, $"duo: dos barras ({barras.Length})");
                if (barras.Length == 2)
                {
                    float y0 = barras[0].Marco.anchoredPosition.y, y1 = barras[1].Marco.anchoredPosition.y;
                    Comprobar(Mathf.Abs(y0 - y1) >= BarraJefe.AlturaPiso - 1f, $"duo: una barra encima de la otra ({y0:0} y {y1:0})");
                }
                yield return Captura($"desafio_{id}_pelea");
                Comprobar(aj.duo && martial.All(m => m.fxOnda != null && m.fxOnda.hoja != null), "duo: modo duo con su onda");

                // 25 s de pelea con el jugador quieto en medio: turnos y ondas.
                p.transform.position = new Vector3(arena.PuntoJefe.x, p.transform.position.y + 0.5f, 0f);
                int golpesAntes = PeligrosBringer.Golpes, cambiosAntes = DirectorDuo.Cambios;
                bool unoAtacaOtroOndas = false, foto = false;
                for (float t = 0f; t < 25f; t += Time.deltaTime)
                {
                    var roles = martial.Select(DirectorDuo.RolDe).ToList();
                    if (roles.Contains(DirectorDuo.Rol.Atacante) && roles.Contains(DirectorDuo.Rol.Ondas)) unoAtacaOtroOndas = true;
                    if (!foto && FindObjectsByType<MediaLunaNecro>(FindObjectsSortMode.None).Length > 0) { foto = true; yield return Captura($"desafio_{id}_ondas"); }
                    yield return null;
                }
                int ondas = martial.Sum(m => m.OndasLanzadas);
                Comprobar(unoAtacaOtroOndas, "duo: normalmente uno ataca y el otro lanza ondas");
                Comprobar(ondas > 0, $"duo: lanzan ondas desde lejos ({ondas} en 25 s, alcanzan {PeligrosBringer.Golpes - golpesAntes})");
                Comprobar(DirectorDuo.Cambios > cambiosAntes, $"duo: se turnan ({DirectorDuo.Cambios - cambiosAntes} cambios, {DirectorDuo.AsaltosDobles} asaltos dobles)");
                Debug.Log("[DESNUEVOS] INFO combos: " + string.Join(" / ", martial.Select(m => m.CombosHechos.Count)));
                // Asalto doble (forzado): atacan los dos.
                DirectorDuo.FinCombo(DirectorDuo.Atacante, 1f);
                Comprobar(DirectorDuo.Ambos && martial.All(m => DirectorDuo.RolDe(m) == DirectorDuo.Rol.Atacante), "duo: en un asalto doble atacan los dos");
                // Cae uno: la pelea sigue.
                martial[0].GetComponent<EnemyHealth>().DanoEstado(99999);
                yield return new WaitForSeconds(2.5f);
                Comprobar(!Desafio.Completado && ArenaJefe.EnCombate, "duo: con uno caido la pelea sigue");
                martial[1].GetComponent<EnemyHealth>().DanoEstado(99999);
            }
            else
            {
                JefeBringer b = jefes.OfType<JefeBringer>().FirstOrDefault();
                Comprobar(b != null && barras.Length == 1, "Bringer: aparece con una sola barra");
                if (b == null) yield break;
                float sueloArena = new SerializedObject(arena).FindProperty("suelo").floatValue;
                Comprobar(Mathf.Abs(b.transform.position.y - sueloArena) < 1f, $"Bringer: aparece en el suelo de la arena (y = {b.transform.position.y:0.0}, suelo {sueloArena:0.0})");
                yield return Captura($"desafio_{id}_pelea");
                EnemyHealth s = b.GetComponent<EnemyHealth>();
                s.DanoEstado(s.MaxHealth / 2 + 10);
                for (float t = 0f; t < 8f && !b.Fase2; t += Time.deltaTime) yield return null;
                Comprobar(b.Fase2 && FindObjectsByType<BarraJefe>(FindObjectsSortMode.None).Length == 1, "Bringer: pasa a la fase 2 con la misma barra");
                yield return new WaitForSeconds(2f);
                yield return Captura($"desafio_{id}_fase2");
                s.DanoEstado(s.CurrentHealth + 10);
            }
            for (float t = 0f; t < 10f && !Desafio.Completado; t += Time.unscaledDeltaTime) yield return null;
            Comprobar(Desafio.Completado && Globales.Completado(id, false), $"{f.nombre}: desafio completado");
            yield return new WaitForSeconds(1f);
            Comprobar(Globales.TieneLogro(logro), $"{f.nombre}: logro {logro}");
            yield return Captura($"desafio_{id}_victoria");
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
