using UnityEngine;

namespace Game
{
    public enum ImpactSeverity
    {
        Light = 0,
        Heavy = 1,
        Crash = 2,
    }

    /// <summary>Something the board can hit. Tells the board how bad the hit was; the board decides what that does.</summary>
    public abstract class ObstacleBase : MonoBehaviour
    {
        [SerializeField] float hitCooldown = 0.5f;
        float _lastHitTime = -999f;

        public abstract ImpactSeverity Severity { get; }

        public void NotifyBoardHit(BoardController board, Collision collision)
        {
            if (Time.time - _lastHitTime < hitCooldown) return;
            _lastHitTime = Time.time;
            board.ApplyImpact(Severity, collision.relativeVelocity.magnitude);
            OnHit(board, collision);
        }

        protected virtual void OnHit(BoardController board, Collision collision) { }
    }
}
