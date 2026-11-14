using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GameJamTests
{
    public class HookupTests
    {
        Scene scene;

        [SetUp] public void SetUp() => scene = EditorSceneManager.NewPreviewScene();
        [TearDown] public void TearDown() => EditorSceneManager.ClosePreviewScene(scene);

        T Make<T>(string name) where T : Component
        {
            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, scene);
            return go.AddComponent<T>();
        }

        [Test]
        public void Collectible_Pickup_CallsPowerUp()
        {
            var evil = (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Characters/Evil/Evil.prefab"), scene);
            evil.name = "Evil";
            evil.tag = "Player";
            var collider = evil.AddComponent<CapsuleCollider>();

            var score = Make<ScoreManager>("ScoreManager");
            var collectible = Make<CollectibleManager>("Collectible");
            collectible._scoreManager = score;

            // the pickup destroys itself with Destroy(), which only logs an error in edit mode
            LogAssert.Expect(LogType.Error, new Regex("Destroy may not be called from edit mode"));
            typeof(CollectibleManager).GetMethod("OnTriggerEnter", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(collectible, new object[] { collider });

            Assert.AreEqual(1, score.EvilScore, "score still counted");
            Assert.IsTrue(evil.GetComponent<EvilPower>().IsPowered, "collecting powers the Evil up");
        }
    }
}
