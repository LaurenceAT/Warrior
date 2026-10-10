using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Textos de las estatuas (Resources/PistasEstatuas), editables en el Inspector.
// Cada estatua tiene dos lecturas: la primera (la pista) y la segunda, mas clara,
// que sale al volver a leerla. El libro de pistas muestra la mas clara leida.
// Se busca por escena + id (el id de la EstatuaPista). Si una estatua no esta
// aqui, usa el texto escrito en ella.
[CreateAssetMenu(menuName = "Warrior/Pistas de las estatuas", fileName = "PistasEstatuas")]
public class PistasEstatuas : ScriptableObject
{
    [System.Serializable]
    public class Pista
    {
        [Tooltip("Escena de la estatua (Nivel Nieve, Nivel Cueva...).")]
        public string escena;
        [Tooltip("Id de la EstatuaPista (no cambiarlo si ya hay partidas).")]
        public string id;
        [Tooltip("Quien habla (solo como guia para escribir; no sale en el juego).")]
        public string voz;
        [Tooltip("Titulo del cuadro y del libro de pistas.")]
        public string titulo;
        [TextArea(2, 6)] public string primera;
        [Tooltip("Al volver a leerla: mas clara, sin dar la solucion con todas las letras. Vacia = se repite la primera.")]
        [TextArea(2, 6)] public string segunda;
    }

    public List<Pista> pistas = new List<Pista>();

    private static PistasEstatuas instancia;

    public static PistasEstatuas Get()
    {
        if (instancia == null) instancia = Resources.Load<PistasEstatuas>("PistasEstatuas");
        return instancia;
    }

    public static Pista Buscar(string escena, string id)
    {
        PistasEstatuas p = Get();
        return p == null ? null : p.pistas.FirstOrDefault(x => x != null && x.escena == escena && x.id == id);
    }

    // El texto de una lectura (1 o 2); null si la estatua no esta en la ficha.
    public static string Texto(string escena, string id, int lectura)
    {
        Pista p = Buscar(escena, id);
        if (p == null) return null;
        if (lectura >= 2 && !string.IsNullOrWhiteSpace(p.segunda)) return p.segunda;
        return string.IsNullOrWhiteSpace(p.primera) ? null : p.primera;
    }
}
