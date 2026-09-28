using UnityEngine;

// Cofre escondido con almas (y, si se indica, el frasco extra de pociones). Se
// abre con la F, una sola vez por partida guardada.
[RequireComponent(typeof(Collider2D))]
public class CofreAlmas : MonoBehaviour, IInteractuable
{
    [SerializeField] private string clave = "cofre";
    [SerializeField] private int almas = 800;
    [SerializeField] private bool darFrasco = true;
    [SerializeField] private SpriteRenderer visual;
    [SerializeField] private Sprite abierto;
    [SerializeField] private float radio = 1.4f;

    private bool cogido;

    private string Clave => "cofre_" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name + "_" + clave;

    public Vector2 PuntoInteraccion => transform.position;
    public float RadioInteraccion => radio;
    public bool PuedeInteractuar => !cogido;
    public string TextoInteraccion => "Presiona F para abrir";

    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
        cogido = Partida.Bandera(Clave);
        if (cogido && visual != null && abierto != null) visual.sprite = abierto;
        AvisoInteraccion.Crear(transform, this, 1.3f);
    }

    private void OnEnable() => Interacciones.Registrar(this);
    private void OnDisable() => Interacciones.Quitar(this);

    public void Interactuar(PlayerControler p)
    {
        if (cogido) return;
        cogido = true;
        Partida.PonerBandera(Clave);
        if (visual != null && abierto != null) visual.sprite = abierto;
        Sonido.Reproducir("cofre_abrir", 0.8f);
        Sonido.Reproducir("alma_mancha");
        OrbeAlma.Soltar((Vector2)transform.position + Vector2.up * 0.6f, almas);
        TextoFlotante.Mostrar("Un tesoro olvidado", (Vector2)transform.position + Vector2.up * 1.6f, new Color(1f, 0.9f, 0.6f), 1f);
        if (darFrasco && !ReservaPociones.Get().RecompensaCogida) FrascoExtra.Crear((Vector2)transform.position + new Vector2(1.2f, 0f));
    }
}
