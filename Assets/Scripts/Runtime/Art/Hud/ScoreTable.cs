using System;
using System.Collections.Generic;
using System.Globalization;
using Game.Frontend;
using Game.Simulation;
using static Game.Hud.HudLayout;

namespace Game.Hud
{
    public struct ScoreRow
    {
        public string Rank, Score, Distance, Crew, Date;
        public bool Highlight;
    }

    /// <summary>
    /// The local TOP SCORES table (end screen and the HIGH SCORES menu): rank, score, distance, crew size, date; the
    /// run that just made it in is highlighted with a pulsing NEW tag. Rows are formatted once (Fill), drawn every frame.
    /// </summary>
    public static class ScoreTable
    {
        static readonly CultureInfo Ci = CultureInfo.InvariantCulture;

        public static void Fill(IReadOnlyList<HighScoreEntry> top, int highlightRank, List<ScoreRow> into)
        {
            into.Clear();
            if (top == null) return;
            for (int i = 0; i < top.Count; i++)
            {
                HighScoreEntry e = top[i];
                into.Add(new ScoreRow
                {
                    Rank = "#" + (i + 1).ToString(Ci),
                    Score = GameRules.Scoring ? ((long)e.Score).ToString("N0", Ci) : ((long)e.Distance).ToString("N0", Ci) + " m",
                    Distance = GameRules.Scoring ? ((long)e.Distance).ToString("N0", Ci) + " m" : "",
                    Crew = "x" + e.Crew.ToString(Ci),
                    Date = e.Date ?? "",
                    Highlight = i + 1 == highlightRank,
                });
            }
        }

        static readonly HudColor Gold = HudColor.Hex(0xFFC21F), Silver = HudColor.Hex(0xC9D3E0), Bronze = HudColor.Hex(0xE0894A);

        /// <summary>Height the card needs (at k = 1): title, podium for the top three, list for #4..#10.</summary>
        public static float Height(int rows) => 330f + Math.Max(1, HighScoreManager.TableSize - 3) * ListRow + 24f;

        const float ListRow = 36f;

