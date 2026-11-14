using UnityEngine;

/// <summary>
/// The Angel's holy light. Drives her GlowSource material (rendered as real HDR glow by
/// SelectiveGlowFeature), a soft inner self-illumination, a light that spills onto her surroundings and
/// floating motes - all breathing slowly, with a bright flare when she does her Stung move (<see cref="Flare"/>).
/// </summary>
[DisallowMultipleComponent]
public class AngelGlow : MonoBehaviour
{
    [Header("Parts")]
    [SerializeField] Renderer target;
    [SerializeField] int bodyMaterialIndex = 0;
    [SerializeField] int glowMaterialIndex = 1;
    [SerializeField] Light auraLight;
    [SerializeField] ParticleSystem motes;

    [Header("Glow")]
    [ColorUsage(false, true)] [SerializeField] Color glowColor = new Color(0.85f, 1.0f, 1.7f);
    [SerializeField] float glowIntensity = 1.0f;
    [ColorUsage(false, true)] [SerializeField] Color innerGlow = new Color(0.32f, 0.34f, 0.42f);
    [SerializeField] float lightIntensity = 1.6f;

    [Header("Breathing")]
    [SerializeField] float breathePeriod = 3.2f;
    [Range(0f, 1f)] [SerializeField] float breatheAmount = 0.2f;

    [Header("Flare (on Stung)")]
    [SerializeField] float flareBoost = 1.6f;
    [SerializeField] float flareTime = 0.7f;
    [SerializeField] int flareMotes = 22;

    static readonly int GlowColorId = Shader.PropertyToID("_GlowColor");
    static readonly int GlowIntensityId = Shader.PropertyToID("_GlowIntensity");
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    MaterialPropertyBlock glowBlock, bodyBlock;
    float flareT = 1f;

    void Awake()
    {
        glowBlock = new MaterialPropertyBlock();
        bodyBlock = new MaterialPropertyBlock();
    }

    /// <summary>A short, smooth burst of light (called when she stings).</summary>
    public void Flare()
    {
        flareT = 0f;
        if (motes) motes.Emit(flareMotes);
    }

    void Update()
    {
        float breathe = 1f + breatheAmount * Mathf.Sin(Time.time * 2f * Mathf.PI / Mathf.Max(0.1f, breathePeriod));
        float flare = 0f;
        if (flareT < 1f)
        {
            flareT = Mathf.Min(1f, flareT + Time.deltaTime / Mathf.Max(0.05f, flareTime));
            flare = Mathf.Sin(flareT * Mathf.PI);                 // smooth rise and fall
        }
        float k = breathe * (1f + flareBoost * flare);

        if (target)
        {
            target.GetPropertyBlock(glowBlock, glowMaterialIndex);
            glowBlock.SetColor(GlowColorId, glowColor);
            glowBlock.SetFloat(GlowIntensityId, glowIntensity * k);
            target.SetPropertyBlock(glowBlock, glowMaterialIndex);

            target.GetPropertyBlock(bodyBlock, bodyMaterialIndex);
            bodyBlock.SetColor(EmissionId, innerGlow * (0.85f + 0.15f * breathe + 0.7f * flare));
            target.SetPropertyBlock(bodyBlock, bodyMaterialIndex);
        }
        if (auraLight) auraLight.intensity = lightIntensity * k;
    }
}
