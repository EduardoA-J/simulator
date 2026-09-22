#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace AvionesPapelVR.Editor
{
    // Opt-in integration checks: menu or a local Temp request. Never run on import alone.
    [InitializeOnLoad]
    public static class GameplayValidation
    {
        public const string ResultPath = "Temp/AvionesValidation.json";
        const string RequestPath = "Temp/AvionesValidation.request";
        const string Running = "AvionesPapelVR.Validation.Running";
        const string Previous = "AvionesPapelVR.Validation.Previous";

        static GameplayValidation()
        {
            EditorApplication.update += Poll;
            EditorApplication.playModeStateChanged += ModeChanged;
        }

        static double _idleSince = EditorApplication.timeSinceStartup;
        static void Poll()
        {
            if (SessionState.GetBool(Running, false) && EditorApplication.isPlaying && EditorApplication.isPaused)
                EditorApplication.isPaused = false;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isPlaying)
            { _idleSince = EditorApplication.timeSinceStartup; return; }
            if (!File.Exists(RequestPath) || EditorApplication.timeSinceStartup - _idleSince < 2d) return;
            File.Delete(RequestPath);
            try { Run(); }
            catch (Exception e) { File.WriteAllText(ResultPath, JsonUtility.ToJson(new StartupFailure { failure = e.Message })); }
        }

        public static void RunBatch()
        {
            SessionState.SetBool("AvionesPapelVR.Validation.Batch", true);
            KeyboardPlaySetup.DisableXrOnStartupForEditorTesting();
            File.WriteAllText(RequestPath, "batch");
        }

        [MenuItem("Aviones de Papel VR/Validar circuito (Play Mode)", priority = 50)]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                {
                    File.WriteAllText(ResultPath, "{\"passed\":false,\"reason\":\"Hay escenas sin guardar; no se alteraron.\"}");
                    return;
                }
            SessionState.SetString(Previous, JsonUtility.ToJson(new SceneSet { scenes = EditorSceneManager.GetSceneManagerSetup() }));
            SessionState.SetBool(Running, true);
            SessionState.SetBool(XrSimulatorGuard.ValidationSimulation, true);
            EditorSceneManager.OpenScene("Assets/AvionesPapelVR/Scenes/Game_VR_Oculus.unity");
            EditorApplication.isPlaying = true;
        }

        static void ModeChanged(PlayModeStateChange mode)
        {
            if (!SessionState.GetBool(Running, false)) return;
            if (mode == PlayModeStateChange.EnteredPlayMode)
                new GameObject("GameplayValidation_Transient").AddComponent<GameplayValidationProbe>();
            if (mode == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(Running, false);
                SessionState.SetBool(XrSimulatorGuard.ValidationSimulation, false);
                var previous = JsonUtility.FromJson<SceneSet>(SessionState.GetString(Previous, "{}"));
                if (previous?.scenes != null && previous.scenes.Any(s => s.isLoaded && s.isActive))
                    EditorSceneManager.RestoreSceneManagerSetup(previous.scenes);
                if (SessionState.GetBool("AvionesPapelVR.Validation.Batch", false))
                {
                    SessionState.SetBool("AvionesPapelVR.Validation.Batch", false);
                    EditorApplication.Exit(File.Exists(ResultPath) && File.ReadAllText(ResultPath).Contains("\"passed\": true") ? 0 : 1);
                }
            }
        }

        [Serializable] class StartupFailure { public bool passed; public string failure; }
        [Serializable] class SceneSet { public SceneSetup[] scenes; }
    }

    public class GameplayValidationProbe : MonoBehaviour
    {
        [Serializable] class Report { public bool passed; public string failure; public List<string> checks = new(); public List<string> errors = new(); }
        readonly Report _report = new();
        void OnEnable() => Application.logMessageReceived += OnLog;
        void OnDisable() => Application.logMessageReceived -= OnLog;
        void OnLog(string message, string trace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) _report.errors.Add(message);
        }
        // Headless Editor has no focused GameView; force the player input buffer in this test only.
        static void PlayerInputUpdate() => typeof(InputSystem).GetMethod("Update",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
            null, new[] { typeof(InputUpdateType) }, null).Invoke(null, new object[] { InputUpdateType.Dynamic });

        static void SetInput<T>(InputControl<T> control, T value) where T : struct
        {
            control.QueueValueChange(value);
            PlayerInputUpdate();
        }

        void Check(bool condition, string description)
        {
            if (!condition) throw new Exception(description);
            _report.checks.Add(description);
        }
        IEnumerator Start()
        {
            Application.runInBackground = true;
            // Stable sampling for XRI's throw history, independent of headless rendering stalls.
            float previousCapture = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 60f;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            var run = Checks();
            while (true)
            {
                object next = null;
                bool more = false;
                try { more = run.MoveNext(); if (more) next = run.Current; }
                catch (Exception e) { _report.failure = e.ToString(); break; }
                if (!more) { _report.passed = _report.errors.Count == 0; break; }
                yield return next;
            }
            string report = JsonUtility.ToJson(_report, true);
            File.WriteAllText(GameplayValidation.ResultPath, report);
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/AvionesValidation.json", report);
            Debug.Log("[AvionesValidation] " + (_report.passed ? "PASS" : "FAIL") + " " + _report.checks.Count + " checks");
            Time.captureDeltaTime = previousCapture;
            EditorApplication.isPlaying = false;
        }

        IEnumerator Checks()
        {
            yield return null;
            yield return null;
            var gm = GameManager.Instance;
            if (gm != null) gm.persistProgress = false;
            Check(gm != null && gm.State == GameState.MainMenu, "Main menu initialized");
            Check(gm.hud.InterfaceCanvas != null, "Structured interface canvas initialized");
            CaptureInterface(gm, "Interface_Menu");
            var simulator = XRInteractionSimulator.instance;
            Check(simulator != null && simulator.isActiveAndEnabled, "XRI simulator instantiated and active");
            // Disable the whole simulator and own the test controller lifecycle.
            simulator.gameObject.SetActive(false);
            var right = InputSystem.AddDevice<XRSimulatedController>();
            InputSystem.SetDeviceUsage(right, UnityEngine.InputSystem.CommonUsages.RightHand);
            Check(right.added, "Deterministic simulated right controller exists");
            VrInput.Trigger(XRNode.RightHand);
            PlayerInputUpdate(); // Resolve bindings/initial state before state injection.
            SetInput(right.trigger, 1f);
            var actions = (Dictionary<(XRNode, string), InputAction>)typeof(VrInput)
                .GetField("Actions", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).GetValue(null);
            var triggerAction = actions[(XRNode.RightHand, "trigger")];
            Check(VrInput.Trigger(XRNode.RightHand), "Gameplay reads simulated trigger through Input System; raw=" + right.trigger.ReadValue() +
                "; enabled=" + right.enabled + "; phase=" + triggerAction.phase + "; controls=" + string.Join(",", triggerAction.controls.Select(c => c.path)));
            SetInput(right.trigger, 0f);
            Vector3 lobby = gm.xrOrigin.position;
            gm.StartPlaneSelect();
            yield return null;
            gm.hud.SendMessage("LateUpdate");
            CaptureInterface(gm, "Interface_Selection");
            var allPlanes = UnityEngine.Object.FindObjectsByType<VrGrabPlane>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Check(allPlanes.Length == gm.planes.Count(d => gm.IsPlaneUnlocked(d)), "Only unlocked planes receive XR grab components");
            var planes = allPlanes.Where(p => p.gameObject.activeInHierarchy).ToArray();
            Check(planes.Length > 0 && planes.Length <= 4, "Reachable table page contains at most four planes");
            foreach (var plane in planes)
            {
                Check(plane.GetComponent<Rigidbody>() != null, "Rigidbody survives selection: " + plane.definition.name);
                foreach (var mc in plane.GetComponentsInChildren<MeshCollider>()) Check(mc.convex, "Dynamic mesh convex: " + plane.definition.name);
            }
            var selected = planes[0];
            var grab = selected.GetComponent<XRGrabInteractable>();
            var hand = new GameObject("ValidationDirectInteractor");
            hand.transform.position = selected.transform.position;
            var sphere = hand.AddComponent<SphereCollider>(); sphere.isTrigger = true; sphere.radius = 0.1f;
            var body = hand.AddComponent<Rigidbody>(); body.isKinematic = true;
            var interactor = hand.AddComponent<XRDirectInteractor>();
            yield return null;
            interactor.StartManualInteraction((IXRSelectInteractable)grab);
            yield return null;
            Check(grab.isSelected && gm.State == GameState.Launch, "XRI manual select enters grab state");
            Check(gm.planeSelector.Current == selected.definition, "Displayed definition matches grabbed plane");
            Check(gm.xrOrigin.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Locomotion.LocomotionProvider>(true)
                .All(provider => !provider.enabled), "Selection and held contexts disable locomotion");
            interactor.interactionManager.SelectCancel((IXRSelectInteractor)interactor, (IXRSelectInteractable)grab);
            // Retire the canceled synthetic hand: EndManualInteraction would send a second exit.
            interactor.enabled = false;
            Destroy(interactor);
            yield return null; yield return null; yield return null;
            Check(gm.State == GameState.PlaneSelect, "Canceled XR grab recovers selection without flight");
            Check(planes.All(p => p.GetComponent<XRGrabInteractable>().enabled), "Canceled grab re-enables other planes");
            interactor = hand.AddComponent<XRDirectInteractor>();
            yield return null;
            hand.transform.position = selected.transform.position;
            interactor.StartManualInteraction((IXRSelectInteractable)grab);
            yield return null;
            // Stationary release must return to the table, never launch via charge.
            yield return new WaitForSeconds(0.6f);
            interactor.EndManualInteraction();
            yield return null; yield return null; yield return null;
            Check(gm.State == GameState.PlaneSelect, "Stationary release returns to selection (actual: " + gm.State + ")");
            hand.transform.position = selected.transform.position;
            interactor.StartManualInteraction((IXRSelectInteractable)grab);
            yield return null;
            float start = Time.time;
            while (Time.time - start < 0.35f)
            {
                hand.transform.position += new Vector3(2f, 0.3f, 6f) * Time.deltaTime;
                yield return null;
            }
            interactor.EndManualInteraction();
            yield return null; yield return null; yield return null;
            Check(gm.State == GameState.Flight, "Moving release starts flight after XRI detach (actual: " + gm.State + ")");
            var player = GameObject.FindGameObjectWithTag("Player");
            Check(player != null && player.GetComponent<Rigidbody>().linearVelocity.magnitude > 1f, "Flight retains nonzero throw velocity");
            Check(player.GetComponent<Rigidbody>().linearVelocity.x > 0.1f, "Throw lateral direction preserved");
            Check(gm.CoursePoint(player.transform.position).z >= 0f && gm.CoursePoint(player.transform.position).z < 2f,
                "VR release starts at the course entrance, independent of the table");
            Check(UnityEngine.Object.FindObjectsByType<CollectiblePickup>(FindObjectsSortMode.None)
                .Where(p => p.IsRing).All(p => gm.CoursePoint(p.transform.position).z > 3f), "All rings spawn ahead of the entrance");
            Check(player.transform.Find("PilotSeat") != null, "Explicit PilotSeat created");
            // Coroutines resume before LateUpdate; sample the rig after its actual follow callback.
            gm.vrRigFollower.SendMessage("LateUpdate");
            Vector3 seat = player.transform.Find("PilotSeat").position;
            Check(Vector3.Distance(gm.gameCamera.transform.position, seat) < 0.15f, "HMD initial height compensated at seat");
            var rb = player.GetComponent<Rigidbody>();
            float beforeBoost = rb.linearVelocity.magnitude;
            gm.flightController.ApplyBoost();
            Check(rb.linearVelocity.magnitude > beforeBoost && rb.linearVelocity.magnitude <= gm.SelectedPlane.EffectiveMaxSpeed + 0.01f, "Boost increases speed within plane limit");
            Check(gm.weapons.Count == 4 && gm.powerUps.Count == 3, "Weapon and power-up definitions wired into scene");
            gm.ActivatePowerUp(PowerUpType.Turbo);
            Check(Mathf.Approximately(gm.TurboTimer, gm.PowerUp(PowerUpType.Turbo).duration), "Runtime turbo uses ScriptableObject duration");
            Check(gm.vrRigFollower.followRotation, "Cockpit follows aircraft heading by default");
            gm.vrRigFollower.Recenter();
            Check(Vector3.Distance(gm.gameCamera.transform.position, player.transform.Find("PilotSeat").position) < 0.01f,
                "Seated recenter places HMD at pilot seat");
            SetInput(right.isTracked, 1f);
            SetInput(right.deviceRotation, Quaternion.identity);
            gm.flightController.steering = FlightController.VrSteering.ControllerTilt;
            gm.flightController.CalibrateSteering();
            SetInput(right.deviceRotation, Quaternion.Euler(-15f, 0f, -15f));
            yield return null;
            Check(gm.flightController.SteeringInput.x > 0.2f && gm.flightController.SteeringInput.y > 0.2f,
                "Tracked controller tilt drives pitch and roll");
            SetInput(right.deviceRotation, Quaternion.identity);
            gm.flightController.CalibrateSteering();
            yield return null;
            Check(gm.flightController.SteeringInput.magnitude < 0.01f, "Neutral calibration removes tilt input");
            var shooter = player.GetComponent<CombatShooter>();
            var previousProjectiles = UnityEngine.Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None);
            shooter.FireMissile();
            var created = UnityEngine.Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None)
                .Except(previousProjectiles).ToArray();
            var missileDefinition = gm.Weapon(WeaponType.FoldMissile);
            Check(created.Length == 1 && Mathf.Approximately(created[0].speed, missileDefinition.speed) &&
                Mathf.Approximately(created[0].damage, missileDefinition.damage * Mathf.Lerp(0.7f, 1.6f, gm.SelectedPlane.firePower)),
                "Missile speed and damage use asset and plane modifier");
            shooter.FireMissile();
            Check(UnityEngine.Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None).Length == previousProjectiles.Length + 1,
                "Missile cooldown prevents immediate second shot");
            foreach (var projectile in created) Destroy(projectile.gameObject);
            gm.flightController.enabled = false;
            rb.isKinematic = true;
            var ring = UnityEngine.Object.FindObjectsByType<CollectiblePickup>(FindObjectsSortMode.None)
                .Where(p => p.kind == CollectiblePickup.Kind.Ring).OrderBy(p => p.transform.position.z).First();
            int score = gm.Score;
            rb.position = ring.transform.position - Vector3.forward;
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            rb.position = ring.transform.position + Vector3.forward;
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            Check(gm.Score == score + 100 && gm.RingsCollected == 1, "Normal ring awards exactly 100 on crossing");
            yield return new WaitForFixedUpdate();
            Check(gm.Score == score + 100, "Ring cannot award twice");
            rb.position = gm.CourseOrigin + gm.CourseRotation * new Vector3(0f, 2f, gm.CurrentLevel.length - 1f);
            yield return null;
            rb.position = gm.CourseOrigin + gm.CourseRotation * new Vector3(0f, 2f, gm.CurrentLevel.length + 1f);
            yield return null;
            gm.flightController.enabled = true;
            Check(gm.State == GameState.GameOver, "Crossing the open finish gate completes the run");
            Check(gm.State == GameState.GameOver && rb.isKinematic, "Landing finalizes and freezes player body");
            Check(gm.Score >= score + 200 && gm.BestScore >= gm.Score, "Results include landing bonus and best score");
            gm.hud.SendMessage("LateUpdate");
            CaptureInterface(gm, "Interface_Results");
            gm.vrRigFollower.StopFollowAndReturnLobby();
            Check(Vector3.Distance(lobby, gm.xrOrigin.position) < 0.01f, "Rig restores captured lobby pose");
            gm.RestartRun();
            yield return null;
            Check(gm.State == GameState.PlaneSelect && gm.Score == 0, "Restart returns to physical selection");
            gm.flightStartAnchor.rotation = Quaternion.Euler(0f, 90f, 0f);
            var backwards = gm.CourseRotation * new Vector3(2f, -3f, -8f);
            gm.BeginFlightFromVrThrow(gm.planes.First(d => gm.IsPlaneUnlocked(d)), backwards, new Vector3(-20f, 1f, -20f));
            Check(Vector3.Distance(gm.Player.position, gm.FlightStart) < 0.01f, "Backward throw ignores out-of-course hand position");
            var localVelocity = Quaternion.Inverse(gm.CourseRotation) * gm.Player.GetComponent<Rigidbody>().linearVelocity;
            Check(localVelocity.z > 0f && localVelocity.x > 0f && Mathf.Abs(localVelocity.y) < localVelocity.z * 0.4f,
                "Backward throw corrected toward rotated course, retaining lateral aim and limiting dive");
            Check(UnityEngine.Object.FindObjectsByType<CollectiblePickup>(FindObjectsSortMode.None)
                .Where(p => p.IsRing && p.gameObject.activeInHierarchy).All(p => gm.CoursePoint(p.transform.position).z > 3f),
                "Rotated course keeps every ring ahead of the pilot");
            CaptureCourse(gm);
            // Exercise the real input-to-physics path above the obstacle field.
            rb = gm.Player.GetComponent<Rigidbody>();
            rb.position = gm.FlightStart + Vector3.up * 8f;
            SetInput(right.deviceRotation, Quaternion.identity);
            gm.flightController.CalibrateSteering();
            float initialYaw = rb.rotation.eulerAngles.y;
            SetInput(right.deviceRotation, Quaternion.Euler(-12f, 20f, 0f));
            for (int i = 0; i < 24; i++) yield return null;
            Check(gm.flightController.SteeringInput.y > 0.4f, "Pointing the controller right steers without requiring wrist roll");
            Check(Mathf.DeltaAngle(initialYaw, rb.rotation.eulerAngles.y) > 3f, "Controller pointing changes aircraft heading right");
            Check(Vector3.Dot(rb.linearVelocity.normalized, rb.rotation * Vector3.forward) > 0.97f,
                "Velocity follows the aircraft nose during a turn");
            gm.vrRigFollower.SendMessage("LateUpdate");
            Check(Vector3.Angle(Vector3.ProjectOnPlane(gm.gameCamera.transform.forward, Vector3.up),
                Vector3.ProjectOnPlane(gm.Player.forward, Vector3.up)) < 12f, "Cockpit view follows the actual flight direction");
            Check(Vector3.Dot(gm.xrOrigin.up, Vector3.up) > 0.999f, "Cockpit horizon stays level during bank and pitch");
            float rightYaw = rb.rotation.eulerAngles.y;
            SetInput(right.deviceRotation, Quaternion.Euler(0f, -20f, 0f));
            for (int i = 0; i < 24; i++) yield return null;
            Check(Mathf.DeltaAngle(rightYaw, rb.rotation.eulerAngles.y) < -3f, "Pointing left reverses the turn");
            SetInput(right.deviceRotation, Quaternion.Euler(-15f, 0f, 0f));
            for (int i = 0; i < 40; i++) yield return null;
            float heldPitch = Mathf.DeltaAngle(0, rb.rotation.eulerAngles.x);
            for (int i = 0; i < 30; i++) yield return null;
            Check(Mathf.Abs(Mathf.DeltaAngle(heldPitch, rb.rotation.eulerAngles.x)) < 2f && heldPitch > -28f,
                "Holding pitch settles at a bounded angle instead of accumulating");
            SetInput(right.deviceRotation, Quaternion.identity);
            for (int i = 0; i < 35; i++) yield return null;
            Check(Mathf.Abs(Mathf.DeltaAngle(2f, rb.rotation.eulerAngles.x)) < 3f,
                "Neutral controller returns aircraft to a shallow glide");
            SetInput(right.primary2DAxis, new Vector2(0.8f, 0f));
            yield return null;
            Check(gm.flightController.SteeringInput.y > 0.7f, "Thumbstick remains available in controller-pointing mode");
            SetInput(right.primary2DAxis, Vector2.zero);
            SetInput(right.isTracked, 0f);
            yield return null;
            Check(gm.flightController.SteeringInput.magnitude < 0.01f, "Tracking loss clears stale steering input");
            gm.FinishRun(false);
            gm.RestartRun();
            yield return null;
            SetInput(right.isTracked, 1f);
            SetInput(right.deviceRotation, Quaternion.identity);
            gm.BeginFlightFromVrThrow(gm.planes.First(d => gm.IsPlaneUnlocked(d)), gm.CourseRotation * Vector3.forward * 8f);
            for (int i = 0; i < 35; i++) yield return null;
            gm.flightController.CalibrateSteering();
            gm.hud.Refresh();
            gm.hud.SendMessage("LateUpdate");
            CaptureInterface(gm, "Interface_Flight");
            gm.FinishRun(false);
            Destroy(hand);
            InputSystem.RemoveDevice(right);
        }

        void CaptureInterface(GameManager gm, string filename)
        {
            Canvas.ForceUpdateCanvases();
            foreach (var text in gm.hud.InterfaceCanvas.GetComponentsInChildren<UnityEngine.UI.Text>())
                Check(text.preferredHeight <= text.rectTransform.rect.height + 2f,
                    filename + ": text fits " + text.gameObject.name);
            var go = new GameObject("InterfacePreviewCamera");
            var camera = go.AddComponent<Camera>();
            camera.CopyFrom(gm.gameCamera);
            camera.stereoTargetEye = StereoTargetEyeMask.None;
            camera.enabled = false;
            camera.fieldOfView = 52f;
            go.transform.SetPositionAndRotation(gm.gameCamera.transform.position, gm.gameCamera.transform.rotation);
            var target = new RenderTexture(1600, 1000, 24);
            camera.targetTexture = target;
            var previous = RenderTexture.active;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            var texture = new Texture2D(1600, 1000, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, 1600, 1000), 0, 0); texture.Apply();
            Directory.CreateDirectory("Logs");
            File.WriteAllBytes("Logs/" + filename + ".png", texture.EncodeToPNG());
            RenderTexture.active = previous;
            camera.targetTexture = null; target.Release();
            Destroy(target); Destroy(texture); Destroy(go);
        }

        static void CaptureCourse(GameManager gm)
        {
            var go = new GameObject("ValidationPreviewCamera");
            var camera = go.AddComponent<Camera>();
            camera.CopyFrom(gm.gameCamera);
            camera.stereoTargetEye = StereoTargetEyeMask.None;
            camera.enabled = false;
            camera.fieldOfView = 72f;
            camera.nearClipPlane = 0.03f;
            go.transform.SetPositionAndRotation(gm.FlightStart + Vector3.up * 0.4f, gm.CourseRotation);
            var target = new RenderTexture(1280, 720, 24);
            camera.targetTexture = target;
            var previous = RenderTexture.active;
            camera.Render();
            RenderTexture.active = target;
            var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            texture.Apply();
            Directory.CreateDirectory("Logs");
            File.WriteAllBytes("Logs/CoursePreview.png", texture.EncodeToPNG());
            RenderTexture.active = previous;
            camera.targetTexture = null;
            target.Release();
            Destroy(target); Destroy(texture); Destroy(go);
        }
    }
}
#endif

