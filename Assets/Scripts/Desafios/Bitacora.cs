using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

// Lo que el jugador va descubriendo de los jefes (fichas de Desafios) y de los
// enemigos normales (Bestiario). Todo va a globales.json, nunca a la partida.
// Sin avisos durante el combate: al morir o completar un desafio sale un
// resumen corto, y en los menus un punto de "nuevo" en lo recien descubierto.
//
// Quien avisa:
//   - Los jefes, al empezar cada ataque y al cambiar de fase (JefeBase).
//   - Las arenas, al entrar (intento). La victoria llega por ArenaJefe.AlVencer.
//   - El player, al recibir un golpe y al morir (que ataque lo mato).
//   - EnemyHealth, en cada golpe (elemento) y al morir (Bestiario).
//   - EnemigoBase, al descubrir al player (Bestiario).
// La condicion de cada pieza esta en FichaJefe y FichaBestiario.
public static class Bitacora
{
    public enum Seccion { Ataques, Debilidades, Fases, Historia, Estadisticas }

    // ------------------------------------------------------------------ Claves

    public static string PrefijoJefe(FichaJefe f) => $"j:{f.id}:";
    public static string ClaveAtaque(FichaJefe f, string ataque) => $"j:{f.id}:a:{ataque}";
    public static string ClaveConsejo(FichaJefe f, string ataque) => $"j:{f.id}:c:{ataque}";
    public static string ClaveFase(FichaJefe f, int fase) => $"j:{f.id}:f:{fase}";
    public static string ClaveElemento(FichaJefe f, Elemento e) => $"j:{f.id}:e:{(int)e}";
    public static string ClaveHistoria(FichaJefe f, string fragmento) => $"j:{f.id}:h:{fragmento}";

    public const string PrefijoBestiario = "b:";
    public static string ClaveVisto(string enemigo) => $"b:{enemigo}:v";
    public static string ClaveDerrotado(string enemigo) => $"b:{enemigo}:d";
    public static string ClavePatrones(string enemigo) => $"b:{enemigo}:p";
    public static string ClaveElemento(string enemigo, Elemento e) => $"b:{enemigo}:e:{(int)e}";
    public static string ContadorDerrotas(string enemigo) => $"b:{enemigo}";

    // El jefe con el que se pelea ahora y su ataque en curso (pantalla de muerte).
    public static FichaJefe JefeEnCombate => ArenaJefe.EnCombate ? jefeActual : null;
    public static FichaJefe.Ataque AtaqueEnCurso => JefeEnCombate != null && ataqueActual != null ? jefeActual.BuscarAtaque(ataqueActual) : null;

    public static bool Tiene(string clave) => Globales.Descubierto(clave);
    public static bool Nuevo(string clave) => Globales.Descubierto(clave) && !Globales.Visto(clave);

    // ------------------------------------------------------------------ Estado del combate

    private static FichaJefe jefeActual;
    // El ataque en curso y el que dio el ultimo golpe al player (el que lo mato).
    private static string ataqueActual, ataqueDelGolpe;
    // Descubierto desde el ultimo resumen (solo de jefes).
    private static readonly List<string> nuevos = new List<string>();
    private static bool iniciado;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Iniciar()
    {
        if (iniciado) return;
        iniciado = true;
        PlayerControler.AlMorir += MuertePlayer;
        ArenaJefe.AlVencer += () => Victoria(FichaJefe.DeEscena(SceneManager.GetActiveScene().name), Desafio.Activo && Desafio.Dificil);
    }

    private static bool Anotar(string clave, bool guardar = true)
    {
        if (!Globales.Descubrir(clave, guardar)) return false;
        if (clave.StartsWith("j:")) nuevos.Add(clave);
        return true;
    }

    // ------------------------------------------------------------------ Jefes

