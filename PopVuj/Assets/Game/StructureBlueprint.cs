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
    /// Emits parameterized parts based on building type and tile width:
    ///   Chapel  → lectern + N pews (N scales with width)
    ///   House   → beds + table
    ///   Workshop → anvil / workbenches
    ///   Farm    → troughs / plots
    ///   Market  → stalls + crates
    ///   Fountain → basin + column
    ///
    /// Buildings are side-view cross-sections. Interior objects sit on
    /// the building floor (Y = RoadH) and span the building's X extent.
    /// Z positions place furniture at BuildingZ (behind road).
    ///
    /// All dimensions scale smoothly with building width — wider buildings
    /// don't just duplicate; objects grow and spread proportionally.
    /// </summary>
    public class StructureBlueprint : IProceduralBlueprint
    {
        private readonly CellType _type;
        private readonly int _buildingWidth;
        private readonly float _originX;    // world X of leftmost tile edge
        private readonly float _roadH;      // road surface Y
        private readonly float _buildingZ;  // Z depth for building furniture
        private readonly PierFixture[] _pierFixtures; // per-slot fixtures for Pier buildings

        // Cell size from CityRenderer
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

        public StructureBlueprint(CellType type, int buildingWidth, float originX,
                                   float roadH = 0.3f, float buildingZ = 1.0f,
                                   PierFixture[] pierFixtures = null)
        {
            _type = type;
            _buildingWidth = Mathf.Max(1, buildingWidth);
            _originX = originX;
            _roadH = roadH;
            _buildingZ = buildingZ;
            _pierFixtures = pierFixtures;
        }

        public string DisplayName => $"{_type}_{_buildingWidth}w";
        public ProceduralLODHint LODHint => ProceduralLODHint.Standard;
        public string PaletteId => "popvuj_structures";

        public ProceduralPartDef[] GetParts()
        {
            var parts = new List<ProceduralPartDef>(16);
            float totalW = _buildingWidth * Cell;
            float cx = _originX + totalW * 0.5f; // center X

            switch (_type)
            {
                case CellType.Chapel:   EmitChapel(parts, totalW, cx);   break;
                case CellType.House:    EmitHouse(parts, totalW, cx);    break;
                case CellType.Workshop: EmitWorkshop(parts, totalW, cx); break;
                case CellType.Farm:     EmitFarm(parts, totalW, cx);     break;
                case CellType.Market:   EmitMarket(parts, totalW, cx);   break;
                case CellType.Fountain: EmitFountain(parts, totalW, cx); break;
                case CellType.Shipyard: EmitShipyard(parts, totalW, cx); break;
                case CellType.Pier:     EmitPier(parts, totalW, cx);     break;
            }

            return parts.ToArray();
        }

        // ═══════════════════════════════════════════════════════════════
        // CHAPEL — lectern at left, pews filling the width
        // ═══════════════════════════════════════════════════════════════

        private void EmitChapel(List<ProceduralPartDef> p, float totalW, float cx)
        {
            float floorY = _roadH;
            float h = GetHeight();
            // Back wall — thin vertical slab at full height
            p.Add(new ProceduralPartDef("chapel_backwall", PrimitiveType.Cube,
                new Vector3(cx, floorY + h * 0.5f, _buildingZ + Cell * 0.45f),
                new Vector3(totalW * 0.90f, h, 0.04f), KWall));

            // Floor
            p.Add(new ProceduralPartDef("chapel_floor", PrimitiveType.Cube,
                new Vector3(cx, floorY + 0.02f, _buildingZ),
                new Vector3(totalW * 0.88f, 0.04f, Cell * 0.85f), KStone));

            // Lectern — at the left 15% of the building
            float lecternX = _originX + totalW * 0.12f;
            float lecternW = Mathf.Lerp(0.08f, 0.12f, (_buildingWidth - 1) / 4f);
            float lecternH = Mathf.Lerp(0.24f, 0.32f, (_buildingWidth - 1) / 4f);
            p.Add(new ProceduralPartDef("lectern_base", PrimitiveType.Cube,
                new Vector3(lecternX, floorY + lecternH * 0.5f, _buildingZ),
                new Vector3(lecternW, lecternH, lecternW * 0.8f), KWood));

            // Cross / holy symbol above lectern
            float crossH = Mathf.Lerp(0.12f, 0.20f, (_buildingWidth - 1) / 4f);
            p.Add(new ProceduralPartDef("cross_vert", PrimitiveType.Cube,
                new Vector3(lecternX, floorY + lecternH + crossH * 0.5f + 0.04f, _buildingZ + Cell * 0.35f),
                new Vector3(0.024f, crossH, 0.024f), KGold));
            p.Add(new ProceduralPartDef("cross_horiz", PrimitiveType.Cube,
                new Vector3(lecternX, floorY + lecternH + crossH * 0.7f + 0.04f, _buildingZ + Cell * 0.35f),
                new Vector3(crossH * 0.6f, 0.024f, 0.024f), KGold));

            // Pews — fill the remaining 75% of width
            float pewStartX = _originX + totalW * 0.28f;
            float pewEndX = _originX + totalW * 0.95f;
            float pewRegion = pewEndX - pewStartX;
            int pewCount = Mathf.Max(1, _buildingWidth * 2);
            float pewSpacing = pewRegion / pewCount;
            float pewW = pewSpacing * 0.65f;
            float pewH = Mathf.Lerp(0.10f, 0.14f, (_buildingWidth - 1) / 4f);
            float pewD = Cell * 0.3f;

            for (int i = 0; i < pewCount; i++)
            {
                float px = pewStartX + pewSpacing * (i + 0.5f);
                // Seat
                p.Add(new ProceduralPartDef($"pew_seat_{i}", PrimitiveType.Cube,
                    new Vector3(px, floorY + pewH * 0.5f, _buildingZ),
                    new Vector3(pewW, pewH, pewD), KWood));
                // Back rest
                p.Add(new ProceduralPartDef($"pew_back_{i}", PrimitiveType.Cube,
                    new Vector3(px, floorY + pewH + 0.06f, _buildingZ + pewD * 0.4f),
                    new Vector3(pewW, 0.12f, 0.03f), KWood));
            }

            // Roof beam
            p.Add(new ProceduralPartDef("chapel_roof", PrimitiveType.Cube,
                new Vector3(cx, floorY + h - 0.04f, _buildingZ),
                new Vector3(totalW * 0.90f, 0.06f, Cell * 0.85f), KRoof));
        }

        // ═══════════════════════════════════════════════════════════════
        // HOUSE — beds along the back wall, table in center
        // ═══════════════════════════════════════════════════════════════

        private void EmitHouse(List<ProceduralPartDef> p, float totalW, float cx)
        {
            float floorY = _roadH;
            float h = GetHeight();

            // Back wall
            p.Add(new ProceduralPartDef("house_backwall", PrimitiveType.Cube,
                new Vector3(cx, floorY + h * 0.5f, _buildingZ + Cell * 0.45f),
                new Vector3(totalW * 0.90f, h, 0.04f), KWall));

            // Floor
            p.Add(new ProceduralPartDef("house_floor", PrimitiveType.Cube,
                new Vector3(cx, floorY + 0.02f, _buildingZ),
                new Vector3(totalW * 0.88f, 0.04f, Cell * 0.85f), KWood));

            // Beds — one per two residents slot, evenly spaced
            int bedCount = Mathf.Max(1, _buildingWidth);
            float bedRegion = totalW * 0.9f;
            float bedSpacing = bedRegion / bedCount;
            float bedW = bedSpacing * 0.7f;
            float bedH = 0.08f;
            float bedD = Cell * 0.35f;

            for (int i = 0; i < bedCount; i++)
            {
                float bx = _originX + totalW * 0.05f + bedSpacing * (i + 0.5f);
                // Bed frame
                p.Add(new ProceduralPartDef($"bed_frame_{i}", PrimitiveType.Cube,
                    new Vector3(bx, floorY + bedH * 0.5f + 0.04f, _buildingZ + Cell * 0.2f),
                    new Vector3(bedW, bedH, bedD), KWood));
                // Blanket
                p.Add(new ProceduralPartDef($"bed_blanket_{i}", PrimitiveType.Cube,
                    new Vector3(bx, floorY + bedH + 0.06f, _buildingZ + Cell * 0.2f),
                    new Vector3(bedW * 0.9f, 0.04f, bedD * 0.85f), KFabric));
            }

            // Table in center (only if width >= 2)
            if (_buildingWidth >= 2)
            {
                float tableW = totalW * 0.3f;
                float tableH = 0.16f;
                p.Add(new ProceduralPartDef("table", PrimitiveType.Cube,
                    new Vector3(cx, floorY + tableH * 0.5f + 0.04f, _buildingZ - Cell * 0.1f),
                    new Vector3(tableW, tableH, Cell * 0.2f), KWood));
            }

            // Roof
            p.Add(new ProceduralPartDef("house_roof", PrimitiveType.Cube,
                new Vector3(cx, floorY + h - 0.04f, _buildingZ),
                new Vector3(totalW * 0.90f, 0.06f, Cell * 0.85f), KRoof));
        }

        // ═══════════════════════════════════════════════════════════════
        // WORKSHOP — workbenches with anvils
        // ═══════════════════════════════════════════════════════════════

        private void EmitWorkshop(List<ProceduralPartDef> p, float totalW, float cx)
        {
            float floorY = _roadH;
            float h = GetHeight();

            // Back wall
            p.Add(new ProceduralPartDef("ws_backwall", PrimitiveType.Cube,
                new Vector3(cx, floorY + h * 0.5f, _buildingZ + Cell * 0.45f),
                new Vector3(totalW * 0.90f, h, 0.04f), KWall));

            // Floor
            p.Add(new ProceduralPartDef("ws_floor", PrimitiveType.Cube,
                new Vector3(cx, floorY + 0.02f, _buildingZ),
                new Vector3(totalW * 0.88f, 0.04f, Cell * 0.85f), KStone));

            // Workbenches — one per worker pair
            int benchCount = Mathf.Max(1, _buildingWidth);
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
                case CellType.Shipyard: return 2f;
                case CellType.Pier:     return 1f;
                default:                return 1f;
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
        // ═══════════════════════════════════════════════════════════════

        private void EmitCraneFixture(List<ProceduralPartDef> p, float cx, float slotW, float floorY)
        {
            float h = 1.8f; // crane height

            // Two A-frame legs
            float legW = 0.06f;
            float legH = h * 0.85f;
            p.Add(new ProceduralPartDef("crane_leg_l", PrimitiveType.Cube,
                new Vector3(cx - slotW * 0.15f, floorY + legH * 0.5f + 0.06f, _buildingZ),
                new Vector3(legW, legH, legW), KWood));
            p.Add(new ProceduralPartDef("crane_leg_r", PrimitiveType.Cube,
                new Vector3(cx + slotW * 0.15f, floorY + legH * 0.5f + 0.06f, _buildingZ),
                new Vector3(legW, legH, legW), KWood));

            // Cross beam at top
            p.Add(new ProceduralPartDef("crane_beam", PrimitiveType.Cube,
                new Vector3(cx, floorY + legH + 0.04f, _buildingZ),
                new Vector3(slotW * 0.35f, 0.06f, 0.06f), KWood));

            // Swing arm extending outward (right, over water)
            float armW = slotW * 0.6f;
            p.Add(new ProceduralPartDef("crane_arm", PrimitiveType.Cube,
                new Vector3(cx + armW * 0.3f, floorY + legH + 0.08f, _buildingZ),
                new Vector3(armW, 0.05f, 0.05f), KWood));

            // Rope hanging from arm tip
            float ropeX = cx + armW * 0.55f;
            float ropeH = h * 0.5f;
            p.Add(new ProceduralPartDef("crane_rope", PrimitiveType.Cube,
                new Vector3(ropeX, floorY + legH - ropeH * 0.5f, _buildingZ),
                new Vector3(0.016f, ropeH, 0.016f), KFabric));

            // Hook at rope bottom
            p.Add(new ProceduralPartDef("crane_hook", PrimitiveType.Cube,
                new Vector3(ropeX, floorY + legH - ropeH + 0.02f, _buildingZ),
                new Vector3(0.04f, 0.04f, 0.03f), KMetal));
        }

        private void EmitCannonFixture(List<ProceduralPartDef> p, float cx, float slotW, float floorY, int idx)
        {
            // Cannon barrel on a wooden carriage
            float barrelL = slotW * 0.5f;
            p.Add(new ProceduralPartDef($"cannon_barrel_{idx}", PrimitiveType.Cube,
                new Vector3(cx, floorY + 0.12f, _buildingZ - Cell * 0.18f),
                new Vector3(barrelL, 0.08f, 0.08f), KMetal));

            // Carriage block
            p.Add(new ProceduralPartDef($"cannon_carriage_{idx}", PrimitiveType.Cube,
                new Vector3(cx, floorY + 0.06f, _buildingZ - Cell * 0.18f),
                new Vector3(slotW * 0.35f, 0.08f, slotW * 0.3f), KWood));
        }

        private void EmitFishingFixture(List<ProceduralPartDef> p, float cx, float slotW, float floorY, int idx)
        {
            // Fishing pole — thin angled rod
            float poleH = 0.70f;
            p.Add(new ProceduralPartDef($"fishing_pole_{idx}", PrimitiveType.Cube,
                new Vector3(cx, floorY + poleH * 0.5f + 0.04f, _buildingZ - Cell * 0.25f),
                new Vector3(0.02f, poleH, 0.02f), KWood));

            // Line dangling from tip
            p.Add(new ProceduralPartDef($"fishing_line_{idx}", PrimitiveType.Cube,
                new Vector3(cx + 0.04f, floorY + 0.04f, _buildingZ - Cell * 0.30f),
                new Vector3(0.008f, poleH * 0.6f, 0.008f), KFabric));
        }
    }
}
