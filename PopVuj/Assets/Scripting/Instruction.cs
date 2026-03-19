// SeaRäuber — Ship Code Interpreter
// Style-transferred from BitNaughts SatelliteCode (MIT License)
using System;

namespace SeaRauber.Scripting
{
    /// <summary>
    /// Machine-level operation codes for ship's onboard computer.
    /// Minimal RISC-like instruction set. Chart room tier gates which opcodes compile.
    /// </summary>
    public enum OpCode
    {
        // ═══════════════════════════════════════════════════════════════
        // DATA MOVEMENT
        // ═══════════════════════════════════════════════════════════════
        
        /// <summary>Load immediate value into register. Args: [destReg, value]</summary>
        LOAD_CONST,
        
        /// <summary>Load from memory address into register. Args: [destReg, address]</summary>
        LOAD_MEM,
        
        /// <summary>Store register value to memory address. Args: [srcReg, address]</summary>
        STORE_MEM,
        
        /// <summary>Copy register to register. Args: [destReg, srcReg]</summary>
        MOV,
        
        // ═══════════════════════════════════════════════════════════════
        // ARITHMETIC
        // ═══════════════════════════════════════════════════════════════
        
        /// <summary>Add two registers, store in first. Args: [destReg, srcReg]</summary>
        ADD,
        
        /// <summary>Subtract second from first, store in first. Args: [destReg, srcReg]</summary>
        SUB,
        
        /// <summary>Multiply two registers, store in first. Args: [destReg, srcReg]</summary>
        MUL,
        
        /// <summary>Divide first by second, store in first. Args: [destReg, srcReg]</summary>
        DIV,
        
        /// <summary>Modulo first by second, store in first. Args: [destReg, srcReg]</summary>
        MOD,
        
        /// <summary>Increment register by 1. Args: [reg]</summary>
        INC,
        
        /// <summary>Decrement register by 1. Args: [reg]</summary>
        DEC,
        
        // ═══════════════════════════════════════════════════════════════
        // COMPARISON & CONTROL FLOW
        // ═══════════════════════════════════════════════════════════════
        
        /// <summary>Compare two registers, set flags. Args: [reg1, reg2]</summary>
        CMP,
        
        /// <summary>Unconditional jump. Args: [address]</summary>
        JMP,
        
        /// <summary>Jump if equal (zero flag set). Args: [address]</summary>
        JEQ,
        
        /// <summary>Jump if not equal. Args: [address]</summary>
        JNE,
        
        /// <summary>Jump if less than. Args: [address]</summary>
        JLT,
        
        /// <summary>Jump if greater than. Args: [address]</summary>
        JGT,
        
        /// <summary>Jump if less than or equal. Args: [address]</summary>
        JLE,
        
        /// <summary>Jump if greater than or equal. Args: [address]</summary>
        JGE,
        
        // ═══════════════════════════════════════════════════════════════
        // STACK OPERATIONS
        // ═══════════════════════════════════════════════════════════════
        
        /// <summary>Push register onto stack. Args: [reg]</summary>
        PUSH,
        
        /// <summary>Pop from stack into register. Args: [reg]</summary>
        POP,
        
        /// <summary>Call subroutine (push PC, jump). Args: [address]</summary>
        CALL,
        
        /// <summary>Return from subroutine (pop PC). Args: none</summary>
        RET,
        
        // ═══════════════════════════════════════════════════════════════
        // SHIP I/O — Crew executes these
        // ═══════════════════════════════════════════════════════════════
        
        /// <summary>Wait for simulation time. Args: [durationReg]</summary>
        WAIT,
        
        /// <summary>Query ship state (wind, position, heading, etc). Args: [destReg, queryId]</summary>
        QUERY,
        
        /// <summary>Issue order to crew (set_heading, trim_sails, fire, etc). Args: [orderId, valueReg]</summary>
        ORDER,
        
        /// <summary>Signal another ship (flags, lanterns). Args: [srcReg, channelId]</summary>
        SIGNAL,
        
        /// <summary>Write entry to ship's log. Args: [srcReg, categoryId]</summary>
        LOG,

        // ═══════════════════════════════════════════════════════════════
        // INTER-PROGRAM MESSAGING
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Send message to another program. Args: [targetId, valueReg]</summary>
        MSG_SEND,

        /// <summary>Receive next message into register. Args: [destReg] (0 if empty)</summary>
        MSG_RECV,

