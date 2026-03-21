// Copyright CodeGamified 2025-2026
// MIT License — PopVuj
using System.Collections.Generic;
using UnityEngine;

namespace PopVuj.Game
{
    /// <summary>
    /// Visual effects for god powers — makes divine actions visible.
    ///
    /// Every god power in PopVujMatchManager now fires a VFX event.
    /// This system spawns short-lived animated sprite effects that
    /// communicate the action to the player:
    ///
    ///   SendProphet  → golden spell_* spiral + heart particles
    ///   Smite        → lightning flash + explosion_* burst
    ///   SummonBears  → ground tremor + big_smoke_* cloud
    ///   SendOmen     → soul_* ascending + sky glow
    ///   SetWeather   → transition flash (handled by WeatherRenderer)
    ///
    /// Effects are pooled and recycled. Each effect type loads its
    /// sprite sheet from Resources/Animations/ (via SpriteAnimator).
    /// </summary>
    public class GodPowerVFX : MonoBehaviour
    {
        private PopVujMatchManager _match;
        private CityGrid _city;
        private WeatherRenderer _weather;

        // Pool of animated sprite effects
        private readonly List<ActiveEffect> _effects = new List<ActiveEffect>();
        private readonly List<GameObject> _pool = new List<GameObject>();

        // Materials (loaded once)
        private Material _explosionMat;
        private Material _soulMat;
        private Material _spellMat;
        private Material _glowMat;
        private Material _heartMat;
        private Material _smokeMat;
        private Material _critMat;

        // Texture atlases for animated effects
        private SpriteAnimator.AtlasResult _explosionAtlas;
        private SpriteAnimator.AtlasResult _soulAtlas;
        private SpriteAnimator.AtlasResult _spellAtlas;
        private SpriteAnimator.AtlasResult _smokeAtlas;

        private const int POOL_SIZE = 24;

        public void Initialize(PopVujMatchManager match, CityGrid city, WeatherRenderer weather = null)
        {
            _match = match;
            _city = city;
            _weather = weather;

            LoadEffectAssets();
            CreatePool();
            SubscribeEvents();
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
        }

        // ═══════════════════════════════════════════════════════════════
        // ASSET LOADING
        // ═══════════════════════════════════════════════════════════════

        private void LoadEffectAssets()
        {
            // Animated sequences → atlases
            _explosionAtlas = SpriteAnimator.LoadAtlas("explosion");
            _soulAtlas = SpriteAnimator.LoadAtlas("soul");
            _spellAtlas = SpriteAnimator.LoadAtlas("spell");
            _smokeAtlas = SpriteAnimator.LoadAtlas("big_smoke");

            // Materials from atlases
            _explosionMat = SpriteAnimator.CreateAtlasMaterial("explosion", additive: true);
            _soulMat = SpriteAnimator.CreateAtlasMaterial("soul", additive: true);
            _spellMat = SpriteAnimator.CreateAtlasMaterial("spell", additive: true);
            _smokeMat = SpriteAnimator.CreateAtlasMaterial("big_smoke");

            // Single-frame materials
            _glowMat = SpriteAnimator.CreateMaterial("glow", additive: true);
            _heartMat = SpriteAnimator.CreateMaterial("heart");
            _critMat = SpriteAnimator.CreateMaterial("critical_hit", additive: true);
        }

        // ═══════════════════════════════════════════════════════════════
        // POOL
        // ═══════════════════════════════════════════════════════════════

        private void CreatePool()
        {
            for (int i = 0; i < POOL_SIZE; i++)
            {
                var go = CreateEffectObject(i);
                go.SetActive(false);
                _pool.Add(go);
            }
        }

        private GameObject CreateEffectObject(int id)
        {
            var go = new GameObject($"VFX_{id}");
            go.transform.SetParent(transform, false);
            var mf = go.AddComponent<MeshFilter>();
            mf.mesh = SpriteAnimator.GetQuadMesh();
            go.AddComponent<MeshRenderer>();
            return go;
        }

        private GameObject Acquire()
        {
            foreach (var go in _pool)
            {
                if (!go.activeSelf)
                {
                    go.SetActive(true);
                    return go;
                }
            }
            // All busy — create one more
            var newGO = CreateEffectObject(_pool.Count);
            _pool.Add(newGO);
            return newGO;
        }

        // ═══════════════════════════════════════════════════════════════
        // EVENT SUBSCRIPTION
        // ═══════════════════════════════════════════════════════════════

        private void SubscribeEvents()
        {
            if (_match == null) return;
            _match.OnProphetSent += OnProphet;
            _match.OnSmiteTriggered += OnSmite;
            _match.OnBearsSummoned += OnBears;
            _match.OnOmenSent += OnOmen;
        }

