using System;
using System.Collections.Generic;
using System.Numerics;
using Game.Simulation;
using static Game.Art.ArtMath;

namespace Game.Art
{
    /// <summary>The colour palette of the game: bright, saturated, well separated from the grey road.</summary>
    public static class Pal
    {
        static ArtColor H(uint rgb, float smooth = 0.15f) => ArtColor.Hex(rgb, smooth);

        // road and street
        public static readonly ArtColor Asphalt = H(0x4B505B);
        public static readonly ArtColor AsphaltPatch = H(0x3D414B);
        public static readonly ArtColor LineWhite = H(0xF4F4EE);
        public static readonly ArtColor LineYellow = H(0xFFC62E);
        public static readonly ArtColor Curb = H(0xC9C3B6);
        public static readonly ArtColor Sidewalk = H(0xE4DCCB);
        public static readonly ArtColor Grass = H(0x7CC45A);
        public static readonly ArtColor GrassDark = H(0x5EA84A);
        public static readonly ArtColor Hill = H(0x9CCB63);
        public static readonly ArtColor Dirt = H(0xC9A46B);
        public static readonly ArtColor Concrete = H(0xCFCBC2);
        public static readonly ArtColor ConcreteDark = H(0x9C9890);
        public static readonly ArtColor BridgeRed = H(0xE0482C, 0.3f);
        public static readonly ArtColor TunnelInside = H(0x6A625A);
        public static readonly ArtColor RampWood = H(0xD99A55);
        public static readonly ArtColor RampWoodDark = H(0xA86E35);

        // hazard / props
        public static readonly ArtColor Orange = H(0xFF7A1A);
        public static readonly ArtColor White = H(0xF7F7F4);
        public static readonly ArtColor Red = H(0xE53935);
        public static readonly ArtColor Black = H(0x26272C);
        public static readonly ArtColor Metal = H(0xB8BEC8, 0.6f);
        public static readonly ArtColor MetalDark = H(0x6D737D, 0.5f);
        public static readonly ArtColor Pothole = H(0x1C1D22);
        public static readonly ArtColor Wood = H(0xC8904F);
        public static readonly ArtColor WoodDark = H(0x8A5A2B);
        public static readonly ArtColor Glass = H(0x2E4A66, 0.85f);
        public static readonly ArtColor Headlight = H(0xFFF3B0, 0.8f);
        public static readonly ArtColor Taillight = H(0xE02020, 0.6f);
        public static readonly ArtColor Tire = H(0x202125);
        public static readonly ArtColor Lamp = H(0xFFF1B8, 0.6f);
        public static readonly ArtColor Yellow = H(0xFFD21F);

        // pickups
        public static readonly ArtColor Gold = H(0xFFC21A, 0.85f);
        public static readonly ArtColor GoldLight = H(0xFFE27A, 0.85f);
        public static readonly ArtColor Diamond = H(0xB44CFF, 0.9f);
        public static readonly ArtColor DiamondLight = H(0xE3B2FF, 0.9f);
        public static readonly ArtColor Nitro = H(0x18C8FF, 0.85f);
        public static readonly ArtColor NitroLight = H(0x9FD4FF, 0.85f);

        // nature and buildings
        public static readonly ArtColor PalmTrunk = H(0xB0834F);
        public static readonly ArtColor PalmLeaf = H(0x3FAE4A);
        public static readonly ArtColor PalmLeafDark = H(0x2E8F3C);
        public static readonly ArtColor TreeLeaf = H(0x4FB548);
        public static readonly ArtColor Roof = H(0xD2693C);
        public static readonly ArtColor RoofDark = H(0xA9502D);
        public static readonly ArtColor Window = H(0x6FA8D8, 0.7f);
        public static readonly ArtColor Door = H(0x7A4A2B);
        public static readonly ArtColor Trim = H(0xFFFFFF);
        public static readonly ArtColor[] Walls =
        {
            H(0xFFD9A8), H(0xA8DDF0), H(0xFFB8C6), H(0xFFF0A8), H(0xC8E6B0), H(0xE8D0FF), H(0xFFFFFF),
        };

        // backdrop
        public static readonly ArtColor Sea = H(0x3AA7D8, 0.75f);
        public static readonly ArtColor SeaDeep = H(0x2B8CC4, 0.75f);
        public static readonly ArtColor FarHill = H(0x7FB28A);
        public static readonly ArtColor FarHill2 = H(0x9CC49A);
        public static readonly ArtColor Sand = H(0xF0DDB0);
        public static readonly ArtColor[] Towers = { H(0xF2F2F2), H(0xDCE3EA), H(0xC9D3DD), H(0xEDE3D2), H(0xB9C6D2) };

        /// <summary>Car paints, glossy.</summary>
        public static readonly ArtColor[] CarPaint =
        {
            H(0xFFC21A, 0.7f), H(0x2F7BEA, 0.7f), H(0xF4F4F4, 0.7f), H(0xE53935, 0.7f), H(0x3BBE6B, 0.7f), H(0x1FC4C4, 0.7f),
        };
    }

    /// <summary>
    /// Every model of the game built from primitives, engine-free so Unity and the headless preview render the
    /// same geometry. Units are metres, Unity axes (X right, Y up, Z forward). Models are cached.
    /// </summary>
    public static class ArtLibrary
    {
        static readonly Dictionary<string, ArtModel> Cache = new Dictionary<string, ArtModel>();

        static ArtModel Cached(string key, Func<ArtModel> build)
        {
            if (!Cache.TryGetValue(key, out ArtModel m)) Cache[key] = m = build();
            return m;
        }

        // ------------------------------------------------------------------ characters

        public enum Headwear { CapForward, SpikyHair, CapBackward, Dreads, Beanie, EarCap }

        public struct Outfit
        {
            public string Name;
            public ArtColor Skin, Shirt, Sleeve, Stripe, Pants, Shoe, ShoeAccent, Hair, Hat, HatAccent;
            public Headwear Headwear;
            public bool Hoodie, Shorts;
            public float Width;
        }

        public const int CharacterCount = 6;

