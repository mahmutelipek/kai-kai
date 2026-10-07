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
        [TuningRange("Board", 3f, 10f)] public float boardLength = 6f;
        [TuningRange("Board", 1.2f, 4f)] public float boardWidth = 2.4f;
        [TuningRange("Board", 0.15f, 0.6f)] public float wheelRadius = 0.35f;
        [TuningRange("Board", 0.4f, 1.5f)] public float deckHeight = 0.85f;

        // ---------------- Weight -> steering ----------------
        [TuningRange("Steering", 1f, 3f)] public float steeringExponent = 1.6f;
        [TuningRange("Steering", 0.05f, 1.5f)] public float steeringSmoothingTime = 0.4f;
        [TuningRange("Steering", 0f, 1f)] public float airborneWeightFactor = 0.25f;
        [TuningRange("Steering", 0f, 60f)] public float yawRateBaseDeg = 20f;
        [TuningRange("Steering", 0f, 4f)] public float yawRatePerSpeedDeg = 1.2f;
        [TuningRange("Steering", 0.02f, 1f)] public float yawResponseTime = 0.25f;
        [TuningRange("Steering", 1f, 30f)] public float tractionAlignRate = 8f;

        // ---------------- Arcade handling (zero preserves the original test tuning) ----------------
        [TuningRange("Steering", 0f, .85f)] public float driftGripLoss = 0f;
        [TuningRange("Steering", 5f, 25f)] public float driftMaxSlipDeg = 18f;
        [TuningRange("Speed", 0f, 2f)] public float slopeAccelerationScale = 0f;
        [TuningRange("Players", 0f, 1f)] public float edgeAssist = 0f;

        // ---------------- Roll ----------------
        [TuningRange("Roll", 0f, 35f)] public float maxRollDeg = 18f;
        [TuningRange("Roll", 0.02f, 1f)] public float rollResponseTime = 0.2f;

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
        [TuningRange("Speed", 1f, 20f)] public float startSpeed = 8f;
        [TuningRange("Speed", 0f, 2f)] public float speedRampPerSecond = 0.25f;
        [TuningRange("Speed", 10f, 80f)] public float softCapSpeed = 35f;
        [TuningRange("Speed", 0.05f, 2f)] public float cruiseGain = 0.35f;
        [TuningRange("Speed", 0f, 15f)] public float frontAcceleration = 4f;
        [TuningRange("Speed", 0f, 20f)] public float rearBraking = 6f;
        [TuningRange("Speed", 0f, 0.5f)] public float frontAccelFadeAboveCap = 0.15f;
        [TuningRange("Speed", 0f, 10f)] public float minSpeed = 3f;
        [TuningRange("Speed", 0f, 20f)] public float offroadDrag = 5f;
        [TuningRange("Speed", 0f, 1f)] public float lightImpactSpeedLoss = 0.08f;

        // ---------------- Vertical ----------------
        [TuningRange("Vertical", 5f, 40f)] public float gravity = 20f;
        [TuningRange("Vertical", 1f, 20f)] public float hardLandingSpeed = 6f;
        [TuningRange("Vertical", 2f, 50f)] public float fallOutOfWorldDepth = 15f;

        // ---------------- Crash ----------------
        [TuningRange("Crash", 0.5f, 6f)] public float crashRestartDelay = 2.5f;
        [TuningRange("Crash", 1f, 40f)] public float crashDeceleration = 14f;

        // ---------------- Players ----------------
        [TuningRange("Players", 0.15f, 0.6f)] public float playerRadius = 0.33f;
        [TuningRange("Players", 0.5f, 2f)] public float playerHeight = 1.1f;
        [TuningRange("Players", 0.5f, 8f)] public float playerMoveSpeed = 3.6f;
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
