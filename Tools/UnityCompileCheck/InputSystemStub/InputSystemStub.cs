// Compile-check stub. NOT shipped. Mirrors the public API of com.unity.inputsystem 1.11 used by Game.Runtime.
using System.Collections;
using System.Collections.Generic;
namespace UnityEngine.InputSystem.Controls
{
    public class InputControl<TValue> where TValue : struct { public TValue ReadValue() => default; }
    public class ButtonControl : InputControl<float>
    {
        public bool isPressed => false;
        public bool wasPressedThisFrame => false;
        public bool wasReleasedThisFrame => false;
    }
    public class KeyControl : ButtonControl { }
    public class Vector2Control : InputControl<Vector2> { }
    public class StickControl : Vector2Control { }
    public class DpadControl : Vector2Control { }
}
namespace UnityEngine.InputSystem.Utilities
{
    public struct ReadOnlyArray<TValue> : IReadOnlyList<TValue>
    {
        public int Count => 0;
        public TValue this[int index] => default;
        public IEnumerator<TValue> GetEnumerator() { yield break; }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
namespace UnityEngine.InputSystem
{
    using UnityEngine.InputSystem.Controls;
    using UnityEngine.InputSystem.Utilities;
    public enum Key { None, Space, Enter, Tab, A = 15, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z,
        Digit1, Digit2, Digit3, Digit4, Digit5, Digit6, Digit7, Digit8, Digit9, Digit0,
        Numpad0, Numpad1, Numpad2, Numpad3, Numpad4, Numpad5, Numpad6, Numpad7, Numpad8, Numpad9,
        LeftArrow = 63, RightArrow, UpArrow, DownArrow, Escape = 60, Backspace = 61, NumpadEnter = 77, LeftShift = 51, RightShift = 52, LeftCtrl = 55, RightCtrl = 56, F1 = 94, F2, F3, F4 }
    public class InputDevice { public int deviceId => 0; }
    public class Keyboard : InputDevice
    {
        public static Keyboard current => null;
        public KeyControl this[Key key] => null;
    }
    public class Gamepad : InputDevice
    {
        public static Gamepad current => null;
        public static ReadOnlyArray<Gamepad> all => default;
        public StickControl leftStick => null;
        public StickControl rightStick => null;
        public DpadControl dpad => null;
        public ButtonControl buttonSouth => null;
        public ButtonControl buttonWest => null;
        public ButtonControl buttonNorth => null;
        public ButtonControl buttonEast => null;
        public ButtonControl startButton => null;
        public ButtonControl selectButton => null;
        public ButtonControl leftShoulder => null;
        public ButtonControl rightShoulder => null;
    }
}
