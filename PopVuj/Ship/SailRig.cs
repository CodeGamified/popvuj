// Copyright SeaRäuber 2025-2026
// MIT License
using UnityEngine;

namespace SeaRauber.Ship
{
    // ═══════════════════════════════════════════════════════════════
    // SAIL TYPES & STATE
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Sail rig type — determines how the sail interacts with wind.
    /// Square rigs are good downwind, fore-and-aft rigs excel upwind.
    /// </summary>
    public enum SailType
    {
        Square,       // Hung from yards, perpendicular to keel. Good downwind.
        ForeAndAft,   // Along the keel (gaff, bermuda). Good upwind.
        Jib,          // Triangular headsail on forestay. Fine upwind trim.
        Staysail,     // Between masts on stays. Fills gaps in sail plan.
        Spanker,      // Fore-and-aft on mizzen. Balances helm.
        Lateen,       // Triangular on tilted yard. Mediterranean.
    }

    /// <summary>
    /// State of an individual sail. Crew must work aloft to change.
    /// </summary>
    public enum SailState
    {
        Furled,       // Gathered and lashed to yard/boom. No area.
        Set,          // Drawing wind. Full area (minus reef).
        Reefed,       // Partially furled to reduce area. Storm tactic.
        Torn,         // Damaged. Flapping. Negative efficiency (drag).
        Luffing,      // Sail flapping — not trimmed to wind. Crew needed.
        Aback,        // Wind hitting wrong side. Dangerous. Slows ship.
    }

    /// <summary>
    /// Running rigging type — lines that crew haul to control sails.
    /// Each line is a crew interaction point on deck or aloft.
    /// </summary>
    public enum LineType
    {
        Sheet,        // Controls sail angle to wind. At rail, port/starboard.
        Halyard,      // Raises/lowers sail or yard. At mast base.
        Brace,        // Rotates yard (square rig only). At rail.
        Clew,         // Pulls up corners of square sail for furling. On yard.
        Bunt,         // Gathers sail body for furling. On yard.
        Reef,         // Ties reef points to reduce area. On boom or yard.
        Vang,         // Controls boom vertical angle. At mast base.
        Topping,      // Supports boom when sail furled. At mast base.
        Downhaul,     // Pulls jib/staysail down along stay. At bow.
    }

    // ═══════════════════════════════════════════════════════════════
    // INDIVIDUAL SAIL
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// One physical sail on the ship.
    ///
    /// Naming convention (square rig, bottom to top):
    ///   Course → Topsail → Topgallant → Royal
    /// Fore-and-aft:
    ///   Mainsail, Foresail, Jib, Staysail, Spanker
    ///
    /// Each sail has its own state, area, condition — and needs crew
    /// on the yard to furl/set, crew on deck to trim sheets.
    /// </summary>
    [System.Serializable]
    public struct IndividualSail
    {
        public string Name;
        public SailType Type;
        public SailState State;

        /// <summary>Full area in m² when set with no reef.</summary>
        public float FullArea;

        /// <summary>Condition 0-1. 1=new canvas, 0=shredded. Below 0.3 tears in storms.</summary>
        [Range(0f, 1f)]
        public float Condition;

        /// <summary>Reef level 0-3. Each reef reduces effective area by ~25%.</summary>
        public int ReefLevel;

        /// <summary>Sheet trim angle 0-1. 0=sheeted hard (close-hauled), 1=eased (running).</summary>
        [Range(0f, 1f)]
        public float SheetTrim;

        /// <summary>Which mast this sail belongs to (index into MastRig[]).</summary>
        public int MastIndex;

        /// <summary>Vertical order on the mast. 0=lowest (course), increases upward.</summary>
        public int VerticalOrder;

        /// <summary>Effective sail area right now (accounting for state, reef, condition).</summary>
        public float EffectiveArea
        {
            get
            {
                if (State == SailState.Furled) return 0f;
                if (State == SailState.Torn) return -FullArea * 0.1f; // drag
                if (State == SailState.Aback) return -FullArea * 0.3f; // reverse force
                if (State == SailState.Luffing) return FullArea * 0.05f; // flapping

                float area = FullArea;
                // Reef: each level removes ~25% of area
                area *= 1f - (ReefLevel * 0.25f);
                // Condition degrades output
                area *= Mathf.Lerp(0.2f, 1f, Condition);
                return area;
            }
        }

