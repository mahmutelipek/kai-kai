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
        public const float RowHeight = 40f;
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
                    Score = ((long)e.Score).ToString("N0", Ci),
                    Distance = ((long)e.Distance).ToString("N0", Ci) + " m",
                    Crew = "x" + e.Crew.ToString(Ci),
                    Date = e.Date ?? "",
                    Highlight = i + 1 == highlightRank,
                });
            }
        }

        /// <summary>Height the table needs for <paramref name="rows"/> rows (at k = 1).</summary>
        public static float Height(int rows) => 128f + Math.Max(1, rows) * RowHeight;

        /// <param name="reveal">Seconds since the table appeared (rows slide in one by one); large = all shown.</param>
        public static void Draw(List<HudCmd> o, List<ScoreRow> rows, float x, float y, float w, float k, float time, float reveal)
        {
            if (reveal <= 0f) return; // not its turn yet (the end screen reveals the summary first)
            float h = Height(HighScoreManager.TableSize) * k;
            float a0 = Clamp01(reveal / 0.2f);
            Img(o, HudTex.Card, x, y, w, h, HudColor.Hex(0x14161E, 0.92f * a0));
            Img(o, HudTex.Panel, x + w * 0.5f - 200f * k, y - 30f * k, 400f * k, 84f * k, Cyan.WithAlpha(a0), rot: -3f);
            Txt(o, Loc.T("TOP SCORES"), x + w * 0.5f - 200f * k, y - 22f * k, 400f * k, 70f * k, 48f * k, Navy.WithAlpha(a0), HudAlign.Center, 0f);

            // column layout (fractions of the width): rank | score | distance | crew
            float cRank = x + 22f * k, cScore = x + 100f * k, cDist = x + w * 0.52f, cCrew = x + w * 0.8f;
            float scoreW = cDist - cScore - 10f * k;
            float hy = y + 66f * k;
            HudColor head = HudColor.Hex(0x9AA0B4, a0);
            Txt(o, Loc.T("SCORE"), cScore, hy, scoreW, 32f * k, 22f * k, head, HudAlign.Left, 2f * k);
            Txt(o, Loc.T("DISTANCE"), cDist, hy, w * 0.28f, 32f * k, 22f * k, head, HudAlign.Left, 2f * k);
            Txt(o, Loc.T("CREW"), cCrew, hy, w * 0.16f, 32f * k, 22f * k, head, HudAlign.Left, 2f * k);

            float ry = y + 104f * k;
            if (rows.Count == 0)
            {
                Txt(o, Loc.T("NO RUNS YET - GO RIDE!"), x, ry + 120f * k, w, 50f * k, 32f * k, HudColor.White.WithAlpha(a0), HudAlign.Center, 2f * k);
                return;
            }
            for (int i = 0; i < rows.Count; i++)
            {
                ScoreRow r = rows[i];
                float t = Clamp01((reveal - 0.15f - i * 0.06f) / 0.15f);
                if (t <= 0f) { ry += RowHeight * k; continue; }
                float slide = (1f - t) * 40f * k;
                HudColor text = HudColor.White.WithAlpha(t), dim = HudColor.Hex(0xD8DCEA, t);
                if (r.Highlight)
                {
                    float pulse = 0.85f + 0.15f * (float)Math.Sin(time * 6f);
                    Img(o, HudTex.Panel, x + 8f * k, ry - 2f * k, w - 16f * k, (RowHeight + 2f) * k, Yellow.WithAlpha(t * pulse), skew: -0.1f);
                    text = dim = Navy.WithAlpha(t);
                    Img(o, HudTex.Panel, x + w - 96f * k, ry + 2f * k, 84f * k, (RowHeight - 6f) * k, Red.WithAlpha(t), rot: 6f);
                    Txt(o, Loc.T("NEW"), x + w - 96f * k, ry + 3f * k, 84f * k, (RowHeight - 8f) * k, 22f * k, HudColor.White.WithAlpha(t), HudAlign.Center, 0f);
                }
                float size = (i < 3 ? 28f : 25f) * k;
                HudColor rankColor = r.Highlight ? text : i == 0 ? Yellow.WithAlpha(t) : i < 3 ? Cyan.WithAlpha(t) : dim;
                Txt(o, r.Rank, cRank + slide, ry + 2f * k, 72f * k, (RowHeight - 4f) * k, size, rankColor, HudAlign.Left, r.Highlight ? 0f : 2f * k);
                Txt(o, r.Score, cScore + slide, ry + 2f * k, scoreW, (RowHeight - 4f) * k, size, text, HudAlign.Left, r.Highlight ? 0f : 2f * k);
                Txt(o, r.Distance, cDist + slide, ry + 2f * k, w * 0.28f, (RowHeight - 4f) * k, size, dim, HudAlign.Left, r.Highlight ? 0f : 2f * k);
                if (!r.Highlight) Txt(o, r.Crew, cCrew + slide, ry + 2f * k, w * 0.12f, (RowHeight - 4f) * k, size, dim, HudAlign.Left, 2f * k);
                ry += RowHeight * k;
            }
        }
    }
}
