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

        /// <summary>Total sewer den slots (derived from houses). Replaces SewerPopulation.</summary>
        public int SewerDenCount => _city.CountSewer(SewerType.Den);

        // ── Harbor ─────────────────────────────────────────────────

        private HarborManager _harbor;
        public HarborManager Harbor => _harbor;
        public int ShipCount => _harbor != null ? _harbor.ShipCount : 0;
        public int DockedShips => _harbor != null ? _harbor.DockedShipCount : 0;
        public int ShipsAtSea => _harbor != null ? _harbor.ShipsAtSea : 0;
        public int HarborWorkers => _harbor != null ? _harbor.HarborWorkerCount : 0;
        public int TradeIncome => _harbor != null ? _harbor.TradeIncome : 0;

        // ── Accessors ───────────────────────────────────────────

        public CityGrid City => _city;

        // ── Events ──────────────────────────────────────────────

        public System.Action OnMatchStarted;
        public System.Action OnGameOver;
        public System.Action<int> OnPopulationChanged;
        public System.Action<float> OnFaithChanged;
        public System.Action OnBoardChanged;

        // ── VFX events (fired by god powers for visual feedback) ────

        public System.Action OnProphetSent;
        public System.Action OnSmiteTriggered;
        public System.Action<int> OnBearsSummoned;
        public System.Action OnOmenSent;

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

        public void SetHarbor(HarborManager harbor) { _harbor = harbor; }

        public void StartMatch()
        {
            _city.Reset();
            Faith = 0.5f;
            Disease = 0f;
            Crime = 0f;
            Heretics = 0;
            Births = 0;
            Deaths = 0;
            CurrentWeather = Weather.Clear;
            Score = 0;
            GameOver = false;
            MatchInProgress = true;
            _tickAccumulator = 0f;

            OnMatchStarted?.Invoke();
            OnBoardChanged?.Invoke();

            // Reset harbor
            _harbor?.ResetHarbor();
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

            // ── Disease — spreads from sewer dens (derived) ──────
            int denSlots = _city.CountSewer(SewerType.Den);
            int fountains = _city.CountSurface(CellType.Fountain);
            float diseasePressure = denSlots * 0.008f - fountains * 0.008f;
            if (CurrentWeather == Weather.Rain) diseasePressure += 0.003f;
            if (CurrentWeather == Weather.Drought) diseasePressure += 0.005f;
            Disease = Mathf.Clamp01(Disease + diseasePressure);

            // ── Crime — underground dens breed crime ────────────
            float crimePressure = denSlots * 0.004f - Faith * 0.01f;
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
                OnPopulationChanged?.Invoke(Population);
            }

            // ── Crime exiles — high crime drives people out ─────
            if (Random.value < Crime * 0.02f && Population > 2)
            {
                Population--;
                OnPopulationChanged?.Invoke(Population);
            }

            // ── Tree growth — wilderness reclaims empty land ────
            if (Random.value < 0.08f) // ~8% chance per tick
                _city.TryGrowTree();

            // ── Workshop production — workshops produce wood ─────
            int workshops = _city.CountSurface(CellType.Workshop);
            if (workshops > 0 && Random.value < workshops * 0.03f)
                _city.AddWood(1);

            // ── Harbor trade bonus — docked ships with crew score trade  ───
            if (_harbor != null && _harbor.DockedShipCount > 0)
            {
                // Trade income adds to score
                Score += _harbor.TradeIncome;
            }

            // ── Score — one point per population per tick ───────
            Score += Population;
            if (Score > HighScore) HighScore = Score;

            OnFaithChanged?.Invoke(Faith);
            OnBoardChanged?.Invoke();

            // ── Game over check ─────────────────────────────────
            if (Population <= 0)
                EndGame();
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
            OnProphetSent?.Invoke();
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
            OnPopulationChanged?.Invoke(Population);
            OnSmiteTriggered?.Invoke();
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
            OnPopulationChanged?.Invoke(Population);
            OnBearsSummoned?.Invoke(kills);
            return kills;
        }

        /// <summary>Send an omen — big temporary faith surge. Returns 1=success.</summary>
        public int SendOmen()
        {
            if (!MatchInProgress || GameOver) return 0;
            Faith = Mathf.Clamp01(Faith + 0.15f);
            OnFaithChanged?.Invoke(Faith);
            OnOmenSent?.Invoke();
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
        // HARBOR COMMANDS — build/manage harbor infrastructure and ships
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Build a shipyard at slot. Costs 3 wood. Returns 1=success.</summary>
        public int BuildShipyard(int slot)
        {
            if (!MatchInProgress || GameOver) return 0;
            if (!_city.SpendWood(3)) return 0;
            if (!_city.PlaceBuilding(slot, CellType.Shipyard)) { _city.AddWood(3); return 0; }
            return 1;
        }

        /// <summary>Build a warehouse at slot. Costs 4 wood. Returns 1=success.</summary>
        public int BuildWarehouse(int slot)
        {
            if (!MatchInProgress || GameOver) return 0;
            if (!_city.SpendWood(4)) return 0;
            if (!_city.PlaceBuilding(slot, CellType.Warehouse)) { _city.AddWood(4); return 0; }
            return 1;
        }

        /// <summary>Build a pier at slot. Costs 2 wood. Must be contiguous from shore. Returns 1=success.</summary>
        public int BuildPier(int slot)
        {
            if (!MatchInProgress || GameOver) return 0;
            if (!_city.IsPierBuildable(slot)) return 0;
            if (!_city.SpendWood(2)) return 0;
            if (!_city.PlaceBuilding(slot, CellType.Pier)) { _city.AddWood(2); return 0; }
            return 1;
        }

        /// <summary>Install a crane fixture on a pier slot. Costs 3 wood. Returns 1=success.</summary>
        public int BuildCrane(int slot)
        {
            if (!MatchInProgress || GameOver) return 0;
            if (_city.GetBuildingAt(slot) != CellType.Pier) return 0;
            if (_city.GetPierFixture(slot) != PierFixture.None) return 0;
            if (!_city.SpendWood(3)) return 0;
            if (!_city.SetPierFixture(slot, PierFixture.Crane)) { _city.AddWood(3); return 0; }
            return 1;
        }

        /// <summary>Build a ship of given width. Cost scales with size. Returns 1=success.</summary>
        public int BuildShipCmd(int width)
        {
            if (!MatchInProgress || GameOver) return 0;
            if (_harbor == null) return 0;
            return _harbor.BuildShip(width) ? 1 : 0;
        }

        /// <summary>Launch the next completed ship. Returns 1=success.</summary>
        public int LaunchShip()
        {
            if (!MatchInProgress || GameOver) return 0;
            if (_harbor == null) return 0;
            return _harbor.LaunchShip() ? 1 : 0;
        }

        /// <summary>Send a docked ship on a trade route. Returns 1=success.</summary>
        public int SendTrade(int routeId)
        {
            if (!MatchInProgress || GameOver) return 0;
            if (_harbor == null) return 0;
            return _harbor.SendTrade(routeId) ? 1 : 0;
        }

        /// <summary>Repair the most damaged docked ship. Costs 1 wood. Returns 1=success.</summary>
        public int RepairShip()
        {
            if (!MatchInProgress || GameOver) return 0;
            if (_harbor == null) return 0;
            return _harbor.RepairShip() ? 1 : 0;
        }

        /// <summary>Bless the next departing ship — prophet effect at sea. Returns 1=success.</summary>
        public int BlessShip()
        {
            if (!MatchInProgress || GameOver) return 0;
            // Bless doubles as a faith + trade bonus for the next voyage
            Faith = Mathf.Clamp01(Faith + 0.05f);
            OnFaithChanged?.Invoke(Faith);
            return 1;
        }

        /// <summary>
        /// Set a module on a docked ship. Costs 1 wood.
        /// moduleType maps to ShipModule enum (0-16). Returns 1=success.
        /// </summary>
        public int SetShipModule(int shipId, int tileIndex, int moduleType)
        {
            if (!MatchInProgress || GameOver) return 0;
            if (_harbor == null) return 0;
            if (moduleType < 0 || moduleType > 16) return 0;
            return _harbor.SetShipModule(shipId, tileIndex, (ShipModule)moduleType) ? 1 : 0;
        }

        /// <summary>
        /// Query what module is at a given linear index on a ship.
        /// Returns the ShipModule enum value, or -1 if invalid.
        /// </summary>
        public int GetShipModule(int shipId, int tileIndex)
        {
            if (_harbor == null) return -1;
            var ship = _harbor.FindShipById(shipId);
            if (ship == null) return -1;
            return (int)ship.GetModule(tileIndex);
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
