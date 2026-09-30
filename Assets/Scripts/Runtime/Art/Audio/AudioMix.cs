using System;
using System.Collections.Generic;
using Game.Art;
using Game.Simulation;

namespace Game.Audio
{
    public struct SfxCue
    {
        public Sfx Sound;
        public float Volume, Pitch;
        public SfxCue(Sfx s, float volume = 1f, float pitch = 1f) { Sound = s; Volume = volume; Pitch = pitch; }
    }

    /// <summary>
    /// Engine-free sound logic: continuous loop levels from the ride (wheels roll with speed, wind with the speed
    /// feel, the nitro jet while it burns, music ducks after a crash) and which one-shots a simulation step fires,
    /// with pitch variation so repeats do not sound robotic (coin chains climb, combo steps climb, landings scale
    /// with impact). The Unity AudioDirector only plays what this decides.
    /// </summary>
    public sealed class AudioMix
    {
        public float WindVolume, WindPitch = 1f, RollVolume, RollPitch = 1f, BurnVolume, BurnPitch = 1f, MusicDuck = 1f;

        const float CoinChainWindow = 0.7f;
        int _coinChain, _lastMultiplier = 1;
        float _lastCoinTime = -99f, _duck;
        int _variation;

        public void Reset()
        {
            WindVolume = RollVolume = BurnVolume = 0f;
            WindPitch = RollPitch = BurnPitch = MusicDuck = 1f;
            _coinChain = 0; _lastMultiplier = 1; _lastCoinTime = -99f; _duck = 0f;
        }

        /// <param name="dt">Game time (0 while paused: the loops hold, the director fades them for the pause).</param>
        public void Update(SpeedFeel feel, in BoardState b, BoardTuningData t, bool running, float dt)
        {
            float speedNorm = Math.Min(1.3f, b.Speed / Math.Max(t.softCapSpeed, 1f));
            bool live = running && !b.Crashed;
            float roll = live && b.Grounded ? 0.12f + 0.63f * Math.Min(1f, speedNorm) : 0f;
            float wind = live ? 0.04f + 0.3f * speedNorm * speedNorm + 0.5f * feel.Wind : 0.02f;
            if (live && !b.Grounded) wind += 0.1f; // airtime: the wheels go quiet, the air gets louder
            float burn = live ? 0.85f * feel.Nitro : 0f;

            float k = Math.Min(1f, dt * 10f);
            RollVolume += (roll - RollVolume) * k;
            WindVolume += (Math.Min(1f, wind) - WindVolume) * Math.Min(1f, dt * 5f);
            BurnVolume += (burn - BurnVolume) * k;
            RollPitch = 0.55f + 0.85f * speedNorm;
            WindPitch = 0.75f + 0.45f * speedNorm + 0.15f * feel.Nitro;
            BurnPitch = 0.95f + 0.1f * speedNorm;

            _duck = Math.Max(0f, _duck - dt * 0.6f);
            MusicDuck = 1f - 0.5f * _duck;
        }

        /// <summary>Appends the one-shots for one simulation step (no allocation once <paramref name="into"/> has grown).</summary>
        public void Cues(in RunStepEvents ev, int multiplier, float time, List<SfxCue> into)
        {
            if (ev.Coins > 0)
            {
                _coinChain = time - _lastCoinTime < CoinChainWindow ? _coinChain + ev.Coins : 1;
                _lastCoinTime = time;
                into.Add(new SfxCue(Sfx.Coin, 0.8f, 1f + 0.06f * Math.Min(_coinChain - 1, 10)));
            }
            if (ev.Diamonds > 0) into.Add(new SfxCue(Sfx.Diamond, 0.9f));
            if (ev.NitroPickups > 0) into.Add(new SfxCue(Sfx.NitroPickup, 0.8f));
            if (ev.NitroStarted) into.Add(new SfxCue(Sfx.NitroFire, 1f));
            if (ev.CarveBoost > 0) into.Add(new SfxCue(Sfx.CarveBoost, 0.85f, ev.CarveBoost > 1 ? 1.12f : 1f));
            if (ev.PerfectOllie) into.Add(new SfxCue(Sfx.PerfectOllie, 0.9f));
            else if (ev.Ollie) into.Add(new SfxCue(Sfx.Ollie, 0.8f, Vary(0.06f)));
            if (ev.SlipstreamStarted) into.Add(new SfxCue(Sfx.Slipstream, 0.6f));
            if (ev.NearMisses > 0) into.Add(new SfxCue(Sfx.NearMiss, 0.75f, Vary(0.1f)));

            if (ev.Crashed)
            {
                into.Add(new SfxCue(Sfx.Crash, 1f));
                _duck = 1f;
            }
            else if (ev.HeavyHits > 0 || ev.WallScrapes > 0) into.Add(new SfxCue(Sfx.Hit, 0.85f, Vary(0.08f)));
            else if (ev.LightHits > 0 || ev.Bumps > 0) into.Add(new SfxCue(Sfx.Bump, 0.6f, Vary(0.1f)));

            if (ev.Landed)
            {
                // harder landings (longer airtime) thud deeper and louder
                float hard = Math.Min(1f, ev.Airtime / 1.2f);
                into.Add(new SfxCue(Sfx.Land, 0.45f + 0.5f * hard, 1.1f - 0.25f * hard));
            }
            if (ev.Respawned) into.Add(new SfxCue(Sfx.Respawn, 0.6f));

            if (multiplier > _lastMultiplier)
                into.Add(new SfxCue(Sfx.ComboUp, 0.7f, (float)Math.Pow(2.0, (multiplier - 2) * 2 / 12.0))); // up a whole tone per step
            _lastMultiplier = multiplier;
        }

        /// <summary>Deterministic small pitch spread (cycles through a fixed pattern).</summary>
        float Vary(float amount)
        {
            _variation = (_variation + 1) % 5;
            return 1f + amount * (_variation - 2) * 0.5f;
        }
    }
}
