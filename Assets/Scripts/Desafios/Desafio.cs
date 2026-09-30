using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// El desafio en curso (desde el menu de Desafios). Mientras dura:
//   - No hay partida activa: nada se guarda en tu partida (ni en PlayerPrefs).
//   - El personaje empieza de cero: nivel 1, sin almas ni mejoras.
//   - Solo se guarda (en Globales) si se completa: el tiempo y los logros. Lo
//     descubierto del jefe (Bitacora) se guarda siempre.
//   - Los objetos de mejora (cofre y totem) van a un inventario y se aplican en
//     la hoguera. Se conservan al morir y se pierden al reiniciar.
// La escena del jefe se monta sola en modo desafio (ModoDesafio).
public static class Desafio
{
    public static bool Activo { get; private set; }
    public static FichaJefe Ficha { get; private set; }
    public static bool Dificil { get; private set; }
    public static float Segundos { get; set; }
    public static int Muertes { get; set; }
    public static bool Completado { get; private set; }
    // Al volver al menu principal, abre directamente la pantalla de Desafios
    // (con el jefe de este desafio elegido).
    public static bool AbrirMenuDesafios { get; set; }
    public static FichaJefe Ultima { get; set; }
    // Se ha reiniciado desde la pausa (el dialogo de entrada se trata como un reintento).
    public static bool Reiniciado { get; private set; }
    // Entre "Reiniciar" y la escena nueva: nada cuenta (ni muertes ni tiempo).
    public static bool Reiniciando { get; set; }

    // Lo comprado en el totem y el objeto marcado (solo en este desafio).
    public static readonly HashSet<string> Comprados = new HashSet<string>();
    public static string Deseado { get; set; }

    // Modo Dificil: numeros del jefe (1 = normal).
    public static float MultVida => Activo && Dificil && Ficha != null ? Mathf.Max(0.1f, Ficha.multVida) : 1f;
    public static float MultDano => Activo && Dificil && Ficha != null ? Mathf.Max(0.1f, Ficha.multDano) : 1f;
    public static float MultVelocidad => Activo && Dificil && Ficha != null ? Mathf.Max(0.1f, Ficha.multVelocidad) : 1f;

    public static bool DificilDesbloqueado(FichaJefe f) => f != null && Globales.Completado(f.id, false);

    public static void Empezar(FichaJefe f, bool dificil)
    {
        if (f == null) return;
        Ficha = f;
        Dificil = dificil && DificilDesbloqueado(f);
        Reiniciado = false;
        DeCero();
        Activo = true;
        PantallaCarga.Cargar(f.escena, 1.2f);
    }

    // Personaje nuevo y nada de la partida normal ni del intento anterior.
    private static void DeCero()
    {
        Partida.Descargar();
        Progreso.Aislado = true;
        Progreso.Reiniciar();
        Equipo.Reiniciar();
        Comprados.Clear();
        Deseado = null;
        porAplicar.Clear();
        Segundos = 0f;
        Muertes = 0;
        Completado = false;
        Bitacora.Limpiar();
        AlCambiarInventario?.Invoke();
    }

    // "Reiniciar desafio" (menu de pausa): todo de cero sin pasar por el menu,
    // con la misma dificultad. Lo descubierto del jefe, los tiempos y los logros
    // se conservan; no cuenta como completado. La escena se vuelve a cargar: el
    // jefe, la musica, la camara, el cofre y el totem salen como al entrar.
    public static void Reiniciar()
    {
        if (!Activo || Ficha == null || PantallaCarga.Cargando) return;
        Reiniciando = true;
        Globales.GuardarPendiente();

        // Nada debe pasar mientras se funde a negro: el jefe se para y el player
        // no puede recibir dano.
        foreach (JefeBase j in Object.FindObjectsByType<JefeBase>(FindObjectsSortMode.None)) j.gameObject.SetActive(false);
        PlayerControler p = Object.FindFirstObjectByType<PlayerControler>();
        if (p != null) p.InvulnerableExterno = true;
        PeligrosJefe.LimpiarTodo();
        EfectoVisual.LimpiarAtascados();
        PlayerControler.TopeVentanaParry = -1f;
        PlayerControler.SiguienteGolpeMagico = PlayerControler.SiguienteGolpeFisico = false;
        Cinematica.Activa = false;
        if (MenuPausa.Existe) MenuPausa.Get().Mostrar(false);

        Reiniciado = true;
        DeCero();
        PantallaCarga.Cargar(Ficha.escena, 0.35f, "Reiniciando el desafío...");
    }

