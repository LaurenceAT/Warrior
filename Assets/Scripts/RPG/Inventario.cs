using System;
using System.Linq;

// Objetos de mejora "por aplicar", en TODOS los niveles: lo que dan los cofres,
// los jefes, el Mimic, el frasco del camino secreto o el totem no se aplica al
// momento; va aqui y se aplica en la hoguera ("Aplicar mejoras").
//   - En un desafio: el inventario del desafio (Desafio).
//   - En la partida normal: la partida guardada (Equipo: piedras y lagrimas sin
//     gastar y los frascos pendientes). Las mejoras ya aplicadas por el sistema
//     viejo se respetan tal cual.
public static class Inventario
{
    public const string Aviso = "Guardado en tu inventario. Aplícalo en la hoguera.";

    public static Equipo.Objeto[] Objetos => Desafio.ObjetosMejora;

    // Cualquier cambio (recoger, comprar, aplicar).
    public static event Action AlCambiar;

    static Inventario()
    {
        Equipo.AlCambiar += () => AlCambiar?.Invoke();
        Desafio.AlCambiarInventario += () => AlCambiar?.Invoke();
    }

    public static int PorAplicar(Equipo.Objeto o) => Desafio.Activo ? Desafio.PorAplicar(o) : Equipo.Pendientes(o);

    public static int Total => Objetos.Sum(PorAplicar);

    public static void Guardar(Equipo.Objeto o, int cantidad = 1)
    {
        if (Desafio.Activo) Desafio.GuardarObjeto(o, cantidad);
        else Equipo.GuardarPendiente(o, cantidad);
    }

    // Aplica uno. False si no hay o ya esta al maximo.
    public static bool Aplicar(Equipo.Objeto o) => Desafio.Activo ? Desafio.Aplicar(o) : Equipo.AplicarPendiente(o);
}
