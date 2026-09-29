using System;
using System.Numerics;

namespace Game.Simulation
{
    /// <summary>Complete, copyable board state (the unit a host will replicate in Milestone 5).</summary>
    public struct BoardState
    {
        /// <summary>World position of the board's ground contact point (X, height, Z).</summary>
        public Vector3 Position;
        /// <summary>Heading in radians. 0 = +Z, positive = turned right.</summary>
        public float Yaw;
        /// <summary>Yaw rate in rad/s (positive = turning right).</summary>
        public float YawRate;
        /// <summary>Direction the board is actually travelling. Differs from Yaw when grip is lost.</summary>
        public float TravelYaw;
        public float Speed;
        public float VerticalVelocity;
        /// <summary>Radians, positive = nose up.</summary>
        public float Pitch;
        /// <summary>Radians, positive = right side down (board leaning right).</summary>
        public float Roll;
        /// <summary>Roll caused by leaning into the turn (without wobble / tip).</summary>
        public float LeanRoll;
        /// <summary>Roll from uneven ground under the wheels (one side on a ramp). Visual only, not danger.</summary>
        public float GroundRoll;

        public float Lateral;
        public float Longitudinal;
        public float RawSteering;
        public float Steering;
        /// <summary>|Steering| scaled by speed/weight stability, divided by the crash threshold. 1 = crash zone.</summary>
        public float Danger;
        /// <summary>0..1, rises from wobbleStartFraction to 1 danger.</summary>
        public float Wobble;
        public float WobblePhase;
        /// <summary>1 = full grip.</summary>
        public float Grip;
        /// <summary>0..1 accumulator while danger >= 1; reaching 1 crashes the board.</summary>
        public float Tip;
        public float Acceleration;
        public float TargetSpeed;
        public float RunTime;
        public float LastGroundHeight;

        public bool Grounded;
        public SurfaceKind Surface;
        public bool Crashed;
        public float CrashTimer;
        public float CrashDirection;
    }

    public struct BoardStepEvents
    {
        public bool Crashed;
        public bool LeftGround;
        public bool Landed;
        public bool HardLanding;
        public float LandingSpeed;
    }

    /// <summary>
    /// The single authoritative board simulation:
    /// weight result -> smoothed steering -> target yaw rate -> yaw torque (first-order) -> heading,
    /// plus speed, traction, danger/wobble/grip loss/crash, roll and ground following.
    /// Board rotation is never set from input; it is integrated from yaw rate.
    /// </summary>
    public sealed class BoardPhysicsSim
    {
        public BoardState State;
        /// <summary>Raises the soft speed cap (set each step from difficulty; 1 = tuning value).</summary>
        public float SpeedCapMultiplier = 1f;

        const float WobbleYawRateDeg = 8f;
        const float SlipSpeedLoss = 0.6f;
        const float GroundStickTolerance = 0.02f;
        const float MaxVerticalSpeed = 40f;
        const float MaxSnapDrop = 0.5f;

        // After a reset or a landing the vertical velocity does not yet match the ground slope,
        // so the next grounded step snaps to the ground instead of running the take-off test.
        bool _snapToGround = true;

        public void Reset(Vector3 position, float yaw, float speed, float runTime = 0f)
        {
            State = new BoardState
            {
                Position = position,
                Yaw = yaw,
                TravelYaw = yaw,
                Speed = speed,
                Grip = 1f,
                Grounded = true,
                RunTime = runTime,
                TargetSpeed = speed,
                LastGroundHeight = position.Y,
            };
            _snapToGround = true;
        }

        /// <summary>Obstacle hit. speedLossFraction 0..1; yawKick in rad/s added to the yaw rate.</summary>
        public void ApplyImpact(float speedLossFraction, float yawKick)
        {
            if (State.Crashed) return;
            State.Speed *= 1f - SimMath.Clamp01(speedLossFraction);
            State.YawRate += yawKick;
        }

