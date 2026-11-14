using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Main character.
///
/// States
///   Idle      flying hover - coat + ghost tail float, wings flap, halo spins (always running layers)
///   Walk      W
///   Walk back S
///   Run       Shift + W
///   Stung     loses the halo first; stung again loses coat, wings and sparkles (naked)
///   Power-up  the reverse: naked -> coat, wings, sparkles come back; then the halo comes back
///
/// Game code calls <see cref="Sting"/> (evil touches him) and <see cref="PowerUp"/> (angel helps him).
/// Q / E are test keys for those two. G shows / hides the on-screen key guide.
/// </summary>
[RequireComponent(typeof(Animator))]
public class MainCharController : MonoBehaviour
{
    public enum MoveState { Idle, Walk, WalkBack, Run }
    public enum Outfit { Naked, CoatAndWings, Full }

    [Header("Removable pieces")]
    [SerializeField] GameObject halo;
    [SerializeField] GameObject coat;
    [SerializeField] GameObject wings;
    [SerializeField] GameObject sparkles;
    [SerializeField] Outfit startOutfit = Outfit.Full;

    [Header("Movement keys")]
    [Tooltip("Drive Idle/Walk/Run from the keyboard. Turn off when game code calls SetMove itself.")]
    [SerializeField] bool keyboardControl = true;
    [SerializeField] Key walkKey = Key.W;
    [SerializeField] Key walkBackKey = Key.S;
    [SerializeField] Key runModifier = Key.LeftShift;

    [Header("Test keys (game code normally calls Sting / PowerUp)")]
    [SerializeField] Key stungKey = Key.Q;
    [SerializeField] Key powerUpKey = Key.E;

    [Header("Movement")]
    [SerializeField] bool moveInWorld = true;
    [SerializeField] float walkSpeed = 1.4f;
    [SerializeField] float walkBackSpeed = 1.0f;
    [SerializeField] float runSpeed = 3.2f;
    [Tooltip("How fast the movement speed eases in/out (m/s per second).")]
    [SerializeField] float acceleration = 8f;

    [Header("Pieces popping in / out")]
    [SerializeField] float popTime = 0.3f;
    [Tooltip("Delay before pieces appear on a power-up (top of the jump).")]
    [SerializeField] float powerUpAppearDelay = 0.35f;

    [Header("Key guide")]
    [SerializeField] bool showKeyGuide = true;
    [SerializeField] Key guideKey = Key.G;

    static readonly int SpeedParam = Animator.StringToHash("Speed");
    static readonly int StungTrigger = Animator.StringToHash("Stung");
    static readonly int PowerUpTrigger = Animator.StringToHash("PowerUp");

    /// <summary>Raised whenever the outfit changes (after a sting or a power-up).</summary>
    public event Action<Outfit> OutfitChanged;

    public MoveState Move { get; private set; }
    public Outfit CurrentOutfit { get; private set; }

    Animator animator;
    float currentSpeed;
    Piece haloPiece, coatPiece, wingsPiece, sparklesPiece;
    GUIStyle boxStyle, labelStyle, lineStyle;

    // A removable piece: its renderer object plus the bones it collapses into while popping.
    class Piece
    {
        public GameObject renderer;
        public Transform[] bones;
        public float progress = 1f, goal = 1f, delay;
    }

    void Awake()
    {
        animator = GetComponent<Animator>();
        haloPiece = MakePiece(halo, "halo");
        coatPiece = MakePiece(coat, "coat");
        wingsPiece = MakePiece(wings, "wing.L", "wing.R");
        sparklesPiece = MakePiece(sparkles, "sparkles");
        ApplyOutfit(startOutfit, instant: true);
    }

