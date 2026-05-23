using System;
using System.Collections;
using UnityEditor.Tilemaps;
using UnityEngine;

public class PlayerControler : MonoBehaviour
{
    //COMPONENTES DE PLAYER
    [Header("Componentes")]
    [SerializeField] private Transform m_transform;
    private Rigidbody2D m_rigitbody2D;
    private GatherInput m_gatherInput;
    private Animator m_animator;

    //ANIMATOR IDS
    private int idIsGrounded;
    private int idSpeed;
    private int idIsWallDetected;

    [Header("Opciones de Movimiento y Salto")]
    [SerializeField] private float speed;
    private int direction = 1;

    [SerializeField] private float jumpForce;
    [SerializeField] private int extraJumps;
    [SerializeField] private int counterExtraJumps;
    [SerializeField] private bool canDoubleJump;

    [Header("Opciones del Ground")]
    [SerializeField] private Transform lFoot;
    [SerializeField] private Transform rFoot;
    RaycastHit2D lFootRay;
    RaycastHit2D rFootRay;
    [SerializeField] private bool isGrounded;
    [SerializeField] private float rayLegnth;
    [SerializeField] private LayerMask groundLayer;

    [Header("Opciones de la Pared")]
    [SerializeField] private float checkWallDistance;
    [SerializeField] private bool isWallDetected;
    [SerializeField] private bool canWallSlide;
    [SerializeField] private float slideSpeed;
    [SerializeField] private Vector2 wallJumpForce;
    [SerializeField] private bool isWallJumping;
    [SerializeField] private float wallJumpDuration;


    //COMPONENTES DEL PLAYER
    private void Awake()
    {
        m_gatherInput = GetComponent<GatherInput>();
        m_transform = GetComponent<Transform>();
        m_rigitbody2D = GetComponent<Rigidbody2D>();
        m_animator = GetComponent<Animator>();
    }
    // OTROS COMPONENTES 
    void Start()
    {
        idSpeed = Animator.StringToHash("Speed");
        idIsGrounded = Animator.StringToHash("isGrounded");
        idIsWallDetected = Animator.StringToHash("isWallDetected");
        lFoot = GameObject.Find("LFoot").GetComponent<Transform>();
        rFoot = GameObject.Find("RFoot").GetComponent<Transform>();
        counterExtraJumps = extraJumps;
    }

    private void Update()
    {
        SetAnimatorValues();
    }

    private void SetAnimatorValues()
    {
        m_animator.SetFloat(idSpeed, Mathf.Abs(m_rigitbody2D.linearVelocityX));
        m_animator.SetBool(idIsGrounded, isGrounded);
        m_animator.SetBool(idIsWallDetected, isWallDetected);
    }

    // 
    void FixedUpdate()
    {
        CheckCollision();
        Move(); 
        Jump();
    }

    //MOVIMIENTO DEL PERSONAJE
    private void Move()
    {
        if (isWallDetected && !isGrounded) return;
        if (isWallJumping) return;
        Flip();
        m_rigitbody2D.linearVelocity = new Vector2(speed * m_gatherInput.Value.x, m_rigitbody2D.linearVelocityY);

    }

    //GIRO DE DIRECCION DEL PERSONAJE
    private void Flip()
    {
        if (m_gatherInput.Value.x * direction < 0)
        {
            HandleDirection();
        }
    }

    private void HandleDirection()
    {
        m_transform.localScale = new Vector3(-m_transform.localScale.x, 1, 1);
        direction *= -1;
    }

    //MECANICA DE SALTO DEL PERSONAJE
    private void Jump()
    {
        if (m_gatherInput.IsJumping) 
        {
            if (isGrounded)
            {
                m_rigitbody2D.linearVelocity = new Vector2(speed * m_gatherInput.Value.x, jumpForce);
                canDoubleJump = true;
            }
            else if (isWallDetected) WallJump();
            else if (counterExtraJumps > 0 && canDoubleJump) DoubleJump();
        }
        m_gatherInput.IsJumping = false;
    }

    //SALTO DE LA PARED
    private void WallJump()
    { 
        m_rigitbody2D.linearVelocity = new Vector2(wallJumpForce.x * -direction, wallJumpForce.y);
        HandleDirection();
        StartCoroutine(WallJumpRoutine());
    }

    IEnumerator WallJumpRoutine()
    {
        isWallJumping = true;
        yield return new WaitForSeconds(wallJumpDuration);
        isWallJumping = false;
    }



    private void DoubleJump()
    {
        m_rigitbody2D.linearVelocity = new Vector2(speed * m_gatherInput.Value.x, jumpForce);
        counterExtraJumps-=1;
    }

    private void CheckCollision()
    {
        HandleGround();
        HandleWall();
        HandleWallSlide();
    }

    private void HandleGround()
    {
        lFootRay = Physics2D.Raycast(lFoot.position, Vector2.down, rayLegnth, groundLayer);
        rFootRay = Physics2D.Raycast(rFoot.position, Vector2.down, rayLegnth, groundLayer);
        if (lFootRay || rFootRay)
        {
            isGrounded = true;
            counterExtraJumps = extraJumps;
            canDoubleJump = false;
        }
        else
        {
            isGrounded = false;
        }
    }
    private void HandleWall()
    {
        isWallDetected = Physics2D.Raycast(m_transform.position, Vector2.right * direction, checkWallDistance, groundLayer);
    }

    private void HandleWallSlide()
    {
        canWallSlide = isWallDetected;
        if (!canWallSlide) return;
        canDoubleJump = false;
        slideSpeed = m_gatherInput.Value.y < 0 ? 1 : 0.5f;
        m_rigitbody2D.linearVelocity = new Vector2(m_rigitbody2D.linearVelocityX, m_rigitbody2D.linearVelocityY * slideSpeed);
    }



    //lINEA DE DETECCION DE LA PARED
    private void OnDrawGizmos()
    {
        Gizmos.DrawLine(m_transform.position, new Vector2(m_transform.position.x + (checkWallDistance * direction), m_transform.position.y));
    }

}
