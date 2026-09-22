using UnityEngine;

namespace AvionesPapelVR
{
    public class CombatShooter : MonoBehaviour
    {
        public Transform muzzle;
        public float fireRate = 6f;
        public GameObject bulletPrefab;
        public GameObject missilePrefab;
        public GameObject bombPrefab;
        public AudioClip shootClip;

        float _damageMul = 1f;
        float _next, _nextMissile, _nextBomb;
        WeaponDefinition _bullet, _missile, _bomb;
        AudioSource _audio;

        public void Setup(PlaneDefinition def, GameObject bullet, GameObject missile, GameObject bomb, AudioClip shoot)
        {
            bulletPrefab = bullet;
            missilePrefab = missile;
            bombPrefab = bomb;
            shootClip = shoot;
            _damageMul = def != null ? Mathf.Lerp(0.7f, 1.6f, def.firePower) : 1f;
            var gm = GameManager.Instance;
            _bullet = gm != null ? gm.Weapon(WeaponType.PaperBullet) : null;
            _missile = gm != null ? gm.Weapon(WeaponType.FoldMissile) : null;
            _bomb = gm != null ? gm.Weapon(WeaponType.InkBomb) : null;
            if (_bullet != null) bulletPrefab = _bullet.projectilePrefab;
            if (_missile != null) missilePrefab = _missile.projectilePrefab;
            if (_bomb != null) bombPrefab = _bomb.projectilePrefab;
            fireRate = _bullet != null ? 1f / Mathf.Max(0.01f, _bullet.cooldown) : 6f;

            if (muzzle == null)
            {
                var m = new GameObject("Muzzle");
                m.transform.SetParent(transform, false);
                m.transform.localPosition = new Vector3(0f, 0f, 0.35f);
                muzzle = m.transform;
            }

            _audio = GetComponent<AudioSource>();
            if (_audio == null) _audio = gameObject.AddComponent<AudioSource>();
            _audio.spatialBlend = 1f;
            _audio.playOnAwake = false;
        }

        public void TryFire()
        {
            if (Time.time < _next) return;
            if (_bullet == null) return;
            _next = Time.time + Mathf.Max(0.01f, _bullet.cooldown);
            bool triple = GameManager.Instance != null && GameManager.Instance.HasTripleShot;
            if (triple)
            {
                SpawnBullet(Quaternion.Euler(0f, -8f, 0f));
                SpawnBullet(Quaternion.identity);
                SpawnBullet(Quaternion.Euler(0f, 8f, 0f));
            }
            else
            {
                SpawnBullet(Quaternion.identity);
            }
            if (shootClip != null) _audio.PlayOneShot(shootClip, 0.4f);
        }

        public void FireMissile()
        {
            if (_missile == null || Time.time < _nextMissile) return;
            _nextMissile = Time.time + Mathf.Max(0.01f, _missile.cooldown);
            SpawnSpecial(_missile.projectilePrefab, _missile.damage, _missile.speed, true);
        }

        public void FireBomb()
        {
            if (_bomb == null || Time.time < _nextBomb) return;
            _nextBomb = Time.time + Mathf.Max(0.01f, _bomb.cooldown);
            SpawnSpecial(_bomb.projectilePrefab, _bomb.damage, _bomb.speed, true);
        }

        void SpawnBullet(Quaternion yawOffset)
        {
            if (_bullet == null || _bullet.projectilePrefab == null) return;
            var origin = muzzle != null ? muzzle : transform;
            var go = Instantiate(_bullet.projectilePrefab, origin.position, origin.rotation * yawOffset);
            PrepareProjectile(go, _bullet.damage * _damageMul, _bullet.speed, true);
        }

        void SpawnSpecial(GameObject prefab, float dmg, float spd, bool ink)
        {
            if (prefab == null) return;
            var origin = muzzle != null ? muzzle : transform;
            var go = Instantiate(prefab, origin.position, origin.rotation);
            PrepareProjectile(go, dmg * _damageMul, spd, ink);
            if (shootClip != null) _audio.PlayOneShot(shootClip, 0.55f);
        }

        void PrepareProjectile(GameObject go, float damage, float speed, bool ink)
        {
            foreach (var c in go.GetComponentsInChildren<Collider>())
            {
                if (c is MeshCollider mesh) mesh.convex = true;
                c.isTrigger = true;
            }
            foreach (var rb in go.GetComponentsInChildren<Rigidbody>())
            {
                rb.useGravity = false;
                rb.isKinematic = false;
            }
            var proj = go.GetComponent<Projectile>();
            if (proj == null) proj = go.AddComponent<Projectile>();
            proj.damage = damage;
            proj.speed = speed;
            proj.useInkExplosion = ink;
            proj.Launch(go.transform.forward, speed);
            // evitar auto-hit
            IgnorePlayerCollisions(go);
        }

        void IgnorePlayerCollisions(GameObject go)
        {
            var myCols = GetComponentsInChildren<Collider>();
            var otherCols = go.GetComponentsInChildren<Collider>();
            foreach (var a in myCols)
            foreach (var b in otherCols)
                if (a != null && b != null)
                    Physics.IgnoreCollision(a, b);
        }
    }
}
