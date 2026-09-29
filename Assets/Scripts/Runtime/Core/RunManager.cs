using Game.Simulation;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Run flow for Milestone 1: tracks distance, respawns the board on the road after a crash,
    /// restarts at the top when the test road ends or on request.
    /// </summary>
    public sealed class RunManager : MonoBehaviour
    {
        const float RespawnBackOff = 6f;
        const float EndOfRoadMargin = 40f;

        BoardController _board;
        RoadPath _road;
        CameraController _camera;
        int _roadHint;

        public float Distance { get; private set; }
        public float RunDistanceStart { get; private set; }
        public float LateralOffset { get; private set; }
        public int Crashes { get; private set; }

        public void Initialize(BoardController board, RoadPath road, CameraController cameraController)
        {
            _board = board;
            _road = road;
            _camera = cameraController;
            _board.Crashed += () => Crashes++;
        }

        void Update()
        {
            if (_board == null || _board.Simulation == null) return;
            BoardState s = _board.State;
            Distance = _road.Project(s.Position, ref _roadHint, out float lateral);
            LateralOffset = lateral;

            if (_board.Simulation.RestartDue) RespawnOnRoad(Distance - RespawnBackOff);
            else if (Distance > _road.Length - EndOfRoadMargin) RestartRun();
        }

        public void RestartRun()
        {
            _roadHint = 0;
            RespawnOnRoad(0f);
        }

        void RespawnOnRoad(float distance)
        {
            distance = Mathf.Clamp(distance, 0f, _road.Length - EndOfRoadMargin - 1f);
            _road.Sample(distance, out System.Numerics.Vector3 position, out float yaw);
            _board.Restart(position.ToUnity(), yaw);
            RunDistanceStart = distance;
            if (_camera != null) _camera.Snap();
        }
    }
}
