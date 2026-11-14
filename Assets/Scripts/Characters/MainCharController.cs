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
///   Stung     Q - hit reaction (flinch + shake)
///   Power-up  E - happy jump with a full twirl
///
/// Game code calls <see cref="Sting"/> and <see cref="PowerUp"/>; Q / E are test keys for those two.
/// G shows / hides the on-screen key guide.
/// </summary>
[RequireComponent(typeof(Animator))]
public class MainCharController : MonoBehaviour
{
    public enum MoveState { Idle, Walk, WalkBack, Run }

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

    [Header("Key guide")]
    [SerializeField] bool showKeyGuide = true;
    [SerializeField] Key guideKey = Key.G;

    static readonly int SpeedParam = Animator.StringToHash("Speed");
    static readonly int StungTrigger = Animator.StringToHash("Stung");
    static readonly int PowerUpTrigger = Animator.StringToHash("PowerUp");

    public MoveState Move { get; private set; }

    Animator animator;
    float currentSpeed;
    GUIStyle boxStyle, labelStyle, lineStyle;

    void Awake() => animator = GetComponent<Animator>();

    void Update()
    {
        HandleKeys();

        float target = Move switch { MoveState.Walk => walkSpeed, MoveState.Run => runSpeed, MoveState.WalkBack => -walkBackSpeed, _ => 0f };
        currentSpeed = Mathf.MoveTowards(currentSpeed, target, acceleration * Time.deltaTime);
        if (moveInWorld && Mathf.Abs(currentSpeed) > 0.001f)
            transform.position += transform.forward * (currentSpeed * Time.deltaTime);
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

    /// <summary>Plays the stung hit reaction.</summary>
    public void Sting() => animator.SetTrigger(StungTrigger);

    /// <summary>Plays the power-up jump and twirl.</summary>
    public void PowerUp() => animator.SetTrigger(PowerUpTrigger);

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
        const float rowH = 22f, keyW = 78f, nameW = 130f;
        string KeyName(Key k) => k.ToString().Replace("Left", "").Replace("Digit", "");
        var rows = new (string key, string name)[]
        {
            (KeyName(walkKey), "Walk"),
            (KeyName(walkBackKey), "Walk back"),
            ($"{KeyName(runModifier)} + {KeyName(walkKey)}", "Run"),
            (KeyName(stungKey), "Stung"),
            (KeyName(powerUpKey), "Power-up"),
            (KeyName(guideKey), "Hide guide"),
        };
        string state = Move switch { MoveState.Walk => "Walk", MoveState.WalkBack => "Walk back", MoveState.Run => "Run", _ => "Idle (flying)" };
        float w = 28 + keyW + nameW, h = 10 + 26 + rows.Length * rowH + 10 + rowH + 10;
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
        GUI.Label(new Rect(x, y, keyW + nameW, rowH), $"State:  <b>{state}</b>", labelStyle);
    }
}
