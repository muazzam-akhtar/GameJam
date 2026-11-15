using System;
using UnityEngine;

/// <summary>
/// A golden offering the Angel hides. It pops in, then hovers, spins and breathes over a glowing ring for as long
/// as it takes the Evil to find it - it never expires. When the Evil touches it, it is devoured (the Evil scores and
/// flares up): it bursts (Art's Collect Burst BAD) and disappears. The Angel can't pick it back up.
/// </summary>
[DisallowMultipleComponent]
public class Offering : MonoBehaviour
{
    [Header("Look (all optional)")]
    public Transform visual;
    public Transform ring;
    public Renderer ringRenderer;
    public Light glowLight;
    public GameObject goodBurst;
    public GameObject badBurst;
    public float burstScale = 6f;
    [SerializeField] float hover = 9f;
    [SerializeField] float bob = 1.4f;
    [SerializeField] float popTime = 0.35f;

    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
    float age;
    Vector3 visualScale = Vector3.one, ringScale = Vector3.one;
    Color ringEmission;
    float lightIntensity;
    MaterialPropertyBlock block;
    bool cached;

    public bool Done { get; private set; }

    public event Action<Offering> Eaten;

    [Tooltip("Single player: the Keeper picks it up and the guard Reapers leave it alone.")]
    public bool keeperCollects;
    /// <summary>Single player: the Keeper found it.</summary>
    public event Action<Offering> Collected;

    void Awake() => Cache();

    void Start() => Burst(goodBurst, 0.6f);                  // a little golden puff where she hides it

    void Cache()
    {
        if (cached) return;
        cached = true;
        if (visual) visualScale = visual.localScale;
        if (ring) ringScale = ring.localScale;
        if (ringRenderer) ringEmission = ringRenderer.sharedMaterial.GetColor(EmissionId);
        if (glowLight) lightIntensity = glowLight.intensity;
    }

    void Update() => Tick(Time.deltaTime);

    public void Tick(float dt)
    {
        if (Done) return;
        Cache();
        age += dt;
        Animate();
    }

    void Animate()
    {
        float pop = popTime > 0f ? EaseOutBack(Mathf.Clamp01(age / popTime)) : 1f;
        // a slow calm breath (each offering a little out of step with the others)
        float phase = transform.position.x * 0.13f + transform.position.z * 0.07f;
        float breath = Mathf.Sin(age * 2.4f + phase);
        float pulse = 1f + breath * 0.06f;
        if (visual)
        {
            visual.localPosition = Vector3.up * (hover + Mathf.Sin(age * 2.2f) * bob);
            visual.localRotation = Quaternion.Euler(0f, age * 70f, 0f);
            visual.localScale = visualScale * (pop * pulse);
        }
        if (ring) ring.localScale = ringScale * pop;
        if (ringRenderer)
        {
            block ??= new MaterialPropertyBlock();
            ringRenderer.GetPropertyBlock(block);
            block.SetColor(EmissionId, ringEmission * (1.5f + breath * 0.5f));
            ringRenderer.SetPropertyBlock(block);
        }
        if (glowLight) glowLight.intensity = lightIntensity * (1.1f + breath * 0.2f) * pop;
    }

    void OnTriggerEnter(Collider other)
    {
        if (Done) return;
        if (keeperCollects)
        {
            var keeper = other.GetComponentInParent<AngelController>();
            if (keeper && keeper.enabled) Collect(keeper);
            return;
        }
        var evil = other.GetComponentInParent<EvilController>();
        if (evil && evil.enabled) Devour(evil);
    }

    /// <summary>Single player: the Keeper got it.</summary>
    public void Collect(AngelController keeper)
    {
        if (Done) return;
        Done = true;
        Burst(goodBurst);
        Collected?.Invoke(this);
        if (keeper) keeper.PowerUp();
        if (Application.isPlaying) Destroy(gameObject);
        else DestroyImmediate(gameObject);
    }

    /// <summary>The Evil got it.</summary>
    public void Devour(EvilController eater)
    {
        if (Done) return;
        Done = true;
        Burst(badBurst);
        Eaten?.Invoke(this);
        if (eater) eater.PowerUp();
        if (Application.isPlaying) Destroy(gameObject);
        else DestroyImmediate(gameObject);
    }

    void Burst(GameObject prefab, float scale = 1f)
    {
        if (!prefab || !Application.isPlaying) return;
        var b = Instantiate(prefab, transform.position + Vector3.up * hover, Quaternion.identity);
        b.transform.localScale = Vector3.one * (burstScale * scale);
        foreach (var ps in b.GetComponentsInChildren<ParticleSystem>())
        {
            var main = ps.main;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.loop = false;
        }
        Destroy(b, 3f);
    }

    static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        float u = x - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;
    }
}
