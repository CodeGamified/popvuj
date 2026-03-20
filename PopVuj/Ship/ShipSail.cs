// Copyright SeaRäuber 2025-2026
// MIT License
using UnityEngine;

namespace SeaRauber.Ship
{
    /// <summary>
    /// Sail physics — converts wind into ship thrust.
    /// 
    /// This is the CORE of SeaRäuber's gameplay loop.
    /// Players write code that reads WindField and sets heading to optimize speed.
    /// Understanding apparent wind and polar diagrams IS the game.
    /// 
    /// HIGH-FIDELITY MODEL:
    ///   Each sail is an individual component with its own state:
    ///     - Furled / Set / Reefed / Torn / Luffing / Aback
    ///     - Condition (wear and battle damage)
    ///     - Sheet trim (how the sail is angled to catch wind)
    ///   
    ///   Each mast has standing rigging (shrouds, stays, ratlines)
    ///   and an optional crow's nest for lookout crew.
    ///   
    ///   Running rigging (sheets, halyards, braces, clews, reef tackles)
    ///   are individual crew stations — a rigger must haul a line to
    ///   raise a sail, trim a sheet, or reef in a storm.
    /// 
    /// Physics:
    ///   Apparent Wind = True Wind - Ship Velocity
    ///   
    ///   Each sail contributes force based on:
    ///     1. Its type (square vs fore-and-aft have different polar curves)
    ///     2. Its effective area (full area × condition × reef × state)
    ///     3. Its sheet trim (mismatched trim → luffing, lost power)
    ///     4. Apparent wind angle (the polar diagram)
    ///   
    ///   Total thrust = sum of individual sail contributions.
    ///   Torn sails and sails aback produce DRAG (negative thrust).
    /// 
    /// Dead zone ("no-go zone"): 
    ///   A real sailboat can't sail within ~30-45° of the wind.
    ///   Fore-and-aft rigs have a tighter dead zone than square rigs.
    ///   To go upwind, you must TACK — zig-zag at 45° angles.
    /// 
    /// Signal path:
    ///   WindField.GetWindAt(shipPos) → apparent wind
    ///     → per-sail polar curve × effective area → per-sail thrust
    ///     → sum → aggregate thrust → velocity
    /// </summary>
    [RequireComponent(typeof(ShipHull))]
    public class ShipSail : MonoBehaviour
    {
        [Header("Sail Configuration")]
        [Tooltip("Sail area in m² — overridden by rig plan if present")]
        public float sailArea = 25f;

        [Tooltip("Sail efficiency (0-1) — accounts for rigging, hull shape, etc.")]
        [Range(0f, 1f)]
        public float sailEfficiency = 0.6f;

        [Header("Polar Diagram")]
        [Tooltip("Dead zone half-angle in degrees — can't sail closer to wind than this")]
        [Range(15f, 60f)]
        public float deadZoneAngle = 40f;

        [Tooltip("Best angle for maximum speed (beam reach, typically ~90°)")]
        [Range(60f, 120f)]
        public float bestAngle = 90f;

        [Tooltip("Running efficiency (fraction of beam reach force at 180° downwind)")]
        [Range(0.3f, 1f)]
        public float runningEfficiency = 0.65f;

        [Header("Ship Performance")]
        [Tooltip("Maximum speed in m/s (about 8 knots for a small square-rigger)")]
        public float maxSpeed = 4.2f;

        [Tooltip("Water drag coefficient — resistance increases with speed²")]
        public float dragCoefficient = 0.15f;

        [Tooltip("Turning rate in degrees per second at full speed")]
        public float turnRate = 25f;

        [Tooltip("How much speed reduces turning (heavier ships turn slower at speed)")]
        [Range(0f, 1f)]
        public float speedTurnReduction = 0.3f;

        [Header("Storm Damage")]
        [Tooltip("Wind speed (m/s) above which sails risk tearing")]
        public float tearWindThreshold = 12f;

        [Tooltip("Chance per second of a sail tearing in excessive wind (per sail)")]
        [Range(0f, 0.1f)]
        public float tearChancePerSecond = 0.02f;

