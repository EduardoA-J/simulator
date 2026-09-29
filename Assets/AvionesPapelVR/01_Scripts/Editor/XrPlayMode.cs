#if UNITY_EDITOR
using UnityEditor;
namespace AvionesPapelVR.Editor
{
    public static class XrPlayMode
    {
        [MenuItem("Aviones de Papel VR/Input VR/Simulador (sin visor)")]
        public static void Simulation()
        {
            RequireEditMode();
            KeyboardPlaySetup.DisableXrOnStartupForEditorTesting();
            EditorPrefs.SetBool(XrSimulatorGuard.DeviceSimulationPreference, false);
            EditorPrefs.SetBool(XrSimulatorGuard.SimulationPreference, true);
        }
        [MenuItem("Aviones de Papel VR/Input VR/XR Device Simulator (clasico)")]
        public static void DeviceSimulation()
        {
            RequireEditMode();
            KeyboardPlaySetup.DisableXrOnStartupForEditorTesting();
            EditorPrefs.SetBool(XrSimulatorGuard.DeviceSimulationPreference, true);
            EditorPrefs.SetBool(XrSimulatorGuard.SimulationPreference, true);
        }
        [MenuItem("Aviones de Papel VR/Input VR/Hardware Quest o Link")]
        public static void Hardware()
        {
            RequireEditMode();
            KeyboardPlaySetup.SetXrStartup("Standalone Settings", true);
            KeyboardPlaySetup.SetXrStartup("Android Settings", true);
            var settings = AssetDatabase.LoadAssetAtPath<UnityEngine.ScriptableObject>(
                "Assets/XRI/Settings/Resources/XRDeviceSimulatorSettings.asset");
            var serialized = new SerializedObject(settings);
            serialized.FindProperty("m_AutomaticallyInstantiateSimulatorPrefab").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var manager = UnityEditor.XR.Management.XRGeneralSettingsPerBuildTarget
                .XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone).Manager;
            foreach (var loader in new System.Collections.Generic.List<UnityEngine.XR.Management.XRLoader>(manager.activeLoaders))
                if (loader != null && loader.GetType().Name == "SimulationLoader") manager.TryRemoveLoader(loader);
            EditorUtility.SetDirty(manager);
            AssetDatabase.SaveAssets();
            EditorPrefs.SetBool(XrSimulatorGuard.SimulationPreference, false);
            EditorPrefs.SetBool(XrSimulatorGuard.DeviceSimulationPreference, false);
        }

        static void RequireEditMode()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new System.InvalidOperationException("Sal de Play antes de cambiar entre simulador y visor.");
        }
    }
}
#endif
