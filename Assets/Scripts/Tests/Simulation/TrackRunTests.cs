using System;
using System.Collections.Generic;
using Game.Simulation;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class TrackRunResult
    {
        public float Time;
        public float Distance;
        public bool Completed;
        public int Crashes;
        public int Falls;
        public int LightHits, Bumps, HeavyHits, WallScrapes;
        public float OffroadTime;
        public float MaxAbsLateral;
        public float MaxSpeed;
        public float MaxDanger;
        public float WobbleTime;
        public bool Airborne;
        /// <summary>Seconds in which the crew's steering is clearly not what the road asks for.</summary>
        public float DisagreementTime;
        public bool AllFinite = true;

        public override string ToString() =>
            $"{(Completed ? "completed" : "stopped")} {Distance:0} m in {Time:0.0} s, crashes {Crashes}, falls {Falls}, " +
            $"hits light/bump/heavy/wall {LightHits}/{Bumps}/{HeavyHits}/{WallScrapes}, offroad {OffroadTime:0.0} s, " +
            $"max |lateral| {MaxAbsLateral:0.0} m, max speed {MaxSpeed:0.0} m/s, max danger {MaxDanger:0.00}, " +
            $"wobble {WobbleTime:0.0} s, disagreement {DisagreementTime:0.0} s, ramp air {Airborne}";
    }

    /// <summary>Drives a RunSimulation with bots (same rules as the Unity scene) and collects metrics.</summary>
    public static class TrackRunner
    {
        public static TrackRunResult Run(BotBehavior[] slots, IList<FixedChunkSpec> fixedTrack, float targetDistance,
                                         float maxSeconds = 600f, int seed = 1, BoardTuningData tuning = null)
        {
            BoardTuningData t = tuning ?? new BoardTuningData { livesPerRun = 0 }; // practice: unlimited respawns
            var run = new RunSimulation(t, seed, slots.Length, fixedTrack);
            var bots = new BotBrain[slots.Length];
            for (int i = 0; i < slots.Length; i++) bots[i] = BotBrain.Create(slots[i], 1000 + i * 17);
            var inputs = new PlayerInputState[BoardSimulation.MaxPlayers];
            var result = new TrackRunResult();
            int hintCache = 0;
            float dt = BoardScenario.Dt;

            while (result.Time < maxSeconds)
            {
                BoardState b = run.Board.Board.State;
                float maxYawRate = (t.yawRateBaseDeg + t.yawRatePerSpeedDeg * b.Speed) * SimMath.Deg2Rad;
                float hint = run.Road.PredictiveSteerHint(b, maxYawRate, 1.0f, ref hintCache);
                var ctx = new BotContext
                {
                    Players = run.Board.Players, ActivePlayerCount = run.Board.ActivePlayerCount, Board = b, Tuning = t,
                    SteerHint = hint, BrakeHint = run.Road.BrakeHint(run.Distance, b.Speed, t), Time = run.Board.Time, Dt = dt,
                };
                for (int i = 0; i < slots.Length; i++) { ctx.Self = i; inputs[i] = bots[i].Decide(ctx); }

                RunStepEvents ev = run.Step(dt, inputs);
                result.Time += dt;
                b = run.Board.Board.State;
                if (!SimMath.IsFinite(b.Position) || !SimMath.IsFinite(b.Yaw)) { result.AllFinite = false; break; }
                if (ev.Crashed) result.Crashes++;
                if (!b.Grounded && !b.Crashed) result.Airborne = true;
                result.Falls += ev.Sim.PlayersFell;
                result.LightHits += ev.LightHits; result.Bumps += ev.Bumps; result.HeavyHits += ev.HeavyHits; result.WallScrapes += ev.WallScrapes;
                result.MaxSpeed = Math.Max(result.MaxSpeed, b.Speed);
                result.MaxDanger = Math.Max(result.MaxDanger, b.Danger);
                if (b.Wobble > 0f) result.WobbleTime += dt;
                if (!b.Crashed && Math.Abs(b.RawSteering - SimMath.Clamp(hint, -0.6f, 0.6f)) > 0.15f) result.DisagreementTime += dt;

                RoadProjection p = run.Projection;
                result.Distance = Math.Max(result.Distance, run.Distance);
                if (!b.Crashed && p.Valid)
                {
                    result.MaxAbsLateral = Math.Max(result.MaxAbsLateral, Math.Abs(p.Lateral));
                    if (Math.Abs(p.Lateral) > p.HalfWidth) result.OffroadTime += dt;
                }
                if (run.Distance >= targetDistance) { result.Completed = true; break; }
            }
            return result;
        }

        public static BotBehavior[] All(BotBehavior behavior, int count)
        {
            var slots = new BotBehavior[count];
            for (int i = 0; i < count; i++) slots[i] = behavior;
            return slots;
        }

        public static BotBehavior[] Mixed(int count)
        {
            var slots = new BotBehavior[count];
            for (int i = 0; i < count; i++) slots[i] = BotBrain.DefaultBehaviorForSlot(i);
            return slots;
        }
    }

    /// <summary>The M1 test track must be drivable with nothing but player weight (no auto-steer anywhere).</summary>
    public class TrackRunTests
    {
        static readonly float TrackEnd = TestRoadLayout.Length - 40f;

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(6)]
        public void CooperativeCrew_CompletesTestTrack_OnTheRoad_WithoutCrash(int players)
        {
            TrackRunResult r = TrackRunner.Run(TrackRunner.All(BotBehavior.Cooperative, players), TestRoadLayout.Build(), TrackEnd);
            TestContext.WriteLine($"{players} cooperative: {r}");
            Assert.IsTrue(r.AllFinite);
            Assert.IsTrue(r.Completed, "must reach the end of the road");
            Assert.AreEqual(0, r.Crashes);
            Assert.That(r.MaxAbsLateral, Is.LessThan(TestRoadLayout.RoadWidth * 0.5f), "must stay on the asphalt");
            Assert.IsTrue(r.Airborne, "the planned line runs over the ramp");
        }

        [TestCase(1)]
        [TestCase(42)]
        [TestCase(1234)]
        public void EndlessRoad_CooperativeCrewsNeverCrash_MixedCrewFinishes(int seed)
        {
            TrackRunResult solo = TrackRunner.Run(TrackRunner.All(BotBehavior.Cooperative, 1), null, 5000f, 900f, seed);
            TrackRunResult coop = TrackRunner.Run(TrackRunner.All(BotBehavior.Cooperative, 6), null, 5000f, 900f, seed);
            TrackRunResult mixed = TrackRunner.Run(TrackRunner.Mixed(6), null, 5000f, 900f, seed);
            TestContext.WriteLine($"seed {seed} solo:  {solo}");
            TestContext.WriteLine($"seed {seed} coop:  {coop}");
            TestContext.WriteLine($"seed {seed} mixed: {mixed}");
            Assert.IsTrue(solo.Completed && coop.Completed && mixed.Completed);
            Assert.AreEqual(0, solo.Crashes, "solo cooperative");
            Assert.AreEqual(0, coop.Crashes, "6 cooperative");
            Assert.That(mixed.Crashes, Is.LessThanOrEqualTo(2), "mixed crews may crash, but rarely");
        }

        [Test]
        public void MixedBots_DisagreeVisibly_ButMostlyStayOnTheRoad()
        {
            // slot 0 is cooperative here as a stand-in for the human player
            TrackRunResult mixed = TrackRunner.Run(TrackRunner.Mixed(6), TestRoadLayout.Build(), TrackEnd);
            TrackRunResult coop = TrackRunner.Run(TrackRunner.All(BotBehavior.Cooperative, 6), TestRoadLayout.Build(), TrackEnd);
            TestContext.WriteLine($"mixed 6: {mixed}");
            TestContext.WriteLine($"coop 6:  {coop}");
            Assert.IsTrue(mixed.AllFinite);
            Assert.IsTrue(mixed.Completed, "respawns must always let a run continue to the end");
            Assert.That(mixed.DisagreementTime, Is.GreaterThan(coop.DisagreementTime * 3f + 5f), "mixed bots must fight over the steering");
            Assert.That(mixed.OffroadTime / mixed.Time, Is.LessThan(0.4f), "but the road must stay followable (grass shoulder is drivable, just slow)");
            Assert.AreEqual(0, coop.Crashes);
        }
    }
}
