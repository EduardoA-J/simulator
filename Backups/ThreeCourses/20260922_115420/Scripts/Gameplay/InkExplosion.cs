using UnityEngine;

namespace AvionesPapelVR
{
    /// <summary>
    /// Efecto simple de "explosión de tinta" (escala + fade demo).
    /// </summary>
    public class InkExplosion : MonoBehaviour
    {
        public float life = 0.6f;
        public float maxScale = 2.5f;
        float _t;
        Vector3 _base;

        void Start() => _base = transform.localScale;

        void Update()
        {
            _t += Time.deltaTime;
            float k = Mathf.Clamp01(_t / life);
            transform.localScale = _base * Mathf.Lerp(0.2f, maxScale, k);
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                foreach (var m in r.materials)
                {
                    if (m.HasProperty("_BaseColor"))
                    {
                        var c = m.GetColor("_BaseColor");
                        c.a = 1f - k;
                        m.SetColor("_BaseColor", c);
                    }
                }
            }
            if (_t >= life) Destroy(gameObject);
        }
    }
}
