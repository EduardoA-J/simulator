using System.Collections.Generic;
using UnityEngine;

namespace AvionesPapelVR
{
    public partial class LevelRunner
    {
        LevelDefinition _level;
        readonly List<Mesh> _courseMeshes = new();
        readonly List<float> _checkpoints = new();
        readonly List<GameObject> _checkpointFrames = new();
        public int GatesPassed { get; private set; }
        public int GateCount => _checkpoints.Count;
        public Vector3 NextGateLocal => _level == null ? Vector3.zero :
            _level.PathPoint(GatesPassed < GateCount ? _checkpoints[GatesPassed] : _level.length);
        public Vector3 GateWorld(int index) => GameManager.Instance.CourseOrigin +
            GameManager.Instance.CourseRotation * _level.PathPoint(_checkpoints[index]);
        public Quaternion GateRotation(int index) => GameManager.Instance.CourseRotation * _level.PathRotation(_checkpoints[index]);
        public string NavigationHint
        {
            get
            {
                var gm = GameManager.Instance;
                if (_level == null || !_level.HasCurves || gm == null || gm.Player == null) return "";
                Vector3 delta = NextGateLocal - gm.CoursePoint(gm.Player.position);
                Vector3 relative = gm.Player.InverseTransformDirection(gm.CourseRotation * delta);
                string turn = relative.x > 1.5f ? "DERECHA" : relative.x < -1.5f ? "IZQUIERDA" : "";
                string height = delta.y > 0.7f ? "SUBE" : delta.y < -0.7f ? "BAJA" : "";
                string direction = relative.z < -2f ? "REGRESA AL PASO" :
                    height != "" && turn != "" ? height + " + " + turn :
                    height != "" ? height : turn != "" ? "GIRA " + turn : "SIGUE EL PASO";
                return GatesPassed < GateCount ? $"PASO {GatesPassed + 1}/{GateCount} · {direction}" : "META · ÚLTIMO TRAMO";
            }
        }

        void ClearCurvedCourse()
        {
            foreach (var mesh in _courseMeshes) if (mesh != null) Destroy(mesh);
            _courseMeshes.Clear(); _checkpoints.Clear(); _checkpointFrames.Clear();
            GatesPassed = 0; _level = null;
        }

        Vector3 GroundPoint(float x, float y, float z)
        {
            if (_level == null || !_level.HasCurves) return new Vector3(x, y, z);
            var point = _level.PathPoint(z); point.y = y;
            return point + _level.GroundRotation(z) * Vector3.right * x;
        }

        Vector3 FlightPoint(float x, float y, float z)
        {
            if (_level == null || !_level.HasCurves) return new Vector3(x, y, z);
            return _level.PathPoint(z) + _level.GroundRotation(z) * Vector3.right * x + Vector3.up * (y - 1.8f);
        }

