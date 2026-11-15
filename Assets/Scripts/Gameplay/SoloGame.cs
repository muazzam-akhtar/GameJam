using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Single player (PLAY on the start screen): the Keeper alone, full screen. Souls are scattered around the courtyard
/// (some deep in the caves) and Reapers stand guard near them (<see cref="GuardAI"/>: they chase a short way, then go
/// back to their posts). Find every soul and the ruined arch lights up - fly through it to win. Lose all your lives
/// and the Reapers win. Started by <see cref="GameManager"/> when <see cref="GameMode.Solo"/> is set.
/// </summary>
public class SoloGame : MonoBehaviour
{
    public int souls = 8;
    public int guards = 5;
    [Tooltip("Centre of the freestanding ruined arch (CourtyardBuilder.RuinArch); you fly through it along z.")]
    public Vector3 gate = new Vector3(280f, 0f, 70f);
    public float gateRadius = 18f;
    public float safeStart = 150f;

    // inner edge of the courtyard wall (CourtyardBuilder.Outline in world space)
    static readonly Vector2[] Outline =
    {
        new Vector2(-224f, -242f), new Vector2(336f, -242f), new Vector2(416f, -162f), new Vector2(416f, 318f),
        new Vector2(336f, 398f), new Vector2(-224f, 398f), new Vector2(-304f, 318f), new Vector2(-304f, -162f),
    };
    static readonly Vector2 Centre = new Vector2(56f, 78f);

    AngelController keeper;
    GameManager sound;                                                    // the sound design lives on the GameManager
    PlayerLives lives;
    Rigidbody body;
    Behaviour keeperMove;
    FollowCamera cam;
    GameHUD hud;
    Offering soulPrefab;
    GameObject guardTemplate;
    Transform container;
    Vector3 start;
    Quaternion startRot;

    readonly List<Offering> placed = new List<Offering>();
    readonly List<GuardAI> posted = new List<GuardAI>();
    Offering portal;
    MatchStats stats;
    int found, livesSeen;
    bool open, over;
    float startedAt;

    public void Begin(AngelController keeper, EvilController evil, FollowCamera keeperCam, FollowCamera evilCam,
                      GameHUD hud, Behaviour keeperMove, AngelDropper dropper, Transform container)
    {
        this.keeper = keeper;
        this.hud = hud;
        this.keeperMove = keeperMove;
        this.container = container;
        sound = GetComponent<GameManager>();
        cam = keeperCam;
        body = keeper.GetComponent<Rigidbody>();
        start = body.position;
        startRot = body.rotation;
        lives = keeper.GetComponent<PlayerLives>();
        lives.Changed += OnLives;
        lives.Invulnerable = false;

        if (dropper) { soulPrefab = dropper.offeringPrefab; dropper.enabled = false; }   // she finds souls, she doesn't drop them
        guardTemplate = evil.gameObject;
        guardTemplate.SetActive(false);                                                  // no second player
        if (evilCam) evilCam.gameObject.SetActive(false);
        if (keeperCam) keeperCam.GetComponent<Camera>().rect = new Rect(0f, 0f, 1f, 1f);

        hud.SetSolo(true);
        hud.RematchPressed += BeginRound;
        hud.MainMenuPressed += ToMenu;
        InputRouter.Changed += ShowControls;
        ShowControls();
        BeginRound();
    }

    void OnDestroy()
    {
        InputRouter.Changed -= ShowControls;
        if (lives) lives.Changed -= OnLives;
        if (hud) { hud.RematchPressed -= BeginRound; hud.MainMenuPressed -= ToMenu; }
    }

    void ShowControls() => hud.SetControls(MatchSide.Angel, InputRouter.Describe(0, false, null));

