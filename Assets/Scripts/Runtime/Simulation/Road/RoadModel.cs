using System;
using System.Collections.Generic;
using System.Numerics;

namespace Game.Simulation
{
    /// <summary>
    /// The currently spawned stretch of road (chunks ordered by distance) and every query on it: projection,
    /// sampling, ground height (IGroundProvider for the board), edges, the planned line and the steer hint.
    /// </summary>
    public sealed class RoadModel : IGroundProvider
    {
        public const float ShoulderWidth = 20f;
        public const float VoidEdgeMargin = 0.25f;

        public readonly List<RoadChunk> Chunks = new List<RoadChunk>(16);
        int _groundHint;

        public float StartAlong => Chunks.Count == 0 ? 0f : Chunks[0].StartAlong;
        public float EndAlong => Chunks.Count == 0 ? 0f : Chunks[Chunks.Count - 1].EndAlong;

        public RoadChunk ChunkAt(float along)
        {
            for (int i = 0; i < Chunks.Count; i++)
                if (along < Chunks[i].EndAlong || i == Chunks.Count - 1) return Chunks[i];
            return null;
        }

        /// <summary>Pose at a global distance (clamped to the spawned stretch).</summary>
        public void Sample(float along, out Vector3 position, out float yaw, out float halfWidth)
        {
            RoadChunk c = ChunkAt(along);
            if (c == null) { position = Vector3.Zero; yaw = 0f; halfWidth = 6f; return; }
            c.SampleLocal(along - c.StartAlong, out position, out yaw, out halfWidth);
        }

        /// <summary>World point for a road-frame coordinate.</summary>
        public Vector3 WorldPoint(float along, float lateral, out float yaw)
        {
            Sample(along, out Vector3 c, out yaw, out _);
            return c + SimMath.Right3(yaw) * lateral;
        }

        /// <summary>Projects a world point onto the road. <paramref name="hint"/> caches the chunk serial for locality.</summary>
        public RoadProjection Project(float x, float z, ref int hint)
        {
            var result = new RoadProjection();
            if (Chunks.Count == 0) return result;

            int start = 0;
            for (int i = 0; i < Chunks.Count; i++) if (Chunks[i].Serial == hint) { start = i; break; }
            float best = float.PositiveInfinity;
            // local search around the hinted chunk first; widen only if that finds nothing close
            for (int pass = 0; pass < 2 && best > 400f; pass++)
            {
                int from = pass == 0 ? Math.Max(0, start - 1) : 0;
                int to = pass == 0 ? Math.Min(Chunks.Count - 1, start + 2) : Chunks.Count - 1;
                for (int i = from; i <= to; i++)
                {
                    RoadChunk c = Chunks[i];
                    float d2 = c.ProjectLocal(x, z, out float a, out float lat, out float cy, out int seg);
                    if (d2 >= best) continue;
                    best = d2;
                    result.Valid = true;
                    result.Chunk = c;
                    result.Along = c.StartAlong + a;
                    result.Lateral = lat;
                    result.CenterHeight = cy;
                    result.HalfWidth = c.HalfWidths[seg];
                    result.Yaw = c.Yaws[seg];
                    result.LeftEdge = c.LeftEdges[seg];
                    result.RightEdge = c.RightEdges[seg];
                }
            }
            if (result.Valid) hint = result.Chunk.Serial;
            return result;
        }

        public RoadProjection Project(Vector3 world, ref int hint) => Project(world.X, world.Z, ref hint);

        /// <summary>Ground for the board: asphalt, ramps, grass shoulders (offroad), nothing past a void edge.</summary>
        public GroundSample Sample(float x, float z, float searchFromHeight)
        {
            RoadProjection p = Project(x, z, ref _groundHint);
            if (!p.Valid) return GroundSample.Missing;
            RoadChunk c = p.Chunk;
            // before the first / after the last spawned chunk there is no road
            if (p.Along <= StartAlong + 1e-3f || p.Along >= EndAlong - 1e-3f)
            {
                Vector3 fwd = SimMath.Forward3(p.Yaw);
                Vector3 edgePoint = c.Points[p.Along <= StartAlong + 1e-3f ? 0 : c.SampleCount - 1];
                float beyond = (x - edgePoint.X) * fwd.X + (z - edgePoint.Z) * fwd.Z;
                if (p.Along <= StartAlong + 1e-3f ? beyond < -0.5f : beyond > 0.5f) return GroundSample.Missing;
            }

            float absLat = Math.Abs(p.Lateral);
            EdgeKind edge = p.Lateral < 0f ? p.LeftEdge : p.RightEdge;
            float y = p.CenterHeight;
            for (int i = 0; i < c.Ramps.Count; i++) y += c.Ramps[i].HeightAt(p.Along, p.Lateral);

            if (absLat <= p.HalfWidth) return y > searchFromHeight ? GroundSample.Missing : GroundSample.At(y, SurfaceKind.Road);
            switch (edge)
            {
                case EdgeKind.Void:
                    return absLat <= p.HalfWidth + VoidEdgeMargin ? GroundSample.At(y, SurfaceKind.Road) : GroundSample.Missing;
                case EdgeKind.Wall:
                    return GroundSample.At(y, SurfaceKind.Offroad); // walls push the board back; ground stays under it
                default:
                    return absLat <= p.HalfWidth + ShoulderWidth ? GroundSample.At(y - 0.01f, SurfaceKind.Offroad) : GroundSample.Missing;
            }
        }

        /// <summary>Planned (obstacle-free, reachable) lateral line at a distance.</summary>
        public float PlannedLateral(float along)
        {
            RoadChunk c = ChunkAt(along);
            if (c == null || c.PlannedLateral.Count == 0) return 0f;
            float a = SimMath.Clamp(along - c.StartAlong, 0f, c.Length);
            int i = Math.Min((int)(a / RoadChunk.SampleSpacing), c.PlannedLateral.Count - 2);
            if (i < 0) return c.PlannedLateral[0];
            float a0 = c.LocalAlongAt(i), a1 = c.LocalAlongAt(i + 1);
            float t = a1 > a0 ? (a - a0) / (a1 - a0) : 0f;
            return c.PlannedLateral[i] + (c.PlannedLateral[i + 1] - c.PlannedLateral[i]) * t;
        }

        /// <summary>
        /// Pure-pursuit steering request (-1..1, steering units) toward the planned line: what the road "asks for".
        /// Only cooperative-minded bots and the scripted ideal driver use it; the board never auto-steers.
        /// </summary>
        public float SteerHint(Vector3 position, float yaw, float speed, float maxYawRate, ref int hint)
        {
            RoadProjection p = Project(position, ref hint);
            if (!p.Valid) return 0f;
            float lookAhead = 12f + 1.1f * speed; // must exceed speed x total response delay (~1 s) or crews weave
            float targetAlong = p.Along + lookAhead;
            Vector3 target = WorldPoint(targetAlong, PlannedLateral(targetAlong), out _);
            float alpha = SimMath.WrapAngle(MathF.Atan2(target.X - position.X, target.Z - position.Z) - yaw);
            float desiredYawRate = 2f * Math.Max(speed, 1f) * MathF.Sin(alpha) / lookAhead;
            return SimMath.Clamp(desiredYawRate / Math.Max(maxYawRate, 1e-3f), -1f, 1f);
        }
    }
}
