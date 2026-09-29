using System;
using System.Numerics;
using Game.Simulation;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>Sloped plane with one ramp wedge across the road (headless stand-in for the test-scene ramp).</summary>
    public sealed class RampGround : IGroundProvider
    {
        public float Grade = 0.05f, RampStart = 60f, RampLength = 8f, RampHeight = 1.3f;

        public GroundSample Sample(float x, float z, float searchFromHeight)
        {
            float h = -Grade * z;
            if (z >= RampStart && z <= RampStart + RampLength && Math.Abs(x) < 2.5f)
                h += RampHeight * (z - RampStart) / RampLength;
            return GroundSample.At(h);
        }
    }

    /// <summary>Crash/restart flow, ramps and bot behaviour (M1 build items beyond the numbered acceptance list).</summary>
    public class BoardFlowTests
    {
        [Test]
        public void Ramp_LaunchesLandsAndStaggersWithoutCrash()
        {
            BoardTuningData t = BoardScenario.TuningAtSpeed(18f);
            BoardSimulation sim = BoardScenario.Create(6, t, new RampGround());
            bool left = false, landed = false, hard = false;
            float airStart = -1f, airTime = 0f, landingSpeed = 0f, maxHeightAboveRoad = 0f;
            int staggeredOnLanding = 0;
            ScenarioTrace trace = BoardScenario.Run(sim, 8f, null, onStep: (s, ev) =>
            {
                BoardState b = s.Board.State;
                maxHeightAboveRoad = Math.Max(maxHeightAboveRoad, b.Position.Y - (-0.05f * b.Position.Z));
                if (ev.Board.LeftGround && !left) { left = true; airStart = s.Time; }
                if (ev.Board.Landed && left && !landed)
                {
                    landed = true;
                    airTime = s.Time - airStart;
                    landingSpeed = ev.Board.LandingSpeed;
                    hard = ev.Board.HardLanding;
                    for (int i = 0; i < s.ActivePlayerCount; i++) if (s.Players[i].StaggerTimer > 0f) staggeredOnLanding++;
                }
            });
            TestContext.WriteLine($"ramp @18 m/s: airtime {airTime:F2}s, peak {maxHeightAboveRoad:F2} m above road, landing {landingSpeed:F1} m/s, hard {hard}, staggered {staggeredOnLanding}");
            Assert.IsTrue(left && landed);
            Assert.IsFalse(trace.Crashed);
            Assert.IsTrue(trace.AllFinite);
            Assert.That(airTime, Is.GreaterThan(0.3f));
            if (hard) Assert.That(staggeredOnLanding, Is.GreaterThan(0));
        }

        [Test]
        public void Crash_EjectsPlayers_RestartPutsEveryoneBack()
        {
            BoardTuningData t = BoardScenario.TuningAtSpeed(20f);
            BoardSimulation sim = BoardScenario.Create(6, t);
            BoardScenario.PinAll(sim, BoardScenario.Layout("LLLLLL", t));
            ScenarioTrace trace = BoardScenario.Run(sim, 3f, stopOnCrash: true);
            Assert.IsTrue(trace.Crashed);
            foreach (PlayerSim p in sim.Players) p.Unpin();
            // ejected players do not respawn while the board is down
            BoardScenario.Run(sim, t.crashRestartDelay * 0.5f);
            for (int i = 0; i < 6; i++) Assert.IsFalse(sim.Players[i].IsOnBoard);
            Assert.IsFalse(sim.RestartDue);
            BoardScenario.Run(sim, t.crashRestartDelay * 0.5f + 0.1f);
            Assert.IsTrue(sim.RestartDue);

            sim.Restart(new Vector3(0f, 0f, 0f), 0f);
            Assert.IsFalse(sim.Board.State.Crashed);
            Assert.That(sim.Board.State.Speed, Is.EqualTo(t.startSpeed));
            for (int i = 0; i < 6; i++) Assert.IsTrue(sim.Players[i].IsOnBoard);
            Assert.That(PlayerCrowdSolver.MinimumSeparation(sim.Players, 6, t), Is.GreaterThan(2f * t.playerRadius));
            ScenarioTrace after = BoardScenario.Run(sim, 5f);
            Assert.IsFalse(after.Crashed, "balanced formation after restart must be stable");
        }

        // Solo test mode: one player is the whole weight model.
        [Test]
        public void SoloPlayer_CenterStraight_HalfwayTurns_EdgeCrashes()
        {
            foreach (float speed in new[] { 8f, 20f, 35f })
            {
                ScenarioTrace center = BoardScenario.RunPinned("C", speed, 8f);
                ScenarioTrace half = BoardScenario.RunPinned("r", speed, 10f);
                ScenarioTrace edge = BoardScenario.RunPinned("R", speed, 6f);
                float halfYaw = half.Mean(half.YawRateDeg, 6f, 10f);
                TestContext.WriteLine($"solo @ {speed}: center {center.MaxAbs(center.YawRateDeg, 3f, 8f):F4} deg/s, " +
                                      $"half-way {halfYaw:F2} deg/s (max danger {half.MaxDanger:F2}), edge crash at {edge.CrashTime:F2}s");
                Assert.That(center.MaxAbs(center.YawRateDeg, 3f, 8f), Is.LessThan(1f));
                Assert.IsFalse(half.Crashed, "half-way out must be a controllable turn");
                Assert.That(halfYaw, Is.GreaterThan(2f));
                Assert.IsTrue(edge.Crashed, "standing at the edge alone must crash");
            }
        }

        [Test]
        public void GrowingFromOneToSixPlayers_KeepsEveryoneSafe()
        {
            BoardTuningData t = BoardScenario.TuningAtSpeed(15f);
            BoardSimulation sim = BoardScenario.Create(1, t);
            for (int n = 1; n <= 6; n++)
            {
                sim.SetActivePlayerCount(n);
                Assert.AreEqual(n, sim.ActivePlayerCount);
                Assert.That(PlayerCrowdSolver.MinimumSeparation(sim.Players, n, t), Is.GreaterThan(2f * t.playerRadius), "new player spawned inside someone");
                ScenarioTrace trace = BoardScenario.Run(sim, 2f);
                Assert.IsTrue(trace.AllFinite);
                Assert.IsFalse(trace.Crashed);
            }
        }

        [Test]
        public void PlayerCountChange_RenormalizesSteering()
        {
            BoardTuningData t = new BoardTuningData();
            BoardSimulation sim = BoardScenario.Create(6, t);
            BoardScenario.PinAll(sim, BoardScenario.Layout("RRCCCC", t));
            float six = sim.ComputeWeight().Lateral;
            sim.SetActivePlayerCount(2); // only the two right-edge players remain
            float two = sim.ComputeWeight().Lateral;
            TestContext.WriteLine($"lateral with 6 players {six:F3}, after dropping to 2 {two:F3}");
            Assert.That(two, Is.GreaterThan(six * 2.5f));
            Assert.That(two, Is.EqualTo(BoardScenario.EdgeN).Within(1e-4f));
        }

        [Test]
        public void JumpingPlayersWeighLess()
        {
            BoardTuningData t = new BoardTuningData();
            BoardSimulation sim = BoardScenario.Create(2, t);
            sim.Players[0].PlaceAt(new Vector2(-1f, 0f));
            sim.Players[1].PlaceAt(new Vector2(1f, 0f));
            sim.Players[1].Height = 0.5f; // right player mid-jump
            float lateral = sim.ComputeWeight().Lateral;
            Assert.That(lateral, Is.LessThan(-0.4f), "board should lean to the grounded player");
        }

        [Test]
        public void Bots_LongRunOnFlatPlane_StaysFinite()
        {
            // Disagreement is measured on the real track (TrackRunTests); here: 3 minutes of every behaviour, no NaN.
            BoardTuningData t = new BoardTuningData();
            BoardSimulation sim = BoardScenario.Create(6, t);
            var bots = new BotBrain[6];
            for (int i = 0; i < 6; i++) bots[i] = BotBrain.Create(BotBrain.DefaultBehaviorForSlot(i), 1000 + i * 17);
            ScenarioTrace trace = BoardScenario.Run(sim, 180f, i => bots[i].Decide(new BotContext
            {
                Self = i, Players = sim.Players, ActivePlayerCount = sim.ActivePlayerCount, Board = sim.Board.State,
                Tuning = sim.Tuning, SteerHint = 0f, Time = sim.Time, Dt = BoardScenario.Dt,
            }),
            onStep: (s, ev) => { if (s.RestartDue) s.Restart(s.Board.State.Position, s.Board.State.Yaw); });
            TestContext.WriteLine($"6 bots flat plane 180 s: falls {trace.Falls}, staggers {trace.Staggers}, crashed {trace.Crashed}");
            Assert.IsTrue(trace.AllFinite);
        }
    }
}
