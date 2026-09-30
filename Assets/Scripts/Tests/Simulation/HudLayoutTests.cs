using System;
using System.Collections.Generic;
using Game.Hud;
using Game.Simulation;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>
    /// The reference-style HUD across screens (skill: game-ui-ux, "verify across screens"): every element stays
    /// inside the safe area, texts never overlap, layout is allocation-free once warm, textures build.
    /// </summary>
    public class HudLayoutTests
    {
        static readonly (string name, float w, float h, float sx, float sy, float sw, float sh)[] Screens =
        {
            ("1080p 16:9", 1920, 1080, 0, 0, 1920, 1080),
            ("720p 16:9", 1280, 720, 0, 0, 1280, 720),
            ("4:3", 1024, 768, 0, 0, 1024, 768),
            ("21:9", 2560, 1080, 0, 0, 2560, 1080),
            ("phone landscape with notch", 2436, 1125, 132, 0, 2172, 1062),
            ("4K", 3840, 2160, 0, 0, 3840, 2160),
        };

        static HudState FullState()
        {
            var run = new RunSimulation(new BoardTuningData { livesPerRun = 3 }, 5, 6);
            var p = new HudPresenter();
            p.Update(run, 12630f, 1f, 0.016f);
            HudState s = p.State;
            // worst case: every element visible with long values
            s.Distance = "12,345 m"; s.Best = "99,999 m"; s.Coins = "9999"; s.Diamonds = "999"; s.Score = "9,999,999";
            s.Combo = "x6"; s.ShowCombo = true; s.ComboFill = 0.5f; s.NitroCharges = 2; s.NitroText = "NITRO READY! x2";
            s.Speed = "188"; s.SpeedFill = 1f; s.Players = 6; s.LivesLeft = 2; s.LivesMax = 3;
            for (int i = 0; i < 6; i++) { s.OnBoard[i] = true; s.Human[i] = i == 0; }
            return s;
        }

        struct Box { public float X0, Y0, X1, Y1; public string What; }

        /// <summary>Axis-aligned bounds of a command after its scale and rotation (skew ignored: it only leans).</summary>
        static Box Bounds(in HudCmd c)
        {
            float scale = c.Scale == 0f ? 1f : c.Scale;
            float hw = c.W * 0.5f * scale, hh = c.H * 0.5f * scale;
            float rad = c.Rotation * (float)Math.PI / 180f, cos = Math.Abs((float)Math.Cos(rad)), sin = Math.Abs((float)Math.Sin(rad));
            float ex = hw * cos + hh * sin + Math.Abs(c.Skew) * hh, ey = hw * sin + hh * cos;
            float cx = c.X + c.W * 0.5f, cy = c.Y + c.H * 0.5f;
            return new Box { X0 = cx - ex, Y0 = cy - ey, X1 = cx + ex, Y1 = cy + ey, What = c.Text ?? c.Tex.ToString() };
        }

        [Test]
        public void Hud_StaysInsideSafeArea_TextsDoNotOverlap_OnEveryScreen()
        {
            HudState s = FullState();
            var cmds = new List<HudCmd>();
            foreach (var sc in Screens)
            {
                HudLayout.Build(s, sc.w, sc.h, sc.sx, sc.sy, sc.sw, sc.sh, cmds);
                var texts = new List<Box>();
                foreach (HudCmd c in cmds)
                {
                    if (c.Tex == HudTex.Vignette) continue; // full-screen by design
                    Box b = Bounds(c);
                    float tol = 2f;
                    Assert.That(b.X0 >= sc.sx - tol && b.Y0 >= sc.sy - tol && b.X1 <= sc.sx + sc.sw + tol && b.Y1 <= sc.sy + sc.sh + tol,
                        $"{sc.name}: '{b.What}' [{b.X0:0},{b.Y0:0} - {b.X1:0},{b.Y1:0}] leaves the safe area");
                    if (c.Text != null) texts.Add(b);
                }
                for (int a = 0; a < texts.Count; a++)
                for (int b = a + 1; b < texts.Count; b++)
                {
                    Box p = texts[a], q = texts[b];
                    // text boxes are generous (whole rect), so require a real overlap of the rects' inner 80 %
                    float ix = Math.Min(p.X1, q.X1) - Math.Max(p.X0, q.X0), iy = Math.Min(p.Y1, q.Y1) - Math.Max(p.Y0, q.Y0);
                    bool overlap = ix > 0.2f * Math.Min(p.X1 - p.X0, q.X1 - q.X0) && iy > 0.35f * Math.Min(p.Y1 - p.Y0, q.Y1 - q.Y0);
                    Assert.IsFalse(overlap, $"{sc.name}: '{p.What}' overlaps '{q.What}'");
                }
                TestContext.WriteLine($"{sc.name}: {cmds.Count} commands, scale {HudLayout.ScaleFor(sc.sw, sc.sh):0.00}");
            }
        }

        [Test]
        public void Hud_Build_IsAllocationFreeOnceWarm()
        {
            HudState s = FullState();
            s.Popups.Add(new HudPopup { Text = "NEAR MISS!", Color = HudLayout.Yellow, Age = 0.3f });
            s.Countdown = "3";
            var cmds = new List<HudCmd>(256);
            HudLayout.Build(s, 1920, 1080, 0, 0, 1920, 1080, cmds);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 100; i++) HudLayout.Build(s, 1920, 1080, 0, 0, 1920, 1080, cmds);
            long alloc = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0, alloc, "HUD layout must not allocate per frame");
        }

        [Test]
        public void Hud_Presenter_RebuildsStringsOnlyOnChange_AndPopsCounters()
        {
            var run = new RunSimulation(new BoardTuningData { livesPerRun = 0 }, 9, 1);
            var p = new HudPresenter();
            p.Update(run, 0f, 0f, 0.016f);
            string coins = p.State.Coins;
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 50; i++) p.Update(run, 0f, i * 0.016f, 0.016f);
            long alloc = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreSame(coins, p.State.Coins, "unchanged value keeps its string");
            Assert.AreEqual(0, alloc, "no per-frame garbage while nothing changes");
            run.Score.OnCoin();
            p.Update(run, 0f, 1f, 0.016f);
            Assert.AreEqual("1", p.State.Coins);
            Assert.That(p.State.CoinPop, Is.GreaterThan(0.9f), "a new coin pops the counter");
        }

        [Test]
        public void Hud_Textures_AllBuild()
        {
            foreach (HudTex t in Enum.GetValues(typeof(HudTex)))
            {
                if (t == HudTex.None) continue;
                HudImage img = HudTextures.Get(t);
                Assert.That(img.Width * img.Height * 4, Is.EqualTo(img.Rgba.Length), t.ToString());
                int opaque = 0;
                for (int i = 3; i < img.Rgba.Length; i += 4) if (img.Rgba[i] > 128) opaque++;
                Assert.That(opaque, Is.GreaterThan(0), $"{t} is empty");
            }
        }
    }
}
