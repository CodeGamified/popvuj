// Copyright SeaRäuber 2025-2026
// MIT License
using UnityEngine;
using System.Collections.Generic;

namespace SeaRauber.Ship
{
    /// <summary>
    /// Ship manifest — defines the physical layout, crew roster, and tier
    /// for each ship class. This is the "data sheet" that the bootstrap
    /// reads when instantiating a ship.
    /// 
    /// Think of it as: what you'd see nailed to the wall of an admiralty office.
    /// 
    /// Each ship class defines:
    ///   - Hull dimensions (length, beam, draft)
    ///   - Mast count + positions
    ///   - Subcomponent layout (deck sections, bays, rooms)
    ///   - Crew roster (role → count, skill)
    ///   - Chart room tier
    ///   - Performance envelope
    /// </summary>
    public static class ShipManifest
    {
        // ═══════════════════════════════════════════════════════════════
        // CREW ROSTER
        // ═══════════════════════════════════════════════════════════════

        /// <summary>A single crew assignment: role, count, base skill.</summary>
        public struct CrewAssignment
        {
            public Scripting.CrewRole Role;
            public int Count;
            public float Skill; // 0-1

            public CrewAssignment(Scripting.CrewRole role, int count, float skill)
            {
                Role = role;
                Count = count;
                Skill = Mathf.Clamp01(skill);
            }
        }

        /// <summary>
        /// Subcomponent definition for ship geometry.
        /// Each becomes a primitive GameObject with a collider for selection.
        /// </summary>
        public struct SubcomponentDef
        {
            public string Name;
            public PrimitiveType Shape;
            public Vector3 LocalPosition;
            public Vector3 LocalScale;
            public Color Color;

            public SubcomponentDef(string name, PrimitiveType shape, Vector3 pos, Vector3 scale, Color color)
            {
                Name = name;
                Shape = shape;
                LocalPosition = pos;
                LocalScale = scale;
                Color = color;
            }
        }

        /// <summary>
        /// Full ship blueprint for a given class.
        /// </summary>
        public struct Blueprint
        {
            public Scripting.ShipClass Class;
            public Scripting.ChartRoomTier Tier;
            public int MastCount;
            public float MaxSpeed;
            public float SailArea;
            public float DeadZone;
            public float HullBow;
            public float HullStern;
            public float HullBeam;
            public CrewAssignment[] Crew;
            public SubcomponentDef[] Subcomponents;
            /// <summary>Hull rib definitions. When present, hull mesh is generated from ribs.</summary>
            public RibDef[] Ribs;
        }

        // ═══════════════════════════════════════════════════════════════
        // PALETTE
        // ═══════════════════════════════════════════════════════════════

        private static readonly Color DARK_WOOD   = new Color(0.35f, 0.22f, 0.12f);
        private static readonly Color LIGHT_WOOD  = new Color(0.50f, 0.35f, 0.20f);
        private static readonly Color DECK_WOOD   = new Color(0.55f, 0.40f, 0.25f);
        private static readonly Color IRON        = new Color(0.30f, 0.30f, 0.32f);
        private static readonly Color BRASS       = new Color(0.72f, 0.55f, 0.20f);
        private static readonly Color CANVAS      = new Color(0.90f, 0.88f, 0.80f);
        private static readonly Color RED_SAIL    = new Color(0.85f, 0.15f, 0.15f);
        private static readonly Color CHART_ROOM  = new Color(0.45f, 0.35f, 0.25f);
        private static readonly Color GUN_METAL   = new Color(0.25f, 0.25f, 0.28f);
        private static readonly Color CARGO       = new Color(0.40f, 0.32f, 0.18f);
        private static readonly Color ROPE        = new Color(0.55f, 0.45f, 0.30f);

        // ═══════════════════════════════════════════════════════════════
        // BLUEPRINTS
        // ═══════════════════════════════════════════════════════════════

        public static Blueprint GetBlueprint(Scripting.ShipClass shipClass)
        {
            switch (shipClass)
            {
                case Scripting.ShipClass.Sloop:      return Sloop();
                case Scripting.ShipClass.Schooner:   return Schooner();
                case Scripting.ShipClass.Brigantine:  return Brigantine();
                case Scripting.ShipClass.Frigate:     return Frigate();
                case Scripting.ShipClass.Galleon:     return Galleon();
                case Scripting.ShipClass.Flagship:    return FlagshipBlueprint();
                default:                              return Sloop();
            }
        }

        // ─────────────────────────────────────────────────────────────
        // SLOOP — small, fast, 1 mast, minimal crew
        // Tier: CompassAndLog (IF/THEN only, ~5 lines)
        // ─────────────────────────────────────────────────────────────
        private static Blueprint Sloop()
        {
            return new Blueprint
            {
                Class = Scripting.ShipClass.Sloop,
                Tier = Scripting.ChartRoomTier.CompassAndLog,
                MastCount = 1,
                MaxSpeed = 5.5f,
                SailArea = 20f,
                DeadZone = 45f,
                HullBow = 3f,
                HullStern = 2.5f,
                HullBeam = 1.2f,
                Crew = new[]
                {
                    new CrewAssignment(Scripting.CrewRole.Helmsman,  1, 0.6f),
                    new CrewAssignment(Scripting.CrewRole.Rigger,    1, 0.5f),
                    new CrewAssignment(Scripting.CrewRole.Navigator, 1, 0.4f), // captain doubles
                    new CrewAssignment(Scripting.CrewRole.Lookout,   1, 0.5f),
                    new CrewAssignment(Scripting.CrewRole.Cook,      1, 0.4f),
                },
                Subcomponents = new[]
                {
                    // Hull
                    new SubcomponentDef("Hull",       PrimitiveType.Cube,     new Vector3(0f, -0.2f, 0f),      new Vector3(1.8f, 0.7f, 5.5f), DARK_WOOD),
                    // Deck
                    new SubcomponentDef("Deck",       PrimitiveType.Cube,     new Vector3(0f, 0.15f, 0f),      new Vector3(1.6f, 0.05f, 4.5f), DECK_WOOD),
                    // Bow (pointed)
                    new SubcomponentDef("Bowsprit",   PrimitiveType.Cylinder, new Vector3(0f, 0.3f, 3.5f),     new Vector3(0.06f, 1.2f, 0.06f), LIGHT_WOOD),
                    // Mast
                    new SubcomponentDef("MainMast",   PrimitiveType.Cylinder, new Vector3(0f, 3.0f, 0.3f),     new Vector3(0.12f, 3.0f, 0.12f), LIGHT_WOOD),
                    // Boom
                    new SubcomponentDef("Boom",       PrimitiveType.Cylinder, new Vector3(0f, 1.5f, -0.5f),    new Vector3(0.06f, 2.0f, 0.06f), LIGHT_WOOD),
                    // Sail
                    new SubcomponentDef("MainSail",   PrimitiveType.Cube,     new Vector3(0f, 3.2f, 0.3f),     new Vector3(2.5f, 2.5f, 0.04f), CANVAS),
                    // Jib sail on forestay
                    new SubcomponentDef("Jib",        PrimitiveType.Cube,     new Vector3(0f, 2.0f, 2.0f),     new Vector3(1.2f, 1.8f, 0.03f), CANVAS),
                    // Standing rigging — shrouds (port & starboard)
                    new SubcomponentDef("Shroud_P",   PrimitiveType.Cylinder, new Vector3(-0.8f, 1.8f, 0.3f),  new Vector3(0.02f, 2.8f, 0.02f), ROPE),
                    new SubcomponentDef("Shroud_S",   PrimitiveType.Cylinder, new Vector3(0.8f, 1.8f, 0.3f),   new Vector3(0.02f, 2.8f, 0.02f), ROPE),
                    // Ratlines on shrouds (rope ladder for climbing)
                    new SubcomponentDef("Ratline_P",  PrimitiveType.Cube,     new Vector3(-0.8f, 1.8f, 0.3f),  new Vector3(0.01f, 0.01f, 0.6f), ROPE),
                    new SubcomponentDef("Ratline_S",  PrimitiveType.Cube,     new Vector3(0.8f, 1.8f, 0.3f),   new Vector3(0.01f, 0.01f, 0.6f), ROPE),
                    // Forestay (bow to masthead)
                    new SubcomponentDef("Forestay",   PrimitiveType.Cylinder, new Vector3(0f, 2.5f, 1.9f),     new Vector3(0.02f, 2.5f, 0.02f), ROPE),
                    // Backstay (stern to masthead)
                    new SubcomponentDef("Backstay",   PrimitiveType.Cylinder, new Vector3(0f, 2.0f, -1.0f),    new Vector3(0.02f, 2.2f, 0.02f), ROPE),
                    // Boom vang (controls boom angle)
                    new SubcomponentDef("BoomVang",   PrimitiveType.Cylinder, new Vector3(0f, 0.8f, -0.2f),    new Vector3(0.02f, 0.8f, 0.02f), ROPE),
                    // Sheet cleats at rail (port & starboard)
                    new SubcomponentDef("SheetCleat_P", PrimitiveType.Cube,   new Vector3(-0.8f, 0.2f, -0.5f), new Vector3(0.08f, 0.04f, 0.15f), IRON),
                    new SubcomponentDef("SheetCleat_S", PrimitiveType.Cube,   new Vector3(0.8f, 0.2f, -0.5f),  new Vector3(0.08f, 0.04f, 0.15f), IRON),
                    // Halyard bitt at mast base
                    new SubcomponentDef("HalyardBitt", PrimitiveType.Cylinder, new Vector3(0f, 0.25f, 0.3f),   new Vector3(0.06f, 0.12f, 0.06f), IRON),
                    // Tiller (helm)
                    new SubcomponentDef("Tiller",     PrimitiveType.Cylinder, new Vector3(0f, 0.3f, -2.0f),    new Vector3(0.04f, 0.5f, 0.04f), LIGHT_WOOD),
                    // Compass/ChartRoom (tiny box at stern)
                    new SubcomponentDef("ChartRoom",  PrimitiveType.Cube,     new Vector3(0f, 0.4f, -1.8f),    new Vector3(0.5f, 0.3f, 0.5f), CHART_ROOM),
                },
                // Sharp V hull — cuts through waves, 2 floors
                Ribs = new[]
                {
                    new RibDef(-2.5f, 0.4f,  0.6f, RibCurveType.SharpV,
                        new FloorDef(FloorFunction.CargoBay)),
                    new RibDef(-1.0f, 1.8f,  1.0f, RibCurveType.SharpV,
                        new FloorDef(FloorFunction.Hammocks),
                        new FloorDef(FloorFunction.OpenDeck)),
                    new RibDef( 0.0f, 2.4f,  1.2f, RibCurveType.SharpV,
                        new FloorDef(FloorFunction.Kitchen),
                        new FloorDef(FloorFunction.OpenDeck)),
                    new RibDef( 1.5f, 2.0f,  1.0f, RibCurveType.SharpV,
                        new FloorDef(FloorFunction.ChartRoom),
                        new FloorDef(FloorFunction.OpenDeck)),
                    new RibDef( 3.0f, 0.3f,  0.5f, RibCurveType.SharpV,
                        new FloorDef(FloorFunction.SailLocker)),
                }
            };
        }

