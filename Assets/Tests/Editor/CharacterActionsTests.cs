using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameJamTests
{
    public class CharacterActionsTests
    {
        const string AngelPrefab = "Assets/Prefabs/Characters/Angel/Angel.prefab";
        const string EvilPrefab = "Assets/Prefabs/Characters/Evil/Evil.prefab";
        Scene scene;

        [SetUp] public void SetUp() => scene = EditorSceneManager.NewPreviewScene();
        [TearDown] public void TearDown() => EditorSceneManager.ClosePreviewScene(scene);

        GameObject Spawn(string prefab) =>
            (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(prefab), scene);

        [TestCase(AngelPrefab)]
        [TestCase(EvilPrefab)]
        public void BothPrefabs_ImplementICharacterActions(string prefab)
        {
            Assert.NotNull(AssetDatabase.LoadAssetAtPath<GameObject>(prefab).GetComponent<ICharacterActions>(),
                $"{prefab} root has an ICharacterActions component");
        }

        [TestCase(AngelPrefab)]
        [TestCase(EvilPrefab)]
        public void Stung_MakesDizzy(string prefab)
        {
            var actions = Spawn(prefab).GetComponent<ICharacterActions>();
            Assert.NotNull(actions);
            Assert.IsFalse(actions.IsDizzy, "not dizzy before the hit");
            actions.Stung();
            Assert.IsTrue(actions.IsDizzy, "dizzy after the hit");
        }

        [Test]
        public void Evil_PowerUp_ActivatesEvilPower()
        {
            var go = Spawn(EvilPrefab);
            var actions = go.GetComponent<ICharacterActions>();
            Assert.NotNull(actions);
            actions.PowerUp();
            Assert.IsTrue(go.GetComponent<EvilPower>().IsPowered);
        }
    }
}
