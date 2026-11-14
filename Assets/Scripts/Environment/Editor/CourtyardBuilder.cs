using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Random = System.Random;

/// <summary>
/// Builds the split-courtyard map (concept art: "Broken Aqueduct" + "Fractured Sanctuary", layout C):
/// left half = aqueduct ruin with an arch the stream flows out of, right half = paved sanctuary courtyard with a
/// raised terrace, stairs, glowing cracks and a freestanding ruined arch. Everything is generated (stone blocks,
/// ground, water, banners) into <see cref="PrefabPath"/>; <see cref="InstallIntoOpenScene"/> swaps it into DemoScene.
/// Menu: GameJam/Environment/Build Courtyard. Safe to run again.
/// World units: the characters are ~32 tall (prefabs scaled x10); the camera looks down from (54,100,-34).
/// </summary>
public static class CourtyardBuilder
{
    public const string PrefabPath = "Assets/Environment/Courtyard/Courtyard.prefab";
    public const string GroundColliderName = "GroundCollider";
    const string Root = "Assets/Environment/Courtyard";
    const string MeshDir = Root + "/Meshes";
    const string MatDir = Root + "/Materials";

    /// <summary>Middle of the walkable arena (y = ground).</summary>
    public static readonly Vector3 ArenaCenter = new Vector3(56f, 0f, 70f);

    /// <summary>Where pickups may spawn: open ground only (SpawnRegion's box in DemoScene).</summary>
    public static readonly Bounds SpawnArea = new Bounds(new Vector3(55f, 4.7f, 30f), new Vector3(146f, 0f, 52f));

    // ------------------------------------------------------------------ layout

    /// <summary>Inner edge of the border wall, counter-clockwise (x, z).</summary>
    static readonly Vector2[] Outline =
    {
        new Vector2(-14, -8), new Vector2(126, -8), new Vector2(146, 12), new Vector2(146, 132),
        new Vector2(126, 152), new Vector2(-14, 152), new Vector2(-34, 132), new Vector2(-34, 12),
    };

    static readonly Rect AqueductRect = Rect.MinMaxRect(-44, 118, 62, 166);
    const float AqueductTop = 26f;
    const float AqueductFace = 118f;
    const float ArchX = 0f, ArchInner = 12f, ArchOuter = 18f, ArchSpring = 8f;

    static readonly Rect TerraceRect = Rect.MinMaxRect(96, 108, 156, 166);
    const float TerraceTop = 22f;
    static readonly Rect StairsRect = Rect.MinMaxRect(74, 112, 96, 146);

    static readonly Vector2 RuinArch = new Vector2(112, 70);
    const float RuinGap = 32f, RuinPillar = 11f, RuinPillarHeight = 24f;

    static readonly Vector2[] CoverClusters = { new Vector2(-20, 92), new Vector2(58, 96), new Vector2(140, 20) };

    /// <summary>Stream centre line (x, z), from inside the aqueduct tunnel to the culvert in the front wall.</summary>
    static readonly Vector2[] StreamLine =
    {
        new Vector2(0, 136), new Vector2(0, 120), new Vector2(3, 104), new Vector2(14, 84), new Vector2(22, 64),
        new Vector2(16, 44), new Vector2(4, 26), new Vector2(-6, 8), new Vector2(-10, -4), new Vector2(-10, -14), new Vector2(-10, -20),
    };
    const float WaterLevel = -1.0f;

    // ------------------------------------------------------------------ palette (sRGB, converted to linear)

    static readonly Color[] StonePalette =
    {
        new Color(0.84f, 0.61f, 0.52f), new Color(0.79f, 0.56f, 0.50f), new Color(0.88f, 0.66f, 0.55f),
        new Color(0.75f, 0.53f, 0.52f), new Color(0.82f, 0.59f, 0.56f), new Color(0.86f, 0.64f, 0.52f), new Color(0.72f, 0.54f, 0.50f),
    };
    static readonly Color[] PavingPalette = { new Color(0.84f, 0.65f, 0.56f), new Color(0.79f, 0.60f, 0.54f), new Color(0.88f, 0.69f, 0.59f), new Color(0.77f, 0.61f, 0.57f) };
    static readonly Color[] MossPalette = { new Color(0.46f, 0.64f, 0.19f), new Color(0.40f, 0.58f, 0.17f), new Color(0.53f, 0.68f, 0.24f) };
    static readonly Color Grass = new Color(0.61f, 0.71f, 0.27f);
    static readonly Color GrassDark = new Color(0.50f, 0.63f, 0.22f);
    static readonly Color GrassLight = new Color(0.69f, 0.74f, 0.31f);
    static readonly Color ForestFloor = new Color(0.40f, 0.50f, 0.19f);
    static readonly Color Dirt = new Color(0.55f, 0.43f, 0.31f);

    static Color Lin(Color c) => c.linear;
    static Color Pick(Color[] palette, Random rng) => Lin(palette[rng.Next(palette.Length)]);

    // ------------------------------------------------------------------ entry points

