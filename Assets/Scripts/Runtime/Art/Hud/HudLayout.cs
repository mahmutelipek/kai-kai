using System;
using System.Collections.Generic;
using Game.Frontend;

namespace Game.Hud
{
    public struct HudColor
    {
        public float R, G, B, A;
        public HudColor(float r, float g, float b, float a = 1f) { R = r; G = g; B = b; A = a; }
        public static HudColor Hex(uint rgb, float a = 1f) => new HudColor(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, a);
        public HudColor WithAlpha(float a) => new HudColor(R, G, B, a);
        public static readonly HudColor White = new HudColor(1f, 1f, 1f);
    }

    public enum HudAlign : byte { Left, Center, Right }

    /// <summary>
    /// One draw command, screen pixels with the origin top-left (IMGUI convention). Rotation (degrees, clockwise
    /// on screen), skew (x += skew * y) and scale are applied around the rect centre.
    /// </summary>
    public struct HudCmd
    {
        public HudTex Tex;
        public string Text;
        public float X, Y, W, H;
        public float Rotation, Skew, Scale;
        public HudColor Color;
        public float FontSize;
        public HudAlign Align;
        /// <summary>Dark outline thickness for text, pixels.</summary>
        public float Outline;
        public HudColor OutlineColor;
    }

    public struct HudPopup
    {
        public string Text;
        public HudColor Color;
        public float Age;
    }

    /// <summary>Everything the HUD shows, filled by the Unity HUD (strings are cached there, not rebuilt per frame).</summary>
    public sealed class HudState
    {
        public float Time;
        public string Distance = "0 m", Best = "0 m", Coins = "0", Diamonds = "0", Score = "0";
        public string Combo = "x1";
        public bool ShowCombo;
        public float ComboFill, ComboPop, CoinPop, DiamondPop;
        public int NitroCharges;
        public bool NitroActive;
        public string NitroText = "NITRO READY!";
        public string Speed = "0";
        public float SpeedFill, Danger;
        public int Players = 1;
        public readonly bool[] OnBoard = new bool[6];
        public readonly bool[] Human = new bool[6];
        public int LivesLeft, LivesMax;
        public string Countdown;
        public float CountdownAge;
        public readonly List<HudPopup> Popups = new List<HudPopup>(8);
        public bool DebugPanelOpen;

        // speed feel (M4.3): screen-edge wind streaks and flashes
        /// <summary>0..1 wind streak strength (speed, acceleration, nitro, boost, slipstream).</summary>
        public float Wind;
        /// <summary>Streak tint: 0 white, 1 nitro cyan; Boost tints orange.</summary>
        public float WindNitro, WindBoost;
        /// <summary>Streak animation clock (game time: stops while paused).</summary>
        public float WindTime;
        public HudColor FlashColor;
        public float FlashAge = 9f;

        // moves (M4.4): meter above the speed (carve charge -> STRAIGHTEN!, boost draining, draft), crew JUMP! cue, tips
        public string MoveLabel;
        public float MoveFill;
        public HudColor MoveColor;
        public bool MoveReady;
        /// <summary>Seconds since the crew spotted a hazard and called JUMP (shown briefly while a human rides).</summary>
        public float JumpCallAge = 9f;
        public string Tip;
        public float TipAge;

        // end of run
        public bool Ended;
        public string EndTitle = "RUN OVER";
        public readonly List<(string label, string value, string points)> EndLines = new List<(string, string, string)>(8);
        public string EndScore = "", EndBest = "", EndHint = "Press R, Space or (A) to ride again";
        /// <summary>Seconds since the end screen opened (panel pops in, lines reveal one by one, NEW BEST stamp lands).</summary>
        public float EndAge = 99f;
        public bool EndNewBest;
    }

    /// <summary>
    /// The HUD of the reference image as draw commands: distance and best on torn brush panels (top-left), coin
    /// and gem pills (top-right), slanted COMBO and NITRO READY! banners (right), big speed with a wedge of
    /// segments (bottom-right), rider avatars with coloured tags and arrows (bottom-left), plus countdown,
    /// popups, danger / nitro vignettes and the end screen. Anchored to the safe area, scaled from 1080p
    /// (skill: game-ui-ux). Allocation-free once the command list has grown.
    /// </summary>
    public static class HudLayout
    {
        public const float ReferenceHeight = 1080f;

