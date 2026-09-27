using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// Configuracion de un nivel en un solo sitio (un archivo por nivel, en
// Assets/Data/Niveles). Se edita en el Inspector:
//   - Terreno: las piezas con las que esta dibujado el suelo.
//   - Fondo parallax: las capas, de la mas lejana a la mas cercana.
//   - Musica: la de combate, el ambiente y la del jefe (por fases).
//   - Sonidos del nivel: sustituyen, solo en este nivel, a los de la biblioteca
//     general (Resources/RecursosRPG) con la misma clave.
//   - Frases de la pantalla de muerte.
//
// La musica, los sonidos y las frases se leen al jugar: basta con cambiarlos y
// darle a Play. El terreno y el fondo estan dibujados en la escena: tras
// cambiarlos hay que pulsar "Aplicar a la escena" (boton arriba del Inspector).
[CreateAssetMenu(menuName = "Warrior/Configuracion de nivel", fileName = "ConfigNivel")]
public class ConfigNivel : ScriptableObject
{
    public enum TipoTerreno { Tiles, Piezas }

    [Serializable]
    public class ConjuntoTiles
    {
        [Tooltip("Nombre para reconocerlo (no afecta al juego).")]
        public string nombre;
        [Tooltip("Color con el que se tine todo el conjunto (blanco = sin tenir).")]
        public Color color = Color.white;
        [Tooltip("Los 9 tiles del conjunto (no hace falta tocarlos).")]
        public Tile[] tiles = new Tile[9];
        [Tooltip("La imagen de cada pieza, en este orden: esquina arriba-izq, arriba, esquina arriba-der, " +
                 "izquierda, centro, derecha, esquina abajo-izq, abajo, esquina abajo-der.")]
        public Sprite[] sprites = new Sprite[9];
    }

    [Serializable]
    public class CapaFondo
    {
        public string nombre;
        public Sprite imagen;
        [Tooltip("1 = quieta respecto a la camara (muy lejos); 0 = se mueve con el mundo (muy cerca).")]
        [Range(0f, 1f)] public float seguimiento = 0.8f;
        public Color color = Color.white;
        [Tooltip("Subir o bajar la capa (unidades).")]
        public float desplazamientoY;
    }

    [Serializable]
    public class MusicaFase
    {
        public AudioClip pista;
        [Range(0f, 1f)] public float volumen = 0.6f;
        [Tooltip("Segundo de la pista por el que empieza.")]
        public float inicio;
        [Tooltip("Tramo que se repite: x = desde, y = hasta (segundos). y = 0: la pista entera.")]
        public Vector2 bucle;
    }

    [Header("Terreno")]
    [Tooltip("Tiles: el suelo es un Tilemap (nivel de nieve). Piezas: el suelo esta hecho de losas y pilares (cueva).")]
    public TipoTerreno tipoTerreno = TipoTerreno.Tiles;
    [Tooltip("Conjuntos de tiles (nieve: el suelo normal y el hielo resbaladizo).")]
    public List<ConjuntoTiles> conjuntosTiles = new List<ConjuntoTiles>();
    [Tooltip("Losas de los suelos y techos (se eligen al azar).")]
    public Sprite[] losas;
    [Tooltip("Pilares de las paredes (se eligen al azar).")]
    public Sprite[] pilares;
    [Tooltip("Relleno del interior de la roca.")]
    public Sprite relleno;
    public Color colorPiezas = Color.white;

    [Header("Fondo parallax")]
    [Tooltip("Alto de cada capa en unidades (mas alto = fondo mas grande).")]
    public float altoFondo = 19f;
    [Tooltip("Cuanto sigue el fondo a la camara en vertical.")]
    [Range(0f, 1f)] public float seguimientoVertical = 0.92f;
    [Tooltip("De la capa mas lejana a la mas cercana.")]
    public List<CapaFondo> capasFondo = new List<CapaFondo>();

    [Header("Musica del nivel")]
    [Tooltip("Suena solo mientras peleas con enemigos normales.")]
    public AudioClip musicaCombate;
    [Range(0f, 1f)] public float volumenCombate = 0.35f;
    [Tooltip("Sonido de fondo mientras exploras (viento, cueva...).")]
    public AudioClip ambiente;
    [Range(0f, 1f)] public float volumenAmbiente = 0.4f;

    [Header("Musica del jefe")]
    public MusicaFase jefeFase1 = new MusicaFase();
    public MusicaFase jefeFase2 = new MusicaFase();

    [Header("Sonidos propios del nivel")]
    [Tooltip("Misma clave que en Resources/RecursosRPG: en este nivel suena esto en su lugar. " +
             "Ejemplo: clave \"pasos\" con otros clips para pisar nieve.")]
    public List<RecursosRPG.GrupoSonido> sonidos = new List<RecursosRPG.GrupoSonido>();

    [Header("Pantalla de muerte")]
    [TextArea] public string[] frasesMuerte;
}
