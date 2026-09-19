using System;
using UnityEngine;

// Diamante recolectable: elige una variante visual al azar y se recoge al tocar al player.
public class Diamond : MonoBehaviour
{
    [SerializeField] private GameManager gameManager;
    [SerializeField] private Rigidbody2D m_rigidbody2D;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Animator animator;
    // Cuánto dura la animación DiamondVFX, tras la cual el diamante ya recogido se destruye.
    [SerializeField] private float vfxDuration = 0.7f;
    private int idPickedDiamond;
    private int idDiamondIndex;

    // Obtiene los componentes y los hashes del Animator.
    private void Awake()
    {
        m_rigidbody2D = GetComponent<Rigidbody2D>();
        animator = GetComponentInChildren<Animator>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        idPickedDiamond = Animator.StringToHash("pickedDiamond");
        idDiamondIndex = Animator.StringToHash("diamondIndex");
    }

    // Guarda la referencia al GameManager y elige la variante visual del diamante.
    private void Start()
    {
        gameManager = GameManager.Instance;
        SetRandomDiamond();
    }

    // Elige al azar el color del diamante. El Blend Tree tiene 5 variantes (umbrales 0 a 4),
    // así que el rango debe ser 0..4: valores mayores se recortarían al último color.
    private void SetRandomDiamond()
    {
        var randomDiamondIndex = UnityEngine.Random.Range(0, 5);
        animator.SetFloat(idDiamondIndex, randomDiamondIndex);
    }

    // Detecta la recolección por parte del player: desactiva la física y suma el diamante al contador.
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            //spriteRenderer.enabled = false;
            m_rigidbody2D.simulated = false;
            gameManager.AddDiamond();
            animator.SetTrigger(idPickedDiamond);

            // El estado DiamondVFX no tiene salida y su clip no loopea: sin esto el diamante
            // recogido se quedaría para siempre congelado en el último frame del efecto.
            Destroy(gameObject, vfxDuration);
        }
    }
}
