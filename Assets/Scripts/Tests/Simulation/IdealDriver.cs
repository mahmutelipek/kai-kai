using System;
using System.Numerics;
using Game.Simulation;

namespace Game.Tests
{
    /// <summary>
    /// Scripted "ideal driver" for M2 acceptance: it only decides the target lateral / longitudinal weight and
    /// walks every player (pinned, speed-limited to the player move speed) to realise it. The board itself is
    /// simulated exactly as in the game: smoothing, danger, crashes, obstacles. No steering shortcuts.
    /// </summary>
    public sealed class IdealDriver
    {
        readonly RunSimulation _run;
        int _predictHint;

        /// <summary>Total response delay of the board pipeline in seconds; -1 = derive from tuning (CrewResponseDelay).</summary>
        public float ResponseDelay = -1f;

        /// <summary>Danger the driver is willing to use (wobble starts at 0.7).</summary>
        public float SafeDanger = 0.6f;
        public float LastTargetLateral { get; private set; }
        public float LastTargetLongitudinal { get; private set; }

        public IdealDriver(RunSimulation run) { _run = run; }

        public void Drive(float dt)
        {
            BoardSimulation sim = _run.Board;
            BoardTuningData t = sim.Tuning;
            BoardState b = sim.Board.State;
            float maxYawRate = (t.yawRateBaseDeg + t.yawRatePerSpeedDeg * b.Speed) * SimMath.Deg2Rad;
            float stability = SimMath.Lerp(t.stabilityFactorLowSpeed, t.stabilityFactorHighSpeed, b.Speed / t.stabilityReferenceSpeed);
            // off the line: accept the wobble zone (still below the crash threshold) to get back
            float lineError = System.Math.Abs(_run.Projection.Lateral - _run.Road.PlannedLateral(_run.Distance));
            float allowedDanger = lineError > 1f ? 0.8f : SafeDanger;
            float safeSteer = allowedDanger * t.crashThreshold / stability;

            float steer = SimMath.Clamp(PredictiveSteer(b, maxYawRate), -safeSteer, safeSteer);
            float lateral = SimMath.Clamp(SimMath.SignedPow(steer, 1f / t.steeringExponent), -0.72f, 0.72f);

            // speed: brake before curves that would need more than the safe steering at this speed
            float required = RequiredSteerAhead(b.Speed, maxYawRate);
            bool weaveAhead = b.Speed > 24f && MaxLineSlopeAhead(b.Speed) > 0.03f; // slalom / dodge coming up
            float longitudinal = required > safeSteer * 0.85f || b.Danger > 0.62f || weaveAhead ? -0.8f : 0f;

            LastTargetLateral = lateral;
            LastTargetLongitudinal = longitudinal;
            PlacePlayers(sim, lateral, longitudinal, dt);
        }

        float PredictiveSteer(in BoardState b, float maxYawRate) =>
            _run.Road.PredictiveSteerHint(b, maxYawRate, ResponseDelay > 0f ? ResponseDelay : _run.Tuning.CrewResponseDelay - 0.05f, ref _predictHint);

        float MaxLineSlopeAhead(float speed)
        {
            float from = _run.Distance, horizon = Math.Max(40f, speed * 3.5f);
            float max = 0f, prev = _run.Road.PlannedLateral(from);
            for (float s = 4f; s <= horizon; s += 4f)
            {
                float x = _run.Road.PlannedLateral(from + s);
                max = Math.Max(max, Math.Abs(x - prev) / 4f);
                prev = x;
            }
            return max;
        }

        float RequiredSteerAhead(float speed, float maxYawRate)
        {
            float from = _run.Distance, horizon = Math.Max(40f, speed * 3.5f);
            float maxCurvature = 0f;
            _run.Road.Sample(from, out _, out float prevYaw, out _);
            for (float s = 4f; s <= horizon; s += 4f)
            {
                _run.Road.Sample(from + s, out _, out float yaw, out _);
                maxCurvature = Math.Max(maxCurvature, Math.Abs(SimMath.WrapAngle(yaw - prevYaw)) / 4f);
                prevYaw = yaw;
            }
            return speed * maxCurvature / Math.Max(maxYawRate, 1e-3f);
        }

        /// <summary>
        /// Players form a block of up to 3 columns centred on the target lateral position; the block slides to
        /// the tail to brake (compact rows) or spreads along the deck when cruising.
        /// </summary>
        static void PlacePlayers(BoardSimulation sim, float lateral, float longitudinal, float dt)
        {
            BoardTuningData t = sim.Tuning;
            int n = sim.ActivePlayerCount;
            float maxStep = t.playerMoveSpeed * dt;
            float x = lateral * t.HalfWidth;
            // near the edge a single column (so no one is clamped at the edge and the average stays exact)
            int columns = Math.Abs(x) > 0.3f ? 1 : Math.Min(3, n);
            int rows = (n + columns - 1) / columns;
            float rowSpacing = columns == 1 ? 0.72f : (longitudinal < -0.1f ? 0.75f : 1.4f);
            float zCenter = longitudinal * (t.HalfLength - 0.9f);
            float maxX = t.HalfWidth - t.playerRadius - 0.05f, maxZ = t.HalfLength - t.playerRadius - 0.1f;
            for (int i = 0; i < n; i++)
            {
                PlayerSim p = sim.Players[i];
                if (!p.IsOnBoard) continue;
                int col = i % columns, row = i / columns;
                float cx = columns == 1 ? 0f : (col - (columns - 1) * 0.5f) * 0.6f;
                float rz = (row - (rows - 1) * 0.5f) * rowSpacing;
                var target = new Vector2(SimMath.Clamp(x + cx, -maxX, maxX), SimMath.Clamp(zCenter + rz, -maxZ, maxZ));
                Vector2 current = p.Pinned ? p.PinnedPosition : p.LocalPosition;
                Vector2 d = target - current;
                float len = d.Length();
                p.Pin(len <= maxStep ? target : current + d / len * maxStep);
            }
        }
    }
}
