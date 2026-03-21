// Copyright CodeGamified 2025-2026
// MIT License — PopVuj
using UnityEngine;
using CodeGamified.Settings;
using PopVuj.Game;

namespace PopVuj.Core
{
    /// <summary>
    /// Ambient background SFX driven by camera position and world state.
    ///
    /// Zones are determined by where the camera looks along the X axis
    /// relative to the city strip:
    ///
    ///   ← wilderness (forest) │ village │ harbor │ ocean →
    ///
    /// Within the village zone, the dominant building type under the
    /// camera (Market, Chapel, etc.) tints the ambient layer further.
    ///
    /// Weather overlays (rain, wind) play on a separate AudioSource
    /// and crossfade independently.
    ///
    /// Wildlife sounds (seagulls, crickets, ducks, raven) are played
    /// sporadically as one-shot stingers based on zone + time-of-day.
    /// </summary>
    public class PopVujAmbientSFX : MonoBehaviour
    {
        // ═══════════════════════════════════════════════════════════
        //  DEPENDENCIES (set via Initialize)
        // ═══════════════════════════════════════════════════════════

        private CityGrid _city;
        private PopVujMatchManager _match;

        // ═══════════════════════════════════════════════════════════
        //  AUDIO SOURCES — two layers: ambient loop + stinger
        // ═══════════════════════════════════════════════════════════

        private AudioSource _ambientA;      // current ambient loop
        private AudioSource _ambientB;      // crossfade target
        private AudioSource _weatherSource; // weather overlay loop
        private AudioSource _stingerSource; // one-shot wildlife stingers

        // ═══════════════════════════════════════════════════════════
        //  CLIPS — loaded from Resources/SFX/
        // ═══════════════════════════════════════════════════════════

        // Zone loops
        private AudioClip _ocean;
        private AudioClip _harbor;
        private AudioClip _village;
        private AudioClip _market;
        private AudioClip _tavern;
        private AudioClip _hall;       // chapel / great hall
        private AudioClip _forest;
        private AudioClip _cityClip;

        // Sub-zone loops (village building types)
        private AudioClip _stream;         // Fountain
        private AudioClip _smallwaterfall; // Fountain (variant)
        private AudioClip _rainforest;     // dense-tree Forest variant

        // Sewer loops
        private AudioClip _dripping;
        private AudioClip _bats;

        // Weather overlays
        private AudioClip[] _rain;
        private AudioClip _wind;
        private AudioClip _fire;           // drought overlay
        private AudioClip _boilingwater;   // drought overlay (variant)

        // Cave ambient stingers (sewer deep)
        private AudioClip[] _cave;         // cave1-13

        // Wildlife stingers
        private AudioClip _seagulls;
        private AudioClip _crickets;
        private AudioClip _ducks;
        private AudioClip _raven;
        private AudioClip _frogs;
        private AudioClip _wolf;
        private AudioClip _bees;
        private AudioClip _goats;
        private AudioClip _windchimes;     // chapel / clear weather
        private AudioClip _waterfall;      // ocean / harbor
        private AudioClip _battle;         // sewer conflict
        private AudioClip _torture;        // sewer dungeon

        // ═══════════════════════════════════════════════════════════
        //  STATE
        // ═══════════════════════════════════════════════════════════

        private enum Zone { Ocean, Harbor, Village, Forest, Sewer }

        private Zone _currentZone = Zone.Village;
        private AudioClip _currentAmbientClip;
        private AudioClip _currentWeatherClip;
        private bool _aIsActive = true;

        // Crossfade
        private float _crossfadeTimer;
        private const float CROSSFADE_DURATION = 2f;
        private bool _crossfading;

        // Stinger cooldown
        private float _stingerCooldown;
        private const float STINGER_MIN_INTERVAL = 8f;
        private const float STINGER_MAX_INTERVAL = 25f;

        // City layout cache (updated when grid changes)
        private float _harborLeftX;
        private float _cityLeftX;
        private float _cityRightX;
        private float _cityTotalWidth;

        // ═══════════════════════════════════════════════════════════
        //  INITIALIZATION
        // ═══════════════════════════════════════════════════════════

