using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Pruebas de la Ronda 17 (linea de comandos, sin -quit):
//   ... -executeMethod PruebaRonda17.Hud
//     Desafio de la Cazadora: cofre nuevo, deseo del totem, interfaz sin solapes
//     en 6 resoluciones, latido cerca de la hoguera, topes de mejoras y texto de carga.
//   ... -executeMethod PruebaRonda17.Nieve
//     Partida normal en la Nieve: objetos al inventario (no se aplican solos),
//     guardado y carga, partidas viejas, hoguera con F, textos de muerte y de carga.
// Escribe "[R17] OK ..." / "[R17] FALLO ..." y el total.
[InitializeOnLoad]
public static class PruebaRonda17
{
    private const string Clave = "PruebaRonda17.Modo";

    static PruebaRonda17()
    {
        EditorApplication.playModeStateChanged += e =>
        {
            string modo = SessionState.GetString(Clave, "");
            if (modo == "") return;
            if (e == PlayModeStateChange.EnteredPlayMode) new GameObject("PruebaRonda17").AddComponent<Conductor>().modo = modo;
            else if (e == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetString(Clave, "");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
        };
    }

    public static void Hud() => Lanzar("hud", "Assets/Scenes/Menu Principal.unity");
    public static void Nieve() => Lanzar("nieve", "Assets/Scenes/Nivel Nieve.unity");

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

        private void Ok(string s) { ok++; Debug.Log("[R17] OK " + s); }
        private void Fallo(string s) { fallos++; Debug.LogWarning("[R17] FALLO " + s); }
        private void Comprobar(bool c, string s) { if (c) Ok(s); else Fallo(s); }

        private IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject);
            Application.logMessageReceived += (c, st, t) => { if ((t == LogType.Exception || t == LogType.Error) && !c.StartsWith("[R17]")) { errores++; Debug.Log("[R17] error visto: " + c); } };
            carpeta = Path.Combine(Directory.GetCurrentDirectory(), "PruebaNieve");
            Partida.CarpetaPruebas = Path.Combine(carpeta, "PartidasR17");
            if (Directory.Exists(Partida.CarpetaPruebas)) Directory.Delete(Partida.CarpetaPruebas, true);
            Directory.CreateDirectory(Partida.CarpetaPruebas);
            Globales.Recargar();
            IEnumerator r = modo == "hud" ? PruebaHud() : PruebaNieve();
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
            Debug.Log($"[R17] RESULTADO {modo}: {ok} OK, {fallos} FALLOS");
            Time.timeScale = 1f;
            // La carpeta de pruebas se queda puesta: lo que se guarde al salir no toca el progreso real.
            Partida.Descargar();
            Globales.GuardarPendiente();
            EditorApplication.ExitPlaymode();
        }

        // ------------------------------------------------------------------ HUD (desafio de la Cazadora)

        private IEnumerator PruebaHud()
        {
            FichaJefe caz = FichaJefe.DeId("blind_huntress");
            Globales.PonerMarca(CodigosSecretos.Marca(caz.id), true);
            Globales.PonerMarca("revelado_" + caz.id, true);
            Globales.PonerMarca("dialogo_cazadora", true);
            Comprobar(caz.almasCofre == 4500 && caz.objetosCofre.Length == 5, "ficha del cofre de la Cazadora: 4500 almas y 5 objetos");
            yield return new WaitForSeconds(2f);
            Desafio.Empezar(caz, false);
            yield return null;
            yield return new WaitForSecondsRealtime(0.9f);
            string textoCarga = TextoCarga();
            Comprobar(textoCarga == "Cargando nivel...", "desafio: el texto de carga sigue igual (" + textoCarga + ")");
            yield return EsperarEscena(caz.escena);
            yield return new WaitForSeconds(2f);
            p = Object.FindFirstObjectByType<PlayerControler>();
            p.InvulnerableExterno = true;

            // El cofre.
            CofreMejora cofre = Object.FindObjectsByType<CofreMejora>(FindObjectsSortMode.None).First(c => c.name == "CofreDesafio");
            Teletransportar(cofre.transform.position + Vector3.right * 0.6f + Vector3.up * 0.5f);
            yield return new WaitForSeconds(0.4f);
            cofre.Interactuar(p);
            yield return new WaitForSeconds(13f);
            Comprobar(Desafio.TotalPorAplicar == 5 && Desafio.PorAplicar(Equipo.Objeto.FrascoMana) == 1 && Desafio.PorAplicar(Equipo.Objeto.LagrimaCeleste) == 1,
                      $"cofre de la Cazadora: 5 objetos al inventario ({Desafio.TotalPorAplicar})");
            Comprobar(Progreso.Almas == 4500, $"cofre de la Cazadora: 4500 almas ({Progreso.Almas})");
            FichaTienda tienda = FichaTienda.Get();
            int total = tienda.articulos.Sum(a => Desafio.Precio(a));
            Comprobar(total > Progreso.Almas, $"con lo del cofre no se puede comprar todo el totem ({Progreso.Almas} < {total})");

            // El deseo, arriba a la derecha.
            Desafio.Deseado = "arma";
            yield return new WaitForSecondsRealtime(0.6f);
            IndicadorDeseo deseo = Object.FindFirstObjectByType<IndicadorDeseo>();
            CanvasGroup gd = deseo.GetComponent<CanvasGroup>();
            string estado = ((TextMeshProUGUI)Campo(deseo, "estado")).text;
            Comprobar(gd.alpha > 0.9f && estado.Contains("Puedes comprarlo"), "deseo visible: " + estado);
            Progreso.Gastar(4000);
            yield return null;
            estado = ((TextMeshProUGUI)Campo(deseo, "estado")).text;
            Comprobar(estado.Contains("Te faltan 600"), "deseo al gastar almas: " + estado);
            TiendaTotem.Abrir(p);
            yield return new WaitForSecondsRealtime(0.6f);
            Comprobar(gd.alpha < 0.1f, "deseo oculto con la tienda abierta");
            TiendaTotem.Cerrar();
            Progreso.SumarAlmas(4000);
            yield return new WaitForSecondsRealtime(0.6f);

            // Latido cerca de la hoguera (con objetos pendientes).
            Hoguera h = Object.FindFirstObjectByType<Hoguera>();
            h.Usar(p);
            yield return new WaitForSecondsRealtime(0.3f);
            Llamar(Object.FindFirstObjectByType<MenuHoguera>(), "Cerrar");
            yield return new WaitForSecondsRealtime(0.4f);
            IndicadorInventario ind = Object.FindFirstObjectByType<IndicadorInventario>();
            Teletransportar(h.transform.position + Vector3.right * 1f + Vector3.up * 0.6f);
            yield return new WaitForSeconds(1.2f);
            Comprobar(ind.CercaDeHoguera, "cerca de la hoguera con objetos pendientes: el aviso late");
            yield return Captura("r17_h01_latido", 1280, 720);

            // Al jefe: la barra, el nombre y las marcas I II III.
            // La entrada de su arena (la misma que usa PruebaNieve.CazadoraPrueba).
            Teletransportar(new Vector3(8f, 0.8f, 0f));
            yield return new WaitForSeconds(1f);
            Comprobar(!ind.CercaDeHoguera, "lejos de la hoguera: no late");
            float t0 = Time.time;
            while (Object.FindFirstObjectByType<BarraJefe>() == null && Time.time - t0 < 10f) yield return null;
            yield return new WaitForSeconds(5f);
            BarraJefe barra = Object.FindFirstObjectByType<BarraJefe>();
            Comprobar(barra != null, "la barra del jefe aparece");

            // Resoluciones: nada se pisa.
            foreach (Vector2Int res in new[] { new Vector2Int(1920, 1080), new Vector2Int(1920, 1200), new Vector2Int(2560, 1080), new Vector2Int(1280, 720), new Vector2Int(1024, 768), new Vector2Int(800, 600) })
            {
                string nombre = $"r17_h02_{res.x}x{res.y}";
                List<(string, Rect)> zonas = null;
                yield return Captura(nombre, res.x, res.y, z => zonas = z);
                var solapes = new List<string>();
                for (int i = 0; i < zonas.Count; i++)
                    for (int j = i + 1; j < zonas.Count; j++)
                        if (Grupo(zonas[i].Item1) != Grupo(zonas[j].Item1) && zonas[i].Item2.Overlaps(zonas[j].Item2))
                            solapes.Add(zonas[i].Item1 + "/" + zonas[j].Item1);
                Debug.Log($"[R17] {res.x}x{res.y}: " + string.Join("  ", zonas.Select(z => $"{z.Item1}=({z.Item2.xMin:0},{z.Item2.yMin:0})-({z.Item2.xMax:0},{z.Item2.yMax:0})")));
                Comprobar(zonas.Count >= 9 && solapes.Count == 0, $"{res.x}x{res.y}: sin solapes {(solapes.Count > 0 ? string.Join(", ", solapes) : "")}");
                Rect Z(string n) => zonas.First(z => z.Item1 == n).Item2;
                Comprobar(Z("almas").yMax <= Z("frasco_sangre").yMin && Z("frasco_mana").yMax <= Z("aviso").yMin && Z("frasco_mana").xMax > res.x * 0.85f,
                          $"{res.x}x{res.y}: abajo a la derecha, de abajo arriba: almas, pociones y aviso");
                Comprobar(Z("barra").width >= res.x * 0.55f, $"{res.x}x{res.y}: barra larga ({Z("barra").width / res.x:P0} del ancho)");
            }

            // Topes: todo lo del cofre y el totem aplicado.
            Progreso.SumarAlmas(20000);
            TiendaTotem.Abrir(p);
            yield return new WaitForSecondsRealtime(0.4f);
            TiendaTotem tt = Object.FindFirstObjectByType<TiendaTotem>();
            foreach (string id in new[] { "arma", "curacion", "mana", "sangre_1", "sangre_2", "sangre_3", "mana_1", "mana_2" })
            {
                object celda = ((System.Collections.IList)Campo(tt, "celdas")).Cast<object>().First(c => ((FichaTienda.Articulo)Campo(c, "art")).id == id);
                Llamar(tt, "Seleccionar", celda);
                Llamar(tt, "Comprar");
            }
            TiendaTotem.Cerrar();
            int pend = Desafio.TotalPorAplicar;
            foreach (Equipo.Objeto o in Desafio.ObjetosMejora) while (Desafio.Aplicar(o)) { }
            ReservaPociones rp = ReservaPociones.Get();
            Comprobar(pend == 13 && Desafio.TotalPorAplicar == 0, $"todo comprado y aplicado ({pend} objetos)");
            Comprobar(Equipo.NivelEspada == 2 && rp.Maximo == 7 && rp.MaximoMana == 4 && Equipo.NivelEspada <= Equipo.NivelMaximoEspada
                      && Equipo.FrascosSangreExtra <= AjustesProgreso.Get().frascosExtraMaximo,
                      $"topes razonables: espada +{Equipo.NivelEspada}, frascos {rp.Maximo} sangre / {rp.MaximoMana} mana");
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Captura("r17_h03_todo_aplicado", 1280, 720);
            Desafio.Salir();
            yield return EsperarEscena("Menu Principal");
        }

        private static string Grupo(string n) => n.StartsWith("barra") || n.StartsWith("marca") ? "barra" : n;

        // ------------------------------------------------------------------ Nieve (partida normal)

        private IEnumerator PruebaNieve()
        {
            yield return new WaitForSeconds(2f);
            Partida.Nueva("Nivel Nieve");
            Equipo.Reiniciar();
            p = Object.FindFirstObjectByType<PlayerControler>();
            p.InvulnerableExterno = true;
            ReservaPociones rp = ReservaPociones.Get();
            int maximo = rp.Maximo;

            // El cofre de almas y su frasco: al inventario.
            // El cofre que da el frasco (en la nieve nueva hay otros, solo de almas).
            CofreAlmas ca = Object.FindObjectsByType<CofreAlmas>(FindObjectsSortMode.None).FirstOrDefault(c => (bool)Campo(c, "darFrasco"));
            Comprobar(ca != null, "la Nieve tiene su cofre de almas");
            Teletransportar(ca.transform.position + Vector3.right * 0.5f + Vector3.up * 0.5f);
            yield return new WaitForSeconds(0.3f);
            ca.Interactuar(p);
            yield return new WaitForSeconds(2f);
            // El frasco cae al lado: si no se ha cogido ya al caer, se va a por el.
            FrascoExtra fe = Object.FindFirstObjectByType<FrascoExtra>();
            if (fe != null) { Teletransportar(fe.transform.position); yield return new WaitForSeconds(1f); }
            Comprobar(Object.FindFirstObjectByType<FrascoExtra>() == null, "frasco recogido");
            Comprobar(Equipo.Pendientes(Equipo.Objeto.FrascoSangre) == 1 && rp.Maximo == maximo, $"el frasco va al inventario, no se aplica ({rp.Maximo})");

            // El cofre de mejora: las dos lagrimas al inventario.
            CofreMejora cm = Object.FindFirstObjectByType<CofreMejora>();
            Teletransportar(cm.transform.position + Vector3.right * 0.6f + Vector3.up * 0.5f);
            yield return new WaitForSeconds(0.3f);
            cm.Interactuar(p);
            yield return new WaitForSeconds(5.5f);
            Comprobar(Inventario.Total == 3 && Equipo.NivelCuracion == 0 && Equipo.NivelMana == 0, $"cofre de mejora: 3 por aplicar ({Inventario.Total})");
            IndicadorInventario ind = Object.FindFirstObjectByType<IndicadorInventario>();
            Comprobar(ind != null && ind.GetComponent<CanvasGroup>().alpha > 0.5f, "el aviso de objetos por aplicar sale en la partida normal");
            yield return Captura("r17_n01_aviso", 1280, 720);

            // Guardado: los pendientes van en la partida.
            Partida.Guardar();
            string json = File.ReadAllText(Directory.GetFiles(Partida.CarpetaPruebas, "partida_*.json").First());
            Comprobar(json.Contains("\"frascosSangrePendientes\": 1") && json.Contains("\"lagrimasCuracion\": 1"), "los pendientes se guardan en la partida");

            // Hoguera con F: punto de reaparicion al momento.
            Hoguera h = Object.FindObjectsByType<Hoguera>(FindObjectsSortMode.None).OrderBy(x => Vector2.Distance(x.transform.position, p.transform.position)).First();
            Teletransportar(h.transform.position + Vector3.right * 0.6f + Vector3.up * 0.6f);
            yield return new WaitForSeconds(0.3f);
            h.Usar(p);
            yield return new WaitForSecondsRealtime(0.4f);
            MenuHoguera mh = Object.FindFirstObjectByType<MenuHoguera>();
            string aviso = ((TextMeshProUGUI)Campo(mh, "avisoPrincipal")).text;
            string boton = ((Button)Campo(mh, "botonEquipo")).GetComponentInChildren<TextMeshProUGUI>().text;
            Comprobar(aviso.Contains("Hoguera activada"), "primera vez: " + aviso);
            Comprobar(boton.Contains("Aplicar mejoras") && boton.Contains("(3)"), "la hoguera de la partida ofrece: " + boton);
            Comprobar(Vector2.Distance(GameManager.Instance.checkpointRespawnPosition, h.PuntoReaparicion) < 0.01f
                      && Partida.Actual.enHoguera && Vector2.Distance(new Vector2(Partida.Actual.x, Partida.Actual.y), h.PuntoReaparicion) < 0.01f,
                      "al activarla ya es el punto de reaparicion (sin descansar)");
            yield return Captura("r17_n02_hoguera", 1280, 720);
            Llamar(mh, "Cerrar");
            yield return new WaitForSecondsRealtime(0.4f);
            Comprobar(ind.CercaDeHoguera, "junto a la hoguera con pendientes: el aviso late");

            // Muerte justo despues: se reaparece en la hoguera.
            Teletransportar(h.transform.position + Vector3.right * 25f + Vector3.up * 3f);
            yield return new WaitForSeconds(0.3f);
            p.InvulnerableExterno = false;
            SetCampo(p, "isInvincible", false);
            p.TakeDamage(99999);
            yield return EsperarReaparecer();
            Comprobar(p != null && Vector2.Distance(p.transform.position, h.PuntoReaparicion) < 1.5f, $"reaparece en la hoguera activada ({p?.transform.position})");
            Comprobar(Partida.Actual.enHoguera && File.ReadAllText(Directory.GetFiles(Partida.CarpetaPruebas, "partida_*.json").First()).Contains("\"enHoguera\": true"),
                      "el punto de la hoguera queda guardado en la partida");
            p.InvulnerableExterno = true;

            // Aplicar en la hoguera.
            Teletransportar(h.transform.position + Vector3.right * 0.6f + Vector3.up * 0.6f);
            h.Usar(p);
            yield return new WaitForSecondsRealtime(0.4f);
            Comprobar(string.IsNullOrEmpty(((TextMeshProUGUI)Campo(mh, "avisoPrincipal")).text), "la segunda vez no repite 'Hoguera activada'");
            Llamar(mh, "AbrirEquipo");
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Captura("r17_n03_aplicar", 1280, 720);
            Llamar(mh, "AplicarTodo");
            yield return new WaitForSecondsRealtime(0.4f);
            Comprobar(Inventario.Total == 0 && rp.Maximo == maximo + 1 && Equipo.NivelCuracion == 1 && Equipo.NivelMana == 1,
                      $"aplicado en la hoguera: frascos {maximo}->{rp.Maximo}, curacion +{Equipo.NivelCuracion}, mana +{Equipo.NivelMana}");
            Llamar(mh, "Cerrar");
            yield return new WaitForSecondsRealtime(0.4f);
            Comprobar(ind.GetComponent<CanvasGroup>().alpha < 0.05f, "sin pendientes el aviso no se ve");

            // Partida vieja: las mejoras ya aplicadas se respetan.
            string vieja = Path.Combine(Partida.CarpetaPruebas, "partida_9.json");
            File.WriteAllText(vieja, "{\"ranura\":9,\"escena\":\"Nivel Nieve\",\"version\":2,\"frascosSangre\":2,\"frascosMana\":1,\"piedrasForja\":2,\"nivelEspada\":1,\"niveles\":[0,0,0,0,0],\"banderas\":[\"frasco_extra\"]}");
            Partida.Cargar(Partida.Listar().First(d => d.ranura == 9));
            Comprobar(Equipo.FrascosSangreExtra == 2 && Equipo.FrascosManaExtra == 1 && Equipo.NivelEspada == 1 && Equipo.Pendientes(Equipo.Objeto.PiedraForja) == 2
                      && Equipo.Pendientes(Equipo.Objeto.FrascoSangre) == 0, "partida vieja: lo aplicado se queda; las piedras sin usar quedan por aplicar");

            // Textos de muerte.
            TextosMuerte tm = TextosMuerte.Get();
            List<string> todas = tm.generales.Concat(tm.enemigoComun).Concat(tm.jefe).Concat(tm.porJefe.SelectMany(x => x.frases)).Concat(tm.caida).Concat(tm.letal)
                .Concat(tm.rapida).Concat(tm.racha5).Concat(tm.racha10).Concat(tm.racha20).Concat(tm.almas).Concat(tm.sangrado).Concat(tm.frio).Concat(tm.fuego).ToList();
            Comprobar(todas.Count >= 60 && todas.All(s => s.Length <= 82), $"{todas.Count} frases, la mas larga {todas.Max(s => s.Length)} caracteres");
            CausaMuerte.Resumen R(CausaMuerte.Tipo t, int seg = 1, int almas = 0, float vivo = 60f, string jefe = null) =>
                new CausaMuerte.Resumen { tipo = t, seguidas = seg, almasPerdidas = almas, segundosVivo = vivo, jefe = jefe };
            string anterior = null; bool repite = false;
            for (int i = 0; i < 40; i++) { string f = tm.Elegir(R(CausaMuerte.Tipo.Otro)); if (f == anterior) repite = true; anterior = f; }
            Comprobar(!repite, "nunca la misma frase dos veces seguidas");
            Comprobar(tm.caida.Contains(tm.Elegir(R(CausaMuerte.Tipo.Caida))), "caida");
            Comprobar(tm.letal.Contains(tm.Elegir(R(CausaMuerte.Tipo.Letal, jefe: "blind_huntress"))), "instakill o parry letal");
            Comprobar(tm.sangrado.Contains(tm.Elegir(R(CausaMuerte.Tipo.Sangrado))) && tm.frio.Contains(tm.Elegir(R(CausaMuerte.Tipo.Frio)))
                      && tm.fuego.Contains(tm.Elegir(R(CausaMuerte.Tipo.Fuego))), "estados");
            Comprobar(tm.racha5.Contains(tm.Elegir(R(CausaMuerte.Tipo.Enemigo, 5))) && tm.racha10.Contains(tm.Elegir(R(CausaMuerte.Tipo.Enemigo, 10)))
                      && tm.racha20.Contains(tm.Elegir(R(CausaMuerte.Tipo.Enemigo, 25))), "muertes seguidas (5, 10, 20+)");
            Comprobar(tm.almas.Contains(tm.Elegir(R(CausaMuerte.Tipo.Enemigo, almas: 3000))), "muchas almas perdidas");
            Comprobar(tm.rapida.Contains(tm.Elegir(R(CausaMuerte.Tipo.Enemigo, vivo: 3f))), "muerte rapida");
            Comprobar(tm.enemigoComun.Contains(tm.Elegir(R(CausaMuerte.Tipo.Enemigo))), "enemigo comun");
            List<string> wraith = tm.jefe.Concat(tm.porJefe.First(x => x.jefe == "crimson_wraith").frases).ToList();
            Comprobar(Enumerable.Range(0, 10).All(_ => wraith.Contains(tm.Elegir(R(CausaMuerte.Tipo.Jefe, jefe: "crimson_wraith")))), "jefe (con las del Wraith)");

            // Una muerte de verdad por caida.
            p = Object.FindFirstObjectByType<PlayerControler>();
            p.InvulnerableExterno = false;
            DeadArea da = Object.FindFirstObjectByType<DeadArea>();
            if (da != null)
            {
                Teletransportar(da.transform.position);
                yield return new WaitForSecondsRealtime(1.6f);
                string frase = ((TextMeshProUGUI)Campo(Object.FindFirstObjectByType<PantallaMuerte>(), "frase")).text;
                Comprobar(tm.caida.Contains(frase) || tm.rapida.Contains(frase) || tm.racha5.Contains(frase), "caer al vacio: " + frase);
                yield return Captura("r17_n04_muerte", 1280, 720);
                yield return EsperarReaparecer();
            }

            // Texto de carga en la partida normal.
            PantallaCarga.Cargar("Nivel Nieve", 0.3f);
            yield return null;
            var vistos = new HashSet<string>();
            for (float t = 0f; t < 1.5f && PantallaCarga.Cargando; t += 0.1f) { vistos.Add(TextoCarga()); yield return new WaitForSecondsRealtime(0.1f); }
            Comprobar(vistos.Count >= 2 && vistos.All(s => s.StartsWith("Cargando.") && s.Trim('.') == "Cargando" && s.Length <= "Cargando.......".Length),
                      "carga normal con puntos animados: " + string.Join(" | ", vistos));
            yield return EsperarEscena("Nivel Nieve");
        }

        // ------------------------------------------------------------------ Ayudas

        private static string TextoCarga()
        {
            object inst = typeof(PantallaCarga).GetField("instancia", Todo).GetValue(null);
            return inst != null ? ((TextMeshProUGUI)Campo(inst, "textoTitulo")).text : "(sin pantalla)";
        }

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

        private static void SetCampo(object o, string campo, object v) => o.GetType().GetField(campo, Todo)?.SetValue(o, v);

        private static void Llamar(object o, string metodo, params object[] args) =>
            o.GetType().GetMethods(Todo).First(x => x.Name == metodo && x.GetParameters().Length == args.Length).Invoke(o, args);

        // Captura a un tamano dado; "zonas" recibe los rectangulos (en pixeles)
        // de cada pieza de la interfaz con ese tamano.
        private IEnumerator Captura(string nombre, int ancho, int alto, System.Action<List<(string, Rect)>> zonas = null)
        {
            Camera cam = Camera.main;
            if (cam == null) yield break;
            RenderTexture rt = new RenderTexture(ancho, alto, 24);
            cam.targetTexture = rt;
            var cambiados = new List<Canvas>();
            foreach (Canvas c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay && c.gameObject.activeInHierarchy)
                { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = cam; c.planeDistance = 1f; c.sortingLayerName = "VFX"; c.sortingOrder += 500; cambiados.Add(c); }
            for (int i = 0; i < 3; i++) { Canvas.ForceUpdateCanvases(); yield return null; }
            if (zonas != null)
            {
                var l = new List<(string, Rect)>();
                void Anadir(string n, RectTransform r) { if (r != null && r.gameObject.activeInHierarchy) l.Add((n, RectPantalla(cam, r))); }
                ContadorPociones cp = Object.FindFirstObjectByType<ContadorPociones>();
                System.Array huecos = (System.Array)Campo(cp, "huecos");
                Anadir("frasco_sangre", (RectTransform)Campo(huecos.GetValue(0), "caja"));
                Anadir("frasco_mana", (RectTransform)Campo(huecos.GetValue(1), "caja"));
                Anadir("aviso", (RectTransform)Campo(Object.FindFirstObjectByType<IndicadorInventario>(), "caja"));
                Anadir("almas", Object.FindFirstObjectByType<ContadorAlmas>()?.transform.Find("Caja") as RectTransform);
                IndicadorDeseo d = Object.FindFirstObjectByType<IndicadorDeseo>();
                if (d != null) Anadir("deseo", d.transform.Find("Caja") as RectTransform);
                BarraJefe b = Object.FindFirstObjectByType<BarraJefe>();
                if (b != null)
                {
                    Anadir("barra", b.Marco);
                    Anadir("barra_nombre", (RectTransform)b.Marco.Find("Nombre"));
                    foreach (TextMeshProUGUI t in b.Marco.GetComponentsInChildren<TextMeshProUGUI>().Where(t => t.text.Contains("I") && t.text.Length < 12 && t.name != "Nombre"))
                        Anadir("marca_" + t.text, t.rectTransform);
                }
                zonas(l);
            }
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

        private static Rect RectPantalla(Camera cam, RectTransform r)
        {
            Vector3[] e = new Vector3[4];
            r.GetWorldCorners(e);
            Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, e[0]), b = RectTransformUtility.WorldToScreenPoint(cam, e[2]);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }
    }
}
