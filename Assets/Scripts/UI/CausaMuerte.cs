using UnityEngine;
using UnityEngine.SceneManagement;

// De que ha muerto el player (para la frase de la pantalla de muerte):
//   - Golpe: lo apunta PlayerControler al recibir un golpe (enemigo, jefe o el
//     ataque letal de un jefe: instakill, guardia o agarre).
//   - Estado: EstadosPlayer, cuando el sangrado, la congelacion o la quemadura hacen dano.
//   - Caida: DeadArea (el vacio).
// Tambien cuenta las muertes seguidas (se reinician al vencer a un jefe o al
// cambiar de nivel) y cuanto se ha estado vivo. No se guarda nada.
// La ultima muerte (causa, jefe y ataque) se queda en memoria hasta que alguien
// la usa (el reintento de la Cazadora): sobrevive a recargar la escena
// ("Reiniciar desafio") porque es estatica, pero no se escribe en disco.
public static class CausaMuerte
{
    public enum Tipo { Otro, Enemigo, Jefe, Letal, Caida, Sangrado, Frio, Fuego }

    public struct Resumen
    {
        public Tipo tipo;
        public string jefe;          // id de la ficha del jefe (o null)
        public int seguidas;         // muertes seguidas, contando esta
        public int almasPerdidas;
        public float segundosVivo;
    }

    // La ultima muerte, tal como fue (para quien la quiera comentar despues).
    public struct Ultima
    {
        public bool valida;
        public Tipo tipo;
        public string jefe;          // id de la ficha del jefe (o null)
        public string ataque;        // id del ataque de su ficha que dio el golpe (o null)
        public int seguidas;
    }

    private static Tipo tipo;
    private static string jefe, ataque;
    private static Ultima ultima;
    private static float inicioVida;
    private static int seguidas;
    private static string escena;
    private static bool iniciado;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Iniciar()
    {
        if (iniciado) return;
        iniciado = true;
        SceneManager.sceneLoaded += (s, m) =>
        {
            if (s.name != escena) seguidas = 0;
            escena = s.name;
            inicioVida = Time.time;
            tipo = Tipo.Otro;
        };
        GameManager.AlReaparecerPlayer += () => { inicioVida = Time.time; tipo = Tipo.Otro; };
        ArenaJefe.AlVencer += () => seguidas = 0;
        PlayerControler.AlMorir += () =>
        {
            seguidas++;
            ultima = new Ultima { valida = true, tipo = tipo, jefe = jefe, ataque = ataque, seguidas = seguidas };
        };
    }

    // La ultima muerte sin usar (y se marca como usada). valida = false si no hay.
    public static Ultima TomarUltima()
    {
        Ultima u = ultima;
        ultima.valida = false;
        return u;
    }

    public static void Golpe(Component atacante)
    {
        FichaJefe f = Bitacora.JefeEnCombate;
        jefe = f != null ? f.id : null;
        if (f != null)
        {
            FichaJefe.Ataque a = Bitacora.AtaqueEnCurso;
            ataque = a != null ? a.id : null;
            bool letal = a != null && (a.instakill || a.id == "guardia" || a.id == "agarre");
            tipo = letal ? Tipo.Letal : Tipo.Jefe;
            return;
        }
        ataque = null;
        tipo = atacante != null && (atacante is EnemigoBase || atacante.GetComponentInParent<EnemyHealth>() != null) ? Tipo.Enemigo : Tipo.Otro;
    }

    public static void Estado(EstadoPlayer e)
    {
        tipo = e == EstadoPlayer.Sangrado ? Tipo.Sangrado : e == EstadoPlayer.Congelacion ? Tipo.Frio : Tipo.Fuego;
        ataque = null;
        FichaJefe f = Bitacora.JefeEnCombate;
        jefe = f != null ? f.id : null;
    }

    public static void Caida()
    {
        tipo = Tipo.Caida;
        jefe = ataque = null;
    }

    // Lo que paso en esta muerte (se pide al mostrar la pantalla).
    public static Resumen Ahora() => new Resumen
    {
        tipo = tipo,
        jefe = jefe,
        seguidas = seguidas,
        almasPerdidas = Progreso.AlmasPerdidas,
        segundosVivo = Time.time - inicioVida,
    };

    // Solo para las pruebas.
    public static void PonerSeguidas(int n) => seguidas = n;
}
