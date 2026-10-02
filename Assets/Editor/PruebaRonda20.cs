using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Pruebas de la Ronda 20 (linea de comandos, sin -quit):
//   ... -executeMethod PruebaRonda20.Cazadora
// The Blind Huntress: tamano (x1.25) y parrys justos (golpes desde arriba mires a
// donde mires, ilusiones que se deshacen al parar a la verdadera, frenazo al
// pararla y gracia al empezar su guardia). Escribe "[R20] OK/FALLO ..." y el total.
[InitializeOnLoad]
public static class PruebaRonda20
{
    private const string Clave = "PruebaRonda20.Modo";

    static PruebaRonda20()
    {
        EditorApplication.playModeStateChanged += e =>
        {
            string modo = SessionState.GetString(Clave, "");
            if (modo == "") return;
            if (e == PlayModeStateChange.EnteredPlayMode) new GameObject("PruebaRonda20").AddComponent<Conductor>().modo = modo;
            else if (e == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetString(Clave, "");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
        };
    }

    public static void Escudo()
    {
        SessionState.SetString(Clave, "escudo");
        EditorSceneManager.OpenScene("Assets/Scenes/Menu Principal.unity");
        ArranquePruebas.Proteger();
        EditorApplication.EnterPlaymode();
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
        private string carpeta;
        private int ok, fallos, errores;
        private PlayerControler p;
        private JefeCazadora j;
        private const BindingFlags Todo = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static;

        // El bot del parry: guardia siempre recien subida (dentro de la ventana).
        private bool bloquear;
        private int mirarA;            // 0 = no tocar; 1/-1 = mirar a ese lado; 2 = mirar a la Cazadora; 3 = mirarla hasta que golpea
        private int ladoFijo = 1;
        private int parrys, golpes, golpesDeElla;
        private float ultimaContra, vidaAntes;

        private void Ok(string s) { ok++; Debug.Log("[R20] OK " + s); }
        private void Fallo(string s) { fallos++; Debug.LogWarning("[R20] FALLO " + s); }
        private void Comprobar(bool c, string s) { if (c) Ok(s); else Fallo(s); }

        private IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject);
            Application.logMessageReceived += (c, st, t) => { if ((t == LogType.Exception || t == LogType.Error) && !c.StartsWith("[R20]")) { errores++; Debug.Log("[R20] error visto: " + c); } };
            carpeta = Path.Combine(Directory.GetCurrentDirectory(), "PruebaNieve");
            Partida.CarpetaPruebas = Path.Combine(carpeta, "PartidasR20");
            if (Directory.Exists(Partida.CarpetaPruebas)) Directory.Delete(Partida.CarpetaPruebas, true);
            Directory.CreateDirectory(Partida.CarpetaPruebas);
            Globales.Recargar();
            IEnumerator r = modo == "escudo" ? PruebaEscudo() : PruebaCazadora();
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
            Debug.Log($"[R20] RESULTADO {modo}: {ok} OK, {fallos} FALLOS");
            Time.timeScale = 1f;
            Partida.Descargar();
            Globales.GuardarPendiente();
            EditorApplication.ExitPlaymode();
        }

        // Cada fotograma: el bot del parry, la vida y los parrys contados.
        private void Update()
        {
            if (p == null) return;
            if (j != null && mirarA != 0)
            {
                // 3 = mirarla mientras se prepara y quedarse mirando a ese lado cuando arranca
                // el golpe (como haria alguien que la ve venir aunque luego te atraviese).
                if (mirarA == 3 && !(bool)Campo(j, "enGolpe")) ladoFijo = j.transform.position.x >= p.transform.position.x ? 1 : -1;
                int dir = mirarA == 2 ? (j.transform.position.x >= p.transform.position.x ? 1 : -1) : mirarA == 3 ? ladoFijo : mirarA;
                Llamar(p, "SetFacing", dir);
            }
            if (bloquear)
            {
                SetCampo(p, "isBlocking", true);
                SetCampo(p, "tBloqueo", 0f);
                SetCampo(p, "ventanaParryActual", (float)Campo(p, "parryWindow"));
            }
            float contra = (float)Campo(p, "ventanaContra");
            if (contra > ultimaContra + 0.5f) { parrys++; Debug.Log($"[R20] (parry t={Time.time:0.00}, ella en golpe {Campo(j, "enGolpe")}, ilusiones vivas {Object.FindObjectsByType<IlusionCazadora>(FindObjectsSortMode.None).Count(i => i.Viva)})"); }
            ultimaContra = contra;
            int vida = (int)Campo(p, "currentHealth");
            if (vida < vidaAntes)
            {
                golpes++;
                if (j != null && (bool)Campo(j, "enGolpe")) golpesDeElla++;
                object tipo = typeof(CausaMuerte).GetField("tipo", Todo).GetValue(null);
                Debug.Log($"[R20] (golpe t={Time.time:0.00} de {vidaAntes - vida}: causa {tipo}, elemento de ella {Campo(j, "elementoAtaque")}, en golpe {Campo(j, "enGolpe")}, llamas {Object.FindObjectsByType<LlamaSuelo>(FindObjectsSortMode.None).Count(l => l.isActiveAndEnabled)})");
            }
            if (vida < p.VidaMaxima) SetCampo(p, "currentHealth", p.VidaMaxima);
            vidaAntes = p.VidaMaxima;
        }

