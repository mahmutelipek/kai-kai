using System;
using System.Collections.Generic;
using System.Numerics;

namespace Game.Simulation
{
    public struct RunStepEvents
    {
        public SimStepEvents Sim;
        public int LightHits;
        public int Bumps;
        public int HeavyHits;
        public int WallScrapes;
        public bool Crashed;
        public bool Respawned;
        public int PlayersThrownOff;
    }

    /// <summary>
    /// One authoritative run: endless road + obstacles + difficulty + board/players. Engine-independent;
    /// Unity hosts it in FixedUpdate, headless tests step it directly, the network host will own it in M5.
    /// </summary>
    public sealed class RunSimulation
    {
        const float RespawnBackOff = 10f;
        const float WallMargin = 0.3f;

        public readonly BoardTuningData Tuning;
        public readonly RoadModel Road = new RoadModel();
        public readonly ObstacleField Obstacles = new ObstacleField();
        public readonly DifficultyManager Difficulty;
        public readonly RoadGenerator Generator;
        public readonly BoardSimulation Board;
        /// <summary>Impacts resolved in the last step (views, audio, score).</summary>
        public readonly List<ImpactEvent> LastImpacts = new List<ImpactEvent>(8);

        readonly List<ImpactEvent> _hits = new List<ImpactEvent>(8);
        IList<FixedChunkSpec> _fixedTrack;
        int _projectionHint;
        float _wallCooldown;

        public RoadProjection Projection { get; private set; }
        /// <summary>Distance along the road of the board (global).</summary>
        public float Distance { get; private set; }
        /// <summary>Furthest distance reached this run.</summary>
        public float MaxDistance { get; private set; }
        public float DifficultyLevel { get; private set; }
        public int Crashes { get; private set; }
        public int Seed => Generator.Seed;

        public RunSimulation(BoardTuningData tuning, int seed, int players, IList<FixedChunkSpec> fixedTrack = null)
        {
            Tuning = tuning;
            Difficulty = new DifficultyManager(tuning);
            Generator = new RoadGenerator(tuning, Road, Obstacles, Difficulty);
            Board = new BoardSimulation(tuning, Road, players);
            Reset(seed, fixedTrack);
        }

        /// <summary>New run: new road from the seed, board at the start.</summary>
        public void Reset(int seed, IList<FixedChunkSpec> fixedTrack = null)
        {
            _fixedTrack = fixedTrack;
            Generator.Reset(seed, fixedTrack);
            Generator.Update(0f);
            _projectionHint = 0;
            Distance = MaxDistance = 0f;
            Crashes = 0;
            Road.Sample(0.5f, out Vector3 p, out float yaw, out _);
            Board.Restart(p, yaw);
            Projection = Road.Project(p, ref _projectionHint);
        }

        /// <summary>
        /// Live tuning: field edits are seen immediately (shared object). If the whole object is replaced
        /// (reset to defaults) the board uses the new one at once; road generation keeps the object it was built with.
        /// </summary>
        public void SetTuning(BoardTuningData tuning)
        {
            Board.Tuning = tuning;
        }

        public RunStepEvents Step(float dt, IReadOnlyList<PlayerInputState> inputs)
        {
            var ev = new RunStepEvents();
            LastImpacts.Clear();
            if (!(dt > 0f)) return ev;

            Generator.Update(Distance);
            DifficultyLevel = Difficulty.At(Distance);
            Board.Board.SpeedCapMultiplier = _fixedTrack == null ? Difficulty.SpeedCapMultiplier(DifficultyLevel) : 1f;
            Obstacles.Step(dt, Road, Distance, Board.Board.State.Speed);

            ev.Sim = Board.Step(dt, inputs);
            if (ev.Sim.Board.Crashed) { ev.Crashed = true; Crashes++; }

            ref BoardState s = ref Board.Board.State;
            Projection = Road.Project(s.Position, ref _projectionHint);
            if (Projection.Valid) Distance = Projection.Along;
            MaxDistance = Math.Max(MaxDistance, Distance);

            ResolveObstacles(ref ev);
            ResolveWalls(dt, ref ev);

            if (Board.RestartDue)
            {
                Respawn(Distance - RespawnBackOff);
                ev.Respawned = true;
            }
            return ev;
        }

