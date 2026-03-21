// Copyright CodeGamified 2025-2026
// MIT License — PopVuj
using UnityEngine;
using CodeGamified.Procedural;
using System.Collections.Generic;

namespace PopVuj.Game
{
    /// <summary>
    /// Procedural blueprint for sewer interiors — the underground space
    /// beneath each surface building.
    ///
    /// Sewers are 2D grids of SewerModules [Width × Height]:
    ///   - Columns run left(0) to right(Width-1)
    ///   - Rows run deepest(0) to surface(Height-1)
    ///
    /// Structure emerges from the grid layout. A 4×2 chapel crypt:
    ///   Row 1: Shrine      | SewerPew    | SewerPew    | Pentagram
    ///   Row 0: Sarcophagus | Tomb        | Sarcophagus | Air
    ///
    /// Air cells make the sewer non-rectangular — same principle
    /// that shapes buildings above ground.
    ///
    /// Each module type has its own visual emitter. Common structure
    /// (back wall, floor slabs, ceiling) wraps occupied cells.
    /// </summary>
    public class SewerBlueprint : IProceduralBlueprint
    {
        private readonly SewerType _type;
        private readonly SewerModule[,] _grid;
        private readonly int _gridCols;
        private readonly int _gridRows;
        private readonly float _originX;
        private readonly float _depth;
        private readonly float _buildingZ;

        private const float Cell = CityRenderer.CellSize;

        // Color keys
        private const string KStone   = "stone";
        private const string KWood    = "wood";
        private const string KMetal   = "metal";
        private const string KWater   = "water";
        private const string KFabric  = "fabric";
        private const string KBone    = "bone";
        private const string KGold    = "gold";

        // Grid cell dimensions (world units)
        private float CellW => Cell;
        private float StoryH => _depth / _gridRows;

        public SewerBlueprint(SewerType type, SewerModule[,] grid, float originX,
                               float depth, float buildingZ = 1.0f)
        {
            _type = type;
            _grid = grid;
            _gridCols = grid.GetLength(0);
            _gridRows = grid.GetLength(1);
            _originX = originX;
            _depth = Mathf.Max(0.10f, depth);
            _buildingZ = buildingZ;
        }

        public string DisplayName => $"Sewer_{_type}_{_gridCols}w";
        public ProceduralLODHint LODHint => ProceduralLODHint.Standard;
        public string PaletteId => "popvuj_sewers";