        private void UnsubscribeEvents()
        {
            if (_match == null) return;
            _match.OnProphetSent -= OnProphet;
            _match.OnSmiteTriggered -= OnSmite;
            _match.OnBearsSummoned -= OnBears;
            _match.OnOmenSent -= OnOmen;
        }

        // ═══════════════════════════════════════════════════════════════
        // EVENT HANDLERS — spawn effects at appropriate locations
        // ═══════════════════════════════════════════════════════════════

        private void OnProphet()
        {
            // Golden spell spiral at a random chapel
            float x = GetRandomBuildingX(CellType.Chapel);
            SpawnAnimatedEffect(_spellMat, _spellAtlas, x, 2.5f, 1.2f, 1.5f,
                new Color(1f, 0.85f, 0.3f, 1f));

            // Heart particles floating up from the chapel
            for (int i = 0; i < 3; i++)
            {
                float ox = x + Random.Range(-0.5f, 0.5f);
                float oy = 1.5f + Random.Range(0f, 1f);
                SpawnRisingEffect(_heartMat, ox, oy, 0.3f, 2f,
                    new Color(1f, 0.3f, 0.4f, 0.9f), 1.2f);
            }
        }

        private void OnSmite()
        {
            // Lightning bolt at a random heretic/building location
            float x = GetRandomCityX();

            // Flash from WeatherRenderer
            _weather?.TriggerLightningAt(x);

            // Explosion at impact point
            SpawnAnimatedEffect(_explosionMat, _explosionAtlas, x, 1.5f, 1.8f, 0.8f,
                new Color(1f, 0.9f, 0.5f, 1f));

            // Critical hit spark
            if (_critMat != null)
                SpawnStaticEffect(_critMat, x, 2f, 0.6f, 0.3f,
                    new Color(1f, 1f, 0.8f, 1f));
        }

        private void OnBears(int kills)
        {
            // Ground-level smoke eruption + tremor feel
            float x = GetRandomCityX();

            // Large smoke cloud
            SpawnAnimatedEffect(_smokeMat, _smokeAtlas, x, 0.8f, 2.5f, 2f,
                new Color(0.6f, 0.5f, 0.35f, 0.8f));

            // Additional bursts per kill
            for (int i = 0; i < Mathf.Min(kills, 3); i++)
            {
                float ox = x + Random.Range(-1.5f, 1.5f);
                SpawnAnimatedEffect(_explosionMat, _explosionAtlas, ox, 1f, 1f, 0.6f,
                    new Color(0.8f, 0.4f, 0.2f, 0.8f));
            }
        }

        private void OnOmen()
        {
            // Souls ascending from the city
            float cx = _city.Width * CityRenderer.CellSize * 0.5f;

            for (int i = 0; i < 5; i++)
            {
                float ox = cx + Random.Range(-3f, 3f);
                float oy = Random.Range(0.5f, 3f);
                SpawnRisingEffect(_soulMat, ox, oy, 0.4f, 3f,
                    new Color(0.5f, 0.8f, 1f, 0.7f), 2f);
            }

            // Sky glow
            if (_glowMat != null)
                SpawnStaticEffect(_glowMat, cx, 8f, 4f, 2.5f,
                    new Color(0.8f, 0.9f, 1f, 0.3f));
        }

        // ═══════════════════════════════════════════════════════════════
        // EFFECT SPAWNING
        // ═══════════════════════════════════════════════════════════════

        private struct ActiveEffect
        {
            public GameObject GO;
            public float Lifetime;
            public float MaxLifetime;
            public float RiseSpeed;    // Y velocity (0 = stationary)
            public bool Animated;
            public int AtlasCols, AtlasRows, FrameCount;
            public Color BaseColor;
        }

        /// <summary>Spawn a static (non-animated) effect that fades over lifetime.</summary>
        private void SpawnStaticEffect(Material mat, float x, float y, float size, float lifetime, Color tint)
        {
            var go = Acquire();
            var r = go.GetComponent<MeshRenderer>();
            r.material = new Material(mat);
            SetColor(r.material, tint);
            SetTransparent(r.material);

            go.transform.localPosition = new Vector3(x, y, -0.5f);
            go.transform.localScale = Vector3.one * size;
            FaceCamera(go.transform);

            _effects.Add(new ActiveEffect
            {
                GO = go, Lifetime = lifetime, MaxLifetime = lifetime,
                BaseColor = tint, Animated = false,
            });
        }

