using System;
using System.Collections.Generic;
using System.Numerics;

namespace Game.Simulation
{
    public struct ChunkLogEntry
    {
        public int Serial;
        public ChunkKind Kind;
        public float StartAlong;
        public float Length;
        public float Difficulty;
        public float DifficultyLevel;
        public int Obstacles;
        public int ObstaclesRemovedForTraversability;
    }

    /// <summary>Fixed chunk for hand-built tracks (the Milestone 1 test track).</summary>
    public struct FixedChunkSpec
    {
        public ChunkKind Kind;
        public float Length;
        public float TurnDeg;
        public float Width;
        public float Grade;
        public Vector2[] Cones;   // (local along, lateral)
        public bool HasRamp;
        public Vector2 RampAt;    // (local along, lateral)
    }

    /// <summary>
    /// Assembles the endless road from RoadChunkLibrary chunks: spawns ahead of the board, despawns behind,
    /// pools chunks, places obstacles per chunk type scaled by difficulty, and proves every chunk traversable
    /// with RoadPlanner (removing blocking obstacles if a random layout ever blocks the way).
    /// </summary>
    public sealed class RoadGenerator
    {
        public const float SpawnAhead = 450f;
        public const float KeepBehind = 150f;
        public const float EndClearance = 20f;
        public const float MaxHeadingDeg = 55f;
        public const int PrewarmChunks = 10;

        readonly BoardTuningData _tuning;
        readonly RoadModel _road;
        readonly ObstacleField _obstacles;
        readonly DifficultyManager _difficulty;
        readonly RoadPlanner _planner = new RoadPlanner();
        readonly Stack<RoadChunk> _pool = new Stack<RoadChunk>();
        IList<FixedChunkSpec> _fixed;
        int _fixedIndex;
        Random _rng;
        int _serial;
        ChunkKind _lastKind;
        bool _lastIntense;

        /// <summary>Optional: when set, every spawned chunk is appended (tests / difficulty curve logging).</summary>
        public List<ChunkLogEntry> Log;
        public int Seed { get; private set; }
        public int ChunksCreated { get; private set; }
        public int PooledChunks => _pool.Count;

        public RoadGenerator(BoardTuningData tuning, RoadModel road, ObstacleField obstacles, DifficultyManager difficulty)
        {
            _tuning = tuning;
            _road = road;
            _obstacles = obstacles;
            _difficulty = difficulty;
            // pre-fill the pool so the endless road never allocates chunks mid-run (~6 are ever alive at once)
            for (int i = 0; i < PrewarmChunks; i++) { _pool.Push(new RoadChunk()); ChunksCreated++; }
        }

        /// <summary>Clears the road and starts a new endless road (or a fixed track when <paramref name="fixedTrack"/> is set).</summary>
        public void Reset(int seed, IList<FixedChunkSpec> fixedTrack = null)
        {
            for (int i = _road.Chunks.Count - 1; i >= 0; i--) Recycle(_road.Chunks[i]);
            _road.Chunks.Clear();
            _obstacles.Clear();
            Seed = seed;
            _rng = new Random(seed);
            _fixed = fixedTrack;
            _fixedIndex = 0;
            // serials keep counting across runs so views can never confuse an old chunk with a new one
            _lastKind = ChunkKind.Straight;
            _lastIntense = false;
        }

        /// <summary>Spawns road ahead of <paramref name="boardAlong"/> and recycles chunks far behind it.</summary>
        public void Update(float boardAlong)
        {
            while (_road.Chunks.Count == 0 || _road.EndAlong < boardAlong + SpawnAhead) SpawnNext();
            while (_road.Chunks.Count > 2 && _road.Chunks[0].EndAlong < boardAlong - KeepBehind)
            {
                RoadChunk old = _road.Chunks[0];
                _road.Chunks.RemoveAt(0);
                Recycle(old);
            }
        }

        void Recycle(RoadChunk chunk)
        {
            for (int i = 0; i < chunk.ObstacleSlots.Count; i++)
                if (chunk.Owns(_obstacles, i)) _obstacles.Despawn(chunk.ObstacleSlots[i]);
            chunk.ObstacleSlots.Clear();
            chunk.ObstacleGenerations.Clear();
            _pool.Push(chunk);
        }

