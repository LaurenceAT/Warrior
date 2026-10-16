using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

// Prueba de "Controles y guia" (Ronda 24), linea de comandos (sin -quit):
//   ... -executeMethod PruebaControles.Todo
// Menu principal, pausa (partida normal y desafio), guia del primer desafio,
// marca guardada, teclas reales, textos y 6 resoluciones. Escribe "[R24] OK/FALLO"
// y capturas en PruebaNieve/r24_*.png. Nunca toca el progreso real.
[InitializeOnLoad]
public static class PruebaControles
{
    private const string Clave = "PruebaControles.Modo";

    static PruebaControles()
    {
        EditorApplication.playModeStateChanged += e =>
        {
            if (SessionState.GetString(Clave, "") == "") return;
            if (e == PlayModeStateChange.EnteredPlayMode) new GameObject("PruebaControles").AddComponent<Conductor>();
            else if (e == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetString(Clave, "");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
        };
    }

    public static void Todo()
    {
        SessionState.SetString(Clave, "todo");
        EditorSceneManager.OpenScene("Assets/Scenes/Menu Principal.unity");
        ArranquePruebas.Proteger();
        SessionState.SetBool(GuiaDesafio.ClaveSinGuia, false);
        EditorApplication.EnterPlaymode();
    }

    private class Conductor : MonoBehaviour
    {
        private const BindingFlags Todo = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static;
        private static readonly Vector2Int[] Resoluciones =
            { new Vector2Int(1920, 1080), new Vector2Int(1920, 1200), new Vector2Int(2560, 1080), new Vector2Int(1280, 720), new Vector2Int(1024, 768), new Vector2Int(800, 600) };
        private static readonly string[] Prohibidas = { "debes", "tienes que", "recuerda", "no olvides", "asegúrate", "es importante", "deberás", "hay que" };

        private string carpeta;
        private int ok, fallos, errores;
        private Keyboard kb;
        private Mouse raton;

        private void Ok(string s) { ok++; Debug.Log("[R24] OK " + s); }
        private void Fallo(string s) { fallos++; Debug.LogWarning("[R24] FALLO " + s); }
        private void Comprobar(bool c, string s) { if (c) Ok(s); else Fallo(s); }

        private IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject);
            Application.logMessageReceived += (c, st, t) =>
            {
                if ((t == LogType.Exception || t == LogType.Error) && !c.StartsWith("[R24]")) { errores++; Debug.Log("[R24] error visto: " + c + "\n" + st); }
            };
            carpeta = Path.Combine(Directory.GetCurrentDirectory(), "PruebaNieve");
            Partida.CarpetaPruebas = Path.Combine(carpeta, "PartidasR24");
            if (Directory.Exists(Partida.CarpetaPruebas)) Directory.Delete(Partida.CarpetaPruebas, true);
            Directory.CreateDirectory(Partida.CarpetaPruebas);
            Globales.Recargar();

            // Teclado y raton virtuales (las pulsaciones de la prueba).
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            kb = InputSystem.AddDevice<Keyboard>("TecladoPrueba");
            raton = InputSystem.AddDevice<Mouse>("RatonPrueba");
            kb.MakeCurrent();
            raton.MakeCurrent();

            IEnumerator r = Pasos();
            float limite = Time.realtimeSinceStartup + 900f;
            while (true)
            {
                bool sigue;
                try { sigue = r.MoveNext(); }
                catch (System.Exception e) { Fallo("excepcion: " + e); sigue = false; }
                if (!sigue || Time.realtimeSinceStartup > limite) break;
                yield return r.Current;
            }
            Comprobar(errores == 0, $"sin errores en la consola ({errores})");
            Debug.Log($"[R24] RESULTADO: {ok} OK, {fallos} FALLOS");
            Time.timeScale = 1f;
            Partida.Descargar();
            Globales.GuardarPendiente();
            EditorApplication.ExitPlaymode();
        }

        private IEnumerator Pasos()
        {
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Ficha();
            yield return MenuPrincipalPaso();
            yield return PausaNormal();
            yield return GuiaPrimerDesafio();
        }

        // ------------------------------------------------------------------ Ficha y textos