        private IEnumerator PruebaCazadora()
        {
            yield return new WaitForSeconds(1f);
            FichaJefe caz = FichaJefe.DeId("blind_huntress");
            Globales.PonerMarca(CodigosSecretos.Marca(caz.id), true);
            Globales.PonerMarca("revelado_" + caz.id, true);
            Globales.PonerMarca("dialogo_cazadora", true);
            Desafio.Empezar(caz, false);
            yield return EsperarEscena(caz.escena);
            yield return new WaitForSeconds(2.5f);
            p = Object.FindFirstObjectByType<PlayerControler>();
            vidaAntes = p.VidaMaxima;
            ZonaJefe z = Object.FindFirstObjectByType<ZonaJefe>();
            Teletransportar(new Vector3(z.transform.position.x + z.distanciaActivador + 4f, 0.8f, 0f));
            yield return Esperar(() => (j = JefeCazadora.Actual) != null && j.Activa, 15f);
            Comprobar(j != null && j.Activa, "empieza la pelea con la Cazadora");
            if (j == null) yield break;

            // ------------------------------------------------ Tamano
            SpritesCazadora hojas = j.GetComponentInChildren<AnimCazadora>().hojas;
            CapsuleCollider2D cuerpo = j.GetComponent<CapsuleCollider2D>();
            SpriteRenderer sr = j.GetComponentInChildren<AnimCazadora>().cuerpo;
            yield return Esperar(() => sr.sprite != null, 2f);
            float altoDibujo = sr.bounds.size.y, altoPlayer = p.GetComponent<Collider2D>().bounds.size.y;
            Debug.Log($"[R20] tamano: escala x{hojas.Escala:0.00}, cuerpo golpeable {cuerpo.size.x:0.00}x{cuerpo.size.y:0.00}, alto del cuerpo {cuerpo.size.y / altoPlayer:0.00} veces el del player");
            Comprobar(Mathf.Abs(hojas.Escala - Ronda20.Escala) < 0.01f && Mathf.Abs(cuerpo.size.y - 1.35f * Ronda20.Escala) < 0.02f,
                      $"la Cazadora es un 25 % mas grande (escala {hojas.Escala:0.00}, cuerpo {cuerpo.size.y:0.00} de alto)");
            yield return CapturaCerca("r20_tamano");

            // ------------------------------------------------ Golpes desde arriba (mirando AL REVES)
            foreach (string ataque in new[] { "salto", "caen", "lluvia" })
            {
                yield return Preparar();
                int espalda = j.transform.position.x >= p.transform.position.x ? -1 : 1;
                mirarA = espalda;
                bloquear = true;
                int p0 = parrys, g0 = golpes;
                j.ForzarAtaque(ataque);
                yield return Esperar(() => string.IsNullOrEmpty((string)Campo(j, "ataqueForzado")), 8f);
                foreach (LlamaSuelo l in Object.FindObjectsByType<LlamaSuelo>(FindObjectsSortMode.None)) l.gameObject.SetActive(false);
                p0 = parrys; g0 = golpes;
                espalda = j.transform.position.x >= p.transform.position.x ? -1 : 1;
                mirarA = espalda;
                // Hasta 0,3 s despues del parry (lo de despues ya es otro ataque, y ella esta delante).
                yield return Esperar(() => parrys > p0, 7f);
                yield return new WaitForSeconds(0.3f);
                int recibidos = golpes - g0;
                bloquear = false; mirarA = 0;
                // La lluvia suelta sus olas tras caer (eso se queda asi): solo cuenta el parry.
                bool sinDano = ataque == "lluvia" || recibidos == 0;
                Comprobar(parrys > p0 && sinDano, $"\"{ataque}\" desde arriba se para mirando hacia el otro lado ({parrys - p0} parrys, {recibidos} golpes recibidos)");
                yield return EsperarFinAtaque(6f);
            }

            // Trampas de sonido: el tajo cae desde arriba; antes solo se paraba mirando a la izquierda.
            yield return Preparar();
            j.ForzarAtaque("trampas");
            yield return Esperar(() => Object.FindObjectsByType<TrampaSonido>(FindObjectsSortMode.None).Any(t => t.isActiveAndEnabled), 5f);
            TrampaSonido trampa = Object.FindObjectsByType<TrampaSonido>(FindObjectsSortMode.None).FirstOrDefault(t => t.isActiveAndEnabled);
            if (trampa != null)
            {
                mirarA = 1;
                bloquear = true;
                int p0 = parrys, g0 = golpes - golpesDeElla;
                Teletransportar(trampa.transform.position + Vector3.up * 0.6f);
                yield return Esperar(() => parrys > p0, 2.5f);
                yield return new WaitForSeconds(0.3f);
                bloquear = false; mirarA = 0;
                Comprobar(parrys > p0 && golpes - golpesDeElla == g0, $"trampa de sonido: su tajo se para mirando a la derecha ({parrys - p0} parrys, {golpes - golpesDeElla - g0} golpes de la trampa)");
            }
            else Fallo("no aparecieron trampas de sonido");

            // ------------------------------------------------ Ilusiones: parar a la verdadera las deshace
            j.SaltarAFase(1);
            yield return new WaitForSeconds(4f);
            foreach (string ataque in new[] { "flanqueo", "cruceDoble", "cruce" })
            {
                int bien = 0, contactos = 0;
                for (int intento = 0; intento < 9 && contactos < 5; intento++)
                {
                    yield return Preparar();
                    mirarA = 3;
                    bloquear = true;
                    int p0 = parrys, g0 = golpes;
                    j.ForzarAtaque(ataque);
                    // Se mide desde que lo empieza de verdad (forzar espera a que acabe lo que hacia).
                    yield return Esperar(() => string.IsNullOrEmpty((string)Campo(j, "ataqueForzado")), 8f);
                    foreach (LlamaSuelo l in Object.FindObjectsByType<LlamaSuelo>(FindObjectsSortMode.None)) l.gameObject.SetActive(false);
                    p0 = parrys; g0 = golpes;
                    Debug.Log($"[R20] (empieza {ataque} t={Time.time:0.00}, player x={p.transform.position.x:0.0}, ella x={j.transform.position.x:0.0})");
                    // Hasta 0,3 s despues de parar a la verdadera (lo de despues ya es otro ataque).
                    yield return Esperar(() => parrys > p0, 7f);
                    yield return new WaitForSeconds(0.3f);
                    int recibidos = golpes - g0;
                    bloquear = false; mirarA = 0;
                    // Si fallo sola (es ciega: a veces ataca a donde oyo ruido), ese intento no cuenta.
                    if (parrys > p0 || recibidos > 0) contactos++;
                    if (parrys > p0 && recibidos == 0) bien++;
                    Debug.Log($"[R20] {ataque} intento {intento + 1}: {parrys - p0} parrys, {recibidos} golpes");
                    yield return EsperarFinAtaque(6f);
                }
                Comprobar(contactos == 5 && bien == 5, $"\"{ataque}\": parandola de cara el parry entra (aunque te atraviese) y nada te golpea por la espalda ({bien}/{contactos})");
            }

            // ------------------------------------------------ Tajo de fuego parado: sin llamas
            {
                yield return Preparar();
                mirarA = 3;
                bloquear = true;
                int p0 = parrys;
                j.ForzarAtaque("tresLunas");
                yield return Esperar(() => string.IsNullOrEmpty((string)Campo(j, "ataqueForzado")), 8f);
                foreach (LlamaSuelo l in Object.FindObjectsByType<LlamaSuelo>(FindObjectsSortMode.None)) l.gameObject.SetActive(false);
                SetCampo(j, "elementoAtaque", Elemento.Fuego);
                p0 = parrys;
                yield return Esperar(() => parrys > p0, 6f);
                yield return new WaitForSeconds(0.5f);
                int llamas = Object.FindObjectsByType<LlamaSuelo>(FindObjectsSortMode.None).Count(l => l.isActiveAndEnabled && Mathf.Abs(l.transform.position.x - p.transform.position.x) < 2f);
                bloquear = false; mirarA = 0;
                Comprobar(parrys > p0 && llamas == 0, $"tajo de fuego parado: no deja llamas a tus pies ({llamas} llamas)");
                yield return EsperarFinAtaque(6f);
            }

            // ------------------------------------------------ Frenazo: no sigue deslizandose tras el parry
            j.SaltarAFase(0);
            yield return new WaitForSeconds(4f);
            yield return Preparar();
            Teletransportar(new Vector3(j.transform.position.x + 3.5f * (p.transform.position.x >= j.transform.position.x ? 1 : -1), 0.8f, 0f));
            mirarA = 2;
            bloquear = true;
            int pp = parrys;
            j.ForzarAtaque("tresLunas");
            yield return Esperar(() => parrys > pp, 6f);
            float x0 = j.transform.position.x;
            yield return new WaitForSeconds(0.35f);
            float desliz = Mathf.Abs(j.transform.position.x - x0);
            bloquear = false; mirarA = 0;
            Comprobar(parrys > pp && desliz < 0.15f, $"tras parar su golpe se frena en seco (se desliza {desliz:0.00})");
            yield return EsperarFinAtaque(4f);

            // ------------------------------------------------ Guardia: gracia al empezar
            yield return Preparar();
            EnemyHealth salud = j.GetComponent<EnemyHealth>();
            // Mientras llega la guardia, el player se protege (que nada lo deje aturdido antes).
            p.InvulnerableExterno = true;
            j.ForzarAtaque("guardia");
            yield return Esperar(() => (bool)Campo(j, "enGuardia"), 6f);
            Comprobar((bool)Campo(j, "enGuardia"), "se pone en guardia");
            p.InvulnerableExterno = false;
            yield return new WaitForSeconds(0.1f);
            int r0 = j.RebotesGuardia;
            salud.TakeDamage(10, (Vector2)p.transform.position, 0f);
            yield return null;
            Comprobar(j.RebotesGuardia == r0 + 1 && !j.ParoTuGolpe, "golpe al empezar la guardia (0,1 s): rebota sin castigo");
            yield return new WaitForSeconds(0.35f);
            salud.TakeDamage(10, (Vector2)p.transform.position, 0f);
            yield return null;
            Comprobar(j.ParoTuGolpe && p.Aturdido, "si sigues pegando despues (0,45 s): te para y te castiga, como antes");
            yield return EsperarFinAtaque(6f);

            Desafio.Salir();
            yield return EsperarEscena("Menu Principal");
            yield return new WaitForSeconds(1f);
        }

