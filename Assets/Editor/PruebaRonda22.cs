using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Pruebas de la Ronda 22 (linea de comandos, sin -quit):
//   ... -executeMethod PruebaRonda22.Cazadora
// The Blind Huntress: Cerco de ilusiones (apertura y en la pelea), premio por
// parry perfecto (vida, aturdimiento, tono, frase, logro y consejo secreto) y la
// Ejecucion doble. Juega un bot de tres maneras (parry a todo, solo bloqueo y
// sin bloquear) y mide la vida que pierde cada uno. Escribe "[R22] OK/FALLO ...".
[InitializeOnLoad]
public static class PruebaRonda22
{
    private const string Clave = "PruebaRonda22.Modo";

    static PruebaRonda22()
    {
        EditorApplication.playModeStateChanged += e =>
        {
            string modo = SessionState.GetString(Clave, "");
            if (modo == "") return;
            if (e == PlayModeStateChange.EnteredPlayMode) new GameObject("PruebaRonda22").AddComponent<Conductor>().modo = modo;
            else if (e == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetString(Clave, "");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
        };
    }

    public static void Cazadora()
    {
        SessionState.SetString(Clave, "cazadora");
        EditorSceneManager.OpenScene("Assets/Scenes/Menu Principal.unity");
        ArranquePruebas.Proteger();
        EditorApplication.EnterPlaymode();
    }

    private class Conductor : MonoBehaviour
    {
        public string modo;
        private int ok, fallos, errores;
        private PlayerControler p;
        private JefeCazadora j;
        private FichaJefe caz;
        private const BindingFlags Todo = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static;

        // El bot: 0 = no hace nada; 1 = parry a todo; 2 = solo bloqueo (sin parry).
        // "sinParar": ese atacante del cerco solo se bloquea (para probar el fallo).
        private int bot;
        private string sinParar;
        private bool rellenarVida = true;
        private readonly List<string> frases = new List<string>();

        private void Ok(string s) { ok++; Debug.Log("[R22] OK " + s); }
        private void Fallo(string s) { fallos++; Debug.LogWarning("[R22] FALLO " + s); }
        private void Comprobar(bool c, string s) { if (c) Ok(s); else Fallo(s); }

        private IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject);
            Application.logMessageReceived += (c, st, t) => { if ((t == LogType.Exception || t == LogType.Error) && !c.StartsWith("[R22]")) { errores++; Debug.Log("[R22] error visto: " + c); } };
            string carpeta = Path.Combine(Directory.GetCurrentDirectory(), "PruebaNieve");
            Partida.CarpetaPruebas = Path.Combine(carpeta, "PartidasR22");
            if (Directory.Exists(Partida.CarpetaPruebas)) Directory.Delete(Partida.CarpetaPruebas, true);
            Directory.CreateDirectory(Partida.CarpetaPruebas);
            Globales.Recargar();
            IEnumerator r = PruebaCazadora();
            float limite = Time.realtimeSinceStartup + 1500f;
            while (true)
            {
                bool sigue;
                try { sigue = r.MoveNext(); }
                catch (System.Exception e) { Fallo("excepcion: " + e); sigue = false; }
                if (!sigue || Time.realtimeSinceStartup > limite) break;
                yield return r.Current;
            }
            Comprobar(errores == 0, $"sin errores en la consola ({errores})");
            Debug.Log($"[R22] RESULTADO {modo}: {ok} OK, {fallos} FALLOS");
            Time.timeScale = 1f;
            Partida.Descargar();
            Globales.GuardarPendiente();
            EditorApplication.ExitPlaymode();
        }