        [Header("State (Read Only)")]
        [SerializeField] private float _currentSpeed;
        [SerializeField] private float _currentHeading; // degrees, 0=N
        [SerializeField] private float _apparentWindAngle; // degrees relative to heading
        [SerializeField] private float _apparentWindSpeed; // m/s
        [SerializeField] private float _sailForce; // normalized 0-1
        [SerializeField] private float _effectiveSailArea; // m² actually drawing
        [SerializeField] private int _sailsSet; // count of sails currently set
        [SerializeField] private int _sailsTorn; // count of torn sails

        // --- Rig ---
        private SailRigPlan _rig;

        // --- Accessors ---
        /// <summary>Current ship speed in m/s</summary>
        public float Speed => _currentSpeed;

        /// <summary>Current ship speed in knots</summary>
        public float SpeedKnots => _currentSpeed * 1.94384f;

        /// <summary>Current heading in degrees (0=N, 90=E)</summary>
        public float Heading => _currentHeading;

        /// <summary>Apparent wind angle relative to ship heading (0=headwind, 180=tailwind)</summary>
        public float ApparentWindAngle => _apparentWindAngle;

        /// <summary>Apparent wind speed in m/s</summary>
        public float ApparentWindSpeed => _apparentWindSpeed;

        /// <summary>Current sail force (0=no power, 1=maximum polar curve output)</summary>
        public float SailForce => _sailForce;

        /// <summary>Is the ship in the dead zone (too close to wind)?</summary>
        public bool InDeadZone => Mathf.Abs(_apparentWindAngle) < deadZoneAngle;

        /// <summary>Current sail trim (0=furled, 1=full sail). Legacy/aggregate.</summary>
        public float SailTrim => _sailTrim;

        /// <summary>The full sail rig plan (masts, sails, rigging lines).</summary>
        public SailRigPlan Rig => _rig;

        /// <summary>Effective sail area currently drawing wind (m²).</summary>
        public float EffectiveSailArea => _effectiveSailArea;

        /// <summary>Number of sails currently set.</summary>
        public int SailsSetCount => _sailsSet;

        /// <summary>Number of torn sails.</summary>
        public int SailsTornCount => _sailsTorn;

        /// <summary>Does any mast have a crow's nest?</summary>
        public bool HasCrowsNest
        {
            get
            {
                if (_rig?.Masts == null) return false;
                for (int i = 0; i < _rig.Masts.Length; i++)
                    if (_rig.Masts[i].HasCrowsNest) return true;
                return false;
            }
        }

        /// <summary>Lookout range bonus from crow's nests (taller = further).</summary>
        public float LookoutRangeBonus
        {
            get
            {
                if (_rig?.Masts == null) return 0f;
                float best = 0f;
                for (int i = 0; i < _rig.Masts.Length; i++)
                    if (_rig.Masts[i].HasCrowsNest)
                        best = Mathf.Max(best, _rig.Masts[i].Height * _rig.Masts[i].CrowsNestHeight);
                return best;
            }
        }

        // --- Control inputs (set by ShipController or player code) ---
        private float _targetHeading;
        private float _sailTrim = 1f; // 0 = furled, 1 = full sail

        // --- Internal ---
        private ShipHull _hull;
        private Core.WindField _windField;
        private float _stormDamageTimer;

        private void Start()
        {
            _hull = GetComponent<ShipHull>();
            _windField = Core.WindField.Instance;

            if (_windField == null)
            {
                Debug.LogError("[SHIP] No WindField found! ShipSail requires wind.");
                enabled = false;
                return;
            }

            _currentHeading = transform.eulerAngles.y;
            _targetHeading = _currentHeading;
        }

        /// <summary>
        /// Initialize the high-fidelity sail rig from a ship class.
        /// Called by bootstrap after construction.
        /// </summary>
        public void InitializeRig(Scripting.ShipClass shipClass)
        {
            _rig = SailRigPlan.ForClass(shipClass);
            sailArea = _rig.TotalFullArea;

            // Set all sails by default
            SetAllSails(SailState.Set);

            Debug.Log($"[SAIL] Rig initialized: {_rig.Masts.Length} masts, " +
                      $"{_rig.Sails.Length} sails ({sailArea:F0} m²), " +
                      $"{_rig.Lines.Length} rigging lines");
        }

