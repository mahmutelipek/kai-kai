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
        /// <summary>0..1 how much the road ahead asks to slow down (see RoadModel.BrakeHint).</summary>
        public float BrakeHint;
        /// <summary>A stored nitro charge can be fired (Action).</summary>
        public bool NitroAvailable;
        /// <summary>The crew's latest "JUMP!" call (see <see cref="CrewCall"/>): bots answer each id once.</summary>
        public int CrewCallId;
        public float CrewCallAge;
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
        int _seenCall;
        float _reaction;
        bool _answered = true;

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
            Vector2 target = ChooseTarget(ctx, self);

            float maxX = ctx.Tuning.HalfWidth - ctx.Tuning.playerRadius - EdgeMargin;
            float maxZ = ctx.Tuning.HalfLength - ctx.Tuning.playerRadius - 0.1f;
            target = new Vector2(SimMath.Clamp(target.X, -maxX, maxX), SimMath.Clamp(target.Y, -maxZ, maxZ));

            var input = new PlayerInputState(Seek(self.LocalPosition, target));

            // answer the crew's "JUMP!" call after a human-like reaction time (most bots, most of the time)
            if (ctx.CrewCallId != _seenCall)
            {
                _seenCall = ctx.CrewCallId;
                _reaction = RandomRange(ReactionMin, ReactionMax);
                _answered = false;
                JumpTimer = Math.Max(JumpTimer, 2f); // no random hop right before or after a crew jump
            }
            if (!_answered && ctx.CrewCallAge >= _reaction)
            {
                _answered = true;
                if (Rng.NextDouble() < AnswerChance) { input.Jump = true; return input; }
            }

            if (JumpTimer <= 0f)
            {
                input.Jump = WantsToJump(ctx);
                JumpTimer = RandomRange(4f, 10f);
            }
            return input;
        }

        protected abstract Vector2 ChooseTarget(in BotContext ctx, PlayerSim self);

        protected virtual bool WantsToJump(in BotContext ctx) => Rng.NextDouble() < 0.3;

        /// <summary>Chance to join a crew call and how fast (seconds). Personalities differ; stubborn bots often ignore it.</summary>
        protected virtual float AnswerChance => 0.85f;
        protected virtual float ReactionMin => 0.08f;
        protected virtual float ReactionMax => 0.26f;

        /// <summary>How far from the deck edge this bot keeps its target (stubborn bots go right to the edge).</summary>
        protected virtual float EdgeMargin => 0.12f;

        /// <summary>
        /// Lateral spot that would give the road what it asks for (and back off when the board gets dangerous).
        /// Proportional correction on the board's current lateral weight.
        /// </summary>
        protected static float RoadHelpX(in BotContext ctx, PlayerSim self)
        {
            BoardTuningData t = ctx.Tuning;
            float wantedSteer = SimMath.Clamp(ctx.SteerHint, -0.6f, 0.6f);
            if (ctx.Board.Danger > 0.68f) wantedSteer = 0f; // safety first (wobble starts at 0.7)
            float wantedLateral = SimMath.SignedPow(wantedSteer, 1f / Math.Max(t.steeringExponent, 0.1f));
            float error = wantedLateral - ctx.Board.Lateral;
            return self.LocalPosition.X + error * t.HalfWidth * 1.0f; // 1.5 overshot at 35-40 m/s: a lone rider wove into dodges and crashed on ~1 in 4 seeds
        }

        /// <summary>Own lane along the deck so bots do not all pile onto the same spot.</summary>
        protected static float LaneZ(in BotContext ctx)
        {
            int n = Math.Max(1, ctx.ActivePlayerCount);
            float t = n == 1 ? 0.5f : ctx.Self / (float)(n - 1);
            return SimMath.Lerp(-0.6f, 0.6f, t) * ctx.Tuning.HalfLength;
        }

        /// <summary>Blend a personal preference toward the road-helping spot (0 = ignores the road, 1 = fully cooperative).</summary>
        protected static float BlendTowardRoad(float personalX, in BotContext ctx, PlayerSim self, float awareness) =>
            personalX + (RoadHelpX(ctx, self) - personalX) * SimMath.Clamp01(awareness);

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

    /// <summary>Gives the road what it asks for and pulls the board back out of danger.</summary>
    public sealed class CooperativeBot : BotBrain
    {
        protected override float AnswerChance => 0.97f;
        protected override float ReactionMin => 0.06f;
        protected override float ReactionMax => 0.2f;
        public override BotBehavior Behavior => BotBehavior.Cooperative;
        public CooperativeBot(int seed) : base(seed) { }


        protected override Vector2 ChooseTarget(in BotContext ctx, PlayerSim self)
        {
            // curve or dodge ahead: squeeze the lanes into the tail half to brake, without piling up
            float lane = LaneZ(ctx);
            float braking = -0.55f * ctx.Tuning.HalfLength + lane * 0.35f;
            float z = lane + (braking - lane) * SimMath.Clamp01(ctx.BrakeHint);
            return new Vector2(RoadHelpX(ctx, self), z);
        }

        protected override bool WantsToJump(in BotContext ctx) => false;
    }

    /// <summary>
    /// Wants to be on one side. Alternates between pushing hard at the edge (ignoring the road) and resting
    /// nearer the middle (half-helping), on its own random timer, so the two stubborn bots only sometimes
    /// cancel out. Gives in only when the board is about to go over.
    /// </summary>
    public sealed class StubbornBot : BotBrain
    {
        protected override float AnswerChance => 0.7f;
        protected override float ReactionMin => 0.12f;
        protected override float ReactionMax => 0.3f;
        readonly float _side;
        bool _pushing;
        float _depth, _z;
        public override BotBehavior Behavior => _side < 0f ? BotBehavior.StubbornLeft : BotBehavior.StubbornRight;
        protected override float EdgeMargin => 0f;

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
                _depth = _pushing ? RandomRange(0.75f, 0.95f) : RandomRange(0.1f, 0.35f);
                _z = LaneZ(ctx) + t.HalfLength * RandomRange(-0.2f, 0.2f);
            }
            if (ctx.Board.Danger > 0.9f && MathF.Sign(ctx.Board.Steering) == MathF.Sign(_side))
                return new Vector2(_side * t.HalfWidth * 0.2f, _z);
            float personal = _side * t.HalfWidth * _depth;
            return new Vector2(_pushing ? personal : BlendTowardRoad(personal, ctx, self, 0.55f), _z);
        }
    }

    /// <summary>Picks a random spot every few seconds and walks there, half-minding the road. Likes jumping.</summary>
    public sealed class WandererBot : BotBrain
    {
        protected override float AnswerChance => 0.9f;
        public override BotBehavior Behavior => BotBehavior.RandomWanderer;
        public WandererBot(int seed) : base(seed) { }

        protected override Vector2 ChooseTarget(in BotContext ctx, PlayerSim self)
        {
            if (RetargetTimer <= 0f)
            {
                RetargetTimer = RandomRange(1.5f, 3.5f);
                BoardTuningData t = ctx.Tuning;
                Target = new Vector2(t.HalfWidth * RandomRange(-0.85f, 0.85f), t.HalfLength * RandomRange(-0.75f, 0.75f));
            }
            return new Vector2(BlendTowardRoad(Target.X, ctx, self, 0.4f), Target.Y);
        }

        protected override bool WantsToJump(in BotContext ctx) => Rng.NextDouble() < 0.6;
    }

    /// <summary>
    /// Crowds the nose for speed and dashes from side to side after imaginary coins, only minding the road
    /// a little. (Following the board's lean instead was tested: it drags the board off the road.)
    /// </summary>
    public sealed class GreedyFrontBot : BotBrain
    {
        protected override float AnswerChance => 0.85f;
        protected override float ReactionMin => 0.06f;
        protected override float ReactionMax => 0.22f;
        float _side = 1f;
        public override BotBehavior Behavior => BotBehavior.GreedyFront;
        public GreedyFrontBot(int seed) : base(seed) { }

        protected override Vector2 ChooseTarget(in BotContext ctx, PlayerSim self)
        {
            BoardTuningData t = ctx.Tuning;
            if (RetargetTimer <= 0f)
            {
                RetargetTimer = RandomRange(1.5f, 3.5f);
                _side = RandomRange(-0.8f, 0.8f);
            }
            return new Vector2(BlendTowardRoad(_side * t.HalfWidth, ctx, self, 0.3f), t.HalfLength * 0.75f);
        }

        /// <summary>Greedy for speed: fires the nitro as soon as the road looks straight-ish.</summary>
        public PlayerInputState DecideWithNitro(in BotContext ctx)
        {
            PlayerInputState input = Decide(ctx);
            if (ctx.NitroAvailable && ctx.BrakeHint < 0.05f && ctx.Board.Danger < 0.3f && Rng.NextDouble() < 0.02) input.Action = true;
            return input;
        }
    }

    /// <summary>Hides at the tail and helps quietly. When the board gets scary it panics to the opposite side.</summary>
    public sealed class ScaredRearBot : BotBrain
    {
        protected override float AnswerChance => 0.8f;
        protected override float ReactionMin => 0.1f;
        protected override float ReactionMax => 0.3f;
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
            return new Vector2(BlendTowardRoad(Target.X, ctx, self, 0.7f), Target.Y);
        }

        protected override bool WantsToJump(in BotContext ctx) => false;
    }
}
