using System;
using System.Collections.Generic;
using System.Numerics;

namespace Game.Art
{
    /// <summary>Vertices + triangles for one material. Unity winding: clockwise seen from the front.</summary>
    public sealed class MeshData
    {
        public readonly List<Vector3> Vertices = new List<Vector3>(1024);
        public readonly List<int> Triangles = new List<int>(2048);

        public int VertexCount => Vertices.Count;

        public void Clear()
        {
            Vertices.Clear();
            Triangles.Clear();
        }

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int i = Vertices.Count;
            Vertices.Add(a); Vertices.Add(b); Vertices.Add(c); Vertices.Add(d);
            Triangles.Add(i); Triangles.Add(i + 1); Triangles.Add(i + 2);
            Triangles.Add(i); Triangles.Add(i + 2); Triangles.Add(i + 3);
        }

        public void Triangle(Vector3 a, Vector3 b, Vector3 c)
        {
            int i = Vertices.Count;
            Vertices.Add(a); Vertices.Add(b); Vertices.Add(c);
            Triangles.Add(i); Triangles.Add(i + 1); Triangles.Add(i + 2);
        }

        /// <summary>Oriented box: centre, rotation, full size. Separate vertices per face (flat shading).</summary>
        public void Box(Vector3 center, Quaternion rotation, Vector3 size)
        {
            Vector3 x = Vector3.Transform(new Vector3(size.X * 0.5f, 0f, 0f), rotation);
            Vector3 y = Vector3.Transform(new Vector3(0f, size.Y * 0.5f, 0f), rotation);
            Vector3 z = Vector3.Transform(new Vector3(0f, 0f, size.Z * 0.5f), rotation);
            Vector3 c = center;
            Quad(c - x + y - z, c - x + y + z, c + x + y + z, c + x + y - z); // top
            Quad(c - x - y + z, c - x - y - z, c + x - y - z, c + x - y + z); // bottom
            Quad(c - x - y - z, c - x + y - z, c + x + y - z, c + x - y - z); // back (-z)
            Quad(c + x - y + z, c + x + y + z, c - x + y + z, c - x - y + z); // front (+z)
            Quad(c - x - y + z, c - x + y + z, c - x + y - z, c - x - y - z); // left
            Quad(c + x - y - z, c + x + y - z, c + x + y + z, c + x - y + z); // right
        }

        public void Box(Vector3 center, Vector3 size) => Box(center, Quaternion.Identity, size);

        /// <summary>Ramp wedge: low edge at <paramref name="origin"/> (centre of the low edge), rising along local +Z.</summary>
        public void Wedge(Vector3 origin, Quaternion rotation, float width, float length, float height)
        {
            Vector3 r = Vector3.Transform(Vector3.UnitX, rotation) * (width * 0.5f);
            Vector3 f = Vector3.Transform(Vector3.UnitZ, rotation) * length;
            Vector3 u = Vector3.Transform(Vector3.UnitY, rotation) * height;
            Vector3 a = origin - r, b = origin + r, c = a + f, d = b + f, e = c + u, g = d + u;
            Quad(a, e, g, b);  // slope
            Quad(d, g, e, c);  // back face
            Triangle(a, c, e); // left
            Triangle(b, g, d); // right
        }

