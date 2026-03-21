// Copyright CodeGamified 2025-2026
// MIT License — PopVuj
using UnityEngine;
using CodeGamified.Time;
using PopVuj.Core;

namespace PopVuj.Game
{
    /// <summary>
    /// Renders sun and moon sprites for the day-night cycle.
    ///
    /// Sun arcs east→zenith→west during 6h–18h.
    /// Moon arcs east→zenith→west during 18h–6h (yin-yang handoff).
    /// Moon sprite = 8-frame vertical strip (Resources/Animations/moon),
    ///   1 phase per day, switching at midnight.
    /// Sun sprite = Resources/Textures/sun.
    ///
    /// Sky backdrop is handled by SkyRenderer (multi-layer properties-driven).
    /// </summary>
    public class DayNightRenderer : MonoBehaviour
    {
        private CityGrid _city;

        // Sprite objects
        private GameObject _sunGO;
        private GameObject _moonGO;
        private MeshRenderer _sunRenderer;
        private MeshRenderer _moonRenderer;

        // Moon frame textures
        private Texture2D[] _moonFrames;
        private Material _moonMat;
        private int _lastMoonPhase = -1;

        // Arc geometry
        private float _cityWidth;
        private const float ARC_RADIUS = 10f;     // distance from arc center to sun/moon
        private const float ARC_CENTER_Y = 0f;    // arc pivot at road level
        private const float SPRITE_SIZE = 6f;      // world-unit size of sun/moon quads
        private const float SPRITE_Z = 8f;        // behind buildings (BuildingZ = 1)

        // Ambient light tinting (applied to sun/moon sprite alpha)
        private const float SUN_ALPHA  = 0.95f;
        private const float MOON_ALPHA = 0.7f;

        public void Initialize(CityGrid city)
        {
            _city = city;
            _cityWidth = city.Width * CityRenderer.CellSize;

            LoadSprites();
            CreateSunObject();
            CreateMoonObject();
        }

        // ═══════════════════════════════════════════════════════════════
        // SPRITE LOADING
        // ═══════════════════════════════════════════════════════════════

        private void LoadSprites()
        {
            // Moon: 8-frame vertical strip in Animations/
            var moonSheet = SpriteAnimator.LoadSheet("moon");
            if (moonSheet.IsStrip && moonSheet.FrameCount >= 8)
            {
                _moonFrames = SpriteAnimator.ExtractFrames(moonSheet);
            }
            else
            {
                // Fallback: single moon texture
                _moonFrames = new[] { moonSheet.Texture ?? CreateFallbackTex(new Color(0.8f, 0.82f, 0.9f)) };
            }
        }

        private void CreateSunObject()
        {
            _sunGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _sunGO.name = "Sun";
            _sunGO.transform.SetParent(transform, false);
            _sunGO.transform.localScale = Vector3.one * SPRITE_SIZE;
            var col = _sunGO.GetComponent<Collider>();
            if (col != null) Destroy(col);

            // Load sun texture from Textures/
            var sunTex = Resources.Load<Texture2D>("Textures/sun");
            if (sunTex != null)
            {
                sunTex.filterMode = FilterMode.Point;
                sunTex.wrapMode = TextureWrapMode.Clamp;
            }

            _sunRenderer = _sunGO.GetComponent<MeshRenderer>();
            var mat = CreateSpriteMat(sunTex, new Color(1f, 0.95f, 0.6f));
            _sunRenderer.material = mat;
            _sunRenderer.sortingOrder = -10;

            _sunGO.SetActive(false);
        }

        private void CreateMoonObject()
        {
            _moonGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _moonGO.name = "Moon";
            _moonGO.transform.SetParent(transform, false);
            _moonGO.transform.localScale = Vector3.one * SPRITE_SIZE * 0.8f;
            var col = _moonGO.GetComponent<Collider>();
            if (col != null) Destroy(col);

            _moonRenderer = _moonGO.GetComponent<MeshRenderer>();
            _moonMat = CreateSpriteMat(_moonFrames[0], new Color(0.75f, 0.8f, 0.95f));
            _moonRenderer.material = _moonMat;
            _moonRenderer.sortingOrder = -10;

            _moonGO.SetActive(false);
        }

