using System;
using System.Collections;
using TMPro;
using UnityEngine;

// Hoguera al estilo Souls (la torre de la luna de sangre). Al acercarse aparece
// "Presiona E"; al pulsarla cura del todo, guarda el punto de reaparicion, y
// avisa a quien escuche AlDescansar (recarga de pociones, reaparicion de los
// enemigos comunes).
//
// La E es tambien la tecla de la espada: el PlayerControler mira Hoguera.Cercana
// y, si hay una a tiro, la E va a la hoguera en vez de enfundar.
public class Hoguera : MonoBehaviour
{
    // La hoguera que el player tiene a tiro ahora mismo (o null).
    public static Hoguera Cercana { get; private set; }
    // Se lanza cada vez que se descansa en cualquier hoguera.
    public static event Action AlDescansar;
    // La ultima en la que se descanso (la partida guarda su nombre y su sitio).
    public static Hoguera UltimaUsada { get; private set; }

    [Header("Uso")]
    [Tooltip("Nombre del lugar (sale en la lista de partidas guardadas).")]
    [SerializeField] private string nombreLugar = "";
    [SerializeField] private float radio = 1.8f;
    // Donde reaparece el player, respecto a la torre (a su lado, un poco alto).
    [SerializeField] private Vector2 puntoReaparicion = new Vector2(1.2f, 0.6f);
    [SerializeField] private string textoAviso = "Presiona E para descansar";
    [SerializeField] private string textoDescanso = "Has descansado";
    // Al descansar se abre el menu de la hoguera (subir de nivel).
    [SerializeField] private bool abrirMenu = true;

    [Header("Efecto (no hay sonido: todo es visual)")]
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

    private TextMeshPro aviso;
    private float alfaAviso;
    private bool encendida;
    private Transform player;
    private Vector3 escalaTorre;
    private Coroutine efecto;

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
        CrearAviso();
    }

    private void OnDisable()
    {
        if (Cercana == this) Cercana = null;
    }

    private void Update()
    {
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }

        bool cerca = player != null && Vector2.Distance(player.position, transform.position) <= radio;
        if (cerca) Cercana = this;
        else if (Cercana == this) Cercana = null;

        // El aviso aparece y se va con un fundido.
        alfaAviso = Mathf.MoveTowards(alfaAviso, cerca ? 1f : 0f, Time.deltaTime * 5f);
        if (aviso != null)
        {
            Color c = aviso.color;
            c.a = alfaAviso;
            aviso.color = c;
        }
    }

    // Lo llama el PlayerControler al pulsar E con esta hoguera a tiro.
    public void Usar(PlayerControler p)
    {
        p.CurarCompleto();

        GameManager.Instance.hasCheckPointActive = true;
        GameManager.Instance.checkpointRespawnPosition = transform.position + (Vector3)puntoReaparicion;

        UltimaUsada = this;
        bool primeraVez = !encendida;
        encendida = true;
        AlDescansar?.Invoke();
        Sonido.Reproducir(primeraVez ? "hoguera_encender" : "hoguera_descansar");
        if (abrirMenu) MenuHoguera.Abrir();

        if (efecto != null) StopCoroutine(efecto);
        efecto = StartCoroutine(Efecto());
    }

    private IEnumerator Efecto()
    {
        ScreenFlash.Destello(colorDestello, tiempoDestello);

        // Brasas que suben desde la punta de la torre.
        Vector2 punta = torre != null ? (Vector2)torre.bounds.center + Vector2.up * torre.bounds.extents.y * 0.6f
                                      : (Vector2)transform.position + Vector2.up;
        ParticulasFx.Rafaga(punta, 28, new Color(1f, 0.35f, 0.2f), new Color(1f, 0.85f, 0.4f),
                            new Vector2(1.5f, 4f), -0.25f, new Vector2(0.05f, 0.12f), new Vector2(0.6f, 1.3f));

        if (aviso != null) aviso.text = textoDescanso;

        // Pulso de la torre: crece un poco y se tine de rojo, y vuelve.
        float t = 0f;
        const float dur = 0.9f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float u = t / dur;
            if (torre != null)
            {
                torre.color = Color.Lerp(colorActivacion, colorEncendida, u * u);
                float s = 1f + 0.12f * Mathf.Sin(u * Mathf.PI);
                torre.transform.localScale = escalaTorre * s;
            }
            yield return null;
        }
        if (torre != null)
        {
            torre.color = colorEncendida;
            torre.transform.localScale = escalaTorre;
        }

        yield return new WaitForSeconds(0.8f);
        if (aviso != null) aviso.text = textoAviso;
        efecto = null;
    }

    private void CrearAviso()
    {
        GameObject go = new GameObject("Aviso");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, alturaAviso, 0f);

        aviso = go.AddComponent<TextMeshPro>();
        aviso.text = textoAviso;
        aviso.fontSize = 2.2f;
        aviso.alignment = TextAlignmentOptions.Center;
        aviso.enableWordWrapping = false;
        aviso.color = new Color(1f, 0.9f, 0.85f, 0f);
        aviso.outlineWidth = 0.2f;
        aviso.outlineColor = new Color(0f, 0f, 0f, 1f);

        MeshRenderer mr = go.GetComponent<MeshRenderer>();
        mr.sortingLayerName = "VFX";
        mr.sortingOrder = 30;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, radio);
        Gizmos.DrawWireSphere(transform.position + (Vector3)puntoReaparicion, 0.15f);
    }
}
