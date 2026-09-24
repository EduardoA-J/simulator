using UnityEngine;

namespace AvionesPapelVR
{
    /// <summary>
    /// Efecto simple de "explosión de tinta" (escala + fade).
    /// Los materiales instanciados se cachean una vez y se liberan al destruir el efecto.
    /// </summary>
    public class InkExplosion : MonoBehaviour
    {
        public float life = 0.6f;
        public float maxScale = 2.5f;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        float _t;
        Vector3 _base;
        Material[] _materials = System.Array.Empty<Material>();
        Color[] _baseColors = System.Array.Empty<Color>();

        void Start()
        {
            _base = transform.localScale;
            var renderers = GetComponentsInChildren<Renderer>();
            var list = new System.Collections.Generic.List<Material>();
            foreach (var r in renderers)
                foreach (var m in r.materials)
                    if (m != null && m.HasProperty(BaseColorId)) list.Add(m);
            _materials = list.ToArray();
            _baseColors = new Color[_materials.Length];
            for (int i = 0; i < _materials.Length; i++) _baseColors[i] = _materials[i].GetColor(BaseColorId);
        }

        void Update()
        {
            _t += Time.deltaTime;
            float k = Mathf.Clamp01(_t / life);
            transform.localScale = _base * Mathf.Lerp(0.2f, maxScale, k);
            for (int i = 0; i < _materials.Length; i++)
            {
                var c = _baseColors[i];
                c.a = 1f - k;
                _materials[i].SetColor(BaseColorId, c);
            }
            if (_t >= life) Destroy(gameObject);
        }

        void OnDestroy()
        {
            foreach (var m in _materials) if (m != null) Destroy(m);
        }
    }
}
