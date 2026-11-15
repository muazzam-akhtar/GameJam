using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Builds the character effects and puts them on the Angel / Evil prefabs:
///   - dizzy stars (golden for the Angel, purple for the Evil) shown after a slap;
///   - the Evil's power-up: fiery glow material slot, rising flame + ember particles, a warm light, EvilPower.
/// Safe to run again: assets are updated in place and existing children are reused.
/// </summary>
public static class CharacterFxBuilder
{
    const string AngelPrefab = "Assets/Prefabs/Characters/Angel/Angel.prefab";
    const string EvilPrefab = "Assets/Prefabs/Characters/Evil/Evil.prefab";
    const string FxDir = "Assets/Prefabs/Characters/FX";

    static readonly Color Gold = new Color(1.0f, 0.74f, 0.16f);
    static readonly Color GoldEmission = new Color(1.1f, 0.62f, 0.08f);
    static readonly Color GoldGlow = new Color(2.6f, 1.9f, 0.5f);
    static readonly Color Purple = new Color(0.62f, 0.2f, 1.0f);
    static readonly Color PurpleEmission = new Color(0.55f, 0.1f, 1.1f);
    static readonly Color PurpleGlow = new Color(1.5f, 0.45f, 2.6f);

    [MenuItem("GameJam/Characters/Build Character FX")]
    public static void BuildAll()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("CharacterFxBuilder: leave Play mode first.");
            return;
        }
        BuildStars();
        BuildEvilPower();
        AssetDatabase.SaveAssets();
    }

    // ------------------------------------------------------------------ dizzy stars

    public static void BuildStars()
    {
        EnsureFolder(FxDir);
        var mesh = SaveMesh(StarMesh(), FxDir + "/DizzyStar.asset");
        var gold = new[] { LitMaterial("MI_DizzyStar_Gold", Gold, GoldEmission), GlowMaterial("MI_DizzyStar_Gold_Glow", GoldGlow, 0.9f) };
        var purple = new[] { LitMaterial("MI_DizzyStar_Purple", Purple, PurpleEmission), GlowMaterial("MI_DizzyStar_Purple_Glow", PurpleGlow, 0.9f) };
        AddStars(AngelPrefab, "head", mesh, gold, -0.06f);   // circles her halo
        AddStars(EvilPrefab, "body", mesh, purple, 0.03f);   // just above the hood tip
    }

    static void AddStars(string prefabPath, string anchorBone, Mesh mesh, Material[] materials, float ringHeight)
    {
        var root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var anchor = root.GetComponentsInChildren<Transform>(true).First(t => t.name == anchorBone);
            var holder = root.transform.Find("DizzyStars") ?? new GameObject("DizzyStars").transform;
            holder.SetParent(root.transform, false);
            holder.localPosition = Vector3.zero; holder.localRotation = Quaternion.identity; holder.localScale = Vector3.one;
            var dizzy = GetOrAdd<DizzyStars>(holder.gameObject);

            var stars = new Transform[3];
            for (int i = 0; i < 3; i++)
            {
                var star = holder.Find($"Star{i + 1}") ?? new GameObject($"Star{i + 1}").transform;
                star.SetParent(holder, false);
                var mf = GetOrAdd<MeshFilter>(star.gameObject);
                var mr = GetOrAdd<MeshRenderer>(star.gameObject);
                mf.sharedMesh = mesh;
                mr.sharedMaterials = materials;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
                mr.lightProbeUsage = LightProbeUsage.Off;
                star.gameObject.SetActive(false);
                stars[i] = star;
            }

            // Ring centre: over the middle of the character, ringHeight (fraction of its height) from its top.
            var bounds = CharacterBounds(root);
            var centre = new Vector3(bounds.center.x, bounds.max.y + ringHeight * bounds.size.y, bounds.center.z);
            var so = new SerializedObject(dizzy);
            so.FindProperty("anchor").objectReferenceValue = anchor;
            so.FindProperty("offset").vector3Value = root.transform.InverseTransformVector(centre - anchor.position);
            so.FindProperty("radius").floatValue = 0.26f * Mathf.Max(bounds.size.x, bounds.size.z);
            so.FindProperty("starSize").floatValue = 0.19f * bounds.size.y;
            // tuned look, written explicitly (an existing prefab keeps old values when script defaults change)
            so.FindProperty("ringTilt").floatValue = -10f;
            so.FindProperty("faceCamera").floatValue = 0.8f;
            var arr = so.FindProperty("stars");
            arr.arraySize = stars.Length;
            for (int i = 0; i < stars.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = stars[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>A puffy cartoon star, 1 unit across, tip up, lying in the XY plane (faces +Z).</summary>
    public static Mesh StarMesh()
    {
        const int outline = 160, rings = 9;
        const float rOut = 0.5f, rIn = 0.25f, sharp = 2.0f, puff = 0.11f;
        var verts = new System.Collections.Generic.List<Vector3>();
        var tris = new System.Collections.Generic.List<int>();

        var edge = new Vector2[outline];
        for (int i = 0; i < outline; i++)
        {
            float a = Mathf.PI * 0.5f + i * 2f * Mathf.PI / outline;
            float lobe = Mathf.Pow(0.5f + 0.5f * Mathf.Cos(5f * (a - Mathf.PI * 0.5f)), sharp);
            float r = Mathf.Lerp(rIn, rOut, lobe);
            edge[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
        }

        // shared outline ring (z = 0), then the two domes
        int edgeStart = verts.Count;
        foreach (var e in edge) verts.Add(new Vector3(e.x, e.y, 0f));
        int[] Dome(float side)
        {
            var ringStart = new int[rings];
            for (int k = rings - 1; k >= 1; k--)        // k = rings-1 is next to the outline, k = 1 is next to the centre
            {
                float s = (float)k / rings;
                float z = side * puff * Mathf.Pow(1f - s * s, 0.55f);
                ringStart[k] = verts.Count;
                foreach (var e in edge) verts.Add(new Vector3(e.x * s, e.y * s, z));
            }
            ringStart[0] = verts.Count;
            verts.Add(new Vector3(0f, 0f, side * puff));
            return ringStart;
        }
        void Quad(int a, int b, int c, int d, bool flip)
        {
            if (flip) { tris.AddRange(new[] { a, c, b, b, c, d }); }
            else { tris.AddRange(new[] { a, b, c, b, d, c }); }
        }
        foreach (var side in new[] { 1f, -1f })
        {
            var ring = Dome(side);
            int RingIndex(int k, int i) => k == rings ? edgeStart + i : ring[k] + i;
            int start = tris.Count;
            for (int k = rings; k >= 2; k--)
                for (int i = 0; i < outline; i++)
                {
                    int j = (i + 1) % outline;
                    Quad(RingIndex(k, i), RingIndex(k, j), RingIndex(k - 1, i), RingIndex(k - 1, j), false);
                }
            for (int i = 0; i < outline; i++)
                tris.AddRange(new[] { RingIndex(1, i), RingIndex(1, (i + 1) % outline), ring[0] });

            // make this side face outwards (+Z for the front, -Z for the back)
            var n = Vector3.Cross(verts[tris[start + 1]] - verts[tris[start]], verts[tris[start + 2]] - verts[tris[start]]);
            if (Mathf.Sign(n.z) != side)
                for (int t = start; t < tris.Count; t += 3) { int tmp = tris[t + 1]; tris[t + 1] = tris[t + 2]; tris[t + 2] = tmp; }
        }

        var mesh = new Mesh { name = "DizzyStar" };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        return mesh;
    }

    // ------------------------------------------------------------------ Evil power-up

    static readonly Color FireGlow = new Color(2.4f, 0.9f, 0.25f);

    public static void BuildEvilPower()
    {
        EnsureFolder(FxDir);
        var glow = GlowMaterial("MI_Evil_PowerGlow", FireGlow, 0f);           // EvilPower drives the intensity
        var flameMat = ParticleMaterial("MI_Evil_PowerFlame", 2.2f);
        var emberMat = ParticleMaterial("MI_Evil_Ember", 3.2f);

        var root = PrefabUtility.LoadPrefabContents(EvilPrefab);
        try
        {
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var bones = root.GetComponentsInChildren<Transform>(true);
            Transform Bone(string n) => bones.First(t => t.name == n);
            var body = Bone("body");
            var flamesBone = Bone("flames");
            // flame.01-15 are children of "flames": scale only them, or the growth compounds
            var flameBones = Enumerable.Range(1, 15).Select(i => Bone($"flame.{i:00}")).ToArray();
            var renderer = root.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r => r.name == "Evil");

            // 3rd material slot: draws the body again into the glow buffer
            var mats = renderer.sharedMaterials.Take(2).ToList();
            mats.Add(glow);
            renderer.sharedMaterials = mats.ToArray();

            var bounds = CharacterBounds(root);
            var flameCentre = flameBones.Aggregate(Vector3.zero, (a, t) => a + t.position) / flameBones.Length;
            float flameSpread = flameBones.Average(t => Vector3.Distance(t.position, flameCentre));

            var light = Child(body, "PowerLight", bounds.center);
            var l = GetOrAdd<Light>(light.gameObject);
            l.type = LightType.Point;
            l.color = new Color(1f, 0.45f, 0.15f);
            l.intensity = 0f; l.range = 3.5f; l.shadows = LightShadows.None; l.enabled = false;

            var fx = Child(flamesBone, "PowerFlames", flameCentre);
            var flames = GetOrAdd<ParticleSystem>(fx.gameObject);
            SetupParticles(flames, flameMat, flameSpread * 0.9f,
                life: new Vector2(0.45f, 0.75f), size: new Vector2(0.16f, 0.28f), rise: 1.2f, noise: 0.3f, max: 160, stretched: true,
                gradient: new[] { (new Color(1f, 0.85f, 0.45f), 0f, 0f), (new Color(1f, 0.6f, 0.15f), 0.12f, 1f),
                                  (new Color(0.95f, 0.25f, 0.05f), 0.6f, 0.75f), (new Color(0.45f, 0.06f, 0.02f), 1f, 0f) });

            var ex = Child(flamesBone, "PowerEmbers", flameCentre + Vector3.up * 0.25f);
            var embers = GetOrAdd<ParticleSystem>(ex.gameObject);
            SetupParticles(embers, emberMat, flameSpread * 1.3f,
                life: new Vector2(0.9f, 1.6f), size: new Vector2(0.035f, 0.07f), rise: 1.8f, noise: 0.9f, max: 160, stretched: false,
                gradient: new[] { (new Color(1f, 0.9f, 0.5f), 0f, 0f), (new Color(1f, 0.65f, 0.2f), 0.1f, 1f),
                                  (new Color(1f, 0.3f, 0.06f), 0.7f, 0.8f), (new Color(0.6f, 0.1f, 0.03f), 1f, 0f) });

            var power = GetOrAdd<EvilPower>(root);
            var so = new SerializedObject(power);
            so.FindProperty("target").objectReferenceValue = renderer;
            so.FindProperty("flameMaterialIndex").intValue = 0;
            so.FindProperty("glowMaterialIndex").intValue = 2;
            so.FindProperty("body").objectReferenceValue = body;
            var arr = so.FindProperty("flameBones");
            arr.arraySize = flameBones.Length;
            for (int i = 0; i < flameBones.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = flameBones[i];
            so.FindProperty("powerLight").objectReferenceValue = l;
            so.FindProperty("powerFlames").objectReferenceValue = flames;
            so.FindProperty("embers").objectReferenceValue = embers;
            // look at full power (written explicitly: the prefab keeps old values when script defaults change)
            so.FindProperty("flameScale").floatValue = 1.5f;
            so.FindProperty("flameBrightness").floatValue = 1.5f;
            so.FindProperty("glowIntensity").floatValue = 0.45f;
            so.FindProperty("lightIntensity").floatValue = 1.6f;
            so.FindProperty("flameRate").floatValue = 26f;
            so.FindProperty("emberRate").floatValue = 24f;
            so.FindProperty("flameBurst").intValue = 16;
            so.FindProperty("emberBurst").intValue = 30;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, EvilPrefab);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>A child of <paramref name="parent"/> at a world position, axes aligned with the character (Y up).</summary>
    static Transform Child(Transform parent, string name, Vector3 worldPos)
    {
        var t = parent.Find(name) ?? new GameObject(name).transform;
        t.SetParent(parent, false);
        t.position = worldPos;
        t.rotation = Quaternion.identity;
        t.localScale = Vector3.one;
        return t;
    }

    static void SetupParticles(ParticleSystem ps, Material mat, float radius, Vector2 life, Vector2 size, float rise, float noise,
        int max, bool stretched, (Color c, float t, float a)[] gradient)
    {
        var main = ps.main;
        main.loop = true; main.playOnAwake = true; main.prewarm = false;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(life.x, life.y);
        main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = Color.white;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;   // stays with the character; scales with it
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = max;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;                                    // EvilPower drives the rate

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = radius;
        shape.radiusThickness = 1f;

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        vel.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.y = new ParticleSystem.MinMaxCurve(rise * 0.7f, rise * 1.3f);
        vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        var n = ps.noise;
        n.enabled = true; n.strength = noise; n.frequency = 1.2f; n.scrollSpeed = 0.6f; n.quality = ParticleSystemNoiseQuality.Medium;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(gradient.Select(k => new GradientColorKey(k.c, k.t)).ToArray(), gradient.Select(k => new GradientAlphaKey(k.a, k.t)).ToArray());
        col.color = g;

        var sz = ps.sizeOverLifetime;
        sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(0.2f, 1f), new Keyframe(1f, 0.15f)));

        var r = ps.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.renderMode = stretched ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
        r.velocityScale = stretched ? 0.08f : 0f;
        r.lengthScale = stretched ? 1.25f : 1f;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.sortingFudge = 10f;
    }

    static Material ParticleMaterial(string name, float intensity)
    {
        var m = LoadOrCreateMaterial(name, "GameJam/SoftGlowParticle");
        m.SetFloat("_Intensity", intensity);
        EditorUtility.SetDirty(m);
        return m;
    }

    // ------------------------------------------------------------------ shared helpers

    /// <summary>Unity's GetComponent returns a fake null in the Editor, so <c>??</c> can't be used to add one.</summary>
    static T GetOrAdd<T>(GameObject go) where T : Component => go.TryGetComponent<T>(out var c) ? c : go.AddComponent<T>();

    static Bounds CharacterBounds(GameObject root)
    {
        var rs = root.GetComponentsInChildren<Renderer>(true)
            .Where(r => !(r is ParticleSystemRenderer) && r.GetComponentInParent<DizzyStars>(true) == null).ToArray();
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    static Mesh SaveMesh(Mesh built, string path)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null) { AssetDatabase.CreateAsset(built, path); return built; }
        existing.Clear();
        existing.SetVertices(built.vertices);
        existing.SetTriangles(built.triangles, 0);
        existing.SetNormals(built.normals);
        existing.SetTangents(built.tangents);
        existing.RecalculateBounds();
        EditorUtility.SetDirty(existing);
        return existing;
    }

    static Material LitMaterial(string name, Color baseColor, Color emission)
    {
        var m = LoadOrCreateMaterial(name, "Universal Render Pipeline/Lit");
        m.SetColor("_BaseColor", baseColor);
        m.SetFloat("_Smoothness", 0.6f);
        m.SetFloat("_Metallic", 0.15f);
        m.SetColor("_EmissionColor", emission);
        m.EnableKeyword("_EMISSION");
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material GlowMaterial(string name, Color glow, float intensity)
    {
        var m = LoadOrCreateMaterial(name, "GameJam/GlowSource");
        m.SetColor("_GlowColor", glow);
        m.SetFloat("_GlowIntensity", intensity);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material LoadOrCreateMaterial(string name, string shader)
    {
        var path = $"{FxDir}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) { m.shader = Shader.Find(shader); return m; }
        m = new Material(Shader.Find(shader)) { name = name };
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }
}
