using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Runs a round of hide and seek:
///   head start - the Angel has <see cref="headStart"/> seconds to fly off and hide her offerings while the Evil is
///                frozen and blindfolded (and can't be hurt);
///   hunt       - <see cref="huntTime"/> seconds for the Evil to find and devour every offering; the Angel must
///                hide all of hers by then;
///   over       - the Evil wins by devouring them all or if the Angel didn't hide them all in time, the Angel when
///                she hid them all and some survived the clock, or whoever still has lives
///                → R / Start to play again (instant reset, no scene reload).
/// Also sets the players' bodies up for smooth split-screen play (no gravity, height locked, interpolated) and turns
/// on each half's look (<see cref="SplitViewFX"/>: the Evil's world is dark, lit by his lantern).
/// </summary>
public class GameManager : MonoBehaviour
{
    public enum Phase { HeadStart, Hunt, Over }

    [SerializeField] AngelController angel;
    [SerializeField] EvilController evil;
    [SerializeField] FollowCamera angelCamera;
    [SerializeField] FollowCamera evilCamera;
    [SerializeField] GameHUD hud;
    [SerializeField] ScoreManager scoreManager;
    [Tooltip("Parent of the dropped offerings (cleared on restart).")]
    [SerializeField] Transform offerings;
    [Tooltip("Offerings the Angel hides = offerings the Evil must devour to win.")]
    [SerializeField] int offeringCount = 8;
    [Tooltip("Seconds the Angel gets to hide before the Evil may move.")]
    [SerializeField] float headStart = 15f;
    [Tooltip("Seconds the Evil has to find them all.")]
    [SerializeField] float huntTime = 120f;
    [SerializeField] Key restartKey = Key.R;

    public MatchRules Rules { get; private set; }
    public Phase Current { get; private set; }
    /// <summary>Seconds left in the current phase.</summary>
    public float TimeLeft { get; private set; }

    PlayerLives angelLives, evilLives;
    AngelDropper dropper;
    Behaviour angelMove, evilMove;
    Rigidbody angelBody, evilBody;
    Vector3 angelStart, evilStart;
    Quaternion angelStartRot, evilStartRot;

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
        if (dropper)
        {
            dropper.pouchSize = offeringCount;
            dropper.Dropped += OnDropped;
        }

        // controllers / keyboard halves / mice are handed out automatically (InputRouter); each steers its own camera
        Hook(angel.gameObject, 0, angelCamera);
        Hook(evil.gameObject, 1, evilCamera);
        InputRouter.Changed += ShowControls;
        ShowControls();

        SplitViewFX.Install(gameObject, angel, evil, angelCamera ? angelCamera.GetComponent<Camera>() : null, evilCamera ? evilCamera.GetComponent<Camera>() : null);
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
        hud.SetControls(MatchSide.Angel, InputRouter.Describe(0, true, "hide offering"));
        hud.SetControls(MatchSide.Evil, InputRouter.Describe(1, false, null));
    }

    void BeginRound()
    {
        if (offerings)
            for (int i = offerings.childCount - 1; i >= 0; i--) Destroy(offerings.GetChild(i).gameObject);
        Rules = new MatchRules(offeringCount);
        if (dropper) dropper.Refill();
        Place(angelBody, angelStart, angelStartRot);
        Place(evilBody, evilStart, evilStartRot);
        evilLives.Invulnerable = true;                 // frozen and blind: no free slaps
        angelLives.ResetLives();
        evilLives.ResetLives();
        if (angelCamera) angelCamera.Snap();
        if (evilCamera) evilCamera.Snap();

        Current = Phase.HeadStart;
        TimeLeft = headStart;
        if (angelMove) angelMove.enabled = true;
        if (dropper) dropper.enabled = true;
        if (evilMove) evilMove.enabled = false;
        Freeze(evilBody, true);
        hud.HideCenter();
        hud.ShowHeadStart(TimeLeft);
        hud.SetTimer(huntTime, false);
        SyncScores();
    }

    void Update()
    {
        switch (Current)
        {
            case Phase.HeadStart:
                TimeLeft -= Time.deltaTime;
                if (TimeLeft > 0f) { hud.ShowHeadStart(TimeLeft); break; }
                Current = Phase.Hunt;
                TimeLeft = huntTime;
                evilLives.Invulnerable = false;
                Freeze(evilBody, false);
                if (evilMove) evilMove.enabled = true;
                hud.StartHunt();
                evil.PowerUp();                         // he wakes up hungry
                break;
            case Phase.Hunt:
                TimeLeft -= Time.deltaTime;
                hud.SetTimer(Mathf.Max(0f, TimeLeft), TimeLeft <= 15f);
                if (TimeLeft <= 0f)
                {
                    Rules.TimeUp(dropper ? dropper.Carried : 0);
                    CheckOver();
                }
                break;
            case Phase.Over:
                if (InputRouter.AnyRestartPressed(restartKey)) BeginRound();
                break;
        }
        if (Current != Phase.Over) SyncScores();
    }

    void OnDropped(Offering o)
    {
        if (offerings) o.transform.SetParent(offerings, true);
        angel.PowerUp();                               // hiding one is her power-up: a happy twirl and a flare of light
        o.Eaten += _ =>
        {
            if (Current == Phase.Over) return;
            Rules.Devoured();                          // the offering itself powers the Evil up
            hud.Devoured(Rules.Eaten, Rules.Offerings);
            SyncScores();
            CheckOver();
        };
    }

    void OnLivesChanged()
    {
        hud.SetLives(MatchSide.Angel, angelLives.Lives);
        hud.SetLives(MatchSide.Evil, evilLives.Lives);
        if (Current == Phase.Over) return;
        Rules.LivesChanged(angelLives.Lives, evilLives.Lives);
        CheckOver();
    }

    void CheckOver()
    {
        if (!Rules.IsOver || Current == Phase.Over) return;
        Current = Phase.Over;
        if (angelMove) angelMove.enabled = false;
        if (evilMove) evilMove.enabled = false;
        if (dropper) dropper.enabled = false;
        Stop(angelBody);
        Stop(evilBody);
        evilLives.Invulnerable = false;
        if (Rules.Winner == MatchSide.Angel) { angel.PowerUp(); evil.Stung(); }
        else { evil.PowerUp(); angel.Stung(); }
        hud.ShowWinner(Rules.Winner, Rules.Reason, Gamepad.all.Count > 0 ? $"{restartKey} / Start" : restartKey.ToString());
    }

    void SyncScores()
    {
        int carried = dropper ? dropper.Carried : 0;
        int hidden = dropper ? dropper.PlacedCount : 0;
        if (scoreManager)
        {
            scoreManager.AngelScore = Rules.Remaining;
            scoreManager.EvilScore = Rules.Eaten;
        }
        hud.SetPouch(carried, hidden, Rules.Eaten, Rules.Offerings, dropper && dropper.CooldownLeft <= 0f, Current == Phase.Hunt && TimeLeft <= 30f);
    }

    /// <summary>Pins a body in place (the Angel can't shove the frozen Evil around).</summary>
    static void Freeze(Rigidbody body, bool on)
    {
        if (!body) return;
        const RigidbodyConstraints xz = RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ;
        body.constraints = on ? body.constraints | xz : body.constraints & ~xz;
        Stop(body);
    }

    static void Stop(Rigidbody body)
    {
        if (!body) return;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
    }

    static void Place(Rigidbody body, Vector3 pos, Quaternion rot)
    {
        body.position = pos;
        body.rotation = rot;
        body.transform.SetPositionAndRotation(pos, rot);
        body.linearVelocity = Vector3.zero;
    }
}
