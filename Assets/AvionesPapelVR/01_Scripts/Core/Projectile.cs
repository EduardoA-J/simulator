using UnityEngine;

namespace AvionesPapelVR
{
    public class Projectile : MonoBehaviour
    {
        public float damage = 10f;
        public float life = 3f;
        public float speed = 16f;
        public bool useInkExplosion;
        public GameObject inkExplosionPrefab;
        public LayerMask hitMask = ~0;
        public bool targetsPlayer;
        [Min(0f)] public float playerImpulse = 1.5f;

        Rigidbody _rb;
        bool _spent;

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            if (_rb == null) _rb = gameObject.AddComponent<Rigidbody>();
            _rb.useGravity = false;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        public void Launch(Vector3 direction, float overrideSpeed = -1f, Transform owner = null)
        {
            // Enemy rounds start inside their drone, so they must ignore its colliders.
            if (owner != null)
                foreach (var ownCollider in owner.GetComponentsInChildren<Collider>())
                foreach (var projectileCollider in GetComponentsInChildren<Collider>())
                    Physics.IgnoreCollision(ownCollider, projectileCollider);
            float s = overrideSpeed > 0f ? overrideSpeed : speed;
            _rb.linearVelocity = direction.normalized * s;
            Destroy(gameObject, life);
        }

        void OnTriggerEnter(Collider other) => TryHit(other);
        void OnCollisionEnter(Collision collision) => TryHit(collision.collider);

        void TryHit(Collider other)
        {
            if (_spent) return;
            if (other.transform.IsChildOf(transform)) return;
            var player = other.GetComponentInParent<PlayerHitbox>();
            if (player != null || other.CompareTag("Player"))
            {
                if (!targetsPlayer) return;
                _spent = true;
                var gm = GameManager.Instance;
                if (gm != null && gm.State == GameState.Flight && gm.Player != null && other.transform.IsChildOf(gm.Player))
                {
                    // Default enemy rounds disturb the glide without ending the run.
                    var body = gm.Player.GetComponent<Rigidbody>();
                    if (!gm.HasShield && body != null && !body.isKinematic)
                        body.AddForce(_rb.linearVelocity.normalized * playerImpulse, ForceMode.VelocityChange);
                    if (damage > 0f) gm.PlayerHit(damage);
                }
                Destroy(gameObject);
                return;
            }

            var dmg = other.GetComponentInParent<Damageable>();
            if (dmg != null)
            {
                _spent = true;
                // Enemy rounds stop at scenery/allies without friendly fire or awarding points.
                if (!targetsPlayer) dmg.TakeDamage(damage, transform.position);
                SpawnInk();
                Destroy(gameObject);
                return;
            }

            if (((1 << other.gameObject.layer) & hitMask) != 0 && !other.isTrigger)
            {
                _spent = true;
                SpawnInk();
                Destroy(gameObject);
            }
        }

        void SpawnInk()
        {
            if (!useInkExplosion) return;
            if (inkExplosionPrefab != null)
            {
                var go = Instantiate(inkExplosionPrefab, transform.position, Quaternion.identity);
                if (go.GetComponent<InkExplosion>() == null)
                    go.AddComponent<InkExplosion>();
                Destroy(go, 1.5f);
            }
            else
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = "InkBurst";
                go.transform.position = transform.position;
                go.transform.localScale = Vector3.one * 0.2f;
                Object.Destroy(go.GetComponent<Collider>());
                var r = go.GetComponent<Renderer>();
                if (r != null) r.material.color = new Color(0.05f, 0.05f, 0.1f, 0.8f);
                go.AddComponent<InkExplosion>();
            }
        }
    }
}
