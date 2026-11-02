using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// La nieve construida desde el dibujo (Assets/SPRITES PARA NUEVOS NIVELES/nivel nieve.drawio).
// Leyenda: SPRITES PARA NUEVOS NIVELES/LEYENDA_DRAWIO.md. Lo que no esta en el dibujo:
// la ficha Assets/Data/Niveles/Extras Nieve. Plano: DISENO_NIEVE_NUEVA.md.
//
// Siempre parte de la copia Assets/Scenes/Nieve_Ronda19.unity (la nieve de la Ronda 19,
// fuera de la lista de escenas) y la guarda como "Nivel Nieve" (de ese nombre dependen el
// logro "Aliento helado", las partidas, el jefe vencido y el desafio de la Sombra).
// De la base se quedan tal cual: el fondo (parallax), el ambiente, la camara, la interfaz,
// la arena de la Sombra con su zona de jefe y niebla, y el portal. Lo demas se rehace.
// Como parte siempre de la misma base, generar dos veces da lo mismo.
//
// Menu Warrior > Nieve:
//   - Generar desde el diagrama.
//   - Usar la nieve Ronda 19 / Usar la nieve del diagrama: intercambia las escenas.
public static class GenerarNieveDiagrama
{
    public const string RutaActiva = "Assets/Scenes/Nivel Nieve.unity";
    public const string RutaBase = "Assets/Scenes/Nieve_Ronda19.unity";
    public const string RutaDiagramaGuardada = "Assets/Scenes/Nieve_Diagrama.unity";
    public const string RutaExtras = "Assets/Data/Niveles/Extras Nieve.asset";
    private const string Tiles = "Assets/Tiles/Nieve/";
    private const string Log = "[NieveDiagrama] ";
    // Desde aqui (x) empieza la arena de la Sombra: sus casillas vienen de la base.
    private const int XArena = 362;
    private const int YPintadoBase = -8;
    private static string CarpetaInforme => Path.Combine(Directory.GetCurrentDirectory(), "Informe Nieve Diagrama");

    // ------------------------------------------------------------------ Menu

    [MenuItem("Warrior/Nieve/Generar desde el diagrama")]
    public static void GenerarMenu()
    {
        if (!Application.isBatchMode && !EditorUtility.DisplayDialog("Nieve desde el diagrama",
                "Se rehace la escena \"Nivel Nieve\" a partir de Nieve_Ronda19 y del dibujo. " +
                "Lo que hayas cambiado a mano en \"Nivel Nieve\" se pierde (el fondo, el ambiente y la arena no cambian).", "Generar", "Cancelar"))
            return;
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Generar();
    }

    [MenuItem("Warrior/Nieve/Usar la nieve Ronda 19")]
    public static void UsarRonda19()
    {
        if (!File.Exists(RutaBase)) { Debug.LogWarning(Log + "No hay copia Nieve_Ronda19."); return; }
        if (File.Exists(RutaDiagramaGuardada)) { Debug.LogWarning(Log + "La nieve Ronda 19 ya es la que se juega."); return; }
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        AssetDatabase.MoveAsset(RutaActiva, RutaDiagramaGuardada);
        AssetDatabase.MoveAsset(RutaBase, RutaActiva);
        AsegurarEnBuild();
        Debug.Log(Log + "Ahora se juega la nieve RONDA 19 (la del diagrama queda en Nieve_Diagrama).");
    }

    [MenuItem("Warrior/Nieve/Usar la nieve del diagrama")]
    public static void UsarDiagrama()
    {
        if (!File.Exists(RutaDiagramaGuardada)) { Debug.LogWarning(Log + "La nieve del diagrama ya es la que se juega."); return; }
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        AssetDatabase.MoveAsset(RutaActiva, RutaBase);
        AssetDatabase.MoveAsset(RutaDiagramaGuardada, RutaActiva);
        AsegurarEnBuild();
        Debug.Log(Log + "Ahora se juega la nieve DEL DIAGRAMA (la Ronda 19 queda en Nieve_Ronda19).");
    }

    private static void AsegurarEnBuild()
    {
        var escenas = EditorBuildSettings.scenes.Where(s => s.path != RutaBase && s.path != RutaDiagramaGuardada).ToList();
        int i = escenas.FindIndex(s => s.path == RutaActiva);
        var activa = new EditorBuildSettingsScene(RutaActiva, true);
        if (i >= 0) escenas[i] = activa; else escenas.Insert(Mathf.Min(1, escenas.Count), activa);
        EditorBuildSettings.scenes = escenas.ToArray();
    }

    // ------------------------------------------------------------------ Lectura del dibujo

    public enum Tipo { Roca, Hueco, Hielo, Agua, ParedFalsa, ParedRompe, Ventisca, Hoguera, Cofre, Enemigo, Corriente, Sello, Estatua, Monticulo, Refugio, Texto }

    public class Forma
    {
        public string id, texto;
        public Tipo tipo;
        public Rect r;      // coordenadas del dibujo (y hacia abajo)
    }

    // Lee el dibujo con la leyenda. Lo desconocido se avisa y no se usa.
    public static List<Forma> LeerDiagrama(string ruta, out int desconocidas)
    {
        desconocidas = 0;
        var lista = new List<Forma>();
        var doc = new XmlDocument();
        doc.Load(ruta);
        foreach (XmlElement c in doc.GetElementsByTagName("mxCell"))
        {
            if (c.GetAttribute("vertex") != "1") continue;   // flechas y raices: no cuentan
            XmlElement g = c["mxGeometry"];
            if (g == null) continue;
            string id = c.GetAttribute("id");
            var estilo = Estilo(c.GetAttribute("style"));
            string texto = SinHtml(c.GetAttribute("value"));
            var r = new Rect(Num(g, "x"), Num(g, "y"), Num(g, "width"), Num(g, "height"));
            Tipo? t = Clasificar(estilo, out string porque);
            if (t == null)
            {
                desconocidas++;
                Debug.LogWarning(Log + "Forma desconocida (no se usa): id " + id + ", " + porque + (texto != "" ? ", texto \"" + texto + "\"" : ""));
                continue;
            }
            lista.Add(new Forma { id = id, texto = texto, tipo = t.Value, r = r });
        }
        // Orden fijo (por posicion y luego por id): el resultado no depende del orden del archivo.
        return lista.OrderBy(f => f.r.x).ThenBy(f => f.r.y).ThenBy(f => f.id, System.StringComparer.Ordinal).ToList();
    }

    private static Tipo? Clasificar(Dictionary<string, string> e, out string porque)
    {
        porque = "";
        e.TryGetValue("shape", out string forma);
        switch (forma)
        {
            case "cloud": return Tipo.Ventisca;
            case "process": return Tipo.ParedRompe;
            case "trapezoid": return Tipo.Hoguera;
            case "cylinder3": return Tipo.Cofre;
            case "umlActor": return Tipo.Enemigo;
            case "doubleArrow": return Tipo.Corriente;
            case "hexagon": return Tipo.Refugio;
            case null: break;
            default: porque = "forma \"" + forma + "\""; return null;
        }
        if (e.ContainsKey("text")) return Tipo.Texto;
        if (e.ContainsKey("rhombus")) return Tipo.Sello;
        if (e.ContainsKey("triangle")) return Tipo.Estatua;
        if (e.ContainsKey("ellipse")) return Tipo.Monticulo;
        if (!e.ContainsKey("rounded") && !e.ContainsKey("whiteSpace")) { porque = "estilo sin forma conocida"; return null; }
        e.TryGetValue("fillColor", out string color);
        color = (color ?? "").ToLowerInvariant();
        switch (color)
        {
            case "": case "none": case "default": case "#ffffff":
                return e.TryGetValue("dashed", out string d) && d == "1" ? Tipo.Hueco : Tipo.Roca;
            case "#0050ef": return Tipo.Hielo;
            case "#1ba1e2": return Tipo.Agua;
            case "#a20025": return Tipo.ParedFalsa;
        }
        porque = "rectangulo de color " + color;
        return null;
    }

    private static Dictionary<string, string> Estilo(string s)
    {
        var d = new Dictionary<string, string>();
        foreach (string p in (s ?? "").Split(';'))
        {
            if (p.Length == 0) continue;
            int i = p.IndexOf('=');
            if (i < 0) d[p] = "";
            else d[p.Substring(0, i)] = p.Substring(i + 1);
        }
        return d;
    }

    private static float Num(XmlElement g, string a)
    {
        string v = g.GetAttribute(a);
        return string.IsNullOrEmpty(v) ? 0f : float.Parse(v, CultureInfo.InvariantCulture);
    }

    private static string SinHtml(string v)
    {
        v = System.Text.RegularExpressions.Regex.Replace(v ?? "", "<[^>]+>", " ");
        v = v.Replace("&nbsp;", " ").Replace("&amp;", "&");
        return System.Text.RegularExpressions.Regex.Replace(v, @"\s+", " ").Trim();
    }

    // ------------------------------------------------------------------ Escala (dibujo -> mundo)

    private static ExtrasNieve ex;
    public static float WX(float x) => ex.anclaMundo.x + (x - ex.anclaDibujo.x) / ex.dibujoPorCasillaX;
    public static float WY(float y) => ex.anclaMundo.y - (y - ex.anclaDibujo.y) / ex.dibujoPorCasillaY;
    public static int Casilla(float v) => Mathf.FloorToInt(v + 0.5f);
    // Casillas de un rectangulo del dibujo: [x0, x1) y filas [y0, y1).
    private static RectInt Casillas(Rect r)
    {
        int x0 = Casilla(WX(r.xMin)), x1 = Casilla(WX(r.xMax));
        int y1 = Casilla(WY(r.yMin)), y0 = Casilla(WY(r.yMax));
        return new RectInt(x0, y0, Mathf.Max(0, x1 - x0), Mathf.Max(0, y1 - y0));
    }

    // ------------------------------------------------------------------ Ficha Extras (no pisa lo editado)

    public static ExtrasNieve Extras()
    {
        var e = AssetDatabase.LoadAssetAtPath<ExtrasNieve>(RutaExtras);
        if (e != null)
        {
            if (Completar(e)) { EditorUtility.SetDirty(e); AssetDatabase.SaveAssets(); }
            return e;
        }
        e = ScriptableObject.CreateInstance<ExtrasNieve>();
        e.huecos.Add("_KMwPsTR1U-Knyks7b6p-10");     // la sala del escondite 4 (bajo la hoguera 1)
        e.rocasExtra.AddRange(new[]
        {
            R("Pared del principio", -460, -800, 60, 2510, "Cierra el nivel por la izquierda (como en la nieve de siempre)."),
            R("Suelo del élite alargado", 7510, 560, 97, 240, "D1: el salto del último escalón al suelo del élite medía 7,9 u con viento en contra; ahora 5 u."),
            R("Repisa 1 (pared de la hoguera 3)", 8560, 470, 60, 50, "D1: el bloque de la hoguera 3 sube 11 u; se escala por repisas de 2 u."),
            R("Repisa 2", 8650, 370, 60, 50, "D1: escalera de repisas."),
            R("Repisa 3", 8560, 270, 60, 50, "D1: escalera de repisas."),
            R("Repisa 4", 8650, 170, 60, 50, "D1: escalera de repisas."),
            R("Repisa 5", 8560, 70, 60, 50, "D1: escalera de repisas; desde aquí se sube al bloque y a la plataforma del élite."),
            R("Plataforma 15 alargada", 8313, 220, 157, 140, "D1: repisa delante de la pared falsa del escondite 17 (se llega desde la repisa 3)."),
            R("Plataforma 14 alargada", 8455, -120, 75, 140, "D1: acerca la plataforma del élite a la repisa 5."),
        });
        e.interiores.AddRange(new[]
        {
            R("Escondite 4", 520, 540, 310, 230, ""),
            R("Pasillo del atajo (7)", 2001, -40, 369, 280, ""),
            R("Pasillo y arena del minijefe", 3160, 740, 1440, 440, ""),
            R("Escondite 17", 7960, 20, 353, 200, ""),
            R("Cueva del élite", 7960, -420, 750, 980, ""),
        });
        e.hogueras.AddRange(new[]
        {
            new ExtrasNieve.Hoguera { id = "zw7YbKTSLgx7DL8a7zN3-28", nombreLugar = "Paso de la Zanja" },
            new ExtrasNieve.Hoguera { id = "zw7YbKTSLgx7DL8a7zN3-50", nombreLugar = "Bajo el Viento" },
            new ExtrasNieve.Hoguera { id = "zw7YbKTSLgx7DL8a7zN3-53", nombreLugar = "Antesala de la Sombra" },
        });
        e.saltos.AddRange(new[]
        {
            S("Zanja (zona inicial)", 440, 310, 520, 310, true),
            S("Escalón a la hoguera 1", 570, 310, 570, 270, true),
            S("Plataforma alta → bloque de x 3320", 3320, 360, 3320, 320, true),
            S("Bloque de x 3320 → hoguera 2", 4080, 320, 4080, 210, true),
            S("Agua (13)", 5360, 210, 5880, 210, true, "el puente de hielo (paso 5)"),
            S("Orilla → escalón 1", 6270, 210, 6366, 200, true),
            S("Escalón 1 → escalón 2", 6616, 200, 6760, 320, true),
            S("Escalón 2 → escalón 3", 7010, 320, 7120, 480, true),
            S("Escalón 3 → suelo del élite", 7370, 480, 7510, 560, true),
            S("Suelo del élite → repisa 1", 8560, 560, 8560, 470, true),
            S("Repisa 1 → repisa 2", 8620, 470, 8650, 370, true),
            S("Repisa 2 → repisa 3", 8650, 370, 8620, 270, true),
            S("Repisa 3 → repisa 4", 8620, 270, 8650, 170, true),
            S("Repisa 4 → repisa 5", 8650, 170, 8620, 70, true),
            S("Repisa 5 → bloque de la hoguera 3", 8620, 70, 8710, 40, true),
            S("Bloque de la hoguera 3 → arena de la Sombra", 9220, 40, 9220, -80, true),
            S("Camino bajo → plataformas (viento)", 2440, 960, 2730, 550, true, "la corriente de viento (paso 5)"),
            S("Hoguera 1 → camino alto", 1030, 270, 1200, 360, false),
            S("Camino alto → escalón de la arquera", 1680, 360, 1680, 240, false),
            S("Camino alto de vuelta a la hoguera 1", 1200, 360, 1030, 270, false),
            S("Camino bajo → escondite 4 (sobre la caída)", 1160, 840, 1040, 770, false),
            S("Hielo → plataforma de x 2835", 2440, 960, 2835, 1080, false),
            S("Repisa 3 → escondite 17", 8560, 270, 8470, 220, false),
            S("Repisa 5 → plataforma del élite", 8560, 70, 8530, -120, false),
            S("Bloque de la hoguera 3 → plataforma del élite", 8710, 40, 8530, -120, false),
        });
        Completar(e);
        Directory.CreateDirectory(Path.GetDirectoryName(RutaExtras));
        AssetDatabase.CreateAsset(e, RutaExtras);
        AssetDatabase.SaveAssets();
        return e;
    }