        // ------------------------------------------------ Escudo y olas devueltas
        private IEnumerator PruebaEscudo()
        {
            yield return new WaitForSeconds(1f);
            FichaJefe caz = FichaJefe.DeId("blind_huntress");
            Globales.PonerMarca(CodigosSecretos.Marca(caz.id), true);
            Globales.PonerMarca("revelado_" + caz.id, true);
            Globales.PonerMarca("dialogo_cazadora", true);
            Desafio.Empezar(caz, false);
            yield return EsperarEscena(caz.escena);
            yield return new WaitForSeconds(2.5f);
            p = Object.FindFirstObjectByType<PlayerControler>();
            vidaAntes = p.VidaMaxima;
            ZonaJefe z = Object.FindFirstObjectByType<ZonaJefe>();
            Teletransportar(new Vector3(z.transform.position.x + z.distanciaActivador + 4f, 0.8f, 0f));
            yield return Esperar(() => (j = JefeCazadora.Actual) != null && j.Activa, 15f);
            Comprobar(j != null && j.Activa, "empieza la pelea con la Cazadora");
            if (j == null) yield break;
            EnemyHealth salud = j.GetComponent<EnemyHealth>();

            // 1. Una ola normal, devuelta con un parry: le hace dano a ella.
            int bien = 0, intentos = 0;
            while (bien == 0 && intentos < 4)
            {
                intentos++;
                yield return Preparar();
                Teletransportar(new Vector3(Mathf.Clamp(j.transform.position.x + (j.transform.position.x > 22f ? -6f : 6f), 4f, 40f), 0.8f, 0f));
                mirarA = 3;
                bloquear = true;
                j.ForzarAtaque("ola");
                yield return Esperar(() => string.IsNullOrEmpty((string)Campo(j, "ataqueForzado")), 8f);
                foreach (LlamaSuelo l in Object.FindObjectsByType<LlamaSuelo>(FindObjectsSortMode.None)) l.gameObject.SetActive(false);
                int p0 = parrys, g0 = golpes, o0 = j.OlasDevueltas, v0 = salud.CurrentHealth;
                yield return Esperar(() => parrys > p0, 5f);
                yield return Esperar(() => j.OlasDevueltas > o0, 3f);
                yield return null;
                int quito = v0 - salud.CurrentHealth;
                int esperado = Mathf.RoundToInt(salud.MaxHealth * j.Ajustes.danoOlaDevuelta);
                bloquear = false; mirarA = 0;
                Debug.Log($"[R20] ola devuelta (intento {intentos}): parrys {parrys - p0}, golpes {golpes - g0}, le llego {j.OlasDevueltas - o0}, le quito {quito} (esperado ~{esperado})");
                if (parrys > p0 && j.OlasDevueltas > o0 && quito > 0) bien++;
                yield return EsperarFinAtaque(5f);
            }
            Comprobar(bien == 1, "una ola parada se devuelve y le hace dano a ella");
            yield return CapturaCerca("r20_ola_devuelta");

            // 2a. Escudo sin tocarlo: la cura empieza muy lenta, acelera y suma el 20 %.
            yield return Preparar();
            salud.DanoEstado(Mathf.RoundToInt(salud.MaxHealth * 0.5f));   // a media vida, para ver la cura
            yield return new WaitForSeconds(0.3f);
            p.InvulnerableExterno = true;
            j.ForzarAtaque("escudo");
            yield return Esperar(() => (bool)Campo(j, "escudoActivo"), 8f);
            Comprobar((bool)Campo(j, "escudoActivo"), "sube el escudo");
            int vInicio = salud.CurrentHealth;
            int curaTotal = Mathf.RoundToInt(salud.MaxHealth * j.Ajustes.EscudoN(0).cura);
            yield return new WaitForSeconds(1f);
            int v1 = salud.CurrentHealth - vInicio;
            yield return new WaitForSeconds(2.5f);
            int v35 = salud.CurrentHealth - vInicio;
            yield return Esperar(() => !(bool)Campo(j, "escudoActivo"), 9f);
            int vTotal = salud.CurrentHealth - vInicio;
            p.InvulnerableExterno = false;
            Debug.Log($"[R20] cura del escudo: {v1} al 1er segundo, {v35} a los 3,5 s, {vTotal} al final (total {curaTotal}, {100f * curaTotal / salud.MaxHealth:0} % de la barra)");
            Comprobar(Mathf.Abs(j.Ajustes.EscudoN(0).cura - 0.2f) < 0.001f, "cura del escudo: 20 % de su barra");
            Comprobar(v1 <= curaTotal * 0.05f && v35 > v1 && v35 <= curaTotal * 0.35f, $"empieza casi sin curarse y va acelerando ({v1} al 1 s, {v35} a los 3,5 s)");
            Comprobar(Mathf.Abs(vTotal - curaTotal) <= curaTotal * 0.1f, $"si no se rompe, al final ha curado el 20 % ({vTotal} de {curaTotal})");
            yield return EsperarFinAtaque(6f);

            // 2b. Con parrys: cada ola parada le quita 1 al pararla y 1 mas al volver.
            yield return Preparar();
            mirarA = 2;
            bloquear = true;
            j.ForzarAtaque("escudo");
            yield return Esperar(() => (bool)Campo(j, "escudoActivo"), 8f);
            int oInicio = j.OlasDevueltas, pInicio = j.OlasParadasEscudo;
            yield return Esperar(() => j.OlasDevueltas > oInicio, 6f);
            yield return null;
            float golpesEsc = (float)Campo(j, "golpesEscudo");
            int paradas = j.OlasParadasEscudo - pInicio, devueltas = j.OlasDevueltas - oInicio;
            yield return Esperar(() => !(bool)Campo(j, "escudoActivo"), 9f);
            bloquear = false; mirarA = 0;
            Debug.Log($"[R20] escudo con parrys: {paradas} olas paradas, {devueltas} devueltas que llegaron, golpes al escudo {golpesEsc}; roto={(bool)Campo(j, "escudoRoto")}");
            Comprobar(paradas >= 1 && devueltas >= 1 && Mathf.Abs(golpesEsc - (paradas + devueltas)) < 0.01f,
                      $"cada ola: -1 al pararla y -1 al volver ({paradas} + {devueltas} = {golpesEsc} golpes al escudo)");
            yield return EsperarFinAtaque(6f);

            // 3. Romperlo a medias: deja de curarse pero conserva lo curado.
            yield return Preparar();
            salud.DanoEstado(Mathf.Max(0, salud.CurrentHealth - salud.MaxHealth / 2));
            yield return new WaitForSeconds(0.3f);
            p.InvulnerableExterno = true;
            j.ForzarAtaque("escudo");
            yield return Esperar(() => (bool)Campo(j, "escudoActivo"), 8f);
            int vAntes = salud.CurrentHealth;
            yield return new WaitForSeconds(2f);
            for (int i = 0; i < 12 && (bool)Campo(j, "escudoActivo"); i++) { salud.TakeDamage(10, (Vector2)p.transform.position, 0f); yield return null; yield return null; }
            int vRoto = salud.CurrentHealth;
            Comprobar((bool)Campo(j, "escudoRoto"), "el escudo se rompe a golpes");
            yield return new WaitForSeconds(4f);
            int vDespues = salud.CurrentHealth;
            p.InvulnerableExterno = false;
            Debug.Log($"[R20] escudo roto: vida {vAntes} -> {vRoto} al romperlo -> {vDespues} despues");
            Comprobar(vRoto > vAntes - 60 && vDespues <= vRoto, $"roto a medias: conserva lo que se curo y ya no se cura mas ({vAntes} -> {vRoto} -> {vDespues})");
            yield return EsperarFinAtaque(6f);

            // 4. Fase 2: parar una ilusion que cae durante el escudo le quita un golpe.
            j.SaltarAFase(1);
            yield return new WaitForSeconds(4f);
            yield return Preparar();
            mirarA = 2;
            bloquear = true;
            int iInicio = j.IlusionesParadasEscudo;
            j.ForzarAtaque("escudo");
            yield return Esperar(() => (bool)Campo(j, "escudoActivo"), 8f);
            yield return Esperar(() => !(bool)Campo(j, "escudoActivo"), 9f);
            bloquear = false; mirarA = 0;
            Comprobar(j.IlusionesParadasEscudo > iInicio, $"parar una ilusion que cae durante el escudo le quita un golpe ({j.IlusionesParadasEscudo - iInicio} paradas)");
            yield return EsperarFinAtaque(6f);

            Desafio.Salir();
            yield return EsperarEscena("Menu Principal");
            yield return new WaitForSeconds(1f);
        }