        // ─────────────────────────────────────────────────────────────
        // SCHOONER — medium, 2 masts, small crew
        // Tier: ChartTable (+ while loops)
        // ─────────────────────────────────────────────────────────────
        private static Blueprint Schooner()
        {
            return new Blueprint
            {
                Class = Scripting.ShipClass.Schooner,
                Tier = Scripting.ChartRoomTier.ChartTable,
                MastCount = 2,
                MaxSpeed = 5.0f,
                SailArea = 35f,
                DeadZone = 40f,
                HullBow = 3.5f,
                HullStern = 3f,
                HullBeam = 1.4f,
                Crew = new[]
                {
                    new CrewAssignment(Scripting.CrewRole.Helmsman,  1, 0.6f),
                    new CrewAssignment(Scripting.CrewRole.Rigger,    2, 0.5f),
                    new CrewAssignment(Scripting.CrewRole.Navigator, 1, 0.5f),
                    new CrewAssignment(Scripting.CrewRole.Lookout,   1, 0.5f),
                    new CrewAssignment(Scripting.CrewRole.Bosun,     1, 0.5f),
                    new CrewAssignment(Scripting.CrewRole.Cook,      1, 0.5f),
                    new CrewAssignment(Scripting.CrewRole.Carpenter, 1, 0.4f),
                },
                Subcomponents = new[]
                {
                    new SubcomponentDef("Hull",        PrimitiveType.Cube,     new Vector3(0f, -0.2f, 0f),      new Vector3(2.0f, 0.8f, 6.5f), DARK_WOOD),
                    new SubcomponentDef("Deck",        PrimitiveType.Cube,     new Vector3(0f, 0.2f, 0f),       new Vector3(1.8f, 0.05f, 5.5f), DECK_WOOD),
                    // Forecastle (raised bow section)
                    new SubcomponentDef("Forecastle",  PrimitiveType.Cube,     new Vector3(0f, 0.5f, 2.5f),     new Vector3(1.6f, 0.3f, 1.2f), DECK_WOOD),
                    // Quarterdeck (raised stern)
                    new SubcomponentDef("Quarterdeck", PrimitiveType.Cube,     new Vector3(0f, 0.5f, -2.2f),    new Vector3(1.6f, 0.3f, 1.5f), DECK_WOOD),
                    // Bowsprit
                    new SubcomponentDef("Bowsprit",    PrimitiveType.Cylinder, new Vector3(0f, 0.4f, 4.2f),     new Vector3(0.06f, 1.5f, 0.06f), LIGHT_WOOD),
                    // Fore mast
                    new SubcomponentDef("ForeMast",    PrimitiveType.Cylinder, new Vector3(0f, 3.5f, 1.5f),     new Vector3(0.12f, 3.5f, 0.12f), LIGHT_WOOD),
                    new SubcomponentDef("ForeSail",    PrimitiveType.Cube,     new Vector3(0f, 3.8f, 1.5f),     new Vector3(2.2f, 2.2f, 0.04f), CANVAS),
                    // Fore staysail (between masts on stay)
                    new SubcomponentDef("ForeStaysail", PrimitiveType.Cube,    new Vector3(0f, 2.5f, 0.5f),     new Vector3(1.0f, 1.5f, 0.03f), CANVAS),
                    // Main mast
                    new SubcomponentDef("MainMast",    PrimitiveType.Cylinder, new Vector3(0f, 4.0f, -0.5f),    new Vector3(0.14f, 4.0f, 0.14f), LIGHT_WOOD),
                    new SubcomponentDef("MainSail",    PrimitiveType.Cube,     new Vector3(0f, 4.2f, -0.5f),    new Vector3(2.8f, 2.8f, 0.04f), CANVAS),
                    // Crow's nest on main mast
                    new SubcomponentDef("CrowsNest",   PrimitiveType.Cylinder, new Vector3(0f, 7.2f, -0.5f),    new Vector3(0.5f, 0.06f, 0.5f), DARK_WOOD),
                    // Jib on bowsprit
                    new SubcomponentDef("Jib",         PrimitiveType.Cube,     new Vector3(0f, 2.2f, 3.0f),     new Vector3(1.4f, 1.8f, 0.03f), CANVAS),
                    // Standing rigging — fore mast shrouds
                    new SubcomponentDef("ForeShroud_P",PrimitiveType.Cylinder, new Vector3(-0.9f, 2.0f, 1.5f),  new Vector3(0.02f, 3.2f, 0.02f), ROPE),
                    new SubcomponentDef("ForeShroud_S",PrimitiveType.Cylinder, new Vector3(0.9f, 2.0f, 1.5f),   new Vector3(0.02f, 3.2f, 0.02f), ROPE),
                    // Standing rigging — main mast shrouds
                    new SubcomponentDef("MainShroud_P",PrimitiveType.Cylinder, new Vector3(-0.9f, 2.5f, -0.5f), new Vector3(0.02f, 3.6f, 0.02f), ROPE),
                    new SubcomponentDef("MainShroud_S",PrimitiveType.Cylinder, new Vector3(0.9f, 2.5f, -0.5f),  new Vector3(0.02f, 3.6f, 0.02f), ROPE),
                    // Ratlines (rope ladder on shrouds)
                    new SubcomponentDef("ForeRatline_P",PrimitiveType.Cube,    new Vector3(-0.9f, 2.0f, 1.5f),  new Vector3(0.01f, 0.01f, 0.7f), ROPE),
                    new SubcomponentDef("ForeRatline_S",PrimitiveType.Cube,    new Vector3(0.9f, 2.0f, 1.5f),   new Vector3(0.01f, 0.01f, 0.7f), ROPE),
                    new SubcomponentDef("MainRatline_P",PrimitiveType.Cube,    new Vector3(-0.9f, 2.5f, -0.5f), new Vector3(0.01f, 0.01f, 0.8f), ROPE),
                    new SubcomponentDef("MainRatline_S",PrimitiveType.Cube,    new Vector3(0.9f, 2.5f, -0.5f),  new Vector3(0.01f, 0.01f, 0.8f), ROPE),
                    // Forestay & backstay
                    new SubcomponentDef("Forestay",    PrimitiveType.Cylinder, new Vector3(0f, 3.0f, 3.0f),     new Vector3(0.02f, 3.5f, 0.02f), ROPE),
                    new SubcomponentDef("Backstay",    PrimitiveType.Cylinder, new Vector3(0f, 2.5f, -1.5f),    new Vector3(0.02f, 3.0f, 0.02f), ROPE),
                    // Boom (main mast)
                    new SubcomponentDef("MainBoom",    PrimitiveType.Cylinder, new Vector3(0f, 2.0f, -1.2f),    new Vector3(0.06f, 2.0f, 0.06f), LIGHT_WOOD),
                    new SubcomponentDef("BoomVang",    PrimitiveType.Cylinder, new Vector3(0f, 1.0f, -0.8f),    new Vector3(0.02f, 1.0f, 0.02f), ROPE),
                    // Sheet cleats at rails
                    new SubcomponentDef("SheetCleat_P",PrimitiveType.Cube,     new Vector3(-0.9f, 0.25f, -0.5f),new Vector3(0.08f, 0.04f, 0.15f), IRON),
                    new SubcomponentDef("SheetCleat_S",PrimitiveType.Cube,     new Vector3(0.9f, 0.25f, -0.5f), new Vector3(0.08f, 0.04f, 0.15f), IRON),
                    // Halyard bitts at mast bases
                    new SubcomponentDef("ForeHalyardBitt",PrimitiveType.Cylinder,new Vector3(0f, 0.28f, 1.5f),  new Vector3(0.06f, 0.12f, 0.06f), IRON),
                    new SubcomponentDef("MainHalyardBitt",PrimitiveType.Cylinder,new Vector3(0f, 0.28f, -0.5f), new Vector3(0.06f, 0.12f, 0.06f), IRON),
                    // Helm wheel
                    new SubcomponentDef("Helm",        PrimitiveType.Cylinder, new Vector3(0f, 0.7f, -2.2f),    new Vector3(0.4f, 0.04f, 0.4f), BRASS),
                    // Chart Room
                    new SubcomponentDef("ChartRoom",   PrimitiveType.Cube,     new Vector3(0f, 0.1f, -2.0f),    new Vector3(0.8f, 0.5f, 0.8f), CHART_ROOM),
                    // Cargo hold hatch
                    new SubcomponentDef("CargoHold",   PrimitiveType.Cube,     new Vector3(0f, 0.22f, 0.5f),    new Vector3(0.8f, 0.06f, 0.8f), CARGO),
                },
                // Round hull — moderate displacement, 2 floors
                Ribs = new[]
                {
                    new RibDef(-3.0f, 0.5f,  0.7f, RibCurveType.Round,
                        new FloorDef(FloorFunction.CargoBay)),
                    new RibDef(-1.5f, 2.2f,  1.2f, RibCurveType.Round,
                        new FloorDef(FloorFunction.Hammocks),
                        new FloorDef(FloorFunction.OpenDeck)),
                    new RibDef( 0.0f, 2.8f,  1.4f, RibCurveType.Round,
                        new FloorDef(FloorFunction.Kitchen),
                        new FloorDef(FloorFunction.OpenDeck)),
                    new RibDef( 1.5f, 2.4f,  1.2f, RibCurveType.Round,
                        new FloorDef(FloorFunction.Workshop),
                        new FloorDef(FloorFunction.OpenDeck)),
                    new RibDef( 3.0f, 1.6f,  1.0f, RibCurveType.Round,
                        new FloorDef(FloorFunction.SailLocker),
                        new FloorDef(FloorFunction.OpenDeck)),
                    new RibDef( 3.5f, 0.4f,  0.5f, RibCurveType.Round,
                        new FloorDef(FloorFunction.None)),
                }
            };
        }

