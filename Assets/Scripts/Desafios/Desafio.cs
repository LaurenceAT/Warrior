using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// El desafio en curso (desde el menu de Desafios). Mientras dura:
//   - No hay partida activa: nada se guarda en tu partida (ni en PlayerPrefs).
//   - El personaje empieza de cero: nivel 1, sin almas ni mejoras.
//   - Solo se guarda (en Globales) si se completa: el tiempo y los logros.
// La escena del jefe se monta sola en modo desafio (ModoDesafio).
public static class Desafio
{
    public static bool Activo { get; private set; }
    public static FichaJefe Ficha { get; private set; }
    public static bool Dificil { get; private set; }
    public static float Segundos { get; set; }
    public static int Muertes { get; set; }
    public static bool Completado { get; private set; }
    // Al volver al menu principal, abre directamente la pantalla de Desafios.
    public static bool AbrirMenuDesafios { get; set; }

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
        // Personaje nuevo y nada de la partida normal.
        Partida.Descargar();
        Progreso.Aislado = true;
        Progreso.Reiniciar();
        Equipo.Reiniciar();
        Comprados.Clear();
        Deseado = null;
        Ficha = f;
        Dificil = dificil && DificilDesbloqueado(f);
        Segundos = 0f;
        Muertes = 0;
        Completado = false;
        Activo = true;
        PantallaCarga.Cargar(f.escena, 1.2f);
    }

    // Jefe vencido: se apunta (tiempo y logros) y sale la pantalla final.
    public static void Completar()
    {
        if (!Activo || Completado) return;
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
        Progreso.Aislado = false;
        Partida.Descargar();
        Equipo.Reiniciar();
        Comprados.Clear();
        Deseado = null;
        Time.timeScale = 1f;
        if (GameManager.Instance != null && GameManager.Instance.IsPaused) GameManager.Instance.ResumeGame();
        AbrirMenuDesafios = true;
        PantallaCarga.Cargar(0);
    }
}
