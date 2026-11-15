using UnityEngine;

public enum MatchSide { None, Angel, Evil }

/// <summary>
/// The rules of a round (on screen the Angel is the Keeper, the Evil the Reaper and the offerings are souls), kept free of scene code (hide and seek):
///   the Angel carries <see cref="Offerings"/> offerings and must hide every one of them (they never expire);
///   the Evil wins by devouring them all before the hunt timer runs out;
///   when the time is up (<see cref="TimeUp"/>) the Angel wins only if she hid them all and some survived -
///   any still in her pouch and the Evil wins;
///   a player with no lives left loses.
/// </summary>
public sealed class MatchRules
{
    public MatchRules(int offerings) => Offerings = Mathf.Max(1, offerings);

    /// <summary>How many offerings the Angel has to hide (= how many the Evil must devour).</summary>
    public int Offerings { get; }
    public int Eaten { get; private set; }
    public int Remaining => Offerings - Eaten;
    public MatchSide Winner { get; private set; }
    public string Reason { get; private set; } = "";
    public bool IsOver => Winner != MatchSide.None;

    public void Devoured()
    {
        if (IsOver) return;
        Eaten = Mathf.Min(Offerings, Eaten + 1);
        if (Eaten >= Offerings) End(MatchSide.Evil, $"all {Offerings} souls taken");
    }

    /// <summary>The hunt timer ran out; <paramref name="stillInPouch"/> = offerings the Angel never hid.</summary>
    public void TimeUp(int stillInPouch)
    {
        if (IsOver) return;
        if (stillInPouch > 0)
            End(MatchSide.Evil, stillInPouch == 1 ? "the Keeper didn't hide the last soul" : $"the Keeper didn't hide {stillInPouch} souls");
        else
            End(MatchSide.Angel, Remaining == 1 ? "one soul survived the hunt" : $"{Remaining} souls survived the hunt");
    }

    public void LivesChanged(int angelLives, int evilLives)
    {
        if (IsOver) return;
        if (angelLives <= 0) End(MatchSide.Evil, "the Keeper is out of lives");
        else if (evilLives <= 0) End(MatchSide.Angel, "the Reaper is out of lives");
    }

    void End(MatchSide winner, string reason)
    {
        Winner = winner;
        Reason = reason;
    }
}
