using System;
using System.Collections.Generic;
using System.Numerics;

namespace Game.Simulation
{
    /// <summary>Shape parameters of one chunk instance (all distances local to the chunk).</summary>
    public struct ChunkShape
    {
        public float Length;
        /// <summary>Signed total turn in radians (+ = right). For S-curves: the first bend.</summary>
        public float Turn;
        public bool SCurve;
        public float Grade;
        public float BaseWidth;

        /// <summary>Optional width change in the middle (narrow section, fork, intersection). 0 = none.</summary>
        public float SpecialWidth;
        public float SpecialStart, SpecialEnd;

        public EdgeKind DefaultEdge;
        /// <summary>Optional edge override between EdgeStart and EdgeEnd (walls in tunnels / narrow sections, void on bridges).</summary>
        public EdgeKind SpecialEdge;
        public float EdgeStart, EdgeEnd;
    }

    /// <summary>
    /// One piece of road: a sampled centerline (every ~2 m) with per-sample heading, half width and edge kinds.
    /// Pooled and rebuilt in place; lists keep their capacity so steady-state generation does not allocate.
    /// </summary>
    public sealed class RoadChunk
    {
        public const float SampleSpacing = 2f;
        public const float WidthBlendLength = 25f;

        public int Serial;
        public ChunkDefinition Definition;
        public ChunkKind Kind => Definition.Kind;
        public bool Mirrored;
        public float Difficulty;
        public ChunkShape Shape;
        public RoadSocket Entry;
        public RoadSocket Exit;

        public readonly List<Vector3> Points = new List<Vector3>(160);
        public readonly List<float> Yaws = new List<float>(160);
        public readonly List<float> HalfWidths = new List<float>(160);
        public readonly List<EdgeKind> LeftEdges = new List<EdgeKind>(160);
        public readonly List<EdgeKind> RightEdges = new List<EdgeKind>(160);
        public readonly List<RampFeature> Ramps = new List<RampFeature>(2);
        public readonly List<int> ObstacleSlots = new List<int>(32);
        /// <summary>Pool generation per slot: a slot freed early (knocked, traffic left) may be reused by another chunk.</summary>
        public readonly List<int> ObstacleGenerations = new List<int>(32);
        /// <summary>Reachable, obstacle-free lateral line per sample (filled by RoadPlanner).</summary>
        public readonly List<float> PlannedLateral = new List<float>(160);

        /// <summary>Local ranges with a roof (tunnel) for visuals.</summary>
        public float RoofStart, RoofEnd;

        public float StartAlong => Entry.Along;
        public float EndAlong => Exit.Along;
        public float Length => Shape.Length;
        public int SampleCount => Points.Count;

        public bool Owns(ObstacleField field, int k) =>
            field.Items[ObstacleSlots[k]].Active && field.Items[ObstacleSlots[k]].Generation == ObstacleGenerations[k];

        public float LocalAlongAt(int i) => Math.Min(i * SampleSpacing, Shape.Length);

        public void Build(int serial, ChunkDefinition definition, bool mirrored, float difficulty, RoadSocket entry, in ChunkShape shape)
        {
            Serial = serial;
            Definition = definition;
            Mirrored = mirrored;
            Difficulty = difficulty;
            Shape = shape;
            Entry = entry;
            Points.Clear(); Yaws.Clear(); HalfWidths.Clear(); LeftEdges.Clear(); RightEdges.Clear();
            Ramps.Clear();
            ObstacleSlots.Clear();
            ObstacleGenerations.Clear();
            PlannedLateral.Clear();
            RoofStart = RoofEnd = 0f;

            int steps = Math.Max(1, (int)MathF.Ceiling(shape.Length / SampleSpacing - 1e-4f));
            Vector3 pos = entry.Position;
            float prevA = 0f;
            AddSample(pos, 0f, entry);
            for (int i = 1; i <= steps; i++)
            {
                float a = Math.Min(i * SampleSpacing, shape.Length);
                float midYaw = YawAt(0.5f * (prevA + a));
                pos += SimMath.Forward3(midYaw) * (a - prevA);
                pos.Y = entry.Position.Y - shape.Grade * a;
                AddSample(pos, a, entry);
                prevA = a;
            }

            int last = Points.Count - 1;
            Exit = new RoadSocket
            {
                Position = Points[last],
                Yaw = Yaws[last],
                Width = HalfWidths[last] * 2f,
                Along = entry.Along + shape.Length,
            };
        }

