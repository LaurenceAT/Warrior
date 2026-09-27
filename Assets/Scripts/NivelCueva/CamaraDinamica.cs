using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

// Cámara con vida propia, encima de la CinemachineCamera:
//   - Se adelanta un poco hacia donde mira el player (se ve lo que viene).
//   - En una caida rapida mira hacia abajo (se ve donde se va a caer).
//   - Zoom por zonas (ZonaCamara): la arena del jefe se ve mas abierta.
//   - Zoom a peticion: CamaraDinamica.Ampliar(tamano, segundos) abre el plano un
//     rato, por ejemplo durante un ataque en area grande del jefe. Si hay varias
//     peticiones a la vez manda la mas abierta.
// Todo se suaviza para que no haya saltos de plano.
[RequireComponent(typeof(CinemachineCamera))]
public class CamaraDinamica : MonoBehaviour
{
    private static CamaraDinamica instancia;

    [Header("Zoom")]
    // Tamano ortografico normal. A 0 se toma el que tenga la camara al empezar.
    [SerializeField] private float tamanoBase;
    // Rapidez del zoom (unidades de tamano por segundo, aproximado).
    [SerializeField] private float rapidezZoom = 3f;

    [Header("Adelanto")]
    [SerializeField] private float adelanto = 1.1f;
    [SerializeField] private float rapidezAdelanto = 1.8f;
    // Al estar quieto el adelanto se reduce a esta fraccion.
    [Range(0f, 1f)] [SerializeField] private float adelantoQuieto = 0.4f;

    [Header("Encuadre")]
    // Cuanto por encima del player mira la camara: en un plataformas interesa ver
    // mas lo de arriba que la roca de debajo del suelo.
    [SerializeField] private float alturaMirada = 1.1f;

    [Header("Caidas")]
    [SerializeField] private float mirarAbajo = 1.8f;
    [SerializeField] private float velocidadCaidaMinima = 9f;
    [SerializeField] private float rapidezVertical = 2.5f;

    private CinemachineCamera cam;
    private CinemachinePositionComposer composer;
    private CinemachineConfiner2D confiner;
    // Pixel perfect: redondea el zoom a pasos nitidos (a 1080p, de 5.6 salta a 8.4).
    // Mientras hay zoom se apaga para que la camara se abra suave, y al volver al
    // tamano normal se enciende otra vez.
    private CinemachinePixelPerfect pixelPerfectCine;
    private Behaviour pixelPerfectCamara;
    private Vector3 offsetBase;
    private Vector2 offsetActual;
    private float tamanoActual;

    private readonly List<ZonaCamara> zonas = new List<ZonaCamara>();
    private readonly List<(float tamano, float hasta)> pulsos = new List<(float, float)>();

    private void Awake()
    {
        instancia = this;
        cam = GetComponent<CinemachineCamera>();
        composer = GetComponent<CinemachinePositionComposer>();
        confiner = GetComponent<CinemachineConfiner2D>();
        if (tamanoBase <= 0f) tamanoBase = cam.Lens.OrthographicSize;
        tamanoActual = cam.Lens.OrthographicSize;
        if (composer != null) offsetBase = composer.TargetOffset;
        pixelPerfectCine = GetComponent<CinemachinePixelPerfect>();
        if (Camera.main != null)
            pixelPerfectCamara = Camera.main.GetComponent<UnityEngine.Rendering.Universal.PixelPerfectCamera>();
    }

    private void OnEnable()
    {
        GameManager.AlReaparecerPlayer += OlvidarZonas;
    }

    private void OnDisable()
    {
        GameManager.AlReaparecerPlayer -= OlvidarZonas;
    }

    // Al morir dentro de una zona no llega su OnTriggerExit (el player se
    // destruye): se olvidan todas y el nuevo player vuelve a entrar si toca.
    private void OlvidarZonas()
    {
        zonas.Clear();
        pulsos.Clear();
    }

    private void OnDestroy()
    {
        if (instancia == this) instancia = null;
    }

    // Abre el plano hasta "tamano" durante "segundos" (luego vuelve solo).
    public static void Ampliar(float tamano, float segundos)
    {
        if (instancia == null) return;
        instancia.pulsos.Add((tamano, Time.time + segundos));
    }

    public static void EntrarZona(ZonaCamara z)
    {
        if (instancia != null && !instancia.zonas.Contains(z)) instancia.zonas.Add(z);
    }

    public static void SalirZona(ZonaCamara z)
    {
        if (instancia != null) instancia.zonas.Remove(z);
    }

    private void LateUpdate()
    {
        float dt = Time.unscaledDeltaTime;
        ActualizarZoom(dt);
        ActualizarOffset(dt);
    }

    private void ActualizarZoom(float dt)
    {
        float objetivo = tamanoBase;
        // La ultima zona en la que se ha entrado manda (las zonas se solapan poco).
        if (zonas.Count > 0) objetivo = zonas[zonas.Count - 1].Tamano;

        pulsos.RemoveAll(p => Time.time >= p.hasta);
        foreach (var p in pulsos) objetivo = Mathf.Max(objetivo, p.tamano);

        bool zoom = Mathf.Abs(objetivo - tamanoBase) > 0.01f || Mathf.Abs(tamanoActual - tamanoBase) > 0.01f;
        if (pixelPerfectCine != null) pixelPerfectCine.enabled = !zoom;
        if (pixelPerfectCamara != null) pixelPerfectCamara.enabled = !zoom;

        if (Mathf.Abs(tamanoActual - objetivo) < 0.001f) return;

        // Suave al llegar: rapido lejos, lento cerca.
        float paso = Mathf.Max(rapidezZoom * dt * Mathf.Abs(objetivo - tamanoActual), 0.15f * dt);
        tamanoActual = Mathf.MoveTowards(tamanoActual, objetivo, paso);

        LensSettings lente = cam.Lens;
        lente.OrthographicSize = tamanoActual;
        cam.Lens = lente;
        // El confiner guarda calculos por tamano de lente: hay que avisarle.
        if (confiner != null) confiner.InvalidateLensCache();
    }

    private void ActualizarOffset(float dt)
    {
        if (composer == null || cam.Follow == null) return;

        Transform objetivo = cam.Follow;
        Rigidbody2D rb = objetivo.GetComponent<Rigidbody2D>();
        float mirada = Mathf.Sign(objetivo.lossyScale.x);
        Vector2 vel = rb != null ? rb.linearVelocity : Vector2.zero;

        bool moviendose = Mathf.Abs(vel.x) > 0.5f;
        float x = mirada * adelanto * (moviendose ? 1f : adelantoQuieto);
        float extra = zonas.Count > 0 ? zonas[zonas.Count - 1].DesplazamientoY : 0f;
        float y = vel.y < -velocidadCaidaMinima ? -mirarAbajo : alturaMirada + extra;

        offsetActual.x = Mathf.Lerp(offsetActual.x, x, 1f - Mathf.Exp(-rapidezAdelanto * dt));
        offsetActual.y = Mathf.Lerp(offsetActual.y, y, 1f - Mathf.Exp(-rapidezVertical * dt));
        composer.TargetOffset = offsetBase + (Vector3)offsetActual;
    }
}
