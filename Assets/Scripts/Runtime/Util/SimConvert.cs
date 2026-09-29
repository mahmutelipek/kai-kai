using UnityEngine;
using SVec2 = System.Numerics.Vector2;
using SVec3 = System.Numerics.Vector3;

namespace Game
{
    /// <summary>Conversions between System.Numerics (simulation) and UnityEngine vectors.</summary>
    public static class SimConvert
    {
        public static Vector3 ToUnity(this SVec3 v) => new Vector3(v.X, v.Y, v.Z);
        public static SVec3 ToSim(this Vector3 v) => new SVec3(v.x, v.y, v.z);
        public static Vector2 ToUnity(this SVec2 v) => new Vector2(v.X, v.Y);
        public static SVec2 ToSim(this Vector2 v) => new SVec2(v.x, v.y);

        /// <summary>Board-local deck point (sim X right, Y forward) at a height above the deck, as a Unity local position.</summary>
        public static Vector3 DeckPoint(SVec2 local, float height) => new Vector3(local.X, height, local.Y);

        /// <summary>Unity rotation for a sim pose. Sim: yaw right-positive, pitch nose-up-positive, roll right-side-down-positive.</summary>
        public static Quaternion PoseRotation(float yawRad, float pitchRad, float rollRad) =>
            Quaternion.Euler(-pitchRad * Mathf.Rad2Deg, yawRad * Mathf.Rad2Deg, -rollRad * Mathf.Rad2Deg);

        public static Vector3 YawForward(float yawRad) => new Vector3(Mathf.Sin(yawRad), 0f, Mathf.Cos(yawRad));
        public static Vector3 YawRight(float yawRad) => new Vector3(Mathf.Cos(yawRad), 0f, -Mathf.Sin(yawRad));
    }
}
