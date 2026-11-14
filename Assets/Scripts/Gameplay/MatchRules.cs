using UnityEngine;

public enum MatchSide { None, Angel, Evil }

/// <summary>
/// The rules of a round, kept free of scene code:
///   the Angel scores when one of her offerings is blessed (survives its bloom time),
///   the Evil scores when it eats an offering,
///   first to <see cref="TargetScore"/> wins, and a player with no lives left loses.
/// </summary>
public sealed class MatchRules
{
    public MatchRules(int targetScore) => TargetScore = Mathf.Max(1, targetScore);

    public int TargetScore { get; }
    public int AngelScore { get; private set; }
    public int EvilScore { get; private set; }
    public MatchSide Winner { get; private set; }
    public string Reason { get; private set; } = "";
    public bool IsOver => Winner != MatchSide.None;

    public void Blessed()
    {
        if (IsOver) return;
        AngelScore++;
        if (AngelScore >= TargetScore) End(MatchSide.Angel, $"{TargetScore} offerings blessed");
    }

    public void Eaten()
    {
        if (IsOver) return;
        EvilScore++;
        if (EvilScore >= TargetScore) End(MatchSide.Evil, $"{TargetScore} offerings devoured");
    }

    public void LivesChanged(int angelLives, int evilLives)
    {
        if (IsOver) return;
        if (angelLives <= 0) End(MatchSide.Evil, "the Angel is out of lives");
        else if (evilLives <= 0) End(MatchSide.Angel, "the Evil is out of lives");
    }

    void End(MatchSide winner, string reason)
    {
        Winner = winner;
        Reason = reason;
    }
}
