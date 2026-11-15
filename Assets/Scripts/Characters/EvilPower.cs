using UnityEngine;

/// <summary>
/// The Evil's power-up look. <see cref="Activate"/> ramps the power in (0.25 s), holds it (4 s) and fades it out
/// (1.2 s). While powered the flames grow and burn brighter, the body gets a fiery glow (GlowSource material,
/// drawn by SelectiveGlowFeature), extra flames and embers rise off it and a warm light comes on. The body
/// swells briefly when the power hits. Applied in LateUpdate, on top of whatever the Animator played.
/// </summary>
[DisallowMultipleComponent]
public class EvilPower : MonoBehaviour
{
    [Header("Parts")]
    [SerializeField] Renderer target;
    [SerializeField] int flameMaterialIndex = 0;
    [Tooltip("Extra GameJam/GlowSource material slot on the renderer (draws the body).")]
    [SerializeField] int glowMaterialIndex = 2;
    [SerializeField] Transform body;
    [SerializeField] Transform[] flameBones = new Transform[0];
    [SerializeField] Light powerLight;
    [SerializeField] ParticleSystem powerFlames;
    [SerializeField] ParticleSystem embers;

    [Header("Timing (seconds)")]
    [SerializeField] float rampIn = 0.25f;
    [SerializeField] float hold = 4f;
    [SerializeField] float fadeOut = 1.2f;

    [Header("At full power")]
    [SerializeField] float flameScale = 1.5f;
    [SerializeField] float flameBrightness = 1.5f;
    [SerializeField] float flameRiseSpeed = 1.6f;
    [ColorUsage(false, true)] [SerializeField] Color glowColor = new Color(2.4f, 0.9f, 0.25f);
    [SerializeField] float glowIntensity = 0.45f;
    [SerializeField] float lightIntensity = 1.6f;
    [Tooltip("Light range in the character's own units (scaled with the character).")]
    [SerializeField] float lightRange = 3.5f;
    [SerializeField] float flameRate = 26f;
    [SerializeField] float emberRate = 24f;
    [SerializeField] int flameBurst = 16;
    [SerializeField] int emberBurst = 30;

    [Header("Swell when it hits")]
    [SerializeField] float swell = 0.08f;
    [SerializeField] float swellTime = 0.35f;

    static readonly int CoreId = Shader.PropertyToID("_CoreColor");
    static readonly int MidId = Shader.PropertyToID("_MidColor");
    static readonly int TipId = Shader.PropertyToID("_TipColor");
    static readonly int EmberId = Shader.PropertyToID("_EmberColor");
    static readonly int RiseId = Shader.PropertyToID("_RiseSpeed");
    static readonly int GlowColorId = Shader.PropertyToID("_GlowColor");
    static readonly int GlowIntensityId = Shader.PropertyToID("_GlowIntensity");

    float clock = -1f;          // seconds into the power-up; negative = off
    float swellClock = -1f;
    MaterialPropertyBlock flameBlock, glowBlock;
    Color baseCore, baseMid, baseTip, baseEmber; float baseRise; bool baseRead;
    Transform[] scaled; Vector3[] baseScale, written;

    public float Power { get; private set; }
    public bool IsPowered => clock >= 0f;
    float Total => rampIn + hold + fadeOut;

    /// <summary>Powers up, or keeps the power full and restarts the hold if it is already on.</summary>
    public void Activate()
    {
        if (!IsPowered) clock = 0f;
        else if (clock > rampIn) clock = Mathf.Clamp01(Power) * rampIn;   // already up / fading: no dip, restart the hold
        swellClock = 0f;
        if (powerFlames) powerFlames.Emit(flameBurst);
        if (embers) embers.Emit(emberBurst);
    }

    void LateUpdate() => Tick(Time.deltaTime);

    void OnDisable()
    {
        clock = -1f; swellClock = -1f; Power = 0f;
        Apply();
    }

