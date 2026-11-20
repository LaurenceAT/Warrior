using System.Collections.Generic;
using UnityEngine;

// Donde va cada cosa de los controles tactiles (Resources/AjustesTactiles).
//
// Todo en pixeles de la pantalla de referencia (1920x1080) y medido DESDE la
// esquina que diga cada boton, asi que se escala solo en cualquier movil. El
// joystick va abajo a la izquierda y los botones abajo a la derecha; lo demas
// se mueve desde aqui sin tocar codigo.
[CreateAssetMenu(menuName = "Warrior/Ajustes de los controles tactiles", fileName = "AjustesTactiles")]
public class AjustesTactiles : ScriptableObject
{
    // De que esquina cuelga cada boton.
    public enum Esquina { AbajoIzquierda, AbajoDerecha, ArribaIzquierda, ArribaDerecha, AbajoCentro }

    // Lo que hace cada boton. El que no tenga sitio aqui no sale en pantalla.
    public enum Accion { Atacar, Saltar, Esquivar, Bloquear, FrascoSangre, FrascoMana, Interactuar, Imbuir, Camara, Pausa, Correr }

    [System.Serializable]
    public class Boton
    {
        public Accion accion;
        [Tooltip("Apagado: no sale en pantalla.")]
        public bool activo = true;
        [Tooltip("Desde que esquina se mide la posicion.")]
        public Esquina anclaje = Esquina.AbajoDerecha;
        [Tooltip("Separacion de esa esquina, en pixeles de 1920x1080 (siempre positivo).")]
        public Vector2 posicion = new Vector2(240f, 240f);
        [Tooltip("Diametro del boton. Por debajo de 110 cuesta acertar con el dedo.")]
        [Min(40f)] public float tamano = 150f;
        [Tooltip("Clave del icono en RecursosRPG (vacio: se usa el texto).")]
        public string icono = "";
        [Tooltip("Lo que se escribe si no hay icono.")]
        public string etiqueta = "";
        [Tooltip("Marcado: vale mientras se mantenga el dedo (bloquear, correr). Sin marcar: un toque.")]
        public bool mantener;
    }

    [Header("Como se ve")]
    [Tooltip("Transparencia de los controles sin tocarlos.")]
    [Range(0.05f, 1f)] public float opacidad = 0.22f;
    [Tooltip("Transparencia del boton que estas pulsando.")]
    [Range(0.05f, 1f)] public float opacidadPulsado = 0.65f;
    [Tooltip("Segundos que tarda en encenderse y apagarse un boton.")]
    [Min(0f)] public float suavizado = 0.08f;
    public Color colorBoton = new Color(0.08f, 0.07f, 0.08f, 0.85f);
    public Color colorFilo = new Color(0.72f, 0.58f, 0.36f, 0.9f);
    public Color colorIcono = new Color(0.96f, 0.9f, 0.78f, 1f);
    [Tooltip("Respeta la muesca y las esquinas redondeadas del movil.")]
    public bool zonaSegura = true;

    [Header("Joystick (abajo a la izquierda)")]
    [Tooltip("Donde descansa el joystick, desde la esquina de abajo a la izquierda.")]
    public Vector2 centroJoystick = new Vector2(300f, 270f);
    [Tooltip("Diametro del aro de fuera.")]
    [Min(60f)] public float tamanoBase = 340f;
    [Tooltip("Diametro del pomo que se mueve.")]
    [Min(30f)] public float tamanoPomo = 150f;
    [Tooltip("Lo que hay que mover el dedo antes de que el personaje ande (0.18 = un 18 % del aro).")]
    [Range(0f, 0.6f)] public float zonaMuerta = 0.18f;
    [Tooltip("Marcado: el joystick aparece donde pongas el dedo, no en un sitio fijo. Apagado: solo se coge tocando su circulo, y el resto de la pantalla queda libre para mover la camara.")]
    public bool seguirDedo = false;
    [Tooltip("Solo con 'seguir dedo' apagado: cuanto se agranda su circulo para cogerlo (1.2 = un 20 % mas).")]
    [Range(1f, 2f)] public float margenJoystick = 1.25f;
    [Tooltip("Solo con 'seguir dedo' marcado: zona de la pantalla donde el dedo coge el joystick, en partes de la pantalla (x, y, ancho, alto).")]
    public Rect zonaJoystick = new Rect(0f, 0f, 0.45f, 0.72f);
    [Tooltip("Empujando el joystick mas alla de esto, el personaje corre (1 = hasta el borde; 1.1 = nunca).")]
    [Range(0.5f, 1.1f)] public float empujeParaCorrer = 0.9f;

