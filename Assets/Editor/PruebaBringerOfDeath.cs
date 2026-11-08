using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Prueba del Bringer of Death en la sala de pruebas (batchmode, sin -quit):
//   ... -executeMethod PruebaBringerOfDeath.Todo
//   1. Sin jugar: Warrior > Bringer of Death > Crear o actualizar, sprites,
//      prefab con sus clips y efectos, ficha y entrada del catalogo (una sola
//      barra, marca en la mitad), y que no esta en Resources ni en Build Settings.
//   2. En la sala (vida infinita, en silencio): cada ataque de la fase 1 forzado
//      y si te alcanza; cambio de fase con la misma barra; cada ataque de la fase
//      2; un golpe enorme en la fase 1 no lo mata (salta a la fase 2); muerte sin
//      hechizos en el aire; 5 reapariciones.
// Escribe "[BRINGER] OK/FALLO/INFO" y capturas en PruebaNieve/bringer_*.png.
[InitializeOnLoad]
public static class PruebaBringerOfDeath
{
    private const string Clave = "PruebaBringerOfDeath.Modo";

    static PruebaBringerOfDeath()
    {
        EditorApplication.playModeStateChanged += e =>
        {
            if (SessionState.GetString(Clave, "") == "") return;
            if (e == PlayModeStateChange.EnteredPlayMode) new GameObject("PruebaBringer").AddComponent<Conductor>();
            else if (e == PlayModeStateChange.EnteredEditMode)
            {
                Debug.Log($"[BRINGER] RESULTADO: {SessionState.GetInt(Clave + ".ok", 0)} OK, {SessionState.GetInt(Clave + ".fallos", 0)} FALLOS");
                SessionState.SetString(Clave, "");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
        };
    }

    private static void Ok(string s) { SessionState.SetInt(Clave + ".ok", SessionState.GetInt(Clave + ".ok", 0) + 1); Debug.Log("[BRINGER] OK " + s); }
    private static void Fallo(string s) { SessionState.SetInt(Clave + ".fallos", SessionState.GetInt(Clave + ".fallos", 0) + 1); Debug.LogWarning("[BRINGER] FALLO " + s); }
    private static void Info(string s) => Debug.Log("[BRINGER] INFO " + s);
    private static void Comprobar(bool c, string s) { if (c) Ok(s); else Fallo(s); }

    public static void Todo()
    {
        SessionState.SetInt(Clave + ".ok", 0);
        SessionState.SetInt(Clave + ".fallos", 0);
        CrearBringerOfDeath.Crear();
        Comprobar(CrearBringerOfDeath.Comprobar(out string informe), "sprites: " + informe);

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CrearBringerOfDeath.RutaPrefab);
        AnimadorHoja an = prefab != null ? prefab.GetComponentInChildren<AnimadorHoja>() : null;
        string[] clips = { "quieto", "andar", "guadana", "conjurar", "conjurarBucle", "golpeado", "muerte", "desvanecer", "aparecer" };
        Comprobar(an != null && clips.All(c => an.clips.Any(k => k.nombre == c && k.fotogramas != null && k.fotogramas.Length > 0 && k.fotogramas.All(t => t != null))),
                  "el prefab tiene sus clips con cuadros: " + string.Join(", ", clips));
        JefeBringer j = prefab != null ? prefab.GetComponent<JefeBringer>() : null;
        var fx = new[] { j?.fxPortal, j?.fxMano, j?.fxAnillo, j?.fxEstallido, j?.fxOla, j?.fxMediaLuna, j?.fxEspiritu, j?.fxEspirituFin,
                         j?.fxMarca, j?.fxCometa, j?.fxEstrellas, j?.fxEsferaAbrir, j?.fxEsferaBucle, j?.fxEsferaFin };
        Comprobar(j != null && fx.All(c => c != null && c.fotogramas != null && c.fotogramas.Length > 0 && c.fotogramas.All(t => t != null)),
                  $"los 14 efectos tienen sus cuadros (Necromante, Starcaller y su hechizo)");
        AjustesBringer aj = AssetDatabase.LoadAssetAtPath<AjustesBringer>(CrearBringerOfDeath.RutaFicha);
        Comprobar(aj != null, "ficha " + CrearBringerOfDeath.RutaFicha);
        if (aj != null)
        {
            int vidaMax = AjustesProgreso.Get().vidaBase;
            int mayor = new[] { aj.danoGuadana, aj.danoMano, aj.danoMediaLuna, aj.danoOla, aj.danoCirculo, aj.danoEspiritu, aj.danoCometa, aj.danoEsfera, aj.danoJuicio }.Max();
            Comprobar(mayor < vidaMax, $"ningun golpe mata con la vida inicial llena (el mayor: {mayor} de {vidaMax})");
        }
        CatalogoPruebas cat = AssetDatabase.LoadAssetAtPath<CatalogoPruebas>(SalaPruebas.CarpetaDatos + "Catalogo Pruebas.asset");
        CatalogoPruebas.Entrada e = cat != null ? cat.entradas.FirstOrDefault(x => x != null && x.prefab == prefab) : null;
        Comprobar(e != null && e.arena == CatalogoPruebas.TipoArena.Generica && !e.barraPorFase && Mathf.Approximately(e.marcaFase, 0.5f),
                  "en el catalogo de la sala: una sola barra con la marca en la mitad");
        bool enResources = AssetDatabase.FindAssets("Bringer", new[] { "Assets/Resources" }).Length > 0;
        bool enBuild = EditorBuildSettings.scenes.Any(s => s.path.Contains("Bringer") || s.path.Contains("SalaPruebas"));
        Comprobar(!enResources && !enBuild, "no esta en Resources ni en Build Settings");

        SessionState.SetString(Clave, "jugar");
        EditorSceneManager.OpenScene(SalaPruebas.Ruta, OpenSceneMode.Single);
        ArranquePruebas.Proteger();
        EditorApplication.EnterPlaymode();
    }

