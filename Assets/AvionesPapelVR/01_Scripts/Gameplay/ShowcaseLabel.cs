using UnityEngine;

namespace AvionesPapelVR
{
    /// <summary>
    /// Etiqueta visual flotante sobre un asset en la escena Showcase.
    /// </summary>
    public class ShowcaseLabel : MonoBehaviour
    {
        public string label = "Asset";

        void OnDrawGizmos()
        {
#if UNITY_EDITOR
            UnityEditor.Handles.Label(transform.position + Vector3.up * 0.35f, label);
#endif
        }
    }
}
