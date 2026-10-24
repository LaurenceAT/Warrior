using System;
using System.Collections.Generic;
using UnityEngine;

// Numeros del Impaler (jefe nuevo, SOLO en la sala de pruebas). Su ficha es
// Assets/Data/Jefes/Ajustes Impaler: todo lo ajustable esta aqui.
//
// Cada ataque es un clip de sus sprites (Assets/Sprites/Impaler) y una lista de
// golpes. Los cuadros se cuentan desde 0 dentro del clip (100 ms por cuadro).
// El dibujo no se cambia: para alargar un aviso solo se sostiene un cuadro que
// ya esta repetido en el dibujo (cuadroPausa).
public class AjustesImpaler : ScriptableObject
{
    // Contraataque: el tajo blanco tras la guardia (su aviso es blanco y fijo).
    public enum Peso { Ligero, Pesado, Contraataque }

    [Serializable]
    public class Golpe
    {
        public string nombre;
        [Tooltip("Ligero: aviso naranja. Pesado: aviso rojo.")]
        public Peso peso;
        [Tooltip("Cuadro del clip donde empieza la senal (desde aqui se ve el aviso).")]
        [Min(0)] public int senal;
        [Tooltip("Cuadros del clip con la caja de golpe activa (los dos incluidos).")]
        [Min(0)] public int activoDesde, activoHasta;
        [Tooltip("Cuadro que se sostiene si hace falta mas aviso: uno repetido en el dibujo o el primero del clip, la pose de salida (-1 = ninguno).")]
        public int cuadroPausa = -1;
        [Tooltip("Pausa que cambia cada vez en el cuadroPausa (x = minima, y = maxima, segundos; 0 = sin pausa variable).")]
        public Vector2 pausaVariable;
        [Tooltip("Aviso deseado: de la senal al golpe (segundos). Nunca baja del minimo de su peso.")]
        [Min(0f)] public float aviso;
        [Tooltip("Dano al jugador (puntos de vida, antes del multiplicador de Dificil).")]
        [Min(0)] public int dano;
        [Tooltip("Caja de golpe: centro respecto a los pies (x hacia donde mira) y tamano, en unidades.")]
        public Vector2 cajaCentro, cajaTamano;
        [Tooltip("De donde viene el golpe para pararlo (unidades por delante del jefe; 0 = el jefe). El eco del Cruce golpea desde su lado.")]
        public float origenX;
        [Tooltip("Fase 1: si lo paras, el resto del ataque no sale.")]
        public bool parryCancelaFase1;
        [Tooltip("Fase 2: Quemadura que acumula si te da (bloqueado, un 30 %). 0 = ninguna.")]
        [Min(0f)] public float quemadura;
    }

    [Serializable]
    public class Ataque
    {
        [Tooltip("Nombre interno (el de la sala de pruebas).")]
        public string id;
        public string nombre;
        [Tooltip("Clip de sus sprites (attack1..attack6, counter).")]
        public string clip;
        [Tooltip("Se acerca andando hasta esta distancia del jugador antes de atacar (unidades).")]
        [Min(0f)] public float alcance = 4f;
        [Tooltip("Frecuencia al elegir (0 = nunca solo; se puede lanzar desde la sala).")]
        [Min(0f)] public float frecuencia = 1f;
        public List<Golpe> golpes = new List<Golpe>();
        [Tooltip("Se queda quieto tras el dibujo del ataque (segundos).")]
        [Min(0f)] public float recuperacion = 0.3f;

        [Header("Salto o embestida (cuadros del clip; -1 = no se mueve)")]
        [Tooltip("Entre estos cuadros avanza hacia donde estabas al empezar a moverse.")]
        public int moverDesde = -1, moverHasta = -1;
        [Tooltip("Se para a esta distancia delante de ti (unidades).")]
        [Min(0f)] public float pararAntes = 1f;
        [Tooltip("Lo maximo que avanza (unidades).")]
        [Min(0f)] public float distanciaMax = 6f;
        [Tooltip("Es un salto: pasa por encima del terreno y cae en el suelo de verdad bajo ti. Si no, embiste por el suelo y una pared lo para.")]
        public bool salto;

