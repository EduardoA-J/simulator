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
        [Header("Ruta y dificultad")]
        public string difficulty = "INICIACIÓN";
        [Min(0f)] public float curveAmplitude;
        [Min(0f)] public float curveCycles;
        [Min(0f)] public float climbAmplitude;
        [Min(6f)] public float flightSpeedLimit = 24f;
        [Min(60f)] public float timeLimit = 90f;
        public bool HasCurves => curveAmplitude > 0f && curveCycles > 0f;

        // Parameter z is forward course distance. The first 18 m are a gentle entry.
        public Vector3 PathPoint(float z)
        {
            float phase = Mathf.Max(0f, z - 18f) / Mathf.Max(1f, length - 18f) * Mathf.PI * 2f * curveCycles;
            float wave = HasCurves ? 1f - Mathf.Cos(phase) : 0f;
            return new Vector3(curveAmplitude * wave, 1.8f + climbAmplitude * wave, z);
        }

        public Quaternion PathRotation(float z)
        {
            return Quaternion.LookRotation((PathPoint(z + 0.1f) - PathPoint(z - 0.1f)).normalized, Vector3.up);
        }

        public Quaternion GroundRotation(float z)
        {
            Vector3 direction = PathRotation(z) * Vector3.forward;
            return Quaternion.LookRotation(Vector3.ProjectOnPlane(direction, Vector3.up));
        }
    }
}
