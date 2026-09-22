#if UNITY_EDITOR
using UnityEditor;
namespace AvionesPapelVR.Editor
{
    public static class XrPlayMode
    {
        [MenuItem("Aviones de Papel VR/Input VR/Simulador (sin visor)")]
        public static void Simulation() => EditorPrefs.SetBool(XrSimulatorGuard.SimulationPreference, true);
        [MenuItem("Aviones de Papel VR/Input VR/Hardware Quest o Link")]
        public static void Hardware() => EditorPrefs.SetBool(XrSimulatorGuard.SimulationPreference, false);
    }
}
#endif
