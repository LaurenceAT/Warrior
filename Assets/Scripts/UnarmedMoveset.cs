using System;
using System.Collections.Generic;
using UnityEngine;

// Tabla de golpes sin arma (espada enfundada) y de como se encadenan.
//
// No hay ninguna cadena escrita en el codigo: cada golpe dice, para cada entrada,
// a que golpe lleva. Asi se pueden mezclar tipos en una misma cadena (puno, puno,
// patada de remate; o puno y luego un picado) anadiendo filas, sin tocar codigo.
//
//  - "inicios": con que golpe empieza una cadena segun la entrada y la situacion
//    (en el suelo, corriendo, en el aire, en el aire con S).
//  - "siguientes" de cada golpe: a que golpe salta si llega esa entrada dentro de
//    su ventana. Si no hay fila para esa entrada, la cadena se corta ahi.
//
// Se crea con: Project > Create > Warrior > Moveset sin arma.
[CreateAssetMenu(menuName = "Warrior/Moveset sin arma", fileName = "MovesetSinArma")]
public class UnarmedMoveset : ScriptableObject
{
    public enum Entrada { Puno, Patada }

    public enum Contexto { Suelo, Corriendo, Aire, AireAbajo }

    // Normal: el personaje se planta (con un pasito si se quiere).
    // Carrera: conserva el impulso de la carrera y frena poco a poco.
    // Picado: se suspende un instante y cae en diagonal hasta tocar suelo o un enemigo.
    public enum Tipo { Normal, Carrera, Picado }

    [Serializable]
    public class Enlace
    {
        public Entrada entrada;
        public string golpe;
    }

    [Serializable]
    public class Inicio
    {
        public Entrada entrada;
        public Contexto contexto;
        public string golpe;
    }

    [Serializable]
    public class Golpe
    {
        public string nombre;
        // Estado del Animator que se reproduce. Tiene que existir con ese nombre.
        public string estadoAnimator;
        public Tipo tipo;

        [Header("Tiempos (segundos desde que empieza)")]
        public float duracion = 0.27f;
        // Franja en la que la caja hace dano: solo los fotogramas de impacto.
        public float activoDesde = 0.12f;
        public float activoHasta = 0.21f;
        // Desde aqui, la siguiente entrada salta ya al siguiente golpe y se come
        // la recuperacion. Antes, la entrada se guarda y sale en cuanto se pueda.
        public float encadenarDesde = 0.2f;
        // Tras terminar, cuanto se espera la siguiente entrada antes de reiniciar.
        public float ventanaCombo = 0.35f;

        [Header("Caja de dano (relativa al centro del player; la X se voltea)")]
        public Vector2 offset = new Vector2(0.45f, 0.05f);
        public Vector2 tamano = new Vector2(0.5f, 0.35f);

        [Header("Efecto")]
        public int dano = 1;
        // 1 = retroceso normal del enemigo.
        public float retroceso = 1f;
        public float costeEstamina = 8f;
        // Empujon hacia delante al lanzarlo (en Carrera: velocidad minima que conserva).
        public float avance = 1.2f;
        // Lo que frena ese empujon por segundo.
        public float frenado = 14f;
        public float congelacion = 0.04f;
        public float sacudida = 0.1f;

        [Header("Solo tipo Picado")]
        // Suspension antes de caer.
        public float inicioPicado = 0.13f;
        // X hacia delante e Y hacia abajo.
        public Vector2 velocidadPicado = new Vector2(6f, 12f);
        // Al acertar a un enemigo: rebote hacia arriba (0 = sigue cayendo).
        public float rebote = 7f;
        // Tiempo clavado tras aterrizar.
        public float recuperacionAterrizaje = 0.15f;

        [Header("Encadenar")]
        public List<Enlace> siguientes = new List<Enlace>();
    }

    public List<Golpe> golpes = new List<Golpe>();
    public List<Inicio> inicios = new List<Inicio>();

    private Dictionary<string, Golpe> porNombre;

    public Golpe Buscar(string nombre)
    {
        if (string.IsNullOrEmpty(nombre)) return null;

        if (porNombre == null)
        {
            porNombre = new Dictionary<string, Golpe>();
            foreach (Golpe g in golpes)
                if (g != null && !string.IsNullOrEmpty(g.nombre)) porNombre[g.nombre] = g;
        }

        porNombre.TryGetValue(nombre, out Golpe encontrado);
        return encontrado;
    }

    // Golpe con el que empieza una cadena. Corriendo sin fila propia usa la del suelo.
    public Golpe Inicial(Entrada entrada, Contexto contexto)
    {
        Golpe g = BuscarInicio(entrada, contexto);
        if (g == null && contexto == Contexto.Corriendo) g = BuscarInicio(entrada, Contexto.Suelo);
        return g;
    }

    // A que golpe lleva esta entrada desde el golpe actual. null = la cadena acaba.
    public Golpe Siguiente(Golpe actual, Entrada entrada)
    {
        if (actual == null) return null;
        foreach (Enlace e in actual.siguientes)
            if (e != null && e.entrada == entrada) return Buscar(e.golpe);
        return null;
    }

    private Golpe BuscarInicio(Entrada entrada, Contexto contexto)
    {
        foreach (Inicio i in inicios)
            if (i != null && i.entrada == entrada && i.contexto == contexto) return Buscar(i.golpe);
        return null;
    }

    // Si se renombra un golpe en el Inspector, el indice se rehace.
    private void OnValidate()
    {
        porNombre = null;

        foreach (Golpe g in golpes)
        {
            if (g == null) continue;
            foreach (Enlace e in g.siguientes)
                if (e != null && !string.IsNullOrEmpty(e.golpe) && Buscar(e.golpe) == null)
                    Debug.LogWarning($"[Moveset] '{g.nombre}' enlaza con '{e.golpe}', que no existe.", this);
        }
        foreach (Inicio i in inicios)
            if (i != null && !string.IsNullOrEmpty(i.golpe) && Buscar(i.golpe) == null)
                Debug.LogWarning($"[Moveset] Un inicio apunta a '{i.golpe}', que no existe.", this);
    }
}