        // ─────────────────────────────────────────────────────────────
        // BRIGANTINE — medium-large, mixed rig
        // Tier: NavigatorsOffice (+ for, subroutines)
        // ─────────────────────────────────────────────────────────────
        private static Blueprint Brigantine()
        {
            return new Blueprint
            {
                Class = Scripting.ShipClass.Brigantine,
                Tier = Scripting.ChartRoomTier.NavigatorsOffice,
                MastCount = 2,
                MaxSpeed = 4.8f,
                SailArea = 45f,
                DeadZone = 38f,
                HullBow = 4f,
                HullStern = 3.5f,
                HullBeam = 1.6f,
                Crew = new[]
                {
                    new CrewAssignment(Scripting.CrewRole.Helmsman,      1, 0.7f),
                    new CrewAssignment(Scripting.CrewRole.Rigger,        3, 0.6f),
                    new CrewAssignment(Scripting.CrewRole.Navigator,     1, 0.6f),
                    new CrewAssignment(Scripting.CrewRole.Lookout,       2, 0.6f),
                    new CrewAssignment(Scripting.CrewRole.Bosun,         1, 0.6f),
                    new CrewAssignment(Scripting.CrewRole.Cook,          1, 0.5f),
                    new CrewAssignment(Scripting.CrewRole.Carpenter,     1, 0.5f),
                    new CrewAssignment(Scripting.CrewRole.Gunner,        2, 0.5f),
                    new CrewAssignment(Scripting.CrewRole.CraneOperator, 1, 0.4f),
                },
                Subcomponents = new[]
                {
                    new SubcomponentDef("Hull",           PrimitiveType.Cube,     new Vector3(0f, -0.25f, 0f),     new Vector3(2.4f, 0.9f, 8f), DARK_WOOD),
                    new SubcomponentDef("Deck",           PrimitiveType.Cube,     new Vector3(0f, 0.2f, 0f),       new Vector3(2.2f, 0.05f, 7f), DECK_WOOD),
                    new SubcomponentDef("Forecastle",     PrimitiveType.Cube,     new Vector3(0f, 0.55f, 3.0f),    new Vector3(2.0f, 0.35f, 1.5f), DECK_WOOD),
                    new SubcomponentDef("Quarterdeck",    PrimitiveType.Cube,     new Vector3(0f, 0.55f, -2.8f),   new Vector3(2.0f, 0.35f, 1.8f), DECK_WOOD),
                    // Bowsprit
                    new SubcomponentDef("Bowsprit",       PrimitiveType.Cylinder, new Vector3(0f, 0.4f, 5.0f),     new Vector3(0.07f, 1.8f, 0.07f), LIGHT_WOOD),
                    // Fore mast (square-rigged)
                    new SubcomponentDef("ForeMast",       PrimitiveType.Cylinder, new Vector3(0f, 4.0f, 1.8f),     new Vector3(0.14f, 4.0f, 0.14f), LIGHT_WOOD),
                    new SubcomponentDef("ForeSail",       PrimitiveType.Cube,     new Vector3(0f, 4.5f, 1.8f),     new Vector3(2.8f, 2.5f, 0.04f), CANVAS),
                    // Fore topsail (upper square sail)
                    new SubcomponentDef("ForeTopsail",    PrimitiveType.Cube,     new Vector3(0f, 6.5f, 1.8f),     new Vector3(2.0f, 1.5f, 0.03f), CANVAS),
                    // Fore yards (lower + upper)
                    new SubcomponentDef("ForeYardLower",  PrimitiveType.Cylinder, new Vector3(0f, 3.5f, 1.8f),     new Vector3(0.06f, 1.5f, 0.06f), LIGHT_WOOD),
                    new SubcomponentDef("ForeYardUpper",  PrimitiveType.Cylinder, new Vector3(0f, 5.5f, 1.8f),     new Vector3(0.05f, 1.1f, 0.05f), LIGHT_WOOD),
                    // Crow's nest on fore mast
                    new SubcomponentDef("ForeCrowsNest",  PrimitiveType.Cylinder, new Vector3(0f, 7.2f, 1.8f),     new Vector3(0.5f, 0.06f, 0.5f), DARK_WOOD),
                    // Main mast (fore-and-aft with gaff)
                    new SubcomponentDef("MainMast",       PrimitiveType.Cylinder, new Vector3(0f, 4.5f, -0.5f),    new Vector3(0.16f, 4.5f, 0.16f), LIGHT_WOOD),
                    new SubcomponentDef("MainSail",       PrimitiveType.Cube,     new Vector3(0f, 4.8f, -0.5f),    new Vector3(3.2f, 3.0f, 0.04f), CANVAS),
                    // Main staysail between masts
                    new SubcomponentDef("MainStaysail",   PrimitiveType.Cube,     new Vector3(0f, 3.0f, 0.6f),     new Vector3(1.2f, 1.5f, 0.03f), CANVAS),
                    // Crow's nest on main mast
                    new SubcomponentDef("MainCrowsNest",  PrimitiveType.Cylinder, new Vector3(0f, 8.0f, -0.5f),    new Vector3(0.55f, 0.06f, 0.55f), DARK_WOOD),
                    // Main boom
                    new SubcomponentDef("MainBoom",       PrimitiveType.Cylinder, new Vector3(0f, 2.2f, -1.5f),    new Vector3(0.06f, 2.2f, 0.06f), LIGHT_WOOD),
                    // Jib on bowsprit
                    new SubcomponentDef("Jib",            PrimitiveType.Cube,     new Vector3(0f, 2.5f, 3.5f),     new Vector3(1.6f, 2.0f, 0.03f), CANVAS),
                    // Standing rigging — fore mast shrouds + ratlines
                    new SubcomponentDef("ForeShroud_P1",  PrimitiveType.Cylinder, new Vector3(-1.0f, 2.2f, 1.8f),  new Vector3(0.02f, 3.6f, 0.02f), ROPE),
                    new SubcomponentDef("ForeShroud_P2",  PrimitiveType.Cylinder, new Vector3(-1.0f, 2.2f, 2.3f),  new Vector3(0.02f, 3.6f, 0.02f), ROPE),
                    new SubcomponentDef("ForeShroud_S1",  PrimitiveType.Cylinder, new Vector3(1.0f, 2.2f, 1.8f),   new Vector3(0.02f, 3.6f, 0.02f), ROPE),
                    new SubcomponentDef("ForeShroud_S2",  PrimitiveType.Cylinder, new Vector3(1.0f, 2.2f, 2.3f),   new Vector3(0.02f, 3.6f, 0.02f), ROPE),
                    new SubcomponentDef("ForeRatline_P",  PrimitiveType.Cube,     new Vector3(-1.0f, 2.2f, 2.0f),  new Vector3(0.01f, 0.01f, 0.8f), ROPE),
                    new SubcomponentDef("ForeRatline_S",  PrimitiveType.Cube,     new Vector3(1.0f, 2.2f, 2.0f),   new Vector3(0.01f, 0.01f, 0.8f), ROPE),
                    // Standing rigging — main mast shrouds + ratlines
                    new SubcomponentDef("MainShroud_P1",  PrimitiveType.Cylinder, new Vector3(-1.0f, 2.8f, -0.5f), new Vector3(0.02f, 4.0f, 0.02f), ROPE),
                    new SubcomponentDef("MainShroud_P2",  PrimitiveType.Cylinder, new Vector3(-1.0f, 2.8f, 0.0f),  new Vector3(0.02f, 4.0f, 0.02f), ROPE),
                    new SubcomponentDef("MainShroud_S1",  PrimitiveType.Cylinder, new Vector3(1.0f, 2.8f, -0.5f),  new Vector3(0.02f, 4.0f, 0.02f), ROPE),
                    new SubcomponentDef("MainShroud_S2",  PrimitiveType.Cylinder, new Vector3(1.0f, 2.8f, 0.0f),   new Vector3(0.02f, 4.0f, 0.02f), ROPE),
                    new SubcomponentDef("MainRatline_P",  PrimitiveType.Cube,     new Vector3(-1.0f, 2.8f, -0.25f),new Vector3(0.01f, 0.01f, 0.9f), ROPE),
                    new SubcomponentDef("MainRatline_S",  PrimitiveType.Cube,     new Vector3(1.0f, 2.8f, -0.25f), new Vector3(0.01f, 0.01f, 0.9f), ROPE),
                    // Forestay + backstay
                    new SubcomponentDef("Forestay",       PrimitiveType.Cylinder, new Vector3(0f, 3.5f, 3.5f),     new Vector3(0.02f, 4.0f, 0.02f), ROPE),
                    new SubcomponentDef("Backstay",       PrimitiveType.Cylinder, new Vector3(0f, 3.0f, -2.0f),    new Vector3(0.02f, 3.5f, 0.02f), ROPE),
                    // Boom vang
                    new SubcomponentDef("BoomVang",       PrimitiveType.Cylinder, new Vector3(0f, 1.2f, -1.0f),    new Vector3(0.02f, 1.2f, 0.02f), ROPE),
                    // Fore brace lines at hull rail
                    new SubcomponentDef("ForeBrace_P",    PrimitiveType.Cylinder, new Vector3(-1.1f, 0.3f, 1.8f),  new Vector3(0.02f, 0.6f, 0.02f), ROPE),
                    new SubcomponentDef("ForeBrace_S",    PrimitiveType.Cylinder, new Vector3(1.1f, 0.3f, 1.8f),   new Vector3(0.02f, 0.6f, 0.02f), ROPE),
                    // Sheet cleats at deck edge
                    new SubcomponentDef("SheetCleat_P",   PrimitiveType.Cube,     new Vector3(-1.1f, 0.25f, -1.0f),new Vector3(0.08f, 0.04f, 0.15f), IRON),
                    new SubcomponentDef("SheetCleat_S",   PrimitiveType.Cube,     new Vector3(1.1f, 0.25f, -1.0f), new Vector3(0.08f, 0.04f, 0.15f), IRON),
                    // Halyard bitts at mast bases
                    new SubcomponentDef("ForeHalyardBitt",PrimitiveType.Cylinder, new Vector3(0f, 0.28f, 1.8f),    new Vector3(0.06f, 0.12f, 0.06f), IRON),
                    new SubcomponentDef("MainHalyardBitt",PrimitiveType.Cylinder, new Vector3(0f, 0.28f, -0.5f),   new Vector3(0.06f, 0.12f, 0.06f), IRON),
                    // Crosstrees (horizontal spreaders on masts)
                    new SubcomponentDef("ForeCrosstrees", PrimitiveType.Cylinder, new Vector3(0f, 5.8f, 1.8f),     new Vector3(0.04f, 0.6f, 0.04f), LIGHT_WOOD),
                    new SubcomponentDef("MainCrosstrees", PrimitiveType.Cylinder, new Vector3(0f, 6.5f, -0.5f),    new Vector3(0.04f, 0.7f, 0.04f), LIGHT_WOOD),
                    // Helm
                    new SubcomponentDef("Helm",           PrimitiveType.Cylinder, new Vector3(0f, 0.8f, -2.8f),    new Vector3(0.5f, 0.04f, 0.5f), BRASS),
                    // Chart Room (below quarterdeck)
                    new SubcomponentDef("ChartRoom",      PrimitiveType.Cube,     new Vector3(0f, 0.1f, -2.5f),    new Vector3(1.2f, 0.6f, 1.0f), CHART_ROOM),
                    // Gun ports (port & starboard)
                    new SubcomponentDef("GunPortPort",    PrimitiveType.Cube,     new Vector3(-1.25f, -0.1f, 0f),  new Vector3(0.05f, 0.2f, 0.3f), GUN_METAL),
                    new SubcomponentDef("GunPortStbd",    PrimitiveType.Cube,     new Vector3(1.25f, -0.1f, 0f),   new Vector3(0.05f, 0.2f, 0.3f), GUN_METAL),
                    // Cargo hatch
                    new SubcomponentDef("CargoHold",      PrimitiveType.Cube,     new Vector3(0f, 0.22f, 0.8f),    new Vector3(1.0f, 0.06f, 1.0f), CARGO),
                    // Crane mount
                    new SubcomponentDef("CraneMast",      PrimitiveType.Cylinder, new Vector3(0.8f, 1.2f, 0.8f),   new Vector3(0.08f, 1.2f, 0.08f), IRON),
                },
                // Tumblehome hull — warship profile, 3 floors
                Ribs = new[]
                {
                    new RibDef(-3.5f, 0.6f,  0.8f, RibCurveType.Tumblehome,
                        new FloorDef(FloorFunction.CaptainCabin)),
                    new RibDef(-2.0f, 2.4f,  1.8f, RibCurveType.Tumblehome,
                        new FloorDef(FloorFunction.Magazine),
                        new FloorDef(FloorFunction.Hammocks),
                        new FloorDef(FloorFunction.OpenDeck)),
                    new RibDef(-0.5f, 3.2f,  2.2f, RibCurveType.Tumblehome,
                        new FloorDef(FloorFunction.CargoBay),
                        new FloorDef(FloorFunction.Cannons),
                        new FloorDef(FloorFunction.OpenDeck)),
                    new RibDef( 1.0f, 3.0f,  2.0f, RibCurveType.Tumblehome,
                        new FloorDef(FloorFunction.Kitchen),
                        new FloorDef(FloorFunction.Cannons),
                        new FloorDef(FloorFunction.OpenDeck)),
                    new RibDef( 2.5f, 2.4f,  1.6f, RibCurveType.Tumblehome,
                        new FloorDef(FloorFunction.Workshop),
                        new FloorDef(FloorFunction.OpenDeck)),
                    new RibDef( 4.0f, 0.5f,  0.6f, RibCurveType.Round,
                        new FloorDef(FloorFunction.SailLocker)),
                }
            };
        }

