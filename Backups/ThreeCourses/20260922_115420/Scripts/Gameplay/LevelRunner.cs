using UnityEngine;

namespace AvionesPapelVR
{
    /// <summary>
    /// Genera el nivel de vuelo (enemigos, obstÃƒÂ¡culos, coleccionables, decoraciÃƒÂ³n).
    /// </summary>
    public class LevelRunner : MonoBehaviour
    {
        readonly System.Collections.Generic.List<GameObject> _spawned = new();
        readonly System.Collections.Generic.List<Transform> _obstacles = new();
        readonly System.Collections.Generic.HashSet<Transform> _passedObstacles = new();
        Mesh _ringMesh;
        Material _normalRingMaterial, _boostRingMaterial;
        readonly System.Collections.Generic.List<Material> _surfaceMaterials = new();
        Vector3 _previousCoursePosition;
        bool _hasPreviousPosition;
        public float Progress01 { get; private set; }

        [Header("DecoraciÃƒÂ³n (opcional)")]
        public GameObject deskPrefab;
        public GameObject treePrefab;
        public GameObject benchPrefab;
        public GameObject buildingPrefab;
        public GameObject crystalPrefab;

        public void ClearLevel()
        {
            foreach (var go in _spawned)
                if (go != null) { go.SetActive(false); Destroy(go); }
            _spawned.Clear();
            _obstacles.Clear();
            _passedObstacles.Clear();
            foreach (var material in _surfaceMaterials) if (material != null) Destroy(material);
            _surfaceMaterials.Clear();
            _hasPreviousPosition = false;
            Progress01 = 0f;
            RenderSettings.fog = false;
        }

        public void BuildLevel(LevelDefinition level, Transform root)
        {
            ClearLevel();
            if (level == null) return;

            var holder = new GameObject("Level_" + level.displayName);
            holder.transform.SetParent(root, false);
            holder.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            holder.transform.localScale = Vector3.one;
            _spawned.Add(holder);

            BuildGroundAndWalls(level, holder.transform);
            SpawnThemeDecor(level, holder.transform);
            SpawnEnemies(level, holder.transform);
            SpawnObstacles(level, holder.transform);
            SpawnCollectibles(level, holder.transform);
            SpawnRings(level, holder.transform);
            BuildRoute(level, holder.transform);
            var gm = GameManager.Instance;
            if (gm != null)
            {
                holder.transform.SetPositionAndRotation(gm.CourseOrigin, gm.CourseRotation);
                foreach (var motion in holder.GetComponentsInChildren<MovingObstacleMotion>())
                    motion.axis = gm.CourseRotation * Vector3.right;
                foreach (var fan in holder.GetComponentsInChildren<WindFanForce>())
                    fan.direction = gm.CourseRotation * Vector3.back;
                _previousCoursePosition = gm.CoursePoint(gm.FlightStart);
                _hasPreviousPosition = true;
            }

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = level.fogColor;
            RenderSettings.fogStartDistance = 28f;
            RenderSettings.fogEndDistance = 95f;
            if (Camera.main != null)
                Camera.main.backgroundColor = level.fogColor;
        }

        void BuildGroundAndWalls(LevelDefinition level, Transform parent)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Ground";
            ground.transform.SetParent(parent, false);
            ground.transform.position = new Vector3(0f, -0.05f, level.length * 0.5f);
            ground.transform.localScale = new Vector3(28f, 0.1f, level.length + 24f);
            Tint(ground, ThemeGround(level.theme));
            _spawned.Add(ground);

            for (int side = -1; side <= 1; side += 2)
            {
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = side < 0 ? "WallL" : "WallR";
                wall.transform.SetParent(parent, false);
                wall.transform.position = new Vector3(side * 11f, 3f, level.length * 0.5f);
                wall.transform.localScale = new Vector3(0.35f, 6f, level.length + 10f);
                Tint(wall, ThemeWall(level.theme));
                _spawned.Add(wall);
            }

