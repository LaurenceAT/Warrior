using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Pruebas de la Ronda 18: la entrada a las zonas de los jefes (linea de comandos, sin -quit):
//   ... -executeMethod PruebaRonda18.Nieve     (Sombra de los Humedales, partida normal)
//   ... -executeMethod PruebaRonda18.Cueva     (Crimson Wraith, partida normal)
//   ... -executeMethod PruebaRonda18.Cazadora  (desafio de The Blind Huntress: dialogo y "Retirarme")
// Escribe "[R18] OK ..." / "[R18] FALLO ..." y el total. Capturas en PruebaNieve/r18_*.png.
[InitializeOnLoad]
public static class PruebaRonda18
{
    private const string Clave = "PruebaRonda18.Modo";

    static PruebaRonda18()
    {
        EditorApplication.playModeStateChanged += e =>
        {
            string modo = SessionState.GetString(Clave, "");
            if (modo == "") return;
            if (e == PlayModeStateChange.EnteredPlayMode) new GameObject("PruebaRonda18").AddComponent<Conductor>().modo = modo;
            else if (e == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetString(Clave, "");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
        };
    }

    // Se arranca desde el menu y el nivel se carga ya con la carpeta de pruebas puesta
    // (si no, los logros por llegar a la escena irian al progreso real).
    public static void Nieve() => Lanzar("nieve", "Assets/Scenes/Menu Principal.unity");
    public static void Cueva() => Lanzar("cueva", "Assets/Scenes/Menu Principal.unity");
    public static void Cazadora() => Lanzar("cazadora", "Assets/Scenes/Menu Principal.unity");

    private static void Lanzar(string modo, string escena)
    {
        SessionState.SetString(Clave, modo);
        EditorSceneManager.OpenScene(escena);
        ArranquePruebas.Proteger();
        EditorApplication.EnterPlaymode();
    }

    private class Conductor : MonoBehaviour
    {
        public string modo;
        private string carpeta;
        private int ok, fallos, errores;
        private PlayerControler p;
        private const BindingFlags Todo = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static;

        private void Ok(string s) { ok++; Debug.Log("[R18] OK " + s); }
        private void Fallo(string s) { fallos++; Debug.LogWarning("[R18] FALLO " + s); }
        private void Comprobar(bool c, string s) { if (c) Ok(s); else Fallo(s); }

        private IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject);
            Application.logMessageReceived += (c, st, t) => { if ((t == LogType.Exception || t == LogType.Error) && !c.StartsWith("[R18]")) { errores++; Debug.Log("[R18] error visto: " + c); } };
            carpeta = Path.Combine(Directory.GetCurrentDirectory(), "PruebaNieve");
            Partida.CarpetaPruebas = Path.Combine(carpeta, "PartidasR18");
            if (Directory.Exists(Partida.CarpetaPruebas)) Directory.Delete(Partida.CarpetaPruebas, true);
            Directory.CreateDirectory(Partida.CarpetaPruebas);
            Globales.Recargar();
            GameManager.AlReaparecerPlayer += () => Debug.Log("[R18] (el player reaparece)");
            ArenaJefe.AlVencer += () => Debug.Log("[R18] (AlVencer)");
            IEnumerator r = modo == "cazadora" ? PruebaCazadora() : PruebaNivel(modo == "nieve" ? "Nivel Nieve" : "Nivel Cueva");
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
            Debug.Log($"[R18] RESULTADO {modo}: {ok} OK, {fallos} FALLOS");
            Time.timeScale = 1f;
            // La carpeta de pruebas se queda puesta: lo que se guarde al salir no toca el progreso real.
            Partida.Descargar();
            Globales.GuardarPendiente();
            EditorApplication.ExitPlaymode();
        }

        // ------------------------------------------------------------------ Nieve y Cueva (partida normal)

