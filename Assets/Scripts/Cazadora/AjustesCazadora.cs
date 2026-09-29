using System;
using UnityEngine;

// Todos los numeros de The Blind Huntress en un solo sitio (Assets/Data/Jefes/
// Ajustes Cazadora): vida por barra, tiempos de cada ataque, dano, oido, escudo,
// instakills, camara, musica y textos. Se cambian en el Inspector sin tocar codigo.
//
// El dano va en fraccion de TU vida maxima (0.2 = un 20 %): asi el golpe pesa lo
// mismo tengas el nivel que tengas. El modo Dificil de los desafios solo sube la
// vida, el dano y acorta las pausas: los avisos no cambian nunca.
[CreateAssetMenu(menuName = "Warrior/Ajustes de la Cazadora", fileName = "Ajustes Cazadora")]
public class AjustesCazadora : ScriptableObject
{
    [Serializable]
    public class Ataque
    {
        [Tooltip("Aviso: la pose quieta y el brillo antes del golpe (segundos).")]
        public float aviso = 0.3f;
        [Tooltip("Golpe: lo que dura el tajo (segundos). La caja de dano va con los cuadros del tajo.")]
        public float golpe = 0.12f;
        [Tooltip("Recuperacion: lo que tarda en volver a actuar. Es tu ventana para castigar.")]
        public float recuperacion = 0.35f;
        [Tooltip("Dano en fraccion de tu vida maxima (0.2 = 20 %).")]
        public float dano = 0.2f;

        public Ataque() { }
        public Ataque(float a, float g, float r, float d) { aviso = a; golpe = g; recuperacion = r; dano = d; }
    }

    [Serializable]
    public class Fase
    {
        public string nombre = "La Cazadora";
        [Tooltip("Vida de esta barra.")]
        public int vida = 2400;
        [Tooltip("Rapidez de golpes y recuperaciones (1.3 = un 30 % mas rapido). Los avisos tienen un minimo y nunca bajan de el.")]
        public float velocidadAtaque = 1f;
        [Tooltip("Rapidez al correr y en los dashes.")]
        public float velocidadMovimiento = 1f;
        [Tooltip("Multiplica el dano de todos sus golpes.")]
        public float dano = 1f;
        [Tooltip("Pausa entre ataques (x = minima, y = maxima). Menos pausa = mas agresiva.")]
        public Vector2 pausa = new Vector2(0.45f, 0.8f);
        [Tooltip("Color de la barra de vida en esta fase.")]
        public Color colorBarra = new Color(0.36f, 0.5f, 0.3f, 1f);
        [Tooltip("Musica de esta fase (pista, volumen, segundo de inicio y tramo que se repite).")]
        public ConfigNivel.MusicaFase musica = new ConfigNivel.MusicaFase();

        [Header("Escenario en esta fase")]
        public Color luz = new Color(0.78f, 0.86f, 0.8f, 1f);
        [Range(0f, 2f)] public float intensidadLuz = 0.9f;
        [Tooltip("Cuanta niebla baja hay (0 = nada, 1 = mucha).")]
        [Range(0f, 1f)] public float niebla = 0.35f;
        [Tooltip("Cuanto se oscurece el cielo (0 = nada, 1 = negro).")]
        [Range(0f, 1f)] public float cieloOscuro = 0f;
    }

    [Serializable]
    public class Escudo
    {
        [Tooltip("Golpes tuyos que lo rompen (cada golpe que conecta cuenta 1, haga el dano que haga).")]
        public int golpes = 10;
        [Tooltip("Golpes que hacen falta si tu arma esta imbuida de OSCURIDAD (contrarresta la luz).")]
        public int golpesOscuridad = 5;
        [Tooltip("Tiempo que tienes para romperlo.")]
        public float tiempo = 7f;
        [Tooltip("Lo que se cura si no lo rompes (fraccion de la barra).")]
        [Range(0f, 1f)] public float cura = 0.3f;
        [Tooltip("Aturdida al romperle el escudo (segundos reales: la camara lenta no lo alarga).")]
        public float aturdida = 3f;
        [Tooltip("Dano extra que recibe mientras esta aturdida por el escudo roto (1.3 = +30 %).")]
        public float bonusDano = 1.3f;
        [Tooltip("Cada cuantos segundos lanza olas de luz bajas mientras se cura.")]
        public float cadaOla = 1.8f;
        [Tooltip("Olas de luz por los dos lados a la vez.")]
        public bool olasDosLados;
        [Tooltip("Caen ilusiones del cielo (con tajo descendente) mientras te acercas.")]
        public bool ilusionesCaen;
        [Tooltip("Segundos desde que sube el escudo hasta la primera caida.")]
        public float inicioCaidas = 0.7f;
        [Tooltip("Cada cuantos segundos caen.")]
        public float cadaCaida = 1.3f;
        [Tooltip("Cuantas caen cada vez.")]
        public int ilusionesPorCaida = 1;

