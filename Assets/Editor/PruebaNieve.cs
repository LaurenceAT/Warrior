using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    // Fotos del nivel en los puntos de la variable de entorno VISTAS ("x,y;x,y").
    public static void PintarCuevaPrueba() => Lanzar("pintarcueva", "Assets/Scenes/Nivel Cueva.unity");
    public static void DecoracionPrueba() => Lanzar("decoracion", System.Environment.GetEnvironmentVariable("VISTAS_ESCENA") ?? Escena);
    public static void PocionesPrueba() => Lanzar("pociones", Escena);
    public static void Ronda6Prueba() => Lanzar("ronda6", Escena);
    public static void MenuPrueba() => Lanzar("menu", "Assets/Scenes/Menu Principal.unity");
    public static void PintarPrueba() => Lanzar("pintar", Escena);
    public static void CornisaPrueba() => Lanzar("cornisa", Escena);
    public static void PortalPrueba() => Lanzar("portal", System.Environment.GetEnvironmentVariable("VISTAS_ESCENA") ?? Escena);
    public static void Vistas() => Lanzar("vistas", System.Environment.GetEnvironmentVariable("VISTAS_ESCENA") ?? Escena);

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
            if (modo == "menu")
            {
                Object.DontDestroyOnLoad(gameObject);
                IEnumerator rm = Menu();
                while (rm.MoveNext()) yield return rm.Current;
                Debug.Log("[Prueba] fin");
                EditorApplication.ExitPlaymode();
                yield break;
            }
            for (int k = 0; k < 8; k++)
            {
                yield return new WaitForSeconds(0.25f);
                PlayerControler pc = Object.FindFirstObjectByType<PlayerControler>();
                if (pc != null) Debug.Log($"[Prueba] inicio t={k * 0.25f:0.00} player={pc.transform.position}");
                else Debug.Log($"[Prueba] inicio t={k * 0.25f:0.00} sin player");
            }
            p = Object.FindFirstObjectByType<PlayerControler>();
            if (p == null || System.Environment.GetEnvironmentVariable("VISTAS_COLIS") == "1")
                foreach (var tmc in Object.FindObjectsByType<UnityEngine.Tilemaps.TilemapCollider2D>(FindObjectsSortMode.None))
                {
                    var cc = tmc.GetComponent<CompositeCollider2D>();
                    var tmap = tmc.GetComponent<UnityEngine.Tilemaps.Tilemap>();
                    Debug.Log($"[Colis] {tmc.name} capa={tmc.gameObject.layer} activo={tmc.enabled} formas={tmc.shapeCount} bounds={tmc.bounds} op={tmc.compositeOperation} " +
                              $"composite={(cc != null ? cc.pathCount + " caminos " + cc.bounds : "-")} tiles={tmap.GetUsedTilesCount()} tipo(2,-1)={tmap.GetColliderType(new Vector3Int(2, -1, 0))}");
                }
            if (p == null) { Debug.LogError("[Prueba] sin player"); EditorApplication.ExitPlaymode(); yield break; }
            Debug.Log($"[Prueba] modo={modo} player={(p != null)} vida={Vida()}/{p.VidaMaxima}");
            IEnumerator rutina = modo == "sistemas" ? Sistemas() : modo == "nivel" ? Nivel() : modo == "enemigos" ? Enemigos()
                               : modo == "jefe" ? Jefe() : modo == "vistas" ? Vistas() : modo == "cornisa" ? Cornisa() : modo == "pintar" ? Pintar() : modo == "pintarcueva" ? PintarCueva() : modo == "decoracion" ? PintarDecoracion() : modo == "pociones" ? Pociones() : modo == "portal" ? Portal() : modo == "ronda6" ? Ronda6() : Cueva();
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
            Entrada().IsInteracting = true;
            yield return new WaitForSecondsRealtime(0.6f);
            Debug.Log($"[Sistemas] menu hoguera={MenuHoguera.Abierto} timeScale={Time.timeScale}");
            MenuHoguera menu = Object.FindFirstObjectByType<MenuHoguera>();
            int vidaAntes = p.VidaMaxima;
            foreach (Progreso.Estadistica e in new[] { Progreso.Estadistica.Vida, Progreso.Estadistica.Vida, Progreso.Estadistica.Mana, Progreso.Estadistica.ResGolpes })
                Llamar(menu, "Subir", e);
            yield return new WaitForSecondsRealtime(0.4f);
            Debug.Log($"[Sistemas] nivel={Progreso.NivelTotal} almas={Progreso.Almas} vida {vidaAntes}->{p.VidaMaxima} mana max={mana.Maximo} resGolpes={Progreso.ResGolpes}");
            // Descansar tras gastar mana y subir de nivel: todo al maximo nuevo.
            mana.Gastar(mana.Actual * 0.8f);
            PlayerStamina est = p.GetComponent<PlayerStamina>();
            float manaGastado = mana.Actual;
            Llamar(menu, "Descansar");
            yield return new WaitForSecondsRealtime(0.3f);
            Debug.Log($"[Sistemas] descanso: mana {manaGastado:0}->{mana.Actual:0}/{mana.Maximo:0} (ManaMax={Progreso.ManaMax:0}) " +
                      $"vida {Vida()}/{p.VidaMaxima} estamina {(est != null ? est.Fraction.ToString("0.00") : "-")} " +
                      $"frascos {ReservaPociones.Get().Cargas}/{ReservaPociones.Get().Maximo} mana {ReservaPociones.Get().CargasMana}/{ReservaPociones.Get().MaximoMana}");
            yield return Captura("s06_hoguera", true);
            Llamar(menu, "Cerrar");
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

            // Agarre con la vida llena: quita el 80 % y se sobrevive.
            SetCampo(p, "isInvincible", false);
            Curar();
            SetCampo(j, "ultimoAtaque", "");
            j.StopAllCoroutines();
            j.StartCoroutine((IEnumerator)Llamar(j, "Agarre"));
            for (float t = 0f; t < 5f; t += 0.25f)
            {
                if (Mathf.Approximately(t % 0.5f, 0f)) yield return Captura($"j03_agarre_lleno_{t:0.0}", true);
                yield return new WaitForSecondsRealtime(0.25f);
            }
            Debug.Log($"[Jefe] agarre con vida llena: vida={Vida()}/{p.VidaMaxima} agarrado={p.Agarrado}");

            // Otra vez, con el 75 % de la vida: mata.
            yield return new WaitForSeconds(1.5f);
            SetCampo(p, "isInvincible", false);
            SetCampo(p, "hasIFrames", false);
            SetCampo(p, "currentHealth", Mathf.RoundToInt(p.VidaMaxima * 0.75f));
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

        // ------------------------------------------------------------------ Cornisa y portal

        private IEnumerator Cornisa()
        {
            // Escalon de la meseta: x 291, arriba 5 (el suelo esta en 3).
            Invulnerable();
            Teletransportar(new Vector3(290.2f, 4.35f, 0f));
            Mirar(1);
            PonerEje(1f);
            AgarreCornisa a = p.GetComponent<AgarreCornisa>();
            Debug.Log($"[Cornisa] componente={(a != null)}");
            for (int k = 0; k < 14; k++)
            {
                yield return new WaitForSeconds(0.05f);
                Debug.Log($"[Cornisa] t={k * 0.05f:0.00} pos={p.transform.position} activo={(a != null && a.Activo)} sprite={p.GetComponent<SpriteRenderer>().sprite.name}");
                if (k % 2 == 0) yield return Captura($"k_{k:00}", false);
            }
            PonerEje(0f);
            yield return new WaitForSeconds(0.5f);
            Debug.Log($"[Cornisa] final pos={p.transform.position}");

            // Borde demasiado alto (pared de la bajada de 297 a 8 desde el suelo 3): no debe agarrarse.
            Teletransportar(new Vector3(296.6f, 3.7f, 0f));
            PonerEje(1f);
            yield return new WaitForSeconds(0.6f);
            Debug.Log($"[Cornisa] pared alta: pos={p.transform.position} activo={a.Activo}");
            PonerEje(0f);
        }

        private IEnumerator Portal()
        {
            Object.DontDestroyOnLoad(gameObject);
            PortalEntrada pe = Object.FindFirstObjectByType<PortalEntrada>();
            Debug.Log($"[Portal] entrada={(pe != null)} en {pe?.transform.position}");
            yield return Captura("p00_entrada", true);
            PortalNivel pn = Object.FindFirstObjectByType<PortalNivel>(FindObjectsInactive.Include);
            Debug.Log($"[Portal] salida={(pn != null)}");
            if (pn == null) yield break;
            pn.gameObject.SetActive(true);
            Invulnerable();
            Teletransportar(pn.transform.position + new Vector3(-1.4f, 0.7f, 0f));
            yield return new WaitForSeconds(0.8f);
            yield return Captura("p01_antes", true);
            Teletransportar(pn.transform.position + new Vector3(-0.5f, 0.7f, 0f));
            string escena = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            float t0 = Time.realtimeSinceStartup;
            int n = 0;
            while (Time.realtimeSinceStartup - t0 < 9f && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == escena)
            {
                if (p != null) Debug.Log($"[Portal] t={Time.realtimeSinceStartup - t0:0.00} x={p.transform.position.x:0.00} vx={p.GetComponent<Rigidbody2D>().linearVelocityX:0.0} cargando={PantallaCarga.Cargando}");
                if (n < 12) yield return Captura($"p02_{n++:00}", true);
                yield return new WaitForSecondsRealtime(0.2f);
            }
            yield return new WaitForSecondsRealtime(1.5f);
            Debug.Log($"[Portal] escena ahora={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name} cargando={PantallaCarga.Cargando}");
            yield return Captura("p03_llegada", true);
        }

        // ------------------------------------------------------------------ Pintar

        private IEnumerator Pintar()
        {
            var tm = Object.FindObjectsByType<UnityEngine.Tilemaps.Tilemap>(FindObjectsSortMode.None).First(t => t.name == "Suelo");
            var nieve = AssetDatabase.LoadAssetAtPath<TileTerreno>("Assets/Tiles/Nieve/Nieve (auto).asset");
            // Bloque de 4x2 flotando sobre el campamento (x 20-23, y 3-4).
            for (int x = 20; x < 24; x++) for (int y = 3; y < 5; y++) tm.SetTile(new Vector3Int(x, y, 0), nieve);
            yield return new WaitForFixedUpdate();
            for (int y = 4; y >= 3; y--)
            {
                string fila = "";
                for (int x = 20; x < 24; x++) fila += tm.GetSprite(new Vector3Int(x, y, 0))?.name + " | ";
                Debug.Log($"[Pintar] y={y}: {fila}");
            }
            Invulnerable();
            Teletransportar(new Vector3(21.5f, 7f, 0f));
            yield return new WaitForSeconds(1.2f);
            Debug.Log($"[Pintar] player sobre el bloque pintado: y={p.transform.position.y:0.00} (arriba del bloque = 5, de pie = 5.63)");
            yield return Captura("pintar", true);
            // Borrar una casilla: deja de ser suelo.
            for (int y = 3; y < 5; y++) { tm.SetTile(new Vector3Int(21, y, 0), null); tm.SetTile(new Vector3Int(22, y, 0), null); }
            yield return new WaitForSeconds(1f);
            Debug.Log($"[Pintar] tras borrar el centro: y={p.transform.position.y:0.00} (deberia caer al suelo 0 => 0.63)");
        }

        // ------------------------------------------------------------------ Cueva pintada

        private IEnumerator PintarCueva()
        {
            var tms = Object.FindObjectsByType<UnityEngine.Tilemaps.Tilemap>(FindObjectsSortMode.None);
            var falsas = tms.First(t => t.name == "ParedesFalsas");
            var ocultas = tms.First(t => t.name == "ZonasOcultas");
            var roca = AssetDatabase.LoadAssetAtPath<UnityEngine.Tilemaps.Tile>("Assets/Tiles/Cueva/Roca cueva.asset");
            var sombra = AssetDatabase.LoadAssetAtPath<UnityEngine.Tilemaps.Tile>("Assets/Tiles/Comun/Sombra.asset");
            // Pared falsa que cierra el pasillo de entrada (x 10, del suelo 0 al techo 7)
            // y, detras, una zona oculta de x 11 a 15.
            for (int y = 0; y < 7; y++) falsas.SetTile(new Vector3Int(10, y, 0), roca);
            for (int x = 11; x < 16; x++) for (int y = 0; y < 7; y++) ocultas.SetTile(new Vector3Int(x, y, 0), sombra);
            Invulnerable();
            Teletransportar(new Vector3(7.5f, 0.7f, 0f));
            yield return new WaitForSeconds(1f);
            yield return Captura("pc0_fuera", true);
            PonerEje(1f);
            float t0 = Time.time;
            while (p.transform.position.x < 10.5f && Time.time - t0 < 3f) yield return null;
            yield return Captura("pc1_dentro_pared", true);
            yield return new WaitForSeconds(0.5f);
            PonerEje(0f);
            yield return new WaitForSeconds(0.6f);
            Debug.Log($"[PintarCueva] tras cruzar: x={p.transform.position.x:0.00} (la pared esta en 10-11)");
            yield return Captura("pc2_detras", true);
        }

        // ------------------------------------------------------------------ Decoracion pintada

        private IEnumerator PintarDecoracion()
        {
            var tm = Object.FindObjectsByType<UnityEngine.Tilemaps.Tilemap>(FindObjectsSortMode.None).First(t => t.name == "Decoracion");
            string carpeta = System.Environment.GetEnvironmentVariable("VISTAS_DECO") ?? "Assets/Tiles/Nieve/Decoracion";
            var tiles = Directory.GetFiles(carpeta, "*.asset").Select(f => AssetDatabase.LoadAssetAtPath<UnityEngine.Tilemaps.Tile>(f.Replace(Path.DirectorySeparatorChar, '/'))).Where(t => t != null).ToArray();
            // En fila sobre el suelo del campamento (y = 0), cada 3 casillas.
            int y = int.Parse(System.Environment.GetEnvironmentVariable("VISTAS_DECO_Y") ?? "0");
            for (int i = 0; i < tiles.Length; i++) tm.SetTile(new Vector3Int(4 + i * 3, y, 0), tiles[i]);
            Debug.Log($"[Deco] pintados {tiles.Length} adornos");
            Invulnerable();
            for (int k = 0; k < 3; k++)
            {
                Teletransportar(new Vector3(10 + k * 16, y + 0.7f, 0f));
                yield return new WaitForSeconds(1f);
                yield return Captura($"d{k}", true);
            }
        }

        // ------------------------------------------------------------------ Pociones

        private IEnumerator Pociones()
        {
            PlayerMana mana = p.GetComponent<PlayerMana>();
            ReservaPociones r = ReservaPociones.Get();
            Invulnerable();
            // Gastar mana imbuyendo y comprobar que golpear no lo devuelve.
            Imbuir(Elemento.Oscuro);
            mana.Gastar(40f);
            yield return new WaitForSeconds(0.3f);
            float tras = mana.Actual;
            EnemyHealth rata = Cercano<EnemigoRata>();
            if (rata != null)
            {
                Teletransportar(rata.transform.position + Vector3.left * 1f);
                yield return Atacar(1, 4);
            }
            Debug.Log($"[Pociones] mana tras imbuir={tras:0} tras golpear={mana.Actual:0} (no debe subir)");

            // R: frasco de mana.
            float m0 = mana.Actual;
            int c0 = r.CargasMana;
            Entrada().IsManaPotion = true;
            yield return new WaitForSeconds(1.5f);
            Debug.Log($"[Pociones] pocion de mana: mana {m0:0}->{mana.Actual:0} cargas {c0}->{r.CargasMana} vida cargas={r.Cargas}");
            // Beber cortado por un golpe: la carga se devuelve.
            mana.Gastar(30f);
            int c1 = r.CargasMana;
            Entrada().IsManaPotion = true;
            yield return new WaitForSeconds(0.1f);
            Llamar(p, "CortarBebida");
            yield return new WaitForSeconds(0.5f);
            Debug.Log($"[Pociones] trago cortado: cargas {c1}->{r.CargasMana} (deben ser iguales)");
            // Q: frasco de sangre.
            SetCampo(p, "currentHealth", 40);
            int v0 = r.Cargas;
            Entrada().IsHealing = true;
            yield return new WaitForSeconds(1.5f);
            Debug.Log($"[Pociones] Q: vida 40->{Vida()} cargas vida {v0}->{r.Cargas}");
            yield return Captura("po1_hud", true);

            // La hoguera rellena las pociones pero no el mana.
            float m1 = mana.Actual;
            (typeof(Hoguera).GetField("AlDescansar", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null) as System.Action)?.Invoke();
            yield return new WaitForSeconds(0.3f);
            Debug.Log($"[Pociones] tras hoguera: mana {m1:0}->{mana.Actual:0} cargas mana={r.CargasMana}/{r.MaximoMana}");
        }

        // ------------------------------------------------------------------ Ronda 6

        private IEnumerator Ronda6()
        {
            PlayerMana mana = p.GetComponent<PlayerMana>();
            PlayerStamina est = p.GetComponent<PlayerStamina>();
            ReservaPociones r = ReservaPociones.Get();
            PlayerHud hud = PlayerHud.Get();
            Invulnerable();
            yield return new WaitForSeconds(1.5f);

            // 1. Valores y largos iniciales.
            float Ancho(string barra) => ((RectTransform)hud.transform.Find("HUD/" + barra)).sizeDelta.x;
            Debug.Log($"[R6] inicial vida={Vida()}/{p.VidaMaxima} estamina={est.Max} mana={mana.Actual}/{mana.Maximo} " +
                      $"anchos vida={Ancho("Vida"):0} estamina={Ancho("Estamina"):0} mana={Ancho("Mana"):0}");
            yield return Captura("r6_01_hud_inicial", true);

            // 2. Mana: la barra sigue al mana (antes se quedaba quieta).
            float Relleno(string barra) => ((RectTransform)PlayerHud.Get().transform.Find("HUD/" + barra + "/Relleno")).anchorMax.x;
            mana.Gastar(50f);
            yield return null;
            yield return null;
            Debug.Log($"[R6] mana gastado={mana.Actual:0} fraccion={mana.Fraccion:0.00} barra={Relleno("Mana"):0.00}");
            Entrada().IsManaPotion = true;
            yield return new WaitForSeconds(1.6f);
            Debug.Log($"[R6] frasco mana: mana={mana.Actual:0} barra={Relleno("Mana"):0.00} cargas={r.CargasMana}/{r.MaximoMana}");
            Entrada().IsManaPotion = true;
            yield return new WaitForSeconds(0.6f);
            Debug.Log($"[R6] segundo intento sin cargas: cargas={r.CargasMana}");
            yield return Captura("r6_02_frascos", true);

            // 3. Imbuir sangrado: anillo del rombo y sangrado en una rata.
            mana.Llenar();
            Llamar(p, "EmpezarImbuir", Elemento.Sangrado);
            yield return new WaitForSeconds(1.2f);
            ArmaImbuida arma = p.GetComponent<ArmaImbuida>();
            UnityEngine.UI.Image anillo = hud.transform.Find("HUD/Anillo").GetComponent<UnityEngine.UI.Image>();
            Debug.Log($"[R6] imbuido={arma.Activo} mana={mana.Actual:0} anillo={anillo.enabled} fill={anillo.fillAmount:0.00} restante={arma.FraccionRestante:0.00}");
            EnemyHealth rata = Cercano<EnemigoRata>();
            if (rata != null)
            {
                SetCampo(rata, "maxHealth", 2000);
                SetCampo(rata, "currentHealth", 2000);
                Teletransportar(rata.transform.position + Vector3.left * 1f);
                for (int i = 0; i < 4; i++)
                {
                    yield return Atacar(1, 1);
                    EstadosEnemigo ee = rata != null ? rata.GetComponent<EstadosEnemigo>() : null;
                    Debug.Log($"[R6] golpe {i + 1}: vida rata={(rata != null ? rata.CurrentHealth : -1)} sangrado={(ee != null ? ee.SangradoFraccion : 0f):0.00} iconos={(ee != null ? ee.Activos().Count : 0)}");
                    if (i == 1) yield return Captura("r6_03_iconos_enemigo", true);
                }
                EstadosEnemigo e2 = rata != null ? rata.GetComponent<EstadosEnemigo>() : null;
                yield return new WaitForSeconds(0.5f);
                float s0 = e2 != null ? e2.SangradoFraccion : 0f;
                yield return new WaitForSeconds(3f);
                Debug.Log($"[R6] sangrado baja solo: {s0:0.00}->{(e2 != null ? e2.SangradoFraccion : 0f):0.00}");
            }
            yield return Captura("r6_04_anillo", true);

            // 4. Resistencias: separadas por tipo y con tope.
            int Golpe(PlayerControler.TipoDano tipo)
            {
                Curar();
                SetCampo(p, "isInvincible", false);
                SetCampo(p, "hasIFrames", false);
                int v = Vida();
                p.TakeDamage(100, null, tipo);
                int d = v - Vida();
                Curar();
                return d;
            }
            foreach (int n in new[] { 0, 1, 5, 10, 20 })
            {
                Progreso.Establecer(0, new[] { 0, 0, 0, 0, n }, 0, Vector2.zero, "");
                Debug.Log($"[R6] resGolpes nivel {n}: fisico 100 -> {Golpe(PlayerControler.TipoDano.Fisico)}  magico 100 -> {Golpe(PlayerControler.TipoDano.Magico)}  ({Progreso.ResGolpes * 100f:0} %)");
                yield return new WaitForSeconds(0.1f);
            }
            Progreso.Establecer(0, new[] { 0, 0, 0, 10, 0 }, 0, Vector2.zero, "");
            Debug.Log($"[R6] resHechizos 10: fisico -> {Golpe(PlayerControler.TipoDano.Fisico)}  magico -> {Golpe(PlayerControler.TipoDano.Magico)}");
            // El agarre (fisico) con resistencia a golpes 10.
            Progreso.Establecer(0, new[] { 0, 0, 0, 0, 10 }, 0, Vector2.zero, "");
            Curar();
            SetCampo(p, "isInvincible", false);
            SetCampo(p, "hasIFrames", false);
            yield return null;
            bool agarrado = p.IntentarAgarre();
            int total = Mathf.RoundToInt(p.VidaMaxima * 0.8f);
            int v1 = Vida();
            for (int i = 0; i < 4; i++) p.CorteAgarre(Mathf.RoundToInt(total * 0.15f));
            p.CorteAgarre(total - 4 * Mathf.RoundToInt(total * 0.15f));
            Debug.Log($"[R6] agarre={agarrado} (80 % = {total}) con resGolpes 10: quita {v1 - Vida()}");
            p.SoltarAgarre(false);
            Progreso.Reiniciar();
            yield return new WaitForSeconds(1.5f);
            Curar();

            // 5. Estados del player: se llena, baja sola y desaparece; al llenarse, salta.
            SetCampo(p, "isInvincible", true);
            EstadosPlayer.Acumular(p, EstadoPlayer.Sangrado, 70f);
            EstadosPlayer.Acumular(p, EstadoPlayer.Congelacion, 40f);
            EstadosPlayer ep = p.GetComponent<EstadosPlayer>();
            yield return new WaitForSeconds(0.4f);
            yield return Captura("r6_05_estados_player", true);
            string Fr() => $"sangrado={ep.Fraccion(EstadoPlayer.Sangrado):0.00} congelacion={ep.Fraccion(EstadoPlayer.Congelacion):0.00}";
            Debug.Log($"[R6] estados al recibir: {Fr()}");
            float antes = ep.Fraccion(EstadoPlayer.Sangrado);
            yield return new WaitForSeconds(2.5f);
            float a1 = ep.Fraccion(EstadoPlayer.Sangrado);
            yield return new WaitForSeconds(1f);
            float a2 = ep.Fraccion(EstadoPlayer.Sangrado);
            yield return new WaitForSeconds(1f);
            float a3 = ep.Fraccion(EstadoPlayer.Sangrado);
            Debug.Log($"[R6] bajada sangrado: {antes:0.00} -> {a1:0.00} -> {a2:0.00} -> {a3:0.00} (lenta arriba, rapida abajo)");
            yield return new WaitForSeconds(6f);
            Transform filaS = hud.transform.Find("HUD/Estado_Sangrado");
            Debug.Log($"[R6] tras bajar: {Fr()} fila visible={filaS.gameObject.activeSelf}");
            Curar();
            int vs = Vida();
            EstadosPlayer.Acumular(p, EstadoPlayer.Sangrado, 110f);
            Debug.Log($"[R6] sangrado lleno: vida {vs}->{Vida()} en efecto={ep.EnEfecto(EstadoPlayer.Sangrado)}");
            Curar();

            // 6. Hoguera con F: menu, subir nivel (con numeros exactos), mejorar.
            Hoguera h = Object.FindObjectsByType<Hoguera>(FindObjectsSortMode.None).OrderBy(x => Vector2.Distance(x.transform.position, p.transform.position)).First();
            Teletransportar(h.transform.position + Vector3.right * 0.5f + Vector3.up * 0.6f);
            yield return new WaitForSeconds(0.6f);
            Progreso.SumarAlmas(5000);
            Equipo.Sumar(Equipo.Objeto.PiedraForja, 1);
            Equipo.Sumar(Equipo.Objeto.LagrimaSagrada, 1);
            Debug.Log($"[R6] interactuable cerca={(Interacciones.Actual as Component)?.name}");
            Entrada().IsInteracting = true;
            yield return new WaitForSecondsRealtime(0.6f);
            MenuHoguera menu = Object.FindFirstObjectByType<MenuHoguera>();
            Debug.Log($"[R6] menu hoguera abierto={MenuHoguera.Abierto}");
            yield return Captura("r6_06_hoguera_principal", true);
            Llamar(menu, "Descansar");
            yield return new WaitForSecondsRealtime(0.3f);
            Debug.Log($"[R6] descansar: cargas mana={r.CargasMana}/{r.MaximoMana} vida={r.Cargas}/{r.Maximo}");
            Llamar(menu, "Mostrar", Enum("MenuHoguera+Pagina", "Nivel"));
            yield return new WaitForSecondsRealtime(0.3f);
            int vmax0 = p.VidaMaxima;
            float emax0 = est.Max, mmax0 = mana.Maximo;
            Llamar(menu, "Subir", Progreso.Estadistica.Vida);
            Llamar(menu, "Subir", Progreso.Estadistica.Estamina);
            Llamar(menu, "Subir", Progreso.Estadistica.Mana);
            Llamar(menu, "Subir", Progreso.Estadistica.ResGolpes);
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Captura("r6_07_hoguera_nivel", true);
            Llamar(menu, "Mostrar", Enum("MenuHoguera+Pagina", "Equipo"));
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Captura("r6_08_hoguera_equipo_antes", true);
            Llamar(menu, "MejorarEspada");
            Llamar(menu, "MejorarFrascos");
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Captura("r6_09_hoguera_equipo_despues", true);
            Debug.Log($"[R6] espada +{Equipo.NivelEspada} x{Equipo.MultiplicadorEspada:0.00} frascos +{Equipo.NivelFrascos} cura={Equipo.CuraFrasco:0.00} mana={Equipo.ManaFrasco:0.00}");
            float anchoAntes = Ancho("Vida");
            Llamar(menu, "Cerrar");
            yield return new WaitForSeconds(0.45f);
            yield return Captura("r6_10_barras_creciendo", true);
            float anchoMedio = Ancho("Vida");
            yield return new WaitForSeconds(1.2f);
            Debug.Log($"[R6] subir: vida {vmax0}->{p.VidaMaxima} estamina {emax0}->{est.Max} mana {mmax0}->{mana.Maximo}; ancho vida {anchoAntes:0} -> {anchoMedio:0} -> {Ancho("Vida"):0}");
            yield return Captura("r6_11_barras_crecidas", true);

            // 7. Cofre de mejora: se abre con F y se levanta el objeto.
            CofreMejora cofre = Object.FindFirstObjectByType<CofreMejora>();
            if (cofre != null)
            {
                int l0 = Equipo.Cantidad(Equipo.Objeto.LagrimaSagrada), p0 = Equipo.Cantidad(Equipo.Objeto.PiedraForja);
                Teletransportar(cofre.transform.position + Vector3.left * 0.6f + Vector3.up * 0.4f);
                yield return new WaitForSeconds(1f);
                yield return Captura("r6_12_cofre_brillo", false);
                Entrada().IsInteracting = true;
                yield return new WaitForSeconds(1.1f);
                Debug.Log($"[R6] cofre abierto={cofre.Abierto} levantando={p.LevantandoObjeto} lagrimas {l0}->{Equipo.Cantidad(Equipo.Objeto.LagrimaSagrada)} piedras {p0}->{Equipo.Cantidad(Equipo.Objeto.PiedraForja)}");
                yield return Captura("r6_13_objeto_levantado", true);
                yield return new WaitForSeconds(2.5f);
                Debug.Log($"[R6] tras levantar: levantando={p.LevantandoObjeto}");
            }
            else Debug.LogWarning("[R6] no hay cofre de mejora en la escena");

            // 8. Recompensa del jefe.
            ArenaJefe arena = Object.FindFirstObjectByType<ArenaJefe>();
            if (arena != null)
            {
                int pi = Equipo.Cantidad(Equipo.Objeto.PiedraForja);
                arena.StartCoroutine((IEnumerator)Llamar(arena, "DarRecompensa"));
                yield return new WaitForSeconds(3.5f);
                Debug.Log($"[R6] recompensa jefe: piedras {pi}->{Equipo.Cantidad(Equipo.Objeto.PiedraForja)} levantando={p.LevantandoObjeto}");
                yield return Captura("r6_14_recompensa_jefe", true);
                yield return new WaitForSeconds(3f);
            }

            // 9. Pausa.
            GameManager.Instance.PauseGame();
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Captura("r6_15_pausa", true);
            GameManager.Instance.ResumeGame();

            // 10. Muerte y reaparicion: el mana vuelve lleno (y la barra tambien).
            mana.Gastar(40f);
            Debug.Log($"[R6] antes de morir: mana={mana.Actual:0} barra={Relleno("Mana"):0.00}");
            p.DanoEstado(99999);
            for (int i = 0; i < 30; i++)
            {
                yield return new WaitForSeconds(0.5f);
                if (PantallaMuerte.Activa) { PantallaMuerte pm = Object.FindFirstObjectByType<PantallaMuerte>(); SetCampo(pm, "puedeContinuar", true); SetCampo(pm, "continuar", true); }
                PlayerControler nuevo = Object.FindObjectsByType<PlayerControler>(FindObjectsSortMode.None).FirstOrDefault(x => x.VidaActual > 0);
                if (nuevo != null) { p = nuevo; break; }
            }
            yield return new WaitForSeconds(1f);
            if (p != null)
            {
                PlayerMana m2 = p.GetComponent<PlayerMana>();
                Debug.Log($"[R6] reaparecido: vida={p.VidaActual} mana={m2.Actual:0}/{m2.Maximo:0} barra={Relleno("Mana"):0.00} cargas mana={r.CargasMana}");
            }
        }

        private static object Enum(string tipo, string valor)
        {
            System.Type t = typeof(MenuHoguera).Assembly.GetType(tipo);
            return System.Enum.Parse(t, valor);
        }

        // ------------------------------------------------------------------ Menu principal

        private IEnumerator Menu()
        {
            Partida.CarpetaPruebas = Path.Combine(carpeta, "Partidas");
            if (Directory.Exists(Partida.CarpetaPruebas)) Directory.Delete(Partida.CarpetaPruebas, true);
            yield return new WaitForSeconds(2.5f);
            MenuPrincipal m = Object.FindFirstObjectByType<MenuPrincipal>();
            Debug.Log($"[Menu] menu={(m != null)} escena={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
            yield return Captura("m0_principal", true);
            foreach (string panel in new[] { "panelControles", "panelOpciones", "panelCargar" })
            {
                Llamar(m, "Mostrar", GetCampo(m, panel));
                yield return new WaitForSecondsRealtime(0.4f);
                yield return Captura("m1_" + panel, true);
            }
            var sr = ((GameObject)GetCampo(m, "panelControles")).GetComponentInChildren<UnityEngine.UI.ScrollRect>(true);
            Llamar(m, "Mostrar", GetCampo(m, "panelControles"));
            yield return null;
            sr.verticalNormalizedPosition = 0.35f;
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Captura("m2_controles_medio", true);
            sr.verticalNormalizedPosition = 0f;
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Captura("m2_controles_fin", true);
            Llamar(m, "Mostrar", GetCampo(m, "panelPrincipal"));

            // Nueva partida: al primer nivel (nieve), personaje a nivel 1.
            Progreso.SumarAlmas(999);
            Llamar(m, "NuevaPartida");
            yield return EsperarEscena("Nivel Nieve");
            yield return new WaitForSeconds(2f);
            p = Object.FindFirstObjectByType<PlayerControler>();
            Debug.Log($"[Menu] nueva: escena={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name} almas={Progreso.Almas} nivel={Progreso.NivelTotal} ranura={Partida.Actual?.ranura} player={p?.transform.position}");

            // Descansar en la segunda hoguera: guarda ahi.
            Hoguera h = Object.FindObjectsByType<Hoguera>(FindObjectsSortMode.None).OrderBy(x => Mathf.Abs(x.transform.position.x - 116f)).First();
            Invulnerable();
            Teletransportar(h.transform.position + Vector3.right * 0.5f + Vector3.up * 0.6f);
            yield return new WaitForSeconds(0.5f);
            Progreso.SumarAlmas(1234);
            h.Usar(p);
            yield return new WaitForSecondsRealtime(0.5f);
            Llamar(Object.FindFirstObjectByType<MenuHoguera>(), "Descansar");
            Llamar(Object.FindFirstObjectByType<MenuHoguera>(), "Cerrar");
            yield return new WaitForSecondsRealtime(0.5f);
            Debug.Log($"[Menu] tras hoguera: lugar={Partida.Actual.lugar} enHoguera={Partida.Actual.enHoguera} pos=({Partida.Actual.x:0.0},{Partida.Actual.y:0.0}) almas={Partida.Actual.almas}");

            // Salir al menu y cargarla.
            PantallaCarga.Cargar(0);
            yield return EsperarEscena("Menu Principal");
            yield return new WaitForSeconds(2f);
            m = Object.FindFirstObjectByType<MenuPrincipal>();
            Llamar(m, "Mostrar", GetCampo(m, "panelCargar"));
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Captura("m3_lista_partidas", true);
            var lista = Partida.Listar();
            Debug.Log($"[Menu] partidas={lista.Count} primera={lista[0].escena}/{lista[0].lugar} tiempo={Partida.Tiempo(lista[0].segundosJugados)}");
            Progreso.Reiniciar();
            Llamar(m, "CargarPartida", lista[0]);
            yield return EsperarEscena("Nivel Nieve");
            yield return new WaitForSeconds(2f);
            p = Object.FindFirstObjectByType<PlayerControler>();
            Debug.Log($"[Menu] cargada: player={p?.transform.position} almas={Progreso.Almas} checkpoint={GameManager.Instance.hasCheckPointActive}");
            yield return Captura("m4_cargada", true);
            Partida.CarpetaPruebas = null;
        }

        private IEnumerator EsperarEscena(string nombre)
        {
            float t0 = Time.realtimeSinceStartup;
            while (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != nombre && Time.realtimeSinceStartup - t0 < 20f) yield return null;
            while (PantallaCarga.Cargando && Time.realtimeSinceStartup - t0 < 25f) yield return null;
        }

        // ------------------------------------------------------------------ Vistas

        private IEnumerator Vistas()
        {
            string lista = System.Environment.GetEnvironmentVariable("VISTAS") ?? "";
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            // Que se dibuja en un rectangulo ("x0,y0,x1,y1"), para localizar piezas sueltas.
            string zona = System.Environment.GetEnvironmentVariable("VISTAS_ZONA");
            if (!string.IsNullOrEmpty(zona))
            {
                float[] z = zona.Split(',').Select(v => float.Parse(v, ci)).ToArray();
                Rect r = Rect.MinMaxRect(z[0], z[1], z[2], z[3]);
                foreach (SpriteRenderer s in Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
                {
                    Bounds b = s.bounds;
                    if (!s.enabled || !r.Overlaps(Rect.MinMaxRect(b.min.x, b.min.y, b.max.x, b.max.y))) continue;
                    string ruta = s.name;
                    for (Transform t = s.transform.parent; t != null; t = t.parent) ruta = t.name + "/" + ruta;
                    Debug.Log($"[Zona] {ruta} sprite={(s.sprite != null ? s.sprite.name : "-")} capa={s.sortingLayerName}:{s.sortingOrder} valor={SortingLayer.GetLayerValueFromID(s.sortingLayerID)} color={s.color} mat={s.sharedMaterial?.name} grupo={s.GetComponentInParent<UnityEngine.Rendering.SortingGroup>() != null} z={s.transform.position.z} min={b.min} max={b.max}");
                }
            }
            if (System.Environment.GetEnvironmentVariable("VISTAS_ROJO") == "1")
                foreach (ZonaOculta z in Object.FindObjectsByType<ZonaOculta>(FindObjectsSortMode.None)) z.enabled = false;
            if (System.Environment.GetEnvironmentVariable("VISTAS_ROJO") == "1")
                foreach (SpriteRenderer s in Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
                    if (s.name == "Sombra" && s.GetComponentInParent<ZonaOculta>(true) != null) s.color = new Color(1f, 0f, 0f, 0.7f);
            int i = 0;
            foreach (string par in lista.Split(';'))
            {
                string[] xy = par.Split(',');
                if (xy.Length < 2) continue;
                Vector3 pos = new Vector3(float.Parse(xy[0], ci), float.Parse(xy[1], ci), 0f);
                Invulnerable();
                Teletransportar(pos);
                yield return new WaitForSeconds(1.2f);
                Debug.Log($"[Vistas] {i} pedido={pos} real={p.transform.position}");
                foreach (ZonaOculta z in Object.FindObjectsByType<ZonaOculta>(FindObjectsSortMode.None))
                {
                    var tmz = z.GetComponent<UnityEngine.Tilemaps.Tilemap>();
                    var celda = tmz.WorldToCell(p.transform.position + Vector3.up * 0.1f);
                    var zonas = (System.Collections.IList)GetCampo(z, "zonas");
                    var alfas = (List<float>)GetCampo(z, "alfas");
                    Debug.Log($"[Vistas] zona oculta: zonas={zonas?.Count} alfas={(alfas != null ? string.Join(",", alfas) : "-")} celda={celda} tile={tmz.HasTile(celda)} color={tmz.GetColor(celda)} tmcolor={tmz.color} flags={tmz.GetTileFlags(celda)}");
                }
                yield return Captura($"v{i:00}_{xy[0]}_{xy[1]}", true);
                i++;
            }
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
