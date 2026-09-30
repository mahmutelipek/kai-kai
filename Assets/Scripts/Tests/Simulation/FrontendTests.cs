using System;
using System.Collections.Generic;
using Game.Frontend;
using Game.Hud;
using Game.Simulation;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>Steam-readiness checks without Unity: settings file, languages, menus, lobby, achievements, menu layout.</summary>
    public class FrontendTests
    {
        [TearDown]
        public void Reset() { Loc.Current = Language.English; Loc.TrackMissing = false; Loc.Missing.Clear(); }

        [Test]
        public void Settings_RoundTrip_AndTolerateBadFiles()
        {
            var s = new GameSettings { Display = DisplayMode.Windowed, Resolution = 3, VSync = false, Quality = GraphicsQuality.Low, ReduceMotion = true,
                                       Language = Language.Turkish, MasterVolume = 0.3f, MusicVolume = 0.1f, EffectsVolume = 1f, CrewSize = 4, BotsFill = false };
            GameSettings back = GameSettings.Parse(s.Serialize());
            Assert.AreEqual(s.Serialize(), back.Serialize());
            GameSettings junk = GameSettings.Parse("display=9\nmaster=abc\nlanguage=-4\ncrew=99\n???\nvsync=0");
            Assert.AreEqual(new GameSettings().Display, junk.Display, "invalid values keep defaults");
            Assert.AreEqual(6, junk.CrewSize, "crew clamped to 6");
            Assert.IsFalse(junk.VSync);
            Assert.AreEqual(Language.Turkish, GameSettings.LanguageFor("Turkish"));
            Assert.AreEqual(Language.English, GameSettings.LanguageFor("German"));
        }

        [Test]
        public void Turkish_CoversEveryStringTheMenusAndHudUse()
        {
            Loc.Current = Language.Turkish;
            Loc.TrackMissing = true;
            var model = new FrontendModel(new GameSettings { Language = Language.Turkish });
            model.Lobby.Join(JoinedDevice.Keyboard(false));
            model.Lobby.Join(JoinedDevice.Keyboard(true));
            model.Lobby.Join(JoinedDevice.Pad(7));
            var cmds = new List<HudCmd>();
            foreach (MenuScreen screen in new[] { MenuScreen.Title, MenuScreen.Lobby, MenuScreen.Pause, MenuScreen.Settings, MenuScreen.HowToPlay, MenuScreen.Credits })
            {
                model.Open(screen, push: false);
                foreach (MenuItem item in Enum.GetValues(typeof(MenuItem))) model.Describe(item);
                FrontendLayout.Build(model, 0f, 1920, 1080, 0, 0, 1920, 1080, cmds);
            }
            // HUD with every popup and the end screen
            var run = new RunSimulation(new BoardTuningData { livesPerRun = 3 }, 3, 6);
            var p = new HudPresenter();
            p.Update(run, 0f, 0f, 0.016f);
            p.OnStep(new RunStepEvents { Ollie = true, PerfectOllie = true, CarveBoost = 2, SlipstreamStarted = true }, run);
            p.OnStep(new RunStepEvents { Ollie = true, CarveBoost = 1 }, run);
            p.OnStep(new RunStepEvents { NearMisses = 1, CleanLanding = true, Landed = true, Airtime = 1f, SectionCleared = true, Diamonds = 1,
                                         NitroPickups = 1, NitroStarted = true, HeavyHits = 1, Crashed = true }, run);
            p.FillEndScreen(run);
            p.SetCountdown(Loc.T("GO!"));
            p.State.ShowCombo = true; p.State.NitroCharges = 2; p.State.LivesMax = 3;
            p.Invalidate(); p.Update(run, 0f, 1f, 0.016f);
            HudLayout.Build(p.State, 1920, 1080, 0, 0, 1920, 1080, cmds);
            p.State.Ended = true;
            HudLayout.Build(p.State, 1920, 1080, 0, 0, 1920, 1080, cmds);
            Loc.T("RIDERS: "); Loc.T("REDUCED MOTION ON"); Loc.T("REDUCED MOTION OFF");
            Assert.IsEmpty(Loc.Missing, "untranslated: " + string.Join(", ", Loc.Missing));
        }

        [Test]
        public void Menus_NavigateLikeAConsoleGame()
        {
            var m = new FrontendModel(new GameSettings());
            Assert.AreEqual(MenuScreen.Title, m.Screen);
            Assert.AreEqual(MenuItem.Play, m.Focused, "a control is focused when a screen opens");
            m.Handle(new MenuInput { Up = true });
            Assert.AreEqual(MenuItem.Quit, m.Focused, "focus wraps");
            m.Handle(new MenuInput { Down = true });
            Assert.AreEqual(MenuAction.None, m.Handle(new MenuInput { Submit = true }));
            Assert.AreEqual(MenuScreen.Lobby, m.Screen);
            Assert.AreEqual(MenuItem.Start, m.Focused, "lobby opens on START");
            Assert.AreEqual(MenuAction.None, m.Handle(new MenuInput { Submit = true }), "cannot start without a rider");
            m.Lobby.Join(JoinedDevice.Pad(3));
            Assert.AreEqual(MenuAction.StartRun, m.Handle(new MenuInput { Submit = true }));
            m.Handle(new MenuInput { Back = true });
            Assert.AreEqual(MenuScreen.Title, m.Screen, "back goes to the title");

            // pause -> settings -> back returns to pause; back on pause resumes
            m.Open(MenuScreen.Pause, push: false);
            m.Handle(new MenuInput { Down = true }); m.Handle(new MenuInput { Down = true });
            Assert.AreEqual(MenuItem.HowToPlay, m.Focused);
            m.Handle(new MenuInput { Submit = true });
            Assert.AreEqual(MenuScreen.HowToPlay, m.Screen, "how to play is one press away from pause");
            Assert.AreEqual(MenuItem.Back, m.Focused);
            m.Handle(new MenuInput { Submit = true });
            Assert.AreEqual(MenuScreen.Pause, m.Screen);
            m.Handle(new MenuInput { Down = true }); m.Handle(new MenuInput { Down = true }); m.Handle(new MenuInput { Down = true });
            Assert.AreEqual(MenuItem.Settings, m.Focused);
            m.Handle(new MenuInput { Submit = true });
            Assert.AreEqual(MenuScreen.Settings, m.Screen);
            m.Handle(new MenuInput { Down = true }); m.Handle(new MenuInput { Down = true });
            Assert.AreEqual(MenuItem.VSync, m.Focused);
            bool vsync = m.Settings.VSync;
            Assert.AreEqual(MenuAction.SettingsChanged, m.Handle(new MenuInput { Right = true }));
            Assert.AreNotEqual(vsync, m.Settings.VSync);
            m.Handle(new MenuInput { Back = true });
            Assert.AreEqual(MenuScreen.Pause, m.Screen);
            Assert.AreEqual(MenuAction.Resume, m.Handle(new MenuInput { Back = true }));

            // English only for now: no language item in the settings list
            m.Open(MenuScreen.Settings, push: false);
            CollectionAssert.DoesNotContain(m.Items, MenuItem.Language);

            // riding tips: switching them back on replays every tip
            m.Settings.TipsSeen = 0b11011;
            while (m.Focused != MenuItem.Tips) m.Handle(new MenuInput { Down = true });
            Assert.AreEqual(MenuAction.SettingsChanged, m.Handle(new MenuInput { Submit = true }));
            Assert.IsFalse(m.Settings.ShowTips);
            m.Handle(new MenuInput { Submit = true });
            Assert.IsTrue(m.Settings.ShowTips);
            Assert.AreEqual(0, m.Settings.TipsSeen);
            var round = GameSettings.Parse(new GameSettings { ShowTips = false, TipsSeen = 21 }.Serialize());
            Assert.IsFalse(round.ShowTips);
            Assert.AreEqual(21, round.TipsSeen, "seen tips survive a restart");

            // title: how to play and credits
            m.Open(MenuScreen.Title, push: false);
            CollectionAssert.AreEqual(new[] { MenuItem.Play, MenuItem.HowToPlay, MenuItem.Settings, MenuItem.Credits, MenuItem.Quit }, m.Items);
        }

        [Test]
        public void Lobby_JoinLeave_AndBotsFillTheCrew()
        {
            var l = new Lobby { CrewSize = 4, BotsFill = true };
            Assert.IsTrue(l.Join(JoinedDevice.Keyboard(false)));
            Assert.IsFalse(l.Join(JoinedDevice.Keyboard(false)), "same device joins once");
            Assert.IsTrue(l.Join(JoinedDevice.Pad(11)));
            Assert.IsTrue(l.Join(JoinedDevice.Pad(12)));
            Assert.AreEqual(4, l.RiderCount, "3 humans + 1 bot up to crew 4");
            l.BotsFill = false;
            Assert.AreEqual(3, l.RiderCount);
            for (int i = 0; i < 5; i++) l.Join(JoinedDevice.Pad(20 + i));
            Assert.AreEqual(6, l.Joined.Count, "never more than 6");
            Assert.IsTrue(l.Leave(JoinedDevice.Pad(11)));
            Assert.AreEqual(5, l.Joined.Count);
        }

        [Test]
        public void Achievements_UnlockOnce_FromARealRun()
        {
            var unlocked = new List<AchievementId>();
            var tracker = new AchievementTracker(unlocked.Add);
            var run = new RunSimulation(new BoardTuningData { livesPerRun = 0 }, 42, 6);
            var driver = new IdealDriver(run);
            var none = new PlayerInputState[BoardSimulation.MaxPlayers];
            tracker.OnRunStart(humans: 6);
            float t = 0f;
            while (run.Distance < 3200f && t < 300f)
            {
                driver.Drive(BoardScenario.Dt);
                RunStepEvents ev = run.Step(BoardScenario.Dt, none);
                tracker.OnStep(ev, run);
                t += BoardScenario.Dt;
            }
            TestContext.WriteLine("unlocked: " + string.Join(", ", unlocked));
            CollectionAssert.Contains(unlocked, AchievementId.FullCrew);
            CollectionAssert.Contains(unlocked, AchievementId.FirstKilometre);
            CollectionAssert.Contains(unlocked, AchievementId.Unstoppable, "the ideal driver does not crash in 3 km");
            CollectionAssert.DoesNotContain(unlocked, AchievementId.Wipeout);
            Assert.AreEqual(unlocked.Count, new HashSet<AchievementId>(unlocked).Count, "each achievement reported once");
            var apiNames = new HashSet<string>();
            foreach (AchievementInfo a in AchievementCatalog.All) Assert.IsTrue(apiNames.Add(a.ApiName), "unique API names");
            Assert.AreEqual(Enum.GetValues(typeof(AchievementId)).Length, AchievementCatalog.All.Length, "every id has an entry");
        }

        [Test]
        public void MenuScreens_StayInsideTheSafeArea_OnPcAndSteamDeck()
        {
            var m = new FrontendModel(new GameSettings());
            m.Lobby.Join(JoinedDevice.Keyboard(false));
            var cmds = new List<HudCmd>();
            foreach (var (w, h) in new[] { (1920f, 1080f), (1280f, 800f), (1280f, 720f), (2560f, 1080f), (1024f, 768f) })
            foreach (MenuScreen screen in new[] { MenuScreen.Title, MenuScreen.Lobby, MenuScreen.Pause, MenuScreen.Settings, MenuScreen.HowToPlay, MenuScreen.Credits })
            {
                m.Open(screen, push: false);
                FrontendLayout.Build(m, 0f, w, h, 0, 0, w, h, cmds);
                float minFont = float.MaxValue;
                foreach (HudCmd c in cmds)
                {
                    if (c.Tex == HudTex.Vignette || (c.Tex == HudTex.White && c.W >= w)) continue;
                    float scale = c.Scale == 0f ? 1f : c.Scale;
                    float cx = c.X + c.W * 0.5f, cy = c.Y + c.H * 0.5f, ex = c.W * 0.5f * scale + 12f, ey = c.H * 0.5f * scale + 12f;
                    Assert.That(cx - ex >= -24f && cy - ey >= -24f && cx + ex <= w + 24f && cy + ey <= h + 24f,
                        $"{screen} @ {w}x{h}: '{c.Text ?? c.Tex.ToString()}' leaves the screen");
                    if (c.Text != null) minFont = Math.Min(minFont, c.FontSize);
                }
                if (w == 1280f && h == 800f) TestContext.WriteLine($"Steam Deck {screen}: smallest text {minFont:0.0} px");
                Assert.That(minFont, Is.GreaterThanOrEqualTo(9f), "Steam Deck legibility: at least 9 px text");
            }
        }
    }
}
