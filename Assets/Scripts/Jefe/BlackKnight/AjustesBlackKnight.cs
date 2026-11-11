using System;
using System.Collections.Generic;
using UnityEngine;

// Numeros del Black Knight (jefe nuevo, SOLO en la sala de pruebas). Su ficha es
// Assets/Data/Jefes/Ajustes Black Knight: todo lo ajustable esta aqui.
//
// Sus sprites salen de una sola hoja (copia limpia en Assets/Sprites/BlackKnight,
// sin los marcos de color del original) en celdas de 144x64. Los cuadros se
// cuentan desde 0 dentro de cada clip.
public class AjustesBlackKnight : ScriptableObject
{
    [Serializable]
    public class Golpe
    {
        public string nombre;
        [Tooltip("Clip del animador (ligero1, ligero2, ligero3, pesado1..4, desenvainar, contra, arte).")]
        public string clip;
        [Tooltip("Ligero: aviso naranja que parpadea rapido. Pesado: aviso rojo que late lento.")]
        public PesoGolpe peso;
        [Tooltip("Aviso deseado (segundos, desde el cuadro de aviso hasta el golpe). Nunca baja del minimo de su peso.")]
        [Min(0f)] public float aviso = 0.35f;
        [Tooltip("Dano (puntos de vida, antes del multiplicador de Dificil).")]
        [Min(0)] public int dano = 20;
        [Tooltip("Caja de golpe: centro respecto a los pies (x hacia donde mira) y tamano, en unidades. No es el fuego dibujado.")]
        public Vector2 cajaCentro = new Vector2(1f, 0.8f), cajaTamano = new Vector2(2f, 1.6f);
        [Header("Cuadros de su clip")]
        [Tooltip("Cuadro del aviso: aqui se sostiene lo que falte para llegar al aviso.")]
        [Min(0)] public int cuadroAviso;
        [Tooltip("Cuadros con la caja activa (los dos incluidos).")]
        [Min(0)] public int golpeDesde = 1, golpeHasta = 2;
        [Tooltip("Si le sigue otro golpe, el clip se corta en este cuadro (enlaza). -1 = el clip entero.")]
        public int cortarEn = -1;
        [Tooltip("Lo que avanza durante sus cuadros de golpe (unidades; 0 = quieto).")]
        [Min(0f)] public float avance;
        [Tooltip("Amago: hace el aviso y el inicio del golpe pero no golpea (enlaza con el siguiente).")]
        public bool amago;
    }

    [Serializable]
    public class Ataque
    {
        [Tooltip("Nombre interno (el de la sala de pruebas).")]
        public string id;
        public string nombre;
        [Tooltip("Frecuencia al elegir (0 = nunca solo; se puede lanzar desde la sala).")]
        [Min(0f)] public float frecuencia = 1f;
        [Tooltip("Primera fase en que lo usa (1, 2 o 3).")]
        [Range(1, 3)] public int faseMinima = 1;
        [Tooltip("Se acerca hasta esta distancia antes de empezar (unidades).")]
        [Min(0.3f)] public float alcance = 1.8f;
        [Tooltip("Los golpes en orden (por nombre, de la lista de golpes).")]
        public List<string> golpes = new List<string>();
        [Tooltip("Probabilidad de cortar el combo a medias y rodar hacia atras (como un jugador que se arrepiente).")]
        [Range(0f, 1f)] public float probCortar;
        [Tooltip("Recuperacion tras el ataque (segundos): su hueco para que le pegues.")]
        [Min(0f)] public float recuperacion = 0.6f;
    }

    [Header("Nombre")]
    [Tooltip("El que sale en la barra. Al cambiarlo, pasa Warrior > Black Knight > Crear o actualizar.")]
    public string nombreMostrado = "Black Knight";

