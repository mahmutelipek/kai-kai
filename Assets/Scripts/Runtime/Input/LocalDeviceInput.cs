using Game.Simulation;
using UnityEngine.InputSystem;
using SVec2 = System.Numerics.Vector2;

namespace Game
{
    /// <summary>Reads keyboard and gamepads (new Input System) into PlayerInputState. Move is board-local: up = toward the nose.</summary>
    public static class LocalDeviceInput
    {
        public static PlayerInputState ReadKeyboard()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return PlayerInputState.None;
            float x = 0f, y = 0f;
            if (kb[Key.A].isPressed || kb[Key.LeftArrow].isPressed) x -= 1f;
            if (kb[Key.D].isPressed || kb[Key.RightArrow].isPressed) x += 1f;
            if (kb[Key.S].isPressed || kb[Key.DownArrow].isPressed) y -= 1f;
            if (kb[Key.W].isPressed || kb[Key.UpArrow].isPressed) y += 1f;
            return new PlayerInputState(new SVec2(x, y), kb[Key.Space].isPressed, kb[Key.E].isPressed, kb[Key.LeftShift].isPressed || kb[Key.RightShift].isPressed);
        }

        public static PlayerInputState ReadGamepad(Gamepad pad)
        {
            if (pad == null) return PlayerInputState.None;
            UnityEngine.Vector2 stick = pad.leftStick.ReadValue();
            UnityEngine.Vector2 dpad = pad.dpad.ReadValue();
            if (dpad.sqrMagnitude > stick.sqrMagnitude) stick = dpad;
            if (stick.sqrMagnitude < 0.02f) stick = UnityEngine.Vector2.zero; // dead zone
            return new PlayerInputState(stick.ToSim(), pad.buttonSouth.isPressed, pad.buttonWest.isPressed, pad.leftTrigger.ReadValue() > .35f);
        }

        public static int GamepadCount => Gamepad.all.Count;

        public static Gamepad GetGamepad(int index) => index < Gamepad.all.Count ? Gamepad.all[index] : null;
    }
}
