#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEditor.XR.Management;
using UnityEngine.XR.OpenXR;

namespace AvionesPapelVR.Editor
{
    // Build the authored game, never regenerate or overwrite its scenes.
    public static class HeadsetBuild
    {
        const string Scene = "Assets/AvionesPapelVR/Scenes/Game_VR_Oculus.unity";

        [MenuItem("Aviones de Papel VR/Compilar visor/Quest - APK")]
        public static void Quest()
        {
            RequirePlatform(BuildTarget.Android);
            RequireOpenXr(BuildTargetGroup.Android);
            if (PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android) != ScriptingImplementation.IL2CPP ||
                PlayerSettings.Android.targetArchitectures != AndroidArchitecture.ARM64)
                throw new BuildFailedException("Quest requiere IL2CPP y arquitectura ARM64.");
            var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            if (settings == null || !settings.GetFeatures<UnityEngine.XR.OpenXR.Features.OpenXRFeature>()
                    .Any(f => f != null && f.enabled && f.GetType().Name == "MetaQuestFeature"))
                throw new BuildFailedException("Activa Meta Quest Support en OpenXR para Android.");
            if (!settings.GetFeatures<UnityEngine.XR.OpenXR.Features.OpenXRFeature>()
                    .Any(f => f != null && f.enabled && f.GetType().Name == "OculusTouchControllerProfile"))
                throw new BuildFailedException("Activa Oculus Touch Controller Profile en OpenXR para Android.");
            Build(BuildTarget.Android, "Builds/Quest/PaperFlight.apk");
        }

        [MenuItem("Aviones de Papel VR/Compilar visor/PC - Quest Link o Rift")]
        public static void Link()
        {
            RequirePlatform(BuildTarget.StandaloneWindows64);
            RequireOpenXr(BuildTargetGroup.Standalone);
            Build(BuildTarget.StandaloneWindows64, "Builds/Link/PaperFlight.exe");
        }

        static void RequirePlatform(BuildTarget target)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new BuildFailedException("Sal de Play antes de compilar.");
            var group = BuildPipeline.GetBuildTargetGroup(target);
            if (!BuildPipeline.IsBuildTargetSupported(group, target))
                throw new BuildFailedException(target == BuildTarget.Android
                    ? "Instala Android Build Support, Android SDK & NDK Tools y OpenJDK para esta version de Unity desde Unity Hub."
                    : "Instala Windows Build Support para esta version de Unity.");
            if (EditorUserBuildSettings.activeBuildTarget != target)
                throw new BuildFailedException("Cambia la plataforma a " + target +
                    " en File > Build Profiles y vuelve a compilar. En consola usa -buildTarget " +
                    (target == BuildTarget.Android ? "Android" : "Win64") + ".");
        }

        static void RequireOpenXr(BuildTargetGroup group)
        {
            var xr = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(group);
            if (xr == null || !xr.InitManagerOnStart || xr.Manager == null ||
                !xr.Manager.activeLoaders.Any(loader => loader is OpenXRLoader))
                throw new BuildFailedException("Activa OpenXR e Initialize XR on Startup para " + group + ".");
        }

        static void Build(BuildTarget target, string path)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(Scene) == null)
                throw new BuildFailedException("No se encuentra la escena de juego VR.");
            BuilderSafety.BeforeSceneChange();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            bool previousBundle = EditorUserBuildSettings.buildAppBundle;
            try
            {
                if (target == BuildTarget.Android) EditorUserBuildSettings.buildAppBundle = false;
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { Scene },
                    locationPathName = path,
                    target = target,
                    options = BuildOptions.None
                });
                if (report.summary.result != BuildResult.Succeeded)
                    throw new BuildFailedException("Build fallido: " + report.summary.result + "; errores: " + report.summary.totalErrors);
                Debug.Log("[HeadsetBuild] Generado: " + Path.GetFullPath(path));
            }
            finally { EditorUserBuildSettings.buildAppBundle = previousBundle; }
        }
    }
}
#endif