        void ResolveObstacles(ref RunStepEvents ev)
        {
            BoardTuningData t = Tuning;
            if (Obstacles.Collide(Board.Board.State, t, Distance, _hits) == 0) return;
            for (int h = 0; h < _hits.Count; h++)
            {
                ImpactEvent hit = _hits[h];
                ref Obstacle o = ref Obstacles.Items[hit.Slot];
                ref BoardState s = ref Board.Board.State;
                if (s.Crashed) break;
                Vector2 velocity = SimMath.HeadingToDirection(s.TravelYaw) * s.Speed;

                if (hit.Severity == ImpactSeverity.Light)
                {
                    o.Knocked = true;
                    o.KnockTime = 0f;
                    o.KnockVelocity = new Vector3(velocity.X * 1.1f - hit.Normal.X * 2f, 3.5f, velocity.Y * 1.1f - hit.Normal.Y * 2f);
                    Board.Board.ApplyImpact(t.lightImpactSpeedLoss, 0f);
                    ev.LightHits++;
                }
                else if (hit.Severity == ImpactSeverity.Bump)
                {
                    o.HitCooldown = 1.5f;
                    Board.Board.ApplyImpact(t.potholeSpeedLoss, 0f);
                    Board.StaggerAll(t.staggerTime);
                    ev.Bumps++;
                }
                else if (hit.ClosingSpeed > t.crashImpactSpeed)
                {
                    hit.Severity = ImpactSeverity.Crash;
                    Board.CrashNow();
                    ev.Crashed = true;
                    Crashes++;
                }
                else
                {
                    o.HitCooldown = 0.6f;
                    float into = Vector2.Dot(velocity, hit.Normal);
                    if (into < 0f) velocity -= hit.Normal * into * 1.3f; // remove (and slightly bounce) the closing velocity
                    velocity *= 1f - t.heavyImpactSpeedLoss;
                    Vector2 forward = SimMath.HeadingToDirection(s.Yaw);
                    float side = forward.X * hit.Normal.Y - forward.Y * hit.Normal.X;
                    Board.Board.Deflect(hit.Normal * (hit.Depth + 0.02f), velocity, -MathF.Sign(side) * 0.6f);

                    Vector2 toObstacle = -hit.Normal;
                    Vector2 right = new Vector2(forward.Y, -forward.X);
                    ev.PlayersThrownOff += Board.ApplyHeavyImpact(new Vector2(Vector2.Dot(toObstacle, right), Vector2.Dot(toObstacle, forward)));
                    ev.HeavyHits++;
                }
                LastImpacts.Add(hit);
            }
        }

        void ResolveWalls(float dt, ref RunStepEvents ev)
        {
            _wallCooldown = Math.Max(0f, _wallCooldown - dt);
            ref BoardState s = ref Board.Board.State;
            RoadProjection p = Projection;
            if (!p.Valid || s.Crashed) return;
            float sign = p.Lateral >= 0f ? 1f : -1f;
            if ((sign > 0f ? p.RightEdge : p.LeftEdge) != EdgeKind.Wall) return;

            float rel = SimMath.WrapAngle(s.Yaw - p.Yaw);
            float extent = Tuning.HalfWidth * MathF.Abs(MathF.Cos(rel)) + Tuning.HalfLength * MathF.Abs(MathF.Sin(rel));
            float over = MathF.Abs(p.Lateral) + extent - (p.HalfWidth + WallMargin);
            if (over <= 0f) return;

            Vector3 right3 = SimMath.Right3(p.Yaw);
            var outward = new Vector2(right3.X * sign, right3.Z * sign);
            Vector2 velocity = SimMath.HeadingToDirection(s.TravelYaw) * s.Speed;
            float into = Vector2.Dot(velocity, outward);
            if (into > Tuning.wallCrashLateralSpeed)
            {
                Board.CrashNow();
                ev.Crashed = true;
                Crashes++;
                return;
            }
            if (into > 0f) velocity -= outward * into * 1.2f;
            velocity *= 1f - Tuning.wallScrapeSpeedLoss * SimMath.Clamp(into / 5f, 0.1f, 1f);
            Board.Board.Deflect(-outward * over, velocity, 0f);
            if (_wallCooldown <= 0f)
            {
                _wallCooldown = 0.4f;
                if (into > 2f) Board.StaggerAll(Tuning.staggerTime * 0.7f);
                LastImpacts.Add(new ImpactEvent { Slot = -1, Severity = ImpactSeverity.Heavy, Normal = -outward, Depth = over, ClosingSpeed = into, IsWall = true });
                ev.WallScrapes++;
            }
        }

        /// <summary>Puts the board back on the planned (free) line a little before <paramref name="along"/>.</summary>
        public void Respawn(float along)
        {
            along = SimMath.Clamp(along, Road.StartAlong + 1f, Road.EndAlong - 1f);
            float lateral = Road.PlannedLateral(along);
            Vector3 p = Road.WorldPoint(along, lateral, out float yaw);
            // clear knockable clutter right in front of the respawn point
            for (int i = 0; i < ObstacleField.Capacity; i++)
            {
                ref Obstacle o = ref Obstacles.Items[i];
                if (o.Active && !o.Knocked && ObstacleCatalog.IsKnockable(o.Kind) && o.Along >= along - 5f && o.Along <= along + 30f)
                    Obstacles.Despawn(i); // the owning chunk sees the slot as no longer its own (generation check)
            }
            float resumeSpeed = Board.Board.State.TargetSpeed * Tuning.respawnSpeedFraction;
            Board.Restart(p, yaw, resumeSpeed);
            Projection = Road.Project(p, ref _projectionHint);
            Distance = Projection.Along;
        }
    }
}