        /// <summary>Collision response: move the board, replace its velocity, nudge the heading. Never used for steering.</summary>
        public void Deflect(Vector2 positionOffset, Vector2 newVelocity, float yawKick)
        {
            if (State.Crashed) return;
            State.Position.X += positionOffset.X;
            State.Position.Z += positionOffset.Y;
            float speed = newVelocity.Length();
            if (speed > 0.1f)
            {
                State.TravelYaw = MathF.Atan2(newVelocity.X, newVelocity.Y);
                State.Yaw = SimMath.WrapAngle(State.Yaw + SimMath.WrapAngle(State.TravelYaw - State.Yaw) * 0.35f);
            }
            State.Speed = speed;
            State.YawRate += yawKick;
        }

        public void ForceCrash()
        {
            if (State.Crashed) return;
            State.Crashed = true;
            State.CrashTimer = 0f;
            State.CrashDirection = State.Steering >= 0f ? 1f : -1f;
        }

        public BoardStepEvents Step(float dt, in WeightResult weight, BoardTuningData t, IGroundProvider ground)
        {
            var ev = new BoardStepEvents();
            if (!(dt > 0f)) return ev;
            ref BoardState s = ref State;

            s.Lateral = weight.Lateral;
            s.Longitudinal = weight.Longitudinal;
            s.RawSteering = weight.RawSteering;

            if (s.Crashed)
            {
                StepCrashed(dt, t, ground);
                return ev;
            }

            s.RunTime += dt;
            float cap = t.softCapSpeed * Math.Max(SpeedCapMultiplier, 0.1f);
            s.TargetSpeed = Math.Min(t.startSpeed + t.speedRampPerSecond * s.RunTime, cap);

            // 1) smoothed steering: heavy, delayed, momentum-like
            s.Steering += (weight.RawSteering - s.Steering) * SimMath.LagAlpha(dt, t.steeringSmoothingTime);

            // 2) danger: speed and front weight lower stability
            float speedNorm = SimMath.Clamp01(s.Speed / Math.Max(t.stabilityReferenceSpeed, 1e-3f));
            float stability = SimMath.Lerp(t.stabilityFactorLowSpeed, t.stabilityFactorHighSpeed, speedNorm)
                              * (1f + t.frontWeightInstability * Math.Max(0f, s.Longitudinal) * speedNorm);
            s.Danger = Math.Abs(s.Steering) * stability / t.crashThreshold;
            s.Wobble = SimMath.SmoothStep(t.wobbleStartFraction, 1f, s.Danger);
            s.Grip = 1f - t.gripLossMax * s.Wobble;
            s.WobblePhase = SimMath.WrapAngle(s.WobblePhase + SimMath.TwoPi * t.wobbleFrequencyHz * (0.8f + 0.4f * s.Wobble) * dt);
            float wobbleWave = MathF.Sin(s.WobblePhase);

            // 3) yaw: steering -> target yaw rate (stronger at speed) -> torque-like first-order response
            if (s.Grounded)
            {
                float maxYawRate = (t.yawRateBaseDeg + t.yawRatePerSpeedDeg * s.Speed) * SimMath.Deg2Rad;
                float targetYawRate = s.Steering * maxYawRate + s.Wobble * WobbleYawRateDeg * SimMath.Deg2Rad * wobbleWave;
                s.YawRate += (targetYawRate - s.YawRate) * SimMath.LagAlpha(dt, t.yawResponseTime);
            }
            else
            {
                s.YawRate *= 1f - SimMath.LagAlpha(dt, 1.5f); // slow bleed in the air
            }
            s.Yaw = SimMath.WrapAngle(s.Yaw + s.YawRate * dt);

            // 4) traction: velocity direction follows the heading; lost grip = slide
            float slip = SimMath.WrapAngle(s.Yaw - s.TravelYaw);
            if (s.Grounded)
            {
                float alignRate = Math.Max(t.tractionAlignRate * s.Grip, 0.1f);
                s.TravelYaw = SimMath.WrapAngle(s.TravelYaw + slip * SimMath.LagAlpha(dt, 1f / alignRate));
            }

            // 5) speed
            if (s.Grounded)
            {
                float fade = 1f - SimMath.SmoothStep(cap * (1f - t.frontAccelFadeAboveCap),
                                                     cap * (1f + t.frontAccelFadeAboveCap), s.Speed);
                // weight on the tail both brakes and suppresses the downhill pull, so braking works at any speed
                float rear = Math.Max(0f, -s.Longitudinal);
                float cruise = t.cruiseGain * (s.TargetSpeed - s.Speed);
                if (cruise > 0f) cruise *= 1f - rear;
                float accel = cruise
                              + t.frontAcceleration * Math.Max(0f, s.Longitudinal) * fade
                              - t.rearBraking * Math.Max(0f, -s.Longitudinal)
                              - (s.Surface == SurfaceKind.Offroad ? t.offroadDrag : 0f)
                              - SlipSpeedLoss * MathF.Abs(MathF.Sin(slip)) * s.Speed;
                float newSpeed = Math.Max(s.Speed + accel * dt, Math.Min(t.minSpeed, s.Speed));
                s.Acceleration = (newSpeed - s.Speed) / dt;
                s.Speed = newSpeed;
            }
            else
            {
                s.Acceleration = 0f;
            }

            // 6) horizontal integration
            Vector2 travel = SimMath.HeadingToDirection(s.TravelYaw);
            s.Position.X += travel.X * s.Speed * dt;
            s.Position.Z += travel.Y * s.Speed * dt;

            // 7) vertical: follow the ground, launch off ramps, land
            StepVertical(dt, t, ground, ref ev);

            // 8) roll: lean into the turn + wobble + tipping
            float leanTarget = s.Steering * t.maxRollDeg * SimMath.Deg2Rad;
            s.LeanRoll += (leanTarget - s.LeanRoll) * SimMath.LagAlpha(dt, t.rollResponseTime);
            if (s.Danger >= 1f) s.Tip += dt / Math.Max(t.crashTipTime, 1e-3f);
            else s.Tip -= dt / Math.Max(t.tipRecoveryTime, 1e-3f);
            s.Tip = SimMath.Clamp01(s.Tip);
            s.Roll = s.LeanRoll + s.GroundRoll
                     + s.Wobble * t.wobbleMaxRollDeg * SimMath.Deg2Rad * wobbleWave
                     + MathF.Sign(s.Steering) * s.Tip * t.tipExtraRollDeg * SimMath.Deg2Rad;

            if (s.Tip >= 1f)
            {
                ForceCrash();
                ev.Crashed = true;
            }
            if (!SimMath.IsFinite(s.Position) || !SimMath.IsFinite(s.Yaw) || !SimMath.IsFinite(s.Speed))
            {
                throw new InvalidOperationException("BoardPhysicsSim produced a non-finite state");
            }
            return ev;
        }

