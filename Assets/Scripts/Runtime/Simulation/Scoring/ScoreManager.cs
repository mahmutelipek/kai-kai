using System;

namespace Game.Simulation
{
    /// <summary>
    /// Score and combo rules. Base points (distance, coins, diamonds, near misses, airtime) are multiplied by the
    /// combo multiplier current when they are earned. Combo grows with consecutive pickups, near misses, clean
    /// landings and surviving hard sections; drains when idle; a light hit halves it; a heavy hit, a fall or a
    /// crash resets it. Pure logic, fully unit-tested.
    /// </summary>
    public sealed class ScoreManager
    {
        public const int ComboPerCoin = 1;
        public const int ComboPerDiamond = 3;
        public const int ComboPerNearMiss = 2;
        public const int ComboPerCleanLanding = 2;
        public const int ComboPerSection = 3;
        public const int ComboPerNitroPickup = 1;

        BoardTuningData _t;
        float _distanceCarry;
        float _idle;
        float _drainCarry;

        public float Score { get; private set; }
        public int Combo { get; private set; }
        public int BestCombo { get; private set; }
        public int Coins { get; private set; }
        public int Diamonds { get; private set; }
        public int NearMisses { get; private set; }
        public int CleanLandings { get; private set; }
        public int SectionsCleared { get; private set; }
        public float Airtime { get; private set; }
        public float Distance { get; private set; }

        // breakdown (points actually awarded per category, after multipliers)
        public float DistancePoints { get; private set; }
        public float CoinPoints { get; private set; }
        public float DiamondPoints { get; private set; }
        public float NearMissPoints { get; private set; }
        public float AirtimePoints { get; private set; }

        public ScoreManager(BoardTuningData tuning) { _t = tuning; }

        public void SetTuning(BoardTuningData tuning) => _t = tuning;

        /// <summary>Multiplier for the current combo: 1 + floor(combo / comboStep), capped.</summary>
        public int Multiplier => MultiplierFor(Combo, _t);

        public static int MultiplierFor(int combo, BoardTuningData t)
        {
            int step = Math.Max(1, (int)t.comboStep);
            return Math.Min(1 + combo / step, Math.Max(1, (int)t.comboMaxMultiplier));
        }

        public void Reset()
        {
            Score = 0f; Combo = 0; BestCombo = 0; Coins = 0; Diamonds = 0; NearMisses = 0; CleanLandings = 0; SectionsCleared = 0;
            Airtime = 0f; Distance = 0f; _distanceCarry = 0f; _idle = 0f; _drainCarry = 0f;
            DistancePoints = CoinPoints = DiamondPoints = NearMissPoints = AirtimePoints = 0f;
        }

        /// <summary>Advances time: distance points and combo drain after comboIdleTime without a combo event.</summary>
        public void Tick(float dt, float metersTravelled)
        {
            if (metersTravelled > 0f)
            {
                Distance += metersTravelled;
                _distanceCarry += metersTravelled;
                // award whole metres so points stay integral
                float whole = MathF.Floor(_distanceCarry);
                if (whole >= 1f)
                {
                    _distanceCarry -= whole;
                    float pts = whole * _t.pointsPerMeter * Multiplier;
                    DistancePoints += pts;
                    Score += pts;
                }
            }

            _idle += dt;
            if (_idle > _t.comboIdleTime && Combo > 0)
            {
                _drainCarry += _t.comboDrainPerSecond * dt;
                int drain = (int)_drainCarry;
                if (drain > 0)
                {
                    _drainCarry -= drain;
                    Combo = Math.Max(0, Combo - drain);
                }
            }
        }

        void AddCombo(int amount)
        {
            Combo += amount;
            BestCombo = Math.Max(BestCombo, Combo);
            _idle = 0f;
            _drainCarry = 0f;
        }

        float Award(float basePoints)
        {
            float pts = basePoints * Multiplier;
            Score += pts;
            return pts;
        }

        public void OnCoin() { Coins++; CoinPoints += Award(_t.coinPoints); AddCombo(ComboPerCoin); }

        public void OnDiamond() { Diamonds++; DiamondPoints += Award(_t.diamondPoints); AddCombo(ComboPerDiamond); }

        public void OnNearMiss() { NearMisses++; NearMissPoints += Award(_t.nearMissPoints); AddCombo(ComboPerNearMiss); }

        public void OnNitroPickup() => AddCombo(ComboPerNitroPickup);

        /// <summary>Airtime always scores (if long enough); only clean landings grow the combo.</summary>
        public void OnLanding(float airtime, bool clean)
        {
            if (airtime < _t.minScoredAirtime) return;
            Airtime += airtime;
            AirtimePoints += Award(airtime * _t.airtimePointsPerSecond);
            if (clean) { CleanLandings++; AddCombo(ComboPerCleanLanding); }
        }

        public void OnSectionCleared() { SectionsCleared++; AddCombo(ComboPerSection); }

        /// <summary>Light hit: combo is halved.</summary>
        public void OnLightHit() { Combo /= 2; _idle = 0f; }

        public void OnHeavyHit() => ResetCombo();
        public void OnPlayerFell() => ResetCombo();
        public void OnCrash() => ResetCombo();

        void ResetCombo()
        {
            Combo = 0;
            _drainCarry = 0f;
        }
    }
}