        public ProceduralPartDef[] GetParts()
        {
            var parts = new List<ProceduralPartDef>(32);

            // ── Common structure (walls, floors, ceiling) ──────
            EmitCommonStructure(parts);

            // ── Per-cell module rendering ───────────────────────
            for (int col = 0; col < _gridCols; col++)
            {
                for (int row = 0; row < _gridRows; row++)
                {
                    SewerModule mod = _grid[col, row];
                    if (mod == SewerModule.Air) continue;

                    float cx = _originX + (col + 0.5f) * CellW;
                    float cy = -_depth + (row + 0.5f) * StoryH;

                    switch (mod)
                    {
                        // Drain
                        case SewerModule.Pipe:         EmitPipe(parts, cx, cy, col, row);         break;
                        case SewerModule.Drip:         EmitDrip(parts, cx, cy, col, row);         break;
                        // Den
                        case SewerModule.Bedroll:      EmitBedroll(parts, cx, cy, col, row);      break;
                        case SewerModule.Barrel:       EmitBarrel(parts, cx, cy, col, row);       break;
                        // Crypt
                        case SewerModule.Sarcophagus:  EmitSarcophagus(parts, cx, cy, col, row);  break;
                        case SewerModule.CryptColumn:  EmitCryptColumn(parts, cx, cy, col, row);  break;
                        case SewerModule.CryptAltar:   EmitCryptAltar(parts, cx, cy, col, row);   break;
                        case SewerModule.Shrine:       EmitShrine(parts, cx, cy, col, row);       break;
                        case SewerModule.Tomb:         EmitTomb(parts, cx, cy, col, row);         break;
                        case SewerModule.Pentagram:    EmitPentagram(parts, cx, cy, col, row);    break;
                        case SewerModule.SewerPew:     EmitSewerPew(parts, cx, cy, col, row);     break;
                        // Tunnel
                        case SewerModule.Rail:         EmitRail(parts, cx, cy, col, row);         break;
                        case SewerModule.SupportBeam:  EmitSupportBeam(parts, cx, cy, col, row);  break;
                        // Cistern
                        case SewerModule.Pool:         EmitPool(parts, cx, cy, col, row);         break;
                        case SewerModule.CisternWall:  EmitCisternWall(parts, cx, cy, col, row);  break;
                        // Bazaar
                        case SewerModule.BazaarStall:  EmitBazaarStall(parts, cx, cy, col, row);  break;
                        case SewerModule.Chest:        EmitChest(parts, cx, cy, col, row);        break;
                        case SewerModule.Lantern:      EmitLantern(parts, cx, cy, col, row);      break;
                        // Drydock
                        case SewerModule.CradleBlock:  EmitCradleBlock(parts, cx, cy, col, row);  break;
                        case SewerModule.DockWater:    EmitDockWater(parts, cx, cy, col, row);    break;
                        // Vault
                        case SewerModule.GoldPile:     EmitGoldPile(parts, cx, cy, col, row);     break;
                        case SewerModule.TaxChest:     EmitTaxChest(parts, cx, cy, col, row);     break;
                        case SewerModule.VaultPillar:  EmitVaultPillar(parts, cx, cy, col, row);  break;
                        case SewerModule.Gate:         EmitGate(parts, cx, cy, col, row);         break;
                        // Canal
                        case SewerModule.CanalWalk:    EmitCanalWalk(parts, cx, cy, col, row);    break;
                        case SewerModule.CanalArch:    EmitCanalArch(parts, cx, cy, col, row);    break;
                        case SewerModule.CanalDrip:    EmitCanalDrip(parts, cx, cy, col, row);    break;
                    }
                }
            }

            return parts.ToArray();
        }

        // ═══════════════════════════════════════════════════════════════
        // COMMON STRUCTURE — walls, floors, ceiling around occupied cells
        // ═══════════════════════════════════════════════════════════════

        private void EmitCommonStructure(List<ProceduralPartDef> p)
        {
            float cW = CellW;
            float sH = StoryH;

            for (int col = 0; col < _gridCols; col++)
            {
                for (int row = 0; row < _gridRows; row++)
                {
                    if (_grid[col, row] == SewerModule.Air) continue;

                    float cellCX = _originX + (col + 0.5f) * cW;
                    float cellCY = -_depth + (row + 0.5f) * sH;
                    float floorY = -_depth + row * sH;

                    // Back wall panel
                    p.Add(new ProceduralPartDef($"backwall_{col}_{row}", PrimitiveType.Cube,
                        new Vector3(cellCX, cellCY, _buildingZ + Cell * 0.45f),
                        new Vector3(cW * 0.90f, sH, 0.04f), KStone));

                    // Floor slab at bottom of cell
                    p.Add(new ProceduralPartDef($"floor_{col}_{row}", PrimitiveType.Cube,
                        new Vector3(cellCX, floorY + 0.02f, _buildingZ),
                        new Vector3(cW * 0.90f, 0.04f, Cell * 0.80f), KStone));
                }
            }

            // Ceiling at surface level
            float totalW = _gridCols * cW;
            float cx = _originX + totalW * 0.5f;
            p.Add(new ProceduralPartDef("ceiling", PrimitiveType.Cube,
                new Vector3(cx, -0.02f, _buildingZ),
                new Vector3(totalW * 0.90f, 0.04f, Cell * 0.80f), KStone));
        }

        // ═══════════════════════════════════════════════════════════════
        // DRAIN MODULES
        // ═══════════════════════════════════════════════════════════════

        private void EmitPipe(List<ProceduralPartDef> p, float cx, float cy, int c, int r)
        {
            float pipeD = Mathf.Min(StoryH * 0.4f, 0.12f);
            p.Add(new ProceduralPartDef($"pipe_{c}_{r}", PrimitiveType.Cube,
                new Vector3(cx, cy, _buildingZ),
                new Vector3(CellW * 0.85f, pipeD, pipeD), KStone));
        }

