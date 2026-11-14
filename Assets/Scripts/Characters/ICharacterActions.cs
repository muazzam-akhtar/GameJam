/// <summary>
/// What gameplay code can ask a player character (Angel / Evil) to do.
/// Find it with <c>GetComponent&lt;ICharacterActions&gt;()</c> on the character's root.
/// </summary>
public interface ICharacterActions
{
    /// <summary>Plays the slap swing (call on the attacker when a slap lands).</summary>
    void Slap();

    /// <summary>Hit reaction + dizzy stars (call on the one who got slapped).</summary>
    void Stung();

    /// <summary>Power-up effect (call when the character collects a power-up).</summary>
    void PowerUp();

    /// <summary>True while the dizzy stars are up after a hit.</summary>
    bool IsDizzy { get; }
}
