using System;
using System.Numerics;

namespace Game.Simulation
{
    public enum PlayerSimState
    {
        OnBoard = 0,
        Fallen = 1,
    }

    /// <summary>What a player feels from the board this step.</summary>
    public struct PlayerBoardContext
    {
        public float Wobble;
        public float Roll;
        public float BoardAcceleration;
    }

    /// <summary>
    /// Kinematic character in board-local space (no Rigidbody). X = right, Y = toward the nose (meters),
    /// Height = feet above the deck. Physics-style reactions (bump, stagger, slide) are applied here.
    /// </summary>
    public sealed class PlayerSim
    {
        public readonly int Id;
        public PlayerSimState State = PlayerSimState.OnBoard;
        public Vector2 LocalPosition;
        public Vector2 PreviousLocalPosition;
        public Vector2 Velocity;
        public float Height;
        public float PreviousHeight;
        public float VerticalVelocity;
        public float FallTimer;
        public float StaggerTimer;
        public float JumpCooldown;
        public float BodyWeight = 1f;
        /// <summary>Normalized direction (board-local) the player left the board in; used by visuals.</summary>
        public Vector2 FallDirection;
        public int FallCount;
        public int RespawnCount;

        /// <summary>Test hook: when set, the player is held at PinnedPosition and ignores input.</summary>
        public bool Pinned;
        public Vector2 PinnedPosition;

        public PlayerSim(int id)
        {
            Id = id;
        }

        public bool IsOnBoard => State == PlayerSimState.OnBoard;
        public bool IsAirborne => Height > 1e-4f || VerticalVelocity > 0f;

        public void PlaceAt(Vector2 localPosition, float height = 0f)
        {
            State = PlayerSimState.OnBoard;
            LocalPosition = PreviousLocalPosition = localPosition;
            Height = PreviousHeight = height;
            Velocity = Vector2.Zero;
            VerticalVelocity = 0f;
            StaggerTimer = 0f;
            FallTimer = 0f;
        }

        public void Pin(Vector2 localPosition)
        {
            Pinned = true;
            PinnedPosition = localPosition;
            PlaceAt(localPosition);
        }

        public void Unpin() => Pinned = false;

        public void Stagger(float time)
        {
            if (Pinned) return;
            StaggerTimer = Math.Max(StaggerTimer, time);
        }

        public void FallOff(float respawnDelay)
        {
            if (State == PlayerSimState.Fallen) return;
            State = PlayerSimState.Fallen;
            FallTimer = respawnDelay;
            float len = LocalPosition.Length();
            FallDirection = len > 1e-4f ? LocalPosition / len : new Vector2(1f, 0f);
            Velocity = Vector2.Zero;
            VerticalVelocity = 0f;
            Height = 0f;
            FallCount++;
        }

        public void Step(float dt, in PlayerInputState input, in PlayerBoardContext board, BoardTuningData t)
        {
            PreviousLocalPosition = LocalPosition;
            PreviousHeight = Height;

            if (State == PlayerSimState.Fallen)
            {
                FallTimer -= dt;
                return;
            }
            if (Pinned)
            {
                LocalPosition = PreviousLocalPosition = PinnedPosition;
                Velocity = Vector2.Zero;
                Height = PreviousHeight = 0f;
                VerticalVelocity = 0f;
                return;
            }

            JumpCooldown = Math.Max(0f, JumpCooldown - dt);
            StaggerTimer = Math.Max(0f, StaggerTimer - dt);
            bool airborne = IsAirborne;
            float control = StaggerTimer > 0f ? t.staggerControlFactor : 1f;

            // steer own velocity toward the requested move
            Vector2 desired = input.ClampedMove() * t.playerMoveSpeed;
            float accelLimit = (airborne ? t.playerAirAcceleration : t.playerGroundAcceleration) * control;
            Vector2 dv = desired - Velocity;
            float dvLen = dv.Length();
            float maxDv = accelLimit * dt;
            if (dvLen > maxDv && dvLen > 1e-6f) dv *= maxDv / dvLen;
            Velocity += dv;

            // board forces: slide toward the low side when wobbling, lurch forward/back with board acceleration
            if (!airborne)
            {
                float slide = MathF.Sign(board.Roll) * t.dangerSlideAcceleration * board.Wobble;
                float lurch = -board.BoardAcceleration * t.boardInertiaFactor;
                Velocity += new Vector2(slide, lurch) * dt;
            }

            LocalPosition += Velocity * dt;

            // jumping
            if (input.Jump && !airborne && JumpCooldown <= 0f && control > 0.5f)
            {
                VerticalVelocity = t.playerJumpVelocity;
                JumpCooldown = t.playerJumpCooldown;
            }
            if (Height > 0f || VerticalVelocity > 0f)
            {
                VerticalVelocity -= t.playerGravity * dt;
                Height += VerticalVelocity * dt;
                if (Height <= 0f)
                {
                    Height = 0f;
                    VerticalVelocity = 0f;
                }
            }

            if (!SimMath.IsFinite(LocalPosition) || !SimMath.IsFinite(Velocity) || !SimMath.IsFinite(Height))
            {
                throw new InvalidOperationException("PlayerSim produced a non-finite state");
            }
        }

        /// <summary>Arcade edge support. Feet remain inside the rounded deck; large jump overshoots still fall.</summary>
        public void ConstrainToDeck(BoardTuningData t)
        {
            if (t.edgeAssist <= .5f || !IsOnBoard || IsAirborne || Pinned) return;
            float radius = Math.Max(.1f, t.HalfWidth - t.playerRadius * .35f);
            float straight = Math.Max(0f, t.HalfLength - t.HalfWidth);
            Vector2 center = new Vector2(0, SimMath.Clamp(LocalPosition.Y, -straight, straight));
            Vector2 delta = LocalPosition - center;
            float distance = delta.Length();
            if (distance <= radius || distance - radius > .4f) return;
            Vector2 normal = delta / distance;
            LocalPosition = center + normal * radius;
            float outward = Vector2.Dot(Velocity, normal);
            if (outward > 0) Velocity -= normal * outward;
        }

        /// <summary>True if the player is standing (not in the air) past the deck edge.</summary>
        public bool IsOverEdge(BoardTuningData t)
        {
            if (Height > 1e-4f) return false;
            if (t.edgeAssist > .5f)
            {
                float straight = Math.Max(0,t.HalfLength-t.HalfWidth);
                Vector2 center = new Vector2(0,SimMath.Clamp(LocalPosition.Y,-straight,straight));
                return Vector2.DistanceSquared(LocalPosition,center) > t.HalfWidth*t.HalfWidth;
            }
            return Math.Abs(LocalPosition.X) > t.HalfWidth || Math.Abs(LocalPosition.Y) > t.HalfLength;
        }
    }
}
