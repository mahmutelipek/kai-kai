using System.Collections.Generic;
using System.Reflection;
using Game.Simulation;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// F2 panel: every [TuningRange] field of BoardTuningData as a live slider. Edits go straight into the
    /// BoardTuning asset the simulation reads (in the Editor they persist after Play mode, like any asset edit).
    /// </summary>
    public sealed class TuningPanel : MonoBehaviour
    {
        struct Entry
        {
            public FieldInfo Field;
            public TuningRangeAttribute Range;
        }

        BoardTuning _tuning;
        readonly List<Entry> _entries = new List<Entry>();
        Vector2 _scroll;

        public bool Visible { get; set; }

        public void Initialize(BoardTuning tuning)
        {
            _tuning = tuning;
            foreach (FieldInfo field in typeof(BoardTuningData).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                var range = field.GetCustomAttribute<TuningRangeAttribute>();
                if (range != null && field.FieldType == typeof(float)) _entries.Add(new Entry { Field = field, Range = range });
            }
        }

        void OnGUI()
        {
            if (!Visible || _tuning == null) return;
            float width = 430f;
            GUILayout.BeginArea(new Rect(Screen.width - width - 10f, 10f, width, Screen.height - 20f), GUI.skin.box);
            GUILayout.Label("<b>BoardTuning (F2)</b>", new GUIStyle(GUI.skin.label) { richText = true });
            GUILayout.Label("Board size changes need a restart to update the visuals.");
            if (GUILayout.Button("Reset all to defaults")) _tuning.ResetToDefaults();

            _scroll = GUILayout.BeginScrollView(_scroll);
            string group = null;
            BoardTuningData data = _tuning.data;
            foreach (Entry e in _entries)
            {
                if (e.Range.Group != group)
                {
                    group = e.Range.Group;
                    GUILayout.Space(4);
                    GUILayout.Label("— " + group + " —");
                }
                float value = (float)e.Field.GetValue(data);
                GUILayout.BeginHorizontal();
                GUILayout.Label(e.Field.Name, GUILayout.Width(190f));
                float newValue = GUILayout.HorizontalSlider(value, e.Range.Min, e.Range.Max, GUILayout.Width(150f));
                GUILayout.Label(newValue.ToString("0.###"), GUILayout.Width(55f));
                GUILayout.EndHorizontal();
                if (!Mathf.Approximately(newValue, value)) e.Field.SetValue(data, newValue);
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
