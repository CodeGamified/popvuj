// Copyright SeaRäuber 2025-2026
// MIT License
using UnityEngine;
using SeaRauber.Scripting;

namespace SeaRauber.Ship
{
    /// <summary>
    /// Ship controller — the interface between human input / AI code and ship systems.
    /// 
    /// In the final game, ShipController is what the PLAYER'S CODE talks to.
    /// The scripting runtime (Phase 3) will expose:
    ///   ship.set_heading(90)
    ///   ship.set_sail(0.8)
    ///   ship.tack()
    ///   wind = ship.get_wind()
    ///   speed = ship.speed_knots
    ///   fold = ship.turbulence
    /// 
    /// For now, this component provides keyboard controls for testing:
    ///   A/D or ←/→  → turn port/starboard
    ///   W/S or ↑/↓  → adjust sail trim
    ///   T            → tack
    ///   G            → jibe
    ///   
    /// Also maintains the ship's "log" — a data stream of position,
    /// heading, speed, wind that will feed into the Ship's Log UI (Phase 3).
    /// </summary>
    [RequireComponent(typeof(ShipHull), typeof(ShipSail))]
    public class ShipController : MonoBehaviour
    {
        [Header("Control Mode")]
        [Tooltip("Enable keyboard controls for testing (disable for scripted ships)")]
        public bool keyboardControl = true;

        [Tooltip("Is this the player's flagship? (camera follows, UI reads from this)")]
        public bool isFlagship = true;

        [Header("Keyboard Settings")]
        [Tooltip("Degrees per keypress for turning")]
        public float turnIncrement = 5f;

        [Tooltip("Sail trim increment per keypress")]
        [Range(0.05f, 0.5f)]
        public float trimIncrement = 0.1f;

        [Header("Ship Identity")]
        public string shipName = "Unnamed Vessel";
        public Scripting.ShipClass shipClass = Scripting.ShipClass.Sloop;

        [Header("HUD (Read Only)")]
        [SerializeField] private string _speedDisplay;
        [SerializeField] private string _headingDisplay;
        [SerializeField] private string _windDisplay;
        [SerializeField] private string _sailDisplay;

        // Components
        private ShipHull _hull;
        private ShipSail _sail;

        // Ship log (circular buffer of recent states)
        private ShipLogEntry[] _logBuffer;
        private int _logIndex;
        private float _logTimer;
        private const int LOG_BUFFER_SIZE = 600; // 10 minutes at 1Hz
        private const float LOG_INTERVAL = 1f;    // 1 entry per second

        /// <summary>Most recent log entry</summary>
        public ShipLogEntry LastLogEntry => _logBuffer != null && _logBuffer.Length > 0
            ? _logBuffer[(_logIndex - 1 + LOG_BUFFER_SIZE) % LOG_BUFFER_SIZE]
            : default;

        /// <summary>Full log buffer (circular, newest at _logIndex-1)</summary>
        public ShipLogEntry[] LogBuffer => _logBuffer;
        public int LogIndex => _logIndex;

        private void Start()
        {
            _hull = GetComponent<ShipHull>();
            _sail = GetComponent<ShipSail>();

            _logBuffer = new ShipLogEntry[LOG_BUFFER_SIZE];
            _logIndex = 0;
            _logTimer = 0f;

            Debug.Log($"[SHIP] {shipName} ({shipClass}) ready. " +
                      $"Keyboard={keyboardControl}, Flagship={isFlagship}");
        }

        private void Update()
        {
            if (keyboardControl && !IsKeyboardConflict())
            {
                HandleKeyboardInput();
            }

            UpdateHUD();
            UpdateLog();
        }

        /// <summary>
        /// Check if camera is in a mode where WASD controls the camera,
        /// not the ship. Prevents double-binding of keys.
        /// </summary>
        private bool IsKeyboardConflict()
        {
            var cam = FindAnyObjectByType<Core.OceanCameraController>();
            if (cam == null) return false;
            // In free mode, WASD pans camera — ship shouldn't also respond
            // In follow/deck mode, camera uses mouse — WASD free for ship
            return cam.Mode == Core.CameraMode.Free;
        }

        // =================================================================
        // KEYBOARD CONTROLS
        // =================================================================

        private void HandleKeyboardInput()
        {
            // Turning
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
                _sail.TurnPort(turnIncrement * Time.deltaTime * 10f);

            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
                _sail.TurnStarboard(turnIncrement * Time.deltaTime * 10f);

            // Sail trim
            if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
                _sail.SetSailTrim(Mathf.Clamp01(GetCurrentTrim() + trimIncrement));

            if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
                _sail.SetSailTrim(Mathf.Clamp01(GetCurrentTrim() - trimIncrement));

            // Tacking
            if (Input.GetKeyDown(KeyCode.T))
            {
                _sail.Tack();
                Debug.Log($"[SHIP] {shipName}: TACKING!");
            }

            // Jibing
            if (Input.GetKeyDown(KeyCode.G))
            {
                _sail.Jibe();
                Debug.Log($"[SHIP] {shipName}: JIBING!");
            }
        }

        private float GetCurrentTrim()
        {
            // Read current sail force as a proxy for trim
            // (actual trim is private in ShipSail — clean API boundary)
            return _sail.SailForce / Mathf.Max(_sail.SailForce, 0.01f);
        }

