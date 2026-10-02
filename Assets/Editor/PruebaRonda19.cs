using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Pruebas de la Ronda 19: la nieve renovada (linea de comandos, sin -quit):
//   ... -executeMethod PruebaRonda19.Recorrido   saltos clave con los controles de verdad
//   ... -executeMethod PruebaRonda19.Mecanicas   sellos, quebradizo, refugios, emboscadas, atajo, enemigos, contenido
//   ... -executeMethod PruebaRonda19.Final       de principio a fin: lago, muro, jefe, guardado; y el desafio de la Sombra
// Escribe "[R19] OK ..." / "[R19] FALLO ..." y el total. Capturas en PruebaNieve/r19_*.png.
[InitializeOnLoad]
public static class PruebaRonda19
{
    private const string Clave = "PruebaRonda19.Modo";

    static PruebaRonda19()
    {
        EditorApplication.playModeStateChanged += e =>
        {
            string modo = SessionState.GetString(Clave, "");
            if (modo == "") return;
            if (e == PlayModeStateChange.EnteredPlayMode) new GameObject("PruebaRonda19").AddComponent<Conductor>().modo = modo;
            else if (e == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetString(Clave, "");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
        };
    }

    public static void Recorrido() => Lanzar("recorrido");
    public static void Mecanicas() => Lanzar("mecanicas");
    public static void Final() => Lanzar("final");

    private static void Lanzar(string modo)
    {
        SessionState.SetString(Clave, modo);
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
        private const BindingFlags Todo = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static;

        private void Ok(string s) { ok++; Debug.Log("[R19] OK " + s); }
        private void Fallo(string s) { fallos++; Debug.LogWarning("[R19] FALLO " + s); }
        private void Comprobar(bool c, string s) { if (c) Ok(s); else Fallo(s); }

        private IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject);
            Application.logMessageReceived += (c, st, t) => { if ((t == LogType.Exception || t == LogType.Error) && !c.StartsWith("[R19]")) { errores++; Debug.Log("[R19] error visto: " + c); } };
            carpeta = Path.Combine(Directory.GetCurrentDirectory(), "PruebaNieve");
            Partida.CarpetaPruebas = Path.Combine(carpeta, "PartidasR19");
            if (Directory.Exists(Partida.CarpetaPruebas)) Directory.Delete(Partida.CarpetaPruebas, true);
            Directory.CreateDirectory(Partida.CarpetaPruebas);
            Globales.Recargar();
            IEnumerator r = modo == "recorrido" ? PruebaRecorrido() : modo == "mecanicas" ? PruebaMecanicas() : PruebaFinal();
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
            Debug.Log($"[R19] RESULTADO {modo}: {ok} OK, {fallos} FALLOS");
            Time.timeScale = 1f;
            Partida.Descargar();
            Globales.GuardarPendiente();
            EditorApplication.ExitPlaymode();
        }

        private IEnumerator NuevaPartida()
        {
            yield return new WaitForSeconds(1f);
            Partida.Nueva("Nivel Nieve");
            Equipo.Reiniciar();
            SceneManager.LoadScene("Nivel Nieve");
            yield return EsperarEscena("Nivel Nieve");
            yield return new WaitForSeconds(2f);
            p = Object.FindFirstObjectByType<PlayerControler>();
            p.InvulnerableExterno = true;
        }

        // ------------------------------------------------------------------ Recorrido (saltos)

        private IEnumerator PruebaRecorrido()
        {
            yield return NuevaPartida();
            foreach (EnemyHealth e in Object.FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None)) if (e.GetComponent<JefeBase>() == null) Destroy(e.gameObject);
            ZonaVentisca.Activa = false;   // las distancias, sin viento (el viento se prueba aparte)
            // Los sellos del camino, abiertos (se prueban aparte).
            foreach (SelloElemental s in Object.FindObjectsByType<SelloElemental>(FindObjectsSortMode.None)) s.ForzarAbrir();
            AguaCongelable lago = Object.FindFirstObjectByType<AguaCongelable>();
            lago.Golpear(Elemento.Hielo, lago.transform.position);
            yield return new WaitForSeconds(1.5f);

