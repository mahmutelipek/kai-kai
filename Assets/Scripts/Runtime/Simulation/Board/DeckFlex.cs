using System;

namespace Game.Simulation
{
    /// <summary>
    /// The deck's reaction to riders jumping (visual; steering and physics are unaffected): a rider pushing off dips
    /// the deck under their feet, landing again thumps it harder. Heave (metres, down negative), roll (radians, right
    /// side down positive) and pitch (radians, nose down positive), each a damped spring (~2.6 Hz, a couple of bounces) kicked where the
    /// rider stands, so a jump on the left edge rocks the board to the left. Several riders add up; crew ollies and
    /// board landings are handled by the board itself.
    /// </summary>
    public sealed class DeckFlex
    {
        public const float Stiffness = 270f, Damping = 9f;
        /// <summary>Downward deck speed per take-off / per m/s of landing speed; roll and pitch rate at the deck edge.</summary>
        public const float TakeoffKick = 1.3f, LandingKick = 0.3f, RollKick = 1.2f, PitchKick = 0.6f;
        public const float MaxHeave = 0.12f, MaxAngle = 0.07f;

        public float Heave, Roll, Pitch;
        public float PreviousHeave, PreviousRoll, PreviousPitch;
        float _vh, _vr, _vp;

        public void Reset()
        {
            Heave = Roll = Pitch = PreviousHeave = PreviousRoll = PreviousPitch = 0f;
            _vh = _vr = _vp = 0f;
        }

        /// <summary>A rider at deck-local (x across, z along, both in metres) pushes the deck with <paramref name="strength"/> m/s.</summary>
        public void Kick(float x, float z, float strength, BoardTuningData t)
        {
            float fx = SimMath.Clamp(x / Math.Max(t.HalfWidth, 0.1f), -1f, 1f);
            float fz = SimMath.Clamp(z / Math.Max(t.HalfLength, 0.1f), -1f, 1f);
            _vh -= strength;
            _vr += fx * strength * RollKick;   // pressing the right side (x > 0) drops the right side
            _vp += fz * strength * PitchKick;  // pressing near the nose (z > 0) drops the nose
        }

        public void Step(float dt)
        {
            PreviousHeave = Heave; PreviousRoll = Roll; PreviousPitch = Pitch;
            Spring(ref Heave, ref _vh, dt, MaxHeave);
            Spring(ref Roll, ref _vr, dt, MaxAngle);
            Spring(ref Pitch, ref _vp, dt, MaxAngle);
        }

        static void Spring(ref float x, ref float v, float dt, float limit)
        {
            v += (-Stiffness * x - Damping * v) * dt;
            x = SimMath.Clamp(x + v * dt, -limit, limit);
        }
    }
}
