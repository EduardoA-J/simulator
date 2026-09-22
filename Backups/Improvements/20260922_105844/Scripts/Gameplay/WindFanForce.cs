using UnityEngine;

namespace AvionesPapelVR
{
    /// <summary>
    /// Ventilador que empuja rigidbodies cercanos (ráfagas de aire).
    /// </summary>
    public class WindFanForce : MonoBehaviour
    {
        public float force = 8f;
        public float radius = 2.5f;
        public Vector3 direction = Vector3.forward;

        void FixedUpdate()
        {
            var cols = Physics.OverlapSphere(transform.position, radius);
            var dir = transform.TransformDirection(direction.normalized);
            foreach (var c in cols)
            {
                var rb = c.attachedRigidbody;
                if (rb == null) continue;
                rb.AddForce(dir * force, ForceMode.Acceleration);
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, radius);
            Gizmos.DrawRay(transform.position, transform.TransformDirection(direction.normalized) * radius);
        }
    }
}
