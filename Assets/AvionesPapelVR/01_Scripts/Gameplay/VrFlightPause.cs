using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace AvionesPapelVR
{
    [DefaultExecutionOrder(-100)]
    public class VrFlightPause : MonoBehaviour
    {
        public bool IsPaused { get; private set; }
        GameManager _game;
        FlightController _pilot;
        float _previousTimeScale, _nextHardwareCheck;
        bool _previousAudioPause, _pilotWasEnabled;
        bool _applicationPaused, _applicationFocused = true, _displayFocused = true, _headPresent = true;
        bool _hadDisplay, _runtimeStopped, _lastA, _resumeArmed;
        readonly List<XRDisplaySubsystem> _displays = new();
        readonly HashSet<XRDisplaySubsystem> _subscribed = new();

        void Awake() => _game = GetComponent<GameManager>();

        void OnApplicationPause(bool paused)
        {
            _applicationPaused = paused;
            if (paused) RequestPause();
        }

        void OnApplicationFocus(bool focused)
        {
#if !UNITY_EDITOR
            _applicationFocused = focused;
            if (!focused) RequestPause();
#endif
        }

        void DisplayFocusChanged(bool focused)
        {
            _displayFocused = focused;
            if (!focused) RequestPause();
        }

        void PollHardware()
        {
            if (Application.isEditor && !XrSimulatorGuard.HardwareRunning) return;
            SubsystemManager.GetSubsystems(_displays);
            bool running = false;
            foreach (var display in _displays)
            {
                if (_subscribed.Add(display)) display.displayFocusChanged += DisplayFocusChanged;
                running |= display.running;
            }
            _runtimeStopped = _hadDisplay && !running;
            _hadDisplay |= running;
            var head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            if (head.TryGetFeatureValue(CommonUsages.userPresence, out bool present)) _headPresent = present;
            else if (running) _headPresent = true; // Some runtimes do not expose a presence sensor.
        }

        bool Blocked => _applicationPaused || !_applicationFocused || !_displayFocused || !_headPresent || _runtimeStopped;

        void Update()
        {
            if (_game == null || !_game.vrMode) return;
            if (Time.unscaledTime >= _nextHardwareCheck)
            { _nextHardwareCheck = Time.unscaledTime + 0.2f; PollHardware(); }
            if (Blocked) RequestPause();
            if (!IsPaused) return;
            if (_game.State != GameState.Flight) { CancelPause(); return; }
            bool pressed = VrInput.PrimaryButton(XRNode.RightHand);
            if (Blocked) _resumeArmed = false;
            else
            {
                if (pressed && !_lastA) _resumeArmed = true;
                // Resume on release so the same A press cannot also launch a missile.
                if (!pressed && _lastA && _resumeArmed)
                {
                    _pilot?.CalibrateSteering();
                    CancelPause();
                }
            }
            _lastA = pressed;
        }

        public void RequestPause()
        {
            if (IsPaused || _game == null || !_game.vrMode || _game.State != GameState.Flight) return;
            IsPaused = true;
            _previousTimeScale = Time.timeScale;
            _previousAudioPause = AudioListener.pause;
            _pilot = _game.flightController;
            _pilotWasEnabled = _pilot != null && _pilot.enabled;
            if (_pilot != null) _pilot.enabled = false;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            _lastA = VrInput.PrimaryButton(XRNode.RightHand);
            _resumeArmed = false;
            _game.hud?.ShowPause(true);
        }

        public void CancelPause()
        {
            if (!IsPaused) return;
            IsPaused = false;
            Time.timeScale = _previousTimeScale;
            AudioListener.pause = _previousAudioPause;
            if (_pilot != null) _pilot.enabled = _pilotWasEnabled;
            _game?.hud?.ShowPause(false);
            _resumeArmed = false;
        }

        void OnDisable()
        {
            CancelPause();
            foreach (var display in _subscribed) display.displayFocusChanged -= DisplayFocusChanged;
            _subscribed.Clear();
        }
    }
}
