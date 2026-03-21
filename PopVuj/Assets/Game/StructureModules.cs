// Copyright CodeGamified 2025-2026
// MIT License — PopVuj
using UnityEngine;

namespace PopVuj.Game
{
    /// <summary>
    /// Structure module type — what occupies each cell of a building's 2D grid.
    /// Buildings are a 2D array of modules [Width × Height] where:
    ///   - Columns run left(0) to right(Width-1)
    ///   - Rows run ground(0) to top(Height-1)
    ///
    /// Structure emerges from the layout. An L-shaped chapel:
    ///   Row 2: Bell    | Air  | Air
    ///   Row 1: Cross   | Air  | Air
    ///   Row 0: Altar   | Pew  | Pew
    ///
    /// Warehouse with cranes at top, storage below:
    ///   Row 2: Crane      | Crane      | Crane
    ///   Row 1: FoodStore  | GoodsStore | FoodStore
    ///   Row 0: WoodStore  | StoneStore | Desk
    ///
    /// Sewers are the underground "shadow":
    ///   # sewer layers = ceil(# building layers / 2)
    ///   1 floor = 1 sewer, 3 floors = 2 sewers.
    /// </summary>
    public enum StructureModule
    {
        Air          = 0,

        // ── Chapel ──────────────────────────────────────────────
        Bell         = 1,    // bell tower cell
        Altar        = 2,    // worship altar
        Pew          = 3,    // seating bench
        Lectern      = 4,    // reading stand
        Cross        = 5,    // religious symbol

        // ── House ───────────────────────────────────────────────
        Bed          = 10,   // sleeping quarters
        Table        = 11,   // dining/work table
        Fireplace    = 12,   // hearth

        // ── Workshop ────────────────────────────────────────────
        Anvil        = 20,   // metalworking
        Workbench    = 21,   // crafting surface
        Forge        = 22,   // smelting furnace

        // ── Warehouse ───────────────────────────────────────────
        WCrane       = 30,   // loading crane (must be top layer)
        WoodStore    = 31,   // wood storage bay
        StoneStore   = 32,   // stone storage bay
        FoodStore    = 33,   // food storage bay
        GoodsStore   = 34,   // goods storage bay
        Desk         = 35,   // keeper's desk

        // ── Market ──────────────────────────────────────────────
        Stall        = 40,   // market stall with counter + awning
        MarketCrate  = 41,   // stacked merchandise

        // ── Farm ────────────────────────────────────────────────
        Crop         = 50,   // planted crop row
        Trough       = 51,   // animal/water trough

        // ── Fountain ────────────────────────────────────────────
        Basin        = 60,   // water basin
        Spout        = 61,   // central column/spout

        // ── Shipyard ────────────────────────────────────────────
        DrydockFrame = 70,   // ship skeleton frame
        TimberStack  = 71,   // raw lumber pile

        // ── Pier ────────────────────────────────────────────────
        PierDeck     = 80,   // bare walkway
        PierCrane    = 81,   // treadwheel crane fixture
        PierCannon   = 82,   // defensive cannon fixture
        PierFishing  = 83,   // fishing pole fixture
    }

    /// <summary>
    /// Sewer module type — what occupies each cell of a sewer's 2D grid.
    /// Sewer layers = ceil(building layers / 2):
    ///   1 floor building = 1 sewer layer
    ///   3 floor building = 2 sewer layers
    /// </summary>
    public enum SewerModule
    {
        Air          = 0,

        // ── Drain (beneath small houses) ────────────────────────
        Pipe         = 1,
        Drip         = 2,

        // ── Den (beneath large houses) ──────────────────────────
        Bedroll      = 10,
        Barrel       = 11,

        // ── Crypt (beneath chapel) ──────────────────────────────
        Sarcophagus  = 20,
        CryptColumn  = 21,
        CryptAltar   = 22,
        Shrine       = 23,   // small worship niche with relics
        Tomb         = 24,   // stone burial slab
        Pentagram    = 25,   // occult circle / ritual site
        SewerPew     = 26,   // underground seating (mirroring chapel pews)

        // ── Tunnel (beneath workshop) ───────────────────────────
        Rail         = 30,
        SupportBeam  = 31,

        // ── Cistern (beneath fountain) ──────────────────────────
        Pool         = 40,
        CisternWall  = 41,

        // ── Bazaar (beneath market) ─────────────────────────────
        BazaarStall  = 50,
        Chest        = 51,
        Lantern      = 52,

