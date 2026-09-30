using System;
using System.Collections.Generic;
using System.Numerics;

namespace Game.Art
{
    /// <summary>Primitive shapes every renderer (Unity, the headless three.js preview) can build identically.</summary>
    public enum ArtShape : byte
    {
        /// <summary>Unit cube scaled by Size.</summary>
        Box,
        /// <summary>Unit sphere (diameter 1) scaled by Size.</summary>
        Sphere,
        /// <summary>Cylinder along Y: diameter Size.X/Z, height Size.Y.</summary>
        Cylinder,
        /// <summary>Capsule along Y like Unity's primitive: total height Size.Y (scaled mesh).</summary>
        Capsule,
        /// <summary>Cone along Y: base diameter Size.X/Z, height Size.Y, centred at mid-height.</summary>
        Cone,
        /// <summary>Ramp wedge: width X, height Y, length Z, rising toward +Z, centred.</summary>
        Wedge,
        /// <summary>Double pyramid (gem): diameter X/Z, height Y.</summary>
        Octahedron,
    }

    /// <summary>Colour + smoothness. Colours are linear-ish sRGB 0..1 (as used by the Unity materials).</summary>
    public struct ArtColor : IEquatable<ArtColor>
    {
        public float R, G, B, Smoothness;

        public ArtColor(float r, float g, float b, float smoothness = 0.2f) { R = r; G = g; B = b; Smoothness = smoothness; }

        public static ArtColor Hex(uint rgb, float smoothness = 0.2f) =>
            new ArtColor(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, smoothness);

        public ArtColor Shade(float f) => new ArtColor(Math.Min(1f, R * f), Math.Min(1f, G * f), Math.Min(1f, B * f), Smoothness);
        public ArtColor Glossy(float smoothness) => new ArtColor(R, G, B, smoothness);
        /// <summary>Self-lit colour (lamps, nitro, gems): rendered with the emissive material so bloom picks it up.</summary>
        public ArtColor Glowing() => new ArtColor(R, G, B, 1f);
        public bool Emissive => Smoothness >= 0.999f;

        public bool Equals(ArtColor o) => R == o.R && G == o.G && B == o.B && Smoothness == o.Smoothness;
        public override bool Equals(object obj) => obj is ArtColor o && Equals(o);
        public override int GetHashCode() => (int)(R * 255) | ((int)(G * 255) << 8) | ((int)(B * 255) << 16) ^ (int)(Smoothness * 100) << 24;
        public string ToHex() => $"#{(int)(Clamp(R) * 255):X2}{(int)(Clamp(G) * 255):X2}{(int)(Clamp(B) * 255):X2}";
        static float Clamp(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }

    public struct ArtPart
    {
        /// <summary>Name of the group (pivot) this part hangs under; "" = model root.</summary>
        public string Group;
        public ArtShape Shape;
        /// <summary>Centre of the shape in its group's local space (metres, Unity axes: X right, Y up, Z forward).</summary>
        public Vector3 Position;
        public Vector3 Size;
        public Quaternion Rotation;
        public ArtColor Color;
        public string Name;
    }

    /// <summary>A named pivot inside a model (e.g. "ArmL" at the shoulder) so views can animate it.</summary>
    public struct ArtGroup
    {
        public string Name;
        public string Parent;
        public Vector3 Pivot;
    }

    /// <summary>A small articulated model made of primitives: characters, the board, obstacles, pickups.</summary>
    public sealed class ArtModel
    {
        public readonly string Name;
        public readonly List<ArtGroup> Groups = new List<ArtGroup>();
        public readonly List<ArtPart> Parts = new List<ArtPart>();

        public ArtModel(string name) { Name = name; }

        public ArtModel Group(string name, Vector3 pivot, string parent = "")
        {
            Groups.Add(new ArtGroup { Name = name, Parent = parent, Pivot = pivot });
            return this;
        }

        public ArtModel Add(ArtShape shape, Vector3 position, Vector3 size, ArtColor color, string name = null, string group = "", Quaternion? rotation = null)
        {
            Parts.Add(new ArtPart
            {
                Group = group ?? "",
                Shape = shape,
                Position = position,
                Size = size,
                Rotation = rotation ?? Quaternion.Identity,
                Color = color,
                Name = name ?? shape.ToString(),
            });
            return this;
        }

        /// <summary>A rounded limb (capsule) from joint a to joint b with the given radius.</summary>
        public ArtModel Limb(Vector3 a, Vector3 b, float radius, ArtColor color, string name, string group = "")
        {
            Vector3 d = b - a;
            float len = d.Length();
            return Add(ArtShape.Capsule, (a + b) * 0.5f, new Vector3(radius * 2f, len + radius * 2f, radius * 2f), color, name, group, ArtMath.FromTo(Vector3.UnitY, d));
        }
    }

    public static class ArtModelExtensions
    {
        /// <summary>A cylinder from point a to point b (cheaper than a limb: no rounded ends).</summary>
        public static ArtModel Rod(this ArtModel m, Vector3 a, Vector3 b, float radius, ArtColor color, string name, string group = "")
        {
            Vector3 d = b - a;
            return m.Add(ArtShape.Cylinder, (a + b) * 0.5f, new Vector3(radius * 2f, d.Length(), radius * 2f), color, name, group, ArtMath.FromTo(Vector3.UnitY, d));
        }
    }

    public static class ArtMath
    {
        public const float Deg = (float)(Math.PI / 180.0);

        /// <summary>Same numeric result as UnityEngine.Quaternion.Euler(x, y, z) (degrees; Z, then X, then Y).</summary>
        public static Quaternion Euler(float xDeg, float yDeg, float zDeg) =>
            Quaternion.CreateFromAxisAngle(Vector3.UnitY, yDeg * Deg)
            * Quaternion.CreateFromAxisAngle(Vector3.UnitX, xDeg * Deg)
            * Quaternion.CreateFromAxisAngle(Vector3.UnitZ, zDeg * Deg);

        /// <summary>Shortest rotation taking direction <paramref name="from"/> onto <paramref name="to"/>.</summary>
        public static Quaternion FromTo(Vector3 from, Vector3 to)
        {
            Vector3 f = Vector3.Normalize(from), t = Vector3.Normalize(to);
            float dot = Vector3.Dot(f, t);
            if (dot > 0.99999f) return Quaternion.Identity;
            if (dot < -0.99999f) return Quaternion.CreateFromAxisAngle(Math.Abs(f.X) < 0.9f ? Vector3.UnitX : Vector3.UnitZ, MathF.PI);
            Vector3 axis = Vector3.Normalize(Vector3.Cross(f, t));
            return Quaternion.CreateFromAxisAngle(axis, MathF.Acos(dot));
        }

        public static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
    }
}