        public static readonly HudColor Dark = HudColor.Hex(0x14161E, 0.86f);
        public static readonly HudColor Yellow = HudColor.Hex(0xFFD21F);
        public static readonly HudColor Cyan = HudColor.Hex(0x33E0FF);
        public static readonly HudColor Navy = HudColor.Hex(0x0B1B3A);
        public static readonly HudColor Red = HudColor.Hex(0xFF3B3B);
        public static readonly HudColor Orange = HudColor.Hex(0xFF9A2E);
        static readonly HudColor Outline = HudColor.Hex(0x0A0B10, 0.9f);

        /// <summary>P1..P6 tag colours (reference: blue, red, green, yellow, purple, orange).</summary>
        public static readonly HudColor[] PlayerColors =
        {
            HudColor.Hex(0x2F8BFF), HudColor.Hex(0xFF3B3B), HudColor.Hex(0x2FD158),
            HudColor.Hex(0xFFD21F), HudColor.Hex(0xB45CFF), HudColor.Hex(0xFF8A1F),
        };
        static readonly string[] Tags = { "P1", "P2", "P3", "P4", "P5", "P6" };

        /// <summary>UI scale for a safe area: 1 at 1080p, shrinks for narrow (portrait) screens.</summary>
        public static float ScaleFor(float safeW, float safeH) => Math.Max(0.45f, Math.Min(safeH / ReferenceHeight, safeW / 1500f));

