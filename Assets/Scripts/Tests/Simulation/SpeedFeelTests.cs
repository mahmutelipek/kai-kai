using System;
using Game.Art;
using Game.Simulation;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>
    /// The shared sense-of-speed signals (HUD wind streaks, camera FOV punch, chroma / lens / blur, 3D wind, audio):
    /// quiet at rest, full on nitro, a short punch on fire, respecting reduce-motion and the pause.
    /// </summary>
    public class SpeedFeelTests
    {
        static BoardState Riding(float speed, float nitro = 0f, float boost = 0f, float accel = 0f) =>
            new BoardState { Speed = speed, NitroTimer = nitro, BoostTimer = boost, Acceleration = accel, Grounded = true };

        static void Run(SpeedFeel f, BoardState b, BoardTuningData t, float seconds, float slip = 0f, bool running = true)
        {
            for (float time = 0f; time < seconds; time += 1f / 60f) f.Update(b, t, slip, running, 1f / 60f);
        }

        [Test]
        public void SpeedFeel_QuietAtRest_BuildsWithSpeed_FullOnNitro()
        {
            var t = new BoardTuningData();
            var f = new SpeedFeel();
            f.Reset();
            Run(f, Riding(2f), t, 1f);
            Assert.That(f.Wind, Is.LessThan(0.01f), "slow cruising: no wind");
            Assert.That(f.Chroma, Is.LessThan(0.01f));
            Assert.That(f.FovPunch, Is.LessThan(0.01f));

            Run(f, Riding(t.softCapSpeed * 0.9f), t, 1f);
            float cruise = f.Wind;
            Run(f, Riding(t.softCapSpeed * 0.9f, accel: 9f), t, 1f);
            float accelerating = f.Wind;
            Run(f, Riding(t.softCapSpeed, nitro: 2f), t, 1f);
            TestContext.WriteLine($"wind: cruise {cruise:0.00}, accelerating {accelerating:0.00}, nitro {f.Wind:0.00}; chroma {f.Chroma:0.00}, blur {f.MotionBlur:0.00}");
            Assert.That(cruise, Is.InRange(0.2f, 0.6f), "fast cruising: some wind");
            Assert.That(accelerating, Is.GreaterThan(cruise + 0.15f), "accelerating hard blows more wind from the sides");
            Assert.That(f.Wind, Is.GreaterThan(0.95f), "nitro: full wind");
            Assert.That(f.Nitro, Is.GreaterThan(0.95f));
            Assert.That(f.MotionBlur, Is.GreaterThan(SpeedFeel.BaseMotionBlur + 0.15f));
        }

        [Test]
        public void SpeedFeel_NitroFire_PunchesThenSettles_ReduceMotionKeepsCameraStill()
        {
            var t = new BoardTuningData();
            var f = new SpeedFeel();
            f.Reset();
            BoardState nitro = Riding(t.softCapSpeed, nitro: 3f);
            f.OnStep(new RunStepEvents { NitroStarted = true });
            f.Update(nitro, t, 0f, true, 1f / 60f);
            float fov0 = f.FovPunch, lens0 = f.LensDistortion, chroma0 = f.Chroma, burst0 = f.Burst;
            Run(f, nitro, t, 1f);
            TestContext.WriteLine($"fire: fov +{fov0:0.0} deg, lens {lens0:0.00}, chroma {chroma0:0.00}, burst {burst0:0.00}; after 1 s: fov +{f.FovPunch:0.0}, lens {f.LensDistortion:0.00}, chroma {f.Chroma:0.00}");
            Assert.That(fov0, Is.GreaterThan(5f), "nitro fire punches the FOV");
            Assert.That(lens0, Is.LessThan(-0.25f), "and warps the lens in");
            Assert.That(burst0, Is.GreaterThan(0.9f));
            Assert.That(f.FovPunch, Is.LessThan(0.5f), "the punch is short");
            Assert.That(f.LensDistortion, Is.InRange(-0.15f, 0f), "then only a slight hold while nitro burns");
            Assert.That(f.Chroma, Is.InRange(0.1f, chroma0), "chroma holds lower than the punch");
            Assert.That(f.Chroma, Is.LessThanOrEqualTo(1f));

            var calm = new SpeedFeel { ReduceMotion = true };
            calm.Reset();
            calm.OnStep(new RunStepEvents { NitroStarted = true });
            Run(calm, nitro, t, 0.2f);
            Assert.AreEqual(0f, calm.FovPunch);
            Assert.AreEqual(0f, calm.LensDistortion);
            Assert.AreEqual(0f, calm.Chroma);
            Assert.AreEqual(0f, calm.MotionBlur);
            Assert.That(calm.Wind, Is.GreaterThan(0.5f), "HUD wind stays: it is not camera motion");
        }

        [Test]
        public void SpeedFeel_CrashOrEndedRun_StopsTheWind_PauseFreezesIt()
        {
            var t = new BoardTuningData();
            var f = new SpeedFeel();
            f.Reset();
            Run(f, Riding(t.softCapSpeed * 0.8f), t, 0.1f);
            float before = f.Wind;
            for (int i = 0; i < 60; i++) f.Update(Riding(t.softCapSpeed, nitro: 2f), t, 0f, true, 0f); // paused: dt 0
            Assert.AreEqual(before, f.Wind, 1e-4f, "paused: nothing moves");
            BoardState crashed = Riding(0f);
            crashed.Crashed = true;
            Run(f, crashed, t, 1f);
            Assert.That(f.Wind, Is.LessThan(0.01f), "crash: wind stops");
            Run(f, Riding(t.softCapSpeed, nitro: 2f), t, 1f, running: false);
            Assert.That(f.Wind, Is.LessThan(0.01f), "run over: wind stops");
        }

        [Test]
        public void SpeedFeel_Slipstream_AddsDraftWindAndSmallFov()
        {
            var t = new BoardTuningData();
            var f = new SpeedFeel();
            f.Reset();
            Run(f, Riding(t.softCapSpeed * 0.5f), t, 1f, slip: 1f);
            TestContext.WriteLine($"draft {f.Draft:0.00}, wind {f.Wind:0.00}, fov +{f.FovPunch:0.0}");
            Assert.That(f.Draft, Is.GreaterThan(0.95f));
            Assert.That(f.Wind, Is.GreaterThan(0.5f));
            Assert.That(f.FovPunch, Is.InRange(1.5f, 2.5f));
        }
    }
}
