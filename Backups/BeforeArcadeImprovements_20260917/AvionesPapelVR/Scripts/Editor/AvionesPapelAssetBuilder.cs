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
    /// <summary>
    /// Genera materiales URP, prefabs, ScriptableObjects y la escena Showcase.
    /// Menú: Aviones de Papel VR → Generar Todos los Assets
    /// También se ejecuta una vez al importar/compilar si falta el marcador.
    /// </summary>
    public static class AvionesPapelAssetBuilder
    {
        const string Root = "Assets/AvionesPapelVR";
        const string MarkerPath = Root + "/Data/_assets_built.marker";
        const string ShowcaseScenePath = Root + "/Scenes/Showcase_Assets.unity";
        static readonly string UrpLitGuid = "933532a4fcc9baf4fa0491de14d08ed7";

        struct PlaneSpec
        {
            public string Id;
            public string Display;
            public float Speed, Glide, Stability, Wind, Fire;
            public PlaneRarity Rarity;
            public string Texture;
            public Color Accent;
            public bool Unlocked;
            public string Skin;
            public string Sticker;
        }

        static readonly PlaneSpec[] Planes =
        {
            new() { Id="DartClassic", Display="Dardo Clásico", Speed=0.55f, Glide=0.45f, Stability=0.6f, Wind=0.4f, Fire=0.4f, Rarity=PlaneRarity.Common, Texture="PaperWhite", Accent=new Color(0.95f,0.93f,0.88f), Unlocked=true, Skin="blanco", Sticker="none" },
            new() { Id="GliderLong", Display="Planeador Largo", Speed=0.35f, Glide=0.9f, Stability=0.7f, Wind=0.55f, Fire=0.25f, Rarity=PlaneRarity.Common, Texture="PaperBlue", Accent=new Color(0.55f,0.75f,0.95f), Unlocked=true, Skin="azul", Sticker="estrella" },
            new() { Id="ArrowSwift", Display="Flecha Veloz", Speed=0.95f, Glide=0.35f, Stability=0.35f, Wind=0.3f, Fire=0.5f, Rarity=PlaneRarity.Rare, Texture="PaperRed", Accent=new Color(0.9f,0.35f,0.3f), Unlocked=true, Skin="rojo", Sticker="rayo" },
            new() { Id="DeltaRacer", Display="Delta Racer", Speed=0.8f, Glide=0.5f, Stability=0.55f, Wind=0.45f, Fire=0.6f, Rarity=PlaneRarity.Rare, Texture="PaperYellow", Accent=new Color(0.95f,0.85f,0.3f), Unlocked=true, Skin="amarillo", Sticker="none" },
            new() { Id="CanardAce", Display="Canard Ace", Speed=0.65f, Glide=0.65f, Stability=0.8f, Wind=0.7f, Fire=0.55f, Rarity=PlaneRarity.Rare, Texture="PaperGreen", Accent=new Color(0.4f,0.8f,0.45f), Unlocked=false, Skin="verde", Sticker="asa" },
            new() { Id="HeavyBomber", Display="Bombardero Pesado", Speed=0.3f, Glide=0.4f, Stability=0.85f, Wind=0.8f, Fire=0.95f, Rarity=PlaneRarity.Epic, Texture="PaperPurple", Accent=new Color(0.65f,0.4f,0.85f), Unlocked=false, Skin="púrpura", Sticker="bomba" },
            new() { Id="ZigZag", Display="ZigZag", Speed=0.7f, Glide=0.55f, Stability=0.4f, Wind=0.35f, Fire=0.45f, Rarity=PlaneRarity.Common, Texture="PaperOrange", Accent=new Color(0.95f,0.55f,0.2f), Unlocked=true, Skin="naranja", Sticker="zigzag" },
            new() { Id="NightHawk", Display="Night Hawk", Speed=0.75f, Glide=0.6f, Stability=0.65f, Wind=0.6f, Fire=0.7f, Rarity=PlaneRarity.Epic, Texture="PaperBlack", Accent=new Color(0.2f,0.2f,0.25f), Unlocked=false, Skin="noche", Sticker="luna" },
            new() { Id="OrigamiPhoenix", Display="Fénix Origami", Speed=0.6f, Glide=0.75f, Stability=0.5f, Wind=0.5f, Fire=0.85f, Rarity=PlaneRarity.Legendary, Texture="PaperPink", Accent=new Color(0.95f,0.45f,0.55f), Unlocked=false, Skin="fénix", Sticker="alas" },
            new() { Id="StormCutter", Display="Cortatormentas", Speed=0.85f, Glide=0.45f, Stability=0.7f, Wind=0.95f, Fire=0.65f, Rarity=PlaneRarity.Epic, Texture="PaperCyan", Accent=new Color(0.35f,0.85f,0.9f), Unlocked=false, Skin="tormenta", Sticker="nube" },
            new() { Id="PaperFalcon", Display="Halcón de Papel", Speed=0.9f, Glide=0.55f, Stability=0.6f, Wind=0.55f, Fire=0.75f, Rarity=PlaneRarity.Legendary, Texture="PaperGold", Accent=new Color(0.95f,0.8f,0.25f), Unlocked=false, Skin="dorado", Sticker="corona" },
            new() { Id="CloudSkimmer", Display="Raspanubes", Speed=0.5f, Glide=0.95f, Stability=0.75f, Wind=0.65f, Fire=0.35f, Rarity=PlaneRarity.Rare, Texture="PaperWhite", Accent=new Color(0.9f,0.95f,1f), Unlocked=true, Skin="nube", Sticker="none" },
        };

        [InitializeOnLoadMethod]
        static void AutoBuildOnce()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (File.Exists(MarkerPath)) return;
                if (!AssetDatabase.IsValidFolder(Root)) return;
                try
                {
                    BuildAll(silent: true);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning("[AvionesPapelVR] Auto-build pendiente: " + e.Message);
                }
            };
        }

        [MenuItem("Aviones de Papel VR/Generar Todos los Assets", priority = 0)]
        public static void MenuBuildAll() => BuildAll(silent: false);

        /// <summary>Entry point para Unity batchmode: -executeMethod AvionesPapelVR.Editor.AvionesPapelAssetBuilder.BuildAllBatch</summary>
        public static void BuildAllBatch()
        {
            BuildAll(silent: true);
            EditorApplication.Exit(0);
        }

        [MenuItem("Aviones de Papel VR/Abrir Escena Showcase", priority = 1)]
        public static void OpenShowcase()
        {
            if (!File.Exists(ShowcaseScenePath))
                BuildAll(silent: true);
            var scene = EditorSceneManager.OpenScene(ShowcaseScenePath, OpenSceneMode.Single);
            FocusSceneView();
            Debug.Log("[AvionesPapelVR] Showcase abierta: " + scene.path);
        }

        public static void BuildAll(bool silent)
        {
            EnsureFolders();
            AssetDatabase.StartAssetEditing();
            try
            {
                BuildMaterials();
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var materials = LoadMaterialMap();
            var prefabs = BuildPrefabs(materials);
            BuildDataAssets(prefabs);
            BuildShowcaseScene(prefabs, materials);

            Directory.CreateDirectory(Path.GetDirectoryName(MarkerPath) ?? Root);
            File.WriteAllText(MarkerPath, System.DateTime.Now.ToString("o"));
            AssetDatabase.Refresh();

            if (!silent)
            {
                EditorUtility.DisplayDialog(
                    "Aviones de Papel VR",
                    "Assets generados.\nEscena: Assets/AvionesPapelVR/Scenes/Showcase_Assets.unity",
                    "Abrir Showcase");
                OpenShowcase();
            }
            else
            {
                Debug.Log("[AvionesPapelVR] Assets generados automáticamente → " + ShowcaseScenePath);
            }
        }

        static void EnsureFolders()
        {
            string[] folders =
            {
                "Assets/AvionesPapelVR",
                "Assets/AvionesPapelVR/Materials",
                "Assets/AvionesPapelVR/Prefabs",
                "Assets/AvionesPapelVR/Prefabs/Planes",
                "Assets/AvionesPapelVR/Prefabs/Weapons",
                "Assets/AvionesPapelVR/Prefabs/Enemies",
                "Assets/AvionesPapelVR/Prefabs/Props",
                "Assets/AvionesPapelVR/Prefabs/Collectibles",
                "Assets/AvionesPapelVR/Prefabs/Environments",
                "Assets/AvionesPapelVR/Data",
                "Assets/AvionesPapelVR/Scenes",
            };
            foreach (var f in folders)
            {
                if (AssetDatabase.IsValidFolder(f)) continue;
                var parent = Path.GetDirectoryName(f)?.Replace("\\", "/");
                var name = Path.GetFileName(f);
                if (!string.IsNullOrEmpty(parent))
                    AssetDatabase.CreateFolder(parent, name);
            }
        }

        static Shader GetUrpLit()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader != null) return shader;
            var path = AssetDatabase.GUIDToAssetPath(UrpLitGuid);
            return AssetDatabase.LoadAssetAtPath<Shader>(path);
        }

        static Material CreateMat(string name, string textureName, Color color, float metallic = 0f, float smooth = 0.35f, bool emission = false)
        {
            var path = $"{Root}/Materials/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            var mat = existing != null ? existing : new Material(GetUrpLit());
            mat.name = name;
            mat.SetColor("_BaseColor", color);
            mat.color = color;
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Smoothness", smooth);

            if (!string.IsNullOrEmpty(textureName))
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Root}/Textures/{textureName}.png");
                if (tex != null)
                {
                    mat.SetTexture("_BaseMap", tex);
                    mat.mainTexture = tex;
                }
            }

            if (emission)
            {
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                mat.SetColor("_EmissionColor", color * 1.5f);
            }

            if (existing == null)
                AssetDatabase.CreateAsset(mat, path);
            else
                EditorUtility.SetDirty(mat);

            return mat;
        }

        static void BuildMaterials()
        {
            CreateMat("M_WoodTable", "WoodTable", new Color(0.55f, 0.35f, 0.18f), 0f, 0.25f);
            CreateMat("M_PaperWhite", "PaperWhite", Color.white);
            CreateMat("M_PaperBlue", "PaperBlue", new Color(0.65f, 0.8f, 1f));
            CreateMat("M_PaperRed", "PaperRed", new Color(0.95f, 0.4f, 0.35f));
            CreateMat("M_PaperYellow", "PaperYellow", new Color(1f, 0.9f, 0.4f));
            CreateMat("M_PaperGreen", "PaperGreen", new Color(0.5f, 0.85f, 0.5f));
            CreateMat("M_PaperPurple", "PaperPurple", new Color(0.7f, 0.5f, 0.9f));
            CreateMat("M_PaperBlack", "PaperBlack", new Color(0.15f, 0.15f, 0.18f), 0.1f, 0.5f);
            CreateMat("M_PaperOrange", "PaperOrange", new Color(1f, 0.6f, 0.25f));
            CreateMat("M_PaperCyan", "PaperCyan", new Color(0.45f, 0.9f, 0.95f));
            CreateMat("M_PaperPink", "PaperPink", new Color(1f, 0.6f, 0.75f));
            CreateMat("M_PaperGold", "PaperGold", new Color(0.95f, 0.8f, 0.3f), 0.4f, 0.65f);
            CreateMat("M_MetalDrone", "MetalDrone", new Color(0.45f, 0.5f, 0.55f), 0.7f, 0.6f);
            CreateMat("M_Grass", "Grass", new Color(0.3f, 0.55f, 0.25f));
            CreateMat("M_StarrySky", "StarrySky", new Color(0.08f, 0.1f, 0.25f));
            CreateMat("M_Ink", "InkBlack", new Color(0.05f, 0.05f, 0.08f));
            CreateMat("M_Shield", "ShieldCyan", new Color(0.3f, 0.9f, 0.95f), 0f, 0.8f, true);
            CreateMat("M_Star", "StarYellow", new Color(1f, 0.9f, 0.2f), 0f, 0.7f, true);
            CreateMat("M_Coin", "CoinGold", new Color(0.95f, 0.75f, 0.2f), 0.5f, 0.7f);
            CreateMat("M_Turbo", "TurboOrange", new Color(1f, 0.45f, 0.1f), 0f, 0.6f, true);
            CreateMat("M_Triple", "TripleMagenta", new Color(0.9f, 0.2f, 0.7f), 0f, 0.6f, true);
            CreateMat("M_Chalk", "ChalkGreen", new Color(0.15f, 0.35f, 0.2f));
            CreateMat("M_Office", "FuturOffice", new Color(0.25f, 0.35f, 0.45f), 0.2f, 0.55f);
            CreateMat("M_Magic", "MagicPurple", new Color(0.5f, 0.25f, 0.75f), 0f, 0.7f, true);
            CreateMat("M_FloorSchool", null, new Color(0.75f, 0.65f, 0.45f));
            CreateMat("M_FloorPark", "Grass", new Color(0.35f, 0.6f, 0.3f));
            CreateMat("M_FloorCity", null, new Color(0.35f, 0.35f, 0.38f));
            CreateMat("M_FloorOffice", "FuturOffice", new Color(0.2f, 0.25f, 0.35f));
            CreateMat("M_FloorMagic", "MagicPurple", new Color(0.2f, 0.12f, 0.3f));
        }

        static Dictionary<string, Material> LoadMaterialMap()
        {
            var map = new Dictionary<string, Material>();
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { Root + "/Materials" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat != null) map[mat.name] = mat;
            }
            return map;
        }

        static Mesh LoadMesh(string relativeObjPath)
        {
            var path = $"{Root}/{relativeObjPath}";
            var all = AssetDatabase.LoadAllAssetsAtPath(path);
            foreach (var a in all)
            {
                if (a is Mesh mesh) return mesh;
            }
            // Fallback: sometimes the main asset is a GameObject with MeshFilter
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go != null)
            {
                var mf = go.GetComponentInChildren<MeshFilter>();
                if (mf != null) return mf.sharedMesh;
            }
            return null;
        }

        static GameObject CreateMeshPrefab(string prefabPath, string objRel, Material mat, string label,
            System.Action<GameObject> setup = null, Vector3? scale = null)
        {
            var mesh = LoadMesh(objRel);
            var root = new GameObject(Path.GetFileNameWithoutExtension(prefabPath));
            var child = new GameObject("Mesh");
            child.transform.SetParent(root.transform, false);
            var mf = child.AddComponent<MeshFilter>();
            var mr = child.AddComponent<MeshRenderer>();
            if (mesh != null) mf.sharedMesh = mesh;
            if (mat != null) mr.sharedMaterial = mat;
            if (scale.HasValue) root.transform.localScale = scale.Value;

            var col = child.AddComponent<MeshCollider>();
            if (mesh != null) col.sharedMesh = mesh;

            var tag = root.AddComponent<ShowcaseLabel>();
            tag.label = label;

            setup?.Invoke(root);
            if (root.GetComponent<Rigidbody>() != null || prefabPath.Contains("/Weapons/") || prefabPath.Contains("/Collectibles/") || prefabPath.Contains("/Enemies/"))
                col.convex = true;

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        static Dictionary<string, GameObject> BuildPrefabs(Dictionary<string, Material> mats)
        {
            var result = new Dictionary<string, GameObject>();

            Material Mat(string n) => mats.TryGetValue(n, out var m) ? m : null;

            foreach (var p in Planes)
            {
                var matName = "M_" + p.Texture;
                var prefab = CreateMeshPrefab(
                    $"{Root}/Prefabs/Planes/{p.Id}.prefab",
                    $"Models/Planes/{p.Id}.obj",
                    Mat(matName),
                    p.Display,
                    go =>
                    {
                        go.AddComponent<PlaneFlightDemo>();
                        var rb = go.AddComponent<Rigidbody>();
                        rb.isKinematic = true;
                        rb.useGravity = false;
                    },
                    Vector3.one * 1.2f);
                result["plane_" + p.Id] = prefab;
            }

            result["weapon_bullet"] = CreateMeshPrefab($"{Root}/Prefabs/Weapons/PaperBullet.prefab",
                "Models/Weapons/PaperBullet.obj", Mat("M_PaperWhite"), "Bala de papel",
                go =>
                {
                    var rb = go.AddComponent<Rigidbody>();
                    rb.useGravity = false;
                });
            result["weapon_missile"] = CreateMeshPrefab($"{Root}/Prefabs/Weapons/FoldMissile.prefab",
                "Models/Weapons/FoldMissile.obj", Mat("M_PaperRed"), "Misil plegable");
            result["weapon_bomb"] = CreateMeshPrefab($"{Root}/Prefabs/Weapons/InkBomb.prefab",
                "Models/Weapons/InkBomb.obj", Mat("M_Ink"), "Bomba de tinta");
            result["weapon_shield"] = CreateMeshPrefab($"{Root}/Prefabs/Weapons/OrigamiShield.prefab",
                "Models/Weapons/OrigamiShield.obj", Mat("M_Shield"), "Escudo origami",
                go => go.AddComponent<SpinBob>());

            result["enemy_drone"] = CreateMeshPrefab($"{Root}/Prefabs/Enemies/HostileDrone.prefab",
                "Models/Enemies/HostileDrone.obj", Mat("M_MetalDrone"), "Drone hostil",
                go => go.AddComponent<SpinBob>().spinSpeed = 40f);
            result["enemy_bird"] = CreateMeshPrefab($"{Root}/Prefabs/Enemies/MechBird.prefab",
                "Models/Enemies/MechBird.obj", Mat("M_MetalDrone"), "Pájaro mecánico");
            result["enemy_fan"] = CreateMeshPrefab($"{Root}/Prefabs/Enemies/WindFan.prefab",
                "Models/Enemies/WindFan.obj", Mat("M_MetalDrone"), "Ventilador de aire",
                go =>
                {
                    go.AddComponent<WindFanForce>();
                    var spin = go.AddComponent<SpinBob>();
                    spin.bobAmplitude = 0f;
                    spin.spinSpeed = 180f;
                });

            result["prop_table"] = CreateMeshPrefab($"{Root}/Prefabs/Props/WoodTable.prefab",
                "Models/Props/WoodTable.obj", Mat("M_WoodTable"), "Mesa VR");
            result["prop_desk"] = CreateMeshPrefab($"{Root}/Prefabs/Props/SchoolDesk.prefab",
                "Models/Props/SchoolDesk.obj", Mat("M_WoodTable"), "Pupitre");
            result["prop_board"] = CreateMeshPrefab($"{Root}/Prefabs/Props/Chalkboard.prefab",
                "Models/Props/Chalkboard.obj", Mat("M_Chalk"), "Pizarra");
            result["prop_tree"] = CreateMeshPrefab($"{Root}/Prefabs/Props/ParkTree.prefab",
                "Models/Props/ParkTree.obj", Mat("M_Grass"), "Árbol");
            result["prop_bench"] = CreateMeshPrefab($"{Root}/Prefabs/Props/ParkBench.prefab",
                "Models/Props/ParkBench.obj", Mat("M_WoodTable"), "Banco");
            result["prop_fountain"] = CreateMeshPrefab($"{Root}/Prefabs/Props/Fountain.prefab",
                "Models/Props/Fountain.obj", Mat("M_Office"), "Fuente");
            result["prop_building"] = CreateMeshPrefab($"{Root}/Prefabs/Props/MiniBuilding.prefab",
                "Models/Props/MiniBuilding.obj", Mat("M_FloorCity"), "Edificio mini");
            result["prop_crystal"] = CreateMeshPrefab($"{Root}/Prefabs/Props/MagicCrystal.prefab",
                "Models/Props/MagicCrystal.obj", Mat("M_Magic"), "Cristal mágico",
                go => go.AddComponent<SpinBob>());
            result["prop_static"] = CreateMeshPrefab($"{Root}/Prefabs/Props/StaticObstacle.prefab",
                "Models/Props/StaticObstacle.obj", Mat("M_FloorCity"), "Obstáculo estático");
            result["prop_moving"] = CreateMeshPrefab($"{Root}/Prefabs/Props/MovingObstacle.prefab",
                "Models/Props/MovingObstacle.obj", Mat("M_PaperOrange"), "Obstáculo móvil",
                go => go.AddComponent<MovingObstacleMotion>());

            result["col_star"] = CreateMeshPrefab($"{Root}/Prefabs/Collectibles/StarCollectible.prefab",
                "Models/Collectibles/StarCollectible.obj", Mat("M_Star"), "Estrella",
                go => go.AddComponent<SpinBob>());
            result["col_coin"] = CreateMeshPrefab($"{Root}/Prefabs/Collectibles/PaperCoin.prefab",
                "Models/Collectibles/PaperCoin.obj", Mat("M_Coin"), "Moneda de papel",
                go => go.AddComponent<SpinBob>());
            result["col_part"] = CreateMeshPrefab($"{Root}/Prefabs/Collectibles/PlanePart.prefab",
                "Models/Collectibles/PlanePart.obj", Mat("M_PaperBlue"), "Pieza de avión",
                go => go.AddComponent<SpinBob>());
            result["pu_turbo"] = CreateMeshPrefab($"{Root}/Prefabs/Collectibles/TurboOrb.prefab",
                "Models/Collectibles/TurboOrb.obj", Mat("M_Turbo"), "Power-up Turbo",
                go => go.AddComponent<SpinBob>());
            result["pu_shield"] = CreateMeshPrefab($"{Root}/Prefabs/Collectibles/ShieldOrb.prefab",
                "Models/Collectibles/ShieldOrb.obj", Mat("M_Shield"), "Power-up Escudo",
                go => go.AddComponent<SpinBob>());
            result["pu_triple"] = CreateMeshPrefab($"{Root}/Prefabs/Collectibles/TripleShotOrb.prefab",
                "Models/Collectibles/TripleShotOrb.obj", Mat("M_Triple"), "Power-up Triple",
                go => go.AddComponent<SpinBob>());

            result["env_floor"] = CreateMeshPrefab($"{Root}/Prefabs/Environments/FloorTile.prefab",
                "Models/Environments/FloorTile.obj", Mat("M_FloorSchool"), "FloorTile");
            result["env_wall"] = CreateMeshPrefab($"{Root}/Prefabs/Environments/WallTile.prefab",
                "Models/Environments/WallTile.obj", Mat("M_FloorSchool"), "WallTile");

            return result;
        }

        static void BuildDataAssets(Dictionary<string, GameObject> prefabs)
        {
            foreach (var p in Planes)
            {
                var path = $"{Root}/Data/Plane_{p.Id}.asset";
                var asset = AssetDatabase.LoadAssetAtPath<PlaneDefinition>(path);
                if (asset == null)
                {
                    asset = ScriptableObject.CreateInstance<PlaneDefinition>();
                    AssetDatabase.CreateAsset(asset, path);
                }
                asset.displayName = p.Display;
                asset.rarity = p.Rarity;
                asset.speed = p.Speed;
                asset.glide = p.Glide;
                asset.stability = p.Stability;
                asset.windResistance = p.Wind;
                asset.firePower = p.Fire;
                asset.unlockedByDefault = p.Unlocked;
                asset.skinId = p.Skin;
                asset.stickerId = p.Sticker;
                asset.accentColor = p.Accent;
                if (prefabs.TryGetValue("plane_" + p.Id, out var prefab))
                    asset.prefab = prefab;
                EditorUtility.SetDirty(asset);
            }

            CreateWeaponData("Weapon_PaperBullet", "Bala de papel", WeaponType.PaperBullet, 8f, 16f, 0.2f, prefabs, "weapon_bullet");
            CreateWeaponData("Weapon_FoldMissile", "Misil plegable", WeaponType.FoldMissile, 25f, 10f, 0.8f, prefabs, "weapon_missile");
            CreateWeaponData("Weapon_InkBomb", "Bomba de tinta", WeaponType.InkBomb, 40f, 6f, 1.2f, prefabs, "weapon_bomb");
            CreateWeaponData("Weapon_OrigamiShield", "Escudo origami", WeaponType.OrigamiShield, 0f, 0f, 5f, prefabs, "weapon_shield");

            CreatePowerUpData("PowerUp_Turbo", "Turbo", PowerUpType.Turbo, 4f, prefabs, "pu_turbo", new Color(1f, 0.5f, 0.1f));
            CreatePowerUpData("PowerUp_Shield", "Escudo", PowerUpType.Shield, 6f, prefabs, "pu_shield", new Color(0.3f, 0.9f, 1f));
            CreatePowerUpData("PowerUp_TripleShot", "Disparo Triple", PowerUpType.TripleShot, 5f, prefabs, "pu_triple", new Color(0.9f, 0.2f, 0.7f));
        }

        static void CreateWeaponData(string file, string name, WeaponType type, float dmg, float spd, float cd,
            Dictionary<string, GameObject> prefabs, string key)
        {
            var path = $"{Root}/Data/{file}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<WeaponDefinition>();
                AssetDatabase.CreateAsset(asset, path);
            }
            asset.displayName = name;
            asset.type = type;
            asset.damage = dmg;
            asset.speed = spd;
            asset.cooldown = cd;
            if (prefabs.TryGetValue(key, out var p)) asset.projectilePrefab = p;
            EditorUtility.SetDirty(asset);
        }

        static void CreatePowerUpData(string file, string name, PowerUpType type, float dur,
            Dictionary<string, GameObject> prefabs, string key, Color glow)
        {
            var path = $"{Root}/Data/{file}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<PowerUpDefinition>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<PowerUpDefinition>();
                AssetDatabase.CreateAsset(asset, path);
            }
            asset.displayName = name;
            asset.type = type;
            asset.duration = dur;
            asset.glowColor = glow;
            if (prefabs.TryGetValue(key, out var p)) asset.prefab = p;
            EditorUtility.SetDirty(asset);
        }

        static GameObject Spawn(Dictionary<string, GameObject> prefabs, string key, Vector3 pos, Transform parent, Vector3? euler = null, float scale = 1f)
        {
            if (!prefabs.TryGetValue(key, out var prefab) || prefab == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.SetParent(parent, true);
            go.transform.position = pos;
            if (euler.HasValue) go.transform.rotation = Quaternion.Euler(euler.Value);
            go.transform.localScale = Vector3.one * scale;
            return go;
        }

        static void BuildShowcaseScene(Dictionary<string, GameObject> prefabs, Dictionary<string, Material> mats)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "Showcase_Assets";

            var root = new GameObject("AvionesPapelVR_Showcase");
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.color = new Color(1f, 0.96f, 0.9f);
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var camGo = new GameObject("Preview Camera");
            var cam = camGo.AddComponent<Camera>();
            cam.tag = "MainCamera";
            camGo.transform.position = new Vector3(0f, 2.2f, -6f);
            camGo.transform.LookAt(new Vector3(0f, 1f, 4f));
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.backgroundColor = new Color(0.55f, 0.75f, 0.95f);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.7f, 0.9f);
            RenderSettings.ambientEquatorColor = new Color(0.45f, 0.5f, 0.55f);
            RenderSettings.ambientGroundColor = new Color(0.25f, 0.22f, 0.18f);

            // --- Zona 1: Mesa de selección ---
            var zoneSelect = new GameObject("01_Seleccion_Mesa").transform;
            zoneSelect.SetParent(root.transform);
            Spawn(prefabs, "prop_table", new Vector3(0f, 0f, 0f), zoneSelect);
            for (int i = 0; i < Planes.Length; i++)
            {
                float x = -1.0f + (i % 6) * 0.4f;
                float z = -0.15f + (i / 6) * 0.35f;
                var plane = Spawn(prefabs, "plane_" + Planes[i].Id, new Vector3(x, 0.82f, z), zoneSelect, new Vector3(0f, 180f, 0f), 0.55f);
                if (plane != null)
                {
                    var demo = plane.GetComponent<PlaneFlightDemo>();
                    if (demo != null) demo.enabled = false;
                }
            }

            // --- Zona 2: Armas ---
            var zoneWeapons = new GameObject("02_Armas").transform;
            zoneWeapons.SetParent(root.transform);
            zoneWeapons.position = new Vector3(4f, 0f, 0f);
            Spawn(prefabs, "weapon_bullet", new Vector3(4f, 1.2f, 0f), zoneWeapons, scale: 3f);
            Spawn(prefabs, "weapon_missile", new Vector3(4.5f, 1.2f, 0f), zoneWeapons, scale: 2f);
            Spawn(prefabs, "weapon_bomb", new Vector3(5f, 1.2f, 0f), zoneWeapons, scale: 2f);
            Spawn(prefabs, "weapon_shield", new Vector3(5.5f, 1.2f, 0f), zoneWeapons, scale: 1.5f);

            // --- Zona 3: Enemigos / obstáculos ---
            var zoneEnemy = new GameObject("03_Enemigos_Obstaculos").transform;
            zoneEnemy.SetParent(root.transform);
            zoneEnemy.position = new Vector3(8f, 0f, 0f);
            Spawn(prefabs, "enemy_drone", new Vector3(8f, 1.5f, 0f), zoneEnemy);
            Spawn(prefabs, "enemy_bird", new Vector3(9f, 1.3f, 0f), zoneEnemy);
            Spawn(prefabs, "enemy_fan", new Vector3(10f, 1f, 0f), zoneEnemy);
            Spawn(prefabs, "prop_static", new Vector3(8.5f, 0f, 1.5f), zoneEnemy, scale: 0.6f);
            Spawn(prefabs, "prop_moving", new Vector3(9.5f, 1.2f, 1.5f), zoneEnemy);

            // --- Zona 4: Coleccionables ---
            var zoneLoot = new GameObject("04_Coleccionables_PowerUps").transform;
            zoneLoot.SetParent(root.transform);
            zoneLoot.position = new Vector3(0f, 0f, 4f);
            Spawn(prefabs, "col_star", new Vector3(-1f, 1.2f, 4f), zoneLoot, scale: 2f);
            Spawn(prefabs, "col_coin", new Vector3(-0.3f, 1.2f, 4f), zoneLoot, scale: 2f);
            Spawn(prefabs, "col_part", new Vector3(0.4f, 1.2f, 4f), zoneLoot, scale: 2f);
            Spawn(prefabs, "pu_turbo", new Vector3(1.1f, 1.2f, 4f), zoneLoot, scale: 2f);
            Spawn(prefabs, "pu_shield", new Vector3(1.8f, 1.2f, 4f), zoneLoot, scale: 2f);
            Spawn(prefabs, "pu_triple", new Vector3(2.5f, 1.2f, 4f), zoneLoot, scale: 2f);

            // --- Zona 5: Escenarios modulares (mini dioramas) ---
            BuildSchoolDiorama(prefabs, mats, root.transform, new Vector3(-8f, 0f, 0f));
            BuildParkDiorama(prefabs, mats, root.transform, new Vector3(-8f, 0f, 8f));
            BuildOfficeDiorama(prefabs, mats, root.transform, new Vector3(0f, 0f, 10f));
            BuildCityDiorama(prefabs, mats, root.transform, new Vector3(8f, 0f, 10f));
            BuildMagicDiorama(prefabs, mats, root.transform, new Vector3(12f, 0f, 0f));

            // --- Zona 6: Demo vuelo + disparo ---
            var zoneFlight = new GameObject("06_Demo_Vuelo").transform;
            zoneFlight.SetParent(root.transform);
            var flyer = Spawn(prefabs, "plane_ArrowSwift", new Vector3(-2f, 2f, -3f), zoneFlight, scale: 0.8f);
            if (flyer != null)
            {
                var demo = flyer.GetComponent<PlaneFlightDemo>();
                if (demo != null)
                {
                    demo.loopPath = true;
                    demo.forwardSpeed = 0.8f;
                    demo.loopRadius = 1.2f;
                    demo.enabled = true;
                }
                var shooter = flyer.AddComponent<PaperShooter>();
                shooter.autoFireDemo = true;
                shooter.fireRate = 1.5f;
                if (prefabs.TryGetValue("weapon_bullet", out var bullet))
                    shooter.projectilePrefab = bullet;
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Root}/Audio/sfx_shoot.wav");
                shooter.shootClip = clip;
            }

            // Audio ambience placeholder
            var audioGo = new GameObject("Audio_Wind");
            audioGo.transform.SetParent(root.transform);
            audioGo.transform.position = new Vector3(0f, 2f, 0f);
            var src = audioGo.AddComponent<AudioSource>();
            src.clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Root}/Audio/sfx_wind.wav");
            src.loop = true;
            src.spatialBlend = 1f;
            src.volume = 0.15f;
            src.playOnAwake = true;

            // Floor reference grid
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground_Reference";
            ground.transform.SetParent(root.transform);
            ground.transform.position = new Vector3(2f, 0f, 4f);
            ground.transform.localScale = new Vector3(3f, 1f, 3f);
            if (mats.TryGetValue("M_FloorSchool", out var floorMat))
                ground.GetComponent<MeshRenderer>().sharedMaterial = floorMat;

            EditorSceneManager.SaveScene(scene, ShowcaseScenePath);
            AddSceneToBuildSettings(ShowcaseScenePath);
        }

        static void PlaceFloor(Dictionary<string, GameObject> prefabs, Dictionary<string, Material> mats,
            string matName, Transform parent, Vector3 origin, int w, int d)
        {
            for (int x = 0; x < w; x++)
            for (int z = 0; z < d; z++)
            {
                var tile = Spawn(prefabs, "env_floor", origin + new Vector3(x * 2f, 0f, z * 2f), parent);
                if (tile != null && mats.TryGetValue(matName, out var m))
                {
                    foreach (var mr in tile.GetComponentsInChildren<MeshRenderer>())
                        mr.sharedMaterial = m;
                }
            }
        }

        static void BuildSchoolDiorama(Dictionary<string, GameObject> prefabs, Dictionary<string, Material> mats, Transform root, Vector3 origin)
        {
            var zone = new GameObject("05A_Escenario_Aula").transform;
            zone.SetParent(root);
            zone.position = origin;
            PlaceFloor(prefabs, mats, "M_FloorSchool", zone, origin, 3, 3);
            Spawn(prefabs, "prop_board", origin + new Vector3(2f, 0f, 4.5f), zone);
            Spawn(prefabs, "prop_desk", origin + new Vector3(1f, 0f, 1.5f), zone, scale: 0.9f);
            Spawn(prefabs, "prop_desk", origin + new Vector3(3f, 0f, 1.5f), zone, scale: 0.9f);
            Spawn(prefabs, "prop_desk", origin + new Vector3(1f, 0f, 3f), zone, scale: 0.9f);
            Spawn(prefabs, "prop_desk", origin + new Vector3(3f, 0f, 3f), zone, scale: 0.9f);
            Spawn(prefabs, "env_wall", origin + new Vector3(2f, 0f, 5.2f), zone);
        }

        static void BuildParkDiorama(Dictionary<string, GameObject> prefabs, Dictionary<string, Material> mats, Transform root, Vector3 origin)
        {
            var zone = new GameObject("05B_Escenario_Parque").transform;
            zone.SetParent(root);
            zone.position = origin;
            PlaceFloor(prefabs, mats, "M_FloorPark", zone, origin, 3, 3);
            Spawn(prefabs, "prop_tree", origin + new Vector3(0.5f, 0f, 1f), zone);
            Spawn(prefabs, "prop_tree", origin + new Vector3(4f, 0f, 3.5f), zone, scale: 1.2f);
            Spawn(prefabs, "prop_bench", origin + new Vector3(2f, 0f, 2f), zone);
            Spawn(prefabs, "prop_fountain", origin + new Vector3(3f, 0f, 4f), zone, scale: 0.7f);
        }

        static void BuildOfficeDiorama(Dictionary<string, GameObject> prefabs, Dictionary<string, Material> mats, Transform root, Vector3 origin)
        {
            var zone = new GameObject("05C_Escenario_Oficina").transform;
            zone.SetParent(root);
            zone.position = origin;
            PlaceFloor(prefabs, mats, "M_FloorOffice", zone, origin, 3, 2);
            Spawn(prefabs, "enemy_drone", origin + new Vector3(1f, 1.8f, 1f), zone, scale: 0.8f);
            Spawn(prefabs, "enemy_drone", origin + new Vector3(3f, 2.2f, 2f), zone, scale: 0.8f);
            Spawn(prefabs, "prop_static", origin + new Vector3(2f, 0f, 1.5f), zone, scale: 0.5f);
            Spawn(prefabs, "env_wall", origin + new Vector3(2f, 0f, 3.5f), zone);
            if (mats.TryGetValue("M_Office", out var om))
            {
                // tint wall
            }
        }

        static void BuildCityDiorama(Dictionary<string, GameObject> prefabs, Dictionary<string, Material> mats, Transform root, Vector3 origin)
        {
            var zone = new GameObject("05D_Escenario_CiudadMini").transform;
            zone.SetParent(root);
            zone.position = origin;
            PlaceFloor(prefabs, mats, "M_FloorCity", zone, origin, 3, 3);
            Spawn(prefabs, "prop_building", origin + new Vector3(0.8f, 0f, 1f), zone, scale: 0.7f);
            Spawn(prefabs, "prop_building", origin + new Vector3(2.5f, 0f, 1.2f), zone, scale: 1f);
            Spawn(prefabs, "prop_building", origin + new Vector3(4f, 0f, 3f), zone, scale: 0.85f);
            Spawn(prefabs, "prop_building", origin + new Vector3(1.5f, 0f, 3.5f), zone, scale: 1.1f);
            Spawn(prefabs, "prop_moving", origin + new Vector3(3f, 0.8f, 2f), zone, scale: 0.6f);
        }

        static void BuildMagicDiorama(Dictionary<string, GameObject> prefabs, Dictionary<string, Material> mats, Transform root, Vector3 origin)
        {
            var zone = new GameObject("05E_Escenario_Fantastico").transform;
            zone.SetParent(root);
            zone.position = origin;
            PlaceFloor(prefabs, mats, "M_FloorMagic", zone, origin, 3, 3);
            Spawn(prefabs, "prop_crystal", origin + new Vector3(1f, 0.5f, 1.5f), zone, scale: 1.5f);
            Spawn(prefabs, "prop_crystal", origin + new Vector3(3.5f, 0.5f, 3f), zone, scale: 2f);
            Spawn(prefabs, "prop_tree", origin + new Vector3(4f, 0f, 1f), zone);
            if (mats.TryGetValue("M_Magic", out var mm))
            {
                var tree = zone.Find("ParkTree");
            }
            Spawn(prefabs, "col_star", origin + new Vector3(2f, 2f, 2.5f), zone, scale: 2.5f);
            var night = new GameObject("NightTint");
            night.transform.SetParent(zone);
            var pl = night.AddComponent<Light>();
            pl.type = LightType.Point;
            pl.color = new Color(0.6f, 0.35f, 1f);
            pl.intensity = 2.5f;
            pl.range = 8f;
            night.transform.position = origin + new Vector3(2f, 3f, 2f);
        }

        static void AddSceneToBuildSettings(string scenePath)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(s => s.path == scenePath)) return;
            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        static void FocusSceneView()
        {
            var view = SceneView.lastActiveSceneView;
            if (view == null) return;
            view.pivot = new Vector3(2f, 1.5f, 3f);
            view.size = 12f;
            view.Repaint();
        }
    }
}
#endif
