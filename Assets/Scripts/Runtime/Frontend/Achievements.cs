using System;
using System.Collections.Generic;
using Game.Simulation;

namespace Game.Frontend
{
    public enum AchievementId : byte
    {
        FirstRide, FirstKilometre, FiveKilometres, TenKilometres, MaxCombo, CoinHoarder, GemHunter,
        NearMissMaster, SmoothLanding, NitroRush, FullCrew, Unstoppable, HighScorer, Wipeout,
        CrewOllie, PerfectOllie, MegaCarve, Drafter,
    }

    public struct AchievementInfo
    {
        public AchievementId Id;
        /// <summary>API name to create in Steamworks (Stats &amp; Achievements).</summary>
        public string ApiName;
        public string Name, Description;
    }

    /// <summary>The achievement list (create the same API names on the Steamworks partner site, see Docs/STEAM_RELEASE.md).</summary>
    public static class AchievementCatalog
    {
        public static readonly AchievementInfo[] All =
        {
            new AchievementInfo { Id = AchievementId.FirstRide, ApiName = "ACH_FIRST_RIDE", Name = "Drop In", Description = "Finish your first run." },
            new AchievementInfo { Id = AchievementId.FirstKilometre, ApiName = "ACH_KM_1", Name = "Downhill Rookie", Description = "Ride 1 km in one run." },
            new AchievementInfo { Id = AchievementId.FiveKilometres, ApiName = "ACH_KM_5", Name = "Hill Bomber", Description = "Ride 5 km in one run." },
            new AchievementInfo { Id = AchievementId.TenKilometres, ApiName = "ACH_KM_10", Name = "Endless Summer", Description = "Ride 10 km in one run." },
            new AchievementInfo { Id = AchievementId.MaxCombo, ApiName = "ACH_COMBO_MAX", Name = "Combo King", Description = "Reach the maximum combo multiplier." },
            new AchievementInfo { Id = AchievementId.CoinHoarder, ApiName = "ACH_COINS_100", Name = "Coin Hoarder", Description = "Collect 100 coins in one run." },
            new AchievementInfo { Id = AchievementId.GemHunter, ApiName = "ACH_DIAMONDS_5", Name = "Gem Hunter", Description = "Collect 5 diamonds in one run." },
            new AchievementInfo { Id = AchievementId.NearMissMaster, ApiName = "ACH_NEAR_MISS_10", Name = "Close Shave", Description = "Get 10 near misses in one run." },
            new AchievementInfo { Id = AchievementId.SmoothLanding, ApiName = "ACH_CLEAN_LANDINGS_5", Name = "Butter Landing", Description = "Land 5 clean jumps in one run." },
            new AchievementInfo { Id = AchievementId.NitroRush, ApiName = "ACH_NITRO", Name = "Nitro Rush", Description = "Fire a nitro boost." },
            new AchievementInfo { Id = AchievementId.FullCrew, ApiName = "ACH_FULL_CREW", Name = "Full Crew", Description = "Ride with four human players." },
            new AchievementInfo { Id = AchievementId.Unstoppable, ApiName = "ACH_NO_CRASH_3KM", Name = "Unstoppable", Description = "Ride 3 km without a crash." },
            new AchievementInfo { Id = AchievementId.HighScorer, ApiName = "ACH_SCORE_50K", Name = "High Roller", Description = "Score 50,000 points in one run." },
            new AchievementInfo { Id = AchievementId.Wipeout, ApiName = "ACH_WIPEOUT", Name = "Epic Wipeout", Description = "Crash the board for the first time." },
            new AchievementInfo { Id = AchievementId.CrewOllie, ApiName = "ACH_OLLIE", Name = "Lift Off", Description = "Crew ollie: jump together and hop the board." },
            new AchievementInfo { Id = AchievementId.PerfectOllie, ApiName = "ACH_PERFECT_OLLIE", Name = "In Sync", Description = "Land a PERFECT OLLIE with the whole crew." },
            new AchievementInfo { Id = AchievementId.MegaCarve, ApiName = "ACH_MEGA_CARVE", Name = "Slingshot", Description = "Fire a MEGA carve boost." },
            new AchievementInfo { Id = AchievementId.Drafter, ApiName = "ACH_DRAFT_5", Name = "Tailgater", Description = "Draft behind 5 cars in one run." },
        };

        public static AchievementInfo Get(AchievementId id)
        {
            foreach (AchievementInfo a in All) if (a.Id == id) return a;
            throw new ArgumentOutOfRangeException(nameof(id));
        }
    }

    /// <summary>
    /// Watches a run and reports achievements the first time their condition holds (each only once per session;
    /// the platform remembers them across sessions). Engine-free and deterministic, so it is tested headless.
    /// </summary>
    public sealed class AchievementTracker
    {
        readonly HashSet<AchievementId> _unlocked = new HashSet<AchievementId>();
        readonly Action<AchievementId> _onUnlock;
        float _crashFreeFrom;
        int _cleanLandings;

        public AchievementTracker(Action<AchievementId> onUnlock) { _onUnlock = onUnlock; }

        public bool IsUnlocked(AchievementId id) => _unlocked.Contains(id);

        /// <summary>Call when a run starts with the number of human riders.</summary>
        public void OnRunStart(int humans)
        {
            _crashFreeFrom = 0f;
            _cleanLandings = 0;
            if (humans >= Lobby.MaxRiders) Unlock(AchievementId.FullCrew);
        }

        public void OnStep(in RunStepEvents ev, RunSimulation r)
        {
            ScoreManager s = r.Score;
            float d = r.MaxDistance;
            if (ev.Crashed) { Unlock(AchievementId.Wipeout); _crashFreeFrom = r.Distance; }
            if (d >= 1000f) Unlock(AchievementId.FirstKilometre);
            if (d >= 5000f) Unlock(AchievementId.FiveKilometres);
            if (d >= 10000f) Unlock(AchievementId.TenKilometres);
            if (r.Distance - _crashFreeFrom >= 3000f) Unlock(AchievementId.Unstoppable);
            if (s.Multiplier >= (int)r.Tuning.comboMaxMultiplier) Unlock(AchievementId.MaxCombo);
            if (s.Coins >= 100) Unlock(AchievementId.CoinHoarder);
            if (s.Diamonds >= 5) Unlock(AchievementId.GemHunter);
            if (s.NearMisses >= 10) Unlock(AchievementId.NearMissMaster);
            if (ev.CleanLanding && ++_cleanLandings >= 5) Unlock(AchievementId.SmoothLanding);
            if (ev.NitroStarted) Unlock(AchievementId.NitroRush);
            if (ev.Ollie) Unlock(AchievementId.CrewOllie);
            if (ev.PerfectOllie) Unlock(AchievementId.PerfectOllie);
            if (ev.CarveBoost > 1) Unlock(AchievementId.MegaCarve);
            if (s.Slipstreams >= 5) Unlock(AchievementId.Drafter);
            if (s.Score >= 50000f) Unlock(AchievementId.HighScorer);
            if (ev.RunEnded) Unlock(AchievementId.FirstRide);
        }

        void Unlock(AchievementId id)
        {
            if (_unlocked.Add(id)) _onUnlock?.Invoke(id);
        }
    }
}