        // ─────────────────────────────────────────────────────────────
        // FRIGATE — large warship, fast, well-armed
        // Tier: WarRoom (+ functions, state machines, combat)
        // ─────────────────────────────────────────────────────────────
        private static Blueprint Frigate()
        {
            return new Blueprint
            {
                Class = Scripting.ShipClass.Frigate,
                Tier = Scripting.ChartRoomTier.WarRoom,
                MastCount = 3,
                MaxSpeed = 4.5f,
                SailArea = 65f,
                DeadZone = 35f,
                HullBow = 5f,
                HullStern = 4f,
                HullBeam = 2f,
                Crew = new[]
                {
                    new CrewAssignment(Scripting.CrewRole.Helmsman,      1, 0.8f),
                    new CrewAssignment(Scripting.CrewRole.Rigger,        5, 0.7f),
                    new CrewAssignment(Scripting.CrewRole.Navigator,     1, 0.7f),
                    new CrewAssignment(Scripting.CrewRole.Lookout,       2, 0.7f),
                    new CrewAssignment(Scripting.CrewRole.Bosun,         1, 0.7f),
                    new CrewAssignment(Scripting.CrewRole.Cook,          1, 0.6f),
                    new CrewAssignment(Scripting.CrewRole.Carpenter,     2, 0.6f),
                    new CrewAssignment(Scripting.CrewRole.Gunner,        6, 0.7f),
                    new CrewAssignment(Scripting.CrewRole.Signalman,     1, 0.6f),
                    new CrewAssignment(Scripting.CrewRole.CraneOperator, 1, 0.5f),
                },
                Subcomponents = new[]
                {
                    new SubcomponentDef("Hull",           PrimitiveType.Cube,     new Vector3(0f, -0.3f, 0f),      new Vector3(3.0f, 1.0f, 10f), DARK_WOOD),
                    new SubcomponentDef("GunDeck",        PrimitiveType.Cube,     new Vector3(0f, 0.2f, 0f),       new Vector3(2.8f, 0.05f, 9f), DECK_WOOD),
                    new SubcomponentDef("WeatherDeck",    PrimitiveType.Cube,     new Vector3(0f, 0.7f, 0f),       new Vector3(2.6f, 0.05f, 8f), DECK_WOOD),
                    new SubcomponentDef("Forecastle",     PrimitiveType.Cube,     new Vector3(0f, 1.0f, 3.5f),     new Vector3(2.4f, 0.35f, 2.0f), DECK_WOOD),
                    new SubcomponentDef("Quarterdeck",    PrimitiveType.Cube,     new Vector3(0f, 1.0f, -3.5f),    new Vector3(2.4f, 0.35f, 2.5f), DECK_WOOD),
                    new SubcomponentDef("Poopdeck",       PrimitiveType.Cube,     new Vector3(0f, 1.4f, -4.5f),    new Vector3(2.0f, 0.25f, 1.5f), DECK_WOOD),
                    // Bowsprit
                    new SubcomponentDef("Bowsprit",       PrimitiveType.Cylinder, new Vector3(0f, 0.5f, 6.5f),     new Vector3(0.08f, 2.5f, 0.08f), LIGHT_WOOD),
                    // ── FORE MAST (square-rigged, 3 sails) ──
                    new SubcomponentDef("ForeMast",       PrimitiveType.Cylinder, new Vector3(0f, 5.0f, 2.5f),     new Vector3(0.16f, 5.0f, 0.16f), LIGHT_WOOD),
                    new SubcomponentDef("ForeCourse",     PrimitiveType.Cube,     new Vector3(0f, 3.5f, 2.5f),     new Vector3(3.0f, 2.0f, 0.04f), CANVAS),
                    new SubcomponentDef("ForeTopsail",    PrimitiveType.Cube,     new Vector3(0f, 5.5f, 2.5f),     new Vector3(2.5f, 1.8f, 0.04f), CANVAS),
                    new SubcomponentDef("ForeTopgallant", PrimitiveType.Cube,     new Vector3(0f, 7.2f, 2.5f),     new Vector3(1.8f, 1.2f, 0.03f), CANVAS),
                    new SubcomponentDef("ForeYardLower",  PrimitiveType.Cylinder, new Vector3(0f, 2.5f, 2.5f),     new Vector3(0.06f, 1.6f, 0.06f), LIGHT_WOOD),
                    new SubcomponentDef("ForeYardTop",    PrimitiveType.Cylinder, new Vector3(0f, 4.6f, 2.5f),     new Vector3(0.05f, 1.3f, 0.05f), LIGHT_WOOD),
                    new SubcomponentDef("ForeYardTGallant",PrimitiveType.Cylinder,new Vector3(0f, 6.5f, 2.5f),     new Vector3(0.04f, 1.0f, 0.04f), LIGHT_WOOD),
                    new SubcomponentDef("ForeCrowsNest",  PrimitiveType.Cylinder, new Vector3(0f, 8.5f, 2.5f),     new Vector3(0.6f, 0.06f, 0.6f), DARK_WOOD),
                    new SubcomponentDef("ForeCrosstrees", PrimitiveType.Cylinder, new Vector3(0f, 6.8f, 2.5f),     new Vector3(0.04f, 0.7f, 0.04f), LIGHT_WOOD),
                    // Fore shrouds + ratlines (port & starboard, 2 pairs)
                    new SubcomponentDef("ForeShroud_P1",  PrimitiveType.Cylinder, new Vector3(-1.3f, 2.8f, 2.5f),  new Vector3(0.02f, 4.5f, 0.02f), ROPE),
                    new SubcomponentDef("ForeShroud_P2",  PrimitiveType.Cylinder, new Vector3(-1.3f, 2.8f, 3.0f),  new Vector3(0.02f, 4.5f, 0.02f), ROPE),
                    new SubcomponentDef("ForeShroud_S1",  PrimitiveType.Cylinder, new Vector3(1.3f, 2.8f, 2.5f),   new Vector3(0.02f, 4.5f, 0.02f), ROPE),
                    new SubcomponentDef("ForeShroud_S2",  PrimitiveType.Cylinder, new Vector3(1.3f, 2.8f, 3.0f),   new Vector3(0.02f, 4.5f, 0.02f), ROPE),
                    new SubcomponentDef("ForeRatline_P",  PrimitiveType.Cube,     new Vector3(-1.3f, 2.8f, 2.75f), new Vector3(0.01f, 0.01f, 1.0f), ROPE),
                    new SubcomponentDef("ForeRatline_S",  PrimitiveType.Cube,     new Vector3(1.3f, 2.8f, 2.75f),  new Vector3(0.01f, 0.01f, 1.0f), ROPE),
                    // Fore braces (rotate yards — at hull rail)
                    new SubcomponentDef("ForeBrace_P",    PrimitiveType.Cylinder, new Vector3(-1.5f, 0.4f, 2.5f),  new Vector3(0.02f, 0.8f, 0.02f), ROPE),
                    new SubcomponentDef("ForeBrace_S",    PrimitiveType.Cylinder, new Vector3(1.5f, 0.4f, 2.5f),   new Vector3(0.02f, 0.8f, 0.02f), ROPE),
                    // ── MAIN MAST (square-rigged, 3 sails) ──
                    new SubcomponentDef("MainMast",       PrimitiveType.Cylinder, new Vector3(0f, 5.5f, 0f),       new Vector3(0.18f, 5.5f, 0.18f), LIGHT_WOOD),
                    new SubcomponentDef("MainCourse",     PrimitiveType.Cube,     new Vector3(0f, 4.0f, 0f),       new Vector3(3.5f, 2.2f, 0.04f), CANVAS),
                    new SubcomponentDef("MainTopsail",    PrimitiveType.Cube,     new Vector3(0f, 5.8f, 0f),       new Vector3(3.0f, 2.0f, 0.04f), CANVAS),
                    new SubcomponentDef("MainTopgallant", PrimitiveType.Cube,     new Vector3(0f, 7.8f, 0f),       new Vector3(2.2f, 1.4f, 0.03f), CANVAS),
                    new SubcomponentDef("MainYardLower",  PrimitiveType.Cylinder, new Vector3(0f, 3.0f, 0f),       new Vector3(0.07f, 1.8f, 0.07f), LIGHT_WOOD),
                    new SubcomponentDef("MainYardTop",    PrimitiveType.Cylinder, new Vector3(0f, 5.0f, 0f),       new Vector3(0.06f, 1.5f, 0.06f), LIGHT_WOOD),
                    new SubcomponentDef("MainYardTGallant",PrimitiveType.Cylinder,new Vector3(0f, 7.0f, 0f),       new Vector3(0.05f, 1.2f, 0.05f), LIGHT_WOOD),
                    new SubcomponentDef("MainCrowsNest",  PrimitiveType.Cylinder, new Vector3(0f, 9.5f, 0f),       new Vector3(0.65f, 0.06f, 0.65f), DARK_WOOD),
                    new SubcomponentDef("MainCrosstrees", PrimitiveType.Cylinder, new Vector3(0f, 7.5f, 0f),       new Vector3(0.05f, 0.8f, 0.05f), LIGHT_WOOD),
                    // Main shrouds + ratlines
                    new SubcomponentDef("MainShroud_P1",  PrimitiveType.Cylinder, new Vector3(-1.4f, 3.0f, 0f),    new Vector3(0.02f, 5.0f, 0.02f), ROPE),
                    new SubcomponentDef("MainShroud_P2",  PrimitiveType.Cylinder, new Vector3(-1.4f, 3.0f, 0.5f),  new Vector3(0.02f, 5.0f, 0.02f), ROPE),
                    new SubcomponentDef("MainShroud_S1",  PrimitiveType.Cylinder, new Vector3(1.4f, 3.0f, 0f),     new Vector3(0.02f, 5.0f, 0.02f), ROPE),
                    new SubcomponentDef("MainShroud_S2",  PrimitiveType.Cylinder, new Vector3(1.4f, 3.0f, 0.5f),   new Vector3(0.02f, 5.0f, 0.02f), ROPE),
                    new SubcomponentDef("MainRatline_P",  PrimitiveType.Cube,     new Vector3(-1.4f, 3.0f, 0.25f), new Vector3(0.01f, 0.01f, 1.1f), ROPE),
                    new SubcomponentDef("MainRatline_S",  PrimitiveType.Cube,     new Vector3(1.4f, 3.0f, 0.25f),  new Vector3(0.01f, 0.01f, 1.1f), ROPE),
                    // Main braces
                    new SubcomponentDef("MainBrace_P",    PrimitiveType.Cylinder, new Vector3(-1.5f, 0.4f, 0f),    new Vector3(0.02f, 0.8f, 0.02f), ROPE),
                    new SubcomponentDef("MainBrace_S",    PrimitiveType.Cylinder, new Vector3(1.5f, 0.4f, 0f),     new Vector3(0.02f, 0.8f, 0.02f), ROPE),
                    // ── MIZZEN MAST (spanker + topsail) ──
                    new SubcomponentDef("MizzenMast",     PrimitiveType.Cylinder, new Vector3(0f, 4.0f, -2.5f),    new Vector3(0.14f, 4.0f, 0.14f), LIGHT_WOOD),
                    new SubcomponentDef("Spanker",        PrimitiveType.Cube,     new Vector3(0f, 3.5f, -2.5f),    new Vector3(2.0f, 2.0f, 0.04f), CANVAS),
                    new SubcomponentDef("MizzenTopsail",  PrimitiveType.Cube,     new Vector3(0f, 5.5f, -2.5f),    new Vector3(1.5f, 1.2f, 0.03f), CANVAS),
                    new SubcomponentDef("SpankerBoom",    PrimitiveType.Cylinder, new Vector3(0f, 2.5f, -3.2f),    new Vector3(0.06f, 1.8f, 0.06f), LIGHT_WOOD),
                    new SubcomponentDef("SpankerGaff",    PrimitiveType.Cylinder, new Vector3(0f, 4.5f, -3.0f),    new Vector3(0.05f, 1.4f, 0.05f), LIGHT_WOOD),
                    new SubcomponentDef("MizzenCrowsNest",PrimitiveType.Cylinder, new Vector3(0f, 7.0f, -2.5f),    new Vector3(0.5f, 0.06f, 0.5f), DARK_WOOD),
                    // Mizzen shrouds
                    new SubcomponentDef("MizzenShroud_P", PrimitiveType.Cylinder, new Vector3(-1.2f, 2.2f, -2.5f), new Vector3(0.02f, 3.5f, 0.02f), ROPE),
                    new SubcomponentDef("MizzenShroud_S", PrimitiveType.Cylinder, new Vector3(1.2f, 2.2f, -2.5f),  new Vector3(0.02f, 3.5f, 0.02f), ROPE),
                    new SubcomponentDef("MizzenRatline_P",PrimitiveType.Cube,     new Vector3(-1.2f, 2.2f, -2.5f), new Vector3(0.01f, 0.01f, 0.8f), ROPE),
                    new SubcomponentDef("MizzenRatline_S",PrimitiveType.Cube,     new Vector3(1.2f, 2.2f, -2.5f),  new Vector3(0.01f, 0.01f, 0.8f), ROPE),
                    // ── HEADSAILS ──
                    new SubcomponentDef("FlyingJib",      PrimitiveType.Cube,     new Vector3(0f, 3.0f, 5.5f),     new Vector3(1.5f, 1.8f, 0.03f), CANVAS),
                    new SubcomponentDef("Jib",            PrimitiveType.Cube,     new Vector3(0f, 2.5f, 4.5f),     new Vector3(1.8f, 2.2f, 0.03f), CANVAS),
                    new SubcomponentDef("ForeStaysail",   PrimitiveType.Cube,     new Vector3(0f, 2.0f, 3.5f),     new Vector3(1.4f, 1.8f, 0.03f), CANVAS),
                    new SubcomponentDef("MainStaysail",   PrimitiveType.Cube,     new Vector3(0f, 3.5f, 1.2f),     new Vector3(1.6f, 2.0f, 0.03f), CANVAS),
                    // ── STAYS (fore-aft structural) ──
                    new SubcomponentDef("Forestay",       PrimitiveType.Cylinder, new Vector3(0f, 4.0f, 4.5f),     new Vector3(0.02f, 5.0f, 0.02f), ROPE),
                    new SubcomponentDef("MainBackstay",   PrimitiveType.Cylinder, new Vector3(0f, 3.5f, -2.0f),    new Vector3(0.02f, 4.5f, 0.02f), ROPE),
                    new SubcomponentDef("MizzenBackstay", PrimitiveType.Cylinder, new Vector3(0f, 2.5f, -3.5f),    new Vector3(0.02f, 3.0f, 0.02f), ROPE),
                    // ── SHEET CLEATS & HALYARD BITTS ──
                    new SubcomponentDef("SheetCleat_P1",  PrimitiveType.Cube,     new Vector3(-1.5f, 0.28f, 2.0f), new Vector3(0.08f, 0.04f, 0.15f), IRON),
                    new SubcomponentDef("SheetCleat_P2",  PrimitiveType.Cube,     new Vector3(-1.5f, 0.28f, 0f),   new Vector3(0.08f, 0.04f, 0.15f), IRON),
                    new SubcomponentDef("SheetCleat_P3",  PrimitiveType.Cube,     new Vector3(-1.5f, 0.28f, -2.0f),new Vector3(0.08f, 0.04f, 0.15f), IRON),
                    new SubcomponentDef("SheetCleat_S1",  PrimitiveType.Cube,     new Vector3(1.5f, 0.28f, 2.0f),  new Vector3(0.08f, 0.04f, 0.15f), IRON),
                    new SubcomponentDef("SheetCleat_S2",  PrimitiveType.Cube,     new Vector3(1.5f, 0.28f, 0f),    new Vector3(0.08f, 0.04f, 0.15f), IRON),
                    new SubcomponentDef("SheetCleat_S3",  PrimitiveType.Cube,     new Vector3(1.5f, 0.28f, -2.0f), new Vector3(0.08f, 0.04f, 0.15f), IRON),
                    new SubcomponentDef("ForeHalyardBitt",PrimitiveType.Cylinder, new Vector3(0f, 0.3f, 2.5f),     new Vector3(0.07f, 0.14f, 0.07f), IRON),
                    new SubcomponentDef("MainHalyardBitt",PrimitiveType.Cylinder, new Vector3(0f, 0.3f, 0f),       new Vector3(0.07f, 0.14f, 0.07f), IRON),
                    new SubcomponentDef("MizzenHalyardBitt",PrimitiveType.Cylinder,new Vector3(0f, 0.3f, -2.5f),   new Vector3(0.06f, 0.12f, 0.06f), IRON),
                    // Helm
                    new SubcomponentDef("Helm",           PrimitiveType.Cylinder, new Vector3(0f, 1.2f, -3.5f),    new Vector3(0.6f, 0.05f, 0.6f), BRASS),
                    // Chart Room
                    new SubcomponentDef("ChartRoom",      PrimitiveType.Cube,     new Vector3(0f, 0.3f, -3.2f),    new Vector3(1.5f, 0.8f, 1.2f), CHART_ROOM),
                    // Gun ports (3 per side)
                    new SubcomponentDef("GunPort_P1",     PrimitiveType.Cube,     new Vector3(-1.55f, -0.1f, 1.5f), new Vector3(0.05f, 0.25f, 0.35f), GUN_METAL),
                    new SubcomponentDef("GunPort_P2",     PrimitiveType.Cube,     new Vector3(-1.55f, -0.1f, 0f),   new Vector3(0.05f, 0.25f, 0.35f), GUN_METAL),
                    new SubcomponentDef("GunPort_P3",     PrimitiveType.Cube,     new Vector3(-1.55f, -0.1f, -1.5f),new Vector3(0.05f, 0.25f, 0.35f), GUN_METAL),
                    new SubcomponentDef("GunPort_S1",     PrimitiveType.Cube,     new Vector3(1.55f, -0.1f, 1.5f),  new Vector3(0.05f, 0.25f, 0.35f), GUN_METAL),
                    new SubcomponentDef("GunPort_S2",     PrimitiveType.Cube,     new Vector3(1.55f, -0.1f, 0f),    new Vector3(0.05f, 0.25f, 0.35f), GUN_METAL),
                    new SubcomponentDef("GunPort_S3",     PrimitiveType.Cube,     new Vector3(1.55f, -0.1f, -1.5f), new Vector3(0.05f, 0.25f, 0.35f), GUN_METAL),
                    // Cargo
                    new SubcomponentDef("CargoHold",      PrimitiveType.Cube,     new Vector3(0f, 0.22f, 1.0f),    new Vector3(1.2f, 0.06f, 1.2f), CARGO),
                    // Signal mast (above mizzen)
                    new SubcomponentDef("SignalHalyard",  PrimitiveType.Cylinder, new Vector3(0f, 6.5f, -2.5f),    new Vector3(0.04f, 1.0f, 0.04f), ROPE),
                },
                // Tumblehome hull — heavy warship, 3 floors
                Ribs = new[]
                {
                    new RibDef(-4.0f, 0.8f,  1.0f, RibCurveType.Tumblehome,
                        new FloorDef(FloorFunction.CaptainCabin)),
                    new RibDef(-2.5f, 3.0f,  2.2f, RibCurveType.Tumblehome,
                        new FloorDef(FloorFunction.Magazine),
                        new FloorDef(FloorFunction.Hammocks),
                        new FloorDef(FloorFunction.OpenDeck)),
                    new RibDef(-1.0f, 4.0f,  2.8f, RibCurveType.Tumblehome,
                        new FloorDef(FloorFunction.CargoBay),
                        new FloorDef(FloorFunction.Cannons),
                        new FloorDef(FloorFunction.OpenDeck)),
                    new RibDef( 0.5f, 4.0f,  2.8f, RibCurveType.Tumblehome,
                        new FloorDef(FloorFunction.Kitchen),
                        new FloorDef(FloorFunction.Cannons),
                        new FloorDef(FloorFunction.OpenDeck)),
                    new RibDef( 2.0f, 3.2f,  2.2f, RibCurveType.Tumblehome,
                        new FloorDef(FloorFunction.Workshop),
                        new FloorDef(FloorFunction.Cannons),
                        new FloorDef(FloorFunction.OpenDeck)),
                    new RibDef( 3.5f, 2.0f,  1.4f, RibCurveType.Round,
                        new FloorDef(FloorFunction.SailLocker),
                        new FloorDef(FloorFunction.OpenDeck)),
                    new RibDef( 5.0f, 0.5f,  0.6f, RibCurveType.Round,
                        new FloorDef(FloorFunction.None)),
                }
            };
        }

