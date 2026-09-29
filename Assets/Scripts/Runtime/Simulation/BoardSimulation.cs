using System;
using System.Collections.Generic;
using System.Numerics;

namespace Game.Simulation
{
    public struct SimStepEvents
    {
        public BoardStepEvents Board;
        public int PlayersFell;
        public int PlayersRespawned;
        public int Staggers;
    }

    /// <summary>
    /// The one authoritative simulation step for board + players. Engine-independent: Unity drives it
    /// from FixedUpdate, headless tests drive it directly. Reads only PlayerInputState per player.
    /// </summary>
    public sealed class BoardSimulation
    {
        public const int MaxPlayers = 6;
        public const int MinPlayers = 1;

        public BoardTuningData Tuning;
        public IGroundProvider Ground;
        public readonly BoardPhysicsSim Board = new BoardPhysicsSim();
        public readonly List<PlayerSim> Players = new List<PlayerSim>(MaxPlayers);
        public WeightResult LastWeight;
        public float Time;

        readonly WeightSample[] _samples = new WeightSample[MaxPlayers];

        public int ActivePlayerCount { get; private set; }

        /// <summary>True once a crashed board has waited crashRestartDelay; call Restart to continue.</summary>
        public bool RestartDue => Board.State.Crashed && Board.State.CrashTimer >= Tuning.crashRestartDelay;

        public BoardSimulation(BoardTuningData tuning, IGroundProvider ground, int playerCount = MaxPlayers)
        {
            Tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
            Ground = ground ?? throw new ArgumentNullException(nameof(ground));
            for (int i = 0; i < MaxPlayers; i++) Players.Add(new PlayerSim(i));
            SetActivePlayerCount(playerCount);
            Board.Reset(Vector3.Zero, 0f, tuning.startSpeed);
            PlaceInFormation();
        }

        public void SetActivePlayerCount(int count)
        {
            count = Math.Max(MinPlayers, Math.Min(MaxPlayers, count));
            int previous = ActivePlayerCount;
            ActivePlayerCount = count;
            for (int i = previous; i < count; i++)
            {
                Players[i].Unpin();
                Players[i].PlaceAt(FindFreeSpot(i));
            }
        }

        /// <summary>Resets board pose and speed ramp and puts every active player back on the deck.</summary>
        public void Restart(Vector3 position, float yaw)
        {
            Board.Reset(position, yaw, Tuning.startSpeed);
            PlaceInFormation();
        }

        /// <summary>Default start formation: two columns, spread along the deck.</summary>
        public void PlaceInFormation()
        {
            float x = Tuning.HalfWidth * 0.4f;
            for (int i = 0; i < ActivePlayerCount; i++)
            {
                if (Players[i].Pinned) { Players[i].PlaceAt(Players[i].PinnedPosition); continue; }
                int row = i / 2;
                int rows = (ActivePlayerCount + 1) / 2;
                float z = rows <= 1 ? 0f : SimMath.Lerp(Tuning.HalfLength * 0.55f, -Tuning.HalfLength * 0.55f, row / (float)(rows - 1));
                float side = (i % 2 == 0) ? -1f : 1f;
                if (ActivePlayerCount % 2 == 1 && i == ActivePlayerCount - 1) side = 0f;
                Players[i].PlaceAt(new Vector2(side * x, z));
            }
        }

        public SimStepEvents Step(float dt, IReadOnlyList<PlayerInputState> inputs)
        {
            var events = new SimStepEvents();
            if (!(dt > 0f)) return events;
            Time += dt;
            BoardTuningData t = Tuning;
            ref BoardState board = ref Board.State;

            // 1) players move in board space, feeling last step's board motion
            var ctx = new PlayerBoardContext
            {
                Wobble = board.Wobble,
                Roll = board.Roll,
                BoardAcceleration = board.Acceleration,
            };
            for (int i = 0; i < ActivePlayerCount; i++)
            {
                PlayerInputState input = inputs != null && i < inputs.Count ? inputs[i] : PlayerInputState.None;
                Players[i].Step(dt, input, ctx, t);
            }

            // 2) bumps / no interpenetration
            events.Staggers = PlayerCrowdSolver.Resolve(Players, ActivePlayerCount, t);

            // 3) falling off the deck edge
            for (int i = 0; i < ActivePlayerCount; i++)
            {
                PlayerSim p = Players[i];
                if (p.IsOnBoard && !p.Pinned && p.IsOverEdge(t))
                {
                    p.FallOff(t.respawnDelay);
                    events.PlayersFell++;
                }
            }

            // 4) respawn fallen players safely (not while the board is crashed)
            if (!board.Crashed)
            {
                for (int i = 0; i < ActivePlayerCount; i++)
                {
                    PlayerSim p = Players[i];
                    if (p.State != PlayerSimState.Fallen || p.FallTimer > 0f) continue;
                    p.PlaceAt(FindFreeSpot(i), 0.5f);
                    p.Stagger(0.2f);
                    p.RespawnCount++;
                    events.PlayersRespawned++;
                }
            }

            // 5) weight model -> board physics
            LastWeight = ComputeWeight();
            events.Board = Board.Step(dt, LastWeight, t, Ground);

            // 6) reactions to board events
            if (events.Board.Crashed)
            {
                for (int i = 0; i < ActivePlayerCount; i++)
                {
                    if (!Players[i].IsOnBoard) continue;
                    Players[i].FallOff(float.MaxValue); // everyone is thrown off; held until the board restarts
                    events.PlayersFell++;
                }
            }
            else if (events.Board.HardLanding)
            {
                float stagger = t.staggerTime * t.landingStaggerFactor *
                                SimMath.Clamp(events.Board.LandingSpeed / Math.Max(t.hardLandingSpeed, 1e-3f), 1f, 2.5f);
                for (int i = 0; i < ActivePlayerCount; i++) Players[i].Stagger(stagger);
            }
            return events;
        }

