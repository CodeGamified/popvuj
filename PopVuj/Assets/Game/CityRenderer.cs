// Copyright CodeGamified 2025-2026
// MIT License — PopVuj
using UnityEngine;
using CodeGamified.Quality;

namespace PopVuj.Game
{
    /// <summary>
    /// 2.5D side-view city renderer — linear strip on the XY plane.
    ///
    /// Three depth lanes:
    ///   Z = +BuildingZ  — buildings sit BEHIND the road
    ///   Z =  0          — road surface / ground line / trees
    ///   Sewers below Y=0 — DERIVED from buildings above.
    ///                      Depth proportional to building height.
    ///                      Type determined by building archetype.
    ///
    /// Multi-tile buildings render as one wide cube.
    /// Multi-tile sewers mirror the footprint below ground.
    /// </summary>
    public class CityRenderer : MonoBehaviour, IQualityResponsive
    {
        private CityGrid _city;

        // Per-slot road blocks
        private GameObject[] _roadObjects;
        private GameObject _groundLine;

        // Dynamic containers (destroyed & recreated on change)
        private GameObject _buildingParent;
        private GameObject _sewerParent;
        private GameObject _treeParent;

        private bool _dirty = true;

        public const float CellSize = 0.5f;
        private const float BuildingZ = 0.5f;
        private const float RoadH = 0.15f;

        // ── Color palette ───────────────────────────────────────

        private static readonly Color RoadColor      = new Color(0.12f, 0.10f, 0.08f);
        private static readonly Color HouseColor     = new Color(0.6f,  0.45f, 0.25f);
        private static readonly Color ChapelColor    = new Color(0.9f,  0.8f,  0.3f);
        private static readonly Color WorkshopColor  = new Color(0.5f,  0.35f, 0.2f);
        private static readonly Color FarmColor      = new Color(0.2f,  0.5f,  0.15f);
        private static readonly Color MarketColor    = new Color(0.7f,  0.3f,  0.15f);
        private static readonly Color FountainColor  = new Color(0.2f,  0.5f,  0.8f);
        private static readonly Color GroundColor    = new Color(0.10f, 0.08f, 0.05f);

        // Sewer archetype colors — one per SewerType
        private static readonly Color DrainColor     = new Color(0.14f, 0.12f, 0.08f);  // thin pipe grey-brown
        private static readonly Color DenColor       = new Color(0.30f, 0.12f, 0.10f);  // crimson den
        private static readonly Color CryptColor     = new Color(0.20f, 0.18f, 0.30f);  // dark purple stone
        private static readonly Color TunnelColor    = new Color(0.18f, 0.15f, 0.10f);  // utility brown
        private static readonly Color CisternColor   = new Color(0.10f, 0.22f, 0.35f);  // deep water blue
        private static readonly Color BazaarColor    = new Color(0.28f, 0.15f, 0.08f);  // smuggler amber

        // Tree colors
        private static readonly Color TrunkColor     = new Color(0.35f, 0.22f, 0.10f);
        private static readonly Color CanopyColor    = new Color(0.08f, 0.30f, 0.08f);

        // ── Building heights ────────────────────────────────────

        private const float HouseH     = 0.7f;
        private const float ChapelH    = 1.0f;
        private const float WorkshopH  = 0.6f;
        private const float FarmH      = 0.3f;
        private const float MarketH    = 0.5f;
        private const float FountainH  = 0.4f;

        // Tree dimensions
        private const float TreeTrunkH = 0.15f;
        private const float TreeTrunkW = 0.08f;
        private const float TreeCanopyH = 0.4f;
        private const float TreeCanopyW = 0.25f;

        public void Initialize(CityGrid city)
        {
            _city = city;

            _roadObjects = new GameObject[_city.Width];
            for (int i = 0; i < _city.Width; i++)
                _roadObjects[i] = CreateCell($"Road_{i}");

            _buildingParent = new GameObject("Buildings");
            _buildingParent.transform.SetParent(transform, false);
            _sewerParent = new GameObject("Sewers");
            _sewerParent.transform.SetParent(transform, false);
            _treeParent = new GameObject("Trees");
            _treeParent.transform.SetParent(transform, false);

            BuildGroundLine();

            _city.OnGridChanged += () => _dirty = true;
            QualityBridge.Register(this);
        }

        private void OnDisable() => QualityBridge.Unregister(this);
        public void OnQualityChanged(QualityTier tier) => _dirty = true;

        private void LateUpdate()
        {
            if (!_dirty) return;
            _dirty = false;
            Render();
        }

        public void MarkDirty() => _dirty = true;
        public float CityWorldWidth => _city.Width * CellSize;

        // ═══════════════════════════════════════════════════════════════
        // RENDER
        // ═══════════════════════════════════════════════════════════════

        private void Render()
        {
            RenderRoad();
            RenderBuildings();
            RenderSewers();
            RenderTrees();
        }

        private void RenderRoad()
        {
            for (int i = 0; i < _city.Width; i++)
            {
                float x = i * CellSize + CellSize * 0.5f;
                var roadGO = _roadObjects[i];
                roadGO.transform.localPosition = new Vector3(x, RoadH * 0.5f, 0f);
                roadGO.transform.localScale = new Vector3(CellSize * 0.95f, RoadH, CellSize * 0.95f);
                SetColor(roadGO, RoadColor);
            }
        }

