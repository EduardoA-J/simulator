using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

namespace AvionesPapelVR
{
    public static class XrSimulatorGuard
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() => SceneManager.sceneLoaded -= OnSceneLoaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            DisableSimulators();
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => DisableSimulators();

        public static void DisableSimulators()
        {
            // A simulated HMD is NOT evidence of physical hardware. Leave it alone
            // in the Editor; the official loader is already configured Editor-only.
            if (Application.isEditor && !XRSettings.isDeviceActive) return;
            if (!XRSettings.isDeviceActive && Application.platform != RuntimePlatform.Android) return;
            foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (mb == null) continue;
                string name = mb.GetType().Name;
                if (name == "XRInteractionSimulator" || name == "XRDeviceSimulator")
                    mb.gameObject.SetActive(false);
            }
        }
    }
}
