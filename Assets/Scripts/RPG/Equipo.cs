using System;
using UnityEngine;

// Mejoras del equipo, al estilo de Elden Ring:
//   - Piedras de forja: mejoran la espada (mas dano).
//   - Lagrimas sagradas: mejoran los frascos (el de sangre cura mas y el de
//     mana devuelve mas).
// Los objetos salen de cofres escondidos y de los jefes, y se gastan en la
// hoguera (Mejorar equipamiento). Todo se guarda en la partida: cuantos objetos
// quedan y a que nivel estan la espada y los frascos. Sin partida (probando una
// escena desde el editor) solo dura mientras se juega.
// Los numeros (cuanto mejora cada nivel, el coste, el maximo) estan en
// Resources/AjustesProgreso.
public static class Equipo
{
    public enum Objeto { PiedraForja = 0, LagrimaSagrada = 1 }

    public static event Action AlCambiar;

    private static AjustesProgreso A => AjustesProgreso.Get();

    // Sin partida: valores de la sesion.
    private static int sesionPiedras, sesionLagrimas, sesionEspada, sesionFrascos;

    public static int Cantidad(Objeto o)
    {
        Partida.Datos d = Partida.Actual;
        if (o == Objeto.PiedraForja) return d != null ? d.piedrasForja : sesionPiedras;
        return d != null ? d.lagrimas : sesionLagrimas;
    }

    public static int NivelEspada => Partida.Actual != null ? Partida.Actual.nivelEspada : sesionEspada;
    public static int NivelFrascos => Partida.Actual != null ? Partida.Actual.nivelFrascos : sesionFrascos;
    public static int NivelMaximoEspada => Mathf.Max(0, A.espadaNivelMaximo);
    public static int NivelMaximoFrascos => Mathf.Max(0, A.frascosNivelMaximo);
    public static int CosteEspada => Mathf.Max(0, A.espadaCoste);
    public static int CosteFrascos => Mathf.Max(0, A.frascosCoste);

    // Dano de la espada a un nivel (1 = sin mejorar).
    public static float MultiplicadorEspadaEn(int nivel) => 1f + A.espadaDanoPorNivel * Mathf.Max(0, nivel);
    public static float MultiplicadorEspada => MultiplicadorEspadaEn(NivelEspada);

    // Lo que cura / devuelve cada frasco a un nivel (parte del maximo).
    public static float CuraFrascoEn(int nivel) => Mathf.Clamp01(A.frascoVidaBase + A.frascoVidaPorNivel * Mathf.Max(0, nivel));
    public static float ManaFrascoEn(int nivel) => Mathf.Clamp01(A.frascoManaBase + A.frascoManaPorNivel * Mathf.Max(0, nivel));
    public static float CuraFrasco => CuraFrascoEn(NivelFrascos);
    public static float ManaFrasco => ManaFrascoEn(NivelFrascos);

    public static string Nombre(Objeto o) => o == Objeto.PiedraForja ? "Piedra de forja" : "Lágrima sagrada";

    public static string Descripcion(Objeto o) => o == Objeto.PiedraForja
        ? "Se usa en la hoguera para mejorar la espada."
        : "Se usa en la hoguera para mejorar los frascos de sangre y de maná.";

    public static string ClaveIcono(Objeto o) => o == Objeto.PiedraForja ? "objeto_piedra" : "objeto_lagrima";

    public static void Sumar(Objeto o, int cantidad = 1)
    {
        if (cantidad == 0) return;
        Partida.Datos d = Partida.Actual;
        if (o == Objeto.PiedraForja)
        {
            if (d != null) d.piedrasForja = Mathf.Max(0, d.piedrasForja + cantidad);
            else sesionPiedras = Mathf.Max(0, sesionPiedras + cantidad);
        }
        else
        {
            if (d != null) d.lagrimas = Mathf.Max(0, d.lagrimas + cantidad);
            else sesionLagrimas = Mathf.Max(0, sesionLagrimas + cantidad);
        }
        Partida.Guardar();
        AlCambiar?.Invoke();
    }

    public static bool PuedeMejorarEspada => NivelEspada < NivelMaximoEspada && Cantidad(Objeto.PiedraForja) >= CosteEspada;
    public static bool PuedeMejorarFrascos => NivelFrascos < NivelMaximoFrascos && Cantidad(Objeto.LagrimaSagrada) >= CosteFrascos;

    public static bool MejorarEspada()
    {
        if (!PuedeMejorarEspada) return false;
        if (Partida.Actual != null) { Partida.Actual.piedrasForja -= CosteEspada; Partida.Actual.nivelEspada++; }
        else { sesionPiedras -= CosteEspada; sesionEspada++; }
        Partida.Guardar();
        AlCambiar?.Invoke();
        return true;
    }

    public static bool MejorarFrascos()
    {
        if (!PuedeMejorarFrascos) return false;
        if (Partida.Actual != null) { Partida.Actual.lagrimas -= CosteFrascos; Partida.Actual.nivelFrascos++; }
        else { sesionLagrimas -= CosteFrascos; sesionFrascos++; }
        Partida.Guardar();
        AlCambiar?.Invoke();
        return true;
    }

    // Empieza de cero (partida nueva y pruebas).
    public static void Reiniciar()
    {
        sesionPiedras = sesionLagrimas = sesionEspada = sesionFrascos = 0;
        AlCambiar?.Invoke();
    }
}
