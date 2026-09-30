using Game.Simulation;
using UnityEngine.InputSystem;
using SVec2 = System.Numerics.Vector2;

namespace Game
{
    /// <summary>Reads keyboard and gamepads (new Input System) into PlayerInputState. Move is board-local: up = toward the nose.</summary>
    public static class LocalDeviceInput
    {
        public enum KeyboardScheme { Full, Left, Right }

        /// <summary>
        /// Full: WASD or arrows, Space jump, E nitro (one keyboard rider). Split for two riders on one keyboard:
        /// Left = WASD / Space / E, Right = arrows / Enter / Right Shift.
        /// </summary>
        public static PlayerInputState ReadKeyboard(KeyboardScheme scheme = KeyboardScheme.Full)
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return PlayerInputState.None;
            bool wasd = scheme != KeyboardScheme.Right, arrows = scheme != KeyboardScheme.Left;
            float x = 0f, y = 0f;
            if ((wasd && kb[Key.A].isPressed) || (arrows && kb[Key.LeftArrow].isPressed)) x -= 1f;
            if ((wasd && kb[Key.D].isPressed) || (arrows && kb[Key.RightArrow].isPressed)) x += 1f;
            if ((wasd && kb[Key.S].isPressed) || (arrows && kb[Key.DownArrow].isPressed)) y -= 1f;
            if ((wasd && kb[Key.W].isPressed) || (arrows && kb[Key.UpArrow].isPressed)) y += 1f;
            bool jump = scheme == KeyboardScheme.Right ? kb[Key.Enter].isPressed || kb[Key.NumpadEnter].isPressed : kb[Key.Space].isPressed;
            bool action = scheme == KeyboardScheme.Right ? kb[Key.RightShift].isPressed : kb[Key.E].isPressed;
            return new PlayerInputState(new SVec2(x, y), jump, action);
        }

        public static Gamepad GamepadById(int deviceId)
        {
            for (int i = 0; i < Gamepad.all.Count; i++) if (Gamepad.all[i].deviceId == deviceId) return Gamepad.all[i];
            return null;
        }

        public static PlayerInputState ReadGamepad(Gamepad pad)
        {
            if (pad == null) return PlayerInputState.None;
            UnityEngine.Vector2 stick = pad.leftStick.ReadValue();
            UnityEngine.Vector2 dpad = pad.dpad.ReadValue();
            if (dpad.sqrMagnitude > stick.sqrMagnitude) stick = dpad;
            if (stick.sqrMagnitude < 0.02f) stick = UnityEngine.Vector2.zero; // dead zone
            return new PlayerInputState(stick.ToSim(), pad.buttonSouth.isPressed, pad.buttonWest.isPressed);
        }

        public static int GamepadCount => Gamepad.all.Count;

        public static Gamepad GetGamepad(int index) => index < Gamepad.all.Count ? Gamepad.all[index] : null;
    }
}