    // Rellena las secciones de la ficha que aun estan vacias (las anadidas en cada paso).
    private static bool Completar(ExtrasNieve e)
    {
        bool cambio = false;
        if (e.zonasOcultas.Count == 0)
        {
            e.zonasOcultas.AddRange(new[]
            {
                R("Escondite 4 (tras la pared falsa de x 830)", 520, 540, 310, 230, ""),
                R("Arena del minijefe (tras la pared falsa de x 3400)", 3430, 740, 1170, 440, ""),
                R("Escondite 17 (tras la pared falsa de x 8283)", 7960, 20, 323, 200, ""),
            });
            cambio = true;
        }
        if (e.refugios.Count == 0) { e.refugios.AddRange(new[] { 4950f, 5320f, 6230f, 7300f }); cambio = true; }
        if (e.corrientes.Count == 0)
        {
            e.corrientes.Add(R("Corriente del vacío (9)", 2440, -40, 470, 1800,
                "D3: del techo al fondo del vacío. Sube a las plataformas, al bloque de x 3320 y al lado derecho de la pared (7)."));
            cambio = true;
        }
        const string D = "-DoK03zRAVoyxfRnJ7wk-", Z = "zw7YbKTSLgx7DL8a7zN3-";
        if (e.cofres.Count == 0)
        {
            e.cofres.AddRange(new[]
            {
                new ExtrasNieve.Cofre { id = "_KMwPsTR1U-Knyks7b6p-13", almas = 300 },
                new ExtrasNieve.Cofre { id = D + "3", almas = 500 },
                new ExtrasNieve.Cofre { id = D + "9", mejora = true, objeto = Equipo.Objeto.LagrimaCarmesi },
                new ExtrasNieve.Cofre { id = D + "5", mejora = true, objeto = Equipo.Objeto.FrascoSangre },
            });
            cambio = true;
        }
        if (e.enemigos.Count == 0)
        {
            ExtrasNieve.Enemigo E(string id, string prefab, string porque) => new ExtrasNieve.Enemigo { id = Z + id, prefab = prefab, porque = porque };
            var l = new List<ExtrasNieve.Enemigo>
            {
                E("40", "RataEscarcha", "La primera, sola, en la zona tranquila: enseña a pelear."),
                E("39", "RataEscarcha", "Escondida en un montículo: enseña la emboscada antes de la zanja."),
                E("36", "RataEscarcha", "Cuerpo a cuerpo abajo mientras la arquera dispara desde el escalón (flanquea)."),
                E("37", "ArqueraArcana", "En el escalón alto: combinación con la rata."),
                E("41", "RataEscarcha", "Sale de un montículo al caer al camino bajo."),
                E("42", "HechiceroSombrio", "El duro del tramo: con armadura y sobre el hielo, antes del viento."),
                E("43", "OjoVigia", "Dispara de lejos mientras esperas el viento."),
                E("44", "MurcielagoCumbres", "Picado en arco entre las plataformas."),
                E("45", "MurcielagoCumbres", "Acosa al entrar en la ventisca."),
                E("46", "OjoVigia", "Sobre el lago: dispara mientras lo congelas y avisa a los cercanos."),
                E("47", "MurcielagoCumbres", "Entre los escalones."),
                E("48", "OjoVigia", "Antes del último salto, con viento en contra."),
                E("21", "EliteMorgath", "El élite de la nieve, sobre la plataforma del cofre."),
            };
            l[1].emboscada = true;
            l[2].flanquear = true;
            l[4].emboscada = true;
            l[9].avisar = 2;
            e.enemigos.AddRange(l);
            cambio = true;
        }
        if (e.sellos.Count == 0)
        {
            e.sellos.AddRange(new[]
            {
                new ExtrasNieve.Sello { nombre = "ensenanza", plantilla = "claro", elemento = Elemento.Sagrado, ensenanza = true, pos = new Vector2(700, 770), runa = new Vector2(650, 770) },
                new ExtrasNieve.Sello { nombre = "muro", plantilla = "muro", elemento = Elemento.Fuego, pos = new Vector2(4000, 320), runa = new Vector2(3930, 320) },
                new ExtrasNieve.Sello { nombre = "bloque", plantilla = "atalaya", elemento = Elemento.Fuego, pos = new Vector2(8090, -120), runa = new Vector2(8160, -120) },
                new ExtrasNieve.Sello { nombre = "cripta", plantilla = "cripta", elemento = Elemento.Sagrado, pos = new Vector2(3650, 1080), runa = new Vector2(3600, 1080) },
            });
            cambio = true;
        }
        if (e.estatuas.Count == 0)
        {
            ExtrasNieve.Estatua T(string id, float x, float y) => new ExtrasNieve.Estatua { id = id, pos = new Vector2(x, y) };
            e.estatuas.AddRange(new[]
            {
                T("estatua_1", -300, 310), T("estatua_cripta", 250, 310), T("estatua_claro", 900, 270), T("estatua_2", 1600, 840),
                T("estatua_picadores", 2150, 240), T("estatua_6", 3550, 1080), T("estatua_5", 3880, 320), T("estatua_3", 4250, 210),
                T("estatua_ermitano", 4700, 210), T("estatua_4", 5250, 210), T("estatua_vigias", 7700, 560), T("estatua_7", 9100, 40),
            });
            cambio = true;
        }
        if (e.minijefes.Count == 0)
        {
            e.minijefes.Add(new ExtrasNieve.Minijefe
            {
                nombre = "Martial Hero", clave = "martial", arena = new Rect(3722, 740, 878, 440),
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/Nuevos/JefeMartialHero.prefab"),
            });
            cambio = true;
        }
        if (e.decoracion.Count == 0)
        {
            ExtrasNieve.ZonaDeco Zd(string n, float x, float y, float w, float h, params (string, int)[] piezas) => new ExtrasNieve.ZonaDeco
            {
                nombre = n, dibujo = new Rect(x, y, w, h),
                piezas = piezas.Select(q => new ExtrasNieve.Pieza { nombre = q.Item1, cantidad = q.Item2 }).ToList(),
            };
            e.decoracion.AddRange(new[]
            {
                Zd("Zona inicial (campamento)", -400, 200, 840, 200, ("Tienda", 1), ("Pino", 3), ("PinoFondo", 2), ("Mata", 2), ("Roca", 2), ("hierba helada", 2)),
                Zd("Hoguera 1", 570, 200, 460, 100, ("Pino", 1), ("Mata", 1), ("piedrecillas", 1)),
                Zd("Escondite 4", 520, 700, 310, 100, ("roca nevada", 1)),
                Zd("Camino alto (bosque)", 1200, 150, 830, 250, ("Pino", 2), ("PinoFondo", 2), ("arbusto nevado", 1), ("Mata", 1)),
                Zd("Pasillo del atajo", 2090, 180, 280, 100, ("piedrecillas", 1), ("roca nevada", 1)),
                Zd("Camino bajo helado (cueva)", 1160, 780, 1280, 220, ("Roca", 2), ("roca nevada", 2), ("hierba helada", 1), ("piedrecillas", 1)),
                Zd("Pasillo y arena del minijefe (cripta)", 3160, 1030, 1440, 200, ("Roca", 2), ("lapida vieja", 1), ("piedrecillas", 2)),
                Zd("Hoguera 2 (bajo el viento)", 3320, 150, 1280, 230, ("Pino", 2), ("PinoFondo", 2), ("Mata", 2), ("RocaGrande", 1), ("arbusto nevado", 1)),
                Zd("Ventisca (arboles secos)", 4600, 150, 2870, 360, ("arbol seco", 3), ("roca nevada", 2), ("arbusto nevado", 1), ("hierba helada", 1)),
                Zd("Zona del élite", 7607, 500, 1103, 100, ("Roca", 2), ("arbol seco", 1), ("piedrecillas", 2)),
                Zd("Antesala de la Sombra", 8710, -20, 510, 100, ("PinoFondo", 2), ("lapida vieja", 1), ("arbol seco", 1), ("Mata", 1)),
            });
            cambio = true;
        }
        return cambio;
    }

    private static ExtrasNieve.Recuadro R(string n, float x, float y, float w, float h, string motivo) =>
        new ExtrasNieve.Recuadro { nombre = n, dibujo = new Rect(x, y, w, h), motivo = motivo };
    private static ExtrasNieve.Salto S(string n, float x0, float y0, float x1, float y1, bool principal, string soloCon = "") =>
        new ExtrasNieve.Salto { nombre = n, desde = new Vector2(x0, y0), hasta = new Vector2(x1, y1), principal = principal, soloCon = soloCon };

    // ------------------------------------------------------------------ Rejilla de roca

    private static int X0, X1, Y0, Y1;
    private static bool[,] roca;
    private static List<Forma> formas;
    // Paredes falsas (cuentan como roca para los bordes de las casillas), casillas de hielo
    // y las rocas de los refugios (su x y la superficie en la que estan).
    private static readonly HashSet<Vector2Int> ilusorias = new HashSet<Vector2Int>();
    private static readonly HashSet<Vector2Int> hielo = new HashSet<Vector2Int>();
    private static readonly List<(Forma f, int x0, int x1, int sup)> tramosHielo = new List<(Forma, int, int, int)>();
    private static readonly List<Vector2Int> rocasRefugio = new List<Vector2Int>();

    private static bool RB(int x, int y) => Rk(x, y) || ilusorias.Contains(new Vector2Int(x, y));

    private static bool Rk(int x, int y)
    {
        if (x < X0 || x >= X1 || y < Y0) return true;
        if (y >= Y1) return false;
        return roca[x - X0, y - Y0];
    }

    private static void Poner(RectInt c, bool r)
    {
        for (int x = c.xMin; x < c.xMax; x++)
        for (int y = c.yMin; y < c.yMax; y++)
            if (x >= X0 && x < X1 && y >= Y0 && y < Y1 && x < XArena) roca[x - X0, y - Y0] = r;
    }

    private static void Trazar(Tilemap sueloBase)
    {
        var rocas = formas.Where(f => f.tipo == Tipo.Roca && !ex.huecos.Contains(f.id)).ToList();
        var huecos = formas.Where(f => f.tipo == Tipo.Hueco || (f.tipo == Tipo.Roca && ex.huecos.Contains(f.id))).ToList();
        var todas = rocas.Select(f => Casillas(f.r)).Concat(ex.rocasExtra.Select(e => Casillas(e.dibujo))).ToList();
        X0 = Mathf.Min(todas.Min(c => c.xMin), XArena);
        X1 = 410;
        Y0 = todas.Min(c => c.yMin) - 3;
        Y1 = 32;
        roca = new bool[X1 - X0, Y1 - Y0];

        // La arena de la Sombra: tal cual estaba (y roca debajo de lo pintado).
        for (int x = XArena; x < X1; x++)
        for (int y = Y0; y < Y1; y++)
            roca[x - X0, y - Y0] = y < YPintadoBase || sueloBase.HasTile(new Vector3Int(x, y, 0));

        foreach (Forma f in rocas)
        {
            RectInt c = Casillas(f.r);
            if (c.width == 0 || c.height == 0) Debug.LogWarning(Log + "El bloque " + f.id + " queda sin casillas con esta escala.");
            Poner(c, true);
            if (BajaAlFondo(f, rocas)) Poner(new RectInt(c.xMin, Y0, c.width, c.yMin - Y0), true);
        }
        // Las rocas extra siguen la misma regla: si son gruesas y no hay nada debajo, bajan al fondo.
        foreach (var e in ex.rocasExtra)
        {
            RectInt c = Casillas(e.dibujo);
            Poner(c, true);
            if (BajaAlFondo(new Forma { id = e.nombre, r = e.dibujo }, rocas)) Poner(new RectInt(c.xMin, Y0, c.width, c.yMin - Y0), true);
        }
        foreach (Forma f in huecos) Poner(Casillas(f.r), false);

        // Paredes falsas: aire que se ve como roca.
        ilusorias.Clear();
        foreach (Forma f in formas.Where(f => f.tipo == Tipo.ParedFalsa))
        {
            RectInt c = Casillas(f.r);
            Poner(c, false);
            for (int x = c.xMin; x < c.xMax; x++) for (int y = c.yMin; y < c.yMax; y++) ilusorias.Add(new Vector2Int(x, y));
        }

        // Refugios de la ventisca: una roca de 1 x 2 sobre el suelo.
        rocasRefugio.Clear();
        foreach (float rx in ex.refugios)
        {
            int x = Casilla(WX(rx) - 0.5f);
            int sup = Superficie(x, Y1 - 1);
            if (sup == int.MinValue) { Debug.LogWarning(Log + "Refugio sin suelo en x " + rx); continue; }
            Poner(new RectInt(x, sup, 1, 2), true);
            rocasRefugio.Add(new Vector2Int(x, sup));
        }

        // Piso resbaladizo: la casilla de arriba del suelo que hay bajo cada franja azul.
        hielo.Clear();
        tramosHielo.Clear();
        foreach (Forma f in formas.Where(f => f.tipo == Tipo.Hielo))
        {
            RectInt c = Casillas(f.r);
            int cerca = Casilla(WY(f.r.yMin));
            int x0 = int.MaxValue, x1 = int.MinValue, supTramo = 0;
            for (int x = c.xMin; x < Mathf.Max(c.xMax, c.xMin + 1); x++)
            {
                int sup = Superficie(x, cerca + 2);
                if (sup == int.MinValue || Mathf.Abs(sup - cerca) > 2) continue;
                hielo.Add(new Vector2Int(x, sup - 1));
                x0 = Mathf.Min(x0, x); x1 = Mathf.Max(x1, x + 1); supTramo = sup;
            }
            if (x0 < x1) tramosHielo.Add((f, x0, x1, supTramo));
            else Debug.LogWarning(Log + "La franja de hielo " + f.id + " no tiene suelo debajo.");
        }
    }

