// Copyright CodeGamified 2025-2026
// MIT License — PopVuj
using System.Collections.Generic;
using UnityEngine;

namespace PopVuj.Game
{
    /// <summary>
    /// Renders ships as side-view profiles on the road surface (Z=0).
    ///
    /// Ships dock along the pier's X extent and travel on the road plane.
    /// They slide left/right like everything else on the city strip.
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
        private const float WaterY = 0.10f;       // water surface Y (slightly above 0)
        private const float ShipZ = 0f;             // Z depth for ships (on the road surface)

        // Ship colors
        private static readonly Color HullColor     = new Color(0.40f, 0.25f, 0.12f);
        private static readonly Color DeckColor     = new Color(0.50f, 0.35f, 0.18f);
        private static readonly Color MastColor     = new Color(0.55f, 0.38f, 0.15f);
        private static readonly Color SailColor     = new Color(0.85f, 0.80f, 0.70f);
        private static readonly Color CannonColor   = new Color(0.25f, 0.25f, 0.25f);
        private static readonly Color SkeletonColor = new Color(0.50f, 0.35f, 0.20f, 0.5f);
        private static readonly Color IronColor     = new Color(0.30f, 0.30f, 0.32f);
        private static readonly Color BrassColor    = new Color(0.72f, 0.55f, 0.20f);
        private static readonly Color RopeColor     = new Color(0.55f, 0.45f, 0.30f);
        private static readonly Color CabinColor    = new Color(0.45f, 0.35f, 0.25f);
        private static readonly Color CargoColor    = new Color(0.40f, 0.32f, 0.18f);
        private static readonly Color KeelColor     = new Color(0.25f, 0.15f, 0.08f);

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
            // Destroy old ships and rebuild (simple approach matching CityRenderer pattern)
            for (int i = _parent.transform.childCount - 1; i >= 0; i--)
                Destroy(_parent.transform.GetChild(i).gameObject);

            _visuals.Clear();

