using System;
using UnityEngine;

/// <summary>
/// A golden offering the Angel puts down. It pops in, hovers and spins over a glowing ring that fills up as it
/// ripens; after <see cref="bloomTime"/> seconds it is blessed (the Angel scores). If the Evil touches it first it is
/// eaten (the Evil scores and flares up). Either way it bursts (Art's Collect Burst Good / BAD) and disappears.
/// </summary>
[DisallowMultipleComponent]
public class Offering : MonoBehaviour
{
    [Tooltip("Seconds the offering must survive to be blessed.")]
    public float bloomTime = 15f;

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
    public float Progress => bloomTime <= 0f ? 1f : Mathf.Clamp01(age / bloomTime);

    public event Action<Offering> Blessed;
    public event Action<Offering> Eaten;

    void Awake() => Cache();

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
        if (age >= bloomTime) Finish(true, null);
    }

    void Animate()
    {
        float pop = popTime > 0f ? EaseOutBack(Mathf.Clamp01(age / popTime)) : 1f;
        float p = Progress;
        float pulse = 1f + Mathf.Sin(age * Mathf.Lerp(3f, 13f, p * p)) * Mathf.Lerp(0.03f, 0.12f, p);
        if (visual)
        {
            visual.localPosition = Vector3.up * (hover + Mathf.Sin(age * 2.2f) * bob);
            visual.localRotation = Quaternion.Euler(0f, age * 70f, 0f);
            visual.localScale = visualScale * (pop * pulse);
        }
        if (ring) ring.localScale = ringScale * (Mathf.Lerp(0.55f, 1f, p) * pop);
        if (ringRenderer)
        {
            block ??= new MaterialPropertyBlock();
            ringRenderer.GetPropertyBlock(block);
            block.SetColor(EmissionId, ringEmission * Mathf.Lerp(0.5f, 2.4f, p) * pulse);
            ringRenderer.SetPropertyBlock(block);
        }
        if (glowLight) glowLight.intensity = lightIntensity * Mathf.Lerp(0.6f, 1.7f, p) * pop;
    }

    void OnTriggerEnter(Collider other)
    {
        if (Done) return;
        var evil = other.GetComponentInParent<EvilController>();
        if (evil) Finish(false, evil);
    }

    void Finish(bool blessed, EvilController eater)
    {
        Done = true;
        Burst(blessed ? goodBurst : badBurst);
        if (blessed) Blessed?.Invoke(this);
        else
        {
            Eaten?.Invoke(this);
            if (eater) eater.PowerUp();
        }
        if (Application.isPlaying) Destroy(gameObject);
        else DestroyImmediate(gameObject);
    }

    void Burst(GameObject prefab)
    {
        if (!prefab || !Application.isPlaying) return;
        var b = Instantiate(prefab, transform.position + Vector3.up * hover, Quaternion.identity);
        b.transform.localScale = Vector3.one * burstScale;
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