        private IEnumerator Ficha()
        {
            GuiaControles g = GuiaControles.Get();
            Comprobar(g != null, "existe la ficha Resources/GuiaControles");
            if (g == null) yield break;
            var todas = g.Todas().ToList();
            Comprobar(todas.Count == 37, $"37 tarjetas ({todas.Count})");
            Comprobar(g.Esenciales().Count == 11, $"11 esenciales ({g.Esenciales().Count})");
            Comprobar(todas.All(t => !string.IsNullOrWhiteSpace(t.titulo) && !string.IsNullOrWhiteSpace(t.texto)), "ninguna tarjeta sin titulo ni texto");
            Comprobar(todas.All(t => TarjetaControl.Cuadros(t) >= 1 && (t.tipo != GuiaControles.TipoImagen.Cuadros || t.cuadros.All(c => c.sprite != null))),
                      "ninguna tarjeta sin imagen");
            Comprobar(todas.All(t => TarjetaControl.Cuadros(t) <= 3), "las imagenes tienen como mucho 3 cuadros");
            string fijas = string.Join(", ", todas.Where(t => TarjetaControl.Cuadros(t) == 1).Select(t => t.titulo));
            Debug.Log("[R24] imagenes fijas: " + fijas);
            var prohibidas = todas.Where(t => Prohibidas.Any(p => g.Resolver(t.texto, true).ToLowerInvariant().Contains(p))).Select(t => t.titulo).ToList();
            Comprobar(prohibidas.Count == 0, "sin muletillas de manual " + string.Join(", ", prohibidas));
            Comprobar(todas.All(t => !g.Resolver(t.texto, true).Contains("{")), "todas las acciones del texto tienen su tecla");
            Comprobar(todas.All(t => !Regex(g.Resolver(t.texto, true))), "sin numeros en los textos");

            // Teclas reales (Controls.inputactions) con su nombre en espanol.
            var esperadas = new Dictionary<string, string>
            {
                { "Jump", "Espacio" }, { "Attack", "clic izquierdo" }, { "Sprint", "Mayús" }, { "Kick", "clic derecho" },
                { "Dodge", "C" }, { "ToggleWeapon", "E" }, { "Move/left", "A" }, { "Move/right", "D" }, { "Move/up", "W" }, { "Move/down", "S" },
            };
            foreach (var e in esperadas)
                Comprobar(g.NombreAccion(e.Key) == e.Value, $"tecla de {e.Key}: \"{g.NombreAccion(e.Key)}\" (esperada \"{e.Value}\")");
            GuiaControles.Tarjeta atacar = todas.First(t => t.id == "atacar");
            Comprobar(g.Resolver(atacar.texto, true).StartsWith("Para realizar los ataques, presiona el clic izquierdo. Esto también funciona si estás en el aire."),
                      "texto base de Atacar intacto");
            GuiaControles.Tarjeta imb = todas.First(t => t.id == "imbuiciones");
            Comprobar(g.Resolver(imb.texto, true) == "Para elegir una imbuición, pulsa la tecla E. Cada imbuición gasta maná y algunas gastan otra cosa.",
                      "texto base de Imbuiciones intacto");
            yield break;
        }

        private static bool Regex(string s) => System.Text.RegularExpressions.Regex.IsMatch(s.Replace("[1] a [5]", "").Replace("1 a 5", ""), @"\d");

        // ------------------------------------------------------------------ Menu principal

