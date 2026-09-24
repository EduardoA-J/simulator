using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace AvionesPapelVR
{
    [DefaultExecutionOrder(200)]
    public class GameHUD : MonoBehaviour
    {
        // Keep scene references compatible with existing builders.
        public bool preferVrCanvas = true;
        public Transform followCamera;
        public Vector3 panelOffset = new Vector3(0f, -0.15f, 1.2f);
        static readonly Color Ink = new Color(0.035f, 0.065f, 0.10f, 0.96f);
        static readonly Color Card = new Color(0.07f, 0.115f, 0.16f, 0.96f);
        static readonly Color Muted = new Color(0.57f, 0.69f, 0.76f);
        static readonly Color Cyan = new Color(0.30f, 0.89f, 0.87f);
        static readonly Color Gold = new Color(1f, 0.76f, 0.36f);
        Canvas _canvas;
        Font _font;
        RectTransform _menu, _flight;
        Text _eyebrow, _title, _description, _footer, _primaryLabel, _page;
        readonly Text[] _statValues = new Text[3], _statLabels = new Text[3];
        readonly Image[] _statBars = new Image[3];
        Text _score, _speed, _altitude, _route, _flightStatus, _flightHint, _feedbackText;
        Image _progress, _boost;
        RectTransform _steerDot;
        Button _primary, _previous, _next, _backToMaps;
        readonly GameObject[] _metricCards = new GameObject[3];
        Button[] _mapButtons = new Button[0];
        Image[] _mapCards = new Image[0], _mapBars = new Image[0];
        Text[] _mapCaptions = new Text[0], _mapNames = new Text[0], _mapRecords = new Text[0];
        const float MapCardWidth = 196f, MapCardStep = 209f;
        XRRayInteractor[] _rays;
        NearFarInteractor[] _nearFar;
        string _feedback;
        Color _feedbackColor;
        float _feedbackUntil, _nextRefresh;
        bool _victory;
        GameState _shownState = (GameState)(-1);
        public Canvas InterfaceCanvas => _canvas;
        public bool HasUiTarget
        {
            get
            {
                if (_rays != null)
                    foreach (var ray in _rays)
                        if (ray != null && ray.isActiveAndEnabled && ray.TryGetCurrentUIRaycastResult(out _)) return true;
                if (_nearFar != null)
                    foreach (var ray in _nearFar)
                        if (ray != null && ray.isActiveAndEnabled && ray.TryGetCurrentUIRaycastResult(out _)) return true;
                return false;
            }
        }

        public void SetVictory(bool value) => _victory = value;
        public void ShowFeedback(string message, Color color)
        {
            _feedback = message; _feedbackColor = color;
            _feedbackUntil = Time.unscaledTime + 1.5f;
            Refresh();
        }

        void Start()
        {
            Build();
            _rays = FindObjectsByType<XRRayInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            _nearFar = FindObjectsByType<NearFarInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Refresh();
        }

        RectTransform Rect(string name, Transform parent, float x, float y, float w, float h)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(w, h);
            return rect;
        }

        Image Panel(string name, Transform parent, float x, float y, float w, float h, Color color)
        {
            var image = Rect(name, parent, x, y, w, h).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        Text Label(string name, Transform parent, float x, float y, float w, float h, int size, Color color, string value = "")
        {
            var text = Rect(name, parent, x, y, w, h).gameObject.AddComponent<Text>();
            text.font = _font; text.fontSize = size; text.color = color;
            text.text = value; text.raycastTarget = false;
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        Image Bar(string name, Transform parent, float x, float y, float w, Color color)
        {
            var track = Panel(name + "Track", parent, x, y, w, 5, new Color(0.16f, 0.23f, 0.29f));
            return Panel(name, track.transform, 0, 0, w, 5, color);
        }

        static void Fill(Image image, float amount, float width)
        {
            image.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width * Mathf.Clamp01(amount));
        }

        Button Action(string name, Transform parent, float x, float y, float w, string caption, UnityEngine.Events.UnityAction action, out Text label)
        {
            var image = Panel(name, parent, x, y, w, 60, Cyan);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = new Color(0.8f, 1f, 1f);
            colors.pressedColor = new Color(0.55f, 0.8f, 0.85f);
            button.colors = colors;
            button.onClick.AddListener(action);
            label = Label(name + "Label", image.transform, 14, 0, w - 28, 60, 22, Ink, caption);
            label.alignment = TextAnchor.MiddleCenter;
            return button;
        }

        void Build()
        {
            if (_canvas != null) return;
            _font = Resources.Load<Font>("Interface/Inter-Regular") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var gm = GameManager.Instance;
            bool vr = gm != null && gm.vrMode;
            var go = new GameObject("FlightInterface", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = vr ? RenderMode.WorldSpace : RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 20;
            var root = (RectTransform)_canvas.transform;
            if (vr)
            {
                root.sizeDelta = new Vector2(1120, 660);
                root.localScale = Vector3.one * 0.0013f;
                go.AddComponent<TrackedDeviceGraphicRaycaster>();
                // The tracked raycaster only accepts XR pointers. The simulator also needs mouse clicks.
                go.AddComponent<GraphicRaycaster>();
            }
            else
            {
                var scaler = go.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1280, 800);
                scaler.matchWidthOrHeight = 0.5f;
                go.AddComponent<GraphicRaycaster>();
            }
            if (EventSystem.current == null)
                new GameObject("InterfaceEventSystem").AddComponent<EventSystem>();
            var events = EventSystem.current;
            foreach (var module in events.GetComponents<BaseInputModule>())
                module.enabled = vr ? module is XRUIInputModule : module is InputSystemUIInputModule;
            if (vr && events.GetComponent<XRUIInputModule>() == null) events.gameObject.AddComponent<XRUIInputModule>();
            if (!vr && events.GetComponent<InputSystemUIInputModule>() == null)
                events.gameObject.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();

            _menu = Rect("Menu", root, 0, 0, 1120, 660);
            _flight = Rect("Instruments", root, 0, 0, 1120, 660);
            foreach (var group in new[] { _menu, _flight })
            {
                group.anchorMin = group.anchorMax = new Vector2(0.5f, 0.5f);
                group.pivot = new Vector2(0.5f, 0.5f);
                group.anchoredPosition = Vector2.zero;
            }
            Panel("MenuSurface", _menu, 0, 0, 1120, 660, Ink);
            Panel("AccentRail", _menu, 0, 0, 5, 660, Cyan);
            Label("Brand", _menu, 44, 28, 700, 32, 22, Cyan, "PAPER FLIGHT  /  ESTUDIO DE VUELO");
            Label("Edition", _menu, 900, 32, 180, 25, 15, Muted, "ARCADE  •  VR");
            Panel("Divider", _menu, 44, 79, 1032, 1, new Color(0.18f, 0.27f, 0.33f));
            _eyebrow = Label("Eyebrow", _menu, 44, 100, 1032, 26, 18, Gold);
            _title = Label("Title", _menu, 42, 137, 1034, 74, 46, Color.white);
            _description = Label("Description", _menu, 44, 223, 1020, 78, 23, Muted);
            for (int i = 0; i < 3; i++)
            {
                var card = Panel("Metric" + i, _menu, 44 + i * 350, 321, 332, 144, Card);
                _metricCards[i] = card.gameObject;
                _statLabels[i] = Label("Caption", card.transform, 22, 17, 290, 26, 16, Muted);
                _statValues[i] = Label("Value", card.transform, 22, 48, 290, 59, 36, Color.white);
                _statBars[i] = Bar("Meter", card.transform, 22, 121, 288, i == 2 ? Gold : Cyan);
            }
            // Fila de mapas: una tarjeta por nivel configurado (sólo visible en el menú principal).
            int mapCount = gm != null ? Mathf.Max(1, gm.levels.Count) : 1;
            _mapButtons = new Button[mapCount]; _mapCards = new Image[mapCount]; _mapBars = new Image[mapCount];
            _mapCaptions = new Text[mapCount]; _mapNames = new Text[mapCount]; _mapRecords = new Text[mapCount];
            float step = mapCount <= 1 ? 0f : Mathf.Min(MapCardStep, (1032f - MapCardWidth) / (mapCount - 1));
            for (int i = 0; i < mapCount; i++)
            {
                var card = Panel("Map" + i, _menu, 44 + i * step, 321, MapCardWidth, 144, Card);
                _mapCards[i] = card;
                _mapCaptions[i] = Label("Caption", card.transform, 16, 14, MapCardWidth - 32, 24, 14, Muted);
                _mapNames[i] = Label("Name", card.transform, 16, 40, MapCardWidth - 32, 56, 24, Color.white);
                _mapRecords[i] = Label("Record", card.transform, 16, 96, MapCardWidth - 32, 22, 14, Gold);
                _mapBars[i] = Bar("Meter", card.transform, 16, 125, MapCardWidth - 32, Cyan);
                int mapIndex = i;
                var button = card.gameObject.AddComponent<Button>();
                button.targetGraphic = card;
                var colors = button.colors;
                colors.highlightedColor = new Color(0.11f, 0.19f, 0.26f);
                colors.pressedColor = new Color(0.16f, 0.30f, 0.38f);
                button.colors = colors;
                button.onClick.AddListener(() => SelectMap(mapIndex));
                _mapButtons[i] = button;
            }
            _primary = Action("Primary", _menu, 44, 494, 500, "EMPEZAR VUELO", PrimaryAction, out _primaryLabel);
            _previous = Action("Previous", _menu, 568, 494, 66, "<", PreviousAction, out _);
            _next = Action("Next", _menu, 1010, 494, 66, ">", () => BrowseSelection(1), out _);
            _backToMaps = Action("BackToMaps", _menu, 650, 494, 340, "VOLVER A MAPAS", () => GameManager.Instance?.GoToMainMenu(), out _);
            _page = Label("Page", _menu, 650, 511, 340, 35, 21, Color.white);
            _page.alignment = TextAnchor.MiddleCenter;
            _footer = Label("Footer", _menu, 44, 585, 1032, 49, 19, Muted);

            var scoreCard = Panel("ScoreCard", _flight, 0, 0, 242, 98, Ink);
            Label("ScoreCaption", scoreCard.transform, 20, 12, 210, 23, 15, Cyan, "PUNTUACIÓN");
            _score = Label("ScoreValue", scoreCard.transform, 18, 35, 220, 55, 38, Color.white);
            var routeCard = Panel("RouteCard", _flight, 760, 0, 360, 98, Ink);
            _route = Label("RouteCaption", routeCard.transform, 20, 17, 324, 32, 20, Color.white);
            _progress = Bar("RouteProgress", routeCard.transform, 20, 72, 320, Cyan);
            _feedbackText = Label("PickupFeedback", _flight, 295, 96, 530, 55, 32, Cyan);
            _feedbackText.alignment = TextAnchor.MiddleCenter;
            // Small fixed reference; never roll the whole UI with the aircraft.
            Panel("ReticleLeft", _flight, 544, 300, 10, 2, new Color(1, 1, 1, 0.55f));
            Panel("ReticleRight", _flight, 566, 300, 10, 2, new Color(1, 1, 1, 0.55f));
            var instruments = Panel("InstrumentPanel", _flight, 0, 488, 1120, 172, Ink);
            Panel("InstrumentAccent", instruments.transform, 0, 0, 1120, 2, Cyan);
            Label("SpeedCaption", instruments.transform, 24, 18, 160, 24, 16, Muted, "VELOCIDAD");
            _speed = Label("SpeedValue", instruments.transform, 24, 48, 190, 55, 34, Color.white);
            Label("AltitudeCaption", instruments.transform, 234, 18, 180, 24, 16, Muted, "ALTURA");
            _altitude = Label("AltitudeValue", instruments.transform, 234, 48, 170, 55, 34, Color.white);
            _flightStatus = Label("FlightStatus", instruments.transform, 445, 22, 485, 36, 22, Cyan);
            _boost = Bar("BoostTime", instruments.transform, 445, 75, 425, Gold);
            _flightHint = Label("ControlHint", instruments.transform, 24, 119, 1068, 36, 18, Muted);
            var control = Panel("SteeringPad", instruments.transform, 970, 15, 98, 88, Card);
            Panel("AxisX", control.transform, 8, 43, 82, 1, Muted);
            Panel("AxisY", control.transform, 49, 8, 1, 72, Muted);
            _steerDot = Panel("SteeringInput", control.transform, 44, 39, 10, 10, Cyan).rectTransform;
        }

        public void Refresh()
        {
            if (_canvas == null) return;
            var gm = GameManager.Instance;
            if (gm == null) return;
            bool flight = gm.State == GameState.Flight;
            bool mainMenu = gm.State == GameState.MainMenu;
            for (int i = 0; i < _mapButtons.Length; i++)
            {
                bool active = mainMenu && i < gm.levels.Count && gm.levels[i] != null;
                _mapCards[i].gameObject.SetActive(mainMenu);
                _mapButtons[i].enabled = active;
                _mapButtons[i].targetGraphic.raycastTarget = active;
            }
            foreach (var card in _metricCards) card.SetActive(!mainMenu);
            _menu.gameObject.SetActive(!flight); _flight.gameObject.SetActive(flight);
            if (_shownState != gm.State)
            {
                _feedbackUntil = 0f;
                _shownState = gm.State;
            }
            if (flight)
            {
                var pilot = gm.flightController;
                float altitude = gm.Player != null ? Mathf.Max(0, gm.CoursePoint(gm.Player.position).y) : 0;
                _score.text = gm.Score.ToString("0000");
                _speed.text = pilot.Speed.ToString("0.0") + " m/s";
                _altitude.text = altitude.ToString("0.0") + " m";
                float progress = gm.levelRunner != null ? gm.levelRunner.Progress01 : 0;
                _route.text = $"NIVEL {gm.CurrentLevelIndex + 1}/{gm.levels.Count}  /  {progress:P0}  /  {gm.RingsCollected} AROS";
                Fill(_progress, progress, 320);
                var turbo = gm.PowerUp(PowerUpType.Turbo);
                Fill(_boost, gm.IsTurboActive ? gm.TurboTimer / Mathf.Max(0.1f, turbo != null ? turbo.duration : 1f) : 0, 425);
                _flightStatus.text = !pilot.SteeringReady ? (pilot.SteeringTracked ? "SOSTÉN EL MANDO CÓMODO..." : "SIN SEGUIMIENTO · USA EL STICK") :
                    altitude < 0.65f ? "SUELO CERCA · LEVANTA EL MORRO" :
                    gm.IsTurboActive ? $"IMPULSO ACTIVO  {gm.TurboTimer:0.0}s" :
                    gm.CurrentLevel != null && gm.CurrentLevel.HasCurves ? gm.levelRunner.NavigationHint : "VUELO LIBRE  /  SIGUE LOS AROS";
                _flightStatus.color = altitude < 0.65f || gm.IsTurboActive ? Gold : Cyan;
                _flightHint.text = pilot.ControlHint;
                _feedbackText.text = Time.unscaledTime < _feedbackUntil ? _feedback : "";
                _feedbackText.color = _feedbackColor;
                _steerDot.anchoredPosition = new Vector2(44 + pilot.SteeringInput.y * 34, -39 + pilot.SteeringInput.x * 30);
                return;
            }
            bool selection = gm.State == GameState.PlaneSelect;
            bool results = gm.State == GameState.GameOver || gm.State == GameState.LevelComplete;
            _previous.gameObject.SetActive(selection || results); _next.gameObject.SetActive(selection);
            _backToMaps.gameObject.SetActive(results);
            _page.text = selection ? $"MESA  {gm.planeSelector.Page} / {gm.planeSelector.PageCount}" : "";
            _primary.interactable = true;
            if (selection || gm.State == GameState.Launch)
            {
                var plane = selection ? gm.planeSelector.Current : gm.SelectedPlane;
                bool available = gm.IsPlaneUnlocked(plane);
                _eyebrow.text = $"NIVEL {gm.CurrentLevelIndex + 1}/{gm.levels.Count} · {gm.CurrentLevel?.displayName} · {gm.CurrentLevel?.difficulty}";
                _title.text = plane != null ? plane.displayName : "Mesa de aviones";
                _description.text = !available ? $"Desbloquea este modelo con un récord de {plane?.unlockScore} puntos." :
                    gm.State == GameState.Launch ? "Lanza y relaja la mano. Después apunta el mando para dirigir el vuelo." :
                    gm.vrMode ? "Agarra un avión de la mesa con Grip. Muévelo hacia delante y suelta para lanzar." :
                    "Cada pliegue cambia el vuelo. Elige velocidad, planeo o estabilidad.";
                Metric(0, "VELOCIDAD", plane != null ? $"{plane.speed * 100:0} / 100" : "—", plane != null ? plane.speed : 0);
                Metric(1, "PLANEO", plane != null ? $"{plane.glide * 100:0} / 100" : "—", plane != null ? plane.glide : 0);
                Metric(2, "ESTABILIDAD", plane != null ? $"{plane.stability * 100:0} / 100" : "—", plane != null ? plane.stability : 0);
                _primaryLabel.text = gm.vrMode && selection ? "VOLVER A MAPAS" : !available ? "MODELO BLOQUEADO" : gm.vrMode ? "GRIP · AGARRA Y LANZA" :
                    selection ? "SELECCIONAR AVIÓN" : "ESPACIO · CARGA Y SUELTA";
                _primary.interactable = selection && (gm.vrMode || available);
                _footer.text = gm.vrMode ? "Stick izquierdo: mesa / Menú izquierdo: mapas / En vuelo: X modo, Y recalibrar" :
                    "Flechas: elegir   /   Enter: confirmar   /   W A S D: pilotar";
            }
            else if (gm.State == GameState.GameOver || gm.State == GameState.LevelComplete)
            {
                _eyebrow.text = "INFORME DE VUELO";
                _title.text = gm.CampaignComplete ? "Último reto superado." :
                    gm.State == GameState.LevelComplete ? $"Nivel {gm.CurrentLevelIndex + 1} superado." : _victory ? "Un vuelo para recordar." : "Cada vuelo cuenta.";
                _description.text = gm.EndReason + (gm.NewlyUnlocked.Count > 0 ? "  /  Nuevo: " + string.Join(", ", gm.NewlyUnlocked) : "  /  Vuelve al taller y mejora tu marca.");
                Metric(0, "PUNTOS", gm.Score.ToString("0000"), 1);
                Metric(1, "DISTANCIA · TIEMPO", $"{gm.RunDistance:0.0} m · {gm.LastRunTime:0.0} s", 1);
                Metric(2, "RÉCORD DEL NIVEL", GameManager.Progress.LevelBestScore(gm.CurrentLevelIndex).ToString("0000"), 1);
                _primaryLabel.text = gm.State == GameState.LevelComplete ? "SIGUIENTE ESCENARIO" : gm.CampaignComplete ? "VOLVER AL NIVEL 1" : "REINTENTAR ESTE NIVEL";
                _footer.text = gm.State == GameState.LevelComplete ? $"Siguiente: {gm.levels[gm.CurrentLevelIndex + 1].displayName} · Apunta y pulsa Trigger, o A / Enter" :
                    $"Aros: {gm.RingsCollected} / Impulsos: {gm.BoostsCollected} / Reintentar: apunta y pulsa Trigger, o A / Enter";
            }
            else
            {
                int reach = GameManager.Progress.CampaignReach;
                _eyebrow.text = reach > 0 ? $"DEL TALLER AL CIELO · CAMPAÑA {Mathf.Min(reach, gm.levels.Count)}/{gm.levels.Count}" : "DEL TALLER AL CIELO";
                _title.text = "Un pliegue. Mil caminos.";
                _description.text = $"{gm.levels.Count} retos: aula, parque, oficina, mini ciudad y mundo mágico.\nElige un avión, lánzalo y supera cada recorrido para avanzar.";
                for (int i = 0; i < _mapButtons.Length; i++)
                {
                    var level = i < gm.levels.Count ? gm.levels[i] : null;
                    bool done = GameManager.Progress.LevelCompleted(i);
                    int best = GameManager.Progress.LevelBestScore(i);
                    _mapCaptions[i].text = level != null ? $"MAPA {i + 1} · {level.difficulty}" : $"MAPA {i + 1}";
                    _mapNames[i].text = level != null ? level.displayName : "—";
                    _mapRecords[i].text = level == null ? "" : done ? $"SUPERADO · {best:0000}" : best > 0 ? $"RÉCORD {best:0000}" : "SIN VOLAR";
                    _mapRecords[i].color = done ? Cyan : Gold;
                    _mapBars[i].color = done ? Cyan : Gold;
                    Fill(_mapBars[i], (i + 1f) / Mathf.Max(1, _mapButtons.Length), MapCardWidth - 32);
                }
                _primaryLabel.text = "ENTRAR AL TALLER";
                _footer.text = $"Elige una tarjeta con el rayo y Trigger. Teclado: 1 a {gm.levels.Count}. Enter empieza en el mapa 1.";
            }
        }

        void Metric(int index, string caption, string value, float amount)
        {
            _statLabels[index].text = caption; _statValues[index].text = value;
            Fill(_statBars[index], amount, 288);
        }

        void PrimaryAction()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.LastMenuTransitionFrame == Time.frameCount) return;
            if (gm.State == GameState.MainMenu) gm.StartSelectedLevel(0);
            else if (gm.State == GameState.LevelComplete) gm.NextLevelOrFinish();
            else if (gm.State == GameState.GameOver) gm.RestartRun();
            else if (gm.State == GameState.PlaneSelect && !gm.vrMode) gm.planeSelector.ConfirmCurrent();
            else if (gm.State == GameState.PlaneSelect) gm.GoToMainMenu();
        }

        void SelectMap(int index)
        {
            var gm = GameManager.Instance;
            if (gm != null && gm.State == GameState.MainMenu && gm.LastMenuTransitionFrame != Time.frameCount)
                gm.StartSelectedLevel(index);
        }

        void PreviousAction()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;
            if (gm.State == GameState.PlaneSelect) BrowseSelection(-1);
            else if (gm.State == GameState.GameOver || gm.State == GameState.LevelComplete) gm.GoToMainMenu();
        }

        void BrowseSelection(int direction)
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.State != GameState.PlaneSelect) return;
            if (gm.vrMode) gm.planeSelector?.BrowsePage(direction);
            else gm.planeSelector?.Browse(direction);
        }

        void LateUpdate()
        {
            if (_canvas == null) return;
            var gm = GameManager.Instance;
            if (gm != null && _canvas.renderMode == RenderMode.WorldSpace)
            {
                var cam = followCamera != null ? followCamera : gm.gameCamera != null ? gm.gameCamera.transform : null;
                if (cam != null)
                {
                    bool selecting = gm.State == GameState.PlaneSelect || gm.State == GameState.Launch;
                    _canvas.transform.localScale = Vector3.one * (selecting ? 0.001f : 0.0013f);
                    _canvas.transform.SetPositionAndRotation(cam.TransformPoint(new Vector3(0, selecting ? 0.32f : 0, 1.8f)), cam.rotation);
                    _canvas.worldCamera = gm.gameCamera;
                }
            }
            if (Time.unscaledTime >= _nextRefresh)
            {
                _nextRefresh = Time.unscaledTime + 0.05f;
                Refresh();
            }
        }
    }
}
