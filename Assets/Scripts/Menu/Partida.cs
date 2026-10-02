using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

// Partidas guardadas, al estilo Souls: se guardan solas (al descansar en una
// hoguera, al cambiar de nivel, al vencer a un jefe, al morir y al salir al menu).
// Cada partida es un archivo en la carpeta de datos del juego y guarda:
//   - donde estas: el nivel y la ultima hoguera en la que descansaste;
//   - el progreso: almas, niveles de cada estadistica, la mancha de almas;
//   - lo que ya hiciste: cofres abiertos, jefes vencidos, frasco extra;
//   - el tiempo jugado.
// Si se juega una escena directamente desde el editor (sin pasar por el menu) no
// hay partida activa y no se guarda nada.
public static class Partida
{
    [Serializable]
    public class Datos
    {
        public int ranura;
        public string escena;
        public string lugar;
        public bool enHoguera;
        public float x, y;
        public int almas;
        public int[] niveles = new int[Progreso.NumEstadisticas];
        public int manchaAlmas;
        public float manchaX, manchaY;
        public string manchaEscena;
        public float segundosJugados;
        public string fecha;
        public List<string> banderas = new List<string>();
        // Equipo (ver Equipo): objetos de mejora y nivel de la espada y los frascos.
        // "lagrimas" y "nivelFrascos" son de antes (una mejora para los dos
        // frascos): al cargar se pasan a las de sangre y de mana (version 2).
        public int piedrasForja, lagrimas, nivelEspada, nivelFrascos;
        public int version;
        public int lagrimasCuracion, lagrimasMana, nivelCuracion, nivelMana;
        // Cargas extra de cada frasco (ya aplicadas).
        public int frascosSangre, frascosMana;
        // Frascos recogidos y aun sin aplicar en la hoguera (partidas viejas: 0).
        // Las piedras y lagrimas sin usar ya son "por aplicar" (piedrasForja...).
        public int frascosSangrePendientes, frascosManaPendientes;
        // Pistas de las estatuas ya leidas (el libro de pistas del menu de pausa).
        public List<PistaLeida> pistas = new List<PistaLeida>();
        // Manchas de sangre que deja el player al morir, por nivel.
        public List<ManchaSangre> manchas = new List<ManchaSangre>();

        public int NivelPersonaje => 1 + (niveles?.Sum() ?? 0);
    }

    [Serializable]
    public class PistaLeida
    {
        public string escena, id, titulo, texto;
    }

    [Serializable]
    public class ManchaSangre
    {
        public string escena;
        public float x, y, angulo, escala;
        public int variante;
    }

    public static Datos Actual { get; private set; }
    // Al cargar: donde tiene que aparecer el player en la escena que se abre.
    public static bool AparicionPendiente { get; private set; }
    public static Vector2 PosicionAparicion { get; private set; }

    // Las pruebas automaticas guardan en otra carpeta para no tocar las partidas reales.
    // En las pruebas por linea de comandos (solo Editor) la carpeta se fija antes de
    // darle a Play (ArranquePruebas), asi ni el primer fotograma toca el progreso real.
    public static string CarpetaPruebas
    {
        get
        {
#if UNITY_EDITOR
            if (carpetaPruebas == null && Application.isBatchMode)
            {
                string s = UnityEditor.SessionState.GetString(ClaveCarpetaArranque, "");
                if (s != "") return s;
            }
#endif
            return carpetaPruebas;
        }
        set => carpetaPruebas = value;
    }
    private static string carpetaPruebas;
    public const string ClaveCarpetaArranque = "Warrior.CarpetaPruebasArranque";
    // La del perfil activo (RegistroGuardado: real o "Simular jugador nuevo").
    private static string Carpeta => RegistroGuardado.Carpeta;
    private static string Archivo(int ranura) => Path.Combine(Carpeta, $"partida_{ranura}.json");

    // Nombre bonito de cada escena para la lista de partidas.
    public static string NombreNivel(string escena)
    {
        switch (escena)
        {
            case "Nivel Nieve": return "Paso Nevado";
            case "Nivel Cueva": return "Cueva Carmesí";
            default: return escena;
        }
    }

    // ------------------------------------------------------------------ Nueva y cargar

