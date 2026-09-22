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
        public float duration = 5f;
        public GameObject prefab;
        public Color glowColor = Color.yellow;
    }
}
