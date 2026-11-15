using UnityEngine;

/// <summary>
/// The hand-slap effect (Art/VFX: SlapVFX_Good for the Keeper, SlapVFX_Evil for the Reaper and the guards), started
/// the instant slap is pressed (<see cref="CharacterActionsBase.Slap"/>) on the hand that actually slaps - the Keeper's
/// left hand, the Reaper's right - and carried by it through the swing: it grows into the hit (~0.38 s into the
/// Slap clip, measured from the animation) and fades out with the follow-through.
/// </summary>
public static class SlapFx
{
    /// <summary>Size relative to the authored prefab: ~10 units long, ~13 at the hit - big, next to a ~30 tall character.</summary>
    public const float Scale = 0.6f;
    /// <summary>Press → wind-up → hit (0.38 s) → follow-through: the effect lives this long.</summary>
    public const float Life = 0.6f;

    // grows into the hit at ~0.38 s of 0.6 s, then eases off
    static readonly AnimationCurve Size = new AnimationCurve(
        new Keyframe(0f, 0.65f), new Keyframe(0.63f, 1.2f), new Keyframe(0.8f, 1.05f), new Keyframe(1f, 0.85f));

    public static void Play(CharacterActionsBase who)
    {
        if (!Application.isPlaying || !who) return;
        var fx = Spawn(who);
        if (fx) Object.Destroy(fx, Life + 0.2f);
    }

    /// <summary>Makes the effect on <paramref name="who"/>'s slapping hand and starts it (also used by previews).</summary>
    public static GameObject Spawn(CharacterActionsBase who)
    {
        var theme = HudTheme.Load();
        bool keeper = who is AngelController;
        var prefab = theme ? (keeper ? theme.slapVfxKeeper : theme.slapVfxReaper) : null;
        if (!prefab) return null;

        var body = who.transform;
        var hand = Find(body, keeper ? "hand.L" : "hand.R");
        var fx = Object.Instantiate(prefab, hand ? hand.position : body.position + body.forward * 12f + Vector3.up * 15f, body.rotation * SlapFxFollow.Aim(keeper));
        fx.transform.localScale = prefab.transform.localScale * Scale;
        if (hand)                                                           // rides with the swing, fingers to the front
        {
            var follow = fx.AddComponent<SlapFxFollow>();
            follow.hand = hand;
            follow.body = body;
            follow.aim = SlapFxFollow.Aim(keeper);
            follow.offset = SlapFxFollow.Offset(keeper);
            follow.Snap();
        }

        foreach (var ps in fx.GetComponentsInChildren<ParticleSystem>())
        {
            if (ps.transform != fx.transform) ps.transform.localPosition = Vector3.zero;   // centred on the hand
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false;
            main.duration = 0.05f;                                           // one hand, right now
            main.startDelay = 0f;
            main.startLifetime = Life;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var grow = ps.sizeOverLifetime;
            grow.enabled = true;
            grow.size = new ParticleSystem.MinMaxCurve(1f, Size);
            ps.Play(false);
        }
        return fx;
    }

    static Transform Find(Transform root, string bone)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == bone) return t;
        return null;
    }
}
