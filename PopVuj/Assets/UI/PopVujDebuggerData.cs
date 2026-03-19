// Copyright CodeGamified 2025-2026
// MIT License — PopVuj
using System.Collections.Generic;
using UnityEngine;
using CodeGamified.Engine;
using CodeGamified.Engine.Runtime;
using CodeGamified.TUI;
using PopVuj.Scripting;
using static PopVuj.Scripting.PopVujOpCode;

namespace PopVuj.UI
{
    /// <summary>
    /// Adapts a PopVujProgram into the engine's IDebuggerDataSource contract.
    /// Fed to DebuggerSourcePanel, DebuggerMachinePanel, DebuggerStatePanel.
    /// </summary>
    public class PopVujDebuggerData : IDebuggerDataSource
    {
        private readonly PopVujProgram _program;
        private readonly string _label;

        public PopVujDebuggerData(PopVujProgram program, string label = null)
        {
            _program = program;
            _label = label;
        }

        // ── IDebuggerDataSource ─────────────────────────────────

        public string ProgramName => _label ?? _program?.ProgramName ?? "PopVuj";
        public string[] SourceLines => _program?.Program?.SourceLines;
        public bool HasLiveProgram =>
            _program != null && _program.Executor != null && _program.Program != null
            && _program.Program.Instructions != null && _program.Program.Instructions.Length > 0;
        public int PC
        {
            get
            {
                var s = _program?.State;
                if (s == null) return 0;
                return s.LastExecutedPC >= 0 ? s.LastExecutedPC : s.PC;
            }
        }
        public long CycleCount => _program?.State?.CycleCount ?? 0;

        public string StatusString
        {
            get
            {
                if (_program == null || _program.Executor == null)
                    return TUIColors.Dimmed("NO PROGRAM");
                var state = _program.State;
                if (state == null) return TUIColors.Dimmed("NO STATE");
                int instCount = _program.Program?.Instructions?.Length ?? 0;
                return TUIColors.Fg(TUIColors.BrightGreen, $"TICK {instCount} inst");
            }
        }

        public List<string> BuildSourceLines(int pc, int scrollOffset, int maxRows)
        {
            var lines = new List<string>();
            var src = SourceLines;
            if (src == null) return lines;

            int activeLine = -1;
            int activeEnd = -1;
            bool isHalt = false;
            Instruction activeInst = default;
            if (HasLiveProgram && _program.Program.Instructions.Length > 0
                && pc < _program.Program.Instructions.Length)
            {
                activeInst = _program.Program.Instructions[pc];
                activeLine = activeInst.SourceLine - 1;
                isHalt = activeInst.Op == OpCode.HALT;
                if (activeLine >= 0)
                    activeEnd = SourceHighlight.GetContinuationEnd(src, activeLine);
            }

            // Synthetic "while True:" at display row 0
            if (scrollOffset == 0 && lines.Count < maxRows)
            {
                string whileLine = "while True:";
                if (isHalt)
                    lines.Add(TUIColors.Fg(TUIColors.BrightGreen, $"  {TUIGlyphs.ArrowR}   {whileLine}"));
                else
                    lines.Add($"  {TUIColors.Dimmed(TUIGlyphs.ArrowR)}   {SynthwaveHighlighter.Highlight(whileLine)}");
            }

            // Find the ONE line that contains the active token
            int tokenLine = -1;
            if (activeLine >= 0)
            {
                string token = SourceHighlight.GetSourceToken(activeInst);
                if (token != null)
                {
                    for (int k = activeLine; k <= activeEnd; k++)
                    {
                        if (src[k].IndexOf(token) >= 0) { tokenLine = k; break; }
                    }
                }
                if (tokenLine < 0) tokenLine = activeLine;
            }

            // Auto-scroll to keep active source line visible
            int focusLine = tokenLine >= 0 ? tokenLine : activeLine;
            if (focusLine >= 0 && src.Length > maxRows)
                scrollOffset = Mathf.Clamp(focusLine - maxRows / 3, 0, src.Length - maxRows);

            for (int i = scrollOffset; i < src.Length && lines.Count < maxRows; i++)
            {
                if (i == tokenLine)
                {
                    lines.Add(SourceHighlight.HighlightActiveLine(
                        src[i], $" {i + 1:D3}      ", activeInst));
                }
                else
                {
                    string num = TUIColors.Dimmed($"{i + 1:D3}");
                    lines.Add($" {num}      {SynthwaveHighlighter.Highlight(src[i])}");
                }
            }
            return lines;
        }

