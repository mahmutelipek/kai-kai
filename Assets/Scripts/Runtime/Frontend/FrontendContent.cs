namespace Game.Frontend
{
    /// <summary>Static text of the HOW TO PLAY and CREDITS screens (English keys; Loc translates).</summary>
    public static class FrontendContent
    {
        /// <summary>Six cards: heading, two short lines. Order = reading order (left to right, top to bottom).</summary>
        public static readonly (string heading, string line1, string line2)[] HowToPlay =
        {
            ("STEER BY STANDING", "WALK WITH STICK / WASD: THE BOARD GOES", "WHERE THE CREW STANDS."),
            ("RAMPS", "HIT A RAMP FAST TO TAKE OFF AND FLY.", "STAY BALANCED FOR THE LANDING!"),
            ("CARVE BOOST", "HOLD A HARD CARVE, THEN STRAIGHTEN OUT", "CLEANLY FOR A BURST OF SPEED."),
            ("DRAFT", "RIDE RIGHT BEHIND A CAR GOING YOUR WAY", "AND GET PULLED ALONG FASTER."),
            ("OFF ROAD", "GRASS IS FAIR GAME, BUT YOU HAVE", "8 SECONDS TO GET BACK ON THE ROAD."),
            ("BEST DISTANCE", "NO POINTS: RIDE AS FAR AS YOU CAN.", "THREE WIPEOUTS END THE RUN!"),
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