    [Header("Vida: tres barras (una por fase)")]
    [Min(1)] public int vidaFase1 = 780;
    [Min(1)] public int vidaFase2 = 840;
    [Min(1)] public int vidaFase3 = 900;
    public Color colorBarraFase2 = new Color(0.95f, 0.45f, 0.12f, 1f);
    public Color colorBarraFase3 = new Color(0.55f, 0.25f, 0.85f, 1f);
    public string textoFase2 = "La hoja del caballero se enciende.";
    public string textoFase3 = "Las sombras de sus duelos pasados despiertan.";
    [Tooltip("Invulnerable mientras cambia de fase (segundos de la transicion).")]
    [Min(0f)] public float duracionCambioFase = 1.8f;

    [Header("Elementos (dano que le entra: 1 = normal, menos = resiste, mas = debil)")]
    [Tooltip("Natura no es un elemento de dano (golpes fisicos): siempre neutral.")]
    [Min(0f)] public float multFuego = 0.6f;
    [Min(0f)] public float multOscuridad = 0.6f;
    [Min(0f)] public float multSangrado = 0.6f;
    [Tooltip("Congelacion (hielo).")]
    [Min(0f)] public float multHielo = 1.4f;
    [Tooltip("Luz (sagrado).")]
    [Min(0f)] public float multSagrado = 1.4f;

    [Header("Quemadura (desde la fase 1)")]
    [Tooltip("Cada golpe suyo que te alcanza llena tu barra de quemadura esto (de 100). Bloqueado, un 30 %; con parry, nada.")]
    [Min(0f)] public float quemaduraPorGolpe = 22f;

    [Header("Camara en sus especiales (cuando se aleja)")]
    [Tooltip("Margen alrededor de los dos para que ambos se vean (unidades).")]
    [Min(0f)] public float margenCamara = 3f;
    [Tooltip("Lo mas que se abre la camara (tamano ortografico).")]
    [Min(1f)] public float tamanoMaxCamara = 11f;
    [Tooltip("Cuanto sube el encuadre (fraccion del alto de la camara) para que el suelo quede abajo.")]
    [Range(0f, 0.45f)] public float subirEncuadre = 0.3f;
    [Tooltip("En ondas y erupciones se aleja hasta el otro extremo, pero nunca a mas de esto de ti (unidades).")]
    [Min(4f)] public float distanciaAlejarse = 14f;

    [Header("Cuerpo")]
    [Tooltip("Caja del cuerpo (ancho y alto, unidades), apoyada en los pies. Es la que recibe tus golpes.")]
    public Vector2 cuerpo = new Vector2(0.55f, 1.05f);

    [Header("Movimiento (con aceleracion, como una persona)")]
    [Min(0f)] public float velocidadCorrer = 6.2f;
    [Tooltip("Aceleracion y frenada (unidades/s por segundo).")]
    [Min(1f)] public float aceleracion = 30f;
    [Min(1f)] public float frenada = 40f;
    [Tooltip("Distancia que le gusta guardar mientras te estudia (min y max).")]
    public Vector2 distanciaEstudio = new Vector2(2.4f, 3.6f);
    [Tooltip("Lo maximo que corre antes de atacar igualmente (segundos).")]
    [Min(0f)] public float tiempoMaxCorrer = 2.2f;
    [Tooltip("Distancia minima a los bordes de la arena (unidades).")]
    [Min(0f)] public float margenArena = 0.8f;
    [Header("Rodada")]
    [Min(0f)] public float distanciaRodada = 3.4f;
    [Tooltip("Cuadros de la rodada en que no le entra nada (incluidos).")]
    public Vector2Int rodadaInvulnerable = new Vector2Int(1, 8);

    [Header("Pausa entre acciones (x = minimo, y = maximo, segundos) por fase")]
    public Vector2 pausaFase1 = new Vector2(0.45f, 0.9f);
    public Vector2 pausaFase2 = new Vector2(0.3f, 0.7f);
    public Vector2 pausaFase3 = new Vector2(0.2f, 0.55f);
    [Tooltip("Velocidad de sus animaciones de ataque por fase (1 = la del dibujo).")]
    public float velocidadFase2 = 1.1f;
    public float velocidadFase3 = 1.2f;

