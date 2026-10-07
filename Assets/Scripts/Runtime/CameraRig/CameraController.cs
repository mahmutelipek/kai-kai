using Game.Simulation;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Angled chase camera with speed FOV, mild banking,
    /// shake only at high speed or on impacts, a slower pull-back camera while crashed.
    /// </summary>
    public sealed class CameraController : MonoBehaviour
    {
        [Header("Framing")]
        public float distanceBehind = 6.3f;
        public float heightAbove = 3.1f;
        public float lateralOffset = -1.35f;
        public float lookAhead = 3.3f;
        public float lookHeight = .6f;
        public float positionSmoothTime = 0.12f;
        public float yawSmoothTime = 0.35f;

        [Header("Feedback")]
        public float fovMin = 61f;
        public float fovMax = 70f;
        public float bankFactor = 0.25f;
        public float highSpeedShake = 0.06f;
        public float impactShake = 0.35f;

        Camera _camera;
        BoardController _board;
        Vector3 _velocity;
        float _yaw, _yawVelocity;
        float _impact;
        float _noiseSeed;
        RunManager _run;

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
            _board.Impact += strength => _impact = Mathf.Max(_impact, strength);
            _noiseSeed = Random.value * 100f;
            Snap();
        }

        public void Snap()
        {
            if (_board == null) return;
            _yaw = _board.State.TravelYaw * Mathf.Rad2Deg;
            _yawVelocity = 0f;
            _velocity = Vector3.zero;
            transform.position = ClearView(_board.transform.position,DesiredPosition(_board.transform.position, _yaw));
            transform.rotation = Quaternion.LookRotation(LookTarget(_board.transform.position, _yaw) - transform.position);
        }

        void LateUpdate()
        {
            if (_board == null || _board.Simulation == null) return;
            if (_run == null) _run = _board.GetComponentInParent<RunManager>();
            BoardState s = _board.State;
            BoardTuningData t = _board.Tuning.data;
            Vector3 boardPos = _board.transform.position;

            // follow the travel direction (not the wobbling nose); freeze heading while crashed
            if (!s.Crashed)
                _yaw = Mathf.SmoothDampAngle(_yaw, s.TravelYaw * Mathf.Rad2Deg, ref _yawVelocity, yawSmoothTime);

            float crashPull = s.Crashed ? Mathf.Clamp01(s.CrashTimer / 1f) : 0f;
            Vector3 desired = DesiredPosition(boardPos, _yaw) + (Vector3.up * 2f - SimConvert.YawForward(_yaw * Mathf.Deg2Rad) * 3f) * crashPull;
            desired=ClearView(boardPos,desired);
            transform.position = ClearView(boardPos,Vector3.SmoothDamp(transform.position, desired, ref _velocity, positionSmoothTime));

            float speedNorm = Mathf.Clamp01(s.Speed / Mathf.Max(t.softCapSpeed, 1f));
            Quaternion look = Quaternion.LookRotation(LookTarget(boardPos, _yaw) - transform.position, Vector3.up);
            float bank = -s.Roll * Mathf.Rad2Deg * bankFactor - s.Steering * s.DriftAmount * 2.5f;
            transform.rotation = look * Quaternion.Euler(0f, 0f, bank);

            _camera.fieldOfView = Mathf.Lerp(_camera.fieldOfView, Mathf.Lerp(fovMin, fovMax, speedNorm) + (_run != null && _run.NitroActive ? 7f : 0f), 1f - Mathf.Exp(-4f * Time.deltaTime));

            // shake: only near top speed, or after impacts
            _impact = Mathf.Max(0f, _impact - Time.deltaTime * 1.5f);
            float amp = highSpeedShake * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.7f, 1f, speedNorm)) + impactShake * _impact * _impact;
            if (amp > 1e-4f)
            {
                float time = Time.time * 18f;
                var offset = new Vector3(Mathf.PerlinNoise(_noiseSeed, time) - 0.5f, Mathf.PerlinNoise(_noiseSeed + 10f, time) - 0.5f, 0f) * (2f * amp);
                transform.position += transform.rotation * offset;
            }
        }

        Vector3 ClearView(Vector3 boardPos,Vector3 desired)
        {
            Vector3 origin=boardPos+Vector3.up*1.2f;
            Vector3 ray=desired-origin;float distance=ray.magnitude;
            if(distance>.01f && Physics.SphereCast(origin,.25f,ray/distance,out RaycastHit hit,distance,1<<2,QueryTriggerInteraction.Ignore))
                return origin+ray/distance*Mathf.Max(.6f,hit.distance-.2f);
            return desired;
        }

        Vector3 DesiredPosition(Vector3 boardPos, float yawDeg)
        {
            Vector3 fwd = SimConvert.YawForward(yawDeg * Mathf.Deg2Rad);
            Vector3 right = new Vector3(fwd.z, 0f, -fwd.x);
            float grade = _board != null ? Mathf.Tan(_board.State.Pitch) : 0;
            return boardPos - fwd * distanceBehind + right * (lateralOffset - (_board != null ? _board.State.Steering * .3f : 0)) + Vector3.up * (heightAbove - grade * distanceBehind * .55f);
        }

        Vector3 LookTarget(Vector3 boardPos, float yawDeg)
        {
            Vector3 fwd = SimConvert.YawForward(yawDeg * Mathf.Deg2Rad);
            float grade = _board != null ? Mathf.Tan(_board.State.Pitch) : 0;
            return boardPos + fwd * lookAhead + Vector3.up * (lookHeight + grade * lookAhead);
        }
    }
}