        // El bot, cada fotograma: mira al atacante del cerco (o a ella) y guarda.
        private void Update()
        {
            if (p == null || j == null) return;
            if (bot != 0)
            {
                Vector2? a = j.CercoAtacante;
                float x = a.HasValue ? a.Value.x : j.transform.position.x;
                if (Mathf.Abs(x - p.transform.position.x) > 0.05f) Llamar(p, "SetFacing", x > p.transform.position.x ? 1 : -1);
                bool parry = bot == 1 && (sinParar == null || QuienVa() != sinParar);
                SetCampo(p, "isBlocking", true);
                SetCampo(p, "tBloqueo", parry ? 0f : 1f);
                SetCampo(p, "ventanaParryActual", (float)Campo(p, "parryWindow"));
                GatherInput gi = p.GetComponent<GatherInput>();
                if (gi != null) SetCampo(gi, "_isKickHeld", !parry);
                PlayerStamina st = p.GetComponent<PlayerStamina>();
                if (st != null) st.Recuperar(100f);
            }
            if (p.VidaActual < vidaVista && j.EnCerco)
                Debug.Log($"[R22] (golpe al player: -{vidaVista - p.VidaActual} de {p.VidaMaxima}, noLetal {Campo(j, "noLetalAccion")}, {j.RegistroCerco.LastOrDefault().que})");
            if (rellenarVida && p.VidaActual < p.VidaMaxima) SetCampo(p, "currentHealth", p.VidaMaxima);
            vidaVista = p.VidaActual;
        }
        private int vidaVista;

        private string QuienVa()
        {
            for (int i = j.RegistroCerco.Count - 1; i >= 0; i--)
                if (j.RegistroCerco[i].que.StartsWith("destello:")) return j.RegistroCerco[i].que.Substring(9);
            return null;
        }

