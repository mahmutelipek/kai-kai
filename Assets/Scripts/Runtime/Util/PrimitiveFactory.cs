using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    /// <summary>Placeholder art from Unity primitives and tiny procedural meshes (Milestone 1: primitives only).</summary>
    public static class PrimitiveFactory
    {
        /// <summary>Creates a primitive child without a collider (visual only).</summary>
        public static GameObject Visual(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 localScale, Color color, string name = null)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name ?? type.ToString();
            if (Application.isPlaying) Object.Destroy(go.GetComponent<Collider>());
            else Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            go.GetComponent<Renderer>().sharedMaterial = MaterialLibrary.Get(color);
            return go;
        }

        public static GameObject MeshObject(string name, Mesh mesh, Color color, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = MaterialLibrary.Get(color);
            return go;
        }

        /// <summary>Cone with its base on y=0.</summary>
        public static Mesh CreateCone(float radius, float height, int segments = 16)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            // sides: separate apex per segment for flat-ish shading
            for (int i = 0; i < segments; i++)
            {
                float a0 = i / (float)segments * Mathf.PI * 2f;
                float a1 = (i + 1) / (float)segments * Mathf.PI * 2f;
                int b = verts.Count;
                verts.Add(new Vector3(Mathf.Sin(a0) * radius, 0f, Mathf.Cos(a0) * radius));
                verts.Add(new Vector3(0f, height, 0f));
                verts.Add(new Vector3(Mathf.Sin(a1) * radius, 0f, Mathf.Cos(a1) * radius));
                tris.Add(b); tris.Add(b + 2); tris.Add(b + 1);
            }
            int center = verts.Count;
            verts.Add(Vector3.zero);
            for (int i = 0; i < segments; i++)
            {
                float a0 = i / (float)segments * Mathf.PI * 2f;
                float a1 = (i + 1) / (float)segments * Mathf.PI * 2f;
                int b = verts.Count;
                verts.Add(new Vector3(Mathf.Sin(a0) * radius, 0f, Mathf.Cos(a0) * radius));
                verts.Add(new Vector3(Mathf.Sin(a1) * radius, 0f, Mathf.Cos(a1) * radius));
                tris.Add(center); tris.Add(b + 1); tris.Add(b);
            }
            var mesh = new Mesh { name = "Cone" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Ramp wedge: width along X, length along Z, rising from z=0 (height 0) to z=length (height h). Pivot at the low edge centre.</summary>
        public static Mesh CreateWedge(float width, float length, float height)
        {
            float w = width * 0.5f;
            Vector3 a = new Vector3(-w, 0f, 0f), b = new Vector3(w, 0f, 0f);
            Vector3 c = new Vector3(-w, 0f, length), d = new Vector3(w, 0f, length);
            Vector3 e = new Vector3(-w, height, length), f = new Vector3(w, height, length);
            var verts = new List<Vector3>();
            var tris = new List<int>();
            void Quad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
            {
                int i = verts.Count;
                verts.Add(p0); verts.Add(p1); verts.Add(p2); verts.Add(p3);
                tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
                tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
            }
            void Tri(Vector3 p0, Vector3 p1, Vector3 p2)
            {
                int i = verts.Count;
                verts.Add(p0); verts.Add(p1); verts.Add(p2);
                tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
            }
            Quad(a, e, f, b);      // sloped top
            Quad(d, f, e, c);      // back (vertical drop)
            Quad(a, b, d, c);      // bottom
            Tri(a, c, e);          // left side
            Tri(b, f, d);          // right side
            var mesh = new Mesh { name = "Wedge" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
