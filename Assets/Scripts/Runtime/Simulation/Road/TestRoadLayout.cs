using System;
using System.Numerics;

namespace Game.Simulation
{
    /// <summary>
    /// Milestone 1 test track definition (shared by the Unity scene and headless tests): long downhill,
    /// one left curve, one right curve, cones, one ramp. Replaced by the procedural generator in M2.
    /// </summary>
    public static class TestRoadLayout
    {
        public const float RoadWidth = 12f;
        public const float ShoulderWidth = 25f;
        public const float Grade = 0.06f;
        public const float SampleSpacing = 2f;

        public const float RampDistance = 760f;
        public const float RampLateral = 0f;
        public const float RampWidth = 5f;
        public const float RampLength = 8f;
        public const float RampHeight = 1.3f;

        public struct Segment
        {
            public float Length;
            public float TurnDeg; // positive = right
            public Segment(float length, float turnDeg) { Length = length; TurnDeg = turnDeg; }
        }

        public static readonly Segment[] Segments =
        {
            new Segment(160f, 0f),
            new Segment(110f, -55f),   // left curve (radius ~115 m)
            new Segment(160f, 0f),
            new Segment(110f, 55f),    // right curve
            new Segment(1800f, 0f),    // long straight with cones and the ramp
        };

        /// <summary>(distance along road, lateral offset in meters; + = right)</summary>
        public static readonly Vector2[] ConeSpots =
        {
            new Vector2(90f, -2.5f), new Vector2(100f, 2.5f), new Vector2(120f, 0f),
            new Vector2(215f, 3.5f), new Vector2(390f, -3.5f),
            new Vector2(620f, -2f), new Vector2(630f, 0f), new Vector2(640f, 2f),
            new Vector2(900f, -4f), new Vector2(920f, 4f), new Vector2(940f, -1f),
        };

        public static RoadPath BuildPath()
        {
            var path = new RoadPath(RoadWidth);
            Vector3 pos = Vector3.Zero;
            float yaw = 0f, distance = 0f;
            path.Add(pos, yaw);
            foreach (Segment seg in Segments)
            {
                int steps = Math.Max(1, (int)MathF.Round(seg.Length / SampleSpacing));
                float step = seg.Length / steps;
                float turnPerStep = seg.TurnDeg * SimMath.Deg2Rad / steps;
                for (int i = 0; i < steps; i++)
                {
                    pos += RoadPath.ForwardOf(yaw + turnPerStep * 0.5f) * step;
                    yaw += turnPerStep;
                    distance += step;
                    pos.Y = -Grade * distance;
                    path.Add(pos, yaw);
                }
            }
            return path;
        }

        /// <summary>Extra height of the ramp wedge at a road position (0 when not on the ramp).</summary>
        public static float RampHeightAt(float along, float lateral)
        {
            if (along < RampDistance || along > RampDistance + RampLength) return 0f;
            if (Math.Abs(lateral - RampLateral) > RampWidth * 0.5f) return 0f;
            return RampHeight * (along - RampDistance) / RampLength;
        }
    }

    /// <summary>
    /// Analytic ground for the test road (headless stand-in for the Unity raycasts): road, drivable grass
    /// shoulders (offroad), the ramp, and nothing beyond the shoulders (the board falls).
    /// </summary>
    public sealed class RoadGround : IGroundProvider
    {
        readonly RoadPath _path;
        int _hint;

        public RoadGround(RoadPath path) { _path = path; }

        public GroundSample Sample(float x, float z, float searchFromHeight)
        {
            float along = _path.Project(new Vector3(x, 0f, z), ref _hint, out float lateral, out float centerY);
            if (along <= 0f && z < _path.PointAt(0).Z - 1f) return GroundSample.Missing;
            float half = TestRoadLayout.RoadWidth * 0.5f;
            if (Math.Abs(lateral) > half + TestRoadLayout.ShoulderWidth) return GroundSample.Missing;
            SurfaceKind kind = Math.Abs(lateral) <= half ? SurfaceKind.Road : SurfaceKind.Offroad;
            float y = centerY + (kind == SurfaceKind.Offroad ? -0.01f : 0f) + TestRoadLayout.RampHeightAt(along, lateral);
            return y > searchFromHeight ? GroundSample.Missing : GroundSample.At(y, kind);
        }
    }
}
