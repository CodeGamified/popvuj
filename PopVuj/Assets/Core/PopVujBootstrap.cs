// Copyright CodeGamified 2025-2026
// MIT License — PopVuj
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using CodeGamified.Camera;
using CodeGamified.Time;
using CodeGamified.Settings;
using CodeGamified.Quality;
using CodeGamified.Bootstrap;
using PopVuj.Game;
using PopVuj.Scripting;
using PopVuj.AI;
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
        private PopVujProgram _playerProgram;
        private PopVujFateController _fateController;
        private PopVujTUIManager _tuiManager;

        // Camera
        private CameraAmbientMotion _cameraSway;

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

            // 6. Visual renderer (needs city)
            CreateRenderer();

            // 7. Input provider
            CreateInputProvider();

            // 8. Player program (needs match + city)
            if (enableScripting) CreatePlayerProgram();

            // 8b. Fate controller (tarot card draws)
            CreateFateController();

            // 8c. TUI manager (left god + right fate + bottom status)
            CreateTUIManager();

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
            cam.transform.position = center + new Vector3(0f, 2f, -8f);
            cam.transform.LookAt(center, Vector3.up);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.01f, 0.04f); // dark indigo — night sky
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 200f;

            // Ambient sway — god's lazy drift
            _cameraSway = cam.gameObject.AddComponent<CameraAmbientMotion>();
            _cameraSway.lookAtTarget = center;

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

            Log("Camera: side-view, XY plane, heavens above, sewers below");
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
            Log("Created CityRenderer (2.5D cubes)");
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
        // EVENT WIRING
        // =================================================================

        private void WireEvents()
        {
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
                };

                _match.OnPopulationChanged += pop =>
                {
                    _renderer?.MarkDirty();
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

                _match.OnBoardChanged += () => _renderer?.MarkDirty();
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
            _playerProgram?.ResetExecution();
            Log("New civilization started");
        }

        private void OnDestroy()
        {
            QualityBridge.Unregister(this);
        }
    }
}
