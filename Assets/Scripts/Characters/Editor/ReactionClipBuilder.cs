using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Builds the Angel's and the Evil's reaction clips from the main character's:
///   Stung   (hit reaction, both)      = MainChar "Stung"
///   PowerUp (jump + twirl, both)       = MainChar "PowerUp"
/// The motion is copied bone by bone (as deltas in character-root space, so different bone axes don't matter),
/// scaled to each character's height, and layered on top of the character's own Fly loop so wings, flames and
/// coat keep moving. Lagging bones (lantern, coat, wings, tail) trail the body for follow-through.
/// Also rewires both animator controllers: the old "Stung" state (it played the Slap swing) becomes "Slap".
/// Safe to run again: clips are rewritten in place and states/transitions are reused.
/// </summary>
public static class ReactionClipBuilder
{
    const string MainCharPrefab = "Assets/Prefabs/Characters/MainChar/MainChar.prefab";
    const string AngelPrefab = "Assets/Prefabs/Characters/Angel/Angel.prefab";
    const string EvilPrefab = "Assets/Prefabs/Characters/Evil/Evil.prefab";
    const string AngelClipDir = "Assets/Prefabs/Characters/Angel/";
    const float Fps = 60f;

    /// <summary>A bone that trails the body: it follows the body's motion from <c>delay</c> seconds ago, by <c>amount</c>.</summary>
    public readonly struct Lag
    {
        public readonly string bone; public readonly float delay, amount;
        public Lag(string bone, float delay, float amount) { this.bone = bone; this.delay = delay; this.amount = amount; }
    }

    static readonly (string src, string dst)[] AngelMap = { ("body", "body"), ("head", "head"), ("arm.L", "hand.L"), ("arm.R", "hand.R") };
    static readonly (string src, string dst)[] EvilMap = { ("body", "body"), ("arm.R", "hand.R") };

    static readonly Lag[] AngelLags = { new Lag("wing.L", 0.06f, 0.5f), new Lag("wing.R", 0.06f, 0.5f), new Lag("tail", 0.10f, 0.6f) };
    static readonly Lag[] AngelTwirlLags = { new Lag("wing.L", 0.05f, 0.25f), new Lag("wing.R", 0.05f, 0.25f), new Lag("tail", 0.08f, 0.35f) };
    static readonly string[] CoatSides = { "F", "L", "BL", "B", "BR", "R" };
    static Lag[] EvilLags()
    {
        var lags = new List<Lag> { new Lag("lantern", 0.08f, 0.8f) };
        foreach (var s in CoatSides) lags.Add(new Lag($"coat.{s}.1", 0.08f, 0.6f));
        foreach (var s in CoatSides) lags.Add(new Lag($"coat.{s}.2", 0.14f, 0.6f));
        return lags.ToArray();
    }
    /// <summary>Gentler trailing for the twirl (a full spin would otherwise whip the lantern and coat around).</summary>
    static Lag[] EvilTwirlLags()
    {
        var lags = new List<Lag> { new Lag("lantern", 0.06f, 0.4f) };
        foreach (var s in CoatSides) lags.Add(new Lag($"coat.{s}.1", 0.06f, 0.3f));
        foreach (var s in CoatSides) lags.Add(new Lag($"coat.{s}.2", 0.10f, 0.3f));
        return lags.ToArray();
    }

