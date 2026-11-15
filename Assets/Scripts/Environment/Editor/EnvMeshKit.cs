using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Accumulates geometry from many pieces and builds one mesh (32-bit indices).</summary>
public sealed class MeshBuilder
{
    public readonly List<Vector3> Vertices = new List<Vector3>();
    public readonly List<Vector3> Normals = new List<Vector3>();
    public readonly List<Color> Colors = new List<Color>();
    public readonly List<Vector2> Uvs = new List<Vector2>();
    public readonly List<int> Triangles = new List<int>();

    public int VertexCount => Vertices.Count;

    public int AddVertex(Vector3 p, Vector3 n, Color c, Vector2 uv)
    {
        Vertices.Add(p); Normals.Add(n); Colors.Add(c); Uvs.Add(uv);
        return Vertices.Count - 1;
    }

    /// <summary>Adds a triangle, flipping it if needed so it faces along <paramref name="outward"/>.</summary>
    public void AddTriangle(int a, int b, int c, Vector3 outward)
    {
        var n = Vector3.Cross(Vertices[b] - Vertices[a], Vertices[c] - Vertices[a]);
        if (Vector3.Dot(n, outward) < 0f) { Triangles.Add(a); Triangles.Add(c); Triangles.Add(b); }
        else { Triangles.Add(a); Triangles.Add(b); Triangles.Add(c); }
    }

    public Mesh Build(string name)
    {
        var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
        mesh.SetVertices(Vertices);
        mesh.SetNormals(Normals);
        mesh.SetColors(Colors);
        mesh.SetUVs(0, Uvs);
        mesh.SetTriangles(Triangles, 0);
        mesh.RecalculateBounds();
        return mesh;
    }
}

/// <summary>Procedural pieces for the hand-painted stone courtyard.</summary>
public static class EnvMeshKit
{
    // ------------------------------------------------------------------ noise

    static float Hash(int x, int y, int z, int seed)
    {
        unchecked
        {
            int h = seed * 374761393 + x * 668265263 + y * 2147483647 + z * 1274126177;
            h = (h ^ (h >> 13)) * 1274126177;
            return ((h ^ (h >> 16)) & 0x7fffffff) / (float)int.MaxValue;
        }
    }