        public void Initialize(CityGrid city, PopVujMatchManager match)
        {
            _city = city;
            _match = match;

            // Create audio sources
            _ambientA = CreateLoopSource("AmbientA", 0f);
            _ambientB = CreateLoopSource("AmbientB", 0f);
            _weatherSource = CreateLoopSource("Weather", 0f);
            _stingerSource = gameObject.AddComponent<AudioSource>();
            _stingerSource.playOnAwake = false;
            _stingerSource.spatialBlend = 0f;
            _stingerSource.loop = false;

            LoadClips();
            UpdateCityBounds();
            _city.OnGridChanged += UpdateCityBounds;

            _stingerCooldown = Random.Range(STINGER_MIN_INTERVAL, STINGER_MAX_INTERVAL);
        }

        private void OnDestroy()
        {
            if (_city != null)
                _city.OnGridChanged -= UpdateCityBounds;
        }

        // ═══════════════════════════════════════════════════════════
        //  UPDATE
        // ═══════════════════════════════════════════════════════════

        private void Update()
        {
            if (_city == null) return;

            float dt = Time.deltaTime;
            float sfxVol = SettingsBridge.SfxVolume;

            // --- Determine zone from camera X & Y ---
            var cam = UnityEngine.Camera.main;
            if (cam == null) return;

            float camX = cam.transform.position.x;
            float camY = cam.transform.position.y;
            Zone zone = ClassifyZone(camX, camY);

            // --- Pick ambient clip for zone ---
            AudioClip desiredClip = PickAmbientClip(zone, camX);

            if (desiredClip != _currentAmbientClip)
            {
                StartCrossfade(desiredClip);
                _currentZone = zone;
            }

            // --- Crossfade tick ---
            if (_crossfading)
            {
                _crossfadeTimer += dt;
                float t = Mathf.Clamp01(_crossfadeTimer / CROSSFADE_DURATION);
                float targetVol = 0.25f * sfxVol;

                if (_aIsActive)
                {
                    _ambientA.volume = Mathf.Lerp(targetVol, 0f, t);
                    _ambientB.volume = Mathf.Lerp(0f, targetVol, t);
                }
                else
                {
                    _ambientB.volume = Mathf.Lerp(targetVol, 0f, t);
                    _ambientA.volume = Mathf.Lerp(0f, targetVol, t);
                }

                if (t >= 1f)
                {
                    _crossfading = false;
                    // Stop the faded-out source
                    if (_aIsActive) _ambientA.Stop(); else _ambientB.Stop();
                    _aIsActive = !_aIsActive;
                }
            }
            else
            {
                // Steady-state volume tracking
                float vol = 0.25f * sfxVol;
                if (_aIsActive) _ambientA.volume = vol;
                else            _ambientB.volume = vol;
            }

            // --- Weather overlay ---
            UpdateWeatherOverlay(sfxVol);

            // --- Wildlife stingers ---
            _stingerCooldown -= dt;
            if (_stingerCooldown <= 0f)
            {
                TryPlayStinger(zone, sfxVol);
                _stingerCooldown = Random.Range(STINGER_MIN_INTERVAL, STINGER_MAX_INTERVAL);
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  ZONE CLASSIFICATION
        // ═══════════════════════════════════════════════════════════

        private Zone ClassifyZone(float camX, float camY)
        {
            // Below ground = sewer
            if (camY < -1f) return Zone.Sewer;

            // Past the right edge of the city (beyond piers) = ocean
            if (camX > _cityTotalWidth + 2f) return Zone.Ocean;

            // Harbor zone: from the leftmost harbor slot to the city edge
            if (camX >= _harborLeftX) return Zone.Harbor;

            // Left of the settled area = forest / wilderness
            if (camX < _cityLeftX - 2f) return Zone.Forest;

            // Otherwise = village
            return Zone.Village;
        }

        private AudioClip PickAmbientClip(Zone zone, float camX)
        {
            switch (zone)
            {
                case Zone.Ocean:  return _ocean;
                case Zone.Harbor: return _harbor;
                case Zone.Forest: return PickForestClip();
                case Zone.Sewer:  return _dripping;
                case Zone.Village:
                    return PickVillageSubClip(camX);
                default:
                    return _village;
            }
        }

        private AudioClip PickVillageSubClip(float camX)
        {
            // Sample the cell under the camera to choose a sub-ambient
            int slot = Mathf.FloorToInt(camX / CityRenderer.CellSize);
            if (slot < 0 || slot >= _city.Width) return _village;

            var bldg = _city.GetBuildingAt(slot);
            switch (bldg)
            {
                case CellType.Market:    return _market ?? _village;
                case CellType.Chapel:    return _hall ?? _village;
                case CellType.Workshop:  return _cityClip ?? _village;
                case CellType.Shipyard:  return _harbor ?? _village;
                case CellType.Fountain:  return _stream ?? _village;
                case CellType.Farm:      return _tavern ?? _village;
                default:                 return _village;
            }
        }

        /// <summary>Pick forest clip — use rainforest variant if area is mostly trees.</summary>
        private AudioClip PickForestClip()
        {
            if (_rainforest != null)
            {
                // Count trees in left wilderness
                int trees = 0;
                int leftBound = Mathf.FloorToInt(_cityLeftX / CityRenderer.CellSize);
                for (int i = 0; i < leftBound && i < _city.Width; i++)
                    if (_city.GetSurface(i) == CellType.Tree) trees++;
                if (trees > 5)
                    return _rainforest;
            }
            return _forest;
        }

        // ═══════════════════════════════════════════════════════════
        //  CROSSFADE
        // ═══════════════════════════════════════════════════════════

        private void StartCrossfade(AudioClip newClip)
        {
            if (newClip == null) return;
            _currentAmbientClip = newClip;

            // The inactive source gets the new clip
            AudioSource incoming = _aIsActive ? _ambientB : _ambientA;
            incoming.clip = newClip;
            incoming.volume = 0f;
            incoming.Play();

            _crossfadeTimer = 0f;
            _crossfading = true;
        }

        // ═══════════════════════════════════════════════════════════
        //  WEATHER OVERLAY
        // ═══════════════════════════════════════════════════════════

        private void UpdateWeatherOverlay(float sfxVol)
        {
            AudioClip desired = null;
            float targetVol = 0f;

            switch (_match.CurrentWeather)
            {
                case Weather.Rain:
                    desired = PickRain();
                    targetVol = 0.2f;
                    break;
                case Weather.Storm:
                    desired = PickRain();
                    targetVol = 0.35f;
                    break;
                case Weather.Drought:
                    desired = Random.value < 0.5f ? _fire : _boilingwater;
                    if (desired == null) desired = _wind;
                    targetVol = 0.15f;
                    break;
                case Weather.Clear:
                default:
                    targetVol = 0f;
                    break;
            }

            targetVol *= sfxVol;

            if (desired != _currentWeatherClip && desired != null)
            {
                _currentWeatherClip = desired;
                _weatherSource.clip = desired;
                _weatherSource.Play();
            }

            // Smooth volume towards target
            _weatherSource.volume = Mathf.MoveTowards(
                _weatherSource.volume, targetVol, Time.deltaTime * 0.5f);

            if (desired == null && _weatherSource.volume < 0.01f && _weatherSource.isPlaying)
            {
                _weatherSource.Stop();
                _currentWeatherClip = null;
            }
        }

        private AudioClip PickRain()
        {
            if (_rain == null || _rain.Length == 0) return null;
            return _rain[Random.Range(0, _rain.Length)];
        }

        // ═══════════════════════════════════════════════════════════
        //  WILDLIFE STINGERS
        // ═══════════════════════════════════════════════════════════

        private void TryPlayStinger(Zone zone, float sfxVol)
        {
            AudioClip clip = null;
            float vol = 0.15f;

            switch (zone)
            {
                case Zone.Ocean:
                    clip = Pick(_seagulls, _waterfall);
                    break;
                case Zone.Harbor:
                    clip = Pick(_seagulls, _ducks, _waterfall);
                    break;
                case Zone.Village:
                    clip = Pick(_crickets, _goats, _bees, _windchimes);
                    break;
                case Zone.Forest:
                    clip = Pick(_wolf, _raven, _crickets);
                    break;
                case Zone.Sewer:
                    clip = PickSewerStinger();
                    vol = 0.2f;
                    break;
            }

            if (clip != null)
                _stingerSource.PlayOneShot(clip, vol * sfxVol);
        }

        private AudioClip PickSewerStinger()
        {
            // Mix bats, frogs, cave ambience, battle, torture
            if (_cave != null && _cave.Length > 0 && Random.value < 0.4f)
                return _cave[Random.Range(0, _cave.Length)];
            return Pick(_bats, _frogs, _battle, _torture);
        }

        private static AudioClip Pick(params AudioClip[] clips)
        {
            // Filter nulls, pick random
            int count = 0;
            for (int i = 0; i < clips.Length; i++)
                if (clips[i] != null) count++;
            if (count == 0) return null;

            int pick = Random.Range(0, count);
            int idx = 0;
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i] == null) continue;
                if (idx == pick) return clips[i];
                idx++;
            }
            return null;
        }