        private IEnumerator PruebaCazadora()
        {
            yield return new WaitForSeconds(1f);
            caz = FichaJefe.DeId("blind_huntress");
            // Desbloqueada SOLO con el codigo: el premio no debe dar logro ni consejo.
            Globales.PonerMarca(CodigosSecretos.Marca(caz.id), true);
            Globales.PonerMarca("revelado_" + caz.id, true);
            Globales.PonerMarca("forzar_" + caz.id, false);
            Desafio.Empezar(caz, false);
            yield return EsperarEscena(caz.escena);
            yield return new WaitForSeconds(2.5f);
            p = Object.FindFirstObjectByType<PlayerControler>();

            // ------------------------------------------------ Apertura: "Voy a derrotarte"
            ArenaCazadora arena = ArenaCazadora.Actual;
            AjustesCazadora aj = (AjustesCazadora)Campo(arena, "ajustes");
            AjustesCazadora copia = Object.Instantiate(aj);
            copia.frasesEntrada = new string[0];
            copia.respuestaDesafiar = new string[0];
            SetCampo(arena, "ajustes", copia);
            ZonaJefe z = Object.FindFirstObjectByType<ZonaJefe>();
            Teletransportar(new Vector3(z.transform.position.x + z.distanciaActivador + 1f, 0.8f, 0f));
            DialogoCazadora dlg = null;
            yield return Esperar(() => (dlg = Object.FindFirstObjectByType<DialogoCazadora>()) != null && (int)Campo(dlg, "eleccion") < 0 && dlg.GetComponentsInChildren<UnityEngine.UI.Button>().Length >= 2, 10f);
            Comprobar(dlg != null, "sale el dialogo de la primera vez");
            j = JefeCazadora.Actual;
            j.AlFrase += f => frases.Add(f);
            bot = 1;
            if (dlg != null) SetCampo(dlg, "eleccion", 1);
            yield return Esperar(() => j.EnCerco, 6f);
            Comprobar(j.EnCerco && !j.Activa, "tras \"Voy a derrotarte\" empieza el Cerco de ilusiones (antes de su pelea normal)");
            yield return Esperar(() => !j.EnCerco, 15f);
            yield return Esperar(() => j.Activa, 8f);
            Comprobar(j.Activa, "al acabar el cerco de apertura empieza su combate normal");
            Comprobar(j.RegistroCerco.Any(r => r.que == "premio"), "con parry a todas las copias de la apertura hay premio");
            bot = 0;

            // ------------------------------------------------ Logro y consejo: con el codigo no
            Comprobar(!Globales.TieneLogro("cerco_perfecto"), "con el codigo \"warrior\" el parry perfecto no da el logro");
            Comprobar(!Bitacora.Tiene(Bitacora.ClaveSecreto(caz, "cerco")), "con el codigo \"warrior\" tampoco da el consejo secreto");
            // La marca antigua (bitacora_v1) y la victoria no lo dan.
            Bitacora.DesbloquearTodo(caz);
            Bitacora.Victoria(caz, false);
            Comprobar(!Bitacora.Tiene(Bitacora.ClaveSecreto(caz, "cerco")), "desbloquear todo (bitacora_v1) y vencerla no dan el consejo secreto");
            Globales.PonerMarca("forzar_" + caz.id, true);

            // ------------------------------------------------ Posiciones (barra 1: 3 copias)
            yield return Preparar(0f);
            var pos = new List<Vector2>();
            Vector2 pp = Vector2.zero, ella = Vector2.zero;
            bot = 1;
            yield return Cerco(() =>
            {
                if (pos.Count > 0 || !j.RegistroCerco.Any(r => r.que.StartsWith("linea:"))) return;
                pp = p.transform.position;
                ella = j.transform.position;
                pos.AddRange(Object.FindObjectsByType<IlusionCazadora>(FindObjectsSortMode.None).Where(i => i.Viva).Select(i => i.Posicion));
            });
            float d = aj.distanciaCerco, ux = aj.cercoArriba.x, uy = aj.cercoArriba.y;
            int lado = ella.x >= pp.x ? 1 : -1;
            bool lateral = pos.Any(q => Mathf.Abs(q.y) < 0.3f && Mathf.Abs(q.x - (pp.x - lado * d)) < 1f);
            int arriba = pos.Count(q => Mathf.Abs(q.y - uy) < 0.6f && Mathf.Abs(Mathf.Abs(q.x - pp.x) - ux) < 1f);
            Comprobar(pos.Count == 3 && lateral && arriba == 2 && Mathf.Abs(Mathf.Abs(ella.x - pp.x) - d) < 1f,
                      $"posiciones: una copia al lado, dos arriba en diagonal y ella al otro lado ({pos.Count} copias, lateral {lateral}, arriba {arriba}, ella a {Mathf.Abs(ella.x - pp.x):0.0})");
            Orden(out string orden1, out bool originalUltima, out bool marcaAntes);
            Comprobar(originalUltima, "la original ataca siempre la ultima");
            Comprobar(marcaAntes, "el destello de \"quien va\" sale antes de cada linea roja, y la linea se fija antes del golpe");

            // ------------------------------------------------ Orden distinto cada vez
            var ordenes = new List<string> { orden1 };
            for (int n = 0; n < 3; n++)
            {
                yield return Preparar(0f);
                yield return Cerco(null);
                Orden(out string o, out bool u, out _);
                ordenes.Add(o);
            }
            bool distintos = true;
            for (int n = 1; n < ordenes.Count; n++) if (ordenes[n] == ordenes[n - 1]) distintos = false;
            Comprobar(distintos, $"el orden de las copias cambia entre cercos ({string.Join(" / ", ordenes)})");

            // ------------------------------------------------ La linea sigue y luego se fija
            yield return Preparar(0f);
            bot = 0;
            p.InvulnerableExterno = true;
            float x0 = 0f, x1 = 0f, x2 = 0f, x3 = 0f;
            int paso = 0;
            LineaApunte primera = null;
            yield return Cerco(() =>
            {
                LineaApunte l = j.LineaCerco;
                // Solo la primera linea (la reserva reutiliza el mismo objeto para la siguiente).
                if (l == null || j.RegistroCerco.Any(r => r.que.StartsWith("ataca:"))) return;
                if (paso == 0 && !j.CercoLineaFija) { primera = l; x0 = l.Hasta.x; paso = 1; Teletransportar(p.transform.position + Vector3.right * 1.2f); }
                else if (paso == 1 && !j.CercoLineaFija && Mathf.Abs(l.Hasta.x - x0) > 0.01f) { x1 = l.Hasta.x; paso = 2; }
                else if (paso == 2 && j.CercoLineaFija) { x2 = l.Hasta.x; paso = 3; Teletransportar(p.transform.position + Vector3.left * 1.2f); }
                else if (paso == 3 && j.CercoLineaFija) x3 = l.Hasta.x;
            });
            p.InvulnerableExterno = false;
            Comprobar(paso >= 3 && Mathf.Abs(x1 - x0) > 0.5f && Mathf.Abs(x3 - x2) < 0.01f,
                      $"la linea te sigue ({x0:0.0} -> {x1:0.0}) y, fijada, ya no ({x2:0.0} -> {x3:0.0})");

            // ------------------------------------------------ Tres maneras de jugar
            var perdidas = new Dictionary<string, (float yo, float ella)>();
            foreach ((int modoBot, string nombre) in new[] { (1, "parry a todo"), (2, "solo bloqueo"), (0, "sin bloquear") })
            {
                yield return Preparar(0f);
                // Vida llena hasta el instante en que empieza el cerco.
                rellenarVida = true;
                EnemyHealth sj = j.GetComponent<EnemyHealth>();
                int vidaElla = sj.CurrentHealth, vidaYoFin = p.VidaActual, vidaEllaFin = vidaElla;
                bot = modoBot;
                // Solo lo que pasa dentro del cerco (no lo que hace despues).
                int vidaYoIni = -1;
                yield return Cerco(() => { rellenarVida = false; if (vidaYoIni < 0) vidaYoIni = p.VidaActual; vidaYoFin = p.VidaActual; vidaEllaFin = sj.CurrentHealth; });
                bot = 0;
                float yo = (float)(vidaYoIni - vidaYoFin) / p.VidaMaxima, laJefa = (float)(vidaElla - vidaEllaFin) / sj.MaxHealth;
                perdidas[nombre] = (yo, laJefa);
                Debug.Log($"[R22] {nombre}: tu vida -{yo * 100f:0} %, su barra -{laJefa * 100f:0} %  ({string.Join(", ", j.RegistroCerco.Where(r => r.que.StartsWith("golpe")).Select(r => r.que))})");
                if (modoBot == 2) Comprobar(j.RegistroCerco.Count(r => r.que.EndsWith(":Bloqueado")) == 4, "cada golpe del cerco se puede bloquear (4 de 4)");
                if (modoBot == 0) Comprobar(vidaYoFin > 0 && j.RegistroCerco.Count(r => r.que.EndsWith(":Recibido")) == 4, $"sin bloquear nada duele mucho ({yo * 100f:0} %) pero no mata con la vida llena");
                rellenarVida = true;
                SetCampo(p, "currentHealth", p.VidaMaxima);
            }
            Comprobar(perdidas["parry a todo"].yo == 0f && Mathf.Abs(perdidas["parry a todo"].ella - aj.premioCerco) < 0.02f,
                      $"parry a todo: tu no pierdes nada y ella pierde el {aj.premioCerco * 100f:0} % de su barra ({perdidas["parry a todo"].ella * 100f:0} %)");
            Comprobar(perdidas["solo bloqueo"].ella == 0f && perdidas["sin bloquear"].ella == 0f, "sin parry perfecto ella no pierde vida");

            // ------------------------------------------------ Premio: una vez, aturdida, tono y frase
            yield return Preparar(0f);
            int premios0 = j.PremiosCerco, frases0 = frases.Count;
            bot = 1;
            yield return Cerco(null);
            float tPremio = j.RegistroCerco.FirstOrDefault(r => r.que == "premio").t, tFin = 0f;
            yield return Esperar(() => j.RegistroCerco.Any(r => r.que == "aturdida_fin"), 4f);
            tFin = j.RegistroCerco.FirstOrDefault(r => r.que == "aturdida_fin").t;
            Comprobar(j.PremiosCerco == premios0 + 1 && j.RegistroCerco.Count(r => r.que == "premio") == 1 && !j.RegistroCerco.Any(r => r.que.StartsWith("golpe:O")),
                      "parry perfecto: el premio se da una sola vez y la original no llega a golpear");
            Comprobar(Mathf.Abs(tFin - tPremio - aj.aturdidaPremioCerco) < 0.15f, $"queda aturdida lo configurado ({tFin - tPremio:0.00} s de {aj.aturdidaPremioCerco} s)");
            var nuevasFrases = frases.Skip(frases0).Where(f => aj.frasesPremioCerco.Contains(f)).ToList();
            Comprobar(nuevasFrases.Count == 1, $"su frase al perder vida sale una vez ({nuevasFrases.Count})");
            Comprobar(Globales.TieneLogro("cerco_perfecto"), "con la Cazadora desbloqueada de verdad, el premio da el logro oculto");
            var pendientes = (List<FichaLogro>)typeof(Logros).GetField("avisosPendientes", Todo).GetValue(null);
            Comprobar(pendientes.Count == 1, "el aviso del logro espera a que acabe la pelea (no sale en combate)");
            Comprobar(Bitacora.Tiene(Bitacora.ClaveSecreto(caz, "cerco")), "el premio desbloquea el consejo secreto de la Bitacora");
            var nuevos = (List<string>)typeof(Bitacora).GetField("nuevos", Todo).GetValue(null);
            Comprobar(nuevos.Contains(Bitacora.ClaveSecreto(caz, "cerco")) && Bitacora.TomarResumen(caz).Contains("consejo"), "el consejo secreto sale en el resumen (no en combate)");
            // HUD: su frase va por encima de la barra del jefe.
            object ui = typeof(UICazadora).GetField("inst", Todo).GetValue(null);
            RectTransform sub = ((TMPro.TextMeshProUGUI)Campo(ui, "subtitulo")).rectTransform;
            AjustesInterfaz ai = AjustesInterfaz.Get();
            ai.Barra(((RectTransform)((Component)ui).transform).rect.width, out _, out _, out float alturaBarra);
            Comprobar(sub.anchoredPosition.y >= alturaBarra + ai.separacionSubtitulo - 1f, $"su frase no tapa la barra del jefe (y {sub.anchoredPosition.y:0}, barra {alturaBarra:0})");

            // Tono: sube con cada parry seguido.
            var tonos = new List<float>();
            yield return Preparar(0f);
            bot = 1;
            yield return Cerco(() =>
            {
                if (j.RegistroCerco.Count(r => r.que.StartsWith("golpe:") && r.que.EndsWith(":Parry")) > tonos.Count) tonos.Add(PlayerControler.UltimoTonoParry);
            });
            Comprobar(tonos.Count == 3 && tonos[0] < tonos[1] && tonos[1] < tonos[2] && Mathf.Approximately(tonos[0], 1f),
                      $"el tono del parry sube con cada copia ({string.Join(", ", tonos.Select(t => t.ToString("0.00")))})");
            var premiosDichos = frases.Where(f => aj.frasesPremioCerco.Contains(f)).ToList();
            bool sinRepetir = true;
            for (int n = 1; n < premiosDichos.Count; n++) if (premiosDichos[n] == premiosDichos[n - 1]) sinRepetir = false;
            Comprobar(premiosDichos.Count >= 2 && sinRepetir, $"la frase del premio no se repite dos veces seguidas ({premiosDichos.Count} premios)");
            Comprobar(Globales.TieneLogro("cerco_perfecto") && !Globales.DarLogro("cerco_perfecto"), "el logro se da una sola vez");

            // Una copia sin parar: sin premio, y el tono vuelve al normal.
            yield return Preparar(0f);
            tonos.Clear();
            int premios1 = j.PremiosCerco;
            sinParar = null;
            bot = 1;
            yield return Cerco(() =>
            {
                if (sinParar == null) sinParar = QuienVa();
                if (j.RegistroCerco.Count(r => r.que.StartsWith("golpe:") && r.que.EndsWith(":Parry")) > tonos.Count) tonos.Add(PlayerControler.UltimoTonoParry);
            });
            Comprobar(j.PremiosCerco == premios1 && j.RegistroCerco.Any(r => r.que.StartsWith("golpe:O")),
                      $"con una copia solo bloqueada no hay premio y la original ataca ({string.Join(", ", j.RegistroCerco.Where(r => r.que.StartsWith("golpe")).Select(r => r.que))})");
            Comprobar(tonos.Count > 0 && Mathf.Approximately(tonos[0], 1f), $"tras fallar una copia, el tono vuelve al normal ({string.Join(", ", tonos.Select(t => t.ToString("0.00")))})");
            sinParar = null;
            // El siguiente cerco empieza de cero (no se arrastra el fallo ni el tono).
            yield return Preparar(0f);
            bot = 1;
            int premios2 = j.PremiosCerco;
            tonos.Clear();
            yield return Cerco(() =>
            {
                if (j.RegistroCerco.Count(r => r.que.StartsWith("golpe:") && r.que.EndsWith(":Parry")) > tonos.Count) tonos.Add(PlayerControler.UltimoTonoParry);
            });
            Comprobar(j.PremiosCerco == premios2 + 1 && tonos.Count > 0 && Mathf.Approximately(tonos[0], 1f), "el cerco siguiente empieza de cero: premio y tono normal");

            // Con poca vida: no la mata ni salta de barra. Con escudo: no le quita vida.
            yield return Preparar(0.04f);
            bot = 1;
            int faseAntes = j.Fase;
            yield return Cerco(null);
            yield return new WaitForSeconds(0.3f);
            Comprobar(j.Fase == faseAntes && j.GetComponent<EnemyHealth>().CurrentHealth >= 1 && !j.Transicionando, "con poca vida el premio no la mata ni salta de barra");
            yield return Preparar(0f);
            bot = 1;
            EnemyHealth sj2 = j.GetComponent<EnemyHealth>();
            int v0 = sj2.CurrentHealth;
            yield return Cerco(() => SetCampo(j, "escudoActivo", true));
            SetCampo(j, "escudoActivo", false);
            Comprobar(sj2.CurrentHealth == v0, "con el escudo puesto el premio no le quita vida (el escudo no se rompe)");
            bot = 0;

            // ------------------------------------------------ Cerca de la pared
            yield return Preparar(0f);
            bot = 1;
            Teletransportar(new Vector3(j.Cuerpo.XMax - 0.8f, 0.8f, 0f));
            yield return new WaitForSeconds(0.3f);
            var posPared = new List<Vector2>();
            Vector2 ppPared = Vector2.zero, ellaPared = Vector2.zero;
            yield return Cerco(() =>
            {
                if (posPared.Count > 0 || !j.RegistroCerco.Any(r => r.que.StartsWith("linea:"))) return;
                ppPared = p.transform.position;
                ellaPared = j.transform.position;
                posPared.AddRange(Object.FindObjectsByType<IlusionCazadora>(FindObjectsSortMode.None).Where(i => i.Viva).Select(i => i.Posicion));
            });
            bool dentro = posPared.All(q => q.x >= j.Cuerpo.XMin - 0.01f && q.x <= j.Cuerpo.XMax + 0.01f && q.y >= -0.01f);
            Comprobar(posPared.Count == 3 && dentro && ellaPared.x < ppPared.x, $"junto a la pared derecha: todo dentro de la arena y ella se pone al otro lado ({posPared.Count} copias)");

            // ------------------------------------------------ Barra 2: una copia mas
            j.SaltarAFase(1);
            yield return new WaitForSeconds(1.5f);
            yield return Preparar(0f);
            bot = 1;
            yield return Cerco(null);
            int copias2 = j.RegistroCerco.Count(r => r.que.StartsWith("destello:") && r.que != "destello:O");
            Comprobar(copias2 == 4 && j.CopiasUltimoCerco == 4, $"desde la barra 2 el cerco tiene una copia mas ({copias2})");
            Comprobar(j.RegistroCerco.Any(r => r.que == "premio"), "con las 4 copias paradas tambien hay premio");
            bot = 0;
            j.SaltarAFase(0);
            yield return new WaitForSeconds(1.5f);

            // ------------------------------------------------ Ejecucion doble
            yield return Preparar(0f);
            p.InvulnerableExterno = true;
            float tAviso = 0f;
            int lineas = 0;
            j.ForzarAtaque("ejecucion");
            float t0 = Time.time;
            bool habiaLinea = false;
            while (Time.time - t0 < 9f)
            {
                bool hay = Object.FindObjectsByType<MarcaSuelo>(FindObjectsSortMode.None).Any(m => m.Activa);
                if (hay && !habiaLinea) { lineas++; if (lineas == 1) tAviso = Time.time; }
                habiaLinea = hay;
                yield return null;
            }
            p.InvulnerableExterno = false;
            Comprobar(lineas == 2, $"la Ejecucion cruza dos veces, cada una con su linea ({lineas})");

            // ------------------------------------------------ Reiniciar desafio 10 veces
            for (int n = 0; n < 10; n++)
            {
                Desafio.Reiniciar();
                yield return new WaitForSeconds(0.5f);
                yield return EsperarEscena(caz.escena);
                yield return new WaitForSeconds(1.5f);
                p = Object.FindFirstObjectByType<PlayerControler>();
                z = Object.FindFirstObjectByType<ZonaJefe>();
                Teletransportar(new Vector3(z.transform.position.x + z.distanciaActivador + 1f, 0.8f, 0f));
                yield return Esperar(() => (j = JefeCazadora.Actual) != null && j.Activa, 12f);
            }
            Comprobar(j != null && j.Activa, "tras reiniciar el desafio 10 veces seguidas la pelea empieza bien");
            if (j != null)
            {
                j.AlFrase += f => frases.Add(f);
                yield return Preparar(0f);
                bot = 1;
                yield return Cerco(null);
                bot = 0;
                Comprobar(j.RegistroCerco.Any(r => r.que == "premio"), "tras 10 reinicios el cerco y su premio funcionan");
            }
            var normal = Tiempos();

            // ------------------------------------------------ Dificil: avisos y pausas mas cortos
            // (Dificil se abre tras completar el Normal.)
            Globales.Completar(caz.id, false, 999f, 1);
            Desafio.Empezar(caz, true);
            yield return new WaitForSeconds(0.5f);
            yield return EsperarEscena(caz.escena);
            yield return new WaitForSeconds(2f);
            p = Object.FindFirstObjectByType<PlayerControler>();
            z = Object.FindFirstObjectByType<ZonaJefe>();
            Teletransportar(new Vector3(z.transform.position.x + z.distanciaActivador + 1f, 0.8f, 0f));
            yield return Esperar(() => (j = JefeCazadora.Actual) != null && j.Activa, 12f);
            if (j != null)
            {
                yield return Preparar(0f);
                bot = 2;
                yield return Cerco(null);
                bot = 0;
                Comprobar(Desafio.Dificil, "el desafio va en Dificil");
                var dificil = Tiempos();
                Debug.Log($"[R22] tiempos: Normal linea {normal.apuntar:0.00} s, fija {normal.fijo:0.00} s, pausa {normal.pausa:0.00} s / Dificil linea {dificil.apuntar:0.00} s, fija {dificil.fijo:0.00} s, pausa {dificil.pausa:0.00} s");
                Comprobar(dificil.apuntar < normal.apuntar - 0.03f && dificil.fijo <= normal.fijo && dificil.fijo >= aj.avisoMinimoCerco - 0.03f,
                          "en Dificil la linea avisa menos, sin bajar del minimo");
                Comprobar(dificil.pausa >= aj.pausaMinimaCerco - 0.05f, "en Dificil la pausa entre golpes no baja del minimo");
            }
        }

