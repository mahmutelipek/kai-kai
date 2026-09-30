using UnityEngine;

namespace Game
{
    /// <summary>
    /// The chunky display font of the HUD and menus: bundled "Luckiest Guy" (Apache-2.0, Resources/Fonts), so
    /// it looks the same on every Steam machine (Windows, Linux / Steam Deck, macOS). OS fonts only as fallback.
    /// </summary>
    public static class HudFont
    {
        static Font _font;
        static bool _bundled;

        public static Font Get()
        {
            if (_font != null) return _font;
            _font = Resources.Load<Font>("Fonts/LuckiestGuy");
            _bundled = _font != null;
            if (_font == null) _font = Font.CreateDynamicFontFromOSFont(new[] { "Arial Black", "Impact", "Segoe UI Black", "Arial" }, 48);
            return _font;
        }

        /// <summary>The bundled font is already heavy: only slant it (like the reference); OS fallbacks get bold too.</summary>
        public static FontStyle Style(Font f) => _bundled && f == _font ? FontStyle.Italic : FontStyle.BoldAndItalic;
    }
}