    // El jefe empieza un ataque (los instakills: al mostrar su aviso).
    public static void Ataque(FichaJefe f, string ataque)
    {
        if (f == null || string.IsNullOrEmpty(ataque)) return;
        jefeActual = f;
        ataqueActual = ataque;
        if (f.BuscarAtaque(ataque) != null) Anotar(ClaveAtaque(f, ataque));
    }

    // Llega a una fase (1, 2 o 3).
    public static void Fase(FichaJefe f, int fase)
    {
        if (f == null || fase < 1) return;
        jefeActual = f;
        Anotar(ClaveFase(f, fase));
        if (fase == 2) Historia(f, FichaJefe.Cuando.Fase2);
        if (fase == 3) Historia(f, FichaJefe.Cuando.Fase3);
    }

    // Entra en la arena (cada intento).
    public static void Intento(FichaJefe f)
    {
        if (f == null) return;
        Migrar();
        jefeActual = f;
        ataqueActual = ataqueDelGolpe = null;
        Fase(f, 1);
        Historia(f, FichaJefe.Cuando.PrimerIntento);
        if (Desafio.Activo && Desafio.Ficha == f) Globales.SumarIntento(f.id, Desafio.Dificil);
    }

    // El player recibe un golpe: se apunta el ataque en curso por si lo mata.
    public static void GolpeRecibido()
    {
        if (ArenaJefe.EnCombate && jefeActual != null && ataqueActual != null) ataqueDelGolpe = ataqueActual;
    }

    // El player muere: el consejo del ataque que lo mato, y la muerte en las
    // estadisticas del desafio.
    private static void MuertePlayer()
    {
        Globales.GuardarPendiente();
        if (!ArenaJefe.EnCombate || jefeActual == null || Desafio.Reiniciando) return;
        FichaJefe f = jefeActual;
        string a = ataqueDelGolpe ?? ataqueActual;
        if (a != null && f.BuscarAtaque(a) != null) Anotar(ClaveConsejo(f, a));
        if (Desafio.Activo && Desafio.Ficha == f) Globales.SumarMuerte(f.id, Desafio.Dificil);
        ataqueDelGolpe = null;
    }

    // Jefe vencido (partida normal o desafio): todos los consejos y la historia.
    public static void Victoria(FichaJefe f, bool dificil)
    {
        if (f == null) return;
        foreach (FichaJefe.Ataque a in f.ataques) Anotar(ClaveConsejo(f, a.id), false);
        Historia(f, FichaJefe.Cuando.PrimeraVictoria, false);
        if (dificil) Historia(f, FichaJefe.Cuando.VictoriaDificil, false);
        Globales.GuardarPendiente();
        jefeActual = null;
        ataqueActual = ataqueDelGolpe = null;
    }

    private static void Historia(FichaJefe f, FichaJefe.Cuando cuando, bool guardar = true)
    {
        foreach (FichaJefe.Fragmento h in f.fragmentosHistoria)
            if (h.cuando == cuando && !string.IsNullOrEmpty(h.id)) Anotar(ClaveHistoria(f, h.id), guardar);
    }

    // Desafio empezado o reiniciado: nada pendiente de resumir.
    public static void Limpiar()
    {
        nuevos.Clear();
        jefeActual = null;
        ataqueActual = ataqueDelGolpe = null;
    }

    // ------------------------------------------------------------------ Golpes a enemigos y jefes

    // Cada golpe del player (con o sin elemento). Un enemigo normal queda "visto";
    // el elemento se revela (enemigo o jefe).
    public static void Golpe(EnemyHealth h, Elemento e)
    {
        if (h == null) return;
        DefinicionEnemigo d = h.Definicion;
        if (d != null)
        {
            Anotar(ClaveVisto(d.name));
            if (e != Elemento.Ninguno) Anotar(ClaveElemento(d.name, e));
            return;
        }
        if (e == Elemento.Ninguno || h.GetComponent<JefeBase>() == null) return;
        FichaJefe f = FichaJefe.DeEscena(h.gameObject.scene.name);
        if (f != null) Anotar(ClaveElemento(f, e));
    }