        // Tiempos del ultimo cerco: linea (te sigue), fija y pausa entre golpes.
        private (float apuntar, float fijo, float pausa) Tiempos()
        {
            var r = j.RegistroCerco;
            float apuntar = 0f, fijo = 0f, pausa = 99f;
            for (int i = 0; i < r.Count; i++)
            {
                if (apuntar == 0f && r[i].que.StartsWith("linea:")) { var f = r.Skip(i).FirstOrDefault(x => x.que.StartsWith("fija:")); if (f.que != null) apuntar = f.t - r[i].t; }
                if (fijo == 0f && r[i].que.StartsWith("fija:")) { var g = r.Skip(i).FirstOrDefault(x => x.que.StartsWith("ataca:")); if (g.que != null) fijo = g.t - r[i].t; }
                if (r[i].que.StartsWith("golpe:") && !r[i].que.StartsWith("golpe:O"))
                {
                    var s = r.Skip(i + 1).FirstOrDefault(x => x.que.StartsWith("destello:"));
                    if (s.que != null) pausa = Mathf.Min(pausa, s.t - r[i].t);
                }
            }
            return (apuntar, fijo, pausa);
        }

        // Orden del ultimo cerco y si la secuencia es la buena.
        private void Orden(out string orden, out bool originalUltima, out bool marcaAntes)
        {
            var r = j.RegistroCerco.Select(x => x.que).ToList();
            var quien = r.Where(x => x.StartsWith("destello:")).Select(x => x.Substring(9)).ToList();
            orden = string.Join("", quien);
            originalUltima = quien.Count > 0 && (quien.Last() == "O" || r.Contains("premio")) && quien.Count(q => q == "O") <= 1
                             && (quien.IndexOf("O") < 0 || quien.IndexOf("O") == quien.Count - 1);
            marcaAntes = true;
            foreach (string q in quien)
            {
                int a = r.IndexOf("destello:" + q), b = r.IndexOf("linea:" + q), c = r.IndexOf("fija:" + q), g = r.FindIndex(x => x.StartsWith("golpe:" + q + ":"));
                if (!(a >= 0 && b > a && c > b && (g < 0 || g > c))) marcaAntes = false;
            }
        }

