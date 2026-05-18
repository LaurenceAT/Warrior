using UnityEngine;

public class PlayerControler : MonoBehaviour
{
    private Rigidbody2D m_rigitbody2D;
    private GatherInput m_gatherInput;
    private Transform m_transform;
    [SerializeField] private float speed;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        m_gatherInput = GetComponent<GatherInput>();
        m_transform = GetComponent<Transform>();
        m_rigitbody2D = GetComponent<Rigidbody2D>();
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        m_rigitbody2D.linearVelocity = new Vector2(speed * m_gatherInput.ValueX, m_rigitbody2D.linearVelocityY);
    }
}
