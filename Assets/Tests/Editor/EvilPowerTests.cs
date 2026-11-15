using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameJamTests
{
    public class EvilPowerTests
    {
        const string EvilPrefab = "Assets/Prefabs/Characters/Evil/Evil.prefab";
        Scene scene;
        GameObject evil;

        [SetUp]
        public void SetUp()
        {
            scene = EditorSceneManager.NewPreviewScene();
            evil = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(EvilPrefab), scene);
        }

        [TearDown] public void TearDown() => EditorSceneManager.ClosePreviewScene(scene);

        EvilPower Power()
        {
            var p = evil.GetComponentInChildren<EvilPower>(true);
            Assert.NotNull(p, "Evil.prefab has EvilPower");
            return p;
        }

        static void Run(EvilPower p, float seconds, float dt = 0.05f)
        {
            for (float t = 0f; t < seconds - 1e-4f; t += dt) p.Tick(dt);
        }

        [Test]
        public void Activate_RampsToFull_HoldsThenFades()
        {
            var p = Power();
            Assert.AreEqual(0f, p.Power, 1e-4f, "off before Activate");
            p.Activate();
            p.Tick(0.01f);
            Assert.Less(p.Power, 0.1f, "ramps in, no pop");
            Run(p, 0.29f);                                   // t = 0.30
            Assert.AreEqual(1f, p.Power, 1e-3f, "full after 0.25 s");
            Run(p, 3.7f);                                    // t = 4.00
            Assert.AreEqual(1f, p.Power, 1e-3f, "still full at 4.0 s");
            Run(p, 1.6f);                                    // t = 5.60 > 0.25 + 4 + 1.2
            Assert.AreEqual(0f, p.Power, 1e-3f, "faded out");
            Assert.IsFalse(p.IsPowered);
        }

        [Test]
        public void Activate_WhilePowered_RestartsHold_NoDip()
        {
            var p = Power();
            p.Activate();
            Run(p, 3.0f);
            p.Activate();                                    // second pickup at 3 s
            float min = 1f;
            for (int i = 0; i < 78; i++) { p.Tick(0.05f); min = Mathf.Min(min, p.Power); }   // 3.0 .. 6.9 s
            Assert.AreEqual(1f, min, 1e-3f, "no dip; hold restarted");
            Run(p, 1.6f);                                    // t = 8.5
            Assert.AreEqual(0f, p.Power, 1e-3f);
        }

        [Test]
        public void Flames_GrowWithPower_AndReturnToSize()
        {
            var p = Power();
            var flame = evil.GetComponentsInChildren<Transform>(true).First(t => t.name == "flame.01");
            var parent = evil.GetComponentsInChildren<Transform>(true).First(t => t.name == "flames");
            var baseScale = flame.localScale;
            var parentBase = parent.localScale;
            p.Activate();
            Run(p, 1.0f);
            Assert.AreEqual(baseScale.x * 1.5f, flame.localScale.x, 0.02f * baseScale.x, "flames ×1.5 at full power");
            Assert.AreEqual(parentBase.x, parent.localScale.x, 1e-4f, "the 'flames' parent is not scaled (no compounding)");
            Run(p, 5.0f);
            Assert.AreEqual(baseScale.x, flame.localScale.x, 0.01f * baseScale.x, "back to normal, no compounding");
        }

        [Test]
        public void Light_ScalesWithCharacter()
        {
            // DemoScene scales the characters x10: light falls off with distance squared, so intensity needs x100
            evil.transform.localScale = Vector3.one * 10f;
            var p = Power();
            var so = new SerializedObject(p);
            float intensity = so.FindProperty("lightIntensity").floatValue;
            float range = so.FindProperty("lightRange").floatValue;
            var light = (Light)so.FindProperty("powerLight").objectReferenceValue;
            p.Activate();
            Run(p, 1.0f);
            Assert.AreEqual(range * 10f, light.range, 1e-3f, "range scales with the character");
            Assert.AreEqual(intensity * 100f, light.intensity, 1e-2f, "intensity scales with the square of the character's size");
        }

        [Test]
        public void Prefab_EvilPower_Wired()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EvilPrefab);
            var p = prefab.GetComponentInChildren<EvilPower>(true);
            Assert.NotNull(p, "EvilPower on Evil.prefab");
            var so = new SerializedObject(p);
            var target = so.FindProperty("target").objectReferenceValue as Renderer;
            Assert.NotNull(target, "renderer");
            Assert.AreEqual(3, target.sharedMaterials.Length, "flame, body, power glow");
            Assert.AreEqual("GameJam/GlowSource", target.sharedMaterials[2].shader.name);
            var bones = so.FindProperty("flameBones");
            Assert.AreEqual(15, bones.arraySize, "flame.01-15 only (they are children of \"flames\"; scaling both would compound)");
            for (int i = 0; i < bones.arraySize; i++) Assert.NotNull(bones.GetArrayElementAtIndex(i).objectReferenceValue, $"flame bone {i}");
            Assert.NotNull(so.FindProperty("body").objectReferenceValue, "body");
            Assert.NotNull(so.FindProperty("powerLight").objectReferenceValue, "light");
            Assert.NotNull(so.FindProperty("powerFlames").objectReferenceValue, "flame particles");
            Assert.NotNull(so.FindProperty("embers").objectReferenceValue, "ember particles");
        }
    }
}
