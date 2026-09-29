using System;

namespace Game.Simulation
{
    /// <summary>
    /// Marks a BoardTuningData field as editable in the runtime debug tuning panel.
    /// Engine-independent replacement for UnityEngine.RangeAttribute.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class TuningRangeAttribute : Attribute
    {
        public readonly float Min;
        public readonly float Max;
        public readonly string Group;

        public TuningRangeAttribute(string group, float min, float max)
        {
            Group = group;
            Min = min;
            Max = max;
        }
    }
}
