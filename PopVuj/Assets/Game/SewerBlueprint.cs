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
    /// Sewer type determines the interior layout:
    ///   Drain   → thin pipe segments
    ///   Den     → makeshift shelters, barrels
    ///   Crypt   → sarcophagi, columns, altar
    ///   Tunnel  → rail / cart track, support beams
    ///   Cistern → water pool, stone walls
    ///   Bazaar  → stalls, chests, lanterns
    ///   Drydock → flooded excavation, hull cradle
    ///   Vault   → reinforced treasure cellar, gold horde, tax chests
    ///
    /// All dimensions parameterize from building width and sewer depth.
    /// </summary>
    public class SewerBlueprint : IProceduralBlueprint
    {
        private readonly SewerType _type;
        private readonly int _buildingWidth;
        private readonly float _originX;
        private readonly float _depth;       // sewer depth in world units (Y extent below 0)
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

        public SewerBlueprint(SewerType type, int buildingWidth, float originX,
                               float depth, float buildingZ = 1.0f)
        {
            _type = type;
            _buildingWidth = Mathf.Max(1, buildingWidth);
            _originX = originX;
            _depth = Mathf.Max(0.10f, depth);
            _buildingZ = buildingZ;
        }

        public string DisplayName => $"Sewer_{_type}_{_buildingWidth}w";
        public ProceduralLODHint LODHint => ProceduralLODHint.Standard;
        public string PaletteId => "popvuj_sewers";

        public ProceduralPartDef[] GetParts()
        {
            var parts = new List<ProceduralPartDef>(12);
            float totalW = _buildingWidth * Cell;
            float cx = _originX + totalW * 0.5f;

            switch (_type)
            {
                case SewerType.Drain:   EmitDrain(parts, totalW, cx);   break;
                case SewerType.Den:     EmitDen(parts, totalW, cx);     break;
                case SewerType.Crypt:   EmitCrypt(parts, totalW, cx);   break;
                case SewerType.Tunnel:  EmitTunnel(parts, totalW, cx);  break;
                case SewerType.Cistern: EmitCistern(parts, totalW, cx); break;
                case SewerType.Bazaar:  EmitBazaar(parts, totalW, cx);  break;
                case SewerType.Drydock: EmitDrydock(parts, totalW, cx); break;
                case SewerType.Vault:   EmitVault(parts, totalW, cx);   break;
            }

            return parts.ToArray();
        }

        // ═══════════════════════════════════════════════════════════════
        // DRAIN — simple pipe segments
        // ═══════════════════════════════════════════════════════════════

        private void EmitDrain(List<ProceduralPartDef> p, float totalW, float cx)
        {
            // Pipe running horizontally through the drain
            float pipeD = Mathf.Min(_depth * 0.5f, 0.12f);
            p.Add(new ProceduralPartDef("pipe", PrimitiveType.Cube,
                new Vector3(cx, -_depth * 0.5f, _buildingZ),
                new Vector3(totalW * 0.85f, pipeD, pipeD), KStone));

            // Drip at the center
            p.Add(new ProceduralPartDef("drip", PrimitiveType.Cube,
                new Vector3(cx, -_depth * 0.15f, _buildingZ),
                new Vector3(0.03f, _depth * 0.3f, 0.03f), KWater));
        }

        // ═══════════════════════════════════════════════════════════════
        // DEN — makeshift shelters, barrels
        // ═══════════════════════════════════════════════════════════════

        private void EmitDen(List<ProceduralPartDef> p, float totalW, float cx)
        {
            // Floor
            p.Add(new ProceduralPartDef("den_floor", PrimitiveType.Cube,
                new Vector3(cx, -_depth + 0.02f, _buildingZ),
                new Vector3(totalW * 0.85f, 0.04f, Cell * 0.8f), KStone));

            // Bed rolls — one per tile width
            int bedCount = _buildingWidth;
            float bedSpacing = totalW * 0.85f / bedCount;
            for (int i = 0; i < bedCount; i++)
            {
                float bx = _originX + totalW * 0.075f + bedSpacing * (i + 0.5f);
                p.Add(new ProceduralPartDef($"bedroll_{i}", PrimitiveType.Cube,
                    new Vector3(bx, -_depth + 0.08f, _buildingZ + Cell * 0.15f),
                    new Vector3(bedSpacing * 0.55f, 0.05f, Cell * 0.25f), KFabric));
            }

            // Barrel at one end
            float barrelH = Mathf.Min(_depth * 0.4f, 0.20f);
            p.Add(new ProceduralPartDef("barrel", PrimitiveType.Cylinder,
                new Vector3(_originX + totalW * 0.88f, -_depth + barrelH * 0.5f + 0.04f, _buildingZ - Cell * 0.2f),
                new Vector3(0.08f, barrelH * 0.5f, 0.08f), KWood));
        }

        // ═══════════════════════════════════════════════════════════════
        // CRYPT — sarcophagi, columns, altar
        // ═══════════════════════════════════════════════════════════════

        private void EmitCrypt(List<ProceduralPartDef> p, float totalW, float cx)
        {
            // Stone floor
            p.Add(new ProceduralPartDef("crypt_floor", PrimitiveType.Cube,
                new Vector3(cx, -_depth + 0.02f, _buildingZ),
                new Vector3(totalW * 0.85f, 0.04f, Cell * 0.8f), KStone));

            // Columns at intervals
            int colCount = Mathf.Max(2, _buildingWidth + 1);
            float colSpacing = totalW * 0.85f / (colCount - 1);
            float colH = _depth * 0.8f;
            float colW = Mathf.Lerp(0.05f, 0.08f, (_buildingWidth - 1) / 4f);

            for (int i = 0; i < colCount; i++)
            {
                float colX = _originX + totalW * 0.075f + colSpacing * i;
                p.Add(new ProceduralPartDef($"column_{i}", PrimitiveType.Cube,
                    new Vector3(colX, -_depth * 0.5f, _buildingZ + Cell * 0.3f),
                    new Vector3(colW, colH, colW), KStone));
            }

            // Sarcophagi — one per tile width
            int sarcCount = Mathf.Max(1, _buildingWidth);
            float sarcSpacing = totalW * 0.7f / sarcCount;
            for (int i = 0; i < sarcCount; i++)
            {
                float sx = _originX + totalW * 0.15f + sarcSpacing * (i + 0.5f);
                float sarcW = sarcSpacing * 0.6f;
                float sarcH = Mathf.Min(_depth * 0.25f, 0.16f);
                p.Add(new ProceduralPartDef($"sarcophagus_{i}", PrimitiveType.Cube,
                    new Vector3(sx, -_depth + 0.04f + sarcH * 0.5f, _buildingZ),
                    new Vector3(sarcW, sarcH, Cell * 0.35f), KBone));
            }

            // Altar at center (if wide enough)
            if (_buildingWidth >= 2)
            {
                float altarH = _depth * 0.3f;
                p.Add(new ProceduralPartDef("altar", PrimitiveType.Cube,
                    new Vector3(cx, -_depth + 0.04f + altarH * 0.5f, _buildingZ - Cell * 0.15f),
                    new Vector3(totalW * 0.2f, altarH, Cell * 0.2f), KGold));
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // TUNNEL — rail track, support beams
        // ═══════════════════════════════════════════════════════════════

        private void EmitTunnel(List<ProceduralPartDef> p, float totalW, float cx)
        {
            // Rail track along the floor
            float railH = 0.03f;
            p.Add(new ProceduralPartDef("rail_l", PrimitiveType.Cube,
                new Vector3(cx, -_depth + 0.02f + railH * 0.5f, _buildingZ - Cell * 0.12f),
                new Vector3(totalW * 0.85f, railH, 0.03f), KMetal));
            p.Add(new ProceduralPartDef("rail_r", PrimitiveType.Cube,
                new Vector3(cx, -_depth + 0.02f + railH * 0.5f, _buildingZ + Cell * 0.12f),
                new Vector3(totalW * 0.85f, railH, 0.03f), KMetal));

            // Ties
            int tieCount = Mathf.Max(2, _buildingWidth * 3);
            float tieSpacing = totalW * 0.85f / tieCount;
            for (int i = 0; i < tieCount; i++)
            {
                float tx = _originX + totalW * 0.075f + tieSpacing * (i + 0.5f);
                p.Add(new ProceduralPartDef($"tie_{i}", PrimitiveType.Cube,
                    new Vector3(tx, -_depth + 0.016f, _buildingZ),
                    new Vector3(0.04f, 0.03f, Cell * 0.3f), KWood));
            }

            // Support beams overhead
            int beamCount = Mathf.Max(2, _buildingWidth + 1);
            float beamSpacing = totalW * 0.85f / (beamCount - 1);
            for (int i = 0; i < beamCount; i++)
            {
                float bx = _originX + totalW * 0.075f + beamSpacing * i;
                p.Add(new ProceduralPartDef($"beam_{i}", PrimitiveType.Cube,
                    new Vector3(bx, -_depth * 0.5f, _buildingZ),
                    new Vector3(0.04f, _depth * 0.9f, 0.04f), KWood));
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // CISTERN — water pool, stone walls
        // ═══════════════════════════════════════════════════════════════

        private void EmitCistern(List<ProceduralPartDef> p, float totalW, float cx)
        {
            // Stone walls
            p.Add(new ProceduralPartDef("wall_l", PrimitiveType.Cube,
                new Vector3(_originX + totalW * 0.06f, -_depth * 0.5f, _buildingZ),
                new Vector3(0.06f, _depth * 0.9f, Cell * 0.8f), KStone));
            p.Add(new ProceduralPartDef("wall_r", PrimitiveType.Cube,
                new Vector3(_originX + totalW * 0.94f, -_depth * 0.5f, _buildingZ),
                new Vector3(0.06f, _depth * 0.9f, Cell * 0.8f), KStone));

            // Water pool — fills most of the cistern
            float waterH = _depth * 0.6f;
            p.Add(new ProceduralPartDef("pool", PrimitiveType.Cube,
                new Vector3(cx, -_depth + waterH * 0.5f + 0.02f, _buildingZ),
                new Vector3(totalW * 0.78f, waterH, Cell * 0.7f), KWater));

            // Stone floor under pool
            p.Add(new ProceduralPartDef("cistern_floor", PrimitiveType.Cube,
                new Vector3(cx, -_depth + 0.02f, _buildingZ),
                new Vector3(totalW * 0.82f, 0.04f, Cell * 0.75f), KStone));
        }

        // ═══════════════════════════════════════════════════════════════
        // BAZAAR — underground stalls, chests
        // ═══════════════════════════════════════════════════════════════

        private void EmitBazaar(List<ProceduralPartDef> p, float totalW, float cx)
        {
            // Floor
            p.Add(new ProceduralPartDef("bazaar_floor", PrimitiveType.Cube,
                new Vector3(cx, -_depth + 0.02f, _buildingZ),
                new Vector3(totalW * 0.85f, 0.04f, Cell * 0.8f), KStone));

            // Stalls
            int stallCount = Mathf.Max(1, _buildingWidth);
            float stallRegion = totalW * 0.85f;
            float stallSpacing = stallRegion / stallCount;

            for (int i = 0; i < stallCount; i++)
            {
                float sx = _originX + totalW * 0.075f + stallSpacing * (i + 0.5f);
                float stallW = stallSpacing * 0.6f;
                float counterH = Mathf.Min(_depth * 0.2f, 0.12f);

                p.Add(new ProceduralPartDef($"stall_{i}", PrimitiveType.Cube,
                    new Vector3(sx, -_depth + 0.04f + counterH * 0.5f, _buildingZ),
                    new Vector3(stallW, counterH, Cell * 0.25f), KWood));

                // Chest on stall
                float chestS = counterH * 0.6f;
                p.Add(new ProceduralPartDef($"chest_{i}", PrimitiveType.Cube,
                    new Vector3(sx, -_depth + 0.04f + counterH + chestS * 0.5f, _buildingZ),
                    new Vector3(chestS, chestS, chestS), KMetal));
            }

            // Lantern hanging at center
            float lanternY = -_depth * 0.15f;
            p.Add(new ProceduralPartDef("lantern", PrimitiveType.Cube,
                new Vector3(cx, lanternY, _buildingZ),
                new Vector3(0.05f, 0.05f, 0.05f), KGold));
        }

        // ═══════════════════════════════════════════════════════════════
        // DRYDOCK — flooded excavation, hull cradle, smuggler nooks
        // ═══════════════════════════════════════════════════════════════

        private void EmitDrydock(List<ProceduralPartDef> p, float totalW, float cx)
        {
            // Stone walls lining the excavation
            p.Add(new ProceduralPartDef("dock_wall_l", PrimitiveType.Cube,
                new Vector3(_originX + totalW * 0.05f, -_depth * 0.5f, _buildingZ),
                new Vector3(0.06f, _depth * 0.9f, Cell * 0.8f), KStone));
            p.Add(new ProceduralPartDef("dock_wall_r", PrimitiveType.Cube,
                new Vector3(_originX + totalW * 0.95f, -_depth * 0.5f, _buildingZ),
                new Vector3(0.06f, _depth * 0.9f, Cell * 0.8f), KStone));

            // Water filling lower portion of drydock
            float waterH = _depth * 0.4f;
            p.Add(new ProceduralPartDef("dock_water", PrimitiveType.Cube,
                new Vector3(cx, -_depth + waterH * 0.5f + 0.02f, _buildingZ),
                new Vector3(totalW * 0.80f, waterH, Cell * 0.7f), KWater));

            // Hull cradle supports — wooden blocks holding ship keel
            int blockCount = Mathf.Max(2, _buildingWidth * 2);
            float blockSpacing = totalW * 0.7f / blockCount;
            for (int i = 0; i < blockCount; i++)
            {
                float bx = _originX + totalW * 0.15f + blockSpacing * (i + 0.5f);
                float blockH = Mathf.Min(_depth * 0.2f, 0.12f);
                p.Add(new ProceduralPartDef($"cradle_{i}", PrimitiveType.Cube,
                    new Vector3(bx, -_depth + waterH + blockH * 0.5f + 0.02f, _buildingZ),
                    new Vector3(blockSpacing * 0.4f, blockH, Cell * 0.2f), KWood));
            }

            // Stone floor
            p.Add(new ProceduralPartDef("dock_floor", PrimitiveType.Cube,
                new Vector3(cx, -_depth + 0.02f, _buildingZ),
                new Vector3(totalW * 0.85f, 0.04f, Cell * 0.75f), KStone));
        }

        // ═══════════════════════════════════════════════════════════════
        // VAULT — reinforced treasure cellar, gold horde, tax chests
        // ═══════════════════════════════════════════════════════════════

        private void EmitVault(List<ProceduralPartDef> p, float totalW, float cx)
        {
            // ── Multi-level vault: levels scale with depth (1 level per 1.0 unit) ──
            int levels = Mathf.Max(1, Mathf.FloorToInt(_depth));
            float storyH = _depth / levels;

            // ── Outer shell: walls, back wall, ceiling ──
            float wallThick = 0.08f;
            float wallH = _depth * 0.92f;
            p.Add(new ProceduralPartDef("vault_wall_l", PrimitiveType.Cube,
                new Vector3(_originX + totalW * 0.04f, -_depth * 0.5f, _buildingZ),
                new Vector3(wallThick, wallH, Cell * 0.82f), KStone));
            p.Add(new ProceduralPartDef("vault_wall_r", PrimitiveType.Cube,
                new Vector3(_originX + totalW * 0.96f, -_depth * 0.5f, _buildingZ),
                new Vector3(wallThick, wallH, Cell * 0.82f), KStone));
            p.Add(new ProceduralPartDef("vault_backwall", PrimitiveType.Cube,
                new Vector3(cx, -_depth * 0.5f, _buildingZ + Cell * 0.40f),
                new Vector3(totalW * 0.86f, wallH, 0.05f), KStone));

            // Ceiling
            p.Add(new ProceduralPartDef("vault_ceiling", PrimitiveType.Cube,
                new Vector3(cx, -0.04f, _buildingZ),
                new Vector3(totalW * 0.86f, 0.06f, Cell * 0.78f), KStone));
            int strapCount = Mathf.Max(2, _buildingWidth * 2);
            float strapSpacing = totalW * 0.80f / strapCount;
            for (int i = 0; i < strapCount; i++)
            {
                float sx = _originX + totalW * 0.10f + strapSpacing * (i + 0.5f);
                p.Add(new ProceduralPartDef($"vault_strap_{i}", PrimitiveType.Cube,
                    new Vector3(sx, -0.02f, _buildingZ),
                    new Vector3(0.025f, 0.02f, Cell * 0.70f), KMetal));
            }

            // ── Iron gate at front (ground-level entrance, spans top level) ──
            float gateH = storyH * 0.75f;
            float gateW = totalW * 0.30f;
            float gateZ = _buildingZ - Cell * 0.35f;
            float gateBaseY = -storyH; // top level floor
            p.Add(new ProceduralPartDef("vault_gate_top", PrimitiveType.Cube,
                new Vector3(cx, gateBaseY + 0.06f + gateH + 0.02f, gateZ),
                new Vector3(gateW + 0.08f, 0.05f, 0.04f), KMetal));
            p.Add(new ProceduralPartDef("vault_gate_l", PrimitiveType.Cube,
                new Vector3(cx - gateW * 0.5f - 0.02f, gateBaseY + 0.06f + gateH * 0.5f, gateZ),
                new Vector3(0.05f, gateH, 0.04f), KMetal));
            p.Add(new ProceduralPartDef("vault_gate_r", PrimitiveType.Cube,
                new Vector3(cx + gateW * 0.5f + 0.02f, gateBaseY + 0.06f + gateH * 0.5f, gateZ),
                new Vector3(0.05f, gateH, 0.04f), KMetal));
            int barCount = Mathf.Max(3, Mathf.RoundToInt(gateW / 0.06f));
            float barSpacing = gateW / (barCount + 1);
            for (int i = 1; i <= barCount; i++)
            {
                float bx = cx - gateW * 0.5f + barSpacing * i;
                p.Add(new ProceduralPartDef($"vault_bar_{i}", PrimitiveType.Cube,
                    new Vector3(bx, gateBaseY + 0.06f + gateH * 0.5f, gateZ),
                    new Vector3(0.018f, gateH * 0.95f, 0.018f), KMetal));
            }

            // ── Per-level contents ──
            int pillarCount = Mathf.Max(2, _buildingWidth + 1);
            float pillarSpacing = totalW * 0.80f / (pillarCount - 1);
            float pillarW = 0.06f;

            for (int lv = 0; lv < levels; lv++)
            {
                float baseY = -_depth + lv * storyH;
                string tag = $"v{lv}";

                // Floor slab
                p.Add(new ProceduralPartDef($"{tag}_floor", PrimitiveType.Cube,
                    new Vector3(cx, baseY + 0.03f, _buildingZ),
                    new Vector3(totalW * 0.88f, 0.06f, Cell * 0.82f), KStone));

                // Iron-banded pillars spanning this level
                float pillarH = storyH * 0.85f;
                for (int pi = 0; pi < pillarCount; pi++)
                {
                    float px = _originX + totalW * 0.10f + pillarSpacing * pi;
                    p.Add(new ProceduralPartDef($"{tag}_pillar_{pi}", PrimitiveType.Cube,
                        new Vector3(px, baseY + storyH * 0.5f, _buildingZ + Cell * 0.30f),
                        new Vector3(pillarW, pillarH, pillarW), KStone));
                    p.Add(new ProceduralPartDef($"{tag}_band_{pi}", PrimitiveType.Cube,
                        new Vector3(px, baseY + storyH * 0.5f, _buildingZ + Cell * 0.30f),
                        new Vector3(pillarW + 0.02f, 0.03f, pillarW + 0.02f), KMetal));
                }

                // Torch sconces (one pair per level)
                float torchY = baseY + storyH * 0.65f;
                p.Add(new ProceduralPartDef($"{tag}_torch_l", PrimitiveType.Cube,
                    new Vector3(_originX + totalW * 0.08f, torchY, _buildingZ),
                    new Vector3(0.03f, 0.08f, 0.03f), KWood));
                p.Add(new ProceduralPartDef($"{tag}_torch_l_fl", PrimitiveType.Cube,
                    new Vector3(_originX + totalW * 0.08f, torchY + 0.06f, _buildingZ),
                    new Vector3(0.025f, 0.035f, 0.025f), KGold));
                p.Add(new ProceduralPartDef($"{tag}_torch_r", PrimitiveType.Cube,
                    new Vector3(_originX + totalW * 0.92f, torchY, _buildingZ),
                    new Vector3(0.03f, 0.08f, 0.03f), KWood));
                p.Add(new ProceduralPartDef($"{tag}_torch_r_fl", PrimitiveType.Cube,
                    new Vector3(_originX + totalW * 0.92f, torchY + 0.06f, _buildingZ),
                    new Vector3(0.025f, 0.035f, 0.025f), KGold));

                // Ladder between levels (except bottom floor)
                if (lv > 0)
                {
                    float ladderX = _originX + totalW * 0.06f + totalW * 0.04f;
                    float ladderH = storyH * 0.92f;
                    // Rails
                    p.Add(new ProceduralPartDef($"{tag}_ladder_l", PrimitiveType.Cube,
                        new Vector3(ladderX - 0.03f, baseY + ladderH * 0.5f, _buildingZ - Cell * 0.20f),
                        new Vector3(0.02f, ladderH, 0.02f), KWood));
                    p.Add(new ProceduralPartDef($"{tag}_ladder_r", PrimitiveType.Cube,
                        new Vector3(ladderX + 0.03f, baseY + ladderH * 0.5f, _buildingZ - Cell * 0.20f),
                        new Vector3(0.02f, ladderH, 0.02f), KWood));
                    // Rungs
                    int rungCount = Mathf.Max(2, Mathf.RoundToInt(ladderH / 0.12f));
                    float rungSpacing = ladderH / (rungCount + 1);
                    for (int r = 1; r <= rungCount; r++)
                    {
                        p.Add(new ProceduralPartDef($"{tag}_rung_{r}", PrimitiveType.Cube,
                            new Vector3(ladderX, baseY + rungSpacing * r, _buildingZ - Cell * 0.20f),
                            new Vector3(0.06f, 0.015f, 0.02f), KWood));
                    }
                }

                // ── Level-specific contents ──
                // Bottom levels: gold coin piles (the deep horde)
                // Middle levels: treasure chests + strongboxes
                // Top level (nearest surface): tax desk + chests

                if (lv < Mathf.CeilToInt(levels * 0.5f))
                {
                    // Lower half: gold piles
                    int pileCount = Mathf.Max(1, _buildingWidth);
                    float pileRegion = totalW * 0.55f;
                    float pileSp = pileRegion / pileCount;
                    for (int i = 0; i < pileCount; i++)
                    {
                        float px = _originX + totalW * 0.22f + pileSp * (i + 0.5f);
                        float baseW = Mathf.Min(pileSp * 0.7f, 0.20f);
                        float layerH = 0.04f;
                        p.Add(new ProceduralPartDef($"{tag}_gold_base_{i}", PrimitiveType.Cube,
                            new Vector3(px, baseY + 0.06f + layerH * 0.5f, _buildingZ),
                            new Vector3(baseW, layerH, baseW * 0.8f), KGold));
                        p.Add(new ProceduralPartDef($"{tag}_gold_mid_{i}", PrimitiveType.Cube,
                            new Vector3(px, baseY + 0.06f + layerH * 1.5f, _buildingZ),
                            new Vector3(baseW * 0.70f, layerH, baseW * 0.55f), KGold));
                        p.Add(new ProceduralPartDef($"{tag}_gold_peak_{i}", PrimitiveType.Cube,
                            new Vector3(px, baseY + 0.06f + layerH * 2.5f, _buildingZ),
                            new Vector3(baseW * 0.40f, layerH, baseW * 0.30f), KGold));
                    }

                    // Strongboxes along back wall on gold levels too
                    int boxCount = Mathf.Max(2, _buildingWidth * 2);
                    float boxRegion = totalW * 0.75f;
                    float boxSp = boxRegion / boxCount;
                    float boxS = Mathf.Min(boxSp * 0.55f, 0.08f);
                    for (int i = 0; i < boxCount; i++)
                    {
                        float bxx = _originX + totalW * 0.12f + boxSp * (i + 0.5f);
                        p.Add(new ProceduralPartDef($"{tag}_sbox_{i}", PrimitiveType.Cube,
                            new Vector3(bxx, baseY + 0.06f + boxS * 0.5f, _buildingZ + Cell * 0.32f),
                            new Vector3(boxS, boxS, boxS * 0.8f), KMetal));
                    }
                }
                else
                {
                    // Upper half: treasure chests
                    int chestCount = Mathf.Max(1, _buildingWidth);
                    float chestReg = totalW * 0.65f;
                    float chestSp = chestReg / chestCount;
                    float cW = Mathf.Min(chestSp * 0.55f, 0.16f);
                    float cH = Mathf.Min(storyH * 0.20f, 0.10f);
                    float cD = cW * 0.65f;
                    for (int i = 0; i < chestCount; i++)
                    {
                        float chX = _originX + totalW * 0.18f + chestSp * (i + 0.5f);
                        float chBaseY = baseY + 0.06f + cH * 0.5f;
                        p.Add(new ProceduralPartDef($"{tag}_chest_{i}", PrimitiveType.Cube,
                            new Vector3(chX, chBaseY, _buildingZ + Cell * 0.22f),
                            new Vector3(cW, cH, cD), KWood));
                        p.Add(new ProceduralPartDef($"{tag}_chest_band_{i}", PrimitiveType.Cube,
                            new Vector3(chX, chBaseY, _buildingZ + Cell * 0.22f),
                            new Vector3(cW + 0.01f, cH * 0.25f, cD + 0.01f), KMetal));
                        p.Add(new ProceduralPartDef($"{tag}_chest_lock_{i}", PrimitiveType.Cube,
                            new Vector3(chX, chBaseY + cH * 0.15f, _buildingZ + Cell * 0.22f - cD * 0.5f - 0.005f),
                            new Vector3(0.025f, 0.025f, 0.015f), KMetal));
                    }

                    // Smaller gold stacks on upper levels (taxes collected)
                    int smallPiles = Mathf.Max(1, _buildingWidth);
                    float smallRegion = totalW * 0.45f;
                    float smallSp = smallRegion / smallPiles;
                    for (int i = 0; i < smallPiles; i++)
                    {
                        float spx = _originX + totalW * 0.28f + smallSp * (i + 0.5f);
                        float sw = Mathf.Min(smallSp * 0.5f, 0.12f);
                        p.Add(new ProceduralPartDef($"{tag}_taxgold_{i}", PrimitiveType.Cube,
                            new Vector3(spx, baseY + 0.06f + 0.025f, _buildingZ),
                            new Vector3(sw, 0.04f, sw * 0.8f), KGold));
                    }
                }

                // Tax ledger desk on the top level only
                if (lv == levels - 1)
                {
                    float deskW = Mathf.Min(totalW * 0.12f, 0.16f);
                    float deskH = storyH * 0.30f;
                    float deskX = _originX + totalW * 0.12f;
                    p.Add(new ProceduralPartDef("vault_desk", PrimitiveType.Cube,
                        new Vector3(deskX, baseY + 0.06f + deskH * 0.5f, _buildingZ - Cell * 0.10f),
                        new Vector3(deskW, deskH, Cell * 0.20f), KWood));
                    p.Add(new ProceduralPartDef("vault_ledger", PrimitiveType.Cube,
                        new Vector3(deskX, baseY + 0.06f + deskH + 0.015f, _buildingZ - Cell * 0.10f),
                        new Vector3(deskW * 0.70f, 0.02f, Cell * 0.12f), KFabric));
                    p.Add(new ProceduralPartDef("vault_candle", PrimitiveType.Cube,
                        new Vector3(deskX + deskW * 0.35f, baseY + 0.06f + deskH + 0.035f, _buildingZ - Cell * 0.10f),
                        new Vector3(0.015f, 0.04f, 0.015f), KBone));
                    p.Add(new ProceduralPartDef("vault_candle_fl", PrimitiveType.Cube,
                        new Vector3(deskX + deskW * 0.35f, baseY + 0.06f + deskH + 0.06f, _buildingZ - Cell * 0.10f),
                        new Vector3(0.012f, 0.015f, 0.012f), KGold));
                }
            }
        }
    }
}