            var ships = _harbor.Ships;
            for (int i = 0; i < ships.Count; i++)
            {
                var ship = ships[i];

                // Building-state ships are rendered at the shipyard, not at anchorage
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

        /// <summary>
        /// Per-frame wave bobbing — update ship Y position and tilt from wave surface.
        /// </summary>
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

                // Tilt ship to match wave surface normal
                Vector3 normal = WaterSurface.Instance.GetWaveNormal(sv.ship.X, 0f);
                float tilt = Mathf.Atan2(-normal.x, normal.y) * Mathf.Rad2Deg;
                sv.go.transform.localRotation = Quaternion.Euler(0f, 0f, tilt * 0.5f);
            }
        }

        private void RenderShip(Ship ship, Transform parent)
        {
            float shipW = ship.Width * Cell;
            float y = 0f; // relative to parent (parent already at WaterY)

            // Hull — always present, shaped by hull class
            float hullH = GetHullHeight(ship.Width);
            float hullDraft = hullH * 0.3f; // how far below waterline

            // Hull bow taper (narrower at front)
            var hull = CreatePrimitive("Hull", parent);
            hull.transform.localPosition = new Vector3(0f, y + hullH * 0.5f - hullDraft, 0f);
            hull.transform.localScale = new Vector3(shipW * 0.90f, hullH, Cell * 0.4f);
            SetColor(hull, HullColor);

            // Keel line (darker strip under hull)
            if (ship.Width >= 2)
            {
                var keel = CreatePrimitive("Keel", parent);
                keel.transform.localPosition = new Vector3(0f, y - hullDraft - 0.01f, 0f);
                keel.transform.localScale = new Vector3(shipW * 0.12f, 0.02f, Cell * 0.3f);
                SetColor(keel, KeelColor);
            }

            // Deck — flat on top of hull
            var deck = CreatePrimitive("Deck", parent);
            deck.transform.localPosition = new Vector3(0f, y + hullH - hullDraft + 0.02f, 0f);
            deck.transform.localScale = new Vector3(shipW * 0.85f, 0.04f, Cell * 0.38f);
            SetColor(deck, DeckColor);

            // Hull rail (thin raised edge along deck perimeter)
            if (ship.Width >= 2)
            {
                float railY = y + hullH - hullDraft + 0.06f;
                var railP = CreatePrimitive("Rail_Port", parent);
                railP.transform.localPosition = new Vector3(-shipW * 0.42f, railY, 0f);
                railP.transform.localScale = new Vector3(0.02f, 0.04f, shipW * 0.85f);
                SetColor(railP, DeckColor);

                var railS = CreatePrimitive("Rail_Stbd", parent);
                railS.transform.localPosition = new Vector3(shipW * 0.42f, railY, 0f);
                railS.transform.localScale = new Vector3(0.02f, 0.04f, shipW * 0.85f);
                SetColor(railS, DeckColor);
            }

            float deckY = y + hullH - hullDraft + 0.04f;
            float mastH = GetMastHeight(ship.Width);

            // ── Per-tile module rendering ────────────────────────
            // Each tile-width gets its module visual, positioned left-to-right
            // Stern (index 0) is at the left (-X), Bow (last index) is at the right (+X)
            if (ship.Modules != null)
            {
                for (int t = 0; t < ship.Modules.Length; t++)
                {
                    // Tile center X relative to ship center
                    float tileX = (t - (ship.Width - 1) * 0.5f) * Cell;

                    RenderDeckModule(ship.Modules[t], parent, tileX, deckY, mastH, t);
                }
            }
        }

        /// <summary>
        /// Render a single deck module at the given tile position.
        /// Each module type has distinct visual geometry.
        /// </summary>
        private void RenderDeckModule(DeckModule module, Transform parent,
            float tileX, float deckY, float mastH, int tileIndex)
        {
            switch (module)
            {
                case DeckModule.Helm:
                    RenderHelm(parent, tileX, deckY, tileIndex);
                    break;
                case DeckModule.Mast:
                    RenderMast(parent, tileX, deckY, mastH, tileIndex);
                    break;
                case DeckModule.Cannon:
                    RenderCannon(parent, tileX, deckY, tileIndex);
                    break;
                case DeckModule.Crane:
                    RenderCrane(parent, tileX, deckY, tileIndex);
                    break;
                case DeckModule.Oars:
                    RenderOars(parent, tileX, deckY, tileIndex);
                    break;
                case DeckModule.CargoHatch:
                    RenderCargoHatch(parent, tileX, deckY, tileIndex);
                    break;
                case DeckModule.Cabin:
                    RenderCabin(parent, tileX, deckY, tileIndex);
                    break;
                case DeckModule.FishingRig:
                    RenderFishingRig(parent, tileX, deckY, tileIndex);
                    break;
                case DeckModule.Lookout:
                    RenderLookout(parent, tileX, deckY, mastH, tileIndex);
                    break;
                case DeckModule.None:
                default:
                    break;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // MODULE RENDERERS — each builds geometry for one tile slot
        // ═══════════════════════════════════════════════════════════════

        private void RenderHelm(Transform parent, float x, float deckY, int idx)
        {
            // Helm wheel
            var wheel = CreatePrimitive($"Helm_{idx}", parent);
            wheel.transform.localPosition = new Vector3(x, deckY + 0.10f, 0f);
            wheel.transform.localScale = new Vector3(0.14f, 0.14f, 0.02f);
            SetColor(wheel, BrassColor);

            // Tiller post
            var post = CreatePrimitive($"HelmPost_{idx}", parent);
            post.transform.localPosition = new Vector3(x, deckY + 0.04f, 0f);
            post.transform.localScale = new Vector3(0.03f, 0.08f, 0.03f);
            SetColor(post, MastColor);

            // Navigation box (chart room)
            var nav = CreatePrimitive($"NavBox_{idx}", parent);
            nav.transform.localPosition = new Vector3(x + 0.15f, deckY + 0.05f, 0f);
            nav.transform.localScale = new Vector3(0.12f, 0.10f, 0.10f);
            SetColor(nav, CabinColor);
        }

        private void RenderMast(Transform parent, float x, float deckY, float mastH, int idx)
        {
            if (mastH <= 0f) mastH = 0.40f;

            // Mast pole
            var mast = CreatePrimitive($"Mast_{idx}", parent);
            mast.transform.localPosition = new Vector3(x, deckY + mastH * 0.5f, 0f);
            mast.transform.localScale = new Vector3(0.03f, mastH, 0.03f);
            SetColor(mast, MastColor);

            // Sail
            float sailW = Cell * 0.55f;
            float sailH = mastH * 0.6f;
            var sail = CreatePrimitive($"Sail_{idx}", parent);
            sail.transform.localPosition = new Vector3(x, deckY + mastH * 0.55f, -Cell * 0.05f);
            sail.transform.localScale = new Vector3(sailW, sailH, 0.01f);
            SetColor(sail, SailColor);

            // Yard (horizontal spar)
            var yard = CreatePrimitive($"Yard_{idx}", parent);
            yard.transform.localPosition = new Vector3(x, deckY + mastH * 0.82f, 0f);
            yard.transform.localScale = new Vector3(sailW * 0.9f, 0.015f, 0.015f);
            SetColor(yard, MastColor);

            // Shroud lines (port & starboard)
            var shroudP = CreatePrimitive($"Shroud_P_{idx}", parent);
            shroudP.transform.localPosition = new Vector3(x - Cell * 0.25f, deckY + mastH * 0.35f, 0f);
            shroudP.transform.localScale = new Vector3(0.008f, mastH * 0.7f, 0.008f);
            shroudP.transform.localRotation = Quaternion.Euler(0f, 90f, 8f);
            SetColor(shroudP, RopeColor);

            var shroudS = CreatePrimitive($"Shroud_S_{idx}", parent);
            shroudS.transform.localPosition = new Vector3(x + Cell * 0.25f, deckY + mastH * 0.35f, 0f);
            shroudS.transform.localScale = new Vector3(0.008f, mastH * 0.7f, 0.008f);
            shroudS.transform.localRotation = Quaternion.Euler(0f, 90f, -8f);
            SetColor(shroudS, RopeColor);
        }

        private void RenderCannon(Transform parent, float x, float deckY, int idx)
        {
            // Cannon barrel (port side)
            var barrelP = CreatePrimitive($"Cannon_P_{idx}", parent);
            barrelP.transform.localPosition = new Vector3(x, deckY + 0.03f, -Cell * 0.16f);
            barrelP.transform.localScale = new Vector3(0.10f, 0.04f, 0.04f);
            SetColor(barrelP, CannonColor);

            // Cannon barrel (starboard side)
            var barrelS = CreatePrimitive($"Cannon_S_{idx}", parent);
            barrelS.transform.localPosition = new Vector3(x, deckY + 0.03f, Cell * 0.16f);
            barrelS.transform.localScale = new Vector3(0.10f, 0.04f, 0.04f);
            SetColor(barrelS, CannonColor);

            // Cannon carriage (small wooden mount)
            var carriageP = CreatePrimitive($"CannonMount_P_{idx}", parent);
            carriageP.transform.localPosition = new Vector3(x, deckY + 0.015f, -Cell * 0.14f);
            carriageP.transform.localScale = new Vector3(0.06f, 0.03f, 0.06f);
            SetColor(carriageP, DeckColor);

            var carriageS = CreatePrimitive($"CannonMount_S_{idx}", parent);
            carriageS.transform.localPosition = new Vector3(x, deckY + 0.015f, Cell * 0.14f);
            carriageS.transform.localScale = new Vector3(0.06f, 0.03f, 0.06f);
            SetColor(carriageS, DeckColor);
        }

        private void RenderCrane(Transform parent, float x, float deckY, int idx)
        {
            // Crane mast (vertical)
            var post = CreatePrimitive($"CranePost_{idx}", parent);
            post.transform.localPosition = new Vector3(x, deckY + 0.18f, 0f);
            post.transform.localScale = new Vector3(0.04f, 0.36f, 0.04f);
            SetColor(post, IronColor);

            // Crane arm (horizontal boom)
            var arm = CreatePrimitive($"CraneArm_{idx}", parent);
            arm.transform.localPosition = new Vector3(x + 0.12f, deckY + 0.34f, 0f);
            arm.transform.localScale = new Vector3(0.28f, 0.025f, 0.025f);
            SetColor(arm, IronColor);

            // Crane rope (hanging line)
            var rope = CreatePrimitive($"CraneRope_{idx}", parent);
            rope.transform.localPosition = new Vector3(x + 0.22f, deckY + 0.22f, 0f);
            rope.transform.localScale = new Vector3(0.008f, 0.20f, 0.008f);
            SetColor(rope, RopeColor);

            // Hook
            var hook = CreatePrimitive($"CraneHook_{idx}", parent);
            hook.transform.localPosition = new Vector3(x + 0.22f, deckY + 0.10f, 0f);
            hook.transform.localScale = new Vector3(0.02f, 0.03f, 0.02f);
            SetColor(hook, IronColor);
        }

        private void RenderOars(Transform parent, float x, float deckY, int idx)
        {
            // Port oar
            var oarP = CreatePrimitive($"Oar_P_{idx}", parent);
            oarP.transform.localPosition = new Vector3(x, deckY + 0.02f, -Cell * 0.20f);
            oarP.transform.localScale = new Vector3(0.03f, 0.015f, 0.22f);
            oarP.transform.localRotation = Quaternion.Euler(0f, 90f, -15f);
            SetColor(oarP, MastColor);

            // Starboard oar
            var oarS = CreatePrimitive($"Oar_S_{idx}", parent);
            oarS.transform.localPosition = new Vector3(x, deckY + 0.02f, Cell * 0.20f);
            oarS.transform.localScale = new Vector3(0.03f, 0.015f, 0.22f);
            oarS.transform.localRotation = Quaternion.Euler(0f, 90f, 15f);
            SetColor(oarS, MastColor);

            // Thwart (seat bench)
            var bench = CreatePrimitive($"Thwart_{idx}", parent);
            bench.transform.localPosition = new Vector3(x, deckY + 0.02f, 0f);
            bench.transform.localScale = new Vector3(0.06f, 0.02f, Cell * 0.28f);
            SetColor(bench, DeckColor);
        }

        private void RenderCargoHatch(Transform parent, float x, float deckY, int idx)
        {
            // Hatch frame
            var frame = CreatePrimitive($"HatchFrame_{idx}", parent);
            frame.transform.localPosition = new Vector3(x, deckY + 0.01f, 0f);
            frame.transform.localScale = new Vector3(Cell * 0.50f, 0.02f, Cell * 0.30f);
            SetColor(frame, DeckColor);

            // Hatch cover (slightly darker, offset up)
            var cover = CreatePrimitive($"HatchCover_{idx}", parent);
            cover.transform.localPosition = new Vector3(x, deckY + 0.03f, 0f);
            cover.transform.localScale = new Vector3(Cell * 0.44f, 0.02f, Cell * 0.24f);
            SetColor(cover, CargoColor);

            // Cargo sack visible through grate
            var sack = CreatePrimitive($"CargoSack_{idx}", parent);
            sack.transform.localPosition = new Vector3(x - 0.05f, deckY + 0.06f, 0f);
            sack.transform.localScale = new Vector3(0.08f, 0.06f, 0.08f);
            SetColor(sack, CargoColor);
        }

        private void RenderCabin(Transform parent, float x, float deckY, int idx)
        {
            // Cabin structure (small raised house on deck)
            var cabin = CreatePrimitive($"Cabin_{idx}", parent);
            cabin.transform.localPosition = new Vector3(x, deckY + 0.09f, 0f);
            cabin.transform.localScale = new Vector3(Cell * 0.55f, 0.18f, Cell * 0.30f);
            SetColor(cabin, CabinColor);

            // Cabin roof (slightly wider, angled look)
            var roof = CreatePrimitive($"CabinRoof_{idx}", parent);
            roof.transform.localPosition = new Vector3(x, deckY + 0.19f, 0f);
            roof.transform.localScale = new Vector3(Cell * 0.60f, 0.02f, Cell * 0.34f);
            SetColor(roof, HullColor);

            // Door (thin dark rectangle on front face)
            var door = CreatePrimitive($"CabinDoor_{idx}", parent);
            door.transform.localPosition = new Vector3(x, deckY + 0.07f, -Cell * 0.16f);
            door.transform.localScale = new Vector3(0.06f, 0.12f, 0.005f);
            SetColor(door, HullColor);
        }

        private void RenderFishingRig(Transform parent, float x, float deckY, int idx)
        {
            // Fishing pole (angled outward from deck)
            var pole = CreatePrimitive($"FishPole_{idx}", parent);
            pole.transform.localPosition = new Vector3(x + 0.10f, deckY + 0.14f, -Cell * 0.10f);
            pole.transform.localScale = new Vector3(0.015f, 0.30f, 0.015f);
            pole.transform.localRotation = Quaternion.Euler(0f, 90f, -30f);
            SetColor(pole, MastColor);

            // Fishing line (thin drooping line)
            var line = CreatePrimitive($"FishLine_{idx}", parent);
            line.transform.localPosition = new Vector3(x + 0.22f, deckY + 0.10f, -Cell * 0.10f);
            line.transform.localScale = new Vector3(0.004f, 0.18f, 0.004f);
            SetColor(line, RopeColor);

            // Tackle box
            var box = CreatePrimitive($"TackleBox_{idx}", parent);
            box.transform.localPosition = new Vector3(x - 0.08f, deckY + 0.03f, 0f);
            box.transform.localScale = new Vector3(0.08f, 0.06f, 0.06f);
            SetColor(box, DeckColor);
        }

        private void RenderLookout(Transform parent, float x, float deckY, float mastH, int idx)
        {
            if (mastH <= 0f) mastH = 0.40f;

            // Tall pole (taller than a regular mast for visibility)
            var pole = CreatePrimitive($"LookoutPole_{idx}", parent);
            float poleH = mastH * 1.1f;
            pole.transform.localPosition = new Vector3(x, deckY + poleH * 0.5f, 0f);
            pole.transform.localScale = new Vector3(0.025f, poleH, 0.025f);
            SetColor(pole, MastColor);

            // Crow's nest platform (small disc at top)
            var nest = CreatePrimitive($"CrowsNest_{idx}", parent);
            nest.transform.localPosition = new Vector3(x, deckY + poleH * 0.88f, 0f);
            nest.transform.localScale = new Vector3(0.14f, 0.02f, 0.14f);
            SetColor(nest, DeckColor);

            // Railing around crow's nest
            var rail = CreatePrimitive($"NestRail_{idx}", parent);
            rail.transform.localPosition = new Vector3(x, deckY + poleH * 0.90f, 0f);
            rail.transform.localScale = new Vector3(0.16f, 0.03f, 0.005f);
            SetColor(rail, DeckColor);

            // Shroud ladder to climb up
            var ladder = CreatePrimitive($"LookoutShroud_{idx}", parent);
            ladder.transform.localPosition = new Vector3(x - 0.06f, deckY + poleH * 0.40f, 0f);
            ladder.transform.localScale = new Vector3(0.006f, poleH * 0.75f, 0.006f);
            ladder.transform.localRotation = Quaternion.Euler(0f, 90f, 5f);
            SetColor(ladder, RopeColor);
        }

        // ═══════════════════════════════════════════════════════════════
        // SHIP VISUAL PROPERTIES
        // ═══════════════════════════════════════════════════════════════

        private static float GetHullHeight(int width)
        {
            switch (Mathf.Clamp(width, 1, 5))
            {
                case 1: return 0.12f;
                case 2: return 0.20f;
                case 3: return 0.28f;
                case 4: return 0.36f;
                default: return 0.44f;
            }
        }

        private static float GetMastHeight(int width)
        {
            switch (Mathf.Clamp(width, 1, 5))
            {
                case 1: return 0.25f;
                case 2: return 0.40f;
                case 3: return 0.60f;
                case 4: return 0.76f;
                default: return 0.90f;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // HELPERS
        // ═══════════════════════════════════════════════════════════════

        private static GameObject CreatePrimitive(string name, Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
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