        public static void Build(HudState s, float screenW, float screenH, float safeX, float safeY, float safeW, float safeH, List<HudCmd> o)
        {
            o.Clear();
            float k = ScaleFor(safeW, safeH);
            float L = safeX, T = safeY, R = safeX + safeW, B = safeY + safeH;
            float m = 26f * k;

            // full-screen vignettes: danger (red, pulsing) and nitro (cyan)
            if (s.Danger > 0.55f)
            {
                float d = (s.Danger - 0.55f) / 0.45f;
                float pulse = 0.75f + 0.25f * (float)Math.Sin(s.Time * 14f);
                Img(o, HudTex.Vignette, 0f, 0f, screenW, screenH, Red.WithAlpha(Math.Min(0.85f, d * pulse)));
            }
            if (s.NitroActive) Img(o, HudTex.Vignette, 0f, 0f, screenW, screenH, Cyan.WithAlpha(0.45f + 0.1f * (float)Math.Sin(s.Time * 20f)));

            WindStreaks(s, screenW, screenH, k, o);
            if (s.FlashAge < FlashTime)
            {
                // edge burst: the glow rushes in from the screen border and fades (nitro / carve boost / perfect ollie)
                float f = 1f - s.FlashAge / FlashTime;
                Img(o, HudTex.Vignette, 0f, 0f, screenW, screenH, s.FlashColor.WithAlpha(s.FlashColor.A * f), scale: 1f + 0.25f * f);
                Img(o, HudTex.Vignette, 0f, 0f, screenW, screenH, s.FlashColor.WithAlpha(s.FlashColor.A * f * f));
            }

            if (s.Ended) { EndScreen(s, (L + R) * 0.5f, (T + B) * 0.5f, k, o); return; }

            // ---- top-left: distance + best (the F1 debug panel uses this corner when open)
            if (!s.DebugPanelOpen)
            {
                Img(o, HudTex.Panel, L + m - 14f * k, T + m, 440f * k, 104f * k, Dark, skew: -0.15f);
                Txt(o, s.Distance, L + m + 18f * k, T + m + 8f * k, 420f * k, 88f * k, 70f * k, HudColor.White, HudAlign.Left, 4f * k);
                Img(o, HudTex.Panel, L + m - 6f * k, T + m + 106f * k, 330f * k, 50f * k, Dark, skew: -0.15f);
                Txt(o, Loc.T("BEST"), L + m + 18f * k, T + m + 110f * k, 110f * k, 42f * k, 28f * k, Cyan, HudAlign.Left, 2f * k);
                Txt(o, s.Best, L + m + 128f * k, T + m + 110f * k, 190f * k, 42f * k, 28f * k, HudColor.White, HudAlign.Left, 2f * k);
                for (int i = 0; i < s.LivesMax && i < 5; i++)
                {
                    HudColor c = i < s.LivesLeft ? Red : HudColor.Hex(0x3A3D48, 0.9f);
                    Img(o, HudTex.Heart, L + m + 8f * k + i * 40f * k, T + m + 164f * k, 34f * k, 34f * k, c);
                }
            }

            // ---- top-centre: score (small, the reference keeps the centre free)
            Img(o, HudTex.Pill, (L + R) * 0.5f - 130f * k, T + m, 260f * k, 54f * k, Dark);
            Txt(o, s.Score, (L + R) * 0.5f - 130f * k, T + m + 5f * k, 260f * k, 44f * k, 34f * k, HudColor.White, HudAlign.Center, 2f * k);

            // ---- top-right: coin and gem pills
            float pw = 210f * k, ph = 66f * k, px = R - m - pw;
            Pill(o, HudTex.Coin, s.Coins, px, T + m, pw, ph, k, s.CoinPop);
            Pill(o, HudTex.Gem, s.Diamonds, px, T + m + ph + 14f * k, pw, ph, k, s.DiamondPop);

            // ---- right: COMBO banner + NITRO banner (slanted, like the reference)
            float by = T + safeH * 0.27f;
            if (s.ShowCombo)
            {
                float pop = 1f + 0.3f * Clamp01(s.ComboPop);
                float bw = 420f * k, bh = 104f * k, bx = R - m - bw;
                Img(o, HudTex.Panel, bx, by, bw, bh, HudColor.Hex(0x0B0C12, 0.92f), rot: -8f, scale: pop);
                TxtR(o, Loc.T("COMBO"), bx + 30f * k, by + 22f * k, 220f * k, 66f * k, 44f * k, HudColor.White, HudAlign.Left, -8f, pop, bx + bw * 0.5f, by + bh * 0.5f, k);
                TxtR(o, s.Combo, bx + 250f * k, by + 2f * k, 150f * k, 96f * k, 80f * k, Yellow, HudAlign.Left, -8f, pop, bx + bw * 0.5f, by + bh * 0.5f, k);
                // progress to the next multiplier along the banner's lower edge
                Img(o, HudTex.White, bx + 40f * k, by + bh - 16f * k, (bw - 80f * k), 6f * k, HudColor.Hex(0x3A3D48, 0.9f), rot: -8f, pivotX: bx + bw * 0.5f, pivotY: by + bh * 0.5f);
                Img(o, HudTex.White, bx + 40f * k, by + bh - 16f * k, (bw - 80f * k) * Clamp01(s.ComboFill), 6f * k, Yellow, rot: -8f, pivotX: bx + bw * 0.5f, pivotY: by + bh * 0.5f);
            }
            if (s.NitroActive || s.NitroCharges > 0)
            {
                float nw = 330f * k, nh = 66f * k, nx = R - m - nw - 10f * k, ny = by + 118f * k;
                float pulse = s.NitroActive ? 1f + 0.06f * (float)Math.Sin(s.Time * 18f) : 1f;
                Img(o, HudTex.Panel, nx, ny, nw, nh, Cyan, rot: -8f, scale: pulse);
                TxtR(o, s.NitroActive ? Loc.T("NITRO!") : s.NitroText, nx, ny + 8f * k, nw, nh - 12f * k, 38f * k, Navy, HudAlign.Center, -8f, pulse, nx + nw * 0.5f, ny + nh * 0.5f, 0f);
            }

            // ---- bottom-right: speed + wedge of segments
            Txt(o, s.Speed, R - m - 330f * k, B - m - 196f * k, 300f * k, 124f * k, 118f * k, HudColor.White, HudAlign.Right, 5f * k);
            Txt(o, Loc.T("KM/H"), R - m - 104f * k, B - m - 60f * k, 104f * k, 44f * k, 28f * k, HudColor.White, HudAlign.Right, 2f * k);
            const int segs = 8;
            float barX = R - m - 400f * k, barY = B - m - 58f * k, barW = 270f * k, barH = 46f * k;
            Img(o, HudTex.White, barX - 8f * k, barY - 4f * k, barW + 20f * k, barH + 8f * k, Dark, skew: -0.35f);
            int lit = (int)Math.Ceiling(Clamp01(s.SpeedFill) * segs);
            HudColor segColor = s.NitroActive ? Cyan : s.Danger > 0.7f ? Red : Yellow;
            float gap = 6f * k, segW = (barW - gap * (segs - 1)) / segs;
            for (int i = 0; i < segs; i++)
            {
                float hgt = barH * (0.35f + 0.65f * (i + 1f) / segs);
                Img(o, HudTex.White, barX + i * (segW + gap), barY + barH - hgt, segW, hgt, i < lit ? segColor : HudColor.Hex(0x3A3D48, 0.85f), skew: -0.35f);
            }

            // ---- above the speed: move meter (carve charge / boost / draft)
            if (s.MoveLabel != null)
            {
                float mw = 380f * k, mh = 58f * k, mx = R - m - mw, my = B - m - 266f * k;
                float pulse = s.MoveReady ? 1f + 0.07f * (float)Math.Sin(s.Time * 16f) : 1f;
                Img(o, HudTex.Panel, mx, my, mw, mh, Dark, skew: -0.2f, scale: pulse);
                Img(o, HudTex.White, mx + 18f * k, my + mh - 14f * k, mw - 36f * k, 7f * k, HudColor.Hex(0x3A3D48, 0.9f), skew: -0.2f);
                Img(o, HudTex.White, mx + 18f * k, my + mh - 14f * k, (mw - 36f * k) * Clamp01(s.MoveFill), 7f * k, s.MoveColor, skew: -0.2f);
                Txt(o, s.MoveLabel, mx, my + 3f * k, mw, mh - 16f * k, 34f * k, s.MoveColor, HudAlign.Center, 3f * k, pulse);
            }

            // ---- bottom-left: rider avatars with coloured tags and arrows
            int n = Math.Max(1, Math.Min(6, s.Players));
            float cell = 78f * k, panelW = n * cell + 22f * k, panelH = 150f * k, plx = L + m, ply = B - m - panelH;
            Img(o, HudTex.Pill, plx, ply, panelW, panelH, Dark);
            for (int i = 0; i < n; i++)
            {
                float cx = plx + 11f * k + i * cell;
                bool on = s.OnBoard[i];
                HudColor pc = PlayerColors[i];
                if (s.Human[i]) Img(o, HudTex.Circle, cx + 3f * k, ply + 10f * k, 70f * k, 70f * k, HudColor.White);
                Img(o, (HudTex)((int)HudTex.Avatar0 + i), cx + 6f * k, ply + 12f * k, 64f * k, 64f * k, on ? HudColor.White : new HudColor(0.4f, 0.4f, 0.45f, 0.8f));
                Txt(o, Tags[i], cx, ply + 76f * k, 76f * k, 40f * k, 30f * k, on ? pc : pc.WithAlpha(0.45f), HudAlign.Center, 3f * k);
                Img(o, HudTex.Arrow, cx + 26f * k, ply + 116f * k, 24f * k, 18f * k, on ? pc : pc.WithAlpha(0.45f));
            }

            // ---- top-centre under the score: first-run tip (fitted to the free width between the corner panels)
            if (s.Tip != null && s.TipAge < TipCoach.ShowTime)
            {
                float left = L + m + 450f * k, right = R - m - 222f * k, tw = Math.Min(1100f * k, right - left - 20f * k);
                float tx = (L + R) * 0.5f - tw * 0.5f, ty = T + m + 66f * k, th = 58f * k;
                float a = Clamp01(s.TipAge / 0.2f) * Clamp01((TipCoach.ShowTime - s.TipAge) / 0.4f);
                float size = Math.Min(30f * k, tw / (s.Tip.Length * 0.62f));
                Img(o, HudTex.Panel, tx, ty, tw, th, Yellow.WithAlpha(0.95f * a), skew: -0.12f);
                Txt(o, s.Tip, tx + 10f * k, ty + 6f * k, tw - 20f * k, th - 12f * k, size, Navy.WithAlpha(a), HudAlign.Center, 0f);
            }
            // ---- crew spotted a hazard: JUMP!
            if (s.JumpCallAge < 0.7f)
            {
                float a = 1f - Clamp01((s.JumpCallAge - 0.45f) / 0.25f);
                float pop = 0.6f + 0.4f * EaseOutBack(Clamp01(s.JumpCallAge / 0.18f));
                Txt(o, Loc.T("JUMP!"), (L + R) * 0.5f - 300f * k, T + safeH * 0.28f, 600f * k, 150f * k, 132f * k, Yellow.WithAlpha(a), HudAlign.Center, 7f * k, pop);
            }

            // ---- centre: countdown, popups
            if (!string.IsNullOrEmpty(s.Countdown))
            {
                float pop = 0.6f + 0.4f * EaseOutBack(Clamp01(s.CountdownAge / 0.35f));
                float fade = 1f - Clamp01((s.CountdownAge - 0.75f) / 0.25f);
                bool go = s.Countdown == Loc.T("GO!");
                Txt(o, s.Countdown, (L + R) * 0.5f - 400f * k, (T + B) * 0.5f - 190f * k, 800f * k, 260f * k, 230f * k,
                    (go ? Yellow : HudColor.White).WithAlpha(fade), HudAlign.Center, 8f * k, pop);
            }
            float py = T + safeH * 0.5f + 40f * k;
            for (int i = s.Popups.Count - 1; i >= 0; i--)
            {
                HudPopup p = s.Popups[i];
                float a = Clamp01(1.6f - p.Age);
                float pop = 0.5f + 0.5f * EaseOutBack(Clamp01(p.Age / 0.22f));
                Txt(o, p.Text, (L + R) * 0.5f - 420f * k, py - p.Age * 36f * k, 840f * k, 64f * k, 48f * k, p.Color.WithAlpha(a), HudAlign.Center, 4f * k, pop);
                py += 60f * k;
            }
        }

