using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Controls the Evil lantern ghost's animations.
/// F toggles flying on/off (flames float around him, the lantern sways); Space slaps with the lantern
/// (works while resting or flying, then goes back to whatever he was doing).
/// Call <see cref="SetFlying"/>, <see cref="ToggleFlying"/> or <see cref="Slap"/> from other scripts or UI events.
/// </summary>
[RequireComponent(typeof(Animator))]
public class EvilController : MonoBehaviour
{
    [Header("Keys")]
    [SerializeField] Key flyKey = Key.F;
    [SerializeField] Key slapKey = Key.Space;

    [Header("Start")]
    [Tooltip("Start the scene already flying instead of resting.")]
    [SerializeField] bool startFlying;

    static readonly int FlyingParam = Animator.StringToHash("Flying");
    static readonly int SlapParam = Animator.StringToHash("Slap");
    static readonly int SlapState = Animator.StringToHash("Slap");

    Animator animator;

    // Looked up on first use, so the public methods are safe even before this object's Awake.
    Animator Anim => animator != null ? animator : animator = GetComponent<Animator>();

    public bool IsFlying { get; private set; }

    public bool IsSlapping
    {
        get
        {
            var current = Anim.GetCurrentAnimatorStateInfo(0);
            var next = Anim.GetNextAnimatorStateInfo(0);
            return current.shortNameHash == SlapState || next.shortNameHash == SlapState;
        }
    }

    void Awake()
    {
        animator = GetComponent<Animator>();
        SetFlying(startFlying);
    }

    void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard[flyKey].wasPressedThisFrame) ToggleFlying();
        if (keyboard[slapKey].wasPressedThisFrame) Slap();
    }

    public void ToggleFlying() => SetFlying(!IsFlying);

    public void SetFlying(bool flying)
    {
        IsFlying = flying;
        Anim.SetBool(FlyingParam, flying);
    }

    /// <summary>Plays the lantern slap once; ignored while a slap is already playing.</summary>
    public void Slap()
    {
        if (IsSlapping) return;
        Anim.SetTrigger(SlapParam);
    }
}
