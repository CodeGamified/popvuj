// Copyright SeaRäuber 2025-2026
// MIT License
using UnityEngine;

namespace SeaRauber.Ship
{
    /// <summary>
    /// Multi-point buoyancy system — makes a ship float on Gerstner waves.
    /// 
    /// Samples OceanSurface at multiple hull points to compute:
    ///   - Vertical position (buoyancy)
    ///   - Pitch and roll (wave slope alignment)
    ///   - Heave velocity (for splash/spray triggers)
    ///   - Surface fold (Jacobian) for turbulence response
    /// 
    /// Physics approach:
    ///   NOT using Unity Rigidbody. Direct transform manipulation with smoothing.
    ///   Ship position.y = weighted average of wave heights at sample points.
    ///   Ship rotation = normal computed from sample point triangle.
    ///   
    ///   Why no Rigidbody? Because:
    ///   1. Gerstner waves are deterministic — no need for physics simulation
    ///   2. Direct placement is cheaper on mobile
    ///   3. Player controls ships via CODE, not physics forces
    ///   4. Deterministic replay is essential for the async gameplay loop
    /// 
    /// Sample point layout (top-down view, ship pointing +Z):
    /// 
    ///        [BOW]          (0, 0, +bowOffset)
    ///       /    \
    ///   [PORT]  [STARBOARD]  (-beam/2, 0, 0)  (+beam/2, 0, 0)
    ///       \    /
    ///       [STERN]         (0, 0, -sternOffset)
    /// 
    /// The 4 points define 2 triangles → averaged normal → pitch + roll.
    /// </summary>
    public class ShipHull : MonoBehaviour
    {
        [Header("Hull Dimensions")]
        [Tooltip("Bow sample point offset from center (forward, +Z local)")]
        public float bowOffset = 4f;

        [Tooltip("Stern sample point offset from center (backward, -Z local)")]
        public float sternOffset = 3.5f;

        [Tooltip("Beam width — distance from center to port/starboard sample")]
        public float beamHalf = 1.5f;

        [Header("Buoyancy")]
        [Tooltip("Ship waterline offset — how deep the hull sits (0 = surface)")]
        public float waterlineOffset = -0.3f;

        [Tooltip("Buoyancy responsiveness (higher = snappier, lower = sluggish heavy ship)")]
        [Range(1f, 20f)]
        public float buoyancySpeed = 6f;

        [Tooltip("Rotation responsiveness for pitch/roll alignment")]
        [Range(1f, 20f)]
        public float rotationSpeed = 4f;

        [Header("Motion Damping")]
        [Tooltip("Vertical velocity damping (prevents oscillation)")]
        [Range(0f, 1f)]
        public float heaveDamping = 0.85f;

        [Tooltip("Maximum heave velocity (prevents launching off waves)")]
        public float maxHeaveVelocity = 5f;

        [Header("Turbulence Response")]
        [Tooltip("How much the ship rocks extra in turbulent (low Jacobian) water")]
        [Range(0f, 2f)]
        public float turbulenceRockAmount = 0.5f;

        [Tooltip("Speed of turbulence-induced rocking")]
        public float turbulenceRockSpeed = 3f;

        [Header("Debug")]
        public bool showSamplePoints = true;
        public bool showWaterline = true;
        public bool logBuoyancyData = false;

        // --- State ---
        private float _currentY;
        private float _heaveVelocity;
        private float _targetY;
        private Vector3 _targetNormal;
        private float _currentFold; // Jacobian fold at ship center

        // --- Accessors for other systems ---
        /// <summary>Current wave fold amount at ship center (0=calm, 1=breaking)</summary>
        public float CurrentFold => _currentFold;

        /// <summary>Current heave velocity (m/s, positive = rising)</summary>
        public float HeaveVelocity => _heaveVelocity;

        /// <summary>Is the ship in rough water? (fold > 0.3)</summary>
        public bool InRoughWater => _currentFold > 0.3f;

        private Core.OceanSurface _ocean;

        private void Start()
        {
            _ocean = Core.OceanSurface.Instance;
            if (_ocean == null)
            {
                Debug.LogError("[SHIP] No OceanSurface found! ShipHull requires an ocean.");
                enabled = false;
                return;
            }

            // Initialize at current wave height
            _currentY = _ocean.GetWaveHeight(transform.position.x, transform.position.z) + waterlineOffset;
            _targetNormal = Vector3.up;
        }

        private void Update()
        {
            if (_ocean == null) return;

            SampleOcean();
            ApplyBuoyancy();
            ApplyRotation();
            ApplyTurbulenceRock();
        }