    // Un bloque grueso sin nada debajo es suelo de la montaña: baja hasta el fondo.
    private static bool BajaAlFondo(Forma f, List<Forma> rocas)
    {
        if (ex.bajanAlFondo.Contains(f.id)) return true;
        if (ex.noBajanAlFondo.Contains(f.id)) return false;
        if (f.r.height < 150f) return false;
        foreach (Forma o in rocas)
        {
            if (o == f) continue;
            bool solapa = Mathf.Min(o.r.xMax, f.r.xMax) - Mathf.Max(o.r.xMin, f.r.xMin) > 0.5f;
            if (solapa && o.r.yMin >= f.r.yMax - 0.5f) return false;
        }
        return true;
    }

    // Superficie (primera casilla libre sobre roca) bajando desde "desde".
    private static int Superficie(int x, int desde)
    {
        for (int y = desde; y > Y0; y--)
            if (Rk(x, y - 1) && !Rk(x, y)) return y;
        return int.MinValue;
    }

    // ------------------------------------------------------------------ Generar

    private static Scene escena;
    private static Transform nivel, raiz;
    private static Sprite blanco, rocaSprite, monticuloSprite;
    private static TileBase reglaParedFalsa, tileSombra;

    // Lo que se toma de la base antes de vaciarla.
    private static void Recursos()
    {
        reglaParedFalsa = AssetDatabase.LoadAssetAtPath<TileBase>(Tiles + "Nieve (auto).asset");
        Tilemap zo = nivel.Find("Terreno/ZonasOcultas").GetComponent<Tilemap>();
        tileSombra = null;
        foreach (Vector3Int c in zo.cellBounds.allPositionsWithin) { tileSombra = zo.GetTile(c); if (tileSombra != null) break; }
        rocaSprite = monticuloSprite = null;
        GameObject deco = Raiz("DecoracionExtra");
        if (deco != null)
            foreach (SpriteRenderer sr in deco.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (monticuloSprite == null && sr.name == "monticulo de nieve") monticuloSprite = sr.sprite;
                if (rocaSprite == null && sr.name == "roca nevada") rocaSprite = sr.sprite;
            }
        // El monticulo de nieve: el de las emboscadas de la base.
        if (monticuloSprite == null)
            monticuloSprite = Object.FindObjectsByType<MonticuloEmboscada>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(m => m.monticulo != null && m.monticulo.sprite != null).Select(m => m.monticulo.sprite).FirstOrDefault();
        if (monticuloSprite == null) Debug.LogWarning(Log + "No encuentro el sprite del montículo en la base.");
        if (tileSombra == null) Debug.LogWarning(Log + "No encuentro el tile de sombra (zonas ocultas) en la base.");
    }

    public static void Generar()
    {
        if (!File.Exists(RutaBase))
        {
            Debug.LogWarning(Log + "Falta la base " + RutaBase + (File.Exists(RutaDiagramaGuardada) ? " (ahora se juega la Ronda 19: usa \"Usar la nieve del diagrama\")." : "."));
            return;
        }
        ex = Extras();
        if (!File.Exists(ex.rutaDiagrama)) { Debug.LogWarning(Log + "No encuentro el dibujo: " + ex.rutaDiagrama); return; }
        formas = LeerDiagrama(ex.rutaDiagrama, out int desconocidas);
        Debug.Log(Log + "Dibujo leido: " + formas.Count + " formas (" + string.Join(", ", formas.GroupBy(f => f.tipo).OrderBy(g => g.Key).Select(g => g.Key + " " + g.Count())) + "), " + desconocidas + " desconocidas.");

        Scene b = EditorSceneManager.OpenScene(RutaBase, OpenSceneMode.Single);
        EditorSceneManager.SaveScene(b, RutaActiva, true);
        escena = EditorSceneManager.OpenScene(RutaActiva, OpenSceneMode.Single);
        nivel = Raiz("Nivel").transform;
        blanco = AssetDatabase.LoadAllAssetsAtPath(Tiles + "Blanco.png").OfType<Sprite>().FirstOrDefault();

        Recursos();
        ApartarPlantillas();
        Limpiar();
        raiz = new GameObject("NieveDiagrama").transform;
        Tilemap suelo = nivel.Find("Terreno/Suelo").GetComponent<Tilemap>();
        Trazar(suelo);
        Terreno();
        Huecos();
        Mecanicas();
        Hogueras();
        Inicio();
        Contenido();
        Minijefes();
        Decorar();
        Object.DestroyImmediate(plantillas.gameObject);

        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
        AsegurarEnBuild();
        Debug.Log(Log + "Nivel generado en " + RutaActiva + $" (x {X0}..{X1}, y {Y0}..{Y1}).");
    }

    private static GameObject Raiz(string n) => escena.GetRootGameObjects().FirstOrDefault(g => g.name == n);

    private static void Destruir(Transform t) { if (t != null) Object.DestroyImmediate(t.gameObject); }
    private static void Vaciar(Transform t) { if (t != null) foreach (Transform h in t.Cast<Transform>().ToList()) Object.DestroyImmediate(h.gameObject); }

    // Fuera lo del trazado de la Ronda 19 (se rehace desde el dibujo). Se quedan el
    // fondo, el ambiente, la camara, la interfaz, la arena, el portal y las hogueras.
    private static void Limpiar()
    {
        Destruir(Raiz("NieveNueva")?.transform);
        Vaciar(Raiz("DecoracionExtra")?.transform);
        Vaciar(Raiz("Estatuas")?.transform);
        Vaciar(nivel.Find("Decoracion"));
        Vaciar(nivel.Find("Obstaculos"));
        Vaciar(nivel.Find("Enemigos"));
        foreach (string n in new[] { "CofreMejora_mejora_1", "BrillosHielo", "RellenoProfundo" }) Destruir(nivel.Find(n));
        Transform zonas = nivel.Find("ZonasCamara");
        if (zonas != null) foreach (Transform z in zonas.Cast<Transform>().ToList()) if (z.name != "Zona_Arena") Destruir(z);

        // Casillas: todo lo que queda a la izquierda de la arena.
        foreach (Tilemap tm in nivel.Find("Terreno").GetComponentsInChildren<Tilemap>(true))
        {
            BoundsInt bb = tm.cellBounds;
            for (int x = bb.xMin; x < Mathf.Min(bb.xMax, XArena); x++)
            for (int y = bb.yMin; y < bb.yMax; y++)
                tm.SetTile(new Vector3Int(x, y, 0), null);
        }
    }

    private static readonly string[] Nombres9 = { "esq_ai", "arriba", "esq_ad", "izq", "centro", "der", "esq_bi", "abajo", "esq_bd" };

    private static void Terreno()
    {
        Transform t = nivel.Find("Terreno");
        Tilemap suelo = t.Find("Suelo").GetComponent<Tilemap>();
        var nieve = Nombres9.ToDictionary(n => n, n => AssetDatabase.LoadAssetAtPath<TileBase>(Tiles + "nieve_" + n + ".asset"));
        var hie = Nombres9.ToDictionary(n => n, n => AssetDatabase.LoadAssetAtPath<TileBase>(Tiles + "hielo_" + n + ".asset"));
        for (int x = X0; x < X1; x++)
        for (int y = Y0; y < Y1; y++)
        {
            // La arena se pinta solo en su borde izquierdo (lo toca el bloque nuevo) y debajo de lo pintado.
            if (x > XArena && y >= YPintadoBase) continue;
            Vector3Int p = new Vector3Int(x, y, 0);
            if (!Rk(x, y)) { if (x <= XArena) suelo.SetTile(p, null); continue; }
            bool arriba = !RB(x, y + 1), abajo = !RB(x, y - 1), izq = !RB(x - 1, y), der = !RB(x + 1, y);
            string n = arriba ? (izq ? "esq_ai" : der ? "esq_ad" : "arriba")
                     : abajo ? (izq ? "esq_bi" : der ? "esq_bd" : "abajo")
                     : izq && der ? "der" : izq ? "izq" : der ? "der" : "centro";
            suelo.SetTile(p, hielo.Contains(new Vector2Int(x, y)) ? hie[n] : nieve[n]);
        }
        PaletaNieve.ActualizarColision(suelo);

        // Paredes falsas (se ven como la roca de al lado y se atraviesan) y salas a oscuras tras ellas.
        Tilemap paredes = t.Find("ParedesFalsas").GetComponent<Tilemap>();
        foreach (Vector2Int c in ilusorias) paredes.SetTile(new Vector3Int(c.x, c.y, 0), reglaParedFalsa);
        Tilemap zonas = t.Find("ZonasOcultas").GetComponent<Tilemap>();
        if (tileSombra != null)
            foreach (var z in ex.zonasOcultas)
            {
                RectInt c = Casillas(z.dibujo);
                for (int x = c.xMin; x < c.xMax; x++)
                for (int y = c.yMin; y < c.yMax; y++)
                    if (x < XArena && (!Rk(x, y) || ilusorias.Contains(new Vector2Int(x, y)))) zonas.SetTile(new Vector3Int(x, y, 0), tileSombra);
            }

        // Pared de fondo de las salas cerradas (tapa el cielo del fondo).
        Tilemap fondo = t.Find("ParedFondo")?.GetComponent<Tilemap>();
        if (fondo != null)
            foreach (var i in ex.interiores)
            {
                RectInt c = Casillas(i.dibujo);
                for (int x = c.xMin; x < c.xMax; x++)
                for (int y = c.yMin; y < c.yMax; y++)
                    if (x < XArena && !Rk(x, y)) fondo.SetTile(new Vector3Int(x, y, 0), nieve["centro"]);
            }
        // Zonas bajas (bajo la montana): el fondo no llega tan abajo, asi que pared de cueva.
        int yCueva = Casilla(WY(ex.paredFondoBajoY));
        if (fondo != null)
            for (int x = X0; x < XArena; x++)
            for (int y = Y0; y < yCueva; y++)
                if (!Rk(x, y)) fondo.SetTile(new Vector3Int(x, y, 0), nieve["centro"]);

        // Relleno oscuro bajo el fondo del nivel.
        Transform relleno = new GameObject("RellenoProfundo").transform;
        relleno.SetParent(nivel, false);
        SpriteRenderer sr = Pieza(relleno, "Relleno", blanco, new Vector2((X0 + X1) * 0.5f, Y0 - 6f), new Vector2(X1 - X0 + 20f, 12f), "Ground", -1);
        sr.color = new Color(0.12f, 0.1f, 0.09f);
    }

    // Huecos mortales: cada tramo de columnas sin suelo hasta el fondo. Muerte al fondo
    // (reaparicion en la ultima hoguera) y la niebla/velo de GrietaMortal.
    private static readonly List<(int x0, int x1, int boca)> tramos = new List<(int, int, int)>();

