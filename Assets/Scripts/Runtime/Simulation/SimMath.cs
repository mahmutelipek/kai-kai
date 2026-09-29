using System;
using System.Numerics;

namespace Game.Simulation
{
    /// <summary>Small engine-independent math helpers (the simulation must compile without UnityEngine).</summary>
    public static class SimMath
    {
        public const float Deg2Rad = (float)(Math.PI / 180.0);
        public const float Rad2Deg = (float)(180.0 / Math.PI);
        public const float TwoPi = (float)(Math.PI * 2.0);

        public static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);

        public static bool IsFinite(Vector2 v) => IsFinite(v.X) && IsFinite(v.Y);

        public static bool IsFinite(Vector3 v) => IsFinite(v.X) && IsFinite(v.Y) && IsFinite(v.Z);

        public static float Clamp(float v, float min, float max) => v < min ? min : (v > max ? max : v);

        public static float Clamp01(float v) => Clamp(v, 0f, 1f);

        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);

        public static float InverseLerp(float a, float b, float v)
        {
            if (Math.Abs(b - a) < 1e-6f) return v >= b ? 1f : 0f;
            return Clamp01((v - a) / (b - a));
        }

        public static float SmoothStep(float edge0, float edge1, float v)
        {
            float t = InverseLerp(edge0, edge1, v);
            return t * t * (3f - 2f * t);
        }

        /// <summary>Exponential smoothing factor for a first-order lag with the given time constant.</summary>
        public static float LagAlpha(float dt, float timeConstant)
        {
            if (timeConstant <= 1e-5f) return 1f;
            return 1f - MathF.Exp(-dt / timeConstant);
        }

        /// <summary>sign(v) * |v|^exponent, the nonlinear response curve.</summary>
        public static float SignedPow(float v, float exponent)
        {
            if (v == 0f) return 0f;
            return MathF.Sign(v) * MathF.Pow(MathF.Abs(v), exponent);
        }

        /// <summary>Wraps an angle in radians to (-PI, PI].</summary>
        public static float WrapAngle(float radians)
        {
            while (radians > MathF.PI) radians -= TwoPi;
            while (radians <= -MathF.PI) radians += TwoPi;
            return radians;
        }

        /// <summary>World-space forward direction (x, z) for a yaw in radians. Yaw 0 = +Z, positive yaw turns right (clockwise from above).</summary>
        public static Vector2 HeadingToDirection(float yaw) => new Vector2(MathF.Sin(yaw), MathF.Cos(yaw));

        /// <summary>3D forward (XZ plane) for a yaw.</summary>
        public static Vector3 Forward3(float yaw) => new Vector3(MathF.Sin(yaw), 0f, MathF.Cos(yaw));

        /// <summary>3D right (XZ plane) for a yaw.</summary>
        public static Vector3 Right3(float yaw) => new Vector3(MathF.Cos(yaw), 0f, -MathF.Sin(yaw));
    }
}
