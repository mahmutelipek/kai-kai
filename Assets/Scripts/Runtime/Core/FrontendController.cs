using System;
using System.Collections.Generic;
using System.IO;
using Game.Frontend;
using Game.Hud;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game
{
    /// <summary>
    /// Steam-ready front end: title over an attract-mode ride, local co-op lobby (each gamepad / keyboard half
    /// joins its own rider, bots fill up to the crew size), pause (Esc / Start, also when a gamepad disconnects
    /// or the window loses focus), settings saved to persistentDataPath/settings.txt and applied to Screen /
    /// QualitySettings / audio. Everything is keyboard and gamepad navigable (Steam Deck, Big Picture,
    /// Remote Play Together). Menus are drawn with the HUD's draw commands (Game.Hud.FrontendLayout).
    /// </summary>
    public sealed class FrontendController : MonoBehaviour
    {
        const float RepeatDelay = 0.35f, RepeatRate = 0.12f;

        GameManager _gm;
        FrontendModel _model;
        readonly List<HudCmd> _cmds = new List<HudCmd>(160);
        string _settingsPath;
        Resolution[] _resolutions = Array.Empty<Resolution>();
        float _repeatTimer;
        int _heldDir;
        int _padCount;

        public FrontendModel Model => _model;
        public bool InMenu => _model != null && _model.Screen != MenuScreen.None;

        public void Initialize(GameManager gm)
        {
            _gm = gm;
            _settingsPath = Path.Combine(Application.persistentDataPath, "settings.txt");
            GameSettings settings = LoadSettings();
            _model = new FrontendModel(settings)
            {
                AllowQuit = Application.platform != RuntimePlatform.WebGLPlayer,
            };
            FillResolutions();
            ApplySettings(settings);
            _model.HighScores = _gm.Board.Run.HighScores?.Top;
            HudDrawer.Prewarm();
            _padCount = Gamepad.all.Count;
            GoToTitle();
        }

        // ------------------------------------------------------------------ flow

        void GoToTitle()
        {
            _gm.Feel.Paused = false;
            Time.timeScale = 1f;
            _gm.Board.Frozen = false;
            _gm.InputRouter.SetAttract(true);
            _gm.Board.Simulation.SetActivePlayerCount(6);
            _gm.Hud.Visible = false;
            if (_gm.Audio != null) _gm.Audio.Attract = true;
            _model.Lobby.Joined.Clear();
            _model.Open(MenuScreen.Title, push: false);
            _gm.Run.RestartRun(countdown: false);
            PlatformServices.Current.SetRichPresence("In the menu");
        }

        /// <summary>Starts a run with the lobby's riders (the lobby's START item; public for tests).</summary>
        public void StartRun()
        {
            Lobby lobby = _model.Lobby;
            _gm.InputRouter.SetAttract(false);
            _gm.InputRouter.BotsEnabled = lobby.BotsFill;
            _gm.InputRouter.SetJoinedDevices(lobby.Joined);
            _gm.Board.Simulation.SetActivePlayerCount(lobby.RiderCount);
            _model.Close();
            _gm.Hud.Visible = true;
            if (_gm.Audio != null) _gm.Audio.Attract = false;
            SaveSettings();
            _gm.Run.RestartRun();
            PlatformServices.Current.SetRichPresence($"Riding downhill with {lobby.Joined.Count} player(s)");
        }

        public void Pause()
        {
            if (InMenu || _gm.Board.Run.State == Simulation.RunState.Ended) return;
            _model.Open(MenuScreen.Pause, push: false);
            _gm.Board.Frozen = true;
            _gm.Feel.Paused = true;
            Time.timeScale = 0f;
            _gm.Audio?.Ui(Audio.Sfx.UiBack);
        }

        public void Resume()
        {
            _model.Close();
            _gm.Feel.Paused = false;
            Time.timeScale = 1f;
            _gm.Board.Frozen = _gm.Run.CountingDown;
        }

        // ------------------------------------------------------------------ input

        void Update()
        {
            if (_model == null) return;

            // a tip was shown: remember it (settings file, synced by Steam Cloud)
            int seen = _gm.Hud.Presenter.Tips.SeenMask;
            if (seen != _model.Settings.TipsSeen) { _model.Settings.TipsSeen = seen; SaveSettings(); }

            // a controller dropping out mid-run pauses the game (platform requirement on consoles, good manners on Steam)
            int pads = Gamepad.all.Count;
            if (pads < _padCount && !InMenu) Pause();
            _padCount = pads;

            if (!InMenu)
            {
                bool ended = _gm.Board.Run.State == Simulation.RunState.Ended;
                if (ended && BackPressed()) { GoToTitle(); return; }
                if (PausePressed()) Pause();
                return;
            }
            // the attract ride behind the title never stops
            if (_model.Lobby.Joined.Count == 0 && _model.Screen != MenuScreen.Pause && _gm.Board.Run.State == Simulation.RunState.Ended)
                _gm.Run.RestartRun(countdown: false);

            if (_model.Screen == MenuScreen.Lobby)
            {
                int riders = _model.Lobby.Joined.Count;
                HandleJoins();
                int now = _model.Lobby.Joined.Count;
                if (now > riders) _gm.Audio?.Ui(Audio.Sfx.Join);
                else if (now < riders) _gm.Audio?.Ui(Audio.Sfx.UiBack);
            }
            MenuInput input = ReadMenuInput();
            if (!input.Any) return;
            MenuScreen screenBefore = _model.Screen;
            int focusBefore = _model.Focus;
            MenuAction action = _model.Handle(input);
            UiSound(input, action, screenBefore, focusBefore);
            switch (action)
            {
                case MenuAction.StartRun: StartRun(); break;
                case MenuAction.Resume: Resume(); break;
                case MenuAction.Restart: Resume(); _gm.Run.RestartRun(); break;
                case MenuAction.MainMenu: GoToTitle(); break;
                case MenuAction.Quit: Quit(); break;
                case MenuAction.SettingsChanged: ApplySettings(_model.Settings); SaveSettings(); break;
                case MenuAction.LanguageChanged: ApplySettings(_model.Settings); _gm.Hud.Presenter.Invalidate(); SaveSettings(); break;
            }
        }

        /// <summary>Menu feedback: a tick when the focus or a value moves, a chime on confirm, a lower one on back.</summary>
        void UiSound(MenuInput input, MenuAction action, MenuScreen screenBefore, int focusBefore)
        {
            AudioDirector audio = _gm.Audio;
            if (audio == null) return;
            if (input.Back && (_model.Screen != screenBefore || action == MenuAction.Resume)) audio.Ui(Audio.Sfx.UiBack);
            else if (input.Submit && action != MenuAction.None || _model.Screen != screenBefore) audio.Ui(Audio.Sfx.UiSelect);
            else if (_model.Focus != focusBefore || action == MenuAction.SettingsChanged) audio.Ui(Audio.Sfx.UiMove);
        }

        void OnApplicationFocus(bool focus)
        {
            // Steam overlay (Shift+Tab) or alt-tab: pause instead of riding blind
            if (!focus && _model != null && !InMenu && !Application.isEditor) Pause();
        }

        static bool BackPressed()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb[Key.Escape].wasPressedThisFrame) return true;
            for (int i = 0; i < Gamepad.all.Count; i++) if (Gamepad.all[i].buttonEast.wasPressedThisFrame) return true;
            return false;
        }

        static bool PausePressed()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb[Key.Escape].wasPressedThisFrame) return true;
            for (int i = 0; i < Gamepad.all.Count; i++) if (Gamepad.all[i].startButton.wasPressedThisFrame) return true;
            return false;
        }

        /// <summary>Lobby joins: (A) / Space / Enter joins that device's rider, (B) / Backspace leaves.</summary>
        void HandleJoins()
        {
            Lobby lobby = _model.Lobby;
            for (int i = 0; i < Gamepad.all.Count; i++)
            {
                Gamepad pad = Gamepad.all[i];
                JoinedDevice d = JoinedDevice.Pad(pad.deviceId);
                if (pad.buttonSouth.wasPressedThisFrame && !lobby.IsJoined(d)) { lobby.Join(d); _consumeSubmit = true; }
                if (pad.buttonEast.wasPressedThisFrame && lobby.IsJoined(d)) { lobby.Leave(d); _consumeBack = true; }
            }
            Keyboard kb = Keyboard.current;
            if (kb == null) return;
            JoinedDevice left = JoinedDevice.Keyboard(false), right = JoinedDevice.Keyboard(true);
            if (kb[Key.Space].wasPressedThisFrame && !lobby.IsJoined(left)) { lobby.Join(left); _consumeSubmit = true; }
            if (kb[Key.Enter].wasPressedThisFrame && !lobby.IsJoined(right) && lobby.IsJoined(left)) { lobby.Join(right); _consumeSubmit = true; }
            if (kb[Key.Backspace].wasPressedThisFrame)
            {
                if (lobby.IsJoined(right)) lobby.Leave(right);
                else if (lobby.IsJoined(left)) lobby.Leave(left);
            }
        }

        bool _consumeSubmit, _consumeBack;

        MenuInput ReadMenuInput()
        {
            var input = new MenuInput();
            Keyboard kb = Keyboard.current;
            int dir = 0; // 1 up, 2 down, 3 left, 4 right
            if (kb != null)
            {
                if (kb[Key.UpArrow].isPressed || kb[Key.W].isPressed) dir = 1;
                else if (kb[Key.DownArrow].isPressed || kb[Key.S].isPressed) dir = 2;
                else if (kb[Key.LeftArrow].isPressed || kb[Key.A].isPressed) dir = 3;
                else if (kb[Key.RightArrow].isPressed || kb[Key.D].isPressed) dir = 4;
                input.Submit |= kb[Key.Enter].wasPressedThisFrame || kb[Key.Space].wasPressedThisFrame || kb[Key.NumpadEnter].wasPressedThisFrame;
                input.Back |= kb[Key.Escape].wasPressedThisFrame;
            }
            for (int i = 0; i < Gamepad.all.Count; i++)
            {
                Gamepad pad = Gamepad.all[i];
                Vector2 v = pad.leftStick.ReadValue() + pad.dpad.ReadValue();
                if (dir == 0 && v.sqrMagnitude > 0.35f)
                    dir = Mathf.Abs(v.y) >= Mathf.Abs(v.x) ? (v.y > 0f ? 1 : 2) : (v.x < 0f ? 3 : 4);
                input.Submit |= pad.buttonSouth.wasPressedThisFrame;
                input.Back |= pad.buttonEast.wasPressedThisFrame || (_model.Screen == MenuScreen.Pause && pad.startButton.wasPressedThisFrame);
            }
            if (_consumeSubmit) { input.Submit = false; _consumeSubmit = false; }
            if (_consumeBack) { input.Back = false; _consumeBack = false; }

            // held direction: fire once, then repeat (unscaled time: menus work while paused)
            if (dir != _heldDir) { _heldDir = dir; _repeatTimer = RepeatDelay; Fire(ref input, dir); }
            else if (dir != 0)
            {
                _repeatTimer -= Time.unscaledDeltaTime;
                if (_repeatTimer <= 0f) { _repeatTimer = RepeatRate; Fire(ref input, dir); }
            }
            return input;
        }

        static void Fire(ref MenuInput input, int dir)
        {
            input.Up |= dir == 1; input.Down |= dir == 2; input.Left |= dir == 3; input.Right |= dir == 4;
        }

        void OnGUI()
        {
            if (!InMenu || Event.current.type != EventType.Repaint) return;
            GUI.depth = -10; // above the HUD
            Rect safe = Screen.safeArea;
            FrontendLayout.Build(_model, Time.unscaledTime, Screen.width, Screen.height, safe.x, Screen.height - safe.yMax, safe.width, safe.height, _cmds);
            HudDrawer.Draw(_cmds);
        }

        void Quit()
        {
            SaveSettings();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ------------------------------------------------------------------ settings

        GameSettings LoadSettings()
        {
            var defaults = new GameSettings { Language = Language.English };
            try
            {
                return File.Exists(_settingsPath) ? GameSettings.Parse(File.ReadAllText(_settingsPath), defaults) : defaults;
            }
            catch (Exception e)
            {
                Debug.LogWarning("Downhill: could not read settings (" + e.Message + "), using defaults.");
                return defaults;
            }
        }

        void SaveSettings()
        {
            try
            {
                string tmp = _settingsPath + ".tmp";
                File.WriteAllText(tmp, _model.Settings.Serialize());
                if (File.Exists(_settingsPath)) File.Delete(_settingsPath);
                File.Move(tmp, _settingsPath);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Downhill: could not save settings: " + e.Message);
            }
        }

        void FillResolutions()
        {
            var list = new List<Resolution>();
            var names = new List<string>();
            foreach (Resolution r in Screen.resolutions)
            {
                bool dup = false;
                foreach (Resolution q in list) if (q.width == r.width && q.height == r.height) { dup = true; break; }
                if (dup || r.width < 1024) continue;
                list.Add(r);
                names.Add(r.width + " x " + r.height);
            }
            _resolutions = list.ToArray();
            _model.Resolutions = names.Count > 0 ? names.ToArray() : new[] { Screen.currentResolution.width + " x " + Screen.currentResolution.height };
        }

        void ApplySettings(GameSettings s)
        {
            Loc.Current = Language.English; // English only for now (the settings keep the field for later)
            CameraController.ReduceMotion = s.ReduceMotion;
            AudioListener.volume = s.MasterVolume;
            AudioDirector.MusicVolume = s.MusicVolume;
            if (_gm != null && _gm.Hud != null)
            {
                _gm.Hud.Presenter.Tips.Enabled = s.ShowTips;
                _gm.Hud.Presenter.Tips.SeenMask = s.TipsSeen;
            }
            AudioDirector.EffectsVolume = s.EffectsVolume;
            QualitySettings.vSyncCount = s.VSync ? 1 : 0;
            Application.targetFrameRate = s.VSync ? -1 : 144;
            int levels = QualitySettings.names.Length;
            if (levels > 0)
            {
                // spread Low / Medium / High over whatever quality levels the project defines
                int index = s.Quality == GraphicsQuality.Low ? 0 : s.Quality == GraphicsQuality.Medium ? levels / 2 : levels - 1;
                if (QualitySettings.GetQualityLevel() != index) QualitySettings.SetQualityLevel(index, true);
            }
            if (Application.isEditor) return; // the editor game view ignores window changes
            FullScreenMode mode = s.Display == DisplayMode.Fullscreen ? FullScreenMode.ExclusiveFullScreen
                : s.Display == DisplayMode.Borderless ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            Resolution native = Screen.currentResolution;
            int w = native.width, h = native.height;
            if (s.Resolution >= 0 && s.Resolution < _resolutions.Length) { w = _resolutions[s.Resolution].width; h = _resolutions[s.Resolution].height; }
            else if (mode == FullScreenMode.Windowed) { w = Mathf.RoundToInt(native.width * 0.75f); h = Mathf.RoundToInt(native.height * 0.75f); }
            Screen.SetResolution(w, h, mode);
        }
    }
}
