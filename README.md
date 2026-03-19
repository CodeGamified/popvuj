# PopVuj: God-Sim

*"This is the account of how all was in suspense, all calm, in silence; all motionless, quiet, and empty was the expanse of the sky."*

Inspired by the Popol Vuh, the Mayan creation narrative. PopVuj is a god-sim where you rule from the heavens, shaping a minion village against rival civilizations through decrees, prophecy, and nature itself. You never lift a sword — you bend fate.

## Concept

Gods don't fight. Gods *influence*. Send prophets to deliver messages, smite the unreligious, unleash bears on heretics, summon rain, and shape weather. Your minions have traits, personalities, and a genetics system — religiousness is heritable, so selective breeding lets you cultivate a pious (or rebellious) civilization across generations.

More religion means more control but less freedom and less science. The tension is the game.

## Camera as Cosmology

The camera *is* the cosmology. The city is a **side-view cross-section** — a linear strip on the XY plane:

```
                   ↑ Heavens (camera pans up)
┌─────┬─────┬─────┬─────┬─────┬─────┐
│WALL │House│Chpl │Farm │House│WALL │  ← Surface (buildings rise above ground)
├─────┼─────┼─────┼─────┼─────┼─────┤  ← Ground line (Y = 0)
│WALL │Sewer│S.Den│Sewer│Sewer│WALL │  ← Sewers (always visible below)
└─────┴─────┴─────┴─────┴─────┴─────┘
                   ↓ Xibalba (underworld)
  ←──── X axis: city width (scroll left/right) ────→
```

- **X axis** = city width. The city grows sideways as far as it needs.
- **Y axis** = cosmological. Up is heavens, down is sewers.
- **No Z axis.** No forward/backward. Scrolling up can only mean "ascend to heavens."
- **Sewers are always visible** beneath the surface — you see both layers at once.

| Layer | View | Gameplay |
|---|---|---|
| **Heavens** | Sky above the walls | Issue decrees, manipulate weather, survey dominion |
| **Village** | Buildings between the walls | Inspect growth, population, macro influence |
| **Sewers** | Tunnels beneath the ground line | Disease, crime, desperation — always visible |

## Sewers

Beneath the houses, beneath the streets — hell on earth. The sewer layer is the underside of your civilization: a network of tunnels where the homeless sleep, thieves hide, and beggars scrape by. Minions who can't afford shelter end up here.

- **Disease** — sickness spawns in the sewers and spreads upward through the village. Plague, rats, contaminated water. Neglect the sewers, lose the surface.
- **Crime** — thieves and outcasts organize underground. Smuggling routes, black markets, assassin guilds. A thriving sewer means a corrupted village.
- **Refuge** — the poorest, the exiled, the faithless. Minions banished by your decrees don't vanish — they descend. A growing sewer population is a sign of divine overreach.
- **Uprising** — push too hard and the sewers push back. Underground religions, resistance movements, counter-prophets preaching against you.

The sewers are the cost of your rule made visible. Every smiting, every drought, every decree that crushes the weak — they all feed the world below.

## God Powers

Gods act through second-hand tools:

- **Prophets** — deliver divine messages, sway public opinion
- **Smiting** — punish the faithless, remind the village who's boss
- **Weather** — summon rain for crops, storms for enemies, drought for discipline
- **Wildlife** — unleash bears on heretics, bless hunters with prey
- **Omens** — shape dreams, eclipse the sun, write in the stars

## Minion Simulation

Each minion runs a needs-driven AI loop:

```
Birth → Traits (genetics + environment)
    → Needs (hunger, shelter, faith, social)
    → Utility scoring → Action selection
    → Pathfinding → Execution
    → Reproduction (trait inheritance)
    → Death
```

### Genetics

Traits are heritable with mutation. Key axes:

- **Religiousness** — how receptive to divine influence
- **Independence** — resistance to decrees, innovation potential
- **Strength / Intelligence / Charisma** — classic stats with genetic drift
- **Fertility** — population growth rate

Selective pressure across generations shapes your civilization's character.

## Architecture

