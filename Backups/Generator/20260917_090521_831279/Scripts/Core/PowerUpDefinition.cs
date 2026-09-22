using UnityEngine;

namespace AvionesPapelVR
{
    public enum PowerUpType
    {
        Turbo,
        Shield,
        TripleShot
    }

    [CreateAssetMenu(menuName = "Aviones Papel VR/PowerUp Definition", fileName = "PowerUp_")]
    public class PowerUpDefinition : ScriptableObject
    {
        public string displayName;
        public PowerUpType type;
        [Min(0f)] public float duration = 5f;
        [Min(0f)] public float boostAcceleration = 2f;
        public GameObject prefab;
        public Color glowColor = Color.yellow;
    }
}