        /// <summary>
        /// Initialize from a pre-built rig plan.
        /// </summary>
        public void InitializeRig(SailRigPlan plan)
        {
            _rig = plan;
            sailArea = _rig.TotalFullArea;
            SetAllSails(SailState.Set);
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            // Pause check
            if (Core.SimulationTime.Instance != null && Core.SimulationTime.Instance.isPaused)
                return;

            UpdateHeading(dt);
            ComputeApparentWind();
            UpdateSailStates(dt);
            ComputeSailForce();
            ApplyThrust(dt);
            ApplyDrag(dt);
            MoveShip(dt);
        }

        // =================================================================
        // HEADING
        // =================================================================

        private void UpdateHeading(float dt)
        {
            // Calculate turn rate — reduced at high speed (momentum)
            float speedFactor = 1f - (_currentSpeed / maxSpeed) * speedTurnReduction;
            float effectiveTurnRate = turnRate * speedFactor;

            // Shortest rotation toward target heading
            float delta = Mathf.DeltaAngle(_currentHeading, _targetHeading);
            float maxTurn = effectiveTurnRate * dt;
            float turn = Mathf.Clamp(delta, -maxTurn, maxTurn);

            _currentHeading = (_currentHeading + turn) % 360f;
            if (_currentHeading < 0f) _currentHeading += 360f;

            // Apply yaw rotation (ShipHull handles pitch/roll from waves)
            Vector3 euler = transform.eulerAngles;
            euler.y = _currentHeading;
            transform.eulerAngles = euler;
        }

        // =================================================================
        // APPARENT WIND
        // =================================================================

        /// <summary>
        /// Apparent wind = true wind - ship velocity.
        /// This is what the sail actually "feels".
        /// A ship moving into the wind increases apparent wind speed.
        /// A ship running downwind decreases it.
        /// </summary>
        private void ComputeApparentWind()
        {
            // True wind at ship position
            Vector2 trueWind = _windField.GetWindAt(transform.position);

            // Ship velocity as 2D vector
            float headingRad = _currentHeading * Mathf.Deg2Rad;
            Vector2 shipVelocity = new Vector2(
                Mathf.Sin(headingRad),
                Mathf.Cos(headingRad)
            ) * _currentSpeed;

            // Apparent wind = true wind - ship velocity
            // (wind is where it blows FROM, velocity is where ship GOES)
            Vector2 apparentWind = trueWind - shipVelocity;

            _apparentWindSpeed = apparentWind.magnitude;

            // Angle between apparent wind and ship heading (0° = headwind, 180° = tailwind)
            if (_apparentWindSpeed > 0.01f)
            {
                float windAngle = Mathf.Atan2(apparentWind.x, apparentWind.y) * Mathf.Rad2Deg;
                _apparentWindAngle = Mathf.DeltaAngle(_currentHeading, windAngle);
            }
            else
            {
                _apparentWindAngle = 0f;
            }
        }

        // =================================================================
        // SAIL STATE MANAGEMENT
        // =================================================================

        /// <summary>
        /// Tick per-sail states: detect luffing, check for storm damage,
        /// auto-detect aback sails after tacking.
        /// </summary>
        private void UpdateSailStates(float dt)
        {
            if (_rig?.Sails == null) return;

            float absAngle = Mathf.Abs(_apparentWindAngle);
            _sailsSet = 0;
            _sailsTorn = 0;

            for (int i = 0; i < _rig.Sails.Length; i++)
            {
                ref var sail = ref _rig.Sails[i];

                if (sail.State == SailState.Torn) { _sailsTorn++; continue; }
                if (sail.State == SailState.Furled) continue;

                _sailsSet++;

                // Detect luffing: sail trimmed wrong for current wind angle
                float optimalTrim = OptimalSheetTrim(absAngle, sail.Type);
                float trimError = Mathf.Abs(sail.SheetTrim - optimalTrim);
                if (trimError > 0.4f && sail.State == SailState.Set)
                    sail.State = SailState.Luffing;
                else if (trimError <= 0.4f && sail.State == SailState.Luffing)
                    sail.State = SailState.Set;

                // Detect aback: wind on wrong side (crossing dead zone during tack)
                if (absAngle < deadZoneAngle * 0.5f && sail.State == SailState.Set)
                    sail.State = SailState.Aback;
                else if (absAngle >= deadZoneAngle && sail.State == SailState.Aback)
                    sail.State = SailState.Set;
            }

            // Storm damage tick
            CheckStormDamage(dt);
        }

