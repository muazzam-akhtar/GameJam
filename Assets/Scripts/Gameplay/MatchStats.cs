using UnityEngine;

/// <summary>
/// What the result screen shows besides the score: slaps landed, times stunned (slaps taken) and the best soul streak -
/// the most souls the Keeper hid / the Reaper took in a row without getting slapped in between.
/// </summary>
public sealed class MatchStats
{
    public int KeeperSlaps { get; private set; }
    public int ReaperSlaps { get; private set; }
    public int KeeperStunned { get; private set; }
    public int ReaperStunned { get; private set; }
    public int KeeperStreak { get; private set; }
    public int ReaperStreak { get; private set; }

    int keeperRun, reaperRun;

    /// <summary>The Keeper put a soul down.</summary>
    public void SoulHidden() => KeeperStreak = Mathf.Max(KeeperStreak, ++keeperRun);

    /// <summary>The Reaper took one.</summary>
    public void SoulTaken() => ReaperStreak = Mathf.Max(ReaperStreak, ++reaperRun);

    /// <summary>A slap landed on <paramref name="victim"/>: it counts for the other side and ends the victim's run.</summary>
    public void Slapped(MatchSide victim)
    {
        if (victim == MatchSide.Angel) { KeeperStunned++; ReaperSlaps++; keeperRun = 0; }
        else if (victim == MatchSide.Evil) { ReaperStunned++; KeeperSlaps++; reaperRun = 0; }
    }
}

/// <summary>Everything the result screen needs about a finished round.</summary>
public struct MatchResult
{
    public MatchSide winner;
    /// <summary>Why it ended, e.g. "3 souls survived the hunt".</summary>
    public string reason;
    /// <summary>The hunt timer ran out ("TIME!").</summary>
    public bool timeUp;
    /// <summary>Souls still safe (the Keeper's score) and souls taken (the Reaper's), out of <see cref="total"/>.</summary>
    public int saved, taken, total;
    public int keeperSlaps, reaperSlaps, keeperStunned, reaperStunned, keeperStreak, reaperStreak;
    /// <summary>How long the hunt lasted.</summary>
    public float huntSeconds;

    /// <param name="stillInPouch">Souls the Keeper never hid: not taken, but not saved either.</param>
    public static MatchResult From(MatchRules rules, MatchStats stats, bool timeUp, float huntSeconds, int stillInPouch = 0) => new MatchResult
    {
        winner = rules.Winner,
        reason = rules.Reason,
        timeUp = timeUp,
        saved = Mathf.Max(0, rules.Remaining - stillInPouch),
        taken = rules.Eaten,
        total = rules.Offerings,
        keeperSlaps = stats.KeeperSlaps,
        reaperSlaps = stats.ReaperSlaps,
        keeperStunned = stats.KeeperStunned,
        reaperStunned = stats.ReaperStunned,
        keeperStreak = stats.KeeperStreak,
        reaperStreak = stats.ReaperStreak,
        huntSeconds = huntSeconds,
    };
}
