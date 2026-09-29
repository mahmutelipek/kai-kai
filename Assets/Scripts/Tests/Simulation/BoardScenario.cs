using System;
using System.Collections.Generic;
using System.Numerics;
using Game.Simulation;

namespace Game.Tests
{
    /// <summary>One recorded simulation run (all angles in degrees for readability).</summary>
    public sealed class ScenarioTrace
    {
        public readonly List<float> Time = new List<float>();
        public readonly List<float> YawRateDeg = new List<float>();
        public readonly List<float> YawDeg = new List<float>();
        public readonly List<float> Speed = new List<float>();
        public readonly List<float> Steering = new List<float>();
        public readonly List<float> Danger = new List<float>();
        public readonly List<float> RollDeg = new List<float>();
        public readonly List<float> Lateral = new List<float>();
        public readonly List<float> Longitudinal = new List<float>();
        public float WobbleStartTime = -1f;
        public float CrashTime = -1f;
        public float MaxDanger;
        public float MinPlayerSeparation = float.PositiveInfinity;
        public float MinPlayerHeight = float.PositiveInfinity;
        public int Falls;
        public int Respawns;
        public int Staggers;
        public bool AllFinite = true;

        public bool Crashed => CrashTime >= 0f;

        /// <summary>Mean of a series over [from, to] seconds.</summary>
        public float Mean(List<float> series, float from, float to)
        {
            double sum = 0; int n = 0;
            for (int i = 0; i < Time.Count; i++)
                if (Time[i] >= from && Time[i] <= to) { sum += series[i]; n++; }
            return n == 0 ? 0f : (float)(sum / n);
        }

        public float MaxAbs(List<float> series, float from, float to)
        {
            float m = 0f;
            for (int i = 0; i < Time.Count; i++)
                if (Time[i] >= from && Time[i] <= to) m = Math.Max(m, Math.Abs(series[i]));
            return m;
        }

        public float StdDev(List<float> series, float from, float to)
        {
            float mean = Mean(series, from, to);
            double sum = 0; int n = 0;
            for (int i = 0; i < Time.Count; i++)
                if (Time[i] >= from && Time[i] <= to) { sum += (series[i] - mean) * (series[i] - mean); n++; }
            return n == 0 ? 0f : (float)Math.Sqrt(sum / n);
        }

        /// <summary>First time |series| reaches fraction*|target|.</summary>
        public float TimeToReach(List<float> series, float target, float fraction)
        {
            for (int i = 0; i < Time.Count; i++)
                if (Math.Abs(series[i]) >= Math.Abs(target) * fraction && Math.Sign(series[i]) == Math.Sign(target)) return Time[i];
            return -1f;
        }

        /// <summary>Number of sign changes of the derivative after 'from' (counts oscillation turning points).</summary>
        public int TurningPoints(List<float> series, float from, float epsilon)
        {
            int count = 0; int lastSign = 0;
            for (int i = 1; i < Time.Count; i++)
            {
                if (Time[i] < from) continue;
                float d = series[i] - series[i - 1];
                if (Math.Abs(d) < epsilon) continue;
                int sign = Math.Sign(d);
                if (lastSign != 0 && sign != lastSign) count++;
                lastSign = sign;
            }
            return count;
        }
    }

    /// <summary>Headless scenario helpers shared by NUnit tests and the headless report tool.</summary>
    public static class BoardScenario
    {
        public const float Dt = 1f / 60f;
        public const float EdgeN = 0.85f;

        /// <summary>Tuning copy with the cruise speed held at <paramref name="speed"/> (no speed ramp).</summary>
        public static BoardTuningData TuningAtSpeed(float speed, BoardTuningData baseTuning = null)
        {
            BoardTuningData t = (baseTuning ?? new BoardTuningData()).Clone();
            t.startSpeed = speed;
            t.speedRampPerSecond = 0f;
            return t;
        }

        public static BoardSimulation Create(int players, BoardTuningData tuning, IGroundProvider ground = null)
        {
            return new BoardSimulation(tuning, ground ?? new SlopedPlaneGround(0.05f), players);
        }

        /// <summary>
        /// Layout string, one char per player: 'L' left edge, 'R' right edge, 'C' center,
        /// 'F' front center, 'B' back (rear) center, 'l'/'r' half-way left/right.
        /// Players sharing a column are spread along the deck.
        /// </summary>
        public static Vector2[] Layout(string layout, BoardTuningData t)
        {
            var result = new Vector2[layout.Length];
            var columns = new Dictionary<char, List<int>>();
            for (int i = 0; i < layout.Length; i++)
            {
                if (!columns.TryGetValue(layout[i], out var list)) columns[layout[i]] = list = new List<int>();
                list.Add(i);
            }
            foreach (var kv in columns)
            {
                int n = kv.Value.Count;
                for (int k = 0; k < n; k++)
                {
                    float spread = n == 1 ? 0f : SimMath.Lerp(-0.8f, 0.8f, k / (float)(n - 1));
                    float nx, nz;
                    switch (kv.Key)
                    {
                        case 'L': nx = -EdgeN; nz = spread; break;
                        case 'R': nx = EdgeN; nz = spread; break;
                        case 'l': nx = -EdgeN * 0.5f; nz = spread; break;
                        case 'r': nx = EdgeN * 0.5f; nz = spread; break;
                        case 'F': nx = SpreadX(k, n); nz = 0.8f; break;
                        case 'B': nx = SpreadX(k, n); nz = -0.8f; break;
                        default: nx = 0f; nz = spread; break;
                    }
                    result[kv.Value[k]] = new Vector2(nx * t.HalfWidth, nz * t.HalfLength);
                }
            }
            return result;
        }

