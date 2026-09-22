using UnityEngine;

namespace AvionesPapelVR
{
    public class EnemyBrain : MonoBehaviour
    {
        public float moveSpeed = 3f;
        public float amplitude = 1.2f;
        public float fireInterval = 2.2f;
        public GameObject bulletPrefab;
        public float bulletSpeed = 8f;
        public float bulletDamage = 0f; // enemigos hacen daÃ±o por contacto en este arcade

        Vector3 _origin;
        float _phase;
        float _nextFire;
        Transform _player;

        void Start()
        {
            _origin = transform.position;
            _phase = Random.Range(0f, Mathf.PI * 2f);
            _nextFire = Time.time + Random.Range(0.5f, fireInterval);
            if (GetComponent<Damageable>() == null)
            {
                var d = gameObject.AddComponent<Damageable>();
                d.maxHealth = 25f;
                d.scoreOnDestroy = 50;
            }
            foreach (var c in GetComponentsInChildren<Collider>())
                c.isTrigger = false;
        }

        void Update()
        {
            if (GameManager.Instance == null || GameManager.Instance.State != GameState.Flight)
                return;

            if (_player == null)
            {
                var p = GameObject.FindGameObjectWithTag("Player");
                if (p != null) _player = p.transform;
            }

            float t = Time.time + _phase;
            var offset = new Vector3(Mathf.Sin(t * 1.3f) * amplitude, Mathf.Cos(t * 0.9f) * amplitude * 0.4f, Mathf.Sin(t * 0.5f) * 0.5f);
            var target = _origin + offset;
            if (_player != null)
            {
                // leve persecuciÃ³n
                target = Vector3.Lerp(target, _player.position + Vector3.up * 0.5f, 0.15f);
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation((_player.position - transform.position).normalized, Vector3.up),
                    Time.deltaTime * 3f);
            }

            transform.position = Vector3.MoveTowards(transform.position, target, moveSpeed * Time.deltaTime);

            if (bulletPrefab != null && Time.time >= _nextFire && _player != null)
            {
                _nextFire = Time.time + fireInterval;
                // en este arcade el "disparo" empuja al jugador con un proyectil visual no-letal de ink
                var go = Instantiate(bulletPrefab, transform.position, Quaternion.LookRotation((_player.position - transform.position).normalized));
                foreach (var c in go.GetComponentsInChildren<Collider>())
                {
                    if (c is MeshCollider mesh) mesh.convex = true;
                    c.isTrigger = true;
                }
                var proj = go.GetComponent<Projectile>();
                if (proj == null) proj = go.AddComponent<Projectile>();
                proj.damage = 0f;
                proj.useInkExplosion = false;
                proj.Launch((_player.position - transform.position).normalized, bulletSpeed);
                Destroy(go, 2.5f);
            }
        }
    }
}
