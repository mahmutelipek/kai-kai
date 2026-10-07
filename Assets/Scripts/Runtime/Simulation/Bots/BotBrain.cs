using System;
using System.Collections.Generic;
using System.Numerics;

namespace Game.Simulation
{
    public enum BotBehavior
    {
        Cooperative = 0,
        StubbornLeft = 1,
        StubbornRight = 2,
        RandomWanderer = 3,
        GreedyFront = 4,
        ScaredRear = 5,
    }

    /// <summary>Everything a bot may look at. Bots only output a PlayerInputState, like humans.</summary>
    public struct BotContext
    {
        public int Self;
        public IReadOnlyList<PlayerSim> Players;
        public int ActivePlayerCount;
        public BoardState Board;
        public BoardTuningData Tuning;
        /// <summary>Steering the road currently asks for, -1..1 (0 when unknown). Only cooperative bots care.</summary>
        public float SteerHint;
        public bool KeepFormation;
        public float Time;
        public float Dt;
    }

    /// <summary>Base class for bots: seeks a board-local target chosen by the concrete behavior.</summary>
    public abstract class BotBrain
    {
        protected readonly Random Rng;
        protected Vector2 Target;
        protected float RetargetTimer;
        protected float JumpTimer;

        public abstract BotBehavior Behavior { get; }

        protected BotBrain(int seed)
        {
            Rng = new Random(seed);
            JumpTimer = RandomRange(3f, 8f);
        }

        public PlayerInputState Decide(in BotContext ctx)
        {
            PlayerSim self = ctx.Players[ctx.Self];
            if (!self.IsOnBoard) return PlayerInputState.None;

            RetargetTimer -= ctx.Dt;
            JumpTimer -= ctx.Dt;
            Target = ChooseTarget(ctx, self);

            float maxX = ctx.Tuning.HalfWidth - ctx.Tuning.playerRadius;
            float maxZ = ctx.Tuning.HalfLength - ctx.Tuning.playerRadius;
            Target = new Vector2(SimMath.Clamp(Target.X, -maxX, maxX), SimMath.Clamp(Target.Y, -maxZ, maxZ));

            var input = new PlayerInputState(Seek(self.LocalPosition, Target));
            if (JumpTimer <= 0f)
            {
                input.Jump = WantsToJump(ctx);
                JumpTimer = RandomRange(4f, 10f);
            }
            return input;
        }

        protected abstract Vector2 ChooseTarget(in BotContext ctx, PlayerSim self);

        protected virtual bool WantsToJump(in BotContext ctx) => Rng.NextDouble() < 0.3;

        protected static Vector2 Seek(Vector2 from, Vector2 to)
        {
            Vector2 d = to - from;
            float dist = d.Length();
            if (dist < 0.05f) return Vector2.Zero;
            float strength = SimMath.Clamp01(dist / 0.6f);
            return d / dist * strength;
        }

        protected float RandomRange(float min, float max) => min + (float)Rng.NextDouble() * (max - min);

        public static BotBrain Create(BotBehavior behavior, int seed)
        {
            switch (behavior)
            {
                case BotBehavior.Cooperative: return new CooperativeBot(seed);
                case BotBehavior.StubbornLeft: return new StubbornBot(seed, -1f);
                case BotBehavior.StubbornRight: return new StubbornBot(seed, 1f);
                case BotBehavior.RandomWanderer: return new WandererBot(seed);
                case BotBehavior.GreedyFront: return new GreedyFrontBot(seed);
                case BotBehavior.ScaredRear: return new ScaredRearBot(seed);
                default: throw new ArgumentOutOfRangeException(nameof(behavior));
            }
        }

        /// <summary>Default behavior per player slot: every slot disagrees in a different way.</summary>
        public static BotBehavior DefaultBehaviorForSlot(int slot)
        {
            switch (slot % 6)
            {
                case 0: return BotBehavior.Cooperative;
                case 1: return BotBehavior.StubbornLeft;
                case 2: return BotBehavior.StubbornRight;
                case 3: return BotBehavior.RandomWanderer;
                case 4: return BotBehavior.GreedyFront;
                default: return BotBehavior.ScaredRear;
            }
        }
    }

    /// <summary>Tries to give the road what it asks for and to pull the board back out of danger.</summary>
    public sealed class CooperativeBot : BotBrain
    {
        public override BotBehavior Behavior => BotBehavior.Cooperative;
        public CooperativeBot(int seed) : base(seed) { }

        protected override Vector2 ChooseTarget(in BotContext ctx, PlayerSim self)
        {
            BoardTuningData t = ctx.Tuning;
            float wantedSteer = SimMath.Clamp(ctx.SteerHint, -0.6f, 0.6f);
            if (ctx.Board.Danger > 0.55f) wantedSteer = 0f; // safety first
            float wantedLateral = SimMath.SignedPow(wantedSteer, 1f / Math.Max(t.steeringExponent, 0.1f));
            float error = wantedLateral - ctx.Board.Lateral;
            float x = self.LocalPosition.X + error * t.HalfWidth * 1.5f;
            int rows = (ctx.ActivePlayerCount + 1) / 2;
            float z = !ctx.KeepFormation || rows <= 1 ? 0f : t.HalfLength * .55f * (1f - 2f * (ctx.Self / 2) / (rows - 1f));
            return new Vector2(x, z);
        }

