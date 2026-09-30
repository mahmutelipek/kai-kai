using System.Collections.Generic;
using System.IO;
using Game.Simulation;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>The local TOP 10 table: ordering, rank of the new run, cap, file round trip, old save files.</summary>
    public class HighScoreTableTests
    {
        sealed class MemoryStore : IHighScoreStore
        {
            public HighScoreRecord Saved;
            public int Saves;
            public HighScoreRecord Load() => Saved;
            public void Save(HighScoreRecord r) { Saved = r; Saves++; }
        }

        [Test]
        public void Table_KeepsTheTenBestRuns_InOrder_AndReportsTheRank()
        {
            var store = new MemoryStore();
            var hs = new HighScoreManager(store) { Today = () => "2026-09-30" };
            float[] scores = { 500, 1500, 300, 9000, 1200, 800, 50, 4000, 2500, 700, 100, 6000 };
            int lastRank = 0;
            foreach (float s in scores) hs.Submit(s, s / 2f, 4, out _, out _, out lastRank);
            Assert.AreEqual(HighScoreManager.TableSize, hs.Top.Count);
            for (int i = 1; i < hs.Top.Count; i++) Assert.That(hs.Top[i - 1].Score, Is.GreaterThanOrEqualTo(hs.Top[i].Score), "highest first");
            Assert.AreEqual(9000f, hs.Top[0].Score);
            Assert.AreEqual(2, lastRank, "6000 is second behind 9000");
            Assert.AreEqual(4, hs.Top[0].Crew);
            Assert.AreEqual("2026-09-30", hs.Top[0].Date);

            hs.Submit(10f, 5f, 1, out bool best, out _, out int rank);
            Assert.AreEqual(0, rank, "too low for the table");
            Assert.IsFalse(best);
            int saves = store.Saves;
            hs.Submit(10f, 5f, 1, out _, out _, out _);
            Assert.AreEqual(saves, store.Saves, "nothing to save when nothing changed");
        }

        [Test]
        public void File_RoundTrips_AndOldFilesStillLoad()
        {
            string dir = Path.Combine(Path.GetTempPath(), "downhill-hs-" + System.Guid.NewGuid().ToString("N"));
            string path = Path.Combine(dir, "highscores.txt");
            try
            {
                var hs = new HighScoreManager(new FileHighScoreStore(path)) { Today = () => "2026-09-30" };
                hs.Submit(1234f, 800f, 6, out _, out _, out _);
                hs.Submit(5678f, 1500f, 2, out _, out _, out _);
                var again = new HighScoreManager(new FileHighScoreStore(path));
                Assert.AreEqual(2, again.Top.Count);
                Assert.AreEqual(5678f, again.Top[0].Score);
                Assert.AreEqual(2, again.Top[0].Crew);
                Assert.AreEqual("2026-09-30", again.Top[1].Date);
                Assert.AreEqual(5678f, again.BestScore);

                // a file from before the table existed: its best becomes the first row
                File.WriteAllText(path, "bestScore=4200\nbestDistance=3100\n");
                var old = new HighScoreManager(new FileHighScoreStore(path));
                Assert.AreEqual(1, old.Top.Count);
                Assert.AreEqual(4200f, old.Top[0].Score);
                Assert.AreEqual(3100f, old.BestDistance);

                // corrupt lines are skipped
                File.WriteAllText(path, "entry=abc;1;2;x\nentry=100;50;3;2026-01-01\nentry=-5;1;1;x\n");
                var messy = new HighScoreManager(new FileHighScoreStore(path));
                Assert.AreEqual(1, messy.Top.Count);
            }
            finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
        }

        [Test]
        public void EndedRun_KnowsItsRank()
        {
            var run = new RunSimulation(new BoardTuningData { livesPerRun = 1 }, 5, 3);
            run.HighScores = new HighScoreManager(new MemoryStore());
            var none = new PlayerInputState[BoardSimulation.MaxPlayers];
            for (int i = 0; i < 600; i++) run.Step(1f / 60f, none);
            run.Board.CrashNow();
            for (int i = 0; i < 400 && run.State == RunState.Running; i++) run.Step(1f / 60f, none);
            Assert.AreEqual(RunState.Ended, run.State);
            Assert.AreEqual(1, run.LastRank, "the first run tops an empty table");
            Assert.AreEqual(3, run.HighScores.Top[0].Crew);
        }
    }
}
