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

        // Sewer archetype colors — one per SewerType
        private static readonly Color DrainColor     = new Color(0.14f, 0.12f, 0.08f, 0.10f);  // thin pipe grey-brown
        private static readonly Color DenColor       = new Color(0.30f, 0.12f, 0.10f, 0.10f);  // crimson den
        private static readonly Color CryptColor     = new Color(0.20f, 0.18f, 0.30f, 0.10f);  // dark purple stone
        private static readonly Color TunnelColor    = new Color(0.18f, 0.15f, 0.10f, 0.10f);  // utility brown
        private static readonly Color CisternColor   = new Color(0.10f, 0.22f, 0.35f, 0.10f);  // deep water blue
        private static readonly Color BazaarColor    = new Color(0.28f, 0.15f, 0.08f, 0.10f);  // smuggler amber
        private static readonly Color DrydockColor   = new Color(0.20f, 0.25f, 0.35f, 0.10f);  // flooded blue-grey
        private static readonly Color VaultColor     = new Color(0.25f, 0.20f, 0.30f, 0.10f);  // dark purple vault
        private static readonly Color CanalColor     = new Color(0.16f, 0.14f, 0.12f, 0.10f);  // dim passage grey

        // Water surface (Gerstner wave simulation)
        private WaterSurface _waterSurface;

        // Tree colors
        private static readonly Color TrunkColor     = new Color(0.35f, 0.22f, 0.10f);
        private static readonly Color CanopyColor    = new Color(0.08f, 0.30f, 0.08f);
        private static readonly Color PineColor      = new Color(0.06f, 0.25f, 0.12f);
        private static readonly Color CypressColor   = new Color(0.04f, 0.22f, 0.06f);
        private static readonly Color PalmColor      = new Color(0.15f, 0.38f, 0.10f);
        private static readonly Color PalmTrunkColor = new Color(0.40f, 0.32f, 0.18f);

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
                var surfType = _city.GetSurface(i);
                int owner = _city.GetOwner(i);

                // Multi-tile buildings: only render at owner origin
                if (owner >= 0 && owner != i) continue;

                // Canal sewers beneath empty/tree (unowned slots)
                if ((surfType == CellType.Empty || surfType == CellType.Tree) && owner < 0)
                {
                    float depth = _city.GetSewerDepth(i);
                    if (depth <= 0.01f) continue;

                    float x = i * CellSize + CellSize * 0.5f;
                    var go = CreatePrimitive($"Sewer_Canal_{i}", _sewerParent.transform);
                    go.transform.localPosition = new Vector3(x, -depth * 0.5f, 0f);
                    go.transform.localScale = new Vector3(CellSize * 0.88f, depth, CellSize * 0.85f);
                    SetColor(go, CanalColor);
                    continue;
                }

                // Buildings: render sewer at owner origin
                if (surfType == CellType.Empty || surfType == CellType.Tree) continue;

                int bw = _city.GetBuildingWidth(i);
                if (bw < 1) bw = 1;

                float bldgDepth = _city.GetSewerDepth(i);
                if (bldgDepth <= 0.01f) continue;

                var sewType = _city.GetSewerAt(i);
                Color sewColor = GetSewerColor(sewType);

                float totalW = bw * CellSize;
                float bx = i * CellSize + totalW * 0.5f;

                var sgo = CreatePrimitive($"Sewer_{sewType}_{i}", _sewerParent.transform);
                sgo.transform.localPosition = new Vector3(bx, -bldgDepth * 0.5f, 0f);
                sgo.transform.localScale = new Vector3(totalW * 0.88f, bldgDepth, CellSize * 0.85f);
                SetColor(sgo, sewColor);
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // TREE FOLIAGE — species assigned by proximity to buildings
        //   Oak:     near farms / default — parent_leaves canopy
        //   Pine:    hash-selected 25% — underbranch layers + top
        //   Cypress: near chapel — columnar pillar + tip
        //   Palm:    near harbor — tall trunk + fronds
        // ═══════════════════════════════════════════════════════════════

        private enum TreeSpecies { Oak, Pine, Cypress, Palm }

        private static readonly string[] OakCanopyMeshes = { "parent_leaves", "parent_leaves_fruit" };
        private static readonly string[] OakCanopyTextures = {
            "9_organic/2_leaves/dark_deciduous_leaves_1",
            "9_organic/2_leaves/dark_deciduous_leaves_2",
            "9_organic/2_leaves/beech_tree_leaves_1",
            "9_organic/2_leaves/beech_tree_leaves_2",
        };
        private static readonly string[] PineUnderbranches = {
            "pine/underbranch", "pine/underbranch_2", "pine/underbranch_3", "pine/underbranch_4"
        };
        private static readonly string[] PineTops = { "pine/top_flat", "pine/top_short", "pine/top_tall" };
        private const string PineNeedleTex = "9_organic/2_leaves/pine/underbranch_upwards";
        private const string PineTopTex    = "9_organic/2_leaves/pine/top";
        private static readonly string[] CypressPillars = {
            "cypress/cypress_leaves_pillar_1", "cypress/cypress_leaves_pillar_2",
            "cypress/cypress_leaves_pillar_3", "cypress/cypress_leaves_pillar_4"
        };
        private static readonly string[] CypressPillarTextures = {
            "9_organic/2_leaves/cypress/cypress_leaves_pillar_1",
            "9_organic/2_leaves/cypress/cypress_leaves_pillar_2",
            "9_organic/2_leaves/cypress/cypress_leaves_pillar_3",
            "9_organic/2_leaves/cypress/cypress_leaves_pillar_4",
        };
        private const string CypressTipTex = "9_organic/2_leaves/cypress/cypress_leaves_top_1";
        private static readonly string[] PalmFronds = {
            "caribbean_royal_palm_straight_1", "caribbean_royal_palm_straight_2",
            "caribbean_royal_palm_old_straight_1", "caribbean_royal_palm_old_straight_2"
        };
        private static readonly string[] PalmFrondTextures = {
            "9_organic/2_leaves/caribbean_royal_palm_leaves_young",
            "9_organic/2_leaves/caribbean_royal_palm_leaves_old",
        };
        private const string OakTrunkTex   = "9_organic/1_wood/oak_log_branches";
        private const string PineTrunkTex  = "9_organic/1_wood/pine_log_branches";
        private const string PalmTrunkTex  = "9_organic/1_wood/palm_tree_trunk";

        private static int TreeHash(int slot, int salt = 0)
        {
            return (int)(((uint)slot * 2654435761u + (uint)salt * 1103515245u) & 0x7FFFFFFFu);
        }

        private TreeSpecies PickTreeSpecies(int slot)
        {
            for (int d = -3; d <= 3; d++)
            {
                int n = slot + d;
                if (n < 0 || n >= _city.Width) continue;
                var bt = _city.GetBuildingAt(n);
                if (bt == CellType.Pier || bt == CellType.Shipyard)
                    return TreeSpecies.Palm;
                if (bt == CellType.Chapel)
                    return TreeSpecies.Cypress;
            }
            return TreeHash(slot, 0) % 4 == 0 ? TreeSpecies.Pine : TreeSpecies.Oak;
        }

        private void RenderTrees()
        {
            ClearChildren(_treeParent);
            var tp = _treeParent.transform;

            for (int i = 0; i < _city.Width; i++)
            {
                if (_city.GetSurface(i) != CellType.Tree) continue;

                float x = i * CellSize + CellSize * 0.5f;
                float z = BuildingZ * 0.5f;

                bool ok;
                switch (PickTreeSpecies(i))
                {
                    case TreeSpecies.Cypress: ok = RenderCypressTree(i, x, z, tp); break;
                    case TreeSpecies.Pine:    ok = RenderPineTree(i, x, z, tp);    break;
                    case TreeSpecies.Palm:    ok = RenderPalmTree(i, x, z, tp);    break;
                    default:                  ok = RenderOakTree(i, x, z, tp);     break;
                }
                if (!ok) RenderFallbackTree(i, x, z, tp);
            }
        }

        private bool RenderOakTree(int i, float x, float z, Transform parent)
        {
            string mesh = OakCanopyMeshes[TreeHash(i, 1) % OakCanopyMeshes.Length];
            var canopy = ObjectScale.CreateMeshObject($"OakCanopy_{i}", mesh, parent);
            if (canopy == null) return false;

            var trunk = CreatePrimitive($"Trunk_{i}", parent);
            trunk.transform.localPosition = new Vector3(x, RoadH + TreeTrunkH * 0.5f, z);
            trunk.transform.localScale = new Vector3(TreeTrunkW, TreeTrunkH, TreeTrunkW);
            SetTexturedOpaque(trunk, OakTrunkTex, Color.white);

            float yRot = TreeHash(i, 10) % 360;
            canopy.transform.localPosition = new Vector3(x, RoadH + TreeTrunkH + 0.28f, z);
            canopy.transform.localScale = new Vector3(0.28f, 0.28f, 0.28f);
            canopy.transform.localRotation = Quaternion.Euler(0f, yRot, 0f);
            string leafTex = OakCanopyTextures[TreeHash(i, 11) % OakCanopyTextures.Length];
            SetTexturedCutout(canopy, leafTex, Color.white);
            return true;
        }

        private bool RenderPineTree(int i, float x, float z, Transform parent)
        {
            string branchMesh = PineUnderbranches[TreeHash(i, 2) % PineUnderbranches.Length];
            var branches = ObjectScale.CreateMeshObject($"PineBranch_{i}", branchMesh, parent);
            if (branches == null) return false;

            var trunk = CreatePrimitive($"Trunk_{i}", parent);
            trunk.transform.localPosition = new Vector3(x, RoadH + TreeTrunkH * 0.6f, z);
            trunk.transform.localScale = new Vector3(TreeTrunkW * 0.8f, TreeTrunkH * 1.2f, TreeTrunkW * 0.8f);
            SetTexturedOpaque(trunk, PineTrunkTex, Color.white);

            float yRot = TreeHash(i, 10) % 360;
            branches.transform.localPosition = new Vector3(x, RoadH + TreeTrunkH + 0.20f, z);
            branches.transform.localScale = new Vector3(0.25f, 0.22f, 0.25f);
            branches.transform.localRotation = Quaternion.Euler(0f, yRot, 0f);
            SetTexturedCutout(branches, PineNeedleTex, Color.white);

            string topMesh = PineTops[TreeHash(i, 3) % PineTops.Length];
            var top = ObjectScale.CreateMeshObject($"PineTop_{i}", topMesh, parent);
            if (top != null)
            {
                top.transform.localPosition = new Vector3(x, RoadH + TreeTrunkH + 0.55f, z);
                top.transform.localScale = new Vector3(0.20f, 0.20f, 0.20f);
                top.transform.localRotation = Quaternion.Euler(0f, yRot, 0f);
                SetTexturedCutout(top, PineTopTex, Color.white);
            }
            return true;
        }

        private bool RenderCypressTree(int i, float x, float z, Transform parent)
        {
            int pillarIdx = TreeHash(i, 4) % CypressPillars.Length;
            string pillarMesh = CypressPillars[pillarIdx];
            var pillar = ObjectScale.CreateMeshObject($"CypressPillar_{i}", pillarMesh, parent);
            if (pillar == null) return false;

            float yRot = TreeHash(i, 10) % 360;
            pillar.transform.localPosition = new Vector3(x, RoadH + 0.35f, z);
            pillar.transform.localScale = new Vector3(0.18f, 0.30f, 0.18f);
            pillar.transform.localRotation = Quaternion.Euler(0f, yRot, 0f);
            string pillarTex = CypressPillarTextures[pillarIdx % CypressPillarTextures.Length];
            SetTexturedCutout(pillar, pillarTex, Color.white);

            var tip = ObjectScale.CreateMeshObject($"CypressTip_{i}", "cypress/cypress_leaves_tip", parent);
            if (tip != null)
            {
                tip.transform.localPosition = new Vector3(x, RoadH + 0.80f, z);
                tip.transform.localScale = new Vector3(0.14f, 0.18f, 0.14f);
                tip.transform.localRotation = Quaternion.Euler(0f, yRot, 0f);
                SetTexturedCutout(tip, CypressTipTex, Color.white);
            }
            return true;
        }

        private bool RenderPalmTree(int i, float x, float z, Transform parent)
        {
            string frondMesh = PalmFronds[TreeHash(i, 5) % PalmFronds.Length];
            var fronds = ObjectScale.CreateMeshObject($"PalmFronds_{i}", frondMesh, parent);
            if (fronds == null) return false;

            var trunk = CreatePrimitive($"Trunk_{i}", parent);
            trunk.transform.localPosition = new Vector3(x, RoadH + 0.40f, z);
            trunk.transform.localScale = new Vector3(TreeTrunkW * 0.6f, 0.80f, TreeTrunkW * 0.6f);
            SetTexturedOpaque(trunk, PalmTrunkTex, Color.white);

            float yRot = TreeHash(i, 10) % 360;
            fronds.transform.localPosition = new Vector3(x, RoadH + 0.85f, z);
            fronds.transform.localScale = new Vector3(0.22f, 0.22f, 0.22f);
            fronds.transform.localRotation = Quaternion.Euler(0f, yRot, 0f);
            string frondTex = PalmFrondTextures[TreeHash(i, 12) % PalmFrondTextures.Length];
            SetTexturedCutout(fronds, frondTex, Color.white);
            return true;
        }

        /// <summary>Original primitive tree — fallback when meshes aren't available.</summary>
        private void RenderFallbackTree(int i, float x, float z, Transform parent)
        {
            var trunk = CreatePrimitive($"Trunk_{i}", parent);
            trunk.transform.localPosition = new Vector3(x, RoadH + TreeTrunkH * 0.5f, z);
            trunk.transform.localScale = new Vector3(TreeTrunkW, TreeTrunkH, TreeTrunkW);
            SetColor(trunk, TrunkColor);

            var canopy = CreatePrimitive($"Canopy_{i}", parent);
            canopy.transform.localPosition = new Vector3(x, RoadH + TreeTrunkH + TreeCanopyH * 0.5f, z);
            canopy.transform.localScale = new Vector3(TreeCanopyW, TreeCanopyH, TreeCanopyW);
            SetColor(canopy, CanopyColor);
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
                case SewerType.Canal:   return CanalColor;
                default:                return DrainColor;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // HELPERS
        // ═══════════════════════════════════════════════════════════════

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

        /// <summary>
        /// Apply a texture and set material properties on ALL materials of a renderer.
        /// Handles multi-submesh .obj models that have multiple material slots.
        /// </summary>
        private static void ApplyTextureToAll(GameObject go, string texturePath, Color tint,
            bool cutout)
        {
            var tex = ObjectScale.LoadTexture(texturePath);
            var r = go.GetComponentInChildren<Renderer>();
            if (r == null) return;

            var mats = r.materials; // copy so we can mutate
            for (int m = 0; m < mats.Length; m++)
            {
                var mat = mats[m];

                // Texture
                if (tex != null)
                {
                    if (mat.HasProperty("_BaseMap"))
                        mat.SetTexture("_BaseMap", tex);
                    else if (mat.HasProperty("_MainTex"))
                        mat.SetTexture("_MainTex", tex);
                }

                // Tint
                if (mat.HasProperty("_BaseColor"))
                    mat.SetColor("_BaseColor", tint);
                else
                    mat.color = tint;

                // Depth & blend — always opaque pipeline with depth writes
                if (mat.HasProperty("_Surface"))
                    mat.SetFloat("_Surface", 0f);  // Opaque
                mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
                mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
                mat.SetFloat("_ZWrite", 1f);
                mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");

                if (cutout)
                {
                    if (mat.HasProperty("_AlphaClip"))
                        mat.SetFloat("_AlphaClip", 1f);
                    if (mat.HasProperty("_Cutoff"))
                        mat.SetFloat("_Cutoff", 0.3f);
                    mat.EnableKeyword("_ALPHATEST_ON");
                    mat.SetOverrideTag("RenderType", "TransparentCutout");
                    mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                    if (mat.HasProperty("_Cull"))
                        mat.SetFloat("_Cull", 0f); // Double-sided
                }
                else
                {
                    mat.DisableKeyword("_ALPHATEST_ON");
                    mat.SetOverrideTag("RenderType", "Opaque");
                    mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
                }
            }
            r.materials = mats; // write back
        }

        /// <summary>
        /// Opaque textured material — for trunks, bark, solid surfaces.
        /// </summary>
        private static void SetTexturedOpaque(GameObject go, string texturePath, Color tint)
        {
            ApplyTextureToAll(go, texturePath, tint, cutout: false);
        }

        /// <summary>
        /// Alpha-cutout textured material — for leaves, foliage, fronds.
        /// </summary>
        private static void SetTexturedCutout(GameObject go, string texturePath, Color tint)
        {
            ApplyTextureToAll(go, texturePath, tint, cutout: true);
        }
    }
}