        public Escudo() { }
        public Escudo(float t, float ola, bool dos, bool caen, float inicio, float cada, int cuantas)
        {
            tiempo = t; cadaOla = ola; olasDosLados = dos; ilusionesCaen = caen; inicioCaidas = inicio; cadaCaida = cada; ilusionesPorCaida = cuantas;
        }
    }

    // ------------------------------------------------------------------ Fases

    [Header("Fases (una por barra de vida)")]
    public Fase[] fases =
    {
        new Fase { nombre = "La Cazadora", vida = 2400, velocidadAtaque = 1f, velocidadMovimiento = 1f, dano = 1f, pausa = new Vector2(0.45f, 0.8f),
                   colorBarra = new Color(0.4f, 0.52f, 0.3f), luz = new Color(0.78f, 0.86f, 0.8f), intensidadLuz = 0.9f, niebla = 0.3f, cieloOscuro = 0f },
        new Fase { nombre = "La Presa Marcada", vida = 2400, velocidadAtaque = 1.15f, velocidadMovimiento = 1.12f, dano = 1.05f, pausa = new Vector2(0.3f, 0.6f),
                   colorBarra = new Color(0.62f, 0.62f, 0.3f), luz = new Color(0.62f, 0.72f, 0.66f), intensidadLuz = 0.75f, niebla = 0.65f, cieloOscuro = 0.45f },
        new Fase { nombre = "Ciega de Verdad", vida = 2400, velocidadAtaque = 1.3f, velocidadMovimiento = 1.25f, dano = 1.1f, pausa = new Vector2(0.2f, 0.45f),
                   colorBarra = new Color(0.75f, 0.2f, 0.2f), luz = new Color(0.45f, 0.5f, 0.5f), intensidadLuz = 0.6f, niebla = 0.8f, cieloOscuro = 0.85f },
    };

    [Tooltip("El aviso de un ataque ligero nunca baja de esto, por rapida que vaya la fase.")]
    public float avisoMinimoLigero = 0.25f;
    [Tooltip("El aviso de un ataque pesado nunca baja de esto.")]
    public float avisoMinimoPesado = 0.5f;
    [Tooltip("Pausa garantizada tras cada ataque grande, aunque sea el modo Dificil.")]
    public float castigoMinimo = 0.6f;

    // ------------------------------------------------------------------ Ataques

    [Header("Fase 1: Tres Lunas (barrido, luna, estocada)")]
    public Ataque barrido = new Ataque(0.3f, 0.1f, 0.12f, 0.18f);
    public Ataque luna = new Ataque(0.22f, 0.14f, 0.12f, 0.18f);
    public Ataque estocadaCombo = new Ataque(0.22f, 0.16f, 0.75f, 0.24f);
    [Tooltip("Pausa entre golpe y golpe del combo.")]
    public float enlaceCombo = 0.08f;

    [Header("Fase 1: Cruce (dash que te atraviesa + tajo hacia atras)")]
    public Ataque cruce = new Ataque(0.55f, 0.28f, 0.1f, 0.3f);
    public Ataque tajoAtras = new Ataque(0.2f, 0.12f, 0.65f, 0.2f);
    [Tooltip("Hasta donde pasa de largo al cruzar (unidades detras de ti).")]
    public float cruceDetras = 3f;

