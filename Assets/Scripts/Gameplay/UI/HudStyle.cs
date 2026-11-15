using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

/// <summary>The TextMeshPro material presets of the SoulKeepers font guide.</summary>
public enum TextLook
{
    /// <summary>The font as it is (small labels on dark plates).</summary>
    Plain,
    /// <summary>UI / HUD: dark outline and a soft shadow (timer, scores).</summary>
    Neutral,
    /// <summary>Keeper text: navy outline, blue glow.</summary>
    Holy,
    /// <summary>Reaper text: deep red outline, ember glow.</summary>
    Hellfire,
}

/// <summary>
/// Shared look for the runtime-built HUD and result screen: the palette, TextMeshPro font assets made at runtime from
/// the theme's TTFs (no generated font files to keep in the repo), the font-guide material presets, and small
/// factories / layout helpers. Layout uses the mockups' 1920x1080 top-left pixel coordinates (<see cref="Tl"/>).
/// </summary>
public static class HudStyle
{
    public static readonly Color KeeperBlue = new Color32(0x4A, 0x9B, 0xE8, 0xFF);
    public static readonly Color KeeperLight = new Color32(0x9C, 0xD6, 0xFF, 0xFF);
    public static readonly Color ReaperRed = new Color32(0xC8, 0x20, 0x6A, 0xFF);
    public static readonly Color ReaperLight = new Color32(0xFF, 0xB0, 0x6E, 0xFF);
    public static readonly Color Gold = new Color32(0xFF, 0xD7, 0x6A, 0xFF);
    public static readonly Color Ink = new Color32(0x0E, 0x0C, 0x18, 0xFF);
    public static readonly Color HeartRed = new Color(1f, 0.28f, 0.38f);

    static readonly Dictionary<Font, TMP_FontAsset> fonts = new Dictionary<Font, TMP_FontAsset>();
    static readonly Dictionary<(TMP_FontAsset, TextLook), Material> looks = new Dictionary<(TMP_FontAsset, TextLook), Material>();
    static Sprite roundedFallback, heartFill, heartEdge;

    // ---------------------------------------------------------------- fonts

    const string Glyphs = " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~·…–—’";

