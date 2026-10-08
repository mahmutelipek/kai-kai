namespace Game.Simulation
{
    /// <summary>Switches for rules that are off in the shipped game but still covered by the simulation tests.</summary>
    public static class GameRules
    {
        /// <summary>
        /// Riders can hop on the deck and the crew can ollie the board. Off: air only comes from ramps
        /// (jump input is ignored, bots never hop, no "JUMP!" calls or tips).
        /// </summary>
        public static bool PlayerJump = false;

        /// <summary>
        /// Points, combo, coins and diamonds. Off: the only goal is the best distance (no score, combo, coin or
        /// diamond on the HUD or the road; the high-score table ranks by distance).
        /// </summary>
        public static bool Scoring = false;

        /// <summary>Nitro pickups and the nitro button. Off: nothing to pick up, nothing to fire.</summary>
        public static bool Nitro = false;
    }
}
