using System.Collections;
using UnityEngine;

// Portal de salida del nivel (sustituye a la puerta). Al tocarlo el player:
//   1. deja de responder al mando y camina (con su animacion normal) hasta el
//      centro del portal;
//   2. el portal destella y suena, y el personaje se desvanece dentro;
//   3. la pantalla se funde a negro y se carga el nivel siguiente con barra de
//      progreso real (PantallaCarga).
// Todo lo que se puede cambiar esta en el Inspector.
public class PortalNivel : MonoBehaviour
{
    [Header("Destino")]
    [Tooltip("Nombre de la escena a cargar (como en Build Settings). Vacio = la siguiente de la lista.")]
    [SerializeField] private string escenaDestino = "";

    [Header("Entrada del player")]
    [Tooltip("Velocidad a la que camina hasta el centro del portal.")]
    [SerializeField] private float velocidadCaminar = 2.6f;
    [Tooltip("Lo que tarda en desvanecerse dentro del portal.")]
    [SerializeField] private float duracionDesvanecer = 0.45f;
    [Tooltip("Fundido a negro (segundos).")]
    [SerializeField] private float fundidoNegro = 0.6f;

    [Header("Efectos")]
    [Tooltip("Animacion del portal (el SpriteRenderer que la muestra).")]
    [SerializeField] private AnimadorHoja animacionPortal;
    [Tooltip("Destello al entrar.")]
    [SerializeField] private AnimadorHoja.Clip efectoEntrar;
    [SerializeField] private Color colorEfecto = new Color(0.75f, 0.6f, 1f, 1f);
    [SerializeField] private float escalaEfecto = 1.6f;
    [Tooltip("Clave del sonido en Resources/RecursosRPG (se cambia el audio alli).")]
    [SerializeField] private string sonidoEntrar = "portal_entrar";
    [Tooltip("Zumbido del portal cuando el player esta cerca.")]
    [SerializeField] private string sonidoCerca = "portal_zumbido";

    private bool usado;
    private bool zumbido;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (usado || !other.CompareTag("Player")) return;
        PlayerControler player = other.GetComponent<PlayerControler>();
        if (player == null) return;
        usado = true;
        StartCoroutine(Entrar(player));
    }

    private void OnEnable()
    {
        // Aparece (tras vencer al jefe): destello y zumbido.
        if (!zumbido && Time.timeSinceLevelLoad > 1f)
        {
            zumbido = true;
            Sonido.Reproducir(sonidoCerca, 0.8f);
            Destello(1.2f);
        }
    }

    private IEnumerator Entrar(PlayerControler player)
    {
        player.ControlExterno(true);
        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        Transform t = player.transform;

        // 1. Caminar hasta el centro (sin teletransporte).
        float x = transform.position.x;
        int lado = x >= t.position.x ? 1 : -1;
        player.MirarHacia(lado);
        float limite = Time.time + 4f;
        while (Mathf.Abs(x - t.position.x) > 0.08f && Time.time < limite && player != null)
        {
            float dx = x - t.position.x;
            rb.linearVelocity = new Vector2(Mathf.Sign(dx) * Mathf.Min(velocidadCaminar, Mathf.Abs(dx) / Time.fixedDeltaTime), rb.linearVelocityY);
            yield return new WaitForFixedUpdate();
        }
        if (player == null) yield break;
        rb.linearVelocity = new Vector2(0f, rb.linearVelocityY);

        // 2. Destello, sonido y desvanecerse dentro.
        Sonido.Reproducir(sonidoEntrar);
        Destello(escalaEfecto);
        CamaraDinamica.Sacudir(0.15f);
        SpriteRenderer sr = player.GetComponent<SpriteRenderer>();
        Vector3 escala = t.localScale;
        Color color = sr != null ? sr.color : Color.white;
        for (float k = 0f; k < duracionDesvanecer; k += Time.deltaTime)
        {
            float f = k / duracionDesvanecer;
            t.localScale = new Vector3(escala.x * (1f - f * 0.6f), escala.y * (1f + f * 0.25f) * (1f - f * 0.7f), escala.z);
            if (sr != null) sr.color = new Color(color.r * (1f - f * 0.4f), color.g * (1f - f * 0.2f), color.b, color.a * (1f - f));
            yield return null;
        }
        if (sr != null) sr.enabled = false;

        // 3. Negro, carga y nivel siguiente.
        // La partida pasa al nivel nuevo (si el siguiente es el menu, el juego termino).
        if (PantallaCarga.IndiceDestino(escenaDestino) != 0) Partida.GuardarCambioNivel(PantallaCarga.NombreDestino(escenaDestino));
        else Partida.Guardar();
        PantallaCarga.Cargar(escenaDestino, fundidoNegro);
    }

    private void Destello(float escala)
    {
        if (efectoEntrar != null && efectoEntrar.cantidad > 0)
            EfectoVisual.Crear(efectoEntrar, transform.position, escala, colorEfecto, false, -1f, "VFX", 30);
        ParticulasFx.Rafaga((Vector2)transform.position + Vector2.up * 0.9f, 18, colorEfecto, new Color(0.3f, 0.2f, 0.6f),
                            new Vector2(1.5f, 3.5f), 0.6f, new Vector2(0.05f, 0.12f), new Vector2(0.4f, 0.9f), 360f, 0f);
    }
}
