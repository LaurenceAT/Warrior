using System.Collections;
using UnityEngine;

// Plataforma que flota suavemente en su sitio y se desploma cuando el player la pisa.
// Tras caer una distancia desaparece y vuelve a aparecer en su posición original.
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class FallingPlatform : MonoBehaviour
{
    private enum State { Reposo, Aviso, Cayendo, Oculta }

    [Header("Balanceo en reposo")]
    // Pequeño vaivén vertical que le da vida mientras nadie la pisa.
    [SerializeField] private bool bob = true;
    [SerializeField] private float bobAmplitude = 0.08f;
    [SerializeField] private float bobSpeed = 2f;

    [Header("Caída")]
    // Margen entre pisarla y empezar a caer, para que el jugador pueda reaccionar.
    [SerializeField] private float delayBeforeFall = 0.5f;
    // Conviene dejarla por debajo de la gravedad del player (29,4 = 9,81 x 3): así la
    // plataforma cae un poco más lenta que él y el jugador se mantiene encima al caer.
    [SerializeField] private float fallAcceleration = 25f;
    [SerializeField] private float maxFallSpeed = 20f;
    // Distancia que recorre antes de desaparecer.
    [SerializeField] private float fallDistance = 6f;
    // Solo se activa si la pisan desde arriba, no al golpearla por debajo.
    [SerializeField] private bool onlyFromAbove = true;

    [Header("Reaparición")]
    [SerializeField] private float respawnDelay = 2.5f;
    // Cuánto tarda en encogerse y desvanecerse al desaparecer.
    [SerializeField] private float disappearTime = 0.15f;
    // Cuánto tarda en volver, creciendo con un pequeño rebote.
    [SerializeField] private float appearTime = 0.3f;
    // Parpadeo de aviso en los últimos segundos de la espera. A 0 se desactiva.
    [SerializeField] private float telegraphTime = 0.6f;
    [SerializeField] private int telegraphBlinks = 3;
    // Tamaño al que parpadea y desde el que crece al volver.
    [SerializeField] private float telegraphScale = 0.35f;

    [Header("Animación")]
    [SerializeField] private Animator animator;
    // Se usan por nombre de estado, sin parámetros ni transiciones en el controlador.
    // Ojo al nombre de los sprites: "On" es la plataforma viva, flotando, y "Off" la
    // que ya se apagó al pisarla. Por eso van al revés de lo que sugiere el estado.
    [SerializeField] private string floatingStateName = "FallingPlatform_On";
    [SerializeField] private string triggeredStateName = "FallingPlatform_Off";

    private Rigidbody2D rb;
    private Collider2D myCollider;
    private SpriteRenderer spriteRenderer;

    private Vector2 startPosition;
    private State state = State.Reposo;
    private float fallSpeed;
    // Escala y color originales, para volver a ellos tras animar la aparición.
    private Vector3 baseScale;
    private Color baseColor = Color.white;

    #region Ciclo de Unity

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        myCollider = GetComponent<Collider2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (animator == null) animator = GetComponentInChildren<Animator>();

        startPosition = rb.position;
        baseScale = transform.localScale;
        if (spriteRenderer != null) baseColor = spriteRenderer.color;
    }

    private void Start()
    {
        // Animator.Play() con un estado inexistente no da error, solo deja de animar.
        WarnIfMissingState(floatingStateName);
        WarnIfMissingState(triggeredStateName);

        // Arranca flotando, sin depender de cuál sea el estado por defecto del Animator.
        PlayState(floatingStateName);
    }

    private void FixedUpdate()
    {
        switch (state)
        {
            case State.Reposo:
                if (bob)
                {
                    float desfase = Mathf.Sin(Time.time * bobSpeed) * bobAmplitude;
                    rb.MovePosition(startPosition + Vector2.up * desfase);
                }
                break;

            case State.Cayendo:
                fallSpeed = Mathf.Min(fallSpeed + fallAcceleration * Time.fixedDeltaTime, maxFallSpeed);
                rb.MovePosition(rb.position + Vector2.down * (fallSpeed * Time.fixedDeltaTime));

                if (startPosition.y - rb.position.y >= fallDistance)
                {
                    // Cambiamos de estado ya, para que este FixedUpdate deje de moverla.
                    state = State.Oculta;
                    StartCoroutine(DisappearAndRespawn());
                }
                break;
        }
    }

    // Se desploma cuando el player la pisa.
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (state != State.Reposo) return;
        if (!collision.collider.CompareTag("Player")) return;

        // Comparamos los bordes: los pies del player deben estar por encima de la
        // superficie de la plataforma, para que un cabezazo desde abajo no la tire.
        if (onlyFromAbove && collision.collider.bounds.min.y < myCollider.bounds.max.y - 0.08f) return;

        StartCoroutine(FallRoutine());
    }

    // Dibuja hasta dónde cae antes de desaparecer.
    private void OnDrawGizmosSelected()
    {
        Vector3 origen = Application.isPlaying ? (Vector3)startPosition : transform.position;
        Vector3 fin = origen + Vector3.down * fallDistance;

        Gizmos.color = Color.red;
        Gizmos.DrawLine(origen, fin);
        Gizmos.DrawLine(fin + Vector3.left * 0.5f, fin + Vector3.right * 0.5f);
    }

    #endregion

    // Avisa con la animación, espera y empieza la caída.
    private IEnumerator FallRoutine()
    {
        state = State.Aviso;
        PlayState(triggeredStateName);

        yield return new WaitForSeconds(delayBeforeFall);

        fallSpeed = 0f;
        state = State.Cayendo;
    }

    // Se desvanece encogiéndose, espera y vuelve a su sitio con un pequeño rebote.
    private IEnumerator DisappearAndRespawn()
    {
        myCollider.enabled = false;

        yield return TrapFx.ScaleAndFade(transform, spriteRenderer, baseScale, baseColor, 1f, 0f, disappearTime, false);
        if (spriteRenderer != null) spriteRenderer.enabled = false;

        // La devolvemos a su sitio ya oculta, para que el aviso y la reaparición
        // ocurran donde el jugador espera encontrarla.
        rb.position = startPosition;
        fallSpeed = 0f;

        yield return new WaitForSeconds(Mathf.Max(0f, respawnDelay - telegraphTime));
        yield return TrapFx.Telegraph(transform, spriteRenderer, baseScale, baseColor, telegraphScale, telegraphTime, telegraphBlinks);

        PlayState(floatingStateName);
        if (spriteRenderer != null) spriteRenderer.enabled = true;

        yield return TrapFx.ScaleAndFade(transform, spriteRenderer, baseScale, baseColor, telegraphScale, 1f, appearTime, true);

        myCollider.enabled = true;
        state = State.Reposo;
    }

    private void PlayState(string stateName)
    {
        if (animator == null || string.IsNullOrEmpty(stateName)) return;
        animator.Play(stateName, 0, 0f);
    }

    private void WarnIfMissingState(string stateName)
    {
        if (animator == null || string.IsNullOrEmpty(stateName)) return;
        if (animator.HasState(0, Animator.StringToHash(stateName))) return;

        Debug.LogWarning($"La plataforma '{name}' no encuentra el estado '{stateName}' en su Animator. Revisa el nombre.", this);
    }
}
