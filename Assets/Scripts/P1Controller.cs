using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class P1Controller : MonoBehaviour
{
    [SerializeField] private float  moveSpeed = 50.0f;
    [SerializeField] private Rigidbody          rb;
    private Vector2                 movementInput;
    public float maxDistance = 10f;
    public LayerMask targetLayers;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
    }

    private void Update()
    {
        float   moveX = Input.GetAxisRaw("P1_Horizontal");
        float   moveZ = Input.GetAxisRaw("P1_Vertical");

        movementInput = new Vector2(moveX, moveZ);
        SlapEvent();
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

    void    SlapEvent()
    {
        // Define the origin and direction
        Vector3 origin = transform.position;
        Vector3 direction = transform.forward;

        // Visualize the ray in the Scene view for debugging
        Debug.DrawRay(origin, direction * maxDistance, Color.red);

        // Perform the raycast
        if (Physics.Raycast(origin, direction, out RaycastHit hit, maxDistance, targetLayers))
        {
            Debug.Log($"Hit {hit.collider.gameObject.name} at distance {hit.distance}");
        }
    }
}
