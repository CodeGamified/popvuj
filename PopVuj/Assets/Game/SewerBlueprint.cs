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
                               float depth, float buildingZ = 0.5f)
        {
            _type = type;
            _buildingWidth = Mathf.Max(1, buildingWidth);
            _originX = originX;
            _depth = Mathf.Max(0.05f, depth);
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
            }

            return parts.ToArray();
        }

        // ═══════════════════════════════════════════════════════════════
        // DRAIN — simple pipe segments
        // ═══════════════════════════════════════════════════════════════

        private void EmitDrain(List<ProceduralPartDef> p, float totalW, float cx)
        {
            // Pipe running horizontally through the drain
            float pipeD = Mathf.Min(_depth * 0.5f, 0.06f);
            p.Add(new ProceduralPartDef("pipe", PrimitiveType.Cube,
                new Vector3(cx, -_depth * 0.5f, _buildingZ),
                new Vector3(totalW * 0.85f, pipeD, pipeD), KStone));

            // Drip at the center
            p.Add(new ProceduralPartDef("drip", PrimitiveType.Cube,
                new Vector3(cx, -_depth * 0.15f, _buildingZ),
                new Vector3(0.015f, _depth * 0.3f, 0.015f), KWater));
        }

        // ═══════════════════════════════════════════════════════════════
        // DEN — makeshift shelters, barrels
        // ═══════════════════════════════════════════════════════════════

        private void EmitDen(List<ProceduralPartDef> p, float totalW, float cx)
        {
            // Floor
            p.Add(new ProceduralPartDef("den_floor", PrimitiveType.Cube,
                new Vector3(cx, -_depth + 0.01f, _buildingZ),
                new Vector3(totalW * 0.85f, 0.02f, Cell * 0.8f), KStone));

            // Bed rolls — one per tile width
            int bedCount = _buildingWidth;
            float bedSpacing = totalW * 0.85f / bedCount;
            for (int i = 0; i < bedCount; i++)
            {
                float bx = _originX + totalW * 0.075f + bedSpacing * (i + 0.5f);
                p.Add(new ProceduralPartDef($"bedroll_{i}", PrimitiveType.Cube,
                    new Vector3(bx, -_depth + 0.04f, _buildingZ + Cell * 0.15f),
                    new Vector3(bedSpacing * 0.55f, 0.025f, Cell * 0.25f), KFabric));
            }

            // Barrel at one end
            float barrelH = Mathf.Min(_depth * 0.4f, 0.10f);
            p.Add(new ProceduralPartDef("barrel", PrimitiveType.Cylinder,
                new Vector3(_originX + totalW * 0.88f, -_depth + barrelH * 0.5f + 0.02f, _buildingZ - Cell * 0.2f),
                new Vector3(0.04f, barrelH * 0.5f, 0.04f), KWood));
        }

        // ═══════════════════════════════════════════════════════════════
        // CRYPT — sarcophagi, columns, altar
        // ═══════════════════════════════════════════════════════════════

        private void EmitCrypt(List<ProceduralPartDef> p, float totalW, float cx)
        {
            // Stone floor
            p.Add(new ProceduralPartDef("crypt_floor", PrimitiveType.Cube,
                new Vector3(cx, -_depth + 0.01f, _buildingZ),
                new Vector3(totalW * 0.85f, 0.02f, Cell * 0.8f), KStone));

            // Columns at intervals
            int colCount = Mathf.Max(2, _buildingWidth + 1);
            float colSpacing = totalW * 0.85f / (colCount - 1);
            float colH = _depth * 0.8f;
            float colW = Mathf.Lerp(0.025f, 0.04f, (_buildingWidth - 1) / 4f);

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
                float sarcH = Mathf.Min(_depth * 0.25f, 0.08f);
                p.Add(new ProceduralPartDef($"sarcophagus_{i}", PrimitiveType.Cube,
                    new Vector3(sx, -_depth + 0.02f + sarcH * 0.5f, _buildingZ),
                    new Vector3(sarcW, sarcH, Cell * 0.35f), KBone));
            }

            // Altar at center (if wide enough)
            if (_buildingWidth >= 2)
            {
                float altarH = _depth * 0.3f;
                p.Add(new ProceduralPartDef("altar", PrimitiveType.Cube,
                    new Vector3(cx, -_depth + 0.02f + altarH * 0.5f, _buildingZ - Cell * 0.15f),
                    new Vector3(totalW * 0.2f, altarH, Cell * 0.2f), KGold));
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // TUNNEL — rail track, support beams
        // ═══════════════════════════════════════════════════════════════

        private void EmitTunnel(List<ProceduralPartDef> p, float totalW, float cx)
        {
            // Rail track along the floor
            float railH = 0.015f;
            p.Add(new ProceduralPartDef("rail_l", PrimitiveType.Cube,
                new Vector3(cx, -_depth + 0.01f + railH * 0.5f, _buildingZ - Cell * 0.12f),
                new Vector3(totalW * 0.85f, railH, 0.015f), KMetal));
            p.Add(new ProceduralPartDef("rail_r", PrimitiveType.Cube,
                new Vector3(cx, -_depth + 0.01f + railH * 0.5f, _buildingZ + Cell * 0.12f),
                new Vector3(totalW * 0.85f, railH, 0.015f), KMetal));

            // Ties
            int tieCount = Mathf.Max(2, _buildingWidth * 3);
            float tieSpacing = totalW * 0.85f / tieCount;
            for (int i = 0; i < tieCount; i++)
            {
                float tx = _originX + totalW * 0.075f + tieSpacing * (i + 0.5f);
                p.Add(new ProceduralPartDef($"tie_{i}", PrimitiveType.Cube,
                    new Vector3(tx, -_depth + 0.008f, _buildingZ),
                    new Vector3(0.02f, 0.015f, Cell * 0.3f), KWood));
            }

            // Support beams overhead
            int beamCount = Mathf.Max(2, _buildingWidth + 1);
            float beamSpacing = totalW * 0.85f / (beamCount - 1);
            for (int i = 0; i < beamCount; i++)
            {
                float bx = _originX + totalW * 0.075f + beamSpacing * i;
                p.Add(new ProceduralPartDef($"beam_{i}", PrimitiveType.Cube,
                    new Vector3(bx, -_depth * 0.5f, _buildingZ),
                    new Vector3(0.02f, _depth * 0.9f, 0.02f), KWood));
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
                new Vector3(0.03f, _depth * 0.9f, Cell * 0.8f), KStone));
            p.Add(new ProceduralPartDef("wall_r", PrimitiveType.Cube,
                new Vector3(_originX + totalW * 0.94f, -_depth * 0.5f, _buildingZ),
                new Vector3(0.03f, _depth * 0.9f, Cell * 0.8f), KStone));

            // Water pool — fills most of the cistern
            float waterH = _depth * 0.6f;
            p.Add(new ProceduralPartDef("pool", PrimitiveType.Cube,
                new Vector3(cx, -_depth + waterH * 0.5f + 0.01f, _buildingZ),
                new Vector3(totalW * 0.78f, waterH, Cell * 0.7f), KWater));

            // Stone floor under pool
            p.Add(new ProceduralPartDef("cistern_floor", PrimitiveType.Cube,
                new Vector3(cx, -_depth + 0.01f, _buildingZ),
                new Vector3(totalW * 0.82f, 0.02f, Cell * 0.75f), KStone));
        }

        // ═══════════════════════════════════════════════════════════════
        // BAZAAR — underground stalls, chests
        // ═══════════════════════════════════════════════════════════════

        private void EmitBazaar(List<ProceduralPartDef> p, float totalW, float cx)
        {
            // Floor
            p.Add(new ProceduralPartDef("bazaar_floor", PrimitiveType.Cube,
                new Vector3(cx, -_depth + 0.01f, _buildingZ),
                new Vector3(totalW * 0.85f, 0.02f, Cell * 0.8f), KStone));

            // Stalls
            int stallCount = Mathf.Max(1, _buildingWidth);
            float stallRegion = totalW * 0.85f;
            float stallSpacing = stallRegion / stallCount;

            for (int i = 0; i < stallCount; i++)
            {
                float sx = _originX + totalW * 0.075f + stallSpacing * (i + 0.5f);
                float stallW = stallSpacing * 0.6f;
                float counterH = Mathf.Min(_depth * 0.2f, 0.06f);

                p.Add(new ProceduralPartDef($"stall_{i}", PrimitiveType.Cube,
                    new Vector3(sx, -_depth + 0.02f + counterH * 0.5f, _buildingZ),
                    new Vector3(stallW, counterH, Cell * 0.25f), KWood));

                // Chest on stall
                float chestS = counterH * 0.6f;
                p.Add(new ProceduralPartDef($"chest_{i}", PrimitiveType.Cube,
                    new Vector3(sx, -_depth + 0.02f + counterH + chestS * 0.5f, _buildingZ),
                    new Vector3(chestS, chestS, chestS), KMetal));
            }

            // Lantern hanging at center
            float lanternY = -_depth * 0.15f;
            p.Add(new ProceduralPartDef("lantern", PrimitiveType.Cube,
                new Vector3(cx, lanternY, _buildingZ),
                new Vector3(0.025f, 0.025f, 0.025f), KGold));
        }
    }
}
