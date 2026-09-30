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
        HUDController _hud;
        bool _countdownEnabled;
        float _countdown = -1f;
        static readonly string[] CountdownTexts = { "GO!", "1", "2", "3" };

        /// <summary>Seconds of "3, 2, 1" before the board rolls; then "GO!".</summary>
        public const float CountdownSeconds = 3f;
        public bool CountingDown => _countdown > 0f;

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

        /// <summary>Enables the 3-2-1-GO start (scene play); PlayMode tests leave it off.</summary>
        public void EnableCountdown(HUDController hud, bool startNow = true)
        {
            _hud = hud;
            _countdownEnabled = true;
            if (startNow) StartCountdown();
        }

        void StartCountdown()
        {
            if (!_countdownEnabled) return;
            _countdown = CountdownSeconds;
            _board.Frozen = true;
        }

        void Update()
        {
            if (_board == null || _board.Run == null) return;
            if (_countdownEnabled && _countdown > -1f)
            {
                _countdown -= Time.deltaTime; // scaled: the countdown waits while the game is paused
                if (_countdown <= 0f && _board.Frozen) _board.Frozen = false;
                string text = _countdown > 0f ? CountdownTexts[Mathf.Clamp(Mathf.CeilToInt(_countdown), 1, 3)] : _countdown > -0.8f ? Game.Frontend.Loc.T(CountdownTexts[0]) : null;
                _hud?.SetCountdown(text);
            }
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

        public void RestartRun() => RestartRun(countdown: true);

        /// <summary>New run; <paramref name="countdown"/> false for the attract-mode ride behind the title.</summary>
        public void RestartRun(bool countdown)
        {
            _board.RestartRun(NewSeed(_fixedSeed), TrackFor(_mode));
            if (countdown) StartCountdown();
            else
            {
                _countdown = -1f;
                _board.Frozen = false;
                _hud?.SetCountdown(null);
            }
        }
    }
}