        // ─────────────────────────────────────────────────────────────
        // GALLEON — heavy cargo/warship
        // Tier: WarRoom
        // ─────────────────────────────────────────────────────────────
        private static Blueprint Galleon()
        {
            return new Blueprint
            {
                Class = Scripting.ShipClass.Galleon,
                Tier = Scripting.ChartRoomTier.WarRoom,
                MastCount = 3,
                MaxSpeed = 3.5f,
                SailArea = 80f,
                DeadZone = 42f,
                HullBow = 5.5f,
                HullStern = 5f,
                HullBeam = 2.5f,
                Crew = new[]
                {
                    new CrewAssignment(Scripting.CrewRole.Helmsman,      1, 0.7f),
                    new CrewAssignment(Scripting.CrewRole.Rigger,        6, 0.6f),
                    new CrewAssignment(Scripting.CrewRole.Navigator,     1, 0.7f),
                    new CrewAssignment(Scripting.CrewRole.Lookout,       2, 0.6f),
                    new CrewAssignment(Scripting.CrewRole.Bosun,         1, 0.7f),
                    new CrewAssignment(Scripting.CrewRole.Cook,          2, 0.6f),
                    new CrewAssignment(Scripting.CrewRole.Carpenter,     3, 0.6f),
                    new CrewAssignment(Scripting.CrewRole.Gunner,        4, 0.6f),
                    new CrewAssignment(Scripting.CrewRole.CraneOperator, 2, 0.6f),
                    new CrewAssignment(Scripting.CrewRole.Signalman,     1, 0.5f),
                },
                Subcomponents = new[]
                {
                    new SubcomponentDef("Hull",           PrimitiveType.Cube,     new Vector3(0f, -0.4f, 0f),      new Vector3(3.5f, 1.2f, 12f), DARK_WOOD),
                    new SubcomponentDef("OrlipDeck",      PrimitiveType.Cube,     new Vector3(0f, 0.0f, 0f),       new Vector3(3.2f, 0.05f, 10.5f), DECK_WOOD),
                    new SubcomponentDef("GunDeck",        PrimitiveType.Cube,     new Vector3(0f, 0.5f, 0f),       new Vector3(3.0f, 0.05f, 10f), DECK_WOOD),
                    new SubcomponentDef("WeatherDeck",    PrimitiveType.Cube,     new Vector3(0f, 1.0f, 0f),       new Vector3(2.8f, 0.05f, 9f), DECK_WOOD),
                    new SubcomponentDef("Forecastle",     PrimitiveType.Cube,     new Vector3(0f, 1.4f, 4.0f),     new Vector3(2.6f, 0.4f, 2.5f), DECK_WOOD),
                    new SubcomponentDef("Quarterdeck",    PrimitiveType.Cube,     new Vector3(0f, 1.4f, -4.0f),    new Vector3(2.6f, 0.4f, 2.5f), DECK_WOOD),
                    new SubcomponentDef("Poopdeck",       PrimitiveType.Cube,     new Vector3(0f, 1.8f, -5.5f),    new Vector3(2.2f, 0.3f, 1.5f), DECK_WOOD),
                    // Bowsprit
                    new SubcomponentDef("Bowsprit",       PrimitiveType.Cylinder, new Vector3(0f, 0.6f, 7.5f),     new Vector3(0.10f, 3.0f, 0.10f), LIGHT_WOOD),
                    // ── FORE MAST (square-rigged, 3 sails) ──
                    new SubcomponentDef("ForeMast",       PrimitiveType.Cylinder, new Vector3(0f, 5.5f, 2.5f),     new Vector3(0.18f, 5.5f, 0.18f), LIGHT_WOOD),
                    new SubcomponentDef("ForeCourse",     PrimitiveType.Cube,     new Vector3(0f, 4.0f, 2.5f),     new Vector3(3.5f, 2.2f, 0.04f), CANVAS),
                    new SubcomponentDef("ForeTopsail",    PrimitiveType.Cube,     new Vector3(0f, 6.0f, 2.5f),     new Vector3(3.0f, 2.0f, 0.04f), CANVAS),
                    new SubcomponentDef("ForeTopgallant", PrimitiveType.Cube,     new Vector3(0f, 8.0f, 2.5f),     new Vector3(2.2f, 1.4f, 0.03f), CANVAS),
                    new SubcomponentDef("ForeYardLower",  PrimitiveType.Cylinder, new Vector3(0f, 3.0f, 2.5f),     new Vector3(0.07f, 1.8f, 0.07f), LIGHT_WOOD),
                    new SubcomponentDef("ForeYardTop",    PrimitiveType.Cylinder, new Vector3(0f, 5.2f, 2.5f),     new Vector3(0.06f, 1.5f, 0.06f), LIGHT_WOOD),
                    new SubcomponentDef("ForeYardTGallant",PrimitiveType.Cylinder,new Vector3(0f, 7.2f, 2.5f),     new Vector3(0.05f, 1.2f, 0.05f), LIGHT_WOOD),
                    new SubcomponentDef("ForeCrowsNest",  PrimitiveType.Cylinder, new Vector3(0f, 9.5f, 2.5f),     new Vector3(0.7f, 0.06f, 0.7f), DARK_WOOD),
                    new SubcomponentDef("ForeCrosstrees", PrimitiveType.Cylinder, new Vector3(0f, 7.5f, 2.5f),     new Vector3(0.05f, 0.8f, 0.05f), LIGHT_WOOD),
                    // Fore shrouds + ratlines (3 pairs — heavy vessel)
                    new SubcomponentDef("ForeShroud_P1",  PrimitiveType.Cylinder, new Vector3(-1.5f, 3.0f, 2.5f),  new Vector3(0.02f, 5.0f, 0.02f), ROPE),
                    new SubcomponentDef("ForeShroud_P2",  PrimitiveType.Cylinder, new Vector3(-1.5f, 3.0f, 3.0f),  new Vector3(0.02f, 5.0f, 0.02f), ROPE),
                    new SubcomponentDef("ForeShroud_P3",  PrimitiveType.Cylinder, new Vector3(-1.5f, 3.0f, 2.0f),  new Vector3(0.02f, 5.0f, 0.02f), ROPE),
                    new SubcomponentDef("ForeShroud_S1",  PrimitiveType.Cylinder, new Vector3(1.5f, 3.0f, 2.5f),   new Vector3(0.02f, 5.0f, 0.02f), ROPE),
                    new SubcomponentDef("ForeShroud_S2",  PrimitiveType.Cylinder, new Vector3(1.5f, 3.0f, 3.0f),   new Vector3(0.02f, 5.0f, 0.02f), ROPE),
                    new SubcomponentDef("ForeShroud_S3",  PrimitiveType.Cylinder, new Vector3(1.5f, 3.0f, 2.0f),   new Vector3(0.02f, 5.0f, 0.02f), ROPE),
                    new SubcomponentDef("ForeRatline_P",  PrimitiveType.Cube,     new Vector3(-1.5f, 3.0f, 2.5f),  new Vector3(0.01f, 0.01f, 1.2f), ROPE),
                    new SubcomponentDef("ForeRatline_S",  PrimitiveType.Cube,     new Vector3(1.5f, 3.0f, 2.5f),   new Vector3(0.01f, 0.01f, 1.2f), ROPE),
                    // Fore braces
                    new SubcomponentDef("ForeBrace_P",    PrimitiveType.Cylinder, new Vector3(-1.7f, 0.5f, 2.5f),  new Vector3(0.02f, 0.9f, 0.02f), ROPE),
                    new SubcomponentDef("ForeBrace_S",    PrimitiveType.Cylinder, new Vector3(1.7f, 0.5f, 2.5f),   new Vector3(0.02f, 0.9f, 0.02f), ROPE),
                    // ── MAIN MAST (square-rigged, 3 sails) ──
                    new SubcomponentDef("MainMast",       PrimitiveType.Cylinder, new Vector3(0f, 6.0f, 0f),       new Vector3(0.20f, 6.0f, 0.20f), LIGHT_WOOD),
                    new SubcomponentDef("MainCourse",     PrimitiveType.Cube,     new Vector3(0f, 4.5f, 0f),       new Vector3(4.0f, 2.5f, 0.04f), CANVAS),
                    new SubcomponentDef("MainTopsail",    PrimitiveType.Cube,     new Vector3(0f, 6.5f, 0f),       new Vector3(3.5f, 2.2f, 0.04f), CANVAS),
                    new SubcomponentDef("MainTopgallant", PrimitiveType.Cube,     new Vector3(0f, 8.5f, 0f),       new Vector3(2.5f, 1.6f, 0.03f), CANVAS),
                    new SubcomponentDef("MainYardLower",  PrimitiveType.Cylinder, new Vector3(0f, 3.5f, 0f),       new Vector3(0.08f, 2.0f, 0.08f), LIGHT_WOOD),
                    new SubcomponentDef("MainYardTop",    PrimitiveType.Cylinder, new Vector3(0f, 5.5f, 0f),       new Vector3(0.07f, 1.8f, 0.07f), LIGHT_WOOD),
                    new SubcomponentDef("MainYardTGallant",PrimitiveType.Cylinder,new Vector3(0f, 7.8f, 0f),       new Vector3(0.06f, 1.4f, 0.06f), LIGHT_WOOD),
                    new SubcomponentDef("MainCrowsNest",  PrimitiveType.Cylinder, new Vector3(0f, 10.5f, 0f),      new Vector3(0.75f, 0.06f, 0.75f), DARK_WOOD),
                    new SubcomponentDef("MainCrosstrees", PrimitiveType.Cylinder, new Vector3(0f, 8.2f, 0f),       new Vector3(0.06f, 0.9f, 0.06f), LIGHT_WOOD),
                    // Main shrouds + ratlines (3 pairs)
                    new SubcomponentDef("MainShroud_P1",  PrimitiveType.Cylinder, new Vector3(-1.6f, 3.5f, 0f),    new Vector3(0.02f, 5.5f, 0.02f), ROPE),
                    new SubcomponentDef("MainShroud_P2",  PrimitiveType.Cylinder, new Vector3(-1.6f, 3.5f, 0.5f),  new Vector3(0.02f, 5.5f, 0.02f), ROPE),
                    new SubcomponentDef("MainShroud_P3",  PrimitiveType.Cylinder, new Vector3(-1.6f, 3.5f, -0.5f), new Vector3(0.02f, 5.5f, 0.02f), ROPE),
                    new SubcomponentDef("MainShroud_S1",  PrimitiveType.Cylinder, new Vector3(1.6f, 3.5f, 0f),     new Vector3(0.02f, 5.5f, 0.02f), ROPE),
                    new SubcomponentDef("MainShroud_S2",  PrimitiveType.Cylinder, new Vector3(1.6f, 3.5f, 0.5f),   new Vector3(0.02f, 5.5f, 0.02f), ROPE),
                    new SubcomponentDef("MainShroud_S3",  PrimitiveType.Cylinder, new Vector3(1.6f, 3.5f, -0.5f),  new Vector3(0.02f, 5.5f, 0.02f), ROPE),
                    new SubcomponentDef("MainRatline_P",  PrimitiveType.Cube,     new Vector3(-1.6f, 3.5f, 0f),    new Vector3(0.01f, 0.01f, 1.3f), ROPE),
                    new SubcomponentDef("MainRatline_S",  PrimitiveType.Cube,     new Vector3(1.6f, 3.5f, 0f),     new Vector3(0.01f, 0.01f, 1.3f), ROPE),
                    // Main braces
                    new SubcomponentDef("MainBrace_P",    PrimitiveType.Cylinder, new Vector3(-1.7f, 0.5f, 0f),    new Vector3(0.02f, 0.9f, 0.02f), ROPE),
                    new SubcomponentDef("MainBrace_S",    PrimitiveType.Cylinder, new Vector3(1.7f, 0.5f, 0f),     new Vector3(0.02f, 0.9f, 0.02f), ROPE),
                    // ── MIZZEN MAST (spanker + topsail) ──
                    new SubcomponentDef("MizzenMast",     PrimitiveType.Cylinder, new Vector3(0f, 4.5f, -3.0f),    new Vector3(0.16f, 4.5f, 0.16f), LIGHT_WOOD),
                    new SubcomponentDef("Spanker",        PrimitiveType.Cube,     new Vector3(0f, 3.8f, -3.0f),    new Vector3(2.5f, 2.2f, 0.04f), CANVAS),
                    new SubcomponentDef("MizzenTopsail",  PrimitiveType.Cube,     new Vector3(0f, 6.0f, -3.0f),    new Vector3(1.8f, 1.4f, 0.03f), CANVAS),
                    new SubcomponentDef("SpankerBoom",    PrimitiveType.Cylinder, new Vector3(0f, 2.8f, -3.8f),    new Vector3(0.07f, 2.0f, 0.07f), LIGHT_WOOD),
                    new SubcomponentDef("SpankerGaff",    PrimitiveType.Cylinder, new Vector3(0f, 5.0f, -3.5f),    new Vector3(0.06f, 1.6f, 0.06f), LIGHT_WOOD),
                    new SubcomponentDef("MizzenCrowsNest",PrimitiveType.Cylinder, new Vector3(0f, 7.8f, -3.0f),    new Vector3(0.6f, 0.06f, 0.6f), DARK_WOOD),
                    // Mizzen shrouds
                    new SubcomponentDef("MizzenShroud_P", PrimitiveType.Cylinder, new Vector3(-1.4f, 2.5f, -3.0f), new Vector3(0.02f, 4.0f, 0.02f), ROPE),
                    new SubcomponentDef("MizzenShroud_S", PrimitiveType.Cylinder, new Vector3(1.4f, 2.5f, -3.0f),  new Vector3(0.02f, 4.0f, 0.02f), ROPE),
                    new SubcomponentDef("MizzenRatline_P",PrimitiveType.Cube,     new Vector3(-1.4f, 2.5f, -3.0f), new Vector3(0.01f, 0.01f, 1.0f), ROPE),
                    new SubcomponentDef("MizzenRatline_S",PrimitiveType.Cube,     new Vector3(1.4f, 2.5f, -3.0f),  new Vector3(0.01f, 0.01f, 1.0f), ROPE),
                    // ── HEADSAILS ──
                    new SubcomponentDef("FlyingJib",      PrimitiveType.Cube,     new Vector3(0f, 3.5f, 6.5f),     new Vector3(1.8f, 2.0f, 0.03f), CANVAS),
                    new SubcomponentDef("Jib",            PrimitiveType.Cube,     new Vector3(0f, 3.0f, 5.5f),     new Vector3(2.2f, 2.5f, 0.03f), CANVAS),
                    new SubcomponentDef("ForeStaysail",   PrimitiveType.Cube,     new Vector3(0f, 2.5f, 4.5f),     new Vector3(1.8f, 2.0f, 0.03f), CANVAS),
                    new SubcomponentDef("MainStaysail",   PrimitiveType.Cube,     new Vector3(0f, 4.0f, 1.2f),     new Vector3(2.0f, 2.2f, 0.03f), CANVAS),
                    // ── STAYS ──
                    new SubcomponentDef("Forestay",       PrimitiveType.Cylinder, new Vector3(0f, 4.5f, 5.5f),     new Vector3(0.025f, 5.5f, 0.025f), ROPE),
                    new SubcomponentDef("MainBackstay",   PrimitiveType.Cylinder, new Vector3(0f, 4.0f, -2.5f),    new Vector3(0.025f, 5.0f, 0.025f), ROPE),
                    new SubcomponentDef("MizzenBackstay", PrimitiveType.Cylinder, new Vector3(0f, 3.0f, -4.5f),    new Vector3(0.025f, 3.5f, 0.025f), ROPE),
                    // ── SHEET CLEATS, HALYARD BITTS, PINRAILS ──
                    new SubcomponentDef("SheetCleat_P1",  PrimitiveType.Cube,     new Vector3(-1.7f, 0.3f, 2.0f),  new Vector3(0.08f, 0.04f, 0.15f), IRON),
                    new SubcomponentDef("SheetCleat_P2",  PrimitiveType.Cube,     new Vector3(-1.7f, 0.3f, 0f),    new Vector3(0.08f, 0.04f, 0.15f), IRON),
                    new SubcomponentDef("SheetCleat_P3",  PrimitiveType.Cube,     new Vector3(-1.7f, 0.3f, -2.5f), new Vector3(0.08f, 0.04f, 0.15f), IRON),
                    new SubcomponentDef("SheetCleat_S1",  PrimitiveType.Cube,     new Vector3(1.7f, 0.3f, 2.0f),   new Vector3(0.08f, 0.04f, 0.15f), IRON),
                    new SubcomponentDef("SheetCleat_S2",  PrimitiveType.Cube,     new Vector3(1.7f, 0.3f, 0f),     new Vector3(0.08f, 0.04f, 0.15f), IRON),
                    new SubcomponentDef("SheetCleat_S3",  PrimitiveType.Cube,     new Vector3(1.7f, 0.3f, -2.5f),  new Vector3(0.08f, 0.04f, 0.15f), IRON),
                    // Pin rails (belaying pin rows at shroud base — halyards belayed here)
                    new SubcomponentDef("PinRail_P",      PrimitiveType.Cube,     new Vector3(-1.6f, 0.5f, 0.5f),  new Vector3(0.06f, 0.06f, 2.0f), LIGHT_WOOD),
                    new SubcomponentDef("PinRail_S",      PrimitiveType.Cube,     new Vector3(1.6f, 0.5f, 0.5f),   new Vector3(0.06f, 0.06f, 2.0f), LIGHT_WOOD),
                    new SubcomponentDef("ForeHalyardBitt",PrimitiveType.Cylinder, new Vector3(0f, 0.35f, 2.5f),    new Vector3(0.08f, 0.16f, 0.08f), IRON),
                    new SubcomponentDef("MainHalyardBitt",PrimitiveType.Cylinder, new Vector3(0f, 0.35f, 0f),      new Vector3(0.08f, 0.16f, 0.08f), IRON),
                    new SubcomponentDef("MizzenHalyardBitt",PrimitiveType.Cylinder,new Vector3(0f, 0.35f, -3.0f),  new Vector3(0.07f, 0.14f, 0.07f), IRON),
                    // Helm
                    new SubcomponentDef("Helm",           PrimitiveType.Cylinder, new Vector3(0f, 1.6f, -4.0f),    new Vector3(0.7f, 0.05f, 0.7f), BRASS),
                    // Chart Room (large)
                    new SubcomponentDef("ChartRoom",      PrimitiveType.Cube,     new Vector3(0f, 0.5f, -4.0f),    new Vector3(1.8f, 1.0f, 1.5f), CHART_ROOM),
                    // Cargo hold hatches (2)
                    new SubcomponentDef("CargoHold_Fore", PrimitiveType.Cube,     new Vector3(0f, 1.02f, 1.5f),    new Vector3(1.5f, 0.06f, 1.5f), CARGO),
                    new SubcomponentDef("CargoHold_Aft",  PrimitiveType.Cube,     new Vector3(0f, 1.02f, -1.0f),   new Vector3(1.5f, 0.06f, 1.5f), CARGO),
                    // Crane
                    new SubcomponentDef("CraneMast",      PrimitiveType.Cylinder, new Vector3(1.2f, 2.5f, 1.5f),   new Vector3(0.10f, 2.0f, 0.10f), IRON),
                    new SubcomponentDef("CraneBoom",      PrimitiveType.Cylinder, new Vector3(1.8f, 3.0f, 1.5f),   new Vector3(0.06f, 1.5f, 0.06f), IRON),
                    // Gun ports (2 per side)
                    new SubcomponentDef("GunPort_P1",     PrimitiveType.Cube,     new Vector3(-1.8f, 0.1f, 1.0f),  new Vector3(0.05f, 0.3f, 0.4f), GUN_METAL),
                    new SubcomponentDef("GunPort_P2",     PrimitiveType.Cube,     new Vector3(-1.8f, 0.1f, -1.0f), new Vector3(0.05f, 0.3f, 0.4f), GUN_METAL),
                    new SubcomponentDef("GunPort_S1",     PrimitiveType.Cube,     new Vector3(1.8f, 0.1f, 1.0f),   new Vector3(0.05f, 0.3f, 0.4f), GUN_METAL),
                    new SubcomponentDef("GunPort_S2",     PrimitiveType.Cube,     new Vector3(1.8f, 0.1f, -1.0f),  new Vector3(0.05f, 0.3f, 0.4f), GUN_METAL),
                },
                // Flat/Round hull — heavy cargo hauler, 4 floors amidships
                Ribs = new[]
                {
                    new RibDef(-5.0f, 1.0f,  1.2f, RibCurveType.Round,
                        new FloorDef(FloorFunction.CaptainCabin)),
                    new RibDef(-3.5f, 3.5f,  2.5f, RibCurveType.Flat,
                        new FloorDef(FloorFunction.Magazine),
                        new FloorDef(FloorFunction.Hammocks),
                        new FloorDef(FloorFunction.OpenDeck)),
                    new RibDef(-2.0f, 4.5f,  3.2f, RibCurveType.Flat,
                        new FloorDef(FloorFunction.CargoBay),
                        new FloorDef(FloorFunction.Hammocks),
                        new FloorDef(FloorFunction.Cannons),
                        new FloorDef(FloorFunction.OpenDeck)),
                    new RibDef( 0.0f, 5.0f,  3.5f, RibCurveType.Flat,
                        new FloorDef(FloorFunction.CargoBay),
                        new FloorDef(FloorFunction.Kitchen),
                        new FloorDef(FloorFunction.Cannons),
                        new FloorDef(FloorFunction.OpenDeck)),
                    new RibDef( 2.0f, 4.5f,  3.2f, RibCurveType.Flat,
                        new FloorDef(FloorFunction.CargoBay),
                        new FloorDef(FloorFunction.Workshop),
                        new FloorDef(FloorFunction.Cannons),
                        new FloorDef(FloorFunction.OpenDeck)),
                    new RibDef( 3.5f, 3.0f,  2.2f, RibCurveType.Round,
                        new FloorDef(FloorFunction.SailLocker),
                        new FloorDef(FloorFunction.OpenDeck)),
                    new RibDef( 5.0f, 1.5f,  1.4f, RibCurveType.Round,
                        new FloorDef(FloorFunction.SailLocker)),
                    new RibDef( 5.5f, 0.4f,  0.6f, RibCurveType.Round,
                        new FloorDef(FloorFunction.None)),
                }
            };
        }

