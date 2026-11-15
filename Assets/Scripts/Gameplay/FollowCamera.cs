using UnityEngine;

/// <summary>
/// Close third-person camera for one player in split screen: orbits behind and above the character, follows with a
/// soft spring, leans a little ahead in the direction they are moving and always looks at them.
/// The player turns it (<see cref="Look"/>: mouse, trackpad, right stick or look keys); movement is relative to its
/// heading (<see cref="Yaw"/>). It slides in front of walls so they never block the view.
/// </summary>
[DisallowMultipleComponent]
public class FollowCamera : MonoBehaviour
{
    public Transform target;
    [Tooltip("Camera position relative to the character at the start of a round (sets the distance and the default tilt).")]
    public Vector3 offset = new Vector3(0f, 40f, -56f);
    [Tooltip("Height on the character the camera aims at.")]
    public float lookHeight = 14f;
    [SerializeField] float smoothTime = 0.2f;
    [Tooltip("Seconds of movement the camera leans ahead.")]
    [SerializeField] float lookAheadTime = 0.3f;
    [SerializeField] float maxLookAhead = 20f;
    [SerializeField] float lookAheadSmooth = 0.45f;

    [Header("Look")]
    [Tooltip("Lowest / highest tilt in degrees (0 = level with the character, 90 = straight down).")]
    public float minPitch = 12f;
    public float maxPitch = 68f;

    [Header("Walls")]
    [SerializeField] bool avoidWalls = true;
    [SerializeField] float wallRadius = 3f;
    [SerializeField] LayerMask wallMask = ~0;
    [Tooltip("How fast the camera backs out again once the wall is gone (units per second).")]
    [SerializeField] float wallRecover = 90f;

    /// <summary>Heading in degrees around the up axis (0 = looking along +Z).</summary>
    public float Yaw { get; private set; }
    /// <summary>Tilt in degrees (0 = level, 90 = straight down).</summary>
    public float Pitch { get; private set; }

    Vector3 pivot, pivotVelocity, ahead, aheadVelocity, lastTarget;
    float baseYaw, basePitch, distance, wallDistance;
    bool hasLast, ready;
    readonly RaycastHit[] hits = new RaycastHit[16];

    void OnEnable() => hasLast = false;

    void LateUpdate() => Tick(Time.deltaTime);

    void Init()
    {
        if (ready) return;
        ready = true;
        var flat = new Vector3(-offset.x, 0f, -offset.z);
        baseYaw = flat.sqrMagnitude > 1e-6f ? Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg : 0f;
        basePitch = Mathf.Atan2(offset.y, flat.magnitude) * Mathf.Rad2Deg;
        distance = offset.magnitude;
        Yaw = baseYaw;
        Pitch = basePitch;
        wallDistance = distance;
        if (target) pivot = target.position;
    }

    /// <summary>Turns the camera: <paramref name="yaw"/> degrees to the right, <paramref name="pitch"/> degrees further down.</summary>
    public void Look(float yaw, float pitch)
    {
        Init();
        Yaw = Mathf.Repeat(Yaw + yaw, 360f);
        Pitch = Mathf.Clamp(Pitch + pitch, minPitch, maxPitch);
    }

    /// <summary>Where the camera sits relative to the character at the current heading and tilt (no walls).</summary>
    public Vector3 CurrentOffset { get { Init(); return Quaternion.Euler(Pitch, Yaw, 0f) * new Vector3(0f, 0f, -distance); } }

    /// <summary>Jumps straight to the resting spot behind the character, facing the starting way (round start).</summary>
    public void Snap()
    {
        Init();
        if (!target) return;
        Yaw = baseYaw;
        Pitch = basePitch;
        ahead = aheadVelocity = pivotVelocity = Vector3.zero;
        lastTarget = pivot = target.position;
        hasLast = true;
        wallDistance = WallLimit(distance);
        Place(Vector3.zero);
    }

    public void Tick(float dt)
    {
        Init();
        if (!target || dt <= 0f) return;
        var tp = target.position;
        var moved = hasLast ? (tp - lastTarget) / dt : Vector3.zero;
        lastTarget = tp;
        if (!hasLast) pivot = transform.position - CurrentOffset;    // carry on from wherever the camera is
        hasLast = true;
        moved.y = 0f;
        var wantAhead = Vector3.ClampMagnitude(moved * lookAheadTime, maxLookAhead);
        ahead = Vector3.SmoothDamp(ahead, wantAhead, ref aheadVelocity, lookAheadSmooth, Mathf.Infinity, dt);
        pivot = Vector3.SmoothDamp(pivot, tp + ahead, ref pivotVelocity, smoothTime, Mathf.Infinity, dt);

        // in front of walls at once, back out smoothly
        float limit = WallLimit(distance);
        wallDistance = limit < wallDistance ? limit : Mathf.MoveTowards(wallDistance, limit, wallRecover * dt);
        Place(ahead * 0.5f);
    }

    void Place(Vector3 lean)
    {
        var dir = Quaternion.Euler(Pitch, Yaw, 0f) * Vector3.back;
        var focus = pivot + Vector3.up * lookHeight;
        // orbit the ground point like the original fixed offset did, but never further than the walls allow
        var want = pivot + dir * distance;
        var toWant = want - focus;
        float full = toWant.magnitude;
        float scale = full > 1e-4f ? Mathf.Min(1f, wallDistance / full) : 1f;
        transform.position = focus + toWant * scale;
        var look = target.position + lean + Vector3.up * lookHeight;
        var aim = look - transform.position;
        if (aim.sqrMagnitude > 1e-6f) transform.rotation = Quaternion.LookRotation(aim, Vector3.up);
    }

    /// <summary>How far from the aim point the camera can sit before something solid is in the way.</summary>
    float WallLimit(float wanted)
    {
        var focus = pivot + Vector3.up * lookHeight;
        var want = pivot + Quaternion.Euler(Pitch, Yaw, 0f) * Vector3.back * wanted;
        var ray = want - focus;
        float len = ray.magnitude;
        if (!avoidWalls || len < 1e-3f) return len;
        var physics = gameObject.scene.IsValid() ? gameObject.scene.GetPhysicsScene() : Physics.defaultPhysicsScene;
        int n = physics.SphereCast(focus, wallRadius, ray / len, hits, len, wallMask, QueryTriggerInteraction.Ignore);
        float best = len;
        for (int i = 0; i < n; i++)
        {
            var h = hits[i];
            if (h.distance <= 0f || !h.collider) continue;                               // started inside it
            if (target && h.collider.transform.IsChildOf(target)) continue;              // our own character
            if (h.collider.GetComponentInParent<CharacterActionsBase>()) continue;      // the other one
            best = Mathf.Min(best, h.distance);
        }
        return Mathf.Max(best, wallRadius);
    }
}
