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
    ///   Z =  0          — road surface / ground line / trees at ground level
    ///   Sewer cubes sit below Y=0 at Z=0
    ///
    /// Multi-tile buildings: the renderer reads _buildingWidth from the grid
    /// and draws one wide cube for the entire building footprint.
    ///
    /// Trees: rendered as simple pine-tree shapes (trunk + cone canopy) on
    /// empty tiles at Z=0.
    /// </summary>
    public class CityRenderer : MonoBehaviour, IQualityResponsive
    {
        private CityGrid _city;

        // Per-slot game objects
        private GameObject[] _roadObjects;     // road blocks at Z=0
        private GameObject[] _sewerObjects;    // sewer cubes below ground
        private GameObject _groundLine;

        // Dynamic building objects (destroyed & recreated on change)
        private GameObject _buildingParent;
        private GameObject _treeParent;

        private bool _dirty = true;

        public const float CellSize = 0.5f;
        private const float BuildingZ = 0.5f;  // buildings sit behind the road
        private const float RoadH = 0.15f;     // road block height

        // ── Color palette ───────────────────────────────────────

        private static readonly Color RoadColor      = new Color(0.12f, 0.10f, 0.08f);  // cobblestone
        private static readonly Color HouseColor     = new Color(0.6f,  0.45f, 0.25f);  // warm wood
        private static readonly Color ChapelColor    = new Color(0.9f,  0.8f,  0.3f);   // golden glow
        private static readonly Color WorkshopColor  = new Color(0.5f,  0.35f, 0.2f);   // dark timber
        private static readonly Color FarmColor      = new Color(0.2f,  0.5f,  0.15f);  // green crops
        private static readonly Color MarketColor    = new Color(0.7f,  0.3f,  0.15f);  // terracotta
        private static readonly Color FountainColor  = new Color(0.2f,  0.5f,  0.8f);   // water blue
        private static readonly Color SewerColor     = new Color(0.18f, 0.14f, 0.08f);  // murky brown
        private static readonly Color SewerDenColor  = new Color(0.30f, 0.10f, 0.10f);  // crimson grime
        private static readonly Color GroundColor    = new Color(0.10f, 0.08f, 0.05f);  // earth divider

        // Tree colors
        private static readonly Color TrunkColor     = new Color(0.35f, 0.22f, 0.10f);  // bark brown
        private static readonly Color CanopyColor    = new Color(0.08f, 0.30f, 0.08f);  // deep pine green

        // ── Heights per building type ───────────────────────────

        private const float HouseH     = 0.7f;
        private const float ChapelH    = 1.0f;
        private const float WorkshopH  = 0.6f;
        private const float FarmH      = 0.3f;
        private const float MarketH    = 0.5f;
        private const float FountainH  = 0.4f;
        private const float SewerH     = 0.4f;
        private const float SewerDenH  = 0.5f;

        // Tree dimensions
        private const float TreeTrunkH = 0.15f;
        private const float TreeTrunkW = 0.08f;
        private const float TreeCanopyH = 0.4f;
        private const float TreeCanopyW = 0.25f;

        public void Initialize(CityGrid city)
        {
            _city = city;

            // Road blocks at Z=0 (one per slot)
            _roadObjects = new GameObject[_city.Width];
            for (int i = 0; i < _city.Width; i++)
                _roadObjects[i] = CreateCell($"Road_{i}");

            // Sewer tiles below ground
            _sewerObjects = new GameObject[_city.Width];
            for (int i = 0; i < _city.Width; i++)
                _sewerObjects[i] = CreateCell($"Sewer_{i}");

            // Containers for dynamic objects
            _buildingParent = new GameObject("Buildings");
            _buildingParent.transform.SetParent(transform, false);
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
            RenderRoadAndSewers();
            RenderBuildings();
            RenderTrees();
        }

        private void RenderRoadAndSewers()
        {
            for (int i = 0; i < _city.Width; i++)
            {
                float x = i * CellSize + CellSize * 0.5f;

                // Road block — always visible at Z=0
                var roadGO = _roadObjects[i];
                roadGO.transform.localPosition = new Vector3(x, RoadH * 0.5f, 0f);
                roadGO.transform.localScale = new Vector3(CellSize * 0.95f, RoadH, CellSize * 0.95f);
                SetColor(roadGO, RoadColor);

                // Sewer below ground
                var sewType = _city.GetSewer(i);
                var sewGO = _sewerObjects[i];
                GetSewerVisual(sewType, out Color sewColor, out float sewH);
                sewGO.transform.localPosition = new Vector3(x, -sewH * 0.5f, 0f);
                sewGO.transform.localScale = new Vector3(CellSize * 0.9f, sewH, CellSize * 0.9f);
                SetColor(sewGO, sewColor);
            }
        }

        private void RenderBuildings()
        {
            // Destroy old building objects
            for (int c = _buildingParent.transform.childCount - 1; c >= 0; c--)
                Destroy(_buildingParent.transform.GetChild(c).gameObject);

            for (int i = 0; i < _city.Width; i++)
            {
                int owner = _city.GetOwner(i);
                if (owner != i) continue; // skip trailing tiles

                var type = _city.GetSurface(i);
                if (type == CellType.Empty || type == CellType.Tree) continue;

                int bw = _city.GetBuildingWidth(i);
                if (bw < 1) bw = 1;

                GetBuildingVisual(type, out Color color, out float height);

                float totalW = bw * CellSize;
                float x = i * CellSize + totalW * 0.5f;

                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = $"Bldg_{type}_{i}";
                go.transform.SetParent(_buildingParent.transform, false);
                go.transform.localPosition = new Vector3(x, RoadH + height * 0.5f, BuildingZ);
                go.transform.localScale = new Vector3(totalW * 0.92f, height, CellSize * 0.9f);
                var col = go.GetComponent<Collider>();
                if (col != null) Destroy(col);
                SetColor(go, color);
            }
        }

        private void RenderTrees()
        {
            // Destroy old tree objects
            for (int c = _treeParent.transform.childCount - 1; c >= 0; c--)
                Destroy(_treeParent.transform.GetChild(c).gameObject);

            for (int i = 0; i < _city.Width; i++)
            {
                if (_city.GetSurface(i) != CellType.Tree) continue;

                float x = i * CellSize + CellSize * 0.5f;

                // Trunk — thin vertical box
                var trunk = GameObject.CreatePrimitive(PrimitiveType.Cube);
                trunk.name = $"Trunk_{i}";
                trunk.transform.SetParent(_treeParent.transform, false);
                trunk.transform.localPosition = new Vector3(x, RoadH + TreeTrunkH * 0.5f, BuildingZ * 0.5f);
                trunk.transform.localScale = new Vector3(TreeTrunkW, TreeTrunkH, TreeTrunkW);
                var tc = trunk.GetComponent<Collider>();
                if (tc != null) Destroy(tc);
                SetColor(trunk, TrunkColor);

                // Canopy — cone-like shape (use cube scaled narrow at top via non-uniform scale)
                // Simple approach: a taller box that tapers — or just use a cube for pine silhouette
                var canopy = GameObject.CreatePrimitive(PrimitiveType.Cube);
                canopy.name = $"Canopy_{i}";
                canopy.transform.SetParent(_treeParent.transform, false);
                canopy.transform.localPosition = new Vector3(x, RoadH + TreeTrunkH + TreeCanopyH * 0.5f, BuildingZ * 0.5f);
                canopy.transform.localScale = new Vector3(TreeCanopyW, TreeCanopyH, TreeCanopyW);
                var cc = canopy.GetComponent<Collider>();
                if (cc != null) Destroy(cc);
                SetColor(canopy, CanopyColor);
            }
        }

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

        private static void GetSewerVisual(CellType type, out Color color, out float height)
        {
            switch (type)
            {
                case CellType.Sewer:    color = SewerColor;    height = SewerH;    return;
                case CellType.SewerDen: color = SewerDenColor; height = SewerDenH; return;
                default:                color = SewerColor;    height = SewerH;    return;
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