        // Entre pruebas: ella quieta delante, lejos de las paredes, y el player curado.
        private IEnumerator Preparar()
        {
            bloquear = false;
            mirarA = 0;
            yield return EsperarFinAtaque(6f);
            // Fuera las llamas que dejaron los tajos de fuego de antes (no son del parry que se mide).
            foreach (LlamaSuelo l in Object.FindObjectsByType<LlamaSuelo>(FindObjectsSortMode.None)) l.gameObject.SetActive(false);
            p = Object.FindFirstObjectByType<PlayerControler>();
            EstadosPlayer ep = p.GetComponent<EstadosPlayer>();
            if (ep != null) Llamar(ep, "Limpiar");
            p.InvulnerableExterno = false;
            SetCampo(p, "isInvincible", false);
            float xj = j.transform.position.x;
            float x = Mathf.Clamp(xj + (xj > 22f ? -3f : 3f), 4f, 40f);
            Teletransportar(new Vector3(x, 0.8f, 0f));
            vidaAntes = p.VidaMaxima;
            yield return new WaitForSeconds(0.4f);
        }

        // Hasta que acaba el ataque forzado (y su recuperacion).
        private IEnumerator EsperarFinAtaque(float tope)
        {
            float t0 = Time.realtimeSinceStartup;
            yield return new WaitForSeconds(0.3f);
            while (Time.realtimeSinceStartup - t0 < tope && (!string.IsNullOrEmpty((string)Campo(j, "ataqueForzado")) || (bool)Campo(j, "enGolpe") || (bool)Campo(j, "enAviso")))
                yield return null;
            yield return new WaitForSeconds(1.2f);
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

        // Primer plano de los dos (camara aparte).
        private IEnumerator CapturaCerca(string nombre)
        {
            Camera principal = Camera.main;
            if (principal == null || j == null) yield break;
            // La camara del juego (la de la pelea), quieta un momento sobre los dos.
            Camera cam = principal;
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

        }
    }
}