            // (nombre, desde x, desde suelo, destino x0, x1, suelo, doble salto)
            var saltos = new (string n, float dx, float dy, float x0, float x1, float top, bool doble)[]
            {
                ("campamento: escalon", 32.5f, 0f, 34.3f, 41.7f, 2f, true),
                ("escalon: bosque", 40.5f, 2f, 42.3f, 55.5f, 3f, false),
                ("grieta del bosque", 73.4f, 3f, 76.3f, 83.6f, 3f, false),
                ("repisa a la loma", 77.6f, 3f, 79.2f, 81.8f, 6f, true),
                ("loma", 81.6f, 6f, 84.3f, 97.6f, 7f, false),
                ("loma: bajada", 96.8f, 7f, 98.3f, 105.7f, 6f, false),
                ("bajada: claro", 104.8f, 6f, 106.3f, 112f, 3f, false),
                ("desfiladero: a la roca", 128.8f, 3f, 130.05f, 130.95f, 5f, true),
                ("desfiladero: roca y grieta 1", 130.6f, 5f, 134.3f, 140f, 3f, false),
                ("desfiladero: grieta 2", 148.8f, 3f, 152.3f, 158f, 3f, false),
                ("desfiladero: grieta 3", 166.8f, 3f, 170.3f, 176f, 3f, false),

                ("tumulo: escalon", 221.6f, 3f, 223.2f, 224.8f, 4f, false),
                ("tumulo: cima", 224f, 4f, 225.3f, 232.7f, 6f, true),
                ("tumulo: orilla", 232f, 6f, 233.3f, 235.5f, 3f, false),
                ("pozo del tumulo 1", 226.8f, -3f, 228.2f, 229.8f, -1f, true),
                ("pozo del tumulo 2", 229.2f, -1f, 226.2f, 227.8f, 1f, true),
                ("pozo del tumulo 3", 227.2f, 1f, 230.1f, 231.9f, 3f, true),
                ("galeria inferior: escalon 1", 274.6f, -3f, 276.2f, 277.8f, -1f, true),
                ("galeria inferior: escalon 2", 277.2f, -1f, 278.2f, 279.8f, 1f, true),
                ("galeria inferior: a las ruinas", 279.3f, 1f, 282.3f, 285.5f, 3f, true),
                ("galeria: bajar al escalon", 275.2f, 3f, 278.2f, 279.8f, 1f, false),
                ("grieta de las ruinas", 284.7f, 3f, 288.3f, 290.7f, 3f, false),
                ("ruinas: escalon 1", 289.6f, 3f, 291.2f, 293.8f, 5f, true),
                ("ruinas: escalon 2", 293f, 5f, 294.2f, 296.8f, 6f, false),
                ("ruinas: escalon 3", 296f, 6f, 297.2f, 299.8f, 8f, true),
                ("ruinas: meseta", 299f, 8f, 300.3f, 306f, 10f, true),
                ("atalaya 1", 300.7f, 10f, 302.1f, 303.9f, 12f, true),
                ("atalaya 2", 303.4f, 12f, 306.1f, 307.9f, 14f, true),
                ("atalaya 3", 306.6f, 14f, 302.1f, 303.9f, 16f, true),
                ("atalaya 4", 303.4f, 16f, 306.1f, 307.9f, 18f, true),
                ("atalaya 5 (nicho)", 307.5f, 18f, 304.4f, 305.9f, 20f, true),
                ("meseta: antesala", 329f, 10f, 330.5f, 340f, 3f, false),
            };
            int bien = 0;
            foreach (var sa in saltos)
            {
                p = Object.FindFirstObjectByType<PlayerControler>();
                SetCampo(p, "isInvincible", true);
                PonerEje(0f);
                Teletransportar(new Vector3(sa.dx, sa.dy + 0.7f, 0f));
                yield return new WaitForSeconds(0.5f);
                float centro = (sa.x0 + sa.x1) * 0.5f;
                int dir = centro > sa.dx ? 1 : -1;
                Llamar(p, "SetFacing", dir);
                PonerEje(dir);
                Entrada().IsJumping = true;
                bool dobleHecho = false;
                Rigidbody2D rb = p.GetComponent<Rigidbody2D>();
                for (float t = 0f; t < 4f; t += Time.deltaTime)
                {
                    if (sa.doble && !dobleHecho && t > 0.15f && rb.linearVelocity.y < 1f) { Entrada().IsJumping = true; dobleHecho = true; }
                    float x = p.transform.position.x;
                    if (dir > 0 ? x >= centro : x <= centro) PonerEje(0f);
                    if (t > 0.4f && (bool)Campo(p, "isGrounded") && Mathf.Abs(rb.linearVelocity.y) < 0.1f) break;
                    yield return null;
                }
                PonerEje(0f);
                yield return new WaitForSeconds(0.3f);
                Vector2 f = p.transform.position;
                bool llego = f.x >= sa.x0 - 0.3f && f.x <= sa.x1 + 0.3f && Mathf.Abs(f.y - (sa.top + 0.63f)) < 0.4f;
                if (llego) bien++;
                Comprobar(llego, $"salto \"{sa.n}\" (acaba en {f.x:0.00},{f.y:0.00}; destino x {sa.x0:0.0}-{sa.x1:0.0} suelo {sa.top:0})");
            }
            Debug.Log($"[R19] {bien} de {saltos.Length} saltos llegan");

