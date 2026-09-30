using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Ficha de un jefe para el menu de Desafios (Resources/Desafios). Todo se edita
// en el Inspector: textos, debilidades que se muestran, el cofre de la arena y
// los numeros del modo Dificil. Para anadir un jefe nuevo basta con crear su
// ficha (Crear > Warrior > Ficha de jefe) y que su nivel tenga una ArenaJefe.
//
// La ficha del menu empieza con solo la imagen y el nombre: lo demas se
// desbloquea peleando (Bitacora). Cada pieza tiene su identificador y su
// condicion:
//   - Ataques: al verlo por primera vez (el id es el que usa el jefe en su codigo).
//   - Consejos: al morir por ese ataque, o todos al derrotarlo por primera vez.
//   - Fases: al llegar a ella (las no alcanzadas no se ven).
//   - Elementos: al golpearlo por primera vez con esa imbuicion.
//   - Historia: cada fragmento con su condicion (entrar, fase, victoria...).
[CreateAssetMenu(menuName = "Warrior/Ficha de jefe", fileName = "FichaJefe")]
public class FichaJefe : ScriptableObject
{
    [Serializable]
    public class Afinidad
    {
        public Elemento elemento;
        [Tooltip("Multiplicador de dano (1.8 = +80 %, 0.4 = -60 %).")]
        public float multiplicador = 1f;
        [Tooltip("Cuando pasa (\"Fase 2\", \"Siempre\"...).")]
        public string cuando = "Siempre";
        [Tooltip("Fase en la que vale (0 = la que diga 'cuando'; si no dice ninguna, siempre).")]
        [Range(0, 3)] public int fase;

        // 0 = siempre.
        public int Fase
        {
            get
            {
                if (fase > 0) return fase;
                if (string.IsNullOrEmpty(cuando)) return 0;
                for (int i = 1; i <= 3; i++) if (cuando.Contains(i.ToString())) return i;
                return 0;
            }
        }
    }

    [Serializable]
    public class Ataque
    {
        [Tooltip("El nombre que usa el jefe en su codigo (no cambiarlo).")]
        public string id;
        public string nombre;
        [Tooltip("Fase en la que aparece por primera vez.")]
        [Range(1, 3)] public int fase = 1;
        [Tooltip("Ataque mortal (se descubre al mostrar su aviso).")]
        public bool instakill;
        [TextArea(2, 5)] public string descripcion;
        [Tooltip("Como esquivarlo o contrarrestarlo. Se desbloquea al morir por el, o al derrotar al jefe.")]
        [TextArea(2, 5)] public string consejo;
    }

    [Serializable]
    public class FaseInfo
    {
        [Range(1, 3)] public int numero = 1;
        public string nombre;
        [TextArea(2, 6)] public string descripcion;
    }

    public enum Cuando { PrimerIntento = 0, Fase2 = 1, Fase3 = 2, PrimeraVictoria = 3, VictoriaDificil = 4 }

    [Serializable]
    public class Fragmento
    {
        [Tooltip("Identificador (no cambiarlo: lo desbloqueado se guarda con el).")]
        public string id;
        public Cuando cuando;
        [TextArea(3, 8)] public string texto;
    }

    [Tooltip("Identificador (no cambiarlo: los tiempos y completados se guardan con el).")]
    public string id;
    public string nombre;
    [Tooltip("Escena donde esta su arena.")]
    public string escena;
    [Tooltip("Orden en la lista.")]
    public int orden;
    public Sprite imagen;

    [Header("Debilidades y resistencias (lo que hace de verdad el jefe)")]
    public List<Afinidad> afinidades = new List<Afinidad>();
    [Tooltip("Algo mas que no sea de un elemento (por ejemplo: la espada sin imbuir). Sale al descubrir el primer elemento.")]
    [TextArea(1, 3)] public string notaAfinidades;

