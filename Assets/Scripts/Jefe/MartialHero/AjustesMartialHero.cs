using System;
using System.Collections.Generic;
using UnityEngine;

// Numeros del Martial Hero (minijefe nuevo, SOLO en la sala de pruebas). Su
// ficha es Assets/Data/Jefes/Ajustes Martial Hero: todo lo ajustable esta aqui.
//
// Sus sprites (copias en Assets/Sprites/MartialHero) se cortan por cuadricula de
// 200x200 con los pies como anclaje. Los cuadros se cuentan desde 0 en cada tira.
public class AjustesMartialHero : ScriptableObject
{
    // Los dos golpes que sabe dar (cada uno con su tira de sprites).
    public enum TipoGolpe { Desenvaine, Tajo }

    [Serializable]
    public class Golpe
    {
        public string nombre;
        [Tooltip("Ligero: aviso naranja que parpadea rapido. Pesado: aviso rojo que late lento.")]
        public PesoGolpe peso;
        [Tooltip("Aviso deseado (segundos). Nunca baja del minimo de su peso ni de la pausa minima entre golpes.")]
        [Min(0f)] public float aviso;
        [Tooltip("Dano al jugador (puntos de vida, antes del multiplicador de Dificil).")]
        [Min(0)] public int dano;
        [Tooltip("Caja de golpe: centro respecto a los pies (x hacia donde mira) y tamano, en unidades. No es el arco dibujado.")]
        public Vector2 cajaCentro, cajaTamano;
        [Header("Cuadros de su tira")]
        [Tooltip("Cuadro del aviso (el destello): si el dibujo no llega al aviso de la ficha, se sostiene aqui lo que falte.")]
        [Min(0)] public int cuadroAviso = 3;
        [Tooltip("Cuadros con la caja de golpe activa (los dos incluidos).")]
        [Min(0)] public int golpeDesde = 4, golpeHasta = 5;
    }

    [Serializable]
    public class Variante
    {
        [Tooltip("Nombre interno (el de la sala de pruebas).")]
        public string id;
        public string nombre;
        [Tooltip("Frecuencia al elegir (0 = nunca solo; se puede lanzar desde la sala).")]
        [Min(0f)] public float frecuencia = 1f;
        public List<TipoGolpe> golpes = new List<TipoGolpe>();
    }

    [Header("Nombre")]
    [Tooltip("El que sale en la barra de vida. Al cambiarlo, pasa Warrior > Martial Hero > Crear o actualizar para que la sala lo use.")]
    public string nombreMostrado = "Martial Hero";

    [Header("Vida")]
    [Min(1)] public int vida = 600;

    [Header("Cuerpo")]
    [Tooltip("Caja del cuerpo (ancho y alto, unidades), apoyada en los pies. Es la que recibe tus golpes.")]
    public Vector2 cuerpo = new Vector2(0.8f, 1.75f);

    [Header("Movimiento")]
    [Tooltip("Velocidad al correr hacia ti (unidades por segundo).")]
    [Min(0f)] public float velocidadCorrer = 5.5f;
    [Tooltip("Se para a esta distancia de ti para atacar (unidades).")]
    [Min(0.2f)] public float distanciaAtaque = 1.6f;
    [Tooltip("Lo maximo que corre antes de atacar igualmente (segundos).")]
    [Min(0f)] public float tiempoMaxCorrer = 2.5f;
    [Tooltip("Distancia minima a los bordes de la arena (unidades).")]
    [Min(0f)] public float margenArena = 1f;

    [Header("Guardia entre ataques: lo que se cubre antes de ir a por ti (x = minimo, y = maximo, segundos)")]
    public Vector2 pausa = new Vector2(0.5f, 0.9f);

    [Header("Ritmo de los combos (pieza comun)")]
    public ReglasCombo reglas = new ReglasCombo();

    [Header("Golpes (todos ligeros: aviso naranja y rapido)")]
    public Golpe desenvaine = new Golpe
    {
        nombre = "Desenvaine", peso = PesoGolpe.Ligero, aviso = 0.3f, dano = 22,
        cajaCentro = new Vector2(1.1f, 0.9f), cajaTamano = new Vector2(1.9f, 1.6f),
    };
    public Golpe tajo = new Golpe
    {
        nombre = "Tajo", peso = PesoGolpe.Ligero, aviso = 0.3f, dano = 24,
        cajaCentro = new Vector2(1.1f, 0.6f), cajaTamano = new Vector2(2.1f, 1.2f),
    };

