#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace AvionesPapelVR.Editor
{
    public static class PlayableVrGameBuilder
    {
        const string Root = "Assets/AvionesPapelVR";
        const string ScenePath = Root + "/Scenes/Game_VR_Oculus.unity";
        const string XrOriginGuid = "f6336ac4ac8b4d34bc5072418cdc62a0";
        const string XrManagerGuid = "83e4e6cca11330d4088d729ab4fc9d9f";

        [MenuItem("Aviones de Papel VR/Modo Oculus VR (mandos visibles)", priority = 5)]
        public static void BuildAndOpen()
        {
            BuilderSafety.BeforeSceneChange();
            EnableXrForOculus();
            if (!File.Exists(ScenePath)) Build();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorUtility.DisplayDialog(
                "Modo Oculus VR",
                "Escena lista: Game_VR_Oculus\n\n1) Conecta el casco Oculus/Meta\n2) Pulsa PLAY ▶\n3) Verás los mandos como en la escena por defecto\n4) Trigger = empezar; Grip = agarrar y soltar",
                "OK");
        }

        [MenuItem("Aviones de Papel VR/Jugar en Oculus Ahora", priority = 6)]
        public static void PlayNow()
        {
            BuilderSafety.BeforeSceneChange();
            if (!File.Exists(ScenePath)) BuildAndOpen();
            else
            {
                EnableXrForOculus();
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
            EditorApplication.isPlaying = true;
        }

        [MenuItem("Aviones de Papel VR/Regenerar escena VR (con backup)", priority = 22)]
        public static void RebuildScene() => Build();

        public static void BuildBatch()
        {
            EnableXrForOculus();
            Build();
            EditorApplication.Exit(0);
        }

        public static void EnableXrForOculus()
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

                // Prefer OpenXR first in Standalone providers
                var loaders = so.FindProperty("m_Loaders");
                if (loaders != null && loaders.isArray && loaders.arraySize >= 1)
                {
                    // leave as configured; OpenXR asset guid 17afc413...
                }
            }

            // Force Standalone init + OpenXR priority via direct YAML-friendly edit on known objects
            var all = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            foreach (var obj in all)
            {
                if (obj.name.Contains("Standalone Settings"))
                {
                    var so = new SerializedObject(obj);
                    var init = so.FindProperty("m_InitManagerOnStart");
                    if (init != null)
                    {
                        init.boolValue = true;
                        so.ApplyModifiedPropertiesWithoutUndo();
                        EditorUtility.SetDirty(obj);
                    }
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[AvionesPapelVR] XR Initialize on Startup = ON (Oculus/OpenXR)");
        }

        public static void Build()
        {
            BuilderSafety.BeforeRebuild();
            if (!File.Exists(Root + "/Scenes/Game_Playable.unity") && !AssetDatabase.IsValidFolder(Root + "/Data/Levels"))
                PlayableGameBuilder.Build();

            // Ensure levels/planes exist
            if (!AssetDatabase.IsValidFolder(Root + "/Data/Levels"))
                PlayableGameBuilder.Build();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Light
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.7f, 0.9f);

            // Floor like BasicScene
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.localScale = new Vector3(2f, 1f, 2f);
            var floorMat = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/M_FloorSchool.mat");
            if (floorMat != null) floor.GetComponent<MeshRenderer>().sharedMaterial = floorMat;

            // XR Interaction Manager
            var mgrGo = new GameObject("XR Interaction Manager");
            var mgrScript = AssetDatabase.LoadAssetAtPath<MonoScript>(
                AssetDatabase.GUIDToAssetPath(XrManagerGuid));
            // Add via known type
            var mgrType = System.Type.GetType("UnityEngine.XR.Interaction.Toolkit.XRInteractionManager, Unity.XR.Interaction.Toolkit");
            if (mgrType != null) mgrGo.AddComponent(mgrType);

            // XR Origin with controllers
            var originPath = AssetDatabase.GUIDToAssetPath(XrOriginGuid);
            var originPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(originPath);
            GameObject xrOrigin = null;
            if (originPrefab != null)
                xrOrigin = (GameObject)PrefabUtility.InstantiatePrefab(originPrefab);
            else
            {
                xrOrigin = new GameObject("XR Origin (XR Rig)");
                Debug.LogWarning("No se encontró XR Origin prefab; crea uno vacío.");
            }
            xrOrigin.name = "XR Origin (XR Rig)";
            xrOrigin.transform.position = new Vector3(0f, 0f, -0.4f);

            // Event System for XR UI
            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                // Input System UI module if available
                var moduleType = typeof(UnityEngine.XR.Interaction.Toolkit.UI.XRUIInputModule);
                if (moduleType != null) es.AddComponent(moduleType);
                else es.AddComponent<StandaloneInputModule>();
            }

            // Anchors in front of player (human scale table)
            var anchors = new GameObject("Anchors").transform;
            var select = new GameObject("SelectAnchor").transform;
            select.SetParent(anchors);
            select.position = new Vector3(0f, 0.9f, 0.85f);

            var launch = new GameObject("LaunchAnchor").transform;
            launch.SetParent(anchors);
            launch.position = new Vector3(0f, 1.2f, 1.2f);

            var flight = new GameObject("FlightStartAnchor").transform;
            flight.SetParent(anchors);
            flight.position = new Vector3(0f, 1.8f, 4f);

            var levelRoot = new GameObject("LevelRoot").transform;

            // Systems
            var systems = new GameObject("GameSystems");
            var gm = systems.AddComponent<GameManager>();
            RuntimeBalanceSetup.Assign(gm);
            var selector = systems.AddComponent<PlaneSelector>();
            var launchCtrl = systems.AddComponent<LaunchController>();
            var flightCtrl = systems.AddComponent<FlightController>();
            var levelRunner = systems.AddComponent<LevelRunner>();
            var hud = systems.AddComponent<GameHUD>();
            var follower = systems.AddComponent<VrRigFollower>();

            selector.tableAnchor = select;
            selector.enableVrGrab = true;
            selector.spacing = 0.38f;
            selector.displayScale = new Vector3(0.5f, 0.5f, 0.5f);

            levelRunner.deskPrefab = LoadPrefab("Prefabs/Props/SchoolDesk.prefab");
            levelRunner.treePrefab = LoadPrefab("Prefabs/Props/ParkTree.prefab");
            levelRunner.benchPrefab = LoadPrefab("Prefabs/Props/ParkBench.prefab");
            levelRunner.buildingPrefab = LoadPrefab("Prefabs/Props/MiniBuilding.prefab");
            levelRunner.crystalPrefab = LoadPrefab("Prefabs/Props/MagicCrystal.prefab");

            follower.xrOrigin = xrOrigin.transform;
            follower.CaptureLobbyPose();

            // Find XR camera
            Camera xrCam = xrOrigin.GetComponentInChildren<Camera>();
            hud.preferVrCanvas = true;
            hud.followCamera = xrCam != null ? xrCam.transform : null;

            gm.vrMode = true;
            gm.xrOrigin = xrOrigin.transform;
            gm.vrRigFollower = follower;
            gm.selectAnchor = select;
            gm.launchAnchor = launch;
            gm.flightStartAnchor = flight;
            gm.levelRoot = levelRoot;
            gm.gameCamera = xrCam;
            gm.planeSelector = selector;
            gm.launchController = launchCtrl;
            gm.flightController = flightCtrl;
            gm.levelRunner = levelRunner;
            gm.hud = hud;
            gm.planes = LoadPlanes();
            gm.levels = LoadLevels();
            WirePrefabs(gm);

            // Lobby table already spawned by selector; place a static visual table too
            var tablePrefab = LoadPrefab("Prefabs/Props/WoodTable.prefab");
            if (tablePrefab != null)
            {
                var table = (GameObject)PrefabUtility.InstantiatePrefab(tablePrefab);
                table.name = "Lobby_WoodTable";
                table.transform.position = new Vector3(0f, 0f, 0.85f);
            }

            // Help placard
            var help = new GameObject("VR_Help_Label");
            help.transform.position = new Vector3(0f, 1.7f, 1.5f);
            var label = help.AddComponent<ShowcaseLabel>();
            label.label = "Oculus: Trigger empezar | Grip agarrar avion | Stick elegir";

            EditorSceneManager.SaveScene(scene, ScenePath);
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == ScenePath);
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("[AvionesPapelVR] Game_VR_Oculus construido → " + ScenePath);
        }

        static void WirePrefabs(GameManager gm)
        {
            gm.bulletPrefab = LoadPrefab("Prefabs/Weapons/PaperBullet.prefab");
            gm.missilePrefab = LoadPrefab("Prefabs/Weapons/FoldMissile.prefab");
            gm.bombPrefab = LoadPrefab("Prefabs/Weapons/InkBomb.prefab");
            gm.enemyDronePrefab = LoadPrefab("Prefabs/Enemies/HostileDrone.prefab");
            gm.enemyBirdPrefab = LoadPrefab("Prefabs/Enemies/MechBird.prefab");
            gm.enemyFanPrefab = LoadPrefab("Prefabs/Enemies/WindFan.prefab");
            gm.obstacleStaticPrefab = LoadPrefab("Prefabs/Props/StaticObstacle.prefab");
            gm.obstacleMovingPrefab = LoadPrefab("Prefabs/Props/MovingObstacle.prefab");
            gm.starPrefab = LoadPrefab("Prefabs/Collectibles/StarCollectible.prefab");
            gm.coinPrefab = LoadPrefab("Prefabs/Collectibles/PaperCoin.prefab");
            gm.partPrefab = LoadPrefab("Prefabs/Collectibles/PlanePart.prefab");
            gm.turboPrefab = LoadPrefab("Prefabs/Collectibles/TurboOrb.prefab");
            gm.shieldPrefab = LoadPrefab("Prefabs/Collectibles/ShieldOrb.prefab");
            gm.triplePrefab = LoadPrefab("Prefabs/Collectibles/TripleShotOrb.prefab");
            gm.tablePrefab = LoadPrefab("Prefabs/Props/WoodTable.prefab");
            gm.sfxShoot = LoadClip("Audio/sfx_shoot.wav");
            gm.sfxPickup = LoadClip("Audio/sfx_pickup.wav");
            gm.sfxHit = LoadClip("Audio/sfx_collision.wav");
            gm.sfxLaunch = LoadClip("Audio/sfx_launch.wav");
            gm.sfxWind = LoadClip("Audio/sfx_wind.wav");
        }

        static List<PlaneDefinition> LoadPlanes()
        {
            var list = new List<PlaneDefinition>();
            foreach (var guid in AssetDatabase.FindAssets("t:PlaneDefinition", new[] { Root + "/Data" }))
            {
                var def = AssetDatabase.LoadAssetAtPath<PlaneDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (def != null) list.Add(def);
            }
            list.Sort((a, b) => string.CompareOrdinal(a.displayName, b.displayName));
            return list;
        }

        static List<LevelDefinition> LoadLevels()
        {
            var list = new List<LevelDefinition>();
            foreach (var guid in AssetDatabase.FindAssets("t:LevelDefinition", new[] { Root + "/Data/Levels" }))
            {
                var def = AssetDatabase.LoadAssetAtPath<LevelDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (def != null) list.Add(def);
            }
            list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            if (list.Count > 3) list.RemoveRange(3, list.Count - 3);
            return list;
        }

        static GameObject LoadPrefab(string rel) =>
            AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/{rel}");

        static AudioClip LoadClip(string rel) =>
            AssetDatabase.LoadAssetAtPath<AudioClip>($"{Root}/{rel}");
    }
}
#endif
