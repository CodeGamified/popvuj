// Copyright CodeGamified 2025-2026
// MIT License — PopVuj
using System.Collections.Generic;
using UnityEngine;
using PopVuj.Crew;

namespace PopVuj.Game
{
    /// <summary>
    /// High-fidelity ship renderer — "floating warehouses" on the road surface.
    ///
    /// Ships are 2D grids of modules [Width × Height] rendered as cutaway
    /// cross-sections showing:
    ///   - Hull outline (far planking, keel, ribs, stem posts) — near side
    ///     omitted for dollhouse/diorama view into the interior
    ///   - Below-deck modules with physically represented cargo (Store)
    ///   - Cannon modules that visually consume hold space (warfare vs trade)
    ///   - Rigging, sails, crew quarters, galley, magazine, etc.
    ///
    /// Design philosophy: boats are floating warehouses.
    /// Cargo is physically rendered in Store modules. Cannons consume
    /// grid cells that could be Store — warfare comes with a visible trade cost.
    /// Structure emerges from the grid layout: "forecastle" is not a concept,
    /// it's the result of enclosed modules stacking at certain positions.
    /// </summary>
    public class ShipRenderer : MonoBehaviour
    {
        private HarborManager _harbor;
        private CityGrid _city;

        private readonly List<GameObject> _shipPool = new List<GameObject>();
        private GameObject _parent;

        private bool _dirty = true;

        // Wave bobbing: track active ship visuals for per-frame updates
        private struct ShipVisual
        {
            public Ship ship;
            public GameObject go;
        }
        private readonly List<ShipVisual> _visuals = new List<ShipVisual>();

        // Dimensions (world units)
        private const float Cell = CityRenderer.CellSize;
        private const float WaterY = 0.10f;
        private const float ShipZ = 0f;

        // ═══════════════════════════════════════════════════════════════
        // COLOR PALETTE — SeaRäuber-grade, expanded from original
        // ═══════════════════════════════════════════════════════════════

        private static readonly Color HullColor     = new Color(0.40f, 0.25f, 0.12f);
        private static readonly Color HullInner     = new Color(0.48f, 0.32f, 0.18f);  // lighter inner hull planking
        private static readonly Color DeckColor     = new Color(0.50f, 0.35f, 0.18f);
        private static readonly Color MastColor     = new Color(0.55f, 0.38f, 0.15f);
        private static readonly Color SailColor     = new Color(0.85f, 0.80f, 0.70f);
        private static readonly Color TopsailColor  = new Color(0.88f, 0.84f, 0.76f);  // slightly lighter upper sails
        private static readonly Color CannonColor   = new Color(0.25f, 0.25f, 0.25f);
        private static readonly Color SkeletonColor = new Color(0.50f, 0.35f, 0.20f, 0.5f);
        private static readonly Color IronColor     = new Color(0.30f, 0.30f, 0.32f);
        private static readonly Color BrassColor    = new Color(0.72f, 0.55f, 0.20f);
        private static readonly Color RopeColor     = new Color(0.55f, 0.45f, 0.30f);
        private static readonly Color CabinColor    = new Color(0.45f, 0.35f, 0.25f);
        private static readonly Color CargoColor    = new Color(0.40f, 0.32f, 0.18f);
        private static readonly Color KeelColor     = new Color(0.25f, 0.15f, 0.08f);
        private static readonly Color RibColor      = new Color(0.44f, 0.28f, 0.14f);  // hull ribs
        private static readonly Color WaterlineClr  = new Color(0.20f, 0.12f, 0.06f);  // dark tarred waterline
        private static readonly Color MagazineColor = new Color(0.30f, 0.25f, 0.20f);  // powder magazine
        private static readonly Color GunDeckColor  = new Color(0.32f, 0.28f, 0.22f);  // gun deck floor
        private static readonly Color HammockColor  = new Color(0.50f, 0.42f, 0.32f);  // crew berthing
        private static readonly Color StemColor     = new Color(0.30f, 0.18f, 0.08f);  // bow/stern stem post

        public void Initialize(HarborManager harbor, CityGrid city)
        {
            _harbor = harbor;
            _city = city;
            _parent = new GameObject("Ships");
            _parent.transform.SetParent(transform, false);
            harbor.OnShipsChanged += () => _dirty = true;
        }

        private void LateUpdate()
        {
            if (_harbor == null) return;
            if (!_dirty)
            {
                UpdateWaveBobbing();
                return;
            }
            _dirty = false;
            Render();
        }

        public void MarkDirty() => _dirty = true;

        // ═══════════════════════════════════════════════════════════════
        // RENDER
        // ═══════════════════════════════════════════════════════════════

        private void Render()
        {
            for (int i = _parent.transform.childCount - 1; i >= 0; i--)
                Destroy(_parent.transform.GetChild(i).gameObject);

            _visuals.Clear();

            var ships = _harbor.Ships;
            for (int i = 0; i < ships.Count; i++)
            {
                var ship = ships[i];

                if (ship.State == ShipState.Building)
                    continue;

                var shipGO = new GameObject($"Ship_{ship.Id}_{ship.Hull}");
                shipGO.transform.SetParent(_parent.transform, false);

                float y = WaterSurface.Instance != null
                    ? WaterSurface.Instance.GetWaveHeight(ship.X)
                    : WaterY;
                shipGO.transform.localPosition = new Vector3(ship.X, y, ShipZ);

                RenderShip(ship, shipGO.transform);
                _visuals.Add(new ShipVisual { ship = ship, go = shipGO });
            }
        }

