using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameJamTests
{
    public class DizzyStarsTests
    {
        const string AngelPrefab = "Assets/Prefabs/Characters/Angel/Angel.prefab";
        const string EvilPrefab = "Assets/Prefabs/Characters/Evil/Evil.prefab";

        Scene scene;

        [SetUp] public void SetUp() => scene = EditorSceneManager.NewPreviewScene();
        [TearDown] public void TearDown() => EditorSceneManager.ClosePreviewScene(scene);

        DizzyStars Spawn(string prefab)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(prefab), scene);
            var stars = go.GetComponentInChildren<DizzyStars>(true);
            Assert.NotNull(stars, $"{prefab} has DizzyStars");
            return stars;
        }

        static int ActiveStars(DizzyStars d) =>
            d.GetComponentsInChildren<MeshRenderer>(true).Count(r => r.gameObject.activeInHierarchy && r.transform.localScale.sqrMagnitude > 1e-6f);

        [TestCase(AngelPrefab)]
        [TestCase(EvilPrefab)]
        public void Show_ActivatesThreeStars(string prefab)
        {
            var d = Spawn(prefab);
            Assert.AreEqual(0, ActiveStars(d), "hidden before Show");
            d.Show();
            d.Tick(0.1f);
            Assert.IsTrue(d.Visible);
            Assert.AreEqual(3, ActiveStars(d));
        }

        [TestCase(AngelPrefab)]
        [TestCase(EvilPrefab)]
        public void Tick_PastDuration_HidesStars(string prefab)
        {
            var d = Spawn(prefab);
            d.Show();
            for (int i = 0; i < 30; i++) d.Tick(0.1f);   // 3.0 s > 2.5 + 0.2
            Assert.IsFalse(d.Visible);
            Assert.AreEqual(0, ActiveStars(d));
        }

        [Test]
        public void Show_WhileVisible_ResetsTimer_NoExtraStars()
        {
            var d = Spawn(AngelPrefab);
            int children = d.transform.childCount;
            d.Show();
            for (int i = 0; i < 20; i++) d.Tick(0.1f);   // 2.0 s
            d.Show();
            for (int i = 0; i < 20; i++) d.Tick(0.1f);   // 2.0 s after the second hit (4.0 s total)
            Assert.IsTrue(d.Visible, "second hit restarts the 2.5 s timer");
            Assert.AreEqual(3, ActiveStars(d));
            Assert.AreEqual(children, d.transform.childCount, "no extra stars created");
        }

        [TestCase(AngelPrefab)]
        [TestCase(EvilPrefab)]
        public void Prefab_Ring_LiftsTheFrontStar(string prefab)
        {
            // a positive tilt dips the front star across the face; the prefab must carry the tuned value
            var d = AssetDatabase.LoadAssetAtPath<GameObject>(prefab).GetComponentInChildren<DizzyStars>(true);
            Assert.NotNull(d);
            Assert.AreEqual(-10f, new SerializedObject(d).FindProperty("ringTilt").floatValue, 1e-3f);
        }

        [Test]
        public void Prefabs_HaveStars_InTheirColor()
        {
            Color StarColor(string prefab)
            {
                var d = AssetDatabase.LoadAssetAtPath<GameObject>(prefab).GetComponentInChildren<DizzyStars>(true);
                Assert.NotNull(d, $"{prefab} has DizzyStars");
                var renderers = d.GetComponentsInChildren<MeshRenderer>(true);
                Assert.AreEqual(3, renderers.Length, "three stars");
                return renderers[0].sharedMaterial.GetColor("_BaseColor");
            }
            var gold = StarColor(AngelPrefab);
            Assert.That(gold.r > gold.g && gold.g > gold.b && gold.b < 0.6f * gold.r, $"Angel stars gold, got {gold}");
            var purple = StarColor(EvilPrefab);
            Assert.That(purple.b > purple.r && purple.r > purple.g, $"Evil stars purple, got {purple}");
        }
    }
}