        private IEnumerator PruebaNivel(string escena)
        {
            string e = escena == "Nivel Nieve" ? "nieve" : "cueva";
            yield return new WaitForSeconds(1f);
            Partida.Nueva(escena);
            Equipo.Reiniciar();
            SceneManager.LoadScene(escena);
            yield return EsperarEscena(escena);
            yield return new WaitForSeconds(2f);
            p = Object.FindFirstObjectByType<PlayerControler>();
            p.InvulnerableExterno = true;
            ZonaJefe z = Object.FindFirstObjectByType<ZonaJefe>();
            ArenaJefe arena = Object.FindFirstObjectByType<ArenaJefe>();
            Comprobar(z != null && z.Muro != null && arena != null && ReferenceEquals(z.Arena, arena) && arena.ZonaEntrada == z, $"{e}: la zona de jefe esta enlazada con su arena");
            if (z == null || arena == null) yield break;
            MuroNiebla muro = z.Muro;
            float xm = z.transform.position.x, suelo = z.transform.position.y;
            float xAct = xm + z.distanciaActivador;

            // Antes de la pelea: nada de niebla, y la niebla vieja apagada.
            Comprobar(muro.Estado == MuroNiebla.EstadoMuro.Abierto && !muro.Bloquea && !Dibujo(muro).enabled, $"{e}: antes de la pelea no hay niebla visible ni bloqueo");
            Comprobar(!Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Any(t => t.name.StartsWith("NieblaJefe")), $"{e}: la niebla antigua no esta activa");

            // Seguridad: el muro nunca se forma sobre el player (en el aire, justo encima).
            Teletransportar(new Vector3(xm + 0.05f, suelo + 3f, 0f));
            z.ForzarCierre();
            yield return new WaitForFixedUpdate();
            Comprobar(p.transform.position.x > xm + muro.anchoBloqueo * 0.5f, $"{e}: al cerrarse encima del player, este queda del lado de la arena (x={p.transform.position.x:0.00})");
            z.ForzarApertura();
            yield return EsperarMuro(muro, MuroNiebla.EstadoMuro.Abierto, 4f);

            // Se entra y se sale libremente antes del activador.
            Teletransportar(new Vector3(xm + 2f, suelo + 1f, 0f));
            yield return new WaitForSeconds(1f);
            Comprobar(!ArenaJefe.EnCombate && z.Armado && z.Disparos == 0, $"{e}: dentro de la arena pero antes del activador no empieza nada");
            yield return Captura($"r18_{e}_1_antes");
            Teletransportar(new Vector3(xm - 2f, suelo + 1f, 0f));
            yield return new WaitForSeconds(0.5f);
            Comprobar(p.transform.position.x < xm - 1f && !muro.Bloquea, $"{e}: se puede volver atras sin pelea");

            // Activador: empieza la pelea y la niebla se forma detras.
            Teletransportar(new Vector3(xAct + 1f, suelo + 1f, 0f));
            yield return Esperar(() => ArenaJefe.EnCombate, 3f);
            Comprobar(ArenaJefe.EnCombate && z.Disparos == 1 && !z.Armado, $"{e}: al pisar el activador empieza la pelea ({z.Disparos} disparo)");
            Comprobar(muro.Bloquea && muro.Estado == MuroNiebla.EstadoMuro.Formando, $"{e}: el muro bloquea al momento y se esta formando");
            Teletransportar(new Vector3(xm + 2.5f, suelo + 1f, 0f));
            yield return new WaitForSeconds(0.35f);
            yield return CapturaMuro($"r18_{e}_m2_formando", muro);
            yield return Captura($"r18_{e}_2_formando");
            yield return new WaitForSeconds(1f);
            Comprobar(muro.Estado == MuroNiebla.EstadoMuro.Cerrado && Dibujo(muro).enabled, $"{e}: el muro queda formado en ~1 s");
            Comprobar(Object.FindFirstObjectByType<BarraJefe>() != null, $"{e}: aparece la barra del jefe");
            AudioSource musica = (AudioSource)Campo(arena, "audioSrc");
            Comprobar(musica != null && musica.isPlaying, $"{e}: suena la musica del jefe");
            AudioSource bucle = ((AudioSource[])muro.GetComponents<AudioSource>()).FirstOrDefault(a => a.loop);
            Comprobar(bucle != null && bucle.isPlaying, $"{e}: el sonido continuo del muro suena");
            yield return new WaitForSeconds(0.6f);
            yield return Captura($"r18_{e}_3_cerrado");
            yield return CapturaMuro($"r18_{e}_m3_cerrado", muro);
            yield return new WaitForSeconds(0.25f);
            yield return CapturaMuro($"r18_{e}_m3b_cerrado", muro);

            // Empujon fuerte hacia la entrada: el muro lo para sin dejarlo pasar.
            Teletransportar(new Vector3(xm + 1.2f, suelo + 1f, 0f));
            Rigidbody2D rb = p.GetComponent<Rigidbody2D>();
            float minimo = xm + muro.anchoBloqueo * 0.5f;
            float peor = float.MaxValue;
            for (int i = 0; i < 12; i++)
            {
                if (i < 4) rb.linearVelocity = new Vector2(-60f, 3f);
                yield return new WaitForFixedUpdate();
                peor = Mathf.Min(peor, p.transform.position.x);
            }
            Comprobar(peor >= minimo - 0.05f, $"{e}: un empujon a 60 u/s no atraviesa el muro (x min {peor:0.00}, muro {minimo:0.00})");
            Teletransportar(new Vector3(xm - 1.5f, suelo + 1f, 0f));
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Comprobar(p.transform.position.x > minimo, $"{e}: con el muro cerrado nunca se queda fuera (x={p.transform.position.x:0.00})");

            // El activador no se dispara dos veces.
            Teletransportar(new Vector3(xAct + 2f, suelo + 1f, 0f));
            yield return new WaitForSeconds(0.5f);
            Comprobar(z.Disparos == 1, $"{e}: el activador no se dispara dos veces");

            // Muerte: al reaparecer el muro se disuelve, el jefe se va y el activador se arma.
            p.InvulnerableExterno = false;
            SetCampo(p, "isInvincible", false);
            p.TakeDamage(99999);
            yield return EsperarReaparecer();
            p.InvulnerableExterno = true;
            Comprobar(!muro.Bloquea && z.Armado && !ArenaJefe.EnCombate && Object.FindFirstObjectByType<JefeBase>() == null,
                      $"{e}: al reaparecer el muro se abre, el jefe se reinicia y el activador vuelve a estar listo");
            yield return EsperarMuro(muro, MuroNiebla.EstadoMuro.Abierto, 4f);
            Comprobar(muro.Estado == MuroNiebla.EstadoMuro.Abierto && z.Disparos == 1 && p.transform.position.x < xm,
                      $"{e}: reaparecer en la hoguera no dispara el activador (player x={p.transform.position.x:0.0})");

            // Se vuelve a entrar: se forma otra vez.
            Teletransportar(new Vector3(xAct + 1f, suelo + 1f, 0f));
            yield return Esperar(() => ArenaJefe.EnCombate, 3f);
            Comprobar(ArenaJefe.EnCombate && z.Disparos == 2 && muro.Bloquea, $"{e}: al volver a entrar el muro se forma de nuevo");
            JefeBase jefe = null;
            yield return Esperar(() => (jefe = Object.FindFirstObjectByType<JefeBase>()) != null, 8f);
            yield return new WaitForSeconds(3f);

            // Victoria: el muro se disuelve y la zona queda abierta. (Solo en la prueba:
            // sin zonas de caida mortal, para que el player no muera mientras.)
            foreach (DeadArea d in Object.FindObjectsByType<DeadArea>(FindObjectsSortMode.None))
                foreach (Collider2D c in d.GetComponents<Collider2D>()) c.enabled = false;
            EnemyHealth s = jefe != null ? jefe.GetComponent<EnemyHealth>() : null;
            if (s != null)
            {
                s.DanoEstado(Mathf.RoundToInt(s.MaxHealth * 0.6f));
                yield return Esperar(() => ArenaJefe.EnFase2, 25f);
                yield return new WaitForSeconds(6f);
                for (int i = 0; i < 3 && ArenaJefe.EnCombate; i++)
                {
                    if (jefe != null) jefe.GetComponent<EnemyHealth>().DanoEstado(99999);
                    yield return Esperar(() => !ArenaJefe.EnCombate, 9f);
                }
            }
            if (ArenaJefe.EnCombate) { Debug.Log("[R18] el jefe no cayo con dano directo: se fuerza la victoria"); Llamar(arena, "Victoria"); }
            yield return Esperar(() => muro.Estado == MuroNiebla.EstadoMuro.Disolviendo, 6f);
            Comprobar(muro.Estado == MuroNiebla.EstadoMuro.Disolviendo && !muro.Bloquea, $"{e}: al caer el jefe el muro se disuelve (suave)");
            Teletransportar(new Vector3(xm + 2.5f, suelo + 1f, 0f));
            yield return new WaitForSeconds(0.3f);
            yield return CapturaMuro($"r18_{e}_m4_disolviendo", muro);
            yield return Captura($"r18_{e}_4_disolviendo");
            yield return EsperarMuro(muro, MuroNiebla.EstadoMuro.Abierto, 5f);
            Comprobar(muro.Estado == MuroNiebla.EstadoMuro.Abierto && !Dibujo(muro).enabled, $"{e}: tras disolverse no queda niebla");
            Teletransportar(new Vector3(xAct + 1f, suelo + 1f, 0f));
            yield return new WaitForSeconds(1f);
            Teletransportar(new Vector3(xm - 2f, suelo + 1f, 0f));
            yield return new WaitForSeconds(0.5f);
            Comprobar(z.Disparos == 2 && !muro.Bloquea && p.transform.position.x < xm - 1f, $"{e}: vencido, la niebla no vuelve y se puede salir (disparos {z.Disparos}, bloquea {muro.Bloquea}, x {p.transform.position.x:0.0})");
            Comprobar(Partida.Bandera("jefe_" + escena), $"{e}: el jefe queda vencido en la partida");

            // Lo vencido sigue vencido: al volver a cargar el nivel la zona sigue abierta.
            SceneManager.LoadScene(escena);
            yield return EsperarEscena(escena);
            yield return new WaitForSeconds(2f);
            p = Object.FindFirstObjectByType<PlayerControler>();
            p.InvulnerableExterno = true;
            z = Object.FindFirstObjectByType<ZonaJefe>();
            Teletransportar(new Vector3(xAct + 1f, suelo + 1f, 0f));
            yield return new WaitForSeconds(1.5f);
            Comprobar(!ArenaJefe.EnCombate && !z.Muro.Bloquea && z.Disparos == 0, $"{e}: tras recargar, el jefe vencido no vuelve ni la niebla bloquea");
        }

