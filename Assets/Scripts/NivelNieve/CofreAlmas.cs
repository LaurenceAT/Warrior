using UnityEngine;

// Cofre escondido con almas (y, si se indica, el frasco extra de pociones). Se
// abre al tocarlo, una sola vez por partida guardada.
[RequireComponent(typeof(Collider2D))]
public class CofreAlmas : MonoBehaviour
{
    [SerializeField] private string clave = "cofre";
    [SerializeField] private int almas = 800;
    [SerializeField] private bool darFrasco = true;
    [SerializeField] private SpriteRenderer visual;
    [SerializeField] private Sprite abierto;

    private bool cogido;

    private string Clave => "cofre_" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name + "_" + clave;

    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
        cogido = Partida.Bandera(Clave);
        if (cogido && visual != null && abierto != null) visual.sprite = abierto;
    }

    private void OnTriggerEnter2D(Collider2D otro)
    {
        if (cogido || !otro.CompareTag("Player")) return;
        cogido = true;
        Partida.PonerBandera(Clave);
        if (visual != null && abierto != null) visual.sprite = abierto;
        Sonido.Reproducir("menu_abrir", 0.8f);
        Sonido.Reproducir("alma_mancha");
        OrbeAlma.Soltar((Vector2)transform.position + Vector2.up * 0.6f, almas);
        TextoFlotante.Mostrar("Un tesoro olvidado", (Vector2)transform.position + Vector2.up * 1.6f, new Color(1f, 0.9f, 0.6f), 1f);
        if (darFrasco && !ReservaPociones.Get().RecompensaCogida) FrascoExtra.Crear((Vector2)transform.position + new Vector2(1.2f, 0f));
    }
}