    void BeginRound()
    {
        StopAllCoroutines();
        foreach (var o in placed) if (o) Destroy(o.gameObject);
        foreach (var g in posted) if (g) Destroy(g.gameObject);
        placed.Clear();
        posted.Clear();
        if (portal) Destroy(portal.gameObject);

        over = open = false;
        found = 0;
        stats = new MatchStats();
        startedAt = Time.time;
        InputRouter.MenuOpen = false;
        body.position = start;
        body.rotation = startRot;
        keeper.transform.SetPositionAndRotation(start, startRot);
        body.linearVelocity = Vector3.zero;
        keeperMove.enabled = true;
        lives.ResetLives();
        livesSeen = lives.Lives;
        if (cam) cam.Snap();

        hud.ResetForRound();
        SpawnSouls();
        SpawnGuards();
        hud.SetSoloProgress(found, placed.Count, false);
        hud.SoloBanner($"FIND ALL {placed.Count} SOULS", "the Reapers guard them", 4f);
    }

    // ---------------------------------------------------------------- spawning

    void SpawnSouls()
    {
        if (!soulPrefab) return;
        var spots = new List<Vector3>();
        var caves = new List<Hideout>(FindObjectsByType<Hideout>(FindObjectsSortMode.None));
        Shuffle(caves);
        foreach (var c in caves)                                       // a few deep in the caves
        {
            if (spots.Count >= Mathf.Min(3, souls)) break;
            var p = Flat(c.transform.position);
            if (Vector3.Distance(p, start) > safeStart * 0.6f) spots.Add(p);
        }
        for (int tries = 0; spots.Count < souls && tries < 600; tries++)    // the rest out in the open
        {
            var p = RandomInside();
            if (Vector3.Distance(p, start) < safeStart || Vector3.Distance(p, gate) < 50f || !Free(p)) continue;
            bool clear = true;
            foreach (var s in spots) if (Vector3.Distance(s, p) < 90f) { clear = false; break; }
            if (clear) spots.Add(p);
        }
        foreach (var p in spots)
        {
            var o = Instantiate(soulPrefab, p, Quaternion.identity, container);
            o.keeperCollects = true;
            o.Collected += OnFound;
            placed.Add(o);
        }
    }

    void SpawnGuards()
    {
        if (!guardTemplate) return;
        var targets = new List<Offering>(placed);
        Shuffle(targets);
        for (int i = 0; i < Mathf.Min(guards, targets.Count); i++)
        {
            var soul = Flat(targets[i].transform.position);
            Vector3 post = soul;
            for (int tries = 0; tries < 24; tries++)                   // a little off the soul, in the open
            {
                float a = Random.value * Mathf.PI * 2f;
                var p = soul + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * Random.Range(28f, 45f);
                if (Inside(p) && Free(p) && Vector3.Distance(p, start) > safeStart) { post = p; break; }
            }
            var go = Instantiate(guardTemplate, post, Quaternion.identity, container);
            go.name = "Evil";                                         // the Keeper's slap looks for this name
            if (go.TryGetComponent<P2Controller>(out var input)) input.enabled = false;
            var rb = go.GetComponent<Rigidbody>();
            rb.useGravity = false;
            rb.constraints |= RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            var guard = go.AddComponent<GuardAI>();
            go.SetActive(true);
            guard.Post(post, soul - post, keeper.transform);
            guard.Stunned += _ => stats.Slapped(MatchSide.Evil);
            guard.HitKeeper += _ => { if (sound) { sound.PlaySlapSound(); sound.PlayStunSound(); } };
            posted.Add(guard);
        }
    }

    // ---------------------------------------------------------------- the round

    void OnFound(Offering o)
    {
        if (over) return;
        found++;
        if (sound) sound.PlayAngelCollectedSound();
        stats.SoulHidden();                                             // best run of souls found without being slapped
        hud.SetSoloProgress(found, placed.Count, false);
        if (found >= placed.Count) OpenGate();
        else hud.SoloBanner($"{placed.Count - found} {(placed.Count - found == 1 ? "SOUL" : "SOULS")} LEFT", "", 1.6f);
    }

