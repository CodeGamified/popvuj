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
            if (!_dirty) return;
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

            var ships = _harbor.Ships;
            for (int i = 0; i < ships.Count; i++)
            {
                var ship = ships[i];

                // Building-state ships are rendered at the shipyard, not at anchorage
                if (ship.State == ShipState.Building)
                    continue;

                var shipGO = new GameObject($"Ship_{ship.Id}_{ship.Hull}");
                shipGO.transform.SetParent(_parent.transform, false);
                shipGO.transform.localPosition = new Vector3(ship.X, WaterY, ShipZ);

                RenderShip(ship, shipGO.transform);
            }
        }

        private void RenderShip(Ship ship, Transform parent)
        {
            float shipW = ship.Width * Cell;
            float y = 0f; // relative to parent (parent already at WaterY)

            // Hull — always present
            float hullH = GetHullHeight(ship.Width);
            float hullDraft = hullH * 0.3f; // how far below waterline
            var hull = CreatePrimitive("Hull", parent);
            hull.transform.localPosition = new Vector3(0f, y + hullH * 0.5f - hullDraft, 0f);
            hull.transform.localScale = new Vector3(shipW * 0.90f, hullH, Cell * 0.4f);
            SetColor(hull, HullColor);

            // Deck — flat on top of hull
            var deck = CreatePrimitive("Deck", parent);
            deck.transform.localPosition = new Vector3(0f, y + hullH - hullDraft + 0.02f, 0f);
            deck.transform.localScale = new Vector3(shipW * 0.85f, 0.04f, Cell * 0.38f);
            SetColor(deck, DeckColor);

            float deckY = y + hullH - hullDraft + 0.04f;

            // Masts + sails (scale with width)
            int mastCount = GetMastCount(ship.Width);
            float mastRegion = shipW * 0.6f;
            float mastSpacing = mastCount > 1 ? mastRegion / (mastCount - 1) : 0f;
            float mastH = GetMastHeight(ship.Width);

            for (int m = 0; m < mastCount; m++)
            {
                float mx = -mastRegion * 0.5f + (mastCount > 1 ? mastSpacing * m : 0f);

                // Mast pole
                var mast = CreatePrimitive($"Mast_{m}", parent);
                mast.transform.localPosition = new Vector3(mx, deckY + mastH * 0.5f, 0f);
                mast.transform.localScale = new Vector3(0.03f, mastH, 0.03f);
                SetColor(mast, MastColor);

                // Sail
                float sailW = shipW * 0.25f / mastCount + 0.04f;
                float sailH = mastH * 0.6f;
                var sail = CreatePrimitive($"Sail_{m}", parent);
                sail.transform.localPosition = new Vector3(mx, deckY + mastH * 0.55f, -Cell * 0.05f);
                sail.transform.localScale = new Vector3(sailW, sailH, 0.01f);
                SetColor(sail, SailColor);
            }

            // Cannons (Brigantine+ only)
            int gunCount = ship.GunnerSlots;
            if (gunCount > 0)
            {
                float gunRegion = shipW * 0.7f;
                float gunSpacing = gunRegion / gunCount;
                for (int g = 0; g < gunCount; g++)
                {
                    float gx = -gunRegion * 0.5f + gunSpacing * (g + 0.5f);
                    var cannon = CreatePrimitive($"Cannon_{g}", parent);
                    cannon.transform.localPosition = new Vector3(gx, deckY + 0.03f, -Cell * 0.18f);
                    cannon.transform.localScale = new Vector3(0.08f, 0.03f, 0.03f);
                    SetColor(cannon, CannonColor);
                }
            }
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

        private static int GetMastCount(int width)
        {
            switch (Mathf.Clamp(width, 1, 5))
            {
                case 1: return 0;   // canoe — no mast, just paddles
                case 2: return 1;   // sloop
                case 3: return 2;   // brigantine
                case 4: return 2;   // frigate
                default: return 3;  // ship of the line
            }
        }

        private static float GetMastHeight(int width)
        {
            switch (Mathf.Clamp(width, 1, 5))
            {
                case 1: return 0f;
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
