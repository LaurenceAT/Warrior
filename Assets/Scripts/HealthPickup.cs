using System.Collections;
using UnityEngine;

// Corazon que devuelve vida al player.
//
// La animacion va por codigo en vez de con un Animator: son dos tiras de
// fotogramas (reposo y recogida) y asi basta con arrastrar los sprites al
// Inspector, sin montar controller ni transiciones.
public class HealthPickup : MonoBehaviour
{
    [Header("Curacion")]
    [SerializeField] private int healAmount = 1;
    // Con la vida llena, el corazon se queda donde esta en vez de gastarse para
    // nada. Si lo marcas, se recoge igual aunque no cure.
    [SerializeField] private bool consumeWhenFull;

    [Header("Animacion de reposo")]
    // Los 8 fotogramas de "Big Heart Idle".
    [SerializeField] private Sprite[] idleFrames;
    [SerializeField] private float idleFps = 10f;
    // Flota arriba y abajo mientras espera.
    [SerializeField] private float bobHeight = 0.08f;
    [SerializeField] private float bobSpeed = 2.5f;

    [Header("Recogida")]
    // Los 2 fotogramas de "Big Heart Hit".
    [SerializeField] private Sprite[] pickupFrames;
    [SerializeField] private float pickupFps = 14f;
    // Al recogerlo sube, crece un poco y se desvanece.
    [SerializeField] private float pickupRise = 0.5f;
    [SerializeField] private float pickupScale = 1.6f;
    [SerializeField] private float pickupDuration = 0.35f;
    // Opcional: particulas o destello que aparece al recogerlo.
    [SerializeField] private GameObject pickupEffect;

    private SpriteRenderer spriteRenderer;
    private Collider2D col;
    private Vector3 posicionBase;
    private Vector3 escalaBase;
    private float fase;
    private bool recogido;

    private void Awake()
    {
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        col = GetComponent<Collider2D>();
        posicionBase = transform.position;
        escalaBase = transform.localScale;

        // Cada corazon arranca en un punto distinto del ciclo. Si no, varios
        // corazones juntos flotarian y latirian sincronizados, que queda artificial.
        fase = Random.Range(0f, 10f);

        // Aviso si la capa del corazon no colisiona con la del player: el trigger
        // no saltaria nunca y no habria ni error ni pista de por que.
        int capaPlayer = LayerMask.NameToLayer("Player");
        if (capaPlayer >= 0 && Physics2D.GetIgnoreLayerCollision(gameObject.layer, capaPlayer))
            Debug.LogWarning($"[HealthPickup] {name} esta en la capa \"{LayerMask.LayerToName(gameObject.layer)}\", " +
                             "que no colisiona con Player. Ponlo en la capa Items o no se podra recoger.", this);
    }

    private void Update()
    {
        if (recogido) return;

        float t = Time.time + fase;

        if (idleFrames != null && idleFrames.Length > 0 && spriteRenderer != null)
        {
            int frame = (int)(t * idleFps) % idleFrames.Length;
            spriteRenderer.sprite = idleFrames[frame];
        }

        transform.position = posicionBase + Vector3.up * (Mathf.Sin(t * bobSpeed) * bobHeight);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (recogido) return;
        if (!other.CompareTag("Player")) return;

        PlayerControler player = other.GetComponent<PlayerControler>();
        if (player == null) return;

        bool curo = player.Heal(healAmount);
        if (!curo && !consumeWhenFull) return;

        recogido = true;
        if (col != null) col.enabled = false;

        if (pickupEffect != null)
            Instantiate(pickupEffect, transform.position, Quaternion.identity);

        StartCoroutine(RecogidaRoutine());
    }

    // Sube, crece y se desvanece mientras reproduce los fotogramas de recogida.
    private IEnumerator RecogidaRoutine()
    {
        Vector3 inicio = transform.position;
        Color colorBase = spriteRenderer != null ? spriteRenderer.color : Color.white;
        float transcurrido = 0f;

        while (transcurrido < pickupDuration)
        {
            transcurrido += Time.deltaTime;
            float k = Mathf.Clamp01(transcurrido / pickupDuration);

            if (pickupFrames != null && pickupFrames.Length > 0 && spriteRenderer != null)
            {
                int frame = Mathf.Min((int)(transcurrido * pickupFps), pickupFrames.Length - 1);
                spriteRenderer.sprite = pickupFrames[frame];
            }

            // Sale rapido y frena al final: se lee como un salto, no como un ascensor.
            float salida = 1f - (1f - k) * (1f - k);
            transform.position = inicio + Vector3.up * (pickupRise * salida);
            transform.localScale = escalaBase * Mathf.Lerp(1f, pickupScale, salida);

            if (spriteRenderer != null)
            {
                Color c = colorBase;
                c.a = colorBase.a * (1f - k);
                spriteRenderer.color = c;
            }

            yield return null;
        }

        Destroy(gameObject);
    }
}