        void Ribbon(Transform parent, string name, float halfWidth, float height, Color color, bool solid)
        {
            int samples = Mathf.CeilToInt(_level.length / 2f);
            var vertices = new Vector3[(samples + 1) * 2];
            var triangles = new int[samples * 6];
            for (int i = 0; i <= samples; i++)
            {
                float z = _level.length * i / samples;
                vertices[i * 2] = GroundPoint(-halfWidth, height, z);
                vertices[i * 2 + 1] = GroundPoint(halfWidth, height, z);
                if (i == samples) continue;
                int v = i * 2, t = i * 6;
                triangles[t] = v; triangles[t + 1] = v + 2; triangles[t + 2] = v + 1;
                triangles[t + 3] = v + 1; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
            }
            var mesh = new Mesh { name = name + "Mesh", vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); _courseMeshes.Add(mesh);
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>();
            Tint(go, color);
            if (solid) go.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        void BuildCurvedGround(LevelDefinition level, Transform parent)
        {
            Ribbon(parent, "Ground", 11f, 0f, ThemeGround(level.theme), true);
            Ribbon(parent, "CurvedRunway", 2.3f, 0.02f, new Color(0.10f, 0.17f, 0.22f), false);
            // Open park and taller office landmarks, leaving enough space to recover a turn.
            for (float z = 6f; z < level.length; z += 9f)
            foreach (float side in new[] { -1f, 1f })
            {
                var post = GameObject.CreatePrimitive(PrimitiveType.Cube);
                post.name = "BoundaryPost"; post.transform.SetParent(parent, false);
                float height = level.theme == LevelTheme.Office ? 12f : 1.1f;
                post.transform.SetPositionAndRotation(GroundPoint(side * 10.5f, height * 0.5f, z), level.GroundRotation(z));
                post.transform.localScale = new Vector3(0.45f, height, 0.45f);
                Tint(post, ThemeWall(level.theme));
            }
            var finish = Frame(parent, "Finish", level.PathPoint(level.length), level.PathRotation(level.length),
                3.5f, 2.25f, new Color(1f, 0.75f, 0.2f));
            Caption(finish.transform, "META", 2.65f);
        }

        void SpawnCourseObstacles(LevelDefinition level, Transform parent)
        {
            bool advanced = level.routePoints != null && level.routePoints.Length > 1;
            for (int i = 0; i < level.obstacleCount; i++)
            {
                // A repeatable rhythm: slalom, moving edge, overhead beam, low hurdle.
                // The centreline always stays open, including the full moving-obstacle sweep.
                float z = Mathf.Lerp(26f, level.length - 16f, (i + 0.5f) / level.obstacleCount);
                int pattern = i % 4;
                bool moving = pattern == 1;
                bool beam = advanced && pattern >= 2;
                var prefab = moving ? GameManager.Instance.obstacleMovingPrefab : GameManager.Instance.obstacleStaticPrefab;
                var go = !beam && prefab != null ? Instantiate(prefab, parent) : GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.transform.SetParent(parent, false);
                go.name = beam ? (pattern == 2 ? "Obstacle_HighBeam" : "Obstacle_LowHurdle") :
                    moving ? "Obstacle_MovingEdge" : "Obstacle_Slalom";
                float side = i % 2 == 0 ? -1f : 1f;
                Vector3 offset = beam ? Vector3.up * (pattern == 2 ? 2f : -1.7f) : Vector3.right * side * 3.5f;
                go.transform.localPosition = level.PathPoint(z) + level.PathRotation(z) * offset;
                go.transform.localRotation = level.PathRotation(z);
                if (beam)
                {
                    go.transform.localScale = new Vector3(6.6f, 0.45f, 0.55f);
                    Tint(go, pattern == 2 ? new Color(0.95f, 0.59f, 0.23f) : new Color(0.25f, 0.67f, 0.74f));
                }
                StripGameplay(go);
                if (moving)
                {
                    var motion = go.GetComponent<MovingObstacleMotion>() ?? go.AddComponent<MovingObstacleMotion>();
                    motion.axis = level.PathRotation(z) * Vector3.right;
                    motion.distance = advanced ? 0.65f : 0.4f;
                    motion.speed = advanced ? 1.15f : 0.8f;
                }
                EnsureDamageable(go, 55f, 25);
                _obstacles.Add(go.transform);
                _spawned.Add(go);
            }
        }

        GameObject Frame(Transform parent, string name, Vector3 point, Quaternion rotation, float halfWidth, float halfHeight, Color color)
        {
            var frame = new GameObject(name); frame.transform.SetParent(parent, false);
            frame.transform.SetPositionAndRotation(point, rotation);
            Decoration(frame.transform, "Left", new Vector3(-halfWidth, 0, 0), new Vector3(0.07f, halfHeight * 2, 0.07f), color);
            Decoration(frame.transform, "Right", new Vector3(halfWidth, 0, 0), new Vector3(0.07f, halfHeight * 2, 0.07f), color);
            Decoration(frame.transform, "Top", new Vector3(0, halfHeight, 0), new Vector3(halfWidth * 2, 0.07f, 0.07f), color);
            return frame;
        }

        void Caption(Transform parent, string value, float y)
        {
            var label = new GameObject("RouteSign"); label.transform.SetParent(parent, false);
            label.transform.localPosition = new Vector3(0, y, 0);
            var text = label.AddComponent<TextMesh>(); text.text = value;
            text.anchor = TextAnchor.MiddleCenter; text.alignment = TextAlignment.Center;
            text.fontSize = 48; text.characterSize = 0.05f; text.color = Color.white;
        }

        void AddCheckpoint(Transform parent, float z)
        {
            _checkpoints.Add(z);
            var frame = Frame(parent, "RouteCheckpoint_" + _checkpoints.Count, _level.PathPoint(z), _level.PathRotation(z),
                2.4f, 1.7f, new Color(0.25f, 0.85f, 0.8f));
            Caption(frame.transform, "PASO " + _checkpoints.Count.ToString("00"), 2.0f);
            _checkpointFrames.Add(frame);
        }

        void BuildCurvedGuides(LevelDefinition level, Transform parent)
        {
            for (float z = 2f; z < level.length; z += 3f)
            {
                var stripe = FrameMarker(parent, "CenterDash", GroundPoint(0, 0.04f, z),
                    level.GroundRotation(z), new Vector3(0.09f, 0.015f, 1f), Color.white);
                foreach (float side in new[] { -1f, 1f })
                {
                    FrameMarker(parent, "FlightGuide", FlightPoint(side * 2.7f, 0.3f, z),
                        level.PathRotation(z), new Vector3(0.12f, 0.05f, 0.8f), new Color(0.2f, 0.8f, 0.85f));
                }
            }
        }

        Transform FrameMarker(Transform parent, string name, Vector3 position, Quaternion rotation, Vector3 scale, Color color)
        {
            var marker = new GameObject(name); marker.transform.SetParent(parent, false);
            marker.transform.SetPositionAndRotation(position, rotation);
            Decoration(marker.transform, name + "Visual", Vector3.zero, scale, color);
            return marker.transform;
        }

        static bool Crossed(Vector3 previous, Vector3 current, Vector3 center, Quaternion rotation, float halfWidth, float halfHeight)
        {
            Quaternion inverse = Quaternion.Inverse(rotation);
            Vector3 from = inverse * (previous - center), to = inverse * (current - center);
            if (from.z >= 0 || to.z < 0) return false;
            Vector3 cross = Vector3.Lerp(from, to, -from.z / (to.z - from.z));
            return Mathf.Abs(cross.x) < halfWidth && Mathf.Abs(cross.y) < halfHeight;
        }

        void UpdateCurvedProgress(Vector3 current)
        {
            if (!_hasPreviousPosition) return;
            while (GatesPassed < GateCount && Crossed(_previousCoursePosition, current,
                _level.PathPoint(_checkpoints[GatesPassed]), _level.PathRotation(_checkpoints[GatesPassed]), 2.4f, 1.7f))
            {
                _checkpointFrames[GatesPassed].SetActive(false);
                GatesPassed++;
            }
            if (GatesPassed < GateCount)
                Progress01 = Mathf.Min(Progress01, _checkpoints[GatesPassed] / _level.length);
            if (Crossed(_previousCoursePosition, current, _level.PathPoint(_level.length), _level.PathRotation(_level.length), 3.5f, 2.25f))
            {
                if (GatesPassed == GateCount) GameManager.Instance.CompleteLevel();
                else GameManager.Instance.hud?.ShowFeedback("FALTAN PASOS · SIGUE LA RUTA", new Color(1f, 0.76f, 0.36f));
            }
        }
    }
}