        [Tooltip("Es el contraataque: guardia primero y su golpe solo sale si le pegas durante ella.")]
        public bool contraataque;
        [Tooltip("Adaptacion: si haces muchos parrys, puede alargar el retraso entre la senal y el golpe (sosteniendo sus cuadros de pausa; el aviso sigue visible).")]
        public bool admiteRetraso = true;

        [Tooltip("Hechizos de la fase 2 de este ataque (cada uno se puede apagar).")]
        public List<Hechizo> hechizosFase2 = new List<Hechizo>();

        [Header("Ventana de castigo")]
        [Tooltip("Desde este cuadro queda vendido (-1 = ninguno).")]
        public int cuadroCastigo = -1;
        [Tooltip("Lo minimo que dura la ventana de castigo (segundos): se sostiene el ultimo cuadro.")]
        [Min(0f)] public float castigoMinimo;
    }

    // Hechizos de la fase 2 (los de PeligrosJefe, tal cual: sus reglas de parry y sus colores).
    public enum TipoHechizo { Vortice, Onda, MediaLuna, Orbe, Meteoros, Pilares }
    // Cuando sale: al empezar el aviso de su golpe, cuando su golpe se activa o al acabar su golpe (+ retraso).
    public enum Momento { AlAvisar, AlGolpear, TrasGolpe }

    [Serializable]
    public class Hechizo
    {
        public TipoHechizo tipo;
        [Tooltip("Apagado = no sale.")]
        public bool activo = true;
        [Tooltip("Golpe del ataque al que va unido (desde 0).")]
        [Min(0)] public int golpe;
        public Momento momento = Momento.TrasGolpe;
        [Tooltip("Espera extra desde ese momento (segundos).")]
        [Min(0f)] public float retraso;
        [Tooltip("Solo si su golpe es el ultimo de lo que hace (ataque suelto o ultimo paso del combo): asi no cae encima del golpe siguiente.")]
        public bool soloSiEsElUltimo = true;
        [Tooltip("Sale del eco del Cruce (su golpe con origen) y va hacia el jefe.")]
        public bool desdeElEco;
        [Min(0)] public int dano = 18;
        [Tooltip("Glifo de aviso antes del dano (pilares y meteoros, segundos).")]
        [Min(0f)] public float aviso = 0.9f;
        [Min(1)] public int cantidad = 1;
        [Tooltip("Separacion entre uno y otro (unidades).")]
        public float separacion = 2f;
        [Tooltip("Donde sale, por delante del jefe (unidades; negativo = detras). Meteoros: alrededor de donde aterriza.")]
        public float distancia = 1f;
        [Min(0f)] public float velocidad = 9f;
        [Tooltip("Vortice: lo que dura. Orbe: lo que vive (segundos).")]
        [Min(0f)] public float duracion = 1f;
        [Tooltip("Vortice: radio y fuerza con la que tira.")]
        [Min(0f)] public float radio = 5f, fuerza = 3f;
        [Tooltip("Sangrado que acumula (los pilares, el orbe y el vortice lo llevan).")]
        [Min(0f)] public float sangrado;
    }

    // Un paso de un combo: un ataque entero o solo sus primeros golpes.
    [Serializable]
    public class Eslabon
    {
        [Tooltip("Id del ataque.")]
        public string ataque;
        [Tooltip("Usa sus golpes hasta este (incluido, desde 0). -1 = todos.")]
        public int hastaGolpe = -1;
    }

    [Serializable]
    public class Combo
    {
        public string id;
        public string nombre;
        [Tooltip("Solo en la fase 2.")]
        public bool soloFase2;
        [Tooltip("Frecuencia al elegir (0 = nunca solo; se puede lanzar desde la sala).")]
        [Min(0f)] public float frecuencia = 1f;
        public List<Eslabon> eslabones = new List<Eslabon>();
        [Tooltip("En Dificil se anaden al final (puede estar vacia). Cumplen las mismas reglas: aviso minimo, pausa de recuperacion y tope de dano.")]
        public List<Eslabon> extraDificil = new List<Eslabon>();
    }

    [Header("Vida por fase")]
    [Min(1)] public int vidaFase1 = 1600;
    [Min(1)] public int vidaFase2 = 1800;

