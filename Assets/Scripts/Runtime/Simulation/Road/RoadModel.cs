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
        /// <summary>Road behind the very start of a run (the chase camera and the rear wheels look back there); drawn by ChunkGeometry.LeadIn.</summary>
        public const float LeadInLength = 70f;
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
            float leadInRise = 0f;
            if (p.Along <= StartAlong + 1e-3f || p.Along >= EndAlong - 1e-3f)
            {
                Vector3 fwd = SimMath.Forward3(p.Yaw);
                bool atStart = p.Along <= StartAlong + 1e-3f;
                Vector3 edgePoint = c.Points[atStart ? 0 : c.SampleCount - 1];
                float beyond = (x - edgePoint.X) * fwd.X + (z - edgePoint.Z) * fwd.Z;
                if (atStart && c.StartAlong <= 1e-3f && c.SampleCount > 1)
                {
                    // the first chunk of a run: keep the road going straight back so nothing hangs over the sea behind the start line
                    if (beyond < -LeadInLength) return GroundSample.Missing;
                    if (beyond < 0f)
                    {
                        Vector3 next = c.Points[1];
                        float run = MathF.Max(1e-3f, MathF.Sqrt((next.X - edgePoint.X) * (next.X - edgePoint.X) + (next.Z - edgePoint.Z) * (next.Z - edgePoint.Z)));
                        leadInRise = (edgePoint.Y - next.Y) / run * -beyond;
                    }
                }
                else if (atStart ? beyond < -0.5f : beyond > 0.5f) return GroundSample.Missing;
            }

            float absLat = Math.Abs(p.Lateral);
            EdgeKind edge = p.Lateral < 0f ? p.LeftEdge : p.RightEdge;
            float y = p.CenterHeight + leadInRise;
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


        /// <summary>
        /// Offset outside the road edge of the front face of a building on <paramref name="side"/> within
        /// <paramref name="reach"/> of <paramref name="along"/> (global distance), or +infinity when there is none.
        /// </summary>
        public float BuildingOffset(float along, int side, float reach)
        {
            float best = float.PositiveInfinity;
            for (int i = 0; i < Chunks.Count; i++)
            {
                RoadChunk c = Chunks[i];
                if (along + reach < c.StartAlong || along - reach > c.StartAlong + c.Length) continue;
                for (int k = 0; k < c.Buildings.Count; k++)
                {
                    BuildingBox b = c.Buildings[k];
                    if (b.Side != side) continue;
                    if (along + reach < c.StartAlong + b.AlongMin || along - reach > c.StartAlong + b.AlongMax) continue;
                    if (b.Near < best) best = b.Near;
                }
            }
            return best;
        }

        /// <summary>Solid poles within <paramref name="reach"/> of <paramref name="along"/>: (global along, signed lateral, radius).</summary>
        public void PostsNear(float along, float reach, List<PostCircle> into)
        {
            into.Clear();
            for (int i = 0; i < Chunks.Count; i++)
            {
                RoadChunk c = Chunks[i];
                if (along + reach < c.StartAlong || along - reach > c.StartAlong + c.Length) continue;
                for (int k = 0; k < c.Posts.Count; k++)
                {
                    PostCircle p = c.Posts[k];
                    float a = c.StartAlong + p.Along;
                    if (a < along - reach || a > along + reach) continue;
                    into.Add(new PostCircle { Along = a, Lateral = p.Lateral, Radius = p.Radius });
                }
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
        /// 0..1: how much the road ahead asks the crew to slow down (curves tighter than the safe steering at this
        /// speed, or dodges on the planned line). Used by cooperative bots and, later, warning feedback.
        /// </summary>
        public float BrakeHint(float along, float speed, BoardTuningData t)
        {
            if (speed < 12f || Chunks.Count == 0) return 0f;
            float maxYawRate = (t.yawRateBaseDeg + t.yawRatePerSpeedDeg * speed) * SimMath.Deg2Rad;
            float stability = SimMath.Lerp(t.stabilityFactorLowSpeed, t.stabilityFactorHighSpeed, speed / t.stabilityReferenceSpeed);
            float safeSteer = 0.6f * t.crashThreshold / stability;
            float horizon = Math.Max(40f, speed * 3.5f);
            float maxCurvature = 0f, maxSlope = 0f;
            Sample(along, out _, out float prevYaw, out _);
            float prevLateral = PlannedLateral(along);
            for (float s = 4f; s <= horizon; s += 4f)
            {
                Sample(along + s, out _, out float yaw, out _);
                float lateral = PlannedLateral(along + s);
                maxCurvature = Math.Max(maxCurvature, Math.Abs(SimMath.WrapAngle(yaw - prevYaw)) / 4f);
                maxSlope = Math.Max(maxSlope, Math.Abs(lateral - prevLateral) / 4f);
                prevYaw = yaw;
                prevLateral = lateral;
            }
            float required = speed * maxCurvature / Math.Max(maxYawRate, 1e-3f);
            float curve = SimMath.InverseLerp(0.7f, 0.95f, required / Math.Max(safeSteer, 1e-3f));
            float weave = speed > 24f ? SimMath.InverseLerp(0.02f, 0.04f, maxSlope) : 0f;
            return Math.Max(curve, weave);
        }

        /// <summary>
        /// Steering request (-1..1) toward the planned line using pure pursuit from the pose the board will have
        /// after the crew's response delay (walking + smoothing + yaw response). Tracks the line much tighter than
        /// plain pursuit; used by cooperative bots and the scripted ideal driver.
        /// </summary>
        public float PredictiveSteerHint(in BoardState b, float maxYawRate, float responseDelay, ref int hint)
        {
            float T = responseDelay;
            float yawP = b.Yaw + b.YawRate * T;
            Vector2 dir = SimMath.HeadingToDirection(b.TravelYaw + b.YawRate * T * 0.5f);
            var posP = new Vector3(b.Position.X + dir.X * b.Speed * T, b.Position.Y, b.Position.Z + dir.Y * b.Speed * T);
            RoadProjection pp = Project(posP, ref hint);
            if (!pp.Valid) return 0f;
            float lookAhead = 6f + 0.35f * b.Speed;
            float targetAlong = pp.Along + lookAhead;
            Vector3 target = WorldPoint(targetAlong, PlannedLateral(targetAlong), out _);
            float alpha = SimMath.WrapAngle(MathF.Atan2(target.X - posP.X, target.Z - posP.Z) - yawP);
            float desiredYawRate = 2f * Math.Max(b.Speed, 1f) * MathF.Sin(alpha) / lookAhead;
            return SimMath.Clamp(desiredYawRate / Math.Max(maxYawRate, 1e-3f), -1f, 1f);
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