        /// <summary>
        /// Optimal sheet trim for current wind angle and sail type.
        /// Close-hauled → sheets tight (0), running → sheets eased (1).
        /// </summary>
        private float OptimalSheetTrim(float absWindAngle, SailType type)
        {
            // Fore-and-aft rigs need tighter trim at all angles
            float offset = (type == SailType.ForeAndAft || type == SailType.Jib) ? -0.1f : 0f;
            float t = Mathf.InverseLerp(deadZoneAngle, 180f, absWindAngle);
            return Mathf.Clamp01(t + offset);
        }

        /// <summary>
        /// In heavy wind, set sails risk tearing.
        /// Reefed sails are safer. Furled sails are immune.
        /// Crew condition affects tear threshold.
        /// </summary>
        private void CheckStormDamage(float dt)
        {
            if (_rig?.Sails == null) return;
            if (_apparentWindSpeed < tearWindThreshold) return;

            _stormDamageTimer += dt;
            if (_stormDamageTimer < 1f) return;
            _stormDamageTimer -= 1f;

            float excessWind = (_apparentWindSpeed - tearWindThreshold) / tearWindThreshold;

            for (int i = 0; i < _rig.Sails.Length; i++)
            {
                ref var sail = ref _rig.Sails[i];
                if (sail.State == SailState.Furled || sail.State == SailState.Torn) continue;

                // Reefed sails are more resilient
                float reefProtection = sail.State == SailState.Reefed ? 0.3f : 1f;
                // Worn sails tear more easily
                float conditionRisk = Mathf.Lerp(3f, 1f, sail.Condition);
                float tearRoll = Random.value;
                float tearThreshold = tearChancePerSecond * excessWind * reefProtection * conditionRisk;

                if (tearRoll < tearThreshold)
                {
                    sail.State = SailState.Torn;
                    sail.Condition = Mathf.Max(0f, sail.Condition - 0.3f);
                    Debug.LogWarning($"[SAIL] {sail.Name} TORN by storm! Condition: {sail.Condition:P0}");
                }
            }
        }

        // =================================================================
        // POLAR DIAGRAM (per-sail)
        // =================================================================

        /// <summary>
        /// Compute aggregate sail force from all individual sails.
        /// Each sail type has its own polar curve characteristics:
        ///   - Square rigs: best on broad reach/running, poor close-hauled
        ///   - Fore-and-aft: excellent close-hauled, moderate running
        ///   - Jibs/staysails: fine-tune upwind performance
        /// </summary>
        private void ComputeSailForce()
        {
            // No rig → fall back to legacy single-sail model
            if (_rig?.Sails == null || _rig.Sails.Length == 0)
            {
                ComputeSailForceLegacy();
                return;
            }

            float absAngle = Mathf.Abs(_apparentWindAngle);
            float totalForce = 0f;
            float totalArea = 0f;
            _effectiveSailArea = 0f;

            for (int i = 0; i < _rig.Sails.Length; i++)
            {
                ref var sail = ref _rig.Sails[i];
                float area = sail.EffectiveArea;
                _effectiveSailArea += Mathf.Max(0f, area);

                if (Mathf.Abs(area) < 0.01f) continue;

                // Per-sail polar curve
                float force = SailPolarForce(absAngle, sail.Type, sail.SheetTrim);

                // Negative area (torn/aback) produces drag
                totalForce += force * area;
                totalArea += Mathf.Abs(area);
            }

            if (totalArea > 0.01f)
            {
                // Normalize: force per unit area, then scale by efficiency
                _sailForce = (totalForce / totalArea) * sailEfficiency;
                // Modulate by aggregate trim (legacy compatibility)
                _sailForce *= _sailTrim;
            }
            else
            {
                _sailForce = 0f;
            }
        }

