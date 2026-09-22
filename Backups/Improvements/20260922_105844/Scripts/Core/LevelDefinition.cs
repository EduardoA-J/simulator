using UnityEngine;

namespace AvionesPapelVR
{
    public enum LevelTheme
    {
        School,
        Park,
        Office,
        MiniCity,
        Magic
    }

    [CreateAssetMenu(menuName = "Aviones Papel VR/Level Definition", fileName = "Level_")]
    public class LevelDefinition : ScriptableObject
    {
        public string displayName = "Nivel";
        public LevelTheme theme = LevelTheme.School;
        public float length = 80f;
        public int enemyCount = 8;
        public int obstacleCount = 6;
        public int starCount = 10;
        public int coinCount = 12;
        public int powerUpCount = 3;
        public float enemySpeed = 3f;
        public float windStrength = 0.5f;
        public int targetScore = 500;
        public Color fogColor = new Color(0.55f, 0.7f, 0.9f);
    }
}