    /// <summary>Advances the power-up by <paramref name="dt"/> seconds and applies the look.</summary>
    public void Tick(float dt)
    {
        if (clock >= 0f)
        {
            clock += dt;
            if (clock >= Total) clock = -1f;
        }
        if (swellClock >= 0f)
        {
            swellClock += dt;
            if (swellClock >= swellTime) swellClock = -1f;
        }
        Power = Envelope(clock);
        Apply();
    }

    float Envelope(float t)
    {
        if (t < 0f) return 0f;
        if (t < rampIn) return Smooth(t / rampIn);
        if (t < rampIn + hold) return 1f;
        return 1f - Smooth((t - rampIn - hold) / fadeOut);
    }

    static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

    void Apply()
    {
        // ---- bone scales (flames grow, body swells), relative to what the Animator wrote this frame
        CacheBones();
        float flame = 1f + (flameScale - 1f) * Power;
        float bodySwell = swellClock >= 0f ? 1f + swell * Mathf.Sin(Mathf.PI * swellClock / swellTime) : 1f;
        for (int i = 0; i < scaled.Length; i++)
        {
            var b = scaled[i];
            if (!b) continue;
            if (b.localScale != written[i]) baseScale[i] = b.localScale;   // the Animator (or someone) set a fresh value
            float f = b == body ? bodySwell : flame;
            b.localScale = baseScale[i] * f;
            written[i] = b.localScale;
        }

        // ---- flame material brightness + glow
        if (target)
        {
            ReadBaseFlame();
            float k = 1f + (flameBrightness - 1f) * Power;
            flameBlock ??= new MaterialPropertyBlock();
            target.GetPropertyBlock(flameBlock, flameMaterialIndex);
            flameBlock.SetColor(CoreId, baseCore * k);
            flameBlock.SetColor(MidId, baseMid * k);
            flameBlock.SetColor(TipId, baseTip * k);
            flameBlock.SetColor(EmberId, baseEmber * k);
            flameBlock.SetFloat(RiseId, baseRise * (1f + (flameRiseSpeed - 1f) * Power));
            target.SetPropertyBlock(flameBlock, flameMaterialIndex);

            if (glowMaterialIndex < target.sharedMaterials.Length)
            {
                glowBlock ??= new MaterialPropertyBlock();
                target.GetPropertyBlock(glowBlock, glowMaterialIndex);
                glowBlock.SetColor(GlowColorId, glowColor);
                glowBlock.SetFloat(GlowIntensityId, glowIntensity * Power);
                target.SetPropertyBlock(glowBlock, glowMaterialIndex);
            }
        }

        // ---- light and particles
        if (powerLight)
        {
            // light falls off with distance squared: a character scaled by s needs s² the intensity to light it the same
            float s = transform.lossyScale.y;
            powerLight.intensity = lightIntensity * Power * s * s;
            powerLight.range = lightRange * s;
            powerLight.enabled = Power > 0.001f;
        }
        if (powerFlames) { var e = powerFlames.emission; e.rateOverTimeMultiplier = flameRate * Power; }
        if (embers) { var e = embers.emission; e.rateOverTimeMultiplier = emberRate * Power; }
    }

    void CacheBones()
    {
        if (scaled != null && scaled.Length == flameBones.Length + 1) return;
        scaled = new Transform[flameBones.Length + 1];
        flameBones.CopyTo(scaled, 0);
        scaled[flameBones.Length] = body;
        baseScale = new Vector3[scaled.Length];
        written = new Vector3[scaled.Length];
        for (int i = 0; i < scaled.Length; i++)
        {
            baseScale[i] = scaled[i] ? scaled[i].localScale : Vector3.one;
            written[i] = baseScale[i];
        }
    }

    void ReadBaseFlame()
    {
        if (baseRead) return;
        var m = flameMaterialIndex < target.sharedMaterials.Length ? target.sharedMaterials[flameMaterialIndex] : null;
        if (m == null) return;
        baseCore = m.GetColor(CoreId); baseMid = m.GetColor(MidId); baseTip = m.GetColor(TipId); baseEmber = m.GetColor(EmberId);
        baseRise = m.GetFloat(RiseId);
        baseRead = true;
    }
}
