// Copyright CodeGamified 2025-2026
// MIT License — PopVuj
using UnityEngine;

namespace PopVuj.Game
{
    /// <summary>
    /// Cell types in the city strip.
    /// Only surface buildings are placed. Sewers are derived automatically.
    /// </summary>
    public enum CellType
    {
        Empty      = 0,   // open ground — trees can grow here
        Tree       = 2,   // pine tree — grows on empty land, harvestable for wood
        House      = 3,   // residential — minions live here
        Chapel     = 4,   // worship — raises faith, god influence
        Workshop   = 5,   // labor — produces resources, science
        Farm       = 6,   // food — feeds the village
        Market     = 7,   // trade — economy hub
        Fountain   = 8,   // health — reduces disease spread
    }

    /// <summary>
    /// The sewer archetype — what kind of underground space exists
    /// beneath a surface building. Derived, never stored.
    /// </summary>
    public enum SewerType
    {
        None       = 0,   // bare earth — Empty / Tree / Farm
        Drain      = 1,   // thin french drain — small buildings (House 1-wide)
        Den        = 2,   // sewer den — housing for homeless, proportional to house above
        Crypt      = 3,   // chapel undercroft — inverted cathedral, water collects here
        Tunnel     = 4,   // workshop utility tunnels / smuggler passages
        Cistern    = 5,   // fountain reservoir — water storage below
        Bazaar     = 6,   // black market — underground trade / thieves guild
    }

    /// <summary>
    /// Linear city strip — surface buildings only.
    ///
    /// SEWERS ARE DERIVED, NOT STORED:
    ///   Every surface building casts an underground shadow.
    ///   Sewer depth = proportional to building height.
    ///   Sewer type = determined by building type above.
    ///
    ///   House (1w) → thin Drain.  House (2w+) → Den with housing density.
    ///   Chapel     → Crypt (inverted cathedral, deep as chapel is tall).
    ///   Workshop   → Tunnel (utility passages).
    ///   Market     → Bazaar (smuggler corridors).
    ///   Fountain   → Cistern (water reservoir).
    ///   Farm       → None (shallow roots, no underground space).
    ///   Empty/Tree → None (bare earth).
    ///
    ///                        ↑ Heavens (camera Y)
    ///   ┌──┬──┬──────────┬──┬──┬──┬──────┬──┐
    ///   │🌲│  │  Chapel   │🌲│  │  │House │🌲│  ← Buildings (Z > 0)
    ///   ├──┴──┴──────────┴──┴──┴──┴──────┴──┤  ← Road (Z = 0)
    ///   │  │  │▓▓Crypt▓▓▓│  │  │  │░Den░│  │  ← Sewers (derived, Y &lt; 0)
    ///   └──┴──┴──────────┴──┴──┴──┴──────┴──┘
    ///                        ↓ Xibalba
    /// </summary>
    public class CityGrid : MonoBehaviour
    {
        public int Width { get; private set; }

        // Surface layer — the only mutable state
        private CellType[] _surface;

        // Multi-tile building support
        private int[] _buildingWidth;
        private int[] _owner;

        // Resources
        public int Wood { get; private set; }

        // Events
        public System.Action OnGridChanged;

        public void Initialize(int width)
        {
            Width = width;
            _surface = new CellType[width];
            _buildingWidth = new int[width];
            _owner = new int[width];
            Generate();
        }

        public void Reset()
        {
            System.Array.Clear(_surface, 0, _surface.Length);
            for (int i = 0; i < Width; i++)
            {
                _buildingWidth[i] = 0;
                _owner[i] = -1;
            }
            Wood = 0;
            Generate();
            OnGridChanged?.Invoke();
        }

        // ═══════════════════════════════════════════════════════════════
        // QUERIES
        // ═══════════════════════════════════════════════════════════════

        public CellType GetSurface(int slot)
        {
            if (slot < 0 || slot >= Width) return CellType.Empty;
            return _surface[slot];
        }

        /// <summary>Get the effective building type at a slot (follows owner chain).</summary>
        public CellType GetBuildingAt(int slot)
        {
            if (slot < 0 || slot >= Width) return CellType.Empty;
            int own = _owner[slot];
            if (own < 0) return _surface[slot];
            return _surface[own];
        }

        public int GetOwner(int slot)
        {
            if (slot < 0 || slot >= Width) return -1;
            return _owner[slot];
        }