        /// <summary>The six riders of the reference image (P1..P6 colours: blue, red, green, yellow, purple, orange).</summary>
        public static Outfit OutfitFor(int slot)
        {
            switch (((slot % CharacterCount) + CharacterCount) % CharacterCount)
            {
                case 0: return new Outfit
                {
                    Name = "P1 Blue Cap", Skin = ArtColor.Hex(0xF5CBA7), Shirt = Pal.White, Sleeve = ArtColor.Hex(0x2F6FE0), Stripe = ArtColor.Hex(0x2F6FE0),
                    Pants = ArtColor.Hex(0x3A5A8C), Shoe = Pal.White, ShoeAccent = ArtColor.Hex(0x2F6FE0), Hair = ArtColor.Hex(0x6B4226),
                    Hat = ArtColor.Hex(0x2F6FE0), HatAccent = Pal.White, Headwear = Headwear.CapForward, Width = 1f,
                };
                case 1: return new Outfit
                {
                    Name = "P2 Red Spikes", Skin = ArtColor.Hex(0xE3A878), Shirt = ArtColor.Hex(0xE8383D), Sleeve = ArtColor.Hex(0xE8383D), Stripe = Pal.White,
                    Pants = ArtColor.Hex(0x2A2A30), Shoe = ArtColor.Hex(0xD62828), ShoeAccent = Pal.White, Hair = ArtColor.Hex(0x1E1B1A),
                    Hat = ArtColor.Hex(0x1E1B1A), HatAccent = ArtColor.Hex(0x1E1B1A), Headwear = Headwear.SpikyHair, Shorts = true, Width = 0.95f,
                };
                case 2: return new Outfit
                {
                    Name = "P3 Green Hoodie", Skin = ArtColor.Hex(0xC68A5C), Shirt = ArtColor.Hex(0x33B249), Sleeve = ArtColor.Hex(0x33B249), Stripe = ArtColor.Hex(0x258A38),
                    Pants = ArtColor.Hex(0x6B7280), Shoe = ArtColor.Hex(0x2B2B2B), ShoeAccent = Pal.White, Hair = ArtColor.Hex(0x3B2416),
                    Hat = Pal.White, HatAccent = ArtColor.Hex(0x33B249), Headwear = Headwear.CapBackward, Hoodie = true, Width = 1.08f,
                };
                case 3: return new Outfit
                {
                    Name = "P4 Yellow Dreads", Skin = ArtColor.Hex(0x7A4B2E), Shirt = ArtColor.Hex(0xFFC928), Sleeve = ArtColor.Hex(0xFFC928), Stripe = ArtColor.Hex(0x2E9E4A),
                    Pants = ArtColor.Hex(0x4A6FA5), Shoe = Pal.White, ShoeAccent = Pal.Red, Hair = ArtColor.Hex(0x2B1A10),
                    Hat = Pal.Red, HatAccent = Pal.Red, Headwear = Headwear.Dreads, Shorts = true, Width = 1.02f,
                };
                case 4: return new Outfit
                {
                    Name = "P5 Purple Beanie", Skin = ArtColor.Hex(0xF2C7A5), Shirt = ArtColor.Hex(0x8E44D9), Sleeve = ArtColor.Hex(0x8E44D9), Stripe = Pal.White,
                    Pants = ArtColor.Hex(0x2C2F4A), Shoe = ArtColor.Hex(0x8E44D9), ShoeAccent = Pal.White, Hair = ArtColor.Hex(0xF5F5F5),
                    Hat = ArtColor.Hex(0x7B3FD1), HatAccent = Pal.White, Headwear = Headwear.Beanie, Width = 0.97f,
                };
                default: return new Outfit
                {
                    Name = "P6 Orange Bear", Skin = ArtColor.Hex(0xD9A06E), Shirt = ArtColor.Hex(0xFF8A1F), Sleeve = ArtColor.Hex(0xFF8A1F), Stripe = ArtColor.Hex(0xE06A00),
                    Pants = ArtColor.Hex(0xC9A66B), Shoe = ArtColor.Hex(0xFF8A1F), ShoeAccent = Pal.White, Hair = ArtColor.Hex(0x7A4A28),
                    Hat = Pal.White, HatAccent = ArtColor.Hex(0xFF8A1F), Headwear = Headwear.EarCap, Hoodie = true, Shorts = true, Width = 1.05f,
                };
            }
        }

        /// <summary>
        /// A rider on the shared rig: groups "Pose" (whole body, pivot at the feet), "ArmL" / "ArmR" (shoulders;
        /// arms hang down along -Y at rest and are raised by rotating around Z). Faces +Z. About 1.6 m tall.
        /// </summary>
        public static ArtModel Character(int slot) => Cached("Character" + slot, () => BuildCharacter(OutfitFor(slot)));

        static ArtModel BuildCharacter(Outfit o)
        {
            var m = new ArtModel(o.Name);
            const string P = "Pose";
            float w = o.Width;
            m.Group(P, Vector3.Zero);
            m.Group("ArmL", V(-0.25f * w, 0.95f, 0f), P);
            m.Group("ArmR", V(0.25f * w, 0.95f, 0f), P);

            // chunky sneakers
            for (int s = -1; s <= 1; s += 2)
            {
                float x = s * 0.16f;
                m.Add(ArtShape.Box, V(x, 0.035f, 0.05f), V(0.23f, 0.07f, 0.4f), Pal.White, "Sole", P);
                m.Add(ArtShape.Box, V(x, 0.12f, 0.02f), V(0.21f, 0.13f, 0.32f), o.Shoe, "Sneaker", P);
                m.Add(ArtShape.Sphere, V(x, 0.1f, 0.19f), V(0.21f, 0.15f, 0.16f), o.Shoe, "Toe", P);
                m.Add(ArtShape.Box, V(x + s * 0.106f, 0.12f, 0.03f), V(0.012f, 0.06f, 0.18f), o.ShoeAccent, "Stripe", P);
                m.Add(ArtShape.Box, V(x, 0.19f, -0.02f), V(0.16f, 0.04f, 0.22f), o.ShoeAccent, "Collar", P);
            }

            // crouched legs
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 ankle = V(s * 0.16f, 0.2f, 0.01f), knee = V(s * 0.19f, 0.37f, 0.19f), hip = V(s * 0.11f * w, 0.54f, -0.1f);
                if (o.Shorts)
                {
                    m.Limb(ankle, knee, 0.062f, o.Skin, "Shin", P);
                    m.Add(ArtShape.Cylinder, ankle + V(0f, 0.02f, 0f), V(0.13f, 0.06f, 0.13f), Pal.White, "Sock", P);
                    m.Limb(knee + V(0f, 0.04f, -0.01f), hip, 0.092f, o.Pants, "Shorts", P);
                }
                else
                {
                    m.Limb(ankle, knee, 0.08f, o.Pants, "Shin", P);
                    m.Limb(knee, hip, 0.09f, o.Pants, "Thigh", P);
                }
            }
            m.Add(ArtShape.Box, V(0f, 0.57f, -0.1f), V(0.4f * w, 0.17f, 0.28f), o.Pants, "Hips", P);

            // torso
            m.Add(ArtShape.Capsule, V(0f, 0.82f, -0.01f), V(0.48f * w, 0.58f, 0.33f), o.Shirt, "Torso", P);
            if (o.Hoodie)
            {
                m.Add(ArtShape.Sphere, V(0f, 1.04f, -0.15f), V(0.4f, 0.24f, 0.22f), o.Stripe, "Hood", P);
                m.Add(ArtShape.Box, V(0f, 0.72f, 0.155f), V(0.3f * w, 0.13f, 0.03f), o.Stripe, "Pocket", P);
                m.Add(ArtShape.Box, V(-0.05f, 0.93f, 0.165f), V(0.02f, 0.12f, 0.02f), Pal.White, "String", P);
                m.Add(ArtShape.Box, V(0.05f, 0.93f, 0.165f), V(0.02f, 0.12f, 0.02f), Pal.White, "String", P);
            }
            else
            {
                m.Add(ArtShape.Cylinder, V(0f, 0.86f, -0.01f), V(0.495f * w, 0.09f, 0.34f), o.Stripe, "ChestStripe", P);
            }
            m.Add(ArtShape.Cylinder, V(0f, 1.08f, 0f), V(0.15f, 0.1f, 0.15f), o.Skin, "Neck", P);