        /// <summary>Peek inbox: 1 if messages waiting, 0 if empty. Args: [destReg]</summary>
        MSG_PEEK,
        
        // ═══════════════════════════════════════════════════════════════
        // SYSTEM
        // ═══════════════════════════════════════════════════════════════
        
        /// <summary>No operation. Args: none</summary>
        NOP,
        
        /// <summary>Halt execution. Args: none</summary>
        HALT,
        
        /// <summary>Debug breakpoint. Args: none</summary>
        BREAK
    }

    /// <summary>
    /// CPU flags for conditional operations.
    /// </summary>
    [Flags]
    public enum CpuFlags
    {
        None = 0,
        Zero = 1,
        Negative = 2,
        Carry = 4,
        Overflow = 8
    }

    /// <summary>
    /// Crew roles that execute ship I/O instructions.
    /// No crew in role → instruction queues, degrades, or fails.
    /// </summary>
    public enum CrewRole
    {
        None,           // No crew needed (data ops, control flow)
        Helmsman,       // set_heading, tack, jibe
        Rigger,         // set_sail_trim, reef_sails
        Gunner,         // fire_broadside, load_cannon
        Navigator,      // plot_course, get_position, get_heading
        Lookout,        // scan_horizon, identify_ship
        Carpenter,      // repair_hull, build_module
        CraneOperator,  // crane_transfer, lay_keel
        Bosun,          // crew_morale, provisions, assign_crew
        Signalman,      // signal flags, lanterns
        Cook            // (passive — provisions efficiency)
    }

    /// <summary>
    /// Chart room tiers gate which code constructs the compiler accepts.
    /// </summary>
    public enum ChartRoomTier
    {
        /// <summary>ON/OFF, IF/THEN only. 1-5 lines. Sloop.</summary>
        CompassAndLog = 0,
        
        /// <summary>+ WHILE loops, timers. 5-15 lines.</summary>
        ChartTable = 1,
        
        /// <summary>+ FOR loops, subroutines (CALL/RET). 15-50 lines.</summary>
        NavigatorsOffice = 2,
        
        /// <summary>+ Functions, state machines, multi-component. Unlimited.</summary>
        WarRoom = 3,
        
        /// <summary>Full Python subset, fleet-level operations, async.</summary>
        AdmiralsBridge = 4
    }

    /// <summary>
    /// Ship I/O query IDs — what you can ask the ship.
    /// </summary>
    public enum QueryId
    {
        WindDirection = 0,
        WindSpeed = 1,
        Heading = 2,
        Speed = 3,
        PositionX = 4,
        PositionZ = 5,
        WaveHeight = 6,
        FoldAmount = 7,
        Provisions = 8,
        CrewCount = 9,
        CrewMorale = 10,
        HullIntegrity = 11,
        CargoFree = 12,
        SailTrim = 13,
        ApparentWindAngle = 14,
        ApparentWindSpeed = 15,

        // ═══ PATHFINDER (A* visualization via code interpreter) ═══
        // These let ShipProgram code drive A* step-by-step.
        // The meta-game: players WATCH their own pathfinding code run.

        /// <summary>Number of nodes in the open set (frontier).</summary>
        PathOpenSize = 100,

        /// <summary>X coord of current node being expanded.</summary>
        PathCurrentX = 101,

        /// <summary>Y coord of current node being expanded.</summary>
        PathCurrentY = 102,

        /// <summary>Layer of current node being expanded.</summary>
        PathCurrentLayer = 103,

        /// <summary>1.0 if current == goal, 0.0 otherwise.</summary>
        PathIsGoal = 104,

        /// <summary>Number of walkable neighbors of current node.</summary>
        PathNeighborCount = 105,

        /// <summary>g-cost of current node.</summary>
        PathGCost = 106,

        /// <summary>f-cost of current node.</summary>
        PathFCost = 107,

        /// <summary>Total nodes explored so far.</summary>
        PathExploredCount = 108,

        /// <summary>Length of found path (0 if not found yet).</summary>
        PathResultLength = 109,

        /// <summary>Search status: 0=idle, 1=searching, 2=found, 3=nopath.</summary>
        PathStatus = 110,
    }

    /// <summary>
    /// Ship I/O order IDs — what you can tell the crew to do.
    /// </summary>
    public enum OrderId
    {
        SetHeading = 0,
        SetSailTrim = 1,
        Tack = 2,
        Jibe = 3,
        ReefSails = 4,
        ClearForAction = 5,
        FireBroadside = 6,
        RepairHull = 7,
        CraneTransfer = 8,
        LayKeel = 9,
        DropAnchor = 10,
        WeighAnchor = 11,
        RingBell = 12,          // The ship's bell (replaces satellite beep)
        SoundHorn = 13,

