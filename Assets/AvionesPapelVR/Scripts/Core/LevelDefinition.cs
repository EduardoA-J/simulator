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
        [Tooltip("Optional authored route, ordered by forward distance (Z). Empty keeps the original route.")]
        public Vector3[] routePoints = new Vector3[0];
        [Min(3f)] public float ringSpacing = 7f;
        [Min(0)] public int layoutSeed = 41;
        public bool HasCurves => routePoints != null && routePoints.Length > 1 || curveAmplitude > 0f && curveCycles > 0f;

        // Parameter z is forward course distance. The first 18 m are a gentle entry.
        public Vector3 PathPoint(float z)
        {
            if (routePoints != null && routePoints.Length > 1)
            {
                int last = routePoints.Length - 1;
                if (z <= routePoints[0].z) return routePoints[0] + Vector3.forward * (z - routePoints[0].z);
                if (z >= routePoints[last].z) return routePoints[last] + Vector3.forward * (z - routePoints[last].z);
                int i = 0;
                while (i < last - 1 && z > routePoints[i + 1].z) i++;
                float span = Mathf.Max(0.01f, routePoints[i + 1].z - routePoints[i].z);
                float t = (z - routePoints[i].z) / span;
                float a = 2f * t * t * t - 3f * t * t + 1f;
                float b = t * t * t - 2f * t * t + t;
                float c = -2f * t * t * t + 3f * t * t;
                float d = t * t * t - t * t;
                Vector3 point = a * routePoints[i] + b * span * RouteTangent(i) +
                    c * routePoints[i + 1] + d * span * RouteTangent(i + 1);
                point.z = z;
                return point;
            }
            float phase = Mathf.Max(0f, z - 18f) / Mathf.Max(1f, length - 18f) * Mathf.PI * 2f * curveCycles;
            float wave = HasCurves ? 1f - Mathf.Cos(phase) : 0f;
            return new Vector3(curveAmplitude * wave, 1.8f + climbAmplitude * wave, z);
        }

        Vector3 RouteTangent(int index)
        {
            if (index == 0 || index == routePoints.Length - 1) return Vector3.forward;
            Vector3 before = routePoints[index] - routePoints[index - 1];
            Vector3 after = routePoints[index + 1] - routePoints[index];
            return new Vector3(Slope(before.x / before.z, after.x / after.z),
                Slope(before.y / before.z, after.y / after.z), 1f);
        }

        // A monotone Hermite tangent rounds each manoeuvre without overshooting its height or sides.
        static float Slope(float a, float b) => a * b <= 0f ? 0f : 2f * a * b / (a + b);

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