        void StepVertical(float dt, BoardTuningData t, IGroundProvider ground, ref BoardStepEvents ev)
        {
            ref BoardState s = ref State;
            Vector2 fwd = SimMath.HeadingToDirection(s.Yaw);
            var right = new Vector2(fwd.Y, -fwd.X);
            float axle = t.boardLength * 0.35f;
            float track = t.HalfWidth * 0.8f;
            float probeFrom = s.Position.Y + 3f;
            // four wheel contacts: a board half on a ramp rides up on that side and tilts
            GroundSample fl = SampleAt(ground, s.Position, fwd * axle - right * track, probeFrom);
            GroundSample fr = SampleAt(ground, s.Position, fwd * axle + right * track, probeFrom);
            GroundSample rl = SampleAt(ground, s.Position, -fwd * axle - right * track, probeFrom);
            GroundSample rr = SampleAt(ground, s.Position, -fwd * axle + right * track, probeFrom);
            GroundSample front = Pair(fl, fr), rear = Pair(rl, rr);
            GroundSample left = Pair(fl, rl), rightSide = Pair(fr, rr);
            float groundRollTarget = left.Found && rightSide.Found ? MathF.Atan2(left.Height - rightSide.Height, 2f * track) : 0f;
            s.GroundRoll += (groundRollTarget - s.GroundRoll) * SimMath.LagAlpha(dt, 0.06f);

            bool found = front.Found || rear.Found;
            float groundY = front.Found && rear.Found ? (front.Height + rear.Height) * 0.5f
                          : front.Found ? front.Height : rear.Height;

            if (s.Grounded)
            {
                float predicted = s.Position.Y + s.VerticalVelocity * dt - 0.5f * t.gravity * dt * dt;
                float lowestStickHeight = _snapToGround ? s.Position.Y - MaxSnapDrop : predicted - GroundStickTolerance;
                if (found && groundY >= lowestStickHeight)
                {
                    _snapToGround = false;
                    s.VerticalVelocity = SimMath.Clamp((groundY - s.Position.Y) / dt, -MaxVerticalSpeed, MaxVerticalSpeed);
                    s.Position.Y = groundY;
                    s.LastGroundHeight = groundY;
                    s.Surface = front.Found ? front.Surface : rear.Surface;
                    if (front.Found && rear.Found)
                    {
                        float target = MathF.Atan2(front.Height - rear.Height, 2f * axle);
                        s.Pitch += (target - s.Pitch) * SimMath.LagAlpha(dt, 0.05f);
                    }
                    return;
                }
                s.Grounded = false;
                ev.LeftGround = true;
            }

            s.VerticalVelocity = Math.Max(s.VerticalVelocity - t.gravity * dt, -MaxVerticalSpeed);
            s.Position.Y += s.VerticalVelocity * dt;
            float airPitch = MathF.Atan2(s.VerticalVelocity, Math.Max(s.Speed, 1f)) * 0.5f;
            s.Pitch += (airPitch - s.Pitch) * SimMath.LagAlpha(dt, 0.4f);

            if (found && s.Position.Y <= groundY)
            {
                ev.Landed = true;
                ev.LandingSpeed = -s.VerticalVelocity;
                ev.HardLanding = ev.LandingSpeed > t.hardLandingSpeed;
                s.Position.Y = groundY;
                s.VerticalVelocity = 0f;
                s.Grounded = true;
                s.LastGroundHeight = groundY;
                _snapToGround = true;
            }
            else if (s.Position.Y < s.LastGroundHeight - t.fallOutOfWorldDepth)
            {
                ForceCrash();
                ev.Crashed = true;
            }
        }