            // big head with a simple face
            const float hy = 1.34f;
            m.Add(ArtShape.Sphere, V(0f, hy, 0.01f), V(0.58f, 0.56f, 0.54f), o.Skin, "Head", P);
            for (int s = -1; s <= 1; s += 2)
            {
                m.Add(ArtShape.Sphere, V(s * 0.29f, hy - 0.02f, 0f), V(0.08f, 0.13f, 0.07f), o.Skin, "Ear", P);
                m.Add(ArtShape.Sphere, V(s * 0.1f, hy + 0.03f, 0.25f), V(0.075f, 0.11f, 0.05f), Pal.Black, "Eye", P);
                m.Add(ArtShape.Sphere, V(s * 0.1f + 0.018f, hy + 0.06f, 0.272f), V(0.028f, 0.028f, 0.02f), Pal.White, "Glint", P);
                m.Add(ArtShape.Box, V(s * 0.1f, hy + 0.13f, 0.245f), V(0.1f, 0.025f, 0.03f), o.Hair, "Brow", P, Euler(0f, 0f, s * -8f));
                m.Add(ArtShape.Sphere, V(s * 0.17f, hy - 0.06f, 0.225f), V(0.08f, 0.05f, 0.03f), ArtColor.Hex(0xFF8F80), "Cheek", P);
            }
            m.Add(ArtShape.Sphere, V(0f, hy - 0.03f, 0.27f), V(0.07f, 0.07f, 0.06f), o.Skin.Shade(0.93f), "Nose", P);
            m.Add(ArtShape.Sphere, V(0f, hy - 0.13f, 0.24f), V(0.14f, 0.06f, 0.04f), ArtColor.Hex(0x8A2A2A), "Mouth", P);
            m.Add(ArtShape.Box, V(0f, hy - 0.115f, 0.255f), V(0.09f, 0.015f, 0.02f), Pal.White, "Teeth", P);

            BuildHeadwear(m, o, hy);

            // arms: sleeve + forearm + hand, hanging down from the shoulder pivot
            for (int s = -1; s <= 1; s += 2)
            {
                string g = s < 0 ? "ArmL" : "ArmR";
                Vector3 elbow = V(s * 0.03f, -0.24f, 0f), wrist = V(s * 0.045f, -0.44f, 0.03f);
                m.Limb(V(0f, -0.02f, 0f), elbow, 0.078f, o.Sleeve, "Sleeve", g);
                if (o.Hoodie)
                {
                    m.Limb(elbow, wrist, 0.068f, o.Sleeve, "Forearm", g);
                    m.Add(ArtShape.Cylinder, wrist + V(0f, 0.01f, 0f), V(0.14f, 0.05f, 0.14f), o.Stripe, "Cuff", g);
                }
                else
                {
                    m.Limb(elbow, wrist, 0.058f, o.Skin, "Forearm", g);
                }
                m.Add(ArtShape.Sphere, wrist + V(0f, -0.07f, 0f), V(0.14f, 0.14f, 0.13f), o.Skin, "Hand", g);
            }
            Articulate(m);
            return m;
        }

        /// <summary>Torso pivot (hips) and head pivot (neck) of the rider rig, in "Pose" space.</summary>
        public static readonly Vector3 TorsoPivot = V(0f, 0.58f, -0.08f), HeadPivot = V(0f, 1.1f, 0f);

        /// <summary>
        /// Splits the body built in "Pose" space into the articulated rig: "Torso" (bends at the hips) holds the
        /// upper body and the arms, "Head" (turns at the neck) holds the head, face, hair and hat.
        /// </summary>
        static void Articulate(ArtModel m)
        {
            m.Group("Torso", TorsoPivot, "Pose");
            m.Group("Head", HeadPivot - TorsoPivot, "Torso");
            for (int i = 0; i < m.Parts.Count; i++)
            {
                ArtPart p = m.Parts[i];
                if (p.Group != "Pose") continue;
                if (p.Position.Y > HeadPivot.Y) { p.Group = "Head"; p.Position -= HeadPivot; }
                else if (p.Position.Y > 0.7f) { p.Group = "Torso"; p.Position -= TorsoPivot; }
                m.Parts[i] = p;
            }
            for (int i = 0; i < m.Groups.Count; i++)
            {
                ArtGroup g = m.Groups[i];
                if (g.Name == "ArmL" || g.Name == "ArmR") { g.Parent = "Torso"; g.Pivot -= TorsoPivot; }
                m.Groups[i] = g;
            }
        }

        static void BuildHeadwear(ArtModel m, Outfit o, float hy)
        {
            const string P = "Pose";
            switch (o.Headwear)
            {
                case Headwear.CapForward:
                    m.Add(ArtShape.Sphere, V(0f, hy + 0.05f, -0.03f), V(0.6f, 0.46f, 0.56f), o.Hair, "HairBack", P);
                    m.Add(ArtShape.Sphere, V(0f, hy + 0.12f, 0f), V(0.62f, 0.4f, 0.6f), o.Hat, "CapDome", P);
                    m.Add(ArtShape.Box, V(0f, hy + 0.11f, 0.35f), V(0.4f, 0.045f, 0.28f), o.Hat, "Brim", P, Euler(10f, 0f, 0f));
                    m.Add(ArtShape.Box, V(0f, hy + 0.2f, 0.27f), V(0.16f, 0.12f, 0.04f), o.HatAccent, "Badge", P, Euler(-25f, 0f, 0f));
                    m.Add(ArtShape.Sphere, V(0f, hy + 0.32f, 0f), V(0.07f, 0.05f, 0.07f), o.HatAccent, "Button", P);
                    break;

                case Headwear.SpikyHair:
                    m.Add(ArtShape.Sphere, V(0f, hy + 0.1f, -0.02f), V(0.62f, 0.44f, 0.58f), o.Hair, "Hair", P);
                    for (int i = 0; i < 9; i++)
                    {
                        float a = (i / 8f - 0.5f) * 140f;
                        float back = i % 2 == 0 ? 0f : -0.12f;
                        Quaternion r = Euler(-25f + back * 100f, 0f, -a * 0.55f);
                        Vector3 root = V(MathF.Sin(a * Deg) * 0.2f, hy + 0.25f, -0.02f + back);
                        m.Add(ArtShape.Cone, root + Vector3.Transform(V(0f, 0.13f, 0f), r), V(0.14f, 0.3f, 0.14f), o.Hair, "Spike", P, r);
                    }
                    break;

                case Headwear.CapBackward:
                    m.Add(ArtShape.Sphere, V(0f, hy + 0.03f, -0.05f), V(0.6f, 0.46f, 0.5f), o.Hair, "Hair", P);
                    m.Add(ArtShape.Sphere, V(0f, hy + 0.12f, -0.01f), V(0.62f, 0.4f, 0.6f), o.Hat, "CapDome", P);
                    m.Add(ArtShape.Box, V(0f, hy + 0.1f, -0.36f), V(0.38f, 0.045f, 0.26f), o.HatAccent, "Brim", P, Euler(10f, 0f, 0f));
                    m.Add(ArtShape.Box, V(0f, hy + 0.12f, -0.29f), V(0.16f, 0.07f, 0.03f), o.HatAccent, "Strap", P);
                    break;

                case Headwear.Dreads:
                    m.Add(ArtShape.Sphere, V(0f, hy + 0.1f, -0.01f), V(0.62f, 0.44f, 0.58f), o.Hair, "Hair", P);
                    m.Add(ArtShape.Cylinder, V(0f, hy + 0.1f, 0f), V(0.61f, 0.08f, 0.57f), o.Hat, "Headband", P);
                    for (int i = 0; i < 11; i++)
                    {
                        float a = (100f + i * 16f) * Deg; // around the sides and back
                        Vector3 dir = V(MathF.Sin(a), 0f, MathF.Cos(a));
                        Vector3 top = V(0f, hy + 0.2f, -0.01f) + dir * 0.24f;
                        Vector3 tip = V(0f, hy - 0.22f - (i % 3) * 0.05f, -0.03f) + dir * 0.36f;
                        m.Limb(top, tip, 0.045f, o.Hair, "Dread", P);
                    }
                    for (int s = -1; s <= 1; s += 2)
                        m.Limb(V(s * 0.3f, hy + 0.1f, -0.05f), V(s * 0.36f, hy + 0.02f, -0.18f), 0.03f, o.Hat, "BandTail", P);
                    break;

                case Headwear.Beanie:
                    for (int s = -1; s <= 1; s += 2)
                        m.Add(ArtShape.Sphere, V(s * 0.25f, hy - 0.02f, -0.06f), V(0.18f, 0.24f, 0.24f), o.Hair, "HairTuft", P);
                    m.Add(ArtShape.Sphere, V(0f, hy - 0.05f, -0.2f), V(0.44f, 0.26f, 0.2f), o.Hair, "HairBack", P);
                    m.Add(ArtShape.Sphere, V(0f, hy + 0.17f, -0.01f), V(0.62f, 0.52f, 0.6f), o.Hat, "Beanie", P);
                    m.Add(ArtShape.Cylinder, V(0f, hy + 0.08f, 0f), V(0.63f, 0.12f, 0.59f), o.Hat.Shade(0.8f), "Fold", P);
                    m.Add(ArtShape.Sphere, V(0f, hy + 0.47f, -0.01f), V(0.18f, 0.18f, 0.18f), o.HatAccent, "Pompom", P);
                    break;

                default: // EarCap: white cap with bear ears
                    m.Add(ArtShape.Sphere, V(0f, hy + 0.05f, -0.03f), V(0.6f, 0.46f, 0.56f), o.Hair, "HairBack", P);
                    m.Add(ArtShape.Sphere, V(0f, hy + 0.12f, 0f), V(0.62f, 0.4f, 0.6f), o.Hat, "CapDome", P);
                    m.Add(ArtShape.Box, V(0f, hy + 0.12f, 0.34f), V(0.4f, 0.045f, 0.26f), o.Hat, "Brim", P, Euler(10f, 0f, 0f));
                    for (int s = -1; s <= 1; s += 2)
                    {
                        m.Add(ArtShape.Sphere, V(s * 0.21f, hy + 0.3f, -0.02f), V(0.18f, 0.18f, 0.09f), o.Hat, "Ear", P);
                        m.Add(ArtShape.Sphere, V(s * 0.21f, hy + 0.3f, 0.02f), V(0.1f, 0.1f, 0.03f), o.HatAccent, "EarInner", P);
                    }
                    break;
            }
        }

