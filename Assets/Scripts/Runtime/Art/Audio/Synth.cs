using System;

namespace Game.Audio
{
    /// <summary>Every one-shot sound in the game.</summary>
    public enum Sfx : byte
    {
        None,
        Coin, Diamond, NitroPickup, NitroFire, CarveBoost, Ollie, PerfectOllie, Land, Bump, Hit, Crash,
        NearMiss, ComboUp, Slipstream, CountdownTick, CountdownGo, UiMove, UiSelect, UiBack, Join, Respawn,
    }

    /// <summary>Seamless loops, mixed continuously from the ride state.</summary>
    public enum Loop : byte { Wind, Roll, NitroBurn, Music }

    /// <summary>
    /// Procedural sound bank: every effect and loop is synthesised from oscillators, noise and filters at start-up,
    /// so the game ships with complete, license-free placeholder audio and no asset files. Engine-free (mono float
    /// PCM at <see cref="SampleRate"/>); Unity wraps the buffers in AudioClips. Deterministic (fixed seeds).
    /// Designed to be replaced sound by sound with recorded / composed assets later.
    /// </summary>
    public static class Synth
    {
        public const int SampleRate = 32000;
        public const float MusicBpm = 128f;
        public const int MusicBars = 4;

        public static float[] Build(Sfx s)
        {
            switch (s)
            {
                case Sfx.Coin: return Coin();
                case Sfx.Diamond: return Diamond();
                case Sfx.NitroPickup: return NitroPickup();
                case Sfx.NitroFire: return NitroFire();
                case Sfx.CarveBoost: return CarveBoost();
                case Sfx.Ollie: return Ollie(false);
                case Sfx.PerfectOllie: return Ollie(true);
                case Sfx.Land: return Thud(0.22f, 95f, 0.9f, 11);
                case Sfx.Bump: return Thud(0.12f, 130f, 0.6f, 12);
                case Sfx.Hit: return Hit();
                case Sfx.Crash: return Crash();
                case Sfx.NearMiss: return Whoosh(0.5f, 500f, 2600f, 0.8f, 21);
                case Sfx.ComboUp: return Blip(0.11f, 660f, 990f, 0.55f, square: true);
                case Sfx.Slipstream: return Whoosh(0.7f, 300f, 1100f, 0.55f, 22);
                case Sfx.CountdownTick: return Tone(0.18f, 880f, 0.7f);
                case Sfx.CountdownGo: return Chord(0.55f, 0.7f, 1046.5f, 1318.5f, 1568f);
                case Sfx.UiMove: return Blip(0.035f, 1400f, 1400f, 0.35f, square: false);
                case Sfx.UiSelect: return Arp(0.06f, 0.45f, 784f, 1175f);
                case Sfx.UiBack: return Arp(0.06f, 0.4f, 1175f, 784f);
                case Sfx.Join: return Arp(0.07f, 0.5f, 784f, 988f, 1175f, 1568f);
                case Sfx.Respawn: return Sweep(0.45f, 300f, 1200f, 0.5f);
                default: return new float[1];
            }
        }

        public static float[] BuildLoop(Loop l)
        {
            switch (l)
            {
                case Loop.Wind: return WindLoop();
                case Loop.Roll: return RollLoop();
                case Loop.NitroBurn: return BurnLoop();
                case Loop.Music: return Music();
                default: return new float[1];
            }
        }

        // ------------------------------------------------------------------ building blocks

        struct Rng
        {
            uint _s;
            public Rng(uint seed) { _s = seed * 747796405u + 2891336453u; if (_s == 0) _s = 1; }
            public float Next() { _s ^= _s << 13; _s ^= _s >> 17; _s ^= _s << 5; return (_s & 0xFFFFFF) / 8388608f - 1f; } // -1..1
        }

        /// <summary>Chamberlin state-variable filter (low / band / high outputs).</summary>
        struct Svf
        {
            float _low, _band;
            public float Low, Band, High;
            public void Run(float x, float cutoff, float q)
            {
                float f = 2f * (float)Math.Sin(Math.PI * Math.Min(cutoff, SampleRate * 0.16f) / SampleRate);
                float damp = 1f / Math.Max(q, 0.5f);
                _low += f * _band;
                float high = x - _low - damp * _band;
                _band += f * high;
                Low = _low; Band = _band; High = high;
            }
        }

