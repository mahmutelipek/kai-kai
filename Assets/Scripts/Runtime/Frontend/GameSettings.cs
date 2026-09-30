using System;
using System.Globalization;
using System.Text;

namespace Game.Frontend
{
    public enum DisplayMode : byte { Fullscreen = 0, Borderless = 1, Windowed = 2 }
    public enum GraphicsQuality : byte { Low = 0, Medium = 1, High = 2 }

    /// <summary>
    /// Player settings, stored as a small key=value text file (persistentDataPath/settings.txt, which Steam
    /// Auto-Cloud can sync). Unknown keys are ignored and missing keys keep their defaults, so old files load.
    /// Engine-free; Unity's SettingsApplier turns it into Screen / QualitySettings / audio calls.
    /// </summary>
    public sealed class GameSettings
    {
        public DisplayMode Display = DisplayMode.Borderless;
        /// <summary>Index into the machine's resolution list; -1 = native (desktop) resolution.</summary>
        public int Resolution = -1;
        public bool VSync = true;
        public GraphicsQuality Quality = GraphicsQuality.High;
        public bool ReduceMotion;
        public Language Language = Language.English;
        public float MasterVolume = 0.8f, MusicVolume = 0.7f, EffectsVolume = 0.9f;
        /// <summary>Crew size the lobby starts with (bots fill up to it).</summary>
        public int CrewSize = 6;
        public bool BotsFill = true;
        /// <summary>First-run coaching tips during rides; <see cref="TipsSeen"/> is a bit mask of tips already shown.</summary>
        public bool ShowTips = true;
        public int TipsSeen;

        public GameSettings Clone() => (GameSettings)MemberwiseClone();

        public string Serialize()
        {
            var ci = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.Append("version=1\n");
            sb.Append("display=").Append((int)Display).Append('\n');
            sb.Append("resolution=").Append(Resolution).Append('\n');
            sb.Append("vsync=").Append(VSync ? 1 : 0).Append('\n');
            sb.Append("quality=").Append((int)Quality).Append('\n');
            sb.Append("reduceMotion=").Append(ReduceMotion ? 1 : 0).Append('\n');
            sb.Append("tips=").Append(ShowTips ? 1 : 0).Append('\n');
            sb.Append("tipsSeen=").Append(TipsSeen).Append('\n');
            sb.Append("language=").Append((int)Language).Append('\n');
            sb.Append("master=").Append(MasterVolume.ToString("0.###", ci)).Append('\n');
            sb.Append("music=").Append(MusicVolume.ToString("0.###", ci)).Append('\n');
            sb.Append("effects=").Append(EffectsVolume.ToString("0.###", ci)).Append('\n');
            sb.Append("crew=").Append(CrewSize).Append('\n');
            sb.Append("botsFill=").Append(BotsFill ? 1 : 0).Append('\n');
            return sb.ToString();
        }

        public static GameSettings Parse(string text, GameSettings defaults = null)
        {
            GameSettings s = (defaults ?? new GameSettings()).Clone();
            if (string.IsNullOrEmpty(text)) return s;
            var ci = CultureInfo.InvariantCulture;
            foreach (string raw in text.Split('\n'))
            {
                string line = raw.Trim();
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string key = line.Substring(0, eq), value = line.Substring(eq + 1);
                bool isInt = int.TryParse(value, NumberStyles.Integer, ci, out int iv);
                bool isFloat = float.TryParse(value, NumberStyles.Float, ci, out float fv);
                switch (key)
                {
                    case "display": if (isInt && iv >= 0 && iv <= 2) s.Display = (DisplayMode)iv; break;
                    case "resolution": if (isInt && iv >= -1) s.Resolution = iv; break;
                    case "vsync": if (isInt) s.VSync = iv != 0; break;
                    case "quality": if (isInt && iv >= 0 && iv <= 2) s.Quality = (GraphicsQuality)iv; break;
                    case "reduceMotion": if (isInt) s.ReduceMotion = iv != 0; break;
                    case "language": if (isInt && iv >= 0 && iv < Loc.LanguageNames.Length) s.Language = (Language)iv; break;
                    case "master": if (isFloat) s.MasterVolume = Clamp01(fv); break;
                    case "music": if (isFloat) s.MusicVolume = Clamp01(fv); break;
                    case "effects": if (isFloat) s.EffectsVolume = Clamp01(fv); break;
                    case "crew": if (isInt) s.CrewSize = Math.Max(1, Math.Min(6, iv)); break;
                    case "botsFill": if (isInt) s.BotsFill = iv != 0; break;
                    case "tips": if (isInt) s.ShowTips = iv != 0; break;
                    case "tipsSeen": if (isInt && iv >= 0) s.TipsSeen = iv; break;
                }
            }
            return s;
        }

        /// <summary>First-run language from the OS language name (e.g. "Turkish").</summary>
        public static Language LanguageFor(string systemLanguage) =>
            string.Equals(systemLanguage, "Turkish", StringComparison.OrdinalIgnoreCase) ? Language.Turkish : Language.English;

        static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
