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
        float _next;
        AudioSource _audio;

        public void Setup(PlaneDefinition def, GameObject bullet, GameObject missile, GameObject bomb, AudioClip shoot)
        {
            bulletPrefab = bullet;
            missilePrefab = missile;
            bombPrefab = bomb;
            shootClip = shoot;
            _damageMul = def != null ? Mathf.Lerp(0.7f, 1.6f, def.firePower) : 1f;
            fireRate = def != null ? Mathf.Lerp(4f, 9f, def.firePower) : 6f;

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
            _next = Time.time + 1f / Mathf.Max(0.1f, fireRate);
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
            SpawnSpecial(missilePrefab, 28f, 22f, true);
        }

        public void FireBomb()
        {
            SpawnSpecial(bombPrefab, 40f, 8f, true);
        }

        void SpawnBullet(Quaternion yawOffset)
        {
            if (bulletPrefab == null) return;
            var origin = muzzle != null ? muzzle : transform;
            var go = Instantiate(bulletPrefab, origin.position, origin.rotation * yawOffset);
            PrepareProjectile(go, 10f * _damageMul, 18f, true);
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