        static int N(float seconds) => Math.Max(1, (int)(seconds * SampleRate));
        const float Tau = (float)(Math.PI * 2.0);

        static float Exp(float t, float decay) => (float)Math.Exp(-t * decay);
        static float Attack(float t, float a) => t >= a ? 1f : t / a;

        static float Square(float phase) => Math.Sign(Math.Sin(phase)) * 0.8f + 0.2f * (float)Math.Sin(phase); // softened
        static float Tri(float phase)
        {
            float p = phase / Tau; p -= (float)Math.Floor(p);
            return 4f * Math.Abs(p - 0.5f) - 1f;
        }
        static float Saw(float phase)
        {
            float p = phase / Tau; p -= (float)Math.Floor(p);
            return 2f * p - 1f;
        }

        /// <summary>Scales the buffer so its peak is <paramref name="peak"/> and removes any DC offset.</summary>
        static float[] Finish(float[] x, float peak)
        {
            double mean = 0; for (int i = 0; i < x.Length; i++) mean += x[i];
            float dc = (float)(mean / x.Length);
            float max = 1e-6f;
            for (int i = 0; i < x.Length; i++) { x[i] -= dc; max = Math.Max(max, Math.Abs(x[i])); }
            float k = peak / max;
            for (int i = 0; i < x.Length; i++) x[i] *= k;
            // 3 ms fade out so no clip ends on a click
            int fade = Math.Min(x.Length, N(0.003f));
            for (int i = 0; i < fade; i++) x[x.Length - 1 - i] *= i / (float)fade;
            return x;
        }

        /// <summary>Makes a loop seamless: renders <c>len + fade</c> samples and crossfades the tail into the head.</summary>
        static float[] Seamless(float[] raw, int len)
        {
            int fade = raw.Length - len;
            var x = new float[len];
            Array.Copy(raw, x, len);
            for (int i = 0; i < fade; i++)
            {
                float w = i / (float)fade;
                x[i] = raw[i] * (float)Math.Sqrt(w) + raw[len + i] * (float)Math.Sqrt(1f - w);
            }
            return x;
        }

        // ------------------------------------------------------------------ one-shots

        static float[] Coin()
        {
            // the classic two-note pickup: B5 then E6
            var x = new float[N(0.32f)];
            float ph = 0f;
            for (int i = 0; i < x.Length; i++)
            {
                float t = i / (float)SampleRate;
                float f = t < 0.06f ? 987.8f : 1318.5f;
                ph += Tau * f / SampleRate;
                float env = t < 0.06f ? 1f : Exp(t - 0.06f, 11f);
                x[i] = (Square(ph) * 0.6f + (float)Math.Sin(ph * 2f) * 0.25f) * env * Attack(t, 0.002f);
            }
            return Finish(x, 0.55f);
        }

        static float[] Diamond()
        {
            // bright sparkle arpeggio C6 E6 G6 C7 with a shimmering tail
            float[] notes = { 1046.5f, 1318.5f, 1568f, 2093f };
            var x = new float[N(0.75f)];
            for (int n = 0; n < notes.Length; n++)
            {
                int start = N(0.055f * n);
                for (int i = start; i < x.Length; i++)
                {
                    float t = (i - start) / (float)SampleRate;
                    float env = Exp(t, n == notes.Length - 1 ? 5f : 14f) * Attack(t, 0.002f);
                    float a = Tau * notes[n] * t;
                    x[i] += ((float)Math.Sin(a) + 0.35f * (float)Math.Sin(a * 1.004f * 2f) + 0.2f * Tri(a * 3f)) * env;
                }
            }
            return Finish(x, 0.55f);
        }

        static float[] NitroPickup()
        {
            var x = new float[N(0.35f)];
            float ph = 0f;
            for (int i = 0; i < x.Length; i++)
            {
                float t = i / (float)SampleRate;
                float f = 400f * (float)Math.Pow(4f, Math.Min(1f, t / 0.25f));
                ph += Tau * f / SampleRate;
                x[i] = ((float)Math.Sin(ph) + 0.3f * Square(ph * 0.5f)) * Attack(t, 0.005f) * (t < 0.25f ? 1f : Exp(t - 0.25f, 30f));
            }
            return Finish(x, 0.5f);
        }

