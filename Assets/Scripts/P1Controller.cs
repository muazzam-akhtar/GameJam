using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
public class P1Controller : MonoBehaviour
{
    [SerializeField] private float  moveSpeed = 50.0f;
    [SerializeField] private Rigidbody          rb;
    [SerializeField] private float rotationSpeed = 720f;
    private Vector2                 movementInput;
<<<<<<< Updated upstream
=======
    public float maxDistance = 20f;
    public LayerMask targetLayers;
    [SerializeField] private bool    canSlap;
>>>>>>> Stashed changes

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
        canSlap = true;
    }

    private void Update()
    {
        float   moveX = Input.GetAxisRaw("P1_Horizontal");
        float   moveZ = Input.GetAxisRaw("P1_Vertical");

        movementInput = new Vector2(moveX, moveZ);
        Debug.Log(movementInput);
    }

    private void FixedUpdate()
    {
        MovePlayer();
        RotatePlayer();
    }

    void    MovePlayer()
    {
        Vector3 moveDirection = new Vector3(movementInput.x, 0.0f, movementInput.y).normalized;
        Vector3 targetVelocity = moveDirection * moveSpeed;
        rb.linearVelocity = targetVelocity;
    }
<<<<<<< Updated upstream
=======

    void RotatePlayer()
    {
        if (movementInput.sqrMagnitude < 0.01f) return;

        Vector3 moveDir = new Vector3(movementInput.x, 0.0f, movementInput.y);
        float targetYaw = Mathf.Atan2(moveDir.x, moveDir.z) * Mathf.Rad2Deg;

        targetYaw = Mathf.Round(targetYaw / 45f) * 45f;

        Quaternion targetRot = Quaternion.Euler(0f, targetYaw, 0f);

        Quaternion newRot = Quaternion.RotateTowards(
            rb.rotation,
            targetRot,
            rotationSpeed * Time.fixedDeltaTime
        );
        rb.MoveRotation(newRot);
    }


    IEnumerator ResetSlapCooldown()
    {
        yield return new WaitForSeconds(4.0f);
        canSlap = true;
    }

    void    SlapEvent()
    {
        Vector3 origin = transform.position;
        Vector3 direction = transform.forward;
        origin.y += 20.0f;

        Debug.DrawRay(origin, direction * maxDistance, Color.red);

        if (canSlap && Physics.Raycast(origin, direction, out RaycastHit hit, maxDistance, targetLayers))
        {
            if (hit.collider.gameObject.name == "Evil")
            {
                Debug.Log($"Hit {hit.collider.gameObject.name} at distance {hit.distance}");
                canSlap = false;
                StartCoroutine(ResetSlapCooldown());
            }
        }
    }
>>>>>>> Stashed changes
}