        public List<string> BuildMachineLines(int pc, int maxRows)
        {
            var lines = new List<string>();
            if (!HasLiveProgram) return lines;

            var instructions = _program.Program.Instructions;
            int total = instructions.Length;

            int offset = 0;
            if (total > maxRows)
                offset = Mathf.Clamp(pc - maxRows / 3, 0, total - maxRows);
            int visibleCount = Mathf.Min(maxRows, total);

            for (int j = 0; j < visibleCount; j++)
            {
                int i = offset + j;
                var inst = instructions[i];
                bool isPC = (i == pc);
                string asm = inst.ToAssembly(FormatPopVujOp);
                if (isPC)
                {
                    lines.Add(TUIColors.Fg(TUIColors.BrightGreen, $" {i:X3}  {asm}"));
                }
                else
                {
                    string addr = TUIColors.Dimmed($"{i:X3}");
                    lines.Add($" {addr}  {SynthwaveHighlighter.HighlightAsm(asm)}");
                }
            }
            return lines;
        }

        public List<string> BuildStateLines()
        {
            if (!HasLiveProgram) return new List<string>();
            var s = _program.State;
            int displayPC = s.LastExecutedPC >= 0 ? s.LastExecutedPC : s.PC;
            return TUIWidgets.BuildStateLines(
                s.Registers, s.LastRegisterModified,
                s.Flags, displayPC, s.Stack.Count,
                s.NameToAddress, s.Memory);
        }

        // ── Custom opcode formatting ────────────────────────────

        static string FormatPopVujOp(Instruction inst)
        {
            int id = (int)inst.Op - (int)OpCode.CUSTOM_0;
            return (PopVujOpCode)id switch
            {
                // Queries (no args → R0)
                GET_POPULATION    => "INP R0, POP",
                GET_FAITH         => "INP R0, FAITH",
                GET_DISEASE       => "INP R0, DISEASE",
                GET_CRIME         => "INP R0, CRIME",
                GET_SEWER_POP     => "INP R0, SEWER",
                GET_WEATHER       => "INP R0, WEATHER",
                GET_CITY_W        => "INP R0, CITY_W",
                GET_SCORE         => "INP R0, SCORE",
                GET_GAME_OVER     => "INP R0, GAMEOVER",
                GET_INPUT         => "INP R0, INPUT",
                GET_HERETICS      => "INP R0, HERETICS",
                GET_BIRTHS        => "INP R0, BIRTHS",
                GET_DEATHS        => "INP R0, DEATHS",
                GET_HOUSES        => "INP R0, HOUSES",
                GET_CHAPELS       => "INP R0, CHAPELS",
                GET_FARMS         => "INP R0, FARMS",
                GET_WORKSHOPS     => "INP R0, WORKSHOPS",
                GET_WOOD          => "INP R0, WOOD",
                GET_TREES         => "INP R0, TREES",

                // One-arg queries (R0 → R0)
                GET_CELL          => "INP R0, CELL(R0)",
                GET_SEWER_CELL    => "INP R0, SEWER(R0)",

                // Commands
                SEND_PROPHET      => "OUT PROPHET",
                SMITE             => "OUT SMITE",
                SET_WEATHER       => "OUT WEATHER, R0",
                SUMMON_BEARS      => "OUT BEARS",
                SEND_OMEN         => "OUT OMEN",
                BUILD_CHAPEL      => "OUT CHAPEL, R0",
                BUILD_HOUSE       => "OUT HOUSE, R0",
                HARVEST_TREE      => "OUT HARVEST, R0",
                EXPAND_BUILDING   => "OUT EXPAND, R0",
                SHRINK_BUILDING   => "OUT SHRINK, R0",

                _                 => $"IO.{id,2} {inst.Arg0}, {inst.Arg1}"
            };
        }
    }
}
