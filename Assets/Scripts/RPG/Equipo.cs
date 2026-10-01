using System;
using UnityEngine;

// Mejoras del equipo, al estilo de Elden Ring:
//   - Piedras de forja: mejoran la espada (mas dano).
//   - Lagrimas carmesi: mejoran el frasco de sangre (cura mas).
//   - Lagrimas celestes: mejoran el frasco de mana (devuelve mas).
//   Las tres se gastan en la hoguera (Mejorar equipamiento), nivel a nivel.
//   - Frasco de sangre / Frasco de mana: +1 carga maxima, al momento.
// La antigua Lagrima sagrada (mejoraba los dos frascos a la vez) ya no existe:
// si algo la da (datos viejos), cuenta como una carmesi y una celeste.
// Los objetos salen de cofres escondidos, de los jefes y del totem de los
// desafios. Todo se guarda en la partida. Sin partida (probando una escena
// desde el editor, o en un desafio) solo dura mientras se juega.
// Los numeros (cuanto mejora cada nivel, el coste, el maximo) estan en
// Resources/AjustesProgreso.
public static class Equipo
{
    // Los numeros se guardan en escenas y datos: no cambiar los que ya existen.
    public enum Objeto { PiedraForja = 0, LagrimaSagrada = 1, LagrimaCarmesi = 2, LagrimaCeleste = 3, FrascoSangre = 4, FrascoMana = 5 }

    public static event Action AlCambiar;

    private static AjustesProgreso A => AjustesProgreso.Get();

    // Sin partida: valores de la sesion.
    private static int sesionPiedras, sesionCarmesi, sesionCeleste, sesionEspada, sesionCuracion, sesionMana, sesionFrascoSangre, sesionFrascoMana;

    public static int Cantidad(Objeto o)
    {
        Partida.Datos d = Partida.Actual;
        switch (o)
        {
            case Objeto.PiedraForja: return d != null ? d.piedrasForja : sesionPiedras;
            case Objeto.LagrimaCarmesi: return d != null ? d.lagrimasCuracion : sesionCarmesi;
            case Objeto.LagrimaCeleste: return d != null ? d.lagrimasMana : sesionCeleste;
            case Objeto.FrascoSangre: return d != null ? d.frascosSangre : sesionFrascoSangre;
            case Objeto.FrascoMana: return d != null ? d.frascosMana : sesionFrascoMana;
            default: return 0;
        }
    }

    public static int NivelEspada => Partida.Actual != null ? Partida.Actual.nivelEspada : sesionEspada;
    public static int NivelCuracion => Partida.Actual != null ? Partida.Actual.nivelCuracion : sesionCuracion;
    public static int NivelMana => Partida.Actual != null ? Partida.Actual.nivelMana : sesionMana;
    public static int NivelMaximoEspada => Mathf.Max(0, A.espadaNivelMaximo);
    public static int NivelMaximoCuracion => Mathf.Max(0, A.frascosNivelMaximo);
    public static int NivelMaximoMana => Mathf.Max(0, A.manaNivelMaximo);
    public static int CosteEspada => Mathf.Max(0, A.espadaCoste);
    public static int CosteFrascos => Mathf.Max(0, A.frascosCoste);

    // Cargas extra de cada frasco (sobre las de la escena).
    public static int FrascosSangreExtra => Mathf.Min(Cantidad(Objeto.FrascoSangre), Mathf.Max(0, A.frascosExtraMaximo));
    public static int FrascosManaExtra => Mathf.Min(Cantidad(Objeto.FrascoMana), Mathf.Max(0, A.frascosExtraMaximo));

    // Dano de la espada a un nivel (1 = sin mejorar).
    public static float MultiplicadorEspadaEn(int nivel) => 1f + A.espadaDanoPorNivel * Mathf.Max(0, nivel);
    public static float MultiplicadorEspada => MultiplicadorEspadaEn(NivelEspada);

    // Lo que cura / devuelve cada frasco a un nivel (parte del maximo).
    public static float CuraFrascoEn(int nivel) => Mathf.Clamp01(A.frascoVidaBase + A.frascoVidaPorNivel * Mathf.Max(0, nivel));
    public static float ManaFrascoEn(int nivel) => Mathf.Clamp01(A.frascoManaBase + A.frascoManaPorNivel * Mathf.Max(0, nivel));
    public static float CuraFrasco => CuraFrascoEn(NivelCuracion);
    public static float ManaFrasco => ManaFrascoEn(NivelMana);

    public static string Nombre(Objeto o)
    {
        switch (o)
        {
            case Objeto.PiedraForja: return "Piedra de forja";
            case Objeto.LagrimaCarmesi: return "Lágrima carmesí";
            case Objeto.LagrimaCeleste: return "Lágrima celeste";
            case Objeto.FrascoSangre: return "Frasco de sangre";
            case Objeto.FrascoMana: return "Frasco de maná";
            default: return "Lágrima sagrada";
        }
    }

