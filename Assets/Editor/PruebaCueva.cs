using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Prueba automatica del nivel Cueva en modo juego (desde la linea de comandos):
//   Unity -batchmode -projectPath ... -executeMethod PruebaCueva.Lanzar
// Abre la escena, entra en Play, lleva al player a cada grupo de enemigos, hace
// capturas cada cierto tiempo y apunta en el log la vida del player y de los
// enemigos. Al acabar cierra Unity. Las capturas van a "PruebaCueva/".
[InitializeOnLoad]
public static class PruebaCueva
{
    private const string Clave = "PruebaCueva.Activa";
    private const string Escena = "Assets/Scenes/Nivel Cueva.unity";

    static PruebaCueva()
    {
        EditorApplication.playModeStateChanged += Cambio;
    }

    public static void Lanzar()
    {
        SessionState.SetBool(Clave, true);
        EditorSceneManager.OpenScene(Escena);
        ArranquePruebas.Proteger();
        EditorApplication.EnterPlaymode();
    }

    // Prueba de alcance de los golpes: pone cada enemigo a varias distancias del
    // player y lanza los tajos de espada, apuntando cuales aciertan.
    public static void LanzarGolpes()
    {
        SessionState.SetBool(Clave, true);
        SessionState.SetBool(Clave + ".Golpes", true);
        EditorSceneManager.OpenScene(Escena);
        ArranquePruebas.Proteger();
        EditorApplication.EnterPlaymode();
    }

