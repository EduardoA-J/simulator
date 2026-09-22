using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace AvionesPapelVR
{
    public class FlightController : MonoBehaviour
    {
        // Preserve serialized fields used by the existing scenes/builders.
        public float baseSpeed = 8f;
        public float lookSensitivity = 2.4f;
        public float pitchLimit = 55f;
        public float bankAmount = 25f;
        public float gravityDrift = 1.2f;
        public Vector3 cameraOffset = new Vector3(0f, 0.45f, -1.35f);
        public bool vrMode;
        public bool invertPitch;
        public bool invertRoll;
        public enum VrSteering { Sticks, ControllerTilt, HeadTilt }
        public VrSteering steering = VrSteering.ControllerTilt;
        [Range(10f, 60f)] public float tiltRange = 30f;
        [Range(0f, 10f)] public float tiltDeadZone = 3f;
        Quaternion _neutralTilt;
        bool _tiltCalibrated, _modeWas, _recenterWas, _comfortWas;
        public string ControlHint => !vrMode ? "W/S: pitch | A/D: roll" :
            (steering == VrSteering.Sticks ? "Sticks: derecho Y pitch / izquierdo X roll" :
             steering == VrSteering.HeadTilt ? "Inclina la cabeza: pitch / roll" : "Inclina el mando derecho: pitch / roll") +
            " | X: modo | Y: centrar";

        bool ReadTilt(out Quaternion rotation)
        {
            if (steering == VrSteering.HeadTilt)
            {
                var gm = GameManager.Instance;
                var camera = gm != null ? gm.gameCamera : null;
                if (camera != null && VrInput.TryRotation(XRNode.Head, out _))
                {
                    rotation = gm.xrOrigin != null ? Quaternion.Inverse(gm.xrOrigin.rotation) * camera.transform.rotation : camera.transform.localRotation;
                    return true;
                }
                rotation = Quaternion.identity;
                return false;
            }
            return VrInput.TryRotation(XRNode.RightHand, out rotation);
        }

        public void CalibrateSteering()
        {
            _tiltCalibrated = ReadTilt(out _neutralTilt);
        }

        float TiltAxis(float angle)
        {
            float signed = Mathf.DeltaAngle(0f, angle);
            return Mathf.Sign(signed) * Mathf.Clamp01((Mathf.Abs(signed) - tiltDeadZone) / Mathf.Max(1f, tiltRange - tiltDeadZone));
        }

        Transform _plane;
        PlaneDefinition _def;
        Camera _cam;
        CombatShooter _shooter;
        Rigidbody _rb;
        bool _active, _secWas, _bombWas;
        float _yaw, _pitch, _roll, _pitchInput, _rollInput, _yawInput;
        float _flightTime;
        Vector3 _lastPosition;
        public float Speed => _rb != null ? _rb.linearVelocity.magnitude : 0f;
        public float Distance { get; private set; }
        public bool IsActive => _active;
        public Vector2 SteeringInput => new Vector2(_pitchInput, _rollInput);
        public Transform Plane => _plane;

        public void Possess(GameObject plane, PlaneDefinition def, Camera cam, Vector3 launchVelocity)
        {
            Release();
            if (plane == null || def == null) return;
            _plane = plane.transform;
            _def = def;
            _cam = vrMode ? null : cam;
            _shooter = plane.GetComponent<CombatShooter>();
            _rb = plane.GetComponent<Rigidbody>();
            if (_rb == null) return;
            _rb.isKinematic = false;
            _rb.useGravity = false;
            _rb.linearDamping = 0f;
            _rb.angularDamping = 2f;
            _rb.constraints = RigidbodyConstraints.FreezeRotation;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _rb.linearVelocity = Vector3.ClampMagnitude(launchVelocity, def.EffectiveMaxSpeed);
            _rb.angularVelocity = Vector3.zero;
            // A minimum speed is an aerodynamic stall threshold, never free thrust.
            if (launchVelocity.sqrMagnitude > 0.001f)
                _rb.rotation = Quaternion.LookRotation(launchVelocity.normalized, Vector3.up);
            _yaw = _rb.rotation.eulerAngles.y;
            _pitch = Mathf.DeltaAngle(0f, _rb.rotation.eulerAngles.x);
            _roll = 0f;
            _pitchInput = _rollInput = _yawInput = 0f;
            Distance = _flightTime = 0f;
            _lastPosition = _rb.position;
            _active = true;
            _tiltCalibrated = false;
            _comfortWas = _modeWas = _recenterWas = _secWas = _bombWas = false;
            if (!vrMode) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
        }

        public void Release()
        {
            if (_rb != null && !_rb.isKinematic)
            {
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
                _rb.isKinematic = true;
                _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            }
            _active = false;
            _plane = null;
            _rb = null;
            _shooter = null;
        }

        void Update()
        {
            if (!_active || _plane == null) return;
            if (vrMode)
            {
                Vector2 left = VrInput.Stick(XRNode.LeftHand), right = VrInput.Stick(XRNode.RightHand);
                _rollInput = DeadZone(left.x);
                _pitchInput = DeadZone(right.y);
                _yawInput = DeadZone(right.x);
                bool mode = VrInput.PrimaryButton(XRNode.LeftHand);
                bool recenter = VrInput.SecondaryButton(XRNode.LeftHand);
                if (mode && !_modeWas)
                {
                    steering = (VrSteering)(((int)steering + 1) % 3);
                    _tiltCalibrated = false;
                }
                if (recenter && !_recenterWas)
                {
                    GameManager.Instance?.vrRigFollower?.Recenter();
                    _tiltCalibrated = false;
                }
                bool comfort = VrInput.StickClick(XRNode.LeftHand);
                if (comfort && !_comfortWas && GameManager.Instance?.vrRigFollower != null)
                    GameManager.Instance.vrRigFollower.followRotation = !GameManager.Instance.vrRigFollower.followRotation;
                _comfortWas = comfort;
                _modeWas = mode; _recenterWas = recenter;
                if (steering != VrSteering.Sticks && ReadTilt(out var rotation))
                {
                    if (!_tiltCalibrated) { _neutralTilt = rotation; _tiltCalibrated = true; }
                    Vector3 delta = (Quaternion.Inverse(_neutralTilt) * rotation).eulerAngles;
                    _pitchInput = -TiltAxis(delta.x);
                    _rollInput = -TiltAxis(delta.z);
                }
                else _tiltCalibrated = false;
            }
            else
            {
                if (GameInput.IsDown(Key.Escape))
                {
                    bool locked = Cursor.lockState == CursorLockMode.Locked;
                    Cursor.lockState = locked ? CursorLockMode.None : CursorLockMode.Locked;
                    Cursor.visible = locked;
                }
                _rollInput = GameInput.MoveX();
                _pitchInput = GameInput.MoveY();
                _yawInput = 0f;
            }
            if (invertPitch) _pitchInput = -_pitchInput;
            if (invertRoll) _rollInput = -_rollInput;
            GameManager.Instance?.NotifyFlightControl(_pitchInput, _rollInput);
            if (vrMode ? VrInput.Trigger(XRNode.RightHand) : GameInput.MouseLeft()) _shooter?.TryFire();
            bool missile = vrMode ? VrInput.PrimaryButton(XRNode.RightHand) : GameInput.IsHeld(Key.F);
            bool bomb = vrMode ? VrInput.SecondaryButton(XRNode.RightHand) : GameInput.IsHeld(Key.G);
            if (missile && !_secWas) _shooter?.FireMissile();
            if (bomb && !_bombWas) _shooter?.FireBomb();
            _secWas = missile; _bombWas = bomb;
        }

        void FixedUpdate()
        {
            if (!_active || _rb == null || GameManager.Instance == null || GameManager.Instance.State != GameState.Flight) return;
            float dt = Time.fixedDeltaTime;
            _flightTime += dt;
            Distance += Vector3.Distance(_rb.position, _lastPosition);
            _lastPosition = _rb.position;
            float speed = Speed;
            float authority = Mathf.Clamp01(speed / Mathf.Max(0.1f, _def.stallSpeed));
            _pitch = Mathf.Clamp(_pitch - _pitchInput * _def.pitchSensitivity * authority * dt, -pitchLimit, pitchLimit);
            _roll = Mathf.MoveTowards(_roll, -_rollInput * bankAmount,
                _def.rollSensitivity * Mathf.Lerp(0.7f, 1.3f, _def.stability) * dt);
            _yaw += (_yawInput - _roll / Mathf.Max(1f, bankAmount)) * _def.turnSpeed * authority * dt;
            // Below stall speed the nose drops; no velocity floor that permits hovering.
            if (authority < 0.9f) _pitch = Mathf.MoveTowards(_pitch, 35f, (1f - authority) * 20f * dt);
            Quaternion attitude = Quaternion.Euler(_pitch, _yaw, _roll);
            _rb.MoveRotation(attitude);
            Vector3 velocity = _rb.linearVelocity;
            Vector3 forward = attitude * Vector3.forward;
            float alignment = Mathf.Lerp(0.7f, 1.8f, _def.stability) * authority;
            velocity = Vector3.RotateTowards(velocity, forward * speed, alignment * dt, 0f);
            _rb.linearVelocity = Vector3.ClampMagnitude(velocity, _def.EffectiveMaxSpeed);
            Vector3 gravity = Physics.gravity * _def.gravityMultiplier;
            Vector3 liftDirection = speed > 0.1f
                ? Vector3.ProjectOnPlane(attitude * Vector3.up, velocity.normalized).normalized : Vector3.up;
            float liftAcceleration = gravity.magnitude * _def.EffectiveLift * authority * authority;
            Vector3 acceleration = gravity + liftDirection * liftAcceleration - velocity * (_def.EffectiveDrag * speed);
            var level = GameManager.Instance.CurrentLevel;
            if (level != null)
                acceleration.x += Mathf.Sin(Time.fixedTime * 0.7f) * level.windStrength * (1f - _def.windResistance) * 0.2f;
            if (GameManager.Instance.IsTurboActive)
            {
                var turbo = GameManager.Instance.PowerUp(PowerUpType.Turbo);
                if (turbo != null && speed < _def.EffectiveMaxSpeed)
                    acceleration += forward * turbo.boostAcceleration;
            }
            _rb.AddForce(acceleration, ForceMode.Acceleration);
            if (_rb.position.y < -1f || _flightTime > 90f || Mathf.Abs(_rb.position.x) > 50f || _rb.position.z < -25f)
                GameManager.Instance.FinishRun(false);
        }

        public void ApplyBoost()
        {
            if (!_active || _rb == null) return;
            Vector3 direction = Speed > 0.1f ? _rb.linearVelocity.normalized : _plane.forward;
            float speed = Mathf.Min(_def.EffectiveMaxSpeed, Mathf.Max(Speed * _def.boostMultiplier, Speed + 2f));
            _rb.linearVelocity = direction * speed;
        }

        void LateUpdate()
        {
            if (!_active || vrMode || _cam == null || _plane == null) return;
            _cam.transform.position = _plane.TransformPoint(cameraOffset);
            _cam.transform.rotation = Quaternion.LookRotation(_plane.forward, Vector3.up);
        }

        static float DeadZone(float value) => Mathf.Abs(value) < 0.15f ? 0f : value;
    }
}
