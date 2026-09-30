using System;
using Game.Art;

namespace Game.Hud
{
    public enum HudTex : byte
    {
        None, White, Panel, Pill, Circle, Arrow, Vignette, Heart, Coin, Gem,
        Avatar0, Avatar1, Avatar2, Avatar3, Avatar4, Avatar5,
    }

    /// <summary>RGBA8 image, rows top to bottom.</summary>
    public sealed class HudImage
    {
        public readonly int Width, Height;
        public readonly byte[] Rgba;
        public HudImage(int w, int h) { Width = w; Height = h; Rgba = new byte[w * h * 4]; }
    }

    /// <summary>
    /// Procedural HUD art in the style of the reference image: torn brush-stroke panels, rounded pills, a gold
    /// coin, a faceted purple gem, rider head avatars, arrows, a vignette. Engine-free so Unity and the headless
    /// preview use identical pixels. White images are tinted at draw time; icons carry their own colours.
    /// </summary>
    public static class HudTextures
    {
        static readonly HudImage[] Cache = new HudImage[Enum.GetValues(typeof(HudTex)).Length];

        public static HudImage Get(HudTex t)
        {
            int i = (int)t;
            if (Cache[i] != null) return Cache[i];
            HudImage img;
            switch (t)
            {
                case HudTex.White: img = Fill(4, 4, (x, y) => 1f); break;
                case HudTex.Panel: img = BrushPanel(512, 128, 7); break;
                case HudTex.Pill: img = Fill(256, 96, (x, y) => RoundRect(x, y, 256, 96, 48)); break;
                case HudTex.Circle: img = Fill(128, 128, (x, y) => Disc(x, y, 64, 64, 62)); break;
                case HudTex.Arrow: img = Fill(64, 48, (x, y) => Poly(x, y, new[] { 4f, 4f, 60f, 4f, 32f, 44f })); break;
                case HudTex.Vignette: img = VignetteImage(256); break;
                case HudTex.Heart: img = Fill(64, 64, (x, y) => Math.Max(Math.Max(Disc(x, y, 21, 24, 15), Disc(x, y, 43, 24, 15)), Poly(x, y, new[] { 7f, 28f, 57f, 28f, 32f, 58f }))); break;
                case HudTex.Coin: img = CoinImage(128); break;
                case HudTex.Gem: img = GemImage(128); break;
                default: img = AvatarImage(128, t - HudTex.Avatar0); break;
            }
            return Cache[i] = img;
        }

        // ------------------------------------------------------------------ shape helpers (coverage 0..1)

        const int SS = 3; // supersampling per axis

        static HudImage Fill(int w, int h, Func<float, float, float> coverage)
        {
            var img = new HudImage(w, h);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float a = Sample(x, y, coverage);
                int k = (y * w + x) * 4;
                img.Rgba[k] = img.Rgba[k + 1] = img.Rgba[k + 2] = 255;
                img.Rgba[k + 3] = (byte)(a * 255f + 0.5f);
            }
            return img;
        }

        static float Sample(int x, int y, Func<float, float, float> coverage)
        {
            float sum = 0f;
            for (int sy = 0; sy < SS; sy++)
            for (int sx = 0; sx < SS; sx++)
                sum += Math.Min(1f, coverage(x + (sx + 0.5f) / SS, y + (sy + 0.5f) / SS));
            return sum / (SS * SS);
        }

