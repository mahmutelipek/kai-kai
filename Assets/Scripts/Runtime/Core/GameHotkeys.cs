using UnityEngine;
using UnityEngine.InputSystem;

namespace Game
{
    /// <summary>Local-testing hotkeys: Tab, B, C, M, 1-6 (and numpad), R, F1, F2, F3 (reduce motion).</summary>
    public sealed class GameHotkeys : MonoBehaviour
    {
        BoardController _board;
        PlayerInputRouter _router;
        RunManager _run;
        DebugOverlay _overlay;
        TuningPanel _tuningPanel;

        static readonly Key[] CountKeys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6 };
        static readonly Key[] NumpadKeys = { Key.Numpad1, Key.Numpad2, Key.Numpad3, Key.Numpad4, Key.Numpad5, Key.Numpad6 };
        static readonly string[] RiderToasts = { "1", "2", "3", "4", "5", "6" };
        HUDController _hud;

        public void Initialize(BoardController board, PlayerInputRouter router, RunManager run, DebugOverlay overlay, TuningPanel tuningPanel, HUDController hud = null)
        {
            _hud = hud;
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

            // developer keys only in the editor and development builds (the shipped game uses the lobby / menus)
            bool dev = Application.isEditor || Debug.isDebugBuild;
            // end screen: jump restarts (only while the HUD shows it, not behind the title menu)
            if (_board.Run.State == Game.Simulation.RunState.Ended && (_hud == null || _hud.Visible) && AnyJumpPressed(kb)) _run.RestartRun();
            if (kb[Key.F3].wasPressedThisFrame)
            {
                CameraController.ReduceMotion = !CameraController.ReduceMotion;
                _hud?.Toast(Game.Frontend.Loc.T(CameraController.ReduceMotion ? "REDUCED MOTION ON" : "REDUCED MOTION OFF"));
            }
            if (!dev) return;
            if (kb[Key.Tab].wasPressedThisFrame) _router.CycleKeyboardSlot(_board.Simulation.ActivePlayerCount);
            if (kb[Key.B].wasPressedThisFrame) _router.BotsEnabled = !_router.BotsEnabled;
            if (kb[Key.C].wasPressedThisFrame) _router.CyclePreset();
            if (kb[Key.R].wasPressedThisFrame) _run.RestartRun();
            if (kb[Key.M].wasPressedThisFrame) _run.ToggleMode();
            if (kb[Key.F1].wasPressedThisFrame) _overlay.Visible = !_overlay.Visible;
            if (kb[Key.F2].wasPressedThisFrame) _tuningPanel.Visible = !_tuningPanel.Visible;
            // 1..6 (top row or numpad) = exactly that many riders on the board; 1 leaves one rider
            for (int i = 0; i < CountKeys.Length; i++)
            {
                if (!kb[CountKeys[i]].wasPressedThisFrame && !kb[NumpadKeys[i]].wasPressedThisFrame) continue;
                _board.Simulation.SetActivePlayerCount(i + 1);
                _hud?.Toast(Game.Frontend.Loc.T("RIDERS: ") + RiderToasts[i]);
            }
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