        private void EmitDrip(List<ProceduralPartDef> p, float cx, float cy, int c, int r)
        {
            p.Add(new ProceduralPartDef($"drip_{c}_{r}", PrimitiveType.Cube,
                new Vector3(cx, cy + StoryH * 0.2f, _buildingZ),
                new Vector3(0.03f, StoryH * 0.3f, 0.03f), KWater));
        }

        // ═══════════════════════════════════════════════════════════════
        // DEN MODULES
        // ═══════════════════════════════════════════════════════════════

        private void EmitBedroll(List<ProceduralPartDef> p, float cx, float cy, int c, int r)
        {
            float baseY = cy - StoryH * 0.35f;
            p.Add(new ProceduralPartDef($"bedroll_{c}_{r}", PrimitiveType.Cube,
                new Vector3(cx, baseY + 0.04f, _buildingZ + Cell * 0.15f),
                new Vector3(CellW * 0.55f, 0.05f, Cell * 0.25f), KFabric));
        }

        private void EmitBarrel(List<ProceduralPartDef> p, float cx, float cy, int c, int r)
        {
            float barrelH = Mathf.Min(StoryH * 0.4f, 0.20f);
            float baseY = cy - StoryH * 0.35f;

            if (ObjectScale.TryAdd(p, $"barrel_{c}_{r}", "barrel",
                new Vector3(cx, baseY + 0.04f, _buildingZ - Cell * 0.2f), KWood))
                return;

            p.Add(new ProceduralPartDef($"barrel_{c}_{r}", PrimitiveType.Cylinder,
                new Vector3(cx, baseY + barrelH * 0.5f + 0.04f, _buildingZ - Cell * 0.2f),
                new Vector3(0.08f, barrelH * 0.5f, 0.08f), KWood));
        }

        // ═══════════════════════════════════════════════════════════════
        // CRYPT MODULES
        // ═══════════════════════════════════════════════════════════════

        private void EmitSarcophagus(List<ProceduralPartDef> p, float cx, float cy, int c, int r)
        {
            float sH = StoryH;
            float sarcW = CellW * 0.6f;
            float sarcH = Mathf.Min(sH * 0.3f, 0.16f);
            float baseY = cy - sH * 0.35f;
            p.Add(new ProceduralPartDef($"sarc_{c}_{r}", PrimitiveType.Cube,
                new Vector3(cx, baseY + sarcH * 0.5f + 0.04f, _buildingZ),
                new Vector3(sarcW, sarcH, Cell * 0.35f), KBone));
            // Lid
            p.Add(new ProceduralPartDef($"sarc_lid_{c}_{r}", PrimitiveType.Cube,
                new Vector3(cx, baseY + sarcH + 0.06f, _buildingZ),
                new Vector3(sarcW * 1.05f, 0.03f, Cell * 0.38f), KStone));
        }

        private void EmitCryptColumn(List<ProceduralPartDef> p, float cx, float cy, int c, int r)
        {
            float colH = StoryH * 0.8f;
            float colW = 0.07f;
            p.Add(new ProceduralPartDef($"col_{c}_{r}", PrimitiveType.Cube,
                new Vector3(cx, cy, _buildingZ + Cell * 0.3f),
                new Vector3(colW, colH, colW), KStone));
        }

        private void EmitCryptAltar(List<ProceduralPartDef> p, float cx, float cy, int c, int r)
        {
            float altarH = StoryH * 0.3f;
            float baseY = cy - StoryH * 0.35f;
            p.Add(new ProceduralPartDef($"c_altar_{c}_{r}", PrimitiveType.Cube,
                new Vector3(cx, baseY + altarH * 0.5f + 0.04f, _buildingZ - Cell * 0.15f),
                new Vector3(CellW * 0.5f, altarH, Cell * 0.2f), KGold));
        }