    [Header("Movimiento")]
    [Tooltip("Velocidad al andar (unidades por segundo).")]
    [Min(0f)] public float velocidadAndar = 2.2f;
    [Tooltip("Lo maximo que anda antes de atacar igualmente (segundos).")]
    [Min(0f)] public float tiempoMaxAndar = 2.5f;
    [Tooltip("Distancia minima a los bordes de la arena (unidades).")]
    [Min(0f)] public float margenArena = 1f;

    [Header("Pausas entre ataques (x = minima, y = maxima, segundos)")]
    public Vector2 pausaFase1 = new Vector2(0.9f, 1.5f);
    public Vector2 pausaFase2 = new Vector2(0.5f, 0.9f);
    [Tooltip("Fase 2: el dibujo va mas rapido (1 = igual). Los avisos nunca bajan de su minimo.")]
    [Min(0.1f)] public float multVelocidadFase2 = 1.25f;

    [Header("Avisos")]
    [Tooltip("Aviso minimo de un golpe ligero (segundos).")]
    [Min(0f)] public float avisoMinLigero = 0.35f;
    [Tooltip("Aviso minimo de un golpe pesado (segundos).")]
    [Min(0f)] public float avisoMinPesado = 0.5f;
    [Tooltip("Aviso minimo del ultimo golpe de un combo (segundos).")]
    [Min(0f)] public float avisoMinFinal = 0.6f;
    [Tooltip("Pausa minima entre el final de un golpe y el siguiente (segundos). El jugador machaca si pulsa antes de 0,5 s.")]
    [Min(0f)] public float pausaMinEntreGolpes = 0.55f;
    public Color colorLigero = new Color(1f, 0.55f, 0.1f, 1f);
    public Color colorPesado = new Color(1f, 0.12f, 0.08f, 1f);
    [Tooltip("Reservado al contraataque.")]
    public Color colorContraataque = Color.white;
    [Range(0f, 1f)] public float intensidadAviso = 0.6f;
    [Tooltip("Ritmo del aviso, para que no dependa solo del color: parpadeos por segundo del golpe ligero (rapido).")]
    [Min(0.1f)] public float parpadeoLigero = 9f;
    [Tooltip("Parpadeos por segundo del golpe pesado (pulso lento y mas fuerte).")]
    [Min(0.1f)] public float parpadeoPesado = 2.5f;

    [Header("Contraataque blanco")]
    [Tooltip("Lo que dura la guardia si no le pegas (segundos).")]
    [Min(0.1f)] public float duracionGuardia = 2f;
    [Tooltip("Retraso minimo desde tu golpe hasta su tajo blanco (segundos): su aviso blanco.")]
    [Min(0f)] public float retrasoContraataque = 0.45f;
    [Tooltip("Aviso minimo del tajo blanco (segundos).")]
    [Min(0f)] public float avisoMinContraataque = 0.4f;
    [Tooltip("Dano que le entra durante la guardia (0,4 = el 40 %).")]
    [Range(0f, 1f)] public float danoEnGuardia = 0.4f;
    [Tooltip("Aturdido si paras el tajo blanco (segundos).")]
    [Min(0f)] public float aturdidoParryContraataque = 3.2f;
    [Tooltip("Brillo blanco suave mientras esta en guardia (0 = ninguno).")]
    [Range(0f, 1f)] public float brilloGuardia = 0.15f;

    [Header("Combos (el combo sigue aunque pares sus golpes)")]
    [Tooltip("Probabilidad de elegir un combo en vez de un ataque suelto.")]
    [Range(0f, 1f)] public float probabilidadCombo = 0.55f;
    [Tooltip("Golpes seguidos como maximo sin una pausa de recuperacion.")]
    [Min(1)] public int maxGolpesSeguidos = 3;
    [Tooltip("Pausa de recuperacion dentro de un combo (segundos).")]
    [Min(0f)] public float pausaRecuperacion = 0.8f;
    [Tooltip("Un combo sin parar nada nunca quita mas que esto de tu vida maxima (0,7 = 70 %, ya con el dano de Dificil).")]
    [Range(0.1f, 1f)] public float topeDanoCombo = 0.7f;
    public List<Combo> combos = new List<Combo>();

