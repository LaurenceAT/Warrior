using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Pruebas automaticas de la Ronda 15 (linea de comandos, sin -quit):
//   Unity -batchmode -projectPath ... -executeMethod PruebaBitacora.Menus
//     Menus por capas (Desafios, Logros, Bestiario), fichas que se desbloquean,
//     compatibilidad con globales viejos y el jefe secreto oculto.
//   ... -executeMethod PruebaBitacora.DesafioCompleto
//     Inventario del desafio (cofre, totem, hoguera), descubrimientos en combate,
//     muerte con consejo, 10 reinicios seguidos (Dificil) y victoria.
//   ... -executeMethod PruebaBitacora.Bestiario
//     Bestiario en la partida normal (Nieve): visto, elemento, derrotas y pausa.
// Cada comprobacion escribe "[Bitacora] OK ..." o "[Bitacora] FALLO ...", y al
// final el total. Las capturas van a "PruebaNieve/".
[InitializeOnLoad]
public static class PruebaBitacora
{
    private const string Clave = "PruebaBitacora.Modo";

    static PruebaBitacora()
    {
        EditorApplication.playModeStateChanged += Cambio;
    }

    public static void Menus() => Lanzar("menus", "Assets/Scenes/Menu Principal.unity");
    public static void DesafioCompleto() => Lanzar("desafio", "Assets/Scenes/Menu Principal.unity");
    public static void Bestiario() => Lanzar("bestiario", "Assets/Scenes/Nivel Nieve.unity");

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
        if (estado == PlayModeStateChange.EnteredPlayMode) new GameObject("PruebaBitacora").AddComponent<Conductor>().modo = modo;
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
        private int ok, fallos, errores;
        private PlayerControler p;
        private static readonly BindingFlags Priv = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;

