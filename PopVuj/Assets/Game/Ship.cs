// Copyright CodeGamified 2025-2026
// MIT License — PopVuj
using UnityEngine;
using PopVuj.Crew;

namespace PopVuj.Game
{
    /// <summary>
    /// Ship state machine — lifecycle from construction to open sea.
    /// </summary>
    public enum ShipState
    {
        Building,    // under construction in a shipyard
        Launched,    // freshly built, sitting at shipyard
        Docking,     // navigating to assigned crane slot
        Loading,     // at crane, cargo being loaded
        Unloading,   // at crane, cargo being removed
        Departing,   // sliding rightward, leaving harbor
        Voyage,      // off-screen, on trade route
        Arriving,    // sliding leftward, returning to harbor
        Anchored,    // waiting near pier for a free crane
        Idle,        // docked at crane, awaiting orders
    }

    /// <summary>
    /// Trade route definitions — where ships can sail.
    /// </summary>
    public enum TradeRoute
    {
        CoastalFishing  = 0,   // 1 day, returns Fish, low risk
        TimberIslands   = 1,   // 2 days, returns Logs, low risk
        NearbyVillage   = 2,   // 3 days, export Crates → returns Crates + Stone, med risk
        DistantEmpire   = 3,   // 7 days, export Grain+Crates → returns ExoticGoods, high risk
        XibalbaCrossing = 4,   // ??? mythological
    }

    /// <summary>
    /// Hull classification — derived from ship width (tile count).
    /// Wider ships are larger vessels with more cargo capacity and crew slots.
    ///
    ///   1w = Canoe       (2 cargo, 1 crew)
    ///   2w = Sloop       (4 cargo, 3 crew)
    ///   3w = Brigantine  (8 cargo, 6 crew)
    ///   4w = Frigate     (12 cargo, 10 crew)
    ///   5w = Ship of the Line (18 cargo, 15 crew)
    /// </summary>
    public enum HullClass
    {
        Canoe,
        Sloop,
        Brigantine,
        Frigate,
        ShipOfTheLine,
    }

    /// <summary>
    /// Ship module type — what occupies each cell of a ship's 2D grid.
    /// Ships are a 2D array of modules [Width × Height] where:
    ///   - Columns run stern(0) to bow(Width-1)
    ///   - Rows run hold(0) to superstructure(Height-1)
    ///
    /// Structure emerges from the layout. "Forecastle" isn't a concept —
    /// it's the result of enclosed modules stacking at the bow.
    ///
    /// Example 5×4 Ship of the Line:
    ///   Row 3: Wheel   | Air     | Sail     | Crane     | Air
    ///   Row 2: Helm    | Lookout | SailBase | CraneBase | FigureHead
    ///   Row 1: Cabin   | Cabin   | Kitchen  | Cannon    | Cabin
    ///   Row 0: Air     | Store   | Store    | Store     | Magazine
    ///
    /// Cargo is below deck (Store modules). Cannons consume space that
    /// could be storage — warfare comes with a visible trade cost.
    /// </summary>
    public enum ShipModule
    {
        Air          = 0,   // empty space — open sky above, open water below waterline
        Helm         = 1,   // captain's station — required (adds 1 crew: captain)
        Mast         = 2,   // mast trunk — propulsion (adds 2 crew: riggers)
        Cannon       = 3,   // gun emplacement with port (adds 1 crew: gunner)
        Crane        = 4,   // loading crane upper structure (adds 1 crew: operator)
        Oars         = 5,   // rowing station (adds 1 crew: rower)
        Store        = 6,   // cargo storage bay (+3 cargo capacity)
        Cabin        = 7,   // crew quarters / bunks (+2 crew capacity)
        FishingRig   = 8,   // fishing equipment (adds 1 crew: fisher)
        Lookout      = 9,   // observation / crow's nest (adds 1 crew: lookout)
        Kitchen      = 10,  // galley — cooking/eating
        Magazine     = 11,  // powder magazine — ammo storage for cannons
        FigureHead   = 12,  // bow decoration
        CraneBase    = 13,  // crane foundation at deck level
        SailBase     = 14,  // mast step at deck level
        Wheel        = 15,  // steering wheel (elevated above helm)
        Sail         = 16,  // sail canvas area (rigging)
    }

