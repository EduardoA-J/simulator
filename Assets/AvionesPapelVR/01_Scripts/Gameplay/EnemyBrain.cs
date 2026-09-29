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
        [Min(0f)] public float bulletImpulse = 1.5f;

        // Enabled only on drone instances; birds retain their existing behaviour.
        public bool useDroneTactics;
        [Min(1f)] public float engagementRange = 24f;
        [Min(0f)] public float minimumSeparation = 2.5f;
        [Min(0f)] public float pursuitLeash = 3.2f;
        [Range(0f, 5f)] public float aimSpreadDegrees = 1.25f;
        [Min(0f)] public float fireWarningDuration = 0.35f;
        public bool IsPreparingShot => _warningUntil >= 0f;

        Vector3 _origin;
        float _phase;
        float _nextFire;
        Transform _player;
        float _warningUntil = -1f;
        Renderer[] _warningRenderers;
        MaterialPropertyBlock[] _originalColors;
        MaterialPropertyBlock _warningColor;

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
            if (useDroneTactics)
            {
                _warningColor = new MaterialPropertyBlock();
                _warningRenderers = GetComponentsInChildren<Renderer>();
                _originalColors = new MaterialPropertyBlock[_warningRenderers.Length];
                for (int i = 0; i < _warningRenderers.Length; i++)
                {
                    _originalColors[i] = new MaterialPropertyBlock();
                    _warningRenderers[i].GetPropertyBlock(_originalColors[i]);
                }
            }
        }

        void Update()
        {
            if (GameManager.Instance == null || GameManager.Instance.State != GameState.Flight)
            { CancelWarning(); return; }

            // El avión pilotado lo publica GameManager: sin búsquedas por tag por frame.
            _player = GameManager.Instance.Player;

            float t = Time.time + _phase;
            var offset = new Vector3(Mathf.Sin(t * 1.3f) * amplitude, Mathf.Cos(t * 0.9f) * amplitude * 0.4f, Mathf.Sin(t * 0.5f) * 0.5f);
            var target = _origin + offset;
            if (_player != null)
            {
                // leve persecuciÃ³n
                target = useDroneTactics ? DroneTarget(target) :
                    Vector3.Lerp(target, _player.position + Vector3.up * 0.5f, 0.15f);
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation((_player.position - transform.position).normalized, Vector3.up),
                    Time.deltaTime * 3f);
            }

            Vector3 next = Vector3.MoveTowards(transform.position, target, moveSpeed * Time.deltaTime);
            if (!useDroneTactics || SafeDronePosition(next)) transform.position = next;

            if (useDroneTactics && !CanEngage()) { CancelWarning(); return; }

            if (bulletPrefab != null && Time.time >= _nextFire && _player != null)
            {
                Vector3 direction = (_player.position - transform.position).normalized;
                if (useDroneTactics)
                {
                    if (!IsPreparingShot)
                    {
                        _warningUntil = Time.time + fireWarningDuration;
                        SetWarningColor(true);
                    }
                    if (Time.time < _warningUntil) return;
                    CancelWarning();
                    var body = _player.GetComponent<Rigidbody>();
                    if (!TryInterceptDirection(transform.position, _player.position,
                        body != null ? body.linearVelocity : Vector3.zero, bulletSpeed, out direction))
                    { _nextFire = Time.time + fireInterval; return; }
                    Vector2 spread = Random.insideUnitCircle * Mathf.Tan(aimSpreadDegrees * Mathf.Deg2Rad);
                    direction = Quaternion.LookRotation(direction) * new Vector3(spread.x, spread.y, 1f).normalized;
                }
                _nextFire = Time.time + fireInterval;
                // en este arcade el "disparo" empuja al jugador con un proyectil visual no-letal de ink
                var go = Instantiate(bulletPrefab, transform.position, Quaternion.LookRotation(direction), transform.parent);
                foreach (var c in go.GetComponentsInChildren<Collider>())
                {
                    if (c is MeshCollider mesh) mesh.convex = true;
                    c.isTrigger = true;
                }
                var proj = go.GetComponent<Projectile>();
                if (proj == null) proj = go.AddComponent<Projectile>();
                proj.damage = bulletDamage;
                proj.targetsPlayer = true;
                proj.playerImpulse = bulletImpulse;
                proj.useInkExplosion = false;
                proj.Launch(direction, bulletSpeed, transform);
                Destroy(go, 2.5f);
            }
        }

        bool CanEngage()
        {
            if (_player == null || Vector3.Distance(transform.position, _player.position) > engagementRange) return false;
            var gm = GameManager.Instance;
            // Course z increases through every bend: stop firing once the pilot passes us.
            return gm.CoursePoint(_player.position).z <= gm.CoursePoint(transform.position).z + 0.5f;
        }

        Vector3 DroneTarget(Vector3 patrol)
        {
            Vector3 target = patrol;
            if (CanEngage())
            {
                Vector3 away = (_origin - _player.position).normalized;
                target = _player.position + away * minimumSeparation;
            }
            return _origin + Vector3.ClampMagnitude(target - _origin, pursuitLeash);
        }

        bool SafeDronePosition(Vector3 next)
        {
            if (_player != null && Vector3.Distance(next, _player.position) < minimumSeparation &&
                Vector3.Distance(next, _player.position) < Vector3.Distance(transform.position, _player.position)) return false;
            var gm = GameManager.Instance;
            if (gm.CurrentLevel == null) return true;
            Vector3 local = gm.CoursePoint(next);
            Vector3 right = gm.CurrentLevel.GroundRotation(local.z) * Vector3.right;
            float side = Vector3.Dot(local - gm.CurrentLevel.PathPoint(local.z), right);
            // Stay on the edges, leaving the centre open even with the larger collider.
            return Mathf.Abs(side) >= 3f && Mathf.Abs(side) <= 8.5f;
        }

        public static bool TryInterceptDirection(Vector3 origin, Vector3 target, Vector3 velocity,
            float speed, out Vector3 direction)
        {
            Vector3 delta = target - origin;
            direction = delta.normalized;
            if (speed <= 0f || delta.sqrMagnitude < 0.0001f) return false;
            float a = velocity.sqrMagnitude - speed * speed;
            float b = 2f * Vector3.Dot(delta, velocity);
            float c = delta.sqrMagnitude;
            float time;
            if (Mathf.Abs(a) < 0.001f)
                time = b < -0.001f ? -c / b : -1f;
            else
            {
                float discriminant = b * b - 4f * a * c;
                if (discriminant < 0f) return false;
                float root = Mathf.Sqrt(discriminant);
                float first = (-b - root) / (2f * a), second = (-b + root) / (2f * a);
                time = first > 0f && second > 0f ? Mathf.Min(first, second) : Mathf.Max(first, second);
            }
            if (time <= 0f || time > 2.5f) return false; // Existing projectile lifetime.
            direction = (delta + velocity * time).normalized;
            return true;
        }

        void SetWarningColor(bool active)
        {
            if (_warningRenderers == null) return;
            for (int i = 0; i < _warningRenderers.Length; i++)
            {
                if (_warningRenderers[i] == null) continue;
                if (active)
                {
                    _warningRenderers[i].GetPropertyBlock(_warningColor);
                    _warningColor.SetColor("_BaseColor", new Color(1f, 0.35f, 0.08f));
                    _warningColor.SetColor("_Color", new Color(1f, 0.35f, 0.08f));
                }
                _warningRenderers[i].SetPropertyBlock(active ? _warningColor : _originalColors[i]);
            }
        }

        void CancelWarning()
        {
            if (!IsPreparingShot) return;
            _warningUntil = -1f;
            SetWarningColor(false);
        }

        void OnDisable() => CancelWarning();
    }
}
