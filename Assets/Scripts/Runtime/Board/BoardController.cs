using System;
using Game.Simulation;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Hosts the authoritative BoardSimulation in FixedUpdate and applies its pose to a kinematic Rigidbody.
    /// It never decides steering itself: pose comes only from the simulation.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class BoardController : MonoBehaviour
    {
        public const float SimulationRate = 60f;

        BoardTuning _tuning;
        Rigidbody _rb;
        IPlayerInputProvider _inputProvider;
        readonly PlayerInputState[] _inputs = new PlayerInputState[BoardSimulation.MaxPlayers];

        public BoardSimulation Simulation { get; private set; }
        public BoardTuning Tuning => _tuning;
        public BoardState State => Simulation.Board.State;
        public SimStepEvents LastEvents { get; private set; }

        public event Action Crashed;
        public event Action<float> Impact;       // strength 0..1 for camera shake / audio
        public event Action<float> Landed;       // landing speed m/s

        public static BoardController Create(BoardTuning tuning, IGroundProvider ground, Vector3 position, float yawRad, int playerCount)
        {
            var go = new GameObject("Board");
            var board = go.AddComponent<BoardController>();
            board.Initialize(tuning, ground, position, yawRad, playerCount);
            return board;
        }

        void Initialize(BoardTuning tuning, IGroundProvider ground, Vector3 position, float yawRad, int playerCount)
        {
            _tuning = tuning;
            Simulation = new BoardSimulation(tuning.data, ground, playerCount);
            Simulation.Restart(position.ToSim(), yawRad);

            _rb = GetComponent<Rigidbody>();
            _rb.isKinematic = true;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            BoardTuningData t = tuning.data;
            var box = gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, t.deckHeight * 0.55f, 0f);
            box.size = new Vector3(t.boardWidth, t.deckHeight * 0.9f, t.boardLength);

            transform.SetPositionAndRotation(position, SimConvert.PoseRotation(yawRad, 0f, 0f));
            _rb.position = transform.position;
            _rb.rotation = transform.rotation;
        }

        void OnDisable()
        {
            if (_rb == null || Simulation == null) return;
            _rb.interpolation = RigidbodyInterpolation.None;
            _rb.position = State.Position.ToUnity();
            _rb.rotation = SimConvert.PoseRotation(State.Yaw, State.Pitch, State.Roll);
            transform.SetPositionAndRotation(_rb.position, _rb.rotation);
        }
        void OnEnable()
        {
            if (_rb != null) _rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        public void SetInputProvider(IPlayerInputProvider provider) => _inputProvider = provider;

        void FixedUpdate()
        {
            if (Simulation == null) return;
            // live tuning: the panel edits the ScriptableObject's data object that the simulation references
            Simulation.Tuning = _tuning.data;

            for (int i = 0; i < _inputs.Length; i++) _inputs[i] = PlayerInputState.None;
            _inputProvider?.CollectInputs(Simulation, _inputs, Time.fixedDeltaTime);

            SimStepEvents ev = Simulation.Step(Time.fixedDeltaTime, _inputs);
            LastEvents = ev;
            ApplyPose();

            if (ev.Board.Crashed)
            {
                Impact?.Invoke(1f);
                Crashed?.Invoke();
            }
            if (ev.Board.Landed) Landed?.Invoke(ev.Board.LandingSpeed);
        }

        void ApplyPose()
        {
            BoardState s = Simulation.Board.State;
            _rb.MovePosition(s.Position.ToUnity());
            _rb.MoveRotation(SimConvert.PoseRotation(s.Yaw, s.Pitch, s.Roll));
        }

        /// <summary>Teleports the board (restart / respawn). Resets the speed ramp.</summary>
        public void Restart(Vector3 position, float yawRad)
        {
            Simulation.Restart(position.ToSim(), yawRad);
            Quaternion rot = SimConvert.PoseRotation(yawRad, 0f, 0f);
            transform.SetPositionAndRotation(position, rot);
            _rb.position = position;
            _rb.rotation = rot;
        }

        public void ApplyImpact(ImpactSeverity severity, float relativeSpeed)
        {
            BoardTuningData t = _tuning.data;
            switch (severity)
            {
                case ImpactSeverity.Light:
                    Simulation.Board.ApplyImpact(t.lightImpactSpeedLoss, 0f);
                    Impact?.Invoke(0.25f);
                    break;
                case ImpactSeverity.Heavy:
                    Simulation.Board.ApplyImpact(t.lightImpactSpeedLoss * 3f, 0f);
                    for (int i = 0; i < Simulation.ActivePlayerCount; i++) Simulation.Players[i].Stagger(t.staggerTime * 2f);
                    Impact?.Invoke(0.6f);
                    break;
                default:
                    Simulation.Board.ForceCrash();
                    for (int i = 0; i < Simulation.ActivePlayerCount; i++) Simulation.Players[i].FallOff(float.MaxValue);
                    Impact?.Invoke(1f);
                    Crashed?.Invoke();
                    break;
            }
        }

        void OnCollisionEnter(Collision collision)
        {
            var obstacle = collision.collider.GetComponentInParent<ObstacleBase>();
            if (obstacle != null) obstacle.NotifyBoardHit(this, collision);
        }
    }
}
