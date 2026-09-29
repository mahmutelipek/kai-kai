using UnityEngine;
using UnityEngine.InputSystem;

namespace Game
{
    /// <summary>Local-testing hotkeys: Tab, B, C, M, 1-6, R, F1, F2.</summary>
    public sealed class GameHotkeys : MonoBehaviour
    {
        BoardController _board;
        PlayerInputRouter _router;
        RunManager _run;
        DebugOverlay _overlay;
        TuningPanel _tuningPanel;

        static readonly Key[] CountKeys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6 };

        public void Initialize(BoardController board, PlayerInputRouter router, RunManager run, DebugOverlay overlay, TuningPanel tuningPanel)
        {
            _board = board;
            _router = router;
            _run = run;
            _overlay = overlay;
            _tuningPanel = tuningPanel;
        }

        void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null || _board == null) return;

            if (kb[Key.Tab].wasPressedThisFrame) _router.CycleKeyboardSlot(_board.Simulation.ActivePlayerCount);
            if (kb[Key.B].wasPressedThisFrame) _router.BotsEnabled = !_router.BotsEnabled;
            if (kb[Key.C].wasPressedThisFrame) _router.CyclePreset();
            if (kb[Key.R].wasPressedThisFrame) _run.RestartRun();
            if (_board.Run.State == Game.Simulation.RunState.Ended && AnyJumpPressed(kb)) _run.RestartRun();
            if (kb[Key.M].wasPressedThisFrame) _run.ToggleMode();
            if (kb[Key.F1].wasPressedThisFrame) _overlay.Visible = !_overlay.Visible;
            if (kb[Key.F2].wasPressedThisFrame) _tuningPanel.Visible = !_tuningPanel.Visible;
            for (int i = 0; i < CountKeys.Length; i++)
                if (kb[CountKeys[i]].wasPressedThisFrame) _board.Simulation.SetActivePlayerCount(i + 1);
        }

        static bool AnyJumpPressed(Keyboard kb)
        {
            if (kb[Key.Space].wasPressedThisFrame) return true;
            for (int i = 0; i < Gamepad.all.Count; i++)
                if (Gamepad.all[i].buttonSouth.wasPressedThisFrame) return true;
            return false;
        }
    }
}