```
PopVujBootstrap        → scene setup, wiring, camera cosmology
PopVujSimulationTime   → day/hour time, 100x max warp
CityGrid               → dual-layer tile map (surface + sewers)
CityRenderer           → 2.5D cube visualization, per-cell heights + colors
PopVujMatchManager     → population, faith, disease, crime, god powers
BuildingSlots          → per-type slot definitions, capacity scales with width
Minion                 → individual entity: position, needs, state machine
MinionManager          → spawning, task assignment, movement, slot occupancy
MinionRenderer         → small cubes walking the road / inside buildings
PopVujProgram          → deity script execution (20 ops/sec)
PopVujCompilerExtension → 26 opcodes (18 queries + 8 commands)
PopVujIOHandler        → bridges opcodes to city/match state
PopVujEditorExtension  → tap-to-code function list
PopVujInputProvider    → camera pan/zoom input
```

### Engine Submodule

The `engine/` folder is a shared Git submodule ([CodeGamified.Engine](PopVuj/Assets/Engine/CodeGamified.Engine/README.md)) providing:

- **Python compiler** — subset of Python → AST → RISC-like bytecode
- **Code executor** — time-scale-aware, deterministic instruction execution
- **TUI system** — retro terminal UI (windows, rows, colors, animations)
- **Editor** — in-game code editor with cursor, scrolling, syntax awareness
- **Persistence** — Git-based save/load for player scripts
- **Procedural** — runtime mesh generation for game objects

### Scripting

Players write Python scripts that issue divine commands:

```python
faith = get_faith()
heretics = get_heretics()
disease = get_disease()
if faith < 0.3:
    send_prophet()
if heretics > 3:
    smite()
if disease > 0.5:
    set_weather(0)
```

Scripts run on the shared CodeGamified execution model — deterministic at any time scale.

### Builtins

| Function | Returns |
|---|---|
| `get_population()` | Total living minions |
| `get_faith()` | Faith level (0.0–1.0) |
| `get_disease()` | Disease level (0.0–1.0) |
| `get_crime()` | Crime level (0.0–1.0) |
| `get_sewer_pop()` | Underground population |
| `get_weather()` | 0=clear, 1=rain, 2=storm, 3=drought |
| `get_heretics()` | Minions rejecting your rule |
| `get_houses()` | House count |
| `get_chapels()` | Chapel count |
| `get_farms()` | Farm count |
| `get_cell(slot)` | Surface cell type at slot |
| `get_sewer_cell(slot)` | Sewer cell type at slot |
| `send_prophet()` | Boost faith (+0.08) |
| `smite()` | Kill a heretic, scare the rest |
| `summon_bears()` | Unleash bears on heretics |
| `send_omen()` | Big faith surge (+0.15) |
| `set_weather(w)` | Change the weather |
| `build_chapel(slot)` | Place chapel at slot |
| `build_house(slot)` | Place house at slot |

## Design Philosophy

The Popol Vuh itself is the design document. Tzacol, Bitol, Alom, Qaholom tried mud, then wood, then finally corn to shape humanity. Expect a few failed prototypes before the corn catches.

### Scope Layers

1. **Core loop** — birth, traits, needs, faith, death (current focus)
2. **God powers** — prophets, smiting, weather, wildlife
3. **Village macro** — buildings, economy, technology, culture
4. **Rival civilizations** — competing villages, diplomacy, warfare
5. **Endgame** — apocalypse scenarios, ascension, mythological events

## Project Structure

```
PopVuj/Assets/
├── Core/                Bootstrap, simulation time
├── Crew/                Minion individuals: BuildingSlots, Minion, MinionManager, MinionRenderer
├── Game/                CityGrid, MatchManager, CityRenderer
├── Scenes/              World scenes
├── Scripting/           CompilerExtension, IOHandler, Program, Editor, Input
├── Settings/            URP render pipeline, quality profiles
└── engine/              Shared CodeGamified submodule
    ├── CodeGamified.Engine/     Compiler + executor
    ├── CodeGamified.TUI/        Terminal UI system
    ├── CodeGamified.Editor/     In-game code editor
    ├── CodeGamified.Camera/     Camera rig + modes
    ├── CodeGamified.Time/       Simulation time + warp
    ├── CodeGamified.Audio/      Audio/haptic bridges
    ├── CodeGamified.Persistence/ Git-based persistence
    ├── CodeGamified.Procedural/  Runtime mesh generation
    ├── CodeGamified.Settings/    Settings system
    ├── CodeGamified.Quality/     Quality tier management
    └── CodeGamified.Bootstrap/   Shared bootstrap base
```

## License

MIT — Copyright CodeGamified 2025-2026