    private class Conductor : MonoBehaviour
    {
        private int errores;
        private SalaPruebas sala;
        private JefeBringer jefe;
        private PlayerControler pc;
        private string carpeta;

        private IEnumerator Start()
        {
            AudioListener.volume = 0f;
            Application.logMessageReceived += (c, st, t) =>
            {
                if (t == LogType.Exception || t == LogType.Error) { errores++; Debug.Log("[BRINGER] error visto: " + c + "\n" + st); }
            };
            carpeta = Path.Combine(Directory.GetCurrentDirectory(), "PruebaNieve");
            Directory.CreateDirectory(carpeta);
            for (float t = 0f; t < 10f && SalaPruebas.Actual == null; t += Time.unscaledDeltaTime) yield return null;
            sala = SalaPruebas.Actual;
            if (sala == null) { Fallo("no hay sala"); Terminar(); yield break; }
            yield return new WaitForSeconds(1f);
            CatalogoPruebas.Entrada e = sala.Catalogo.entradas.FirstOrDefault(x => x != null && x.nombre == CrearBringerOfDeath.NombreCatalogo);
            if (e == null) { Fallo("no esta en el catalogo"); Terminar(); yield break; }
            sala.VidaInfinita = true;
            yield return Invocar(e);
            if (jefe == null) { Fallo("no se invoca"); Terminar(); yield break; }
            AjustesBringer aj = jefe.Ajustes;
            EnemyHealth salud = jefe.GetComponent<EnemyHealth>();
            Comprobar(salud.MaxHealth == aj.vida && salud.CurrentHealth == aj.vida, $"una barra con toda su vida ({salud.CurrentHealth}/{salud.MaxHealth})");
            Comprobar(FindObjectsByType<BarraJefe>(FindObjectsSortMode.None).Length == 1, "una sola barra en pantalla");
            yield return Captura("bringer_entrada");

            // Fase 1.
            foreach (string id in new[] { JefeBringer.Guadana, JefeBringer.Mano, JefeBringer.MediaLuna, JefeBringer.Ola, JefeBringer.Desvanecer, JefeBringer.Cosecha })
                yield return Probar(id, false);

            // Cambio de fase con la misma barra.
            PeligrosBringer.Golpes = 0;
            sala.SaltarAFase(2);
            for (float t = 0f; t < 1f && !jefe.Transformando; t += Time.deltaTime) yield return null;
            Comprobar(jefe.Transformando && jefe.Modificar(100, TipoArma.Espada) == 0, "al cambiar de fase conjura invulnerable");
            yield return new WaitForSeconds(0.9f);
            yield return Captura("bringer_cambio_fase");
            for (float t = 0f; t < 6f && !jefe.Fase2; t += Time.deltaTime) yield return null;
            Comprobar(jefe.Fase2 && jefe.CambiosDeFase == 1, "pasa a la fase 2");
            Comprobar(salud.MaxHealth == aj.vida && salud.CurrentHealth > 0 && salud.CurrentHealth <= aj.vida * aj.marcaFase,
                      $"la misma barra, por debajo de la mitad ({salud.CurrentHealth}/{salud.MaxHealth})");
            Comprobar(FindObjectsByType<BarraJefe>(FindObjectsSortMode.None).Length == 1, "sigue con una sola barra");

            // Fase 2: los nuevos y los de siempre.
            foreach (string id in new[] { JefeBringer.Circulo, JefeBringer.Espiritus, JefeBringer.Cometas, JefeBringer.Esfera,
                                          JefeBringer.Juicio, JefeBringer.Marea, JefeBringer.Mano, JefeBringer.Guadana, JefeBringer.Cosecha })
                yield return Probar(id, true);

            // Un golpe enorme en la fase 1 no lo mata: se queda en la marca y cambia de fase.
            yield return Invocar(e);
            salud = jefe.GetComponent<EnemyHealth>();
            yield return new WaitForSeconds(0.5f);
            salud.DanoEstado(salud.MaxHealth * 3);
            yield return null;
            Comprobar(!salud.Muerto && salud.CurrentHealth == Mathf.FloorToInt(salud.MaxHealth * aj.marcaFase) && jefe.Transformando,
                      $"un golpe enorme en la fase 1 lo deja en la mitad y empieza la fase 2 ({salud.CurrentHealth})");
            for (float t = 0f; t < 6f && !jefe.Fase2; t += Time.deltaTime) yield return null;

            // Muerte: sin hechizos en el aire.
            jefe.ForzarAtaque(JefeBringer.Cometas);
            yield return new WaitForSeconds(2f);
            salud.DanoEstado(salud.CurrentHealth + 10);
            yield return new WaitForSeconds(0.3f);
            Comprobar(salud.Muerto, "en la fase 2 muere");
            Comprobar(Peligros() == 0, $"al morir no queda ningun hechizo ({Peligros()})");
            yield return new WaitForSeconds(1.5f);
            yield return Captura("bringer_muerte");

            for (int i = 0; i < 5; i++)
            {
                yield return Invocar(e);
                jefe.ForzarAtaque(JefeBringer.Todos[Random.Range(0, JefeBringer.Todos.Length)]);
                yield return new WaitForSeconds(1.2f);
            }
            Comprobar(jefe != null && FindObjectsByType<JefeBringer>(FindObjectsSortMode.None).Length == 1, "5 reapariciones: un solo jefe");
            sala.Limpiar();
            yield return new WaitForSeconds(0.5f);
            Comprobar(Peligros() == 0, "al limpiar la sala no queda ningun hechizo");

            Comprobar(errores == 0, $"sin errores en la consola ({errores})");
            Terminar();
        }

