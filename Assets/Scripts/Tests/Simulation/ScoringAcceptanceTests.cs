using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Game.Simulation;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>Milestone 3 acceptance: score / combo rules, near miss and airtime detection, nitro, high scores.</summary>
    public class ScoringAcceptanceTests
    {
        // points, combo, coins and diamonds are off in the shipped game; these tests cover the rule set itself
        [SetUp] public void ScoringOn() { GameRules.Scoring = true; GameRules.Nitro = true; }
        [TearDown] public void ScoringOff() { GameRules.Scoring = false; GameRules.Nitro = false; }

        static BoardTuningData T() => new BoardTuningData();

        // ------------------------------------------------------------------ 1. score math and combo rules

        [Test]
        public void M3_1_BasePointsPerEvent_AtMultiplierOne()
        {
            var t = T();
            var s = new ScoreManager(t);
            s.OnCoin();
            Assert.AreEqual(t.coinPoints, s.Score, 1e-4);
            s.Reset(); s.OnDiamond();
            Assert.AreEqual(t.diamondPoints, s.Score, 1e-4);
            s.Reset(); s.OnNearMiss();
            Assert.AreEqual(t.nearMissPoints, s.Score, 1e-4);
            s.Reset(); s.OnLanding(1.5f, clean: false);
            Assert.AreEqual(1.5f * t.airtimePointsPerSecond, s.Score, 1e-3);
            s.Reset(); s.Tick(0.1f, 12.7f);
            Assert.AreEqual(12f * t.pointsPerMeter, s.Score, 1e-4, "whole metres are awarded, the rest carries over");
            s.Tick(0.1f, 0.4f);
            Assert.AreEqual(13f * t.pointsPerMeter, s.Score, 1e-4);
        }

        [Test]
        public void M3_1_ComboGrowth_PerEventType()
        {
            var s = new ScoreManager(T());
            s.OnCoin(); Assert.AreEqual(ScoreManager.ComboPerCoin, s.Combo);
            s.OnDiamond(); Assert.AreEqual(ScoreManager.ComboPerCoin + ScoreManager.ComboPerDiamond, s.Combo);
            int c = s.Combo;
            s.OnNearMiss(); Assert.AreEqual(c + ScoreManager.ComboPerNearMiss, s.Combo);
            c = s.Combo;
            s.OnLanding(1f, clean: true); Assert.AreEqual(c + ScoreManager.ComboPerCleanLanding, s.Combo, "clean landing grows combo");
            c = s.Combo;
            s.OnLanding(1f, clean: false); Assert.AreEqual(c, s.Combo, "dirty landing scores airtime but no combo");
            s.OnSectionCleared(); Assert.AreEqual(c + ScoreManager.ComboPerSection, s.Combo);
            c = s.Combo;
            s.OnNitroPickup(); Assert.AreEqual(c + ScoreManager.ComboPerNitroPickup, s.Combo);
            s.OnLanding(0.1f, clean: true); Assert.AreEqual(c + ScoreManager.ComboPerNitroPickup, s.Combo, "tiny hops do not count");
            Assert.AreEqual(s.Combo, s.BestCombo);
        }

        [Test]
        public void M3_1_Multiplier_StepsAndCap()
        {
            var t = T();
            t.comboStep = 5f;           // test the formula with explicit values
            t.comboMaxMultiplier = 8f;
            Assert.AreEqual(1, ScoreManager.MultiplierFor(0, t));
            Assert.AreEqual(1, ScoreManager.MultiplierFor(4, t));
            Assert.AreEqual(2, ScoreManager.MultiplierFor(5, t));
            Assert.AreEqual(3, ScoreManager.MultiplierFor(10, t));
            Assert.AreEqual(8, ScoreManager.MultiplierFor(35, t));
            Assert.AreEqual(8, ScoreManager.MultiplierFor(500, t), "capped");

            var s = new ScoreManager(t);
            for (int i = 0; i < 5; i++) s.OnCoin(); // coins 1..5 score at x1 (combo 0..4 when awarded)
            Assert.AreEqual(5 * t.coinPoints, s.Score, 1e-3);
            s.OnCoin(); // combo 5 -> x2
            Assert.AreEqual(5 * t.coinPoints + 2 * t.coinPoints, s.Score, 1e-3);
            float before = s.Score;
            s.Tick(0.1f, 10f); // distance also multiplied (combo now 6 -> x2)
            Assert.AreEqual(before + 10f * t.pointsPerMeter * 2f, s.Score, 1e-3);
        }

        [Test]
        public void M3_1_ComboDecay_IdleDrain()
        {
            var t = T(); // idle comboIdleTime (5 s), drain 2 / s
            var s = new ScoreManager(t);
            for (int i = 0; i < 10; i++) s.OnCoin();
            int idleTicks = (int)Math.Round(t.comboIdleTime / 0.1f);
            for (int i = 0; i < idleTicks - 2; i++) s.Tick(0.1f, 0f); // just before the idle time: nothing lost yet
            Assert.AreEqual(10, s.Combo);
            for (int i = 0; i < 12; i++) s.Tick(0.1f, 0f); // ~1 s of draining at 2/s
            Assert.That(s.Combo, Is.InRange(7, 9));
            for (int i = 0; i < 100; i++) s.Tick(0.1f, 0f);
            Assert.AreEqual(0, s.Combo, "drains to zero");
            Assert.AreEqual(10, s.BestCombo, "best combo is kept");
            s.OnCoin();
            for (int i = 0; i < idleTicks - 10; i++) s.Tick(0.1f, 0f);
            Assert.AreEqual(1, s.Combo, "a new event restarts the idle timer");
        }

        [Test]
        public void M3_1_ComboResetsAndHits()
        {
            var t = T();
            var s = new ScoreManager(t);
            for (int i = 0; i < 9; i++) s.OnCoin();
            s.OnLightHit(); Assert.AreEqual(4, s.Combo, "light hit halves (9 -> 4)");
            s.OnHeavyHit(); Assert.AreEqual(0, s.Combo, "heavy hit resets");
            for (int i = 0; i < 6; i++) s.OnCoin();
            s.OnPlayerFell(); Assert.AreEqual(0, s.Combo, "a fall resets");
            for (int i = 0; i < 6; i++) s.OnCoin();
            s.OnCrash(); Assert.AreEqual(0, s.Combo, "crash resets");
            float scoreBefore = s.Score;
            s.OnCrash();
            Assert.AreEqual(scoreBefore, s.Score, "resets never take points away");
        }

        [Test]
        public void M3_1_BreakdownAddsUpToScore()
        {
            var s = new ScoreManager(T());
            var rng = new Random(3);
            for (int i = 0; i < 400; i++)
            {
                switch (rng.Next(7))
                {
                    case 0: s.OnCoin(); break;
                    case 1: s.OnDiamond(); break;
                    case 2: s.OnNearMiss(); break;
                    case 3: s.OnLanding((float)rng.NextDouble() * 2f, rng.Next(2) == 0); break;
                    case 4: s.OnLightHit(); break;
                    case 5: if (rng.Next(10) == 0) s.OnHeavyHit(); break;
                    default: s.Tick(0.1f, (float)rng.NextDouble() * 5f); break;
                }
            }
            float sum = s.DistancePoints + s.CoinPoints + s.DiamondPoints + s.NearMissPoints + s.AirtimePoints;
            TestContext.WriteLine($"score {s.Score:0}, breakdown sum {sum:0}, best combo {s.BestCombo}");
            Assert.AreEqual(s.Score, sum, 1e-2);
        }

        // ------------------------------------------------------------------ 2. near miss and airtime detection

        /// <summary>Drives a board state in a straight line past one obstacle at a given lateral gap.</summary>
        static (int nearMisses, bool hit) PassObstacle(float gap, float speed, ObstacleKind kind = ObstacleKind.ConcreteBarrier)
        {
            var t = T();
            var field = new ObstacleField();
            Vector2 half = ObstacleCatalog.DefaultHalfExtents(kind);
            int slot = field.Spawn(new Obstacle { Kind = kind, HalfExtents = half, Height = ObstacleCatalog.Height(kind), Along = 50f });
            field.Items[slot].Position = new Vector3(0f, 0f, 50f);
            field.Items[slot].Yaw = 0f;
            float boardX = half.X + t.HalfWidth + gap;
            var hits = new List<ImpactEvent>();
            var misses = new List<int>();
            int nearMisses = 0; bool hit = false;
            for (float z = 0f; z <= 100f; z += speed / 60f)
            {
                var b = new BoardState { Position = new Vector3(boardX, 0f, z), Yaw = 0f, TravelYaw = 0f, Speed = speed, Grounded = true };
                if (field.Collide(b, t, z, hits) > 0) hit = true;
                nearMisses += field.TrackNearMisses(b, t, z, misses);
            }
            return (nearMisses, hit);
        }

        [Test]
        public void M3_2_NearMiss_DetectedOnceWhenCloseAndFast()
        {
            var t = T();
            (int close, bool hitClose) = PassObstacle(0.4f, 25f);
            (int far, _) = PassObstacle(t.nearMissDistance + 0.5f, 25f);
            (int slow, _) = PassObstacle(0.4f, t.nearMissMinSpeed - 3f);
            (int touching, bool hitTouching) = PassObstacle(-0.2f, 25f);
            (int car, _) = PassObstacle(0.6f, 30f, ObstacleKind.ParkedCar);
            (int cone, _) = PassObstacle(0.2f, 25f, ObstacleKind.Cone);
            TestContext.WriteLine($"gap 0.4 m @25: {close}; gap {t.nearMissDistance + 0.5f} m: {far}; slow: {slow}; overlapping: {touching} (hit {hitTouching}); car 0.6 m: {car}; cone: {cone}");
            Assert.AreEqual(1, close, "exactly one near miss");
            Assert.IsFalse(hitClose);
            Assert.AreEqual(0, far, "too far is not a near miss");
            Assert.AreEqual(0, slow, "too slow is not a near miss");
            Assert.IsTrue(hitTouching);
            Assert.AreEqual(0, touching, "a hit is not a near miss");
            Assert.AreEqual(1, car);
            Assert.AreEqual(0, cone, "knockable clutter does not count");
        }

        [Test]
        public void M3_2_Airtime_RampJumpScoresAndCleanLandingGrowsCombo()
        {
            var t = new BoardTuningData { livesPerRun = 0 };
            var run = new RunSimulation(t, 1, 6, TestRoadLayout.Build());
            BoardScenario.PinAll(run.Board, BoardScenario.Layout("CCCCCC", t));
            run.Respawn(TestRoadLayout.RampDistance - 40f);
            run.Board.Board.State.Speed = 22f;
            run.Board.Board.State.RunTime = (22f - t.startSpeed) / t.speedRampPerSecond; // cruise at 22 m/s
            var none = new PlayerInputState[6];
            float measuredAir = 0f, reportedAir = -1f; bool clean = false; int comboBefore = run.Score.Combo;
            for (int i = 0; i < 60 * 6 && reportedAir < 0f; i++)
            {
                RunStepEvents ev = run.Step(BoardScenario.Dt, none);
                if (!run.Board.Board.State.Grounded) measuredAir += BoardScenario.Dt;
                if (ev.Landed && ev.Airtime > 0.1f) { reportedAir = ev.Airtime; clean = ev.CleanLanding; }
            }
            TestContext.WriteLine($"ramp at 22 m/s: measured airborne {measuredAir:F3} s, reported {reportedAir:F3} s, clean {clean}, airtime points {run.Score.AirtimePoints:0}, combo {comboBefore} -> {run.Score.Combo}");
            Assert.That(reportedAir, Is.EqualTo(measuredAir).Within(BoardScenario.Dt * 1.5f));
            Assert.That(run.Score.AirtimePoints, Is.GreaterThan(0f));
            Assert.IsTrue(clean, "balanced crew lands cleanly");
            Assert.That(run.Score.Combo, Is.GreaterThanOrEqualTo(comboBefore + ScoreManager.ComboPerCleanLanding));
        }

        [Test]
        public void M3_2_CleanLandingRule_And_DirtyLandingsScoreWithoutCombo()
        {
            var t = T();
            Assert.IsTrue(RunSimulation.IsCleanLanding(0.2f, 0, false, t), "balanced landing");
            Assert.IsFalse(RunSimulation.IsCleanLanding(t.wobbleStartFraction + 0.01f, 0, false, t), "landing while wobbling");
            Assert.IsFalse(RunSimulation.IsCleanLanding(0.2f, 1, false, t), "someone thrown off");
            Assert.IsFalse(RunSimulation.IsCleanLanding(0.2f, 0, true, t), "crash");

            // a big drop (3 m ramp): lands hard, airtime still scores
            BoardTuningData fast = BoardScenario.TuningAtSpeed(22f);
            fast.hardLandingSpeed = 6f; // a 3 m drop is a hard landing at the old threshold (the shipped one is higher so ramp flights land clean)
            var sim = BoardScenario.Create(6, fast, new RampGround { RampHeight = 3f, RampLength = 8f });
            bool hard = false; float air = 0f, landedAir = 0f;
            BoardScenario.Run(sim, 6f, null, onStep: (s, ev) =>
            {
                if (!s.Board.State.Grounded) air += BoardScenario.Dt;
                if (ev.Board.Landed && air > 0.2f) { hard = ev.Board.HardLanding; landedAir = air; }
            });
            var score = new ScoreManager(fast);
            score.OnLanding(landedAir, clean: false);
            TestContext.WriteLine($"3 m ramp: airtime {landedAir:F2} s, hard landing {hard}; dirty landing -> combo {score.Combo}, airtime points {score.AirtimePoints:0}");
            Assert.IsTrue(hard);
            Assert.AreEqual(0, score.Combo);
            Assert.That(score.AirtimePoints, Is.GreaterThan(0f));
        }

        // ------------------------------------------------------------------ 3. nitro

        [Test]
        public void M3_3_Nitro_RaisesSpeed_AndLowersStability_PerTuning()
        {
            BoardTuningData t = BoardScenario.TuningAtSpeed(20f);
            // speed-independent stability so the ratio is exactly the tuning value
            t.stabilityFactorLowSpeed = t.stabilityFactorHighSpeed = 1.5f;

            BoardSimulation baseSim = BoardScenario.Create(6, t);
            BoardSimulation nitroSim = BoardScenario.Create(6, t);
            BoardScenario.PinAll(baseSim, BoardScenario.Layout("LLLLRR", t));
            BoardScenario.PinAll(nitroSim, BoardScenario.Layout("LLLLRR", t));
            nitroSim.Board.StartNitro(t.nitroDuration);
            ScenarioTrace a = BoardScenario.Run(baseSim, t.nitroDuration);
            ScenarioTrace b = BoardScenario.Run(nitroSim, t.nitroDuration);
            float speedGain = b.Speed[b.Speed.Count - 1] - a.Speed[a.Speed.Count - 1];
            float dangerRatio = b.Danger[30] / a.Danger[30];
            TestContext.WriteLine($"nitro {t.nitroDuration}s from 20 m/s: speed {a.Speed[a.Speed.Count - 1]:F1} -> {b.Speed[b.Speed.Count - 1]:F1} m/s (+{speedGain:F1}); danger ratio {dangerRatio:F3} (tuning {t.nitroInstability})");
            Assert.That(speedGain, Is.GreaterThan(t.nitroSpeedBonus * 0.6f));
            Assert.That(dangerRatio, Is.EqualTo(t.nitroInstability).Within(1e-3f));
            // and it ends
            BoardScenario.Run(nitroSim, 1f);
            Assert.AreEqual(0f, nitroSim.Board.State.NitroTimer);
        }

        [Test]
        public void M3_3_Nitro_PickupThenActionButtonFiresIt()
        {
            var t = new BoardTuningData { livesPerRun = 0 };
            var run = new RunSimulation(t, 3, 2, TestRoadLayout.Build());
            BoardState b = run.Board.Board.State;
            run.Pickups.Spawn(PickupKind.Nitro, -1, run.Distance + 2f, 0f, 1.1f, run.Road);
            var inputs = new PlayerInputState[6];
            for (int i = 0; i < 30; i++) run.Step(BoardScenario.Dt, inputs);
            Assert.AreEqual(1, run.NitroCharges, "nitro collected");
            inputs[1].Action = true; // any player may fire it
            RunStepEvents ev = run.Step(BoardScenario.Dt, inputs);
            Assert.IsTrue(ev.NitroStarted);
            Assert.AreEqual(0, run.NitroCharges);
            Assert.That(run.Board.Board.State.NitroTimer, Is.GreaterThan(0f));
        }

        // ------------------------------------------------------------------ 4. high score persistence

        [Test]
        public void M3_4_HighScore_SurvivesRestartAndRelaunch()
        {
            string path = Path.Combine(Path.GetTempPath(), "downhill_hs_" + Guid.NewGuid().ToString("N") + ".txt");
            try
            {
                var first = new HighScoreManager(new FileHighScoreStore(path));
                Assert.AreEqual(0f, first.BestScore);
                Assert.IsTrue(first.Submit(1234.5f, 800f, out bool s1, out bool d1));
                Assert.IsTrue(s1 && d1);
                Assert.IsFalse(first.Submit(1000f, 700f, out _, out _), "worse run changes nothing");

                var relaunched = new HighScoreManager(new FileHighScoreStore(path)); // new process = new instance reading the file
                Assert.AreEqual(1234.5f, relaunched.BestScore, 1e-3);
                Assert.AreEqual(800f, relaunched.BestDistance, 1e-3);
                relaunched.Submit(900f, 1500f, out bool s2, out bool d2);
                Assert.IsFalse(s2); Assert.IsTrue(d2);
                var again = new HighScoreManager(new FileHighScoreStore(path));
                Assert.AreEqual(1234.5f, again.BestScore, 1e-3, "score and distance are tracked independently");
                Assert.AreEqual(1500f, again.BestDistance, 1e-3);
                TestContext.WriteLine("store file after 3 launches:\n" + File.ReadAllText(path));
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }

        [Test]
        public void M3_4_RunEnds_AfterLastLife_SubmitsHighScore_RestartKeepsIt()
        {
            string path = Path.Combine(Path.GetTempPath(), "downhill_hs_" + Guid.NewGuid().ToString("N") + ".txt");
            try
            {
                var t = new BoardTuningData { livesPerRun = 1 };
                var run = new RunSimulation(t, 5, 6, TestRoadLayout.Build()) { HighScores = new HighScoreManager(new FileHighScoreStore(path)) };
                BoardScenario.PinAll(run.Board, BoardScenario.Layout("CCCCCC", t));
                var none = new PlayerInputState[6];
                for (int i = 0; i < 60 * 20; i++) run.Step(BoardScenario.Dt, none); // score some distance
                run.Board.CrashNow();
                bool ended = false;
                for (int i = 0; i < 60 * 5 && !ended; i++) ended = run.Step(BoardScenario.Dt, none).RunEnded || run.State == RunState.Ended;
                Assert.IsTrue(ended, "no lives left: the run ends");
                float scored = run.Score.Score;
                Assert.That(scored, Is.GreaterThan(0f));
                Assert.IsTrue(run.NewBestScore);
                run.Reset(6, TestRoadLayout.Build()); // restart
                Assert.AreEqual(RunState.Running, run.State);
                Assert.AreEqual(0f, run.Score.Score);
                Assert.AreEqual(scored, run.HighScores.BestScore, 1e-3, "best survives the restart");
                Assert.AreEqual(scored, new HighScoreManager(new FileHighScoreStore(path)).BestScore, 1e-3, "and a relaunch");
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }

        // ------------------------------------------------------------------ placement

        [Test]
        public void Pickups_CoinsOnSafeLine_DiamondsOffIt_NitroRegularly()
        {
            var t = new BoardTuningData();
            var road = new RoadModel();
            var obstacles = new ObstacleField();
            var pickups = new PickupField();
            var gen = new RoadGenerator(t, road, obstacles, new DifficultyManager(t), pickups);
            gen.Reset(42);
            int coins = 0, diamonds = 0, nitros = 0, coinsOffLine = 0, diamondsNearLine = 0;
            var seen = new HashSet<int>();
            for (float along = 0f; along < 10000f; along += 5f)
            {
                gen.Update(along);
                foreach (RoadChunk c in road.Chunks)
                {
                    if (!seen.Add(c.Serial)) continue;
                    for (int k = 0; k < c.PickupSlots.Count; k++)
                    {
                        Pickup p = pickups.Items[c.PickupSlots[k]];
                        float off = Math.Abs(p.Lateral - road.PlannedLateral(p.Along));
                        if (p.Kind == PickupKind.Coin) { coins++; if (off > 0.05f) coinsOffLine++; }
                        else if (p.Kind == PickupKind.Diamond) { diamonds++; if (off < 0.5f && p.Hover < 2f) diamondsNearLine++; }
                        else nitros++;
                    }
                }
            }
            TestContext.WriteLine($"10 km seed 42: {coins} coins ({coinsOffLine} off the safe line), {diamonds} diamonds ({diamondsNearLine} on the safe line), {nitros} nitro");
            Assert.That(coins, Is.GreaterThan(300));
            Assert.AreEqual(0, coinsOffLine, "coins follow the safe line");
            Assert.That(diamonds, Is.GreaterThan(10));
            Assert.That(diamondsNearLine, Is.LessThanOrEqualTo(diamonds / 4), "diamonds are on risky lines");
            Assert.That(nitros, Is.InRange(7, 12), "roughly one nitro per kilometre");
        }
    }
}