        private IEnumerator MenuPrincipalPaso()
        {
            MenuPrincipal m = FindFirstObjectByType<MenuPrincipal>();
            Comprobar(m != null, "menu principal cargado");
            if (m == null) yield break;
            GameObject principal = (GameObject)Campo(m, "panelPrincipal");
            GameObject panel = (GameObject)Campo(m, "panelControles");
            PantallaControles pc = (PantallaControles)Campo(m, "pantallaControles");
            Comprobar(principal.GetComponentsInChildren<TextMeshProUGUI>(true).Any(t => t.text == "Controles y guía"), "el menu principal tiene \"Controles y guía\"");

            Llamar(m, "Mostrar", panel);
            yield return new WaitForSecondsRealtime(0.4f);
            Comprobar(panel.activeInHierarchy && pc.Elegida != null && pc.Elegida.id == "caminar", "se abre con la primera accion elegida (Caminar)");
            Comprobar(pc.Abierta(0) && Enumerable.Range(1, pc.Categorias - 1).All(c => !pc.Abierta(c)), "la primera categoria abierta y las demas cerradas");
            Comprobar(pc.TarjetasEnLista().Count() == 37, "las 37 tarjetas estan en la lista");
            int visibles = pc.FilasVisibles;
            pc.Alternar(1);
            Comprobar(pc.Abierta(1) && pc.FilasVisibles > visibles, "una categoria se abre");
            pc.Alternar(1);
            Comprobar(!pc.Abierta(1) && pc.FilasVisibles == visibles, "y se cierra");
            GuiaControles.Tarjeta drenado = GuiaControles.Get().Todas().First(t => t.id == "drenado");
            pc.Elegir(drenado);
            yield return null;
            Comprobar(pc.Elegida == drenado && pc.Tarjeta.CampoTitulo.text == "Drenado" && pc.Abierta(5), "elegir una accion muestra su tarjeta y abre su categoria");

            // Las 6 resoluciones, con la tarjeta mas larga a la vista.
            pc.Elegir(GuiaControles.Get().Todas().First(t => t.id == "tus_estados"));
            foreach (Vector2Int res in Resoluciones)
                yield return Captura($"r24_m_{res.x}x{res.y}", res, () => Medir("menu", pc.Panel, pc.Tarjeta, res));

            // Cada tarjeta cabe en la pantalla grande.
            var largas = new List<string>();
            foreach (GuiaControles.Tarjeta t in GuiaControles.Get().Todas())
            {
                pc.Elegir(t, false);
                Canvas.ForceUpdateCanvases();
                if (!Cabe(pc.Tarjeta)) largas.Add(t.titulo);
            }
            Comprobar(largas.Count == 0, "todas las tarjetas caben en la pantalla grande " + string.Join(", ", largas));
            Comprobar(!Globales.Marca(GuiaDesafio.Marca), "ver Controles no pone la marca de la guia");

            // Tab vuelve atras.
            yield return Pulsar(Key.Tab);
            yield return null;
            Comprobar(principal.activeInHierarchy && !panel.activeInHierarchy, "Tab vuelve al menu principal");
            Llamar(m, "Mostrar", panel);
            yield return null;
            yield return PulsarRaton();
            yield return null;
            Comprobar(principal.activeInHierarchy && !panel.activeInHierarchy, "clic derecho vuelve al menu principal");

            // Abrir y cerrar muchas veces no deja objetos sobrantes.
            // (Sin contar las brasas del fuego del menu, que nacen solas.)
            int Objetos() => FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Count(t => t.name != "Brasa");
            int antes = Objetos();
            for (int i = 0; i < 20; i++) { Llamar(m, "Mostrar", panel); yield return null; Llamar(m, "Mostrar", principal); yield return null; }
            int despues = Objetos();
            Comprobar(despues == antes, $"abrir y cerrar 20 veces no deja objetos ({antes} -> {despues})");
        }

        // ------------------------------------------------------------------ Pausa en partida normal

        private IEnumerator PausaNormal()
        {
            Partida.Nueva("Nivel Nieve");
            Equipo.Reiniciar();
            SceneManager.LoadScene("Nivel Nieve");
            yield return EsperarEscena("Nivel Nieve");
            yield return new WaitForSeconds(2f);
            Comprobar(!GuiaDesafio.Abierta, "en la partida normal no sale la guia");
            GameManager.Instance.PauseGame();
            yield return new WaitForSecondsRealtime(0.3f);
            MenuPausa mp = MenuPausa.Get();
            GameObject principal = (GameObject)Campo(mp, "principal");
            Comprobar(principal.GetComponentsInChildren<TextMeshProUGUI>(true).Any(t => t.text == "Controles y guía"), "la pausa tiene \"Controles y guía\"");
            Comprobar(BajoReanudar(principal), "\"Controles y guía\" va justo debajo de \"Reanudar\"");
            Comprobar(CabePausa(principal), "las opciones de la pausa caben en su panel");

            foreach (Key k in new[] { Key.Tab, Key.Escape })
            {
                Llamar(mp, "AbrirControles");
                yield return new WaitForSecondsRealtime(0.2f);
                Comprobar((bool)Campo(mp, "controlesAbierto") && Time.timeScale == 0f, $"Controles se abre desde la pausa ({k})");
                yield return Pulsar(k);
                yield return null;
                Comprobar(!(bool)Campo(mp, "controlesAbierto") && principal.activeInHierarchy && GameManager.Instance.IsPaused, $"{k} vuelve a la pausa sin cerrarla");
            }
            Llamar(mp, "AbrirControles");
            yield return null;
            yield return PulsarRaton();
            yield return null;
            Comprobar(!(bool)Campo(mp, "controlesAbierto") && GameManager.Instance.IsPaused, "clic derecho vuelve a la pausa sin cerrarla");

            // Lo pulsado con Controles abierto no hace nada al volver al juego.
            PlayerControler p = FindFirstObjectByType<PlayerControler>();
            ReservaPociones rp = ReservaPociones.Get();
            int cargas = rp.Cargas;
            Llamar(mp, "AbrirControles");
            yield return null;
            yield return Pulsar(Key.Q);
            yield return Pulsar(Key.F);
            yield return Pulsar(Key.Tab);
            yield return null;
            GameManager.Instance.ResumeGame();
            yield return new WaitForSeconds(1f);
            Comprobar(Time.timeScale == 1f && !GameManager.Instance.IsPaused, "el tiempo vuelve a la normalidad");
            Comprobar(rp.Cargas == cargas && !MenuHoguera.Abierto, $"las teclas pulsadas en Controles no cuentan al volver (cargas {cargas} -> {rp.Cargas})");
            Partida.Descargar();
        }

