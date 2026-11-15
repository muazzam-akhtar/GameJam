using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Turns the open DemoScene into the split-screen Angel vs Evil game (menu GameJam/Game/Setup Split-Screen Game):
/// builds the Offering prefab and the post-processing profile, gives each player a close third-person camera on
/// its half of the screen, adds lives / the Angel's drop key / the game manager and HUD, sky and distance fog,
/// and switches off the old random collectible spawner. Safe to run again.
/// </summary>
public static class GameSetupBuilder
{
    public const string OfferingPath = "Assets/Prefabs/Gameplay/Offering.prefab";
    const string Dir = "Assets/Prefabs/Gameplay";
    const string ProfilePath = "Assets/Environment/PostFX_Courtyard.asset";
    const string SkyPath = "Assets/Silver_Cats/Hand_Painted_Nature_Kit_LITE/Skyboxes/Sky_01_Sun_Cloud.mat";

    [MenuItem("GameJam/Game/Setup Split-Screen Game")]
    public static void SetupAll()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("GameSetupBuilder: leave Play mode first."); return; }
        EnsureLayers();
        EnsureViewFxMaterials();
        var offering = BuildOfferingPrefab();
        var profile = BuildPostProfile();
        SetupOpenScene(offering, profile);
    }

    // ---------------------------------------------------------------- layers + view materials

    /// <summary>Layers for per-player visuals (SplitViewFX) and cave roofs (Hideout / FollowCamera).</summary>
    public static void EnsureLayers()
    {
        var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = tags.FindProperty("layers");
        foreach (var name in new[] { SplitViewFX.AngelViewLayer, SplitViewFX.EvilViewLayer, CourtyardBuilder.CaveRoofLayer })
        {
            bool found = false;
            for (int i = 0; i < layers.arraySize && !found; i++) found = layers.GetArrayElementAtIndex(i).stringValue == name;
            if (found) continue;
            for (int i = 8; i < layers.arraySize; i++)
            {
                var slot = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(slot.stringValue)) continue;
                slot.stringValue = name;
                break;
            }
        }
        tags.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>Materials SplitViewFX loads at runtime (in Resources so builds keep their shaders).</summary>
    public static void EnsureViewFxMaterials()
    {
        const string dir = "Assets/Resources/ViewFX";
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder("Assets/Resources", "ViewFX");
        void Make(string name, string shader, System.Action<Material> setup)
        {
            var path = $"{dir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(Shader.Find(shader)) { name = name }; AssetDatabase.CreateAsset(m, path); }
            m.shader = Shader.Find(shader);
            setup(m);
            EditorUtility.SetDirty(m);
        }
        Make("SoftShade", "GameJam/SoftShade", m => { m.SetColor("_Color", Color.white); m.SetFloat("_Softness", 1.0f); });
        Make("SoftGlow", "GameJam/SoftGlowParticle", m => m.SetFloat("_Intensity", 2.2f));
        AssetDatabase.SaveAssets();
    }

    // ---------------------------------------------------------------- offering prefab

    public static Offering BuildOfferingPrefab()
    {
        EnsureFolder(Dir);
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var root = new GameObject("Offering");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
            var offering = root.AddComponent<Offering>();
            var trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 20f, 0f);
            trigger.size = new Vector3(24f, 40f, 24f);

            // Art's golden pickup, scaled up to the characters (its particles follow the hierarchy scale)
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Collectable.prefab"), scene);
            visual.name = "Visual";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = Vector3.up * 9f;
            visual.transform.localScale = Vector3.one * 7f;
            foreach (var ps in visual.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            }

            // glowing ring on the ground that fills up as it ripens
            var ringGo = new GameObject("Ring");
            ringGo.transform.SetParent(root.transform, false);
            ringGo.AddComponent<MeshFilter>().sharedMesh = SaveAsset(RingMesh(8.5f, 11f, 72), $"{Dir}/OfferingRing.asset");
            var ringRenderer = ringGo.AddComponent<MeshRenderer>();
            // emissive + bloom only: the GlowSource pass draws through walls, which would give away offerings hidden in caves
            ringRenderer.sharedMaterials = new[] { RingMaterial() };
            ringRenderer.shadowCastingMode = ShadowCastingMode.Off;

            var lightGo = new GameObject("Glow");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.localPosition = Vector3.up * 12f;
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.8f, 0.42f);
            light.range = 48f;
            light.intensity = 3f;
            light.shadows = LightShadows.None;

            offering.visual = visual.transform;
            offering.ring = ringGo.transform;
            offering.ringRenderer = ringRenderer;
            offering.glowLight = light;
            offering.goodBurst = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Collect Burst Good.prefab");
            offering.badBurst = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Collect Burst BAD.prefab");
            offering.burstScale = 6f;

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, OfferingPath);
            return prefab.GetComponent<Offering>();
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    static Mesh RingMesh(float inner, float outer, int segments)
    {
        var mb = new MeshBuilder();
        for (int i = 0; i <= segments; i++)
        {
            float a = i * Mathf.PI * 2f / segments;
            var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            mb.AddVertex(dir * inner + Vector3.up * 0.3f, Vector3.up, Color.white, new Vector2(0f, i));
            mb.AddVertex(dir * outer + Vector3.up * 0.3f, Vector3.up, Color.white, new Vector2(1f, i));
        }
        for (int i = 0; i < segments; i++)
        {
            int a = i * 2;
            mb.AddTriangle(a, a + 1, a + 2, Vector3.up);
            mb.AddTriangle(a + 1, a + 3, a + 2, Vector3.up);
        }
        var mesh = mb.Build("OfferingRing");
        var tris = mesh.GetTriangles(0);
        mesh.subMeshCount = 2;                        // second pass: GlowSource
        mesh.SetTriangles(tris, 1);
        return mesh;
    }

    static Material RingMaterial()
    {
        var m = LoadOrCreate($"{Dir}/MI_OfferingRing.mat", "GameJam/StylizedEnv");
        m.SetColor("_BaseColor", new Color(0.35f, 0.25f, 0.08f));
        m.SetColor("_EmissionColor", new Color(2.4f, 1.7f, 0.5f));
        m.SetFloat("_MottleStrength", 0f);
        m.SetFloat("_RimStrength", 0f);
        m.SetFloat("_Cull", 0f);
        return m;
    }

    // ---------------------------------------------------------------- post-processing

    public static VolumeProfile BuildPostProfile()
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if (!profile)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);
        }
        T Get<T>() where T : VolumeComponent
        {
            if (profile.TryGet<T>(out var c)) return c;
            c = profile.Add<T>(true);
            c.name = typeof(T).Name;
            AssetDatabase.AddObjectToAsset(c, profile);
            return c;
        }
        var bloom = Get<Bloom>();
        bloom.threshold.Override(1.0f);
        bloom.intensity.Override(0.55f);
        bloom.scatter.Override(0.65f);
        var tone = Get<Tonemapping>();
        tone.mode.Override(TonemappingMode.Neutral);
        var color = Get<ColorAdjustments>();
        color.postExposure.Override(0.25f);
        color.contrast.Override(12f);
        color.saturation.Override(14f);
        var vignette = Get<Vignette>();
        vignette.intensity.Override(0.24f);
        vignette.smoothness.Override(0.45f);
        var white = Get<WhiteBalance>();
        white.temperature.Override(6f);
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        return profile;
    }

    // ---------------------------------------------------------------- scene

    public static void SetupOpenScene(Offering offeringPrefab, VolumeProfile profile)
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var angelGo = GameObject.Find("Angel");
        var evilGo = GameObject.Find("Evil");
        var angel = angelGo.GetComponent<AngelController>();
        var evil = evilGo.GetComponent<EvilController>();

        GetOrAdd<PlayerLives>(angelGo);
        GetOrAdd<PlayerLives>(evilGo);
        var offeringsRoot = FindOrCreate("Offerings");
        var dropper = GetOrAdd<AngelDropper>(angelGo);
        dropper.offeringPrefab = offeringPrefab;
        dropper.container = offeringsRoot.transform;
        foreach (var c in new MonoBehaviour[] { angel, evil })
        {
            var so = new SerializedObject(c);
            so.FindProperty("testKeys").boolValue = false;    // the real game: no debug keys
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // split screen: Angel left, Evil right, each with a close third-person camera
        var angelCamGo = GameObject.Find("Camera_Angel") ?? GameObject.Find("Main Camera");
        angelCamGo.name = "Camera_Angel";
        angelCamGo.tag = "MainCamera";
        var evilCamGo = GameObject.Find("Camera_Evil") ?? new GameObject("Camera_Evil", typeof(Camera));
        var angelFollow = SetupCamera(angelCamGo, new Rect(0f, 0f, 0.5f, 1f), angelGo.transform);
        var evilFollow = SetupCamera(evilCamGo, new Rect(0.5f, 0f, 0.5f, 1f), evilGo.transform);
        var extraListener = evilCamGo.GetComponent<AudioListener>();
        if (extraListener) Object.DestroyImmediate(extraListener);
        if (!angelCamGo.GetComponent<AudioListener>()) angelCamGo.AddComponent<AudioListener>();

        var volumeGo = FindOrCreate("PostProcessing");
        var volume = GetOrAdd<Volume>(volumeGo);
        volume.isGlobal = true;
        volume.priority = 1f;
        volume.sharedProfile = profile;

        // sky + soft distance haze so the third-person views fade into the forest
        var sky = AssetDatabase.LoadAssetAtPath<Material>(SkyPath);
        if (sky) RenderSettings.skybox = sky;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.78f, 0.82f, 0.88f);
        RenderSettings.fogStartDistance = 420f;
        RenderSettings.fogEndDistance = 1400f;
        DynamicGI.UpdateEnvironment();

        var game = FindOrCreate("Game");
        var manager = GetOrAdd<GameManager>(game);
        var hud = GetOrAdd<GameHUD>(game);
        var gso = new SerializedObject(manager);
        gso.FindProperty("angel").objectReferenceValue = angel;
        gso.FindProperty("evil").objectReferenceValue = evil;
        gso.FindProperty("angelCamera").objectReferenceValue = angelFollow;
        gso.FindProperty("evilCamera").objectReferenceValue = evilFollow;
        gso.FindProperty("hud").objectReferenceValue = hud;
        gso.FindProperty("scoreManager").objectReferenceValue = Object.FindFirstObjectByType<ScoreManager>();
        gso.FindProperty("offerings").objectReferenceValue = offeringsRoot.transform;
        gso.ApplyModifiedPropertiesWithoutUndo();

        // the old random collectible spawner and Art's test bursts sitting in the scene are not part of this game
        foreach (var name in new[] { "SpawnRegion", "Collect Burst Good", "Collect Burst BAD" })
        {
            var go = scene.GetRootGameObjects().FirstOrDefault(g => g.name == name);
            if (go) go.SetActive(false);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    static FollowCamera SetupCamera(GameObject go, Rect rect, Transform target)
    {
        var cam = go.GetComponent<Camera>();
        cam.rect = rect;
        cam.fieldOfView = 52f;
        cam.nearClipPlane = 0.5f;
        cam.farClipPlane = 2500f;
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.allowHDR = true;
        var data = cam.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = true;
        data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        data.antialiasingQuality = AntialiasingQuality.High;
        data.renderShadows = true;
        var follow = GetOrAdd<FollowCamera>(go);
        follow.target = target;
        go.transform.position = target.position + follow.offset;   // looks right in the editor too
        go.transform.rotation = Quaternion.LookRotation(target.position + Vector3.up * follow.lookHeight - go.transform.position);
        return follow;
    }

    // ---------------------------------------------------------------- helpers

    static T GetOrAdd<T>(GameObject go) where T : Component => go.TryGetComponent<T>(out var c) ? c : go.AddComponent<T>();

    static GameObject FindOrCreate(string name)
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        return scene.GetRootGameObjects().FirstOrDefault(g => g.name == name) ?? new GameObject(name);
    }

    static Material LoadOrCreate(string path, string shader)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m) { m = new Material(Shader.Find(shader)); AssetDatabase.CreateAsset(m, path); }
        EditorUtility.SetDirty(m);
        return m;
    }

    static Mesh SaveAsset(Mesh mesh, string path)
    {
        if (AssetDatabase.LoadAssetAtPath<Mesh>(path)) AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }
}