        // ------------------------------------------------------------------ longboard

        /// <summary>
        /// Giant longboard: maple deck with kicktails, grip tape, red bands, metal trucks and big red wheels.
        /// Groups "Wheel0".."Wheel3" sit at the hubs (spin around X). Deck top at <paramref name="deckTop"/>.
        /// </summary>
        public static ArtModel Board(float width, float length, float deckTop, float wheelRadius) =>
            Cached($"Board{width:F2}x{length:F2}x{deckTop:F2}x{wheelRadius:F2}", () =>
            {
                var m = new ArtModel("Longboard");
                const float thick = 0.14f;
                float w = width, l = length, r = wheelRadius, y = deckTop - thick * 0.5f;
                float slab = l - w;
                ArtColor wood = ArtColor.Hex(0xE7B57A), woodSide = ArtColor.Hex(0xC98A4B), grip = ArtColor.Hex(0x2B2D35);
                m.Add(ArtShape.Box, V(0f, y, 0f), V(w, thick, slab), woodSide, "Deck");
                for (int s = -1; s <= 1; s += 2)
                {
                    m.Add(ArtShape.Cylinder, V(0f, y, s * slab * 0.5f), V(w, thick, w), woodSide, "End");
                    m.Add(ArtShape.Cylinder, V(0f, deckTop + 0.004f, s * slab * 0.5f), V(w * 0.93f, 0.012f, w * 0.93f), grip, "GripEnd");
                    for (int b = 0; b < 2; b++)
                        m.Add(ArtShape.Box, V(0f, deckTop + 0.012f, s * slab * 0.14f + b * 0.62f * s), V(w * 1.08f, 0.014f, 0.3f), Pal.Red, "Band", "", Euler(0f, 28f, 0f));
                }
                m.Add(ArtShape.Box, V(0f, deckTop + 0.004f, 0f), V(w * 0.93f, 0.012f, slab), grip, "Grip");
                m.Add(ArtShape.Box, V(0f, y - 0.02f, 0f), V(w * 1.005f, 0.04f, slab), Pal.Red, "SideStripe");

                float axleZ = l * 0.35f;
                int k = 0;
                for (int zs = -1; zs <= 1; zs += 2)
                {
                    float z = zs * axleZ;
                    float baseH = deckTop - thick - r + 0.1f;
                    m.Add(ArtShape.Box, V(0f, deckTop - thick - 0.03f, z), V(0.6f, 0.06f, 0.45f), Pal.MetalDark, "Baseplate");
                    m.Add(ArtShape.Box, V(0f, r + baseH * 0.5f, z), V(0.3f, baseH, 0.3f), Pal.Metal, "Kingpin");
                    m.Add(ArtShape.Box, V(0f, r, z), V(w * 0.82f, 0.14f, 0.18f), Pal.Metal, "Hanger");
                    for (int xs = -1; xs <= 1; xs += 2)
                    {
                        string g = "Wheel" + k++;
                        m.Group(g, V(xs * (w * 0.5f - 0.08f), r, z));
                        Quaternion side = Euler(0f, 0f, 90f);
                        m.Add(ArtShape.Cylinder, Vector3.Zero, V(r * 2f, 0.42f, r * 2f), ArtColor.Hex(0xE8322F, 0.5f), "Tire", g, side);
                        m.Add(ArtShape.Cylinder, V(xs * 0.02f, 0f, 0f), V(r * 0.9f, 0.44f, r * 0.9f), Pal.Metal, "Hub", g, side);
                        m.Add(ArtShape.Box, V(xs * 0.17f, 0f, 0f), V(0.02f, r * 1.3f, 0.1f), Pal.White, "Spoke", g);
                    }
                }
                return m;
            });

        // ------------------------------------------------------------------ obstacles

        /// <summary>Number of looks per obstacle kind (cars come in several paints).</summary>
        public static int ObstacleVariants(ObstacleKind kind) =>
            kind == ObstacleKind.ParkedCar || kind == ObstacleKind.MovingCar ? Pal.CarPaint.Length : 1;

        /// <summary>
        /// Obstacle look, footprint as in ObstacleCatalog.DefaultHalfExtents. Size-dependent kinds put their
        /// geometry in group "Scaled", which the view stretches to the actual footprint.
        /// </summary>
        public static ArtModel Obstacle(ObstacleKind kind, int variant = 0) =>
            Cached($"Obstacle{kind}{variant}", () => BuildObstacle(kind, variant));

