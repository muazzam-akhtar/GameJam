using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Shared part of the Angel and Evil controllers: the Slap / Stung animator triggers, the dizzy stars,
/// and test keys so each reaction can be tried without playing a round.
/// </summary>
[RequireComponent(typeof(Animator))]
public abstract class CharacterActionsBase : MonoBehaviour, ICharacterActions
{
    protected static readonly int SlapHash = Animator.StringToHash("Slap");
    protected static readonly int StungHash = Animator.StringToHash("Stung");
    protected static readonly int PowerUpHash = Animator.StringToHash("PowerUp");

    Animator animator;
    DizzyStars stars;
    protected Animator Anim => animator ? animator : animator = GetComponent<Animator>();
    protected DizzyStars Stars => stars ? stars : stars = GetComponentInChildren<DizzyStars>(true);

    protected abstract bool TestKeys { get; }
    protected abstract Key SlapKey { get; }
    protected abstract Key StungKey { get; }
    protected abstract Key PowerUpKey { get; }

    public bool IsDizzy => Stars && Stars.Visible;

    protected virtual void Update()
    {
        if (!TestKeys) return;
        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb[SlapKey].wasPressedThisFrame) Slap();
        if (kb[StungKey].wasPressedThisFrame) Stung();
        if (kb[PowerUpKey].wasPressedThisFrame) PowerUp();
    }

    public virtual void Slap()
    {
        Play(SlapHash);
        SlapFx.Play(this);                                   // the hand-slap effect (Keeper: good, Reaper: evil)
    }

    public virtual void Stung()
    {
        Play(StungHash);
        if (Stars) Stars.Show();
    }

    public abstract void PowerUp();

    /// <summary>
    /// Fires a reaction (its trigger and state share a name) unless it is already playing, so a trigger set
    /// mid-reaction can't linger and replay it afterwards.
    /// </summary>
    protected void Play(int reaction)
    {
        var a = Anim;
        if (!a || !a.isActiveAndEnabled || !a.isInitialized) return;
        if (a.GetCurrentAnimatorStateInfo(0).shortNameHash == reaction || a.GetNextAnimatorStateInfo(0).shortNameHash == reaction) return;
        a.SetTrigger(reaction);
    }
}
