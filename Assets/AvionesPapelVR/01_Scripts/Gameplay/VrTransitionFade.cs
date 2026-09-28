using UnityEngine;
using UnityEngine.UI;
namespace AvionesPapelVR
{
    // Short blink masks the seat/lobby pose discontinuity, using unscaled time.
    public class VrTransitionFade : MonoBehaviour
    {
        Image _image;
        float _remaining;
        const float Duration = 0.18f;
        public void Blink(Camera camera)
        {
            if (camera == null) return;
            if (_image == null)
            {
                var canvasObject = new GameObject("VR Transition", typeof(RectTransform), typeof(Canvas));
                canvasObject.transform.SetParent(camera.transform, false);
                var canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = camera.nearClipPlane + 0.01f;
                canvas.sortingOrder = short.MaxValue;
                var cover = new GameObject("Fade", typeof(RectTransform), typeof(Image));
                cover.transform.SetParent(canvasObject.transform, false);
                _image = cover.GetComponent<Image>();
                _image.raycastTarget = false;
                var rect = cover.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
            }
            _remaining = Duration;
            _image.color = Color.black;
            _image.enabled = true;
        }
        void LateUpdate()
        {
            if (_image == null || !_image.enabled) return;
            _remaining = Mathf.Max(0f, _remaining - Time.unscaledDeltaTime);
            _image.color = new Color(0f, 0f, 0f, _remaining / Duration);
            if (_remaining <= 0f) _image.enabled = false;
        }
        void OnDestroy() { if (_image != null) Destroy(_image.transform.parent.gameObject); }
    }
}
