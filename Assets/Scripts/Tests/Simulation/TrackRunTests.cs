using System;
using System.Numerics;
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
            $"offroad {OffroadTime:0.0} s, max |lateral| {MaxAbsLateral:0.0} m, max speed {MaxSpeed:0.0} m/s, " +
            $"max danger {MaxDanger:0.00}, wobble {WobbleTime:0.0} s, disagreement {DisagreementTime:0.0} s, ramp air {Airborne}";
    }

    /// <summary>Drives the full M1 test road headless (same layout, ramp and respawn rules as the Unity scene).</summary>
    public static class TrackRunner
    {
        const float RespawnBackOff = 6f;
        const float EndMargin = 40f;

        public static TrackRunResult Run(BotBehavior[] slots, float maxSeconds = 400f, BoardTuningData tuning = null)
        {
            RoadPath path = TestRoadLayout.BuildPath();
            BoardTuningData t = tuning ?? new BoardTuningData();
            var sim = new BoardSimulation(t, new RoadGround(path), slots.Length);
            path.Sample(0f, out Vector3 start, out float startYaw);
            sim.Restart(start, startYaw);

            var bots = new BotBrain[slots.Length];
            for (int i = 0; i < slots.Length; i++) bots[i] = BotBrain.Create(slots[i], 1000 + i * 17);
            var inputs = new PlayerInputState[BoardSimulation.MaxPlayers];
            var result = new TrackRunResult();
            int hintHint = 0, projHint = 0;
            float dt = BoardScenario.Dt;

            while (result.Time < maxSeconds)
            {
                BoardState b = sim.Board.State;
                float maxYawRate = (t.yawRateBaseDeg + t.yawRatePerSpeedDeg * b.Speed) * SimMath.Deg2Rad;
                float hint = path.SteerHint(b.Position, b.Yaw, b.Speed, maxYawRate, ref hintHint);
                var ctx = new BotContext
                {
                    Players = sim.Players, ActivePlayerCount = sim.ActivePlayerCount, Board = b, Tuning = t,
                    SteerHint = hint, Time = sim.Time, Dt = dt,
                };
                for (int i = 0; i < slots.Length; i++) { ctx.Self = i; inputs[i] = bots[i].Decide(ctx); }

                SimStepEvents ev = sim.Step(dt, inputs);
                result.Time += dt;
                b = sim.Board.State;
                if (!SimMath.IsFinite(b.Position) || !SimMath.IsFinite(b.Yaw)) { result.AllFinite = false; break; }
                if (ev.Board.Crashed) result.Crashes++;
                if (!b.Grounded && !b.Crashed) result.Airborne = true;
                result.Falls += ev.PlayersFell;
                result.MaxSpeed = Math.Max(result.MaxSpeed, b.Speed);
                result.MaxDanger = Math.Max(result.MaxDanger, b.Danger);
                if (b.Wobble > 0f) result.WobbleTime += dt;
                if (!b.Crashed && Math.Abs(b.RawSteering - SimMath.Clamp(hint, -0.6f, 0.6f)) > 0.15f) result.DisagreementTime += dt;

                float along = path.Project(b.Position, ref projHint, out float lateral);
                result.Distance = Math.Max(result.Distance, along);
                if (!b.Crashed)
                {
                    result.MaxAbsLateral = Math.Max(result.MaxAbsLateral, Math.Abs(lateral));
                    if (Math.Abs(lateral) > path.HalfWidth) result.OffroadTime += dt;
                }

                if (sim.RestartDue)
                {
                    path.Sample(Math.Max(0f, along - RespawnBackOff), out Vector3 p, out float yaw);
                    sim.Restart(p, yaw);
                }
                if (along >= path.Length - EndMargin) { result.Completed = true; break; }
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

    /// <summary>The M1 test road must be drivable with nothing but player weight (no auto-steer anywhere).</summary>
    public class TrackRunTests
    {
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(6)]
        public void CooperativeCrew_CompletesTrack_OnTheRoad_WithoutCrash(int players)
        {
            TrackRunResult r = TrackRunner.Run(TrackRunner.All(BotBehavior.Cooperative, players));
            TestContext.WriteLine($"{players} cooperative: {r}");
            Assert.IsTrue(r.AllFinite);
            Assert.IsTrue(r.Completed, "must reach the end of the road");
            Assert.AreEqual(0, r.Crashes);
            Assert.That(r.MaxAbsLateral, Is.LessThan(TestRoadLayout.RoadWidth * 0.5f), "must stay on the asphalt");
            Assert.IsTrue(r.Airborne, "centre line runs over the ramp");
        }

        [Test]
        public void MixedBots_DisagreeVisibly_ButMostlyStayOnTheRoad()
        {
            // slot 0 is cooperative here as a stand-in for the human player
            TrackRunResult mixed = TrackRunner.Run(TrackRunner.Mixed(6));
            TrackRunResult coop = TrackRunner.Run(TrackRunner.All(BotBehavior.Cooperative, 6));
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