        /// <summary>
        /// Polar curve for a specific sail type.
        /// Square and fore-and-aft have fundamentally different curves.
        /// </summary>
        private float SailPolarForce(float absAngle, SailType type, float sheetTrim)
        {
            float dz, peak, runEff;

            switch (type)
            {
                case SailType.Square:
                    // Square rigs: wider dead zone, peak at ~120° (broad reach), strong running
                    dz = deadZoneAngle + 10f;
                    peak = 120f;
                    runEff = 0.9f;
                    break;
                case SailType.ForeAndAft:
                case SailType.Spanker:
                    // Fore-and-aft: tighter dead zone, peak at ~75° (close reach), weaker running
                    dz = Mathf.Max(25f, deadZoneAngle - 10f);
                    peak = 75f;
                    runEff = 0.45f;
                    break;
                case SailType.Jib:
                case SailType.Staysail:
                    // Jibs: very tight dead zone, peak at ~60°, falls off quickly past beam
                    dz = Mathf.Max(20f, deadZoneAngle - 15f);
                    peak = 60f;
                    runEff = 0.25f;
                    break;
                case SailType.Lateen:
                    // Lateen: versatile, moderate all-around
                    dz = deadZoneAngle - 5f;
                    peak = 80f;
                    runEff = 0.55f;
                    break;
                default:
                    dz = deadZoneAngle;
                    peak = bestAngle;
                    runEff = runningEfficiency;
                    break;
            }

            if (absAngle < dz) return 0f;

            float force;
            if (absAngle <= peak)
            {
                float t = (absAngle - dz) / (peak - dz);
                force = t * t;
            }
            else
            {
                float t = (absAngle - peak) / (180f - peak);
                force = Mathf.Lerp(1f, runEff, t);
            }

            // Sheet trim penalty: wrong trim reduces force
            float optimalTrim = Mathf.InverseLerp(dz, 180f, absAngle);
            float trimPenalty = 1f - Mathf.Abs(sheetTrim - optimalTrim) * 0.6f;

            return force * Mathf.Max(0.1f, trimPenalty);
        }

        /// <summary>Legacy single-sail polar (fallback when no rig is initialized).</summary>
        private void ComputeSailForceLegacy()
        {
            float absAngle = Mathf.Abs(_apparentWindAngle);

            if (absAngle < deadZoneAngle)
            {
                _sailForce = 0f;
                return;
            }

            float force;
            if (absAngle <= bestAngle)
            {
                float t = (absAngle - deadZoneAngle) / (bestAngle - deadZoneAngle);
                force = t * t;
            }
            else
            {
                float t = (absAngle - bestAngle) / (180f - bestAngle);
                force = Mathf.Lerp(1f, runningEfficiency, t);
            }

            _sailForce = force * _sailTrim * sailEfficiency;
            _effectiveSailArea = sailArea * _sailTrim;
        }

        // =================================================================
        // THRUST & DRAG
        // =================================================================

        private void ApplyThrust(float dt)
        {
            // Use effective area from rig if available, otherwise legacy sailArea
            float area = _rig != null ? _effectiveSailArea : sailArea;
            float thrust = _sailForce * _apparentWindSpeed * area * 0.001f;

            _currentSpeed += thrust * dt;
        }

        private void ApplyDrag(float dt)
        {
            // Drag ∝ speed² (quadratic water resistance)
            float drag = _currentSpeed * _currentSpeed * dragCoefficient;

            // Turbulence increases drag (rough water slows you down)
            if (_hull != null && _hull.CurrentFold > 0.1f)
            {
                drag *= 1f + _hull.CurrentFold * 0.5f;
            }

            // Torn sails add windage drag
            if (_sailsTorn > 0 && _rig != null)
            {
                float tornDrag = _sailsTorn * 0.02f * _apparentWindSpeed * 0.01f;
                drag += tornDrag;
            }

            _currentSpeed -= drag * dt;
            _currentSpeed = Mathf.Max(0f, _currentSpeed);

            // Hard cap
            if (_currentSpeed > maxSpeed) _currentSpeed = maxSpeed;
        }

        private void MoveShip(float dt)
        {
            if (_currentSpeed < 0.001f) return;

            float headingRad = _currentHeading * Mathf.Deg2Rad;
            Vector3 forward = new Vector3(
                Mathf.Sin(headingRad),
                0f,
                Mathf.Cos(headingRad)
            );

            // Move on XZ plane — ShipHull handles Y
            Vector3 pos = transform.position;
            pos.x += forward.x * _currentSpeed * dt;
            pos.z += forward.z * _currentSpeed * dt;
            transform.position = pos;
        }

