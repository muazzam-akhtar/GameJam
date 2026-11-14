using UnityEngine;

public class P2Controller : MonoBehaviour
{
    [SerializeField] private float  moveSpeed = 50.0f;
    [SerializeField] private Rigidbody               rb;
    private Vector2                 movementInput;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
    }

    private void Update()
    {
        float   moveX = Input.GetAxisRaw("P2_Horizontal");
        float   moveZ = Input.GetAxisRaw("P2_Vertical");

        movementInput = new Vector2(moveX, moveZ);
        Debug.Log(movementInput);
    }

    private void FixedUpdate()
    {
        MovePlayer();
    }

    void    MovePlayer()
    {
        Vector3 moveDirection = new Vector3(movementInput.x, 0.0f, movementInput.y).normalized;
        Vector3 targetVelocity = moveDirection * moveSpeed;
        rb.linearVelocity = targetVelocity;
    }
}
