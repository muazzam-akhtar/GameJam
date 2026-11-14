using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace GameJamTests
{
    /// <summary>The split-courtyard map: valid materials, a closed border, open ground where pickups spawn.</summary>
    public class CourtyardTests
    {
        static GameObject Prefab()
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(CourtyardBuilder.PrefabPath);
            Assert.NotNull(go, "Courtyard prefab built");
            return go;
        }

        /// <summary>Every solid BoxCollider in the prefab (not the ground), tested as an oriented box.</summary>
        static List<BoxCollider> Solids(GameObject root) =>
            root.GetComponentsInChildren<BoxCollider>(true).Where(b => b.name != CourtyardBuilder.GroundColliderName).ToList();

        static bool Contains(BoxCollider box, Vector3 world)
        {
            var local = box.transform.InverseTransformPoint(world) - box.center;
            var e = box.size * 0.5f;
            return Mathf.Abs(local.x) <= e.x && Mathf.Abs(local.y) <= e.y && Mathf.Abs(local.z) <= e.z;
        }

        static bool Hits(BoxCollider box, Ray ray, float max)
        {
            var t = box.transform;
            var local = new Ray(t.InverseTransformPoint(ray.origin), t.InverseTransformDirection(ray.direction));
            return new Bounds(box.center, box.size).IntersectRay(local, out float d) && d <= max;
        }

        [Test]
        public void Prefab_HasNoBrokenMaterials()
        {
            var renderers = Prefab().GetComponentsInChildren<Renderer>(true);
            Assert.IsNotEmpty(renderers);
            foreach (var r in renderers)
                foreach (var m in r.sharedMaterials)
                {
                    Assert.NotNull(m, $"{r.name} has a missing material");
                    Assert.IsTrue(m.shader.isSupported, $"{r.name}: shader {m.shader.name} not supported");
                    Assert.AreNotEqual("Hidden/InternalErrorShader", m.shader.name, $"{r.name}: error shader");
                }
        }

        [Test]
        public void Renderers_HaveASubmeshPerMaterial()
        {
            // extra materials (like the GlowSource pass on the cracks) need their own submesh, or Unity warns every play
            foreach (var mr in Prefab().GetComponentsInChildren<MeshRenderer>(true))
            {
                var mesh = mr.GetComponent<MeshFilter>().sharedMesh;
                Assert.LessOrEqual(mr.sharedMaterials.Length, mesh.subMeshCount, $"{mr.name}: {mr.sharedMaterials.Length} materials, {mesh.subMeshCount} submeshes");
            }
        }

        [Test]
        public void Arena_IsEnclosed()
        {
            var solids = Solids(Prefab());
            var c = CourtyardBuilder.ArenaCenter + Vector3.up * 12f;
            for (int i = 0; i < 72; i++)
            {
                float a = i * Mathf.PI * 2f / 72f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Assert.IsTrue(solids.Any(b => Hits(b, new Ray(c, dir), 220f)), $"players could leave the arena heading {dir}");
            }
        }

        [Test]
        public void SpawnArea_IsOpenGround()
        {
            var solids = Solids(Prefab());
            var area = CourtyardBuilder.SpawnArea;
            for (float x = area.min.x; x <= area.max.x + 0.01f; x += 4f)
                for (float z = area.min.z; z <= area.max.z + 0.01f; z += 4f)
                {
                    var p = new Vector3(x, 12f, z);
                    var inside = solids.FirstOrDefault(b => Contains(b, p));
                    Assert.IsNull(inside, $"spawn point {p} is inside {inside?.name}");
                }
        }

        [TestCase(0f, 47.1f)]
        [TestCase(124.5f, 47.1f)]
        public void PlayerStart_IsFree(float x, float z)
        {
            var solids = Solids(Prefab());
            // the character's footprint (~24 wide) at body height
            for (float dx = -12f; dx <= 12f; dx += 4f)
                for (float dz = -12f; dz <= 12f; dz += 4f)
                    foreach (var y in new[] { 8f, 18f, 28f })
                    {
                        var p = new Vector3(x + dx, y, z + dz);
                        var inside = solids.FirstOrDefault(b => Contains(b, p));
                        Assert.IsNull(inside, $"start at ({x},{z}) overlaps {inside?.name}");
                    }
        }
    }
}
