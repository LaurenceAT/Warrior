using System.Collections.Generic;
using UnityEngine;

// Ficha "Extras Nieve" (Assets/Data/Niveles/Extras Nieve.asset): lo que el
// generador de la nieve (GenerarNieveDiagrama) necesita y NO esta en el dibujo.
// Todas las posiciones van en coordenadas del dibujo (las de drawio: x a la
// derecha, y hacia ABAJO), asi que si mueves algo en el dibujo, lo de aqui se
// mueve con el mismo criterio. Las "id" son las de las formas del dibujo
// (en drawio: clic derecho > Editar datos, arriba sale el ID).
[CreateAssetMenu(menuName = "Warrior/Extras Nieve", fileName = "Extras Nieve")]
public class ExtrasNieve : ScriptableObject
{
    [System.Serializable]
    public class Recuadro
    {
        public string nombre;
        [Tooltip("Rectangulo en coordenadas del dibujo: x, y (esquina de arriba a la izquierda), ancho, alto.")]
        public UnityEngine.Rect dibujo;
        [TextArea] public string motivo;
    }

    [System.Serializable]
    public class Hoguera
    {
        [Tooltip("Id del trapecio en el dibujo.")]
        public string id;
        public string nombreLugar;
    }

    [System.Serializable]
    public class Salto
    {
        public string nombre;
        [Tooltip("Borde desde el que se salta (coordenadas del dibujo, sobre la superficie).")]
        public Vector2 desde;
        [Tooltip("Borde al que se llega (coordenadas del dibujo, sobre la superficie).")]
        public Vector2 hasta;
        [Tooltip("Camino principal (si no, secundario).")]
        public bool principal = true;
        [Tooltip("Solo se hace con una mecanica (viento, puente de hielo...): no se calcula.")]
        public string soloCon = "";
    }

    [Header("Dibujo")]
    [Tooltip("Ruta del dibujo, desde la carpeta del proyecto.")]
    public string rutaDiagrama = "Assets/SPRITES PARA NUEVOS NIVELES/nivel nieve.drawio";

    [Header("Escala (decision D1)")]
    [Tooltip("Unidades del dibujo que ocupa una casilla a lo ancho.")]
    public float dibujoPorCasillaX = 30f;
    [Tooltip("Unidades del dibujo que ocupa una casilla a lo alto.")]
    public float dibujoPorCasillaY = 50f;
    [Tooltip("Punto del dibujo que cae en 'anclaMundo' (el borde izquierdo de la zona del jefe).")]
    public Vector2 anclaDibujo = new Vector2(9220f, -80f);
    [Tooltip("Donde cae ese punto en el mundo: la entrada de la arena de la Sombra (no se mueve).")]
    public Vector2 anclaMundo = new Vector2(362f, 3f);

    [Header("Lectura del dibujo")]
    [Tooltip("Rectangulos sin color que son HUECOS (salas de aire), no roca.")]
    public List<string> huecos = new List<string>();
    [Tooltip("Bloques de roca que bajan hasta el fondo aunque la regla diga que no.")]
    public List<string> bajanAlFondo = new List<string>();
    [Tooltip("Bloques de roca que NO bajan hasta el fondo aunque la regla diga que si.")]
    public List<string> noBajanAlFondo = new List<string>();

    [Header("Ajustes de escala (tabla D1) y bordes del nivel")]
    [Tooltip("Roca que se anade al dibujo (repisas, alargues, la pared del principio).")]
    public List<Recuadro> rocasExtra = new List<Recuadro>();
    [Tooltip("Salas cerradas: se les pone la pared de fondo (tapa el cielo).")]
    public List<Recuadro> interiores = new List<Recuadro>();
    [Tooltip("Por debajo de esta y del dibujo, todo el aire lleva pared de cueva detras: ahi el fondo (parallax) ya no llega " +
             "y se veria su borde. 320 = altura -5 del mundo (8 casillas bajo el suelo de la arena).")]
    public float paredFondoBajoY = 320f;

    [Header("Inicio y hogueras")]
    [Tooltip("Donde aparece el player al entrar al nivel (x del dibujo; la y se busca en el suelo).")]
    public float inicioX = -340f;
    public List<Hoguera> hogueras = new List<Hoguera>();

    [Header("Comprobacion de saltos")]
    public List<Salto> saltos = new List<Salto>();

