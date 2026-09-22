using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace AvionesPapelVR
{
    public class LaunchController : MonoBehaviour
    {
        public float minForce = 6f;
        public float maxForce = 18f;
        public float chargeSpeed = 1.2f;

        GameObject _plane;
        PlaneDefinition _def;
        System.Action<Vector3> _onLaunched;
        bool _active;
        float _charge;
        bool _charging;
        float _enableAt;
        bool _trigWas;

        public void Begin(GameObject plane, PlaneDefinition def, System.Action<Vector3> onLaunched)
        {
            _plane = plane;
            _def = def;
            _onLaunched = onLaunched;
            _active = true;
            _charge = 0.35f;
            _charging = false;
            _trigWas = false;
            _enableAt = Time.unscaledTime + 0.35f;
            if (GameManager.Instance == null || !GameManager.Instance.vrMode)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        void Update()
        {
            if (!_active || _plane == null) return;
            if (Time.unscaledTime < _enableAt) return;

            bool hold = GameInput.Confirm()
                        || VrInput.Trigger(XRNode.RightHand) || VrInput.Grip(XRNode.RightHand);

            if (hold)
            {
                _charging = true;
                _trigWas = true;
                _charge = Mathf.Clamp01(_charge + Time.deltaTime * chargeSpeed);
            }
            else if (_charging && _trigWas)
            {
                Launch();
                return;
            }

            if (GameInput.IsDown(Key.Enter) || GameInput.IsDown(Key.NumpadEnter)
                || VrInput.PrimaryButton(XRNode.RightHand))
            {
                _charge = Mathf.Max(_charge, 0.7f);
                Launch();
                return;
            }

            float pull = _charge * 0.35f;
            var basePos = GameManager.Instance != null && GameManager.Instance.launchAnchor != null
                ? GameManager.Instance.launchAnchor.position
                : _plane.transform.position;
            _plane.transform.position = basePos - Vector3.forward * pull;
            _plane.transform.rotation = Quaternion.Euler(-8f - _charge * 12f, 0f, 0f);
        }

        void Launch()
        {
            if (!_active) return;
            _active = false;
            float speedMul = _def != null ? Mathf.Lerp(0.75f, 1.35f, _def.speed) : 1f;
            float force = Mathf.Lerp(minForce, maxForce, _charge) * speedMul;
            var dir = (Vector3.forward + Vector3.up * 0.15f).normalized;
            var vel = dir * force;
            GameManager.Instance?.PlaySfx(GameManager.Instance.sfxLaunch, 0.7f);
            _onLaunched?.Invoke(vel);
        }

        public float Charge01 => _charge;
        public bool IsActive => _active;
    }
}