    [Header("Ritmo de los combos (pieza comun)")]
    public ReglasCombo reglas = new ReglasCombo
    {
        avisoMinLigero = 0.32f, avisoMinPesado = 0.5f, avisoMinFinal = 0.4f, pausaMinEntreGolpes = 0.45f,
        maxGolpesSeguidos = 4, pausaRecuperacion = 0.6f, topeDanoCombo = 0.45f,
    };

    [Header("Retraso (golpes que se hacen esperar, para pillar el parry temprano)")]
    [Tooltip("Probabilidad de que un golpe se retrase (fase 1, 2 y 3).")]
    public Vector3 probRetraso = new Vector3(0.12f, 0.2f, 0.28f);
    [Tooltip("Retraso extra (segundos, min y max). El aviso sigue encendido mientras.")]
    public Vector2 retraso = new Vector2(0.25f, 0.5f);

    [Header("Golpes")]
    public List<Golpe> golpes = new List<Golpe>
    {
        new Golpe { nombre = "Tajo alto", clip = "ligero1", peso = PesoGolpe.Ligero, aviso = 0.35f, dano = 20, cuadroAviso = 4, golpeDesde = 9, golpeHasta = 10, cortarEn = 12,
                    cajaCentro = new Vector2(0.95f, 0.75f), cajaTamano = new Vector2(1.9f, 1.5f), avance = 0.4f },
        new Golpe { nombre = "Arco", clip = "ligero2", peso = PesoGolpe.Ligero, aviso = 0.32f, dano = 18, cuadroAviso = 0, golpeDesde = 1, golpeHasta = 2, cortarEn = 4,
                    cajaCentro = new Vector2(0.5f, 0.9f), cajaTamano = new Vector2(2.6f, 1.9f) },
        new Golpe { nombre = "Estocada", clip = "ligero3", peso = PesoGolpe.Ligero, aviso = 0.38f, dano = 22, cuadroAviso = 3, golpeDesde = 5, golpeHasta = 7, cortarEn = 9,
                    cajaCentro = new Vector2(1.6f, 0.45f), cajaTamano = new Vector2(2.6f, 0.8f), avance = 1.4f },
        new Golpe { nombre = "Tajo pesado", clip = "pesado1", peso = PesoGolpe.Pesado, aviso = 0.55f, dano = 30, cuadroAviso = 2, golpeDesde = 3, golpeHasta = 4, cortarEn = 6,
                    cajaCentro = new Vector2(1.0f, 0.8f), cajaTamano = new Vector2(2.3f, 1.7f), avance = 0.6f },
        new Golpe { nombre = "Reves pesado", clip = "pesado2", peso = PesoGolpe.Pesado, aviso = 0.5f, dano = 30, cuadroAviso = 0, golpeDesde = 1, golpeHasta = 2, cortarEn = 5,
                    cajaCentro = new Vector2(0.4f, 0.9f), cajaTamano = new Vector2(2.6f, 1.9f) },
        new Golpe { nombre = "Barrido", clip = "pesado3", peso = PesoGolpe.Pesado, aviso = 0.55f, dano = 32, cuadroAviso = 4, golpeDesde = 6, golpeHasta = 7, cortarEn = 9,
                    cajaCentro = new Vector2(0.2f, 0.35f), cajaTamano = new Vector2(3.4f, 0.8f) },
        new Golpe { nombre = "Estocada de fuego", clip = "pesado4", peso = PesoGolpe.Pesado, aviso = 0.7f, dano = 34, cuadroAviso = 1, golpeDesde = 2, golpeHasta = 6,
                    cajaCentro = new Vector2(2.6f, 0.5f), cajaTamano = new Vector2(4.2f, 0.9f), avance = 0.8f },
        new Golpe { nombre = "Desenvaine", clip = "desenvainar", peso = PesoGolpe.Ligero, aviso = 0.4f, dano = 28, cuadroAviso = 7, golpeDesde = 9, golpeHasta = 11, cortarEn = 12,
                    cajaCentro = new Vector2(1.0f, 0.6f), cajaTamano = new Vector2(2.2f, 1.3f), avance = 3.2f },
        new Golpe { nombre = "Amago", clip = "pesado1", peso = PesoGolpe.Pesado, aviso = 0.3f, dano = 0, cuadroAviso = 2, golpeDesde = 3, golpeHasta = 3, cortarEn = 2, amago = true },
        new Golpe { nombre = "Estallido", clip = "contra", peso = PesoGolpe.Pesado, aviso = 0.75f, dano = 26, cuadroAviso = 1, golpeDesde = 2, golpeHasta = 4,
                    cajaCentro = new Vector2(0.5f, 0.9f), cajaTamano = new Vector2(2.6f, 2.1f) },
        new Golpe { nombre = "Arte 1", clip = "arte", peso = PesoGolpe.Ligero, aviso = 0.35f, dano = 18, cuadroAviso = 1, golpeDesde = 3, golpeHasta = 4,
                    cajaCentro = new Vector2(1.0f, 0.9f), cajaTamano = new Vector2(2.4f, 1.8f) },
        new Golpe { nombre = "Arte 2", clip = "arte", peso = PesoGolpe.Ligero, aviso = 0.35f, dano = 18, cuadroAviso = 8, golpeDesde = 10, golpeHasta = 11,
                    cajaCentro = new Vector2(0.3f, 0.7f), cajaTamano = new Vector2(3.0f, 1.4f) },
        new Golpe { nombre = "Arte 3", clip = "arte", peso = PesoGolpe.Ligero, aviso = 0.35f, dano = 18, cuadroAviso = 13, golpeDesde = 15, golpeHasta = 16,
                    cajaCentro = new Vector2(1.2f, 0.8f), cajaTamano = new Vector2(2.6f, 1.4f), avance = 0.8f },
        new Golpe { nombre = "Arte 4", clip = "arte", peso = PesoGolpe.Pesado, aviso = 0.6f, dano = 30, cuadroAviso = 21, golpeDesde = 23, golpeHasta = 25,
                    cajaCentro = new Vector2(0.3f, 1.2f), cajaTamano = new Vector2(2.8f, 2.6f) },
        new Golpe { nombre = "Arte 5", clip = "arte", peso = PesoGolpe.Pesado, aviso = 0.5f, dano = 30, cuadroAviso = 26, golpeDesde = 27, golpeHasta = 28,
                    cajaCentro = new Vector2(1.1f, 1.0f), cajaTamano = new Vector2(2.6f, 2.2f) },
        new Golpe { nombre = "Caida", clip = "caer_golpe", peso = PesoGolpe.Pesado, aviso = 0.45f, dano = 30, cuadroAviso = 0, golpeDesde = 1, golpeHasta = 2,
                    cajaCentro = new Vector2(0f, 0.4f), cajaTamano = new Vector2(2.6f, 0.9f) },
        new Golpe { nombre = "Tajo por la espalda", clip = "ligero2", peso = PesoGolpe.Ligero, aviso = 0.34f, dano = 22, cuadroAviso = 0, golpeDesde = 1, golpeHasta = 2,
                    cajaCentro = new Vector2(0.5f, 0.9f), cajaTamano = new Vector2(2.4f, 1.9f) },
    };