        private void EmitShrine(List<ProceduralPartDef> p, float cx, float cy, int c, int r)
        {
            float sH = StoryH;
            float cW = CellW;
            float baseY = cy - sH * 0.35f;
            // Shrine niche (recessed back wall area with shelf)
            p.Add(new ProceduralPartDef($"shrine_niche_{c}_{r}", PrimitiveType.Cube,
                new Vector3(cx, cy, _buildingZ + Cell * 0.35f),
                new Vector3(cW * 0.50f, sH * 0.60f, 0.06f), KStone));
            // Relic shelf
            p.Add(new ProceduralPartDef($"shrine_shelf_{c}_{r}", PrimitiveType.Cube,
                new Vector3(cx, baseY + sH * 0.40f, _buildingZ + Cell * 0.30f),
                new Vector3(cW * 0.45f, 0.03f, Cell * 0.15f), KWood));
            // Relic
            p.Add(new ProceduralPartDef($"shrine_relic_{c}_{r}", PrimitiveType.Cube,
                new Vector3(cx, baseY + sH * 0.40f + 0.04f, _buildingZ + Cell * 0.30f),
                new Vector3(0.04f, 0.06f, 0.04f), KGold));
        }

        private void EmitTomb(List<ProceduralPartDef> p, float cx, float cy, int c, int r)
        {
            float sH = StoryH;
            float tombW = CellW * 0.7f;
            float tombH = sH * 0.15f;
            float baseY = cy - sH * 0.35f;
            // Flat stone slab flush with floor
            p.Add(new ProceduralPartDef($"tomb_{c}_{r}", PrimitiveType.Cube,
                new Vector3(cx, baseY + tombH * 0.5f + 0.04f, _buildingZ),
                new Vector3(tombW, tombH, Cell * 0.45f), KStone));
            // Inscription line
            p.Add(new ProceduralPartDef($"tomb_mark_{c}_{r}", PrimitiveType.Cube,
                new Vector3(cx, baseY + tombH + 0.05f, _buildingZ),
                new Vector3(tombW * 0.6f, 0.01f, Cell * 0.25f), KBone));
        }

        private void EmitPentagram(List<ProceduralPartDef> p, float cx, float cy, int c, int r)
        {
            float sH = StoryH;
            float baseY = cy - sH * 0.35f;
            float circleR = CellW * 0.35f;
            // Floor circle marking
            p.Add(new ProceduralPartDef($"penta_ring_{c}_{r}", PrimitiveType.Cylinder,
                new Vector3(cx, baseY + 0.015f, _buildingZ),
                new Vector3(circleR, 0.005f, circleR), KGold));
            // Candles at 4 corners
            float candleOff = circleR * 0.7f;
            for (int i = 0; i < 4; i++)
            {
                float dx = (i % 2 == 0 ? -1 : 1) * candleOff;
                float dz = (i < 2 ? -1 : 1) * candleOff * 0.5f;
                p.Add(new ProceduralPartDef($"penta_candle_{c}_{r}_{i}", PrimitiveType.Cube,
                    new Vector3(cx + dx, baseY + 0.04f + 0.03f, _buildingZ + dz),
                    new Vector3(0.015f, 0.06f, 0.015f), KBone));
            }
        }

        private void EmitSewerPew(List<ProceduralPartDef> p, float cx, float cy, int c, int r)
        {
            float sH = StoryH;
            float cW = CellW;
            float baseY = cy - sH * 0.35f;
            float benchH = sH * 0.20f;
            // Stone bench
            p.Add(new ProceduralPartDef($"spew_seat_{c}_{r}", PrimitiveType.Cube,
                new Vector3(cx, baseY + benchH * 0.5f + 0.04f, _buildingZ),
                new Vector3(cW * 0.70f, benchH, Cell * 0.25f), KStone));
        }

        // ═══════════════════════════════════════════════════════════════
        // TUNNEL MODULES
        // ═══════════════════════════════════════════════════════════════

