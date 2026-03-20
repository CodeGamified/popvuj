// Copyright CodeGamified 2025-2026
// MIT License — PopVuj
using UnityEngine;
using System.Collections.Generic;
using CodeGamified.Procedural;

namespace PopVuj.Game
{
    /// <summary>
    /// Renders high-fidelity procedural interiors for buildings and sewers.
    ///
    /// Subscribes to CityGrid.OnGridChanged and rebuilds interior GameObjects
    /// using ProceduralAssembler + StructureBlueprint / SewerBlueprint.
    ///
    /// Each building gets an assembled interior positioned inside the
    /// transparent 10% alpha placeholder box from CityRenderer.
    /// Minions appear at their slot positions inside these detailed interiors.
    ///
    /// The interiors scale smoothly with building width — wider buildings
    /// get proportionally more furniture, not just stretched copies.
    /// </summary>
    public class StructureRenderer : MonoBehaviour
    {
        private CityGrid _city;
        private ColorPalette _structurePalette;
        private ColorPalette _sewerPalette;

        private GameObject _structureParent;
        private GameObject _sewerInteriorParent;

        // Track assembled results for cleanup
        private readonly List<AssemblyResult> _structureAssemblies = new List<AssemblyResult>();
        private readonly List<AssemblyResult> _sewerAssemblies = new List<AssemblyResult>();

        private bool _dirty = true;

        // Track resource values to detect changes (warehouse fill)
        private int _prevWood, _prevStone, _prevFood, _prevGoods;

        // Constants matching CityRenderer
        private const float RoadH = 0.3f;
        private const float BuildingZ = 1.0f;

        public void Initialize(CityGrid city)
        {
            _city = city;

            _structureParent = new GameObject("StructureInteriors");
            _structureParent.transform.SetParent(transform, false);
            _sewerInteriorParent = new GameObject("SewerInteriors");
            _sewerInteriorParent.transform.SetParent(transform, false);

            // Build runtime color palettes for structure & sewer interiors
            _structurePalette = CreateStructurePalette();
            _sewerPalette = CreateSewerPalette();

            _city.OnGridChanged += () => _dirty = true;
        }

        private void LateUpdate()
        {
            // Auto-dirty when city resources change (warehouse fill)
            if (_city != null
                && (_city.Wood != _prevWood || _city.Stone != _prevStone
                 || _city.Food != _prevFood || _city.Goods != _prevGoods))
            {
                _prevWood  = _city.Wood;
                _prevStone = _city.Stone;
                _prevFood  = _city.Food;
                _prevGoods = _city.Goods;
                _dirty = true;
            }

            if (!_dirty) return;
            _dirty = false;
            Rebuild();
        }

        public void MarkDirty() => _dirty = true;

        // ═══════════════════════════════════════════════════════════════
        // REBUILD — tear down old interiors, assemble new ones
        // ═══════════════════════════════════════════════════════════════

        private void Rebuild()
        {
            DestroyAssemblies(_structureAssemblies);
            DestroyAssemblies(_sewerAssemblies);

            for (int i = 0; i < _city.Width; i++)
            {
                int owner = _city.GetOwner(i);
                if (owner != i) continue;

                var type = _city.GetSurface(i);
                if (type == CellType.Empty || type == CellType.Tree) continue;

                int bw = _city.GetBuildingWidth(i);
                if (bw < 1) bw = 1;

                float originX = i * CityRenderer.CellSize;

                // ── Surface building interior ───────────────────
                PierFixture[] fixtures = null;
                if (type == CellType.Pier)
                {
                    fixtures = new PierFixture[bw];
                    for (int f = 0; f < bw; f++)
                        fixtures[f] = _city.GetPierFixture(i + f);
                }
                var bldgBP = new StructureBlueprint(type, bw, originX, RoadH, BuildingZ, fixtures,
                    resWood:  type == CellType.Warehouse ? _city.Wood  : 0,
                    resStone: type == CellType.Warehouse ? _city.Stone : 0,
                    resFood:  type == CellType.Warehouse ? _city.Food  : 0,
                    resGoods: type == CellType.Warehouse ? _city.Goods : 0);
                var bldgResult = ProceduralAssembler.Build(bldgBP, _structurePalette);
                if (bldgResult.IsValid)
                {
                    bldgResult.Root.transform.SetParent(_structureParent.transform, false);
                    _structureAssemblies.Add(bldgResult);
                }

                // ── Sewer interior ──────────────────────────────
                float depth = _city.GetSewerDepth(i);
                if (depth <= 0.01f) continue;

                var sewType = _city.GetSewerAt(i);
                if (sewType == SewerType.None) continue;

                var sewBP = new SewerBlueprint(sewType, bw, originX, depth, BuildingZ);
                var sewResult = ProceduralAssembler.Build(sewBP, _sewerPalette);
                if (sewResult.IsValid)
                {
                    sewResult.Root.transform.SetParent(_sewerInteriorParent.transform, false);
                    _sewerAssemblies.Add(sewResult);
                }
            }
        }

        private static void DestroyAssemblies(List<AssemblyResult> list)
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i].Root != null)
                    Destroy(list[i].Root);
            }
            list.Clear();
        }

        // ═══════════════════════════════════════════════════════════════
        // COLOR PALETTES — Mayan-inspired tones
        // ═══════════════════════════════════════════════════════════════

        private static ColorPalette CreateStructurePalette()
        {
            var colors = new Dictionary<string, Color>
            {
                { "wall",   new Color(0.40f, 0.32f, 0.22f) },   // adobe brown
                { "floor",  new Color(0.30f, 0.25f, 0.18f) },   // dark earth
                { "wood",   new Color(0.45f, 0.30f, 0.15f) },   // warm wood
                { "stone",  new Color(0.50f, 0.48f, 0.42f) },   // limestone grey
                { "fabric", new Color(0.65f, 0.20f, 0.15f) },   // woven red
                { "metal",  new Color(0.35f, 0.33f, 0.30f) },   // dark iron
                { "gold",   new Color(0.85f, 0.72f, 0.20f) },   // temple gold
                { "water",  new Color(0.15f, 0.40f, 0.65f) },   // cenote blue
                { "grain",  new Color(0.55f, 0.65f, 0.20f) },   // maize green
                { "roof",   new Color(0.35f, 0.20f, 0.10f) },   // thatch brown
            };
            return ColorPalette.CreateRuntime(colors, Color.magenta);
        }

        private static ColorPalette CreateSewerPalette()
        {
            var colors = new Dictionary<string, Color>
            {
                { "stone",  new Color(0.25f, 0.22f, 0.20f) },   // dark damp stone
                { "wood",   new Color(0.30f, 0.20f, 0.10f) },   // rotting wood
                { "metal",  new Color(0.28f, 0.26f, 0.24f) },   // tarnished iron
                { "water",  new Color(0.08f, 0.18f, 0.30f) },   // murky water
                { "fabric", new Color(0.35f, 0.15f, 0.10f) },   // dirty cloth
                { "bone",   new Color(0.60f, 0.55f, 0.45f) },   // ancient bone
                { "gold",   new Color(0.65f, 0.50f, 0.15f) },   // tarnished gold
            };
            return ColorPalette.CreateRuntime(colors, Color.magenta);
        }
    }
}