    [Header("Textos")]
    [Tooltip("Resumen antiguo (ya no sale en el menu: lo sustituyen Ataques y Fases).")]
    [TextArea(4, 14)] public string informacion;
    [Tooltip("Historia completa (ya no sale entera: se muestra por fragmentos).")]
    [TextArea(4, 14)] public string historia;

    [Header("Ficha que se desbloquea peleando")]
    public List<Ataque> ataques = new List<Ataque>();
    public List<FaseInfo> fases = new List<FaseInfo>();
    public List<Fragmento> fragmentosHistoria = new List<Fragmento>();

    [Header("Reiniciar desafio")]
    [Tooltip("Al reiniciar el desafio desde la pausa, el dialogo de entrada sale como la primera vez (si no, como al reintentar tras morir).")]
    public bool dialogoAlReiniciar;

    [Header("Cofre de la arena")]
    public int almasCofre = 2500;
    public Equipo.Objeto[] objetosCofre = { Equipo.Objeto.PiedraForja, Equipo.Objeto.LagrimaCarmesi, Equipo.Objeto.FrascoSangre };

    [Header("Modo Dificil (solo numeros: mismos ataques y avisos)")]
    [Tooltip("Vida del jefe (1.5 = +50 %).")]
    public float multVida = 1.5f;
    [Tooltip("Dano que te hace (1.3 = +30 %).")]
    public float multDano = 1.3f;
    [Tooltip("Rapidez entre ataques: las pausas se dividen por esto (1.35 = pausas un 26 % mas cortas).")]
    public float multVelocidad = 1.35f;

    [Header("Jefe secreto")]
    [Tooltip("No aparece en Desafios (ni su nombre, ni una silueta, ni sus logros) hasta completar en Normal los desafios de 'requisitos'.")]
    public bool secreto;
    [Tooltip("Ids de los jefes cuyo desafio Normal hay que completar para desbloquearlo.")]
    public string[] requisitos = new string[0];

    public Ataque BuscarAtaque(string ataque) => ataques.FirstOrDefault(a => a.id == ataque);

    // Numero de fases que tiene (la mas alta de las que hay en la ficha).
    public int NumeroFases => fases.Count > 0 ? fases.Max(f => f.numero) : 1;

    // Se calcula con los desafios completados (globales.json): si ya estaban
    // completados antes de esta version, tambien cuenta. Las herramientas de
    // prueba pueden forzarlo en un sentido u otro.
    public bool Desbloqueado
    {
        get
        {
            if (!secreto) return true;
            if (Globales.Marca("bloqueo_" + id)) return false;
            if (Globales.Marca("forzar_" + id)) return true;
            return requisitos != null && requisitos.All(r => Globales.Completado(r, false));
        }
    }

    // Desbloquear o volver a bloquear un jefe secreto (solo pruebas).
    public static void ForzarSecreto(string id, bool desbloqueado)
    {
        Globales.PonerMarca("forzar_" + id, desbloqueado);
        Globales.PonerMarca("bloqueo_" + id, !desbloqueado);
        if (!desbloqueado) Globales.PonerMarca("revelado_" + id, false);
    }

    private static FichaJefe[] todas;

    public static FichaJefe[] Todas()
    {
        if (todas == null) todas = Resources.LoadAll<FichaJefe>("Desafios").OrderBy(f => f.orden).ThenBy(f => f.nombre).ToArray();
        return todas;
    }

    // Los que salen en el menu (los secretos, solo una vez desbloqueados).
    public static FichaJefe[] Visibles() => Todas().Where(f => f.Desbloqueado).ToArray();

    // Secretos que aun no estan desbloqueados (para saber si uno se acaba de abrir).
    public static string[] Bloqueados() => Todas().Where(f => !f.Desbloqueado).Select(f => f.id).ToArray();

    public static FichaJefe DeEscena(string escena) => Todas().FirstOrDefault(f => f.escena == escena);

    public static FichaJefe DeId(string id) => Todas().FirstOrDefault(f => f.id == id);
}