            // Camino principal por tierra: sin muros invisibles entre zonas (se anda de 3 a 360 con teletransportes cortos).
            yield return Captura("r19_recorrido_fin");
        }

        // ------------------------------------------------------------------ Mecanicas

        private IEnumerator PruebaMecanicas()
        {
            yield return NuevaPartida();
            // Contenido.
            int enemigos = Object.FindObjectsByType<EnemigoBase>(FindObjectsSortMode.None).Count(e => !(e is JefeBase));
            int hogueras = Object.FindObjectsByType<Hoguera>(FindObjectsSortMode.None).Length;
            int estatuas = Object.FindObjectsByType<EstatuaPista>(FindObjectsSortMode.None).Length;
            int cofresM = Object.FindObjectsByType<CofreMejora>(FindObjectsSortMode.None).Length;
            int cofresA = Object.FindObjectsByType<CofreAlmas>(FindObjectsSortMode.None).Length;
            int pinchos = Object.FindObjectsByType<ZonaDano>(FindObjectsSortMode.None).Length;
            Debug.Log($"[R19] contenido: enemigos {enemigos}, hogueras {hogueras}, estatuas {estatuas}, cofres de mejora {cofresM}, cofres de almas {cofresA}, pinchos {pinchos}");
            Comprobar(pinchos == 0, "no queda ningun pincho");
            Comprobar(hogueras == 4 && cofresM == 1 && cofresA == 3 && estatuas == 12 && enemigos == 23, "contenido esperado (4 hogueras, 1+3 cofres, 12 estatuas, 23 enemigos)");
            CofreMejora cm = Object.FindFirstObjectByType<CofreMejora>();
            Comprobar(cm.transform.position.y > 19f, "el cofre de mejora (lagrimas) esta en la Atalaya");
            CofreAlmas cofreFrasco = Object.FindObjectsByType<CofreAlmas>(FindObjectsSortMode.None).FirstOrDefault(c => (bool)Campo(c, "darFrasco"));
            Comprobar(cofreFrasco != null && cofreFrasco.transform.position.x > 300f && cofreFrasco.transform.position.x < 329f, "el cofre de almas con frasco esta en la Cripta");
            Comprobar(Object.FindObjectsByType<SelloSombrio>(FindObjectsSortMode.None).Length == 0 && Object.FindObjectsByType<MuroHielo>(FindObjectsSortMode.None).Length == 0,
                      "el portal oscuro y el muro de hielo son ahora sellos (con funcion y guardados)");
            EnemyHealth elite = Object.FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None).FirstOrDefault(h => h.Definicion != null && h.Definicion.EsElite);
            Comprobar(elite != null && elite.transform.position.x > 300f && elite.transform.position.y > 9f, "elite Morgath en la meseta");

            // Sellos: el de enseñanza.
            SelloElemental claro = Sello("claro");
            Teletransportar(new Vector3(107f, 3.7f, 0f));
            yield return new WaitForSeconds(0.5f);
            claro.Golpear(Elemento.Fuego, claro.transform.position);
            yield return new WaitForSeconds(0.3f);
            Comprobar(!claro.Abierto && claro.solido.enabled, "sello del claro: con fuego no se abre (reacciona)");
            yield return Captura("r19_m01_sello_claro");
            for (int i = 0; i < 2; i++) { claro.Golpear(Elemento.Sagrado, claro.transform.position); yield return new WaitForSeconds(0.3f); }
            yield return new WaitForSeconds(0.8f);
            Comprobar(claro.Abierto && !claro.solido.enabled && Partida.Bandera("sello_Nivel Nieve_claro"), "sello del claro: con sagrado se abre y queda guardado");
            yield return Captura("r19_m02_sello_abierto");
            // Muro de hielo (fuego), atalaya (fuego) y cripta (sagrado).
            foreach (var (clave, bueno, malo) in new[] { ("muro", Elemento.Fuego, Elemento.Hielo), ("atalaya", Elemento.Fuego, Elemento.Sagrado), ("cripta", Elemento.Sagrado, Elemento.Oscuro) })
            {
                SelloElemental s = Sello(clave);
                s.Golpear(malo, s.transform.position);
                yield return new WaitForSeconds(0.2f);
                bool cerrado = !s.Abierto;
                for (int i = 0; i < s.golpesNecesarios; i++) { s.Golpear(bueno, s.transform.position); yield return new WaitForSeconds(0.25f); }
                yield return new WaitForSeconds(0.8f);
                Comprobar(cerrado && s.Abierto && Partida.Bandera("sello_Nivel Nieve_" + clave), $"sello \"{clave}\": solo cede con {Elementos.Nombre(bueno)} y queda guardado");
            }
            // Las runas de cada sello se apagan al abrirlo.
            Comprobar(Object.FindObjectsByType<SimboloSello>(FindObjectsSortMode.None).Length >= 9, "cada sello tiene al menos dos señales (runa, brillo o grietas)");
            // Lago.
            AguaCongelable lago = Object.FindFirstObjectByType<AguaCongelable>();
            lago.Golpear(Elemento.Hielo, lago.transform.position);
            yield return new WaitForSeconds(1.5f);
            Comprobar(Partida.Bandera("sello_Nivel Nieve_lago"), "el lago congelado queda guardado");

            // Hielo quebradizo de enseñanza: cae a la galeria inferior, sin morir.
            SueloFalso q = Object.FindObjectsByType<SueloFalso>(FindObjectsSortMode.None).First(s => !s.conPista);
            Teletransportar(new Vector3(q.transform.position.x, 3.8f, 0f));
            yield return new WaitForSeconds(0.4f);
            yield return Captura("r19_m03_quebradizo_aviso");
            yield return new WaitForSeconds(2.2f);
            p = Object.FindFirstObjectByType<PlayerControler>();
            Comprobar(q.Caido && p != null && p.transform.position.y < 0f && p.transform.position.y > -3.5f, $"hielo quebradizo: cede y cae a salvo a la galeria inferior (y={p.transform.position.y:0.0})");
            yield return Captura("r19_m04_galeria_inferior");

            // Emboscadas de la galeria inferior: salen al acercarse, con aviso.
            MonticuloEmboscada emb = Object.FindObjectsByType<MonticuloEmboscada>(FindObjectsSortMode.None).OrderBy(m => Mathf.Abs(m.transform.position.x - 262f) + Mathf.Abs(m.transform.position.y + 3f)).First();
            Comprobar(emb.Escondido && !emb.GetComponent<Rigidbody2D>().simulated, "emboscada: el enemigo espera escondido bajo el montículo");
            Teletransportar(new Vector3(emb.transform.position.x - 3f, -2.3f, 0f));
            yield return new WaitForSeconds(0.35f);
            Comprobar(emb.Avisando, "emboscada: al acercarse el montículo avisa antes de salir");
            yield return Captura("r19_m05_emboscada_aviso");
            yield return new WaitForSeconds(1.2f);
            EnemigoBase eb = emb.GetComponent<EnemigoBase>();
            Comprobar(!emb.Escondido && eb.enabled && eb.Alerta, "emboscada: sale y ya esta en alerta");

            // Atajo: el derrumbe del tumulo no cede desde la orilla, si desde dentro.
            DerrumbeAtajo der = Object.FindFirstObjectByType<DerrumbeAtajo>();
            Teletransportar(new Vector3(234f, 3.7f, 0f));
            yield return new WaitForSeconds(0.3f);
            for (int i = 0; i < 4; i++) der.Golpear(Elemento.Ninguno, der.transform.position);
            yield return new WaitForSeconds(0.5f);
            Comprobar(der.gameObject.activeSelf, "atajo: desde la orilla no cede");
            Teletransportar(new Vector3(231.2f, 3.7f, 0f));
            yield return new WaitForSeconds(0.3f);
            for (int i = 0; i < 4; i++) { der.Golpear(Elemento.Ninguno, der.transform.position); yield return new WaitForSeconds(0.15f); }
            yield return new WaitForSeconds(1f);
            Comprobar(!der.gameObject.activeSelf && Partida.Bandera("atajo_Nivel Nieve_tumulo"), "atajo: desde dentro se abre y queda guardado");

            // Ventisca: empuja fuera del refugio y no dentro.
            Teletransportar(new Vector3(124.5f, 3.7f, 0f));
            yield return new WaitForSeconds(1f);
            bool empuja = Time.time < (float)Campo(p, "vientoHasta");
            Teletransportar(new Vector3(128.4f, 3.7f, 0f));
            yield return new WaitForSeconds(0.6f);
            bool protegido = Time.time >= (float)Campo(p, "vientoHasta");
            Comprobar(empuja && protegido, $"ventisca: empuja en campo abierto ({empuja}) y no tras la roca del refugio ({protegido})");
            yield return Captura("r19_m06_refugio");

            // Ojo vigia: avisa a otros con tope (2).
            EnemigoBase ojo = Object.FindObjectsByType<EnemigoBase>(FindObjectsSortMode.None).Where(e => e is EnemigoOjo).OrderBy(e => Mathf.Abs(e.transform.position.x - 159f)).First();
            Teletransportar(new Vector3(156f, 3.7f, 0f));
            yield return new WaitForSeconds(2f);
            Comprobar(ojo.Alerta && ojo.Avisados >= 1 && ojo.Avisados <= 2, $"ojo vigia: al verte avisa a otros, como mucho 2 ({ojo.Avisados})");

            // Rata de escarcha: reacciona a la imbuicion.
            EnemigoRata rata = Object.FindObjectsByType<EnemigoRata>(FindObjectsSortMode.None).Where(r => r.enabled && r.GetComponent<MonticuloEmboscada>() == null).OrderBy(r => Mathf.Abs(r.transform.position.x - 177f)).First();
            Teletransportar(new Vector3(rata.transform.position.x - 3.5f, 3.7f, 0f));
            p.GetComponent<ArmaImbuida>().Activar(Elemento.Hielo);
            yield return new WaitForSeconds(1.5f);
            string conHielo = rata != null ? rata.Actitud : "-";
            p.GetComponent<ArmaImbuida>().Activar(Elemento.Fuego);
            yield return new WaitForSeconds(1f);
            string conFuego = rata != null ? rata.Actitud : "-";
            Comprobar(conHielo == "crecida" && conFuego == "cauta", $"rata de escarcha: con escarcha se crece ({conHielo}) y con fuego se vuelve cauta ({conFuego})");
            int flanquean = Object.FindObjectsByType<EnemigoRata>(FindObjectsSortMode.None).Count(r => r.Flanquea);
            int seRetiran = Object.FindObjectsByType<EnemigoRata>(FindObjectsSortMode.None).Count(r => r.SeRetira);
            Comprobar(flanquean == 2 && seRetiran == 1, $"tacticas colocadas: {flanquean} ratas flanquean, {seRetiran} se retira");

            // Lo guardado sigue al cargar la partida otra vez.
            Partida.Guardar();
            SceneManager.LoadScene("Nivel Nieve");
            yield return EsperarEscena("Nivel Nieve");
            yield return new WaitForSeconds(2f);
            bool todos = new[] { "claro", "muro", "atalaya", "cripta" }.All(c => Sello(c).Abierto);
            Comprobar(todos && Object.FindFirstObjectByType<AguaCongelable>() != null && Object.FindFirstObjectByType<DerrumbeAtajo>() == null,
                      "al volver a cargar: sellos abiertos, lago congelado y atajo abierto");
        }

        // ------------------------------------------------------------------ De principio a fin

        private IEnumerator PruebaFinal()
        {
            yield return NuevaPartida();
            Comprobar(Globales.TieneLogro("aliento_helado"), "logro \"Aliento helado\" al llegar a la nieve");
            // Se avanza por tramos con los obstaculos de verdad: el lago se congela con escarcha y el muro con fuego.
            Teletransportar(new Vector3(198.5f, 3.7f, 0f));
            yield return new WaitForSeconds(0.5f);
            AguaCongelable lago = Object.FindFirstObjectByType<AguaCongelable>();
            Comprobar(lago.GetComponentInParent<Transform>() != null && Object.FindObjectsByType<SueloHielo>(FindObjectsSortMode.None).Any(s => s.name == "PuenteHielo" && !s.GetComponent<Collider2D>().enabled),
                      "el lago no se cruza sin congelarlo");
            p.GetComponent<ArmaImbuida>().Activar(Elemento.Hielo);
            lago.Golpear(Elemento.Hielo, lago.transform.position);
            yield return new WaitForSeconds(1.2f);
            PonerEje(1f);
            for (float t = 0f; t < 6f && p.transform.position.x < 219f; t += Time.deltaTime) yield return null;
            PonerEje(0f);
            Comprobar(p.transform.position.x >= 219f && p.transform.position.y > 3f, $"se cruza el lago andando por el hielo (x={p.transform.position.x:0.0})");
            // Hoguera 3.
            Hoguera h3 = Object.FindObjectsByType<Hoguera>(FindObjectsSortMode.None).OrderBy(h => Mathf.Abs(h.transform.position.x - 236f)).First();
            Teletransportar(h3.transform.position + new Vector3(0.6f, 0.7f, 0f));
            yield return new WaitForSeconds(0.3f);
            h3.Usar(p);
            yield return new WaitForSecondsRealtime(0.4f);
            MenuHoguera mh = Object.FindFirstObjectByType<MenuHoguera>();
            if (mh != null) Llamar(mh, "Cerrar");
            yield return new WaitForSecondsRealtime(0.3f);
            Comprobar(Vector2.Distance(GameManager.Instance.checkpointRespawnPosition, h3.PuntoReaparicion) < 0.1f, "hoguera 3: punto de reaparicion");
            // Muro de hielo: bloquea hasta usar fuego.
            SelloElemental muro = Sello("muro");
            Teletransportar(new Vector3(244.5f, 3.7f, 0f));
            PonerEje(1f);
            yield return new WaitForSeconds(1f);
            PonerEje(0f);
            Comprobar(p.transform.position.x < muro.transform.position.x, "el muro de hielo cierra el paso");
            p.GetComponent<ArmaImbuida>().Activar(Elemento.Fuego);
            for (int i = 0; i < 3; i++) { muro.Golpear(Elemento.Fuego, muro.transform.position); yield return new WaitForSeconds(0.25f); }
            yield return new WaitForSeconds(1f);
            PonerEje(1f);
            for (float t = 0f; t < 4f && p.transform.position.x < 252f; t += Time.deltaTime) yield return null;
            PonerEje(0f);
            Comprobar(p.transform.position.x >= 252f, $"con fuego el muro se derrite y se sigue ({p.transform.position.x:0.0})");
            // Antesala: hoguera 4 y la arena.
            Hoguera h4 = Object.FindObjectsByType<Hoguera>(FindObjectsSortMode.None).OrderBy(h => Mathf.Abs(h.transform.position.x - 350f)).First();
            Comprobar(Mathf.Abs(h4.transform.position.x - 350f) < 0.1f, "hoguera de la antesala en su sitio");
            ZonaJefe z = Object.FindFirstObjectByType<ZonaJefe>();
            ArenaJefe arena = Object.FindFirstObjectByType<ArenaJefe>();
            Teletransportar(new Vector3(z.transform.position.x + z.distanciaActivador + 1f, 3.7f, 0f));
            yield return Esperar(() => ArenaJefe.EnCombate, 3f);
            Comprobar(ArenaJefe.EnCombate && z.Muro.Bloquea, "la arena de la Sombra: el muro de niebla se forma y empieza la pelea");
            JefeBase jefe = null;
            yield return Esperar(() => (jefe = Object.FindFirstObjectByType<JefeBase>()) != null, 8f);
            yield return new WaitForSeconds(3f);
            foreach (DeadArea d in Object.FindObjectsByType<DeadArea>(FindObjectsSortMode.None)) foreach (Collider2D c in d.GetComponents<Collider2D>()) c.enabled = false;
            // Solo en la prueba: el agarre del jefe hace dano aunque se sea invulnerable; se le cura sin parar.
            StartCoroutine(MantenerVivo());
            EnemyHealth s = jefe.GetComponent<EnemyHealth>();
            s.DanoEstado(Mathf.RoundToInt(s.MaxHealth * 0.6f));
            yield return Esperar(() => ArenaJefe.EnFase2, 25f);
            yield return new WaitForSeconds(6f);
            for (int i = 0; i < 3 && ArenaJefe.EnCombate; i++)
            {
                if (jefe != null) jefe.GetComponent<EnemyHealth>().DanoEstado(99999);
                yield return Esperar(() => !ArenaJefe.EnCombate, 9f);
            }
            yield return new WaitForSeconds(3f);
            Comprobar(Partida.Bandera("jefe_Nivel Nieve") && Globales.TieneLogro("humedales_callan"), "la Sombra cae: jefe vencido en la partida y su logro");
            GameObject salida = GameObject.Find("ExitDoor");
            Comprobar(salida != null && salida.activeInHierarchy, "se abre la salida del nivel");
            yield return Captura("r19_f01_victoria");

            // El desafio de la Sombra sigue funcionando en la escena nueva.
            Partida.Descargar();
            FichaJefe f = FichaJefe.DeEscena("Nivel Nieve");
            Desafio.Empezar(f, false);
            yield return EsperarEscena("Nivel Nieve");
            yield return new WaitForSeconds(2.5f);
            p = Object.FindFirstObjectByType<PlayerControler>();
            p.InvulnerableExterno = true;
            Hoguera[] hs = Object.FindObjectsByType<Hoguera>(FindObjectsSortMode.None);
            Comprobar(hs.Length == 1 && Mathf.Abs(hs[0].transform.position.x - 350f) < 0.1f && Object.FindFirstObjectByType<TotemTienda>() != null,
                      $"desafio: una hoguera (la de la antesala), totem y cofre ({hs.Length} hogueras)");
            z = Object.FindFirstObjectByType<ZonaJefe>();
            Teletransportar(new Vector3(z.transform.position.x + z.distanciaActivador + 1f, 3.7f, 0f));
            yield return Esperar(() => ArenaJefe.EnCombate, 3f);
            yield return Esperar(() => Object.FindFirstObjectByType<JefeBase>() != null, 8f);
            Comprobar(ArenaJefe.EnCombate && Object.FindFirstObjectByType<JefeBase>() != null, "desafio: la pelea con la Sombra empieza");
            yield return Captura("r19_f02_desafio");
            Desafio.Salir();
            yield return EsperarEscena("Menu Principal");
            yield return new WaitForSeconds(1f);
        }

        // ------------------------------------------------------------------ Ayudas

        private IEnumerator MantenerVivo()
        {
            while (true)
            {
                if (p != null) { SetCampo(p, "currentHealth", p.VidaMaxima); SetCampo(p, "isInvincible", true); }
                yield return null;
            }
        }

        private static SelloElemental Sello(string clave) => Object.FindObjectsByType<SelloElemental>(FindObjectsSortMode.None).First(s => s.clave == clave);

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

        private GatherInput Entrada() => p.GetComponent<GatherInput>();
        private void PonerEje(float x) { if (p != null) SetCampo(Entrada(), "_value", new Vector2(x, 0f)); }

        private void Teletransportar(Vector3 pos)
        {
            if (p == null) p = Object.FindFirstObjectByType<PlayerControler>();
            if (p == null) return;
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
