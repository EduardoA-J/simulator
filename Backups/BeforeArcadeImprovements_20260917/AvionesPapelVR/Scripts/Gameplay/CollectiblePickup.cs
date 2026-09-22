using UnityEngine;

namespace AvionesPapelVR
{
    public class CollectiblePickup : MonoBehaviour
    {
        public enum Kind { Star, Coin, Part, Turbo, Shield, Triple, Ring, BoostRing }
        public Kind kind = Kind.Star;
        public float magnetRange = 2.5f;
        public float ringRadius = 1.15f;
        bool _collected, _hasPrevious;
        Vector3 _previous;
        Transform _trackedPlane;
        Rigidbody _body;
        public bool IsRing => kind == Kind.Ring || kind == Kind.BoostRing;

        void Start()
        {
            foreach (var c in GetComponentsInChildren<Collider>())
            {
                if (c is MeshCollider mesh) mesh.convex = true;
                c.isTrigger = true;
            }
            if (GetComponentInChildren<Collider>() == null)
            {
                var sphere = gameObject.AddComponent<SphereCollider>();
                sphere.isTrigger = true;
                sphere.radius = IsRing ? ringRadius : 0.25f;
            }
            _body = GetComponent<Rigidbody>();
            if (_body == null) _body = gameObject.AddComponent<Rigidbody>();
            _body.isKinematic = true;
            _body.useGravity = false;
            var oldSpin = GetComponent<SpinBob>();
            if (oldSpin != null) { oldSpin.enabled = false; Destroy(oldSpin); }
            if (!IsRing)
            {
                var visual = GetComponentInChildren<MeshRenderer>();
                if (visual != null && visual.gameObject != gameObject && visual.GetComponent<SpinBob>() == null)
                    visual.gameObject.AddComponent<SpinBob>();
            }
        }

        void FixedUpdate()
        {
            var gm = GameManager.Instance;
            if (_collected || gm == null || gm.State != GameState.Flight || gm.Player == null) return;
            var player = gm.Player;
            if (IsRing)
            {
                Vector3 current = transform.InverseTransformPoint(player.position);
                if (_hasPrevious && _trackedPlane == player && _previous.z < 0f && current.z >= 0f)
                {
                    float fraction = -_previous.z / (current.z - _previous.z);
                    Vector3 crossing = Vector3.Lerp(_previous, current, fraction);
                    // Segment-plane crossing prevents collecting on approach and catches fast throws.
                    if (crossing.x * crossing.x + crossing.y * crossing.y < ringRadius * ringRadius)
                        Collect();
                }
                _previous = current;
                _trackedPlane = player;
                _hasPrevious = true;
            }
            else if (_body != null && Vector3.Distance(transform.position, player.position) < magnetRange)
                _body.MovePosition(Vector3.MoveTowards(_body.position, player.position, 6f * Time.fixedDeltaTime));
        }

        void OnTriggerEnter(Collider other)
        {
            if (IsRing) return;
            if (other.GetComponentInParent<PlayerHitbox>() != null) Collect();
        }

        void Collect()
        {
            var gm = GameManager.Instance;
            if (_collected || gm == null || gm.State != GameState.Flight) return;
            _collected = true; // Guard before callbacks; Destroy is deferred.
            foreach (var collider in GetComponentsInChildren<Collider>()) collider.enabled = false;
            switch (kind)
            {
                case Kind.Star: gm.CollectStar(); break;
                case Kind.Coin: gm.CollectCoin(); break;
                case Kind.Part: gm.CollectPart(); break;
                case Kind.Turbo: gm.ActivatePowerUp(PowerUpType.Turbo, 2f); break;
                case Kind.Shield: gm.ActivatePowerUp(PowerUpType.Shield, 6f); break;
                case Kind.Triple: gm.ActivatePowerUp(PowerUpType.TripleShot, 5f); break;
                case Kind.Ring: gm.CollectRing(false); break;
                case Kind.BoostRing: gm.CollectRing(true); break;
            }
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