    private static void Huecos()
    {
        Transform padre = new GameObject("Huecos").transform;
        padre.SetParent(raiz, false);
        int items = LayerMask.NameToLayer("Items");
        tramos.Clear();
        int x = X0;
        while (x < XArena)
        {
            if (Rk(x, Y0)) { x++; continue; }
            int a = x;
            while (x < XArena && !Rk(x, Y0)) x++;
            int boca = Mathf.Min(TopeSuelo(a - 1), TopeSuelo(x));
            tramos.Add((a, x, boca));
        }
        foreach (var (a, b, boca) in tramos)
        {
            float cx = (a + b) * 0.5f, w = b - a;
            Forma agua = formas.FirstOrDefault(f => f.tipo == Tipo.Agua && Mathf.Abs(WX(f.r.center.x) - cx) < w * 0.5f);
            GameObject go = new GameObject(agua != null ? "FondoDelAgua_" + a : "Hueco_" + a);
            go.transform.SetParent(padre, false);
            go.transform.position = new Vector3(cx, boca, 0f);
            if (agua == null)
            {
                GrietaMortal gm = go.AddComponent<GrietaMortal>();
                gm.ancho = w;
                gm.fondo = boca - Y0;
                gm.colorNiebla = new Color(0.85f, 0.92f, 1f, 0.3f);
                gm.colorVacio = new Color(0.03f, 0.04f, 0.08f, 0.95f);
            }
            GameObject muerte = new GameObject("Muerte");
            muerte.layer = items;
            muerte.transform.SetParent(go.transform, false);
            muerte.transform.position = new Vector3(cx, Y0 + 0.7f, 0f);
            BoxCollider2D bc = muerte.AddComponent<BoxCollider2D>();
            bc.isTrigger = true;
            bc.size = new Vector2(w, 1.4f);
            muerte.AddComponent<DeadArea>();
        }

        // La caida mortal general: debajo de todo el nivel.
        Transform caida = nivel.Find("Trampas/CaidaMortal");
        BoxCollider2D cb = caida != null ? caida.GetComponent<BoxCollider2D>() : null;
        if (cb != null)
        {
            caida.position = new Vector3((X0 + X1) * 0.5f, Y0 - 2f, 0f);
            cb.offset = Vector2.zero;
            cb.size = new Vector2((X1 - X0 + 40f) / Mathf.Max(0.01f, caida.lossyScale.x), 2f / Mathf.Max(0.01f, caida.lossyScale.y));
        }
        else Debug.LogWarning(Log + "No encuentro Trampas/CaidaMortal con BoxCollider2D.");
        Debug.Log(Log + "Huecos mortales: " + string.Join(", ", tramos.Select(t => $"x {t.x0}-{t.x1}")));
    }

    // ------------------------------------------------------------------ Contenido (paso 6)

    private static Transform plantillas;

    // Antes de vaciar la base: aparta lo que se reutiliza (sellos, cofres, estatuas y decoracion).
    private static void ApartarPlantillas()
    {
        plantillas = new GameObject("_Plantillas").transform;
        plantillas.gameObject.SetActive(false);
        foreach (SelloElemental s in Object.FindObjectsByType<SelloElemental>(FindObjectsInactive.Include, FindObjectsSortMode.None).OrderBy(s => s.clave, System.StringComparer.Ordinal))
        {
            s.gameObject.name = "Sello_" + s.clave;
            s.transform.SetParent(plantillas, true);
        }
        CofreAlmas ca = nivel.GetComponentsInChildren<CofreAlmas>(true).FirstOrDefault(c => c.transform.parent != null && c.transform.parent.name == "Obstaculos");
        if (ca != null) { ca.name = "CofreAlmas"; ca.transform.SetParent(plantillas, true); }
        Transform cm = nivel.Find("CofreMejora_mejora_1");
        if (cm != null) { cm.name = "CofreMejora"; cm.SetParent(plantillas, true); }
        Transform est = new GameObject("Estatuas").transform;
        est.SetParent(plantillas, false);
        foreach (EstatuaPista e in Object.FindObjectsByType<EstatuaPista>(FindObjectsInactive.Include, FindObjectsSortMode.None).OrderBy(e => e.id, System.StringComparer.Ordinal))
            e.transform.SetParent(est, true);
        Transform deco = new GameObject("Decoracion").transform;
        deco.SetParent(plantillas, false);
        var piezas = new List<Transform>();
        Transform d1 = nivel.Find("Decoracion");
        if (d1 != null) piezas.AddRange(d1.Cast<Transform>());
        GameObject d2 = Raiz("DecoracionExtra");
        if (d2 != null) piezas.AddRange(d2.transform.Cast<Transform>());
        foreach (Transform p in piezas.ToList()) p.SetParent(deco, true);

        // Inventario de la decoracion de la base (para colocarla en el paso 7).
        var inv = new StringBuilder("Decoracion de la base:\n");
        foreach (var g in deco.Cast<Transform>().GroupBy(t => t.name).OrderBy(g => g.Key))
        {
            Transform t = g.First();
            Bounds bb = t.GetComponentsInChildren<SpriteRenderer>(true).Select(r => r.bounds).Aggregate((x, y) => { x.Encapsulate(y); return x; });
            inv.AppendLine($"  {g.Key} x{g.Count()}: {bb.size.x:0.0} x {bb.size.y:0.0} u, colisiones: {t.GetComponentsInChildren<Collider2D>(true).Count(c => !c.isTrigger)}, capa {t.GetComponentInChildren<SpriteRenderer>(true).sortingLayerName}");
        }
        foreach (string n in new[] { "Terreno/Decoracion", "Terreno/DecoracionFondo" })
        {
            Tilemap tm = nivel.Find(n)?.GetComponent<Tilemap>();
            if (tm == null) continue;
            var cuenta = new Dictionary<string, int>();
            foreach (Vector3Int c in tm.cellBounds.allPositionsWithin) { TileBase tb = tm.GetTile(c); if (tb != null) cuenta[tb.name] = cuenta.TryGetValue(tb.name, out int k) ? k + 1 : 1; }
            inv.AppendLine("  Tiles en " + n + ": " + string.Join(", ", cuenta.OrderBy(k => k.Key).Select(k => k.Key + " x" + k.Value)));
        }
        inventarioDeco = inv.ToString();
    }

    public static string inventarioDeco = "";

    private static void Contenido()
    {
        Cofres();
        Enemigos();
        Sellos();
        Estatuas();
    }

    private static int SueloBajo(Forma f) => Superficie(Casilla(WX(f.r.center.x) - 0.5f), Casilla(WY(f.r.yMax)) + 2);
    private static int SueloCerca(Vector2 dibujo) => Superficie(Casilla(WX(dibujo.x) - 0.5f), Casilla(WY(dibujo.y)) + 2);

