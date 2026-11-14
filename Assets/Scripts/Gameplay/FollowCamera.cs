using UnityEngine;

/// <summary>
/// Close third-person camera for one player in split screen: sits behind and above the character, follows with a
/// soft spring, leans a little ahead in the direction they are moving and always looks at them. The camera keeps a
/// fixed heading so WASD / arrow keys always match the screen.
/// </summary>
[DisallowMultipleComponent]
public class FollowCamera : MonoBehaviour
{
    public Transform target;
    [Tooltip("Camera position relative to the character.")]
    public Vector3 offset = new Vector3(0f, 40f, -56f);
    [Tooltip("Height on the character the camera aims at.")]
    public float lookHeight = 14f;
    [SerializeField] float smoothTime = 0.2f;
    [Tooltip("Seconds of movement the camera leans ahead.")]
    [SerializeField] float lookAheadTime = 0.3f;
    [SerializeField] float maxLookAhead = 20f;
    [SerializeField] float lookAheadSmooth = 0.45f;

    Vector3 velocity, ahead, aheadVelocity, lastTarget;
    bool hasLast;

    void OnEnable() => hasLast = false;

    void LateUpdate() => Tick(Time.deltaTime);

    /// <summary>Jumps straight to the resting spot (round start).</summary>
    public void Snap()
    {
        if (!target) return;
        ahead = aheadVelocity = velocity = Vector3.zero;
        lastTarget = target.position;
        hasLast = true;
        transform.position = target.position + offset;
        Aim(Vector3.zero);
    }

    public void Tick(float dt)
    {
        if (!target || dt <= 0f) return;
        var tp = target.position;
        var moved = hasLast ? (tp - lastTarget) / dt : Vector3.zero;
        lastTarget = tp;
        hasLast = true;
        moved.y = 0f;
        var wantAhead = Vector3.ClampMagnitude(moved * lookAheadTime, maxLookAhead);
        ahead = Vector3.SmoothDamp(ahead, wantAhead, ref aheadVelocity, lookAheadSmooth, Mathf.Infinity, dt);
        transform.position = Vector3.SmoothDamp(transform.position, tp + ahead + offset, ref velocity, smoothTime, Mathf.Infinity, dt);
        Aim(ahead * 0.5f);
    }

    void Aim(Vector3 lean)
    {
        var look = target.position + lean + Vector3.up * lookHeight;
        transform.rotation = Quaternion.LookRotation(look - transform.position, Vector3.up);
    }
}
