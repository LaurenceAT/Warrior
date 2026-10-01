using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Frases de la pantalla de muerte (Resources/TextosMuerte), por categorias.
// Todo se edita en el Inspector. Se elige la categoria por la causa de la
// muerte (CausaMuerte) y nunca sale la misma frase dos veces seguidas.
// Orden de preferencia: racha de muertes (5, 10, 20...) > aviso letal >
// caida > estado > muchas almas perdidas > muerte rapida > jefe > enemigo comun
// > generales. Si una categoria esta vacia, se pasa a la siguiente.
[CreateAssetMenu(menuName = "Warrior/Textos de muerte", fileName = "TextosMuerte")]
public class TextosMuerte : ScriptableObject
{
    [Serializable]
    public class FrasesJefe
    {
        [Tooltip("Id de la ficha del jefe (shadowed_wetlands, crimson_wraith, blind_huntress).")]
        public string jefe;
        [TextArea(1, 2)] public List<string> frases = new List<string>();
    }

    [TextArea(1, 2)] public List<string> generales = new List<string>();
    [TextArea(1, 2)] public List<string> enemigoComun = new List<string>();
    [Tooltip("Contra cualquier jefe (se mezclan con las de su jefe).")]
    [TextArea(1, 2)] public List<string> jefe = new List<string>();
    public List<FrasesJefe> porJefe = new List<FrasesJefe>();
    [TextArea(1, 2)] public List<string> caida = new List<string>();
    [Tooltip("Instakill avisado o el castigo de un parry/agarre.")]
    [TextArea(1, 2)] public List<string> letal = new List<string>();
    [TextArea(1, 2)] public List<string> rapida = new List<string>();
    [TextArea(1, 2)] public List<string> racha5 = new List<string>();
    [TextArea(1, 2)] public List<string> racha10 = new List<string>();
    [TextArea(1, 2)] public List<string> racha20 = new List<string>();
    [TextArea(1, 2)] public List<string> almas = new List<string>();
    [TextArea(1, 2)] public List<string> sangrado = new List<string>();
    [TextArea(1, 2)] public List<string> frio = new List<string>();
    [TextArea(1, 2)] public List<string> fuego = new List<string>();

    [Header("Cuando se usa cada categoria")]
    [Tooltip("Morir antes de estos segundos tras aparecer: 'muerte rapida'.")]
    public float segundosRapida = 12f;
    [Tooltip("Perder al menos estas almas: 'muchas almas'.")]
    public int almasMuchas = 1000;
    [Tooltip("Cada cuantas muertes seguidas sale una frase de racha (desde la 5).")]
    public int cadaRacha = 5;

    private static TextosMuerte instancia;
    private static string ultima;

    public static TextosMuerte Get()
    {
        if (instancia == null) instancia = Resources.Load<TextosMuerte>("TextosMuerte");
        return instancia;
    }

    // La frase para esta muerte.
    public string Elegir(CausaMuerte.Resumen r)
    {
        foreach (List<string> l in Candidatas(r))
        {
            string f = Una(l);
            if (f != null) return f;
        }
        return "Has muerto. Otra vez.";
    }

    private IEnumerable<List<string>> Candidatas(CausaMuerte.Resumen r)
    {
        int c = Mathf.Max(1, cadaRacha);
        if (r.seguidas >= 5 && r.seguidas % c == 0) yield return r.seguidas >= 20 ? racha20 : r.seguidas >= 10 ? racha10 : racha5;
        if (r.tipo == CausaMuerte.Tipo.Letal) yield return letal;
        if (r.tipo == CausaMuerte.Tipo.Caida) yield return caida;
        if (r.tipo == CausaMuerte.Tipo.Sangrado) yield return sangrado;
        if (r.tipo == CausaMuerte.Tipo.Frio) yield return frio;
        if (r.tipo == CausaMuerte.Tipo.Fuego) yield return fuego;
        if (r.almasPerdidas >= almasMuchas) yield return almas;
        if (r.segundosVivo < segundosRapida) yield return rapida;
        if (r.jefe != null)
        {
            // Las del jefe y las generales de jefe, mezcladas.
            List<string> mezcla = new List<string>(jefe);
            FrasesJefe pj = porJefe.FirstOrDefault(p => p.jefe == r.jefe);
            if (pj != null) { mezcla.AddRange(pj.frases); mezcla.AddRange(pj.frases); }
            yield return mezcla;
        }
        if (r.tipo == CausaMuerte.Tipo.Enemigo) yield return enemigoComun;
        yield return generales;
    }

    // Una al azar, sin repetir la anterior (si hay mas de una).
    private static string Una(List<string> l)
    {
        List<string> v = l?.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        if (v == null || v.Count == 0) return null;
        if (v.Count > 1) v.RemoveAll(s => s == ultima);
        string f = v[UnityEngine.Random.Range(0, v.Count)];
        ultima = f;
        return f;
    }

    public int Total => generales.Count + enemigoComun.Count + jefe.Count + porJefe.Sum(p => p.frases.Count) + caida.Count + letal.Count + rapida.Count
                        + racha5.Count + racha10.Count + racha20.Count + almas.Count + sangrado.Count + frio.Count + fuego.Count;
}