    private static void Cofres()
    {
        Transform padre = new GameObject("Cofres").transform;
        padre.SetParent(raiz, false);
        GameObject almas = plantillas.Find("CofreAlmas")?.gameObject, mejora = plantillas.Find("CofreMejora")?.gameObject;
        foreach (Forma f in formas.Where(f => f.tipo == Tipo.Cofre))
        {
            var info = ex.cofres.FirstOrDefault(c => c.id == f.id);
            if (info == null) { Debug.LogWarning(Log + "El cofre " + f.id + " no tiene contenido en Extras Nieve: no se pone."); continue; }
            GameObject p = info.mejora ? mejora : almas;
            if (p == null) { Debug.LogWarning(Log + "Falta la plantilla de cofre en la base."); continue; }
            GameObject go = Object.Instantiate(p, padre);
            go.name = (info.mejora ? "CofreMejora_" : "CofreAlmas_") + f.id;
            go.transform.position = new Vector3(WX(f.r.center.x), SueloBajo(f), 0f);
            if (info.mejora)
            {
                go.GetComponent<CofreMejora>().Configurar(Clave(f), info.objeto, info.extra ?? new Equipo.Objeto[0], 0);
                EditorUtility.SetDirty(go.GetComponent<CofreMejora>());
            }
            else
            {
                var so = new SerializedObject(go.GetComponent<CofreAlmas>());
                so.FindProperty("clave").stringValue = Clave(f);
                so.FindProperty("almas").intValue = info.almas;
                so.FindProperty("darFrasco").boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }

    private static readonly string[] Voladores = { "MurcielagoCumbres", "OjoVigia" };

    private static void Enemigos()
    {
        Transform ene = nivel.Find("Enemigos");
        foreach (Forma f in formas.Where(f => f.tipo == Tipo.Enemigo))
        {
            var info = ex.enemigos.FirstOrDefault(e => e.id == f.id);
            string prefab = info?.prefab;
            if (info == null)
            {
                string t = f.texto.ToLowerInvariant();
                prefab = t.Contains("elite") ? "EliteMorgath" : t.Contains("volador") ? "MurcielagoCumbres" : t.Contains("arquero") ? "ArqueraArcana" : t.Contains("cuerpo a cuerpo") ? "RataEscarcha" : null;
                Debug.LogWarning(Log + "El enemigo " + f.id + " (\"" + f.texto + "\") no esta en Extras Nieve" + (prefab != null ? ": se usa " + prefab + " (leyenda)." : " y su texto no esta en la leyenda: no se pone."));
                if (prefab == null) continue;
            }
            GameObject pre = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/Nieve/" + prefab + ".prefab")
                             ?? AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/Elites/" + prefab + ".prefab");
            if (pre == null) { Debug.LogWarning(Log + "Falta el prefab " + prefab); continue; }
            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(pre, ene);
            go.name = prefab + "_" + f.id;
            if (Voladores.Contains(prefab)) go.transform.position = new Vector3(WX(f.r.center.x), WY(f.r.center.y), 0f);
            else go.transform.position = new Vector3(WX(f.r.center.x), SueloBajo(f) + 0.02f, 0f);
            if (info == null) continue;
            if (info.flanquear || info.retirarse)
            {
                EnemigoRata rata = go.GetComponent<EnemigoRata>();
                if (rata != null)
                {
                    var so = new SerializedObject(rata);
                    so.FindProperty("flanquear").boolValue = info.flanquear;
                    so.FindProperty("retirarse").boolValue = info.retirarse;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            if (info.avisar > 0)
            {
                var so = new SerializedObject(go.GetComponent<EnemigoBase>());
                so.FindProperty("avisarCercanos").intValue = info.avisar;
                so.FindProperty("radioAviso").floatValue = 20f;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            if (info.emboscada) Emboscada(go);
        }
    }

    // Montículo de nieve a los pies del enemigo: tiembla y suelta nieve antes de que salga.
    private static void Emboscada(GameObject enemigo)
    {
        MonticuloEmboscada m = enemigo.GetComponent<MonticuloEmboscada>() ?? enemigo.AddComponent<MonticuloEmboscada>();
        SpriteRenderer sr = new GameObject("Monticulo").AddComponent<SpriteRenderer>();
        sr.transform.SetParent(enemigo.transform, false);
        sr.sprite = monticuloSprite != null ? monticuloSprite : blanco;
        sr.sortingLayerName = "Middleground";
        sr.sortingOrder = 6;
        float esc = 1.5f / Mathf.Max(0.01f, sr.sprite.bounds.size.x);
        sr.transform.localScale = new Vector3(esc / Mathf.Max(0.01f, enemigo.transform.lossyScale.x), esc / Mathf.Max(0.01f, enemigo.transform.lossyScale.y), 1f);
        float pie = enemigo.transform.position.y - 0.02f;
        sr.transform.position = new Vector3(enemigo.transform.position.x, pie, 0f);
        sr.transform.position += Vector3.up * (pie - sr.bounds.min.y - 0.03f);
        m.monticulo = sr;
    }

    private static void Sellos()
    {
        Transform padre = new GameObject("Sellos").transform;
        padre.SetParent(raiz, false);
        foreach (var s in ex.sellos)
        {
            Transform p = plantillas.Find("Sello_" + s.plantilla);
            if (p == null) { Debug.LogWarning(Log + "No hay sello \"" + s.plantilla + "\" en la base."); continue; }
            int x = Casilla(WX(s.pos.x) - 0.5f);
            int suelo = Superficie(x, Casilla(WY(s.pos.y)) + 2);
            int techo = suelo;
            while (techo < Y1 && !Rk(x, techo)) techo++;
            GameObject go = Object.Instantiate(p.gameObject, padre);
            go.name = "Sello_" + s.nombre;
            SelloElemental se = go.GetComponent<SelloElemental>();
            Encajar(go, se, WX(s.pos.x), suelo, techo - suelo);
            se.clave = "dx_" + s.nombre;
            se.elemento = s.elemento;
            se.ensenanza = s.ensenanza;
            int sr = SueloCerca(s.runa);
            se.simbolos = new[]
            {
                Simbolo(padre, s.elemento, new Vector2(WX(s.runa.x), sr), true),
                Brillo(padre, s.elemento, new Vector2(WX(s.pos.x), suelo + 0.03f), 1.6f),
            };
            EditorUtility.SetDirty(se);
        }
        // El lago es el sello de la escarcha: su runa y su brillo en la orilla.
        Forma agua = formas.FirstOrDefault(f => f.tipo == Tipo.Agua);
        if (agua != null)
        {
            int sl = SueloCerca(ex.runaLago);
            Simbolo(padre, Elemento.Hielo, new Vector2(WX(ex.runaLago.x), sl), true);
            Brillo(padre, Elemento.Hielo, new Vector2(Casillas(agua.r).xMin - 0.7f, sl + 0.03f), 1.4f);
        }
    }

    // Coloca el sello del suelo al techo en x. Las sombras se estiran; el hielo se apila
    // en bloques (asi no se deforma).
    private static void Encajar(GameObject go, SelloElemental se, float cx, int suelo, int alto)
    {
        BoxCollider2D bc = se.solido as BoxCollider2D;
        if (bc == null) { Debug.LogWarning(Log + go.name + ": el sello no tiene BoxCollider2D."); return; }
        Physics2D.SyncTransforms();
        Bounds b0 = bc.bounds;
        go.transform.position += new Vector3(cx - b0.center.x, suelo - b0.min.y, 0f);
        Physics2D.SyncTransforms();
        Bounds b = bc.bounds;
        float h0 = b.size.y;
        var sprites = go.GetComponentsInChildren<SpriteRenderer>(true).ToList();
        if (se.estilo == SelloElemental.Estilo.Hielo)
        {
            int copias = Mathf.Max(1, Mathf.RoundToInt(alto / h0));
            for (int i = 1; i < copias; i++)
                foreach (SpriteRenderer sr in sprites)
                {
                    if (sr.transform == go.transform) continue;
                    SpriteRenderer c = Object.Instantiate(sr, sr.transform.parent);
                    c.transform.position = sr.transform.position + Vector3.up * h0 * i;
                }
            h0 *= copias;
            sprites = go.GetComponentsInChildren<SpriteRenderer>(true).ToList();
        }
        float k = alto / h0;
        foreach (SpriteRenderer sr in sprites)
        {
            Vector3 w = sr.transform.position;
            sr.transform.position = new Vector3(w.x, suelo + (w.y - suelo) * k, w.z);
            Vector3 ls = sr.transform.localScale;
            sr.transform.localScale = new Vector3(ls.x, ls.y * k, ls.z);
        }
        float esc = Mathf.Max(0.01f, bc.transform.lossyScale.y);
        bc.size = new Vector2(bc.size.x, alto / esc);
        bc.offset = new Vector2(bc.offset.x, (suelo + alto * 0.5f - bc.transform.position.y) / esc);
        se.visuales = sprites.ToArray();
    }

    private const string RutaPistas = "Assets/Resources/PistasSellos.asset";

    // Runa grabada sobre el suelo, con un halo suave (como en la nieve Ronda 19).
    private static SimboloSello Simbolo(Transform padre, Elemento e, Vector2 pos, bool enSuelo)
    {
        PistasSellos.Entrada d = AssetDatabase.LoadAssetAtPath<PistasSellos>(RutaPistas).De(e);
        GameObject go = new GameObject("Runa_" + e);
        go.transform.SetParent(padre, false);
        SpriteRenderer sr = Pieza(go.transform, "Runa", d.runa != null ? d.runa : blanco, pos, Vector2.zero, "Ground", 7);
        float esc = 0.62f / Mathf.Max(0.01f, sr.sprite.bounds.size.x);
        sr.transform.localScale = new Vector3(esc, esc, 1f);
        if (enSuelo) sr.transform.position = new Vector3(pos.x, pos.y + sr.bounds.extents.y - 0.04f, 0f);
        Sprite resplandor = AssetDatabase.LoadAssetAtPath<Sprite>(Tiles + "Resplandor.png");
        SpriteRenderer halo = Pieza(go.transform, "Halo", resplandor != null ? resplandor : blanco, sr.transform.position, Vector2.zero, "Ground", 6);
        halo.transform.localScale = Vector3.one * 1.3f;
        halo.color = new Color(d.color.r, d.color.g, d.color.b, 0.35f);
        SimboloSello s = go.AddComponent<SimboloSello>();
        s.elemento = e;
        s.piezas = new[] { sr, halo };
        return s;
    }

    // Franja fina del color del elemento en el suelo.
    private static SimboloSello Brillo(Transform padre, Elemento e, Vector2 pos, float ancho)
    {
        PistasSellos.Entrada d = AssetDatabase.LoadAssetAtPath<PistasSellos>(RutaPistas).De(e);
        GameObject go = new GameObject("Brillo_" + e);
        go.transform.SetParent(padre, false);
        SpriteRenderer sr = Pieza(go.transform, "Franja", blanco, pos, new Vector2(ancho, 0.08f), "Ground", 5);
        sr.color = new Color(d.color.r, d.color.g, d.color.b, 0.7f);
        SimboloSello s = go.AddComponent<SimboloSello>();
        s.elemento = e;
        s.piezas = new[] { sr };
        return s;
    }

    // Las estatuas de siempre (mismos textos), en su sitio nuevo.
    private static void Estatuas()
    {
        Transform destino = Raiz("Estatuas").transform;
        Transform origen = plantillas.Find("Estatuas");
        foreach (var e in ex.estatuas)
        {
            EstatuaPista ep = origen.GetComponentsInChildren<EstatuaPista>(true).FirstOrDefault(x => x.id == e.id);
            if (ep == null) { Debug.LogWarning(Log + "No hay estatua " + e.id + " en la base."); continue; }
            ep.transform.SetParent(destino, true);
            ep.name = "Estatua_" + e.id;
            ep.transform.position = new Vector3(WX(e.pos.x), SueloCerca(e.pos) + 1.3f, ep.transform.position.z);
        }
        foreach (EstatuaPista sobra in origen.GetComponentsInChildren<EstatuaPista>(true))
            Debug.LogWarning(Log + "La estatua " + sobra.id + " no tiene sitio en Extras Nieve: se quita.");
    }

    // ------------------------------------------------------------------ Decoracion (paso 7)

    // Lo que la decoracion nunca tapa: centro y radio.
    private static List<(Vector2 p, float r)> Respetar()
    {
        var l = new List<(Vector2, float)>();
        foreach (EnemigoBase e in nivel.Find("Enemigos").GetComponentsInChildren<EnemigoBase>(true)) l.Add((e.transform.position, 1.6f));
        foreach (CofreAlmas c in Object.FindObjectsByType<CofreAlmas>(FindObjectsSortMode.None)) l.Add((c.transform.position, 2f));
        foreach (CofreMejora c in Object.FindObjectsByType<CofreMejora>(FindObjectsSortMode.None)) l.Add((c.transform.position, 2f));
        foreach (Hoguera h in nivel.Find("Hogueras").GetComponentsInChildren<Hoguera>(true)) l.Add((h.transform.position, 2.5f));
        foreach (EstatuaPista e in Object.FindObjectsByType<EstatuaPista>(FindObjectsSortMode.None)) l.Add(((Vector2)e.transform.position - new Vector2(0f, 1.3f), 1.5f));
        foreach (SelloElemental s in Object.FindObjectsByType<SelloElemental>(FindObjectsSortMode.None)) l.Add((new Vector2(s.solido.bounds.center.x, s.solido.bounds.min.y), 2.5f));
        foreach (SimboloSello s in Object.FindObjectsByType<SimboloSello>(FindObjectsSortMode.None)) l.Add((s.GetComponentInChildren<SpriteRenderer>().transform.position, 1.2f));
        foreach (DerrumbeAtajo d in Object.FindObjectsByType<DerrumbeAtajo>(FindObjectsSortMode.None)) l.Add((new Vector2(d.transform.position.x, d.GetComponent<Collider2D>().bounds.min.y), 3f));
        foreach (Vector2Int r in rocasRefugio) l.Add((new Vector2(r.x - 1f, r.y), 3f));
        foreach (Vector2Int c in ilusorias) l.Add((new Vector2(c.x + 0.5f, c.y), 3f));
        foreach (AguaCongelable a in Object.FindObjectsByType<AguaCongelable>(FindObjectsSortMode.None))
        {
            Bounds b = a.GetComponent<Collider2D>().bounds;
            l.Add((new Vector2(b.min.x, b.min.y), 1.5f));
            l.Add((new Vector2(b.max.x, b.min.y), 1.5f));
        }
        GameObject pl = escena.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).Select(t => t.gameObject).FirstOrDefault(g => g.CompareTag("Player"));
        if (pl != null) l.Add((pl.transform.position, 1f));
        return l;
    }

    // Suelo plano y con sitio: ninguna casilla de borde (ni hueco) a menos de "margen" del centro.
    private static bool SueloLlano(int x, int sup, float medioAncho, float alto)
    {
        int m = Mathf.CeilToInt(medioAncho + 1.2f);
        for (int i = x - m; i <= x + m; i++)
        {
            if (!Rk(i, sup - 1) || Rk(i, sup)) return false;
            if (ilusorias.Contains(new Vector2Int(i, sup - 1))) return false;
        }
        int h = Mathf.CeilToInt(alto);
        for (int i = x - Mathf.CeilToInt(medioAncho); i <= x + Mathf.CeilToInt(medioAncho); i++)
            for (int j = sup; j < sup + h; j++) if (Rk(i, j) || ilusorias.Contains(new Vector2Int(i, j))) return false;
        return true;
    }

    private static readonly List<(string zona, string pieza, Vector2 pos)> decoPuesta = new List<(string, string, Vector2)>();

    private static void Decorar()
    {
        decoPuesta.Clear();
        Transform origen = plantillas.Find("Decoracion");
        Transform destino = nivel.Find("Decoracion");
        if (origen == null || destino == null) { Debug.LogWarning(Log + "No hay decoracion en la base."); return; }
        var respetar = Respetar();
        var puestas = new List<(Vector2 p, float medio, string capa)>();
        for (int zi = 0; zi < ex.decoracion.Count; zi++)
        {
            var z = ex.decoracion[zi];
            Transform cont = new GameObject("Zona_" + z.nombre).transform;
            cont.SetParent(destino, false);
            var azar = new System.Random(ex.semillaDeco + zi * 101);
            RectInt c = Casillas(z.dibujo);
            // Candidatos: superficies dentro del rectangulo de la zona, en orden al azar (siempre el mismo).
            var candidatos = new List<Vector2Int>();
            for (int x = Mathf.Max(c.xMin, X0); x < Mathf.Min(c.xMax, XArena); x++)
                for (int y = c.yMin; y <= c.yMax; y++)
                    if (Rk(x, y - 1) && !Rk(x, y) && !ilusorias.Contains(new Vector2Int(x, y - 1))) candidatos.Add(new Vector2Int(x, y));
            candidatos = candidatos.OrderBy(_ => azar.Next()).ToList();
            foreach (var pz in z.piezas)
            {
                Transform plantilla = origen.Cast<Transform>().FirstOrDefault(t => t.name == pz.nombre);
                if (plantilla == null) { Debug.LogWarning(Log + "No hay pieza de decoracion \"" + pz.nombre + "\"."); continue; }
                SpriteRenderer[] srs = plantilla.GetComponentsInChildren<SpriteRenderer>(true);
                Bounds b0 = srs.Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; });
                string capa = srs[0].sortingLayerName;
                bool fondo = capa == "Background";
                float medio = b0.extents.x;
                int puestasPieza = 0;
                foreach (Vector2Int cand in candidatos)
                {
                    if (puestasPieza >= pz.cantidad) break;
                    Vector2 pie = new Vector2(cand.x + 0.5f, cand.y);
                    // Las del fondo (detras de la roca) no necesitan sitio por encima.
                    if (!SueloLlano(cand.x, cand.y, fondo ? 0.5f : medio, fondo ? 1f : b0.size.y)) continue;
                    if (respetar.Any(r => Mathf.Abs(r.p.x - pie.x) < r.r + (fondo ? 0f : medio) && Mathf.Abs(r.p.y - pie.y) < 3f)) continue;
                    if (puestas.Any(o => o.capa == capa && Mathf.Abs(o.p.y - pie.y) < 2f && Mathf.Abs(o.p.x - pie.x) < o.medio + medio + 0.6f)) continue;
                    if (puestas.Any(o => Vector2.Distance(o.p, pie) < 1.2f)) continue;
                    GameObject go = Object.Instantiate(plantilla.gameObject, cont);
                    go.name = pz.nombre;
                    go.transform.position = new Vector3(pie.x, pie.y, plantilla.position.z);
                    if (azar.Next(2) == 0) { Vector3 s = go.transform.localScale; go.transform.localScale = new Vector3(-s.x, s.y, s.z); }
                    Bounds bb = go.GetComponentsInChildren<SpriteRenderer>(true).Select(r => r.bounds).Aggregate((a, q) => { a.Encapsulate(q); return a; });
                    go.transform.position += new Vector3(pie.x - bb.center.x, pie.y - 0.05f - bb.min.y, 0f);
                    puestas.Add((pie, medio, capa));
                    decoPuesta.Add((z.nombre, pz.nombre, pie));
                    puestasPieza++;
                }
                if (puestasPieza < pz.cantidad)
                    Debug.Log(Log + $"Decoracion \"{z.nombre}\": {pz.nombre} {puestasPieza} de {pz.cantidad} (no hay mas sitio libre).");
            }
        }
    }

    // ------------------------------------------------------------------ Minijefes (paso 9)

    // Arena opcional: una ArenaJefe que NO es la del jefe del nivel (guarda su propia bandera)
    // y una copia del prefab "Zona de jefe" (activador + muro de niebla) en su borde izquierdo.
    private static void Minijefes()
    {
        GameObject zonaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Zona de jefe.prefab");
        ConfigNivel config = AssetDatabase.LoadAssetAtPath<ConfigNivel>("Assets/Data/Niveles/Config Nivel Nieve.asset");
        foreach (var m in ex.minijefes)
        {
            JefeBase jefe = m.prefab != null ? m.prefab.GetComponent<JefeBase>() : null;
            if (jefe == null) { Debug.LogWarning(Log + "Minijefe " + m.nombre + ": falta el prefab con JefeBase."); continue; }
            RectInt c = Casillas(m.arena);
            int xMedio = (c.xMin + c.xMax) / 2;
            int suelo = Superficie(xMedio, c.yMax);
            int techo = int.MaxValue;
            for (int x = c.xMin; x < c.xMax; x++)
            {
                int y = suelo;
                while (y < Y1 && !Rk(x, y)) y++;
                techo = Mathf.Min(techo, y);
            }
            float x0 = c.xMin + 0.5f, x1 = c.xMax - 0.5f;

            GameObject go = new GameObject("ArenaMinijefe_" + m.clave);
            go.layer = LayerMask.NameToLayer("Items");
            go.transform.SetParent(raiz, false);
            go.transform.position = new Vector3((x0 + x1) * 0.5f, (suelo + techo) * 0.5f, 0f);
            BoxCollider2D bc = go.AddComponent<BoxCollider2D>();
            bc.isTrigger = true;
            bc.size = new Vector2(x1 - x0, techo - suelo);
            ArenaJefe arena = go.AddComponent<ArenaJefe>();
            var so = new SerializedObject(arena);
            so.FindProperty("prefabJefe").objectReferenceValue = jefe;
            so.FindProperty("nombre").stringValue = m.nombre;
            so.FindProperty("titulo").stringValue = "";
            so.FindProperty("zona").rectValue = Rect.MinMaxRect(x0, suelo, x1, techo);
            so.FindProperty("suelo").floatValue = suelo;
            so.FindProperty("marcaFase").floatValue = 0f;
            so.FindProperty("textoFase2").stringValue = "";
            so.FindProperty("jefePrincipal").boolValue = false;
            so.FindProperty("claveMinijefe").stringValue = m.clave;
            if (m.musicaDelNivel && config != null && config.jefeFase1.pista != null)
            {
                so.FindProperty("musica").objectReferenceValue = config.jefeFase1.pista;
                so.FindProperty("volumen").floatValue = config.jefeFase1.volumen;
                so.FindProperty("inicioFase1").floatValue = config.jefeFase1.inicio;
                so.FindProperty("bucleFase1").vector2Value = config.jefeFase1.bucle;
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            if (zonaPrefab == null) { Debug.LogWarning(Log + "Falta el prefab Zona de jefe."); continue; }
            GameObject zona = (GameObject)PrefabUtility.InstantiatePrefab(zonaPrefab, go.transform);
            zona.name = "Zona de jefe (" + m.nombre + ")";
            zona.transform.position = new Vector3(x0 + 0.5f, suelo, 0f);
            var sz = new SerializedObject(zona.GetComponent<ZonaJefe>());
            sz.FindProperty("arena").objectReferenceValue = arena;
            sz.FindProperty("ladoArena").intValue = 1;
            sz.FindProperty("distanciaActivador").floatValue = 3f;
            sz.FindProperty("largoActivador").floatValue = x1 - x0 - 4f;
            sz.FindProperty("altoActivador").floatValue = techo - suelo;
            sz.ApplyModifiedPropertiesWithoutUndo();
            MuroNiebla muro = zona.GetComponentInChildren<MuroNiebla>(true);
            if (muro != null)
            {
                muro.alto = techo - suelo;
                EditorUtility.SetDirty(muro);
            }
        }
    }

    // ------------------------------------------------------------------ Mecanicas (paso 5)

    // Clave guardada de una forma del dibujo: su id con "d_" delante (nunca coincide con las de la Ronda 19).
    public static string Clave(Forma f) => "d_" + f.id;

    private static void Mecanicas()
    {
        Transform padre = new GameObject("Mecanicas").transform;
        padre.SetParent(raiz, false);
        BrillosHielo(padre);
        foreach (Forma f in formas.Where(f => f.tipo == Tipo.ParedRompe)) Derrumbe(padre, f);
        foreach (Forma f in formas.Where(f => f.tipo == Tipo.Agua)) Lago(padre, f);
        Ventiscas();
        Refugios(padre);
        Corrientes(padre);
    }

    // Brillos sobre el piso resbaladizo (las casillas "hielo_" ya resbalan; esto se ve).
    private static void BrillosHielo(Transform padre)
    {
        Transform cont = new GameObject("BrillosHielo").transform;
        cont.SetParent(padre, false);
        foreach (var (f, x0, x1, sup) in tramosHielo)
        {
            GameObject go = new GameObject("Hielo_" + f.id);
            go.transform.SetParent(cont, false);
            go.transform.position = new Vector3((x0 + x1) * 0.5f, sup - 0.5f, 0f);
            SueloHielo sh = go.AddComponent<SueloHielo>();
            var lista = new List<SpriteRenderer>();
            for (float x = x0 + 0.6f; x < x1 - 0.4f; x += 1.7f)
            {
                SpriteRenderer b = Pieza(go.transform, "Brillo", blanco, new Vector2(x, sup - 0.12f), new Vector2(0.9f, 0.05f), "Ground", 3);
                b.color = new Color(1f, 1f, 1f, 0.35f);
                lista.Add(b);
            }
            sh.PonerBrillos(lista.ToArray());
        }
    }

    // Pared que se rompe solo desde la derecha (el final del atajo). Rota, queda abierta en la partida.
    private static void Derrumbe(Transform padre, Forma f)
    {
        RectInt c = Casillas(f.r);
        GameObject der = new GameObject("Derrumbe_" + f.id);
        der.layer = LayerMask.NameToLayer("Ground");
        der.transform.SetParent(padre, false);
        der.transform.position = new Vector3(c.center.x, c.center.y, 0f);
        BoxCollider2D bd = der.AddComponent<BoxCollider2D>();
        bd.size = new Vector2(c.width, c.height);
        Transform dv = new GameObject("Visual").transform;
        dv.SetParent(der.transform, false);
        int k = 0;
        for (int y = c.yMin; y < c.yMax; y++)
        for (int x = c.xMin; x < c.xMax; x++, k++)
        {
            SpriteRenderer sr = Pieza(dv, "Roca", rocaSprite != null ? rocaSprite : blanco,
                                      new Vector2(x + 0.5f + (k % 2 == 0 ? -0.08f : 0.1f), y + 0.4f), Vector2.zero, "Middleground", 8 + k % 3);
            float esc = 1.1f / Mathf.Max(0.1f, sr.sprite.bounds.size.x);
            sr.transform.localScale = new Vector3(esc, esc, 1f);
            sr.color = new Color(0.78f, 0.82f, 0.9f);
        }
        DerrumbeAtajo da = der.AddComponent<DerrumbeAtajo>();
        da.clave = Clave(f);
        da.ladoQueAbre = 1;
        da.visual = dv;
        da.sonidoGolpe = "cueva_crujido";
        da.sonidoAbrir = "cueva_derrumbe";
    }

    // El agua: como el lago de siempre (mismas piezas), del ancho del dibujo. Caer es morir;
    // un tajo con escarcha deja el puente de hielo, que queda guardado en la partida.
    private static void Lago(Transform padre, Forma f)
    {
        RectInt c = Casillas(f.r);
        float x0 = c.xMin, x1 = c.xMax;
        int suelo = Mathf.Max(Superficie(c.xMin - 1, Y1 - 1), Superficie(c.xMax, Y1 - 1));
        float superficie = suelo - 0.25f;
        int capaSuelo = LayerMask.NameToLayer("Ground"), trampas = LayerMask.NameToLayer("Traps");
        string gandalf = ConfigurarRecursosRPG.Pack + "MAPAS/Mapa_BosqueGandalf/";

        GameObject lago = new GameObject("Lago_" + f.id);
        lago.transform.SetParent(padre, false);
        lago.transform.position = new Vector3((x0 + x1) * 0.5f, suelo - 0.5f, 0f);
        GameObject agua = new GameObject("Agua");
        agua.transform.SetParent(lago.transform);
        Texture2D hojaAgua = AssetDatabase.LoadAssetAtPath<Texture2D>(gandalf + "Animated Sprites/GandalfHardcore Water Tiles sheet.png");
        var clipAgua = new AnimadorHoja.Clip
        {
            nombre = "agua", hoja = hojaAgua, anchoCelda = 160, altoCelda = 64, fila = 0, primero = 0, cantidad = 3,
            fps = 4f, bucle = true, pivote = new Vector2(0.5f, 1f), pixelesPorUnidad = 32f,
        };
        for (float x = x0 + 2.5f; x < x1 + 2.5f; x += 5f)
        {
            SpriteRenderer sr = new GameObject("Tramo").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(agua.transform);
            sr.transform.position = new Vector3(Mathf.Min(x, x1 - 2.5f), superficie + 0.1f, 0f);
            sr.sortingLayerName = "Ground";
            sr.sortingOrder = 4;
            sr.color = new Color(0.7f, 0.85f, 0.95f, 0.95f);
            AnimadorHoja a = sr.gameObject.AddComponent<AnimadorHoja>();
            a.destino = sr;
            a.clips = new List<AnimadorHoja.Clip> { clipAgua };
            a.clipInicial = "agua";
        }
        SpriteRenderer fondoAgua = Pieza(agua.transform, "Profundo", blanco, new Vector2((x0 + x1) * 0.5f, superficie - 1.9f - 6f),
                                         new Vector2(x1 - x0, 12f), "Ground", 3);
        fondoAgua.color = new Color(0.05f, 0.14f, 0.2f, 1f);

        GameObject ahogo = new GameObject("Ahogarse");
        ahogo.layer = trampas;
        ahogo.transform.SetParent(lago.transform);
        ahogo.transform.position = new Vector3((x0 + x1) * 0.5f, superficie - 1.2f, 0f);
        BoxCollider2D ah = ahogo.AddComponent<BoxCollider2D>();
        ah.isTrigger = true;
        ah.size = new Vector2(x1 - x0, 1.6f);
        ahogo.AddComponent<DeadArea>();

        GameObject puente = new GameObject("PuenteHielo");
        puente.layer = capaSuelo;
        puente.transform.SetParent(lago.transform);
        puente.transform.position = new Vector3((x0 + x1) * 0.5f, suelo - 0.5f, 0f);
        BoxCollider2D pc = puente.AddComponent<BoxCollider2D>();
        pc.size = new Vector2(x1 - x0, 1f);
        puente.AddComponent<SueloHielo>();
        GameObject visHielo = new GameObject("HieloVisual");
        visHielo.transform.SetParent(puente.transform);
        for (float x = x0 + 0.5f; x < x1; x += 1f)
        {
            SpriteRenderer t = Pieza(visHielo.transform, "Losa", blanco, new Vector2(x, suelo - 0.5f), new Vector2(1.02f, 1f), "Ground", 6);
            t.color = Mathf.RoundToInt(x) % 2 == 0 ? new Color(0.62f, 0.85f, 1f, 0.92f) : new Color(0.7f, 0.9f, 1f, 0.92f);
            SpriteRenderer borde = Pieza(visHielo.transform, "Escarcha", blanco, new Vector2(x, suelo - 0.05f), new Vector2(1.02f, 0.1f), "Ground", 7);
            borde.color = new Color(0.95f, 0.98f, 1f, 1f);
        }
        GameObject golpeable = new GameObject("SuperficieLago");
        golpeable.transform.SetParent(lago.transform);
        golpeable.transform.position = new Vector3((x0 + x1) * 0.5f, suelo + 0.1f, 0f);
        BoxCollider2D gc = golpeable.AddComponent<BoxCollider2D>();
        gc.isTrigger = true;
        gc.size = new Vector2(x1 - x0 + 0.4f, 1.2f);
        AguaCongelable ac = golpeable.AddComponent<AguaCongelable>();
        ac.Configurar(pc, agua, visHielo, x1 - x0);
        ac.clave = Clave(f);
    }

    // Ventisca: la de siempre (mismo objeto y mismos numeros), donde esta la nube.
    private static void Ventiscas()
    {
        Transform plantilla = nivel.Find("Ventisca");
        var nubes = formas.Where(f => f.tipo == Tipo.Ventisca).ToList();
        if (plantilla == null) { if (nubes.Count > 0) Debug.LogWarning(Log + "La base no tiene Ventisca."); return; }
        float yArriba = WY(ex.alturaVentisca.x), yAbajo = WY(ex.alturaVentisca.y);
        for (int i = 0; i < nubes.Count; i++)
        {
            Transform v = i == 0 ? plantilla : Object.Instantiate(plantilla.gameObject, nivel).transform;
            v.name = i == 0 ? "Ventisca" : "Ventisca_" + (i + 1);
            float a = WX(nubes[i].r.xMin), b = WX(nubes[i].r.xMax);
            v.position = new Vector3((a + b) * 0.5f, (yArriba + yAbajo) * 0.5f, 0f);
            BoxCollider2D bc = v.GetComponent<BoxCollider2D>();
            bc.offset = Vector2.zero;
            bc.size = new Vector2((b - a) / Mathf.Max(0.01f, v.lossyScale.x), (yArriba - yAbajo) / Mathf.Max(0.01f, v.lossyScale.y));
        }
        if (nubes.Count == 0) Destruir(plantilla);
    }

    // Refugios: tras cada roca (a sotavento, a su izquierda) el viento no empuja.
    private static void Refugios(Transform padre)
    {
        int items = LayerMask.NameToLayer("Items");
        foreach (Vector2Int r in rocasRefugio)
        {
            GameObject go = new GameObject("Refugio_" + r.x);
            go.layer = items;
            go.transform.SetParent(padre, false);
            go.transform.position = new Vector3(r.x - 1.5f, r.y + 1.5f, 0f);
            BoxCollider2D b = go.AddComponent<BoxCollider2D>();
            b.isTrigger = true;
            b.size = new Vector2(3f, 3f);
            go.AddComponent<RefugioViento>();
            SpriteRenderer m = Pieza(go.transform, "Remanso", blanco, new Vector2(r.x - 1.5f, r.y + 0.06f), new Vector2(2.6f, 0.12f), "Ground", 4);
            m.color = new Color(0.95f, 0.98f, 1f, 0.55f);
            if (monticuloSprite != null)
            {
                SpriteRenderer nm = Pieza(go.transform, "NieveQuieta", monticuloSprite, new Vector2(r.x - 0.7f, r.y), Vector2.zero, "Middleground", 3);
                nm.transform.localScale = Vector3.one * 0.6f;
                nm.transform.position = new Vector3(r.x - 0.7f, r.y - (nm.bounds.min.y - nm.transform.position.y) - 0.02f, 0f);
            }
        }
    }

    public const string RutaAjustesCorriente = "Assets/Data/Niveles/Corriente Viento.asset";

    private static void Corrientes(Transform padre)
    {
        var aj = AssetDatabase.LoadAssetAtPath<AjustesCorriente>(RutaAjustesCorriente);
        if (aj == null)
        {
            aj = ScriptableObject.CreateInstance<AjustesCorriente>();
            AssetDatabase.CreateAsset(aj, RutaAjustesCorriente);
            AssetDatabase.SaveAssets();
        }
        int items = LayerMask.NameToLayer("Items");
        foreach (var co in ex.corrientes)
        {
            RectInt c = Casillas(co.dibujo);
            int yMin = Mathf.Max(c.yMin, Y0);
            GameObject go = new GameObject("Corriente_" + co.nombre);
            go.layer = items;
            go.transform.SetParent(padre, false);
            go.transform.position = new Vector3(c.center.x, (yMin + c.yMax) * 0.5f, 0f);
            BoxCollider2D b = go.AddComponent<BoxCollider2D>();
            b.isTrigger = true;
            b.size = new Vector2(c.width, c.yMax - yMin);
            go.AddComponent<CorrienteViento>().ajustes = aj;
        }
    }

    // La parte de arriba de la roca que llega al fondo en esa columna.
    private static int TopeSuelo(int x)
    {
        int y = Y0;
        while (y < Y1 && Rk(x, y)) y++;
        return y;
    }

    private static void Hogueras()
    {
        Transform cont = nivel.Find("Hogueras");
        var dibujadas = formas.Where(f => f.tipo == Tipo.Hoguera).OrderBy(f => f.r.x).ToList();
        var viejas = cont.Cast<Transform>().OrderBy(h => h.name, System.StringComparer.Ordinal).ToList();
        // Partidas viejas: los puntos de reaparicion de las hogueras de la base, de izquierda a derecha.
        ReaparicionNieve rn = raiz.gameObject.AddComponent<ReaparicionNieve>();
        rn.hoguerasViejas = viejas.Select(h => h.GetComponent<Hoguera>()).Where(h => h != null)
                                  .Select(h => h.PuntoReaparicion).OrderBy(v => v.x).ToArray();
        Transform plantilla = viejas.FirstOrDefault();
        for (int i = 0; i < dibujadas.Count; i++)
        {
            Forma f = dibujadas[i];
            Transform h = i < viejas.Count ? viejas[i] : Object.Instantiate(plantilla.gameObject, cont).transform;
            h.name = "Hoguera_" + (i + 1);
            int x = Casilla(WX(f.r.center.x) - 0.5f);
            int sup = Superficie(x, Casilla(WY(f.r.yMax)) + 3);
            h.position = new Vector3(WX(f.r.center.x), sup, 0f);
            var info = ex.hogueras.FirstOrDefault(e => e.id == f.id);
            Hoguera hg = h.GetComponent<Hoguera>();
            if (hg != null && info != null)
            {
                var so = new SerializedObject(hg);
                so.FindProperty("nombreLugar").stringValue = info.nombreLugar;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            if (info == null) Debug.LogWarning(Log + "La hoguera " + f.id + " no tiene nombre en Extras Nieve.");
        }
        for (int i = dibujadas.Count; i < viejas.Count; i++) Destruir(viejas[i]);
    }

    // El player, su punto de reaparicion y la puerta de entrada, al principio del dibujo.
    private static void Inicio()
    {
        int x = Casilla(WX(ex.inicioX) - 0.5f);
        int sup = Superficie(x, Y1 - 1);
        Vector3 p = new Vector3(WX(ex.inicioX), sup, 0f);
        GameObject player = escena.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).Select(t => t.gameObject).FirstOrDefault(g => g.CompareTag("Player"));
        if (player != null) player.transform.position = p + new Vector3(0f, 0.7f, 0f);
        GameObject rp = Raiz("RespawnPoint");
        if (rp != null) rp.transform.position = p + new Vector3(0f, 0.7f, 0f);
        GameObject ent = Raiz("EntranceDoor");
        if (ent != null) ent.transform.position = p;
        GameObject cam = Raiz("Main Camera");
        if (cam != null) cam.transform.position = new Vector3(p.x + 1.7f, p.y + 0.7f, cam.transform.position.z);
    }

    private static SpriteRenderer Pieza(Transform padre, string nombre, Sprite s, Vector2 centro, Vector2 tamano, string capa, int orden)
    {
        SpriteRenderer sr = new GameObject(nombre).AddComponent<SpriteRenderer>();
        sr.transform.SetParent(padre, false);
        sr.sprite = s;
        sr.transform.position = centro;
        if (tamano != Vector2.zero && s != null)
            sr.transform.localScale = new Vector3(tamano.x / s.bounds.size.x, tamano.y / s.bounds.size.y, 1f);
        sr.sortingLayerName = capa;
        sr.sortingOrder = orden;
        return sr;
    }

    // ------------------------------------------------------------------ Comprobacion de saltos (calculo)

    public struct ResultadoSalto { public string nombre, veredicto, detalle; public bool principal, ok; }

    // Con los numeros del player (prefab): salto 10, doble salto, gravedad x3, carrera 8,5, andando 5.
    public static List<ResultadoSalto> ComprobarSaltos()
    {
        ex = Extras();
        GameObject pre = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");
        var so = new SerializedObject(pre.GetComponent<PlayerControler>());
        float salto = so.FindProperty("jumpForce").floatValue;
        int extra = so.FindProperty("extraJumps").intValue;
        float andar = so.FindProperty("speed").floatValue;
        float correr = so.FindProperty("sprintSpeed").floatValue;
        float g = -Physics2D.gravity.y * pre.GetComponent<Rigidbody2D>().gravityScale;
        // Agarre de cornisa: el borde puede quedar por encima de los pies hasta (cuerpo + alcance) - margen.
        const float cornisa = 1.2f, anchoMedio = 0.25f;

        var res = new List<ResultadoSalto>();
        foreach (var s in ex.saltos)
        {
            float x0 = Casilla(WX(s.desde.x)), x1 = Casilla(WX(s.hasta.x));
            float y0 = Casilla(WY(s.desde.y)), y1 = Casilla(WY(s.hasta.y));
            float dx = Mathf.Max(0f, Mathf.Abs(x1 - x0) - anchoMedio * 2f), dy = y1 - y0;
            var r = new ResultadoSalto { nombre = s.nombre, principal = s.principal };
            string medida = $"{Mathf.Abs(x1 - x0):0.#} u de ancho, {(dy >= 0 ? "sube" : "baja")} {Mathf.Abs(dy):0.#} u";
            if (!string.IsNullOrEmpty(s.soloCon))
            {
                r.veredicto = "solo con " + s.soloCon;
                r.ok = true;
                r.detalle = medida;
                res.Add(r);
                continue;
            }
            float corriendo = MejorAltura(dx, correr, salto, extra, g), andando = MejorAltura(dx, andar, salto, extra, g);
            if (andando >= dy + 0.05f) r.veredicto = "sí (andando)";
            else if (corriendo >= dy + 0.05f) r.veredicto = "sí (corriendo)";
            else if (corriendo >= dy - cornisa) r.veredicto = "sí (con agarre de cornisa)";
            else r.veredicto = "NO";
            r.ok = r.veredicto != "NO";
            r.detalle = medida + $" · altura que sobra corriendo: {corriendo - dy:+0.0;-0.0} u";
            res.Add(r);
        }
        return res;
    }

    // La mayor altura (sobre el punto de salida) a la que se puede estar habiendo
    // recorrido ya "dx" en horizontal, eligiendo bien el momento del doble salto.
    private static float MejorAltura(float dx, float v, float salto, int extra, float g)
    {
        float tMin = dx / v, mejor = float.MinValue;
        for (float td = 0f; td <= 2f * salto / g; td += 0.005f)
        for (float t = tMin; t <= tMin + 2.5f; t += 0.005f)
        {
            float y;
            if (extra < 1 || t <= td) y = salto * t - 0.5f * g * t * t;
            else
            {
                float y1 = salto * td - 0.5f * g * td * td, u = t - td;
                y = y1 + salto * u - 0.5f * g * u * u;
            }
            if (y > mejor) mejor = y;
            if (extra < 1) break;
        }
        // Sin doble salto el bucle de td no hace falta: la parabola simple.
        return mejor;
    }

    // ------------------------------------------------------------------ Huella (para comparar dos generaciones)

    public static string Huella(Scene s)
    {
        var sb = new StringBuilder();
        foreach (GameObject r in s.GetRootGameObjects().OrderBy(g => g.name, System.StringComparer.Ordinal).ThenBy(g => g.transform.position.x))
            HuellaDe(r.transform, "", sb);
        using (var sha = System.Security.Cryptography.SHA1.Create())
            return System.BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()))).Replace("-", "").Substring(0, 16);
    }