        static float Disc(float x, float y, float cx, float cy, float r) => (x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r ? 1f : 0f;

        static float RoundRect(float x, float y, float w, float h, float r)
        {
            float qx = Math.Max(Math.Abs(x - w * 0.5f) - (w * 0.5f - r), 0f), qy = Math.Max(Math.Abs(y - h * 0.5f) - (h * 0.5f - r), 0f);
            return qx * qx + qy * qy <= r * r ? 1f : 0f;
        }

        /// <summary>Point in polygon (x0, y0, x1, y1, ...), even-odd rule.</summary>
        static float Poly(float x, float y, float[] p)
        {
            bool inside = false;
            for (int i = 0, j = p.Length - 2; i < p.Length; j = i, i += 2)
            {
                float xi = p[i], yi = p[i + 1], xj = p[j], yj = p[j + 1];
                if ((yi > y) != (yj > y) && x < (xj - xi) * (y - yi) / (yj - yi) + xi) inside = !inside;
            }
            return inside ? 1f : 0f;
        }

        static float Hash(int n)
        {
            unchecked
            {
                uint h = (uint)n * 2654435761u;
                h ^= h >> 15; h *= 2246822519u; h ^= h >> 13;
                return (h & 0xFFFF) / 65535f;
            }
        }

        static float Noise1(float t, int seed)
        {
            int i = (int)Math.Floor(t);
            float f = t - i, u = f * f * (3f - 2f * f);
            return Hash(i + seed * 1013) * (1f - u) + Hash(i + 1 + seed * 1013) * u;
        }

        // ------------------------------------------------------------------ images

        /// <summary>Torn brush stroke: ragged ends, wavy top / bottom, faint bristle streaks.</summary>
        static HudImage BrushPanel(int w, int h, int seed)
        {
            var img = new HudImage(w, h);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float a = Sample(x, y, (px, py) =>
                {
                    float v = py / h;
                    float left = w * (0.02f + 0.07f * Noise1(v * 9f, seed) + 0.03f * Noise1(v * 31f, seed + 1));
                    float right = w * (0.98f - 0.08f * Noise1(v * 8f, seed + 2) - 0.03f * Noise1(v * 29f, seed + 3));
                    float top = h * (0.06f + 0.06f * Noise1(px / w * 6f, seed + 4));
                    float bottom = h * (0.94f - 0.06f * Noise1(px / w * 7f, seed + 5));
                    return px >= left && px <= right && py >= top && py <= bottom ? 1f : 0f;
                });
                float streak = 0.88f + 0.12f * Noise1(y * 0.7f, seed + 9);
                int k = (y * w + x) * 4;
                img.Rgba[k] = img.Rgba[k + 1] = img.Rgba[k + 2] = 255;
                img.Rgba[k + 3] = (byte)(a * streak * 255f + 0.5f);
            }
            return img;
        }

