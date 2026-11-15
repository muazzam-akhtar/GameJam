using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// The end-of-round panel, built from staging's SK_Result_Panel_Wireframe and the KEEPERS WIN / REAPERS WIN mockups:
/// a ribbon banner, the flavour line (and why it ended), both characters - the winner big with rays, a glow and a
/// WINNER tag, the loser grey and smaller - the scores counting up (SAVED / TAKEN), four stat rows with the better
/// value highlighted, the match footer and REMATCH / MAIN MENU (default focus REMATCH; R or Start also rematch).
/// Flow (unscaled time): freeze <see cref="Freeze"/> s → dim → panel scales 0.85 → 1 → banner drops in → scores count
/// up → rays + WINNER tag → rows stagger in → buttons fade in last.
/// </summary>
public class ResultScreen : MonoBehaviour
{
    /// <summary>Seconds the finished round stays frozen (TIME! / the winner's cheer) before the panel comes in.</summary>
    public const float Freeze = 1.2f;
    float freeze = Freeze;                              // longer when the end-of-match story plays first
    float DimAt => freeze;
    float PanelAt => freeze + 0.1f;
    float BannerAt => freeze + 0.3f;
    float CountAt => freeze + 0.45f;
    float RaysAt => freeze + 0.75f;
    float RowsAt => freeze + 0.85f;
    float ButtonsAt => freeze + 1.25f;

    public event Action Rematch, MainMenu;

    /// <summary>A round result is up (including the freeze before the panel).</summary>
    public bool Showing { get; private set; }
    /// <summary>The buttons are in and take input.</summary>
    public bool Interactive => Showing && !chosen && age >= ButtonsAt + 0.3f;
    /// <summary>0 → 1 as the screen dims behind the panel (the match HUD fades out with it).</summary>
    public float DimProgress => Showing ? Mathf.Clamp01((age - DimAt) / 0.3f) : 0f;
    /// <summary>Seconds since the round ended, and how long the panel waits (longer while the ending plays).</summary>
    public float Age => age;
    public float FreezeSeconds => freeze;
    public MatchResult Current => result;

    HudTheme theme;
    Action click;
    MatchResult result;
    Key restartKey = Key.R;
    float age;
    bool chosen, buttonsLive;

    Image dim, panel, banner, keeperHero, reaperHero, glow, bigRays;
    RectTransform area, panelRt, bannerRt, tagRt;
    CanvasGroup panelGroup, bannerGroup, tagGroup, raysGroup, buttonsGroup;
    TMP_Text bannerText, flavour, reason, keeperScore, reaperScore, keeperName, reaperName, keeperPlayer, reaperPlayer, footer, savedLabel, takenLabel;
    Vector2 bannerHome;
    Button rematch, menu;
    readonly Row[] rows = new Row[4];

    class Row
    {
        public CanvasGroup group;
        public RectTransform rt;
        public Vector2 home;
        public TMP_Text left, right, label;
        public RectTransform leftBar, rightBar;
    }

    public static ResultScreen Create(RectTransform canvas, HudTheme theme, Action click)
    {
        var go = new GameObject("Result Screen", typeof(RectTransform), typeof(CanvasGroup));
        go.transform.SetParent(canvas, false);
        var rs = go.AddComponent<ResultScreen>();
        rs.theme = theme;
        rs.click = click;
        rs.Build();
        rs.Hide();
        return rs;
    }

    Font Display => theme ? theme.display : null;
    Font Heading => theme ? theme.heading : null;
    Font Numbers => theme ? theme.numbers : null;
    Font Label => theme ? theme.label : null;

