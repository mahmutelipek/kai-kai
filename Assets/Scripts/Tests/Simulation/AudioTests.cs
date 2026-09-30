using System;
using System.Collections.Generic;
using System.Diagnostics;
using Game.Art;
using Game.Audio;
using Game.Simulation;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>
    /// Procedural audio, checked numerically (nobody listened to it here): every sound is present, never clips,
    /// has no DC offset or end click, loops wrap without a jump, the music keeps its beat; the mix follows the ride.
    /// </summary>
    public class AudioTests
    {
        static float Rms(float[] x, int from = 0, int to = -1)
        {
            if (to < 0) to = x.Length;
            double sum = 0;
            for (int i = from; i < to; i++) sum += x[i] * x[i];
            return (float)Math.Sqrt(sum / Math.Max(1, to - from));
        }

        [Test]
        public void Synth_EverySound_IsAudible_NeverClips_NoDcNoEndClick()
        {
            var watch = Stopwatch.StartNew();
            foreach (Sfx s in Enum.GetValues(typeof(Sfx)))
            {
                if (s == Sfx.None) continue;
                float[] x = Synth.Build(s);
                float seconds = x.Length / (float)Synth.SampleRate, peak = 0f;
                double mean = 0;
                foreach (float v in x)
                {
                    Assert.IsFalse(float.IsNaN(v) || float.IsInfinity(v), $"{s}: NaN");
                    peak = Math.Max(peak, Math.Abs(v));
                    mean += v;
                }
                mean /= x.Length;
                float rms = Rms(x);
                TestContext.WriteLine($"{s,-14} {seconds * 1000f,5:0} ms  peak {peak:0.00}  rms {rms:0.000}");
                Assert.That(seconds, Is.InRange(0.03f, 1.5f), $"{s} length");
                Assert.That(peak, Is.InRange(0.3f, 0.95f), $"{s} peak (headroom, not silent)");
                Assert.That(rms, Is.GreaterThan(0.02f), $"{s} is too quiet");
                Assert.That(Math.Abs(mean), Is.LessThan(0.01), $"{s} DC offset");
                Assert.That(Math.Abs(x[x.Length - 1]), Is.LessThan(0.01f), $"{s} ends on a click");
            }
            TestContext.WriteLine($"all one-shots built in {watch.ElapsedMilliseconds} ms");
        }

        [Test]
        public void Synth_Loops_WrapWithoutAJump_MusicIsExactlyFourBars()
        {
            var watch = Stopwatch.StartNew();
            foreach (Loop l in Enum.GetValues(typeof(Loop)))
            {
                float[] x = Synth.BuildLoop(l);
                // the step across the wrap must look like any other step inside the loop
                var steps = new List<float>(x.Length);
                for (int i = 1; i < x.Length; i++) steps.Add(Math.Abs(x[i] - x[i - 1]));
                steps.Sort();
                float p999 = steps[(int)(steps.Count * 0.999f)];
                float wrap = Math.Abs(x[0] - x[x.Length - 1]);
                float peak = 0f;
                foreach (float v in x) peak = Math.Max(peak, Math.Abs(v));
                TestContext.WriteLine($"{l,-10} {x.Length / (float)Synth.SampleRate:0.00} s  peak {peak:0.00}  rms {Rms(x):0.000}  wrap step {wrap:0.0000} (99.9% of steps < {p999:0.0000})");
                Assert.That(wrap, Is.LessThanOrEqualTo(p999), $"{l}: audible click at the loop point");
                Assert.That(peak, Is.InRange(0.3f, 0.95f), $"{l} peak");
                Assert.That(Rms(x), Is.GreaterThan(0.03f), $"{l} too quiet");
            }
            float[] music = Synth.BuildLoop(Loop.Music);
            float bars = music.Length / (float)Synth.SampleRate / (60f / Synth.MusicBpm * 4f);
            Assert.AreEqual(Synth.MusicBars, bars, 0.001f, "music loop length");
            TestContext.WriteLine($"all loops built in {watch.ElapsedMilliseconds} ms");
            Assert.That(watch.ElapsedMilliseconds, Is.LessThan(5000), "start-up synthesis must stay quick");
        }

        [Test]
        public void Synth_Music_KickLandsOnEveryBeat()
        {
            float[] x = Synth.BuildLoop(Loop.Music);
            int beat = (int)(Synth.SampleRate * 60f / Synth.MusicBpm), win = Synth.SampleRate / 50; // 20 ms
            int beats = x.Length / beat, onBeat = 0;
            for (int b = 1; b < beats; b++)
            {
                int s = b * beat;
                if (Rms(x, s, s + win) > 1.5f * Rms(x, s - win, s)) onBeat++;
            }
            TestContext.WriteLine($"{onBeat}/{beats - 1} beats start louder than the 20 ms before them");
            Assert.That(onBeat, Is.GreaterThanOrEqualTo(beats - 2), "four-on-the-floor");
        }

        /// <summary>Tools/SoundGen/sounds.json (ElevenLabs prompts) must name every effect and loop the game plays.</summary>
        [Test]
        public void SoundGenManifest_CoversEverySoundTheGamePlays()
        {
            string dir = TestContext.CurrentContext.TestDirectory, path = null;
            for (int i = 0; i < 8 && dir != null; i++, dir = System.IO.Path.GetDirectoryName(dir))
            {
                string p = System.IO.Path.Combine(dir, "Tools", "SoundGen", "sounds.json");
                if (System.IO.File.Exists(p)) { path = p; break; }
            }
            if (path == null) Assert.Ignore("Tools/SoundGen/sounds.json not found from the test directory");
            string json = System.IO.File.ReadAllText(path);
            foreach (Sfx s in Enum.GetValues(typeof(Sfx)))
                if (s != Sfx.None) StringAssert.Contains("\"name\": \"" + s + "\"", json, $"no prompt for {s}");
            foreach (Loop l in Enum.GetValues(typeof(Loop)))
                StringAssert.Contains("\"name\": \"" + l + "\"", json, $"no prompt for loop {l}");
        }

        static BoardState Ride(float speed, bool grounded = true, float nitro = 0f) =>
            new BoardState { Speed = speed, Grounded = grounded, NitroTimer = nitro };

        [Test]
        public void Mix_FollowsTheRide_WheelsWindNitroAndCrashDuck()
        {
            var t = new BoardTuningData();
            var feel = new SpeedFeel();
            var mix = new AudioMix();
            void Run(BoardState b, float seconds, bool running = true)
            {
                for (float time = 0f; time < seconds; time += 1f / 60f)
                {
                    feel.Update(b, t, 0f, running, 1f / 60f);
                    mix.Update(feel, b, t, running, 1f / 60f);
                }
            }

            Run(Ride(0f), 1f);
            Assert.That(mix.RollVolume, Is.InRange(0.1f, 0.15f), "standing: a faint roll");
            float slowWind = mix.WindVolume;
            Run(Ride(t.softCapSpeed), 1f);
            float fastRoll = mix.RollVolume, fastPitch = mix.RollPitch, fastWind = mix.WindVolume;
            Assert.That(fastRoll, Is.GreaterThan(0.6f));
            Assert.That(fastPitch, Is.GreaterThan(1.2f), "wheels whine higher with speed");
            Assert.That(fastWind, Is.GreaterThan(slowWind + 0.3f), "wind grows with speed");
            Assert.AreEqual(0f, mix.BurnVolume, 1e-3f);

            Run(Ride(t.softCapSpeed, nitro: 3f), 1f);
            Assert.That(mix.BurnVolume, Is.GreaterThan(0.7f), "nitro jet");
            Assert.That(mix.WindVolume, Is.GreaterThan(fastWind), "nitro blows more wind");

            Run(Ride(t.softCapSpeed, grounded: false), 0.5f);
            Assert.That(mix.RollVolume, Is.LessThan(0.05f), "airborne: wheels silent");

            var cues = new List<SfxCue>();
            mix.Cues(new RunStepEvents { Crashed = true }, 1, 10f, cues);
            Assert.AreEqual(Sfx.Crash, cues[0].Sound);
            BoardState crashed = Ride(0f);
            crashed.Crashed = true;
            Run(crashed, 0.1f);
            Assert.That(mix.MusicDuck, Is.LessThan(0.6f), "music ducks under the crash");
            Assert.That(mix.RollVolume, Is.LessThan(0.3f));
            Run(crashed, 2f);
            Assert.That(mix.MusicDuck, Is.GreaterThan(0.95f), "and comes back");
        }

        [Test]
        public void Mix_Cues_CoinChainsClimb_ComboSteps_LandingsScale_NoAllocation()
        {
            var mix = new AudioMix();
            var cues = new List<SfxCue>(16);
            float last = 0f;
            for (int i = 0; i < 5; i++)
            {
                cues.Clear();
                mix.Cues(new RunStepEvents { Coins = 1 }, 1, i * 0.2f, cues);
                Assert.AreEqual(Sfx.Coin, cues[0].Sound);
                Assert.That(cues[0].Pitch, Is.GreaterThan(last), "a quick coin chain climbs in pitch");
                last = cues[0].Pitch;
            }
            cues.Clear();
            mix.Cues(new RunStepEvents { Coins = 1 }, 1, 5f, cues);
            Assert.AreEqual(1f, cues[0].Pitch, "a new chain starts low");

            cues.Clear();
            mix.Cues(new RunStepEvents(), 2, 6f, cues);
            mix.Cues(new RunStepEvents(), 3, 6.1f, cues);
            mix.Cues(new RunStepEvents(), 3, 6.2f, cues);
            Assert.AreEqual(2, cues.Count, "one combo cue per new multiplier");
            Assert.That(cues[1].Pitch, Is.GreaterThan(cues[0].Pitch));

            cues.Clear();
            mix.Cues(new RunStepEvents { Landed = true, Airtime = 0.2f }, 3, 7f, cues);
            mix.Cues(new RunStepEvents { Landed = true, Airtime = 1.2f }, 3, 8f, cues);
            Assert.That(cues[1].Volume, Is.GreaterThan(cues[0].Volume), "big air lands louder");
            Assert.That(cues[1].Pitch, Is.LessThan(cues[0].Pitch), "and deeper");

            cues.Clear();
            mix.Cues(new RunStepEvents { NitroStarted = true, CarveBoost = 2, PerfectOllie = true, Ollie = true, NearMisses = 1 }, 3, 9f, cues);
            Assert.That(cues.ConvertAll(c => c.Sound), Is.EquivalentTo(new[] { Sfx.NitroFire, Sfx.CarveBoost, Sfx.PerfectOllie, Sfx.NearMiss }));

            var ev = new RunStepEvents { Coins = 1, NearMisses = 1, Bumps = 1 };
            for (int i = 0; i < 200; i++) { cues.Clear(); mix.Cues(ev, 3, 10f, cues); } // warm (JIT tiering)
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++) { cues.Clear(); mix.Cues(ev, 3, 10f + i, cues); }
            long alloc = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0, alloc, $"cues must not allocate per step ({alloc} B over 1000 steps)");
        }
    }
}
