using UnityEngine;

namespace AvionesPapelVR
{
    public class Damageable : MonoBehaviour
    {
        public float maxHealth = 30f;
        public int scoreOnDestroy = 50;
        public bool destroyOnDeath = true;
        public GameObject deathVfxPrefab;
        public AudioClip deathClip;

        public float CurrentHealth { get; private set; }
        public bool IsDead => CurrentHealth <= 0f;

        public System.Action<Damageable> Died;

        void Awake() => CurrentHealth = maxHealth;

        public void ResetHealth() => CurrentHealth = maxHealth;

        public void TakeDamage(float amount, Vector3 hitPoint)
        {
            if (IsDead) return;
            CurrentHealth -= amount;
            if (CurrentHealth <= 0f)
            {
                CurrentHealth = 0f;
                Die(hitPoint);
            }
        }

        void Die(Vector3 hitPoint)
        {
            Died?.Invoke(this);
            GameManager.Instance?.AddScore(scoreOnDestroy);

            if (deathVfxPrefab != null)
            {
                var fx = Instantiate(deathVfxPrefab, hitPoint, Quaternion.identity);
                Destroy(fx, 2f);
            }

            if (deathClip != null)
                AudioSource.PlayClipAtPoint(deathClip, hitPoint, 0.7f);

            if (destroyOnDeath)
                Destroy(gameObject);
            else
                gameObject.SetActive(false);
        }
    }
}