        protected override bool WantsToJump(in BotContext ctx) => false;
    }

    /// <summary>
    /// Wants to be on one side. Alternates between pushing hard at the edge and "resting" nearer the middle,
    /// on its own random timer, so the two stubborn bots only sometimes cancel out. Ignores danger until
    /// the board is about to go over.
    /// </summary>
    public sealed class StubbornBot : BotBrain
    {
        readonly float _side;
        bool _pushing;
        public override BotBehavior Behavior => _side < 0f ? BotBehavior.StubbornLeft : BotBehavior.StubbornRight;

        public StubbornBot(int seed, float side) : base(seed)
        {
            _side = side;
            _pushing = Rng.NextDouble() < 0.5;
        }

        protected override Vector2 ChooseTarget(in BotContext ctx, PlayerSim self)
        {
            BoardTuningData t = ctx.Tuning;
            if (RetargetTimer <= 0f)
            {
                _pushing = !_pushing;
                RetargetTimer = _pushing ? RandomRange(3f, 7f) : RandomRange(2f, 5f);
                float depth = _pushing ? RandomRange(0.75f, 0.95f) : RandomRange(0.1f, 0.35f);
                Target = new Vector2(_side * t.HalfWidth * depth, t.HalfLength * RandomRange(-0.6f, 0.6f));
            }
            if (ctx.Board.Danger > 0.9f && MathF.Sign(ctx.Board.Steering) == MathF.Sign(_side))
                return new Vector2(_side * t.HalfWidth * 0.2f, Target.Y);
            return Target;
        }
    }

    /// <summary>Picks a random spot every few seconds and walks there. Likes jumping.</summary>
    public sealed class WandererBot : BotBrain
    {
        public override BotBehavior Behavior => BotBehavior.RandomWanderer;
        public WandererBot(int seed) : base(seed) { }

        protected override Vector2 ChooseTarget(in BotContext ctx, PlayerSim self)
        {
            if (RetargetTimer <= 0f)
            {
                RetargetTimer = RandomRange(1.5f, 3.5f);
                BoardTuningData t = ctx.Tuning;
                Target = new Vector2(t.HalfWidth * RandomRange(-0.95f, 0.95f), t.HalfLength * RandomRange(-0.85f, 0.85f));
            }
            return Target;
        }

        protected override bool WantsToJump(in BotContext ctx) => Rng.NextDouble() < 0.6;
    }

    /// <summary>
    /// Crowds the nose for speed and runs to whichever side the board is already leaning toward
    /// ("where the action is"), which amplifies turns until someone else pushes back.
    /// </summary>
    public sealed class GreedyFrontBot : BotBrain
    {
        float _side = 1f;
        public override BotBehavior Behavior => BotBehavior.GreedyFront;
        public GreedyFrontBot(int seed) : base(seed) { }

        protected override Vector2 ChooseTarget(in BotContext ctx, PlayerSim self)
        {
            BoardTuningData t = ctx.Tuning;
            if (RetargetTimer <= 0f)
            {
                RetargetTimer = RandomRange(1.5f, 3.5f);
                float lean = ctx.Board.Lateral;
                _side = Math.Abs(lean) > 0.05f ? MathF.Sign(lean) : (Rng.NextDouble() < 0.5 ? -1f : 1f);
                if (Rng.NextDouble() < 0.25) _side = -_side; // sometimes changes its mind
            }
            return new Vector2(_side * t.HalfWidth * 0.75f, t.HalfLength * 0.85f);
        }
    }

    /// <summary>Hides at the tail. When the board gets scary it panics toward the uphill side.</summary>
    public sealed class ScaredRearBot : BotBrain
    {
        public override BotBehavior Behavior => BotBehavior.ScaredRear;
        public ScaredRearBot(int seed) : base(seed) { }

        protected override Vector2 ChooseTarget(in BotContext ctx, PlayerSim self)
        {
            BoardTuningData t = ctx.Tuning;
            if (ctx.Board.Danger > 0.45f)
                return new Vector2(-MathF.Sign(ctx.Board.Steering) * t.HalfWidth * 0.85f, -t.HalfLength * 0.8f);
            if (RetargetTimer <= 0f)
            {
                RetargetTimer = RandomRange(2f, 4f);
                Target = new Vector2(t.HalfWidth * RandomRange(-0.3f, 0.3f), -t.HalfLength * 0.8f);
            }
            return Target;
        }

        protected override bool WantsToJump(in BotContext ctx) => false;
    }
}
