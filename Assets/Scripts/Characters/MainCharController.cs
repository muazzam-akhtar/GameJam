using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Main character controls + animation showcase.
///
/// Animations
///   L  Walk          (toggle)
///   K  Run           (toggle)
///   I  Leg float     (ghost tail + string, toggle)
///   O  Coat float    (toggle)
///   P  Halo spin     (toggle)
/// Pieces
///   1  Halo   2  Coat   3  Wings   4  Sparkles   (show / hide)
///   0  Reset everything
///   G  Show / hide this key guide
///
/// Other scripts can call SetMove, SetLegFloat, SetCoatFloat, SetHaloSpin, SetHalo, SetCoat, SetWings, SetSparkles.
/// </summary>
[RequireComponent(typeof(Animator))]
public class MainCharController : MonoBehaviour
{
    public enum MoveState { Idle, Walk, Run }

    [Header("Removable pieces")]
    [SerializeField] GameObject halo;
    [SerializeField] GameObject coat;
    [SerializeField] GameObject wings;
    [SerializeField] GameObject sparkles;

    [Header("Animation keys")]
    [SerializeField] Key walkKey = Key.L;
    [SerializeField] Key runKey = Key.K;
    [SerializeField] Key legFloatKey = Key.I;
    [SerializeField] Key coatFloatKey = Key.O;
    [SerializeField] Key haloSpinKey = Key.P;

    [Header("Piece keys")]
    [SerializeField] Key haloKey = Key.Digit1;
    [SerializeField] Key coatKey = Key.Digit2;
    [SerializeField] Key wingsKey = Key.Digit3;
    [SerializeField] Key sparklesKey = Key.Digit4;
    [SerializeField] Key resetKey = Key.Digit0;

    [Header("Key guide")]
    [SerializeField] bool showKeyGuide = true;
    [SerializeField] Key guideKey = Key.G;

    [Header("Blending")]
    [Tooltip("Seconds to fade a float animation in or out.")]
    [SerializeField] float layerFade = 0.35f;

    static readonly int SpeedParam = Animator.StringToHash("Speed");

    Animator animator;
    int legLayer = -1, coatLayer = -1, haloLayer = -1;
    float legTarget = 1f, coatTarget = 1f, haloTarget = 1f;
    GUIStyle boxStyle, labelStyle, lineStyle;

    public MoveState Move { get; private set; }
    public bool LegFloat => legTarget > 0.5f;
    public bool CoatFloat => coatTarget > 0.5f;
    public bool HaloSpin => haloTarget > 0.5f;
    public bool HasHalo => halo && halo.activeSelf;
    public bool HasCoat => coat && coat.activeSelf;
    public bool HasWings => wings && wings.activeSelf;
    public bool HasSparkles => sparkles && sparkles.activeSelf;

    void Awake()
    {
        animator = GetComponent<Animator>();
        legLayer = animator.GetLayerIndex("Leg");
        coatLayer = animator.GetLayerIndex("Coat");
        haloLayer = animator.GetLayerIndex("Halo");
    }

    void Update()
    {
        HandleKeys();
        FadeLayer(legLayer, legTarget);
        FadeLayer(coatLayer, coatTarget);
        FadeLayer(haloLayer, haloTarget);
    }