        // =================================================================
        // CONTROL API — LEGACY (called by ShipController / player code)
        // =================================================================

        /// <summary>
        /// Set target heading in degrees (0=N, 90=E, 180=S, 270=W).
        /// Ship will turn toward this heading at its turn rate.
        /// </summary>
        public void SetHeading(float headingDegrees)
        {
            _targetHeading = ((headingDegrees % 360f) + 360f) % 360f;
        }

        /// <summary>
        /// Set aggregate sail trim (0 = furled/no sail, 1 = full sail).
        /// When a rig is present, this acts as a master throttle
        /// on top of individual sail states.
        /// </summary>
        public void SetSailTrim(float trim)
        {
            _sailTrim = Mathf.Clamp01(trim);
        }

        /// <summary>Turn to port (left) by given degrees.</summary>
        public void TurnPort(float degrees)
        {
            SetHeading(_currentHeading - Mathf.Abs(degrees));
        }

        /// <summary>Turn to starboard (right) by given degrees.</summary>
        public void TurnStarboard(float degrees)
        {
            SetHeading(_currentHeading + Mathf.Abs(degrees));
        }

        /// <summary>
        /// Execute a tack — turn through the wind to the other side.
        /// If sailing on port tack (wind from port), tack to starboard tack.
        /// Crosses the dead zone — ship will momentarily lose speed.
        /// Sails go aback briefly during the maneuver.
        /// </summary>
        public void Tack()
        {
            // Mirror apparent wind angle across the bow
            float newAngle = -_apparentWindAngle;
            float windAngle = Mathf.Atan2(
                _windField.GetGlobalWindDirection().x,
                _windField.GetGlobalWindDirection().y
            ) * Mathf.Rad2Deg;
            SetHeading(windAngle + newAngle);
        }

        /// <summary>
        /// Execute a jibe — turn downwind to the other side.
        /// Faster than a tack but more dangerous: boom swings violently
        /// across the deck. Risk of sail/boom damage in heavy wind.
        /// </summary>
        public void Jibe()
        {
            // Mirror apparent wind angle across the stern
            float currentAbsAngle = Mathf.Abs(_apparentWindAngle);
            float mirrorAngle = 360f - currentAbsAngle;
            if (_apparentWindAngle > 0)
                SetHeading(_currentHeading - (mirrorAngle - currentAbsAngle));
            else
                SetHeading(_currentHeading + (mirrorAngle - currentAbsAngle));

            // Jibe damage risk: boom-mounted sails can be damaged in heavy wind
            if (_rig?.Sails != null && _apparentWindSpeed > tearWindThreshold * 0.7f)
            {
                for (int i = 0; i < _rig.Sails.Length; i++)
                {
                    if (_rig.Sails[i].Type == SailType.ForeAndAft ||
                        _rig.Sails[i].Type == SailType.Spanker)
                    {
                        if (_rig.Sails[i].State != SailState.Furled && Random.value < 0.15f)
                        {
                            _rig.Sails[i].Condition = Mathf.Max(0f, _rig.Sails[i].Condition - 0.15f);
                            Debug.LogWarning($"[SAIL] {_rig.Sails[i].Name} damaged by jibe!");
                        }
                    }
                }
            }
        }

        // =================================================================
        // CONTROL API — INDIVIDUAL SAILS (crew interaction)
        // =================================================================

        /// <summary>
        /// Set a specific sail's state. Requires rigger crew at the
        /// appropriate station (halyard for raising, yard for furling).
        /// </summary>
        public void SetSailState(int sailIndex, SailState newState)
        {
            if (_rig?.Sails == null || sailIndex < 0 || sailIndex >= _rig.Sails.Length) return;
            // Can't set a torn sail — must repair first
            if (_rig.Sails[sailIndex].State == SailState.Torn && newState == SailState.Set) return;
            _rig.Sails[sailIndex].State = newState;
        }

        /// <summary>
        /// Set a sail's state by name. Returns true if found.
        /// </summary>
        public bool SetSailState(string sailName, SailState newState)
        {
            if (_rig?.Sails == null) return false;
            for (int i = 0; i < _rig.Sails.Length; i++)
            {
                if (_rig.Sails[i].Name == sailName)
                {
                    SetSailState(i, newState);
                    return true;
                }
            }
            return false;
        }

