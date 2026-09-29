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
        const string PreviousXrStartup = "AvionesPapelVR.Validation.PreviousXrStartup";

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
            // Call Run directly; batchmode exits after -executeMethod unless Play Mode is entered here.
            if (File.Exists(RequestPath)) File.Delete(RequestPath);
            Run();
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
            var xr = UnityEditor.XR.Management.XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);
            SessionState.SetBool(PreviousXrStartup, xr != null && xr.InitManagerOnStart);
            // Temporary in-memory override: an interrupted test must not disable Link on disk.
            if (xr != null) xr.InitManagerOnStart = false;
            SessionState.SetBool(Running, true);
            SessionState.SetBool(XrSimulatorGuard.ValidationSimulation, true);
            EditorSceneManager.OpenScene("Assets/AvionesPapelVR/00_Scenes/Game_VR_Oculus.unity");
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
                var xr = UnityEditor.XR.Management.XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);
                if (xr != null) xr.InitManagerOnStart = SessionState.GetBool(PreviousXrStartup, true);
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
        const int ExpectedLevels = 5;
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
            DontDestroyOnLoad(gameObject); // The probe must survive loading Game_Playable.
            Application.runInBackground = true;
            // Stable sampling for XRI's throw history, independent of headless rendering stalls.
            float previousCapture = Time.captureDeltaTime;
            bool previousAsyncShaders = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            Time.captureDeltaTime = 1f / 60f;
            var previousBackgroundBehavior = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            var runs = new Stack<IEnumerator>();
            runs.Push(Checks());
            while (runs.Count > 0)
            {
                object next = null;
                bool more = false;
                try { more = runs.Peek().MoveNext(); if (more) next = runs.Peek().Current; }
                catch (Exception e) { _report.failure = e.ToString(); break; }
                if (!more)
                {
                    runs.Pop();
                    if (runs.Count == 0) _report.passed = _report.errors.Count == 0;
                    continue;
                }
                if (next is IEnumerator nested) { runs.Push(nested); continue; }
                yield return next;
            }
            string report = JsonUtility.ToJson(_report, true);
            File.WriteAllText(GameplayValidation.ResultPath, report);
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/AvionesValidation.json", report);
            Debug.Log("[AvionesValidation] " + (_report.passed ? "PASS" : "FAIL") + " " + _report.checks.Count + " checks");
            Time.captureDeltaTime = previousCapture;
            ShaderUtil.allowAsyncCompilation = previousAsyncShaders;
            InputSystem.settings.backgroundBehavior = previousBackgroundBehavior;
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
            Check(gm.levels.Count == ExpectedLevels && gm.levels.All(l => l != null), "Exactly five referenced map assets");
            var androidXr = UnityEditor.XR.Management.XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Android);
            Check(androidXr != null && androidXr.InitManagerOnStart && androidXr.Manager.activeLoaders
                .Any(l => l is UnityEngine.XR.OpenXR.OpenXRLoader), "Quest Android starts the existing OpenXR loader");
            var androidOpenXr = UnityEngine.XR.OpenXR.OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            Check(androidOpenXr != null && androidOpenXr.GetFeature<UnityEngine.XR.OpenXR.Features.MetaQuestSupport.MetaQuestFeature>().enabled &&
                androidOpenXr.GetFeature<UnityEngine.XR.OpenXR.Features.Interactions.OculusTouchControllerProfile>().enabled,
                "Quest support and Oculus Touch bindings are enabled for Android");
            Check((PlayerSettings.Android.targetArchitectures & AndroidArchitecture.ARM64) != 0 &&
                PlayerSettings.GetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android) == ScriptingImplementation.IL2CPP,
                "Quest player targets ARM64 with IL2CPP");
            Check(EditorBuildSettings.scenes.Any(s => s.enabled && s.path.EndsWith("/Game_VR_Oculus.unity")) &&
                EditorBuildSettings.scenes.Any(s => s.enabled && s.path.EndsWith("/Game_Playable.unity")),
                "VR and keyboard entry scenes are enabled in Build Settings");
            for (int i = 0; i < gm.levels.Count; i++)
            {
                yield return null;
                Click(gm, "Map" + i);
                Check(gm.State == GameState.PlaneSelect && gm.CurrentLevelIndex == i,
                    "Existing map card " + (i + 1) + " selects its own LevelDefinition");
                Check(UnityEngine.Object.FindObjectsByType<GameManager>(FindObjectsSortMode.None).Length == 1,
                    "Map selection keeps one GameManager and the existing XR rig");
                yield return VerifyTablePages(gm);
                yield return null;
                Click(gm, "Primary");
                Check(gm.State == GameState.MainMenu, "Table button returns to map menu");
            }
            CaptureInterface(gm, "Interface_Menu");
            var simulator = XRInteractionSimulator.instance;
            Check(simulator != null && simulator.isActiveAndEnabled, "XRI simulator instantiated and active");
            // Disable the whole simulator and own the test controller lifecycle.
            // Leftover simulated devices keep RightHand usage and steal pose/UI bindings from the test controller.
            simulator.gameObject.SetActive(false);
            foreach (var leftover in InputSystem.devices.OfType<XRSimulatedController>().ToArray())
                InputSystem.RemoveDevice(leftover);
            var right = InputSystem.AddDevice<XRSimulatedController>();
            InputSystem.SetDeviceUsage(right, UnityEngine.InputSystem.CommonUsages.RightHand);
            Check(right.added, "Deterministic simulated right controller exists");
            Check(InputSystem.devices.OfType<XRSimulatedController>().Count() == 1,
                "Validation owns a single XRSimulatedController for pose and UI Press");
            // GameManager already opened VrInput actions against the simulator devices; rebind after the swap.
            typeof(VrInput).GetMethod("Reset", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                .Invoke(null, null);
            VrInput.Trigger(XRNode.RightHand);
            PlayerInputUpdate(); // Resolve bindings/initial state before state injection.
            SetInput(right.trigger, 1f);
            SetInput(right.triggerButton, 1f);
            var actions = (Dictionary<(XRNode, string), InputAction>)typeof(VrInput)
                .GetField("Actions", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).GetValue(null);
            var triggerAction = actions[(XRNode.RightHand, "trigger")];
            Check(VrInput.Trigger(XRNode.RightHand), "Gameplay reads simulated trigger through Input System; raw=" + right.trigger.ReadValue() +
                "; enabled=" + right.enabled + "; phase=" + triggerAction.phase + "; controls=" + string.Join(",", triggerAction.controls.Select(c => c.path)));
            SetInput(right.trigger, 0f);
            SetInput(right.triggerButton, 0f);
            // A trigger without a UI target must never activate the global map-1 shortcut.
            yield return null;
            SetInput(right.trigger, 1f);
            SetInput(right.triggerButton, 1f);
            yield return null; yield return null;
            Check(gm.State == GameState.MainMenu, "Unpointed trigger cannot start a map before UI release");
            SetInput(right.trigger, 0f);
            SetInput(right.triggerButton, 0f);
            yield return null;
            yield return VerifyVrMapRay(gm, right);
            Vector3 lobby = gm.xrOrigin.position;
            gm.StartPlaneSelect();
            yield return null;
            gm.hud.SendMessage("LateUpdate");
            CaptureInterface(gm, "Interface_Selection");
            var allPlanes = UnityEngine.Object.FindObjectsByType<VrGrabPlane>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Check(allPlanes.Length == gm.planes.Count(d => gm.IsPlaneUnlocked(d)), "Only unlocked planes receive XR grab components");
            var planes = allPlanes.Where(p => p.gameObject.activeInHierarchy).ToArray();
            Check(planes.Length > 0 && planes.Length <= 4, "Reachable table page contains at most four planes");
            Check(Vector3.Distance(new Vector3(lobby.x, gm.selectAnchor.position.y, lobby.z), planes[0].transform.position) < 0.8f,
                "First grabbable plane is within 80 cm horizontally of the initial rig");
            Check(planes[0].transform.position.y > 0.85f && planes[0].transform.position.y < 1.1f,
                "Aircraft sits at a comfortable standing table height");
            Check(GameObject.Find("Lobby_Workshop") != null && GameObject.Find("TableLight") != null,
                "Workshop and local table lighting are present");
            CaptureView(gm, "Lobby_Workshop", gm.gameCamera.transform.position,
                Quaternion.LookRotation(gm.selectAnchor.position - gm.gameCamera.transform.position), false);
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
            var originalDefinition = selected.definition;
            var lockedDefinition = Instantiate(originalDefinition);
            lockedDefinition.unlockedByDefault = false;
            lockedDefinition.unlockScore = gm.BestScore + 1;
            selected.definition = lockedDefinition;
            interactor.StartManualInteraction((IXRSelectInteractable)grab);
            yield return null; yield return null;
            Check(!grab.isSelected && selected.State == TablePlaneState.OnTable && gm.State == GameState.PlaneSelect,
                "Rejected locked-plane grab cancels XRI selection and returns to the table");
            selected.definition = originalDefinition;
            Destroy(lockedDefinition);
            interactor.enabled = false;
            Destroy(interactor);
            yield return null;
            interactor = hand.AddComponent<XRDirectInteractor>();
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
            Check(gm.State == GameState.LevelComplete, "Map 1 finish advances to level results, not campaign completion");
            Check(rb.isKinematic, "Finishing freezes player body");
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
            gm.flightStartAnchor.rotation = Quaternion.Euler(0, 25f, 0);
            SetInput(right.deviceRotation, Quaternion.identity);
            SetInput(right.primary2DAxis, Vector2.zero);
            yield return VerifyControlResponse(gm, right);
            yield return VerifyFlightPause(gm, right);
            yield return VerifyCourses(gm);
            yield return VerifyEnemyShots(gm);
            yield return VerifyDroneTactics(gm);
            yield return VerifyUnlocks(gm);
            for (int index = 0; index < gm.levels.Count; index++)
                yield return FlyCourse(gm, right, index);
            gm.GoToMainMenu();
            yield return VerifyKeyboardScene();
            Destroy(hand);
            if (right.added) InputSystem.RemoveDevice(right);
        }

        IEnumerator VerifyControlResponse(GameManager gm, XRSimulatedController right)
        {
            gm.StartSelectedLevel(1); yield return null; // Space for unscaled tracking-recovery waits.
            gm.BeginFlightFromVrThrow(gm.planes.First(d => d.unlockedByDefault), gm.CourseRotation * Vector3.forward * 8f);
            var pilot = gm.flightController;
            var body = gm.Player.GetComponent<Rigidbody>();
            body.position = gm.FlightStart + Vector3.up * 30f;
            pilot.steering = FlightController.VrSteering.Sticks;
            SetInput(right.primary2DAxis, Vector2.zero);
            float previous = 0f, jump = 0f;
            for (int i = 0; i <= 35; i++)
            {
                SetInput(right.primary2DAxis, new Vector2(i * 0.01f, 0f)); yield return null;
                float current = pilot.SteeringInput.y;
                jump = Mathf.Max(jump, Mathf.Abs(current - previous)); previous = current;
            }
            Check(jump < 0.04f && previous > 0.1f, "Stick leaves its dead zone continuously, without a steering step");
            SetInput(right.primary2DAxis, Vector2.zero);
            yield return new WaitForSeconds(0.3f);
            SetInput(right.primary2DAxis, Vector2.right);
            yield return null; yield return new WaitForFixedUpdate();
            Check(pilot.AppliedSteeringInput.y > 0f && pilot.AppliedSteeringInput.y < 0.4f,
                "Full stick starts a progressive turn rather than snapping to maximum");
            yield return new WaitForSeconds(0.3f);
            Check(pilot.AppliedSteeringInput.y > 0.95f, "Smooth steering still reaches full turning authority promptly");
            SetInput(right.primary2DAxis, Vector2.zero);
            yield return new WaitForSeconds(0.18f);
            Check(pilot.AppliedSteeringInput.magnitude < 0.05f, "Releasing the stick quickly removes lingering turn input");
            pilot.steering = FlightController.VrSteering.ControllerTilt;
            SetInput(right.isTracked, 1f); SetInput(right.trackingState, 3);
            SetInput(right.deviceRotation, Quaternion.identity); pilot.CalibrateSteering();
            jump = 0f; previous = 0f;
            for (int i = 0; i <= 16; i++)
            {
                SetInput(right.deviceRotation, Quaternion.Euler(0f, 12f, 8f + i * 0.5f)); yield return null;
                float current = pilot.SteeringInput.y;
                if (i > 0) jump = Mathf.Max(jump, Mathf.Abs(current - previous)); previous = current;
            }
            Check(jump < 0.08f, "Opposing pointing and wrist roll blend without sudden sign changes");
            SetInput(right.deviceRotation, Quaternion.Euler(0f, -20f, 0f)); yield return null;
            previous = pilot.SteeringInput.y; jump = 0f;
            for (int i = 0; i <= 24; i++)
            {
                SetInput(right.primary2DAxis, new Vector2(i * 0.025f, 0f)); yield return null;
                float current = pilot.SteeringInput.y;
                jump = Mathf.Max(jump, Mathf.Abs(current - previous)); previous = current;
            }
            Check(jump < 0.16f && previous > 0.3f, "Opposite stick override takes control gradually from controller tilt");
            SetInput(right.primary2DAxis, Vector2.zero);
            SetInput(right.trackingState, 1); yield return null;
            Check(!pilot.SteeringTracked && pilot.SteeringInput.magnitude < 0.01f && pilot.AppliedSteeringInput.magnitude < 0.01f,
                "Position-only tracking cannot reuse stale controller rotation or filtered steering");
            SetInput(right.deviceRotation, Quaternion.Euler(0f, 35f, 0f));
            SetInput(right.trackingState, 3);
            // Calibration uses unscaled time; captureDeltaTime can advance game time faster than wall time.
            yield return new WaitForSecondsRealtime(0.3f);
            Check(pilot.SteeringReady && pilot.SteeringInput.magnitude < 0.01f,
                "Recovered controller tracking calibrates a neutral pose without a sudden turn");
            SetInput(right.primary2DAxis, Vector2.right); yield return new WaitForSeconds(0.2f);
            // SendMessage broadcasts to every component, including the separately tested flight-pause owner.
            var controlPause = typeof(FlightController).GetMethod("OnApplicationPause",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            controlPause.Invoke(pilot, new object[] { true }); yield return null;
            Check(pilot.SteeringInput.magnitude < 0.01f && pilot.AppliedSteeringInput.magnitude < 0.01f,
                "Application suspension clears live and smoothed steering");
            SetInput(right.primary2DAxis, Vector2.zero); SetInput(right.deviceRotation, Quaternion.identity);
            controlPause.Invoke(pilot, new object[] { false }); yield return new WaitForSecondsRealtime(0.3f);
            Check(pilot.SteeringReady && pilot.SteeringInput.magnitude < 0.01f, "Application resume returns to neutral steering");
            gm.GoToMainMenu(); yield return null;
        }

        IEnumerator VerifyFlightPause(GameManager gm, XRSimulatedController right)
        {
            gm.StartSelectedLevel(1); yield return null;
            gm.BeginFlightFromVrThrow(gm.planes.First(d => d.unlockedByDefault), gm.CourseRotation * Vector3.forward * 8f);
            var body = gm.Player.GetComponent<Rigidbody>();
            body.position = gm.FlightStart + Vector3.up * 15f;
            var level = gm.CurrentLevel;
            int score = gm.Score, gates = gm.levelRunner.GatesPassed;
            var pause = gm.FlightPause;
            SetInput(right.primaryButton, 0f);
            pause.SendMessage("OnApplicationFocus", false); yield return null;
            Check(!pause.IsPaused && Time.timeScale == 1f, "Editor window focus changes do not pause validation");
            pause.SendMessage("OnApplicationPause", true);
            Vector3 position = body.position, velocity = body.linearVelocity;
            float flightTime = gm.flightController.FlightTime;
            Check(pause.IsPaused && Time.timeScale == 0f && AudioListener.pause && gm.hud.PauseVisible,
                "Headset application suspension freezes time/audio and shows the existing-style pause panel");
            CaptureView(gm, "Interface_Pause", gm.gameCamera.transform.position, gm.gameCamera.transform.rotation, false);
            yield return new WaitForSecondsRealtime(0.15f);
            Check(body.position == position && body.linearVelocity == velocity && gm.flightController.FlightTime == flightTime &&
                gm.CurrentLevel == level && gm.Score == score && gm.levelRunner.GatesPassed == gates,
                "Pause freezes flight physics and timers without losing level, score or checkpoints");
            SetInput(right.primaryButton, 1f); yield return null;
            SetInput(right.primaryButton, 0f); yield return null;
            Check(pause.IsPaused, "A cannot resume while the application is still suspended");
            pause.SendMessage("OnApplicationPause", false); yield return null;
            Check(pause.IsPaused, "Returning to the headset waits for an explicit A confirmation");
            SetInput(right.isTracked, 1f); SetInput(right.trackingState, 3);
            SetInput(right.deviceRotation, Quaternion.Euler(0f, 22f, 0f));
            int shots = gm.levelRoot.GetComponentsInChildren<Projectile>().Length;
            SetInput(right.primaryButton, 1f); yield return null;
            SetInput(right.primaryButton, 0f); yield return null;
            Check(!pause.IsPaused && Time.timeScale == 1f && !AudioListener.pause && !gm.hud.PauseVisible &&
                gm.CurrentLevel == level && gm.State == GameState.Flight && gm.flightController.enabled,
                "A resumes the same flight and restores time, audio and the HUD");
            Check(gm.flightController.SteeringReady && gm.flightController.SteeringInput.magnitude < 0.01f,
                "Resume recalibrates controller tilt to the current neutral pose");
            Check(gm.levelRoot.GetComponentsInChildren<Projectile>().Length == shots,
                "The resume A press does not also fire a missile");
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            Check(Vector3.Distance(body.position, position) > 0.01f, "Flight physics moves again after resume");
            pause.RequestPause(); gm.GoToMainMenu(); yield return null;
            Check(Time.timeScale == 1f && !AudioListener.pause && !pause.IsPaused, "Leaving a paused flight cannot leave the next level frozen");
            SetInput(right.deviceRotation, Quaternion.identity);
        }

        IEnumerator VerifyEnemyShots(GameManager gm)
        {
            gm.GoToMainMenu(); yield return null;
            gm.StartSelectedLevel(1); yield return null; // The introductory classroom intentionally has no enemies.
            gm.BeginFlightFromVrThrow(gm.planes.First(d => d.unlockedByDefault), gm.CourseRotation * Vector3.forward * 8f);
            gm.flightController.enabled = false;
            var body = gm.Player.GetComponent<Rigidbody>();
            body.position = gm.FlightStart + Vector3.up * 20f;
            body.linearVelocity = Vector3.zero;
            var enemies = gm.levelRoot.GetComponentsInChildren<EnemyBrain>();
            Check(enemies.Length > 0, "Level creates enemies using the existing EnemyBrain");
            var drone = enemies.First(e => e.name == "Enemy_0");
            foreach (var enemy in enemies) enemy.enabled = enemy == drone;
            drone.transform.position = body.position + Vector3.right * 2f;
            drone.moveSpeed = 0f;
            drone.fireInterval = 0.1f;
            // This fixture isolates single-impact physics; spread is checked separately below.
            drone.aimSpreadDegrees = 0f;
            Vector3 direction = (body.position - drone.transform.position).normalized;
            Projectile shot = null;
            float deadline = Time.time + 1.5f;
            while (shot == null && Time.time < deadline)
            {
                yield return null;
                shot = gm.levelRoot.GetComponentsInChildren<Projectile>().FirstOrDefault(p => p.targetsPlayer);
            }
            Check(shot != null, "Drone fires a real enemy projectile toward the active player");
            drone.enabled = false;
            Check(shot.damage == 0f && shot.playerImpulse > 0f,
                "Default enemy projectile applies a non-lethal impulse");
            Check(Physics.GetIgnoreCollision(drone.GetComponentInChildren<Collider>(), shot.GetComponentInChildren<Collider>()),
                "Enemy projectile ignores its emitter instead of disappearing inside it");
            deadline = Time.time + 1f;
            while (shot != null && Time.time < deadline) yield return new WaitForFixedUpdate();
            Check(shot == null && Vector3.Dot(body.linearVelocity, direction) > 1f && gm.State == GameState.Flight,
                "Enemy shot hits an untagged child collider, pushes the plane once and preserves flight");
            Check(Mathf.Abs(body.linearVelocity.magnitude - drone.bulletImpulse) < 0.05f,
                "Compound player colliders cannot apply the same enemy impulse twice");
            body.linearVelocity = Vector3.zero;
            gm.ActivatePowerUp(PowerUpType.Shield, 5f);
            drone.enabled = true;
            deadline = Time.time + 1.5f;
            while (shot == null && Time.time < deadline)
            {
                yield return null;
                shot = gm.levelRoot.GetComponentsInChildren<Projectile>().FirstOrDefault(p => p.targetsPlayer);
            }
            Check(shot != null && gm.HasShield, "Drone fires a real projectile at a shielded plane");
            drone.enabled = false;
            deadline = Time.time + 1f;
            while (shot != null && Time.time < deadline) yield return new WaitForFixedUpdate();
            Check(shot == null && body.linearVelocity.magnitude < 0.01f && gm.State == GameState.Flight,
                "Shield blocks enemy impulse and keeps the flight active");
            var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blocker.transform.SetParent(gm.levelRoot);
            blocker.transform.position = body.position + Vector3.up * 5f;
            var damageable = blocker.AddComponent<Damageable>();
            int scoreBefore = gm.Score;
            float healthBefore = damageable.CurrentHealth;
            var blockedShot = Instantiate(gm.bulletPrefab, blocker.transform.position + Vector3.left * 2f,
                Quaternion.LookRotation(Vector3.right), gm.levelRoot);
            foreach (var collider in blockedShot.GetComponentsInChildren<Collider>())
            {
                if (collider is MeshCollider mesh) mesh.convex = true;
                collider.isTrigger = true;
            }
            var blockedProjectile = blockedShot.GetComponent<Projectile>() ?? blockedShot.AddComponent<Projectile>();
            blockedProjectile.targetsPlayer = true;
            blockedProjectile.Launch(Vector3.right, 8f);
            deadline = Time.time + 1f;
            while (blockedShot != null && Time.time < deadline) yield return new WaitForFixedUpdate();
            Check(blockedShot == null && damageable.CurrentHealth == healthBefore && gm.Score == scoreBefore,
                "Scenery blocks enemy shots without taking friendly-fire damage or awarding points");
            gm.GoToMainMenu();
            gm.flightController.enabled = true;
            yield return null;
            Check(!UnityEngine.Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None).Any(p => p.targetsPlayer),
                "Returning to maps removes enemy projectiles with their level");
        }

        IEnumerator VerifyDroneTactics(GameManager gm)
        {
            foreach (var velocity in new[] { Vector3.zero, Vector3.right * 24f, Vector3.forward * 30f })
            {
                Vector3 offset = velocity.z > 0f ? Vector3.back * 10f : new Vector3(10f, 0f, 10f);
                Check(EnemyBrain.TryInterceptDirection(Vector3.zero, offset, velocity, 30f, out var aim),
                    "Prediction handles stationary, crossing and equal-speed approaching targets: " + velocity);
                Vector3 closing = aim * 30f - velocity;
                float time = Vector3.Dot(offset, closing) / closing.sqrMagnitude;
                Check(time > 0 && time <= 2.5f && (closing * time - offset).magnitude < 0.001f,
                    "Predicted projectile meets the moving target within its lifetime");
            }
            Check(!EnemyBrain.TryInterceptDirection(Vector3.zero, Vector3.forward * 10f, Vector3.forward * 40f, 30f, out _),
                "Prediction rejects a receding target that cannot be intercepted");
            gm.StartSelectedLevel(1); yield return null;
            gm.BeginFlightFromVrThrow(gm.planes.First(d => d.unlockedByDefault), gm.CourseRotation * Vector3.forward * 8f);
            gm.flightController.enabled = false;
            var body = gm.Player.GetComponent<Rigidbody>();
            body.position = gm.FlightStart + Vector3.up * 20f;
            var initialVelocity = gm.CourseRotation * Vector3.right * 12f;
            body.linearVelocity = initialVelocity;
            var drone = gm.levelRoot.GetComponentsInChildren<EnemyBrain>().First(e => e.useDroneTactics);
            foreach (var enemy in gm.levelRoot.GetComponentsInChildren<EnemyBrain>()) enemy.enabled = enemy == drone;
            drone.transform.position = body.position + gm.CourseRotation * new Vector3(8f, 0f, 10f);
            drone.moveSpeed = 0f; drone.fireInterval = 0.1f;
            Check(drone.aimSpreadDegrees > 0f && drone.aimSpreadDegrees <= 2f && drone.fireWarningDuration >= 0.25f,
                "Live drones have a small configurable spread and a visible warning");
            drone.aimSpreadDegrees = 0f; // Deterministic moving-body intercept, without a random miss.
            float deadline = Time.time + 1.5f;
            bool warned = false;
            Projectile shot = null;
            while (shot == null && Time.time < deadline)
            {
                yield return null;
                warned |= drone.IsPreparingShot;
                shot = gm.levelRoot.GetComponentsInChildren<Projectile>().FirstOrDefault(p => p.targetsPlayer);
            }
            Check(warned && shot != null, "Drone warns before firing at a plane moving sideways at 12 m/s");
            drone.enabled = false;
            deadline = Time.time + 1.5f;
            while (shot != null && Time.time < deadline) yield return new WaitForFixedUpdate();
            Check(shot == null && Mathf.Abs((body.linearVelocity - initialVelocity).magnitude - drone.bulletImpulse) < 0.05f,
                "Predictive enemy shot physically intercepts a moving plane with exactly one impulse");
            body.linearVelocity = Vector3.zero;
            body.position = drone.transform.position - gm.CourseRotation * Vector3.forward * 8f;
            drone.enabled = true;
            deadline = Time.time + 0.5f;
            while (!drone.IsPreparingShot && Time.time < deadline) yield return null;
            Check(drone.IsPreparingShot, "Drone starts a fresh warning for an eligible target");
            body.position = drone.transform.position - gm.CourseRotation * Vector3.forward * 80f;
            yield return new WaitForSeconds(0.6f);
            Check(!drone.IsPreparingShot && !gm.levelRoot.GetComponentsInChildren<Projectile>().Any(p => p.targetsPlayer),
                "Leaving range during the warning cancels the shot");
            body.position = drone.transform.position + gm.CourseRotation * Vector3.forward * 4f;
            yield return new WaitForSeconds(0.6f);
            Check(!drone.IsPreparingShot && !gm.levelRoot.GetComponentsInChildren<Projectile>().Any(p => p.targetsPlayer),
                "A drone cannot fire from behind a plane that has passed it");
            // Sample actual emitted directions, leaving the target still to isolate spread.
            body.position = drone.transform.position - gm.CourseRotation * Vector3.forward * 8f;
            drone.aimSpreadDegrees = 1.25f;
            for (int i = 0; i < 5; i++)
            {
                shot = null; deadline = Time.time + 1f;
                while (shot == null && Time.time < deadline)
                {
                    yield return null;
                    shot = gm.levelRoot.GetComponentsInChildren<Projectile>().FirstOrDefault(p => p.targetsPlayer);
                }
                Check(shot != null && Vector3.Angle(shot.GetComponent<Rigidbody>().linearVelocity,
                    body.position - drone.transform.position) <= drone.aimSpreadDegrees + 0.01f,
                    "Emitted shot remains inside the configured spread cone " + i);
                Destroy(shot.gameObject); yield return null;
            }
            gm.GoToMainMenu(); yield return null;
            gm.StartSelectedLevel(1); yield return null;
            gm.BeginFlightFromVrThrow(gm.planes.First(d => d.unlockedByDefault), gm.CourseRotation * Vector3.forward * 8f);
            gm.flightController.enabled = false;
            body = gm.Player.GetComponent<Rigidbody>(); body.isKinematic = true;
            drone = gm.levelRoot.GetComponentsInChildren<EnemyBrain>().First(e => e.useDroneTactics);
            drone.bulletPrefab = null;
            Vector3 origin = drone.transform.position;
            Vector3 localOrigin = gm.CoursePoint(origin);
            body.position = gm.CourseOrigin + gm.CourseRotation * gm.CurrentLevel.PathPoint(localOrigin.z - 6f);
            float before = Vector3.Distance(body.position, origin);
            yield return new WaitForSeconds(1.5f);
            Check(Vector3.Distance(body.position, drone.transform.position) < before - 1f &&
                Vector3.Distance(origin, drone.transform.position) <= drone.pursuitLeash + 0.01f,
                "Drone pursues noticeably but stays inside its spawn leash; before=" + before +
                "; after=" + Vector3.Distance(body.position, drone.transform.position) +
                "; travel=" + Vector3.Distance(origin, drone.transform.position) +
                "; origin=" + gm.CoursePoint(origin) + "; drone=" + gm.CoursePoint(drone.transform.position) +
                "; player=" + gm.CoursePoint(body.position));
            Vector3 localDrone = gm.CoursePoint(drone.transform.position);
            float lateral = Mathf.Abs(Vector3.Dot(localDrone - gm.CurrentLevel.PathPoint(localDrone.z),
                gm.CurrentLevel.GroundRotation(localDrone.z) * Vector3.right));
            Check(lateral >= 3f && lateral <= 8.5f, "Pursuing drone leaves the course centre open");
            body.position = drone.transform.position - gm.CourseRotation * Vector3.forward;
            float separation = Vector3.Distance(body.position, drone.transform.position);
            yield return new WaitForSeconds(0.5f);
            Check(Vector3.Distance(body.position, drone.transform.position) >= separation - 0.01f,
                "Drone does not approach further when the plane is inside minimum separation");
            var health = drone.GetComponent<Damageable>();
            health.TakeDamage(health.maxHealth, drone.transform.position);
            yield return null;
            Check(drone == null, "Enlarged drone remains destructible through its existing Damageable");
            gm.GoToMainMenu(); gm.flightController.enabled = true; yield return null;
            var dronePrefab = gm.enemyDronePrefab; var birdPrefab = gm.enemyBirdPrefab; var fanPrefab = gm.enemyFanPrefab;
            var staticPrefab = gm.obstacleStaticPrefab; var movingPrefab = gm.obstacleMovingPrefab;
            try
            {
                gm.enemyDronePrefab = gm.enemyBirdPrefab = gm.enemyFanPrefab = null;
                gm.obstacleStaticPrefab = gm.obstacleMovingPrefab = null;
                gm.StartSelectedLevel(1); yield return null;
                gm.BeginFlightFromVrThrow(gm.planes.First(d => d.unlockedByDefault), gm.CourseRotation * Vector3.forward * 8f);
                Check(!gm.levelRoot.GetComponentsInChildren<EnemyBrain>().Any() &&
                    !gm.levelRoot.GetComponentsInChildren<WindFanForce>().Any() &&
                    !gm.levelRoot.GetComponentsInChildren<Damageable>().Any(d => d.name.StartsWith("Obstacle_")),
                    "Missing enemy and obstacle prefabs are skipped without inventing replacement objects");
            }
            finally
            {
                gm.enemyDronePrefab = dronePrefab; gm.enemyBirdPrefab = birdPrefab; gm.enemyFanPrefab = fanPrefab;
                gm.obstacleStaticPrefab = staticPrefab; gm.obstacleMovingPrefab = movingPrefab;
                gm.GoToMainMenu();
            }
            yield return null;
        }

        IEnumerator VerifyUnlocks(GameManager gm)
        {
            int originalBest = gm.BestScore;
            var bestProperty = typeof(GameManager).GetProperty(nameof(GameManager.BestScore));
            Check(!gm.persistProgress, "Unlock checks cannot overwrite the player's saved progress");
            try
            {
                bestProperty.SetValue(gm, 0);
                Check(gm.planes.Count == 12 && gm.planes.Count(gm.IsPlaneUnlocked) == 6,
                    "A new player has 12 aircraft across three pages, six initially unlocked");
                foreach (int score in new[] { 299, 300, 599, 600, 899, 900 })
                {
                    gm.GoToMainMenu(); yield return null;
                    gm.StartSelectedLevel(0); yield return null;
                    gm.BeginFlightFromVrThrow(gm.planes.First(d => d.unlockedByDefault), gm.CourseRotation * Vector3.forward * 8f);
                    gm.AddScore(score);
                    gm.FinishRun(false);
                    int expected = score < 300 ? 6 : score < 600 ? 7 : score < 900 ? 10 : 12;
                    Check(gm.BestScore == score && gm.planes.Count(gm.IsPlaneUnlocked) == expected,
                        "Finishing with " + score + " points makes " + expected + " aircraft selectable");
                }
            }
            finally { bestProperty.SetValue(gm, originalBest); }
            gm.GoToMainMenu(); yield return null;
        }

        void Click(GameManager gm, string name)
        {
            var button = gm.hud.InterfaceCanvas.GetComponentsInChildren<UnityEngine.UI.Button>()
                .Single(b => b.name == name);
            Check(button.isActiveAndEnabled && button.IsInteractable(), name + " is an active button");
            // Go through hit testing: invoking Button.OnPointerClick directly hides missing raycasters
            // and labels which look clickable but do not actually receive pointer events.
            gm.hud.SendMessage("LateUpdate");
            Canvas.ForceUpdateCanvases();
            var canvas = gm.hud.InterfaceCanvas;
            var rect = (RectTransform)button.transform;
            var events = UnityEngine.EventSystems.EventSystem.current;
            var pointer = new UnityEngine.EventSystems.PointerEventData(events)
            {
                button = UnityEngine.EventSystems.PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(
                    canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
                    rect.TransformPoint(rect.rect.center))
            };
            var hits = new List<UnityEngine.EventSystems.RaycastResult>();
            events.RaycastAll(pointer, hits);
            Check(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Button>() == button,
                name + " receives pointer hits at its visible centre");
            UnityEngine.EventSystems.ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer,
                UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
        }

        IEnumerator VerifyTablePages(GameManager gm)
        {
            var selector = gm.planeSelector;
            Check(selector.Page == 1 && selector.PageCount == 3, "VR table starts on page 1 of 3");
            for (int page = 2; page <= selector.PageCount; page++)
            {
                yield return null;
                Click(gm, "Next");
                Check(selector.Page == page && selector.Current == gm.planes[(page - 1) * 4],
                    "One Next click switches to table " + page);
                int visible = gm.selectAnchor.Cast<Transform>().Count(t => t.name.StartsWith("Select_") && t.gameObject.activeInHierarchy);
                Check(visible == Mathf.Min(4, gm.planes.Count - (page - 1) * 4),
                    "Table " + page + " shows only its own aircraft");
            }
            yield return null;
            Click(gm, "Next");
            Check(selector.Page == 1, "Next wraps from table 3 to table 1");
            for (int page = selector.PageCount; page >= 1; page--)
            {
                yield return null;
                Click(gm, "Previous");
                Check(selector.Page == page, "Previous switches directly to table " + page);
            }
        }

        IEnumerator VerifyVrMapRay(GameManager gm, XRSimulatedController right)
        {
            Check(gm.State == GameState.MainMenu, "Map ray test starts on the main menu");
            gm.hud.Refresh();
            gm.hud.SendMessage("LateUpdate");
            var events = UnityEngine.EventSystems.EventSystem.current;
            Check(events != null && events.GetComponent<UnityEngine.XR.Interaction.Toolkit.UI.XRUIInputModule>() is { enabled: true },
                "Existing EventSystem keeps XRUIInputModule enabled for VR UI");
            var rays = gm.xrOrigin.GetComponentsInChildren<NearFarInteractor>(true);
            var ray = rays.FirstOrDefault(r =>
                r.GetComponentsInParent<Transform>(true).Any(t => t.name.IndexOf("Right", StringComparison.OrdinalIgnoreCase) >= 0));
            Check(ray != null, "Right NearFarInteractor exists under XR Origin (found=" + rays.Length + ": " +
                string.Join(", ", rays.Select(r => r.name + "@" + (r.transform.parent != null ? r.transform.parent.name : "?"))) + ")");
            // Modality managers keep controllers off until tracked; force the right ray on for this UI check.
            foreach (var t in ray.GetComponentsInParent<Transform>(true))
                t.gameObject.SetActive(true);
            ray.enableUIInteraction = true;
            Check(ray.isActiveAndEnabled && ray.enableUIInteraction, "Right NearFarInteractor is active for UI");
            var card = gm.hud.InterfaceCanvas.GetComponentsInChildren<UnityEngine.UI.Button>().Single(b => b.name == "Map2");
            Check(card.isActiveAndEnabled && card.IsInteractable() && card.targetGraphic.raycastTarget,
                "Map 3 card remains a real interactable Button with raycast graphics");
            var hand = ray.GetComponentsInParent<Transform>(true)
                .First(t => t.name.IndexOf("Right Controller", StringComparison.OrdinalIgnoreCase) >= 0);
            // TrackedPoseDriver can lag or disagree with injected device pose under batch Play Mode.
            // Aim the live controller transform so NearFarInteractor/XRUIInputModule still drive the real UI path.
            var poseDrivers = hand.GetComponentsInChildren<Behaviour>(true)
                .Where(b => b != null && b.GetType().Name.Contains("TrackedPoseDriver")).ToArray();
            foreach (var driver in poseDrivers) driver.enabled = false;
            SetInput(right.isTracked, 1f);
            SetInput(right.trackingState, 3);
            SetInput(right.trigger, 0f);
            SetInput(right.triggerButton, 0f);
            for (int attempt = 0; attempt < 30; attempt++)
            {
                gm.hud.SendMessage("LateUpdate");
                var rect = (RectTransform)card.transform;
                Vector3 target = rect.TransformPoint(rect.rect.center);
                Vector3 camPos = gm.gameCamera != null ? gm.gameCamera.transform.position : gm.xrOrigin.position + Vector3.up * 1.6f;
                hand.position = camPos + gm.gameCamera.transform.right * 0.2f + gm.gameCamera.transform.forward * 0.25f - Vector3.up * 0.15f;
                Vector3 aimOrigin = ray.curveOrigin != null ? ray.curveOrigin.position : hand.position;
                hand.rotation = Quaternion.LookRotation((target - aimOrigin).normalized, Vector3.up);
                SetInput(right.devicePosition, gm.xrOrigin.InverseTransformPoint(hand.position));
                SetInput(right.deviceRotation, Quaternion.Inverse(gm.xrOrigin.rotation) * hand.rotation);
                PlayerInputUpdate();
                yield return null; yield return null;
                if (ray.TryGetCurrentUIRaycastResult(out var currentHit) &&
                    currentHit.gameObject != null &&
                    currentHit.gameObject.GetComponentInParent<UnityEngine.UI.Button>() == card)
                    break;
            }
            yield return null; yield return null;
            bool aimed = ray.TryGetCurrentUIRaycastResult(out var hit) && hit.gameObject != null &&
                hit.gameObject.GetComponentInParent<UnityEngine.UI.Button>() == card && gm.hud.HasUiTarget;
            var cardCenter = ((RectTransform)card.transform).TransformPoint(((RectTransform)card.transform).rect.center);
            Check(aimed, "Actual XR controller ray targets the existing map 3 card (hit=" + hit.gameObject?.name +
                "; origin=" + (ray.curveOrigin != null ? ray.curveOrigin.position.ToString("F2") : "null") +
                "; card=" + cardCenter.ToString("F2") +
                "; canvas=" + gm.hud.InterfaceCanvas.transform.position.ToString("F2") + ")");
            // UI Press bindings use TriggerButton; keep axis and button in sync for XRUIInputModule.
            SetInput(right.trigger, 1f);
            SetInput(right.triggerButton, 1f);
            yield return null; yield return null;
            Check(gm.State == GameState.MainMenu, "UI trigger press does not prematurely start map 1");
            SetInput(right.trigger, 0f);
            SetInput(right.triggerButton, 0f);
            yield return null; yield return null; yield return null;
            Check(gm.State == GameState.PlaneSelect && gm.CurrentLevelIndex == 2,
                "XR UI trigger release clicks map 3 through the real EventSystem");
            foreach (var driver in poseDrivers) driver.enabled = true;
            gm.GoToMainMenu();
            SetInput(right.deviceRotation, Quaternion.identity);
            yield return null;
        }

        void CheckClearObstacleSweep(Vector3 world, Damageable[] obstacles, int index, int phase)
        {
            if (Physics.OverlapSphere(world, 0.7f, ~0, QueryTriggerInteraction.Ignore)
                .Any(c => obstacles.Contains(c.GetComponentInParent<Damageable>())))
                throw new Exception("Moving obstacle blocks map " + (index + 1) + " at " + world + ", phase " + phase);
        }

        IEnumerator VerifyCourses(GameManager gm)
        {
            var originalRig = gm.xrOrigin;
            float previousBulletSpeed = 0f, previousInterval = float.MaxValue;
            int last = gm.levels.Count - 1;
            for (int index = 1; index <= last; index++)
            {
                gm.GoToMainMenu();
                yield return null;
                Click(gm, "Map" + index);
                yield return null;
                gm.BeginFlightFromVrThrow(gm.planes.First(d => d.unlockedByDefault), gm.CourseRotation * Vector3.forward * 8f);
                gm.flightController.enabled = false;
                var body = gm.Player.GetComponent<Rigidbody>();
                body.isKinematic = true;
                yield return null;
                var level = gm.CurrentLevel;
                Check(gm.xrOrigin == originalRig && gm.levelRunner.GateCount > 0, "Map " + (index + 1) + " builds its route without replacing XR Origin");
                float peakHeight = 0, maxTurnRate = 0, minPitch = 0, maxPitch = 0;
                for (float z = 1; z < level.length - 1; z += 0.25f)
                {
                    Vector3 tangent = level.PathRotation(z) * Vector3.forward;
                    Vector3 next = level.PathRotation(z + 0.25f) * Vector3.forward;
                    float segment = Vector3.Distance(level.PathPoint(z), level.PathPoint(z + 0.25f));
                    maxTurnRate = Mathf.Max(maxTurnRate, Vector3.Angle(Vector3.ProjectOnPlane(tangent, Vector3.up),
                        Vector3.ProjectOnPlane(next, Vector3.up)) / segment * level.flightSpeedLimit);
                    float pitch = Mathf.Asin(tangent.y) * Mathf.Rad2Deg;
                    minPitch = Mathf.Min(minPitch, pitch); maxPitch = Mathf.Max(maxPitch, pitch);
                    peakHeight = Mathf.Max(peakHeight, level.PathPoint(z).y);
                }
                Check(maxTurnRate < gm.SelectedPlane.turnSpeed, "Map " + (index + 1) + " bends stay within aircraft turning authority: " + maxTurnRate.ToString("0.0") + " deg/s");
                if (index >= 2)
                    Check(peakHeight > 8 && minPitch < -4 && maxPitch > 4 && level.length > gm.levels[1].length * 2,
                        "Map " + (index + 1) + " combines climbs, descents and a substantially longer route");
                if (index >= 3)
                    Check(level.routePoints != null && level.routePoints.Length > 8 && level.enemyCount > gm.levels[index - 1].enemyCount,
                        "Map " + (index + 1) + " is authored with its own route and escalating enemies");
                var obstacles = gm.levelRoot.GetComponentsInChildren<Damageable>().Where(d => d.name.StartsWith("Obstacle_")).ToArray();
                Check(obstacles.Length == level.obstacleCount, "All authored obstacles are built");
                var drones = gm.levelRoot.GetComponentsInChildren<EnemyBrain>().Where(e => e.useDroneTactics).ToArray();
                Check(drones.Length == (level.enemyCount + 2) / 3 && drones.All(e =>
                    Vector3.Distance(e.transform.localScale, gm.enemyDronePrefab.transform.localScale * gm.levelRunner.droneScale) < 0.001f),
                    "Map " + (index + 1) + " scales only existing drone instances by " + gm.levelRunner.droneScale);
                Check(drones.All(e => e.GetComponent<Damageable>().maxHealth == 32f && e.GetComponentsInChildren<Collider>().Any(c => !c.isTrigger)),
                    "Larger drones retain destructible solid colliders and original health");
                Check(drones.All(e => e.GetComponent<SpinBob>() == null || !e.GetComponent<SpinBob>().enabled) &&
                    gm.enemyDronePrefab.GetComponent<SpinBob>().enabled,
                    "Drone instances disable conflicting showcase motion without changing the shared prefab");
                var bird = gm.levelRoot.GetComponentsInChildren<EnemyBrain>().First(e => e.name == "Enemy_1");
                Check(!bird.useDroneTactics && bird.transform.localScale == gm.enemyBirdPrefab.transform.localScale && bird.bulletSpeed == 8f,
                    "Bird scale and existing shooting behaviour remain unchanged");
                Check(drones[0].bulletSpeed > previousBulletSpeed && drones[0].fireInterval < previousInterval,
                    "Drone projectile speed and cadence increase with map difficulty");
                previousBulletSpeed = drones[0].bulletSpeed; previousInterval = drones[0].fireInterval;
                Physics.SyncTransforms();
                for (float z = 8; z < level.length - 8; z += 0.75f)
                {
                    Vector3 world = gm.CourseOrigin + gm.CourseRotation * level.PathPoint(z);
                    var blockers = Physics.OverlapSphere(world, 0.7f, ~0, QueryTriggerInteraction.Ignore)
                        .Where(c => obstacles.Contains(c.GetComponentInParent<Damageable>())).ToArray();
                    if (blockers.Length > 0) throw new Exception("Blocked route at " + z + ": " + blockers[0].transform.root.name);
                }
                Check(true, "Map " + (index + 1) + " centreline has at least 0.7 m obstacle clearance");
                var moving = obstacles.Select(o => o.GetComponent<MovingObstacleMotion>()).Where(m => m != null).ToArray();
                var positions = moving.Select(m => m.transform.position).ToArray();
                for (int phase = -4; phase <= 4; phase++)
                {
                    for (int i = 0; i < moving.Length; i++)
                        moving[i].transform.position = positions[i] + moving[i].axis.normalized * moving[i].distance * phase / 4f;
                    Physics.SyncTransforms();
                    for (float z = 8; z < level.length - 8; z += 0.75f)
                    {
                        Vector3 world = gm.CourseOrigin + gm.CourseRotation * level.PathPoint(z);
                        CheckClearObstacleSweep(world, obstacles, index, phase);
                    }
                }
                for (int i = 0; i < moving.Length; i++) moving[i].transform.position = positions[i];
                Physics.SyncTransforms();
                Check(true, "Map " + (index + 1) + " keeps 0.7 m clearance across nine moving-obstacle phases, including both extremes");
                CaptureView(gm, "Map" + (index + 1) + "_Course", gm.CourseOrigin + gm.CourseRotation *
                    (level.PathPoint(index == 1 ? 28 : 185) + new Vector3(-7, 8, -12)),
                    gm.CourseRotation * Quaternion.Euler(20, 24, 0), true);
                Vector3 finish = gm.CourseOrigin + gm.CourseRotation * level.PathPoint(level.length);
                Vector3 finishForward = gm.CourseRotation * level.PathRotation(level.length) * Vector3.forward;
                // Route the teleport outside the gates. A single entrance-to-finish segment can
                // legitimately cross the first gate and is not a test of skipping every gate.
                body.position = gm.CourseOrigin + gm.CourseRotation * new Vector3(40f, 30f, 0f);
                yield return new WaitForFixedUpdate(); yield return null;
                body.position = gm.CourseOrigin + gm.CourseRotation * new Vector3(40f, 30f, level.length);
                yield return new WaitForFixedUpdate(); yield return null;
                body.position = finish - finishForward;
                yield return new WaitForFixedUpdate(); yield return null;
                body.position = finish + finishForward;
                yield return new WaitForFixedUpdate(); yield return null;
                Check(gm.State == GameState.Flight && gm.levelRunner.GatesPassed == 0,
                    "Skipping required gates cannot complete map " + (index + 1) +
                    "; state=" + gm.State + "; gates=" + gm.levelRunner.GatesPassed + "/" + gm.levelRunner.GateCount);
                for (int gate = 0; gate < gm.levelRunner.GateCount; gate++)
                {
                    Vector3 center = gm.levelRunner.GateWorld(gate);
                    Vector3 forward = gm.levelRunner.GateRotation(gate) * Vector3.forward;
                    body.position = center - forward * 0.6f;
                    yield return new WaitForFixedUpdate(); yield return null;
                    body.position = center + forward * 0.6f;
                    yield return new WaitForFixedUpdate(); yield return null;
                    Check(gm.levelRunner.GatesPassed == gate + 1, "Ordered checkpoint " + gate + " on map " + (index + 1));
                }
                body.position = finish - finishForward;
                yield return null;
                body.position = finish + finishForward;
                yield return null;
                Check(index < last ? gm.State == GameState.LevelComplete : gm.State == GameState.GameOver && gm.CampaignComplete,
                    "Map " + (index + 1) + " reaches the correct results state");
                gm.flightController.enabled = true;
                yield return null;
                Click(gm, "Primary");
                Check(gm.State == GameState.PlaneSelect && gm.CurrentLevelIndex == (index < last ? index + 1 : 0),
                    "Results button advances to the next map or restarts the completed campaign");
            }
            gm.GoToMainMenu(); yield return null;
            Click(gm, "Map1"); yield return null;
            gm.BeginFlightFromVrThrow(gm.planes.First(d => d.unlockedByDefault), gm.CourseRotation * Vector3.forward * 8f);
            gm.FinishRun(false); yield return null;
            Click(gm, "Primary");
            Check(gm.CurrentLevelIndex == 1 && gm.State == GameState.PlaneSelect && gm.levelRunner.GatesPassed == 0,
                "Retry stays on selected map and clears checkpoints");
            yield return null;
            gm.BeginFlightFromVrThrow(gm.planes.First(d => d.unlockedByDefault), gm.CourseRotation * Vector3.forward * 8f);
            gm.FinishRun(false); yield return null;
            Click(gm, "BackToMaps");
            Check(gm.State == GameState.MainMenu && gm.Player == null && gm.levelRunner.GateCount == 0,
                "Full results back button cleans the course and returns to the map menu");
        }

        // Test pilot feeds the real simulated stick and physics. No teleport, auto-boost or disabled collisions.
        IEnumerator FlyCourse(GameManager gm, XRSimulatedController right, int index)
        {
            Debug.Log("[AvionesValidation] Starting physical flight for map " + (index + 1));
            gm.GoToMainMenu(); yield return null;
            gm.StartSelectedLevel(index); yield return null;
            var plane = gm.planes.First(d => d.unlockedByDefault);
            gm.flightController.steering = FlightController.VrSteering.Sticks;
            gm.BeginFlightFromVrThrow(plane, gm.CourseRotation * Vector3.forward * 8f);
            var body = gm.Player.GetComponent<Rigidbody>();
            float elapsed = 0;
            while (gm.State == GameState.Flight && elapsed < gm.CurrentLevel.timeLimit)
            {
                Vector3 local = gm.CoursePoint(body.position);
                float lookAhead = Mathf.Max(3f, body.linearVelocity.magnitude * 0.65f);
                Vector3 target = gm.CourseOrigin + gm.CourseRotation * gm.CurrentLevel.PathPoint(local.z + lookAhead);
                Vector3 delta = target - body.position;
                float yaw = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
                float turn = Mathf.Clamp(Mathf.DeltaAngle(body.rotation.eulerAngles.y, yaw) * 3f / plane.turnSpeed, -1f, 1f);
                float climb = Mathf.Atan2(delta.y, new Vector2(delta.x, delta.z).magnitude) * Mathf.Rad2Deg;
                float pitch = Mathf.Clamp((climb + 3.5f) / 28f, -1f, 1f);
                SetInput(right.primary2DAxis, new Vector2(turn, pitch));
                elapsed += Time.deltaTime;
                yield return null;
            }
            SetInput(right.primary2DAxis, Vector2.zero);
            bool lastMap = index == gm.levels.Count - 1;
            Check(gm.State == (!lastMap ? GameState.LevelComplete : GameState.GameOver) &&
                gm.levelRunner.GatesPassed == gm.levelRunner.GateCount && (!lastMap || gm.CampaignComplete),
                "Physical flight completes map " + (index + 1) + " with the starter plane; elapsed=" + elapsed.ToString("0.0") +
                "; gates=" + gm.levelRunner.GatesPassed + "/" + gm.levelRunner.GateCount + "; position=" + gm.CoursePoint(body.position));
            Check(gm.BoostsCollected > 2, "Physical flight collects the route's energy boosts");
        }

        IEnumerator VerifyMouseFlight(GameManager gm)
        {
            var mouse = InputSystem.AddDevice<Mouse>();
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                gm.StartSelectedLevel(0); yield return null;
                Click(gm, "Primary");
                gm.BeginFlight(gm.CourseRotation * Vector3.forward * 8f);
                var pilot = gm.flightController;
                var body = gm.Player.GetComponent<Rigidbody>();
                body.position = gm.FlightStart + Vector3.up * 30f;
                float startYaw = body.rotation.eulerAngles.y;
                var readControls = typeof(FlightController).GetMethod("Update",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                // Mouse delta resets at the next Input System update. Consume each injected sample in its frame.
                Cursor.lockState = CursorLockMode.Locked;
                SetInput(mouse.delta, new Vector2(120f, 0f)); readControls.Invoke(pilot, null);
                SetInput(mouse.delta, Vector2.zero);
                Check(pilot.SteeringInput.y > 0.2f, "Mouse motion actually commands aircraft steering without keyboard input");
                yield return new WaitForSeconds(1.2f);
                float turned = Mathf.DeltaAngle(startYaw, body.rotation.eulerAngles.y);
                Check(turned > 10f && turned < 19f && Mathf.Abs(pilot.AppliedSteeringInput.y) < 0.08f,
                    "Mouse selects a heading, reaches it smoothly and stops turning; degrees=" + turned);
                float rightYaw = body.rotation.eulerAngles.y;
                SetInput(mouse.delta, new Vector2(-180f, 0f)); readControls.Invoke(pilot, null);
                SetInput(mouse.delta, Vector2.zero); yield return new WaitForSeconds(0.7f);
                Check(Mathf.DeltaAngle(rightYaw, body.rotation.eulerAngles.y) < -10f,
                    "Moving the mouse left changes the aircraft heading left");
                SetInput(mouse.delta, new Vector2(0f, 100f)); readControls.Invoke(pilot, null);
                SetInput(mouse.delta, Vector2.zero); yield return new WaitForSeconds(0.4f);
                Check(pilot.SteeringInput.x > 0.3f && Mathf.DeltaAngle(0f, body.rotation.eulerAngles.x) < -4f,
                    "Moving the mouse up pitches the aircraft up with bounded control");
                SetInput(keyboard.sKey, 1f); SetInput(keyboard.dKey, 1f); yield return null;
                Check(pilot.SteeringInput.x < -0.9f && pilot.SteeringInput.y > 0.9f,
                    "Keyboard corrections override mouse pitch and heading");
                SetInput(keyboard.sKey, 0f); SetInput(keyboard.dKey, 0f); yield return new WaitForSeconds(0.2f);
                Check(pilot.SteeringInput.magnitude < 0.01f, "Releasing keyboard correction does not snap back to an old mouse heading");
                SetInput(keyboard.escapeKey, 1f); readControls.Invoke(pilot, null);
                SetInput(keyboard.escapeKey, 0f); yield return null;
                SetInput(mouse.delta, new Vector2(300f, 300f)); readControls.Invoke(pilot, null);
                SetInput(mouse.delta, Vector2.zero);
                Check(Cursor.lockState == CursorLockMode.None && pilot.SteeringInput.magnitude < 0.01f,
                    "Escape releases the cursor and mouse movement no longer steers the plane");
                gm.GoToMainMenu(); yield return null;
            }
            finally
            {
                if (mouse.added) InputSystem.RemoveDevice(mouse);
                if (keyboard.added) InputSystem.RemoveDevice(keyboard);
            }
        }

        IEnumerator VerifyKeyboardScene()
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("Game_Playable");
            yield return null; yield return null;
            var gm = GameManager.Instance;
            gm.persistProgress = false;
            Check(!gm.vrMode && gm.levels.Count == ExpectedLevels, "SceneManager loads the keyboard entry with the same five maps");
            for (int index = 0; index < gm.levels.Count; index++)
            {
                yield return null;
                Click(gm, "Map" + index);
                Check(gm.CurrentLevelIndex == index && gm.State == GameState.PlaneSelect, "Keyboard map card loads map " + (index + 1));
                yield return null;
                var previousPlane = gm.planeSelector.Current;
                Click(gm, "Next");
                Check(gm.planeSelector.Current != previousPlane, "Next aircraft button is connected");
                Click(gm, "Previous");
                Check(gm.planeSelector.Current == previousPlane, "Previous aircraft button is connected");
                Click(gm, "Primary");
                Check(gm.State == GameState.Launch, "Keyboard selection enters the existing launch controller");
                gm.BeginFlight(gm.CourseRotation * Vector3.forward * 8);
                Check(gm.State == GameState.Flight && gm.CurrentLevelIndex == index, "Keyboard launch builds the selected map");
                gm.GoToMainMenu();
            }
            yield return VerifyMouseFlight(gm);
