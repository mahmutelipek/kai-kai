namespace Game.Frontend
{
    /// <summary>Static text of the HOW TO PLAY and CREDITS screens (English keys; Loc translates).</summary>
    public static class FrontendContent
    {
        /// <summary>Six cards: heading, two short lines. Order = reading order (left to right, top to bottom).</summary>
        public static readonly (string heading, string line1, string line2)[] HowToPlay =
        {
            ("STEER BY STANDING", "EVERYONE RIDES ONE GIANT BOARD.", "WHERE THE CREW STANDS, IT GOES."),
            ("CREW OLLIE", "JUMP TOGETHER TO HOP POTHOLES AND CONES.", "ALL AT ONCE = PERFECT OLLIE!"),
            ("CARVE BOOST", "HOLD A HARD CARVE, THEN STRAIGHTEN OUT", "CLEANLY FOR A BURST OF SPEED."),
            ("DRAFT", "RIDE RIGHT BEHIND A CAR GOING YOUR WAY", "AND GET PULLED ALONG FASTER."),
            ("NITRO", "GRAB THE LIGHTNING, THEN PRESS", "ACTION: (X) / E TO FIRE IT."),
            ("COMBO", "COINS, NEAR MISSES AND TRICKS BUILD IT.", "CRASH OR TUMBLE AND IT'S GONE!"),
        };

        /// <summary>Credit lines: (role, name). Studio name: set it here and in BuildScript.companyName.</summary>
        public static readonly (string role, string name)[] Credits =
        {
            ("A GAME BY", "DOWNHILL PARTY TEAM"),
            ("FONT", "LUCKIEST GUY BY ASTIGMATIC (APACHE 2.0)"),
            ("ENGINE", "UNITY"),
            ("SOUND AND MUSIC", "SYNTHESISED IN-GAME"),
            ("PLAYTESTERS", "YOU AND YOUR CREW"),
        };
    }
}