        static float[] NitroFire()
        {
            // jet ignition: a sub boom, a crackle, and a noise whoosh sweeping up
            var x = new float[N(1.2f)];
            var rng = new Rng(31);
            var svf = new Svf();
            float ph = 0f;
            for (int i = 0; i < x.Length; i++)
            {
                float t = i / (float)SampleRate;
                float noise = rng.Next();
                float cutoff = 300f + 3200f * (float)Math.Sqrt(Math.Min(1f, t / 0.6f));
                svf.Run(noise, cutoff, 1.4f);
                float whoosh = svf.Band * Attack(t, 0.05f) * Exp(t, 2.6f);
                float boomF = 70f * Exp(t, 3f) + 38f;
                ph += Tau * boomF / SampleRate;
                float boom = (float)Math.Sin(ph) * Exp(t, 5f) * Attack(t, 0.004f);
                float crackle = rng.Next() > 0.985f ? rng.Next() * Exp(t, 6f) : 0f;
                x[i] = whoosh * 1.6f + boom * 0.9f + crackle * 0.5f;
            }
            return Finish(x, 0.8f);
        }

        static float[] CarveBoost()
        {
            var x = Whoosh(0.5f, 600f, 2400f, 1f, 41);
            // + a bright confirmation chime on top
            for (int i = 0; i < x.Length; i++)
            {
                float t = i / (float)SampleRate - 0.06f;
                if (t < 0f) continue;
                float a = Tau * 1760f * t;
                x[i] += ((float)Math.Sin(a) + 0.5f * (float)Math.Sin(a * 1.5f)) * Exp(t, 9f) * 0.35f;
            }
            return Finish(x, 0.6f);
        }

        static float[] Ollie(bool perfect)
        {
            // tail pop: a sharp click, a wooden "tock" with a falling pitch, then (perfect) a sparkle
            var x = new float[N(perfect ? 0.6f : 0.25f)];
            var rng = new Rng(51);
            var svf = new Svf();
            float ph = 0f;
            for (int i = 0; i < x.Length; i++)
            {
                float t = i / (float)SampleRate;
                svf.Run(rng.Next(), 4000f, 0.8f);
                float click = svf.High * Exp(t, 250f);
                float f = 120f + 180f * Exp(t, 40f);
                ph += Tau * f / SampleRate;
                float tock = ((float)Math.Sin(ph) + 0.4f * Tri(ph * 2.7f)) * Exp(t, 28f);
                x[i] = click * 1.2f + tock;
                if (perfect && t > 0.08f)
                {
                    float u = t - 0.08f, a = Tau * 1568f * u;
                    x[i] += ((float)Math.Sin(a) + 0.4f * (float)Math.Sin(a * 2.01f)) * Exp(u, 7f) * 0.45f;
                }
            }
            return Finish(x, perfect ? 0.7f : 0.6f);
        }

        static float[] Thud(float seconds, float f0, float peak, uint seed)
        {
            var x = new float[N(seconds)];
            var rng = new Rng(seed);
            var svf = new Svf();
            float ph = 0f;
            for (int i = 0; i < x.Length; i++)
            {
                float t = i / (float)SampleRate;
                ph += Tau * (f0 * (0.55f + 0.45f * Exp(t, 30f))) / SampleRate;
                svf.Run(rng.Next(), 900f, 0.7f);
                x[i] = (float)Math.Sin(ph) * Exp(t, 18f) + svf.Low * Exp(t, 45f) * 1.5f;
            }
            return Finish(x, peak);
        }

        static float[] Hit()
        {
            var x = new float[N(0.35f)];
            var rng = new Rng(61);
            var svf = new Svf();
            float ph = 0f;
            for (int i = 0; i < x.Length; i++)
            {
                float t = i / (float)SampleRate;
                svf.Run(rng.Next(), 2200f * Exp(t, 6f) + 300f, 0.9f);
                ph += Tau * 110f / SampleRate;
                x[i] = svf.Low * Exp(t, 12f) * 1.4f + (float)Math.Sin(ph) * Exp(t, 20f) * 0.8f;
            }
            return Finish(x, 0.85f);
        }