    /// <summary>
    /// A ship — a 2D grid of modules forming a "floating building" with
    /// crew slots, cargo hold, and a state machine.
    ///
    /// The grid is [Width × Height]:
    ///   - Columns: stern(0) to bow(Width-1)
    ///   - Rows:    hold(0) to superstructure(Height-1)
    ///   - Row 0 is below waterline for ships with Height >= 2
    ///
    /// Structure emerges from the layout — no hardcoded forecastle,
    /// quarterdeck, etc. Those appear when enclosed modules stack up.
    ///
    /// Cannon modules consume grid cells that could be Store modules,
    /// so warfare comes with a visible trade cost.
    /// </summary>
    public class Ship
    {
        // ── Identity ────────────────────────────────────────────
        public int Id;

        // ── Dimensions ──────────────────────────────────────────
        public int Width;                 // columns (stern-to-bow tile count)
        public int Height => GetHeight(Width);

        // ── Classification ──────────────────────────────────────
        public HullClass Hull => ClassifyHull(Width);

        // ── State ───────────────────────────────────────────────
        public ShipState State;

        // ── Position (world X along pier extent) ────────────────
        public float X;

        // ── Crane docking ───────────────────────────────────────
        public int TargetCraneSlot;       // pier slot of assigned crane (-1 = none)
        public float DockX;               // target world X for docking
        public float AnchorTimer;         // sim-seconds before anchored ship gives up

        // ── Construction ────────────────────────────────────────
        public float BuildProgress;       // 0→1, advances per shipwright-tick
        public int ShipyardOrigin;        // which shipyard built this (-1 after launch)

        // ── Module Grid (2D ship layout) ────────────────────────
        /// <summary>
        /// 2D module grid [col, row]. Col 0=stern, Col Width-1=bow.
        /// Row 0=hold (below waterline), Row Height-1=superstructure.
        /// </summary>
        public ShipModule[,] Grid;

        // ── Crew (module-derived) ───────────────────────────────
        public int CrewCount;             // minions currently aboard
        public int CrewCapacity => GetGridCrewCapacity();
        public int GunnerSlots => CountModule(ShipModule.Cannon);

        // ── Cargo hold (module-derived) ─────────────────────────
        public int CargoCount;            // cargo units currently loaded
        public int CargoCapacity => GetGridCargoCapacity();

        /// <summary>What kind of cargo is in the hold (set by route or on spawn).</summary>
        public CargoKind HoldCargoKind;

        // ── Trade ───────────────────────────────────────────────
        public TradeRoute Route;
        public float VoyageTimer;         // sim-seconds remaining on voyage
        public float VoyageDuration;      // total duration of current voyage

        // ── Condition ───────────────────────────────────────────
        public float Condition;           // 1.0 = pristine, 0 = sinking
        public bool NeedsRepair => Condition < 0.3f;

        // ── Sentinel ────────────────────────────────────────────
        public const int NO_SHIPYARD = -1;

        // ═══════════════════════════════════════════════════════════════
        // CONSTRUCTION
        // ═══════════════════════════════════════════════════════════════

        public Ship(int id, int width, int shipyardOrigin)
        {
            Id = id;
            Width = Mathf.Clamp(width, 1, 5);
            State = ShipState.Building;
            BuildProgress = 0f;
            ShipyardOrigin = shipyardOrigin;
            CrewCount = 0;
            CargoCount = 0;
            Condition = 1f;
            X = 0f;
            TargetCraneSlot = -1;

            // Initialize 2D module grid
            Grid = GetDefaultGrid(Width);
        }