        static ArtModel BuildObstacle(ObstacleKind kind, int variant)
        {
            var m = new ArtModel(kind.ToString());
            Vector2 half = ObstacleCatalog.DefaultHalfExtents(kind);
            switch (kind)
            {
                case ObstacleKind.Cone:
                    ConeProp(m, Vector3.Zero, 1f);
                    break;

                case ObstacleKind.ConcreteBarrier: // red / white plastic road barrier (jersey profile)
                {
                    m.Group("Scaled", Vector3.Zero);
                    int segments = 4;
                    float segW = half.X * 2f / segments;
                    for (int i = 0; i < segments; i++)
                    {
                        float x = -half.X + (i + 0.5f) * segW;
                        ArtColor c = i % 2 == 0 ? Pal.Red : Pal.White;
                        m.Add(ArtShape.Box, V(x, 0.15f, 0f), V(segW - 0.02f, 0.3f, 0.7f), c, "Base", "Scaled");
                        m.Add(ArtShape.Box, V(x, 0.4f, 0f), V(segW - 0.02f, 0.26f, 0.5f), c, "Mid", "Scaled");
                        m.Add(ArtShape.Box, V(x, 0.72f, 0f), V(segW - 0.02f, 0.38f, 0.3f), c, "Top", "Scaled");
                    }
                    m.Add(ArtShape.Box, V(0f, 0.93f, 0f), V(half.X * 2f, 0.04f, 0.26f), Pal.White, "Cap", "Scaled");
                    break;
                }

                case ObstacleKind.ParkedCar:
                case ObstacleKind.MovingCar:
                    Car(m, Pal.CarPaint[((variant % Pal.CarPaint.Length) + Pal.CarPaint.Length) % Pal.CarPaint.Length], variant == 0);
                    break;

                case ObstacleKind.ConstructionBarrier: // A-frame with orange/white striped boards and a warning light
                {
                    m.Group("Scaled", Vector3.Zero);
                    for (int row = 0; row < 2; row++)
                    {
                        float y = 0.55f + row * 0.3f;
                        int stripes = 7;
                        for (int i = 0; i < stripes; i++)
                        {
                            float x = -half.X + (i + 0.5f) * (half.X * 2f / stripes);
                            m.Add(ArtShape.Box, V(x, y, 0f), V(half.X * 2f / stripes + 0.001f, 0.2f, 0.07f), (i + row) % 2 == 0 ? Pal.Orange : Pal.White, "Board", "Scaled");
                        }
                    }
                    for (int s = -1; s <= 1; s += 2)
                    {
                        m.Add(ArtShape.Box, V(s * (half.X - 0.1f), 0.5f, 0.1f), V(0.08f, 1.0f, 0.08f), Pal.White, "Leg", "Scaled", Euler(12f, 0f, 0f));
                        m.Add(ArtShape.Box, V(s * (half.X - 0.1f), 0.5f, -0.1f), V(0.08f, 1.0f, 0.08f), Pal.White, "Leg", "Scaled", Euler(-12f, 0f, 0f));
                        m.Add(ArtShape.Box, V(s * (half.X - 0.1f), 0.04f, 0f), V(0.14f, 0.08f, 0.6f), Pal.Black, "Foot", "Scaled");
                    }
                    m.Add(ArtShape.Cylinder, V(-half.X + 0.25f, 1.1f, 0f), V(0.2f, 0.2f, 0.2f), Pal.Yellow.Glossy(0.8f), "Light", "Scaled");
                    break;
                }

                case ObstacleKind.Pothole: // dark hole in a broken light rim, circled with road-crew spray paint
                {
                    m.Group("Scaled", Vector3.Zero);
                    m.Add(ArtShape.Cylinder, V(0f, 0.012f, 0f), V(half.X * 2.9f, 0.02f, half.Y * 2.9f), Pal.Orange, "PaintRing", "Scaled");
                    m.Add(ArtShape.Cylinder, V(0f, 0.017f, 0f), V(half.X * 2.6f, 0.02f, half.Y * 2.6f), Pal.Concrete, "Rim", "Scaled");
                    m.Add(ArtShape.Cylinder, V(0f, 0.022f, 0f), V(half.X * 2f, 0.02f, half.Y * 2f), Pal.Pothole, "Hole", "Scaled");
                    for (int i = 0; i < 4; i++)
                    {
                        float a = i * 97f;
                        m.Add(ArtShape.Box, V(MathF.Sin(a * Deg) * half.X * 1.1f, 0.026f, MathF.Cos(a * Deg) * half.Y * 1.1f), V(0.08f, 0.02f, 0.45f), Pal.Pothole, "Crack", "Scaled", Euler(0f, a + 20f, 0f));
                    }
                    break;
                }

                case ObstacleKind.Divider:
                {
                    m.Group("Scaled", Vector3.Zero);
                    m.Add(ArtShape.Box, V(0f, 0.4f, 0f), V(half.X * 2f, 0.8f, half.Y * 2f), Pal.Concrete, "Wall", "Scaled");
                    m.Add(ArtShape.Box, V(0f, 0.85f, 0f), V(half.X * 2f + 0.02f, 0.1f, half.Y * 2f), Pal.Yellow, "Cap", "Scaled");
                    for (int i = 0; i < 9; i++)
                    {
                        float z = -half.Y + (i + 0.5f) * (half.Y * 2f / 9f);
                        for (int s = -1; s <= 1; s += 2)
                            m.Add(ArtShape.Box, V(s * (half.X + 0.005f), 0.55f, z), V(0.02f, 0.25f, 0.5f), i % 2 == 0 ? Pal.Black : Pal.Yellow, "Chevron", "Scaled");
                    }
                    break;
                }

                case ObstacleKind.Crate:
                    m.Add(ArtShape.Box, V(0f, 0.5f, 0f), V(1f, 1f, 1f), Pal.Wood, "Crate");
                    for (int s = -1; s <= 1; s += 2)
                    {
                        m.Add(ArtShape.Box, V(0f, 0.5f, s * 0.505f), V(1.04f, 0.14f, 0.03f), Pal.WoodDark, "Plank");
                        m.Add(ArtShape.Box, V(s * 0.505f, 0.5f, 0f), V(0.03f, 0.14f, 1.04f), Pal.WoodDark, "Plank");
                        m.Add(ArtShape.Box, V(0f, 0.5f, s * 0.51f), V(0.14f, 1.0f, 0.03f), Pal.WoodDark, "Plank", "", Euler(0f, 0f, 45f));
                        m.Add(ArtShape.Box, V(s * 0.51f, 0.5f, 0f), V(0.03f, 1.0f, 0.14f), Pal.WoodDark, "Plank", "", Euler(45f, 0f, 0f));
                    }
                    m.Add(ArtShape.Box, V(0f, 1.005f, 0f), V(1.04f, 0.03f, 0.14f), Pal.WoodDark, "Plank");
                    break;

                default: // BrokenPiece: a tilted slab of old asphalt with a hazard stripe and rubble
                    m.Add(ArtShape.Box, V(0f, 0.16f, 0f), V(1.6f, 0.3f, 1.2f), Pal.ConcreteDark, "Slab", "", Euler(12f, 0f, -8f));
                    m.Add(ArtShape.Box, V(0.03f, 0.315f, 0.03f), V(1.5f, 0.02f, 1.1f), Pal.AsphaltPatch, "Top", "", Euler(12f, 0f, -8f));
                    m.Add(ArtShape.Box, V(0f, 0.3f, -0.62f), V(1.4f, 0.1f, 0.05f), Pal.Yellow, "Edge", "", Euler(12f, 0f, -8f));
                    m.Add(ArtShape.Box, V(0.7f, 0.1f, 0.6f), V(0.35f, 0.2f, 0.3f), Pal.ConcreteDark, "Rubble", "", Euler(10f, 30f, 15f));
                    m.Add(ArtShape.Box, V(-0.75f, 0.08f, -0.5f), V(0.28f, 0.16f, 0.25f), Pal.Concrete, "Rubble", "", Euler(-12f, 70f, 8f));
                    break;
            }
            return m;
        }

