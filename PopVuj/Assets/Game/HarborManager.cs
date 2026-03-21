// Copyright CodeGamified 2025-2026
// MIT License — PopVuj
using System.Collections.Generic;
using UnityEngine;
using CodeGamified.Time;
using PopVuj.Crew;

namespace PopVuj.Game
{
    /// <summary>
    /// Manages the harbor district — ships, trade routes, crane operations,
    /// and the interface between land economy and sea.
    ///
    /// Ships travel on the road surface (Z=0) and dock along the pier’s X
    /// extent. The dockable area spans from the leftmost Pier cell to
    /// the rightmost. Ships pack left-to-right by their widths — the pier
    /// must be longer than the sum of all docked ship lengths.
    ///
    /// Crane fixtures on pier slots service ships by spatial X overlap.
    /// Any crane-fixture slot whose X range intersects a ship can load/unload
    /// it. N crane fixtures with N operators can service N ships simultaneously.
    ///
    /// Ships are modular "buildings" — width defines hull class:
    ///   1w=Canoe, 2w=Sloop, 3w=Brigantine, 4w=Frigate, 5w=Ship of the Line
    ///
    /// This is a C# simulation layer. Not scriptable via Python directly —
    /// scripting builtins delegate to MatchManager which calls here.
    /// </summary>
    public class HarborManager : MonoBehaviour
    {
        private CityGrid _city;
        private PopVujMatchManager _match;

        private readonly List<Ship> _ships = new List<Ship>();
        private int _nextShipId;

        // ── Trade income tracking ───────────────────────────────
        public int TradeIncome { get; private set; }

        // ── Public API ──────────────────────────────────────────
        public IReadOnlyList<Ship> Ships => _ships;
        public int ShipCount => _ships.Count;
        public int DockedShipCount { get { int c = 0; foreach (var s in _ships) if (IsDocked(s)) c++; return c; } }
        public int ShipsAtSea { get { int c = 0; foreach (var s in _ships) if (IsAtSea(s)) c++; return c; } }
        public int HarborWorkerCount { get; private set; }

        // ── Events ──────────────────────────────────────────────
        public System.Action OnShipsChanged;
        public System.Action<Ship> OnShipLaunched;
        public System.Action<Ship> OnShipReturned;
        public System.Action<Ship> OnShipLost;

        // ── Timing ──────────────────────────────────────────────
        private float _tickAccumulator;
        private const float TICK_INTERVAL = 1f;

        // ── Pier extent (X range where ships can dock) ─────────
        // Updated whenever the grid changes. Ships pack left-to-right
        // within [_pierLeftX, _pierRightX] by their widths.
        private float _pierLeftX;
        private float _pierRightX;

        // ── Crane throughput ────────────────────────────────────
        private const float CRANE_LOAD_INTERVAL = 4f;  // sim-seconds per cargo unit transfer
        private readonly Dictionary<int, float> _craneTimers = new Dictionary<int, float>();

        // ── Anchoring ───────────────────────────────────────
        private const float ANCHOR_TIMEOUT = 30f; // sim-seconds before anchored ship gives up

        public void Initialize(CityGrid city, PopVujMatchManager match)
        {
            _city = city;
            _match = match;
            _city.OnGridChanged += UpdatePierExtent;
            UpdatePierExtent();
        }

        private void OnDestroy()
        {
            if (_city != null)
                _city.OnGridChanged -= UpdatePierExtent;
        }

        public void ResetHarbor()
        {
            _ships.Clear();
            _nextShipId = 0;
            TradeIncome = 0;
            HarborWorkerCount = 0;
            _craneTimers.Clear();
            UpdatePierExtent();
            OnShipsChanged?.Invoke();
        }