        // ------------------------------------------------------------------ Guia del primer desafio

        private IEnumerator GuiaPrimerDesafio()
        {
            FichaJefe sombra = FichaJefe.Todas().First(f => f.id == "shadowed_wetlands");
            FichaJefe wraith = FichaJefe.Todas().First(f => f.id == "crimson_wraith");
            FichaJefe caz = FichaJefe.Todas().First(f => f.id == "blind_huntress");
            Comprobar(!Globales.Marca(GuiaDesafio.Marca), "perfil nuevo: la marca empieza vacia");

            Desafio.Empezar(sombra, false);
            yield return EsperarGuia();
            Comprobar(GuiaDesafio.Abierta, "la guia sale al entrar al primer desafio (Sombra)");
            if (!GuiaDesafio.Abierta) yield break;
            Comprobar(Time.timeScale == 0f && GuiaDesafio.Total == 11 && GuiaDesafio.Paso == 0, "juego en pausa, 11 tarjetas, empieza en la 1");
            TextMeshProUGUI contador = FindFirstObjectByType<GuiaDesafio>().GetComponentsInChildren<TextMeshProUGUI>().First(t => t.text.Contains(" de "));
            Comprobar(contador.text == "1 de 11", $"muestra \"1 de 11\" ({contador.text})");

            // El tiempo del desafio no corre y el combate no empieza.
            float t0 = Desafio.Segundos;
            ZonaJefe z = FindFirstObjectByType<ZonaJefe>();
            PlayerControler p = FindFirstObjectByType<PlayerControler>();
            Vector3 antes = p.transform.position;
            p.transform.position = new Vector3(z.transform.position.x + z.distanciaActivador + 0.5f, z.transform.position.y + 1f, 0f);
            yield return new WaitForSecondsRealtime(1.5f);
            Comprobar(Mathf.Approximately(Desafio.Segundos, t0), $"el tiempo del desafio no avanza ({t0:0.00} -> {Desafio.Segundos:0.00})");
            Comprobar(z.Muro.Estado == MuroNiebla.EstadoMuro.Abierto, "el combate no empieza aunque el player este en el activador");
            p.transform.position = antes;

            // La pausa no se abre encima (Esc salta la guia y no abre la pausa).
            foreach (Vector2Int res in Resoluciones)
                yield return Captura($"r24_g_{res.x}x{res.y}", res, () => Medir("guia", GuiaDesafio.PanelActual, GuiaDesafio.TarjetaActual, res));
            var largas = new List<string>();
            GuiaDesafio gd = FindFirstObjectByType<GuiaDesafio>();
            for (int i = 0; i < 11; i++)
            {
                Canvas.ForceUpdateCanvases();
                if (!Cabe(GuiaDesafio.TarjetaActual)) largas.Add(GuiaDesafio.TarjetaActual.Actual.titulo);
                if (i < 10) gd.Siguiente();
            }
            Comprobar(largas.Count == 0, "todas las tarjetas esenciales caben en la tarjeta pequena " + string.Join(", ", largas));
            Comprobar(GuiaDesafio.Paso == 10 && gd.GetComponentsInChildren<TextMeshProUGUI>().Any(t => t.text == "Empezar"), "al final dice \"Empezar\"");
            gd.Anterior();
            Comprobar(GuiaDesafio.Paso == 9, "\"Anterior\" vuelve una tarjeta");
            gd.Siguiente();
            gd.Siguiente();   // Empezar
            yield return null;
            Comprobar(!GuiaDesafio.Abierta && Globales.Marca(GuiaDesafio.Marca) && Time.timeScale == 1f, "\"Empezar\" la cierra, pone la marca y quita la pausa");
            yield return new WaitForSeconds(1f);
            Comprobar(Desafio.Segundos > t0, "despues, el tiempo del desafio corre");

            // Reiniciar desafio 10 veces: no vuelve.
            bool salio = false;
            for (int i = 0; i < 10; i++)
            {
                Desafio.Reiniciar();
                yield return new WaitForSecondsRealtime(0.5f);
                yield return EsperarCarga();
                yield return new WaitForSecondsRealtime(0.6f);
                salio |= GuiaDesafio.Abierta;
            }
            Comprobar(!salio, "no vuelve a salir tras \"Reiniciar desafío\" (10 veces)");

            // Otro desafio: no sale.
            Desafio.Salir();
            yield return EsperarEscena("Menu Principal");
            yield return new WaitForSecondsRealtime(1f);
            Desafio.Empezar(wraith, false);
            yield return EsperarGuia();
            Comprobar(!GuiaDesafio.Abierta, "no sale en otro desafio (Wraith)");
            Desafio.Salir();
            yield return EsperarEscena("Menu Principal");
            yield return new WaitForSecondsRealtime(1f);

            // Herramientas del Editor (reiniciar Desafios) borran la marca.
            Globales.Borrar(CategoriaGuardado.Desafios);
            Comprobar(!Globales.Marca(GuiaDesafio.Marca), "reiniciar el progreso de Desafios borra la marca");

            // Codigo "warrior" y la Cazadora como primer desafio: sale igual; Esc salta sin abrir la pausa.
            Globales.PonerMarca("codigo_blind_huntress", true);
            Comprobar(caz.Desbloqueado, "el codigo \"warrior\" desbloquea a la Cazadora");
            Desafio.Empezar(caz, false);
            yield return EsperarGuia();
            Comprobar(GuiaDesafio.Abierta, "sale con la Cazadora como primer desafio");
            yield return Pulsar(Key.Escape);
            yield return null;
            yield return null;
            Comprobar(!GuiaDesafio.Abierta && Globales.Marca(GuiaDesafio.Marca), "Esc salta la guia y pone la marca");
            Comprobar(!GameManager.Instance.IsPaused, "el Esc que salta la guia no abre la pausa");

            // En la pausa del desafio tambien esta "Controles y guía" y no cambia la marca.
            GameManager.Instance.PauseGame();
            yield return new WaitForSecondsRealtime(0.3f);
            MenuPausa mp = MenuPausa.Get();
            Llamar(mp, "AbrirControles");
            yield return new WaitForSecondsRealtime(0.3f);
            Comprobar((bool)Campo(mp, "controlesAbierto"), "Controles se abre desde la pausa del desafio");
            Globales.PonerMarca(GuiaDesafio.Marca, false);
            yield return Pulsar(Key.Tab);
            yield return null;
            Comprobar(!Globales.Marca(GuiaDesafio.Marca) && GameManager.Instance.IsPaused, "ver Controles en la pausa no cambia la marca y Tab vuelve a la pausa");
            GameManager.Instance.ResumeGame();
            Desafio.Salir();
            yield return EsperarEscena("Menu Principal");
        }

