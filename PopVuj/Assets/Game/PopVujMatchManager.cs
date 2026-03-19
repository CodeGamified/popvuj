// Copyright CodeGamified 2025-2026
// MIT License — PopVuj
using UnityEngine;
using CodeGamified.Time;

namespace PopVuj.Game
{
    /// <summary>
    /// Weather states — controlled by divine decree.
    /// </summary>
    public enum Weather
    {
        Clear    = 0,
        Rain     = 1,
        Storm    = 2,
        Drought  = 3,
    }

    /// <summary>
    /// Match manager for PopVuj — civilization lifecycle.
    ///
    /// Tracks population, faith, disease, crime, sewer population.
    /// Ticks needs each sim-second. Game over when population hits zero.
    ///
    /// God powers are commands issued by the player's script:
    ///   - send_prophet → boosts faith
    ///   - smite → kills heretics, scares population
    ///   - set_weather → affects farms, disease, morale
    ///   - summon_bears → punishes low-faith areas
    ///   - send_omen → temporary faith surge
    /// </summary>
    public class PopVujMatchManager : MonoBehaviour
    {
        private CityGrid _city;

        // Config
        private bool _autoRestart;
        private float _restartDelay;

        // ── Population state ────────────────────────────────────

        public int Population { get; private set; }
        public int SewerPopulation { get; private set; }
        public int Heretics { get; private set; }
        public int Births { get; private set; }
        public int Deaths { get; private set; }

        // ── Civilization meters (0-1) ───────────────────────────

        public float Faith { get; private set; }
        public float Disease { get; private set; }
        public float Crime { get; private set; }

        // ── Weather ─────────────────────────────────────────────

        public Weather CurrentWeather { get; private set; } = Weather.Clear;

        // ── Score ───────────────────────────────────────────────

        public int Score { get; private set; }
        public int HighScore { get; private set; }
        public bool GameOver { get; private set; }
        public bool MatchInProgress { get; private set; }
        public int MatchesPlayed { get; private set; }

        // ── Resources ───────────────────────────────────────────

        public int Wood => _city.Wood;

        // ── Accessors ───────────────────────────────────────────

        public CityGrid City => _city;

        // ── Events ──────────────────────────────────────────────

        public System.Action OnMatchStarted;
        public System.Action OnGameOver;
        public System.Action<int> OnPopulationChanged;
        public System.Action<float> OnFaithChanged;
        public System.Action OnBoardChanged;

        // ── Tick timing ─────────────────────────────────────────

        private float _tickAccumulator;
        private const float TICK_INTERVAL = 1f; // one sim-tick per sim-second

        public void Initialize(CityGrid city, int startPop, float startFaith,
                               bool autoRestart = true, float restartDelay = 5f)
        {
            _city = city;
            Population = startPop;
            Faith = startFaith;
            _autoRestart = autoRestart;
            _restartDelay = restartDelay;
        }

        public void StartMatch()
        {
            _city.Reset();
            Faith = 0.5f;
            Disease = 0f;
            Crime = 0f;
            Heretics = 0;
            Births = 0;
            Deaths = 0;
            SewerPopulation = 0;
            CurrentWeather = Weather.Clear;
            Score = 0;
            GameOver = false;
            MatchInProgress = true;
            _tickAccumulator = 0f;

            OnMatchStarted?.Invoke();
            OnBoardChanged?.Invoke();
        }

