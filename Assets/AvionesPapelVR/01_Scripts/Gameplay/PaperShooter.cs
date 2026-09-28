using UnityEngine;

namespace AvionesPapelVR
{
    /// <summary>
    /// Disparo arcade de proyectiles de papel (demo / gameplay).
    /// </summary>
    public class PaperShooter : MonoBehaviour
    {
        public GameObject projectilePrefab;
        public Transform muzzle;
        public float fireRate = 4f;
        public float projectileSpeed = 14f;
        public float projectileLife = 3f;
        public AudioClip shootClip;
        public bool autoFireDemo;

        float _next;
        AudioSource _audio;

        void Awake()
        {
            _audio = GetComponent<AudioSource>();
            if (_audio == null) _audio = gameObject.AddComponent<AudioSource>();
            _audio.spatialBlend = 1f;
            _audio.playOnAwake = false;
        }

        void Update()
        {
            if (!autoFireDemo) return;
            if (Time.time < _next) return;
            Fire();
            _next = Time.time + 1f / Mathf.Max(0.1f, fireRate);
        }

        public void Fire()
        {
            if (projectilePrefab == null) return;
            var origin = muzzle != null ? muzzle : transform;
            var go = Instantiate(projectilePrefab, origin.position, origin.rotation);
            var rb = go.GetComponent<Rigidbody>();
            if (rb == null) rb = go.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.linearVelocity = origin.forward * projectileSpeed;
            if (shootClip != null) _audio.PlayOneShot(shootClip);
            Destroy(go, projectileLife);
        }
    }
}
