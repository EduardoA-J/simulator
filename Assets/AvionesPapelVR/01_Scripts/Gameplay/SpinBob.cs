using UnityEngine;

namespace AvionesPapelVR
{
    /// <summary>
    /// Rota colectibles / power-ups para feedback visual en VR.
    /// </summary>
    public class SpinBob : MonoBehaviour
    {
        public float spinSpeed = 90f;
        public float bobAmplitude = 0.08f;
        public float bobSpeed = 2f;
        Vector3 _origin;

        void Start() => _origin = transform.localPosition;

        void Update()
        {
            transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
            var p = _origin;
            p.y += Mathf.Sin(Time.time * bobSpeed) * bobAmplitude;
            transform.localPosition = p;
        }
    }
}
