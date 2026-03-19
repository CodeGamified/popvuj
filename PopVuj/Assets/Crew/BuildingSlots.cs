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
                default:                return 0;
            }
        }

        /// <summary>What role does slot index <paramref name="slotIndex"/> serve in this building?</summary>
        public static SlotRole GetSlotRole(CellType type, int slotIndex)
        {
            switch (type)
            {
                case CellType.House:    return SlotRole.Resident;
                case CellType.Chapel:   return slotIndex == 0 ? SlotRole.Preacher : SlotRole.Worshipper;
                case CellType.Workshop: return SlotRole.Worker;
                case CellType.Farm:     return SlotRole.Farmer;
                case CellType.Market:   return SlotRole.Merchant;
                case CellType.Fountain: return SlotRole.Caretaker;
                default:                return SlotRole.Resident;
            }
        }
    }
}