        static float[] Crash()
        {
            // a big boom, a crunchy noise tail and scattered clatter (board and riders tumbling)
            var x = new float[N(1.3f)];
            var rng = new Rng(71);
            var svf = new Svf();
            float ph = 0f;
            for (int i = 0; i < x.Length; i++)
            {
                float t = i / (float)SampleRate;
                svf.Run(rng.Next(), 2600f * Exp(t, 3f) + 200f, 0.8f);
                ph += Tau * (55f + 40f * Exp(t, 8f)) / SampleRate;
                x[i] = svf.Low * Exp(t, 3.5f) * 1.5f + (float)Math.Sin(ph) * Exp(t, 6f);
            }
            var clatter = new Rng(72);
            for (int c = 0; c < 9; c++)
            {
                int start = N(0.1f + 0.9f * (clatter.Next() * 0.5f + 0.5f));
                float f = 300f + 500f * (clatter.Next() * 0.5f + 0.5f);
                for (int i = start; i < Math.Min(x.Length, start + N(0.06f)); i++)
                {
                    float t = (i - start) / (float)SampleRate;
                    x[i] += Tri(Tau * f * t) * Exp(t, 60f) * 0.5f * Exp(start / (float)SampleRate, 1.5f);
                }
            }
            return Finish(x, 0.9f);
        }

        /// <summary>Band-passed noise with a centre sweeping up then back down (a pass-by / doppler whoosh).</summary>
        static float[] Whoosh(float seconds, float fLow, float fHigh, float peak, uint seed)
        {
            var x = new float[N(seconds)];
            var rng = new Rng(seed);
            var svf = new Svf();
            for (int i = 0; i < x.Length; i++)
            {
                float u = i / (float)x.Length;
                float bell = (float)Math.Sin(Math.PI * Math.Pow(u, 0.7));
                svf.Run(rng.Next(), fLow + (fHigh - fLow) * bell, 2f);
                x[i] = svf.Band * bell * bell;
            }
            return Finish(x, peak);
        }

        static float[] Blip(float seconds, float f0, float f1, float peak, bool square)
        {
            var x = new float[N(seconds)];
            float ph = 0f;
            for (int i = 0; i < x.Length; i++)
            {
                float u = i / (float)x.Length, t = i / (float)SampleRate;
                ph += Tau * (f0 + (f1 - f0) * u) / SampleRate;
                x[i] = (square ? Square(ph) * 0.6f : (float)Math.Sin(ph)) * Attack(t, 0.002f) * (1f - u);
            }
            return Finish(x, peak);
        }

        static float[] Tone(float seconds, float f, float peak)
        {
            var x = new float[N(seconds)];
            for (int i = 0; i < x.Length; i++)
            {
                float t = i / (float)SampleRate, a = Tau * f * t;
                x[i] = ((float)Math.Sin(a) + 0.25f * Square(a)) * Attack(t, 0.003f) * Exp(t, 14f);
            }
            return Finish(x, peak);
        }

        static float[] Chord(float seconds, float peak, params float[] f)
        {
            var x = new float[N(seconds)];
            for (int i = 0; i < x.Length; i++)
            {
                float t = i / (float)SampleRate, v = 0f;
                for (int k = 0; k < f.Length; k++) v += (float)Math.Sin(Tau * f[k] * t) + 0.2f * Square(Tau * f[k] * t);
                x[i] = v * Attack(t, 0.004f) * Exp(t, 5f);
            }
            return Finish(x, peak);
        }

        static float[] Arp(float step, float peak, params float[] notes)
        {
            var x = new float[N(step * notes.Length + 0.12f)];
            for (int n = 0; n < notes.Length; n++)
            {
                int start = N(step * n);
                for (int i = start; i < x.Length; i++)
                {
                    float t = (i - start) / (float)SampleRate;
                    x[i] += (float)Math.Sin(Tau * notes[n] * t) * Attack(t, 0.002f) * Exp(t, 30f);
                }
            }
            return Finish(x, peak);
        }