        // ------------------------------------------------------------------ Medidas

        // La tarjeta: el panel dentro de la pantalla y los textos sin salirse.
        private void Medir(string que, RectTransform panel, TarjetaControl t, Vector2Int res)
        {
            Camera cam = Camera.main;
            Rect r = RectPantalla(cam, panel);
            Comprobar(r.xMin >= -1 && r.yMin >= -1 && r.xMax <= res.x + 1 && r.yMax <= res.y + 1, $"{que} {res.x}x{res.y}: cabe en la pantalla");
            Comprobar(Cabe(t), $"{que} {res.x}x{res.y}: textos sin cortar");
        }

        private static bool Cabe(TarjetaControl t)
        {
            if (t == null) return false;
            t.CampoTexto.ForceMeshUpdate();
            t.CampoTitulo.ForceMeshUpdate();
            bool texto = t.CampoTexto.preferredHeight <= t.CampoTexto.rectTransform.rect.height + 1f;
            bool titulo = t.CampoTitulo.preferredWidth <= t.CampoTitulo.rectTransform.rect.width + 1f;
            return texto && titulo;
        }

        private static bool BajoReanudar(GameObject principal)
        {
            var textos = principal.GetComponentsInChildren<TextMeshProUGUI>(true).Select(t => t.text).ToList();
            int a = textos.IndexOf("Reanudar"), b = textos.IndexOf("Controles y guía");
            return a >= 0 && b == a + 1;
        }

