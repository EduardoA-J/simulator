using UnityEngine;

namespace AvionesPapelVR
{
    /// <summary>
    /// Animación simple de vuelo planeado (balanceo + avance demo).
    /// </summary>
    public class PlaneFlightDemo : MonoBehaviour
    {
        public float forwardSpeed = 0.4f;
        public float bankAmount = 12f;
        public float bankSpeed = 1.5f;
        public bool loopPath;
        public float loopRadius = 1.5f;
        float _t;
        Vector3 _origin;

        void Start() => _origin = transform.position;

        void Update()
        {
            _t += Time.deltaTime;
            float bank = Mathf.Sin(_t * bankSpeed) * bankAmount;
            transform.localRotation = Quaternion.Euler(bank * 0.25f, transform.localEulerAngles.y, -bank);

            if (loopPath)
            {
                float ang = _t * forwardSpeed;
                transform.position = _origin + new Vector3(Mathf.Cos(ang) * loopRadius, 0f, Mathf.Sin(ang) * loopRadius);
                transform.rotation = Quaternion.LookRotation(
                    new Vector3(-Mathf.Sin(ang), 0f, Mathf.Cos(ang)), Vector3.up) *
                    Quaternion.Euler(bank * 0.25f, 0f, -bank);
            }
        }
    }
}