        public int GetBuildingWidth(int slot)
        {
            if (slot < 0 || slot >= Width) return 0;
            return _buildingWidth[slot];
        }

        // ── Derived sewer queries ───────────────────────────────

        /// <summary>
        /// Get the sewer archetype beneath a slot. Derived from the building above.
        /// </summary>
        public SewerType GetSewerAt(int slot)
        {
            var bldg = GetBuildingAt(slot);
            int bw = GetBuildingWidthAt(slot);
            return DeriveSewerType(bldg, bw);
        }

        /// <summary>
        /// Get the sewer depth beneath a slot (world units).
        /// Proportional to the building height above — big building = deep sewer.
        /// </summary>
        public float GetSewerDepth(int slot)
        {
            var bldg = GetBuildingAt(slot);
            int bw = GetBuildingWidthAt(slot);
            return DeriveSewerDepth(bldg, bw);
        }

        /// <summary>Building width via owner chain (works for any slot in a multi-tile building).</summary>
        private int GetBuildingWidthAt(int slot)
        {
            if (slot < 0 || slot >= Width) return 0;
            int own = _owner[slot];
            if (own < 0) return 0;
            return _buildingWidth[own];
        }

        // ── Sewer derivation rules ──────────────────────────────

        /// <summary>Map surface building → sewer archetype.</summary>
        public static SewerType DeriveSewerType(CellType surface, int buildingWidth)
        {
            switch (surface)
            {
                case CellType.House:
                    return buildingWidth >= 2 ? SewerType.Den : SewerType.Drain;
                case CellType.Chapel:   return SewerType.Crypt;
                case CellType.Workshop: return SewerType.Tunnel;
                case CellType.Market:   return SewerType.Bazaar;
                case CellType.Fountain: return SewerType.Cistern;
                default:                return SewerType.None;
            }
        }

        /// <summary>Map surface building → sewer depth (world units).</summary>
        public static float DeriveSewerDepth(CellType surface, int buildingWidth)
        {
            // Depth scales with building height; wider buildings go deeper
            float baseDepth;
            switch (surface)
            {
                case CellType.House:    baseDepth = 0.3f; break;
                case CellType.Chapel:   baseDepth = 0.8f; break;   // deep crypt
                case CellType.Workshop: baseDepth = 0.4f; break;
                case CellType.Market:   baseDepth = 0.35f; break;
                case CellType.Fountain: baseDepth = 0.5f; break;   // cistern
                case CellType.Farm:     return 0.05f;               // just roots
                default:                return 0f;
            }
            // Wider buildings dig deeper (diminishing returns)
            float widthBonus = buildingWidth > 1 ? (buildingWidth - 1) * 0.15f : 0f;
            return baseDepth + widthBonus;
        }

        // ── Standard queries ────────────────────────────────────

        public bool IsBuildable(int slot)
        {
            if (slot < 0 || slot >= Width) return false;
            return _owner[slot] < 0 && (_surface[slot] == CellType.Empty || _surface[slot] == CellType.Tree);
        }

        public bool IsRangeBuildable(int start, int width)
        {
            for (int i = start; i < start + width; i++)
                if (!IsBuildable(i)) return false;
            return true;
        }

        public int CountSurface(CellType type)
        {
            int count = 0;
            for (int i = 0; i < Width; i++)
            {
                if (_surface[i] == type && (_owner[i] == i || _owner[i] < 0))
                    count++;
            }
            return count;
        }

        /// <summary>Count slots with a given sewer archetype (derived).</summary>
        public int CountSewer(SewerType type)
        {
            int count = 0;
            for (int i = 0; i < Width; i++)
                if (GetSewerAt(i) == type) count++;
            return count;
        }

        // ═══════════════════════════════════════════════════════════════
        // COMMANDS
        // ═══════════════════════════════════════════════════════════════

        public bool PlaceBuilding(int slot, CellType type, int tileWidth = 1)
        {
            if (type == CellType.Tree || type == CellType.Empty)
                return false;
            if (tileWidth < 1 || slot < 0 || slot + tileWidth > Width)
                return false;
            if (!IsRangeBuildable(slot, tileWidth))
                return false;

            for (int i = slot; i < slot + tileWidth; i++)
                _surface[i] = CellType.Empty;

            _surface[slot] = type;
            _buildingWidth[slot] = tileWidth;
            _owner[slot] = slot;
            for (int i = slot + 1; i < slot + tileWidth; i++)
            {
                _surface[i] = CellType.Empty;
                _owner[i] = slot;
                _buildingWidth[i] = 0;
            }

            OnGridChanged?.Invoke();
            return true;
        }

