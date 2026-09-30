using System;
using System.Globalization;
using Game.Frontend;
using Game.Simulation;

namespace Game.Hud
{
    /// <summary>
    /// Turns the run into HudState: strings rebuilt only when their value changes, pop animations when a counter
    /// rises, event popups (near miss, clean landing, ...), the start countdown and a wobble warning.
    /// Engine-free: Unity's HUDController and the headless preview use the same presenter
    /// (skill: game-ui-ux, "drive the HUD from events"; game-feel, "pop with overshoot").
    /// </summary>
    public sealed class HudPresenter
    {
        public readonly HudState State = new HudState();

        int _distance = -1, _best = -1, _coins = -1, _diamonds = -1, _score = -1, _mult = -1, _speed = -1, _nitro = -1;
        float _wobbleWarnCooldown;
        bool _wasWobbling;
        static readonly CultureInfo Ci = CultureInfo.InvariantCulture;

        /// <summary>Per-frame update from the current run state (no allocation unless a shown value changed).</summary>
        public void Update(RunSimulation r, float bestDistance, float time, float dt)
        {
            HudState s = State;
            s.Time = time;
            ScoreManager sc = r.Score;
            BoardState b = r.Board.Board.State;
            BoardTuningData t = r.Tuning;

            Set(ref _distance, (int)r.MaxDistance, ref s.Distance, v => v.ToString("N0", Ci) + " m");
            Set(ref _best, (int)Math.Max(bestDistance, r.HighScores != null ? r.HighScores.BestDistance : 0f), ref s.Best, v => v.ToString("N0", Ci) + " m");
            if (Set(ref _coins, sc.Coins, ref s.Coins, v => v.ToString(Ci)) && _coins > 0) s.CoinPop = 1f;
            if (Set(ref _diamonds, sc.Diamonds, ref s.Diamonds, v => v.ToString(Ci)) && _diamonds > 0) s.DiamondPop = 1f;
            Set(ref _score, (int)sc.Score, ref s.Score, v => v.ToString("N0", Ci));
            int prevMult = _mult;
            if (Set(ref _mult, sc.Multiplier, ref s.Combo, v => "x" + v.ToString(Ci)) && prevMult >= 0 && _mult > prevMult) s.ComboPop = 1f;
            s.ShowCombo = sc.Multiplier > 1 || sc.Combo > 0;
            int step = Math.Max(1, (int)t.comboStep);
            s.ComboFill = (sc.Combo % step) / (float)step;
            s.NitroCharges = r.NitroCharges;
            s.NitroActive = b.NitroTimer > 0f;
            Set(ref _nitro, r.NitroCharges, ref s.NitroText, v => v > 1 ? Loc.T("NITRO READY!") + " x" + v.ToString(Ci) : Loc.T("NITRO READY!"));
            Set(ref _speed, (int)Math.Round(b.Speed * 3.6f), ref s.Speed, v => v.ToString(Ci));
            float cap = t.softCapSpeed * r.Board.Board.SpeedCapMultiplier + t.nitroSpeedBonus * 0.5f;
            s.SpeedFill = b.Speed / Math.Max(cap, 1f);
            s.Danger = b.Crashed ? 0f : b.Danger;
            s.Players = r.Board.ActivePlayerCount;
            for (int i = 0; i < s.OnBoard.Length; i++) s.OnBoard[i] = i < r.Board.ActivePlayerCount && r.Board.Players[i].IsOnBoard;
            s.LivesLeft = r.LivesLeft;
            s.LivesMax = r.LivesPerRun;

            // decays
            s.CoinPop = Math.Max(0f, s.CoinPop - dt * 5f);
            s.DiamondPop = Math.Max(0f, s.DiamondPop - dt * 4f);
            s.ComboPop = Math.Max(0f, s.ComboPop - dt * 3.5f);
            for (int i = s.Popups.Count - 1; i >= 0; i--)
            {
                HudPopup p = s.Popups[i];
                p.Age += dt;
                if (p.Age > 1.6f) s.Popups.RemoveAt(i);
                else s.Popups[i] = p;
            }
            if (s.Countdown != null) s.CountdownAge += dt;

            // wobble warning: tells the crew the board is about to go
            bool wobbling = b.Wobble > 0.05f && !b.Crashed;
            _wobbleWarnCooldown = Math.Max(0f, _wobbleWarnCooldown - dt);
            if (wobbling && !_wasWobbling && _wobbleWarnCooldown <= 0f)
            {
                Push(Loc.T("HOLD ON! BALANCE!"), HudLayout.Red);
                _wobbleWarnCooldown = 3f;
            }
            _wasWobbling = wobbling;

            s.Ended = r.State == RunState.Ended;
        }