        /// <param name="reveal">Seconds since the table appeared (podium rises, then the list slides in); large = all shown.</param>
        public static void Draw(List<HudCmd> o, List<ScoreRow> rows, float x, float y, float w, float k, float time, float reveal)
        {
            if (reveal <= 0f) return; // not its turn yet (the end screen reveals the summary first)
            float h = Height(HighScoreManager.TableSize) * k;
            float a0 = Clamp01(reveal / 0.2f);
            Img(o, HudTex.Card, x, y, w, h, HudColor.Hex(0x14161E, 0.92f * a0));
            Img(o, HudTex.Panel, x + w * 0.5f - 200f * k, y - 30f * k, 400f * k, 84f * k, Cyan.WithAlpha(a0), rot: -3f);
            Txt(o, Loc.T("TOP SCORES"), x + w * 0.5f - 200f * k, y - 22f * k, 400f * k, 70f * k, 48f * k, Navy.WithAlpha(a0), HudAlign.Center, 0f);
            if (rows.Count == 0)
            {
                Txt(o, Loc.T("NO RUNS YET - GO RIDE!"), x, y + 220f * k, w, 50f * k, 32f * k, HudColor.White.WithAlpha(a0), HudAlign.Center, 2f * k);
                return;
            }

            // ---- podium: #2 left, #1 centre (tallest), #3 right; medal, score and distance stand on each block
            float bw = w * 0.3f, gap = w * 0.035f, baseY = y + 318f * k;
            for (int place = 0; place < 3; place++)
            {
                int slot = place == 0 ? 1 : place == 1 ? 0 : 2;             // screen column of this place
                float bx = x + w * 0.5f - bw * 1.5f - gap + slot * (bw + gap);
                float bh = (place == 0 ? 128f : place == 1 ? 96f : 74f) * k;
                // blocks rise one after another: #3, #2, then #1
                float t = Clamp01((reveal - 0.15f - (2 - place) * 0.15f) / 0.25f);
                if (t <= 0f) continue;
                float rise = EaseOutBack(t);
                HudColor medal = place == 0 ? Gold : place == 1 ? Silver : Bronze;
                bool has = place < rows.Count;
                bool mine = has && rows[place].Highlight;
                float pulse = mine ? 1f + 0.05f * (float)Math.Sin(time * 6f) : 1f;
                float top = baseY - bh * rise;
                Img(o, HudTex.Card, bx, top, bw, bh * rise, (has ? medal : HudColor.Hex(0x3A3D48)).WithAlpha(0.95f * t), scale: pulse);
                Txt(o, (place + 1).ToString(Ci), bx, top + 6f * k, bw, Math.Max(1f, bh * rise - 10f * k), (place == 0 ? 72f : 56f) * k,
                    HudColor.Hex(0x14161E, 0.55f * t), HudAlign.Center, 0f);
                if (!has) continue;
                ScoreRow r = rows[place];
                float stand = top - 8f * k;
                float scoreSize = Fit((place == 0 ? 36f : 30f) * k, bw + gap, r.Score);
                Txt(o, r.Distance + "  " + r.Crew, bx - gap * 0.5f, stand - 30f * k, bw + gap, 28f * k, Fit(21f * k, bw + gap, r.Distance + "  " + r.Crew),
                    HudColor.Hex(0xD8DCEA, t), HudAlign.Center, 2f * k);
                Txt(o, r.Score, bx - gap * 0.5f, stand - 72f * k, bw + gap, 44f * k, scoreSize, (mine ? Yellow : HudColor.White).WithAlpha(t), HudAlign.Center, 3f * k, pulse);
                Img(o, HudTex.Circle, bx + bw * 0.5f - 26f * k, stand - 132f * k, 52f * k, 52f * k, medal.WithAlpha(t), scale: pulse);
                Txt(o, "#" + (place + 1).ToString(Ci), bx + bw * 0.5f - 26f * k, stand - 126f * k, 52f * k, 40f * k, 26f * k, Navy.WithAlpha(t), HudAlign.Center, 0f);
                if (mine)
                {
                    Img(o, HudTex.Panel, bx + bw - 62f * k, stand - 138f * k, 80f * k, 34f * k, Red.WithAlpha(t), rot: 8f);
                    Txt(o, Loc.T("NEW"), bx + bw - 62f * k, stand - 137f * k, 80f * k, 32f * k, 21f * k, HudColor.White.WithAlpha(t), HudAlign.Center, 0f);
                }
            }

            // ---- #4..#10 list under the podium
            float ry = baseY + 16f * k;
            float cRank = x + 26f * k, cScore = x + 96f * k, cDist = x + w * 0.52f, cCrew = x + w * 0.8f;
            float scoreW = cDist - cScore - 10f * k;
            for (int i = 3; i < rows.Count; i++)
            {
                ScoreRow r = rows[i];
                float t = Clamp01((reveal - 0.75f - (i - 3) * 0.06f) / 0.15f);
                if (t <= 0f) { ry += ListRow * k; continue; }
                float slide = (1f - t) * 40f * k;
                HudColor text = HudColor.White.WithAlpha(t), dim = HudColor.Hex(0xAEB4C6, t);
                if (r.Highlight)
                {
                    float pulse = 0.85f + 0.15f * (float)Math.Sin(time * 6f);
                    Img(o, HudTex.Panel, x + 10f * k, ry - 2f * k, w - 20f * k, (ListRow + 2f) * k, Yellow.WithAlpha(t * pulse), skew: -0.1f);
                    text = dim = Navy.WithAlpha(t);
                    Img(o, HudTex.Panel, x + w - 96f * k, ry + 1f * k, 84f * k, (ListRow - 4f) * k, Red.WithAlpha(t), rot: 6f);
                    Txt(o, Loc.T("NEW"), x + w - 96f * k, ry + 2f * k, 84f * k, (ListRow - 6f) * k, 21f * k, HudColor.White.WithAlpha(t), HudAlign.Center, 0f);
                }
                float size = 23f * k, ol = r.Highlight ? 0f : 2f * k;
                Txt(o, r.Rank, cRank + slide, ry + 2f * k, 66f * k, (ListRow - 4f) * k, size, dim, HudAlign.Left, ol);
                Txt(o, r.Score, cScore + slide, ry + 2f * k, scoreW, (ListRow - 4f) * k, size, text, HudAlign.Left, ol);
                Txt(o, r.Distance, cDist + slide, ry + 2f * k, w * 0.26f, (ListRow - 4f) * k, size, dim, HudAlign.Left, ol);
                if (!r.Highlight) Txt(o, r.Crew, cCrew + slide, ry + 2f * k, w * 0.12f, (ListRow - 4f) * k, size, dim, HudAlign.Left, ol);
                ry += ListRow * k;
            }
        }

        /// <summary>Largest font size (up to <paramref name="size"/>) that keeps <paramref name="text"/> inside <paramref name="width"/>.</summary>
        static float Fit(float size, float width, string text) => Math.Min(size, width / Math.Max(1f, text.Length * 0.6f));
    }
}