        static float[] Sweep(float seconds, float f0, float f1, float peak)
        {
            var x = new float[N(seconds)];
            float ph = 0f;
            for (int i = 0; i < x.Length; i++)
            {
                float u = i / (float)x.Length, t = i / (float)SampleRate;
                ph += Tau * (f0 * (float)Math.Pow(f1 / f0, u)) / SampleRate;
                x[i] = ((float)Math.Sin(ph) + 0.3f * Tri(ph * 2f)) * Attack(t, 0.01f) * (1f - u);
            }
            return Finish(x, peak);
        }

        // ------------------------------------------------------------------ loops

        static float[] WindLoop()
        {
            // rushing air: low-passed noise whose cutoff breathes (gusts), plus a thin whistle band
            int len = N(4f), fade = N(0.5f);
            var raw = new float[len + fade];
            var rng = new Rng(81);
            var body = new Svf();
            var whistle = new Svf();
            for (int i = 0; i < raw.Length; i++)
            {
                float t = i / (float)SampleRate;
                float gust = 0.5f + 0.5f * (float)Math.Sin(Tau * 0.25f * t) * (float)Math.Sin(Tau * 0.6f * t + 1f);
                float n = rng.Next();
                body.Run(n, 500f + 900f * gust, 0.8f);
                whistle.Run(n, 1800f + 500f * gust, 6f);
                raw[i] = body.Low * (0.6f + 0.4f * gust) + whistle.Band * 0.12f;
            }
            return FinishLoop(Seamless(raw, len), 0.7f);
        }

        static float[] RollLoop()
        {
            // urethane on asphalt: brown-noise rumble + fine grain, and a soft clack every road seam (0.5 s at pitch 1)
            int len = N(2f), fade = N(0.25f);
            var raw = new float[len + fade];
            var rng = new Rng(91);
            var grain = new Svf();
            float brown = 0f;
            for (int i = 0; i < raw.Length; i++)
            {
                float t = i / (float)SampleRate;
                float n = rng.Next();
                brown = brown * 0.995f + n * 0.05f;
                grain.Run(n, 2500f, 0.7f);
                float seamT = t % 0.5f;
                float seam = (float)Math.Sin(Tau * 160f * seamT) * Exp(seamT, 45f);
                raw[i] = brown * 1.6f + grain.Band * 0.12f + seam * 0.25f;
            }
            return FinishLoop(Seamless(raw, len), 0.7f);
        }

        static float[] BurnLoop()
        {
            // nitro jet: roaring band noise over a low buzzing saw, with a fast flutter
            int len = N(2f), fade = N(0.25f);
            var raw = new float[len + fade];
            var rng = new Rng(101);
            var roar = new Svf();
            var hum = new Svf();
            float ph = 0f;
            for (int i = 0; i < raw.Length; i++)
            {
                float t = i / (float)SampleRate;
                float flutter = 0.8f + 0.2f * (float)Math.Sin(Tau * 23f * t);
                roar.Run(rng.Next(), 1300f, 1.2f);
                ph += Tau * 55f / SampleRate;
                hum.Run(Saw(ph) + 0.5f * Saw(ph * 1.5f), 400f, 0.9f);
                raw[i] = roar.Band * 1.4f * flutter + hum.Low * 0.35f;
            }
            return FinishLoop(Seamless(raw, len), 0.7f);
        }

