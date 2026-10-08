using System;
using System.Collections.Generic;
using System.Numerics;

namespace Game.Simulation
{
    public struct ImpactEvent
    {
        public int Slot;
        public ObstacleKind Kind;
        public ImpactSeverity Severity;
        /// <summary>World XZ unit vector pointing from the obstacle toward the board.</summary>
        public Vector2 Normal;
        public float Depth;
        /// <summary>Closing speed along the normal (m/s).</summary>
        public float ClosingSpeed;
        public bool IsWall;
    }

    /// <summary>
    /// Fixed pool of obstacles: spawning, moving traffic (including cross traffic that yields to the board),
    /// knocked-away animation state, and board-vs-obstacle collision detection (2D oriented boxes + height).
    /// </summary>
    public sealed class ObstacleField
    {
        public const int Capacity = 512;
        public readonly Obstacle[] Items = new Obstacle[Capacity];
        int _cursor;

        public int ActiveCount { get; private set; }

        public int Spawn(in Obstacle template)
        {
            for (int n = 0; n < Capacity; n++)
            {
                int i = (_cursor + n) % Capacity;
                if (Items[i].Active) continue;
                int generation = Items[i].Generation + 1;
                Items[i] = template;
                Items[i].Active = true;
                Items[i].MinClearance = float.PositiveInfinity;
                Items[i].Generation = generation;
                _cursor = (i + 1) % Capacity;
                ActiveCount++;
                return i;
            }
            return -1; // pool exhausted: the obstacle is simply not placed (road stays traversable)
        }

        public void Despawn(int slot)
        {
            if (slot < 0 || !Items[slot].Active) return;
            Items[slot].Active = false;
            ActiveCount--;
        }

        public void Clear()
        {
            for (int i = 0; i < Capacity; i++) Items[i].Active = false;
            ActiveCount = 0;
        }

        /// <summary>Recomputes the world pose of a road-frame obstacle.</summary>
        public void UpdateWorldPose(RoadModel road, int slot)
        {
            ref Obstacle o = ref Items[slot];
            o.Position = road.WorldPoint(o.Along, o.Lateral, out float roadYaw);
            o.Yaw = roadYaw + o.YawOffset;
        }

        /// <summary>Moves traffic, ages knocked obstacles and hit cooldowns.</summary>
        public void Step(float dt, RoadModel road, float boardAlong, float boardSpeed)
        {
            for (int i = 0; i < Capacity; i++)
            {
                ref Obstacle o = ref Items[i];
                if (!o.Active) continue;
                o.HitCooldown = Math.Max(0f, o.HitCooldown - dt);
                if (o.Knocked)
                {
                    o.KnockTime += dt;
                    if (o.KnockTime > 3f) Despawn(i);
                    continue;
                }

                switch (o.Motion)
                {
                    case ObstacleMotion.SameDirection:
                        if (boardAlong < o.MotionMin - 150f) continue; // parked until the board is near
                        o.Along += o.Speed * dt;
                        if (o.Along > o.MotionMax) { Despawn(i); continue; }
                        break;
                    case ObstacleMotion.Oncoming:
                        if (boardAlong < o.MotionMin - 250f) continue;
                        o.Along -= o.Speed * dt;
                        if (o.Along < o.MotionMin) { Despawn(i); continue; }
                        break;
                    case ObstacleMotion.Cross:
                        StepCrossing(ref o, dt, boardAlong, boardSpeed);
                        break;
                    default:
                        continue;
                }
                Vector3 before = o.Position;
                UpdateWorldPose(road, i);
                o.Velocity = dt > 0f ? new Vector2(o.Position.X - before.X, o.Position.Z - before.Z) / dt : Vector2.Zero;
            }
        }