    [Header("Adaptacion al jugador (suave y con tope; se reinicia en cada pelea)")]
    public bool adaptacionActiva = true;
    [Tooltip("Mira los ultimos N ataques o combos.")]
    [Min(1)] public int combosObservados = 4;
    [Tooltip("Si paras al menos esta parte de los golpes que te alcanzan...")]
    [Range(0f, 1f)] public float umbralParrys = 0.6f;
    [Tooltip("...el retraso extra antes del golpe sube hasta este tope (segundos). Sale al azar entre 0 y lo aprendido.")]
    [Min(0f)] public float topeRetraso = 0.3f;
    [Tooltip("Lo que sube o baja el retraso tras cada ataque o combo (segundos).")]
    [Min(0f)] public float pasoRetraso = 0.05f;
    [Tooltip("Si le pegas al menos estas veces en una guardia del contraataque...")]
    [Min(1)] public int umbralGolpesGuardia = 2;
    [Tooltip("...elige el contraataque mas a menudo, hasta este tope (x veces su frecuencia).")]
    [Min(1f)] public float topePesoContraataque = 3f;
    [Tooltip("Lo que sube o baja ese peso en cada guardia.")]
    [Min(0f)] public float pasoPesoContraataque = 0.4f;

    [Header("Parry")]
    [Tooltip("Aturdido al acabar un combo (o un ataque de 2 o mas golpes) si paraste todos los golpes que te alcanzaban, al menos dos (segundos).")]
    [Min(0f)] public float aturdidoComboPerfecto = 2.2f;
    [Tooltip("Parrys seguidos (aunque sean de combos distintos) que le rompen la postura.")]
    [Min(1)] public int parrysParaRomper = 5;
    [Tooltip("Aturdido con la postura rota (segundos).")]
    [Min(0f)] public float aturdidoPostura = 2.6f;
    [Tooltip("Parada de imagen extra al parar un golpe (segundos, sin escala, menos de 0,08). El jugador ya para la imagen 0,12 s en cada parry: esta solo se nota si la suya ya acabo.")]
    [Range(0f, 0.08f)] public float paradaImagen = 0f;
    public Color colorChispas = new Color(1f, 0.85f, 0.45f, 1f);
    public Color colorChispas2 = new Color(1f, 0.45f, 0.15f, 1f);
    [Min(0)] public int chispas = 14;
    public string textoAturdido = "Aturdido";
    public string textoPostura = "¡Postura rota!";
    public Color colorTextoAturdido = new Color(1f, 0.85f, 0.45f, 1f);
    [Tooltip("Temblor mientras esta aturdido (unidades).")]
    [Min(0f)] public float temblorAturdido = 0.05f;

    [Header("Sonido del parry (sube de tono con cada parry seguido)")]
    [Min(0.1f)] public float tonoInicial = 1f;
    [Min(0f)] public float subidaTono = 0.06f;
    [Min(0.1f)] public float tonoMaximo = 1.5f;
    [Tooltip("Vuelve al tono inicial si te da o bloqueas sin parry.")]
    public bool tonoReiniciaAlFallar = true;
    [Tooltip("Vuelve al tono inicial tras estos segundos sin parry (0 = nunca).")]
    [Min(0f)] public float tonoReiniciaSegundos = 4f;

    [Header("Afinidades (1 = normal, menos = resiste, mas = debil)")]
    [Min(0f)] public float fuego = 0.6f;
    [Min(0f)] public float hielo = 0.6f;
    [Min(0f)] public float sagrado = 1f;
    [Min(0f)] public float sangrado = 1f;
    [Min(0f)] public float oscuridad = 1.5f;

    [Header("Fase 2: lanza encendida")]
    [Tooltip("Tinte de todo el cuerpo en la fase 2 (blanco = sin tinte).")]
    public Color tinteFase2 = new Color(1f, 0.86f, 0.86f, 1f);
    [Tooltip("Brillo de la punta de la lanza (la copia del sprite con el brillo de la Sombra).")]
    public Color colorLanzaFase2 = new Color(1f, 0.3f, 0.12f, 1f);
    [Range(0f, 1f)] public float brilloLanza = 0.7f;
    [Tooltip("Colores de la punta de la lanza en el dibujo (solo esos se encienden).")]
    public Color lanza1 = new Color32(139, 93, 93, 255), lanza2 = new Color32(105, 62, 62, 255), lanza3 = new Color32(180, 138, 138, 255);
    [Range(0f, 0.3f)] public float toleranciaLanza = 0.06f;
    [Tooltip("Shader del brillo (Assets/Shaders/BladeGlow, el de la Sombra).")]
    public Shader shaderBrillo;
    [Tooltip("Colores de los hechizos (los de siempre del Wraith).")]
    public Color colorHechizos = new Color(1f, 0.18f, 0.22f, 1f);
    public Color colorOnda = new Color(1f, 0.45f, 0.55f, 1f);
    public Color colorMeteoro = new Color(1f, 0.45f, 0.2f, 1f);
    [Tooltip("La Quemadura solo se acumula si te queda al menos esta parte de la vida (asi nunca mata sin aviso).")]
    [Range(0f, 1f)] public float quemaduraVidaMinima = 0.4f;