    [MenuItem("GameJam/Environment/Build Courtyard")]
    public static void BuildAndInstall()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("CourtyardBuilder: leave Play mode first."); return; }
        Build();
        InstallIntoOpenScene();
    }

    /// <summary>Generates every mesh/material and saves the courtyard prefab. Doesn't touch the open scene.</summary>
    public static GameObject Build()
    {
        EnsureFolder(MeshDir);
        EnsureFolder(MatDir);
        var mats = Materials();
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var root = new GameObject("Courtyard");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
            var rng = new Random(20261115);

            var stone = new MeshBuilder();      // walls, aqueduct, terrace, stairs, arch, cover
            var paving = new MeshBuilder();
            var moss = new MeshBuilder();
            var cracks = new MeshBuilder();
            var cloth = new MeshBuilder();
            var trim = new MeshBuilder();       // banner rods, brackets, caps
            var colliders = new GameObject("Colliders").transform; colliders.SetParent(root.transform, false);

            BuildBorder(stone, moss, colliders, rng);
            BuildAqueduct(stone, paving, moss, cracks, cloth, trim, colliders, rng);
            BuildTerrace(stone, paving, moss, cracks, cloth, trim, colliders, rng);
            BuildStairs(stone, colliders, rng);
            BuildRuinArch(stone, moss, cracks, colliders, rng);
            BuildCover(stone, moss, colliders, rng);
            BuildPaving(paving, cracks, rng);
            var streamPath = StreamPath();
            BuildStreamStones(stone, paving, moss, streamPath, rng);

            AddPart(root, "Stone", stone.Build("Courtyard_Stone"), mats["Stone"]);
            AddPart(root, "Paving", paving.Build("Courtyard_Paving"), mats["Paving"]);
            AddPart(root, "Moss", moss.Build("Courtyard_Moss"), mats["Moss"]);
            AddPart(root, "Cracks", cracks.Build("Courtyard_Cracks"), mats["Crack"], mats["CrackGlow"], castShadows: false);
            AddPart(root, "Banners", cloth.Build("Courtyard_Banners"), mats["Cloth"]);
            AddPart(root, "BannerTrim", trim.Build("Courtyard_BannerTrim"), mats["Trim"]);
            AddPart(root, "Ground", BuildGround(streamPath), mats["Ground"], castShadows: false);
            AddPart(root, "Water", BuildWater(streamPath), mats["Water"], castShadows: false);

            var ground = new GameObject(GroundColliderName);
            ground.transform.SetParent(colliders, false);
            var gb = ground.AddComponent<BoxCollider>();
            gb.center = new Vector3(56f, -5f, 80f);
            gb.size = new Vector3(460f, 10f, 460f);

            BuildVegetation(root.transform, rng);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            return prefab;
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    // ------------------------------------------------------------------ border wall

    static void BuildBorder(MeshBuilder stone, MeshBuilder moss, Transform colliders, Random rng)
    {
        const float thickness = 9f;
        for (int s = 0; s < Outline.Length; s++)
        {
            var a = Outline[s];
            var b = Outline[(s + 1) % Outline.Length];
            var d2 = (b - a).normalized;
            var len = (b - a).magnitude;
            var dir = new Vector3(d2.x, 0, d2.y);
            var outward = new Vector3(d2.y, 0, -d2.x);                 // outline is counter-clockwise
            if (Vector3.Dot(outward, new Vector3((a.x + b.x) * 0.5f - ArenaCenter.x, 0, (a.y + b.y) * 0.5f - ArenaCenter.z)) < 0) outward = -outward;
            var rot = Quaternion.LookRotation(outward, Vector3.up);
            var start = new Vector3(a.x, 0, a.y);

            // collider: one tall slab per side
            var col = new GameObject($"Wall_{s}").transform;
            col.SetParent(colliders, false);
            col.position = start + dir * (len * 0.5f) + outward * (thickness * 0.5f) + Vector3.up * 30f;
            col.rotation = rot;
            col.gameObject.AddComponent<BoxCollider>().size = new Vector3(len + thickness * 1.6f, 60f, thickness + 2f);

            float t = -thickness * 0.5f;                                // start a little past the corner so corners close
            while (t < len + thickness * 0.3f)
            {
                float w = Mathf.Lerp(9.5f, 14f, (float)rng.NextDouble());
                float mid = t + w * 0.5f;
                var c = start + dir * mid + outward * (thickness * 0.5f);
                t += w + 0.5f;
                if (InsideStructure(new Vector2(c.x, c.z), 6f)) continue;
                bool culvert = s == 0 && Mathf.Abs(c.x - (-10f)) < 9f;   // front wall over the stream
                bool front = s == 0 || s == 1 || s == Outline.Length - 1;   // the camera looks over these
                float hgt = front ? Mathf.Lerp(6.5f, 8.5f, (float)rng.NextDouble()) : Mathf.Lerp(9f, 11.5f, (float)rng.NextDouble());
                if (rng.NextDouble() < 0.16) hgt += front ? 2f : 3.5f;    // the odd taller block
                var color = Pick(StonePalette, rng);
                var yaw = Quaternion.Euler(0, (float)(rng.NextDouble() * 4 - 2), 0);
                if (culvert)
                {
                    // two low jambs and a lintel: the stream runs out underneath
                    Block(stone, c + Vector3.up * 7f, new Vector3(w + 2f, 3f, thickness), rot * yaw, color, rng);
                    continue;
                }
                Block(stone, c + Vector3.up * (hgt * 0.5f), new Vector3(w, hgt, thickness), rot * yaw, color, rng);
                if (rng.NextDouble() < 0.18)                               // a cap stone on top
                    Block(stone, c + Vector3.up * (hgt + 2f) + dir * (float)(rng.NextDouble() * 2 - 1), new Vector3(w * 0.6f, 4f, thickness * 0.8f), rot * Quaternion.Euler(0, (float)(rng.NextDouble() * 10 - 5), 0), Pick(StonePalette, rng), rng);
                else if (rng.NextDouble() < 0.22)
                    Moss(moss, c + Vector3.up * hgt, w, thickness, rng);
            }
        }
        // culvert jambs either side of the stream
        foreach (var x in new[] { -18.5f, -1.5f })
            Block(stone, new Vector3(x, 3f, -12.5f), new Vector3(5f, 6f, 9f), Quaternion.identity, Pick(StonePalette, rng), rng);
    }

    // ------------------------------------------------------------------ aqueduct (left back)

    static void BuildAqueduct(MeshBuilder stone, MeshBuilder paving, MeshBuilder moss, MeshBuilder cracks, MeshBuilder cloth, MeshBuilder trim, Transform colliders, Random rng)
    {
        var r = AqueductRect;
        AddBoxCollider(colliders, "Aqueduct", new Vector3(r.center.x, 30f, r.center.y), new Vector3(r.width, 60f, r.height));

        // front face: three courses; the broken right end steps down
        float[] courseTop = { 8f, 17f, AqueductTop };
        float[] courseEnd = { 62f, 57f, 50f };
        for (int k = 0; k < 3; k++)
        {
            float y0 = k == 0 ? 0f : courseTop[k - 1], y1 = courseTop[k];
            float ym = (y0 + y1) * 0.5f;
            float archHalf = ym < ArchSpring ? ArchOuter : (ym - ArchSpring < ArchOuter ? Mathf.Sqrt(ArchOuter * ArchOuter - (ym - ArchSpring) * (ym - ArchSpring)) : 0f);
            LayCourse(stone, r.xMin, ArchX - archHalf, y0, y1, AqueductFace, rng);
            LayCourse(stone, ArchX + archHalf, courseEnd[k], y0, y1, AqueductFace, rng);
            if (k == 0)
                foreach (var side in new[] { -1f, 1f })                    // arch jambs
                    Block(stone, new Vector3(ArchX + side * (ArchInner + ArchOuter) * 0.5f, ArchSpring * 0.5f, AqueductFace + 5.5f),
                        new Vector3(ArchOuter - ArchInner, ArchSpring, 11f), Quaternion.identity, Pick(StonePalette, rng), rng);
        }
        // voussoirs around the arch
        Voussoirs(stone, new Vector3(ArchX, ArchSpring, AqueductFace + 5.5f), ArchInner, ArchOuter, 11f, 9, Enumerable.Range(0, 9), rng);

        // tunnel: dark side walls, ceiling and back
        var dark = Lin(new Color(0.36f, 0.26f, 0.30f));
        foreach (var side in new[] { -1f, 1f })
            Block(stone, new Vector3(ArchX + side * (ArchInner + 2f), 12f, AqueductFace + 18f), new Vector3(4f, 24f, 18f), Quaternion.identity, dark, rng, 0.1f);
        Block(stone, new Vector3(ArchX, 27f, AqueductFace + 18f), new Vector3(ArchInner * 2f + 4f, 6f, 18f), Quaternion.identity, dark * 0.8f, rng, 0.1f);
        Block(stone, new Vector3(ArchX, 12f, AqueductFace + 27f), new Vector3(ArchInner * 2f + 4f, 24f, 4f), Quaternion.identity, dark * 0.6f, rng, 0.1f);

        // body behind the face (only its top edge shows) and the deck slabs
        Block(stone, new Vector3((r.xMin + 50f) * 0.5f, AqueductTop * 0.5f - 0.5f, (AqueductFace + 11f + r.yMax) * 0.5f), new Vector3(50f - r.xMin, AqueductTop - 1f, r.yMax - AqueductFace - 11f), Quaternion.identity, Pick(StonePalette, rng), rng, 0.05f);
        for (float x = r.xMin + 6f; x < 50f; x += 12.5f)
            for (float z = AqueductFace + 6f; z < r.yMax; z += 12.5f)
            {
                if (rng.NextDouble() < 0.08) continue;                   // missing slab
                var c = new Vector3(x + (float)rng.NextDouble(), AqueductTop + 0.6f + (float)rng.NextDouble() * 0.5f, z + (float)rng.NextDouble());
                Block(paving, c, new Vector3(11.8f, 2f, 11.8f), Quaternion.Euler((float)rng.NextDouble() * 2 - 1, (float)rng.NextDouble() * 6 - 3, (float)rng.NextDouble() * 2 - 1), Pick(PavingPalette, rng), rng, 0.12f);
                if (rng.NextDouble() < 0.18) Moss(moss, c + Vector3.up * 1f, 11f, 11f, rng);
            }
        // parapet along the front edge, chunky end blocks
        for (float x = r.xMin + 4f; x < 46f; x += 15f + (float)rng.NextDouble() * 4f)
        {
            if (rng.NextDouble() < 0.25) continue;
            var c = new Vector3(x, AqueductTop + 4.5f, AqueductFace + 4f);
            Block(stone, c, new Vector3(9f + (float)rng.NextDouble() * 3f, 7f, 7f), Quaternion.Euler(0, (float)rng.NextDouble() * 8 - 4, 0), Pick(StonePalette, rng), rng);
            if (rng.NextDouble() < 0.35) Moss(moss, c + Vector3.up * 3.5f, 8f, 6f, rng);
        }
        Block(stone, new Vector3(r.xMin + 7f, AqueductTop + 6f, AqueductFace + 8f), new Vector3(14f, 12f, 14f), Quaternion.Euler(0, 3, 0), Pick(StonePalette, rng), rng);
        Moss(moss, new Vector3(r.xMin + 7f, AqueductTop + 12f, AqueductFace + 8f), 12f, 12f, rng);

        // broken end: rubble on the ground
        for (int i = 0; i < 6; i++)
        {
            var c = new Vector3(60f + (float)rng.NextDouble() * 12f, 2.5f + (float)rng.NextDouble() * 2f, AqueductFace - 6f + (float)rng.NextDouble() * 14f);
            Block(stone, c, new Vector3(5f + (float)rng.NextDouble() * 5f, 4f + (float)rng.NextDouble() * 3f, 5f + (float)rng.NextDouble() * 4f),
                Quaternion.Euler((float)rng.NextDouble() * 20 - 10, (float)rng.NextDouble() * 360, (float)rng.NextDouble() * 20 - 10), Pick(StonePalette, rng), rng);
        }

        // teal banner with a gold diamond, and a pink crack near the broken end
        var gold = Lin(new Color(0.95f, 0.72f, 0.22f));
        var teal = Lin(new Color(0.10f, 0.55f, 0.60f));
        Banner(cloth, trim, new Vector3(34f, AqueductTop - 2f, AqueductFace - 0.9f), Vector3.back, 11f, 16f, (u, v) =>
        {
            bool border = u < 0.1f || u > 0.9f || v < 0.06f;
            float dx = Mathf.Abs(u - 0.5f) * 2f, dy = Mathf.Abs(v - 0.45f) * 2.6f;
            bool diamond = dx + dy < 0.62f;
            return border || diamond ? gold : teal;
        }, gold, rng);
        EnvMeshKit.Crack(cracks, new[] { new Vector3(52f, 2f, AqueductFace - 1.2f), new Vector3(54f, 5f, AqueductFace - 1.2f), new Vector3(51f, 8f, AqueductFace - 1.2f), new Vector3(55f, 12f, AqueductFace - 1.2f), new Vector3(53f, 15f, AqueductFace - 1.2f) }, Vector3.back, 1.8f);
    }

    // ------------------------------------------------------------------ terrace (right back)

    static void BuildTerrace(MeshBuilder stone, MeshBuilder paving, MeshBuilder moss, MeshBuilder cracks, MeshBuilder cloth, MeshBuilder trim, Transform colliders, Random rng)
    {
        var r = TerraceRect;
        AddBoxCollider(colliders, "Terrace", new Vector3(r.center.x, 30f, r.center.y), new Vector3(r.width, 60f, r.height));
        float[] tops = { 7f, 14.5f, TerraceTop };
        for (int k = 0; k < 3; k++)
        {
            float y0 = k == 0 ? 0f : tops[k - 1];
            LayCourse(stone, r.xMin, r.xMax, y0, tops[k], r.yMin, rng);                                   // front face
            LayCourseZ(stone, r.yMin + 10f, r.yMax, y0, tops[k], r.xMin, rng);                              // left face
        }
        Block(stone, new Vector3(r.center.x + 5f, TerraceTop * 0.5f - 0.5f, r.center.y + 5f), new Vector3(r.width - 10f, TerraceTop - 1f, r.height - 10f), Quaternion.identity, Pick(StonePalette, rng), rng, 0.05f);
        for (float x = r.xMin + 6f; x < r.xMax; x += 12.5f)
            for (float z = r.yMin + 6f; z < r.yMax; z += 12.5f)
            {
                var c = new Vector3(x + (float)rng.NextDouble(), TerraceTop + 0.6f + (float)rng.NextDouble() * 0.4f, z + (float)rng.NextDouble());
                Block(paving, c, new Vector3(11.8f, 2f, 11.8f), Quaternion.Euler(0, (float)rng.NextDouble() * 6 - 3, 0), Pick(PavingPalette, rng), rng, 0.12f);
                if (rng.NextDouble() < 0.14) Moss(moss, c + Vector3.up, 10f, 10f, rng);
            }
        // parapet with gaps
        for (float x = r.xMin + 5f; x < r.xMax - 4f; x += 14f + (float)rng.NextDouble() * 5f)
        {
            if (rng.NextDouble() < 0.3) continue;
            Block(stone, new Vector3(x, TerraceTop + 4f, r.yMin + 4f), new Vector3(9f, 6f, 7f), Quaternion.Euler(0, (float)rng.NextDouble() * 8 - 4, 0), Pick(StonePalette, rng), rng);
        }
        // pillar with the pink banner, a broken pillar
        var pillar = new Vector3(147f, TerraceTop, r.yMin + 6f);
        for (int i = 0; i < 2; i++)
            Block(stone, pillar + Vector3.up * (3.5f + i * 7f), new Vector3(9f - i * 0.4f, 7f, 9f - i * 0.4f), Quaternion.Euler(0, (float)rng.NextDouble() * 6 - 3, 0), Pick(StonePalette, rng), rng);
        Block(stone, pillar + Vector3.up * 15f, new Vector3(11.5f, 3f, 11.5f), Quaternion.identity, Pick(StonePalette, rng), rng);
        var magenta = Lin(new Color(0.78f, 0.18f, 0.48f));
        var plum = Lin(new Color(0.32f, 0.10f, 0.28f));
        Banner(cloth, trim, new Vector3(128f, TerraceTop - 1.5f, r.yMin - 0.9f), Vector3.back, 10f, 16f, (u, v) =>
        {
            bool border = u < 0.1f || u > 0.9f || v < 0.06f;
            float x = (u - 0.5f) * 2f, y = v;
            bool claw = Mathf.Abs(Mathf.Abs(x) - (0.55f - y * 0.55f)) < 0.09f && y > 0.25f && y < 0.75f || Mathf.Abs(x) < 0.07f && y > 0.2f && y < 0.8f;
            return border || claw ? plum : magenta;
        }, Lin(new Color(0.9f, 0.7f, 0.3f)), rng);
        Block(stone, new Vector3(104f, TerraceTop + 4f, r.yMin + 8f), new Vector3(8f, 8f, 8f), Quaternion.Euler(4, 20, 0), Pick(StonePalette, rng), rng);
        Block(stone, new Vector3(112f, TerraceTop + 1.5f, r.yMin + 14f), new Vector3(6f, 3f, 5f), Quaternion.Euler(10, 50, 6), Pick(StonePalette, rng), rng);

        // glowing cracks across the front face and the left face
        Crack(cracks, new Vector3(104f, 1f, r.yMin - 1.2f), Vector3.back, Vector3.right, 19f, rng);
        Crack(cracks, new Vector3(146f, 1.5f, r.yMin - 1.2f), Vector3.back, Vector3.right, 17f, rng);
        Crack(cracks, new Vector3(r.xMin - 1.2f, 1f, 132f), Vector3.left, Vector3.forward, 17f, rng);
    }

    // ------------------------------------------------------------------ stairs up to the terrace

    static void BuildStairs(MeshBuilder stone, Transform colliders, Random rng)
    {
        var r = StairsRect;
        AddBoxCollider(colliders, "Stairs", new Vector3(r.center.x, 30f, r.center.y), new Vector3(r.width, 60f, r.height));
        const int steps = 8;
        float run = (r.xMax - r.xMin) / steps, rise = TerraceTop / steps;
        for (int k = 0; k < steps; k++)
        {
            float x0 = r.xMin + k * run;
            float h = (k + 1) * rise;
            for (int part = 0; part < 2; part++)
            {
                float z0 = r.yMin + 3f + part * 13.5f;
                Block(stone, new Vector3(x0 + run * 0.5f + 0.4f, h * 0.5f, z0 + 6.6f), new Vector3(run + 0.8f, h, 13.2f), Quaternion.Euler(0, (float)rng.NextDouble() * 2 - 1, 0), Pick(PavingPalette, rng), rng, 0.1f);
            }
        }
        // stepped cheek walls on both sides
        foreach (var z in new[] { r.yMin + 1.5f, r.yMax - 1.5f })
            for (int k = 0; k < steps; k += 2)
            {
                float x0 = r.xMin + k * run;
                float h = (k + 2) * rise + 3f;
                Block(stone, new Vector3(x0 + run, h * 0.5f, z), new Vector3(run * 2f + 0.5f, h, 3.5f), Quaternion.identity, Pick(StonePalette, rng), rng);
            }
    }

    // ------------------------------------------------------------------ freestanding ruined arch

    static void BuildRuinArch(MeshBuilder stone, MeshBuilder moss, MeshBuilder cracks, Transform colliders, Random rng)
    {
        float left = RuinArch.x - RuinGap * 0.5f - RuinPillar * 0.5f, right = RuinArch.x + RuinGap * 0.5f + RuinPillar * 0.5f;
        float z = RuinArch.y;
        // left pillar full height, right pillar broken lower
        Block(stone, new Vector3(left, 1.2f, z), new Vector3(RuinPillar + 3f, 2.4f, RuinPillar + 3f), Quaternion.identity, Pick(StonePalette, rng), rng);   // plinths
        Block(stone, new Vector3(right, 1.2f, z), new Vector3(RuinPillar + 3f, 2.4f, RuinPillar + 3f), Quaternion.identity, Pick(StonePalette, rng), rng);
        for (int i = 0; i < 3; i++)
            Block(stone, new Vector3(left + (float)rng.NextDouble() - 0.5f, 6.4f + i * 7.2f, z), new Vector3(RuinPillar, 7.2f, RuinPillar), Quaternion.Euler(0, (float)rng.NextDouble() * 4 - 2, 0), Pick(StonePalette, rng), rng);
        for (int i = 0; i < 3; i++)
            Block(stone, new Vector3(right + (float)rng.NextDouble() - 0.5f, 6.4f + i * 7.2f, z), new Vector3(RuinPillar, 7.2f, RuinPillar), Quaternion.Euler(0, (float)rng.NextDouble() * 4 - 2, 0), Pick(StonePalette, rng), rng);
        foreach (var px in new[] { left, right })                                                              // capitals
            Block(stone, new Vector3(px, RuinPillarHeight + 1.2f, z), new Vector3(RuinPillar + 2.5f, 2.4f, RuinPillar + 2.5f), Quaternion.identity, Pick(StonePalette, rng), rng);
        // arch: left half and top survive, the right side has fallen
        float inner = RuinGap * 0.5f, outer = inner + 6f;
        Voussoirs(stone, new Vector3(RuinArch.x, RuinPillarHeight + 2.4f, z), inner, outer, 11f, 9, new[] { 0, 1, 2, 3, 4, 5, 6, 7 }, rng);
        Moss(moss, new Vector3(RuinArch.x - 6f, RuinPillarHeight + 2.4f + outer - 0.5f, z), 7f, 9f, rng);
        // fallen voussoirs on the ground
        for (int i = 0; i < 2; i++)
            Block(stone, new Vector3(right + 4f + i * 6f + (float)rng.NextDouble() * 3f, 2.6f, z + 10f + (float)rng.NextDouble() * 6f), new Vector3(6f, 5f, 9f),
                Quaternion.Euler((float)rng.NextDouble() * 30 - 15, (float)rng.NextDouble() * 360, (float)rng.NextDouble() * 30 - 15), Pick(StonePalette, rng), rng);
        AddBoxCollider(colliders, "ArchLeft", new Vector3(left, 30f, z), new Vector3(RuinPillar + 1f, 60f, RuinPillar + 1f));
        AddBoxCollider(colliders, "ArchRight", new Vector3(right, 30f, z), new Vector3(RuinPillar + 1f, 60f, RuinPillar + 1f));
        // glowing cracks on the ground under the arch
        Crack(cracks, new Vector3(RuinArch.x - 14f, 0.35f, z - 14f), Vector3.up, new Vector3(0.7f, 0, 0.7f).normalized, 24f, rng);
        Crack(cracks, new Vector3(RuinArch.x + 4f, 0.35f, z + 6f), Vector3.up, new Vector3(0.9f, 0, -0.4f).normalized, 18f, rng);
        Crack(cracks, new Vector3(right - RuinPillar * 0.5f - 0.5f, 3f, z - RuinPillar * 0.5f - 1.2f), Vector3.back, Vector3.right, 16f, rng);
    }

    // ------------------------------------------------------------------ cover blocks

    static void BuildCover(MeshBuilder stone, MeshBuilder moss, Transform colliders, Random rng)
    {
        foreach (var c in CoverClusters)
        {
            var center = new Vector3(c.x, 0, c.y);
            Block(stone, center + new Vector3(-3f, 7f, 0f), new Vector3(13f, 14f, 13f), Quaternion.Euler(0, (float)rng.NextDouble() * 20 - 10, 0), Pick(StonePalette, rng), rng);
            Block(stone, center + new Vector3(7f, 5f, 4f), new Vector3(9f, 10f, 10f), Quaternion.Euler(0, (float)rng.NextDouble() * 30, 0), Pick(StonePalette, rng), rng);
            if (rng.NextDouble() < 0.7) Moss(moss, center + new Vector3(-3f, 14f, 0f), 11f, 11f, rng);
            AddBoxCollider(colliders, $"Cover_{c.x}_{c.y}", center + Vector3.up * 30f, new Vector3(24f, 60f, 20f));
        }
    }

    // ------------------------------------------------------------------ paving (sanctuary courtyard)

    static void BuildPaving(MeshBuilder paving, MeshBuilder cracks, Random rng)
    {
        const float cell = 10f;
        for (float x = 58f; x < 142f; x += cell)
            for (float z = -2f; z < 106f; z += cell)
            {
                var p = new Vector2(x + cell * 0.5f, z + cell * 0.5f);
                if (!InsideArena(p, 6f) || InsideStructure(p, 4f) || NearCover(p, 16f) || NearRuinPillar(p, 9f)) continue;
                float mask = EnvMeshKit.Noise(new Vector3(p.x / 34f, 0, p.y / 34f), 91);
                float near = Mathf.Max(1f - Vector2.Distance(p, RuinArch) / 40f, p.y > 90f ? 0.6f : 0f);
                float edge = Mathf.InverseLerp(58f, 80f, p.x);                  // fades out toward the stream half
                if (mask * 0.8f + near * 0.6f + 0.15f < 0.62f || rng.NextDouble() > edge + 0.2f) continue;
                float w = Mathf.Lerp(8.4f, 9.4f, (float)rng.NextDouble());
                var c = new Vector3(p.x + (float)rng.NextDouble() - 0.5f, 0.1f + (float)rng.NextDouble() * 0.25f, p.y + (float)rng.NextDouble() - 0.5f);
                if (rng.NextDouble() < 0.12)
                {   // a broken slab: two halves
                    Block(paving, c + new Vector3(-w * 0.26f, 0, 0), new Vector3(w * 0.46f, 1f, w), Quaternion.Euler(0, (float)rng.NextDouble() * 10 - 5, 1.5f), Pick(PavingPalette, rng), rng, 0.15f);
                    Block(paving, c + new Vector3(w * 0.26f, -0.1f, 0), new Vector3(w * 0.44f, 1f, w * 0.9f), Quaternion.Euler(0, (float)rng.NextDouble() * 14 - 7, -2f), Pick(PavingPalette, rng), rng, 0.15f);
                }
                else Block(paving, c, new Vector3(w, 1f, w), Quaternion.Euler((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() * 6 - 3, (float)rng.NextDouble() - 0.5f), Pick(PavingPalette, rng), rng, 0.15f);
            }
        // a few slabs in front of the aqueduct too
        for (int i = 0; i < 9; i++)
        {
            var p = new Vector2(24f + (float)rng.NextDouble() * 34f, 100f + (float)rng.NextDouble() * 14f);
            if (InsideStructure(p, 3f) || DistanceToStream(p) < 12f) continue;
            Block(paving, new Vector3(p.x, 0.2f, p.y), new Vector3(8.6f, 1f, 8.6f), Quaternion.Euler(0, (float)rng.NextDouble() * 20 - 10, 0), Pick(PavingPalette, rng), rng, 0.15f);
        }
    }

    // ------------------------------------------------------------------ stream stones, bridge, stepping stones

    static void BuildStreamStones(MeshBuilder stone, MeshBuilder paving, MeshBuilder moss, List<Vector3> path, Random rng)
    {
        for (int i = 4; i < path.Count - 6; i += 4)
        {
            if (path[i].z > AqueductFace - 2f) continue;
            var fwd = (path[i + 1] - path[i - 1]); fwd.y = 0; fwd.Normalize();
            var side = Vector3.Cross(Vector3.up, fwd);
            float halfW = StreamWidth(path[i].z) * 0.5f;
            foreach (var s in new[] { -1f, 1f })
            {
                if (rng.NextDouble() < 0.45) continue;
                var c = path[i] + side * s * (halfW + 1.5f + (float)rng.NextDouble() * 2f);
                c.y = 0.8f;
                float sz = 3f + (float)rng.NextDouble() * 3f;
                Block(stone, c, new Vector3(sz, 2.2f + (float)rng.NextDouble() * 1.5f, sz * 0.8f), Quaternion.Euler(0, (float)rng.NextDouble() * 360, 0), Pick(StonePalette, rng), rng, 0.45f, 0.08f);
                if (rng.NextDouble() < 0.3) Moss(moss, c + Vector3.up * 1.6f, sz * 0.8f, sz * 0.6f, rng);
            }
        }
        // slab bridge where the stream crosses z ~ 96
        var bridge = new Vector3(14f, 0f, 84f);
        Block(paving, bridge + new Vector3(0, 2.4f, 0), new Vector3(30f, 2.8f, 12f), Quaternion.Euler(0, 26, 0), Pick(PavingPalette, rng), rng, 0.12f);
        foreach (var s in new[] { -1f, 1f })
            Block(stone, bridge + Quaternion.Euler(0, 26, 0) * new Vector3(s * 12f, 1.2f, 0), new Vector3(6f, 3f, 13f), Quaternion.Euler(0, 26, 0), Pick(StonePalette, rng), rng);
        // stepping stones near z ~ 30
        for (int i = -1; i <= 1; i++)
            Block(paving, new Vector3(3f + i * 6.5f, 0.2f, 27f + i * 1.5f), new Vector3(5.5f, 2.2f, 5.5f), Quaternion.Euler(0, i * 17f, 0), Pick(PavingPalette, rng), rng, 0.4f);
    }

    // ------------------------------------------------------------------ ground

    static Mesh BuildGround(List<Vector3> streamPath)
    {
        var grass = Lin(Grass); var dark = Lin(GrassDark); var light = Lin(GrassLight); var forest = Lin(ForestFloor); var dirt = Lin(Dirt);
        var cliff = Lin(new Color(0.40f, 0.32f, 0.28f));
        float Height(float x, float z)
        {
            var p = new Vector2(x, z);
            float inside = ArenaDistance(p);                              // >0 outside the arena
            float h = 0f;
            if (inside > 6f)
            {
                float hills = (EnvMeshKit.Noise(new Vector3(x / 46f, 0, z / 46f), 5) - 0.4f) * 9f;
                h = Mathf.Max(0f, Mathf.Min((inside - 6f) * 0.12f, 16f)) + hills * Mathf.Clamp01((inside - 6f) / 30f);
            }
            if (z < -20f) h -= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-20f, -36f, z)) * 42f;   // the front edge falls away
            float ds = DistanceToStream(p);
            float halfW = StreamWidth(z) * 0.5f;
            if (ds < halfW + 6f && z < AqueductFace + 20f)
                h = Mathf.Min(h, Mathf.Lerp(-3.2f, 0f, Mathf.SmoothStep(0f, 1f, (ds - halfW + 1f) / 7f)));
            return h;
        }
        Color Paint(float x, float z)
        {
            var p = new Vector2(x, z);
            float inside = ArenaDistance(p);
            float n = EnvMeshKit.Noise(new Vector3(x / 22f, 0, z / 22f), 11);
            float n2 = EnvMeshKit.Noise(new Vector3(x / 9f, 0, z / 9f), 12);
            var c = Color.Lerp(dark, light, Mathf.SmoothStep(0.3f, 0.7f, n));
            c = Color.Lerp(c, grass, 0.35f + 0.2f * n2);
            if (inside > -10f) c *= Mathf.Lerp(1f, 0.8f, Mathf.InverseLerp(-10f, 0f, inside));        // shade along the walls
            if (inside > 4f) c = Color.Lerp(c, forest, Mathf.Clamp01((inside - 4f) / 12f)) * (0.9f + 0.2f * n2);
            if (inside > 10f && n2 > 0.72f) c = Color.Lerp(c, dirt, 0.5f);
            float ds = DistanceToStream(p);
            float halfW = StreamWidth(z) * 0.5f;
            if (ds < halfW + 5f) c = Color.Lerp(dirt, c, Mathf.SmoothStep(0f, 1f, (ds - halfW + 1f) / 6f));
            if (z < -20f) c = Color.Lerp(c, cliff, Mathf.InverseLerp(-20f, -28f, z));
            c.a = 1f;
            return c;
        }
        return EnvMeshKit.Ground(new Vector2(-240f, -60f), new Vector2(340f, 360f), 4f, Height, Paint, "Courtyard_Ground");
    }

    // ------------------------------------------------------------------ water

    static List<Vector3> StreamPath()
    {
        var pts = new List<Vector3>();
        for (int i = 0; i < StreamLine.Length - 1; i++)
        {
            var p0 = StreamLine[Mathf.Max(i - 1, 0)]; var p1 = StreamLine[i]; var p2 = StreamLine[i + 1]; var p3 = StreamLine[Mathf.Min(i + 2, StreamLine.Length - 1)];
            int steps = Mathf.Max(2, Mathf.CeilToInt(Vector2.Distance(p1, p2) / 2f));
            for (int s = 0; s < steps; s++)
            {
                float t = (float)s / steps;
                var q = 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t * t + (-p0 + 3f * p1 - 3f * p2 + p3) * t * t * t);
                pts.Add(new Vector3(q.x, WaterLevel, q.y));
            }
        }
        var last = StreamLine[StreamLine.Length - 1];
        pts.Add(new Vector3(last.x, WaterLevel, last.y));
        // waterfall over the front edge
        pts.Add(new Vector3(last.x, WaterLevel - 6f, last.y - 3f));
        pts.Add(new Vector3(last.x, WaterLevel - 22f, last.y - 6f));
        pts.Add(new Vector3(last.x, WaterLevel - 40f, last.y - 8f));
        return pts;
    }

    static float StreamWidth(float z) => z > AqueductFace ? 22f : z < -6f ? 11f : Mathf.Lerp(12f, 16f, Mathf.Sin(Mathf.InverseLerp(-6f, AqueductFace, z) * Mathf.PI));

    static Mesh BuildWater(List<Vector3> path)
    {
        var widths = path.Select(p => StreamWidth(p.z)).ToArray();
        return EnvMeshKit.Ribbon(path, t => widths[Mathf.Clamp(Mathf.RoundToInt(t * (widths.Length - 1)), 0, widths.Length - 1)], 16f, "Courtyard_Water");
    }

    // ------------------------------------------------------------------ vegetation (Silver_Cats)

    static void BuildVegetation(Transform root, Random rng)
    {
        GameObject Load(string name) => AssetDatabase.FindAssets(name + " t:Prefab", new[] { "Assets/Silver_Cats" })
            .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<GameObject>).First(g => g.name == name);
        var pine = Load("Pine_Tree"); var cedar = Load("Cedar_Tree_03"); var larch = Load("Larch_Tree"); var stump = Load("Stump");
        var fern = Load("Fern"); var plantain = Load("Plantain"); var dandelion = Load("Dandelion_02"); var mushroom = Load("Boletus_Edulis");
        var veg = new GameObject("Vegetation").transform; veg.SetParent(root, false);
        var trees = new GameObject("Trees").transform; trees.SetParent(veg, false);
        var plants = new GameObject("Plants").transform; plants.SetParent(veg, false);

        void Place(GameObject prefab, Transform parent, Vector3 pos, float scale)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.gameObject.scene);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0));
            go.transform.localScale = Vector3.one * scale;
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(c);
        }

        // forest ring outside the walls
        for (float x = -230f; x < 330f; x += 24f)
            for (float z = -10f; z < 350f; z += 24f)
            {
                var p = new Vector2(x + (float)(rng.NextDouble() * 16 - 8), z + (float)(rng.NextDouble() * 16 - 8));
                float d = ArenaDistance(p);
                if (d < 22f || rng.NextDouble() < 0.18) continue;
                if (AqueductRect.Contains(p) || TerraceRect.Contains(p)) continue;
                float near = Mathf.InverseLerp(22f, 70f, d);                 // smaller trees right next to the arena
                float y = GroundHeightApprox(p);
                double k = rng.NextDouble();
                var pos = new Vector3(p.x, y - 0.5f, p.y);
                if (k < 0.42) Place(pine, trees, pos, Mathf.Lerp(2.2f, 3.3f, near) * (0.85f + 0.3f * (float)rng.NextDouble()));
                else if (k < 0.72) Place(cedar, trees, pos, Mathf.Lerp(4.2f, 6.4f, near) * (0.85f + 0.3f * (float)rng.NextDouble()));
                else Place(larch, trees, pos, Mathf.Lerp(3.6f, 5.4f, near) * (0.85f + 0.3f * (float)rng.NextDouble()));
                if (rng.NextDouble() < 0.35) Place(fern, plants, pos + new Vector3((float)rng.NextDouble() * 10 - 5, 0, (float)rng.NextDouble() * 10 - 5), 18f + (float)rng.NextDouble() * 10f);
                if (rng.NextDouble() < 0.08) Place(stump, plants, pos + new Vector3(9f, 0, 4f), 9f + (float)rng.NextDouble() * 4f);
            }

        // inside: ferns and plantain along the walls and structures, dandelions and mushrooms dotted around
        for (int i = 0; i < 260; i++)
        {
            var p = new Vector2(Mathf.Lerp(-32f, 144f, (float)rng.NextDouble()), Mathf.Lerp(-6f, 166f, (float)rng.NextDouble()));
            if (!InsideArena(p, 2f) || InsideStructure(p, 1f) || DistanceToStream(p) < StreamWidth(p.y) * 0.5f + 2f || NearCover(p, 9f) || NearRuinPillar(p, 8f)) continue;
            float wall = -ArenaDistance(p);                                   // distance to the wall
            float structure = DistanceToStructures(p);
            bool edge = wall < 9f || structure < 8f || NearCover(p, 15f);
            bool paved = p.x > 70f && p.y < 106f;
            bool nearCamera = p.y < 30f;                                     // the front strip: keep it low and light
            if (edge && rng.NextDouble() < (nearCamera ? 0.45 : 0.75))
            {
                if (rng.NextDouble() < (nearCamera ? 0.3 : 0.6)) Place(fern, plants, new Vector3(p.x, 0, p.y), (nearCamera ? 13f : 18f) + (float)rng.NextDouble() * 8f);
                else Place(plantain, plants, new Vector3(p.x, 0, p.y), (nearCamera ? 14f : 18f) + (float)rng.NextDouble() * 8f);
            }
            else if (!paved && rng.NextDouble() < 0.16) Place(dandelion, plants, new Vector3(p.x, 0, p.y), 22f + (float)rng.NextDouble() * 8f);
            else if (structure < 20f && rng.NextDouble() < 0.12) Place(mushroom, plants, new Vector3(p.x, 0, p.y), 16f + (float)rng.NextDouble() * 8f);
        }
        // on top of the aqueduct and terrace
        for (int i = 0; i < 22; i++)
        {
            bool aq = i % 2 == 0;
            var rect = aq ? Rect.MinMaxRect(-40, 124, 48, 164) : Rect.MinMaxRect(100, 116, 152, 164);
            var p = new Vector2(Mathf.Lerp(rect.xMin, rect.xMax, (float)rng.NextDouble()), Mathf.Lerp(rect.yMin, rect.yMax, (float)rng.NextDouble()));
            Place(rng.NextDouble() < 0.7 ? fern : plantain, plants, new Vector3(p.x, (aq ? AqueductTop : TerraceTop) + 1.4f, p.y), 18f + (float)rng.NextDouble() * 9f);
        }
    }

    // ------------------------------------------------------------------ scene install

    /// <summary>Replaces the old environment in the open DemoScene with the courtyard and sets up light / shadows.</summary>
    public static void InstallIntoOpenScene()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        foreach (var go in scene.GetRootGameObjects())
        {
            var n = go.name;
            if (n == "Floor" || n == "Wall" || n == "Trees" || n == "Terrain" || n.StartsWith("Cube (") || n == "Courtyard")
                UnityEngine.Object.DestroyImmediate(go);
        }
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var map = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        map.transform.SetAsFirstSibling();

        var spawn = GameObject.Find("SpawnRegion");
        if (spawn)
        {
            spawn.transform.SetPositionAndRotation(SpawnArea.center, Quaternion.identity);
            var box = spawn.GetComponent<BoxCollider>();
            box.center = Vector3.zero;
            box.size = SpawnArea.size;
        }

        var sun = GameObject.Find("Directional Light");
        if (sun)
        {
            var l = sun.GetComponent<Light>();
            sun.transform.rotation = Quaternion.Euler(50f, 64f, 0f);
            l.color = new Color(1f, 0.93f, 0.82f);
            l.intensity = 1.35f;
            l.shadows = LightShadows.Soft;
            l.shadowStrength = 0.8f;
        }
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.86f, 0.82f, 0.94f);
        RenderSettings.ambientEquatorColor = new Color(0.76f, 0.68f, 0.72f);
        RenderSettings.ambientGroundColor = new Color(0.40f, 0.36f, 0.36f);
        RenderSettings.fog = false;

        var cam = Camera.main;
        if (cam)
        {
            var data = cam.GetUniversalAdditionalCameraData();
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
        }
        ConfigureShadows();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    /// <summary>The game camera is 100-260 units from the ground: shadows must reach that far.</summary>
    static void ConfigureShadows()
    {
        foreach (var path in new[] { "Assets/Settings/PC_RPAsset.asset", "Assets/Settings/Mobile_RPAsset.asset" })
        {
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            if (!asset) continue;
            var so = new SerializedObject(asset);
            so.FindProperty("m_ShadowDistance").floatValue = 320f;
            so.FindProperty("m_ShadowCascadeCount").intValue = path.Contains("PC") ? 4 : 2;
            so.FindProperty("m_Cascade4Split").vector3Value = new Vector3(0.38f, 0.55f, 0.75f);
            so.FindProperty("m_Cascade2Split").floatValue = 0.55f;
            if (path.Contains("PC")) so.FindProperty("m_MainLightShadowmapResolution").intValue = 4096;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
        }
        AssetDatabase.SaveAssets();
    }

    // ------------------------------------------------------------------ building blocks

    static void Block(MeshBuilder mb, Vector3 center, Vector3 size, Quaternion rot, Color color, Random rng, float roundness = 0.12f, float wobble = 0.035f) =>
        EnvMeshKit.Block(mb, center, size, rot, color, rng.Next(1, 100000), roundness, wobble);

    /// <summary>A course of blocks along x on a face at z = <paramref name="faceZ"/> (blocks extend +z from it).</summary>
    static void LayCourse(MeshBuilder mb, float x0, float x1, float y0, float y1, float faceZ, Random rng)
    {
        float x = x0;
        while (x < x1 - 1f)
        {
            float w = Mathf.Min(Mathf.Lerp(11f, 18f, (float)rng.NextDouble()), x1 - x);
            if (x1 - (x + w) < 5f) w = x1 - x;
            var c = new Vector3(x + w * 0.5f, (y0 + y1) * 0.5f, faceZ + 5.5f);
            Block(mb, c + new Vector3(0, 0, (float)rng.NextDouble() * 0.8f - 0.4f), new Vector3(w - 0.5f, y1 - y0 - 0.4f, 11f), Quaternion.Euler(0, (float)rng.NextDouble() * 2 - 1, (float)rng.NextDouble() * 1.5f - 0.75f), Pick(StonePalette, rng), rng);
            x += w;
        }
    }

    /// <summary>A course of blocks along z on a face at x = <paramref name="faceX"/> (blocks extend +x from it).</summary>
    static void LayCourseZ(MeshBuilder mb, float z0, float z1, float y0, float y1, float faceX, Random rng)
    {
        float z = z0;
        while (z < z1 - 1f)
        {
            float w = Mathf.Min(Mathf.Lerp(11f, 18f, (float)rng.NextDouble()), z1 - z);
            if (z1 - (z + w) < 5f) w = z1 - z;
            var c = new Vector3(faceX + 5.5f, (y0 + y1) * 0.5f, z + w * 0.5f);
            Block(mb, c, new Vector3(11f, y1 - y0 - 0.4f, w - 0.5f), Quaternion.Euler((float)rng.NextDouble() * 1.5f - 0.75f, (float)rng.NextDouble() * 2 - 1, 0), Pick(StonePalette, rng), rng);
            z += w;
        }
    }

    /// <summary>Wedge stones of a semicircular arch springing at <paramref name="center"/> (arch plane = XY).</summary>
    static void Voussoirs(MeshBuilder mb, Vector3 center, float inner, float outer, float depth, int count, IEnumerable<int> keep, Random rng)
    {
        float mid = (inner + outer) * 0.5f;
        float arc = Mathf.PI * mid / count;
        foreach (int k in keep)
        {
            float theta = Mathf.PI - (k + 0.5f) * Mathf.PI / count;
            var c = center + new Vector3(Mathf.Cos(theta) * mid, Mathf.Sin(theta) * mid, 0f);
            float scale = k == count / 2 ? 1.12f : 1f;                     // keystone
            var rot = Quaternion.Euler(0, 0, theta * Mathf.Rad2Deg - 90f);
            Block(mb, c, new Vector3(arc - 0.5f, (outer - inner) * scale, depth * scale), rot, Pick(StonePalette, rng), rng, 0.2f, 0.04f);
        }
    }

    /// <summary>A clump of soft moss blobs hugging one edge of a block's top.</summary>
    static void Moss(MeshBuilder moss, Vector3 topCenter, float w, float d, Random rng)
    {
        float edgeX = (rng.NextDouble() < 0.5 ? -1f : 1f) * w * 0.3f;
        int blobs = 3 + rng.Next(4);
        var anchor = topCenter + new Vector3(edgeX, 0f, (float)(rng.NextDouble() - 0.5) * d * 0.4f);
        for (int i = 0; i < blobs; i++)
        {
            float sz = Mathf.Lerp(1.6f, 3.6f, (float)rng.NextDouble());
            var off = new Vector3((float)(rng.NextDouble() - 0.5) * w * 0.35f, 0.15f, (float)(rng.NextDouble() - 0.5) * d * 0.45f);
            Block(moss, anchor + off, new Vector3(sz, Mathf.Lerp(0.8f, 1.5f, (float)rng.NextDouble()), sz * Mathf.Lerp(0.7f, 1.1f, (float)rng.NextDouble())),
                Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0), Pick(MossPalette, rng), rng, 0.95f, 0.15f);
        }
    }

    static void Banner(MeshBuilder cloth, MeshBuilder trim, Vector3 top, Vector3 facing, float width, float height, Func<float, float, Color> paint, Color rodColor, Random rng)
    {
        EnvMeshKit.Banner(cloth, top, facing, width, height, paint, rng.Next(1, 1000));
        var right = Vector3.Cross(Vector3.up, facing).normalized;
        EnvMeshKit.Block(trim, top + Vector3.up * 0.4f, new Vector3(width + 3f, 1.1f, 1.1f), Quaternion.LookRotation(facing), rodColor, rng.Next(1, 1000), 0.45f, 0.01f);
        foreach (var s in new[] { -1f, 1f })
            EnvMeshKit.Block(trim, top + Vector3.up * 0.4f + right * s * (width * 0.5f + 1.8f), new Vector3(1.8f, 1.8f, 1.8f), Quaternion.identity, rodColor, rng.Next(1, 1000), 0.9f, 0.01f);
    }

    /// <summary>A jagged glowing crack starting at <paramref name="start"/>, running along <paramref name="along"/> on a surface facing <paramref name="normal"/>.</summary>
    static void Crack(MeshBuilder mb, Vector3 start, Vector3 normal, Vector3 along, float length, Random rng)
    {
        var across = Vector3.Cross(normal, along).normalized;
        if (normal == Vector3.back || normal == Vector3.left) across = Vector3.up;   // wall cracks climb upward
        var pts = new List<Vector3> { start };
        int n = 6 + rng.Next(3);
        for (int i = 1; i <= n; i++)
            pts.Add(start + along * (length * i / n) * (normal == Vector3.up ? 1f : 0.35f) + across * (normal == Vector3.up ? (float)(rng.NextDouble() * 4 - 2) : length * i / n * 0.8f) + along * (float)(rng.NextDouble() * 3 - 1.5));
        EnvMeshKit.Crack(mb, pts, normal, 1.8f);
        // a short branch
        int b = 2 + rng.Next(Mathf.Max(1, n - 3));
        var branch = new List<Vector3> { pts[b], pts[b] + along * 3f + across * 2f, pts[b] + along * 5f + across * 1f };
        EnvMeshKit.Crack(mb, branch, normal, 1.2f);
    }

    static void AddBoxCollider(Transform parent, string name, Vector3 center, Vector3 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = center;
        go.AddComponent<BoxCollider>().size = size;
    }

    static void AddPart(GameObject root, string name, Mesh mesh, Material mat, Material extra = null, bool castShadows = true)
    {
        if (extra)
        {   // the extra pass (e.g. GlowSource) draws the same triangles from its own submesh
            var tris = mesh.GetTriangles(0);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(tris, 1);
        }
        var path = $"{MeshDir}/{mesh.name}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing) AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(mesh, path);
        var go = new GameObject(name);
        go.transform.SetParent(root.transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterials = extra ? new[] { mat, extra } : new[] { mat };
        mr.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
    }

    // ------------------------------------------------------------------ materials

    static Dictionary<string, Material> Materials()
    {
        var env = Shader.Find("GameJam/StylizedEnv");
        var water = Shader.Find("GameJam/StylizedWater");
        var glow = Shader.Find("GameJam/GlowSource");
        Material Mat(string name, Shader shader, Action<Material> setup)
        {
            var path = $"{MatDir}/MI_Courtyard_{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(shader) { name = $"MI_Courtyard_{name}" }; AssetDatabase.CreateAsset(m, path); }
            m.shader = shader;
            setup(m);
            EditorUtility.SetDirty(m);
            return m;
        }
        var grassTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Silver_Cats/Hand_Painted_Nature_Kit_LITE/Textures/Forest/Forest_Grass_02_d.png");
        return new Dictionary<string, Material>
        {
            ["Stone"] = Mat("Stone", env, m => { m.SetFloat("_MottleScale", 0.12f); m.SetFloat("_MottleStrength", 0.17f); m.SetColor("_TopTint", new Color(1.1f, 1.04f, 0.94f)); m.SetColor("_SideTint", new Color(0.8f, 0.72f, 0.86f)); m.SetFloat("_AmbientBoost", 0.95f); m.SetFloat("_RimStrength", 0.06f); }),
            ["Paving"] = Mat("Paving", env, m => { m.SetFloat("_MottleScale", 0.16f); m.SetFloat("_MottleStrength", 0.16f); m.SetColor("_TopTint", new Color(1.04f, 1.0f, 0.95f)); m.SetColor("_SideTint", new Color(0.76f, 0.68f, 0.8f)); m.SetFloat("_AmbientBoost", 0.95f); }),
            ["Moss"] = Mat("Moss", env, m => { m.SetFloat("_MottleScale", 0.3f); m.SetFloat("_MottleStrength", 0.25f); m.SetColor("_TopTint", new Color(1.08f, 1.06f, 0.9f)); m.SetColor("_SideTint", new Color(0.86f, 0.9f, 0.8f)); }),
            ["Ground"] = Mat("Ground", env, m =>
            {
                m.SetFloat("_MottleScale", 0.035f); m.SetFloat("_MottleStrength", 0.14f);
                m.SetTexture("_DetailTex", grassTex); m.SetFloat("_DetailScale", 0.045f); m.SetFloat("_DetailStrength", 0.32f);
                m.SetColor("_TopTint", Color.white); m.SetColor("_SideTint", new Color(0.9f, 0.86f, 0.8f)); m.SetFloat("_RimStrength", 0f);
            }),
            ["Cloth"] = Mat("Cloth", env, m => { m.SetFloat("_Cull", 0f); m.SetFloat("_MottleStrength", 0.06f); m.SetColor("_TopTint", Color.white); m.SetColor("_SideTint", Color.white); m.SetFloat("_Wrap", 0.6f); }),
            ["Trim"] = Mat("Trim", env, m => { m.SetFloat("_MottleStrength", 0.05f); m.SetFloat("_RimStrength", 0.3f); }),
            ["Crack"] = Mat("Crack", env, m => { m.SetColor("_BaseColor", new Color(0.25f, 0.02f, 0.15f)); m.SetColor("_EmissionColor", new Color(3.4f, 0.35f, 2.0f)); m.SetFloat("_MottleStrength", 0f); m.SetFloat("_RimStrength", 0f); }),
            ["CrackGlow"] = Mat("CrackGlow", glow, m => { m.SetColor("_GlowColor", new Color(2.6f, 0.3f, 1.6f)); m.SetFloat("_GlowIntensity", 0.9f); }),
            ["Water"] = Mat("Water", water, m => { }),
        };
    }

    // ------------------------------------------------------------------ geometry helpers

    /// <summary>Signed distance from the arena outline (negative inside).</summary>
    static float ArenaDistance(Vector2 p)
    {
        float best = float.MaxValue;
        bool inside = false;
        for (int i = 0, j = Outline.Length - 1; i < Outline.Length; j = i++)
        {
            var a = Outline[j]; var b = Outline[i];
            if ((b.y > p.y) != (a.y > p.y) && p.x < (a.x - b.x) * (p.y - b.y) / (a.y - b.y) + b.x) inside = !inside;
            var ab = a - b; float t = Mathf.Clamp01(Vector2.Dot(p - b, ab) / ab.sqrMagnitude);
            best = Mathf.Min(best, Vector2.Distance(p, b + ab * t));
        }
        return inside ? -best : best;
    }

    static bool InsideArena(Vector2 p, float margin) => ArenaDistance(p) < -margin;

    static bool InsideStructure(Vector2 p, float pad)
    {
        Rect Grow(Rect r) => Rect.MinMaxRect(r.xMin - pad, r.yMin - pad, r.xMax + pad, r.yMax + pad);
        return Grow(AqueductRect).Contains(p) || Grow(TerraceRect).Contains(p) || Grow(StairsRect).Contains(p);
    }

    static float DistanceToStructures(Vector2 p)
    {
        float D(Rect r) { float dx = Mathf.Max(r.xMin - p.x, 0, p.x - r.xMax), dz = Mathf.Max(r.yMin - p.y, 0, p.y - r.yMax); return Mathf.Sqrt(dx * dx + dz * dz); }
        return Mathf.Min(D(AqueductRect), Mathf.Min(D(TerraceRect), D(StairsRect)));
    }

    static bool NearCover(Vector2 p, float r) => CoverClusters.Any(c => Vector2.Distance(c, p) < r);

    static bool NearRuinPillar(Vector2 p, float r)
    {
        float off = RuinGap * 0.5f + RuinPillar * 0.5f;
        return Vector2.Distance(p, RuinArch + new Vector2(-off, 0)) < r || Vector2.Distance(p, RuinArch + new Vector2(off, 0)) < r;
    }

    static float DistanceToStream(Vector2 p)
    {
        float best = float.MaxValue;
        for (int i = 0; i < StreamLine.Length - 1; i++)
        {
            var a = StreamLine[i]; var b = StreamLine[i + 1];
            var ab = b - a; float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            best = Mathf.Min(best, Vector2.Distance(p, a + ab * t));
        }
        return best;
    }

    static float GroundHeightApprox(Vector2 p)
    {
        float inside = ArenaDistance(p);
        if (inside <= 6f) return 0f;
        float hills = (EnvMeshKit.Noise(new Vector3(p.x / 46f, 0, p.y / 46f), 5) - 0.4f) * 9f;
        float h = Mathf.Max(0f, Mathf.Min((inside - 6f) * 0.12f, 16f)) + hills * Mathf.Clamp01((inside - 6f) / 30f);
        if (p.y < -20f) h -= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-20f, -36f, p.y)) * 42f;
        return h;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }
}
