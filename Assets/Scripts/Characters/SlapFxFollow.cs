using UnityEngine;

/// <summary>
/// Keeps a slap effect on the slapping hand through the swing (its position follows the hand bone) while the hand
/// mesh keeps pointing where the character faces - fingers towards whoever is being slapped.
/// </summary>
public class SlapFxFollow : MonoBehaviour
{
    public Transform hand, body;
    public Quaternion aim = Quaternion.identity;
    [Tooltip("Nudge from the hand bone in the body's space: out to the slapping side and a little forward, clear of the character's glow.")]
    public Vector3 offset;

    public static Vector3 Offset(bool leftHand) => new Vector3(leftHand ? -8f : 8f, 2f, 5f);

    /// <summary>
    /// The hand mesh is authored pointing along its -x: this turns it forward and 40° out to the slapping side
    /// (left hand: forward-left, right hand: forward-right) - a sideways slap that reads from the camera behind.
    /// </summary>
    public static Quaternion Aim(bool leftHand) => Quaternion.Euler(0f, leftHand ? 50f : 130f, 0f);

    void LateUpdate() => Snap();

    public void Snap()
    {
        if (!hand || !body) return;
        transform.SetPositionAndRotation(hand.position + body.rotation * offset, body.rotation * aim);
    }
}