        // Un cerco entero (forzado), llamando a "cada" en cada fotograma.
        private IEnumerator Cerco(System.Action cada)
        {
            j.ForzarAtaque("cerco");
            yield return Esperar(() => j.EnCerco, 8f);
            if (!j.EnCerco) { Fallo("el cerco no empezo"); yield break; }
            float t0 = Time.realtimeSinceStartup;
            while (j.EnCerco && Time.realtimeSinceStartup - t0 < 20f)
            {
                cada?.Invoke();
                yield return null;
            }
            yield return new WaitForSeconds(0.2f);
        }

        // Ella quieta y con la vida que toque; sin instakills, escudos ni guardias.
        private IEnumerator Preparar(float vida)
        {
            bot = 0;
            sinParar = null;
            yield return Esperar(() => !j.EnCerco, 20f);
            yield return EsperarFinAtaque(8f);
            p = Object.FindFirstObjectByType<PlayerControler>();
            p.InvulnerableExterno = false;
            SetCampo(p, "isInvincible", false);
            SetCampo(p, "isBlocking", false);
            GatherInput gi = p.GetComponent<GatherInput>();
            if (gi != null) SetCampo(gi, "_isKickHeld", false);
            EstadosPlayer ep = p.GetComponent<EstadosPlayer>();
            if (ep != null) Llamar(ep, "Limpiar");
            for (int f = 0; f < 3; f++) { ((bool[])Campo(j, "escudoUsado"))[f] = true; ((bool[])Campo(j, "instakillUsado"))[f] = true; }
            ((Queue<float>)Campo(j, "golpesRecibidos")).Clear();
            j.PonerVida(vida > 0f ? vida : 1f);
            float xj = j.transform.position.x;
            float cx = (j.Cuerpo.XMin + j.Cuerpo.XMax) * 0.5f;
            Teletransportar(new Vector3(cx, 0.8f, 0f));
            SetCampo(p, "currentHealth", p.VidaMaxima);
            yield return new WaitForSeconds(0.5f);
        }

        private IEnumerator EsperarFinAtaque(float tope)
        {
            float t0 = Time.realtimeSinceStartup;
            yield return new WaitForSeconds(0.3f);
            while (Time.realtimeSinceStartup - t0 < tope && (!string.IsNullOrEmpty((string)Campo(j, "ataqueForzado")) || (bool)Campo(j, "enGolpe") || (bool)Campo(j, "enAviso") || j.EnCerco))
                yield return null;
            yield return new WaitForSeconds(1f);
        }

        private static IEnumerator Esperar(System.Func<bool> cond, float segundos)
        {
            float t0 = Time.realtimeSinceStartup;
            while (!cond() && Time.realtimeSinceStartup - t0 < segundos) yield return null;
        }

        private IEnumerator EsperarEscena(string nombre)
        {
            float t0 = Time.realtimeSinceStartup;
            while ((SceneManager.GetActiveScene().name != nombre || PantallaCarga.Cargando) && Time.realtimeSinceStartup - t0 < 40f) yield return null;
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
    }
}
