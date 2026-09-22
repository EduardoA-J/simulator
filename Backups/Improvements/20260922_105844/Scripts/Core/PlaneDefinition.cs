using UnityEngine;

namespace AvionesPapelVR
{
    public enum PlaneRarity
    {
        Common,
        Rare,
        Epic,
        Legendary
    }

    [CreateAssetMenu(menuName = "Aviones Papel VR/Plane Definition", fileName = "Plane_")]
    public class PlaneDefinition : ScriptableObject
    {
        public string displayName;
        public PlaneRarity rarity = PlaneRarity.Common;
        [Range(0f, 1f)] public float speed = 0.5f;
        [Range(0f, 1f)] public float glide = 0.5f;
        [Range(0f, 1f)] public float stability = 0.5f;
        [Range(0f, 1f)] public float windResistance = 0.5f;
        [Range(0f, 1f)] public float firePower = 0.5f;
        public bool unlockedByDefault = true;
        public string skinId = "default";
        public string stickerId = "none";
        public GameObject prefab;
        public Color accentColor = Color.white;

        [Header("Planeo arcade")]
        [Min(1f)] public float maxSpeed = 24f;
        [Min(0.1f)] public float stallSpeed = 2f;
        [Min(1f)] public float pitchSensitivity = 45f;
        [Min(1f)] public float rollSensitivity = 60f;
        [Min(1f)] public float turnSpeed = 45f;
        [Range(0.01f, 1f)] public float gravityMultiplier = 0.2f;
        [Range(0f, 0.1f)] public float drag = 0.006f;
        [Range(0f, 1f)] public float lift = 0.92f;
        [Range(1f, 3f)] public float boostMultiplier = 1.5f;
        [Min(0)] public int unlockScore = 500;
        public string persistentId;

        // Existing normalized attributes remain meaningful for every saved plane.
        public float EffectiveDrag => drag * Mathf.Lerp(1.5f, 0.65f, glide);
        public float EffectiveLift => Mathf.Min(0.98f, lift * Mathf.Lerp(0.85f, 1.05f, glide));
        public float EffectiveMaxSpeed => maxSpeed * Mathf.Lerp(0.75f, 1f, speed);
        public string SaveId => string.IsNullOrEmpty(persistentId) ? name : persistentId;
    }
}
