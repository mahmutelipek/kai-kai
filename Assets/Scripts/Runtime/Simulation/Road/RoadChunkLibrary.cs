using System;
using System.Collections.Generic;

namespace Game.Simulation
{
    /// <summary>Static description of a chunk type. Instances randomize within the ranges (and mirror left/right).</summary>
    public sealed class ChunkDefinition
    {
        public ChunkKind Kind;
        public string Name;
        /// <summary>1 (trivial) .. 10 (hardest).</summary>
        public float Rating;
        /// <summary>Earliest global distance at which the generator may use it.</summary>
        public float MinDistance;
        public float LengthMin, LengthMax;
        /// <summary>Total turn in degrees (random sign unless the kind fixes the shape). 0 for straight kinds.</summary>
        public float TurnMin, TurnMax;
        public float Weight = 1f;
        /// <summary>Hard chunks want a breather afterwards.</summary>
        public bool IsIntense;
    }

    /// <summary>All chunk types the endless road is assembled from.</summary>
    public static class RoadChunkLibrary
    {
        public static readonly IReadOnlyList<ChunkDefinition> All = new List<ChunkDefinition>
        {
            new ChunkDefinition { Kind = ChunkKind.Straight,        Name = "Straight",          Rating = 1, MinDistance = 0,    LengthMin = 120, LengthMax = 200, Weight = 1.2f },
            new ChunkDefinition { Kind = ChunkKind.GentleCurve,     Name = "Gentle curve",      Rating = 2, MinDistance = 0,    LengthMin = 140, LengthMax = 200, TurnMin = 20, TurnMax = 35, Weight = 1.4f },
            new ChunkDefinition { Kind = ChunkKind.Ramp,            Name = "Ramp",              Rating = 3, MinDistance = 200,  LengthMin = 130, LengthMax = 170 },
            new ChunkDefinition { Kind = ChunkKind.Bridge,          Name = "Bridge",            Rating = 3, MinDistance = 400,  LengthMin = 180, LengthMax = 240, TurnMin = 0, TurnMax = 15 },
            new ChunkDefinition { Kind = ChunkKind.SCurve,          Name = "S-curve",           Rating = 4, MinDistance = 500,  LengthMin = 220, LengthMax = 270, TurnMin = 25, TurnMax = 38 },
            new ChunkDefinition { Kind = ChunkKind.Construction,    Name = "Construction area", Rating = 4, MinDistance = 600,  LengthMin = 160, LengthMax = 200 },
            new ChunkDefinition { Kind = ChunkKind.Tunnel,          Name = "Tunnel",            Rating = 4, MinDistance = 900,  LengthMin = 160, LengthMax = 240, TurnMin = 0, TurnMax = 20 },
            new ChunkDefinition { Kind = ChunkKind.HardCurve,       Name = "Hard curve",        Rating = 5, MinDistance = 800,  LengthMin = 120, LengthMax = 150, TurnMin = 45, TurnMax = 65, IsIntense = true },
            new ChunkDefinition { Kind = ChunkKind.Narrow,          Name = "Narrow section",    Rating = 5, MinDistance = 1200, LengthMin = 150, LengthMax = 190, IsIntense = true },
            new ChunkDefinition { Kind = ChunkKind.Fork,            Name = "Fork",              Rating = 5, MinDistance = 1400, LengthMin = 190, LengthMax = 230 },
            new ChunkDefinition { Kind = ChunkKind.PartialBarriers, Name = "Partial barriers",  Rating = 5, MinDistance = 1500, LengthMin = 160, LengthMax = 200, IsIntense = true },
            new ChunkDefinition { Kind = ChunkKind.BrokenRoad,      Name = "Broken road",       Rating = 6, MinDistance = 1800, LengthMin = 150, LengthMax = 190, IsIntense = true },
            new ChunkDefinition { Kind = ChunkKind.Traffic,         Name = "Traffic",           Rating = 6, MinDistance = 2000, LengthMin = 200, LengthMax = 260, IsIntense = true },
            new ChunkDefinition { Kind = ChunkKind.Intersection,    Name = "Downhill intersection", Rating = 7, MinDistance = 2500, LengthMin = 120, LengthMax = 140, IsIntense = true },
        };

        public static ChunkDefinition Get(ChunkKind kind)
        {
            for (int i = 0; i < All.Count; i++) if (All[i].Kind == kind) return All[i];
            throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }
}
