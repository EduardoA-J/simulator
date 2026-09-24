using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AvionesPapelVR
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Contenido")]
        public List<PlaneDefinition> planes = new();
        public List<LevelDefinition> levels = new();
        public List<WeaponDefinition> weapons = new();
        public List<PowerUpDefinition> powerUps = new();
        public WeaponDefinition Weapon(WeaponType type)
        {
            if (_weaponLookup.TryGetValue(type, out var cached) && cached != null) return cached;
            foreach (var w in weapons) if (w != null && w.type == type) { _weaponLookup[type] = w; return w; }
            return null;
        }
        public PowerUpDefinition PowerUp(PowerUpType type)
        {
            if (_powerUpLookup.TryGetValue(type, out var cached) && cached != null) return cached;
            foreach (var p in powerUps) if (p != null && p.type == type) { _powerUpLookup[type] = p; return p; }
            return null;
        }
        public void ActivatePowerUp(PowerUpType type)
        {
            var definition = PowerUp(type);
            if (definition == null) { Debug.LogWarning("Power-up sin datos: " + type); return; }
            ActivatePowerUp(type, definition.duration);
        }
        public GameObject bulletPrefab;
        public GameObject missilePrefab;
        public GameObject bombPrefab;
        public GameObject enemyDronePrefab;
        public GameObject enemyBirdPrefab;
        public GameObject enemyFanPrefab;
        public GameObject obstacleStaticPrefab;
        public GameObject obstacleMovingPrefab;
        public GameObject starPrefab;
        public GameObject coinPrefab;
        public GameObject partPrefab;
        public GameObject turboPrefab;
        public GameObject shieldPrefab;
        public GameObject triplePrefab;
        public GameObject tablePrefab;
        public AudioClip sfxShoot;
        public AudioClip sfxPickup;
        public AudioClip sfxHit;
        public AudioClip sfxLaunch;
        public AudioClip sfxWind;

        [Header("Referencias escena")]
        public Transform selectAnchor;
        public Transform launchAnchor;
        public Transform flightStartAnchor;
        public Transform levelRoot;
        public Camera gameCamera;
        public PlaneSelector planeSelector;
        public LaunchController launchController;
        public FlightController flightController;
        public LevelRunner levelRunner;
        public GameHUD hud;
        public VrRigFollower vrRigFollower;
        public Transform xrOrigin;
        public bool vrMode;

        // One horizontal reference for spawning, aiming and procedural course geometry.
        public Quaternion CourseRotation
        {
            get
            {
                Vector3 forward = flightStartAnchor != null ? flightStartAnchor.forward : Vector3.forward;
                forward = Vector3.ProjectOnPlane(forward, Vector3.up);
                return forward.sqrMagnitude > 0.001f ? Quaternion.LookRotation(forward) : Quaternion.identity;
            }
        }
        public Vector3 FlightStart => flightStartAnchor != null ? flightStartAnchor.position : new Vector3(0f, 2f, 8f);
        // Match the scene's ground plane so the lobby floor cannot cover the runway.
        public Vector3 CourseOrigin => new Vector3(FlightStart.x, 0f, FlightStart.z);
        public Vector3 CoursePoint(Vector3 world) => Quaternion.Inverse(CourseRotation) * (world - CourseOrigin);

        public Vector3 AssistedLaunch(Vector3 velocity)
        {
            Vector3 local = Quaternion.Inverse(CourseRotation) * velocity;
            float speed = velocity.magnitude;
            if (speed < 0.01f) return CourseRotation * Vector3.forward * 6f;
            // Fold a backwards gesture into the course, retaining lateral aim and throw strength.
            float yaw = Mathf.Clamp(Mathf.Atan2(local.x, Mathf.Max(0.1f, Mathf.Abs(local.z))) * Mathf.Rad2Deg, -25f, 25f);
            float elevation = Mathf.Clamp(Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg, -8f, 18f);
            float limit = SelectedPlane != null ? SelectedPlane.EffectiveMaxSpeed : 24f;
            return CourseRotation * Quaternion.Euler(-elevation, yaw, 0f) * Vector3.forward * Mathf.Min(limit, Mathf.Max(6f, speed));
        }

        bool _vrPrimary, _vrMenu;
        static readonly Key[] MapKeys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9 };
        readonly Dictionary<WeaponType, WeaponDefinition> _weaponLookup = new();
        readonly Dictionary<PowerUpType, PowerUpDefinition> _powerUpLookup = new();

        public GameState State { get; private set; } = GameState.MainMenu;
        public PlaneDefinition SelectedPlane { get; private set; }
        public int CurrentLevelIndex { get; private set; }
        public bool CampaignComplete { get; private set; }
        public int LastMenuTransitionFrame { get; private set; } = -1;
        int _levelStartScore;
        public int Score { get; private set; }
        public int Lives { get; private set; } = 3;
        public int StarsCollected { get; private set; }
        public int CoinsCollected { get; private set; }
        public int PartsCollected { get; private set; }
        public bool HasShield { get; private set; }
        public bool HasTripleShot { get; private set; }
        public float TurboTimer { get; private set; }
        public float ShieldTimer { get; private set; }
        public float TripleTimer { get; private set; }

        GameObject _playerPlane;
        AudioSource _audio;
        FlightTutorial _tutorial;
        public bool persistProgress = true;
        public int RingsCollected { get; private set; }
        public int BoostsCollected { get; private set; }
        public int BestScore { get; private set; }
        public float BestDistance { get; private set; }
        public float RunDistance { get; private set; }
        public float LastRunTime { get; private set; }
        public string EndReason { get; private set; } = "";
        public List<string> NewlyUnlocked { get; } = new();
        public Transform Player => _playerPlane != null ? _playerPlane.transform : null;
        Rigidbody _playerBody;
        public Rigidbody PlayerBody
        {
            get
            {
                if (_playerPlane == null) return null;
                if (_playerBody == null || _playerBody.gameObject != _playerPlane) _playerBody = _playerPlane.GetComponent<Rigidbody>();
                return _playerBody;
            }
        }
        public FlightTutorial Tutorial => _tutorial;
        public event System.Action<PlaneDefinition> PlaneGrabbed;
        public event System.Action<Vector3> FlightStarted;


        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            BestScore = PlayerPrefs.GetInt("AvionesPapelVR.BestScore", 0);
            BestDistance = PlayerPrefs.GetFloat("AvionesPapelVR.BestDistance", 0f);
            _tutorial = GetComponent<FlightTutorial>();
            if (_tutorial == null) _tutorial = gameObject.AddComponent<FlightTutorial>();
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0f;
        }

        void Start()
        {
            if (hud == null) hud = FindFirstObjectByType<GameHUD>();
            if (planeSelector == null) planeSelector = FindFirstObjectByType<PlaneSelector>();
            if (launchController == null) launchController = FindFirstObjectByType<LaunchController>();
            if (flightController == null) flightController = FindFirstObjectByType<FlightController>();
            if (levelRunner == null) levelRunner = FindFirstObjectByType<LevelRunner>();
            planeSelector?.PrepareLobby();
            GoToMainMenu();
        }

        void Update()
        {
            TickPowerUps();

            // Trigger belongs exclusively to XRI UI. A previous-frame hover check can miss a
            // newly aimed button and start/retry the wrong map before its click is released.
            bool primary = vrMode && VrInput.PrimaryButton();
            bool vrConfirm = primary && !_vrPrimary;
            _vrPrimary = primary;
            bool menu = vrMode && VrInput.MenuButton();
            bool back = menu && !_vrMenu;
            _vrMenu = menu;
            if (back && State != GameState.MainMenu)
            {
                GoToMainMenu();
                return;
            }

            switch (State)
            {
                case GameState.MainMenu:
                    int pressed = -1;
                    for (int i = 0; i < levels.Count && i < MapKeys.Length; i++)
                        if (GameInput.IsDown(MapKeys[i])) { pressed = i; break; }
                    if (pressed >= 0) { StartSelectedLevel(pressed); break; }
                    if (GameInput.ConfirmDown() || vrConfirm)
                        StartSelectedLevel(0);
                    break;
                case GameState.LevelComplete:
                    if (GameInput.ConfirmDown() || vrConfirm)
                        NextLevelOrFinish();
                    break;
                case GameState.GameOver:
                    if (GameInput.IsDown(Key.R) || GameInput.ConfirmDown() || vrConfirm)
                        RestartRun();
                    break;
            }

            if ((State == GameState.PlaneSelect || State == GameState.Launch || State == GameState.LevelComplete || State == GameState.GameOver) &&
                GameInput.IsDown(Key.Escape)) GoToMainMenu();

            if (GameInput.IsDown(Key.F1))
                Time.timeScale = Time.timeScale > 0.5f ? 0f : 1f;
        }

        public void GoToMainMenu()
        {
            LastMenuTransitionFrame = Time.frameCount;
            State = GameState.MainMenu;
            if (!vrMode)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            CleanupPlayer();
            vrRigFollower?.StopFollowAndReturnLobby();
            levelRunner?.ClearLevel();
            planeSelector?.Clear();
            RingsCollected = BoostsCollected = 0;
            RunDistance = 0f;
            Lives = 3;
            Score = 0;
            StarsCollected = 0;
            CoinsCollected = 0;
            PartsCollected = 0;
            CurrentLevelIndex = 0;
            CampaignComplete = false;
            _levelStartScore = 0;
            EndReason = "";
            SelectedPlane = null;
            NewlyUnlocked.Clear();
            hud?.SetVictory(false);
            ClearPowerUps();
            hud?.Refresh();
        }

        // The three maps are LevelDefinition assets in the existing scene; retain the same XR rig.
        // Public int argument also supports Inspector Button.OnClick bindings (zero based).
        public void StartSelectedLevel(int index)
        {
            if (State == GameState.Flight || State == GameState.Launch ||
                index < 0 || index >= levels.Count || levels[index] == null) return;
            GoToMainMenu();
            CurrentLevelIndex = index;
            StartPlaneSelect();
        }

        public void StartPlaneSelect()
        {
            LastMenuTransitionFrame = Time.frameCount;
            State = GameState.PlaneSelect;
            if (!vrMode)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            CleanupPlayer();
            vrRigFollower?.StopFollowAndReturnLobby();
            levelRunner?.ClearLevel();
            if (planeSelector != null)
                planeSelector.enableVrGrab = vrMode;
            if (vrMode) vrRigFollower?.SetInteractionLock(true);
            planeSelector?.Begin(planes, OnPlaneChosen);
            _tutorial?.Begin();
            hud?.Refresh();
        }

        void OnPlaneChosen(PlaneDefinition def)
        {
            SelectedPlane = def;
            BeginLaunch();
        }

        public void NotifyVrPlaneGrabbed(PlaneDefinition def)
        {
            if (def == null || !IsPlaneUnlocked(def)) return;
            if (State != GameState.PlaneSelect && State != GameState.Launch) return;
            SelectedPlane = def;
            planeSelector?.SelectGrabbed(def);
            _tutorial?.Record(FlightTutorial.Step.Grab);
            PlaneGrabbed?.Invoke(def);
            State = GameState.Launch;
            hud?.Refresh();
        }

        public void NotifyVrPlaneReturned()
        {
            if (State == GameState.Launch) State = GameState.PlaneSelect;
            planeSelector?.ReturnToTable();
            hud?.Refresh();
        }

        /// <summary>
        /// Lanzamiento desde un avión de mesa. El avión de mesa se retira y el vuelo lo realiza un avión nuevo
        /// creado en <see cref="FlightStart"/>. Devuelve false si no se pudo iniciar (el avión vuelve a la mesa).
        /// </summary>
        public bool BeginFlightFromVrThrow(PlaneDefinition def, Vector3 velocity, Vector3? releasePosition = null)
        {
            if (State != GameState.PlaneSelect && State != GameState.Launch) return false;
            if (velocity.sqrMagnitude < 0.01f || CurrentLevel == null) return false;
            if (def == null) def = SelectedPlane;
            if (!IsPlaneUnlocked(def)) return false;
            SelectedPlane = def;
            planeSelector?.Clear();
            BeginFlight(velocity);
            return State == GameState.Flight;
        }

        public bool BeginFlightFromVrThrow(Vector3 velocity) => BeginFlightFromVrThrow(SelectedPlane, velocity);

        public void BeginLaunch()
        {
            if (vrMode) { StartPlaneSelect(); return; }
            State = GameState.Launch;
            SpawnPlayerAt(launchAnchor != null ? launchAnchor.position : new Vector3(0f, 1.2f, 0f),
                launchAnchor != null ? launchAnchor.rotation : Quaternion.identity, forLaunch: true);
            launchController?.Begin(_playerPlane, SelectedPlane, OnLaunched);
            hud?.Refresh();
            PlaySfx(sfxLaunch, 0.4f);
        }

        void OnLaunched(Vector3 velocity)
        {
            BeginFlight(velocity);
        }

        public void BeginFlight(Vector3 launchVelocity)
        {
            if (CurrentLevel == null || SelectedPlane == null)
            {
                Debug.LogError("[AvionesPapelVR] No se puede volar sin avion y nivel asignados.");
                return;
            }
            State = GameState.Flight;
            launchVelocity = AssistedLaunch(launchVelocity);
            var start = FlightStart;
            if (_playerPlane == null)
                SpawnPlayerAt(start, Quaternion.LookRotation(launchVelocity.normalized), forLaunch: false);
            else
            {
                _playerPlane.transform.position = start;
                _playerPlane.transform.rotation = Quaternion.LookRotation(launchVelocity.normalized);
            }

            var level = levels[Mathf.Clamp(CurrentLevelIndex, 0, levels.Count - 1)];
            levelRunner?.BuildLevel(level, levelRoot != null ? levelRoot : transform);

            if (flightController != null)
            {
                flightController.vrMode = vrMode;
                flightController.Possess(_playerPlane, SelectedPlane, vrMode ? null : gameCamera, launchVelocity);
            }

            if (vrMode && vrRigFollower != null)
                vrRigFollower.StartFollow(_playerPlane.transform);
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            _tutorial?.Record(FlightTutorial.Step.Throw);
            FlightStarted?.Invoke(launchVelocity);
            hud?.Refresh();
            PlaySfx(sfxLaunch, 0.5f);
        }

        public void CompleteLevel()
        {
            if (State != GameState.Flight) return;
            float time = flightController != null ? flightController.FlightTime : 0f;
            FinishRun(true);
            CampaignComplete = CurrentLevelIndex >= levels.Count - 1;
            State = CampaignComplete ? GameState.GameOver : GameState.LevelComplete;
            EndReason = CampaignComplete ? "MAPA FINAL COMPLETADO" : "ESCENARIO COMPLETADO";
            LastRunTime = time;
            Progress.RecordCompletion(CurrentLevelIndex, time, persistProgress);
            hud?.Refresh();
        }

        /// <summary>Persistencia sencilla en PlayerPrefs: récord global, por nivel y progreso de campaña.</summary>
        public static class Progress
        {
            const string Prefix = "AvionesPapelVR.";
            public static int LevelBestScore(int level) => PlayerPrefs.GetInt(Prefix + "Level" + level + ".BestScore", 0);
            public static float LevelBestTime(int level) => PlayerPrefs.GetFloat(Prefix + "Level" + level + ".BestTime", 0f);
            public static bool LevelCompleted(int level) => PlayerPrefs.GetInt(Prefix + "Level" + level + ".Completed", 0) == 1;
            public static string LevelBestPlane(int level) => PlayerPrefs.GetString(Prefix + "Level" + level + ".Plane", "");
            /// <summary>Número de niveles superados de forma consecutiva desde el primero.</summary>
            public static int CampaignReach => PlayerPrefs.GetInt(Prefix + "Campaign.Reach", 0);

            public static void RecordRun(int level, int levelScore, string planeId, bool persist)
            {
                if (!persist || level < 0) return;
                if (levelScore > LevelBestScore(level))
                {
                    PlayerPrefs.SetInt(Prefix + "Level" + level + ".BestScore", levelScore);
                    PlayerPrefs.SetString(Prefix + "Level" + level + ".Plane", planeId ?? "");
                }
            }

            public static void RecordCompletion(int level, float time, bool persist)
            {
                if (!persist || level < 0) return;
                PlayerPrefs.SetInt(Prefix + "Level" + level + ".Completed", 1);
                float best = LevelBestTime(level);
                if (time > 0f && (best <= 0f || time < best)) PlayerPrefs.SetFloat(Prefix + "Level" + level + ".BestTime", time);
                if (level + 1 > CampaignReach) PlayerPrefs.SetInt(Prefix + "Campaign.Reach", level + 1);
                PlayerPrefs.Save();
            }
        }

        public void NextLevelOrFinish()
        {
            if (State != GameState.LevelComplete) return;
            CurrentLevelIndex++;
            if (CurrentLevelIndex >= levels.Count)
            {
                State = GameState.GameOver; // victoria final reutiliza pantalla con mensaje
                hud?.SetVictory(true);
                hud?.Refresh();
                return;
            }
            ClearPowerUps();
            _levelStartScore = Score;
            RingsCollected = BoostsCollected = 0;
            RunDistance = 0f;
            Lives = 3;
            EndReason = "";
            StartPlaneSelect();
        }

        public void PlayerHit(float damage)
        {
            if (State != GameState.Flight) return;
            if (HasShield)
            {
                HasShield = false;
                ShieldTimer = 0f;
                PlaySfx(sfxHit, 0.35f);
                return;
            }
            PlaySfx(sfxHit, 0.6f);
            FinishRun(false);
        }

        public void FinishRun(bool landed)
        {
            if (State != GameState.Flight) return;
            RunDistance = flightController != null ? flightController.Distance : 0f;
            int previousBest = BestScore;
            if (landed) AddScore(100);
            BestScore = Mathf.Max(BestScore, Score);
            BestDistance = Mathf.Max(BestDistance, RunDistance);
            NewlyUnlocked.Clear();
            foreach (var def in planes)
                if (def != null && !def.unlockedByDefault && previousBest < def.unlockScore && BestScore >= def.unlockScore)
                    NewlyUnlocked.Add(def.displayName);
            LastRunTime = flightController != null ? flightController.FlightTime : 0f;
            Progress.RecordRun(CurrentLevelIndex, Score - _levelStartScore, SelectedPlane != null ? SelectedPlane.SaveId : "", persistProgress);
            if (persistProgress)
            {
                PlayerPrefs.SetInt("AvionesPapelVR.BestScore", BestScore);
                PlayerPrefs.SetFloat("AvionesPapelVR.BestDistance", BestDistance);
                PlayerPrefs.Save();
            }
            if (landed) _tutorial?.Record(FlightTutorial.Step.Land);
            EndReason = landed ? "ATERRIZAJE" : "CHOQUE / FIN DEL VUELO";
            State = GameState.GameOver;
            Lives = landed ? Lives : 0;
            flightController?.Release();
            vrRigFollower?.StopFollowAndReturnLobby();
            if (!vrMode) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            hud?.SetVictory(landed);
            hud?.Refresh();
        }

        public bool IsPlaneUnlocked(PlaneDefinition def) =>
            def != null && (def.unlockedByDefault || BestScore >= def.unlockScore);

        public void CollectRing(bool boost)
        {
            if (State != GameState.Flight) return;
            if (boost)
            {
                BoostsCollected++;
                hud?.ShowFeedback("IMPULSO", new Color(1f, 0.76f, 0.25f));
                ActivatePowerUp(PowerUpType.Turbo);
                _tutorial?.Record(FlightTutorial.Step.Boost);
            }
            else
            {
                RingsCollected++;
                hud?.ShowFeedback("+100  ARO", new Color(0.3f, 0.9f, 1f));
                AddScore(100);
                PlaySfx(sfxPickup, 0.5f);
                _tutorial?.Record(FlightTutorial.Step.Ring);
            }
            hud?.Refresh();
        }

        public void NotifyFlightControl(float pitch, float roll) => _tutorial?.ObserveControls(pitch, roll);
        public void NotifyObstaclePassed() => _tutorial?.Record(FlightTutorial.Step.Dodge);

        public void RestartRun()
        {
            if (CampaignComplete) { CurrentLevelIndex = 0; _levelStartScore = 0; }
            CampaignComplete = false;
            Score = _levelStartScore;
            Lives = 3;
            StarsCollected = 0;
            CoinsCollected = 0;
            PartsCollected = 0;
            RingsCollected = BoostsCollected = 0;
            RunDistance = 0f;
            EndReason = "";
            NewlyUnlocked.Clear();
            ClearPowerUps();
            hud?.SetVictory(false);
            StartPlaneSelect();
        }

        public void AddScore(int amount)
        {
            Score += amount;
            hud?.Refresh();
        }

        public void CollectStar() { StarsCollected++; AddScore(25); PlaySfx(sfxPickup, 0.45f); }
        public void CollectCoin() { CoinsCollected++; AddScore(10); PlaySfx(sfxPickup, 0.35f); }
        public void CollectPart() { PartsCollected++; AddScore(40); PlaySfx(sfxPickup, 0.5f); }

        public void ActivatePowerUp(PowerUpType type, float duration)
        {
            switch (type)
            {
                case PowerUpType.Turbo:
                    TurboTimer = Mathf.Max(TurboTimer, duration);
                    flightController?.ApplyBoost();
                    break;
                case PowerUpType.Shield:
                    HasShield = true;
                    ShieldTimer = Mathf.Max(ShieldTimer, duration);
                    break;
                case PowerUpType.TripleShot:
                    HasTripleShot = true;
                    TripleTimer = Mathf.Max(TripleTimer, duration);
                    break;
            }
            PlaySfx(sfxPickup, 0.55f);
            hud?.Refresh();
        }

        void TickPowerUps()
        {
            if (TurboTimer > 0f)
            {
                TurboTimer -= Time.deltaTime;
                if (TurboTimer <= 0f) TurboTimer = 0f;
            }
            if (ShieldTimer > 0f)
            {
                ShieldTimer -= Time.deltaTime;
                if (ShieldTimer <= 0f) { ShieldTimer = 0f; HasShield = false; }
            }
            if (TripleTimer > 0f)
            {
                TripleTimer -= Time.deltaTime;
                if (TripleTimer <= 0f) { TripleTimer = 0f; HasTripleShot = false; }
            }
        }

        void ClearPowerUps()
        {
            TurboTimer = 0f;
            ShieldTimer = 0f;
            TripleTimer = 0f;
            HasShield = false;
            HasTripleShot = false;
        }

        public bool IsTurboActive => TurboTimer > 0f;

        void SpawnPlayerAt(Vector3 pos, Quaternion rot, bool forLaunch)
        {
            CleanupPlayer();
            if (SelectedPlane == null || SelectedPlane.prefab == null)
            {
                _playerPlane = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _playerPlane.name = "FallbackPlane";
            }
            else
            {
                _playerPlane = Instantiate(SelectedPlane.prefab, pos, rot);
            }

            _playerPlane.name = "PlayerPlane";
            _playerPlane.tag = "Player";
            foreach (var c in _playerPlane.GetComponentsInChildren<Collider>())
            {
                c.isTrigger = false;
                if (c is MeshCollider mesh) mesh.convex = true;
            }

            // Quitar animaciones de exposición y cualquier grab: el avión de vuelo sólo lo mueve FlightController.
            foreach (var d in _playerPlane.GetComponentsInChildren<PlaneFlightDemo>()) { d.enabled = false; Destroy(d); }
            foreach (var s in _playerPlane.GetComponentsInChildren<SpinBob>()) { s.enabled = false; Destroy(s); }
            foreach (var g in _playerPlane.GetComponentsInChildren<VrGrabPlane>()) { g.Retire(); Destroy(g); }
            foreach (var g in _playerPlane.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable>()) { g.enabled = false; Destroy(g); }
            foreach (var rb in _playerPlane.GetComponentsInChildren<Rigidbody>())
            {
                if (rb.gameObject != _playerPlane)
                    Destroy(rb);
            }

            // Hasta que FlightController tome posesión el cuerpo permanece cinemático y sin velocidad.
            var body = _playerPlane.GetComponent<Rigidbody>();
            if (body == null) body = _playerPlane.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.isKinematic = true;
            body.constraints = RigidbodyConstraints.None;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.interpolation = RigidbodyInterpolation.Interpolate;

            var playerHit = _playerPlane.GetComponent<PlayerHitbox>();
            if (playerHit == null) playerHit = _playerPlane.AddComponent<PlayerHitbox>();

            var shooter = _playerPlane.GetComponent<CombatShooter>();
            if (shooter == null) shooter = _playerPlane.AddComponent<CombatShooter>();
            shooter.Setup(SelectedPlane, bulletPrefab, missilePrefab, bombPrefab, sfxShoot);

            _playerPlane.transform.position = pos;
            _playerPlane.transform.rotation = rot;
        }

        void CleanupPlayer()
        {
            launchController?.Cancel();
            flightController?.Release();
            if (_playerPlane != null)
            {
                _playerPlane.SetActive(false);
                Destroy(_playerPlane);
            }
            _playerPlane = null;
            _playerBody = null;
        }

        public void PlaySfx(AudioClip clip, float vol = 0.5f)
        {
            if (clip == null || _audio == null) return;
            _audio.PlayOneShot(clip, vol);
        }

        public LevelDefinition CurrentLevel =>
            levels.Count == 0 ? null : levels[Mathf.Clamp(CurrentLevelIndex, 0, levels.Count - 1)];
    }
}