    [MenuItem("GameJam/Characters/Build Reaction Clips")]
    public static void BuildAll()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("ReactionClipBuilder: leave Play mode first.");
            return;
        }
        var mainChar = AssetDatabase.LoadAssetAtPath<GameObject>(MainCharPrefab);
        var mcIdle = SubClip(MainCharPrefab, "Idle");
        var mcStung = SubClip(MainCharPrefab, "Stung");
        var mcPowerUp = SubClip(MainCharPrefab, "PowerUp");

        // Angel: separate .anim files next to her FBX
        var angel = AssetDatabase.LoadAssetAtPath<GameObject>(AngelPrefab);
        var angelCtrl = Controller(angel);
        var angelFly = angelCtrl.animationClips.First(c => c.name == "Fly");
        var aStung = SaveClipFile(Retarget(mainChar, mcIdle, mcStung, angel, angelFly, AngelMap, AngelLags), AngelClipDir + "Angel_Stung.anim");
        var aPower = SaveClipFile(Retarget(mainChar, mcIdle, mcPowerUp, angel, angelFly, AngelMap, AngelTwirlLags), AngelClipDir + "Angel_PowerUp.anim");
        WireController(angelCtrl, new[] { ("Stung", aStung), ("PowerUp", aPower) });

        // Evil: everything stays inside Evil.prefab
        var evil = AssetDatabase.LoadAssetAtPath<GameObject>(EvilPrefab);
        var evilCtrl = Controller(evil);
        var evilFly = evilCtrl.animationClips.First(c => c.name == "Fly");
        var eStung = SaveSubClip(Retarget(mainChar, mcIdle, mcStung, evil, evilFly, EvilMap, EvilLags()), EvilPrefab, "Stung");
        var ePower = SaveSubClip(Retarget(mainChar, mcIdle, mcPowerUp, evil, evilFly, EvilMap, EvilTwirlLags()), EvilPrefab, "PowerUp");
        WireController(evilCtrl, new[] { ("Stung", eStung), ("PowerUp", ePower) });

        AssetDatabase.SaveAssets();
        Debug.Log("ReactionClipBuilder: built Angel_Stung, Angel_PowerUp, Evil Stung, Evil PowerUp and rewired both controllers.");
    }

    // ------------------------------------------------------------------ retargeting

    /// <summary>Samples <paramref name="srcMotion"/> on the source rig and writes the matching motion for the destination rig.</summary>
    public static AnimationClip Retarget(GameObject srcPrefab, AnimationClip srcRest, AnimationClip srcMotion,
        GameObject dstPrefab, AnimationClip dstBase, (string src, string dst)[] map, Lag[] lags)
    {
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var s = (GameObject)PrefabUtility.InstantiatePrefab(srcPrefab, scene);
            var d = (GameObject)PrefabUtility.InstantiatePrefab(dstPrefab, scene);
            s.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            d.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            var sB = map.Select(m => Bone(s, m.src)).ToArray();
            var dB = map.Select(m => Bone(d, m.dst)).ToArray();
            int bodyIndex = System.Array.FindIndex(map, m => m.dst == "body");

            srcRest.SampleAnimation(s, 0f);
            var sRestRot = sB.Select(b => b.localRotation).ToArray();
            var sRestPos = sB.Select(b => b.localPosition).ToArray();
            var sRestScale = sB.Select(b => b.localScale).ToArray();
            var sParentRot = sB.Select(b => RootRot(s, b.parent)).ToArray();
            var sParentScale = sB.Select(b => RootScale(s, b.parent)).ToArray();
            int sVertical = VerticalAxis(RootRot(s, sB[bodyIndex]));
            float sHeight = Height(s);

            dstBase.SampleAnimation(d, 0f);
            var dParentRot = dB.Select(b => RootRot(d, b.parent)).ToArray();
            var dParentScale = dB.Select(b => RootScale(d, b.parent)).ToArray();
            int dVertical = VerticalAxis(RootRot(d, dB[bodyIndex]));
            float heightRatio = Height(d) / sHeight;

            var lagBones = (lags ?? new Lag[0]).Select(l => (lag: l, t: Bone(d, l.bone))).OrderBy(x => Depth(x.t, d.transform)).ToArray();

            var paths = AnimationUtility.GetCurveBindings(dstBase).Select(b => b.path)
                .Concat(dB.Select(b => PathOf(b, d.transform)))
                .Concat(lagBones.Select(x => PathOf(x.t, d.transform)))
                .Distinct().ToArray();
            var targets = paths.Select(p => p.Length == 0 ? d.transform : d.transform.Find(p)).ToArray();

            int n = Mathf.RoundToInt(srcMotion.length * Fps) + 1;
            var rot = new Quaternion[paths.Length][]; var pos = new Vector3[paths.Length][]; var scl = new Vector3[paths.Length][];
            for (int p = 0; p < paths.Length; p++) { rot[p] = new Quaternion[n]; pos[p] = new Vector3[n]; scl[p] = new Vector3[n]; }
            var bodyDeltaRoot = new Quaternion[n];

            for (int i = 0; i < n; i++)
            {
                float t = Mathf.Min(i / Fps, srcMotion.length);
                srcMotion.SampleAnimation(s, t);
                dstBase.SampleAnimation(d, Mathf.Repeat(t, dstBase.length));

                for (int k = 0; k < map.Length; k++)
                {
                    var src = sB[k]; var dst = dB[k];
                    dst.localRotation = ReactionRetarget.TransferRotation(sRestRot[k], src.localRotation, sParentRot[k], dParentRot[k], dst.localRotation);
                    if (k != bodyIndex) continue;

                    bodyDeltaRoot[i] = sParentRot[k] * (src.localRotation * Quaternion.Inverse(sRestRot[k])) * Quaternion.Inverse(sParentRot[k]);
                    dst.localPosition += ReactionRetarget.TransferOffset(src.localPosition - sRestPos[k], sParentRot[k], sParentScale[k],
                        dParentRot[k], dParentScale[k], heightRatio);
                    float v = Ratio(src.localScale, sRestScale[k], sVertical);
                    float h = 0.5f * (Ratio(src.localScale, sRestScale[k], (sVertical + 1) % 3) + Ratio(src.localScale, sRestScale[k], (sVertical + 2) % 3));
                    var f = new Vector3(h, h, h); f[dVertical] = v;
                    dst.localScale = Vector3.Scale(dst.localScale, f);
                }

                foreach (var (lag, bone) in lagBones)
                {
                    int j = Mathf.Max(0, i - Mathf.RoundToInt(lag.delay * Fps));
                    var trail = Quaternion.Slerp(Quaternion.identity, bodyDeltaRoot[j] * Quaternion.Inverse(bodyDeltaRoot[i]), lag.amount);
                    var parentRoot = RootRot(d, bone.parent);
                    bone.localRotation = Quaternion.Inverse(parentRoot) * trail * parentRoot * bone.localRotation;
                }

                for (int p = 0; p < paths.Length; p++)
                {
                    rot[p][i] = targets[p].localRotation; pos[p][i] = targets[p].localPosition; scl[p][i] = targets[p].localScale;
                }
            }

            var clip = new AnimationClip { frameRate = Fps };
            for (int p = 0; p < paths.Length; p++)
            {
                ReactionRetarget.MakeContinuous(rot[p]);
                Write(clip, paths[p], "m_LocalRotation", n, i => new Vector4(rot[p][i].x, rot[p][i].y, rot[p][i].z, rot[p][i].w), 4);
                Write(clip, paths[p], "m_LocalPosition", n, i => pos[p][i], 3);
                Write(clip, paths[p], "m_LocalScale", n, i => scl[p][i], 3);
            }
            clip.EnsureQuaternionContinuity();
            return clip;
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    static void Write(AnimationClip clip, string path, string prop, int n, System.Func<int, Vector4> value, int dims)
    {
        string[] axes = { "x", "y", "z", "w" };
        for (int a = 0; a < dims; a++)
        {
            float first = value(0)[a];
            bool constant = true;
            for (int i = 1; i < n && constant; i++) constant = Mathf.Abs(value(i)[a] - first) < 1e-5f;
            // a channel that never changes needs only 2 keys (keeps Evil.prefab, which embeds its clips, small)
            var keys = constant ? new[] { new Keyframe(0f, first), new Keyframe((n - 1) / Fps, first) } : new Keyframe[n];
            if (!constant) for (int i = 0; i < n; i++) keys[i] = new Keyframe(i / Fps, value(i)[a]);
            var curve = new AnimationCurve(keys);
            for (int i = 0; i < keys.Length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
            }
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), $"{prop}.{axes[a]}"), curve);
        }
    }

    // ------------------------------------------------------------------ assets

    static AnimationClip SaveClipFile(AnimationClip built, string path)
    {
        var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (existing == null)
        {
            built.name = System.IO.Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(built, path);
            existing = built;
        }
        else CopyCurves(built, existing);
        SetNoLoop(existing);
        return existing;
    }

    static AnimationClip SaveSubClip(AnimationClip built, string assetPath, string name)
    {
        var existing = AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<AnimationClip>().FirstOrDefault(c => c.name == name);
        if (existing == null)
        {
            built.name = name;
            AssetDatabase.AddObjectToAsset(built, assetPath);
            existing = built;
        }
        else CopyCurves(built, existing);
        SetNoLoop(existing);
        return existing;
    }

    static void CopyCurves(AnimationClip from, AnimationClip to)
    {
        to.ClearCurves();
        to.frameRate = from.frameRate;
        foreach (var b in AnimationUtility.GetCurveBindings(from))
            AnimationUtility.SetEditorCurve(to, b, AnimationUtility.GetEditorCurve(from, b));
        to.EnsureQuaternionContinuity();
    }

    static void SetNoLoop(AnimationClip clip)
    {
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = false;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
    }

    // ------------------------------------------------------------------ animator controllers

    /// <summary>Old "Stung" (Slap clip) becomes "Slap" on trigger "Slap"; each reaction gets a state on its own trigger.</summary>
    static void WireController(AnimatorController c, (string name, AnimationClip clip)[] reactions)
    {
        var sm = c.layers[0].stateMachine;
        var idle = sm.states.Select(s => s.state).First(s => s.name == "Idle");
        var slap = sm.states.Select(s => s.state).First(s => s.motion != null && s.motion.name == "Slap");
        slap.name = "Slap";
        EnsureTrigger(c, "Slap");
        ConfigureReaction(sm, slap, "Slap", idle);

        float y = 120f;
        foreach (var (name, clip) in reactions)
        {
            EnsureTrigger(c, name);
            var state = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == name && s != slap)
                        ?? sm.AddState(name, new Vector3(520f, y, 0f));
            y += 70f;
            state.motion = clip;
            ConfigureReaction(sm, state, name, idle);
        }
        EditorUtility.SetDirty(c);
    }

    static void ConfigureReaction(AnimatorStateMachine sm, AnimatorState state, string trigger, AnimatorState idle)
    {
        var any = sm.anyStateTransitions.Where(t => t.destinationState == state).ToList();
        while (any.Count > 1) { sm.RemoveAnyStateTransition(any[1]); any.RemoveAt(1); }
        var anyT = any.Count == 1 ? any[0] : sm.AddAnyStateTransition(state);
        anyT.conditions = new[] { new AnimatorCondition { mode = AnimatorConditionMode.If, parameter = trigger } };
        anyT.hasExitTime = false; anyT.hasFixedDuration = true; anyT.duration = 0.08f; anyT.canTransitionToSelf = false;
        EditorUtility.SetDirty(anyT);

        var back = state.transitions.Where(t => t.destinationState == idle).ToList();
        while (back.Count > 1) { state.RemoveTransition(back[1]); back.RemoveAt(1); }
        var backT = back.Count == 1 ? back[0] : state.AddTransition(idle);
        backT.conditions = new AnimatorCondition[0];
        backT.hasExitTime = true; backT.exitTime = 0.92f; backT.hasFixedDuration = true; backT.duration = 0.12f;
        EditorUtility.SetDirty(backT);
        EditorUtility.SetDirty(state);
    }

    static void EnsureTrigger(AnimatorController c, string name)
    {
        if (c.parameters.Any(p => p.name == name)) return;
        c.AddParameter(name, AnimatorControllerParameterType.Trigger);
    }

    // ------------------------------------------------------------------ helpers

    static AnimatorController Controller(GameObject prefab) => (AnimatorController)prefab.GetComponent<Animator>().runtimeAnimatorController;

    static AnimationClip SubClip(string assetPath, string name) =>
        AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<AnimationClip>().First(c => c.name == name);

    static Transform Bone(GameObject root, string name) =>
        root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name)
        ?? throw new System.ArgumentException($"{root.name} has no bone '{name}'");

    static Quaternion RootRot(GameObject root, Transform t) => Quaternion.Inverse(root.transform.rotation) * t.rotation;

    static Vector3 RootScale(GameObject root, Transform t)
    {
        var a = t.lossyScale; var r = root.transform.lossyScale;
        return new Vector3(a.x / r.x, a.y / r.y, a.z / r.z);
    }

    static int VerticalAxis(Quaternion rootRot)
    {
        int best = 0; float bestDot = -1f;
        for (int a = 0; a < 3; a++)
        {
            var axis = Vector3.zero; axis[a] = 1f;
            float dot = Mathf.Abs(Vector3.Dot(rootRot * axis, Vector3.up));
            if (dot > bestDot) { bestDot = dot; best = a; }
        }
        return best;
    }

    static float Ratio(Vector3 now, Vector3 rest, int axis) => Mathf.Abs(rest[axis]) < 1e-6f ? 1f : now[axis] / rest[axis];

    public static float Height(GameObject go)
    {
        // the character's own meshes only: not particles, not the dizzy stars (they sit inactive at the origin)
        var rs = go.GetComponentsInChildren<Renderer>(true)
            .Where(r => !(r is ParticleSystemRenderer) && r.GetComponentInParent<DizzyStars>(true) == null).ToArray();
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b.size.y;
    }

    static int Depth(Transform t, Transform root) { int d = 0; while (t != null && t != root) { d++; t = t.parent; } return d; }

    static string PathOf(Transform t, Transform root)
    {
        var parts = new List<string>();
        while (t != null && t != root) { parts.Insert(0, t.name); t = t.parent; }
        return string.Join("/", parts);
    }
}