        /// <summary>
        /// Cross traffic waits at the kerb and only starts crossing when it can finish before the board arrives
        /// (or after the board has passed). This keeps intersections traversable by construction.
        /// </summary>
        static void StepCrossing(ref Obstacle o, float dt, float boardAlong, float boardSpeed)
        {
            bool waitingAtEnd = o.Direction == 0f;
            if (waitingAtEnd)
            {
                o.WaitTimer -= dt;
                if (o.WaitTimer > 0f) return;
                float crossTime = (o.MotionMax - o.MotionMin) / Math.Max(o.Speed, 0.1f);
                float distance = o.Along - boardAlong;
                bool passed = distance < -8f;
                bool clear = distance > 0f && distance / Math.Max(boardSpeed, 1f) > crossTime * 1.5f + 1.5f;
                if (!passed && !clear) return;
                o.Direction = o.Lateral <= o.MotionMin + 0.01f ? 1f : -1f;
            }
            o.Lateral += o.Direction * o.Speed * dt;
            if (o.Lateral >= o.MotionMax || o.Lateral <= o.MotionMin)
            {
                o.Lateral = SimMath.Clamp(o.Lateral, o.MotionMin, o.MotionMax);
                o.Direction = 0f;
                o.WaitTimer = 1.5f;
            }
            o.YawOffset = MathF.PI * 0.5f; // drives across the road
        }

        /// <summary>
        /// Board footprint (oriented rectangle at the board pose) against every nearby active obstacle.
        /// Airborne boards pass over obstacles lower than their clearance.
        /// </summary>
        public int Collide(in BoardState board, BoardTuningData t, float boardAlong, List<ImpactEvent> hits, float stepDistance = 0f)
        {
            hits.Clear();
            if (board.Crashed) return 0;
            // Light obstacles (cones, crates) are knocked on touch, not once the nose is inside them: an end-of-step test
            // only sees them up to a whole step late (0.7 m at 35 m/s), so for those the box leads the nose by half a
            // step. Solid obstacles use the exact box (planned clearances are tight; a hit must be a real overlap),
            // the response then puts the board back at the contact point (see RunSimulation.ResolveObstacles).
            Vector2 travel = SimMath.HeadingToDirection(board.TravelYaw);
            float lead = Math.Max(0f, stepDistance) * 0.5f;
            var boardPos = new Vector2(board.Position.X, board.Position.Z);
            var boardHalf = new Vector2(t.HalfWidth, t.HalfLength);
            var leadHalf = new Vector2(t.HalfWidth, t.HalfLength + lead * 0.5f);
            Vector2 boardVel = travel * board.Speed;

            for (int i = 0; i < Capacity; i++)
            {
                ref Obstacle o = ref Items[i];
                if (!o.Active || o.Knocked || o.HitCooldown > 0f) continue;
                if (Math.Abs(o.Along - boardAlong) > 15f) continue;
                if (board.Position.Y > o.Position.Y + o.Height) continue; // jumped over it

                var obsCenter = new Vector2(o.Position.X, o.Position.Z);
                bool light = ObstacleCatalog.IsKnockable(o.Kind);
                if (!Overlap(light ? boardPos + travel * (lead * 0.5f) : boardPos, board.Yaw, light ? leadHalf : boardHalf, obsCenter, o.Yaw, o.HalfExtents, out Vector2 normal, out float depth)) continue;

                o.HitByBoard = true;
                float closing = Vector2.Dot(boardVel - o.Velocity, -normal);
                hits.Add(new ImpactEvent
                {
                    Slot = i,
                    Kind = o.Kind,
                    Severity = ObstacleCatalog.BaseSeverity(o.Kind),
                    Normal = normal,
                    Depth = depth,
                    ClosingSpeed = closing,
                });
            }
            return hits.Count;
        }

        /// <summary>
        /// Near misses: while the board passes a solid obstacle (or car) the closest gap is tracked; once the board is
        /// past it, a gap under nearMissDistance at speed without a hit counts once.
        /// </summary>
        public int TrackNearMisses(in BoardState board, BoardTuningData t, float boardAlong, List<int> nearMisses)
        {
            nearMisses.Clear();
            if (board.Crashed) return 0;
            var boardCenter = new Vector2(board.Position.X, board.Position.Z);
            var boardHalf = new Vector2(t.HalfWidth, t.HalfLength);
            for (int i = 0; i < Capacity; i++)
            {
                ref Obstacle o = ref Items[i];
                if (!o.Active || o.Knocked || o.NearMissEvaluated) continue;
                if (ObstacleCatalog.IsKnockable(o.Kind) || o.Kind == ObstacleKind.Pothole) continue;
                float relAlong = boardAlong - o.Along;
                if (relAlong < -20f) continue;
                if (relAlong <= o.HalfExtents.Y + t.boardLength + 2f)
                {
                    if (Math.Abs(relAlong) < 15f + o.HalfExtents.Y && board.Position.Y <= o.Position.Y + o.Height)
                    {
                        float gap = Gap(boardCenter, board.Yaw, boardHalf, new Vector2(o.Position.X, o.Position.Z), o.Yaw, o.HalfExtents);
                        o.MinClearance = Math.Min(o.MinClearance, gap);
                    }
                    continue;
                }
                o.NearMissEvaluated = true;
                if (!o.HitByBoard && o.MinClearance < t.nearMissDistance && board.Speed >= t.nearMissMinSpeed) nearMisses.Add(i);
            }
            return nearMisses.Count;
        }