    private static void Cambio(PlayModeStateChange estado)
    {
        if (!SessionState.GetBool(Clave, false)) return;
        if (estado == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Clave + ".Vida", false))
        {
            SessionState.SetBool(Clave + ".Vida", false);
            new GameObject("PruebaVida").AddComponent<ConductorVida>();
        }
        else if (estado == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Clave + ".UI", false))
        {
            SessionState.SetBool(Clave + ".UI", false);
            new GameObject("PruebaUI").AddComponent<ConductorUI>();
        }
        else if (estado == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Clave + ".Parte4", false))
        {
            SessionState.SetBool(Clave + ".Parte4", false);
            new GameObject("PruebaParte4").AddComponent<ConductorParte4>();
        }
        else if (estado == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Clave + ".Reinicio", false))
        {
            SessionState.SetBool(Clave + ".Reinicio", false);
            new GameObject("PruebaReinicio").AddComponent<ConductorReinicio>();
        }
        else if (estado == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Clave + ".Jefe", false))
        {
            SessionState.SetBool(Clave + ".Jefe", false);
            new GameObject("PruebaJefe").AddComponent<ConductorJefe>();
        }
        else if (estado == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Clave + ".Golpes", false))
        {
            SessionState.SetBool(Clave + ".Golpes", false);
            new GameObject("PruebaGolpes").AddComponent<ConductorGolpes>();
        }
        else if (estado == PlayModeStateChange.EnteredPlayMode)
            new GameObject("PruebaCueva").AddComponent<Conductor>();
        else if (estado == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Clave, false);
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
    }

    public static void LanzarVida()
    {
        SessionState.SetBool(Clave, true);
        SessionState.SetBool(Clave + ".Vida", true);
        EditorSceneManager.OpenScene(Escena);
        ArranquePruebas.Proteger();
        EditorApplication.EnterPlaymode();
    }

    private class ConductorVida : MonoBehaviour
    {
        private static readonly System.Reflection.BindingFlags Priv = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

        private IEnumerator Start()
        {
            yield return new WaitForSeconds(1.5f);
            PlayerControler p = Object.FindFirstObjectByType<PlayerControler>();
            PlayerStamina est = p.GetComponent<PlayerStamina>();
            System.Func<PlayerControler, object> vida = x => typeof(PlayerControler).GetField("currentHealth", Priv).GetValue(x);
            Debug.Log($"[Vida] inicio: vida={vida(p)} estamina={est.Current}");

            // Un "atacante" delante del player.
            int dir = p.transform.localScale.x >= 0f ? 1 : -1;
            Transform atacante = new GameObject("Atacante").transform;
            atacante.position = p.transform.position + Vector3.right * dir;

            // Bloqueo mantenido (fuera de la ventana de parry).
            typeof(PlayerControler).GetField("isBlocking", Priv).SetValue(p, true);
            typeof(PlayerControler).GetField("tBloqueo", Priv).SetValue(p, 1f);
            typeof(PlayerControler).GetField("ventanaParryActual", Priv).SetValue(p, 0.2f);
            float e0 = est.Current;
            var r = p.TakeDamage(40, atacante);
            Debug.Log($"[Vida] bloqueo: resultado={r} vida={vida(p)} estamina {e0:0} -> {est.Current:0}");

            // Parry.
            yield return new WaitForSeconds(0.3f);
            typeof(PlayerControler).GetField("isBlocking", Priv).SetValue(p, true);
            typeof(PlayerControler).GetField("tBloqueo", Priv).SetValue(p, 0.05f);
            e0 = est.Current;
            r = p.TakeDamage(40, atacante);
            Debug.Log($"[Vida] parry: resultado={r} vida={vida(p)} estamina {e0:0} -> {est.Current:0}");

            // Pociones: beber una, morir, y reaparecer con todas.
            yield return new WaitForSeconds(0.5f);
            p.GetComponent<GatherInput>().IsHealing = true;
            yield return new WaitForSeconds(1.3f);
            ReservaPociones res = ReservaPociones.Get();
            Debug.Log($"[Vida] tras beber: vida={vida(p)} cargas={res.Cargas}/{res.Maximo}");
            p.TakeDamage(999);
            yield return new WaitForSecondsRealtime(1.2f);
            Debug.Log($"[Vida] muerto: pantalla activa={PantallaMuerte.Activa} player={(Object.FindFirstObjectByType<PlayerControler>() != null)}");
            yield return new WaitForSecondsRealtime(4f);
            p = Object.FindFirstObjectByType<PlayerControler>();
            Debug.Log($"[Vida] reaparecido: player={(p != null)} vida={(p != null ? vida(p) : "-")} cargas={res.Cargas}/{res.Maximo} pantalla activa={PantallaMuerte.Activa}");

            Debug.Log("[Prueba] fin");
            EditorApplication.ExitPlaymode();
        }
    }

    public static void LanzarUI()
    {
        SessionState.SetBool(Clave, true);
        SessionState.SetBool(Clave + ".UI", true);
        EditorSceneManager.OpenScene(Escena);
        ArranquePruebas.Proteger();
        EditorApplication.EnterPlaymode();
    }

    private class ConductorUI : MonoBehaviour
    {
        private string carpeta;

        private IEnumerator Start()
        {
            carpeta = Path.Combine(Directory.GetCurrentDirectory(), "PruebaCueva");
            Directory.CreateDirectory(carpeta);
            yield return new WaitForSeconds(1f);

            // Pausa.
            GameManager.Instance.PauseGame();
            yield return new WaitForSecondsRealtime(0.4f);
            Debug.Log($"[UI] pausa: timeScale={Time.timeScale} menu={MenuPausa.Get().gameObject.activeSelf}");
            yield return CapturaConUI("ui_pausa");

            // Destrabar: se manda al player a un sitio raro y se destraba.
            PlayerControler p = Object.FindFirstObjectByType<PlayerControler>();
            Vector2 antes = p.transform.position;
            p.transform.position = new Vector2(10f, -5f);
            typeof(MenuPausa).GetMethod("Destrabar", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(MenuPausa.Get(), null);
            yield return new WaitForSeconds(0.2f);
            Debug.Log($"[UI] destrabar: timeScale={Time.timeScale} antes={antes} ahora={p.transform.position}");

            // Muerte en pleno combate con el jefe, con sus ataques en el aire.
            p.transform.position = new Vector2(152f, 4.8f);
            yield return new WaitForSeconds(2f);
            p = Object.FindFirstObjectByType<PlayerControler>();
            if (p != null) p.CurarCompleto();
            yield return new WaitForSeconds(7f);
            JefeWraith j = Object.FindFirstObjectByType<JefeWraith>();
            if (j != null) { EnemyHealth s = j.GetComponent<EnemyHealth>(); s.TakeDamage(s.CurrentHealth - s.MaxHealth / 2 + 5, j.transform.position); }
            yield return new WaitForSeconds(5f);
            Debug.Log($"[UI] antes de morir: efectos={Object.FindObjectsByType<EfectoVisual>(FindObjectsSortMode.None).Length}");
            p = Object.FindFirstObjectByType<PlayerControler>();
            while (p != null) { p.TakeDamage(999); yield return null; p = Object.FindFirstObjectByType<PlayerControler>(); }
            yield return new WaitForSecondsRealtime(1.2f);
            yield return CapturaConUI("ui_muerte");
            yield return new WaitForSeconds(3f);
            int bucles = 0;
            foreach (EfectoVisual e in Object.FindObjectsByType<EfectoVisual>(FindObjectsSortMode.None))
            {
                var campo = typeof(EfectoVisual).GetField("vida", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if ((float)campo.GetValue(e) <= 0f) { bucles++; Debug.Log($"[UI] efecto atascado: {e.name} en {e.transform.position}"); }
            }
            Debug.Log($"[UI] tras reaparecer: efectos en bucle que quedan={bucles} jefes={Object.FindObjectsByType<JefeWraith>(FindObjectsSortMode.None).Length}");

            // Reiniciar desde el punto de control.
            GameManager.Instance.ReiniciarDesdeCheckpoint();
            yield return new WaitForSeconds(2f);
            p = Object.FindFirstObjectByType<PlayerControler>();
            Debug.Log($"[UI] reiniciar desde checkpoint: player={(p != null ? p.transform.position.ToString() : "null")}");

            Debug.Log("[Prueba] fin");
            EditorApplication.ExitPlaymode();
        }

        // Las capturas con Camera.Render no incluyen los canvas en modo Overlay:
        // se pasan un momento a la camara para que salgan.
        private IEnumerator CapturaConUI(string nombre)
        {
            Camera cam = Camera.main;
            var cambiados = new List<Canvas>();
            foreach (Canvas c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay && c.gameObject.activeInHierarchy)
                { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = cam; c.planeDistance = 1f; c.sortingLayerName = "VFX"; c.sortingOrder = 500 + c.sortingOrder; cambiados.Add(c); }
            Canvas.ForceUpdateCanvases();
            yield return null;
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

    public static void LanzarParte4()
    {
        SessionState.SetBool(Clave, true);
        SessionState.SetBool(Clave + ".Parte4", true);
        EditorSceneManager.OpenScene(Escena);
        ArranquePruebas.Proteger();
        EditorApplication.EnterPlaymode();
    }

    private class ConductorParte4 : MonoBehaviour
    {
        private IEnumerator Start()
        {
            string carpeta = Path.Combine(Directory.GetCurrentDirectory(), "PruebaCueva");
            Directory.CreateDirectory(carpeta);
            yield return new WaitForSeconds(1f);
            PlayerControler p = Object.FindFirstObjectByType<PlayerControler>();
            GatherInput gi = p.GetComponent<GatherInput>();
            var vida = typeof(PlayerControler).GetField("currentHealth", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            ReservaPociones r = ReservaPociones.Get();
            Debug.Log($"[P4] inicio: vida={vida.GetValue(p)} cargas={r.Cargas}/{r.Maximo}");

            // Herir y beber.
            p.transform.position = new Vector2(6f, 0.7f);
            for (int i = 0; i < 3; i++) { p.TakeDamage(20); yield return new WaitForSeconds(1.3f); }
            Debug.Log($"[P4] herido: vida={vida.GetValue(p)}");
            gi.IsHealing = true;
            yield return new WaitForSeconds(0.3f);
            Captura(carpeta, "p4_bebiendo");
            yield return new WaitForSeconds(1f);
            Debug.Log($"[P4] tras beber: vida={vida.GetValue(p)} cargas={r.Cargas}/{r.Maximo} estado={p.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).shortNameHash == Animator.StringToHash("UsarObjeto")}");

            // Tajos contra un slime quieto.
            foreach (EnemyHealth e in Object.FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None))
                if (Vector2.Distance(e.transform.position, p.transform.position) < 10f) Destroy(e.gameObject);
            GameObject slime = Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/Cueva/Slime.prefab"),
                                           new Vector3(7.2f, 0f, 0f), Quaternion.identity);
            foreach (EnemigoBase ia in slime.GetComponents<EnemigoBase>()) ia.enabled = false;
            yield return new WaitForSeconds(0.3f);
            for (int g = 0; g < 3; g++)
            {
                p.DealAttackCombo(g);
                yield return new WaitForSecondsRealtime(0.12f);
                Captura(carpeta, "p4_tajo" + g);
                yield return new WaitForSecondsRealtime(0.4f);
                Time.timeScale = 1f;
            }

            // Recompensa del Mimic.
            SoltarRecompensa mimic = Object.FindFirstObjectByType<SoltarRecompensa>();
            Debug.Log($"[P4] mimic con recompensa: {mimic != null}");
            if (mimic != null)
            {
                Vector2 pm = mimic.transform.position;
                mimic.GetComponent<EnemyHealth>().TakeDamage(9999, pm);
                yield return new WaitForSeconds(0.5f);
                Debug.Log($"[P4] frasco en el suelo: {Object.FindFirstObjectByType<FrascoExtra>() != null}");
                p.transform.position = pm + Vector2.up * 0.7f;
                yield return new WaitForSeconds(0.6f);
                Debug.Log($"[P4] tras recoger: cargas={r.Cargas}/{r.Maximo} cogida={r.RecompensaCogida}");
            }

            Debug.Log("[Prueba] fin");
            EditorApplication.ExitPlaymode();
        }

        private static void Captura(string carpeta, string nombre)
        {
            Camera cam = Camera.main;
            if (cam == null) return;
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
        }
    }

    public static void LanzarReinicioJefe()
    {
        SessionState.SetBool(Clave, true);
        SessionState.SetBool(Clave + ".Reinicio", true);
        EditorSceneManager.OpenScene(Escena);
        ArranquePruebas.Proteger();
        EditorApplication.EnterPlaymode();
    }

    // Fase 2, muerte del player, reinicio del combate y segundo intento.
    private class ConductorReinicio : MonoBehaviour
    {
        private IEnumerator Start()
        {
            yield return new WaitForSeconds(1f);
            PlayerControler p = Object.FindFirstObjectByType<PlayerControler>();
            p.transform.position = new Vector2(152f, 4.8f);
            yield return Vigilar("intento1", 4f, true);

            JefeWraith j = Object.FindFirstObjectByType<JefeWraith>();
            EnemyHealth s = j.GetComponent<EnemyHealth>();
            s.TakeDamage(s.CurrentHealth - s.MaxHealth / 2 + 5, j.transform.position);
            yield return Vigilar("fase2", 8f, true);

            p = Object.FindFirstObjectByType<PlayerControler>();
            Debug.Log("[Reinicio] matando al player");
            for (int i = 0; i < 10 && p != null; i++) { p.TakeDamage(999); yield return new WaitForSeconds(1.1f); p = Object.FindFirstObjectByType<PlayerControler>(); }
            yield return new WaitForSeconds(3f);
            p = Object.FindFirstObjectByType<PlayerControler>();
            Debug.Log($"[Reinicio] tras morir: player={(p != null ? p.transform.position.ToString("0.0") : "null")} jefes={Object.FindObjectsByType<JefeWraith>(FindObjectsSortMode.None).Length}");

            p.transform.position = new Vector2(152f, 4.8f);
            yield return Vigilar("intento2", 4f, true);
            j = Object.FindFirstObjectByType<JefeWraith>();
            if (j != null)
            {
                s = j.GetComponent<EnemyHealth>();
                s.TakeDamage(s.CurrentHealth - s.MaxHealth / 2 + 5, j.transform.position);
            }
            yield return Vigilar("fase2_bis", 30f, true);
            Debug.Log("[Prueba] fin");
            EditorApplication.ExitPlaymode();
        }

        private IEnumerator Vigilar(string etiqueta, float segundos, bool curar)
        {
            for (float t = 0f; t < segundos; t += 0.5f)
            {
                yield return new WaitForSeconds(0.5f);
                PlayerControler p = Object.FindFirstObjectByType<PlayerControler>();
                if (curar && p != null) p.CurarCompleto();
                JefeWraith j = Object.FindFirstObjectByType<JefeWraith>();
                AnimadorHoja a = j != null ? j.GetComponentInChildren<AnimadorHoja>() : null;
                EnemyHealth s = j != null ? j.GetComponent<EnemyHealth>() : null;
                Debug.Log($"[Reinicio] {etiqueta} t={t + 0.5f:0.0} player={(p != null ? p.transform.position.ToString("0.0") : "null")} " +
                          $"jefe={(s != null ? s.CurrentHealth.ToString() : "-")} pose={(a != null ? a.Actual : "-")}");
            }
        }
    }

    public static void LanzarJefe()
    {
        SessionState.SetBool(Clave, true);
        SessionState.SetBool(Clave + ".Jefe", true);
        EditorSceneManager.OpenScene(Escena);
        ArranquePruebas.Proteger();
        EditorApplication.EnterPlaymode();
    }

    // Entra en la arena, deja que el jefe haga de todo (curando al player para que
    // no se acabe la prueba), fuerza la fase 2 y comprueba la debilidad.
    private class ConductorJefe : MonoBehaviour
    {
        private string carpeta;

        private IEnumerator Start()
        {
            carpeta = Path.Combine(Directory.GetCurrentDirectory(), "PruebaCueva");
            Directory.CreateDirectory(carpeta);
            yield return new WaitForSeconds(1f);

            PlayerControler p = Object.FindFirstObjectByType<PlayerControler>();
            p.transform.position = new Vector2(152f, 4.8f);
            Debug.Log("[Jefe] player en la arena");

            JefeWraith jefe = null;
            for (float t = 0f; t < 36f; t += 0.5f)
            {
                yield return new WaitForSeconds(0.5f);
                p = Object.FindFirstObjectByType<PlayerControler>();
                if (p != null) p.CurarCompleto();
                // A ratos lejos, para que use la embestida, los orbes y el teletransporte.
                if (p != null && (Mathf.Approximately(t, 4f) || Mathf.Approximately(t, 26f))) p.transform.position = new Vector2(172f, 4.8f);
                if (p != null && Mathf.Approximately(t, 11f)) p.transform.position = new Vector2(152f, 4.8f);
                if (jefe == null) jefe = Object.FindFirstObjectByType<JefeWraith>();
                EnemyHealth s = jefe != null ? jefe.GetComponent<EnemyHealth>() : null;
                AnimadorHoja a = jefe != null ? jefe.GetComponentInChildren<AnimadorHoja>() : null;
                string cam = Camera.main != null ? Camera.main.orthographicSize.ToString("0.00") : "-";
                Debug.Log($"[Jefe] t={t + 0.5f:0.0} cam={cam} jefe={(s != null ? s.CurrentHealth.ToString() : "-")} " +
                          $"pose={(a != null ? a.Actual : "-")} pos={(jefe != null ? jefe.transform.position.ToString("0.0") : "-")} " +
                          $"peligros={Object.FindObjectsByType<OndaCarmesi>(FindObjectsSortMode.None).Length}o/" +
                          $"{Object.FindObjectsByType<PilarSangre>(FindObjectsSortMode.None).Length}p/" +
                          $"{Object.FindObjectsByType<OrbeSangre>(FindObjectsSortMode.None).Length}b/" +
                          $"{Object.FindObjectsByType<Meteoro>(FindObjectsSortMode.None).Length}m");
                if (Mathf.Approximately(t % 1f, 0f)) Captura($"jefe_{t + 0.5f:00.0}");

                // A los 15 s se le baja la vida para ver la fase 2 y la debilidad.
                if (Mathf.Approximately(t, 15f) && s != null)
                {
                    s.TakeDamage(s.CurrentHealth - s.MaxHealth / 2 + 5, jefe.transform.position);
                    Debug.Log($"[Jefe] vida bajada a {s.CurrentHealth}");
                }
                if (Mathf.Approximately(t, 22f) && s != null)
                {
                    int v0 = s.CurrentHealth;
                    EnemyHealth.ArmaDelGolpe = TipoArma.Punos;
                    s.TakeDamage(10, jefe.transform.position);
                    int v1 = s.CurrentHealth;
                    EnemyHealth.ArmaDelGolpe = TipoArma.Espada;
                    s.TakeDamage(10, jefe.transform.position);
                    Debug.Log($"[Jefe] debilidad: punos 10 -> {v0 - v1}, espada 10 -> {v1 - s.CurrentHealth}");
                }
            }

            Debug.Log("[Prueba] fin");
            EditorApplication.ExitPlaymode();
        }

        private void Captura(string nombre)
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            const int w = 1280, h = 720;
            RenderTexture rt = new RenderTexture(w, h, 24);
            RenderTexture previa = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = previa;
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(carpeta, nombre + ".png"), tex.EncodeToPNG());
            Destroy(tex);
            rt.Release();
        }
    }

    private class ConductorGolpes : MonoBehaviour
    {
        private IEnumerator Start()
        {
            yield return new WaitForSeconds(1f);
            // Fuera los enemigos del nivel, para que no molesten.
            foreach (EnemyHealth e in Object.FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None)) Destroy(e.gameObject);
            yield return null;

            PlayerControler p = Object.FindFirstObjectByType<PlayerControler>();
            Vector2 pos = new Vector2(6f, 0.62f);
            p.transform.position = pos;
            int dir = p.transform.localScale.x >= 0f ? 1 : -1;

            string[] tipos = { "Slime", "Mimic", "Mago", "Cacodemonio" };
            float[] distancias = { -0.4f, 0f, 0.3f, 0.6f, 1.0f, 1.5f, 2.0f };
            foreach (string tipo in tipos)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Enemies/Cueva/{tipo}.prefab");
                foreach (float d in distancias)
                {
                    string fila = "";
                    for (int golpe = 0; golpe < 3; golpe++)
                    {
                        float y = tipo == "Cacodemonio" ? pos.y + 0.2f : 0f;
                        GameObject e = Instantiate(prefab, new Vector3(pos.x + d * dir, y, 0f), Quaternion.identity);
                        foreach (EnemigoBase ia in e.GetComponents<EnemigoBase>()) ia.enabled = false;
                        e.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
                        yield return new WaitForFixedUpdate();
                        EnemyHealth salud = e.GetComponent<EnemyHealth>();
                        int antes = salud.CurrentHealth;
                        p.DealAttackCombo(golpe);
                        fila += salud.CurrentHealth < antes ? " SI" : " no";
                        Destroy(e);
                        yield return new WaitForSecondsRealtime(0.15f);
                        Time.timeScale = 1f;
                    }
                    Debug.Log($"[Golpes] {tipo,-12} d={d,5:0.0} tajos 1-3:{fila}");
                }
            }
            Debug.Log("[Prueba] fin");
            EditorApplication.ExitPlaymode();
        }
    }

    private class Conductor : MonoBehaviour
    {
        private string carpeta;

        private IEnumerator Start()
        {
            carpeta = Path.Combine(Directory.GetCurrentDirectory(), "PruebaCueva");
            Directory.CreateDirectory(carpeta);

            yield return new WaitForSeconds(1.5f);

            // Grupo, posicion del player y segundos de observacion.
            var escenas = new List<(string nombre, Vector2 pos, float segundos)>
            {
                ("slime_mago", new Vector2(28.5f, 0.8f), 8f),
                ("volador_pozo", new Vector2(45f, 18f), 7f),
                ("mago_cerca", new Vector2(30.8f, 0.8f), 6f),
                ("mimic", new Vector2(76.9f, 8.8f), 7f),
                ("volador_caida", new Vector2(110.5f, 8.8f), 7f),
                ("arena", new Vector2(158f, 4.8f), 3f),
            };

            foreach (var (nombre, pos, segundos) in escenas)
            {
                PlayerControler p = Object.FindFirstObjectByType<PlayerControler>();
                if (p == null) { Debug.Log("[Prueba] sin player"); yield return new WaitForSeconds(3f); p = Object.FindFirstObjectByType<PlayerControler>(); }
                if (p == null) continue;

                p.transform.position = pos;
                p.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                p.CurarCompleto();
                Debug.Log($"[Prueba] === {nombre} === player en {pos}");

                float t = 0f;
                int n = 0;
                while (t < segundos)
                {
                    yield return new WaitForSeconds(0.5f);
                    t += 0.5f;
                    Estado(nombre, t);
                    if (n++ % 2 == 0) Captura($"{nombre}_{t:0.0}");
                }
            }

            Debug.Log("[Prueba] fin");
            EditorApplication.ExitPlaymode();
        }

        private void Estado(string nombre, float t)
        {
            PlayerControler p = Object.FindFirstObjectByType<PlayerControler>();
            string vida = p != null ? GetVida(p) : "muerto";
            string enemigos = "";
            foreach (EnemyHealth e in Object.FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None))
            {
                if (p == null || Vector2.Distance(e.transform.position, p.transform.position) > 12f) continue;
                AnimadorHoja a = e.GetComponentInChildren<AnimadorHoja>();
                enemigos += $" {e.name}[{e.CurrentHealth} {(a != null ? a.Actual : "-")} ({e.transform.position.x:0.0},{e.transform.position.y:0.0})]";
            }
            Debug.Log($"[Prueba] {nombre} t={t:0.0} cam={(Camera.main != null ? Camera.main.orthographicSize : 0f):0.00} player={vida} ({(p != null ? p.transform.position.ToString("0.0") : "")}){enemigos}");
        }

        private static string GetVida(PlayerControler p)
        {
            var f = typeof(PlayerControler).GetField("currentHealth", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return f != null ? f.GetValue(p).ToString() : "?";
        }

        private void Captura(string nombre)
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            const int w = 1280, h = 720;
            RenderTexture rt = new RenderTexture(w, h, 24);
            RenderTexture previa = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = previa;
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(carpeta, nombre + ".png"), tex.EncodeToPNG());
            Destroy(tex);
            rt.Release();
        }
    }
}
