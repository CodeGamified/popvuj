// Copyright CodeGamified 2025-2026
// MIT License — PopVuj
using UnityEngine;
using CodeGamified.Procedural;
using System.Collections.Generic;

namespace PopVuj.Game
{
    /// <summary>
    /// Procedural blueprint for a surface building's interior.
    ///
    /// Buildings are 2D grids of StructureModules [Width × Height]:
    ///   - Columns run left(0) to right(Width-1)
    ///   - Rows run ground(0) to top(Height-1)
    ///
    /// Structure emerges from the grid layout. An L-shaped chapel:
    ///   Bell   | Air  | Air
    ///   Cross  | Air  | Air
    ///   Altar  | Pew  | Pew
    ///
    /// Warehouses have cranes at the top layer, storage types below.
    /// Each module type has its own visual emitter. Common structure
    /// (back wall, floor slabs, roof) wraps occupied cells.
    /// </summary>
    public class StructureBlueprint : IProceduralBlueprint
    {
        private readonly CellType _type;
        private readonly StructureModule[,] _grid;
        private readonly int _gridCols;
        private readonly int _gridRows;
        private readonly float _originX;
        private readonly float _roadH;
        private readonly float _buildingZ;

        // Warehouse resource snapshot
        private readonly int _resWood, _resStone, _resFood, _resGoods;

        private const float Cell = CityRenderer.CellSize;

        // Color keys resolved by palette at assembly time
        private const string KWall     = "wall";
        private const string KFloor    = "floor";
        private const string KWood     = "wood";
        private const string KStone    = "stone";
        private const string KFabric   = "fabric";
        private const string KMetal    = "metal";
        private const string KGold     = "gold";
        private const string KWater    = "water";
        private const string KGrain    = "grain";
        private const string KRoof     = "roof";

        public StructureBlueprint(CellType type, StructureModule[,] grid,
                                   float originX,
                                   float roadH = 0.3f, float buildingZ = 1.0f,
                                   int resWood = 0, int resStone = 0,
                                   int resFood = 0, int resGoods = 0)
        {
            _type = type;
            _grid = grid;
            _gridCols = grid.GetLength(0);
            _gridRows = grid.GetLength(1);
            _originX = originX;
            _roadH = roadH;
            _buildingZ = buildingZ;
            _resWood = resWood;
            _resStone = resStone;
            _resFood = resFood;
            _resGoods = resGoods;
        }

        public string DisplayName => $"{_type}_{_gridCols}w";
        public ProceduralLODHint LODHint => ProceduralLODHint.Standard;
        public string PaletteId => "popvuj_structures";

        // Grid cell dimensions (world units)
        private float CellW => (_gridCols * Cell) / _gridCols; // = Cell = 1.0
        private float StoryH => GetTotalHeight() / _gridRows;

        public ProceduralPartDef[] GetParts()
        {
            var parts = new List<ProceduralPartDef>(32);
            float totalW = _gridCols * Cell;
            float totalH = GetTotalHeight();

            // ── Common structure (walls, floors, roof) ──────────
            EmitCommonStructure(parts, totalW, totalH);

            // ── Per-cell module rendering ────────────────────────
            // Pre-count store modules for resource distribution
            int woodStores  = CountModule(StructureModule.WoodStore);
            int stoneStores = CountModule(StructureModule.StoneStore);
            int foodStores  = CountModule(StructureModule.FoodStore);
            int goodsStores = CountModule(StructureModule.GoodsStore);
            int woodIdx = 0, stoneIdx = 0, foodIdx = 0, goodsIdx = 0;

            for (int col = 0; col < _gridCols; col++)
            {
                for (int row = 0; row < _gridRows; row++)
                {
                    StructureModule mod = _grid[col, row];
                    if (mod == StructureModule.Air) continue;

                    float cx = _originX + (col + 0.5f) * CellW;
                    float cy = _roadH + (row + 0.5f) * StoryH;

                    switch (mod)
                    {
                        // Chapel
                        case StructureModule.Bell:     EmitBell(parts, cx, cy, col, row);     break;
                        case StructureModule.Altar:    EmitAltar(parts, cx, cy, col, row);    break;
                        case StructureModule.Pew:      EmitPew(parts, cx, cy, col, row);      break;
                        case StructureModule.Lectern:  EmitLectern(parts, cx, cy, col, row);  break;
                        case StructureModule.Cross:    EmitCross(parts, cx, cy, col, row);    break;
                        // House
                        case StructureModule.Bed:       EmitBed(parts, cx, cy, col, row);        break;
                        case StructureModule.Table:     EmitTable(parts, cx, cy, col, row);      break;
                        case StructureModule.Fireplace: EmitFireplace(parts, cx, cy, col, row);  break;
                        // Workshop
                        case StructureModule.Anvil:     EmitAnvil(parts, cx, cy, col, row);      break;
                        case StructureModule.Workbench: EmitWorkbench(parts, cx, cy, col, row);  break;
                        case StructureModule.Forge:     EmitForge(parts, cx, cy, col, row);      break;
                        // Warehouse
                        case StructureModule.WCrane:    EmitWarehouseCrane(parts, cx, cy, col, row); break;
                        case StructureModule.Desk:      EmitDesk(parts, cx, cy, col, row);       break;
                        case StructureModule.WoodStore:
                            EmitStoreCell(parts, cx, cy, col, row, KWood, "wood",
                                _resWood, woodStores, ref woodIdx);
                            break;
                        case StructureModule.StoneStore:
                            EmitStoreCell(parts, cx, cy, col, row, KStone, "stone",
                                _resStone, stoneStores, ref stoneIdx);
                            break;
                        case StructureModule.FoodStore:
                            EmitStoreCell(parts, cx, cy, col, row, KGrain, "food",
                                _resFood, foodStores, ref foodIdx);
                            break;
                        case StructureModule.GoodsStore:
                            EmitStoreCell(parts, cx, cy, col, row, KMetal, "goods",
                                _resGoods, goodsStores, ref goodsIdx);
                            break;
                        // Market
                        case StructureModule.Stall:      EmitStall(parts, cx, cy, col, row);      break;
                        case StructureModule.MarketCrate: EmitMarketCrate(parts, cx, cy, col, row); break;
                        // Farm
                        case StructureModule.Crop:       EmitCrop(parts, cx, cy, col, row);       break;
                        case StructureModule.Trough:     EmitTrough(parts, cx, cy, col, row);     break;
                        // Fountain
                        case StructureModule.Basin:      EmitBasin(parts, cx, cy, col, row);      break;
                        case StructureModule.Spout:      EmitSpout(parts, cx, cy, col, row);      break;
                        // Shipyard
                        case StructureModule.DrydockFrame: EmitDrydockFrame(parts, cx, cy, col, row); break;
                        case StructureModule.TimberStack:  EmitTimberStack(parts, cx, cy, col, row);  break;
                        // Pier
                        case StructureModule.PierDeck:    EmitPierDeck(parts, cx, cy, col, row);    break;
                        case StructureModule.PierCrane:   EmitPierCrane(parts, cx, cy, col, row);   break;
                        case StructureModule.PierCannon:  EmitPierCannon(parts, cx, cy, col, row);  break;
                        case StructureModule.PierFishing: EmitPierFishing(parts, cx, cy, col, row); break;
                    }
                }
            }

            return parts.ToArray();
        }

        private int CountModule(StructureModule module)
        {
            int count = 0;
            for (int c = 0; c < _gridCols; c++)
                for (int r = 0; r < _gridRows; r++)
                    if (_grid[c, r] == module) count++;
            return count;
        }

        // ═══════════════════════════════════════════════════════════════
        // COMMON STRUCTURE — walls, floor slabs, roof
        // ═══════════════════════════════════════════════════════════════

        private void EmitCommonStructure(List<ProceduralPartDef> p, float totalW, float totalH)
        {
            float floorY = _roadH;
            float cx = _originX + totalW * 0.5f;
            float sH = StoryH;
            bool isPier = _type == CellType.Pier;
            bool isFarm = _type == CellType.Farm;
            bool isFountain = _type == CellType.Fountain;

            if (isPier)
            {
                EmitPierStructure(p, totalW, cx);
                return;
            }

            // ── Per-cell back wall + floor panels ───────────────
            // Each occupied cell gets its own background so L-shapes
            // and irregular layouts emerge naturally from the grid.
            float cW = CellW;
            for (int col = 0; col < _gridCols; col++)
            {
                for (int row = 0; row < _gridRows; row++)
                {
                    if (_grid[col, row] == StructureModule.Air) continue;

                    float cellCX = _originX + (col + 0.5f) * cW;
                    float cellCY = floorY + (row + 0.5f) * sH;

                    // Back wall panel
                    if (!isFarm && !isFountain)
                    {
                        p.Add(new ProceduralPartDef($"backwall_{col}_{row}", PrimitiveType.Cube,
                            new Vector3(cellCX, cellCY, _buildingZ + Cell * 0.45f),
                            new Vector3(cW * 0.90f, sH, 0.04f), KWall));
                    }

                    // Floor slab at bottom of cell
                    float fy = floorY + row * sH;
                    string floorColor = (row == 0 && _type == CellType.Warehouse) ? KStone
                        : isFarm ? KFloor : KWood;
                    p.Add(new ProceduralPartDef($"floor_{col}_{row}", PrimitiveType.Cube,
                        new Vector3(cellCX, fy + 0.02f, _buildingZ),
                        new Vector3(cW * 0.88f, 0.04f, Cell * 0.85f), floorColor));
                }
            }

            // ── Side columns per occupied column-span ───────────
            if (_gridRows >= 2)
            {
                float colWW = 0.06f;
                // Left column: at leftmost occupied column
                for (int col = 0; col < _gridCols; col++)
                {
                    int lo = -1, hi = -1;
                    for (int row = 0; row < _gridRows; row++)
                    {
                        if (_grid[col, row] != StructureModule.Air)
                        {
                            if (lo < 0) lo = row;
                            hi = row;
                        }
                    }
                    if (lo < 0) continue;

                    float spanH = (hi - lo + 1) * sH;
                    float spanCY = floorY + (lo + (hi - lo + 1) * 0.5f) * sH;

                    // Left edge of this column
                    float lx = _originX + col * cW + colWW * 0.5f + cW * 0.02f;
                    p.Add(new ProceduralPartDef($"col_l_{col}", PrimitiveType.Cube,
                        new Vector3(lx, spanCY, _buildingZ),
                        new Vector3(colWW, spanH, colWW), KWood));

                    // Right edge of this column
                    float rx = _originX + (col + 1) * cW - colWW * 0.5f - cW * 0.02f;
                    p.Add(new ProceduralPartDef($"col_r_{col}", PrimitiveType.Cube,
                        new Vector3(rx, spanCY, _buildingZ),
                        new Vector3(colWW, spanH, colWW), KWood));
                }
            }

            // ── Roof per occupied top cell ──────────────────────
            if (!isFarm && !isFountain)
            {
                for (int col = 0; col < _gridCols; col++)
                {
                    // Find highest occupied row in this column
                    int topRow = -1;
                    for (int row = _gridRows - 1; row >= 0; row--)
                    {
                        if (_grid[col, row] != StructureModule.Air)
                        { topRow = row; break; }
                    }
                    if (topRow < 0) continue;

                    float roofCX = _originX + (col + 0.5f) * cW;
                    float roofY = floorY + (topRow + 1) * sH - 0.04f;
                    p.Add(new ProceduralPartDef($"roof_{col}", PrimitiveType.Cube,
                        new Vector3(roofCX, roofY, _buildingZ),
                        new Vector3(cW * 0.92f, 0.06f, Cell * 0.88f), KRoof));
                }
            }

            // ── Farm fence (open-air) ───────────────────────────
            if (isFarm)
            {
                float postH = 0.24f;
                p.Add(new ProceduralPartDef("fence_l", PrimitiveType.Cube,
                    new Vector3(_originX + 0.04f, floorY + postH * 0.5f, _buildingZ - Cell * 0.35f),
                    new Vector3(0.04f, postH, 0.04f), KWood));
                p.Add(new ProceduralPartDef("fence_r", PrimitiveType.Cube,
                    new Vector3(_originX + totalW - 0.04f, floorY + postH * 0.5f, _buildingZ - Cell * 0.35f),
                    new Vector3(0.04f, postH, 0.04f), KWood));
                p.Add(new ProceduralPartDef("fence_rail", PrimitiveType.Cube,
                    new Vector3(cx, floorY + postH * 0.7f, _buildingZ - Cell * 0.35f),
                    new Vector3(totalW - 0.08f, 0.03f, 0.03f), KWood));
            }

            // ── Market support posts ────────────────────────────
            if (_type == CellType.Market)
            {
                float postH = totalH * 0.8f;
                p.Add(new ProceduralPartDef("post_l", PrimitiveType.Cube,
                    new Vector3(_originX + totalW * 0.05f, floorY + postH * 0.5f, _buildingZ - Cell * 0.35f),
                    new Vector3(0.04f, postH, 0.04f), KWood));
                p.Add(new ProceduralPartDef("post_r", PrimitiveType.Cube,
                    new Vector3(_originX + totalW * 0.95f, floorY + postH * 0.5f, _buildingZ - Cell * 0.35f),
                    new Vector3(0.04f, postH, 0.04f), KWood));
            }
        }