        /// <summary>Traffic cone (0.75 m at scale 1) with its base at <paramref name="at"/>.</summary>
        static void ConeProp(ArtModel m, Vector3 at, float scale, string group = "")
        {
            m.Add(ArtShape.Box, at + V(0f, 0.025f, 0f) * scale, V(0.56f, 0.05f, 0.56f) * scale, Pal.Black, "Base", group);
            m.Add(ArtShape.Cone, at + V(0f, 0.39f, 0f) * scale, V(0.44f, 0.72f, 0.44f) * scale, Pal.Orange, "Body", group);
            m.Add(ArtShape.Cylinder, at + V(0f, 0.34f, 0f) * scale, V(0.27f, 0.09f, 0.27f) * scale, Pal.White, "Band", group);
            m.Add(ArtShape.Cylinder, at + V(0f, 0.52f, 0f) * scale, V(0.17f, 0.06f, 0.17f) * scale, Pal.White, "Band", group);
        }

        /// <summary>Compact car, 2.0 x 4.4 m, nose at +Z. A taxi gets a roof sign.</summary>
        static void Car(ArtModel m, ArtColor paint, bool taxi)
        {
            m.Add(ArtShape.Box, V(0f, 0.62f, 0f), V(1.96f, 0.62f, 4.3f), paint, "Body");
            m.Add(ArtShape.Box, V(0f, 0.72f, 2.12f), V(1.8f, 0.34f, 0.2f), paint, "Nose");
            m.Add(ArtShape.Box, V(0f, 1.2f, -0.35f), V(1.72f, 0.56f, 2.2f), Pal.Glass, "Cabin");
            m.Add(ArtShape.Box, V(0f, 1.5f, -0.4f), V(1.76f, 0.08f, 1.9f), paint, "Roof");
            for (int s = -1; s <= 1; s += 2)
            {
                m.Add(ArtShape.Box, V(s * 0.86f, 1.2f, 0.72f), V(0.07f, 0.56f, 0.1f), paint, "Pillar");
                m.Add(ArtShape.Box, V(s * 0.86f, 1.2f, -1.42f), V(0.07f, 0.56f, 0.12f), paint, "Pillar");
            }
            m.Add(ArtShape.Box, V(0f, 0.36f, 2.18f), V(2.0f, 0.2f, 0.16f), Pal.MetalDark, "Bumper");
            m.Add(ArtShape.Box, V(0f, 0.36f, -2.18f), V(2.0f, 0.2f, 0.16f), Pal.MetalDark, "Bumper");
            for (int x = -1; x <= 1; x += 2)
            {
                m.Add(ArtShape.Box, V(x * 0.66f, 0.72f, 2.16f), V(0.38f, 0.16f, 0.06f), Pal.Headlight, "Headlight");
                m.Add(ArtShape.Box, V(x * 0.7f, 0.74f, -2.16f), V(0.36f, 0.14f, 0.06f), Pal.Taillight, "Taillight");
                for (int z = -1; z <= 1; z += 2)
                {
                    Quaternion side = Euler(0f, 0f, 90f);
                    m.Add(ArtShape.Cylinder, V(x * 0.9f, 0.36f, z * 1.38f), V(0.72f, 0.26f, 0.72f), Pal.Tire, "Wheel", "", side);
                    m.Add(ArtShape.Cylinder, V(x * 0.92f, 0.36f, z * 1.38f), V(0.36f, 0.27f, 0.36f), Pal.Metal, "Rim", "", side);
                }
            }
            if (taxi)
            {
                m.Add(ArtShape.Box, V(0f, 1.64f, -0.3f), V(0.6f, 0.2f, 0.3f), Pal.White, "TaxiSign");
                m.Add(ArtShape.Box, V(0f, 0.7f, 0f), V(1.97f, 0.12f, 3.2f), Pal.Black, "Checker");
            }
        }

        // ------------------------------------------------------------------ pickups

        /// <summary>Pickup model centred on its origin (the view spins and bobs it).</summary>
        public static ArtModel Pickup(PickupKind kind) => Cached("Pickup" + kind, () =>
        {
            var m = new ArtModel(kind.ToString());
            switch (kind)
            {
                case PickupKind.Coin:
                {
                    Quaternion face = Euler(90f, 0f, 0f);
                    m.Add(ArtShape.Cylinder, Vector3.Zero, V(0.8f, 0.1f, 0.8f), Pal.Gold, "Coin", "", face);
                    m.Add(ArtShape.Cylinder, Vector3.Zero, V(0.56f, 0.13f, 0.56f), Pal.GoldLight, "Face", "", face);
                    m.Add(ArtShape.Box, Vector3.Zero, V(0.1f, 0.34f, 0.15f), Pal.Gold, "Mark");
                    break;
                }
                case PickupKind.Diamond:
                    m.Add(ArtShape.Octahedron, V(0f, -0.1f, 0f), V(0.75f, 0.9f, 0.75f), Pal.Diamond, "Gem");
                    m.Add(ArtShape.Cylinder, V(0f, 0.12f, 0f), V(0.62f, 0.14f, 0.62f), Pal.DiamondLight, "Crown");
                    break;
                default: // nitro bottle
                    m.Add(ArtShape.Capsule, V(0f, -0.05f, 0f), V(0.5f, 0.85f, 0.5f), Pal.Nitro, "Bottle");
                    m.Add(ArtShape.Cylinder, V(0f, -0.05f, 0f), V(0.52f, 0.2f, 0.52f), Pal.White, "Label");
                    m.Add(ArtShape.Box, V(0f, -0.05f, 0.26f), V(0.12f, 0.24f, 0.02f), Pal.Yellow, "Bolt", "", Euler(0f, 0f, 25f));
                    m.Add(ArtShape.Box, V(0f, -0.05f, -0.26f), V(0.12f, 0.24f, 0.02f), Pal.Yellow, "Bolt", "", Euler(0f, 0f, 25f));
                    m.Add(ArtShape.Cylinder, V(0f, 0.46f, 0f), V(0.2f, 0.18f, 0.2f), Pal.Metal, "Neck");
                    m.Add(ArtShape.Cylinder, V(0f, 0.58f, 0f), V(0.26f, 0.08f, 0.26f), Pal.Red, "Cap");
                    break;
            }
            return m;
        });

        // ------------------------------------------------------------------ props (baked into chunk meshes)