        // =================================================================
        // HUD
        // =================================================================

        private void UpdateHUD()
        {
            _speedDisplay = $"{_sail.SpeedKnots:F1} kts";
            _headingDisplay = $"{_sail.Heading:F0}° {GetCompassBearing(_sail.Heading)}";
            _windDisplay = _sail.InDeadZone
                ? $"IN IRONS ({_sail.ApparentWindAngle:F0}°)"
                : $"AWA {_sail.ApparentWindAngle:F0}° @ {_sail.ApparentWindSpeed * 1.94384f:F0} kts";
            _sailDisplay = $"Force: {_sail.SailForce:P0}" +
                          (_hull.InRoughWater ? " ⚠ ROUGH" : "");
        }

        // =================================================================
        // SHIP LOG
        // =================================================================

        private void UpdateLog()
        {
            _logTimer += Time.deltaTime;
            if (_logTimer < LOG_INTERVAL) return;
            _logTimer -= LOG_INTERVAL;

            _logBuffer[_logIndex] = new ShipLogEntry
            {
                timestamp = Core.SimulationTime.Instance != null
                    ? (float)Core.SimulationTime.Instance.simulationTime
                    : Time.time,
                positionX = transform.position.x,
                positionZ = transform.position.z,
                heading = _sail.Heading,
                speed = _sail.Speed,
                apparentWindAngle = _sail.ApparentWindAngle,
                apparentWindSpeed = _sail.ApparentWindSpeed,
                sailForce = _sail.SailForce,
                fold = _hull.CurrentFold,
                heave = _hull.HeaveVelocity
            };

            _logIndex = (_logIndex + 1) % LOG_BUFFER_SIZE;
        }

        // =================================================================
        // SCRIPTING API (Phase 3 — what player code calls)
        // =================================================================
        // These methods are the contract between player scripts and the ship.
        // Naming matches the Python API that will be exposed.

        /// <summary>Set heading (degrees, 0=N, 90=E)</summary>
        public void SetHeading(float degrees) => _sail.SetHeading(degrees);

        /// <summary>Set sail trim (0=furled, 1=full)</summary>
        public void SetSail(float trim) => _sail.SetSailTrim(trim);

        /// <summary>Execute a tack maneuver</summary>
        public void ExecuteTack() => _sail.Tack();

        /// <summary>Execute a jibe maneuver</summary>
        public void ExecuteJibe() => _sail.Jibe();

        /// <summary>Get wind at ship position (x=direction component, y=direction component, magnitude=speed)</summary>
        public Vector2 GetWind() => Core.WindField.Instance != null
            ? Core.WindField.Instance.GetWindAt(transform.position)
            : Vector2.zero;

        /// <summary>Get current speed in knots</summary>
        public float GetSpeedKnots() => _sail.SpeedKnots;

        /// <summary>Get current heading</summary>
        public float GetHeading() => _sail.Heading;

        /// <summary>Get turbulence at ship (0=calm, 1=breaking)</summary>
        public float GetTurbulence() => _hull.CurrentFold;

        /// <summary>Get current sail trim (0-1)</summary>
        public float GetSailTrim() => _sail.SailTrim;

        /// <summary>Set sail trim (0=furled, 1=full) — aliased for scripting</summary>
        public void SetSailTrim(float trim) => _sail.SetSailTrim(trim);

        /// <summary>Tack — aliased for scripting API</summary>
        public void Tack() => _sail.Tack();

        /// <summary>Jibe — aliased for scripting API</summary>
        public void Jibe() => _sail.Jibe();

        /// <summary>Get VMG toward a bearing</summary>
        public float GetVMG(float bearing) => _sail.GetVMG(bearing);

        /// <summary>Get optimal tacking headings for upwind</summary>
        public (float port, float starboard) GetOptimalTack() => _sail.GetOptimalUpwindHeadings();

        // =================================================================
        // UTILITY
        // =================================================================

        private static string GetCompassBearing(float degrees)
        {
            string[] bearings = { "N", "NNE", "NE", "ENE", "E", "ESE", "SE", "SSE",
                                  "S", "SSW", "SW", "WSW", "W", "WNW", "NW", "NNW" };
            int index = Mathf.RoundToInt(degrees / 22.5f) % 16;
            if (index < 0) index += 16;
            return bearings[index];
        }
    }

    // =====================================================================
    // DATA STRUCTURES
    // (ShipClass is defined in SeaRauber.Scripting.ShipClass — single source of truth)
    // =====================================================================

    /// <summary>
    /// Single log entry — compact struct for circular buffer.
    /// This feeds into the Ship's Log UI and replay system.
    /// </summary>
    [System.Serializable]
    public struct ShipLogEntry
    {
        public float timestamp;       // Simulation time
        public float positionX;       // World X
        public float positionZ;       // World Z
        public float heading;         // Degrees
        public float speed;           // m/s
        public float apparentWindAngle; // Degrees
        public float apparentWindSpeed; // m/s
        public float sailForce;       // 0-1
        public float fold;            // Jacobian fold (turbulence)
        public float heave;           // Vertical velocity
    }
}
