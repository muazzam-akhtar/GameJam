using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GameJamTests
{
    public class ReactionClipTests
    {
        const string AngelPrefab = "Assets/Prefabs/Characters/Angel/Angel.prefab";
        const string EvilPrefab = "Assets/Prefabs/Characters/Evil/Evil.prefab";

        static AnimatorController Ctrl(string prefab) =>
            (AnimatorController)AssetDatabase.LoadAssetAtPath<GameObject>(prefab).GetComponent<Animator>().runtimeAnimatorController;

        static AnimatorState State(AnimatorController c, string name) =>
            c.layers[0].stateMachine.states.Select(s => s.state).FirstOrDefault(s => s.name == name);

        static string[] Triggers(AnimatorController c) =>
            c.parameters.Where(p => p.type == AnimatorControllerParameterType.Trigger).Select(p => p.name).OrderBy(n => n).ToArray();

        static void AssertReaction(AnimatorController c, string stateName, string trigger, string clipName, float length)
        {
            var state = State(c, stateName);
            Assert.NotNull(state, $"state {stateName}");
            var clip = state.motion as AnimationClip;
            Assert.NotNull(clip, $"{stateName} has a clip");
            Assert.AreEqual(clipName, clip.name, $"{stateName} clip");
            Assert.AreEqual(length, clip.length, 0.02f, $"{stateName} length");
            var any = c.layers[0].stateMachine.anyStateTransitions.Where(t => t.destinationState == state).ToArray();
            Assert.AreEqual(1, any.Length, $"one AnyState transition into {stateName}");
            CollectionAssert.AreEqual(new[] { trigger }, any[0].conditions.Select(x => x.parameter).ToArray(), $"{stateName} trigger");
            Assert.IsTrue(state.transitions.Any(t => t.destinationState != null && t.destinationState.name == "Idle" && t.hasExitTime),
                $"{stateName} returns to Idle");
        }

        [Test]
        public void Angel_Controller_HasSlapStungPowerUp()
        {
            var c = Ctrl(AngelPrefab);
            CollectionAssert.AreEqual(new[] { "PowerUp", "Slap", "Stung" }, Triggers(c));
            AssertReaction(c, "Slap", "Slap", "Slap", 1.0f);
            AssertReaction(c, "Stung", "Stung", "Angel_Stung", 0.8f);
            AssertReaction(c, "PowerUp", "PowerUp", "Angel_PowerUp", 1.0f);
        }

        [Test]
        public void Evil_Controller_HasSlapStungPowerUp()
        {
            var c = Ctrl(EvilPrefab);
            CollectionAssert.AreEqual(new[] { "PowerUp", "Slap", "Stung" }, Triggers(c));
            AssertReaction(c, "Slap", "Slap", "Slap", 1.0f);
            AssertReaction(c, "Stung", "Stung", "Stung", 0.8f);
            AssertReaction(c, "PowerUp", "PowerUp", "PowerUp", 1.0f);
            Assert.AreEqual(EvilPrefab, AssetDatabase.GetAssetPath(State(c, "Stung").motion), "Evil Stung clip lives inside Evil.prefab");
            Assert.AreEqual(EvilPrefab, AssetDatabase.GetAssetPath(State(c, "PowerUp").motion), "Evil PowerUp clip lives inside Evil.prefab");
        }

        [TestCase(AngelPrefab, "Stung")]
        [TestCase(AngelPrefab, "PowerUp")]
        [TestCase(EvilPrefab, "Stung")]
        [TestCase(EvilPrefab, "PowerUp")]
        public void Clip_Bindings_ResolveOnPrefab(string prefab, string stateName)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(prefab);
            var state = State(Ctrl(prefab), stateName);
            Assert.NotNull(state, $"state {stateName}");
            Assert.AreNotEqual("Slap", state.motion.name, $"{stateName} must play its own reaction clip, not the slap swing");
            var bindings = AnimationUtility.GetCurveBindings((AnimationClip)state.motion);
            Assert.IsNotEmpty(bindings);
            foreach (var b in bindings)
                Assert.NotNull(b.path.Length == 0 ? go.transform : go.transform.Find(b.path), $"binding path {b.path}");
        }

        [TestCase(AngelPrefab)]
        [TestCase(EvilPrefab)]
        public void PowerUp_TurnsFullCircle(string prefab)
        {
            var state = State(Ctrl(prefab), "PowerUp");
            Assert.NotNull(state, "PowerUp state");
            var clip = (AnimationClip)state.motion;
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(prefab), scene);
                var body = go.GetComponentsInChildren<Transform>(true).First(t => t.name == "body");
                Quaternion RootRot() => Quaternion.Inverse(go.transform.rotation) * body.rotation;

                clip.SampleAnimation(go, 0f);
                var axis = new[] { Vector3.right, Vector3.up, Vector3.forward }
                    .OrderBy(a => Mathf.Abs((RootRot() * a).y)).First();   // a body axis that lies flat
                Vector3 Flat() { var v = RootRot() * axis; v.y = 0; return v.normalized; }

                float total = 0f;
                var prev = Flat();
                const int steps = 120;
                for (int i = 1; i <= steps; i++)
                {
                    clip.SampleAnimation(go, clip.length * i / steps);
                    var cur = Flat();
                    total += Vector3.SignedAngle(prev, cur, Vector3.up);
                    prev = cur;
                }
                Assert.GreaterOrEqual(Mathf.Abs(total), 350f, $"twirl turned {total:F0} degrees");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        [TestCase(AngelPrefab, "Stung")]
        [TestCase(AngelPrefab, "PowerUp")]
        [TestCase(EvilPrefab, "Stung")]
        [TestCase(EvilPrefab, "PowerUp")]
        public void Clip_ConstantChannels_AreStoredCompactly(string prefab, string stateName)
        {
            // keeps Evil.prefab (which embeds its clips) well under GitHub's 100 MB file limit
            var state = State(Ctrl(prefab), stateName);
            Assert.NotNull(state, $"state {stateName}");
            var clip = (AnimationClip)state.motion;
            int bloated = 0;
            foreach (var b in AnimationUtility.GetCurveBindings(clip))
            {
                var keys = AnimationUtility.GetEditorCurve(clip, b).keys;
                if (keys.Length > 2 && keys.All(k => Mathf.Abs(k.value - keys[0].value) < 1e-5f)) bloated++;
            }
            Assert.AreEqual(0, bloated, "unchanging channels keep only 2 keys");
        }

        [Test]
        public void Height_IgnoresDizzyStars()
        {
            // the stars sit inactive at the character's origin; counting them would change the retarget scale
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(EvilPrefab), scene);
                var body = go.GetComponentsInChildren<Renderer>(true)
                    .Where(r => !(r is ParticleSystemRenderer) && r.GetComponentInParent<DizzyStars>(true) == null).ToArray();
                var b = body[0].bounds;
                foreach (var r in body) b.Encapsulate(r.bounds);
                Assert.AreEqual(b.size.y, ReactionClipBuilder.Height(go), 1e-4f);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
