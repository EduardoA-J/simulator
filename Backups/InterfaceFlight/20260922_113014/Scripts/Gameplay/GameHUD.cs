using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace AvionesPapelVR
{
    /// <summary>
    /// HUD visible en casco VR (Canvas World Space delante de la cÃ¡mara)
    /// y tambiÃ©n OnGUI para pruebas en editor.
    /// </summary>
    public class GameHUD : MonoBehaviour
    {
        public bool preferVrCanvas = true;
        public Transform followCamera;
        public Vector3 panelOffset = new Vector3(0f, -0.15f, 1.2f);

        bool _victoryFinal;
        Text _vrText;
        Canvas _canvas;
        Image _background;
        string _feedback;
        Color _feedbackColor;
        float _feedbackUntil;
        float _nextRefresh;

        public void ShowFeedback(string message, Color color)
        {
            _feedback = message;
            _feedbackColor = color;
            _feedbackUntil = Time.unscaledTime + 1.4f;
            Refresh();
        }

        public void SetVictory(bool value) => _victoryFinal = value;
        public void Refresh() => UpdateVrText();

        void Start()
        {
            if (preferVrCanvas)
                EnsureVrCanvas();
            if (GameManager.Instance != null && GameManager.Instance.vrMode && EventSystem.current != null)
            {
                var system = EventSystem.current;
                foreach (var module in system.GetComponents<BaseInputModule>())
                    if (!(module is XRUIInputModule)) module.enabled = false;
                if (system.GetComponent<XRUIInputModule>() == null) system.gameObject.AddComponent<XRUIInputModule>();
            }
        }

        void LateUpdate()
        {
            if (_canvas == null) return;
            var cam = followCamera != null ? followCamera : (Camera.main != null ? Camera.main.transform : null);
            if (cam == null) return;
            bool flying = GameManager.Instance != null && GameManager.Instance.State == GameState.Flight;
            Vector3 offset = flying ? new Vector3(0f, -0.65f, 1.5f) : new Vector3(0f, 0f, 1.5f);
            _canvas.transform.position = cam.TransformPoint(offset);
            _canvas.transform.rotation = Quaternion.LookRotation(cam.forward, Vector3.up);
            ((RectTransform)_canvas.transform).sizeDelta = new Vector2(1000, flying ? 260 : 550);
            if (_background != null) _background.color = new Color(0.025f, 0.06f, 0.09f, flying ? 0.65f : 0.9f);
            if (Time.unscaledTime >= _nextRefresh)
            {
                _nextRefresh = Time.unscaledTime + 0.1f;
                UpdateVrText();
            }
        }

        void EnsureVrCanvas()
        {
            if (_canvas != null) return;
            var go = new GameObject("VR_HUD_Canvas");
            go.transform.SetParent(transform, false);
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(1000, 550);
            go.transform.localScale = Vector3.one * 0.0012f;
            go.AddComponent<CanvasScaler>();
            go.AddComponent<TrackedDeviceGraphicRaycaster>();

            var bg = new GameObject("BG");
            bg.transform.SetParent(go.transform, false);
            var bgImg = bg.AddComponent<Image>();
            _background = bgImg;
            bgImg.color = new Color(0f, 0f, 0f, 0.4f);
            var bgRt = bg.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            _vrText = textGo.AddComponent<Text>();
            _vrText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _vrText.fontSize = 30;
            _vrText.supportRichText = true;
            _vrText.raycastTarget = false;
            bgImg.raycastTarget = false;
            _vrText.color = Color.white;
            _vrText.alignment = TextAnchor.UpperLeft;
            _vrText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _vrText.verticalOverflow = VerticalWrapMode.Overflow;
            var tRt = textGo.GetComponent<RectTransform>();
            tRt.anchorMin = Vector2.zero;
            tRt.anchorMax = Vector2.one;
            tRt.offsetMin = new Vector2(24, 24);
            tRt.offsetMax = new Vector2(-24, -24);
        }

        void UpdateVrText()
        {
            if (_vrText == null) return;
            var gm = GameManager.Instance;
            if (gm == null)
            {
                _vrText.text = "";
                return;
            }
            _vrText.text = BuildText(gm);
        }

        string BuildText(GameManager gm)
        {
            string tutorial = gm.Tutorial != null && !string.IsNullOrEmpty(gm.Tutorial.Instruction)
                ? "\n\nTUTORIAL\n" + gm.Tutorial.Instruction : "";
            switch (gm.State)
            {
                case GameState.MainMenu:
                    return "<color=#58DDEB><size=42>AVIONES DE PAPEL</size></color>\nTALLER DE VUELO  /  VR\n\nAgarra. Lanza. Encuentra tu ruta.\n<color=#58DDEB>Aros azules: +100</color>   <color=#FFC34A>Dorados: impulso</color>\n\nTrigger / A / Enter: empezar\nRecord: " + gm.BestScore + " puntos";
                case GameState.PlaneSelect:
                    var p = gm.planeSelector != null ? gm.planeSelector.Current : null;
                    if (p == null) return "No hay aviones configurados.";
                    string unlocked = gm.IsPlaneUnlocked(p) ? "DISPONIBLE" : "BLOQUEADO: record necesario " + p.unlockScore;
                    return $"MESA DE AVIONES  {gm.planeSelector.Page}/{gm.planeSelector.PageCount}\n" +
                        $"{p.displayName} | {unlocked}\n" +
                        $"Velocidad {p.speed:0.00}  Planeo {p.glide:0.00}  Estabilidad {p.stability:0.00}\n" +
                        "Stick izquierdo / flechas: consultar y cambiar pagina\n" +
                        (gm.vrMode ? "Grip: agarrar el avion disponible que quieras" : "Enter: elegir | Espacio: cargar y soltar") + tutorial;
                case GameState.Launch:
                    return "LANZAMIENTO: " + gm.SelectedPlane?.displayName + "\n\n" +
                        (gm.vrMode ? "Mueve la mano y suelta Grip.\nSalida asistida hacia los aros.\nSoltar sin impulso devuelve el avion a la mesa." :
                            $"Manten Espacio o clic para cargar ({Mathf.RoundToInt(gm.launchController.Charge01 * 100)}%). Suelta para lanzar.") + tutorial;
                case GameState.Flight:
                    float progress = gm.levelRunner != null ? gm.levelRunner.Progress01 : 0f;
                    float altitude = gm.Player != null ? Mathf.Max(0f, gm.CoursePoint(gm.Player.position).y) : 0f;
                    string status = Time.unscaledTime < _feedbackUntil
                        ? $"<color=#{ColorUtility.ToHtmlStringRGB(_feedbackColor)}>{_feedback}</color>"
                        : altitude < 0.65f ? "<color=#FFC34A>SUELO CERCA: nivela el avion</color>" : "AZUL +100   /   DORADO IMPULSO";
                    return $"<color=#58DDEB><size=38>{gm.Score:0000}</size> PUNTOS</color>    {gm.flightController.Speed:0.0} m/s    {altitude:0.0} m ALT\n" +
                        $"RUTA {progress:P0}    AROS {gm.RingsCollected}    IMPULSOS {gm.BoostsCollected}" +
                        (gm.IsTurboActive ? $"    TURBO {gm.TurboTimer:0.0}s" : "") +
                        "\n" + status + "\n<size=22>" + gm.flightController.ControlHint + "</size>";
                case GameState.LevelComplete:
                    return $"NIVEL COMPLETADO\nPuntos: {gm.Score}\nTrigger / A / Enter: siguiente";
                case GameState.GameOver:
                    string unlocks = gm.NewlyUnlocked.Count > 0 ? "\nDesbloqueados: " + string.Join(", ", gm.NewlyUnlocked) : "";
                    return $"{gm.EndReason}\nPuntos: {gm.Score} | Record: {gm.BestScore}\n" +
                        $"Distancia: {gm.RunDistance:0.0} m | Mejor: {gm.BestDistance:0.0} m\n" +
                        $"Aros: {gm.RingsCollected} | Boosts: {gm.BoostsCollected}" + unlocks +
                        "\n\nTrigger / A / Enter: reintentar";
                default: return "";
            }
        }

        void OnGUI()
        {
            // Solo editor / no VR: OnGUI de respaldo
            if (preferVrCanvas && (GameManager.Instance == null || GameManager.Instance.vrMode))
                return;

            var gm = GameManager.Instance;
            if (gm == null) return;
            var style = new GUIStyle(GUI.skin.box)
            {
                richText = true, alignment = TextAnchor.UpperLeft, fontSize = 18,
                padding = new RectOffset(18, 18, 14, 14), wordWrap = true
            };
            GUI.Box(new Rect(16, 12, Mathf.Min(760, Screen.width - 32), gm.State == GameState.Flight ? 155 : 330), BuildText(gm), style);
        }
    }
}