        // ═══ PATHFINDER (A* visualization via code interpreter) ═══

        /// <summary>Begin A* search. Args: startX, startY, goalX, goalY (in registers).</summary>
        PathBegin = 100,

        /// <summary>Execute one A* step (pop best, expand neighbors). The money shot.</summary>
        PathStep = 101,

        /// <summary>Execute N steps. Arg: count register.</summary>
        PathStepN = 102,

        /// <summary>Reconstruct path (call after PathIsGoal == 1).</summary>
        PathReconstruct = 103,

        /// <summary>Move the crew agent along the found path.</summary>
        PathMoveCrew = 104,

        /// <summary>Set target layer for search. Arg: layer register.</summary>
        PathSetLayer = 105,
    }

    /// <summary>
    /// A single machine instruction with operands.
    /// Immutable struct for efficient execution.
    /// </summary>
    public readonly struct Instruction
    {
        public readonly OpCode Op;
        public readonly int Arg0;
        public readonly int Arg1;
        public readonly int Arg2;
        
        /// <summary>Source line number in original Python code (for Ship's Log)</summary>
        public readonly int SourceLine;
        
        /// <summary>Human-readable description of this instruction</summary>
        public readonly string Comment;
        
        /// <summary>Which crew role must execute this instruction (None for pure compute)</summary>
        public readonly CrewRole RequiredCrew;

        public Instruction(OpCode op, int arg0 = 0, int arg1 = 0, int arg2 = 0,
                          int sourceLine = -1, string comment = null, 
                          CrewRole crew = CrewRole.None)
        {
            Op = op;
            Arg0 = arg0;
            Arg1 = arg1;
            Arg2 = arg2;
            SourceLine = sourceLine;
            Comment = comment;
            RequiredCrew = crew;
        }

        public string ToAssembly()
        {
            return Op switch
            {
                OpCode.LOAD_CONST => $"LOAD_CONST R{Arg0}, {Arg1}",
                OpCode.LOAD_MEM => $"LOAD_MEM R{Arg0}, [{Arg1}]",
                OpCode.STORE_MEM => $"STORE_MEM [{Arg1}], R{Arg0}",
                OpCode.MOV => $"MOV R{Arg0}, R{Arg1}",
                OpCode.ADD => $"ADD R{Arg0}, R{Arg1}",
                OpCode.SUB => $"SUB R{Arg0}, R{Arg1}",
                OpCode.MUL => $"MUL R{Arg0}, R{Arg1}",
                OpCode.DIV => $"DIV R{Arg0}, R{Arg1}",
                OpCode.MOD => $"MOD R{Arg0}, R{Arg1}",
                OpCode.INC => $"INC R{Arg0}",
                OpCode.DEC => $"DEC R{Arg0}",
                OpCode.CMP => $"CMP R{Arg0}, R{Arg1}",
                OpCode.JMP => $"JMP @{Arg0}",
                OpCode.JEQ => $"JEQ @{Arg0}",
                OpCode.JNE => $"JNE @{Arg0}",
                OpCode.JLT => $"JLT @{Arg0}",
                OpCode.JGT => $"JGT @{Arg0}",
                OpCode.JLE => $"JLE @{Arg0}",
                OpCode.JGE => $"JGE @{Arg0}",
                OpCode.PUSH => $"PUSH R{Arg0}",
                OpCode.POP => $"POP R{Arg0}",
                OpCode.CALL => $"CALL @{Arg0}",
                OpCode.RET => "RET",
                OpCode.WAIT => $"WAIT R{Arg0}",
                OpCode.QUERY => $"QUERY R{Arg0}, #{(QueryId)Arg1}",
                OpCode.ORDER => $"ORDER #{(OrderId)Arg0}, R{Arg1}",
                OpCode.SIGNAL => $"SIGNAL R{Arg0}, ch{Arg1}",
                OpCode.LOG => $"LOG R{Arg0}, #{Arg1}",
                OpCode.NOP => "NOP",
                OpCode.HALT => "HALT",
                OpCode.BREAK => "BREAK",
                _ => $"??? {Op} {Arg0} {Arg1} {Arg2}"
            };
        }
    }
}
