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
            ("Steam Deck 1280x800", 1280, 800, 0, 0, 1280, 800),
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

        static void AssertLayout(HudState s)
        {
            var cmds = new List<HudCmd>();
            foreach (var sc in Screens)
            {
                HudLayout.Build(s, sc.w, sc.h, sc.sx, sc.sy, sc.sw, sc.sh, cmds);
                var texts = new List<Box>();
                foreach (HudCmd c in cmds)
                {
                    if (c.Tex == HudTex.Vignette || c.Tex == HudTex.Streak) continue; // full-screen effects by design
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
        public void Hud_StaysInsideSafeArea_TextsDoNotOverlap_OnEveryScreen() => AssertLayout(FullState());

        [Test]
        public void Hud_WithMoveMeter_JumpCue_AndTip_StillFitsEveryScreen()
        {
            HudState s = FullState();
            s.MoveLabel = "MEGA! STRAIGHTEN!"; s.MoveFill = 1f; s.MoveColor = HudLayout.Yellow; s.MoveReady = true;
            s.JumpCallAge = 0.3f;
            s.Tip = Game.Hud.TipCoach.Text(Game.Hud.Tip.Steer); s.TipAge = 1f;
            s.Countdown = null;
            AssertLayout(s);
        }

        [Test]
        public void Hud_EndScreen_WithNewBestStamp_FitsEveryScreen_AndAnimatesIn()
        {
            HudState s = FullState();
            var run = new RunSimulation(new BoardTuningData { livesPerRun = 3 }, 5, 6);
            var p = new HudPresenter();
            p.FillEndScreen(run);
            s.EndLines.Clear(); s.EndLines.AddRange(p.State.EndLines);
            s.EndTitle = p.State.EndTitle; s.EndScore = "SCORE  9,999,999"; s.EndBest = p.State.EndBest; s.EndHint = p.State.EndHint;
            s.Ended = true; s.EndNewBest = true;
            var top = new List<HighScoreEntry>();
            for (int i = 0; i < HighScoreManager.TableSize; i++)
                top.Add(new HighScoreEntry { Score = 9999999f - i * 1000f, Distance = 99999f - i, Crew = 6, Date = "2026-09-30" });
            Game.Hud.ScoreTable.Fill(top, 3, s.EndTable); // this run placed #3
            var cmds = new List<HudCmd>();
            int previous = -1;
            foreach (float age in new[] { 0f, 0.4f, 0.8f, 1.15f, 2.5f })
            {
                s.EndAge = age;
                HudLayout.Build(s, 1920, 1080, 0, 0, 1920, 1080, cmds);
                int texts = 0;
                foreach (HudCmd c in cmds) if (c.Text != null) texts++;
                TestContext.WriteLine($"end screen at {age:0.0} s: {texts} texts");
                Assert.That(texts, Is.GreaterThan(previous), "lines, score, stamp and hint arrive one after another");
                previous = texts;
            }
            bool stamp = false, table = false, newTag = false;
            foreach (HudCmd c in cmds) { stamp |= c.Text == "NEW BEST!"; table |= c.Text == "TOP SCORES"; newTag |= c.Text == "NEW"; }
            Assert.IsTrue(stamp, "NEW BEST! stamp");
            Assert.IsTrue(table && newTag, "TOP SCORES table with this run's NEW row");
            AssertLayout(s);
        }

        [Test]
        public void TipCoach_TeachesEachMoveOnce_WhenItMatters_OnlyForHumans()
        {
            var run = new RunSimulation(new BoardTuningData { livesPerRun = 0 }, 9, 6);
            var none = new PlayerInputState[BoardSimulation.MaxPlayers];
            var coach = new Game.Hud.TipCoach();
            for (int i = 0; i < 600; i++) { run.Step(1f / 60f, none); coach.Update(run, humanRiding: false, 1f / 60f); }
            Assert.AreEqual(0, coach.SeenMask, "no tips for a bot-only (attract) ride");
            string first = null;
            for (int i = 0; i < 240 && first == null; i++) { run.Step(1f / 60f, none); coach.Update(run, true, 1f / 60f); first = coach.Current; }
            Assert.AreEqual(Game.Hud.TipCoach.Text(Game.Hud.Tip.Steer), first, "steering comes first, right after the start");
            int shown = 1;
            string last = first;
            for (int i = 0; i < 60 * 150; i++)
            {
                run.Step(1f / 60f, none);
                coach.Update(run, true, 1f / 60f);
                if (coach.Current != null && coach.Current != last) { shown++; last = coach.Current; }
                if (coach.Current == null) last = null;
            }
            TestContext.WriteLine($"tips shown in 2.5 min: {shown}, seen mask {Convert.ToString(coach.SeenMask, 2)}");
            Assert.That(shown, Is.InRange(3, 5));
            int mask = coach.SeenMask;
            var again = new Game.Hud.TipCoach { SeenMask = mask };
            var run2 = new RunSimulation(new BoardTuningData { livesPerRun = 0 }, 9, 6);
            for (int i = 0; i < 600; i++) { run2.Step(1f / 60f, none); again.Update(run2, true, 1f / 60f); }
            Assert.IsNull(again.Current, "seen tips never repeat");
            var off = new Game.Hud.TipCoach { Enabled = false };
            for (int i = 0; i < 600; i++) off.Update(run2, true, 1f / 60f);
            Assert.AreEqual(0, off.SeenMask, "tips switched off in the settings");
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

        static int Count(List<HudCmd> cmds, HudTex t)
        {
            int n = 0;
            foreach (HudCmd c in cmds) if (c.Tex == t) n++;
            return n;
        }

        [Test]
        public void Hud_WindStreaks_GrowWithWind_StayOffTheCentre_AndDoNotAllocate()
        {
            HudState s = FullState();
            var cmds = new List<HudCmd>(256);
            int previous = -1;
            foreach (float wind in new[] { 0f, 0.3f, 0.6f, 1f })
            {
                s.Wind = wind;
                HudLayout.Build(s, 1920, 1080, 0, 0, 1920, 1080, cmds);
                int n = Count(cmds, HudTex.Streak);
                TestContext.WriteLine($"wind {wind:0.0}: {n} streaks");
                Assert.That(n, Is.GreaterThan(previous), "more wind, more streaks");
                previous = n;
                foreach (HudCmd c in cmds)
                {
                    if (c.Tex != HudTex.Streak) continue;
                    float cx = c.X + c.W * 0.5f, cy = c.Y + c.H * 0.5f;
                    // the board and the road ahead (screen centre) stay readable
                    Assert.That(Math.Abs(cx - 960f) > 300f || Math.Abs(cy - 560f) > 300f, $"streak at {cx:0},{cy:0} covers the centre");
                }
            }
            Assert.AreEqual(0, Count(new List<HudCmd>(), HudTex.Streak));

            s.WindTime = 3.7f; s.WindNitro = 1f; s.FlashAge = 0.1f; s.FlashColor = HudLayout.Cyan;
            for (int i = 0; i < 20; i++) { s.WindTime += 0.016f; HudLayout.Build(s, 1920, 1080, 0, 0, 1920, 1080, cmds); } // warm (JIT)
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 100; i++) { s.WindTime += 0.016f; HudLayout.Build(s, 1920, 1080, 0, 0, 1920, 1080, cmds); }
            long alloc = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0, alloc, $"wind streaks must not allocate per frame ({alloc} B, {cmds.Count} cmds, capacity {cmds.Capacity})");
        }

        [Test]
        public void Hud_Nitro_FlashesTheEdges_AndBlowsFullWind_CrashStopsIt()
        {
            var run = new RunSimulation(new BoardTuningData { livesPerRun = 0 }, 9, 1);
            var p = new HudPresenter();
            p.Update(run, 0f, 0f, 0.016f);
            Assert.That(p.State.Wind, Is.LessThan(0.05f), "standing still: no wind");

            run.Board.Board.StartNitro(2f);
            p.OnStep(new RunStepEvents { NitroStarted = true }, run);
            Assert.That(p.State.FlashAge, Is.EqualTo(0f), "nitro fires an edge flash");
            for (int i = 0; i < 60; i++) p.Update(run, 0f, i * 0.016f, 0.016f);
            TestContext.WriteLine($"after 1 s of nitro: wind {p.State.Wind:0.00}, cyan {p.State.WindNitro:0.00}");
            Assert.That(p.State.Wind, Is.GreaterThan(0.9f), "nitro = full wind");
            Assert.That(p.State.WindNitro, Is.GreaterThan(0.9f), "nitro wind is cyan");
            Assert.That(p.State.FlashAge, Is.GreaterThan(HudLayout.FlashTime), "the flash is short");

            run.Board.Board.ForceCrash();
            for (int i = 0; i < 60; i++) p.Update(run, 0f, 1f + i * 0.016f, 0.016f);
            Assert.That(p.State.Wind, Is.LessThan(0.05f), "a crash stops the wind");
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
