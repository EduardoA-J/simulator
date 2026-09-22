#if UNITY_EDITOR
using UnityEditor;
namespace AvionesPapelVR.Editor
{
    public static class RuntimeBalanceSetup
    {
        public static void Assign(GameManager manager)
        {
            manager.weapons.Clear(); manager.powerUps.Clear();
            foreach (var guid in AssetDatabase.FindAssets("t:WeaponDefinition", new[] { "Assets/AvionesPapelVR/Data" }))
                manager.weapons.Add(AssetDatabase.LoadAssetAtPath<WeaponDefinition>(AssetDatabase.GUIDToAssetPath(guid)));
            foreach (var guid in AssetDatabase.FindAssets("t:PowerUpDefinition", new[] { "Assets/AvionesPapelVR/Data" }))
                manager.powerUps.Add(AssetDatabase.LoadAssetAtPath<PowerUpDefinition>(AssetDatabase.GUIDToAssetPath(guid)));
        }
    }
}
#endif