        static GroundSample SampleAt(IGroundProvider ground, Vector3 center, Vector2 offset, float probeFrom) =>
            ground.Sample(center.X + offset.X, center.Z + offset.Y, probeFrom);

        /// <summary>Average of two contacts (either one if the other has no ground under it).</summary>
        static GroundSample Pair(in GroundSample a, in GroundSample b)
        {
            if (a.Found && b.Found)
                return new GroundSample { Found = true, Height = (a.Height + b.Height) * 0.5f, Surface = a.Surface == SurfaceKind.Offroad || b.Surface == SurfaceKind.Offroad ? SurfaceKind.Offroad : SurfaceKind.Road };
            return a.Found ? a : b;
        }

        void StepCrashed(float dt, BoardTuningData t, IGroundProvider ground)
        {
            ref BoardState s = ref State;
            s.CrashTimer += dt;
            s.Speed = Math.Max(0f, s.Speed - t.crashDeceleration * dt);
            s.YawRate *= 1f - SimMath.LagAlpha(dt, 0.3f);
            s.Yaw = SimMath.WrapAngle(s.Yaw + s.YawRate * dt);
            s.TravelYaw = SimMath.WrapAngle(s.TravelYaw + SimMath.WrapAngle(s.Yaw - s.TravelYaw) * SimMath.LagAlpha(dt, 0.5f));
            s.Wobble = 0f;
            s.Grip = 0f;
            s.Tip = 1f;
            float flipTarget = s.CrashDirection * 110f * SimMath.Deg2Rad;
            s.Roll += (flipTarget - s.Roll) * SimMath.LagAlpha(dt, 0.25f);

            Vector2 travel = SimMath.HeadingToDirection(s.TravelYaw);
            s.Position.X += travel.X * s.Speed * dt;
            s.Position.Z += travel.Y * s.Speed * dt;

            GroundSample g = ground.Sample(s.Position.X, s.Position.Z, s.Position.Y + 3f);
            if (g.Found && s.Position.Y <= g.Height + 0.01f)
            {
                s.Position.Y = g.Height;
                s.VerticalVelocity = 0f;
                s.Grounded = true;
            }
            else
            {
                s.Grounded = false;
                s.VerticalVelocity = Math.Max(s.VerticalVelocity - t.gravity * dt, -MaxVerticalSpeed);
                s.Position.Y += s.VerticalVelocity * dt;
                if (g.Found && s.Position.Y < g.Height) { s.Position.Y = g.Height; s.VerticalVelocity = 0f; }
            }
        }
    }
}
