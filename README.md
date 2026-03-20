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
PopVujCompilerExtension → 44 opcodes (23 queries + 21 commands, incl. harbor)
PopVujIOHandler        → bridges opcodes to city/match state
PopVujEditorExtension  → tap-to-code function list
PopVujInputProvider    → camera pan/zoom input
Ship                   → modular hull: width→class, crew slots, cargo hold, state machine
HarborManager          → ships, trade routes, crane ops, anchorage positioning
ShipRenderer           → side-view ship profiles with masts, sails, cannons
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

## Harbor — The Right Edge

The city strip ends where land meets ocean. The rightmost slots of the grid are the **harbor district** — a transition zone where solid ground gives way to wooden piers stretching out over water. Every city has this coastal edge; it's not optional, it's geography.

The harbor is a **first-class starter structure**, placed at generation just like the Chapel. Every civilization is born with both a chapel and a harbor — one facing the heavens, the other facing the sea. Their relative sizes define the civilization's character from turn one:

```
         LAND (village)              │  HARBOR   │     OCEAN
 ┌──┬──┬──────┬──┬──────┬──┬────────┼───┬───┬───┼──────────────┐
 │🌲│  │Chapel│🌲│House │  │Workshop│SY │Pr │Cr │  ⛵ docked   │
 ├──┴──┴──────┴──┴──────┴──┴────────┼───┴───┴───┤  ⛵ arriving │
 │  │  │▓Crypt▓│  │░Den░│  │Tunnel │░Drydock░│~~│  ⛵ departing│
 └──┴──┴──────┴──┴──────┴──┴───────┴─────────┴──┴──────────────┘
         ← faith                       greed →
```

### The Piety–Commerce Axis

The Chapel and Harbor are the two poles of civilization. Their widths signify the balance between heaven and earth, between the spiritual and the mercantile:

| Chapel width | Harbor width | Civilization archetype |
|---|---|---|
| 4w Chapel | 1w Shipyard, 1w Pier | **Monastic enclave** — deep faith, sparse trade, self-sufficient |
| 2w Chapel | 1w Shipyard, 1w Pier, 1w Crane | **Balanced start** (default) — the tension is live |
| 1w Chapel | 3w Shipyard, 2w Pier, 2w Crane | **Hanseatic league** — merchant empire, faith is an afterthought |

The god's first strategic choice: expand the chapel or expand the dock? Every tile of chapel width is a tile not spent on shipyard width, and vice versa. The city strip is finite — **piety and commerce compete for the same ground**.

A wide chapel generates more faith, deeper crypts, stronger divine influence — but the village stays poor, reliant on farms, isolated. A wide harbor generates more ships, more trade income, more exotic goods flowing in — but minions drift from worship, faithlessness rises, and the sewers fill with smugglers.

### Starter Layout

Every city generates with:
- **Chapel (2w)** at center — the spiritual anchor
- **Shipyard (1w)** at right edge − 2 — the first shore building
- **Pier (1w)** at right edge − 1 — walkway over water
- **Crane (1w)** at right edge — the loading arm

This is the balanced start. The god inherits a village that could become anything.

### Zone Layout

The harbor zone occupies the rightmost N slots of the city strip, divided into two sub-zones:

| Sub-zone | Grid region | Ground | What's here |
|---|---|---|---|
| **Shore** | Last 2–3 land slots | Solid (sand/gravel) | Shipyard, warehouses, rope walks |
| **Pier** | Extends right over water | Wooden planks over water | Cranes, bollards, gangplanks, docked ships |

The shore is buildable land. The piers are wooden structures that extend the walkable surface over water. Ships travel on the road plane (Z=0) and dock along the pier’s X extent — the total dockable length equals the sum of all Pier+Crane cell widths. The pier must be longer than the combined widths of all docked ships.

### Harbor Buildings

Three new building types occupy the harbor zone:

#### Shipyard (`CellType.Shipyard`)
Built on shore slots. Where ships are constructed from wood and rope.
- **Slots**: 2 + width × 2 (1 foreman + workers)
- **Roles**: `Foreman`, `Shipwright`
- **Consumes**: Wood (from tree harvesting, existing `CargoKind.Log`)
- **Produces**: Ships (over time — a ship is a multi-day project)
- **Sewer shadow**: `Drydock` — a flooded excavation beneath, where hulls are laid

