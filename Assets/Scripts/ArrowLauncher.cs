using System.Collections;
using UnityEngine;

// Flecha que gira sobre su propio eje e impulsa al player hacia donde apunta su punta
// en el momento de tocarla. Tras usarse desaparece y vuelve a aparecer en el mismo
// sitio pasados unos segundos.
[RequireComponent(typeof(Collider2D))]
public class ArrowLauncher : MonoBehaviour
{
    [Header("Giro")]
    // Grados por segundo. En negativo gira al contrario.
    [SerializeField] private float rotationSpeed = 120f;
    // Corrección en grados para que la punta dibujada coincida con la dirección real
    // del impulso. Ajústalo mirando la línea verde del gizmo.
    [SerializeField] private float tipAngleOffset;
    // Sigue girando mientras está oculta, para que el jugador pueda cronometrar su vuelta.
    [SerializeField] private bool keepSpinningWhileHidden = true;

    [Header("Impulso")]
    [SerializeField] private float launchForce = 14f;
    // Tiempo que el player pierde el control horizontal. Sin esto, su Move() anularía
    // el impulso en el mismo FixedUpdate.
    [SerializeField] private float controlLockTime = 0.25f;

    [Header("Reaparición")]
    [SerializeField] private float respawnDelay = 2f;
    // Instante en que se ve el sprite de "disparada" antes de desaparecer.
    [SerializeField] private float activeFlashTime = 0.08f;
    // Cuánto tarda en encogerse y desvanecerse al usarse.
    [SerializeField] private float disappearTime = 0.15f;
    // Cuánto tarda en volver, creciendo con un pequeño rebote.
    [SerializeField] private float appearTime = 0.3f;
    // Parpadeo de aviso en los últimos segundos de la espera, para que el jugador
    // vea dónde y cuándo va a reaparecer. A 0 se desactiva.
    [SerializeField] private float telegraphTime = 0.6f;
    [SerializeField] private int telegraphBlinks = 3;
    // Tamaño al que parpadea y desde el que crece al volver.
    [SerializeField] private float telegraphScale = 0.35f;

    [Header("Animación")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    // Si hay Animator se usan los estados de abajo por nombre, sin necesidad de crear
    // parámetros ni transiciones en el controlador.
    [SerializeField] private Animator animator;
    [SerializeField] private string idleStateName = "Arrow_Idle";
    [SerializeField] private string activeStateName = "Arrow_Active";

    // Alternativa sin Animator: si los rellenas, se intercambian estos dos sprites.
    // Déjalos vacíos cuando uses Animator, o este los pisaría.
    [SerializeField] private Sprite idleSprite;
    [SerializeField] private Sprite activeSprite;

    private Collider2D myCollider;
    private bool isAvailable = true;
    // Escala y color originales, para volver a ellos tras animar la aparición.
    private Vector3 baseScale;
    private Color baseColor = Color.white;

    // Dirección a la que apunta la punta ahora mismo.
    public Vector2 TipDirection => (Quaternion.Euler(0f, 0f, tipAngleOffset) * transform.right).normalized;

    #region Ciclo de Unity

    private void Awake()
    {
        myCollider = GetComponent<Collider2D>();

        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (animator == null) animator = GetComponentInChildren<Animator>();

        baseScale = transform.localScale;
        if (spriteRenderer != null) baseColor = spriteRenderer.color;
    }

    private void Update()
    {
        if (!isAvailable && !keepSpinningWhileHidden) return;

        transform.Rotate(0f, 0f, rotationSpeed * Time.deltaTime);
    }

    // Impulsa al player y arranca el ciclo de desaparición y reaparición.
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!isAvailable) return;
        if (!other.CompareTag("Player")) return;

        PlayerControler player = other.GetComponent<PlayerControler>();
        if (player == null) return;

        player.Launch(TipDirection * launchForce, controlLockTime);
        StartCoroutine(UseRoutine());
    }

    // Dibuja hacia dónde saldría disparado el player.
    private void OnDrawGizmos()
    {
        Vector3 punta = transform.position + (Vector3)(TipDirection * 1.5f);

        Gizmos.color = Color.green;
        Gizmos.DrawLine(transform.position, punta);
        Gizmos.DrawWireSphere(punta, 0.12f);
    }

    #endregion

    // Oculta la flecha, espera y la devuelve a su sitio.
    private IEnumerator UseRoutine()
    {
        isAvailable = false;
        myCollider.enabled = false;

        // Animación de disparo antes de desaparecer.
        if (animator != null && !string.IsNullOrEmpty(activeStateName)) animator.Play(activeStateName, 0, 0f);
        else if (spriteRenderer != null && activeSprite != null) spriteRenderer.sprite = activeSprite;

        if (activeFlashTime > 0f) yield return new WaitForSeconds(activeFlashTime);

        // Se encoge y se desvanece en vez de desaparecer de golpe.
        yield return TrapFx.ScaleAndFade(transform, spriteRenderer, baseScale, baseColor, 1f, 0f, disappearTime, false);
        SetRendererVisible(false);

        // Espera, reservando el final para el parpadeo de aviso.
        yield return new WaitForSeconds(Mathf.Max(0f, respawnDelay - telegraphTime));
        yield return TrapFx.Telegraph(transform, spriteRenderer, baseScale, baseColor, telegraphScale, telegraphTime, telegraphBlinks);

        // Vuelve a su estado de reposo y crece con un pequeño rebote.
        if (animator != null && !string.IsNullOrEmpty(idleStateName)) animator.Play(idleStateName, 0, 0f);
        else if (spriteRenderer != null && idleSprite != null) spriteRenderer.sprite = idleSprite;

        SetRendererVisible(true);
        yield return TrapFx.ScaleAndFade(transform, spriteRenderer, baseScale, baseColor, telegraphScale, 1f, appearTime, true);

        myCollider.enabled = true;
        isAvailable = true;
    }

    private void SetRendererVisible(bool visible)
    {
        if (spriteRenderer != null) spriteRenderer.enabled = visible;
    }
}