    public static void HuellaDe(Transform t, string camino, StringBuilder sb)
    {
        string c = camino + "/" + t.name;
        Vector3 p = t.position;
        sb.Append(c).Append(' ').Append(t.gameObject.activeSelf).Append(' ')
          .Append(p.x.ToString("0.###", CultureInfo.InvariantCulture)).Append(',').Append(p.y.ToString("0.###", CultureInfo.InvariantCulture)).Append(' ');
        foreach (Component k in t.GetComponents<Component>()) sb.Append(k != null ? k.GetType().Name : "null").Append(';');
        sb.Append('\n');
        Tilemap tm = t.GetComponent<Tilemap>();
        if (tm != null)
        {
            BoundsInt b = tm.cellBounds;
            for (int x = b.xMin; x < b.xMax; x++)
            for (int y = b.yMin; y < b.yMax; y++)
            {
                TileBase tb = tm.GetTile(new Vector3Int(x, y, 0));
                if (tb != null) sb.Append(x).Append(',').Append(y).Append('=').Append(tb.name).Append('\n');
            }
        }
        foreach (Transform h in t.Cast<Transform>().OrderBy(h => h.name, System.StringComparer.Ordinal).ThenBy(h => h.position.x).ThenBy(h => h.position.y))
            HuellaDe(h, c, sb);
    }

