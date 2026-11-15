using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameJamTests
{
    public class GameplayTests
    {
        Scene scene;
        [SetUp] public void SetUp() => scene = EditorSceneManager.NewPreviewScene();
        [TearDown] public void TearDown() => EditorSceneManager.ClosePreviewScene(scene);

        GameObject New(string name)
        {
            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, scene);
            return go;
        }

        GameObject Character(string prefab)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(prefab), scene);
            go.AddComponent<CapsuleCollider>();
            return go;
        }

        // ---------------------------------------------------------------- lives

        [Test]
        public void Lives_StartAtThree_AndDropOnHit()
        {
            var lives = New("P").AddComponent<PlayerLives>();
            lives.ResetLives();
            Assert.AreEqual(3, lives.Lives);
            Assert.IsTrue(lives.LoseLife(10f));
            Assert.AreEqual(2, lives.Lives);
        }

        [Test]
        public void Lives_BrieflyInvulnerableAfterAHit()
        {
            var lives = New("P").AddComponent<PlayerLives>();
            lives.ResetLives();
            lives.LoseLife(10f);
            Assert.IsFalse(lives.LoseLife(10.5f), "a second slap right away doesn't count");
            Assert.IsTrue(lives.LoseLife(12f));
            Assert.AreEqual(1, lives.Lives);
        }

        [Test]
        public void Lives_Invulnerable_IgnoresHits()
        {
            var lives = New("P").AddComponent<PlayerLives>();
            lives.ResetLives();
            lives.Invulnerable = true;
            Assert.IsFalse(lives.LoseLife(10f), "the frozen Evil can't be slapped during the head start");
            Assert.AreEqual(3, lives.Lives);
            lives.Invulnerable = false;
            Assert.IsTrue(lives.LoseLife(20f));
        }

        [Test]
        public void Lives_StopAtZero()
        {
            var lives = New("P").AddComponent<PlayerLives>();
            lives.ResetLives();
            for (int i = 0; i < 6; i++) lives.LoseLife(10f + i * 5f);
            Assert.AreEqual(0, lives.Lives);
            Assert.IsFalse(lives.IsAlive);
        }

        // ---------------------------------------------------------------- match rules

        [Test]
        public void Rules_EvilWinsByDevouringEveryOffering()
        {
            var r = new MatchRules(3);
            r.Devoured(); r.Devoured();
            Assert.IsFalse(r.IsOver, "one is still hidden");
            Assert.AreEqual(1, r.Remaining);
            r.Devoured();
            Assert.AreEqual(MatchSide.Evil, r.Winner);
        }

        [Test]
        public void Rules_AngelWinsWhenAllHiddenAndSomeSurviveTheHunt()
        {
            var r = new MatchRules(8);
            for (int i = 0; i < 7; i++) r.Devoured();
            r.TimeUp(0);
            Assert.AreEqual(MatchSide.Angel, r.Winner);
            StringAssert.Contains("one offering survived", r.Reason);
            r.Devoured();
            Assert.AreEqual(MatchSide.Angel, r.Winner, "nothing changes once it's over");
            Assert.AreEqual(7, r.Eaten);
        }

        [Test]
        public void Rules_EvilWinsIfTheAngelDidntHideThemAll()
        {
            var r = new MatchRules(8);
            r.Devoured();
            r.TimeUp(2);                                  // two never left her pouch
            Assert.AreEqual(MatchSide.Evil, r.Winner);
            StringAssert.Contains("didn't hide 2", r.Reason);
        }

        [Test]
        public void Rules_OutOfLivesLoses()
        {
            var r = new MatchRules(8);
            r.LivesChanged(2, 1);
            Assert.IsFalse(r.IsOver);
            r.LivesChanged(2, 0);
            Assert.AreEqual(MatchSide.Angel, r.Winner);
            var r2 = new MatchRules(8);
            r2.LivesChanged(0, 3);
            Assert.AreEqual(MatchSide.Evil, r2.Winner);
        }

        // ---------------------------------------------------------------- offerings

        [Test]
        public void Offering_NeverExpires()
        {
            var o = New("Offering").AddComponent<Offering>();
            for (int i = 0; i < 6000; i++) o.Tick(0.1f);            // ten minutes on the ground
            Assert.IsTrue(o, "still there");
            Assert.IsFalse(o.Done);
        }

        [Test]
        public void Offering_EatenByTheEvil()
        {
            var o = New("Offering").AddComponent<Offering>();
            int eaten = 0; o.Eaten += _ => eaten++;
            var evil = Character("Assets/Prefabs/Characters/Evil/Evil.prefab");
            typeof(Offering).GetMethod("OnTriggerEnter", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(o, new object[] { evil.GetComponent<CapsuleCollider>() });
            Assert.AreEqual(1, eaten);
        }

        [Test]
        public void Offering_IgnoresTheAngel()
        {
            var o = New("Offering").AddComponent<Offering>();
            int eaten = 0; o.Eaten += _ => eaten++;
            var angel = Character("Assets/Prefabs/Characters/Angel/Angel.prefab");
            typeof(Offering).GetMethod("OnTriggerEnter", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(o, new object[] { angel.GetComponent<CapsuleCollider>() });
            Assert.AreEqual(0, eaten);
            Assert.IsFalse(o.Done);
        }

        // ---------------------------------------------------------------- dropping

        AngelDropper Dropper(int pouch, float cooldown)
        {
            var template = New("Template").AddComponent<Offering>();
            var d = New("Angel").AddComponent<AngelDropper>();
            d.offeringPrefab = template;
            d.container = New("Offerings").transform;
            d.pouchSize = pouch;
            d.cooldown = cooldown;
            d.Refill();
            return d;
        }

        [Test]
        public void Drop_RespectsCooldown()
        {
            var d = Dropper(10, 1f);
            Assert.NotNull(d.TryDrop(5f));
            Assert.IsNull(d.TryDrop(5.5f), "still cooling down");
            Assert.NotNull(d.TryDrop(6.1f));
            Assert.AreEqual(2, d.PlacedCount);
            Assert.AreEqual(8, d.Carried);
        }

        [Test]
        public void Drop_StopsWhenThePouchIsEmpty()
        {
            var d = Dropper(2, 0f);
            d.TryDrop(1f); d.TryDrop(2f);
            Assert.AreEqual(0, d.Carried);
            Assert.IsNull(d.TryDrop(3f), "she only has two");
            d.Refill();
            Assert.AreEqual(2, d.Carried, "a new round fills the pouch");
        }


        [Test]
        public void Drop_PutsTheOfferingOnTheGroundNearTheAngel()
        {
            var d = Dropper(10, 0f);
            d.transform.position = new Vector3(40f, 0f, 20f);
            var o = d.TryDrop(1f);
            Assert.AreEqual(0f, o.transform.position.y, 0.01f);
            Assert.Less(Vector3.Distance(o.transform.position, d.transform.position), 20f);
        }

        // ---------------------------------------------------------------- follow camera

        [Test]
        public void Camera_SettlesBehindAndAboveTheTarget()
        {
            var target = New("Target").transform;
            target.position = new Vector3(100f, 0f, 50f);
            var cam = New("Cam").AddComponent<FollowCamera>();
            cam.target = target;
            cam.transform.position = Vector3.zero;
            for (int i = 0; i < 400; i++) cam.Tick(1f / 60f);
            Assert.Less(Vector3.Distance(cam.transform.position, target.position + cam.offset), 0.5f, "settles at the offset");
            var toTarget = (target.position + Vector3.up * cam.lookHeight - cam.transform.position).normalized;
            Assert.Greater(Vector3.Dot(cam.transform.forward, toTarget), 0.999f, "looks at the character");
        }

        [Test]
        public void Camera_SnapJumpsStraightThere()
        {
            var target = New("Target").transform;
            target.position = new Vector3(-30f, 0f, 10f);
            var cam = New("Cam").AddComponent<FollowCamera>();
            cam.target = target;
            cam.Snap();
            Assert.Less(Vector3.Distance(cam.transform.position, target.position + cam.offset), 0.01f);
        }
    }
}
