// Copyright CodeGamified 2025-2026
// MIT License — PopVuj
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using CodeGamified.Audio;
using CodeGamified.Camera;
using CodeGamified.Time;
using CodeGamified.Settings;
using CodeGamified.Quality;
using CodeGamified.Bootstrap;
using PopVuj.Game;
using PopVuj.Scripting;
using PopVuj.AI;
using PopVuj.Crew;
using PopVuj.UI;

namespace PopVuj.Core
{
    /// <summary>
    /// Bootstrap for PopVuj — god-sim inspired by the Popol Vuh.
    ///
    /// Architecture:
    ///   - Multi-layer city: Sewers → Village → Heavens
    ///   - Camera Y = cosmology (zoom up to command, down to observe)
    ///   - Players WRITE CODE to issue divine decrees
    ///   - Minions are born, live, breed, worship, and die
    ///
    /// Attach to a GameObject. Press Play → city appears.
    /// </summary>
    public class PopVujBootstrap : GameBootstrap, IQualityResponsive
    {
        protected override string LogTag => "POPVUJ";

        // =================================================================
        // INSPECTOR
        // =================================================================

        [Header("City")]
        [Tooltip("City width in slots (linear strip)")]
        public int cityWidth = 24;

        [Header("Population")]
        [Tooltip("Starting minion count")]
        public int startingPopulation = 12;

        [Tooltip("Starting faith level (0-1)")]
        public float startingFaith = 0.5f;

        [Header("Match")]
        [Tooltip("Auto-restart after civilization collapse")]
        public bool autoRestart = true;

        [Tooltip("Delay before restarting (sim-seconds)")]
        public float restartDelay = 5f;

        [Header("Time")]
        public bool enableTimeScale = true;

        [Header("Scripting")]
        public bool enableScripting = true;

        [Header("Camera")]
        public bool configureCamera = true;

        // =================================================================
        // RUNTIME REFERENCES
        // =================================================================

        private CityGrid _city;
        private CityRenderer _renderer;
        private PopVujMatchManager _match;
        private MinionManager _minionManager;
        private MinionRenderer _minionRenderer;
        private StructureRenderer _structureRenderer;
        private HarborManager _harborManager;
        private ShipRenderer _shipRenderer;
        private WeatherRenderer _weatherRenderer;
        private DayNightRenderer _dayNightRenderer;
        private SkyRenderer _skyRenderer;
        private GodPowerVFX _godPowerVFX;
        private WildlifeManager _wildlifeManager;
        private PopVujProgram _playerProgram;
        private PopVujFateController _fateController;
        private PopVujTUIManager _tuiManager;

        // Audio
        private PopVujAudioProvider _audioProvider;
        private AudioBridge.EditorHandlers _editorAudio;
        private AudioBridge.EngineHandlers _engineAudio;
        private AudioBridge.TimeHandlers _timeAudio;
        private AudioBridge.PersistenceHandlers _persistAudio;
        private PopVujAmbientSFX _ambientSFX;
        private PopVujGameSFX _gameSFX;

        // Camera
        private CameraRig _cameraRig;

        // Post-processing
        private Bloom _bloom;
        private Volume _postProcessVolume;

        // =================================================================
        // UPDATE
        // =================================================================

        private void Update()
        {
            UpdateBloomScale();
        }

        private void UpdateBloomScale()
        {
            if (_bloom == null || !_bloom.active) return;
            var cam = Camera.main;
            if (cam == null) return;
            float dist = Vector3.Distance(cam.transform.position, CityCenter());
            float defaultDist = 20f;
            float scale = Mathf.Clamp01(defaultDist / Mathf.Max(dist, 0.01f));
            _bloom.intensity.value = Mathf.Lerp(0.5f, 1.0f, scale);
        }

        // =================================================================
        // BOOTSTRAP — mandatory wiring order
        // =================================================================

