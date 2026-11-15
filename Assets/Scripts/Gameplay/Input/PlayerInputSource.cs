using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// One player's input for this frame, from whatever <see cref="InputRouter"/> gave them: a controller, a keyboard
/// half, the whole keyboard, and/or a mouse or trackpad. Turns their camera (<see cref="view"/>) and gives movement
/// relative to it, so "forward" is always into the screen.
///
/// Controller: left stick / d-pad move, right stick look, X / RB / RT slap, A / LB action.
/// Keyboard left: WASD, Left Shift slap, E action, Q / R look. Keyboard right: arrows, Right Shift, Right Ctrl, comma / period.
/// Mouse / trackpad: move to look, click slap, right-click action.
/// </summary>
[DefaultExecutionOrder(-50)]
[DisallowMultipleComponent]
public class PlayerInputSource : MonoBehaviour
{
    [Tooltip("0 = player one (left screen), 1 = player two (right screen).")]
    [Range(0, 1)] public int player;
    [Tooltip("The camera this player steers.")]
    public FollowCamera view;

    [Header("Look speed")]
    [Tooltip("Degrees per pixel of mouse / trackpad movement.")]
    public float mouseYaw = 0.14f;
    public float mousePitch = 0.09f;
    [Tooltip("Degrees per second at full right stick.")]
    public float stickYaw = 210f;
    public float stickPitch = 110f;
    [Tooltip("Degrees per second while a look key is held.")]
    public float keyYaw = 150f;

    /// <summary>-1..1 on each axis: x = right, y = forward, both relative to the camera.</summary>
    public Vector2 Move { get; private set; }
    /// <summary>The move as a flat world direction (length 0..1).</summary>
    public Vector3 MoveWorld => ToWorld(Move, view ? view.Yaw : 0f);
    /// <summary>The move came from a stick (free angle) rather than keys (8 directions).</summary>
    public bool Analog { get; private set; }
    public bool SlapPressed { get; private set; }
    public bool ActionPressed { get; private set; }

    void OnEnable() { if (Application.isPlaying) InputRouter.Acquire(); }
    void OnDisable() { if (Application.isPlaying) InputRouter.Release(); }

    void Update() => Poll(Time.deltaTime);

    /// <summary>Camera-relative input → world direction on the ground.</summary>
    public static Vector3 ToWorld(Vector2 move, float yaw) => Quaternion.Euler(0f, yaw, 0f) * new Vector3(move.x, 0f, move.y);

    void Poll(float dt)
    {
        var d = InputRouter.Devices(player);
        var kb = Keyboard.current;
        var pad = d.pad ? InputRouter.PadOf(player) : null;

        Vector2 move = Vector2.zero, look = Vector2.zero;
        bool slap = false, action = false, analog = false;

        if (kb != null)
        {
            if ((d.keys & KeySet.Left) != 0)
            {
                move += Axis(kb.aKey, kb.dKey, kb.sKey, kb.wKey);
                look.x += Held(kb.rKey) - Held(kb.qKey);
                slap |= kb.leftShiftKey.wasPressedThisFrame;
                action |= kb.eKey.wasPressedThisFrame;
            }
            if ((d.keys & KeySet.Right) != 0)
            {
                move += Axis(kb.leftArrowKey, kb.rightArrowKey, kb.downArrowKey, kb.upArrowKey);
                look.x += Held(kb.periodKey) - Held(kb.commaKey);
                slap |= kb.rightShiftKey.wasPressedThisFrame;
                action |= kb.rightCtrlKey.wasPressedThisFrame;
            }
            look.x = Mathf.Clamp(look.x, -1f, 1f) * keyYaw * dt;
        }

        if (pad != null)
        {
            var stick = pad.leftStick.ReadValue();
            if (stick.sqrMagnitude > 0.0001f) analog = true;
            move += stick + pad.dpad.ReadValue();
            var right = pad.rightStick.ReadValue();
            right *= right.magnitude;                            // gentle near the centre, fast at the edge
            look += new Vector2(right.x * stickYaw, -right.y * stickPitch) * dt;
            slap |= pad.buttonWest.wasPressedThisFrame || pad.rightShoulder.wasPressedThisFrame || pad.rightTrigger.wasPressedThisFrame;
            action |= pad.buttonSouth.wasPressedThisFrame || pad.leftShoulder.wasPressedThisFrame;
        }

        if (d.pointer >= 0)
        {
            var delta = InputRouter.PointerDelta(player);
            look += new Vector2(delta.x * mouseYaw, -delta.y * mousePitch);
            slap |= InputRouter.PointerClick(player);
            action |= InputRouter.PointerRightClick(player);
        }

        Move = Vector2.ClampMagnitude(move, 1f);
        Analog = analog;
        SlapPressed = slap;
        ActionPressed = action;
        if (view && look != Vector2.zero) view.Look(look.x, look.y);
    }

    static Vector2 Axis(UnityEngine.InputSystem.Controls.KeyControl left, UnityEngine.InputSystem.Controls.KeyControl right,
                        UnityEngine.InputSystem.Controls.KeyControl back, UnityEngine.InputSystem.Controls.KeyControl forward)
        => new Vector2(Held(right) - Held(left), Held(forward) - Held(back));

    static float Held(UnityEngine.InputSystem.Controls.KeyControl k) => k.isPressed ? 1f : 0f;
}