        /// <summary>Low-poly cylinder along the rotated Y axis (smooth sides, flat caps).</summary>
        public void Cylinder(Vector3 center, Quaternion rotation, float radius, float height, int sides = 0, float topRadius = -1f)
        {
            if (topRadius < 0f) topRadius = radius;
            if (sides <= 0) sides = radius < 0.12f ? 8 : radius < 0.5f ? 12 : 20;
            Vector3 up = Vector3.Transform(Vector3.UnitY, rotation) * (height * 0.5f);
            Vector3 ax = Vector3.Transform(Vector3.UnitX, rotation), az = Vector3.Transform(Vector3.UnitZ, rotation);
            int start = Vertices.Count;
            for (int i = 0; i <= sides; i++)
            {
                float a = i / (float)sides * MathF.PI * 2f;
                Vector3 dir = ax * MathF.Sin(a) + az * MathF.Cos(a);
                Vertices.Add(center - up + dir * radius);
                Vertices.Add(center + up + dir * topRadius);
            }
            for (int i = 0; i < sides; i++)
            {
                int b = start + i * 2;
                Triangles.Add(b); Triangles.Add(b + 3); Triangles.Add(b + 1);
                Triangles.Add(b); Triangles.Add(b + 2); Triangles.Add(b + 3);
            }
            // caps
            int topCenter = Vertices.Count;
            Vertices.Add(center + up);
            int bottomCenter = Vertices.Count;
            Vertices.Add(center - up);
            int ring = Vertices.Count;
            for (int i = 0; i <= sides; i++)
            {
                float a = i / (float)sides * MathF.PI * 2f;
                Vector3 dir = ax * MathF.Sin(a) + az * MathF.Cos(a);
                Vertices.Add(center + up + dir * topRadius);
                Vertices.Add(center - up + dir * radius);
            }
            for (int i = 0; i < sides; i++)
            {
                int b = ring + i * 2;
                Triangles.Add(topCenter); Triangles.Add(b); Triangles.Add(b + 2);
                Triangles.Add(bottomCenter); Triangles.Add(b + 3); Triangles.Add(b + 1);
            }
        }

        /// <summary>Flat-shaded double pyramid (gem) with full size <paramref name="size"/>.</summary>
        public void Octahedron(Vector3 center, Quaternion rotation, Vector3 size, int sides = 6)
        {
            Vector3 top = center + Vector3.Transform(new Vector3(0f, size.Y * 0.5f, 0f), rotation);
            Vector3 bottom = center - Vector3.Transform(new Vector3(0f, size.Y * 0.5f, 0f), rotation);
            for (int i = 0; i < sides; i++)
            {
                float a0 = i / (float)sides * MathF.PI * 2f, a1 = (i + 1) / (float)sides * MathF.PI * 2f;
                Vector3 p0 = center + Vector3.Transform(new Vector3(MathF.Sin(a0) * size.X * 0.5f, 0f, MathF.Cos(a0) * size.Z * 0.5f), rotation);
                Vector3 p1 = center + Vector3.Transform(new Vector3(MathF.Sin(a1) * size.X * 0.5f, 0f, MathF.Cos(a1) * size.Z * 0.5f), rotation);
                Triangle(p0, p1, top);
                Triangle(p0, bottom, p1);
            }
        }

