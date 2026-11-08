using UnityEngine;

// Numeros del Bringer of Death (jefe nuevo, SOLO en la sala de pruebas).
// Ficha: Assets/Data/Jefes/Ajustes Bringer of Death. Todo se edita aqui.
//
// Una sola barra de vida: al bajar de "marcaFase" (la mitad) pasa a la fase 2
// sin barra nueva. Danos en puntos de vida (la vida inicial del player es 150).
public class AjustesBringer : ScriptableObject
{
    [Header("General")]
    public string nombreMostrado = "Bringer of Death";
    [Tooltip("Vida total (una sola barra).")]
    [Min(1)] public int vida = 3200;
    [Tooltip("Parte de la barra en la que empieza la fase 2 (0,5 = la mitad).")]
    [Range(0.1f, 0.9f)] public float marcaFase = 0.5f;
    [Tooltip("Lo que dura el cambio de fase (invulnerable, sin atacar).")]
    [Min(0f)] public float transicion = 2.4f;
    [Tooltip("Todo va mas rapido en la fase 2 (animaciones, pausas, andar).")]
    [Min(0.1f)] public float multVelocidadFase2 = 1.25f;
    [Tooltip("Cuerpo (ancho y alto, en unidades).")]
    public Vector2 cuerpo = new Vector2(1.3f, 2.4f);

    [Header("Ritmo")]
    public float velocidadAndar = 3.4f;
    [Tooltip("Lo que anda como mucho para acercarse antes de un ataque (s).")]
    public float tiempoMaxAndar = 2.2f;
    [Tooltip("No se acerca a menos de esto de las paredes de la arena.")]
    public float margenArena = 1.2f;
    [Tooltip("Pausa entre ataques (min y max, s).")]
    public Vector2 pausaFase1 = new Vector2(0.7f, 1.2f);
    public Vector2 pausaFase2 = new Vector2(0.35f, 0.75f);
    [Tooltip("Pausa antes del primer ataque de la fase 2.")]
    public float pausaInicialFase2 = 0.6f;
    [Tooltip("Ataques de toda la arena: segundos minimos entre uno y otro.")]
    public float enfriamientoArena = 9f;

    [Header("Frecuencia de cada ataque (0 = nunca)")]
    public Frecuencias fase1 = new Frecuencias
    {
        guadana = 3f, mano = 1.6f, mediaLuna = 1.4f, ola = 1.2f, desvanecer = 1.1f, cosecha = 1f,
    };
    public Frecuencias fase2 = new Frecuencias
    {
        guadana = 2.4f, mano = 1.4f, mediaLuna = 1f, ola = 0.8f, desvanecer = 1.2f, cosecha = 1f,
        circulo = 1.3f, espiritus = 1.1f, cometas = 1f, esfera = 0.8f, juicio = 0.9f, marea = 0.9f,
    };

    [System.Serializable]
    public class Frecuencias
    {
        public float guadana, mano, mediaLuna, ola, desvanecer, cosecha;
        [Header("Solo fase 2")]
        public float circulo, espiritus, cometas, esfera, juicio, marea;
    }

    [Header("Avisos")]
    [Tooltip("Color del aviso en su cuerpo antes de un golpe de guadana.")]
    public Color colorAviso = new Color(0.75f, 0.25f, 1f, 1f);
    [Tooltip("Color del aviso antes de un hechizo.")]
    public Color colorHechizo = new Color(0.45f, 1f, 0.7f, 1f);
    [Range(0f, 1f)] public float intensidadAviso = 0.6f;

    [Header("Guadanazo (fisico)")]
    [Min(0)] public int danoGuadana = 38;
    [Tooltip("Aviso minimo antes del tajo (se sostiene la guadana en alto).")]
    public float avisoGuadana = 0.55f;
    [Tooltip("Cuadros 4-5 del ataque: caja propia (delante, desde los pies).")]
    public Vector2 cajaGuadanaCentro = new Vector2(1.9f, 1.2f);
    public Vector2 cajaGuadanaTamano = new Vector2(3.8f, 2.4f);
    [Tooltip("Distancia a la que empieza el guadanazo.")]
    public float alcanceGuadana = 3f;
    [Tooltip("Probabilidad de un segundo guadanazo seguido (fase 1 / fase 2).")]
    [Range(0f, 1f)] public float dobleFase1 = 0.35f;
    [Range(0f, 1f)] public float dobleFase2 = 0.6f;
    [Tooltip("El segundo guadanazo se retrasa al azar (min y max, s): castiga al que se precipita.")]
    public Vector2 retrasoSegundo = new Vector2(0.15f, 0.65f);
    public float recuperacionGuadana = 0.45f;
    [Tooltip("Si le paras un guadanazo, se tambalea (s). Dos parrys seguidos: aturdido.")]
    public float tambaleoParry = 0.55f;
    public float aturdidoDobleParry = 1.6f;

    [Header("Mano del abismo (su propio hechizo)")]
    [Min(0)] public int danoMano = 30;
    [Tooltip("El portal te sigue este tiempo (s) y luego se queda fijo.")]
    public float seguirMano = 0.85f;
    [Tooltip("Desde que se fija hasta que baja la mano (s).")]
    public float fijoMano = 0.4f;
    public Vector2 cajaMano = new Vector2(1.3f, 2.6f);
    [Tooltip("Fase 2: cuantas caen seguidas y cada cuanto.")]
    public int manosFase2 = 3;
    public float intervaloManos = 0.55f;