        private void Start()
        {
            Log("PopVuj Bootstrap starting...");

            // 1. Settings + Quality
            SettingsBridge.Load();
            QualityBridge.SetTier((QualityTier)SettingsBridge.QualityLevel);
            QualityBridge.Register(this);
            Log($"Settings loaded (Quality={SettingsBridge.QualityLevel})");

            // 2. Simulation time
            EnsureSimulationTime<PopVujSimulationTime>();

            // 3. Camera + post-processing
            SetupCamera();

            // 4. City grid (domain object)
            CreateCityGrid();

            // 5. Match manager (needs city)
            CreateMatchManager();

            // 5a. Harbor manager (needs city + match)
            CreateHarborManager();

            // 5b. Minion manager (needs city + match + harbor)
            CreateMinionManager();

            // 6. Visual renderer (needs city)
            CreateRenderer();

            // 6a. Structure interior renderer (needs city + procedural engine)
            CreateStructureRenderer();

            // 6b. Minion renderer (needs minion manager + city)
            CreateMinionRenderer();

            // 6c. Ship renderer (needs harbor manager + city)
            CreateShipRenderer();

            // 6d. Weather renderer (needs match + city)
            CreateWeatherRenderer();

            // 6d½. Sky renderer (properties-driven multi-layer sky)
            CreateSkyRenderer();

            // 6d¾. Day-night renderer (sun/moon arcs)
            CreateDayNightRenderer();

            // 6e. God power VFX (needs match + city + weather)
            CreateGodPowerVFX();

            // 6f. Wildlife (needs city + match)
            CreateWildlifeManager();

            // 7. Input provider
            CreateInputProvider();

            // 8. Player program (needs match + city)
            if (enableScripting) CreatePlayerProgram();

            // 8b. Fate controller (tarot card draws)
            CreateFateController();

            // 8c. TUI manager (left god + right fate + bottom status)
            CreateTUIManager();

            // 8d. Audio provider + bridge handlers
            CreateAudioProvider();

            // 8e. Ambient SFX (needs city + match + camera)
            CreateAmbientSFX();

            // 8f. Game-event SFX (needs match + harbor)
            CreateGameSFX();

            // 9. Wire events + start
            WireEvents();
            StartCoroutine(RunBootSequence());
        }

        public void OnQualityChanged(QualityTier tier)
        {
            Log($"Quality changed -> {tier}");
        }

        // =================================================================
        // CAMERA — cosmological vertical axis
        // =================================================================

        /// <summary>
        /// Center of the city strip — surface midpoint (X center, Y=0).
        /// Camera orbits this. Moving up = heavens. Moving down = sewers.
        /// </summary>
        private Vector3 CityCenter()
        {
            float w = cityWidth * CityRenderer.CellSize;
            return new Vector3(w * 0.5f, 0f, 0f);
        }

        private void SetupCamera()
        {
            if (!configureCamera) return;

            var cam = EnsureCamera();

            // Side-view camera — faces the XY plane from -Z
            // X = city width, Y = cosmological axis (sewers below, heavens above)
            // Scroll left/right to pan city, scroll up = ascend to heavens
            cam.orthographic = false;
            cam.fieldOfView = 50f;
            var center = CityCenter();
            cam.transform.position = center + new Vector3(0f, 4f, -16f);
            cam.transform.LookAt(center, Vector3.up);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.01f, 0.04f); // dark indigo — night sky
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 200f;

            // WASD camera rig — Free mode, side-view XY strip
            _cameraRig = cam.gameObject.AddComponent<CameraRig>();
            _cameraRig.enableWASDPan = true;
            _cameraRig.enableMiddleMousePan = true;
            _cameraRig.enableKeyboardRotate = false;    // Q/E reserved for zoom by InputProvider
            _cameraRig.panSpeed = 12f;
            _cameraRig.clampLookTargetY = false;         // allow vertical exploration (heavens/sewers)
            _cameraRig.verticalPanMode = true;              // W/S = Y (up/down), A/D = X (left/right)
            _cameraRig.minPitch = 5f;
            _cameraRig.maxPitch = 60f;
            _cameraRig.minZoomDistance = 4f;
            _cameraRig.maxZoomDistance = 60f;
            _cameraRig.zoomSpeed = 8f;
            _cameraRig.freeSmoothness = 0.88f;

