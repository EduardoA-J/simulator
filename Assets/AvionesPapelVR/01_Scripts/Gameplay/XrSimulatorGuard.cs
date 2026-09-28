using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;

namespace AvionesPapelVR
{
    public static class XrSimulatorGuard
    {
        public const string SimulationPreference = "AvionesPapelVR.UseSimulator";
        public const string DeviceSimulationPreference = "AvionesPapelVR.UseClassicDeviceSimulator";
        public const string ValidationSimulation = "AvionesPapelVR.ValidationSimulator";
        static readonly List<XRDisplaySubsystem> Displays = new();
        public static bool HardwareRunning
        {
            get
            {
                SubsystemManager.GetSubsystems(Displays);
                foreach (var display in Displays) if (display.running) return true;
                return false;
            }
        }

        // XRI automatic instantiation is disabled in the settings asset. Only this
        // explicit editor mode creates simulated devices; Android never does.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
#if UNITY_EDITOR
            bool requested = UnityEditor.EditorPrefs.GetBool(SimulationPreference, false) ||
                UnityEditor.SessionState.GetBool(ValidationSimulation, false);
#pragma warning disable CS0618
            if (requested && !HardwareRunning && XRInteractionSimulator.instance == null &&
                Object.FindFirstObjectByType<XRDeviceSimulator>() == null)
#pragma warning restore CS0618
            {
                bool classic = UnityEditor.EditorPrefs.GetBool(DeviceSimulationPreference, false) &&
                    !UnityEditor.SessionState.GetBool(ValidationSimulation, false);
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(classic ?
                    "18ddb545287c546e19cc77dc9fbb2189" : "58d0a4ac86f2348deb02f3880c71378e");
                var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null) Object.DontDestroyOnLoad(Object.Instantiate(prefab));
            }
#endif
            new GameObject("XR Hardware Monitor").AddComponent<XrHardwareMonitor>();
            DisableSimulators();
        }

        public static void DisableSimulators()
        {
            if (Application.isEditor && !HardwareRunning) return;
            foreach (var simulator in Object.FindObjectsByType<XRInteractionSimulator>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                simulator.gameObject.SetActive(false);
#pragma warning disable CS0618
            foreach (var simulator in Object.FindObjectsByType<XRDeviceSimulator>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                simulator.gameObject.SetActive(false);
#pragma warning restore CS0618
        }
    }
}
