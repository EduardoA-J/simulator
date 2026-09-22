#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace AvionesPapelVR.Editor
{
    public static class PlayableGameBuilder
    {
        const string Root = "Assets/AvionesPapelVR";
        const string ScenePath = Root + "/Scenes/Game_Playable.unity";

        static readonly (string id, string name, LevelTheme theme, float len, int enemies, int obs, int stars, int coins, int pups, float enemySpd, float wind, Color fog)[] LevelSpecs =
        {
            ("01_Aula", "Aula Escolar", LevelTheme.School, 70f, 6, 4, 8, 10, 3, 2.5f, 0.3f, new Color(0.7f, 0.75f, 0.65f)),
            ("02_Parque", "Parque Urbano", LevelTheme.Park, 85f, 8, 5, 10, 12, 3, 3f, 0.55f, new Color(0.55f, 0.75f, 0.9f)),
            ("03_Oficina", "Oficina Futurista", LevelTheme.Office, 90f, 10, 6, 10, 12, 4, 3.4f, 0.4f, new Color(0.35f, 0.45f, 0.6f)),
            ("04_Ciudad", "Ciudad en Miniatura", LevelTheme.MiniCity, 100f, 12, 8, 12, 14, 4, 3.8f, 0.6f, new Color(0.45f, 0.5f, 0.55f)),
            ("05_Magico", "Bosque Mágico", LevelTheme.Magic, 110f, 14, 8, 14, 16, 5, 4.2f, 0.75f, new Color(0.25f, 0.15f, 0.4f)),
        };

        [MenuItem("Aviones de Papel VR/Construir Juego Jugable 100%", priority = 20)]
        public static void BuildAndOpen()
        {
            Build();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorUtility.DisplayDialog(
                "Aviones de Papel VR",
                "Juego listo.\n\nEscena: Game_Playable\nPulsa PLAY â–¶\n\nENTER menÃº â†’ elige aviÃ³n â†’ lanza â†’ vuela y dispara.",
                "OK");
        }

        [MenuItem("Aviones de Papel VR/Jugar Ahora (Play)", priority = 21)]
        public static void PlayNow()
        {
            BuilderSafety.BeforeSceneChange();
            if (!File.Exists(ScenePath)) Build();
            if (EditorApplication.isPlaying) return;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        public static void BuildBatch()
        {
            Build();
            EditorApplication.Exit(0);
        }

        public static void Build()
        {
            BuilderSafety.BeforeRebuild();
            EnsurePlayerTag();

            if (!AssetDatabase.IsValidFolder(Root + "/Data/Levels"))
                AssetDatabase.CreateFolder(Root + "/Data", "Levels");

            var levels = BuildLevelAssets();
            var planes = LoadAllPlanes();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Lighting
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            light.color = new Color(1f, 0.97f, 0.92f);
            lightGo.transform.rotation = Quaternion.Euler(45f, -25f, 0f);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.7f, 0.9f);
            RenderSettings.ambientEquatorColor = new Color(0.4f, 0.45f, 0.5f);
            RenderSettings.ambientGroundColor = new Color(0.2f, 0.18f, 0.15f);

            // Camera
            var camGo = new GameObject("Game Camera");
            var cam = camGo.AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.55f, 0.72f, 0.9f);
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 200f;
            camGo.AddComponent<AudioListener>();
            camGo.transform.position = new Vector3(0f, 1.6f, -2f);

            // Anchors
            var anchors = new GameObject("Anchors").transform;
            var select = new GameObject("SelectAnchor").transform;
            select.SetParent(anchors);
            select.position = new Vector3(0f, 0.85f, 0f);

            var launch = new GameObject("LaunchAnchor").transform;
            launch.SetParent(anchors);
            launch.position = new Vector3(0f, 1.3f, 2f);

            var flight = new GameObject("FlightStartAnchor").transform;
            flight.SetParent(anchors);
            flight.position = new Vector3(0f, 2f, 6f);

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

            selector.tableAnchor = select;
            levelRunner.deskPrefab = LoadPrefab("Prefabs/Props/SchoolDesk.prefab");
            levelRunner.treePrefab = LoadPrefab("Prefabs/Props/ParkTree.prefab");
            levelRunner.benchPrefab = LoadPrefab("Prefabs/Props/ParkBench.prefab");
            levelRunner.buildingPrefab = LoadPrefab("Prefabs/Props/MiniBuilding.prefab");
            levelRunner.crystalPrefab = LoadPrefab("Prefabs/Props/MagicCrystal.prefab");

            gm.selectAnchor = select;
            gm.launchAnchor = launch;
            gm.flightStartAnchor = flight;
            gm.levelRoot = levelRoot;
            gm.gameCamera = cam;
            gm.planeSelector = selector;
            gm.launchController = launchCtrl;
            gm.flightController = flightCtrl;
            gm.levelRunner = levelRunner;
            gm.hud = hud;
            gm.planes = planes;
            gm.levels = levels;

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

            // Mesa visible en zona de selecciÃ³n (ambiente)
            if (gm.tablePrefab != null)
            {
                var table = (GameObject)PrefabUtility.InstantiatePrefab(gm.tablePrefab);
                table.name = "Lobby_Table";
                table.transform.position = new Vector3(0f, 0f, 0f);
            }

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[AvionesPapelVR] Game_Playable construido â†’ " + ScenePath);
        }

        static List<LevelDefinition> BuildLevelAssets()
        {
            var list = new List<LevelDefinition>();
            foreach (var s in LevelSpecs)
            {
                var path = $"{Root}/Data/Levels/Level_{s.id}.asset";
                var asset = AssetDatabase.LoadAssetAtPath<LevelDefinition>(path);
                if (asset != null) { list.Add(asset); continue; }
                if (asset == null)
                {
                    asset = ScriptableObject.CreateInstance<LevelDefinition>();
                    AssetDatabase.CreateAsset(asset, path);
                }
                asset.displayName = s.name;
                asset.theme = s.theme;
                asset.length = s.len;
                asset.enemyCount = s.enemies;
                asset.obstacleCount = s.obs;
                asset.starCount = s.stars;
                asset.coinCount = s.coins;
                asset.powerUpCount = s.pups;
                asset.enemySpeed = s.enemySpd;
                asset.windStrength = s.wind;
                asset.fogColor = s.fog;
                asset.targetScore = 400 + s.enemies * 40;
                EditorUtility.SetDirty(asset);
                list.Add(asset);
            }
            return list;
        }

        static List<PlaneDefinition> LoadAllPlanes()
        {
            var list = new List<PlaneDefinition>();
            foreach (var guid in AssetDatabase.FindAssets("t:PlaneDefinition", new[] { Root + "/Data" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var def = AssetDatabase.LoadAssetAtPath<PlaneDefinition>(path);
                if (def != null) list.Add(def);
            }
            list.Sort((a, b) => string.CompareOrdinal(a.displayName, b.displayName));
            return list;
        }

        static GameObject LoadPrefab(string rel) =>
            AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/{rel}");

        static AudioClip LoadClip(string rel) =>
            AssetDatabase.LoadAssetAtPath<AudioClip>($"{Root}/{rel}");

        static void EnsurePlayerTag()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var tags = tagManager.FindProperty("tags");
            bool found = false;
            for (int i = 0; i < tags.arraySize; i++)
            {
                if (tags.GetArrayElementAtIndex(i).stringValue == "Player")
                {
                    found = true;
                    break;
                }
            }
            if (!found)
            {
                tags.InsertArrayElementAtIndex(tags.arraySize);
                tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = "Player";
                tagManager.ApplyModifiedProperties();
            }
        }

        static void AddToBuildSettings(string scenePath)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            // poner Game_Playable primero
            scenes.RemoveAll(s => s.path == scenePath);
            scenes.Insert(0, new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
#endif