    // Empieza de cero en una ranura nueva: personaje a nivel 1, sin almas.
    public static void Nueva(string primeraEscena)
    {
        int ranura = Listar().Select(d => d.ranura).DefaultIfEmpty(0).Max() + 1;
        Progreso.Reiniciar();
        Equipo.Reiniciar();
        Actual = new Datos { ranura = ranura, escena = primeraEscena, lugar = "Inicio", fecha = DateTime.Now.ToString("dd/MM/yyyy HH:mm"), version = VersionActual };
        AparicionPendiente = false;
        Guardar();
    }

    private const int VersionActual = 2;

    // Partidas de antes de separar las mejoras de los frascos: la mejora comun
    // pasa a las dos (el mismo poder que tenian).
    private static void Migrar(Datos d)
    {
        if (d == null || d.version >= VersionActual) return;
        d.lagrimasCuracion += d.lagrimas;
        d.lagrimasMana += d.lagrimas;
        d.lagrimas = 0;
        d.nivelCuracion = Mathf.Max(d.nivelCuracion, d.nivelFrascos);
        d.nivelMana = Mathf.Max(d.nivelMana, d.nivelFrascos);
        d.nivelFrascos = 0;
        // El frasco extra de sangre de antes era una bandera.
        if (d.banderas != null && d.banderas.Contains("frasco_extra")) d.frascosSangre = Mathf.Max(d.frascosSangre, 1);
        d.version = VersionActual;
    }

    // Sin partida activa (los desafios): nada de lo que pase se guarda en ella.
    public static void Descargar()
    {
        Actual = null;
        AparicionPendiente = false;
        banderasSesion.Clear();
        pistasSesion.Clear();
        manchasSesion.Clear();
    }

    public static void Cargar(Datos d)
    {
        Migrar(d);
        Actual = d;
        Progreso.Establecer(d.almas, d.niveles, d.manchaAlmas, new Vector2(d.manchaX, d.manchaY), d.manchaEscena);
        AparicionPendiente = d.enHoguera;
        PosicionAparicion = new Vector2(d.x, d.y);
    }

    // La escena ya uso la posicion de aparicion.
    public static void AparicionHecha() => AparicionPendiente = false;

    public static List<Datos> Listar()
    {
        var l = new List<Datos>();
        if (!Directory.Exists(Carpeta)) return l;
        foreach (string f in Directory.GetFiles(Carpeta, "partida_*.json"))
        {
            try
            {
                Datos d = JsonUtility.FromJson<Datos>(File.ReadAllText(f));
                if (d != null) l.Add(d);
            }
            catch (Exception e) { Debug.LogWarning("[Partida] No se pudo leer " + f + ": " + e.Message); }
        }
        return l.OrderByDescending(d => d.fecha != null ? ParseFecha(d.fecha) : DateTime.MinValue).ToList();
    }

    public static void Borrar(Datos d)
    {
        string f = Archivo(d.ranura);
        if (File.Exists(f)) File.Delete(f);
        if (Actual != null && Actual.ranura == d.ranura) Actual = null;
    }

    private static DateTime ParseFecha(string s)
    {
        return DateTime.TryParseExact(s, "dd/MM/yyyy HH:mm", null, System.Globalization.DateTimeStyles.None, out DateTime f) ? f : DateTime.MinValue;
    }

    // ------------------------------------------------------------------ Guardar

    // Guarda como esta ahora. "lugar"/"posicion": la hoguera (si se acaba de
    // descansar); si no, se conserva la ultima.
    public static void Guardar(string lugar = null, Vector2? hoguera = null, string escena = null)
    {
        if (Actual == null) return;
        Actual.escena = escena ?? SceneManager.GetActiveScene().name;
        if (hoguera.HasValue)
        {
            Actual.enHoguera = true;
            Actual.x = hoguera.Value.x;
            Actual.y = hoguera.Value.y;
        }
        if (lugar != null) Actual.lugar = lugar;
        Actual.almas = Progreso.Almas;
        for (int i = 0; i < Progreso.NumEstadisticas; i++) Actual.niveles[i] = Progreso.Nivel((Progreso.Estadistica)i);
        Actual.manchaAlmas = Progreso.AlmasPerdidas;
        Actual.manchaX = Progreso.PosicionMancha.x;
        Actual.manchaY = Progreso.PosicionMancha.y;
        Actual.manchaEscena = Progreso.EscenaMancha;
        Actual.fecha = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
        try
        {
            RegistroGuardado.EscribirSeguro(Archivo(Actual.ranura), JsonUtility.ToJson(Actual, true));
        }
        catch (Exception e) { Debug.LogWarning("[Partida] No se pudo guardar: " + e.Message); }
    }