        static HudImage VignetteImage(int n)
        {
            var img = new HudImage(n, n);
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                float r = (float)Math.Sqrt(dx * dx * 0.8f + dy * dy * 1.1f);
                float a = Clamp01((r - 0.6f) / 0.55f);
                int k = (y * n + x) * 4;
                img.Rgba[k] = img.Rgba[k + 1] = img.Rgba[k + 2] = 255;
                img.Rgba[k + 3] = (byte)(a * a * 255f);
            }
            return img;
        }

        static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

        /// <summary>Paints layers back to front: each layer is a coverage function and a colour.</summary>
        static HudImage Layers(int w, int h, params (Func<float, float, float> cov, uint rgb)[] layers)
        {
            var img = new HudImage(w, h);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float r = 0f, g = 0f, b = 0f, a = 0f;
                foreach ((Func<float, float, float> cov, uint rgb) in layers)
                {
                    float c = Sample(x, y, cov);
                    if (c <= 0f) continue;
                    float lr = ((rgb >> 16) & 0xFF) / 255f, lg = ((rgb >> 8) & 0xFF) / 255f, lb = (rgb & 0xFF) / 255f;
                    r = r * (1f - c) + lr * c; g = g * (1f - c) + lg * c; b = b * (1f - c) + lb * c;
                    a = a + c * (1f - a);
                }
                int k = (y * w + x) * 4;
                // colours were composited over black; un-premultiply for straight alpha
                float inv = a > 1e-4f ? 1f / a : 0f;
                img.Rgba[k] = (byte)(Clamp01(r * inv) * 255f);
                img.Rgba[k + 1] = (byte)(Clamp01(g * inv) * 255f);
                img.Rgba[k + 2] = (byte)(Clamp01(b * inv) * 255f);
                img.Rgba[k + 3] = (byte)(a * 255f + 0.5f);
            }
            return img;
        }

        static HudImage CoinImage(int n)
        {
            float c = n * 0.5f;
            return Layers(n, n,
                ((x, y) => Disc(x, y, c, c, n * 0.48f), 0x8A5A00),
                ((x, y) => Disc(x, y, c, c, n * 0.44f), 0xE39A0A),
                ((x, y) => Disc(x, y, c - 2f, c - 2f, n * 0.42f), 0xFFC21A),
                ((x, y) => Disc(x, y, c, c, n * 0.32f), 0xF0A800),
                ((x, y) => Disc(x, y, c - 1f, c - 1f, n * 0.3f), 0xFFD84A),
                // lightning bolt
                ((x, y) => Poly(x, y, new[] { 0.56f * n, 0.2f * n, 0.36f * n, 0.54f * n, 0.5f * n, 0.54f * n, 0.43f * n, 0.8f * n, 0.65f * n, 0.44f * n, 0.51f * n, 0.44f * n }), 0xC77800),
                // highlight
                ((x, y) => Disc(x, y, c - n * 0.18f, c - n * 0.2f, n * 0.07f), 0xFFF3B8));
        }

        static HudImage GemImage(int n)
        {
            float f = n / 128f;
            float[] P(params float[] v) { for (int i = 0; i < v.Length; i++) v[i] *= f; return v; }
            return Layers(n, n,
                ((x, y) => Poly(x, y, P(24, 44, 44, 20, 84, 20, 104, 44, 64, 112)), 0x5A1A9E),       // outline
                ((x, y) => Poly(x, y, P(29, 45, 47, 24, 81, 24, 99, 45, 64, 106)), 0x9B3BF0),        // body
                ((x, y) => Poly(x, y, P(29, 45, 47, 24, 64, 45)), 0xC98BFF),                          // crown left
                ((x, y) => Poly(x, y, P(47, 24, 81, 24, 64, 45)), 0xE3B8FF),                          // crown mid
                ((x, y) => Poly(x, y, P(81, 24, 99, 45, 64, 45)), 0xB06BFF),                          // crown right
                ((x, y) => Poly(x, y, P(29, 45, 64, 45, 64, 106)), 0x7F2AD6),                         // pavilion left
                ((x, y) => Poly(x, y, P(56, 30, 62, 27, 60, 38)), 0xFFFFFF));                         // sparkle
        }

        /// <summary>Round head portrait of rider <paramref name="slot"/>: skin, eyes, smile and their headwear.</summary>
        static HudImage AvatarImage(int n, int slot)
        {
            ArtLibrary.Outfit o = ArtLibrary.OutfitFor(slot);
            uint U(ArtColor c) => (uint)(Palette.Byte(c.R) << 16 | Palette.Byte(c.G) << 8 | Palette.Byte(c.B));
            float f = n / 128f;
            uint skin = U(o.Skin), hat = U(o.Hat), accent = U(o.HatAccent), hair = U(o.Hair), outline = 0x1B1C24;
            float hx = 64 * f, hy = 72 * f, hr = 38 * f;
            var layers = new System.Collections.Generic.List<(Func<float, float, float>, uint)>();
            // hair / hat behind the head
            switch (o.Headwear)
            {
                case ArtLibrary.Headwear.Dreads:
                    for (int i = 0; i < 7; i++)
                    {
                        float dx = (i - 3) * 13f * f;
                        layers.Add(((x, y) => Poly(x, y, new[] { hx + dx - 6f * f, 40f * f, hx + dx + 6f * f, 40f * f, hx + dx * 1.35f + 4f * f, 112f * f, hx + dx * 1.35f - 6f * f, 112f * f }), hair));
                    }
                    break;
                case ArtLibrary.Headwear.Beanie:
                    layers.Add(((x, y) => Disc(x, y, hx - 30f * f, hy + 6f * f, 14f * f) + Disc(x, y, hx + 30f * f, hy + 6f * f, 14f * f), hair));
                    break;
            }
            layers.Add(((x, y) => Disc(x, y, hx, hy, hr + 3f * f), outline));
            layers.Add(((x, y) => Disc(x, y, hx - 36f * f, hy + 2f * f, 8f * f) + Disc(x, y, hx + 36f * f, hy + 2f * f, 8f * f), skin)); // ears
            layers.Add(((x, y) => Disc(x, y, hx, hy, hr), skin));
            // face
            layers.Add(((x, y) => Disc(x, y, hx - 13f * f, hy + 4f * f, 5f * f) + Disc(x, y, hx + 13f * f, hy + 4f * f, 5f * f), outline));
            layers.Add(((x, y) => Disc(x, y, hx - 11f * f, hy + 2f * f, 1.8f * f) + Disc(x, y, hx + 15f * f, hy + 2f * f, 1.8f * f), 0xFFFFFF));
            layers.Add(((x, y) => Disc(x, y, hx, hy + 17f * f, 9f * f) * (y > hy + 16f * f ? 1f : 0f), 0x8A2A2A));
            layers.Add(((x, y) => Disc(x, y, hx - 22f * f, hy + 13f * f, 5f * f) + Disc(x, y, hx + 22f * f, hy + 13f * f, 5f * f), 0xFF8F80));
            // headwear over the head
            float capTop = hy - hr - 4f * f;
            switch (o.Headwear)
            {
                case ArtLibrary.Headwear.CapForward:
                    layers.Add(((x, y) => Disc(x, y, hx, hy - 6f * f, hr + 3f * f) * (y < hy - 10f * f ? 1f : 0f), hat));
                    layers.Add(((x, y) => Poly(x, y, new[] { hx - 30f * f, hy - 12f * f, hx + 50f * f, hy - 16f * f, hx + 52f * f, hy - 6f * f, hx - 30f * f, hy - 4f * f }), hat));
                    layers.Add(((x, y) => Disc(x, y, hx, hy - 28f * f, 8f * f), accent));
                    break;
                case ArtLibrary.Headwear.CapBackward:
                    layers.Add(((x, y) => Disc(x, y, hx, hy - 6f * f, hr + 3f * f) * (y < hy - 12f * f ? 1f : 0f), hat));
                    layers.Add(((x, y) => Poly(x, y, new[] { hx - 52f * f, hy - 18f * f, hx - 20f * f, hy - 14f * f, hx - 20f * f, hy - 8f * f, hx - 50f * f, hy - 8f * f }), accent));
                    break;
                case ArtLibrary.Headwear.SpikyHair:
                    layers.Add(((x, y) => Disc(x, y, hx, hy - 8f * f, hr + 2f * f) * (y < hy - 10f * f ? 1f : 0f), hair));
                    for (int i = 0; i < 6; i++)
                    {
                        float sx = hx + (i - 2.5f) * 12f * f, tip = sx + (i - 2.5f) * 4f * f;
                        layers.Add(((x, y) => Poly(x, y, new[] { sx - 9f * f, hy - 30f * f, sx + 9f * f, hy - 30f * f, tip, capTop - 16f * f }), hair));
                    }
                    break;
                case ArtLibrary.Headwear.Dreads:
                    layers.Add(((x, y) => Disc(x, y, hx, hy - 8f * f, hr + 2f * f) * (y < hy - 12f * f ? 1f : 0f), hair));
                    layers.Add(((x, y) => (y > hy - 18f * f && y < hy - 10f * f && Disc(x, y, hx, hy, hr + 3f * f) > 0f) ? 1f : 0f, hat));
                    break;
                case ArtLibrary.Headwear.Beanie:
                    layers.Add(((x, y) => Disc(x, y, hx, hy - 10f * f, hr + 4f * f) * (y < hy - 8f * f ? 1f : 0f), hat));
                    layers.Add(((x, y) => (y > hy - 16f * f && y < hy - 6f * f && Disc(x, y, hx, hy - 10f * f, hr + 5f * f) > 0f) ? 1f : 0f, U(o.Hat.Shade(0.75f))));
                    layers.Add(((x, y) => Disc(x, y, hx, capTop - 14f * f, 10f * f), accent));
                    break;
                default: // bear-ear cap
                    layers.Add(((x, y) => Disc(x, y, hx - 26f * f, capTop + 2f * f, 12f * f) + Disc(x, y, hx + 26f * f, capTop + 2f * f, 12f * f), hat));
                    layers.Add(((x, y) => Disc(x, y, hx - 26f * f, capTop + 2f * f, 6f * f) + Disc(x, y, hx + 26f * f, capTop + 2f * f, 6f * f), accent));
                    layers.Add(((x, y) => Disc(x, y, hx, hy - 6f * f, hr + 3f * f) * (y < hy - 10f * f ? 1f : 0f), hat));
                    layers.Add(((x, y) => Poly(x, y, new[] { hx - 30f * f, hy - 12f * f, hx + 50f * f, hy - 16f * f, hx + 52f * f, hy - 6f * f, hx - 30f * f, hy - 4f * f }), accent));
                    break;
            }
            return Layers(n, n, layers.ToArray());
        }
    }
}
