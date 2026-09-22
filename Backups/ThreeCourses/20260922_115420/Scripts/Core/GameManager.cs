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
        public WeaponDefinition Weapon(WeaponType type) => weapons.Find(w => w != null && w.type == type);
        public PowerUpDefinition PowerUp(PowerUpType type) => powerUps.Find(p => p != null && p.type == type);
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

        bool _vrTrigR, _vrTrigL, _vrPrimary;

        public GameState State { get; private set; } = GameState.MainMenu;
        public PlaneDefinition SelectedPlane { get; private set; }
        public int CurrentLevelIndex { get; private set; }
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
        public string EndReason { get; private set; } = "";
        public List<string> NewlyUnlocked { get; } = new();
        public Transform Player => _playerPlane != null ? _playerPlane.transform : null;
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
            GoToMainMenu();
        }

        void Update()
        {
            TickPowerUps();

            bool vrConfirm = VrInput.AnyConfirmDown(ref _vrTrigR, ref _vrTrigL, ref _vrPrimary);

            switch (State)
            {
                case GameState.MainMenu:
                    if (GameInput.ConfirmDown() || vrConfirm)
                        StartPlaneSelect();
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

            if (GameInput.IsDown(Key.F1))
                Time.timeScale = Time.timeScale > 0.5f ? 0f : 1f;
        }

        public void GoToMainMenu()
        {
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
            ClearPowerUps();
            hud?.Refresh();
        }

        public void StartPlaneSelect()
        {
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
            SelectedPlane = def;
            planeSelector?.SelectGrabbed(def);
            _tutorial?.Record(FlightTutorial.Step.Grab);
            PlaneGrabbed?.Invoke(def);
            if (State == GameState.PlaneSelect)
                State = GameState.Launch;
            hud?.Refresh();
        }

        public void NotifyVrPlaneReturned()
        {
            if (State == GameState.Launch) State = GameState.PlaneSelect;
            planeSelector?.ReturnToTable();
            hud?.Refresh();
        }

        public void ConfirmVrPlaneSelection(PlaneDefinition def)
        {
            if (def != null) SelectedPlane = def;
            planeSelector?.Clear();
            if (State == GameState.PlaneSelect || State == GameState.Launch)
                BeginLaunch();
        }

        public void BeginFlightFromVrThrow(PlaneDefinition def, Vector3 velocity, Vector3? releasePosition = null)
        {
            if (State != GameState.PlaneSelect && State != GameState.Launch) return;
            if (velocity.sqrMagnitude < 0.01f || CurrentLevel == null || !IsPlaneUnlocked(def)) return;
            if (def != null) SelectedPlane = def;
            planeSelector?.Clear();
            if (SelectedPlane == null) return;

            if (_playerPlane == null)
            {
                velocity = AssistedLaunch(velocity);
                SpawnPlayerAt(
                    FlightStart,
                    Quaternion.LookRotation(velocity.normalized),
                    forLaunch: false);
            }
            BeginFlight(velocity);
        }

        public void BeginFlightFromVrThrow(Vector3 velocity)
        {
            BeginFlightFromVrThrow(SelectedPlane, velocity);
        }

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
            FinishRun(true);
            EndReason = "RECORRIDO COMPLETADO";
            hud?.Refresh();
        }

        void NextLevelOrFinish()
        {
            CurrentLevelIndex++;
            if (CurrentLevelIndex >= levels.Count)
            {
                State = GameState.GameOver; // victoria final reutiliza pantalla con mensaje
                hud?.SetVictory(true);
                hud?.Refresh();
                return;
            }
            ClearPowerUps();
            BeginLaunch();
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
            CurrentLevelIndex = 0;
            Score = 0;
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

            // quitar demos
            foreach (var d in _playerPlane.GetComponentsInChildren<PlaneFlightDemo>())
                Destroy(d);
            foreach (var s in _playerPlane.GetComponentsInChildren<SpinBob>())
                Destroy(s);
            foreach (var rb in _playerPlane.GetComponentsInChildren<Rigidbody>())
            {
                if (rb.gameObject != _playerPlane)
                    Destroy(rb);
            }

            var body = _playerPlane.GetComponent<Rigidbody>();
            if (body == null) body = _playerPlane.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.isKinematic = forLaunch;
            body.collisionDetectionMode = forLaunch ? CollisionDetectionMode.ContinuousSpeculative : CollisionDetectionMode.ContinuousDynamic;
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
            flightController?.Release();
            if (_playerPlane != null) Destroy(_playerPlane);
            _playerPlane = null;
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
