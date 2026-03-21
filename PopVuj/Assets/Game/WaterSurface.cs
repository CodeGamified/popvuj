// Copyright CodeGamified 2025-2026
// MIT License — PopVuj
using UnityEngine;
using UnityEngine.Rendering;

namespace PopVuj.Game
{
    /// <summary>
    /// Procedural water surface with Gerstner wave displacement.
    ///
    /// Adapted for PopVuj's 2.5D side-view perspective:
    ///   - Camera faces -Z → +Z, viewing the XY plane from slightly above
    ///   - XZ grid mesh at the water surface level, displaced by Gerstner waves
    ///   - Deep water body rendered as a simple child cube below the surface
    ///   - CPU-side wave functions mirror the shader for ship physics
    ///
    /// Gerstner Wave Math (GPU Gems Chapter 1):
    ///   P.x = x + Σ(A_i * D_i.x * cos(k_i * dot(D_i, P0) - ω_i * t))
    ///   P.y = Σ(A_i * sin(k_i * dot(D_i, P0) - ω_i * t))
    ///   P.z = z + Σ(A_i * D_i.z * cos(k_i * dot(D_i, P0) - ω_i * t))
    ///   Where: k = 2π/λ, ω = √(g*k), A = steepness/k
    ///
    /// Weather coupling:
    ///   Clear(0) = gentle swells, Storm(2) = large breaking waves
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class WaterSurface : MonoBehaviour
    {
        public static WaterSurface Instance { get; private set; }

        [Header("Mesh")]
        [Tooltip("X subdivisions (more = smoother wave profile)")]
        [Range(16, 256)]
        public int resolution = 64;

        [Header("Wave Parameters")]
        [Tooltip("Primary swell (dirX, dirZ, steepness, wavelength)")]
        public Vector4 wave0 = new Vector4(1.0f, 0.0f, 0.15f, 4.0f);
        [Tooltip("Cross swell — angled off primary for realistic confusion")]
        public Vector4 wave1 = new Vector4(0.87f, 0.5f, 0.10f, 2.2f);
        [Tooltip("High-frequency chop — adds surface texture")]
        public Vector4 wave2 = new Vector4(-0.5f, 0.87f, 0.05f, 1.0f);

        [Tooltip("Global wave amplitude multiplier")]
        [Range(0f, 3f)]
        public float waveScale = 1f;

        [Header("Colors")]
        public Color shallowColor = new Color(0.12f, 0.30f, 0.38f, 1f);
        public Color deepColor    = new Color(0.05f, 0.14f, 0.22f, 1f);
        public Color foamColor    = new Color(0.85f, 0.90f, 0.95f, 1f);
        public Color fresnelColor = new Color(0.30f, 0.45f, 0.60f, 1f);
        public Color sssColor     = new Color(0.06f, 0.22f, 0.18f, 1f);

        [Header("Lighting")]
        public Color sunColor = new Color(1f, 0.95f, 0.85f, 1f);
        [Range(1f, 512f)]
        public float specularPower = 128f;
        [Range(0f, 5f)]
        public float specularIntensity = 1.5f;

        // Internal
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private Material _material;
        private Mesh _mesh;
        private GameObject _deepWaterGO;

        // Bounds (set by CityRenderer)
        private float _startX, _endX;
        private float _surfaceY, _bottomY;
        private float _zCenter, _zExtent;
        private bool _initialized;

        // Shader property IDs
        private static readonly int PropWave0 = Shader.PropertyToID("_Wave0");
        private static readonly int PropWave1 = Shader.PropertyToID("_Wave1");
        private static readonly int PropWave2 = Shader.PropertyToID("_Wave2");
        private static readonly int PropWaveScale = Shader.PropertyToID("_WaveScale");
        private static readonly int PropShallowColor = Shader.PropertyToID("_ShallowColor");
        private static readonly int PropDeepColor = Shader.PropertyToID("_DeepColor");
        private static readonly int PropFoamColor = Shader.PropertyToID("_FoamColor");
        private static readonly int PropFresnelColor = Shader.PropertyToID("_FresnelColor");
        private static readonly int PropSSSColor = Shader.PropertyToID("_SSSColor");
        private static readonly int PropSunDir = Shader.PropertyToID("_SunDir");
        private static readonly int PropSunColor = Shader.PropertyToID("_SunColor");
        private static readonly int PropSpecularPower = Shader.PropertyToID("_SpecularPower");
        private static readonly int PropSpecularIntensity = Shader.PropertyToID("_SpecularIntensity");

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        /// <summary>
        /// Configure the water zone bounds. Called by CityRenderer when layout changes.
        /// Regenerates the mesh and updates material properties.
        /// </summary>
        public void SetBounds(float startX, float endX, float surfaceY, float bottomY,
                              float zCenter, float zExtent)
        {
            _startX = startX;
            _endX = endX;
            _surfaceY = surfaceY;
            _bottomY = bottomY;
            _zCenter = zCenter;
            _zExtent = zExtent;

            if (!_initialized)
            {
                _meshFilter = GetComponent<MeshFilter>();
                _meshRenderer = GetComponent<MeshRenderer>();
                CreateMaterial();
                _initialized = true;
            }

            GenerateSurfaceMesh();
            UpdateDeepWater();
            UpdateMaterial();
        }

        /// <summary>
        /// Set wave parameters from weather state.
        ///   0=Clear, 1=Rain, 2=Storm, 3=Drought
        /// </summary>
        public void SetWeather(int weather)
        {
            switch (weather)
            {
                case 0: // Clear — gentle swells
                    waveScale = 0.6f;
                    wave0 = new Vector4(1.0f, 0.0f, 0.12f, 4.0f);
                    wave1 = new Vector4(0.87f, 0.5f, 0.06f, 2.2f);
                    wave2 = new Vector4(-0.5f, 0.87f, 0.03f, 1.0f);
                    break;
                case 1: // Rain — moderate seas
                    waveScale = 1.0f;
                    wave0 = new Vector4(1.0f, 0.0f, 0.18f, 3.5f);
                    wave1 = new Vector4(0.87f, 0.5f, 0.10f, 2.0f);
                    wave2 = new Vector4(-0.5f, 0.87f, 0.06f, 0.8f);
                    break;
                case 2: // Storm — heavy breaking waves
                    waveScale = 2.0f;
                    wave0 = new Vector4(1.0f, 0.0f, 0.25f, 5.0f);
                    wave1 = new Vector4(0.87f, 0.5f, 0.15f, 2.8f);
                    wave2 = new Vector4(-0.5f, 0.87f, 0.10f, 1.2f);
                    break;
                case 3: // Drought — dead calm
                    waveScale = 0.3f;
                    wave0 = new Vector4(1.0f, 0.0f, 0.08f, 5.0f);
                    wave1 = new Vector4(0.87f, 0.5f, 0.04f, 3.0f);
                    wave2 = new Vector4(-0.5f, 0.87f, 0.02f, 1.5f);
                    break;
            }
        }

        private void Update()
        {
            if (!_initialized) return;
            UpdateSunDirection();
            UpdateMaterial();
        }

        // =====================================================================
        // MESH GENERATION
        // =====================================================================

        /// <summary>
        /// Generate a flat XZ grid at the water surface level.
        /// The shader displaces vertices with Gerstner waves.
        /// </summary>
        private void GenerateSurfaceMesh()
        {
            float width = _endX - _startX;
            int resX = resolution;
            int resZ = Mathf.Max(2, resolution / 8);

            _mesh = new Mesh();
            _mesh.name = "WaterSurface";

            int vertCount = (resX + 1) * (resZ + 1);

            Vector3[] verts = new Vector3[vertCount];
            Vector2[] uvs = new Vector2[vertCount];
            int[] tris = new int[resX * resZ * 6];

            float zStart = _zCenter - _zExtent * 0.5f;

            for (int z = 0; z <= resZ; z++)
            {
                for (int x = 0; x <= resX; x++)
                {
                    int i = z * (resX + 1) + x;
                    float xPos = _startX + (x / (float)resX) * width;
                    float zPos = zStart + (z / (float)resZ) * _zExtent;
                    verts[i] = new Vector3(xPos, _surfaceY, zPos);
                    uvs[i] = new Vector2(x / (float)resX, z / (float)resZ);
                }
            }

            int ti = 0;
            for (int z = 0; z < resZ; z++)
            {
                for (int x = 0; x < resX; x++)
                {
                    int vi = z * (resX + 1) + x;
                    int ni = vi + (resX + 1);

                    tris[ti++] = vi;
                    tris[ti++] = ni;
                    tris[ti++] = ni + 1;

                    tris[ti++] = vi;
                    tris[ti++] = ni + 1;
                    tris[ti++] = vi + 1;
                }
            }

            _mesh.vertices = verts;
            _mesh.uv = uvs;
            _mesh.triangles = tris;

            // Large bounds to prevent frustum culling (shader displaces vertices)
            _mesh.bounds = new Bounds(
                new Vector3((_startX + _endX) * 0.5f, _surfaceY, _zCenter),
                new Vector3(width * 2f, 4f, _zExtent * 2f)
            );

            _meshFilter.mesh = _mesh;
        }

        /// <summary>
        /// Simple cube child for the water body below the wave surface.
        /// Provides the deep blue mass visible from the 2.5D side view.
        /// </summary>
        private void UpdateDeepWater()
        {
            if (_deepWaterGO == null)
            {
                _deepWaterGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _deepWaterGO.name = "WaterBody";
                _deepWaterGO.transform.SetParent(transform, false);
                var col = _deepWaterGO.GetComponent<Collider>();
                if (col != null) Destroy(col);

                var r = _deepWaterGO.GetComponent<Renderer>();
                if (r != null)
                    r.shadowCastingMode = ShadowCastingMode.Off;
            }

            float waterDepth = _surfaceY - _bottomY;
            float cx = (_startX + _endX) * 0.5f;
            _deepWaterGO.transform.localPosition = new Vector3(cx, _surfaceY - waterDepth * 0.5f, _zCenter);
            _deepWaterGO.transform.localScale = new Vector3(_endX - _startX, waterDepth, _zExtent);

            var rend = _deepWaterGO.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = rend.material;
                Color bodyColor = new Color(deepColor.r, deepColor.g, deepColor.b, 0.10f);
                if (mat.HasProperty("_BaseColor"))
                    mat.SetColor("_BaseColor", bodyColor);
                else
                    mat.color = bodyColor;

                // Configure transparent rendering
                if (mat.HasProperty("_Surface"))
                    mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_ZWrite", 0f);
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.SetOverrideTag("RenderType", "Transparent");
            }
        }

