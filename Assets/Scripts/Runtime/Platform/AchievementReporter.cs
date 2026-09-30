using Game.Frontend;
using Game.Simulation;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Connects the run to the platform: achievements (AchievementTracker), leaderboards (best distance / score
    /// when a run ends) and lifetime stats. Works with the null platform too (nothing is sent).
    /// </summary>
    public sealed class AchievementReporter : MonoBehaviour
    {
        BoardController _board;
        PlayerInputRouter _router;
        AchievementTracker _tracker;
        int _totalMetres;

        public void Initialize(BoardController board, PlayerInputRouter router)
        {
            _board = board;
            _router = router;
            _tracker = new AchievementTracker(id => PlatformServices.Current.UnlockAchievement(AchievementCatalog.Get(id).ApiName));
            _board.Stepped += OnStepped;
            _board.Respawned += OnRespawned;
            OnRespawned();
        }

        void OnDestroy()
        {
            if (_board == null) return;
            _board.Stepped -= OnStepped;
            _board.Respawned -= OnRespawned;
        }

        /// <summary>RestartRun raises Respawned too: count humans for a fresh run.</summary>
        void OnRespawned()
        {
            RunSimulation r = _board.Run;
            if (r.Distance > 1f) return; // a crash respawn, not a new run
            int humans = 0;
            for (int i = 0; i < r.Board.ActivePlayerCount; i++) if (_router != null && _router.IsHumanControlled(i)) humans++;
            _tracker.OnRunStart(humans);
        }

        void OnStepped(RunStepEvents ev)
        {
            // attract mode (bots only behind the title) earns nothing
            if (_router == null || !AnyHuman()) return;
            RunSimulation r = _board.Run;
            _tracker.OnStep(ev, r);
            if (ev.RunEnded)
            {
                IPlatformServices p = PlatformServices.Current;
                p.SubmitScore(SteamSettings.LeaderboardBestDistance, (int)r.MaxDistance);
                p.SubmitScore(SteamSettings.LeaderboardBestScore, (int)r.Score.Score);
                _totalMetres += (int)r.MaxDistance;
                p.SetStat("STAT_TOTAL_METRES", _totalMetres);
            }
        }

        bool AnyHuman()
        {
            for (int i = 0; i < _board.Run.Board.ActivePlayerCount; i++) if (_router.IsHumanControlled(i)) return true;
            return false;
        }
    }
}
