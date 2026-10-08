using System;
using System.Collections.Generic;

namespace Game.Frontend
{
    public enum MenuScreen : byte { None, Title, Lobby, Pause, Settings, HowToPlay, Credits, HighScores }

    public enum MenuAction : byte
    {
        None, StartRun, Resume, Restart, MainMenu, Quit, SettingsChanged, LanguageChanged,
    }

    public enum MenuItem : byte
    {
        Play, Settings, Quit,
        Resume, Restart, MainMenu,
        DisplayMode, Resolution, VSync, Quality, ReduceMotion, Language, MasterVolume, MusicVolume, EffectsVolume,
        CrewSize, BotsFill, Start, Back,
        HowToPlay, Credits, Tips, HighScores,
    }

    /// <summary>Menu navigation from any device this frame (keyboard, every gamepad, Steam Input).</summary>
    public struct MenuInput
    {
        public bool Up, Down, Left, Right, Submit, Back;
        public bool Any => Up || Down || Left || Right || Submit || Back;
    }

    public enum DeviceKind : byte { KeyboardLeft, KeyboardRight, Gamepad }

    /// <summary>A physical input that joined the crew: WASD half, arrows half, or one gamepad (by device id).</summary>
    public struct JoinedDevice : IEquatable<JoinedDevice>
    {
        public DeviceKind Kind;
        public int GamepadId;
        public static JoinedDevice Keyboard(bool right) => new JoinedDevice { Kind = right ? DeviceKind.KeyboardRight : DeviceKind.KeyboardLeft };
        public static JoinedDevice Pad(int id) => new JoinedDevice { Kind = DeviceKind.Gamepad, GamepadId = id };
        public bool Equals(JoinedDevice o) => Kind == o.Kind && (Kind != DeviceKind.Gamepad || GamepadId == o.GamepadId);
        public override bool Equals(object obj) => obj is JoinedDevice o && Equals(o);
        public override int GetHashCode() => (int)Kind * 1000 + GamepadId;
    }

    /// <summary>Local co-op lobby: up to 4 riders join with their own device, bots fill up to the crew size.</summary>
    public sealed class Lobby
    {
        public const int MaxRiders = 4;
        public readonly List<JoinedDevice> Joined = new List<JoinedDevice>(MaxRiders);
        public int CrewSize = 1;
        public bool BotsFill = true;

        public bool IsJoined(JoinedDevice d) => Joined.Contains(d);

        public bool Join(JoinedDevice d)
        {
            if (Joined.Contains(d) || Joined.Count >= MaxRiders) return false;
            Joined.Add(d);
            if (CrewSize < Joined.Count) CrewSize = Joined.Count;
            return true;
        }

        public bool Leave(JoinedDevice d) => Joined.Remove(d);

        /// <summary>Riders on the board when the run starts: every human, plus bots up to the crew size.</summary>
        public int RiderCount => BotsFill ? Math.Max(Math.Max(1, Joined.Count), CrewSize) : Math.Max(1, Joined.Count);
    }

    /// <summary>
    /// The front end as a small state machine: Title -> Lobby -> (run) -> Pause -> Settings, with a back stack
    /// and one focused item per screen. Engine-free: Unity feeds MenuInput and executes the returned actions;
    /// FrontendLayout draws it with the HUD's draw commands (skill: game-ui-ux, "screens as a stack",
    /// "always focus one control").
    /// </summary>
    public sealed class FrontendModel
    {
        static readonly MenuItem[] TitleItems = { MenuItem.Play, MenuItem.HowToPlay, MenuItem.HighScores, MenuItem.Settings, MenuItem.Credits, MenuItem.Quit };
        static readonly MenuItem[] LobbyItems = { MenuItem.CrewSize, MenuItem.BotsFill, MenuItem.Start, MenuItem.Back };
        static readonly MenuItem[] PauseItems = { MenuItem.Resume, MenuItem.Restart, MenuItem.HowToPlay, MenuItem.Settings, MenuItem.MainMenu, MenuItem.Quit };
        static readonly MenuItem[] BackOnly = { MenuItem.Back };
        static readonly MenuItem[] SettingsItems =
        {
            // Language is hidden until more languages ship (English only for now; Loc keeps the Turkish table)
            MenuItem.DisplayMode, MenuItem.Resolution, MenuItem.VSync, MenuItem.Quality, MenuItem.ReduceMotion, MenuItem.Tips,
            MenuItem.MasterVolume, MenuItem.MusicVolume, MenuItem.EffectsVolume, MenuItem.Back,
        };

        readonly Stack<MenuScreen> _back = new Stack<MenuScreen>(4);

