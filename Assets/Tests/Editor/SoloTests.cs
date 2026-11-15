using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameJamTests
{
    /// <summary>Single player: the Keeper collects souls (guards don't eat them); spawns stay inside the walls.</summary>
    public class SoloTests
    {
        Scene scene;
        [SetUp] public void SetUp() => scene = EditorSceneManager.NewPreviewScene();
        [TearDown] public void TearDown() => EditorSceneManager.ClosePreviewScene(scene);

        [Test]
        public void Soul_KeeperCollects_GuardsLeaveIt()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<Offering>("Assets/Prefabs/Gameplay/Offering.prefab");
            var soul = (Offering)PrefabUtility.InstantiatePrefab(prefab, scene);
            soul.keeperCollects = true;
            int found = 0;
            soul.Collected += _ => found++;
            var evil = new GameObject("Evil", typeof(BoxCollider));
            SceneManager.MoveGameObjectToScene(evil, scene);
            evil.AddComponent<EvilController>();
            typeof(Offering).GetMethod("OnTriggerEnter", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(soul, new object[] { evil.GetComponent<Collider>() });
            Assert.IsFalse(soul.Done, "a guard Reaper doesn't take it");
            soul.Collect(null);
            Assert.AreEqual(1, found);
        }

        [Test]
        public void Inside_IsTheCourtyard()
        {
            Assert.IsTrue(SoloGame.Inside(new Vector3(56f, 0f, 78f)), "the centre");
            Assert.IsTrue(SoloGame.Inside(new Vector3(-208f, 0f, -90f)), "the Keeper's start");
            Assert.IsFalse(SoloGame.Inside(new Vector3(600f, 0f, 78f)), "outside the wall");
            Assert.IsFalse(SoloGame.Inside(new Vector3(-300f, 0f, 390f)), "the cut corner");
        }
    }
}
