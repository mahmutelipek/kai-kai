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

        Transform _pose, _armL, _armR, _marker;
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
            Color accent = Color.Lerp(main, Color.white, 0.55f);

            _pose = new GameObject("Pose").transform;
            _pose.SetParent(transform, false);

            for (int side = -1; side <= 1; side += 2)
            {
                PrimitiveFactory.Visual(PrimitiveType.Cube, _pose, new Vector3(side * 0.11f, 0.07f, 0.04f), new Vector3(0.17f, 0.13f, 0.32f), Color.white, "Sneaker");
                PrimitiveFactory.Visual(PrimitiveType.Cube, _pose, new Vector3(side * 0.11f, 0.02f, 0.04f), new Vector3(0.18f, 0.04f, 0.33f), main, "Sole");
                PrimitiveFactory.Visual(PrimitiveType.Capsule, _pose, new Vector3(side * 0.1f, 0.3f, 0f), new Vector3(0.14f, 0.18f, 0.14f), MaterialLibrary.Pants, "Leg");
            }
            PrimitiveFactory.Visual(PrimitiveType.Capsule, _pose, new Vector3(0f, 0.6f, 0f), new Vector3(0.44f, 0.24f, 0.32f), main, "Hoodie");
            _armL = Arm(-1, main);
            _armR = Arm(1, main);
            PrimitiveFactory.Visual(PrimitiveType.Sphere, _pose, new Vector3(0f, 0.98f, 0f), new Vector3(0.46f, 0.44f, 0.44f), MaterialLibrary.Skin, "Head");
            for (int side = -1; side <= 1; side += 2)
                PrimitiveFactory.Visual(PrimitiveType.Sphere, _pose, new Vector3(side * 0.08f, 1.0f, 0.2f), new Vector3(0.06f, 0.08f, 0.04f), Color.black, "Eye");

            // headwear differs per slot so silhouettes differ even before the art pass
            switch (slot % 3)
            {
                case 0: // cap
                    PrimitiveFactory.Visual(PrimitiveType.Sphere, _pose, new Vector3(0f, 1.1f, -0.01f), new Vector3(0.48f, 0.26f, 0.48f), accent, "Cap");
                    PrimitiveFactory.Visual(PrimitiveType.Cube, _pose, new Vector3(0f, 1.1f, 0.26f), new Vector3(0.3f, 0.03f, 0.2f), accent, "Visor");
                    break;
                case 1: // beanie
                    PrimitiveFactory.Visual(PrimitiveType.Sphere, _pose, new Vector3(0f, 1.13f, 0f), new Vector3(0.47f, 0.34f, 0.47f), accent, "Beanie");
                    PrimitiveFactory.Visual(PrimitiveType.Sphere, _pose, new Vector3(0f, 1.3f, 0f), new Vector3(0.1f, 0.1f, 0.1f), accent, "Pompom");
                    break;
                default: // spiky hair
                    for (int i = 0; i < 5; i++)
                    {
                        GameObject spike = PrimitiveFactory.Visual(PrimitiveType.Cube, _pose, new Vector3((i - 2) * 0.07f, 1.18f, -0.03f), new Vector3(0.07f, 0.18f, 0.2f), new Color(0.15f, 0.1f, 0.08f), "Hair");
                        spike.transform.localRotation = Quaternion.Euler(-20f, 0f, (i - 2) * 12f);
                    }
                    break;
            }

            _marker = PrimitiveFactory.Visual(PrimitiveType.Cube, transform, new Vector3(0f, 1.55f, 0f), new Vector3(0.16f, 0.16f, 0.16f), main, "YouMarker").transform;
            _marker.localRotation = Quaternion.Euler(45f, 0f, 45f);
        }

        Transform Arm(int side, Color color)
        {
            var pivot = new GameObject(side < 0 ? "ArmL" : "ArmR").transform;
            pivot.SetParent(_pose, false);
            pivot.localPosition = new Vector3(side * 0.24f, 0.72f, 0f);
            PrimitiveFactory.Visual(PrimitiveType.Capsule, pivot, new Vector3(side * 0.02f, -0.17f, 0f), new Vector3(0.11f, 0.17f, 0.11f), color, "Sleeve");
            PrimitiveFactory.Visual(PrimitiveType.Sphere, pivot, new Vector3(side * 0.03f, -0.34f, 0f), new Vector3(0.1f, 0.1f, 0.1f), MaterialLibrary.Skin, "Hand");
            return pivot;
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

            // lean into own motion, counter-lean against the board roll, shake while staggered
            float speedNorm = Mathf.Clamp01(board.Speed / Mathf.Max(t.softCapSpeed, 1f));
            float leanForward = Mathf.Clamp(v.magnitude * 5f, 0f, 18f) + speedNorm * 10f;
            float counterRoll = board.Roll * Mathf.Rad2Deg * 0.6f;
            float shake = p.StaggerTimer > 0f ? Mathf.Sin(Time.time * 40f) * 12f : 0f;
            _pose.localRotation = Quaternion.Euler(0f, _facingYaw, 0f) * Quaternion.Euler(leanForward, 0f, counterRoll + shake);

            // crouch with speed and danger, stretch while jumping; pop after respawn
            float crouch = 1f - 0.12f * speedNorm - 0.15f * board.Wobble;
            float stretch = p.Height > 0.05f ? (p.VerticalVelocity > 0f ? 1.15f : 1.05f) : 1f;
            _popTimer = Mathf.Max(0f, _popTimer - Time.deltaTime);
            float pop = 1f + Mathf.Sin(_popTimer / 0.3f * Mathf.PI) * 0.25f;
            float thin = stretch > 1f ? 0.93f : 1f;
            _pose.localScale = new Vector3(pop * thin, crouch * pop * stretch, pop * thin);

            // arms: balance out wide at speed, flail when wobbling, up in the air when cheering
            _celebrateTimer = Mathf.Max(0f, _celebrateTimer - Time.deltaTime);
            float cheer = _celebrateTimer > 0f && board.Wobble < 0.1f ? Mathf.Sin(Mathf.Min(1f, _celebrateTimer / 1.1f) * Mathf.PI) : 0f;
            float spread = Mathf.Lerp(15f + 35f * speedNorm + 90f * board.Wobble, 160f, cheer);
            float flail = board.Wobble * Mathf.Sin(Time.time * 18f + _slot) * 40f + cheer * Mathf.Sin(Time.time * 14f + _slot) * 15f;
            _armL.localRotation = Quaternion.Euler(0f, 0f, -spread - flail);
            _armR.localRotation = Quaternion.Euler(0f, 0f, spread - flail);

            _marker.localPosition = new Vector3(0f, 1.55f + Mathf.Sin(Time.time * 4f) * 0.06f, 0f);
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