        /// <summary>Crash right now (worst-case impact): flips the board and throws everybody off.</summary>
        public void CrashNow()
        {
            if (Board.State.Crashed) return;
            Board.ForceCrash();
            for (int i = 0; i < ActivePlayerCount; i++)
                if (Players[i].IsOnBoard) Players[i].FallOff(float.MaxValue);
        }

        /// <summary>
        /// Heavy hit: everybody staggers; players standing toward the impact (beyond heavyFallThreshold of the
        /// deck extent in that direction) are thrown off. <paramref name="impactDirLocal"/> points from the deck
        /// centre toward the obstacle in board space (X right, Y nose). Returns how many fell.
        /// </summary>
        public int ApplyHeavyImpact(Vector2 impactDirLocal)
        {
            BoardTuningData t = Tuning;
            float len = impactDirLocal.Length();
            Vector2 dir = len > 1e-4f ? impactDirLocal / len : new Vector2(0f, 1f);
            int fell = 0;
            for (int i = 0; i < ActivePlayerCount; i++)
            {
                PlayerSim p = Players[i];
                if (!p.IsOnBoard) continue;
                var normalized = new Vector2(p.LocalPosition.X / t.HalfWidth, p.LocalPosition.Y / t.HalfLength);
                if (!p.Pinned && Vector2.Dot(normalized, dir) > t.heavyFallThreshold)
                {
                    p.FallOff(t.respawnDelay);
                    fell++;
                }
                else p.Stagger(t.heavyStaggerTime);
            }
            return fell;
        }

        public void StaggerAll(float time)
        {
            for (int i = 0; i < ActivePlayerCount; i++) Players[i].Stagger(time);
        }

        public WeightResult ComputeWeight()
        {
            for (int i = 0; i < ActivePlayerCount; i++)
            {
                PlayerSim p = Players[i];
                float contact = !p.IsOnBoard ? 0f : (p.IsAirborne ? Tuning.airborneWeightFactor : 1f);
                _samples[i] = new WeightSample { LocalPosition = p.LocalPosition, Weight = p.BodyWeight * contact };
            }
            return BoardWeightSystem.Compute(_samples, ActivePlayerCount, Tuning);
        }

        /// <summary>Free deck spot for player <paramref name="self"/>: center first, then outward rings.</summary>
        public Vector2 FindFreeSpot(int self)
        {
            BoardTuningData t = Tuning;
            float r = t.playerRadius;
            float maxX = Math.Max(0f, t.HalfWidth - r - 0.1f);
            float maxZ = Math.Max(0f, t.HalfLength - r - 0.1f);
            float clearance = 2f * r + 0.05f;

            Vector2 best = Vector2.Zero;
            float bestScore = float.NegativeInfinity;
            for (int ring = 0; ring <= 6; ring++)
            {
                int steps = ring == 0 ? 1 : 8 * ring;
                for (int k = 0; k < steps; k++)
                {
                    Vector2 c = Vector2.Zero;
                    if (ring > 0)
                    {
                        float a = k / (float)steps * SimMath.TwoPi;
                        c = new Vector2(MathF.Sin(a) * ring * 0.35f, MathF.Cos(a) * ring * 0.55f);
                    }
                    if (Math.Abs(c.X) > maxX || Math.Abs(c.Y) > maxZ) continue;
                    float nearest = NearestOtherDistance(c, self);
                    if (nearest >= clearance) return c;
                    if (nearest > bestScore) { bestScore = nearest; best = c; }
                }
            }
            return best;
        }

        float NearestOtherDistance(Vector2 point, int self)
        {
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < ActivePlayerCount; i++)
            {
                if (i == self || !Players[i].IsOnBoard) continue;
                nearest = Math.Min(nearest, Vector2.Distance(point, Players[i].LocalPosition));
            }
            return nearest;
        }
    }
}
