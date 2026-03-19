// SeaRäuber — Ship Machine State
// Style-transferred from BitNaughts SatelliteCode (MIT License)
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace SeaRauber.Scripting
{
    /// <summary>
    /// Complete machine state of a ship's onboard chart room computer.
    /// Registers, stack, memory, PC, flags, crew dispatch tracking.
    /// </summary>
    public class MachineState
    {
        // ═══════════════════════════════════════════════════════════════
        // CONFIGURATION
        // ═══════════════════════════════════════════════════════════════
        
        public const int REGISTER_COUNT = 8;
        public const int MAX_STACK = 64;
        public const int MAX_MEMORY = 256;
        
        // ═══════════════════════════════════════════════════════════════
        // STATE
        // ═══════════════════════════════════════════════════════════════
        
        public readonly float[] Registers = new float[REGISTER_COUNT];
        public readonly Stack<float> Stack = new Stack<float>(MAX_STACK);
        public readonly Dictionary<string, float> Memory = new Dictionary<string, float>();
        public readonly Dictionary<int, string> MemoryNames = new Dictionary<int, string>();
        public readonly Dictionary<string, int> NameToAddress = new Dictionary<string, int>();
        private int _nextAddress = 0;
        
        public int PC { get; set; } = 0;
        public CpuFlags Flags { get; set; } = CpuFlags.None;
        public bool IsHalted { get; set; } = false;
        public bool IsWaiting { get; set; } = false;
        public float WaitTimeRemaining { get; set; } = 0f;
        public long InstructionsExecuted { get; set; } = 0;
        public long CycleCount { get; set; } = 0;
        
        // ═══════════════════════════════════════════════════════════════
        // CREW DISPATCH STATE
        // ═══════════════════════════════════════════════════════════════
        
        /// <summary>Crew available per role. Zero = instruction fails/queues.</summary>
        public readonly Dictionary<CrewRole, int> CrewAvailable = new Dictionary<CrewRole, int>();
        
        /// <summary>Crew skill per role (0-1). Higher = better results, faster execution.</summary>
        public readonly Dictionary<CrewRole, float> CrewSkill = new Dictionary<CrewRole, float>();
        
        /// <summary>Queue of instructions waiting for crew to become available.</summary>
        public readonly Queue<Instruction> CrewWaitQueue = new Queue<Instruction>();
        
        /// <summary>Last crew dispatch result for visualization.</summary>
        public CrewDispatchResult LastDispatch { get; set; }
        
        // ═══════════════════════════════════════════════════════════════
        // EXECUTION TRACE (for Ship's Log visualization)
        // ═══════════════════════════════════════════════════════════════
        
        public Instruction? LastInstruction { get; set; }
        public int LastMemoryAccess { get; set; } = -1;
        public bool LastMemoryWasWrite { get; set; }
        public int LastRegisterModified { get; set; } = -1;
        public Queue<ShipEvent> OutputEvents { get; } = new Queue<ShipEvent>();
        
        // ═══════════════════════════════════════════════════════════════
        // MEMORY MANAGEMENT (identical to BitNaughts — proven)
        // ═══════════════════════════════════════════════════════════════
        
        public int GetOrAllocateAddress(string name)
        {
            if (NameToAddress.TryGetValue(name, out int addr))
                return addr;
            
            addr = _nextAddress++;
            NameToAddress[name] = addr;
            MemoryNames[addr] = name;
            Memory[name] = 0f;
            return addr;
        }
        
        public float ReadMemory(int address)
        {
            LastMemoryAccess = address;
            LastMemoryWasWrite = false;
            if (MemoryNames.TryGetValue(address, out string name))
                return Memory.TryGetValue(name, out float val) ? val : 0f;
            return 0f;
        }
        
        public float ReadMemory(string name)
        {
            if (NameToAddress.TryGetValue(name, out int addr))
                LastMemoryAccess = addr;
            LastMemoryWasWrite = false;
            return Memory.TryGetValue(name, out float val) ? val : 0f;
        }
        
        public void WriteMemory(int address, float value)
        {
            LastMemoryAccess = address;
            LastMemoryWasWrite = true;
            if (MemoryNames.TryGetValue(address, out string name))
            {
                Memory[name] = value;
            }
            else
            {
                string autoName = $"_mem{address}";
                MemoryNames[address] = autoName;
                NameToAddress[autoName] = address;
                Memory[autoName] = value;
            }
        }
        
        public void WriteMemory(string name, float value)
        {
            GetOrAllocateAddress(name);
            if (NameToAddress.TryGetValue(name, out int addr))
            {
                LastMemoryAccess = addr;
                LastMemoryWasWrite = true;
            }
            Memory[name] = value;
        }
        
        // ═══════════════════════════════════════════════════════════════
        // REGISTER ACCESS
        // ═══════════════════════════════════════════════════════════════
        
        public float GetRegister(int index)
        {
            if (index < 0 || index >= REGISTER_COUNT)
            {
                Debug.LogError($"[MachineState] Invalid register R{index}");
                return 0f;
            }
            return Registers[index];
        }
        
        public void SetRegister(int index, float value)
        {
            if (index < 0 || index >= REGISTER_COUNT)
            {
                Debug.LogError($"[MachineState] Invalid register R{index}");
                return;
            }
            Registers[index] = value;
            LastRegisterModified = index;
        }
        
        // ═══════════════════════════════════════════════════════════════
        // FLAG MANAGEMENT
        // ═══════════════════════════════════════════════════════════════
        
        public void SetCompareFlags(float a, float b)
        {
            float diff = a - b;
            Flags = CpuFlags.None;
            if (Mathf.Approximately(diff, 0f))
                Flags |= CpuFlags.Zero;
            if (diff < 0f)
                Flags |= CpuFlags.Negative;
        }
        
        public bool IsZero => (Flags & CpuFlags.Zero) != 0;
        public bool IsNegative => (Flags & CpuFlags.Negative) != 0;
        
        // ═══════════════════════════════════════════════════════════════
        // CREW MANAGEMENT
        // ═══════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Check if crew is available to execute an instruction.
        /// Returns dispatch result with quality modifier.
        /// </summary>
        public CrewDispatchResult CanDispatch(CrewRole role)
        {
            if (role == CrewRole.None)
                return new CrewDispatchResult { Status = DispatchStatus.Executed, QualityModifier = 1f };
            
            if (!CrewAvailable.TryGetValue(role, out int count) || count <= 0)
                return new CrewDispatchResult { Status = DispatchStatus.NoCrew, QualityModifier = 0f, Role = role };
            
            float skill = CrewSkill.TryGetValue(role, out float s) ? s : 0.5f;
            return new CrewDispatchResult
            {
                Status = DispatchStatus.Executed,
                QualityModifier = skill,
                Role = role
            };
        }
        
        /// <summary>
        /// Set crew roster for this ship's runtime.
        /// </summary>
        public void SetCrew(CrewRole role, int count, float skill = 0.5f)
        {
            CrewAvailable[role] = count;
            CrewSkill[role] = Mathf.Clamp01(skill);
        }
        
        // ═══════════════════════════════════════════════════════════════
        // RESET
        // ═══════════════════════════════════════════════════════════════
        
        public void Reset()
        {
            for (int i = 0; i < REGISTER_COUNT; i++)
                Registers[i] = 0f;
            
            Stack.Clear();
            Memory.Clear();
            MemoryNames.Clear();
            NameToAddress.Clear();
            _nextAddress = 0;
            
            PC = 0;
            Flags = CpuFlags.None;
            IsHalted = false;
            IsWaiting = false;
            WaitTimeRemaining = 0f;
            InstructionsExecuted = 0;
            CycleCount = 0;
            
            LastInstruction = null;
            LastMemoryAccess = -1;
            LastRegisterModified = -1;
            OutputEvents.Clear();
            CrewWaitQueue.Clear();
            // Don't clear CrewAvailable/CrewSkill — those are set by ShipHull
        }
        
        // ═══════════════════════════════════════════════════════════════
        // VISUALIZATION
        // ═══════════════════════════════════════════════════════════════
        
        public string ToShipLogString()
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("═══ CHART ROOM REGISTERS ═══");
            for (int i = 0; i < REGISTER_COUNT; i++)
            {
                string highlight = (i == LastRegisterModified) ? "►" : " ";
                sb.AppendLine($"{highlight}R{i}: {Registers[i]:F2}");
            }
            
            sb.AppendLine($"\nFLAGS: {Flags}");
            sb.AppendLine($"PC: {PC}");
            
            sb.AppendLine("\n═══ STACK ═══");
            if (Stack.Count == 0)
                sb.AppendLine("  [empty]");
            else
                foreach (var val in Stack)
                    sb.AppendLine($"  {val:F2}");
            
            sb.AppendLine("\n═══ SHIP'S SLATE ═══");
            foreach (var kvp in Memory)
            {
                int addr = NameToAddress.TryGetValue(kvp.Key, out int a) ? a : -1;
                string highlight = (addr == LastMemoryAccess) ? "►" : " ";
                sb.AppendLine($"{highlight}{kvp.Key}: {kvp.Value:F2}");
            }
            
            sb.AppendLine("\n═══ CREW STATUS ═══");
            foreach (var kvp in CrewAvailable)
            {
                float skill = CrewSkill.TryGetValue(kvp.Key, out float s) ? s : 0f;
                sb.AppendLine($"  {kvp.Key}: {kvp.Value} crew (skill: {skill:P0})");
            }
            
            return sb.ToString();
        }
    }
    
    // ═══════════════════════════════════════════════════════════════
    // CREW DISPATCH TYPES
    // ═══════════════════════════════════════════════════════════════
    
    public enum DispatchStatus
    {
        /// <summary>Crew executed the instruction.</summary>
        Executed,
        /// <summary>Crew busy — instruction queued.</summary>
        Queued,
        /// <summary>No crew in this role — instruction failed.</summary>
        NoCrew,
        /// <summary>Crew too weak (scurvy, wounded) — degraded execution.</summary>
        Degraded
    }
    
    public struct CrewDispatchResult
    {
        public DispatchStatus Status;
        public float QualityModifier;   // 0-1, from crew skill
        public CrewRole Role;
    }
    
    /// <summary>
    /// An event produced by ship I/O instructions.
    /// Fed to Ship's Log visualization.
    /// </summary>
    public struct ShipEvent
    {
        public enum EventType
        {
            Bell,               // Ship's bell (replaces satellite beep)
            Signal,             // Flag/lantern signal to other ships
            Order,              // Crew order issued
            Query,              // State query result
            LogEntry,           // Ship's log write
            CrewDispatchFailed  // Instruction failed — no crew
        }
        
        public EventType Type;
        public float Value;
        public int Channel;
        public double SimulationTime;
        public CrewRole DispatchedTo;
        public DispatchStatus DispatchResult;
        
        public ShipEvent(EventType type, float value, int channel, double simTime,
                        CrewRole crew = CrewRole.None, DispatchStatus dispatch = DispatchStatus.Executed)
        {
            Type = type;
            Value = value;
            Channel = channel;
            SimulationTime = simTime;
            DispatchedTo = crew;
            DispatchResult = dispatch;
        }
    }
}
