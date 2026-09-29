#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace AvionesPapelVR.Editor
{
    // Explicit settings command only. Does not build, open, or save scenes.
    public static class QuestAppSetup
    {
        [MenuItem("Aviones de Papel VR/Configurar identidad e iconos Quest")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new System.InvalidOperationException("Sal de Play antes de configurar la aplicación.");
            XrPlayMode.Hardware();
            var quest = UnityEngine.XR.OpenXR.OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android)
                .GetFeature<UnityEngine.XR.OpenXR.Features.MetaQuestSupport.MetaQuestFeature>();
            var questSettings = new SerializedObject(quest);
            var devices = questSettings.FindProperty("targetDevices");
            for (int i = 0; i < devices.arraySize; i++)
            {
                var device = devices.GetArrayElementAtIndex(i);
                string id = device.FindPropertyRelative("manifestName").stringValue;
                // OpenXR adds missing built-in entries on enable: keep Quest 1 present but disabled.
                device.FindPropertyRelative("enabled").boolValue =
                    id == "quest2" || id == "cambria" || id == "eureka" || id == "quest3s";
            }
            questSettings.ApplyModifiedPropertiesWithoutUndo();
            PlayerSettings.companyName = "David";
            PlayerSettings.productName = "Aviones de Papel VR";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.david.avionespapel");
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel32;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel34;
            const string iconPath = "Assets/AvionesPapelVR/07_Textures/AppIcon.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(iconPath);
            importer.textureType = TextureImporterType.Default;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 512;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath);
            if (icon == null) throw new System.InvalidOperationException("AppIcon no se importó como Texture2D.");
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
            foreach (var kind in PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.Android))
            {
                var slots = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
                foreach (var slot in slots)
                    slot.SetTextures(Enumerable.Repeat(icon, slot.minLayerCount).ToArray());
                PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, kind, slots);
            }
            // Keep Unity branding; custom title/background do not require removing its logo.
            const string titlePath = "Assets/AvionesPapelVR/07_Textures/AppSplashTitle.png";
            var titleImporter = (TextureImporter)AssetImporter.GetAtPath(titlePath);
            titleImporter.textureType = TextureImporterType.Sprite;
            titleImporter.textureShape = TextureImporterShape.Texture2D;
            titleImporter.spriteImportMode = SpriteImportMode.Single;
            titleImporter.alphaIsTransparency = true;
            titleImporter.mipmapEnabled = false;
            titleImporter.textureCompression = TextureImporterCompression.Uncompressed;
            titleImporter.SaveAndReimport();
            var title = AssetDatabase.LoadAssetAtPath<Sprite>(titlePath);
            if (title == null) throw new System.InvalidOperationException("No se importó el título de inicio como Sprite.");
            PlayerSettings.SplashScreen.backgroundColor = new Color(0.035f, 0.065f, 0.1f, 1f);
            PlayerSettings.SplashScreen.logos = PlayerSettings.SplashScreen.logos
                .Where(l => l.logo != null && l.logo != title).Append(PlayerSettings.SplashScreenLogo.Create(2f, title)).ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("[QuestAppSetup] Identity, default/Android icons and splash title configured; Unity logo retained.");
        }

        public static void ApplyAndValidate()
        {
            Apply();
            GameplayValidation.RunBatch();
        }
    }
}
#endif
