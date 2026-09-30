using System;
using System.Numerics;
using static Game.Art.ArtMath;

namespace Game.Art
{
    /// <summary>
    /// Procedural skate pose of a rider (shared by PlayerView and the headless preview): a deep crouch that bends
    /// further with speed, leans into turns, flails while the board wobbles and cheers after clean landings.
    /// Purely visual; nothing here feeds back into the simulation.
    /// </summary>
    public struct RiderPoseInput
    {
        /// <summary>Board speed / soft cap, 0..1.</summary>
        public float SpeedNorm;
        /// <summary>Board wobble 0..1.</summary>
        public float Wobble;
        /// <summary>Board roll (radians, + = right side down).</summary>
        public float BoardRoll;
        /// <summary>Board yaw rate (radians/s, + = turning right).</summary>
        public float YawRate;
        /// <summary>0..1 cheer amount (after a clean landing).</summary>
        public float Cheer;
        /// <summary>Stagger shake 0..1.</summary>
        public float Stagger;
        public float Time;
        public int Slot;
        /// <summary>Crouch pop while jumping: 1 = standing tall.</summary>
        public float Jump;
    }

    public struct RiderPoseOutput
    {
        /// <summary>Body roll around the feet (on top of the facing yaw).</summary>
        public Quaternion Pose;
        public Quaternion Torso, Head, ArmL, ArmR;
        /// <summary>Uniform-ish scale of the Pose group (x, y, z): crouch squash / jump stretch.</summary>
        public Vector3 Scale;
    }

    public static class RiderPose
    {
        public static RiderPoseOutput Compute(in RiderPoseInput i)
        {
            float phase = i.Slot * 1.7f;
            float bob = MathF.Sin(i.Time * 7f + phase) * (1.5f + 3f * i.SpeedNorm);

            // lean into the turn with the whole body, and against the board roll to stay upright
            float turnLeanDeg = Clamp(i.YawRate * 57.3f * 0.6f, -18f, 18f);
            float counterRoll = i.BoardRoll * 57.3f * 0.6f;
            float shake = i.Stagger > 0f ? MathF.Sin(i.Time * 40f) * 12f * i.Stagger : 0f;

            float torsoPitch = 18f + 16f * i.SpeedNorm + 10f * i.Wobble + bob;
            torsoPitch *= 1f - 0.8f * i.Cheer;
            torsoPitch = torsoPitch * (1f - i.Jump) + 4f * i.Jump;
            float torsoRoll = -turnLeanDeg * 0.5f;

            // arms: out for balance (more with speed), flail when wobbling, up when cheering
            float spread = 30f + 30f * i.SpeedNorm + 85f * i.Wobble;
            float flail = i.Wobble * MathF.Sin(i.Time * 18f + i.Slot) * 40f + i.Cheer * MathF.Sin(i.Time * 14f + i.Slot) * 15f;
            spread = spread + (160f - spread) * i.Cheer;
            float reach = -25f - 15f * i.SpeedNorm; // hands forward a little
            // the arm on the inside of the turn drops, the outside one rises
            float asym = turnLeanDeg * 1.2f;

            var o = new RiderPoseOutput
            {
                Pose = Euler(0f, 0f, -turnLeanDeg * 0.6f + counterRoll + shake),
                Torso = Euler(torsoPitch, 0f, torsoRoll),
                Head = Euler(-torsoPitch * 0.8f, MathF.Sin(i.Time * 0.9f + phase) * 8f, 0f),
                ArmL = Euler(reach * (1f - i.Cheer), 0f, -(spread + asym) - flail),
                ArmR = Euler(reach * (1f - i.Cheer), 0f, spread - asym - flail),
                Scale = Vector3.One,
            };
            return o;
        }

        /// <summary>Group rotation lookup for MeshSet.Add (preview baking).</summary>
        public static Func<string, Quaternion> AsLookup(RiderPoseOutput o) => g =>
            g == "Pose" ? o.Pose : g == "Torso" ? o.Torso : g == "Head" ? o.Head : g == "ArmL" ? o.ArmL : g == "ArmR" ? o.ArmR : Quaternion.Identity;

        static float Clamp(float v, float a, float b) => v < a ? a : (v > b ? b : v);
    }
}
