using UnityEngine;

// Antorcha de pared de la cueva. Apagada al principio: con la F se enciende,
// se anima y alumbra un trozo del camino en las zonas oscuras (FuenteLuz). Se
// queda encendida en la partida (se guarda con una bandera).
[RequireComponent(typeof(FuenteLuz))]
public class AntorchaCueva : MonoBehaviour, IInteractuable
{
    [Tooltip("Clave unica de esta antorcha (para recordar que se encendio).")]
    public string clave = "antorcha";
    public SpriteRenderer sprite;
    public Sprite apagada;
    public Sprite[] encendida;
    public float fps = 10f;
    public float radio = 1.3f;
    [Tooltip("Sonido al encenderla (Resources/RecursosRPG).")]
    public string sonido = "antorcha_encender";

    private FuenteLuz luz;
    private bool ardiendo;
    private float t;

    public Vector2 PuntoInteraccion => transform.position;
    public float RadioInteraccion => radio;
    public bool PuedeInteractuar => !ardiendo;
    public string TextoInteraccion => "Presiona F para encender";

    private string Bandera => "antorcha_" + gameObject.scene.name + "_" + clave;

    private void Awake()
    {
        luz = GetComponent<FuenteLuz>();
        Poner(Partida.Bandera(Bandera));
    }

    private void OnEnable() => Interacciones.Registrar(this);
    private void OnDisable() => Interacciones.Quitar(this);

    public void Interactuar(PlayerControler p)
    {
        if (ardiendo) return;
        Poner(true);
        Partida.PonerBandera(Bandera);
        Sonido.Reproducir(sonido, 0.7f);
        ParticulasFx.Rafaga((Vector2)transform.position + Vector2.up * 0.3f, 10, new Color(1f, 0.75f, 0.3f), new Color(1f, 0.3f, 0.05f),
                            new Vector2(0.8f, 2f), -0.5f, new Vector2(0.04f, 0.08f), new Vector2(0.3f, 0.6f), 70f, 90f);
    }

    private void Poner(bool on)
    {
        ardiendo = on;
        if (luz != null) luz.encendida = on;
        if (sprite != null && !on) sprite.sprite = apagada;
    }

    private void Update()
    {
        if (!ardiendo || sprite == null || encendida == null || encendida.Length == 0) return;
        t += Time.deltaTime;
        sprite.sprite = encendida[(int)(t * fps) % encendida.Length];
    }
}
