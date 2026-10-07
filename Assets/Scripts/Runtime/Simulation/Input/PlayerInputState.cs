using System.Numerics;

namespace Game.Simulation
{
    /// <summary>
    /// The only thing the simulation reads from a player (human, gamepad, bot or, later, network).
    /// Move is in board-local space: X = right, Y = toward the board nose. Magnitude is clamped to 1.
    /// </summary>
    public struct PlayerInputState
    {
        public Vector2 Move;
        public bool Jump;
        public bool Action;
        public bool Drift;

        public static readonly PlayerInputState None = default;

        public PlayerInputState(Vector2 move, bool jump = false, bool action = false, bool drift = false)
        {
            Move = move;
            Jump = jump;
            Action = action;
            Drift = drift;
        }

        public Vector2 ClampedMove()
        {
            float len = Move.Length();
            if (!SimMath.IsFinite(len) || len < 1e-5f) return Vector2.Zero;
            return len > 1f ? Move / len : Move;
        }
    }
}