        public IndividualSail(string name, SailType type, float area, int mastIndex, int vOrder)
        {
            Name = name;
            Type = type;
            State = SailState.Furled;
            FullArea = area;
            Condition = 1f;
            ReefLevel = 0;
            SheetTrim = 0.5f;
            MastIndex = mastIndex;
            VerticalOrder = vOrder;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // RIGGING LINE (crew-operable control line)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// A single control line that a crew member can haul.
    /// Each line has a position on the ship where a sailor stands to work it.
    /// Port sheets are on the port rail, starboard sheets on starboard, etc.
    /// </summary>
    [System.Serializable]
    public struct RiggingLine
    {
        public string Name;
        public LineType Type;

        /// <summary>Local position where crew stands to operate this line.</summary>
        public Vector3 StationPosition;

        /// <summary>Which sail this line controls (-1 for general/structural).</summary>
        public int SailIndex;

        /// <summary>Port or starboard side. True=port, False=starboard, null=center.</summary>
        public bool IsPort;

        /// <summary>Current tension 0-1. Affects sail shape and crew fatigue.</summary>
        [Range(0f, 1f)]
        public float Tension;

        public RiggingLine(string name, LineType type, Vector3 pos, int sailIdx, bool port)
        {
            Name = name;
            Type = type;
            StationPosition = pos;
            SailIndex = sailIdx;
            IsPort = port;
            Tension = 0f;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // MAST
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// A single mast with its standing rigging, sails, yards, and crow's nest.
    ///
    /// Standing rigging (fixed):
    ///   Shrouds — wire ropes from chainplates to masthead, port+starboard
    ///   Backstay — aft support from masthead to stern
    ///   Forestay — forward support (jibs fly from this)
    ///   Ratlines — horizontal ropes across shrouds forming a rope ladder
    ///   Crosstrees — horizontal platforms where shrouds spread
    ///
    /// Yards (horizontal spars, square rig):
    ///   Lower yard → course sail
    ///   Top yard → topsail
    ///   Topgallant yard → topgallant
    ///   Each yard can be braced (rotated) by crew on deck.
    /// </summary>
    [System.Serializable]
    public struct MastRig
    {
        public string Name;

        /// <summary>Mast height in meters (determines performance, climbing time).</summary>
        public float Height;

        /// <summary>Local position of mast base on deck.</summary>
        public Vector3 BasePosition;

        /// <summary>Has crow's nest / fighting top?</summary>
        public bool HasCrowsNest;

        /// <summary>Height of crow's nest above deck (fraction of total height).</summary>
        public float CrowsNestHeight;

        /// <summary>Has crosstrees / spreaders?</summary>
        public bool HasCrosstrees;

        /// <summary>Number of shroud pairs (port+starboard). More = sturdier mast.</summary>
        public int ShroudPairs;

        /// <summary>Number of yard arms. 0 for pure fore-and-aft rigs.</summary>
        public int YardCount;

        /// <summary>Has ratlines (rope ladders on shrouds for climbing)?</summary>
        public bool HasRatlines;

        public MastRig(string name, float height, Vector3 basePos,
            bool crowsNest, int shroudPairs, int yardCount)
        {
            Name = name;
            Height = height;
            BasePosition = basePos;
            HasCrowsNest = crowsNest;
            CrowsNestHeight = 0.85f;
            HasCrosstrees = height > 5f;
            ShroudPairs = shroudPairs;
            YardCount = yardCount;
            HasRatlines = shroudPairs >= 2;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // FULL SAIL RIG — complete rigging plan for a ship
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Complete sail rig for a vessel — all masts, all sails, all lines.
    /// Built from ShipManifest blueprint at startup.
    ///
    /// This is the high-fidelity model that ShipSail.cs uses to
    /// compute aggregate sail performance from individual components.
    /// Crew interact with individual sails and lines — not a single "trim" knob.
    /// </summary>
    [System.Serializable]
    public class SailRigPlan
    {
        public MastRig[] Masts;
        public IndividualSail[] Sails;
        public RiggingLine[] Lines;

        /// <summary>Total full sail area across all sails.</summary>
        public float TotalFullArea
        {
            get
            {
                float total = 0f;
                if (Sails != null)
                    for (int i = 0; i < Sails.Length; i++)
                        total += Sails[i].FullArea;
                return total;
            }
        }

        /// <summary>Total effective area right now (accounting for state, reef, damage).</summary>
        public float TotalEffectiveArea
        {
            get
            {
                float total = 0f;
                if (Sails != null)
                    for (int i = 0; i < Sails.Length; i++)
                        total += Sails[i].EffectiveArea;
                return total;
            }
        }

        /// <summary>Fraction of rigging that is set (not furled). 0-1.</summary>
        public float SetFraction
        {
            get
            {
                if (Sails == null || Sails.Length == 0) return 0f;
                int set = 0;
                for (int i = 0; i < Sails.Length; i++)
                    if (Sails[i].State == SailState.Set || Sails[i].State == SailState.Reefed)
                        set++;
                return (float)set / Sails.Length;
            }
        }

        /// <summary>Any sail currently torn?</summary>
        public bool HasTornSail
        {
            get
            {
                if (Sails == null) return false;
                for (int i = 0; i < Sails.Length; i++)
                    if (Sails[i].State == SailState.Torn) return true;
                return false;
            }
        }

        /// <summary>Average sheet trim across all set sails.</summary>
        public float AverageSheetTrim
        {
            get
            {
                if (Sails == null) return 0.5f;
                float sum = 0f;
                int count = 0;
                for (int i = 0; i < Sails.Length; i++)
                {
                    if (Sails[i].State == SailState.Set || Sails[i].State == SailState.Reefed)
                    {
                        sum += Sails[i].SheetTrim;
                        count++;
                    }
                }
                return count > 0 ? sum / count : 0.5f;
            }
        }

        // =============================================================
        // FACTORY — Build rig plans per ship class
        // =============================================================

        public static SailRigPlan ForSloop()
        {
            var plan = new SailRigPlan();
            plan.Masts = new[]
            {
                new MastRig("MainMast", 6f, new Vector3(0f, 0f, 0.3f),
                    crowsNest: false, shroudPairs: 2, yardCount: 0),
            };
            plan.Sails = new[]
            {
                new IndividualSail("Mainsail", SailType.ForeAndAft, 14f, 0, 0),
                new IndividualSail("Jib",      SailType.Jib,         6f, 0, 0),
            };
            plan.Lines = BuildLines(plan);
            return plan;
        }

        public static SailRigPlan ForSchooner()
        {
            var plan = new SailRigPlan();
            plan.Masts = new[]
            {
                new MastRig("ForeMast", 7f, new Vector3(0f, 0f, 1.5f),
                    crowsNest: false, shroudPairs: 3, yardCount: 0),
                new MastRig("MainMast", 8f, new Vector3(0f, 0f, -0.5f),
                    crowsNest: true, shroudPairs: 3, yardCount: 0),
            };
            plan.Sails = new[]
            {
                new IndividualSail("Fore Sail",  SailType.ForeAndAft, 12f, 0, 0),
                new IndividualSail("Main Sail",  SailType.ForeAndAft, 15f, 1, 0),
                new IndividualSail("Fore Staysail", SailType.Staysail, 4f, 0, 1),
                new IndividualSail("Jib",        SailType.Jib,         4f, 0, 0),
            };
            plan.Lines = BuildLines(plan);
            return plan;
        }

        public static SailRigPlan ForBrigantine()
        {
            var plan = new SailRigPlan();
            plan.Masts = new[]
            {
                new MastRig("ForeMast", 8f, new Vector3(0f, 0f, 1.8f),
                    crowsNest: true, shroudPairs: 4, yardCount: 2),
                new MastRig("MainMast", 9f, new Vector3(0f, 0f, -0.5f),
                    crowsNest: true, shroudPairs: 4, yardCount: 0),
            };
            plan.Sails = new[]
            {
                new IndividualSail("Fore Course",   SailType.Square,     12f, 0, 0),
                new IndividualSail("Fore Topsail",  SailType.Square,      8f, 0, 1),
                new IndividualSail("Main Sail",     SailType.ForeAndAft, 16f, 1, 0),
                new IndividualSail("Jib",           SailType.Jib,         5f, 0, 0),
                new IndividualSail("Main Staysail", SailType.Staysail,    4f, 1, 1),
            };
            plan.Lines = BuildLines(plan);
            return plan;
        }

        public static SailRigPlan ForFrigate()
        {
            var plan = new SailRigPlan();
            plan.Masts = new[]
            {
                new MastRig("ForeMast",   10f, new Vector3(0f, 0f, 2.5f),
                    crowsNest: true, shroudPairs: 5, yardCount: 3),
                new MastRig("MainMast",   11f, new Vector3(0f, 0f, 0f),
                    crowsNest: true, shroudPairs: 5, yardCount: 3),
                new MastRig("MizzenMast",  8f, new Vector3(0f, 0f, -2.5f),
                    crowsNest: true, shroudPairs: 4, yardCount: 1),
            };
            plan.Sails = new[]
            {
                // Foremast
                new IndividualSail("Fore Course",      SailType.Square,      14f, 0, 0),
                new IndividualSail("Fore Topsail",     SailType.Square,      10f, 0, 1),
                new IndividualSail("Fore Topgallant",  SailType.Square,       6f, 0, 2),
                // Mainmast
                new IndividualSail("Main Course",      SailType.Square,      16f, 1, 0),
                new IndividualSail("Main Topsail",     SailType.Square,      12f, 1, 1),
                new IndividualSail("Main Topgallant",  SailType.Square,       7f, 1, 2),
                // Mizzen
                new IndividualSail("Spanker",          SailType.Spanker,      8f, 2, 0),
                // Headsails
                new IndividualSail("Flying Jib",       SailType.Jib,          4f, 0, 0),
                new IndividualSail("Jib",              SailType.Jib,          5f, 0, 0),
                new IndividualSail("Fore Staysail",    SailType.Staysail,     4f, 0, 1),
                new IndividualSail("Main Staysail",    SailType.Staysail,     4f, 1, 1),
            };
            plan.Lines = BuildLines(plan);
            return plan;
        }

        public static SailRigPlan ForGalleon()
        {
            var plan = new SailRigPlan();
            plan.Masts = new[]
            {
                new MastRig("ForeMast",   11f, new Vector3(0f, 0f, 2.5f),
                    crowsNest: true, shroudPairs: 5, yardCount: 3),
                new MastRig("MainMast",   12f, new Vector3(0f, 0f, 0f),
                    crowsNest: true, shroudPairs: 6, yardCount: 3),
                new MastRig("MizzenMast",  9f, new Vector3(0f, 0f, -3.0f),
                    crowsNest: true, shroudPairs: 4, yardCount: 1),
            };
            plan.Sails = new[]
            {
                // Foremast
                new IndividualSail("Fore Course",        SailType.Square,      16f, 0, 0),
                new IndividualSail("Fore Topsail",       SailType.Square,      12f, 0, 1),
                new IndividualSail("Fore Topgallant",    SailType.Square,       8f, 0, 2),
                // Mainmast
                new IndividualSail("Main Course",        SailType.Square,      20f, 1, 0),
                new IndividualSail("Main Topsail",       SailType.Square,      14f, 1, 1),
                new IndividualSail("Main Topgallant",    SailType.Square,       9f, 1, 2),
                // Mizzen
                new IndividualSail("Spanker",            SailType.Spanker,     10f, 2, 0),
                new IndividualSail("Mizzen Topsail",     SailType.Square,       6f, 2, 1),
                // Headsails
                new IndividualSail("Flying Jib",         SailType.Jib,          5f, 0, 0),
                new IndividualSail("Jib",                SailType.Jib,          6f, 0, 0),
                new IndividualSail("Fore Staysail",      SailType.Staysail,     5f, 0, 1),
                new IndividualSail("Main Staysail",      SailType.Staysail,     5f, 1, 1),
            };
            plan.Lines = BuildLines(plan);
            return plan;
        }

        public static SailRigPlan ForFlagship()
        {
            // Flagship uses frigate rig with upgraded condition
            var plan = ForFrigate();
            for (int i = 0; i < plan.Sails.Length; i++)
                plan.Sails[i].Condition = 1f;
            return plan;
        }

        public static SailRigPlan ForClass(Scripting.ShipClass cls)
        {
            switch (cls)
            {
                case Scripting.ShipClass.Sloop:      return ForSloop();
                case Scripting.ShipClass.Schooner:   return ForSchooner();
                case Scripting.ShipClass.Brigantine:  return ForBrigantine();
                case Scripting.ShipClass.Frigate:     return ForFrigate();
                case Scripting.ShipClass.Galleon:     return ForGalleon();
                case Scripting.ShipClass.Flagship:    return ForFlagship();
                default:                              return ForSloop();
            }
        }

        // =============================================================
        // LINE AUTO-GENERATION
        // =============================================================

        /// <summary>
        /// Automatically generate rigging lines from masts and sails.
        /// Each sail gets sheets (port+starboard), halyard, and type-specific lines.
        /// </summary>
        static RiggingLine[] BuildLines(SailRigPlan plan)
        {
            var lines = new System.Collections.Generic.List<RiggingLine>();
            float hullHalfBeam = 1.2f; // approximate — real value from manifest

            for (int s = 0; s < plan.Sails.Length; s++)
            {
                var sail = plan.Sails[s];
                var mast = plan.Masts[sail.MastIndex];
                float deckY = 0.3f;
                float mastZ = mast.BasePosition.z;

                // Sheet — port
                lines.Add(new RiggingLine(
                    $"{sail.Name} Sheet (Port)",
                    LineType.Sheet,
                    new Vector3(-hullHalfBeam, deckY, mastZ - 0.5f),
                    s, port: true));

                // Sheet — starboard
                lines.Add(new RiggingLine(
                    $"{sail.Name} Sheet (Stbd)",
                    LineType.Sheet,
                    new Vector3(hullHalfBeam, deckY, mastZ - 0.5f),
                    s, port: false));

                // Halyard — at mast base
                lines.Add(new RiggingLine(
                    $"{sail.Name} Halyard",
                    LineType.Halyard,
                    new Vector3(0f, deckY, mastZ),
                    s, port: false));

                // Square rig extras
                if (sail.Type == SailType.Square)
                {
                    // Braces — port & starboard (rotate yard)
                    lines.Add(new RiggingLine(
                        $"{sail.Name} Brace (Port)",
                        LineType.Brace,
                        new Vector3(-hullHalfBeam, deckY, mastZ + 0.3f),
                        s, port: true));
                    lines.Add(new RiggingLine(
                        $"{sail.Name} Brace (Stbd)",
                        LineType.Brace,
                        new Vector3(hullHalfBeam, deckY, mastZ + 0.3f),
                        s, port: false));

                    // Clew lines (for furling)
                    lines.Add(new RiggingLine(
                        $"{sail.Name} Clew",
                        LineType.Clew,
                        new Vector3(0f, deckY, mastZ),
                        s, port: false));
                }

                // Reef lines for larger sails
                if (sail.FullArea >= 10f)
                {
                    lines.Add(new RiggingLine(
                        $"{sail.Name} Reef Tackle",
                        LineType.Reef,
                        new Vector3(0f, deckY, mastZ - 0.3f),
                        s, port: false));
                }
            }

            // Boom vang for fore-and-aft sails with booms
            for (int s = 0; s < plan.Sails.Length; s++)
            {
                if (plan.Sails[s].Type == SailType.ForeAndAft ||
                    plan.Sails[s].Type == SailType.Spanker)
                {
                    var mast = plan.Masts[plan.Sails[s].MastIndex];
                    lines.Add(new RiggingLine(
                        $"{plan.Sails[s].Name} Vang",
                        LineType.Vang,
                        new Vector3(0f, 0.3f, mast.BasePosition.z),
                        s, port: false));
                }
            }

            return lines.ToArray();
        }
    }
}