        public static ArtModel Palm(int variant) => Cached("Palm" + variant, () =>
        {
            var m = new ArtModel("Palm");
            var rng = new ArtRandom(variant * 7919 + 13);
            float height = 6.5f + rng.Range(0f, 3f);
            float lean = rng.Range(-8f, 8f);
            Vector3 p = Vector3.Zero;
            int segs = 5;
            for (int i = 0; i < segs; i++)
            {
                float t = (i + 1f) / segs;
                Vector3 next = V(MathF.Sin(lean * Deg) * height * t * t, height * t, 0f);
                m.Rod(p, next + (next - p) * 0.04f, 0.22f - 0.06f * t, i % 2 == 0 ? Pal.PalmTrunk : Pal.PalmTrunk.Shade(0.88f), "Trunk");
                p = next;
            }
            m.Add(ArtShape.Sphere, p, V(0.6f, 0.5f, 0.6f), Pal.PalmTrunk.Shade(0.75f), "Nuts");
            int leaves = 7;
            for (int i = 0; i < leaves; i++)
            {
                float a = i * 360f / leaves + rng.Range(-10f, 10f);
                Quaternion r = Euler(0f, a, 0f) * Euler(-rng.Range(5f, 25f), 0f, 0f);
                // leaf: a flattened box drooping outward in two segments
                Vector3 dir = Vector3.Transform(Vector3.UnitZ, r);
                Vector3 mid = p + dir * 1.4f + V(0f, 0.2f, 0f);
                Vector3 tip = p + dir * 2.7f + V(0f, -0.8f, 0f);
                AddLeaf(m, p, mid, i % 2 == 0 ? Pal.PalmLeaf : Pal.PalmLeafDark);
                AddLeaf(m, mid, tip, i % 2 == 0 ? Pal.PalmLeaf : Pal.PalmLeafDark);
            }
            return m;
        });

        static void AddLeaf(ArtModel m, Vector3 a, Vector3 b, ArtColor color)
        {
            Vector3 d = b - a;
            float len = d.Length();
            Quaternion r = FromTo(Vector3.UnitZ, d);
            m.Add(ArtShape.Box, (a + b) * 0.5f, V(0.75f, 0.06f, len + 0.1f), color, "Leaf", "", r);
        }

        public static ArtModel Tree(int variant) => Cached("Tree" + variant, () =>
        {
            var m = new ArtModel("Tree");
            var rng = new ArtRandom(variant * 104729 + 7);
            float h = 2.2f + rng.Range(0f, 1.2f);
            m.Add(ArtShape.Cylinder, V(0f, h * 0.5f, 0f), V(0.35f, h, 0.35f), Pal.PalmTrunk.Shade(0.8f), "Trunk");
            float s = 2.6f + rng.Range(0f, 1.2f);
            m.Add(ArtShape.Sphere, V(0f, h + s * 0.35f, 0f), V(s, s * 0.85f, s), Pal.TreeLeaf, "Crown");
            m.Add(ArtShape.Sphere, V(s * 0.3f, h + s * 0.15f, s * 0.15f), V(s * 0.6f, s * 0.5f, s * 0.6f), Pal.GrassDark, "Crown");
            return m;
        });

        public static ArtModel Bush(int variant) => Cached("Bush" + variant, () =>
        {
            var m = new ArtModel("Bush");
            var rng = new ArtRandom(variant * 31 + 3);
            for (int i = 0; i < 3; i++)
            {
                float s = 0.9f + rng.Range(0f, 0.6f);
                m.Add(ArtShape.Sphere, V(rng.Range(-0.6f, 0.6f), s * 0.35f, rng.Range(-0.4f, 0.4f)), V(s, s * 0.75f, s), i == 1 ? Pal.GrassDark : Pal.TreeLeaf, "Bush");
            }
            return m;
        });

        /// <summary>Pastel house with a terracotta roof, facing +Z (the road). Footprint about 8 x 7 m.</summary>
        public static ArtModel House(int variant) => Cached("House" + variant, () =>
        {
            var m = new ArtModel("House");
            var rng = new ArtRandom(variant * 6151 + 1);
            ArtColor wall = Pal.Walls[variant % Pal.Walls.Length];
            float w = 7f + rng.Range(0f, 3f), d = 6.5f + rng.Range(0f, 2f);
            int floors = 1 + (variant / Pal.Walls.Length) % 2 + (rng.Range(0f, 1f) > 0.6f ? 1 : 0);
            float h = floors * 3f;
            m.Add(ArtShape.Box, V(0f, -0.9f, 0f), V(w + 0.3f, 1.8f, d + 0.3f), Pal.Concrete, "Foundation");
            m.Add(ArtShape.Box, V(0f, h * 0.5f, 0f), V(w, h, d), wall, "Walls");
            // pitched roof: two tilted slabs + gable
            float pitch = 28f;
            float half = d * 0.5f + 0.5f;
            float slabLen = half / MathF.Cos(pitch * Deg);
            float rise = MathF.Tan(pitch * Deg) * half;
            for (int s = -1; s <= 1; s += 2)
                m.Add(ArtShape.Box, V(0f, h + rise * 0.5f, s * half * 0.5f), V(w + 0.8f, 0.22f, slabLen), s > 0 ? Pal.Roof : Pal.RoofDark, "Roof", "", Euler(s * pitch, 0f, 0f));
            m.Add(ArtShape.Wedge, V(0f, h + rise * 0.5f, d * 0.25f), V(w - 0.02f, rise, d * 0.5f), wall, "Gable", "", Euler(0f, 180f, 0f));
            m.Add(ArtShape.Wedge, V(0f, h + rise * 0.5f, -d * 0.25f), V(w - 0.02f, rise, d * 0.5f), wall, "Gable");
            // windows with white trim, a door
            for (int f = 0; f < floors; f++)
            {
                float y = f * 3f + 1.7f;
                int count = w > 8.5f ? 3 : 2;
                for (int i = 0; i < count; i++)
                {
                    float x = -w * 0.5f + (i + 0.5f) * (w / count);
                    if (f == 0 && i == count - 1) continue;
                    m.Add(ArtShape.Box, V(x, y, d * 0.5f + 0.02f), V(1.3f, 1.3f, 0.06f), Pal.Trim, "Frame");
                    m.Add(ArtShape.Box, V(x, y, d * 0.5f + 0.05f), V(1.05f, 1.05f, 0.04f), Pal.Window, "Window");
                }
            }
            float doorX = w * 0.5f - (w / (w > 8.5f ? 3 : 2)) * 0.5f;
            m.Add(ArtShape.Box, V(doorX, 1.1f, d * 0.5f + 0.03f), V(1.2f, 2.2f, 0.08f), Pal.Trim, "DoorFrame");
            m.Add(ArtShape.Box, V(doorX, 1.05f, d * 0.5f + 0.06f), V(0.95f, 2.05f, 0.06f), Pal.Door, "Door");
            m.Add(ArtShape.Box, V(-w * 0.25f, h + rise * 0.6f, -d * 0.2f), V(0.7f, 1.6f, 0.7f), Pal.Concrete, "Chimney");
            return m;
        });

        /// <summary>Flat-roofed apartment block, 3-5 floors, facing +Z.</summary>
        public static ArtModel Apartment(int variant) => Cached("Apartment" + variant, () =>
        {
            var m = new ArtModel("Apartment");
            var rng = new ArtRandom(variant * 977 + 5);
            ArtColor wall = Pal.Walls[(variant + 3) % Pal.Walls.Length];
            float w = 10f + rng.Range(0f, 4f), d = 9f;
            int floors = 3 + variant % 3;
            float h = floors * 3.1f;
            m.Add(ArtShape.Box, V(0f, -0.9f, 0f), V(w + 0.3f, 1.8f, d + 0.3f), Pal.Concrete, "Foundation");
            m.Add(ArtShape.Box, V(0f, h * 0.5f, 0f), V(w, h, d), wall, "Walls");
            m.Add(ArtShape.Box, V(0f, h + 0.25f, 0f), V(w + 0.4f, 0.5f, d + 0.4f), Pal.Trim, "Parapet");
            for (int f = 0; f < floors; f++)
            {
                float y = f * 3.1f + 1.8f;
                int count = (int)(w / 2.6f);
                for (int i = 0; i < count; i++)
                {
                    float x = -w * 0.5f + (i + 0.5f) * (w / count);
                    m.Add(ArtShape.Box, V(x, y, d * 0.5f + 0.03f), V(1.3f, 1.5f, 0.06f), Pal.Window, "Window");
                }
                if (f > 0) m.Add(ArtShape.Box, V(0f, f * 3.1f + 0.5f, d * 0.5f + 0.4f), V(w * 0.5f, 0.12f, 0.8f), Pal.Trim, "Balcony");
            }
            m.Add(ArtShape.Box, V(0f, h * 0.5f, d * 0.5f + 0.02f), V(0.3f, h, 0.06f), Pal.Trim, "Pilaster");
            return m;
        });