        // ═══════════════════════════════════════════════════════════
        //  CITY BOUNDS
        // ═══════════════════════════════════════════════════════════

        private void UpdateCityBounds()
        {
            _cityTotalWidth = _city.Width * CityRenderer.CellSize;

            // Find leftmost non-empty non-tree slot
            _cityLeftX = 0f;
            for (int i = 0; i < _city.Width; i++)
            {
                var s = _city.GetSurface(i);
                if (s != CellType.Empty && s != CellType.Tree)
                {
                    _cityLeftX = i * CityRenderer.CellSize;
                    break;
                }
            }

            // Find leftmost harbor slot
            _harborLeftX = _cityTotalWidth;
            for (int i = 0; i < _city.Width; i++)
            {
                if (_city.IsHarborSlot(i))
                {
                    _harborLeftX = i * CityRenderer.CellSize;
                    break;
                }
            }

            // Rightmost building
            _cityRightX = _cityTotalWidth;
            for (int i = _city.Width - 1; i >= 0; i--)
            {
                var s = _city.GetSurface(i);
                if (s != CellType.Empty && s != CellType.Tree)
                {
                    _cityRightX = (i + 1) * CityRenderer.CellSize;
                    break;
                }
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  CLIP LOADING
        // ═══════════════════════════════════════════════════════════

        private void LoadClips()
        {
            // Zone loops
            _ocean    = Load("extra/ocean");
            _harbor   = Load("extra/harbor");
            _village  = Load("extra/village");
            _market   = Load("extra/market");
            _tavern   = Load("extra/tavern");
            _hall     = Load("extra/hall");
            _forest   = Load("extra/forest");
            _cityClip = Load("extra/city");

            // Sub-zone loops
            _stream         = Load("extra/stream");
            _smallwaterfall = Load("extra/smallwaterfall");
            _rainforest     = Load("extra/rainforest");

            // Sewer loops
            _dripping = Load("extra/dripping");
            _bats     = Load("extra/bats");

            // Cave ambient bank
            _cave = new AudioClip[13];
            for (int i = 0; i < 13; i++)
                _cave[i] = Load($"ambient/cave/cave{i + 1}");

            // Weather
            _rain = new[]
            {
                Load("ambient/weather/rain1"),
                Load("ambient/weather/rain2"),
                Load("ambient/weather/rain3"),
                Load("ambient/weather/rain4"),
                Load("ambient/weather/rain5"),
            };
            _wind = Load("extra/wind");
            _fire         = Load("fire/fire");
            _boilingwater = Load("extra/boilingwater");

            // Wildlife stingers
            _seagulls   = Load("extra/seagulls");
            _crickets   = Load("extra/crickets");
            _ducks      = Load("extra/ducks");
            _raven      = Load("extra/raven");
            _frogs      = Load("extra/frogs");
            _wolf       = Load("extra/wolf");
            _bees       = Load("extra/bees");
            _goats      = Load("extra/goats");
            _windchimes = Load("extra/windchimes");
            _waterfall  = Load("extra/waterfall");
            _battle     = Load("extra/battle");
            _torture    = Load("extra/torture");
        }

        private static AudioClip Load(string path)
        {
            var clip = Resources.Load<AudioClip>($"SFX/{path}");
            if (clip == null)
                Debug.LogWarning($"[AmbientSFX] Missing clip: SFX/{path}");
            return clip;
        }

        private AudioSource CreateLoopSource(string name, float volume)
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            src.loop = true;
            src.volume = volume;
            return src;
        }
    }
}