    [Header("Tregua (fin de la fase 1)")]
    [Tooltip("El cartel de jefe derrotado (el mismo que al vencer de verdad).")]
    public string textoTregua = "ENEMIGO CAÍDO";
    public Color colorTregua = new Color(1f, 0.85f, 0.45f, 1f);
    [Min(0f)] public float esperaCartel = 1.5f;
    [Min(0f)] public float duracionCartel = 4f;
    [Tooltip("Frasco de sangre gratis al empezar la tregua (una vez por pelea; nada de mana). Si los tienes llenos, se guarda y llega al beber uno.")]
    public bool frascoGratis = true;
    [Min(0)] public int frascosGratis = 1;
    public string textoFrasco = "Frasco de sangre";
    public string descripcionFrasco = "Una carga más para el frasco de sangre";
    [Tooltip("Brillo de la flor que late.")]
    public Color colorFlor = new Color(1f, 0.4f, 0.5f, 1f);
    [Range(0f, 1f)] public float brilloFlor = 0.35f;
    [Tooltip("Latidos por segundo de la flor, y en los ultimos segundos antes de renacer por tiempo.")]
    [Min(0.1f)] public float latidoFlor = 0.8f, latidoFlorRapido = 2.5f;
    [Min(0f)] public float segundosLatidoRapido = 3f;
    [Tooltip("Renace si estas mas lejos que esto de la flor (unidades)...")]
    [Min(0f)] public float distanciaRenacer = 9f;
    [Tooltip("...durante este tiempo seguido (segundos).")]
    [Min(0f)] public float tiempoLejos = 1f;
    [Tooltip("Si no te alejas, renace igual a los X segundos de caer (0 = solo al alejarte).")]
    [Min(0f)] public float tiempoMaxRenacer = 12f;
    [Tooltip("Velocidad de la muerte al reves al renacer (1 = la del dibujo).")]
    [Min(0.1f)] public float velocidadRenacer = 1f;
    [Tooltip("Pausa tras renacer antes del primer ataque de la fase 2 (segundos).")]
    [Min(0f)] public float pausaInicialFase2 = 1.2f;

    [Header("Ataques")]
    public List<Ataque> ataques = new List<Ataque>();

    public Ataque Buscar(string id) => ataques.Find(a => a != null && a.id == id);
    public Combo BuscarCombo(string id) => combos.Find(c => c != null && c.id == id);

    public float AvisoMinimo(Golpe g, bool final) =>
        Mathf.Max(g.peso == Peso.Pesado ? avisoMinPesado : g.peso == Peso.Contraataque ? avisoMinContraataque : avisoMinLigero, final ? avisoMinFinal : 0f);

    public Color ColorAviso(Golpe g) => g.peso == Peso.Pesado ? colorPesado : g.peso == Peso.Contraataque ? colorContraataque : colorLigero;

    // Parpadeos por segundo de su aviso (0 = fijo, el del contraataque).
    public float Parpadeo(Golpe g) => g.peso == Peso.Pesado ? parpadeoPesado : g.peso == Peso.Contraataque ? 0f : parpadeoLigero;

    public float Afinidad(Elemento e)
    {
        switch (e)
        {
            case Elemento.Fuego: return fuego;
            case Elemento.Hielo: return hielo;
            case Elemento.Sagrado: return sagrado;
            case Elemento.Sangrado: return sangrado;
            case Elemento.Oscuro: return oscuridad;
            default: return 1f;
        }
    }
}