            var goal = GameObject.CreatePrimitive(PrimitiveType.Cube);
            goal.name = "GoalGate";
            goal.transform.SetParent(parent, false);
            goal.transform.position = new Vector3(0f, 2.2f, level.length);
            goal.transform.localScale = new Vector3(7f, 4.5f, 0.35f);
            goal.GetComponent<Renderer>().enabled = false;
            goal.GetComponent<Collider>().isTrigger = true;
            _spawned.Add(goal);
            var gold = new Color(1f, 0.72f, 0.18f);
            Decoration(parent, "Finish_Left", new Vector3(-3.6f, 2.25f, level.length), new Vector3(0.16f, 4.5f, 0.2f), gold);
            Decoration(parent, "Finish_Right", new Vector3(3.6f, 2.25f, level.length), new Vector3(0.16f, 4.5f, 0.2f), gold);
            Decoration(parent, "Finish_Top", new Vector3(0f, 4.5f, level.length), new Vector3(7.35f, 0.18f, 0.2f), gold);
        }

        void SpawnThemeDecor(LevelDefinition level, Transform parent)
        {
            int count = 12;
            for (int i = 0; i < count; i++)
            {
                float z = Random.Range(4f, level.length - 4f);
                float x = (i % 2 == 0) ? Random.Range(-9.5f, -5f) : Random.Range(5f, 9.5f);
                var prefab = ResolveDecor(level.theme);
                var go = prefab != null
                    ? Instantiate(prefab, parent)
                    : GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.transform.SetParent(parent, true);
                go.transform.position = new Vector3(x, 0f, z);
                go.transform.localScale *= Random.Range(1.8f, 2.6f);
                StripGameplay(go);
                foreach (var c in go.GetComponentsInChildren<Collider>())
                    c.enabled = false;
                _spawned.Add(go);
            }
        }

        GameObject ResolveDecor(LevelTheme theme)
        {
            return theme switch
            {
                LevelTheme.School => deskPrefab != null ? deskPrefab : GameManager.Instance?.tablePrefab,
                LevelTheme.Park => treePrefab != null ? treePrefab : benchPrefab,
                LevelTheme.Office => GameManager.Instance?.obstacleStaticPrefab,
                LevelTheme.MiniCity => buildingPrefab != null ? buildingPrefab : GameManager.Instance?.obstacleStaticPrefab,
                LevelTheme.Magic => crystalPrefab != null ? crystalPrefab : GameManager.Instance?.starPrefab,
                _ => null
            };
        }

        void SpawnEnemies(LevelDefinition level, Transform parent)
        {
            var gm = GameManager.Instance;
            for (int i = 0; i < level.enemyCount; i++)
            {
                float z = Mathf.Lerp(10f, level.length - 10f, (i + 1f) / (level.enemyCount + 1f));
                float x = (i % 2 == 0 ? -1f : 1f) * Random.Range(3.5f, 4.5f);
                float y = Random.Range(1.3f, 3.8f);
                int roll = i % 3;
                GameObject prefab = null;
                if (gm != null)
                    prefab = roll == 0 ? gm.enemyDronePrefab : roll == 1 ? gm.enemyBirdPrefab : gm.enemyFanPrefab;

                var go = prefab != null ? Instantiate(prefab, parent) : GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.transform.SetParent(parent, true);
                go.name = "Enemy_" + roll;
                go.transform.position = new Vector3(x, y, z);
                StripGameplay(go);

                if (roll == 2)
                {
                    var fan = go.GetComponent<WindFanForce>() ?? go.AddComponent<WindFanForce>();
                    fan.force = 5f + level.windStrength * 5f;
                    fan.radius = 3.2f;
                    fan.direction = Vector3.back;
                    EnsureDamageable(go, 45f, 35);
                }
                else
                {
                    var brain = go.GetComponent<EnemyBrain>() ?? go.AddComponent<EnemyBrain>();
                    brain.moveSpeed = level.enemySpeed + roll * 0.4f;
                    brain.amplitude = 1f + level.windStrength;
                    brain.bulletPrefab = gm != null ? gm.bulletPrefab : null;
                    EnsureDamageable(go, roll == 0 ? 32f : 22f, roll == 0 ? 70 : 50);
                }
                _spawned.Add(go);
            }
        }