        private void EmitRail(List<ProceduralPartDef> p, float cx, float cy, int c, int r)
        {
            float baseY = cy - StoryH * 0.45f;
            float railH = 0.03f;
            p.Add(new ProceduralPartDef($"rail_l_{c}_{r}", PrimitiveType.Cube,
                new Vector3(cx, baseY + railH * 0.5f + 0.02f, _buildingZ - Cell * 0.12f),
                new Vector3(CellW * 0.85f, railH, 0.03f), KMetal));
            p.Add(new ProceduralPartDef($"rail_r_{c}_{r}", PrimitiveType.Cube,
                new Vector3(cx, baseY + railH * 0.5f + 0.02f, _buildingZ + Cell * 0.12f),
                new Vector3(CellW * 0.85f, railH, 0.03f), KMetal));
            // Tie
            p.Add(new ProceduralPartDef($"tie_{c}_{r}", PrimitiveType.Cube,
                new Vector3(cx, baseY + 0.016f, _buildingZ),
                new Vector3(0.04f, 0.03f, Cell * 0.3f), KWood));
        }

        private void EmitSupportBeam(List<ProceduralPartDef> p, float cx, float cy, int c, int r)
        {
            float beamH = StoryH * 0.85f;
            p.Add(new ProceduralPartDef($"beam_{c}_{r}", PrimitiveType.Cube,
                new Vector3(cx, cy, _buildingZ),
                new Vector3(0.04f, beamH, 0.04f), KWood));
        }

        // ═══════════════════════════════════════════════════════════════
        // CISTERN MODULES
        // ═══════════════════════════════════════════════════════════════

        private void EmitPool(List<ProceduralPartDef> p, float cx, float cy, int c, int r)
        {
            float waterH = StoryH * 0.6f;
            float baseY = cy - StoryH * 0.45f;
            p.Add(new ProceduralPartDef($"pool_{c}_{r}", PrimitiveType.Cube,
                new Vector3(cx, baseY + waterH * 0.5f + 0.02f, _buildingZ),
                new Vector3(CellW * 0.80f, waterH, Cell * 0.70f), KWater));
        }

        private void EmitCisternWall(List<ProceduralPartDef> p, float cx, float cy, int c, int r)
        {
            float wallH = StoryH * 0.85f;
            p.Add(new ProceduralPartDef($"ciswall_{c}_{r}", PrimitiveType.Cube,
                new Vector3(cx, cy, _buildingZ),
                new Vector3(0.06f, wallH, Cell * 0.80f), KStone));
        }

        // ═══════════════════════════════════════════════════════════════
        // BAZAAR MODULES
        // ═══════════════════════════════════════════════════════════════

        private void EmitBazaarStall(List<ProceduralPartDef> p, float cx, float cy, int c, int r)
        {
            float sH = StoryH;
            float counterH = Mathf.Min(sH * 0.25f, 0.12f);
            float baseY = cy - sH * 0.35f;
            float stallW = CellW * 0.60f;

            if (ObjectScale.TryAdd(p, $"bstall_{c}_{r}", "trestle",
                new Vector3(cx, baseY + 0.04f, _buildingZ), KWood))
                return;

            p.Add(new ProceduralPartDef($"bstall_{c}_{r}", PrimitiveType.Cube,
                new Vector3(cx, baseY + counterH * 0.5f + 0.04f, _buildingZ),
                new Vector3(stallW, counterH, Cell * 0.25f), KWood));
        }

        private void EmitChest(List<ProceduralPartDef> p, float cx, float cy, int c, int r)
        {
            float sH = StoryH;
            float chestS = Mathf.Min(sH * 0.25f, 0.10f);
            float baseY = cy - sH * 0.35f;

            if (ObjectScale.TryAdd(p, $"chest_{c}_{r}", "whitewashed_linen_chest",
                new Vector3(cx, baseY + 0.04f, _buildingZ), KMetal))
                return;

            p.Add(new ProceduralPartDef($"chest_{c}_{r}", PrimitiveType.Cube,
                new Vector3(cx, baseY + chestS * 0.5f + 0.04f, _buildingZ),
                new Vector3(chestS, chestS, chestS), KMetal));
        }

        private void EmitLantern(List<ProceduralPartDef> p, float cx, float cy, int c, int r)
        {
            if (ObjectScale.TryAdd(p, $"lantern_{c}_{r}", "lantern_large",
                new Vector3(cx, cy + StoryH * 0.2f, _buildingZ), KGold))
                return;

            p.Add(new ProceduralPartDef($"lantern_{c}_{r}", PrimitiveType.Cube,
                new Vector3(cx, cy + StoryH * 0.3f, _buildingZ),
                new Vector3(0.05f, 0.05f, 0.05f), KGold));
        }