    [Header("Ataques y combos (el mismo no se repite dos veces seguidas)")]
    public List<Ataque> ataques = new List<Ataque>
    {
        new Ataque { id = "combo_ligero", nombre = "Combo ligero", frecuencia = 1.4f, alcance = 1.7f, probCortar = 0.25f, recuperacion = 0.55f,
                     golpes = { "Tajo alto", "Arco", "Estocada" } },
        new Ataque { id = "combo_corto", nombre = "Combo corto", frecuencia = 1f, alcance = 1.6f, recuperacion = 0.45f,
                     golpes = { "Arco", "Tajo alto" } },
        new Ataque { id = "estocada", nombre = "Estocada a distancia", frecuencia = 0.8f, alcance = 2.6f, recuperacion = 0.6f,
                     golpes = { "Estocada" } },
        new Ataque { id = "combo_pesado", nombre = "Combo pesado", frecuencia = 1f, alcance = 1.8f, probCortar = 0.2f, recuperacion = 0.8f,
                     golpes = { "Tajo pesado", "Reves pesado", "Barrido" } },
        new Ataque { id = "estocada_fuego", nombre = "Estocada de fuego", frecuencia = 0.7f, alcance = 3.6f, recuperacion = 0.9f,
                     golpes = { "Estocada de fuego" } },
        new Ataque { id = "amago", nombre = "Amago", frecuencia = 0.7f, alcance = 1.6f, recuperacion = 0.5f,
                     golpes = { "Amago", "Arco", "Estocada" } },
        new Ataque { id = "mixto", nombre = "Mixto", frecuencia = 0.8f, faseMinima = 2, alcance = 1.7f, probCortar = 0.2f, recuperacion = 0.7f,
                     golpes = { "Tajo alto", "Arco", "Tajo pesado", "Barrido" } },
        new Ataque { id = "arte", nombre = "Arte del arma", frecuencia = 0.6f, faseMinima = 3, alcance = 1.8f, recuperacion = 1.1f,
                     golpes = { "Arte 1", "Arte 2", "Arte 3", "Arte 4", "Arte 5" } },
    };

