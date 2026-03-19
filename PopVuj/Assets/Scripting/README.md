# SeaRäuber — Ship Scripting Engine

Style-transferred from [BitNaughts SatelliteCode](https://github.com/BitNaughts) (MIT License).  
Python subset → AST → RISC-like bytecode → crew-aware executor.

## Architecture

```
Python Source → [PythonCompiler] → AST → [CompilerContext] → Instructions[]
                                            ↑ ChartRoomTier gates constructs
                                            ↑ CrewRole annotates I/O

Instructions[] → [CodeExecutor] → MachineState
                     ↑ crew dispatch check before every I/O
                     ↑ time-scale aware (step <10x, batch ≥10x)
                     ↑ OnQuery/OnOrder callbacks → actual ship systems
```

## Chart Room Tiers

The chart room tier gates what code constructs the compiler accepts.  
Small ships get small code. This is also the **mobile UX solution** — sloops only need 5 lines.

| Tier | Ship Class | Allows | Max Lines |
|------|-----------|--------|-----------|
| Compass & Log | Sloop | `if/else`, assignment, `bell()`, `wait()` | ~5 |
| Chart Table | Schooner | + `while` loops, timers | ~15 |
| Navigator's Office | Brigantine | + `for`, subroutines (`call`/`ret`) | ~50 |
| War Room | Frigate, Galleon | + functions, state machines, combat ops | Unlimited |
| Admiral's Bridge | Flagship | Full Python subset, fleet ops, async | Unlimited |

## Crew as Threads

Every I/O instruction requires a crew role. No crew = instruction fails.  
Crew skill (0–1) modifies execution quality:

- **Navigator** — wind/position/heading queries. Low skill = noisy readings.
- **Helmsman** — heading, tack, jibe orders. Low skill = sluggish response.
- **Rigger** — sail trim, reef. Low skill = slower trim.
- **Gunner** — fire broadside. Requires War Room tier.
- **Lookout** — scan horizon. Low skill = late detection.
- **Carpenter** — repair hull, build.
- **Crane Op** — cargo transfer, lay keel. Requires Navigator's Office tier.
- **Bosun** — provisions, morale, crew count queries.
- **Signalman** — flag/lantern signals to other ships.

## Files

| File | Lines | Purpose |
|------|-------|---------|
| `Instruction.cs` | ~300 | OpCode enum, CpuFlags, CrewRole, ChartRoomTier, QueryId, OrderId, Instruction struct |
| `MachineState.cs` | ~340 | Registers R0-R7, stack, memory, PC, flags, crew dispatch, ship events |
| `CodeExecutor.cs` | ~420 | Time-scale aware execution, crew dispatch gating, I/O callbacks |
| `PythonCompiler.cs` | ~500 | AST parser + bytecode compiler, chart room tier gating |
| `ShipProgram.cs` | ~170 | MonoBehaviour wrapper, audio, source editor |
| `ShipProgramDatabase.cs` | ~200 | Ship class → default program, ScriptableObject |

## Example: Schooner Trader

```python
# Chart Table tier — while loops allowed
Navigator nav = new Navigator()
Helm helm = new Helm()
Rigging rig = new Rigging()

while True:
    wind = nav.get_wind()
    speed = nav.speed()
    helm.set_heading(wind + 30)
    if speed < 5:
        rig.trim(80)
    wait(5)
```

Compiles to:
```asm
0000: NOP                            ; crew Navigator nav
0001: NOP                            ; crew Helm helm
0002: NOP                            ; crew Rigging rig
0003: QUERY R0, #WindDirection    [Navigator]  ; nav.get_wind() → R0
0004: STORE_MEM [0], R0                        ; store to wind
0005: QUERY R0, #Speed            [Navigator]  ; nav.speed() → R0
0006: STORE_MEM [1], R0                        ; store to speed
0007: LOAD_MEM R0, [0]                         ; load wind
0008: LOAD_CONST R1, 30000                     ; load 30 (scaled)
0009: ADD R0, R1                               ; add
0010: ORDER #SetHeading, R1       [Helmsman]   ; helm.set_heading()
0011: LOAD_MEM R0, [1]                         ; load speed
0012: LOAD_CONST R1, 5000                      ; load 5 (scaled)
0013: CMP R0, R1                               ; compare <
...
0018: ORDER #SetSailTrim, R1      [Rigger]     ; rig.trim()
0019: LOAD_CONST R0, 5000                      ; load 5 (scaled)
0020: WAIT R0                                  ; wait 5 seconds
0021: JMP @3                                   ; loop back
0022: HALT                                     ; end of program
```

## Wiring to Ship Systems

```csharp
var program = GetComponent<ShipProgram>();
var hull = GetComponent<ShipHull>();
var sail = GetComponent<ShipSail>();
var controller = GetComponent<ShipController>();

program.OnQuery = (queryId) => queryId switch
{
    QueryId.WindDirection => controller.GetWindDirection(),
    QueryId.Speed => hull.Speed,
    QueryId.Heading => hull.Heading,
    QueryId.HullIntegrity => hull.Integrity,
    _ => 0f
};

program.OnOrder = (orderId, value, crewSkill) =>
{
    switch (orderId)
    {
        case OrderId.SetHeading:
            controller.SetTargetHeading(value);
            break;
        case OrderId.SetSailTrim:
            sail.SetTrim(value * crewSkill); // skill modifies quality
            break;
    }
};
```

## Key Differences from BitNaughts

| BitNaughts | SeaRäuber | Why |
|-----------|-----------|-----|
| `SENSOR_READ` | `QUERY` | Reading ship state, not sensors |
| `TRANSMIT` | `SIGNAL` | Ship-to-ship flags, not radio |
| `OUTPUT` | `ORDER` | Commanding crew, not hardware |
| `beep()` | `bell()` | Ship's bell |
| `Radio r = new Radio()` | `Helm h = new Helm()` | Crew roles, not electronics |
| No gating | Chart room tier | Mobile UX + progression |
| No dispatch | Crew dispatch | Crew = threads metaphor |
| NORAD # database | ShipClass database | Ship taxonomy |

## Status

Phase 3 of SeaRäuber engine. Core interpreter complete. Next:
- [ ] Wire to ShipController scripting API
- [ ] Ship's Log visualization (register/memory inspector)
- [ ] WHEN/THEN visual rule builder (mobile editor)
- [ ] Fleet-level signaling protocol