        private static int Peligros() =>
            FindObjectsByType<ManoAbismo>(FindObjectsSortMode.None).Length + FindObjectsByType<EstallidoNecro>(FindObjectsSortMode.None).Length
            + FindObjectsByType<TramoOla>(FindObjectsSortMode.None).Length + FindObjectsByType<MediaLunaNecro>(FindObjectsSortMode.None).Length
            + FindObjectsByType<EspirituNecro>(FindObjectsSortMode.None).Length + FindObjectsByType<CometaEstelar>(FindObjectsSortMode.None).Length
            + FindObjectsByType<EsferaVacio>(FindObjectsSortMode.None).Length;

        private IEnumerator Invocar(CatalogoPruebas.Entrada e)
        {
            sala.Invocar(e);
            jefe = sala.Jefe as JefeBringer;
            pc = sala.Player;
            for (float t = 0f; t < 6f && jefe != null && jefe.Estado == EstadoEnemigo.Patrulla; t += Time.deltaTime) yield return null;
            yield return new WaitForSeconds(0.3f);
        }

        // Pone al jugador a una distancia, fuerza el ataque y espera a que acabe.
        private IEnumerator Probar(string id, bool fase2)
        {
            while (jefe.EnCurso != null) yield return null;
            float bx = jefe.transform.position.x;
            float lado = bx > 0f ? -1f : 1f;
            float dist = id == JefeBringer.Guadana ? 2.2f : id == JefeBringer.Esfera ? 3f : 6f;
            pc.transform.position = new Vector3(bx + lado * dist, 0.3f, 0f);
            pc.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            int hechos = jefe.Hechos.Count;
            int golpes = PeligrosBringer.Golpes;
            int guadanas = jefe.ResultadosGuadana.Count;
            jefe.ForzarAtaque(id);
            float inicio = Time.time;
            bool foto = false;
            float cuando = id == JefeBringer.Guadana ? 0.8f : id == JefeBringer.Desvanecer ? 1.6f : id == JefeBringer.Cosecha ? 1.4f
                         : id == JefeBringer.Cometas ? 2.2f : id == JefeBringer.Esfera ? 2.4f : id == JefeBringer.Juicio ? 1.4f : 1.2f;
            while (jefe.Hechos.Count == hechos && Time.time - inicio < 20f)
            {
                if (!foto && jefe.EnCurso == id && Time.time - inicio > cuando) { foto = true; yield return Captura($"bringer_{(fase2 ? "f2" : "f1")}_{id}"); }
                yield return null;
            }
            bool hecho = jefe.Hechos.Count > hechos && jefe.Hechos[jefe.Hechos.Count - 1] == id;
            int alcanzado = PeligrosBringer.Golpes - golpes;
            var tajos = jefe.ResultadosGuadana.Skip(guadanas).ToList();
            string res = $"{(fase2 ? "fase 2" : "fase 1")} {id}: {(hecho ? "hecho" : "NO HECHO")} en {Time.time - inicio:0.0} s, hechizos que alcanzan {alcanzado}, tajos [{string.Join(",", tajos.Select(r => r.HasValue ? r.Value.ToString() : "-"))}]";
            Comprobar(hecho, res);
            switch (id)
            {
                case JefeBringer.Guadana:
                case JefeBringer.Desvanecer:
                    Comprobar(tajos.Count > 0 && tajos[0] == PlayerControler.ResultadoDano.Recibido, $"{id}: el tajo te alcanza");
                    Info($"{id}: aviso real {jefe.UltimoAviso:0.00} s");
                    break;
                case JefeBringer.Mano:
                case JefeBringer.Ola:
                case JefeBringer.Circulo:
                case JefeBringer.Juicio:
                case JefeBringer.Marea:
                case JefeBringer.Esfera:
                    Comprobar(alcanzado > 0, $"{id}: te alcanza si te quedas quieto");
                    break;
                case JefeBringer.Cosecha:
                    Comprobar(jefe.UltimasManos >= 10, $"cosecha: {jefe.UltimasManos} manos por toda la arena, huecos en {string.Join(" / ", jefe.UltimosHuecos.Select(x => x.ToString("0.0")))}");
                    float px = pc.transform.position.x;
                    bool enHueco = jefe.UltimosHuecos.Any(x => Mathf.Abs(x - px) < 0.4f);
                    Info($"cosecha: jugador en x={px:0.0} {(enHueco ? "(en un hueco)" : "")}, alcanzado {alcanzado}");
                    if (enHueco) Comprobar(alcanzado == 0, "cosecha: en un hueco no te alcanza");
                    break;
                case JefeBringer.Cometas:
                    Comprobar(jefe.UltimosCometas >= jefe.Ajustes.cometas, $"cometas: {jefe.UltimosCometas} por toda la arena");
                    break;
            }
            if (id == JefeBringer.Esfera && jefe.UltimaEsfera != null)
                Info("esfera: resultado " + jefe.UltimaEsfera.Resultado);
            yield return new WaitForSeconds(0.3f);
        }

        // Toda la arena (40 x 20) en 1280x720.
        private IEnumerator Captura(string nombre)
        {
            // (WaitForEndOfFrame no llega nunca en batchmode.)
            yield return null;
            Camera cam = Camera.main;
            if (cam == null) yield break;
            Vector3 pos = cam.transform.position;
            float tam = cam.orthographicSize;
            cam.transform.position = new Vector3(0f, 6.5f, pos.z);
            cam.orthographicSize = 11.5f;
            RenderTexture rt = new RenderTexture(1280, 720, 24);
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = null;
            cam.transform.position = pos;
            cam.orthographicSize = tam;
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(carpeta, nombre + ".png"), tex.EncodeToPNG());
            rt.Release();
            Destroy(tex);
        }

        private void Terminar() => EditorApplication.ExitPlaymode();
    }
}
