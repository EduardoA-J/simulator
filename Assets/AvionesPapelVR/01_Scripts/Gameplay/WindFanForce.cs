using UnityEngine;

namespace AvionesPapelVR
{
    /// <summary>
    /// Ventilador que empuja al avión del jugador cuando pasa cerca (ráfaga de aire).
    /// Sólo actúa durante el vuelo y sólo sobre el cuerpo pilotado: nunca sobre aviones de mesa ni escenografía.
    /// </summary>
    public class WindFanForce : MonoBehaviour
    {
        public float force = 8f;
        public float radius = 2.5f;
        public Vector3 direction = Vector3.forward;

        void FixedUpdate()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.State != GameState.Flight) return;
            var player = gm.Player;
            if (player == null) return;
            float sqrRadius = radius * radius;
            if ((player.position - transform.position).sqrMagnitude > sqrRadius) return;
            var rb = gm.PlayerBody;
            if (rb == null || rb.isKinematic) return;
            var dir = transform.TransformDirection(direction.normalized);
            rb.AddForce(dir * force, ForceMode.Acceleration);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, radius);
            Gizmos.DrawRay(transform.position, transform.TransformDirection(direction.normalized) * radius);
        }
    }
}