    void OpenGate()
    {
        open = true;
        hud.SetSoloProgress(found, placed.Count, true);
        hud.SoloBanner("THE GATE IS OPEN", "fly through the ruined arch", 5f);
        if (!soulPrefab) return;
        // the soul's glowing ring, stood up and grown to fill the arch
        portal = Instantiate(soulPrefab, gate + Vector3.up * 16f, Quaternion.Euler(90f, 0f, 0f), container);
        portal.name = "Gate Portal";
        foreach (var c in portal.GetComponentsInChildren<Collider>()) c.enabled = false;
        if (portal.visual) portal.visual.gameObject.SetActive(false);
        portal.transform.localScale *= 2.6f;
        if (portal.glowLight) portal.glowLight.range *= 2.6f;
    }

    void Update()
    {
        if (over || !open) return;
        if (Vector3.Distance(Flat(body.position), gate) < gateRadius) End(MatchSide.Angel, "the Keeper escaped through the gate");
    }

    void OnLives(PlayerLives l)
    {
        if (over) return;
        for (int i = l.Lives; i < livesSeen; i++) stats.Slapped(MatchSide.Angel);
        livesSeen = l.Lives;
        if (!l.IsAlive) End(MatchSide.Evil, "the Keeper ran out of lives");
    }

    void End(MatchSide winner, string reason)
    {
        over = true;
        keeperMove.enabled = false;
        body.linearVelocity = Vector3.zero;
        foreach (var g in posted) if (g) g.enabled = false;
        if (winner == MatchSide.Angel) keeper.PowerUp(); else keeper.Stung();
        InputRouter.MenuOpen = true;
        hud.ShowResult(new MatchResult
        {
            solo = true,
            winner = winner,
            reason = reason,
            saved = found,
            taken = placed.Count - found,
            total = placed.Count,
            keeperSlaps = stats.KeeperSlaps,
            reaperSlaps = stats.ReaperSlaps,
            keeperStunned = stats.KeeperStunned,
            reaperStunned = stats.ReaperStunned,
            keeperStreak = stats.KeeperStreak,
            huntSeconds = Time.time - startedAt,
        });
    }

    void ToMenu()
    {
        if (over) StartCoroutine(LoadMenu());
    }

    IEnumerator LoadMenu()
    {
        yield return new WaitForSecondsRealtime(0.35f);
        if (Application.CanStreamedLevelBeLoaded("MainMenu")) SceneManager.LoadScene("MainMenu");
        else BeginRound();
    }

    // ---------------------------------------------------------------- places

    static Vector3 RandomInside()
    {
        for (int i = 0; i < 50; i++)
        {
            var p = new Vector3(Random.Range(-304f, 416f), 0f, Random.Range(-242f, 398f));
            if (Inside(p)) return p;
        }
        return new Vector3(Centre.x, 0f, Centre.y);
    }

    /// <summary>Inside the courtyard wall with a margin (the outline shrunk towards the centre).</summary>
    public static bool Inside(Vector3 p)
    {
        var q = Centre + (new Vector2(p.x, p.z) - Centre) / 0.88f;
        bool inside = false;
        for (int i = 0, j = Outline.Length - 1; i < Outline.Length; j = i++)
            if ((Outline[i].y > q.y) != (Outline[j].y > q.y)
                && q.x < (Outline[j].x - Outline[i].x) * (q.y - Outline[i].y) / (Outline[j].y - Outline[i].y) + Outline[i].x)
                inside = !inside;
        return inside;
    }

    /// <summary>Room for a character here: nothing solid between knee and head height.</summary>
    static bool Free(Vector3 p) =>
        !Physics.CheckCapsule(p + Vector3.up * 16f, p + Vector3.up * 30f, 10f, ~0, QueryTriggerInteraction.Ignore);   // clear of the ground

    static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

    static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
