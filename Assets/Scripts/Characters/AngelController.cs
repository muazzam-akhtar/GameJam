using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Controls the Angel's animations.
/// F toggles flying on/off, Space slaps (works while resting or flying).
/// Call <see cref="SetFlying"/>, <see cref="ToggleFlying"/> or <see cref="Slap"/> from other scripts or UI events.
/// </summary>
[RequireComponent(typeof(Animator))]
public class AngelController : MonoBehaviour
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

    public bool IsFlying { get; private set; }

    public bool IsSlapping
    {
        get
        {
            var current = animator.GetCurrentAnimatorStateInfo(0);
            var next = animator.GetNextAnimatorStateInfo(0);
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
        animator.SetBool(FlyingParam, flying);
    }

    /// <summary>Plays the slap once; ignored while a slap is already playing.</summary>
    public void Slap()
    {
        if (IsSlapping) return;
        animator.SetTrigger(SlapParam);
    }
}
