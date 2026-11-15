using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Evil (lantern ghost).
///   Idle     flying loop - flames float, lantern sways (always playing)
///   Slap     the lantern slap swing - call <see cref="CharacterActionsBase.Slap"/> on the attacker
///   Stung    hit reaction (same as the main character's) + purple dizzy stars - call on the one who got slapped
///   PowerUp  the main character's jump + twirl, and the flames grow and burn brighter with a fiery glow,
///            rising fire + embers (<see cref="EvilPower"/>)
/// Test keys: 8 slap, 9 stung, 0 power-up.
/// </summary>
public class EvilController : CharacterActionsBase
{
    [Header("Test keys (gameplay code calls Slap / Stung / PowerUp)")]
    [SerializeField] bool testKeys = true;
    [SerializeField] Key testSlapKey = Key.Digit8;
    [SerializeField] Key testStungKey = Key.Digit9;
    [SerializeField] Key testPowerUpKey = Key.Digit0;

    protected override bool TestKeys => testKeys;
    protected override Key SlapKey => testSlapKey;
    protected override Key StungKey => testStungKey;
    protected override Key PowerUpKey => testPowerUpKey;

    EvilPower power;
    EvilPower Power => power ? power : power = GetComponent<EvilPower>();

    public override void PowerUp()
    {
        Play(PowerUpHash);
        if (Power) Power.Activate();
    }
}