        void AddSample(Vector3 pos, float a, in RoadSocket entry)
        {
            Points.Add(pos);
            Yaws.Add(YawAt(a));
            HalfWidths.Add(WidthAt(a, entry.Width) * 0.5f);
            EdgeKind edge = a >= Shape.EdgeStart && a <= Shape.EdgeEnd && Shape.EdgeEnd > Shape.EdgeStart ? Shape.SpecialEdge : Shape.DefaultEdge;
            LeftEdges.Add(edge);
            RightEdges.Add(edge);
        }

        /// <summary>Heading at a local distance. Curvature eases in and out (smoothstep), so joins have zero curvature.</summary>
        public float YawAt(float a)
        {
            float L = Shape.Length;
            float lead = L * 0.12f;
            if (!Shape.SCurve)
                return Entry.Yaw + Shape.Turn * SimMath.SmoothStep(lead, L - lead, a);
            float mid = L * 0.5f;
            return Entry.Yaw + Shape.Turn * SimMath.SmoothStep(lead, mid, a) - Shape.Turn * SimMath.SmoothStep(mid, L - lead, a);
        }

        float WidthAt(float a, float entryWidth)
        {
            float w = Shape.BaseWidth + (entryWidth - Shape.BaseWidth) * (1f - SimMath.SmoothStep(0f, WidthBlendLength, a));
            if (Shape.SpecialWidth > 0f && Shape.SpecialEnd > Shape.SpecialStart)
            {
                float blend = SimMath.SmoothStep(Shape.SpecialStart - 20f, Shape.SpecialStart, a) *
                              (1f - SimMath.SmoothStep(Shape.SpecialEnd, Shape.SpecialEnd + 20f, a));
                w += (Shape.SpecialWidth - w) * blend;
            }
            return w;
        }

        /// <summary>Interpolated pose at a local distance.</summary>
        public void SampleLocal(float a, out Vector3 position, out float yaw, out float halfWidth)
        {
            a = SimMath.Clamp(a, 0f, Shape.Length);
            int i = Math.Min((int)(a / SampleSpacing), Points.Count - 2);
            float a0 = LocalAlongAt(i), a1 = LocalAlongAt(i + 1);
            float t = a1 > a0 ? (a - a0) / (a1 - a0) : 0f;
            position = Vector3.Lerp(Points[i], Points[i + 1], t);
            yaw = Yaws[i] + SimMath.WrapAngle(Yaws[i + 1] - Yaws[i]) * t;
            halfWidth = HalfWidths[i] + (HalfWidths[i + 1] - HalfWidths[i]) * t;
        }

        public int SampleIndexAt(float a) => Math.Max(0, Math.Min((int)MathF.Round(SimMath.Clamp(a, 0f, Shape.Length) / SampleSpacing), Points.Count - 1));

        /// <summary>Closest point on this chunk's centerline. Returns squared XZ distance; fills local along and lateral.</summary>
        public float ProjectLocal(float x, float z, out float localAlong, out float lateral, out float centerY, out int segment)
        {
            float best = float.PositiveInfinity;
            localAlong = 0f; lateral = 0f; centerY = Points[0].Y; segment = 0;
            var p = new Vector2(x, z);
            for (int i = 0; i < Points.Count - 1; i++)
            {
                var a = new Vector2(Points[i].X, Points[i].Z);
                var b = new Vector2(Points[i + 1].X, Points[i + 1].Z);
                Vector2 ab = b - a;
                float len2 = ab.LengthSquared();
                float t = len2 > 1e-6f ? SimMath.Clamp01(Vector2.Dot(p - a, ab) / len2) : 0f;
                Vector2 c = a + ab * t;
                float d2 = (c - p).LengthSquared();
                if (d2 < best)
                {
                    best = d2;
                    segment = i;
                    localAlong = LocalAlongAt(i) + (LocalAlongAt(i + 1) - LocalAlongAt(i)) * t;
                    float yaw = Yaws[i] + SimMath.WrapAngle(Yaws[i + 1] - Yaws[i]) * t;
                    Vector3 right = SimMath.Right3(yaw);
                    lateral = (x - c.X) * right.X + (z - c.Y) * right.Z;
                    centerY = Points[i].Y + (Points[i + 1].Y - Points[i].Y) * t;
                }
            }
            return best;
        }

        /// <summary>World position of a road-frame point (local along, lateral offset).</summary>
        public Vector3 WorldPoint(float localAlong, float lateral, out float yaw)
        {
            SampleLocal(localAlong, out Vector3 c, out yaw, out _);
            return c + SimMath.Right3(yaw) * lateral;
        }
    }
}
