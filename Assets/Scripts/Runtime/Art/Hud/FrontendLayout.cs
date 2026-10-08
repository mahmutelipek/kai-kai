using System;
using System.Collections.Generic;
using Game.Frontend;
using static Game.Hud.HudLayout;

namespace Game.Hud
{
    /// <summary>
    /// Title, lobby, pause and settings screens drawn with the HUD's style (torn brush panels, Luckiest Guy,
    /// yellow focus). Same draw commands for Unity and the headless preview; anchored to the safe area.
    /// </summary>
    public static class FrontendLayout
    {
        static readonly string[] Tags = { "P1", "P2", "P3", "P4", "P5", "P6" };

        // geometry of the last Build, for mouse hit-testing (GUI space: origin top-left)
        static bool _listValid, _slotsValid;
        static readonly System.Collections.Generic.List<(float x0, float y0, float x1, float y1)> _hintRects = new System.Collections.Generic.List<(float, float, float, float)>(4);
        static float _listX, _listY, _listW, _listH, _listGap;
        static float _slotsX, _slotsY, _slotsW, _slotsH, _slotsGap;

        /// <summary>Menu item under the point (index into FrontendModel.Items), or -1. Fraction is 0..1 across the row.</summary>
        public static int HitItem(FrontendModel m, float x, float y, out float fraction)
        {
            fraction = 0f;
            if (!_listValid || InHint(x, y)) return -1; // the controls text along the bottom is not a button
            MenuItem[] items = m.Items;
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i] == MenuItem.Quit && !m.AllowQuit) continue;
                float iy = _listY + i * (_listH + _listGap);
                if (x < _listX || x > _listX + _listW || y < iy || y > iy + _listH) continue;
                fraction = (x - _listX) / _listW;
                return i;
            }
            return -1;
        }

        /// <summary>Rider card under the point (0..5), or -1.</summary>
        public static int HitSlot(float x, float y)
        {
            if (!_slotsValid || y < _slotsY || y > _slotsY + _slotsH) return -1;
            for (int i = 0; i < Lobby.MaxRiders; i++)
            {
                float sx = _slotsX + i * (_slotsW + _slotsGap);
                if (x >= sx && x <= sx + _slotsW) return i;
            }
            return -1;
        }

        public static void Build(FrontendModel m, float time, float screenW, float screenH, float safeX, float safeY, float safeW, float safeH, List<HudCmd> o)
        {
            o.Clear();
            _listValid = false; _slotsValid = false; _hintRects.Clear();
            if (m.Screen == MenuScreen.None) return;
            float k = ScaleFor(safeW, safeH);
            float cx = safeX + safeW * 0.5f, top = safeY, bottom = safeY + safeH;
            _hintWidth = Math.Min(1800f * k, safeW - 40f);


            switch (m.Screen)
            {
                case MenuScreen.Title:
                    Logo(o, cx, top + safeH * 0.2f, k, time);
                    List(m, o, cx, top + safeH * 0.37f, k, 460f);
                    break;
                case MenuScreen.Lobby:
                    Title(o, Loc.T("WHO'S RIDING?"), cx, top + 50f * k, k);
                    Slots(m, o, cx, top + 190f * k, k, time);
                    List(m, o, cx, top + 470f * k, k, 820f, compact: true);
                    Hint(o, cx, bottom - 110f * k, k, Loc.T("PRESS (A) / SPACE TO JOIN") + "   ·   " + Loc.T("ENTER: SECOND KEYBOARD RIDER (ARROWS)"));
                    Hint(o, cx, bottom - 64f * k, k, Loc.T("(B) / ESC: LEAVE") + "   ·   " + Loc.T("STAND WHERE YOU WANT THE BOARD TO GO!"));
                    break;
                case MenuScreen.Pause:
                    Title(o, Loc.T("PAUSED"), cx, top + safeH * 0.16f, k);
                    List(m, o, cx, top + safeH * 0.34f, k, 420f);
                    break;
                case MenuScreen.Settings:
                    Title(o, Loc.T("SETTINGS"), cx, top + 40f * k, k);
                    List(m, o, cx, top + 170f * k, k, 760f, compact: true);
                    break;
                case MenuScreen.HowToPlay:
                    Title(o, Loc.T("HOW TO PLAY"), cx, top + 30f * k, k);
                    HowToPlay(o, cx, top + 170f * k, k);
                    List(m, o, cx, bottom - 170f * k, k, 360f, compact: true);
                    break;
                case MenuScreen.HighScores:
                {
                    Title(o, Loc.T("HIGH SCORES"), cx, top + 30f * k, k);
                    RefreshRows(m.HighScores);
                    float tw = 760f * k;
                    ScoreTable.Draw(o, _rows, cx - tw * 0.5f, top + 200f * k, tw, k, time, 99f);
                    List(m, o, cx, bottom - 140f * k, k, 360f, compact: true);
                    break;
                }
                case MenuScreen.Credits:
                    Title(o, Loc.T("CREDITS"), cx, top + 50f * k, k);
                    Credits(o, cx, top + 220f * k, k);
                    List(m, o, cx, bottom - 150f * k, k, 360f, compact: true);
                    break;
            }
        }

        /// <summary>Two columns of three cards (fits the 1500-unit design width, so 4:3 and Steam Deck too).</summary>
        static void HowToPlay(List<HudCmd> o, float cx, float y, float k)
        {
            var cards = FrontendContent.HowToPlay;
            float cw = 720f * k, ch = 176f * k, gx = 40f * k, gy = 18f * k;
            for (int i = 0; i < cards.Length; i++)
            {
                float x = cx - cw - gx * 0.5f + (i % 2) * (cw + gx), cy = y + (i / 2) * (ch + gy);
                Img(o, HudTex.Card, x, cy, cw, ch, HudColor.Hex(0x14161E, 0.9f));
                Img(o, HudTex.Panel, x + 16f * k, cy + 14f * k, 440f * k, 58f * k, i % 2 == 0 ? Yellow : Cyan, skew: -0.15f);
                Txt(o, Loc.T(cards[i].heading), x + 46f * k, cy + 16f * k, 380f * k, 54f * k, 38f * k, Navy, HudAlign.Center, 0f);
                Txt(o, Loc.T(cards[i].line1), x + 30f * k, cy + 82f * k, cw - 60f * k, 38f * k, 25f * k, HudColor.White, HudAlign.Left, 2f * k);
                Txt(o, Loc.T(cards[i].line2), x + 30f * k, cy + 122f * k, cw - 60f * k, 38f * k, 25f * k, HudColor.Hex(0xD8DCEA), HudAlign.Left, 2f * k);
            }
        }

        static void Credits(List<HudCmd> o, float cx, float y, float k)
        {
            var lines = FrontendContent.Credits;
            Img(o, HudTex.Card, cx - 740f * k, y - 34f * k, 1480f * k, lines.Length * 112f * k + 40f * k, HudColor.Hex(0x14161E, 0.88f));
            for (int i = 0; i < lines.Length; i++)
            {
                float ly = y + i * 112f * k;
                Txt(o, Loc.T(lines[i].role), cx - 600f * k, ly, 1200f * k, 40f * k, 28f * k, Cyan, HudAlign.Center, 2f * k);
                Txt(o, lines[i].name, cx - 700f * k, ly + 40f * k, 1400f * k, 56f * k, 42f * k, HudColor.White, HudAlign.Center, 3f * k);
            }
        }

        static void Logo(List<HudCmd> o, float cx, float y, float k, float time)
        {
            float bob = (float)Math.Sin(time * 2f) * 4f * k;
            Img(o, HudTex.Panel, cx - 520f * k, y - 90f * k + bob, 1040f * k, 190f * k, HudColor.Hex(0x0B0C12, 0.92f), rot: -4f);
            Txt(o, "DOWNHILL", cx - 500f * k, y - 92f * k + bob, 1000f * k, 110f * k, 104f * k, Yellow, HudAlign.Center, 6f * k);
            Txt(o, "PARTY BOARD", cx - 500f * k, y + 6f * k + bob, 1000f * k, 90f * k, 78f * k, Cyan, HudAlign.Center, 5f * k);
        }

        static void Title(List<HudCmd> o, string text, float cx, float y, float k)
        {
            Img(o, HudTex.Panel, cx - 360f * k, y, 720f * k, 110f * k, HudColor.Hex(0x0B0C12, 0.92f), rot: -3f);
            Txt(o, text, cx - 340f * k, y + 6f * k, 680f * k, 96f * k, 70f * k, Yellow, HudAlign.Center, 4f * k);
        }

        static float _hintWidth;
        static readonly List<ScoreRow> _rows = new List<ScoreRow>(10);
        static int _rowsCount = -1;
        static float _rowsTop = -1f;

        /// <summary>Formats the table only when it changed (menus stay allocation-free while idle).</summary>
        static void RefreshRows(IReadOnlyList<Game.Simulation.HighScoreEntry> top)
        {
            int count = top != null ? top.Count : 0;
            float first = count > 0 ? top[0].Score + top[count - 1].Score : 0f;
            if (count == _rowsCount && first == _rowsTop) return;
            _rowsCount = count;
            _rowsTop = first;
            ScoreTable.Fill(top, 0, _rows);
        }

        static void Hint(List<HudCmd> o, float cx, float y, float k, string text)
        {
            _hintRects.Add((cx - _hintWidth * 0.5f, y, cx + _hintWidth * 0.5f, y + 44f * k));
            Txt(o, text, cx - _hintWidth * 0.5f, y, _hintWidth, 44f * k, 28f * k, HudColor.Hex(0xE8ECF5), HudAlign.Center, 3f * k);
        }

        static bool InHint(float x, float y)
        {
            for (int i = 0; i < _hintRects.Count; i++)
                if (x >= _hintRects[i].x0 && x <= _hintRects[i].x1 && y >= _hintRects[i].y0 && y <= _hintRects[i].y1) return true;
            return false;
        }

        /// <summary>Vertical list; the focused item is a yellow panel with navy text, slightly bigger.</summary>
        static void List(FrontendModel m, List<HudCmd> o, float cx, float y, float k, float width, bool compact = false)
        {
            MenuItem[] items = m.Items;
            float h = (compact ? 66f : 84f) * k, gap = (compact ? 10f : 16f) * k, w = width * k;
            _listValid = true; _listX = cx - w * 0.5f; _listY = y; _listW = w; _listH = h; _listGap = gap;
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i] == MenuItem.Quit && !m.AllowQuit) continue;
                bool focus = m.MouseMode ? i == m.Hover : i == m.Focus;
                (string label, string value) = m.Describe(items[i]);
                float iy = y + i * (h + gap);
                float scale = focus ? 1.06f : 1f;
                Img(o, HudTex.Panel, cx - w * 0.5f, iy, w, h, focus ? Yellow : HudColor.Hex(0x14161E, 0.88f), skew: -0.12f, scale: scale);
                HudColor text = focus ? Navy : HudColor.White;
                float size = (compact ? 38f : 50f) * k;
                if (value == null)
                    Txt(o, label, cx - w * 0.5f, iy + 4f * k, w, h - 8f * k, size, text, HudAlign.Center, focus ? 0f : 3f * k, scale);
                else
                {
                    Txt(o, label, cx - w * 0.5f + 36f * k, iy + 4f * k, w * 0.55f, h - 8f * k, size, text, HudAlign.Left, focus ? 0f : 3f * k, scale);
                    Txt(o, "<  " + value + "  >", cx, iy + 4f * k, w * 0.5f - 36f * k, h - 8f * k, size, focus ? Navy : Cyan, HudAlign.Right, focus ? 0f : 3f * k, scale);
                }
            }
        }

        /// <summary>Six rider cards: avatar, tag, and who drives it (device, bot, or "join").</summary>
        static void Slots(FrontendModel m, List<HudCmd> o, float cx, float y, float k, float time)
        {
            Lobby lobby = m.Lobby;
            float cw = 190f * k, ch = 250f * k, gap = 22f * k, x0 = cx - (Lobby.MaxRiders * cw + (Lobby.MaxRiders - 1) * gap) * 0.5f;
            _slotsValid = true; _slotsX = x0; _slotsY = y; _slotsW = cw; _slotsH = ch; _slotsGap = gap;
            int riders = lobby.RiderCount;
            for (int i = 0; i < Lobby.MaxRiders; i++)
            {
                float x = x0 + i * (cw + gap);
                bool human = i < lobby.Joined.Count;
                bool bot = !human && i < riders;
                HudColor pc = PlayerColors[i];
                Img(o, HudTex.Pill, x, y, cw, ch, human ? HudColor.Hex(0x1C2030, 0.95f) : HudColor.Hex(0x14161E, 0.7f));
                if (human) Img(o, HudTex.White, x + 14f * k, y + ch - 12f * k, cw - 28f * k, 6f * k, pc);
                float pulse = human ? 1f : 0.92f + 0.05f * (float)Math.Sin(time * 4f + i);
                Img(o, (HudTex)((int)HudTex.Avatar0 + i), x + cw * 0.5f - 60f * k, y + 16f * k, 120f * k, 120f * k,
                    human || bot ? HudColor.White : new HudColor(0.35f, 0.35f, 0.4f, 0.8f), scale: human ? 1f : pulse);
                Txt(o, Tags[i], x, y + 138f * k, cw, 48f * k, 40f * k, human || bot ? pc : pc.WithAlpha(0.5f), HudAlign.Center, 3f * k);
                string who = human ? DeviceName(lobby.Joined[i]) : bot ? Loc.T("BOT") : Loc.T("JOIN");
                Txt(o, who, x, y + 186f * k, cw, 40f * k, 26f * k, human ? HudColor.White : HudColor.Hex(0x9AA0B4), HudAlign.Center, 2f * k);
            }
        }

        static string DeviceName(JoinedDevice d) =>
            d.Kind == DeviceKind.KeyboardLeft ? Loc.T("KEYBOARD") : d.Kind == DeviceKind.KeyboardRight ? Loc.T("KEYBOARD (ARROWS)") : Loc.T("GAMEPAD");
    }
}