#pragma warning disable CS0618
            // Both simulators own the same XRI lifecycle singleton, even while inactive.
            if (XRInteractionSimulator.instance != null) Destroy(XRInteractionSimulator.instance.gameObject);
            foreach (var device in InputSystem.devices.OfType<XRSimulatedController>().ToArray())
                InputSystem.RemoveDevice(device);
            yield return null;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath("18ddb545287c546e19cc77dc9fbb2189"));
            var classic = Instantiate(prefab);
            yield return null;
            Check(classic != null && classic.GetComponent<XRDeviceSimulator>().isActiveAndEnabled,
                "Installed classic XR Device Simulator prefab initializes successfully");
            Destroy(classic);
#pragma warning restore CS0618
        }

        static void CaptureView(GameManager gm, string name, Vector3 position, Quaternion rotation, bool hideInterface)
        {
            bool wasEnabled = gm.hud.InterfaceCanvas.enabled;
            if (hideInterface) gm.hud.InterfaceCanvas.enabled = false;
            bool fog = RenderSettings.fog;
            if (hideInterface) RenderSettings.fog = false;
            var go = new GameObject("ValidationView");
            var camera = go.AddComponent<Camera>(); camera.CopyFrom(gm.gameCamera);
            camera.enabled = false; camera.stereoTargetEye = StereoTargetEyeMask.None;
            camera.fieldOfView = 68; camera.farClipPlane = 650;
            go.transform.SetPositionAndRotation(position, rotation);
            var target = new RenderTexture(1280, 800, 24);
            var previous = RenderTexture.active;
            camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            var texture = new Texture2D(1280, 800, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0); texture.Apply();
            Directory.CreateDirectory("Logs"); File.WriteAllBytes("Logs/" + name + ".png", texture.EncodeToPNG());
            camera.targetTexture = null; RenderTexture.active = previous; target.Release();
            Destroy(texture); Destroy(target); Destroy(go);
            RenderSettings.fog = fog; gm.hud.InterfaceCanvas.enabled = wasEnabled;
        }

        void CaptureInterface(GameManager gm, string filename)
        {
            gm.hud.SendMessage("LateUpdate");
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

