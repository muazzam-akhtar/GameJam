using UnityEngine;

/// <summary>
/// The SoulKeepers look for the in-match HUD and the result screen: the team's art, fonts and sounds (staging's
/// "2D Assets" / "Audio") plus a few generated shapes (Assets/UI/Sprites). Kept in Resources so the runtime-built
/// HUD finds it without any scene wiring; whatever is missing falls back to plain shapes and the default font.
/// Rebuilt by GameJam ▸ Build HUD Theme (HudThemeBuilder).
/// </summary>
[CreateAssetMenu(menuName = "GameJam/HUD Theme", fileName = "HudTheme")]
public class HudTheme : ScriptableObject
{
    public const string ResourcePath = "HudTheme";

    [Header("Fonts (TTF - TextMeshPro font assets are made from them at runtime)")]
    [Tooltip("Cinzel Decorative Bold: logo, banners, big moments (KEEPERS WIN, TIME!, HUNT!).")]
    public Font display;
    [Tooltip("Cinzel Bold: headings, buttons, player names.")]
    public Font heading;
    [Tooltip("Nunito Black: timer, scores, numbers.")]
    public Font numbers;
    [Tooltip("Nunito ExtraBold: labels, body, tooltips.")]
    public Font label;
    [Tooltip("TextMeshPro/Distance Field (outline + glow presets); referenced so builds include it.")]
    public Shader textShader;

    [Header("Top HUD")]
    public Sprite podKeeper;
    public Sprite podReaper;
    public Sprite timerNormal;
    public Sprite timerWarning;
    public Sprite portraitKeeper;
    public Sprite portraitReaper;
    public Sprite slapIcon;

    [Header("Result screen")]
    public Sprite panel;
    public Sprite ribbonKeeper;
    public Sprite ribbonReaper;
    public Sprite heroKeeper;
    public Sprite heroReaper;
    public Sprite heroKeeperGrey;
    public Sprite heroReaperGrey;
    public Sprite buttonNormal;
    public Sprite buttonHover;
    public Sprite buttonPressed;
    public Sprite buttonDisabled;

    [Header("Shapes")]
    public Sprite pill;
    public Sprite circle;
    public Sprite rounded;
    public Sprite glow;
    public Sprite rays;
    public Sprite ring;
    public Sprite vignette;

    [Header("End-of-match story (versus)")]
    [Tooltip("The main character who comes in when a versus round ends.")]
    public GameObject mainCharacter;
    public Sprite emoteHappy;
    public Sprite emoteSad;
    [Tooltip("The main character's body texture with a sad face painted on (the sad ending).")]
    public Texture2D mainCharacterSadFace;

    [Header("Sound")]
    public AudioClip music;
    public AudioClip click;
    [Range(0f, 1f)] public float musicVolume = 0.45f;
    [Range(0f, 1f)] public float clickVolume = 0.9f;

    static HudTheme cached;

    /// <summary>The theme in Resources, or null if there is none.</summary>
    public static HudTheme Load()
    {
        if (!cached) cached = Resources.Load<HudTheme>(ResourcePath);
        return cached;
    }
}