        /// <summary>Set all sails to a given state.</summary>
        public void SetAllSails(SailState state)
        {
            if (_rig?.Sails == null) return;
            for (int i = 0; i < _rig.Sails.Length; i++)
            {
                if (state == SailState.Set && _rig.Sails[i].State == SailState.Torn) continue;
                _rig.Sails[i].State = state;
            }
        }

        /// <summary>
        /// Trim a specific sail's sheet. 0=sheeted hard, 1=eased.
        /// Crew must be at the sheet station (port or starboard rail).
        /// </summary>
        public void TrimSheet(int sailIndex, float trim)
        {
            if (_rig?.Sails == null || sailIndex < 0 || sailIndex >= _rig.Sails.Length) return;
            _rig.Sails[sailIndex].SheetTrim = Mathf.Clamp01(trim);
        }

        /// <summary>
        /// Auto-trim all sheets to optimal for current wind angle.
        /// Requires rigger crew — skill affects how close to optimal.
        /// </summary>
        public void AutoTrimSheets(float crewSkill)
        {
            if (_rig?.Sails == null) return;
            float absAngle = Mathf.Abs(_apparentWindAngle);
            for (int i = 0; i < _rig.Sails.Length; i++)
            {
                if (_rig.Sails[i].State != SailState.Set && _rig.Sails[i].State != SailState.Reefed)
                    continue;

                float optimal = OptimalSheetTrim(absAngle, _rig.Sails[i].Type);
                // Skill affects accuracy — novice crew trim ±20%, expert ±2%
                float error = (1f - crewSkill) * 0.2f;
                float actual = optimal + Random.Range(-error, error);
                _rig.Sails[i].SheetTrim = Mathf.Clamp01(actual);
            }
        }

        /// <summary>
        /// Reef a specific sail to a given level (0-3).
        /// Reduces effective area but protects from storm damage.
        /// Crew must be on the yard/boom.
        /// </summary>
        public void ReefSail(int sailIndex, int reefLevel)
        {
            if (_rig?.Sails == null || sailIndex < 0 || sailIndex >= _rig.Sails.Length) return;
            _rig.Sails[sailIndex].ReefLevel = Mathf.Clamp(reefLevel, 0, 3);
            if (reefLevel > 0 && _rig.Sails[sailIndex].State == SailState.Set)
                _rig.Sails[sailIndex].State = SailState.Reefed;
            else if (reefLevel == 0 && _rig.Sails[sailIndex].State == SailState.Reefed)
                _rig.Sails[sailIndex].State = SailState.Set;
        }

        /// <summary>
        /// Reef all sails to a given level. Storm protocol.
        /// </summary>
        public void ReefAllSails(int reefLevel)
        {
            if (_rig?.Sails == null) return;
            for (int i = 0; i < _rig.Sails.Length; i++)
                ReefSail(i, reefLevel);
        }

        /// <summary>
        /// Repair a torn sail. Requires carpenter + sail locker supplies.
        /// Restores to furled state — crew must then set it again.
        /// </summary>
        public void RepairSail(int sailIndex, float repairAmount)
        {
            if (_rig?.Sails == null || sailIndex < 0 || sailIndex >= _rig.Sails.Length) return;
            if (_rig.Sails[sailIndex].State != SailState.Torn) return;

            _rig.Sails[sailIndex].Condition = Mathf.Clamp01(_rig.Sails[sailIndex].Condition + repairAmount);
            if (_rig.Sails[sailIndex].Condition >= 0.5f)
            {
                _rig.Sails[sailIndex].State = SailState.Furled;
                Debug.Log($"[SAIL] {_rig.Sails[sailIndex].Name} repaired and furled.");
            }
        }

        // =================================================================
        // CONTROL API — RIGGING LINES
        // =================================================================

