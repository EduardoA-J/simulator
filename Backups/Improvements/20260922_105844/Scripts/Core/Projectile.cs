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

        Rigidbody _rb;
        bool _spent;

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            if (_rb == null) _rb = gameObject.AddComponent<Rigidbody>();
            _rb.useGravity = false;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        public void Launch(Vector3 direction, float overrideSpeed = -1f)
        {
            float s = overrideSpeed > 0f ? overrideSpeed : speed;
            _rb.linearVelocity = direction.normalized * s;
            Destroy(gameObject, life);
        }

        void OnTriggerEnter(Collider other) => TryHit(other);
        void OnCollisionEnter(Collision collision) => TryHit(collision.collider);

        void TryHit(Collider other)
        {
            if (_spent) return;
            if (other.CompareTag("Player") || other.transform.IsChildOf(transform)) return;

            var dmg = other.GetComponentInParent<Damageable>();
            if (dmg != null)
            {
                _spent = true;
                dmg.TakeDamage(damage, transform.position);
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
