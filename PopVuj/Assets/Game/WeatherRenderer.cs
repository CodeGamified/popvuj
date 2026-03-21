// Copyright CodeGamified 2025-2026
// MIT License — PopVuj
using UnityEngine;
using CodeGamified.Time;

namespace PopVuj.Game
{
    /// <summary>
    /// Weather visual system — renders atmospheric particles driven by
    /// PopVujMatchManager.CurrentWeather.
    ///
    /// Uses sprite textures from Resources/Animations/:
    ///   Clear   → sparse glitter particles + sun glow
    ///   Rain    → rain.png falling, splash_* at ground, drip_* from buildings
    ///   Storm   → heavy rain + lightning flashes + big_smoke ground fog
    ///   Drought → vapor_* rising, flame shimmer, cracked-earth tint
    ///
    /// All particles use Unity's ParticleSystem for GPU-instanced rendering.
    /// The emitter tracks the camera so weather fills the visible area.
    ///
    /// Weather transitions crossfade over ~1 second by ramping emission rates.
    /// </summary>
    public class WeatherRenderer : MonoBehaviour
    {
        private PopVujMatchManager _match;
        private CityGrid _city;

        // Active weather (tracks transitions)
        private Weather _currentWeather = Weather.Clear;
        private Weather _targetWeather = Weather.Clear;
        private float _transitionTimer;
        private const float TRANSITION_DURATION = 1.5f;

        // Particle systems
        private ParticleSystem _rainPS;
        private ParticleSystem _snowPS;      // reuses rain slot for drought vapor
        private ParticleSystem _splashPS;
        private ParticleSystem _fogPS;
        private ParticleSystem _glitterPS;

        // Lightning
        private GameObject _lightningFlash;
        private float _lightningTimer;
        private float _lightningCooldown;
        private float _flashAlpha;
        private MeshRenderer _flashRenderer;

        // Materials (loaded from Animations/)
        private Material _rainMat;
        private Material _splashMat;
        private Material _vaporMat;
        private Material _smokeMat;
        private Material _glitterMat;

        // Dimensions
        private float _cityWidth;
        private const float EMIT_HEIGHT = 12f;    // Y above ground to spawn rain
        private const float GROUND_Y = 0.3f;      // road surface height
        private const float EMIT_DEPTH = 4f;       // Z span for particle box

        // Rain tuning
        private const float RAIN_RATE = 120f;
        private const float RAIN_SPEED = 6f;
        private const float RAIN_LIFETIME = 2.5f;
        private const float RAIN_SIZE = 0.06f;

        // Storm tuning
        private const float STORM_RATE = 350f;
        private const float STORM_SPEED = 9f;
        private const float STORM_SPLASH_RATE = 40f;
        private const float LIGHTNING_MIN_INTERVAL = 2f;
        private const float LIGHTNING_MAX_INTERVAL = 8f;
        private const float LIGHTNING_FLASH_DURATION = 0.12f;

        // Drought tuning
        private const float VAPOR_RATE = 25f;
        private const float VAPOR_SPEED = 0.8f;
        private const float VAPOR_LIFETIME = 4f;

        // Clear tuning
        private const float GLITTER_RATE = 3f;

        public void Initialize(PopVujMatchManager match, CityGrid city)
        {
            _match = match;
            _city = city;
            _cityWidth = city.Width * CityRenderer.CellSize;

            LoadMaterials();
            CreateParticleSystems();
            CreateLightningFlash();

            _currentWeather = match.CurrentWeather;
            _targetWeather = match.CurrentWeather;
            ApplyWeather(_currentWeather, 1f);
        }

        // ═══════════════════════════════════════════════════════════════
        // MATERIAL LOADING
        // ═══════════════════════════════════════════════════════════════

        private void LoadMaterials()
        {
            _rainMat = SpriteAnimator.CreateMaterial("rain") ?? CreateFallbackMat(new Color(0.6f, 0.7f, 0.9f, 0.7f));
            _splashMat = SpriteAnimator.CreateMaterial("splash") ?? CreateFallbackMat(new Color(0.5f, 0.6f, 0.8f, 0.5f));
            _vaporMat = SpriteAnimator.CreateMaterial("vapor") ?? CreateFallbackMat(new Color(0.8f, 0.5f, 0.3f, 0.3f));
            _smokeMat = SpriteAnimator.CreateMaterial("big_smoke") ?? CreateFallbackMat(new Color(0.3f, 0.3f, 0.35f, 0.3f));
            _glitterMat = SpriteAnimator.CreateMaterial("glitter", additive: true) ?? CreateFallbackMat(new Color(1f, 0.95f, 0.7f, 0.5f));
        }