        private void Update()
        {
            if (!MatchInProgress || GameOver) return;

            float timeScale = SimulationTime.Instance?.timeScale ?? 1f;
            if (SimulationTime.Instance != null && SimulationTime.Instance.isPaused) return;

            float simDelta = Time.deltaTime * timeScale;
            _tickAccumulator += simDelta;

            while (_tickAccumulator >= TICK_INTERVAL)
            {
                _tickAccumulator -= TICK_INTERVAL;
                SimTick();
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // SIMULATION TICK — one sim-second of civilization
        // ═══════════════════════════════════════════════════════════════

        private void SimTick()
        {
            // ── Faith decay / growth ────────────────────────────
            int chapels = _city.CountSurface(CellType.Chapel);
            float chapelBonus = chapels * 0.005f;
            float faithDrift = chapelBonus - 0.002f; // slow decay without chapels
            Faith = Mathf.Clamp01(Faith + faithDrift);

            // ── Heretics — low faith breeds dissent ─────────────
            Heretics = Mathf.Max(0, (int)(Population * (1f - Faith) * 0.3f));

            // ── Disease — spreads from sewers ───────────────────
            int sewerDens = _city.CountSewer(CellType.SewerDen);
            int fountains = _city.CountSurface(CellType.Fountain);
            float diseasePressure = sewerDens * 0.01f - fountains * 0.008f;
            if (CurrentWeather == Weather.Rain) diseasePressure += 0.003f;
            if (CurrentWeather == Weather.Drought) diseasePressure += 0.005f;
            Disease = Mathf.Clamp01(Disease + diseasePressure);

            // ── Crime — sewer population breeds crime ───────────
            float crimePressure = SewerPopulation * 0.005f - Faith * 0.01f;
            Crime = Mathf.Clamp01(Crime + crimePressure);

            // ── Births — farms + houses + low disease ───────────
            int houses = _city.CountSurface(CellType.House);
            int farms = _city.CountSurface(CellType.Farm);
            float birthRate = (houses * 0.02f + farms * 0.01f) * (1f - Disease * 0.5f);
            if (Random.value < birthRate && Population < houses * 4)
            {
                Population++;
                Births++;
                OnPopulationChanged?.Invoke(Population);
            }

            // ── Deaths — disease + starvation ───────────────────
            float deathRate = Disease * 0.03f;
            if (farms == 0 && Population > 5) deathRate += 0.02f; // starvation
            if (Random.value < deathRate && Population > 0)
            {
                Population--;
                Deaths++;

                // Some die into the sewers (become sewer pop)
                if (Random.value < 0.3f)
                    SewerPopulation++;

                OnPopulationChanged?.Invoke(Population);
            }

            // ── Sewer dynamics — exiles descend ─────────────────
            // Low faith + high crime = people flee underground
            if (Random.value < Crime * 0.02f && Population > 2)
            {
                Population--;
                SewerPopulation++;
                OnPopulationChanged?.Invoke(Population);
            }

            // Sewer dens grow organically
            if (SewerPopulation > 3 && Random.value < 0.05f)
            {
                TryGrowSewerDen();
            }

            // ── Tree growth — wilderness reclaims empty land ────
            if (Random.value < 0.08f) // ~8% chance per tick
                _city.TryGrowTree();

            // ── Workshop production — workshops produce wood ─────
            int workshops = _city.CountSurface(CellType.Workshop);
            if (workshops > 0 && Random.value < workshops * 0.03f)
                _city.AddWood(1);

            // ── Score — one point per population per tick ───────
            Score += Population;
            if (Score > HighScore) HighScore = Score;

            OnFaithChanged?.Invoke(Faith);
            OnBoardChanged?.Invoke();

            // ── Game over check ─────────────────────────────────
            if (Population <= 0)
                EndGame();
        }

        private void TryGrowSewerDen()
        {
            // Find a random sewer cell and upgrade it to a den
            for (int attempt = 0; attempt < 10; attempt++)
            {
                int slot = Random.Range(0, _city.Width);
                if (_city.AddSewerDen(slot))
                    return;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // GOD POWERS — called by IOHandler in response to CUSTOM opcodes
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Send a prophet — boosts faith. Returns 1=success.</summary>
        public int SendProphet()
        {
            if (!MatchInProgress || GameOver) return 0;
            Faith = Mathf.Clamp01(Faith + 0.08f);
            Heretics = Mathf.Max(0, Heretics - 2);
            OnFaithChanged?.Invoke(Faith);
            return 1;
        }

        /// <summary>Smite a heretic — kills one, scares the rest. Returns 1=success.</summary>
        public int Smite()
        {
            if (!MatchInProgress || GameOver || Heretics <= 0) return 0;
            Heretics--;
            Population--;
            Deaths++;
            Faith = Mathf.Clamp01(Faith + 0.03f); // fear boost
            // But some flee to sewers
            if (Random.value < 0.5f) SewerPopulation++;
            OnPopulationChanged?.Invoke(Population);
            return 1;
        }

        /// <summary>Set the weather. Returns 1=success.</summary>
        public int SetWeather(int weatherId)
        {
            if (!MatchInProgress || GameOver) return 0;
            if (weatherId < 0 || weatherId > 3) return 0;
            CurrentWeather = (Weather)weatherId;
            OnBoardChanged?.Invoke();
            return 1;
        }

        /// <summary>Summon bears on heretics — kills several, massive fear. Returns kills.</summary>
        public int SummonBears()
        {
            if (!MatchInProgress || GameOver || Heretics <= 0) return 0;
            int kills = Mathf.Min(Heretics, Random.Range(1, 4));
            Heretics -= kills;
            Population -= kills;
            Deaths += kills;
            Faith = Mathf.Clamp01(Faith + 0.12f);
            SewerPopulation += kills / 2; // survivors flee underground
            OnPopulationChanged?.Invoke(Population);
            return kills;
        }

        /// <summary>Send an omen — big temporary faith surge. Returns 1=success.</summary>
        public int SendOmen()
        {
            if (!MatchInProgress || GameOver) return 0;
            Faith = Mathf.Clamp01(Faith + 0.15f);
            OnFaithChanged?.Invoke(Faith);
            return 1;
        }

        /// <summary>Build a chapel at slot. Costs 2 wood. Returns 1=success.</summary>
        public int BuildChapel(int slot)
        {
            if (!MatchInProgress || GameOver) return 0;
            if (!_city.SpendWood(2)) return 0;
            if (!_city.PlaceBuilding(slot, CellType.Chapel)) { _city.AddWood(2); return 0; }
            return 1;
        }

        /// <summary>Build a house at slot. Costs 1 wood. Returns 1=success.</summary>
        public int BuildHouse(int slot)
        {
            if (!MatchInProgress || GameOver) return 0;
            if (!_city.SpendWood(1)) return 0;
            if (!_city.PlaceBuilding(slot, CellType.House)) { _city.AddWood(1); return 0; }
            return 1;
        }

        /// <summary>Harvest a tree at slot for wood. Returns 1=success.</summary>
        public int HarvestTree(int slot)
        {
            if (!MatchInProgress || GameOver) return 0;
            return _city.HarvestTree(slot) ? 1 : 0;
        }

        /// <summary>Expand a building at slot by 1 tile. Costs 1 wood. Returns 1=success.</summary>
        public int ExpandBuilding(int slot)
        {
            if (!MatchInProgress || GameOver) return 0;
            if (!_city.SpendWood(1)) return 0;
            if (!_city.ExpandBuilding(slot)) { _city.AddWood(1); return 0; }
            return 1;
        }

        /// <summary>Shrink a building at slot by 1 tile. Returns 1 wood. Returns 1=success.</summary>
        public int ShrinkBuilding(int slot)
        {
            if (!MatchInProgress || GameOver) return 0;
            if (!_city.ShrinkBuilding(slot)) return 0;
            _city.AddWood(1); // reclaim material
            return 1;
        }

        // ═══════════════════════════════════════════════════════════════
        // GAME OVER
        // ═══════════════════════════════════════════════════════════════

        private void EndGame()
        {
            GameOver = true;
            MatchInProgress = false;
            MatchesPlayed++;
            if (Score > HighScore) HighScore = Score;
            OnGameOver?.Invoke();

            if (_autoRestart)
                StartCoroutine(RestartAfterDelay());
        }

        private System.Collections.IEnumerator RestartAfterDelay()
        {
            float waited = 0f;
            while (waited < _restartDelay)
            {
                if (SimulationTime.Instance != null && !SimulationTime.Instance.isPaused)
                    waited += Time.deltaTime * (SimulationTime.Instance?.timeScale ?? 1f);
                yield return null;
            }
            StartMatch();
        }
    }
}