        const int MaxStreaks = 60;
        public const float FlashTime = 0.45f;

        /// <summary>
        /// Anime-style wind: thin streaks rushing outward along the left and right screen edges (the centre, where
        /// the board and the road are, stays clear). Count, length and opacity grow with Wind; tinted cyan under
        /// nitro and orange during a carve boost. Deterministic per streak (no allocation, no RNG state).
        /// </summary>
        static void WindStreaks(HudState s, float w, float h, float k, List<HudCmd> o)
        {
            if (s.Wind <= 0.02f) return;
            int count = (int)(MaxStreaks * Clamp01(s.Wind));
            float cx = w * 0.5f, cy = h * 0.55f;
            HudColor tint = new HudColor(1f - 0.7f * s.WindNitro, 1f - 0.08f * s.WindNitro - 0.25f * s.WindBoost, 1f - 0.65f * s.WindBoost);
            for (int i = 0; i < count; i++)
            {
                float r1 = Hash01(i * 3 + 1), r2 = Hash01(i * 3 + 2), r3 = Hash01(i * 3 + 3);
                // angles hug the left and right sides (+-40 degrees around horizontal), a few more below the horizon
                float side = i % 2 == 0 ? 0f : (float)Math.PI;
                float spread = (r1 - 0.42f) * 1.4f;
                float ang = side + (i % 2 == 0 ? spread : -spread);
                float dirX = (float)Math.Cos(ang), dirY = (float)Math.Sin(ang);
                // distance to the screen edge along this direction: streaks travel from 35 % of it to just past it
                float edge = Math.Min(Math.Abs(dirX) > 1e-3f ? cx / Math.Abs(dirX) : 1e9f, Math.Abs(dirY) > 1e-3f ? cy / Math.Abs(dirY) : 1e9f);
                float speed = 1.2f + 1.4f * r2 + 1.6f * s.Wind;
                float t = (s.WindTime * speed + r3) % 1f;
                float dist = edge * (0.38f + 0.7f * t);
                float len = (140f + 260f * s.Wind) * k * (0.5f + 0.9f * r2) * (0.5f + t);
                float thick = (4f + 5f * r3) * k * (0.6f + 0.6f * s.Wind);
                float px = cx + dirX * dist, py = cy + dirY * dist;
                float alpha = Clamp01(0.3f + s.Wind * 0.9f) * (float)Math.Sin(t * Math.PI) * (0.55f + 0.45f * r1);
                Img(o, HudTex.Streak, px - len * 0.5f, py - thick, len, thick * 2f, tint.WithAlpha(alpha), rot: ang * 180f / (float)Math.PI);
            }
        }

