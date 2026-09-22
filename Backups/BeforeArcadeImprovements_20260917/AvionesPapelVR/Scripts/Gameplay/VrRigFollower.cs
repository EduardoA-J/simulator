using System.Collections.Generic;
using UnityEngine;
using Unity.XR.CoreUtils;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;

namespace AvionesPapelVR
{
    public class VrRigFollower : MonoBehaviour
    {
        public Transform xrOrigin;
        public Transform plane;
        public Vector3 seatOffset = new Vector3(0f, 0.15f, -0.25f);
        public bool followRotation = true;
        public float positionLerp = 12f; // Kept for serialized compatibility.
        public float rotationLerp = 8f;

        bool _active, _hasLobbyPose;
        Vector3 _lobbyPos, _headLocal;
        Quaternion _lobbyRot;
        float _headYaw;
        Transform _seat;
        readonly Dictionary<Behaviour, bool> _locomotion = new();
        CharacterController _character;
        bool _characterEnabled;

        void Awake() => CaptureLobbyPose();

        public void CaptureLobbyPose()
        {
            if (xrOrigin == null || _active) return;
            _lobbyPos = xrOrigin.position;
            _lobbyRot = xrOrigin.rotation;
            _hasLobbyPose = true;
        }

        public void StartFollow(Transform targetPlane)
        {
            if (targetPlane == null || xrOrigin == null) return;
            if (_active) StopFollowAndReturnLobby();
            CaptureLobbyPose();
            plane = targetPlane;
            _seat = plane.Find("PilotSeat");
            if (_seat == null)
            {
                _seat = new GameObject("PilotSeat").transform;
                _seat.SetParent(plane, false);
                _seat.localPosition = seatOffset;
            }
            var origin = xrOrigin.GetComponent<XROrigin>();
            var camera = origin != null ? origin.Camera : xrOrigin.GetComponentInChildren<Camera>();
            _headLocal = camera != null ? xrOrigin.InverseTransformPoint(camera.transform.position) : Vector3.zero;
            _headYaw = camera != null ? Mathf.DeltaAngle(xrOrigin.eulerAngles.y, camera.transform.eulerAngles.y) : 0f;
            _locomotion.Clear();
            foreach (var b in xrOrigin.GetComponentsInChildren<Behaviour>(true))
            {
                if (b is LocomotionProvider || b is XRBodyTransformer)
                {
                    _locomotion[b] = b.enabled;
                    b.enabled = false;
                }
            }
            _character = xrOrigin.GetComponent<CharacterController>();
            if (_character != null) { _characterEnabled = _character.enabled; _character.enabled = false; }
            _active = true;
            Follow(true);
        }

        public void StopFollowAndReturnLobby()
        {
            _active = false;
            plane = null;
            _seat = null;
            if (xrOrigin != null && _hasLobbyPose)
                xrOrigin.SetPositionAndRotation(_lobbyPos, _lobbyRot);
            foreach (var pair in _locomotion)
                if (pair.Key != null) pair.Key.enabled = pair.Value;
            _locomotion.Clear();
            if (_character != null) _character.enabled = _characterEnabled;
            _character = null;
        }

        void LateUpdate()
        {
            if (_active && plane != null && xrOrigin != null) Follow(false);
        }

        void Follow(bool snap)
        {
            if (followRotation)
            {
                Vector3 forward = Vector3.ProjectOnPlane(plane.forward, Vector3.up);
                if (forward.sqrMagnitude > 0.001f)
                {
                    Quaternion rotation = Quaternion.LookRotation(forward) * Quaternion.Euler(0f, -_headYaw, 0f);
                    xrOrigin.rotation = snap ? rotation : Quaternion.Slerp(xrOrigin.rotation, rotation,
                        1f - Mathf.Exp(-rotationLerp * Time.deltaTime));
                }
            }
            // Compensate the INITIAL HMD offset. Later tracked head motion is preserved.
            xrOrigin.position = _seat.position - xrOrigin.TransformVector(_headLocal);
        }

        void OnDisable() { if (_active) StopFollowAndReturnLobby(); }
    }
}
