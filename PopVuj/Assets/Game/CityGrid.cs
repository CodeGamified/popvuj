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

    /// <summary>
    /// Linear city strip — a 1D array of slots with two vertical layers.
    ///
    /// Layout (side-view cross-section):
    ///
    ///   Buildings are rendered BEHIND the road surface (Z > 0).
    ///   Trees grow on empty tiles over time and can be harvested for Wood.
    ///   Buildings can span multiple tiles (tracked by _buildingWidth[]).
    ///   Only the leftmost slot of a multi-tile building stores the CellType;
    ///   trailing slots are marked CellType.Empty but claimed via _owner[].
    ///
    ///                        ↑ Heavens (camera Y)
    ///   ┌──┬──┬──────────┬──┬──┬──┬──────┬──┐
    ///   │🌲│  │  Chapel   │🌲│  │  │House │🌲│  ← Buildings (Z > 0, behind road)
    ///   ├──┴──┴──────────┴──┴──┴──┴──────┴──┤  ← Road / ground (Z = 0)
    ///   │Swr│Swr│  Swr   │Swr│Swr│Swr│Swr│Swr│  ← Sewers (Y < 0)
    ///   └──┴──┴──────────┴──┴──┴──┴──────┴──┘
    ///                        ↓ Xibalba
    /// </summary>
    public class CityGrid : MonoBehaviour
    {
        public int Width { get; private set; }

        // Surface layer
        private CellType[] _surface;

        // Multi-tile building support:
        // _buildingWidth[i] = how many tiles wide the building at slot i is (1 = single tile).
        // Only meaningful when _surface[i] != Empty and _owner[i] == i (i.e. leftmost slot).
        // _owner[i] = index of the leftmost slot that owns this tile.
        //             If _owner[i] == i, this is a building origin.
        //             If _owner[i] != i, this tile is part of a larger building.
        //             -1 means unowned (Empty or Tree).
        private int[] _buildingWidth;
        private int[] _owner;

        // Sewer layer
        private CellType[] _sewers;

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
            _sewers = new CellType[width];
            Generate();
        }

        public void Reset()
        {
            System.Array.Clear(_surface, 0, _surface.Length);
            System.Array.Clear(_sewers, 0, _sewers.Length);
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
            if (own < 0) return _surface[slot]; // unowned — Empty or Tree
            return _surface[own];
        }

        /// <summary>Get the owner (leftmost slot) of whatever building covers this slot. -1 if none.</summary>
        public int GetOwner(int slot)
        {
            if (slot < 0 || slot >= Width) return -1;
            return _owner[slot];
        }

        /// <summary>Width of the building anchored at slot. 0 if not a building origin.</summary>
        public int GetBuildingWidth(int slot)
        {
            if (slot < 0 || slot >= Width) return 0;
            return _buildingWidth[slot];
        }

        public CellType GetSewer(int slot)
        {
            if (slot < 0 || slot >= Width) return CellType.Empty;
            return _sewers[slot];
        }

        public bool IsBuildable(int slot)
        {
            if (slot < 0 || slot >= Width) return false;
            return _owner[slot] < 0 && (_surface[slot] == CellType.Empty || _surface[slot] == CellType.Tree);
        }

        /// <summary>Check if a contiguous run of slots is buildable.</summary>
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
                // For multi-tile buildings, only count the origin
                if (_surface[i] == type && (_owner[i] == i || _owner[i] < 0))
                    count++;
            }
            return count;
        }

        public int CountSewer(CellType type)
        {
            int count = 0;
            for (int i = 0; i < Width; i++)
                if (_sewers[i] == type) count++;
            return count;
        }

        // ═══════════════════════════════════════════════════════════════
        // COMMANDS
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Place a building spanning 'width' tiles starting at 'slot'. Returns true on success.</summary>
        public bool PlaceBuilding(int slot, CellType type, int tileWidth = 1)
        {
            if (type == CellType.Sewer || type == CellType.SewerDen || type == CellType.Tree)
                return false;
            if (tileWidth < 1 || slot < 0 || slot + tileWidth > Width)
                return false;
            if (!IsRangeBuildable(slot, tileWidth))
                return false;

            // Clear any trees in the range (no wood gain — that's harvest_tree)
            for (int i = slot; i < slot + tileWidth; i++)
                _surface[i] = CellType.Empty;

            // Place the building
            _surface[slot] = type;
            _buildingWidth[slot] = tileWidth;
            _owner[slot] = slot;
            for (int i = slot + 1; i < slot + tileWidth; i++)
            {
                _surface[i] = CellType.Empty; // trailing tiles
                _owner[i] = slot;
                _buildingWidth[i] = 0;
            }

            OnGridChanged?.Invoke();
            return true;
        }

        /// <summary>Expand a building by 1 tile on the right. Returns true on success.</summary>
        public bool ExpandBuilding(int slot)
        {
            if (slot < 0 || slot >= Width) return false;
            int origin = _owner[slot];
            if (origin < 0) return false;

            int curW = _buildingWidth[origin];
            int newEnd = origin + curW; // the slot we want to claim
            if (!IsBuildable(newEnd)) return false;

            _buildingWidth[origin] = curW + 1;
            _surface[newEnd] = CellType.Empty;
            _owner[newEnd] = origin;
            OnGridChanged?.Invoke();
            return true;
        }

        /// <summary>Shrink a building by 1 tile from the right. Min size = 1. Returns true on success.</summary>
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

        /// <summary>Destroy a building (all its tiles), leaving empty ground.</summary>
        public bool DestroyBuilding(int slot)
        {
            if (slot < 0 || slot >= Width) return false;
            int origin = _owner[slot];
            if (origin < 0)
            {
                // Not a building — might be a lone tree, just clear it
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

        /// <summary>Harvest a tree at slot for wood. Returns true on success.</summary>
        public bool HarvestTree(int slot)
        {
            if (slot < 0 || slot >= Width) return false;
            if (_surface[slot] != CellType.Tree) return false;
            _surface[slot] = CellType.Empty;
            Wood++;
            OnGridChanged?.Invoke();
            return true;
        }

        /// <summary>Spend wood. Returns true if enough wood available.</summary>
        public bool SpendWood(int amount)
        {
            if (Wood < amount) return false;
            Wood -= amount;
            return true;
        }

        /// <summary>Add wood directly (e.g. from workshops).</summary>
        public void AddWood(int amount) { Wood += amount; }

        /// <summary>Add a sewer den at a location.</summary>
        public bool AddSewerDen(int slot)
        {
            if (slot < 0 || slot >= Width) return false;
            if (_sewers[slot] != CellType.Sewer) return false;
            _sewers[slot] = CellType.SewerDen;
            OnGridChanged?.Invoke();
            return true;
        }

        // ═══════════════════════════════════════════════════════════════
        // TREE GROWTH — called by MatchManager each tick
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Attempt to grow a tree on a random empty, unowned tile.
        /// Call once per sim-tick with a probability check externally.
        /// </summary>
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
            // 1. Clear everything
            for (int i = 0; i < Width; i++)
            {
                _surface[i] = CellType.Empty;
                _owner[i] = -1;
                _buildingWidth[i] = 0;
            }

            // 2. Starter buildings — clustered around the center
            int mid = Width / 2;
            PlaceStarterBuildings(mid);

            // 3. Scatter starter trees on empty outskirt slots
            PlaceStarterTrees(mid);

            // 4. Sewers — tunnel under every slot
            GenerateSewers();

            // 5. Starting wood
            Wood = 5;
        }

        private void PlaceStarterBuildings(int mid)
        {
            // Chapel at center — 2 tiles wide
            PlaceBuilding(mid, CellType.Chapel, 2);

            // Houses flanking the chapel
            PlaceBuilding(mid - 1, CellType.House);
            PlaceBuilding(mid + 2, CellType.House);
            PlaceBuilding(mid - 4, CellType.House);
            PlaceBuilding(mid + 4, CellType.House);

            // Farms on the outskirts
            PlaceBuilding(mid - 6, CellType.Farm);
            PlaceBuilding(mid + 6, CellType.Farm);

            // Workshop and market
            PlaceBuilding(mid - 2, CellType.Workshop);
            PlaceBuilding(mid + 3, CellType.Market);

            // Fountain
            PlaceBuilding(mid + 5, CellType.Fountain);
        }

        private void PlaceStarterTrees(int mid)
        {
            // Trees on the edges — wilderness
            for (int i = 0; i < Width; i++)
            {
                if (_surface[i] == CellType.Empty && _owner[i] < 0)
                {
                    // Higher chance further from center
                    float dist = Mathf.Abs(i - mid);
                    if (Random.value < dist / Width * 0.8f)
                        _surface[i] = CellType.Tree;
                }
            }
        }

        private void GenerateSewers()
        {
            for (int i = 0; i < Width; i++)
                _sewers[i] = CellType.Sewer;
        }
    }
}
