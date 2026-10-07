using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    /// <summary>Cached curved fronds with separate tapered leaflets instead of solid umbrella triangles.</summary>
    public static class StylizedPalm
    {
        static Mesh _trunk, _fronds;
        public static void Create(Transform parent, Vector3 position, float yaw, float scale)
        {
            if (_trunk == null) BuildMeshes();
            var root = new GameObject("Frond Palm").transform; root.SetParent(parent, false);
            root.SetPositionAndRotation(position, Quaternion.Euler(0,yaw,0)); root.localScale = Vector3.one * scale;
            PrimitiveFactory.MeshObject("Curved trunk", _trunk, new Color(.44f,.29f,.14f), root);
            PrimitiveFactory.MeshObject("Palm leaflets", _fronds, new Color(.28f,.56f,.19f), root);
        }
        static Vector3 Stem(float t) => new Vector3(t*t*.65f, t*7f, 0);
        static void BuildMeshes()
        {
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            for (int ring = 0; ring <= 12; ring++)
            {
                float t = ring / 12f, radius = Mathf.Lerp(.23f,.12f,t) * (ring % 2 == 0 ? 1 : .93f);
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.PI / 4;
                    vertices.Add(Stem(t) + new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius));
                    if (ring == 0) continue;
                    int v = ring * 8 + i, next = ring * 8 + (i+1)%8;
                    triangles.AddRange(new[] {v-8,v,next,v-8,next,next-8});
                }
            }
            _trunk = Mesh("Palm trunk",vertices,triangles); vertices.Clear(); triangles.Clear();
            for (int frond = 0; frond < 10; frond++)
            {
                float a = frond * Mathf.PI * 2 / 10;
                Vector3 outward = new Vector3(Mathf.Cos(a),0,Mathf.Sin(a)), side = new Vector3(-outward.z,0,outward.x);
                float length = 2.6f + frond%3*.25f;
                for (int segment = 0; segment < 12; segment++)
                {
                    float t0 = segment / 12f, t1 = (segment + 1) / 12f;
                    Vector3 a0 = Stem(1) + outward * (t0*length) + Vector3.up * (Mathf.Sin(t0*Mathf.PI)*.8f-t0*t0*1.35f);
                    Vector3 a1 = Stem(1) + outward * (t1*length) + Vector3.up * (Mathf.Sin(t1*Mathf.PI)*.8f-t1*t1*1.35f);
                    float w0 = Mathf.Lerp(.1f,.015f,t0), w1 = Mathf.Lerp(.1f,.015f,t1);
                    int v = vertices.Count;
                    vertices.Add(a0-side*w0); vertices.Add(a0+side*w0); vertices.Add(a1-side*w1); vertices.Add(a1+side*w1);
                    triangles.AddRange(new[] {v,v+2,v+1,v+1,v+2,v+3});
                    for (int j = 0; j < 4; j++) vertices.Add(vertices[v+j]);
                    triangles.AddRange(new[] {v+4,v+5,v+6,v+5,v+7,v+6});
                }
                for (int segment = 1; segment <= 11; segment++)
                {
                    float t = segment / 12f;
                    Vector3 origin = Stem(1) + outward * (t*length) + Vector3.up * (Mathf.Sin(t*Mathf.PI)*.8f-t*t*1.35f);
                    float width = Mathf.Sin(t*Mathf.PI) * .65f;
                    for (int sign = -1; sign <= 1; sign += 2)
                    {
                        Vector3 tip = origin + side * (sign*width) + outward * .35f + Vector3.down * (.12f+t*.15f);
                        int v = vertices.Count;
                        vertices.Add(origin-outward*.12f); vertices.Add(tip); vertices.Add(origin+outward*.15f);
                        // Both sides are present so the crown reads from below and above.
                        triangles.AddRange(new[] {v,v+1,v+2});
                        vertices.Add(vertices[v+2]); vertices.Add(vertices[v+1]); vertices.Add(vertices[v]);
                        triangles.AddRange(new[] {v+3,v+4,v+5});
                    }
                }
            }
            _fronds = Mesh("Palm fronds",vertices,triangles);
        }
        static Mesh Mesh(string name, List<Vector3> vertices, List<int> triangles)
        {
            var mesh = new Mesh {name=name}; mesh.SetVertices(vertices); mesh.SetTriangles(triangles,0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
    }
}
