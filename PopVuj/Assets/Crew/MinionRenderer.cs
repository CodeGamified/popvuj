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

        private const float MinionW = 0.08f;
        private const float MinionH = 0.14f;
        private const float CartW   = 0.18f;           // wider body for cart-pullers
        private const float CartH   = 0.12f;           // slightly shorter (hunched under load)
        private const float CartDepth = 0.12f;          // thicker Z for the cart body
        private const float RoadY = 0.15f;              // matches CityRenderer RoadH
        private const float MinionZ = 0.25f;            // center road lane Z
        private const float InBuildingZ = 0.35f;        // slightly in front of building face

        // ── Color palette ───────────────────────────────────────

        private static readonly Color IdleColor = new Color(0.85f, 0.65f, 0.45f);
        private static readonly Color WalkColor = new Color(0.80f, 0.60f, 0.40f);
        private static readonly Color CartIdleColor = new Color(0.55f, 0.40f, 0.20f);   // darker wood-brown for cart
        private static readonly Color CartWalkColor = new Color(0.50f, 0.35f, 0.18f);
        private static readonly Color WorkColor = new Color(0.50f, 0.60f, 0.70f);
        private static readonly Color PrayColor = new Color(0.90f, 0.85f, 0.50f);
        private static readonly Color RestColor = new Color(0.55f, 0.50f, 0.65f);
        private static readonly Color EatColor  = new Color(0.60f, 0.75f, 0.45f);

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
                    // Inside building — positioned at slot within building volume
                    x = GetSlotWorldX(m.TargetBuilding, m.SlotIndex);
                    y = GetBuildingSlotY(m.TargetBuilding);
                    z = InBuildingZ;
                    col = GetInBuildingColor(m.TargetBuilding);
                    w = MinionW; h = MinionH; d = MinionW;
                    // No cargo visible inside buildings
                    cargoGO.SetActive(false);
                }
                else
                {
                    // Walking or idle — on the road surface, lane-offset on Z
                    x = m.X;
                    y = RoadY + (m.HasCart ? CartH : MinionH) * 0.5f;
                    z = MinionZ + m.Lane;

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
                        float cargoX = x + m.FacingDirection * (w * 0.5f + m.Cargo.VisualWidth * 0.3f);
                        float cargoY = y + m.Cargo.CarryOffsetY;
                        cargoGO.transform.localPosition = new Vector3(cargoX, cargoY, z);
                        cargoGO.transform.localScale = new Vector3(
                            m.Cargo.VisualWidth, m.Cargo.VisualHeight, m.Cargo.VisualDepth);
                        SetColor(cargoGO, Cargo.GetColor(m.Cargo.Kind));
                    }
                    else
                    {
                        cargoGO.SetActive(false);
                    }
                }

                go.transform.localPosition = new Vector3(x, y, z);
                go.transform.localScale = new Vector3(w, h, d);
                SetColor(go, col);
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // SLOT POSITIONING
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// World X for a specific slot within a building.
        /// Slots are evenly distributed across the building's width.
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

            float margin = buildingW * 0.1f;
            float usable = buildingW - margin * 2f;
            float step = usable / (totalSlots - 1);
            return startX + margin + Mathf.Clamp(slotIndex, 0, totalSlots - 1) * step;
        }

        /// <summary>
        /// World Y for a minion inside a building — roughly mid-height of the structure.
        /// </summary>
        private float GetBuildingSlotY(int origin)
        {
            var type = _city.GetSurface(origin);
            float height;
            switch (type)
            {
                case CellType.House:    height = 0.7f;  break;
                case CellType.Chapel:   height = 1.0f;  break;
                case CellType.Workshop: height = 0.6f;  break;
                case CellType.Farm:     height = 0.3f;  break;
                case CellType.Market:   height = 0.5f;  break;
                case CellType.Fountain: height = 0.4f;  break;
                default:                height = 0.4f;  break;
            }
            return RoadY + height * 0.35f;
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
                default:               return WorkColor;
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
