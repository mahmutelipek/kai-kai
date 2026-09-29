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
        public void Bots_DisagreeVisibly_AndRunsSurvive()
        {
            BoardTuningData t = new BoardTuningData();
            BoardSimulation sim = BoardScenario.Create(6, t);
            var bots = new BotBrain[6];
            for (int i = 0; i < 6; i++) bots[i] = BotBrain.Create(BotBrain.DefaultBehaviorForSlot(i), 1000 + i * 17);
            int crashes = 0;
            float wobbleTime = 0f, turningTime = 0f, maxSpeed = 0f;
            ScenarioTrace trace = BoardScenario.Run(sim, 180f, i => bots[i].Decide(new BotContext
            {
                Self = i, Players = sim.Players, ActivePlayerCount = sim.ActivePlayerCount, Board = sim.Board.State,
                Tuning = sim.Tuning, SteerHint = 0f, Time = sim.Time, Dt = BoardScenario.Dt,
            }),
            onStep: (s, ev) =>
            {
                if (ev.Board.Crashed) crashes++;
                BoardState b = s.Board.State;
                if (b.Wobble > 0f) wobbleTime += BoardScenario.Dt;
                if (Math.Abs(b.Steering) > 0.05f) turningTime += BoardScenario.Dt;
                maxSpeed = Math.Max(maxSpeed, b.Speed);
                if (s.RestartDue) s.Restart(b.Position, b.Yaw);
            });
            float std = trace.StdDev(trace.Steering, 0f, 180f);
            TestContext.WriteLine($"6 bots, 180 s (speed ramps to cap): crashes {crashes}, falls {trace.Falls}, steering std {std:F3}, " +
                                  $"turning {turningTime / 180f:P0} of the time, wobbling {wobbleTime / 180f:P0}, max speed {maxSpeed:F1} m/s, staggers {trace.Staggers}");
            Assert.IsTrue(trace.AllFinite);
            Assert.That(std, Is.GreaterThan(0.03f), "bots should push the board around");
            Assert.That(turningTime / 180f, Is.GreaterThan(0.1f));
        }
    }
}
