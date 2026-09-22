using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace AvionesPapelVR
{
    /// <summary>
    /// Avión agarrable con el mando Oculus (grip). Al soltarlo con velocidad, lanza.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class VrGrabPlane : MonoBehaviour
    {
        public PlaneDefinition definition;
        public float throwBoost = 1f;
        public float minThrowSpeed = 1.2f;
        public float maxThrowSpeed = 24f;

        ThrowGrabInteractable _grab;
        bool _cancelled;
        Vector3 _releasePosition;
        Rigidbody _rb;
        bool _armed;
        Vector3 _homePosition;
        Quaternion _homeRotation;
        Coroutine _release;

        public bool IsHeld => _grab != null && _grab.isSelected;

        public void Arm(PlaneDefinition def)
        {
            definition = def;
            _armed = true;
            _homePosition = transform.position;
            _homeRotation = transform.rotation;
            _rb = GetComponent<Rigidbody>();
            if (_rb == null) _rb = gameObject.AddComponent<Rigidbody>();
            _rb.useGravity = false;
            _rb.isKinematic = false;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            foreach (var mesh in GetComponentsInChildren<MeshCollider>()) mesh.convex = true;

            // Asegurar collider en raíz o hijos
            if (GetComponentInChildren<Collider>() == null)
            {
                var box = gameObject.AddComponent<BoxCollider>();
                box.size = new Vector3(0.35f, 0.08f, 0.55f);
            }

            _grab = GetComponent<ThrowGrabInteractable>();
            if (_grab == null) _grab = gameObject.AddComponent<ThrowGrabInteractable>();
            _grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
            _grab.throwOnDetach = true;
            _grab.throwVelocityScale = 1f;
            _grab.throwAngularVelocityScale = 1f;
            _grab.selectEntered.RemoveListener(OnGrabbed);
            _grab.selectExited.RemoveListener(OnReleased);
            _grab.selectEntered.AddListener(OnGrabbed);
            _grab.selectExited.AddListener(OnReleased);
        }

        void OnGrabbed(SelectEnterEventArgs _)
        {
            if (!_armed) return;
            if (_release != null) StopCoroutine(_release);
            _release = null;
            _grab.ResetRelease();
            _cancelled = false;
            if (definition != null && GameManager.Instance != null)
                GameManager.Instance.NotifyVrPlaneGrabbed(definition);
        }

        void OnReleased(SelectExitEventArgs args)
        {
            if (!_armed || GameManager.Instance == null) return;
            _cancelled = args.isCanceled;
            _releasePosition = transform.position;
            _release = StartCoroutine(FinishRelease());
        }

        IEnumerator FinishRelease()
        {
            // XRI applies throw velocity in its Late phase, AFTER selectExited.
            // Resume next frame, never destroy/repossess the object in the event.
            yield return null;
            _release = null;
            if (!_armed || IsHeld || _rb == null || GameManager.Instance == null) yield break;
            var gm = GameManager.Instance;
            if (gm.State != GameState.PlaneSelect && gm.State != GameState.Launch)
                yield break;

            Vector3 vel = _grab.HasRelease ? _grab.ReleaseVelocity : Vector3.zero;
            if (_cancelled || vel.magnitude < minThrowSpeed)
            {
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
                _rb.position = _homePosition;
                _rb.rotation = _homeRotation;
                gm.NotifyVrPlaneReturned();
                yield break;
            }

            _armed = false;
            gm.BeginFlightFromVrThrow(definition, Vector3.ClampMagnitude(vel * throwBoost, maxThrowSpeed), _releasePosition);
        }

        void OnDisable()
        {
            if (_release != null) StopCoroutine(_release);
            _release = null;
            var gm = GameManager.Instance;
            if (_armed && gm != null && gm.State == GameState.Launch && gm.SelectedPlane == definition)
            {
                if (_rb != null)
                {
                    if (!_rb.isKinematic) { _rb.linearVelocity = Vector3.zero; _rb.angularVelocity = Vector3.zero; }
                    _rb.position = _homePosition; _rb.rotation = _homeRotation;
                }
                gm.NotifyVrPlaneReturned();
            }
        }

        void OnDestroy()
        {
            if (_grab == null) return;
            _grab.selectEntered.RemoveListener(OnGrabbed);
            _grab.selectExited.RemoveListener(OnReleased);
        }
    }
}
