using System.Collections.Generic;
using System.Numerics;
using Game.Simulation;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>Obstacle contact is detected at the moment the board touches, not after it has driven inside.</summary>
    public class ContactTests
    {
        static bool HitsAtGap(ObstacleKind kind, float gapAheadOfNose, float stepDistance)
        {
            var t = new BoardTuningData();
            var field = new ObstacleField();
            Vector2 half = ObstacleCatalog.DefaultHalfExtents(kind);
            float z = t.HalfLength + half.Y + gapAheadOfNose;
            int slot = field.Spawn(new Obstacle { Kind = kind, HalfExtents = half, Height = ObstacleCatalog.Height(kind), Along = z });
            field.Items[slot].Position = new Vector3(0f, 0f, z);
            var b = new BoardState { Position = Vector3.Zero, Yaw = 0f, TravelYaw = 0f, Speed = 35f, Grounded = true };
            return field.Collide(b, t, 0f, new List<ImpactEvent>(), stepDistance) > 0;
        }

        [Test]
        public void FastBoard_KnocksLightObstaclesOnTouch_NotAWholeStepLate()
        {
            Assert.IsFalse(HitsAtGap(ObstacleKind.Cone, 0.5f, 0.7f), "0.5 m clear of the cone: no hit yet");
            Assert.IsTrue(HitsAtGap(ObstacleKind.Cone, 0.2f, 0.7f), "0.2 m short at 35 m/s is within a step of touching: the cone is knocked now");
            Assert.IsFalse(HitsAtGap(ObstacleKind.Cone, 0.2f, 0f), "without the lead the end-of-step test would still be waiting");
        }

        [Test]
        public void SolidObstacles_NeedARealOverlap()
        {
            Assert.IsFalse(HitsAtGap(ObstacleKind.ConcreteBarrier, 0.05f, 0.7f), "a clean pass is never turned into a crash");
            Assert.IsTrue(HitsAtGap(ObstacleKind.ConcreteBarrier, -0.05f, 0.7f), "touching / overlapping: a hit");
        }
    }
}
