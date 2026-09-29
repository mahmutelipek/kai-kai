using System;
using System.Collections.Generic;
using System.Numerics;

namespace Game.Simulation
{
    /// <summary>One player's contribution to the board weight model.</summary>
    public struct WeightSample
    {
        /// <summary>Board-local position in meters (X = right, Y = toward nose).</summary>
        public Vector2 LocalPosition;
        /// <summary>Effective weight: body mass factor times contact (0 when fallen, reduced while airborne).</summary>
        public float Weight;
    }

    /// <summary>Result of the weight model for one simulation step.</summary>
    public struct WeightResult
    {
        /// <summary>Weighted average of normalized lateral positions, -1 (left edge) .. +1 (right edge).</summary>
        public float Lateral;
        /// <summary>Weighted average of normalized longitudinal positions, -1 (tail) .. +1 (nose).</summary>
        public float Longitudinal;
        /// <summary>Lateral passed through the nonlinear response curve (before smoothing).</summary>
        public float RawSteering;
        public float TotalWeight;
        public int ContributingPlayers;
    }

    /// <summary>
    /// Pure function: board-local player positions -> weighted center of mass -> nonlinear steering value.
    /// Uses a weighted average, so the result is normalized by player count (2 players can reach the
    /// same range as 6; 6 players are not stronger than 2).
    /// </summary>
    public static class BoardWeightSystem
    {
        public static WeightResult Compute(IReadOnlyList<WeightSample> samples, int count, BoardTuningData tuning)
        {
            var result = new WeightResult();
            float halfWidth = Math.Max(tuning.HalfWidth, 1e-3f);
            float halfLength = Math.Max(tuning.HalfLength, 1e-3f);

            float sumW = 0f, sumX = 0f, sumZ = 0f;
            int contributing = 0;
            for (int i = 0; i < count; i++)
            {
                WeightSample s = samples[i];
                if (!(s.Weight > 0f) || !SimMath.IsFinite(s.LocalPosition)) continue;
                float nx = SimMath.Clamp(s.LocalPosition.X / halfWidth, -1f, 1f);
                float nz = SimMath.Clamp(s.LocalPosition.Y / halfLength, -1f, 1f);
                sumW += s.Weight;
                sumX += nx * s.Weight;
                sumZ += nz * s.Weight;
                contributing++;
            }

            result.TotalWeight = sumW;
            result.ContributingPlayers = contributing;
            if (sumW <= 1e-6f) return result; // nobody on the board: neutral

            result.Lateral = SimMath.Clamp(sumX / sumW, -1f, 1f);
            result.Longitudinal = SimMath.Clamp(sumZ / sumW, -1f, 1f);
            result.RawSteering = SimMath.SignedPow(result.Lateral, tuning.steeringExponent);
            return result;
        }
    }
}