    [Header("Movimientos sueltos (ids para la sala de pruebas)")]
    [Tooltip("Rodada hasta tu espalda y tajo (frecuencia al elegir).")]
    [Min(0f)] public float frecuenciaRodadaEspalda = 0.6f;
    [Tooltip("Salto por encima y caida con la espada (frecuencia al elegir).")]
    [Min(0f)] public float frecuenciaSalto = 0.5f;
    [Tooltip("Guardia con parry propio (frecuencia al elegir, aparte de cuando te lee).")]
    [Min(0f)] public float frecuenciaGuardia = 0.4f;
    [Tooltip("Altura del salto (unidades) y duracion (segundos).")]
    public float alturaSalto = 3.2f;
    [Min(0.2f)] public float duracionSalto = 0.75f;

    [Header("Su parry (guardia con la espada en alto)")]
    [Tooltip("Lo que aguanta en guardia esperando tu golpe (segundos).")]
    [Min(0.1f)] public float duracionGuardia = 1.1f;
    [Tooltip("Si no le pegas, al bajar la guardia queda abierto este tiempo (segundos).")]
    [Min(0f)] public float abiertoTrasGuardia = 0.9f;
    [Tooltip("Dano que te entra si le pegas en su guardia: ninguno (lo para) y te aturde un instante.")]
    [Min(0f)] public float aturdirAlJugador = 0.35f;
    public Color colorGuardia = new Color(0.95f, 0.95f, 1f, 1f);
    [Range(0f, 1f)] public float brilloGuardia = 0.35f;

    [Header("Te lee (como otra persona)")]
    [Tooltip("Golpes tuyos seguidos (en 1,5 s) para que reaccione: rueda, para o se aparta.")]
    [Min(1)] public int golpesParaLeer = 2;
    [Tooltip("Probabilidad base de reaccionar a tus golpes cuando esta cerca y libre (fase 1, 2 y 3).")]
    public Vector3 probReaccion = new Vector3(0.35f, 0.5f, 0.6f);
    [Tooltip("Cuanto sube esa probabilidad por cada vez que le pegas machacando (adaptacion, con tope).")]
    [Min(0f)] public float subidaPorMachacar = 0.05f;
    [Range(0f, 0.5f)] public float topeAdaptacion = 0.25f;
    [Tooltip("Pesos de la reaccion: parry, rodada a tu espalda, rodada hacia atras.")]
    public Vector3 pesosReaccion = new Vector3(1f, 1f, 0.6f);
    [Tooltip("Si bebes un frasco a esta distancia o menos, va a por ti con el desenvaine.")]
    [Min(0f)] public float distanciaCastigoFrasco = 9f;
    [Tooltip("Si te quedas quieto este tiempo (segundos), salta sobre ti.")]
    [Min(0.5f)] public float quietoParaSaltar = 2.2f;
    [Tooltip("A veces, tras su ataque, envaina para provocarte (probabilidad); si te acercas, desenvaine.")]
    [Range(0f, 1f)] public float probProvocar = 0.15f;
    [Min(0.3f)] public float duracionProvocar = 1.6f;

