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

        static double _idleSince;
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
                if (previous?.scenes != null) EditorSceneManager.RestoreSceneManagerSetup(previous.scenes);
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
        void Check(bool condition, string description)
        {
            if (!condition) throw new Exception(description);
            _report.checks.Add(description);
        }
        IEnumerator Start()
        {
            Application.runInBackground = true;
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
            File.WriteAllText(GameplayValidation.ResultPath, JsonUtility.ToJson(_report, true));
            Debug.Log("[AvionesValidation] " + (_report.passed ? "PASS" : "FAIL") + " " + _report.checks.Count + " checks");
            EditorApplication.isPlaying = false;
        }

        IEnumerator Checks()
        {
            yield return null;
            yield return null;
            var gm = GameManager.Instance;
            if (gm != null) gm.persistProgress = false;
            Check(gm != null && gm.State == GameState.MainMenu, "Main menu initialized");
            var simulator = XRInteractionSimulator.instance;
            Check(simulator != null && simulator.isActiveAndEnabled, "XRI simulator instantiated and active");
            // Keep devices, stop keyboard simulation from overwriting deterministic input.
            simulator.enabled = false;
            XRSimulatedController right = null;
            foreach (var d in InputSystem.devices)
                if (d is XRSimulatedController c && c.usages.Contains(UnityEngine.InputSystem.CommonUsages.RightHand)) right = c;
            Check(right != null, "Simulated right controller exists");
            VrInput.Trigger(XRNode.RightHand);
            InputState.Change(right.trigger, 1f);
            Check(VrInput.Trigger(XRNode.RightHand), "Gameplay reads simulated trigger through Input System");
            InputState.Change(right.trigger, 0f);
            Vector3 lobby = gm.xrOrigin.position;
            gm.StartPlaneSelect();
            yield return null;
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
            Check(gm.State == GameState.Flight, "Moving release starts flight after XRI detach");
            var player = GameObject.FindGameObjectWithTag("Player");
            Check(player != null && player.GetComponent<Rigidbody>().linearVelocity.magnitude > 1f, "Flight retains nonzero throw velocity");
            Check(player.GetComponent<Rigidbody>().linearVelocity.x > 0.1f, "Throw lateral direction preserved");
            Check(player.transform.Find("PilotSeat") != null, "Explicit PilotSeat created");
            Vector3 seat = player.transform.Find("PilotSeat").position;
            Check(Vector3.Distance(gm.gameCamera.transform.position, seat) < 0.15f, "HMD initial height compensated at seat");
            var rb = player.GetComponent<Rigidbody>();
            float beforeBoost = rb.linearVelocity.magnitude;
            gm.flightController.ApplyBoost();
            Check(rb.linearVelocity.magnitude > beforeBoost && rb.linearVelocity.magnitude <= gm.SelectedPlane.EffectiveMaxSpeed + 0.01f, "Boost increases speed within plane limit");
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
            gm.flightController.enabled = true;
            gm.FinishRun(true);
            Check(gm.State == GameState.GameOver && rb.isKinematic, "Landing finalizes and freezes player body");
            Check(gm.Score >= score + 200 && gm.BestScore >= gm.Score, "Results include landing bonus and best score");
            gm.vrRigFollower.StopFollowAndReturnLobby();
            Check(Vector3.Distance(lobby, gm.xrOrigin.position) < 0.01f, "Rig restores captured lobby pose");
            gm.RestartRun();
            yield return null;
            Check(gm.State == GameState.PlaneSelect && gm.Score == 0, "Restart returns to physical selection");
            Destroy(hand);
        }
    }
}
#endif