        // =====================================================================
        // MATERIAL
        // =====================================================================

        private void CreateMaterial()
        {
            Shader waterShader = Shader.Find("PopVuj/Water");
            if (waterShader == null)
            {
                Debug.LogWarning("[POPVUJ] Could not find 'PopVuj/Water' shader, using URP/Lit fallback");
                waterShader = Shader.Find("Universal Render Pipeline/Lit");
            }

            _material = new Material(waterShader);
            _material.name = "PopVujWater";
            _meshRenderer.material = _material;
            _meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _meshRenderer.receiveShadows = true;
        }

        private void UpdateMaterial()
        {
            if (_material == null) return;

            _material.SetVector(PropWave0, wave0);
            _material.SetVector(PropWave1, wave1);
            _material.SetVector(PropWave2, wave2);
            _material.SetFloat(PropWaveScale, waveScale);

            _material.SetColor(PropShallowColor, shallowColor);
            _material.SetColor(PropDeepColor, deepColor);
            _material.SetColor(PropFoamColor, foamColor);
            _material.SetColor(PropFresnelColor, fresnelColor);
            _material.SetColor(PropSSSColor, sssColor);

            _material.SetColor(PropSunColor, sunColor);
            _material.SetFloat(PropSpecularPower, specularPower);
            _material.SetFloat(PropSpecularIntensity, specularIntensity);
        }