    [Header("Media luna (Necromante)")]
    [Min(0)] public int danoMediaLuna = 26;
    public float velocidadMediaLuna = 11f;
    public float alcanceMediaLuna = 24f;
    [Tooltip("Altura a la que vuela: baja (hay que saltar) o alta (hay que agacharse... o quedarse en el suelo).")]
    public float alturaBaja = 0.6f;
    public float alturaAlta = 2.2f;
    public Vector2 cajaMediaLuna = new Vector2(1.2f, 1.1f);
    [Tooltip("Cuantas lanza (fase 1 / fase 2).")]
    public int mediasLunasFase1 = 1;
    public int mediasLunasFase2 = 2;
    public float intervaloMediaLuna = 0.45f;

    [Header("Ola de almas (Necromante): estallidos por el suelo")]
    [Min(0)] public int danoOla = 28;
    [Tooltip("Distancia entre un estallido y el siguiente.")]
    public float pasoOla = 1.9f;
    [Tooltip("Tiempo entre un estallido y el siguiente.")]
    public float intervaloOla = 0.12f;
    [Tooltip("Aviso antes del primer estallido (s).")]
    public float avisoOla = 0.5f;
    public Vector2 cajaOla = new Vector2(1.7f, 1.7f);

    [Header("Desvanecerse")]
    [Tooltip("Tiempo oculto (s), y a que distancia aparece a tu espalda.")]
    public float tiempoOculto = 0.55f;
    public float distanciaEspalda = 1.7f;
    [Tooltip("Aviso del guadanazo al reaparecer (mas corto que el normal).")]
    public float avisoEspalda = 0.42f;

    [Header("Cosecha: manos por toda la arena")]
    [Tooltip("Distancia entre manos.")]
    public float separacionCosecha = 2f;
    [Tooltip("Huecos libres (fase 1 / fase 2). Cada hueco es de 2 manos.")]
    public int huecosFase1 = 2;
    public int huecosFase2 = 1;
    [Tooltip("Los portales se ven este tiempo antes de que baje la primera mano.")]
    public float avisoCosecha = 1.1f;
    [Tooltip("Retraso entre una mano y la siguiente (barrido de un lado al otro).")]
    public float barridoCosecha = 0.05f;

    [Header("Fase 2: Circulo de condena (Necromante)")]
    [Min(0)] public int danoCirculo = 32;
    public int circulos = 3;
    public float avisoCirculo = 0.8f;
    public float intervaloCirculo = 0.5f;
    public Vector2 cajaCirculo = new Vector2(2.6f, 2.8f);

    [Header("Fase 2: Espiritus (Necromante)")]
    [Min(0)] public int danoEspiritu = 20;
    public int espiritus = 3;
    public float velocidadEspiritu = 4.2f;
    [Tooltip("Giro hacia ti (grados por segundo).")]
    public float giroEspiritu = 110f;
    public float vidaEspiritu = 6f;

    [Header("Fase 2: Lluvia de cometas (Starcaller), toda la arena")]
    [Min(0)] public int danoCometa = 24;
    public int cometas = 16;
    [Tooltip("Lo que dura la lluvia (s).")]
    public float duracionLluvia = 2.8f;
    [Tooltip("Marca en el suelo antes de que caiga cada cometa.")]
    public float avisoCometa = 0.75f;
    public float velocidadCometa = 18f;
    public Vector2 cajaCometa = new Vector2(1.6f, 1.4f);
    [Tooltip("Uno de cada tantos cae justo donde estas.")]
    public int cometaApuntado = 4;

    [Header("Fase 2: Esfera del vacio (Starcaller), ataque especial")]
    [Min(0)] public int danoEsfera = 45;
    [Tooltip("Te atrae durante este tiempo y luego implosiona.")]
    public float atraccion = 2.3f;
    public float fuerzaAtraccion = 5.5f;
    [Tooltip("Radio en que te atrae (unidades).")]
    public float radioAtraccion = 14f;
    public Vector2 cajaEsfera = new Vector2(3.4f, 3.4f);
    [Tooltip("Estado del especial: congelacion (frena tu estamina).")]
    public float congelacionEsfera = 60f;

    [Header("Fase 2: Juicio (toda la arena, por tramos alternos)")]
    [Min(0)] public int danoJuicio = 34;
    [Tooltip("Ancho de cada tramo del suelo.")]
    public float tramoJuicio = 2.8f;
    [Tooltip("Pasadas: los tramos pares, luego los impares, luego los pares...")]
    public int pasadasJuicio = 3;
    public float avisoJuicio = 0.9f;
    public float entrePasadas = 0.35f;

    [Header("Fase 2: Marea de almas (olas desde las dos paredes)")]
    [Tooltip("Retraso de la segunda ola (s).")]
    public float retrasoMarea = 0.35f;

    [Header("Elementos (multiplicador del dano)")]
    public float afinidadFuego = 1.1f;
    public float afinidadHielo = 1f;
    public float afinidadOscuro = 0.5f;
    public float afinidadSagrado = 1.5f;
    public float afinidadSangrado = 0.8f;
    [Tooltip("Fase 2: el sagrado aun mas.")]
    public float afinidadSagradoFase2 = 1.8f;

    [Header("Animacion")]
    public float fps = 10f;
    public float fpsHechizo = 12f;

    [Header("Colores de los efectos")]
    public Color colorNecro = Color.white;
    public Color colorEstrellas = Color.white;
    public Color colorMano = Color.white;

    public float Afinidad(Elemento e, bool fase2)
    {
        switch (e)
        {
            case Elemento.Fuego: return afinidadFuego;
            case Elemento.Hielo: return afinidadHielo;
            case Elemento.Oscuro: return afinidadOscuro;
            case Elemento.Sagrado: return fase2 ? afinidadSagradoFase2 : afinidadSagrado;
            case Elemento.Sangrado: return afinidadSangrado;
            default: return 1f;
        }
    }
}