        /// <summary>Street light: pole with an arm reaching toward +X (the road) and a lamp.</summary>
        public static ArtModel StreetLight() => Cached("StreetLight", () =>
        {
            var m = new ArtModel("StreetLight");
            m.Add(ArtShape.Cylinder, V(0f, 0.2f, 0f), V(0.4f, 0.4f, 0.4f), Pal.MetalDark, "Foot");
            m.Add(ArtShape.Cylinder, V(0f, 3.2f, 0f), V(0.16f, 6.4f, 0.16f), Pal.MetalDark, "Pole");
            m.Add(ArtShape.Box, V(0.9f, 6.35f, 0f), V(1.8f, 0.1f, 0.1f), Pal.MetalDark, "Arm");
            m.Add(ArtShape.Box, V(1.75f, 6.25f, 0f), V(0.7f, 0.18f, 0.35f), Pal.MetalDark, "Head");
            m.Add(ArtShape.Box, V(1.75f, 6.15f, 0f), V(0.6f, 0.04f, 0.28f), Pal.Lamp, "Lamp");
            return m;
        });

        /// <summary>Yellow chevron curve sign on a post; arrows point toward +X when <paramref name="right"/>.</summary>
        public static ArtModel ChevronSign(bool right) => Cached("Chevron" + right, () =>
        {
            var m = new ArtModel("ChevronSign");
            m.Add(ArtShape.Box, V(0f, 0.8f, 0f), V(0.1f, 1.6f, 0.1f), Pal.Metal, "Post");
            m.Add(ArtShape.Box, V(0f, 1.9f, 0f), V(1.4f, 0.9f, 0.06f), Pal.Yellow, "Panel");
            float dir = right ? 1f : -1f;
            for (int i = -1; i <= 1; i++)
            {
                float x = i * 0.38f;
                m.Add(ArtShape.Box, V(x, 2.02f, 0.04f), V(0.1f, 0.4f, 0.02f), Pal.Black, "Chevron", "", Euler(0f, 0f, -dir * 40f));
                m.Add(ArtShape.Box, V(x, 1.78f, 0.04f), V(0.1f, 0.4f, 0.02f), Pal.Black, "Chevron", "", Euler(0f, 0f, dir * 40f));
            }
            return m;
        });

        /// <summary>Round road sign (speed / warning) on a post, facing +Z.</summary>
        public static ArtModel RoadSign(int variant) => Cached("RoadSign" + variant, () =>
        {
            var m = new ArtModel("RoadSign");
            m.Add(ArtShape.Cylinder, V(0f, 1.1f, 0f), V(0.09f, 2.2f, 0.09f), Pal.Metal, "Post");
            Quaternion face = Euler(90f, 0f, 0f);
            if (variant % 2 == 0)
            {
                m.Add(ArtShape.Cylinder, V(0f, 2.4f, 0.05f), V(0.8f, 0.05f, 0.8f), Pal.Red, "Ring", "", face);
                m.Add(ArtShape.Cylinder, V(0f, 2.4f, 0.07f), V(0.6f, 0.04f, 0.6f), Pal.White, "Face", "", face);
                m.Add(ArtShape.Box, V(-0.08f, 2.4f, 0.1f), V(0.08f, 0.3f, 0.02f), Pal.Black, "Digit");
                m.Add(ArtShape.Box, V(0.1f, 2.4f, 0.1f), V(0.16f, 0.3f, 0.02f), Pal.Black, "Digit");
            }
            else
            {
                m.Add(ArtShape.Box, V(0f, 2.4f, 0.05f), V(0.75f, 0.75f, 0.04f), Pal.Yellow, "Diamond", "", Euler(0f, 0f, 45f));
                m.Add(ArtShape.Box, V(0f, 2.45f, 0.08f), V(0.08f, 0.3f, 0.02f), Pal.Black, "Mark");
                m.Add(ArtShape.Box, V(0f, 2.22f, 0.08f), V(0.08f, 0.08f, 0.02f), Pal.Black, "Dot");
            }
            return m;
        });

        public static ArtModel Barrel() => Cached("Barrel", () =>
        {
            var m = new ArtModel("Barrel");
            m.Add(ArtShape.Cylinder, V(0f, 0.5f, 0f), V(0.62f, 1.0f, 0.62f), Pal.Orange, "Barrel");
            m.Add(ArtShape.Cylinder, V(0f, 0.35f, 0f), V(0.64f, 0.14f, 0.64f), Pal.White, "Band");
            m.Add(ArtShape.Cylinder, V(0f, 0.7f, 0f), V(0.64f, 0.14f, 0.64f), Pal.White, "Band");
            m.Add(ArtShape.Cylinder, V(0f, 0.05f, 0f), V(0.8f, 0.1f, 0.8f), Pal.Black, "Base");
            return m;
        });

        public static ArtModel ConeStack() => Cached("ConeProp", () =>
        {
            var m = new ArtModel("Cones");
            ConeProp(m, V(-0.5f, 0f, 0f), 0.9f);
            ConeProp(m, V(0.5f, 0f, 0.3f), 0.9f);
            return m;
        });

        public static ArtModel ParkedCarProp(int variant) => Cached("CarProp" + variant, () =>
        {
            var m = new ArtModel("CarProp");
            Car(m, Pal.CarPaint[variant % Pal.CarPaint.Length], false);
            return m;
        });

        public static ArtModel Fence(float length) => Cached($"Fence{length:F1}", () =>
        {
            var m = new ArtModel("Fence");
            int posts = Math.Max(2, (int)(length / 1.2f));
            for (int i = 0; i < posts; i++)
                m.Add(ArtShape.Box, V(0f, 0.5f, -length * 0.5f + i * length / (posts - 1)), V(0.1f, 1f, 0.1f), Pal.White, "Post");
            m.Add(ArtShape.Box, V(0f, 0.8f, 0f), V(0.06f, 0.1f, length), Pal.White, "Rail");
            m.Add(ArtShape.Box, V(0f, 0.45f, 0f), V(0.06f, 0.1f, length), Pal.White, "Rail");
            return m;
        });
    }

    /// <summary>Tiny allocation-free deterministic RNG (xorshift) for scenery placement.</summary>
    public struct ArtRandom
    {
        uint _s;

        public ArtRandom(int seed) { _s = (uint)seed * 2654435761u + 0x9E3779B9u; if (_s == 0) _s = 1; Next(); }

        public uint Next()
        {
            _s ^= _s << 13; _s ^= _s >> 17; _s ^= _s << 5;
            return _s;
        }

        public float Value() => (Next() & 0xFFFFFF) / 16777216f;
        public float Range(float min, float max) => min + (max - min) * Value();
        public int Range(int min, int maxExclusive) => min + (int)(Value() * (maxExclusive - min));
        public bool Chance(float p) => Value() < p;
    }
}