    // Jefe vencido: se apunta (tiempo y logros) y sale la pantalla final.
    public static void Completar()
    {
        if (!Activo || Completado || Reiniciando) return;
        Completado = true;
        string[] antes = FichaJefe.Bloqueados();
        bool record = Globales.Completar(Ficha.id, Dificil, Segundos, Muertes);
        Logros.Comprobar(Dificil ? FichaLogro.Condicion.CompletarDesafioDificil : FichaLogro.Condicion.CompletarDesafio, Ficha.id);
        // Este desafio ha despertado a un jefe secreto: una linea discreta.
        bool despierta = antes.Except(FichaJefe.Bloqueados()).Any();
        PantallaDesafio.Mostrar(Ficha, Dificil, Segundos, Muertes, record, despierta ? "Algo despertó en el bosque..." : null);
    }

    // Salir del desafio (completado o abandonado) al menu de Desafios.
    public static void Salir()
    {
        Activo = false;
        Reiniciado = Reiniciando = false;
        Progreso.Aislado = false;
        Partida.Descargar();
        Equipo.Reiniciar();
        Comprados.Clear();
        Deseado = null;
        porAplicar.Clear();
        Time.timeScale = 1f;
        if (GameManager.Instance != null && GameManager.Instance.IsPaused) GameManager.Instance.ResumeGame();
        Globales.GuardarPendiente();
        Ultima = Ficha;
        AbrirMenuDesafios = true;
        PantallaCarga.Cargar(0);
    }

    // ------------------------------------------------------------------ Inventario

    // Objetos de mejora recogidos o comprados que aun no se han aplicado.
    private static readonly Dictionary<Equipo.Objeto, int> porAplicar = new Dictionary<Equipo.Objeto, int>();
    public static event System.Action AlCambiarInventario;

    // Orden en que salen en la hoguera.
    public static readonly Equipo.Objeto[] ObjetosMejora =
        { Equipo.Objeto.PiedraForja, Equipo.Objeto.LagrimaCarmesi, Equipo.Objeto.LagrimaCeleste, Equipo.Objeto.FrascoSangre, Equipo.Objeto.FrascoMana };

    public static int PorAplicar(Equipo.Objeto o) => porAplicar.TryGetValue(o, out int n) ? n : 0;
    public static int TotalPorAplicar => porAplicar.Values.Sum();

    public static void GuardarObjeto(Equipo.Objeto o, int cantidad = 1)
    {
        if (cantidad <= 0) return;
        // La vieja lagrima sagrada son dos objetos.
        if (o == Equipo.Objeto.LagrimaSagrada)
        {
            GuardarObjeto(Equipo.Objeto.LagrimaCarmesi, cantidad);
            GuardarObjeto(Equipo.Objeto.LagrimaCeleste, cantidad);
            return;
        }
        porAplicar[o] = PorAplicar(o) + cantidad;
        AlCambiarInventario?.Invoke();
    }

    // Aplica uno en la hoguera. False si no hay o ya esta al maximo.
    public static bool Aplicar(Equipo.Objeto o)
    {
        if (PorAplicar(o) <= 0 || Equipo.AlMaximo(o)) return false;
        if (!Equipo.SubirDirecto(o)) return false;
        porAplicar[o] = PorAplicar(o) - 1;
        if (porAplicar[o] <= 0) porAplicar.Remove(o);
        AlCambiarInventario?.Invoke();
        return true;
    }

    // Solo para las pruebas (el editor).
    public static void VaciarInventario()
    {
        porAplicar.Clear();
        AlCambiarInventario?.Invoke();
    }
}
