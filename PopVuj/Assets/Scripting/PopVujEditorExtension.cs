// Copyright CodeGamified 2025-2026
// MIT License — PopVuj
using System.Collections.Generic;
using CodeGamified.Editor;

namespace PopVuj.Scripting
{
    /// <summary>
    /// Editor extension — provides divine builtins to the tap-to-code editor.
    /// </summary>
    public class PopVujEditorExtension : IEditorExtension
    {
        public List<EditorTypeInfo> GetAvailableTypes() => new();

        public List<EditorFuncInfo> GetAvailableFunctions() => new()
        {
            // ── Queries ─────────────────────────────────────────
            new EditorFuncInfo { Name = "get_population",  Hint = "total living minions",       ArgCount = 0 },
            new EditorFuncInfo { Name = "get_faith",       Hint = "faith level (0.0-1.0)",      ArgCount = 0 },
            new EditorFuncInfo { Name = "get_disease",     Hint = "disease level (0.0-1.0)",    ArgCount = 0 },
            new EditorFuncInfo { Name = "get_crime",       Hint = "crime level (0.0-1.0)",      ArgCount = 0 },
            new EditorFuncInfo { Name = "get_sewer_pop",   Hint = "underground population",     ArgCount = 0 },
            new EditorFuncInfo { Name = "get_weather",     Hint = "0=clear 1=rain 2=storm 3=drought", ArgCount = 0 },
            new EditorFuncInfo { Name = "get_heretics",    Hint = "minions rejecting your rule", ArgCount = 0 },
            new EditorFuncInfo { Name = "get_births",      Hint = "total births this era",      ArgCount = 0 },
            new EditorFuncInfo { Name = "get_deaths",      Hint = "total deaths this era",      ArgCount = 0 },
            new EditorFuncInfo { Name = "get_houses",      Hint = "house count",                ArgCount = 0 },
            new EditorFuncInfo { Name = "get_chapels",     Hint = "chapel count",               ArgCount = 0 },
            new EditorFuncInfo { Name = "get_farms",       Hint = "farm count",                 ArgCount = 0 },
            new EditorFuncInfo { Name = "get_workshops",   Hint = "workshop count",             ArgCount = 0 },
            new EditorFuncInfo { Name = "get_city_width",  Hint = "city strip width (slots)",   ArgCount = 0 },
            new EditorFuncInfo { Name = "get_cell",        Hint = "surface cell type at slot",  ArgCount = 1 },
            new EditorFuncInfo { Name = "get_sewer_cell",  Hint = "sewer cell type at slot",    ArgCount = 1 },
            new EditorFuncInfo { Name = "get_score",       Hint = "civilization score",         ArgCount = 0 },
            new EditorFuncInfo { Name = "get_game_over",   Hint = "1 if collapsed",             ArgCount = 0 },
            new EditorFuncInfo { Name = "get_input",       Hint = "keyboard input code",        ArgCount = 0 },

            // ── Commands ────────────────────────────────────────
            new EditorFuncInfo { Name = "send_prophet",    Hint = "boost faith (+0.08)",        ArgCount = 0 },
            new EditorFuncInfo { Name = "smite",           Hint = "kill a heretic",             ArgCount = 0 },
            new EditorFuncInfo { Name = "summon_bears",    Hint = "unleash bears on heretics",  ArgCount = 0 },
            new EditorFuncInfo { Name = "send_omen",       Hint = "big faith surge (+0.15)",    ArgCount = 0 },
            new EditorFuncInfo { Name = "set_weather",     Hint = "0=clear 1=rain 2=storm 3=drought", ArgCount = 1 },
            new EditorFuncInfo { Name = "build_chapel",    Hint = "place chapel at slot (2 wood)", ArgCount = 1 },
            new EditorFuncInfo { Name = "build_house",     Hint = "place house at slot (1 wood)",  ArgCount = 1 },
            new EditorFuncInfo { Name = "harvest_tree",    Hint = "chop tree for +1 wood",         ArgCount = 1 },
            new EditorFuncInfo { Name = "expand_building", Hint = "grow building +1 tile (1 wood)", ArgCount = 1 },
            new EditorFuncInfo { Name = "shrink_building", Hint = "shrink building -1 tile (+1 wood)", ArgCount = 1 },
            new EditorFuncInfo { Name = "get_wood",        Hint = "current wood stockpile",        ArgCount = 0 },
            new EditorFuncInfo { Name = "get_trees",       Hint = "tree count on surface",         ArgCount = 0 },
        };

        public List<EditorMethodInfo> GetMethodsForType(string typeName) => new();

        public List<string> GetVariableNameSuggestions() => new()
        {
            "faith", "pop", "disease", "crime", "heretics", "weather",
            "sewer", "births", "deaths", "houses", "chapels", "farms",
            "wood", "trees"
        };

        public List<string> GetStringLiteralSuggestions() => new();
    }
}
