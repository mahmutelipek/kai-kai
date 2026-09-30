using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using Game.Art;
using Game.Simulation;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>
    /// Milestone 4 checks that run without Unity: the art library builds, the six riders are distinct by
    /// construction (key colours and headwear), chunk geometry is deterministic, allocation-free once warm and
    /// within a triangle budget, the palette fits one texture, and the tuning is pinned. Screenshot-based checks
    /// (silhouettes, reaction distance) live in Tools/ArtPreview and are reported in Docs/M4_REPORT.md.
    /// </summary>
    public class ArtAcceptanceTests
    {
        static float Distance(ArtColor a, ArtColor b) =>
            MathF.Sqrt((a.R - b.R) * (a.R - b.R) + (a.G - b.G) * (a.G - b.G) + (a.B - b.B) * (a.B - b.B));

        [Test]
        public void M4_1_SixRiders_HaveDistinctKeyColoursAndHeadwear()
        {
            var headwear = new HashSet<ArtLibrary.Headwear>();
            float minShirt = float.MaxValue;
            for (int a = 0; a < ArtLibrary.CharacterCount; a++)
            {
                ArtLibrary.Outfit oa = ArtLibrary.OutfitFor(a);
                Assert.IsTrue(headwear.Add(oa.Headwear), $"{oa.Name}: headwear must be unique (silhouette from behind)");
                for (int b = a + 1; b < ArtLibrary.CharacterCount; b++)
                    minShirt = Math.Min(minShirt, Distance(oa.Shirt, ArtLibrary.OutfitFor(b).Shirt));
            }
            TestContext.WriteLine($"min shirt colour distance between riders: {minShirt:0.00} (RGB 0..1)");
            // closest pair is P4 yellow / P6 orange (both from the reference); they also differ in headwear, hair and skin
            Assert.That(minShirt, Is.GreaterThan(0.2f), "shirt colours must be clearly different");
        }

        [Test]
        public void M4_1_Riders_BuildWithTheSharedRig()
        {
            for (int i = 0; i < ArtLibrary.CharacterCount; i++)
            {
                ArtModel m = ArtLibrary.Character(i);
                var names = new HashSet<string>();
                foreach (ArtGroup g in m.Groups) names.Add(g.Name);
                foreach (string required in new[] { "Pose", "Torso", "Head", "ArmL", "ArmR" })
                    Assert.IsTrue(names.Contains(required), $"{m.Name} lacks group {required}");
                int inPose = 0;
                foreach (ArtPart p in m.Parts)
                {
                    Assert.IsTrue(p.Group.Length > 0, $"{m.Name}: part {p.Name} must hang under the rig");
                    if (p.Group == "Pose") inPose++;
                }
                Assert.That(inPose, Is.GreaterThan(5), "legs and shoes stay on the Pose group");
                var set = new MeshSet();
                set.Add(m, Vector3.Zero, Quaternion.Identity, 1f, RiderPose.AsLookup(RiderPose.Compute(new RiderPoseInput { SpeedNorm = 1f, Wobble = 1f })), null);
                Assert.That(set.VertexCount, Is.GreaterThan(500).And.LessThan(20000), $"{m.Name} vertex count");
            }
        }

        [Test]
        public void M4_ArtLibrary_BuildsEveryObstacleAndPickup()
        {
            foreach (ObstacleKind kind in Enum.GetValues(typeof(ObstacleKind)))
            for (int v = 0; v < ArtLibrary.ObstacleVariants(kind); v++)
            {
                ArtModel m = ArtLibrary.Obstacle(kind, v);
                Assert.That(m.Parts.Count, Is.GreaterThan(0), kind.ToString());
                bool sized = kind == ObstacleKind.ConcreteBarrier || kind == ObstacleKind.ConstructionBarrier || kind == ObstacleKind.Pothole || kind == ObstacleKind.Divider;
                if (sized) Assert.IsTrue(m.Groups.Exists(g => g.Name == "Scaled"), $"{kind} must put its geometry in the Scaled group");
            }
            foreach (PickupKind kind in Enum.GetValues(typeof(PickupKind)))
                Assert.That(ArtLibrary.Pickup(kind).Parts.Count, Is.GreaterThan(0), kind.ToString());
        }

        [Test]
        public void M4_2_ChunkGeometry_DeterministicAllocationFreeAndWithinBudget()
        {
            var t = new BoardTuningData { livesPerRun = 0 };
            var run = new RunSimulation(t, 11, 6);
            var set = new MeshSet();
            // warm up on every chunk the generator produced
            int maxVerts = 0, maxTris = 0;
            foreach (RoadChunk c in run.Road.Chunks)
            {
                set.Clear();
                ChunkGeometry.Build(c, set);
                int v = 0, tr = 0;
                foreach (var kv in set.ByColor) { v += kv.Value.VertexCount; tr += kv.Value.Triangles.Count / 3; }
                maxVerts = Math.Max(maxVerts, v);
                maxTris = Math.Max(maxTris, tr);
            }
            RoadChunk first = run.Road.Chunks[0];
            set.Clear();
            ChunkGeometry.Build(first, set);
            int before = set.VertexCount;

            long alloc = GC.GetAllocatedBytesForCurrentThread();
            for (int k = 0; k < 5; k++)
            {
                set.Clear();
                ChunkGeometry.Build(first, set);
            }
            alloc = GC.GetAllocatedBytesForCurrentThread() - alloc;
            TestContext.WriteLine($"chunk geometry: max {maxVerts} verts / {maxTris} tris per chunk, rebuild allocations {alloc} bytes");
            Assert.AreEqual(before, set.VertexCount, "same chunk, same geometry");
            Assert.AreEqual(0, alloc, "rebuilding a chunk must not allocate once warm");
            Assert.That(maxTris, Is.LessThan(60000), "triangle budget per chunk");
        }

        [Test]
        public void M4_Palette_FitsOneTexture()
        {
            var set = new MeshSet();
            for (int i = 0; i < ArtLibrary.CharacterCount; i++) set.Add(ArtLibrary.Character(i), Vector3.Zero, Quaternion.Identity);
            foreach (ObstacleKind kind in Enum.GetValues(typeof(ObstacleKind)))
                for (int v = 0; v < ArtLibrary.ObstacleVariants(kind); v++) set.Add(ArtLibrary.Obstacle(kind, v), Vector3.Zero, Quaternion.Identity);
            foreach (PickupKind kind in Enum.GetValues(typeof(PickupKind))) set.Add(ArtLibrary.Pickup(kind), Vector3.Zero, Quaternion.Identity);
            MeshSet backdrop = Backdrop.Build();
            var run = new RunSimulation(new BoardTuningData { livesPerRun = 0 }, 3, 1);
            foreach (RoadChunk c in run.Road.Chunks) ChunkGeometry.Build(c, set);
            foreach (var kv in set.ByColor) Palette.IndexOf(kv.Key);
            foreach (var kv in backdrop.ByColor) Palette.IndexOf(kv.Key);
            TestContext.WriteLine($"palette colours in use: {Palette.Count} of {Palette.Capacity}");
            Assert.That(Palette.Count, Is.LessThan(Palette.Capacity));
        }

        /// <summary>
        /// M4 acceptance 4 ("art work does not change the feel"): every tuning default is pinned here. The only
        /// changes vs M3 are the ones the user asked for ("mechanics more dynamic"), listed in M3ToM4Changes.
        /// </summary>
        [Test]
        public void M4_4_TuningIsPinned_OnlyRequestedChangesSinceM3()
        {
            var expectedChanges = new Dictionary<string, (float m3, float m4)>
            {
                ["steeringSmoothingTime"] = (0.4f, 0.28f),
                ["yawRateBaseDeg"] = (20f, 24f),
                ["yawResponseTime"] = (0.25f, 0.18f),
                ["maxRollDeg"] = (18f, 24f),
                ["rollResponseTime"] = (0.2f, 0.15f),
                ["startSpeed"] = (8f, 12f),
                ["speedRampPerSecond"] = (0.25f, 0.4f),
                ["softCapSpeed"] = (35f, 38f),
                ["frontAcceleration"] = (4f, 6f),
                ["playerMoveSpeed"] = (3.6f, 4.2f),
                ["respawnSpeedFraction"] = (0.6f, 0.7f),
                // M4 follow-up ("more playable"): friendlier near misses and combo idle time
                ["nearMissDistance"] = (1.2f, 1.5f),
                ["comboIdleTime"] = (4f, 5f),
                // M4.5 ("the board should be bigger next to the riders")
                ["boardLength"] = (6f, 6.75f),
                ["boardWidth"] = (2.4f, 2.7f),
            };
            var d = new BoardTuningData();
            float Get(string name) => (float)typeof(BoardTuningData).GetField(name).GetValue(d);
            foreach (var kv in expectedChanges) Assert.AreEqual(kv.Value.m4, Get(kv.Key), 1e-6, kv.Key);

            // fields added after M3 (M4.3 moves) are new, not changes
            var added = new HashSet<string>
            {
                "ollieVelocity", "perfectOllieVelocity", "ollieWindow", "ollieCrewFraction", "ollieCooldown",
                "carveMinSteering", "carveMinTime", "carveBoostSpeed", "carveBoostTime", "carveBoostAcceleration",
                "slipstreamDistance", "slipstreamWidth", "slipstreamBonus", "slipstreamBuildTime",
                "olliePoints", "carveBoostPoints", "slipstreamPoints",
            };
            // everything else must still equal the M3 defaults (hash of the remaining fields)
            double sum = 0;
            int n = 0;
            foreach (FieldInfo f in typeof(BoardTuningData).GetFields())
            {
                if (f.FieldType != typeof(float) || expectedChanges.ContainsKey(f.Name) || added.Contains(f.Name)) continue;
                sum += (float)f.GetValue(d) * (++n % 7 + 1);
            }
            TestContext.WriteLine($"unchanged tuning fields: {n}, weighted sum {sum:R}");
            Assert.AreEqual(M3UnchangedFieldCount, n, "a tuning field was added or removed");
            Assert.AreEqual(M3UnchangedWeightedSum, sum, 1e-3, "a tuning value changed that the user did not ask for");
        }

        /// <summary>Old Unity tuning assets keep stale defaults: the migration upgrades exactly the changed fields, keeps hand-tuned ones.</summary>
        [Test]
        public void TuningMigration_UpgradesStaleDefaults_KeepsHandTunedValues()
        {
            var old = new BoardTuningData { boardLength = 6f, boardWidth = 2.4f, startSpeed = 8f, softCapSpeed = 33f, comboIdleTime = 4f };
            int n = TuningMigration.Upgrade(old);
            var current = new BoardTuningData();
            Assert.AreEqual(4, n);
            Assert.AreEqual(current.boardLength, old.boardLength);
            Assert.AreEqual(current.boardWidth, old.boardWidth);
            Assert.AreEqual(current.startSpeed, old.startSpeed);
            Assert.AreEqual(current.comboIdleTime, old.comboIdleTime);
            Assert.AreEqual(33f, old.softCapSpeed, "a hand-tuned value is kept");
            Assert.AreEqual(0, TuningMigration.Upgrade(old), "idempotent");
            Assert.AreEqual(0, TuningMigration.Upgrade(new BoardTuningData()), "current defaults need nothing");
            foreach ((string field, float[] olds) in TuningMigration.History)
                Assert.IsNotNull(typeof(BoardTuningData).GetField(field), field);
        }

        // From the M3 defaults (tag m3-done, commit 0fededf) excluding the fields listed above;
        // `git diff 0fededf -- BoardTuningData.cs` shows only those fields changed.
        // (M4.5: boardLength / boardWidth moved to the requested changes: 68 fields remain, same values as at M3)
        const int M3UnchangedFieldCount = 68;
        const double M3UnchangedWeightedSum = 42179.68000065535;

        [Test]
        public void M4_RiderCountKeys_OneLeavesExactlyOneRider()
        {
            var sim = new BoardSimulation(new BoardTuningData(), new SlopedPlaneGround(0.03f), 6);
            for (int count = 6; count >= 1; count--)
            {
                sim.SetActivePlayerCount(count);
                Assert.AreEqual(count, sim.ActivePlayerCount);
            }
            sim.SetActivePlayerCount(1);
            var none = new PlayerInputState[BoardSimulation.MaxPlayers];
            for (int i = 0; i < 120; i++) sim.Step(1f / 60f, none);
            Assert.AreEqual(1, sim.ActivePlayerCount, "one rider keeps riding alone");
            Assert.IsFalse(sim.Board.State.Crashed);
        }
    }
}
