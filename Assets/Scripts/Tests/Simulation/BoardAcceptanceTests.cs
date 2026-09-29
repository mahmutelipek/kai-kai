using System;
using System.Numerics;
using Game.Simulation;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>
    /// Milestone 1 acceptance tests on the engine-independent simulation. Run in Unity's Test Runner
    /// and headless via Tools/Headless (dotnet test).
    /// </summary>
    public class BoardAcceptanceTests
    {
        static readonly float[] Speeds = { 8f, 20f, 35f };

        // ---------- 1. balanced -> straight ----------

        [TestCase("LLLRRR", 8f)]
        [TestCase("LLLRRR", 20f)]
        [TestCase("LLLRRR", 35f)]
        [TestCase("CCCCCC", 20f)]
        [TestCase("CCCCCC", 35f)]
        [TestCase("LRLRLR", 35f)]
        public void A1_BalancedBoard_GoesStraight(string layout, float speed)
        {
            ScenarioTrace pinned = BoardScenario.RunPinned(layout, speed, 8f);
            ScenarioTrace held = BoardScenario.RunHeld(layout, speed, 8f);
            float pinnedMax = pinned.MaxAbs(pinned.YawRateDeg, 3f, 8f);
            float heldMax = held.MaxAbs(held.YawRateDeg, 3f, 8f);
            TestContext.WriteLine($"{layout} @ {speed} m/s: max|yaw rate| pinned {pinnedMax:F4} deg/s, held {heldMax:F4} deg/s");
            Assert.That(pinnedMax, Is.LessThan(1f));
            Assert.That(heldMax, Is.LessThan(1f));
            Assert.IsFalse(pinned.Crashed);
            Assert.IsFalse(held.Crashed);
        }

        [Test]
        public void A1_BalancedWithPositionNoise_StaysBelowOneDegreePerSecond()
        {
            var rng = new Random(1234);
            for (int trial = 0; trial < 20; trial++)
            {
                BoardTuningData t = BoardScenario.TuningAtSpeed(35f);
                BoardSimulation sim = BoardScenario.Create(6, t);
                Vector2[] pos = BoardScenario.Layout("LLLRRR", t);
                for (int i = 0; i < pos.Length; i++)
                    pos[i] += new Vector2((float)(rng.NextDouble() - 0.5) * 0.1f, (float)(rng.NextDouble() - 0.5) * 0.1f); // +-5 cm
                BoardScenario.PinAll(sim, pos);
                ScenarioTrace trace = BoardScenario.Run(sim, 8f);
                Assert.That(trace.MaxAbs(trace.YawRateDeg, 3f, 8f), Is.LessThan(1f), "trial " + trial);
            }
        }

        // ---------- 2. imbalance -> proportional turn, full edge -> wobble then crash ----------

        [Test]
        public void A2_ImbalanceTurnsGraduallyAndProportionally_BothSides()
        {
            foreach (float speed in Speeds)
            {
                ScenarioTrace fourTwo = BoardScenario.RunPinned("LLLLRR", speed, 8f);
                ScenarioTrace fiveOne = BoardScenario.RunPinned("LLLLLR", speed, 8f);
                float y42 = fourTwo.Mean(fourTwo.YawRateDeg, 6f, 8f);
                float y51 = fiveOne.Mean(fiveOne.YawRateDeg, 6f, 8f);
                float rise42 = fourTwo.TimeToReach(fourTwo.YawRateDeg, y42, 0.63f);
                TestContext.WriteLine($"{speed} m/s: 4L/2R {y42:F2} deg/s (63% after {rise42:F2}s), 5L/1R {y51:F2} deg/s");

                Assert.That(y42, Is.LessThan(-1f), "4L/2R must turn left");
                Assert.That(y51, Is.LessThan(y42 * 1.5f), "5L/1R must be clearly stronger than 4L/2R");
                Assert.That(rise42, Is.GreaterThan(0.35f), "turn must build up gradually, not instantly");
                Assert.IsFalse(fourTwo.Crashed);
                Assert.IsFalse(fiveOne.Crashed);

                ScenarioTrace m42 = BoardScenario.RunPinned(BoardScenario.Mirror("LLLLRR"), speed, 8f);
                ScenarioTrace m51 = BoardScenario.RunPinned(BoardScenario.Mirror("LLLLLR"), speed, 8f);
                Assert.That(m42.Mean(m42.YawRateDeg, 6f, 8f), Is.EqualTo(-y42).Within(1e-3f), "mirror 2R/4L");
                Assert.That(m51.Mean(m51.YawRateDeg, 6f, 8f), Is.EqualTo(-y51).Within(1e-3f), "mirror 1L/5R");
            }
        }

        public const float CrashDeadlineSeconds = 3f;

        [Test]
        public void A2_AllFarOneSide_WobblesThenCrashesWithinDeadline_BothSides()
        {
            foreach (string layout in new[] { "LLLLLL", "RRRRRR" })
            foreach (float speed in Speeds)
            {
                ScenarioTrace trace = BoardScenario.RunPinned(layout, speed, 6f);
                TestContext.WriteLine($"{layout} @ {speed}: wobble at {trace.WobbleStartTime:F2}s, crash at {trace.CrashTime:F2}s");
                Assert.IsTrue(trace.Crashed, "must crash");
                Assert.That(trace.WobbleStartTime, Is.GreaterThan(0f), "must wobble first");
                Assert.That(trace.CrashTime - trace.WobbleStartTime, Is.GreaterThan(0.3f), "wobble must be readable before the crash");
                Assert.That(trace.CrashTime, Is.LessThan(CrashDeadlineSeconds));
                float yawSign = layout[0] == 'L' ? -1f : 1f;
                Assert.That(trace.YawRateDeg[trace.YawRateDeg.Count / 2] * yawSign, Is.GreaterThan(0f), "turns toward the loaded side");
            }
        }

        // ---------- 3. front = faster, rear = slower ----------

        [Test]
        public void A3_FrontWeightAccelerates_RearWeightBrakes()
        {
            foreach (float speed in new[] { 12f, 20f })
            {
                ScenarioTrace center = BoardScenario.RunPinned("CCCCCC", speed, 5f);
                ScenarioTrace front = BoardScenario.RunPinned("FFFFFF", speed, 5f);
                ScenarioTrace rear = BoardScenario.RunPinned("BBBBBB", speed, 5f);
                float vc = center.Speed[center.Speed.Count - 1];
                float vf = front.Speed[front.Speed.Count - 1];
                float vr = rear.Speed[rear.Speed.Count - 1];
                TestContext.WriteLine($"start {speed}: after 5 s center {vc:F2}, front {vf:F2}, rear {vr:F2} m/s");
                Assert.That(vf, Is.GreaterThan(vc + 2f));
                Assert.That(vr, Is.LessThan(vc - 2f));
                Assert.IsFalse(front.Crashed);
                Assert.IsFalse(rear.Crashed);
            }
        }

        [Test]
        public void A3_FrontWeightRespectsSoftCap()
        {
            ScenarioTrace front = BoardScenario.RunPinned("FFFFFF", 35f, 20f);
            float v = front.Speed[front.Speed.Count - 1];
            TestContext.WriteLine($"all front at cap: {v:F2} m/s");
            Assert.That(v, Is.LessThan(35f * 1.2f));
        }

        // ---------- 4. two-player mode ----------

        [Test]
        public void A4_TwoPlayers_EdgePlusCenter_IsControllableTurn()
        {
            foreach (float speed in Speeds)
            {
                ScenarioTrace trace = BoardScenario.RunPinned("RC", speed, 20f);
                float yaw = trace.Mean(trace.YawRateDeg, 5f, 20f);
                TestContext.WriteLine($"2p edge+center @ {speed}: {yaw:F2} deg/s, max danger {trace.MaxDanger:F2}");
                Assert.IsFalse(trace.Crashed);
                Assert.That(yaw, Is.GreaterThan(1.5f), "must actually turn");
                Assert.That(trace.MaxDanger, Is.LessThan(0.7f), "must not even wobble");
            }
        }

        [Test]
        public void A4_TwoPlayers_BothAtEdge_CanCrash()
        {
            foreach (float speed in Speeds)
            {
                ScenarioTrace trace = BoardScenario.RunPinned("RR", speed, 6f);
                TestContext.WriteLine($"2p both edge @ {speed}: crash at {trace.CrashTime:F2}s");
                Assert.IsTrue(trace.Crashed);
            }
        }

        [Test]
        public void A4_SteeringIsNormalizedByPlayerCount()
        {
            BoardTuningData t = new BoardTuningData();
            var two = BoardScenario.Create(2, t);
            var six = BoardScenario.Create(6, t);
            BoardScenario.PinAll(two, BoardScenario.Layout("RR", t));
            BoardScenario.PinAll(six, BoardScenario.Layout("RRRRRR", t));
            Assert.That(two.ComputeWeight().RawSteering, Is.EqualTo(six.ComputeWeight().RawSteering).Within(1e-5f));
        }

        // ---------- 5. no NaN, jitter or oscillation ----------

        [Test]
        public void A5_AtRest_NoMotionNoDrift()
        {
            foreach (float speed in Speeds)
            {
                BoardTuningData t = BoardScenario.TuningAtSpeed(speed);
                BoardSimulation sim = BoardScenario.Create(6, t);
                var start = new Vector2[6];
                for (int i = 0; i < 6; i++) start[i] = sim.Players[i].LocalPosition;
                ScenarioTrace trace = BoardScenario.Run(sim, 10f);
                float drift = 0f;
                for (int i = 0; i < 6; i++) drift = Math.Max(drift, Vector2.Distance(start[i], sim.Players[i].LocalPosition));
                TestContext.WriteLine($"rest @ {speed}: max|yaw rate| {trace.MaxAbs(trace.YawRateDeg, 0f, 10f):E2}, max|roll| {trace.MaxAbs(trace.RollDeg, 0f, 10f):E2}, player drift {drift:E2} m");
                Assert.IsTrue(trace.AllFinite);
                Assert.That(trace.MaxAbs(trace.YawRateDeg, 0f, 10f), Is.LessThan(1e-3f));
                Assert.That(trace.MaxAbs(trace.RollDeg, 0f, 10f), Is.LessThan(1e-3f));
                Assert.That(drift, Is.LessThan(1e-4f));
            }
        }

        // Steady turns below the wobble zone must be clean. (Inside the danger zone the wobble is an
        // intentional, visible oscillation - covered by A2 and A5_WobbleOnlyInDangerZone.)
        [TestCase("LLLLLR", 8f)]
        [TestCase("LLLLLR", 20f)]
        [TestCase("LLLLRR", 35f)]
        [TestCase("RC", 35f)]
        public void A5_SteadyTurn_NoOvershootNoOscillation(string layout, float speed)
        {
            foreach (bool held in new[] { false, true })
            {
                ScenarioTrace trace = held ? BoardScenario.RunHeld(layout, speed, 10f) : BoardScenario.RunPinned(layout, speed, 10f);
                float steady = trace.Mean(trace.YawRateDeg, 7f, 10f);
                float peak = trace.MaxAbs(trace.YawRateDeg, 0f, 10f);
                float std = trace.StdDev(trace.YawRateDeg, 4f, 10f);
                int turns = trace.TurningPoints(trace.YawRateDeg, 2f, 1e-4f);
                TestContext.WriteLine($"{layout} {(held ? "held" : "pinned")} @ {speed}: steady {steady:F2}, peak {peak:F2}, std {std:F4} deg/s, turning points {turns}, max danger {trace.MaxDanger:F2}");
                Assert.IsTrue(trace.AllFinite);
                Assert.That(trace.MaxDanger, Is.LessThan(new BoardTuningData().wobbleStartFraction), "test case must be below the wobble zone");
                Assert.That(peak, Is.LessThan(Math.Abs(steady) * 1.05f + 0.05f), "overshoot");
                Assert.That(std, Is.LessThan(0.1f), "jitter");
                Assert.That(turns, Is.LessThanOrEqualTo(2), "oscillation");
            }
        }

        [Test]
        public void A5_WobbleOnlyInDangerZone()
        {
            ScenarioTrace safe = BoardScenario.RunPinned("LLLLRR", 35f, 8f);
            ScenarioTrace edge = BoardScenario.RunPinned("LLLLLL", 20f, 8f);
            Assert.That(safe.WobbleStartTime, Is.LessThan(0f), "no wobble below the threshold");
            Assert.That(edge.WobbleStartTime, Is.GreaterThan(0f));
        }

        [Test]
        public void A5_ChaosFuzz_StaysFinite()
        {
            var rng = new Random(99);
            for (int run = 0; run < 10; run++)
            {
                BoardTuningData t = new BoardTuningData { speedRampPerSecond = 1f };
                BoardSimulation sim = BoardScenario.Create(2 + run % 5, t);
                ScenarioTrace trace = BoardScenario.Run(sim, 60f, i =>
                    new PlayerInputState(new Vector2((float)rng.NextDouble() * 2f - 1f, (float)rng.NextDouble() * 2f - 1f) * 3f,
                                         rng.NextDouble() < 0.05),
                    onStep: (s, ev) => { if (s.RestartDue) s.Restart(s.Board.State.Position, s.Board.State.Yaw); });
                Assert.IsTrue(trace.AllFinite, "run " + run);
            }
        }

        // ---------- 6. no interpenetration, never under the deck, safe respawn ----------

        [Test]
        public void A6_CrowdStress_NoOverlapNoUnderDeckSafeRespawn()
        {
            BoardTuningData t = BoardScenario.TuningAtSpeed(20f);
            BoardSimulation sim = BoardScenario.Create(6, t);
            var rng = new Random(7);
            float worstRespawnGap = float.PositiveInfinity;
            float longestFall = 0f;
            var fallStart = new float[6];
            for (int i = 0; i < 6; i++) fallStart[i] = -1f;

            // everyone sprints at the centre (or a random spot), jumping randomly: maximum pile-up
            ScenarioTrace trace = BoardScenario.Run(sim, 120f, i =>
            {
                PlayerSim p = sim.Players[i];
                Vector2 target = (sim.Time % 6f) < 3f ? Vector2.Zero
                    : new Vector2((float)rng.NextDouble() * 2.6f - 1.3f, (float)rng.NextDouble() * 6.4f - 3.2f);
                Vector2 d = target - p.LocalPosition;
                return new PlayerInputState(d.Length() > 1e-3f ? Vector2.Normalize(d) : Vector2.Zero, rng.NextDouble() < 0.02);
            },
            onStep: (s, ev) =>
            {
                for (int i = 0; i < s.ActivePlayerCount; i++)
                {
                    PlayerSim p = s.Players[i];
                    if (!p.IsOnBoard && fallStart[i] < 0f) fallStart[i] = s.Time;
                    if (p.IsOnBoard && fallStart[i] >= 0f)
                    {
                        longestFall = Math.Max(longestFall, s.Time - fallStart[i]);
                        fallStart[i] = -1f;
                    }
                    if (p.IsOnBoard) Assert.That(p.Height, Is.GreaterThanOrEqualTo(0f), "under the deck");
                    // airborne players may hang over the edge; they fall only if they land outside
                    if (p.IsOnBoard && !p.IsAirborne)
                    {
                        Assert.That(Math.Abs(p.LocalPosition.X), Is.LessThanOrEqualTo(t.HalfWidth + 1e-4f));
                        Assert.That(Math.Abs(p.LocalPosition.Y), Is.LessThanOrEqualTo(t.HalfLength + 1e-4f));
                    }
                }
                if (ev.PlayersRespawned > 0)
                    worstRespawnGap = Math.Min(worstRespawnGap, PlayerCrowdSolver.MinimumSeparation(s.Players, s.ActivePlayerCount, t));
            });

            float diameter = 2f * t.playerRadius;
            TestContext.WriteLine($"crowd stress: min separation {trace.MinPlayerSeparation:F3} m (diameter {diameter:F2}), falls {trace.Falls}, respawns {trace.Respawns}, staggers {trace.Staggers}, longest time off board {longestFall:F2}s");
            Assert.IsTrue(trace.AllFinite);
            Assert.That(trace.MinPlayerSeparation, Is.GreaterThan(diameter * 0.9f), "players stuck inside each other");
            Assert.That(trace.MinPlayerHeight, Is.GreaterThanOrEqualTo(0f));
            Assert.That(longestFall, Is.LessThanOrEqualTo(t.respawnDelay + 0.05f), "respawn delay");
        }

        [Test]
        public void A6_CoincidentPlayers_AreSeparated()
        {
            BoardTuningData t = new BoardTuningData();
            BoardSimulation sim = BoardScenario.Create(6, t);
            for (int i = 0; i < 6; i++) sim.Players[i].PlaceAt(Vector2.Zero);
            ScenarioTrace trace = BoardScenario.Run(sim, 1f);
            Assert.That(PlayerCrowdSolver.MinimumSeparation(sim.Players, 6, t), Is.GreaterThan(2f * t.playerRadius * 0.95f));
            Assert.IsTrue(trace.AllFinite);
        }
    }
}