        // ------------------------------------------------------------------ Cazadora (desafio)

        private IEnumerator PruebaCazadora()
        {
            yield return new WaitForSeconds(2f);
            FichaJefe caz = FichaJefe.DeId("blind_huntress");
            Globales.PonerMarca(CodigosSecretos.Marca(caz.id), true);
            Globales.PonerMarca("revelado_" + caz.id, true);
            Globales.PonerMarca("dialogo_cazadora", false);
            Desafio.Empezar(caz, false);
            yield return EsperarEscena(caz.escena);
            yield return new WaitForSeconds(2.5f);
            p = Object.FindFirstObjectByType<PlayerControler>();
            p.InvulnerableExterno = true;
            ZonaJefe z = Object.FindFirstObjectByType<ZonaJefe>();
            ArenaCazadora arena = ArenaCazadora.Actual;
            Comprobar(z != null && arena != null && arena.ZonaEntrada == z, "cazadora: la zona esta enlazada con su arena");
            if (z == null || arena == null) yield break;
            MuroNiebla muro = z.Muro;
            float xm = z.transform.position.x, suelo = z.transform.position.y;
            Comprobar(muro.Estado == MuroNiebla.EstadoMuro.Abierto && !muro.Bloquea, "cazadora: antes de la pelea no hay niebla");

            // Dialogo sin frases que esperen una tecla: se llega directo a la eleccion.
            AjustesCazadora aj = (AjustesCazadora)Campo(arena, "ajustes");
            AjustesCazadora copia = Object.Instantiate(aj);
            copia.frasesEntrada = new string[0];
            copia.respuestaRetirarse = new string[0];
            SetCampo(arena, "ajustes", copia);

            Teletransportar(new Vector3(xm + z.distanciaActivador + 1f, suelo + 0.8f, 0f));
            DialogoCazadora dlg = null;
            yield return Esperar(() => (dlg = Object.FindFirstObjectByType<DialogoCazadora>()) != null && (int)Campo(dlg, "eleccion") < 0 && dlg.GetComponentsInChildren<UnityEngine.UI.Button>().Length >= 2, 8f);
            Comprobar(dlg != null && Cinematica.Activa, "cazadora: al pisar el activador empieza su dialogo");
            Comprobar(!muro.Bloquea && muro.Estado == MuroNiebla.EstadoMuro.Abierto, "cazadora: durante el dialogo el muro aun no se forma");
            yield return Captura("r18_caz_1_dialogo");
            // "Retirarme": ella no le deja ir.
            if (dlg != null) SetCampo(dlg, "eleccion", 0);
            yield return Esperar(() => z.Cerrada, 6f);
            Comprobar(z.Cerrada && muro.Bloquea, "cazadora: tras elegir \"Retirarme\" el muro se cierra igualmente");
            Comprobar(p.transform.position.x > xm + muro.anchoBloqueo * 0.5f, "cazadora: el player queda dentro de la arena");
            Teletransportar(new Vector3(xm + 2.5f, suelo + 0.8f, 0f));
            yield return new WaitForSeconds(1.4f);
            Comprobar(muro.Estado == MuroNiebla.EstadoMuro.Cerrado, "cazadora: el muro queda formado");
            yield return Captura("r18_caz_2_cerrado");
            yield return CapturaMuro("r18_caz_m2_cerrado", muro);

            // Muerte y reintento (entrada corta, sin dialogo): el muro vuelve a formarse.
            p.InvulnerableExterno = false;
            SetCampo(p, "isInvincible", false);
            p.TakeDamage(99999);
            yield return EsperarReaparecer();
            p.InvulnerableExterno = true;
            Comprobar(!muro.Bloquea && z.Armado, "cazadora: al reaparecer el muro se abre y el activador se arma");
            yield return EsperarMuro(muro, MuroNiebla.EstadoMuro.Abierto, 4f);
            Teletransportar(new Vector3(xm + z.distanciaActivador + 1f, suelo + 0.8f, 0f));
            yield return Esperar(() => z.Cerrada, 6f);
            DialogoCazadora d2 = Object.FindFirstObjectByType<DialogoCazadora>();
            Comprobar(z.Cerrada && muro.Bloquea && z.Disparos == 2 && (d2 == null || !d2.isActiveAndEnabled || dlg == d2),
                      "cazadora: al reintentar (entrada corta) el muro se forma de nuevo");

            // Victoria: el muro se disuelve.
            yield return new WaitForSeconds(1.5f);
            Llamar(arena, "Victoria");
            yield return Esperar(() => muro.Estado == MuroNiebla.EstadoMuro.Disolviendo, 6f);
            Comprobar(muro.Estado == MuroNiebla.EstadoMuro.Disolviendo && !muro.Bloquea, "cazadora: al vencer el muro se disuelve");
            yield return EsperarMuro(muro, MuroNiebla.EstadoMuro.Abierto, 5f);
            Comprobar(muro.Estado == MuroNiebla.EstadoMuro.Abierto, "cazadora: la zona queda abierta");
            if (JefeCazadora.Actual != null) Destroy(JefeCazadora.Actual.gameObject);
            yield return new WaitForSeconds(2f);
            Desafio.Salir();
            yield return EsperarEscena("Menu Principal");
            yield return new WaitForSeconds(1f);
        }

