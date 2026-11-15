using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameJamTests
{
    /// <summary>Who plays on what (controllers / keyboard halves / mice) and the camera that turns with them.</summary>
    public class InputTests
    {
        // ---------------------------------------------------------------- handing out controllers

        static int[] Owners(params int[] o) => o;

        [Test]
        public void TwoControllers_OneEach_InOrder()
        {
            var owner = Owners(-1, -1);
            Assert.IsTrue(InputAssignment.DistributePads(new List<int> { 5, 7 }, owner));
            CollectionAssert.AreEqual(new[] { 5, 7 }, owner, "controller 1 → player one, controller 2 → player two");
        }

        [Test]
        public void OneController_GoesToPlayerOne_KeyboardAndMouseToPlayerTwo()
        {
            var owner = Owners(-1, -1);
            InputAssignment.DistributePads(new List<int> { 5 }, owner);
            CollectionAssert.AreEqual(new[] { 5, -1 }, owner);
            var d = InputAssignment.Resolve(true, false, 1, false);
            Assert.AreEqual(KeySet.None, d[0].keys, "the controller player isn't also on the keyboard");
            Assert.AreEqual(-1, d[0].pointer);
            Assert.AreEqual(KeySet.Both, d[1].keys, "the other player has the whole keyboard");
            Assert.AreEqual(0, d[1].pointer, "…and the mouse");
        }

        [Test]
        public void SecondController_GoesToThePlayerWithout()
        {
            var owner = Owners(5, -1);
            InputAssignment.DistributePads(new List<int> { 5, 9 }, owner);
            CollectionAssert.AreEqual(new[] { 5, 9 }, owner);
        }

        [Test]
        public void Unplugging_KeepsTheOtherPlayersController()
        {
            var owner = Owners(5, 9);
            Assert.IsTrue(InputAssignment.DistributePads(new List<int> { 9 }, owner));
            CollectionAssert.AreEqual(new[] { -1, 9 }, owner, "player two keeps theirs, player one falls back to the keyboard");
            var d = InputAssignment.Resolve(false, true, 1, false);
            Assert.AreEqual(KeySet.Both, d[0].keys);
            Assert.AreEqual(0, d[0].pointer);
            Assert.AreEqual(KeySet.None, d[1].keys);
        }

        [Test]
        public void NothingChanged_ReportsNoChange()
        {
            var owner = Owners(5, 9);
            Assert.IsFalse(InputAssignment.DistributePads(new List<int> { 5, 9 }, owner));
        }

        [Test]
        public void TwoControllers_KeyboardHalvesStayAsBackup_NoMouse()
        {
            var d = InputAssignment.Resolve(true, true, 1, false);
            Assert.AreEqual(KeySet.Left, d[0].keys);
            Assert.AreEqual(KeySet.Right, d[1].keys);
            Assert.AreEqual(-1, d[0].pointer);
            Assert.AreEqual(-1, d[1].pointer);
        }

        // ---------------------------------------------------------------- both on the keyboard

        [Test]
        public void BothOnKeyboard_SplitTheKeyboard_AndOneMouseEach()
        {
            var d = InputAssignment.Resolve(false, false, 2, false);
            Assert.AreEqual(KeySet.Left, d[0].keys);
            Assert.AreEqual(KeySet.Right, d[1].keys);
            Assert.AreEqual(0, d[0].pointer);
            Assert.AreEqual(1, d[1].pointer);
        }

        [Test]
        public void BothOnKeyboard_SwapTradesTheMice()
        {
            var d = InputAssignment.Resolve(false, false, 2, true);
            Assert.AreEqual(1, d[0].pointer);
            Assert.AreEqual(0, d[1].pointer);
            Assert.AreEqual(KeySet.Left, d[0].keys, "keyboard halves don't move");
        }

        [Test]
        public void BothOnKeyboard_OnlyOneMouse_GoesToPlayerOne()
        {
            var d = InputAssignment.Resolve(false, false, 1, false);
            Assert.AreEqual(0, d[0].pointer);
            Assert.AreEqual(-1, d[1].pointer);
            d = InputAssignment.Resolve(false, false, 1, true);
            Assert.AreEqual(-1, d[0].pointer);
            Assert.AreEqual(0, d[1].pointer);
        }

        // ---------------------------------------------------------------- camera-relative movement

        [Test]
        public void Forward_FollowsTheCamerasHeading()
        {
            AssertNear(new Vector3(0f, 0f, 1f), PlayerInputSource.ToWorld(Vector2.up, 0f));
            AssertNear(new Vector3(1f, 0f, 0f), PlayerInputSource.ToWorld(Vector2.up, 90f), "camera turned right: forward is +X");
            AssertNear(new Vector3(0f, 0f, -1f), PlayerInputSource.ToWorld(Vector2.right, 90f), "…and right is -Z");
        }

        static void AssertNear(Vector3 want, Vector3 got, string msg = null) =>
            Assert.Less(Vector3.Distance(want, got), 1e-4f, $"{msg} want {want} got {got}");

        // ---------------------------------------------------------------- turning camera

        Scene scene;
        [SetUp] public void SetUp() => scene = EditorSceneManager.NewPreviewScene();
        [TearDown] public void TearDown() => EditorSceneManager.ClosePreviewScene(scene);

        GameObject New(string name)
        {
            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, scene);
            return go;
        }

        FollowCamera Cam(Vector3 targetPos)
        {
            var target = New("Target").transform;
            target.position = targetPos;
            var cam = New("Cam").AddComponent<FollowCamera>();
            cam.target = target;
            cam.Snap();
            return cam;
        }

        [Test]
        public void Look_OrbitsAroundTheCharacter_AtTheSameDistance()
        {
            var cam = Cam(new Vector3(10f, 0f, 20f));
            var t = cam.target.position;
            cam.Look(90f, 0f);
            for (int i = 0; i < 400; i++) cam.Tick(1f / 60f);
            var want = t + new Vector3(cam.offset.z, cam.offset.y, 0f);       // turned right: now behind it on -X
            Assert.Less(Vector3.Distance(cam.transform.position, want), 0.5f, $"want {want} got {cam.transform.position}");
            Assert.AreEqual(cam.offset.magnitude, Vector3.Distance(cam.transform.position, t), 0.5f);
            Assert.Greater(Vector3.Dot(cam.transform.forward, Vector3.right), 0.6f, "looking along +X now");
        }

        [Test]
        public void Look_TiltIsClamped()
        {
            var cam = Cam(Vector3.zero);
            cam.Look(0f, 500f);
            Assert.AreEqual(cam.maxPitch, cam.Pitch, 1e-3f);
            cam.Look(0f, -900f);
            Assert.AreEqual(cam.minPitch, cam.Pitch, 1e-3f);
        }

        [Test]
        public void Snap_FacesTheStartingWayAgain()
        {
            var cam = Cam(new Vector3(-30f, 0f, 10f));
            cam.Look(123f, 10f);
            cam.Snap();
            Assert.AreEqual(0f, Mathf.DeltaAngle(0f, cam.Yaw), 1e-3f);
            Assert.Less(Vector3.Distance(cam.transform.position, cam.target.position + cam.offset), 0.01f);
        }

        [Test]
        public void Wall_BehindTheCharacter_PullsTheCameraIn()
        {
            var target = New("Target").transform;
            target.position = Vector3.zero;
            var wall = New("Wall");
            wall.AddComponent<BoxCollider>().size = new Vector3(200f, 200f, 4f);
            wall.transform.position = new Vector3(0f, 0f, -30f);          // between the character and the camera
            Physics.SyncTransforms();
            var cam = New("Cam").AddComponent<FollowCamera>();
            cam.target = target;
            cam.Snap();
            Assert.Greater(cam.transform.position.z, -30f, $"camera stays in front of the wall (z = {cam.transform.position.z})");
        }
    }
}
