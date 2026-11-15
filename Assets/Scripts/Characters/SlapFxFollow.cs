using UnityEngine;

/// <summary>
/// Holds a slap glove on the slapping hand every frame: the glove's wrist on the hand bone, its fingers along the
/// bone (the rig's hand bones point their up axis down the fingers), its palm turned to the slapping side - so it
/// swings, turns and lands exactly with the animated hand. Without a hand bone it waits in front of the body.
/// If the slap connected (<see cref="landed"/>), <see cref="burstAt"/> seconds in (the hit) it sets off the impact
/// burst at the palm, once; a miss gets no burst.
/// </summary>
public class SlapFxFollow : MonoBehaviour
{
    public Transform hand, body;
    public GameObject burst;
    public float burstAt;
    /// <summary>The slap hit the other player (set by <see cref="SlapFx.Landed"/>).</summary>
    public bool landed;

    float age;
    bool burstDone;

    void LateUpdate()
    {
        Snap();
        age += Time.deltaTime;
        if (burstDone || !landed || !burst || age < burstAt) return;
        burstDone = true;
        SlapFx.Burst(burst, Palm, body ? body.rotation : transform.rotation);
    }

    /// <summary>The middle of the glove, where the slap lands.</summary>
    public Vector3 Palm => transform.position + transform.forward * (SlapFx.Length * 0.45f);

    public void Snap()
    {
        if (!body) return;
        if (!hand)
        {
            transform.SetPositionAndRotation(body.position + body.forward * 12f + Vector3.up * 15f, body.rotation);
            return;
        }
        var fingers = hand.up;                                              // the glove mesh points its fingers along +z
        var side = Vector3.Dot(hand.position - body.position, body.right) < 0f ? body.right : -body.right;   // palm towards the target
        if (Mathf.Abs(Vector3.Dot(fingers, side)) > 0.95f) side = body.up;
        transform.SetPositionAndRotation(hand.position, Quaternion.LookRotation(fingers, side));
    }
}