        RoadChunk Rent()
        {
            if (_pool.Count > 0) return _pool.Pop();
            ChunksCreated++;
            return new RoadChunk();
        }

        void SpawnNext()
        {
            RoadSocket entry;
            float entryLateral;
            if (_road.Chunks.Count == 0)
            {
                entry = new RoadSocket { Position = Vector3.Zero, Yaw = 0f, Width = _difficulty.RoadWidth(0f), Along = 0f };
                entryLateral = 0f;
            }
            else
            {
                RoadChunk last = _road.Chunks[_road.Chunks.Count - 1];
                entry = last.Exit;
                entryLateral = last.PlannedLateral.Count > 0 ? last.PlannedLateral[last.PlannedLateral.Count - 1] : 0f;
            }

            RoadChunk chunk = Rent();
            float d = _difficulty.At(entry.Along);
            int removed = 0;
            if (_fixed != null) BuildFixed(chunk, entry, d);
            else BuildRandom(chunk, entry, d);

            // prove traversability; if a random layout blocks the way, drop the blocking obstacle nearest the failure
            while (!_planner.Plan(chunk, _obstacles, _tuning, entryLateral))
            {
                if (!RemoveBlockingNear(chunk, _planner.FailedAlong)) break;
                removed++;
            }

            _lastKind = chunk.Kind;
            _lastIntense = chunk.Definition.IsIntense;
            Log?.Add(new ChunkLogEntry
            {
                Serial = chunk.Serial,
                Kind = chunk.Kind,
                StartAlong = chunk.StartAlong,
                Length = chunk.Length,
                Difficulty = chunk.Difficulty,
                DifficultyLevel = d,
                Obstacles = chunk.ObstacleSlots.Count,
                ObstaclesRemovedForTraversability = removed,
            });
        }

        bool RemoveBlockingNear(RoadChunk chunk, float along)
        {
            int bestK = -1;
            float bestD = float.PositiveInfinity;
            for (int k = 0; k < chunk.ObstacleSlots.Count; k++)
            {
                if (!chunk.Owns(_obstacles, k)) continue;
                ref Obstacle o = ref _obstacles.Items[chunk.ObstacleSlots[k]];
                if (!ObstacleCatalog.BlocksCorridor(o.Kind)) continue;
                float dist = o.Motion == ObstacleMotion.Static ? Math.Abs(o.Along - along)
                           : (along >= o.MotionMin && along <= o.MotionMax ? 0f : 1e6f);
                if (dist < bestD) { bestD = dist; bestK = k; }
            }
            if (bestK < 0) return false;
            _obstacles.Despawn(chunk.ObstacleSlots[bestK]);
            chunk.ObstacleSlots.RemoveAt(bestK);
            chunk.ObstacleGenerations.RemoveAt(bestK);
            return true;
        }

        // ------------------------------------------------------------------ chunk selection

        ChunkDefinition Choose(float along, float d)
        {
            if (along < 1f) return RoadChunkLibrary.Get(ChunkKind.Straight); // calm start
            float target = _difficulty.TargetRating(d);
            bool combined = _difficulty.CombinedHazards(d);
            float total = 0f;
            IReadOnlyList<ChunkDefinition> all = RoadChunkLibrary.All;
            for (int pass = 0; pass < 2; pass++)
            {
                float pick = pass == 1 ? (float)_rng.NextDouble() * total : 0f;
                for (int i = 0; i < all.Count; i++)
                {
                    float w = WeightOf(all[i], along, target, combined);
                    if (w <= 0f) continue;
                    if (pass == 0) { total += w; continue; }
                    pick -= w;
                    if (pick <= 0f) return all[i];
                }
            }
            return RoadChunkLibrary.Get(ChunkKind.Straight);
        }

        float WeightOf(ChunkDefinition def, float along, float target, bool combined)
        {
            if (def.MinDistance > along) return 0f;
            if (def.Kind == _lastKind && def.Kind != ChunkKind.GentleCurve) return 0f;
            if (_lastIntense && def.IsIntense && !combined) return 0f;
            if (_lastKind == ChunkKind.Ramp && def.Kind == ChunkKind.HardCurve && !combined) return 0f;
            float diff = (def.Rating - target) / 2.2f;
            float w = def.Weight * MathF.Exp(-diff * diff);
            if (_lastIntense && def.Rating <= 2f) w *= 3f; // breather
            return w;
        }

