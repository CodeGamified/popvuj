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
    /// Deck module type — what is installed on each 1-unit-wide tile of the ship.
    /// Analogous to PierFixture for pier tiles. Each tile-width position along
    /// the ship deck can hold a different module, allowing customization:
    ///
    ///   A 3-wide Brigantine might be:
    ///     [Cannon | Mast | Helm]     — warship config
    ///     [CargoHatch | Crane | Helm] — trade hauler config
    ///     [FishingRig | Mast | Helm]  — fishing vessel config
    ///
    /// Modules affect derived stats: crew capacity, cargo capacity, speed, combat.
    /// The Helm module is always required (placed at stern by default).
    /// </summary>
    public enum DeckModule
    {
        None         = 0,   // empty deck space — walkway only
        Helm         = 1,   // captain's station — required, 1 per ship (adds 1 crew: captain)
        Mast         = 2,   // sailing mast with canvas — propulsion (adds 2 crew: riggers)
        Cannon       = 3,   // gun emplacement — combat power (adds 1 crew: gunner)
        Crane        = 4,   // cargo loading crane — faster load/unload (adds 1 crew: operator)
        Oars         = 5,   // rowing station — backup propulsion, no wind needed (adds 1 crew: rower)
        CargoHatch   = 6,   // hold access hatch — extra cargo storage (+3 cargo capacity)
        Cabin        = 7,   // crew quarters below deck — extra bunks (+2 crew capacity)
        FishingRig   = 8,   // fishing station — passive food income on voyage (adds 1 crew: fisher)
        Lookout      = 9,   // elevated crow's nest platform — detection range bonus (adds 1 crew: lookout)
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

        // ── Deck Modules (per-tile customization) ───────────────
        /// <summary>
        /// One module per tile-width, indexed stern(0) to bow(Width-1).
        /// Like PierFixture for pier tiles — each slot can hold a different module.
        /// </summary>
        public DeckModule[] Modules;

        // ── Crew (now module-derived) ───────────────────────────
        public int CrewCount;             // minions currently aboard
        public int CrewCapacity => GetModuleCrewCapacity();
        public int GunnerSlots => CountModule(DeckModule.Cannon);

        // ── Cargo hold (now module-derived) ─────────────────────
        public int CargoCount;            // cargo units currently loaded
        public int CargoCapacity => GetModuleCargoCapacity();

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

            // Initialize default deck modules for this hull class
            Modules = GetDefaultModules(Width);
        }

        // ═══════════════════════════════════════════════════════════════
        // DECK MODULE SYSTEM
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Count how many tiles have a specific module installed.</summary>
        public int CountModule(DeckModule module)
        {
            int count = 0;
            if (Modules == null) return 0;
            for (int i = 0; i < Modules.Length; i++)
                if (Modules[i] == module) count++;
            return count;
        }

        /// <summary>Get the module at a specific tile index (0=stern, Width-1=bow).</summary>
        public DeckModule GetModule(int tileIndex)
        {
            if (Modules == null || tileIndex < 0 || tileIndex >= Modules.Length)
                return DeckModule.None;
            return Modules[tileIndex];
        }

        /// <summary>
        /// Set / swap the module at a tile index. Cannot remove the last Helm.
        /// Returns true on success.
        /// </summary>
        public bool SetModule(int tileIndex, DeckModule module)
        {
            if (Modules == null || tileIndex < 0 || tileIndex >= Modules.Length)
                return false;

            // Prevent removing the last Helm — every ship needs exactly one
            if (Modules[tileIndex] == DeckModule.Helm && module != DeckModule.Helm)
            {
                if (CountModule(DeckModule.Helm) <= 1)
                    return false;
            }

            // Prevent placing a second Helm
            if (module == DeckModule.Helm && Modules[tileIndex] != DeckModule.Helm)
            {
                if (CountModule(DeckModule.Helm) >= 1)
                    return false;
            }

            Modules[tileIndex] = module;
            return true;
        }

        /// <summary>Does this ship have at least one Mast or Oars for propulsion?</summary>
        public bool HasPropulsion =>
            CountModule(DeckModule.Mast) > 0 || CountModule(DeckModule.Oars) > 0;

        /// <summary>Does this ship have a Crane module for self-loading?</summary>
        public bool HasCrane => CountModule(DeckModule.Crane) > 0;

        /// <summary>Does this ship have a Lookout module?</summary>
        public bool HasLookout => CountModule(DeckModule.Lookout) > 0;

        /// <summary>Does this ship have a FishingRig?</summary>
        public bool HasFishingRig => CountModule(DeckModule.FishingRig) > 0;

        /// <summary>
        /// Crew capacity derived from installed modules.
        /// Each module type contributes a fixed number of crew stations:
        ///   Helm=1(captain), Mast=2(riggers), Cannon=1(gunner),
        ///   Crane=1(operator), Oars=1(rower), Cabin=2(extra bunks),
        ///   FishingRig=1(fisher), Lookout=1(lookout), CargoHatch=0, None=0
        /// </summary>
        private int GetModuleCrewCapacity()
        {
            if (Modules == null) return GetBaseCrewCapacity(Width);
            int total = 0;
            for (int i = 0; i < Modules.Length; i++)
                total += GetModuleCrewSlots(Modules[i]);
            return Mathf.Max(1, total); // always at least 1 (the paddler/oarsman)
        }

        /// <summary>
        /// Cargo capacity derived from installed modules.
        /// Base: 1 per tile-width. Each CargoHatch adds +3. Crane adds +1.
        /// </summary>
        private int GetModuleCargoCapacity()
        {
            if (Modules == null) return GetBaseCargoCapacity(Width);
            int total = Width; // base 1 per tile
            for (int i = 0; i < Modules.Length; i++)
            {
                if (Modules[i] == DeckModule.CargoHatch) total += 3;
                else if (Modules[i] == DeckModule.Crane) total += 1;
            }
            return total;
        }

        /// <summary>Crew slots contributed by a single module.</summary>
        public static int GetModuleCrewSlots(DeckModule module)
        {
            switch (module)
            {
                case DeckModule.Helm:       return 1;  // captain
                case DeckModule.Mast:       return 2;  // riggers
                case DeckModule.Cannon:     return 1;  // gunner
                case DeckModule.Crane:      return 1;  // operator
                case DeckModule.Oars:       return 1;  // rower
                case DeckModule.Cabin:      return 2;  // extra bunks
                case DeckModule.FishingRig: return 1;  // fisher
                case DeckModule.Lookout:    return 1;  // lookout
                case DeckModule.CargoHatch: return 0;
                case DeckModule.None:       return 0;
                default:                    return 0;
            }
        }

        /// <summary>
        /// Default module layout for a given ship width.
        /// Provides a balanced starting configuration per hull class.
        /// Index 0 = stern (helm), last index = bow.
        /// </summary>
        public static DeckModule[] GetDefaultModules(int width)
        {
            switch (Mathf.Clamp(width, 1, 5))
            {
                case 1: // Canoe — just oars
                    return new[] { DeckModule.Oars };

                case 2: // Sloop — helm + mast
                    return new[] { DeckModule.Helm, DeckModule.Mast };

                case 3: // Brigantine — helm + mast + cannon
                    return new[] { DeckModule.Helm, DeckModule.Mast, DeckModule.Cannon };

                case 4: // Frigate — helm + mast + cannon + mast
                    return new[] { DeckModule.Helm, DeckModule.Mast, DeckModule.Cannon, DeckModule.Mast };

                default: // Ship of the Line — helm + mast + cannon + cannon + mast
                    return new[] { DeckModule.Helm, DeckModule.Mast, DeckModule.Cannon, DeckModule.Cannon, DeckModule.Mast };
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
                int masts = CountModule(DeckModule.Mast);
                int oars = CountModule(DeckModule.Oars);
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
            // Walk modules and assign roles by accumulated crew slots
            int accumulated = 0;
            if (Modules != null)
            {
                for (int m = 0; m < Modules.Length; m++)
                {
                    int slots = GetModuleCrewSlots(Modules[m]);
                    if (Modules[m] == DeckModule.Helm)
                    {
                        // Helm's 1 slot is the captain (slotIndex 0)
                        accumulated += slots;
                        continue;
                    }
                    if (slotIndex < accumulated + slots)
                    {
                        if (Modules[m] == DeckModule.Cannon) return ShipSlotRole.Gunner;
                        return ShipSlotRole.Sailor;
                    }
                    accumulated += slots;
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