    /// <summary>A dynamic SDF font asset for <paramref name="font"/> (made once, then cached), or TMP's default font.</summary>
    public static TMP_FontAsset FontAsset(Font font)
    {
        if (!font) return TMP_Settings.defaultFontAsset;
        if (fonts.TryGetValue(font, out var cached) && Intact(cached)) return cached;
        var fa = TMP_FontAsset.CreateFontAsset(font, 64, 7, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
        if (!fa) return TMP_Settings.defaultFontAsset;
        fa.name = font.name + " SDF (runtime)";
        fa.TryAddCharacters(Glyphs, out _);                                 // the usual set up front: no hitch mid-match
        var fallback = TMP_Settings.defaultFontAsset;                       // glyphs the font lacks (arrows etc.)
        if (fallback && fallback != fa) fa.fallbackFontAssetTable = new List<TMP_FontAsset> { fallback };
        // made at runtime, owned by nothing in a scene: keep the asset, its atlas and material through scene loads
        // and asset clean-ups (Resources.UnloadUnusedAssets would otherwise destroy the atlas under it)
        fa.hideFlags = HideFlags.DontSave;
        if (fa.material) fa.material.hideFlags = HideFlags.DontSave;
        foreach (var tex in fa.atlasTextures) if (tex) tex.hideFlags = HideFlags.DontSave;
        fonts[font] = fa;
        return fa;
    }

    static bool Intact(TMP_FontAsset fa)
    {
        if (!fa || !fa.material || fa.atlasTextures == null || fa.atlasTextures.Length == 0) return false;
        foreach (var tex in fa.atlasTextures) if (!tex) return false;
        return true;
    }

    /// <summary>The shared material for a font + preset (outline / glow / shadow from the font guide).</summary>
    public static Material Look(TMP_FontAsset fa, TextLook look)
    {
        if (!fa) return null;
        if (look == TextLook.Plain) return fa.material;
        if (looks.TryGetValue((fa, look), out var cached) && cached) return cached;

        ShaderUtilities.GetShaderPropertyIDs();                              // the IDs are filled in lazily
        var theme = HudTheme.Load();
        var shader = theme && theme.textShader ? theme.textShader : Shader.Find("TextMeshPro/Distance Field");
        var m = new Material(fa.material) { name = fa.name + " " + look, hideFlags = HideFlags.DontSave };
        if (shader) m.shader = shader;
        m.SetColor(ShaderUtilities.ID_FaceColor, Color.white);
        switch (look)
        {
            case TextLook.Neutral:
                Outline(m, 0.25f, Ink);
                m.EnableKeyword(ShaderUtilities.Keyword_Underlay);
                m.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.55f));
                m.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.6f);
                m.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.3f);
                break;
            case TextLook.Holy:
                Outline(m, 0.2f, new Color32(0x0E, 0x2A, 0x5A, 0xFF));
                Glow(m, new Color32(0x5F, 0xB0, 0xF2, 0x99));
                break;
            case TextLook.Hellfire:
                Outline(m, 0.2f, new Color32(0x3A, 0x00, 0x18, 0xFF));
                Glow(m, new Color32(0xFF, 0x6A, 0x2A, 0x99));
                break;
        }
        ShaderUtilities.UpdateShaderRatios(m);
        looks[(fa, look)] = m;
        return m;
    }

    static void Outline(Material m, float width, Color color)
    {
        m.EnableKeyword(ShaderUtilities.Keyword_Outline);
        m.SetFloat(ShaderUtilities.ID_OutlineWidth, width);
        m.SetColor(ShaderUtilities.ID_OutlineColor, color);
    }

    static void Glow(Material m, Color color)
    {
        m.EnableKeyword(ShaderUtilities.Keyword_Glow);
        m.SetColor(ShaderUtilities.ID_GlowColor, color);
        m.SetFloat(ShaderUtilities.ID_GlowOffset, 0f);
        m.SetFloat(ShaderUtilities.ID_GlowInner, 0.05f);
        m.SetFloat(ShaderUtilities.ID_GlowOuter, 0.45f);
        m.SetFloat(ShaderUtilities.ID_GlowPower, 0.75f);
    }

    // ---------------------------------------------------------------- factories

    public static TextMeshProUGUI Text(Transform parent, string name, Font font, float size, Color color,
        TextAlignmentOptions align, TextLook look = TextLook.Neutral, float spacing = 0f)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        var fa = FontAsset(font);
        if (fa) t.font = fa;
        SetLook(t, look);
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.characterSpacing = spacing;
        t.raycastTarget = false;
        t.richText = true;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        return t;
    }

    public static void SetLook(TMP_Text t, TextLook look)
    {
        var m = Look(t.font, look);
        if (m) t.fontSharedMaterial = m;
    }

    public static Image Img(Transform parent, string name, Sprite sprite, Color color, bool sliced = false)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        if (sprite && sliced && sprite.border != Vector4.zero) img.type = Image.Type.Sliced;
        return img;
    }

    public static RectTransform Group(Transform parent, string name)
    {
        var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        return rt;
    }

    /// <summary>The theme's rounded rectangle, or a generated one (9-sliced either way).</summary>
    public static Sprite Rounded(HudTheme theme)
    {
        if (theme && theme.rounded) return theme.rounded;
        if (!roundedFallback) roundedFallback = RoundedSprite();
        return roundedFallback;
    }

    public static Sprite Heart(bool edge)
    {
        if (edge) { if (!heartEdge) heartEdge = HeartSprite(true); return heartEdge; }
        if (!heartFill) heartFill = HeartSprite(false);
        return heartFill;
    }

    // ---------------------------------------------------------------- layout

    /// <summary>
    /// Places <paramref name="rt"/> by its top-left corner (<paramref name="x"/>, <paramref name="y"/>, y down) inside
    /// a parent laid out in mockup pixels; the pivot is the centre so punch / scale animations grow from the middle.
    /// </summary>
    public static RectTransform Tl(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(x + w * 0.5f, -(y + h * 0.5f));
        return rt;
    }

    public static RectTransform Stretch(RectTransform rt, Vector2 min, Vector2 max, Vector2 offMin, Vector2 offMax)
    {
        rt.anchorMin = min; rt.anchorMax = max; rt.offsetMin = offMin; rt.offsetMax = offMax;
        return rt;
    }

    public static RectTransform Fill(RectTransform rt) => Stretch(rt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

    /// <summary>Width that keeps the sprite's aspect at <paramref name="height"/>.</summary>
    public static float WidthAt(Sprite s, float height) => s ? height * s.rect.width / s.rect.height : height;

    // ---------------------------------------------------------------- easing

    public static float OutCubic(float t) { t = Mathf.Clamp01(t); return 1f - (1f - t) * (1f - t) * (1f - t); }

    public static float OutBack(float t)
    {
        t = Mathf.Clamp01(t);
        const float c1 = 1.70158f, c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    public static void SetText(TMP_Text t, string s)
    {
        if (t && t.text != s) t.text = s;
    }

    public static void SetAlpha(Graphic g, float a)
    {
        var c = g.color;
        if (!Mathf.Approximately(c.a, a)) { c.a = a; g.color = c; }
    }

    /// <summary>"01:24" with fixed-width digits so the timer never jitters.</summary>
    public static string Clock(int seconds)
    {
        seconds = Mathf.Max(0, seconds);
        return $"<mspace=0.58em>{seconds / 60:00}</mspace>:<mspace=0.58em>{seconds % 60:00}</mspace>";
    }

    // ---------------------------------------------------------------- generated fallbacks

    /// <summary>A heart shape (or its slightly larger silhouette for the outline), anti-aliased, white.</summary>
    static Sprite HeartSprite(bool edge)
    {
        const int n = 96;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
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
        var sprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        sprite.hideFlags = HideFlags.DontSave;
        return sprite;
    }

    /// <summary>A white rounded rectangle for 9-sliced panels.</summary>
    static Sprite RoundedSprite()
    {
        const int n = 64, r = 22;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
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
        var sprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
        sprite.hideFlags = HideFlags.DontSave;
        return sprite;
    }
}
