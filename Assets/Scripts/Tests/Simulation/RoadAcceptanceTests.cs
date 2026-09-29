using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Text;
using Game.Simulation;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>Milestone 2 acceptance: endless road soak, ideal driver, difficulty curve, allocations / step time.</summary>
    public class RoadAcceptanceTests
    {
        static readonly int[] Seeds = { 1, 7, 42, 1234, 99991 };
        const float SoakDistance = 10000f;

        // ------------------------------------------------------------------ 1. soak + validity at every join

        sealed class SoakStats
        {
            public int Chunks, Joins, ObstaclesRemoved, MaxActiveChunks, ChunkObjectsCreated;
            public float MaxPosGap, MaxYawGap, MaxWidthGap, MaxHeightGap, MaxAlongGap, MaxHeadingDeg, MinChunkClearance = float.PositiveInfinity;
            public readonly Dictionary<ChunkKind, int> KindCounts = new Dictionary<ChunkKind, int>();
            public readonly List<string> Errors = new List<string>();
        }

        static SoakStats Soak(int seed, List<ChunkLogEntry> log)
        {
            var t = new BoardTuningData();
            var road = new RoadModel();
            var obstacles = new ObstacleField();
            var generator = new RoadGenerator(t, road, obstacles, new DifficultyManager(t)) { Log = log };
            generator.Reset(seed);
            var stats = new SoakStats();
            var validated = new HashSet<int>();

            for (float along = 0f; along <= SoakDistance; along += 5f)
            {
                generator.Update(along);
                stats.MaxActiveChunks = Math.Max(stats.MaxActiveChunks, road.Chunks.Count);
                for (int i = 0; i < road.Chunks.Count; i++)
                {
                    RoadChunk c = road.Chunks[i];
                    if (!validated.Add(c.Serial)) continue;
                    ValidateChunk(c, t, obstacles, stats);
                    if (i > 0) ValidateJoin(road.Chunks[i - 1], c, stats);
                    for (int j = 0; j < i - 1; j++) ValidateNoOverlap(road.Chunks[j], c, stats);
                }
            }
            stats.Chunks = validated.Count;
            stats.ChunkObjectsCreated = generator.ChunksCreated;
            foreach (ChunkLogEntry e in log)
            {
                stats.ObstaclesRemoved += e.ObstaclesRemovedForTraversability;
                stats.KindCounts.TryGetValue(e.Kind, out int n);
                stats.KindCounts[e.Kind] = n + 1;
            }
            return stats;
        }

        static void ValidateJoin(RoadChunk a, RoadChunk b, SoakStats s)
        {
            s.Joins++;
            s.MaxPosGap = Math.Max(s.MaxPosGap, Vector3.Distance(a.Points[a.SampleCount - 1], b.Points[0]));
            s.MaxYawGap = Math.Max(s.MaxYawGap, Math.Abs(SimMath.WrapAngle(a.Yaws[a.SampleCount - 1] - b.Yaws[0])));
            s.MaxWidthGap = Math.Max(s.MaxWidthGap, Math.Abs(a.HalfWidths[a.SampleCount - 1] - b.HalfWidths[0]) * 2f);
            s.MaxHeightGap = Math.Max(s.MaxHeightGap, Math.Abs(a.Points[a.SampleCount - 1].Y - b.Points[0].Y));
            s.MaxAlongGap = Math.Max(s.MaxAlongGap, Math.Abs(a.EndAlong - b.StartAlong));
            // the drivable line must continue across the join
            float lineGap = Math.Abs(a.PlannedLateral[a.PlannedLateral.Count - 1] - b.PlannedLateral[0]);
            if (lineGap > 1e-3f) s.Errors.Add($"planned line jumps {lineGap:F3} m at join {a.Serial}->{b.Serial}");
        }

        static void ValidateChunk(RoadChunk c, BoardTuningData t, ObstacleField obstacles, SoakStats s)
        {
            if (c.PlannedLateral.Count != c.SampleCount) { s.Errors.Add($"chunk {c.Serial} {c.Kind}: no traversable line"); return; }
            for (int i = 0; i < c.SampleCount; i++)
            {
                s.MaxHeadingDeg = Math.Max(s.MaxHeadingDeg, Math.Abs(c.Yaws[i]) * SimMath.Rad2Deg);
                if (i > 0 && c.Points[i].Z <= c.Points[i - 1].Z) s.Errors.Add($"chunk {c.Serial}: no downhill progress at sample {i}");
                if (!SimMath.IsFinite(c.Points[i])) s.Errors.Add($"chunk {c.Serial}: non-finite point");
            }
            // independent re-check of the planned line: on the asphalt, slope-limited, clear of blocking obstacles
            for (int i = 0; i < c.SampleCount; i++)
            {
                float x = c.PlannedLateral[i];
                float along = c.StartAlong + c.LocalAlongAt(i);
                if (Math.Abs(x) + t.HalfWidth > c.HalfWidths[i] + 1e-3f) s.Errors.Add($"chunk {c.Serial} {c.Kind}: line off the asphalt at {along:F0}");
                if (i > 0 && Math.Abs(x - c.PlannedLateral[i - 1]) > RoadChunk.SampleSpacing * RoadPlanner.LateralSlope + 1e-3f)
                    s.Errors.Add($"chunk {c.Serial}: line too steep at {along:F0}");
                for (int k = 0; k < c.ObstacleSlots.Count; k++)
                {
                    if (!c.Owns(obstacles, k)) continue;
                    ref Obstacle o = ref obstacles.Items[c.ObstacleSlots[k]];
                    if (!ObstacleCatalog.BlocksCorridor(o.Kind) || o.Motion == ObstacleMotion.Cross) continue;
                    float extLat = o.HalfExtents.X * MathF.Abs(MathF.Cos(o.YawOffset)) + o.HalfExtents.Y * MathF.Abs(MathF.Sin(o.YawOffset));
                    float extAlong = o.HalfExtents.X * MathF.Abs(MathF.Sin(o.YawOffset)) + o.HalfExtents.Y * MathF.Abs(MathF.Cos(o.YawOffset));
                    bool alongOverlap = o.Motion == ObstacleMotion.Static
                        ? Math.Abs(along - o.Along) <= extAlong + t.HalfLength
                        : along >= o.MotionMin - t.HalfLength && along <= o.MotionMax + t.HalfLength;
                    if (alongOverlap && Math.Abs(x - o.Lateral) < extLat + t.HalfWidth)
                        s.Errors.Add($"chunk {c.Serial} {c.Kind}: line hits {o.Kind} at {along:F0}");
                }
            }
        }

        static void ValidateNoOverlap(RoadChunk older, RoadChunk newer, SoakStats s)
        {
            // non-adjacent chunks: the asphalt (plus shoulders) must never touch
            for (int i = 0; i < newer.SampleCount; i += 2)
            for (int j = 0; j < older.SampleCount; j += 2)
            {
                Vector3 a = newer.Points[i], b = older.Points[j];
                float d = new Vector2(a.X - b.X, a.Z - b.Z).Length();
                float clearance = d - newer.HalfWidths[i] - older.HalfWidths[j];
                s.MinChunkClearance = Math.Min(s.MinChunkClearance, clearance);
                if (clearance < 2f) { s.Errors.Add($"chunks {older.Serial} and {newer.Serial} overlap"); return; }
            }
        }

        [Test]
        public void M2_1_Soak10km_EveryJoinValid_NoGapsNoOverlapsAllReachable()
        {
            foreach (int seed in Seeds)
            {
                var log = new List<ChunkLogEntry>();
                SoakStats s = Soak(seed, log);
                var kinds = new StringBuilder();
                foreach (var kv in s.KindCounts) kinds.Append($"{kv.Key}:{kv.Value} ");
                TestContext.WriteLine($"seed {seed}: {s.Chunks} chunks / {s.Joins} joins over {SoakDistance:0} m. " +
                                      $"max gaps pos {s.MaxPosGap:E1} m, yaw {s.MaxYawGap:E1} rad, width {s.MaxWidthGap:E1} m, height {s.MaxHeightGap:E1} m, along {s.MaxAlongGap:E1} m; " +
                                      $"max heading {s.MaxHeadingDeg:F1} deg; min clearance between non-adjacent chunks {s.MinChunkClearance:F1} m; " +
                                      $"obstacles removed to keep it traversable {s.ObstaclesRemoved}; active chunks max {s.MaxActiveChunks}, chunk objects ever created {s.ChunkObjectsCreated}");
                TestContext.WriteLine("   kinds: " + kinds);
                for (int i = 0; i < Math.Min(5, s.Errors.Count); i++) TestContext.WriteLine("   ERROR " + s.Errors[i]);

                Assert.AreEqual(0, s.Errors.Count, "validity errors");
                Assert.That(s.MaxPosGap, Is.LessThan(1e-3f), "gap between chunks");
                Assert.That(s.MaxYawGap, Is.LessThan(1e-4f), "kink between chunks");
                Assert.That(s.MaxWidthGap, Is.LessThan(1e-3f), "width jump");
                Assert.That(s.MaxHeightGap, Is.LessThan(1e-3f), "step between chunks");
                Assert.That(s.MaxAlongGap, Is.LessThan(1e-2f), "distance bookkeeping");
                Assert.That(s.MaxHeadingDeg, Is.LessThanOrEqualTo(RoadGenerator.MaxHeadingDeg + 0.5f));
                Assert.That(s.ChunkObjectsCreated, Is.LessThanOrEqualTo(RoadGenerator.PrewarmChunks), "chunks must come from the pre-filled pool");
                Assert.That(s.KindCounts.Count, Is.GreaterThanOrEqualTo(12), "most chunk types should appear within 10 km");
            }
        }

        // ------------------------------------------------------------------ 2. ideal driver

        public sealed class DriveResult
        {
            public float Distance, Time, MaxSpeed, AvgSpeed, MaxDanger;
            public int Crashes, Light, Bumps, Heavy, Walls, Falls;
            public override string ToString() =>
                $"{Distance:0} m in {Time:0} s (avg {AvgSpeed:0.0}, max {MaxSpeed:0.0} m/s), crashes {Crashes}, hits light/bump/heavy/wall {Light}/{Bumps}/{Heavy}/{Walls}, falls {Falls}, max danger {MaxDanger:0.00}";
        }

        public static DriveResult DriveIdeal(int seed, float distance, int players = 6, Action<RunSimulation> perStep = null)
        {
            var t = new BoardTuningData();
            var run = new RunSimulation(t, seed, players);
            var driver = new IdealDriver(run);
            var none = new PlayerInputState[BoardSimulation.MaxPlayers];
            var r = new DriveResult();
            float dt = BoardScenario.Dt;
            while (run.Distance < distance && r.Time < distance / 5f)
            {
                driver.Drive(dt);
                RunStepEvents ev = run.Step(dt, none);
                perStep?.Invoke(run);
                r.Time += dt;
                BoardState b = run.Board.Board.State;
                if (ev.Crashed) r.Crashes++;
                r.Light += ev.LightHits; r.Bumps += ev.Bumps; r.Heavy += ev.HeavyHits; r.Walls += ev.WallScrapes;
                r.Falls += ev.Sim.PlayersFell;
                r.MaxSpeed = Math.Max(r.MaxSpeed, b.Speed);
                r.MaxDanger = Math.Max(r.MaxDanger, b.Danger);
            }
            r.Distance = run.Distance;
            r.AvgSpeed = r.Distance / Math.Max(r.Time, 1e-3f);
            return r;
        }

        [Test]
        public void M2_2_IdealDriver_Completes10km_WithoutCrashing()
        {
            foreach (int seed in Seeds)
            {
                DriveResult r = DriveIdeal(seed, SoakDistance);
                TestContext.WriteLine($"seed {seed}: {r}");
                Assert.That(r.Distance, Is.GreaterThanOrEqualTo(SoakDistance));
                Assert.AreEqual(0, r.Crashes, "ideal driver crashed");
            }
        }

        // ------------------------------------------------------------------ 3. difficulty curve

        [Test]
        public void M2_3_DifficultyCurve_TrendsUpward()
        {
            var log = new List<ChunkLogEntry>();
            Soak(42, log);
            const float bin = 1000f;
            int bins = (int)(SoakDistance / bin);
            var sum = new float[bins]; var count = new int[bins];
            double sx = 0, sy = 0, sxx = 0, sxy = 0;
            var csv = new StringBuilder("start_along_m,chunk,kind,length_m,chunk_difficulty,difficulty_level,obstacles\n");
            foreach (ChunkLogEntry e in log)
            {
                if (e.StartAlong >= SoakDistance) continue;
                int b = Math.Min(bins - 1, (int)(e.StartAlong / bin));
                sum[b] += e.Difficulty; count[b]++;
                sx += e.StartAlong; sy += e.Difficulty; sxx += e.StartAlong * e.StartAlong; sxy += e.StartAlong * e.Difficulty;
                csv.Append($"{e.StartAlong:0},{e.Serial},{e.Kind},{e.Length:0},{e.Difficulty:0.00},{e.DifficultyLevel:0.000},{e.Obstacles}\n");
            }
            int n = log.Count;
            double slope = (n * sxy - sx * sy) / (n * sxx - sx * sx);
            var table = new StringBuilder();
            for (int b = 0; b < bins; b++) table.Append($"{b * bin / 1000f:0}-{(b + 1) * bin / 1000f:0} km: {(count[b] > 0 ? sum[b] / count[b] : 0f):0.00}   ");
            TestContext.WriteLine("mean chunk difficulty per km: " + table);
            TestContext.WriteLine($"linear trend: +{slope * 1000:0.000} difficulty per km over {n} chunks");

            string dir = Environment.GetEnvironmentVariable("DOWNHILL_REPORT_DIR");
            if (!string.IsNullOrEmpty(dir)) File.WriteAllText(Path.Combine(dir, "M2_difficulty_curve.csv"), csv.ToString());

            Assert.That(slope, Is.GreaterThan(0.0));
            Assert.That(sum[bins - 1] / count[bins - 1], Is.GreaterThan(sum[0] / count[0] + 2f), "late road must be clearly harder");
        }

        // ------------------------------------------------------------------ 4. allocations and step time

        [Test]
        public void M2_4_SteadyState_NoAllocations_StepTimeStats()
        {
            var t = new BoardTuningData();
            var run = new RunSimulation(t, 42, 6);
            var driver = new IdealDriver(run);
            var none = new PlayerInputState[BoardSimulation.MaxPlayers];
            float dt = BoardScenario.Dt;
            while (run.Distance < 1500f) { driver.Drive(dt); run.Step(dt, none); } // warm-up: pools reach their size

            const int steps = 20000;
            var times = new double[steps];
            long allocated = 0;
            long maxStepAlloc = 0;
            var sw = new Stopwatch();
            for (int i = 0; i < steps; i++)
            {
                driver.Drive(dt);
                long before = GC.GetAllocatedBytesForCurrentThread();
                sw.Restart();
                run.Step(dt, none);
                sw.Stop();
                long a = GC.GetAllocatedBytesForCurrentThread() - before;
                allocated += a;
                maxStepAlloc = Math.Max(maxStepAlloc, a);
                times[i] = sw.Elapsed.TotalMilliseconds;
            }
            Array.Sort(times);
            double mean = 0; foreach (double v in times) mean += v; mean /= steps;
            TestContext.WriteLine($"{steps} steps ({steps * dt:0} s of play, {run.Distance - 1500f:0} m incl. chunk spawning): " +
                                  $"allocated {allocated} bytes total, worst step {maxStepAlloc} bytes; step time mean {mean * 1000:0} us, " +
                                  $"p99 {times[(int)(steps * 0.99)] * 1000:0} us, max {times[steps - 1] * 1000:0} us (budget at 60 Hz: 16667 us)");
            Assert.AreEqual(0, allocated, "simulation + generator + pooling must not allocate in steady state");
            Assert.That(times[(int)(steps * 0.99)], Is.LessThan(2.0), "p99 step time must stay far below the frame budget");
        }
    }
}