    [Header("Fase 1: Ola de luz (se salta)")]
    public Ataque ola = new Ataque(0.45f, 0.1f, 0.5f, 0.16f);
    public float velocidadOla = 11f;
    public float alcanceOla = 26f;

    [Header("Fase 1: Tajo ascendente (castiga los saltos)")]
    public Ataque tajoAscendente = new Ataque(0.26f, 0.14f, 0.45f, 0.22f);
    [Tooltip("Si estas en el aire encima de ella a menos de esta distancia, lo usa.")]
    public float distanciaAntiSalto = 2.6f;

    [Header("Fase 1: Salto con tajo descendente")]
    public Ataque saltoDescendente = new Ataque(0.5f, 0.15f, 0.65f, 0.38f);
    public float alturaSalto = 4.5f;

    [Header("Parry de la Cazadora (si atacas sin parar)")]
    [Tooltip("Golpes seguidos tuyos que la hacen ponerse en guardia...")]
    public int golpesParaParry = 4;
    [Tooltip("...dentro de estos segundos.")]
    public float ventanaGolpes = 2.6f;
    [Tooltip("Lo que dura la pose de guardia (el brillo en la espada). Si no la golpeas, se apaga.")]
    public float duracionPose = 1.1f;
    [Tooltip("Tiempo minimo entre dos guardias.")]
    public float enfriamientoParry = 12f;
    [Tooltip("Pausa muy breve al contacto (segundos reales).")]
    public float hitStopParry = 0.08f;
    [Tooltip("Lo que quedas aturdido cuando te para el golpe (sin moverte, atacar, rodar, saltar, beber ni imbuir). Cubre hasta la estocada.")]
    public float aturdimientoJugador = 1.4f;
    [Tooltip("Retroceso corto y suave al contacto (unidades).")]
    public float retrocesoParry = 1.2f;
    [Tooltip("Del contacto a que se lanza a por ti (segundos).")]
    public float esperaEstocada = 0.35f;
    [Tooltip("Si estas mas lejos que esto, te alcanza con un dash (Dash attack).")]
    public float alcanceEstocada = 1.3f;
    [Tooltip("Velocidad de ese dash (unidades por segundo).")]
    public float velocidadDashParry = 18f;
    [Tooltip("Dano de la estocada (fraccion de tu vida maxima). ES LETAL si te queda esto o menos.")]
    public float danoEstocadaParry = 0.5f;
    [Tooltip("Con menos de la mitad de vida: latido, vineta roja y esta pausa antes de la estocada (segundos reales).")]
    public float pausaDramatica = 0.5f;
    [Tooltip("Si sobrevives, sales despedido esta distancia (unidades).")]
    public float empujonDistancia = 6f;
    [Tooltip("Altura del empujon (velocidad hacia arriba).")]
    public float empujonAltura = 6f;
    [Tooltip("Invulnerable mientras caes y te levantas.")]
    public bool invulnerableAlCaer = true;
    [Tooltip("Lo que ella se queda quieta tras la estocada (tu tiempo para reaccionar).")]
    public float recuperacionTrasEstocada = 0.8f;

    [Header("Rugido y escudo (a la barra le queda poco)")]
    [Range(0f, 1f)] public float umbralEscudo = 0.25f;
    [Tooltip("Empuje del rugido (no hace dano).")]
    public float empujeRugido = 14f;
    [Tooltip("El rugido deja de empujarte cuando estas a esta distancia de ella (no te lanza contra la pared).")]
    public float empujeMaximoRugido = 3.5f;
    [Tooltip("Cuanto se aleja de ti para curarse (un tercio de pantalla, mas o menos). Siempre hacia el lado con sitio.")]
    public float distanciaEscudo = 7.5f;
    [Tooltip("Tamano de la camara mientras se cura (mas grande = mas abierta).")]
    public float zoomEscudo = 7.8f;
    [Tooltip("Al romperlo: escala del tiempo, duracion real y acercamiento de la camara.")]
    [Range(0.05f, 1f)] public float lentaRotura = 0.3f;
    public float lentaRoturaTiempo = 0.45f;
    public float zoomRotura = 5.4f;
    public Escudo[] escudos =
    {
        // Fase 1: olas bajas frecuentes (hay que saltarlas mientras le pegas).
        new Escudo(7f, 1.2f, false, false, 0.7f, 1.3f, 1),
        // Fase 2: ilusiones que caen pronto y de dos en dos.
        new Escudo(7f, 1.6f, false, true, 0.3f, 0.9f, 2),
        // Fase 3: olas por los dos lados, ilusiones y menos tiempo (y la oscuridad).
        new Escudo(5.5f, 1.3f, true, true, 0.3f, 1f, 2),
    };

