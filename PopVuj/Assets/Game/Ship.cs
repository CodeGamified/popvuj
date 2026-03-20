// Copyright CodeGamified 2025-2026
// MIT License — PopVuj
using UnityEngine;

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
    /// A ship — a modular "floating building" with crew slots, cargo hold,
    /// and a state machine. Width determines hull class and all derived stats.
    ///
    /// Ships travel on the road surface (Z=0) and dock along the pier's X
    /// extent. The pier must be long enough to accommodate all docked ships
    /// (sum of ship widths ≤ pier length). Cranes service ships by spatial
    /// overlap — any crane whose X range overlaps a ship can load/unload it.
    ///
    /// Like buildings, ships have slots: sailors man positions (helm, sails,
    /// cannons, oars) the way worshippers fill chapel pews.
    ///
    /// Ship roles by slot index:
    ///   Slot 0        = Captain (always — helm position)
    ///   Slot 1..N     = Sailors (rigging, sails, oars)
    ///   Last slots    = Gunners (if Brigantine+, man the cannons)
    /// </summary>
    public class Ship
    {
        // ── Identity ────────────────────────────────────────────
        public int Id;

        // ── Dimensions (tile-width, like buildings) ─────────────
        public int Width;

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

        // ── Crew ────────────────────────────────────────────────
        public int CrewCount;             // minions currently aboard
        public int CrewCapacity => GetCrewCapacity(Width);
        public int GunnerSlots => GetGunnerSlots(Width);

        // ── Cargo hold ──────────────────────────────────────────
        public int CargoCount;            // cargo units currently loaded
        public int CargoCapacity => GetCargoCapacity(Width);

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

        /// <summary>Total crew slots (captain + sailors + gunners).</summary>
        public static int GetCrewCapacity(int width)
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

        /// <summary>Gunner positions (cannons). Only Brigantine+ have them.</summary>
        public static int GetGunnerSlots(int width)
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

        /// <summary>Cargo hold capacity (number of cargo units).</summary>
        public static int GetCargoCapacity(int width)
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

        /// <summary>Ship speed factor — affects voyage duration and departure/arrival animation.</summary>
        public float SpeedFactor
        {
            get
            {
                // Base speed from hull class, boosted by crew ratio
                float baseSpeed = Width >= 3 ? 1.2f : 1f;
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
        /// Slot 0 = Captain, then Sailors, then Gunners at the end.
        /// </summary>
        public ShipSlotRole GetSlotRole(int slotIndex)
        {
            if (slotIndex == 0) return ShipSlotRole.Captain;
            int gunnerStart = CrewCapacity - GunnerSlots;
            if (slotIndex >= gunnerStart) return ShipSlotRole.Gunner;
            return ShipSlotRole.Sailor;
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
