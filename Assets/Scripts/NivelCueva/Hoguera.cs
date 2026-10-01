using System;
using System.Collections;
using UnityEngine;

// Hoguera al estilo Souls (la torre de la luna de sangre). Al acercarse aparece
// "Presiona F"; al pulsarla se enciende (la primera vez, con el aviso "Hoguera
// activada"), queda como punto de reaparicion y se abre su menu:
//   1. Descansar: cura del todo, rellena los frascos, reaparecen los enemigos,
//      queda como punto de reaparicion y se guarda la partida.
//   2. Subir de nivel y estadisticas.
//   3. Aplicar mejoras (lo recogido en cofres y jefes).
// Quien quiera enterarse de un descanso escucha AlDescansar.
public class Hoguera : MonoBehaviour, IInteractuable
{
    // Se lanza cada vez que se descansa en cualquier hoguera.
    public static event Action AlDescansar;
    // La ultima en la que se descanso (la partida guarda su nombre y su sitio).
    public static Hoguera UltimaUsada { get; private set; }

    [Header("Uso")]
    [Tooltip("Nombre del lugar (sale en la lista de partidas guardadas y en el menu).")]
    [SerializeField] private string nombreLugar = "";
    [SerializeField] private float radio = 1.8f;
    // Donde reaparece el player, respecto a la torre (a su lado, un poco alto).
    [SerializeField] private Vector2 puntoReaparicion = new Vector2(1.2f, 0.6f);
    [SerializeField] private string textoInteractuar = "Presiona F para usar la hoguera";
    [SerializeField] private string textoEncender = "Presiona F para encender la hoguera";
    [SerializeField] private string textoDescanso = "Has descansado";
    [Tooltip("Aviso la primera vez que se enciende (sale en el menu de la hoguera).")]
    [SerializeField] private string textoActivada = "Hoguera activada. Si caes, volverás aquí.";

    [Header("Efecto")]
    [SerializeField] private SpriteRenderer torre;
    [SerializeField] private Color colorDestello = new Color(1f, 0.2f, 0.15f, 0.35f);
    [SerializeField] private float tiempoDestello = 0.35f;
    // Tinte de la torre al activarse, que luego se relaja hasta el de encendida.
    [SerializeField] private Color colorActivacion = new Color(1f, 0.35f, 0.3f, 1f);
    [SerializeField] private Color colorEncendida = new Color(1f, 0.82f, 0.82f, 1f);
    [SerializeField] private Color colorApagada = new Color(0.65f, 0.6f, 0.65f, 1f);
    [SerializeField] private float alturaAviso = 1.9f;

    public string NombreLugar => string.IsNullOrEmpty(nombreLugar) ? "Hoguera" : nombreLugar;
    public Vector2 PuntoReaparicion => (Vector2)transform.position + puntoReaparicion;
    public bool Encendida => encendida;

    // IInteractuable
    public Vector2 PuntoInteraccion => transform.position;
    public float RadioInteraccion => radio;
    public bool PuedeInteractuar => !MenuHoguera.Abierto;
    public string TextoInteraccion => encendida ? textoInteractuar : textoEncender;

    private AvisoInteraccion aviso;
    private bool encendida;
    private Vector3 escalaTorre;
    private Coroutine efecto;

    private void OnEnable() => Interacciones.Registrar(this);
    private void OnDisable() => Interacciones.Quitar(this);

    private void Start()
    {
        if (torre == null) torre = GetComponentInChildren<SpriteRenderer>();
        if (torre != null)
        {
            escalaTorre = torre.transform.localScale;
            torre.color = colorApagada;
        }
        // La hoguera de la partida cargada ya estaba encendida.
        Partida.Datos d = Partida.Actual;
        if (d != null && d.enHoguera && d.escena == gameObject.scene.name && Vector2.Distance(PuntoReaparicion, new Vector2(d.x, d.y)) < 0.5f)
        {
            encendida = true;
            UltimaUsada = this;
            if (torre != null) torre.color = colorEncendida;
        }
        aviso = AvisoInteraccion.Crear(transform, this, alturaAviso);
    }

    // F con la hoguera a tiro: se enciende si hacia falta y se abre el menu.
    public void Interactuar(PlayerControler p) => Usar(p);

    public void Usar(PlayerControler p)
    {
        bool primera = !encendida;
        if (!encendida)
        {
            encendida = true;
            Sonido.Reproducir("hoguera_encender");
            if (efecto != null) StopCoroutine(efecto);
            efecto = StartCoroutine(Efecto(false));
        }
        // Al usarla ya es tu punto de reaparicion (sin descansar).
        FijarReaparicion();
        MenuHoguera.Abrir(this, p, primera ? textoActivada : null);
    }

    // Reapareces aqui. En la partida se apunta en memoria: se escribe con el
    // siguiente guardado de siempre (al descansar, al reaparecer, cada minuto...).
    private void FijarReaparicion()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.hasCheckPointActive = true;
            GameManager.Instance.checkpointRespawnPosition = transform.position + (Vector3)puntoReaparicion;
        }
        UltimaUsada = this;
        Partida.Datos d = Partida.Actual;
        if (d == null) return;
        d.enHoguera = true;
        d.x = PuntoReaparicion.x;
        d.y = PuntoReaparicion.y;
        d.lugar = NombreLugar;
    }

    // "Descansar" en el menu: cura, rellena, reaparecen los enemigos y se guarda.
    public void Descansar(PlayerControler p)
    {
        if (p != null) p.CurarCompleto();
        if (GameManager.Instance != null)
        {
            GameManager.Instance.hasCheckPointActive = true;
            GameManager.Instance.checkpointRespawnPosition = transform.position + (Vector3)puntoReaparicion;
        }
        UltimaUsada = this;
        encendida = true;
        AlDescansar?.Invoke();
        Sonido.Reproducir("hoguera_descansar");
        if (efecto != null) StopCoroutine(efecto);
        efecto = StartCoroutine(Efecto(true));
    }

    // Va con el tiempo real: el juego esta parado con el menu abierto.
    private IEnumerator Efecto(bool descanso)
    {
        ScreenFlash.Destello(colorDestello, tiempoDestello);

        // Brasas que suben desde la punta de la torre.
        Vector2 punta = torre != null ? (Vector2)torre.bounds.center + Vector2.up * torre.bounds.extents.y * 0.6f
                                      : (Vector2)transform.position + Vector2.up;
        ParticulasFx.Rafaga(punta, 28, new Color(1f, 0.35f, 0.2f), new Color(1f, 0.85f, 0.4f),
                            new Vector2(1.5f, 4f), -0.25f, new Vector2(0.05f, 0.12f), new Vector2(0.6f, 1.3f));

        if (descanso && aviso != null) aviso.Forzar(textoDescanso, 1.8f);

        // Pulso de la torre: crece un poco y se tine de rojo, y vuelve.
        const float dur = 0.9f;
        for (float t = 0f; t < dur; t += Time.unscaledDeltaTime)
        {
            float u = t / dur;
            if (torre != null)
            {
                torre.color = Color.Lerp(colorActivacion, colorEncendida, u * u);
                torre.transform.localScale = escalaTorre * (1f + 0.12f * Mathf.Sin(u * Mathf.PI));
            }
            yield return null;
        }
        if (torre != null)
        {
            torre.color = colorEncendida;
            torre.transform.localScale = escalaTorre;
        }
        efecto = null;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, radio);
        Gizmos.DrawWireSphere(transform.position + (Vector3)puntoReaparicion, 0.15f);
    }
}
