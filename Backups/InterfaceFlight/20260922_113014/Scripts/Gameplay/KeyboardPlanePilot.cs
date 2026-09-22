using UnityEngine;
using UnityEngine.InputSystem;

namespace AvionesPapelVR
{
    /// <summary>
    /// Piloto de prueba con teclado/ratón (Input System).
    /// </summary>
    public class KeyboardPlanePilot : MonoBehaviour
    {
        [Header("Movimiento")]
        public float moveSpeed = 4f;
        public float lookSensitivity = 2.2f;
        public float boostMultiplier = 2.2f;
        public float pitchLimit = 80f;

        [Header("Disparo")]
        public PaperShooter shooter;
        public Key fireKey = Key.Space;

        [Header("Cámara")]
        public Transform cameraPivot;
        public Vector3 cameraOffset = new Vector3(0f, 0.35f, -1.2f);

        float _yaw;
        float _pitch;
        Vector3 _startPos;
        Quaternion _startRot;
        bool _locked;

        void Start()
        {
            _startPos = transform.position;
            _startRot = transform.rotation;
            _yaw = transform.eulerAngles.y;
            _pitch = 0f;

            if (cameraPivot == null && Camera.main != null)
                cameraPivot = Camera.main.transform;

            if (shooter == null)
                shooter = GetComponent<PaperShooter>();

            LockCursor(true);
        }

        void Update()
        {
            if (GameInput.IsDown(Key.Escape))
                LockCursor(!_locked);

            if (GameInput.IsDown(Key.R))
            {
                transform.SetPositionAndRotation(_startPos, _startRot);
                _yaw = _startRot.eulerAngles.y;
                _pitch = 0f;
            }

            if (!_locked) return;

            Vector2 md = GameInput.MouseDelta();
            _yaw += md.x * lookSensitivity;
            _pitch = Mathf.Clamp(_pitch - md.y * lookSensitivity, -pitchLimit, pitchLimit);
            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);

            float h = GameInput.MoveX();
            float v = GameInput.MoveY();
            float up = 0f;
            if (GameInput.IsHeld(Key.E)) up += 1f;
            if (GameInput.IsHeld(Key.Q)) up -= 1f;
            if (GameInput.IsHeld(Key.PageUp)) up += 1f;
            if (GameInput.IsHeld(Key.PageDown)) up -= 1f;

            var dir = (transform.forward * v + transform.right * h + Vector3.up * up).normalized;
            float speed = moveSpeed * (GameInput.IsHeld(Key.LeftShift) ? boostMultiplier : 1f);
            transform.position += dir * speed * Time.deltaTime;

            if (GameInput.IsDown(fireKey) && shooter != null)
                shooter.Fire();

            UpdateCamera();
        }

        void UpdateCamera()
        {
            if (cameraPivot == null) return;
            var target = transform.TransformPoint(cameraOffset);
            cameraPivot.position = Vector3.Lerp(cameraPivot.position, target, 1f - Mathf.Exp(-10f * Time.deltaTime));
            cameraPivot.rotation = Quaternion.Slerp(cameraPivot.rotation, transform.rotation, 1f - Mathf.Exp(-10f * Time.deltaTime));
        }

        void LockCursor(bool value)
        {
            _locked = value;
            Cursor.lockState = value ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !value;
        }

        void OnGUI()
        {
            const string help =
                "TECLADO: WASD mover | Ratón mirar | Q/E altura | Shift turbo | Espacio disparar | R reiniciar | Esc liberar ratón";
            GUI.Label(new Rect(16, 12, Screen.width - 32, 28), help);
        }
    }
}