        /// <summary>Pier common structure — pilings, deck, railings, connector.</summary>
        private void EmitPierStructure(List<ProceduralPartDef> p, float totalW, float cx)
        {
            float floorY = _roadH;

            // Plank deck
            p.Add(new ProceduralPartDef("pier_deck", PrimitiveType.Cube,
                new Vector3(cx, floorY + 0.02f, _buildingZ),
                new Vector3(totalW * 0.92f, 0.06f, Cell * 0.80f), KWood));

            // Pilings
            int pilingCount = Mathf.Max(2, _gridCols + 1);
            float pilingSpacing = totalW * 0.85f / (pilingCount - 1);
            float pilingH = 0.60f;
            for (int i = 0; i < pilingCount; i++)
            {
                float px = _originX + totalW * 0.075f + pilingSpacing * i;
                p.Add(new ProceduralPartDef($"piling_{i}", PrimitiveType.Cube,
                    new Vector3(px, floorY - pilingH * 0.5f, _buildingZ - Cell * 0.25f),
                    new Vector3(0.06f, pilingH, 0.06f), KWood));
                p.Add(new ProceduralPartDef($"piling_back_{i}", PrimitiveType.Cube,
                    new Vector3(px, floorY - pilingH * 0.5f, _buildingZ + Cell * 0.25f),
                    new Vector3(0.06f, pilingH, 0.06f), KWood));
            }

            // Rope railing
            float railH = 0.12f;
            p.Add(new ProceduralPartDef("rail_front", PrimitiveType.Cube,
                new Vector3(cx, floorY + railH + 0.04f, _buildingZ - Cell * 0.35f),
                new Vector3(totalW * 0.88f, 0.024f, 0.024f), KFabric));

            // Bollards
            float bollardH = 0.10f;
            p.Add(new ProceduralPartDef("bollard_l", PrimitiveType.Cube,
                new Vector3(_originX + 0.06f, floorY + bollardH * 0.5f + 0.04f, _buildingZ - Cell * 0.3f),
                new Vector3(0.05f, bollardH, 0.05f), KWood));
            p.Add(new ProceduralPartDef("bollard_r", PrimitiveType.Cube,
                new Vector3(_originX + totalW - 0.06f, floorY + bollardH * 0.5f + 0.04f, _buildingZ - Cell * 0.3f),
                new Vector3(0.05f, bollardH, 0.05f), KWood));

            // L-connector from road to pier
            float connCX = _originX + Cell * 0.5f;
            float connZLen = 2f * Cell;
            float connCZ = 0.5f;
            p.Add(new ProceduralPartDef("connector_deck", PrimitiveType.Cube,
                new Vector3(connCX, floorY + 0.02f, connCZ),
                new Vector3(Cell, 0.06f, connZLen), KWood));
            p.Add(new ProceduralPartDef("conn_piling_front", PrimitiveType.Cube,
                new Vector3(connCX, floorY - pilingH * 0.5f, connCZ - connZLen * 0.3f),
                new Vector3(0.06f, pilingH, 0.06f), KWood));
            p.Add(new ProceduralPartDef("conn_piling_back", PrimitiveType.Cube,
                new Vector3(connCX, floorY - pilingH * 0.5f, connCZ + connZLen * 0.3f),
                new Vector3(0.06f, pilingH, 0.06f), KWood));
            float railZLen = connZLen * 0.5f;
            float railCZ = railZLen * 0.5f;
            p.Add(new ProceduralPartDef("conn_rail_right", PrimitiveType.Cube,
                new Vector3(connCX + Cell * 0.45f, floorY + railH + 0.04f, railCZ),
                new Vector3(0.024f, 0.024f, railZLen), KFabric));
        }

        // ═══════════════════════════════════════════════════════════════
        // HEIGHT LOOKUP (mirrors CityRenderer constants)
        // ═══════════════════════════════════════════════════════════════

