// Copyright CodeGamified 2025-2026
// MIT License — PopVuj
using System.Collections.Generic;
using UnityEngine;
using PopVuj.Game;

namespace PopVuj.Crew
{
    /// <summary>
    /// Renders minions as small cubes on the city road.
    ///
    /// Walking/idle minions appear on the road surface (foreground Z).
    /// Working minions appear elevated inside their building, colored by role.
    /// Slot positions are distributed evenly across the building's width.
    ///
    /// Cargo volumes are rendered as a second cube attached to each minion,
    /// sized and colored per CargoKind. Hidden when minion carries nothing.
    /// </summary>
    public class MinionRenderer : MonoBehaviour
    {
        private MinionManager _manager;
        private CityGrid _city;

        private readonly List<GameObject> _pool = new List<GameObject>();
        private readonly List<GameObject> _cargoPool = new List<GameObject>();
        private GameObject _parent;
        private GameObject _cargoParent;

        // ── Minion dimensions ───────────────────────────────────

        private const float MinionW = 0.16f;
        private const float MinionH = 0.28f;
        private const float CartW   = 0.36f;           // wider body for cart-pullers
        private const float CartH   = 0.24f;           // slightly shorter (hunched under load)
        private const float CartDepth = 0.24f;          // thicker Z for the cart body
        private const float RoadY = 0.3f;              // matches CityRenderer RoadH
        private const float MinionZ = 0.5f;            // rendering Z base (SyncWorldPositions compensates)
        private const float InBuildingZ = 0.7f;        // slightly in front of building face

        // ── Color palette ───────────────────────────────────────

        private static readonly Color IdleColor = new Color(0.85f, 0.65f, 0.45f);
        private static readonly Color WalkColor = new Color(0.80f, 0.60f, 0.40f);
        private static readonly Color CartIdleColor = new Color(0.55f, 0.40f, 0.20f);   // darker wood-brown for cart
        private static readonly Color CartWalkColor = new Color(0.50f, 0.35f, 0.18f);
        private static readonly Color WorkColor = new Color(0.50f, 0.60f, 0.70f);
        private static readonly Color PrayColor = new Color(0.90f, 0.85f, 0.50f);
        private static readonly Color RestColor = new Color(0.55f, 0.50f, 0.65f);
        private static readonly Color EatColor  = new Color(0.60f, 0.75f, 0.45f);
        private static readonly Color HarborColor = new Color(0.40f, 0.55f, 0.65f);
        private static readonly Color SailorColor = new Color(0.35f, 0.45f, 0.60f);

        public void Initialize(MinionManager manager, CityGrid city)
        {
            _manager = manager;
            _city = city;
            _parent = new GameObject("Minions");
            _parent.transform.SetParent(transform, false);
            _cargoParent = new GameObject("Cargo");
            _cargoParent.transform.SetParent(transform, false);
        }