    void HandleKeys()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb[walkKey].wasPressedThisFrame) SetMove(Move == MoveState.Walk ? MoveState.Idle : MoveState.Walk);
        if (kb[runKey].wasPressedThisFrame) SetMove(Move == MoveState.Run ? MoveState.Idle : MoveState.Run);
        if (kb[legFloatKey].wasPressedThisFrame) SetLegFloat(!LegFloat);
        if (kb[coatFloatKey].wasPressedThisFrame) SetCoatFloat(!CoatFloat);
        if (kb[haloSpinKey].wasPressedThisFrame) SetHaloSpin(!HaloSpin);

        if (kb[haloKey].wasPressedThisFrame) SetHalo(!HasHalo);
        if (kb[coatKey].wasPressedThisFrame) SetCoat(!HasCoat);
        if (kb[wingsKey].wasPressedThisFrame) SetWings(!HasWings);
        if (kb[sparklesKey].wasPressedThisFrame) SetSparkles(!HasSparkles);
        if (kb[resetKey].wasPressedThisFrame) ResetAll();
        if (kb[guideKey].wasPressedThisFrame) showKeyGuide = !showKeyGuide;
    }

    void FadeLayer(int layer, float target)
    {
        if (layer < 0) return;
        float w = Mathf.MoveTowards(animator.GetLayerWeight(layer), target, Time.deltaTime / Mathf.Max(layerFade, 0.01f));
        animator.SetLayerWeight(layer, w);
    }

    // ---------- public API
    public void SetMove(MoveState state)
    {
        Move = state;
        animator.SetFloat(SpeedParam, state == MoveState.Run ? 1f : state == MoveState.Walk ? 0.5f : 0f);
    }

    public void SetLegFloat(bool on) => legTarget = on ? 1f : 0f;
    public void SetCoatFloat(bool on) => coatTarget = on ? 1f : 0f;
    public void SetHaloSpin(bool on) => haloTarget = on ? 1f : 0f;

    public void SetHalo(bool on) { if (halo) halo.SetActive(on); }
    public void SetCoat(bool on) { if (coat) coat.SetActive(on); }
    public void SetWings(bool on) { if (wings) wings.SetActive(on); }
    public void SetSparkles(bool on) { if (sparkles) sparkles.SetActive(on); }

    public void ResetAll()
    {
        SetMove(MoveState.Idle);
        SetLegFloat(true); SetCoatFloat(true); SetHaloSpin(true);
        SetHalo(true); SetCoat(true); SetWings(true); SetSparkles(true);
    }

    // ---------- on-screen key guide
    void OnGUI()
    {
        if (!showKeyGuide) return;
        if (boxStyle == null)
        {
            var bg = new Texture2D(1, 1); bg.SetPixel(0, 0, new Color(0.07f, 0.08f, 0.12f, 0.8f)); bg.Apply();
            boxStyle = new GUIStyle { normal = { background = bg }, padding = new RectOffset(14, 14, 10, 12) };
            labelStyle = new GUIStyle(GUI.skin.label) { richText = true, fontSize = 15 };
            var line = new Texture2D(1, 1); line.SetPixel(0, 0, new Color(1f, 1f, 1f, 0.18f)); line.Apply();
            lineStyle = new GUIStyle { normal = { background = line } };
        }
        const float rowH = 22f, keyW = 34f, nameW = 110f, stateW = 40f;
        var rows = new (Key key, string name, bool? on)[]
        {
            (walkKey, "Walk", Move == MoveState.Walk),
            (runKey, "Run", Move == MoveState.Run),
            (legFloatKey, "Leg float", LegFloat),
            (coatFloatKey, "Coat float", CoatFloat),
            (haloSpinKey, "Halo spin", HaloSpin),
            (haloKey, "Halo", HasHalo),
            (coatKey, "Coat", HasCoat),
            (wingsKey, "Wings", HasWings),
            (sparklesKey, "Sparkles", HasSparkles),
            (resetKey, "Reset all", null),
            (guideKey, "Hide guide", null),
        };
        float w = 14 + keyW + nameW + stateW + 14, h = 10 + 26 + rows.Length * rowH + 12;
        GUI.Box(new Rect(16, 16, w, h), GUIContent.none, boxStyle);
        float x = 30, y = 26;
        GUI.Label(new Rect(x, y, w, 24), "<b><size=17>Main Character</size></b>", labelStyle);
        y += 26;
        for (int i = 0; i < rows.Length; i++, y += rowH)
        {
            if (i == 5 || i == 9) GUI.Box(new Rect(x, y - 2, keyW + nameW + stateW, 1), GUIContent.none, lineStyle);
            GUI.Label(new Rect(x, y, keyW, rowH), $"<b><color=#FFD866>{KeyName(rows[i].key)}</color></b>", labelStyle);
            GUI.Label(new Rect(x + keyW, y, nameW, rowH), rows[i].name, labelStyle);
            if (rows[i].on.HasValue)
                GUI.Label(new Rect(x + keyW + nameW, y, stateW, rowH),
                    rows[i].on.Value ? "<color=#7CFC9A>ON</color>" : "<color=#FF8A8A>off</color>", labelStyle);
        }
    }

    static string KeyName(Key k)
    {
        string s = k.ToString();
        return s.StartsWith("Digit") ? s.Substring(5) : s;
    }
}
