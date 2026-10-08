using Game.Simulation;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>Poles, signs and trunks beside the road are solid: the board cannot drive through them.</summary>
    public class PostTests
    {
        [Test]
        public void BoardDrivingIntoAPole_IsStoppedByIt_NotThroughIt()
        {
            var run = new RunSimulation(new BoardTuningData { livesPerRun = 0 }, 3, 1, TestRoadLayout.Build());
            RoadChunk chunk = run.Road.Chunks[0];
            chunk.Posts.Add(new PostCircle { Along = 60f, Lateral = 0f, Radius = 0.4f }); // dead ahead on the centre line
            var none = new PlayerInputState[BoardSimulation.MaxPlayers];
            float dt = BoardScenario.Dt;
            int scrapes = 0, crashes = 0;
            float minSeparation = float.PositiveInfinity;
            for (int i = 0; i < 600 && run.Distance < 110f; i++)
            {
                RunStepEvents ev = run.Step(dt, none);
                scrapes += ev.WallScrapes;
                if (ev.Crashed) crashes++;
                // the pole must never end up inside the board's footprint
                BoardState b = run.Board.Board.State;
                RoadProjection p = run.Projection;
                if (p.Valid && !b.Crashed)
                {
                    float dAlong = System.Math.Abs(chunk.StartAlong + 60f - p.Along), dLat = System.Math.Abs(p.Lateral);
                    if (dAlong < run.Tuning.HalfLength && dLat < run.Tuning.HalfWidth) minSeparation = -1f;
                }
            }
            Assert.IsTrue(scrapes > 0 || crashes > 0, "the pole registered a hit");
            Assert.AreNotEqual(-1f, minSeparation, "the pole was never inside the board");
        }
    }
}
