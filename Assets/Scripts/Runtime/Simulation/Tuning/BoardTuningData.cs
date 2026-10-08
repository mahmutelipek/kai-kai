using System;

namespace Game.Simulation
{
    /// <summary>
    /// Every gameplay tuning value in one place. Lives inside the BoardTuning ScriptableObject in Unity
    /// and is used directly by headless tests. Units are in the field names where not obvious.
    /// </summary>
    [Serializable]
    public class BoardTuningData
    {
        // ---------------- Board geometry ----------------
        [TuningRange("Board", 3f, 10f)] public float boardLength = 6.75f;
        [TuningRange("Board", 1.2f, 4f)] public float boardWidth = 2.7f;
        [TuningRange("Board", 0.15f, 0.6f)] public float wheelRadius = 0.35f;
        [TuningRange("Board", 0.4f, 1.5f)] public float deckHeight = 0.85f;

        // ---------------- Weight -> steering ----------------
        [TuningRange("Steering", 1f, 3f)] public float steeringExponent = 1.6f;
        [TuningRange("Steering", 0.05f, 1.5f)] public float steeringSmoothingTime = 0.28f;
        [TuningRange("Steering", 0f, 1f)] public float airborneWeightFactor = 0.25f;
        [TuningRange("Steering", 0f, 60f)] public float yawRateBaseDeg = 24f;
        [TuningRange("Steering", 0f, 4f)] public float yawRatePerSpeedDeg = 1.2f;
        [TuningRange("Steering", 0.02f, 1f)] public float yawResponseTime = 0.18f;
        [TuningRange("Steering", 1f, 30f)] public float tractionAlignRate = 8f;

        // ---------------- Roll ----------------
        [TuningRange("Roll", 0f, 35f)] public float maxRollDeg = 24f;
        [TuningRange("Roll", 0.02f, 1f)] public float rollResponseTime = 0.15f;

        // ---------------- Stability / danger ----------------
        [TuningRange("Stability", 0.5f, 3f)] public float stabilityFactorLowSpeed = 1.25f;
        [TuningRange("Stability", 0.5f, 3f)] public float stabilityFactorHighSpeed = 1.8f;
        [TuningRange("Stability", 5f, 60f)] public float stabilityReferenceSpeed = 35f;
        [TuningRange("Stability", 0f, 1f)] public float frontWeightInstability = 0.15f;
        [TuningRange("Stability", 0.3f, 2f)] public float crashThreshold = 1f;
        [TuningRange("Stability", 0.3f, 0.99f)] public float wobbleStartFraction = 0.7f;
        [TuningRange("Stability", 0f, 20f)] public float wobbleMaxRollDeg = 6f;
        [TuningRange("Stability", 0.5f, 10f)] public float wobbleFrequencyHz = 4.5f;
        [TuningRange("Stability", 0f, 1f)] public float gripLossMax = 0.5f;
        [TuningRange("Stability", 0.1f, 3f)] public float crashTipTime = 0.5f;
        [TuningRange("Stability", 0.1f, 5f)] public float tipRecoveryTime = 1f;
        [TuningRange("Stability", 0f, 60f)] public float tipExtraRollDeg = 25f;

        // ---------------- Speed ----------------
        [TuningRange("Speed", 1f, 20f)] public float startSpeed = 12f;
        [TuningRange("Speed", 0f, 2f)] public float speedRampPerSecond = 0.4f;
        [TuningRange("Speed", 10f, 80f)] public float softCapSpeed = 38f;
        [TuningRange("Speed", 0.05f, 2f)] public float cruiseGain = 0.35f;
        [TuningRange("Speed", 0f, 15f)] public float frontAcceleration = 6f;
        [TuningRange("Speed", 0f, 20f)] public float rearBraking = 6f;
        [TuningRange("Speed", 0f, 0.5f)] public float frontAccelFadeAboveCap = 0.15f;
        [TuningRange("Speed", 0f, 10f)] public float minSpeed = 3f;
        [TuningRange("Speed", 0f, 20f)] public float offroadDrag = 5f;
        [TuningRange("Speed", 0f, 1f)] public float lightImpactSpeedLoss = 0.08f;

