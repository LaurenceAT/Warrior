using System;
using UnityEditor.Tilemaps;
using UnityEngine;

public class PlayerControler : MonoBehaviour
{
    //COMPONENTES DE JUGADOR
    private Rigidbody2D m_rigitbody2D;
    private GatherInput m_gatherInput;
    private Transform m_transform;
    private Animator m_animator;

    [Header("Opciones de Movimiento y Salto")]
    [SerializeField] private float speed;
    private int direction = 1;

    [SerializeField] private float jumpForce;
    [SerializeField] private int extraJumps;
    [SerializeField] private int counterExtraJumps;
    private int idSpeed;

    [Header("Opciones del Ground")]
    [SerializeField] private Transform lFoot;
    [SerializeField] private Transform rFoot;
    [SerializeField] private bool isGrounded;
    [SerializeField] private float rayLegnth;
    [SerializeField] private LayerMask groundLayer;
    private int idIsGrounded;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        m_gatherInput = GetComponent<GatherInput>();
        m_transform = GetComponent<Transform>();
        m_rigitbody2D = GetComponent<Rigidbody2D>();
        m_animator = GetComponent<Animator>();
        idSpeed = Animator.StringToHash("Speed");
        idIsGrounded = Animator.StringToHash("isGrounded");
        lFoot = GameObject.Find("LFoot").GetComponent<Transform>();
        rFoot = GameObject.Find("RFoot").GetComponent<Transform>();
    }

    private void Update()
    {
        SetAnimatorValues();
    }

    private void SetAnimatorValues()
    {
        m_animator.SetFloat(idSpeed, Mathf.Abs(m_rigitbody2D.linearVelocityX));
        m_animator.SetBool(idIsGrounded, isGrounded);
    }

    // 
    void FixedUpdate()
    {
        Move(); 
        Jump();
        CheckGround();
    }

    private void Move()
    {
        Flip();
        m_rigitbody2D.linearVelocity = new Vector2(speed * m_gatherInput.ValueX, m_rigitbody2D.linearVelocityY);

    }

    private void Flip()
    {
        if (m_gatherInput.ValueX * direction < 0)
        {
            m_transform.localScale = new Vector3(-m_transform.localScale.x, 1, 1);
            direction *= -1;
        }
    }

    private void Jump()
    {
        if (m_gatherInput.IsJumping) 
        {
            if (isGrounded)
                 m_rigitbody2D.linearVelocity = new Vector2(speed * m_gatherInput.ValueX, jumpForce);
            if (counterExtraJumps > 0)
            {
                m_rigitbody2D.linearVelocity = new Vector2(speed * m_gatherInput.ValueX, jumpForce);
                counterExtraJumps--;
            }
        }
        m_gatherInput.IsJumping = false;
    }
    private void CheckGround()
    {
        RaycastHit2D lFootRay = Physics2D.Raycast(lFoot.position, Vector2.down, rayLegnth, groundLayer);
        RaycastHit2D rFootRay = Physics2D.Raycast(rFoot.position, Vector2.down, rayLegnth, groundLayer);
        if (lFootRay || rFootRay)
        { 
            isGrounded = true;
            counterExtraJumps = extraJumps;
        }
        else
        {
            isGrounded = false;
        }

    }
}
