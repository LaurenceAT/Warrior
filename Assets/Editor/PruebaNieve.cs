using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Pruebas automaticas del nivel nevado en modo juego (linea de comandos, sin -quit):
//   Unity -batchmode -projectPath ... -executeMethod PruebaNieve.Sistemas
//   (tambien Nivel, Enemigos, Jefe y Cueva)
// Apuntan en el log lo que pasa y guardan capturas en "PruebaNieve/".
[InitializeOnLoad]
public static class PruebaNieve
{
    private const string Clave = "PruebaNieve.Modo";
    private const string Escena = "Assets/Scenes/Nivel Nieve.unity";
    private static readonly BindingFlags Priv = BindingFlags.NonPublic | BindingFlags.Instance;

    static PruebaNieve()
    {
        EditorApplication.playModeStateChanged += Cambio;
    }

    public static void Sistemas() => Lanzar("sistemas", Escena);
    public static void Nivel() => Lanzar("nivel", Escena);
    public static void Enemigos() => Lanzar("enemigos", Escena);
    public static void Jefe() => Lanzar("jefe", Escena);
    public static void Cueva() => Lanzar("cueva", "Assets/Scenes/Nivel Cueva.unity");

    private static void Lanzar(string modo, string escena)
    {
        SessionState.SetString(Clave, modo);
        EditorSceneManager.OpenScene(escena);
        EditorApplication.EnterPlaymode();
    }

