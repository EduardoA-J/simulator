#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Management;

namespace AvionesPapelVR.Editor
{
    /// <summary>
    /// Prepara el proyecto para probar con teclado (sin casco).
    /// </summary>
    public static class KeyboardPlaySetup
    {
        const string ShowcasePath = "Assets/AvionesPapelVR/Scenes/Showcase_Assets.unity";
        const string KeyboardScenePath = "Assets/AvionesPapelVR/Scenes/Keyboard_Playtest.unity";

        [MenuItem("Aviones de Papel VR/Probar con Teclado (recomendado)", priority = 10)]
        public static void SetupAndOpenKeyboardPlay()
        {
            // Redirige al juego completo jugable
            PlayableGameBuilder.BuildAndOpen();
        }

        [MenuItem("Aviones de Papel VR/Modo Teclado: Desactivar XR al iniciar", priority = 11)]
        public static void DisableXrOnStartupForEditorTesting()
        {
            // Desktop test settings must never disable the Quest/Android loader.
            const string path = "Assets/XR/XRGeneralSettingsPerBuildTarget.asset";
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (obj == null || obj.name != "Standalone Settings") continue;
                var serialized = new SerializedObject(obj);
                var init = serialized.FindProperty("m_InitManagerOnStart");
                if (init == null) continue;
                init.boolValue = false;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(obj);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[AvionesPapelVR] XR desactivado solo en Standalone. Android conserva su configuracion.");
        }

        [MenuItem("Aviones de Papel VR/Modo VR: Reactivar XR al iniciar", priority = 12)]
        public static void EnableXrOnStartup()
        {
            var assetPath = "Assets/XR/XRGeneralSettingsPerBuildTarget.asset";
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(assetPath))
            {
                if (obj == null) continue;
                var so = new SerializedObject(obj);
                var prop = so.FindProperty("m_InitManagerOnStart");
                if (prop != null)
                {
                    prop.boolValue = true;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(obj);
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[AvionesPapelVR] Initialize XR on Startup = ON (modo casco).");
            EditorUtility.DisplayDialog("Modo VR", "Initialize XR on Startup reactivado.\nConecta el casco y abre SampleScene.", "OK");
        }

        static void EnsureKeyboardPlayScene()
        {
            if (!File.Exists(ShowcasePath))
            {
                AvionesPapelAssetBuilder.BuildAll(silent: true);
            }

            // Clonar showcase como base de playtest
            if (!File.Exists(KeyboardScenePath))
            {
                AssetDatabase.CopyAsset(ShowcasePath, KeyboardScenePath);
            }

            var scene = EditorSceneManager.OpenScene(KeyboardScenePath, OpenSceneMode.Single);

            // Quitar demos de vuelo automÃ¡tico conflictivos
            foreach (var demo in Object.FindObjectsByType<PlaneFlightDemo>(FindObjectsSortMode.None))
            {
                if (demo != null) demo.enabled = false;
            }
            foreach (var shooter in Object.FindObjectsByType<PaperShooter>(FindObjectsSortMode.None))
            {
                if (shooter != null) shooter.autoFireDemo = false;
            }

            var existing = Object.FindFirstObjectByType<KeyboardPlanePilot>();
            if (existing == null)
            {
                var planePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/AvionesPapelVR/Prefabs/Planes/ArrowSwift.prefab");
                GameObject plane;
                if (planePrefab != null)
                    plane = (GameObject)PrefabUtility.InstantiatePrefab(planePrefab);
                else
                {
                    plane = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    plane.name = "TempPlane";
                }

                plane.name = "Player_PaperPlane_Keyboard";
                plane.transform.position = new Vector3(0f, 1.6f, -2f);
                plane.transform.rotation = Quaternion.identity;
                plane.transform.localScale = Vector3.one * 0.7f;

                var flight = plane.GetComponent<PlaneFlightDemo>();
                if (flight != null) Object.DestroyImmediate(flight);

                var shooter = plane.GetComponent<PaperShooter>();
                if (shooter == null) shooter = plane.AddComponent<PaperShooter>();
                shooter.autoFireDemo = false;
                var bullet = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/AvionesPapelVR/Prefabs/Weapons/PaperBullet.prefab");
                shooter.projectilePrefab = bullet;
                shooter.shootClip = AssetDatabase.LoadAssetAtPath<AudioClip>(
                    "Assets/AvionesPapelVR/Audio/sfx_shoot.wav");

                var pilot = plane.AddComponent<KeyboardPlanePilot>();
                pilot.shooter = shooter;

                // CÃ¡mara dedicada
                var camGo = GameObject.Find("Preview Camera");
                Camera cam;
                if (camGo == null)
                {
                    camGo = new GameObject("Keyboard Camera");
                    cam = camGo.AddComponent<Camera>();
                    cam.tag = "MainCamera";
                }
                else
                {
                    cam = camGo.GetComponent<Camera>();
                    camGo.tag = "MainCamera";
                }
                camGo.transform.position = plane.transform.position + new Vector3(0f, 0.4f, -1.4f);
                pilot.cameraPivot = camGo.transform;

                // Luz si falta
                if (Object.FindFirstObjectByType<Light>() == null)
                {
                    var lightGo = new GameObject("Directional Light");
                    var light = lightGo.AddComponent<Light>();
                    light.type = LightType.Directional;
                    lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.Refresh();
        }
    }
}
#endif