        // ═══════════════════════════════════════════════════════════════
        // DRYDOCK MODULES
        // ═══════════════════════════════════════════════════════════════

        private void EmitCradleBlock(List<ProceduralPartDef> p, float cx, float cy, int c, int r)
        {
            float blockH = Mathf.Min(StoryH * 0.3f, 0.12f);
            float baseY = cy - StoryH * 0.35f;
            p.Add(new ProceduralPartDef($"cradle_{c}_{r}", PrimitiveType.Cube,
                new Vector3(cx, baseY + blockH * 0.5f + 0.04f, _buildingZ),
                new Vector3(CellW * 0.40f, blockH, Cell * 0.20f), KWood));
        }

        private void EmitDockWater(List<ProceduralPartDef> p, float cx, float cy, int c, int r)
        {
            float waterH = StoryH * 0.5f;
            float baseY = cy - StoryH * 0.45f;
            p.Add(new ProceduralPartDef($"dwater_{c}_{r}", PrimitiveType.Cube,
                new Vector3(cx, baseY + waterH * 0.5f + 0.02f, _buildingZ),
                new Vector3(CellW * 0.80f, waterH, Cell * 0.70f), KWater));
        }

        // ═══════════════════════════════════════════════════════════════
        // VAULT MODULES
        // ═══════════════════════════════════════════════════════════════

        private void EmitGoldPile(List<ProceduralPartDef> p, float cx, float cy, int c, int r)
        {
            float sH = StoryH;
            float baseY = cy - sH * 0.35f;
            float baseW = Mathf.Min(CellW * 0.5f, 0.20f);
            float layerH = 0.04f;
            // Pyramid stack of coins
            p.Add(new ProceduralPartDef($"gold_base_{c}_{r}", PrimitiveType.Cube,
                new Vector3(cx, baseY + 0.04f + layerH * 0.5f, _buildingZ),
                new Vector3(baseW, layerH, baseW * 0.8f), KGold));
            p.Add(new ProceduralPartDef($"gold_mid_{c}_{r}", PrimitiveType.Cube,
                new Vector3(cx, baseY + 0.04f + layerH * 1.5f, _buildingZ),
                new Vector3(baseW * 0.70f, layerH, baseW * 0.55f), KGold));
            p.Add(new ProceduralPartDef($"gold_peak_{c}_{r}", PrimitiveType.Cube,
                new Vector3(cx, baseY + 0.04f + layerH * 2.5f, _buildingZ),
                new Vector3(baseW * 0.40f, layerH, baseW * 0.30f), KGold));
        }

        private void EmitTaxChest(List<ProceduralPartDef> p, float cx, float cy, int c, int r)
        {
            float sH = StoryH;
            float baseY = cy - sH * 0.35f;
            float cW = Mathf.Min(CellW * 0.50f, 0.16f);
            float cH = Mathf.Min(sH * 0.20f, 0.10f);
            float cD = cW * 0.65f;
            float chBaseY = baseY + 0.04f + cH * 0.5f;

            if (ObjectScale.TryAdd(p, $"tchest_{c}_{r}", "whitewashed_linen_chest",
                new Vector3(cx, baseY + 0.04f, _buildingZ + Cell * 0.22f), KWood))
                return;

            // Chest body
            p.Add(new ProceduralPartDef($"tchest_{c}_{r}", PrimitiveType.Cube,
                new Vector3(cx, chBaseY, _buildingZ + Cell * 0.22f),
                new Vector3(cW, cH, cD), KWood));
            // Iron band
            p.Add(new ProceduralPartDef($"tchest_band_{c}_{r}", PrimitiveType.Cube,
                new Vector3(cx, chBaseY, _buildingZ + Cell * 0.22f),
                new Vector3(cW + 0.01f, cH * 0.25f, cD + 0.01f), KMetal));
            // Lock
            p.Add(new ProceduralPartDef($"tchest_lock_{c}_{r}", PrimitiveType.Cube,
                new Vector3(cx, chBaseY + cH * 0.15f, _buildingZ + Cell * 0.22f - cD * 0.5f - 0.005f),
                new Vector3(0.025f, 0.025f, 0.015f), KMetal));
        }

