using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game
{
    /// <summary>
    /// Accumulates triangles in reusable lists and uploads them into a reused Mesh, so rebuilding pooled road
    /// chunks does not allocate. Boxes get their own vertices per face (flat shading).
    /// </summary>
    public sealed class MeshWriter
    {
        readonly List<Vector3> _vertices = new List<Vector3>(4096);
        readonly List<int> _triangles = new List<int>(8192);

        public int VertexCount => _vertices.Count;

        public void Clear()
        {
            _vertices.Clear();
            _triangles.Clear();
        }

        /// <summary>Quad from four corners in clockwise order as seen from the front.</summary>
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int i = _vertices.Count;
            _vertices.Add(a); _vertices.Add(b); _vertices.Add(c); _vertices.Add(d);
            _triangles.Add(i); _triangles.Add(i + 1); _triangles.Add(i + 2);
            _triangles.Add(i); _triangles.Add(i + 2); _triangles.Add(i + 3);
        }

        public void Triangle(Vector3 a, Vector3 b, Vector3 c)
        {
            int i = _vertices.Count;
            _vertices.Add(a); _vertices.Add(b); _vertices.Add(c);
            _triangles.Add(i); _triangles.Add(i + 1); _triangles.Add(i + 2);
        }

        /// <summary>Oriented box (center, rotation, full size).</summary>
        public void Box(Vector3 center, Quaternion rotation, Vector3 size)
        {
            Vector3 x = rotation * new Vector3(size.x * 0.5f, 0f, 0f);
            Vector3 y = rotation * new Vector3(0f, size.y * 0.5f, 0f);
            Vector3 z = rotation * new Vector3(0f, 0f, size.z * 0.5f);
            Vector3 c = center;
            Quad(c - x + y - z, c - x + y + z, c + x + y + z, c + x + y - z); // top
            Quad(c - x - y + z, c - x - y - z, c + x - y - z, c + x - y + z); // bottom
            Quad(c - x - y - z, c - x + y - z, c + x + y - z, c + x - y - z); // back (-z)
            Quad(c + x - y + z, c + x + y + z, c - x + y + z, c - x - y + z); // front (+z)
            Quad(c - x - y + z, c - x + y + z, c - x + y - z, c - x - y - z); // left
            Quad(c + x - y - z, c + x + y - z, c + x + y + z, c + x - y + z); // right
        }

        /// <summary>Ramp wedge: low edge at <paramref name="origin"/>, rising along <paramref name="forward"/>.</summary>
        public void Wedge(Vector3 origin, Quaternion rotation, float width, float length, float height)
        {
            Vector3 r = rotation * Vector3.right * (width * 0.5f);
            Vector3 f = rotation * Vector3.forward * length;
            Vector3 u = Vector3.up * height;
            Vector3 a = origin - r, b = origin + r, c = a + f, d = b + f, e = c + u, g = d + u;
            Quad(a, e, g, b);  // slope
            Quad(d, g, e, c);  // back face
            Triangle(a, c, e); // left
            Triangle(b, g, d); // right
        }

        public void Upload(Mesh mesh)
        {
            mesh.Clear();
            if (_vertices.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(_vertices);
            mesh.SetTriangles(_triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }
    }
}