        static float SpreadX(int k, int n)
        {
            // symmetric spread across the width so rows at the nose/tail stay balanced left-right
            if (n == 1) return 0f;
            return SimMath.Lerp(-0.7f, 0.7f, k / (float)(n - 1));
        }

        public static void PinAll(BoardSimulation sim, Vector2[] positions)
        {
            for (int i = 0; i < positions.Length; i++) sim.Players[i].Pin(positions[i]);
        }

        /// <summary>Input that walks each player to its target spot and holds it there (a "real" player, not pinned).</summary>
        public static Func<int, PlayerInputState> HoldPositions(BoardSimulation sim, Vector2[] targets)
        {
            return i =>
            {
                Vector2 d = targets[i] - sim.Players[i].LocalPosition;
                float dist = d.Length();
                if (dist < 0.02f) return PlayerInputState.None;
                return new PlayerInputState(d / dist * SimMath.Clamp01(dist / 0.4f));
            };
        }

        public static ScenarioTrace Run(BoardSimulation sim, float seconds, Func<int, PlayerInputState> inputs = null,
                                        bool stopOnCrash = false, Action<BoardSimulation, SimStepEvents> onStep = null)
        {
            var trace = new ScenarioTrace();
            var inputBuffer = new PlayerInputState[BoardSimulation.MaxPlayers];
            int steps = (int)Math.Round(seconds / Dt);
            for (int step = 0; step < steps; step++)
            {
                for (int i = 0; i < sim.ActivePlayerCount; i++) inputBuffer[i] = inputs != null ? inputs(i) : PlayerInputState.None;
                SimStepEvents ev = sim.Step(Dt, inputBuffer);
                BoardState b = sim.Board.State;

                trace.Time.Add(sim.Time);
                trace.YawRateDeg.Add(b.YawRate * SimMath.Rad2Deg);
                trace.YawDeg.Add(b.Yaw * SimMath.Rad2Deg);
                trace.Speed.Add(b.Speed);
                trace.Steering.Add(b.Steering);
                trace.Danger.Add(b.Danger);
                trace.RollDeg.Add(b.Roll * SimMath.Rad2Deg);
                trace.Lateral.Add(b.Lateral);
                trace.Longitudinal.Add(b.Longitudinal);
                trace.MaxDanger = Math.Max(trace.MaxDanger, b.Danger);
                if (trace.WobbleStartTime < 0f && b.Wobble > 0f) trace.WobbleStartTime = sim.Time;
                if (ev.Board.Crashed && trace.CrashTime < 0f) trace.CrashTime = sim.Time;
                trace.Falls += ev.PlayersFell;
                trace.Respawns += ev.PlayersRespawned;
                trace.Staggers += ev.Staggers;

                trace.MinPlayerSeparation = Math.Min(trace.MinPlayerSeparation,
                    PlayerCrowdSolver.MinimumSeparation(sim.Players, sim.ActivePlayerCount, sim.Tuning));
                for (int i = 0; i < sim.ActivePlayerCount; i++)
                {
                    PlayerSim p = sim.Players[i];
                    if (!p.IsOnBoard) continue;
                    trace.MinPlayerHeight = Math.Min(trace.MinPlayerHeight, p.Height);
                    if (!SimMath.IsFinite(p.LocalPosition) || !SimMath.IsFinite(p.Height)) trace.AllFinite = false;
                }
                if (!SimMath.IsFinite(b.Position) || !SimMath.IsFinite(b.Yaw) || !SimMath.IsFinite(b.Roll) ||
                    !SimMath.IsFinite(b.Speed) || !SimMath.IsFinite(b.YawRate)) trace.AllFinite = false;

                onStep?.Invoke(sim, ev);
                if (stopOnCrash && b.Crashed) break;
            }
            return trace;
        }

        /// <summary>Pinned-layout run at a constant cruise speed. The core measurement used by most acceptance tests.</summary>
        public static ScenarioTrace RunPinned(string layout, float speed, float seconds, BoardTuningData baseTuning = null, bool stopOnCrash = true)
        {
            BoardTuningData t = TuningAtSpeed(speed, baseTuning);
            BoardSimulation sim = Create(layout.Length, t);
            PinAll(sim, Layout(layout, t));
            return Run(sim, seconds, null, stopOnCrash);
        }

        /// <summary>Same as RunPinned but players walk to and hold their spots with real input.</summary>
        public static ScenarioTrace RunHeld(string layout, float speed, float seconds, BoardTuningData baseTuning = null, bool stopOnCrash = true)
        {
            BoardTuningData t = TuningAtSpeed(speed, baseTuning);
            BoardSimulation sim = Create(layout.Length, t);
            Vector2[] targets = Layout(layout, t);
            for (int i = 0; i < targets.Length; i++) sim.Players[i].PlaceAt(targets[i]);
            return Run(sim, seconds, HoldPositions(sim, targets), stopOnCrash);
        }

        public static string Mirror(string layout)
        {
            char[] c = layout.ToCharArray();
            for (int i = 0; i < c.Length; i++)
            {
                if (c[i] == 'L') c[i] = 'R';
                else if (c[i] == 'R') c[i] = 'L';
                else if (c[i] == 'l') c[i] = 'r';
                else if (c[i] == 'r') c[i] = 'l';
            }
            return new string(c);
        }
    }
}