        public bool ExpandBuilding(int slot)
        {
            if (slot < 0 || slot >= Width) return false;
            int origin = _owner[slot];
            if (origin < 0) return false;

            int curW = _buildingWidth[origin];
            int newEnd = origin + curW;
            if (!IsBuildable(newEnd)) return false;

            _buildingWidth[origin] = curW + 1;
            _surface[newEnd] = CellType.Empty;
            _owner[newEnd] = origin;
            OnGridChanged?.Invoke();
            return true;
        }

        public bool ShrinkBuilding(int slot)
        {
            if (slot < 0 || slot >= Width) return false;
            int origin = _owner[slot];
            if (origin < 0) return false;

            int curW = _buildingWidth[origin];
            if (curW <= 1) return false;

            int freed = origin + curW - 1;
            _owner[freed] = -1;
            _buildingWidth[origin] = curW - 1;
            OnGridChanged?.Invoke();
            return true;
        }

        public bool DestroyBuilding(int slot)
        {
            if (slot < 0 || slot >= Width) return false;
            int origin = _owner[slot];
            if (origin < 0)
            {
                if (_surface[slot] == CellType.Tree)
                {
                    _surface[slot] = CellType.Empty;
                    OnGridChanged?.Invoke();
                    return true;
                }
                return false;
            }

            int w = _buildingWidth[origin];
            for (int i = origin; i < origin + w; i++)
            {
                _surface[i] = CellType.Empty;
                _owner[i] = -1;
                _buildingWidth[i] = 0;
            }
            OnGridChanged?.Invoke();
            return true;
        }

        public bool HarvestTree(int slot)
        {
            if (slot < 0 || slot >= Width) return false;
            if (_surface[slot] != CellType.Tree) return false;
            _surface[slot] = CellType.Empty;
            Wood++;
            OnGridChanged?.Invoke();
            return true;
        }

        public bool SpendWood(int amount)
        {
            if (Wood < amount) return false;
            Wood -= amount;
            return true;
        }

        public void AddWood(int amount) { Wood += amount; }

        // ═══════════════════════════════════════════════════════════════
        // TREE GROWTH
        // ═══════════════════════════════════════════════════════════════

        public bool TryGrowTree()
        {
            for (int attempt = 0; attempt < 10; attempt++)
            {
                int slot = Random.Range(0, Width);
                if (_surface[slot] == CellType.Empty && _owner[slot] < 0)
                {
                    _surface[slot] = CellType.Tree;
                    OnGridChanged?.Invoke();
                    return true;
                }
            }
            return false;
        }

        // ═══════════════════════════════════════════════════════════════
        // GENERATION
        // ═══════════════════════════════════════════════════════════════

        private void Generate()
        {
            for (int i = 0; i < Width; i++)
            {
                _surface[i] = CellType.Empty;
                _owner[i] = -1;
                _buildingWidth[i] = 0;
            }

            int mid = Width / 2;
            PlaceStarterBuildings(mid);
            PlaceStarterTrees(mid);
            Wood = 5;
        }

        private void PlaceStarterBuildings(int mid)
        {
            PlaceBuilding(mid, CellType.Chapel, 2);
            PlaceBuilding(mid - 1, CellType.House);
            PlaceBuilding(mid + 2, CellType.House);
            PlaceBuilding(mid - 4, CellType.House);
            PlaceBuilding(mid + 4, CellType.House);
            PlaceBuilding(mid - 6, CellType.Farm);
            PlaceBuilding(mid + 6, CellType.Farm);
            PlaceBuilding(mid - 2, CellType.Workshop);
            PlaceBuilding(mid + 3, CellType.Market);
            PlaceBuilding(mid + 5, CellType.Fountain);
        }

        private void PlaceStarterTrees(int mid)
        {
            for (int i = 0; i < Width; i++)
            {
                if (_surface[i] == CellType.Empty && _owner[i] < 0)
                {
                    float dist = Mathf.Abs(i - mid);
                    if (Random.value < dist / Width * 0.8f)
                        _surface[i] = CellType.Tree;
                }
            }
        }
    }
}