        private void UpdateSunDirection()
        {
            if (_material == null) return;

            Vector3 sunDir = new Vector3(0.5f, 0.8f, 0.3f).normalized;
            var simTime = CodeGamified.Time.SimulationTime.Instance;
            if (simTime != null)
            {
                sunDir = simTime.GetSunDirection();
                if (sunDir.y < 0.05f) sunDir.y = 0.05f;
                sunDir.Normalize();
            }
            _material.SetVector(PropSunDir, new Vector4(sunDir.x, sunDir.y, sunDir.z, 0f));
        }

        // =====================================================================
        // CPU-SIDE WAVE QUERIES — must match shader math exactly
        // =====================================================================

        private const float GRAVITY = 9.81f;

        /// <summary>
        /// Get wave height at a world X position (at the default Z center).
        /// Returns world Y of the water surface. Use for ship buoyancy.
        /// </summary>
        public float GetWaveHeight(float worldX)
        {
            return _surfaceY + GetWaveDisplacement(worldX, _zCenter).y;
        }

        /// <summary>
        /// Get full 3D wave displacement at a world position.
        /// Gerstner waves displace horizontally AND vertically.
        /// </summary>
        public Vector3 GetWaveDisplacement(float worldX, float worldZ)
        {
            Vector3 disp = Vector3.zero;
            Vector4[] waves = { wave0, wave1, wave2 };
            float time = Time.time;

            for (int i = 0; i < 3; i++)
            {
                Vector4 w = waves[i];
                float steepness = w.z * waveScale;
                float wavelength = w.w;
                if (wavelength < 0.001f) continue;

                float k = 2f * Mathf.PI / wavelength;
                float c = Mathf.Sqrt(GRAVITY / k);
                Vector2 d = new Vector2(w.x, w.y).normalized;
                float f = k * (d.x * worldX + d.y * worldZ - c * time);
                float a = steepness / k;

                disp.x += d.x * a * Mathf.Cos(f);
                disp.y += a * Mathf.Sin(f);
                disp.z += d.y * a * Mathf.Cos(f);
            }

            return disp;
        }

