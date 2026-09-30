using UnityEngine;

namespace Game
{
    /// <summary>
    /// Time-based juice: a short hit-stop on heavy impacts and a brief slow-motion on crashes.
    /// Only scales time (the fixed-step simulation simply runs slower); never touches gameplay values.
    /// </summary>
    public sealed class GameFeel : MonoBehaviour
    {
        public float hitStopScale = 0.08f;
        public float hitStopSeconds = 0.07f;
        public float crashSlowMoScale = 0.35f;
        public float crashSlowMoSeconds = 0.8f;

        BoardController _board;
        float _hitStopUntil, _slowMoUntil;

        public bool Enabled { get; set; } = true;

        /// <summary>
        /// The pause menu owns time while open: this component writes Time.timeScale every frame, so without the flag
        /// it would reset the pause's 0 back to 1 (countdown, particles and effects kept running under the menu).
        /// </summary>
        public bool Paused { get; set; }

        public void Initialize(BoardController board)
        {
            _board = board;
            _board.Impact += OnImpact;
            _board.Crashed += OnCrashed;
        }

        void OnDestroy()
        {
            if (_board != null)
            {
                _board.Impact -= OnImpact;
                _board.Crashed -= OnCrashed;
            }
            Time.timeScale = 1f;
        }

        void OnImpact(float strength)
        {
            if (Enabled && strength >= 0.5f && strength < 1f) _hitStopUntil = Time.unscaledTime + hitStopSeconds;
        }

        void OnCrashed()
        {
            if (Enabled) _slowMoUntil = Time.unscaledTime + crashSlowMoSeconds;
        }

        void Update()
        {
            if (Paused) { Time.timeScale = 0f; return; }
            if (!Enabled || CameraController.ReduceMotion) { Time.timeScale = 1f; return; }
            float now = Time.unscaledTime;
            float scale = 1f;
            if (now < _slowMoUntil)
            {
                // ease back to full speed over the last third
                float remaining = (_slowMoUntil - now) / crashSlowMoSeconds;
                scale = Mathf.Lerp(1f, crashSlowMoScale, Mathf.Clamp01(remaining * 3f));
            }
            if (now < _hitStopUntil) scale = Mathf.Min(scale, hitStopScale);
            Time.timeScale = scale;
        }
    }
}