        public MenuScreen Screen { get; private set; } = MenuScreen.Title;
        public int Focus { get; private set; }
        /// <summary>The pointer was used last: only the row under it is highlighted (nothing when it is over empty space), instead of the keyboard focus.</summary>
        public bool MouseMode;
        /// <summary>Row under the pointer (index into Items), or -1.</summary>
        public int Hover = -1;
        public GameSettings Settings;
        public readonly Lobby Lobby = new Lobby();
        /// <summary>Resolution names shown in Settings (filled by the platform, e.g. "1920 x 1080").</summary>
        public string[] Resolutions = { "NATIVE" };
        /// <summary>The local top-10 table shown on the HIGH SCORES screen (live list from the HighScoreManager).</summary>
        public System.Collections.Generic.IReadOnlyList<Game.Simulation.HighScoreEntry> HighScores;
        /// <summary>Hide the Quit item (consoles / web builds would).</summary>
        public bool AllowQuit = true;

        public FrontendModel(GameSettings settings)
        {
            Settings = settings;
            Lobby.CrewSize = settings.CrewSize;
            Lobby.BotsFill = settings.BotsFill;
        }

        public MenuItem[] Items
        {
            get
            {
                switch (Screen)
                {
                    case MenuScreen.Title: return TitleItems;
                    case MenuScreen.Lobby: return LobbyItems;
                    case MenuScreen.Pause: return PauseItems;
                    case MenuScreen.Settings: return SettingsItems;
                    case MenuScreen.HowToPlay:
                    case MenuScreen.HighScores:
                    case MenuScreen.Credits: return BackOnly;
                    default: return Array.Empty<MenuItem>();
                }
            }
        }

        public void SetFocus(int index)
        {
            if (index >= 0 && index < Items.Length) Focus = index;
        }

        public MenuItem Focused => Items.Length > 0 ? Items[Math.Min(Focus, Items.Length - 1)] : MenuItem.Back;

        public void Open(MenuScreen screen, bool push = true)
        {
            if (push && Screen != MenuScreen.None && Screen != screen) _back.Push(Screen);
            if (!push) _back.Clear();
            Screen = screen;
            Focus = 0;
            if (screen == MenuScreen.Lobby) Focus = Array.IndexOf(LobbyItems, MenuItem.Start);
        }

        public void Close()
        {
            _back.Clear();
            Screen = MenuScreen.None;
            Focus = 0;
        }

        /// <summary>Goes back one screen; returns false when there is nowhere to go back to.</summary>
        public bool Back()
        {
            if (_back.Count == 0) return false;
            Screen = _back.Pop();
            Focus = 0;
            return true;
        }

        public MenuAction Handle(MenuInput input)
        {
            MenuItem[] items = Items;
            if (items.Length == 0) return MenuAction.None;
            if (input.Up) Focus = (Focus + items.Length - 1) % items.Length;
            if (input.Down) Focus = (Focus + 1) % items.Length;
            if (Focus == Array.IndexOf(items, MenuItem.Quit) && !AllowQuit) Focus = (Focus + (input.Up ? items.Length - 1 : 1)) % items.Length;
            if (input.Back)
            {
                if (Screen == MenuScreen.Pause) return MenuAction.Resume;
                Back();
                return MenuAction.None;
            }
            int dir = input.Right ? 1 : input.Left ? -1 : 0;
            if (dir != 0) return Adjust(Focused, dir);
            if (input.Submit) return Activate(Focused);
            return MenuAction.None;
        }

        MenuAction Activate(MenuItem item)
        {
            switch (item)
            {
                case MenuItem.Play: Open(MenuScreen.Lobby); return MenuAction.None;
                case MenuItem.Settings: Open(MenuScreen.Settings); return MenuAction.None;
                case MenuItem.HowToPlay: Open(MenuScreen.HowToPlay); return MenuAction.None;
                case MenuItem.Credits: Open(MenuScreen.Credits); return MenuAction.None;
                case MenuItem.HighScores: Open(MenuScreen.HighScores); return MenuAction.None;
                case MenuItem.Quit: return AllowQuit ? MenuAction.Quit : MenuAction.None;
                case MenuItem.Resume: return MenuAction.Resume;
                case MenuItem.Restart: return MenuAction.Restart;
                case MenuItem.MainMenu: return MenuAction.MainMenu;
                case MenuItem.Start:
                    Settings.CrewSize = Lobby.CrewSize;
                    Settings.BotsFill = Lobby.BotsFill;
                    return Lobby.Joined.Count > 0 ? MenuAction.StartRun : MenuAction.None;
                case MenuItem.Back: Back(); return MenuAction.None;
                default: return Adjust(item, 1); // toggles and cyclers also react to Submit
            }
        }

