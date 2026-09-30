namespace Game
{
    /// <summary>Steam configuration. Replace the App ID with yours from the Steamworks partner site.</summary>
    public static class SteamSettings
    {
        /// <summary>480 = Valve's "Spacewar" test app (works for development). Set your own before release.</summary>
        public const uint AppId = 480;

        public const string LeaderboardBestDistance = "best_distance";
        public const string LeaderboardBestScore = "best_score";
    }
}