        private Material CreateSpriteMat(Texture2D tex, Color tint)
        {
            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Transparent");
            var mat = new Material(shader);
            if (tex != null) mat.mainTexture = tex;
            mat.color = tint;
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent - 50;
            return mat;
        }

        private Texture2D CreateFallbackTex(Color color)
        {
            var tex = new Texture2D(2, 2);
            var pixels = new[] { color, color, color, color };
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        // ═══════════════════════════════════════════════════════════════
        // UPDATE
        // ═══════════════════════════════════════════════════════════════

        private void Update()
        {
            var simTime = SimulationTime.Instance as PopVujSimulationTime;
            if (simTime == null) return;

            float hour = simTime.GetTimeOfDay();
            float centerX = _cityWidth * 0.5f;

            UpdateSun(hour, centerX);
            UpdateMoon(hour, centerX, simTime);
        }

        // ═══════════════════════════════════════════════════════════════
        // SUN ARC (6h → 18h, east to west)
        // ═══════════════════════════════════════════════════════════════

        private void UpdateSun(float hour, float centerX)
        {
            // Visible from 5.5h (pre-dawn rise) to 18.5h (post-sunset fade)
            bool sunVisible = hour >= 5.5f && hour < 18.5f;
            _sunGO.SetActive(sunVisible);
            if (!sunVisible) return;

            // Arc progress: 0 at 6h (east), 1 at 18h (west)
            float t = Mathf.Clamp01((hour - 6f) / 12f);
            float angle = t * Mathf.PI; // 0 → π

            float x = centerX - Mathf.Cos(angle) * ARC_RADIUS;
            float y = ARC_CENTER_Y + Mathf.Sin(angle) * ARC_RADIUS;

            _sunGO.transform.localPosition = new Vector3(x, y, SPRITE_Z);

            // Fade in/out near horizon
            float alpha = SUN_ALPHA;
            if (hour < 6.5f)       alpha *= Mathf.InverseLerp(5.5f, 6.5f, hour);
            else if (hour > 17.5f) alpha *= Mathf.InverseLerp(18.5f, 17.5f, hour);

            var c = _sunRenderer.material.color;
            _sunRenderer.material.color = new Color(c.r, c.g, c.b, alpha);
        }

        // ═══════════════════════════════════════════════════════════════
        // MOON ARC (18h → 6h next day, east to west)
        // ═══════════════════════════════════════════════════════════════

        private void UpdateMoon(float hour, float centerX, PopVujSimulationTime simTime)
        {
            // Visible from 17.5h (pre-dusk rise) to 6.5h (post-dawn fade)
            bool moonVisible = hour >= 17.5f || hour < 6.5f;
            _moonGO.SetActive(moonVisible);
            if (!moonVisible) return;

            // Moon arc progress: 0 at 18h (east), 1 at 6h (west)
            // Remap: 18→24 = 0→0.5, 0→6 = 0.5→1.0
            float nightHour;
            if (hour >= 18f)
                nightHour = hour - 18f;         // 0–6
            else
                nightHour = hour + 6f;           // 6–12
            float t = Mathf.Clamp01(nightHour / 12f);
            float angle = t * Mathf.PI;

            float x = centerX - Mathf.Cos(angle) * ARC_RADIUS;
            float y = ARC_CENTER_Y + Mathf.Sin(angle) * ARC_RADIUS;

            _moonGO.transform.localPosition = new Vector3(x, y, SPRITE_Z);

            // Fade near horizon
            float alpha = MOON_ALPHA;
            if (hour >= 17.5f && hour < 18.5f)
                alpha *= Mathf.InverseLerp(17.5f, 18.5f, hour);
            else if (hour >= 5.5f && hour < 6.5f)
                alpha *= Mathf.InverseLerp(6.5f, 5.5f, hour);

            var c = _moonMat.color;
            _moonMat.color = new Color(c.r, c.g, c.b, alpha);

            // Update moon phase texture (1 frame per day)
            int phase = simTime.GetMoonPhase();
            if (phase != _lastMoonPhase && _moonFrames != null)
            {
                int idx = Mathf.Clamp(phase, 0, _moonFrames.Length - 1);
                _moonMat.mainTexture = _moonFrames[idx];
                _lastMoonPhase = phase;
            }
        }

    }
}
