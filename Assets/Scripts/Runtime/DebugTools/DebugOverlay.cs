using Game.Simulation;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// F1 overlay: centre-of-mass dot on the deck plus lateral / longitudinal / steering / roll / speed readouts.
    /// Hidden by default; the normal game communicates balance through board motion only.
    /// </summary>
    public sealed class DebugOverlay : MonoBehaviour
    {
        BoardController _board;
        BoardView _boardView;
        PlayerInputRouter _router;
        RunManager _run;
        Transform _comDot, _rawDot;
        GUIStyle _style;

        public bool Visible { get; set; }

        public void Initialize(BoardController board, BoardView boardView, PlayerInputRouter router, RunManager run)
        {
            _board = board;
            _boardView = boardView;
            _router = router;
            _run = run;
            _comDot = PrimitiveFactory.Visual(PrimitiveType.Sphere, boardView.DeckTop, Vector3.zero, Vector3.one * 0.3f, Color.magenta, "COM").transform;
            _rawDot = PrimitiveFactory.Visual(PrimitiveType.Cylinder, boardView.DeckTop, Vector3.zero, new Vector3(0.18f, 0.01f, 0.18f), Color.cyan, "SmoothedSteer").transform;
            SetMarkersVisible(false);
        }

        void SetMarkersVisible(bool visible)
        {
            if (_comDot.gameObject.activeSelf != visible) _comDot.gameObject.SetActive(visible);
            if (_rawDot.gameObject.activeSelf != visible) _rawDot.gameObject.SetActive(visible);
        }

        void LateUpdate()
        {
            SetMarkersVisible(Visible);
            if (!Visible || _board.Simulation == null) return;
            BoardState s = _board.State;
            BoardTuningData t = _board.Tuning.data;
            _comDot.localPosition = new Vector3(s.Lateral * t.HalfWidth, 0.05f, s.Longitudinal * t.HalfLength);
            // cyan disc: the smoothed steering value mapped back to a lateral position (what the board "feels")
            float smoothedLateral = SimMath.SignedPow(s.Steering, 1f / Mathf.Max(t.steeringExponent, 0.1f));
            _rawDot.localPosition = new Vector3(smoothedLateral * t.HalfWidth, 0.02f, 0f);
        }

        void OnGUI()
        {
            if (!Visible || _board == null || _board.Simulation == null) return;
            if (_style == null) _style = new GUIStyle(GUI.skin.label) { fontSize = 14, richText = true };
            BoardSimulation sim = _board.Simulation;
            BoardState s = sim.Board.State;

            GUILayout.BeginArea(new Rect(10, 10, 400, 540), GUI.skin.box);
            Line($"<b>DEBUG (F1)</b>   players {sim.ActivePlayerCount}   bots {(_router.BotsEnabled ? _router.Preset.ToString() : "off")}   keyboard P{_router.KeyboardSlot + 1}");
            Line($"lateral       {s.Lateral,7:+0.000;-0.000}");
            Line($"longitudinal  {s.Longitudinal,7:+0.000;-0.000}");
            Line($"steer raw     {s.RawSteering,7:+0.000;-0.000}   smoothed {s.Steering:+0.000;-0.000}");
            Line($"yaw rate      {s.YawRate * Mathf.Rad2Deg,7:+0.0;-0.0} deg/s");
            Line($"roll          {s.Roll * Mathf.Rad2Deg,7:+0.0;-0.0} deg");
            Line($"speed         {s.Speed,7:0.0} m/s  ({s.Speed * 3.6f:0} km/h)  target {s.TargetSpeed:0.0}");
            Line($"danger        {s.Danger,7:0.00}  wobble {s.Wobble:0.00}  grip {s.Grip:0.00}  tip {s.Tip:0.00}");
            Line($"grounded {s.Grounded}  surface {s.Surface}  crashed {s.Crashed}");
            Line($"distance {_run.Distance:0} m   lateral offset {_run.LateralOffset:+0.0;-0.0} m   crashes {_run.Crashes}");
            var streamer = _board.GetComponentInParent<GameManager>().Road.Streamer;
            Line(streamer != null ? $"chunks {streamer.ActiveChunkCount}   diamonds {_run.Diamonds}" : "finite test track");
            Line($"road steer hint {_router.LastSteerHint:+0.00;-0.00} (cooperative bots only)");
            GUILayout.Space(6);
            for (int i = 0; i < sim.ActivePlayerCount; i++)
            {
                PlayerSim p = sim.Players[i];
                string who = _router.SourceOf(i) == InputSourceKind.Bot ? _router.BotBehaviorOf(i).ToString() : _router.SourceOf(i).ToString();
                Line($"P{i + 1} {who,-15} x {p.LocalPosition.X,5:+0.00;-0.00} z {p.LocalPosition.Y,5:+0.00;-0.00} {(p.IsOnBoard ? "" : "FALLEN")} {(p.StaggerTimer > 0f ? "stagger" : "")}");
            }
            GUILayout.Space(6);
            Line("<i>Tab switch player · B bots · C bot mix · 2-6 player count · R restart · F2 tuning</i>");
            GUILayout.EndArea();
        }

        void Line(string text) => GUILayout.Label(text, _style);
    }
}
