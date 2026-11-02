using System.Linq;
using Unity.Cinemachine;
using UnityEngine;

// Partidas guardadas con otra version de la nieve (la Ronda 19 u otra anterior):
// si la partida reaparece en una hoguera que ya no existe, se la lleva a la
// hoguera nueva que le corresponde (por orden: las primeras viejas a la primera
// nueva...); si la posicion no es de ninguna hoguera conocida, a la hoguera 1.
// La mancha de almas que quede dentro de la roca o sin suelo debajo se trae
// junto a esa hoguera, para no perder las almas.
// No toca la Partida ni el GameManager: corrige la posicion guardada antes de que
// las hogueras la miren (Awake) y mueve al player despues de que lo coloquen (Start).
[DefaultExecutionOrder(-200)]
public class ReaparicionNieve : MonoBehaviour
{
    [Tooltip("Puntos de reaparicion de las hogueras de la version anterior, de izquierda a derecha (los pone el generador).")]
    public Vector2[] hoguerasViejas = new Vector2[0];
    [Tooltip("Distancia para dar por buena una posicion guardada (como hace la Hoguera).")]
    public float tolerancia = 0.5f;

    private Vector2? corregida;

    private void Awake()
    {
        Partida.Datos d = Partida.Actual;
        if (d == null || !d.enHoguera || d.escena != gameObject.scene.name) return;
        Hoguera[] nuevas = FindObjectsByType<Hoguera>(FindObjectsSortMode.None).OrderBy(h => h.transform.position.x).ToArray();
        if (nuevas.Length == 0) return;
        Vector2 guardada = new Vector2(d.x, d.y);
        if (nuevas.Any(h => Vector2.Distance(h.PuntoReaparicion, guardada) < tolerancia)) return;

        Hoguera destino = nuevas[0];
        for (int i = 0; i < hoguerasViejas.Length; i++)
            if (Vector2.Distance(hoguerasViejas[i], guardada) < tolerancia)
            {
                destino = nuevas[Mathf.Min(nuevas.Length - 1, i * nuevas.Length / hoguerasViejas.Length)];
                break;
            }
        corregida = destino.PuntoReaparicion;
        d.x = corregida.Value.x;
        d.y = corregida.Value.y;
        d.lugar = destino.NombreLugar;
        Debug.Log($"[ReaparicionNieve] La partida reaparecia en ({guardada.x:0.#}, {guardada.y:0.#}), que ya no es una hoguera: pasa a {destino.name}.");
    }

    // Despues del GameManager (que coloca al player en la posicion vieja) y de la mancha.
    private void Start() => Invoke(nameof(Colocar), 0f);

    private void Colocar()
    {
        Vector2 aparicion = corregida ?? Vector2.zero;
        if (corregida.HasValue)
        {
            PlayerControler p = FindFirstObjectByType<PlayerControler>();
            if (p != null)
            {
                p.transform.position = aparicion;
                Rigidbody2D rb = p.GetComponent<Rigidbody2D>();
                if (rb != null) { rb.position = aparicion; rb.linearVelocity = Vector2.zero; }
            }
            if (GameManager.Instance != null)
            {
                GameManager.Instance.hasCheckPointActive = true;
                GameManager.Instance.checkpointRespawnPosition = aparicion;
            }
            CinemachineCamera cam = FindFirstObjectByType<CinemachineCamera>();
            if (cam != null) cam.ForceCameraPosition(new Vector3(aparicion.x, aparicion.y, cam.transform.position.z), cam.transform.rotation);
        }

        // Mancha de almas dentro de la roca o sobre el vacio: junto a la hoguera de reaparicion.
        ManchaAlmas mancha = FindFirstObjectByType<ManchaAlmas>();
        if (mancha == null) return;
        Vector2 m = mancha.transform.position;
        int suelo = LayerMask.GetMask("Ground");
        bool enRoca = Physics2D.OverlapPoint(m, suelo) != null;
        bool sinSuelo = !Physics2D.Raycast(m, Vector2.down, 40f, suelo);
        if (!enRoca && !sinSuelo) return;
        Vector2 cerca = corregida.HasValue ? aparicion
                      : GameManager.Instance != null && GameManager.Instance.hasCheckPointActive ? (Vector2)GameManager.Instance.checkpointRespawnPosition
                      : (Vector2)FindObjectsByType<Hoguera>(FindObjectsSortMode.None).OrderBy(h => h.transform.position.x).First().PuntoReaparicion;
        mancha.transform.position = cerca + new Vector2(1.5f, 0f);
        Debug.Log("[ReaparicionNieve] La mancha de almas estaba fuera del nivel nuevo: se trae junto a la hoguera.");
    }
}
