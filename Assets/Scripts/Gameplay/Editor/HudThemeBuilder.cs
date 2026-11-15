using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Builds Assets/Resources/HudTheme.asset from the team's SoulKeepers art (staging's "2D Assets" / "Audio"), the static
/// font cuts in Assets/UI/Fonts and the generated shapes in Assets/UI/Sprites (whose import settings it also sets:
/// sprite, no mipmaps, 9-slice borders on the pill / rounded shapes). Safe to run again; it only fills in references.
/// </summary>
public static class HudThemeBuilder
{
    const string ThemePath = "Assets/Resources/HudTheme.asset";
    const string Art = "Assets/2D Assets/";
    const string Hud = "Assets/2D Assets/HUD Assets/Sprites/";
    const string Ui = "Assets/UI/Sprites/";
    const string Fonts = "Assets/UI/Fonts/";

    [MenuItem("GameJam/Build HUD Theme")]
    public static HudTheme Build()
    {
        ImportShapes();

        var theme = AssetDatabase.LoadAssetAtPath<HudTheme>(ThemePath);
        bool create = !theme;
        if (create) theme = ScriptableObject.CreateInstance<HudTheme>();

        theme.display = Load<Font>("Assets/2D Assets/HUD Assets/Fonts/CinzelDecorative-Bold.ttf");
        theme.heading = Load<Font>(Fonts + "Cinzel-Bold.ttf");
        theme.numbers = Load<Font>(Fonts + "Nunito-Black.ttf");
        theme.label = Load<Font>(Fonts + "Nunito-ExtraBold.ttf");
        theme.textShader = Shader.Find("TextMeshPro/Distance Field");

        theme.podKeeper = SpriteAt(Hud + "SK_ScorePod_Keeper.png");
        theme.podReaper = SpriteAt(Hud + "SK_ScorePod_Reaper.png");
        theme.timerNormal = SpriteAt(Hud + "SK_Timer_Normal.png");
        theme.timerWarning = SpriteAt(Hud + "SK_Timer_Warning.png");
        theme.ribbonKeeper = SpriteAt(Hud + "SK_Ribbon_Keeper.png");
        theme.ribbonReaper = SpriteAt(Hud + "SK_Ribbon_Reaper.png");
        theme.portraitKeeper = SpriteAt(Art + "AngelIcon.png");
        theme.portraitReaper = SpriteAt(Art + "EvilIcon.png");
        theme.heroKeeper = SpriteAt(Art + "SK_Angel_Hero.png");
        theme.heroReaper = SpriteAt(Art + "SK_Evil_Hero.png");
        theme.buttonNormal = SpriteAt(Art + "SK_Button_Normal.png");
        theme.buttonHover = SpriteAt(Art + "SK_Button_Hover.png");
        theme.buttonPressed = SpriteAt(Art + "SK_Button_Pressed.png");
        theme.buttonDisabled = SpriteAt(Art + "SK_Button_Disabled.png");

        theme.slapIcon = SpriteAt(Ui + "SK_SlapIcon.png");
        theme.panel = SpriteAt(Ui + "SK_Result_Panel.png");
        theme.heroKeeperGrey = SpriteAt(Ui + "SK_Angel_Hero_Grey.png");
        theme.heroReaperGrey = SpriteAt(Ui + "SK_Evil_Hero_Grey.png");
        theme.pill = SpriteAt(Ui + "SK_Pill.png");
        theme.circle = SpriteAt(Ui + "SK_Circle.png");
        theme.rounded = SpriteAt(Ui + "SK_Rounded.png");
        theme.glow = SpriteAt(Ui + "SK_Glow.png");
        theme.rays = SpriteAt(Ui + "SK_Rays.png");
        theme.ring = SpriteAt(Ui + "SK_Ring.png");
        theme.vignette = SpriteAt(Ui + "SK_Vignette.png");

        theme.slapVfxKeeper = Load<GameObject>("Assets/Prefabs/SlapVFX_Good.prefab");
        theme.slapVfxReaper = Load<GameObject>("Assets/Prefabs/SlapVFX_Evil.prefab");
        theme.mainCharacter = Load<GameObject>("Assets/Prefabs/Characters/MainChar/MainChar.prefab");
        theme.emoteHappy = SpriteAt(Ui + "SK_Emote_Happy.png");
        theme.emoteSad = SpriteAt(Ui + "SK_Emote_Sad.png");
        theme.mainCharacterSadFace = Load<Texture2D>("Assets/UI/Textures/MC_Body_Sad.png");

        theme.music = Load<AudioClip>("Assets/Audio/Gameplay intro music.mp3");
        theme.click = Load<AudioClip>("Assets/Audio/Main menu Gameplay sound.mp3");

        if (create)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ThemePath));
            AssetDatabase.CreateAsset(theme, ThemePath);
        }
        EditorUtility.SetDirty(theme);
        AssetDatabase.SaveAssets();
        return theme;
    }

    /// <summary>Sprite import settings for the generated shapes (sprite, single, no mips, 9-slice where it applies).</summary>
    static void ImportShapes()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Ui.TrimEnd('/') }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (!(AssetImporter.GetAtPath(path) is TextureImporter ti)) continue;
            var name = Path.GetFileNameWithoutExtension(path);
            var border = name == "SK_Pill" ? new Vector4(32, 32, 32, 32)
                       : name == "SK_Rounded" ? new Vector4(28, 28, 28, 28)
                       : Vector4.zero;
            bool changed = ti.textureType != TextureImporterType.Sprite
                || ti.spriteImportMode != SpriteImportMode.Single
                || ti.mipmapEnabled
                || !ti.alphaIsTransparency
                || ti.spriteBorder != border
                || ti.maxTextureSize != 2048;
            if (!changed) continue;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.spriteBorder = border;
            ti.maxTextureSize = 2048;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.SaveAndReimport();
        }
    }

    static T Load<T>(string path) where T : Object
    {
        var a = AssetDatabase.LoadAssetAtPath<T>(path);
        if (!a) Debug.LogWarning($"HudThemeBuilder: missing {typeof(T).Name} at {path}");
        return a;
    }

    /// <summary>The sprite in a texture (staging's art is imported as "Multiple" with a single slice).</summary>
    static Sprite SpriteAt(string path)
    {
        var s = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
        if (!s) Debug.LogWarning($"HudThemeBuilder: no sprite in {path}");
        return s;
    }
}