    // ------------------------------------------------------------------ Capturas

    // Vistas por zonas (rectangulos del dibujo) y una vista del nivel entero.
    private static readonly (string n, float x0, float y0, float x1, float y1)[] Vistas =
    {
        ("01_zona_inicial", -420, 120, 600, 620),
        ("02_hoguera1_escondite_bajada", 420, 120, 1300, 1180),
        ("03_camino_alto_pared7", 1150, -120, 2450, 700),
        ("04_camino_bajo_hielo", 1100, 560, 2520, 1180),
        ("05_vacio_viento_plataformas", 2250, -120, 3450, 1250),
        ("06_pasillo_arena_minijefe", 3100, 640, 4680, 1300),
        ("07_hoguera2_agua", 3900, -300, 6050, 800),
        ("08_escalones_ventisca", 5800, -300, 7750, 850),
        ("09_elite_hoguera3", 7480, -560, 9300, 860),
        ("10_antesala_arena_sombra", 8600, -900, 10800, 900),
    };

    public static void Capturar(string sufijo = "")
    {
        ex = Extras();
        escena = EditorSceneManager.OpenScene(RutaActiva, OpenSceneMode.Single);
        string carpeta = Path.Combine(CarpetaInforme, "imagenes");
        Directory.CreateDirectory(carpeta);
        var p = Object.FindFirstObjectByType<ParallaxCueva>();
        GameObject player = GameObject.FindWithTag("Player");
        Vector3 inicio = (player != null ? player.transform.position : Vector3.zero) + new Vector3(1.7f, 0f, 0f);
        if (p != null) p.PrepararRellenos();

        // Marcas para leer las capturas: rojo = muerte, cian = tamaño del player.
        var marcas = new List<GameObject>();
        Sprite bl = AssetDatabase.LoadAllAssetsAtPath(Tiles + "Blanco.png").OfType<Sprite>().FirstOrDefault();
        foreach (DeadArea d in Object.FindObjectsByType<DeadArea>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            Collider2D c = d.GetComponent<Collider2D>();
            if (c == null) continue;
            Bounds b = c.bounds;
            var m = Pieza(null, "MarcaMuerte", bl, b.center, new Vector2(Mathf.Min(b.size.x, 600f), Mathf.Max(b.size.y, 0.6f)), "VFX", 100);
            m.color = new Color(1f, 0.1f, 0.1f, 0.55f);
            marcas.Add(m.gameObject);
        }
        foreach (GrietaMortal gm in Object.FindObjectsByType<GrietaMortal>(FindObjectsSortMode.None))
        {
            var m = Pieza(null, "MarcaHueco", bl, (Vector2)gm.transform.position + new Vector2(0f, -gm.fondo * 0.5f), new Vector2(gm.ancho, gm.fondo), "VFX", 99);
            m.color = new Color(0.9f, 0.1f, 0.15f, 0.16f);
            marcas.Add(m.gameObject);
        }
        var regla = new List<Vector2>();
        if (player != null) regla.Add(player.transform.position);
        foreach (Hoguera h in Object.FindObjectsByType<Hoguera>(FindObjectsSortMode.None)) regla.Add((Vector2)h.transform.position + new Vector2(1.2f, 0.52f));
        foreach (Vector2 r in regla)
        {
            var m = Pieza(null, "MarcaPlayer", bl, r, new Vector2(0.5f, 1.04f), "VFX", 101);
            m.color = new Color(0.2f, 1f, 1f, 0.8f);
            marcas.Add(m.gameObject);
        }

        GameObject go = new GameObject("CamCaptura");
        Camera cam = go.AddComponent<Camera>();
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.1f, 0.12f, 0.18f);

