using System;
using System.Collections.Generic;
using Game.Simulation;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Hosts the authoritative RunSimulation (road, obstacles, board, players) in FixedUpdate and applies the
    /// board pose to a kinematic Rigidbody. Never decides steering itself; collisions come from the simulation.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class BoardController : MonoBehaviour
    {
        /// <summary>Profiler marker around the simulation step (skill: tools-unity-profiling).</summary>
        static readonly Unity.Profiling.ProfilerMarker StepMarker = new Unity.Profiling.ProfilerMarker("Downhill.RunSimulation.Step");

        public const float SimulationRate = 60f;

        BoardTuning _tuning;
        Rigidbody _rb;
        IPlayerInputProvider _inputProvider;
        readonly PlayerInputState[] _inputs = new PlayerInputState[BoardSimulation.MaxPlayers];

        public RunSimulation Run { get; private set; }
        public BoardSimulation Simulation => Run?.Board;
        public BoardTuning Tuning => _tuning;
        public BoardState State => Run.Board.Board.State;
        public RunStepEvents LastEvents { get; private set; }

        public event Action Crashed;
        /// <summary>Strength 0..1 for camera shake / audio.</summary>
        public event Action<float> Impact;
        /// <summary>Landing speed in m/s.</summary>
        public event Action<float> Landed;
        public event Action Respawned;
        /// <summary>Raised after every simulation step with that step's events (HUD popups, audio later).</summary>
        public event Action<RunStepEvents> Stepped;

        public static BoardController Create(BoardTuning tuning, int seed, int playerCount, IList<FixedChunkSpec> fixedTrack)
        {
            var go = new GameObject("Board");
            var board = go.AddComponent<BoardController>();
            board._tuning = tuning;
            board.Run = new RunSimulation(tuning.data, seed, playerCount, fixedTrack);
            board._rb = go.GetComponent<Rigidbody>();
            board._rb.isKinematic = true;
            board._rb.interpolation = RigidbodyInterpolation.Interpolate;
            board.Teleport();
            return board;
        }

        public void SetInputProvider(IPlayerInputProvider provider) => _inputProvider = provider;

        void FixedUpdate()
        {
            if (Run == null) return;
            // live tuning: the panel edits the ScriptableObject's data object that the simulation references
            if (!ReferenceEquals(Run.Board.Tuning, _tuning.data)) Run.SetTuning(_tuning.data);

            for (int i = 0; i < _inputs.Length; i++) _inputs[i] = PlayerInputState.None;
            _inputProvider?.CollectInputs(Run.Board, _inputs, Time.fixedDeltaTime);

            RunStepEvents ev;
            using (StepMarker.Auto()) ev = Run.Step(Time.fixedDeltaTime, _inputs);
            LastEvents = ev;

            if (ev.Respawned)
            {
                Teleport();
                Respawned?.Invoke();
            }
            else ApplyPose();

            if (ev.Crashed) { Impact?.Invoke(1f); Crashed?.Invoke(); }
            else if (ev.HeavyHits > 0 || ev.WallScrapes > 0) Impact?.Invoke(0.6f);
            else if (ev.LightHits > 0 || ev.Bumps > 0) Impact?.Invoke(0.25f);
            if (ev.Sim.Board.Landed) Landed?.Invoke(ev.Sim.Board.LandingSpeed);
            Stepped?.Invoke(ev);
        }

        void ApplyPose()
        {
            BoardState s = State;
            _rb.MovePosition(s.Position.ToUnity());
            _rb.MoveRotation(SimConvert.PoseRotation(s.Yaw, s.Pitch, s.Roll));
        }

        /// <summary>Snaps the transform to the simulation (no interpolation smear after restarts).</summary>
        void Teleport()
        {
            BoardState s = State;
            Vector3 p = s.Position.ToUnity();
            Quaternion r = SimConvert.PoseRotation(s.Yaw, s.Pitch, s.Roll);
            transform.SetPositionAndRotation(p, r);
            _rb.position = p;
            _rb.rotation = r;
        }

        /// <summary>New run (new road from the seed).</summary>
        public void RestartRun(int seed, IList<FixedChunkSpec> fixedTrack)
        {
            Run.Reset(seed, fixedTrack);
            Teleport();
            Respawned?.Invoke();
        }

        /// <summary>Test hook: put the board on the road at a distance (planned lateral) with a speed.</summary>
        public void PlaceOnRoad(float along, float speed)
        {
            Run.Respawn(along);
            Run.Board.Board.State.Speed = speed;
            Teleport();
        }
    }
}
