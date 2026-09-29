using Game.Simulation;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// The single tuning asset. Wraps the engine-independent BoardTuningData so the simulation and the
    /// headless tests share exactly the same values. Edited live through the F2 tuning panel.
    /// </summary>
    [CreateAssetMenu(menuName = "Downhill/Board Tuning", fileName = "BoardTuning")]
    public sealed class BoardTuning : ScriptableObject
    {
        public BoardTuningData data = new BoardTuningData();

        public static BoardTuning CreateDefault()
        {
            var tuning = CreateInstance<BoardTuning>();
            tuning.name = "BoardTuning (runtime default)";
            return tuning;
        }

        public void ResetToDefaults() => data = new BoardTuningData();

        void OnValidate()
        {
            if (data == null) data = new BoardTuningData();
            if (!data.Validate(out string error)) Debug.LogWarning($"BoardTuning: {error}", this);
        }
    }
}
