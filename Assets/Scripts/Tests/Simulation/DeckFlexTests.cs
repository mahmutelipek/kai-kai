using System;
using System.Numerics;
using Game.Simulation;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>Riders jumping and landing rock the deck a little (visual flex), toward the side they stand on.</summary>
    public class DeckFlexTests
    {
        // jumping is off in the shipped game; these tests cover the rule itself
        [SetUp] public void JumpsOn() => GameRules.PlayerJump = true;
        [TearDown] public void JumpsOff() => GameRules.PlayerJump = false;

        [Test]
        public void JumpOnTheLeftEdge_DipsAndRocksTheDeckLeft_ThenSettles()
        {
            var t = BoardScenario.TuningAtSpeed(15f);
            BoardSimulation sim = BoardScenario.Create(2, t);
            var inputs = new PlayerInputState[2];
            for (int i = 0; i < 60; i++) sim.Step(BoardScenario.Dt, inputs);
            sim.Players[0].PlaceAt(new Vector2(-t.HalfWidth * 0.8f, t.HalfLength * 0.5f));
            sim.Players[1].PlaceAt(new Vector2(t.HalfWidth * 0.2f, -t.HalfLength * 0.5f));
            for (int i = 0; i < 120; i++) sim.Step(BoardScenario.Dt, inputs);
            Assert.AreEqual(0f, sim.Flex.Heave, 1e-3f, "calm deck");

            float minHeave = 0f, minRoll = 0f, maxPitch = 0f;
            bool landed = false;
            for (int i = 0; i < 120; i++)
            {
                inputs[0] = new PlayerInputState(Vector2.Zero, i == 0, false);
                sim.Step(BoardScenario.Dt, inputs);
                landed |= sim.Players[0].JustLanded;
                minHeave = Math.Min(minHeave, sim.Flex.Heave);
                minRoll = Math.Min(minRoll, sim.Flex.Roll);
                maxPitch = Math.Max(maxPitch, sim.Flex.Pitch);
            }
            TestContext.WriteLine($"left-front rider jump: heave {minHeave:0.000} m, roll {minRoll * 57.3f:0.0} deg, pitch {maxPitch * 57.3f:0.0} deg, landed {landed}");
            Assert.IsTrue(landed);
            Assert.That(minHeave, Is.InRange(-DeckFlex.MaxHeave, -0.03f), "the deck dips noticeably but lightly");
            Assert.That(minRoll, Is.LessThan(-0.025f), "rocks toward the left edge the rider stood on");
            Assert.That(maxPitch, Is.GreaterThan(0.002f), "and toward the nose");
            for (int i = 0; i < 90; i++) sim.Step(BoardScenario.Dt, inputs);
            Assert.AreEqual(0f, sim.Flex.Heave, 0.005f, "settles in about a second");
            Assert.AreEqual(0f, sim.Flex.Roll, 0.005f);
        }
    }
}
