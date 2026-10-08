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
        /// <summary>The shared sense-of-speed signals (the Unity camera, post effects, particles and audio read these too).</summary>
        public readonly Game.Art.SpeedFeel Feel = new Game.Art.SpeedFeel();
        /// <summary>First-run tips (the front end loads / saves <see cref="TipCoach.SeenMask"/> with the settings).</summary>
        public readonly TipCoach Tips = new TipCoach();
        float _boostMax;

        int _distance = -1, _best = -1, _coins = -1, _diamonds = -1, _score = -1, _mult = -1, _speed = -1, _nitro = -1;
        float _wobbleWarnCooldown;
        bool _wasWobbling;
        static readonly CultureInfo Ci = CultureInfo.InvariantCulture;

        /// <summary>Per-frame update from the current run state (no allocation unless a shown value changed).</summary>
        /// <param name="simDt">Game-time step (0 while paused) for the speed feel; negative = same as <paramref name="dt"/>.</param>
        public void Update(RunSimulation r, float bestDistance, float time, float dt, float simDt = -1f)
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

            // wind streaks: advance on game time, so they freeze with the pause menu
            if (simDt < 0f) simDt = dt;
            Feel.Update(r, simDt);
            s.Wind = Feel.Wind;
            s.WindNitro = Feel.Nitro;
            s.WindBoost = Feel.Boost;
            s.WindTime += simDt;
            s.FlashAge += dt;

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

            // move meter: boost draining > carve charging (STRAIGHTEN! when ready) > drafting
            bool humanRiding = false;
            for (int i = 0; i < s.Human.Length; i++) humanRiding |= s.Human[i] && s.OnBoard[i];
            s.MoveLabel = null;
            if (r.OffroadTime > 0.75f && !b.Crashed)
            {
                // off the asphalt: a few seconds to find the road again
                float left = RunSimulation.OffroadLimit - r.OffroadTime;
                s.MoveLabel = Loc.T("OFF ROAD! GET BACK") + "  " + Math.Max(1, (int)Math.Ceiling(left)).ToString(Ci);
                s.MoveFill = Math.Max(0f, left / RunSimulation.OffroadLimit);
                s.MoveColor = HudLayout.Red;
                s.MoveReady = left < 3f;
            }
            else if (b.BoostTimer > 0f && !b.Crashed)
            {
                _boostMax = Math.Max(_boostMax, b.BoostTimer);
                s.MoveLabel = Loc.T("BOOST!");
                s.MoveFill = b.BoostTimer / _boostMax;
                s.MoveColor = HudLayout.Orange;
                s.MoveReady = false;
            }
            else
            {
                _boostMax = 0f;
                float charge = b.CarveCharge / Math.Max(t.carveMinTime, 0.01f);
                if (b.CarveCharge > 0.15f && b.Grounded && !b.Crashed)
                {
                    s.MoveLabel = charge >= 2f ? Loc.T("MEGA! STRAIGHTEN!") : charge >= 1f ? Loc.T("STRAIGHTEN!") : Loc.T("CARVE");
                    s.MoveFill = Math.Min(1f, charge * 0.5f);
                    s.MoveColor = charge >= 1f ? HudLayout.Yellow : HudLayout.Orange;
                    s.MoveReady = charge >= 1f;
                }
                else if (r.Slipstream > 0.02f && !b.Crashed)
                {
                    s.MoveLabel = Loc.T("DRAFTING");
                    s.MoveFill = r.Slipstream;
                    s.MoveColor = HudColor.White;
                    s.MoveReady = r.Slipstream >= 0.99f;
                }
            }
            s.JumpCallAge = r.Crew.FromHazard && humanRiding ? r.Crew.Age : 9f;
            Tips.Update(r, humanRiding, simDt);
            s.Tip = Tips.Current;
            s.TipAge = Tips.Age;

            s.Ended = r.State == RunState.Ended;
            s.EndAge = s.Ended ? s.EndAge + dt : 0f;
        }

        /// <summary>Event popups for one simulation step (strings only built when something happened).</summary>
        public void OnStep(in RunStepEvents ev, RunSimulation r)
        {
            int mult = r.Score.Multiplier;
            bool pts = GameRules.Scoring;
            if (pts && ev.NearMisses > 0) Push(Loc.T("NEAR MISS!") + "  +" + ((int)(r.Tuning.nearMissPoints * mult)).ToString(Ci), HudLayout.Yellow);
            if (!pts) { }
            else if (ev.CleanLanding) Push(Loc.T("CLEAN LANDING!") + "  " + ev.Airtime.ToString("0.0", Ci) + Loc.T("S AIR"), HudColor.Hex(0x5CFF8F));
            else if (ev.Landed && ev.Airtime >= r.Tuning.minScoredAirtime) Push(ev.Airtime.ToString("0.0", Ci) + Loc.T("S AIR"), HudColor.White);
            if (pts && ev.SectionCleared) Push(Loc.T("SECTION CLEARED!"), HudLayout.Cyan);
            if (pts && ev.Diamonds > 0) Push(Loc.T("DIAMOND!") + "  +" + ((int)(r.Tuning.diamondPoints * mult)).ToString(Ci), HudColor.Hex(0xD08CFF));
            if (ev.NitroPickups > 0) Push(Loc.T("NITRO GET!"), HudLayout.Cyan);
            if (ev.NitroStarted) Push(Loc.T("NITRO BOOST!"), HudLayout.Cyan);
            Feel.OnStep(ev);
            if (ev.NitroStarted) Flash(HudColor.Hex(0x8FEFFF, 0.95f));
            else if (ev.CarveBoost > 0) Flash(HudColor.Hex(0xFFC070, ev.CarveBoost > 1 ? 0.6f : 0.4f));
            else if (ev.PerfectOllie) Flash(HudColor.Hex(0xFFFFFF, 0.35f));
            if (!pts) { }
            else if (ev.PerfectOllie) Push(Loc.T("PERFECT OLLIE!") + "  +" + ((int)(r.Tuning.olliePoints * 2f * mult)).ToString(Ci), HudLayout.Yellow);
            else if (ev.Ollie) Push(Loc.T("OLLIE!"), HudColor.White);
            if (ev.CarveBoost > 1) Push(Loc.T("MEGA CARVE BOOST!"), HudColor.Hex(0xFF8A1F));
            else if (ev.CarveBoost > 0) Push(Loc.T("CARVE BOOST!"), HudColor.Hex(0xFFB347));
            if (ev.SlipstreamStarted) Push(Loc.T("SLIPSTREAM!"), HudLayout.Cyan);
            if (ev.HeavyHits > 0) Push(pts ? Loc.T("OUCH! COMBO LOST") : Loc.T("OUCH!"), HudColor.Hex(0xFF6A50));
            if (ev.Crashed) Push(r.LivesPerRun > 0 ? Loc.T("WIPEOUT!") + "  " + r.LivesLeft.ToString(Ci) + " " + Loc.T("LEFT") : Loc.T("WIPEOUT!"), HudLayout.Red);
        }

        void Flash(HudColor c)
        {
            State.FlashColor = c;
            State.FlashAge = 0f;
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
            if (!GameRules.Scoring)
            {
                // no points: the run is its distance, and the goal is beating the best one
                s.EndLines.Clear();
                s.EndScore = r.MaxDistance.ToString("N0", Ci) + " m";
                string bestD = r.HighScores != null ? Loc.T("BEST") + "  " + r.HighScores.BestDistance.ToString("N0", Ci) + " m" : "";
                if (r.LastRank > 0 && !r.NewBestDistance) bestD = Loc.T("RANK") + " #" + r.LastRank.ToString(Ci) + "   ·   " + bestD;
                ScoreTable.Fill(r.HighScores?.Top, r.LastRank, s.EndTable);
                s.EndBest = bestD;
                s.EndTitle = Loc.T("RUN OVER");
                s.EndNewBest = r.NewBestDistance;
                s.EndHint = Loc.T("PRESS R, SPACE OR (A) TO RIDE AGAIN");
                return;
            }
            s.EndLines.Add((Loc.T("DISTANCE"), r.MaxDistance.ToString("N0", Ci) + " m", "+" + sc.DistancePoints.ToString("N0", Ci)));
            s.EndLines.Add((Loc.T("COINS"), sc.Coins.ToString(Ci), "+" + sc.CoinPoints.ToString("N0", Ci)));
            s.EndLines.Add((Loc.T("DIAMONDS"), sc.Diamonds.ToString(Ci), "+" + sc.DiamondPoints.ToString("N0", Ci)));
            s.EndLines.Add((Loc.T("NEAR MISSES"), sc.NearMisses.ToString(Ci), "+" + sc.NearMissPoints.ToString("N0", Ci)));
            s.EndLines.Add((Loc.T("TRICKS"), (sc.Ollies + sc.CarveBoosts + sc.Slipstreams).ToString(Ci), "+" + sc.MovePoints.ToString("N0", Ci)));
            s.EndLines.Add((Loc.T("AIRTIME"), sc.Airtime.ToString("0.0", Ci) + " s", "+" + sc.AirtimePoints.ToString("N0", Ci)));
            s.EndLines.Add((Loc.T("BEST COMBO"), "x" + ScoreManager.MultiplierFor(sc.BestCombo, r.Tuning).ToString(Ci), ""));
            s.EndScore = Loc.T("SCORE") + "  " + sc.Score.ToString("N0", Ci);
            string best = r.HighScores != null ? Loc.T("BEST") + "  " + r.HighScores.BestScore.ToString("N0", Ci) + "   ·   " + r.HighScores.BestDistance.ToString("N0", Ci) + " m" : "";
            if (r.LastRank > 0 && !r.NewBestScore) best = Loc.T("RANK") + " #" + r.LastRank.ToString(Ci) + "   ·   " + best;
            ScoreTable.Fill(r.HighScores?.Top, r.LastRank, s.EndTable);
            if (r.NewBestScore) best = Loc.T("NEW BEST SCORE!") + "   " + best;
            else if (r.NewBestDistance) best = Loc.T("NEW BEST DISTANCE!") + "   " + best;
            s.EndBest = best;
            s.EndTitle = Loc.T("RUN OVER");
            s.EndNewBest = r.NewBestScore || r.NewBestDistance;
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