        /// <summary>Event popups for one simulation step (strings only built when something happened).</summary>
        public void OnStep(in RunStepEvents ev, RunSimulation r)
        {
            int mult = r.Score.Multiplier;
            if (ev.NearMisses > 0) Push(Loc.T("NEAR MISS!") + "  +" + ((int)(r.Tuning.nearMissPoints * mult)).ToString(Ci), HudLayout.Yellow);
            if (ev.CleanLanding) Push(Loc.T("CLEAN LANDING!") + "  " + ev.Airtime.ToString("0.0", Ci) + Loc.T("S AIR"), HudColor.Hex(0x5CFF8F));
            else if (ev.Landed && ev.Airtime >= r.Tuning.minScoredAirtime) Push(ev.Airtime.ToString("0.0", Ci) + Loc.T("S AIR"), HudColor.White);
            if (ev.SectionCleared) Push(Loc.T("SECTION CLEARED!"), HudLayout.Cyan);
            if (ev.Diamonds > 0) Push(Loc.T("DIAMOND!") + "  +" + ((int)(r.Tuning.diamondPoints * mult)).ToString(Ci), HudColor.Hex(0xD08CFF));
            if (ev.NitroPickups > 0) Push(Loc.T("NITRO GET!"), HudLayout.Cyan);
            if (ev.NitroStarted) Push(Loc.T("NITRO BOOST!"), HudLayout.Cyan);
            if (ev.HeavyHits > 0) Push(Loc.T("OUCH! COMBO LOST"), HudColor.Hex(0xFF6A50));
            if (ev.Crashed) Push(r.LivesPerRun > 0 ? Loc.T("WIPEOUT!") + "  " + r.LivesLeft.ToString(Ci) + " " + Loc.T("LEFT") : Loc.T("WIPEOUT!"), HudLayout.Red);
        }

        /// <summary>Forces every cached string to rebuild (language changed).</summary>
        public void Invalidate()
        {
            _distance = _best = _coins = _diamonds = _score = _mult = _speed = _nitro = -1;
            State.NitroText = null;
        }

        /// <summary>Start countdown text ("3", "2", "1", "GO!", null to hide); restarts the pop animation on change.</summary>
        public void SetCountdown(string text)
        {
            if (text == State.Countdown) return;
            State.Countdown = text;
            State.CountdownAge = 0f;
        }

        public void Push(string text, HudColor color)
        {
            if (State.Popups.Count >= 5) State.Popups.RemoveAt(0);
            State.Popups.Add(new HudPopup { Text = text, Color = color, Age = 0f });
        }

        /// <summary>Fills the end-of-run breakdown (once, when the run ends).</summary>
        public void FillEndScreen(RunSimulation r)
        {
            HudState s = State;
            ScoreManager sc = r.Score;
            s.EndLines.Clear();
            s.EndLines.Add((Loc.T("DISTANCE"), r.MaxDistance.ToString("N0", Ci) + " m", "+" + sc.DistancePoints.ToString("N0", Ci)));
            s.EndLines.Add((Loc.T("COINS"), sc.Coins.ToString(Ci), "+" + sc.CoinPoints.ToString("N0", Ci)));
            s.EndLines.Add((Loc.T("DIAMONDS"), sc.Diamonds.ToString(Ci), "+" + sc.DiamondPoints.ToString("N0", Ci)));
            s.EndLines.Add((Loc.T("NEAR MISSES"), sc.NearMisses.ToString(Ci), "+" + sc.NearMissPoints.ToString("N0", Ci)));
            s.EndLines.Add((Loc.T("AIRTIME"), sc.Airtime.ToString("0.0", Ci) + " s", "+" + sc.AirtimePoints.ToString("N0", Ci)));
            s.EndLines.Add((Loc.T("BEST COMBO"), "x" + ScoreManager.MultiplierFor(sc.BestCombo, r.Tuning).ToString(Ci), ""));
            s.EndScore = Loc.T("SCORE") + "  " + sc.Score.ToString("N0", Ci);
            string best = r.HighScores != null ? Loc.T("BEST") + "  " + r.HighScores.BestScore.ToString("N0", Ci) + "   ·   " + r.HighScores.BestDistance.ToString("N0", Ci) + " m" : "";
            if (r.NewBestScore) best = Loc.T("NEW BEST SCORE!") + "   " + best;
            else if (r.NewBestDistance) best = Loc.T("NEW BEST DISTANCE!") + "   " + best;
            s.EndBest = best;
            s.EndTitle = Loc.T("RUN OVER");
            s.EndHint = Loc.T("PRESS R, SPACE OR (A) TO RIDE AGAIN");
        }

        static bool Set(ref int last, int value, ref string text, Func<int, string> format)
        {
            if (value == last && text != null) return false;
            last = value;
            text = format(value);
            return true;
        }
    }
}