        void SpawnObstacles(LevelDefinition level, Transform parent)
        {
            var gm = GameManager.Instance;
            for (int i = 0; i < level.obstacleCount; i++)
            {
                // Even spacing guarantees a readable opening and prevents obstacle clusters.
                float z = Mathf.Lerp(14f, level.length - 8f, (i + 1f) / (level.obstacleCount + 1f));
                float x = (i % 2 == 0 ? -1f : 1f) * Random.Range(2.6f, 4f);
                bool moving = i % 2 == 0;
                GameObject prefab = gm != null
                    ? (moving ? gm.obstacleMovingPrefab : gm.obstacleStaticPrefab)
                    : null;
                var go = prefab != null ? Instantiate(prefab, parent) : GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.transform.SetParent(parent, true);
                go.name = moving ? "Obstacle_Moving" : "Obstacle_Static";
                go.transform.position = new Vector3(x, moving ? 1.6f : 0.9f, z);
                StripGameplay(go);
                if (moving)
                {
                    var m = go.GetComponent<MovingObstacleMotion>() ?? go.AddComponent<MovingObstacleMotion>();
                    m.distance = 2.2f;
                    m.speed = 1.1f + i * 0.08f;
                }
                EnsureDamageable(go, 55f, 25);
                _obstacles.Add(go.transform);
                _spawned.Add(go);
            }
        }

        void SpawnCollectibles(LevelDefinition level, Transform parent)
        {
            var gm = GameManager.Instance;
            for (int i = 0; i < level.starCount; i++)
                SpawnPickup(gm?.starPrefab, CollectiblePickup.Kind.Star, parent, RandomPoint(level), 1.6f);
            for (int i = 0; i < level.coinCount; i++)
                SpawnPickup(gm?.coinPrefab, CollectiblePickup.Kind.Coin, parent, RandomPoint(level), 1.5f);
            for (int i = 0; i < 5; i++)
                SpawnPickup(gm?.partPrefab, CollectiblePickup.Kind.Part, parent, RandomPoint(level), 1.4f);

            for (int i = 0; i < level.powerUpCount; i++)
            {
                int r = i % 3;
                if (r == 0) SpawnPickup(gm?.turboPrefab, CollectiblePickup.Kind.Turbo, parent, RandomPoint(level), 1.7f);
                else if (r == 1) SpawnPickup(gm?.shieldPrefab, CollectiblePickup.Kind.Shield, parent, RandomPoint(level), 1.7f);
                else SpawnPickup(gm?.triplePrefab, CollectiblePickup.Kind.Triple, parent, RandomPoint(level), 1.7f);
            }
        }

