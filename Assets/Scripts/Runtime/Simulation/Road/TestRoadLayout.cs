using System.Numerics;

namespace Game.Simulation
{
    /// <summary>
    /// The Milestone 1 test track, expressed as a fixed chunk sequence for the road generator:
    /// long downhill, one left curve, one right curve (peak radius ~110 m), cones, one ramp (~2.5 km).
    /// </summary>
    public static class TestRoadLayout
    {
        public const float RoadWidth = 12f;
        public const float Grade = 0.06f;
        /// <summary>Global distance of the ramp's low edge.</summary>
        public const float RampDistance = 960f;
        public const float Length = 2540f;

        public static FixedChunkSpec[] Build()
        {
            return new[]
            {
                new FixedChunkSpec
                {
                    Kind = ChunkKind.Straight, Length = 160f, Width = RoadWidth, Grade = Grade,
                    Cones = new[] { new Vector2(90f, -2.5f), new Vector2(100f, 2.5f), new Vector2(120f, 0f) },
                },
                new FixedChunkSpec
                {
                    Kind = ChunkKind.HardCurve, Length = 210f, TurnDeg = -55f, Width = RoadWidth, Grade = Grade,
                    Cones = new[] { new Vector2(105f, 3.5f) },
                },
                new FixedChunkSpec
                {
                    Kind = ChunkKind.Straight, Length = 160f, Width = RoadWidth, Grade = Grade,
                    Cones = new[] { new Vector2(120f, -3.5f) },
                },
                new FixedChunkSpec { Kind = ChunkKind.HardCurve, Length = 210f, TurnDeg = 55f, Width = RoadWidth, Grade = Grade },
                new FixedChunkSpec
                {
                    Kind = ChunkKind.Straight, Length = 450f, Width = RoadWidth, Grade = Grade,
                    Cones = new[]
                    {
                        new Vector2(80f, -2f), new Vector2(90f, 0f), new Vector2(100f, 2f),
                        new Vector2(360f, -4f), new Vector2(380f, 4f), new Vector2(400f, -1f),
                    },
                    HasRamp = true, RampAt = new Vector2(RampDistance - 740f, 0f),
                },
                // the rest of the 1800 m straight (split so every chunk fits the planner)
                new FixedChunkSpec { Kind = ChunkKind.Straight, Length = 450f, Width = RoadWidth, Grade = Grade },
                new FixedChunkSpec { Kind = ChunkKind.Straight, Length = 450f, Width = RoadWidth, Grade = Grade },
                new FixedChunkSpec { Kind = ChunkKind.Straight, Length = 450f, Width = RoadWidth, Grade = Grade },
            };
        }
    }
}