        // ═══════════════════════════════════════════════════════════════
        // MODULE GRID SYSTEM
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Count how many cells have a specific module installed.</summary>
        public int CountModule(ShipModule module)
        {
            if (Grid == null) return 0;
            int count = 0;
            int cols = Grid.GetLength(0);
            int rows = Grid.GetLength(1);
            for (int c = 0; c < cols; c++)
                for (int r = 0; r < rows; r++)
                    if (Grid[c, r] == module) count++;
            return count;
        }

        /// <summary>Get the module at a grid position.</summary>
        public ShipModule GetModule(int col, int row)
        {
            if (Grid == null || col < 0 || row < 0
                || col >= Grid.GetLength(0) || row >= Grid.GetLength(1))
                return ShipModule.Air;
            return Grid[col, row];
        }

        /// <summary>Get module by linear index (col + row * Width) for backward compat.</summary>
        public ShipModule GetModule(int linearIndex)
        {
            if (Grid == null) return ShipModule.Air;
            int cols = Grid.GetLength(0);
            int col = linearIndex % cols;
            int row = linearIndex / cols;
            return GetModule(col, row);
        }

        /// <summary>
        /// Set / swap the module at a grid position. Cannot remove the last Helm.
        /// Returns true on success.
        /// </summary>
        public bool SetModule(int col, int row, ShipModule module)
        {
            if (Grid == null || col < 0 || row < 0
                || col >= Grid.GetLength(0) || row >= Grid.GetLength(1))
                return false;

            // Prevent removing the last Helm
            if (Grid[col, row] == ShipModule.Helm && module != ShipModule.Helm)
            {
                if (CountModule(ShipModule.Helm) <= 1)
                    return false;
            }

            // Prevent placing a second Helm
            if (module == ShipModule.Helm && Grid[col, row] != ShipModule.Helm)
            {
                if (CountModule(ShipModule.Helm) >= 1)
                    return false;
            }

            Grid[col, row] = module;
            return true;
        }

        /// <summary>Set module by linear index for backward compat.</summary>
        public bool SetModule(int linearIndex, ShipModule module)
        {
            if (Grid == null) return false;
            int cols = Grid.GetLength(0);
            int col = linearIndex % cols;
            int row = linearIndex / cols;
            return SetModule(col, row, module);
        }

        /// <summary>Does this ship have at least one Mast or Oars for propulsion?</summary>
        public bool HasPropulsion =>
            CountModule(ShipModule.Mast) > 0 || CountModule(ShipModule.Oars) > 0;

        /// <summary>Does this ship have a Crane module for self-loading?</summary>
        public bool HasCrane => CountModule(ShipModule.Crane) > 0;

        /// <summary>Does this ship have a Lookout module?</summary>
        public bool HasLookout => CountModule(ShipModule.Lookout) > 0;

        /// <summary>Does this ship have a FishingRig?</summary>
        public bool HasFishingRig => CountModule(ShipModule.FishingRig) > 0;

        /// <summary>
        /// Crew capacity derived from all modules in the 2D grid.
        /// </summary>
        private int GetGridCrewCapacity()
        {
            if (Grid == null) return GetBaseCrewCapacity(Width);
            int total = 0;
            int cols = Grid.GetLength(0);
            int rows = Grid.GetLength(1);
            for (int c = 0; c < cols; c++)
                for (int r = 0; r < rows; r++)
                    total += GetModuleCrewSlots(Grid[c, r]);
            return Mathf.Max(1, total);
        }

        /// <summary>
        /// Cargo capacity derived from Store modules in the 2D grid.
        /// Base: 1 per column. Each Store adds +3. Crane adds +1.
        /// </summary>
        private int GetGridCargoCapacity()
        {
            if (Grid == null) return GetBaseCargoCapacity(Width);
            int total = Width; // base 1 per column
            int cols = Grid.GetLength(0);
            int rows = Grid.GetLength(1);
            for (int c = 0; c < cols; c++)
                for (int r = 0; r < rows; r++)
                {
                    if (Grid[c, r] == ShipModule.Store) total += 3;
                    else if (Grid[c, r] == ShipModule.Crane) total += 1;
                }
            return total;
        }