        /// <summary>Separation between two oriented rectangles along the best separating axis (0 when touching).</summary>
        public static float Gap(Vector2 ca, float yawA, Vector2 ha, Vector2 cb, float yawB, Vector2 hb)
        {
            Vector2 ax = new Vector2(MathF.Cos(yawA), -MathF.Sin(yawA)), az = new Vector2(MathF.Sin(yawA), MathF.Cos(yawA));
            Vector2 bx = new Vector2(MathF.Cos(yawB), -MathF.Sin(yawB)), bz = new Vector2(MathF.Sin(yawB), MathF.Cos(yawB));
            Vector2 d = ca - cb;
            float gap = 0f;
            gap = Math.Max(gap, AxisGap(ax, d, ax, az, ha, bx, bz, hb));
            gap = Math.Max(gap, AxisGap(az, d, ax, az, ha, bx, bz, hb));
            gap = Math.Max(gap, AxisGap(bx, d, ax, az, ha, bx, bz, hb));
            gap = Math.Max(gap, AxisGap(bz, d, ax, az, ha, bx, bz, hb));
            return gap;
        }

        static float AxisGap(Vector2 axis, Vector2 d, Vector2 ax, Vector2 az, Vector2 ha, Vector2 bx, Vector2 bz, Vector2 hb)
        {
            float ra = ha.X * Math.Abs(Vector2.Dot(ax, axis)) + ha.Y * Math.Abs(Vector2.Dot(az, axis));
            float rb = hb.X * Math.Abs(Vector2.Dot(bx, axis)) + hb.Y * Math.Abs(Vector2.Dot(bz, axis));
            return Math.Abs(Vector2.Dot(d, axis)) - ra - rb;
        }

        /// <summary>2D separating-axis test between two oriented rectangles. Normal points from B toward A.</summary>
        public static bool Overlap(Vector2 ca, float yawA, Vector2 ha, Vector2 cb, float yawB, Vector2 hb, out Vector2 normal, out float depth)
        {
            normal = Vector2.Zero;
            depth = float.PositiveInfinity;
            Vector2 ax = new Vector2(MathF.Cos(yawA), -MathF.Sin(yawA)), az = new Vector2(MathF.Sin(yawA), MathF.Cos(yawA));
            Vector2 bx = new Vector2(MathF.Cos(yawB), -MathF.Sin(yawB)), bz = new Vector2(MathF.Sin(yawB), MathF.Cos(yawB));
            Vector2 d = ca - cb;
            if (!Axis(ax, d, ax, az, ha, bx, bz, hb, ref normal, ref depth)) return false;
            if (!Axis(az, d, ax, az, ha, bx, bz, hb, ref normal, ref depth)) return false;
            if (!Axis(bx, d, ax, az, ha, bx, bz, hb, ref normal, ref depth)) return false;
            if (!Axis(bz, d, ax, az, ha, bx, bz, hb, ref normal, ref depth)) return false;
            return true;
        }

        static bool Axis(Vector2 axis, Vector2 d, Vector2 ax, Vector2 az, Vector2 ha, Vector2 bx, Vector2 bz, Vector2 hb,
                         ref Vector2 normal, ref float depth)
        {
            float ra = ha.X * Math.Abs(Vector2.Dot(ax, axis)) + ha.Y * Math.Abs(Vector2.Dot(az, axis));
            float rb = hb.X * Math.Abs(Vector2.Dot(bx, axis)) + hb.Y * Math.Abs(Vector2.Dot(bz, axis));
            float dist = Vector2.Dot(d, axis);
            float overlap = ra + rb - Math.Abs(dist);
            if (overlap <= 0f) return false;
            if (overlap < depth)
            {
                depth = overlap;
                normal = dist >= 0f ? axis : -axis;
            }
            return true;
        }
    }
}
