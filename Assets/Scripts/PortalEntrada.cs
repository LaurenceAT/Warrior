using System.Collections;
using UnityEngine;

// Portal de entrada del nivel (sustituye a la puerta de entrada): al empezar el
// nivel se abre, destella y suena mientras el personaje aparece, y luego se
// cierra. Si el player reaparece aqui tras morir, vuelve a abrirse.
// Lleva la etiqueta EntranceDoor, como la puerta vieja.
public class PortalEntrada : MonoBehaviour
{
    [Tooltip("Animacion del portal (el SpriteRenderer que la muestra).")]
    [SerializeField] private AnimadorHoja animacionPortal;
    [Tooltip("Tamano del portal abierto.")]
    [SerializeField] private float escala = 1f;
    [Tooltip("Segundos abierto antes de cerrarse (0 = se queda abierto).")]
    [SerializeField] private float cerrarTras = 2.2f;
    [SerializeField] private float duracionAbrir = 0.3f;
    [SerializeField] private float duracionCerrar = 0.45f;

    [Header("Efectos")]
    [SerializeField] private AnimadorHoja.Clip efectoSalir;
    [SerializeField] private Color colorEfecto = new Color(0.75f, 0.6f, 1f, 1f);
    [SerializeField] private float escalaEfecto = 1.6f;
    [Tooltip("Clave del sonido en Resources/RecursosRPG (se cambia el audio alli).")]
    [SerializeField] private string sonidoSalir = "portal_salir";
    [Tooltip("Retraso del destello, para que coincida con la aparicion del personaje.")]
    [SerializeField] private float retrasoDestello = 0.15f;

    private Transform visual;
    private Coroutine rutina;

    private void Awake()
    {
        visual = animacionPortal != null ? animacionPortal.transform : transform;
    }

    private void OnEnable()
    {
        GameManager.AlReaparecerPlayer += AlReaparecer;
    }

    private void OnDisable()
    {
        GameManager.AlReaparecerPlayer -= AlReaparecer;
    }

    private void Start()
    {
        Abrir();
    }

    private void AlReaparecer()
    {
        // Solo si el player sale por aqui (sin hoguera activa).
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null && Vector2.Distance(p.transform.position, transform.position) < 4f) Abrir();
    }

    public void Abrir()
    {
        if (rutina != null) StopCoroutine(rutina);
        rutina = StartCoroutine(Secuencia());
    }

    private IEnumerator Secuencia()
    {
        visual.gameObject.SetActive(true);
        for (float t = 0f; t < duracionAbrir; t += Time.deltaTime)
        {
            visual.localScale = Vector3.one * escala * Mathf.SmoothStep(0f, 1f, t / duracionAbrir);
            yield return null;
        }
        visual.localScale = Vector3.one * escala;

        yield return new WaitForSeconds(retrasoDestello);
        Sonido.Reproducir(sonidoSalir);
        Vector2 centro = (Vector2)transform.position + Vector2.up * 0.9f;
        if (efectoSalir != null && efectoSalir.cantidad > 0)
            EfectoVisual.Crear(efectoSalir, transform.position, escalaEfecto, colorEfecto, false, -1f, "VFX", 30);
        ParticulasFx.Rafaga(centro, 16, colorEfecto, new Color(0.3f, 0.2f, 0.6f),
                            new Vector2(1.5f, 3.2f), 0.4f, new Vector2(0.05f, 0.12f), new Vector2(0.4f, 0.9f), 360f, 0f);

        if (cerrarTras <= 0f) yield break;
        yield return new WaitForSeconds(cerrarTras);
        for (float t = 0f; t < duracionCerrar; t += Time.deltaTime)
        {
            visual.localScale = new Vector3(escala * (1f - t / duracionCerrar), escala * (1f + 0.3f * t / duracionCerrar) * (1f - t / duracionCerrar), 1f);
            yield return null;
        }
        visual.gameObject.SetActive(visual == transform);
        if (visual == transform) visual.localScale = Vector3.zero;
    }
}