        /// <summary>Crew slots contributed by a single module.</summary>
        public static int GetModuleCrewSlots(ShipModule module)
        {
            switch (module)
            {
                case ShipModule.Helm:       return 1;  // captain
                case ShipModule.Mast:       return 2;  // riggers
                case ShipModule.Cannon:     return 1;  // gunner
                case ShipModule.Crane:      return 1;  // operator
                case ShipModule.Oars:       return 1;  // rower
                case ShipModule.Cabin:      return 2;  // extra bunks
                case ShipModule.FishingRig: return 1;  // fisher
                case ShipModule.Lookout:    return 1;  // lookout
                case ShipModule.Kitchen:    return 1;  // cook
                default:                    return 0;
            }
        }

        /// <summary>
        /// Default 2D module grid for a given ship width.
        /// Provides a balanced starting configuration per hull class.
        /// [col, row] — col: stern(0) to bow(Width-1), row: hold(0) to top.
        /// </summary>
        public static ShipModule[,] GetDefaultGrid(int width)
        {
            int w = Mathf.Clamp(width, 1, 5);
            int h = GetHeight(w);
            var grid = new ShipModule[w, h];

            // Fill with Air by default
            for (int c = 0; c < w; c++)
                for (int r = 0; r < h; r++)
                    grid[c, r] = ShipModule.Air;

            switch (w)
            {
                case 1: // Canoe — single cell with oars
                    grid[0, 0] = ShipModule.Oars;
                    break;

                case 2: // Sloop (2×2)
                    // Row 1 (deck): Helm, Mast
                    grid[0, 1] = ShipModule.Helm;
                    grid[1, 1] = ShipModule.Mast;
                    // Row 0 (hold): Store, Store
                    grid[0, 0] = ShipModule.Store;
                    grid[1, 0] = ShipModule.Store;
                    break;

                case 3: // Brigantine (3×3)
                    // Row 2 (deck): Helm, Mast, Cannon
                    grid[0, 2] = ShipModule.Helm;
                    grid[1, 2] = ShipModule.Mast;
                    grid[2, 2] = ShipModule.Cannon;
                    // Row 1 (between-decks): Cabin, Store, Store
                    grid[0, 1] = ShipModule.Cabin;
                    grid[1, 1] = ShipModule.Store;
                    grid[2, 1] = ShipModule.Store;
                    // Row 0 (hold): Air, Store, Magazine
                    grid[1, 0] = ShipModule.Store;
                    grid[2, 0] = ShipModule.Magazine;
                    break;

                case 4: // Frigate (4×3)
                    // Row 2 (deck): Helm, Mast, Cannon, Mast
                    grid[0, 2] = ShipModule.Helm;
                    grid[1, 2] = ShipModule.Mast;
                    grid[2, 2] = ShipModule.Cannon;
                    grid[3, 2] = ShipModule.Mast;
                    // Row 1 (between-decks): Cabin, Store, Store, Cabin
                    grid[0, 1] = ShipModule.Cabin;
                    grid[1, 1] = ShipModule.Store;
                    grid[2, 1] = ShipModule.Store;
                    grid[3, 1] = ShipModule.Cabin;
                    // Row 0 (hold): Air, Store, Store, Magazine
                    grid[1, 0] = ShipModule.Store;
                    grid[2, 0] = ShipModule.Store;
                    grid[3, 0] = ShipModule.Magazine;
                    break;

                default: // Ship of the Line (5×4) — matches user spec
                    // Row 3 (superstructure): Wheel, Air, Sail, Crane, Air
                    grid[0, 3] = ShipModule.Wheel;
                    grid[2, 3] = ShipModule.Sail;
                    grid[3, 3] = ShipModule.Crane;
                    // Row 2 (deck): Helm, Lookout, SailBase, CraneBase, FigureHead
                    grid[0, 2] = ShipModule.Helm;
                    grid[1, 2] = ShipModule.Lookout;
                    grid[2, 2] = ShipModule.SailBase;
                    grid[3, 2] = ShipModule.CraneBase;
                    grid[4, 2] = ShipModule.FigureHead;
                    // Row 1 (between-decks): Cabin, Cabin, Kitchen, Cannon, Cabin
                    grid[0, 1] = ShipModule.Cabin;
                    grid[1, 1] = ShipModule.Cabin;
                    grid[2, 1] = ShipModule.Kitchen;
                    grid[3, 1] = ShipModule.Cannon;
                    grid[4, 1] = ShipModule.Cabin;
                    // Row 0 (hold): Air, Store, Store, Store, Magazine
                    grid[1, 0] = ShipModule.Store;
                    grid[2, 0] = ShipModule.Store;
                    grid[3, 0] = ShipModule.Store;
                    grid[4, 0] = ShipModule.Magazine;
                    break;
            }

            return grid;
        }