        /// <summary>
        /// Upbeat party loop, 128 BPM, 4 bars of I-V-vi-IV in C (C G Am F): four-on-the-floor kick, clap on 2 and 4,
        /// off-beat hats, an eighth-note bass and off-beat chord stabs. Rendered with wrap-around so the tails of the
        /// last bar ring into the first and the loop is seamless by construction.
        /// </summary>
        static float[] Music()
        {
            float beat = 60f / MusicBpm;
            int len = N(beat * 4 * MusicBars);
            var x = new float[len];
            float[][] chords =
            {
                new[] { 261.6f, 329.6f, 392.0f },  // C
                new[] { 246.9f, 293.7f, 392.0f },  // G (B D G)
                new[] { 261.6f, 329.6f, 440.0f },  // Am (C E A)
                new[] { 261.6f, 349.2f, 440.0f },  // F (C F A)
            };
            float[] roots = { 65.41f, 49.0f, 55.0f, 43.65f }; // C2 G1 A1 F1

            for (int bar = 0; bar < MusicBars; bar++)
            for (int b = 0; b < 4; b++)
            {
                float beatStart = (bar * 4 + b) * beat;
                // kick: every beat
                Voice(x, beatStart, 0.3f, (t, _) => (float)Math.Sin(Tau * (45f * t + 110f * (1f - (float)Math.Exp(-t * 35f)) / 35f)) * Exp(t, 9f), 0.6f);
                // clap / snare on 2 and 4
                if (b == 1 || b == 3)
                {
                    var r = new Rng((uint)(bar * 4 + b + 200));
                    Voice(x, beatStart, 0.2f, (t, _) => (r.Next() * Exp(t, 22f) + 0.3f * (float)Math.Sin(Tau * 200f * t) * Exp(t, 30f)), 0.35f);
                }
                // hats on the off-beats
                {
                    var r = new Rng((uint)(bar * 4 + b + 400));
                    float prev = 0f;
                    Voice(x, beatStart + beat * 0.5f, 0.06f, (t, _) => { float n = r.Next(); float hp = n - prev; prev = n; return hp * Exp(t, 70f); }, 0.18f);
                }
                // bass: eighth notes on the root, octave jump on the last eighth of each beat pair
                for (int e = 0; e < 2; e++)
                {
                    float f = roots[bar] * (e == 1 && b % 2 == 1 ? 2f : 1f);
                    Voice(x, beatStart + e * beat * 0.5f, beat * 0.45f, (t, dur) =>
                        (Saw(Tau * f * t) * 0.6f + (float)Math.Sin(Tau * f * t)) * Attack(t, 0.004f) * (1f - t / dur), 0.15f);
                }
                // chord stab on the off-beat
                float[] ch = chords[bar];
                Voice(x, beatStart + beat * 0.5f, beat * 0.35f, (t, dur) =>
                {
                    float v = 0f;
                    for (int k = 0; k < ch.Length; k++) v += Square(Tau * ch[k] * t) * 0.5f + Saw(Tau * ch[k] * 1.003f * t) * 0.3f;
                    return v * Attack(t, 0.003f) * Exp(t, 9f);
                }, 0.32f);
                // sparkly arpeggio: chord tones an octave up, sixteenth notes, up-down
                for (int q = 0; q < 4; q++)
                {
                    int step = (b * 4 + q) % 6;
                    float note = ch[step < 3 ? step : 5 - step] * 2f;
                    Voice(x, beatStart + q * beat * 0.25f, beat * 0.24f, (t, dur) =>
                        (Square(Tau * note * t) * 0.5f + (float)Math.Sin(Tau * note * 2f * t) * 0.3f) * Attack(t, 0.002f) * Exp(t, 16f), 0.15f);
                }
            }
            // soften the square/saw fizz a touch
            var lp = new Svf();
            for (int pass = 0; pass < 2; pass++)
                for (int i = 0; i < x.Length; i++) { lp.Run(x[i], 6000f, 0.7f); if (pass == 1) x[i] = lp.Low; }
            return FinishLoop(x, 0.75f);
        }

        /// <summary>Adds a voice at <paramref name="start"/> seconds, wrapping past the loop end.</summary>
        static void Voice(float[] x, float start, float seconds, Func<float, float, float> f, float gain)
        {
            int s0 = N(start), n = N(seconds);
            for (int i = 0; i < n; i++)
                x[(s0 + i) % x.Length] += f(i / (float)SampleRate, seconds) * gain;
        }

        /// <summary>Like Finish but without the end fade (a loop's end joins its start).</summary>
        static float[] FinishLoop(float[] x, float peak)
        {
            double mean = 0; for (int i = 0; i < x.Length; i++) mean += x[i];
            float dc = (float)(mean / x.Length);
            float max = 1e-6f;
            for (int i = 0; i < x.Length; i++) { x[i] -= dc; max = Math.Max(max, Math.Abs(x[i])); }
            float k = peak / max;
            for (int i = 0; i < x.Length; i++) x[i] *= k;
            return x;
        }
    }
}
