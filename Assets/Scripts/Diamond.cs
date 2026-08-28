using System;
using UnityEngine;

// Diamante recolectable: elige una variante visual al azar y se recoge al tocar al player.
public class Diamond : MonoBehaviour
{
    [SerializeField] private GameManager gameManager;
    [SerializeField] private Rigidbody2D m_rigidbody2D;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Animator animator;
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

    // Elige al azar el índice de sprite/animación del diamante (una de 7 variantes).
    private void SetRandomDiamond()
    {
        var randomDiamondIndex = UnityEngine.Random.Range(0, 7);
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
        }
    }
}