        static float Hash01(int n)
        {
            unchecked
            {
                uint x = (uint)n * 2654435761u;
                x ^= x >> 16; x *= 2246822519u; x ^= x >> 13;
                return (x & 0xFFFF) / 65535f;
            }
        }

        static void Pill(List<HudCmd> o, HudTex icon, string text, float x, float y, float w, float h, float k, float popAnim)
        {
            float pop = 1f + 0.25f * Clamp01(popAnim);
            Img(o, HudTex.Pill, x, y, w, h, Dark);
            Img(o, icon, x - 18f * k, y - 10f * k, h + 20f * k, h + 20f * k, HudColor.White, scale: pop);
            Txt(o, text, x + h, y + 4f * k, w - h - 18f * k, h - 8f * k, 48f * k, HudColor.White, HudAlign.Right, 3f * k, pop);
        }

        static void EndScreen(HudState s, float cx, float cy, float k, List<HudCmd> o)
        {
            float t = s.EndAge;
            float w = 700f * k, h = 640f * k, x = cx - w * 0.5f, y = cy - h * 0.5f;
            float panel = 0.7f + 0.3f * EaseOutBack(Clamp01(t / 0.3f));
            Img(o, HudTex.Card, x, y, w, h, HudColor.Hex(0x14161E, 0.92f * Clamp01(t / 0.15f)), scale: panel);
            Img(o, HudTex.Panel, x + 90f * k, y - 34f * k, w - 180f * k, 110f * k, Yellow, rot: -4f, scale: panel);
            Txt(o, s.EndTitle, x + 90f * k, y - 22f * k, w - 180f * k, 90f * k, 72f * k, Navy, HudAlign.Center, 0f, panel);
            // lines reveal one by one, sliding in
            float ly = y + 104f * k;
            int i = 0;
            foreach ((string label, string value, string points) in s.EndLines)
            {
                float lt = Clamp01((t - 0.3f - i * 0.1f) / 0.18f) * (points == "+0" ? 0.45f : 1f); // nothing earned: dimmed
                if (lt > 0f)
                {
                    float slide = (1f - EaseOutBack(lt)) * 60f * k;
                    Txt(o, label, x + 50f * k + slide, ly, 270f * k, 44f * k, 30f * k, HudColor.Hex(0xD8DCEA).WithAlpha(lt), HudAlign.Left, 2f * k);
                    Txt(o, value, x + 300f * k + slide, ly, 200f * k, 44f * k, 30f * k, HudColor.White.WithAlpha(lt), HudAlign.Left, 2f * k);
                    if (!string.IsNullOrEmpty(points)) Txt(o, points, x + 470f * k + slide, ly, 180f * k, 44f * k, 30f * k, Yellow.WithAlpha(lt), HudAlign.Right, 2f * k);
                }
                ly += 48f * k;
                i++;
            }
            float st = t - 0.3f - i * 0.1f - 0.1f;
            float scorePop = 0.5f + 0.5f * EaseOutBack(Clamp01(st / 0.25f));
            if (st > 0f)
            {
                Txt(o, s.EndScore, x, ly + 12f * k, w, 80f * k, 66f * k, Yellow, HudAlign.Center, 4f * k, scorePop);
                Txt(o, s.EndBest, x, ly + 94f * k, w, 44f * k, 30f * k, Cyan.WithAlpha(Clamp01(st / 0.3f)), HudAlign.Center, 2f * k);
            }
            // NEW BEST stamp slams onto the corner of the card
            if (s.EndNewBest && st > 0.35f)
            {
                float stamp = 1f + 1.2f * (1f - EaseOutBack(Clamp01((st - 0.35f) / 0.2f)));
                float sw = 330f * k, sh = 96f * k, sx = x + w - sw * 0.6f, sy = y - 118f * k;
                Img(o, HudTex.Panel, sx, sy, sw, sh, Red, rot: 12f, scale: stamp);
                TxtR(o, Loc.T("NEW BEST!"), sx, sy + 8f * k, sw, sh - 16f * k, 54f * k, HudColor.White, HudAlign.Center, 12f, stamp, sx + sw * 0.5f, sy + sh * 0.5f, k);
            }
            if (t > 1.2f)
            {
                float blink = 0.65f + 0.35f * (float)Math.Sin(s.Time * 5f);
                Txt(o, s.EndHint, x, y + h - 64f * k, w, 44f * k, 28f * k, HudColor.White.WithAlpha(blink), HudAlign.Center, 2f * k);
            }
        }

