using System;
using System.Reflection;

namespace Game.Simulation
{
    /// <summary>
    /// Brings a saved tuning (Unity's Assets/Settings/BoardTuning.asset, created on first import and kept by the
    /// project) up to date: Unity keeps the old serialized values when a default changes in code, so without this a
    /// project opened at M1-M3 would never see later default changes (e.g. the bigger board). A field is upgraded
    /// only while it still holds one of its earlier defaults, so values tuned by hand are left alone.
    /// Every past default comes from git history of BoardTuningData.cs (M1 74f9cf6 .. M4.3 17934b1).
    /// </summary>
    public static class TuningMigration
    {
        /// <summary>Field, and every default it had before the current one.</summary>
        public static readonly (string field, float[] oldDefaults)[] History =
        {
            ("boardLength", new[] { 6f }),
            ("boardWidth", new[] { 2.4f }),
            ("steeringSmoothingTime", new[] { 0.4f }),
            ("yawRateBaseDeg", new[] { 20f }),
            ("yawResponseTime", new[] { 0.25f }),
            ("maxRollDeg", new[] { 18f }),
            ("rollResponseTime", new[] { 0.2f }),
            ("startSpeed", new[] { 8f }),
            ("speedRampPerSecond", new[] { 0.25f }),
            ("softCapSpeed", new[] { 35f }),
            ("frontAcceleration", new[] { 4f }),
            ("playerMoveSpeed", new[] { 3.6f }),
            ("respawnSpeedFraction", new[] { 0.6f }),
            ("comboIdleTime", new[] { 4f }),
            ("nearMissDistance", new[] { 1.2f }),
        };

        static readonly BoardTuningData Defaults = new BoardTuningData();

        /// <summary>Upgrades stale defaults in place; returns how many fields changed (0 when already current).</summary>
        public static int Upgrade(BoardTuningData d)
        {
            if (d == null) return 0;
            int changed = 0;
            foreach ((string field, float[] oldDefaults) in History)
            {
                FieldInfo f = typeof(BoardTuningData).GetField(field);
                if (f == null) continue;
                float value = (float)f.GetValue(d);
                foreach (float old in oldDefaults)
                {
                    if (Math.Abs(value - old) > 1e-5f) continue;
                    f.SetValue(d, f.GetValue(Defaults));
                    changed++;
                    break;
                }
            }
            return changed;
        }
    }
}
