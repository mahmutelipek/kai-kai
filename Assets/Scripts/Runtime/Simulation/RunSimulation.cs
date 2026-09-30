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
        public int Coins, Diamonds, NitroPickups, NearMisses;
        public bool NitroStarted;
        public bool Landed;
        public float Airtime;
        public bool CleanLanding;
        public bool SectionCleared;
        /// <summary>This step the run ended (no lives left).</summary>
        public bool RunEnded;
        /// <summary>M4.3 moves: crew ollie, carve boost (1 / 2 = long carve), entering a car's slipstream.</summary>
        public bool Ollie, PerfectOllie;
        public int CarveBoost;
        public bool SlipstreamStarted;
    }

    public enum RunState
    {
        Running,
        Ended,
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
        public readonly PickupField Pickups = new PickupField();
        public readonly ScoreManager Score;
        /// <summary>Optional: set by the host to persist best score / distance.</summary>
        public HighScoreManager HighScores;
        public readonly DifficultyManager Difficulty;
        public readonly RoadGenerator Generator;
        public readonly BoardSimulation Board;
        /// <summary>Impacts resolved in the last step (views, audio, score).</summary>
        public readonly List<ImpactEvent> LastImpacts = new List<ImpactEvent>(8);

        readonly List<ImpactEvent> _hits = new List<ImpactEvent>(8);
        readonly List<PickupEvent> _collected = new List<PickupEvent>(8);
        readonly List<int> _nearMisses = new List<int>(4);
        /// <summary>Pickups collected in the last step.</summary>
        public readonly List<PickupEvent> LastPickups = new List<PickupEvent>(8);
        /// <summary>Obstacle slots near-missed in the last step.</summary>
        public readonly List<int> LastNearMisses = new List<int>(4);
        float _airtime;
        bool _wasCrashed;
        float _scoredDistance;
        int _sectionSerial = -1;
        bool _sectionClean;
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
        public RunState State { get; private set; }
        public int NitroCharges { get; private set; }
        public const int MaxNitroCharges = 2;
        public int LivesLeft { get; private set; }
        /// <summary>0 = endless practice (unlimited respawns).</summary>
        public int LivesPerRun => (int)Tuning.livesPerRun;
        public bool NewBestScore { get; private set; }
        public bool NewBestDistance { get; private set; }
        /// <summary>0..1: how deep the board is in a car's slipstream (1 = full bonus).</summary>
        public float Slipstream { get; private set; }

        /// <summary>The crew's "JUMP!" call (human jumps, spotted hazards) that bots answer.</summary>
        public readonly CrewCall Crew = new CrewCall();

        /// <summary>
        /// Nearest low hazard an ollie clears (pothole, debris, cone) lying in the board's path: time until the board's
        /// nose reaches it at the current speed. Cars, barriers and crates are for steering (a crate needs a perfect ollie).
        /// </summary>
        public bool JumpHazardAhead(out float timeToContact, out ObstacleKind kind)
        {
            timeToContact = float.MaxValue;
            kind = default;
            BoardState b = Board.Board.State;
            if (!Projection.Valid || b.Speed < 3f) return false;
            BoardTuningData t = Tuning;
            float nose = Distance + t.HalfLength, lateral = Projection.Lateral;
            bool found = false;
            Obstacle[] items = Obstacles.Items;
            for (int i = 0; i < items.Length; i++)
            {
                ref Obstacle o = ref items[i];
                if (!o.Active || o.Knocked || o.Motion != ObstacleMotion.Static) continue;
                if (o.Kind != ObstacleKind.Pothole && o.Kind != ObstacleKind.BrokenPiece && o.Kind != ObstacleKind.Cone) continue;
                float gap = o.Along - o.HalfExtents.Y - nose;
                if (gap < 0f || gap > 40f) continue;
                if (Math.Abs(o.Lateral - lateral) > o.HalfExtents.X + t.HalfWidth) continue;
                float ttc = gap / b.Speed;
                if (ttc < timeToContact) { timeToContact = ttc; kind = o.Kind; found = true; }
            }
            return found;
        }
        float _draftTime, _draftLinger;
        int _draftSlot = -1, _draftGeneration = -1;

        public RunSimulation(BoardTuningData tuning, int seed, int players, IList<FixedChunkSpec> fixedTrack = null)
        {
            Tuning = tuning;
            Difficulty = new DifficultyManager(tuning);
            Score = new ScoreManager(tuning);
            Generator = new RoadGenerator(tuning, Road, Obstacles, Difficulty, Pickups);
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
            Score.Reset();
            State = RunState.Running;
            NitroCharges = 0;
            LivesLeft = LivesPerRun;
            NewBestScore = NewBestDistance = false;
            _airtime = 0f;
            _draftTime = _draftLinger = 0f;
            _draftSlot = _draftGeneration = -1;
            Slipstream = 0f;
            Crew.Reset();
            _wasCrashed = false;
            _scoredDistance = 0f;
            _sectionSerial = -1;
            _sectionClean = true;
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
            Score.SetTuning(tuning);
        }

        public RunStepEvents Step(float dt, IReadOnlyList<PlayerInputState> inputs)
        {
            var ev = new RunStepEvents();
            LastImpacts.Clear();
            LastPickups.Clear();
            LastNearMisses.Clear();
            if (!(dt > 0f) || State == RunState.Ended) return ev;

            // nitro: any player pressing the action button fires a stored charge
            if (NitroCharges > 0 && Board.Board.State.NitroTimer <= 0f && !Board.Board.State.Crashed && AnyAction(inputs))
            {
                NitroCharges--;
                Board.Board.StartNitro(Tuning.nitroDuration);
                ev.NitroStarted = true;
            }

            Generator.Update(Distance);
            DifficultyLevel = Difficulty.At(Distance);
            Board.Board.SpeedCapMultiplier = _fixedTrack == null ? Difficulty.SpeedCapMultiplier(DifficultyLevel) : 1f;
            Obstacles.Step(dt, Road, Distance, Board.Board.State.Speed);
            Pickups.Step(dt);
            UpdateSlipstream(dt, ref ev);

            ev.Sim = Board.Step(dt, inputs);

            ref BoardState s = ref Board.Board.State;
            Projection = Road.Project(s.Position, ref _projectionHint);
            if (Projection.Valid) Distance = Projection.Along;
            MaxDistance = Math.Max(MaxDistance, Distance);

            ResolveObstacles(ref ev);
            ResolveWalls(dt, ref ev);
            UpdateScoring(dt, ref ev);

            if (Board.RestartDue)
            {
                if (LivesPerRun > 0 && LivesLeft <= 0) EndRun(ref ev);
                else
                {
                    Respawn(Distance - RespawnBackOff);
                    ev.Respawned = true;
                }
            }
            return ev;
        }

        /// <summary>
        /// Slipstream: close behind a car driving the same way (within slipstreamDistance, lateral offset under
        /// slipstreamWidth) the board builds up draft and gets extra target speed. Lingers 1 s after leaving.
        /// </summary>
        void UpdateSlipstream(float dt, ref RunStepEvents ev)
        {
            ref BoardState b = ref Board.Board.State;
            int found = -1;
            if (!b.Crashed && b.Grounded && Projection.Valid)
            {
                for (int i = 0; i < ObstacleField.Capacity; i++)
                {
                    ref Obstacle o = ref Obstacles.Items[i];
                    if (!o.Active || o.Knocked || o.Motion != ObstacleMotion.SameDirection) continue;
                    float ahead = o.Along - Distance - o.HalfExtents.Y - Tuning.HalfLength;
                    if (ahead < 0.5f || ahead > Tuning.slipstreamDistance) continue;
                    if (Math.Abs(o.Lateral - Projection.Lateral) > Tuning.slipstreamWidth) continue;
                    found = i;
                    break;
                }
            }
            if (found >= 0)
            {
                bool sameCar = found == _draftSlot && Obstacles.Items[found].Generation == _draftGeneration;
                if (!sameCar) { _draftSlot = found; _draftGeneration = Obstacles.Items[found].Generation; _draftTime = 0f; }
                float before = _draftTime;
                _draftTime += dt;
                if (before < Tuning.slipstreamBuildTime && _draftTime >= Tuning.slipstreamBuildTime)
                {
                    ev.SlipstreamStarted = true;
                    Score.OnSlipstream();
                }
                _draftLinger = 1f;
            }
            else
            {
                _draftLinger = Math.Max(0f, _draftLinger - dt);
                if (_draftLinger <= 0f) _draftTime = 0f;
            }
            Slipstream = _draftTime >= Tuning.slipstreamBuildTime ? _draftLinger : SimMath.Clamp01(_draftTime / Math.Max(Tuning.slipstreamBuildTime, 1e-3f)) * 0.5f;
            b.ExternalSpeedBonus = _draftTime >= Tuning.slipstreamBuildTime ? Tuning.slipstreamBonus * _draftLinger : 0f;
        }

        static bool AnyAction(IReadOnlyList<PlayerInputState> inputs)
        {
            if (inputs == null) return false;
            for (int i = 0; i < inputs.Count; i++) if (inputs[i].Action) return true;
            return false;
        }

        void UpdateScoring(float dt, ref RunStepEvents ev)
        {
            BoardState s = Board.Board.State;

            // pickups
            Pickups.Collect(s, Tuning, Distance, _collected);
            for (int i = 0; i < _collected.Count; i++)
            {
                LastPickups.Add(_collected[i]);
                switch (_collected[i].Kind)
                {
                    case PickupKind.Coin: Score.OnCoin(); ev.Coins++; break;
                    case PickupKind.Diamond: Score.OnDiamond(); ev.Diamonds++; break;
                    default:
                        NitroCharges = Math.Min(MaxNitroCharges, NitroCharges + 1);
                        Score.OnNitroPickup();
                        ev.NitroPickups++;
                        break;
                }
            }

            // near misses
            Obstacles.TrackNearMisses(s, Tuning, Distance, _nearMisses);
            for (int i = 0; i < _nearMisses.Count; i++) { LastNearMisses.Add(_nearMisses[i]); Score.OnNearMiss(); ev.NearMisses++; }

            // moves
            if (ev.Sim.Ollie) { ev.Ollie = true; ev.PerfectOllie = ev.Sim.PerfectOllie; Score.OnOllie(ev.PerfectOllie); }
            else if (ev.Sim.PerfectOllie) { ev.PerfectOllie = true; Score.OnPerfectOllieUpgrade(); } // late riders joined in time
            if (ev.Sim.Board.CarveBoost > 0) { ev.CarveBoost = ev.Sim.Board.CarveBoost; Score.OnCarveBoost(ev.CarveBoost > 1); }

            // airtime and landings
            if (!s.Grounded && !s.Crashed) _airtime += dt;
            if (ev.Sim.Board.Landed)
            {
                bool clean = IsCleanLanding(s.Danger, ev.Sim.PlayersFell, s.Crashed, Tuning);
                ev.Landed = true;
                ev.Airtime = _airtime;
                ev.CleanLanding = clean && _airtime >= Tuning.minScoredAirtime;
                Score.OnLanding(_airtime, clean);
                _airtime = 0f;
            }
            if (s.Crashed) _airtime = 0f;

            // one place counts crashes, whatever caused them (tipping, obstacle, wall, falling off, external)
            if (s.Crashed && !_wasCrashed)
            {
                ev.Crashed = true;
                Crashes++;
                LivesLeft = Math.Max(0, LivesLeft - 1);
            }
            _wasCrashed = s.Crashed;

            // collisions and falls break the combo
            if (ev.Crashed) { Score.OnCrash(); _sectionClean = false; }
            else
            {
                if (ev.HeavyHits > 0) { Score.OnHeavyHit(); _sectionClean = false; }
                else if (ev.Sim.PlayersFell > 0) Score.OnPlayerFell();
                if (ev.LightHits > 0 || ev.Bumps > 0) Score.OnLightHit();
            }

            // surviving a hard section
            RoadChunk chunk = Projection.Chunk;
            if (chunk != null && chunk.Serial != _sectionSerial)
            {
                RoadChunk previous = FindChunk(_sectionSerial);
                if (previous != null && previous.Definition.IsIntense && _sectionClean && chunk.Serial > _sectionSerial)
                {
                    Score.OnSectionCleared();
                    ev.SectionCleared = true;
                }
                _sectionSerial = chunk.Serial;
                _sectionClean = true;
            }

            // distance: only new ground counts (respawning backwards does not score twice)
            float progressed = Math.Max(0f, MaxDistance - _scoredDistance);
            _scoredDistance = Math.Max(_scoredDistance, MaxDistance);
            Score.Tick(dt, progressed);
        }

        /// <summary>
        /// A landing is clean when the board comes down balanced (below the wobble zone), nobody is thrown off and it
        /// does not crash. A hard landing still staggers everyone but does not spoil it.
        /// </summary>
        public static bool IsCleanLanding(float danger, int playersFell, bool crashed, BoardTuningData t) =>
            !crashed && playersFell == 0 && danger < t.wobbleStartFraction;

        RoadChunk FindChunk(int serial)
        {
            for (int i = 0; i < Road.Chunks.Count; i++) if (Road.Chunks[i].Serial == serial) return Road.Chunks[i];
            return null;
        }

        void EndRun(ref RunStepEvents ev)
        {
            State = RunState.Ended;
            ev.RunEnded = true;
            if (HighScores != null)
            {
                HighScores.Submit(Score.Score, MaxDistance, out bool bestScore, out bool bestDistance);
                NewBestScore = bestScore;
                NewBestDistance = bestDistance;
            }
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
            Generator.Update(Math.Max(along, 0f)); // make sure the road exists there (teleports / test hooks)
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
            _wasCrashed = false;
            Projection = Road.Project(p, ref _projectionHint);
            Distance = Projection.Along;
        }
    }
}
