using UnityEngine;

/// <summary>
/// Three stars that circle over a character's head while it is dizzy (after being slapped).
/// <see cref="Show"/> pops them in; they orbit, spin and bob for <see cref="duration"/> seconds, then pop out.
/// The ring follows the anchor bone (head) but stays level, so it doesn't tip over with the hit reaction.
/// Lives on a child of the character root, so it scales with the character.
/// </summary>
[DisallowMultipleComponent]
public class DizzyStars : MonoBehaviour
{
    [Header("Placement")]
    [Tooltip("Bone the ring sits over (the head; the Evil has no head bone and uses its body).")]
    [SerializeField] Transform anchor;
    [Tooltip("Ring centre relative to the anchor, in the character's own units and axes.")]
    [SerializeField] Vector3 offset = new Vector3(0f, 0.5f, 0f);

    [Header("Stars")]
    [SerializeField] Transform[] stars = new Transform[0];
    [Tooltip("How long the stars stay up after a hit (seconds).")]
    public float duration = 2.5f;
    [SerializeField] float starSize = 0.3f;
    [SerializeField] float radius = 0.45f;
    [Tooltip("Degrees per second around the ring.")]
    [SerializeField] float orbitSpeed = 220f;
    [Tooltip("Tilt of the ring; negative lifts the front stars so they never cross the face.")]
    [SerializeField] float ringTilt = -10f;
    [Tooltip("0 = stars face outwards from the ring, 1 = always face the camera.")]
    [Range(0f, 1f)] [SerializeField] float faceCamera = 0.8f;
    [Tooltip("Camera the stars turn towards. Empty = the main camera.")]
    public Camera viewCamera;
    [Tooltip("Each star's own spin, degrees per second.")]
    [SerializeField] float spinSpeed = 260f;
    [SerializeField] float bobHeight = 0.05f;
    [SerializeField] float popIn = 0.18f;
    [SerializeField] float popOut = 0.2f;

    float clock = -1f;   // seconds since Show; negative = hidden
    float orbit;

    public bool Visible => clock >= 0f && clock < duration + popOut;

    /// <summary>Pops the stars in, or restarts the timer if they are already up.</summary>
    public void Show()
    {
        if (Visible)
        {
            clock = Mathf.Min(clock, popIn);   // keep them up, restart the countdown
            return;
        }
        clock = 0f;
        SetStarsActive(true);
        Place(0f);
    }

    public void Hide()
    {
        clock = -1f;
        SetStarsActive(false);
    }

    void OnDisable() => Hide();

    void LateUpdate() => Tick(Time.deltaTime);   // after the Animator, so the ring follows this frame's head

    /// <summary>Advances the stars by <paramref name="dt"/> seconds.</summary>
    public void Tick(float dt)
    {
        if (clock < 0f) return;
        clock += dt;
        if (!Visible) { Hide(); return; }
        orbit = Mathf.Repeat(orbit + orbitSpeed * dt, 360f);
        Place(Envelope(clock));
    }

    void Place(float size)
    {
        if (anchor && transform.parent)
        {
            transform.position = anchor.position + transform.parent.TransformVector(offset);
            transform.localRotation = Quaternion.identity;
        }
        var tilt = Quaternion.Euler(ringTilt, 0f, 0f);
        var cam = viewCamera ? viewCamera : Camera.main;
        for (int i = 0; i < stars.Length; i++)
        {
            if (!stars[i]) continue;
            float deg = orbit + i * 360f / stars.Length;
            float rad = deg * Mathf.Deg2Rad;
            var radial = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
            var pos = tilt * (radial * radius);
            pos.y += bobHeight * Mathf.Sin(clock * 7f + i * 2.1f);
            stars[i].localPosition = pos;
            // Mostly face the viewer so the stars read as stars, not slivers, all the way round.
            var face = radial;
            if (cam)
            {
                var toCam = transform.InverseTransformDirection(cam.transform.position - stars[i].position).normalized;
                face = Vector3.Slerp(radial, toCam, faceCamera);
            }
            stars[i].localRotation = Quaternion.LookRotation(face) * Quaternion.Euler(0f, 0f, clock * spinSpeed + i * 40f);
            stars[i].localScale = Vector3.one * (starSize * size);
        }
    }

    float Envelope(float t)
    {
        if (t < popIn) return EaseOutBack(t / popIn);
        if (t < duration) return 1f;
        float x = Mathf.Clamp01((t - duration) / popOut);
        return 1f - x * x;
    }

    static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        float u = x - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;
    }

    void SetStarsActive(bool on)
    {
        foreach (var s in stars)
            if (s) s.gameObject.SetActive(on);
    }
}
