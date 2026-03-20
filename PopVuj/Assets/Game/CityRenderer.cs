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
        private GameObject _waterParent;

        private bool _dirty = true;

        public const float CellSize = 1.0f;
        private const float BuildingZ = 1.0f;
        private const float RoadH = 0.3f;

        // ── Color palette ───────────────────────────────────────

        private static readonly Color RoadColor      = new Color(0.12f, 0.10f, 0.08f);
        private static readonly Color HouseColor     = new Color(0.6f,  0.45f, 0.25f, 0.10f);
        private static readonly Color ChapelColor    = new Color(0.9f,  0.8f,  0.3f,  0.10f);
        private static readonly Color WorkshopColor  = new Color(0.5f,  0.35f, 0.2f,  0.10f);
        private static readonly Color FarmColor      = new Color(0.2f,  0.5f,  0.15f, 0.10f);
        private static readonly Color MarketColor    = new Color(0.7f,  0.3f,  0.15f, 0.10f);
        private static readonly Color FountainColor  = new Color(0.2f,  0.5f,  0.8f,  0.10f);
        private static readonly Color ShipyardColor  = new Color(0.45f, 0.30f, 0.15f, 0.10f);
        private static readonly Color PierColor      = new Color(0.50f, 0.35f, 0.18f, 0.10f);
        private static readonly Color WarehouseColor = new Color(0.35f, 0.30f, 0.40f, 0.10f);
        private static readonly Color GroundColor    = new Color(0.10f, 0.08f, 0.05f);

        // Sewer archetype colors — one per SewerType
        private static readonly Color DrainColor     = new Color(0.14f, 0.12f, 0.08f, 0.10f);  // thin pipe grey-brown
        private static readonly Color DenColor       = new Color(0.30f, 0.12f, 0.10f, 0.10f);  // crimson den
        private static readonly Color CryptColor     = new Color(0.20f, 0.18f, 0.30f, 0.10f);  // dark purple stone
        private static readonly Color TunnelColor    = new Color(0.18f, 0.15f, 0.10f, 0.10f);  // utility brown
        private static readonly Color CisternColor   = new Color(0.10f, 0.22f, 0.35f, 0.10f);  // deep water blue
        private static readonly Color BazaarColor    = new Color(0.28f, 0.15f, 0.08f, 0.10f);  // smuggler amber
        private static readonly Color DrydockColor   = new Color(0.20f, 0.25f, 0.35f, 0.10f);  // flooded blue-grey
        private static readonly Color VaultColor     = new Color(0.25f, 0.20f, 0.30f, 0.10f);  // dark purple vault

        // Water surface (Gerstner wave simulation)
        private WaterSurface _waterSurface;

        // Tree colors
        private static readonly Color TrunkColor     = new Color(0.35f, 0.22f, 0.10f);
        private static readonly Color CanopyColor    = new Color(0.08f, 0.30f, 0.08f);

        // ── Building heights ────────────────────────────────────

        private const float HouseH     = 2f;
        private const float ChapelH    = 3f;
        private const float WorkshopH  = 2f;
        private const float FarmH      = 1f;
        private const float MarketH    = 1f;
        private const float FountainH  = 1f;
        private const float ShipyardH  = 2f;
        private const float PierH      = 1f;
        private const float WarehouseH = 3f;

        // Tree dimensions
        private const float TreeTrunkH = 0.3f;
        private const float TreeTrunkW = 0.16f;
        private const float TreeCanopyH = 0.8f;
        private const float TreeCanopyW = 0.5f;

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
            _waterParent = new GameObject("Water");
            _waterParent.transform.SetParent(transform, false);

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
            RenderWater();
        }

        private void RenderRoad()
        {
            for (int i = 0; i < _city.Width; i++)
            {
                float x = i * CellSize + CellSize * 0.5f;
                var roadGO = _roadObjects[i];

                // Pier cells are over water — no road beneath them
                var type = _city.GetBuildingAt(i);
                if (type == CellType.Pier)
                {
                    roadGO.transform.localScale = Vector3.zero;
                    continue;
                }

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

        /// <summary>
        /// Configure Gerstner wave water surface beneath piers and extending
        /// to the right as open ocean past the last pier.
        /// </summary>
        private void RenderWater()
        {
            // Find the leftmost pier/crane slot (water starts here)
            int waterStart = _city.Width;
            for (int i = 0; i < _city.Width; i++)
            {
                var type = _city.GetBuildingAt(i);
                if (type == CellType.Pier)
                {
                    int origin = _city.GetOwner(i);
                    if (origin >= 0 && origin < waterStart)
                        waterStart = origin;
                }
            }

            if (waterStart >= _city.Width)
            {
                if (_waterSurface != null) _waterSurface.gameObject.SetActive(false);
                return;
            }

            // Create WaterSurface once, reuse across layout changes
            if (_waterSurface == null)
            {
                var go = new GameObject("WaterSurface");
                go.transform.SetParent(_waterParent.transform, false);
                _waterSurface = go.AddComponent<WaterSurface>();
            }

            _waterSurface.gameObject.SetActive(true);

            float waterStartX = waterStart * CellSize;
            float waterEndX = _city.Width * CellSize + CellSize * 4f;
            float surfaceY = RoadH * 0.3f;
            float bottomY = -0.8f;
            float waterZCenter = BuildingZ * 0.5f;
            float waterZExtent = BuildingZ + CellSize * 0.5f;

            _waterSurface.SetBounds(waterStartX, waterEndX, surfaceY, bottomY,
                                    waterZCenter, waterZExtent);
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
                case CellType.Shipyard:  color = ShipyardColor;  height = ShipyardH;  return;
                case CellType.Pier:      color = PierColor;      height = PierH;      return;
                case CellType.Warehouse: color = WarehouseColor; height = WarehouseH; return;
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
                case SewerType.Drydock: return DrydockColor;
                case SewerType.Vault:   return VaultColor;
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

            // Enable transparent rendering when alpha < 1
            if (color.a < 1f)
            {
                if (mat.HasProperty("_Surface"))
                    mat.SetFloat("_Surface", 1f); // 0=Opaque, 1=Transparent
                if (mat.HasProperty("_Blend"))
                    mat.SetFloat("_Blend", 0f);   // 0=Alpha blend
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
