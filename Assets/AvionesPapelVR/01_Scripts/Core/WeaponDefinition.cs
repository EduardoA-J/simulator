using UnityEngine;

namespace AvionesPapelVR
{
    public enum WeaponType
    {
        PaperBullet,
        FoldMissile,
        InkBomb,
        OrigamiShield
    }

    [CreateAssetMenu(menuName = "Aviones Papel VR/Weapon Definition", fileName = "Weapon_")]
    public class WeaponDefinition : ScriptableObject
    {
        public string displayName;
        public WeaponType type;
        public float damage = 10f;
        public float speed = 12f;
        public float cooldown = 0.25f;
        public GameObject projectilePrefab;
    }
}
