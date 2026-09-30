using Game.Art;
using Game.Simulation;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Primitive placeholder character that mirrors one PlayerSim: interpolated deck position, lean,
    /// crouch, panic arms while the board wobbles, a tumbling fall when knocked off, a pop on respawn.
    /// Purely visual: it never feeds anything back into the simulation.
    /// </summary>
    public sealed class PlayerView : MonoBehaviour
    {
        BoardController _board;
        PlayerInputRouter _router;
        BoardView _boardView;
        int _slot;

        Transform _pose, _torso, _head, _armL, _armR, _marker;
        bool _detached;
        Vector3 _flightVelocity, _spin;
        float _flightTime, _popTimer, _facingYaw, _celebrateTimer;

        public int Slot => _slot;

        public static PlayerView Create(int slot, BoardController board, BoardView boardView, PlayerInputRouter router)
        {
            var go = new GameObject("Player " + (slot + 1));
            var view = go.AddComponent<PlayerView>();
            view.Build(slot, board, boardView, router);
            return view;
        }

        void Build(int slot, BoardController board, BoardView boardView, PlayerInputRouter router)
        {
            _slot = slot;
            _board = board;
            _boardView = boardView;
            _router = router;
            transform.SetParent(boardView.DeckTop, false);
            // cheer after a clean landing (the board is still upright)
            board.Landed += speed => { if (speed > 3f && board.Simulation != null && !board.State.Crashed) _celebrateTimer = 1.1f; };

            Color main = MaterialLibrary.PlayerColors[slot % MaterialLibrary.PlayerColors.Length];

            // M4: the stylised rider from Game.Art (rig groups Pose > Torso > Head / ArmL / ArmR)
            var rig = ArtBuilder.Build(ArtLibrary.Character(slot), transform);
            transform.localScale = Vector3.one * ArtLibrary.RiderScale;
            _pose = rig["Pose"];
            _torso = rig["Torso"];
            _head = rig["Head"];
            _armL = rig["ArmL"];
            _armR = rig["ArmR"];

            _marker = PrimitiveFactory.Visual(PrimitiveType.Cube, transform, new Vector3(0f, 2.1f, 0f), new Vector3(0.16f, 0.16f, 0.16f), main, "YouMarker").transform;
            _marker.localRotation = Quaternion.Euler(45f, 0f, 45f);
        }

        void LateUpdate()
        {
            BoardSimulation sim = _board.Simulation;
            if (sim == null) return;
            bool active = _slot < sim.ActivePlayerCount;
            if (_pose.gameObject.activeSelf != active && !_detached) _pose.gameObject.SetActive(active);
            _marker.gameObject.SetActive(active && !_detached && _router != null && _router.IsHumanControlled(_slot));
            if (!active) return;

            PlayerSim p = sim.Players[_slot];
            if (p.IsOnBoard)
            {
                if (_detached) Reattach();
                UpdateOnBoard(p, sim.Board.State, sim.Tuning);
            }
            else
            {
                if (!_detached) Detach(p, sim.Board.State);
                UpdateFlight();
            }
        }

        void UpdateOnBoard(PlayerSim p, BoardState board, BoardTuningData t)
        {
            float alpha = Mathf.Clamp01((Time.time - Time.fixedTime) / Time.fixedDeltaTime);
            Vector2 local = Vector2.Lerp(p.PreviousLocalPosition.ToUnity(), p.LocalPosition.ToUnity(), alpha);
            float height = Mathf.Lerp(p.PreviousHeight, p.Height, alpha);
            transform.localPosition = new Vector3(local.x, height, local.y);

            // face where you walk; otherwise face the nose
            Vector2 v = p.Velocity.ToUnity();
            float targetYaw = v.sqrMagnitude > 0.25f ? Mathf.Atan2(v.x, v.y) * Mathf.Rad2Deg : 0f;
            _facingYaw = Mathf.MoveTowardsAngle(_facingYaw, targetYaw, 540f * Time.deltaTime);

            // procedural skate pose (shared with the headless preview): deep crouch, lean into turns, balance arms
            float speedNorm = Mathf.Clamp01(board.Speed / Mathf.Max(t.softCapSpeed, 1f));
            _celebrateTimer = Mathf.Max(0f, _celebrateTimer - Time.deltaTime);
            float cheer = _celebrateTimer > 0f && board.Wobble < 0.1f ? Mathf.Sin(Mathf.Min(1f, _celebrateTimer / 1.1f) * Mathf.PI) : 0f;
            RiderPoseOutput pose = RiderPose.Compute(new RiderPoseInput
            {
                SpeedNorm = speedNorm, Wobble = board.Wobble, BoardRoll = board.Roll, YawRate = board.YawRate,
                Cheer = cheer, Stagger = p.StaggerTimer > 0f ? 1f : 0f, Time = Time.time, Slot = _slot,
                Jump = p.Height > 0.05f ? 1f : 0f,
            });
            // walking: lean into your own motion a little
            float walkLean = Mathf.Clamp(v.magnitude * 4f, 0f, 12f);
            _pose.localRotation = Quaternion.Euler(0f, _facingYaw, 0f) * pose.Pose.ToUnity() * Quaternion.Euler(walkLean, 0f, 0f);
            _torso.localRotation = pose.Torso.ToUnity();
            _head.localRotation = pose.Head.ToUnity();
            _armL.localRotation = pose.ArmL.ToUnity();
            _armR.localRotation = pose.ArmR.ToUnity();

            // squash with danger, stretch while jumping; pop after respawn
            float crouch = 1f - 0.08f * speedNorm - 0.12f * board.Wobble;
            float stretch = p.Height > 0.05f ? (p.VerticalVelocity > 0f ? 1.12f : 1.04f) : 1f;
            _popTimer = Mathf.Max(0f, _popTimer - Time.deltaTime);
            float pop = 1f + Mathf.Sin(_popTimer / 0.3f * Mathf.PI) * 0.25f;
            float thin = stretch > 1f ? 0.94f : 1f;
            _pose.localScale = new Vector3(pop * thin, crouch * pop * stretch, pop * thin);

            _marker.localPosition = new Vector3(0f, 2.1f + Mathf.Sin(Time.time * 4f) * 0.06f, 0f);
            _marker.Rotate(0f, 180f * Time.deltaTime, 0f, Space.World);
        }

        void Detach(PlayerSim p, BoardState board)
        {
            _detached = true;
            _flightTime = 0f;
            Transform deck = _boardView.DeckTop;
            Vector3 outward = deck.TransformDirection(new Vector3(p.FallDirection.X, 0f, p.FallDirection.Y)).normalized;
            Vector3 boardVelocity = SimConvert.YawForward(board.TravelYaw) * board.Speed + Vector3.up * board.VerticalVelocity;
            _flightVelocity = boardVelocity * 0.85f + outward * 4f + Vector3.up * 4.5f;
            _spin = new Vector3(Random.Range(-400f, 400f), Random.Range(-200f, 200f), Random.Range(-400f, 400f));
            transform.SetParent(null, true);
        }

        void UpdateFlight()
        {
            _flightTime += Time.deltaTime;
            _flightVelocity += Vector3.down * 20f * Time.deltaTime;
            transform.position += _flightVelocity * Time.deltaTime;
            transform.Rotate(_spin * Time.deltaTime, Space.Self);
            _armL.localRotation = Quaternion.Euler(0f, 0f, -160f);
            _armR.localRotation = Quaternion.Euler(0f, 0f, 160f);
            if (_flightTime > 1.3f && _pose.gameObject.activeSelf) _pose.gameObject.SetActive(false);
        }

        void Reattach()
        {
            _detached = false;
            transform.SetParent(_boardView.DeckTop, false);
            transform.localRotation = Quaternion.identity;
            _pose.gameObject.SetActive(true);
            _popTimer = 0.3f;
        }
    }
}
