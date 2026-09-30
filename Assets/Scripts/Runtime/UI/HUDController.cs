using System.Collections.Generic;
using Game.Hud;
using Game.Simulation;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// The HUD of the reference image, drawn with IMGUI from engine-free draw commands (Game.Hud.HudLayout):
    /// distance + best on torn brush panels, coin / gem pills, slanted COMBO and NITRO READY! banners, big speed
    /// with a wedge of segments, P1..P6 avatars, countdown, popups, danger / nitro vignettes, end screen.
    /// The same commands render in the headless preview, so screenshots show this layout.
    /// Anchored to Screen.safeArea, scaled from 1080p; strings are only rebuilt when values change
    /// (skills: game-ui-ux, performance-optimization).
    /// </summary>
    public sealed class HUDController : MonoBehaviour
    {
        BoardController _board;
        RunManager _run;
        PlayerInputRouter _router;
        DebugOverlay _overlay;
        readonly HudPresenter _presenter = new HudPresenter();
        readonly List<HudCmd> _cmds = new List<HudCmd>(128);
        bool _endFilled;

        public bool Visible { get; set; } = true;
        public HudPresenter Presenter => _presenter;

        public void Initialize(BoardController board, RunManager run, PlayerInputRouter router, DebugOverlay overlay)
        {
            _board = board;
            _run = run;
            _router = router;
            _overlay = overlay;
            _board.Stepped += OnStepped;
            // pre-build every HUD texture now: generating one mid-run (first danger vignette) would hitch
            HudDrawer.Prewarm();
        }

        void OnDestroy()
        {
            if (_board != null) _board.Stepped -= OnStepped;
        }

        void OnStepped(RunStepEvents ev) => _presenter.OnStep(ev, _board.Run);

        /// <summary>Short message in the popup column (e.g. "RIDERS: 1" from the count hotkeys).</summary>
        public void Toast(string text) => _presenter.Push(text, HudColor.White);

        public void SetCountdown(string text) => _presenter.SetCountdown(text);

        void Update()
        {
            if (_board == null || _board.Run == null) return;
            RunSimulation r = _board.Run;
            _presenter.Update(r, _run != null ? _run.BestDistance : 0f, Time.unscaledTime, Time.unscaledDeltaTime);
            HudState s = _presenter.State;
            for (int i = 0; i < s.Human.Length; i++) s.Human[i] = _router != null && _router.IsHumanControlled(i);
            s.DebugPanelOpen = _overlay != null && _overlay.Visible;
            if (s.Ended && !_endFilled) { _presenter.FillEndScreen(r); _endFilled = true; }
            if (!s.Ended) _endFilled = false;
        }

        void OnGUI()
        {
            if (!Visible || _board == null || _board.Run == null || Event.current.type != EventType.Repaint) return;
            Rect safe = Screen.safeArea;
            HudLayout.Build(_presenter.State, Screen.width, Screen.height, safe.x, Screen.height - safe.yMax, safe.width, safe.height, _cmds);
            HudDrawer.Draw(_cmds);
        }
    }
}
