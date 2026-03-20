// Copyright CodeGamified 2025-2026
// MIT License — PopVuj
using System.Collections.Generic;
using UnityEngine;
using CodeGamified.Time;
using PopVuj.Game;

namespace PopVuj.Crew
{
    /// <summary>
    /// Manages all minion instances — spawning, task assignment, movement, slot occupancy.
    ///
    /// Minions are the visual representation of PopVujMatchManager.Population.
    /// The manager syncs minion count to the authoritative population integer and
    /// animates individuals walking between buildings to satisfy needs.
    ///
    /// Movement uses an edge-based walkway graph: nodes connected by edges
    /// (road, bridge, pier). Minions follow planned routes through
    /// the graph, handling turns at junctions automatically.
    ///
    /// Traffic: minions keep right-hand travel and aggressively shift lanes
    /// to overtake slower vehicles (carts, laden haulers).
    ///
    /// This is a C# visual layer; it does not need Python representation.
    /// </summary>
    public class MinionManager : MonoBehaviour
    {
        private CityGrid _city;
        private PopVujMatchManager _match;
        private HarborManager _harbor;

        private readonly List<Minion> _minions = new List<Minion>();
        private int _nextId;

        // Slot occupancy: building origin → occupied flags per slot
        private readonly Dictionary<int, bool[]> _occupancy = new Dictionary<int, bool[]>();

        // Timing
        private float _needTickAccumulator;
        private const float NEED_TICK_INTERVAL = 1f;

        // ── Tuning ──────────────────────────────────────────────

        private const float TASK_DURATION_MIN = 3f;
        private const float TASK_DURATION_MAX = 8f;
        private const float IDLE_DELAY_MIN = 0.3f;
        private const float IDLE_DELAY_MAX = 1.5f;
        private const float WANDER_DURATION_MIN = 2f;
        private const float WANDER_DURATION_MAX = 5f;
        private const float NEED_GROW_RATE = 0.02f;
        private const float NEED_SATISFY_RATE = 0.15f;

        // ── Traffic / lane tuning ───────────────────────────────

        private const float LANE_DRIFT_SPEED = 0.4f;     // how fast minions shift between lanes (Z units/sec)
        private const float BRIDGE_LANE_DRIFT = 2.0f;    // faster drift on bridge for snappy turns
        private const float PROXIMITY_THRESHOLD = 0.28f;  // travel-axis distance for overlap detection
        private const float CART_SLOWDOWN = 0.6f;         // speed multiplier when stuck behind a cart
        private const float WAYPOINT_ARRIVE = 0.06f;      // close enough to a waypoint target

        // ── Walkway graph ───────────────────────────────────────
        private readonly WalkGraph _graph = new WalkGraph();

        // ── Public API ──────────────────────────────────────────

        public IReadOnlyList<Minion> Minions => _minions;
        public WalkGraph Graph => _graph;

        public void Initialize(CityGrid city, PopVujMatchManager match, HarborManager harbor = null)
        {
            _city = city;
            _match = match;
            _harbor = harbor;

            _city.OnGridChanged += RebuildOccupancy;
            _city.OnGridChanged += RebuildGraph;
            _match.OnPopulationChanged += SyncPopulation;
            _match.OnMatchStarted += OnMatchStarted;

            RebuildGraph();
            RebuildOccupancy();
        }