    [Header("Aturdimiento")]
    [Tooltip("Aturdido si paras todos los golpes de un combo de 2 o mas (segundos).")]
    [Min(0f)] public float aturdidoCombo = 1.6f;
    [Tooltip("Dano extra que recibe aturdido (1,3 = un 30 % mas).")]
    [Min(1f)] public float multDanoAturdido = 1.3f;
    public string textoAturdido = "Aturdido";
    public Color colorTextoAturdido = new Color(1f, 0.85f, 0.45f, 1f);
    [Min(0f)] public float temblorAturdido = 0.04f;

    [Header("Especial: ondas de fuego (desde la fase 2)")]
    [Tooltip("Probabilidad de elegirlo en cada decision (fase 2 y fase 3), con un enfriamiento minimo.")]
    public Vector2 probOndas = new Vector2(0.14f, 0.12f);
    [Min(0f)] public float enfriamientoOndas = 22f;
    [Tooltip("El empujon del principio (velocidad x, y que te da) y su dano.")]
    public Vector2 empujon = new Vector2(11f, 6f);
    [Min(0)] public int danoEmpujon = 8;
    [Tooltip("Ondas que lanza como maximo y tiempo maximo del ataque (segundos).")]
    [Min(1)] public int ondasMaximas = 14;
    [Min(1f)] public float duracionMaxOndas = 16f;
    [Tooltip("Tiempo entre una onda y la siguiente (segundos, min y max). Tiene que dar tiempo a recuperar la esquiva (0,85 s).")]
    public Vector2 intervaloOndas = new Vector2(0.95f, 1.25f);
    [Tooltip("Velocidad de las ondas (unidades por segundo).")]
    [Min(1f)] public float velocidadOnda = 8.5f;
    [Min(0)] public int danoOnda = 22;
    [Tooltip("Caja de la onda (ancho y alto). Mas alta que un salto: solo se pasa esquivando.")]
    public Vector2 cajaOnda = new Vector2(0.55f, 5.5f);
    [Tooltip("Tamano del dibujo de la onda (unidades).")]
    public Vector2 tamanoOnda = new Vector2(1.6f, 6f);
    public Color colorOnda = new Color(1f, 0.6f, 0.15f, 1f);
    public Color colorOnda2 = new Color(1f, 0.92f, 0.5f, 1f);
    [Tooltip("Golpes que hay que darle para cortar las ondas (con 1 basta).")]
    [Min(1)] public int golpesParaCortar = 1;
    [Tooltip("Al cortarle las ondas, queda aturdido (segundos).")]
    [Min(0f)] public float aturdidoAlCortar = 1.4f;

    [Header("Especial: sombras espectrales (fase 3)")]
    [Range(0f, 1f)] public float probSombras = 0.13f;
    [Min(0f)] public float enfriamientoSombras = 26f;
    [Tooltip("Lo que dura (segundos) con la espada clavada.")]
    [Min(1f)] public float duracionSombras = 10f;
    [Tooltip("Pausa entre una sombra y la siguiente (segundos).")]
    public Vector2 pausaSombras = new Vector2(0.35f, 0.6f);
    [Tooltip("Dano de cada golpe de sombra (fraccion del golpe normal).")]
    [Range(0.1f, 1.5f)] public float danoSombra = 0.8f;
    [Tooltip("Dano que le entra al jefe con la espada clavada (1 = normal).")]
    [Min(0f)] public float danoClavado = 1f;
    public Color colorSombra = new Color(0.35f, 0.25f, 0.55f, 0.75f);
    public Color colorSombraBorde = new Color(0.75f, 0.6f, 1f, 1f);
    [Tooltip("Las sombras salen a esta distancia de ti (unidades).")]
    [Min(0.5f)] public float distanciaSombra = 1.3f;
    [Tooltip("Altura desde la que cae la sombra de arriba (unidades sobre ti).")]
    [Min(1f)] public float alturaSombraArriba = 4.5f;