        GameObject fondo = p != null ? p.gameObject : null;
        var lista = Vistas.Select(v => (v.n, WX(v.x0), WY(v.y1), WX(v.x1), WY(v.y0), 1280, 720)).ToList();
        lista.Insert(0, ("00_nivel_entero", (float)X0Escena(), -36f, 410f, 22f, 3600, 640));
        foreach (var (n, ax, ay, bx, by, w, h) in lista)
        {
            float aspecto = (float)w / h;
            Vector3 centro = new Vector3((ax + bx) * 0.5f, (ay + by) * 0.5f, -20f);
            cam.aspect = aspecto;
            cam.orthographicSize = Mathf.Max((by - ay) * 0.5f, (bx - ax) * 0.5f / aspecto) + 0.5f;
            go.transform.position = centro;
            // En la vista del nivel entero el fondo estorba: se oculta solo para esa foto.
            if (fondo != null) fondo.SetActive(n != "00_nivel_entero");
            if (p != null) { p.FijarInicio(inicio); p.Colocar(centro, 0f); }
            RenderTexture rt = new RenderTexture(w, h, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(carpeta, n + sufijo + ".png"), tex.EncodeToPNG());
            cam.targetTexture = null;
            rt.Release();
            Object.DestroyImmediate(tex);
        }
        if (fondo != null) fondo.SetActive(true);
        Object.DestroyImmediate(go);
        foreach (GameObject m in marcas) Object.DestroyImmediate(m);
        Debug.Log(Log + "Capturas en " + carpeta);
    }

    // Fotos con el tamano de la camara del juego (CinemachineCamera: 3,33), con el fondo
    // colocado como si la partida hubiera empezado en "inicio". Sirven para ver si el
    // parallax deja ver su borde al bajar o subir mucho.
    private static readonly (string n, float x, float y)[] PuntosJuego =
    {
        ("inicio", -340, 310), ("hoguera1", 700, 270), ("camino_bajo", 1300, 840), ("hielo", 2200, 960),
        ("fondo_del_viento", 2700, 1060), ("plataforma_alta", 3100, 360), ("arena_minijefe", 4100, 1180),
        ("hoguera2", 4140, 210), ("agua", 5600, 210), ("escalones", 6900, 320), ("suelo_elite", 8200, 560),
        ("plataforma_elite", 8200, -120), ("hoguera3", 8870, 40),
    };

    public static void CapturasJuego(string ruta, string prefijo, IEnumerable<(string n, Vector2 pos)> puntos)
    {
        EditorSceneManager.OpenScene(ruta, OpenSceneMode.Single);
        string carpeta = Path.Combine(CarpetaInforme, "imagenes", "juego");
        Directory.CreateDirectory(carpeta);
        var p = Object.FindFirstObjectByType<ParallaxCueva>();
        GameObject player = GameObject.FindWithTag("Player");
        Vector3 inicio = (player != null ? player.transform.position : Vector3.zero) + new Vector3(1.7f, 1.1f, 0f);
        if (p != null) p.PrepararRellenos();
        GameObject go = new GameObject("CamCaptura");
        Camera cam = go.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 3.33f;
        cam.aspect = 16f / 9f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(1f, 0f, 1f);   // magenta: si se ve, no hay fondo ahi
        foreach (var (n, pos) in puntos)
        {
            Vector3 centro = new Vector3(pos.x + 1.7f, pos.y + 1.1f, -20f);
            go.transform.position = centro;
            if (p != null) { p.FijarInicio(inicio); p.Colocar(centro, 0f); }
            RenderTexture rt = new RenderTexture(960, 540, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(960, 540, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(carpeta, prefijo + n + ".png"), tex.EncodeToPNG());
            cam.targetTexture = null;
            rt.Release();
            Object.DestroyImmediate(tex);
        }
        Object.DestroyImmediate(go);
    }

    // Borde izquierdo del nivel (el de la ultima generacion; si no, el de la escena).
    private static int X0Escena() => X0 != 0 ? X0 - 1 : 30;

    // ------------------------------------------------------------------ Batch: generar dos veces, comparar, saltos y capturas

    public static void Batch()
    {
        var informe = new StringBuilder();
        try
        {
            // Huellas de lo que NO debe cambiar (fondo y ambiente), en la base.
            Scene b = EditorSceneManager.OpenScene(RutaBase, OpenSceneMode.Single);
            string fondoBase = HuellaParte(b, "Nivel/Fondo") + HuellaParte(b, "AmbienteNieve") + HuellaParte(b, "Nivel/ArenaJefe");
            string config = Hash(File.ReadAllBytes("Assets/Data/Niveles/Config Nivel Nieve.asset"));

            Generar();
            string h1 = Huella(EditorSceneManager.GetActiveScene());
            Generar();
            Scene s = EditorSceneManager.GetActiveScene();
            string h2 = Huella(s);
            string fondoNuevo = HuellaParte(s, "Nivel/Fondo") + HuellaParte(s, "AmbienteNieve") + HuellaParte(s, "Nivel/ArenaJefe");
            informe.AppendLine("# Comprobación del paso 4\n");
            informe.AppendLine($"- Generar dos veces: {(h1 == h2 ? "MISMO resultado" : "DISTINTO")} (huellas {h1} / {h2})");
            informe.AppendLine($"- Fondo (parallax), ambiente y arena: {(fondoBase == fondoNuevo ? "iguales que en Nieve_Ronda19" : "CAMBIARON")}");
            informe.AppendLine($"- Config Nivel Nieve: {(config == Hash(File.ReadAllBytes("Assets/Data/Niveles/Config Nivel Nieve.asset")) ? "sin cambios" : "CAMBIÓ")}");
            informe.AppendLine($"- Huecos mortales: {string.Join(", ", tramos.Select(t => $"x {t.x0}–{t.x1} (boca {t.boca})"))}");
            informe.AppendLine($"- Hogueras: {string.Join(", ", Object.FindObjectsByType<Hoguera>(FindObjectsSortMode.None).OrderBy(h => h.transform.position.x).Select(h => $"{h.name} ({h.transform.position.x:0.#}, {h.transform.position.y:0.#}) \"{h.NombreLugar}\""))}");
            GameObject pl = GameObject.FindWithTag("Player");
            informe.AppendLine($"- Inicio del player: ({pl.transform.position.x:0.#}, {pl.transform.position.y:0.#})");
            informe.AppendLine($"- Piso resbaladizo: {string.Join(", ", tramosHielo.Select(t => $"x {t.x0}–{t.x1} (suelo {t.sup})"))}");
            informe.AppendLine($"- Paredes falsas: {string.Join(", ", ilusorias.GroupBy(c => c.x).OrderBy(g => g.Key).Select(g => $"x {g.Key}, y {g.Min(c => c.y)}–{g.Max(c => c.y)}"))}");
            foreach (DerrumbeAtajo d in Object.FindObjectsByType<DerrumbeAtajo>(FindObjectsSortMode.None))
                informe.AppendLine($"- Pared que se rompe: ({d.transform.position.x:0.#}, {d.transform.position.y:0.#}), se abre desde {(d.ladoQueAbre > 0 ? "la derecha" : "la izquierda")}, clave atajo_Nivel Nieve_{d.clave}");
            foreach (AguaCongelable a in Object.FindObjectsByType<AguaCongelable>(FindObjectsSortMode.None))
                informe.AppendLine($"- Agua: centro x {a.transform.position.x:0.#}, clave sello_Nivel Nieve_{a.clave}");
            foreach (ZonaVentisca v in Object.FindObjectsByType<ZonaVentisca>(FindObjectsSortMode.None))
            {
                Bounds vb = v.GetComponent<Collider2D>().bounds;
                informe.AppendLine($"- Ventisca: x {vb.min.x:0.#}–{vb.max.x:0.#}, y {vb.min.y:0.#}–{vb.max.y:0.#}");
            }
            informe.AppendLine($"- Refugios (roca): {string.Join(", ", rocasRefugio.Select(r => $"({r.x}, {r.y})"))}");
            foreach (CorrienteViento c in Object.FindObjectsByType<CorrienteViento>(FindObjectsSortMode.None))
            {
                Bounds cb = c.GetComponent<Collider2D>().bounds;
                informe.AppendLine($"- Corriente de viento: x {cb.min.x:0.#}–{cb.max.x:0.#}, y {cb.min.y:0.#}–{cb.max.y:0.#}");
            }
            foreach (CofreAlmas c in Object.FindObjectsByType<CofreAlmas>(FindObjectsSortMode.None).OrderBy(c => c.transform.position.x))
            {
                var so = new SerializedObject(c);
                informe.AppendLine($"- Cofre de almas ({c.transform.position.x:0.#}, {c.transform.position.y:0.#}): {so.FindProperty("almas").intValue} almas, clave {so.FindProperty("clave").stringValue}");
            }
            foreach (CofreMejora c in Object.FindObjectsByType<CofreMejora>(FindObjectsSortMode.None).OrderBy(c => c.transform.position.x))
            {
                var so = new SerializedObject(c);
                informe.AppendLine($"- Cofre de mejora ({c.transform.position.x:0.#}, {c.transform.position.y:0.#}): {(Equipo.Objeto)so.FindProperty("objeto").enumValueIndex} x{so.FindProperty("cantidad").intValue} + {so.FindProperty("extra").arraySize} más, clave {so.FindProperty("clave").stringValue}");
            }
            informe.AppendLine("- Enemigos: " + string.Join(", ", Object.FindObjectsByType<EnemigoBase>(FindObjectsSortMode.None).OrderBy(e => e.transform.position.x)
                .Select(e => $"{e.name.Split('_')[0]} ({e.transform.position.x:0.#}, {e.transform.position.y:0.#}){(e.GetComponent<MonticuloEmboscada>() != null ? " montículo" : "")}")));
            foreach (SelloElemental se in Object.FindObjectsByType<SelloElemental>(FindObjectsSortMode.None).OrderBy(e => e.transform.position.x))
            {
                Bounds sb = se.solido.bounds;
                informe.AppendLine($"- Sello {se.clave} ({se.elemento}): x {sb.min.x:0.#}–{sb.max.x:0.#}, y {sb.min.y:0.#}–{sb.max.y:0.#}");
            }
            foreach (ArenaJefe a in Object.FindObjectsByType<ArenaJefe>(FindObjectsSortMode.None).OrderBy(a => a.transform.position.x))
            {
                var sa = new SerializedObject(a);
                informe.AppendLine($"- Arena: {sa.FindProperty("nombre").stringValue}, principal {a.EsPrincipal}, zona {sa.FindProperty("zona").rectValue}, jefe {(sa.FindProperty("prefabJefe").objectReferenceValue != null ? sa.FindProperty("prefabJefe").objectReferenceValue.name : "-")}");
            }
            ReaparicionNieve rp = Object.FindFirstObjectByType<ReaparicionNieve>();
            if (rp != null) informe.AppendLine("- Partidas viejas: hogueras de antes " + string.Join(", ", rp.hoguerasViejas.Select(v => $"({v.x:0.#}, {v.y:0.#})")));
            informe.AppendLine("- Estatuas: " + string.Join(", ", Object.FindObjectsByType<EstatuaPista>(FindObjectsSortMode.None).OrderBy(e => e.transform.position.x).Select(e => $"{e.id} ({e.transform.position.x:0.#}, {e.transform.position.y - 1.3f:0.#})")));
            informe.AppendLine();
            informe.AppendLine("```\n" + inventarioDeco + "```\n");
            informe.AppendLine("## Decoración por zonas\n");
            foreach (var g in decoPuesta.GroupBy(d => d.zona))
                informe.AppendLine($"- {g.Key}: " + string.Join(", ", g.GroupBy(d => d.pieza).Select(q => q.Key + " x" + q.Count())));
            int conColision = nivel.Find("Decoracion").GetComponentsInChildren<Collider2D>(true).Count(c => !c.isTrigger);
            informe.AppendLine($"- Piezas de decoración con colisión: {conColision}\n");

            informe.AppendLine("## Saltos\n\n| Salto | Camino | Medida | ¿Se puede? |\n|---|---|---|---|");
            foreach (var r in ComprobarSaltos())
                informe.AppendLine($"| {r.nombre} | {(r.principal ? "principal" : "secundario")} | {r.detalle} | {r.veredicto} |");

            Capturar();
            CapturasJuego(RutaActiva, "nueva_", PuntosJuego.Select(q => (q.n, new Vector2(WX(q.x), Casilla(WY(q.y))))).ToList());
            // Referencia: la nieve Ronda 19 en su punto mas bajo y mas alto.
            CapturasJuego(RutaBase, "ronda19_", new[] { ("inicio", new Vector2(3f, 0f)), ("galeria_inferior", new Vector2(250f, -3f)), ("atalaya", new Vector2(303f, 20f)) });
        }
        catch (System.Exception e)
        {
            informe.AppendLine("ERROR: " + e);
            Debug.LogException(e);
        }
        Directory.CreateDirectory(CarpetaInforme);
        File.WriteAllText(Path.Combine(CarpetaInforme, "paso4.md"), informe.ToString());
        Debug.Log(Log + "Informe:\n" + informe);
    }

    private static string HuellaParte(Scene s, string camino)
    {
        string[] partes = camino.Split('/');
        GameObject r = s.GetRootGameObjects().FirstOrDefault(g => g.name == partes[0]);
        if (r == null) return "falta " + camino;
        Transform t = partes.Length > 1 ? r.transform.Find(string.Join("/", partes.Skip(1))) : r.transform;
        if (t == null) return "falta " + camino;
        var sb = new StringBuilder();
        HuellaDe(t, "", sb);
        return Hash(Encoding.UTF8.GetBytes(sb.ToString()));
    }

    private static string Hash(byte[] datos)
    {
        using (var sha = System.Security.Cryptography.SHA1.Create())
            return System.BitConverter.ToString(sha.ComputeHash(datos)).Replace("-", "").Substring(0, 16);
    }
}
