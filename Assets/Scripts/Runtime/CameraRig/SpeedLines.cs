using UnityEngine;

namespace Game
{
    /// <summary>
    /// Thin white streaks rushing past the edges of the view at high speed. Kept away from the screen centre so
    /// they never cover the board or the road ahead.
    /// </summary>
    public sealed class SpeedLines : MonoBehaviour
    {
        const int Count = 28;
        const float Near = 2.5f, Far = 22f;

        public float startAtSpeedFraction = 0.55f;

        BoardController _board;
        readonly Transform[] _lines = new Transform[Count];
        readonly Vector2[] _slots = new Vector2[Count];
        readonly float[] _depth = new float[Count];

        public static SpeedLines Create(Camera camera, BoardController board)
        {
            var root = new GameObject("SpeedLines").transform;
            root.SetParent(camera.transform, false);
            var lines = root.gameObject.AddComponent<SpeedLines>();
            lines._board = board;
            for (int i = 0; i < Count; i++)
            {
                GameObject line = PrimitiveFactory.Visual(PrimitiveType.Cube, root, Vector3.zero, new Vector3(0.025f, 0.025f, 1.6f), Color.white, "Line");
                line.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lines._lines[i] = line.transform;
                lines.Respawn(i, Random.Range(Near, Far));
            }
            return lines;
        }

        void Respawn(int i, float depth)
        {
            // ring around the view axis, wider than the board's silhouette
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float radius = Random.Range(2.6f, 5f);
            _slots[i] = new Vector2(Mathf.Cos(angle) * radius * 1.6f, Mathf.Sin(angle) * radius);
            _depth[i] = depth;
        }

        void LateUpdate()
        {
            if (_board == null || _board.Simulation == null) return;
            float cap = Mathf.Max(_board.Tuning.data.softCapSpeed, 1f);
            float intensity = Mathf.InverseLerp(startAtSpeedFraction, 1.1f, _board.State.Speed / cap);
            bool visible = intensity > 0.01f && !_board.State.Crashed;
            float move = _board.State.Speed * 1.5f * Time.deltaTime;
            for (int i = 0; i < Count; i++)
            {
                bool show = visible && i < Mathf.CeilToInt(Count * intensity);
                if (_lines[i].gameObject.activeSelf != show) _lines[i].gameObject.SetActive(show);
                if (!show) continue;
                _depth[i] -= move;
                if (_depth[i] < Near) Respawn(i, Far);
                _lines[i].localPosition = new Vector3(_slots[i].x, _slots[i].y, _depth[i]);
                _lines[i].localScale = new Vector3(0.025f, 0.025f, 0.8f + 2.2f * intensity);
            }
        }
    }
}
