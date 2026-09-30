using System;
using System.Globalization;
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
            Set(ref _nitro, r.NitroCharges, ref s.NitroText, v => v > 1 ? "NITRO READY! x" + v.ToString(Ci) : "NITRO READY!");
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
                Push("HOLD ON! BALANCE!", HudLayout.Red);
                _wobbleWarnCooldown = 3f;
            }
            _wasWobbling = wobbling;

            s.Ended = r.State == RunState.Ended;
        }

        /// <summary>Event popups for one simulation step (strings only built when something happened).</summary>
        public void OnStep(in RunStepEvents ev, RunSimulation r)
        {
            int mult = r.Score.Multiplier;
            if (ev.NearMisses > 0) Push("NEAR MISS!  +" + ((int)(r.Tuning.nearMissPoints * mult)).ToString(Ci), HudLayout.Yellow);
            if (ev.CleanLanding) Push("CLEAN LANDING!  " + ev.Airtime.ToString("0.0", Ci) + "s AIR", HudColor.Hex(0x5CFF8F));
            else if (ev.Landed && ev.Airtime >= r.Tuning.minScoredAirtime) Push(ev.Airtime.ToString("0.0", Ci) + "s AIR", HudColor.White);
            if (ev.SectionCleared) Push("SECTION CLEARED!", HudLayout.Cyan);
            if (ev.Diamonds > 0) Push("DIAMOND!  +" + ((int)(r.Tuning.diamondPoints * mult)).ToString(Ci), HudColor.Hex(0xD08CFF));
            if (ev.NitroPickups > 0) Push("NITRO GET!", HudLayout.Cyan);
            if (ev.NitroStarted) Push("NITRO BOOST!", HudLayout.Cyan);
            if (ev.HeavyHits > 0) Push("OUCH! COMBO LOST", HudColor.Hex(0xFF6A50));
            if (ev.Crashed) Push(r.LivesPerRun > 0 ? "WIPEOUT!  " + r.LivesLeft.ToString(Ci) + " LEFT" : "WIPEOUT!", HudLayout.Red);
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
            s.EndLines.Add(("Distance", r.MaxDistance.ToString("N0", Ci) + " m", "+" + sc.DistancePoints.ToString("N0", Ci)));
            s.EndLines.Add(("Coins", sc.Coins.ToString(Ci), "+" + sc.CoinPoints.ToString("N0", Ci)));
            s.EndLines.Add(("Diamonds", sc.Diamonds.ToString(Ci), "+" + sc.DiamondPoints.ToString("N0", Ci)));
            s.EndLines.Add(("Near misses", sc.NearMisses.ToString(Ci), "+" + sc.NearMissPoints.ToString("N0", Ci)));
            s.EndLines.Add(("Airtime", sc.Airtime.ToString("0.0", Ci) + " s", "+" + sc.AirtimePoints.ToString("N0", Ci)));
            s.EndLines.Add(("Best combo", "x" + ScoreManager.MultiplierFor(sc.BestCombo, r.Tuning).ToString(Ci), ""));
            s.EndScore = "SCORE  " + sc.Score.ToString("N0", Ci);
            string best = r.HighScores != null ? "BEST  " + r.HighScores.BestScore.ToString("N0", Ci) + "   ·   " + r.HighScores.BestDistance.ToString("N0", Ci) + " m" : "";
            if (r.NewBestScore) best = "NEW BEST SCORE!   " + best;
            else if (r.NewBestDistance) best = "NEW BEST DISTANCE!   " + best;
            s.EndBest = best;
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
