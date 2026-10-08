using System.Collections.Generic;
using Game.Hud;
using Game.Simulation;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>With scoring off (the shipped rule) the only goal is distance: no coins or diamonds on the road, nothing points-related on the HUD.</summary>
    public class NoPointsTests
    {
        [Test]
        public void Road_HasNoCoinsDiamondsOrNitro()
        {
            Assert.IsFalse(GameRules.Scoring);
            var run = new RunSimulation(new BoardTuningData { livesPerRun = 0 }, 5, 1);
            int coins = 0, diamonds = 0, nitro = 0;
            for (float d = 0f; d < 3000f; d += 60f)
            {
                run.Generator.Update(d);
                for (int i = 0; i < run.Pickups.Items.Length; i++)
                {
                    if (!run.Pickups.Items[i].Active) continue;
                    PickupKind k = run.Pickups.Items[i].Kind;
                    if (k == PickupKind.Coin) coins++; else if (k == PickupKind.Diamond) diamonds++; else if (k == PickupKind.Nitro) nitro++;
                }
            }
            Assert.AreEqual(0, coins);
            Assert.AreEqual(0, diamonds);
            Assert.AreEqual(0, nitro, "no nitro either");
        }

        [Test]
        public void Hud_ShowsNoScoreCoinsOrCombo()
        {
            var s = new HudState { Score = "12,345", Coins = "77", Diamonds = "3", ShowCombo = true, Combo = "x4" };
            var cmds = new List<HudCmd>();
            HudLayout.Build(s, 1920f, 1080f, 0f, 0f, 1920f, 1080f, cmds);
            foreach (HudCmd c in cmds)
            {
                Assert.AreNotEqual("12,345", c.Text, "score pill");
                Assert.AreNotEqual("77", c.Text, "coin count");
                Assert.AreNotEqual("x4", c.Text, "combo");
            }
        }
    }
}