            // Post-processing: bloom for divine glow
            var camData = cam.GetComponent<UniversalAdditionalCameraData>();
            if (camData == null)
                camData = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;

            var volumeGO = new GameObject("PostProcessVolume");
            _postProcessVolume = volumeGO.AddComponent<Volume>();
            _postProcessVolume.isGlobal = true;
            _postProcessVolume.priority = 1;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _bloom = profile.Add<Bloom>();
            _bloom.threshold.overrideState = true;
            _bloom.threshold.value = 0.8f;
            _bloom.intensity.overrideState = true;
            _bloom.intensity.value = 1.0f;
            _bloom.scatter.overrideState = true;
            _bloom.scatter.value = 0.5f;
            _bloom.clamp.overrideState = true;
            _bloom.clamp.value = 20f;
            _bloom.highQualityFiltering.overrideState = true;
            _bloom.highQualityFiltering.value = true;
            _postProcessVolume.profile = profile;

            Log("Camera: CameraRig (WASD/scroll) + ambient sway, side-view XY");
        }

        // =================================================================
        // CITY GRID
        // =================================================================

        private void CreateCityGrid()
        {
            var go = new GameObject("CityGrid");
            _city = go.AddComponent<CityGrid>();
            _city.Initialize(cityWidth);
            Log($"Created CityGrid (width={cityWidth}, linear strip)");
        }

        // =================================================================
        // MATCH MANAGER
        // =================================================================

        private void CreateMatchManager()
        {
            var go = new GameObject("MatchManager");
            _match = go.AddComponent<PopVujMatchManager>();
            _match.Initialize(_city, startingPopulation, startingFaith, autoRestart, restartDelay);
            Log($"Created MatchManager (pop={startingPopulation}, faith={startingFaith:F1})");
        }

        // =================================================================
        // RENDERER
        // =================================================================

        private void CreateRenderer()
        {
            _renderer = _city.gameObject.AddComponent<CityRenderer>();
            _renderer.Initialize(_city);
            Log("Created CityRenderer (2.5D cubes, 10% alpha placeholders)");
        }

        // =================================================================
        // STRUCTURE RENDERER (procedural interiors)
        // =================================================================

        private void CreateStructureRenderer()
        {
            var go = new GameObject("StructureRenderer");
            _structureRenderer = go.AddComponent<StructureRenderer>();
            _structureRenderer.Initialize(_city);
            Log("Created StructureRenderer (procedural building + sewer interiors)");
        }

        // =================================================================
        // MINION MANAGER
        // =================================================================

        private void CreateMinionManager()
        {
            var go = new GameObject("MinionManager");
            _minionManager = go.AddComponent<MinionManager>();
            _minionManager.Initialize(_city, _match, _harborManager);
            Log($"Created MinionManager (slot-based minion AI + harbor jobs)");
        }

        // =================================================================
        // HARBOR MANAGER
        // =================================================================

        private void CreateHarborManager()
        {
            var go = new GameObject("HarborManager");
            _harborManager = go.AddComponent<HarborManager>();
            _harborManager.Initialize(_city, _match);
            _match.SetHarbor(_harborManager);
            Log("Created HarborManager (ships, trade routes, crane ops)");
        }

        // =================================================================
        // MINION RENDERER
        // =================================================================

        private void CreateMinionRenderer()
        {
            var go = new GameObject("MinionRenderer");
            _minionRenderer = go.AddComponent<MinionRenderer>();
            _minionRenderer.Initialize(_minionManager, _city);
            Log("Created MinionRenderer");
        }

        // =================================================================
        // SHIP RENDERER
        // =================================================================

        private void CreateShipRenderer()
        {
            var go = new GameObject("ShipRenderer");
            _shipRenderer = go.AddComponent<ShipRenderer>();
            _shipRenderer.Initialize(_harborManager, _city);
            Log("Created ShipRenderer (modular ship visuals)");
        }

