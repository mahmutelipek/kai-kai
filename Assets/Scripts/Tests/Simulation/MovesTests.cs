using System;
using System.Numerics;
using Game.Simulation;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>
    /// M4.3 moves that make the ride feel like a game: crew ollie (jump together -> the board hops), carve boost
    /// (hold a hard carve, straighten out -> speed burst), slipstream (tuck behind a car -> extra speed).
    /// </summary>
    public class MovesTests
    {
        static PlayerInputState Jump => new PlayerInputState(Vector2.Zero, true, false);

        [Test]
        public void CrewOllie_EnoughRidersJumpingTogether_HopsTheBoard()
        {
            var t = BoardScenario.TuningAtSpeed(20f);
            BoardSimulation sim = BoardScenario.Create(6, t);
            var inputs = new PlayerInputState[6];
            for (int i = 0; i < 60; i++) sim.Step(BoardScenario.Dt, inputs); // settle
            Assert.IsTrue(sim.Board.State.Grounded);

            // two of six jumping is not enough (60 % needed)
            inputs[0] = Jump; inputs[1] = Jump;
            bool ollie = false;
            for (int i = 0; i < 20; i++) ollie |= sim.Step(BoardScenario.Dt, inputs).Ollie;
            Assert.IsFalse(ollie, "2 of 6 riders do not lift a giant board");

            // everyone jumps together: a perfect ollie, the board leaves the ground and lands again
            for (int i = 0; i < 90; i++) sim.Step(BoardScenario.Dt, new PlayerInputState[6]); // cooldowns
            for (int i = 0; i < 6; i++) inputs[i] = Jump;
            SimStepEvents ev = default;
            bool perfect = false; float maxHeight = 0f, startY = sim.Board.State.Position.Y;
            int steps = 0;
            for (; steps < 180; steps++)
            {
                ev = sim.Step(BoardScenario.Dt, inputs);
                if (steps == 0) for (int i = 0; i < 6; i++) inputs[i] = default;
                perfect |= ev.PerfectOllie;
                maxHeight = Math.Max(maxHeight, sim.Board.State.Position.Y - (startY - t.startSpeed * 0f));
                if (steps > 5 && sim.Board.State.Grounded) break;
            }
            float slopeDrop = 0.05f * 20f * steps * BoardScenario.Dt; // SlopedPlaneGround grade 0.05
            TestContext.WriteLine($"perfect ollie: air {steps * BoardScenario.Dt:0.00} s");
            Assert.IsTrue(perfect, "all six jumped together");
            Assert.That(steps * BoardScenario.Dt, Is.GreaterThan(0.5f), "the board was airborne for a real hop");
            Assert.IsFalse(sim.Board.State.Crashed);
        }

        /// <summary>Humans never press in the same frame: late riders joining right after the pop still make it perfect.</summary>
        [Test]
        public void CrewOllie_LateRidersWithinGrace_UpgradeToPerfect_TooLateStaysNormal()
        {
            float Air(float lateBy, out bool ollie, out bool perfect, out int upgrades)
            {
                var t = BoardScenario.TuningAtSpeed(20f);
                BoardSimulation sim = BoardScenario.Create(6, t);
                var inputs = new PlayerInputState[6];
                for (int i = 0; i < 60; i++) sim.Step(BoardScenario.Dt, inputs);
                ollie = perfect = false; upgrades = 0;
                int late = (int)Math.Round(lateBy / BoardScenario.Dt), steps = 0;
                for (; steps < 240; steps++)
                {
                    for (int i = 0; i < 6; i++) inputs[i] = default;
                    if (steps == 0) for (int i = 0; i < 4; i++) inputs[i] = Jump;   // 4 of 6: the threshold
                    if (steps == late) { inputs[4] = Jump; inputs[5] = Jump; }      // the last two, a bit later
                    SimStepEvents ev = sim.Step(BoardScenario.Dt, inputs);
                    ollie |= ev.Ollie;
                    if (ev.PerfectOllie) { perfect = true; if (!ev.Ollie) upgrades++; }
                    if (steps > 5 && sim.Board.State.Grounded) break;
                }
                return steps * BoardScenario.Dt;
            }

            float normal = Air(0.6f, out bool o1, out bool p1, out _);
            float upgraded = Air(0.1f, out bool o2, out bool p2, out int up2);
            TestContext.WriteLine($"airtime: normal {normal:0.00} s, upgraded to perfect (last riders 0.10 s late) {upgraded:0.00} s");
            Assert.IsTrue(o1 && !p1, "the last riders 0.6 s late: a normal ollie");
            Assert.IsTrue(o2 && p2, "0.1 s late: upgraded to perfect");
            Assert.AreEqual(1, up2, "one upgrade event");
            Assert.That(upgraded, Is.GreaterThan(normal + 0.1f), "the upgrade adds real lift");
        }

        [Test]
        public void CrewOllie_Solo_EveryJumpIsAnOllie()
        {
            var t = BoardScenario.TuningAtSpeed(18f);
            BoardSimulation sim = BoardScenario.Create(1, t);
            var inputs = new PlayerInputState[1];
            for (int i = 0; i < 30; i++) sim.Step(BoardScenario.Dt, inputs);
            inputs[0] = Jump;
            Assert.IsTrue(sim.Step(BoardScenario.Dt, inputs).Ollie);
        }

        [Test]
        public void CarveBoost_HoldAndRelease_GivesSpeed_NotWhenWobbling()
        {
            var t = BoardScenario.TuningAtSpeed(20f);
            BoardSimulation sim = BoardScenario.Create(6, t);
            BoardScenario.PinAll(sim, BoardScenario.Layout("LLLLCC", t)); // firm carve to the left
            int boost = 0; float speedAtRelease = 0f;
            for (int i = 0; i < 150; i++) sim.Step(BoardScenario.Dt, null);
            float charge = sim.Board.State.CarveCharge;
            Assert.That(charge, Is.GreaterThan(t.carveMinTime), "holding a carve charges the boost");
            Assert.That(sim.Board.State.Wobble, Is.EqualTo(0f), "this carve stays out of the wobble zone");
            BoardScenario.PinAll(sim, BoardScenario.Layout("CCCCCC", t)); // straighten out
            speedAtRelease = sim.Board.State.Speed;
            for (int i = 0; i < 120 && boost == 0; i++) boost = sim.Step(BoardScenario.Dt, null).Board.CarveBoost;
            Assert.That(boost, Is.GreaterThan(0), "straightening out fires the boost");
            for (int i = 0; i < 60; i++) sim.Step(BoardScenario.Dt, null);
            TestContext.WriteLine($"carve {charge:0.00} s -> boost level {boost}, speed {speedAtRelease:0.0} -> {sim.Board.State.Speed:0.0} m/s");
            Assert.That(sim.Board.State.Speed, Is.GreaterThan(speedAtRelease + 2f), "the boost is felt");
        }

        [Test]
        public void Slipstream_BehindASameDirectionCar_BuildsSpeed()
        {
            var t = new BoardTuningData { livesPerRun = 0 };
            var run = new RunSimulation(t, 21, 1, TestRoadLayout.Build());
            var none = new PlayerInputState[BoardSimulation.MaxPlayers];
            run.Board.Players[0].Pin(new Vector2(0f, 0f));
            for (int i = 0; i < 60; i++) run.Step(BoardScenario.Dt, none);
            // a slow car right ahead in the board's lane
            var car = new Obstacle
            {
                Kind = ObstacleKind.MovingCar, Motion = ObstacleMotion.SameDirection, Along = run.Distance + t.HalfLength + 2.2f + 6f,
                Lateral = run.Projection.Lateral, HalfExtents = ObstacleCatalog.DefaultHalfExtents(ObstacleKind.MovingCar),
                Height = ObstacleCatalog.Height(ObstacleKind.MovingCar), Speed = run.Board.Board.State.Speed,
                MotionMin = run.Distance - 50f, MotionMax = run.Distance + 2000f,
            };
            int slot = run.Obstacles.Spawn(car);
            Assert.That(slot, Is.GreaterThanOrEqualTo(0));
            bool started = false;
            for (int i = 0; i < 60; i++)
            {
                ref Obstacle o = ref run.Obstacles.Items[slot];
                o.Along = run.Distance + t.HalfLength + 2.2f + 6f; // keep it right ahead for the test
                o.Lateral = run.Projection.Lateral;
                started |= run.Step(BoardScenario.Dt, none).SlipstreamStarted;
            }
            TestContext.WriteLine($"slipstream {run.Slipstream:0.00}, bonus {run.Board.Board.State.ExternalSpeedBonus:0.0} m/s");
            Assert.IsTrue(started, "tucked in behind the car");
            Assert.That(run.Board.Board.State.ExternalSpeedBonus, Is.GreaterThan(t.slipstreamBonus * 0.9f));
            Assert.AreEqual(1, run.Score.Slipstreams);
        }

        [Test]
        public void IdealDriver_WithMovesEnabled_StillNeverCrashes()
        {
            int boosts = 0, crashes = 0;
            var t = new BoardTuningData { livesPerRun = 0 };
            var run = new RunSimulation(t, 42, 6);
            var driver = new IdealDriver(run);
            var none = new PlayerInputState[BoardSimulation.MaxPlayers];
            float time = 0f;
            while (run.Distance < 6000f && time < 600f)
            {
                driver.Drive(BoardScenario.Dt);
                RunStepEvents ev = run.Step(BoardScenario.Dt, none);
                if (ev.CarveBoost > 0) boosts++;
                if (ev.Crashed) crashes++;
                time += BoardScenario.Dt;
            }
            TestContext.WriteLine($"6 km: {boosts} carve boosts, {run.Score.Slipstreams} slipstreams, {crashes} crashes, avg {run.Distance / time * 3.6f:0} km/h");
            Assert.AreEqual(0, crashes);
        }
    }
}
