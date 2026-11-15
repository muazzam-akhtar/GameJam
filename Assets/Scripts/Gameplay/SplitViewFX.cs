using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Gives each half of the split screen its own look, in the same scene:
///  - the Evil's view is night: dark and cold (a grading volume only his camera reads, the sun dimmed to moonlight
///    while his camera renders, a dark fog), lit by his lantern - a red light that only exists for his camera - and
///    the Angel shines in it: a white light and halo follow her;
///  - the Angel's view stays day, and the Evil drags a dark shadow with him: a dark pool on the ground and wisps of
///    dark smoke that only her camera sees.
/// Visuals are split with two layers (AngelView / EvilView) left out of the other camera's culling mask; lights,
/// sun and fog are switched per camera in RenderPipelineManager.beginCameraRendering.
/// </summary>
[DisallowMultipleComponent]
public class SplitViewFX : MonoBehaviour
{
    public const string AngelViewLayer = "AngelView";
    public const string EvilViewLayer = "EvilView";

    [Header("The Evil's night")]
    public float nightExposure = -0.65f;
    public Color nightFilter = new Color(0.76f, 0.79f, 1f);
    public float nightSaturation = -18f;
    public float nightVignette = 0.32f;
    [Range(0f, 1f)] public float moonStrength = 0.6f;
    public Color moonColor = new Color(0.62f, 0.68f, 1f);
    public Color nightFog = new Color(0.07f, 0.065f, 0.13f);
    public float nightFogStart = 200f;
    public float nightFogEnd = 1000f;

    [Header("His lantern (his view only)")]
    public Color lanternColor = new Color(1f, 0.2f, 0.14f);
    [Tooltip("URP point lights fall off with distance squared: at this world scale (characters ~32 tall) they need big numbers.")]
    public float lanternIntensity = 2400f;
    public float lanternRange = 170f;

    [Header("The Angel's light (his view only)")]
    public Color holyColor = new Color(1f, 0.96f, 0.88f);
    public float holyIntensity = 1000f;
    public float holyRange = 120f;
    public float haloSize = 74f;

    [Header("The Evil's shadow (her view only)")]
    public Color shadowColor = new Color(0.015f, 0f, 0.03f, 0.92f);
    public float shadowSize = 130f;
    public Color auraColor = new Color(0.02f, 0f, 0.04f, 0.62f);
    public float auraSize = 105f;
    public Color smokeColor = new Color(0.03f, 0f, 0.05f, 0.85f);

    Camera angelCam, evilCam;
    Transform angel, evil;
    Light sun, lantern, holy;
    float sunIntensity, flicker;
    Color sunColor;
    bool fogOn;
    Color fogColor;
    FogMode fogMode;
    float fogStart, fogEnd;
    Transform shadowPool, aura, halo, lanternBone;
    Renderer[] evilBody = new Renderer[0];
    ShadowCastingMode[] evilBodyShadows = new ShadowCastingMode[0];
    static Mesh quad;

    public static SplitViewFX Install(GameObject host, AngelController angel, EvilController evil, Camera angelCam, Camera evilCam)
    {
        if (!host || !angel || !evil || !angelCam || !evilCam) return null;
        if (!host.TryGetComponent<SplitViewFX>(out var fx)) fx = host.AddComponent<SplitViewFX>();
        fx.Setup(angel.transform, evil.transform, angelCam, evilCam);
        return fx;
    }

    void Setup(Transform angelT, Transform evilT, Camera aCam, Camera eCam)
    {
        angel = angelT; evil = evilT; angelCam = aCam; evilCam = eCam;
        int angelOnly = LayerMask.NameToLayer(AngelViewLayer), evilOnly = LayerMask.NameToLayer(EvilViewLayer);
        bool layers = angelOnly >= 0 && evilOnly >= 0;
        if (!layers) Debug.LogWarning("SplitViewFX: add the AngelView / EvilView layers (GameJam > Game > Setup Split-Screen Game) for per-player visuals.");

        foreach (Transform child in transform)
            if (child.name.StartsWith("ViewFX")) Destroy(child.gameObject);

        // --- night grading, read only by the Evil's camera
        if (layers)
        {
            angelCam.cullingMask &= ~(1 << evilOnly);
            evilCam.cullingMask &= ~(1 << angelOnly);
            var night = new GameObject("ViewFX Night Volume") { layer = evilOnly };
            night.transform.SetParent(transform, false);
            var volume = night.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.sharedProfile = NightProfile();
            var eData = evilCam.GetUniversalAdditionalCameraData();
            var aData = angelCam.GetUniversalAdditionalCameraData();
            eData.volumeLayerMask |= 1 << evilOnly;
            aData.volumeLayerMask &= ~(1 << evilOnly);
        }

        // --- lights that only exist while the Evil's camera renders
        lanternBone = FindDeep(evil, "lantern");
        // in his own view he doesn't shade his lantern's glow (the moon is too dim to need his shadow)
        evilBody = evil.GetComponentsInChildren<Renderer>(true);
        evilBodyShadows = new ShadowCastingMode[evilBody.Length];
        for (int i = 0; i < evilBody.Length; i++) evilBodyShadows[i] = evilBody[i].shadowCastingMode;
        lantern = MakeLight("ViewFX Lantern Light", evil, Vector3.up * 30f, lanternColor, lanternIntensity, lanternRange, LightShadows.Soft);
        holy = MakeLight("ViewFX Holy Light", angel, Vector3.up * 20f, holyColor, holyIntensity, holyRange, LightShadows.None);
        sun = RenderSettings.sun;
        if (!sun)
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) { sun = l; break; }
        if (sun) { sunIntensity = sun.intensity; sunColor = sun.color; }