    [Header("Rendimiento (solo en un movil de verdad)")]
    [Tooltip("Tope de fotogramas. En un movil de 120 Hz, dejarlo suelto solo sirve para calentarlo.")]
    [Range(30, 120)] public int fotogramasObjetivo = 60;
    [Tooltip("Marcado: si aun asi va mal, baja la resolucion a la que se dibuja (una sola vez).")]
    public bool bajarSolo = true;
    [Tooltip("Por debajo de estos fotogramas (dos tandas seguidas de 3 s) se considera que va mal.")]
    [Range(20f, 60f)] public float fotogramasMinimos = 45f;
    [Tooltip("A cuanto se baja la resolucion de dibujado (0.8 = un 80 %; el juego se ve igual de grande, solo menos fino).")]
    [Range(0.5f, 1f)] public float escalaBaja = 0.8f;

    [Header("Boton de volver (en los menus)")]
    [Tooltip("Donde va el boton de volver, desde abajo a la izquierda.")]
    public Vector2 posicionVolver = new Vector2(120f, 120f);
    [Min(40f)] public float tamanoVolver = 110f;

    [Header("Camara con el dedo")]
    [Tooltip("Arrastrando por donde no hay ningun circulo se mueve la camara, como en muchos juegos de movil.")]
    public bool camaraConDedo = true;
    [Tooltip("Unidades del mundo que se desplaza la camara al arrastrar media pantalla.")]
    [Min(0f)] public float alcanceCamara = 5f;
    [Tooltip("Pixeles que hay que arrastrar antes de que la camara se mueva (un toque suelto no la mueve).")]
    [Min(0f)] public float arrastreMinimo = 18f;
    [Tooltip("Marcado: tambien se mueve arriba y abajo.")]
    public bool camaraVertical = true;

    [Header("Botones")]
    public List<Boton> botones = new List<Boton>();

    // La disposicion de salida. La usa el generador y tambien vale de respaldo
    // si todavia no existe la ficha, asi nunca se queda la pantalla sin botones.
    public static AjustesTactiles PorDefecto()
    {
        AjustesTactiles a = CreateInstance<AjustesTactiles>();
        a.botones = new List<Boton>
        {
            // El grande, donde cae el pulgar derecho: ataca y, si hay algo cerca
            // (hoguera, cofre, estatua), lo usa.
            B(Accion.Atacar,       Esquina.AbajoDerecha, 280f, 200f, 280f, "ctrl_espada"),
            B(Accion.Saltar,       Esquina.AbajoDerecha, 445f, 375f, 110f, "ctrl_saltar"),
            B(Accion.Esquivar,     Esquina.AbajoDerecha, 320f, 460f, 120f, "ctrl_esquiva"),
            B(Accion.Bloquear,     Esquina.AbajoDerecha, 110f, 450f, 170f, "ctrl_bloqueo", true),

            // Los frascos, en el borde de la derecha.
            B(Accion.FrascoMana,   Esquina.AbajoDerecha, 75f, 200f, 110f, "objeto_frasco_mana"),
            B(Accion.FrascoSangre, Esquina.AbajoDerecha, 75f, 80f, 110f, "objeto_frasco_sangre"),

            // Lado izquierdo: la imbuicion al lado del joystick y el fijar debajo.
            B(Accion.Imbuir,       Esquina.AbajoIzquierda, 455f, 480f, 115f, "ctrl_rueda"),
            Etiqueta(B(Accion.Camara, Esquina.AbajoIzquierda, 520f, 185f, 78f, ""), "FIJAR"),

            // El menu, arriba del todo a la izquierda: lejos de los golpes.
            B(Accion.Pausa,        Esquina.ArribaIzquierda, 95f, 95f, 100f, "ctrl_pausa"),

            // Ya no tiene boton propio: lo hace el de atacar cuando hay algo a tiro.
            Apagado(B(Accion.Interactuar, Esquina.AbajoDerecha, 640f, 285f, 140f, "ctrl_interactuar")),

            // Con joystick no hace falta: se corre empujandolo al fondo. Se deja
            // apagado por si alguien lo prefiere como boton.
            Apagado(B(Accion.Correr, Esquina.AbajoIzquierda, 640f, 230f, 130f, "ctrl_correr", true)),
        };
        return a;
    }

    private static Boton B(Accion accion, Esquina anclaje, float x, float y, float tamano, string icono, bool mantener = false)
        => new Boton { accion = accion, anclaje = anclaje, posicion = new Vector2(x, y), tamano = tamano, icono = icono, mantener = mantener };

    private static Boton Etiqueta(Boton b, string texto) { b.etiqueta = texto; return b; }
    private static Boton Apagado(Boton b) { b.activo = false; return b; }

    // La ficha de Resources; si no esta, la de salida (asi se puede probar antes
    // de pasar el generador).
    private static AjustesTactiles cache;
    public static AjustesTactiles Cargar()
    {
        if (cache != null) return cache;
        cache = Resources.Load<AjustesTactiles>("AjustesTactiles");
        if (cache == null) cache = PorDefecto();
        return cache;
    }

    // Para que el generador y las pruebas puedan volver a leerla.
    public static void Olvidar() => cache = null;
}
