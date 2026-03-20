// Copyright CodeGamified 2025-2026
// MIT License — PopVuj
using PopVuj.Game;

namespace PopVuj.Crew
{
    /// <summary>
    /// Roles a minion can occupy within a building slot.
    /// </summary>
    public enum SlotRole
    {
        Resident,     // living in a house
        Preacher,     // behind the lectern in a chapel
        Worshipper,   // in the pews of a chapel
        Worker,       // at a workbench in a workshop
        Farmer,       // tending crops on a farm
        Merchant,     // selling at a market stall
        Caretaker,    // maintaining a fountain
        Foreman,      // supervises shipyard construction
        Shipwright,   // builds/repairs ships at shipyard
        Docker,       // carries cargo along the pier
        CraneOperator,// operates a pier crane
        Sailor,       // crews a ship (boards vessel, leaves road)
        WarehouseKeeper, // manages warehouse inventory
    }

    /// <summary>
    /// Static slot definitions per building type.
    /// Slot count scales with building width — wider buildings have more throughput.
    ///
    /// Examples:
    ///   Chapel (1w): 1 preacher + 2 pews  = 3 slots
    ///   Chapel (3w): 1 preacher + 6 pews  = 7 slots
    ///   Workshop (1w): 2 workers
    ///   Workshop (5w): 10 workers  ("large workshop blacksmith")
    ///   House (1w): 2 residents
    ///   House (3w): 6 residents
    /// </summary>
    public static class BuildingSlots
    {
        /// <summary>Total minion slots for a building type at a given tile width.</summary>
        public static int GetSlotCount(CellType type, int buildingWidth)
        {
            if (buildingWidth < 1) return 0;
            switch (type)
            {
                case CellType.House:    return buildingWidth * 2;
                case CellType.Chapel:   return 1 + buildingWidth * 2;   // 1 preacher + pews
                case CellType.Workshop: return buildingWidth * 2;
                case CellType.Farm:     return buildingWidth * 2;
                case CellType.Market:   return 1 + buildingWidth;       // 1 vendor + stalls
                case CellType.Fountain: return 1;
                case CellType.Shipyard: return 1 + buildingWidth * 2;   // 1 foreman + shipwrights
                case CellType.Pier:      return buildingWidth;           // 1 slot per tile (role depends on fixture)
                case CellType.Warehouse: return buildingWidth + 1 + buildingWidth; // crane ops + keeper + haulers
                default:                return 0;
            }
        }

        /// <summary>What role does slot index <paramref name="slotIndex"/> serve in this building?</summary>
        public static SlotRole GetSlotRole(CellType type, int slotIndex, int buildingWidth = 1)
        {
            switch (type)
            {
                case CellType.House:    return SlotRole.Resident;
                case CellType.Chapel:   return slotIndex == 0 ? SlotRole.Preacher : SlotRole.Worshipper;
                case CellType.Workshop: return SlotRole.Worker;
                case CellType.Farm:     return SlotRole.Farmer;
                case CellType.Market:   return SlotRole.Merchant;
                case CellType.Fountain: return SlotRole.Caretaker;
                case CellType.Shipyard: return slotIndex == 0 ? SlotRole.Foreman : SlotRole.Shipwright;
                case CellType.Pier:      return SlotRole.Docker;
                case CellType.Warehouse:
                    // Slots 0..bw-1: CraneOperator, slot bw: WarehouseKeeper, slots bw+1..: Worker
                    if (slotIndex < buildingWidth) return SlotRole.CraneOperator;
                    if (slotIndex == buildingWidth) return SlotRole.WarehouseKeeper;
                    return SlotRole.Worker;
                default:                return SlotRole.Resident;
            }
        }

        /// <summary>Get the slot role for a pier tile with a specific fixture.</summary>
        public static SlotRole GetPierSlotRole(PierFixture fixture)
        {
            switch (fixture)
            {
                case PierFixture.Crane:       return SlotRole.CraneOperator;
                case PierFixture.Cannon:      return SlotRole.Docker; // reuse for now
                case PierFixture.FishingPole: return SlotRole.Docker; // reuse for now
                default:                      return SlotRole.Docker;
            }
        }
    }
}