        // ---------------- Obstacles / impacts (M2) ----------------
        [TuningRange("Impacts", 0f, 1f)] public float heavyImpactSpeedLoss = 0.3f;
        [TuningRange("Impacts", 5f, 40f)] public float crashImpactSpeed = 16f;
        [TuningRange("Impacts", 0f, 2f)] public float heavyStaggerTime = 0.7f;
        [TuningRange("Impacts", 0f, 1f)] public float heavyFallThreshold = 0.55f;
        [TuningRange("Impacts", 0f, 0.5f)] public float potholeSpeedLoss = 0.06f;
        [TuningRange("Impacts", 0f, 0.5f)] public float wallScrapeSpeedLoss = 0.12f;
        [TuningRange("Impacts", 2f, 30f)] public float wallCrashLateralSpeed = 11f;

        // ---------------- Difficulty (M2) ----------------
        [TuningRange("Difficulty", 500f, 30000f)] public float difficultyFullDistance = 8000f;
        [TuningRange("Difficulty", 0f, 1f)] public float difficultySpeedCapBonus = 0.25f;

        // ---------------- Nitro (M3) ----------------
        [TuningRange("Nitro", 0.5f, 8f)] public float nitroDuration = 3f;
        [TuningRange("Nitro", 0f, 30f)] public float nitroSpeedBonus = 12f;
        [TuningRange("Nitro", 0f, 30f)] public float nitroAcceleration = 10f;
        /// <summary>Stability multiplier while boosting: >1 means the same weight shift is more dangerous.</summary>
        [TuningRange("Nitro", 1f, 3f)] public float nitroInstability = 1.35f;

        // ---------------- Moves (M4.3): crew ollie, carve boost, slipstream ----------------
        /// <summary>Board hop when enough of the crew jumps together (solo: every jump).</summary>
        [TuningRange("Moves", 0f, 12f)] public float ollieVelocity = 6.5f;
        /// <summary>Hop when every rider on the deck jumps together.</summary>
        [TuningRange("Moves", 0f, 14f)] public float perfectOllieVelocity = 8f;
        [TuningRange("Moves", 0.05f, 1f)] public float ollieWindow = 0.35f;
        [TuningRange("Moves", 0.2f, 1f)] public float ollieCrewFraction = 0.6f;
        [TuningRange("Moves", 0f, 3f)] public float ollieCooldown = 0.9f;
        /// <summary>Hold a carve at least this hard (|steering|) ...</summary>
        [TuningRange("Moves", 0.1f, 1f)] public float carveMinSteering = 0.3f;
        /// <summary>... for this long, then straighten out (not wobbling) for a speed boost.</summary>
        [TuningRange("Moves", 0.1f, 3f)] public float carveMinTime = 0.8f;
        [TuningRange("Moves", 0f, 15f)] public float carveBoostSpeed = 5f;
        [TuningRange("Moves", 0f, 3f)] public float carveBoostTime = 1.2f;
        [TuningRange("Moves", 0f, 30f)] public float carveBoostAcceleration = 9f;
        /// <summary>Riding right behind a car going the same way: less drag, more speed.</summary>
        [TuningRange("Moves", 0f, 40f)] public float slipstreamDistance = 16f;
        [TuningRange("Moves", 0f, 4f)] public float slipstreamWidth = 1.6f;
        [TuningRange("Moves", 0f, 15f)] public float slipstreamBonus = 5f;
        [TuningRange("Moves", 0f, 3f)] public float slipstreamBuildTime = 0.5f;

        // ---------------- Scoring (M3) ----------------
        [TuningRange("Score", 0f, 10f)] public float pointsPerMeter = 1f;
        [TuningRange("Score", 0f, 100f)] public float coinPoints = 10f;
        [TuningRange("Score", 0f, 1000f)] public float diamondPoints = 100f;
        [TuningRange("Score", 0f, 500f)] public float nearMissPoints = 50f;
        [TuningRange("Score", 0f, 1000f)] public float airtimePointsPerSecond = 100f;
        [TuningRange("Score", 1f, 30f)] public float comboStep = 10f;
        [TuningRange("Score", 1f, 20f)] public float comboMaxMultiplier = 6f;
        [TuningRange("Score", 0.5f, 15f)] public float comboIdleTime = 5f;
        [TuningRange("Score", 0f, 10f)] public float comboDrainPerSecond = 2f;
        [TuningRange("Score", 0f, 5f)] public float nearMissDistance = 1.5f;
        [TuningRange("Score", 0f, 30f)] public float nearMissMinSpeed = 10f;
        [TuningRange("Score", 0f, 2f)] public float minScoredAirtime = 0.35f;
        [TuningRange("Score", 0f, 10f)] public float livesPerRun = 3f;
        [TuningRange("Score", 0f, 500f)] public float olliePoints = 20f;
        [TuningRange("Score", 0f, 500f)] public float carveBoostPoints = 40f;
        [TuningRange("Score", 0f, 500f)] public float slipstreamPoints = 30f;