        void SpawnPickup(GameObject prefab, CollectiblePickup.Kind kind, Transform parent, Vector3 pos, float scale)
        {
            var go = prefab != null ? Instantiate(prefab, parent) : GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.transform.SetParent(parent, true);
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * scale;
            StripGameplay(go);
            var pick = go.GetComponent<CollectiblePickup>() ?? go.AddComponent<CollectiblePickup>();
            pick.kind = kind;
            foreach (var c in go.GetComponentsInChildren<Collider>())
            {
                if (c is MeshCollider mesh) mesh.convex = true;
                c.isTrigger = true;
            }
            if (go.GetComponentInChildren<Collider>() == null)
            {
                var s = go.AddComponent<SphereCollider>();
                s.isTrigger = true;
                s.radius = 0.3f;
            }
            _spawned.Add(go);
        }

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.State != GameState.Flight || gm.Player == null) return;
            Vector3 current = gm.CoursePoint(gm.Player.position);
            float length = gm.CurrentLevel != null ? gm.CurrentLevel.length : 0f;
            Progress01 = length > 0f ? Mathf.Clamp01(current.z / length) : 0f;
            foreach (var obstacle in _obstacles)
                if (obstacle != null && !_passedObstacles.Contains(obstacle) && current.z > gm.CoursePoint(obstacle.position).z + 2f)
                {
                    _passedObstacles.Add(obstacle);
                    gm.NotifyObstaclePassed();
                }
            if (_hasPreviousPosition && length > 0f && _previousCoursePosition.z < length && current.z >= length)
            {
                float fraction = (length - _previousCoursePosition.z) / (current.z - _previousCoursePosition.z);
                Vector3 crossing = Vector3.Lerp(_previousCoursePosition, current, fraction);
                if (Mathf.Abs(crossing.x) < 3.5f && crossing.y > 0f && crossing.y < 4.5f)
                    gm.CompleteLevel();
            }
            _previousCoursePosition = current;
            _hasPreviousPosition = true;
        }

        void SpawnRings(LevelDefinition level, Transform parent)
        {
            if (_ringMesh == null) _ringMesh = CreateRingMesh();
            if (_normalRingMaterial == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                _normalRingMaterial = new Material(shader) { color = new Color(0.1f, 0.7f, 1f) };
                _boostRingMaterial = new Material(shader) { color = new Color(1f, 0.7f, 0.1f) };
                foreach (var material in new[] { _normalRingMaterial, _boostRingMaterial })
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", material.color * 0.65f);
                    material.SetFloat("_Smoothness", 0.55f);
                }
            }
            int index = 0;
            for (float z = 6f; z < level.length - 5f; z += 7f)
            {
                var ring = new GameObject(index % 2 == 0 ? "ScoreRing" : "BoostRing");
                ring.transform.SetParent(parent, false);
                float sway = index < 2 ? 0f : Mathf.Sin(index * 0.65f) * 0.45f;
                ring.transform.position = new Vector3(sway, Mathf.Lerp(1.8f, 1.25f, z / level.length), z);
                ring.AddComponent<MeshFilter>().sharedMesh = _ringMesh;
                ring.AddComponent<MeshRenderer>().sharedMaterial = index % 2 == 0 ? _normalRingMaterial : _boostRingMaterial;
                var trigger = ring.AddComponent<SphereCollider>();
                trigger.isTrigger = true;
                trigger.radius = 1.15f;
                var pickup = ring.AddComponent<CollectiblePickup>();
                pickup.kind = index % 2 == 0 ? CollectiblePickup.Kind.Ring : CollectiblePickup.Kind.BoostRing;
                pickup.magnetRange = 0f;
                _spawned.Add(ring);
                index++;
            }
        }

        static Mesh CreateRingMesh()
        {
            const int major = 32, minor = 6;
            var vertices = new Vector3[(major + 1) * (minor + 1)];
            var normals = new Vector3[vertices.Length];
            var triangles = new int[major * minor * 6];
            int t = 0;
            for (int a = 0; a <= major; a++)
            for (int b = 0; b <= minor; b++)
            {
                float u = a * Mathf.PI * 2f / major, v = b * Mathf.PI * 2f / minor;
                int i = a * (minor + 1) + b;
                var normal = new Vector3(Mathf.Cos(u) * Mathf.Cos(v), Mathf.Sin(u) * Mathf.Cos(v), Mathf.Sin(v));
                vertices[i] = new Vector3(Mathf.Cos(u), Mathf.Sin(u), 0f) * 1.25f + normal * 0.08f;
                normals[i] = normal;
                if (a == major || b == minor) continue;
                int n = i + minor + 1;
                triangles[t++] = i; triangles[t++] = n; triangles[t++] = i + 1;
                triangles[t++] = i + 1; triangles[t++] = n; triangles[t++] = n + 1;
            }
            var mesh = new Mesh { name = "ArcadeRing", vertices = vertices, normals = normals, triangles = triangles };
            mesh.RecalculateBounds();
            return mesh;
        }

        void OnDestroy()
        {
            foreach (var material in _surfaceMaterials) if (material != null) Destroy(material);
            if (_ringMesh != null) Destroy(_ringMesh);
            if (_normalRingMaterial != null) Destroy(_normalRingMaterial);
            if (_boostRingMaterial != null) Destroy(_boostRingMaterial);
        }

        static void EnsureDamageable(GameObject go, float hp, int score)
        {
            var d = go.GetComponent<Damageable>() ?? go.AddComponent<Damageable>();
            d.maxHealth = hp;
            d.scoreOnDestroy = score;
            d.ResetHealth();
        }

        static void StripGameplay(GameObject go)
        {
            foreach (var c in go.GetComponentsInChildren<PlaneFlightDemo>()) Destroy(c);
            foreach (var c in go.GetComponentsInChildren<KeyboardPlanePilot>()) Destroy(c);
            foreach (var c in go.GetComponentsInChildren<PaperShooter>()) Destroy(c);
            foreach (var rb in go.GetComponentsInChildren<Rigidbody>())
            {
                if (rb.gameObject != go) Destroy(rb);
                else { rb.isKinematic = true; rb.useGravity = false; }
            }
        }

        static Vector3 RandomPoint(LevelDefinition level) =>
            new(Random.Range(-4f, 4f), Random.Range(1.1f, 3.4f), Random.Range(8f, level.length - 5f));

        void Tint(GameObject go, Color c)
        {
            var r = go.GetComponent<Renderer>();
            if (r == null) return;
            var existing = _surfaceMaterials.Find(m => m != null && m.color == c);
            if (existing != null) { r.sharedMaterial = existing; return; }
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? r.sharedMaterial.shader);
            material.color = c;
            material.SetFloat("_Smoothness", 0.25f);
            r.sharedMaterial = material;
            _surfaceMaterials.Add(material);
        }

        void Decoration(Transform parent, string label, Vector3 position, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = label;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.GetComponent<Collider>().enabled = false;
            Destroy(go.GetComponent<Collider>());
            Tint(go, color);
        }

        void BuildRoute(LevelDefinition level, Transform parent)
        {
            var cyan = new Color(0.18f, 0.78f, 0.85f);
            foreach (float side in new[] { -1f, 1f })
            {
                Decoration(parent, "WallTrim", new Vector3(side * 10.8f, 0.8f, level.length * 0.5f),
                    new Vector3(0.08f, 0.12f, level.length), cyan);
                for (float z = 8f; z < level.length - 5f; z += 12f)
                {
                    Decoration(parent, "WindowFrame", new Vector3(side * 10.78f, 3.6f, z),
                        new Vector3(0.1f, 2.8f, 5f), new Color(0.88f, 0.85f, 0.74f));
                    Decoration(parent, "WindowGlass", new Vector3(side * 10.7f, 3.6f, z),
                        new Vector3(0.06f, 2.5f, 4.7f), new Color(0.4f, 0.7f, 0.85f));
                    Decoration(parent, "WindowMullion", new Vector3(side * 10.65f, 3.6f, z),
                        new Vector3(0.08f, 2.5f, 0.1f), new Color(0.88f, 0.85f, 0.74f));
                }
            }
            Decoration(parent, "Runway", new Vector3(0f, 0.012f, level.length * 0.5f),
                new Vector3(4.5f, 0.018f, level.length), new Color(0.12f, 0.19f, 0.24f));
            for (float z = 1f; z < level.length; z += 4f)
            {
                Decoration(parent, "CenterDash", new Vector3(0f, 0.03f, z), new Vector3(0.07f, 0.012f, 1.2f), new Color(0.85f, 0.89f, 0.8f));
                foreach (float x in new[] { -2.3f, 2.3f })
                    Decoration(parent, "RouteMarker", new Vector3(x, 0.035f, z), new Vector3(0.1f, 0.025f, 0.65f), cyan);
            }
            for (int i = 0; i < 8; i++)
                Decoration(parent, "FinishStripe", new Vector3((i - 3.5f) * 0.55f, 0.04f, level.length - 0.5f),
                    new Vector3(0.5f, 0.015f, 1f), i % 2 == 0 ? Color.white : new Color(0.1f, 0.12f, 0.15f));
        }

        static Color ThemeGround(LevelTheme t) => t switch
        {
            LevelTheme.School => new Color(0.72f, 0.6f, 0.4f),
            LevelTheme.Park => new Color(0.3f, 0.55f, 0.28f),
            LevelTheme.Office => new Color(0.22f, 0.28f, 0.35f),
            LevelTheme.MiniCity => new Color(0.35f, 0.35f, 0.38f),
            LevelTheme.Magic => new Color(0.2f, 0.12f, 0.3f),
            _ => Color.gray
        };

        static Color ThemeWall(LevelTheme t) => t switch
        {
            LevelTheme.School => new Color(0.85f, 0.82f, 0.7f),
            LevelTheme.Park => new Color(0.45f, 0.65f, 0.4f),
            LevelTheme.Office => new Color(0.3f, 0.4f, 0.5f),
            LevelTheme.MiniCity => new Color(0.4f, 0.4f, 0.45f),
            LevelTheme.Magic => new Color(0.35f, 0.2f, 0.5f),
            _ => Color.gray
        };
    }
}
