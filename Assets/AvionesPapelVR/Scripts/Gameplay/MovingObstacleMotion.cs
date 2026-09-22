using UnityEngine;

namespace AvionesPapelVR
{
    public class MovingObstacleMotion : MonoBehaviour
    {
        public Vector3 axis = Vector3.right;
        public float distance = 1.5f;
        public float speed = 1f;
        Vector3 _origin;
        Rigidbody _body;
        void Start()
        {
            _origin = transform.position;
            foreach (var mesh in GetComponentsInChildren<MeshCollider>()) mesh.convex = true;
            _body = GetComponent<Rigidbody>();
            if (_body == null) _body = gameObject.AddComponent<Rigidbody>();
            _body.isKinematic = true;
            _body.useGravity = false;
            _body.interpolation = RigidbodyInterpolation.Interpolate;
            _body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }
        void FixedUpdate()
        {
            if (_body == null || GameManager.Instance == null || GameManager.Instance.State != GameState.Flight) return;
            _body.MovePosition(_origin + axis.normalized * (Mathf.Sin(Time.fixedTime * speed) * distance));
        }
    }
}
