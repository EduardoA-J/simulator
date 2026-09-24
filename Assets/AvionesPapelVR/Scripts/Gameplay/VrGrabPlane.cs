using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace AvionesPapelVR
{
    /// <summary>Ciclo de vida de un avión expuesto en la mesa. Única fuente de verdad de su estado.</summary>
    public enum TablePlaneState
    {
        /// <summary>Quieto en su posición de la mesa. Rigidbody cinemático; nadie más lo mueve.</summary>
        OnTable,
        /// <summary>Sujeto por un interactor XRI, que es quien manda sobre el Transform.</summary>
        Held,
        /// <summary>Soltado; a la espera de decidir (siguiente frame) entre lanzar o volver a la mesa.</summary>
        Launching,
        /// <summary>Retirado: ya se convirtió en vuelo o la mesa se está desmontando. No reacciona a nada.</summary>
        Consumed
    }

    /// <summary>
    /// Avión de la mesa agarrable con Grip. Es el propietario del movimiento fuera del agarre:
    /// en <see cref="TablePlaneState.OnTable"/> el cuerpo es cinemático y se mantiene en su pose de origen;
    /// durante <see cref="TablePlaneState.Held"/> XRI mueve el Transform y aquí sólo se muestrea la velocidad
    /// de la mano; al soltar se decide lanzar (el vuelo lo realiza un avión nuevo que crea GameManager) o volver.
    /// El Rigidbody de mesa nunca recibe velocidad ni fuerzas.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class VrGrabPlane : MonoBehaviour
    {
        public PlaneDefinition definition;
        public float throwBoost = 1f;
        public float minThrowSpeed = 1.2f;
        public float maxThrowSpeed = 24f;
        [Tooltip("Ventana de suavizado de la velocidad de la mano al soltar.")]
        public float throwSmoothingDuration = 0.2f;

        const int SampleCount = 24;
        readonly Vector3[] _sampleVelocity = new Vector3[SampleCount];
        readonly float[] _sampleTime = new float[SampleCount];
        int _sampleIndex;
        bool _hasSample;
        Vector3 _lastSamplePosition;

        ThrowGrabInteractable _grab;
        Rigidbody _rb;
        Transform _homeParent;
        Vector3 _homePosition;
        Quaternion _homeRotation;
        bool _cancelled;
        Vector3 _releaseVelocity;
        Vector3 _releasePosition;
        Coroutine _release;

        public TablePlaneState State { get; private set; } = TablePlaneState.Consumed;
        public bool IsHeld => State == TablePlaneState.Held;
        public bool IsArmed => State != TablePlaneState.Consumed;

        /// <summary>Prepara el avión en su pose actual como pose de mesa y habilita el agarre.</summary>
        public void Arm(PlaneDefinition def)
        {
            definition = def;
            _homeParent = transform.parent;
            _homePosition = transform.position;
            _homeRotation = transform.rotation;

            _rb = GetComponent<Rigidbody>();
            if (_rb == null) _rb = gameObject.AddComponent<Rigidbody>();
            ConfigureTableBody(_rb);
            foreach (var mesh in GetComponentsInChildren<MeshCollider>()) mesh.convex = true;
            if (GetComponentInChildren<Collider>() == null)
            {
                var box = gameObject.AddComponent<BoxCollider>();
                box.size = new Vector3(0.35f, 0.08f, 0.55f);
            }

            _grab = GetComponent<ThrowGrabInteractable>();
            if (_grab == null) _grab = gameObject.AddComponent<ThrowGrabInteractable>();
            _grab.ApplyTablePlaneSettings();
            _grab.selectEntered.RemoveListener(OnGrabbed);
            _grab.selectExited.RemoveListener(OnReleased);
            _grab.selectEntered.AddListener(OnGrabbed);
            _grab.selectExited.AddListener(OnReleased);
            _grab.enabled = true;
            State = TablePlaneState.OnTable;
        }

        /// <summary>Configuración física de un avión en reposo: cinemático, sin gravedad, sin velocidad.</summary>
        public static void ConfigureTableBody(Rigidbody body)
        {
            if (body == null) return;
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.None;
            body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            body.constraints = RigidbodyConstraints.None;
        }

        /// <summary>
        /// Devuelve el avión a su pose de mesa y limpia agarre, velocidad, temporizadores y corrutinas.
        /// Seguro de llamar desde cualquier estado; deja el avión en <see cref="TablePlaneState.OnTable"/>.
        /// </summary>
        public void ResetToTable()
        {
            if (_release != null) StopCoroutine(_release);
            _release = null;
            _cancelled = false;
            _hasSample = false;
            System.Array.Clear(_sampleTime, 0, SampleCount);
            // Mientras se cancela el agarre XRI no debe reentrar la lógica de soltar.
            State = TablePlaneState.Consumed;
            if (_grab != null && _grab.isSelected && _grab.interactionManager != null)
                _grab.interactionManager.CancelInteractableSelection((UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable)_grab);
            if (_homeParent != null && transform.parent != _homeParent && _homeParent.gameObject.activeInHierarchy)
                transform.SetParent(_homeParent, true);
            transform.SetPositionAndRotation(_homePosition, _homeRotation);
            if (_rb != null)
            {
                ConfigureTableBody(_rb);
                _rb.position = _homePosition;
                _rb.rotation = _homeRotation;
            }
            if (_grab != null) _grab.enabled = true;
            State = TablePlaneState.OnTable;
        }

        /// <summary>Retira el avión del ciclo: ya no agarra, no lanza y no notifica. Previo a destruirlo.</summary>
        public void Retire()
        {
            if (_release != null) StopCoroutine(_release);
            _release = null;
            State = TablePlaneState.Consumed;
            if (_grab != null) _grab.enabled = false;
        }

        void OnGrabbed(SelectEnterEventArgs _)
        {
            if (State == TablePlaneState.Consumed) return;
            if (_release != null) StopCoroutine(_release);
            _release = null;
            _cancelled = false;
            _hasSample = false;
            System.Array.Clear(_sampleTime, 0, SampleCount);
            State = TablePlaneState.Held;
            if (definition != null && GameManager.Instance != null)
                GameManager.Instance.NotifyVrPlaneGrabbed(definition);
        }

        void OnReleased(SelectExitEventArgs args)
        {
            if (State != TablePlaneState.Held) return;
            // XRI ya restauró el cuerpo (cinemático). Decidimos el siguiente frame, nunca dentro del evento.
            _cancelled = args.isCanceled;
            _releaseVelocity = SmoothedVelocity();
            _releasePosition = transform.position;
            State = TablePlaneState.Launching;
            if (isActiveAndEnabled) _release = StartCoroutine(FinishRelease());
            else FinishReleaseNow();
        }

        IEnumerator FinishRelease()
        {
            yield return null;
            _release = null;
            FinishReleaseNow();
        }

        void FinishReleaseNow()
        {
            if (State != TablePlaneState.Launching) return;
            var gm = GameManager.Instance;
            if (_grab != null && _grab.isSelected) { State = TablePlaneState.Held; return; }
            bool canLaunch = gm != null && (gm.State == GameState.PlaneSelect || gm.State == GameState.Launch);
            float speed = _releaseVelocity.magnitude;
            if (_cancelled || !canLaunch || speed < minThrowSpeed)
            {
                ResetToTable();
                gm?.NotifyVrPlaneReturned();
                return;
            }
            Vector3 launch = Vector3.ClampMagnitude(_releaseVelocity * throwBoost, maxThrowSpeed);
            State = TablePlaneState.Consumed;
            if (!gm.BeginFlightFromVrThrow(definition, launch, _releasePosition))
            {
                ResetToTable();
                gm.NotifyVrPlaneReturned();
            }
        }

        void LateUpdate()
        {
            if (State != TablePlaneState.Held) return;
            float dt = Time.deltaTime;
            Vector3 position = transform.position;
            if (_hasSample && dt > 0.0005f)
            {
                _sampleVelocity[_sampleIndex] = (position - _lastSamplePosition) / dt;
                _sampleTime[_sampleIndex] = Time.time;
                _sampleIndex = (_sampleIndex + 1) % SampleCount;
            }
            _lastSamplePosition = position;
            _hasSample = true;
        }

        Vector3 SmoothedVelocity()
        {
            Vector3 sum = Vector3.zero;
            float weights = 0f;
            float now = Time.time;
            for (int i = 0; i < SampleCount; i++)
            {
                if (_sampleTime[i] <= 0f) continue;
                float age = now - _sampleTime[i];
                if (age > throwSmoothingDuration) continue;
                float weight = 1f - age / Mathf.Max(0.01f, throwSmoothingDuration);
                sum += _sampleVelocity[i] * weight;
                weights += weight;
            }
            return weights > 0f ? sum / weights : Vector3.zero;
        }

        void FixedUpdate()
        {
            // Vigilancia: en la mesa nadie más puede mover el avión. Corrige cualquier deriva externa.
            if (State != TablePlaneState.OnTable || _rb == null) return;
            if (!_rb.isKinematic) ConfigureTableBody(_rb);
            if ((_rb.position - _homePosition).sqrMagnitude > 1e-6f || Quaternion.Angle(_rb.rotation, _homeRotation) > 0.25f)
            {
                transform.SetPositionAndRotation(_homePosition, _homeRotation);
                _rb.position = _homePosition;
                _rb.rotation = _homeRotation;
            }
        }

        void OnDisable()
        {
            if (_release != null) StopCoroutine(_release);
            _release = null;
            if (State == TablePlaneState.Held || State == TablePlaneState.Launching)
            {
                // Desactivado con el avión en la mano (p. ej. cambio de estado externo): vuelve a la mesa sin lanzar.
                var gm = GameManager.Instance;
                bool notify = gm != null && gm.State == GameState.Launch && gm.SelectedPlane == definition;
                _cancelled = true;
                ResetToTable();
                if (notify) gm.NotifyVrPlaneReturned();
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