        private void LateUpdate()
        {
            if (_manager == null) return;
            var minions = _manager.Minions;

            // Grow pools on demand
            while (_pool.Count < minions.Count)
                _pool.Add(CreateMinionCube(_pool.Count));
            while (_cargoPool.Count < minions.Count)
                _cargoPool.Add(CreateCargoCube(_cargoPool.Count));

            // Hide excess pool objects
            for (int i = minions.Count; i < _pool.Count; i++)
                _pool[i].SetActive(false);
            for (int i = minions.Count; i < _cargoPool.Count; i++)
                _cargoPool[i].SetActive(false);

            // Position each active minion + cargo
            for (int i = 0; i < minions.Count; i++)
            {
                var m = minions[i];
                var go = _pool[i];
                var cargoGO = _cargoPool[i];
                go.SetActive(true);

                float x, y, z, w, h, d;
                Color col;

                if (m.State == MinionState.InSlot && m.TargetBuilding >= 0)
                {
                    // Inside building — positioned at slot-specific furniture
                    x = GetSlotWorldX(m.TargetBuilding, m.SlotIndex);
                    y = GetBuildingSlotY(m.TargetBuilding, m.SlotIndex);
                    z = InBuildingZ;
                    col = GetInBuildingColor(m.TargetBuilding);
                    w = MinionW; h = MinionH; d = MinionW;
                    // No cargo visible inside buildings
                    cargoGO.SetActive(false);
                }
                else
                {
                    // Walking or idle — on the road surface
                    x = m.X;
                    y = RoadY + (m.HasCart ? CartH : MinionH) * 0.5f;
                    z = m.RenderZ;

                    if (m.HasCart)
                    {
                        col = m.State == MinionState.Walking ? CartWalkColor : CartIdleColor;
                        w = CartW; h = CartH; d = CartDepth;
                    }
                    else
                    {
                        col = m.State == MinionState.Walking ? WalkColor : IdleColor;
                        w = MinionW; h = MinionH; d = MinionW;
                    }

                    // Render cargo volume if carrying something
                    if (!m.Cargo.IsEmpty)
                    {
                        cargoGO.SetActive(true);
                        float facingRad = m.FacingAngle * Mathf.Deg2Rad;
                        float cargoFwd = w * 0.5f + m.Cargo.VisualWidth * 0.3f;
                        float cargoX = x + Mathf.Sin(facingRad) * cargoFwd;
                        float cargoY = y + m.Cargo.CarryOffsetY;
                        float cargoZ = z + Mathf.Cos(facingRad) * cargoFwd;
                        cargoGO.transform.localPosition = new Vector3(cargoX, cargoY, cargoZ);
                        cargoGO.transform.localScale = new Vector3(
                            m.Cargo.VisualWidth, m.Cargo.VisualHeight, m.Cargo.VisualDepth);
                        cargoGO.transform.localRotation = Quaternion.Euler(0f, m.FacingAngle, 0f);
                        SetColor(cargoGO, Cargo.GetColor(m.Cargo.Kind));
                    }
                    else
                    {
                        cargoGO.SetActive(false);
                    }
                }

                go.transform.localPosition = new Vector3(x, y, z);
                go.transform.localScale = new Vector3(w, h, d);
                go.transform.localRotation = Quaternion.Euler(0f, m.FacingAngle, 0f);
                SetColor(go, col);
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // SLOT POSITIONING — aligned to procedural blueprint furniture
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// World X for a specific slot within a building.
        /// Positions match the procedural blueprint interior layouts so
        /// minions appear at the correct furniture (lectern, pew, bed, bench).
        /// </summary>
        private float GetSlotWorldX(int origin, int slotIndex)
        {
            int bw = _city.GetBuildingWidth(origin);
            if (bw < 1) bw = 1;
            var type = _city.GetSurface(origin);
            int totalSlots = BuildingSlots.GetSlotCount(type, bw);

            float startX = origin * CityRenderer.CellSize;
            float buildingW = bw * CityRenderer.CellSize;

            if (totalSlots <= 1)
                return startX + buildingW * 0.5f;

            var role = BuildingSlots.GetSlotRole(type, slotIndex, bw);

            switch (type)
            {
                case CellType.Chapel:
                    if (role == SlotRole.Preacher)
                    {
                        // Lectern position — left 12% of building
                        return startX + buildingW * 0.12f;
                    }
                    else
                    {
                        // Pew positions — fill 28% to 95% of building
                        int pewIndex = slotIndex - 1; // slot 0 is preacher
                        int pewCount = totalSlots - 1;
                        float pewStart = startX + buildingW * 0.28f;
                        float pewEnd = startX + buildingW * 0.95f;
                        float pewRegion = pewEnd - pewStart;
                        float pewSpacing = pewRegion / Mathf.Max(1, pewCount);
                        return pewStart + pewSpacing * (pewIndex + 0.5f);
                    }

                case CellType.House:
                {
                    // Beds — evenly distributed across 5% to 95% of building
                    float bedRegion = buildingW * 0.9f;
                    int bedCount = Mathf.Max(1, bw);
                    float bedSpacing = bedRegion / bedCount;
                    int bedIndex = Mathf.Clamp(slotIndex / 2, 0, bedCount - 1); // 2 residents per bed
                    return startX + buildingW * 0.05f + bedSpacing * (bedIndex + 0.5f);
                }

                case CellType.Workshop:
                {
                    // Workbenches — evenly distributed across 5% to 95%
                    int benchCount = Mathf.Max(1, bw);
                    float benchRegion = buildingW * 0.9f;
                    float benchSpacing = benchRegion / benchCount;
                    int benchIndex = Mathf.Clamp(slotIndex / 2, 0, benchCount - 1);
                    return startX + buildingW * 0.05f + benchSpacing * (benchIndex + 0.5f);
                }

                case CellType.Market:
                {
                    // Stalls — merchant at left stall (slot 0), others at stalls
                    int stallCount = Mathf.Max(1, bw);
                    float stallRegion = buildingW * 0.9f;
                    float stallSpacing = stallRegion / stallCount;
                    int stallIndex = Mathf.Clamp(slotIndex, 0, stallCount - 1);
                    return startX + buildingW * 0.05f + stallSpacing * (stallIndex + 0.5f);
                }

                case CellType.Shipyard:
                {
                    // Foreman at left (slot 0), shipwrights along the drydock
                    if (slotIndex == 0)
                        return startX + buildingW * 0.10f;
                    int wrightCount = totalSlots - 1;
                    float wrightRegion = buildingW * 0.8f;
                    float wrightSpacing = wrightRegion / Mathf.Max(1, wrightCount);
                    return startX + buildingW * 0.15f + wrightSpacing * (slotIndex - 1 + 0.5f);
                }

                case CellType.Pier:
                {
                    // Dockers spread along the pier
                    float pierRegion = buildingW * 0.9f;
                    float pierSpacing = pierRegion / Mathf.Max(1, totalSlots);
                    return startX + buildingW * 0.05f + pierSpacing * (slotIndex + 0.5f);
                }

                case CellType.Warehouse:
                {
                    // Crane operators at front (1 per tile width), keeper at desk, haulers at shelves
                    if (slotIndex < bw)
                    {
                        // Crane operator — centered on their crane
                        return startX + (slotIndex + 0.5f) * CityRenderer.CellSize;
                    }
                    if (slotIndex == bw)
                    {
                        // Keeper at desk (far left)
                        return startX + buildingW * 0.05f;
                    }
                    // Haulers distributed across shelving area
                    int haulerIdx = slotIndex - bw - 1;
                    int haulerCount = totalSlots - bw - 1;
                    float haulerRegion = buildingW * 0.80f;
                    float haulerSpacing = haulerRegion / Mathf.Max(1, haulerCount);
                    return startX + buildingW * 0.10f + haulerSpacing * (haulerIdx + 0.5f);
                }

                default:
                {
                    // Generic even distribution
                    float margin = buildingW * 0.1f;
                    float usable = buildingW - margin * 2f;
                    float step = usable / (totalSlots - 1);
                    return startX + margin + Mathf.Clamp(slotIndex, 0, totalSlots - 1) * step;
                }
            }
        }

        /// <summary>
        /// World Y for a minion inside a building — placed at furniture height.
        /// Preacher stands taller (at lectern), worshippers sit (on pew), etc.
        /// </summary>
        private float GetBuildingSlotY(int origin, int slotIndex)
        {
            var type = _city.GetSurface(origin);
            int bw = _city.GetBuildingWidth(origin);
            if (bw < 1) bw = 1;
            var role = BuildingSlots.GetSlotRole(type, slotIndex, bw);

            switch (type)
            {
                case CellType.Chapel:
                    // Preacher stands at lectern height; worshippers sit on pews
                    return role == SlotRole.Preacher
                        ? RoadY + 0.28f  // standing at lectern
                        : RoadY + 0.16f; // seated in pew

                case CellType.House:
                    // Resting in beds (low position)
                    return RoadY + 0.16f;

                case CellType.Workshop:
                    // Standing at workbench
                    return RoadY + 0.28f;

                case CellType.Farm:
                    // Crouching among crops
                    return RoadY + 0.16f;

                case CellType.Market:
                    // Standing at counter
                    return RoadY + 0.24f;

                case CellType.Fountain:
                    return RoadY + 0.20f;

                case CellType.Shipyard:
                    // Foreman stands, shipwrights work at hull height
                    return role == SlotRole.Foreman
                        ? RoadY + 0.28f
                        : RoadY + 0.24f;

                case CellType.Pier:
                    return RoadY + 0.20f;

                case CellType.Warehouse:
                    // Crane operators at ground in front; keeper at desk; haulers at shelf height
                    if (role == SlotRole.CraneOperator)
                        return RoadY + 0.20f;
                    if (role == SlotRole.WarehouseKeeper)
                        return RoadY + 0.22f;
                    return RoadY + 0.28f;

                default:
                    return RoadY + 0.20f;
            }
        }

        private Color GetInBuildingColor(int origin)
        {
            var type = _city.GetSurface(origin);
            switch (type)
            {
                case CellType.Chapel:  return PrayColor;
                case CellType.House:   return RestColor;
                case CellType.Farm:
                case CellType.Market:  return EatColor;
                case CellType.Shipyard:
                case CellType.Pier:       return HarborColor;
                case CellType.Warehouse:  return WorkColor;
                default:                  return WorkColor;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // HELPERS
        // ═══════════════════════════════════════════════════════════════

        private GameObject CreateMinionCube(int index)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"Minion_{index}";
            go.transform.SetParent(_parent.transform, false);
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            return go;
        }

        private GameObject CreateCargoCube(int index)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"Cargo_{index}";
            go.transform.SetParent(_cargoParent.transform, false);
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            go.SetActive(false);
            return go;
        }

        private static void SetColor(GameObject go, Color color)
        {
            var r = go.GetComponent<Renderer>();
            if (r == null) return;
            var mat = r.material;
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            else
                mat.color = color;
        }
    }
}
