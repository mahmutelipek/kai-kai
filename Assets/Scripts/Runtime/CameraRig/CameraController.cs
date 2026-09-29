using Game.Simulation;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Chase camera: ~7 m behind, 3.5 m above, ~15 deg down, FOV 65 -> 80 with speed, mild banking.
    /// Feel: pulls back with speed, leads into turns, dips on hard landings, kicks away from impacts,
    /// trembles while the board wobbles, shakes near top speed, pulls up and back while crashed.
    /// Always keeps the board, its tilt and the road ahead readable.
    /// </summary>
    public sealed class CameraController : MonoBehaviour
    {
        [Header("Framing")]
        public float distanceBehind = 7f;
        public float extraDistanceAtSpeed = 1.3f;
        public float heightAbove = 3.5f;
        public float lookAhead = 6f;
        public float lookHeight = 0f;
        public float positionSmoothTime = 0.12f;
        public float yawSmoothTime = 0.35f;
        [Tooltip("Seconds of yaw rate the camera looks ahead into a turn")]
        public float turnLead = 0.35f;

        [Header("Feedback")]
        public float fovMin = 65f;
        public float fovMax = 80f;
        public float bankFactor = 0.25f;
        public float highSpeedShake = 0.06f;
        public float impactShake = 0.35f;
        public float wobbleShake = 0.05f;
        public float landingDipPerMs = 0.03f;

        Camera _camera;
        BoardController _board;
        Vector3 _velocity;
        float _yaw, _yawVelocity;
        float _impact;
        float _noiseSeed;
        // spring for landing dips / impact kicks (camera-local offset)
        Vector3 _kick, _kickVelocity;

        public static CameraController Create(BoardController board)
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }
            var controller = cam.gameObject.GetComponent<CameraController>();
            if (controller == null) controller = cam.gameObject.AddComponent<CameraController>();
            controller.Attach(board, cam);
            return controller;
        }

        void Attach(BoardController board, Camera cam)
        {
            _camera = cam;
            _camera.nearClipPlane = 0.2f;
            _camera.farClipPlane = 1500f;
            _board = board;
            _board.Impact += OnImpact;
            _board.Landed += OnLanded;
            _noiseSeed = Random.value * 100f;
            Snap();
        }

        void OnDestroy()
        {
            if (_board == null) return;
            _board.Impact -= OnImpact;
            _board.Landed -= OnLanded;
        }

        void OnImpact(float strength)
        {
            _impact = Mathf.Max(_impact, strength);
            _kickVelocity += new Vector3(Random.Range(-1f, 1f), 0.5f, -1.5f) * (strength * 3f);
        }

        void OnLanded(float landingSpeed)
        {
            if (landingSpeed < 2f) return;
            _kickVelocity += Vector3.down * Mathf.Min(landingSpeed * landingDipPerMs * 30f, 6f);
            if (landingSpeed > 6f) _impact = Mathf.Max(_impact, 0.3f);
        }

        public void Snap()
        {
            if (_board == null) return;
            _yaw = _board.State.TravelYaw * Mathf.Rad2Deg;
            _yawVelocity = 0f;
            _velocity = Vector3.zero;
            _kick = _kickVelocity = Vector3.zero;
            transform.position = DesiredPosition(_board.transform.position, _yaw, 0f);
            transform.rotation = Quaternion.LookRotation(LookTarget(_board.transform.position, _yaw, 0f) - transform.position);
        }

        void LateUpdate()
        {
            if (_board == null || _board.Simulation == null) return;
            BoardState s = _board.State;
            BoardTuningData t = _board.Tuning.data;
            Vector3 boardPos = _board.transform.position;
            float dt = Time.deltaTime;

            // follow the travel direction (not the wobbling nose), leading a little into turns; freeze while crashed
            if (!s.Crashed)
            {
                float lead = s.YawRate * Mathf.Rad2Deg * turnLead;
                _yaw = Mathf.SmoothDampAngle(_yaw, s.TravelYaw * Mathf.Rad2Deg + lead, ref _yawVelocity, yawSmoothTime);
            }

            float speedNorm = Mathf.Clamp01(s.Speed / Mathf.Max(t.softCapSpeed, 1f));
            float crashPull = s.Crashed ? Mathf.Clamp01(s.CrashTimer / 1f) : 0f;
            Vector3 desired = DesiredPosition(boardPos, _yaw, speedNorm)
                              + (Vector3.up * 2f - SimConvert.YawForward(_yaw * Mathf.Deg2Rad) * 3f) * crashPull;
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref _velocity, positionSmoothTime);

            // look into the turn so the road ahead stays in frame
            float turnLook = Mathf.Clamp(s.YawRate * Mathf.Rad2Deg * 0.08f, -3f, 3f);
            Quaternion look = Quaternion.LookRotation(LookTarget(boardPos, _yaw, turnLook) - transform.position, Vector3.up);
            float bank = -s.Roll * Mathf.Rad2Deg * bankFactor;
            transform.rotation = look * Quaternion.Euler(0f, 0f, bank);

            _camera.fieldOfView = Mathf.Lerp(_camera.fieldOfView, Mathf.Lerp(fovMin, fovMax, speedNorm), 1f - Mathf.Exp(-4f * dt));

            // kick spring (landing dip, impact recoil)
            const float stiffness = 90f, damping = 14f;
            _kickVelocity += (-stiffness * _kick - damping * _kickVelocity) * dt;
            _kick += _kickVelocity * dt;
            transform.position += transform.rotation * (_kick * 0.1f);

            // shake: near top speed, after impacts, and a tremble while the board wobbles
            _impact = Mathf.Max(0f, _impact - dt * 1.5f);
            float amp = highSpeedShake * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.7f, 1f, speedNorm))
                        + impactShake * _impact * _impact
                        + wobbleShake * s.Wobble;
            if (amp > 1e-4f)
            {
                float time = Time.time * 18f;
                var offset = new Vector3(Mathf.PerlinNoise(_noiseSeed, time) - 0.5f, Mathf.PerlinNoise(_noiseSeed + 10f, time) - 0.5f, 0f) * (2f * amp);
                transform.position += transform.rotation * offset;
            }
        }

        Vector3 DesiredPosition(Vector3 boardPos, float yawDeg, float speedNorm)
        {
            Vector3 fwd = SimConvert.YawForward(yawDeg * Mathf.Deg2Rad);
            float distance = distanceBehind + extraDistanceAtSpeed * speedNorm;
            return boardPos - fwd * distance + Vector3.up * heightAbove;
        }

        Vector3 LookTarget(Vector3 boardPos, float yawDeg, float lateral)
        {
            float yaw = yawDeg * Mathf.Deg2Rad;
            return boardPos + SimConvert.YawForward(yaw) * lookAhead + SimConvert.YawRight(yaw) * lateral + Vector3.up * lookHeight;
        }
    }
}