        // ─────────────────────────────────────────────────────────────
        // FLAGSHIP — fleet command, full Python subset
        // Tier: AdmiralsBridge
        // ─────────────────────────────────────────────────────────────
        private static Blueprint FlagshipBlueprint()
        {
            var bp = Frigate(); // Starts as Frigate layout
            bp.Class = Scripting.ShipClass.Flagship;
            bp.Tier = Scripting.ChartRoomTier.AdmiralsBridge;

            // Flagship gets upgraded ribs: captain's cabin → brig at stern
            bp.Ribs = new[]
            {
                new RibDef(-4.0f, 1.0f,  1.2f, RibCurveType.Tumblehome,
                    new FloorDef(FloorFunction.CaptainCabin)),
                new RibDef(-2.5f, 3.2f,  2.5f, RibCurveType.Tumblehome,
                    new FloorDef(FloorFunction.Brig),
                    new FloorDef(FloorFunction.Hammocks),
                    new FloorDef(FloorFunction.OpenDeck)),
                new RibDef(-1.0f, 4.0f,  2.8f, RibCurveType.Tumblehome,
                    new FloorDef(FloorFunction.Magazine),
                    new FloorDef(FloorFunction.Cannons),
                    new FloorDef(FloorFunction.OpenDeck)),
                new RibDef( 0.5f, 4.0f,  2.8f, RibCurveType.Tumblehome,
                    new FloorDef(FloorFunction.Kitchen),
                    new FloorDef(FloorFunction.Cannons),
                    new FloorDef(FloorFunction.OpenDeck)),
                new RibDef( 2.0f, 3.5f,  2.4f, RibCurveType.Tumblehome,
                    new FloorDef(FloorFunction.CargoBay),
                    new FloorDef(FloorFunction.Workshop),
                    new FloorDef(FloorFunction.OpenDeck)),
                new RibDef( 3.5f, 2.0f,  1.4f, RibCurveType.Round,
                    new FloorDef(FloorFunction.SailLocker),
                    new FloorDef(FloorFunction.OpenDeck)),
                new RibDef( 5.0f, 0.5f,  0.6f, RibCurveType.Round,
                    new FloorDef(FloorFunction.None)),
            };

            // Upgrade crew
            bp.Crew = new[]
            {
                new CrewAssignment(Scripting.CrewRole.Helmsman,      1, 0.9f),
                new CrewAssignment(Scripting.CrewRole.Rigger,        6, 0.8f),
                new CrewAssignment(Scripting.CrewRole.Navigator,     2, 0.9f),
                new CrewAssignment(Scripting.CrewRole.Lookout,       3, 0.8f),
                new CrewAssignment(Scripting.CrewRole.Bosun,         1, 0.8f),
                new CrewAssignment(Scripting.CrewRole.Cook,          2, 0.7f),
                new CrewAssignment(Scripting.CrewRole.Carpenter,     2, 0.7f),
                new CrewAssignment(Scripting.CrewRole.Gunner,        8, 0.8f),
                new CrewAssignment(Scripting.CrewRole.CraneOperator, 2, 0.7f),
                new CrewAssignment(Scripting.CrewRole.Signalman,     2, 0.8f),
            };

            return bp;
        }