        private void RenderBuildings()
        {
            ClearChildren(_buildingParent);

            for (int i = 0; i < _city.Width; i++)
            {
                int owner = _city.GetOwner(i);
                if (owner != i) continue;

                var type = _city.GetSurface(i);
                if (type == CellType.Empty || type == CellType.Tree) continue;

                int bw = _city.GetBuildingWidth(i);
                if (bw < 1) bw = 1;

                GetBuildingVisual(type, out Color color, out float height);

                float totalW = bw * CellSize;
                float x = i * CellSize + totalW * 0.5f;

                var go = CreatePrimitive($"Bldg_{type}_{i}", _buildingParent.transform);
                go.transform.localPosition = new Vector3(x, RoadH + height * 0.5f, BuildingZ);
                go.transform.localScale = new Vector3(totalW * 0.92f, height, CellSize * 0.9f);
                SetColor(go, color);
            }
        }

        /// <summary>
        /// Render sewers — derived from the building above each slot.
        /// Same footprint as the building, depth proportional to building height.
        /// </summary>
        private void RenderSewers()
        {
            ClearChildren(_sewerParent);

            for (int i = 0; i < _city.Width; i++)
            {
                int owner = _city.GetOwner(i);
                if (owner != i) continue;

                var type = _city.GetSurface(i);
                if (type == CellType.Empty || type == CellType.Tree) continue;

                int bw = _city.GetBuildingWidth(i);
                if (bw < 1) bw = 1;

                float depth = _city.GetSewerDepth(i);
                if (depth <= 0.01f) continue; // no sewer (e.g. farm)

                var sewType = _city.GetSewerAt(i);
                Color sewColor = GetSewerColor(sewType);

                float totalW = bw * CellSize;
                float x = i * CellSize + totalW * 0.5f;

                var go = CreatePrimitive($"Sewer_{sewType}_{i}", _sewerParent.transform);
                go.transform.localPosition = new Vector3(x, -depth * 0.5f, BuildingZ);
                go.transform.localScale = new Vector3(totalW * 0.88f, depth, CellSize * 0.85f);
                SetColor(go, sewColor);
            }
        }

        private void RenderTrees()
        {
            ClearChildren(_treeParent);

            for (int i = 0; i < _city.Width; i++)
            {
                if (_city.GetSurface(i) != CellType.Tree) continue;

                float x = i * CellSize + CellSize * 0.5f;

                var trunk = CreatePrimitive($"Trunk_{i}", _treeParent.transform);
                trunk.transform.localPosition = new Vector3(x, RoadH + TreeTrunkH * 0.5f, BuildingZ * 0.5f);
                trunk.transform.localScale = new Vector3(TreeTrunkW, TreeTrunkH, TreeTrunkW);
                SetColor(trunk, TrunkColor);

                var canopy = CreatePrimitive($"Canopy_{i}", _treeParent.transform);
                canopy.transform.localPosition = new Vector3(x, RoadH + TreeTrunkH + TreeCanopyH * 0.5f, BuildingZ * 0.5f);
                canopy.transform.localScale = new Vector3(TreeCanopyW, TreeCanopyH, TreeCanopyW);
                SetColor(canopy, CanopyColor);
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // VISUAL LOOKUPS
        // ═══════════════════════════════════════════════════════════════

        private static void GetBuildingVisual(CellType type, out Color color, out float height)
        {
            switch (type)
            {
                case CellType.House:    color = HouseColor;    height = HouseH;    return;
                case CellType.Chapel:   color = ChapelColor;   height = ChapelH;   return;
                case CellType.Workshop: color = WorkshopColor; height = WorkshopH; return;
                case CellType.Farm:     color = FarmColor;     height = FarmH;     return;
                case CellType.Market:   color = MarketColor;   height = MarketH;   return;
                case CellType.Fountain: color = FountainColor; height = FountainH; return;
                default:                color = HouseColor;    height = HouseH;    return;
            }
        }

        private static Color GetSewerColor(SewerType type)
        {
            switch (type)
            {
                case SewerType.Drain:   return DrainColor;
                case SewerType.Den:     return DenColor;
                case SewerType.Crypt:   return CryptColor;
                case SewerType.Tunnel:  return TunnelColor;
                case SewerType.Cistern: return CisternColor;
                case SewerType.Bazaar:  return BazaarColor;
                default:                return DrainColor;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // HELPERS
        // ═══════════════════════════════════════════════════════════════

        private void BuildGroundLine()
        {
            _groundLine = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _groundLine.name = "GroundLine";
            _groundLine.transform.SetParent(transform, false);
            float cityW = _city.Width * CellSize;
            float lineHeight = CellSize * 0.08f;
            _groundLine.transform.localPosition = new Vector3(cityW * 0.5f, 0f, 0f);
            _groundLine.transform.localScale = new Vector3(cityW, lineHeight, CellSize);
            var col = _groundLine.GetComponent<Collider>();
            if (col != null) Destroy(col);
            SetColor(_groundLine, GroundColor);
        }

        private GameObject CreateCell(string name)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.one * (CellSize * 0.9f);
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            return go;
        }

        private GameObject CreatePrimitive(string name, Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            return go;
        }

        private static void ClearChildren(GameObject parent)
        {
            for (int c = parent.transform.childCount - 1; c >= 0; c--)
                Destroy(parent.transform.GetChild(c).gameObject);
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
