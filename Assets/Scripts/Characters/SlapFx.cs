using UnityEngine;

/// <summary>
/// The hand-slap effect (Art/VFX: SlapVFX_Good for the Keeper, SlapVFX_Evil for the Reaper and the guards), played
/// in front of whoever swings, on every slap (<see cref="CharacterActionsBase.Slap"/>). The prefabs loop when dropped
/// in a scene; here each swing gets one short burst that cleans itself up.
/// </summary>
public static class SlapFx
{
    /// <summary>How big the hand is, relative to the authored prefab.</summary>
    public const float Scale = 1.8f;
    const float Burst = 0.18f, Life = 0.9f;

    public static void Play(CharacterActionsBase who)
    {
        if (!Application.isPlaying || !who) return;
        var theme = HudTheme.Load();
        var prefab = theme ? (who is AngelController ? theme.slapVfxKeeper : theme.slapVfxReaper) : null;
        if (!prefab) return;
        var t = who.transform;
        var fx = Object.Instantiate(prefab, t.position + t.forward * 16f + Vector3.up * 15f, t.rotation);
        fx.transform.localScale = prefab.transform.localScale * Scale;
        foreach (var ps in fx.GetComponentsInChildren<ParticleSystem>())
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false;
            main.duration = Burst;                                  // one quick flurry of hands, not a stream
            main.startLifetime = 0.32f;                             // long enough to read, still a snap
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;   // so Scale grows the hands too
            ps.Play(false);
        }
        Object.Destroy(fx, Life);
    }
}
