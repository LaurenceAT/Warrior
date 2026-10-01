using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;

// Codigos secretos del menu de Desafios (Resources/Desafios/Codigos). Se
// escriben con el teclado mientras el menu esta abierto (sin cuadro de texto).
// Para anadir uno: un elemento mas en "codigos" con su texto y su accion (y,
// si hace falta una accion nueva, se anade al enum Accion y a MenuDesafios.Usar).
// "Codigos activos" los apaga todos (por ejemplo, en una version publicada).
[CreateAssetMenu(menuName = "Warrior/Codigos secretos", fileName = "Codigos")]
public class CodigosSecretos : ScriptableObject
{
    public enum Accion
    {
        // Desbloquea el jefe secreto con ese id (parametro). No completa otros
        // desafios, no da logros ni desbloquea informacion del jefe.
        DesbloquearJefeSecreto = 0,
    }

    [Serializable]
    public class Codigo
    {
        [Tooltip("Lo que hay que escribir (da igual mayusculas y acentos).")]
        public string texto;
        public Accion accion;
        [Tooltip("Para DesbloquearJefeSecreto: el id del jefe (blind_huntress).")]
        public string parametro;
    }

    [Tooltip("Apagado: ningun codigo hace nada.")]
    public bool codigosActivos = true;
    [Tooltip("Segundos sin pulsar una tecla para olvidar lo escrito.")]
    public float olvidarTras = 2f;
    public List<Codigo> codigos = new List<Codigo>();

    // Marca global (globales.json) del jefe desbloqueado con un codigo.
    public static string Marca(string jefe) => "codigo_" + jefe;

    // Minusculas y sin acentos (solo letras y numeros).
    public static string Normalizar(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new StringBuilder();
        foreach (char c in s.Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(c))
                sb.Append(char.ToLowerInvariant(c));
        return sb.ToString();
    }

    // El codigo con el que acaba lo escrito (o null).
    public Codigo Buscar(string escrito)
    {
        if (!codigosActivos || string.IsNullOrEmpty(escrito)) return null;
        return codigos.FirstOrDefault(c => c != null && Normalizar(c.texto).Length > 0 && escrito.EndsWith(Normalizar(c.texto)));
    }

    public int Largo => codigos.Where(c => c != null).Select(c => Normalizar(c.texto).Length).DefaultIfEmpty(0).Max();

    private static CodigosSecretos instancia;

    public static CodigosSecretos Get()
    {
        if (instancia == null) instancia = Resources.Load<CodigosSecretos>("Desafios/Codigos");
        return instancia;
    }
}
