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
        GET_SEWER_POP    = 4,
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
        GET_CELL         = 17,  // get_cell(slot) → cell type id
        GET_SEWER_CELL   = 18,  // get_sewer_cell(slot) → sewer type id

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
