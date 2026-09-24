using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace AvionesPapelVR
{
    // Actions read both OpenXR controllers and XRI simulated Input System devices.
    public static class VrInput
    {
        static readonly Dictionary<(XRNode, string), InputAction> Actions = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            foreach (var action in Actions.Values) action.Dispose();
            Actions.Clear();
        }

        static InputAction Action(XRNode node, string control, string layout)
        {
            var key = (node, control);
            if (!Actions.TryGetValue(key, out var action))
            {
                string hand = node == XRNode.LeftHand ? "LeftHand" : "RightHand";
                string binding = node == XRNode.Head ? "<XRHMD>/" + control : "<XRController>{" + hand + "}/" + control;
                action = new InputAction(node + control, InputActionType.Value,
                    binding, expectedControlType: layout);
                action.Enable();
                Actions.Add(key, action);
            }
            return action;
        }

        public static bool Trigger(XRNode node = XRNode.RightHand) =>
            Action(node, "trigger", "Axis").ReadValue<float>() > 0.7f;
        public static bool Grip(XRNode node = XRNode.RightHand) =>
            Action(node, "grip", "Axis").ReadValue<float>() > 0.7f;
        public static bool PrimaryButton(XRNode node = XRNode.RightHand) =>
            Action(node, "primaryButton", "Button").ReadValue<float>() > 0.5f;
        public static bool SecondaryButton(XRNode node = XRNode.RightHand) =>
            Action(node, "secondaryButton", "Button").ReadValue<float>() > 0.5f;
        public static bool MenuButton() =>
            Action(XRNode.LeftHand, "menuButton", "Button").ReadValue<float>() > 0.5f;
        public static bool StickClick(XRNode node = XRNode.LeftHand) =>
            Action(node, "primary2DAxisClick", "Button").ReadValue<float>() > 0.5f;
        public static Vector2 Stick(XRNode node = XRNode.LeftHand) =>
            Action(node, "primary2DAxis", "Vector2").ReadValue<Vector2>();

        public static bool TryRotation(XRNode node, out Quaternion rotation)
        {
            var tracked = Action(node, "isTracked", "Button").ReadValue<float>() > 0.5f;
            rotation = Action(node, "deviceRotation", "Quaternion").ReadValue<Quaternion>();
            return tracked && rotation != default;
        }

        public static bool TriggerDown(XRNode node, ref bool was)
        {
            bool now = Trigger(node), down = now && !was;
            was = now;
            return down;
        }

        public static bool AnyConfirmDown(ref bool wasRight, ref bool wasLeft, ref bool wasPrimary)
        {
            bool r = TriggerDown(XRNode.RightHand, ref wasRight);
            bool l = TriggerDown(XRNode.LeftHand, ref wasLeft);
            bool now = PrimaryButton(XRNode.RightHand) || PrimaryButton(XRNode.LeftHand);
            bool p = now && !wasPrimary;
            wasPrimary = now;
            return r || l || p;
        }

        public static bool IsVrLikelyAvailable() =>
            UnityEngine.InputSystem.XR.XRController.leftHand != null ||
            UnityEngine.InputSystem.XR.XRController.rightHand != null;
    }
}