        private void OnDestroy()
        {
            if (_city != null)
            {
                _city.OnGridChanged -= RebuildOccupancy;
                _city.OnGridChanged -= RebuildGraph;
            }
            if (_match != null)
            {
                _match.OnPopulationChanged -= SyncPopulation;
                _match.OnMatchStarted -= OnMatchStarted;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // MATCH LIFECYCLE
        // ═══════════════════════════════════════════════════════════════

        private void OnMatchStarted()
        {
            _minions.Clear();
            _nextId = 0;
            _occupancy.Clear();
            RebuildGraph();
            RebuildOccupancy();
            SyncPopulation(_match.Population);
        }

        /// <summary>
        /// Sync minion count with the authoritative population from MatchManager.
        /// Spawns or despawns minions to match.
        /// </summary>
        private void SyncPopulation(int targetPop)
        {
            while (_minions.Count < targetPop)
                SpawnMinion();
            while (_minions.Count > targetPop && _minions.Count > 0)
                DespawnMinion();
        }

        private void SpawnMinion()
        {
            var edge = _graph.Edges.Count > 0 ? _graph.Edges[0] : null;
            float minP = edge != null ? 0.1f : 0.1f;
            float maxP = edge != null ? edge.Length - 0.1f : _city.Width * CityRenderer.CellSize - 0.1f;
            float p = Random.Range(minP, maxP);
            float x = edge != null ? edge.WorldAt(p).x : p;
            var m = new Minion(_nextId++, x);
            if (edge != null)
            {
                m.CurrentEdge = edge;
                m.EdgeProgress = Mathf.Clamp(p, 0f, edge.Length);
                m.EdgeDirection = m.FacingDirection;
            }
            _minions.Add(m);
        }

        private void DespawnMinion()
        {
            if (_minions.Count == 0) return;
            int idx = _minions.Count - 1;
            VacateSlot(_minions[idx]);
            _minions.RemoveAt(idx);
        }

        // ═══════════════════════════════════════════════════════════════
        // UPDATE LOOP
        // ═══════════════════════════════════════════════════════════════

        private void Update()
        {
            if (_match == null || !_match.MatchInProgress || _match.GameOver) return;

            float timeScale = SimulationTime.Instance?.timeScale ?? 1f;
            if (SimulationTime.Instance != null && SimulationTime.Instance.isPaused) return;

            float simDelta = Time.deltaTime * timeScale;

            // Tick needs periodically
            _needTickAccumulator += simDelta;
            while (_needTickAccumulator >= NEED_TICK_INTERVAL)
            {
                _needTickAccumulator -= NEED_TICK_INTERVAL;
                TickNeeds();
            }

            // Update each minion's state machine
            for (int i = 0; i < _minions.Count; i++)
                UpdateMinion(_minions[i], simDelta);

            // Resolve traffic — lane shifts + congestion slowdowns
            ResolveTraffic(simDelta);

            // Drift lanes toward target
            DriftLanes(simDelta);

            // Sync world X/Z from walkway position
            SyncWorldPositions();
        }

        private void TickNeeds()
        {
            for (int i = 0; i < _minions.Count; i++)
            {
                var m = _minions[i];
                m.Hunger = Mathf.Clamp01(m.Hunger + NEED_GROW_RATE);
                m.Fatigue = Mathf.Clamp01(m.Fatigue + NEED_GROW_RATE);
                m.Faithlessness = Mathf.Clamp01(m.Faithlessness + NEED_GROW_RATE * 0.7f);
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // MINION STATE MACHINE
        // ═══════════════════════════════════════════════════════════════

        private void UpdateMinion(Minion m, float simDelta)
        {
            switch (m.State)
            {
                case MinionState.Idle:
                    m.TaskTimer -= simDelta;
                    if (m.TaskTimer <= 0f)
                        PickTask(m);
                    break;

                case MinionState.Walking:
                    MoveMinion(m, simDelta);
                    break;

                case MinionState.InSlot:
                    m.TaskTimer -= simDelta;
                    SatisfyNeed(m, simDelta);
                    if (m.TaskTimer <= 0f)
                    {
                        // Warehouse crane operators + keeper stay permanently
                        if (m.TargetBuilding >= 0)
                        {
                            var bType = _city.GetSurface(m.TargetBuilding);
                            if (bType == CellType.Warehouse)
                            {
                                int bw = _city.GetBuildingWidth(m.TargetBuilding);
                                var role = BuildingSlots.GetSlotRole(bType, m.SlotIndex, bw);
                                if (role == SlotRole.CraneOperator || role == SlotRole.WarehouseKeeper)
                                {
                                    m.TaskTimer = Random.Range(TASK_DURATION_MIN, TASK_DURATION_MAX);
                                    break;
                                }
                            }
                        }
                        // Leaving a building — pick up cargo if this place produces goods
                        PickupCargoOnExit(m);
                        VacateSlot(m);
                        EnterIdle(m);
                    }
                    break;

                case MinionState.Hauling:
                    MoveHauler(m, simDelta);
                    break;
            }
        }

        /// <summary>
        /// Evaluate top need, find nearest building with an open slot, plan a route.
        /// If no slot available, wander.
        /// </summary>
        private void PickTask(Minion m)
        {
            // Priority 1: hauling cargo from ships to warehouse
            if (TryAssignHaulTask(m)) return;

            // Priority 1.5: staff warehouse (crane operators + keeper)
            if (TryStaffWarehouse(m)) return;

            // Priority 2: normal need-based tasks
            m.CurrentNeed = GetTopNeed(m);
            CellType[] targets = GetTargetTypes(m.CurrentNeed);

            int bestOrigin = -1;
            int bestSlot = -1;
            float bestDist = float.MaxValue;

            for (int i = 0; i < _city.Width; i++)
            {
                int origin = _city.GetOwner(i);
                if (origin != i) continue;

                var type = _city.GetSurface(i);
                if (!MatchesAny(type, targets)) continue;

                int open = FindOpenSlot(origin);
                if (open < 0) continue;

                float dist = Mathf.Abs(m.X - GetBuildingCenterX(origin));
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestOrigin = origin;
                    bestSlot = open;
                }
            }

            if (bestOrigin >= 0)
            {
                m.TargetBuilding = bestOrigin;
                m.SlotIndex = bestSlot;
                OccupySlot(bestOrigin, bestSlot);
                m.State = MinionState.Walking;

                // Plan route through the walkway graph
                float destProg;
                var destEdge = _graph.FindEdgeForBuilding(_city, bestOrigin, out destProg);
                if (m.CurrentEdge != null && destEdge != null
                    && _graph.PlanRoute(m.CurrentEdge, m.EdgeProgress, destEdge, destProg, m.Route))
                {
                    m.RouteIndex = 0;
                    UpdateEdgeDirection(m);
                }
                else
                {
                    m.Route.Clear();
                    EnterIdle(m);
                }
            }
            else
            {
                StartWander(m);
            }
        }

        private void StartWander(Minion m)
        {
            m.CurrentNeed = MinionNeed.Wander;
            m.TargetBuilding = Minion.WANDERING;
            m.SlotIndex = -1;
            m.State = MinionState.Walking;
            m.FacingDirection = Random.value > 0.5f ? 1 : -1;
            m.FacingAngle = m.FacingDirection > 0 ? 90f : -90f;
            m.EdgeDirection = m.FacingDirection;
            m.LaneTarget = Minion.GetDirectionalLane(m.FacingDirection);
            m.TaskTimer = Random.Range(WANDER_DURATION_MIN, WANDER_DURATION_MAX);
            m.Route.Clear();
            m.RouteIndex = 0;
        }

        private void MoveMinion(Minion m, float simDelta)
        {
            float step = m.WalkSpeed * simDelta;

            // ── Wandering ──
            if (m.TargetBuilding == Minion.WANDERING)
            {
                AdvanceOnEdge(m, step);
                m.TaskTimer -= simDelta;
                if (m.TaskTimer <= 0f)
                {
                    EnterIdle(m);
                }
                else if (IsAtEdgeEnd(m))
                {
                    // Bounce — reverse direction instead of piling up at endpoints
                    m.EdgeDirection = -m.EdgeDirection;
                    m.FacingDirection = -m.FacingDirection;
                    UpdateFacingAngle(m);
                    m.LaneTarget = WalkEdge.RightHandLane(m.EdgeDirection);
                }
                return;
            }

            if (m.TargetBuilding < 0) { EnterIdle(m); return; }

            // Target destroyed mid-walk
            if (!_occupancy.ContainsKey(m.TargetBuilding))
            {
                m.TargetBuilding = Minion.NO_TARGET;
                m.SlotIndex = -1;
                m.Route.Clear();
                EnterIdle(m);
                return;
            }

            // No route or past end → enter building
            if (m.Route.Count == 0 || m.RouteIndex >= m.Route.Count)
            {
                DropCargoOnEnter(m);
                m.State = MinionState.InSlot;
                m.TaskTimer = Random.Range(TASK_DURATION_MIN, TASK_DURATION_MAX);
                m.Route.Clear();
                return;
            }

            var rs = m.Route[m.RouteIndex];

            // Ensure we're on the correct edge (first frame or after rebuild)
            if (m.CurrentEdge != rs.Edge)
            {
                m.CurrentEdge = rs.Edge;
                m.EdgeProgress = rs.Edge.Project(m.X, m.RenderZ);
                UpdateEdgeDirection(m);
            }

            float dist = Mathf.Abs(rs.TargetProgress - m.EdgeProgress);
            if (dist <= WAYPOINT_ARRIVE)
            {
                // Snap and advance to next step
                m.EdgeProgress = rs.TargetProgress;
                m.RouteIndex++;

                if (m.RouteIndex < m.Route.Count)
                {
                    var next = m.Route[m.RouteIndex];
                    if (m.CurrentEdge != next.Edge)
                    {
                        // Determine which node we arrived at
                        bool atA = rs.TargetProgress < 0.01f;
                        WalkNode arrived = atA ? rs.Edge.A : rs.Edge.B;
                        m.CurrentEdge = next.Edge;
                        m.EdgeProgress = next.Edge.ProgressAt(arrived);
                    }
                    UpdateEdgeDirection(m);
                }
                else
                {
                    DropCargoOnEnter(m);
                    m.State = MinionState.InSlot;
                    m.TaskTimer = Random.Range(TASK_DURATION_MIN, TASK_DURATION_MAX);
                    m.Route.Clear();
                }
            }
            else
            {
                AdvanceOnEdge(m, step);
            }
        }

        private static void AdvanceOnEdge(Minion m, float step)
        {
            if (m.CurrentEdge == null) return;
            m.EdgeProgress += m.EdgeDirection * step;
            m.EdgeProgress = Mathf.Clamp(m.EdgeProgress, 0f, m.CurrentEdge.Length);
        }

        private static bool IsAtEdgeEnd(Minion m)
        {
            if (m.CurrentEdge == null) return true;
            return m.EdgeProgress <= 0.01f || m.EdgeProgress >= m.CurrentEdge.Length - 0.01f;
        }

        /// <summary>
        /// Set EdgeDirection and FacingDirection based on current route step.
        /// Also updates lane target for right-hand traffic.
        /// </summary>
        private void UpdateEdgeDirection(Minion m)
        {
            if (m.RouteIndex >= m.Route.Count) return;
            var rs = m.Route[m.RouteIndex];
            float diff = rs.TargetProgress - m.EdgeProgress;
            m.EdgeDirection = diff >= 0 ? 1 : -1;

            int facing = m.CurrentEdge.GetFacing(m.EdgeDirection);
            if (facing != 0) m.FacingDirection = facing;

            UpdateFacingAngle(m);
            m.LaneTarget = WalkEdge.RightHandLane(m.EdgeDirection);
        }

        /// <summary>
        /// Compute FacingAngle from the minion's current edge + edge direction.
        /// Y-rotation: 90 = +X (right), -90 = -X (left), 0 = +Z (away), 180 = -Z (toward camera).
        /// </summary>
        private static void UpdateFacingAngle(Minion m)
        {
            if (m.CurrentEdge == null) return;
            var dir = m.CurrentEdge.Dir * m.EdgeDirection;
            m.FacingAngle = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
        }

        private void EnterIdle(Minion m)
        {
            m.State = MinionState.Idle;
            m.TaskTimer = Random.Range(IDLE_DELAY_MIN, IDLE_DELAY_MAX);
            m.Route.Clear();
            m.RouteIndex = 0;
            if (m.CurrentEdge != null)
                m.EdgeProgress = Mathf.Clamp(m.EdgeProgress, 0f, m.CurrentEdge.Length);
        }

        /// <summary>
        /// While in a building slot, reduce the need that this building satisfies.
        /// </summary>
        private void SatisfyNeed(Minion m, float simDelta)
        {
            if (m.TargetBuilding < 0) return;
            var type = _city.GetSurface(m.TargetBuilding);
            float rate = NEED_SATISFY_RATE * simDelta;

            switch (type)
            {
                case CellType.House:
                    m.Fatigue = Mathf.Clamp01(m.Fatigue - rate);
                    break;
                case CellType.Farm:
                case CellType.Market:
                    m.Hunger = Mathf.Clamp01(m.Hunger - rate);
                    break;
                case CellType.Chapel:
                    m.Faithlessness = Mathf.Clamp01(m.Faithlessness - rate);
                    break;
                // Harbor buildings satisfy work need (fatigue stays, but they're productive)
                case CellType.Shipyard:
                case CellType.Pier:
                    // No personal need reduction — harbor work is purely economic
                    break;
                case CellType.Warehouse:
                    // Warehouse staff get slow need reduction so they stay healthy on shift
                    m.Fatigue = Mathf.Clamp01(m.Fatigue - rate * 0.5f);
                    m.Hunger = Mathf.Clamp01(m.Hunger - rate * 0.3f);
                    break;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // CARGO — pickup on exit, drop on enter
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// When a minion finishes working in a production building, they
        /// pick up the appropriate cargo to haul to the next destination.
        ///
        /// Not every exit produces cargo — only production roles:
        ///   Workshop worker → Crate
        ///   Farm farmer     → Grain
        ///   Fountain keeper → Water
        ///   Tree harvester  → Log (handled externally via PickupCargo)
        ///
        /// Worship, rest, and market visits don't produce output.
        /// If already carrying something, don't double-stack.
        /// </summary>
        private void PickupCargoOnExit(Minion m)
        {
            if (!m.Cargo.IsEmpty) return;
            if (m.TargetBuilding < 0) return;

            var type = _city.GetSurface(m.TargetBuilding);
            switch (type)
            {
                case CellType.Workshop:
                    m.PickupCargo(CargoKind.Crate);
                    break;
                case CellType.Farm:
                    // Workers produce grain; eaters don't
                    if (m.CurrentNeed == MinionNeed.Work)
                        m.PickupCargo(CargoKind.Grain);
                    break;
                case CellType.Fountain:
                    m.PickupCargo(CargoKind.Water);
                    break;
                // Shipyard workers produce planks (from wood)
                case CellType.Shipyard:
                    m.PickupCargo(CargoKind.Plank);
                    break;
                // Pier dockers carry trade crates
                case CellType.Pier:
                    m.PickupCargo(CargoKind.TradeCrate);
                    break;
            }
        }

        /// <summary>
        /// When a minion arrives at a building, they drop off whatever
        /// they're carrying — the building receives the goods.
        ///
        /// Any building accepts deliveries (houses store food, chapels
        /// receive offerings, markets stock crates). The cargo simply
        /// disappears into the building's economy.
        /// </summary>
        private static void DropCargoOnEnter(Minion m)
        {
            if (m.Cargo.IsEmpty) return;
            m.DropCargo();
        }

        // ═══════════════════════════════════════════════════════════════
        // HAULING — minions shuttle cargo between crane and warehouse
        // ═══════════════════════════════════════════════════════════════

        private const int MAX_ACTIVE_HAULERS = 6;

        private int CountActiveHaulers()
        {
            int count = 0;
            for (int i = 0; i < _minions.Count; i++)
                if (_minions[i].State == MinionState.Hauling) count++;
            return count;
        }

        /// <summary>Find the first Warehouse building origin, or -1.</summary>
        private int FindWarehouseOrigin()
        {
            for (int i = 0; i < _city.Width; i++)
            {
                int origin = _city.GetOwner(i);
                if (origin != i) continue;
                if (_city.GetSurface(i) == CellType.Warehouse) return origin;
            }
            return -1;
        }

        /// <summary>
        /// Fill unstaffed warehouse crane-operator and keeper slots.
        /// These roles stay permanently once assigned.
        /// </summary>
        private bool TryStaffWarehouse(Minion m)
        {
            int warehouse = FindWarehouseOrigin();
            if (warehouse < 0) return false;
            if (!_occupancy.TryGetValue(warehouse, out var slots)) return false;

            int bw = _city.GetBuildingWidth(warehouse);
            int staffSlots = bw + 1; // crane operators (0..bw-1) + keeper (bw)

            for (int s = 0; s < staffSlots && s < slots.Length; s++)
            {
                if (slots[s]) continue; // already occupied

                m.TargetBuilding = warehouse;
                m.SlotIndex = s;
                OccupySlot(warehouse, s);
                m.State = MinionState.Walking;
                m.CurrentNeed = MinionNeed.Work;

                float destProg;
                var destEdge = _graph.FindEdgeForBuilding(_city, warehouse, out destProg);
                if (m.CurrentEdge != null && destEdge != null
                    && _graph.PlanRoute(m.CurrentEdge, m.EdgeProgress, destEdge, destProg, m.Route))
                {
                    m.RouteIndex = 0;
                    UpdateEdgeDirection(m);
                    return true;
                }

                // Can't route — cancel
                VacateSlot(m);
                break;
            }
            return false;
        }

        /// <summary>
        /// Attempt to assign a haul task: carry cargo from a ship's crane to the warehouse.
        /// Returns true if a task was assigned.
        /// </summary>
        private bool TryAssignHaulTask(Minion m)
        {
            if (_harbor == null) return false;
            if (CountActiveHaulers() >= MAX_ACTIVE_HAULERS) return false;

            int warehouse = FindWarehouseOrigin();
            if (warehouse < 0) return false;

            int craneSlot = _harbor.FindCraneWithUnloadingShip();
            if (craneSlot < 0) return false;

            // Assign haul: crane → warehouse
            m.HaulSource = craneSlot;
            m.HaulDestination = warehouse;
            m.HaulPhase = HaulPhase.GoingToSource;
            m.State = MinionState.Hauling;
            m.CurrentNeed = MinionNeed.Haul;
            m.TargetBuilding = Minion.NO_TARGET;
            m.SlotIndex = -1;

            // Plan route to crane
            float sourceProg;
            var sourceEdge = _graph.FindEdgeForCell(_city, craneSlot, out sourceProg);
            if (m.CurrentEdge != null && sourceEdge != null
                && _graph.PlanRoute(m.CurrentEdge, m.EdgeProgress, sourceEdge, sourceProg, m.Route))
            {
                m.RouteIndex = 0;
                UpdateEdgeDirection(m);
                return true;
            }

            // Can't route — cancel
            m.State = MinionState.Idle;
            m.Route.Clear();
            return false;
        }

        /// <summary>
        /// Hauling movement — two-phase: walk to crane, pick up, walk to warehouse, deposit.
        /// Uses the same route-following as MoveMinion but with haul phase transitions.
        /// </summary>
        private void MoveHauler(Minion m, float simDelta)
        {
            float step = m.WalkSpeed * simDelta;

            // Check if we've arrived at current destination (route exhausted)
            if (m.Route.Count == 0 || m.RouteIndex >= m.Route.Count)
            {
                if (m.HaulPhase == HaulPhase.GoingToSource)
                {
                    // Arrived at crane — pick up cargo from ship
                    bool pickedUp = false;
                    if (_harbor != null)
                    {
                        var ship = _harbor.GetUnloadingShipAtCrane(m.HaulSource);
                        if (ship != null && _harbor.TakeCargoUnit(ship))
                        {
                            m.PickupCargo(ship.HoldCargoKind != CargoKind.None
                                ? ship.HoldCargoKind
                                : Ship.GetRouteCargoKind(ship.Route));
                            pickedUp = true;
                        }
                    }

                    if (!pickedUp)
                    {
                        // No cargo available — go idle
                        EnterIdle(m);
                        return;
                    }

                    // Now route to warehouse
                    m.HaulPhase = HaulPhase.GoingToDestination;
                    float destProg;
                    var destEdge = _graph.FindEdgeForBuilding(_city, m.HaulDestination, out destProg);
                    if (m.CurrentEdge != null && destEdge != null
                        && _graph.PlanRoute(m.CurrentEdge, m.EdgeProgress, destEdge, destProg, m.Route))
                    {
                        m.RouteIndex = 0;
                        UpdateEdgeDirection(m);
                    }
                    else
                    {
                        DepositCargo(m);
                        EnterIdle(m);
                    }
                }
                else // GoingToDestination — arrived at warehouse
                {
                    DepositCargo(m);
                    EnterIdle(m);
                }
                return;
            }

            // Follow route steps (same logic as MoveMinion)
            var rs = m.Route[m.RouteIndex];

            if (m.CurrentEdge != rs.Edge)
            {
                m.CurrentEdge = rs.Edge;
                m.EdgeProgress = rs.Edge.Project(m.X, m.RenderZ);
                UpdateEdgeDirection(m);
            }

            float dist = Mathf.Abs(rs.TargetProgress - m.EdgeProgress);
            if (dist <= WAYPOINT_ARRIVE)
            {
                m.EdgeProgress = rs.TargetProgress;
                m.RouteIndex++;

                if (m.RouteIndex < m.Route.Count)
                {
                    var next = m.Route[m.RouteIndex];
                    if (m.CurrentEdge != next.Edge)
                    {
                        bool atA = rs.TargetProgress < 0.01f;
                        WalkNode arrived = atA ? rs.Edge.A : rs.Edge.B;
                        m.CurrentEdge = next.Edge;
                        m.EdgeProgress = next.Edge.ProgressAt(arrived);
                    }
                    UpdateEdgeDirection(m);
                }
            }
            else
            {
                AdvanceOnEdge(m, step);
            }
        }

        /// <summary>Deposit carried cargo into city resources (at warehouse).</summary>
        private void DepositCargo(Minion m)
        {
            if (m.Cargo.IsEmpty) return;
            var kind = m.DropCargo();
            switch (kind)
            {
                case CargoKind.Log:
                case CargoKind.Plank:
                    _city.AddWood(1);
                    break;
                case CargoKind.Stone:
                    _city.AddStone(1);
                    break;
                case CargoKind.Grain:
                case CargoKind.Fish:
                case CargoKind.Water:
                    _city.AddFood(1);
                    break;
                default: // Crate, TradeCrate, ExoticGoods, Rope, etc.
                    _city.AddGoods(1);
                    break;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // TRAFFIC — lane avoidance & congestion
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// For every walking/idle minion, check if any other road-minion is
        /// close enough to overlap. When two minions share the same lane and
        /// are within footprint range, one shifts to an adjacent lane.
        ///
        /// Cart-pullers never dodge — everyone else moves around them.
        /// If a minion is walking in the same direction behind a cart and
        /// can't shift lanes (all occupied), it slows to the cart's speed.
        /// </summary>
        private void ResolveTraffic(float simDelta)
        {
            for (int i = 0; i < _minions.Count; i++)
            {
                var a = _minions[i];
                if (a.State == MinionState.InSlot) continue;

                bool aSlowed = false;

                for (int j = i + 1; j < _minions.Count; j++)
                {
                    var b = _minions[j];
                    if (b.State == MinionState.InSlot) continue;

                    // Only compare minions on the same edge
                    if (a.CurrentEdge != b.CurrentEdge) continue;

                    // Check travel-axis proximity — are they close enough to care?
                    float combinedFoot = (a.Footprint + b.Footprint) * 0.5f;
                    float dx = Mathf.Abs(a.EdgeProgress - b.EdgeProgress);
                    if (dx > combinedFoot + PROXIMITY_THRESHOLD) continue;

                    // Check lane proximity — are they in roughly the same lane?
                    float dz = Mathf.Abs(a.Lane - b.Lane);
                    if (dz > Minion.LANE_WIDTH * 1.2f) continue;

                    // Overlap detected — decide who dodges.
                    // Laden minions and carts don't dodge — lighter ones yield.
                    // Tie-break: higher Id yields.
                    Minion yielder, blocker;
                    bool aHeavy = a.HasCart || !a.Cargo.IsEmpty;
                    bool bHeavy = b.HasCart || !b.Cargo.IsEmpty;
                    if (aHeavy && !bHeavy)             { yielder = b; blocker = a; }
                    else if (bHeavy && !aHeavy)        { yielder = a; blocker = b; }
                    else if (a.Id > b.Id)              { yielder = a; blocker = b; }
                    else                               { yielder = b; blocker = a; }

                    // Pick a free lane direction
                    float shift = PickLaneShift(yielder, blocker);
                    yielder.LaneTarget = ClampLane(yielder.Lane + shift);

                    // Congestion slowdown — walking same direction behind a slow hauler
                    bool blockerSlow = blocker.HasCart || !blocker.Cargo.IsEmpty;
                    if (blockerSlow && (yielder.State == MinionState.Walking || yielder.State == MinionState.Hauling)
                        && yielder.FacingDirection == blocker.FacingDirection
                        && !CanShiftLane(yielder, blocker))
                    {
                        // Stuck behind — can't pass, match their speed this frame
                        float blockerStep = blocker.WalkSpeed * simDelta;
                        float selfStep = yielder.WalkSpeed * simDelta;
                        if (selfStep > blockerStep)
                        {
                            // Nudge back so we don't overtake through the hauler
                            float excess = (selfStep - blockerStep) * CART_SLOWDOWN;
                            yielder.EdgeProgress -= yielder.EdgeDirection * excess;
                            if (yielder.CurrentEdge != null)
                                yielder.EdgeProgress = Mathf.Clamp(yielder.EdgeProgress,
                                    0f, yielder.CurrentEdge.Length);
                        }
                        if (yielder == a) aSlowed = true;
                    }
                }

                // Idle minions that aren't near anyone drift toward their directional lane
                if (a.State == MinionState.Idle && !aSlowed)
                {
                    float homeLane = a.FacingDirection > 0
                        ? Minion.RIGHT_LANE_OFFSET
                        : Minion.LEFT_LANE_OFFSET;
                    a.LaneTarget = homeLane;
                }
            }
        }

        /// <summary>
        /// Pick which direction to shift: away from the blocker's lane.
        /// Prefers shifting within own side of the road.
        /// </summary>
        private static float PickLaneShift(Minion yielder, Minion blocker)
        {
            float laneW = Minion.LANE_WIDTH;
            float max = Minion.LEFT_LANE_OFFSET + Minion.MAX_LANES * laneW;
            float min = Minion.RIGHT_LANE_OFFSET - Minion.MAX_LANES * laneW;
            // Prefer shifting away from the blocker
            float away = yielder.Lane > blocker.Lane ? laneW : -laneW;
            // But if that pushes us off the road, go the other way
            float candidate = yielder.Lane + away;
            if (candidate > max || candidate < min)
                away = -away;
            return away;
        }

        /// <summary>Can the yielder actually shift to an adjacent lane that's clear?</summary>
        private bool CanShiftLane(Minion yielder, Minion blocker)
        {
            float laneW = Minion.LANE_WIDTH;
            float tryUp = yielder.Lane + laneW;
            float tryDown = yielder.Lane - laneW;
            float max = Minion.LEFT_LANE_OFFSET + Minion.MAX_LANES * laneW;
            float min = Minion.RIGHT_LANE_OFFSET - Minion.MAX_LANES * laneW;

            bool upClear = tryUp <= max && !LaneOccupiedNear(yielder, tryUp);
            bool downClear = tryDown >= min && !LaneOccupiedNear(yielder, tryDown);
            return upClear || downClear;
        }

        /// <summary>Is there another walking minion near this one in the candidate lane?</summary>
        private bool LaneOccupiedNear(Minion self, float candidateLane)
        {
            for (int i = 0; i < _minions.Count; i++)
            {
                var other = _minions[i];
                if (other.Id == self.Id) continue;
                if (other.State == MinionState.InSlot) continue;
                if (other.CurrentEdge != self.CurrentEdge) continue;
                if (Mathf.Abs(other.EdgeProgress - self.EdgeProgress) > PROXIMITY_THRESHOLD + self.Footprint) continue;
                if (Mathf.Abs(other.Lane - candidateLane) < Minion.LANE_WIDTH * 0.8f)
                    return true;
            }
            return false;
        }

        /// <summary>Smoothly drift each minion's actual lane toward its target lane.</summary>
        private void DriftLanes(float simDelta)
        {
            for (int i = 0; i < _minions.Count; i++)
            {
                var m = _minions[i];
                if (m.State == MinionState.InSlot) continue;
                // Use fast drift on the bridge for snappy turns
                bool onBridge = m.CurrentEdge != null && m.CurrentEdge.IsDepthEdge;
                float speed = onBridge ? BRIDGE_LANE_DRIFT : LANE_DRIFT_SPEED;
                float drift = speed * simDelta;
                float diff = m.LaneTarget - m.Lane;
                if (Mathf.Abs(diff) < 0.001f)
                {
                    m.Lane = m.LaneTarget;
                }
                else
                {
                    m.Lane += Mathf.Sign(diff) * Mathf.Min(drift, Mathf.Abs(diff));
                }
            }
        }

        private static float ClampLane(float lane)
        {
            // Clamp to the full road depth range: right lane minus max offset to left lane plus max offset
            float max = Minion.LEFT_LANE_OFFSET + Minion.MAX_LANES * Minion.LANE_WIDTH;
            float min = Minion.RIGHT_LANE_OFFSET - Minion.MAX_LANES * Minion.LANE_WIDTH;
            return Mathf.Clamp(lane, min, max);
        }

        /// <summary>
        /// After all movement and lane drifting, write the walkway-graph position
        /// back to the minion's world X and Lane (Z) for rendering.
        /// </summary>
        private void SyncWorldPositions()
        {
            for (int i = 0; i < _minions.Count; i++)
            {
                var m = _minions[i];
                if (m.State == MinionState.InSlot) continue;
                if (m.CurrentEdge == null) continue;

                var pos = m.CurrentEdge.WorldAt(m.EdgeProgress);
                m.X = pos.x + m.CurrentEdge.Perp.x * m.Lane;
                m.RenderZ = pos.y + m.CurrentEdge.Perp.y * m.Lane;
            }
        }

        /// <summary>
        /// Rebuild the walkway graph from the current city layout.
        /// Re-place any minions whose edges disappeared.
        /// </summary>
        private void RebuildGraph()
        {
            _graph.Build(_city);

            for (int i = 0; i < _minions.Count; i++)
            {
                var m = _minions[i];

                // InSlot: remap to the edge for their building
                if (m.State == MinionState.InSlot)
                {
                    if (m.TargetBuilding >= 0)
                    {
                        float prog;
                        m.CurrentEdge = _graph.FindEdgeForBuilding(_city, m.TargetBuilding, out prog);
                    }
                    if (m.CurrentEdge == null && _graph.Edges.Count > 0)
                        m.CurrentEdge = _graph.Edges[0];
                    continue;
                }

                // Walking/idle: find nearest edge
                float progress;
                var edge = _graph.FindNearestEdge(m.X, m.RenderZ, out progress);
                if (edge == null && _graph.Edges.Count > 0)
                { edge = _graph.Edges[0]; progress = 0f; }
                if (edge == null) continue;

                m.CurrentEdge = edge;
                m.EdgeProgress = progress;

                // Re-plan route if walking to a building
                if (m.State == MinionState.Walking && m.TargetBuilding >= 0
                    && m.TargetBuilding != Minion.WANDERING)
                {
                    float destProg;
                    var destEdge = _graph.FindEdgeForBuilding(_city, m.TargetBuilding, out destProg);
                    if (destEdge != null
                        && _graph.PlanRoute(m.CurrentEdge, m.EdgeProgress, destEdge, destProg, m.Route))
                    {
                        m.RouteIndex = 0;
                        UpdateEdgeDirection(m);
                    }
                }

                // Re-plan route if hauling cargo
                if (m.State == MinionState.Hauling)
                {
                    float destProg;
                    WalkEdge destEdge;
                    if (m.HaulPhase == HaulPhase.GoingToSource)
                        destEdge = _graph.FindEdgeForCell(_city, m.HaulSource, out destProg);
                    else
                        destEdge = _graph.FindEdgeForBuilding(_city, m.HaulDestination, out destProg);
                    if (destEdge != null
                        && _graph.PlanRoute(m.CurrentEdge, m.EdgeProgress, destEdge, destProg, m.Route))
                    {
                        m.RouteIndex = 0;
                        UpdateEdgeDirection(m);
                    }
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // NEED EVALUATION
        // ═══════════════════════════════════════════════════════════════

        private static MinionNeed GetTopNeed(Minion m)
        {
            // Urgent thresholds first
            if (m.Hunger > 0.7f) return MinionNeed.Hunger;
            if (m.Fatigue > 0.7f) return MinionNeed.Rest;
            if (m.Faithlessness > 0.6f) return MinionNeed.Faith;

            // Relative comparison
            if (m.Hunger > m.Fatigue && m.Hunger > m.Faithlessness) return MinionNeed.Hunger;
            if (m.Fatigue > m.Faithlessness) return MinionNeed.Rest;
            if (m.Faithlessness > 0.3f) return MinionNeed.Faith;

            // Default: contribute labor
            return MinionNeed.Work;
        }

        private static CellType[] GetTargetTypes(MinionNeed need)
        {
            switch (need)
            {
                case MinionNeed.Rest:   return new[] { CellType.House };
                case MinionNeed.Hunger: return new[] { CellType.Farm, CellType.Market };
                case MinionNeed.Faith:  return new[] { CellType.Chapel };
                case MinionNeed.Work:   return new[] { CellType.Workshop, CellType.Farm,
                                                       CellType.Shipyard, CellType.Pier, CellType.Warehouse };
                default:                return System.Array.Empty<CellType>();
            }
        }

        private static bool MatchesAny(CellType type, CellType[] targets)
        {
            for (int i = 0; i < targets.Length; i++)
                if (targets[i] == type) return true;
            return false;
        }

        // ═══════════════════════════════════════════════════════════════
        // SLOT OCCUPANCY
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Rebuild the occupancy map from the current city grid.
        /// Preserves existing occupancy for unchanged buildings.
        /// Evicts minions from destroyed or shrunk buildings.
        /// </summary>
        private void RebuildOccupancy()
        {
            var activeOrigins = new HashSet<int>();

            for (int i = 0; i < _city.Width; i++)
            {
                int origin = _city.GetOwner(i);
                if (origin != i) continue;

                var type = _city.GetSurface(i);
                if (type == CellType.Empty || type == CellType.Tree) continue;

                int bw = _city.GetBuildingWidth(i);
                int slots = BuildingSlots.GetSlotCount(type, bw);
                activeOrigins.Add(origin);

                if (_occupancy.TryGetValue(origin, out var arr))
                {
                    if (arr.Length != slots)
                    {
                        // Size changed — preserve what fits, evict overflow
                        var newArr = new bool[slots];
                        int copyLen = Mathf.Min(arr.Length, slots);
                        System.Array.Copy(arr, newArr, copyLen);
                        _occupancy[origin] = newArr;

                        if (slots < arr.Length)
                            EvictSlotsAbove(origin, slots);
                    }
                }
                else
                {
                    _occupancy[origin] = new bool[slots];
                }
            }

            // Remove occupancy for destroyed buildings
            var toRemove = new List<int>();
            foreach (var key in _occupancy.Keys)
                if (!activeOrigins.Contains(key))
                    toRemove.Add(key);
            foreach (var key in toRemove)
                _occupancy.Remove(key);

            // Evict minions whose buildings were destroyed
            for (int i = 0; i < _minions.Count; i++)
            {
                var m = _minions[i];
                if (m.TargetBuilding >= 0 && !_occupancy.ContainsKey(m.TargetBuilding))
                {
                    m.TargetBuilding = Minion.NO_TARGET;
                    m.SlotIndex = -1;
                    if (m.State == MinionState.InSlot || m.State == MinionState.Walking)
                        EnterIdle(m);
                }
            }
        }

        private void EvictSlotsAbove(int origin, int maxSlot)
        {
            for (int i = 0; i < _minions.Count; i++)
            {
                var m = _minions[i];
                if (m.TargetBuilding == origin && m.SlotIndex >= maxSlot)
                {
                    m.SlotIndex = -1;
                    m.TargetBuilding = Minion.NO_TARGET;
                    EnterIdle(m);
                }
            }
        }

        private int FindOpenSlot(int buildingOrigin)
        {
            if (!_occupancy.TryGetValue(buildingOrigin, out var slots)) return -1;
            for (int i = 0; i < slots.Length; i++)
                if (!slots[i]) return i;
            return -1;
        }

        private void OccupySlot(int buildingOrigin, int slotIndex)
        {
            if (_occupancy.TryGetValue(buildingOrigin, out var slots) && slotIndex < slots.Length)
                slots[slotIndex] = true;
        }

        private void VacateSlot(Minion m)
        {
            if (m.TargetBuilding >= 0 && m.SlotIndex >= 0 &&
                _occupancy.TryGetValue(m.TargetBuilding, out var slots) &&
                m.SlotIndex < slots.Length)
            {
                slots[m.SlotIndex] = false;
            }
            m.TargetBuilding = Minion.NO_TARGET;
            m.SlotIndex = -1;
        }

        private float GetBuildingCenterX(int origin)
        {
            int bw = _city.GetBuildingWidth(origin);
            if (bw < 1) bw = 1;
            return (origin + bw * 0.5f) * CityRenderer.CellSize;
        }
    }
}