        /// <summary>Low-poly ellipsoid (smooth shading) with full size <paramref name="size"/>.</summary>
        public void Sphere(Vector3 center, Quaternion rotation, Vector3 size, int rings = 0, int segments = 0)
        {
            if (rings <= 0)
            {
                float r = Math.Max(size.X, Math.Max(size.Y, size.Z)) * 0.5f;
                rings = r < 0.08f ? 4 : r < 0.4f ? 7 : 9;
                segments = r < 0.08f ? 6 : r < 0.4f ? 12 : 14;
            }
            int start = Vertices.Count;
            for (int r = 0; r <= rings; r++)
            {
                float v = r / (float)rings * MathF.PI;
                for (int s = 0; s <= segments; s++)
                {
                    float u = s / (float)segments * MathF.PI * 2f;
                    var local = new Vector3(MathF.Sin(v) * MathF.Sin(u) * size.X * 0.5f, MathF.Cos(v) * size.Y * 0.5f, MathF.Sin(v) * MathF.Cos(u) * size.Z * 0.5f);
                    Vertices.Add(center + Vector3.Transform(local, rotation));
                }
            }
            int stride = segments + 1;
            for (int r = 0; r < rings; r++)
            for (int s = 0; s < segments; s++)
            {
                int a = start + r * stride + s, b = a + stride;
                Triangles.Add(a); Triangles.Add(b + 1); Triangles.Add(a + 1);
                Triangles.Add(a); Triangles.Add(b); Triangles.Add(b + 1);
            }
        }
    }

    /// <summary>A set of MeshData keyed by colour (one material each).</summary>
    public sealed class MeshSet
    {
        public readonly Dictionary<ArtColor, MeshData> ByColor = new Dictionary<ArtColor, MeshData>();

        public MeshData this[ArtColor color]
        {
            get
            {
                if (!ByColor.TryGetValue(color, out MeshData data)) ByColor[color] = data = new MeshData();
                return data;
            }
        }

        public void Clear()
        {
            foreach (MeshData d in ByColor.Values) d.Clear();
        }

        public int VertexCount
        {
            get { int n = 0; foreach (MeshData d in ByColor.Values) n += d.VertexCount; return n; }
        }

        /// <summary>Bakes an ArtModel (all groups at rest) into this set at a world pose.</summary>
        public void Add(ArtModel model, Vector3 position, Quaternion rotation, float scale = 1f) =>
            Add(model, position, rotation, scale, null, null);

        /// <summary>
        /// Bakes an ArtModel at a world pose. <paramref name="pose"/> optionally rotates named groups around their
        /// pivots (e.g. raised arms); <paramref name="onlyGroup"/> restricts output to one group's own parts.
        /// </summary>
        public void Add(ArtModel model, Vector3 position, Quaternion rotation, float scale, Func<string, Quaternion> pose, string onlyGroup)
        {
            foreach (ArtPart p in model.Parts)
            {
                if (onlyGroup != null && p.Group != onlyGroup) continue;
                GroupTransform(model, p.Group, pose, out Vector3 gPos, out Quaternion gRot);
                Vector3 c = position + Vector3.Transform((gPos + Vector3.Transform(p.Position, gRot)) * scale, rotation);
                Quaternion r = rotation * gRot * p.Rotation;
                AddShape(this[p.Color], p.Shape, c, r, p.Size * scale);
            }
        }

        /// <summary>Bakes only the parts of one group, in that group's own local space (pivot at the origin).</summary>
        public void AddGroupLocal(ArtModel model, string group)
        {
            foreach (ArtPart p in model.Parts)
                if (p.Group == group) AddShape(this[p.Color], p.Shape, p.Position, p.Rotation, p.Size);
        }

        /// <summary>Adds one primitive (centre, rotation, full size) to a mesh.</summary>
        public static void AddShape(MeshData m, ArtShape shape, Vector3 c, Quaternion r, Vector3 s)
        {
            switch (shape)
            {
                case ArtShape.Box: m.Box(c, r, s); break;
                case ArtShape.Sphere: m.Sphere(c, r, s); break;
                case ArtShape.Cylinder: m.Cylinder(c, r, s.X * 0.5f, s.Y); break;
                case ArtShape.Capsule:
                {
                    float body = Math.Max(0.01f, s.Y - s.X);
                    Vector3 up = Vector3.Transform(new Vector3(0f, body * 0.5f, 0f), r);
                    var cap = new Vector3(s.X, s.X, s.Z);
                    m.Cylinder(c, r, s.X * 0.5f, body);
                    m.Sphere(c + up, r, cap);
                    m.Sphere(c - up, r, cap);
                    break;
                }
                case ArtShape.Cone: m.Cylinder(c, r, s.X * 0.5f, s.Y, 0, 0.001f); break;
                case ArtShape.Wedge: m.Wedge(c - Vector3.Transform(new Vector3(0f, s.Y * 0.5f, s.Z * 0.5f), r), r, s.X, s.Z, s.Y); break;
                default: m.Octahedron(c, r, s); break;
            }
        }

        /// <summary>Model-space transform of a group (pivot chain, with optional posed rotations).</summary>
        public static void GroupTransform(ArtModel model, string group, Func<string, Quaternion> pose, out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.Zero;
            rotation = Quaternion.Identity;
            if (string.IsNullOrEmpty(group)) return;
            ArtGroup g = default;
            bool found = false;
            foreach (ArtGroup candidate in model.Groups) if (candidate.Name == group) { g = candidate; found = true; break; }
            if (!found) return;
            GroupTransform(model, g.Parent, pose, out Vector3 parentPos, out Quaternion parentRot);
            position = parentPos + Vector3.Transform(g.Pivot, parentRot);
            rotation = pose != null ? parentRot * pose(group) : parentRot;
        }
    }
}