        /// <summary>
        /// Spawn pre-built demo ships for visual testing.
        /// Call after match start (after ResetHarbor clears the fleet).
        /// </summary>
        public void SpawnDemoShips()
        {
            UpdatePierExtent();

            int[] widths = { 1, 2, 3 };
            foreach (int w in widths)
            {
                var ship = new Ship(_nextShipId++, w, Ship.NO_SHIPYARD);
                ship.BuildProgress = 1f;
                ship.ShipyardOrigin = Ship.NO_SHIPYARD;
                ship.Condition = 1f;
                _ships.Add(ship);

                // Ships arrive carrying lumber from the timber islands
                ship.CargoCount = ship.CargoCapacity;
                ship.HoldCargoKind = CargoKind.Log;
                ship.Route = TradeRoute.TimberIslands;

                int crane = FindFreeCraneSlot();
                if (crane >= 0)
                {
                    ship.TargetCraneSlot = crane;
                    ship.DockX = (crane + 0.5f) * CityRenderer.CellSize;
                    ship.X = ship.DockX;
                    ship.State = ShipState.Unloading; // waiting for minion haulers
                }
                else
                {
                    AnchorShip(ship);
                }
            }

            OnShipsChanged?.Invoke();
        }

        // ═══════════════════════════════════════════════════════════════
        // UPDATE
        // ═══════════════════════════════════════════════════════════════

        private void Update()
        {
            if (_match == null || !_match.MatchInProgress || _match.GameOver) return;

            float timeScale = SimulationTime.Instance?.timeScale ?? 1f;
            if (SimulationTime.Instance != null && SimulationTime.Instance.isPaused) return;

            float simDelta = Time.deltaTime * timeScale;
            _tickAccumulator += simDelta;

            while (_tickAccumulator >= TICK_INTERVAL)
            {
                _tickAccumulator -= TICK_INTERVAL;
                HarborTick();
            }

            // Animate departing/arriving ships
            AnimateShips(simDelta);
        }

        // ═══════════════════════════════════════════════════════════════
        // HARBOR TICK — one sim-second
        // ═══════════════════════════════════════════════════════════════

        private void HarborTick()
        {
            // Count harbor workers (shipyard + pier + crane staffing)
            UpdateHarborWorkerCount();

            // Advance ship builds in shipyards
            TickShipConstruction();

            // Crane loading/unloading
            TickCranes();

            // Voyage progress
            TickVoyages();

            // Condition decay for ships at sea
            TickCondition();

            // Anchored ships waiting for a crane slot
            TickAnchored();
        }

        private void UpdateHarborWorkerCount()
        {
            int count = 0;
            for (int i = 0; i < _city.Width; i++)
            {
                int origin = _city.GetOwner(i);
                if (origin != i) continue;
                var type = _city.GetSurface(i);
                if (type == CellType.Shipyard || type == CellType.Pier)
                {
                    int bw = _city.GetBuildingWidth(i);
                    count += BuildingSlots.GetSlotCount(type, bw);
                }
            }
            // Add sailors at sea
            foreach (var ship in _ships)
                if (IsAtSea(ship))
                    count += ship.CrewCount;
            HarborWorkerCount = count;
        }

        // ── Construction ────────────────────────────────────────

        private void TickShipConstruction()
        {
            for (int i = _ships.Count - 1; i >= 0; i--)
            {
                var ship = _ships[i];
                if (ship.State != ShipState.Building) continue;

                // Check shipyard still exists
                if (ship.ShipyardOrigin < 0 || _city.GetSurface(ship.ShipyardOrigin) != CellType.Shipyard)
                {
                    _ships.RemoveAt(i);
                    OnShipsChanged?.Invoke();
                    continue;
                }

                // Build rate: more shipwrights = faster build
                int shipyardWidth = _city.GetBuildingWidth(ship.ShipyardOrigin);
                int workers = BuildingSlots.GetSlotCount(CellType.Shipyard, shipyardWidth);
                // Each worker contributes ~0.01 progress per tick; foreman gives 1.5x bonus
                float buildRate = (workers - 1) * 0.01f + 0.015f; // foreman + shipwrights
                float totalCost = Ship.GetBuildCost(ship.Width);
                ship.BuildProgress += buildRate / Mathf.Max(1f, totalCost * 0.5f);

                if (ship.BuildProgress >= 1f)
                {
                    ship.BuildProgress = 1f;
                    // Save shipyard position before clearing origin
                    int syOrigin = ship.ShipyardOrigin;
                    int syWidth = _city.GetBuildingWidth(syOrigin);
                    ship.X = (syOrigin + syWidth * 0.5f) * CityRenderer.CellSize;

                    ship.State = ShipState.Launched;
                    ship.ShipyardOrigin = Ship.NO_SHIPYARD;
                    OnShipLaunched?.Invoke(ship);
                    OnShipsChanged?.Invoke();
                }
            }
        }

