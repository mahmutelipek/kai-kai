using System;

namespace Game.Simulation
{
    /// <summary>
    /// Maps distance to difficulty 0..1 and everything derived from it. Difficulty raises speed AND decision
    /// complexity: harder chunk types, narrower roads, denser obstacles, faster / oncoming traffic, combined hazards.
    /// </summary>
    public sealed class DifficultyManager
    {
        readonly BoardTuningData _tuning;

        public DifficultyManager(BoardTuningData tuning) { _tuning = tuning; }

        /// <summary>0 at the start, 1 at difficultyFullDistance, with a gentle wave so it breathes.</summary>
        public float At(float along)
        {
            float full = Math.Max(_tuning.difficultyFullDistance, 100f);
            float linear = SimMath.Clamp01(along / full);
            float wave = 0.06f * MathF.Sin(along / 900f * SimMath.TwoPi) * linear;
            return SimMath.Clamp01(linear + wave);
        }

        /// <summary>Early: wide; late: narrow.</summary>
        public float RoadWidth(float d) => SimMath.Lerp(14f, 10f, d);

        /// <summary>Chunk rating the generator aims for (1..10).</summary>
        public float TargetRating(float d) => 1.5f + 6.5f * d;

        public float ObstacleDensity(float d) => SimMath.Lerp(0.3f, 1f, d);

        public float TrafficSpeed(float d) => SimMath.Lerp(8f, 15f, d);

        public bool OncomingTraffic(float d) => d > 0.55f;

        /// <summary>Late game may chain hazards (ramp straight into a hard curve, intense after intense).</summary>
        public bool CombinedHazards(float d) => d > 0.65f;

        public float SpeedCapMultiplier(float d) => 1f + _tuning.difficultySpeedCapBonus * d;

        /// <summary>Speed the board will roughly have at this difficulty (used to space hazards so they stay reactable).</summary>
        public float ExpectedSpeed(float d) => _tuning.softCapSpeed * SpeedCapMultiplier(d) * SimMath.Lerp(0.5f, 1f, SimMath.Clamp01(d * 3f));
    }
}