    [Header("Mecanicas que no estan en el dibujo")]
    [Tooltip("Salas que quedan a oscuras desde fuera (tras las paredes falsas) hasta que se entra.")]
    public List<Recuadro> zonasOcultas = new List<Recuadro>();
    [Tooltip("Refugios de la ventisca: x del dibujo de cada roca (el remanso queda a su izquierda, a sotavento).")]
    public List<float> refugios = new List<float>();
    [Tooltip("Columnas de corriente de viento (rectangulos del dibujo).")]
    public List<Recuadro> corrientes = new List<Recuadro>();
    [Tooltip("Alto de la ventisca de cada nube, en y del dibujo: x = borde de arriba, y = borde de abajo.")]
    public Vector2 alturaVentisca = new Vector2(-640f, 700f);

    [System.Serializable]
    public class Cofre
    {
        [Tooltip("Id del cilindro en el dibujo.")]
        public string id;
        [Tooltip("Desmarcado = CofreAlmas; marcado = CofreMejora.")]
        public bool mejora;
        public int almas = 300;
        public Equipo.Objeto objeto = Equipo.Objeto.LagrimaCarmesi;
        public Equipo.Objeto[] extra = new Equipo.Objeto[0];
    }

    [System.Serializable]
    public class Enemigo
    {
        [Tooltip("Id de la figura en el dibujo.")]
        public string id;
        [Tooltip("Prefab (Assets/Prefabs/Enemies/Nieve o Elites), sin .prefab.")]
        public string prefab;
        [Tooltip("Escondido en un montículo de nieve (MonticuloEmboscada).")]
        public bool emboscada;
        public bool flanquear, retirarse;
        [Tooltip("Avisa a los cercanos (0 = no).")]
        public int avisar;
        [TextArea] public string porque;
    }

    [System.Serializable]
    public class Sello
    {
        [Tooltip("Su clave guardada es \"dx_\" + nombre.")]
        public string nombre;
        [Tooltip("Sello de la nieve Ronda 19 que se copia: claro, cripta, muro o atalaya.")]
        public string plantilla;
        public Elemento elemento = Elemento.Sagrado;
        public bool ensenanza;
        [Tooltip("Posicion en el dibujo (x) y una y cerca del suelo en el que va; ocupa del suelo al techo.")]
        public Vector2 pos;
        [Tooltip("Runa grabada que lo anuncia (posicion en el dibujo, sobre el suelo).")]
        public Vector2 runa;
    }

    [System.Serializable]
    public class Estatua
    {
        [Tooltip("Id de la estatua (el de su ficha en Resources/PistasEstatuas).")]
        public string id;
        [Tooltip("Posicion en el dibujo (x) y una y cerca del suelo en el que va.")]
        public Vector2 pos;
    }

    [Header("Contenido (paso 6)")]
    public List<Cofre> cofres = new List<Cofre>();
    public List<Enemigo> enemigos = new List<Enemigo>();
    public List<Sello> sellos = new List<Sello>();
    [Tooltip("Runa y brillo del lago (el agua es su sello): posicion en el dibujo.")]
    public Vector2 runaLago = new Vector2(5300f, 210f);
    public List<Estatua> estatuas = new List<Estatua>();

    [System.Serializable]
    public class Pieza
    {
        [Tooltip("Nombre de la pieza de decoracion de la nieve Ronda 19 (Pino, PinoFondo, Roca, Mata, Tienda, arbol seco...).")]
        public string nombre;
        public int cantidad = 1;
    }

    [System.Serializable]
    public class ZonaDeco
    {
        public string nombre;
        [Tooltip("Rectangulo del dibujo: solo se decoran los suelos cuya superficie cae dentro.")]
        public UnityEngine.Rect dibujo;
        public List<Pieza> piezas = new List<Pieza>();
    }

    [Header("Decoracion (paso 7)")]
    [Tooltip("Cada zona con su mezcla de piezas. El generador las coloca solo donde no tapan nada: lejos de bordes de salto, " +
             "enemigos, cofres, hogueras, estatuas, sellos, refugios, la pared que se rompe y las paredes falsas.")]
    public List<ZonaDeco> decoracion = new List<ZonaDeco>();
    [Tooltip("Semilla del reparto (cambiala para probar otro reparto; el mismo numero da siempre lo mismo).")]
    public int semillaDeco = 1919;

    [System.Serializable]
    public class Minijefe
    {
        public string nombre = "Martial Hero";
        [Tooltip("Prefab del jefe (con JefeBase).")]
        public GameObject prefab;
        [Tooltip("Bandera de vencido: minijefe_<escena>_<clave>.")]
        public string clave = "martial";
        [Tooltip("Arena (rectangulo del dibujo). El muro de niebla se forma en su borde izquierdo.")]
        public UnityEngine.Rect arena;
        [Tooltip("Usa la musica de jefe de Config Nivel (fase 1).")]
        public bool musicaDelNivel = true;
    }

    [Header("Minijefes (paso 9)")]
    public List<Minijefe> minijefes = new List<Minijefe>();
}