        /// <summary>
        /// Get the wave surface normal at a world position.
        /// Use for ship tilt/roll alignment.
        /// </summary>
        public Vector3 GetWaveNormal(float worldX, float worldZ)
        {
            Vector3 normal = new Vector3(0f, 1f, 0f);
            Vector4[] waves = { wave0, wave1, wave2 };
            float time = Time.time;

            for (int i = 0; i < 3; i++)
            {
                Vector4 w = waves[i];
                float steepness = w.z * waveScale;
                float wavelength = w.w;
                if (wavelength < 0.001f) continue;

                float k = 2f * Mathf.PI / wavelength;
                float c = Mathf.Sqrt(GRAVITY / k);
                Vector2 d = new Vector2(w.x, w.y).normalized;
                float f = k * (d.x * worldX + d.y * worldZ - c * time);
                float a = steepness / k;

                normal.x -= d.x * k * a * Mathf.Cos(f);
                normal.y -= steepness * Mathf.Sin(f);
                normal.z -= d.y * k * a * Mathf.Cos(f);
            }

            return normal.normalized;
        }

        /// <summary>
        /// Get the Jacobian determinant — measures surface compression.
        ///   J = 1.0 → flat, undisturbed
        ///   J &lt; 1.0 → surface compressing (wave building)
        ///   J → 0.0 → wave about to break (whitecap territory)
        ///   J &lt; 0.0 → surface folded (breaking wave)
        /// </summary>
        public float GetJacobian(float worldX, float worldZ)
        {
            Vector4[] waves = { wave0, wave1, wave2 };
            float time = Time.time;

            float jacXX = 0f, jacZZ = 0f, jacXZ = 0f;

            for (int i = 0; i < 3; i++)
            {
                Vector4 w = waves[i];
                float steepness = w.z * waveScale;
                float wavelength = w.w;
                if (wavelength < 0.001f) continue;

                float k = 2f * Mathf.PI / wavelength;
                float c = Mathf.Sqrt(GRAVITY / k);
                Vector2 d = new Vector2(w.x, w.y).normalized;
                float f = k * (d.x * worldX + d.y * worldZ - c * time);
                float a = steepness / k;

                float ka_sinF = k * a * Mathf.Sin(f);
                jacXX += d.x * d.x * ka_sinF;
                jacZZ += d.y * d.y * ka_sinF;
                jacXZ += d.x * d.y * ka_sinF;
            }

            float J_xx = 1f - jacXX;
            float J_zz = 1f - jacZZ;
            float J_xz = -jacXZ;
            return J_xx * J_zz - J_xz * J_xz;
        }

        /// <summary>
        /// Fold amount (0 = calm, 1 = breaking). Convenience wrapper.
        /// </summary>
        public float GetFoldAmount(float worldX, float worldZ)
        {
            return Mathf.Clamp01(1f - GetJacobian(worldX, worldZ));
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_deepWaterGO != null) Destroy(_deepWaterGO);
        }
    }
}
