using UnityEngine;

/// <summary>
/// Close third-person camera for one player in split screen: orbits behind and above the character, follows with a
/// soft spring, leans a little ahead in the direction they are moving and always looks at them.
/// The player turns it (<see cref="Look"/>: mouse, trackpad, right stick or look keys); movement is relative to its
/// heading (<see cref="Yaw"/>). It slides in front of walls so they never block the view, and inside a
/// <see cref="Hideout"/> (a cave) it tilts down over the cut-away roof so the player can see in.
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
    [Tooltip("The wall check starts this high on the character, so low ruins, cover and the border wall never pull the camera in.")]
    [SerializeField] float wallCheckHeight = 24f;
    [Tooltip("Closest the camera comes when backed against a wall.")]
    [SerializeField] float minDistance = 22f;

    [Header("Inside a hideout")]
    [Tooltip("Lowest tilt while inside a cave (looks down through the cut-away roof).")]
    public float indoorPitch = 70f;
    [Tooltip("Distance while inside, relative to the normal one.")]
    [SerializeField] float indoorDistance = 1f;
    [Tooltip("Seconds to ease into / out of the cave view.")]
    [SerializeField] float indoorBlend = 0.6f;
    [Tooltip("Seconds to ease in front of a wall / back out again.")]
    [SerializeField] float wallInTime = 0.08f;
    [SerializeField] float wallOutTime = 0.45f;
    [Tooltip("Layer of the cave roofs (ignored by the wall check while inside).")]
    [SerializeField] string roofLayer = "CaveRoof";

    /// <summary>Heading in degrees around the up axis (0 = looking along +Z).</summary>
    public float Yaw { get; private set; }
    /// <summary>Tilt in degrees (0 = level, 90 = straight down).</summary>
    public float Pitch { get; private set; }
    /// <summary>The cave the character is in (its roof is cut away for this camera), or null.</summary>
    public Hideout Hideout { get; private set; }

    Vector3 pivot, pivotVelocity, ahead, aheadVelocity, lastTarget;
    float baseYaw, basePitch, distance, wallDistance, wallVelocity, indoor, indoorVelocity;
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
        Hideout = Hideout.At(target.position);
        indoor = Hideout ? 1f : 0f;
        indoorVelocity = wallVelocity = 0f;
        wallDistance = WallLimit(Distance);
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

        // inside a cave? (a little slack on the way out so the edge doesn't flicker)
        if (Hideout && !Hideout.Contains(tp, 12f)) Hideout = null;
        if (!Hideout) Hideout = Hideout.At(tp);
        indoor = Mathf.SmoothDamp(indoor, Hideout ? 1f : 0f, ref indoorVelocity, indoorBlend, Mathf.Infinity, dt);

        // ease in front of walls quickly and back out gently (no snapping)
        float limit = WallLimit(Distance);
        wallDistance = Mathf.SmoothDamp(wallDistance, limit, ref wallVelocity, limit < wallDistance ? wallInTime : wallOutTime, Mathf.Infinity, dt);
        Place(ahead * 0.5f);
    }

    /// <summary>Tilt actually used: the player's, pushed down over a cave's cut-away roof while inside.</summary>
    float ViewPitch => Mathf.Lerp(Pitch, Mathf.Max(Pitch, indoorPitch), indoor);
    float Distance => distance * Mathf.Lerp(1f, indoorDistance, indoor);

    void Place(Vector3 lean)
    {
        var dir = Quaternion.Euler(ViewPitch, Yaw, 0f) * Vector3.back;
        var focus = pivot + Vector3.up * Mathf.Max(lookHeight, wallCheckHeight);
        // orbit the ground point like the original fixed offset did, but never further than the walls allow
        var want = pivot + dir * Distance;
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
        var focus = pivot + Vector3.up * Mathf.Max(lookHeight, wallCheckHeight);
        var want = pivot + Quaternion.Euler(ViewPitch, Yaw, 0f) * Vector3.back * wanted;
        var ray = want - focus;
        float len = ray.magnitude;
        if (!avoidWalls || len < 1e-3f) return len;
        var physics = gameObject.scene.IsValid() ? gameObject.scene.GetPhysicsScene() : Physics.defaultPhysicsScene;
        int mask = wallMask;
        if (Hideout)
        {
            int roof = LayerMask.NameToLayer(roofLayer);
            if (roof >= 0) mask &= ~(1 << roof);                                         // we look through it
        }
        int n = physics.SphereCast(focus, wallRadius, ray / len, hits, len, mask, QueryTriggerInteraction.Ignore);
        float best = len;
        for (int i = 0; i < n; i++)
        {
            var h = hits[i];
            if (h.distance <= 0f || !h.collider) continue;                               // started inside it
            if (target && h.collider.transform.IsChildOf(target)) continue;              // our own character
            if (Hideout && h.collider.transform.IsChildOf(Hideout.transform)) continue;  // the cave we're in: we look down into it
            if (h.collider.GetComponentInParent<CharacterActionsBase>()) continue;      // the other one
            best = Mathf.Min(best, h.distance);
        }
        return Mathf.Max(best, Mathf.Min(minDistance, len));
    }
}