        // ------------------------------------------------------------------ geometry

        float TurnToward(float entryYaw, float magnitudeDeg)
        {
            float yawDeg = entryYaw * SimMath.Rad2Deg;
            float sign = _rng.NextDouble() < 0.5 ? -1f : 1f;
            if (Math.Abs(yawDeg) > 15f && _rng.NextDouble() < 0.85) sign = -MathF.Sign(yawDeg);
            if (Math.Abs(yawDeg + sign * magnitudeDeg) > MaxHeadingDeg) sign = -sign;
            float turn = sign * magnitudeDeg;
            float result = yawDeg + turn;
            if (Math.Abs(result) > MaxHeadingDeg) turn = MathF.Sign(result) * MaxHeadingDeg - yawDeg;
            return turn * SimMath.Deg2Rad;
        }

        float Range(float min, float max) => min + (float)_rng.NextDouble() * (max - min);

        void BuildRandom(RoadChunk chunk, RoadSocket entry, float d)
        {
            ChunkDefinition def = Choose(entry.Along, d);
            float length = Range(def.LengthMin, def.LengthMax);
            float baseWidth = _difficulty.RoadWidth(d);
            var shape = new ChunkShape
            {
                Length = length,
                Grade = Range(0.05f, 0.075f),
                BaseWidth = baseWidth,
                DefaultEdge = EdgeKind.Grass,
            };
            bool mirrored = false;

            switch (def.Kind)
            {
                case ChunkKind.GentleCurve:
                case ChunkKind.HardCurve:
                    shape.Turn = TurnToward(entry.Yaw, Range(def.TurnMin, def.TurnMax));
                    mirrored = shape.Turn < 0f;
                    break;
                case ChunkKind.SCurve:
                    shape.SCurve = true;
                    shape.Turn = TurnToward(entry.Yaw, Range(def.TurnMin, def.TurnMax));
                    mirrored = shape.Turn < 0f;
                    break;
                case ChunkKind.Bridge:
                    shape.Grade = 0.02f;
                    shape.Turn = TurnToward(entry.Yaw, Range(def.TurnMin, def.TurnMax));
                    shape.SpecialEdge = EdgeKind.Void;
                    shape.EdgeStart = 12f;
                    shape.EdgeEnd = length - 12f;
                    break;
                case ChunkKind.Tunnel:
                    shape.Grade = 0.045f;
                    shape.Turn = TurnToward(entry.Yaw, Range(def.TurnMin, def.TurnMax));
                    shape.SpecialEdge = EdgeKind.Wall;
                    shape.EdgeStart = 10f;
                    shape.EdgeEnd = length - 10f;
                    break;
                case ChunkKind.Narrow:
                    shape.SpecialWidth = SimMath.Lerp(8.5f, 7f, d);
                    shape.SpecialStart = 45f;
                    shape.SpecialEnd = length - 45f;
                    shape.SpecialEdge = EdgeKind.Wall;
                    shape.EdgeStart = shape.SpecialStart;
                    shape.EdgeEnd = shape.SpecialEnd;
                    break;
                case ChunkKind.Traffic:
                    shape.BaseWidth = Math.Max(baseWidth, 12.5f);
                    shape.Turn = TurnToward(entry.Yaw, Range(0f, 12f));
                    break;
                case ChunkKind.Intersection:
                    shape.Grade = 0.03f;
                    shape.SpecialWidth = baseWidth + 4f;
                    shape.SpecialStart = length * 0.4f;
                    shape.SpecialEnd = length * 0.6f;
                    break;
                case ChunkKind.Fork:
                    shape.SpecialWidth = Math.Max(baseWidth + 7f, 18f);
                    shape.SpecialStart = 60f;
                    shape.SpecialEnd = length - 60f;
                    break;
            }

            chunk.Build(++_serial, def, mirrored, def.Rating + 3f * d, entry, shape);
            if (def.Kind == ChunkKind.Tunnel) { chunk.RoofStart = 15f; chunk.RoofEnd = length - 15f; }
            _road.Chunks.Add(chunk);
            PlaceObstacles(chunk, d);
        }