    public static string Descripcion(Objeto o)
    {
        switch (o)
        {
            case Objeto.PiedraForja: return "Se usa en la hoguera para mejorar la espada.";
            case Objeto.LagrimaCarmesi: return "Se usa en la hoguera: el frasco de sangre cura más.";
            case Objeto.LagrimaCeleste: return "Se usa en la hoguera: el frasco de maná devuelve más.";
            case Objeto.FrascoSangre: return "Una carga más para el frasco de sangre.";
            case Objeto.FrascoMana: return "Una carga más para el frasco de maná.";
            default: return "Mejora los dos frascos.";
        }
    }

    public static string ClaveIcono(Objeto o)
    {
        switch (o)
        {
            case Objeto.PiedraForja: return "objeto_piedra";
            case Objeto.LagrimaCarmesi: return "objeto_lagrima_curacion";
            case Objeto.LagrimaCeleste: return "objeto_lagrima_mana";
            case Objeto.FrascoSangre: return "objeto_frasco_sangre";
            case Objeto.FrascoMana: return "objeto_frasco_mana";
            default: return "objeto_lagrima";
        }
    }

    public static void Sumar(Objeto o, int cantidad = 1)
    {
        if (cantidad == 0) return;
        // La vieja lagrima mejoraba los dos frascos: ahora son dos objetos.
        if (o == Objeto.LagrimaSagrada)
        {
            Sumar(Objeto.LagrimaCarmesi, cantidad);
            Sumar(Objeto.LagrimaCeleste, cantidad);
            return;
        }
        Partida.Datos d = Partida.Actual;
        switch (o)
        {
            case Objeto.PiedraForja: if (d != null) d.piedrasForja = Max0(d.piedrasForja + cantidad); else sesionPiedras = Max0(sesionPiedras + cantidad); break;
            case Objeto.LagrimaCarmesi: if (d != null) d.lagrimasCuracion = Max0(d.lagrimasCuracion + cantidad); else sesionCarmesi = Max0(sesionCarmesi + cantidad); break;
            case Objeto.LagrimaCeleste: if (d != null) d.lagrimasMana = Max0(d.lagrimasMana + cantidad); else sesionCeleste = Max0(sesionCeleste + cantidad); break;
            case Objeto.FrascoSangre: if (d != null) d.frascosSangre = Max0(d.frascosSangre + cantidad); else sesionFrascoSangre = Max0(sesionFrascoSangre + cantidad); break;
            case Objeto.FrascoMana: if (d != null) d.frascosMana = Max0(d.frascosMana + cantidad); else sesionFrascoMana = Max0(sesionFrascoMana + cantidad); break;
        }
        Partida.Guardar();
        AlCambiar?.Invoke();
    }

    private static int Max0(int v) => Mathf.Max(0, v);

    public static bool PuedeMejorarEspada => NivelEspada < NivelMaximoEspada && Cantidad(Objeto.PiedraForja) >= CosteEspada;
    public static bool PuedeMejorarCuracion => NivelCuracion < NivelMaximoCuracion && Cantidad(Objeto.LagrimaCarmesi) >= CosteFrascos;
    public static bool PuedeMejorarMana => NivelMana < NivelMaximoMana && Cantidad(Objeto.LagrimaCeleste) >= CosteFrascos;

    // En la hoguera: gasta el objeto y sube un nivel.
    public static bool MejorarEspada() => Mejorar(Objeto.PiedraForja, PuedeMejorarEspada, CosteEspada);
    public static bool MejorarCuracion() => Mejorar(Objeto.LagrimaCarmesi, PuedeMejorarCuracion, CosteFrascos);
    public static bool MejorarMana() => Mejorar(Objeto.LagrimaCeleste, PuedeMejorarMana, CosteFrascos);

    private static bool Mejorar(Objeto o, bool puede, int coste)
    {
        if (!puede) return false;
        Partida.Datos d = Partida.Actual;
        switch (o)
        {
            case Objeto.PiedraForja: if (d != null) { d.piedrasForja -= coste; d.nivelEspada++; } else { sesionPiedras -= coste; sesionEspada++; } break;
            case Objeto.LagrimaCarmesi: if (d != null) { d.lagrimasCuracion -= coste; d.nivelCuracion++; } else { sesionCarmesi -= coste; sesionCuracion++; } break;
            case Objeto.LagrimaCeleste: if (d != null) { d.lagrimasMana -= coste; d.nivelMana++; } else { sesionCeleste -= coste; sesionMana++; } break;
        }
        Partida.Guardar();
        AlCambiar?.Invoke();
        return true;
    }