    /// <summary>Smooth 3D value noise in [0,1].</summary>
    public static float Noise(Vector3 p, int seed)
    {
        int xi = Mathf.FloorToInt(p.x), yi = Mathf.FloorToInt(p.y), zi = Mathf.FloorToInt(p.z);
        float fx = p.x - xi, fy = p.y - yi, fz = p.z - zi;
        fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy); fz = fz * fz * (3 - 2 * fz);
        float L(int a, int b, int c) => Hash(xi + a, yi + b, zi + c, seed);
        return Mathf.Lerp(
            Mathf.Lerp(Mathf.Lerp(L(0, 0, 0), L(1, 0, 0), fx), Mathf.Lerp(L(0, 1, 0), L(1, 1, 0), fx), fy),
            Mathf.Lerp(Mathf.Lerp(L(0, 0, 1), L(1, 0, 1), fx), Mathf.Lerp(L(0, 1, 1), L(1, 1, 1), fx), fy), fz);
    }

    // ------------------------------------------------------------------ stone block

    /// <summary>Grid coordinates along one axis of a rounded block: dense across the rounded edges.</summary>
    /// <summary>Multiplies the interior grid step of every block: bigger = fewer vertices on big faces (1 = full detail).</summary>
    public static float Coarseness = 1f;

    /// <summary>Blocks added to these builders get a lighter rounded edge (small or flat pieces: paving, moss).</summary>
    public static readonly HashSet<MeshBuilder> LowDetail = new HashSet<MeshBuilder>();

    static List<float> AxisSteps(float h, float r, float interiorStep, bool low)
    {
        var s = low ? new List<float> { -h, -h + 0.5f * r, -h + r } : new List<float> { -h, -h + 0.29f * r, -h + 0.71f * r, -h + r };
        float inner = 2f * (h - r);
        int cuts = Mathf.Max(0, Mathf.CeilToInt(inner / interiorStep) - 1);
        for (int i = 1; i <= cuts; i++) s.Add(-h + r + inner * i / (cuts + 1));
        if (low) { s.Add(h - r); s.Add(h - 0.5f * r); s.Add(h); }
        else { s.Add(h - r); s.Add(h - 0.71f * r); s.Add(h - 0.29f * r); s.Add(h); }
        s.Sort();
        for (int i = s.Count - 1; i > 0; i--) if (s[i] - s[i - 1] < 1e-4f) s.RemoveAt(i);
        return s;
    }

    /// <summary>
    /// A chunky stone block with rounded edges, a slightly hand-made wobble and painted vertex colours
    /// (lighter top, darker toward the bottom). <paramref name="size"/> is the full size; the block sits centred on
    /// <paramref name="center"/> rotated by <paramref name="rotation"/>.
    /// </summary>
    public static void Block(MeshBuilder mb, Vector3 center, Vector3 size, Quaternion rotation, Color color, int seed,
        float roundness = 0.22f, float wobble = 0.05f)
    {
        var h = size * 0.5f;
        float r = Mathf.Min(Mathf.Min(h.x, h.y), h.z) * Mathf.Clamp(roundness, 0.02f, 0.95f) * 2f;
        r = Mathf.Min(r, Mathf.Min(Mathf.Min(h.x, h.y), h.z) * 0.95f);
        var inner = h - Vector3.one * r;
        float minSize = Mathf.Min(Mathf.Min(size.x, size.y), size.z);
        float step = Mathf.Max(2.5f, minSize * 0.45f) * Mathf.Max(1f, Coarseness);
        bool low = LowDetail.Contains(mb);
        var xs = AxisSteps(h.x, r, step, low); var ys = AxisSteps(h.y, r, step, low); var zs = AxisSteps(h.z, r, step, low);
        float noiseFreq = 1f / Mathf.Max(3f, minSize * 0.9f);
        var offset = new Vector3(seed * 3.17f, seed * 1.71f, seed * 2.33f);

        void Face(int axis, float sign)
        {
            int ua = (axis + 1) % 3, va = (axis + 2) % 3;
            var us = ua == 0 ? xs : ua == 1 ? ys : zs;
            var vs = va == 0 ? xs : va == 1 ? ys : zs;
            int start = mb.VertexCount;
            var outward = Vector3.zero; outward[axis] = sign;
            for (int j = 0; j < vs.Count; j++)
                for (int i = 0; i < us.Count; i++)
                {
                    var p = Vector3.zero;
                    p[axis] = sign * h[axis]; p[ua] = us[i]; p[va] = vs[j];
                    var c = new Vector3(Mathf.Clamp(p.x, -inner.x, inner.x), Mathf.Clamp(p.y, -inner.y, inner.y), Mathf.Clamp(p.z, -inner.z, inner.z));
                    var n = (p - c).normalized;
                    var q = c + n * r;
                    // hand-made wobble along the normal
                    float w = (Noise(q * noiseFreq + offset, seed) - 0.5f) * 2f * wobble * minSize;
                    q += n * w;
                    // painted shading: darker toward the bottom, lighter on top faces and upper edges
                    float t = Mathf.InverseLerp(-h.y, h.y, q.y);
                    float shade = Mathf.Lerp(0.78f, 1.0f, Mathf.Sqrt(t));
                    if (n.y > 0.55f) shade *= 1.1f;
                    else if (n.y < -0.5f) shade *= 0.6f;
                    // painted edge highlight on the upper rounded edges, a darker seam on the lower ones
                    float edge = 1f - Mathf.Max(Mathf.Abs(n.x), Mathf.Max(Mathf.Abs(n.y), Mathf.Abs(n.z)));
                    if (edge > 0.05f) shade *= n.y > 0.15f ? 1.12f : (n.y < -0.15f ? 0.85f : 1.04f);
                    shade *= 0.96f + 0.08f * Noise(q * noiseFreq * 3f + offset, seed + 7);
                    var col = new Color(color.r * shade, color.g * shade, color.b * shade, 1f);
                    mb.AddVertex(center + rotation * q, rotation * n, col, new Vector2(q.x, q.z));
                }
            for (int j = 0; j < vs.Count - 1; j++)
                for (int i = 0; i < us.Count - 1; i++)
                {
                    int a = start + j * us.Count + i, b = a + 1, c = a + us.Count, d = c + 1;
                    var o = rotation * outward;
                    mb.AddTriangle(a, b, c, o);
                    mb.AddTriangle(b, d, c, o);
                }
        }

        for (int axis = 0; axis < 3; axis++) { Face(axis, 1f); Face(axis, -1f); }
    }

    // ------------------------------------------------------------------ ground

    /// <summary>A height-field grid over [min,max] (xz) with per-vertex height and colour callbacks.</summary>
    public static Mesh Ground(Vector2 min, Vector2 max, float cell, Func<float, float, float> height, Func<float, float, Color> color, string name)
    {
        int nx = Mathf.CeilToInt((max.x - min.x) / cell) + 1, nz = Mathf.CeilToInt((max.y - min.y) / cell) + 1;
        var mb = new MeshBuilder();
        var hs = new float[nx, nz];
        for (int j = 0; j < nz; j++)
            for (int i = 0; i < nx; i++)
                hs[i, j] = height(min.x + i * cell, min.y + j * cell);
        for (int j = 0; j < nz; j++)
            for (int i = 0; i < nx; i++)
            {
                float x = min.x + i * cell, z = min.y + j * cell;
                float hl = hs[Mathf.Max(0, i - 1), j], hr = hs[Mathf.Min(nx - 1, i + 1), j];
                float hd = hs[i, Mathf.Max(0, j - 1)], hu = hs[i, Mathf.Min(nz - 1, j + 1)];
                var n = new Vector3(hl - hr, 2f * cell, hd - hu).normalized;
                mb.AddVertex(new Vector3(x, hs[i, j], z), n, color(x, z), new Vector2(x, z));
            }
        for (int j = 0; j < nz - 1; j++)
            for (int i = 0; i < nx - 1; i++)
            {
                int a = j * nx + i, b = a + 1, c = a + nx, d = c + 1;
                mb.AddTriangle(a, c, b, Vector3.up);
                mb.AddTriangle(b, c, d, Vector3.up);
            }
        return mb.Build(name);
    }

    // ------------------------------------------------------------------ water ribbon

    /// <summary>A flat ribbon following <paramref name="path"/>; uv.x across (0..1), uv.y along (distance / tile).</summary>
    public static Mesh Ribbon(IList<Vector3> path, Func<float, float> widthAt, float tile, string name)
    {
        var mb = new MeshBuilder();
        float dist = 0f;
        for (int i = 0; i < path.Count; i++)
        {
            var fwd = (path[Mathf.Min(i + 1, path.Count - 1)] - path[Mathf.Max(i - 1, 0)]);
            var flat = new Vector3(fwd.x, 0f, fwd.z).normalized;
            var side = Vector3.Cross(Vector3.up, flat).normalized;
            if (i > 0) dist += Vector3.Distance(path[i], path[i - 1]);
            float t = path.Count > 1 ? (float)i / (path.Count - 1) : 0f;
            float w = widthAt(t) * 0.5f;
            var up = Vector3.Cross(flat, side) * -1f;
            if (up.y < 0) up = -up;
            mb.AddVertex(path[i] - side * w, Vector3.up, Color.white, new Vector2(0f, dist / tile));
            mb.AddVertex(path[i] + side * w, Vector3.up, Color.white, new Vector2(1f, dist / tile));
        }
        for (int i = 0; i < path.Count - 1; i++)
        {
            int a = i * 2, b = a + 1, c = a + 2, d = a + 3;
            mb.AddTriangle(a, c, b, Vector3.up);
            mb.AddTriangle(b, c, d, Vector3.up);
        }
        var mesh = mb.Build(name);
        mesh.RecalculateNormals();
        return mesh;
    }

    // ------------------------------------------------------------------ banner

    /// <summary>
    /// A hanging cloth banner (double-sided material), top edge centred on <paramref name="top"/>, facing
    /// <paramref name="facing"/>. Colours come from <paramref name="paint"/>(u, v) with u across, v from top (0) to bottom (1).
    /// The bottom is cut into a swallow-tail.
    /// </summary>
    public static void Banner(MeshBuilder mb, Vector3 top, Vector3 facing, float width, float height, Func<float, float, Color> paint, int seed)
    {
        const int nu = 14, nv = 26;
        var right = Vector3.Cross(Vector3.up, facing).normalized;
        int start = mb.VertexCount;
        for (int j = 0; j <= nv; j++)
            for (int i = 0; i <= nu; i++)
            {
                float u = (float)i / nu, v = (float)j / nv;
                float tail = 0.16f * (1f - Mathf.Abs(u * 2f - 1f));          // swallow-tail notch
                float vv = v * (1f - tail * Mathf.SmoothStep(0.7f, 1f, v) / Mathf.Max(0.001f, v));
                float wave = Mathf.Sin(u * 5.2f + seed) * 0.35f * vv + Mathf.Sin(vv * 4f + seed * 0.7f) * 0.25f * vv;
                var p = top + right * ((u - 0.5f) * width) + Vector3.down * (vv * height) + facing * wave;
                mb.AddVertex(p, facing, paint(u, v), new Vector2(u, v));
            }
        for (int j = 0; j < nv; j++)
            for (int i = 0; i < nu; i++)
            {
                int a = start + j * (nu + 1) + i, b = a + 1, c = a + nu + 1, d = c + 1;
                mb.AddTriangle(a, c, b, facing);
                mb.AddTriangle(b, c, d, facing);
            }
    }

    // ------------------------------------------------------------------ glowing crack

    /// <summary>A zig-zag glowing crack strip laid on a surface: points in world space, offset along <paramref name="normal"/>.</summary>
    public static void Crack(MeshBuilder mb, IList<Vector3> points, Vector3 normal, float width)
    {
        for (int i = 0; i < points.Count - 1; i++)
        {
            var a = points[i] + normal * 0.25f; var b = points[i + 1] + normal * 0.25f;
            var dir = (b - a).normalized;
            var side = Vector3.Cross(normal, dir).normalized;
            float wa = width * (i == 0 ? 0.35f : 1f), wb = width * (i == points.Count - 2 ? 0.35f : 1f);
            int s = mb.VertexCount;
            mb.AddVertex(a - side * wa * 0.5f, normal, Color.white, new Vector2(0, 0));
            mb.AddVertex(a + side * wa * 0.5f, normal, Color.white, new Vector2(1, 0));
            mb.AddVertex(b - side * wb * 0.5f, normal, Color.white, new Vector2(0, 1));
            mb.AddVertex(b + side * wb * 0.5f, normal, Color.white, new Vector2(1, 1));
            mb.AddTriangle(s, s + 2, s + 1, normal);
            mb.AddTriangle(s + 1, s + 2, s + 3, normal);
        }
    }
}
