#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace AvionesPapelVR.Editor
{
    public static class BuilderSafety
    {
        public static void BeforeSceneChange()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Deten Play Mode antes de cambiar escenas.");
            if (Application.isBatchMode)
            {
                for (int i = 0; i < SceneManager.sceneCount; i++)
                    if (SceneManager.GetSceneAt(i).isDirty)
                        throw new InvalidOperationException("Escena sin guardar; regeneracion cancelada.");
            }
            else if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                throw new OperationCanceledException("Cambio de escena cancelado.");
        }
        public static void BeforeRebuild()
        {
            BeforeSceneChange();
            string root = "Assets/AvionesPapelVR";
            string backup = "Backups/Builders/" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            if (!Directory.Exists(root)) return;
            foreach (string source in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
            {
                string destination = Path.Combine(backup, source.Substring(root.Length + 1));
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                File.Copy(source, destination, false);
            }
            Debug.Log("[AvionesPapelVR] Backup antes de regenerar: " + Path.GetFullPath(backup));
        }
    }
}
#endif
