using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

/// <summary>
/// Angel.
///   Idle   flying loop (always playing)
///   Stung  Space - the stung/slap move, then back to Idle
/// Game code can call <see cref="Stung"/>.
/// </summary>
[RequireComponent(typeof(Animator))]
public class AngelController : MonoBehaviour
{
    [Header("Keys")]
    [FormerlySerializedAs("slapKey")]
    [SerializeField] Key stungKey = Key.Space;

    static readonly int StungTrigger = Animator.StringToHash("Stung");
    static readonly int StungState = Animator.StringToHash("Stung");

    Animator animator;
    Animator Anim => animator != null ? animator : animator = GetComponent<Animator>();

    public bool IsStunging
    {
        get
        {
            var current = Anim.GetCurrentAnimatorStateInfo(0);
            var next = Anim.GetNextAnimatorStateInfo(0);
            return current.shortNameHash == StungState || next.shortNameHash == StungState;
        }
    }

    void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard[stungKey].wasPressedThisFrame) Stung();
    }

    /// <summary>Plays the stung move once; ignored while it is already playing.</summary>
    public void Stung()
    {
        if (IsStunging) return;
        Anim.SetTrigger(StungTrigger);
    }
}
