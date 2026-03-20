// Copyright CodeGamified 2025-2026
// MIT License — PopVuj
using System.Collections.Generic;
using CodeGamified.Engine;
using CodeGamified.Engine.Compiler;

namespace PopVuj.Scripting
{
    /// <summary>
    /// Opcode enum for PopVuj — divine queries and god powers.
    ///
    /// Convention:
    ///   - Queries first (read world state → R0)
    ///   - Commands last (act on world, result → R0: 1=success, 0=fail)
    ///
    /// Linear city: no row dimension. Slots are indexed 0..Width-1.
    /// </summary>
    public enum PopVujOpCode
    {
        // ── Queries (no args, result in R0) ──────────────────
        GET_POPULATION   = 0,
        GET_FAITH        = 1,
        GET_DISEASE      = 2,
        GET_CRIME        = 3,
        GET_SEWER_POP    = 4,   // sewer den slot count (derived from houses)
        GET_WEATHER      = 5,   // 0=clear, 1=rain, 2=storm, 3=drought
        GET_CITY_W       = 6,
        GET_SCORE        = 7,
        GET_GAME_OVER    = 8,
        GET_INPUT        = 9,
        GET_HERETICS     = 10,
        GET_BIRTHS       = 11,
        GET_DEATHS       = 12,
        GET_HOUSES       = 13,
        GET_CHAPELS      = 14,
        GET_FARMS        = 15,
        GET_WORKSHOPS    = 16,

        // ── One-arg query (R0=slot → R0=cell type) ──────────
        GET_CELL         = 17,  // get_cell(slot) → surface cell type id
        GET_SEWER_CELL   = 18,  // get_sewer_cell(slot) → sewer archetype (0-6, derived)

        // ── Commands (args in R0, result in R0) ──────────────
        SEND_PROPHET     = 19,  // → 1=ok
        SMITE            = 20,  // → 1=ok, 0=no heretics
        SET_WEATHER      = 21,  // set_weather(id) → 1=ok
        SUMMON_BEARS     = 22,  // → kills count
        SEND_OMEN        = 23,  // → 1=ok
        BUILD_CHAPEL     = 24,  // build_chapel(slot) → 1=ok (costs 2 wood)
        BUILD_HOUSE      = 25,  // build_house(slot) → 1=ok (costs 1 wood)
        HARVEST_TREE     = 26,  // harvest_tree(slot) → 1=ok (+1 wood)
        EXPAND_BUILDING  = 27,  // expand_building(slot) → 1=ok (costs 1 wood)
        SHRINK_BUILDING  = 28,  // shrink_building(slot) → 1=ok (+1 wood)
        GET_WOOD         = 29,  // get_wood() → wood count
        GET_TREES        = 30,  // get_trees() → tree count

        // ── Harbor queries ────────────────────────────────────────
        GET_SHIPS        = 31,  // get_ships() → total ships
        GET_SHIPS_DOCKED = 32,  // get_ships_docked() → ships at pier
        GET_SHIPS_AT_SEA = 33,  // get_ships_at_sea() → ships on voyages
        GET_HARBOR_WORKERS = 34, // get_harbor_workers() → minions in harbor roles
        GET_TRADE_INCOME = 35,  // get_trade_income() → cumulative trade value

        // ── Harbor commands ───────────────────────────────────────
        BUILD_SHIPYARD   = 36,  // build_shipyard(slot) → 1=ok (costs 3 wood)
        BUILD_PIER       = 37,  // build_pier(slot) → 1=ok (costs 2 wood, contiguous)
        BUILD_CRANE      = 38,  // build_crane(slot) → 1=ok (costs 3 wood, installs crane fixture on pier slot)
        BUILD_SHIP       = 39,  // build_ship(width) → 1=ok (costs scale with width)
        LAUNCH_SHIP      = 40,  // launch_ship() → 1=ok
        SEND_TRADE       = 41,  // send_trade(route) → 1=ok (0-4)
        REPAIR_SHIP      = 42,  // repair_ship() → 1=ok (costs 1 wood)
        BLESS_SHIP       = 43,  // bless_ship() → 1=ok

        // ── Ship module customization ─────────────────────────────
        SET_SHIP_MODULE  = 44,  // set_ship_module(packed) → 1=ok (packed = ship_id*100 + tile*10 + module)
        GET_SHIP_MODULE  = 45,  // get_ship_module(packed) → module type (packed = ship_id*100 + tile*10)
    }