        // ═══════════════════════════════════════════════════════════════
        // CREW SUMMARY
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Total crew count for a blueprint.</summary>
        public static int TotalCrew(Blueprint bp)
        {
            int total = 0;
            if (bp.Crew != null)
                foreach (var c in bp.Crew) total += c.Count;
            return total;
        }

        /// <summary>Log the manifest for a blueprint.</summary>
        public static string Describe(Blueprint bp)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"╔══ {bp.Class} ══╗");
            sb.AppendLine($"║ Tier: {bp.Tier}");
            sb.AppendLine($"║ Masts: {bp.MastCount} | Speed: {bp.MaxSpeed} m/s | Sail: {bp.SailArea} m²");
            sb.AppendLine($"║ Hull: bow={bp.HullBow} stern={bp.HullStern} beam={bp.HullBeam}");
            sb.AppendLine($"║ Crew ({TotalCrew(bp)}):");
            if (bp.Crew != null)
                foreach (var c in bp.Crew)
                    sb.AppendLine($"║   {c.Role}: {c.Count} × {c.Skill:P0} skill");
            sb.AppendLine($"║ Subcomponents: {bp.Subcomponents?.Length ?? 0}");
            if (bp.Ribs != null && bp.Ribs.Length > 0)
            {
                sb.AppendLine($"║ Ribs ({bp.Ribs.Length}):");
                foreach (var rib in bp.Ribs)
                    sb.AppendLine($"║   z={rib.ZOffset:+0.0;-0.0} w={rib.Width:F1} h={rib.Height:F1} " +
                                  $"{rib.CurveType} floors={rib.FloorCount}");
            }
            sb.AppendLine($"╚{'═', 1}══════════════╝");
            return sb.ToString();
        }
    }
}