        // --- per-view visuals
        var shade = Resources.Load<Material>("ViewFX/SoftShade");
        var glow = Resources.Load<Material>("ViewFX/SoftGlow");
        if (layers && shade)
        {
            shadowPool = Quad("ViewFX Evil Shadow", angelOnly, shade, shadowColor, shadowSize).transform;
            aura = Quad("ViewFX Evil Aura", angelOnly, shade, auraColor, auraSize).transform;
            Smoke(evil, angelOnly, shade);
        }
        if (layers && glow)
        {
            halo = Quad("ViewFX Angel Halo", evilOnly, glow, new Color(1f, 0.97f, 0.9f, 0.55f), haloSize).transform;
            Sparkles(angel, evilOnly, glow);
        }
        LateUpdate();
    }

    void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += BeginCamera;
        RenderPipelineManager.endCameraRendering += EndCamera;
    }

    void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= BeginCamera;
        RenderPipelineManager.endCameraRendering -= EndCamera;
        Restore();
    }

    void LateUpdate()
    {
        flicker = 1f + (Mathf.PerlinNoise(Time.time * 7f, 0.3f) - 0.5f) * 0.22f;
        // the light swings with his lantern but sits a little above it, so his own body doesn't shade the glow
        if (lantern && evil)
            lantern.transform.position = (lanternBone ? lanternBone.position : evil.position) + Vector3.up * (lanternBone ? 9f : 30f);
        if (shadowPool && evil)
        {
            var p = evil.position; p.y = 1.3f;                       // just above the paving slabs
            shadowPool.SetPositionAndRotation(p, Quaternion.Euler(90f, 0f, 0f));
        }
        if (aura && evil && angelCam)
        {
            // a dark cloud behind him, facing her camera (the mirror of her halo in his view)
            var toCam = angelCam.transform.position - evil.position; toCam.y = 0f;
            var p = evil.position + Vector3.up * 16f - toCam.normalized * 10f;
            aura.SetPositionAndRotation(p, Quaternion.LookRotation(p - angelCam.transform.position, Vector3.up));
            aura.localScale = Vector3.one * auraSize * (1f + Mathf.Sin(Time.time * 1.7f) * 0.06f);
        }
        if (halo && angel && evilCam)
        {
            var p = angel.position + Vector3.up * 16f;
            halo.SetPositionAndRotation(p, Quaternion.LookRotation(p - evilCam.transform.position, Vector3.up));
            halo.localScale = Vector3.one * haloSize * (1f + Mathf.Sin(Time.time * 2.1f) * 0.05f);
        }
    }

    void BeginCamera(ScriptableRenderContext context, Camera cam)
    {
        bool night = cam == evilCam;
        if (lantern) { lantern.enabled = night; lantern.intensity = lanternIntensity * flicker; }
        if (holy) holy.enabled = night;
        for (int i = 0; i < evilBody.Length; i++)
            if (evilBody[i]) evilBody[i].shadowCastingMode = night ? ShadowCastingMode.Off : evilBodyShadows[i];
        if (sun)
        {
            sun.intensity = night ? sunIntensity * moonStrength : sunIntensity;
            sun.color = night ? moonColor : sunColor;
        }
        if (night)
        {
            fogOn = RenderSettings.fog; fogColor = RenderSettings.fogColor; fogMode = RenderSettings.fogMode;
            fogStart = RenderSettings.fogStartDistance; fogEnd = RenderSettings.fogEndDistance;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = nightFog;
            RenderSettings.fogStartDistance = nightFogStart;
            RenderSettings.fogEndDistance = nightFogEnd;
        }
    }

    void EndCamera(ScriptableRenderContext context, Camera cam)
    {
        if (cam == evilCam) Restore();
    }

    void Restore()
    {
        if (lantern) lantern.enabled = false;
        if (holy) holy.enabled = false;
        for (int i = 0; i < evilBody.Length; i++)
            if (evilBody[i]) evilBody[i].shadowCastingMode = evilBodyShadows[i];
        if (sun) { sun.intensity = sunIntensity; sun.color = sunColor; }
        if (fogEnd > 0f)
        {
            RenderSettings.fog = fogOn; RenderSettings.fogColor = fogColor; RenderSettings.fogMode = fogMode;
            RenderSettings.fogStartDistance = fogStart; RenderSettings.fogEndDistance = fogEnd;
        }
    }

    // ---------------------------------------------------------------- building

    VolumeProfile NightProfile()
    {
        var p = ScriptableObject.CreateInstance<VolumeProfile>();
        p.name = "Evil Night (runtime)";
        var ca = p.Add<ColorAdjustments>(true);
        ca.postExposure.Override(nightExposure);
        ca.colorFilter.Override(nightFilter);
        ca.saturation.Override(nightSaturation);
        ca.contrast.Override(8f);
        var vg = p.Add<Vignette>(true);
        vg.intensity.Override(nightVignette);
        vg.smoothness.Override(0.5f);
        vg.color.Override(new Color(0.02f, 0f, 0.05f));
        var bloom = p.Add<Bloom>(true);
        bloom.intensity.Override(1.6f);                       // lantern, offerings and her light glow in the dark
        return p;
    }

    static Light MakeLight(string name, Transform parent, Vector3 offset, Color color, float intensity, float range, LightShadows shadows)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = parent.position + offset;      // world offset (the characters are scaled x10)
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = color;
        l.intensity = intensity;
        l.range = range;                                      // world units (light range ignores the scaled rig)
        l.shadows = shadows;
        l.shadowStrength = 0.85f;
        l.shadowNearPlane = 2f;
        l.renderMode = LightRenderMode.ForcePixel;
        l.enabled = false;
        return l;
    }

    GameObject Quad(string name, int layer, Material template, Color color, float size)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)) { layer = layer };
        go.transform.SetParent(transform, false);
        go.transform.localScale = Vector3.one * size;
        go.GetComponent<MeshFilter>().sharedMesh = QuadMesh(color);
        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = template;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;
        return go;
    }

    static Mesh QuadMesh(Color color)
    {
        var m = new Mesh { name = "ViewFX Quad" };
        m.SetVertices(new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f) });
        m.SetUVs(0, new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) });
        m.SetColors(new[] { color, color, color, color });
        m.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
        m.RecalculateNormals();
        m.bounds = new Bounds(Vector3.zero, Vector3.one * 2f);
        return m;
    }

    void Smoke(Transform owner, int layer, Material mat)
    {
        var ps = NewParticles("ViewFX Evil Smoke", owner, layer, mat, Vector3.up * 12f);
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 6f);
        main.startSize = new ParticleSystem.MinMaxCurve(26f, 44f);
        main.startColor = smokeColor;
        main.maxParticles = 140;
        main.gravityModifier = -0.15f;                         // drifts up
        var emission = ps.emission;
        emission.rateOverTime = 32f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 12f;
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.55f), new Keyframe(1f, 1.35f)));
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);
        ps.Play();
    }

    void Sparkles(Transform owner, int layer, Material mat)
    {
        var ps = NewParticles("ViewFX Angel Sparkles", owner, layer, mat, Vector3.up * 16f);
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.0f, 1.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 4f);
        main.startSize = new ParticleSystem.MinMaxCurve(2.5f, 5f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 1f, 0.9f), new Color(1f, 0.9f, 0.6f, 0.9f));
        main.maxParticles = 60;
        main.gravityModifier = -0.05f;
        var emission = ps.emission;
        emission.rateOverTime = 22f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 16f;
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        ps.Play();
    }

    static ParticleSystem NewParticles(string name, Transform owner, int layer, Material mat, Vector3 offset)
    {
        var old = owner.Find(name);
        if (old) Destroy(old.gameObject);
        var go = new GameObject(name) { layer = layer };
        go.transform.SetParent(owner, false);
        go.transform.position = owner.position + offset;
        go.transform.rotation = Quaternion.identity;
        float parentScale = Mathf.Max(0.001f, owner.lossyScale.x);
        go.transform.localScale = Vector3.one / parentScale;   // the characters are scaled x10: work in world units
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Shape;
        main.playOnAwake = true;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        return ps;
    }

    static Transform FindDeep(Transform t, string name)
    {
        if (t.name == name) return t;
        foreach (Transform c in t)
        {
            var f = FindDeep(c, name);
            if (f) return f;
        }
        return null;
    }
}