        // =================================================================
        // WEATHER RENDERER
        // =================================================================

        private void CreateWeatherRenderer()
        {
            var go = new GameObject("WeatherRenderer");
            _weatherRenderer = go.AddComponent<WeatherRenderer>();
            _weatherRenderer.Initialize(_match, _city);
            Log("Created WeatherRenderer (rain, snow, storm, drought particles)");
        }

        // =================================================================
        // SKY RENDERER
        // =================================================================

        private void CreateSkyRenderer()
        {
            var go = new GameObject("SkyRenderer");
            _skyRenderer = go.AddComponent<SkyRenderer>();
            _skyRenderer.Initialize(_match, _city);
            Log("Created SkyRenderer (properties-driven multi-layer sky)");
        }

        // =================================================================
        // DAY-NIGHT RENDERER
        // =================================================================

        private void CreateDayNightRenderer()
        {
            var go = new GameObject("DayNightRenderer");
            _dayNightRenderer = go.AddComponent<DayNightRenderer>();
            _dayNightRenderer.Initialize(_city);
            Log("Created DayNightRenderer (sun arc + moon phases + sky tint)");
        }

        // =================================================================
        // GOD POWER VFX
        // =================================================================

        private void CreateGodPowerVFX()
        {
            var go = new GameObject("GodPowerVFX");
            _godPowerVFX = go.AddComponent<GodPowerVFX>();
            _godPowerVFX.Initialize(_match, _city, _weatherRenderer);
            Log("Created GodPowerVFX (smite, prophet, omen, bears)");
        }

        // =================================================================
        // WILDLIFE
        // =================================================================

        private void CreateWildlifeManager()
        {
            var go = new GameObject("WildlifeManager");
            _wildlifeManager = go.AddComponent<WildlifeManager>();
            _wildlifeManager.Initialize(_city, _match);
            Log("Created WildlifeManager (birds, rats, bats, toads)");
        }

        // =================================================================
        // INPUT PROVIDER
        // =================================================================

        private void CreateInputProvider()
        {
            var go = new GameObject("InputProvider");
            go.AddComponent<PopVujInputProvider>();
            Log("Created InputProvider");
        }

        // =================================================================
        // PLAYER SCRIPTING
        // =================================================================

        private void CreatePlayerProgram()
        {
            var go = new GameObject("DeityProgram");
            _playerProgram = go.AddComponent<PopVujProgram>();
            _playerProgram.Initialize(_match, _city);
            Log("Created DeityProgram (code-controlled god)");
        }

        // =================================================================
        // FATE CONTROLLER (tarot card draws)
        // =================================================================

        private void CreateFateController()
        {
            var go = new GameObject("FateController");
            _fateController = go.AddComponent<PopVujFateController>();
            _fateController.Initialize(_match, _city, FateDifficulty.Fickle);
            Log($"Created FateController (difficulty={_fateController.Difficulty})");
        }

        // =================================================================
        // TUI MANAGER
        // =================================================================

        private void CreateTUIManager()
        {
            var go = new GameObject("TUIManager");
            _tuiManager = go.AddComponent<PopVujTUIManager>();
            _tuiManager.Initialize(_match, _playerProgram, _fateController);
            Log("Created TUIManager (god debugger + fate debugger + status panel)");
        }

        // =================================================================
        // AUDIO
        // =================================================================

        private void CreateAudioProvider()
        {
            _audioProvider = new PopVujAudioProvider();
            Func<float> ts = () => SimulationTime.Instance?.timeScale ?? 1f;
            _editorAudio  = AudioBridge.ForEditor(_audioProvider, ts);
            _engineAudio  = AudioBridge.ForEngine(_audioProvider, ts);
            _timeAudio    = AudioBridge.ForTime(_audioProvider, ts);
            _persistAudio = AudioBridge.ForPersistence(_audioProvider, ts);
            Log("Created PopVujAudioProvider + bridge handlers");
        }