    [Header("Combo (seguido, como en el dibujo: el desenvaine enlaza con el tajo). Si hay varios, el mismo no se repite dos veces seguidas")]
    public List<Variante> variantes = new List<Variante>
    {
        new Variante { id = "combo_cuadruple", nombre = "Combo cuadruple", golpes = { TipoGolpe.Desenvaine, TipoGolpe.Tajo, TipoGolpe.Desenvaine, TipoGolpe.Tajo } },
    };

    [Header("Cuando ataca (siempre con el combo)")]
    [Tooltip("Si entras en este radio mientras se cubre, ataca enseguida (unidades, desde sus pies). Si ya estabas dentro, ataca al acabar su guardia.")]
    [Min(0.5f)] public float radioEntrada = 2.8f;
    [Tooltip("Si le pegas mientras se cubre, contraataca tras este tiempo (segundos; el aviso del primer golpe va despues).")]
    [Min(0f)] public float retrasoContraataque = 0.25f;

    [Header("Guardia (siempre que no ataca, se recupera ni esta aturdido)")]
    [Tooltip("Dano que le entra de frente en guardia (0,25 = el 25 %). Nunca menos de 1: ningun golpe queda anulado. Por la espalda entra entero.")]
    [Range(0.01f, 1f)] public float danoEnGuardia = 0.1f;
    [Tooltip("Si le pegas por la espalda, se gira hacia ti tras este tiempo (segundos).")]
    [Min(0f)] public float giroGuardia = 0.3f;
    [Tooltip("Chispas en cada golpe que para en guardia.")]
    [Min(0)] public int chispasGuardia = 8;
    [Tooltip("Tinte mientras se cubre (asi se sabe que esta en guardia) y cuanto se nota (0 = nada).")]
    public Color colorGuardia = new Color(0.45f, 0.8f, 1f, 1f);
    [Range(0f, 1f)] public float intensidadGuardia = 0.3f;

    [Header("Aturdimiento")]
    [Tooltip("Aturdido si paras todos los golpes del combo (segundos).")]
    [Min(0f)] public float aturdidoCombo = 2f;
    [Tooltip("Dano que recibe mientras esta aturdido por un parry (1,5 = un 50 % mas). Los aturdimientos del sagrado no lo multiplican.")]
    [Min(1f)] public float multDanoAturdido = 1.5f;
    [Tooltip("Tras un aturdimiento por parry, segundos en que nada lo vuelve a aturdir (ni el sagrado). Los parrys siguen funcionando.")]
    [Min(0f)] public float inmunidadTrasAturdido = 2f;
    [Tooltip("Mientras esta inmune: chispas de este color en tus parrys...")]
    public Color colorChispasInmune = new Color(0.75f, 0.75f, 0.8f, 1f);
    public Color colorChispasInmune2 = new Color(0.45f, 0.45f, 0.5f, 1f);
    [Tooltip("...y este texto cuando lo habria aturdido.")]
    public string textoInmune = "¡Firme!";
    public Color colorTextoInmune = new Color(0.8f, 0.82f, 0.9f, 1f);
    public string textoAturdido = "Aturdido";
    public Color colorTextoAturdido = new Color(1f, 0.85f, 0.45f, 1f);
    [Tooltip("Temblor mientras esta aturdido (unidades).")]
    [Min(0f)] public float temblorAturdido = 0.04f;

    [Header("Sagrado contra la guardia")]
    [Tooltip("Un golpe sagrado rompe la guardia y lo aturde esto (segundos; sin el multiplicador de dano). Estando inmune, la rompe sin aturdir.")]
    [Min(0f)] public float aturdidoRomperGuardia = 0.6f;
    public string textoGuardiaRota = "¡Guardia rota!";

    [Header("Animacion (cuadros por segundo de cada tira)")]
    [Min(1f)] public float fpsEspera = 10f;
    [Min(1f)] public float fpsCorrer = 12f;
    [Min(1f)] public float fpsAtaque = 12f;
    [Min(1f)] public float fpsMuerte = 8f;
    [Tooltip("Al caer aturdido y al levantarse (cuadros 2-4 de la muerte).")]
    [Min(1f)] public float fpsAturdido = 12f;

    public Golpe DeTipo(TipoGolpe t) => t == TipoGolpe.Tajo ? tajo : desenvaine;
    public Variante Buscar(string id) => variantes.Find(v => v != null && v.id == id);
}
