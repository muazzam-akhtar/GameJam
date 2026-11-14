using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Runs a round of Angel vs Evil:
///   countdown (controls locked) → play → someone wins (first to the target score, or the other is out of lives)
///   → press R to play again (instant reset, no scene reload).
/// Also sets the players' bodies up for smooth split-screen play: no gravity, height locked, interpolated movement.
/// </summary>
public class GameManager : MonoBehaviour
{
    public enum Phase { Countdown, Playing, Over }

    [SerializeField] AngelController angel;
    [SerializeField] EvilController evil;
    [SerializeField] FollowCamera angelCamera;
    [SerializeField] FollowCamera evilCamera;
    [SerializeField] GameHUD hud;
    [SerializeField] ScoreManager scoreManager;
    [Tooltip("Parent of the dropped offerings (cleared on restart).")]
    [SerializeField] Transform offerings;
    [Tooltip("Blessed (Angel) or devoured (Evil) offerings needed to win.")]
    [SerializeField] int targetScore = 8;
    [SerializeField] float countdownTime = 3f;
    [SerializeField] Key restartKey = Key.R;

    public MatchRules Rules { get; private set; }
    public Phase Current { get; private set; }

    PlayerLives angelLives, evilLives;
    AngelDropper dropper;
    Behaviour angelMove, evilMove;
    Rigidbody angelBody, evilBody;
    Vector3 angelStart, evilStart;
    Quaternion angelStartRot, evilStartRot;
    float timer;

    void Start()
    {
        angelLives = angel.GetComponent<PlayerLives>();
        evilLives = evil.GetComponent<PlayerLives>();
        dropper = angel.GetComponent<AngelDropper>();
        angelMove = angel.GetComponent<P1Controller>();
        evilMove = evil.GetComponent<P2Controller>();
        angelBody = angel.GetComponent<Rigidbody>();
        evilBody = evil.GetComponent<Rigidbody>();
        foreach (var body in new[] { angelBody, evilBody })
        {
            body.useGravity = false;
            body.constraints |= RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.position = new Vector3(body.position.x, 0f, body.position.z);
        }
        angelStart = angel.transform.position; angelStartRot = angel.transform.rotation;
        evilStart = evil.transform.position; evilStartRot = evil.transform.rotation;

        angelLives.Changed += _ => OnLivesChanged();
        evilLives.Changed += _ => OnLivesChanged();
        if (dropper) dropper.Dropped += OnDropped;
        BeginRound();
    }

    void BeginRound()
    {
        if (offerings)
            for (int i = offerings.childCount - 1; i >= 0; i--) Destroy(offerings.GetChild(i).gameObject);
        Rules = new MatchRules(targetScore);
        SyncScores();
        Place(angelBody, angelStart, angelStartRot);
        Place(evilBody, evilStart, evilStartRot);
        angelLives.ResetLives();
        evilLives.ResetLives();
        if (angelCamera) angelCamera.Snap();
        if (evilCamera) evilCamera.Snap();
        SetControls(false);
        Current = Phase.Countdown;
        timer = countdownTime;
    }

    void Update()
    {
        switch (Current)
        {
            case Phase.Countdown:
                timer -= Time.deltaTime;
                if (timer > 0f) hud.ShowCountdown(Mathf.CeilToInt(timer));
                else
                {
                    Current = Phase.Playing;
                    SetControls(true);
                    hud.ShowGo();
                }
                break;
            case Phase.Over:
                var kb = Keyboard.current;
                if (kb != null && kb[restartKey].wasPressedThisFrame) BeginRound();
                break;
        }
        if (dropper) hud.SetOfferings(dropper.ActiveCount, dropper.maxActive, dropper.CooldownLeft <= 0f);
    }

    void OnDropped(Offering o)
    {
        if (offerings) o.transform.SetParent(offerings, true);
        o.Blessed += _ =>
        {
            if (Current != Phase.Playing) return;
            Rules.Blessed();
            angel.PowerUp();                       // her offering ripened: a happy twirl
            SyncScores();
            CheckOver();
        };
        o.Eaten += _ =>
        {
            if (Current != Phase.Playing) return;
            Rules.Eaten();                         // the offering itself powers the Evil up
            SyncScores();
            CheckOver();
        };
    }

    void OnLivesChanged()
    {
        hud.SetLives(MatchSide.Angel, angelLives.Lives);
        hud.SetLives(MatchSide.Evil, evilLives.Lives);
        if (Current != Phase.Playing) return;
        Rules.LivesChanged(angelLives.Lives, evilLives.Lives);
        CheckOver();
    }

    void CheckOver()
    {
        if (!Rules.IsOver || Current == Phase.Over) return;
        Current = Phase.Over;
        SetControls(false);
        if (Rules.Winner == MatchSide.Angel) { angel.PowerUp(); evil.Stung(); }
        else { evil.PowerUp(); angel.Stung(); }
        hud.ShowWinner(Rules.Winner, Rules.Reason, restartKey.ToString());
    }

    void SyncScores()
    {
        if (scoreManager)
        {
            scoreManager.AngelScore = Rules.AngelScore;
            scoreManager.EvilScore = Rules.EvilScore;
        }
        hud.SetScore(MatchSide.Angel, Rules.AngelScore, Rules.TargetScore);
        hud.SetScore(MatchSide.Evil, Rules.EvilScore, Rules.TargetScore);
    }

    void SetControls(bool on)
    {
        if (angelMove) angelMove.enabled = on;
        if (evilMove) evilMove.enabled = on;
        if (dropper) dropper.enabled = on;
        foreach (var body in new[] { angelBody, evilBody })
            if (body) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
    }

    static void Place(Rigidbody body, Vector3 pos, Quaternion rot)
    {
        body.position = pos;
        body.rotation = rot;
        body.transform.SetPositionAndRotation(pos, rot);
        body.linearVelocity = Vector3.zero;
    }
}