        /// <summary>Height (row count) for a given ship width.</summary>
        public static int GetHeight(int width)
        {
            switch (Mathf.Clamp(width, 1, 5))
            {
                case 1:  return 1;   // Canoe: single row
                case 2:  return 2;   // Sloop: deck + hold
                case 3:  return 3;   // Brigantine: deck + between-decks + hold
                case 4:  return 3;   // Frigate: deck + between-decks + hold
                default: return 4;   // Ship of the Line: super + deck + between + hold
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // HULL CLASSIFICATION
        // ═══════════════════════════════════════════════════════════════

        public static HullClass ClassifyHull(int width)
        {
            switch (Mathf.Clamp(width, 1, 5))
            {
                case 1:  return HullClass.Canoe;
                case 2:  return HullClass.Sloop;
                case 3:  return HullClass.Brigantine;
                case 4:  return HullClass.Frigate;
                default: return HullClass.ShipOfTheLine;
            }
        }

        /// <summary>Base crew capacity (legacy fallback when modules are null).</summary>
        public static int GetBaseCrewCapacity(int width)
        {
            switch (Mathf.Clamp(width, 1, 5))
            {
                case 1:  return 1;    // canoe: just the paddler
                case 2:  return 3;    // sloop: captain + 2 sailors
                case 3:  return 6;    // brigantine: captain + 3 sailors + 2 gunners
                case 4:  return 10;   // frigate: captain + 5 sailors + 4 gunners
                default: return 15;   // ship of the line: captain + 8 sailors + 6 gunners
            }
        }

        /// <summary>Gunner positions (legacy fallback). Use CountModule(Cannon) instead.</summary>
        public static int GetBaseGunnerSlots(int width)
        {
            switch (Mathf.Clamp(width, 1, 5))
            {
                case 1:  return 0;
                case 2:  return 0;
                case 3:  return 2;
                case 4:  return 4;
                default: return 6;
            }
        }

        /// <summary>Base cargo capacity (legacy fallback when modules are null).</summary>
        public static int GetBaseCargoCapacity(int width)
        {
            switch (Mathf.Clamp(width, 1, 5))
            {
                case 1:  return 2;    // canoe
                case 2:  return 4;    // sloop
                case 3:  return 8;    // brigantine
                case 4:  return 12;   // frigate
                default: return 18;   // ship of the line
            }
        }

        /// <summary>Build cost in wood for a ship of this width.</summary>
        public static int GetBuildCost(int width)
        {
            switch (Mathf.Clamp(width, 1, 5))
            {
                case 1:  return 2;
                case 2:  return 5;
                case 3:  return 10;
                case 4:  return 18;
                default: return 30;
            }
        }

        /// <summary>Ship speed factor — affected by modules and crew ratio.</summary>
        public float SpeedFactor
        {
            get
            {
                // Masts provide primary propulsion, Oars provide backup
                int masts = CountModule(ShipModule.Mast);
                int oars = CountModule(ShipModule.Oars);
                float propulsion = masts * 1.0f + oars * 0.5f;
                float baseSpeed = Mathf.Clamp(propulsion * 0.6f, 0.3f, 1.5f);
                float crewRatio = CrewCapacity > 0 ? (float)CrewCount / CrewCapacity : 0f;
                return baseSpeed * Mathf.Lerp(0.3f, 1f, crewRatio);
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // TRADE ROUTE PROPERTIES
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Base voyage duration in sim-seconds for a trade route.</summary>
        public static float GetRouteDuration(TradeRoute route)
        {
            switch (route)
            {
                case TradeRoute.CoastalFishing:  return 60f;    // ~1 sim-day
                case TradeRoute.TimberIslands:   return 120f;   // ~2 sim-days
                case TradeRoute.NearbyVillage:   return 180f;   // ~3 sim-days
                case TradeRoute.DistantEmpire:   return 420f;   // ~7 sim-days
                case TradeRoute.XibalbaCrossing: return 600f;   // ???
                default:                         return 120f;
            }
        }

        /// <summary>Base risk of losing the ship (0-1) for a trade route.</summary>
        public static float GetRouteRisk(TradeRoute route)
        {
            switch (route)
            {
                case TradeRoute.CoastalFishing:  return 0.02f;
                case TradeRoute.TimberIslands:   return 0.05f;
                case TradeRoute.NearbyVillage:   return 0.10f;
                case TradeRoute.DistantEmpire:   return 0.20f;
                case TradeRoute.XibalbaCrossing: return 0.40f;
                default:                         return 0.10f;
            }
        }

        /// <summary>
        /// Get the ship "slot role" for a given slot index.
        /// Slot 0 = Captain (from Helm), then per-module crew stations.
        /// </summary>
        public ShipSlotRole GetSlotRole(int slotIndex)
        {
            if (slotIndex == 0) return ShipSlotRole.Captain;
            // Walk grid cells and assign roles by accumulated crew slots
            int accumulated = 0;
            if (Grid != null)
            {
                int cols = Grid.GetLength(0);
                int rows = Grid.GetLength(1);
                for (int c = 0; c < cols; c++)
                {
                    for (int r = rows - 1; r >= 0; r--)  // top-down
                    {
                        var mod = Grid[c, r];
                        int slots = GetModuleCrewSlots(mod);
                        if (mod == ShipModule.Helm)
                        {
                            accumulated += slots;
                            continue;
                        }
                        if (slotIndex < accumulated + slots)
                        {
                            if (mod == ShipModule.Cannon) return ShipSlotRole.Gunner;
                            return ShipSlotRole.Sailor;
                        }
                        accumulated += slots;
                    }
                }
            }
            return ShipSlotRole.Sailor;
        }

        /// <summary>Default cargo kind for a trade route.</summary>
        public static CargoKind GetRouteCargoKind(TradeRoute route)
        {
            switch (route)
            {
                case TradeRoute.CoastalFishing:  return CargoKind.Fish;
                case TradeRoute.TimberIslands:   return CargoKind.Log;
                case TradeRoute.NearbyVillage:   return CargoKind.Crate;
                case TradeRoute.DistantEmpire:   return CargoKind.ExoticGoods;
                case TradeRoute.XibalbaCrossing: return CargoKind.ExoticGoods;
                default:                         return CargoKind.Crate;
            }
        }
    }

    /// <summary>
    /// Roles within a ship — like building SlotRoles but for vessels.
    /// </summary>
    public enum ShipSlotRole
    {
        Captain,    // slot 0 — commands the vessel
        Sailor,     // rigging, sails, oars
        Gunner,     // mans the cannons (Brigantine+)
    }
}