    Piece MakePiece(GameObject go, params string[] boneNames)
    {
        var p = new Piece { renderer = go, bones = new Transform[boneNames.Length] };
        var all = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < boneNames.Length; i++)
            p.bones[i] = Array.Find(all, t => t.name == boneNames[i]);
        return p;
    }

    void Update()
    {
        HandleKeys();

        float target = Move switch { MoveState.Walk => walkSpeed, MoveState.Run => runSpeed, MoveState.WalkBack => -walkBackSpeed, _ => 0f };
        currentSpeed = Mathf.MoveTowards(currentSpeed, target, acceleration * Time.deltaTime);
        if (moveInWorld && Mathf.Abs(currentSpeed) > 0.001f)
            transform.position += transform.forward * (currentSpeed * Time.deltaTime);

        foreach (var p in new[] { haloPiece, coatPiece, wingsPiece, sparklesPiece }) StepPiece(p);
    }

    void HandleKeys()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (keyboardControl)
        {
            bool run = kb[runModifier].isPressed || (runModifier == Key.LeftShift && kb.rightShiftKey.isPressed);
            if (kb[walkKey].isPressed) SetMove(run ? MoveState.Run : MoveState.Walk);
            else if (kb[walkBackKey].isPressed) SetMove(MoveState.WalkBack);
            else SetMove(MoveState.Idle);
        }

        if (kb[stungKey].wasPressedThisFrame) Sting();
        if (kb[powerUpKey].wasPressedThisFrame) PowerUp();
        if (kb[guideKey].wasPressedThisFrame) showKeyGuide = !showKeyGuide;
    }

    // ---------- public API
    public void SetMove(MoveState state)
    {
        Move = state;
        animator.SetFloat(SpeedParam, state switch { MoveState.Run => 1f, MoveState.Walk => 0.5f, MoveState.WalkBack => -0.5f, _ => 0f });
    }

    /// <summary>Evil stings him: Full -> loses the halo, CoatAndWings -> loses coat, wings and sparkles.</summary>
    public void Sting()
    {
        animator.SetTrigger(StungTrigger);
        if (CurrentOutfit == Outfit.Full) SetOutfit(Outfit.CoatAndWings, 0f);
        else if (CurrentOutfit == Outfit.CoatAndWings) SetOutfit(Outfit.Naked, 0f);
    }

    /// <summary>Angel helps him: Naked -> gets coat, wings and sparkles, CoatAndWings -> gets the halo.</summary>
    public void PowerUp()
    {
        animator.SetTrigger(PowerUpTrigger);
        if (CurrentOutfit == Outfit.Naked) SetOutfit(Outfit.CoatAndWings, powerUpAppearDelay);
        else if (CurrentOutfit == Outfit.CoatAndWings) SetOutfit(Outfit.Full, powerUpAppearDelay);
    }

    public void SetOutfit(Outfit outfit) => SetOutfit(outfit, 0f);

    void SetOutfit(Outfit outfit, float appearDelay)
    {
        if (outfit == CurrentOutfit) return;
        CurrentOutfit = outfit;
        Target(haloPiece, outfit == Outfit.Full, appearDelay);
        bool dressed = outfit != Outfit.Naked;
        Target(coatPiece, dressed, appearDelay);
        Target(wingsPiece, dressed, appearDelay);
        Target(sparklesPiece, dressed, appearDelay);
        OutfitChanged?.Invoke(outfit);
    }

    void ApplyOutfit(Outfit outfit, bool instant)
    {
        CurrentOutfit = outfit;
        foreach (var (p, on) in new[] { (haloPiece, outfit == Outfit.Full), (coatPiece, outfit != Outfit.Naked),
                                        (wingsPiece, outfit != Outfit.Naked), (sparklesPiece, outfit != Outfit.Naked) })
        {
            p.goal = p.progress = on ? 1f : 0f;
            if (p.renderer) p.renderer.SetActive(on);
        }
    }

    void Target(Piece p, bool on, float appearDelay)
    {
        p.goal = on ? 1f : 0f;
        p.delay = on ? appearDelay : 0f;
    }

    void StepPiece(Piece p)
    {
        if (p.delay > 0f) { p.delay -= Time.deltaTime; return; }
        if (p.goal > 0.5f && p.renderer && !p.renderer.activeSelf) p.renderer.SetActive(true);
        p.progress = Mathf.MoveTowards(p.progress, p.goal, Time.deltaTime / Mathf.Max(popTime, 0.01f));
        if (p.progress <= 0f && p.goal <= 0f && p.renderer && p.renderer.activeSelf) p.renderer.SetActive(false);
    }

    void LateUpdate()
    {
        // after the Animator has posed the bones: collapse / grow the pieces that are popping
        foreach (var p in new[] { haloPiece, coatPiece, wingsPiece, sparklesPiece })
        {
            if (p.progress >= 1f || p.renderer == null || !p.renderer.activeSelf) continue;
            float s = p.goal > 0.5f ? EaseOutBack(p.progress) : p.progress * p.progress * (3f - 2f * p.progress);
            foreach (var b in p.bones) if (b) b.localScale = Vector3.one * Mathf.Max(s, 0.0001f);
        }
    }

    static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    // ---------- on-screen key guide
    void OnGUI()
    {
        if (!showKeyGuide) return;
        if (boxStyle == null)
        {
            var bg = new Texture2D(1, 1); bg.SetPixel(0, 0, new Color(0.07f, 0.08f, 0.12f, 0.8f)); bg.Apply();
            boxStyle = new GUIStyle { normal = { background = bg } };
            labelStyle = new GUIStyle(GUI.skin.label) { richText = true, fontSize = 15 };
            var line = new Texture2D(1, 1); line.SetPixel(0, 0, new Color(1f, 1f, 1f, 0.18f)); line.Apply();
            lineStyle = new GUIStyle { normal = { background = line } };
        }
        const float rowH = 22f, keyW = 78f, nameW = 150f;
        string KeyName(Key k) => k.ToString().Replace("Left", "").Replace("Digit", "");
        var rows = new (string key, string name)[]
        {
            (KeyName(walkKey), "Walk"),
            (KeyName(walkBackKey), "Walk back"),
            ($"{KeyName(runModifier)} + {KeyName(walkKey)}", "Run"),
            (KeyName(stungKey), "Stung (lose items)"),
            (KeyName(powerUpKey), "Power-up (get items)"),
            (KeyName(guideKey), "Hide guide"),
        };
        string outfit = CurrentOutfit switch { Outfit.Full => "<color=#FFD866>Halo + coat + wings</color>", Outfit.CoatAndWings => "<color=#9AD0FF>Coat + wings</color>", _ => "<color=#FF8A8A>Naked</color>" };
        string state = Move switch { MoveState.Walk => "Walk", MoveState.WalkBack => "Walk back", MoveState.Run => "Run", _ => "Idle (flying)" };
        float w = 28 + keyW + nameW, h = 10 + 26 + rows.Length * rowH + 10 + 2 * rowH + 10;
        GUI.Box(new Rect(16, 16, w, h), GUIContent.none, boxStyle);
        float x = 30, y = 26;
        GUI.Label(new Rect(x, y, w, 24), "<b><size=17>Main Character</size></b>", labelStyle); y += 26;
        foreach (var r in rows)
        {
            GUI.Label(new Rect(x, y, keyW, rowH), $"<b><color=#FFD866>{r.key}</color></b>", labelStyle);
            GUI.Label(new Rect(x + keyW, y, nameW, rowH), r.name, labelStyle);
            y += rowH;
        }
        GUI.Box(new Rect(x, y + 4, keyW + nameW, 1), GUIContent.none, lineStyle); y += 10;
        GUI.Label(new Rect(x, y, keyW + nameW, rowH), $"State:  <b>{state}</b>", labelStyle); y += rowH;
        GUI.Label(new Rect(x, y, keyW + nameW, rowH), $"Outfit: <b>{outfit}</b>", labelStyle);
    }
}