    // Al pasar de nivel: la partida sigue en el nivel nuevo, desde su entrada.
    public static void GuardarCambioNivel(string escenaNueva)
    {
        if (Actual == null) return;
        Actual.enHoguera = false;
        Actual.lugar = "Inicio del nivel";
        Guardar(escena: escenaNueva);
    }

    // ------------------------------------------------------------------ Banderas

    // Cosas hechas (cofre abierto, jefe vencido...). Sin partida (probando una
    // escena en el editor) solo duran mientras se juega.
    private static readonly HashSet<string> banderasSesion = new HashSet<string>();

    public static bool Bandera(string clave)
    {
        if (Actual != null) return Actual.banderas.Contains(clave);
        return banderasSesion.Contains(clave);
    }

    public static void PonerBandera(string clave)
    {
        if (Actual != null)
        {
            if (!Actual.banderas.Contains(clave)) Actual.banderas.Add(clave);
            Guardar();
            return;
        }
        banderasSesion.Add(clave);
    }

    // ------------------------------------------------------------------ Pistas y manchas

    // Igual que las banderas: sin partida solo duran mientras se juega.
    private static readonly List<PistaLeida> pistasSesion = new List<PistaLeida>();
    private static readonly List<ManchaSangre> manchasSesion = new List<ManchaSangre>();

    public static List<PistaLeida> Pistas => Actual != null ? (Actual.pistas ??= new List<PistaLeida>()) : pistasSesion;
    public static List<ManchaSangre> Manchas => Actual != null ? (Actual.manchas ??= new List<ManchaSangre>()) : manchasSesion;

    public static bool PistaLeidaYa(string escena, string id) => Pistas.Any(p => p.escena == escena && p.id == id);

    // Apunta la pista (o la actualiza si se cambio su texto) y guarda.
    public static void LeerPista(string escena, string id, string titulo, string texto)
    {
        PistaLeida p = Pistas.FirstOrDefault(x => x.escena == escena && x.id == id);
        if (p == null) Pistas.Add(p = new PistaLeida { escena = escena, id = id });
        p.titulo = titulo;
        p.texto = texto;
        Guardar();
    }

    // Las manchas se guardan junto con lo demas (al descansar, morir, etc.).
    public static void AnadirMancha(ManchaSangre m, int maximoPorNivel)
    {
        List<ManchaSangre> l = Manchas;
        l.Add(m);
        // Por encima del limite se quitan las mas antiguas de ese nivel.
        while (l.Count(x => x.escena == m.escena) > maximoPorNivel)
            l.Remove(l.First(x => x.escena == m.escena));
    }

    // ------------------------------------------------------------------ Reloj y guardado automatico

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Iniciar()
    {
        GameObject go = new GameObject("RelojPartida");
        UnityEngine.Object.DontDestroyOnLoad(go);
        go.hideFlags = HideFlags.HideInHierarchy;
        go.AddComponent<Reloj>();
        Hoguera.AlDescansar += () =>
        {
            Hoguera h = Hoguera.UltimaUsada;
            if (h != null) Guardar(h.NombreLugar, h.PuntoReaparicion);
            else Guardar();
        };
        ArenaJefe.AlVencer += () => PonerBandera("jefe_" + SceneManager.GetActiveScene().name);
        GameManager.AlReaparecerPlayer += () => Guardar();
        // Las mejoras de la hoguera (estadisticas) se guardan al momento.
        Progreso.AlSubirNivel += () => Guardar();
    }

    // Cuenta el tiempo jugado (no en el menu, ni en pausa) y guarda de vez en cuando.
    private class Reloj : MonoBehaviour
    {
        private float siguienteGuardado;

        private void Update()
        {
            if (Actual == null || SceneManager.GetActiveScene().buildIndex == 0 || Time.timeScale <= 0f) return;
            Actual.segundosJugados += Time.unscaledDeltaTime;
            if (Time.unscaledTime >= siguienteGuardado)
            {
                siguienteGuardado = Time.unscaledTime + 60f;
                Guardar();
            }
        }

        private void OnApplicationQuit() => Guardar();
    }

    public static string Tiempo(float segundos)
    {
        int s = Mathf.FloorToInt(segundos);
        return $"{s / 3600}:{s / 60 % 60:00}:{s % 60:00}";
    }
}