        private IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject);
            carpeta = Path.Combine(Directory.GetCurrentDirectory(), "PruebaNieve");
            Directory.CreateDirectory(carpeta);
            Application.logMessageReceived += (c, st, t) =>
            {
                if ((t == LogType.Exception || t == LogType.Error) && !c.StartsWith("[Bitacora]")) { errores++; Debug.Log("[Bitacora] error visto: " + c); }
            };
            Partida.CarpetaPruebas = Path.Combine(carpeta, "PartidasBitacora");
            if (Directory.Exists(Partida.CarpetaPruebas)) Directory.Delete(Partida.CarpetaPruebas, true);
            Directory.CreateDirectory(Partida.CarpetaPruebas);
            Globales.Recargar();
            Progreso.Reiniciar();

            IEnumerator r = modo == "menus" ? PruebaMenus() : modo == "desafio" ? PruebaDesafio() : PruebaBestiario();
            float limite = Time.realtimeSinceStartup + 600f;
            while (true)
            {
                bool sigue;
                try { sigue = r.MoveNext(); }
                catch (System.Exception e) { Fallo("excepcion en la prueba: " + e); sigue = false; }
                if (!sigue) break;
                if (Time.realtimeSinceStartup > limite) { Fallo("tiempo agotado"); break; }
                yield return r.Current;
            }
            if (errores > 0) Fallo($"{errores} errores o excepciones en la consola");
            else Ok("sin errores ni excepciones en la consola");
            Debug.Log($"[Bitacora] RESULTADO {modo}: {ok} OK, {fallos} FALLOS");
            Time.timeScale = 1f;
            Partida.CarpetaPruebas = null;
            EditorApplication.ExitPlaymode();
        }

        private void Ok(string que) { ok++; Debug.Log("[Bitacora] OK " + que); }
        private void Fallo(string que) { fallos++; Debug.LogWarning("[Bitacora] FALLO " + que); }
        private void Comprobar(bool c, string que) { if (c) Ok(que); else Fallo(que); }

        // ------------------------------------------------------------------ Menus

        private IEnumerator PruebaMenus()
        {
            // Globales de antes de esta version: Wraith completado en Normal.
            string archivo = Path.Combine(Partida.CarpetaPruebas, "globales.json");
            File.WriteAllText(archivo, "{\"logros\":[],\"desafios\":[{\"jefe\":\"crimson_wraith\",\"dificil\":false,\"completado\":true,\"mejorTiempo\":321.0,\"muertesMejor\":2}],\"marcas\":[]}");
            Globales.Recargar();

            yield return new WaitForSeconds(2.5f);
            MenuPrincipal m = Object.FindFirstObjectByType<MenuPrincipal>();
            yield return Captura("r15_m00_menu");
            Llamar(m, "Mostrar", Campo(m, "panelDesafios"));
            yield return new WaitForSecondsRealtime(0.6f);
            MenuDesafios md = (MenuDesafios)Campo(m, "menuDesafios");
            FichaJefe wraith = FichaJefe.DeId("crimson_wraith"), sombra = FichaJefe.DeId("shadowed_wetlands"), cazadora = FichaJefe.DeId("blind_huntress");

            // Compatibilidad: todo lo del Wraith desbloqueado; nada de la Sombra.
            Comprobar(Bitacora.Tiene(Bitacora.ClaveAtaque(wraith, "tajos")) && Bitacora.Tiene(Bitacora.ClaveConsejo(wraith, "frenesi"))
                      && Bitacora.Tiene(Bitacora.ClaveFase(wraith, 2)) && Bitacora.Tiene(Bitacora.ClaveHistoria(wraith, "h4")),
                      "migracion: el Wraith completado antes de la version tiene toda su informacion");
            Comprobar(!Bitacora.Claves(sombra, Bitacora.Seccion.Ataques).Any() && !Bitacora.Tiene(Bitacora.ClaveFase(sombra, 1)), "migracion: la Sombra (sin completar) no tiene nada");
            Comprobar(Bitacora.HayNuevo(wraith), "el Wraith lleva el punto de nuevo");

            // Jefe secreto: ni en la lista ni en ningun texto.
            IList filas = (IList)Campo(md, "filas");
            Comprobar(filas.Count == 2, $"lista con 2 jefes (sin el secreto): {filas.Count}");
            bool fuga = md.GetComponentsInChildren<TextMeshProUGUI>(true).Any(t => t.text.Contains("Huntress") || t.text.Contains("Cazadora"));
            Comprobar(!fuga, "ningun texto del menu nombra a la Cazadora");
            Comprobar(!Bitacora.Claves(cazadora, Bitacora.Seccion.Ataques).Any(), "la Cazadora no tiene nada descubierto");

            // Capa 0: sin jefe elegido.
            GameObject ficha = ((RectTransform)Campo(md, "ficha")).gameObject;
            Comprobar(!ficha.activeSelf && ((GameObject)Campo(md, "placeholder")).activeSelf, "al entrar: ningun jefe elegido (solo el aviso 'Elige un jefe')");
            yield return Captura("r15_m01_desafios_lista");

            // Resaltar (seleccion del EventSystem) no elige.
            EventSystem.current.SetSelectedGameObject(((Button)Campo(filas[0], "boton")).gameObject);
            yield return new WaitForSecondsRealtime(0.2f);
            Comprobar(!ficha.activeSelf, "pasar por un jefe solo lo resalta (no abre su ficha)");

            // Clic: se elige, queda marcado y entra la ficha (capa 1).
            ((Button)Campo(filas[0], "boton")).onClick.Invoke();
            yield return new WaitForSecondsRealtime(0.4f);
            CapasMenu capas = (CapasMenu)Campo(md, "capas");
            OpcionEstilo e0 = (OpcionEstilo)Campo(filas[0], "estilo");
            Comprobar(ficha.activeSelf && capas.Profundidad == 1 && e0.Marcada, "clic en la Sombra: ficha abierta, capa 1 y marcada");
            string textos = string.Join("|", ficha.GetComponentsInChildren<TextMeshProUGUI>(true).Select(t => t.text));
            Comprobar(textos.Contains("???") && textos.Contains("Aún no sabes nada"), "Sombra sin descubrir: solo imagen, nombre y ???");
            yield return Captura("r15_m02_sombra_vacia");

            // Dificil bloqueado.
            Llamar(md, "ElegirDificultad", true);
            Comprobar(!(bool)Campo(md, "dificil"), "Dificil bloqueado en la Sombra: no se puede elegir");
            Button bDif = (Button)Campo(md, "botonDificil");
            Comprobar(bDif.GetComponent<OpcionEstilo>().Bloqueada, "el boton Dificil se ve bloqueado");

            // Atras: de la ficha a la lista; otra vez: nada (el menu principal vuelve).
            Comprobar(md.Atras() && !ficha.activeSelf && capas.Profundidad == 0 && !e0.Marcada, "atras: de la ficha a la lista");
            Comprobar(!md.Atras(), "atras en la lista: se deja al menu principal");

            // El Wraith: ficha completa, pestanas y nuevo.
            ((Button)Campo(filas[1], "boton")).onClick.Invoke();
            yield return new WaitForSecondsRealtime(0.5f);
            Comprobar(!Bitacora.HayNuevo(wraith, (Bitacora.Seccion)Campo(md, "seccion")), "la seccion que se abre queda vista");
            yield return Captura("r15_m03_wraith_ataques");
            string[] nombres = { "debilidades", "fases", "historia", "estadisticas" };
            for (int i = 1; i <= 4; i++)
            {
                Llamar(md, "MostrarSeccion", (Bitacora.Seccion)i);
                yield return new WaitForSecondsRealtime(0.35f);
                yield return Captura($"r15_m0{3 + i}_wraith_{nombres[i - 1]}");
            }
            Comprobar(!Bitacora.HayNuevo(wraith), "vistas todas las secciones: el punto de nuevo se va");
            Llamar(md, "ElegirDificultad", true);
            Comprobar((bool)Campo(md, "dificil"), "Dificil libre en el Wraith (Normal completado): queda marcado");
            yield return Captura("r15_m08_wraith_dificil");

            // Confirmacion: capa 2; atras la cierra sin salir de la ficha.
            Llamar(md, "PedirConfirmacion");
            yield return new WaitForSecondsRealtime(0.3f);
            GameObject conf = (GameObject)Campo(md, "confirmacion");
            Comprobar(conf.activeSelf && capas.Profundidad == 2, "Desafiar abre la confirmacion (capa 2)");
            yield return Captura("r15_m09_confirmacion");
            Comprobar(md.Atras() && !conf.activeSelf && ficha.activeSelf && capas.Profundidad == 1, "atras cierra la confirmacion y deja la ficha");
            md.Atras();

            // Logros: la pestana elegida queda marcada.
            Llamar(m, "Mostrar", Campo(m, "panelLogros"));
            yield return new WaitForSecondsRealtime(0.4f);
            MenuLogros ml = (MenuLogros)Campo(m, "menuLogros");
            IList pest = (IList)Campo(ml, "pestanas");
            Comprobar(((Button)pest[0]).GetComponent<OpcionEstilo>().Marcada, "Logros: la pestana Partida esta marcada");
            ((Button)pest[1]).onClick.Invoke();
            yield return new WaitForSecondsRealtime(0.3f);
            Comprobar(((Button)pest[1]).GetComponent<OpcionEstilo>().Marcada && !((Button)pest[0]).GetComponent<OpcionEstilo>().Marcada, "Logros: clic en Desafios la marca");
            yield return Captura("r15_m10_logros");

            // Bestiario: todo en silueta; luego desbloqueado.
            Llamar(m, "Mostrar", Campo(m, "panelBestiario"));
            yield return new WaitForSecondsRealtime(0.5f);
            MenuBestiario mb = (MenuBestiario)Campo(m, "menuBestiario");
            IList fb = (IList)Campo(mb, "filas");
            Comprobar(fb.Count == 18, $"Bestiario: 18 entradas ({fb.Count})");
            string tot = ((TextMeshProUGUI)Campo(mb, "total")).text;
            Comprobar(tot.StartsWith("0/"), "Bestiario vacio: " + tot);
            bool jefeEnBestiario = mb.GetComponentsInChildren<TextMeshProUGUI>(true).Any(t => t.text.Contains("Wraith") || t.text.Contains("Wetlands"));
            Comprobar(!jefeEnBestiario, "los jefes no salen en el Bestiario");
            yield return Captura("r15_m11_bestiario_vacio");
            Bitacora.DesbloquearBestiario();
            Llamar(m, "Mostrar", Campo(m, "panelBestiario"));
            yield return new WaitForSecondsRealtime(0.4f);
            object rataCeniza = fb.Cast<object>().First(f => ((FichaBestiario.Entrada)Campo(f, "entrada")).Id == "RataCeniza");
            ((Button)Campo(rataCeniza, "boton")).onClick.Invoke();
            yield return new WaitForSecondsRealtime(0.4f);
            Image retrato = (Image)Campo(mb, "retrato");
            Comprobar(retrato.material != null && retrato.material.shader.name == "Sprites/Flash", "variante: retrato con su tono de color");
            yield return Captura("r15_m12_bestiario_variante");
            object morgath = fb.Cast<object>().First(f => ((FichaBestiario.Entrada)Campo(f, "entrada")).Id == "EliteMorgath");
            ((Button)Campo(morgath, "boton")).onClick.Invoke();
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Captura("r15_m13_bestiario_elite");
            Comprobar(mb.Atras() && !mb.Atras(), "Bestiario: atras vuelve a la lista y luego sale");
        }

        // ------------------------------------------------------------------ Desafio

        private IEnumerator PruebaDesafio()
        {
            FichaJefe f = FichaJefe.DeId("crimson_wraith");
            // Normal completado (para jugar en Dificil) y ya migrado: se empieza sin nada descubierto.
            Globales.Completar(f.id, false, 999f, 5);
            Globales.PonerMarca("bitacora_v1", true);
            Bitacora.Reiniciar();
            yield return new WaitForSeconds(2f);

            Desafio.Empezar(f, true);
            yield return EsperarEscena(f.escena);
            yield return new WaitForSeconds(2.5f);
            p = Object.FindFirstObjectByType<PlayerControler>();
            Comprobar(Desafio.Activo && Desafio.Dificil && Partida.Actual == null && Progreso.Aislado, "desafio en Dificil, sin partida, personaje aislado");
            CofreMejora cofre = Object.FindObjectsByType<CofreMejora>(FindObjectsSortMode.None).First(c => c.name == "CofreDesafio");
            TotemTienda totem = Object.FindFirstObjectByType<TotemTienda>();
            Hoguera h = Object.FindFirstObjectByType<Hoguera>();

            // Cofre: al inventario, sin aplicar.
            ReservaPociones rp = ReservaPociones.Get();
            int maxAntes = rp.Maximo;
            Invulnerable();
            Teletransportar(cofre.transform.position + Vector3.right * 0.6f + Vector3.up * 0.5f);
            yield return new WaitForSeconds(0.5f);
            cofre.Interactuar(p);
            yield return new WaitForSeconds(5f);
            Comprobar(Desafio.TotalPorAplicar == 3 && Equipo.NivelEspada == 0 && Equipo.NivelCuracion == 0 && rp.Maximo == maxAntes,
                      $"cofre: 3 objetos por aplicar, nada aplicado (espada +{Equipo.NivelEspada}, frascos {rp.Maximo})");
            yield return Captura("r15_d01_indicador");

            // Totem: lo comprado va al inventario y queda Agotado.
            Teletransportar(totem.transform.position + Vector3.left * 0.8f + Vector3.up * 0.5f);
            yield return new WaitForSeconds(0.4f);
            TiendaTotem.Abrir(p);
            yield return new WaitForSecondsRealtime(0.4f);
            TiendaTotem t = Object.FindFirstObjectByType<TiendaTotem>();
            object Celda(string id) => ((IList)Campo(t, "celdas")).Cast<object>().First(c => ((FichaTienda.Articulo)Campo(c, "art")).id == id);
            Llamar(t, "Seleccionar", Celda("curacion")); Llamar(t, "Comprar");
            Llamar(t, "Seleccionar", Celda("sangre_1")); Llamar(t, "Comprar");
            yield return new WaitForSecondsRealtime(0.3f);
            Comprobar(Desafio.TotalPorAplicar == 5 && Desafio.Comprados.Contains("curacion") && Equipo.NivelCuracion == 0,
                      $"totem: 2 compras al inventario ({Desafio.TotalPorAplicar} por aplicar), agotadas y sin aplicar");
            string detalle = ((TextMeshProUGUI)Campo(t, "detTexto")).text;
            Comprobar(detalle.Contains("hoguera"), "el totem dice que se aplica en la hoguera");
            yield return Captura("r15_d02_totem");
            TiendaTotem.Cerrar();
            yield return new WaitForSecondsRealtime(0.3f);

            // Morir: el inventario se conserva.
            SetCampo(p, "isInvincible", false);
            p.TakeDamage(99999);
            yield return EsperarReaparecer();
            Comprobar(Desafio.TotalPorAplicar == 5 && Desafio.Muertes == 1, $"tras morir: se conservan los 5 objetos (muertes {Desafio.Muertes})");

            // Hoguera: Aplicar mejoras.
            Teletransportar(h.transform.position + Vector3.right * 0.5f + Vector3.up * 0.6f);
            MenuHoguera.Abrir(h, p);
            yield return new WaitForSecondsRealtime(0.4f);
            MenuHoguera mh = Object.FindFirstObjectByType<MenuHoguera>();
            Button botonEquipo = (Button)Campo(mh, "botonEquipo");
            Comprobar(botonEquipo.GetComponentInChildren<TextMeshProUGUI>().text.Contains("Aplicar mejoras"), "la hoguera ofrece 'Aplicar mejoras (5)'");
            Llamar(mh, "AbrirEquipo");
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Captura("r15_d03_hoguera_aplicar");
            Llamar(mh, "AplicarTodo");
            yield return new WaitForSecondsRealtime(0.5f);
            Comprobar(Desafio.TotalPorAplicar == 0 && Equipo.NivelEspada == 1 && Equipo.NivelCuracion == 2 && rp.Maximo == maxAntes + 2,
                      $"aplicar todo: espada +{Equipo.NivelEspada}, curacion +{Equipo.NivelCuracion}, frascos {maxAntes}->{rp.Maximo}");
            yield return Captura("r15_d04_hoguera_aplicado");
            Llamar(mh, "Cerrar");
            yield return new WaitForSecondsRealtime(0.4f);

            // Al jefe: descubrimientos en combate (sin avisos).
            ArenaJefe arena = Object.FindFirstObjectByType<ArenaJefe>();
            Teletransportar(arena.transform.position + Vector3.up);
            JefeBase jefe = null;
            float tj = Time.time;
            while (jefe == null && Time.time - tj < 12f) { jefe = Object.FindFirstObjectByType<JefeBase>(); yield return null; }
            Comprobar(jefe != null, "el jefe aparece");
            p.InvulnerableExterno = true;
            yield return new WaitForSeconds(14f);
            int ataques = f.ataques.Count(a => Bitacora.Tiene(Bitacora.ClaveAtaque(f, a.id)));
            Comprobar(ataques >= 2 && Bitacora.Tiene(Bitacora.ClaveAtaque(f, "caida_entrada")), $"ataques vistos en combate: {ataques}");
            Comprobar(Bitacora.Tiene(Bitacora.ClaveFase(f, 1)) && !Bitacora.Tiene(Bitacora.ClaveFase(f, 2)), "fase 1 descubierta, la 2 no");
            Comprobar(Bitacora.Tiene(Bitacora.ClaveHistoria(f, "h1")) && !Bitacora.Tiene(Bitacora.ClaveHistoria(f, "h3")), "historia: fragmento del primer intento");
            Globales.Registro reg = Globales.Desafio(f.id, true);
            Comprobar(reg != null && reg.intentos == 1, $"estadisticas: 1 intento en Dificil ({reg?.intentos})");
            EnemyHealth sj = jefe.GetComponent<EnemyHealth>();
            EnemyHealth.ElementoDelGolpe = Elemento.Fuego;
            sj.TakeDamage(1, p.transform.position, 0f);
            Comprobar(Bitacora.Tiene(Bitacora.ClaveElemento(f, Elemento.Fuego)) && !Bitacora.Tiene(Bitacora.ClaveElemento(f, Elemento.Hielo)), "golpe con fuego: se revela el fuego");

            // Muerte por un ataque: su consejo.
            p.InvulnerableExterno = false;
            SetCampo(p, "isInvincible", false);
            int consejos = f.ataques.Count(a => Bitacora.Tiene(Bitacora.ClaveConsejo(f, a.id)));
            p.TakeDamage(99999, jefe, PlayerControler.TipoDano.Fisico);
            yield return new WaitForSecondsRealtime(1.5f);
            string resumen = ((TextMeshProUGUI)Campo(Object.FindFirstObjectByType<PantallaMuerte>(), "resumen")).text;
            int consejos2 = f.ataques.Count(a => Bitacora.Tiene(Bitacora.ClaveConsejo(f, a.id)));
            Comprobar(consejos2 == consejos + 1, $"morir por un ataque desbloquea su consejo ({consejos} -> {consejos2})");
            Comprobar(resumen.Contains("Nuevo en la ficha") && resumen.Contains("consejo"), "resumen en la pantalla de muerte: " + resumen);
            yield return Captura("r15_d05_muerte_resumen");
            yield return EsperarReaparecer();
            reg = Globales.Desafio(f.id, true);
            Comprobar(reg.muertes == 1, $"estadisticas: 1 muerte en Dificil ({reg.muertes})");

            // Reiniciar 10 veces seguidas desde la pausa.
            int objetosAntes = -1;
            int claves = Bitacora.Claves(f, Bitacora.Seccion.Ataques).Count();
            for (int i = 1; i <= 10; i++)
            {
                Desafio.GuardarObjeto(Equipo.Objeto.PiedraForja);
                Progreso.SumarAlmas(500);
                GameManager.Instance.PauseGame();
                yield return new WaitForSecondsRealtime(0.3f);
                MenuPausa mp = MenuPausa.Get();
                Button bRein = (Button)Campo(mp, "botonReiniciarDesafio"), bBest = (Button)Campo(mp, "botonBestiario");
                if (i == 1) Comprobar(bRein.gameObject.activeSelf && !bBest.gameObject.activeSelf, "pausa del desafio: 'Reiniciar desafío' (y no el Bestiario)");
                Llamar(mp, "PedirReinicio");
                yield return new WaitForSecondsRealtime(0.3f);
                if (i == 1)
                {
                    Comprobar(MenuPausa.LibroAbierto, "con la confirmacion abierta, Esc no cierra la pausa");
                    yield return Captura("r15_d06_confirmar_reinicio");
                }
                Llamar(mp, "ConfirmarReinicio");
                yield return EsperarEscena(f.escena, true);
                yield return new WaitForSeconds(2f);
                p = Object.FindFirstObjectByType<PlayerControler>();
                int jefes = Object.FindObjectsByType<JefeBase>(FindObjectsSortMode.None).Length;
                int totems = Object.FindObjectsByType<TotemTienda>(FindObjectsSortMode.None).Length;
                int cofres = Object.FindObjectsByType<CofreMejora>(FindObjectsSortMode.None).Count(c => c.name == "CofreDesafio");
                int indicadores = Object.FindObjectsByType<IndicadorInventario>(FindObjectsSortMode.None).Length;
                int sistemas = Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length;
                int objetos = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
                CofreMejora c0 = Object.FindObjectsByType<CofreMejora>(FindObjectsSortMode.None).First(c => c.name == "CofreDesafio");
                bool limpio = Time.timeScale == 1f && !GameManager.Instance.IsPaused && Desafio.Activo && Desafio.Dificil && Desafio.Reiniciado && !Desafio.Reiniciando
                              && Desafio.Muertes == 0 && Desafio.Segundos < 6f && Desafio.TotalPorAplicar == 0 && Desafio.Comprados.Count == 0
                              && Progreso.Almas == 0 && Equipo.NivelEspada == 0 && Progreso.NivelTotal <= 1 && jefes == 0 && totems == 1 && cofres == 1 && !c0.Abierto
                              && indicadores == 1 && sistemas == 1 && p != null && !p.InvulnerableExterno && !Cinematica.Activa;
                Debug.Log($"[Bitacora] reinicio {i}: t={Desafio.Segundos:0.0} almas={Progreso.Almas} jefes={jefes} totems={totems} cofres={cofres} indicadores={indicadores} " +
                          $"eventsystems={sistemas} objetos={objetos} memoria={System.GC.GetTotalMemory(false) / 1048576}MB");
                if (i == 3) objetosAntes = objetos;
                Comprobar(limpio, $"reinicio {i}: todo de cero y limpio");
                if (i == 10) Comprobar(objetosAntes < 0 || Mathf.Abs(objetos - objetosAntes) <= 5, $"sin objetos acumulados tras 10 reinicios ({objetosAntes} -> {objetos})");
            }
            Comprobar(Bitacora.Claves(f, Bitacora.Seccion.Ataques).Count() >= claves && Globales.Desafio(f.id, true).muertes == 1 && Globales.Completado(f.id, false),
                      "tras reiniciar: lo descubierto, las estadisticas y los tiempos se conservan");

            // El jefe tras reiniciar: toda su vida y en la fase 1 (Dificil: x1.5).
            ArenaJefe ar = Object.FindFirstObjectByType<ArenaJefe>();
            Invulnerable();
            p.InvulnerableExterno = true;
            Teletransportar(ar.transform.position + Vector3.up);
            JefeBase j2 = null;
            tj = Time.time;
            while (j2 == null && Time.time - tj < 12f) { j2 = Object.FindFirstObjectByType<JefeBase>(); yield return null; }
            yield return new WaitForSeconds(3f);
            EnemyHealth s2 = j2.GetComponent<EnemyHealth>();
            Comprobar(s2.CurrentHealth == s2.MaxHealth && !ArenaJefe.EnFase2, $"jefe tras reiniciar: vida llena {s2.CurrentHealth}/{s2.MaxHealth}, fase 1");
            Comprobar(Globales.Desafio(f.id, true).intentos == 2, $"el intento tras reiniciar cuenta ({Globales.Desafio(f.id, true).intentos})");

            // Victoria: todos los consejos y la historia (Dificil: tambien el ultimo fragmento).
            // Primero a mitad de vida (se transforma: fase 2) y luego el golpe final.
            s2.DanoEstado(Mathf.RoundToInt(s2.MaxHealth * 0.6f));
            float t2 = Time.time;
            while (!ArenaJefe.EnFase2 && Time.time - t2 < 25f) yield return null;
            Comprobar(ArenaJefe.EnFase2, "el jefe llega a su fase 2");
            yield return new WaitForSeconds(6f);
            for (int i = 0; i < 2 && j2 != null; i++)
            {
                s2.DanoEstado(99999);
                yield return new WaitForSeconds(9f);
            }
            float tf = Time.realtimeSinceStartup;
            while (!PantallaDesafio.Abierta && Time.realtimeSinceStartup - tf < 20f) yield return null;
            yield return new WaitForSecondsRealtime(1f);
            Comprobar(f.ataques.All(a => Bitacora.Tiene(Bitacora.ClaveConsejo(f, a.id))), "victoria: todos los consejos");
            Comprobar(Bitacora.Tiene(Bitacora.ClaveHistoria(f, "h3")) && Bitacora.Tiene(Bitacora.ClaveHistoria(f, "h4")) && Bitacora.Tiene(Bitacora.ClaveFase(f, 2)),
                      "victoria en Dificil: fragmentos de victoria y de Dificil, y la fase 2");
            string datos = ((TextMeshProUGUI)Campo(Object.FindFirstObjectByType<PantallaDesafio>(), "datos")).text;
            Comprobar(datos.Contains("Nuevo en la ficha"), "resumen en la pantalla de desafio completado");
            yield return Captura("r15_d07_completado");

            Desafio.Salir();
            yield return EsperarEscena("Menu Principal");
            yield return new WaitForSeconds(2f);
            MenuPrincipal m = Object.FindFirstObjectByType<MenuPrincipal>();
            MenuDesafios md = (MenuDesafios)Campo(m, "menuDesafios");
            Comprobar(Campo(md, "elegido") as FichaJefe == f, "al volver: Desafios abierto con el Wraith elegido");
            yield return Captura("r15_d08_vuelta");
        }

        // ------------------------------------------------------------------ Bestiario en la partida

        private IEnumerator PruebaBestiario()
        {
            Bitacora.Reiniciar();
            yield return new WaitForSeconds(2f);
            p = Object.FindFirstObjectByType<PlayerControler>();
            Invulnerable();
            EnemyHealth rata = Object.FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None).FirstOrDefault(e => e.Definicion != null && e.Definicion.name == "RataEscarcha");
            Comprobar(rata != null, "hay una rata de escarcha en la Nieve");
            if (rata == null) yield break;
            Comprobar(!Bitacora.Tiene(Bitacora.ClaveVisto("RataEscarcha")), "al empezar: rata no vista");
            EnemyHealth.ElementoDelGolpe = Elemento.Fuego;
            rata.TakeDamage(1, p.transform.position, 0f);
            Comprobar(Bitacora.Tiene(Bitacora.ClaveVisto("RataEscarcha")) && Bitacora.Tiene(Bitacora.ClaveElemento("RataEscarcha", Elemento.Fuego)), "golpe con fuego: vista y fuego revelado");
            rata.DanoEstado(99999);
            yield return new WaitForSeconds(0.5f);
            Comprobar(Bitacora.Tiene(Bitacora.ClaveDerrotado("RataEscarcha")) && !Bitacora.Tiene(Bitacora.ClavePatrones("RataEscarcha")) && Bitacora.Derrotas("RataEscarcha") == 1,
                      "primera derrota: descripcion, sin patrones");
            // Cuatro derrotas mas (con copias): los patrones.
            for (int i = 0; i < 4; i++)
            {
                EnemyHealth otra = Object.FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None).FirstOrDefault(e => e.Definicion != null && e.Definicion.name == "RataEscarcha" && !e.Muerto);
                if (otra == null) otra = Instantiate(rata.gameObject, rata.transform.position + Vector3.right * 40f, Quaternion.identity).GetComponent<EnemyHealth>();
                Llamar(otra, "Revivir", otra.MaxHealth);
                otra.DanoEstado(99999);
                yield return new WaitForSeconds(0.2f);
            }
            Comprobar(Bitacora.Derrotas("RataEscarcha") >= 5 && Bitacora.Tiene(Bitacora.ClavePatrones("RataEscarcha")), $"5 derrotas: patrones ({Bitacora.Derrotas("RataEscarcha")})");

            // La pausa de la partida normal: Bestiario (y no Reiniciar desafio).
            GameManager.Instance.PauseGame();
            yield return new WaitForSecondsRealtime(0.3f);
            MenuPausa mp = MenuPausa.Get();
            Comprobar(((Button)Campo(mp, "botonBestiario")).gameObject.activeSelf && !((Button)Campo(mp, "botonReiniciarDesafio")).gameObject.activeSelf,
                      "pausa normal: 'Bestiario' (y no 'Reiniciar desafío')");
            yield return Captura("r15_b01_pausa");
            Llamar(mp, "AbrirBestiario");
            yield return new WaitForSecondsRealtime(0.5f);
            MenuBestiario mb = (MenuBestiario)Campo(mp, "bestiario");
            object filaRata = ((IList)Campo(mb, "filas")).Cast<object>().First(f => ((FichaBestiario.Entrada)Campo(f, "entrada")).Id == "RataEscarcha");
            Comprobar(((PuntoNuevo)Campo(filaRata, "nuevo")).gameObject.activeSelf, "la rata lleva el punto de nuevo");
            ((Button)Campo(filaRata, "boton")).onClick.Invoke();
            yield return new WaitForSecondsRealtime(0.4f);
            Comprobar(!Bitacora.HayNuevo("RataEscarcha"), "vista su ficha: ya no es nueva");
            Comprobar(MenuPausa.LibroAbierto, "con el Bestiario abierto, Esc no cierra la pausa");
            yield return Captura("r15_b02_bestiario_rata");
            Comprobar(mb.Atras(), "atras: de la ficha a la lista");
            Llamar(mp, "CerrarBestiario", false);
            GameManager.Instance.ResumeGame();
            yield return new WaitForSecondsRealtime(0.3f);
            Comprobar(Time.timeScale == 1f && !GameManager.Instance.IsPaused, "se vuelve al juego");

            // Lo del Bestiario queda en globales.json (no en la partida).
            Globales.GuardarPendiente();
            string g = File.ReadAllText(Path.Combine(Partida.CarpetaPruebas, "globales.json"));
            Comprobar(g.Contains("b:RataEscarcha:p") && g.Contains("\"clave\": \"b:RataEscarcha\""), "guardado en globales.json");
            Comprobar(Directory.GetFiles(Partida.CarpetaPruebas, "partida_*.json").Length == 0, "ninguna partida guardada tocada");
        }

        // ------------------------------------------------------------------ Ayudas

        private IEnumerator EsperarEscena(string nombre, bool recarga = false)
        {
            float t0 = Time.realtimeSinceStartup;
            if (recarga) while (!PantallaCarga.Cargando && Time.realtimeSinceStartup - t0 < 5f) yield return null;
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

        private static object Campo(object o, string campo)
        {
            for (System.Type t = o.GetType(); t != null; t = t.BaseType)
            {
                FieldInfo f = t.GetField(campo, Priv);
                if (f != null) return f.GetValue(o);
            }
            Debug.LogWarning("[Bitacora] sin campo " + campo);
            return null;
        }

        private static void SetCampo(object o, string campo, object valor)
        {
            FieldInfo f = o.GetType().GetField(campo, Priv);
            if (f != null) f.SetValue(o, valor);
        }

        private static object Llamar(object o, string metodo, params object[] args)
        {
            MethodInfo m = o.GetType().GetMethods(Priv).FirstOrDefault(x => x.Name == metodo && x.GetParameters().Length == args.Length);
            if (m == null) { Debug.LogWarning("[Bitacora] sin metodo " + metodo); return null; }
            return m.Invoke(o, args);
        }

        private IEnumerator Captura(string nombre)
        {
            Camera cam = Camera.main;
            if (cam == null) cam = Object.FindFirstObjectByType<Camera>();
            if (cam == null) yield break;
            var cambiados = new List<Canvas>();
            foreach (Canvas c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay && c.gameObject.activeInHierarchy)
                { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = cam; c.planeDistance = 1f; c.sortingLayerName = "VFX"; c.sortingOrder = 500 + c.sortingOrder; cambiados.Add(c); }
            Canvas.ForceUpdateCanvases();
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
            Destroy(rt);
            Destroy(tex);
            foreach (Canvas c in cambiados) { c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder -= 500; }
        }
    }
}
