using System.Numerics;

namespace Game.Simulation
{
    public enum ObstacleKind : byte
    {
        Cone,
        ConcreteBarrier,
        ParkedCar,
        MovingCar,
        ConstructionBarrier,
        Pothole,
        Divider,
        Crate,
        BrokenPiece,
    }

    public enum ObstacleMotion : byte
    {
        Static,
        /// <summary>Drives down the road in its lane, slower than the board.</summary>
        SameDirection,
        /// <summary>Drives up the road in its lane.</summary>
        Oncoming,
        /// <summary>Crosses the road at an intersection, yielding when the board is too close.</summary>
        Cross,
    }

    public enum ImpactSeverity : byte
    {
        /// <summary>Slow down a little (and, from M3, a combo hit).</summary>
        Light,
        /// <summary>Ground bump: small slowdown, everybody staggers.</summary>
        Bump,
        /// <summary>Big slowdown, stagger, players near the impact fall off.</summary>
        Heavy,
        /// <summary>Board flips.</summary>
        Crash,
    }

    /// <summary>
    /// One obstacle instance. Lives in a fixed pool (no allocation while running). Stored both in road frame
    /// (Along / Lateral, used for traversability checks and movers) and world frame (used for collisions and views).
    /// </summary>
    public struct Obstacle
    {
        public bool Active;
        /// <summary>Light obstacle that was hit: no longer collides, the view animates it flying away.</summary>
        public bool Knocked;
        public ObstacleKind Kind;
        public ObstacleMotion Motion;
        /// <summary>Increments every time the slot is reused so views can detect a new obstacle.</summary>
        public int Generation;
        public int ChunkSerial;

        public float Along;
        public float Lateral;
        /// <summary>Half size across the road (X) and along the road (Y), in the obstacle's own frame.</summary>
        public Vector2 HalfExtents;
        /// <summary>Rotation relative to the road direction (radians).</summary>
        public float YawOffset;
        public float Height;

        public Vector3 Position;
        public float Yaw;
        /// <summary>World-space XZ velocity (movers), used for relative impact speed and views.</summary>
        public Vector2 Velocity;

        public float Speed;
        public float MotionMin;
        public float MotionMax;
        public float WaitTimer;
        public float Direction;

        public float HitCooldown;
        public Vector3 KnockVelocity;
        public float KnockTime;
    }

    /// <summary>Static rules: what each obstacle is, how big, how bad a hit is.</summary>
    public static class ObstacleCatalog
    {
        public static Vector2 DefaultHalfExtents(ObstacleKind kind)
        {
            switch (kind)
            {
                case ObstacleKind.Cone: return new Vector2(0.3f, 0.3f);
                case ObstacleKind.ConcreteBarrier: return new Vector2(1.0f, 0.35f);
                case ObstacleKind.ParkedCar:
                case ObstacleKind.MovingCar: return new Vector2(1.0f, 2.2f);
                case ObstacleKind.ConstructionBarrier: return new Vector2(1.1f, 0.2f);
                case ObstacleKind.Pothole: return new Vector2(0.8f, 0.8f);
                case ObstacleKind.Divider: return new Vector2(0.4f, 10f);
                case ObstacleKind.Crate: return new Vector2(0.5f, 0.5f);
                default: return new Vector2(0.8f, 0.6f); // BrokenPiece
            }
        }

        public static float Height(ObstacleKind kind)
        {
            switch (kind)
            {
                case ObstacleKind.Cone: return 0.75f;
                case ObstacleKind.ConcreteBarrier: return 0.9f;
                case ObstacleKind.ParkedCar:
                case ObstacleKind.MovingCar: return 1.5f;
                case ObstacleKind.ConstructionBarrier: return 1.0f;
                case ObstacleKind.Pothole: return 0.05f;
                case ObstacleKind.Divider: return 0.9f;
                case ObstacleKind.Crate: return 1.0f;
                default: return 0.35f;
            }
        }

        /// <summary>Light obstacles get knocked away; they never block the road.</summary>
        public static bool IsKnockable(ObstacleKind kind) =>
            kind == ObstacleKind.Cone || kind == ObstacleKind.ConstructionBarrier || kind == ObstacleKind.Crate;

        /// <summary>Must the traversability check keep a lane free around this obstacle?</summary>
        public static bool BlocksCorridor(ObstacleKind kind) => !IsKnockable(kind) && kind != ObstacleKind.Pothole;

        public static ImpactSeverity BaseSeverity(ObstacleKind kind)
        {
            if (IsKnockable(kind)) return ImpactSeverity.Light;
            if (kind == ObstacleKind.Pothole) return ImpactSeverity.Bump;
            return ImpactSeverity.Heavy;
        }
    }
}