    [Header("Especial: amuleto sellado (desde la fase 2)")]
    [Tooltip("Probabilidad de elegirlo en cada decision (fase 2 y fase 3). Solo si llevas algun amuleto que se pueda sellar.")]
    public Vector2 probSello = new Vector2(0.1f, 0.08f);
    [Min(0f)] public float enfriamientoSello = 45f;
    [Tooltip("Lo que dura el sello (segundos).")]
    [Min(1f)] public float duracionSello = 25f;
    [Tooltip("Aviso (la pose del buff) antes de sellar (segundos).")]
    [Min(0f)] public float avisoSello = 0.9f;
    public Color colorSello = new Color(0.55f, 0.3f, 0.95f, 1f);
    public string textoSello = "Sellado";

    [Header("Especial: erupciones (desde la fase 2)")]
    public Vector2 probErupciones = new Vector2(0.12f, 0.1f);
    [Min(0f)] public float enfriamientoErupciones = 24f;
    [Tooltip("Lo que dura la lluvia de erupciones (segundos).")]
    [Min(1f)] public float duracionErupciones = 6f;
    [Tooltip("Tiempo entre una erupcion y la siguiente (segundos, min y max).")]
    public Vector2 intervaloErupciones = new Vector2(0.22f, 0.45f);
    [Tooltip("Aviso de cada erupcion (la grieta que brilla en el suelo, segundos).")]
    [Min(0.3f)] public float avisoErupcion = 0.85f;
    [Tooltip("Ancho y alto de cada erupcion (min y max, unidades).")]
    public Vector2 anchoErupcion = new Vector2(0.8f, 2.4f);
    public Vector2 altoErupcion = new Vector2(2f, 5f);
    [Tooltip("Probabilidad de que una erupcion salga justo donde estas.")]
    [Range(0f, 1f)] public float probBajoJugador = 0.35f;
    [Min(0)] public int danoErupcion = 24;
    [Tooltip("Quemadura de cada erupcion (de 100).")]
    [Min(0f)] public float quemaduraErupcion = 30f;
    public Color colorErupcion = new Color(1f, 0.45f, 0.1f, 1f);
    public Color colorErupcion2 = new Color(1f, 0.9f, 0.45f, 1f);

    [Header("Animacion (cuadros por segundo)")]
    [Min(1f)] public float fpsEspera = 9f;
    [Min(1f)] public float fpsCorrer = 14f;
    [Min(1f)] public float fpsAtaque = 14f;
    [Min(1f)] public float fpsRodada = 16f;
    [Min(1f)] public float fpsOtros = 12f;

    public float Afinidad(Elemento e)
    {
        switch (e)
        {
            case Elemento.Fuego: return multFuego;
            case Elemento.Oscuro: return multOscuridad;
            case Elemento.Sangrado: return multSangrado;
            case Elemento.Hielo: return multHielo;
            case Elemento.Sagrado: return multSagrado;
            default: return 1f;
        }
    }

    public Golpe Buscar(string nombre) => golpes.Find(g => g != null && g.nombre == nombre);
    public Ataque BuscarAtaque(string id) => ataques.Find(a => a != null && a.id == id);

    public Vector2 Pausa(int fase) => fase >= 3 ? pausaFase3 : fase == 2 ? pausaFase2 : pausaFase1;
    public float Velocidad(int fase) => fase >= 3 ? velocidadFase3 : fase == 2 ? velocidadFase2 : 1f;
    public int Vida(int fase) => fase >= 3 ? vidaFase3 : fase == 2 ? vidaFase2 : vidaFase1;
    public static float PorFase(Vector3 v, int fase) => fase >= 3 ? v.z : fase == 2 ? v.y : v.x;
}