        // ── Drydock (beneath shipyard) ──────────────────────────
        CradleBlock  = 60,
        DockWater    = 61,

        // ── Vault (beneath warehouse) ───────────────────────────
        GoldPile     = 70,
        TaxChest     = 71,
        VaultPillar  = 72,
        Gate         = 73,

        // ── Canal (connection passage beneath empty/tree) ────────
        CanalWalk    = 80,   // simple walkway — traversable connection
        CanalArch    = 81,   // arched ceiling support
        CanalDrip    = 82,   // drip / moisture point
    }

    /// <summary>
    /// Static factory for building and sewer 2D module grids.
    /// Analogous to Ship.GetDefaultGrid() for ships.
    /// </summary>
    public static class StructureGrids
    {
        /// <summary>Sewer layers = ceil(buildingLayers / 2).</summary>
        public static int GetSewerLayerCount(int buildingLayers)
        {
            return Mathf.Max(0, Mathf.CeilToInt(buildingLayers / 2f));
        }

        /// <summary>Building layers (rows) for a given type.</summary>
        public static int GetBuildingLayers(CellType type)
        {
            switch (type)
            {
                case CellType.House:     return 2;
                case CellType.Chapel:    return 3;
                case CellType.Workshop:  return 2;
                case CellType.Farm:      return 1;
                case CellType.Market:    return 1;
                case CellType.Fountain:  return 1;
                case CellType.Shipyard:  return 2;
                case CellType.Pier:      return 1;
                case CellType.Warehouse: return 3;
                default:                 return 1;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // STRUCTURE GRID
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Default 2D structure grid for a building type and width.
        /// [col, row] — col: left(0) to right(Width-1), row: ground(0) to top.
        /// </summary>
        public static StructureModule[,] GetDefaultGrid(CellType type, int width,
            PierFixture[] pierFixtures = null)
        {
            int w = Mathf.Max(1, width);
            int h = GetBuildingLayers(type);
            var grid = new StructureModule[w, h];

            switch (type)
            {
                case CellType.Chapel:    FillChapel(grid, w, h);                break;
                case CellType.House:     FillHouse(grid, w, h);                 break;
                case CellType.Workshop:  FillWorkshop(grid, w, h);              break;
                case CellType.Farm:      FillFarm(grid, w, h);                  break;
                case CellType.Market:    FillMarket(grid, w, h);                break;
                case CellType.Fountain:  FillFountain(grid, w, h);              break;
                case CellType.Shipyard:  FillShipyard(grid, w, h);              break;
                case CellType.Pier:      FillPier(grid, w, h, pierFixtures);    break;
                case CellType.Warehouse: FillWarehouse(grid, w, h);             break;
            }

            return grid;
        }

        // ═══════════════════════════════════════════════════════════════
        // SEWER GRID
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Default 2D sewer grid derived from building type and dimensions.
        /// </summary>
        public static SewerModule[,] GetDefaultSewerGrid(SewerType sewType, int width,
            int buildingLayers)
        {
            int w = Mathf.Max(1, width);
            int h = GetSewerLayerCount(buildingLayers);
            if (h <= 0 || sewType == SewerType.None)
                return new SewerModule[w, Mathf.Max(1, h)];

            var grid = new SewerModule[w, h];

            switch (sewType)
            {
                case SewerType.Drain:   FillDrain(grid, w, h);   break;
                case SewerType.Den:     FillDen(grid, w, h);     break;
                case SewerType.Crypt:   FillCrypt(grid, w, h);   break;
                case SewerType.Tunnel:  FillTunnel(grid, w, h);  break;
                case SewerType.Cistern: FillCistern(grid, w, h); break;
                case SewerType.Bazaar:  FillBazaar(grid, w, h);  break;
                case SewerType.Drydock: FillDrydock(grid, w, h); break;
                case SewerType.Vault:   FillVault(grid, w, h);   break;
                case SewerType.Canal:   FillCanal(grid, w, h);   break;
            }

            return grid;
        }

        // ═══════════════════════════════════════════════════════════════
        // BUILDING GRID FILLS
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Chapel: L-shape with Bell tower at top-left, Altar + Pews on ground.
        ///   Row 2: Bell    | Air  | Air
        ///   Row 1: Cross   | Air  | Air
        ///   Row 0: Altar   | Pew  | Pew
        /// </summary>
        private static void FillChapel(StructureModule[,] g, int w, int h)
        {
            // Top: Bell at left
            g[0, h - 1] = StructureModule.Bell;

            // Middle rows: Cross / Lectern at left
            if (h >= 3)
                g[0, 1] = StructureModule.Cross;

            // Ground: Altar at left, Pews fill right
            g[0, 0] = StructureModule.Altar;
            for (int c = 1; c < w; c++)
                g[c, 0] = StructureModule.Pew;
        }

        private static void FillHouse(StructureModule[,] g, int w, int h)
        {
            // Top row: Beds
            for (int c = 0; c < w; c++)
                g[c, h - 1] = StructureModule.Bed;

            // Ground: Table(s) + Fireplace at right end
            if (h >= 2)
            {
                for (int c = 0; c < w; c++)
                    g[c, 0] = (c == w - 1) ? StructureModule.Fireplace : StructureModule.Table;
            }
        }

        private static void FillWorkshop(StructureModule[,] g, int w, int h)
        {
            // Ground: Workbenches, Anvils, Forge at right end
            for (int c = 0; c < w; c++)
            {
                if (c == w - 1 && w >= 2)
                    g[c, 0] = StructureModule.Forge;
                else if (c % 2 == 0)
                    g[c, 0] = StructureModule.Workbench;
                else
                    g[c, 0] = StructureModule.Anvil;
            }
            // Row 1+ Air (open workspace above)
        }

        private static void FillFarm(StructureModule[,] g, int w, int h)
        {
            for (int c = 0; c < w; c++)
                g[c, 0] = (c % 3 == 2) ? StructureModule.Trough : StructureModule.Crop;
        }

        private static void FillMarket(StructureModule[,] g, int w, int h)
        {
            for (int c = 0; c < w; c++)
                g[c, 0] = (c % 2 == 0) ? StructureModule.Stall : StructureModule.MarketCrate;
        }

        private static void FillFountain(StructureModule[,] g, int w, int h)
        {
            int mid = w / 2;
            for (int c = 0; c < w; c++)
                g[c, 0] = (c == mid) ? StructureModule.Spout : StructureModule.Basin;
        }

        private static void FillShipyard(StructureModule[,] g, int w, int h)
        {
            for (int c = 0; c < w; c++)
                g[c, 0] = StructureModule.DrydockFrame;
            if (h >= 2)
                for (int c = 0; c < w; c++)
                    g[c, 1] = StructureModule.TimberStack;
        }

        private static void FillPier(StructureModule[,] g, int w, int h,
            PierFixture[] fixtures)
        {
            for (int c = 0; c < w; c++)
            {
                PierFixture fix = (fixtures != null && c < fixtures.Length)
                    ? fixtures[c] : PierFixture.None;
                switch (fix)
                {
                    case PierFixture.Crane:       g[c, 0] = StructureModule.PierCrane;   break;
                    case PierFixture.Cannon:      g[c, 0] = StructureModule.PierCannon;  break;
                    case PierFixture.FishingPole: g[c, 0] = StructureModule.PierFishing; break;
                    default:                      g[c, 0] = StructureModule.PierDeck;    break;
                }
            }
        }

        /// <summary>
        /// Warehouse: Cranes at top layer, storage types below.
        ///   Row 2: WCrane     | WCrane     | WCrane
        ///   Row 1: FoodStore  | GoodsStore | FoodStore
        ///   Row 0: WoodStore  | StoneStore | Desk
        /// </summary>
        private static void FillWarehouse(StructureModule[,] g, int w, int h)
        {
            // Top row: Cranes
            for (int c = 0; c < w; c++)
                g[c, h - 1] = StructureModule.WCrane;

            // Bottom row: WoodStore/StoneStore + optional Desk at end
            for (int c = 0; c < w; c++)
            {
                if (c == w - 1 && w >= 3)
                    g[c, 0] = StructureModule.Desk;
                else if (c % 2 == 0)
                    g[c, 0] = StructureModule.WoodStore;
                else
                    g[c, 0] = StructureModule.StoneStore;
            }

            // Middle row(s): FoodStore / GoodsStore
            for (int r = 1; r < h - 1; r++)
            {
                for (int c = 0; c < w; c++)
                    g[c, r] = (c % 2 == 0) ? StructureModule.FoodStore : StructureModule.GoodsStore;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // SEWER GRID FILLS
        // ═══════════════════════════════════════════════════════════════

        private static void FillDrain(SewerModule[,] g, int w, int h)
        {
            for (int c = 0; c < w; c++)
                for (int r = 0; r < h; r++)
                    g[c, r] = (c == w / 2 && r == h - 1) ? SewerModule.Drip : SewerModule.Pipe;
        }

        private static void FillDen(SewerModule[,] g, int w, int h)
        {
            for (int c = 0; c < w; c++)
                for (int r = 0; r < h; r++)
                    g[c, r] = (c == w - 1 && r == 0) ? SewerModule.Barrel : SewerModule.Bedroll;
        }

        /// <summary>
        /// Crypt: Inverted chapel shadow underground.
        /// Sewer layers = ceil(building layers / 2).
        /// For a 4-wide, 3-tall chapel (2 sewer layers):
        ///   Row 1: Shrine   | SewerPew | SewerPew  | Pentagram
        ///   Row 0: Sarcophagus | Tomb  | Sarcophagus | Air
        /// Air cells make the sewer L-shaped (non-rectangular), mirroring
        /// how Air shapes buildings above ground.
        /// </summary>
        private static void FillCrypt(SewerModule[,] g, int w, int h)
        {
            // Row 0 (deepest): Sarcophagi with Tomb in between, Air at far right
            for (int c = 0; c < w; c++)
            {
                if (c == w - 1 && w >= 3)
                    g[c, 0] = SewerModule.Air;     // ground cutout — non-rectangular shape
                else if (c % 2 == 1)
                    g[c, 0] = SewerModule.Tomb;
                else
                    g[c, 0] = SewerModule.Sarcophagus;
            }

            // Row 1+ (upper layers): Shrine at left, SewerPews in middle, Pentagram at right
            for (int r = 1; r < h; r++)
            {
                g[0, r] = SewerModule.Shrine;
                for (int c = 1; c < w - 1; c++)
                    g[c, r] = SewerModule.SewerPew;
                if (w >= 2)
                    g[w - 1, r] = SewerModule.Pentagram;
            }

            // Single-wide chapel: just stack Sarcophagus / Shrine
            if (w == 1 && h >= 2)
                g[0, 1] = SewerModule.Shrine;
        }

        private static void FillTunnel(SewerModule[,] g, int w, int h)
        {
            for (int c = 0; c < w; c++)
                for (int r = 0; r < h; r++)
                    g[c, r] = (c % 2 == 0) ? SewerModule.Rail : SewerModule.SupportBeam;
        }

        private static void FillCistern(SewerModule[,] g, int w, int h)
        {
            for (int c = 0; c < w; c++)
                for (int r = 0; r < h; r++)
                    g[c, r] = (c == 0 || c == w - 1) ? SewerModule.CisternWall : SewerModule.Pool;
        }

        private static void FillBazaar(SewerModule[,] g, int w, int h)
        {
            for (int c = 0; c < w; c++)
                for (int r = 0; r < h; r++)
                {
                    if (r == h - 1 && c == w / 2)
                        g[c, r] = SewerModule.Lantern;
                    else
                        g[c, r] = (c % 2 == 0) ? SewerModule.BazaarStall : SewerModule.Chest;
                }
        }

        private static void FillDrydock(SewerModule[,] g, int w, int h)
        {
            for (int c = 0; c < w; c++)
                for (int r = 0; r < h; r++)
                    g[c, r] = (r == 0) ? SewerModule.DockWater : SewerModule.CradleBlock;
        }

        /// <summary>
        /// Vault: Gold + Pillars at bottom, TaxChests + Gate at top.
        ///   Row 1: TaxChest    | Gate        | TaxChest
        ///   Row 0: VaultPillar | GoldPile    | VaultPillar
        /// </summary>
        private static void FillVault(SewerModule[,] g, int w, int h)
        {
            for (int c = 0; c < w; c++)
            {
                for (int r = 0; r < h; r++)
                {
                    if (r == h - 1)
                        g[c, r] = (c == w / 2) ? SewerModule.Gate : SewerModule.TaxChest;
                    else
                        g[c, r] = (c == 0 || c == w - 1) ? SewerModule.VaultPillar : SewerModule.GoldPile;
                }
            }
        }

        /// <summary>
        /// Canal: Simple connection passage beneath empty/tree land.
        /// Always 1 layer tall — just enough for traversal.
        ///   Row 0: CanalWalk | CanalWalk | ...
        /// </summary>
        private static void FillCanal(SewerModule[,] g, int w, int h)
        {
            for (int c = 0; c < w; c++)
                g[c, 0] = SewerModule.CanalWalk;
        }
    }
}