    // Un enemigo normal te detecta.
    public static void Visto(EnemyHealth h)
    {
        DefinicionEnemigo d = h != null ? h.Definicion : null;
        if (d != null) Anotar(ClaveVisto(d.name));
    }

    // Un enemigo normal muere: cuenta y, al llegar a su numero, sus patrones.
    public static void Derrotado(EnemyHealth h)
    {
        DefinicionEnemigo d = h != null ? h.Definicion : null;
        if (d == null) return;
        string id = d.name;
        Anotar(ClaveVisto(id));
        Anotar(ClaveDerrotado(id));
        int n = Globales.Sumar(ContadorDerrotas(id));
        FichaBestiario.Entrada entrada = FichaBestiario.Get() != null ? FichaBestiario.Get().Buscar(id) : null;
        if (n >= (entrada != null ? Mathf.Max(1, entrada.derrotasParaPatrones) : 5)) Anotar(ClavePatrones(id));
    }

    public static int Derrotas(string enemigo) => Globales.Cuenta(ContadorDerrotas(enemigo));

    // ------------------------------------------------------------------ Menu: secciones y "nuevo"

    // Lo descubierto de una seccion de la ficha de un jefe.
    public static IEnumerable<string> Claves(FichaJefe f, Seccion s)
    {
        switch (s)
        {
            case Seccion.Ataques:
                foreach (FichaJefe.Ataque a in f.ataques)
                {
                    string ka = ClaveAtaque(f, a.id);
                    if (!Tiene(ka)) continue;
                    yield return ka;
                    string kc = ClaveConsejo(f, a.id);
                    if (Tiene(kc)) yield return kc;
                }
                break;
            case Seccion.Debilidades:
                foreach (Elemento e in Elementos.Todos) { string k = ClaveElemento(f, e); if (Tiene(k)) yield return k; }
                break;
            case Seccion.Fases:
                for (int i = 1; i <= 3; i++) { string k = ClaveFase(f, i); if (Tiene(k)) yield return k; }
                break;
            case Seccion.Historia:
                foreach (FichaJefe.Fragmento h in f.fragmentosHistoria) { string k = ClaveHistoria(f, h.id); if (Tiene(k)) yield return k; }
                break;
        }
    }

    public static bool HayNuevo(FichaJefe f, Seccion s) => f != null && Claves(f, s).Any(k => !Globales.Visto(k));

    public static bool HayNuevo(FichaJefe f) =>
        f != null && (HayNuevo(f, Seccion.Ataques) || HayNuevo(f, Seccion.Debilidades) || HayNuevo(f, Seccion.Fases) || HayNuevo(f, Seccion.Historia));

    public static void MarcarVisto(FichaJefe f, Seccion s)
    {
        if (f != null) Globales.MarcarVistos(Claves(f, s).ToList());
    }

    // Las estadisticas salen tras el primer intento.
    public static bool HayEstadisticas(FichaJefe f)
    {
        Globales.Registro n = Globales.Desafio(f.id, false), d = Globales.Desafio(f.id, true);
        return (n != null && (n.intentos > 0 || n.completado)) || (d != null && (d.intentos > 0 || d.completado));
    }

    // Bestiario: lo descubierto de un enemigo.
    public static IEnumerable<string> Claves(string enemigo)
    {
        foreach (string k in new[] { ClaveVisto(enemigo), ClaveDerrotado(enemigo), ClavePatrones(enemigo) })
            if (Tiene(k)) yield return k;
        foreach (Elemento e in Elementos.Todos) { string k = ClaveElemento(enemigo, e); if (Tiene(k)) yield return k; }
    }

    public static bool HayNuevo(string enemigo) => Claves(enemigo).Any(k => !Globales.Visto(k));

    public static void MarcarVisto(string enemigo) => Globales.MarcarVistos(Claves(enemigo).ToList());