        private void UpdateWaveBobbing()
        {
            if (WaterSurface.Instance == null) return;

            for (int i = 0; i < _visuals.Count; i++)
            {
                var sv = _visuals[i];
                if (sv.go == null || sv.ship == null) continue;
                if (sv.ship.State == ShipState.Voyage) continue;

                float waveY = WaterSurface.Instance.GetWaveHeight(sv.ship.X);
                sv.go.transform.localPosition = new Vector3(sv.ship.X, waveY, ShipZ);

                Vector3 normal = WaterSurface.Instance.GetWaveNormal(sv.ship.X, 0f);
                float tilt = Mathf.Atan2(-normal.x, normal.y) * Mathf.Rad2Deg;
                sv.go.transform.localRotation = Quaternion.Euler(0f, 0f, tilt * 0.5f);
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // SHIP HULL — 2D grid cross-section with cutaway interior
        // ═══════════════════════════════════════════════════════════════

        // Grid cell dimensions (world units)
        private const float CellH = Cell * 0.55f; // row height — shorter than wide
        private const float HullDepth = Cell * 0.40f; // Z depth

        /// <summary>
        /// True for modules enclosed inside the hull (hold, cabins, stores).
        /// False for open/above-deck modules (masts, sails, rigging, etc.)
        /// that should render against open sky, not hull planking.
        /// </summary>
        private static bool IsEnclosedHull(ShipModule mod)
        {
            switch (mod)
            {
                case ShipModule.Store:
                case ShipModule.Cabin:
                case ShipModule.Kitchen:
                case ShipModule.Magazine:
                case ShipModule.Cannon:
                case ShipModule.Oars:
                case ShipModule.Helm:
                    return true;
                default:
                    return false;
            }
        }

        private void RenderShip(Ship ship, Transform parent)
        {
            int w = ship.Width;
            int h = ship.Height;
            float shipW = w * Cell;
            // Waterline: row 0 is below water for ships with height >= 2
            int waterRow = h >= 2 ? 1 : 0;

            // ── HULL STRUCTURE (cutaway cross-section) ──────────
            RenderHullStructure(parent, ship, w, h, waterRow);

            // ── GRID MODULES ────────────────────────────────────
            // Distribute cargo across Store modules
            int storeCount = ship.CountModule(ShipModule.Store);
            int cargoPerStore = storeCount > 0
                ? Mathf.CeilToInt((float)ship.CargoCount / storeCount)
                : 0;
            int cargoRemaining = ship.CargoCount;

            for (int col = 0; col < w; col++)
            {
                for (int row = 0; row < h; row++)
                {
                    ShipModule mod = ship.Grid[col, row];
                    if (mod == ShipModule.Air) continue;

                    float cx = (col - (w - 1) * 0.5f) * Cell;
                    float cy = (row - waterRow + 0.5f) * CellH;

                    if (mod == ShipModule.Store)
                    {
                        int items = Mathf.Min(cargoPerStore, cargoRemaining);
                        RenderStore(parent, cx, cy, col, row, ship.HoldCargoKind,
                            items, ship.CargoCapacity, storeCount);
                        cargoRemaining -= items;
                    }
                    else
                    {
                        RenderGridModule(mod, parent, cx, cy, col, row, ship, w, h);
                    }
                }
            }
        }

        /// <summary>
        /// Hull structure — the wooden shell visible as a cutaway cross-section.
        /// Far (starboard) side drawn as background, near (port) side omitted
        /// to show interior. Keel, ribs, deck planks, railing, stem posts.
        /// </summary>
        private void RenderHullStructure(Transform parent, Ship ship,
            int w, int h, int waterRow)
        {
            float shipW = w * Cell;
            float hullBottom = -waterRow * CellH;
            float hullTop = (h - waterRow) * CellH;

            // ── FAR HULL PLANKING (background wall) ─────────────
            // One panel per enclosed hull cell. Open/above-deck modules
            // (Mast, Sail, Crane, Lookout, etc.) get no hull backing.
            for (int col = 0; col < w; col++)
            {
                float cx = (col - (w - 1) * 0.5f) * Cell;
                for (int row = 0; row < h; row++)
                {
                    if (!IsEnclosedHull(ship.Grid[col, row]))
                        continue;

                    float cellY = (row - waterRow + 0.5f) * CellH;
                    var farHull = CreatePrimitive($"FarHull_{col}_{row}", parent);
                    farHull.transform.localPosition = new Vector3(cx, cellY, HullDepth * 0.48f);
                    farHull.transform.localScale = new Vector3(Cell * 0.96f, CellH, 0.02f);
                    SetColor(farHull, HullInner);
                }
            }

            // ── KEEL — backbone along the bottom ────────────────
            // Runs under all columns that have row-0 modules
            float keelY = hullBottom - 0.01f;
            var keel = CreatePrimitive("Keel", parent);
            keel.transform.localPosition = new Vector3(0f, keelY, 0f);
            keel.transform.localScale = new Vector3(shipW * 0.90f, 0.025f, Cell * 0.08f);
            SetColor(keel, KeelColor);

            // ── WATERLINE STRIPE ────────────────────────────────
            if (waterRow > 0)
            {
                var wl = CreatePrimitive("Waterline", parent);
                wl.transform.localPosition = new Vector3(0f, 0.005f, -HullDepth * 0.50f);
                wl.transform.localScale = new Vector3(shipW * 0.88f, 0.018f, 0.005f);
                SetColor(wl, WaterlineClr);
            }

            // ── HULL RIBS (per-cell vertical frames) ────────────
            // Only at boundaries of enclosed hull cells.
            for (int col = 0; col <= w; col++)
            {
                float rx = (-w * 0.5f + col) * Cell;
                for (int row = 0; row < h; row++)
                {
                    bool leftHull = col > 0 && IsEnclosedHull(ship.Grid[col - 1, row]);
                    bool rightHull = col < w && IsEnclosedHull(ship.Grid[col, row]);
                    if (!leftHull && !rightHull) continue;

                    float ribY = (row - waterRow + 0.5f) * CellH;
                    var rib = CreatePrimitive($"Rib_{col}_{row}", parent);
                    rib.transform.localPosition = new Vector3(rx, ribY, -HullDepth * 0.48f);
                    rib.transform.localScale = new Vector3(0.016f, CellH, 0.012f);
                    SetColor(rib, RibColor);
                }
            }

            // ── DECK PLANKS (per-cell floor panels) ─────────────
            // Only between enclosed hull cells.
            for (int col = 0; col < w; col++)
            {
                float cx = (col - (w - 1) * 0.5f) * Cell;
                for (int row = 1; row < h; row++)
                {
                    bool belowHull = IsEnclosedHull(ship.Grid[col, row - 1]);
                    bool aboveHull = IsEnclosedHull(ship.Grid[col, row]);
                    if (!belowHull && !aboveHull) continue;

                    float floorY = (row - waterRow) * CellH;
                    var floor = CreatePrimitive($"DeckFloor_{col}_{row}", parent);
                    floor.transform.localPosition = new Vector3(cx, floorY, 0f);
                    floor.transform.localScale = new Vector3(Cell * 0.88f, 0.02f, HullDepth * 0.90f);
                    SetColor(floor, DeckColor);
                }
            }

            // ── RAILING / BULWARK (per-cell at top of each hull column) ─
            {
                float railH = 0.025f + w * 0.005f;
                for (int col = 0; col < w; col++)
                {
                    // Find highest enclosed hull row in this column
                    int topHull = -1;
                    for (int row = h - 1; row >= 0; row--)
                    {
                        if (IsEnclosedHull(ship.Grid[col, row]))
                        { topHull = row; break; }
                    }
                    if (topHull < 0) continue;

                    float cx = (col - (w - 1) * 0.5f) * Cell;
                    float railY = (topHull - waterRow + 1) * CellH;

                    var railP = CreatePrimitive($"Rail_P_{col}", parent);
                    railP.transform.localPosition = new Vector3(cx, railY + railH * 0.5f, -HullDepth * 0.45f);
                    railP.transform.localScale = new Vector3(Cell * 0.86f, railH, 0.012f);
                    SetColor(railP, DeckColor);

                    var railS = CreatePrimitive($"Rail_S_{col}", parent);
                    railS.transform.localPosition = new Vector3(cx, railY + railH * 0.5f, HullDepth * 0.45f);
                    railS.transform.localScale = new Vector3(Cell * 0.86f, railH, 0.012f);
                    SetColor(railS, DeckColor);
                }
            }

            // ── BOW STEM POST ───────────────────────────────────
            if (w >= 2)
            {
                float bowX = (w - 1) * 0.5f * Cell + Cell * 0.46f;
                float stemH = (h - waterRow + waterRow) * CellH * 0.7f;
                var bow = CreatePrimitive("BowStem", parent);
                bow.transform.localPosition = new Vector3(bowX, hullBottom + stemH * 0.55f, 0f);
                bow.transform.localScale = new Vector3(0.03f, stemH, 0.04f);
                bow.transform.localRotation = Quaternion.Euler(0f, 0f, -10f);
                SetColor(bow, StemColor);
            }

            // ── STERN POST + RUDDER ─────────────────────────────
            if (w >= 2)
            {
                float sternX = -(w - 1) * 0.5f * Cell - Cell * 0.46f;
                float stemH = (h - waterRow + waterRow) * CellH * 0.55f;
                var stern = CreatePrimitive("SternPost", parent);
                stern.transform.localPosition = new Vector3(sternX, hullBottom + stemH * 0.45f, 0f);
                stern.transform.localScale = new Vector3(0.028f, stemH, 0.04f);
                stern.transform.localRotation = Quaternion.Euler(0f, 0f, 6f);
                SetColor(stern, StemColor);

                var rudder = CreatePrimitive("Rudder", parent);
                rudder.transform.localPosition = new Vector3(sternX - 0.02f, hullBottom + 0.03f, 0f);
                rudder.transform.localScale = new Vector3(0.02f, CellH * 0.8f, 0.04f);
                SetColor(rudder, IronColor);
            }
        }

        /// <summary>The "deck row" — the main exposed deck level.</summary>
        private static int GetDeckRow(int height)
        {
            // For h=1: row 0, h=2: row 1, h=3: row 2, h=4: row 2
            return Mathf.Min(height - 1, 2);
        }

        // ═══════════════════════════════════════════════════════════════
        // GRID MODULE DISPATCH
        // ═══════════════════════════════════════════════════════════════

        private void RenderGridModule(ShipModule module, Transform parent,
            float cx, float cy, int col, int row, Ship ship, int shipW, int shipH)
        {
            switch (module)
            {
                case ShipModule.Helm:
                    RenderHelm(parent, cx, cy, col, row, shipW);
                    break;
                case ShipModule.Mast:
                    RenderMast(parent, cx, cy, col, row, shipW);
                    break;
                case ShipModule.Cannon:
                    RenderCannon(parent, cx, cy, col, row, shipW);
                    break;
                case ShipModule.Crane:
                    RenderCrane(parent, cx, cy, col, row);
                    break;
                case ShipModule.Oars:
                    RenderOars(parent, cx, cy, col, row);
                    break;
                case ShipModule.Cabin:
                    RenderCabin(parent, cx, cy, col, row, shipW);
                    break;
                case ShipModule.FishingRig:
                    RenderFishingRig(parent, cx, cy, col, row);
                    break;
                case ShipModule.Lookout:
                    RenderLookout(parent, cx, cy, col, row, shipW);
                    break;
                case ShipModule.Kitchen:
                    RenderKitchen(parent, cx, cy, col, row);
                    break;
                case ShipModule.Magazine:
                    RenderMagazine(parent, cx, cy, col, row);
                    break;
                case ShipModule.FigureHead:
                    RenderFigureHead(parent, cx, cy, col, row);
                    break;
                case ShipModule.CraneBase:
                    RenderCraneBase(parent, cx, cy, col, row);
                    break;
                case ShipModule.SailBase:
                    RenderSailBase(parent, cx, cy, col, row, shipW);
                    break;
                case ShipModule.Wheel:
                    RenderWheel(parent, cx, cy, col, row, shipW);
                    break;
                case ShipModule.Sail:
                    RenderSail(parent, cx, cy, col, row, shipW);
                    break;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // HELM — wheel, binnacle, chart table
        // ═══════════════════════════════════════════════════════════════

        private void RenderHelm(Transform parent, float cx, float cy,
            int col, int row, int shipWidth)
        {
            float wheelD = 0.08f + shipWidth * 0.012f;
            var wheel = CreatePrimitive($"Helm_{col}_{row}", parent);
            wheel.transform.localPosition = new Vector3(cx, cy + CellH * 0.15f, 0f);
            wheel.transform.localScale = new Vector3(wheelD, wheelD, 0.015f);
            SetColor(wheel, BrassColor);

            var spoke = CreatePrimitive($"HelmSpoke_{col}_{row}", parent);
            spoke.transform.localPosition = new Vector3(cx, cy + CellH * 0.15f, 0f);
            spoke.transform.localScale = new Vector3(wheelD * 0.7f, 0.008f, 0.008f);
            SetColor(spoke, BrassColor);

            var post = CreatePrimitive($"HelmPost_{col}_{row}", parent);
            post.transform.localPosition = new Vector3(cx, cy - CellH * 0.15f, 0f);
            post.transform.localScale = new Vector3(0.025f, CellH * 0.35f, 0.025f);
            SetColor(post, MastColor);

            var binnacle = CreatePrimitive($"Binnacle_{col}_{row}", parent);
            binnacle.transform.localPosition = new Vector3(cx + Cell * 0.18f, cy - CellH * 0.05f, 0f);
            binnacle.transform.localScale = new Vector3(0.04f, CellH * 0.40f, 0.04f);
            SetColor(binnacle, MastColor);

            var compassCap = CreatePrimitive($"CompassCap_{col}_{row}", parent);
            compassCap.transform.localPosition = new Vector3(cx + Cell * 0.18f, cy + CellH * 0.18f, 0f);
            compassCap.transform.localScale = new Vector3(0.05f, 0.02f, 0.05f);
            SetColor(compassCap, BrassColor);
        }

        // ═══════════════════════════════════════════════════════════════
        // WHEEL — elevated steering wheel (superstructure)
        // ═══════════════════════════════════════════════════════════════

        private void RenderWheel(Transform parent, float cx, float cy,
            int col, int row, int shipWidth)
        {
            float wheelD = 0.10f + shipWidth * 0.015f;
            var wheel = CreatePrimitive($"Wheel_{col}_{row}", parent);
            wheel.transform.localPosition = new Vector3(cx, cy + CellH * 0.10f, 0f);
            wheel.transform.localScale = new Vector3(wheelD, wheelD, 0.018f);
            SetColor(wheel, BrassColor);

            var spoke1 = CreatePrimitive($"WheelSpoke1_{col}_{row}", parent);
            spoke1.transform.localPosition = new Vector3(cx, cy + CellH * 0.10f, 0f);
            spoke1.transform.localScale = new Vector3(wheelD * 0.7f, 0.008f, 0.008f);
            SetColor(spoke1, BrassColor);

            var spoke2 = CreatePrimitive($"WheelSpoke2_{col}_{row}", parent);
            spoke2.transform.localPosition = new Vector3(cx, cy + CellH * 0.10f, 0f);
            spoke2.transform.localScale = new Vector3(0.008f, wheelD * 0.7f, 0.008f);
            SetColor(spoke2, BrassColor);

            // Pedestal
            var ped = CreatePrimitive($"WheelPed_{col}_{row}", parent);
            ped.transform.localPosition = new Vector3(cx, cy - CellH * 0.20f, 0f);
            ped.transform.localScale = new Vector3(0.03f, CellH * 0.45f, 0.03f);
            SetColor(ped, MastColor);
        }

        // ═══════════════════════════════════════════════════════════════
        // MAST — pole with lower sail, shrouds, ratlines
        // ═══════════════════════════════════════════════════════════════

        private void RenderMast(Transform parent, float cx, float cy,
            int col, int row, int shipWidth)
        {
            float mastH = CellH * 2.2f + shipWidth * 0.04f;
            float mastW = 0.022f + shipWidth * 0.003f;

            var mast = CreatePrimitive($"Mast_{col}_{row}", parent);
            mast.transform.localPosition = new Vector3(cx, cy + mastH * 0.35f, 0f);
            mast.transform.localScale = new Vector3(mastW, mastH, mastW);
            SetColor(mast, MastColor);

            // Lower sail (course)
            float sailW = Cell * 0.55f + shipWidth * 0.02f;
            float sailH = mastH * 0.35f;
            var sail = CreatePrimitive($"Course_{col}_{row}", parent);
            sail.transform.localPosition = new Vector3(cx, cy + mastH * 0.35f, -Cell * 0.02f);
            sail.transform.localScale = new Vector3(0.008f, sailH, sailW);
            SetColor(sail, SailColor);

            // Lower yard
            var yard = CreatePrimitive($"YardLow_{col}_{row}", parent);
            yard.transform.localPosition = new Vector3(cx, cy + mastH * 0.52f, 0f);
            yard.transform.localScale = new Vector3(0.012f, 0.012f, sailW * 0.90f);
            SetColor(yard, MastColor);

            // Topsail for larger ships
            if (shipWidth >= 3)
            {
                float topW = sailW * 0.70f;
                float topH = mastH * 0.20f;
                var topsail = CreatePrimitive($"Topsail_{col}_{row}", parent);
                topsail.transform.localPosition = new Vector3(cx, cy + mastH * 0.62f, -Cell * 0.015f);
                topsail.transform.localScale = new Vector3(0.006f, topH, topW);
                SetColor(topsail, TopsailColor);

                var yardUp = CreatePrimitive($"YardUp_{col}_{row}", parent);
                yardUp.transform.localPosition = new Vector3(cx, cy + mastH * 0.72f, 0f);
                yardUp.transform.localScale = new Vector3(0.010f, 0.010f, topW * 0.85f);
                SetColor(yardUp, MastColor);
            }

            // Shrouds (port/starboard)
            float shroudSpread = Cell * 0.24f;
            float shroudH = mastH * 0.60f;

            var shroudP = CreatePrimitive($"Shroud_P_{col}_{row}", parent);
            shroudP.transform.localPosition = new Vector3(cx, cy + shroudH * 0.35f, -shroudSpread);
            shroudP.transform.localScale = new Vector3(0.005f, shroudH, 0.005f);
            shroudP.transform.localRotation = Quaternion.Euler(7f, 0f, 0f);
            SetColor(shroudP, RopeColor);

            var shroudS = CreatePrimitive($"Shroud_S_{col}_{row}", parent);
            shroudS.transform.localPosition = new Vector3(cx, cy + shroudH * 0.35f, shroudSpread);
            shroudS.transform.localScale = new Vector3(0.005f, shroudH, 0.005f);
            shroudS.transform.localRotation = Quaternion.Euler(-7f, 0f, 0f);
            SetColor(shroudS, RopeColor);

            // Ratlines
            int ratCount = Mathf.Max(2, Mathf.FloorToInt(shroudH / 0.06f));
            for (int r = 0; r < ratCount; r++)
            {
                float ry = cy + 0.03f + shroudH * 0.70f * ((float)(r + 1) / (ratCount + 1));
                var ratP = CreatePrimitive($"Rat_P_{col}_{row}_{r}", parent);
                ratP.transform.localPosition = new Vector3(cx, ry, -shroudSpread * 0.8f);
                ratP.transform.localScale = new Vector3(0.004f, 0.004f, shroudSpread * 0.4f);
                SetColor(ratP, RopeColor);

                var ratS = CreatePrimitive($"Rat_S_{col}_{row}_{r}", parent);
                ratS.transform.localPosition = new Vector3(cx, ry, shroudSpread * 0.8f);
                ratS.transform.localScale = new Vector3(0.004f, 0.004f, shroudSpread * 0.4f);
                SetColor(ratS, RopeColor);
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // SAIL — canvas area (above mast modules)
        // ═══════════════════════════════════════════════════════════════

        private void RenderSail(Transform parent, float cx, float cy,
            int col, int row, int shipWidth)
        {
            float sailW = Cell * 0.60f + shipWidth * 0.02f;
            float sailH = CellH * 0.80f;

            var sail = CreatePrimitive($"Sail_{col}_{row}", parent);
            sail.transform.localPosition = new Vector3(cx, cy, -Cell * 0.02f);
            sail.transform.localScale = new Vector3(0.008f, sailH, sailW);
            SetColor(sail, SailColor);

            // Yard at top
            var yard = CreatePrimitive($"SailYard_{col}_{row}", parent);
            yard.transform.localPosition = new Vector3(cx, cy + sailH * 0.45f, 0f);
            yard.transform.localScale = new Vector3(0.012f, 0.012f, sailW * 0.90f);
            SetColor(yard, MastColor);
        }

        // ═══════════════════════════════════════════════════════════════
        // SAILBASE — mast step at deck level
        // ═══════════════════════════════════════════════════════════════

        private void RenderSailBase(Transform parent, float cx, float cy,
            int col, int row, int shipWidth)
        {
            float mastW = 0.028f + shipWidth * 0.004f;

            // Mast trunk passing through this level
            var mast = CreatePrimitive($"MastBase_{col}_{row}", parent);
            mast.transform.localPosition = new Vector3(cx, cy, 0f);
            mast.transform.localScale = new Vector3(mastW, CellH * 0.95f, mastW);
            SetColor(mast, MastColor);

            // Mast step (reinforcing block at base)
            var step = CreatePrimitive($"MastStep_{col}_{row}", parent);
            step.transform.localPosition = new Vector3(cx, cy - CellH * 0.35f, 0f);
            step.transform.localScale = new Vector3(Cell * 0.12f, CellH * 0.15f, Cell * 0.12f);
            SetColor(step, DeckColor);

            // Belaying pin rail
            var pinRail = CreatePrimitive($"PinRail_{col}_{row}", parent);
            pinRail.transform.localPosition = new Vector3(cx - Cell * 0.18f, cy - CellH * 0.10f, 0f);
            pinRail.transform.localScale = new Vector3(Cell * 0.08f, CellH * 0.20f, 0.02f);
            SetColor(pinRail, MastColor);
        }

        // ═══════════════════════════════════════════════════════════════
        // CANNON — gun barrel, carriage, gunport, ammunition
        // Consumes space that could be Store — warfare vs trade
        // ═══════════════════════════════════════════════════════════════

        private void RenderCannon(Transform parent, float cx, float cy,
            int col, int row, int shipWidth)
        {
            // Cannon barrel (port side — protruding through hull)
            var barrel = CreatePrimitive($"Cannon_{col}_{row}", parent);
            barrel.transform.localPosition = new Vector3(cx, cy + CellH * 0.05f, -HullDepth * 0.35f);
            barrel.transform.localScale = new Vector3(0.07f, 0.032f, 0.032f);
            SetColor(barrel, CannonColor);

            // Cannon carriage (wooden wheeled mount)
            var carriage = CreatePrimitive($"Carriage_{col}_{row}", parent);
            carriage.transform.localPosition = new Vector3(cx, cy - CellH * 0.15f, -HullDepth * 0.20f);
            carriage.transform.localScale = new Vector3(0.06f, 0.028f, 0.05f);
            SetColor(carriage, DeckColor);

            // Gunport lid (open, hinged outward)
            var portLid = CreatePrimitive($"Gunport_{col}_{row}", parent);
            portLid.transform.localPosition = new Vector3(cx, cy + CellH * 0.20f, -HullDepth * 0.48f);
            portLid.transform.localScale = new Vector3(0.07f, 0.05f, 0.008f);
            portLid.transform.localRotation = Quaternion.Euler(-25f, 0f, 0f);
            SetColor(portLid, HullColor);

            // Cannonball rack
            var rack = CreatePrimitive($"BallRack_{col}_{row}", parent);
            rack.transform.localPosition = new Vector3(cx + Cell * 0.15f, cy - CellH * 0.25f, 0f);
            rack.transform.localScale = new Vector3(0.04f, 0.035f, 0.04f);
            SetColor(rack, IronColor);

            // Powder bucket
            var bucket = CreatePrimitive($"PowderBkt_{col}_{row}", parent);
            bucket.transform.localPosition = new Vector3(cx - Cell * 0.15f, cy - CellH * 0.28f, 0f);
            bucket.transform.localScale = new Vector3(0.03f, 0.03f, 0.03f);
            SetColor(bucket, DeckColor);

            // Recoil tackle
            var tackle = CreatePrimitive($"Tackle_{col}_{row}", parent);
            tackle.transform.localPosition = new Vector3(cx, cy, -HullDepth * 0.18f);
            tackle.transform.localScale = new Vector3(0.004f, 0.004f, HullDepth * 0.15f);
            SetColor(tackle, RopeColor);
        }

        // ═══════════════════════════════════════════════════════════════
        // STORE — warehouse-style physical cargo in the hold
        // This is the "floating warehouse" core: cargo PHYSICALLY VISIBLE
        // ═══════════════════════════════════════════════════════════════

        private void RenderStore(Transform parent, float cx, float cy,
            int col, int row, CargoKind cargoKind, int cargoItems,
            int totalCapacity, int storeCount)
        {
            // Hold walls (inner planking visible on cell sides)
            var backWall = CreatePrimitive($"StoreWall_{col}_{row}", parent);
            backWall.transform.localPosition = new Vector3(cx, cy, HullDepth * 0.30f);
            backWall.transform.localScale = new Vector3(Cell * 0.88f, CellH * 0.90f, 0.008f);
            SetColor(backWall, HullInner);

            // Floor (bottom of store)
            var floor = CreatePrimitive($"StoreFloor_{col}_{row}", parent);
            floor.transform.localPosition = new Vector3(cx, cy - CellH * 0.42f, 0f);
            floor.transform.localScale = new Vector3(Cell * 0.80f, 0.015f, HullDepth * 0.60f);
            SetColor(floor, DeckColor);

            // Physical cargo items — warehouse-style stacking bottom-up
            if (cargoItems > 0 && cargoKind != CargoKind.None)
            {
                RenderHoldCargo(parent, cx, cy - CellH * 0.35f,
                    CellH * 0.70f, Cell * 0.70f, cargoKind, cargoItems, col, row);
            }
        }

        /// <summary>
        /// Physical cargo items stacked bottom-up in a cell.
        /// Warehouse-style fill: items stack in rows/columns.
        /// </summary>
        private void RenderHoldCargo(Transform parent, float cx, float baseY,
            float maxH, float maxW, CargoKind kind, int count, int col, int row)
        {
            Color itemColor = Cargo.GetColor(kind);
            float itemW, itemH, itemD;

            switch (kind)
            {
                case CargoKind.Log:
                    itemW = 0.04f; itemH = 0.028f; itemD = 0.10f; break;
                case CargoKind.Crate: case CargoKind.TradeCrate: case CargoKind.ExoticGoods:
                    itemW = 0.045f; itemH = 0.045f; itemD = 0.045f; break;
                case CargoKind.Grain: case CargoKind.Fish:
                    itemW = 0.036f; itemH = 0.045f; itemD = 0.036f; break;
                case CargoKind.Plank:
                    itemW = 0.07f; itemH = 0.018f; itemD = 0.035f; break;
                case CargoKind.Water:
                    itemW = 0.036f; itemH = 0.055f; itemD = 0.036f; break;
                default:
                    itemW = 0.038f; itemH = 0.038f; itemD = 0.038f; break;
            }

            int colsPerRow = Mathf.Max(1, Mathf.FloorToInt(maxW / (itemW * 1.15f)));
            int maxRows = Mathf.Max(1, Mathf.FloorToInt(maxH / (itemH * 1.1f)));
            int toPlace = Mathf.Min(count, colsPerRow * maxRows);

            float startX = cx - (colsPerRow - 1) * itemW * 0.55f;
            int placed = 0;
            // Pick a mesh name based on cargo kind
            string meshName = null;
            switch (kind)
            {
                case CargoKind.Crate: case CargoKind.TradeCrate: case CargoKind.ExoticGoods:
                    meshName = "rounded_chest_2"; break;
                case CargoKind.Grain: case CargoKind.Fish:
                    meshName = "burlap_sack_one_sack"; break;
                case CargoKind.Water:
                    meshName = "wooden_bucket"; break;
                default:
                    meshName = "small_barrel_one_barrel"; break;
            }
            bool hasMesh = meshName != null && ObjectScale.LoadMesh(meshName) != null;

            for (int r = 0; r < maxRows && placed < toPlace; r++)
            {
                float iy = baseY + (hasMesh ? 0f : itemH * 0.5f) + r * itemH * 1.1f;
                for (int c = 0; c < colsPerRow && placed < toPlace; c++, placed++)
                {
                    float ix = startX + c * itemW * 1.1f;
                    GameObject item;
                    if (hasMesh)
                    {
                        item = CreateMeshOrPrimitive($"Cargo_{col}_{row}_{placed}", meshName, parent);
                        item.transform.localPosition = new Vector3(ix, iy, 0f);
                        item.transform.localScale = ObjectScale.GetScale(meshName);
                    }
                    else
                    {
                        item = CreatePrimitive($"Cargo_{col}_{row}_{placed}", parent);
                        item.transform.localPosition = new Vector3(ix, iy, 0f);
                        item.transform.localScale = new Vector3(itemW, itemH, itemD);
                    }
                    SetColor(item, itemColor);
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // CRANE — A-frame with boom and hook
        // ═══════════════════════════════════════════════════════════════

        private void RenderCrane(Transform parent, float cx, float cy,
            int col, int row)
        {
            float legH = CellH * 0.80f;
            float legSpread = Cell * 0.12f;

            var legP = CreatePrimitive($"CraneLeg_P_{col}_{row}", parent);
            legP.transform.localPosition = new Vector3(cx - legSpread, cy, 0f);
            legP.transform.localScale = new Vector3(0.025f, legH, 0.025f);
            SetColor(legP, IronColor);

            var legS = CreatePrimitive($"CraneLeg_S_{col}_{row}", parent);
            legS.transform.localPosition = new Vector3(cx + legSpread, cy, 0f);
            legS.transform.localScale = new Vector3(0.025f, legH, 0.025f);
            SetColor(legS, IronColor);

            var beam = CreatePrimitive($"CraneBeam_{col}_{row}", parent);
            beam.transform.localPosition = new Vector3(cx, cy + legH * 0.45f, 0f);
            beam.transform.localScale = new Vector3(legSpread * 2.2f, 0.035f, 0.035f);
            SetColor(beam, IronColor);

            // Boom arm
            var boom = CreatePrimitive($"CraneBoom_{col}_{row}", parent);
            boom.transform.localPosition = new Vector3(cx + Cell * 0.22f, cy + legH * 0.35f, 0f);
            boom.transform.localScale = new Vector3(Cell * 0.35f, 0.02f, 0.02f);
            boom.transform.localRotation = Quaternion.Euler(0f, 0f, -5f);
            SetColor(boom, IronColor);

            // Rope + hook
            var rope = CreatePrimitive($"CraneRope_{col}_{row}", parent);
            rope.transform.localPosition = new Vector3(cx + Cell * 0.35f, cy + legH * 0.10f, 0f);
            rope.transform.localScale = new Vector3(0.005f, legH * 0.50f, 0.005f);
            SetColor(rope, RopeColor);

            var hook = CreatePrimitive($"CraneHook_{col}_{row}", parent);
            hook.transform.localPosition = new Vector3(cx + Cell * 0.35f, cy - legH * 0.10f, 0f);
            hook.transform.localScale = new Vector3(0.02f, 0.022f, 0.02f);
            SetColor(hook, IronColor);
        }

        // ═══════════════════════════════════════════════════════════════
        // CRANEBASE — foundation at deck level supporting crane above
        // ═══════════════════════════════════════════════════════════════

        private void RenderCraneBase(Transform parent, float cx, float cy,
            int col, int row)
        {
            // Heavy timber platform
            var platform = CreatePrimitive($"CraneBasePlat_{col}_{row}", parent);
            platform.transform.localPosition = new Vector3(cx, cy, 0f);
            platform.transform.localScale = new Vector3(Cell * 0.60f, CellH * 0.18f, HullDepth * 0.70f);
            SetColor(platform, DeckColor);

            // Cross bracing
            var brace1 = CreatePrimitive($"CraneBrace1_{col}_{row}", parent);
            brace1.transform.localPosition = new Vector3(cx - Cell * 0.10f, cy - CellH * 0.20f, 0f);
            brace1.transform.localScale = new Vector3(0.02f, CellH * 0.50f, 0.02f);
            brace1.transform.localRotation = Quaternion.Euler(0f, 0f, 8f);
            SetColor(brace1, MastColor);

            var brace2 = CreatePrimitive($"CraneBrace2_{col}_{row}", parent);
            brace2.transform.localPosition = new Vector3(cx + Cell * 0.10f, cy - CellH * 0.20f, 0f);
            brace2.transform.localScale = new Vector3(0.02f, CellH * 0.50f, 0.02f);
            brace2.transform.localRotation = Quaternion.Euler(0f, 0f, -8f);
            SetColor(brace2, MastColor);
        }

        // ═══════════════════════════════════════════════════════════════
        // OARS — rowing station with thwart bench
        // ═══════════════════════════════════════════════════════════════

        private void RenderOars(Transform parent, float cx, float cy,
            int col, int row)
        {
            var oarP = CreatePrimitive($"Oar_P_{col}_{row}", parent);
            oarP.transform.localPosition = new Vector3(cx, cy + CellH * 0.05f, -HullDepth * 0.30f);
            oarP.transform.localScale = new Vector3(0.025f, 0.012f, 0.18f);
            oarP.transform.localRotation = Quaternion.Euler(-15f, 0f, 0f);
            SetColor(oarP, MastColor);

            var oarS = CreatePrimitive($"Oar_S_{col}_{row}", parent);
            oarS.transform.localPosition = new Vector3(cx, cy + CellH * 0.05f, HullDepth * 0.30f);
            oarS.transform.localScale = new Vector3(0.025f, 0.012f, 0.18f);
            oarS.transform.localRotation = Quaternion.Euler(15f, 0f, 0f);
            SetColor(oarS, MastColor);

            // Thwart bench
            var bench = CreatePrimitive($"Thwart_{col}_{row}", parent);
            bench.transform.localPosition = new Vector3(cx, cy - CellH * 0.10f, 0f);
            bench.transform.localScale = new Vector3(0.05f, 0.018f, HullDepth * 0.50f);
            SetColor(bench, DeckColor);
        }

        // ═══════════════════════════════════════════════════════════════
        // CABIN — enclosed crew quarters
        // ═══════════════════════════════════════════════════════════════

        private void RenderCabin(Transform parent, float cx, float cy,
            int col, int row, int shipWidth)
        {
            float cabW = Cell * 0.80f;
            float cabH = CellH * 0.85f;

            // Side walls (left + right)
            var wallL = CreatePrimitive($"CabinWallL_{col}_{row}", parent);
            wallL.transform.localPosition = new Vector3(cx - cabW * 0.45f, cy, 0f);
            wallL.transform.localScale = new Vector3(0.015f, cabH, HullDepth * 0.50f);
            SetColor(wallL, CabinColor);

            var wallR = CreatePrimitive($"CabinWallR_{col}_{row}", parent);
            wallR.transform.localPosition = new Vector3(cx + cabW * 0.45f, cy, 0f);
            wallR.transform.localScale = new Vector3(0.015f, cabH, HullDepth * 0.50f);
            SetColor(wallR, CabinColor);

            // Door
            var door = CreatePrimitive($"CabinDoor_{col}_{row}", parent);
            door.transform.localPosition = new Vector3(cx, cy - CellH * 0.05f, -HullDepth * 0.24f);
            door.transform.localScale = new Vector3(0.04f, cabH * 0.60f, 0.004f);
            SetColor(door, HullColor);

            // Hammock
            var hammock = CreatePrimitive($"Hammock_{col}_{row}", parent);
            hammock.transform.localPosition = new Vector3(cx, cy + CellH * 0.10f, 0f);
            hammock.transform.localScale = new Vector3(cabW * 0.60f, 0.012f, 0.03f);
            SetColor(hammock, HammockColor);

            // Porthole
            if (shipWidth >= 2)
            {
                var porthole = CreatePrimitive($"Porthole_{col}_{row}", parent);
                porthole.transform.localPosition = new Vector3(cx + cabW * 0.30f, cy + CellH * 0.12f, -HullDepth * 0.24f);
                porthole.transform.localScale = new Vector3(0.022f, 0.022f, 0.003f);
                SetColor(porthole, new Color(0.55f, 0.50f, 0.40f));
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // KITCHEN — galley with stove and table
        // ═══════════════════════════════════════════════════════════════

        private void RenderKitchen(Transform parent, float cx, float cy,
            int col, int row)
        {
            // Stove / hearth — try custom mesh
            var stove = CreateMeshOrPrimitive($"Stove_{col}_{row}", "stove", parent);
            stove.transform.localPosition = new Vector3(cx - Cell * 0.12f, cy - CellH * 0.15f, 0f);
            stove.transform.localScale = ObjectScale.LoadMesh("stove") != null
                ? ObjectScale.GetScale("stove")
                : new Vector3(Cell * 0.22f, CellH * 0.35f, HullDepth * 0.35f);
            SetColor(stove, new Color(0.35f, 0.22f, 0.15f));

            // Chimney pipe
            var chimney = CreatePrimitive($"Chimney_{col}_{row}", parent);
            chimney.transform.localPosition = new Vector3(cx - Cell * 0.12f, cy + CellH * 0.20f, 0f);
            chimney.transform.localScale = new Vector3(0.025f, CellH * 0.35f, 0.025f);
            SetColor(chimney, IronColor);

            // Table
            var table = CreatePrimitive($"GalleyTable_{col}_{row}", parent);
            table.transform.localPosition = new Vector3(cx + Cell * 0.12f, cy - CellH * 0.10f, 0f);
            table.transform.localScale = new Vector3(Cell * 0.25f, 0.015f, HullDepth * 0.30f);
            SetColor(table, DeckColor);

            // Pot on stove
            var pot = CreatePrimitive($"Pot_{col}_{row}", parent);
            pot.transform.localPosition = new Vector3(cx - Cell * 0.10f, cy + CellH * 0.05f, 0f);
            pot.transform.localScale = new Vector3(0.03f, 0.025f, 0.03f);
            SetColor(pot, IronColor);
        }

        // ═══════════════════════════════════════════════════════════════
        // MAGAZINE — sealed powder room
        // ═══════════════════════════════════════════════════════════════

        private void RenderMagazine(Transform parent, float cx, float cy,
            int col, int row)
        {
            // Heavy walls (this is a sealed room)
            var wall = CreatePrimitive($"MagWall_{col}_{row}", parent);
            wall.transform.localPosition = new Vector3(cx, cy, HullDepth * 0.25f);
            wall.transform.localScale = new Vector3(Cell * 0.85f, CellH * 0.88f, 0.015f);
            SetColor(wall, MagazineColor);

            // Powder barrels (2-3 dark kegs) — try custom mesh
            for (int b = 0; b < 3; b++)
            {
                float bx = cx + (b - 1) * 0.05f;
                var barrel = CreateMeshOrPrimitive($"PowderKeg_{col}_{row}_{b}", "keg", parent);
                barrel.transform.localPosition = new Vector3(bx, cy - CellH * 0.18f, 0f);
                barrel.transform.localScale = ObjectScale.LoadMesh("keg") != null
                    ? ObjectScale.GetScale("keg")
                    : new Vector3(0.035f, 0.045f, 0.035f);
                SetColor(barrel, new Color(0.18f, 0.15f, 0.12f));
            }

            // Warning lantern (copper)
            var lantern = CreatePrimitive($"MagLantern_{col}_{row}", parent);
            lantern.transform.localPosition = new Vector3(cx, cy + CellH * 0.25f, -HullDepth * 0.10f);
            lantern.transform.localScale = new Vector3(0.018f, 0.025f, 0.018f);
            SetColor(lantern, BrassColor);
        }

        // ═══════════════════════════════════════════════════════════════
        // FISHING RIG — pole, line, tackle
        // ═══════════════════════════════════════════════════════════════

        private void RenderFishingRig(Transform parent, float cx, float cy,
            int col, int row)
        {
            var pole = CreatePrimitive($"FishPole_{col}_{row}", parent);
            pole.transform.localPosition = new Vector3(cx + Cell * 0.15f, cy + CellH * 0.20f, -HullDepth * 0.15f);
            pole.transform.localScale = new Vector3(0.010f, CellH * 0.70f, 0.010f);
            pole.transform.localRotation = Quaternion.Euler(0f, 0f, -30f);
            SetColor(pole, MastColor);

            var line = CreatePrimitive($"FishLine_{col}_{row}", parent);
            line.transform.localPosition = new Vector3(cx + Cell * 0.30f, cy + CellH * 0.05f, -HullDepth * 0.15f);
            line.transform.localScale = new Vector3(0.003f, CellH * 0.40f, 0.003f);
            SetColor(line, RopeColor);

            // Tackle box
            var box = CreatePrimitive($"TackleBox_{col}_{row}", parent);
            box.transform.localPosition = new Vector3(cx - Cell * 0.10f, cy - CellH * 0.28f, 0f);
            box.transform.localScale = new Vector3(0.06f, 0.04f, 0.04f);
            SetColor(box, DeckColor);

            // Net
            var net = CreatePrimitive($"FishNet_{col}_{row}", parent);
            net.transform.localPosition = new Vector3(cx + Cell * 0.05f, cy - CellH * 0.15f, -HullDepth * 0.25f);
            net.transform.localScale = new Vector3(0.10f, 0.05f, 0.01f);
            SetColor(net, RopeColor);
        }

        // ═══════════════════════════════════════════════════════════════
        // LOOKOUT — crow's nest / observation platform
        // ═══════════════════════════════════════════════════════════════

        private void RenderLookout(Transform parent, float cx, float cy,
            int col, int row, int shipWidth)
        {
            float poleH = CellH * 1.8f + shipWidth * 0.03f;

            // Tall pole
            var pole = CreatePrimitive($"LookoutPole_{col}_{row}", parent);
            pole.transform.localPosition = new Vector3(cx, cy + poleH * 0.35f, 0f);
            pole.transform.localScale = new Vector3(0.020f, poleH, 0.020f);
            SetColor(pole, MastColor);

            // Crow's nest platform
            var nest = CreatePrimitive($"CrowsNest_{col}_{row}", parent);
            nest.transform.localPosition = new Vector3(cx, cy + poleH * 0.82f, 0f);
            nest.transform.localScale = new Vector3(0.12f, 0.015f, 0.12f);
            SetColor(nest, DeckColor);

            // Nest railings
            var railF = CreatePrimitive($"NestRail_F_{col}_{row}", parent);
            railF.transform.localPosition = new Vector3(cx, cy + poleH * 0.85f, -0.05f);
            railF.transform.localScale = new Vector3(0.12f, 0.02f, 0.005f);
            SetColor(railF, DeckColor);

            var railB = CreatePrimitive($"NestRail_B_{col}_{row}", parent);
            railB.transform.localPosition = new Vector3(cx, cy + poleH * 0.85f, 0.05f);
            railB.transform.localScale = new Vector3(0.12f, 0.02f, 0.005f);
            SetColor(railB, DeckColor);

            // Climbing ladder rungs
            int rungs = Mathf.Max(3, Mathf.FloorToInt(poleH / 0.08f));
            for (int r = 0; r < rungs; r++)
            {
                float ry = cy + 0.03f + poleH * 0.68f * ((float)(r + 1) / (rungs + 1));
                var rung = CreatePrimitive($"LookoutRung_{col}_{row}_{r}", parent);
                rung.transform.localPosition = new Vector3(cx - 0.03f, ry, 0f);
                rung.transform.localScale = new Vector3(0.035f, 0.003f, 0.003f);
                SetColor(rung, RopeColor);
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // FIGUREHEAD — bow decoration
        // ═══════════════════════════════════════════════════════════════

        private void RenderFigureHead(Transform parent, float cx, float cy,
            int col, int row)
        {
            // Main figurehead body (angled forward)
            var figure = CreatePrimitive($"FigureHead_{col}_{row}", parent);
            figure.transform.localPosition = new Vector3(cx + Cell * 0.15f, cy, 0f);
            figure.transform.localScale = new Vector3(Cell * 0.20f, CellH * 0.40f, 0.04f);
            figure.transform.localRotation = Quaternion.Euler(0f, 0f, -20f);
            SetColor(figure, BrassColor);

            // Bowsprit spar
            var bowsprit = CreatePrimitive($"Bowsprit_{col}_{row}", parent);
            bowsprit.transform.localPosition = new Vector3(cx + Cell * 0.28f, cy + CellH * 0.15f, 0f);
            bowsprit.transform.localScale = new Vector3(Cell * 0.30f, 0.02f, 0.02f);
            bowsprit.transform.localRotation = Quaternion.Euler(0f, 0f, 15f);
            SetColor(bowsprit, MastColor);

            // Jib stay line
            var jibStay = CreatePrimitive($"JibStay_{col}_{row}", parent);
            jibStay.transform.localPosition = new Vector3(cx + Cell * 0.20f, cy + CellH * 0.30f, 0f);
            jibStay.transform.localScale = new Vector3(0.004f, CellH * 0.50f, 0.004f);
            jibStay.transform.localRotation = Quaternion.Euler(0f, 0f, 20f);
            SetColor(jibStay, RopeColor);
        }

        // ═══════════════════════════════════════════════════════════════
        // HELPERS
        // ═══════════════════════════════════════════════════════════════

        private static GameObject CreatePrimitive(string name, Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            return go;
        }

        /// <summary>
        /// Try to create a mesh object from Resources/Objects/. Falls back to
        /// a primitive cube if the mesh isn't found.
        /// </summary>
        private static GameObject CreateMeshOrPrimitive(string name, string objName, Transform parent)
        {
            var go = ObjectScale.CreateMeshObject(name, objName, parent);
            if (go != null) return go;
            return CreatePrimitive(name, parent);
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

            if (color.a < 1f)
            {
                if (mat.HasProperty("_Surface"))
                    mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_ZWrite", 0f);
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.SetOverrideTag("RenderType", "Transparent");
            }
        }
    }
}