        void BuildFixed(RoadChunk chunk, RoadSocket entry, float d)
        {
            FixedChunkSpec spec = _fixedIndex < _fixed.Count
                ? _fixed[_fixedIndex++]
                : new FixedChunkSpec { Kind = ChunkKind.Straight, Length = 200f, Width = entry.Width, Grade = 0.06f };
            var shape = new ChunkShape
            {
                Length = spec.Length,
                Turn = spec.TurnDeg * SimMath.Deg2Rad,
                Grade = spec.Grade,
                BaseWidth = spec.Width,
                DefaultEdge = EdgeKind.Grass,
            };
            if (_road.Chunks.Count == 0) entry.Width = spec.Width;
            ChunkDefinition def = RoadChunkLibrary.Get(spec.Kind);
            chunk.Build(++_serial, def, spec.TurnDeg < 0f, def.Rating, entry, shape);
            _road.Chunks.Add(chunk);
            if (spec.Cones != null)
                for (int i = 0; i < spec.Cones.Length; i++) Place(chunk, ObstacleKind.Cone, spec.Cones[i].X, spec.Cones[i].Y);
            if (spec.HasRamp) AddRamp(chunk, spec.RampAt.X, spec.RampAt.Y, 5f, 8f, 1.3f);
        }

        // ------------------------------------------------------------------ obstacles

        int Place(RoadChunk chunk, ObstacleKind kind, float localAlong, float lateral, float yawOffset = 0f, Vector2 halfExtents = default)
        {
            var template = new Obstacle
            {
                Kind = kind,
                Motion = ObstacleMotion.Static,
                ChunkSerial = chunk.Serial,
                Along = chunk.StartAlong + localAlong,
                Lateral = lateral,
                HalfExtents = halfExtents == default ? ObstacleCatalog.DefaultHalfExtents(kind) : halfExtents,
                Height = ObstacleCatalog.Height(kind),
                YawOffset = yawOffset,
            };
            int slot = _obstacles.Spawn(template);
            if (slot < 0) return -1;
            _obstacles.UpdateWorldPose(_road, slot);
            chunk.ObstacleSlots.Add(slot);
            chunk.ObstacleGenerations.Add(_obstacles.Items[slot].Generation);
            return slot;
        }

        void AddRamp(RoadChunk chunk, float localAlong, float lateral, float width, float length, float height)
        {
            chunk.Ramps.Add(new RampFeature
            {
                Along = chunk.StartAlong + localAlong,
                Length = length,
                Lateral = lateral,
                Width = width,
                Height = height,
            });
        }

        float HalfWidthAt(RoadChunk chunk, float localAlong)
        {
            chunk.SampleLocal(localAlong, out _, out _, out float hw);
            return hw;
        }

        float RandomLateral(RoadChunk chunk, float localAlong, float edgeMargin)
        {
            float hw = HalfWidthAt(chunk, localAlong) - edgeMargin;
            return Range(-hw, hw);
        }