    // ------------------------------------------------------------------ Resumen

    // "Nuevo en la ficha de X: 2 ataques · 1 consejo..." (y se vacia). Vacio si no hay nada.
    public static string TomarResumen(FichaJefe f)
    {
        if (f == null) { nuevos.Clear(); return ""; }
        string p = PrefijoJefe(f);
        List<string> mias = nuevos.Where(k => k.StartsWith(p)).ToList();
        nuevos.Clear();
        if (mias.Count == 0) return "";
        var partes = new List<string>();
        void Parte(string tipo, string uno, string varios)
        {
            int n = mias.Count(k => k.StartsWith(p + tipo));
            if (n > 0) partes.Add(n == 1 ? "1 " + uno : n + " " + varios);
        }
        Parte("a:", "ataque", "ataques");
        Parte("c:", "consejo", "consejos");
        Parte("f:", "fase", "fases");
        Parte("e:", "reacción a un elemento", "reacciones a elementos");
        Parte("h:", "fragmento de historia", "fragmentos de historia");
        return partes.Count == 0 ? "" : $"Nuevo en la ficha de {f.nombre}: {string.Join("  ·  ", partes)}";
    }

    // ------------------------------------------------------------------ Compatibilidad y pruebas

    // Jefes con el desafio completado antes de esta version: toda su informacion
    // desbloqueada (una sola vez).
    public static void Migrar()
    {
        if (Globales.Marca("bitacora_v1")) return;
        foreach (FichaJefe f in FichaJefe.Todas())
            if (Globales.Completado(f.id, false) || Globales.Completado(f.id, true)) DesbloquearTodo(f, false);
        Globales.PonerMarca("bitacora_v1", true);
    }

    public static void DesbloquearTodo(FichaJefe f, bool resumen = false)
    {
        if (f == null) return;
        void A(string k) { if (Globales.Descubrir(k, false) && resumen) nuevos.Add(k); }
        foreach (FichaJefe.Ataque a in f.ataques) { A(ClaveAtaque(f, a.id)); A(ClaveConsejo(f, a.id)); }
        for (int i = 1; i <= f.NumeroFases; i++) A(ClaveFase(f, i));
        foreach (Elemento e in Elementos.Todos) A(ClaveElemento(f, e));
        foreach (FichaJefe.Fragmento h in f.fragmentosHistoria) A(ClaveHistoria(f, h.id));
        Globales.GuardarPendiente();
    }

    public static void BloquearTodo(FichaJefe f)
    {
        if (f == null) return;
        Globales.Olvidar(PrefijoJefe(f));
        Globales.OlvidarEstadisticas(f.id);
    }

    public static void DesbloquearBestiario()
    {
        FichaBestiario b = FichaBestiario.Get();
        if (b == null) return;
        foreach (FichaBestiario.Entrada en in b.Visibles)
        {
            string id = en.Id;
            Globales.Descubrir(ClaveVisto(id), false);
            Globales.Descubrir(ClaveDerrotado(id), false);
            Globales.Descubrir(ClavePatrones(id), false);
            foreach (Elemento e in Elementos.Todos) Globales.Descubrir(ClaveElemento(id, e), false);
            int falta = Mathf.Max(1, en.derrotasParaPatrones) - Derrotas(id);
            if (falta > 0) Globales.Sumar(ContadorDerrotas(id), falta);
        }
        Globales.GuardarPendiente();
    }

    public static void BloquearBestiario() => Globales.Olvidar(PrefijoBestiario);

    // Todo lo descubierto (jefes y Bestiario) vuelve a cero. Los logros y los tiempos se quedan.
    public static void Reiniciar()
    {
        Globales.Olvidar("j:");
        Globales.Olvidar(PrefijoBestiario);
        foreach (FichaJefe f in FichaJefe.Todas()) Globales.OlvidarEstadisticas(f.id);
        Limpiar();
    }
}
