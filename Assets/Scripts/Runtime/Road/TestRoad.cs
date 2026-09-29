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
        public const float RoadWidth = 12f;
        public const float ShoulderWidth = 25f;
        public const float Grade = 0.06f;
        public const float SampleSpacing = 2f;
        public const float RampDistance = 760f;

        struct Segment
        {
            public float Length;
            public float TurnDeg; // positive = right
            public Segment(float length, float turnDeg) { Length = length; TurnDeg = turnDeg; }
        }

        static readonly Segment[] Layout =
        {
            new Segment(160f, 0f),
            new Segment(110f, -55f),   // left curve (radius ~115 m)
            new Segment(160f, 0f),
            new Segment(110f, 55f),    // right curve
            new Segment(1800f, 0f),    // long straight with cones and the ramp
        };

        // (distance along road, lateral offset in meters; + = right)
        static readonly Vector2[] ConeSpots =
        {
            new Vector2(90f, -2.5f), new Vector2(100f, 2.5f), new Vector2(120f, 0f),
            new Vector2(215f, 3.5f), new Vector2(390f, -3.5f),
            new Vector2(620f, -2f), new Vector2(630f, 0f), new Vector2(640f, 2f),
            new Vector2(900f, -4f), new Vector2(920f, 4f), new Vector2(940f, -1f),
        };

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
            Path = BuildPath();
            BuildStrip("Road", -RoadWidth * 0.5f, RoadWidth * 0.5f, 0f, MaterialLibrary.Road, SurfaceKind.Road);
            BuildStrip("ShoulderLeft", -RoadWidth * 0.5f - ShoulderWidth, -RoadWidth * 0.5f, -0.01f, MaterialLibrary.Grass, SurfaceKind.Offroad);
            BuildStrip("ShoulderRight", RoadWidth * 0.5f, RoadWidth * 0.5f + ShoulderWidth, -0.01f, MaterialLibrary.Grass, SurfaceKind.Offroad);
            BuildLine("EdgeLineLeft", -RoadWidth * 0.5f + 0.35f, 0.18f, MaterialLibrary.LineWhite, 0f);
            BuildLine("EdgeLineRight", RoadWidth * 0.5f - 0.35f, 0.18f, MaterialLibrary.LineWhite, 0f);
            BuildLine("CenterLine", 0f, 0.2f, MaterialLibrary.LineYellow, 4f);

            var obstacles = new GameObject("Obstacles").transform;
            obstacles.SetParent(transform, false);
            foreach (Vector2 spot in ConeSpots)
            {
                Path.Sample(spot.x, out Vector3 p, out float yaw);
                ConeObstacle.Create(obstacles, p + SimConvert.YawRight(yaw) * spot.y + Vector3.up * 0.02f, yaw * Mathf.Rad2Deg);
            }
            BuildRamp(RampDistance, 0f);
        }

        static RoadPath BuildPath()
        {
            var path = new RoadPath(RoadWidth);
            Vector3 pos = Vector3.zero;
            float yaw = 0f, distance = 0f;
            path.Add(pos, yaw);
            foreach (Segment seg in Layout)
            {
                int steps = Mathf.Max(1, Mathf.RoundToInt(seg.Length / SampleSpacing));
                float step = seg.Length / steps;
                float turnPerStep = seg.TurnDeg * Mathf.Deg2Rad / steps;
                for (int i = 0; i < steps; i++)
                {
                    float midYaw = yaw + turnPerStep * 0.5f;
                    pos += SimConvert.YawForward(midYaw) * step;
                    yaw += turnPerStep;
                    distance += step;
                    pos.y = -Grade * distance;
                    path.Add(pos, yaw);
                }
            }
            return path;
        }

        void BuildStrip(string name, float innerOffset, float outerOffset, float yOffset, Color color, SurfaceKind kind)
        {
            var verts = new List<Vector3>(Path.Count * 2);
            var tris = new List<int>(Path.Count * 6);
            for (int i = 0; i < Path.Count; i++)
            {
                Vector3 c = Path.PointAt(i) + Vector3.up * yOffset;
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
                Vector3 a = Path.PointAt(i - 1) + Vector3.up * 0.015f, b = Path.PointAt(i) + Vector3.up * 0.015f;
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
            Path.Sample(distance, out Vector3 p, out float yaw);
            var go = PrimitiveFactory.MeshObject("Ramp", PrimitiveFactory.CreateWedge(5f, 8f, 1.3f), MaterialLibrary.RampWood, transform);
            go.transform.SetPositionAndRotation(
                p + SimConvert.YawRight(yaw) * lateral + Vector3.up * 0.005f,
                Quaternion.Euler(Mathf.Atan(Grade) * Mathf.Rad2Deg, yaw * Mathf.Rad2Deg, 0f));
            go.AddComponent<MeshCollider>().sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
            go.AddComponent<GroundSurface>().kind = SurfaceKind.Road;
            for (int i = 0; i < 3; i++) // chevrons so it reads as a ramp from the camera
            {
                float z = 1.8f + i * 2.1f;
                float y = 1.3f * z / 8f + 0.03f;
                GameObject stripe = PrimitiveFactory.Visual(PrimitiveType.Cube, go.transform, new Vector3(0f, y, z), new Vector3(3.2f, 0.04f, 0.5f), MaterialLibrary.LineYellow, "Chevron");
                stripe.transform.localRotation = Quaternion.Euler(-Mathf.Atan2(1.3f, 8f) * Mathf.Rad2Deg, 0f, 0f);
            }
        }

        void OnDrawGizmosSelected()
        {
            if (Path == null) return;
            Gizmos.color = Color.cyan;
            for (int i = 1; i < Path.Count; i++) Gizmos.DrawLine(Path.PointAt(i - 1), Path.PointAt(i));
        }
    }
}
