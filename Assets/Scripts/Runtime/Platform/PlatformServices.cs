using System;
using UnityEngine;

namespace Game
{
    /// <summary>Store / platform features the game uses (Steam today). Everything is optional: the game runs without it.</summary>
    public interface IPlatformServices
    {
        string Name { get; }
        bool IsAvailable { get; }
        string PlayerName { get; }
        void Tick();
        void UnlockAchievement(string apiName);
        void SetStat(string apiName, int value);
        /// <summary>Keeps the player's best (higher is better).</summary>
        void SubmitScore(string leaderboard, int score);
        void SetRichPresence(string status);
        void Shutdown();
    }

    /// <summary>No platform (editor without Steam, DRM-free builds): logs achievements in development builds.</summary>
    public sealed class NullPlatform : IPlatformServices
    {
        public string Name => "None";
        public bool IsAvailable => false;
        public string PlayerName => "Player";
        public void Tick() { }
        public void UnlockAchievement(string apiName) { if (Debug.isDebugBuild) Debug.Log("Achievement (no platform): " + apiName); }
        public void SetStat(string apiName, int value) { }
        public void SubmitScore(string leaderboard, int score) { }
        public void SetRichPresence(string status) { }
        public void Shutdown() { }
    }

    /// <summary>
    /// Holds the active platform. The Steam assembly (Assets/Scripts/Steam, compiled only when the Steamworks.NET
    /// package is installed) registers <see cref="SteamFactory"/>; if Steam is not running the game falls back to
    /// <see cref="NullPlatform"/> and keeps working.
    /// </summary>
    public static class PlatformServices
    {
        public static IPlatformServices Current { get; private set; } = new NullPlatform();
        /// <summary>Set by Game.Steam at load; returns null when Steam could not start.</summary>
        public static Func<IPlatformServices> SteamFactory;
        static bool _initialized;

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            if (SteamFactory != null)
            {
                try
                {
                    IPlatformServices steam = SteamFactory();
                    if (steam != null && steam.IsAvailable) Current = steam;
                }
                catch (Exception e)
                {
                    Debug.LogWarning("Downhill: Steam unavailable (" + e.Message + "); running without platform features.");
                }
            }
            var go = new GameObject("Platform Services");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<PlatformPump>();
            Debug.Log("Downhill: platform = " + Current.Name);
        }

        sealed class PlatformPump : MonoBehaviour
        {
            void Update() => Current.Tick();
            void OnApplicationQuit() => Current.Shutdown();
        }
    }
}