        // ── Crane operations ────────────────────────────────────

        private void TickCranes()
        {
            // Iterate pier slots looking for crane fixtures
            for (int i = 0; i < _city.Width; i++)
            {
                if (_city.GetPierFixture(i) != PierFixture.Crane) continue;

                Ship docked = FindShipAtCrane(i);
                if (docked == null) continue;

                // Loading is still crane-driven (automatic when ship is in Loading state)
                if (docked.State == ShipState.Loading)
                {
                    if (!_craneTimers.ContainsKey(i))
                        _craneTimers[i] = 0f;

                    _craneTimers[i] += 1f;
                    if (_craneTimers[i] < CRANE_LOAD_INTERVAL) continue;
                    _craneTimers[i] = 0f;

                    if (docked.CargoCount < docked.CargoCapacity)
                    {
                        docked.CargoCount++;
                        if (docked.CargoCount >= docked.CargoCapacity)
                            docked.State = ShipState.Idle;
                        OnShipsChanged?.Invoke();
                    }
                }
                // Unloading is handled by minion haulers — no automatic transfer
            }
        }

        // ── Voyages ─────────────────────────────────────────────

        private void TickVoyages()
        {
            for (int i = _ships.Count - 1; i >= 0; i--)
            {
                var ship = _ships[i];
                if (ship.State != ShipState.Voyage) continue;

                ship.VoyageTimer -= 1f;
                if (ship.VoyageTimer <= 0f)
                {
                    // Check for loss (risk roll)
                    float risk = Ship.GetRouteRisk(ship.Route);
                    if (_match.CurrentWeather == Weather.Storm) risk *= 2f;
                    if (ship.Condition < 0.5f) risk *= 1.5f;

                    if (Random.value < risk)
                    {
                        // Ship lost at sea
                        OnShipLost?.Invoke(ship);
                        _ships.RemoveAt(i);
                        OnShipsChanged?.Invoke();
                        continue;
                    }

                    // Ship returns — fill hold with route-specific cargo
                    ship.CargoCount = GetReturnCargo(ship.Route, ship.CargoCapacity);
                    ship.HoldCargoKind = Ship.GetRouteCargoKind(ship.Route);
                    ship.State = ShipState.Arriving;
                    ship.X = _pierRightX + 10f; // offscreen right
                    OnShipReturned?.Invoke(ship);
                    OnShipsChanged?.Invoke();
                }
            }
        }