        // ------------------------------------------------------------------ helpers

        internal static void Img(List<HudCmd> o, HudTex tex, float x, float y, float w, float h, HudColor c,
                        float rot = 0f, float skew = 0f, float scale = 1f, float pivotX = float.NaN, float pivotY = float.NaN)
        {
            var cmd = new HudCmd { Tex = tex, X = x, Y = y, W = w, H = h, Color = c, Rotation = rot, Skew = skew, Scale = scale };
            if (!float.IsNaN(pivotX))
            {
                // rotate around an external pivot: emulate by rotating the rect centre around it
                float rad = rot * (float)Math.PI / 180f;
                float cx = x + w * 0.5f - pivotX, cy = y + h * 0.5f - pivotY;
                float rx = cx * (float)Math.Cos(rad) - cy * (float)Math.Sin(rad), ry = cx * (float)Math.Sin(rad) + cy * (float)Math.Cos(rad);
                cmd.X = pivotX + rx - w * 0.5f;
                cmd.Y = pivotY + ry - h * 0.5f;
            }
            o.Add(cmd);
        }

        internal static void Txt(List<HudCmd> o, string text, float x, float y, float w, float h, float size, HudColor c, HudAlign align, float outline, float scale = 1f)
        {
            if (string.IsNullOrEmpty(text)) return;
            o.Add(new HudCmd { Text = text, X = x, Y = y, W = w, H = h, FontSize = size, Color = c, Align = align, Outline = outline, OutlineColor = Outline.WithAlpha(Outline.A * c.A), Scale = scale });
        }

        /// <summary>Text rotated with a banner around the banner centre.</summary>
        static void TxtR(List<HudCmd> o, string text, float x, float y, float w, float h, float size, HudColor c, HudAlign align, float rot, float scale, float pivotX, float pivotY, float k)
        {
            float rad = rot * (float)Math.PI / 180f;
            float cx = (x + w * 0.5f - pivotX) * scale, cy = (y + h * 0.5f - pivotY) * scale;
            float rx = cx * (float)Math.Cos(rad) - cy * (float)Math.Sin(rad), ry = cx * (float)Math.Sin(rad) + cy * (float)Math.Cos(rad);
            o.Add(new HudCmd
            {
                Text = text, X = pivotX + rx - w * 0.5f, Y = pivotY + ry - h * 0.5f, W = w, H = h, FontSize = size, Color = c, Align = align,
                Outline = k > 0f ? 3f * k : 0f, OutlineColor = Outline, Rotation = rot, Scale = scale,
            });
        }

        public static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

        /// <summary>Ease-out with overshoot (skill: game-feel, "BACK for pop").</summary>
        public static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }
    }
}