        private void EmitVaultPillar(List<ProceduralPartDef> p, float cx, float cy, int c, int r)
        {
            float pillarH = StoryH * 0.85f;
            float pillarW = 0.06f;
            p.Add(new ProceduralPartDef($"vpillar_{c}_{r}", PrimitiveType.Cube,
                new Vector3(cx, cy, _buildingZ + Cell * 0.30f),
                new Vector3(pillarW, pillarH, pillarW), KStone));
            // Iron band
            p.Add(new ProceduralPartDef($"vband_{c}_{r}", PrimitiveType.Cube,
                new Vector3(cx, cy, _buildingZ + Cell * 0.30f),
                new Vector3(pillarW + 0.02f, 0.03f, pillarW + 0.02f), KMetal));
        }

        private void EmitGate(List<ProceduralPartDef> p, float cx, float cy, int c, int r)
        {
            float sH = StoryH;
            float gateH = sH * 0.75f;
            float gateW = CellW * 0.70f;
            float gateZ = _buildingZ - Cell * 0.35f;
            // Frame
            p.Add(new ProceduralPartDef($"gate_top_{c}_{r}", PrimitiveType.Cube,
                new Vector3(cx, cy + gateH * 0.4f, gateZ),
                new Vector3(gateW + 0.08f, 0.05f, 0.04f), KMetal));
            p.Add(new ProceduralPartDef($"gate_l_{c}_{r}", PrimitiveType.Cube,
                new Vector3(cx - gateW * 0.5f - 0.02f, cy, gateZ),
                new Vector3(0.05f, gateH, 0.04f), KMetal));
            p.Add(new ProceduralPartDef($"gate_r_{c}_{r}", PrimitiveType.Cube,
                new Vector3(cx + gateW * 0.5f + 0.02f, cy, gateZ),
                new Vector3(0.05f, gateH, 0.04f), KMetal));
            // Bars
            int barCount = Mathf.Max(3, Mathf.RoundToInt(gateW / 0.06f));
            float barSpacing = gateW / (barCount + 1);
            for (int i = 1; i <= barCount; i++)
            {
                float bx = cx - gateW * 0.5f + barSpacing * i;
                p.Add(new ProceduralPartDef($"gate_bar_{c}_{r}_{i}", PrimitiveType.Cube,
                    new Vector3(bx, cy, gateZ),
                    new Vector3(0.018f, gateH * 0.95f, 0.018f), KMetal));
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // CANAL MODULES
        // ═══════════════════════════════════════════════════════════════

        private void EmitCanalWalk(List<ProceduralPartDef> p, float cx, float cy, int c, int r)
        {
            float waterH = StoryH * 0.15f;
            float baseY = cy - StoryH * 0.45f;
            // Shallow water channel
            p.Add(new ProceduralPartDef($"cwater_{c}_{r}", PrimitiveType.Cube,
                new Vector3(cx, baseY + 0.04f + waterH * 0.5f, _buildingZ),
                new Vector3(CellW * 0.40f, waterH, Cell * 0.30f), KWater));
        }

        private void EmitCanalArch(List<ProceduralPartDef> p, float cx, float cy, int c, int r)
        {
            float archH = StoryH * 0.20f;
            p.Add(new ProceduralPartDef($"carch_{c}_{r}", PrimitiveType.Cube,
                new Vector3(cx, cy + StoryH * 0.35f, _buildingZ),
                new Vector3(CellW * 0.15f, archH, Cell * 0.75f), KStone));
        }

        private void EmitCanalDrip(List<ProceduralPartDef> p, float cx, float cy, int c, int r)
        {
            p.Add(new ProceduralPartDef($"cdrip_{c}_{r}", PrimitiveType.Cube,
                new Vector3(cx, cy + StoryH * 0.2f, _buildingZ),
                new Vector3(0.03f, StoryH * 0.3f, 0.03f), KWater));
        }
    }
}