    [Header("Fase 2: elementos")]
    [Tooltip("Llamas que deja el fuego en el suelo (segundos).")]
    public float duracionLlamas = 2.5f;
    public float acumulacionEstado = 35f;
    [Tooltip("Lo que se cura con cada golpe de sangre (fraccion de la barra).")]
    public float roboSangre = 0.03f;
    [Tooltip("Tope de cura por golpes de sangre en cada barra (fraccion de la barra).")]
    public float topeRoboSangre = 0.1f;
    [Tooltip("El sagrado va mas rapido y mas ancho.")]
    public float sagradoRapidez = 1.25f;
    public float sagradoAncho = 1.3f;

    [Header("Fase 2: contraataque elemental")]
    [Tooltip("Resiste el elemento que llevas imbuido: el dano se multiplica por esto.")]
    public float resistenciaCopiada = 0.35f;
    public float duracionResistencia = 6f;
    public float enfriamientoResistencia = 14f;

    [Header("Fase 2: ilusiones")]
    public Ataque flanqueo = new Ataque(0.5f, 0.14f, 0.7f, 0.4f);
    [Tooltip("Dano de la ilusion en el flanqueo (la real hace un poco mas).")]
    public float danoIlusion = 0.34f;
    public Ataque ilusionCae = new Ataque(0.6f, 0.12f, 0.3f, 0.26f);
    public float danoExplosionEspejismo = 0.22f;
    public float vidaEspejismo = 7f;
    public Ataque cuchilladaFantasma = new Ataque(0.6f, 0.12f, 0.2f, 0.24f);
    [Tooltip("La cuchillada aparece donde estabas hace este tiempo.")]
    public float retrasoFantasma = 1f;
    public Ataque trampaSonido = new Ataque(0.25f, 0.12f, 0.5f, 0.2f);
    public int trampasPorVez = 3;
    public float vidaTrampas = 9f;
    public Ataque lluvia = new Ataque(0.6f, 0.15f, 0.7f, 0.3f);
    public Ataque cruceDoble = new Ataque(0.55f, 0.28f, 0.6f, 0.3f);
    public Color colorIlusion = new Color(0.6f, 0.78f, 1f, 0.55f);

    [Header("Fase 3")]
    public Ataque danzaGolpe = new Ataque(0.22f, 0.09f, 0.05f, 0.07f);
    public Ataque danzaFinal = new Ataque(0.6f, 0.16f, 1.6f, 0.3f);
    public Vector2Int golpesDanza = new Vector2Int(8, 12);
    public int ilusionesEspejos = 3;
    [Tooltip("Robo de vida al golpearte (fraccion de la barra).")]
    public float roboVidaFase3 = 0.04f;
    [Tooltip("Tope de vida robada en toda la pelea (fraccion de la barra).")]
    public float topeRoboFase3 = 0.12f;
    [Tooltip("Radio del circulo de luz alrededor de ti.")]
    public float radioLuz = 4.2f;
    [Range(0f, 1f)] public float opacidadOscuridad = 0.92f;

    [Header("Instakills (uno por fase; los avisos no cambian en Dificil)")]
    [Tooltip("Vida que le tiene que quedar a la barra (o menos) para que pueda lanzarlo.")]
    [Range(0f, 1f)] public float umbralInstakill = 0.65f;
    [Tooltip("Ejecucion: aviso de la linea roja.")]
    public float avisoEjecucion = 1.35f;
    public float velocidadEjecucion = 40f;
    [Tooltip("Sentencia: la marca te sigue este tiempo...")]
    public float seguimientoSentencia = 1.5f;
    [Tooltip("...y, fijada, cae el tajo tras este tiempo.")]
    public float fijoSentencia = 0.6f;
    public float anchoSentencia = 2.4f;
    [Tooltip("El Silencio: segundos que tienes que aguantar sin hacer ruido.")]
    public float duracionSilencio = 4f;
    [Tooltip("Expuesta tras esquivar un instakill.")]
    public float expuestaTrasInstakill = 2.2f;
    [Tooltip("Nunca lanza un instakill hasta este tiempo despues de que reaparezcas.")]
    public float esperaTrasReaparecer = 5f;

