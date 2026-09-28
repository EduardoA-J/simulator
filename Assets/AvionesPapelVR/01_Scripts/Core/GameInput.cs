using UnityEngine;
using UnityEngine.InputSystem;

namespace AvionesPapelVR
{
    /// <summary>
    /// Compatibilidad con Input System (el proyecto no usa el Input Manager antiguo).
    /// </summary>
    public static class GameInput
    {
        public static bool IsDown(Key key)
        {
            var kb = Keyboard.current;
            return kb != null && kb[key].wasPressedThisFrame;
        }

        public static bool IsHeld(Key key)
        {
            var kb = Keyboard.current;
            return kb != null && kb[key].isPressed;
        }

        public static bool IsUp(Key key)
        {
            var kb = Keyboard.current;
            return kb != null && kb[key].wasReleasedThisFrame;
        }

        public static bool MouseLeft()
        {
            var m = Mouse.current;
            return m != null && m.leftButton.isPressed;
        }

        public static bool MouseLeftDown()
        {
            var m = Mouse.current;
            return m != null && m.leftButton.wasPressedThisFrame;
        }

        public static bool MouseLeftUp()
        {
            var m = Mouse.current;
            return m != null && m.leftButton.wasReleasedThisFrame;
        }

        public static Vector2 MouseDelta()
        {
            var m = Mouse.current;
            return m != null ? m.delta.ReadValue() * 0.05f : Vector2.zero;
        }

        public static float MoveX()
        {
            float v = 0f;
            if (IsHeld(Key.A) || IsHeld(Key.LeftArrow)) v -= 1f;
            if (IsHeld(Key.D) || IsHeld(Key.RightArrow)) v += 1f;
            return v;
        }

        public static float MoveY()
        {
            float v = 0f;
            if (IsHeld(Key.S) || IsHeld(Key.DownArrow)) v -= 1f;
            if (IsHeld(Key.W) || IsHeld(Key.UpArrow)) v += 1f;
            return v;
        }

        public static bool ConfirmDown() =>
            IsDown(Key.Enter) || IsDown(Key.NumpadEnter) || IsDown(Key.Space);

        public static bool Confirm() =>
            IsHeld(Key.Space) || MouseLeft();

        public static bool ConfirmUp() =>
            IsUp(Key.Space) || MouseLeftUp();
    }
}
