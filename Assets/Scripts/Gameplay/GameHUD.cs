using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Split-screen HUD, built at runtime (no prefab to keep in sync): a panel per player in its own half
/// (name, hearts, score, controls), a divider between the halves, the 3-2-1-GO countdown and the winner card.
/// </summary>
public class GameHUD : MonoBehaviour
{
    [SerializeField] Color angelColor = new Color(0.62f, 0.86f, 1f);
    [SerializeField] Color evilColor = new Color(1f, 0.42f, 0.62f);
    [SerializeField] Color gold = new Color(1f, 0.83f, 0.35f);

    class PlayerPanel
    {
        public readonly List<Image> hearts = new List<Image>();
        public readonly List<float> punch = new List<float>();
        public TMP_Text score, extra;
        public int lives = -1;
    }

    PlayerPanel angel, evil;
    TMP_Text center, centerSub, centerHint;
    RectTransform centerCard;
    float centerPunch;
    Sprite heartFill, heartEdge, rounded;

    void Awake() => Build();

    void Build()
    {
        if (angel != null) return;
        heartFill = HeartSprite(false);
        heartEdge = HeartSprite(true);
        rounded = RoundedSprite();

        var canvasGo = new GameObject("HUD Canvas", typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        var root = canvasGo.GetComponent<RectTransform>();

        // divider between the two halves
        var divider = NewImage(root, "Divider", new Color(0.06f, 0.05f, 0.09f, 1f));
        Stretch(divider.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(-4f, 0f), new Vector2(4f, 0f));
        var line = NewImage(divider.rectTransform, "Line", new Color(gold.r, gold.g, gold.b, 0.65f));
        Stretch(line.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(-1f, 0f), new Vector2(1f, 0f));

        angel = Panel(root, "ANGEL", angelColor, true, "<b>WASD</b> move   <b>L-Shift</b> slap   <b>E</b> drop offering");
        evil = Panel(root, "EVIL", evilColor, false, "<b>Arrows</b> move   <b>R-Shift</b> slap   eat her offerings");

        // centre card: countdown / winner
        var card = NewImage(root, "Center Card", new Color(0.05f, 0.04f, 0.08f, 0.78f));
        card.sprite = rounded; card.type = Image.Type.Sliced;
        centerCard = card.rectTransform;
        centerCard.anchorMin = centerCard.anchorMax = new Vector2(0.5f, 0.56f);
        centerCard.sizeDelta = new Vector2(760f, 300f);
        center = NewText(centerCard, "Title", 120f, Color.white, TextAlignmentOptions.Center);
        Stretch(center.rectTransform, new Vector2(0f, 0.4f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        centerSub = NewText(centerCard, "Sub", 34f, new Color(1f, 1f, 1f, 0.85f), TextAlignmentOptions.Center);
        Stretch(centerSub.rectTransform, new Vector2(0f, 0.18f), new Vector2(1f, 0.42f), Vector2.zero, Vector2.zero);
        centerHint = NewText(centerCard, "Hint", 26f, gold, TextAlignmentOptions.Center);
        Stretch(centerHint.rectTransform, new Vector2(0f, 0.02f), new Vector2(1f, 0.2f), Vector2.zero, Vector2.zero);
        HideCenter();
    }

    PlayerPanel Panel(RectTransform root, string title, Color color, bool left, string controls)
    {
        var p = new PlayerPanel();
        var bg = NewImage(root, title + " Panel", new Color(0.05f, 0.04f, 0.08f, 0.62f));
        bg.sprite = rounded; bg.type = Image.Type.Sliced;
        var rt = bg.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(left ? 0f : 1f, 1f);
        rt.pivot = new Vector2(left ? 0f : 1f, 1f);
        rt.anchoredPosition = new Vector2(left ? 28f : -28f, -24f);
        rt.sizeDelta = new Vector2(430f, 168f);

        var name = NewText(rt, "Name", 40f, color, left ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.TopRight);
        name.text = $"<b>{title}</b>";
        Stretch(name.rectTransform, Vector2.zero, Vector2.one, new Vector2(22f, 0f), new Vector2(-22f, -12f));

        for (int i = 0; i < 3; i++)
        {
            var slot = new GameObject("Heart " + (i + 1), typeof(RectTransform)).GetComponent<RectTransform>();
            slot.SetParent(rt, false);
            slot.anchorMin = slot.anchorMax = new Vector2(left ? 0f : 1f, 1f);
            slot.sizeDelta = new Vector2(46f, 46f);
            slot.anchoredPosition = new Vector2(left ? 46f + i * 54f : -46f - i * 54f, -88f);
            var edge = NewImage(slot, "Edge", new Color(0.12f, 0.06f, 0.1f, 1f)); edge.sprite = heartEdge;
            Stretch(edge.rectTransform, Vector2.zero, Vector2.one, new Vector2(-4f, -4f), new Vector2(4f, 4f));
            var fill = NewImage(slot, "Fill", new Color(1f, 0.28f, 0.38f)); fill.sprite = heartFill;
            Stretch(fill.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            p.hearts.Add(fill);
            p.punch.Add(0f);
        }

        p.score = NewText(rt, "Score", 30f, Color.white, left ? TextAlignmentOptions.Right : TextAlignmentOptions.Left);
        Stretch(p.score.rectTransform, new Vector2(left ? 0.45f : 0f, 0.3f), new Vector2(left ? 1f : 0.55f, 0.62f), new Vector2(left ? 0f : 22f, 0f), new Vector2(left ? -22f : 0f, 0f));
        p.extra = NewText(rt, "Extra", 22f, new Color(1f, 1f, 1f, 0.75f), left ? TextAlignmentOptions.Right : TextAlignmentOptions.Left);
        Stretch(p.extra.rectTransform, new Vector2(left ? 0.45f : 0f, 0.62f), new Vector2(left ? 1f : 0.55f, 0.82f), new Vector2(left ? 0f : 22f, 0f), new Vector2(left ? -22f : 0f, 0f));
        var help = NewText(rt, "Controls", 19f, new Color(1f, 1f, 1f, 0.6f), left ? TextAlignmentOptions.BottomLeft : TextAlignmentOptions.BottomRight);
        help.text = controls;
        Stretch(help.rectTransform, Vector2.zero, Vector2.one, new Vector2(22f, 12f), new Vector2(-22f, 0f));
        return p;
    }

    // ---------------------------------------------------------------- updates from the game

    public void SetLives(MatchSide side, int lives)
    {
        Build();
        var p = side == MatchSide.Angel ? angel : evil;
        for (int i = 0; i < p.hearts.Count; i++)
        {
            bool full = i < lives;
            p.hearts[i].color = full ? new Color(1f, 0.28f, 0.38f) : new Color(0.35f, 0.3f, 0.36f, 0.45f);
            if (p.lives > lives && i >= lives && i < p.lives) p.punch[i] = 1f;       // the heart just lost
        }
        p.lives = lives;
    }

    public void SetScore(MatchSide side, int score, int target)
    {
        Build();
        var p = side == MatchSide.Angel ? angel : evil;
        p.score.text = side == MatchSide.Angel ? $"Blessed <b>{score}</b> / {target}" : $"Devoured <b>{score}</b> / {target}";
    }

    public void SetOfferings(int active, int max, bool ready)
    {
        Build();
        angel.extra.text = $"Offerings {active}/{max}" + (ready ? "" : "  <color=#ffffff66>…</color>");
        evil.extra.text = active > 0 ? $"<color=#ffd35a>{active} offering{(active == 1 ? "" : "s")} to find</color>" : "nothing to eat yet";
    }

    public void ShowCountdown(int n)
    {
        Build();
        if (center.text != n.ToString()) centerPunch = 1f;
        ShowCenter(n.ToString(), "Angel: bless your offerings   Evil: devour them", "slap = they lose a life");
    }

    public void ShowGo()
    {
        Build();
        centerPunch = 1f;
        ShowCenter("GO!", "", "");
        CancelInvoke(nameof(HideCenter));
        Invoke(nameof(HideCenter), 0.9f);
    }

    public void ShowWinner(MatchSide winner, string reason, string restartKey)
    {
        Build();
        CancelInvoke(nameof(HideCenter));
        centerPunch = 1f;
        var col = winner == MatchSide.Angel ? angelColor : evilColor;
        ShowCenter($"<color=#{ColorUtility.ToHtmlStringRGB(col)}>{(winner == MatchSide.Angel ? "ANGEL" : "EVIL")} WINS!</color>", reason, $"press {restartKey} to play again");
    }

    public void HideCenter()
    {
        if (centerCard) centerCard.gameObject.SetActive(false);
    }

    void ShowCenter(string title, string sub, string hint)
    {
        centerCard.gameObject.SetActive(true);
        center.text = $"<b>{title}</b>";
        centerSub.text = sub;
        centerHint.text = hint;
    }

    void Update()
    {
        if (angel == null) return;
        float dt = Time.unscaledDeltaTime;
        if (centerCard && centerCard.gameObject.activeSelf)
        {
            centerPunch = Mathf.Max(0f, centerPunch - dt * 3f);
            centerCard.localScale = Vector3.one * (1f + 0.18f * centerPunch * centerPunch);
        }
        foreach (var p in new[] { angel, evil })
            for (int i = 0; i < p.hearts.Count; i++)
            {
                p.punch[i] = Mathf.Max(0f, p.punch[i] - dt * 2.2f);
                float k = p.punch[i];
                p.hearts[i].rectTransform.localScale = Vector3.one * (1f + 0.6f * k * k);
                p.hearts[i].rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(k * 20f) * 14f * k);
            }
    }

    // ---------------------------------------------------------------- ui helpers

    static Image NewImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    static TMP_Text NewText(Transform parent, string name, float size, Color color, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
        t.richText = true;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.outlineWidth = 0.18f;
        t.outlineColor = new Color32(20, 10, 24, 255);
        return t;
    }

    static void Stretch(RectTransform rt, Vector2 min, Vector2 max, Vector2 offMin, Vector2 offMax)
    {
        rt.anchorMin = min; rt.anchorMax = max; rt.offsetMin = offMin; rt.offsetMax = offMax;
    }

    /// <summary>A heart shape (or its slightly larger silhouette for the outline), anti-aliased, white.</summary>
    static Sprite HeartSprite(bool edge)
    {
        const int n = 96;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color32[n * n];
        float s = edge ? 1.0f : 0.9f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int hits = 0;
                for (int sy = 0; sy < 4; sy++)
                    for (int sx = 0; sx < 4; sx++)
                    {
                        float u = ((x + (sx + 0.5f) / 4f) / n * 2f - 1f) * 1.3f / s;
                        float v = ((y + (sy + 0.5f) / 4f) / n * 2f - 1f) * 1.3f / s + 0.15f;
                        float a = u * u + v * v - 1f;
                        if (a * a * a - u * u * v * v * v <= 0f) hits++;
                    }
                px[y * n + x] = new Color32(255, 255, 255, (byte)(hits * 255 / 16));
            }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
    }

    /// <summary>A white rounded rectangle for 9-sliced panels.</summary>
    static Sprite RoundedSprite()
    {
        const int n = 64, r = 22;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = Mathf.Max(0f, Mathf.Max(r - x - 0.5f, x + 0.5f - (n - r)));
                float dy = Mathf.Max(0f, Mathf.Max(r - y - 0.5f, y + 0.5f - (n - r)));
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(r - d + 0.5f) * 255));
            }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
    }
}