        // =================================================================
        // AMBIENT SFX
        // =================================================================

        private void CreateAmbientSFX()
        {
            var go = new GameObject("AmbientSFX");
            _ambientSFX = go.AddComponent<PopVujAmbientSFX>();
            _ambientSFX.Initialize(_city, _match);
            Log("Created AmbientSFX (zone-based loops + weather overlay + wildlife stingers)");
        }

        // =================================================================
        // GAME-EVENT SFX
        // =================================================================

        private void CreateGameSFX()
        {
            var go = new GameObject("GameSFX");
            _gameSFX = go.AddComponent<PopVujGameSFX>();
            _gameSFX.Initialize();
            Log("Created GameSFX (death, smite, trade, construction)");
        }

        // =================================================================
        // EVENT WIRING
        // =================================================================

        private void WireEvents()
        {
            // Track previous population for death detection
            int prevPop = startingPopulation;

            if (SimulationTime.Instance != null)
            {
                SimulationTime.Instance.OnTimeScaleChanged += s => Log($"Time scale -> {s:F0}x");
                SimulationTime.Instance.OnPausedChanged += p => Log(p ? "PAUSED" : "RESUMED");
            }

            if (_match != null)
            {
                _match.OnMatchStarted += () =>
                {
                    Log("CIVILIZATION FOUNDED");
                    _renderer?.MarkDirty();
                    prevPop = startingPopulation;
                };

                _match.OnPopulationChanged += pop =>
                {
                    _renderer?.MarkDirty();
                    _gameSFX?.OnPopulationChanged(pop, prevPop);
                    prevPop = pop;
                };

                _match.OnFaithChanged += faith =>
                {
                    _renderer?.MarkDirty();
                };

                _match.OnGameOver += () =>
                {
                    Log($"CIVILIZATION COLLAPSED | Pop: {_match.Population} | Faith: {_match.Faith:F2}");
                    if (autoRestart)
                        StartCoroutine(RestartAfterDelay());
                };

                _match.OnBoardChanged += () =>
                {
                    _renderer?.MarkDirty();
                    _gameSFX?.OnBoardChanged();
                };

                // VFX events → game SFX
                _match.OnSmiteTriggered   += () => _gameSFX?.OnSmite();
                _match.OnBearsSummoned    += kills => _gameSFX?.OnBearsSummoned(kills);
                _match.OnOmenSent         += () => _gameSFX?.OnOmen();
            }

            if (_harborManager != null)
            {
                _harborManager.OnShipReturned += ship => _gameSFX?.OnShipReturned(ship);
            }
        }

        // =================================================================
        // BOOT SEQUENCE
        // =================================================================

        private IEnumerator RunBootSequence()
        {
            yield return null;
            yield return null;

            LogDivider();
            Log("POPVUJ — Bend Fate");
            LogDivider();
            LogStatus("CITY", $"width={cityWidth}");
            LogStatus("POPULATION", $"{startingPopulation}");
            LogStatus("FAITH", $"{startingFaith:F1}");
            LogEnabled("SCRIPTING", enableScripting);
            LogEnabled("TIME SCALE", enableTimeScale);
            LogEnabled("AUTO RESTART", autoRestart);
            LogDivider();

            _match.StartMatch();
            _harborManager?.SpawnDemoShips();
            Log("First civilization started — shape them.");
        }

        private IEnumerator RestartAfterDelay()
        {
            float waited = 0f;
            while (waited < restartDelay)
            {
                if (SimulationTime.Instance != null && !SimulationTime.Instance.isPaused)
                    waited += Time.deltaTime * (SimulationTime.Instance?.timeScale ?? 1f);
                yield return null;
            }

            _match.StartMatch();
            _harborManager?.SpawnDemoShips();
            _playerProgram?.ResetExecution();
            Log("New civilization started");
        }

        private void OnDestroy()
        {
            QualityBridge.Unregister(this);
        }
    }
}
