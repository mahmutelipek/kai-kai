using System;
using Game.Simulation;

namespace Game.Art
{
    /// <summary>
    /// One engine-free source for every "sense of speed" effect, so the HUD streaks, the camera, the post effects,
    /// the 3D wind particles and the audio all agree (skill game-feel: layer several small, consistent signals).
    /// Update once per rendered frame; OnStep for every simulation step's events. Everything is 0 at rest and
    /// with <see cref="ReduceMotion"/> the camera-moving parts (FOV punch, lens warp, chroma) stay 0.
    /// </summary>
    public sealed class SpeedFeel
    {
        /// <summary>0..1: speed above ~55 % of the soft cap, hard acceleration, nitro (1), carve boost, slipstream.</summary>
        public float Wind { get; private set; }
        /// <summary>0..1 blend toward the nitro (cyan) and carve-boost (orange) tints.</summary>
        public float Nitro { get; private set; }
        public float Boost { get; private set; }
        /// <summary>0..1 draft strength behind a car (smoothed).</summary>
        public float Draft { get; private set; }
        /// <summary>Post effects: URP ChromaticAberration intensity (0..1), LensDistortion (negative = punch in), motion blur.</summary>
        public float Chroma { get; private set; }
        public float LensDistortion { get; private set; }
        public float MotionBlur { get; private set; }
        /// <summary>Extra field of view in degrees on top of the speed FOV (nitro and boost punches, then a hold).</summary>
        public float FovPunch { get; private set; }
        /// <summary>0..1, decays from 1 right after a nitro / carve boost fires (bursts, shockwave, audio whoosh).</summary>
        public float Burst { get; private set; }

        public bool ReduceMotion;
        public const float BaseMotionBlur = 0.18f;

        float _punch;      // FOV / lens impulse, decays
        float _burst;

        public void Reset()
        {
            Wind = Nitro = Boost = Draft = Chroma = LensDistortion = FovPunch = Burst = 0f;
            MotionBlur = BaseMotionBlur;
            _punch = _burst = 0f;
        }

        public void OnStep(in RunStepEvents ev)
        {
            if (ev.NitroStarted) { _punch = 1f; _burst = 1f; }
            else if (ev.CarveBoost > 0) { _punch = Math.Max(_punch, ev.CarveBoost > 1 ? 0.8f : 0.55f); _burst = Math.Max(_burst, 0.7f); }
            else if (ev.SlipstreamStarted) _punch = Math.Max(_punch, 0.3f);
        }

        public void Update(RunSimulation run, float dt) =>
            Update(run.Board.Board.State, run.Board.Tuning, run.Slipstream, run.State == RunState.Running, dt);

        public void Update(in BoardState b, BoardTuningData t, float slipstream, bool running, float dt)
        {
            bool live = running && !b.Crashed;
            float speedNorm = b.Speed / Math.Max(t.softCapSpeed, 1f);
            float wind = Clamp01((speedNorm - 0.55f) / 0.5f) * 0.55f + Clamp01((b.Acceleration - 2f) / 8f) * 0.35f;
            if (b.NitroTimer > 0f) wind = Math.Max(wind, 1f);
            if (b.BoostTimer > 0f) wind = Math.Max(wind, 0.8f);
            wind = Math.Max(wind, slipstream * 0.6f);
            if (!live) wind = 0f;

            Wind = Approach(Wind, wind, 6f, dt);
            Nitro = Approach(Nitro, live && b.NitroTimer > 0f ? 1f : 0f, 5f, dt);
            Boost = Approach(Boost, live && b.BoostTimer > 0f ? 1f : 0f, 5f, dt);
            Draft = Approach(Draft, live ? slipstream : 0f, 4f, dt);

            _punch = Math.Max(0f, _punch - dt * 2.2f);
            _burst = Math.Max(0f, _burst - dt * 2.5f);
            Burst = _burst;
            float hold = Math.Max(Nitro, Boost * 0.6f);
            if (ReduceMotion)
            {
                Chroma = LensDistortion = FovPunch = 0f;
                MotionBlur = 0f;
                return;
            }
            // chroma: a sliver at top speed, strong on the nitro punch; lens: a quick "warp in" then relax
            Chroma = Clamp01(0.12f * Clamp01((speedNorm - 0.8f) / 0.3f) + 0.25f * hold + 0.6f * _punch * _punch);
            LensDistortion = -(0.32f * _punch * _punch + 0.1f * hold);
            MotionBlur = BaseMotionBlur + 0.22f * hold + 0.1f * Draft;
            FovPunch = 7f * _punch * _punch + 2f * Draft;
        }

        static float Approach(float v, float target, float rate, float dt) => v + (target - v) * Math.Min(1f, dt * rate);
        static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
