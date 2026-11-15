using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// A covered hiding spot (a small cave). Its inside is a box in local space. When a player is inside, their own
/// camera sees it cut away: the roof is drawn as shadow only (the inside stays shaded, nothing blocks the view) and
/// the camera tilts down over it (<see cref="FollowCamera"/>). The other player's camera still sees a solid roof,
/// so offerings tucked inside are only found by going in.
/// </summary>
[DisallowMultipleComponent]
public class Hideout : MonoBehaviour
{
    [Tooltip("Centre of the inside, local space.")]
    public Vector3 center = new Vector3(0f, 22f, 0f);
    [Tooltip("Size of the inside, local space.")]
    public Vector3 size = new Vector3(60f, 44f, 60f);
    [Tooltip("Roof renderers: shadow-only for a camera whose player is inside.")]
    public Renderer[] roof = new Renderer[0];

    static readonly List<Hideout> all = new List<Hideout>();
    public static IReadOnlyList<Hideout> All => all;

    void OnEnable()
    {
        if (!all.Contains(this)) all.Add(this);
        Hook();
    }

    void OnDisable() => all.Remove(this);

    /// <summary>Is the point inside (grown by <paramref name="margin"/> on every side)?</summary>
    public bool Contains(Vector3 world, float margin = 0f)
    {
        var l = transform.InverseTransformPoint(world) - center;
        return Mathf.Abs(l.x) <= size.x * 0.5f + margin && Mathf.Abs(l.y) <= size.y * 0.5f + margin && Mathf.Abs(l.z) <= size.z * 0.5f + margin;
    }

    /// <summary>The hideout the point is in, or null.</summary>
    public static Hideout At(Vector3 world, float margin = 0f)
    {
        foreach (var h in all)
            if (h && h.Contains(world, margin)) return h;
        return null;
    }

    // ---------------------------------------------------------------- per-camera roof cutaway

    static bool hooked;

    static void Hook()
    {
        if (hooked) return;
        hooked = true;
        RenderPipelineManager.beginCameraRendering += BeginCamera;
    }

    static void BeginCamera(ScriptableRenderContext context, Camera cam)
    {
        var inside = cam.TryGetComponent<FollowCamera>(out var follow) ? follow.Hideout : null;
        foreach (var h in all)
        {
            if (!h) continue;
            var mode = h == inside ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
            foreach (var r in h.roof)
                if (r && r.shadowCastingMode != mode) r.shadowCastingMode = mode;
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.5f);
        Gizmos.DrawWireCube(center, size);
    }
}
