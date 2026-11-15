using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Angel.
///   Idle     flying loop (always playing)
///   Slap     the slap swing - call <see cref="CharacterActionsBase.Slap"/> on the attacker
///   Stung    hit reaction (same as the main character's) + golden dizzy stars - call on the one who got slapped
///   PowerUp  the main character's jump + twirl, with a flare of her holy light
/// Test keys: 1 slap, 2 stung, 3 power-up.
/// </summary>
public class AngelController : CharacterActionsBase
{
    [Header("Test keys (gameplay code calls Slap / Stung / PowerUp)")]
    [SerializeField] bool testKeys = true;
    [SerializeField] Key testSlapKey = Key.Digit1;
    [SerializeField] Key testStungKey = Key.Digit2;
    [SerializeField] Key testPowerUpKey = Key.Digit3;

    protected override bool TestKeys => testKeys;
    protected override Key SlapKey => testSlapKey;
    protected override Key StungKey => testStungKey;
    protected override Key PowerUpKey => testPowerUpKey;

    AngelGlow glow;
    AngelGlow Glow => glow ? glow : glow = GetComponent<AngelGlow>();

    public override void Slap()
    {
        base.Slap();
        if (Glow) Glow.Flare();
    }

    public override void PowerUp()
    {
        Play(PowerUpHash);
        if (Glow) Glow.Flare();
    }
}