    void Build()
    {
        HudStyle.Fill((RectTransform)transform);

        dim = HudStyle.Img(transform, "Dim", null, new Color(0.02f, 0.01f, 0.04f, 0f));
        dim.raycastTarget = true;                                         // nothing behind it takes clicks
        HudStyle.Fill(dim.rectTransform);

        area = HudStyle.Group(transform, "Area");                         // the mockup's 1920 x 1080, centred
        area.anchorMin = area.anchorMax = area.pivot = new Vector2(0.5f, 0.5f);
        area.sizeDelta = new Vector2(1920f, 1080f);

        bigRays = HudStyle.Img(area, "Rays", theme ? theme.rays : null, Color.white);
        raysGroup = bigRays.gameObject.AddComponent<CanvasGroup>();
        if (!bigRays.sprite) bigRays.enabled = false;

        panel = HudStyle.Img(area, "Panel", theme ? theme.panel : null, Color.white);
        if (!panel.sprite) { panel.sprite = HudStyle.Rounded(theme); panel.type = Image.Type.Sliced; panel.color = new Color(0.08f, 0.1f, 0.2f, 0.96f); }
        panelRt = HudStyle.Tl(panel.rectTransform, 380f, 200f, 1160f, 770f);
        panelGroup = panel.gameObject.AddComponent<CanvasGroup>();

        glow = HudStyle.Img(panelRt, "Winner Glow", theme ? theme.glow : null, Color.white);
        if (!glow.sprite) glow.enabled = false;
        foreach (var k in new[] { true, false })
        {
            var ring = HudStyle.Img(panelRt, k ? "Keeper Ring" : "Reaper Ring", theme ? theme.ring : null, new Color(1f, 1f, 1f, 0.55f));
            HudStyle.Tl(ring.rectTransform, (k ? 260f : 900f) - 125f, 438f, 250f, 55f);
            if (!ring.sprite) ring.enabled = false;
        }
        keeperHero = HudStyle.Img(panelRt, "Keeper", null, Color.white);
        reaperHero = HudStyle.Img(panelRt, "Reaper", null, Color.white);
        keeperHero.preserveAspect = reaperHero.preserveAspect = true;

        var tag = HudStyle.Img(panelRt, "Winner Tag", theme && theme.pill ? theme.pill : HudStyle.Rounded(theme), HudStyle.Gold, true);
        tagRt = HudStyle.Tl(tag.rectTransform, 190f, 110f, 140f, 34f);
        tagGroup = tag.gameObject.AddComponent<CanvasGroup>();
        var tagText = HudStyle.Text(tagRt, "Text", Label, 15f, new Color32(0x4A, 0x2C, 0x00, 0xFF), TextAlignmentOptions.Center, TextLook.Plain, 28f);
        tagText.text = "WINNER";
        HudStyle.Fill(tagText.rectTransform);

        // scores
        keeperScore = HudStyle.Text(panelRt, "Saved", Numbers, 110f, Color.white, TextAlignmentOptions.Center, TextLook.Holy);
        HudStyle.Tl(keeperScore.rectTransform, 395f, 228f, 190f, 136f);
        reaperScore = HudStyle.Text(panelRt, "Taken", Numbers, 110f, Color.white, TextAlignmentOptions.Center, TextLook.Hellfire);
        HudStyle.Tl(reaperScore.rectTransform, 575f, 228f, 190f, 136f);
        var saved = HudStyle.Text(panelRt, "Saved Label", Label, 15f, new Color(1f, 1f, 1f, 0.85f), TextAlignmentOptions.Center, TextLook.Plain, 30f);
        saved.text = "SAVED";
        savedLabel = saved;
        HudStyle.Tl(saved.rectTransform, 400f, 360f, 180f, 20f);
        var taken = HudStyle.Text(panelRt, "Taken Label", Label, 15f, new Color(1f, 1f, 1f, 0.85f), TextAlignmentOptions.Center, TextLook.Plain, 30f);
        taken.text = "TAKEN";
        takenLabel = taken;
        HudStyle.Tl(taken.rectTransform, 580f, 360f, 180f, 20f);

        // names
        keeperName = HudStyle.Text(panelRt, "Keeper Name", Heading, 32f, Color.white, TextAlignmentOptions.Center, TextLook.Holy, 8f);
        keeperName.text = "KEEPER";
        HudStyle.Tl(keeperName.rectTransform, 110f, 492f, 300f, 40f);
        keeperPlayer = HudStyle.Text(panelRt, "Keeper Player", Label, 17f, new Color(1f, 1f, 1f, 0.72f), TextAlignmentOptions.Center, TextLook.Plain);
        keeperPlayer.text = "Player 1";
        HudStyle.Tl(keeperPlayer.rectTransform, 110f, 532f, 300f, 24f);
        reaperName = HudStyle.Text(panelRt, "Reaper Name", Heading, 32f, Color.white, TextAlignmentOptions.Center, TextLook.Hellfire, 8f);
        reaperName.text = "REAPER";
        HudStyle.Tl(reaperName.rectTransform, 750f, 492f, 300f, 40f);
        reaperPlayer = HudStyle.Text(panelRt, "Reaper Player", Label, 17f, new Color(1f, 1f, 1f, 0.72f), TextAlignmentOptions.Center, TextLook.Plain);
        reaperPlayer.text = "Player 2";
        HudStyle.Tl(reaperPlayer.rectTransform, 750f, 532f, 300f, 24f);

        string[] labels = { "SOULS SAVED / TAKEN", "SLAPS LANDED", "TIMES STUNNED", "BEST SOUL STREAK" };
        for (int i = 0; i < rows.Length; i++) rows[i] = BuildRow(i, labels[i]);

        footer = HudStyle.Text(panelRt, "Footer", Label, 13f, new Color(1f, 1f, 1f, 0.5f), TextAlignmentOptions.Center, TextLook.Plain, 24f);
        HudStyle.Tl(footer.rectTransform, 0f, 728f, 1160f, 20f);

        // banner, flavour line, why it ended
        banner = HudStyle.Img(panelRt, "Banner", null, Color.white);
        bannerRt = HudStyle.Tl(banner.rectTransform, 160f, -94f, 840f, 159f);
        bannerHome = bannerRt.anchoredPosition;
        bannerGroup = banner.gameObject.AddComponent<CanvasGroup>();
        bannerText = HudStyle.Text(bannerRt, "Title", Display, 60f, Color.white, TextAlignmentOptions.Center, TextLook.Holy, 2f);
        HudStyle.Tl(bannerText.rectTransform, 120f, 22f, 600f, 104f);
        flavour = HudStyle.Text(panelRt, "Flavour", Label, 22f, new Color(1f, 1f, 1f, 0.94f), TextAlignmentOptions.Center, TextLook.Neutral);
        flavour.fontStyle = FontStyles.Italic;
        HudStyle.Tl(flavour.rectTransform, 0f, 60f, 1160f, 30f);
        reason = HudStyle.Text(panelRt, "Reason", Label, 15f, new Color(1f, 1f, 1f, 0.62f), TextAlignmentOptions.Center, TextLook.Plain, 14f);
        HudStyle.Tl(reason.rectTransform, 0f, 90f, 1160f, 20f);

        // buttons
        var buttons = HudStyle.Fill(HudStyle.Group(area, "Buttons"));
        buttonsGroup = buttons.gameObject.AddComponent<CanvasGroup>();
        rematch = MakeButton(buttons, "Rematch", "REMATCH", 730f, () => Choose(true));
        menu = MakeButton(buttons, "Main Menu", "MAIN MENU", 1190f, () => Choose(false));
        rematch.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = menu, selectOnLeft = menu };
        menu.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = rematch, selectOnRight = rematch };
    }

    Row BuildRow(int i, string label)
    {
        var row = new Row();
        row.rt = HudStyle.Tl(HudStyle.Group(panelRt, "Row " + label), 170f, 556f + i * 38f, 820f, 34f);
        row.home = row.rt.anchoredPosition;
        row.group = row.rt.gameObject.AddComponent<CanvasGroup>();
        if (i % 2 == 0)
        {
            var stripe = HudStyle.Img(row.rt, "Stripe", HudStyle.Rounded(theme), new Color(1f, 1f, 1f, 0.05f), true);
            HudStyle.Fill(stripe.rectTransform);
        }
        row.left = HudStyle.Text(row.rt, "Keeper", Numbers, 22f, Color.white, TextAlignmentOptions.Center, TextLook.Plain);
        HudStyle.Tl(row.left.rectTransform, 20f, 0f, 80f, 34f);
        row.right = HudStyle.Text(row.rt, "Reaper", Numbers, 22f, Color.white, TextAlignmentOptions.Center, TextLook.Plain);
        HudStyle.Tl(row.right.rectTransform, 720f, 0f, 80f, 34f);
        var text = HudStyle.Text(row.rt, "Label", Label, 14f, new Color(1f, 1f, 1f, 0.82f), TextAlignmentOptions.Center, TextLook.Plain, 22f);
        text.text = label;
        row.label = text;
        HudStyle.Tl(text.rectTransform, 260f, 0f, 300f, 34f);
        row.leftBar = Bar(row.rt, true);
        row.rightBar = Bar(row.rt, false);
        return row;
    }

    RectTransform Bar(RectTransform row, bool keeper)
    {
        var sprite = theme && theme.pill ? theme.pill : HudStyle.Rounded(theme);
        var bar = HudStyle.Img(row, keeper ? "Keeper Bar" : "Reaper Bar", sprite, keeper ? HudStyle.KeeperBlue : HudStyle.ReaperRed, true);
        var rt = bar.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(keeper ? 0f : 1f, 0.5f);
        rt.anchoredPosition = new Vector2(keeper ? 110f : 710f, -17f);
        rt.sizeDelta = new Vector2(0f, 4f);
        return rt;
    }

    Button MakeButton(RectTransform parent, string name, string label, float centreX, Action onClick)
    {
        var sprite = theme ? theme.buttonNormal : null;
        var img = HudStyle.Img(parent, name, sprite, Color.white);
        img.raycastTarget = true;
        if (!sprite) { img.sprite = HudStyle.Rounded(theme); img.type = Image.Type.Sliced; img.color = new Color(0.1f, 0.12f, 0.24f, 0.95f); }
        HudStyle.Tl(img.rectTransform, centreX - 220f, 1028f - 48.5f, 440f, 97f);
        var b = img.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        if (theme && theme.buttonHover)
        {
            b.transition = Selectable.Transition.SpriteSwap;
            b.spriteState = new SpriteState
            {
                highlightedSprite = theme.buttonHover,
                selectedSprite = theme.buttonHover,
                pressedSprite = theme.buttonPressed ? theme.buttonPressed : theme.buttonHover,
                disabledSprite = theme.buttonDisabled,
            };
        }
        var text = HudStyle.Text(img.rectTransform, "Label", Heading, 24f, Color.white, TextAlignmentOptions.Center, TextLook.Neutral, 16f);
        text.text = label;
        HudStyle.Stretch(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(60f, 0f), new Vector2(-60f, -2f));
        b.onClick.AddListener(() => onClick());
        return b;
    }

    // ---------------------------------------------------------------- show / hide

    public void Show(MatchResult r, Key restart = Key.R, float freezeSeconds = Freeze)
    {
        result = r;
        freeze = Mathf.Max(0f, freezeSeconds);
        restartKey = restart;
        Showing = true;
        chosen = buttonsLive = false;
        age = 0f;
        gameObject.SetActive(true);
        var group = GetComponent<CanvasGroup>();
        group.alpha = 1f;
        group.blocksRaycasts = true;
        group.interactable = true;

        bool keeperWins = r.winner != MatchSide.Evil;
        var sideLight = keeperWins ? HudStyle.KeeperLight : HudStyle.ReaperLight;
        banner.sprite = theme ? (keeperWins ? theme.ribbonKeeper : theme.ribbonReaper) : null;
        banner.enabled = banner.sprite;
        bannerText.text = keeperWins ? "KEEPERS WIN" : "REAPERS WIN";
        HudStyle.SetLook(bannerText, keeperWins ? TextLook.Holy : TextLook.Hellfire);
        flavour.text = r.solo
            ? (keeperWins ? "Every soul found. The gate is yours." : "The Reapers caught the Keeper.")
            : (keeperWins ? "The souls are safe... for now." : "The souls belong to the Reaper now.");
        savedLabel.text = r.solo ? "FOUND" : "SAVED";
        takenLabel.text = r.solo ? "LEFT" : "TAKEN";
        rows[0].label.text = r.solo ? "SOULS FOUND / LEFT" : "SOULS SAVED / TAKEN";
        reaperPlayer.text = r.solo ? "Guards" : "Player 2";
        reason.text = string.IsNullOrEmpty(r.reason) ? "" : r.reason.ToUpperInvariant();

        PlaceHero(keeperHero, true, keeperWins);
        PlaceHero(reaperHero, false, !keeperWins);
        float wx = keeperWins ? 260f : 900f;
        HudStyle.Tl(glow.rectTransform, wx - 230f, 303f - 230f, 460f, 460f);
        glow.color = new Color(sideLight.r, sideLight.g, sideLight.b, 0.55f);
        HudStyle.Tl(bigRays.rectTransform, 380f + wx - 640f, 200f + 303f - 640f, 1280f, 1280f);
        bigRays.color = new Color(sideLight.r, sideLight.g, sideLight.b, 0.32f);
        HudStyle.Tl(tagRt, keeperWins ? 190f : 830f, 110f, 140f, 34f);

        keeperName.alpha = keeperWins ? 1f : 0.6f;
        keeperPlayer.alpha = keeperWins ? 0.75f : 0.45f;
        reaperName.alpha = keeperWins ? 0.6f : 1f;
        reaperPlayer.alpha = keeperWins ? 0.45f : 0.75f;
        keeperScore.color = keeperWins ? Color.white : new Color(0.78f, 0.84f, 0.95f, 0.85f);
        reaperScore.color = keeperWins ? new Color(0.93f, 0.7f, 0.78f, 0.85f) : Color.white;
        keeperScore.text = reaperScore.text = "0";

        SetRow(rows[0], r.saved, r.taken, true);
        SetRow(rows[1], r.keeperSlaps, r.reaperSlaps, true);
        SetRow(rows[2], r.keeperStunned, r.reaperStunned, false);
        SetRow(rows[3], r.keeperStreak, r.reaperStreak, true);

        int secs = Mathf.CeilToInt(r.huntSeconds);
        string keys = Gamepad.all.Count > 0 ? $"{restartKey} / START" : restartKey.ToString();
        footer.text = $"MATCH {secs / 60:00}:{secs % 60:00}  ·  {r.total} SOULS {(r.solo ? "HIDDEN" : "RELEASED")}  ·  {keys} = REMATCH";

        rematch.interactable = menu.interactable = true;
        if (Application.isPlaying) EnsureEventSystem();
        Apply(0f);
    }

    public void Hide()
    {
        Showing = false;
        gameObject.SetActive(false);
    }

    void PlaceHero(Image img, bool keeper, bool winner)
    {
        Sprite s = null;
        if (theme)
            s = keeper ? (winner ? theme.heroKeeper : theme.heroKeeperGrey ? theme.heroKeeperGrey : theme.heroKeeper)
                       : (winner ? theme.heroReaper : theme.heroReaperGrey ? theme.heroReaperGrey : theme.heroReaper);
        img.sprite = s;
        img.enabled = s;
        img.color = winner ? Color.white : new Color(0.85f, 0.85f, 0.88f, 0.9f);
        float h = winner ? 330f : 270f;
        float w = Mathf.Min(HudStyle.WidthAt(s, h), 330f);
        h = s ? Mathf.Min(h, w * s.rect.height / s.rect.width) : h;
        float cx = keeper ? 260f : 900f;
        HudStyle.Tl(img.rectTransform, cx - w * 0.5f, 466f - h, w, h);         // feet on the ring
    }

    static void SetRow(Row row, int keeper, int reaper, bool higherIsBetter)
    {
        row.left.text = keeper.ToString();
        row.right.text = reaper.ToString();
        int top = Mathf.Max(1, Mathf.Max(keeper, reaper));
        row.leftBar.sizeDelta = new Vector2(150f * keeper / top, 4f);
        row.rightBar.sizeDelta = new Vector2(150f * reaper / top, 4f);
        bool keeperBetter = keeper != reaper && (keeper > reaper) == higherIsBetter;
        bool reaperBetter = keeper != reaper && !keeperBetter;
        row.left.color = keeperBetter ? HudStyle.KeeperLight : new Color(1f, 1f, 1f, keeper == reaper ? 0.85f : 0.6f);
        row.right.color = reaperBetter ? HudStyle.ReaperLight : new Color(1f, 1f, 1f, keeper == reaper ? 0.85f : 0.6f);
        row.left.fontSize = keeperBetter ? 24f : 22f;
        row.right.fontSize = reaperBetter ? 24f : 22f;
    }

    void Choose(bool again)
    {
        if (!Showing || chosen) return;
        chosen = true;
        rematch.interactable = menu.interactable = false;
        click?.Invoke();
        if (again) Rematch?.Invoke();
        else MainMenu?.Invoke();
    }

    // ---------------------------------------------------------------- animation

    void Update()
    {
        if (!Showing) return;
        Step(Time.unscaledDeltaTime);
        if (!Application.isPlaying || age < freeze || chosen) return;
        var kb = Keyboard.current;
        bool again = kb != null && restartKey != Key.None && kb[restartKey].wasPressedThisFrame;
        foreach (var g in Gamepad.all) again |= g.startButton.wasPressedThisFrame;
        if (again) Choose(true);
    }

    /// <summary>Advances the reveal (Update; tests and previews drive it directly).</summary>
    public void Step(float dt)
    {
        if (!Showing) return;
        age += dt;
        Apply(age);
        if (!buttonsLive && age >= ButtonsAt + 0.3f)
        {
            buttonsLive = true;
            buttonsGroup.interactable = true;
            buttonsGroup.blocksRaycasts = true;
            if (Application.isPlaying) Focus();
        }
    }

    void Apply(float t)
    {
        HudStyle.SetAlpha(dim, 0.55f * Mathf.Clamp01((t - DimAt) / 0.3f));

        float p = Mathf.Clamp01((t - PanelAt) / 0.35f);
        panelGroup.alpha = Mathf.Clamp01(p * 2.5f);
        panelRt.localScale = Vector3.one * Mathf.LerpUnclamped(0.85f, 1f, HudStyle.OutBack(p));

        float b = Mathf.Clamp01((t - BannerAt) / 0.3f);
        bannerGroup.alpha = Mathf.Clamp01(b * 3f);
        bannerRt.anchoredPosition = bannerHome + new Vector2(0f, 70f * (1f - HudStyle.OutBack(b)));

        float c = HudStyle.OutCubic((t - CountAt) / 0.8f);
        HudStyle.SetText(keeperScore, Mathf.RoundToInt(result.saved * c).ToString());
        HudStyle.SetText(reaperScore, Mathf.RoundToInt(result.taken * c).ToString());

        float g = Mathf.Clamp01((t - RaysAt) / 0.35f);
        raysGroup.alpha = g;
        bigRays.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -t * 6f);
        glow.color = new Color(glow.color.r, glow.color.g, glow.color.b, 0.55f * g);
        tagGroup.alpha = g;
        tagRt.localScale = Vector3.one * Mathf.Lerp(1.4f, 1f, HudStyle.OutCubic(g));

        for (int i = 0; i < rows.Length; i++)
        {
            float k = Mathf.Clamp01((t - RowsAt - i * 0.06f) / 0.25f);
            rows[i].group.alpha = k;
            rows[i].rt.anchoredPosition = rows[i].home + new Vector2(14f * (1f - HudStyle.OutCubic(k)), 0f);
        }

        float u = Mathf.Clamp01((t - ButtonsAt) / 0.3f);
        buttonsGroup.alpha = u;
        if (u < 1f) { buttonsGroup.interactable = false; buttonsGroup.blocksRaycasts = false; }
    }

    /// <summary>Default focus REMATCH so a controller / the keyboard can pick straight away.</summary>
    void Focus()
    {
        EnsureEventSystem();
        var es = EventSystem.current;
        if (es) es.SetSelectedGameObject(rematch.gameObject);
    }

    static void EnsureEventSystem()
    {
        if (EventSystem.current || FindAnyObjectByType<EventSystem>()) return;
        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
    }
}
