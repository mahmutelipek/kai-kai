using System;
using Game.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game
{
    public enum RunPhase { Ready, Playing, Paused, Finishing, Results }
    public sealed class RunManager : MonoBehaviour
    {
        BoardController _board;
        RoadPath _road;
        CameraController _camera;
        EndlessRoad _streamer;
        int _roadHint;
        float _scoredDistance;
        public RunLeaderboard Leaderboard { get; } = new RunLeaderboard();
        float _pauseAt, _finishAt, _lastGem = -100f, _lastHit = -100f;
        public bool SessionEnabled { get; private set; }
        public RunPhase Phase { get; private set; } = RunPhase.Playing;
        public bool AcceptsGameplay => Phase == RunPhase.Playing;
        public float Distance { get; private set; }
        public float RunDistanceStart { get; private set; }
        public float LateralOffset { get; private set; }
        public int Crashes { get; private set; }
        public int Coins { get; private set; }
        public float NitroCharge { get; private set; }
        public float NitroRemaining { get; private set; }
        public bool NitroActive => NitroRemaining > 0;
        public bool NitroReady => NitroCharge >= 100f;
        public float BestDistance => PlayerPrefs.GetFloat("KaiKai.BestDistance", 0);
        public event Action<ArcadePickupKind> Pickup;
        public event Action NitroStarted;
        public int Diamonds { get; private set; }
        public int Hull { get; private set; } = 3;
        public int Combo { get; private set; }
        public int GemScore { get; private set; }
        public int Score => Mathf.FloorToInt(Distance) + GemScore;
        public int BestScore => PlayerPrefs.GetInt("KaiKai.BestScore", 0);
        public event Action<bool> Feedback;
        public event Action Restarted;
        public void Initialize(BoardController board, RoadPath road, CameraController cameraController, EndlessRoad streamer = null)
        {
            _board = board; _road = road; _camera = cameraController; _streamer = streamer;
            _board.Crashed += OnCrash;
        }
        public void ConfigureSession(bool enabled)
        {
            SessionEnabled = enabled;
            if (enabled) ShowMenu();
        }
        void Update()
        {
            if (_board == null || _board.Simulation == null) return;
            if (Phase == RunPhase.Finishing)
            {
                if (Time.unscaledTime >= _finishAt)
                {
                    Phase = RunPhase.Results; _board.enabled = false;
                    if (Distance > BestDistance) PlayerPrefs.SetFloat("KaiKai.BestDistance", Distance);
                    if (Score > BestScore) PlayerPrefs.SetInt("KaiKai.BestScore", Score);
                    PlayerPrefs.Save();
                }
                return;
            }
            if (!AcceptsGameplay) return;
            if ((Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame) ||
                (Gamepad.current != null && Gamepad.current.rightShoulder.wasPressedThisFrame)) TryActivateNitro();
            BoardState s = _board.State;
            Distance = _road.Project(s.Position.ToUnity(), ref _roadHint, out float lateral);
            LateralOffset = lateral;
            float progress=Mathf.Max(0,Distance-_scoredDistance);
            _scoredDistance=Mathf.Max(_scoredDistance,Distance);
            if(!s.Crashed)for(int i=0;i<_board.Simulation.ActivePlayerCount;i++)
                if(_board.Simulation.Players[i].IsOnBoard)Leaderboard.Travel(i,progress);
            if (SessionEnabled && !s.Crashed)
            {
                _road.Sample(Distance, out Vector3 roadPoint, out _);
                if (Mathf.Abs(lateral) > 18f || s.Position.Y < roadPoint.y - 8f)
                { _board.ApplyImpact(ImpactSeverity.Crash, s.Speed); return; }
            }
            if (Time.time - _lastGem > 3f) Combo = 0;
            if (!SessionEnabled && _board.Simulation.RestartDue) RespawnOnRoad(Distance - 6f);
            else if (_streamer == null && Distance > _road.Length - 40f) RestartRun();
        }
        public void ShowMenu() { RestartRun(); Phase = RunPhase.Ready; _board.enabled = false; }
        public void TogglePause()
        {
            if (!SessionEnabled || (Phase != RunPhase.Playing && Phase != RunPhase.Paused)) return;
            if (Phase == RunPhase.Playing) { _pauseAt = Time.time; Phase = RunPhase.Paused; }
            else { float elapsed=Time.time-_pauseAt; _lastGem+=elapsed; _lastHit+=elapsed; Phase=RunPhase.Playing; }
            _board.enabled = Phase == RunPhase.Playing;
        }
        public void RestartRun()
        {
            _streamer?.ResetRoad(); _roadHint = 0;
            Diamonds = Coins = Crashes = Combo = GemScore = 0; NitroCharge = NitroRemaining = 0; Hull = 3; Distance = 0f;
            _lastGem = _lastHit = -100f; _scoredDistance=0; Leaderboard.Reset();
            RespawnOnRoad(0f); Phase = RunPhase.Playing; _board.enabled = true; Restarted?.Invoke();
        }
        public void CollectDiamond(int rider=-1)
        {
            if (!AcceptsGameplay) return;
            Diamonds++; Combo = Time.time - _lastGem < 3f ? Mathf.Min(5, Combo + 1) : 1;
            _lastGem = Time.time; NitroCharge = Mathf.Min(100, NitroCharge + 5); GemScore += 25 * Combo; if(rider>=0)Leaderboard.Award(rider,25*Combo); Feedback?.Invoke(true);
        }
        void FixedUpdate()
        {
            if (_board == null || !AcceptsGameplay) return;
            NitroRemaining = Mathf.Max(0, NitroRemaining - Time.fixedDeltaTime);
            _board.Simulation.Board.NitroActive = NitroActive;
            if (!NitroActive && _board.State.DriftAmount > .4f)
                NitroCharge = Mathf.Min(100, NitroCharge + 8f * Time.fixedDeltaTime);
        }
        public bool TryActivateNitro()
        {
            if (!AcceptsGameplay || !NitroReady || NitroActive || _board.State.Crashed) return false;
            NitroCharge = 0; NitroRemaining = 3.5f; _board.Simulation.Board.NitroActive = true;
            NitroStarted?.Invoke(); return true;
        }
        public void CollectArcade(ArcadePickupKind kind,int rider=-1)
        {
            if (!AcceptsGameplay) return;
            if (kind == ArcadePickupKind.Coin) { Coins++; GemScore += 10; NitroCharge = Mathf.Min(100,NitroCharge + 2); }
            else NitroCharge = 100;
            if(rider>=0)Leaderboard.Award(rider,kind==ArcadePickupKind.Coin?10:30);
            Pickup?.Invoke(kind);
        }
        public int NearestRider(Vector3 pickupPosition)
        {
            int best=-1;float distance=float.PositiveInfinity;
            for(int i=0;i<_board.Simulation.ActivePlayerCount;i++)
            {
                var p=_board.Simulation.Players[i];if(!p.IsOnBoard)continue;
                Vector3 position=_board.transform.TransformPoint(new Vector3(p.LocalPosition.X,_board.Tuning.data.deckHeight,p.LocalPosition.Y));
                float d=(position-pickupPosition).sqrMagnitude;if(d<distance){distance=d;best=i;}
            }
            return best;
        }
        public void HitObstacle(bool heavy)
        {
            if (!AcceptsGameplay || Time.time - _lastHit < 1f) return;
            _lastHit = Time.time; Combo = 0; Feedback?.Invoke(false);
            if (!SessionEnabled) return;
            NitroRemaining = 0; _board.Simulation.Board.NitroActive = false;
            Hull = Mathf.Max(0, Hull - (heavy ? 2 : 1));
            if (Hull == 0) _board.ApplyImpact(ImpactSeverity.Crash, _board.State.Speed);
        }
        void OnCrash()
        {
            Crashes++; NitroRemaining = 0; _board.Simulation.Board.NitroActive = false;
            if (SessionEnabled) { Leaderboard.Finish(_board.Simulation.ActivePlayerCount); Phase = RunPhase.Finishing; _finishAt = Time.unscaledTime + 1.4f; }
        }
        void OnDestroy() { if (_board != null) _board.Crashed -= OnCrash; }
        void RespawnOnRoad(float distance)
        {
            distance = Mathf.Clamp(distance, _road.StartDistance, _road.Length - 41f);
            _road.Sample(distance, out Vector3 position, out float yaw);
            _board.Restart(position, yaw); RunDistanceStart = distance; _camera?.Snap();
        }
    }
}
