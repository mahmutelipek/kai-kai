using System.Collections.Generic;
using Game.Simulation;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Minimal HUD following the reference layout: distance + best (top-left), score (top-centre),
    /// coins + diamonds (top-right), combo + nitro (right), speed (bottom-right), player badges (bottom-left),
    /// short event popups, and the end-of-run screen with the score breakdown.
    /// M4 (skills: game-ui-ux, performance-optimization): scales with a 1080p reference, stays inside
    /// Screen.safeArea, reacts to simulation events for popups, and caches styles and strings so drawing
    /// allocates nothing per frame (strings are rebuilt only when their value changes).
    /// </summary>
    public sealed class HUDController : MonoBehaviour
    {
        struct Popup
        {
            public string Text;
            public Color Color;
            public float Time;
        }

        BoardController _board;
        RunManager _run;
        PlayerInputRouter _router;
        DebugOverlay _overlay;
        readonly List<Popup> _popups = new List<Popup>(8);
        GUIStyle _big, _medium, _small, _center, _bigCenter, _bigRight, _smallCenter, _smallRight, _mediumTop;
        CachedLabel _distance, _best, _lives, _score, _coins, _diamonds, _combo, _nitro, _kmh;
        static readonly string[] BadgeNames = { "P1", "P2", "P3", "P4", "P5", "P6" };
        Texture2D _panel, _white;
        float _scale = 1f;

        public bool Visible { get; set; } = true;

        public void Initialize(BoardController board, RunManager run, PlayerInputRouter router, DebugOverlay overlay)
        {
            _board = board;
            _run = run;
            _router = router;
            _overlay = overlay;
            _board.Stepped += OnStepped;
        }

        void OnDestroy()
        {
            if (_board != null) _board.Stepped -= OnStepped;
        }

        void OnStepped(RunStepEvents ev)
        {
            RunSimulation r = _board.Run;
            if (ev.NearMisses > 0) Push($"NEAR MISS  +{(int)(r.Tuning.nearMissPoints * r.Score.Multiplier)}", new Color(1f, 0.85f, 0.2f));
            if (ev.CleanLanding) Push($"CLEAN LANDING  {ev.Airtime:0.0}s AIR", new Color(0.4f, 1f, 0.6f));
            else if (ev.Landed && ev.Airtime >= r.Tuning.minScoredAirtime) Push($"{ev.Airtime:0.0}s AIR", Color.white);
            if (ev.SectionCleared) Push("SECTION CLEARED", new Color(0.4f, 0.8f, 1f));
            if (ev.Diamonds > 0) Push($"DIAMOND  +{(int)(r.Tuning.diamondPoints * r.Score.Multiplier)}", new Color(0.8f, 0.45f, 1f));
            if (ev.NitroPickups > 0) Push("NITRO READY!", new Color(0.3f, 0.8f, 1f));
            if (ev.NitroStarted) Push("NITRO!", new Color(0.3f, 0.8f, 1f));
            if (ev.HeavyHits > 0) Push("OUCH! COMBO LOST", new Color(1f, 0.4f, 0.3f));
            if (ev.Crashed) Push(r.LivesPerRun > 0 ? $"WIPEOUT!  {r.LivesLeft} LEFT" : "WIPEOUT!", new Color(1f, 0.3f, 0.25f));
        }

        /// <summary>Short message in the popup column (e.g. "RIDERS: 1" from the count hotkeys).</summary>
        public void Toast(string text) => Push(text, Color.white);

        void Push(string text, Color color)
        {
            if (_popups.Count >= 6) _popups.RemoveAt(0);
            _popups.Add(new Popup { Text = text, Color = color, Time = Time.time });
        }

        void EnsureStyles()
        {
            float scale = Mathf.Max(0.6f, Screen.height / 1080f);
            if (_big != null && Mathf.Approximately(scale, _scale)) return;
            _scale = scale;
            _big = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(56 * scale), fontStyle = FontStyle.BoldAndItalic };
            _medium = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(30 * scale), fontStyle = FontStyle.BoldAndItalic };
            _small = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(20 * scale), fontStyle = FontStyle.Bold };
            _center = new GUIStyle(_medium) { alignment = TextAnchor.MiddleCenter };
            _bigRight = new GUIStyle(_big) { alignment = TextAnchor.UpperRight };
            _smallCenter = new GUIStyle(_small) { alignment = TextAnchor.MiddleCenter };
            _smallRight = new GUIStyle(_small) { alignment = TextAnchor.UpperRight };
            _mediumTop = new GUIStyle(_medium) { alignment = TextAnchor.UpperCenter };
            _bigCenter = new GUIStyle(_big) { alignment = TextAnchor.MiddleCenter };
            if (_panel == null) _panel = MakeTexture(new Color(0.05f, 0.06f, 0.1f, 0.62f));
            if (_white == null) _white = MakeTexture(Color.white);
        }

        static Texture2D MakeTexture(Color color)
        {
            var tex = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            tex.SetPixel(0, 0, color);
            tex.Apply();
            return tex;
        }

        void OnGUI()
        {
            if (!Visible || _board == null || _board.Run == null) return;
            EnsureStyles();
            RunSimulation r = _board.Run;
            ScoreManager score = r.Score;
            // keep everything inside the safe area (notches, rounded corners, TV overscan)
            Rect safe = Screen.safeArea;
            var area = new Rect(safe.x, Screen.height - safe.yMax, safe.width, safe.height);
            GUI.BeginGroup(area);
            float s = _scale, w = area.width, h = area.height, m = 24f * s;

            if (r.State == RunState.Ended) { DrawEndScreen(r, w, h); GUI.EndGroup(); return; }

            // top-left: distance + best (hidden while the F1 debug panel uses that corner)
            if (_overlay == null || !_overlay.Visible)
            {
                Panel(new Rect(m, m, 330f * s, 130f * s));
                Text(new Rect(m + 16f * s, m + 4f * s, 320f * s, 70f * s), _distance.Of((int)r.MaxDistance, "N0", "", " m"), _big, Color.white);
                Text(new Rect(m + 16f * s, m + 74f * s, 320f * s, 40f * s), _best.Of((int)Mathf.Max(_run.BestDistance, r.HighScores != null ? r.HighScores.BestDistance : 0f), "N0", "BEST  ", " m"), _small, new Color(0.35f, 0.85f, 1f));
                if (r.LivesPerRun > 0) Text(new Rect(m + 190f * s, m + 74f * s, 150f * s, 40f * s), _lives.Of(r.LivesLeft * 100 + r.LivesPerRun, "", "LIVES ", "", v => $"{v / 100}/{v % 100}"), _small, new Color(1f, 0.35f, 0.4f));
            }

            // top-centre: score
            Text(new Rect(w * 0.5f - 250f * s, m, 500f * s, 60f * s), _score.Of((int)score.Score, "N0"), _mediumTop, Color.white);

            // top-right: coins and diamonds
            float right = w - m - 200f * s;
            Panel(new Rect(right, m, 200f * s, 60f * s));
            Dot(new Rect(right + 14f * s, m + 14f * s, 32f * s, 32f * s), new Color(1f, 0.78f, 0.1f));
            Text(new Rect(right + 60f * s, m + 6f * s, 140f * s, 50f * s), _coins.Of(score.Coins, ""), _medium, Color.white);
            Panel(new Rect(right, m + 70f * s, 200f * s, 60f * s));
            Dot(new Rect(right + 14f * s, m + 84f * s, 32f * s, 32f * s), new Color(0.75f, 0.35f, 1f));
            Text(new Rect(right + 60f * s, m + 76f * s, 140f * s, 50f * s), _diamonds.Of(score.Diamonds, ""), _medium, Color.white);

            // right: combo and nitro
            float y = h * 0.3f;
            if (score.Multiplier > 1 || score.Combo > 0)
            {
                Text(new Rect(w - m - 300f * s, y, 300f * s, 70f * s), _combo.Of(score.Multiplier, "", "COMBO x"), _big, new Color(1f, 0.85f, 0.15f));
                int step = Mathf.Max(1, (int)r.Tuning.comboStep);
                float fill = (score.Combo % step) / (float)step;
                Bar(new Rect(w - m - 260f * s, y + 72f * s, 240f * s, 10f * s), fill, new Color(1f, 0.85f, 0.15f));
            }
            if (r.Board.Board.State.NitroTimer > 0f)
                Text(new Rect(w - m - 300f * s, y + 95f * s, 300f * s, 50f * s), "NITRO!", _medium, new Color(0.3f, 0.85f, 1f));
            else if (r.NitroCharges > 0)
                Text(new Rect(w - m - 300f * s, y + 95f * s, 300f * s, 50f * s), r.NitroCharges > 1 ? _nitro.Of(r.NitroCharges, "", "NITRO READY! x") : "NITRO READY!", _medium, new Color(0.3f, 0.85f, 1f));

            // bottom-right: speed
            BoardState b = r.Board.Board.State;
            float kmh = b.Speed * 3.6f;
            Text(new Rect(w - m - 260f * s, h - m - 120f * s, 170f * s, 80f * s), _kmh.Of(Mathf.RoundToInt(kmh), ""), _bigRight, Color.white);
            Text(new Rect(w - m - 85f * s, h - m - 90f * s, 90f * s, 40f * s), "KM/H", _small, Color.white);
            float cap = r.Tuning.softCapSpeed * r.Board.Board.SpeedCapMultiplier + r.Tuning.nitroSpeedBonus;
            Segments(new Rect(w - m - 260f * s, h - m - 34f * s, 260f * s, 22f * s), b.Speed / Mathf.Max(cap, 1f), b.NitroTimer > 0f ? new Color(0.3f, 0.85f, 1f) : new Color(1f, 0.82f, 0.15f));

            // bottom-left: player badges
            int players = r.Board.ActivePlayerCount;
            for (int i = 0; i < players; i++)
            {
                Rect badge = new Rect(m + i * 64f * s, h - m - 70f * s, 56f * s, 56f * s);
                Color c = MaterialLibrary.PlayerColors[i % MaterialLibrary.PlayerColors.Length];
                bool onBoard = r.Board.Players[i].IsOnBoard;
                Panel(badge);
                Dot(new Rect(badge.x + 6f * s, badge.y + 6f * s, 44f * s, 44f * s), onBoard ? c : c * 0.35f);
                Text(new Rect(badge.x, badge.y + 10f * s, badge.width, 40f * s), BadgeNames[i % BadgeNames.Length], _smallCenter, Color.white);
                if (_router != null && _router.IsHumanControlled(i))
                    Text(new Rect(badge.x - 10f * s, badge.y - 30f * s, badge.width + 20f * s, 30f * s), "YOU", _smallCenter, c);
            }

            // popups (right-centre, fading)
            float py = h * 0.5f;
            for (int i = _popups.Count - 1; i >= 0; i--)
            {
                float age = Time.time - _popups[i].Time;
                if (age > 1.6f) { _popups.RemoveAt(i); continue; }
                Color c = _popups[i].Color;
                c.a = Mathf.Clamp01(1.6f - age);
                Text(new Rect(w * 0.5f, py - age * 40f * s, w * 0.45f, 50f * s), _popups[i].Text, _medium, c);
                py += 46f * s;
            }
            GUI.EndGroup();
        }

        void DrawEndScreen(RunSimulation r, float w, float h)
        {
            float s = _scale;
            ScoreManager sc = r.Score;
            var box = new Rect(w * 0.5f - 330f * s, h * 0.5f - 300f * s, 660f * s, 600f * s);
            Panel(box);
            Panel(box);
            float y = box.y + 20f * s;
            Text(new Rect(box.x, y, box.width, 70f * s), "RUN OVER", _bigCenter, Color.white);
            y += 90f * s;
            Line(box, ref y, "Distance", $"{r.MaxDistance:N0} m", sc.DistancePoints);
            Line(box, ref y, "Coins", sc.Coins.ToString(), sc.CoinPoints);
            Line(box, ref y, "Diamonds", sc.Diamonds.ToString(), sc.DiamondPoints);
            Line(box, ref y, "Near misses", sc.NearMisses.ToString(), sc.NearMissPoints);
            Line(box, ref y, "Airtime", $"{sc.Airtime:0.0} s", sc.AirtimePoints);
            Line(box, ref y, "Best combo", $"{sc.BestCombo}  (x{ScoreManager.MultiplierFor(sc.BestCombo, r.Tuning)})", -1f);
            y += 10f * s;
            Text(new Rect(box.x, y, box.width, 70f * s), _score.Of((int)sc.Score, "N0", "SCORE  "), _bigCenter, new Color(1f, 0.85f, 0.15f));
            y += 72f * s;
            string best = r.HighScores != null ? $"BEST  {r.HighScores.BestScore:N0}   ·   {r.HighScores.BestDistance:N0} m" : "";
            if (r.NewBestScore) best = "NEW BEST SCORE!   " + best;
            else if (r.NewBestDistance) best = "NEW BEST DISTANCE!   " + best;
            Text(new Rect(box.x, y, box.width, 40f * s), best, _center, new Color(0.35f, 0.85f, 1f));
            y += 60f * s;
            Text(new Rect(box.x, y, box.width, 40f * s), "Press R, Space or (A) to ride again", _smallCenter, Color.white);
        }

        void Line(Rect box, ref float y, string label, string value, float points)
        {
            float s = _scale;
            Text(new Rect(box.x + 40f * s, y, 260f * s, 40f * s), label, _small, new Color(0.85f, 0.88f, 0.95f));
            Text(new Rect(box.x + 290f * s, y, 160f * s, 40f * s), value, _small, Color.white);
            if (points >= 0f) Text(new Rect(box.x + 450f * s, y, 170f * s, 40f * s), "+" + points.ToString("N0"), _smallRight, new Color(1f, 0.85f, 0.15f));
            y += 42f * s;
        }

        void Panel(Rect r) => GUI.DrawTexture(r, _panel);

        void Dot(Rect r, Color c)
        {
            Color old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, _white);
            GUI.color = old;
        }

        void Bar(Rect r, float fill, Color c)
        {
            Panel(r);
            Dot(new Rect(r.x, r.y, r.width * Mathf.Clamp01(fill), r.height), c);
        }

        void Segments(Rect r, float fill, Color c)
        {
            const int count = 10;
            float gap = 4f * _scale, segW = (r.width - gap * (count - 1)) / count;
            int lit = Mathf.CeilToInt(Mathf.Clamp01(fill) * count);
            for (int i = 0; i < count; i++)
            {
                var seg = new Rect(r.x + i * (segW + gap), r.y, segW, r.height);
                if (i < lit) Dot(seg, c); else Panel(seg);
            }
        }

        /// <summary>A string rebuilt only when its integer value changes (no per-frame garbage).</summary>
        struct CachedLabel
        {
            int _value;
            string _text;

            public string Of(int value, string format, string prefix = "", string suffix = "", System.Func<int, string> custom = null)
            {
                if (_text != null && value == _value) return _text;
                _value = value;
                string body = custom != null ? custom(value) : value.ToString(format, System.Globalization.CultureInfo.InvariantCulture);
                _text = prefix + body + suffix;
                return _text;
            }
        }

        /// <summary>Text with a dark drop shadow so it reads over any background.</summary>
        static void Text(Rect r, string text, GUIStyle style, Color color)
        {
            Color old = style.normal.textColor;
            style.normal.textColor = new Color(0f, 0f, 0f, color.a * 0.75f);
            GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), text, style);
            style.normal.textColor = color;
            GUI.Label(r, text, style);
            style.normal.textColor = old;
        }
    }
}