        void PlaceObstacles(RoadChunk chunk, float d)
        {
            float L = chunk.Length;
            float density = _difficulty.ObstacleDensity(d);
            float a0 = EndClearance, a1 = L - EndClearance;
            float sideOfTurn = MathF.Sign(chunk.Shape.Turn);

            switch (chunk.Kind)
            {
                case ChunkKind.Straight:
                {
                    int n = _rng.Next(0, 2 + (int)(3f * density));
                    for (int i = 0; i < n; i++)
                    {
                        float a = Range(a0 + 10f, a1);
                        ObstacleKind kind = d < 0.35f || _rng.NextDouble() < 0.5 ? ObstacleKind.Cone : ObstacleKind.Crate;
                        Place(chunk, kind, a, RandomLateral(chunk, a, 1f));
                    }
                    break;
                }
                case ChunkKind.GentleCurve:
                {
                    int n = _rng.Next(0, 3);
                    for (int i = 0; i < n; i++)
                    {
                        float a = Range(L * 0.3f, L * 0.7f);
                        float hw = HalfWidthAt(chunk, a);
                        Place(chunk, ObstacleKind.Cone, a, -sideOfTurn * Range(1f, hw - 1f));
                    }
                    break;
                }
                case ChunkKind.HardCurve:
                {
                    // warning cones on the outside of the apex
                    int n = 3 + _rng.Next(0, 3);
                    for (int i = 0; i < n; i++)
                    {
                        float a = SimMath.Lerp(L * 0.35f, L * 0.65f, i / (float)Math.Max(1, n - 1));
                        Place(chunk, ObstacleKind.Cone, a, sideOfTurn * (HalfWidthAt(chunk, a) - 0.8f));
                    }
                    if (d > 0.5f) Place(chunk, ObstacleKind.Crate, L * 0.5f, -sideOfTurn * Range(0f, 2f));
                    break;
                }
                case ChunkKind.SCurve:
                {
                    int n = _rng.Next(0, 4);
                    for (int i = 0; i < n; i++)
                    {
                        float a = Range(a0 + 10f, a1);
                        Place(chunk, ObstacleKind.Cone, a, RandomLateral(chunk, a, 1f));
                    }
                    break;
                }
                case ChunkKind.Ramp:
                {
                    float a = L * 0.45f;
                    float lat = Range(-2f, 2f);
                    float width = 5f, len = 8f, height = SimMath.Lerp(1.1f, 1.6f, d);
                    AddRamp(chunk, a, lat, width, len, height);
                    Place(chunk, ObstacleKind.Cone, a - 1f, lat - width * 0.5f - 0.8f);
                    Place(chunk, ObstacleKind.Cone, a - 1f, lat + width * 0.5f + 0.8f);
                    break;
                }
                case ChunkKind.Bridge:
                {
                    int n = d > 0.4f ? _rng.Next(0, 3) : 0;
                    for (int i = 0; i < n; i++)
                    {
                        float a = Range(a0 + 20f, a1 - 20f);
                        Place(chunk, ObstacleKind.Crate, a, RandomLateral(chunk, a, 1.5f));
                    }
                    break;
                }
                case ChunkKind.Tunnel:
                {
                    if (d > 0.4f)
                    {
                        float side = _rng.NextDouble() < 0.5 ? -1f : 1f;
                        float a = L * 0.5f;
                        Place(chunk, ObstacleKind.ParkedCar, a, side * (HalfWidthAt(chunk, a) - 1.3f));
                    }
                    break;
                }
                case ChunkKind.Construction:
                {
                    // one side closed with light construction barriers, tapered with cones
                    float side = _rng.NextDouble() < 0.5 ? -1f : 1f;
                    for (float a = 45f; a < L - 40f; a += 8f)
                    {
                        float hw = HalfWidthAt(chunk, a);
                        float closedHalf = hw * 0.42f;
                        Place(chunk, ObstacleKind.ConstructionBarrier, a, side * (hw - closedHalf - 0.2f), 0f, new Vector2(closedHalf, 0.2f));
                    }
                    for (int i = 0; i < 4; i++)
                    {
                        float a = 25f + i * 5f;
                        float hw = HalfWidthAt(chunk, a);
                        Place(chunk, ObstacleKind.Cone, a, side * (hw - 0.6f - i * hw * 0.2f));
                    }
                    break;
                }
                case ChunkKind.Narrow:
                {
                    if (d > 0.5f && _rng.NextDouble() < 0.5)
                    {
                        float a = L * 0.5f;
                        Place(chunk, ObstacleKind.Cone, a, Range(-1f, 1f));
                    }
                    break;
                }
                case ChunkKind.PartialBarriers:
                {
                    // rows alternately block the right and the left part of the road, each reaching to `inner`
                    // metres short of the centreline on its own side; the free lane shifts by a few metres per row,
                    // spaced so the board can weave through at the speed it will have here
                    float spacing = Math.Max(45f, _difficulty.ExpectedSpeed(d) * 1.7f);
                    float side = _rng.NextDouble() < 0.5 ? -1f : 1f;
                    for (float a = 35f; a < L - 30f; a += spacing)
                    {
                        float hw = HalfWidthAt(chunk, a);
                        float inner = SimMath.Lerp(1.0f, 0.3f, d);
                        float lo = side > 0f ? inner : -hw - 0.3f;
                        float hi = side > 0f ? hw + 0.3f : -inner;
                        Place(chunk, ObstacleKind.ConcreteBarrier, a, (lo + hi) * 0.5f, 0f, new Vector2((hi - lo) * 0.5f, 0.35f));
                        side = -side;
                    }
                    break;
                }
                case ChunkKind.BrokenRoad:
                {
                    int holes = 3 + (int)(6f * density);
                    for (int i = 0; i < holes; i++)
                    {
                        float a = Range(a0 + 5f, a1);
                        Place(chunk, ObstacleKind.Pothole, a, RandomLateral(chunk, a, 1f));
                    }
                    int pieces = 1 + (int)(4f * d);
                    for (int i = 0; i < pieces; i++)
                    {
                        float a = Range(a0 + 10f, a1 - 10f);
                        Place(chunk, ObstacleKind.BrokenPiece, a, RandomLateral(chunk, a, 1.5f), Range(-0.6f, 0.6f));
                    }
                    break;
                }
                case ChunkKind.Traffic:
                {
                    float hw = chunk.Shape.BaseWidth * 0.5f;
                    float lane = hw - 2.1f;
                    int cars = 1 + (int)(2f * d + (float)_rng.NextDouble());
                    for (int i = 0; i < cars; i++)
                        PlaceMover(chunk, ObstacleMotion.SameDirection, Range(32f, L * 0.5f), lane, _difficulty.TrafficSpeed(d) * Range(0.8f, 1.1f));
                    if (_difficulty.OncomingTraffic(d))
                    {
                        int oncoming = 1 + _rng.Next(0, 2);
                        for (int i = 0; i < oncoming; i++)
                            PlaceMover(chunk, ObstacleMotion.Oncoming, L - Range(32f, L * 0.4f), -lane, _difficulty.TrafficSpeed(d) * Range(0.8f, 1.1f));
                    }
                    break;
                }
                case ChunkKind.Intersection:
                {
                    int cars = d > 0.5f ? 2 : 1;
                    for (int i = 0; i < cars; i++)
                    {
                        float a = L * 0.5f + (i == 0 ? -3f : 3f);
                        float reach = HalfWidthAt(chunk, a) + 8f;
                        var template = new Obstacle
                        {
                            Kind = ObstacleKind.MovingCar,
                            Motion = ObstacleMotion.Cross,
                            ChunkSerial = chunk.Serial,
                            Along = chunk.StartAlong + a,
                            Lateral = _rng.NextDouble() < 0.5 ? -reach : reach,
                            HalfExtents = ObstacleCatalog.DefaultHalfExtents(ObstacleKind.MovingCar),
                            Height = ObstacleCatalog.Height(ObstacleKind.MovingCar),
                            YawOffset = MathF.PI * 0.5f,
                            Speed = Range(7f, 11f) * (1f + 0.3f * d),
                            MotionMin = -reach,
                            MotionMax = reach,
                            WaitTimer = Range(0f, 2f),
                        };
                        int slot = _obstacles.Spawn(template);
                        if (slot < 0) continue;
                        _obstacles.UpdateWorldPose(_road, slot);
                        chunk.ObstacleSlots.Add(slot);
            chunk.ObstacleGenerations.Add(_obstacles.Items[slot].Generation);
                    }
                    break;
                }
                case ChunkKind.Fork:
                {
                    float start = 75f, end = L - 75f;
                    Place(chunk, ObstacleKind.Divider, (start + end) * 0.5f, 0f, 0f, new Vector2(0.4f, (end - start) * 0.5f));
                    Place(chunk, ObstacleKind.Cone, start - 3f, 0f);
                    break;
                }
            }
        }

        void PlaceMover(RoadChunk chunk, ObstacleMotion motion, float localAlong, float lateral, float speed)
        {
            var template = new Obstacle
            {
                Kind = ObstacleKind.MovingCar,
                Motion = motion,
                ChunkSerial = chunk.Serial,
                Along = chunk.StartAlong + localAlong,
                Lateral = lateral,
                HalfExtents = ObstacleCatalog.DefaultHalfExtents(ObstacleKind.MovingCar),
                Height = ObstacleCatalog.Height(ObstacleKind.MovingCar),
                YawOffset = motion == ObstacleMotion.Oncoming ? MathF.PI : 0f,
                Speed = speed,
                MotionMin = chunk.StartAlong + 30f,
                MotionMax = chunk.EndAlong - 30f,
            };
            int slot = _obstacles.Spawn(template);
            if (slot < 0) return;
            _obstacles.UpdateWorldPose(_road, slot);
            chunk.ObstacleSlots.Add(slot);
            chunk.ObstacleGenerations.Add(_obstacles.Items[slot].Generation);
        }
    }
}