        private void TickCondition()
        {
            foreach (var ship in _ships)
            {
                if (ship.State == ShipState.Voyage)
                    ship.Condition = Mathf.Max(0f, ship.Condition - 0.002f);
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // ANIMATION — departing/arriving ships slide horizontally
        // ═══════════════════════════════════════════════════════════════

        private void AnimateShips(float simDelta)
        {
            float slideSpeed = 3.0f;
            bool anyAnimated = false;
            foreach (var ship in _ships)
            {
                switch (ship.State)
                {
                    case ShipState.Departing:
                        anyAnimated = true;
                        ship.X += slideSpeed * ship.SpeedFactor * simDelta;
                        if (ship.X > _pierRightX + 12f)
                        {
                            ship.State = ShipState.Voyage;
                        }
                        break;

                    case ShipState.Arriving:
                        anyAnimated = true;
                        ship.X -= slideSpeed * ship.SpeedFactor * simDelta;
                        if (ship.X <= _pierRightX)
                        {
                            if (!AssignCraneSlot(ship))
                                AnchorShip(ship);
                        }
                        break;

                    case ShipState.Docking:
                        anyAnimated = true;
                        float dockDist = ship.DockX - ship.X;
                        float dockStep = slideSpeed * ship.SpeedFactor * simDelta;
                        if (Mathf.Abs(dockDist) <= dockStep)
                        {
                            ship.X = ship.DockX;
                            ship.State = ship.CargoCount > 0 ? ShipState.Unloading : ShipState.Idle;
                        }
                        else
                        {
                            ship.X += Mathf.Sign(dockDist) * dockStep;
                        }
                        break;
                }
            }
            if (anyAnimated)
                OnShipsChanged?.Invoke();
        }

        // ═══════════════════════════════════════════════════════════════
        // COMMANDS — called by MatchManager (from scripting builtins)
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Start building a ship of the given width at the first available shipyard.</summary>
        public bool BuildShip(int width)
        {
            width = Mathf.Clamp(width, 1, 5);
            int cost = Ship.GetBuildCost(width);
            if (!_city.SpendWood(cost)) return false;

            // Find a shipyard not already building
            int shipyard = FindFreeShipyard();
            if (shipyard < 0) { _city.AddWood(cost); return false; }

            var ship = new Ship(_nextShipId++, width, shipyard);
            _ships.Add(ship);
            OnShipsChanged?.Invoke();
            return true;
        }

        /// <summary>Launch the next completed ship — navigate to a crane or anchor.</summary>
        public bool LaunchShip()
        {
            foreach (var ship in _ships)
            {
                if (ship.State == ShipState.Launched)
                {
                    if (!AssignCraneSlot(ship))
                        AnchorShip(ship);
                    return true;
                }
            }
            return false;
        }

        /// <summary>Send a docked/idle ship on a trade route.</summary>
        public bool SendTrade(int routeId)
        {
            if (routeId < 0 || routeId > 4) return false;
            var route = (TradeRoute)routeId;

            foreach (var ship in _ships)
            {
                if (ship.State == ShipState.Idle || ship.State == ShipState.Launched)
                {
                    if (ship.CrewCount < 1) continue; // needs at least one crew member

                    ship.TargetCraneSlot = -1; // release crane
                    ship.Route = route;
                    float baseDuration = Ship.GetRouteDuration(route);
                    ship.VoyageDuration = baseDuration / ship.SpeedFactor;
                    ship.VoyageTimer = ship.VoyageDuration;
                    ship.State = ShipState.Departing;
                    OnShipsChanged?.Invoke();
                    return true;
                }
            }
            return false;
        }

        /// <summary>Begin loading cargo onto a docked idle ship.</summary>
        public bool StartLoading()
        {
            foreach (var ship in _ships)
            {
                if (ship.State == ShipState.Idle && ship.CargoCount < ship.CargoCapacity)
                {
                    ship.State = ShipState.Loading;
                    OnShipsChanged?.Invoke();
                    return true;
                }
            }
            return false;
        }

        /// <summary>Repair docked ships at shipyard. Costs 1 wood per repair tick.</summary>
        public bool RepairShip()
        {
            foreach (var ship in _ships)
            {
                if ((ship.State == ShipState.Idle || ship.State == ShipState.Launched)
                    && ship.NeedsRepair)
                {
                    if (!_city.SpendWood(1)) return false;
                    ship.Condition = Mathf.Min(1f, ship.Condition + 0.25f);
                    OnShipsChanged?.Invoke();
                    return true;
                }
            }
            return false;
        }

        /// <summary>Board a crew member onto a docked ship (called by MinionManager).</summary>
        public bool BoardCrew(Ship ship)
        {
            if (ship.CrewCount >= ship.CrewCapacity) return false;
            if (ship.State != ShipState.Idle && ship.State != ShipState.Launched
                && ship.State != ShipState.Loading && ship.State != ShipState.Anchored) return false;
            ship.CrewCount++;
            OnShipsChanged?.Invoke();
            return true;
        }

        /// <summary>Disembark crew when ship docks.</summary>
        public int DisembarkCrew(Ship ship)
        {
            int count = ship.CrewCount;
            ship.CrewCount = 0;
            if (count > 0) OnShipsChanged?.Invoke();
            return count;
        }

        /// <summary>Get the first ship available for crewing (idle/launched/anchored at pier).</summary>
        public Ship GetCrewableShip()
        {
            foreach (var ship in _ships)
            {
                if ((ship.State == ShipState.Idle || ship.State == ShipState.Launched
                     || ship.State == ShipState.Loading || ship.State == ShipState.Anchored)
                    && ship.CrewCount < ship.CrewCapacity)
                    return ship;
            }
            return null;
        }

        // ═══════════════════════════════════════════════════════════════
        // CRANE–MINION INTERFACE — called by MinionManager haulers
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Get a ship at a specific crane that is waiting for minion unloading.</summary>
        public Ship GetUnloadingShipAtCrane(int craneSlot)
        {
            foreach (var s in _ships)
            {
                if (s.TargetCraneSlot == craneSlot
                    && s.State == ShipState.Unloading && s.CargoCount > 0)
                    return s;
            }
            return null;
        }

        /// <summary>Find any crane slot that has a ship needing unloading. Returns -1 if none.</summary>
        public int FindCraneWithUnloadingShip()
        {
            for (int i = 0; i < _city.Width; i++)
            {
                if (_city.GetPierFixture(i) != PierFixture.Crane) continue;
                if (GetUnloadingShipAtCrane(i) != null) return i;
            }
            return -1;
        }

        /// <summary>
        /// Take one cargo unit from a ship (called when a hauler minion arrives at the crane).
        /// Returns true if a unit was taken, false if the ship is empty.
        /// Transitions ship to Idle when hold is empty.
        /// </summary>
        public bool TakeCargoUnit(Ship ship)
        {
            if (ship.CargoCount <= 0) return false;
            ship.CargoCount--;
            TradeIncome++;
            if (ship.CargoCount <= 0)
                ship.State = ShipState.Idle;
            OnShipsChanged?.Invoke();
            return true;
        }

        // ═══════════════════════════════════════════════════════════════
        // DECK MODULE CUSTOMIZATION
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Set/swap a module on a docked ship at a grid position (col, row).
        /// Ship must be Idle or Launched (not at sea or loading).
        /// Costs 1 wood per swap. Returns true on success.
        /// </summary>
        public bool SetShipModule(int shipId, int col, int row, ShipModule module)
        {
            var ship = FindShipById(shipId);
            if (ship == null) return false;
            if (ship.State != ShipState.Idle && ship.State != ShipState.Launched)
                return false;

            if (ship.Grid == null) return false;
            if (col < 0 || col >= ship.Grid.GetLength(0)) return false;
            if (row < 0 || row >= ship.Grid.GetLength(1)) return false;

            // Can't swap if crew currently onboard exceeds new capacity
            var testGrid = (ShipModule[,])ship.Grid.Clone();
            testGrid[col, row] = module;

            int newCrew = 0;
            int cols = testGrid.GetLength(0);
            int rows = testGrid.GetLength(1);
            for (int c = 0; c < cols; c++)
                for (int r = 0; r < rows; r++)
                    newCrew += Ship.GetModuleCrewSlots(testGrid[c, r]);
            newCrew = Mathf.Max(1, newCrew);
            if (ship.CrewCount > newCrew)
                return false;

            if (!_city.SpendWood(1)) return false;

            if (!ship.SetModule(col, row, module))
            {
                _city.AddWood(1);
                return false;
            }

            OnShipsChanged?.Invoke();
            return true;
        }

        /// <summary>Backward-compatible: set module by linear index (col + row * Width).</summary>
        public bool SetShipModule(int shipId, int linearIndex, ShipModule module)
        {
            var ship = FindShipById(shipId);
            if (ship == null || ship.Grid == null) return false;
            int w = ship.Grid.GetLength(0);
            return SetShipModule(shipId, linearIndex % w, linearIndex / w, module);
        }

        /// <summary>Get a ship by ID. Returns null if not found.</summary>
        public Ship FindShipById(int shipId)
        {
            for (int i = 0; i < _ships.Count; i++)
                if (_ships[i].Id == shipId) return _ships[i];
            return null;
        }

        // ═══════════════════════════════════════════════════════════════
        // HELPERS
        // ═══════════════════════════════════════════════════════════════

        private static bool IsDocked(Ship s)
        {
            return s.State == ShipState.Idle || s.State == ShipState.Launched
                || s.State == ShipState.Loading || s.State == ShipState.Unloading
                || s.State == ShipState.Docking || s.State == ShipState.Anchored;
        }

        private static bool IsAtSea(Ship s)
        {
            return s.State == ShipState.Voyage || s.State == ShipState.Departing
                || s.State == ShipState.Arriving;
        }

        private void UpdatePierExtent()
        {
            // Pier extent spans from leftmost Pier cell to rightmost edge
            float leftmost = float.MaxValue;
            float rightmost = 0f;
            bool found = false;

            for (int i = 0; i < _city.Width; i++)
            {
                int origin = _city.GetOwner(i);
                if (origin < 0) continue;
                var type = _city.GetSurface(origin);
                if (type != CellType.Pier) continue;

                found = true;
                float left = origin * CityRenderer.CellSize;
                int bw = _city.GetBuildingWidth(origin);
                float right = (origin + bw) * CityRenderer.CellSize;

                if (left < leftmost) leftmost = left;
                if (right > rightmost) rightmost = right;
            }

            if (!found)
            {
                // No piers — use the city's right edge
                _pierLeftX = _city.Width * CityRenderer.CellSize;
                _pierRightX = _pierLeftX;
            }
            else
            {
                _pierLeftX = leftmost;
                _pierRightX = rightmost;
            }
        }

        /// <summary>
        /// Find a crane fixture not already assigned to a docking/docked ship.
        /// Returns the pier slot index, or -1 if none available.
        /// </summary>
        private int FindFreeCraneSlot()
        {
            for (int i = 0; i < _city.Width; i++)
            {
                if (_city.GetPierFixture(i) != PierFixture.Crane) continue;
                if (IsCraneOccupiedByShip(i)) continue;
                return i;
            }
            return -1;
        }

        /// <summary>Is a crane slot already assigned to a ship?</summary>
        private bool IsCraneOccupiedByShip(int craneSlot)
        {
            foreach (var s in _ships)
            {
                if (s.TargetCraneSlot == craneSlot
                    && (s.State == ShipState.Docking || s.State == ShipState.Loading
                        || s.State == ShipState.Unloading || s.State == ShipState.Idle))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Assign a free crane slot to a ship and set it to Docking state.
        /// Returns true if a crane was found.
        /// </summary>
        private bool AssignCraneSlot(Ship ship)
        {
            int slot = FindFreeCraneSlot();
            if (slot < 0) return false;

            ship.TargetCraneSlot = slot;
            ship.DockX = (slot + 0.5f) * CityRenderer.CellSize;
            ship.State = ShipState.Docking;
            OnShipsChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Park a ship at the anchorage (just past the pier edge) and start the wait timer.
        /// </summary>
        private void AnchorShip(Ship ship)
        {
            ship.State = ShipState.Anchored;
            ship.TargetCraneSlot = -1;
            ship.AnchorTimer = ANCHOR_TIMEOUT;
            float shipW = ship.Width * CityRenderer.CellSize;
            ship.X = _pierRightX + shipW * 0.5f + 0.5f;
            OnShipsChanged?.Invoke();
        }

        /// <summary>Find a loading/unloading ship assigned to the given crane slot.</summary>
        private Ship FindShipAtCrane(int craneSlot)
        {
            foreach (var s in _ships)
            {
                if (s.TargetCraneSlot == craneSlot
                    && (s.State == ShipState.Loading || s.State == ShipState.Unloading))
                    return s;
            }
            return null;
        }

        /// <summary>
        /// Tick anchored ships: retry crane assignment, decrement timer, depart on timeout.
        /// </summary>
        private void TickAnchored()
        {
            for (int i = _ships.Count - 1; i >= 0; i--)
            {
                var ship = _ships[i];
                if (ship.State != ShipState.Anchored) continue;

                // Try to find a free crane
                if (AssignCraneSlot(ship))
                    continue;

                ship.AnchorTimer -= 1f;
                if (ship.AnchorTimer <= 0f)
                {
                    // Give up — sail away
                    ship.State = ShipState.Departing;
                    OnShipsChanged?.Invoke();
                }
            }
        }

        private int FindFreeShipyard()
        {
            for (int i = 0; i < _city.Width; i++)
            {
                int origin = _city.GetOwner(i);
                if (origin != i) continue;
                if (_city.GetSurface(i) != CellType.Shipyard) continue;

                // Check if a ship is already building here
                bool busy = false;
                foreach (var s in _ships)
                {
                    if (s.ShipyardOrigin == origin && s.State == ShipState.Building)
                    { busy = true; break; }
                }
                if (!busy) return origin;
            }
            return -1;
        }

        private static int GetReturnCargo(TradeRoute route, int capacity)
        {
            switch (route)
            {
                case TradeRoute.CoastalFishing:  return Mathf.Min(capacity, 2);
                case TradeRoute.TimberIslands:   return Mathf.Min(capacity, 4);
                case TradeRoute.NearbyVillage:   return Mathf.Min(capacity, capacity / 2 + 1);
                case TradeRoute.DistantEmpire:   return capacity; // full hold
                case TradeRoute.XibalbaCrossing: return capacity; // mysterious bounty
                default:                         return 1;
            }
        }
    }
}
