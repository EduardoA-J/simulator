using UnityEngine;

namespace AvionesPapelVR
{
    public class PlayerHitbox : MonoBehaviour
    {
        public float invulnTime = 1f;
        float _nextHurt;

        void OnCollisionEnter(Collision collision)
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.State != GameState.Flight || Time.time < _nextHurt) return;
            var other = collision.collider;
            if (other.GetComponentInParent<Projectile>() != null || other.GetComponentInParent<PlayerHitbox>() != null) return;
            bool ground = other.name == "Ground" || other.name == "Floor" || other.name == "LandingZone";
            if (ground)
            {
                Vector3 velocity = collision.relativeVelocity;
                bool topContact = collision.contactCount > 0 && collision.GetContact(0).normal.y > 0.65f;
                bool level = Vector3.Dot(transform.up, Vector3.up) > 0.85f;
                gm.FinishRun(topContact && level && Mathf.Abs(velocity.y) < 2.5f && velocity.magnitude < 14f);
                return;
            }
            _nextHurt = Time.time + invulnTime;
            gm.PlayerHit(1f); // Any solid collision is a crash, including a collider named Mesh.
        }
    }
}
