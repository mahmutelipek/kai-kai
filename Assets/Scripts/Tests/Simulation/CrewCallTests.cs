using System;
using System.Numerics;
using Game.Simulation;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>
    /// The crew's "JUMP!" call: bots answer a human's jump (so one human + five bots can crew-ollie at all) and the
    /// crew calls a jump itself when a low hazard lies in the board's path.
    /// </summary>
    public class CrewCallTests
    {
        sealed class Rig
        {
            public readonly RunSimulation Run;
            public readonly BotBrain[] Bots = new BotBrain[BoardSimulation.MaxPlayers];
            public readonly bool[] Human = new bool[BoardSimulation.MaxPlayers];
            public readonly PlayerInputState[] Inputs = new PlayerInputState[BoardSimulation.MaxPlayers];
            public bool CrewCallsEnabled = true;
            int _hint;

            public Rig(int players, bool mixed, int humans, int seed = 7)
            {
                Run = new RunSimulation(new BoardTuningData { livesPerRun = 0 }, seed, players, TestRoadLayout.Build());
                for (int i = 0; i < players; i++)
                {
                    Human[i] = i < humans;
                    Bots[i] = BotBrain.Create(mixed ? BotBrain.DefaultBehaviorForSlot(i) : BotBehavior.Cooperative, 1000 + i * 17);
                }
            }

            /// <summary>One step like PlayerInputRouter: humans first, then the crew call, then the bots.</summary>
            public RunStepEvents Step(bool humanJump)
            {
                BoardTuningData t = Run.Tuning;
                BoardState b = Run.Board.Board.State;
                float dt = BoardScenario.Dt;
                for (int i = 0; i < Run.Board.ActivePlayerCount; i++)
                    Inputs[i] = Human[i] ? new PlayerInputState(Vector2.Zero, humanJump, false) : default;
                if (CrewCallsEnabled) Run.Crew.Update(Run, Inputs, Human, dt);
                float maxYawRate = (t.yawRateBaseDeg + t.yawRatePerSpeedDeg * b.Speed) * SimMath.Deg2Rad;
                var ctx = new BotContext
                {
                    Players = Run.Board.Players, ActivePlayerCount = Run.Board.ActivePlayerCount, Board = b, Tuning = t,
                    SteerHint = Run.Road.PredictiveSteerHint(b, maxYawRate, t.CrewResponseDelay + 0.05f, ref _hint),
                    BrakeHint = Run.Road.BrakeHint(Run.Distance, b.Speed, t), Time = Run.Board.Time, Dt = dt,
                    CrewCallId = Run.Crew.Id, CrewCallAge = Run.Crew.Age,
                };
                for (int i = 0; i < Run.Board.ActivePlayerCount; i++)
                    if (!Human[i]) { ctx.Self = i; Inputs[i] = Bots[i].Decide(ctx); }
                return Run.Step(dt, Inputs);
            }
        }

        [Test]
        public void OneHumanFiveMixedBots_HumanJump_TheCrewJoinsAndOllies()
        {
            var rig = new Rig(6, mixed: true, humans: 1);
            for (int i = 0; i < 120; i++) rig.Step(false);
            int trials = 0, ollies = 0, perfects = 0;
            for (int trial = 0; trial < 40; trial++)
            {
                // wait for a calm, grounded moment, then press jump for a few frames (a human tap)
                for (int i = 0; i < 150; i++) rig.Step(false);
                BoardState b = rig.Run.Board.Board.State;
                if (!b.Grounded || b.Crashed) continue;
                trials++;
                bool ollie = false, perfect = false;
                for (int i = 0; i < 45; i++)
                {
                    RunStepEvents ev = rig.Step(i < 5);
                    ollie |= ev.Ollie;
                    perfect |= ev.PerfectOllie;
                }
                if (ollie) ollies++;
                if (perfect) perfects++;
            }
            TestContext.WriteLine($"1 human + 5 mixed bots: {ollies}/{trials} jumps became crew ollies, {perfects} perfect");
            Assert.That(trials, Is.GreaterThan(25));
            Assert.That(ollies, Is.GreaterThanOrEqualTo((int)(trials * 0.75f)), "the crew backs up the human's jump");
            Assert.That(perfects, Is.GreaterThan(0), "sometimes everyone makes it");
            Assert.That(perfects, Is.LessThan(ollies), "but perfect stays special");
        }

        [Test]
        public void WithoutAnsweringBots_OneHumanCannotOllieASixCrewBoard()
        {
            var rig = new Rig(6, mixed: true, humans: 1) { CrewCallsEnabled = false };
            for (int i = 0; i < 120; i++) rig.Step(false);
            bool ollie = false;
            for (int i = 0; i < 60; i++) ollie |= rig.Step(i < 5).Ollie;
            Assert.IsFalse(ollie, "the bug this fixes: 1 of 6 is below the 60 % crew threshold");
        }

        static (bool hit, bool ollie, bool called) ConeAhead(bool crewCalls)
        {
            var rig = new Rig(6, mixed: false, humans: 0) { CrewCallsEnabled = crewCalls };
            for (int i = 0; i < 120; i++) rig.Step(false);
            RunSimulation run = rig.Run;
            var cone = new Obstacle
            {
                Kind = ObstacleKind.Cone, Motion = ObstacleMotion.Static, Along = run.Distance + 30f, Lateral = run.Projection.Lateral,
                HalfExtents = ObstacleCatalog.DefaultHalfExtents(ObstacleKind.Cone), Height = ObstacleCatalog.Height(ObstacleKind.Cone),
            };
            int slot = run.Obstacles.Spawn(cone);
            Assert.That(slot, Is.GreaterThanOrEqualTo(0));
            run.Obstacles.UpdateWorldPose(run.Road, slot);
            bool hit = false, ollie = false;
            int calls = run.Crew.Id;
            for (int i = 0; i < 150; i++)
            {
                ref Obstacle o = ref run.Obstacles.Items[slot];
                if (run.Distance < o.Along - 12f)
                {
                    o.Lateral = run.Projection.Lateral; // stays in the path until the crew commits
                    run.Obstacles.UpdateWorldPose(run.Road, slot);
                }
                RunStepEvents ev = rig.Step(false);
                hit |= run.Obstacles.Items[slot].Knocked || ev.LightHits > 0 || ev.Bumps > 0;
                ollie |= ev.Ollie;
            }
            return (hit, ollie, run.Crew.Id > calls && run.Crew.FromHazard);
        }

        [Test]
        public void Bots_SpotAConeInThePath_CallJump_AndClearIt()
        {
            var with = ConeAhead(true);
            var without = ConeAhead(false);
            TestContext.WriteLine($"cone in the path: crew calls -> called {with.called}, ollie {with.ollie}, hit {with.hit}; without -> hit {without.hit}");
            Assert.IsTrue(without.hit, "control: the board runs into the cone");
            Assert.IsTrue(with.called && with.ollie, "the crew calls JUMP and ollies");
            Assert.IsFalse(with.hit, "and clears the cone");
        }

        [Test]
        public void CrewCall_OneCallPerJumpMoment_HazardCooldown()
        {
            var call = new CrewCall();
            var run = new RunSimulation(new BoardTuningData(), 3, 2, TestRoadLayout.Build());
            var inputs = new PlayerInputState[BoardSimulation.MaxPlayers];
            var human = new bool[BoardSimulation.MaxPlayers];
            human[0] = human[1] = true;
            inputs[0] = inputs[1] = new PlayerInputState(Vector2.Zero, true, false);
            call.Update(run, inputs, human, 1f / 60f);
            Assert.AreEqual(1, call.Id, "two humans pressing together = one call");
            call.Update(run, inputs, human, 1f / 60f);
            Assert.AreEqual(1, call.Id, "holding jump does not repeat the call");
            inputs[0] = inputs[1] = default;
            call.Update(run, inputs, human, 0.5f);
            inputs[0] = new PlayerInputState(Vector2.Zero, true, false);
            call.Update(run, inputs, human, 1f / 60f);
            Assert.AreEqual(2, call.Id, "a new press later is a new call");
            Assert.IsFalse(call.FromHazard);
        }
    }
}