    private static void Cambio(PlayModeStateChange estado)
    {
        string modo = SessionState.GetString(Clave, "");
        if (modo == "") return;
        if (estado == PlayModeStateChange.EnteredPlayMode)
        {
            Conductor c = new GameObject("PruebaNieve").AddComponent<Conductor>();
            c.modo = modo;
        }
        else if (estado == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetString(Clave, "");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
    }

    private class Conductor : MonoBehaviour
    {
        public string modo;
        private string carpeta;
        private PlayerControler p;

        private IEnumerator Start()
        {
            carpeta = Path.Combine(Directory.GetCurrentDirectory(), "PruebaNieve");
            Directory.CreateDirectory(carpeta);
            Progreso.Reiniciar();
            PlayerPrefs.DeleteKey("cofre_Nivel Nieve_cofre");
            yield return new WaitForSeconds(2f);
            p = Object.FindFirstObjectByType<PlayerControler>();
            Debug.Log($"[Prueba] modo={modo} player={(p != null)} vida={Vida()}/{p.VidaMaxima}");
            IEnumerator rutina = modo == "sistemas" ? Sistemas() : modo == "nivel" ? Nivel() : modo == "enemigos" ? Enemigos()
                               : modo == "jefe" ? Jefe() : Cueva();
            float limite = Time.realtimeSinceStartup + 240f;
            while (rutina.MoveNext())
            {
                if (Time.realtimeSinceStartup > limite) { Debug.LogError("[Prueba] tiempo agotado"); break; }
                yield return rutina.Current;
            }
            Debug.Log("[Prueba] fin");
            Time.timeScale = 1f;
            EditorApplication.ExitPlaymode();
        }

        // ------------------------------------------------------------------ Sistemas

        private IEnumerator Sistemas()
        {
            yield return Captura("s01_inicio", true);

            // Rueda de imbuir: se abre con la E.
            Entrada().IsTogglingWeapon = true;
            yield return new WaitForSecondsRealtime(0.4f);
            Debug.Log($"[Sistemas] rueda abierta={RuedaImbuir.Abierta} timeScale={Time.timeScale}");
            yield return Captura("s02_rueda", true);
            RuedaImbuir.Cerrar();
            yield return new WaitForSecondsRealtime(0.5f);

            // Imbuir en fuego (lo que haria la rueda al elegir).
            PlayerMana mana = p.GetComponent<PlayerMana>();
            float m0 = mana.Actual;
            Llamar(p, "EmpezarImbuir", Elemento.Fuego);
            yield return new WaitForSeconds(0.4f);
            yield return Captura("s03_imbuyendo", false);
            yield return new WaitForSeconds(0.8f);
            Debug.Log($"[Sistemas] imbuido={p.ElementoActivo} mana {m0:0}->{mana.Actual:0}");
            yield return Captura("s04_imbuido", true);

            // Contra una rata: estados y almas.
            EnemyHealth rata = Cercano<EnemigoRata>();
            if (rata != null)
            {
                Teletransportar(rata.transform.position + Vector3.left * 1f);
                float t0 = Time.time;
                int almas0 = Progreso.Almas;
                while (rata != null && !rata.Muerto && Time.time - t0 < 6f)
                {
                    Mirar(1);
                    Entrada().IsAttacking = true;
                    Invulnerable();
                    yield return new WaitForSeconds(0.06f);
                    yield return Captura($"s05_tajo_{(Time.time - t0):0.00}", false);
                    yield return new WaitForSeconds(0.14f);
                }
                yield return new WaitForSeconds(2.5f);
                Debug.Log($"[Sistemas] rata muerta={(rata == null || rata.Muerto)} almas {almas0}->{Progreso.Almas} mana={mana.Actual:0}");
            }

            // Cambiar a escarcha enseguida: en recarga.
            Debug.Log($"[Sistemas] impedimento al recambiar: {p.GetComponent<ArmaImbuida>().Impedimento(mana)}");

            // Hoguera: descansar, subir de nivel.
            Hoguera h = Object.FindFirstObjectByType<Hoguera>();
            Teletransportar(h.transform.position + Vector3.right * 0.5f + Vector3.up * 0.6f);
            yield return new WaitForSeconds(0.5f);
            Progreso.SumarAlmas(3000);
            Entrada().IsTogglingWeapon = true;
            yield return new WaitForSecondsRealtime(0.6f);
            Debug.Log($"[Sistemas] menu hoguera={MenuHoguera.Abierto} timeScale={Time.timeScale}");
            MenuHoguera menu = Object.FindFirstObjectByType<MenuHoguera>();
            int vidaAntes = p.VidaMaxima;
            foreach (Progreso.Estadistica e in new[] { Progreso.Estadistica.Vida, Progreso.Estadistica.Vida, Progreso.Estadistica.Mana, Progreso.Estadistica.ResGolpes })
                Llamar(menu, "Subir", e);
            yield return new WaitForSecondsRealtime(0.4f);
            Debug.Log($"[Sistemas] nivel={Progreso.NivelTotal} almas={Progreso.Almas} vida {vidaAntes}->{p.VidaMaxima} mana max={mana.Maximo} resGolpes={Progreso.ResGolpes}");
            yield return Captura("s06_hoguera", true);
            Llamar(menu, "CerrarInterno");
            yield return new WaitForSecondsRealtime(0.5f);

            // Resistencia a golpes: un golpe de 40 ahora quita menos.
            SetCampo(p, "isInvincible", false);
            int v0 = Vida();
            p.TakeDamage(40);
            Debug.Log($"[Sistemas] golpe 40 con resistencia: vida {v0}->{Vida()}");

            // Pocion: cura el 75 %.
            yield return new WaitForSeconds(1.2f);
            p.TakeDamage(60);
            yield return new WaitForSeconds(1.2f);
            int antes = Vida();
            Entrada().IsHealing = true;
            yield return new WaitForSeconds(1.3f);
            Debug.Log($"[Sistemas] pocion: vida {antes}->{Vida()} (max {p.VidaMaxima})");

            // Muerte: mancha de almas y la recoge.
            int almasVivo = Progreso.Almas;
            p.TakeDamage(9999);
            yield return new WaitForSecondsRealtime(4.5f);
            p = Object.FindFirstObjectByType<PlayerControler>();
            Debug.Log($"[Sistemas] muerto: almas {almasVivo}->{Progreso.Almas} mancha={Progreso.AlmasPerdidas} player={(p != null)}");
            ManchaAlmas mancha = Object.FindFirstObjectByType<ManchaAlmas>();
            if (mancha != null && p != null)
            {
                Teletransportar(mancha.transform.position);
                yield return new WaitForSeconds(0.5f);
            }
            Debug.Log($"[Sistemas] tras recoger mancha: almas={Progreso.Almas} mancha={Progreso.AlmasPerdidas}");
        }

        // ------------------------------------------------------------------ Nivel

        private IEnumerator Nivel()
        {
            foreach (EnemyHealth e in Object.FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None)) Destroy(e.gameObject);
            yield return null;
            // Hielo: se suelta el movimiento y sigue deslizando.
            Teletransportar(new Vector3(62f, 3f, 0f));
            yield return new WaitForSeconds(0.6f);
            PonerEje(1f);
            yield return new WaitForSeconds(0.5f);
            float vx0 = p.GetComponent<Rigidbody2D>().linearVelocityX;
            PonerEje(0f);
            float x0 = p.transform.position.x;
            Debug.Log($"[Nivel] hielo: vx al soltar={vx0:0.00} suelo={GetCampo(p, "isGrounded")}");
            yield return new WaitForSeconds(0.4f);
            Debug.Log($"[Nivel] hielo: sobreHielo={GetCampo(p, "sobreHielo")} desliza {p.transform.position.x - x0:0.00} u tras soltar");
            yield return Captura("n01_hielo", true);

            // Ventisca.
            Teletransportar(new Vector3(158f, 4f, 0f));
            yield return new WaitForSeconds(2.5f);
            float xv = p.transform.position.x;
            yield return new WaitForSeconds(1f);
            Debug.Log($"[Nivel] ventisca: intensidad={OverlayVentisca.IntensidadMaxima:0.00} empuje {p.transform.position.x - xv:0.00} u/s");
            yield return Captura("n02_ventisca", true);

            // Lago: con fuego no pasa nada; con escarcha se congela.
            Teletransportar(new Vector3(199.2f, 4f, 0f));
            yield return new WaitForSeconds(0.6f);
            Imbuir(Elemento.Fuego);
            yield return Atacar(1, 3);
            AguaCongelable lago = Object.FindFirstObjectByType<AguaCongelable>();
            Collider2D puente = GameObject.Find("PuenteHielo").GetComponent<Collider2D>();
            Debug.Log($"[Nivel] lago con fuego: puente={puente.enabled}");
            Imbuir(Elemento.Hielo);
            yield return Atacar(1, 3);
            yield return new WaitForSeconds(1.2f);
            Debug.Log($"[Nivel] lago con escarcha: puente={puente.enabled}");
            yield return Captura("n03_lago", true);
            PonerEje(1f);
            yield return new WaitForSeconds(2.5f);
            PonerEje(0f);
            Debug.Log($"[Nivel] cruzando el lago: x={p.transform.position.x:0.0} vivo={(p != null)}");

            // Muro de hielo: con escarcha no; con fuego se rompe.
            Teletransportar(new Vector3(244.8f, 4f, 0f));
            yield return new WaitForSeconds(0.5f);
            GameObject muro = GameObject.Find("MuroHielo");
            yield return Atacar(1, 2);
            Debug.Log($"[Nivel] muro con escarcha: activo={muro.activeSelf}");
            Imbuir(Elemento.Fuego);
            yield return Atacar(1, 5);
            yield return new WaitForSeconds(0.8f);
            Debug.Log($"[Nivel] muro con fuego: activo={muro.activeSelf}");
            yield return Captura("n04_muro", true);

            // Sello: con sagrado se abre; el cofre da almas.
            Teletransportar(new Vector3(299f, 4f, 0f));
            yield return new WaitForSeconds(0.5f);
            Imbuir(Elemento.Sagrado);
            yield return Atacar(1, 4);
            Collider2D sello = GameObject.Find("SelloSombrio").GetComponent<Collider2D>();
            Debug.Log($"[Nivel] sello con sagrado: solido={sello.enabled}");
            int a0 = Progreso.Almas;
            Teletransportar(new Vector3(326f, 3.7f, 0f));
            yield return new WaitForSeconds(3f);
            Debug.Log($"[Nivel] cofre: almas {a0}->{Progreso.Almas} pociones max={ReservaPociones.Get().Maximo}");
            yield return Captura("n05_cofre", true);

            // Pinchos y caida.
            Teletransportar(new Vector3(73.5f, 1.5f, 0f));
            SetCampo(p, "isInvincible", false);
            int v0 = Vida();
            yield return new WaitForSeconds(0.5f);
            Debug.Log($"[Nivel] pinchos: vida {v0}->{Vida()}");
            yield return Captura("n06_pinchos", true);
        }

        // ------------------------------------------------------------------ Enemigos

        private IEnumerator Enemigos()
        {
            yield return new WaitForSeconds(4f);
            Debug.Log("[Audio] explorando: " + Audio());
            System.Type[] tipos = { typeof(EnemigoRata), typeof(EnemigoMurcielago), typeof(EnemigoOjo), typeof(EnemigoArquero), typeof(EnemigoHechicero) };
            foreach (System.Type t in tipos)
            {
                MonoBehaviour e = (MonoBehaviour)Object.FindFirstObjectByType(t);
                if (e == null) { Debug.LogWarning("[Enemigos] no hay " + t.Name); continue; }
                p = Object.FindFirstObjectByType<PlayerControler>();
                Teletransportar(e.transform.position + Vector3.left * 3.5f + Vector3.up * 0.3f);
                Mirar(1);
                SetCampo(p, "isInvincible", false);
                int v0 = Vida();
                int golpes = 0, hitsPrev = v0;
                for (float s = 0f; s < 7f; s += 0.5f)
                {
                    if (p == null) break;
                    if (Vida() < hitsPrev) golpes++;
                    hitsPrev = Vida();
                    // Si le quitan mucho, se cura para seguir mirando.
                    if (Vida() < 40) { Curar(); hitsPrev = Vida(); }
                    if (Mathf.Approximately(s % 1.5f, 0f)) yield return Captura($"e_{t.Name}_{s:0.0}", false);
                    yield return new WaitForSeconds(0.5f);
                }
                EnemigoBase eb = e as EnemigoBase;
                Debug.Log("[Audio] combate: " + Audio());
            Debug.Log($"[Enemigos] {t.Name}: golpes al player={golpes} vida {v0}->{Vida()} pos enemigo={(e != null ? e.transform.position.ToString("0.0") : "-")}");

                // Afinidad: un golpe con su debilidad.
                if (e != null)
                {
                    IAfinidadElemental af = e.GetComponent<IAfinidadElemental>();
                    string lista = "";
                    foreach (Elemento el in Elementos.Todos) lista += $"{el}={af.Multiplicador(el):0.0} ";
                    Debug.Log($"[Enemigos] {t.Name} afinidad: {lista}");
                }
                Curar();
            }
        }

        // ------------------------------------------------------------------ Jefe

        private IEnumerator Jefe()
        {
            Teletransportar(new Vector3(368f, 3.8f, 0f));
            yield return new WaitForSeconds(1f);
            JefeSombra j = null;
            for (float t = 0f; t < 5f && j == null; t += 0.2f) { j = Object.FindFirstObjectByType<JefeSombra>(); yield return new WaitForSeconds(0.2f); }
            Debug.Log($"[Jefe] aparece={(j != null)}");
            if (j == null) yield break;
            EnemyHealth s = j.GetComponent<EnemyHealth>();
            yield return new WaitForSeconds(2.5f);
            Debug.Log($"[Jefe] vida fase1={s.CurrentHealth}/{s.MaxHealth}");
            Debug.Log("[Audio] primer intento: " + Audio());
            yield return Captura("j01_entrada", true);

            // Fase 1: se mira que ataques usa (player invulnerable).
            string ultimo = "";
            var vistos = new HashSet<string>();
            for (float t = 0f; t < 22f; t += 0.25f)
            {
                Invulnerable();
                Curar();
                string a = (string)GetCampo(j, "ultimoAtaque");
                if (a != ultimo) { ultimo = a; vistos.Add(a); Debug.Log($"[Jefe] f1 ataque: {a} t={t:0.0}"); }
                if (Mathf.Approximately(t % 2f, 0f)) yield return Captura($"j02_f1_{t:00}", true);
                yield return new WaitForSeconds(0.25f);
            }
            Debug.Log($"[Jefe] ataques fase 1 vistos: {string.Join(", ", vistos)}");

            // Agarre: sin invulnerabilidad, a ver si conecta y mata.
            SetCampo(p, "isInvincible", false);
            SetCampo(j, "ultimoAtaque", "");
            j.StopAllCoroutines();
            j.StartCoroutine((IEnumerator)Llamar(j, "Agarre"));
            for (float t = 0f; t < 5f; t += 0.25f)
            {
                if (Mathf.Approximately(t % 0.5f, 0f)) yield return Captura($"j03_agarre_{t:0.0}", true);
                yield return new WaitForSecondsRealtime(0.25f);
            }
            Debug.Log($"[Jefe] tras agarre: player vivo={(Object.FindFirstObjectByType<PlayerControler>() != null && Vida() > 0)} pantalla muerte={PantallaMuerte.Activa}");
            yield return new WaitForSecondsRealtime(4f);
            p = Object.FindFirstObjectByType<PlayerControler>();
            Debug.Log($"[Jefe] reaparece: {(p != null)} jefe reiniciado={(Object.FindFirstObjectByType<JefeSombra>() == null)}");

            // Otra vez a la arena; se le vacia la fase 1 para ver la resurreccion.
            Teletransportar(new Vector3(368f, 3.8f, 0f));
            for (float t = 0f; t < 5f && (j = Object.FindFirstObjectByType<JefeSombra>()) == null; t += 0.2f) yield return new WaitForSeconds(0.2f);
            if (j == null) yield break;
            s = j.GetComponent<EnemyHealth>();
            yield return new WaitForSeconds(3.5f);
            Debug.Log("[Audio] reintento: " + Audio());
            Invulnerable();
            s.DanoEstado(s.CurrentHealth + 10);
            Debug.Log($"[Jefe] fase 1 vaciada: agotado={s.Agotado} muerto={s.Muerto}");
            for (float t = 0f; t < 9f; t += 0.5f)
            {
                Invulnerable();
                yield return Captura($"j04_revive_{t:0.0}", true);
                yield return new WaitForSecondsRealtime(0.5f);
            }
            Debug.Log($"[Jefe] tras revivir: fase2={GetCampo(j, "fase2")} vida={s.CurrentHealth}/{s.MaxHealth} escarcha={Object.FindObjectsByType<ZonaEscarcha>(FindObjectsSortMode.None).Length}");

            vistos.Clear();
            for (float t = 0f; t < 25f; t += 0.25f)
            {
                Invulnerable();
                Curar();
                string a = (string)GetCampo(j, "ultimoAtaque");
                if (a != ultimo) { ultimo = a; vistos.Add(a); Debug.Log($"[Jefe] f2 ataque: {a} t={t:0.0}"); }
                if (Mathf.Approximately(t % 2.5f, 0f)) yield return Captura($"j05_f2_{t:00.0}", true);
                yield return new WaitForSeconds(0.25f);
            }
            Debug.Log($"[Jefe] ataques fase 2 vistos: {string.Join(", ", vistos)} lento={GetCampo(p, "factorLento")}");

            // Remate.
            s.DanoEstado(s.CurrentHealth + 10);
            yield return new WaitForSeconds(2f);
            Debug.Log($"[Jefe] derrotado: muerto={s == null || s.Muerto}");
            yield return Captura("j06_victoria", true);
            yield return new WaitForSeconds(4f);
            Debug.Log($"[Jefe] salida activa={(GameObject.Find("ExitDoor") != null)} almas={Progreso.Almas}");
        }

        // ------------------------------------------------------------------ Cueva

        private IEnumerator Cueva()
        {
            ArenaJefe arena = Object.FindFirstObjectByType<ArenaJefe>();
            Teletransportar(new Vector3(146f, 4.8f, 0f));
            JefeWraith j = null;
            for (float t = 0f; t < 6f && (j = Object.FindFirstObjectByType<JefeWraith>()) == null; t += 0.2f) yield return new WaitForSeconds(0.2f);
            Debug.Log($"[Cueva] jefe={(j != null)}");
            if (j == null) yield break;
            yield return new WaitForSeconds(3f);
            AudioSource[] fuentes = arena.GetComponents<AudioSource>();
            Debug.Log($"[Cueva] musica f1: {fuentes[0].clip?.name} t={fuentes[0].time:0.0} vol={fuentes[0].volume:0.00}");
            EnemyHealth s = j.GetComponent<EnemyHealth>();
            Invulnerable();
            s.DanoEstado(s.MaxHealth / 2 + 5);
            yield return new WaitForSeconds(4f);
            Debug.Log($"[Cueva] musica f2: {fuentes[1].clip?.name} t={fuentes[1].time:0.0} vol={fuentes[1].volume:0.00} (f1 vol={fuentes[0].volume:0.00})");
            yield return Captura("c01_fase2", true);

            // Debilidad: sagrado frente a espada sin imbuir.
            IAfinidadElemental af = j;
            Debug.Log($"[Cueva] fase2 sagrado x{af.Multiplicador(Elemento.Sagrado)} oscuro x{af.Multiplicador(Elemento.Oscuro)}");
        }

        // ------------------------------------------------------------------ Ayudas

        private static string Audio()
        {
            DatosNivel d = Object.FindFirstObjectByType<DatosNivel>();
            string s = "";
            if (d != null) foreach (AudioSource a in d.GetComponents<AudioSource>()) s += $"{a.clip.name}={a.volume:0.00} ";
            ArenaJefe ar = Object.FindFirstObjectByType<ArenaJefe>();
            if (ar != null) foreach (AudioSource a in ar.GetComponents<AudioSource>()) s += $"jefe:{(a.clip != null ? a.clip.name : "-")}={a.volume:0.00} ";
            return s;
        }

        private GatherInput Entrada() => p.GetComponent<GatherInput>();
        private int Vida() => p != null ? (int)GetCampo(p, "currentHealth") : 0;

        private void Curar()
        {
            if (p == null) return;
            SetCampo(p, "currentHealth", p.VidaMaxima);
        }

        private void Invulnerable()
        {
            if (p == null) p = Object.FindFirstObjectByType<PlayerControler>();
            if (p != null) SetCampo(p, "isInvincible", true);
        }

        private void Teletransportar(Vector3 pos)
        {
            if (p == null) p = Object.FindFirstObjectByType<PlayerControler>();
            if (p == null) return;
            Rigidbody2D rb = p.GetComponent<Rigidbody2D>();
            rb.position = pos;
            rb.linearVelocity = Vector2.zero;
            p.transform.position = pos;
        }

        private void Mirar(int dir)
        {
            if (p == null) return;
            Llamar(p, "SetFacing", dir);
        }

        private void PonerEje(float x) => SetCampo(Entrada(), "_value", new Vector2(x, 0f));

        private void Imbuir(Elemento e)
        {
            ArmaImbuida a = p.GetComponent<ArmaImbuida>();
            a.Activar(e);
        }

        private IEnumerator Atacar(int dir, int veces)
        {
            p.GetComponent<PlayerStamina>().Llenar();
            for (int i = 0; i < veces; i++)
            {
                Mirar(dir);
                Entrada().IsAttacking = true;
                yield return new WaitForSeconds(0.45f);
            }
        }

        private EnemyHealth Cercano<T>() where T : Component
        {
            T[] todos = Object.FindObjectsByType<T>(FindObjectsSortMode.None);
            T mejor = null;
            foreach (T t in todos)
                if (mejor == null || Vector2.Distance(t.transform.position, p.transform.position) < Vector2.Distance(mejor.transform.position, p.transform.position))
                    mejor = t;
            return mejor != null ? mejor.GetComponent<EnemyHealth>() : null;
        }

        private static object GetCampo(object o, string campo)
        {
            FieldInfo f = o.GetType().GetField(campo, Priv | BindingFlags.Public);
            return f != null ? f.GetValue(o) : null;
        }

        private static void SetCampo(object o, string campo, object valor)
        {
            FieldInfo f = o.GetType().GetField(campo, Priv | BindingFlags.Public);
            if (f != null) f.SetValue(o, valor);
            else Debug.LogWarning("[Prueba] sin campo " + campo);
        }

        private static object Llamar(object o, string metodo, params object[] args)
        {
            MethodInfo m = o.GetType().GetMethod(metodo, Priv | BindingFlags.Public);
            if (m == null) { Debug.LogWarning("[Prueba] sin metodo " + metodo); return null; }
            return m.Invoke(o, args);
        }

        // Captura de la camara del juego; con UI pasa los canvas a la camara un momento.
        private IEnumerator Captura(string nombre, bool conUI)
        {
            Camera cam = Camera.main;
            if (cam == null) yield break;
            var cambiados = new List<Canvas>();
            if (conUI)
            {
                foreach (Canvas c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                    if (c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay && c.gameObject.activeInHierarchy)
                    { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = cam; c.planeDistance = 1f; c.sortingLayerName = "VFX"; c.sortingOrder = 500 + c.sortingOrder; cambiados.Add(c); }
                Canvas.ForceUpdateCanvases();
                yield return null;
            }
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
            foreach (Canvas c in cambiados) { c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder -= 500; }
        }
    }
}