        /// <summary>
        /// Sample the ocean at 4 hull points to get target height and normal.
        /// All sampling is in world space.
        /// </summary>
        private void SampleOcean()
        {
            // Transform sample points from local hull space to world space
            Vector3 bowWorld = transform.TransformPoint(0f, 0f, bowOffset);
            Vector3 sternWorld = transform.TransformPoint(0f, 0f, -sternOffset);
            Vector3 portWorld = transform.TransformPoint(-beamHalf, 0f, 0f);
            Vector3 starboardWorld = transform.TransformPoint(beamHalf, 0f, 0f);

            // Sample wave heights
            float hBow = _ocean.GetWaveHeight(bowWorld.x, bowWorld.z);
            float hStern = _ocean.GetWaveHeight(sternWorld.x, sternWorld.z);
            float hPort = _ocean.GetWaveHeight(portWorld.x, portWorld.z);
            float hStarboard = _ocean.GetWaveHeight(starboardWorld.x, starboardWorld.z);

            // Target Y = average of all 4 sample points + waterline offset
            _targetY = (hBow + hStern + hPort + hStarboard) * 0.25f + waterlineOffset;

            // Compute surface normal from the sample cross
            // Forward vector: bow to stern (tangent along ship length)
            Vector3 bowPoint = new Vector3(bowWorld.x, hBow, bowWorld.z);
            Vector3 sternPoint = new Vector3(sternWorld.x, hStern, sternWorld.z);
            Vector3 portPoint = new Vector3(portWorld.x, hPort, portWorld.z);
            Vector3 starboardPoint = new Vector3(starboardWorld.x, hStarboard, starboardWorld.z);

            // Two tangent vectors → cross product → normal
            Vector3 longitudinal = bowPoint - sternPoint;   // Along ship length
            Vector3 lateral = starboardPoint - portPoint;    // Across ship beam

            _targetNormal = Vector3.Cross(lateral, longitudinal).normalized;

            // Ensure normal points up (not flipped)
            if (_targetNormal.y < 0f) _targetNormal = -_targetNormal;

            // Sample Jacobian at ship center for turbulence
            _currentFold = _ocean.GetFoldAmount(transform.position.x, transform.position.z);

            if (logBuoyancyData)
            {
                Debug.Log($"[SHIP] Y={_targetY:F2} fold={_currentFold:F2} " +
                          $"bow={hBow:F2} stern={hStern:F2} port={hPort:F2} star={hStarboard:F2}");
            }
        }

        /// <summary>
        /// Smoothly move ship to target height with velocity damping.
        /// Spring-damper system prevents oscillation on steep waves.
        /// </summary>
        private void ApplyBuoyancy()
        {
            float dt = Time.deltaTime;

            // Spring force toward target
            float springForce = (_targetY - _currentY) * buoyancySpeed;
            _heaveVelocity += springForce * dt;

            // Damping
            _heaveVelocity *= heaveDamping;

            // Clamp
            _heaveVelocity = Mathf.Clamp(_heaveVelocity, -maxHeaveVelocity, maxHeaveVelocity);

            // Integrate
            _currentY += _heaveVelocity * dt;

            // Apply
            Vector3 pos = transform.position;
            pos.y = _currentY;
            transform.position = pos;
        }

        /// <summary>
        /// Smoothly align ship rotation to wave surface normal.
        /// Preserves yaw (heading) — only modifies pitch and roll.
        /// </summary>
        private void ApplyRotation()
        {
            // Current heading (yaw only)
            float yaw = transform.eulerAngles.y;

            // Target rotation: align up-vector to wave normal, keep heading
            Quaternion targetRot = Quaternion.LookRotation(
                Vector3.ProjectOnPlane(transform.forward, _targetNormal).normalized,
                _targetNormal
            );

            // Slerp toward target
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRot,
                Time.deltaTime * rotationSpeed
            );
        }

        /// <summary>
        /// Add extra rocking in turbulent water (high fold / low Jacobian).
        /// This is the "ship in a storm" feel — not from waves directly,
        /// but from the surface folding beneath the hull.
        /// </summary>
        private void ApplyTurbulenceRock()
        {
            if (_currentFold < 0.1f) return; // No turbulence

            float t = Time.time * turbulenceRockSpeed;
            float rockAmount = _currentFold * turbulenceRockAmount;

            // Perlin-driven rock (not sinusoidal — more chaotic)
            float rockPitch = (Mathf.PerlinNoise(t, 0.5f) * 2f - 1f) * rockAmount * 5f;
            float rockRoll = (Mathf.PerlinNoise(0.5f, t * 1.3f) * 2f - 1f) * rockAmount * 8f;

            transform.Rotate(rockPitch * Time.deltaTime, 0f, rockRoll * Time.deltaTime, Space.Self);
        }

        // =================================================================
        // PUBLIC API (for ShipController / player scripts)
        // =================================================================

        /// <summary>
        /// Get the ocean wave height directly below the ship center.
        /// </summary>
        public float GetWaterHeightAtCenter()
        {
            if (_ocean == null) return 0f;
            return _ocean.GetWaveHeight(transform.position.x, transform.position.z);
        }

        /// <summary>
        /// Get the ocean surface normal directly below the ship.
        /// </summary>
        public Vector3 GetWaterNormalAtCenter()
        {
            if (_ocean == null) return Vector3.up;
            return _ocean.GetWaveNormal(transform.position.x, transform.position.z);
        }

        // =================================================================
        // DEBUG GIZMOS
        // =================================================================

        private void OnDrawGizmos()
        {
            if (showSamplePoints)
            {
                Gizmos.color = Color.yellow;
                float r = 0.2f;
                Gizmos.DrawSphere(transform.TransformPoint(0f, 0f, bowOffset), r);
                Gizmos.DrawSphere(transform.TransformPoint(0f, 0f, -sternOffset), r);

                Gizmos.color = Color.red;
                Gizmos.DrawSphere(transform.TransformPoint(-beamHalf, 0f, 0f), r); // Port
                
                Gizmos.color = Color.green;
                Gizmos.DrawSphere(transform.TransformPoint(beamHalf, 0f, 0f), r);  // Starboard
            }

            if (showWaterline)
            {
                Gizmos.color = new Color(0f, 0.5f, 1f, 0.5f);
                Vector3 wl = transform.position;
                wl.y = _targetY;
                Gizmos.DrawWireCube(wl, new Vector3(beamHalf * 2f, 0.05f, bowOffset + sternOffset));
            }
        }
    }
}