        // ------------------------------------------------------------------ Ayudas

        private static SpriteRenderer Dibujo(MuroNiebla m) => m.transform.Find("Dibujo").GetComponent<SpriteRenderer>();

        private static IEnumerator Esperar(System.Func<bool> cond, float segundos)
        {
            float t0 = Time.realtimeSinceStartup;
            while (!cond() && Time.realtimeSinceStartup - t0 < segundos) yield return null;
        }

        private static IEnumerator EsperarMuro(MuroNiebla m, MuroNiebla.EstadoMuro estado, float segundos) => Esperar(() => m.Estado == estado, segundos);

        private IEnumerator EsperarEscena(string nombre)
        {
            float t0 = Time.realtimeSinceStartup;
            while ((SceneManager.GetActiveScene().name != nombre || PantallaCarga.Cargando) && Time.realtimeSinceStartup - t0 < 40f) yield return null;
        }

        private IEnumerator EsperarReaparecer()
        {
            float t0 = Time.realtimeSinceStartup;
            yield return new WaitForSeconds(1f);
            while (Object.FindFirstObjectByType<PlayerControler>() == null && Time.realtimeSinceStartup - t0 < 12f) yield return null;
            yield return new WaitForSeconds(1.5f);
            p = Object.FindFirstObjectByType<PlayerControler>();
        }

