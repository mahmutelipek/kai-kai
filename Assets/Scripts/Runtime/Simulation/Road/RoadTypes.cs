using System.Numerics;

namespace Game.Simulation
{
    /// <summary>What lies beyond the asphalt edge.</summary>
    public enum EdgeKind : byte
    {
        /// <summary>Drivable grass shoulder (offroad drag), then nothing.</summary>
        Grass = 0,
        /// <summary>Solid wall / barrier line just outside the road: heavy scrape.</summary>
        Wall = 1,
        /// <summary>Drop (bridge): no ground beyond the edge.</summary>
        Void = 2,
    }

    public enum ChunkKind
    {
        Straight,
        GentleCurve,
        HardCurve,
        SCurve,
        Narrow,
        Construction,
        Bridge,
        Tunnel,
        Ramp,
        BrokenRoad,
        Traffic,
        Intersection,
        PartialBarriers,
        Fork,
    }

    /// <summary>Connection point between chunks. Exit socket of chunk N must equal entry socket of chunk N+1.</summary>
    public struct RoadSocket
    {
        public Vector3 Position;
        public float Yaw;
        public float Width;
        /// <summary>Global distance along the road.</summary>
        public float Along;
    }

    /// <summary>A ramp wedge on the road surface (part of the ground, not an obstacle).</summary>
    public struct RampFeature
    {
        /// <summary>Global along of the low edge.</summary>
        public float Along;
        public float Length;
        public float Lateral;
        public float Width;
        public float Height;

        public float HeightAt(float along, float lateral)
        {
            if (along < Along || along > Along + Length) return 0f;
            if (System.Math.Abs(lateral - Lateral) > Width * 0.5f) return 0f;
            return Height * (along - Along) / Length;
        }
    }

    /// <summary>Result of projecting a world point onto the road.</summary>
    public struct RoadProjection
    {
        public bool Valid;
        public RoadChunk Chunk;
        public float Along;
        /// <summary>Signed offset from the centerline, + = right.</summary>
        public float Lateral;
        public float CenterHeight;
        public float HalfWidth;
        public float Yaw;
        public EdgeKind LeftEdge;
        public EdgeKind RightEdge;
    }
}
