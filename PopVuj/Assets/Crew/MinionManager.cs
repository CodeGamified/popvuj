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
    /// Movement is simple: leave building → walk left/right → enter next building.
    /// No complex pathfinding — just horizontal movement along the road.
    ///
    /// Traffic: after movement, minions detect nearby walkers and shift lanes (Z)
    /// to avoid overlap. Cart-pullers have a wider footprint and slow down
    /// anyone stuck behind them, causing realistic congestion.
    ///
    /// This is a C# visual layer; it does not need Python representation.
    /// </summary>
    public class MinionManager : MonoBehaviour
    {
        private CityGrid _city;
        private PopVujMatchManager _match;

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
        private const float PROXIMITY_THRESHOLD = 0.14f;  // X distance to count as "overlapping" (slightly larger than a person)
        private const float CART_SLOWDOWN = 0.6f;         // speed multiplier when stuck behind a cart
        // Road boundary cache (updated when grid changes)
        private float _roadMinX;
        private float _roadMaxX;
        // ── Public API ──────────────────────────────────────────

        public IReadOnlyList<Minion> Minions => _minions;

        public void Initialize(CityGrid city, PopVujMatchManager match)
        {
            _city = city;
            _match = match;

            _city.OnGridChanged += RebuildOccupancy;
            _city.OnGridChanged += UpdateRoadBounds;
            _match.OnPopulationChanged += SyncPopulation;
            _match.OnMatchStarted += OnMatchStarted;

            UpdateRoadBounds();
            RebuildOccupancy();
        }

        private void OnDestroy()
        {
            if (_city != null)
            {
                _city.OnGridChanged -= RebuildOccupancy;
                _city.OnGridChanged -= UpdateRoadBounds;
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
            UpdateRoadBounds();
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
            float x = Random.Range(_roadMinX + 0.1f, _roadMaxX - 0.1f);
            _minions.Add(new Minion(_nextId++, x));
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
                        // Leaving a building — pick up cargo if this place produces goods
                        PickupCargoOnExit(m);
                        VacateSlot(m);
                        EnterIdle(m);
                    }
                    break;
            }
        }

        /// <summary>
        /// Evaluate top need, find nearest building with an open slot, start walking.
        /// If no slot available, wander.
        /// </summary>
        private void PickTask(Minion m)
        {
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
                float targetX = GetBuildingCenterX(bestOrigin);
                m.FacingDirection = targetX > m.X ? 1 : -1;
                // Switch to the directional lane for new heading
                m.LaneTarget = Minion.GetDirectionalLane(m.FacingDirection);
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
            m.LaneTarget = Minion.GetDirectionalLane(m.FacingDirection);
            m.TaskTimer = Random.Range(WANDER_DURATION_MIN, WANDER_DURATION_MAX);
        }

        private void MoveMinion(Minion m, float simDelta)
        {
            float step = m.WalkSpeed * simDelta;

            // Wandering — walk in a direction until timer expires or road edge reached
            if (m.TargetBuilding == Minion.WANDERING)
            {
                m.X += m.FacingDirection * step;
                m.X = Mathf.Clamp(m.X, _roadMinX, _roadMaxX);
                m.TaskTimer -= simDelta;
                if (m.TaskTimer <= 0f || m.X <= _roadMinX + 0.01f || m.X >= _roadMaxX - 0.01f)
                    EnterIdle(m);
                return;
            }

            // No target — go idle
            if (m.TargetBuilding < 0)
            {
                EnterIdle(m);
                return;
            }

            // Target building was destroyed mid-walk
            if (!_occupancy.ContainsKey(m.TargetBuilding))
            {
                m.TargetBuilding = Minion.NO_TARGET;
                m.SlotIndex = -1;
                EnterIdle(m);
                return;
            }

            // Walk toward target building
            float targetX = GetBuildingCenterX(m.TargetBuilding);
            float dx = targetX - m.X;
            float dist = Mathf.Abs(dx);

            if (dist <= step)
            {
                // Arrived — drop cargo if this building accepts deliveries, then enter slot
                DropCargoOnEnter(m);
                m.X = targetX;
                m.State = MinionState.InSlot;
                m.TaskTimer = Random.Range(TASK_DURATION_MIN, TASK_DURATION_MAX);
            }
            else
            {
                int oldDir = m.FacingDirection;
                m.X += Mathf.Sign(dx) * step;
                m.X = Mathf.Clamp(m.X, _roadMinX, _roadMaxX);
                m.FacingDirection = dx > 0 ? 1 : -1;
                // If direction changed mid-walk, switch to the correct lane
                if (m.FacingDirection != oldDir)
                    m.LaneTarget = Minion.GetDirectionalLane(m.FacingDirection);
            }
        }

        private void EnterIdle(Minion m)
        {
            m.State = MinionState.Idle;
            m.TaskTimer = Random.Range(IDLE_DELAY_MIN, IDLE_DELAY_MAX);
            m.X = Mathf.Clamp(m.X, _roadMinX, _roadMaxX);
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

                    // Check X proximity — are they close enough to care?
                    float combinedFoot = (a.Footprint + b.Footprint) * 0.5f;
                    float dx = Mathf.Abs(a.X - b.X);
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
                    if (blockerSlow && yielder.State == MinionState.Walking
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
                            yielder.X -= yielder.FacingDirection * excess;
                            yielder.X = Mathf.Clamp(yielder.X, _roadMinX, _roadMaxX);
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

        /// <summary>Is there another walking minion near this X in the candidate lane?</summary>
        private bool LaneOccupiedNear(Minion self, float candidateLane)
        {
            for (int i = 0; i < _minions.Count; i++)
            {
                var other = _minions[i];
                if (other.Id == self.Id) continue;
                if (other.State == MinionState.InSlot) continue;
                if (Mathf.Abs(other.X - self.X) > PROXIMITY_THRESHOLD + self.Footprint) continue;
                if (Mathf.Abs(other.Lane - candidateLane) < Minion.LANE_WIDTH * 0.8f)
                    return true;
            }
            return false;
        }

        /// <summary>Smoothly drift each minion's actual lane toward its target lane.</summary>
        private void DriftLanes(float simDelta)
        {
            float drift = LANE_DRIFT_SPEED * simDelta;
            for (int i = 0; i < _minions.Count; i++)
            {
                var m = _minions[i];
                if (m.State == MinionState.InSlot) continue;
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
                case MinionNeed.Work:   return new[] { CellType.Workshop, CellType.Farm };
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
        // ROAD BOUNDS — minions stay on the road, not in empty wilderness
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Compute the walkable road extent from leftmost building edge
        /// to rightmost building edge. Minions cannot wander past these.
        /// </summary>
        private void UpdateRoadBounds()
        {
            float cs = CityRenderer.CellSize;
            int leftmost = _city.Width;
            int rightmost = -1;

            for (int i = 0; i < _city.Width; i++)
            {
                int origin = _city.GetOwner(i);
                if (origin < 0) continue;
                if (origin < leftmost) leftmost = origin;
                int bw = _city.GetBuildingWidth(origin);
                int end = origin + Mathf.Max(bw, 1);
                if (end > rightmost) rightmost = end;
            }

            if (leftmost >= _city.Width || rightmost <= 0)
            {
                // No buildings — allow the whole strip
                _roadMinX = 0f;
                _roadMaxX = _city.Width * cs;
            }
            else
            {
                _roadMinX = leftmost * cs;
                _roadMaxX = rightmost * cs;
            }
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
