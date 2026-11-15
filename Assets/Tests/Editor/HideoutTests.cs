using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameJamTests
{
    /// <summary>Caves to hide offerings in: what counts as inside, and the camera looking in over the cut-away roof.</summary>
    public class HideoutTests
    {
        Scene scene;
        [SetUp] public void SetUp() => scene = EditorSceneManager.NewPreviewScene();

        [TearDown]
        public void TearDown()
        {
            foreach (var h in Hideout.All.ToArray())                    // edit mode: unregister by hand
                if (h) typeof(Hideout).GetMethod("OnDisable", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(h, null);
            EditorSceneManager.ClosePreviewScene(scene);
        }

        GameObject New(string name)
        {
            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, scene);
            return go;
        }

        Hideout Cave(Vector3 pos, float yaw)
        {
            var go = New("Cave");
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            var h = go.AddComponent<Hideout>();
            h.center = new Vector3(0f, 22f, 0f);
            h.size = new Vector3(60f, 44f, 40f);
            typeof(Hideout).GetMethod("OnEnable", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(h, null);
            return h;
        }

        [Test]
        public void Contains_UsesTheCavesOwnFrame()
        {
            var h = Cave(new Vector3(100f, 0f, 0f), 90f);              // turned: its 60-wide side now runs along z
            Assert.IsTrue(h.Contains(new Vector3(100f, 5f, 25f)));
            Assert.IsFalse(h.Contains(new Vector3(125f, 5f, 0f)), "only 40 deep along x once turned");
            Assert.IsTrue(h.Contains(new Vector3(125f, 5f, 0f), 6f), "…unless we allow some slack");
            Assert.AreSame(h, Hideout.At(new Vector3(100f, 5f, 0f)));
            Assert.IsNull(Hideout.At(new Vector3(0f, 5f, 0f)));
        }

        [Test]
        public void Camera_InsideACave_TiltsDownOverIt()
        {
            Cave(Vector3.zero, 0f);
            var target = New("Player").transform;
            target.position = Vector3.zero;
            var cam = New("Cam").AddComponent<FollowCamera>();
            cam.target = target;
            cam.Snap();
            Assert.NotNull(cam.Hideout, "knows the player is inside");
            var toCam = cam.transform.position - target.position;
            float tilt = Mathf.Asin(toCam.normalized.y) * Mathf.Rad2Deg;
            Assert.Greater(tilt, cam.indoorPitch - 2f, $"looks down into the cave (tilt {tilt:F0}°)");

            target.position = new Vector3(0f, 0f, -200f);                // walked out
            for (int i = 0; i < 120; i++) cam.Tick(1f / 60f);
            Assert.IsNull(cam.Hideout);
        }

        [Test]
        public void Offering_DoesNotGlowThroughWalls()
        {
            // GlowSource is drawn with ZTest Always: on an offering it would give away a hiding spot through the rock
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GameSetupBuilder.OfferingPath);
            foreach (var r in prefab.GetComponentsInChildren<Renderer>(true))
                foreach (var m in r.sharedMaterials)
                    Assert.IsFalse(m && m.shader.name == "GameJam/GlowSource", $"{r.name} uses the see-through glow");
        }

        [Test]
        public void Courtyard_HasCavesWithCutawayRoofs()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CourtyardBuilder.PrefabPath);
            var caves = prefab.GetComponentsInChildren<Hideout>(true);
            Assert.GreaterOrEqual(caves.Length, 4, "a few caves to hide offerings in");
            int roofLayer = LayerMask.NameToLayer(CourtyardBuilder.CaveRoofLayer);
            Assert.GreaterOrEqual(roofLayer, 0, "CaveRoof layer exists");
            foreach (var c in caves)
            {
                Assert.IsNotEmpty(c.roof, $"{c.name} has a roof to cut away");
                Assert.IsTrue(c.roof.All(r => r), $"{c.name} roof renderers are set");
                Assert.IsTrue(c.GetComponentsInChildren<BoxCollider>(true).Any(b => b.gameObject.layer == roofLayer), $"{c.name} roof blocks cameras only");
            }
        }
    }
}
