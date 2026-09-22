using UnityEngine;
namespace AvionesPapelVR
{
    public class XrHardwareMonitor : MonoBehaviour
    {
        void Awake() { DontDestroyOnLoad(gameObject); InvokeRepeating(nameof(Check), 0.25f, 0.5f); }
        void Check() => XrSimulatorGuard.DisableSimulators();
    }
}