    // ------------------------------------------------------------------ Oido

    [Header("Oido (es ciega: caza por el sonido)")]
    [Tooltip("De cerca te siente siempre: sus ataques van a donde estas.")]
    public float rangoCercano = 4.5f;
    [Tooltip("Un ruido de volumen 1 se oye hasta esta distancia.")]
    public float alcanceOido = 20f;
    [Tooltip("Sin oirte este tiempo, va a donde te oyo por ultima vez y adivina.")]
    public float olvido = 4f;
    [Tooltip("Si falla un ataque porque ya no estabas donde te oyo, se queda confundida este tiempo.")]
    public float confusion = 0.5f;
    public float ruidoAtacar = 1f;
    public float ruidoRodar = 0.8f;
    public float ruidoAterrizar = 0.6f;
    public float ruidoFrasco = 1f;
    public float ruidoImbuir = 0.9f;
    public float ruidoCorrer = 0.45f;
    public float ruidoSaltar = 0.35f;
    [Tooltip("Muestra un anillo pequeno donde te oye (sin el, la mecanica no se entiende).")]
    public bool anilloRuido = true;

    // ------------------------------------------------------------------ Movimiento

    [Header("Movimiento")]
    public float velocidadCorrer = 6.5f;
    [Tooltip("Aceleracion al arrancar y frenada (unidades/s por segundo).")]
    public float aceleracion = 38f;
    public float frenada = 46f;
    [Tooltip("Distancia a la que le gusta pelear de cerca.")]
    public float distanciaCerca = 2.2f;
    [Tooltip("A partir de aqui cuenta como lejos (proyectiles y dashes).")]
    public float distanciaLejos = 6.5f;
    [Tooltip("Imagenes fantasma que deja al hacer un dash (cada cuanto sale una).")]
    public float cadaEstela = 0.025f;
    public float vidaEstela = 0.28f;

    // ------------------------------------------------------------------ Camara

    [Header("Camara")]
    [Tooltip("Apertura maxima al mantenerte a ti y a ella en pantalla.")]
    public float aperturaMaxima = 8.4f;
    [Tooltip("Distancia entre los dos a partir de la que la camara se abre.")]
    public float distanciaApertura = 9f;
    [Tooltip("Cuanto se desplaza el encuadre hacia ella (0 = solo tu, 1 = centrada en ella).")]
    [Range(0f, 1f)] public float pesoEncuadre = 0.4f;
    [Tooltip("Cuanto puede apartarse el encuadre de ti para verla a ella (unidades).")]
    public float apartamientoMaximo = 9f;
    public float zoomInstakill = 4.6f;
    public float zoomTransicion = 4.2f;
    public float zoomParry = 4.4f;
    public float temblorLigero = 0.25f;
    public float temblorPesado = 0.7f;
    public float temblorRugido = 1.3f;
    public float temblorAterrizaje = 0.9f;
    [Tooltip("Ninguna sacudida pasa de esto.")]
    public float temblorMaximo = 1.5f;
    [Tooltip("Congelado breve al conectar golpes pesados (segundos).")]
    public float congeladoPesado = 0.07f;
    [Tooltip("Camara lenta en las transiciones de fase (escala del tiempo y duracion real).")]
    public float lentaTransicion = 0.35f;
    public float lentaTransicionTiempo = 1.1f;
    [Tooltip("Camara lenta en tu parry perfecto.")]
    public float lentaParry = 0.4f;
    public float lentaParryTiempo = 0.35f;
    [Tooltip("Cuanto sube la camara cuando caen ilusiones del cielo.")]
    public float subidaIlusiones = 1.6f;

    // ------------------------------------------------------------------ Musica y sonido