A 3-wide shipyard might have a foreman + 6 shipwrights all hammering planks. Wider shipyard = faster ship production. Ship quality also scales — a 1-wide yard makes canoes, a 5-wide yard makes galleons.

#### Pier (`CellType.Pier`)
Built over water, extending rightward from the shoreline. Piers are the connective tissue between land and sea.
- **Slots**: width × 1 (foot traffic — dockers walk the pier)
- **Roles**: `Docker` (hauling cargo along the pier)
- **Not a production building** — it's infrastructure. Cargo flows across it.
- **Sewer shadow**: None (it's over water — the underside is visible wave/tide)
- **Visual**: Wooden planks, rope railings, barnacles underneath

Piers must be contiguous from shore. You can't float a pier in open water.

#### Crane (`CellType.Crane`)
Built on pier slots. Vertical wooden structures with rope-and-pulley arms that swing cargo between pier and ship.
- **Slots**: 1 per crane (1 `CraneOperator`)
- **Roles**: `CraneOperator`
- **Function**: Transfers `Cargo` between dock-side and ship hold
- **Throughput**: 1 cargo unit per crane per ~4 sim-seconds
- **Visual**: Tall wooden A-frame with a swinging arm, rope descending to water level

More cranes = faster loading. But cranes need operators, pulling minions from other jobs. The god must balance port throughput against village labor needs.

### Ships

Ships are **modular "buildings"** — their tile width determines hull class, just like a wider chapel has more pews. They exist in the anchorage zone to the right of the piers.

```
Ship lifecycle:
  [Shipyard builds hull] → [Launch] → [Dock at pier] → [Load cargo via cranes]
       → [Depart on trade route] → ... → [Return with foreign goods] → [Unload] → repeat
```

#### Hull Classes (width = ship size)

| Width | Hull Class | Cargo | Crew | Gunners | Build Cost |
|---|---|---|---|---|---|
| 1w | **Canoe** | 2 | 1 (paddler) | 0 | 2 wood |
| 2w | **Sloop** | 4 | 3 (captain + 2 sailors) | 0 | 5 wood |
| 3w | **Brigantine** | 8 | 6 (captain + 3 sailors + 2 gunners) | 2 | 10 wood |
| 4w | **Frigate** | 12 | 10 (captain + 5 sailors + 4 gunners) | 4 | 18 wood |
| 5w | **Ship of the Line** | 18 | 15 (captain + 8 sailors + 6 gunners) | 6 | 30 wood |

#### Ship Slots (crewing like buildings)

Ships have crew slots — just like a chapel has a preacher slot and pew slots. Crewing a ship works identically to attending service at a church: minions walk to the anchorage, board the vessel, and occupy their assigned position.

| Slot | Role | Function |
|---|---|---|
| 0 | **Captain** | Commands the vessel. Always slot 0. Speed bonus. |
| 1..N | **Sailor** | Rigging, sails, oars. More sailors = faster voyages. |
| Last slots | **Gunner** | Man the cannons (Brigantine+ only). Combat defense. |

A ship with empty slots still sails, but slower and with higher risk. A fully crewed Ship of the Line is a floating fortress.

#### Ship Properties
- **Speed**: Base from hull class, scaled by crew ratio. Empty crew = 30% speed, full crew = 100%.
- **Condition**: 1.0 (pristine) → degrades per voyage → 0.0 (sinking). Repair at shipyard costs 1 wood.
- **Cargo Hold**: Loaded/unloaded by cranes. Capacity determined by width.

#### Ship States
| State | Where | What's happening |
|---|---|---|
| `Building` | Shipyard (on land) | Shipwrights assembling the hull |
| `Launched` | Docked at pier | Freshly built, empty, awaiting crew |
| `Loading` | Docked at pier (crane range) | Cranes swinging cargo aboard |
| `Unloading` | Docked at pier (crane range) | Cranes pulling cargo off |
| `Departing` | Moving right → | Sailing away on a trade route |
| `Voyage` | Off-screen right | Gone for N sim-days |
| `Arriving` | Moving left ← | Returning from voyage |
| `Idle` | Docked at pier | Waiting for orders |

Ships travel on the road surface (Z=0), sliding left/right along the pier’s X extent. They pack left-to-right by their widths. Any crane whose X range overlaps a ship can load/unload it — N cranes with N operators service N ships simultaneously. If the pier is too short for all ships, arriving vessels wait offshore until space opens.

### Trade Routes

When a ship departs, it enters a **trade route** — an abstracted voyage with a destination, duration, and outcome.

| Route | Duration | Exports → | ← Returns | Risk |
|---|---|---|---|---|
| **Coastal fishing** | 1 day | Nothing | Grain (fish) | Low |
| **Timber islands** | 2 days | Nothing | Logs | Low |
| **Nearby village** | 3 days | Crates | Crates + Stone | Medium |
| **Distant empire** | 7 days | Grain + Crates | Gold + Exotic goods | High |
| **Xibalba crossing** | ??? | Offerings | ??? | Mythological |

Risk means ships can be lost — storms, pirates, sea monsters. Weather affects risk: `Storm` weather doubles loss chance. A god who summons storms to punish heretics might also sink their own trade fleet.

**Trade value**: Returned goods boost the village economy. Gold increases score. Exotic goods could unlock new building types or god powers. Fish feeds the village (reducing hunger pressure on farms).

### New Cargo Types

Extending the existing `CargoKind` enum:

| CargoKind | Source | Destination | Visual |
|---|---|---|---|
| `Fish` | Returning fishing boats | Market / direct consumption | Silver-blue sack |
| `Rope` | Workshop (new recipe) | Shipyard / Pier construction | Coiled brown bundle |
| `Plank` | Workshop (processed Log) | Shipyard / Pier construction | Flat lumber stack |
| `TradeCrate` | Market (packed for export) | Ship hold (via crane) | Stamped wooden crate |
| `ExoticGoods` | Returning trade ships | Market (unpacked for sale) | Ornate chest |

### New Slot Roles

Extending `SlotRole`:

| Role | Building | What they do |
|---|---|---|
| `Shipwright` | Shipyard | Builds/repairs ships. Consumes wood. |
| `Foreman` | Shipyard | Supervises construction. 1 per shipyard. Speed bonus. |
| `Docker` | Pier | Carries cargo between warehouse and crane foot |
| `CraneOperator` | Crane | Operates the crane arm to swing cargo ship↔pier |
| `Sailor` | Ship | Crews the vessel. More sailors = faster voyage, better combat |

### Minion Harbor Workflow

A minion assigned to harbor work follows this loop:

```
[Idle on road] → need = Work → utility picks harbor job
  → Walk right toward harbor
    → If Shipwright: enter Shipyard slot, hammer ship (consume Wood, advance build timer)
    → If Docker: walk pier, pick up cargo at warehouse end, carry to crane foot, drop off
    → If CraneOperator: stand at crane, swing arm each tick (cargo pier→ship or ship→pier)
    → If Sailor: board ship, disappear from road (returns when ship docks)
  → Task complete → leave slot → walk back toward village
```

Dockers are the labor bottleneck. A single crane can only move cargo as fast as dockers supply it. The whole pipeline is:

```
Workshop produces Crate → Docker carries to pier → CraneOperator loads onto ship
Ship returns with Fish → CraneOperator unloads → Docker carries to market
```

Every step needs a minion. Every minion at the docks is a minion not farming, not worshipping, not building. The god's tension: **do you invest in trade or in faith?**

### Harbor Economy Loop

```
         ┌──────────────┐
         │   WORKSHOP   │──── produces Crates/Planks/Rope ────┐
         └──────────────┘                                      │
                                                               ▼
         ┌──────────────┐         ┌──────┐         ┌────────────────┐
         │    FOREST    │── Logs→ │DOCKER│ ──────→ │   SHIPYARD     │
         └──────────────┘         └──────┘         │ (builds ships) │
                                                   └───────┬────────┘
                                                           │ Ship launched
                                      ┌────────────────────▼───────────┐
            ┌──────────┐              │          PIER / CRANES         │
            │  MARKET  │◄── Docker ◄──│  CraneOp unloads returning     │
            │(sells to │              │  CraneOp loads departing        │
            │ village) │              └────────────────┬────────────────┘
            └──────────┘                               │
                                                       ▼
                                              ┌─────────────────┐
                                              │   TRADE ROUTE   │
                                              │  (days at sea)  │
                                              └─────────────────┘
```

### Sewer Implications

The harbor has its own underworld:

- **Drydock** (beneath Shipyard) — flooded excavation. Smugglers hide contraband in unfinished hulls. Pirates recruit here.
- **Pier underside** — no sewer, but thieves hide in the pilings. Rats nest where rope meets water. Disease vector.
- **Smuggler routes** — if Crime is high, a shadow economy emerges: ships carry illicit cargo, bypassing the market. Income for the sewer population, lost tax revenue for the village.

A neglected harbor becomes a pirate haven. Sewer dwellers who reach the docks can stow away on ships and establish criminal trade networks.

### God Powers at Sea

| Power | Harbor effect |
|---|---|
| **Storm** | Sinks ships at sea. Damages docked ships. Floods pier. Risk/reward: storms punish heretics but destroy trade. |
| **Clear weather** | Faster voyages. Reduced risk. Fishing yields double. |
| **Drought** | No rain → no disease on docks, but fish stocks decline. Fishing yields halved. |
| **Smite** | Can target docked ships — lightning strike burns the vessel. Dramatic. |
| **Prophet** | Blesses a departing ship — guaranteed safe voyage, bonus cargo on return. |
| **Omen** | Sailors see signs in the sky. High-faith crews interpret omens favorably (speed bonus). Low-faith crews panic (mutiny chance). |

### Scripting Builtins (New)

| Function | Returns / Effect |
|---|---|
| `get_ships()` | Total ships (docked + at sea) |
| `get_ships_docked()` | Ships currently at pier |
| `get_ships_at_sea()` | Ships on active voyages |
| `get_harbor_workers()` | Minions employed in harbor roles |
| `get_trade_income()` | Cumulative trade value this match |
| `build_pier(slot)` | Extend a pier at the given slot |
| `build_crane(slot)` | Place a crane on an existing pier slot |
| `build_shipyard(slot)` | Place a shipyard on a shore slot |
| `launch_ship()` | Launch the next completed ship |
| `send_trade(route)` | Dispatch a docked ship on a trade route (0–4) |
| `bless_ship()` | Prophet blesses next departing ship |

### Visual Treatment

The harbor is the rightmost visible zone. As the player scrolls right past the last building, the ground texture transitions:

1. **City road** → cobblestone, buildings on both sides
2. **Shore** → sand/gravel, shipyard structures, rope coils
3. **Pier** → wooden planks over water, cranes rising above
4. **Water** → animated blue surface, ships bobbing

Below the pier, instead of sewers, the player sees **water and pilings** — the wooden support structure holding up the pier. Fish swim. Barnacles cluster. In stormy weather, waves crash against the supports.

Ships are rendered as side-view profiles (matching the 2.5D aesthetic), sliding left and right in the water zone. A docked ship sits flush against the pier end, gangplank extended.

### Implementation Phases

**Phase 1 — Shore zone & Shipyard**
- Extend `CellType` with `Shipyard`, `Pier`, `Crane`
- Add `SlotRole.Shipwright`, `Foreman`
- Harbor zone: rightmost N slots marked as shore
- Shipyard building with wood consumption and ship build timer
- `StructureBlueprint` for shipyard interior (drydock frame, tools, timber stacks)
- `SewerType.Drydock`

**Phase 2 — Piers & Cranes**
- Pier as an over-water building type with contiguity constraint
- Crane building on pier slots
- `SlotRole.Docker`, `CraneOperator`
- Docker cargo-carry workflow (warehouse → crane foot)
- Crane loading/unloading animation tick
- `StructureBlueprint` for pier (planks, rope rails) and crane (A-frame, pulley)

**Phase 3 — Ships & Trade**
- Ship entity (not a grid cell — a free-floating object in anchorage)
- Ship states: Building → Launched → Loading → Departing → Voyage → Arriving → Unloading
- Trade route system with duration, risk, and cargo exchange
- New `CargoKind` entries: `Fish`, `Rope`, `Plank`, `TradeCrate`, `ExoticGoods`
- Sailor role — minions board ships and leave the road
- Ship rendering in the water zone

**Phase 4 — Integration & Balance**
- Weather effects on trade (storms sink, clear speeds up)
- God power interactions (bless ship, smite ship, omen at sea)
- Sewer interactions (smuggler routes, pirate recruitment)
- Scripting builtins for harbor queries and commands
- Balancing: labor cost vs. trade income, faith drain from secular harbor work

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
├── Crew/                Minion individuals: BuildingSlots, Minion, MinionManager, MinionRenderer, Cargo
├── Game/                CityGrid, MatchManager, CityRenderer, Ship, HarborManager, ShipRenderer
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