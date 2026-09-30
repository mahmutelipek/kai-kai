// Steam integration. Compiles only when the Steamworks.NET package (com.rlabrecque.steamworks.net) is installed:
// the package defines STEAMWORKS_NET through this assembly's versionDefines. Without it the game uses NullPlatform.
#if STEAMWORKS_NET && !DISABLESTEAMWORKS
using System;
using Steamworks;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Steam through Steamworks.NET: achievements and stats (Steam shows its own unlock popup), leaderboards
    /// (keep-best uploads), rich presence, and the persona name. SteamAPI.RunCallbacks runs every frame.
    /// </summary>
    public sealed class SteamPlatform : IPlatformServices
    {
        bool _ok, _statsDirty;
        float _storeTimer;
        CallResult<LeaderboardFindResult_t> _find;
        CallResult<LeaderboardScoreUploaded_t> _upload;
        readonly System.Collections.Generic.Queue<(string board, int score)> _pendingScores = new System.Collections.Generic.Queue<(string, int)>();
        bool _finding;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() => PlatformServices.SteamFactory = () =>
        {
            var steam = new SteamPlatform();
            return steam.Init() ? steam : null;
        };

        public string Name => "Steam";
        public bool IsAvailable => _ok;
        public string PlayerName => _ok ? SteamFriends.GetPersonaName() : "Player";

        bool Init()
        {
            try
            {
                if (!Packsize.Test() || !DllCheck.Test())
                {
                    Debug.LogWarning("Downhill: Steamworks.NET binaries are wrong for this platform.");
                    return false;
                }
#if !UNITY_EDITOR
                // started outside Steam: let Steam relaunch the game (skipped in development with steam_appid.txt)
                if (SteamAPI.RestartAppIfNecessary(new AppId_t(SteamSettings.AppId)))
                {
                    Application.Quit();
                    return false;
                }
#endif
                _ok = SteamAPI.Init();
                if (!_ok) Debug.LogWarning("Downhill: SteamAPI.Init failed (is Steam running?).");
                if (_ok)
                {
                    _find = CallResult<LeaderboardFindResult_t>.Create(OnLeaderboardFound);
                    _upload = CallResult<LeaderboardScoreUploaded_t>.Create((r, fail) => { });
                }
                return _ok;
            }
            catch (DllNotFoundException e)
            {
                Debug.LogWarning("Downhill: steam_api library missing: " + e.Message);
                return false;
            }
        }

        public void Tick()
        {
            if (!_ok) return;
            SteamAPI.RunCallbacks();
            if (_statsDirty)
            {
                _storeTimer -= Time.unscaledDeltaTime;
                if (_storeTimer <= 0f) { SteamUserStats.StoreStats(); _statsDirty = false; }
            }
            if (!_finding && _pendingScores.Count > 0)
            {
                _finding = true;
                (string board, _) = _pendingScores.Peek();
                _find.Set(SteamUserStats.FindOrCreateLeaderboard(board, ELeaderboardSortMethod.k_ELeaderboardSortMethodDescending,
                                                                   ELeaderboardDisplayType.k_ELeaderboardDisplayTypeNumeric));
            }
        }

        void OnLeaderboardFound(LeaderboardFindResult_t result, bool ioFailure)
        {
            (string board, int score) = _pendingScores.Dequeue();
            _finding = false;
            if (ioFailure || result.m_bLeaderboardFound == 0) { Debug.LogWarning("Downhill: leaderboard not found: " + board); return; }
            _upload.Set(SteamUserStats.UploadLeaderboardScore(result.m_hSteamLeaderboard,
                ELeaderboardUploadScoreMethod.k_ELeaderboardUploadScoreMethodKeepBest, score, null, 0));
        }

        public void UnlockAchievement(string apiName)
        {
            if (!_ok) return;
            SteamUserStats.SetAchievement(apiName);
            MarkDirty(0f); // store now: Steam shows the unlock popup on StoreStats
        }

        public void SetStat(string apiName, int value)
        {
            if (!_ok) return;
            SteamUserStats.SetStat(apiName, value);
            MarkDirty(5f);
        }

        public void SubmitScore(string leaderboard, int score)
        {
            if (_ok && score > 0) _pendingScores.Enqueue((leaderboard, score));
        }

        public void SetRichPresence(string status)
        {
            if (_ok) SteamFriends.SetRichPresence("status", status);
        }

        public void Shutdown()
        {
            if (!_ok) return;
            if (_statsDirty) SteamUserStats.StoreStats();
            SteamAPI.Shutdown();
            _ok = false;
        }

        void MarkDirty(float delay)
        {
            _statsDirty = true;
            _storeTimer = Math.Min(_storeTimer <= 0f ? delay : _storeTimer, delay);
        }
    }
}
#endif