    [Header("Musica")]
    [Tooltip("Segundos del fundido cruzado entre fases.")]
    public float crossfade = 2.2f;
    [Tooltip("Volumen de la musica durante el aviso de un instakill (fraccion).")]
    [Range(0f, 1f)] public float musicaEnAviso = 0.3f;
    [Tooltip("Volumen de la musica en El Silencio.")]
    [Range(0f, 1f)] public float musicaEnSilencio = 0.04f;
    [Tooltip("Ambiente del bosque (bucle).")]
    public AudioClip ambiente;
    [Range(0f, 1f)] public float volumenAmbiente = 0.35f;
    [Tooltip("Viento para El Silencio (bucle).")]
    public AudioClip viento;
    [Range(0f, 1f)] public float volumenViento = 0.5f;

    // ------------------------------------------------------------------ Dialogo

    [Header("Dialogo de entrada")]
    public string nombreDialogo = "La Cazadora Ciega";
    [TextArea(2, 4)]
    public string[] frasesEntrada =
    {
        "Ah. Otro paso en mi bosque. Lo oí desde lejos.",
        "No necesito ojos para saber lo que eres: una respiración nerviosa, un metal que tiembla.",
        "Hazme un favor, cazador. Da la vuelta. Los árboles ya tienen suficientes huesos.",
    };
    public string opcionRetirarse = "Retirarme";
    public string opcionDesafiar = "Voy a derrotarte";
    [TextArea(2, 4)]
    public string[] respuestaRetirarse =
    {
        "Qué dulce. ¿De verdad creíste que te dejaría salir?",
        "La presa nunca decide cuándo termina la cacería.",
    };
    [TextArea(2, 4)]
    public string[] respuestaDesafiar =
    {
        "...Ja. Qué valiente suena el miedo.",
        "Veamos si tus oídos son tan rápidos como tu lengua.",
    };
    public string trasParryDialogo = "...Interesante.";
    [Tooltip("Sonido de las letras al escribirse (suena mientras aparece el texto). Vacio = el tic de los menus.")]
    public AudioClip sonidoEscritura;
    [Range(0f, 1f)] public float volumenEscritura = 0.35f;
    [Tooltip("Letras por segundo del dialogo.")]
    public float letrasPorSegundo = 34f;
    [Tooltip("Segundos manteniendo F para saltar el dialogo.")]
    public float mantenerParaSaltar = 1f;
    [Tooltip("Tajo tras 'Voy a derrotarte': aviso minimo (pero legible) y dano (nunca mortal).")]
    public float avisoTajoDialogo = 0.32f;
    public float danoTajoDialogo = 0.3f;
    [Tooltip("Ventana de parry para ese tajo (la normal es 0.2 s): un poco mas corta.")]
    public float ventanaParryDialogo = 0.14f;

    [Header("Frases en combate (subtitulos)")]
    public string finBarra1 = "¿Eso fue todo el ruido que sabes hacer?";
    public string finBarra2 = "Ya casi puedo oír tu final.";
    public string muerteFinal = "...Por fin... silencio.";

    [Header("Colores de sus efectos (el blanco se tine)")]
    public Color colorNormal = new Color(0.93f, 0.97f, 1f, 1f);
    public Color colorFuego = new Color(1f, 0.55f, 0.16f, 1f);
    public Color colorHielo = new Color(0.55f, 0.86f, 1f, 1f);
    public Color colorSagrado = new Color(1f, 0.93f, 0.55f, 1f);
    public Color colorSangre = new Color(0.92f, 0.12f, 0.16f, 1f);
    public Color colorInstakill = new Color(1f, 0.08f, 0.1f, 1f);
    [Tooltip("Contorno leve que la separa del fondo oscuro (0 = sin contorno). En la oscuridad de la fase 3 brilla mas.")]
    [Range(0f, 1f)] public float contorno = 0.35f;
    [Range(0f, 1f)] public float contornoOscuridad = 0.8f;

    public Fase FaseN(int i) => fases[Mathf.Clamp(i, 0, fases.Length - 1)];
    public Escudo EscudoN(int i) => escudos[Mathf.Clamp(i, 0, escudos.Length - 1)];
}
