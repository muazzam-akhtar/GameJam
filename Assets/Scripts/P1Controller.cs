using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using UnityEngine.UI;

[RequireComponent(typeof(Rigidbody))]
public class P1Controller : MonoBehaviour
{
    [SerializeField] private float  moveSpeed = 50.0f;
    [SerializeField] private Rigidbody          rb;
    [SerializeField] private float rotationSpeed = 720f;
    private Vector2                 movementInput;
    private PlayerInputSource       input;
    public float maxDistance = 20f;
    public LayerMask targetLayers;
    [SerializeField] private bool    canSlap;
    [Tooltip("Slap button. P1 = Left Shift (next to WASD).")]
    [SerializeField] private Key     slapKey = Key.LeftShift;
    [SerializeField] private Image _slapIcon;
    public GameManager              _gameManager;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
        canSlap = true;
        input = GetComponent<PlayerInputSource>();
    }

    private void Update()
    {
        if (!input) input = GetComponent<PlayerInputSource>();
        if (input)
        {
            // keyboard, controller or both, relative to this player's camera (PlayerInputSource / InputRouter)
            var world = input.MoveWorld;
            movementInput = new Vector2(world.x, world.z);
            if (input.SlapPressed) SlapEvent();
            return;
        }

        float   moveX = Input.GetAxisRaw("P1_Horizontal");
        float   moveZ = Input.GetAxisRaw("P1_Vertical");

        movementInput = new Vector2(moveX, moveZ);

        // slap only when the slap button is pressed (no more automatic slapping)
        var keyboard = Keyboard.current;
        if (keyboard != null && slapKey != Key.None && keyboard[slapKey].wasPressedThisFrame)
            SlapEvent();
    }

    private void FixedUpdate()
    {
        MovePlayer();
        RotatePlayer();
    }

    /// <summary>The slap is off cooldown (the HUD's slap icon lights up).</summary>
    public bool SlapReady => canSlap;

    void    MovePlayer()
    {
        Vector3 moveDirection = new Vector3(movementInput.x, 0.0f, movementInput.y).normalized;
        Vector3 targetVelocity = moveDirection * moveSpeed;
        rb.linearVelocity = targetVelocity;
    }

    void RotatePlayer()
    {
        if (movementInput.sqrMagnitude < 0.01f) return;

        Vector3 moveDir = new Vector3(movementInput.x, 0.0f, movementInput.y);
        float targetYaw = Mathf.Atan2(moveDir.x, moveDir.z) * Mathf.Rad2Deg;

        // keys snap the facing to 8 directions; with a camera that turns (input source) the move already points
        // exactly where the player means, so face it as-is
        if (!input) targetYaw = Mathf.Round(targetYaw / 45f) * 45f;

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
        SetAlpha(1.0f);
    }

    void    SlapEvent()
    {
        if (!canSlap) return;
        // the slapper always swings; it only lands if the other player is in front and in range
        if (TryGetComponent<ICharacterActions>(out var me)) me.Slap();

        Vector3 origin = transform.position;
        Vector3 direction = transform.forward;
        origin.y += 20.0f;

        Debug.DrawRay(origin, direction * maxDistance, Color.red);

        if (Physics.Raycast(origin, direction, out RaycastHit hit, maxDistance, targetLayers))
        {
            if (hit.collider.gameObject.name == "Evil")
            {
                Debug.Log($"Hit {hit.collider.gameObject.name} at distance {hit.distance}");
                canSlap = false;
                SetAlpha(0.5f);
                StartCoroutine(ResetSlapCooldown());
                _gameManager.PlaySlapSound();
                // the one who got hit flinches and sees stars
                var victim = hit.collider.GetComponentInParent<ICharacterActions>();
                if (victim != null) victim.Stung();
                SlapFx.Landed(this);                    // it connected: the impact burst goes off at the hit
                var victimLives = hit.collider.GetComponentInParent<PlayerLives>();
                if (victimLives != null) victimLives.LoseLife();
                _gameManager.PlayStunSound();
            }
        }
    }

    private void SetAlpha(float alphaValue)
    {
        if (!_slapIcon) return;                 // not wired in every scene (the HUD shows slap state too)
        Color currentColor = _slapIcon.color;
        Debug.Log("Color Before: " + _slapIcon.color);
        currentColor.a = alphaValue;
        _slapIcon.color = currentColor;
        Debug.Log("Color Before: " + _slapIcon.color);
    }
}