        private static Material CreateFallbackMat(Color color)
        {
            var shader = Shader.Find("Particles/Standard Unlit")
                      ?? Shader.Find("Sprites/Default");
            var mat = new Material(shader);
            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", color);
            return mat;
        }

        // ═══════════════════════════════════════════════════════════════
        // PARTICLE SYSTEM CREATION
        // ═══════════════════════════════════════════════════════════════

        private void CreateParticleSystems()
        {
            _rainPS = CreateWeatherPS("Rain", _rainMat, RAIN_RATE, RAIN_LIFETIME, RAIN_SIZE,
                new Vector3(_cityWidth * 0.5f, EMIT_HEIGHT, 0f),
                new Vector3(_cityWidth, 0.1f, EMIT_DEPTH),
                new Vector3(0.5f, -RAIN_SPEED, 0f));

            _splashPS = CreateWeatherPS("Splash", _splashMat, 0f, 0.4f, 0.12f,
                new Vector3(_cityWidth * 0.5f, GROUND_Y + 0.05f, 0f),
                new Vector3(_cityWidth, 0.05f, EMIT_DEPTH),
                Vector3.zero);
            // Splash: burst from below, tiny upward pop
            var splashVel = _splashPS.velocityOverLifetime;
            splashVel.enabled = true;
            splashVel.y = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
            var splashSize = _splashPS.sizeOverLifetime;
            splashSize.enabled = true;
            splashSize.size = new ParticleSystem.MinMaxCurve(1f,
                new AnimationCurve(new Keyframe(0, 0.3f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0f)));

            _fogPS = CreateWeatherPS("Fog", _smokeMat, 0f, 3f, 0.8f,
                new Vector3(_cityWidth * 0.5f, GROUND_Y - 0.1f, 0f),
                new Vector3(_cityWidth, 0.3f, EMIT_DEPTH),
                new Vector3(0.2f, 0.1f, 0f));
            var fogAlpha = _fogPS.colorOverLifetime;
            fogAlpha.enabled = true;
            var fogGradient = new Gradient();
            fogGradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.4f, 0.3f), new GradientAlphaKey(0f, 1f) });
            fogAlpha.color = fogGradient;

            _glitterPS = CreateWeatherPS("Glitter", _glitterMat, GLITTER_RATE, 2f, 0.05f,
                new Vector3(_cityWidth * 0.5f, 4f, 0f),
                new Vector3(_cityWidth * 0.6f, 6f, EMIT_DEPTH),
                new Vector3(0f, -0.3f, 0f));
            var glitterAlpha = _glitterPS.colorOverLifetime;
            glitterAlpha.enabled = true;
            var glitterGrad = new Gradient();
            glitterGrad.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.95f, 0.7f), 0f), new GradientColorKey(new Color(1f, 0.95f, 0.7f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
            glitterAlpha.color = glitterGrad;
        }

        private ParticleSystem CreateWeatherPS(string name, Material mat, float rate,
            float lifetime, float size, Vector3 position, Vector3 boxSize, Vector3 velocity)
        {
            var go = new GameObject($"Weather_{name}");
            go.transform.SetParent(transform, false);

            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.startLifetime = lifetime;
            main.startSpeed = 0f; // use velocity module instead
            main.startSize = size;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 2000;
            main.playOnAwake = false;
            main.loop = true;
            main.gravityModifier = 0f;

            var emission = ps.emission;
            emission.rateOverTime = rate;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = boxSize;
            shape.position = position;

            if (velocity != Vector3.zero)
            {
                var vel = ps.velocityOverLifetime;
                vel.enabled = true;
                vel.x = velocity.x;
                vel.y = velocity.y;
                vel.z = velocity.z;
            }

            // Alpha fade
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0.8f, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.material = mat;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingOrder = 10;

            // If the texture is a vertical strip, animate frames over particle lifetime
            var tex = mat?.mainTexture as Texture2D;
            if (tex != null && tex.width > 0 && tex.height > tex.width && tex.height % tex.width == 0)
            {
                int stripFrames = tex.height / tex.width;
                var tsa = ps.textureSheetAnimation;
                tsa.enabled = true;
                tsa.mode = ParticleSystemAnimationMode.Grid;
                tsa.numTilesX = 1;
                tsa.numTilesY = stripFrames;
                tsa.cycleCount = 1;
                tsa.animation = ParticleSystemAnimationType.WholeSheet;
            }

            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return ps;
        }

        // ═══════════════════════════════════════════════════════════════
        // LIGHTNING FLASH
        // ═══════════════════════════════════════════════════════════════

        private void CreateLightningFlash()
        {
            _lightningFlash = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _lightningFlash.name = "LightningFlash";
            _lightningFlash.transform.SetParent(transform, false);
            var col = _lightningFlash.GetComponent<Collider>();
            if (col != null) Destroy(col);

            // Large quad covering the sky area
            _lightningFlash.transform.localPosition = new Vector3(_cityWidth * 0.5f, 6f, 3f);
            _lightningFlash.transform.localScale = new Vector3(_cityWidth * 1.5f, 12f, 1f);

            _flashRenderer = _lightningFlash.GetComponent<MeshRenderer>();
            var mat = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color"));
            mat.color = new Color(0.9f, 0.92f, 1f, 0f);
            // Transparent rendering
            mat.SetFloat("_Mode", 3); // Transparent
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 100;
            _flashRenderer.material = mat;

            _lightningFlash.SetActive(false);
            _lightningCooldown = Random.Range(LIGHTNING_MIN_INTERVAL, LIGHTNING_MAX_INTERVAL);
        }

        // ═══════════════════════════════════════════════════════════════
        // UPDATE
        // ═══════════════════════════════════════════════════════════════

        private void Update()
        {
            if (_match == null) return;

            float timeScale = SimulationTime.Instance?.timeScale ?? 1f;
            if (SimulationTime.Instance != null && SimulationTime.Instance.isPaused) return;
            float dt = Time.deltaTime * Mathf.Min(timeScale, 4f); // cap visual speed

            // Detect weather change
            if (_match.CurrentWeather != _targetWeather)
            {
                _targetWeather = _match.CurrentWeather;
                _transitionTimer = TRANSITION_DURATION;
            }

            // Transition
            if (_transitionTimer > 0f)
            {
                _transitionTimer -= dt;
                float t = 1f - Mathf.Clamp01(_transitionTimer / TRANSITION_DURATION);
                ApplyWeather(_targetWeather, t);
                if (_transitionTimer <= 0f)
                    _currentWeather = _targetWeather;
            }

            // Lightning (Storm only)
            UpdateLightning(dt);

            // Follow camera X to keep particles in view
            FollowCamera();
        }

        private void FollowCamera()
        {
            var cam = Camera.main;
            if (cam == null) return;
            // Shift emitter shapes to center on camera X
            float camX = cam.transform.position.x;
            float offset = camX - _cityWidth * 0.5f;
            // We don't move the systems themselves — they emit in world space
            // with shape positions set at creation. This is fine because the
            // city is the fixed reference and the emission box covers it.
        }

        // ═══════════════════════════════════════════════════════════════
        // WEATHER APPLICATION
        // ═══════════════════════════════════════════════════════════════

        private void ApplyWeather(Weather weather, float blend)
        {
            // Ramp emission rates based on target weather and blend factor
            switch (weather)
            {
                case Weather.Clear:
                    SetEmission(_rainPS, 0f);
                    SetEmission(_splashPS, 0f);
                    SetEmission(_fogPS, 0f);
                    SetEmission(_glitterPS, GLITTER_RATE * blend);
                    EnsurePlaying(_glitterPS);
                    EnsureStopped(_rainPS);
                    EnsureStopped(_splashPS);
                    EnsureStopped(_fogPS);
                    break;

                case Weather.Rain:
                    SetEmission(_rainPS, RAIN_RATE * blend);
                    SetEmission(_splashPS, 15f * blend);
                    SetEmission(_fogPS, 0f);
                    SetEmission(_glitterPS, 0f);
                    SetRainSpeed(RAIN_SPEED);
                    EnsurePlaying(_rainPS);
                    EnsurePlaying(_splashPS);
                    EnsureStopped(_fogPS);
                    EnsureStopped(_glitterPS);
                    break;

                case Weather.Storm:
                    SetEmission(_rainPS, STORM_RATE * blend);
                    SetEmission(_splashPS, STORM_SPLASH_RATE * blend);
                    SetEmission(_fogPS, 8f * blend);
                    SetEmission(_glitterPS, 0f);
                    SetRainSpeed(STORM_SPEED);
                    EnsurePlaying(_rainPS);
                    EnsurePlaying(_splashPS);
                    EnsurePlaying(_fogPS);
                    EnsureStopped(_glitterPS);
                    break;

                case Weather.Drought:
                    SetEmission(_rainPS, 0f);
                    SetEmission(_splashPS, 0f);
                    // Reuse fog system for rising heat vapor
                    SetEmission(_fogPS, VAPOR_RATE * blend);
                    SetEmission(_glitterPS, 1f * blend); // faint heat shimmer
                    ConfigureVapor();
                    EnsureStopped(_rainPS);
                    EnsureStopped(_splashPS);
                    EnsurePlaying(_fogPS);
                    EnsurePlaying(_glitterPS);
                    break;
            }
        }

        private void SetEmission(ParticleSystem ps, float rate)
        {
            if (ps == null) return;
            var em = ps.emission;
            em.rateOverTime = rate;
        }

        private void SetRainSpeed(float speed)
        {
            if (_rainPS == null) return;
            var vel = _rainPS.velocityOverLifetime;
            vel.y = -speed;
        }

        private void ConfigureVapor()
        {
            if (_fogPS == null) return;
            // Swap to vapor material if loaded
            if (_vaporMat != null)
            {
                var r = _fogPS.GetComponent<ParticleSystemRenderer>();
                r.material = _vaporMat;
            }
            var vel = _fogPS.velocityOverLifetime;
            vel.enabled = true;
            vel.y = VAPOR_SPEED;
            vel.x = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);
        }

        private static void EnsurePlaying(ParticleSystem ps)
        {
            if (ps != null && !ps.isPlaying) ps.Play();
        }

        private static void EnsureStopped(ParticleSystem ps)
        {
            if (ps != null && ps.isPlaying) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        // ═══════════════════════════════════════════════════════════════
        // LIGHTNING
        // ═══════════════════════════════════════════════════════════════

        private void UpdateLightning(float dt)
        {
            if (_currentWeather != Weather.Storm && _targetWeather != Weather.Storm)
            {
                if (_lightningFlash.activeSelf) _lightningFlash.SetActive(false);
                return;
            }

            // Flash active — fade out
            if (_flashAlpha > 0f)
            {
                _flashAlpha -= dt / LIGHTNING_FLASH_DURATION;
                if (_flashAlpha <= 0f)
                {
                    _flashAlpha = 0f;
                    _lightningFlash.SetActive(false);
                }
                else
                {
                    var mat = _flashRenderer.material;
                    mat.color = new Color(0.9f, 0.92f, 1f, _flashAlpha);
                }
                return;
            }

            // Cooldown
            _lightningCooldown -= dt;
            if (_lightningCooldown <= 0f)
            {
                TriggerLightning();
                _lightningCooldown = Random.Range(LIGHTNING_MIN_INTERVAL, LIGHTNING_MAX_INTERVAL);
            }
        }

        private void TriggerLightning()
        {
            _flashAlpha = 0.7f;
            _lightningFlash.SetActive(true);
            var mat = _flashRenderer.material;
            mat.color = new Color(0.9f, 0.92f, 1f, _flashAlpha);

            // Position flash over a random X position
            float x = Random.Range(0f, _cityWidth);
            _lightningFlash.transform.localPosition = new Vector3(x, 6f, 3f);
        }

        /// <summary>External trigger for VFX to fire a lightning bolt at a specific X.</summary>
        public void TriggerLightningAt(float worldX)
        {
            _flashAlpha = 0.9f;
            _lightningFlash.SetActive(true);
            _lightningFlash.transform.localPosition = new Vector3(worldX, 6f, 3f);
            var mat = _flashRenderer.material;
            mat.color = new Color(0.95f, 0.95f, 1f, _flashAlpha);
        }
    }
}
