using System.IO;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>Keeps test runs out of the player's own save files (high scores).</summary>
    [SetUpFixture]
    public class TestSetup
    {
        [OneTimeSetUp]
        public void Redirect()
        {
            string path = Path.Combine(Path.GetTempPath(), "kai_test_highscores.txt");
            if (File.Exists(path)) File.Delete(path);
            GameManager.HighScorePath = path;
        }

        [OneTimeTearDown]
        public void Restore() => GameManager.HighScorePath = null;
    }
}