        /// <summary>Spawn an animated atlas effect at a position.</summary>
        private void SpawnAnimatedEffect(Material mat, SpriteAnimator.AtlasResult atlas,
            float x, float y, float size, float lifetime, Color tint)
        {
            if (mat == null || atlas.FrameCount == 0) return;
            var go = Acquire();
            var r = go.GetComponent<MeshRenderer>();
            r.material = new Material(mat);
            SetColor(r.material, tint);
            SetTransparent(r.material);

            go.transform.localPosition = new Vector3(x, y, -0.5f);
            go.transform.localScale = Vector3.one * size;
            FaceCamera(go.transform);

            _effects.Add(new ActiveEffect
            {
                GO = go, Lifetime = lifetime, MaxLifetime = lifetime,
                BaseColor = tint, Animated = true,
                AtlasCols = atlas.Columns, AtlasRows = atlas.Rows, FrameCount = atlas.FrameCount,
            });
        }

        /// <summary>Spawn an effect that rises upward (souls, hearts).</summary>
        private void SpawnRisingEffect(Material mat, float x, float y, float size, float lifetime,
            Color tint, float riseSpeed)
        {
            if (mat == null) return;
            var go = Acquire();
            var r = go.GetComponent<MeshRenderer>();
            r.material = new Material(mat);
            SetColor(r.material, tint);
            SetTransparent(r.material);

            go.transform.localPosition = new Vector3(x, y, -0.5f);
            go.transform.localScale = Vector3.one * size;
            FaceCamera(go.transform);

            _effects.Add(new ActiveEffect
            {
                GO = go, Lifetime = lifetime, MaxLifetime = lifetime,
                BaseColor = tint, RiseSpeed = riseSpeed,
            });
        }

        // ═══════════════════════════════════════════════════════════════
        // UPDATE — animate + age + recycle
        // ═══════════════════════════════════════════════════════════════

        private void Update()
        {
            float dt = Time.deltaTime;

            for (int i = _effects.Count - 1; i >= 0; i--)
            {
                var fx = _effects[i];
                fx.Lifetime -= dt;

                if (fx.Lifetime <= 0f || fx.GO == null)
                {
                    if (fx.GO != null) fx.GO.SetActive(false);
                    _effects.RemoveAt(i);
                    continue;
                }

                float t = 1f - (fx.Lifetime / fx.MaxLifetime); // 0→1 over life

                // Rise
                if (fx.RiseSpeed > 0f)
                {
                    var pos = fx.GO.transform.localPosition;
                    pos.y += fx.RiseSpeed * dt;
                    fx.GO.transform.localPosition = pos;
                }

                // Fade alpha
                float alpha = fx.Lifetime < 0.5f ? fx.Lifetime / 0.5f : 1f; // fade in last 0.5s
                float baseAlpha = fx.BaseColor.a;
                var col = fx.BaseColor;
                col.a = baseAlpha * alpha;
                SetColor(fx.GO.GetComponent<MeshRenderer>().material, col);

                // Animated atlas UV offset
                if (fx.Animated && fx.FrameCount > 1)
                {
                    int frame = Mathf.FloorToInt(t * fx.FrameCount) % fx.FrameCount;
                    int col2 = frame % fx.AtlasCols;
                    int row = frame / fx.AtlasCols;
                    float tileX = 1f / fx.AtlasCols;
                    float tileY = 1f / fx.AtlasRows;
                    // UV offset: bottom-left of the tile (Unity Y=0 is bottom)
                    float ux = col2 * tileX;
                    float uy = 1f - (row + 1) * tileY;
                    var mat = fx.GO.GetComponent<MeshRenderer>().material;
                    mat.mainTextureOffset = new Vector2(ux, uy);
                    mat.mainTextureScale = new Vector2(tileX, tileY);
                }

                // Billboard
                FaceCamera(fx.GO.transform);

                _effects[i] = fx;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // HELPERS
        // ═══════════════════════════════════════════════════════════════

        private float GetRandomCityX()
        {
            return Random.Range(1f, (_city.Width - 1) * CityRenderer.CellSize);
        }

        private float GetRandomBuildingX(CellType type)
        {
            // Find a random building of the requested type
            var candidates = new List<int>();
            for (int i = 0; i < _city.Width; i++)
            {
                if (_city.GetSurface(i) == type && _city.GetOwner(i) == i)
                    candidates.Add(i);
            }
            if (candidates.Count == 0) return GetRandomCityX();
            int slot = candidates[Random.Range(0, candidates.Count)];
            int bw = _city.GetBuildingWidth(slot);
            return (slot + bw * 0.5f) * CityRenderer.CellSize;
        }

        private static void FaceCamera(Transform t)
        {
            var cam = Camera.main;
            if (cam == null) return;
            t.rotation = Quaternion.LookRotation(t.position - cam.transform.position, Vector3.up);
        }

        private static void SetColor(Material mat, Color c)
        {
            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", c);
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", c);
            if (mat.HasProperty("_TintColor"))
                mat.SetColor("_TintColor", c);
        }

        private static void SetTransparent(Material mat)
        {
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
    }
}