    // La tienda del totem: la mejora se aplica al momento, sin pasar por la hoguera.
    public static bool SubirDirecto(Objeto o)
    {
        switch (o)
        {
            case Objeto.PiedraForja: if (NivelEspada >= NivelMaximoEspada) return false; Sumar(o, Mathf.Max(1, CosteEspada)); return MejorarEspada();
            case Objeto.LagrimaCarmesi: if (NivelCuracion >= NivelMaximoCuracion) return false; Sumar(o, Mathf.Max(1, CosteFrascos)); return MejorarCuracion();
            case Objeto.LagrimaCeleste: if (NivelMana >= NivelMaximoMana) return false; Sumar(o, Mathf.Max(1, CosteFrascos)); return MejorarMana();
            case Objeto.FrascoSangre:
            case Objeto.FrascoMana: Sumar(o); return true;
            default: return false;
        }
    }

    // ------------------------------------------------------------------ Por aplicar (partida normal)

    // Lo recogido que aun no se ha aplicado en la hoguera ("Aplicar mejoras").
    // Piedras y lagrimas: las que hay sin gastar. Frascos: sus pendientes.
    private static int sesionFrascoSangrePend, sesionFrascoManaPend;

    public static int Pendientes(Objeto o)
    {
        Partida.Datos d = Partida.Actual;
        switch (o)
        {
            case Objeto.PiedraForja: return Cantidad(o) / Mathf.Max(1, CosteEspada);
            case Objeto.LagrimaCarmesi:
            case Objeto.LagrimaCeleste: return Cantidad(o) / Mathf.Max(1, CosteFrascos);
            case Objeto.FrascoSangre: return d != null ? d.frascosSangrePendientes : sesionFrascoSangrePend;
            case Objeto.FrascoMana: return d != null ? d.frascosManaPendientes : sesionFrascoManaPend;
            default: return 0;
        }
    }

    // Recoger un objeto: va al inventario (no se aplica todavia).
    public static void GuardarPendiente(Objeto o, int cantidad = 1)
    {
        if (cantidad <= 0) return;
        Partida.Datos d = Partida.Actual;
        switch (o)
        {
            case Objeto.FrascoSangre:
                if (d != null) d.frascosSangrePendientes += cantidad; else sesionFrascoSangrePend += cantidad;
                Partida.Guardar();
                AlCambiar?.Invoke();
                break;
            case Objeto.FrascoMana:
                if (d != null) d.frascosManaPendientes += cantidad; else sesionFrascoManaPend += cantidad;
                Partida.Guardar();
                AlCambiar?.Invoke();
                break;
            case Objeto.PiedraForja: Sumar(o, cantidad * Mathf.Max(1, CosteEspada)); break;
            case Objeto.LagrimaCarmesi:
            case Objeto.LagrimaCeleste: Sumar(o, cantidad * Mathf.Max(1, CosteFrascos)); break;
            default: Sumar(o, cantidad); break; // la vieja lagrima sagrada: una de cada
        }
    }

    // Aplicar uno en la hoguera. False si no hay o ya esta al maximo.
    public static bool AplicarPendiente(Objeto o)
    {
        if (Pendientes(o) <= 0 || AlMaximo(o)) return false;
        Partida.Datos d = Partida.Actual;
        switch (o)
        {
            case Objeto.PiedraForja: return MejorarEspada();
            case Objeto.LagrimaCarmesi: return MejorarCuracion();
            case Objeto.LagrimaCeleste: return MejorarMana();
            case Objeto.FrascoSangre:
                if (d != null) d.frascosSangrePendientes--; else sesionFrascoSangrePend--;
                Sumar(o);
                return true;
            case Objeto.FrascoMana:
                if (d != null) d.frascosManaPendientes--; else sesionFrascoManaPend--;
                Sumar(o);
                return true;
            default: return false;
        }
    }

    // Ya al maximo contando "pendientes" mas por aplicar (el inventario de los
    // desafios): no tiene sentido conseguir otro.
    public static bool AlMaximo(Objeto o, int pendientes = 0)
    {
        switch (o)
        {
            case Objeto.PiedraForja: return NivelEspada + pendientes >= NivelMaximoEspada;
            case Objeto.LagrimaCarmesi: return NivelCuracion + pendientes >= NivelMaximoCuracion;
            case Objeto.LagrimaCeleste: return NivelMana + pendientes >= NivelMaximoMana;
            case Objeto.FrascoSangre: return FrascosSangreExtra + pendientes >= A.frascosExtraMaximo;
            case Objeto.FrascoMana: return FrascosManaExtra + pendientes >= A.frascosExtraMaximo;
            default: return false;
        }
    }

    // Empieza de cero (partida nueva, desafios y pruebas).
    public static void Reiniciar()
    {
        sesionPiedras = sesionCarmesi = sesionCeleste = sesionEspada = sesionCuracion = sesionMana = sesionFrascoSangre = sesionFrascoMana = 0;
        sesionFrascoSangrePend = sesionFrascoManaPend = 0;
        AlCambiar?.Invoke();
    }
}