        private void Teletransportar(Vector3 pos)
        {
            if (p == null) p = Object.FindFirstObjectByType<PlayerControler>();
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

        private static void Llamar(object o, string metodo, params object[] args) =>
            o.GetType().GetMethods(Todo).First(x => x.Name == metodo && x.GetParameters().Length == args.Length).Invoke(o, args);

        // Primer plano del muro: una camara aparte centrada en el.
        private IEnumerator CapturaMuro(string nombre, MuroNiebla m)
        {
            Camera principal = Camera.main;
            if (principal == null) yield break;
            Camera cam = Instantiate(principal.gameObject).GetComponent<Camera>();
            foreach (Behaviour b in cam.GetComponents<Behaviour>()) if (!(b is Camera) && b.GetType().Name != "UniversalAdditionalCameraData") b.enabled = false;
            cam.tag = "Untagged";
            cam.orthographicSize = 4.2f;
            cam.transform.position = new Vector3(m.transform.position.x + 1.5f, m.transform.position.y + 3.6f, -10f);
            const int ancho = 1280, alto = 720;
            RenderTexture rt = new RenderTexture(ancho, alto, 24);
            cam.targetTexture = rt;
            yield return null;
            cam.Render();
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(ancho, alto, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, ancho, alto), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(carpeta, nombre + ".png"), tex.EncodeToPNG());
            cam.targetTexture = null;
            rt.Release();
            Destroy(cam.gameObject);
        }

        private IEnumerator Captura(string nombre)
        {
            Camera cam = Camera.main;
            if (cam == null) yield break;
            const int ancho = 1280, alto = 720;
            RenderTexture rt = new RenderTexture(ancho, alto, 24);
            cam.targetTexture = rt;
            var cambiados = new List<Canvas>();
            foreach (Canvas c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay && c.gameObject.activeInHierarchy)
                { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = cam; c.planeDistance = 1f; c.sortingLayerName = "VFX"; c.sortingOrder += 500; cambiados.Add(c); }
            for (int i = 0; i < 3; i++) { Canvas.ForceUpdateCanvases(); yield return null; }
            cam.Render();
            cam.targetTexture = null;
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(ancho, alto, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, ancho, alto), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(carpeta, nombre + ".png"), tex.EncodeToPNG());
            rt.Release();
            foreach (Canvas c in cambiados) { c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder -= 500; }
            for (int i = 0; i < 2; i++) yield return null;
        }
    }
}
