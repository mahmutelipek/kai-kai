using System.Collections.Generic;
using Game.Simulation;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Milestone 1 test track, built at runtime from code: long downhill, one left curve, one right curve,
    /// several cones and one ramp. (Replaced by the procedural RoadGenerator in Milestone 2.)
    /// </summary>
    public sealed class TestRoad : MonoBehaviour
    {
        public const float RoadWidth = TestRoadLayout.RoadWidth;
        public const float ShoulderWidth = TestRoadLayout.ShoulderWidth;
        public const float Grade = TestRoadLayout.Grade;
        public const float RampDistance = TestRoadLayout.RampDistance;

        public RoadPath Path { get; private set; }

        public static TestRoad Build(Transform parent)
        {
            var go = new GameObject("TestRoad");
            go.transform.SetParent(parent, false);
            var road = go.AddComponent<TestRoad>();
            road.BuildInternal();
            return road;
        }

        void BuildInternal()
        {
            Path = TestRoadLayout.BuildPath();
            BuildStrip("Road", -RoadWidth * 0.5f, RoadWidth * 0.5f, 0f, MaterialLibrary.Road, SurfaceKind.Road);
            BuildStrip("ShoulderLeft", -RoadWidth * 0.5f - ShoulderWidth, -RoadWidth * 0.5f, -0.01f, MaterialLibrary.Grass, SurfaceKind.Offroad);
            BuildStrip("ShoulderRight", RoadWidth * 0.5f, RoadWidth * 0.5f + ShoulderWidth, -0.01f, MaterialLibrary.Grass, SurfaceKind.Offroad);
            BuildLine("EdgeLineLeft", -RoadWidth * 0.5f + 0.35f, 0.18f, MaterialLibrary.LineWhite, 0f);
            BuildLine("EdgeLineRight", RoadWidth * 0.5f - 0.35f, 0.18f, MaterialLibrary.LineWhite, 0f);
            BuildLine("CenterLine", 0f, 0.2f, MaterialLibrary.LineYellow, 4f);

            var obstacles = new GameObject("Obstacles").transform;
            obstacles.SetParent(transform, false);
            foreach (System.Numerics.Vector2 spot in TestRoadLayout.ConeSpots)
            {
                Path.Sample(spot.X, out System.Numerics.Vector3 p, out float yaw);
                ConeObstacle.Create(obstacles, p.ToUnity() + SimConvert.YawRight(yaw) * spot.Y + Vector3.up * 0.02f, yaw * Mathf.Rad2Deg);
            }
            BuildRamp(TestRoadLayout.RampDistance, TestRoadLayout.RampLateral);
        }

        void BuildStrip(string name, float innerOffset, float outerOffset, float yOffset, Color color, SurfaceKind kind)
        {
            var verts = new List<Vector3>(Path.Count * 2);
            var tris = new List<int>(Path.Count * 6);
            for (int i = 0; i < Path.Count; i++)
            {
                Vector3 c = Path.PointAt(i).ToUnity() + Vector3.up * yOffset;
                Vector3 r = SimConvert.YawRight(Path.YawAt(i));
                verts.Add(c + r * innerOffset);
                verts.Add(c + r * outerOffset);
                if (i == 0) continue;
                int b = verts.Count - 4;
                tris.Add(b); tris.Add(b + 2); tris.Add(b + 1);
                tris.Add(b + 1); tris.Add(b + 2); tris.Add(b + 3);
            }
            var mesh = new Mesh { name = name };
            if (verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            GameObject go = PrimitiveFactory.MeshObject(name, mesh, color, transform);
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            go.AddComponent<GroundSurface>().kind = kind;
        }

        /// <summary>Visual-only line strip. dashLength 0 = solid.</summary>
        void BuildLine(string name, float offset, float width, Color color, float dashLength)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            for (int i = 1; i < Path.Count; i++)
            {
                if (dashLength > 0f && Mathf.FloorToInt(Path.DistanceAt(i) / dashLength) % 2 == 1) continue;
                Vector3 a = Path.PointAt(i - 1).ToUnity() + Vector3.up * 0.015f, b = Path.PointAt(i).ToUnity() + Vector3.up * 0.015f;
                Vector3 ra = SimConvert.YawRight(Path.YawAt(i - 1)), rb = SimConvert.YawRight(Path.YawAt(i));
                int v = verts.Count;
                verts.Add(a + ra * (offset - width * 0.5f));
                verts.Add(a + ra * (offset + width * 0.5f));
                verts.Add(b + rb * (offset - width * 0.5f));
                verts.Add(b + rb * (offset + width * 0.5f));
                tris.Add(v); tris.Add(v + 2); tris.Add(v + 1);
                tris.Add(v + 1); tris.Add(v + 2); tris.Add(v + 3);
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            PrimitiveFactory.MeshObject(name, mesh, color, transform);
        }

        void BuildRamp(float distance, float lateral)
        {
            Path.Sample(distance, out System.Numerics.Vector3 p, out float yaw);
            float w = TestRoadLayout.RampWidth, l = TestRoadLayout.RampLength, h = TestRoadLayout.RampHeight;
            var go = PrimitiveFactory.MeshObject("Ramp", PrimitiveFactory.CreateWedge(w, l, h), MaterialLibrary.RampWood, transform);
            go.transform.SetPositionAndRotation(
                p.ToUnity() + SimConvert.YawRight(yaw) * lateral + Vector3.up * 0.005f,
                Quaternion.Euler(Mathf.Atan(Grade) * Mathf.Rad2Deg, yaw * Mathf.Rad2Deg, 0f));
            go.AddComponent<MeshCollider>().sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
            go.AddComponent<GroundSurface>().kind = SurfaceKind.Road;
            for (int i = 0; i < 3; i++) // chevrons so it reads as a ramp from the camera
            {
                float z = l * (0.22f + i * 0.26f);
                float y = h * z / l + 0.03f;
                GameObject stripe = PrimitiveFactory.Visual(PrimitiveType.Cube, go.transform, new Vector3(0f, y, z), new Vector3(3.2f, 0.04f, 0.5f), MaterialLibrary.LineYellow, "Chevron");
                stripe.transform.localRotation = Quaternion.Euler(-Mathf.Atan2(h, l) * Mathf.Rad2Deg, 0f, 0f);
            }
        }

        void OnDrawGizmosSelected()
        {
            if (Path == null) return;
            Gizmos.color = Color.cyan;
            for (int i = 1; i < Path.Count; i++) Gizmos.DrawLine(Path.PointAt(i - 1).ToUnity(), Path.PointAt(i).ToUnity());
        }
    }
}