        /// <summary>
        /// Set tension on a specific rigging line. Crew hauls or eases.
        /// Sheets: tension = trim (related to sail SheetTrim).
        /// Halyards: tension = how far the sail is raised.
        /// </summary>
        public void SetLineTension(int lineIndex, float tension)
        {
            if (_rig?.Lines == null || lineIndex < 0 || lineIndex >= _rig.Lines.Length) return;
            _rig.Lines[lineIndex].Tension = Mathf.Clamp01(tension);

            // Propagate line effects to the sail
            var line = _rig.Lines[lineIndex];
            if (line.SailIndex < 0 || line.SailIndex >= _rig.Sails.Length) return;

            switch (line.Type)
            {
                case LineType.Sheet:
                    _rig.Sails[line.SailIndex].SheetTrim = tension;
                    break;
                case LineType.Halyard:
                    // Halyard tension: 0=lowered (furled), 1=raised (set)
                    if (tension < 0.2f && _rig.Sails[line.SailIndex].State == SailState.Set)
                        _rig.Sails[line.SailIndex].State = SailState.Furled;
                    else if (tension >= 0.8f && _rig.Sails[line.SailIndex].State == SailState.Furled)
                        _rig.Sails[line.SailIndex].State = SailState.Set;
                    break;
            }
        }

        /// <summary>Get all rigging lines for a given sail index.</summary>
        public RiggingLine[] GetLinesForSail(int sailIndex)
        {
            if (_rig?.Lines == null) return System.Array.Empty<RiggingLine>();

            int count = 0;
            for (int i = 0; i < _rig.Lines.Length; i++)
                if (_rig.Lines[i].SailIndex == sailIndex) count++;

            var result = new RiggingLine[count];
            int idx = 0;
            for (int i = 0; i < _rig.Lines.Length; i++)
                if (_rig.Lines[i].SailIndex == sailIndex)
                    result[idx++] = _rig.Lines[i];
            return result;
        }

        // =================================================================
        // QUERIES (for UI, scripting, AI)
        // =================================================================

        /// <summary>
        /// Get the optimal heading for maximum speed
        /// given current wind conditions. Returns two options
        /// (port and starboard tack) for upwind sailing.
        /// </summary>
        public (float portTack, float starboardTack) GetOptimalUpwindHeadings()
        {
            float windAngle = Mathf.Atan2(
                _windField.GetGlobalWindDirection().x,
                _windField.GetGlobalWindDirection().y
            ) * Mathf.Rad2Deg;

            // Optimal upwind = wind direction ± (deadZone + a few degrees margin)
            float optimalAngle = deadZoneAngle + 5f;
            return (
                ((windAngle + optimalAngle) % 360f + 360f) % 360f,
                ((windAngle - optimalAngle) % 360f + 360f) % 360f
            );
        }

        /// <summary>
        /// Get the velocity made good (VMG) toward a target bearing.
        /// This is how fast you're actually progressing toward your goal.
        /// Negative = moving away from target.
        /// </summary>
        public float GetVMG(float targetBearingDegrees)
        {
            float angleDiff = Mathf.DeltaAngle(_currentHeading, targetBearingDegrees);
            return _currentSpeed * Mathf.Cos(angleDiff * Mathf.Deg2Rad);
        }

        /// <summary>Get a specific sail by index. Returns default if out of range.</summary>
        public IndividualSail GetSail(int index)
        {
            if (_rig?.Sails == null || index < 0 || index >= _rig.Sails.Length)
                return default;
            return _rig.Sails[index];
        }

        /// <summary>Get a mast rig by index.</summary>
        public MastRig GetMast(int index)
        {
            if (_rig?.Masts == null || index < 0 || index >= _rig.Masts.Length)
                return default;
            return _rig.Masts[index];
        }

        /// <summary>Number of masts on this vessel.</summary>
        public int MastCount => _rig?.Masts?.Length ?? 0;

        /// <summary>Number of individual sails on this vessel.</summary>
        public int SailCount => _rig?.Sails?.Length ?? 0;

        /// <summary>Number of rigging lines (crew interaction points).</summary>
        public int LineCount => _rig?.Lines?.Length ?? 0;

        /// <summary>Total number of shroud pairs across all masts.</summary>
        public int TotalShroudPairs
        {
            get
            {
                if (_rig?.Masts == null) return 0;
                int total = 0;
                for (int i = 0; i < _rig.Masts.Length; i++)
                    total += _rig.Masts[i].ShroudPairs;
                return total;
            }
        }
    }
}
