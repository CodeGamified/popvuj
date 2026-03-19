// SeaRäuber — Python Compiler (Ship Edition)
// Style-transferred from BitNaughts SatelliteCode (MIT License)
// Compiles Python subset → ship machine instructions.
// Chart room tier gates which constructs are allowed.
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace SeaRauber.Scripting
{
    /// <summary>
    /// Python-to-bytecode compiler for ship programs.
    /// Chart room tier gates allowed constructs:
    ///   CompassAndLog:     assignment, if/else, bell(), wait()
    ///   ChartTable:        + while loops
    ///   NavigatorsOffice:  + for, subroutines (call/ret)
    ///   WarRoom:           + functions, multiple crew types
    ///   AdmiralsBridge:    full subset, fleet ops
    /// </summary>
    public class PythonCompiler
    {
        // ═══════════════════════════════════════════════════════════════
        // AST NODE TYPES (structure identical to BitNaughts)
        // ═══════════════════════════════════════════════════════════════
        
        public abstract class AstNode
        {
            public int SourceLine;
            public abstract void Compile(CompilerContext ctx);
        }
        
        public class ProgramNode : AstNode
        {
            public List<AstNode> Statements = new List<AstNode>();
            public override void Compile(CompilerContext ctx)
            {
                foreach (var stmt in Statements) stmt.Compile(ctx);
            }
        }
        
        public class WhileNode : AstNode
        {
            public ExprNode Condition;
            public List<AstNode> Body = new List<AstNode>();
            public bool IsInfinite;
            
            public override void Compile(CompilerContext ctx)
            {
                ctx.RequireTier(ChartRoomTier.ChartTable, SourceLine, "while loops");
                
                int loopStart = ctx.CurrentAddress;
                
                if (!IsInfinite)
                {
                    Condition.Compile(ctx, 0);
                    ctx.Emit(OpCode.LOAD_CONST, 1, 0, sourceLine: SourceLine, comment: "load 0 for comparison");
                    ctx.Emit(OpCode.CMP, 0, 1, sourceLine: SourceLine, comment: "test condition");
                    int jumpPatch = ctx.CurrentAddress;
                    ctx.Emit(OpCode.JEQ, 0, sourceLine: SourceLine, comment: "exit loop if false");
                    
                    foreach (var stmt in Body) stmt.Compile(ctx);
                    ctx.Emit(OpCode.JMP, loopStart, sourceLine: SourceLine, comment: "loop back");
                    ctx.PatchJump(jumpPatch, ctx.CurrentAddress);
                }
                else
                {
                    foreach (var stmt in Body) stmt.Compile(ctx);
                    ctx.Emit(OpCode.JMP, loopStart, sourceLine: SourceLine, comment: "loop back");
                }
            }
        }
        
        public class AssignNode : AstNode
        {
            public string VarName;
            public ExprNode Value;
            public override void Compile(CompilerContext ctx)
            {
                Value.Compile(ctx, 0);
                int addr = ctx.GetVariableAddress(VarName);
                ctx.Emit(OpCode.STORE_MEM, 0, addr, sourceLine: SourceLine, comment: $"store to {VarName}");
            }
        }
        
        public class AssignFromMethodNode : AstNode
        {
            public string VarName;
            public string ObjectName;
            public string MethodName;
            public List<ExprNode> Args = new List<ExprNode>();
            
            public override void Compile(CompilerContext ctx)
            {
                var methodCall = new MethodCallNode
                {
                    SourceLine = SourceLine,
                    ObjectName = ObjectName,
                    MethodName = MethodName,
                    Args = Args
                };
                methodCall.Compile(ctx);
                int addr = ctx.GetVariableAddress(VarName);
                ctx.Emit(OpCode.STORE_MEM, 0, addr, sourceLine: SourceLine, comment: $"store to {VarName}");
            }
        }
        
        /// <summary>
        /// Standalone function call: bell(), wait(3), etc.
        /// </summary>
        public class CallNode : AstNode
        {
            public string FunctionName;
            public List<ExprNode> Args = new List<ExprNode>();
            
            public override void Compile(CompilerContext ctx)
            {
                switch (FunctionName.ToLower())
                {
                    case "bell":
                        ctx.Emit(OpCode.LOAD_CONST, 0, 1000, sourceLine: SourceLine, comment: "bell value = 1");
                        ctx.Emit(OpCode.ORDER, (int)OrderId.RingBell, 0, sourceLine: SourceLine,
                                comment: "ring ship's bell", crew: CrewRole.None);
                        break;
                    
                    case "horn":
                        ctx.Emit(OpCode.LOAD_CONST, 0, 1000, sourceLine: SourceLine, comment: "horn value = 1");
                        ctx.Emit(OpCode.ORDER, (int)OrderId.SoundHorn, 0, sourceLine: SourceLine,
                                comment: "sound horn", crew: CrewRole.None);
                        break;
                    
                    case "wait":
                        if (Args.Count > 0)
                        {
                            Args[0].Compile(ctx, 0);
                            ctx.Emit(OpCode.WAIT, 0, sourceLine: SourceLine, comment: "wait R0 seconds");
                        }
                        break;
                    
                    case "log":
                        if (Args.Count > 0)
                        {
                            Args[0].Compile(ctx, 0);
                            int category = Args.Count > 1 ? (int)((NumberNode)Args[1]).Value : 0;
                            ctx.Emit(OpCode.LOG, 0, category, sourceLine: SourceLine,
                                    comment: $"write to ship's log");
                        }
                        break;
                    
                    case "get_wind":
                        ctx.Emit(OpCode.QUERY, 0, (int)QueryId.WindDirection, sourceLine: SourceLine,
                                comment: "query wind direction", crew: CrewRole.Navigator);
                        break;
                    
                    case "get_wind_speed":
                        ctx.Emit(OpCode.QUERY, 0, (int)QueryId.WindSpeed, sourceLine: SourceLine,
                                comment: "query wind speed", crew: CrewRole.Navigator);
                        break;
                    
                    case "get_heading":
                        ctx.Emit(OpCode.QUERY, 0, (int)QueryId.Heading, sourceLine: SourceLine,
                                comment: "query heading", crew: CrewRole.Navigator);
                        break;
                    
                    case "get_speed":
                        ctx.Emit(OpCode.QUERY, 0, (int)QueryId.Speed, sourceLine: SourceLine,
                                comment: "query speed", crew: CrewRole.Navigator);
                        break;
                    
                    case "get_provisions":
                        ctx.Emit(OpCode.QUERY, 0, (int)QueryId.Provisions, sourceLine: SourceLine,
                                comment: "query provisions", crew: CrewRole.Bosun);
                        break;
                    
                    case "scan_horizon":
                        ctx.Emit(OpCode.QUERY, 0, (int)QueryId.WaveHeight, sourceLine: SourceLine,
                                comment: "scan horizon", crew: CrewRole.Lookout);
                        break;
                    
                    default:
                        Debug.LogWarning($"[Compiler] Unknown function: {FunctionName}");
                        break;
                }
            }
        }
        
        /// <summary>
        /// Method call on a crew component: helm.set_heading(90), rigging.trim(0.8), etc.
        /// </summary>
        public class MethodCallNode : AstNode
        {
            public string ObjectName;
            public string MethodName;
            public List<ExprNode> Args = new List<ExprNode>();
            
            public override void Compile(CompilerContext ctx)
            {
                string crewType = ctx.GetObjectType(ObjectName);
                string key = $"{crewType}.{MethodName}".ToLower();
                
                switch (key)
                {
                    // ═══ HELM ═══
                    case "helm.set_heading":
                        if (Args.Count > 0) Args[0].Compile(ctx, 1);
                        ctx.Emit(OpCode.ORDER, (int)OrderId.SetHeading, 1, sourceLine: SourceLine,
                                comment: $"{ObjectName}.set_heading()", crew: CrewRole.Helmsman);
                        break;
                    
                    case "helm.tack":
                        ctx.Emit(OpCode.ORDER, (int)OrderId.Tack, 0, sourceLine: SourceLine,
                                comment: $"{ObjectName}.tack()", crew: CrewRole.Helmsman);
                        break;
                    
                    case "helm.jibe":
                        ctx.Emit(OpCode.ORDER, (int)OrderId.Jibe, 0, sourceLine: SourceLine,
                                comment: $"{ObjectName}.jibe()", crew: CrewRole.Helmsman);
                        break;
                    
                    case "helm.get_heading":
                        ctx.Emit(OpCode.QUERY, 0, (int)QueryId.Heading, sourceLine: SourceLine,
                                comment: $"{ObjectName}.get_heading() → R0", crew: CrewRole.Navigator);
                        break;
                    
                    // ═══ RIGGING ═══
                    case "rigging.trim":
                    case "rigging.set_trim":
                        if (Args.Count > 0) Args[0].Compile(ctx, 1);
                        ctx.Emit(OpCode.ORDER, (int)OrderId.SetSailTrim, 1, sourceLine: SourceLine,
                                comment: $"{ObjectName}.trim()", crew: CrewRole.Rigger);
                        break;
                    
                    case "rigging.reef":
                        ctx.Emit(OpCode.ORDER, (int)OrderId.ReefSails, 0, sourceLine: SourceLine,
                                comment: $"{ObjectName}.reef()", crew: CrewRole.Rigger);
                        break;
                    
                    // ═══ LOOKOUT ═══
                    case "lookout.scan":
                    case "lookout.scan_horizon":
                        ctx.Emit(OpCode.QUERY, 0, (int)QueryId.WaveHeight, sourceLine: SourceLine,
                                comment: $"{ObjectName}.scan() → R0", crew: CrewRole.Lookout);
                        break;
                    
                    // ═══ NAVIGATOR ═══
                    case "navigator.get_wind":
                    case "navigator.wind":
                        ctx.Emit(OpCode.QUERY, 0, (int)QueryId.WindDirection, sourceLine: SourceLine,
                                comment: $"{ObjectName}.get_wind() → R0", crew: CrewRole.Navigator);
                        break;
                    
                    case "navigator.wind_speed":
                        ctx.Emit(OpCode.QUERY, 0, (int)QueryId.WindSpeed, sourceLine: SourceLine,
                                comment: $"{ObjectName}.wind_speed() → R0", crew: CrewRole.Navigator);
                        break;
                    
                    case "navigator.position_x":
                        ctx.Emit(OpCode.QUERY, 0, (int)QueryId.PositionX, sourceLine: SourceLine,
                                comment: $"{ObjectName}.position_x() → R0", crew: CrewRole.Navigator);
                        break;
                    
                    case "navigator.position_z":
                        ctx.Emit(OpCode.QUERY, 0, (int)QueryId.PositionZ, sourceLine: SourceLine,
                                comment: $"{ObjectName}.position_z() → R0", crew: CrewRole.Navigator);
                        break;
                    
                    case "navigator.speed":
                        ctx.Emit(OpCode.QUERY, 0, (int)QueryId.Speed, sourceLine: SourceLine,
                                comment: $"{ObjectName}.speed() → R0", crew: CrewRole.Navigator);
                        break;
                    
                    // ═══ GUNNER ═══
                    case "gunner.fire":
                    case "gunner.fire_broadside":
                        ctx.RequireTier(ChartRoomTier.WarRoom, SourceLine, "combat operations");
                        ctx.Emit(OpCode.ORDER, (int)OrderId.FireBroadside, 0, sourceLine: SourceLine,
                                comment: $"{ObjectName}.fire()", crew: CrewRole.Gunner);
                        break;
                    
                    // ═══ CARPENTER ═══
                    case "carpenter.repair":
                        ctx.Emit(OpCode.ORDER, (int)OrderId.RepairHull, 0, sourceLine: SourceLine,
                                comment: $"{ObjectName}.repair()", crew: CrewRole.Carpenter);
                        break;
                    
                    // ═══ CRANE ═══
                    case "crane.transfer":
                        ctx.RequireTier(ChartRoomTier.NavigatorsOffice, SourceLine, "crane operations");
                        if (Args.Count > 0) Args[0].Compile(ctx, 1);
                        ctx.Emit(OpCode.ORDER, (int)OrderId.CraneTransfer, 1, sourceLine: SourceLine,
                                comment: $"{ObjectName}.transfer()", crew: CrewRole.CraneOperator);
                        break;
                    
                    case "crane.lay_keel":
                        ctx.RequireTier(ChartRoomTier.WarRoom, SourceLine, "ship construction");
                        if (Args.Count > 0) Args[0].Compile(ctx, 1);
                        ctx.Emit(OpCode.ORDER, (int)OrderId.LayKeel, 1, sourceLine: SourceLine,
                                comment: $"{ObjectName}.lay_keel()", crew: CrewRole.CraneOperator);
                        break;
                    
                    // ═══ BOSUN ═══
                    case "bosun.provisions":
                        ctx.Emit(OpCode.QUERY, 0, (int)QueryId.Provisions, sourceLine: SourceLine,
                                comment: $"{ObjectName}.provisions() → R0", crew: CrewRole.Bosun);
                        break;
                    
                    case "bosun.morale":
                        ctx.Emit(OpCode.QUERY, 0, (int)QueryId.CrewMorale, sourceLine: SourceLine,
                                comment: $"{ObjectName}.morale() → R0", crew: CrewRole.Bosun);
                        break;
                    
                    case "bosun.crew_count":
                        ctx.Emit(OpCode.QUERY, 0, (int)QueryId.CrewCount, sourceLine: SourceLine,
                                comment: $"{ObjectName}.crew_count() → R0", crew: CrewRole.Bosun);
                        break;
                    
                    // ═══ SIGNAL ═══
                    case "signal.send":
                    case "signal.flag":
                        if (Args.Count > 0) Args[0].Compile(ctx, 0);
                        int channel = ctx.GetObjectChannel(ObjectName);
                        ctx.Emit(OpCode.SIGNAL, 0, channel, sourceLine: SourceLine,
                                comment: $"{ObjectName}.send()", crew: CrewRole.Signalman);
                        break;

                    // ═══ PATHFINDER (A* meta-game) ═══
                    // Navigator can run A* on the deck grid.
                    // The code IS the algorithm. CodeTerminal + PathVisualizer
                    // show the same state from two perspectives.

                    case "pathfinder.begin":
                        // pathfinder.begin(startX, startY, goalX, goalY)
                        if (Args.Count >= 4) { Args[0].Compile(ctx, 0); Args[1].Compile(ctx, 1); Args[2].Compile(ctx, 2); Args[3].Compile(ctx, 3); }
                        ctx.Emit(OpCode.ORDER, (int)OrderId.PathBegin, 0, sourceLine: SourceLine,
                                comment: $"{ObjectName}.begin()", crew: CrewRole.Navigator);
                        break;

                    case "pathfinder.step":
                        // pathfinder.step() — one A* iteration
                        ctx.Emit(OpCode.ORDER, (int)OrderId.PathStep, 0, sourceLine: SourceLine,
                                comment: $"{ObjectName}.step() — expand best node", crew: CrewRole.Navigator);
                        break;

                    case "pathfinder.step_n":
                        // pathfinder.step_n(count)
                        if (Args.Count > 0) Args[0].Compile(ctx, 1);
                        ctx.Emit(OpCode.ORDER, (int)OrderId.PathStepN, 1, sourceLine: SourceLine,
                                comment: $"{ObjectName}.step_n()", crew: CrewRole.Navigator);
                        break;

                    case "pathfinder.reconstruct":
                        ctx.Emit(OpCode.ORDER, (int)OrderId.PathReconstruct, 0, sourceLine: SourceLine,
                                comment: $"{ObjectName}.reconstruct() — build path", crew: CrewRole.Navigator);
                        break;

                    case "pathfinder.move_crew":
                        // pathfinder.move_crew(crew_index)
                        if (Args.Count > 0) Args[0].Compile(ctx, 1);
                        ctx.Emit(OpCode.ORDER, (int)OrderId.PathMoveCrew, 1, sourceLine: SourceLine,
                                comment: $"{ObjectName}.move_crew()", crew: CrewRole.Navigator);
                        break;

                    case "pathfinder.set_layer":
                        if (Args.Count > 0) Args[0].Compile(ctx, 1);
                        ctx.Emit(OpCode.ORDER, (int)OrderId.PathSetLayer, 1, sourceLine: SourceLine,
                                comment: $"{ObjectName}.set_layer()", crew: CrewRole.Navigator);
                        break;

                    case "pathfinder.open_size":
                        ctx.Emit(OpCode.QUERY, 0, (int)QueryId.PathOpenSize, sourceLine: SourceLine,
                                comment: $"{ObjectName}.open_size() → R0", crew: CrewRole.Navigator);
                        break;

                    case "pathfinder.is_goal":
                        ctx.Emit(OpCode.QUERY, 0, (int)QueryId.PathIsGoal, sourceLine: SourceLine,
                                comment: $"{ObjectName}.is_goal() → R0", crew: CrewRole.Navigator);
                        break;

                    case "pathfinder.neighbor_count":
                        ctx.Emit(OpCode.QUERY, 0, (int)QueryId.PathNeighborCount, sourceLine: SourceLine,
                                comment: $"{ObjectName}.neighbor_count() → R0", crew: CrewRole.Navigator);
                        break;

                    case "pathfinder.g_cost":
                        ctx.Emit(OpCode.QUERY, 0, (int)QueryId.PathGCost, sourceLine: SourceLine,
                                comment: $"{ObjectName}.g_cost() → R0", crew: CrewRole.Navigator);
                        break;

                    case "pathfinder.f_cost":
                        ctx.Emit(OpCode.QUERY, 0, (int)QueryId.PathFCost, sourceLine: SourceLine,
                                comment: $"{ObjectName}.f_cost() → R0", crew: CrewRole.Navigator);
                        break;

                    case "pathfinder.explored_count":
                        ctx.Emit(OpCode.QUERY, 0, (int)QueryId.PathExploredCount, sourceLine: SourceLine,
                                comment: $"{ObjectName}.explored_count() → R0", crew: CrewRole.Navigator);
                        break;

                    case "pathfinder.path_length":
                        ctx.Emit(OpCode.QUERY, 0, (int)QueryId.PathResultLength, sourceLine: SourceLine,
                                comment: $"{ObjectName}.path_length() → R0", crew: CrewRole.Navigator);
                        break;

                    case "pathfinder.status":
                        ctx.Emit(OpCode.QUERY, 0, (int)QueryId.PathStatus, sourceLine: SourceLine,
                                comment: $"{ObjectName}.status() → R0", crew: CrewRole.Navigator);
                        break;
                    
                    default:
                        Debug.LogWarning($"[Compiler] Unknown method: {ObjectName}.{MethodName}() (type: {crewType})");
                        break;
                }
            }
        }
        
        /// <summary>
        /// Crew component instantiation: Helm helm = new Helm()
        /// </summary>
        public class CrewDeclNode : AstNode
        {
            public string TypeName;
            public string VarName;
            public List<ExprNode> ConstructorArgs = new List<ExprNode>();
            
            public override void Compile(CompilerContext ctx)
            {
                ctx.RegisterObject(VarName, TypeName, ConstructorArgs);
                ctx.Emit(OpCode.NOP, sourceLine: SourceLine, comment: $"crew {TypeName} {VarName}");
            }
        }
        
        public class IfNode : AstNode
        {
            public ExprNode Condition;
            public List<AstNode> ThenBody = new List<AstNode>();
            public List<AstNode> ElseBody = new List<AstNode>();
            
            public override void Compile(CompilerContext ctx)
            {
                Condition.Compile(ctx, 0);
                ctx.Emit(OpCode.LOAD_CONST, 1, 0, sourceLine: SourceLine, comment: "load 0 for comparison");
                ctx.Emit(OpCode.CMP, 0, 1, sourceLine: SourceLine, comment: "test condition");
                
                int jumpToElse = ctx.CurrentAddress;
                ctx.Emit(OpCode.JEQ, 0, sourceLine: SourceLine, comment: "jump to else if false");
                
                foreach (var stmt in ThenBody) stmt.Compile(ctx);
                
                if (ElseBody.Count > 0)
                {
                    int jumpPastElse = ctx.CurrentAddress;
                    ctx.Emit(OpCode.JMP, 0, sourceLine: SourceLine, comment: "jump past else");
                    ctx.PatchJump(jumpToElse, ctx.CurrentAddress);
                    foreach (var stmt in ElseBody) stmt.Compile(ctx);
                    ctx.PatchJump(jumpPastElse, ctx.CurrentAddress);
                }
                else
                {
                    ctx.PatchJump(jumpToElse, ctx.CurrentAddress);
                }
            }
        }
        
        // ═══════════════════════════════════════════════════════════════
        // EXPRESSION NODES (identical to BitNaughts)
        // ═══════════════════════════════════════════════════════════════
        
        public abstract class ExprNode : AstNode
        {
            public abstract void Compile(CompilerContext ctx, int targetReg);
            public override void Compile(CompilerContext ctx) => Compile(ctx, 0);
        }
        
        public class NumberNode : ExprNode
        {
            public float Value;
            public override void Compile(CompilerContext ctx, int targetReg)
            {
                ctx.Emit(OpCode.LOAD_CONST, targetReg, (int)(Value * 1000), sourceLine: SourceLine,
                    comment: $"load {Value} (scaled)");
            }
        }
        
        public class VarNode : ExprNode
        {
            public string Name;
            public override void Compile(CompilerContext ctx, int targetReg)
            {
                int addr = ctx.GetVariableAddress(Name);
                ctx.Emit(OpCode.LOAD_MEM, targetReg, addr, sourceLine: SourceLine, comment: $"load {Name}");
            }
        }
        
        public class BinaryOpNode : ExprNode
        {
            public ExprNode Left;
            public ExprNode Right;
            public string Op;
            
            public override void Compile(CompilerContext ctx, int targetReg)
            {
                Left.Compile(ctx, targetReg);
                Right.Compile(ctx, targetReg + 1);
                
                switch (Op)
                {
                    case "+": ctx.Emit(OpCode.ADD, targetReg, targetReg + 1, sourceLine: SourceLine, comment: "add"); break;
                    case "-": ctx.Emit(OpCode.SUB, targetReg, targetReg + 1, sourceLine: SourceLine, comment: "subtract"); break;
                    case "*": ctx.Emit(OpCode.MUL, targetReg, targetReg + 1, sourceLine: SourceLine, comment: "multiply"); break;
                    case "/": ctx.Emit(OpCode.DIV, targetReg, targetReg + 1, sourceLine: SourceLine, comment: "divide"); break;
                    case "%": ctx.Emit(OpCode.MOD, targetReg, targetReg + 1, sourceLine: SourceLine, comment: "modulo"); break;
                    case "<": case ">": case "==": case "!=": case "<=": case ">=":
                        ctx.Emit(OpCode.CMP, targetReg, targetReg + 1, sourceLine: SourceLine, comment: $"compare {Op}");
                        EmitCompareResult(ctx, Op, targetReg, SourceLine);
                        break;
                }
            }
            
            private void EmitCompareResult(CompilerContext ctx, string op, int reg, int line)
            {
                switch (op)
                {
                    case "<":
                        ctx.Emit(OpCode.LOAD_CONST, reg, 1, sourceLine: line, comment: "assume true");
                        ctx.Emit(OpCode.JLT, ctx.CurrentAddress + 2, sourceLine: line, comment: "skip if less");
                        ctx.Emit(OpCode.LOAD_CONST, reg, 0, sourceLine: line, comment: "was false");
                        break;
                    case ">":
                        ctx.Emit(OpCode.LOAD_CONST, reg, 1, sourceLine: line, comment: "assume true");
                        ctx.Emit(OpCode.JGT, ctx.CurrentAddress + 2, sourceLine: line, comment: "skip if greater");
                        ctx.Emit(OpCode.LOAD_CONST, reg, 0, sourceLine: line, comment: "was false");
                        break;
                    case "==":
                        ctx.Emit(OpCode.LOAD_CONST, reg, 1, sourceLine: line, comment: "assume true");
                        ctx.Emit(OpCode.JEQ, ctx.CurrentAddress + 2, sourceLine: line, comment: "skip if equal");
                        ctx.Emit(OpCode.LOAD_CONST, reg, 0, sourceLine: line, comment: "was false");
                        break;
                    case "!=":
                        ctx.Emit(OpCode.LOAD_CONST, reg, 1, sourceLine: line, comment: "assume true");
                        ctx.Emit(OpCode.JNE, ctx.CurrentAddress + 2, sourceLine: line, comment: "skip if not equal");
                        ctx.Emit(OpCode.LOAD_CONST, reg, 0, sourceLine: line, comment: "was false");
                        break;
                    case "<=":
                        ctx.Emit(OpCode.LOAD_CONST, reg, 1, sourceLine: line, comment: "assume true");
                        ctx.Emit(OpCode.JLE, ctx.CurrentAddress + 2, sourceLine: line, comment: "skip if <=");
                        ctx.Emit(OpCode.LOAD_CONST, reg, 0, sourceLine: line, comment: "was false");
                        break;
                    case ">=":
                        ctx.Emit(OpCode.LOAD_CONST, reg, 1, sourceLine: line, comment: "assume true");
                        ctx.Emit(OpCode.JGE, ctx.CurrentAddress + 2, sourceLine: line, comment: "skip if >=");
                        ctx.Emit(OpCode.LOAD_CONST, reg, 0, sourceLine: line, comment: "was false");
                        break;
                }
            }
        }
        
        public class BoolNode : ExprNode
        {
            public bool Value;
            public override void Compile(CompilerContext ctx, int targetReg)
            {
                ctx.Emit(OpCode.LOAD_CONST, targetReg, Value ? 1 : 0, sourceLine: SourceLine,
                    comment: Value ? "True" : "False");
            }
        }
        
        // ═══════════════════════════════════════════════════════════════
        // COMPILER CONTEXT
        // ═══════════════════════════════════════════════════════════════
        
        public class CompilerContext
        {
            public List<Instruction> Instructions = new List<Instruction>();
            public Dictionary<string, int> Variables = new Dictionary<string, int>();
            private int _nextVarAddress = 0;
            
            /// <summary>Chart room tier — gates which constructs compile.</summary>
            public ChartRoomTier Tier = ChartRoomTier.AdmiralsBridge;
            
            /// <summary>Compile errors from tier violations.</summary>
            public List<string> TierErrors = new List<string>();
            
            // ── OOP crew tracking ──
            public Dictionary<string, string> ObjectTypes = new Dictionary<string, string>();
            public Dictionary<string, int> ObjectChannels = new Dictionary<string, int>();
            private int _nextChannel = 0;
            
            public int CurrentAddress => Instructions.Count;
            
            public void Emit(OpCode op, int arg0 = 0, int arg1 = 0, int arg2 = 0,
                           int sourceLine = -1, string comment = null, CrewRole crew = CrewRole.None)
            {
                Instructions.Add(new Instruction(op, arg0, arg1, arg2, sourceLine, comment, crew));
            }
            
            public int GetVariableAddress(string name)
            {
                if (!Variables.TryGetValue(name, out int addr))
                {
                    addr = _nextVarAddress++;
                    Variables[name] = addr;
                }
                return addr;
            }
            
            public void PatchJump(int instructionIndex, int targetAddress)
            {
                if (instructionIndex >= 0 && instructionIndex < Instructions.Count)
                {
                    var old = Instructions[instructionIndex];
                    Instructions[instructionIndex] = new Instruction(
                        old.Op, targetAddress, old.Arg1, old.Arg2, old.SourceLine, old.Comment, old.RequiredCrew);
                }
            }
            
            /// <summary>
            /// Gate a construct by chart room tier. Logs error if tier too low.
            /// </summary>
            public void RequireTier(ChartRoomTier required, int line, string feature)
            {
                if (Tier < required)
                {
                    string msg = $"Line {line}: '{feature}' requires {required} chart room (current: {Tier})";
                    TierErrors.Add(msg);
                    Debug.LogWarning($"[Compiler] {msg}");
                }
            }
            
            public void RegisterObject(string name, string typeName, List<ExprNode> args)
            {
                ObjectTypes[name] = typeName;
                if (typeName.ToLower() == "signal")
                    ObjectChannels[name] = _nextChannel++;
            }
            
            public string GetObjectType(string name) =>
                ObjectTypes.TryGetValue(name, out string type) ? type : "Unknown";
            
            public int GetObjectChannel(string name) =>
                ObjectChannels.TryGetValue(name, out int ch) ? ch : 0;
        }
        
        // ═══════════════════════════════════════════════════════════════
        // PARSER (structure identical to BitNaughts, crew types added)
        // ═══════════════════════════════════════════════════════════════
        
        private string[] _lines;
        private int _lineIndex;
        
        /// <summary>Known crew component type names for parser recognition.</summary>
        private static readonly HashSet<string> CrewTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Helm", "Rigging", "Lookout", "Navigator", "Gunner",
            "Carpenter", "Crane", "Bosun", "Signal", "Pathfinder"
        };
        
        public ProgramNode Parse(string source)
        {
            _lines = source.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            _lineIndex = 0;
            var program = new ProgramNode();
            program.Statements = ParseBlock(0);
            return program;
        }
        
        private List<AstNode> ParseBlock(int expectedIndent)
        {
            var statements = new List<AstNode>();
            
            while (_lineIndex < _lines.Length)
            {
                string line = _lines[_lineIndex];
                int indent = GetIndent(line);
                string trimmed = line.Trim();
                
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
                {
                    _lineIndex++;
                    continue;
                }
                
                if (indent < expectedIndent) break;
                
                if (indent > expectedIndent)
                {
                    Debug.LogWarning($"[Parser] Unexpected indent at line {_lineIndex + 1}");
                    _lineIndex++;
                    continue;
                }
                
                var stmt = ParseStatement(trimmed, _lineIndex + 1);
                if (stmt != null) statements.Add(stmt);
                
                _lineIndex++;
                
                if (trimmed.EndsWith(":") && stmt != null)
                {
                    var body = ParseBlock(expectedIndent + 4);
                    if (stmt is WhileNode wn) wn.Body = body;
                    else if (stmt is IfNode ifn) ifn.ThenBody = body;
                }
            }
            
            return statements;
        }
        
        private int GetIndent(string line)
        {
            int spaces = 0;
            foreach (char c in line)
            {
                if (c == ' ') spaces++;
                else if (c == '\t') spaces += 4;
                else break;
            }
            return spaces;
        }
        
        private AstNode ParseStatement(string trimmed, int lineNum)
        {
            // while condition:
            var whileMatch = Regex.Match(trimmed, @"^while\s+(.+):$");
            if (whileMatch.Success)
            {
                var cond = whileMatch.Groups[1].Value.Trim();
                return new WhileNode
                {
                    SourceLine = lineNum,
                    IsInfinite = (cond == "True" || cond == "1"),
                    Condition = cond == "True" ? new BoolNode { Value = true, SourceLine = lineNum }
                                               : ParseExpression(cond, lineNum)
                };
            }
            
            // if condition:
            var ifMatch = Regex.Match(trimmed, @"^if\s+(.+):$");
            if (ifMatch.Success)
            {
                return new IfNode
                {
                    SourceLine = lineNum,
                    Condition = ParseExpression(ifMatch.Groups[1].Value.Trim(), lineNum)
                };
            }
            
            // Crew component instantiation: Helm helm = new Helm()
            var crewDeclMatch = Regex.Match(trimmed, @"^(\w+)\s+(\w+)\s*=\s*new\s+(\w+)\((.*)\)$");
            if (crewDeclMatch.Success && CrewTypes.Contains(crewDeclMatch.Groups[1].Value))
            {
                var node = new CrewDeclNode
                {
                    SourceLine = lineNum,
                    TypeName = crewDeclMatch.Groups[1].Value,
                    VarName = crewDeclMatch.Groups[2].Value
                };
                string argsStr = crewDeclMatch.Groups[4].Value.Trim();
                if (!string.IsNullOrEmpty(argsStr))
                    foreach (var arg in argsStr.Split(','))
                        node.ConstructorArgs.Add(ParseExpression(arg.Trim(), lineNum));
                return node;
            }
            
            // Method call: crew.method(args)
            var methodCallMatch = Regex.Match(trimmed, @"^(\w+)\.(\w+)\((.*)\)$");
            if (methodCallMatch.Success)
            {
                var node = new MethodCallNode
                {
                    SourceLine = lineNum,
                    ObjectName = methodCallMatch.Groups[1].Value,
                    MethodName = methodCallMatch.Groups[2].Value
                };
                string argsStr = methodCallMatch.Groups[3].Value.Trim();
                if (!string.IsNullOrEmpty(argsStr))
                    foreach (var arg in argsStr.Split(','))
                        node.Args.Add(ParseExpression(arg.Trim(), lineNum));
                return node;
            }
            
            // Assignment: x = expr or x = crew.method()
            var assignMatch = Regex.Match(trimmed, @"^(\w+)\s*=\s*(.+)$");
            if (assignMatch.Success)
            {
                string varName = assignMatch.Groups[1].Value;
                string valueExpr = assignMatch.Groups[2].Value.Trim();
                
                var rhsMethodMatch = Regex.Match(valueExpr, @"^(\w+)\.(\w+)\((.*)\)$");
                if (rhsMethodMatch.Success)
                {
                    return new AssignFromMethodNode
                    {
                        SourceLine = lineNum,
                        VarName = varName,
                        ObjectName = rhsMethodMatch.Groups[1].Value,
                        MethodName = rhsMethodMatch.Groups[2].Value,
                        Args = ParseArgs(rhsMethodMatch.Groups[3].Value, lineNum)
                    };
                }
                
                return new AssignNode
                {
                    SourceLine = lineNum,
                    VarName = varName,
                    Value = ParseExpression(valueExpr, lineNum)
                };
            }
            
            // Standalone function call: bell(), wait(3)
            var callMatch = Regex.Match(trimmed, @"^(\w+)\((.*)\)$");
            if (callMatch.Success)
            {
                var call = new CallNode
                {
                    SourceLine = lineNum,
                    FunctionName = callMatch.Groups[1].Value
                };
                string argsStr = callMatch.Groups[2].Value.Trim();
                if (!string.IsNullOrEmpty(argsStr))
                    foreach (var arg in argsStr.Split(','))
                        call.Args.Add(ParseExpression(arg.Trim(), lineNum));
                return call;
            }
            
            Debug.LogWarning($"[Parser] Unparseable line {lineNum}: {trimmed}");
            return null;
        }
        
        private List<ExprNode> ParseArgs(string argsStr, int lineNum)
        {
            var args = new List<ExprNode>();
            argsStr = argsStr.Trim();
            if (!string.IsNullOrEmpty(argsStr))
                foreach (var arg in argsStr.Split(','))
                    args.Add(ParseExpression(arg.Trim(), lineNum));
            return args;
        }
        
        private ExprNode ParseExpression(string expr, int lineNum)
        {
            expr = expr.Trim();
            
            if (expr == "True") return new BoolNode { Value = true, SourceLine = lineNum };
            if (expr == "False") return new BoolNode { Value = false, SourceLine = lineNum };
            
            if (float.TryParse(expr, out float num))
                return new NumberNode { Value = num, SourceLine = lineNum };
            
            foreach (string op in new[] { "<=", ">=", "==", "!=", "<", ">", "+", "-", "*", "/", "%" })
            {
                int idx = expr.LastIndexOf(op);
                if (idx > 0 && idx < expr.Length - op.Length)
                {
                    return new BinaryOpNode
                    {
                        SourceLine = lineNum,
                        Left = ParseExpression(expr.Substring(0, idx), lineNum),
                        Right = ParseExpression(expr.Substring(idx + op.Length), lineNum),
                        Op = op
                    };
                }
            }
            
            if (Regex.IsMatch(expr, @"^\w+$"))
                return new VarNode { Name = expr, SourceLine = lineNum };
            
            Debug.LogWarning($"[Parser] Unparseable expression: {expr}");
            return new NumberNode { Value = 0, SourceLine = lineNum };
        }
        
        // ═══════════════════════════════════════════════════════════════
        // PUBLIC API
        // ═══════════════════════════════════════════════════════════════
        
        public static CompiledProgram Compile(string source, string programName = "Untitled",
                                               ChartRoomTier tier = ChartRoomTier.AdmiralsBridge)
        {
            var compiler = new PythonCompiler();
            var ast = compiler.Parse(source);
            
            var ctx = new CompilerContext { Tier = tier };
            ast.Compile(ctx);
            
            // Add implicit HALT if needed
            if (ctx.Instructions.Count == 0 ||
                ctx.Instructions[ctx.Instructions.Count - 1].Op != OpCode.HALT)
            {
                var last = ctx.Instructions.Count > 0 ? ctx.Instructions[ctx.Instructions.Count - 1] : default;
                if (last.Op != OpCode.JMP)
                    ctx.Emit(OpCode.HALT, comment: "end of program");
            }
            
            return new CompiledProgram
            {
                Name = programName,
                SourceCode = source,
                SourceLines = source.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None),
                Instructions = ctx.Instructions.ToArray(),
                Variables = ctx.Variables,
                CrewComponents = new Dictionary<string, string>(ctx.ObjectTypes),
                RequiredTier = tier,
                TierErrors = ctx.TierErrors
            };
        }
    }
    
    /// <summary>
    /// A compiled ship program ready for execution.
    /// </summary>
    public class CompiledProgram
    {
        public string Name;
        public string SourceCode;
        public string[] SourceLines;
        public Instruction[] Instructions;
        public Dictionary<string, int> Variables;
        
        /// <summary>Crew components declared in code (name → type)</summary>
        public Dictionary<string, string> CrewComponents = new Dictionary<string, string>();
        
        /// <summary>Chart room tier this was compiled for</summary>
        public ChartRoomTier RequiredTier;
        
        /// <summary>Tier violation errors (code too complex for chart room)</summary>
        public List<string> TierErrors = new List<string>();
        
        /// <summary>Did compilation succeed without tier violations?</summary>
        public bool IsValid => TierErrors.Count == 0;
        
        public string ToAssemblyListing()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"; {Name}");
            sb.AppendLine($"; {Instructions.Length} instructions | tier: {RequiredTier}");
            if (TierErrors.Count > 0)
            {
                sb.AppendLine($"; ⚠ {TierErrors.Count} TIER VIOLATIONS:");
                foreach (var err in TierErrors)
                    sb.AppendLine($";   {err}");
            }
            sb.AppendLine();
            
            for (int i = 0; i < Instructions.Length; i++)
            {
                var inst = Instructions[i];
                string addr = i.ToString("D4");
                string asm = inst.ToAssembly().PadRight(30);
                string crew = inst.RequiredCrew != CrewRole.None ? $"[{inst.RequiredCrew}]" : "";
                string comment = !string.IsNullOrEmpty(inst.Comment) ? $"; {inst.Comment}" : "";
                sb.AppendLine($"{addr}: {asm} {crew,14} {comment}");
            }
            
            return sb.ToString();
        }
    }
}