        private static bool CabePausa(GameObject principal)
        {
            Canvas.ForceUpdateCanvases();
            RectTransform panel = (RectTransform)principal.transform;
            Vector3[] p = new Vector3[4]; panel.GetWorldCorners(p);
            foreach (OpcionEstilo o in principal.GetComponentsInChildren<OpcionEstilo>())
            {
                Vector3[] c = new Vector3[4]; ((RectTransform)o.transform).GetWorldCorners(c);
                if (c[0].y < p[0].y - 0.5f) return false;
            }
            return true;
        }

        private IEnumerator Captura(string nombre, Vector2Int res, System.Action medir)
        {
            Camera cam = Camera.main;
            if (cam == null) { Fallo("no hay camara para " + nombre); yield break; }
            RenderTexture rt = new RenderTexture(res.x, res.y, 24);
            cam.targetTexture = rt;
            var cambiados = new List<Canvas>();
            foreach (Canvas c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay && c.gameObject.activeInHierarchy)
                { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = cam; c.planeDistance = 1f; c.sortingLayerName = "VFX"; c.sortingOrder += 500; cambiados.Add(c); }
            for (int i = 0; i < 3; i++) { Canvas.ForceUpdateCanvases(); yield return null; }
            medir?.Invoke();
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
            for (int i = 0; i < 2; i++) yield return null;
        }

        private static Rect RectPantalla(Camera cam, RectTransform r)
        {
            Vector3[] c = new Vector3[4];
            r.GetWorldCorners(c);
            Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, c[0]), b = RectTransformUtility.WorldToScreenPoint(cam, c[2]);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        // ------------------------------------------------------------------ Ayudas

        private IEnumerator Pulsar(Key k)
        {
            InputSystem.QueueStateEvent(kb, new KeyboardState(k));
            yield return null;
            InputSystem.QueueStateEvent(kb, new KeyboardState());
            yield return null;
        }

        private IEnumerator PulsarRaton()
        {
            InputSystem.QueueStateEvent(raton, new MouseState().WithButton(MouseButton.Right));
            yield return null;
            InputSystem.QueueStateEvent(raton, new MouseState());
            yield return null;
        }

        private IEnumerator EsperarEscena(string escena)
        {
            float t = Time.realtimeSinceStartup;
            while ((SceneManager.GetActiveScene().name != escena || PantallaCarga.Cargando) && Time.realtimeSinceStartup - t < 60f) yield return null;
            yield return null;
        }

        private IEnumerator EsperarCarga()
        {
            float t = Time.realtimeSinceStartup;
            while (PantallaCarga.Cargando && Time.realtimeSinceStartup - t < 60f) yield return null;
        }

        private IEnumerator EsperarGuia()
        {
            float t = Time.realtimeSinceStartup;
            yield return new WaitForSecondsRealtime(0.5f);
            while (PantallaCarga.Cargando && Time.realtimeSinceStartup - t < 60f) yield return null;
            float t2 = Time.realtimeSinceStartup;
            while (!GuiaDesafio.Abierta && Time.realtimeSinceStartup - t2 < 2f) yield return null;
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

        private static void Llamar(object o, string metodo, params object[] args) =>
            o.GetType().GetMethods(Todo).First(x => x.Name == metodo && x.GetParameters().Length == args.Length).Invoke(o, args);
    }
}