        // ---------------- Vertical ----------------
        [TuningRange("Vertical", 5f, 40f)] public float gravity = 20f;
        [TuningRange("Vertical", 1f, 30f)] public float hardLandingSpeed = 22f;
        [TuningRange("Vertical", 2f, 50f)] public float fallOutOfWorldDepth = 15f;
        /// <summary>Leaving the top of a ramp: the climb speed is multiplied and topped up so ramps really throw the board into the air.</summary>
        [TuningRange("Vertical", 1f, 4f)] public float rampLaunchBoost = 2.4f;
        [TuningRange("Vertical", 0f, 8f)] public float rampLaunchBase = 4f;
        [TuningRange("Vertical", 0f, 5f)] public float rampLaunchMinRise = 1f;
        /// <summary>Upper limit of the launch speed: keeps even a fast board in the air under ~1.7 s so it lands on the same stretch of road.</summary>
        [TuningRange("Vertical", 4f, 20f)] public float rampLaunchMax = 9.5f;

        // ---------------- Crash ----------------
        [TuningRange("Crash", 0.5f, 6f)] public float crashRestartDelay = 2.5f;
        [TuningRange("Crash", 1f, 40f)] public float crashDeceleration = 14f;
        /// <summary>After a crash the board resumes at this fraction of its previous cruise speed (not from zero).</summary>
        [TuningRange("Crash", 0f, 1f)] public float respawnSpeedFraction = 0.7f;

        // ---------------- Players ----------------
        [TuningRange("Players", 0.15f, 0.6f)] public float playerRadius = 0.33f;
        [TuningRange("Players", 0.5f, 2f)] public float playerHeight = 1.1f;
        [TuningRange("Players", 0.5f, 8f)] public float playerMoveSpeed = 4.2f;
        [TuningRange("Players", 1f, 60f)] public float playerGroundAcceleration = 20f;
        [TuningRange("Players", 0f, 30f)] public float playerAirAcceleration = 6f;
        [TuningRange("Players", 1f, 10f)] public float playerJumpVelocity = 4.5f;
        [TuningRange("Players", 5f, 40f)] public float playerGravity = 14f;
        [TuningRange("Players", 0f, 1f)] public float playerJumpCooldown = 0.25f;
        [TuningRange("Players", 0.3f, 5f)] public float respawnDelay = 1.5f;
        [TuningRange("Players", 0f, 1f)] public float bumpRestitution = 0.5f;
        [TuningRange("Players", 0.5f, 8f)] public float staggerRelativeSpeed = 2.8f;
        [TuningRange("Players", 0f, 1.5f)] public float staggerTime = 0.35f;
        [TuningRange("Players", 0f, 1f)] public float staggerControlFactor = 0.2f;
        [TuningRange("Players", 0f, 20f)] public float dangerSlideAcceleration = 5f;
        [TuningRange("Players", 0f, 1f)] public float boardInertiaFactor = 0.25f;
        [TuningRange("Players", 0f, 1f)] public float landingStaggerFactor = 1f;

        public float HalfLength => boardLength * 0.5f;
        public float HalfWidth => boardWidth * 0.5f;

        /// <summary>
        /// Rough time from "the crew decides to steer" to "the board turns": walking (~0.3 s) + weight smoothing
        /// + yaw response. Predictive controllers (cooperative bots, the test driver) aim that far ahead.
        /// </summary>
        public float CrewResponseDelay => 0.3f + steeringSmoothingTime + yawResponseTime;

        public BoardTuningData Clone() => (BoardTuningData)MemberwiseClone();

        /// <summary>Returns false (with a reason) for values that would break the simulation.</summary>
        public bool Validate(out string error)
        {
            foreach (var field in typeof(BoardTuningData).GetFields())
            {
                if (field.FieldType != typeof(float)) continue;
                float v = (float)field.GetValue(this);
                if (!SimMath.IsFinite(v)) { error = field.Name + " is not finite"; return false; }
            }
            if (boardLength <= 0f || boardWidth <= 0f) { error = "board size must be positive"; return false; }
            if (crashThreshold <= 0f) { error = "crashThreshold must be positive"; return false; }
            if (softCapSpeed <= minSpeed) { error = "softCapSpeed must exceed minSpeed"; return false; }
            error = null;
            return true;
        }
    }
}
