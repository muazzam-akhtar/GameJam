using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The hand-slap effect (Art/VFX: SlapVFX_Good for the Keeper, SlapVFX_Evil for the Reaper and the guards).
/// It starts the instant slap is pressed (<see cref="CharacterActionsBase.Slap"/>) as a glowing glove over the hand
/// that actually slaps - the Keeper's left (hand.L), the Reaper's right (hand.R) - and moves and turns with that hand
/// through the swing (<see cref="SlapFxFollow"/>), so the animation itself is the motion. It grows into the hit
/// (~0.38 s into the Slap clip, measured) and is gone with the follow-through.
/// The prefab's own spin (rotation over lifetime) and drift (velocity over lifetime) were made for a loose, looping
/// effect: on a hand they make it flip over and float away, so they're switched off here.
/// When the slap connects with the other player (<see cref="Landed"/>) the impact burst (SlapBurstVFX: flash, ring,
/// stars) goes off once at the palm at the hit (<see cref="HitTime"/>); a miss gets the glove only.
/// </summary>
public static class SlapFx
{
    /// <summary>
    /// Glove length in world units: about half a character (~30 tall) - the effect's shader only glows at the edges,
    /// so a hand-sized glove would vanish; this one reads clearly and still sits on the hand.
    /// </summary>
    public const float Length = 14f;
    /// <summary>Seconds from the press to gone: wind-up, hit at ~0.38, follow-through.</summary>
    public const float Life = 0.55f;
    /// <summary>When the hand lands, from the press (measured on both Slap clips: fastest at 0.38 s).</summary>
    public const float HitTime = 0.38f;

    // small on the press, full at the hit (0.38 / 0.55 ≈ 0.7), shrinking away after
    static readonly AnimationCurve Grow = new AnimationCurve(
        new Keyframe(0f, 0.55f), new Keyframe(0.69f, 1.15f), new Keyframe(0.85f, 1f), new Keyframe(1f, 0f));

    // each character's glove of its current swing, so the hit test can arm its burst
    static readonly Dictionary<Transform, SlapFxFollow> swings = new Dictionary<Transform, SlapFxFollow>();

    /// <summary>
    /// The slap of <paramref name="slapper"/> connected (its hit test found the other player): its glove sets off the
    /// impact burst at the hit. Called right after the swing started, by the slap code that does the hit test.
    /// </summary>
    public static void Landed(Component slapper)
    {
        if (slapper && swings.TryGetValue(slapper.transform, out var glove) && glove) glove.landed = true;
    }

    public static void Play(CharacterActionsBase who)
    {
        if (!Application.isPlaying || !who) return;
        var fx = Spawn(who);
        if (fx) Object.Destroy(fx, Life + 0.1f);
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
        var fx = Object.Instantiate(prefab);
        fx.transform.localScale = Vector3.one;
        var follow = fx.AddComponent<SlapFxFollow>();
        follow.hand = hand;
        follow.body = body;
        follow.burst = theme.slapBurst;
        follow.burstAt = HitTime;
        swings[body] = follow;
        follow.Snap();

        foreach (var ps in fx.GetComponentsInChildren<ParticleSystem>())
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.transform.localPosition = Vector3.zero;                       // the prefab's 2-unit side offset
            ps.transform.localRotation = Quaternion.identity;                // and its 270° roll: the glove follows the hand
            var main = ps.main;
            main.loop = false;
            main.duration = 0.05f;                                           // one glove, right now
            main.startDelay = 0f;
            main.startLifetime = Life;
            main.startSpeed = 0f;
            main.startRotation = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;      // rides the follower
            main.scalingMode = ParticleSystemScalingMode.Local;
            var mesh = ps.GetComponent<ParticleSystemRenderer>().mesh;
            main.startSize = mesh ? Length / Mathf.Max(0.01f, mesh.bounds.size.z) : 4.5f;
            var spin = ps.rotationOverLifetime; spin.enabled = false;
            var drift = ps.velocityOverLifetime; drift.enabled = false;
            var grow = ps.sizeOverLifetime;
            grow.enabled = true;
            grow.separateAxes = false;
            grow.size = new ParticleSystem.MinMaxCurve(1f, Grow);
            ps.Play(false);
        }
        return fx;
    }

    /// <summary>
    /// The impact burst, once: the prefab's systems loop (they were set up to be watched in a scene), so here they
    /// play a single shot and clean up. The stars spray the way the slapper faces.
    /// </summary>
    public static GameObject Burst(GameObject prefab, Vector3 at, Quaternion facing)
    {
        if (!prefab) return null;
        var fx = Object.Instantiate(prefab, at, facing);
        float longest = 0f;
        foreach (var ps in fx.GetComponentsInChildren<ParticleSystem>())
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false;
            main.startDelay = 0f;
            longest = Mathf.Max(longest, main.startLifetime.constantMax);
            ps.Play(false);
        }
        if (Application.isPlaying) Object.Destroy(fx, longest + 0.3f);
        return fx;
    }

    static Transform Find(Transform root, string bone)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == bone) return t;
        return null;
    }
}
