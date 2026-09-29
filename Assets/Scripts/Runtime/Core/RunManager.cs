using System.Collections.Generic;
using Game.Simulation;
using UnityEngine;

namespace Game
{
    public enum RoadMode
    {
        /// <summary>Endless procedural road (Milestone 2).</summary>
        Endless = 0,
        /// <summary>The fixed Milestone 1 test track.</summary>
        TestTrack = 1,
    }

    /// <summary>
    /// Run flow: restart (R) with a new seed, session best distance, snapping the camera after respawns.
    /// Crash respawns themselves happen inside the RunSimulation.
    /// </summary>
    public sealed class RunManager : MonoBehaviour
    {
        BoardController _board;
        CameraController _camera;
        RoadMode _mode;
        int _fixedSeed;

        public float Distance => _board.Run.Distance;
        public float LateralOffset => _board.Run.Projection.Lateral;
        public float BestDistance { get; private set; }
        public int Crashes => _board.Run.Crashes;
        public int Seed => _board.Run.Seed;
        public RoadMode Mode => _mode;
        public string CurrentChunk => _board.Run.Projection.Chunk != null ? _board.Run.Projection.Chunk.Definition.Name : "-";

        public static IList<FixedChunkSpec> TrackFor(RoadMode mode) => mode == RoadMode.TestTrack ? TestRoadLayout.Build() : null;

        /// <summary><paramref name="fixedSeed"/> 0 = random seed every run.</summary>
        public void Initialize(BoardController board, CameraController cameraController, RoadMode mode, int fixedSeed)
        {
            _board = board;
            _camera = cameraController;
            _mode = mode;
            _fixedSeed = fixedSeed;
            _board.Respawned += () => { if (_camera != null) _camera.Snap(); };
        }

        public static int NewSeed(int fixedSeed) => fixedSeed != 0 ? fixedSeed : Random.Range(1, int.MaxValue);

        void Update()
        {
            if (_board == null || _board.Run == null) return;
            BestDistance = Mathf.Max(BestDistance, _board.Run.MaxDistance);
            // the fixed test track ends: start over at the top
            if (_mode == RoadMode.TestTrack && Distance > TestRoadLayout.Length - 40f) RestartRun();
        }

        /// <summary>Switch between the endless road and the M1 test track (starts a new run).</summary>
        public void ToggleMode()
        {
            _mode = _mode == RoadMode.Endless ? RoadMode.TestTrack : RoadMode.Endless;
            RestartRun();
        }

        public void RestartRun()
        {
            _board.RestartRun(NewSeed(_fixedSeed), TrackFor(_mode));
        }
    }
}
