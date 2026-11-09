using System.Collections.Generic;
using UnityEngine;

// Reparte los papeles del Duo Martial Hero (los dos con "duo" en su ficha):
//   - Normalmente ataca uno (como el minijefe) y el otro se aleja y lanza ondas.
//   - Tras cada combo del que ataca, cambian de papel o, a veces (probAmbos),
//     atacan los dos a la vez; despues de ese asalto doble, vuelven a turnarse.
//   - Si queda uno solo, siempre ataca.
// No guarda nada: se arma con los que esten vivos en la escena.
public static class DirectorDuo
{
    public enum Rol { Atacante, Ondas }

    private static readonly List<JefeMartialHero> miembros = new List<JefeMartialHero>();
    private static readonly HashSet<JefeMartialHero> hechosAmbos = new HashSet<JefeMartialHero>();
    private static JefeMartialHero atacante;
    private static bool ambos;

    // Lo leen las pruebas.
    public static bool Ambos => ambos;
    public static JefeMartialHero Atacante => atacante;
    public static int AsaltosDobles { get; private set; }
    public static int Cambios { get; private set; }

    private static bool Vivo(JefeMartialHero j) => j != null && j.isActiveAndEnabled && j.EstadoActual != JefeMartialHero.EstadoMartial.Muerte;

    private static void Limpiar()
    {
        miembros.RemoveAll(j => !Vivo(j));
        hechosAmbos.RemoveWhere(j => !Vivo(j));
        if (!Vivo(atacante)) atacante = miembros.Count > 0 ? miembros[0] : null;
        if (miembros.Count < 2) ambos = false;
    }

    public static void Entrar(JefeMartialHero j)
    {
        Limpiar();
        if (miembros.Count == 0) { ambos = false; AsaltosDobles = Cambios = 0; }
        if (!miembros.Contains(j)) miembros.Add(j);
        if (atacante == null) atacante = j;
    }

    public static Rol RolDe(JefeMartialHero j)
    {
        Limpiar();
        if (miembros.Count < 2 || ambos || j == atacante) return Rol.Atacante;
        return Rol.Ondas;
    }

    // Un combo acabado: decide el siguiente asalto.
    public static void FinCombo(JefeMartialHero j, float probAmbos)
    {
        Limpiar();
        if (miembros.Count < 2) return;
        if (ambos)
        {
            hechosAmbos.Add(j);
            if (hechosAmbos.Count < miembros.Count) return;
            ambos = false;
            hechosAmbos.Clear();
            atacante = Otro(j);
            Cambios++;
            return;
        }
        if (j != atacante) return;
        if (Random.value < probAmbos)
        {
            ambos = true;
            hechosAmbos.Clear();
            AsaltosDobles++;
            return;
        }
        atacante = Otro(j);
        Cambios++;
    }

    private static JefeMartialHero Otro(JefeMartialHero j)
    {
        foreach (JefeMartialHero m in miembros) if (m != j) return m;
        return j;
    }
}