    /// <summary>
    /// Compiler extension — maps Python builtin names to CUSTOM opcodes.
    /// One switch case per builtin function.
    /// </summary>
    public class PopVujCompilerExtension : ICompilerExtension
    {
        public void RegisterBuiltins(CompilerContext ctx) { }

        public bool TryCompileCall(string functionName, List<AstNodes.ExprNode> args,
                                   CompilerContext ctx, int sourceLine)
        {
            switch (functionName)
            {
                // ── Queries: no args, result in R0 ──────────────
                case "get_population":
                    Emit(ctx, PopVujOpCode.GET_POPULATION, sourceLine, "get_population → R0");
                    return true;
                case "get_faith":
                    Emit(ctx, PopVujOpCode.GET_FAITH, sourceLine, "get_faith → R0");
                    return true;
                case "get_disease":
                    Emit(ctx, PopVujOpCode.GET_DISEASE, sourceLine, "get_disease → R0");
                    return true;
                case "get_crime":
                    Emit(ctx, PopVujOpCode.GET_CRIME, sourceLine, "get_crime → R0");
                    return true;
                case "get_sewer_pop":
                    Emit(ctx, PopVujOpCode.GET_SEWER_POP, sourceLine, "get_sewer_pop → R0");
                    return true;
                case "get_weather":
                    Emit(ctx, PopVujOpCode.GET_WEATHER, sourceLine, "get_weather → R0");
                    return true;
                case "get_city_width":
                    Emit(ctx, PopVujOpCode.GET_CITY_W, sourceLine, "get_city_width → R0");
                    return true;
                case "get_score":
                    Emit(ctx, PopVujOpCode.GET_SCORE, sourceLine, "get_score → R0");
                    return true;
                case "get_game_over":
                    Emit(ctx, PopVujOpCode.GET_GAME_OVER, sourceLine, "get_game_over → R0");
                    return true;
                case "get_input":
                    Emit(ctx, PopVujOpCode.GET_INPUT, sourceLine, "get_input → R0");
                    return true;
                case "get_heretics":
                    Emit(ctx, PopVujOpCode.GET_HERETICS, sourceLine, "get_heretics → R0");
                    return true;
                case "get_births":
                    Emit(ctx, PopVujOpCode.GET_BIRTHS, sourceLine, "get_births → R0");
                    return true;
                case "get_deaths":
                    Emit(ctx, PopVujOpCode.GET_DEATHS, sourceLine, "get_deaths → R0");
                    return true;
                case "get_houses":
                    Emit(ctx, PopVujOpCode.GET_HOUSES, sourceLine, "get_houses → R0");
                    return true;
                case "get_chapels":
                    Emit(ctx, PopVujOpCode.GET_CHAPELS, sourceLine, "get_chapels → R0");
                    return true;
                case "get_farms":
                    Emit(ctx, PopVujOpCode.GET_FARMS, sourceLine, "get_farms → R0");
                    return true;
                case "get_workshops":
                    Emit(ctx, PopVujOpCode.GET_WORKSHOPS, sourceLine, "get_workshops → R0");
                    return true;

                // ── One-arg queries: arg → R0, result → R0 ─────
                case "get_cell":
                    if (args != null && args.Count > 0)
                        args[0].Compile(ctx); // slot → R0
                    Emit(ctx, PopVujOpCode.GET_CELL, sourceLine, "get_cell(R0=slot) → R0");
                    return true;
                case "get_sewer_cell":
                    if (args != null && args.Count > 0)
                        args[0].Compile(ctx); // slot → R0
                    Emit(ctx, PopVujOpCode.GET_SEWER_CELL, sourceLine, "get_sewer_cell(R0=slot) → R0");
                    return true;

                // ── Commands: no args ───────────────────────────
                case "send_prophet":
                    Emit(ctx, PopVujOpCode.SEND_PROPHET, sourceLine, "send_prophet → R0");
                    return true;
                case "smite":
                    Emit(ctx, PopVujOpCode.SMITE, sourceLine, "smite → R0");
                    return true;
                case "summon_bears":
                    Emit(ctx, PopVujOpCode.SUMMON_BEARS, sourceLine, "summon_bears → R0");
                    return true;
                case "send_omen":
                    Emit(ctx, PopVujOpCode.SEND_OMEN, sourceLine, "send_omen → R0");
                    return true;

                // ── Commands: one arg ───────────────────────────
                case "set_weather":
                    if (args != null && args.Count > 0)
                        args[0].Compile(ctx); // weather id → R0
                    Emit(ctx, PopVujOpCode.SET_WEATHER, sourceLine, "set_weather(R0) → R0");
                    return true;
                case "build_chapel":
                    if (args != null && args.Count > 0)
                        args[0].Compile(ctx); // slot → R0
                    Emit(ctx, PopVujOpCode.BUILD_CHAPEL, sourceLine, "build_chapel(R0=slot) → R0");
                    return true;
                case "build_house":
                    if (args != null && args.Count > 0)
                        args[0].Compile(ctx); // slot → R0
                    Emit(ctx, PopVujOpCode.BUILD_HOUSE, sourceLine, "build_house(R0=slot) → R0");
                    return true;
                case "harvest_tree":
                    if (args != null && args.Count > 0)
                        args[0].Compile(ctx); // slot → R0
                    Emit(ctx, PopVujOpCode.HARVEST_TREE, sourceLine, "harvest_tree(R0=slot) → R0");
                    return true;
                case "expand_building":
                    if (args != null && args.Count > 0)
                        args[0].Compile(ctx); // slot → R0
                    Emit(ctx, PopVujOpCode.EXPAND_BUILDING, sourceLine, "expand_building(R0=slot) → R0");
                    return true;
                case "shrink_building":
                    if (args != null && args.Count > 0)
                        args[0].Compile(ctx); // slot → R0
                    Emit(ctx, PopVujOpCode.SHRINK_BUILDING, sourceLine, "shrink_building(R0=slot) → R0");
                    return true;
                case "get_wood":
                    Emit(ctx, PopVujOpCode.GET_WOOD, sourceLine, "get_wood → R0");
                    return true;
                case "get_trees":
                    Emit(ctx, PopVujOpCode.GET_TREES, sourceLine, "get_trees → R0");
                    return true;

                // ── Harbor queries ──────────────────────────────────────
                case "get_ships":
                    Emit(ctx, PopVujOpCode.GET_SHIPS, sourceLine, "get_ships → R0");
                    return true;
                case "get_ships_docked":
                    Emit(ctx, PopVujOpCode.GET_SHIPS_DOCKED, sourceLine, "get_ships_docked → R0");
                    return true;
                case "get_ships_at_sea":
                    Emit(ctx, PopVujOpCode.GET_SHIPS_AT_SEA, sourceLine, "get_ships_at_sea → R0");
                    return true;
                case "get_harbor_workers":
                    Emit(ctx, PopVujOpCode.GET_HARBOR_WORKERS, sourceLine, "get_harbor_workers → R0");
                    return true;
                case "get_trade_income":
                    Emit(ctx, PopVujOpCode.GET_TRADE_INCOME, sourceLine, "get_trade_income → R0");
                    return true;

                // ── Harbor commands ─────────────────────────────────────
                case "build_shipyard":
                    if (args != null && args.Count > 0)
                        args[0].Compile(ctx);
                    Emit(ctx, PopVujOpCode.BUILD_SHIPYARD, sourceLine, "build_shipyard(R0=slot) → R0");
                    return true;
                case "build_pier":
                    if (args != null && args.Count > 0)
                        args[0].Compile(ctx);
                    Emit(ctx, PopVujOpCode.BUILD_PIER, sourceLine, "build_pier(R0=slot) → R0");
                    return true;
                case "build_crane":
                    if (args != null && args.Count > 0)
                        args[0].Compile(ctx);
                    Emit(ctx, PopVujOpCode.BUILD_CRANE, sourceLine, "build_crane(R0=slot) → R0");
                    return true;
                case "build_ship":
                    if (args != null && args.Count > 0)
                        args[0].Compile(ctx);
                    Emit(ctx, PopVujOpCode.BUILD_SHIP, sourceLine, "build_ship(R0=width) → R0");
                    return true;
                case "launch_ship":
                    Emit(ctx, PopVujOpCode.LAUNCH_SHIP, sourceLine, "launch_ship → R0");
                    return true;
                case "send_trade":
                    if (args != null && args.Count > 0)
                        args[0].Compile(ctx);
                    Emit(ctx, PopVujOpCode.SEND_TRADE, sourceLine, "send_trade(R0=route) → R0");
                    return true;
                case "repair_ship":
                    Emit(ctx, PopVujOpCode.REPAIR_SHIP, sourceLine, "repair_ship → R0");
                    return true;
                case "bless_ship":
                    Emit(ctx, PopVujOpCode.BLESS_SHIP, sourceLine, "bless_ship → R0");
                    return true;

                // ── Ship module customization ────────────────────
                // set_ship_module(packed): packed = ship_id*100 + tile*10 + module
                //   Module types: 0=None 1=Helm 2=Mast 3=Cannon 4=Crane
                //                 5=Oars 6=CargoHatch 7=Cabin 8=FishingRig 9=Lookout
                case "set_ship_module":
                    if (args != null && args.Count > 0)
                        args[0].Compile(ctx);
                    Emit(ctx, PopVujOpCode.SET_SHIP_MODULE, sourceLine, "set_ship_module(R0=packed) → R0");
                    return true;
                // get_ship_module(packed): packed = ship_id*100 + tile*10 → module type in R0
                case "get_ship_module":
                    if (args != null && args.Count > 0)
                        args[0].Compile(ctx);
                    Emit(ctx, PopVujOpCode.GET_SHIP_MODULE, sourceLine, "get_ship_module(R0=packed) → R0");
                    return true;

                // ── Tarot card name lookups (compile-time) ──────
                case "get_face":
                {
                    string[] arcana = {
                        "The Fool", "The Magician", "High Priestess", "The Empress",
                        "The Emperor", "The Hierophant", "The Lovers", "The Chariot",
                        "Strength", "The Hermit", "Wheel of Fortune", "Justice",
                        "The Hanged Man", "Death", "Temperance", "The Devil",
                        "The Tower", "The Star", "The Moon", "The Sun",
                        "Judgement", "The World"
                    };
                    int baseIdx = ctx.AddStringConstant(arcana[0]);
                    for (int i = 1; i < arcana.Length; i++)
                        ctx.AddStringConstant(arcana[i]);
                    if (args != null && args.Count > 0)
                        args[0].Compile(ctx);
                    int bfi = ctx.AddFloatConstant((float)baseIdx);
                    ctx.Emit(OpCode.LOAD_FLOAT, 1, bfi, sourceLine: sourceLine,
                        comment: $"face base={baseIdx}");
                    ctx.Emit(OpCode.ADD, 0, 1, sourceLine: sourceLine,
                        comment: "get_face(R0) → str idx");
                    return true;
                }
                case "get_gentle_face":
                {
                    string[] gentle = {
                        "High Priestess", "The Empress", "Strength", "The Hermit",
                        "Temperance", "The Star", "The Sun", "The World",
                        "Ace of Cups", "Ace of Pentacles"
                    };
                    int baseIdx = ctx.AddStringConstant(gentle[0]);
                    for (int i = 1; i < gentle.Length; i++)
                        ctx.AddStringConstant(gentle[i]);
                    if (args != null && args.Count > 0)
                        args[0].Compile(ctx);
                    int bfi = ctx.AddFloatConstant((float)baseIdx);
                    ctx.Emit(OpCode.LOAD_FLOAT, 1, bfi, sourceLine: sourceLine,
                        comment: $"gentle base={baseIdx}");
                    ctx.Emit(OpCode.ADD, 0, 1, sourceLine: sourceLine,
                        comment: "get_gentle_face(R0) → str idx");
                    return true;
                }

                default:
                    return false;
            }
        }

        private static void Emit(CompilerContext ctx, PopVujOpCode op, int line, string comment)
        {
            ctx.Emit(OpCode.CUSTOM_0 + (int)op, 0, 0, 0, line, comment);
        }

        public bool TryCompileMethodCall(string objectName, string methodName,
                                         List<AstNodes.ExprNode> args,
                                         CompilerContext ctx, int sourceLine) => false;

        public bool TryCompileObjectDecl(string typeName, string varName,
                                         List<AstNodes.ExprNode> constructorArgs,
                                         CompilerContext ctx, int sourceLine) => false;
    }
}
