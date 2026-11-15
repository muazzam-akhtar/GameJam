using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Split-screen HUD in the SoulKeepers style (staging's SK_Countdown_HUD mockup / wireframe), built at runtime - no
/// prefab to keep in sync:
///   top band, anchored top-centre: the Keeper's and the Reaper's score pods with portraits (souls safe / souls taken),
///   the match timer between them (normal, warning ≤10 s, final ≤3 s with a red vignette, TIME!), the soul-balance bar
///   and the "souls in play" chip;
///   under each pod: lives (hearts), the slap-ready icon and a status line; each half's controls in its bottom corner;
///   head start: the Reaper's half blindfolded with a countdown, a "hide them" strip for the Keeper, then HUNT!;
///   the result screen (<see cref="ResultScreen"/>) when the round is over.
/// On screen the Angel is the Keeper, the Evil the Reaper and the offerings are souls. Art, fonts and sounds come from
/// <see cref="HudTheme"/>; without one it falls back to plain shapes and the default font.
/// </summary>
public class GameHUD : MonoBehaviour
{
    class Side
    {
        public readonly List<Image> hearts = new List<Image>();
        public readonly List<float> heartPunch = new List<float>();
        public TMP_Text score, status, controls;
        public Image slap;
        public bool slapReady = true;
        public float slapPunch, scorePunch;
        public int lives = -1, scoreShown = -1;
        public readonly List<GameObject> parts = new List<GameObject>();     // hidden in single player (the Reaper's)
        public TMP_Text label;
    }

    HudTheme theme;
    Side keeper, reaper;
    RectTransform root, match, band;
    CanvasGroup matchGroup;
    ResultScreen result;
    AudioSource ui;

    // timer
    Image timerPlate;
    TMP_Text timerLabel, timerDigits, timerTime;
    int timerShown = -1, timerState = -1;
    float timerPulse, timeUpAge = -1f, heartbeat;
    Image vignette;

    // soul balance
    RectTransform barFill, barOrb;
    float barValue = 1f, barTarget = 1f;
    TMP_Text chip;
    GameObject divider, balance, chipBox;

    // single player
    TMP_Text bannerTitle;
    float soloBanner = -1f;

    // head start / hunt call
    CanvasGroup blind, keeperBanner, huntCall;
    TMP_Text blindNumber, bannerSub, huntCallText;
    float huntAge = -1f;

    /// <summary>REMATCH on the result screen (button, R or Start).</summary>
    public event Action RematchPressed;
    /// <summary>MAIN MENU on the result screen.</summary>
    public event Action MainMenuPressed;
    public bool ResultShowing => result && result.Showing;
    public ResultScreen Result { get { Build(); return result; } }

    void Awake() => Build();

    void Build()
    {
        if (keeper != null) return;
        theme = HudTheme.Load();

        var canvasGo = new GameObject("HUD Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(AudioSource));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        ui = canvasGo.GetComponent<AudioSource>();
        ui.playOnAwake = false;
        ui.spatialBlend = 0f;
        root = canvasGo.GetComponent<RectTransform>();

        // everything of the match itself; it fades out under the result screen
        match = HudStyle.Fill(HudStyle.Group(root, "Match"));
        matchGroup = match.gameObject.AddComponent<CanvasGroup>();
        matchGroup.interactable = matchGroup.blocksRaycasts = false;

        BuildHeadStart();                                   // the blindfold sits under everything else

        var dividerImg = HudStyle.Img(match, "Divider", null, new Color(0.05f, 0.04f, 0.08f, 1f));
        divider = dividerImg.gameObject;
        HudStyle.Stretch(dividerImg.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(-3f, 0f), new Vector2(3f, 0f));
        var line = HudStyle.Img(dividerImg.rectTransform, "Line", null, new Color(0.93f, 0.88f, 1f, 0.35f));
        HudStyle.Stretch(line.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(-1f, 0f), new Vector2(1f, 0f));

        vignette = HudStyle.Img(match, "Final Vignette", theme ? theme.vignette : null, new Color(1f, 0.12f, 0.18f, 0f));
        HudStyle.Fill(vignette.rectTransform);
        if (!vignette.sprite) vignette.enabled = false;

        band = HudStyle.Group(match, "Top HUD");
        band.anchorMin = band.anchorMax = new Vector2(0.5f, 1f);
        band.pivot = new Vector2(0.5f, 1f);
        band.sizeDelta = new Vector2(1920f, 200f);
        band.anchoredPosition = Vector2.zero;

        keeper = BuildSide(true);
        reaper = BuildSide(false);
        BuildTimer();
        BuildBalance();
        BuildCalls();

        result = ResultScreen.Create(root, theme, PlayClick);
        result.Rematch += () => RematchPressed?.Invoke();
        result.MainMenu += () => MainMenuPressed?.Invoke();
    }

    // ---------------------------------------------------------------- top band

    Side BuildSide(bool k)
    {
        var s = new Side();
        var podSprite = theme ? (k ? theme.podKeeper : theme.podReaper) : null;
        var pod = HudStyle.Img(band, k ? "Keeper Pod" : "Reaper Pod", podSprite, Color.white);
        if (!podSprite)
        {
            pod.sprite = HudStyle.Rounded(theme); pod.type = Image.Type.Sliced;
            pod.color = k ? new Color(0.11f, 0.2f, 0.42f, 0.92f) : new Color(0.3f, 0.06f, 0.17f, 0.92f);
        }
        var prt = HudStyle.Tl(pod.rectTransform, k ? 334f : 1136f, 16f, 450f, 112.5f);
        s.parts.Add(pod.gameObject);

        var portraitSprite = theme ? (k ? theme.portraitKeeper : theme.portraitReaper) : null;
        if (portraitSprite)
        {
            var portrait = HudStyle.Img(prt, "Portrait", portraitSprite, Color.white);
            portrait.preserveAspect = true;
            HudStyle.Tl(portrait.rectTransform, (k ? 56f : 394f) - 45f, 11f, 90f, 90f);
        }

        var align = k ? TextAlignmentOptions.Left : TextAlignmentOptions.Right;
        float tx = k ? 116f : 104f;                                                // clear of the portrait ring
        var label = HudStyle.Text(prt, "Label", theme ? theme.label : null, 14f, new Color(1f, 1f, 1f, 0.82f), align, TextLook.Plain, 18f);
        label.text = k ? "SOULS SAFE" : "SOULS TAKEN";
        s.label = label;
        HudStyle.Tl(label.rectTransform, tx, 25f, 230f, 18f);
        var name = HudStyle.Text(prt, "Name", theme ? theme.heading : null, 25f, Color.white, align, k ? TextLook.Holy : TextLook.Hellfire, 4f);
        name.text = k ? "KEEPER" : "REAPER";
        HudStyle.Tl(name.rectTransform, tx, 43f, 230f, 34f);
        s.score = HudStyle.Text(prt, "Score", theme ? theme.numbers : null, 48f, Color.white, k ? TextAlignmentOptions.Right : TextAlignmentOptions.Left, TextLook.Neutral);
        HudStyle.Tl(s.score.rectTransform, k ? 300f : 32f, 19f, 118f, 72f);
        s.score.text = "0";

        // lives
        for (int i = 0; i < 3; i++)
        {
            var slot = HudStyle.Tl(HudStyle.Group(band, "Heart " + (i + 1)), k ? 426f + i * 40f : 1920f - 426f - 32f - i * 40f, 132f, 32f, 32f);
            s.parts.Add(slot.gameObject);
            var edge = HudStyle.Img(slot, "Edge", HudStyle.Heart(true), new Color(0.1f, 0.05f, 0.09f, 1f));
            HudStyle.Stretch(edge.rectTransform, Vector2.zero, Vector2.one, new Vector2(-3f, -3f), new Vector2(3f, 3f));
            var fill = HudStyle.Img(slot, "Fill", HudStyle.Heart(false), HudStyle.HeartRed);
            HudStyle.Fill(fill.rectTransform);
            s.hearts.Add(fill);
            s.heartPunch.Add(0f);
        }

        // slap ready (dims during the cooldown)
        var slapSprite = theme ? theme.slapIcon : null;
        s.slap = HudStyle.Img(band, "Slap Ready", slapSprite, Color.white);
        s.slap.preserveAspect = true;
        s.parts.Add(s.slap.gameObject);
        HudStyle.Tl(s.slap.rectTransform, k ? 359f : 1920f - 359f - 42f, 127f, 42f, 42f);
        if (!slapSprite) s.slap.enabled = false;

        s.status = HudStyle.Text(band, "Status", theme ? theme.label : null, 18f, new Color(1f, 1f, 1f, 0.9f), align, TextLook.Neutral);
        HudStyle.Tl(s.status.rectTransform, k ? 426f : 1920f - 426f - 420f, 168f, 420f, 26f);
        s.parts.Add(s.status.gameObject);

        // who plays on what - bottom corner of the player's own half
        s.controls = HudStyle.Text(match, k ? "Keeper Controls" : "Reaper Controls", theme ? theme.label : null, 17f, new Color(1f, 1f, 1f, 0.66f),
            k ? TextAlignmentOptions.BottomLeft : TextAlignmentOptions.BottomRight, TextLook.Neutral);
        s.controls.enableAutoSizing = true;
        s.controls.fontSizeMin = 11f;
        s.controls.fontSizeMax = 17f;
        HudStyle.Stretch(s.controls.rectTransform, new Vector2(k ? 0f : 0.5f, 0f), new Vector2(k ? 0.5f : 1f, 0f), new Vector2(24f, 16f), new Vector2(-24f, 70f));
        s.parts.Add(s.controls.gameObject);
        return s;
    }

    void BuildTimer()
    {
        var plate = theme ? theme.timerNormal : null;
        timerPlate = HudStyle.Img(band, "Timer", plate, Color.white);
        if (!plate) { timerPlate.sprite = HudStyle.Rounded(theme); timerPlate.type = Image.Type.Sliced; timerPlate.color = new Color(0.08f, 0.07f, 0.13f, 0.92f); }
        var rt = HudStyle.Tl(timerPlate.rectTransform, 820f, 9f, 280f, 112f);
        timerLabel = HudStyle.Text(rt, "Label", theme ? theme.label : null, 13f, new Color(1f, 1f, 1f, 0.88f), TextAlignmentOptions.Center, TextLook.Plain, 22f);
        timerLabel.text = "TIME LEFT";
        HudStyle.Tl(timerLabel.rectTransform, 0f, 24f, 280f, 16f);
        timerDigits = HudStyle.Text(rt, "Digits", theme ? theme.numbers : null, 54f, Color.white, TextAlignmentOptions.Center, TextLook.Neutral);
        HudStyle.Tl(timerDigits.rectTransform, 0f, 35f, 280f, 64f);
        timerDigits.text = HudStyle.Clock(120);
        timerTime = HudStyle.Text(rt, "Time Up", theme ? theme.display : null, 54f, Color.white, TextAlignmentOptions.Center, TextLook.Hellfire);
        timerTime.text = "TIME!";
        HudStyle.Tl(timerTime.rectTransform, 0f, 14f, 280f, 84f);
        timerTime.gameObject.SetActive(false);
    }

    void BuildBalance()
    {
        var pill = theme ? theme.pill : null;
        var track = HudStyle.Img(band, "Soul Balance", pill ? pill : HudStyle.Rounded(theme), HudStyle.ReaperRed, true);
        balance = track.gameObject;
        var trt = HudStyle.Tl(track.rectTransform, 560f, 143f, 800f, 14f);
        var fill = HudStyle.Img(trt, "Safe", pill ? pill : HudStyle.Rounded(theme), HudStyle.KeeperBlue, true);
        barFill = HudStyle.Fill(fill.rectTransform);

        barOrb = HudStyle.Group(trt, "Orb");
        barOrb.anchorMin = barOrb.anchorMax = new Vector2(1f, 0.5f);
        barOrb.sizeDelta = Vector2.zero;
        if (theme && theme.glow)
        {
            var halo = HudStyle.Img(barOrb, "Glow", theme.glow, new Color(0.85f, 0.92f, 1f, 0.9f));
            HudStyle.Stretch(halo.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-26f, -26f), new Vector2(26f, 26f));
        }
        var core = HudStyle.Img(barOrb, "Core", theme && theme.circle ? theme.circle : HudStyle.Rounded(theme), Color.white);
        HudStyle.Stretch(core.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-8f, -8f), new Vector2(8f, 8f));

        var chipBg = HudStyle.Img(band, "Souls In Play", pill ? pill : HudStyle.Rounded(theme), new Color(0.055f, 0.047f, 0.094f, 0.82f), true);
        chipBox = chipBg.gameObject;
        var crt = HudStyle.Tl(chipBg.rectTransform, 850f, 163f, 220f, 26f);
        chip = HudStyle.Text(crt, "Text", theme ? theme.label : null, 13f, new Color(1f, 1f, 1f, 0.88f), TextAlignmentOptions.Center, TextLook.Plain, 18f);
        HudStyle.Fill(chip.rectTransform);
        chip.text = "SOULS IN PLAY · 0";
    }

    // ---------------------------------------------------------------- head start + hunt call

    void BuildHeadStart()
    {
        // the Reaper's half: blindfolded, deep crimson, a big countdown
        var shade = HudStyle.Img(match, "Reaper Blindfold", null, new Color(0.075f, 0.012f, 0.045f, 0.97f));
        HudStyle.Stretch(shade.rectTransform, new Vector2(0.5f, 0f), Vector2.one, new Vector2(3f, 0f), Vector2.zero);
        blind = shade.gameObject.AddComponent<CanvasGroup>();
        if (theme && theme.glow)
        {
            var ember = HudStyle.Img(shade.rectTransform, "Ember", theme.glow, new Color(0.85f, 0.12f, 0.3f, 0.32f));
            HudStyle.Stretch(ember.rectTransform, new Vector2(0.05f, 0.12f), new Vector2(0.95f, 0.88f), Vector2.zero, Vector2.zero);
        }
        var title = HudStyle.Text(shade.rectTransform, "Title", theme ? theme.heading : null, 40f, Color.white, TextAlignmentOptions.Center, TextLook.Hellfire, 6f);
        title.text = "THE KEEPER IS HIDING THE SOULS";
        title.enableAutoSizing = true; title.fontSizeMin = 22f; title.fontSizeMax = 40f;
        HudStyle.Stretch(title.rectTransform, new Vector2(0.06f, 0.6f), new Vector2(0.94f, 0.7f), Vector2.zero, Vector2.zero);
        blindNumber = HudStyle.Text(shade.rectTransform, "Number", theme ? theme.numbers : null, 230f, Color.white, TextAlignmentOptions.Center, TextLook.Hellfire);
        HudStyle.Stretch(blindNumber.rectTransform, new Vector2(0f, 0.3f), new Vector2(1f, 0.6f), Vector2.zero, Vector2.zero);
        var sub = HudStyle.Text(shade.rectTransform, "Sub", theme ? theme.label : null, 28f, new Color(1f, 1f, 1f, 0.8f), TextAlignmentOptions.Center, TextLook.Neutral);
        sub.text = "eyes shut, Reaper... then find and take <b>every</b> soul before the time runs out";
        sub.textWrappingMode = TextWrappingModes.Normal;
        HudStyle.Stretch(sub.rectTransform, new Vector2(0.1f, 0.16f), new Vector2(0.9f, 0.3f), Vector2.zero, Vector2.zero);
        blind.gameObject.SetActive(false);
    }

    void BuildCalls()
    {
        // the Keeper's half: one slim strip at the bottom, out of the way while they play
        var banner = HudStyle.Img(match, "Keeper Banner", HudStyle.Rounded(theme), new Color(0.06f, 0.11f, 0.24f, 0.8f), true);
        var br = banner.rectTransform;
        br.anchorMin = br.anchorMax = new Vector2(0.25f, 0f);
        br.pivot = new Vector2(0.5f, 0f);
        br.anchoredPosition = new Vector2(0f, 84f);
        br.sizeDelta = new Vector2(660f, 58f);
        var rim = banner.gameObject.AddComponent<Outline>();
        rim.effectColor = new Color(HudStyle.KeeperLight.r, HudStyle.KeeperLight.g, HudStyle.KeeperLight.b, 0.45f);
        rim.effectDistance = new Vector2(1.5f, -1.5f);
        keeperBanner = banner.gameObject.AddComponent<CanvasGroup>();
        var bt = HudStyle.Text(br, "Title", theme ? theme.heading : null, 24f, Color.white, TextAlignmentOptions.Left, TextLook.Holy, 4f);
        bt.text = "HIDE ALL YOUR SOULS";
        bannerTitle = bt;
        HudStyle.Stretch(bt.rectTransform, Vector2.zero, new Vector2(0.6f, 1f), new Vector2(24f, 0f), Vector2.zero);
        bannerSub = HudStyle.Text(br, "Sub", theme ? theme.label : null, 22f, new Color(1f, 1f, 1f, 0.9f), TextAlignmentOptions.Right, TextLook.Neutral);
        HudStyle.Stretch(bannerSub.rectTransform, new Vector2(0.6f, 0f), Vector2.one, Vector2.zero, new Vector2(-24f, 0f));
        keeperBanner.gameObject.SetActive(false);

        // HUNT! on the Reaper's half when the blindfold drops
        var call = HudStyle.Group(match, "Hunt Call");
        call.gameObject.AddComponent<CanvasGroup>();
        HudStyle.Stretch(call, new Vector2(0.5f, 0.4f), new Vector2(1f, 0.75f), Vector2.zero, Vector2.zero);
        huntCall = call.GetComponent<CanvasGroup>();
        huntCallText = HudStyle.Text(call, "Text", theme ? theme.display : null, 170f, Color.white, TextAlignmentOptions.Center, TextLook.Hellfire);
        huntCallText.text = "HUNT!";
        HudStyle.Fill(huntCallText.rectTransform);
        huntCall.gameObject.SetActive(false);
    }

    // ---------------------------------------------------------------- updates from the game

    public void SetLives(MatchSide side, int lives)
    {
        Build();
        var p = side == MatchSide.Angel ? keeper : reaper;
        for (int i = 0; i < p.hearts.Count; i++)
        {
            bool full = i < lives;
            p.hearts[i].color = full ? HudStyle.HeartRed : new Color(0.35f, 0.3f, 0.36f, 0.45f);
            if (p.lives > lives && i >= lives && i < p.lives) p.heartPunch[i] = 1f;        // the heart just lost
        }
        p.lives = lives;
    }

    /// <summary>What this player plays on and which buttons do what (changes when controllers come and go).</summary>
    public void SetControls(MatchSide side, string text)
    {
        Build();
        HudStyle.SetText((side == MatchSide.Angel ? keeper : reaper).controls, text);
    }

    /// <summary>The slap is off cooldown (bright icon) or recharging (dim); it pops when it comes back.</summary>
    public void SetSlapReady(MatchSide side, bool ready)
    {
        Build();
        var p = side == MatchSide.Angel ? keeper : reaper;
        if (ready == p.slapReady) return;
        if (ready) p.slapPunch = 1f;
        p.slapReady = ready;
        p.slap.color = ready ? Color.white : new Color(0.55f, 0.55f, 0.62f, 0.4f);
    }

    /// <summary>The souls: in the Keeper's pouch, hidden around the map, taken so far, out of how many.</summary>
    /// <param name="late">The clock is running out: unhidden souls turn red for the Keeper.</param>
    public void SetPouch(int carried, int hidden, int eaten, int total, bool ready, bool late = false)
    {
        Build();
        int safe = total - eaten;
        SetScore(keeper, safe);
        SetScore(reaper, eaten);
        HudStyle.SetText(keeper.status, carried > 0
            ? $"<color=#{(late ? "ff6b8f" : "ffd35a")}>{carried} {Souls(carried)} still to hide!</color>  ·  {hidden} hidden" + (ready ? "" : "  <color=#ffffff55>…</color>")
            : $"<color=#9df0b0>all hidden</color>  ·  {hidden} left");
        HudStyle.SetText(reaper.status, carried > 0
            ? $"<color=#ffd35a>{hidden} hidden</color>  ·  the Keeper still holds {carried}"
            : safe > 0 ? $"<color=#ffd35a>{safe} left to find</color>" : "all taken!");
        HudStyle.SetText(chip, $"SOULS IN PLAY · {hidden}");
        barTarget = total > 0 ? Mathf.Clamp01(safe / (float)total) : 1f;
    }

    static string Souls(int n) => n == 1 ? "soul" : "souls";

    void SetScore(Side s, int value)
    {
        if (value == s.scoreShown) return;
        if (s.scoreShown >= 0) s.scorePunch = 1f;
        s.scoreShown = value;
        s.score.text = value.ToString();
    }

    /// <summary>The Reaper just took one: both scores jump.</summary>
    public void Devoured(int eaten, int total)
    {
        Build();
        keeper.scorePunch = reaper.scorePunch = 1f;
    }

    /// <summary>Head start: the Reaper's half is blindfolded with a countdown, the Keeper gets a "hide them" strip.</summary>
    public void ShowHeadStart(float secondsLeft)
    {
        Build();
        huntAge = -1f;
        blind.gameObject.SetActive(true);
        blind.alpha = 1f;
        keeperBanner.gameObject.SetActive(true);
        keeperBanner.alpha = 1f;
        huntCall.gameObject.SetActive(false);
        int n = Mathf.CeilToInt(secondsLeft);
        if (blindNumber.text != n.ToString())
        {
            blindNumber.text = n.ToString();
            blindNumber.rectTransform.localScale = Vector3.one * 1.25f;
        }
        HudStyle.SetText(bannerSub, $"Reaper wakes in <b>{n}</b>");
    }

    /// <summary>Head start over: the blindfold drops, HUNT! on the Reaper's half, a warning on the Keeper's.</summary>
    public void StartHunt()
    {
        Build();
        huntAge = 0f;
        huntCall.gameObject.SetActive(true);
        bannerSub.text = "<color=#ff6b8f><b>the Reaper is awake!</b></color>";
    }

    /// <summary>Seconds left in the hunt: normal, warning (≤10 s, red plate, pulsing), final 3 (bigger, red vignette).</summary>
    public void SetTimer(float seconds)
    {
        Build();
        if (timeUpAge >= 0f) return;
        int whole = Mathf.CeilToInt(Mathf.Max(0f, seconds));
        int state = whole <= 3 ? 2 : whole <= 10 ? 1 : 0;
        if (whole != timerShown)
        {
            if (state > 0 && whole < timerShown) timerPulse = 1f;
            timerShown = whole;
            timerDigits.text = HudStyle.Clock(whole);
        }
        if (state != timerState) ApplyTimerState(state);
    }

    void ApplyTimerState(int state)
    {
        timerState = state;
        var plate = theme ? (state > 0 ? theme.timerWarning : theme.timerNormal) : null;
        if (plate) timerPlate.sprite = plate;
        else timerPlate.color = state > 0 ? new Color(0.35f, 0.06f, 0.08f, 0.92f) : new Color(0.08f, 0.07f, 0.13f, 0.92f);
        timerDigits.color = state > 0 ? new Color(1f, 0.5f, 0.45f) : Color.white;
        HudStyle.SetLook(timerDigits, state > 0 ? TextLook.Hellfire : TextLook.Neutral);
        timerDigits.fontSize = state == 2 ? 60f : 54f;
    }

    /// <summary>The hunt timer hit zero: "TIME!" slams onto the plate.</summary>
    public void TimeUp()
    {
        Build();
        ApplyTimerState(2);
        timeUpAge = 0f;
        timerLabel.gameObject.SetActive(false);
        timerDigits.gameObject.SetActive(false);
        timerTime.gameObject.SetActive(true);
    }

    /// <summary>The round is over: the result screen (after the 1.2 s freeze).</summary>
    public void ShowResult(MatchResult r, Key restartKey = Key.R, float freeze = ResultScreen.Freeze)
    {
        Build();
        result.Show(r, restartKey, freeze);
    }

    // ---------------------------------------------------------------- single player

    /// <summary>Single player: the Keeper's side only, full width - no Reaper pod, divider, balance bar or timer.</summary>
    public void SetSolo(bool on)
    {
        Build();
        foreach (var go in reaper.parts) go.SetActive(!on);
        divider.SetActive(!on);
        balance.SetActive(!on);
        chipBox.SetActive(!on);
        timerPlate.gameObject.SetActive(!on);
        keeper.label.text = on ? "SOULS FOUND" : "SOULS SAFE";
        var br = (RectTransform)keeperBanner.transform;
        br.anchorMin = br.anchorMax = new Vector2(on ? 0.5f : 0.25f, 0f);
    }

    /// <summary>Single player: souls found so far, and whether the gate is open.</summary>
    public void SetSoloProgress(int found, int total, bool gateOpen)
    {
        Build();
        SetScore(keeper, found);
        int left = total - found;
        HudStyle.SetText(keeper.status, gateOpen
            ? "<color=#9df0b0>the gate is open</color>  ·  fly through the ruined arch"
            : $"<color=#ffd35a>{left} {Souls(left)} left</color>  ·  the Reapers guard them");
    }

    /// <summary>Single player: a message on the bottom strip for a few seconds.</summary>
    public void SoloBanner(string title, string sub, float seconds)
    {
        Build();
        bannerTitle.text = title;
        bannerSub.text = sub;
        keeperBanner.gameObject.SetActive(true);
        keeperBanner.alpha = 1f;
        soloBanner = seconds;
    }

    /// <summary>A new round: no result screen, no TIME!, calm timer.</summary>
    public void ResetForRound()
    {
        Build();
        result.Hide();
        timeUpAge = -1f;
        timerLabel.gameObject.SetActive(true);
        timerDigits.gameObject.SetActive(true);
        timerTime.gameObject.SetActive(false);
        timerShown = -1;
        ApplyTimerState(0);
    }

    void PlayClick()
    {
        if (theme && theme.click && ui) ui.PlayOneShot(theme.click, theme.clickVolume);
    }

    // ---------------------------------------------------------------- animation

    void Update()
    {
        if (keeper != null) Step(Time.unscaledDeltaTime);
    }

    /// <summary>Advances the HUD's animations (Update; tests and previews drive it directly).</summary>
    public void Step(float dt)
    {
        Build();
        AnimateHeadStart(dt);
        AnimateTimer(dt);

        barValue = Mathf.Lerp(barValue, barTarget, 1f - Mathf.Exp(-dt * 6f));
        if (Mathf.Abs(barValue - barTarget) < 0.001f) barValue = barTarget;
        barFill.anchorMax = new Vector2(barValue, 1f);
        barOrb.anchorMin = barOrb.anchorMax = new Vector2(barValue, 0.5f);
        barFill.gameObject.SetActive(barValue > 0.005f);

        foreach (var p in new[] { keeper, reaper })
        {
            p.scorePunch = Mathf.Max(0f, p.scorePunch - dt * 5f);                    // 1.25 → 1 in 0.2 s
            p.score.rectTransform.localScale = Vector3.one * (1f + 0.25f * p.scorePunch);
            p.slapPunch = Mathf.Max(0f, p.slapPunch - dt * 3f);
            p.slap.rectTransform.localScale = Vector3.one * (1f + 0.35f * p.slapPunch * p.slapPunch);
            for (int i = 0; i < p.hearts.Count; i++)
            {
                p.heartPunch[i] = Mathf.Max(0f, p.heartPunch[i] - dt * 2.2f);
                float k = p.heartPunch[i];
                p.hearts[i].rectTransform.localScale = Vector3.one * (1f + 0.6f * k * k);
                p.hearts[i].rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(k * 20f) * 14f * k);
            }
        }
        result.Step(dt);
        // the match HUD gives way to the panel - or, while the ending plays, right after TIME! so nothing covers it
        float early = result.Showing && result.FreezeSeconds > ResultScreen.Freeze ? Mathf.Clamp01((result.Age - 0.7f) / 0.4f) : 0f;
        matchGroup.alpha = 1f - Mathf.Max(early, result.DimProgress);
        if (soloBanner >= 0f)
        {
            soloBanner -= dt;
            keeperBanner.alpha = Mathf.Clamp01(soloBanner / 0.6f);
            if (soloBanner < 0f) keeperBanner.gameObject.SetActive(false);
        }
    }

    void AnimateTimer(float dt)
    {
        timerPulse = Mathf.Max(0f, timerPulse - dt * 2.5f);
        float pulse = timerState == 2 ? 0.1f : 0.06f;                               // 1.0 → 1.06 every second
        timerDigits.rectTransform.localScale = Vector3.one * (1f + pulse * timerPulse);

        float vignetteTarget = 0f;
        if (timerState == 2 && (timeUpAge < 0f || timeUpAge < ResultScreen.Freeze))
        {
            heartbeat += dt;
            vignetteTarget = 0.2f + 0.14f * Mathf.Abs(Mathf.Sin(heartbeat * Mathf.PI * 1.1f));
        }
        if (timeUpAge >= 0f)
        {
            timeUpAge += dt;
            float k = HudStyle.OutCubic(timeUpAge / 0.25f);                          // TIME! slams in: 1.4 → 1.0
            timerTime.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.4f, 1f, k);
            timerTime.alpha = Mathf.Clamp01(timeUpAge / 0.12f);
        }
        if (vignette.enabled)
        {
            float a = Mathf.MoveTowards(vignette.color.a, vignetteTarget, dt * 1.6f);
            HudStyle.SetAlpha(vignette, a);
        }
    }

    void AnimateHeadStart(float dt)
    {
        if (blindNumber.rectTransform.localScale.x > 1f)
            blindNumber.rectTransform.localScale = Vector3.one * Mathf.MoveTowards(blindNumber.rectTransform.localScale.x, 1f, dt * 1.6f);
        if (huntAge < 0f) return;
        huntAge += dt;
        blind.alpha = 1f - Mathf.Clamp01(huntAge / 0.7f);                           // the blindfold drops
        if (blind.alpha <= 0f && blind.gameObject.activeSelf) blind.gameObject.SetActive(false);
        float k = Mathf.Clamp01(huntAge / 0.35f);
        huntCall.alpha = huntAge < 1.2f ? k : 1f - Mathf.Clamp01((huntAge - 1.2f) / 0.6f);
        huntCallText.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.6f, 1f, 1f - (1f - k) * (1f - k));
        if (huntAge > 1.8f && huntCall.gameObject.activeSelf) huntCall.gameObject.SetActive(false);
        keeperBanner.alpha = 1f - Mathf.Clamp01((huntAge - 1.6f) / 0.6f);
        if (huntAge > 2.2f && keeperBanner.gameObject.activeSelf) keeperBanner.gameObject.SetActive(false);
    }
}