        private float GetTotalHeight()
        {
            switch (_type)
            {
                case CellType.House:    return 2f;
                case CellType.Chapel:   return 3f;
                case CellType.Workshop: return 2f;
                case CellType.Farm:     return 1f;
                case CellType.Market:   return 1f;
                case CellType.Fountain: return 1f;
                case CellType.Shipyard: return 2f;
                case CellType.Pier:     return 1f;
                case CellType.Warehouse:return 3f;
                default:                return 1f;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // CHAPEL MODULES — Bell, Altar, Pew, Lectern, Cross
        // ═══════════════════════════════════════════════════════════════

        private void EmitBell(List<ProceduralPartDef> p, float cx, float cy, int c, int r)
        {
            float sH = StoryH;
            float cW = CellW;
            // Bell frame
            p.Add(new ProceduralPartDef($"bell_frame_{c}_{r}", PrimitiveType.Cube,
                new Vector3(cx, cy + sH * 0.2f, _buildingZ),
                new Vector3(cW * 0.40f, 0.04f, cW * 0.40f), KWood));
            // Bell body
            p.Add(new ProceduralPartDef($"bell_{c}_{r}", PrimitiveType.Cube,
                new Vector3(cx, cy, _buildingZ),
                new Vector3(cW * 0.25f, sH * 0.40f, cW * 0.25f), KGold));
            // Bell support posts
            p.Add(new ProceduralPartDef($"bell_post_l_{c}_{r}", PrimitiveType.Cube,
                new Vector3(cx - cW * 0.18f, cy, _buildingZ),
                new Vector3(0.03f, sH * 0.60f, 0.03f), KWood));
            p.Add(new ProceduralPartDef($"bell_post_r_{c}_{r}", PrimitiveType.Cube,
                new Vector3(cx + cW * 0.18f, cy, _buildingZ),
                new Vector3(0.03f, sH * 0.60f, 0.03f), KWood));
        }

        private void EmitAltar(List<ProceduralPartDef> p, float cx, float cy, int c, int r)
        {
            float sH = StoryH;
            float cW = CellW;
            // Altar table
            float altH = sH * 0.30f;
            float altW = cW * 0.60f;
            p.Add(new ProceduralPartDef($"altar_{c}_{r}", PrimitiveType.Cube,
                new Vector3(cx, cy - sH * 0.25f + altH * 0.5f, _buildingZ),
                new Vector3(altW, altH, altW * 0.6f), KStone));
            // Cross above altar
            float crossH = sH * 0.20f;
            p.Add(new ProceduralPartDef($"altar_cross_v_{c}_{r}", PrimitiveType.Cube,
                new Vector3(cx, cy + sH * 0.10f, _buildingZ + Cell * 0.35f),
                new Vector3(0.024f, crossH, 0.024f), KGold));
            p.Add(new ProceduralPartDef($"altar_cross_h_{c}_{r}", PrimitiveType.Cube,
                new Vector3(cx, cy + sH * 0.16f, _buildingZ + Cell * 0.35f),
                new Vector3(crossH * 0.6f, 0.024f, 0.024f), KGold));
        }

        private void EmitPew(List<ProceduralPartDef> p, float cx, float cy, int c, int r)
        {
            float sH = StoryH;
            float cW = CellW;
            float pewW = cW * 0.65f;
            float pewH = sH * 0.14f;
            float pewD = Cell * 0.3f;
            float baseY = cy - sH * 0.35f;

            // Try two .obj benches per cell
            bool usedMesh = true;
            for (int i = 0; i < 2; i++)
            {
                float px = cx + (i == 0 ? -cW * 0.15f : cW * 0.15f);
                if (!ObjectScale.TryAdd(p, $"pew_{c}_{r}_{i}", "old_bench",
                    new Vector3(px, baseY, _buildingZ), KWood))
                { usedMesh = false; break; }
            }
            if (usedMesh) return;

            // Fallback: two pew rows per cell
            for (int i = 0; i < 2; i++)
            {
                float px = cx + (i == 0 ? -cW * 0.15f : cW * 0.15f);
                // Seat
                p.Add(new ProceduralPartDef($"pew_seat_{c}_{r}_{i}", PrimitiveType.Cube,
                    new Vector3(px, baseY + pewH * 0.5f, _buildingZ),
                    new Vector3(pewW * 0.40f, pewH, pewD), KWood));
                // Back rest
                p.Add(new ProceduralPartDef($"pew_back_{c}_{r}_{i}", PrimitiveType.Cube,
                    new Vector3(px, baseY + pewH + 0.04f, _buildingZ + pewD * 0.4f),
                    new Vector3(pewW * 0.40f, sH * 0.10f, 0.03f), KWood));
            }
        }

        private void EmitLectern(List<ProceduralPartDef> p, float cx, float cy, int c, int r)
        {
            float sH = StoryH;
            float cW = CellW;
            float lecW = cW * 0.20f;
            float lecH = sH * 0.35f;
            float baseY = cy - sH * 0.35f;

            if (ObjectScale.TryAdd(p, $"lectern_{c}_{r}", "scroll_stand/scroll_stand_1",
                new Vector3(cx, baseY + 0.04f, _buildingZ), KWood))
                return;

            p.Add(new ProceduralPartDef($"lectern_{c}_{r}", PrimitiveType.Cube,
                new Vector3(cx, cy - sH * 0.15f, _buildingZ),
                new Vector3(lecW, lecH, lecW * 0.8f), KWood));
        }

        private void EmitCross(List<ProceduralPartDef> p, float cx, float cy, int c, int r)
        {
            float sH = StoryH;
            float crossH = sH * 0.40f;
            p.Add(new ProceduralPartDef($"cross_v_{c}_{r}", PrimitiveType.Cube,
                new Vector3(cx, cy, _buildingZ + Cell * 0.35f),
                new Vector3(0.028f, crossH, 0.028f), KGold));
            p.Add(new ProceduralPartDef($"cross_h_{c}_{r}", PrimitiveType.Cube,
                new Vector3(cx, cy + crossH * 0.2f, _buildingZ + Cell * 0.35f),
                new Vector3(crossH * 0.6f, 0.028f, 0.028f), KGold));
        }

        // ═══════════════════════════════════════════════════════════════
        // HOUSE MODULES — Bed, Table, Fireplace
        // ═══════════════════════════════════════════════════════════════

        private void EmitBed(List<ProceduralPartDef> p, float cx, float cy, int c, int r)
        {
            float sH = StoryH;
            float cW = CellW;
            float bedW = cW * 0.70f;
            float bedH = 0.08f;
            float bedD = Cell * 0.35f;
            float baseY = cy - sH * 0.35f;
            // Frame
            p.Add(new ProceduralPartDef($"bed_frame_{c}_{r}", PrimitiveType.Cube,
                new Vector3(cx, baseY + bedH * 0.5f + 0.04f, _buildingZ + Cell * 0.2f),
                new Vector3(bedW, bedH, bedD), KWood));
            // Blanket
            p.Add(new ProceduralPartDef($"bed_blanket_{c}_{r}", PrimitiveType.Cube,
                new Vector3(cx, baseY + bedH + 0.06f, _buildingZ + Cell * 0.2f),
                new Vector3(bedW * 0.90f, 0.04f, bedD * 0.85f), KFabric));
        }

        private void EmitTable(List<ProceduralPartDef> p, float cx, float cy, int c, int r)
        {
            float sH = StoryH;
            float cW = CellW;
            float tableW = cW * 0.55f;
            float tableH = 0.16f;
            float baseY = cy - sH * 0.35f;

            if (ObjectScale.TryAdd(p, $"table_{c}_{r}", "standing_two_legged_table_1",
                new Vector3(cx, baseY + 0.04f, _buildingZ - Cell * 0.1f), KWood))
                return;

            p.Add(new ProceduralPartDef($"table_{c}_{r}", PrimitiveType.Cube,
                new Vector3(cx, baseY + tableH * 0.5f + 0.04f, _buildingZ - Cell * 0.1f),
                new Vector3(tableW, tableH, Cell * 0.2f), KWood));
        }

        private void EmitFireplace(List<ProceduralPartDef> p, float cx, float cy, int c, int r)
        {
            float sH = StoryH;
            float cW = CellW;
            float fpW = cW * 0.40f;
            float fpH = sH * 0.60f;
            float baseY = cy - sH * 0.35f;

            if (ObjectScale.TryAdd(p, $"fireplace_{c}_{r}", "stove",
                new Vector3(cx, baseY + 0.04f, _buildingZ + Cell * 0.30f), KStone))
                return;

            // Chimney body
            p.Add(new ProceduralPartDef($"fireplace_{c}_{r}", PrimitiveType.Cube,
                new Vector3(cx, cy - sH * 0.10f, _buildingZ + Cell * 0.30f),
                new Vector3(fpW, fpH, fpW), KStone));
            // Fire opening
            p.Add(new ProceduralPartDef($"fire_opening_{c}_{r}", PrimitiveType.Cube,
                new Vector3(cx, cy - sH * 0.25f, _buildingZ + Cell * 0.15f),
                new Vector3(fpW * 0.5f, fpH * 0.3f, 0.02f), KMetal));
        }

        // ═══════════════════════════════════════════════════════════════
        // WORKSHOP MODULES — Workbench, Anvil, Forge
        // ═══════════════════════════════════════════════════════════════

        private void EmitWorkbench(List<ProceduralPartDef> p, float cx, float cy, int c, int r)
        {
            float sH = StoryH;
            float cW = CellW;
            float benchW = cW * 0.65f;
            float benchH = 0.20f;
            float benchD = Cell * 0.35f;
            float baseY = cy - sH * 0.35f;

            if (ObjectScale.TryAdd(p, $"bench_{c}_{r}", "trestle",
                new Vector3(cx, baseY + 0.04f, _buildingZ + Cell * 0.05f), KWood))
                return;

            p.Add(new ProceduralPartDef($"bench_{c}_{r}", PrimitiveType.Cube,
                new Vector3(cx, baseY + benchH * 0.5f + 0.04f, _buildingZ + Cell * 0.05f),
                new Vector3(benchW, benchH, benchD), KWood));
        }

        private void EmitAnvil(List<ProceduralPartDef> p, float cx, float cy, int c, int r)
        {
            float sH = StoryH;
            float cW = CellW;
            float benchH = 0.20f;
            float baseY = cy - sH * 0.35f;

            if (ObjectScale.TryAdd(p, $"anvil_{c}_{r}", "anvil_on_log",
                new Vector3(cx, baseY + 0.04f, _buildingZ + Cell * 0.05f), KMetal))
                return;

            // Fallback: procedural primitives
            // Stump
            p.Add(new ProceduralPartDef($"anvil_stump_{c}_{r}", PrimitiveType.Cube,
                new Vector3(cx, baseY + benchH * 0.5f + 0.04f, _buildingZ + Cell * 0.05f),
                new Vector3(cW * 0.30f, benchH, cW * 0.30f), KWood));
            // Anvil on top
            p.Add(new ProceduralPartDef($"anvil_{c}_{r}", PrimitiveType.Cube,
                new Vector3(cx, baseY + benchH + 0.08f, _buildingZ + Cell * 0.05f),
                new Vector3(cW * 0.25f, 0.08f, cW * 0.20f), KMetal));
        }

        private void EmitForge(List<ProceduralPartDef> p, float cx, float cy, int c, int r)
        {
            float sH = StoryH;
            float cW = CellW;
            float forgeW = cW * 0.45f;
            float forgeH = sH * 0.70f;
            float baseY = cy - sH * 0.35f;

            if (ObjectScale.TryAdd(p, $"forge_{c}_{r}", "stove",
                new Vector3(cx, baseY + 0.04f, _buildingZ + Cell * 0.30f), KStone))
                return;

            p.Add(new ProceduralPartDef($"forge_{c}_{r}", PrimitiveType.Cube,
                new Vector3(cx, cy - sH * 0.10f, _buildingZ + Cell * 0.30f),
                new Vector3(forgeW, forgeH, forgeW), KStone));
            // Bellows
            p.Add(new ProceduralPartDef($"bellows_{c}_{r}", PrimitiveType.Cube,
                new Vector3(cx - cW * 0.25f, cy - sH * 0.20f, _buildingZ + Cell * 0.10f),
                new Vector3(cW * 0.15f, sH * 0.15f, cW * 0.10f), KWood));
        }

        // ═══════════════════════════════════════════════════════════════
        // WAREHOUSE MODULES — WCrane, Store cells, Desk
        // ═══════════════════════════════════════════════════════════════

        private void EmitWarehouseCrane(List<ProceduralPartDef> p, float cx, float cy, int c, int r)
        {
            float sH = StoryH;
            float cW = CellW;
            float floorY = cy - sH * 0.5f;
            float totalH = GetTotalHeight();
            float craneZ = _buildingZ - Cell * 0.35f;

            // A-frame legs
            float legW = 0.05f;
            float legH = totalH * 0.90f;
            p.Add(new ProceduralPartDef($"crane_leg_l_{c}_{r}", PrimitiveType.Cube,
                new Vector3(cx - cW * 0.15f, floorY + legH * 0.5f, craneZ),
                new Vector3(legW, legH, legW), KWood));
            p.Add(new ProceduralPartDef($"crane_leg_r_{c}_{r}", PrimitiveType.Cube,
                new Vector3(cx + cW * 0.15f, floorY + legH * 0.5f, craneZ),
                new Vector3(legW, legH, legW), KWood));
            // Cross beam
            p.Add(new ProceduralPartDef($"crane_beam_{c}_{r}", PrimitiveType.Cube,
                new Vector3(cx, floorY + legH + 0.03f, craneZ),
                new Vector3(cW * 0.35f, 0.05f, 0.05f), KWood));
            // Rope
            float ropeH = totalH * 0.65f;
            p.Add(new ProceduralPartDef($"crane_rope_{c}_{r}", PrimitiveType.Cube,
                new Vector3(cx, floorY + legH - ropeH * 0.5f, craneZ),
                new Vector3(0.014f, ropeH, 0.014f), KFabric));
            // Basket
            p.Add(new ProceduralPartDef($"crane_basket_{c}_{r}", PrimitiveType.Cube,
                new Vector3(cx, floorY + 0.08f, craneZ),
                new Vector3(0.18f, 0.04f, 0.16f), KWood));
            // Hook
            p.Add(new ProceduralPartDef($"crane_hook_{c}_{r}", PrimitiveType.Cube,
                new Vector3(cx, floorY + legH - ropeH + 0.02f, craneZ),
                new Vector3(0.03f, 0.03f, 0.025f), KMetal));
        }

        /// <summary>
        /// Warehouse store cell — shelf posts, boards, resource fill.
        /// Resources distributed across all cells of that type.
        /// </summary>
        private void EmitStoreCell(List<ProceduralPartDef> p, float cx, float cy,
            int c, int r, string colorKey, string tag,
            int totalResource, int cellCount, ref int cellIndex)
        {
            float sH = StoryH;
            float cW = CellW;
            float baseY = cy - sH * 0.5f;
            float postW = 0.03f;
            float postH = sH * 0.85f;
            float shelfW = cW * 0.70f;
            float shelfD = Cell * 0.25f;

            // Shelf posts
            p.Add(new ProceduralPartDef($"store_postL_{c}_{r}", PrimitiveType.Cube,
                new Vector3(cx - shelfW * 0.45f, baseY + postH * 0.5f + 0.04f, _buildingZ + Cell * 0.20f),
                new Vector3(postW, postH, postW), KWood));
            p.Add(new ProceduralPartDef($"store_postR_{c}_{r}", PrimitiveType.Cube,
                new Vector3(cx + shelfW * 0.45f, baseY + postH * 0.5f + 0.04f, _buildingZ + Cell * 0.20f),
                new Vector3(postW, postH, postW), KWood));

            // Two shelf boards
            for (int b = 1; b <= 2; b++)
            {
                float boardY = baseY + sH * b / 3f;
                p.Add(new ProceduralPartDef($"store_board_{c}_{r}_{b}", PrimitiveType.Cube,
                    new Vector3(cx, boardY, _buildingZ + Cell * 0.20f),
                    new Vector3(shelfW, 0.025f, shelfD), KWood));
            }

            // Resource fill — distribute evenly across cells of this type
            int perCell = cellCount > 0 ? Mathf.CeilToInt((float)totalResource / cellCount) : 0;
            int remaining = Mathf.Max(0, totalResource - cellIndex * perCell);
            int amount = Mathf.Min(perCell, remaining);
            cellIndex++;

            if (amount > 0)
            {
                // Pick mesh by resource type: wood→barrel, food→burlap, stone→barrel, goods→chest
                string objName = tag == "food" ? "burlap_sack_one_sack"
                    : tag == "goods" ? "rounded_chest_2"
                    : "small_barrel_one_barrel";
                float itemS = 0.09f;
                int slotsPerBay = 3;
                int filled = Mathf.Min(Mathf.CeilToInt((float)amount / 5), slotsPerBay);
                for (int s = 0; s < filled; s++)
                {
                    float sy;
                    if (s == 0)
                        sy = baseY + 0.06f;
                    else
                        sy = baseY + sH * s / 3f + 0.025f;

                    if (!ObjectScale.TryAdd(p, $"store_res_{tag}_{c}_{r}_{s}", objName,
                        new Vector3(cx, sy, _buildingZ + Cell * 0.20f), colorKey))
                    {
                        p.Add(new ProceduralPartDef($"store_res_{tag}_{c}_{r}_{s}", PrimitiveType.Cube,
                            new Vector3(cx, sy + itemS * 0.5f, _buildingZ + Cell * 0.20f),
                            new Vector3(itemS, itemS, itemS), colorKey));
                    }
                }
            }
        }

        private void EmitDesk(List<ProceduralPartDef> p, float cx, float cy, int c, int r)
        {
            float sH = StoryH;
            float cW = CellW;
            float deskW = cW * 0.50f;
            float deskH = 0.18f;
            float baseY = cy - sH * 0.35f;

            if (ObjectScale.TryAdd(p, $"desk_{c}_{r}", "standing_two_legged_table_1",
                new Vector3(cx, baseY + 0.04f, _buildingZ), KWood))
                return;

            p.Add(new ProceduralPartDef($"desk_{c}_{r}", PrimitiveType.Cube,
                new Vector3(cx, baseY + deskH * 0.5f + 0.04f, _buildingZ),
                new Vector3(deskW, deskH, Cell * 0.25f), KWood));
        }

        // ═══════════════════════════════════════════════════════════════
        // MARKET MODULES — Stall, Crate
        // ═══════════════════════════════════════════════════════════════

        private void EmitStall(List<ProceduralPartDef> p, float cx, float cy, int c, int r)
        {
            float sH = StoryH;
            float cW = CellW;
            float baseY = cy - sH * 0.45f;
            float counterH = 0.16f;
            float counterW = cW * 0.70f;

            // Counter — try trestle mesh
            if (!ObjectScale.TryAdd(p, $"counter_{c}_{r}", "trestle",
                new Vector3(cx, baseY + 0.04f, _buildingZ), KWood))
            {
                p.Add(new ProceduralPartDef($"counter_{c}_{r}", PrimitiveType.Cube,
                    new Vector3(cx, baseY + counterH * 0.5f + 0.04f, _buildingZ),
                    new Vector3(counterW, counterH, Cell * 0.3f), KWood));
            }
            // Awning
            p.Add(new ProceduralPartDef($"awning_{c}_{r}", PrimitiveType.Cube,
                new Vector3(cx, cy + sH * 0.25f, _buildingZ - Cell * 0.1f),
                new Vector3(counterW * 1.1f, 0.03f, Cell * 0.5f), KFabric));
        }

        private void EmitMarketCrate(List<ProceduralPartDef> p, float cx, float cy, int c, int r)
        {
            float sH = StoryH;
            float cW = CellW;
            float baseY = cy - sH * 0.45f;
            float crateS = cW * 0.30f;

            if (ObjectScale.TryAdd(p, $"crate_{c}_{r}", "rounded_chest_2",
                new Vector3(cx, baseY + 0.04f, _buildingZ), KWood))
                return;

            p.Add(new ProceduralPartDef($"crate_{c}_{r}", PrimitiveType.Cube,
                new Vector3(cx, baseY + crateS * 0.5f + 0.04f, _buildingZ),
                new Vector3(crateS, crateS, crateS), KWood));
            // Second smaller crate on top
            p.Add(new ProceduralPartDef($"crate2_{c}_{r}", PrimitiveType.Cube,
                new Vector3(cx + crateS * 0.15f, baseY + crateS + crateS * 0.35f + 0.04f, _buildingZ),
                new Vector3(crateS * 0.7f, crateS * 0.7f, crateS * 0.7f), KWood));
        }

        // ═══════════════════════════════════════════════════════════════
        // FARM MODULES — Crop, Trough
        // ═══════════════════════════════════════════════════════════════

        private void EmitCrop(List<ProceduralPartDef> p, float cx, float cy, int c, int r)
        {
            float sH = StoryH;
            float cW = CellW;
            float baseY = cy - sH * 0.45f;
            float cropH = sH * 0.50f;
            // Soil mound
            p.Add(new ProceduralPartDef($"soil_{c}_{r}", PrimitiveType.Cube,
                new Vector3(cx, baseY + 0.04f, _buildingZ),
                new Vector3(cW * 0.80f, 0.06f, Cell * 0.70f), KFloor));
            // Crop rows (2 per cell)
            for (int i = 0; i < 2; i++)
            {
                float px = cx + (i == 0 ? -cW * 0.15f : cW * 0.15f);
                p.Add(new ProceduralPartDef($"crop_{c}_{r}_{i}", PrimitiveType.Cube,
                    new Vector3(px, baseY + 0.08f + cropH * 0.5f, _buildingZ),
                    new Vector3(cW * 0.18f, cropH, Cell * 0.18f), KGrain));
            }
        }

        private void EmitTrough(List<ProceduralPartDef> p, float cx, float cy, int c, int r)
        {
            float sH = StoryH;
            float cW = CellW;
            float baseY = cy - sH * 0.45f;

            if (ObjectScale.TryAdd(p, $"trough_{c}_{r}", "water_trough",
                new Vector3(cx, baseY + 0.04f, _buildingZ), KWood))
                return;

            // Fallback: procedural trough
            float tH = sH * 0.20f;
            p.Add(new ProceduralPartDef($"trough_{c}_{r}", PrimitiveType.Cube,
                new Vector3(cx, baseY + tH * 0.5f + 0.04f, _buildingZ),
                new Vector3(cW * 0.60f, tH, Cell * 0.30f), KWood));
            // Water inside
            p.Add(new ProceduralPartDef($"trough_water_{c}_{r}", PrimitiveType.Cube,
                new Vector3(cx, baseY + tH + 0.02f, _buildingZ),
                new Vector3(cW * 0.50f, 0.02f, Cell * 0.22f), KWater));
        }

        // ═══════════════════════════════════════════════════════════════
        // FOUNTAIN MODULES — Basin, Spout
        // ═══════════════════════════════════════════════════════════════

        private void EmitBasin(List<ProceduralPartDef> p, float cx, float cy, int c, int r)
        {
            float sH = StoryH;
            float cW = CellW;
            float baseY = cy - sH * 0.45f;
            float basinH = 0.12f;
            float basinW = cW * 0.70f;
            p.Add(new ProceduralPartDef($"basin_{c}_{r}", PrimitiveType.Cube,
                new Vector3(cx, baseY + basinH * 0.5f + 0.02f, _buildingZ),
                new Vector3(basinW, basinH, basinW * 0.8f), KStone));
            // Water surface
            p.Add(new ProceduralPartDef($"basin_water_{c}_{r}", PrimitiveType.Cube,
                new Vector3(cx, baseY + basinH + 0.01f, _buildingZ),
                new Vector3(basinW * 0.85f, 0.02f, basinW * 0.65f), KWater));
        }

        private void EmitSpout(List<ProceduralPartDef> p, float cx, float cy, int c, int r)
        {
            float sH = StoryH;
            float cW = CellW;
            float baseY = cy - sH * 0.45f;
            float basinH = 0.12f;
            float colH = sH * 0.55f;
            float colW = cW * 0.15f;
            // Column
            p.Add(new ProceduralPartDef($"spout_{c}_{r}", PrimitiveType.Cylinder,
                new Vector3(cx, baseY + basinH + colH * 0.5f, _buildingZ),
                new Vector3(colW, colH * 0.5f, colW), KStone));
            // Cap
            p.Add(new ProceduralPartDef($"spout_cap_{c}_{r}", PrimitiveType.Cube,
                new Vector3(cx, baseY + basinH + colH + 0.02f, _buildingZ),
                new Vector3(colW * 2f, 0.04f, colW * 2f), KStone));
        }

        // ═══════════════════════════════════════════════════════════════
        // SHIPYARD MODULES — DrydockFrame, TimberStack
        // ═══════════════════════════════════════════════════════════════

        private void EmitDrydockFrame(List<ProceduralPartDef> p, float cx, float cy, int c, int r)
        {
            float sH = StoryH;
            float cW = CellW;
            float baseY = cy - sH * 0.35f;
            // Keel segment
            float keelW = cW * 0.70f;
            float keelH = sH * 0.15f;
            p.Add(new ProceduralPartDef($"keel_{c}_{r}", PrimitiveType.Cube,
                new Vector3(cx, baseY + keelH * 0.5f + 0.06f, _buildingZ),
                new Vector3(keelW, keelH, Cell * 0.15f), KWood));
            // Ribs
            float ribH = sH * 0.35f;
            p.Add(new ProceduralPartDef($"rib_l_{c}_{r}", PrimitiveType.Cube,
                new Vector3(cx - keelW * 0.3f, baseY + keelH + ribH * 0.5f + 0.06f, _buildingZ),
                new Vector3(0.03f, ribH, Cell * 0.25f), KWood));
            p.Add(new ProceduralPartDef($"rib_r_{c}_{r}", PrimitiveType.Cube,
                new Vector3(cx + keelW * 0.3f, baseY + keelH + ribH * 0.5f + 0.06f, _buildingZ),
                new Vector3(0.03f, ribH, Cell * 0.25f), KWood));
        }

        private void EmitTimberStack(List<ProceduralPartDef> p, float cx, float cy, int c, int r)
        {
            float sH = StoryH;
            float cW = CellW;
            float baseY = cy - sH * 0.35f;
            float stackW = cW * 0.60f;
            // Logs stacked 2 high
            for (int i = 0; i < 2; i++)
            {
                p.Add(new ProceduralPartDef($"timber_{c}_{r}_{i}", PrimitiveType.Cube,
                    new Vector3(cx, baseY + 0.08f + i * 0.12f, _buildingZ + Cell * 0.30f),
                    new Vector3(stackW, 0.10f, Cell * 0.15f), KWood));
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // PIER MODULES — PierDeck, PierCrane, PierCannon, PierFishing
        // ═══════════════════════════════════════════════════════════════

        private void EmitPierDeck(List<ProceduralPartDef> p, float cx, float cy, int c, int r)
        {
            // Bare walkway — no additional fixtures, just a plank highlight
            float cW = CellW;
            p.Add(new ProceduralPartDef($"plank_{c}_{r}", PrimitiveType.Cube,
                new Vector3(cx, cy - 0.02f, _buildingZ),
                new Vector3(cW * 0.85f, 0.03f, Cell * 0.70f), KWood));
        }

        private void EmitPierCrane(List<ProceduralPartDef> p, float cx, float cy, int c, int r)
        {
            float cW = CellW;
            float floorY = _roadH;
            float h = 1.8f;

            // A-frame uprights
            float legW = 0.06f;
            float legH = h * 0.85f;
            float legYC = floorY + legH * 0.5f + 0.06f;
            p.Add(new ProceduralPartDef($"crane_leg_l_{c}", PrimitiveType.Cube,
                new Vector3(cx - cW * 0.15f, legYC, _buildingZ),
                new Vector3(legW, legH, legW), KWood));
            p.Add(new ProceduralPartDef($"crane_leg_r_{c}", PrimitiveType.Cube,
                new Vector3(cx + cW * 0.15f, legYC, _buildingZ),
                new Vector3(legW, legH, legW), KWood));

            // Cross brace
            p.Add(new ProceduralPartDef($"crane_brace_{c}", PrimitiveType.Cube,
                new Vector3(cx, floorY + legH * 0.45f, _buildingZ),
                new Vector3(cW * 0.30f, 0.04f, 0.04f), KWood));

            // Axle
            float axleY = floorY + legH + 0.04f;
            p.Add(new ProceduralPartDef($"crane_axle_{c}", PrimitiveType.Cube,
                new Vector3(cx, axleY, _buildingZ),
                new Vector3(cW * 0.35f, 0.06f, 0.06f), KWood));

            // Treadwheel
            float wheelR = h * 0.30f;
            float wheelZ = _buildingZ + Cell * 0.10f;
            p.Add(new ProceduralPartDef($"wheel_rim_{c}", PrimitiveType.Cube,
                new Vector3(cx, axleY, wheelZ),
                new Vector3(wheelR * 2f, wheelR * 2f, 0.04f), KWood));
            p.Add(new ProceduralPartDef($"wheel_hub_{c}", PrimitiveType.Cube,
                new Vector3(cx, axleY, wheelZ),
                new Vector3(0.08f, 0.08f, 0.06f), KMetal));
            // Spokes
            float spokeLen = wheelR * 0.85f;
            p.Add(new ProceduralPartDef($"spoke_h_{c}", PrimitiveType.Cube,
                new Vector3(cx, axleY, wheelZ),
                new Vector3(spokeLen * 2f, 0.03f, 0.03f), KWood));
            p.Add(new ProceduralPartDef($"spoke_v_{c}", PrimitiveType.Cube,
                new Vector3(cx, axleY, wheelZ),
                new Vector3(0.03f, spokeLen * 2f, 0.03f), KWood));

            // Treadwheel treads
            for (int t = 0; t < 8; t++)
            {
                float angle = t * Mathf.PI * 2f / 8;
                float tX = cx + Mathf.Cos(angle) * wheelR * 0.75f;
                float tY = axleY + Mathf.Sin(angle) * wheelR * 0.75f;
                p.Add(new ProceduralPartDef($"tread_{c}_{t}", PrimitiveType.Cube,
                    new Vector3(tX, tY, wheelZ),
                    new Vector3(0.05f, 0.02f, 0.04f), KWood));
            }

            // Jib arm
            float jibLen = cW * 0.70f;
            float jibZ = _buildingZ - jibLen * 0.5f;
            p.Add(new ProceduralPartDef($"crane_jib_{c}", PrimitiveType.Cube,
                new Vector3(cx, axleY + 0.04f, jibZ),
                new Vector3(0.05f, 0.05f, jibLen), KWood));

            // Rope + hook
            float ropeTipZ = _buildingZ - jibLen + 0.04f;
            float ropeH = h * 0.50f;
            p.Add(new ProceduralPartDef($"crane_rope_{c}", PrimitiveType.Cube,
                new Vector3(cx, axleY - ropeH * 0.5f, ropeTipZ),
                new Vector3(0.016f, ropeH, 0.016f), KFabric));
            p.Add(new ProceduralPartDef($"crane_hook_{c}", PrimitiveType.Cube,
                new Vector3(cx, axleY - ropeH + 0.02f, ropeTipZ),
                new Vector3(0.04f, 0.04f, 0.03f), KMetal));

            // Base plinth
            p.Add(new ProceduralPartDef($"crane_base_{c}", PrimitiveType.Cube,
                new Vector3(cx, floorY + 0.04f, _buildingZ),
                new Vector3(cW * 0.40f, 0.06f, cW * 0.35f), KStone));
        }

        private void EmitPierCannon(List<ProceduralPartDef> p, float cx, float cy, int c, int r)
        {
            float cW = CellW;
            float floorY = _roadH;
            float barrelL = cW * 0.5f;
            // Barrel
            p.Add(new ProceduralPartDef($"cannon_barrel_{c}", PrimitiveType.Cube,
                new Vector3(cx, floorY + 0.12f, _buildingZ - Cell * 0.18f),
                new Vector3(barrelL, 0.08f, 0.08f), KMetal));
            // Carriage
            p.Add(new ProceduralPartDef($"cannon_carriage_{c}", PrimitiveType.Cube,
                new Vector3(cx, floorY + 0.06f, _buildingZ - Cell * 0.18f),
                new Vector3(cW * 0.35f, 0.08f, cW * 0.3f), KWood));
        }

        private void EmitPierFishing(List<ProceduralPartDef> p, float cx, float cy, int c, int r)
        {
            float floorY = _roadH;
            float poleH = 0.70f;
            // Pole
            p.Add(new ProceduralPartDef($"fishing_pole_{c}", PrimitiveType.Cube,
                new Vector3(cx, floorY + poleH * 0.5f + 0.04f, _buildingZ - Cell * 0.25f),
                new Vector3(0.02f, poleH, 0.02f), KWood));
            // Line
            p.Add(new ProceduralPartDef($"fishing_line_{c}", PrimitiveType.Cube,
                new Vector3(cx + 0.04f, floorY + 0.04f, _buildingZ - Cell * 0.30f),
                new Vector3(0.008f, poleH * 0.6f, 0.008f), KFabric));
        }
    }
}
/*
            float benchRegion = totalW * 0.9f;
            float benchSpacing = benchRegion / benchCount;
            float benchW = benchSpacing * 0.65f;
            float benchH = 0.20f;
            float benchD = Cell * 0.35f;

            for (int i = 0; i < benchCount; i++)
            {
                float bx = _originX + totalW * 0.05f + benchSpacing * (i + 0.5f);
                // Workbench top
                p.Add(new ProceduralPartDef($"bench_{i}", PrimitiveType.Cube,
                    new Vector3(bx, floorY + benchH * 0.5f + 0.04f, _buildingZ + Cell * 0.05f),
                    new Vector3(benchW, benchH, benchD), KWood));

                // Anvil on top (every other bench, or first one)
                if (i % 2 == 0)
                {
                    p.Add(new ProceduralPartDef($"anvil_{i}", PrimitiveType.Cube,
                        new Vector3(bx, floorY + benchH + 0.08f, _buildingZ + Cell * 0.05f),
                        new Vector3(benchW * 0.4f, 0.08f, benchD * 0.5f), KMetal));
                }
            }

            // Chimney / forge at the back (if width >= 2)
            if (_buildingWidth >= 2)
            {
                float forgeW = Mathf.Lerp(0.12f, 0.24f, (_buildingWidth - 1) / 4f);
                p.Add(new ProceduralPartDef("forge", PrimitiveType.Cube,
                    new Vector3(_originX + totalW * 0.85f, floorY + h * 0.4f, _buildingZ + Cell * 0.3f),
                    new Vector3(forgeW, h * 0.8f, forgeW), KStone));
            }

            // Roof
            p.Add(new ProceduralPartDef("ws_roof", PrimitiveType.Cube,
                new Vector3(cx, floorY + h - 0.04f, _buildingZ),
                new Vector3(totalW * 0.90f, 0.06f, Cell * 0.85f), KRoof));
        }

        // ═══════════════════════════════════════════════════════════════
        // FARM — crop troughs, fence posts
        // ═══════════════════════════════════════════════════════════════

        private void EmitFarm(List<ProceduralPartDef> p, float totalW, float cx)
        {
            float floorY = _roadH;

            // Soil bed (slightly raised)
            p.Add(new ProceduralPartDef("soil", PrimitiveType.Cube,
                new Vector3(cx, floorY + 0.04f, _buildingZ),
                new Vector3(totalW * 0.88f, 0.08f, Cell * 0.85f), KFloor));

            // Crop rows
            int rowCount = Mathf.Max(1, _buildingWidth * 2);
            float rowRegion = totalW * 0.85f;
            float rowSpacing = rowRegion / rowCount;
            float cropH = Mathf.Lerp(0.12f, 0.20f, (_buildingWidth - 1) / 4f);

            for (int i = 0; i < rowCount; i++)
            {
                float rx = _originX + totalW * 0.075f + rowSpacing * (i + 0.5f);
                p.Add(new ProceduralPartDef($"crop_{i}", PrimitiveType.Cube,
                    new Vector3(rx, floorY + 0.08f + cropH * 0.5f, _buildingZ),
                    new Vector3(rowSpacing * 0.35f, cropH, Cell * 0.18f), KGrain));
            }

            // Fence posts at edges
            float postH = 0.24f;
            p.Add(new ProceduralPartDef("fence_l", PrimitiveType.Cube,
                new Vector3(_originX + 0.04f, floorY + postH * 0.5f, _buildingZ - Cell * 0.35f),
                new Vector3(0.04f, postH, 0.04f), KWood));
            p.Add(new ProceduralPartDef("fence_r", PrimitiveType.Cube,
                new Vector3(_originX + totalW - 0.04f, floorY + postH * 0.5f, _buildingZ - Cell * 0.35f),
                new Vector3(0.04f, postH, 0.04f), KWood));
            // Fence rail
            p.Add(new ProceduralPartDef("fence_rail", PrimitiveType.Cube,
                new Vector3(cx, floorY + postH * 0.7f, _buildingZ - Cell * 0.35f),
                new Vector3(totalW - 0.08f, 0.03f, 0.03f), KWood));
        }

        // ═══════════════════════════════════════════════════════════════
        // MARKET — stall awnings, crate stacks
        // ═══════════════════════════════════════════════════════════════

        private void EmitMarket(List<ProceduralPartDef> p, float totalW, float cx)
        {
            float floorY = _roadH;
            float h = GetHeight();

            // Floor
            p.Add(new ProceduralPartDef("market_floor", PrimitiveType.Cube,
                new Vector3(cx, floorY + 0.02f, _buildingZ),
                new Vector3(totalW * 0.88f, 0.04f, Cell * 0.85f), KWood));

            // Stalls — each has a counter + awning
            int stallCount = Mathf.Max(1, _buildingWidth);
            float stallRegion = totalW * 0.9f;
            float stallSpacing = stallRegion / stallCount;

            for (int i = 0; i < stallCount; i++)
            {
                float sx = _originX + totalW * 0.05f + stallSpacing * (i + 0.5f);
                float stallW = stallSpacing * 0.7f;

                // Counter
                float counterH = 0.16f;
                p.Add(new ProceduralPartDef($"counter_{i}", PrimitiveType.Cube,
                    new Vector3(sx, floorY + counterH * 0.5f + 0.04f, _buildingZ),
                    new Vector3(stallW, counterH, Cell * 0.3f), KWood));

                // Awning overhead
                p.Add(new ProceduralPartDef($"awning_{i}", PrimitiveType.Cube,
                    new Vector3(sx, floorY + h * 0.75f, _buildingZ - Cell * 0.1f),
                    new Vector3(stallW * 1.1f, 0.03f, Cell * 0.5f), KFabric));

                // Crate on counter
                float crateS = Mathf.Lerp(0.06f, 0.10f, (_buildingWidth - 1) / 3f);
                p.Add(new ProceduralPartDef($"crate_{i}", PrimitiveType.Cube,
                    new Vector3(sx, floorY + counterH + 0.04f + crateS * 0.5f, _buildingZ),
                    new Vector3(crateS, crateS, crateS), KWood));
            }

            // Support posts
            p.Add(new ProceduralPartDef("post_l", PrimitiveType.Cube,
                new Vector3(_originX + totalW * 0.05f, floorY + h * 0.4f, _buildingZ - Cell * 0.35f),
                new Vector3(0.04f, h * 0.8f, 0.04f), KWood));
            p.Add(new ProceduralPartDef("post_r", PrimitiveType.Cube,
                new Vector3(_originX + totalW * 0.95f, floorY + h * 0.4f, _buildingZ - Cell * 0.35f),
                new Vector3(0.04f, h * 0.8f, 0.04f), KWood));
        }

        // ═══════════════════════════════════════════════════════════════
        // FOUNTAIN — basin + column + water
        // ═══════════════════════════════════════════════════════════════

        private void EmitFountain(List<ProceduralPartDef> p, float totalW, float cx)
        {
            float floorY = _roadH;

            // Stone base / basin
            float basinW = totalW * 0.7f;
            float basinH = 0.12f;
            p.Add(new ProceduralPartDef("basin", PrimitiveType.Cube,
                new Vector3(cx, floorY + basinH * 0.5f + 0.02f, _buildingZ),
                new Vector3(basinW, basinH, basinW * 0.8f), KStone));

            // Water surface inside basin
            p.Add(new ProceduralPartDef("water", PrimitiveType.Cube,
                new Vector3(cx, floorY + basinH + 0.01f, _buildingZ),
                new Vector3(basinW * 0.85f, 0.02f, basinW * 0.65f), KWater));

            // Central column
            float colH = Mathf.Lerp(0.30f, 0.50f, (_buildingWidth - 1) / 3f);
            float colW = Mathf.Lerp(0.06f, 0.10f, (_buildingWidth - 1) / 3f);
            p.Add(new ProceduralPartDef("column", PrimitiveType.Cylinder,
                new Vector3(cx, floorY + basinH + colH * 0.5f, _buildingZ),
                new Vector3(colW, colH * 0.5f, colW), KStone));

            // Top cap
            p.Add(new ProceduralPartDef("cap", PrimitiveType.Cube,
                new Vector3(cx, floorY + basinH + colH + 0.02f, _buildingZ),
                new Vector3(colW * 2f, 0.04f, colW * 2f), KStone));
        }

        // ═══════════════════════════════════════════════════════════════
        // HEIGHT LOOKUP (mirrors CityRenderer constants)
        // ═══════════════════════════════════════════════════════════════

        private float GetHeight()
        {
            switch (_type)
            {
                case CellType.House:    return 2f;
                case CellType.Chapel:   return 3f;
                case CellType.Workshop: return 2f;
                case CellType.Farm:     return 1f;
                case CellType.Market:   return 1f;
                case CellType.Fountain: return 1f;
                case CellType.Shipyard:  return 2f;
                case CellType.Pier:      return 1f;
                case CellType.Warehouse: return 3f;
                default:                 return 1f;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // SHIPYARD — drydock frame, timber stacks, tools
        // ═══════════════════════════════════════════════════════════════

        private void EmitShipyard(List<ProceduralPartDef> p, float totalW, float cx)
        {
            float floorY = _roadH;
            float h = GetHeight();

            // Back wall
            p.Add(new ProceduralPartDef("yard_backwall", PrimitiveType.Cube,
                new Vector3(cx, floorY + h * 0.5f, _buildingZ + Cell * 0.45f),
                new Vector3(totalW * 0.90f, h, 0.04f), KWall));

            // Floor (gravel/sand)
            p.Add(new ProceduralPartDef("yard_floor", PrimitiveType.Cube,
                new Vector3(cx, floorY + 0.02f, _buildingZ),
                new Vector3(totalW * 0.88f, 0.04f, Cell * 0.85f), KStone));

            // Drydock frame — ship skeleton under construction
            float keelW = totalW * 0.7f;
            float keelH = h * 0.15f;
            p.Add(new ProceduralPartDef("keel", PrimitiveType.Cube,
                new Vector3(cx, floorY + keelH * 0.5f + 0.06f, _buildingZ),
                new Vector3(keelW, keelH, Cell * 0.15f), KWood));

            // Ribs rising from keel
            int ribCount = Mathf.Max(2, _buildingWidth * 2);
            float ribSpacing = keelW / ribCount;
            float ribH = Mathf.Lerp(0.20f, 0.40f, (_buildingWidth - 1) / 4f);
            for (int i = 0; i < ribCount; i++)
            {
                float rx = cx - keelW * 0.5f + ribSpacing * (i + 0.5f);
                p.Add(new ProceduralPartDef($"rib_{i}", PrimitiveType.Cube,
                    new Vector3(rx, floorY + keelH + ribH * 0.5f + 0.06f, _buildingZ),
                    new Vector3(0.03f, ribH, Cell * 0.25f), KWood));
            }

            // Timber stacks along the back
            int stackCount = Mathf.Max(1, _buildingWidth);
            float stackRegion = totalW * 0.25f;
            float stackW = stackRegion / stackCount * 0.7f;
            for (int i = 0; i < stackCount; i++)
            {
                float sx = _originX + totalW * 0.78f + (stackRegion / stackCount) * (i + 0.5f) - stackRegion * 0.5f;
                p.Add(new ProceduralPartDef($"timber_{i}", PrimitiveType.Cube,
                    new Vector3(sx, floorY + 0.08f, _buildingZ + Cell * 0.3f),
                    new Vector3(stackW, 0.12f, Cell * 0.15f), KWood));
            }

            // Roof beams (open-sided shelter)
            p.Add(new ProceduralPartDef("yard_roof", PrimitiveType.Cube,
                new Vector3(cx, floorY + h - 0.04f, _buildingZ),
                new Vector3(totalW * 0.90f, 0.06f, Cell * 0.85f), KRoof));
        }

        // ═══════════════════════════════════════════════════════════════
        // WAREHOUSE — multi-story storage with front cranes + resource fill
        // ═══════════════════════════════════════════════════════════════

        private void EmitWarehouse(List<ProceduralPartDef> p, float totalW, float cx)
        {
            float floorY = _roadH;
            float h = GetHeight();         // 3.0
            int floors = 3;
            float storyH = h / floors;     // 1.0 per floor
            int craneCount = _buildingWidth; // 1 crane per tile width

            // Back wall — full height
            p.Add(new ProceduralPartDef("wh_backwall", PrimitiveType.Cube,
                new Vector3(cx, floorY + h * 0.5f, _buildingZ + Cell * 0.45f),
                new Vector3(totalW * 0.92f, h, 0.04f), KWall));

            // Side columns
            float colW = 0.06f;
            p.Add(new ProceduralPartDef("wh_col_l", PrimitiveType.Cube,
                new Vector3(_originX + colW * 0.5f + totalW * 0.02f, floorY + h * 0.5f, _buildingZ),
                new Vector3(colW, h, colW), KWood));
            p.Add(new ProceduralPartDef("wh_col_r", PrimitiveType.Cube,
                new Vector3(_originX + totalW - colW * 0.5f - totalW * 0.02f, floorY + h * 0.5f, _buildingZ),
                new Vector3(colW, h, colW), KWood));

            // ── Shelf parameters ──
            float shelfRegion = totalW * 0.80f;        // shelves span 80% of width
            float shelfStartX = _originX + totalW * 0.10f;
            int shelfCount = Mathf.Max(1, _buildingWidth * 2); // 2 shelf bays per tile
            float shelfSpacing = shelfRegion / shelfCount;
            float shelfW = shelfSpacing * 0.85f;
            float shelfD = Cell * 0.25f;
            float postW = 0.03f;
            float itemS = Mathf.Lerp(0.08f, 0.11f, (_buildingWidth - 1) / 4f);

            // ── Per-floor: slab + thin shelf support posts + horizontal boards ──
            for (int f = 0; f < floors; f++)
            {
                float baseY = floorY + f * storyH;
                string tag = $"f{f}";

                // Floor slab
                p.Add(new ProceduralPartDef($"wh_{tag}_floor", PrimitiveType.Cube,
                    new Vector3(cx, baseY + 0.02f, _buildingZ),
                    new Vector3(totalW * 0.90f, 0.04f, Cell * 0.85f), f == 0 ? KStone : KWood));

                // Shelf bays: thin vertical posts + horizontal boards
                for (int s = 0; s < shelfCount; s++)
                {
                    float sx = shelfStartX + shelfSpacing * (s + 0.5f);
                    float postH = storyH * 0.85f;

                    // Left + right support posts (thin sticks)
                    p.Add(new ProceduralPartDef($"wh_{tag}_postL_{s}", PrimitiveType.Cube,
                        new Vector3(sx - shelfW * 0.45f, baseY + postH * 0.5f + 0.04f, _buildingZ + Cell * 0.20f),
                        new Vector3(postW, postH, postW), KWood));
                    p.Add(new ProceduralPartDef($"wh_{tag}_postR_{s}", PrimitiveType.Cube,
                        new Vector3(sx + shelfW * 0.45f, baseY + postH * 0.5f + 0.04f, _buildingZ + Cell * 0.20f),
                        new Vector3(postW, postH, postW), KWood));

                    // Two horizontal shelf boards
                    for (int b = 1; b <= 2; b++)
                    {
                        float boardY = baseY + storyH * b / 3f;
                        p.Add(new ProceduralPartDef($"wh_{tag}_board_{s}_{b}", PrimitiveType.Cube,
                            new Vector3(sx, boardY, _buildingZ + Cell * 0.20f),
                            new Vector3(shelfW, 0.025f, shelfD), KWood));
                    }
                }

                // Ground floor keeper desk
                if (f == 0)
                {
                    float deskW = totalW * 0.08f;
                    float deskH = 0.18f;
                    p.Add(new ProceduralPartDef("wh_desk", PrimitiveType.Cube,
                        new Vector3(_originX + totalW * 0.05f, baseY + deskH * 0.5f + 0.04f, _buildingZ),
                        new Vector3(deskW, deskH, Cell * 0.25f), KWood));
                }
            }

            // ── Dynamic resource fill ──
            //   Floor 0: Wood (logs) + Stone
            //   Floor 1: Food
            //   Floor 2: Goods
            // Each shelf bay has 3 slots (floor level + board1 + board2).
            // Half the bays per floor for wood/stone split.
            int halfBays = Mathf.Max(1, shelfCount / 2);
            int slotsPerBay = 3;
            EmitWoodLogFill(p, _resWood, 0, 0, halfBays, slotsPerBay,
                shelfStartX, shelfSpacing, storyH, floorY);
            EmitResourceFill(p, _resStone, KStone, "stone", 0, halfBays, shelfCount - halfBays, slotsPerBay,
                shelfStartX, shelfSpacing, storyH, itemS, floorY);
            EmitResourceFill(p, _resFood,  KGrain, "food",  1, 0, shelfCount, slotsPerBay,
                shelfStartX, shelfSpacing, storyH, itemS, floorY);
            EmitResourceFill(p, _resGoods, KMetal, "goods", 2, 0, shelfCount, slotsPerBay,
                shelfStartX, shelfSpacing, storyH, itemS, floorY);

            // ── Front cranes (1 per tile width, at the building front face) ──
            float craneZ = _buildingZ - Cell * 0.35f; // in front of building
            for (int c = 0; c < craneCount; c++)
            {
                float craneCX = _originX + (c + 0.5f) * Cell;
                EmitWarehouseCrane(p, craneCX, Cell, floorY, h, craneZ, c);
            }

            // Roof
            p.Add(new ProceduralPartDef("wh_roof", PrimitiveType.Cube,
                new Vector3(cx, floorY + h - 0.04f, _buildingZ),
                new Vector3(totalW * 0.94f, 0.06f, Cell * 0.88f), KRoof));
        }

        /// <summary>
        /// Emit a warehouse front crane — A-frame legs, cross beam, rope + platform.
        /// </summary>
        private void EmitWarehouseCrane(List<ProceduralPartDef> p,
            float cx, float slotW, float floorY, float totalH, float craneZ, int idx)
        {
            float legW = 0.05f;
            float legH = totalH * 0.90f;

            // A-frame legs
            p.Add(new ProceduralPartDef($"whcr_leg_l_{idx}", PrimitiveType.Cube,
                new Vector3(cx - slotW * 0.15f, floorY + legH * 0.5f, craneZ),
                new Vector3(legW, legH, legW), KWood));
            p.Add(new ProceduralPartDef($"whcr_leg_r_{idx}", PrimitiveType.Cube,
                new Vector3(cx + slotW * 0.15f, floorY + legH * 0.5f, craneZ),
                new Vector3(legW, legH, legW), KWood));

            // Cross beam at top
            p.Add(new ProceduralPartDef($"whcr_beam_{idx}", PrimitiveType.Cube,
                new Vector3(cx, floorY + legH + 0.03f, craneZ),
                new Vector3(slotW * 0.35f, 0.05f, 0.05f), KWood));

            // Rope
            float ropeH = totalH * 0.65f;
            p.Add(new ProceduralPartDef($"whcr_rope_{idx}", PrimitiveType.Cube,
                new Vector3(cx, floorY + legH - ropeH * 0.5f, craneZ),
                new Vector3(0.014f, ropeH, 0.014f), KFabric));

            // Basket / platform at rope bottom
            p.Add(new ProceduralPartDef($"whcr_basket_{idx}", PrimitiveType.Cube,
                new Vector3(cx, floorY + 0.08f, craneZ),
                new Vector3(0.18f, 0.04f, 0.16f), KWood));

            // Hook at rope tip
            p.Add(new ProceduralPartDef($"whcr_hook_{idx}", PrimitiveType.Cube,
                new Vector3(cx, floorY + legH - ropeH + 0.02f, craneZ),
                new Vector3(0.03f, 0.03f, 0.025f), KMetal));
        }

        /// <summary>
        /// Emit wood as individual log pieces stacking into 4×4 mounds.
        /// Each log matches the minion-carried size (elongated 1:1:4 ratio).
        /// Logs stack 4 across × 4 high per shelf slot, filling bottom-up.
        /// </summary>
        private void EmitWoodLogFill(
            List<ProceduralPartDef> p, int amount,
            int floor, int bayOffset, int maxBays, int slotsPerBay,
            float shelfStartX, float shelfSpacing, float storyH, float floorY)
        {
            if (amount <= 0) return;

            const float logCross  = 0.06f;  // width & height of each log
            const float logLength = 0.24f;  // depth (4× cross-section)
            const int LOGS_PER_ROW   = 4;
            const int ROWS_PER_MOUND = 4;
            const int LOGS_PER_MOUND = LOGS_PER_ROW * ROWS_PER_MOUND; // 16

            float baseY = floorY + floor * storyH;
            int placed = 0;

            for (int bay = 0; bay < maxBays && placed < amount; bay++)
            {
                float bx = shelfStartX + shelfSpacing * (bay + bayOffset + 0.5f);

                for (int s = 0; s < slotsPerBay && placed < amount; s++)
                {
                    float slotBaseY;
                    if (s == 0)
                        slotBaseY = baseY + 0.06f;
                    else
                        slotBaseY = baseY + storyH * s / 3f + 0.025f;

                    float moundLeftX = bx - (LOGS_PER_ROW - 1) * logCross * 0.5f;

                    for (int row = 0; row < ROWS_PER_MOUND && placed < amount; row++)
                    {
                        for (int col = 0; col < LOGS_PER_ROW && placed < amount; col++, placed++)
                        {
                            float lx = moundLeftX + col * logCross;
                            float ly = slotBaseY + logCross * 0.5f + row * logCross;
                            float lz = _buildingZ + Cell * 0.20f;

                            p.Add(new ProceduralPartDef($"wh_log_{placed}", PrimitiveType.Cube,
                                new Vector3(lx, ly, lz),
                                new Vector3(logCross, logCross, logLength), KWood));
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Emit resource items on warehouse shelves. Each visual slot holds
        /// ~5 units. Items fill bottom-up: floor level → board 1 → board 2,
        /// then next bay.
        /// </summary>
        private void EmitResourceFill(
            List<ProceduralPartDef> p, int amount, string colorKey, string resTag,
            int floor, int bayOffset, int maxBays, int slotsPerBay,
            float shelfStartX, float shelfSpacing, float storyH,
            float itemS, float floorY)
        {
            if (amount <= 0) return;
            const int UNITS_PER_SLOT = 5;
            int totalSlots = maxBays * slotsPerBay;
            int filled = Mathf.Min(Mathf.CeilToInt((float)amount / UNITS_PER_SLOT), totalSlots);
            float baseY = floorY + floor * storyH;

            int idx = 0;
            for (int bay = 0; bay < maxBays && idx < filled; bay++)
            {
                float bx = shelfStartX + shelfSpacing * (bay + bayOffset + 0.5f);

                for (int s = 0; s < slotsPerBay && idx < filled; s++, idx++)
                {
                    float sy;
                    if (s == 0)
                        sy = baseY + 0.06f + itemS * 0.5f;                    // on floor
                    else
                        sy = baseY + storyH * s / 3f + 0.025f + itemS * 0.5f; // on shelf board

                    p.Add(new ProceduralPartDef($"wh_res_{resTag}_{idx}", PrimitiveType.Cube,
                        new Vector3(bx, sy, _buildingZ + Cell * 0.20f),
                        new Vector3(itemS, itemS, itemS), colorKey));
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // PIER — wooden planks, rope railings, bollards
        // ═══════════════════════════════════════════════════════════════

        private void EmitPier(List<ProceduralPartDef> p, float totalW, float cx)
        {
            float floorY = _roadH;

            // Plank deck
            p.Add(new ProceduralPartDef("pier_deck", PrimitiveType.Cube,
                new Vector3(cx, floorY + 0.02f, _buildingZ),
                new Vector3(totalW * 0.92f, 0.06f, Cell * 0.80f), KWood));

            // Support pilings underneath (visible in water zone)
            int pilingCount = Mathf.Max(2, _buildingWidth + 1);
            float pilingSpacing = totalW * 0.85f / (pilingCount - 1);
            float pilingH = 0.60f; // extends below deck into water
            for (int i = 0; i < pilingCount; i++)
            {
                float px = _originX + totalW * 0.075f + pilingSpacing * i;
                p.Add(new ProceduralPartDef($"piling_{i}", PrimitiveType.Cube,
                    new Vector3(px, floorY - pilingH * 0.5f, _buildingZ - Cell * 0.25f),
                    new Vector3(0.06f, pilingH, 0.06f), KWood));
                p.Add(new ProceduralPartDef($"piling_back_{i}", PrimitiveType.Cube,
                    new Vector3(px, floorY - pilingH * 0.5f, _buildingZ + Cell * 0.25f),
                    new Vector3(0.06f, pilingH, 0.06f), KWood));
            }

            // Rope railing (front side, facing camera)
            float railH = 0.12f;
            p.Add(new ProceduralPartDef("rail_front", PrimitiveType.Cube,
                new Vector3(cx, floorY + railH + 0.04f, _buildingZ - Cell * 0.35f),
                new Vector3(totalW * 0.88f, 0.024f, 0.024f), KFabric));

            // Bollards at ends
            float bollardH = 0.10f;
            p.Add(new ProceduralPartDef("bollard_l", PrimitiveType.Cube,
                new Vector3(_originX + 0.06f, floorY + bollardH * 0.5f + 0.04f, _buildingZ - Cell * 0.3f),
                new Vector3(0.05f, bollardH, 0.05f), KWood));
            p.Add(new ProceduralPartDef("bollard_r", PrimitiveType.Cube,
                new Vector3(_originX + totalW - 0.06f, floorY + bollardH * 0.5f + 0.04f, _buildingZ - Cell * 0.3f),
                new Vector3(0.05f, bollardH, 0.05f), KWood));

            // ── L-connector: perpendicular walkway from road (Z=0) to pier (Z=BuildingZ) ──
            // 1 cell wide, 2 cells deep (bridges road lane to structure lane).
            float connCX = _originX + Cell * 0.5f;      // center of the first pier slot
            float connXW = Cell;                         // 1 cell wide
            float connZLen = 2f * Cell;                  // 2 cells deep
            float connCZ = 0.5f;                         // anchor centered: spans Z=-0.5 to Z=1.5

            // Connector deck (runs in Z direction)
            p.Add(new ProceduralPartDef("connector_deck", PrimitiveType.Cube,
                new Vector3(connCX, floorY + 0.02f, connCZ),
                new Vector3(connXW, 0.06f, connZLen), KWood));

            // Pilings under the connector
            p.Add(new ProceduralPartDef("conn_piling_front", PrimitiveType.Cube,
                new Vector3(connCX, floorY - pilingH * 0.5f, connCZ - connZLen * 0.3f),
                new Vector3(0.06f, pilingH, 0.06f), KWood));
            p.Add(new ProceduralPartDef("conn_piling_back", PrimitiveType.Cube,
                new Vector3(connCX, floorY - pilingH * 0.5f, connCZ + connZLen * 0.3f),
                new Vector3(0.06f, pilingH, 0.06f), KWood));

            // Right-side railing (X+), road-side half only (Z=0 to midpoint).
            // Terminates at the midpoint where it meets the pier's front railing.
            float railZLen = connZLen * 0.5f;            // half the connector = road-side half
            float railCZ = railZLen * 0.5f;              // centered in the road-side half (Z=0 to midpoint)
            p.Add(new ProceduralPartDef("conn_rail_right", PrimitiveType.Cube,
                new Vector3(connCX + connXW * 0.45f, floorY + railH + 0.04f, railCZ),
                new Vector3(0.024f, 0.024f, railZLen), KFabric));

            // Per-slot fixtures (cranes, cannons, fishing poles)
            if (_pierFixtures != null)
            {
                for (int s = 0; s < _buildingWidth; s++)
                {
                    PierFixture fix = s < _pierFixtures.Length ? _pierFixtures[s] : PierFixture.None;
                    if (fix == PierFixture.None) continue;

                    float slotCX = _originX + (s + 0.5f) * Cell;
                    float slotW = Cell;

                    switch (fix)
                    {
                        case PierFixture.Crane:
                            EmitCraneFixture(p, slotCX, slotW, floorY);
                            break;
                        case PierFixture.Cannon:
                            EmitCannonFixture(p, slotCX, slotW, floorY, s);
                            break;
                        case PierFixture.FishingPole:
                            EmitFishingFixture(p, slotCX, slotW, floorY, s);
                            break;
                    }
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // PIER FIXTURES — crane, cannon, fishing pole per slot



            // ── A-frame uprights (straddling the pier, oriented along Z) ──
            float legW = 0.06f;
            float legH = h * 0.85f;
            float legYC = floorY + legH * 0.5f + 0.06f;
            // Legs sit on the pier, offset in X to form the A-frame
            p.Add(new ProceduralPartDef("crane_leg_l", PrimitiveType.Cube,
                new Vector3(cx - slotW * 0.15f, legYC, _buildingZ),
                new Vector3(legW, legH, legW), KWood));
            p.Add(new ProceduralPartDef("crane_leg_r", PrimitiveType.Cube,
                new Vector3(cx + slotW * 0.15f, legYC, _buildingZ),
                new Vector3(legW, legH, legW), KWood));

            // Cross brace between legs (mid-height structural support)
            float braceY = floorY + legH * 0.45f;
            p.Add(new ProceduralPartDef("crane_brace", PrimitiveType.Cube,
                new Vector3(cx, braceY, _buildingZ),
                new Vector3(slotW * 0.30f, 0.04f, 0.04f), KWood));

            // Top axle beam spanning between the two uprights
            float axleY = floorY + legH + 0.04f;
            p.Add(new ProceduralPartDef("crane_axle", PrimitiveType.Cube,
                new Vector3(cx, axleY, _buildingZ),
                new Vector3(slotW * 0.35f, 0.06f, 0.06f), KWood));

            // ── Treadwheel (on the pier/building side, +Z) ──
            // Represented as a large disc: outer rim + hub + spokes
            float wheelR = h * 0.30f;       // wheel radius
            float wheelZ = _buildingZ + Cell * 0.10f; // behind the axle (pier side)
            float wheelCY = axleY;           // centered on the axle

            // Outer rim (thin wide cube approximating a circle)
            p.Add(new ProceduralPartDef("wheel_rim", PrimitiveType.Cube,
                new Vector3(cx, wheelCY, wheelZ),
                new Vector3(wheelR * 2f, wheelR * 2f, 0.04f), KWood));
            // Hub
            p.Add(new ProceduralPartDef("wheel_hub", PrimitiveType.Cube,
                new Vector3(cx, wheelCY, wheelZ),
                new Vector3(0.08f, 0.08f, 0.06f), KMetal));
            // Spokes (4 radial bars forming a cross)
            float spokeLen = wheelR * 0.85f;
            p.Add(new ProceduralPartDef("spoke_h", PrimitiveType.Cube,
                new Vector3(cx, wheelCY, wheelZ),
                new Vector3(spokeLen * 2f, 0.03f, 0.03f), KWood));
            p.Add(new ProceduralPartDef("spoke_v", PrimitiveType.Cube,
                new Vector3(cx, wheelCY, wheelZ),
                new Vector3(0.03f, spokeLen * 2f, 0.03f), KWood));

            // Treadwheel treads (steps inside the rim, hinted by short boards on the inner face)
            int treadCount = 8;
            for (int t = 0; t < treadCount; t++)
            {
                float angle = t * Mathf.PI * 2f / treadCount;
                float tX = cx + Mathf.Cos(angle) * wheelR * 0.75f;
                float tY = wheelCY + Mathf.Sin(angle) * wheelR * 0.75f;
                p.Add(new ProceduralPartDef($"tread_{t}", PrimitiveType.Cube,
                    new Vector3(tX, tY, wheelZ),
                    new Vector3(0.05f, 0.02f, 0.04f), KWood));
            }

            // ── Jib arm extending over the water (-Z, toward docked ship) ──
            float jibLen = slotW * 0.70f;
            float jibZ = _buildingZ - jibLen * 0.5f;
            p.Add(new ProceduralPartDef("crane_jib", PrimitiveType.Cube,
                new Vector3(cx, axleY + 0.04f, jibZ),
                new Vector3(0.05f, 0.05f, jibLen), KWood));

            // Counter-weight stub on the pier side (+Z)
            float counterLen = slotW * 0.18f;
            p.Add(new ProceduralPartDef("crane_counter", PrimitiveType.Cube,
                new Vector3(cx, axleY + 0.04f, _buildingZ + counterLen * 0.5f + 0.04f),
                new Vector3(0.05f, 0.05f, counterLen), KWood));

            // Support struts from legs to jib (diagonal bracing seen from the side)
            float strutTopY = axleY + 0.02f;
            float strutBotY = floorY + legH * 0.60f;
            float strutTopZ = _buildingZ - jibLen * 0.25f;
            float strutMidY = (strutTopY + strutBotY) * 0.5f;
            float strutMidZ = (_buildingZ + strutTopZ) * 0.5f;
            float strutLen = Mathf.Sqrt(
                (strutTopY - strutBotY) * (strutTopY - strutBotY) +
                (strutTopZ - _buildingZ) * (strutTopZ - _buildingZ));
            p.Add(new ProceduralPartDef("crane_strut_l", PrimitiveType.Cube,
                new Vector3(cx - slotW * 0.08f, strutMidY, strutMidZ),
                new Vector3(0.03f, strutLen, 0.03f), KWood));
            p.Add(new ProceduralPartDef("crane_strut_r", PrimitiveType.Cube,
                new Vector3(cx + slotW * 0.08f, strutMidY, strutMidZ),
                new Vector3(0.03f, strutLen, 0.03f), KWood));

            // ── Rope hanging from jib tip (over the ship) ──
            float ropeTipZ = _buildingZ - jibLen + 0.04f;
            float ropeH = h * 0.50f;
            p.Add(new ProceduralPartDef("crane_rope", PrimitiveType.Cube,
                new Vector3(cx, axleY - ropeH * 0.5f, ropeTipZ),
                new Vector3(0.016f, ropeH, 0.016f), KFabric));

            // Hook at rope bottom
            p.Add(new ProceduralPartDef("crane_hook", PrimitiveType.Cube,
                new Vector3(cx, axleY - ropeH + 0.02f, ropeTipZ),
                new Vector3(0.04f, 0.04f, 0.03f), KMetal));

            // ── Platform / base plinth under the crane ──
            p.Add(new ProceduralPartDef("crane_base", PrimitiveType.Cube,
                new Vector3(cx, floorY + 0.04f, _buildingZ),
                new Vector3(slotW * 0.40f, 0.06f, slotW * 0.35f), KStone));
        }
*/



