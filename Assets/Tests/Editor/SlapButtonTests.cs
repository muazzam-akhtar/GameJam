using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace GameJamTests
{
    /// <summary>Slap is a button now (P1 Left Shift, P2 Right Shift), not automatic.</summary>
    public class SlapButtonTests
    {
        const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        Scene scene;

        [SetUp] public void SetUp() => scene = EditorSceneManager.NewPreviewScene();
        [TearDown] public void TearDown() => EditorSceneManager.ClosePreviewScene(scene);

        (MonoBehaviour player, FakeCharacterActions actions) Make(Type controller)
        {
            var go = new GameObject(controller.Name);
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<Rigidbody>();
            var player = (MonoBehaviour)go.AddComponent(controller);
            var actions = go.AddComponent<FakeCharacterActions>();
            controller.GetMethod("Awake", Any).Invoke(player, null);     // edit mode doesn't run Awake
            return (player, actions);
        }

        static void Call(MonoBehaviour m, string method) => m.GetType().GetMethod(method, Any).Invoke(m, null);

        [TestCase(typeof(P1Controller), Key.LeftShift)]
        [TestCase(typeof(P2Controller), Key.RightShift)]
        public void SlapKey_IsShift(Type controller, Key expected)
        {
            var field = controller.GetField("slapKey", Any);
            Assert.NotNull(field, $"{controller.Name} has a slapKey");
            Assert.AreEqual(expected, field.GetValue(Make(controller).player));
        }

        [TestCase(typeof(P1Controller))]
        [TestCase(typeof(P2Controller))]
        public void Slap_Swings_EvenWhenItMisses(Type controller)
        {
            var (player, actions) = Make(controller);
            Call(player, "SlapEvent");            // nothing in front: the ray misses
            Assert.AreEqual(1, actions.slaps, "pressing slap always swings");
            Assert.AreEqual(0, actions.stungs);
        }

        [TestCase(typeof(P1Controller))]
        [TestCase(typeof(P2Controller))]
        public void Slap_DuringCooldown_DoesNothing(Type controller)
        {
            var (player, actions) = Make(controller);
            controller.GetField("canSlap", Any).SetValue(player, false);
            Call(player, "SlapEvent");
            Assert.AreEqual(0, actions.slaps);
        }

        [TestCase(typeof(P1Controller))]
        [TestCase(typeof(P2Controller))]
        public void NoButton_NoSlap(Type controller)
        {
            var (player, actions) = Make(controller);
            for (int i = 0; i < 10; i++) Call(player, "Update");
            Assert.AreEqual(0, actions.slaps, "no automatic slapping");
        }
    }
}