        MenuAction Adjust(MenuItem item, int dir)
        {
            GameSettings s = Settings;
            switch (item)
            {
                case MenuItem.DisplayMode: s.Display = (DisplayMode)Wrap((int)s.Display + dir, 3); return MenuAction.SettingsChanged;
                case MenuItem.Resolution: s.Resolution = Wrap(s.Resolution + 1 + dir, Resolutions.Length + 1) - 1; return MenuAction.SettingsChanged;
                case MenuItem.VSync: s.VSync = !s.VSync; return MenuAction.SettingsChanged;
                case MenuItem.Quality: s.Quality = (GraphicsQuality)Wrap((int)s.Quality + dir, 3); return MenuAction.SettingsChanged;
                case MenuItem.ReduceMotion: s.ReduceMotion = !s.ReduceMotion; return MenuAction.SettingsChanged;
                case MenuItem.Tips:
                    s.ShowTips = !s.ShowTips;
                    if (s.ShowTips) s.TipsSeen = 0; // switching tips back on replays them from the start
                    return MenuAction.SettingsChanged;
                case MenuItem.Language: s.Language = (Language)Wrap((int)s.Language + dir, Loc.LanguageNames.Length); return MenuAction.LanguageChanged;
                case MenuItem.MasterVolume: s.MasterVolume = Step(s.MasterVolume, dir); return MenuAction.SettingsChanged;
                case MenuItem.MusicVolume: s.MusicVolume = Step(s.MusicVolume, dir); return MenuAction.SettingsChanged;
                case MenuItem.EffectsVolume: s.EffectsVolume = Step(s.EffectsVolume, dir); return MenuAction.SettingsChanged;
                case MenuItem.CrewSize: Lobby.CrewSize = Math.Max(Math.Max(1, Lobby.Joined.Count), Math.Min(Lobby.MaxRiders, Lobby.CrewSize + dir)); return MenuAction.None;
                case MenuItem.BotsFill: Lobby.BotsFill = !Lobby.BotsFill; return MenuAction.None;
                default: return MenuAction.None;
            }
        }

        static int Wrap(int v, int n) => ((v % n) + n) % n;
        static float Step(float v, int dir) => (float)Math.Round(Math.Max(0f, Math.Min(1f, v + dir * 0.1f)), 1);

        /// <summary>Label and current value of an item in the current language.</summary>
        public (string label, string value) Describe(MenuItem item)
        {
            GameSettings s = Settings;
            switch (item)
            {
                case MenuItem.Play: return (Loc.T("PLAY"), null);
                case MenuItem.Settings: return (Loc.T("SETTINGS"), null);
                case MenuItem.Quit: return (Loc.T("QUIT"), null);
                case MenuItem.Resume: return (Loc.T("RESUME"), null);
                case MenuItem.Restart: return (Loc.T("RESTART"), null);
                case MenuItem.MainMenu: return (Loc.T("MAIN MENU"), null);
                case MenuItem.Start: return (Loc.T("START"), null);
                case MenuItem.Back: return (Loc.T("BACK"), null);
                case MenuItem.HowToPlay: return (Loc.T("HOW TO PLAY"), null);
                case MenuItem.Credits: return (Loc.T("CREDITS"), null);
                case MenuItem.HighScores: return (Loc.T("HIGH SCORES"), null);
                case MenuItem.Tips: return (Loc.T("RIDING TIPS"), Loc.T(s.ShowTips ? "ON" : "OFF"));
                case MenuItem.DisplayMode: return (Loc.T("DISPLAY MODE"), Loc.T(s.Display == DisplayMode.Fullscreen ? "FULLSCREEN" : s.Display == DisplayMode.Borderless ? "BORDERLESS" : "WINDOWED"));
                case MenuItem.Resolution: return (Loc.T("RESOLUTION"), s.Resolution < 0 || s.Resolution >= Resolutions.Length ? Loc.T("NATIVE") : Resolutions[s.Resolution]);
                case MenuItem.VSync: return (Loc.T("VSYNC"), Loc.T(s.VSync ? "ON" : "OFF"));
                case MenuItem.Quality: return (Loc.T("QUALITY"), Loc.T(s.Quality == GraphicsQuality.Low ? "LOW" : s.Quality == GraphicsQuality.Medium ? "MEDIUM" : "HIGH"));
                case MenuItem.ReduceMotion: return (Loc.T("REDUCE MOTION"), Loc.T(s.ReduceMotion ? "ON" : "OFF"));
                case MenuItem.Language: return (Loc.T("LANGUAGE"), Loc.LanguageNames[(int)s.Language]);
                case MenuItem.MasterVolume: return (Loc.T("MASTER VOLUME"), Percent(s.MasterVolume));
                case MenuItem.MusicVolume: return (Loc.T("MUSIC VOLUME"), Percent(s.MusicVolume));
                case MenuItem.EffectsVolume: return (Loc.T("EFFECTS VOLUME"), Percent(s.EffectsVolume));
                case MenuItem.CrewSize: return (Loc.T("CREW SIZE"), Lobby.CrewSize.ToString());
                case MenuItem.BotsFill: return (Loc.T("BOTS FILL EMPTY SPOTS"), Loc.T(Lobby.BotsFill ? "ON" : "OFF"));
                default: return (item.ToString(), null);
            }
        }

        static string Percent(float v) => ((int)Math.Round(v * 100f)).ToString() + "%";
    }
}
