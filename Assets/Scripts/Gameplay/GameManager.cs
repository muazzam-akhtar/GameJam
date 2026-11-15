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

        // controllers / keyboard halves / mice are handed out automatically (InputRouter); each steers its own camera
        Hook(angel.gameObject, 0, angelCamera);
        Hook(evil.gameObject, 1, evilCamera);
        InputRouter.Changed += ShowControls;
        ShowControls();
        BeginRound();
    }

    void OnDestroy() => InputRouter.Changed -= ShowControls;

    static void Hook(GameObject player, int index, FollowCamera view)
    {
        if (!player.TryGetComponent<PlayerInputSource>(out var input)) input = player.AddComponent<PlayerInputSource>();
        input.player = index;
        input.view = view;
    }

    void ShowControls()
    {
        if (!hud) return;
        hud.SetControls(MatchSide.Angel, InputRouter.Describe(0, true, "drop"));
        hud.SetControls(MatchSide.Evil, InputRouter.Describe(1, false, null));
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
                if (InputRouter.AnyRestartPressed(restartKey)) BeginRound();
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
        hud.ShowWinner(Rules.Winner, Rules.Reason, Gamepad.all.Count > 0 ? $"{restartKey} / Start" : restartKey.ToString());
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
